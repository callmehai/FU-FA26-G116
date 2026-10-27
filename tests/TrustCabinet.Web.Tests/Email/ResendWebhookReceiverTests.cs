using System.Security.Cryptography;
using System.Text;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// The Svix signature Resend puts on every webhook, and how an event becomes a delivery state (ADR-0031). Recording it
/// on the row needs PostgreSQL and lives in <see cref="EmailQueuePostgresTests"/>.
/// </summary>
public sealed class ResendWebhookReceiverTests
{
    internal static readonly string Secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("trustcabinet-test-secret-32-bytes"));
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"type":"email.delivered"}""");

    /// <summary>Signs the way Svix documents it, independently of the code under test.</summary>
    internal static string Sign(string secret, string id, long timestamp, byte[] body)
    {
        var key = Convert.FromBase64String(secret["whsec_".Length..]);
        var signed = Encoding.UTF8.GetBytes($"{id}.{timestamp}.").Concat(body).ToArray();
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, signed));
    }

    [Fact]
    public void A_correct_signature_is_accepted()
    {
        var timestamp = Now.ToUnixTimeSeconds();

        Assert.True(ResendWebhookReceiver.IsSigned(
            Secret, "msg_1", timestamp.ToString(), Sign(Secret, "msg_1", timestamp, Body), Body, Now));
    }

    [Fact]
    public void One_matching_entry_among_several_is_enough()
    {
        // During a secret rotation Svix signs with both secrets.
        var timestamp = Now.ToUnixTimeSeconds();
        var header = "v1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA= " + Sign(Secret, "msg_1", timestamp, Body);

        Assert.True(ResendWebhookReceiver.IsSigned(Secret, "msg_1", timestamp.ToString(), header, Body, Now));
    }

    [Fact]
    public void A_changed_body_id_or_secret_is_rejected()
    {
        var timestamp = Now.ToUnixTimeSeconds();
        var signature = Sign(Secret, "msg_1", timestamp, Body);
        var otherSecret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("another-secret-another-secret!!!"));

        Assert.False(ResendWebhookReceiver.IsSigned(
            Secret, "msg_1", timestamp.ToString(), signature, Encoding.UTF8.GetBytes("""{"type":"email.bounced"}"""), Now));
        Assert.False(ResendWebhookReceiver.IsSigned(Secret, "msg_2", timestamp.ToString(), signature, Body, Now));
        Assert.False(ResendWebhookReceiver.IsSigned(otherSecret, "msg_1", timestamp.ToString(), signature, Body, Now));
    }

    [Fact]
    public void A_replayed_request_outside_the_window_is_rejected()
    {
        var stale = Now.AddMinutes(-6).ToUnixTimeSeconds();

        Assert.False(ResendWebhookReceiver.IsSigned(
            Secret, "msg_1", stale.ToString(), Sign(Secret, "msg_1", stale, Body), Body, Now));
    }

    [Theory]
    [InlineData(null, "1", "v1,abc")]
    [InlineData("msg_1", "not-a-number", "v1,abc")]
    [InlineData("msg_1", "1", null)]
    [InlineData("msg_1", "1", "v1,***not-base64***")]
    public void Missing_or_garbled_headers_are_rejected(string? id, string? timestamp, string? signature)
        => Assert.False(ResendWebhookReceiver.IsSigned(Secret, id, timestamp, signature, Body, Now));

    [Fact]
    public void A_bounce_carries_its_reason_and_the_row_id_from_the_tag()
    {
        var rowId = Guid.CreateVersion7();
        var report = ResendWebhookReceiver.Parse(Encoding.UTF8.GetBytes($$"""
            {
              "type": "email.bounced",
              "created_at": "2026-10-27T08:00:05.000Z",
              "data": {
                "email_id": "4ef9a417-02e9-4d39-ad75-9611e0fcc33c",
                "to": ["nobody@example.com"],
                "bounce": { "type": "Permanent", "subType": "General", "message": "550 5.1.1 user unknown" },
                "tags": { "email_message_id": "{{rowId:N}}" }
              }
            }
            """));

        Assert.NotNull(report);
        Assert.Equal(EmailDeliveryState.Bounced, report.State);
        Assert.Equal("4ef9a417-02e9-4d39-ad75-9611e0fcc33c", report.EmailId);
        Assert.Equal(rowId, report.RowId);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 8, 0, 5, TimeSpan.Zero), report.OccurredAt);
        Assert.Equal("Permanent General 550 5.1.1 user unknown", report.Detail);
    }

    [Theory]
    [InlineData("email.sent")]
    [InlineData("email.opened")]
    [InlineData("email.clicked")]
    [InlineData("domain.updated")]
    public void Events_the_app_does_not_track_are_ignored(string type)
        => Assert.Null(ResendWebhookReceiver.Parse(Encoding.UTF8.GetBytes(
            $$$"""{"type":"{{{type}}}","created_at":"2026-10-27T08:00:00Z","data":{"email_id":"x"}}""")));
}
