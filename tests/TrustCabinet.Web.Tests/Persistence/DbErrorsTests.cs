using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Persistence;

/// <summary>
/// SCRUM-166 (CA-01): <see cref="DbErrors"/> answers the same for what the database threw and for the
/// <see cref="PersistenceException"/> a unit of work made of it, so a service still in Infrastructure keeps its
/// <c>MSGnn</c> whichever of the two reaches its <c>catch</c>.
/// </summary>
public sealed class DbErrorsTests
{
    private const string Index = "ux_calendar_shares_open_pair";

    /// <summary>Each failure as the database throws it and as <see cref="PersistenceExceptionTranslator"/> hands it on.</summary>
    private static TheoryData<Exception> BothForms(params Exception[] failures)
    {
        var data = new TheoryData<Exception>();
        foreach (var failure in failures)
        {
            data.Add(failure);
            data.Add(PersistenceExceptionTranslator.Translate(failure)!);
        }

        return data;
    }

    public static TheoryData<Exception> UniqueViolations => BothForms(
        new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, Index)),
        // Raw SQL and ExecuteUpdate throw it bare.
        Postgres(PostgresErrorCodes.UniqueViolation, Index),
        new InvalidOperationException("transient",
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, Index))));

    [Theory]
    [MemberData(nameof(UniqueViolations))]
    public void A_unique_violation_is_recognised_with_and_without_the_name_of_its_index(Exception failure)
    {
        Assert.True(DbErrors.IsUniqueViolation(failure));
        Assert.True(DbErrors.IsUniqueViolation(failure, Index));
        Assert.False(DbErrors.IsUniqueViolation(failure, "ux_another_index"));

        Assert.True(DbErrors.IsPersistenceFailure(failure));
        Assert.False(DbErrors.IsReferenceViolation(failure));
        Assert.False(DbErrors.IsConcurrencyConflict(failure));
    }

    [Fact]
    public void A_unique_violation_that_names_no_index_matches_only_when_none_is_asked_for()
    {
        var failure = new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation));

        Assert.True(DbErrors.IsUniqueViolation(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure, Index));
    }

    public static TheoryData<Exception> ReferenceViolations => BothForms(
        new DbUpdateException("save failed", Postgres(PostgresErrorCodes.ForeignKeyViolation)),
        new DbUpdateException("save failed", Postgres(PostgresErrorCodes.RestrictViolation)),
        Postgres(PostgresErrorCodes.ForeignKeyViolation));

    [Theory]
    [MemberData(nameof(ReferenceViolations))]
    public void A_foreign_key_or_restrict_violation_is_a_reference_violation(Exception failure)
    {
        Assert.True(DbErrors.IsReferenceViolation(failure));
        Assert.True(DbErrors.IsPersistenceFailure(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure));
        Assert.False(DbErrors.IsConcurrencyConflict(failure));
    }

    public static TheoryData<Exception> ConcurrencyConflicts => BothForms(new DbUpdateConcurrencyException("0 rows"));

    [Theory]
    [MemberData(nameof(ConcurrencyConflicts))]
    public void A_row_that_changed_under_the_write_is_a_concurrency_conflict(Exception failure)
    {
        Assert.True(DbErrors.IsConcurrencyConflict(failure));
        Assert.True(DbErrors.IsPersistenceFailure(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure));
        Assert.False(DbErrors.IsReferenceViolation(failure));
    }

    public static TheoryData<Exception> OtherStorageFailures => BothForms(
        new DbUpdateException("save failed", new InvalidCastException()),
        new NpgsqlException("connection refused"),
        Postgres(PostgresErrorCodes.CheckViolation),
        new TimeoutException(),
        // What EF Core's execution strategy throws for a transient failure it does not retry. Most services let
        // these three through as a 500 before SCRUM-146.
        new InvalidOperationException("transient", new NpgsqlException("connection reset")),
        new InvalidOperationException("transient", new TimeoutException()),
        new InvalidOperationException("transient",
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.SerializationFailure))));

    [Theory]
    [MemberData(nameof(OtherStorageFailures))]
    public void Every_other_storage_failure_is_a_persistence_failure_and_nothing_narrower(Exception failure)
    {
        Assert.True(DbErrors.IsPersistenceFailure(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure));
        Assert.False(DbErrors.IsReferenceViolation(failure));
        Assert.False(DbErrors.IsConcurrencyConflict(failure));
    }

    /// <summary>What a hand-written fake throws in a unit test: the translated exception with nothing inside.</summary>
    [Fact]
    public void A_translated_exception_that_carries_no_database_exception_is_known_by_its_type()
    {
        Assert.True(DbErrors.IsUniqueViolation(new UniqueConflictException(UniqueConflict.Unknown)));
        Assert.False(DbErrors.IsUniqueViolation(new UniqueConflictException(UniqueConflict.Unknown), Index));
        Assert.True(DbErrors.IsReferenceViolation(new ReferenceConflictException()));
        Assert.True(DbErrors.IsConcurrencyConflict(new ConcurrencyConflictException()));
        Assert.True(DbErrors.IsPersistenceFailure(new PersistenceUnavailableException()));
        Assert.False(DbErrors.IsUniqueViolation(new PersistenceUnavailableException()));
    }

    public static TheoryData<Exception> NotStorage => new()
    {
        new InvalidOperationException("a rule of the use case"),
        new InvalidOperationException("wrapped", new ArgumentException()),
        new ArgumentException(),
        new IOException(),
        new OperationCanceledException(),
        new TaskCanceledException(),
        // A statement cancelled half way carries the database error it was cancelled with.
        new OperationCanceledException("cancelled", Postgres(PostgresErrorCodes.UniqueViolation, Index)),
    };

    [Theory]
    [MemberData(nameof(NotStorage))]
    public void What_is_not_a_storage_failure_is_none_of_them(Exception failure)
    {
        Assert.False(DbErrors.IsPersistenceFailure(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure));
        Assert.False(DbErrors.IsUniqueViolation(failure, Index));
        Assert.False(DbErrors.IsReferenceViolation(failure));
        Assert.False(DbErrors.IsConcurrencyConflict(failure));
    }

    private static PostgresException Postgres(string sqlState, string? constraint = null)
        => new("rejected", "ERROR", "ERROR", sqlState, constraintName: constraint);
}
