using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Agenda;

public sealed class AgendaMeetingEndTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeAgendaRepository _agenda = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Meeting _meeting = new() { Title = "Họp giao ban", Status = MeetingStatus.InProgress };

    public AgendaMeetingEndTests() => _agenda.Meetings.Add(_meeting);

    /// <summary>The way MeetingLifecycleService calls it: inside the meeting's own transaction.</summary>
    private Task<AgendaChange?> PrepareInsideTheCallersTransaction(DateTimeOffset endedAt)
        => _unitOfWork.ExecuteAsync(token => new AgendaMeetingEnd(_agenda, _unitOfWork)
            .PrepareAsync(_meeting.Id, endedAt, token), TransactionMode.ReadCommitted);

    private AgendaItem GivenItem(AgendaItemStatus status, DateTimeOffset? startedAt)
    {
        var item = new AgendaItem { MeetingId = _meeting.Id, Title = "Mục 1", Position = 1, Status = status, ActualStartedAt = startedAt };
        _agenda.Items.Add(item);
        return item;
    }

    [Fact]
    public async Task The_item_in_progress_is_completed_inside_the_callers_transaction_without_saving()
    {
        var item = GivenItem(AgendaItemStatus.InProgress, Now.AddMinutes(-10));

        var change = await PrepareInsideTheCallersTransaction(Now);

        Assert.Equal(("MeetingEnded", item.Id, Now), (change!.ChangeType, change.AgendaItemId!.Value, change.OccurredAt));
        Assert.Equal((AgendaItemStatus.Done, Now), (item.Status, item.ActualEndedAt!.Value));
        Assert.Equal([_meeting.Id], _agenda.Locked);
        // Joined without a transaction of its own; the caller commits and saves.
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Saves));
    }

    [Fact]
    public async Task No_item_in_progress_changes_nothing()
    {
        var item = GivenItem(AgendaItemStatus.Pending, null);

        Assert.Null(await PrepareInsideTheCallersTransaction(Now));

        Assert.Equal(AgendaItemStatus.Pending, item.Status);
        Assert.Equal([_meeting.Id], _agenda.Locked);
    }

    [Fact]
    public async Task An_end_before_the_start_is_refused()
    {
        var item = GivenItem(AgendaItemStatus.InProgress, Now.AddMinutes(5));

        await Assert.ThrowsAsync<InvalidAgendaExecutionException>(() => PrepareInsideTheCallersTransaction(Now));

        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task A_store_failure_reaches_the_caller_whose_transaction_it_is()
    {
        _agenda.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => PrepareInsideTheCallersTransaction(Now));

        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
