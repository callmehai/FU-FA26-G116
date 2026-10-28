using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

public sealed class RecordingOptions
{
    public const string SectionName = "Recordings";

    /// <summary>
    /// Where audio files are written. Relative paths resolve against the app's content root; the test server mounts
    /// a volume here so a redeploy does not erase every recording.
    /// </summary>
    public string StorageRoot { get; set; } = "App_Data/recordings";

    /// <summary>
    /// How long a recording is kept before it is erased automatically — the "thời hạn lưu trữ" Luật 91/2025 asks to be
    /// stated at collection. Long enough to write and approve the minutes, short enough not to become an archive.
    /// </summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>Three hours of mp3 or one of uncompressed 16 kHz WAV fit with room to spare.</summary>
    public int MaxUploadMegabytes { get; set; } = 300;

    /// <summary>How long a signed playback URL works. Covers listening through a long meeting without re-signing.</summary>
    public int PlaybackUrlMinutes { get; set; } = 120;
}

/// <summary>
/// Recordings on the local disk. Files are named by a fresh id, never by the uploaded name, so a client cannot
/// choose where its bytes land or overwrite someone else's.
/// </summary>
public sealed class LocalRecordingStorage(IOptions<RecordingOptions> options) : IRecordingStorage
{
    private string Root => Path.GetFullPath(options.Value.StorageRoot);

    public async Task<(string StoragePath, long ByteSize)> SaveAsync(Stream content, string extension, long maxBytes,
        CancellationToken ct)
    {
        var relative = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM"), $"{Guid.CreateVersion7():N}{extension}");
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        long total = 0;
        try
        {
            await using var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > maxBytes) throw new RecordingTooLargeException();
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch
        {
            File.Delete(full);
            throw;
        }
        return (relative.Replace('\\', '/'), total);
    }

    public Stream OpenRead(string storagePath)
        => new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

    public void Delete(string storagePath)
    {
        if (string.IsNullOrEmpty(storagePath)) return;
        File.Delete(Resolve(storagePath));
    }

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Recording path escapes the storage root.");
        return full;
    }
}

/// <summary>
/// HMAC over "recording id + expiry", plus ".download" for a link that saves the file. The key is derived from the
/// token signing key with its own label, so the two can never be mistaken for each other and no extra secret has to
/// be configured.
/// </summary>
public sealed class RecordingPlaybackSigner(IOptions<RecordingOptions> options, byte[] signingKey) : IRecordingPlaybackSigner
{
    private readonly byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, signingKey, 32,
        info: Encoding.UTF8.GetBytes("trustcabinet/recording-playback/v1"));

    public RecordingPlayback Sign(Guid recordingId, DateTimeOffset now, bool download = false)
    {
        var expiresAt = now.AddMinutes(options.Value.PlaybackUrlMinutes);
        var expires = expiresAt.ToUnixTimeSeconds();
        return new($"/api/recordings/{recordingId:D}/audio?expires={expires}&signature={Mac(recordingId, expires, download)}"
            + (download ? "&download=1" : ""), DateTimeOffset.FromUnixTimeSeconds(expires));
    }

    public bool Verify(Guid recordingId, long expires, string signature, DateTimeOffset now, bool download = false)
    {
        if (expires < now.ToUnixTimeSeconds()) return false;
        var expected = Encoding.ASCII.GetBytes(Mac(recordingId, expires, download));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(signature));
    }

    private string Mac(Guid recordingId, long expires, bool download)
        => Base64Url(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes($"{recordingId:N}.{expires}{(download ? ".download" : "")}")));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
