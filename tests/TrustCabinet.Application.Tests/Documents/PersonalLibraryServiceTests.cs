using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Documents;

public sealed class PersonalLibraryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakePersonalLibraryRepository _library = new();
    private readonly FakeStoredFileRepository _storedFiles = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _owner = Guid.CreateVersion7();

    private PersonalLibraryService Service() => new(_library,
        new StoredFileWriter(_storedFiles, _storage, _unitOfWork, new FixedClock(Now),
            Options.Create(new FileStorageOptions()), NullLogger<StoredFileWriter>.Instance),
        _storage, _audit, _unitOfWork, new FixedClock(Now),
        NullLogger<PersonalLibraryService>.Instance);

    [Fact]
    public async Task Viewing_lists_folders_and_documents_in_one_read()
    {
        var folder = new Folder { OwnerId = _owner, Name = "Kế hoạch", CreatedAt = Now, UpdatedAt = Now };
        _library.Folders[folder.Id] = folder;
        _library.DocumentRows.Add(new LibraryDocument(Guid.CreateVersion7(), "a.pdf", "application/pdf",
            3, 1, folder.Id, Now, 0));

        var outcome = await Service().ViewAsync(_owner, _owner, folder.Id, null, null);

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Value);
        Assert.Single(outcome.Value.Folders);
        Assert.Single(outcome.Value.Documents);
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Viewing_answers_NotFound_for_someone_elses_library()
    {
        var outcome = await Service().ViewAsync(Guid.CreateVersion7(), _owner, null, null, null);

        Assert.Equal(LibraryFailure.NotFound, outcome.Failure);
        Assert.Equal(["deactivated"], _library.Calls);
    }

    [Fact]
    public async Task Viewing_maps_a_database_failure_to_save_failed()
    {
        _library.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ViewAsync(_owner, _owner, null, null, null);

        Assert.Equal(LibraryFailure.SaveFailed, outcome.Failure);
    }

    [Fact]
    public async Task Creating_a_folder_stages_it_and_commits_once()
    {
        var outcome = await Service().CreateFolderAsync(_owner, null, "Kế hoạch");

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.Equal("Kế hoạch", outcome.Value!.Name);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal([_owner], _library.Locks);
    }

    [Fact]
    public async Task Creating_a_folder_too_deep_rolls_back_without_saving()
    {
        var at = (Guid?)null;
        for (var depth = 0; depth < 5; depth++)
        {
            var folder = new Folder { OwnerId = _owner, ParentId = at, Name = $"Cấp {depth}",
                CreatedAt = Now, UpdatedAt = Now };
            _library.Folders[folder.Id] = folder;
            at = folder.Id;
        }

        var outcome = await Service().CreateFolderAsync(_owner, at, "Cấp 5");

        Assert.Equal(LibraryFailure.TooDeep, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
