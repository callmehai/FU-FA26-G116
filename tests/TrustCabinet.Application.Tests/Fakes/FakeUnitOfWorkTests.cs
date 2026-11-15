using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// The fake must count the way <c>EfUnitOfWork</c> behaves (its own tests are in
/// <c>TrustCabinet.Web.Tests/Persistence</c>), or every use case test built on it asserts the wrong thing.
/// </summary>
public sealed class FakeUnitOfWorkTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Work_that_returns_is_committed_and_its_mode_recorded()
    {
        var result = await _unitOfWork.ExecuteAsync(_ => Task.FromResult(7), TransactionMode.RepeatableRead);

        Assert.Equal(7, result);
        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal([TransactionMode.RepeatableRead], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Work_that_throws_is_rolled_back_and_the_exception_passes_through()
    {
        await Assert.ThrowsAsync<UniqueConflictException>(() => _unitOfWork.ExecuteAsync(
            _ => throw new UniqueConflictException(UniqueConflict.Unknown)));

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task CommitWhen_false_rolls_back_and_still_returns_the_result()
    {
        var result = await _unitOfWork.ExecuteAsync(_ => Task.FromResult("refused"), outcome => outcome != "refused");

        Assert.Equal("refused", result);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Only_the_outermost_transactional_call_commits()
    {
        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _unitOfWork.ExecuteAsync(_ => Task.CompletedTask, TransactionMode.None, ct);
            await _unitOfWork.ExecuteAsync(_ => Task.CompletedTask, TransactionMode.ReadCommitted, ct);
        });

        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(
            [TransactionMode.ReadCommitted, TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_stronger_level_inside_a_weaker_transaction_is_a_programming_error()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteAsync(
            ct => _unitOfWork.ExecuteAsync(_ => Task.CompletedTask, TransactionMode.RepeatableRead, ct)));

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task No_transaction_means_nothing_to_commit_or_roll_back()
    {
        await _unitOfWork.ExecuteAsync(_ => Task.CompletedTask, TransactionMode.None);

        Assert.Equal((0, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task The_failure_set_for_the_next_save_is_thrown_once()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => _unitOfWork.SaveChangesAsync());
        await _unitOfWork.SaveChangesAsync();
        _unitOfWork.DiscardChanges();

        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Discards));
    }
}
