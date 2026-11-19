using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Audit;

/// <summary>An account or a Guest invitation as an audit page names it.</summary>
public sealed record AuditLogPerson(string Name, string Email);

/// <summary>
/// The object of one audit row as the list names it (BR-442; PO 19/11/2026): the meeting's title, the document's file
/// name, the account's full name. <see cref="Secret"/> when the object is, or belongs to, a Mật meeting (Hải 20/11/2026):
/// then <see cref="Name"/> is null.
/// </summary>
public sealed record AuditLogObject(string? Name, bool Secret);

/// <summary>The names of one audit page: the actors (accounts and Guest invitations) and each row's object, by row id.</summary>
public sealed record AuditLogNames(
    IReadOnlyDictionary<Guid, AuditLogPerson> Users,
    IReadOnlyDictionary<Guid, AuditLogPerson> Invitations,
    IReadOnlyDictionary<Guid, AuditLogObject> Objects);

/// <summary>What the log never shows of a Mật meeting.</summary>
public static class AuditLogSecrecy
{
    /// <summary>
    /// Keys under which an entry may have recorded a Mật meeting's names; left out of what the log shows and of what
    /// the keyword search reads.
    /// </summary>
    public static readonly IReadOnlyList<string> NameKeys =
        ["name", "Name", "title", "Title", "fileName", "FileName", "description", "Description", "options", "Options"];
}

/// <summary>UC-13's reads of the audit log, untracked. Every criterion of the query must hold (BR-31).</summary>
public interface IAuditLogQuery
{
    /// <summary>How many rows meet the criteria.</summary>
    Task<int> CountAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>The rows meeting the criteria, newest first: <paramref name="skip"/> skipped, then <c>PageSize</c> taken.</summary>
    Task<List<AuditLogEntry>> FindPageAsync(AuditLogQuery query, int skip, CancellationToken ct = default);

    /// <summary>
    /// The accounts and invitations of these ids that exist, and the object of each of these rows named by the same
    /// expression the keyword search reads — in three reads.
    /// </summary>
    Task<AuditLogNames> FindNamesAsync(
        List<Guid> userIds, List<Guid> invitationIds, Guid[] entryIds, CancellationToken ct = default);
}
