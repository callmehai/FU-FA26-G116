using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Infrastructure.Documents;

public static class DocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers meeting documents and their file store (ADR-0024). A missing FileStorage section is
    /// not an error: the app still starts, uploads answer 503/MSG166 and the purge stays idle, so
    /// nobody working on something else has to run a store.
    /// </summary>
    public static IServiceCollection AddTrustCabinetDocuments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddScoped<IMeetingDocumentRepository, MeetingDocumentRepository>();
        services.AddScoped<IMeetingDocumentService, MeetingDocumentService>();
        services.AddScoped<StoredFileWriter>();
        services.AddScoped<IPersonalLibraryService, PersonalLibraryService>();
        services.AddScoped<IDocumentCommentService, DocumentCommentService>();
        services.AddScoped<OrphanFilePurger>();
        services.AddHostedService<OrphanFilePurgeWorker>();
        return services;
    }
}
