using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// The email queue in lists. <see cref="Rows"/> is what is stored, <see cref="Added"/> what was staged; nothing here
/// saves. The lease-checked updates apply to <see cref="Rows"/> as the real ones would, and <see cref="Next"/> is what
/// the next claim locks.
/// </summary>
public sealed class FakeEmailMessageRepository : IEmailMessageRepository
{
    public List<EmailMessage> Rows { get; } = [];
    public List<EmailMessage> Added { get; } = [];
    public List<EmailMessage> Detached { get; } = [];
    public List<string> Calls { get; } = [];

    /// <summary>What <see cref="LockNextDueAsync"/> answers; null when nothing is due.</summary>
    public EmailMessage? Next { get; set; }

    /// <summary>When true, the lease-checked updates find no row: another worker took the message over.</summary>
    public bool LeaseLost { get; set; }

    /// <summary>Thrown by <see cref="LockNextDueAsync"/> in place of answering: a store that cannot be reached.</summary>
    public Exception? FailLock { get; set; }

    public Task<bool> ExistsWithDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default)
        => Task.FromResult(Rows.Any(e => e.DeduplicationKey == deduplicationKey));

    public Task<Guid?> FindIdByDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default)
        => Task.FromResult(Rows.Where(e => e.DeduplicationKey == deduplicationKey).Select(e => (Guid?)e.Id).SingleOrDefault());

    public void Add(EmailMessage message) => Added.Add(message);

    public Task<int> MarkExhaustedAsFailedAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default)
    {
        Calls.Add(nameof(MarkExhaustedAsFailedAsync));
        return Task.FromResult(0);
    }

    public Task<EmailMessage?> LockNextDueAsync(int maxAttempts, DateTimeOffset now, CancellationToken ct = default)
    {
        Calls.Add(nameof(LockNextDueAsync));
        if (FailLock is { } failure) throw failure;
        return Task.FromResult(Next);
    }

    public void Detach(EmailMessage message) => Detached.Add(message);

    public Task<int> MarkSentAsync(Guid id, Guid leaseToken, string? providerMessageId, DateTimeOffset sentAt,
        CancellationToken ct = default)
        => Claimed(id, leaseToken, message =>
        {
            message.Status = EmailStatus.Sent;
            message.SentAt = sentAt;
            message.ProviderMessageId = providerMessageId;
            message.LastError = string.Empty;
            message.NextAttemptAt = null;
        });

    public Task<int> MarkAttemptFailedAsync(Guid id, Guid leaseToken, EmailStatus status, string error,
        DateTimeOffset? nextAttemptAt, DateTimeOffset failedAt, CancellationToken ct = default)
        => Claimed(id, leaseToken, message =>
        {
            message.Status = status;
            message.LastError = error;
            message.NextAttemptAt = nextAttemptAt;
        });

    public List<(string ProviderMessageId, Guid? RowId, EmailDeliveryState State)> Reports { get; } = [];

    /// <summary>What <see cref="RecordDeliveryReportAsync"/> answers.</summary>
    public int ReportsMatched { get; set; } = 1;

    /// <summary>Thrown by <see cref="RecordDeliveryReportAsync"/> in place of answering.</summary>
    public Exception? FailReport { get; set; }

    public Task<int> RecordDeliveryReportAsync(string providerMessageId, Guid? rowId, EmailDeliveryState state,
        DateTimeOffset occurredAt, string detail, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        if (FailReport is { } failure) throw failure;
        Reports.Add((providerMessageId, rowId, state));
        return Task.FromResult(ReportsMatched);
    }

    private Task<int> Claimed(Guid id, Guid leaseToken, Action<EmailMessage> update)
    {
        var message = Rows.SingleOrDefault(m =>
            m.Id == id && m.Status == EmailStatus.Processing && m.LeaseToken == leaseToken);
        if (message is null || LeaseLost) return Task.FromResult(0);
        update(message);
        message.LeaseToken = null;
        message.LeaseExpiresAt = null;
        return Task.FromResult(1);
    }
}

/// <summary>
/// The dispatches in a list and the facts about the meeting and invitees in fields. <see cref="Locked"/> is what the
/// lock answers (null: skipped by another worker); <see cref="Writes"/> records the direct updates in order.
/// </summary>
public sealed class FakeMeetingInvitationDispatchRepository : IMeetingInvitationDispatchRepository
{
    public List<MeetingInvitationDispatch> Added { get; } = [];
    public List<MeetingGuestInvitation> Guests { get; } = [];
    public List<MeetingInvitationDispatch> Rows { get; } = [];
    public List<string> Writes { get; } = [];

    public MeetingInvitationDispatch? Locked { get; set; }
    public MeetingStatus MeetingStatus { get; set; } = MeetingStatus.Published;
    public bool ParticipantActive { get; set; } = true;
    public bool GuestActive { get; set; } = true;
    public bool InvitationMaterialized { get; set; }

    /// <summary>Thrown by <see cref="GetMeetingStatusAsync"/>, inside the dispatch's transaction.</summary>
    public Exception? FailStatus { get; set; }

    public (Guid Id, int MaxAttempts, string Error)? FailedAttempt { get; private set; }

    public void Add(MeetingInvitationDispatch dispatch) => Added.Add(dispatch);

    public Task<MeetingGuestInvitation[]> ListActiveGuestInvitationsAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult(Guests.Where(g => g.MeetingId == meetingId && g.RevokedAt == null).ToArray());

    public Task<Guid[]> ListDueIdsAsync(DateTimeOffset now, int take, CancellationToken ct = default)
        => Task.FromResult(Rows.Select(d => d.Id).Take(take).ToArray());

    public Task<MeetingInvitationDispatch?> FindAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Rows.SingleOrDefault(d => d.Id == id));

    public Task<MeetingInvitationDispatch?> LockAsync(Guid meetingId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Locked);

    public Task<bool> IsGuestInvitationActiveAsync(Guid? guestInvitationId, CancellationToken ct = default)
        => Task.FromResult(GuestActive);

    public Task<bool> IsParticipantActiveAsync(Guid participantId, CancellationToken ct = default)
        => Task.FromResult(ParticipantActive);

    public Task<bool> IsMeetingClosedAsync(Guid? meetingId, CancellationToken ct = default)
        => Task.FromResult(MeetingStatus is MeetingStatus.Ended or MeetingStatus.Cancelled);

    public Task<MeetingStatus> GetMeetingStatusAsync(Guid? meetingId, CancellationToken ct = default)
        => FailStatus is { } failure ? Task.FromException<MeetingStatus>(failure) : Task.FromResult(MeetingStatus);

    /// <summary>The meeting's current Chair, named in every email; null when it has none.</summary>
    public string? ChairName { get; set; } = "Nguyễn Chủ Toạ";

    public MeetingOrganizer Organizer { get; set; } = new("Trần Tổ Chức", "organizer@example.vn");

    public Task<string?> FindCurrentChairNameAsync(Guid? meetingId, CancellationToken ct = default)
        => Task.FromResult(ChairName);

    public Task<MeetingOrganizer> GetOrganizerAsync(Guid? meetingId, CancellationToken ct = default)
        => Task.FromResult(Organizer);

    public Task<string?> GetParticipantInitialKeyAsync(Guid participantId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<string?> GetGuestInitialKeyAsync(Guid? guestInvitationId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<bool> HasMaterializedInvitationAsync(Guid? meetingId, Guid? participantId, Guid? guestInvitationId,
        CancellationToken ct = default)
        => Task.FromResult(InvitationMaterialized);

    public Task<int> SuppressAsync(Guid id, DateTimeOffset at, CancellationToken ct = default)
        => Write(nameof(SuppressAsync));

    public Task<int> BlockOnDependencyAsync(Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset at,
        CancellationToken ct = default)
        => Write(nameof(BlockOnDependencyAsync));

    public Task<int> RecordFailedAttemptAsync(Guid id, int maxAttempts, DateTimeOffset retryAt, string error,
        DateTimeOffset at, CancellationToken ct = default)
    {
        FailedAttempt = (id, maxAttempts, error);
        return Write(nameof(RecordFailedAttemptAsync));
    }

    private Task<int> Write(string name)
    {
        Writes.Add(name);
        return Task.FromResult(1);
    }
}

/// <summary>Records what it was asked to send; <see cref="Fail"/> is thrown in place of sending.</summary>
internal sealed class FakeEmailSender(FakeUnitOfWork? unitOfWork = null) : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public Exception? Fail { get; set; }

    /// <summary>Whether any send ran while the unit of work held a transaction open.</summary>
    public bool SentInTransaction { get; private set; }

    public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        SentInTransaction |= unitOfWork?.InTransaction == true;
        if (Fail is { } failure) throw failure;
        Sent.Add(message);
        return Task.FromResult<string?>("provider-1");
    }
}
