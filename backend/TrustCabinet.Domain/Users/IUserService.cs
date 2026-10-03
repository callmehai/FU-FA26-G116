namespace TrustCabinet.Domain.Users;

/// <summary>
/// The <c>:User Service</c> participant of UC-04 … UC-07. Owns every rule about user accounts:
/// what makes one valid, whether an email is free, whether a status change is allowed, and what
/// gets written to the audit log when one goes through.
///
/// The endpoint holds none of that — it turns HTTP into a call here and an outcome into a status
/// code, exactly as <c>AuthEndpoints</c> does for UC-01.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// UC-04 — every account, sorted. Returns all of them: the organisation is one workspace with
    /// a countable staff (ADR-0004), so paging would be machinery for a list nobody scrolls.
    /// ponytail: no paging. Add it if this list ever outgrows a screenful in practice.
    /// </summary>
    Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// UC-06's first half — the current values, to fill the edit form with.
    /// <see cref="UserOutcome.NotFound"/> when no such account.
    /// </summary>
    Task<UserResult> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// UC-05 — creates an active account. <paramref name="actorId"/> is the administrator doing
    /// it, recorded in the audit entry.
    ///
    /// The created account signs in with a password. A Google-only account is not created here:
    /// linking one needs a Google subject that only Google can supply, and no screen in this story
    /// collects it.
    /// </summary>
    Task<UserResult> CreateAsync(NewUser request, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-06's second half — name, email and role. Not the status: that is UC-07, which also has
    /// to end the person's sessions, so mixing the two would make it possible to deactivate
    /// someone while leaving them signed in.
    /// </summary>
    Task<UserResult> UpdateAsync(
        Guid id, UserEdit request, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-07 — deactivate or reactivate. Deactivating also ends every session the account holds,
    /// because a row marked inactive while its access token still works is not deactivated in any
    /// sense that matters.
    ///
    /// Refused (<see cref="UserOutcome.StatusChangeNotAllowed"/>) when the account is already in
    /// the requested state, and when an administrator aims at their own account — locking yourself
    /// out is not something to do by misclick, and with one organisation there may be no second
    /// administrator to undo it.
    /// </summary>
    Task<UserResult> ChangeStatusAsync(
        Guid id, bool isActive, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-08 — emails the account a single-use link to choose a new password. The current password
    /// keeps working until the link is used: an administrator pressing the button must not be able
    /// to lock someone out.
    ///
    /// <see cref="UserOutcome.PasswordResetNotAvailable"/> for a deactivated account,
    /// <see cref="UserOutcome.PasswordResetEmailFailed"/> when the mail could not be sent.
    /// </summary>
    Task<UserResult> RequestPasswordResetAsync(
        Guid id, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// The other end of UC-08's link: the person sets a new password with the token from the email.
    /// Spends the token and ends every session of the account. Anonymous — the token is the proof.
    ///
    /// <see cref="UserOutcome.PasswordResetLinkInvalid"/> for a dead link,
    /// <see cref="UserOutcome.InvalidInformation"/> (key <c>newPassword</c>) for a password that
    /// breaks the rule, without spending the link.
    /// </summary>
    Task<UserResult> ResetPasswordAsync(
        string? token, string? newPassword, CancellationToken ct = default);
}

/// <summary>
/// What UC-05 collects. Nullable strings: a missing field is a validation error, not a 400.
/// <paramref name="RoleId"/> is <see cref="Guid.Empty"/> when the caller sent none or an
/// unreadable one — it matches no role, so it lands on the same "Vai trò không tồn tại." message.
/// </summary>
public sealed record NewUser(string? FullName, string? Email, string? Password, Guid RoleId);

/// <summary>
/// What UC-06 collects. No password: changing someone else's password is UC-08, and no
/// administrator screen should be able to set one silently while editing a name.
/// </summary>
public sealed record UserEdit(string? FullName, string? Email, Guid RoleId);
