namespace TrustCabinet.Application.Retention;

/// <summary>
/// How long each kind of leftover is kept before the retention job removes it (ADR-0067). Bound and validated by the
/// infrastructure project, which reads configuration.
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>How often the job runs. Every rule is about hours or days, so an hour is plenty.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// An audio file nothing points at is deleted only once it has not been written for this long: an upload, a take
    /// being saved or a mix being built writes its file before the row that names it commits.
    /// </summary>
    public int OrphanAudioGraceHours { get; set; } = 24;

    /// <summary>A notification already read is deleted this many days after it was created.</summary>
    public int ReadNotificationDays { get; set; } = 90;

    /// <summary>Any notification, read or not, is deleted this many days after it was created.</summary>
    public int NotificationDays { get; set; } = 180;

    /// <summary>A guest's verification code is deleted this many days after it expired; by then it no longer counts
    /// towards the daily limit either (BR-153).</summary>
    public int GuestChallengeDays { get; set; } = 1;

    /// <summary>The body, HTML and calendar attachment of an email sent or given up on are cleared after this many days.</summary>
    public int EmailBodyDays { get; set; } = 30;
}
