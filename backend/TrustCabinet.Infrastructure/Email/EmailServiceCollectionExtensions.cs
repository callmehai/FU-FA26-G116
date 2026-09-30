using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;
using TrustCabinet.Domain.Notifications;

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
        services.AddScoped<IMeetingInvitationQueue, MeetingInvitationQueue>();
        services.AddScoped<EmailQueueProcessor>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        if (configuration.GetValue<bool>("Email:Smtp:Enabled"))
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
            || options.ProcessingLeaseSeconds is < 10 or > 3600)
        {
            return false;
        }

        if (!options.Smtp.Enabled)
        {
            return true;
        }

        return Uri.TryCreate(options.ApplicationBaseUrl, UriKind.Absolute, out var applicationUri)
            && applicationUri.Scheme is "http" or "https"
            && !string.IsNullOrWhiteSpace(options.Smtp.Host)
            && options.Smtp.Port is >= 1 and <= 65535
            && MailboxAddress.TryParse(options.Smtp.FromAddress, out _)
            && options.Smtp.TimeoutSeconds is >= 1 and <= 300
            && options.ProcessingLeaseSeconds > options.Smtp.TimeoutSeconds
            && Enum.IsDefined(options.Smtp.Security)
            && (string.IsNullOrWhiteSpace(options.Smtp.Username)
                == string.IsNullOrWhiteSpace(options.Smtp.Password));
    }

    private const string InvalidOptionsMessage =
        "Email configuration is invalid. When Email:Smtp:Enabled is true, set an absolute "
        + "Email:ApplicationBaseUrl, SMTP host, port, sender address and timeout. Username and "
        + "password must either both be supplied or both be omitted. The processing lease must "
        + "be longer than the SMTP timeout, and retry, lease and polling values must be within "
        + "their documented ranges.";
}
