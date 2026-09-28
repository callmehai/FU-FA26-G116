using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A meeting, from first draft to archived record. <see cref="Status"/> is the spine of the whole
/// system: it decides who may edit, when voting opens, and when minutes can be issued.
/// </summary>
public class Meeting : Entity
{
    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// No database default on purpose. The application must state the status explicitly, so a code
    /// path that forgets fails loudly instead of silently creating a meeting in some default state.
    /// </summary>
    public MeetingStatus Status { get; set; }

    /// <summary>
    /// Deliberately has no database default. The application applies the form's explicit initial
    /// choice (`Normal`) when a request omits this optional selector.
    /// </summary>
    public Sensitivity Sensitivity { get; set; }

    public MeetingMode Mode { get; set; }

    /// <summary>Room name, address, or how to join when the meeting is online.</summary>
    public string Location { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Optional planned end; when supplied, it must be after <see cref="StartsAt"/>.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>The active approver assignment for this meeting's current approval cycle.</summary>
    public Guid? AssignedApproverId { get; set; }
    public User? AssignedApprover { get; set; }

    public ICollection<MeetingParticipant> Participants { get; set; } = [];
    public ICollection<MeetingStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<AgendaItem> AgendaItems { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<VotingSession> VotingSessions { get; set; } = [];
    public ICollection<SpeakingRequest> SpeakingRequests { get; set; } = [];
    public ICollection<MeetingNote> Notes { get; set; } = [];
    public ICollection<Recording> Recordings { get; set; } = [];
    public ICollection<MeetingMinutes> Minutes { get; set; } = [];

    /// <summary>At most one, but modelled as a reference so a missing summary is simply null.</summary>
    public MeetingSummary? Summary { get; set; }
}
