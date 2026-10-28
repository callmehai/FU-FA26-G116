using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

public static class RecordingServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetRecordings(this IServiceCollection services,
        IConfiguration configuration, string contentRoot)
    {
        services.AddOptions<RecordingOptions>().Bind(configuration.GetSection(RecordingOptions.SectionName))
            .PostConfigure(o => o.StorageRoot = Path.GetFullPath(o.StorageRoot, contentRoot));
        services.AddSingleton<IRecordingStorage, LocalRecordingStorage>();
        services.AddSingleton<IRecordingPlaybackSigner>(provider => new RecordingPlaybackSigner(
            provider.GetRequiredService<IOptions<RecordingOptions>>(),
            Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"] ?? "")));
        services.AddSingleton<ISpeechClipAudioSigner>(provider => new SpeechClipAudioSigner(
            provider.GetRequiredService<IOptions<RecordingOptions>>(),
            Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"] ?? "")));
        services.AddScoped<IRecordingRepository, RecordingRepository>();
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddSingleton<SpeechClipGate>();
        services.AddScoped<ISpeechClipService, SpeechClipService>();
        return services;
    }
}
