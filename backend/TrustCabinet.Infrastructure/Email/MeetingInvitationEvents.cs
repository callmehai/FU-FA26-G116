using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

/// <param name="Reason">Why the meeting was cancelled; only on a <see cref="InvitationEventKind.Cancelled"/> event (BR-300).</param>
public sealed record InvitationPayload(string Name, string Email, string MeetingTitle, string Description,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Place, string Mode, string? Role, string? Reason = null);

public static class MeetingInvitationEvents
{
    public static string InitialKey(MeetingParticipant p) => p.InitialInvitationDeliveryKey ?? $"meeting:{p.MeetingId}:published:{p.UserId}";

    public static void Stage(TrustCabinetDbContext db, Meeting meeting, MeetingParticipant? p, MeetingGuestInvitation? g,
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
        db.MeetingInvitationDispatches.Add(new()
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
            db.Notifications.Add(new()
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
                Body = kind == InvitationEventKind.Cancelled
                    ? Limit($"Cuộc họp “{meeting.Title}” đã bị huỷ. Lý do: {reason}", 2000)
                    : $"Cuộc họp “{meeting.Title}”: {Title(kind).ToLowerInvariant()}.",
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

    /// <summary>
    /// A withdrawal tells someone a meeting they were invited to is off for them, so it carries no entry link and is
    /// worth sending only if the invitation itself went out.
    /// </summary>
    public static bool IsWithdrawal(InvitationEventKind kind)
        => kind is InvitationEventKind.Removed or InvitationEventKind.Cancelled;

    private static string Limit(string value, int maximum)
        => value.Length <= maximum ? value : value[..(char.IsHighSurrogate(value[maximum - 1]) ? maximum - 1 : maximum)];
}
