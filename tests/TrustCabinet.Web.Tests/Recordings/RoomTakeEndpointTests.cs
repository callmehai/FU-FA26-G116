using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>ADR-0041 — the room mic streams a take in parts; switching it off appends it to the meeting's room recording.</summary>
public sealed class RoomTakeEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const int Part = RecordingFixture.RoomPartBytes;

    /// <summary>What the browser writes first: 16 kHz mono 16-bit PCM whose length is not known yet.</summary>
    private static byte[] Header(int sampleRate = 16_000)
    {
        var h = new byte[44];
        "RIFF"u8.CopyTo(h);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), uint.MaxValue);
        "WAVEfmt "u8.CopyTo(h.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(24), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(28), (uint)sampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(34), 16);
        "data"u8.CopyTo(h.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(40), uint.MaxValue - 36);
        return h;
    }

    /// <summary>A take of <paramref name="bytes"/> bytes, header included, filled with <paramref name="fill"/>.</summary>
    private static byte[] Tape(int bytes, byte fill)
    {
        var tape = Enumerable.Repeat(fill, bytes).ToArray();
        Header().CopyTo(tape, 0);
        return tape;
    }

    private static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private static string Takes(RecordingFixture.Scenario s) => $"{s.Url}/takes";

    private static Task<HttpResponseMessage> Begin(HttpClient client, RecordingFixture.Scenario s, bool consent = true)
        => client.PostAsJsonAsync(Takes(s), new { consentConfirmed = consent, consentPurpose = "Lập biên bản phiên họp" });

    private static Task<HttpResponseMessage> Put(HttpClient client, Guid take, int part, byte[] bytes, bool final = false)
    {
        var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return client.PutAsync($"/api/recordings/{take}/parts/{part}{(final ? "?final=true" : "")}", body);
    }

    private static Task<HttpResponseMessage> Complete(HttpClient client, Guid take)
        => client.PostAsync($"/api/recordings/{take}/complete", null);

    private static async Task<Guid> Open(HttpClient client, RecordingFixture.Scenario s, bool consent = true)
    {
        var opened = await Begin(client, s, consent);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var body = await opened.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Part, body.GetProperty("partSize").GetInt32());
        return body.GetProperty("recordingId").GetGuid();
    }

    /// <summary>Streams the tape as the browser does: full parts, then a short last one, then the mic goes off.</summary>
    private async Task<JsonElement> Record(HttpClient client, Guid take, byte[] tape)
    {
        var parts = tape.Chunk(Part).ToArray();
        for (var i = 0; i < parts.Length; i++)
        {
            var final = i == parts.Length - 1 && parts[i].Length < Part;
            Assert.Equal(HttpStatusCode.NoContent, (await Put(client, take, i + 1, parts[i], final)).StatusCode);
        }
        return await AssertCode(await Complete(client, take), 200, "MSG363");
    }

    private async Task ProcessAll()
    {
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) return;
        }
    }

    private async Task<byte[]> StoredBytes(Guid recordingId)
    {
        await using var db = fixture.Db();
        var path = (await db.Recordings.SingleAsync(r => r.Id == recordingId)).StoragePath;
        return await File.ReadAllBytesAsync(Path.Combine(fixture.StorageRoot, path));
    }

    [Fact]
    public async Task The_first_take_streams_in_parts_and_becomes_the_meetings_room_recording()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using var secretary = fixture.Client(s.Secretary);
        using var member = fixture.Client(s.Member);
        Assert.True((await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("canRecord").GetBoolean());
        Assert.False((await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("canRecord").GetBoolean());

        var take = await Open(secretary, s);
        // Everyone sees the room is being recorded; nobody hears the take before the mic goes off.
        var open = Assert.Single((await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
        Assert.Equal("Pending", open.GetProperty("status").GetString());
        Assert.False(open.GetProperty("canClose").GetBoolean());
        Assert.False(open.GetProperty("canErase").GetBoolean());
        Assert.Equal(s.Secretary, open.GetProperty("createdById").GetGuid());
        Assert.True((await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("canClose").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/recordings/{take}/playback")).StatusCode);

        var tape = Tape(Part + 1000, 7);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 1, tape[..Part])).StatusCode);
        // A part sent again (its answer was lost) lands on the same place.
        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 1, tape[..Part])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 2, tape[Part..], final: true)).StatusCode);
        var saved = await AssertCode(await Complete(secretary, take), 200, "MSG363");
        Assert.Equal(take, saved.GetProperty("recordingId").GetGuid());
        Assert.False(saved.GetProperty("appended").GetBoolean());

        var item = Assert.Single((await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
        Assert.Equal("Available", item.GetProperty("status").GetString());
        Assert.Equal("BrowserCapture", item.GetProperty("source").GetString());
        Assert.Equal("Pending", item.GetProperty("transcriptStatus").GetString());
        Assert.Equal((Part + 1000 - 44) * 1000L / 32_000, item.GetProperty("durationMs").GetInt64());
        Assert.Equal("Lập biên bản phiên họp", item.GetProperty("consentPurpose").GetString());
        var only = Assert.Single(item.GetProperty("takes").EnumerateArray());
        Assert.Equal(0, only.GetProperty("offsetMs").GetInt64());
        Assert.Equal(fixture.Clock.Now, only.GetProperty("startedAt").GetDateTimeOffset());

        // The header went out before the length was known; saving writes it in.
        var stored = await StoredBytes(take);
        Assert.Equal(tape.Length, stored.Length);
        Assert.Equal((uint)tape.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(stored.AsSpan(4)));
        Assert.Equal((uint)tape.Length - 44, BinaryPrimitives.ReadUInt32LittleEndian(stored.AsSpan(40)));
        Assert.Equal(tape[44..], stored[44..]);
        // Saved twice (the first answer lost): the same answer.
        await AssertCode(await Complete(secretary, take), 200, "MSG363");

        await using var db = fixture.Db();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.RecordingTakeStarted && a.ObjectId == take && a.ActorId == s.Secretary));
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.RecordingTakeSaved && a.ObjectId == take && a.ActorId == s.Secretary));
    }

    [Fact]
    public async Task A_later_take_is_appended_to_the_room_recording_and_the_whole_is_transcribed_again()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        fixture.Speech.Failure = null;
        using var secretary = fixture.Client(s.Secretary);
        var first = Tape(2 * Part, 1);
        var room = (await Record(secretary, await Open(secretary, s), first)).GetProperty("recordingId").GetGuid();
        await ProcessAll();
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);

        // The participants were told before the first take; switching the mic on again asks nothing.
        var second = Tape(Part + 500, 2);
        var take = await Open(secretary, s, consent: false);
        var saved = await Record(secretary, take, second);
        Assert.Equal(room, saved.GetProperty("recordingId").GetGuid());
        Assert.True(saved.GetProperty("appended").GetBoolean());

        var item = Assert.Single((await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
        Assert.Equal(room, item.GetProperty("id").GetGuid());
        Assert.Equal("Pending", item.GetProperty("transcriptStatus").GetString());
        var takes = item.GetProperty("takes").EnumerateArray().ToArray();
        Assert.Equal(2, takes.Length);
        Assert.Equal((2 * Part - 44) * 1000L / 32_000, takes[1].GetProperty("offsetMs").GetInt64());
        Assert.Equal((Part + 500 - 44) * 1000L / 32_000, takes[1].GetProperty("durationMs").GetInt64());
        Assert.Equal(fixture.Clock.Now, takes[1].GetProperty("startedAt").GetDateTimeOffset());

        // One file: the first take, then the second without its own header.
        byte[] merged = [.. first, .. second[44..]];
        var stored = await StoredBytes(room);
        Assert.Equal(merged[44..], stored[44..]);
        Assert.Equal((uint)merged.Length - 44, BinaryPrimitives.ReadUInt32LittleEndian(stored.AsSpan(40)));
        await ProcessAll();
        Assert.Equal(stored, fixture.Speech.LastAudio);
        Assert.Equal("Completed", (await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{room}/transcript"))
            .GetProperty("status").GetString());
        // Nothing of the take is left beside the room recording.
        await using var db = fixture.Db();
        Assert.False(await db.Recordings.AnyAsync(r => r.Id == take));
    }

    [Fact]
    public async Task A_take_appended_while_the_room_recording_is_transcribed_makes_that_run_start_over()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        fixture.Speech.Failure = null;
        using var secretary = fixture.Client(s.Secretary);
        var room = (await Record(secretary, await Open(secretary, s), Tape(Part, 1))).GetProperty("recordingId").GetGuid();
        fixture.Speech.During = async () =>
        {
            fixture.Speech.During = null;
            await Record(secretary, await Open(secretary, s, consent: false), Tape(Part, 2));
        };
        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync());

        // The result of the shorter file was thrown away: the whole waits to be transcribed.
        var waiting = await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{room}/transcript");
        Assert.Equal("Pending", waiting.GetProperty("status").GetString());
        Assert.Empty(waiting.GetProperty("segments").EnumerateArray());
        await ProcessAll();
        Assert.Equal(await StoredBytes(room), fixture.Speech.LastAudio);
    }

    [Fact]
    public async Task The_room_mic_switches_on_only_for_the_secretary_of_a_running_meeting_with_a_room()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using (var member = fixture.Client(s.Member)) await AssertCode(await Begin(member, s), 403, "MSG295");
        using (var outsider = fixture.Client(s.Outsider)) Assert.Equal(HttpStatusCode.NotFound, (await Begin(outsider, s)).StatusCode);
        using var secretary = fixture.Client(s.Secretary);
        // Told before the first take, or nothing is recorded.
        var refused = await AssertCode(await Begin(secretary, s, consent: false), 400, "MSG291");
        Assert.True(refused.GetProperty("errors").TryGetProperty("consentConfirmed", out _));

        var online = await fixture.Seed(mode: MeetingMode.Online);
        using (var client = fixture.Client(online.Secretary)) await AssertCode(await Begin(client, online), 409, "MSG358");
        var ended = await fixture.Seed(MeetingStatus.Ended, MeetingMode.InPerson);
        using (var client = fixture.Client(ended.Secretary)) await AssertCode(await Begin(client, ended), 409, "MSG359");

        // The room has one mic: one take at a time, whichever laptop holds it.
        var take = await Open(secretary, s);
        await AssertCode(await Begin(secretary, s), 409, "MSG360");
        // It cannot be erased while it is still being recorded.
        await AssertCode(await secretary.DeleteAsync($"/api/recordings/{take}"), 409, "MSG364");
        Assert.Equal(1, await CountFiles(s.MeetingId));
    }

    [Fact]
    public async Task Parts_come_in_order_at_their_size_from_the_takes_maker_until_it_is_saved()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using var secretary = fixture.Client(s.Secretary);
        var take = await Open(secretary, s);
        var tape = Tape(3 * Part, 3);

        var noise = Enumerable.Repeat((byte)9, Part).ToArray();
        await AssertCode(await Put(secretary, take, 1, noise), 400, "MSG361");
        var wrongRate = Tape(Part, 3);
        Header(44_100).CopyTo(wrongRate, 0);
        await AssertCode(await Put(secretary, take, 1, wrongRate), 400, "MSG361");
        await AssertCode(await Put(secretary, take, 1, tape[..(Part - 2)]), 400, "MSG361");
        await AssertCode(await Put(secretary, take, 0, tape[..Part]), 400, "MSG361");
        // A gap would be silence nobody recorded.
        await AssertCode(await Put(secretary, take, 2, tape[Part..(2 * Part)]), 400, "MSG361");

        using (var chair = fixture.Client(s.Chair))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Put(chair, take, 1, tape[..Part])).StatusCode);
            // Only its maker or a Secretary switches it off.
            await AssertCode(await Complete(chair, take), 403, "MSG295");
        }

        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 1, tape[..Part])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 2, tape[Part..(Part + 10)], final: true)).StatusCode);
        // Nothing follows a short last part.
        await AssertCode(await Put(secretary, take, 3, tape[(2 * Part)..]), 400, "MSG361");
        await AssertCode(await Complete(secretary, take), 200, "MSG363");
        await AssertCode(await Put(secretary, take, 2, tape[Part..(2 * Part)]), 409, "MSG362");
    }

    [Fact]
    public async Task A_take_cannot_grow_past_the_recording_limit()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using var secretary = fixture.Client(s.Secretary);
        var take = await Open(secretary, s);
        var max = RecordingFixture.MaxUploadMegabytes * 1024 * 1024;
        var part = Tape(Part, 4);
        var silence = Enumerable.Repeat((byte)4, Part).ToArray();
        var n = 1;
        for (; (long)n * Part <= max; n++)
            Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, n, n == 1 ? part : silence)).StatusCode);
        await AssertCode(await Put(secretary, take, n, silence), 413, "MSG293");
    }

    [Fact]
    public async Task A_take_the_mic_heard_nothing_in_is_dropped()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using var secretary = fixture.Client(s.Secretary);
        var before = fixture.StoredFileCount();
        var take = await Open(secretary, s);
        var saved = await AssertCode(await Complete(secretary, take), 200, "MSG365");
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("recordingId").ValueKind);
        Assert.Empty((await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
        Assert.Equal(before, fixture.StoredFileCount());
        // The mic switches on again as if nothing had been.
        await Open(secretary, s);
    }

    [Fact]
    public async Task A_take_left_open_is_saved_by_the_server_once_no_part_has_come_for_hours()
    {
        var s = await fixture.Seed(mode: MeetingMode.InPerson);
        using var secretary = fixture.Client(s.Secretary);
        var take = await Open(secretary, s);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(secretary, take, 1, Tape(Part, 5))).StatusCode);

        using (var scope = fixture.Host.Services.CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IRoomTakeService>().CompleteIdleAsync());
        fixture.Clock.Now = fixture.Clock.Now.AddHours(13);
        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IRoomTakeService>().CompleteIdleAsync() >= 1);

        var item = Assert.Single((await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
        Assert.Equal("Available", item.GetProperty("status").GetString());
        await using var db = fixture.Db();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.RecordingTakeSaved && a.ObjectId == take && a.ActorId == null));
    }

    private async Task<int> CountFiles(Guid meetingId)
    {
        await using var db = fixture.Db();
        var paths = await db.Recordings.Where(r => r.MeetingId == meetingId).Select(r => r.StoragePath).ToListAsync();
        return paths.Count(p => File.Exists(Path.Combine(fixture.StorageRoot, p)));
    }
}
