using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Ai;
using TrustCabinet.Infrastructure.Ai;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

public static class SummaryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI draft (SCRUM-36, ADR-0054). Like the speech worker, a missing Ai section is not an error: the app
    /// starts, and asking for a draft answers that the AI is not ready.
    /// </summary>
    public static IServiceCollection AddTrustCabinetSummaries(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LanguageModelOptions>(configuration.GetSection(LanguageModelOptions.SectionName));
        services.AddHttpClient(LanguageModelClient.HttpClientName);
        services.AddSingleton<ILanguageModel, LanguageModelClient>();
        // ADR-0056: an API for ordinary meetings, set up by an administrator, its key encrypted at rest.
        services.AddHttpClient(LanguageModelProvider.ApiHttpClientName);
        services.AddSingleton(provider => new AiKeyProtector(
            provider.GetRequiredService<IOptions<LanguageModelOptions>>().Value.EncryptionKey,
            Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"] ?? "")));
        services.AddScoped<ILanguageModelProvider, LanguageModelProvider>();
        services.AddScoped<IAiSettingsService, AiSettingsService>();
        services.AddScoped<MeetingTranscriptReader>();
        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<IMinutesService, MinutesService>();
        return services;
    }
}
