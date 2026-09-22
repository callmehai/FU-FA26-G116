using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
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
    ILogger<RolePermissionService> logger) : IRolePermissionService
{
    public async Task<RolePermissionMatrix> GetMatrixAsync(CancellationToken ct = default)
        => Build(await roles.GetMatrixAsync(ct));

    public async Task<RolePermissionResult> UpdateAsync(
        SystemRole role,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct = default)
    {
        var matrix = await roles.GetMatrixAsync(ct);

        // The diagram's "Validate permission configuration". Every reason is collected before
        // answering, so the screen shows all of them at once rather than one per attempt.
        var errors = Validate(role, capabilities, matrix);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "Permission configuration for {Role} rejected: {Reasons}.",
                role, string.Join("; ", errors.Values));

            // MSG17 — the invalid branch.
            return RolePermissionResult.Invalid(errors);
        }

        // Duplicates would otherwise be written twice and break the composite primary key; the
        // set is also what the audit entry and the comparison below should speak in.
        var requested = capabilities.Distinct().OrderBy(c => c).ToArray();
        var before = matrix.TryGetValue(role, out var current)
            ? current.OrderBy(c => c).ToArray()
            : [];

        // Nothing to do. Answered as success rather than refused — the administrator asked for a
        // state the system is already in — but nothing is written: no audit row for a change that
        // did not happen, and no session ended for capabilities nobody lost.
        if (before.SequenceEqual(requested))
        {
            return RolePermissionResult.Success(Build(matrix));
        }

        await roles.ReplaceCapabilitiesAsync(role, requested, ct);

        try
        {
            await roles.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            // MSG18 — the diagram's "Permission configuration cannot be saved". Caught rather
            // than left to surface as a 500, because the diagram gives this its own branch and
            // because "retry" is the right advice for it, unlike for a validation failure.
            logger.LogError(
                exception, "Saving the permission configuration for {Role} failed.", role);
            return RolePermissionResult.Failure(RolePermissionOutcome.SaveFailed);
        }

        await audit.RecordAsync(
            AuditActions.RolePermissionsUpdated,
            AuditActions.RoleObjectType,
            // No object id: the row's identity is the role, which is an enum and not a Guid.
            // It travels in the details instead, where the before/after values already are.
            null,
            actorId,
            new
            {
                Role = role.ToString(),
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
        var affected = await roles.FindActiveUserIdsInRoleAsync(role, ct);

        foreach (var userId in affected)
        {
            await refreshTokens.RevokeAllForUserAsync(userId, ct);
        }

        logger.LogInformation(
            "Permissions for {Role} changed by {ActorId}; {Count} session(s) ended.",
            role, actorId, affected.Count);

        // Rebuilt from the requested values rather than re-queried: the save succeeded, so this is
        // what the table now holds, and a second round trip would only be able to disagree.
        var updated = new Dictionary<SystemRole, IReadOnlyCollection<Capability>>(matrix)
        {
            [role] = requested,
        };

        return RolePermissionResult.Success(Build(updated));
    }

    /// <summary>
    /// The rules UC-10 enforces. Returns field name → Vietnamese message, empty when the
    /// configuration is acceptable.
    /// </summary>
    private static Dictionary<string, string> Validate(
        SystemRole role,
        IReadOnlyCollection<Capability> capabilities,
        IReadOnlyDictionary<SystemRole, IReadOnlyCollection<Capability>> matrix)
    {
        var errors = new Dictionary<string, string>();

        // A value outside the enum arrives as a cast integer when a caller posts a number or a
        // name neither enum has. Stored unchecked it would become a grant nothing matches, which
        // is a silent way to hand out or withhold rights.
        if (!Enum.IsDefined(role))
        {
            errors["role"] = "Vai trò không hợp lệ.";
        }

        var unknown = capabilities.Where(c => !Enum.IsDefined(c)).ToArray();

        if (unknown.Length > 0)
        {
            errors["capabilities"] = "Danh sách quyền chứa giá trị không hợp lệ.";
        }

        // Nothing below can be judged without a valid role and a valid capability list.
        if (errors.Count > 0)
        {
            return errors;
        }

        // The lock-out rule, and the reason this screen is dangerous at all: AdminUsers is what
        // gates this very endpoint, so a matrix where no role carries it cannot be repaired
        // through the application — only through a database console.
        //
        // Checked across the whole matrix rather than as "Admin must keep AdminUsers", because
        // the point of this table is that the capability can be re-pointed to another role. Moving
        // it to Secretary is legitimate; leaving it nowhere is not.
        //
        // Not checked on the frontend alone: the acceptance criteria say "Kiểm ở server, không
        // phải ẩn nút".
        if (!capabilities.Contains(Capability.AdminUsers)
            && !matrix.Any(entry =>
                entry.Key != role && entry.Value.Contains(Capability.AdminUsers)))
        {
            errors["capabilities"] =
                "Không thể gỡ quyền quản trị người dùng khỏi vai trò cuối cùng còn giữ quyền này — "
                + "hệ thống sẽ không còn ai vào được màn hình phân quyền.";
        }

        return errors;
    }

    /// <summary>
    /// Turns the stored pairs into what UC-09 shows: every role listed, even one with nothing
    /// granted, alongside the catalogue that gives the grid its columns.
    /// </summary>
    private static RolePermissionMatrix Build(
        IReadOnlyDictionary<SystemRole, IReadOnlyCollection<Capability>> matrix)
        => new(
            PermissionCatalog.All,
            [.. Enum.GetValues<SystemRole>().Select(role => new RolePermissions(
                role,
                matrix.TryGetValue(role, out var capabilities)
                    ? [.. capabilities.OrderBy(c => c)]
                    : []))]);
}
