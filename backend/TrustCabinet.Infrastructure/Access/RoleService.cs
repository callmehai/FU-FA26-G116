using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Access;

/// <summary>
/// UC-11 … UC-13's <c>:Role Service</c> (SCRUM-63). Every rule about creating, renaming and
/// deleting a role lives here.
///
/// The order inside each write is the order the diagrams give: check, then persist, then audit.
/// Auditing before the write would record things that did not happen.
/// </summary>
public sealed partial class RoleService(
    IRoleRepository roles,
    IAuditService audit,
    IAdministratorInvariantGuard administratorInvariant,
    ILogger<RoleService> logger) : IRoleService
{
    /// <summary>
    /// Enforced here only: the column is citext, which ignores RoleConfiguration's HasMaxLength, and
    /// this service is the only writer of role names.
    /// </summary>
    private const int MaxNameLength = 64;

    private const int MaxDescriptionLength = 256;

    public Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default)
        => roles.ListAsync(ct);

    public async Task<RoleResult> CreateAsync(
        NewRole request, Guid actorId, CancellationToken ct = default)
    {
        var name = NormaliseName(request.Name);
        var description = NormaliseDescription(request.Description);

        // "Validate role information" — every field before answering, so the form shows all of
        // its errors at once.
        var errors = Validate(name, description);
        var capabilities = ParseCapabilities(request.Capabilities ?? [], errors);

        if (errors.Count > 0)
        {
            return RoleResult.Invalid(errors);
        }

        // Through the guard because this writes role_capabilities, and every write to that table
        // goes through it (ADR-0007). A new role nobody holds cannot break the "someone still
        // administers" rule, but "this one surely cannot" is the reasoning ADR-0007 had to undo
        // after review — the rule stays unconditional. It also makes the role and its capabilities
        // one transaction.
        return await administratorInvariant.WriteAsync(
            token => CreateWithinTransactionAsync(name, description, capabilities, actorId, token),
            ct);
    }

    private async Task<RoleResult> CreateWithinTransactionAsync(
        string name,
        string? description,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct)
    {
        // MSG21. Checked first so the common case answers cleanly; the unique index still decides
        // when two requests race past this together — see the catch below.
        if (await roles.NameExistsAsync(name, ct: ct))
        {
            return RoleResult.Failure(RoleOutcome.DuplicateName);
        }

        var role = new Role { Name = name, Description = description, IsBuiltIn = false };

        await roles.AddAsync(role, capabilities, ct);

        var failure = await SaveAsync("create", ct);

        if (failure is not null)
        {
            return RoleResult.Failure(failure.Value);
        }

        await audit.RecordAsync(
            AuditActions.RoleCreated,
            AuditActions.RoleObjectType,
            role.Id,
            actorId,
            new
            {
                role.Name,
                role.Description,
                Capabilities = capabilities.Select(c => c.ToString()).ToArray(),
            },
            ct);

        logger.LogInformation("Role {RoleId} created by {ActorId}.", role.Id, actorId);

        // Nobody holds a role that did not exist a moment ago.
        return RoleResult.Success(Summarise(role, userCount: 0));
    }

    public async Task<RoleResult> UpdateAsync(
        Guid id, RoleEdit request, Guid actorId, CancellationToken ct = default)
    {
        // Not through the guard: renaming touches neither capabilities nor who holds them, so it
        // cannot affect who can administer.
        var role = await roles.FindByIdAsync(id, ct);

        if (role is null)
        {
            return RoleResult.Failure(RoleOutcome.NotFound);
        }

        // MSG24 — before validation, as the diagram orders it: there is nothing to correct in the
        // form when the role cannot be renamed at all.
        if (role.IsBuiltInRole())
        {
            return RoleResult.Failure(RoleOutcome.BuiltInRole);
        }

        var name = NormaliseName(request.Name);
        var description = NormaliseDescription(request.Description);
        var errors = Validate(name, description);

        if (errors.Count > 0)
        {
            return RoleResult.Invalid(errors);
        }

        // "No changes submitted" — still MSG22, but nothing is written and nothing audited: the
        // SCRUM-60 convention that no change means no record. Ordinal, so changing only the case of
        // the name is a change.
        if (string.Equals(role.Name, name, StringComparison.Ordinal)
            && string.Equals(role.Description, description, StringComparison.Ordinal))
        {
            return RoleResult.Success(Summarise(role, await roles.CountUsersAsync(id, ct)));
        }

        // Excluding this role, so re-casing its own name is not a duplicate.
        if (await roles.NameExistsAsync(name, id, ct))
        {
            return RoleResult.Failure(RoleOutcome.DuplicateName);
        }

        var before = new { role.Name, role.Description };

        role.Name = name;
        role.Description = description;
        role.UpdatedAt = DateTimeOffset.UtcNow;

        var failure = await SaveAsync("update", ct);

        if (failure is not null)
        {
            return RoleResult.Failure(failure.Value);
        }

        await audit.RecordAsync(
            AuditActions.RoleUpdated,
            AuditActions.RoleObjectType,
            role.Id,
            actorId,
            new { Before = before, After = new { role.Name, role.Description } },
            ct);

        logger.LogInformation("Role {RoleId} updated by {ActorId}.", role.Id, actorId);

        // Renaming ends nobody's session: capabilities did not change, and /api/auth/me reads the
        // name from the database the next time the screen asks.
        return RoleResult.Success(Summarise(role, await roles.CountUsersAsync(id, ct)));
    }

    public Task<RoleResult> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
        // Through the guard: it deletes role_capabilities rows (by cascade), and the user count
        // below should be read under the same lock UserService takes to change someone's role.
        => administratorInvariant.WriteAsync(
            token => DeleteWithinTransactionAsync(id, actorId, token), ct);

    private async Task<RoleResult> DeleteWithinTransactionAsync(
        Guid id, Guid actorId, CancellationToken ct)
    {
        var role = await roles.FindByIdAsync(id, ct);

        if (role is null)
        {
            return RoleResult.Failure(RoleOutcome.NotFound);
        }

        if (role.IsBuiltInRole())
        {
            return RoleResult.Failure(RoleOutcome.BuiltInRole);
        }

        // MSG25. Deactivated accounts count: they still point at the role, and reactivating one
        // would otherwise bring back an account whose role no longer exists.
        var userCount = await roles.CountUsersAsync(id, ct);

        if (userCount > 0)
        {
            return RoleResult.InUse(userCount);
        }

        // Read before the delete: the cascade removes them, and the audit row is the only place
        // they survive.
        var capabilities = await roles.GetCapabilitiesAsync(id, ct);

        roles.Remove(role);

        try
        {
            await roles.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (HasSqlState(exception, PostgresErrorCodes.ForeignKeyViolation))
        {
            // "Role was assigned to a user account in the meantime": somebody was given the role
            // between the count above and this delete — account creation does not take the guard.
            // The RESTRICT foreign key refused it; answer MSG25 with the fresh count rather than
            // a 500. At least one, since the refusal proves someone holds it.
            var nowHeldBy = Math.Max(1, await roles.CountUsersAsync(id, ct));

            logger.LogInformation(
                "Deleting role {RoleId} refused by the foreign key: {Count} account(s) hold it.",
                id, nowHeldBy);

            return RoleResult.InUse(nowHeldBy);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            logger.LogError(exception, "Deleting role {RoleId} failed.", id);
            return RoleResult.Failure(RoleOutcome.SaveFailed);
        }

        await audit.RecordAsync(
            AuditActions.RoleDeleted,
            AuditActions.RoleObjectType,
            role.Id,
            actorId,
            new { role.Name, Capabilities = capabilities.Select(c => c.ToString()).ToArray() },
            ct);

        logger.LogInformation("Role {RoleId} deleted by {ActorId}.", role.Id, actorId);

        return RoleResult.Success();
    }

    /// <summary>
    /// Saves, turning a refusal into the branch it stands for: a unique violation is MSG21 (two
    /// requests raced past the name check), anything else from the database is the diagrams'
    /// "cannot be saved". Null when the save went through.
    /// </summary>
    private async Task<RoleOutcome?> SaveAsync(string operation, CancellationToken ct)
    {
        try
        {
            await roles.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception exception) when (HasSqlState(exception, PostgresErrorCodes.UniqueViolation))
        {
            logger.LogInformation("Role {Operation} rejected: name already taken (unique index).", operation);
            return RoleOutcome.DuplicateName;
        }
        catch (DbUpdateConcurrencyException)
        {
            // UPDATE matched no row: the role was deleted between reading it and saving the rename
            // (renaming does not take the guard). The diagram's "Role not found" — the client
            // refreshes its list — rather than "cannot be saved, retry", which could never succeed.
            logger.LogInformation("Role {Operation} rejected: the role no longer exists.", operation);
            return RoleOutcome.NotFound;
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            // NpgsqlException as well: a dropped connection or a timeout comes straight from the
            // driver, and those are exactly the failures this branch describes.
            logger.LogError(exception, "Role {Operation} failed to save.", operation);
            return RoleOutcome.SaveFailed;
        }
    }

    /// <summary>
    /// The rules UC-11 and UC-12 share. Returns field name → Vietnamese message, empty when
    /// acceptable. Takes the values already normalised.
    /// </summary>
    private static Dictionary<string, string> Validate(string name, string? description)
    {
        var errors = new Dictionary<string, string>();

        if (name.Length == 0)
        {
            errors["name"] = "Tên vai trò không được để trống.";
        }
        else if (name.Length > MaxNameLength)
        {
            errors["name"] = $"Tên vai trò không được vượt quá {MaxNameLength} ký tự.";
        }

        if (description?.Length > MaxDescriptionLength)
        {
            errors["description"] = $"Mô tả không được vượt quá {MaxDescriptionLength} ký tự.";
        }

        return errors;
    }

    /// <summary>
    /// The capability names the client sent, as enum values; an unknown one is reported by name
    /// in <paramref name="errors"/>, like MSG17 does.
    ///
    /// Matched against the enum's names, not with <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>:
    /// that also accepts "1" or "99" and turns them into a capability — or into a value no
    /// capability has, which would be stored as a grant nothing ever checks.
    /// </summary>
    private static Capability[] ParseCapabilities(
        IReadOnlyCollection<string?> requested, Dictionary<string, string> errors)
    {
        var known = Enum.GetValues<Capability>()
            .ToDictionary(c => c.ToString(), StringComparer.OrdinalIgnoreCase);

        var unknown = requested.Where(c => c is null || !known.ContainsKey(c)).ToArray();

        if (unknown.Length > 0)
        {
            errors["capabilities"] = $"Quyền không hợp lệ: {string.Join(", ", unknown.Select(c => c ?? "null"))}.";
            return [];
        }

        // Distinct: a repeated name would break role_capabilities' composite primary key.
        return [.. requested.Select(c => known[c!]).Distinct().Order()];
    }

    /// <summary>
    /// Trimmed, with runs of whitespace collapsed to one space, and in Unicode NFC — "Thư  ký   phó",
    /// or "Thư ký phó" typed as base letters plus combining marks (Unikey's "Unicode tổ hợp"), would
    /// otherwise be roles that look identical on screen yet pass citext's unique index. NFC also makes
    /// the 64-character limit count letters as the user sees them.
    /// </summary>
    private static string NormaliseName(string? name)
        => Whitespace().Replace((name ?? "").Normalize(NormalizationForm.FormC), " ").Trim();

    /// <summary>Trimmed; blank is stored as null rather than an empty string.</summary>
    private static string? NormaliseDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static RoleSummary Summarise(Role role, int userCount)
        => new(role.Id, role.Name, role.Description, role.IsBuiltIn, BuiltInRoles.IsDefault(role.Id), userCount);

    /// <summary>
    /// Whether the database refused the save with this SQLSTATE. Matched on the code rather than
    /// the message, which varies with the server's locale; EF wraps the provider exception, so the
    /// real one is the inner exception.
    /// </summary>
    private static bool HasSqlState(Exception exception, string sqlState)
        => exception is DbUpdateException { InnerException: PostgresException postgres }
            && postgres.SqlState == sqlState;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
