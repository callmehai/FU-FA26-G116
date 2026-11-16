namespace TrustCabinet.Application.Documents;

/// <summary>
/// Section <c>FileStorage</c>. The endpoint, bucket and keys are secrets in practice — set them with
/// <c>dotnet user-secrets</c> or <c>FileStorage__*</c> environment variables, never appsettings.json (ADR-0024).
/// Bound by the infrastructure project, which reads configuration.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>R2: <c>https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>; dev: <c>http://localhost:8333</c>.</summary>
    public string ServiceUrl { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;

    public int OrphanPurgeIntervalMinutes { get; set; } = 60;

    /// <summary>How long an unreferenced file is left alone, so one being attached is not purged mid-way (BR-213).
    /// At least an hour, whatever is configured: an upload's own rows are unreferenced until it commits.</summary>
    public int OrphanGracePeriodHours { get; set; } = 24;

    /// <summary>
    /// What every stored document together may take, in GB (PO 27/10/2026, ADR-0034): R2's free tier holds 10 GB.
    /// The organisation that takes the system over raises it to what it pays for.
    /// </summary>
    public int MaxTotalGigabytes { get; set; } = 10;

    public long MaxTotalBytes => MaxTotalGigabytes * 1024L * 1024 * 1024;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl) && !string.IsNullOrWhiteSpace(BucketName)
        && !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey);
}
