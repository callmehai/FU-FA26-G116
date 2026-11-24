using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Email;

/// <summary>
/// Stages the invitation dispatches (and the in-app notices that go with them) for a roster or meeting change. Runs
/// inside the caller's unit of work; the caller saves, so they are written with the change itself.
/// </summary>
public interface IMeetingInvitationStager
{
    /// <param name="notify">Whether to also queue the in-app notice for the participant.</param>
    /// <param name="sourceEventId">The event the dispatch belongs to; a new one when null.</param>
    /// <param name="reason">Why the meeting was cancelled; only on a cancellation (UC-22).</param>
    /// <param name="previous">The time a reschedule moved the meeting from; only on a reschedule.</param>
    void Stage(Meeting meeting, MeetingParticipant? participant, MeetingGuestInvitation? guest,
        InvitationEventKind kind, DateTimeOffset now, bool notify, Guid? sourceEventId = null, string? reason = null,
        (DateTimeOffset Start, DateTimeOffset End)? previous = null);

    /// <summary>Invites every participant of the published meeting, and every Guest whose invitation stands.</summary>
    Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Emails every invitee and Guest of a Published meeting that it was cancelled, with the reason (UC-22).</summary>
    Task StageCancellationAsync(
        Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Emails every invitee and Guest of a Published meeting its new time (PO 15/11/2026: internal invitees too, not
    /// only the bell).
    /// </summary>
    /// <param name="previous">The planned start and end before this reschedule, which the email shows struck through.</param>
    Task StageRescheduleInvitationsAsync(Meeting meeting, Guid historyId, (DateTimeOffset Start, DateTimeOffset End) previous,
        DateTimeOffset now, CancellationToken ct = default);
}
