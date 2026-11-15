namespace TrustCabinet.Application.Abstractions;

/// <summary>
/// The store rejected the work or could not take it. A use case catches one of the four kinds below instead of
/// a database exception; the original stays in <see cref="Exception.InnerException"/> for the log.
/// </summary>
public abstract class PersistenceException(string message, Exception? innerException)
    : Exception(message, innerException);

/// <summary>A row with the same unique value already exists. <see cref="Conflict"/> says which rule it broke.</summary>
public sealed class UniqueConflictException(UniqueConflict conflict, Exception? innerException = null)
    : PersistenceException($"A unique rule of the store was broken ({conflict}).", innerException)
{
    public UniqueConflict Conflict { get; } = conflict;
}

/// <summary>A row points at one that does not exist, or the row to remove is still pointed at.</summary>
public sealed class ReferenceConflictException(Exception? innerException = null)
    : PersistenceException("A reference between rows of the store was broken.", innerException);

/// <summary>The row changed or disappeared between being read and being written.</summary>
public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : PersistenceException("The row was changed by someone else before this write.", innerException);

/// <summary>The store failed for any other reason: unreachable, timed out, or a write it refused.</summary>
public sealed class PersistenceUnavailableException(Exception? innerException = null)
    : PersistenceException("The store could not complete the work.", innerException);
