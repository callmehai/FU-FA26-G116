using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Infrastructure.Users;

/// <summary>
/// UC-04 … UC-07's <c>:User Service</c>. Every rule about user accounts lives here: validation,
/// email availability, whether a status change is allowed, and what the audit log records.
///
/// The order inside each write is the order the diagrams give: check, then persist, then audit.
/// Auditing before the write would record things that did not happen.
/// </summary>
public sealed partial class UserService(
    IUserAccountRepository users,
    IPasswordHasher passwordHasher,
    IAuditService audit,
    IRefreshTokenStore refreshTokens,
    ILogger<UserService> logger) : IUserService
{
    /// <summary>
    /// Minimum password length for an account an administrator creates. Long rather than
    /// complicated: length is what makes a password hard to guess, while a character-class rule
    /// mostly produces "Password@1".
    /// </summary>
    private const int MinimumPasswordLength = 12;

    public async Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken ct = default)
        => [.. (await users.ListAsync(ct)).Select(UserAccount.From)];

    public async Task<UserResult> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);

        return user is null
            ? UserResult.Failure(UserOutcome.NotFound)
            : UserResult.Success(UserAccount.From(user));
    }

    public async Task<UserResult> CreateAsync(
        NewUser request, Guid actorId, CancellationToken ct = default)
    {
        var fullName = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";
        var password = request.Password ?? "";

        // Branch 1 — invalid information. Every field is checked before answering, so the form
        // shows all of its errors at once instead of one per round trip.
        var errors = Validate(fullName, email, request.Role);

        if (password.Length < MinimumPasswordLength)
        {
            errors["password"] = $"Mật khẩu phải có ít nhất {MinimumPasswordLength} ký tự.";
        }

        if (errors.Count > 0)
        {
            return UserResult.Invalid(errors);
        }

        // Branch 2 — MSG08. The unique index is what actually guarantees uniqueness; this check
        // exists so the common case answers MSG08 instead of a 500. Two simultaneous creates with
        // the same email still race past it, and the index catches that one — see the catch below.
        if (await users.EmailExistsAsync(email, ct: ct))
        {
            return UserResult.Failure(UserOutcome.DuplicateEmail);
        }

        // Branch 3 — valid. Created active: an administrator filling this form wants a working
        // account, and UC-07 is where deactivating happens.
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            Role = request.Role,
            IsActive = true,
        };

        await users.AddAsync(user, ct);

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsDuplicateEmail(ex))
        {
            // The race the check above cannot close: both requests saw the email free, one index
            // violation follows. MSG08 is the honest answer — the account was not created because
            // that email is taken.
            logger.LogInformation("Create rejected: email already taken (unique index).");
            return UserResult.Failure(UserOutcome.DuplicateEmail);
        }

        await audit.RecordAsync(
            AuditActions.UserCreated,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            // The password is not in here, hashed or otherwise. The audit log is read by more
            // people than the users table and is never deleted.
            new { user.Email, user.FullName, Role = user.Role.ToString() },
            ct);

        logger.LogInformation("Account {UserId} created by {ActorId}.", user.Id, actorId);

        return UserResult.Success(UserAccount.From(user));
    }

    public async Task<UserResult> UpdateAsync(
        Guid id, UserEdit request, Guid actorId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);

        if (user is null)
        {
            return UserResult.Failure(UserOutcome.NotFound);
        }

        var fullName = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";

        var errors = Validate(fullName, email, request.Role);

        if (errors.Count > 0)
        {
            return UserResult.Invalid(errors);
        }

        // Excluding this account: keeping your own email is not a duplicate. UC-06's diagram has
        // no duplicate branch, but the unique index does not care what the diagram says — without
        // this, editing someone into an existing email would be a 500 rather than MSG08.
        if (await users.EmailExistsAsync(email, id, ct))
        {
            return UserResult.Failure(UserOutcome.DuplicateEmail);
        }

        // Removing your own ability to administer is the same lockout as deactivating yourself,
        // so it is refused for the same reason (see ChangeStatusAsync). Checked before anything
        // is assigned, so a refused update leaves the tracked entity untouched.
        if (id == actorId && user.Role == SystemRole.Admin && request.Role != SystemRole.Admin)
        {
            logger.LogInformation(
                "Update rejected: administrator {ActorId} tried to drop their own Admin role.",
                actorId);
            return UserResult.Failure(UserOutcome.StatusChangeNotAllowed);
        }

        // Kept for the audit entry: "what did it use to be" is most of the value of the record.
        var before = new { user.Email, user.FullName, Role = user.Role.ToString() };

        user.FullName = fullName;
        user.Email = email;
        user.Role = request.Role;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsDuplicateEmail(ex))
        {
            // The entity keeps the rejected values in memory. Harmless as things stand — the
            // DbContext is per-request and this request is over — but anything that saved again
            // on the same context would write them. Nothing does; keep it that way.
            logger.LogInformation("Update rejected: email already taken (unique index).");
            return UserResult.Failure(UserOutcome.DuplicateEmail);
        }

        await audit.RecordAsync(
            AuditActions.UserUpdated,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            new { Before = before, After = new { user.Email, user.FullName, Role = user.Role.ToString() } },
            ct);

        // A role change alters what the account may do, and capabilities travel inside the access
        // token — so until it expires the person keeps the rights they had. Dropping their refresh
        // tokens ends the session instead of waiting for it to lapse.
        if (before.Role != user.Role.ToString())
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        logger.LogInformation("Account {UserId} updated by {ActorId}.", user.Id, actorId);

        return UserResult.Success(UserAccount.From(user));
    }

    public async Task<UserResult> ChangeStatusAsync(
        Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);

        if (user is null)
        {
            return UserResult.Failure(UserOutcome.NotFound);
        }

        // Branch 1 — status change not allowed. Two reasons, both answered the same way.
        //
        // Deactivating yourself: with one organisation there may be no second administrator to
        // undo it, so the system would need a database console to recover from a misclick.
        if (id == actorId)
        {
            logger.LogInformation(
                "Status change rejected: administrator {ActorId} aimed at their own account.",
                actorId);
            return UserResult.Failure(UserOutcome.StatusChangeNotAllowed);
        }

        // Already in that state: two administrators on the same list, or a double-submitted form.
        // Refused rather than treated as success, because "deactivated" would then be written to
        // the audit log twice for one deactivation, and the evidence should say what happened.
        if (user.IsActive == isActive)
        {
            return UserResult.Failure(UserOutcome.StatusChangeNotAllowed);
        }

        // Branch 2 — allowed. Deactivated, never deleted: the person still chairs last quarter's
        // meeting and their approvals stay attributable (SCRUM-13, ERD).
        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await users.SaveChangesAsync(ct);

        // The <c>:Session Service</c> step of UC-07, on the deactivate branch only. A row marked
        // inactive while its tokens still work is not deactivated in any sense that matters: the
        // access token stays valid until it expires, and dropping the refresh tokens is what stops
        // it being renewed. AuthenticationService.RefreshAsync re-reads IsActive, so the next
        // refresh would refuse anyway — this makes it immediate rather than eventual.
        if (!isActive)
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        await audit.RecordAsync(
            isActive ? AuditActions.UserReactivated : AuditActions.UserDeactivated,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            new { user.Email, IsActive = isActive },
            ct);

        logger.LogInformation(
            "Account {UserId} {Change} by {ActorId}.",
            user.Id, isActive ? "reactivated" : "deactivated", actorId);

        return UserResult.Success(UserAccount.From(user));
    }

    /// <summary>
    /// The fields UC-05 and UC-06 share. Returns field name → Vietnamese message, empty when
    /// everything is acceptable.
    /// </summary>
    private static Dictionary<string, string> Validate(string fullName, string email, SystemRole role)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["fullName"] = "Họ tên không được để trống.";
        }
        else if (fullName.Length > 200)
        {
            // Matches UserConfiguration's HasMaxLength(200). Checked here so an over-long name is
            // a validation message rather than a truncation or a database error.
            errors["fullName"] = "Họ tên không được vượt quá 200 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = "Email không được để trống.";
        }
        else if (email.Length > 256 || !EmailPattern().IsMatch(email))
        {
            errors["email"] = "Email không hợp lệ.";
        }

        // A value outside the enum arrives as a cast integer when a caller posts a number the
        // enum has no name for. Without this it would be stored and read back as a role nothing
        // matches, which is a silent way to end up with an account that can do nothing.
        if (!Enum.IsDefined(role))
        {
            errors["role"] = "Vai trò không hợp lệ.";
        }

        return errors;
    }

    /// <summary>
    /// Deliberately loose: something, an @, something with a dot, no spaces. Whether an address
    /// can actually receive mail is answered by sending to it, not by a regular expression — the
    /// strict ones reject valid addresses and still accept unreachable ones.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    /// <summary>
    /// Whether this save failed because a unique index rejected the row.
    ///
    /// Matched on PostgreSQL's SQLSTATE 23505 rather than on a message string, which varies with
    /// the server's locale. EF wraps the provider exception in a DbUpdateException, so the real
    /// one is the inner exception.
    ///
    /// <c>users</c> has exactly two unique indexes — email, and the filtered one on
    /// <c>google_subject_id</c> — and nothing in this service writes the second. Should a third
    /// ever appear, narrow this to the constraint name.
    /// </summary>
    private static bool IsDuplicateEmail(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } };
}
