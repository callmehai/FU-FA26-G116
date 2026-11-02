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

    private const string Prefix = "pages=";

    /// <summary>
    /// The page count, or null when the file is not a PDF the viewer can show. A child that cannot be started is a
    /// server fault, not a bad file, and throws.
    /// </summary>
    public static async Task<int?> CountAsync(Stream content, CancellationToken ct)
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
            await Slots.WaitAsync(ct);
            try
            {
                return await RunChildAsync(path, ct);
            }
            finally
            {
                Slots.Release();
            }
        }
        finally
        {
            if (copy is not null) File.Delete(copy);
        }
    }

    private static async Task<int?> RunChildAsync(string path, CancellationToken ct)
    {
        var start = new ProcessStartInfo(Host())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // This assembly, run as an app of its own: the same build, so the same PdfPig and the same rules.
        start.ArgumentList.Add(typeof(PdfPageCounter).Assembly.Location);
        start.ArgumentList.Add(ChildArgument);
        start.ArgumentList.Add(path);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x" + HeapLimitBytes.ToString("X", CultureInfo.InvariantCulture);

        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the PDF check.");
        var output = child.StandardOutput.ReadToEndAsync(ct);
        _ = child.StandardError.ReadToEndAsync(ct);
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
            return null;
        }
        var line = (await output).Trim();
        return child.ExitCode == 0 && line.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(line[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var pages) && pages > 0
            ? pages : null;
    }

    /// <summary>The dotnet host: the one running us (as in the container), else the SDK's (tests, <c>dotnet run</c>).</summary>
    private static string Host()
    {
        var running = Environment.ProcessPath;
        if (running is not null && Path.GetFileNameWithoutExtension(running).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return running;
        return Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } sdk ? sdk : "dotnet";
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
