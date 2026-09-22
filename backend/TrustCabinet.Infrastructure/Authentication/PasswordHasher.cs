using Microsoft.AspNetCore.Identity;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Password hashing, delegated to ASP.NET Core's own hasher rather than assembled here.
///
/// Its default (PBKDF2-HMAC-SHA512, 210 000 iterations, 128-bit random salt per password) is a
/// reviewed default that is raised as hardware gets faster, and its format carries the algorithm
/// and parameters inside the hash string, so an old hash stays verifiable after that default
/// changes. Hand-rolling this is how projects end up with a single unsalted SHA-256.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    // The generic argument is only how the framework's interface is shaped; nothing about the User
    // row takes part in the hash, and the instance below is never touched.
    private static readonly PasswordHasher<User> Hasher = new();

    private static readonly User Unused = new()
    {
        FullName = string.Empty,
        Email = string.Empty,
        PasswordHash = string.Empty,
    };

    public string Hash(string password) => Hasher.HashPassword(Unused, password);

    public bool Verify(string passwordHash, string providedPassword)
    {
        // A malformed or placeholder hash (the seeder's "SET-BY-SCRUM-11", say) makes the
        // framework's parser throw. That is a failed verification, not a server error.
        try
        {
            var result = Hasher.VerifyHashedPassword(Unused, passwordHash, providedPassword);

            // SuccessRehashNeeded means the hash is valid but was made with older parameters.
            // Treated as success; re-hashing on sign-in is SCRUM-11's follow-up, not this story.
            // ponytail: no transparent rehash-on-login — add when the iteration default changes.
            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
