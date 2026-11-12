using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0062 — a hybrid meeting heard as one track: the room recording and each online sentence, on the real clock,
/// summed into one WAV after the meeting ends; rebuilt when a source changes; never over a Secretary's erasure.
/// </summary>
public sealed class CombinedRecordingTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private DateTimeOffset T0 => fixture.Clock.Now.AddMinutes(-30);

    private static byte[] Wav(params (int Ms, short Value)[] runs)
    {
        var samples = runs.SelectMany(r => Enumerable.Repeat(r.Value, r.Ms * 16)).ToArray();
        var bytes = new byte[44 + samples.Length * 2];
        CombinedRecordingRules.WavHeader().CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(36 + samples.Length * 2));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), (uint)(samples.Length * 2));
        for (var i = 0; i < samples.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + i * 2), samples[i]);
        return bytes;
    }

    private string Store(byte[] content)
    {
        var path = $"test/{Guid.NewGuid():N}.wav";
        var full = Path.Combine(fixture.StorageRoot, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        return path;
    }

    /// <summary>
    /// The room mic: 1 s at 100, then (a second take, 3 s later on the clock) 0.5 s at 200. Online: 1 s at 1000 from
    /// 0.5 s, overlapping the room; 0.5 s at 30000 and at 10000 together from 5 s; one sentence out of the record.
    /// </summary>
    private async Task<(RecordingFixture.Scenario S, Guid Room, Guid Overlapping)> Arrange(MeetingMode mode = MeetingMode.Hybrid)
    {
        var s = await fixture.Seed(MeetingStatus.Ended, mode);
        var settled = fixture.Clock.Now.AddMinutes(-5);
        await using var db = fixture.Db();
        await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(u => u
            .SetProperty(m => m.ActualStartedAt, T0).SetProperty(m => m.ActualEndedAt, fixture.Clock.Now.AddMinutes(-10)));
        var room = new Recording
        {
            MeetingId = s.MeetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
            FileName = "ghi-am-phong.wav", StoragePath = Store(Wav((1000, 100), (500, 200))), ContentType = "audio/wav",
            ByteSize = 44 + 1500 * 32, DurationMs = 1500, ConsentObtainedAt = T0, ConsentPurpose = "Lập biên bản",
            Takes = [new(0, 1000, T0), new(1000, 500, T0.AddSeconds(3))], CreatedById = s.Secretary,
            CreatedAt = settled, UpdatedAt = settled,
        };
        db.Recordings.Add(room);
        SpeechClip Clip(int atMs, int ms, short value, bool excluded = false) => new()
        {
            MeetingId = s.MeetingId, SpeakerId = s.Member, ClientClipId = Guid.NewGuid(), StartedAt = T0.AddMilliseconds(atMs),
            DurationMs = ms, Text = "x", AudioPath = Store(Wav((ms, value))), AudioContentType = "audio/wav",
            ExcludedAt = excluded ? settled : null, CreatedAt = settled, UpdatedAt = settled,
        };
        var overlapping = Clip(500, 1000, 1000);
        db.SpeechClips.AddRange(overlapping, Clip(5000, 500, 30000), Clip(5000, 500, 10000), Clip(2000, 500, 5000, excluded: true));
        await db.SaveChangesAsync();
        return (s, room.Id, overlapping.Id);
    }

    private async Task BuildAll()
    {
        for (var i = 0; i < 20; i++)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ICombinedRecordingService>().BuildNextAsync()) return;
        }
    }

    private async Task<Recording?> Combined(Guid meetingId)
    {
        await using var db = fixture.Db();
        return await db.Recordings.AsNoTracking().SingleOrDefaultAsync(r => r.MeetingId == meetingId && r.Source == RecordingSource.Combined);
    }

    private short At(Recording combined, int ms)
    {
        var bytes = File.ReadAllBytes(Path.Combine(fixture.StorageRoot, combined.StoragePath));
        return BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44 + ms * 32));
    }

    [Fact]
    public async Task The_room_and_every_online_sentence_are_mixed_on_the_real_clock()
    {
        var (s, room, _) = await Arrange();
        await BuildAll();

        var combined = await Combined(s.MeetingId);
        Assert.NotNull(combined);
        Assert.Equal(RecordingStatus.Available, combined.Status);
        Assert.Equal("audio/wav", combined.ContentType);
        Assert.StartsWith("ban-ghi-tong-hop-", combined.FileName);
        Assert.Null(combined.Takes);
        // From the first sound (the room, at T0) to the last (the online pair, ending at 5.5 s).
        Assert.Equal(5500, combined.DurationMs);
        Assert.Equal(44 + 5500 * 32, combined.ByteSize);
        Assert.Equal(44 + 5500 * 32, new FileInfo(Path.Combine(fixture.StorageRoot, combined.StoragePath)).Length);
        Assert.Equal(100, At(combined, 250));          // the room alone
        Assert.Equal(1100, At(combined, 750));         // the room and an online mic at once
        Assert.Equal(1000, At(combined, 1250));        // the online mic after the room's first take
        Assert.Equal(0, At(combined, 2250));           // the sentence out of the record is left out
        Assert.Equal(200, At(combined, 3250));         // the room's second take, at its own time
        Assert.Equal(short.MaxValue, At(combined, 5250)); // two online mics at once, clipped rather than wrapped
        // Same consent, retention and owner as the room recording; no transcript of its own.
        await using var db = fixture.Db();
        var source = await db.Recordings.AsNoTracking().SingleAsync(r => r.Id == room);
        Assert.Equal(source.ConsentPurpose, combined.ConsentPurpose);
        Assert.Equal(source.CreatedById, combined.CreatedById);
        Assert.NotNull(combined.RetainUntil);
        Assert.False(await db.Transcripts.AnyAsync(t => t.RecordingId == combined.Id));
    }

    [Fact]
    public async Task It_is_listed_played_and_never_sent_to_transcription()
    {
        var (s, _, _) = await Arrange();
        await BuildAll();
        var combined = (await Combined(s.MeetingId))!;

        using var secretary = fixture.Client(s.Secretary);
        var list = await secretary.GetFromJsonAsync<JsonElement>(s.Url);
        var row = list.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == combined.Id);
        Assert.Equal("Combined", row.GetProperty("source").GetString());
        Assert.Equal("Available", row.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync($"/api/recordings/{combined.Id}/playback")).StatusCode);

        var refused = await secretary.PostAsync($"/api/recordings/{combined.Id}/transcribe", null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("MSG479", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var db = fixture.Db();
        Assert.False(await db.Transcripts.AnyAsync(t => t.RecordingId == combined.Id));
    }

    [Fact]
    public async Task It_is_rebuilt_in_place_when_a_source_changes_and_left_alone_otherwise()
    {
        var (s, _, overlapping) = await Arrange();
        await BuildAll();
        var first = (await Combined(s.MeetingId))!;
        await BuildAll();
        Assert.Equal(first.UpdatedAt, (await Combined(s.MeetingId))!.UpdatedAt);

        // The Chair takes the overlapping sentence out of the record; a minute later the track follows.
        await using (var db = fixture.Db())
            await db.SpeechClips.Where(c => c.Id == overlapping).ExecuteUpdateAsync(u => u
                .SetProperty(c => c.ExcludedAt, fixture.Clock.Now.AddMinutes(-2)).SetProperty(c => c.UpdatedAt, fixture.Clock.Now.AddMinutes(-2)));
        await BuildAll();

        var second = (await Combined(s.MeetingId))!;
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.StoragePath, second.StoragePath);
        Assert.False(File.Exists(Path.Combine(fixture.StorageRoot, first.StoragePath)));
        Assert.Equal(100, At(second, 750));
        Assert.Equal(0, At(second, 1250));
    }

    [Fact]
    public async Task Nothing_is_built_too_early_or_for_another_kind_of_meeting_or_over_an_erasure()
    {
        // In person: one source, the room, already one track.
        var (inPerson, _, _) = await Arrange(MeetingMode.InPerson);
        // A room take still streaming.
        var (open, _, _) = await Arrange();
        await using (var db = fixture.Db())
        {
            db.Recordings.Add(new Recording
            {
                MeetingId = open.MeetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
                FileName = "luot.wav", StoragePath = Store(Wav((100, 1))), ContentType = "audio/wav", Takes = [],
                CreatedById = open.Secretary, CreatedAt = fixture.Clock.Now.AddMinutes(-5), UpdatedAt = fixture.Clock.Now.AddMinutes(-5),
            });
            await db.SaveChangesAsync();
        }
        // A sentence that arrived less than a minute ago.
        var (fresh, _, _) = await Arrange();
        await using (var db = fixture.Db())
            await db.SpeechClips.Where(c => c.MeetingId == fresh.MeetingId).Take(1)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.UpdatedAt, fixture.Clock.Now.AddSeconds(-10)));
        await BuildAll();
        Assert.Null(await Combined(inPerson.MeetingId));
        Assert.Null(await Combined(open.MeetingId));
        Assert.Null(await Combined(fresh.MeetingId));

        // Erased by the Secretary: a later change does not bring it back.
        var (erased, _, overlapping) = await Arrange();
        await BuildAll();
        var combined = (await Combined(erased.MeetingId))!;
        using (var secretary = fixture.Client(erased.Secretary))
            Assert.True((await secretary.DeleteAsync($"/api/recordings/{combined.Id}")).IsSuccessStatusCode);
        await using (var db = fixture.Db())
            await db.SpeechClips.Where(c => c.Id == overlapping).ExecuteUpdateAsync(u => u.SetProperty(c => c.UpdatedAt, fixture.Clock.Now.AddMinutes(-2)));
        await BuildAll();
        Assert.Equal(RecordingStatus.Deleted, (await Combined(erased.MeetingId))!.Status);
    }

    [Fact]
    public async Task Long_silence_is_cut_to_two_seconds_and_a_track_past_twelve_hours_is_not_built()
    {
        // A meeting left running for days: the room spoke at T0, an online sentence came three hours later.
        var s = await fixture.Seed(MeetingStatus.Ended, MeetingMode.Hybrid);
        var settled = fixture.Clock.Now.AddMinutes(-5);
        var start = fixture.Clock.Now.AddHours(-4);
        await using (var db = fixture.Db())
        {
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(u => u
                .SetProperty(m => m.ActualStartedAt, start).SetProperty(m => m.ActualEndedAt, fixture.Clock.Now.AddMinutes(-10)));
            db.Recordings.Add(new Recording
            {
                MeetingId = s.MeetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Available,
                FileName = "ghi-am-phong.wav", StoragePath = Store(Wav((1000, 100))), ContentType = "audio/wav",
                ByteSize = 44 + 1000 * 32, DurationMs = 1000, Takes = [new(0, 1000, start)], CreatedById = s.Secretary,
                CreatedAt = settled, UpdatedAt = settled,
            });
            db.SpeechClips.Add(new SpeechClip
            {
                MeetingId = s.MeetingId, SpeakerId = s.Member, ClientClipId = Guid.NewGuid(), StartedAt = start.AddHours(3),
                DurationMs = 1000, Text = "x", AudioPath = Store(Wav((1000, 700))), AudioContentType = "audio/wav",
                CreatedAt = settled, UpdatedAt = settled,
            });
            await db.SaveChangesAsync();
        }
        await BuildAll();
        var combined = (await Combined(s.MeetingId))!;
        Assert.Equal(4000, combined.DurationMs);           // 1 s, 2 s of the three hours, 1 s
        Assert.Equal(44 + 4000 * 32, new FileInfo(Path.Combine(fixture.StorageRoot, combined.StoragePath)).Length);
        Assert.Equal(100, At(combined, 500));
        Assert.Equal(0, At(combined, 2000));
        Assert.Equal(700, At(combined, 3500));

        // A room recording that claims thirteen hours of sound is never written out.
        var (huge, room, _) = await Arrange();
        await using (var db = fixture.Db())
            await db.Recordings.Where(r => r.Id == room).ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Takes, new List<RecordingTake> { new(0, (long)TimeSpan.FromHours(13).TotalMilliseconds, T0) }));
        var files = fixture.StoredFileCount();
        await BuildAll();
        Assert.Null(await Combined(huge.MeetingId));
        Assert.Equal(files, fixture.StoredFileCount());
    }

    [Fact]
    public void A_track_keeps_the_real_clock_within_a_stretch_of_sound_and_cuts_the_silence_between()
    {
        var t = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        var track = new CombinedTrack([(t, 1000), (t.AddMilliseconds(2500), 1000), (t.AddMinutes(10), 500), (t.AddMinutes(10).AddMilliseconds(100), 100)]);
        // [0, 3.5 s] is one stretch (a 1.5 s pause); ten minutes later another, 0.5 s long, 2 s after it on the track.
        Assert.Equal(3500 + 2000 + 500, track.TotalMs);
        Assert.Equal(0, track.SampleAt(t));
        Assert.Equal(2500 * 16, track.SampleAt(t.AddMilliseconds(2500)));
        Assert.Equal(5500 * 16, track.SampleAt(t.AddMinutes(10)));
        Assert.Equal(5600 * 16, track.SampleAt(t.AddMinutes(10).AddMilliseconds(100)));
    }

    [Fact]
    public void Only_16_kHz_mono_16_bit_wav_is_read()
    {
        Assert.Empty(CombinedRecordingRules.ReadPcm(Wav())!);
        Assert.Equal(16, CombinedRecordingRules.ReadPcm(Wav((1, 7)))!.Length);
        var stereo = Wav((1, 7));
        BinaryPrimitives.WriteUInt16LittleEndian(stereo.AsSpan(22), 2);
        Assert.Null(CombinedRecordingRules.ReadPcm(stereo));
        Assert.Null(CombinedRecordingRules.ReadPcm("OggS"u8.ToArray()));
    }
}
