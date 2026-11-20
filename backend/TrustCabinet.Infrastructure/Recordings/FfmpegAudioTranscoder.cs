using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

/// <summary>
/// ADR-0069 — converts a recording to MP3 with ffmpeg while it is downloaded: ffmpeg reads the stored file and writes MP3
/// to its standard output, which goes straight into the response. Nothing is written to disk, so no second copy of the
/// recording exists for retention or erasure to miss. Mono at <see cref="RecordingOptions.Mp3Kbps"/>: speech, not music.
/// </summary>
public sealed class FfmpegAudioTranscoder(IOptions<RecordingOptions> options, ILogger<FfmpegAudioTranscoder> logger)
    : IAudioTranscoder
{
    /// <summary>A failed check is tried again after this long: ffmpeg may be installed without a restart.</summary>
    private static readonly TimeSpan Recheck = TimeSpan.FromMinutes(1);

    private sealed record Probe(bool Ok, long At);

    private volatile Probe? probe;

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (probe is { } known && (known.Ok || Environment.TickCount64 - known.At < Recheck.TotalMilliseconds)) return known.Ok;
        var ok = await ProbeAsync(ct);
        probe = new(ok, Environment.TickCount64);
        return ok;
    }

    /// <summary>ffmpeg runs and has the MP3 encoder: a build without libmp3lame would fail every download.</summary>
    private async Task<bool> ProbeAsync(CancellationToken ct)
    {
        using var process = Prepare(["-hide_banner", "-encoders"], input: false);
        try
        {
            if (!process.Start()) return false;
            var errors = process.StandardError.ReadToEndAsync(ct);
            var encoders = await process.StandardOutput.ReadToEndAsync(ct);
            await errors;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);
            var ok = process.ExitCode == 0 && encoders.Contains("libmp3lame", StringComparison.Ordinal);
            if (!ok) logger.LogError("ffmpeg at {Path} has no MP3 encoder: recordings cannot be downloaded", options.Value.FfmpegPath);
            return ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            Kill(process);
            logger.LogError(ex, "ffmpeg at {Path} cannot run: recordings cannot be downloaded", options.Value.FfmpegPath);
            return false;
        }
    }

    public async Task<Stream> OpenMp3Async(Stream source, CancellationToken ct)
    {
        // A file is handed to ffmpeg by name: an m4a whose index sits at its end cannot be read through a pipe.
        var path = source is FileStream file ? file.Name : null;
        List<string> arguments = ["-hide_banner", "-loglevel", "error"];
        if (path is not null) arguments.Add("-nostdin");
        arguments.AddRange(
        [
            "-i", path is null ? "pipe:0" : $"file:{path}",
            "-vn", "-map_metadata", "-1", "-ac", "1", "-codec:a", "libmp3lame", "-b:a", $"{options.Value.Mp3Kbps}k",
            "-f", "mp3", "pipe:1",
        ]);
        var process = Prepare(arguments, input: path is null);
        try
        {
            if (!process.Start()) throw new AudioTranscodeException("ffmpeg did not start.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            await source.DisposeAsync();
            logger.LogError(ex, "ffmpeg at {Path} cannot run", options.Value.FfmpegPath);
            throw new AudioTranscodeException("ffmpeg cannot run.", ex);
        }

        var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
        // Ends by itself once ffmpeg exits or is killed.
        var feeding = path is null ? FeedAsync(source, process) : Task.CompletedTask;
        var first = new byte[64 * 1024];
        int read;
        try
        {
            read = await process.StandardOutput.BaseStream.ReadAsync(first, ct);
        }
        catch
        {
            Kill(process);
            process.Dispose();
            await source.DisposeAsync();
            throw;
        }
        if (read == 0)
        {
            // Nothing came out: not audio, or a file ffmpeg cannot read. The caller still answers with an error.
            await process.WaitForExitAsync(CancellationToken.None);
            var message = (await errors).Trim();
            var code = process.ExitCode;
            process.Dispose();
            await feeding;
            await source.DisposeAsync();
            logger.LogError("ffmpeg produced no MP3 (exit {Code}): {Errors}", code, message);
            throw new AudioTranscodeException($"ffmpeg produced no MP3 (exit {code}).");
        }
        return new Mp3Stream(process, source, first, read, errors, logger);
    }

    private Process Prepare(IEnumerable<string> arguments, bool input)
    {
        var start = new ProcessStartInfo(options.Value.FfmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardInput = input,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return new Process { StartInfo = start };
    }

    private static async Task FeedAsync(Stream source, Process process)
    {
        try
        {
            await source.CopyToAsync(process.StandardInput.BaseStream);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // ffmpeg stopped reading (it failed, or the download was cancelled): its exit says why.
        }
        finally
        {
            try { process.StandardInput.Close(); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Never started, or already gone.
        }
    }

    /// <summary>
    /// ffmpeg's output, with the bytes read to make sure it started. A conversion that fails halfway throws at the end
    /// instead of ending quietly, so the browser marks the download failed rather than keep a cut-off file.
    /// </summary>
    private sealed class Mp3Stream(Process process, Stream source, byte[] head, int headLength, Task<string> errors,
        ILogger logger) : Stream
    {
        private int headAt;
        private bool ended;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (headAt < headLength)
            {
                var count = Math.Min(buffer.Length, headLength - headAt);
                head.AsMemory(headAt, count).CopyTo(buffer);
                headAt += count;
                return count;
            }
            if (ended) return 0;
            var read = await process.StandardOutput.BaseStream.ReadAsync(buffer, cancellationToken);
            if (read > 0) return read;
            ended = true;
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                logger.LogError("ffmpeg failed while converting a download (exit {Code}): {Errors}", process.ExitCode, (await errors).Trim());
                throw new IOException($"ffmpeg failed while converting (exit {process.ExitCode}).");
            }
            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Kill(process);
                process.Dispose();
                source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
