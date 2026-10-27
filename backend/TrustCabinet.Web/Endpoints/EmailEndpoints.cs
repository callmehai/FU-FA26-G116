using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// Resend's delivery webhook (SCRUM-15, ADR-0031): delivered, bounced, marked as spam. Only Resend calls it, so the
/// answers are bare status codes with no message code — nobody reads them but Resend's retry logic.
/// </summary>
public static class EmailEndpoints
{
    /// <summary>A delivery event is about 1 KB; anything this large is not Resend.</summary>
    private const int MaxWebhookBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous at the HTTP layer: Resend proves itself with a Svix signature over the body.
        app.MapPost("/api/email/resend/webhook", ReceiveResendWebhookAsync)
            .WithTags("Email")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> ReceiveResendWebhookAsync(
        HttpRequest request,
        IEmailWebhookReceiver receiver,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        if (request.ContentLength > MaxWebhookBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxWebhookBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var headers = request.Headers;
        EmailWebhookOutcome outcome;
        try
        {
            // The signature covers the exact bytes received, so they are passed on unparsed.
            outcome = await receiver.ReceiveResendAsync(
                buffer.ToArray(), headers["svix-id"], headers["svix-timestamp"], headers["svix-signature"], ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // 500 makes Resend retry the same event later.
            loggers.CreateLogger(nameof(EmailEndpoints))
                .LogError(error, "Resend webhook {WebhookId} failed", headers["svix-id"].ToString());
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return outcome switch
        {
            EmailWebhookOutcome.NotConfigured => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            EmailWebhookOutcome.InvalidSignature => Results.Unauthorized(),
            // Signed but unreadable: retrying the same bytes cannot help, so it is not a 5xx.
            EmailWebhookOutcome.Malformed => Results.BadRequest(),
            _ => Results.Ok(),
        };
    }
}
