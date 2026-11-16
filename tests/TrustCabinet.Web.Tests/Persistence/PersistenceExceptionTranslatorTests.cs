using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure.Configurations;
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
        // The same, for a serialization failure during a save.
        new InvalidOperationException("transient",
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.SerializationFailure))),
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
        // A statement cancelled half way carries the database error it was cancelled with.
        new OperationCanceledException("cancelled", Postgres(PostgresErrorCodes.UniqueViolation)),
        // Translated already by an inner unit of work: it must not be wrapped a second time.
        new UniqueConflictException(UniqueConflict.Unknown,
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation))),
        new PersistenceUnavailableException(new NpgsqlException("connection refused")),
    };

    [Theory]
    [MemberData(nameof(NotStorage))]
    public void What_is_not_a_storage_failure_or_is_translated_already_is_left_alone(Exception failure)
        => Assert.Null(PersistenceExceptionTranslator.Translate(failure));

    /// <summary>SCRUM-171 (CA-06): the table names the index whose duplicate the calendar-share writes answer with MSG449.</summary>
    [Fact]
    public void The_open_pair_index_of_calendar_shares_is_in_the_table()
    {
        var wrapped = new DbUpdateException("save failed",
            Postgres(PostgresErrorCodes.UniqueViolation, "ux_calendar_shares_open_pair"));

        Assert.Equal(UniqueConflict.CalendarShareOpenPair,
            Assert.IsType<UniqueConflictException>(PersistenceExceptionTranslator.Translate(wrapped)).Conflict);
    }

    /// <summary>SCRUM-177 (CA-12): the indexes whose duplicates the vote writes answer with their own outcome.</summary>
    [Theory]
    [InlineData("ix_voting_sessions_meeting_id", UniqueConflict.OpenVoteInMeeting)]
    [InlineData("ix_vote_receipts_voting_session_id_voter_id", UniqueConflict.BallotAlreadyCast)]
    [InlineData("ix_vote_receipts_voting_session_id_guest_invitation_id", UniqueConflict.BallotAlreadyCast)]
    [InlineData("ix_document_read_confirmations_meeting_document_id_user_id", UniqueConflict.DocumentAlreadyConfirmed)]
    [InlineData("ix_document_read_confirmations_meeting_document_id_guest_invit", UniqueConflict.DocumentAlreadyConfirmed)]
    public void The_vote_indexes_are_in_the_table(string index, UniqueConflict conflict)
    {
        var wrapped = new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, index));

        Assert.Equal(conflict, Assert.IsType<UniqueConflictException>(PersistenceExceptionTranslator.Translate(wrapped)).Conflict);
    }

    [Theory]
    [InlineData(SpeakingRequestConfiguration.FloorIndex, UniqueConflict.SpeakingFloorTaken)]
    [InlineData(SpeakingRequestConfiguration.OpenUserIndex, UniqueConflict.SpeakingRequestAlreadyOpen)]
    [InlineData(SpeakingRequestConfiguration.OpenGuestIndex, UniqueConflict.SpeakingRequestAlreadyOpen)]
    public void The_speaking_indexes_are_in_the_table(string index, UniqueConflict conflict)
    {
        var wrapped = new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, index));

        Assert.Equal(conflict, Assert.IsType<UniqueConflictException>(PersistenceExceptionTranslator.Translate(wrapped)).Conflict);
    }

    private static PostgresException Postgres(string sqlState, string? constraint = null)
        => new("rejected", "ERROR", "ERROR", sqlState, constraintName: constraint);
}
