using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// Section <c>FileStorage</c>. The endpoint, bucket and keys are secrets in practice — set them with
/// <c>dotnet user-secrets</c> or <c>FileStorage__*</c> environment variables, never appsettings.json (ADR-0024).
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

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl) && !string.IsNullOrWhiteSpace(BucketName)
        && !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey);
}

/// <summary>Any S3-compatible store: Cloudflare R2 on the test server, SeaweedFS in development.</summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly FileStorageOptions _options;
    private readonly AmazonS3Client? _client;

    public S3FileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
        if (!_options.IsConfigured) return;
        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                // R2 treats us-east-1 as an alias of its "auto" region; SeaweedFS ignores the region.
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,
                // R2 rejects the CRC trailers the SDK would otherwise add to every request.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public bool IsConfigured => _client is not null;

    private AmazonS3Client Client => _client
        ?? throw new InvalidOperationException("FileStorage is not configured. See docs/02-design/03-co-so-du-lieu.md.");

    public async Task WriteAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType,
            UseChunkEncoding = false,
            // Over HTTPS, TLS already protects the body, and signing it would make the SDK read the
            // whole file once more just to hash it. The SDK refuses unsigned payloads over HTTP.
            DisablePayloadSigning = _options.ServiceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
        };
        request.Headers.ContentLength = length;
        await Client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await Client.GetObjectAsync(_options.BucketName, key, ct);
            return response.ResponseStream;
        }
        // Only a missing key: a missing bucket is misconfiguration and must not read as "file gone".
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NoSuchKey")
        {
            throw new FileNotFoundException($"Object {key} does not exist.", key, ex);
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct)
        => Client.DeleteObjectAsync(_options.BucketName, key, ct);

    public void Dispose() => _client?.Dispose();
}
