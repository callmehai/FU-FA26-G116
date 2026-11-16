using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Email;

public sealed class ResendWebhookReceiverTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Secret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("bi-mat-chi-dung-trong-test"));

    private readonly FakeEmailMessageRepository _messages = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ResendWebhookReceiver Receiver(string secret) => new(
        _messages, _unitOfWork,
        Options.Create(new EmailDeliveryOptions { Resend = new ResendOptions { WebhookSecret = secret } }),
        new FixedClock(Now), NullLogger<ResendWebhookReceiver>.Instance);

    private static byte[] Event(string type) => Encoding.UTF8.GetBytes($$$"""
        {"type":"{{{type}}}","created_at":"2026-11-16T08:59:00Z","data":{"email_id":"re_1"}}
        """);

    private Task<EmailWebhookOutcome> Receive(byte[] body, string? secret = null)
    {
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var key = Convert.FromBase64String(Secret[6..]);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(key, (byte[])[.. Encoding.UTF8.GetBytes($"msg_1.{timestamp}."), .. body]));
        return Receiver(secret ?? Secret).ReceiveResendAsync(body, "msg_1", timestamp, "v1," + signature);
    }

    [Fact]
    public async Task A_signed_delivery_report_is_recorded_on_its_message()
    {
        Assert.Equal(EmailWebhookOutcome.Recorded, await Receive(Event("email.delivered")));

        Assert.Equal(("re_1", (Guid?)null, EmailDeliveryState.Delivered), Assert.Single(_messages.Reports));
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_report_about_no_message_or_an_older_one_is_ignored()
    {
        _messages.ReportsMatched = 0;

        Assert.Equal(EmailWebhookOutcome.Ignored, await Receive(Event("email.bounced")));
    }

    [Fact]
    public async Task A_request_not_signed_with_the_secret_records_nothing()
    {
        var other = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("mot-bi-mat-khac"));

        Assert.Equal(EmailWebhookOutcome.InvalidSignature, await Receive(Event("email.delivered"), other));

        Assert.Empty(_messages.Reports);
        Assert.Empty(_unitOfWork.Modes);
    }

    [Fact]
    public async Task An_event_type_it_does_not_track_is_ignored_without_touching_the_store()
    {
        Assert.Equal(EmailWebhookOutcome.Ignored, await Receive(Event("email.opened")));

        Assert.Empty(_messages.Reports);
    }

    [Fact]
    public async Task A_storage_failure_escapes_so_the_endpoint_answers_500_and_resend_retries()
    {
        _messages.FailReport = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Receive(Event("email.delivered")));
    }
}
