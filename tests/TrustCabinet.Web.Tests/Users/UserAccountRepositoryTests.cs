using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Users;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// That the repository's queries translate to SQL the PostgreSQL provider can run.
///
/// No database: EF builds the SQL against the Npgsql provider, which is where an untranslatable
/// expression throws. Without this, a LINQ query that only EF's in-memory provider accepts would
/// pass every other test in this suite and fail the first time anybody opened the screen.
/// <c>DatabaseModelTests</c> uses the same trick for the model itself.
/// </summary>
public class UserAccountRepositoryTests
{
    private static TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql("Host=none;Database=none")
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public void List_orders_by_name_in_the_database()
    {
        using var db = NewContext();

        var sql = db.Users.OrderBy(u => u.FullName).ThenBy(u => u.Email).ToQueryString();

        // Sorted by PostgreSQL's collation, not by .NET's ordinal comparison, which would put "Đ"
        // after "Z".
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("full_name", sql);
    }

    [Fact]
    public void Email_lookup_does_not_lowercase_either_side()
    {
        using var db = NewContext();

        var sql = db.Users.Where(u => u.Email == "ai-do@trustcabinet.local").ToQueryString();

        // The column is citext, so PostgreSQL already compares case-insensitively and can use the
        // unique index. A LOWER() on either side would silently turn this into a sequential scan.
        Assert.DoesNotContain("lower(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Counting_capability_holders_translates_to_a_join_not_a_role_name()
    {
        using var db = NewContext();
        var exceptId = Guid.CreateVersion7();

        var sql = db.Users
            .Where(u => u.IsActive
                && u.Id != exceptId
                && db.RoleCapabilities.Any(rc =>
                    rc.RoleId == u.RoleId && rc.Capability == Capability.AdminUsers))
            .ToQueryString();

        // The correlated subquery has to reach role_capabilities: counting on the Admin role name
        // would silently stop working once AdminUsers is granted to another role, which is the
        // reconfiguration role_capabilities exists to allow.
        Assert.Contains("role_capabilities", sql);
        Assert.Contains("is_active", sql);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Email_exists_translates_with_and_without_the_excluded_account(bool exclude)
    {
        using var db = NewContext();
        // The nullable Guid captured in the predicate is the part worth checking: EF has to turn
        // `exceptId == null` into a constant rather than a parameter comparison, and getting this
        // wrong throws only at runtime.
        Guid? exceptId = exclude ? Guid.CreateVersion7() : null;

        var sql = db.Users
            .Where(u => u.Email == "ai-do@trustcabinet.local"
                && (exceptId == null || u.Id != exceptId))
            .ToQueryString();

        Assert.Contains("email", sql);
        // Excluding an account has to appear in the SQL; without it, UC-06 would read an account
        // keeping its own email as a duplicate.
        Assert.Equal(exclude, sql.Contains("id <>", StringComparison.OrdinalIgnoreCase));
    }
}
