using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetEmailDelivery(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<EmailDeliveryOptions>()
            .Bind(configuration.GetSection(EmailDeliveryOptions.SectionName))
            .Validate(ValidOptions, InvalidOptionsMessage)
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<MeetingInvitationDispatchProcessor>();
        services.AddScoped<EmailQueueProcessor>();
        services.AddScoped<IEmailQueueProcessor>(provider =>
            provider.GetRequiredService<EmailQueueProcessor>());
        services.AddHttpClient(ResendEmailSender.HttpClientName);
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<ResendEmailSender>();
        services.AddSingleton<IEmailSender>(provider =>
            provider.GetRequiredService<IOptions<EmailDeliveryOptions>>().Value.Provider
                == EmailProvider.Resend
                ? provider.GetRequiredService<ResendEmailSender>()
                : provider.GetRequiredService<SmtpEmailSender>());
        services.AddSingleton<IPasswordResetEmailSender, PasswordResetEmailSender>();
        services.AddScoped<IEmailWebhookReceiver, ResendWebhookReceiver>();

        var delivery = configuration.GetSection(EmailDeliveryOptions.SectionName).Get<EmailDeliveryOptions>();
        if (delivery?.Enabled == true)
        {
            services.AddHostedService<EmailDeliveryWorker>();
        }

        return services;
    }

    private static bool ValidOptions(EmailDeliveryOptions options)
    {
        if (options.MaxAttempts is < 1 or > 20
            || options.RetryDelaySeconds is < 1 or > 3600
            || options.PollingIntervalSeconds is < 1 or > 3600
            || options.ProcessingLeaseSeconds is < 10 or > 3600
            || options.SendAttemptTimeoutSeconds is < 1 or > 300
            || options.ProcessingLeaseSeconds <= options.SendAttemptTimeoutSeconds)
        {
            return false;
        }

        if (!Enum.IsDefined(options.Provider))
        {
            return false;
        }

        if (!options.Enabled)
        {
            return true;
        }

        if (!Uri.TryCreate(options.ApplicationBaseUrl, UriKind.Absolute, out var applicationUri)
            || applicationUri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return options.Provider == EmailProvider.Resend
            ? ValidResend(options.Resend)
            : ValidSmtp(options.Smtp);
    }

    private static bool ValidResend(ResendOptions options)
        => Uri.TryCreate(options.ApiUrl, UriKind.Absolute, out var apiUri)
            && apiUri.Scheme == "https"
            && !string.IsNullOrWhiteSpace(options.ApiKey)
            && MailboxAddress.TryParse(options.FromAddress, out _);

    private static bool ValidSmtp(SmtpOptions options)
        => !string.IsNullOrWhiteSpace(options.Host)
            && options.Port is >= 1 and <= 65535
            && MailboxAddress.TryParse(options.FromAddress, out _)
            && options.TimeoutSeconds is >= 1 and <= 300
            && Enum.IsDefined(options.Security)
            && (string.IsNullOrWhiteSpace(options.Username)
                == string.IsNullOrWhiteSpace(options.Password));

    private const string InvalidOptionsMessage =
        "Email configuration is invalid. Email:Provider must be Smtp or Resend. When the chosen provider is enabled, "
        + "set an absolute Email:ApplicationBaseUrl; for Resend an https ApiUrl, an ApiKey and a FromAddress; for SMTP the "
        + "host, port, sender address and timeout, with username and "
        + "password either both supplied or both omitted. The processing lease must "
        + "be longer than the complete send-attempt timeout, and retry, lease, SMTP timeout and polling values must be within "
        + "their documented ranges.";
}
