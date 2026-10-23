using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

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
        // One client for both jobs of the worker; tests replace either interface on its own.
        services.AddSingleton<SpeechWorkerClient>();
        services.AddSingleton<ISpeechEngine>(provider => provider.GetRequiredService<SpeechWorkerClient>());
        services.AddSingleton<IVoiceEmbedder>(provider => provider.GetRequiredService<SpeechWorkerClient>());
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<ITranscriptionService, TranscriptionService>();
        return services;
    }
}
