using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>Shared by the two sides of ADR-0060: the person's proxy consent and the Secretary's collection.</summary>
internal static class VoiceCollection
{
    internal const string Recorded = "RIFF-giong-mau-thu-ho";

    internal static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    internal static MultipartFormDataContent Sample(string type = "audio/wav")
    {
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes(Recorded));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        return new MultipartFormDataContent { { file, "file", "giong-mau.wav" } };
    }

    /// <summary>A signed-in client whose token carries these capabilities; the gate still re-reads them from the database.</summary>
    internal static HttpClient Client(RecordingFixture fixture, Guid actor, params Capability[] capabilities)
    {
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", actor.ToString()), .. capabilities.Select(c => new Claim("cap", c.ToString()))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(RecordingFixture.SigningKey)),
                SecurityAlgorithms.HmacSha256)));
        var client = fixture.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Gives the built-in Secretary role its default capability (ReferenceData) and puts these users in that role. The
    /// test database is migrated, not seeded, so its role_capabilities predates ADR-0060.
    /// </summary>
    internal static async Task MakeCollectors(RecordingFixture fixture, params Guid[] users)
    {
        await using var db = fixture.Db();
        if (!await db.RoleCapabilities.AnyAsync(rc => rc.RoleId == BuiltInRoles.Secretary && rc.Capability == Capability.VoiceCollector))
        {
            db.RoleCapabilities.Add(new() { RoleId = BuiltInRoles.Secretary, Capability = Capability.VoiceCollector });
            await db.SaveChangesAsync();
        }
        await db.Users.Where(u => users.Contains(u.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.RoleId, BuiltInRoles.Secretary));
    }

    internal static Task<HttpResponseMessage> GrantProxy(HttpClient client, string version = VoiceIdRules.ProxyNoticeVersion)
        => client.PostAsJsonAsync("/api/me/voice/proxy-consent", new { noticeVersion = version });

    internal static async Task GrantProxy(RecordingFixture fixture, Guid user)
    {
        using var client = fixture.Client(user);
        await AssertCode(await GrantProxy(client), 200, "MSG471");
    }

    internal static string Url(Guid meetingId) => $"/api/meetings/{meetingId}/voice-collection";

    internal static async Task<Guid> Collect(RecordingFixture fixture, HttpClient secretary, Guid meetingId, Guid user,
        params float[] embedding)
    {
        fixture.Speech.NextEmbedding = embedding.Length > 0 ? embedding : [1f, 0f, 0f];
        var added = await AssertCode(await secretary.PostAsync($"{Url(meetingId)}/{user}/samples", Sample()), 201, "MSG473");
        return added.GetProperty("data").GetProperty("id").GetGuid();
    }

    internal static async Task SetMeeting(RecordingFixture fixture, Guid meetingId, MeetingStatus? status = null,
        Sensitivity? sensitivity = null, MeetingMode? mode = null)
    {
        await using var db = fixture.Db();
        var meeting = await db.Meetings.SingleAsync(m => m.Id == meetingId);
        meeting.Status = status ?? meeting.Status;
        meeting.Sensitivity = sensitivity ?? meeting.Sensitivity;
        meeting.Mode = mode ?? meeting.Mode;
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// ADR-0060 — the Secretary of an in-person or hybrid meeting records Voice ID samples for its people on the room's mic:
/// only with the capability, only as the current Secretary of that meeting, and only for a person who consented.
/// </summary>
public sealed class VoiceCollectionEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const string Msg473Added = "MSG473";
    private const string Msg474Deleted = "MSG474";
    private const string Msg475NoProxyConsent = "MSG475";
    private const string Msg476Limit = "MSG476";
    private const string Msg477NotCollectable = "MSG477";

    private async Task<(RecordingFixture.Scenario S, HttpClient Secretary)> Arrange(MeetingStatus status = MeetingStatus.InProgress,
        MeetingMode mode = MeetingMode.Hybrid)
    {
        var s = await fixture.Seed(status, mode);
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        return (s, VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector));
    }

    [Fact]
    public async Task Not_signed_in_gets_401()
    {
        var s = await fixture.Seed();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(VoiceCollection.Url(s.MeetingId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample())).StatusCode);
    }

    [Fact]
    public async Task Without_the_capability_even_the_meetings_secretary_gets_403()
    {
        var s = await fixture.Seed();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var url = VoiceCollection.Url(s.MeetingId);
        // A plain Member role, with no claim at all, or with a claim the database no longer backs: the gate is the data.
        using var noClaim = fixture.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.Forbidden, (await noClaim.GetAsync(url)).StatusCode);
        using var staleClaim = VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector);
        Assert.Equal(HttpStatusCode.Forbidden, (await staleClaim.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staleClaim.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staleClaim.GetAsync($"{url}/samples/{Guid.NewGuid()}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staleClaim.DeleteAsync($"{url}/samples/{Guid.NewGuid()}")).StatusCode);

        // The capability taken from the role while the token still names it: refused on the next call.
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        Assert.Equal(HttpStatusCode.OK, (await staleClaim.GetAsync(url)).StatusCode);
        await using var db = fixture.Db();
        await db.RoleCapabilities.Where(rc => rc.RoleId == BuiltInRoles.Secretary && rc.Capability == Capability.VoiceCollector)
            .ExecuteDeleteAsync();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await staleClaim.GetAsync(url)).StatusCode);
            Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
        }
        finally
        {
            db.RoleCapabilities.Add(new() { RoleId = BuiltInRoles.Secretary, Capability = Capability.VoiceCollector });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task A_collector_who_is_not_the_current_secretary_of_that_meeting_gets_404_not_403()
    {
        var (s, secretary) = await Arrange();
        var other = await fixture.Seed();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var sample = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        // The Chair of this meeting, the Secretary of another one and an outsider all hold the capability.
        var url = VoiceCollection.Url(s.MeetingId);
        foreach (var caller in new[] { s.Chair, other.Secretary, s.Outsider })
        {
            await VoiceCollection.MakeCollectors(fixture, caller);
            using var client = VoiceCollection.Client(fixture, caller, Capability.VoiceCollector);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{url}/samples/{sample}/audio")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{url}/samples/{sample}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync(VoiceCollection.Url(Guid.NewGuid()))).StatusCode);

        // A Secretary replaced mid-meeting loses it at once.
        await using var db = fixture.Db();
        await db.MeetingParticipants.Where(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Role, MeetingRole.Member));
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"{url}/samples/{sample}")).StatusCode);
        Assert.True(await db.VoiceSamples.AnyAsync(x => x.Id == sample));
        Assert.Equal(1, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Theory]
    [InlineData(MeetingMode.Online, Sensitivity.Normal, MeetingStatus.InProgress)]
    [InlineData(MeetingMode.Hybrid, Sensitivity.Secret, MeetingStatus.InProgress)]
    [InlineData(MeetingMode.InPerson, Sensitivity.Secret, MeetingStatus.Published)]
    [InlineData(MeetingMode.Hybrid, Sensitivity.Normal, MeetingStatus.Cancelled)]
    public async Task A_meeting_without_a_room_mic_a_secret_one_or_a_cancelled_one_is_not_found(MeetingMode mode,
        Sensitivity sensitivity, MeetingStatus status)
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var sample = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        await VoiceCollection.SetMeeting(fixture, s.MeetingId, status, sensitivity, mode);
        var url = VoiceCollection.Url(s.MeetingId);

        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"{url}/samples/{sample}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"{url}/samples/{sample}")).StatusCode);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task The_list_shows_the_active_people_their_consent_their_count_and_only_this_meetings_samples()
    {
        var (s, secretary) = await Arrange(MeetingStatus.Published, MeetingMode.InPerson);
        var other = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, other.Secretary);
        Guid gone;
        await using (var db = fixture.Db())
        {
            var leaver = new User { FullName = "Đã nghỉ", Email = $"{Guid.NewGuid():N}@recording.test", PasswordHash = "x", IsActive = false };
            db.Users.Add(leaver);
            db.MeetingParticipants.Add(new() { MeetingId = s.MeetingId, UserId = leaver.Id, Role = MeetingRole.Member });
            // The member attends the other meeting too, and is recorded there by its Secretary.
            db.MeetingParticipants.Add(new() { MeetingId = other.MeetingId, UserId = s.Member, Role = MeetingRole.Member });
            db.MeetingGuestInvitations.Add(new()
            {
                MeetingId = s.MeetingId, InvitedById = s.Secretary, InvitedName = "Khách mời", Email = "khach@ngoai.test",
                CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now,
            });
            await db.SaveChangesAsync();
            gone = leaver.Id;
        }
        await VoiceCollection.GrantProxy(fixture, s.Member);
        // A consent to enrol one's own voice is not a consent to be recorded by a Secretary.
        using (var chair = fixture.Client(s.Chair))
            await VoiceCollection.AssertCode(await chair.PostAsJsonAsync("/api/me/voice/consent",
                new { noticeVersion = VoiceIdRules.NoticeVersion }), 200, "MSG305");
        var here = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using (var elsewhere = VoiceCollection.Client(fixture, other.Secretary, Capability.VoiceCollector))
            await VoiceCollection.Collect(fixture, elsewhere, other.MeetingId, s.Member);

        var view = await secretary.GetFromJsonAsync<JsonElement>(VoiceCollection.Url(s.MeetingId));
        Assert.True(view.GetProperty("canCollect").GetBoolean());
        Assert.Equal(VoiceIdRules.MaxSamples, view.GetProperty("maxSamples").GetInt32());
        var people = view.GetProperty("people").EnumerateArray().ToDictionary(p => p.GetProperty("userId").GetGuid());
        Assert.Equal(new[] { s.Secretary, s.Chair, s.Member }.Order(), people.Keys.Order());
        Assert.DoesNotContain(gone, people.Keys);
        Assert.DoesNotContain("Khách mời", view.GetRawText());

        var member = people[s.Member];
        Assert.True(member.GetProperty("consented").GetBoolean());
        Assert.Equal("Recording test 2", member.GetProperty("fullName").GetString());
        Assert.Equal(2, member.GetProperty("sampleCount").GetInt32());
        var listed = Assert.Single(member.GetProperty("samples").EnumerateArray());
        Assert.Equal(here, listed.GetProperty("id").GetGuid());
        Assert.Equal("Recording test 0", listed.GetProperty("collectedByName").GetString());
        Assert.Equal(7200, listed.GetProperty("speechMs").GetInt64());
        Assert.False(people[s.Chair].GetProperty("consented").GetBoolean());
        Assert.Equal(0, people[s.Chair].GetProperty("sampleCount").GetInt32());
        Assert.DoesNotContain("ciphertext", view.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MeetingStatus.Published, true)]
    [InlineData(MeetingStatus.InProgress, true)]
    [InlineData(MeetingStatus.Approved, false)]
    [InlineData(MeetingStatus.Ended, false)]
    public async Task Samples_are_taken_only_while_the_meeting_is_published_or_in_progress_but_listed_in_any_live_state(
        MeetingStatus status, bool collectable)
    {
        var (s, secretary) = await Arrange(status);
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var view = await secretary.GetFromJsonAsync<JsonElement>(VoiceCollection.Url(s.MeetingId));
        Assert.Equal(collectable, view.GetProperty("canCollect").GetBoolean());
        Assert.Equal(3, view.GetProperty("people").GetArrayLength());

        var response = await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample());
        if (collectable) await VoiceCollection.AssertCode(response, 201, Msg473Added);
        else await VoiceCollection.AssertCode(response, 409, Msg477NotCollectable);
        await using var db = fixture.Db();
        Assert.Equal(collectable ? 1 : 0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task A_sample_is_the_persons_own_kept_encrypted_audited_and_they_are_told()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        fixture.Speech.NextEmbedding = [0.25f, -0.5f, 0.75f];
        var added = await VoiceCollection.AssertCode(
            await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample()), 201, Msg473Added);
        var id = added.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal("Recording test 0", added.GetProperty("data").GetProperty("collectedByName").GetString());

        await using var db = fixture.Db();
        var stored = await db.VoiceSamples.Include(x => x.Consent).SingleAsync(x => x.Id == id);
        Assert.Equal(s.Member, stored.UserId);
        Assert.Equal(s.Secretary, stored.CollectedById);
        Assert.Equal(s.MeetingId, stored.MeetingId);
        Assert.Equal(VoiceConsentScope.Proxy, stored.Consent.Scope);
        Assert.Equal(s.Member, stored.Consent.UserId);
        Assert.Equal("audio/wav", stored.AudioContentType);
        Assert.NotNull(stored.AudioCiphertext);
        Assert.DoesNotContain(Convert.ToHexString(Encoding.ASCII.GetBytes("RIFF-giong")), Convert.ToHexString(stored.AudioCiphertext!));

        var notice = await db.Notifications.SingleAsync(n => n.UserId == s.Member && n.Type == NotificationType.VoiceSampleCollected);
        Assert.Equal("VoiceSample", notice.TargetType);
        Assert.Equal(id, notice.TargetId);
        Assert.Contains("Recording test 0", notice.Body);
        Assert.False(await db.Notifications.AnyAsync(n => n.UserId == s.Secretary && n.Type == NotificationType.VoiceSampleCollected));
        using var member = fixture.Client(s.Member);
        var target = await member.GetFromJsonAsync<JsonElement>($"/api/notifications/{notice.Id}/target");
        Assert.Equal("VoiceSample", target.GetProperty("targetType").GetString());
        Assert.Equal(id, target.GetProperty("targetId").GetGuid());
        Assert.Equal("/mgmt/my-voice?tab=proxy", target.GetProperty("url").GetString());
        // The notice is the person's: nobody else opens it.
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);

        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleAdded && a.ObjectId == id);
        Assert.Equal(s.Secretary, audit.ActorId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));
        Assert.Contains(s.Member.ToString(), audit.Details);

        // Deleted, the notice opens nothing.
        await VoiceCollection.AssertCode(await secretary.DeleteAsync($"{VoiceCollection.Url(s.MeetingId)}/samples/{id}"), 200, Msg474Deleted);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);
    }

    [Fact]
    public async Task Nothing_is_taken_without_the_persons_proxy_consent_and_only_for_a_person_of_the_meeting()
    {
        var (s, secretary) = await Arrange();
        var url = VoiceCollection.Url(s.MeetingId);
        // Their own Voice ID is on, but they never let a Secretary record them.
        using (var member = fixture.Client(s.Member))
            await VoiceCollection.AssertCode(await member.PostAsJsonAsync("/api/me/voice/consent",
                new { noticeVersion = VoiceIdRules.NoticeVersion }), 200, "MSG305");
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample()), 409,
            Msg475NoProxyConsent);

        // Someone outside the meeting, unknown, or no longer active is not a person of it, consent or not.
        await VoiceCollection.GrantProxy(fixture, s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PostAsync($"{url}/{s.Outsider}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PostAsync($"{url}/{Guid.NewGuid()}/samples", VoiceCollection.Sample())).StatusCode);
        await VoiceCollection.GrantProxy(fixture, s.Chair);
        await using var db = fixture.Db();
        await db.MeetingParticipants.Where(p => p.MeetingId == s.MeetingId && p.UserId == s.Chair)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.AssignmentStatus, AssignmentStatus.Removed)
                .SetProperty(p => p.RemovedAt, fixture.Clock.Now).SetProperty(p => p.RemovedById, s.Secretary));
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PostAsync($"{url}/{s.Chair}/samples", VoiceCollection.Sample())).StatusCode);

        // The form is checked as for one's own sample.
        await VoiceCollection.GrantProxy(fixture, s.Member);
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample("text/plain")), 415, "MSG292");
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{url}/{s.Member}/samples", JsonContent.Create(new { })), 400, "MSG313");
        fixture.Speech.EmbedFailure = new VoiceSampleRejectedException("multiple_speakers");
        try
        {
            await VoiceCollection.AssertCode(await secretary.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample()), 422, "MSG309");
        }
        finally { fixture.Speech.EmbedFailure = null; }
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member || x.UserId == s.Outsider || x.UserId == s.Chair));
        Assert.False(await db.Notifications.AnyAsync(n => n.Type == NotificationType.VoiceSampleCollected
            && (n.UserId == s.Member || n.UserId == s.Outsider || n.UserId == s.Chair)));
    }

    [Fact]
    public async Task A_person_holds_at_most_five_proxy_samples_across_meetings_and_their_own_set_does_not_count()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        using (var member = fixture.Client(s.Member))
        {
            await VoiceCollection.AssertCode(await member.PostAsJsonAsync("/api/me/voice/consent",
                new { noticeVersion = VoiceIdRules.NoticeVersion }), 200, "MSG305");
            for (var i = 0; i < VoiceIdRules.MaxSamples; i++)
                await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307");
        }
        var other = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, other.Secretary);
        await using (var db = fixture.Db())
        {
            db.MeetingParticipants.Add(new() { MeetingId = other.MeetingId, UserId = s.Member, Role = MeetingRole.Member });
            await db.SaveChangesAsync();
        }
        using var elsewhere = VoiceCollection.Client(fixture, other.Secretary, Capability.VoiceCollector);
        await VoiceCollection.Collect(fixture, elsewhere, other.MeetingId, s.Member);
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++) await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);

        await VoiceCollection.AssertCode(
            await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample()), 409, Msg476Limit);
        await using var check = fixture.Db();
        Assert.Equal(VoiceIdRules.MaxSamples, await check.VoiceSamples.CountAsync(x => x.UserId == s.Member && x.CollectedById != null));
        Assert.Equal(VoiceIdRules.MaxSamples, await check.VoiceSamples.CountAsync(x => x.UserId == s.Member && x.CollectedById == null));
    }

    [Fact]
    public async Task The_secretary_hears_and_deletes_only_samples_taken_in_their_meeting_and_each_play_is_audited()
    {
        var (s, secretary) = await Arrange();
        var other = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, other.Secretary);
        await using (var db = fixture.Db())
        {
            db.MeetingParticipants.Add(new() { MeetingId = other.MeetingId, UserId = s.Member, Role = MeetingRole.Member });
            await db.SaveChangesAsync();
        }
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var here = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var elsewhere = VoiceCollection.Client(fixture, other.Secretary, Capability.VoiceCollector);
        var there = await VoiceCollection.Collect(fixture, elsewhere, other.MeetingId, s.Member);
        Guid own;
        using (var member = fixture.Client(s.Member))
        {
            await VoiceCollection.AssertCode(await member.PostAsJsonAsync("/api/me/voice/consent",
                new { noticeVersion = VoiceIdRules.NoticeVersion }), 200, "MSG305");
            own = (await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307"))
                .GetProperty("data").GetProperty("id").GetGuid();
        }
        var url = VoiceCollection.Url(s.MeetingId);

        var heard = await secretary.GetAsync($"{url}/samples/{here}/audio");
        Assert.Equal(HttpStatusCode.OK, heard.StatusCode);
        Assert.Equal("audio/wav", heard.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Encoding.ASCII.GetBytes(VoiceCollection.Recorded), await heard.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", heard.Headers.CacheControl?.ToString());
        await using var check = fixture.Db();
        var played = await check.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSamplePlayed && a.ObjectId == here);
        Assert.Equal(s.Secretary, played.ActorId);

        // Taken in another meeting, or recorded by the person themself: as if it did not exist, and nothing is audited.
        foreach (var sample in new[] { there, own, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"{url}/samples/{sample}/audio")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"{url}/samples/{sample}")).StatusCode);
        }
        Assert.Equal(1, await check.AuditLog.CountAsync(a => a.Action == AuditActions.VoiceSamplePlayed
            && (a.ObjectId == here || a.ObjectId == there || a.ObjectId == own)));

        var deleted = await VoiceCollection.AssertCode(await secretary.DeleteAsync($"{url}/samples/{here}"), 200, Msg474Deleted);
        Assert.Equal(here, deleted.GetProperty("data").GetProperty("id").GetGuid());
        Assert.False(await check.VoiceSamples.AnyAsync(x => x.Id == here));
        Assert.True(await check.VoiceSamples.AnyAsync(x => x.Id == there));
        Assert.True(await check.VoiceSamples.AnyAsync(x => x.Id == own));
        var audit = await check.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleDeleted && a.ObjectId == here);
        Assert.Equal(s.Secretary, audit.ActorId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"{url}/samples/{here}/audio")).StatusCode);
    }

    [Fact]
    public async Task Two_secretaries_recording_the_last_place_at_once_keep_the_limit()
    {
        var (s, secretary) = await Arrange();
        var other = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, other.Secretary);
        await using (var db = fixture.Db())
        {
            db.MeetingParticipants.Add(new() { MeetingId = other.MeetingId, UserId = s.Member, Role = MeetingRole.Member });
            await db.SaveChangesAsync();
        }
        await VoiceCollection.GrantProxy(fixture, s.Member);
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++) await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var elsewhere = VoiceCollection.Client(fixture, other.Secretary, Capability.VoiceCollector);

        var both = await Task.WhenAll(
            secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample()),
            elsewhere.PostAsync($"{VoiceCollection.Url(other.MeetingId)}/{s.Member}/samples", VoiceCollection.Sample()));
        Assert.Single(both, r => r.StatusCode == HttpStatusCode.Created);
        await VoiceCollection.AssertCode(Assert.Single(both, r => r.StatusCode != HttpStatusCode.Created), 409, Msg476Limit);
        await using var check = fixture.Db();
        Assert.Equal(VoiceIdRules.MaxSamples, await check.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task Deleting_frees_a_place_under_the_limit()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.GrantProxy(fixture, s.Member);
        var first = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++) await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        var url = $"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples";
        await VoiceCollection.AssertCode(await secretary.PostAsync(url, VoiceCollection.Sample()), 409, Msg476Limit);
        await VoiceCollection.AssertCode(await secretary.DeleteAsync($"{VoiceCollection.Url(s.MeetingId)}/samples/{first}"), 200, Msg474Deleted);
        await VoiceCollection.AssertCode(await secretary.PostAsync(url, VoiceCollection.Sample()), 201, Msg473Added);
    }
}
