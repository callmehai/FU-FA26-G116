using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Agenda;

namespace TrustCabinet.Application.Agenda;

public sealed class AgendaMeetingEnd(IAgendaRepository agenda, IUnitOfWork unitOfWork) : IAgendaMeetingEnd
{
    // TransactionMode.None: this runs inside the caller's meeting transaction and never opens one of its own, so a
    // caller without one is refused by LockMeetingAsync rather than given a transaction that commits on its own.
    public Task<AgendaChange?> PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default)
        => unitOfWork.ExecuteAsync<AgendaChange?>(async token =>
        {
            // Reacquiring an already held row lock is harmless and protects accidental callers as well.
            await agenda.LockMeetingAsync(meetingId, token);
            var item = await agenda.FindCurrentAsync(meetingId, token);
            if (item is null) return null;
            if (!AgendaExecution.TryComplete(item, endedAt))
                throw new InvalidAgendaExecutionException();
            return new(Guid.CreateVersion7(), meetingId, "MeetingEnded", item.Id, item.Title, item.ActualEndedAt!.Value);
        }, TransactionMode.None, ct);
}
