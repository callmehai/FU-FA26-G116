using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.Users;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0060, the person's side: their own consent to Secretaries recording them, separate from enrolling their own
/// voice; and the samples taken under it, which stay theirs to see, hear and delete.
/// </summary>
public sealed class VoiceIdProxyConsentTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const string Msg471Granted = "MSG471";
    private const string Msg472Withdrawn = "MSG472";
    private const string Msg475NoProxyConsent = "MSG475";

    private async Task<(RecordingFixture.Scenario S, HttpClient Secretary)> Arrange()
    {
        var s = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        return (s, VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector));
    }

    private static async Task<Guid> EnrolOwn(HttpClient owner)
    {
        await owner.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = VoiceIdRules.NoticeVersion });
        return (await VoiceCollection.AssertCode(await owner.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307"))
            .GetProperty("data").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task The_profile_carries_an_empty_proxy_set_with_its_own_notice()
    {
        var s = await fixture.Seed();
        using var member = fixture.Client(s.Member);
        var proxy = (await member.GetFromJsonAsync<JsonElement>("/api/me/voice")).GetProperty("proxy");
        Assert.Equal(JsonValueKind.Null, proxy.GetProperty("consent").ValueKind);
        Assert.Equal(0, proxy.GetProperty("samples").GetArrayLength());
        Assert.Equal(VoiceIdRules.MaxSamples, proxy.GetProperty("maxSamples").GetInt32());
        Assert.Equal("1.0", proxy.GetProperty("noticeVersion").GetString());
        Assert.Equal(VoiceIdRules.ProxyPurpose, proxy.GetProperty("purpose").GetString());

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await VoiceCollection.GrantProxy(anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("/api/me/voice/proxy-consent")).StatusCode);
    }

    [Fact]
    public async Task The_proxy_consent_is_explicit_versioned_given_once_and_independent_of_the_own_one()
    {
        var s = await fixture.Seed();
        using var member = fixture.Client(s.Member);
        var stale = await VoiceCollection.AssertCode(await VoiceCollection.GrantProxy(member, VoiceIdRules.NoticeVersion), 400, "MSG313");
        Assert.True(stale.GetProperty("errors").TryGetProperty("noticeVersion", out _));

        // No consent to enrol their own voice is needed.
        var granted = await VoiceCollection.AssertCode(await VoiceCollection.GrantProxy(member), 200, Msg471Granted);
        var data = granted.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("consent").ValueKind);
        var consent = data.GetProperty("proxy").GetProperty("consent");
        Assert.Equal(VoiceIdRules.ProxyNoticeVersion, consent.GetProperty("noticeVersion").GetString());
        Assert.Equal(VoiceIdRules.ProxyPurpose, consent.GetProperty("purpose").GetString());
        await VoiceCollection.AssertCode(await VoiceCollection.GrantProxy(member), 200, Msg471Granted);

        await using var db = fixture.Db();
        var row = await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member);
        Assert.Equal(VoiceConsentScope.Proxy, row.Scope);
        Assert.Null(row.WithdrawnAt);
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentGranted && a.ActorId == s.Member);
        Assert.Equal(row.Id, audit.ObjectId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));

        // The own consent is a second standing row; neither replaces the other.
        await VoiceCollection.AssertCode(await member.PostAsJsonAsync("/api/me/voice/consent",
            new { noticeVersion = VoiceIdRules.NoticeVersion }), 200, "MSG305");
        Assert.Equal(2, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
    }

    [Fact]
    public async Task Two_grants_at_once_leave_one_standing_consent()
    {
        for (var round = 0; round < 5; round++)
        {
            var s = await fixture.Seed();
            using var first = fixture.Client(s.Member);
            using var second = fixture.Client(s.Member);
            var both = await Task.WhenAll(VoiceCollection.GrantProxy(first), VoiceCollection.GrantProxy(second));
            // A double click: there is no row yet for FOR UPDATE to lock, so both insert and the unique index keeps one;
            // the request that lost the race answers as if it had won — the person has agreed.
            foreach (var response in both) await VoiceCollection.AssertCode(response, 200, Msg471Granted);
            await VoiceCollection.AssertCode(await VoiceCollection.GrantProxy(second), 200, Msg471Granted);
            await using var db = fixture.Db();
            Assert.Equal(1, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
        }
    }

    [Fact]
    public async Task Withdrawing_one_consent_erases_only_its_own_set()
    {
        var (s, secretary) = await Arrange();
        using var member = fixture.Client(s.Member);
        var own = await EnrolOwn(member);
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var taken = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);

        var withdrawn = await VoiceCollection.AssertCode(await member.DeleteAsync("/api/me/voice/proxy-consent"), 200, Msg472Withdrawn);
        var data = withdrawn.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("proxy").GetProperty("consent").ValueKind);
        Assert.Equal(0, data.GetProperty("proxy").GetProperty("samples").GetArrayLength());
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("consent").ValueKind);
        Assert.Equal(own, Assert.Single(data.GetProperty("samples").EnumerateArray()).GetProperty("id").GetGuid());
        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == taken));
        Assert.True(await db.VoiceSamples.AnyAsync(x => x.Id == own));
        var proxyEnded = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Member);
        Assert.Contains("\"scope\":\"proxy\"", proxyEnded.Details.Replace(" ", ""));
        Assert.Contains("\"erasedSamples\":1", proxyEnded.Details.Replace(" ", ""));
        // The Secretary can take nothing more, and the withdrawn row stays as evidence.
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples",
            VoiceCollection.Sample()), 409, Msg475NoProxyConsent);
        Assert.NotNull((await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member && c.Scope == VoiceConsentScope.Proxy)).WithdrawnAt);

        // Agreeing again starts a new consent; withdrawing the own one now leaves the Secretary's samples alone.
        await VoiceCollection.AssertCode(await VoiceCollection.GrantProxy(member), 200, Msg471Granted);
        var again = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        var ownEnded = await VoiceCollection.AssertCode(await member.DeleteAsync("/api/me/voice/consent"), 200, "MSG306");
        Assert.Equal(JsonValueKind.Null, ownEnded.GetProperty("data").GetProperty("consent").ValueKind);
        Assert.Equal(again, Assert.Single(ownEnded.GetProperty("data").GetProperty("proxy").GetProperty("samples").EnumerateArray())
            .GetProperty("id").GetGuid());
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == own));
        Assert.True(await db.VoiceSamples.AnyAsync(x => x.Id == again));
        Assert.Equal(2, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.Scope == VoiceConsentScope.Proxy));
        Assert.Contains(await db.AuditLog.Where(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Member)
            .Select(a => a.Details).ToListAsync(), d => d.Replace(" ", "").Contains("\"scope\":\"self\""));
        // Withdrawing a consent that does not stand changes nothing.
        await VoiceCollection.AssertCode(await member.DeleteAsync("/api/me/voice/consent"), 200, "MSG306");
        Assert.True(await db.VoiceSamples.AnyAsync(x => x.Id == again));
    }

    [Fact]
    public async Task The_own_limit_counts_only_the_samples_the_person_recorded()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        for (var i = 0; i < VoiceIdRules.MaxSamples; i++) await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var member = fixture.Client(s.Member);
        await EnrolOwn(member);
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++)
            await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307");
        await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 409, "MSG310");

        var profile = await member.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("samples").GetArrayLength());
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("proxy").GetProperty("samples").GetArrayLength());
    }

    [Fact]
    public async Task The_person_sees_hears_and_deletes_a_sample_a_secretary_took_and_nobody_else_does()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var taken = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var member = fixture.Client(s.Member);

        var profile = await member.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(0, profile.GetProperty("samples").GetArrayLength());
        var listed = Assert.Single(profile.GetProperty("proxy").GetProperty("samples").EnumerateArray());
        Assert.Equal(taken, listed.GetProperty("id").GetGuid());
        Assert.Equal(s.MeetingId, listed.GetProperty("meetingId").GetGuid());
        Assert.Equal("Recording test", listed.GetProperty("meetingTitle").GetString());
        Assert.Equal("Recording test 0", listed.GetProperty("collectedByName").GetString());
        Assert.True(listed.GetProperty("playable").GetBoolean());

        var heard = await member.GetAsync($"/api/me/voice/samples/{taken}/audio");
        Assert.Equal(HttpStatusCode.OK, heard.StatusCode);
        Assert.Equal(Encoding.ASCII.GetBytes(VoiceCollection.Recorded), await heard.Content.ReadAsByteArrayAsync());
        // The person's own listening is not audited; only someone else's is.
        await using var db = fixture.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.VoiceSamplePlayed && a.ObjectId == taken));
        // The Secretary reaches it only through their meeting, never through the owner's routes.
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"/api/me/voice/samples/{taken}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"/api/me/voice/samples/{taken}")).StatusCode);
        using var chair = fixture.Client(s.Chair);
        Assert.Equal(HttpStatusCode.NotFound, (await chair.GetAsync($"/api/me/voice/samples/{taken}/audio")).StatusCode);

        var deleted = await VoiceCollection.AssertCode(await member.DeleteAsync($"/api/me/voice/samples/{taken}"), 200, "MSG311");
        Assert.Equal(0, deleted.GetProperty("data").GetProperty("proxy").GetProperty("samples").GetArrayLength());
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == taken));
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleDeleted && a.ObjectId == taken);
        Assert.Equal(s.Member, audit.ActorId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));
        Assert.Contains(s.Secretary.ToString(), audit.Details);
        // The consent stands: the Secretary can take another one.
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
    }

    [Fact]
    public async Task Deactivating_an_account_ends_both_consents_and_erases_both_sets()
    {
        var (s, secretary) = await Arrange();
        using (var member = fixture.Client(s.Member)) await EnrolOwn(member);
        await VoiceCollection.GrantProxy(fixture, s.Member);
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);

        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserService>().ChangeStatusAsync(s.Member, false, s.Organizer)).Succeeded);

        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
        Assert.Equal(2, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt != null));
        Assert.False(await db.VoiceConsents.AnyAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
        var ended = (await db.AuditLog.Where(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Organizer)
            .Select(a => a.Details).ToListAsync()).Select(d => d.Replace(" ", "")).ToList();
        Assert.Equal(2, ended.Count);
        Assert.All(ended, d => Assert.Contains("\"reason\":\"accountDeactivated\"", d));
        Assert.Contains(ended, d => d.Contains("\"scope\":\"self\""));
        Assert.Contains(ended, d => d.Contains("\"scope\":\"proxy\""));
    }

    [Fact]
    public async Task A_sample_a_secretary_took_names_the_person_in_the_transcript()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        // The proxy notice is 1.0, under the 1.2 an own consent needs: it says the name is filled in all the same.
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member, 0f, 1f, 0f);
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) break;
        }
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent([.. "RIFF"u8, .. new byte[256]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "hop.wav");
        using var uploader = fixture.Client(s.Secretary);
        var id = (await (await uploader.PostAsync(s.Url, form)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = new(30000, "vi", "gipformer-1.5-68m-int8@abc123def456", "eres2net-base-200k@0123456789ab",
            [new(0, 9000, "SPEAKER_00", "Tôi xin phát biểu.")], [new SpeechSpeaker("SPEAKER_00", 9000, [0f, 1f, 0.1f])]);
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) break;
        }

        await using var db = fixture.Db();
        var voice = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_00");
        Assert.Equal(s.Member, voice.SuggestedUserId);
        Assert.Equal(s.Member, voice.AssignedUserId);
        Assert.Null(voice.AssignedById);
    }
}
