using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

/// <summary>What <see cref="MeetingReminderEmails"/> reads, untracked: the meetings about to start and their people.</summary>
public interface IMeetingReminderQuery
{
    /// <summary>The Published meetings planned to start after <paramref name="now"/> and no later than <paramref name="cutoff"/>.</summary>
    Task<IReadOnlyList<ReminderMeeting>> ListStartingAsync(DateTimeOffset now, DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>The active participants of these meetings whose accounts are active.</summary>
    Task<IReadOnlyList<ReminderParticipant>> ListParticipantsAsync(Guid[] meetingIds, CancellationToken ct = default);

    /// <summary>The Guests of these meetings whose invitations stand.</summary>
    Task<IReadOnlyList<ReminderGuest>> ListGuestsAsync(Guid[] meetingIds, CancellationToken ct = default);

    /// <summary>Every first invitation staged for these meetings, queued or sent, with when it was staged.</summary>
    Task<IReadOnlyList<ReminderInvitation>> ListInvitationsAsync(Guid[] meetingIds, CancellationToken ct = default);

    /// <summary>The keys of the reminder emails already queued for these meetings (<see cref="MeetingReminderEmails.KeyPrefix"/>).</summary>
    Task<IReadOnlyList<string>> ListReminderKeysAsync(Guid[] meetingIds, CancellationToken ct = default);
}

/// <summary>A meeting about to start, untracked, with who organizes it.</summary>
public sealed record ReminderMeeting(Meeting Meeting, string OrganizerName, string OrganizerEmail);

public sealed record ReminderParticipant(Guid Id, Guid MeetingId, MeetingRole Role, string FullName, string Email);

public sealed record ReminderGuest(Guid Id, Guid MeetingId, string InvitedName, string Email);

/// <summary>An invitation dispatch: whose (participant or Guest invitation id) and when it was staged.</summary>
public sealed record ReminderInvitation(Guid MeetingId, Guid? Owner, DateTimeOffset CreatedAt);
