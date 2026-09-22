namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Password hashing, kept behind an interface so the authentication service can be unit-tested
/// without paying for a real KDF on every test (the real one is deliberately slow).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash for storing in <see cref="Entities.User.PasswordHash"/>. Algorithm, parameters and a
    /// per-user salt all live inside the returned string — that is why there is no salt column.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Whether the password matches the stored hash. Implementations compare in constant time;
    /// never compare hashes with <c>==</c>.
    /// </summary>
    bool Verify(string passwordHash, string providedPassword);
}
