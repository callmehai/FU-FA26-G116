using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A meeting, from first draft to its end. One meeting is one sitting and carries its own schedule
/// (ADR-0013). <see cref="Status"/> is the spine of the whole system: it decides who may edit, when
/// voting opens, and when minutes can be drafted.
/// </summary>
public class Meeting : Entity
{
    public long ParticipantsRevision { get; set; }
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

    /// <summary>
    /// How speech is captured (SCRUM-109). Changeable until the meeting starts, then fixed: people
    /// must know before they speak whether their own mic is being transcribed, and a transcript
    /// cannot be half per-mic lines and half one room recording.
    /// </summary>
    public CaptureMode CaptureMode { get; set; } = CaptureMode.PerMic;

    /// <summary>Room name, address, or how to join when the meeting is online.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// An external meeting link. Null means the TrustCabinet online room, which is the default for
    /// an Online or Hybrid meeting (BR-163).
    /// </summary>
    public string? OnlineLink { get; set; }

    public DateTimeOffset PlannedStartTime { get; set; }

    /// <summary>Always after <see cref="PlannedStartTime"/> (BR-62).</summary>
    public DateTimeOffset PlannedEndTime { get; set; }

    /// <summary>Written by the system when the Chair starts the meeting (UC-21).</summary>
    public DateTimeOffset? ActualStartedAt { get; set; }

    /// <summary>Written by the system when the Chair ends the meeting (UC-22).</summary>
    public DateTimeOffset? ActualEndedAt { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>The active approver assignment for this meeting's current approval cycle.</summary>
    public Guid? AssignedApproverId { get; set; }
    public User? AssignedApprover { get; set; }

    public ICollection<MeetingParticipant> Participants { get; set; } = [];
    public ICollection<MeetingStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<AgendaItem> AgendaItems { get; set; } = [];
    public ICollection<MeetingDocument> Documents { get; set; } = [];
    public ICollection<VotingSession> VotingSessions { get; set; } = [];
    public ICollection<SpeakingRequest> SpeakingRequests { get; set; } = [];
    public ICollection<MeetingNote> Notes { get; set; } = [];
    public ICollection<Recording> Recordings { get; set; } = [];
    public ICollection<MeetingMinutes> Minutes { get; set; } = [];

    /// <summary>At most one, but modelled as a reference so a missing summary is simply null.</summary>
    public MeetingSummary? Summary { get; set; }
}
