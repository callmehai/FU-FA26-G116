using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

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

    /// <summary>
    /// ADR-0041 — the Secretary switched the room mic on (a take opened), or a take was saved: appended to the meeting's
    /// room recording, kept as the first one, or dropped because the mic heard nothing. Who closed it is the actor; the
    /// server closing a take left idle has none.
    /// </summary>
    public const string RecordingTakeStarted = "recording.take_started";
    public const string RecordingTakeSaved = "recording.take_saved";

    /// <summary>A take erased from a room recording; the details say which, how long, and how many lines went with it.</summary>
    public const string RecordingTakeDeleted = "recording.take_deleted";

    /// <summary>SCRUM-35 — a person agreed to Voice ID; the notice version and purpose they agreed to are in the details.</summary>
    public const string VoiceConsentGranted = "voice.consent_granted";

    /// <summary>A person agreed to a newer Voice ID notice while consenting already; the details name both versions.</summary>
    public const string VoiceConsentRenewed = "voice.consent_renewed";

    /// <summary>A person withdrew Voice ID; the details say how many samples were erased with it.</summary>
    public const string VoiceConsentWithdrawn = "voice.consent_withdrawn";

    public const string VoiceSampleAdded = "voice.sample_added";
    public const string VoiceSampleDeleted = "voice.sample_deleted";
    public const string VoiceConsentObjectType = "VoiceConsent";
    public const string VoiceSampleObjectType = "VoiceSample";

    /// <summary>A secretary said who a voice in a recording is — or cleared it. The machine's suggestion travels along.</summary>
    public const string TranscriptSpeakerAssigned = "transcript.speaker_assigned";
    public const string TranscriptSegmentSpeakerAssigned = "transcript.segment_speaker_assigned";
    public const string TranscriptSpeakerObjectType = "TranscriptSpeaker";

    /// <summary>SCRUM-109 — the Secretary corrected the machine's text of one per-mic sentence.</summary>
    public const string SpeechClipEdited = "speech_clip.edited";

    /// <summary>A per-mic sentence was taken out — by its speaker or the Secretary (the details say which).</summary>
    public const string SpeechClipDeleted = "speech_clip.deleted";

    /// <summary>ADR-0056 — the administrator changed where AI drafts are written. Never carries the API key.</summary>
    public const string AiSettingsUpdated = "ai_settings.updated";

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

    /// <summary>
    /// SCRUM-28 — the meeting ended with a vote still a draft, so the system deleted it (PO 08/11/2026). No actor: the
    /// system did it; the Chair who ended the meeting, the meeting and what the draft said travel in the details,
    /// because the row they describe is gone by the time anyone reads this one.
    /// </summary>
    public const string VoteDraftDeletedOnMeetingEnd = "vote.draft_deleted_on_meeting_end";

    /// <summary>What <c>AuditLogEntry.ObjectType</c> carries for the action above; the <c>ObjectId</c> is the vote's id.</summary>
    public const string VoteObjectType = "VotingSession";

    /// <summary>
    /// UC-32c — a document of a Secret meeting was opened in the viewer (BR-318). There is no "downloaded" twin: a
    /// Secret meeting's documents cannot be downloaded (BR-188), and the other levels are not audited (ADR-0057).
    /// </summary>
    public const string DocumentViewed = "document.viewed";

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

    /// <summary>
    /// SCRUM-59 — a verified guest was given a room token after confirming the recording notice. There is no actor
    /// (guests are not users); the <c>ObjectId</c> is the guest session, the details carry the meeting, invitation,
    /// self-declared name and the notice, and the row's IP is the guest's. The visit itself is in <c>call_participants</c>.
    /// </summary>
    public const string CallGuestAdmitted = "call.guest_admitted";

    public const string GuestSessionObjectType = "GuestSession";

    /// <summary>
    /// BR-444 — a password or Google sign-in succeeded. The actor and the object are the account; the details carry the
    /// method, never the password or a token.
    /// </summary>
    public const string SignedIn = "auth.signed_in";

    /// <summary>
    /// BR-444 — a sign-in to an existing account was refused: wrong password, or the account is deactivated. The actor
    /// is the account the attempt was made in the name of, so filtering by that account shows its failed attempts too;
    /// whoever typed the password is not known. An email matching no account is not recorded — it may be a password
    /// typed into the wrong box.
    /// </summary>
    public const string SignInFailed = "auth.sign_in_failed";

    /// <summary>BR-444 — a Guest typed the right one-time code (UC-04). No actor; the invitation is in the details.</summary>
    public const string GuestVerified = "guest.verified";

    /// <summary>BR-446 — the Chair approved minutes, which issues them (BR-408); a correction is issued the same way.</summary>
    public const string MinutesIssued = "minutes.issued";

    public const string MinutesObjectType = "MeetingMinutes";

    /// <summary>The Secretary saved attendance (UC-38); every changed entry, before and after, is in the details.</summary>
    public const string MeetingAttendanceRecorded = "meeting.attendance.recorded";

    /// <summary>
    /// A participant change, followed by the <c>ParticipantOperation</c> name as written since SCRUM-123
    /// ("meeting.participants.AddGuest"). Kept PascalCase: rows already stored carry it.
    /// </summary>
    public static string MeetingParticipants(ParticipantOperation operation) => "meeting.participants." + operation;

    /// <summary>
    /// BR-445 — the code of the audit row written with each <c>MeetingStatusHistory</c> row, one per lifecycle action.
    /// </summary>
    public static string MeetingHistory(MeetingHistoryAction action) => action switch
    {
        MeetingHistoryAction.Create => "meeting.created",
        MeetingHistoryAction.SubmitForApproval => "meeting.submitted_for_approval",
        MeetingHistoryAction.Approve => "meeting.approved",
        MeetingHistoryAction.Reject => "meeting.rejected",
        MeetingHistoryAction.ReturnToDraft => "meeting.returned_to_draft",
        MeetingHistoryAction.Publish => "meeting.published",
        MeetingHistoryAction.TransferOrganizer => "meeting.organizer_transferred",
        MeetingHistoryAction.Reschedule => "meeting.rescheduled",
        MeetingHistoryAction.Start => "meeting.started",
        MeetingHistoryAction.End => "meeting.ended",
        MeetingHistoryAction.Cancel => "meeting.cancelled",
        MeetingHistoryAction.AutoCancel => "meeting.auto_cancelled",
        _ => "meeting.status_changed",
    };

    /// <summary>
    /// SCRUM-142 — every step of a calendar share (BR-426). The <c>ObjectId</c> is the share; the details name both people,
    /// the level and the range, before and after for a change, and the reason for a stop. A system close has no actor.
    /// </summary>
    public const string CalendarShareRequested = "calendar_share.requested";
    public const string CalendarShareShared = "calendar_share.shared";
    public const string CalendarShareAccepted = "calendar_share.accepted";
    public const string CalendarShareDeclined = "calendar_share.declined";
    public const string CalendarShareChangeRequested = "calendar_share.change_requested";
    public const string CalendarShareChangeAccepted = "calendar_share.change_accepted";
    public const string CalendarShareChangeDeclined = "calendar_share.change_declined";
    public const string CalendarShareUpdated = "calendar_share.updated";
    public const string CalendarShareWithdrawn = "calendar_share.withdrawn";
    public const string CalendarShareChangeWithdrawn = "calendar_share.change_withdrawn";
    public const string CalendarShareEnded = "calendar_share.ended";
    public const string CalendarShareStopped = "calendar_share.stopped";
    /// <summary>A share past its end date (BR-425) closed when a new share of the same pair was sent. No notification.</summary>
    public const string CalendarShareClosedAfterEndDate = "calendar_share.closed_after_end_date";
    public const string CalendarShareObjectType = "CalendarShare";

    /// <summary>
    /// SCRUM-38, UC-70b — a user exported the meeting report to Excel (BR-438). No object id: the report is not a row.
    /// The details carry the period; viewing the report on screen is not recorded.
    /// </summary>
    public const string ReportExported = "report.exported";

    public const string ReportObjectType = "Report";
}
