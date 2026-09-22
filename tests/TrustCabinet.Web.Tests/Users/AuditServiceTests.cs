using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Audit;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// What <see cref="AuditService"/> puts in the row — the action, the actor, the caller's address
/// and the JSON details.
///
/// Against EF Core's in-memory provider rather than a real database. The full schema is
/// PostgreSQL-specific (citext, jsonb, a filtered unique index), so SQLite cannot create it and
/// standing up PostgreSQL would make CI depend on a container for three assertions. What is under
/// test here is the content this service produces, not the DDL — and
/// <c>DatabaseModelTests</c> already covers the mapping against the real provider.
/// </summary>
public class AuditServiceTests : IDisposable
{
    private readonly TrustCabinetDbContext _db = new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            // A fresh database per test class instance, so rows from one test cannot be seen by
            // the next — xUnit constructs the class once per test.
            .UseInMemoryDatabase($"audit-{Guid.CreateVersion7()}")
            .Options);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Record_stores_the_action_actor_and_caller_address()
    {
        var actorId = Guid.CreateVersion7();
        var objectId = Guid.CreateVersion7();
        var service = new AuditService(_db, new FakeCallerContext("203.0.113.7"));

        await service.RecordAsync(
            AuditActions.UserCreated, AuditActions.UserObjectType, objectId, actorId,
            new { Email = "moi@trustcabinet.local", FullName = "Trần Chủ Toạ" });

        var entry = await _db.AuditLog.SingleAsync();

        Assert.Equal(AuditActions.UserCreated, entry.Action);
        Assert.Equal(AuditActions.UserObjectType, entry.ObjectType);
        Assert.Equal(objectId, entry.ObjectId);
        Assert.Equal(actorId, entry.ActorId);
        // Where the request came from is part of the evidence, so it is stored with the row.
        Assert.Equal("203.0.113.7", entry.IpAddress);
        Assert.Contains("moi@trustcabinet.local", entry.Details);
    }

    [Fact]
    public async Task Record_writes_vietnamese_text_readably()
    {
        // System.Text.Json escapes everything outside ASCII by default, which would fill the
        // column with "Tần Chủ Toạ" — evidence nobody can read in a database
        // console.
        var service = new AuditService(_db, new FakeCallerContext());

        await service.RecordAsync(
            AuditActions.UserCreated, AuditActions.UserObjectType,
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            new { FullName = "Trần Chủ Toạ" });

        var details = (await _db.AuditLog.SingleAsync()).Details;

        Assert.Contains("Trần Chủ Toạ", details);
        Assert.DoesNotContain("\\u", details);
    }

    [Fact]
    public async Task Record_writes_an_empty_object_when_there_are_no_details()
    {
        // The column is jsonb and NOT NULL: "nothing to add" has to be valid JSON, not null.
        var service = new AuditService(_db, new FakeCallerContext());

        await service.RecordAsync(
            AuditActions.UserReactivated, AuditActions.UserObjectType,
            Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Equal("{}", (await _db.AuditLog.SingleAsync()).Details);
    }

    [Fact]
    public async Task Record_tolerates_having_no_request_in_flight()
    {
        // A background job or a seed run has no caller address; the row still has to be writable.
        var service = new AuditService(_db, new FakeCallerContext(string.Empty));

        await service.RecordAsync(
            AuditActions.UserUpdated, AuditActions.UserObjectType, Guid.CreateVersion7(), null);

        var entry = await _db.AuditLog.SingleAsync();

        Assert.Equal(string.Empty, entry.IpAddress);
        // Null actor is legitimate — AuditLogEntry allows it for an action with no signed-in user.
        Assert.Null(entry.ActorId);
    }
}
