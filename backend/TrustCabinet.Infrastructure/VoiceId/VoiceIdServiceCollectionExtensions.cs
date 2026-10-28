using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

public static class VoiceIdServiceCollectionExtensions
{
    /// <summary>SCRUM-35 — Voice ID. Needs AddTrustCabinetTranscription for the speech worker client.</summary>
    public static IServiceCollection AddTrustCabinetVoiceId(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VoiceIdOptions>(configuration.GetSection(VoiceIdOptions.SectionName));
        services.AddSingleton<IVoiceprintProtector>(provider => new VoiceprintProtector(
            provider.GetRequiredService<IOptions<VoiceIdOptions>>().Value.EncryptionKey,
            Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"] ?? "")));
        services.AddScoped<IVoiceIdRepository, VoiceIdRepository>();
        services.AddScoped<IVoiceIdService, VoiceIdService>();
        return services;
    }
}
