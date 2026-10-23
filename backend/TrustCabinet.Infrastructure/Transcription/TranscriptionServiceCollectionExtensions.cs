using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Infrastructure.Transcription;

public static class TranscriptionServiceCollectionExtensions
{
    /// <summary>
    /// Registers speech to text (SCRUM-31). Like LiveKit, a missing Speech section is not an error: the app starts,
    /// and a transcript fails with a message saying the speech worker is not configured.
    /// </summary>
    public static IServiceCollection AddTrustCabinetTranscription(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.AddHttpClient(SpeechWorkerClient.HttpClientName);
        services.AddSingleton<ISpeechEngine, SpeechWorkerClient>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<ITranscriptionService, TranscriptionService>();
        return services;
    }
}
