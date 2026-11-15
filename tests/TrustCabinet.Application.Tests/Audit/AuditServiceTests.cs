using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;

namespace TrustCabinet.Application.Tests.Audit;

public sealed class AuditServiceTests
{
    private readonly FakeAuditLog _log = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AuditService Service(string ipAddress = "") => new(_log, _unitOfWork, new FakeCallerContext(ipAddress));

    [Fact]
    public async Task Records_one_row_with_the_caller_address_and_saves_it_at_once()
    {
        var (objectId, actorId) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        await Service("203.0.113.7").RecordAsync(AuditActions.UserCreated, AuditActions.UserObjectType, objectId, actorId,
            new { FullName = "Trần Chủ Toạ" });

        var row = Assert.Single(_log.Rows);
        Assert.Equal((AuditActions.UserCreated, objectId, actorId, "203.0.113.7"),
            (row.Action, row.ObjectId, row.ActorId, row.IpAddress));
        Assert.Equal("""{"FullName":"Trần Chủ Toạ"}""", row.Details);
        // It saves on its own rather than with the caller's unit of work (see AuditService).
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task No_details_is_an_empty_object()
    {
        await Service().RecordAsync(AuditActions.UserReactivated, AuditActions.UserObjectType, Guid.CreateVersion7(), null);

        Assert.Equal("{}", Assert.Single(_log.Rows).Details);
    }

    [Fact]
    public async Task A_failed_save_reaches_the_caller()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().RecordAsync(
            AuditActions.UserUpdated, AuditActions.UserObjectType, Guid.CreateVersion7(), null));
    }
}
