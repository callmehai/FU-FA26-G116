namespace TrustCabinet.Domain.Participants;

public enum AssignmentStatus { Current, Removed }

public enum ParticipantOperation { Context, Roster, Options, OwnResponse, AddInternal, AddGuest, ChangeAssignments, RemoveInternal, RevokeGuest, Respond, ChairHandoverCandidates, HandOverChair }
