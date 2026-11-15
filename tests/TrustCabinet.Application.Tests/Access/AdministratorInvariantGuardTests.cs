using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Tests.Fakes;

namespace TrustCabinet.Application.Tests.Access;

public sealed class AdministratorInvariantGuardTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRolePermissionRepository _roles = new();

    private AdministratorInvariantGuard Guard() => new(_unitOfWork, _roles);

    [Fact]
    public async Task Takes_the_lock_before_the_write_in_one_read_committed_transaction()
    {
        var result = await Guard().WriteAsync(_ =>
        {
            _roles.Calls.Add("write");
            return Task.FromResult(7);
        });

        Assert.Equal(7, result);
        Assert.Equal(["lock", "write"], _roles.Calls);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_nested_write_joins_the_outer_transaction()
    {
        var guard = Guard();

        await guard.WriteAsync(_ => guard.WriteAsync(_ => Task.FromResult(0)));

        // One commit, by the outer call; the inner one only joins.
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_write_that_throws_rolls_back()
    {
        await Assert.ThrowsAsync<PersistenceUnavailableException>(() =>
            Guard().WriteAsync<int>(_ => throw new PersistenceUnavailableException()));

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
