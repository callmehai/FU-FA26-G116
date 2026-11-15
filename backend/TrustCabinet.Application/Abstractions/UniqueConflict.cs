namespace TrustCabinet.Application.Abstractions;

/// <summary>
/// Which unique rule a <see cref="UniqueConflictException"/> broke, in business terms. A member is added only
/// when a use case answers differently for that rule than for any other duplicate, together with its row in
/// the table of <c>PersistenceExceptionTranslator</c> (SCRUM-143, rule R8).
/// </summary>
public enum UniqueConflict
{
    /// <summary>A duplicate no use case tells apart from the others.</summary>
    Unknown = 0,
}
