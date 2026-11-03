using Microsoft.Extensions.Logging.Abstractions;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>ADR-0044: the PDF check runs in a child process, at most two at once, and an upload does not wait forever.</summary>
public sealed class PdfPageCounterTests
{
    [Fact]
    public async Task Counts_pages_in_a_child_process()
    {
        using var slots = new SemaphoreSlim(1);
        Assert.Equal(3, await PdfPageCounter.CountAsync(new MemoryStream(SampleFiles.Pdf("x", pages: 3)),
            NullLogger.Instance, slots, TimeSpan.FromSeconds(5), default));
        Assert.Equal(1, slots.CurrentCount);
    }

    [Fact]
    public async Task No_free_slot_in_time_is_busy_not_an_invalid_file()
    {
        // Both slots taken, as when someone keeps both parsers busy with files built to hang them.
        using var slots = new SemaphoreSlim(0);
        await Assert.ThrowsAsync<PdfPageCounter.BusyException>(() => PdfPageCounter.CountAsync(
            new MemoryStream(SampleFiles.Pdf()), NullLogger.Instance, slots, TimeSpan.FromMilliseconds(50), default));
        Assert.Equal(0, slots.CurrentCount);
    }
}
