using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Infrastructure.Calls;

namespace TrustCabinet.Web.Tests.Calls;

/// <summary>
/// The webhook signature check and parser, against payloads shaped like the ones LiveKit sends.
/// Signs the header the way LiveKit does: an HS256 JWT issued by the API key, whose sha256 claim
/// is the base64 SHA-256 of the exact body.
/// </summary>
public sealed class LiveKitWebhookParsingTests
{
    private const string ApiKey = "trustcabinet-test";
    private const string ApiSecret = "khoa-bi-mat-livekit-chi-dung-trong-test";

    /// <summary>
    /// A real room_finished body from livekit-server 1.13.7 (TURN password removed). It carries
    /// roomEndReason, which the SDK's own WebhookReceiver rejects as an unknown field.
    /// </summary>
    private const string RoomFinished =
        """{"event":"room_finished","room":{"sid":"RM_X5eULkqkUoEu","name":"meeting-0192f5c4-8a4e-7d2b-9c1a-3f6e5d4c3b2a","emptyTimeout":300,"departureTimeout":20,"creationTime":"1790732775","creationTimeMs":"1790732775933","turnPassword":"redacted","enabledCodecs":[{"mime":"audio/opus"},{"mime":"video/VP8"}],"numParticipants":1},"id":"EV_oGbfc5Kvgok3","createdAt":"1790732822","roomEndReason":"ROOM_END_API_DELETE"}""";

    private const string TrackPublished =
        """{"event":"track_published","room":{"sid":"RM_1","name":"meeting-0192f5c4-8a4e-7d2b-9c1a-3f6e5d4c3b2a"},"participant":{"sid":"PA_1","identity":"0192f5c4-0000-7000-8000-000000000001","joinedAt":"1790732700"},"track":{"sid":"TR_1","type":"VIDEO","source":"SCREEN_SHARE"},"id":"EV_2","createdAt":"1790732800"}""";

    private static LiveKitGateway Gateway() => new(
        Options.Create(new LiveKitOptions
        {
            PublicUrl = "ws://livekit.test",
            ApiUrl = "http://livekit.test",
            ApiKey = ApiKey,
            ApiSecret = ApiSecret,
        }),
        new NoHttp());

    [Fact]
    public void A_newer_server_payload_with_unknown_fields_still_parses()
    {
        var body = Encoding.UTF8.GetBytes(RoomFinished);

        var webhook = Gateway().ReadWebhook(body, Sign(body));

        Assert.NotNull(webhook);
        Assert.Equal("room_finished", webhook.Event);
        Assert.Equal("meeting-0192f5c4-8a4e-7d2b-9c1a-3f6e5d4c3b2a", webhook.RoomName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790732822), webhook.CreatedAt);
    }

    [Fact]
    public void Participant_and_track_fields_are_mapped()
    {
        var body = Encoding.UTF8.GetBytes(TrackPublished);

        var webhook = Gateway().ReadWebhook(body, Sign(body))!;

        Assert.Equal("0192f5c4-0000-7000-8000-000000000001", webhook.ParticipantIdentity);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790732700), webhook.ParticipantJoinedAt);
        Assert.Equal("TR_1", webhook.TrackSid);
        Assert.Equal(CallTrackSource.ScreenShare, webhook.TrackSource);
    }

    [Fact]
    public void A_body_changed_after_signing_is_rejected()
    {
        var body = Encoding.UTF8.GetBytes(RoomFinished);
        var header = Sign(body);
        body[10] ^= 1;

        Assert.Null(Gateway().ReadWebhook(body, header));
    }

    [Fact]
    public void A_header_signed_with_another_secret_or_missing_is_rejected()
    {
        var body = Encoding.UTF8.GetBytes(RoomFinished);

        Assert.Null(Gateway().ReadWebhook(body, Sign(body, secret: "mot-khoa-khac-hoan-toan-du-32-ky-tu")));
        Assert.Null(Gateway().ReadWebhook(body, null));
        Assert.Null(Gateway().ReadWebhook(body, "Bearer " + Sign(body)[..10]));
    }

    [Fact]
    public void Nothing_is_accepted_while_livekit_is_not_configured()
    {
        var body = Encoding.UTF8.GetBytes(RoomFinished);
        var gateway = new LiveKitGateway(Options.Create(new LiveKitOptions()), new NoHttp());

        Assert.False(gateway.IsConfigured);
        Assert.Null(gateway.ReadWebhook(body, Sign(body)));
    }

    private static string Sign(byte[] body, string secret = ApiSecret)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiKey,
            Subject = new ClaimsIdentity([new Claim("sha256", Convert.ToBase64String(SHA256.HashData(body)))]),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No HTTP in parsing tests.");
    }
}
