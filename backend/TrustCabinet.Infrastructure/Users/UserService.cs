using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Infrastructure.Users;

/// <summary>
/// UC-04 … UC-08's <c>:User Service</c>. Every rule about user accounts lives here: validation,
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
    IAdministratorInvariantGuard administratorInvariant,
    IPasswordResetTokenStore passwordResetTokens,
    IPasswordResetEmailSender passwordResetEmail,
    ILogger<UserService> logger) : IUserService
{
    /// <summary>
    /// Minimum password length for an account an administrator creates. Long rather than
    /// complicated: length is what makes a password hard to guess, while a character-class rule
    /// mostly produces "Password@1".
    /// </summary>
    private const int MinimumPasswordLength = 12;

    /// <summary>
    /// How long a UC-08 reset link works. Long enough to find the email and act on it, short
    /// enough that a link left in an inbox is not a standing way into the account.
    /// </summary>
    internal static readonly TimeSpan PasswordResetLinkLifetime = TimeSpan.FromMinutes(60);

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
        var errors = Validate(fullName, email);
        var role = await FindRoleAsync(request.RoleId, errors, ct);

        if (password.Length < MinimumPasswordLength)
        {
            errors["password"] = $"Mật khẩu phải có ít nhất {MinimumPasswordLength} ký tự.";
        }

        if (role is null || errors.Count > 0)
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
            RoleId = role.Id,
            Role = role,
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
        catch (Exception ex) when (IsForeignKeyViolation(ex))
        {
            // The role was deleted between FindRoleAsync above and this insert — creating an
            // account does not take the guard. role_id is the only foreign key this insert sets, so
            // the answer is the one FindRoleAsync gives for a role that is gone.
            logger.LogInformation("Create rejected: the chosen role was deleted meanwhile.");
            return UserResult.Invalid(new Dictionary<string, string> { ["roleId"] = RoleNotFound });
        }

        await audit.RecordAsync(
            AuditActions.UserCreated,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            // The password is not in here, hashed or otherwise. The audit log is read by more
            // people than the users table and is never deleted.
            new { user.Email, user.FullName, user.RoleId, Role = role.Name },
            ct);

        logger.LogInformation("Account {UserId} created by {ActorId}.", user.Id, actorId);

        return UserResult.Success(UserAccount.From(user));
    }

    public Task<UserResult> UpdateAsync(
        Guid id, UserEdit request, Guid actorId, CancellationToken ct = default)
        // Under the shared lock: a role change can take the last AdminUsers away, and that rule is
        // decided by users and role_capabilities together. Checking it outside the lock would let
        // this and a matrix edit each see an administrator the other is in the middle of removing.
        // See IAdministratorInvariantGuard.
        => GuardedWriteAsync(
            token => UpdateWithinTransactionAsync(id, request, actorId, token), ct);

    private async Task<Written> UpdateWithinTransactionAsync(
        Guid id, UserEdit request, Guid actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id, ct);

        if (user is null)
        {
            return UserResult.Failure(UserOutcome.NotFound);
        }

        var fullName = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";

        var errors = Validate(fullName, email);
        var role = await FindRoleAsync(request.RoleId, errors, ct);

        if (role is null || errors.Count > 0)
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

        // Refuse a role change that would leave nobody able to administer. Checked before
        // anything is assigned, so a refused update leaves the tracked entity untouched.
        //
        // Not "is this me?": comparing id == actorId only stops someone demoting themselves, and
        // two administrators can still demote each other into a system with no administrator at
        // all. What has to hold is the invariant — at least one active account keeps AdminUsers.
        //
        // Only when the role actually moves: editing a name or an email takes no capability away,
        // so the sole administrator must still be able to correct their own profile.
        if (role.Id != user.RoleId
            && await WouldRemoveTheLastAdministratorAsync(user, role.Id, ct))
        {
            logger.LogWarning(
                "Update rejected: changing the role of {UserId} would leave no active "
                + "administrator.", user.Id);
            return UserResult.Failure(UserOutcome.StatusChangeNotAllowed);
        }

        // Kept for the audit entry: "what did it use to be" is most of the value of the record.
        // The role's name as well as its id: the evidence should read the way the administrator
        // saw it, and a role can be renamed or deleted later.
        var before = new { user.Email, user.FullName, user.RoleId, Role = user.Role?.Name };

        user.FullName = fullName;
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsDuplicateEmail(ex))
        {
            // The entity is still tracked and still holds the values the database refused. The
            // audit write below runs on the same DbContext, so its SaveChanges would flush them —
            // discarding here closes that rather than trusting nobody ever saves again.
            await users.DiscardChangesAsync(user, ct);
            logger.LogInformation("Update rejected: email already taken (unique index).");
            return UserResult.Failure(UserOutcome.DuplicateEmail);
        }

        await audit.RecordAsync(
            AuditActions.UserUpdated,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            new { Before = before, After = new { user.Email, user.FullName, user.RoleId, Role = role.Name } },
            ct);

        logger.LogInformation("Account {UserId} updated by {ActorId}.", user.Id, actorId);

        // The <c>:Token Service</c> step again, which the UC-06 diagram does not draw. A role
        // change alters what the account may do, and capabilities travel inside the access token —
        // so until it expires the person keeps the rights they had. Dropping their refresh tokens
        // ends the session instead of waiting for it to lapse.
        //
        // Handed back rather than done here: Redis is a different system, and reaching it while
        // holding the invariant lock would block every other administrative write for the length
        // of a network round trip — and make a Redis outage roll back a legitimate edit. See
        // GuardedWriteAsync.
        //
        // An email change is revoked too, not only a role change: the access token carries the
        // email claim, so /api/auth/me would keep answering with the old address until it expired.
        var changed = before.RoleId != user.RoleId
            || !string.Equals(before.Email, user.Email, StringComparison.Ordinal);

        return new Written(
            UserResult.Success(UserAccount.From(user)),
            changed ? user.Id : null);
    }

    public Task<UserResult> ChangeStatusAsync(
        Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
        // Under the shared lock, for the same reason as UpdateAsync: deactivating the last active
        // administrator is the other half of the invariant, and the matrix screen can remove the
        // capability from underneath this check. See IAdministratorInvariantGuard.
        => GuardedWriteAsync(
            token => ChangeStatusWithinTransactionAsync(id, isActive, actorId, token), ct);

    private async Task<Written> ChangeStatusWithinTransactionAsync(
        Guid id, bool isActive, Guid actorId, CancellationToken ct)
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

        // Deactivating the last administrator. The self-check above is not enough on its own:
        // two administrators can deactivate each other in turn and leave nobody able to
        // administer — the access token of the one deactivated first still carries AdminUsers
        // until it expires, so their second request is authorized. The invariant is what has to
        // be enforced, not "am I aiming at myself?".
        if (!isActive && await WouldRemoveTheLastAdministratorAsync(user, user.RoleId, ct))
        {
            logger.LogWarning(
                "Status change rejected: deactivating {UserId} would leave no active "
                + "administrator.", user.Id);
            return UserResult.Failure(UserOutcome.StatusChangeNotAllowed);
        }

        // Branch 2 — allowed. Deactivated, never deleted: the person still chairs last quarter's
        // meeting and their approvals stay attributable (SCRUM-13, ERD).
        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await users.SaveChangesAsync(ct);

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

        // The <c>:Token Service</c> step of UC-07, on the deactivate branch only. A row marked
        // inactive while its tokens still work is not deactivated in any sense that matters: the
        // access token stays valid until it expires, and dropping the refresh tokens is what stops
        // it being renewed. AuthenticationService.RefreshAsync re-reads IsActive, so the next
        // refresh would refuse anyway — this closes the session at the refresh boundary instead of
        // waiting for it to lapse. The access token itself is still valid until it expires, which
        // is why /api/users revalidates the caller on every request.
        //
        // After the commit, not inside it — see GuardedWriteAsync.
        return new Written(
            UserResult.Success(UserAccount.From(user)),
            isActive ? null : user.Id);
    }

    public async Task<UserResult> RequestPasswordResetAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);

        if (user is null)
        {
            return UserResult.Failure(UserOutcome.NotFound);
        }

        // EX-1 — MSG14. A deactivated account cannot sign in, so a password for it is of no use,
        // and mailing a working credential for an account the organisation has closed is the
        // opposite of what deactivating it meant (PRE-3: "exists and is active").
        if (!user.IsActive)
        {
            logger.LogInformation(
                "Password reset rejected: account {UserId} is deactivated.", user.Id);
            return UserResult.Failure(UserOutcome.PasswordResetNotAvailable);
        }

        // The diagram's ":Authentication Service — Create password reset request". The current
        // password is left alone: it keeps working until the person uses the link, so pressing
        // this button can never lock anyone out.
        var token = await passwordResetTokens.CreateAsync(user.Id, PasswordResetLinkLifetime, ct);

        // EX-2 — MSG15. Not audited: the evidence should say what happened, and nothing reached
        // the person. The token just created was never activated, so it does not work and expires
        // on its own — and a link sent earlier, already in the person's inbox, keeps working.
        // The address the link goes to, kept apart from the entity, which is what gets compared
        // after the send below.
        var sentTo = user.Email;

        if (!await passwordResetEmail.SendAsync(
                sentTo, user.FullName, token, PasswordResetLinkLifetime, ct))
        {
            logger.LogWarning(
                "Password reset for account {UserId} could not be emailed.", user.Id);
            return UserResult.Failure(UserOutcome.PasswordResetEmailFailed);
        }

        // Only now does the new link work and the old one stop. CancellationToken.None: the email
        // is out, and an administrator closing the tab must not leave it carrying a dead link.
        await passwordResetTokens.ActivateAsync(
            user.Id, token, PasswordResetLinkLifetime, CancellationToken.None);

        // The send can take up to Email:SendAttemptTimeoutSeconds. If another administrator
        // deactivated the account or changed its email meanwhile, their revoke (GuardedWriteAsync)
        // ran before the activation above and missed this link — which went to the old address.
        // So read the account again, fresh from the database, and take the link back ourselves.
        // A change committed after this read revokes after the activation, so it is caught by
        // their own revoke: between the two, no ordering leaves the link alive.
        //
        // Still MSG13: the email did go out, exactly as if the other change had come a moment
        // after it — which the rules already answer by revoking the link. That change has its own
        // audit entry.
        var currentEmail = await users.FindActiveEmailAsync(user.Id, CancellationToken.None);

        // Case-insensitive like the citext column: a change of case is the same mailbox.
        if (!string.Equals(currentEmail, sentTo, StringComparison.OrdinalIgnoreCase))
        {
            await passwordResetTokens.RevokeAsync(user.Id, CancellationToken.None);
            logger.LogInformation(
                "Password reset link for account {UserId} revoked: the account was deactivated or "
                + "its email changed while the email was being sent.",
                user.Id);
        }

        await audit.RecordAsync(
            AuditActions.UserPasswordResetRequested,
            AuditActions.UserObjectType,
            user.Id,
            actorId,
            // Where the link went. Never the token: the audit log outlives the link and is read by
            // more people than the mailbox.
            new { Email = sentTo },
            // Not the request's token: the email is out, and an administrator closing the tab a
            // moment later must not leave a live link in someone's inbox with no record of it.
            CancellationToken.None);

        logger.LogInformation(
            "Password reset for account {UserId} requested by {ActorId}.", user.Id, actorId);

        return UserResult.Success(UserAccount.From(user));
    }

    public async Task<UserResult> ResetPasswordAsync(
        string? token, string? newPassword, CancellationToken ct = default)
    {
        // The link first, the password second: a dead link should say so straight away rather than
        // after the person has typed a valid password into a form that can never succeed.
        if (string.IsNullOrWhiteSpace(token)
            || await passwordResetTokens.PeekAsync(token, ct) is not { } ownerId)
        {
            return UserResult.Failure(UserOutcome.PasswordResetLinkInvalid);
        }

        // Peeked, not consumed: a password that breaks the rule must leave the link usable for the
        // corrected attempt.
        var password = newPassword ?? "";

        if (password.Length < MinimumPasswordLength)
        {
            return UserResult.Invalid(new Dictionary<string, string>
            {
                ["newPassword"] = $"Mật khẩu phải có ít nhất {MinimumPasswordLength} ký tự.",
            });
        }

        // Spent before anything is written. Of two simultaneous submissions only one gets the
        // account back; the other is a dead link like any other.
        if (await passwordResetTokens.ConsumeAsync(token, ct) != ownerId)
        {
            return UserResult.Failure(UserOutcome.PasswordResetLinkInvalid);
        }

        var user = await users.FindByIdAsync(ownerId, ct);

        // Deactivated after the link was sent: the link dies with the account. Same answer as any
        // dead link — this endpoint is anonymous, and it should not tell a holder of an old link
        // what became of the account.
        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Password reset refused: account {UserId} is gone or deactivated.", ownerId);
            return UserResult.Failure(UserOutcome.PasswordResetLinkInvalid);
        }

        // Hashed before the sessions are touched: the hasher is deliberately slow, and every
        // millisecond between the revoke below and the save is a window in which the old password
        // still signs in.
        var newHash = passwordHasher.Hash(password);

        // Signed out everywhere — twice. A reset is what someone does when the old password may be
        // known to someone else, so a session opened with it must not outlive it, and a refresh
        // only re-checks is_active, so a surviving session would run for the refresh token's full
        // seven days.
        //
        // Before the save, so a Redis failure leaves the old password and the old sessions
        // together, as they were — not a new password next to the sessions it was meant to end,
        // with the link already spent.
        await refreshTokens.RevokeAllForUserAsync(user.Id, ct);

        user.PasswordHash = newHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await users.SaveChangesAsync(ct);

        // And after it, for a sign-in with the old password — or a refresh already past its
        // consume — that landed between the first revoke and the save. From here on the old
        // password no longer signs in, so nothing new can slip through.
        await RevokeSessionsAfterResetAsync(user.Id);

        // CancellationToken.None from the save on: the password has changed, and a client that
        // hung up a moment ago must not leave that change without its evidence.
        await audit.RecordAsync(
            AuditActions.UserPasswordReset,
            AuditActions.UserObjectType,
            user.Id,
            // The account itself: the administrator sent the link but never saw the password.
            user.Id,
            ct: CancellationToken.None);

        logger.LogInformation("Password of account {UserId} reset through its link.", user.Id);

        return UserResult.Success(UserAccount.From(user));
    }

    /// <summary>
    /// The second revoke of <see cref="ResetPasswordAsync"/>. Logged rather than thrown: the new
    /// password is saved and the link spent, so failing the request would tell the person their
    /// reset did not happen when it did, with no link left to retry. The first revoke has already
    /// ended every session that existed when the reset began.
    /// </summary>
    private async Task RevokeSessionsAfterResetAsync(Guid userId)
    {
        try
        {
            await refreshTokens.RevokeAllForUserAsync(userId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Password of account {UserId} was reset, but the second session revoke failed; a "
                + "sign-in made during the reset may stay open.",
                userId);
        }
    }

    /// <summary>
    /// What a guarded write produced: the answer for the caller, and whose session the change
    /// invalidated — <c>null</c> when nothing needs revoking, including on every refused branch.
    /// </summary>
    private readonly record struct Written(UserResult Result, Guid? RevokeSessionsOf)
    {
        /// <summary>
        /// Lets a refusal be returned as-is. Every early return is a branch that wrote nothing,
        /// so there is never a session to end on one, and spelling that out at each of them would
        /// be a dozen chances to pass the wrong thing.
        /// </summary>
        public static implicit operator Written(UserResult result) => new(result, null);
    }

    /// <summary>
    /// Runs a write under the shared administrator-invariant lock, then ends the affected sessions
    /// once the transaction has committed.
    ///
    /// Revoking afterwards rather than inside is deliberate. Redis is a separate system: reaching
    /// it while holding the lock would keep every other administrative write waiting for a network
    /// round trip, and an outage there would roll back an edit the database had already accepted.
    ///
    /// The cost of this order is a window, not a hole: between the commit and the revoke the
    /// person still holds a usable refresh token. That window is bounded by the access token's own
    /// 15 minutes — the same exposure already accepted elsewhere, and the reason
    /// <c>/api/users</c> re-reads the caller's account on every request instead of trusting the
    /// token's claims.
    /// </summary>
    private async Task<UserResult> GuardedWriteAsync(
        Func<CancellationToken, Task<Written>> write, CancellationToken ct)
    {
        var written = await administratorInvariant.WriteAsync(write, ct);

        if (written.RevokeSessionsOf is { } userId)
        {
            try
            {
                await refreshTokens.RevokeAllForUserAsync(userId, ct);
            }
            catch (Exception exception)
            {
                // Logged, not rethrown. The write is committed and correct; failing the request now
                // would tell the administrator their change did not happen, and they would repeat
                // it. What is actually lost is the early end of one session, which expires by
                // itself within the access token's lifetime.
                logger.LogError(
                    exception,
                    "Account {UserId} was changed, but ending its sessions failed. The session "
                    + "lapses on its own when the access token expires.",
                    userId);
            }

            // An outstanding UC-08 reset link goes too, for every change that ends the sessions:
            //   - deactivation: otherwise reactivating within the hour brings the link back;
            //   - email change: otherwise a link sent to a mistyped or compromised address keeps
            //     working after the address is corrected;
            //   - role change: not needed for safety, but it rides on the same signal, and an
            //     administrator who changed someone's access can send a fresh link in one click.
            // Separate from the revoke above, so a failure in either cannot skip the other.
            try
            {
                await passwordResetTokens.RevokeAsync(userId, ct);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Account {UserId} was changed, but revoking its password reset link failed. "
                    + "The link expires on its own within the hour.",
                    userId);
            }
        }

        return written.Result;
    }

    /// <summary>
    /// Whether this change would leave the organisation with no active account able to
    /// administer users.
    ///
    /// <paramref name="newRoleId"/> is the role the account would end up with — pass the unchanged
    /// role when deactivating, since there the account loses its capability by becoming inactive
    /// rather than by changing role.
    ///
    /// Answers false when the account does not currently administer anything: taking away a
    /// capability nobody has cannot remove the last holder of it.
    /// </summary>
    private async Task<bool> WouldRemoveTheLastAdministratorAsync(
        User user, Guid newRoleId, CancellationToken ct)
    {
        // An inactive account administers nothing — it cannot sign in, and the count below only
        // ever counts active accounts. So no change to it can remove the last administrator.
        //
        // Without this the rule was a dead end rather than a safeguard: the count excludes this
        // account and counts only active ones, so the sole holder of AdminUsers, once deactivated,
        // could never be moved to another role — the answer was "that would leave no active
        // administrator", when in truth there was already none and this change would not alter
        // that. Reactivating them is still guarded, because that goes through ChangeStatusAsync.
        if (!user.IsActive)
        {
            return false;
        }

        var current = await users.GetCapabilitiesAsync(user.RoleId, ct);

        if (!current.Contains(Capability.AdminUsers))
        {
            return false;
        }

        // A role change that keeps the capability takes nothing away. Callers pass the unchanged
        // role when deactivating, where the capability is lost by the account going inactive
        // rather than by the role moving.
        if (newRoleId != user.RoleId)
        {
            var next = await users.GetCapabilitiesAsync(newRoleId, ct);

            if (next.Contains(Capability.AdminUsers))
            {
                return false;
            }
        }

        // Excluding this account, is anybody else still able to administer?
        return await users.CountActiveWithCapabilityAsync(Capability.AdminUsers, user.Id, ct) == 0;
    }

    /// <summary>
    /// The fields UC-05 and UC-06 share. Returns field name → Vietnamese message, empty when
    /// everything is acceptable.
    /// </summary>
    private static Dictionary<string, string> Validate(string fullName, string email)
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

        return errors;
    }

    /// <summary>
    /// The chosen role, or null with the reason added to <paramref name="errors"/>. A role is a row
    /// an administrator can delete (ADR-0008), so a form opened before the delete can post an id
    /// that is gone — MSG09 on the field, rather than the foreign key's 500.
    /// </summary>
    private async Task<Role?> FindRoleAsync(
        Guid roleId, Dictionary<string, string> errors, CancellationToken ct)
    {
        var role = await users.FindRoleAsync(roleId, ct);

        if (role is null)
        {
            errors["roleId"] = RoleNotFound;
        }

        return role;
    }

    /// <summary>
    /// Deliberately loose: something, an @, something with a dot, no spaces. Whether an address
    /// can actually receive mail is answered by sending to it, not by a regular expression — the
    /// strict ones reject valid addresses and still accept unreachable ones.
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\z</c>, not <c>$</c>: in .NET <c>$</c> also matches just before a trailing
    /// newline, so "user@x.vn\n" would pass. The callers trim first, but this method has to be
    /// safe for the next one that does not — citext treats the two as different addresses, which
    /// would allow two accounts that look identical on screen.
    /// </remarks>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+\z")]
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

    /// <summary>SQLSTATE 23503 — matched on the code for the reason <see cref="IsDuplicateEmail"/> gives.</summary>
    private static bool IsForeignKeyViolation(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException { SqlState: "23503" } };

    private const string RoleNotFound = "Vai trò không tồn tại.";
}
