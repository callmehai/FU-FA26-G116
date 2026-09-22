using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Users;

/// <summary>
/// One row of the user list, and what an administrator gets back after creating or editing an
/// account.
///
/// A projection rather than the <see cref="User"/> entity itself: the entity carries
/// <see cref="User.PasswordHash"/> and <see cref="User.GoogleSubjectId"/>, and neither belongs in
/// a response. Serialising the entity would put both on the wire the moment someone adds a field,
/// which is exactly the kind of leak nobody notices in review.
/// </summary>
/// <param name="HasPassword">
/// Whether the account can sign in with a password at all — UC-08 will need it to decide whether
/// resetting one makes sense, and the list screen shows how each person signs in.
/// The hash itself never leaves the backend.
/// </param>
public sealed record UserAccount(
    Guid Id,
    string FullName,
    string Email,
    SystemRole Role,
    bool IsActive,
    bool HasPassword,
    bool HasGoogleLink,
    DateTimeOffset CreatedAt)
{
    public static UserAccount From(User user) => new(
        user.Id,
        user.FullName,
        user.Email,
        user.Role,
        user.IsActive,
        !string.IsNullOrEmpty(user.PasswordHash),
        !string.IsNullOrEmpty(user.GoogleSubjectId),
        user.CreatedAt);
}
