using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0071, ADR-0075 — a holder of "Thu giọng mẫu hộ" records one person at any time, with no meeting: only with the capability,
/// only for an active person whose own consent is to notice 1.4 or later; they see, hear and delete every proxy sample
/// of that person, each play audited, and the person is told each time.
/// </summary>
public sealed class VoicePersonCollectionEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const string Msg473Added = "MSG473";
    private const string Msg474Deleted = "MSG474";
    private const string Msg475NoProxyConsent = "MSG475";
    private const string Msg476Limit = "MSG476";
    private const string Msg483NotConfirmed = "MSG483";

    private static string Url(Guid userId) => $"/api/users/{userId}/voice-collection";

    /// <summary>A collector in no meeting with the person: the Outsider of a fresh scenario, given the capability.</summary>
    private async Task<(RecordingFixture.Scenario S, HttpClient Collector)> Arrange()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        await VoiceCollection.MakeCollectors(fixture, s.Outsider);
        return (s, VoiceCollection.Client(fixture, s.Outsider, Capability.VoiceCollector));
    }

    private async Task<Guid> Collect(HttpClient collector, Guid user)
    {
        fixture.Speech.NextEmbedding = [1f, 0f, 0f];
        var added = await VoiceCollection.AssertCode(await collector.PostAsync($"{Url(user)}/samples", VoiceCollection.Sample()), 201,
            Msg473Added);
        return added.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task AgreeTo(Guid user, string version)
    {
        await using var db = fixture.Db();
        db.VoiceConsents.Add(new()
        {
            UserId = user, GrantedAt = fixture.Clock.Now, NoticeVersion = version, Purpose = "Nhận ra người phát biểu.",
            CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Not_signed_in_gets_401_and_without_the_capability_403_on_every_route()
    {
        var s = await fixture.Seed();
        await VoiceCollection.Agree(fixture, s.Member);
        var sample = Guid.NewGuid();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(s.Member))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{Url(s.Member)}/samples", VoiceCollection.Sample())).StatusCode);

        // The meeting's own Secretary, without the capability on their role, gets nothing here: that is ADR-0060's way in.
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(Url(s.Member))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.PostAsync($"{Url(s.Member)}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync($"{Url(s.Member)}/samples/{sample}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.DeleteAsync($"{Url(s.Member)}/samples/{sample}")).StatusCode);
        // A token that still claims it is re-read against the database.
        using var stale = VoiceCollection.Client(fixture, s.Chair, Capability.VoiceCollector);
        Assert.Equal(HttpStatusCode.Forbidden, (await stale.GetAsync(Url(s.Member))).StatusCode);
        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task A_collector_records_a_consenting_person_with_no_meeting_and_the_person_is_told_and_sees_it()
    {
        var (s, collector) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);

        var id = await Collect(collector, s.Member);

        await using var db = fixture.Db();
        var stored = await db.VoiceSamples.SingleAsync(x => x.Id == id);
        Assert.Equal((s.Member, (Guid?)s.Outsider, (Guid?)null), (stored.UserId, stored.CollectedById, stored.MeetingId));
        Assert.NotNull(stored.AudioCiphertext);
        var notice = await db.Notifications.SingleAsync(n => n.UserId == s.Member && n.Type == NotificationType.VoiceSampleCollected);
        Assert.Equal(id, notice.TargetId);
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleAdded && a.ObjectId == id);
        Assert.Equal(s.Outsider, audit.ActorId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));

        // The person sees it in their own proxy set, with no meeting.
        using var member = fixture.Client(s.Member);
        var proxy = (await member.GetFromJsonAsync<JsonElement>("/api/me/voice")).GetProperty("proxy").GetProperty("samples");
        var seen = Assert.Single(proxy.EnumerateArray());
        Assert.Equal(id, seen.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("meetingId").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("meetingTitle").ValueKind);
    }

    [Fact]
    public async Task Nothing_is_taken_without_a_consent_to_notice_1_4_and_only_for_an_active_person()
    {
        var (s, collector) = await Arrange();
        // No Voice ID at all.
        await VoiceCollection.AssertCode(await collector.PostAsync($"{Url(s.Member)}/samples", VoiceCollection.Sample()), 409, Msg475NoProxyConsent);
        // A consent to 1.3 does not cover this (nor, since ADR-0073, a meeting's Secretary).
        await AgreeTo(s.Member, VoiceIdRules.CollectionNoticeVersion);
        await VoiceCollection.AssertCode(await collector.PostAsync($"{Url(s.Member)}/samples", VoiceCollection.Sample()), 409, Msg475NoProxyConsent);
        var view = await collector.GetFromJsonAsync<JsonElement>(Url(s.Member));
        Assert.False(view.GetProperty("consented").GetBoolean());
        Assert.False(view.GetProperty("canCollect").GetBoolean());

        // Unknown or deactivated: a person who does not exist.
        Assert.Equal(HttpStatusCode.NotFound, (await collector.GetAsync(Url(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await collector.PostAsync($"{Url(Guid.NewGuid())}/samples", VoiceCollection.Sample())).StatusCode);
        await VoiceCollection.Agree(fixture, s.Chair);
        await using var db = fixture.Db();
        await db.Users.Where(u => u.Id == s.Chair).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        Assert.Equal(HttpStatusCode.NotFound, (await collector.GetAsync(Url(s.Chair))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await collector.PostAsync($"{Url(s.Chair)}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member || x.UserId == s.Chair));
        Assert.False(await db.Notifications.AnyAsync(n => n.Type == NotificationType.VoiceSampleCollected
            && (n.UserId == s.Member || n.UserId == s.Chair)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task ADR_0073_a_take_without_the_persons_agreement_to_it_is_refused_before_the_worker_hears_it(string? personAgreed)
    {
        var (s, collector) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        var embedded = fixture.Speech.EmbedCalls;

        await VoiceCollection.AssertCode(await collector.PostAsync($"{Url(s.Member)}/samples",
            VoiceCollection.Sample(personAgreed: personAgreed)), 400, Msg483NotConfirmed);

        Assert.Equal(embedded, fixture.Speech.EmbedCalls);
        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));

        var id = await Collect(collector, s.Member);
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleAdded && a.ObjectId == id);
        Assert.Contains("\"personAgreed\":true", audit.Details.Replace(" ", ""));
    }

    [Fact]
    public async Task The_view_lists_every_proxy_sample_of_the_person_which_the_collector_hears_audited_and_deletes()
    {
        var (s, collector) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        // One a meeting's Secretary took in the meeting before ADR-0075 (its meeting kept), one by this collector with no
        // meeting, and the person's own.
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        using var secretary = VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector);
        var inMeeting = await VoiceCollection.Collect(fixture, secretary, s.Member);
        await using (var legacy = fixture.Db())
            await legacy.VoiceSamples.Where(x => x.Id == inMeeting).ExecuteUpdateAsync(u => u.SetProperty(x => x.MeetingId, s.MeetingId));
        var outside = await Collect(collector, s.Member);
        using var member = fixture.Client(s.Member);
        var own = (await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307"))
            .GetProperty("data").GetProperty("id").GetGuid();

        var view = await collector.GetFromJsonAsync<JsonElement>(Url(s.Member));
        Assert.True(view.GetProperty("consented").GetBoolean());
        Assert.True(view.GetProperty("canCollect").GetBoolean());
        Assert.Equal(VoiceIdRules.MaxProxySamples, view.GetProperty("maxSamples").GetInt32());
        var samples = view.GetProperty("samples").EnumerateArray().ToArray();
        Assert.Equal([inMeeting, outside], samples.Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(s.MeetingId, samples[0].GetProperty("meetingId").GetGuid());
        Assert.Equal(JsonValueKind.Null, samples[1].GetProperty("meetingId").ValueKind);

        // Heard back, a sample taken by someone else included; each play audited.
        var audio = await collector.GetAsync($"{Url(s.Member)}/samples/{inMeeting}/audio");
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal(VoiceCollection.Recorded, await audio.Content.ReadAsStringAsync());
        await using (var db = fixture.Db())
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.VoiceSamplePlayed && a.ObjectId == inMeeting
                && a.ActorId == s.Outsider));

        // The person's own sample, or one of theirs asked for under another person, is not here.
        Assert.Equal(HttpStatusCode.NotFound, (await collector.GetAsync($"{Url(s.Member)}/samples/{own}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await collector.DeleteAsync($"{Url(s.Member)}/samples/{own}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await collector.DeleteAsync($"{Url(s.Chair)}/samples/{outside}")).StatusCode);

        await VoiceCollection.AssertCode(await collector.DeleteAsync($"{Url(s.Member)}/samples/{inMeeting}"), 200, Msg474Deleted);
        await using var check = fixture.Db();
        Assert.Equal([outside, own], await check.VoiceSamples.Where(x => x.UserId == s.Member).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync());
        Assert.True(await check.AuditLog.AnyAsync(a => a.Action == AuditActions.VoiceSampleDeleted && a.ObjectId == inMeeting));
    }

    [Fact]
    public async Task One_collector_may_take_all_five_proxy_samples_and_no_more()
    {
        // The per-collector share of 5 went with the meeting Secretaries (ADR-0075); the person's limit is 5 since 23/11/2026 (was 50).
        var (s, collector) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        await using (var seed = fixture.Db())
        {
            var consent = await seed.VoiceConsents.SingleAsync(c => c.UserId == s.Member && c.WithdrawnAt == null);
            for (var i = 0; i < VoiceIdRules.MaxProxySamples - 1; i++)
                seed.VoiceSamples.Add(new()
                {
                    UserId = s.Member, ConsentId = consent.Id, EmbeddingModel = "eres2net-base-200k@0123456789ab", Ciphertext = [1],
                    CollectedById = s.Outsider, CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now,
                });
            await seed.SaveChangesAsync();
        }
        Assert.True((await collector.GetFromJsonAsync<JsonElement>(Url(s.Member))).GetProperty("canCollect").GetBoolean());
        await Collect(collector, s.Member);

        await VoiceCollection.AssertCode(await collector.PostAsync($"{Url(s.Member)}/samples", VoiceCollection.Sample()), 409, Msg476Limit);
        Assert.False((await collector.GetFromJsonAsync<JsonElement>(Url(s.Member))).GetProperty("canCollect").GetBoolean());
        await using var db = fixture.Db();
        Assert.Equal(VoiceIdRules.MaxProxySamples, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }
}
