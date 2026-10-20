using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

public sealed record InvitationPayload(string Name, string Email, string MeetingTitle, string Description,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Place, string Mode, string? Role);

public static class MeetingInvitationEvents
{
    public static string InitialKey(MeetingParticipant p) => p.InitialInvitationDeliveryKey ?? $"meeting:{p.MeetingId}:published:{p.UserId}";

    public static void Stage(TrustCabinetDbContext db, Meeting meeting, MeetingParticipant? p, MeetingGuestInvitation? g,
        InvitationEventKind kind, DateTimeOffset now, bool notify, Guid? sourceEventId = null)
    {
        var eventId = sourceEventId ?? Guid.CreateVersion7();
        var ownerId = p?.Id ?? g!.Id;
        var key = kind == InvitationEventKind.Invitation ? p is not null ? InitialKey(p) : g!.InitialInvitationDeliveryKey
            : $"roster:{eventId}:{kind}:{ownerId}";
        var place = meeting.Mode == MeetingMode.Online ? meeting.OnlineLink ?? "Phòng họp trực tuyến TrustCabinet"
            : meeting.Mode == MeetingMode.Hybrid ? $"{meeting.Location} · {meeting.OnlineLink ?? "Phòng họp trực tuyến TrustCabinet"}" : meeting.Location;
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
                meeting.Mode.ToString(), p?.Role.ToString()))
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
                    _ => NotificationType.MeetingParticipantRemoved
                },
                Title = Title(kind),
                Body = $"Cuộc họp “{meeting.Title}”: {Title(kind).ToLowerInvariant()}.",
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
        _ => "Lời mời tham dự đã được thu hồi"
    };
}
