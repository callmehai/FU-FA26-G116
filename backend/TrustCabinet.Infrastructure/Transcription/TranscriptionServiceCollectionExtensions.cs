using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;

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
        // A Mac asleep or off the tailnet never refuses a connection; give up on it quickly so the backup takes over
        // (ADR-0081). The whole request still has Speech:TimeoutMinutes.
        services.AddHttpClient(SpeechWorkerClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(provider =>
            new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(
                    Math.Max(1, provider.GetRequiredService<IOptions<SpeechOptions>>().Value.ConnectTimeoutSeconds)),
            });
        services.TryAddSingleton(TimeProvider.System);
        // One client for both jobs of the worker; tests replace either interface on its own.
        services.AddSingleton<SpeechWorkerClient>();
        services.AddSingleton<ISpeechEngine>(provider => provider.GetRequiredService<SpeechWorkerClient>());
        services.AddSingleton<IVoiceEmbedder>(provider => provider.GetRequiredService<SpeechWorkerClient>());
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        return services;
    }
}
