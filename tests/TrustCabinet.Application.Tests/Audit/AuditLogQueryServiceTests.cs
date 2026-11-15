using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Audit;

public sealed class AuditLogQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeAuditLog _log = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AuditLogQueryService _service;

    public AuditLogQueryServiceTests()
        => _service = new(_log, _unitOfWork, NullLogger<AuditLogQueryService>.Instance);

    private static AuditLogQuery Query(int page = 1, int pageSize = 20)
        => new(null, null, null, null, null, null, page, pageSize);

    private AuditLogEntry Row(Guid? actorId, string details = "{}", string objectType = "Meeting", Guid? objectId = null,
        int minutesAgo = 0)
    {
        var row = new AuditLogEntry
        {
            ActorId = actorId, Action = AuditActions.UserCreated, ObjectType = objectType, ObjectId = objectId,
            Details = details, OccurredAt = Now.AddMinutes(-minutesAgo),
        };
        _log.Rows.Add(row);
        return row;
    }

    [Fact]
    public async Task Names_the_user_the_guest_and_the_system_newest_first()
    {
        var (userId, roleId, invitationId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        _log.Users[userId] = new("Trần Thư Ký", "thuky@example.com");
        _log.Roles[roleId] = "Thư ký";
        _log.Invitations[invitationId] = new("Khách mời", "khach@example.com");
        var byUser = Row(userId, objectType: AuditActions.RoleObjectType, objectId: roleId, minutesAgo: 2);
        var byGuest = Row(null, $$$"""{"guest":{"InvitationId":"{{{invitationId}}}"}}""", minutesAgo: 1);
        var bySystem = Row(null);

        var page = await _service.GetPageAsync(Query());

        Assert.NotNull(page);
        Assert.Equal([bySystem.Id, byGuest.Id, byUser.Id], page.Items.Select(i => i.Id));
        Assert.Equal(new AuditActorView(AuditActorKind.System, null, null, null), page.Items[0].Actor);
        Assert.Equal(new AuditActorView(AuditActorKind.Guest, invitationId, "Khách mời", "khach@example.com"), page.Items[1].Actor);
        Assert.Equal(new AuditActorView(AuditActorKind.User, userId, "Trần Thư Ký", "thuky@example.com"), page.Items[2].Actor);
        Assert.Equal("Thư ký", page.Items[2].ObjectName);
        Assert.Equal((3, 1), (page.TotalCount, page.TotalPages));
        Assert.Equal(["count", "page", "names"], _log.Reads);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_and_reads_no_rows()
    {
        Row(null);

        var page = await _service.GetPageAsync(Query(page: 3, pageSize: 1));

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal((1, 1), (page.TotalCount, page.TotalPages));
        Assert.Equal(["count"], _log.Reads);
    }

    [Fact]
    public async Task An_unreadable_log_answers_null()
    {
        _log.Fail = new PersistenceUnavailableException();

        Assert.Null(await _service.GetPageAsync(Query()));
    }
}
