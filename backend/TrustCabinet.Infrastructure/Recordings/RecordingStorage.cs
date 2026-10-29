using System.Buffers.Binary;
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

    /// <summary>
    /// The room mic's part, in bytes: ten seconds of 16 kHz PCM. What has not reached the server yet waits on the
    /// device, so a smaller part only means more requests.
    /// </summary>
    public int RoomPartBytes { get; set; } = 320_000;

    /// <summary>A take no part has reached for this long is saved by the server: the laptop is not coming back.</summary>
    public int IdleTakeHours { get; set; } = 12;
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

    public string Create(string extension)
    {
        var relative = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM"), $"{Guid.CreateVersion7():N}{extension}");
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using (new FileStream(full, FileMode.CreateNew, FileAccess.Write)) { }
        return relative.Replace('\\', '/');
    }

    public async Task<long> WriteAtAsync(string storagePath, long offset, Stream content, long maxBytes, CancellationToken ct)
    {
        await using var file = new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Write, FileShare.Read, 81920, true);
        file.Position = offset;
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new RecordingTooLargeException();
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return total;
    }

    public void Truncate(string storagePath, long length)
    {
        using var file = new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Write, FileShare.Read);
        if (file.Length > length) file.SetLength(length);
    }

    public async Task<string> ConcatAsync(string first, string second, long skip, CancellationToken ct)
    {
        var relative = Create(Path.GetExtension(first));
        try
        {
            await using var target = new FileStream(Resolve(relative), FileMode.Open, FileAccess.Write, FileShare.None, 81920, true);
            await using (var head = OpenRead(first)) await head.CopyToAsync(target, ct);
            await using var tail = OpenRead(second);
            tail.Position = Math.Min(skip, tail.Length);
            await tail.CopyToAsync(target, ct);
        }
        catch
        {
            File.Delete(Resolve(relative));
            throw;
        }
        return relative;
    }

    public async Task<string> CutAsync(string source, long from, long length, CancellationToken ct)
    {
        var relative = Create(Path.GetExtension(source));
        try
        {
            await using var target = new FileStream(Resolve(relative), FileMode.Open, FileAccess.Write, FileShare.None, 81920, true);
            await using var input = OpenRead(source);
            await CopyRangeAsync(input, target, from, ct);
            input.Position = Math.Min(input.Length, from + length);
            await input.CopyToAsync(target, ct);
        }
        catch
        {
            File.Delete(Resolve(relative));
            throw;
        }
        return relative;
    }

    private static async Task CopyRangeAsync(Stream input, Stream target, long count, CancellationToken ct)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)), ct);
            if (read == 0) break;
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            count -= read;
        }
    }

    public void SealWav(string storagePath)
    {
        using var file = new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        if (file.Length < RecordingRules.WavHeaderBytes) return;
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)Math.Min(uint.MaxValue, file.Length - 8));
        file.Position = 4;
        file.Write(size);
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)Math.Min(uint.MaxValue, file.Length - RecordingRules.WavHeaderBytes));
        file.Position = 40;
        file.Write(size);
    }

    public Stream OpenRead(string storagePath)
        => new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, true);

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

/// <summary>
/// The same kind of link for one sentence's audio from a per-mic meeting (ADR-0039). Its key has a label of its own,
/// so a recording's link can never open a sentence, nor the other way round. A sentence is never downloaded.
/// </summary>
public sealed class SpeechClipAudioSigner(IOptions<RecordingOptions> options, byte[] signingKey) : ISpeechClipAudioSigner
{
    private readonly byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, signingKey, 32,
        info: Encoding.UTF8.GetBytes("trustcabinet/speech-clip-audio/v1"));

    public RecordingPlayback Sign(Guid clipId, DateTimeOffset now)
    {
        var expires = now.AddMinutes(options.Value.PlaybackUrlMinutes).ToUnixTimeSeconds();
        return new($"/api/speech-clips/{clipId:D}/audio?expires={expires}&signature={Mac(clipId, expires)}",
            DateTimeOffset.FromUnixTimeSeconds(expires));
    }

    public bool Verify(Guid clipId, long expires, string signature, DateTimeOffset now)
        => expires >= now.ToUnixTimeSeconds() && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Mac(clipId, expires)), Encoding.ASCII.GetBytes(signature));

    private string Mac(Guid clipId, long expires)
        => Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes($"{clipId:N}.{expires}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
