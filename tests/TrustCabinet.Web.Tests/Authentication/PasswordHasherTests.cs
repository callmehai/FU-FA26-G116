using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The real hasher. Few tests, because the algorithm is the framework's — what is worth pinning is
/// that it is salted, that verification is not accidentally inverted, and that bad input fails
/// rather than throws.
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void A_password_verifies_against_its_own_hash()
    {
        var hash = _hasher.Hash("MatKhau@2026");

        Assert.True(_hasher.Verify(hash, "MatKhau@2026"));
    }

    [Fact]
    public void A_different_password_does_not_verify()
    {
        var hash = _hasher.Hash("MatKhau@2026");

        Assert.False(_hasher.Verify(hash, "MatKhau@2027"));
    }

    [Fact]
    public void Password_comparison_is_case_sensitive()
    {
        var hash = _hasher.Hash("MatKhau@2026");

        // Unlike the email, which is citext.
        Assert.False(_hasher.Verify(hash, "matkhau@2026"));
    }

    [Fact]
    public void The_hash_does_not_contain_the_password()
    {
        var hash = _hasher.Hash("MatKhau@2026");

        Assert.DoesNotContain("MatKhau", hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        var first = _hasher.Hash("MatKhau@2026");
        var second = _hasher.Hash("MatKhau@2026");

        // Proof the salt is per-password and random: without it, identical passwords would produce
        // identical hashes and one leaked table would reveal every account sharing a password.
        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, "MatKhau@2026"));
        Assert.True(_hasher.Verify(second, "MatKhau@2026"));
    }

    [Fact]
    public void The_hash_fits_the_column()
    {
        // users.password_hash is varchar(512). A hash that overflowed it would fail at INSERT,
        // after the account looked successfully created.
        Assert.True(_hasher.Hash("MatKhau@2026").Length <= 512);
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-hash")]
    [InlineData("SET-BY-SCRUM-11")]
    public void A_malformed_stored_hash_fails_verification_instead_of_throwing(string storedHash)
    {
        // The seeder wrote a placeholder before this story. Such a row must be unsignable-into,
        // and must not turn a sign-in attempt into a 500.
        Assert.False(_hasher.Verify(storedHash, "bat-ky-mat-khau"));
    }

    [Fact]
    public void A_long_passphrase_is_supported()
    {
        var passphrase = new string('a', 200);

        Assert.True(_hasher.Verify(_hasher.Hash(passphrase), passphrase));
    }
}
