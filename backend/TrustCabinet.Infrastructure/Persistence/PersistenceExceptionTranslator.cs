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
    private static readonly Dictionary<string, UniqueConflict> UniqueIndexes = new(StringComparer.Ordinal);

    /// <summary>
    /// The storage exception <paramref name="exception"/> stands for, carrying it as the inner exception, or
    /// <c>null</c> when it is not a storage failure and must be rethrown as it is. An exception translated
    /// already and a cancellation are left alone.
    /// </summary>
    public static PersistenceException? Translate(Exception exception) => Translate(exception, UniqueIndexes);

    internal static PersistenceException? Translate(
        Exception exception, IReadOnlyDictionary<string, UniqueConflict> uniqueIndexes)
    {
        if (exception is PersistenceException or OperationCanceledException) return null;
        if (exception is DbUpdateConcurrencyException) return new ConcurrencyConflictException(exception);

        // EF Core wraps the PostgreSQL error in a DbUpdateException; raw SQL and ExecuteUpdate throw it bare.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch ((current as PostgresException)?.SqlState)
            {
                case PostgresErrorCodes.UniqueViolation:
                    var index = ((PostgresException)current).ConstraintName;
                    return new UniqueConflictException(
                        index is not null && uniqueIndexes.TryGetValue(index, out var conflict) ? conflict : UniqueConflict.Unknown,
                        exception);
                case PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation:
                    return new ReferenceConflictException(exception);
            }
        }

        // The last form is what EF Core's execution strategy throws for a transient failure it does not retry.
        return exception is DbUpdateException or NpgsqlException or TimeoutException
            or InvalidOperationException { InnerException: NpgsqlException or TimeoutException }
            ? new PersistenceUnavailableException(exception)
            : null;
    }
}
