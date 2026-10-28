namespace TrustCabinet.Domain.Enums;

/// <summary>Where a recording came from.</summary>
public enum RecordingSource
{
    /// <summary>
    /// The room's mic, streamed from the Secretary's browser in parts while the sitting runs (ADR-0041). Each take is
    /// appended to the meeting's one room recording once the mic goes off.
    /// </summary>
    BrowserCapture,
    /// <summary>A file recorded elsewhere and uploaded afterwards.</summary>
    Upload,
    /// <summary>Mixed audio from the in-app call room (SCRUM-44).</summary>
    CallRoom,
}

public enum RecordingStatus
{
    /// <summary>A room-mic take still being streamed; nobody hears it before the mic goes off.</summary>
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
