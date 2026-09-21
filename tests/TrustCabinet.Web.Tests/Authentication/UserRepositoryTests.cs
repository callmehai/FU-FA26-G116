using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The real repository against a real PostgreSQL — previously untested, which is how a blank
/// Google subject came to match arbitrary accounts.
///
/// A fake could not have caught that: the fake in AuthenticationTestDoubles guards against a null
/// subject, while production did not, so every test passed. The bug lived in the gap between the
/// two, and only real SQL shows it — `== null` translates to `IS NULL`, which is a match rather
/// than a comparison.
///
/// Needs the dev database: `docker compose up -d`. Fails rather than skips when it is unreachable,
/// for the same reason RedisRefreshTokenStoreTests does.
/// </summary>
public class UserRepositoryTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=trustcabinet_repo_tests;Username=trustcabinet;Password=123";

    private TrustCabinetDbContext _db = null!;

    /// <summary>
    /// A database of its own, created and dropped per run, so these tests never touch the rows
    /// someone is working with in the dev database.
    /// </summary>
    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new TrustCabinetDbContext(options);

        try
        {
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
        }
        catch (Npgsql.NpgsqlException e)
        {
            throw new InvalidOperationException(
                "These tests need the development PostgreSQL. Start it with "
                + "`docker compose up -d postgres`.", e);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<User> AddUserAsync(
        string email, string? passwordHash = "hash", string? googleSubjectId = null)
    {
        var user = new User
        {
            FullName = "Người Dùng",
            Email = email,
            PasswordHash = passwordHash,
            GoogleSubjectId = googleSubjectId,
            Role = SystemRole.Secretary,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task A_linked_account_is_found_by_its_google_subject()
    {
        var user = await AddUserAsync("linked@x.local", googleSubjectId: "sub-abc-123");

        var found = await new UserRepository(_db).FindByGoogleSubjectIdAsync("sub-abc-123");

        Assert.Equal(user.Id, found!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // The case that actually mattered. EF translates `== null` into `IS NULL`, which is a match
    // rather than a comparison, while `== ""` stays a comparison and is harmless. Verified against
    // this database with ToQueryString():
    //   "sub-123" → WHERE google_subject_id = @subject
    //   ""        → WHERE google_subject_id = @subject
    //   null      → WHERE google_subject_id IS NULL      ← matches every unlinked account
    // Passed as null! because the interface takes a non-nullable string: the point is precisely
    // that a null arrives anyway, from a Google payload whose `sub` nobody had validated.
    [InlineData(null!)]
    public async Task A_blank_google_subject_matches_nothing(string subject)
    {
        // Four accounts that sign in with a password and have no Google link — exactly the shape
        // of the seeded database.
        await AddUserAsync("a@x.local");
        await AddUserAsync("b@x.local");
        await AddUserAsync("c@x.local");
        await AddUserAsync("d@x.local");

        var found = await new UserRepository(_db).FindByGoogleSubjectIdAsync(subject);

        // Before the fix this returned whichever unlinked row PostgreSQL happened to give first,
        // and the caller was issued a full token pair as that person. On the seeded data that was
        // the Chair.
        Assert.Null(found);
    }

    [Fact]
    public async Task An_unknown_google_subject_matches_nothing()
    {
        await AddUserAsync("linked@x.local", googleSubjectId: "sub-abc-123");

        Assert.Null(await new UserRepository(_db).FindByGoogleSubjectIdAsync("sub-khac"));
    }

    [Fact]
    public async Task The_google_subject_comparison_is_case_sensitive()
    {
        await AddUserAsync("linked@x.local", googleSubjectId: "Sub-AbC-123");

        // Unlike the email, which is citext: a Google subject is an opaque identifier, not
        // something a person types, so two spellings are two different identities.
        Assert.Null(await new UserRepository(_db).FindByGoogleSubjectIdAsync("sub-abc-123"));
    }

    [Fact]
    public async Task An_email_is_matched_without_regard_to_case()
    {
        var user = await AddUserAsync("ThuKy@TrustCabinet.local");

        // citext, so the unique index also rejects a second account differing only in case.
        var found = await new UserRepository(_db).FindByEmailAsync("thuky@trustcabinet.LOCAL");

        Assert.Equal(user.Id, found!.Id);
    }

    [Fact]
    public async Task A_google_only_account_can_be_stored_and_found()
    {
        // The whole point of the migration: no password at all.
        var user = await AddUserAsync("googleonly@x.local", passwordHash: null, googleSubjectId: "sub-g-1");

        var found = await new UserRepository(_db).FindByGoogleSubjectIdAsync("sub-g-1");

        Assert.Equal(user.Id, found!.Id);
        Assert.Null(found.PasswordHash);
    }

    [Fact]
    public async Task An_account_with_no_way_to_sign_in_is_rejected_by_the_database()
    {
        _db.Users.Add(new User
        {
            FullName = "Không Có Gì",
            Email = "nothing@x.local",
            PasswordHash = null,
            GoogleSubjectId = null,
            Role = SystemRole.Member,
        });

        // ck_users_has_a_way_to_sign_in. Enforced by the database, so a seed or a hand-written
        // INSERT cannot create an account nobody could ever sign into either.
        var e = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("ck_users_has_a_way_to_sign_in", e.InnerException!.Message);

        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task One_google_identity_cannot_be_linked_to_two_accounts()
    {
        await AddUserAsync("first@x.local", googleSubjectId: "sub-trung");

        _db.Users.Add(new User
        {
            FullName = "Người Thứ Hai",
            Email = "second@x.local",
            PasswordHash = "hash",
            GoogleSubjectId = "sub-trung",
            Role = SystemRole.Member,
        });

        // The filtered unique index. Without it, which account a Google sign-in landed on would
        // depend on row order.
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());

        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Many_accounts_may_have_no_google_link()
    {
        // The unique index is filtered on IS NOT NULL, so NULLs do not collide.
        await AddUserAsync("p1@x.local");
        await AddUserAsync("p2@x.local");
        await AddUserAsync("p3@x.local");

        Assert.Equal(3, await _db.Users.CountAsync(u => u.GoogleSubjectId == null));
    }

    [Fact]
    public async Task Capabilities_come_from_the_role_capabilities_table()
    {
        // EnsureCreated applies the HasData rows, so the seeded mapping is present.
        var capabilities = await new UserRepository(_db).GetCapabilitiesAsync(SystemRole.Secretary);

        Assert.Contains(Capability.MeetingCreator, capabilities);
        Assert.Contains(Capability.MinutesEditor, capabilities);
    }

    [Fact]
    public async Task A_member_has_no_capability()
    {
        var capabilities = await new UserRepository(_db).GetCapabilitiesAsync(SystemRole.Member);

        // Empty is a legitimate answer, not a missing row — a plain Member attends, reads and
        // votes, none of which needs a capability.
        Assert.Empty(capabilities);
    }
}
