using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Infrastructure.Persistence;

/// <summary>
/// The one place that knows a storage failure by its PostgreSQL <c>SqlState</c> and a unique rule by the name
/// of its index, and turns both into the exceptions a use case catches (SCRUM-143, rule R8).
/// </summary>
internal static class PersistenceExceptionTranslator
{
    /// <summary>
    /// Unique index → the rule it enforces. A module adds a row, with the <see cref="UniqueConflict"/> member,
    /// only where its use case answers differently for that index; every other duplicate is
    /// <see cref="UniqueConflict.Unknown"/>.
    /// </summary>
    private static readonly Dictionary<string, UniqueConflict> UniqueIndexes = new(StringComparer.Ordinal)
    {
        ["ux_calendar_shares_open_pair"] = UniqueConflict.CalendarShareOpenPair,
        ["ix_voting_sessions_meeting_id"] = UniqueConflict.OpenVoteInMeeting,
        ["ix_vote_receipts_voting_session_id_voter_id"] = UniqueConflict.BallotAlreadyCast,
        ["ix_vote_receipts_voting_session_id_guest_invitation_id"] = UniqueConflict.BallotAlreadyCast,
        ["ix_document_read_confirmations_meeting_document_id_user_id"] = UniqueConflict.DocumentAlreadyConfirmed,
        // PostgreSQL cuts identifiers at 63 bytes.
        ["ix_document_read_confirmations_meeting_document_id_guest_invit"] = UniqueConflict.DocumentAlreadyConfirmed,
    };

    /// <summary>
    /// The storage exception <paramref name="exception"/> stands for, carrying it as the inner exception, or
    /// <c>null</c> when it is not a storage failure and must be rethrown as it is. An exception translated
    /// already and a cancellation are left alone.
    /// </summary>
    public static PersistenceException? Translate(Exception exception) => Translate(exception, UniqueIndexes);

    internal static PersistenceException? Translate(
        Exception exception, IReadOnlyDictionary<string, UniqueConflict> uniqueIndexes)
    {
        if (exception is PersistenceException) return null;

        return Classify(exception, out var index) switch
        {
            StorageFailure.UniqueViolation => new UniqueConflictException(
                index is not null && uniqueIndexes.TryGetValue(index, out var conflict) ? conflict : UniqueConflict.Unknown,
                exception),
            StorageFailure.ReferenceViolation => new ReferenceConflictException(exception),
            StorageFailure.ConcurrencyConflict => new ConcurrencyConflictException(exception),
            StorageFailure.Unavailable => new PersistenceUnavailableException(exception),
            _ => null,
        };
    }

    /// <summary>
    /// What kind of storage failure a database exception is, with the constraint a unique violation names.
    /// <see cref="Translate(Exception)"/> and <see cref="DbErrors"/> both decide by this, so code that still
    /// catches the database exception and code that catches the translated one agree on every failure.
    /// </summary>
    internal static StorageFailure Classify(Exception exception, out string? constraint)
    {
        constraint = null;
        if (exception is OperationCanceledException) return StorageFailure.None;
        if (exception is DbUpdateConcurrencyException) return StorageFailure.ConcurrencyConflict;

        // EF Core wraps the PostgreSQL error in a DbUpdateException; raw SQL and ExecuteUpdate throw it bare.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch ((current as PostgresException)?.SqlState)
            {
                case PostgresErrorCodes.UniqueViolation:
                    constraint = ((PostgresException)current).ConstraintName;
                    return StorageFailure.UniqueViolation;
                case PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation:
                    return StorageFailure.ReferenceViolation;
            }
        }

        // The last form is what EF Core's execution strategy throws for a transient failure it does not retry: a lost
        // connection, or a serialization failure, which reaches it inside a DbUpdateException when it happens in a save.
        return exception is DbUpdateException or NpgsqlException or TimeoutException
            or InvalidOperationException { InnerException: DbUpdateException or NpgsqlException or TimeoutException }
            ? StorageFailure.Unavailable
            : StorageFailure.None;
    }
}

/// <summary>The kinds of storage failure, one per exception of <c>PersistenceExceptions.cs</c>.</summary>
internal enum StorageFailure { None, UniqueViolation, ReferenceViolation, ConcurrencyConflict, Unavailable }
