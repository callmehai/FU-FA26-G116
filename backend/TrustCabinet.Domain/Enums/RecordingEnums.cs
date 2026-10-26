namespace TrustCabinet.Domain.Enums;

/// <summary>Where a recording came from.</summary>
public enum RecordingSource
{
    /// <summary>Captured in the browser during the sitting.</summary>
    BrowserCapture,
    /// <summary>A file recorded elsewhere and uploaded afterwards.</summary>
    Upload,
    /// <summary>Mixed audio from the in-app call room (SCRUM-44).</summary>
    CallRoom,
}

public enum RecordingStatus
{
    /// <summary>Capture or upload in progress.</summary>
    Pending,
    Available,
    Failed,
    /// <summary>Erased on request or past its retention date — the row stays as evidence that it existed.</summary>
    Deleted,
}

/// <summary>Speech-to-text runs in the background, so its state is visible (SCRUM-31).</summary>
public enum TranscriptStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

/// <summary>
/// The minutes have their own approval flow, separate from the meeting's (SCRUM-33).
/// </summary>
public enum MinutesStatus
{
    Draft,
    WaitingApproval,
    Rejected,
    /// <summary>Approved and locked. Corrections require a new revision, never an edit.</summary>
    Issued,
    /// <summary>Replaced by a later revision.</summary>
    Superseded,
}

/// <summary>
/// How a meeting's speech is captured (SCRUM-109), chosen before it starts and fixed once it has.
/// The same choice AI-eCabinet offers, with the attendee's own mic as the default.
/// </summary>
public enum CaptureMode
{
    /// <summary>
    /// Each attendee's browser captures their own microphone and sends it a sentence at a time, so
    /// every line of the transcript belongs to the owner of the mic that heard it.
    /// </summary>
    PerMic,
    /// <summary>One recording of the whole room, uploaded or captured by the Secretary (SCRUM-29/31/35).</summary>
    Room,
}
