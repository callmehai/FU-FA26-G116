using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Application.Tests.Documents;

public sealed class StoredFileWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeStoredFileRepository _files = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _actor = Guid.CreateVersion7();

    private StoredFileWriter Writer() => new(_files, _storage, _unitOfWork, new FixedClock(Now),
        Options.Create(new FileStorageOptions()), NullLogger<StoredFileWriter>.Instance);

    private static DocumentUploadFile File(string name = "a.pdf") => new(name, "application/pdf", 3,
        new string('a', 64), 1, () => new MemoryStream([1, 2, 3]));

    [Fact]
    public async Task Reserving_then_committing_writes_rows_bytes_and_links()
    {
        var outcome = await Writer().WriteAsync(_actor, [File()],
            async (stored, now, token) =>
            {
                Assert.Single(stored);
                await _unitOfWork.SaveChangesAsync(token);
                return "ok";
            },
            result => result == "ok", "failed", "full", default);

        Assert.Equal("ok", outcome);
        Assert.Single(_files.Added);
        Assert.Single(_storage.Objects);
        Assert.Equal(2, _unitOfWork.Commits);
        Assert.Equal(2, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Empty(_files.Stamped);
    }

    [Fact]
    public async Task A_full_store_answers_full_before_any_byte_moves()
    {
        _files.UsedBytes = new FileStorageOptions().MaxTotalBytes;

        var outcome = await Writer().WriteAsync(_actor, [File()],
            (_, _, _) => Task.FromResult("ok"),
            result => result == "ok", "failed", "full", default);

        Assert.Equal("full", outcome);
        Assert.Empty(_storage.Objects);
        Assert.Empty(_files.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_failed_commit_deletes_the_objects_and_stamps_the_rows_purged()
    {
        var outcome = await Writer().WriteAsync(_actor, [File()],
            (_, _, _) => throw new PersistenceUnavailableException(),
            result => result == "ok", "failed", "full", default);

        Assert.Equal("failed", outcome);
        Assert.Empty(_storage.Objects);
        Assert.Single(_files.Added);
        Assert.Single(_files.Stamped);
    }

    [Fact]
    public async Task A_refused_commit_rolls_back_and_releases_the_room()
    {
        var outcome = await Writer().WriteAsync(_actor, [File()],
            (_, _, _) => Task.FromResult("invalid"),
            result => result == "ok", "failed", "full", default);

        Assert.Equal("invalid", outcome);
        Assert.Empty(_storage.Objects);
        Assert.Single(_files.Stamped);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
