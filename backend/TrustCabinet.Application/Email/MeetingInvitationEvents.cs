using System.Text.Json;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Email;

/// <param name="Reason">Why the meeting was cancelled; only on a <see cref="InvitationEventKind.Cancelled"/> event (BR-300).</param>
public sealed record InvitationPayload(string Name, string Email, string MeetingTitle, string Description,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Place, string Mode, string? Role, string? Reason = null);

/// <summary>
/// Decides which invitation dispatches and in-app notices a roster or meeting change queues, and what they say. They are
/// staged in the caller's unit of work and written by its save.
/// </summary>
public sealed class MeetingInvitationEvents(
    IMeetingInvitationDispatchRepository dispatches,
    INotificationRepository notifications) : IMeetingInvitationStager
{
    public static string InitialKey(MeetingParticipant p) => p.InitialInvitationDeliveryKey ?? $"meeting:{p.MeetingId}:published:{p.UserId}";

    public async Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var participant in MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants))
            Stage(meeting, participant, null, InvitationEventKind.Invitation, now, notify: false);
        foreach (var guest in await dispatches.ListActiveGuestInvitationsAsync(meeting.Id, ct))
            Stage(meeting, null, guest, InvitationEventKind.Invitation, now, notify: false);
    }

    public async Task StageCancellationAsync(
        Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default)
    {
        // The organizer who cancelled needs no bell for it, but still gets the email like every invitee.
        foreach (var participant in MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants))
            Stage(meeting, participant, null, InvitationEventKind.Cancelled, now,
                notify: participant.UserId != actorId, historyId, reason);
        foreach (var guest in await dispatches.ListActiveGuestInvitationsAsync(meeting.Id, ct))
            Stage(meeting, null, guest, InvitationEventKind.Cancelled, now,
                notify: false, historyId, reason);
    }

    public async Task StageGuestRescheduleInvitationsAsync(Meeting meeting, Guid historyId, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var guest in await dispatches.ListActiveGuestInvitationsAsync(meeting.Id, ct))
            Stage(meeting, null, guest, InvitationEventKind.Rescheduled, now, notify: false, historyId);
    }

    public void Stage(Meeting meeting, MeetingParticipant? p, MeetingGuestInvitation? g,
        InvitationEventKind kind, DateTimeOffset now, bool notify, Guid? sourceEventId = null, string? reason = null)
    {
        var eventId = sourceEventId ?? Guid.CreateVersion7();
        var ownerId = p?.Id ?? g!.Id;
        var key = kind == InvitationEventKind.Invitation ? p is not null ? InitialKey(p) : g!.InitialInvitationDeliveryKey
            : $"roster:{eventId}:{kind}:{ownerId}";
        var link = meeting.OnlineLink ?? "Phòng họp trực tuyến TrustCabinet";
        // A hybrid meeting may have no room yet: then it happens at its link alone.
        var place = meeting.Mode switch
        {
            MeetingMode.Online => link,
            MeetingMode.Hybrid when meeting.Location.Length > 0 => $"{meeting.Location} · {link}",
            MeetingMode.Hybrid => link,
            _ => meeting.Location,
        };
        dispatches.Add(new()
        {
            EventId = eventId,
            MeetingId = meeting.Id,
            ParticipantId = p?.Id,
            GuestInvitationId = g?.Id,
            EventKind = kind,
            DeduplicationKey = key,
            CreatedAt = now,
            UpdatedAt = now,
            PayloadJson = JsonSerializer.Serialize(new InvitationPayload(p?.User.FullName ?? g!.InvitedName, p?.User.Email ?? g!.Email,
                meeting.Title, meeting.Description, meeting.PlannedStartTime, meeting.PlannedEndTime, place,
                meeting.Mode.ToString(), p?.Role.ToString(), reason))
        });
        if (notify && p is not null)
        {
            var removed = kind == InvitationEventKind.Removed;
            notifications.Add(new()
            {
                UserId = p.UserId,
                MeetingId = meeting.Id,
                DeduplicationKey = "inapp:" + key,
                Type = kind switch
                {
                    InvitationEventKind.Invitation => NotificationType.MeetingInvited,
                    InvitationEventKind.RoleChanged => NotificationType.MeetingRoleChanged,
                    InvitationEventKind.Rescheduled => NotificationType.MeetingRescheduled,
                    InvitationEventKind.Cancelled => NotificationType.MeetingCancelled,
                    _ => NotificationType.MeetingParticipantRemoved
                },
                Title = Title(kind),
                Body = kind switch
                {
                    InvitationEventKind.Cancelled => Limit($"Cuộc họp “{meeting.Title}” đã bị huỷ. Lý do: {reason}", 2000),
                    // Say the new role, not just that it changed (SCRUM-147).
                    InvitationEventKind.RoleChanged => $"Cuộc họp “{meeting.Title}”: bạn đã được giao làm {RoleLabel(p.Role.ToString())}.",
                    _ => $"Cuộc họp “{meeting.Title}”: {Title(kind).ToLowerInvariant()}.",
                },
                TargetType = removed ? null : "Meeting",
                TargetId = removed ? null : meeting.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
    public static string Title(InvitationEventKind kind) => kind switch
    {
        InvitationEventKind.Invitation => "Lời mời tham dự cuộc họp",
        InvitationEventKind.RoleChanged => "Vai trò cuộc họp đã thay đổi",
        InvitationEventKind.Rescheduled => "Cuộc họp đã dời lịch",
        InvitationEventKind.Cancelled => "Cuộc họp đã bị huỷ",
        _ => "Lời mời tham dự đã được thu hồi"
    };

    /// <summary>A meeting role as people read it; no role is a guest.</summary>
    public static string RoleLabel(string? role) => role switch
    {
        "Chair" => "Chủ toạ",
        "Secretary" => "Thư ký",
        "Member" => "Thành viên",
        _ => "Khách",
    };

    /// <summary>
    /// A withdrawal tells someone a meeting they were invited to is off for them, so it carries no entry link and is
    /// worth sending only if the invitation itself went out.
    /// </summary>
    public static bool IsWithdrawal(InvitationEventKind kind)
        => kind is InvitationEventKind.Removed or InvitationEventKind.Cancelled;

    private static string Limit(string value, int maximum)
        => value.Length <= maximum ? value : value[..(char.IsHighSurrogate(value[maximum - 1]) ? maximum - 1 : maximum)];
}
