using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Persistence;

/// <summary>
/// SCRUM-143 (CA-00): the order in which <see cref="PersistenceExceptionTranslator"/> classifies a failure.
/// The failures PostgreSQL really raises are covered by <see cref="EfUnitOfWorkTests"/>; here each form is
/// built by hand so that none is missed.
/// </summary>
public sealed class PersistenceExceptionTranslatorTests
{
    private static readonly Dictionary<string, UniqueConflict> Indexes = new() { ["ux_known"] = (UniqueConflict)1 };

    [Theory]
    [InlineData("ux_known", 1)]
    [InlineData("ux_not_registered", 0)]
    [InlineData(null, 0)]
    public void A_unique_violation_names_the_rule_only_for_an_index_in_the_table(string? index, int expected)
    {
        var wrapped = new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, index));

        var translated = Assert.IsType<UniqueConflictException>(PersistenceExceptionTranslator.Translate(wrapped, Indexes));

        Assert.Equal((UniqueConflict)expected, translated.Conflict);
        Assert.Same(wrapped, translated.InnerException);
    }

    [Fact]
    public void A_unique_violation_thrown_bare_by_raw_sql_is_recognised_too()
    {
        var bare = Postgres(PostgresErrorCodes.UniqueViolation, "ux_known");

        Assert.Equal((UniqueConflict)1,
            Assert.IsType<UniqueConflictException>(PersistenceExceptionTranslator.Translate(bare, Indexes)).Conflict);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData(PostgresErrorCodes.RestrictViolation)]
    public void A_foreign_key_or_restrict_violation_is_a_reference_conflict(string sqlState)
    {
        var wrapped = new DbUpdateException("save failed", Postgres(sqlState));

        Assert.Same(wrapped,
            Assert.IsType<ReferenceConflictException>(PersistenceExceptionTranslator.Translate(wrapped)).InnerException);
    }

    [Fact]
    public void A_concurrency_failure_is_a_concurrency_conflict()
        => Assert.IsType<ConcurrencyConflictException>(
            PersistenceExceptionTranslator.Translate(new DbUpdateConcurrencyException("0 rows")));

    public static TheoryData<Exception> Unavailable => new()
    {
        new DbUpdateException("save failed", new InvalidCastException()),
        new NpgsqlException("connection refused"),
        // Any other PostgreSQL error, here a check constraint.
        Postgres(PostgresErrorCodes.CheckViolation),
        new TimeoutException(),
        // What EF Core's execution strategy throws for a transient failure it does not retry.
        new InvalidOperationException("transient", new NpgsqlException("connection reset")),
        new InvalidOperationException("transient", new TimeoutException()),
    };

    [Theory]
    [MemberData(nameof(Unavailable))]
    public void Every_other_storage_failure_is_persistence_unavailable(Exception failure)
        => Assert.Same(failure,
            Assert.IsType<PersistenceUnavailableException>(PersistenceExceptionTranslator.Translate(failure)).InnerException);

    public static TheoryData<Exception> NotStorage => new()
    {
        new InvalidOperationException("a rule of the use case"),
        new InvalidOperationException("wrapped", new ArgumentException()),
        new ArgumentException(),
        new OperationCanceledException(),
        new TaskCanceledException(),
        // Translated already by an inner unit of work: it must not be wrapped a second time.
        new UniqueConflictException(UniqueConflict.Unknown,
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation))),
        new PersistenceUnavailableException(new NpgsqlException("connection refused")),
    };

    [Theory]
    [MemberData(nameof(NotStorage))]
    public void What_is_not_a_storage_failure_or_is_translated_already_is_left_alone(Exception failure)
        => Assert.Null(PersistenceExceptionTranslator.Translate(failure));

    private static PostgresException Postgres(string sqlState, string? constraint = null)
        => new("rejected", "ERROR", "ERROR", sqlState, constraintName: constraint);
}
