using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Audit;

/// <summary>
/// Writes audit rows, stamping each with the caller's IP address.
///
/// It saves on its own rather than sharing the caller's unit of work. That is deliberate and it
/// has a cost: an audit row can survive a business change that later fails to save. The other way
/// round — a user created with no record of who created them — is the one that destroys the
/// evidence trail, so the trade goes this way.
/// </summary>
public sealed class AuditService(TrustCabinetDbContext db, ICallerContext caller) : IAuditService
{
    /// <summary>
    /// Writes Vietnamese text as itself rather than as \u escapes.
    ///
    /// The default encoder is deliberately conservative — it escapes everything outside ASCII so
    /// that output is safe to drop into HTML. This JSON goes into a jsonb column that people read
    /// in a database console, where "Tên Cũ" is evidence nobody can read at a glance.
    /// Safe here because the value is never interpolated into a page: the audit screen will fetch
    /// it as JSON, and the browser escapes on output.
    /// </summary>
    private static readonly JsonSerializerOptions DetailsOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public async Task RecordAsync(
        string action,
        string objectType,
        Guid? objectId,
        Guid? actorId,
        object? details = null,
        CancellationToken ct = default)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            ActorId = actorId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            IpAddress = caller.IpAddress,
            // The column is jsonb and NOT NULL, so "nothing to add" is an empty object, not null.
            Details = details is null ? "{}" : JsonSerializer.Serialize(details, DetailsOptions),
        });

        await db.SaveChangesAsync(ct);
    }
}
