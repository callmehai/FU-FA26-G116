using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Infrastructure.Persistence;

/// <summary>
/// What a storage failure is, for the code still in Infrastructure (SCRUM-143, rule R8; SCRUM-146). A service
/// not moved to Application yet catches whatever the database threw, and it may also be handed a
/// <see cref="PersistenceException"/> by a moved service it calls: both forms answer the same here, so the
/// caller keeps the <c>MSGnn</c> it gave before.
/// </summary>
internal static class DbErrors
{
    /// <summary>
    /// Any failure of the store, in the widest form: a constraint the database enforced, a lost connection, a
    /// timeout, and the same wrapped in the <see cref="InvalidOperationException"/> of EF Core's execution strategy.
    /// </summary>
    public static bool IsPersistenceFailure(Exception exception) => Classify(exception, out _) != StorageFailure.None;

    /// <summary>A unique index refused the row; with <paramref name="constraint"/>, that index and no other.</summary>
    public static bool IsUniqueViolation(Exception exception, string? constraint = null)
        => Classify(exception, out var violated) == StorageFailure.UniqueViolation
            && (constraint is null || violated == constraint);

    /// <summary>A foreign key refused the write: the row pointed at is gone, or the row to remove is still pointed at.</summary>
    public static bool IsReferenceViolation(Exception exception)
        => Classify(exception, out _) == StorageFailure.ReferenceViolation;

    /// <summary>The row changed or disappeared between being read and being written.</summary>
    public static bool IsConcurrencyConflict(Exception exception)
        => Classify(exception, out _) == StorageFailure.ConcurrencyConflict;

    private static StorageFailure Classify(Exception exception, out string? constraint)
    {
        if (exception is not PersistenceException translated)
            return PersistenceExceptionTranslator.Classify(exception, out constraint);

        // Translated already by a unit of work: the database exception it carries still names the index. One
        // built without it (a fake in a unit test) is known by its type alone.
        constraint = null;
        if (translated.InnerException is { } original
            && PersistenceExceptionTranslator.Classify(original, out constraint) is var kind and not StorageFailure.None)
        {
            return kind;
        }

        return translated switch
        {
            UniqueConflictException => StorageFailure.UniqueViolation,
            ReferenceConflictException => StorageFailure.ReferenceViolation,
            ConcurrencyConflictException => StorageFailure.ConcurrencyConflict,
            _ => StorageFailure.Unavailable,
        };
    }
}
