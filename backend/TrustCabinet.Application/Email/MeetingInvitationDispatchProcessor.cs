using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

public sealed class MeetingInvitationDispatchProcessor(
    IMeetingInvitationDispatchRepository dispatches,
    IEmailMessageRepository emails,
    IUnitOfWork unitOfWork,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider clock,
    IGuestInvitationLinkProvider links,
    ILogger<MeetingInvitationDispatchProcessor> logger
)
{
    public async Task<int> ProcessNextAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return 0;
        var now = clock.GetUtcNow();
        var ids = await unitOfWork.ExecuteAsync(
            token => dispatches.ListDueIdsAsync(now, 50, token),
            TransactionMode.None,
            ct
        );
        var count = 0;
        foreach (var id in ids)
            count += await ProcessAsync(id, ct);
        return count;
    }

    public async Task<int> ProcessAsync(Guid id, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return 0;
        var attempt = 0;
        try
        {
            var candidate = await unitOfWork.ExecuteAsync(
                token => dispatches.FindAsync(id, token),
                TransactionMode.None,
                ct
            );
            if (
                candidate is null
                || candidate.State
                    is not (
                        InvitationDispatchState.Pending
                        or InvitationDispatchState.BlockedDependency
                    )
                || candidate.NextAttemptAt > clock.GetUtcNow()
            )
                return 0;
            attempt = candidate.AttemptCount;
            Guid? meetingId = candidate.MeetingId;
            if (
                candidate.GuestInvitationId is not null
                && !MeetingInvitationEvents.IsWithdrawal(candidate.EventKind)
                && !await links.IsAvailableAsync(ct)
            )
            {
                // An unavailable integration must not serialize roster mutations on the meeting row.
                var deferredAt = clock.GetUtcNow();
                await unitOfWork.ExecuteAsync(
                    async token =>
                    {
                        if (
                            !await dispatches.IsGuestInvitationActiveAsync(candidate.GuestInvitationId, token)
                            || await dispatches.IsMeetingClosedAsync(meetingId, token)
                        )
                        {
                            await dispatches.SuppressAsync(id, deferredAt, token);
                            return;
                        }
                        var next = DependencyRetryAt(deferredAt, candidate.DependencyAttemptCount);
                        await dispatches.BlockOnDependencyAsync(id, next, deferredAt, token);
                    },
                    TransactionMode.None,
                    ct
                );
                return 0;
            }
            // A way out that wrote nothing (another worker holds the dispatch, or it is no longer due) ends the
            // transaction without committing, as before; every other way out has saved, and commits.
            var outcome = await unitOfWork.ExecuteAsync(
                token => MaterializeAsync(id, meetingId.Value, locked => attempt = locked, token),
                result => result.Commit,
                TransactionMode.ReadCommitted,
                ct
            );
            return outcome.Count;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            unitOfWork.DiscardChanges();
            // The failure as it happened, not the storage layer's translation of it (rule R8).
            var failure = ex is PersistenceException { InnerException: { } inner } ? inner : ex;
            // Never log payloads or exception messages from a credential provider.
            logger.LogWarning(
                "Invitation materialization failed for {DispatchId}; failure type {FailureType}",
                id,
                failure.GetType().Name
            );
            var now = clock.GetUtcNow();
            var retryAt = now.AddSeconds(
                Math.Min(900, options.Value.RetryDelaySeconds * Math.Pow(2, Math.Min(attempt, 10)))
            );
            await unitOfWork.ExecuteAsync(
                token => dispatches.RecordFailedAttemptAsync(
                    id,
                    options.Value.MaxAttempts,
                    retryAt,
                    failure.GetType().Name,
                    now,
                    token
                ),
                TransactionMode.None,
                ct
            );
            return 0;
        }
    }

    /// <summary>How a locked dispatch was settled: emails queued, and whether its transaction commits.</summary>
    private readonly record struct Materialization(int Count, bool Commit);

    private static readonly Materialization Skipped = new(0, Commit: false);
    private static readonly Materialization Settled = new(0, Commit: true);

    /// <summary>
    /// Runs inside the dispatch's transaction, under the meeting and dispatch locks. <paramref name="onLocked"/>
    /// receives the attempt count read under the lock, for the retry delay if this throws.
    /// </summary>
    private async Task<Materialization> MaterializeAsync(
        Guid id,
        Guid meetingId,
        Action<int> onLocked,
        CancellationToken ct
    )
    {
        var dispatch = await dispatches.LockAsync(meetingId, id, ct);
        if (dispatch is null)
            return Skipped;
        onLocked(dispatch.AttemptCount);
        var now = clock.GetUtcNow();
        if (
            dispatch.State
                is not (
                    InvitationDispatchState.Pending
                    or InvitationDispatchState.BlockedDependency
                )
            || dispatch.NextAttemptAt > now
        )
            return Skipped;
        var existing = await emails.FindIdByDeduplicationKeyAsync(dispatch.DeduplicationKey, ct);
        if (existing is { } existingId)
        {
            dispatch.State = InvitationDispatchState.Materialized;
            dispatch.EmailMessageId = existingId;
            await unitOfWork.SaveChangesAsync(ct);
            return Settled;
        }
        var status = await dispatches.GetMeetingStatusAsync(meetingId, ct);
        var current = dispatch.ParticipantId is { } participantId
            ? await dispatches.IsParticipantActiveAsync(participantId, ct)
            : await dispatches.IsGuestInvitationActiveAsync(dispatch.GuestInvitationId, ct);
        // An invitation, role change or new time for a meeting that has ended or been called off is moot. The
        // cancellation notice itself still goes to everyone who is on the roster.
        var moot = dispatch.EventKind switch
        {
            InvitationEventKind.Removed => false,
            InvitationEventKind.Cancelled => !current,
            _ => !current || status is MeetingStatus.Ended or MeetingStatus.Cancelled,
        };
        if (moot)
        {
            dispatch.State = InvitationDispatchState.Suppressed;
            dispatch.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(ct);
            return Settled;
        }
        // A withdrawal is meaningful only after this assignment's invitation became an email.
        // Match the delivery key too: pre-migration invitations may have no dispatch row.
        if (MeetingInvitationEvents.IsWithdrawal(dispatch.EventKind))
        {
            var initialKey = dispatch.ParticipantId is { } ownerId
                ? await dispatches.GetParticipantInitialKeyAsync(ownerId, ct)
                : await dispatches.GetGuestInitialKeyAsync(dispatch.GuestInvitationId, ct);
            var materialized =
                await dispatches.HasMaterializedInvitationAsync(
                    meetingId,
                    dispatch.ParticipantId,
                    dispatch.GuestInvitationId,
                    ct
                )
                || (
                    initialKey != null
                    && await emails.ExistsWithDeduplicationKeyAsync(initialKey, ct)
                );
            if (!materialized)
            {
                dispatch.State = InvitationDispatchState.Suppressed;
                dispatch.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(ct);
                return Settled;
            }
        }
        var link =
            options.Value.ApplicationBaseUrl.TrimEnd('/') + $"/mgmt/meetings/{meetingId}";
        if (
            dispatch.GuestInvitationId is { } guestId
            && !MeetingInvitationEvents.IsWithdrawal(dispatch.EventKind)
        )
        {
            var result = await links.PrepareAsync(guestId, ct);
            if (result.Status != GuestLinkStatus.Ready)
            {
                dispatch.State =
                    result.Status == GuestLinkStatus.Unavailable
                        ? InvitationDispatchState.BlockedDependency
                        : InvitationDispatchState.Suppressed;
                dispatch.NextAttemptAt = DependencyRetryAt(
                    now,
                    dispatch.DependencyAttemptCount++
                );
                dispatch.LastError = result.Status.ToString();
                dispatch.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(ct);
                return Settled;
            }
            if (
                !Uri.TryCreate(result.Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || uri.GetLeftPart(UriPartial.Authority)
                    != new Uri(options.Value.ApplicationBaseUrl).GetLeftPart(
                        UriPartial.Authority
                    )
            )
                throw new JsonException(
                    "Guest verification URL must use the configured application origin."
                );
            link = result.Url!;
        }
        if (dispatch.PayloadVersion != 1)
            throw new JsonException("Unsupported invitation payload version.");
        var payload =
            JsonSerializer.Deserialize<InvitationPayload>(dispatch.PayloadJson)
            ?? throw new JsonException("Missing invitation payload.");
        var removal = dispatch.EventKind == InvitationEventKind.Removed;
        var cancellation = dispatch.EventKind == InvitationEventKind.Cancelled;
        var subject =
            $"{MeetingInvitationEvents.Title(dispatch.EventKind)}: {payload.MeetingTitle}"
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        if (subject.Length > 300)
            subject = subject[..(char.IsHighSurrogate(subject[299]) ? 299 : 300)];
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var body =
            $"Xin chào {payload.Name},\n\n{MeetingInvitationEvents.Title(dispatch.EventKind)}: \"{payload.MeetingTitle}\".\n";
        if (cancellation)
        {
            // No link: there is nothing left to join, and a Guest's link would need verifying first.
            body +=
                $"\nThời gian dự kiến: {TimeZoneInfo.ConvertTime(payload.StartsAt, zone):dd/MM/yyyy HH:mm zzz}\nLý do huỷ: {payload.Reason}\n";
        }
        else if (!removal)
        {
            body +=
                $"\nBắt đầu: {TimeZoneInfo.ConvertTime(payload.StartsAt, zone):dd/MM/yyyy HH:mm zzz}\nKết thúc: {TimeZoneInfo.ConvertTime(payload.EndsAt, zone):dd/MM/yyyy HH:mm zzz}\n";
            body += $"Vai trò: {MeetingInvitationEvents.RoleLabel(payload.Role)}\n";
            // Guests must verify email before learning room-entry links.
            if (dispatch.GuestInvitationId is null)
                body += $"Địa điểm: {payload.Place}\n";
            body += dispatch.GuestInvitationId is null
                ? $"\nXem cuộc họp: {link}\n"
                : $"\nXác minh email để truy cập cuộc họp: {link}\n";
        }
        var email = new EmailMessage
        {
            MeetingId = meetingId,
            DeduplicationKey = dispatch.DeduplicationKey,
            ToAddress = payload.Email,
            Subject = subject,
            Body = body,
            CreatedAt = now,
            UpdatedAt = now,
        };
        if (dispatch.EventKind is InvitationEventKind.Invitation or InvitationEventKind.Cancelled)
            email.CalendarAttachment = CalendarInvitationBuilder.Build(
                meetingId,
                payload.MeetingTitle,
                dispatch.GuestInvitationId is null ? payload.Description : "",
                dispatch.GuestInvitationId is null ? payload.Place : "",
                payload.StartsAt,
                payload.EndsAt,
                payload.Name,
                payload.Email,
                link,
                options.Value.FromName,
                options.Value.FromAddress,
                now,
                cancelled: cancellation
            );
        emails.Add(email);
        dispatch.EmailMessageId = email.Id;
        dispatch.State = InvitationDispatchState.Materialized;
        dispatch.LastError = "";
        dispatch.NextAttemptAt = null;
        dispatch.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        return new Materialization(1, Commit: true);
    }

    private static DateTimeOffset DependencyRetryAt(DateTimeOffset now, int attempt) =>
        now.AddMinutes(
            attempt switch
            {
                0 => 1,
                1 => 5,
                2 => 30,
                _ => 360,
            }
        );
}
