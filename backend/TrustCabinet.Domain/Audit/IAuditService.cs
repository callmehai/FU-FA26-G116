namespace TrustCabinet.Domain.Audit;

/// <summary>
/// The <c>:Audit Service</c> participant of UC-06a, UC-06b and UC-06c — writes the business evidence
/// that something was done and by whom.
///
/// Not application logging. <c>AuditLogEntry</c> says it: this is append-only evidence, kept
/// because "who deactivated this person, and when?" has to be answerable months later, while
/// technical logs get rotated away and nobody treats them as authoritative.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records one action. <paramref name="details"/> is free-form JSON-serialisable data kept
    /// alongside the row — what changed, typically.
    ///
    /// Never put a password, a hash or a token in it: the audit log is read by more people than
    /// the users table, and it is deliberately never deleted.
    /// </summary>
    Task RecordAsync(
        string action,
        string objectType,
        Guid? objectId,
        Guid? actorId,
        object? details = null,
        CancellationToken ct = default);
}

/// <summary>
/// The action codes this story writes. Constants rather than literals scattered across the
/// service, because a typo in one of them produces a row that no later query finds.
/// </summary>
public static class AuditActions
{
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";

    /// <summary>
    /// A Google identity was linked to an account at its first Google sign-in (ADR-0009). The
    /// actor is the account itself: nobody else was involved.
    /// </summary>
    public const string UserGoogleLinked = "user.google_linked";

    /// <summary>UC-07 — an administrator had a reset link emailed to the account.</summary>
    public const string UserPasswordResetRequested = "user.password_reset_requested";

    /// <summary>
    /// UC-07's link was used and the password changed. The actor is the account itself — the
    /// administrator only sent the link, they never learn or choose the password.
    /// </summary>
    public const string UserPasswordReset = "user.password_reset";

    /// <summary>What <c>AuditLogEntry.ObjectType</c> carries for all of the above.</summary>
    public const string UserObjectType = "User";

    /// <summary>
    /// UC-11 — an administrator changed what a role may do (SCRUM-60). <c>AuditLogEntry</c> names
    /// "changing a person's rights" as something that has to be recorded, and this changes it for
    /// everyone holding the role at once.
    /// </summary>
    public const string RolePermissionsUpdated = "role.permissions.updated";

    /// <summary>UC-12a — an administrator created a role (SCRUM-63).</summary>
    public const string RoleCreated = "role.created";

    /// <summary>UC-12b — an administrator renamed a role or changed its description.</summary>
    public const string RoleUpdated = "role.updated";

    /// <summary>
    /// UC-12c — an administrator deleted a role. Its name and capabilities travel in the details,
    /// because the cascade has removed both from the database by the time anyone reads this row.
    /// </summary>
    public const string RoleDeleted = "role.deleted";

    /// <summary>
    /// What <c>AuditLogEntry.ObjectType</c> carries for every role action above. The row's
    /// <c>ObjectId</c> is the role's id — roles are rows since ADR-0008.
    /// </summary>
    public const string RoleObjectType = "Role";

    /// <summary>SCRUM-29 — a secretary stored a recording, after confirming the participants were told.</summary>
    public const string RecordingUploaded = "recording.uploaded";

    /// <summary>
    /// The audio bytes of a recording were erased — on a secretary's request or because the retention period
    /// ran out (the details say which). The row stays, so the erasure itself can be shown to have happened.
    /// </summary>
    public const string RecordingErased = "recording.erased";

    public const string RecordingObjectType = "Recording";

    /// <summary>SCRUM-35 — a person agreed to Voice ID; the notice version and purpose they agreed to are in the details.</summary>
    public const string VoiceConsentGranted = "voice.consent_granted";

    /// <summary>A person withdrew Voice ID; the details say how many samples were erased with it.</summary>
    public const string VoiceConsentWithdrawn = "voice.consent_withdrawn";

    public const string VoiceSampleAdded = "voice.sample_added";
    public const string VoiceSampleDeleted = "voice.sample_deleted";
    public const string VoiceConsentObjectType = "VoiceConsent";
    public const string VoiceSampleObjectType = "VoiceSample";

    /// <summary>A secretary said who a voice in a recording is — or cleared it. The machine's suggestion travels along.</summary>
    public const string TranscriptSpeakerAssigned = "transcript.speaker_assigned";
    public const string TranscriptSpeakerObjectType = "TranscriptSpeaker";

    /// <summary>SCRUM-109 — the Secretary corrected the machine's text of one per-mic sentence.</summary>
    public const string SpeechClipEdited = "speech_clip.edited";

    /// <summary>A per-mic sentence was taken out — by its speaker or the Secretary (the details say which).</summary>
    public const string SpeechClipDeleted = "speech_clip.deleted";

    /// <summary>The Chair or a Secretary took a per-mic sentence out of the record, or put it back (reason in details).</summary>
    public const string SpeechClipExcluded = "speech_clip.excluded";
    public const string SpeechClipRestored = "speech_clip.restored";

    /// <summary>The Chair or a Secretary saved the per-mic transcript, a recording's transcript, or a recording's file.</summary>
    public const string SpeechClipsExported = "speech_clip.exported";
    public const string TranscriptExported = "transcript.exported";
    public const string RecordingDownloaded = "recording.downloaded";

    /// <summary>A Secretary corrected a transcript segment, or queued a finished transcript to be made again.</summary>
    public const string TranscriptSegmentEdited = "transcript.segment_edited";
    public const string TranscriptRerun = "transcript.rerun";
    public const string SpeechClipObjectType = "SpeechClip";

    public const string MeetingObjectType = "Meeting";

    /// <summary>UC-32c — a document of a Confidential or Secret meeting was opened in the viewer (BR-318).</summary>
    public const string DocumentViewed = "document.viewed";

    /// <summary>UC-32c AF-1 — the same, downloaded. Only a Confidential one gets here: Secret is not downloadable (BR-188).</summary>
    public const string DocumentDownloaded = "document.downloaded";

    /// <summary>
    /// What <c>AuditLogEntry.ObjectType</c> carries for the two above. The <c>ObjectId</c> is the
    /// MeetingDocument, since that is what the reader opened; the meeting goes in the details.
    /// </summary>
    public const string MeetingDocumentObjectType = "MeetingDocument";

    /// <summary>
    /// PO 27/10/2026 — an administrator read a deactivated person's personal library, to recover their documents.
    /// The <c>ObjectId</c> is the owner; the details say which folder was listed or which document was opened.
    /// </summary>
    public const string PersonalLibraryRead = "library.read";
}
