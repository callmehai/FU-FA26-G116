using System.Diagnostics;
using System.Globalization;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Web;

/// <summary>
/// ADR-0044: an uploaded PDF is parsed in a child process of this same app (<c>--check-pdf &lt;file&gt;</c>), never in the
/// server's own. PdfPig walks a PDF's page tree recursively, so a 2 MB file of nested page nodes overflows the stack —
/// which .NET cannot catch, and which would end the whole server — and a 1 MB file can inflate to gigabytes. In the
/// child that costs only the child: a crash, the memory cap or the time limit each read as "not a valid PDF" (MSG164).
/// </summary>
public static class PdfPageCounter
{
    public const string ChildArgument = "--check-pdf";

    /// <summary>A real meeting paper parses in well under a second; this only stops a file built to hang the parser.</summary>
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(15);

    /// <summary>The child's GC heap cap: a 500-page PDF needs tens of MB, a decompression bomb hits this and dies.</summary>
    private const long HeapLimitBytes = 384L * 1024 * 1024;

    /// <summary>At most two parsers at once, so ten hostile files in parallel cannot take the server's memory.</summary>
    private static readonly SemaphoreSlim Slots = new(2);

    /// <summary>
    /// How long an upload waits for a free parser. Two slots of at most 15 s each: past this, someone is keeping both
    /// busy, and the upload answers "try again" (503) rather than queueing everyone behind them.
    /// </summary>
    private static readonly TimeSpan SlotWait = TimeSpan.FromSeconds(30);

    /// <summary>How much of the child's stderr the log keeps when it dies: enough to tell a crash from a missing host.</summary>
    private const int StderrLogChars = 1000;

    private const string Prefix = "pages=";

    /// <summary>No parser came free within <see cref="SlotWait"/>: not the file's fault, so not MSG164.</summary>
    public sealed class BusyException() : Exception("No PDF check slot came free in time.");

    /// <summary>
    /// The page count, or null when the file is not a PDF the viewer can show. A child that cannot be started is a
    /// server fault, not a bad file, and throws; so does waiting too long for a slot (<see cref="BusyException"/>).
    /// </summary>
    public static Task<int?> CountAsync(Stream content, ILogger logger, CancellationToken ct)
        => CountAsync(content, logger, Slots, SlotWait, ct);

    /// <summary><see cref="CountAsync(Stream, ILogger, CancellationToken)"/> with its slots given, for tests.</summary>
    public static async Task<int?> CountAsync(Stream content, ILogger logger, SemaphoreSlim slots, TimeSpan slotWait,
        CancellationToken ct)
    {
        // Uploads are already temp files on disk; anything else (a test's memory stream) is written to one.
        var path = (content as FileStream)?.Name;
        string? copy = null;
        if (path is null)
        {
            copy = path = Path.GetTempFileName();
            await using var file = File.Create(copy);
            content.Position = 0;
            await content.CopyToAsync(file, ct);
        }
        try
        {
            if (!await slots.WaitAsync(slotWait, ct))
            {
                logger.LogWarning("No PDF check slot came free within {Wait}; the upload is refused as busy.", slotWait);
                throw new BusyException();
            }
            try
            {
                return await RunChildAsync(path, logger, ct);
            }
            finally
            {
                slots.Release();
            }
        }
        finally
        {
            if (copy is not null) File.Delete(copy);
        }
    }

    private static async Task<int?> RunChildAsync(string path, ILogger logger, CancellationToken ct)
    {
        var (host, apphost) = Host();
        var start = new ProcessStartInfo(host)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // This assembly, run as an app of its own: the same build, so the same PdfPig and the same rules. The app's own
        // executable needs no assembly path; the dotnet host does.
        if (!apphost) start.ArgumentList.Add(typeof(PdfPageCounter).Assembly.Location);
        start.ArgumentList.Add(ChildArgument);
        start.ArgumentList.Add(path);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x" + HeapLimitBytes.ToString("X", CultureInfo.InvariantCulture);

        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the PDF check.");
        var output = child.StandardOutput.ReadToEndAsync(ct);
        var error = child.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeLimit);
        try
        {
            await child.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true);
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("The PDF check ran past {Limit} and was killed; the file is refused (MSG164).", TimeLimit);
            return null;
        }
        var line = (await output).Trim();
        // Exit 2 is the child's own "does not parse". Anything else is a crash, the memory cap, or a host that could
        // not run the app at all — which would refuse every PDF, so it must show in the log.
        if (child.ExitCode is not (0 or 2))
        {
            var stderr = (await error).Trim();
            logger.LogWarning("The PDF check exited with code {ExitCode}; the file is refused (MSG164). stderr: {Stderr}",
                child.ExitCode, stderr.Length > StderrLogChars ? stderr[..StderrLogChars] + "…" : stderr);
        }
        return child.ExitCode == 0 && line.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(line[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var pages) && pages > 0
            ? pages : null;
    }

    /// <summary>
    /// What to start: this app's own executable when it runs as one (<c>dotnet run</c>, a published apphost), else the
    /// dotnet host running us (the container), else the SDK's (tests run under testhost). Only the last needs PATH.
    /// </summary>
    private static (string Host, bool Apphost) Host()
    {
        var running = Environment.ProcessPath;
        var name = running is null ? null : Path.GetFileNameWithoutExtension(running);
        if (string.Equals(name, typeof(PdfPageCounter).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
            return (running!, true);
        if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase)) return (running!, false);
        return (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } sdk ? sdk : "dotnet", false);
    }

    /// <summary>The child's side: prints <c>pages=N</c> and exits 0, or exits 2 for a file that does not parse.</summary>
    public static async Task<int> RunAsChildAsync(string path)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (await DocumentFileRules.CountPdfPagesAsync(file, CancellationToken.None) is not { } pages) return 2;
        Console.Out.Write(Prefix + pages.ToString(CultureInfo.InvariantCulture));
        return 0;
    }
}
