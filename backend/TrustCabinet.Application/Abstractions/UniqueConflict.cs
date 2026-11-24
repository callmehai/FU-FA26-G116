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

    /// <summary>BR-142, BR-143: a pair, in one direction, already has an open (Pending or Effective) calendar share.</summary>
    CalendarShareOpenPair = 1,

    /// <summary>BR-090: the meeting already has an Open vote.</summary>
    OpenVoteInMeeting = 2,

    /// <summary>BR-092, BR-093: the voter (user or guest invitation) already holds a receipt for this vote.</summary>
    BallotAlreadyCast = 3,

    /// <summary>UC-42: the reader (user or guest invitation) already confirmed reading this meeting document.</summary>
    DocumentAlreadyConfirmed = 4,

    /// <summary>BR-104–BR-106: someone in the meeting is already invited to speak or speaking.</summary>
    SpeakingFloorTaken = 5,

    /// <summary>UC-50 EX-2: the person (user or guest invitation) already has an open speaking request.</summary>
    SpeakingRequestAlreadyOpen = 6,
}
