using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingInvitationDispatchProcessor(TrustCabinetDbContext db, IOptions<EmailDeliveryOptions> options,
    TimeProvider clock, IGuestInvitationLinkProvider links, ILogger<MeetingInvitationDispatchProcessor> logger)
{
    public async Task<int> ProcessNextAsync(CancellationToken ct)
    {
        if (!options.Value.Smtp.Enabled) return 0;
        var now = clock.GetUtcNow();
        var ids = await db.MeetingInvitationDispatches.AsNoTracking().Where(d =>
            (d.State == InvitationDispatchState.Pending || d.State == InvitationDispatchState.BlockedDependency)
            && (d.NextAttemptAt == null || d.NextAttemptAt <= now)).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).Select(d => d.Id).Take(50).ToArrayAsync(ct);
        var count = 0;
        foreach (var id in ids) count += await ProcessAsync(id, ct);
        return count;
    }

    public async Task<int> ProcessAsync(Guid id, CancellationToken ct)
    {
        if (!options.Value.Smtp.Enabled) return 0;
        var meetingId = await db.MeetingInvitationDispatches.AsNoTracking().Where(d => d.Id == id).Select(d => (Guid?)d.MeetingId).SingleOrDefaultAsync(ct);
        if (meetingId is null) return 0;
        var attempt = 0;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId.Value} FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
            var locked = await db.MeetingInvitationDispatches.FromSqlInterpolated($"SELECT * FROM meeting_invitation_dispatches WHERE id = {id} FOR UPDATE SKIP LOCKED")
                .AsNoTracking().ToArrayAsync(ct);
            if (locked.Length == 0) return 0;
            var dispatch = await db.MeetingInvitationDispatches.SingleAsync(d => d.Id == id, ct);
            await db.Entry(dispatch).ReloadAsync(ct);
            attempt = dispatch.AttemptCount;
            var now = clock.GetUtcNow();
            if (dispatch.State is not (InvitationDispatchState.Pending or InvitationDispatchState.BlockedDependency) || dispatch.NextAttemptAt > now) return 0;
            var existing = await db.EmailMessages.Where(e => e.DeduplicationKey == dispatch.DeduplicationKey).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);
            if (existing is { } existingId)
            {
                dispatch.State = InvitationDispatchState.Materialized; dispatch.EmailMessageId = existingId;
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return 0;
            }
            var status = await db.Meetings.Where(m => m.Id == meetingId).Select(m => m.Status).SingleAsync(ct);
            var current = dispatch.ParticipantId is { } participantId
                ? await db.MeetingParticipants.AnyAsync(p => p.Id == participantId && p.AssignmentStatus == AssignmentStatus.Current
                    && p.Role != MeetingRole.Guest && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId == null && p.User.IsActive, ct)
                : await db.MeetingGuestInvitations.AnyAsync(g => g.Id == dispatch.GuestInvitationId && g.RevokedAt == null, ct);
            if (dispatch.EventKind != InvitationEventKind.Removed && (!current || status == MeetingStatus.Ended))
            {
                dispatch.State = InvitationDispatchState.Suppressed; dispatch.UpdatedAt = now;
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return 0;
            }
            var link = options.Value.ApplicationBaseUrl.TrimEnd('/') + $"/mgmt/meetings/{meetingId}";
            if (dispatch.GuestInvitationId is { } guestId && dispatch.EventKind != InvitationEventKind.Removed)
            {
                var result = await links.PrepareAsync(guestId, ct);
                if (result.Status != GuestLinkStatus.Ready)
                {
                    dispatch.State = result.Status == GuestLinkStatus.Unavailable ? InvitationDispatchState.BlockedDependency : InvitationDispatchState.Suppressed;
                    dispatch.NextAttemptAt = now.AddSeconds(60); dispatch.LastError = result.Status.ToString(); dispatch.UpdatedAt = now;
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return 0;
                }
                if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                    || uri.GetLeftPart(UriPartial.Authority) != new Uri(options.Value.ApplicationBaseUrl).GetLeftPart(UriPartial.Authority))
                    throw new JsonException("Guest verification URL must use the configured application origin.");
                link = result.Url!;
            }
            if (dispatch.PayloadVersion != 1) throw new JsonException("Unsupported invitation payload version.");
            var payload = JsonSerializer.Deserialize<InvitationPayload>(dispatch.PayloadJson) ?? throw new JsonException("Missing invitation payload.");
            var removal = dispatch.EventKind == InvitationEventKind.Removed;
            var subject = $"{MeetingInvitationEvents.Title(dispatch.EventKind)}: {payload.MeetingTitle}".Replace('\r', ' ').Replace('\n', ' ');
            if (subject.Length > 300) subject = subject[..(char.IsHighSurrogate(subject[299]) ? 299 : 300)];
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            var body = $"Xin chào {payload.Name},\n\n{MeetingInvitationEvents.Title(dispatch.EventKind)}: \"{payload.MeetingTitle}\".\n";
            if (!removal)
            {
                body += $"\nBắt đầu: {TimeZoneInfo.ConvertTime(payload.StartsAt, zone):dd/MM/yyyy HH:mm zzz}\nKết thúc: {TimeZoneInfo.ConvertTime(payload.EndsAt, zone):dd/MM/yyyy HH:mm zzz}\n";
                var role = payload.Role switch { "Chair" => "Chủ tọa", "Secretary" => "Thư ký", "Member" => "Thành viên", _ => "Khách" };
                var permissions = payload.Permissions.Select(key => MeetingPermissionCatalog.All.SingleOrDefault(p => p.Code == key)?.Name ?? key);
                body += $"Vai trò: {role}\nQuyền bổ sung: {string.Join(", ", permissions)}\n";
                // Guests must verify email before learning room-entry links.
                if (dispatch.GuestInvitationId is null) body += $"Địa điểm: {payload.Place}\n";
                body += dispatch.GuestInvitationId is null ? $"\nXem cuộc họp: {link}\n" : $"\nXác minh email để truy cập cuộc họp: {link}\n";
            }
            var email = new EmailMessage
            {
                MeetingId = meetingId,
                DeduplicationKey = dispatch.DeduplicationKey,
                ToAddress = payload.Email,
                Subject = subject,
                Body = body,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (dispatch.EventKind == InvitationEventKind.Invitation)
                email.CalendarAttachment = CalendarInvitationBuilder.Build(meetingId.Value, payload.MeetingTitle,
                    dispatch.GuestInvitationId is null ? payload.Description : "", dispatch.GuestInvitationId is null ? payload.Place : "",
                    payload.StartsAt, payload.EndsAt, payload.Name, payload.Email, link, options.Value.Smtp.FromName, options.Value.Smtp.FromAddress, now);
            db.EmailMessages.Add(email);
            dispatch.EmailMessageId = email.Id; dispatch.State = InvitationDispatchState.Materialized;
            dispatch.LastError = ""; dispatch.NextAttemptAt = null; dispatch.UpdatedAt = now;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return 1;
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or TimeoutException or JsonException)
        {
            db.ChangeTracker.Clear();
            // Never log payloads or exception messages from a credential provider.
            logger.LogWarning("Invitation materialization failed for {DispatchId}; failure type {FailureType}", id, ex.GetType().Name);
            var now = clock.GetUtcNow();
            var retryAt = now.AddSeconds(Math.Min(900, options.Value.RetryDelaySeconds * Math.Pow(2, Math.Min(attempt, 10))));
            await db.MeetingInvitationDispatches.Where(d => d.Id == id && (d.State == InvitationDispatchState.Pending || d.State == InvitationDispatchState.BlockedDependency))
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.AttemptCount, d => d.AttemptCount + 1)
                    .SetProperty(d => d.State, d => d.AttemptCount + 1 >= options.Value.MaxAttempts ? InvitationDispatchState.Failed : InvitationDispatchState.Pending)
                    .SetProperty(d => d.NextAttemptAt, retryAt)
                    .SetProperty(d => d.LastError, ex.GetType().Name).SetProperty(d => d.UpdatedAt, now), ct);
            return 0;
        }
    }
}
