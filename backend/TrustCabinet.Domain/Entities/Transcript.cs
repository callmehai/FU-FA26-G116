using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// The speech-to-text result for one recording (SCRUM-31). Runs in the background, so its state
/// and failure reason are stored rather than being a transient detail of one request.
/// </summary>
public class Transcript : Entity
{
    public Guid RecordingId { get; set; }
    public Recording Recording { get; set; } = null!;

    public TranscriptStatus Status { get; set; } = TranscriptStatus.Pending;

    /// <summary>
    /// Which service produced it. Recorded because the choice is still open (SCRUM-31) and because
    /// using an external service means voice data left the system — a fact the team must be able
    /// to state per recording, not just in a design document.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public string Language { get; set; } = "vi";

    /// <summary>0–100 while running, so the UI can show progress instead of an indefinite spinner.</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Why it failed, shown to the user with the option to run it again.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<TranscriptSegment> Segments { get; set; } = [];

    /// <summary>The voices found in the recording and who each one is (SCRUM-35).</summary>
    public ICollection<TranscriptSpeaker> Speakers { get; set; } = [];
}
