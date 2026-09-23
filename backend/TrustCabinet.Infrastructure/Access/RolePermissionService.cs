using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Access;

/// <summary>
/// UC-09 and UC-10's <c>:Role Permission Service</c> (SCRUM-60). Every rule about the matrix lives
/// here: what a valid configuration is, and what the audit log records.
///
/// The order inside the write is the order UC-10's diagram gives: validate, then persist, then
/// audit. Auditing before the write would record changes that did not happen.
/// </summary>
public sealed class RolePermissionService(
    IRolePermissionRepository roles,
    IAuditService audit,
    IRefreshTokenStore refreshTokens,
    IAdministratorInvariantGuard administratorInvariant,
    ILogger<RolePermissionService> logger) : IRolePermissionService
{
    public async Task<RolePermissionMatrix> GetMatrixAsync(CancellationToken ct = default)
        => Build(await roles.ListRolesAsync(ct), await roles.GetMatrixAsync(ct));

    public async Task<RolePermissionResult> UpdateAsync(
        Guid roleId,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct = default)
    {
        // Read, validate and write under one lock. The rule below spans rows — "somebody can
        // still administer" is a property of the whole matrix — so validating against a snapshot
        // another request may already be replacing is how two legal-looking updates combine into
        // a system nobody can administer.
        //
        // The shared guard, not a lock on role_capabilities alone. The rule also depends on
        // `users`: deactivating the last administrator removes a holder of AdminUsers without
        // touching this table at all, so a lock taken here would never have stopped it. Both
        // sides queue behind one name instead — see IAdministratorInvariantGuard.
        var written = await administratorInvariant.WriteAsync(
            token => UpdateWithinTransactionAsync(roleId, capabilities, actorId, token), ct);

        // After the commit, never inside it. Revoking runs one Redis round trip per affected
        // account, in sequence; doing that under the lock would block every other administrative
        // write for the whole run, and a Redis outage would roll back a configuration change the
        // database had already accepted.
        //
        // The cost is a window, not a hole: until it runs, those accounts still hold a usable
        // refresh token. It is bounded by the access token's 15 minutes, the same exposure
        // accepted elsewhere for the same reason.
        foreach (var userId in written.RevokeSessionsOf)
        {
            try
            {
                await refreshTokens.RevokeAllForUserAsync(userId, ct);
            }
            catch (Exception exception)
            {
                // Logged, not rethrown, and the loop goes on: the matrix is already saved, and
                // telling the administrator it failed would have them repeat a change that did
                // happen. One account's session lapsing on its own is the smaller loss.
                logger.LogError(
                    exception,
                    "Permissions for role {RoleId} changed, but ending the sessions of {UserId} "
                    + "failed. The session lapses on its own when the access token expires.",
                    roleId, userId);
            }
        }

        return written.Result;
    }

    /// <summary>
    /// What a guarded update produced: the answer for the caller, and whose sessions the change
    /// invalidated — empty on every branch that wrote nothing.
    /// </summary>
    private readonly record struct Written(
        RolePermissionResult Result, IReadOnlyCollection<Guid> RevokeSessionsOf)
    {
        /// <summary>
        /// Lets a refusal be returned as-is: a branch that wrote nothing has no session to end.
        /// </summary>
        public static implicit operator Written(RolePermissionResult result) => new(result, []);
    }

    private async Task<Written> UpdateWithinTransactionAsync(
        Guid roleId,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct)
    {
        var roleList = await roles.ListRolesAsync(ct);
        var matrix = await roles.GetMatrixAsync(ct);
        var role = roleList.FirstOrDefault(r => r.Id == roleId);

        // The diagram's "Validate permission configuration". Every reason is collected before
        // answering, so the screen shows all of them at once rather than one per attempt.
        var errors = await ValidateAsync(role, capabilities, matrix, ct);

        if (role is null || errors.Count > 0)
        {
            logger.LogWarning(
                "Permission configuration for role {RoleId} rejected: {Reasons}.",
                roleId, string.Join("; ", errors.Values));

            // MSG17 — the invalid branch.
            return RolePermissionResult.Invalid(errors);
        }

        // Duplicates would otherwise be written twice and break the composite primary key; the
        // set is also what the audit entry and the comparison below should speak in.
        var requested = capabilities.Distinct().OrderBy(c => c).ToArray();
        var before = matrix.TryGetValue(roleId, out var current)
            ? current.OrderBy(c => c).ToArray()
            : [];

        // Nothing to do. Answered as success rather than refused — the administrator asked for a
        // state the system is already in — but nothing is written: no audit row for a change that
        // did not happen, and no session ended for capabilities nobody lost.
        if (before.SequenceEqual(requested))
        {
            return RolePermissionResult.Success(Build(roleList, matrix));
        }

        await roles.ReplaceCapabilitiesAsync(roleId, requested, ct);

        try
        {
            await roles.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            // MSG18 — the diagram's "Permission configuration cannot be saved". Caught rather
            // than left to surface as a bare 500, because the diagram gives this its own branch
            // and because "retry" is the right advice for it, unlike for a validation failure.
            //
            // NpgsqlException as well as DbUpdateException: EF wraps a constraint violation, but
            // a dropped connection or a lock timeout comes straight from the driver, and those
            // are exactly the transient failures the branch is meant to describe.
            logger.LogError(
                exception, "Saving the permission configuration for role {RoleId} failed.", roleId);
            return RolePermissionResult.Failure(RolePermissionOutcome.SaveFailed);
        }

        await audit.RecordAsync(
            AuditActions.RolePermissionsUpdated,
            AuditActions.RoleObjectType,
            roleId,
            actorId,
            new
            {
                // The name as it was at the time: a role can be renamed later, and the evidence
                // should read the way the administrator saw it.
                role.Name,
                Before = before.Select(c => c.ToString()).ToArray(),
                After = requested.Select(c => c.ToString()).ToArray(),
            },
            ct);

        // SCRUM-60: "thay đổi có hiệu lực ngay với phiên đang đăng nhập, hoặc nói rõ là phải đăng
        // nhập lại". Capabilities are copied into the access token when it is issued, so everyone
        // already holding this role keeps the old set until their token expires. Dropping their
        // refresh tokens ends the session at the next refresh instead of waiting it out — the same
        // step UserService takes on a role change, for the same reason.
        //
        // Both directions, not only on a capability removed: someone whose role just gained a
        // capability is holding a token that does not carry it, so their new rights would not
        // work either until the token lapsed.
        //
        // Collected here, revoked by the caller once the transaction has committed — see
        // UpdateAsync for why Redis must not be reached while the lock is held.
        var affected = await roles.FindActiveUserIdsInRoleAsync(roleId, ct);

        logger.LogInformation(
            "Permissions for role {RoleId} changed by {ActorId}; {Count} session(s) to end.",
            roleId, actorId, affected.Count);

        // Rebuilt from the requested values rather than re-queried: the save succeeded, so this is
        // what the table now holds, and a second round trip would only be able to disagree.
        var updated = new Dictionary<Guid, IReadOnlyCollection<Capability>>(matrix)
        {
            [roleId] = requested,
        };

        return new Written(RolePermissionResult.Success(Build(roleList, updated)), affected);
    }

    /// <summary>
    /// The rules UC-10 enforces. Returns field name → Vietnamese message, empty when the
    /// configuration is acceptable.
    /// </summary>
    private async Task<Dictionary<string, string>> ValidateAsync(
        Role? role,
        IReadOnlyCollection<Capability> capabilities,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>> matrix,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string>();

        // An id no role has: a stale screen after another administrator deleted the role, or a
        // made-up value. Written unchecked, the foreign key would refuse it as a 500.
        if (role is null)
        {
            errors["roleId"] = "Vai trò không hợp lệ.";
        }

        // A value outside the enum arrives as a cast integer when a caller posts a number or a
        // name the enum does not have. Stored unchecked it would become a grant nothing matches,
        // which is a silent way to hand out or withhold rights.
        var unknown = capabilities.Where(c => !Enum.IsDefined(c)).ToArray();

        if (unknown.Length > 0)
        {
            errors["capabilities"] = "Danh sách quyền chứa giá trị không hợp lệ.";
        }

        // Nothing below can be judged without a valid role and a valid capability list.
        if (role is null || errors.Count > 0)
        {
            return errors;
        }

        // The lock-out rule, and the reason this screen is dangerous at all: AdminUsers is what
        // gates this very endpoint, so a system where nobody holds it cannot be repaired through
        // the application — only through a database console.
        //
        // Checked across the whole matrix rather than as "Admin must keep AdminUsers", because
        // the point of this table is that the capability can be re-pointed to another role. Moving
        // it to Secretary is legitimate; leaving it unreachable is not.
        //
        // Counted in **active accounts**, not roles. "Some role still holds it" is not the
        // invariant: granting AdminUsers to a role nobody has, then removing it from Admin, would
        // satisfy a role-only check while leaving no person able to administer — two ordinary
        // requests, no race needed. UserService.WouldRemoveTheLastAdministratorAsync guards the
        // same invariant from the user-management side and counts accounts too; the two have to
        // agree, and — since agreeing is not enough if they can run at the same time — they take
        // the same lock, IAdministratorInvariantGuard.
        //
        // Not checked on the frontend alone: the acceptance criteria say "Kiểm ở server, không
        // phải ẩn nút".
        var rolesAfterwards = matrix
            .Where(entry => entry.Key != role.Id && entry.Value.Contains(Capability.AdminUsers))
            .Select(entry => entry.Key)
            .ToList();

        if (capabilities.Contains(Capability.AdminUsers))
        {
            rolesAfterwards.Add(role.Id);
        }

        if (await roles.CountActiveUsersInRolesAsync(rolesAfterwards, ct) == 0)
        {
            errors["adminUsers"] =
                "Không thể lưu cấu hình này: sau khi lưu sẽ không còn tài khoản đang hoạt động nào "
                + "giữ quyền quản trị người dùng, nên không ai vào được màn hình phân quyền nữa.";
        }

        return errors;
    }

    /// <summary>
    /// Turns the stored pairs into what UC-09 shows: every role listed, even one with nothing
    /// granted, alongside the catalogue that gives the grid its columns.
    /// </summary>
    private static RolePermissionMatrix Build(
        IReadOnlyCollection<Role> roleList,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>> matrix)
        => new(
            PermissionCatalog.All,
            [.. roleList.Select(role => new RolePermissions(
                role.Id,
                role.Name,
                role.Description,
                role.IsBuiltIn,
                matrix.TryGetValue(role.Id, out var capabilities)
                    ? [.. capabilities.OrderBy(c => c)]
                    : []))]);
}
