using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>Shared by the proxy Voice ID tests: the person's consent and the collector's side (ADR-0071, ADR-0075).</summary>
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

    /// <param name="personAgreed">The collector's confirmation that the person agreed to this take (ADR-0073); null sends
    /// no field at all. One's own sample ignores it.</param>
    internal static MultipartFormDataContent Sample(string type = "audio/wav", string? personAgreed = "true")
    {
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes(Recorded));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        var form = new MultipartFormDataContent { { file, "file", "giong-mau.wav" } };
        if (personAgreed is not null) form.Add(new StringContent(personAgreed), "personAgreed");
        return form;
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
    /// Puts these users in Quản trị viên, the built-in role that holds "Thu giọng mẫu hộ" by default (ADR-0075). The test
    /// database is migrated, not seeded; SystemRolesWithoutChairSecretary gives that role the capability.
    /// </summary>
    internal static async Task MakeCollectors(RecordingFixture fixture, params Guid[] users)
    {
        await using var db = fixture.Db();
        await db.Users.Where(u => users.Contains(u.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.RoleId, BuiltInRoles.Admin));
    }

    /// <summary>The person's one Voice ID consent, to the current notice: it covers others recording them (ADR-0071).</summary>
    internal static Task<HttpResponseMessage> Agree(HttpClient client, string version = VoiceIdRules.NoticeVersion)
        => client.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = version });

    internal static async Task Agree(RecordingFixture fixture, Guid user)
    {
        using var client = fixture.Client(user);
        await AssertCode(await Agree(client), 200, "MSG305");
    }

    /// <summary>A consent given before notice 1.4 (by default before 1.3, which did not yet name Secretaries at all).</summary>
    internal static async Task AgreeToOldNotice(RecordingFixture fixture, Guid user, string version = "1.2")
    {
        await using var db = fixture.Db();
        db.VoiceConsents.Add(new()
        {
            UserId = user, GrantedAt = fixture.Clock.Now, NoticeVersion = version, Purpose = "Nhận ra người phát biểu.",
            CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The one way to record someone else's voice (ADR-0071, ADR-0075).</summary>
    internal static string Url(Guid userId) => $"/api/users/{userId}/voice-collection";

    internal static async Task<Guid> Collect(RecordingFixture fixture, HttpClient collector, Guid user, params float[] embedding)
    {
        fixture.Speech.NextEmbedding = embedding.Length > 0 ? embedding : [1f, 0f, 0f];
        var added = await AssertCode(await collector.PostAsync($"{Url(user)}/samples", Sample()), 201, "MSG473");
        return added.GetProperty("data").GetProperty("id").GetGuid();
    }
}

/// <summary>
/// ADR-0075 — a meeting's Secretary no longer records anyone's voice: the meeting routes of ADR-0060 are gone, and a
/// Secretary whose system role lacks "Thu giọng mẫu hộ" is refused on the only route left.
/// </summary>
public sealed class VoiceCollectionByMeetingRoleTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly HttpStatusCode[] Gone = [HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed];

    [Fact]
    public async Task The_meeting_routes_are_gone_even_for_a_collector()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress, MeetingMode.Hybrid);
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        await VoiceCollection.Agree(fixture, s.Member);
        using var secretary = VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector);
        var url = $"/api/meetings/{s.MeetingId}/voice-collection";

        // No such route: answered like any other unknown URL (404, or 405 where another verb shares the path template).
        Assert.Contains((await secretary.GetAsync(url)).StatusCode, Gone);
        Assert.Contains((await secretary.PostAsync($"{url}/{s.Member}/samples", VoiceCollection.Sample())).StatusCode, Gone);
        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task A_meeting_Secretary_without_the_capability_cannot_record_on_the_person_route()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress, MeetingMode.Hybrid);
        await VoiceCollection.Agree(fixture, s.Member);
        using var secretary = fixture.Client(s.Secretary);
        var embedded = fixture.Speech.EmbedCalls;

        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(VoiceCollection.Url(s.Member))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await secretary.PostAsync($"{VoiceCollection.Url(s.Member)}/samples", VoiceCollection.Sample())).StatusCode);
        Assert.Equal(embedded, fixture.Speech.EmbedCalls);
    }
}
