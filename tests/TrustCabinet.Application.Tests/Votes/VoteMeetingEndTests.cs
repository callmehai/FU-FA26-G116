using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Votes;

/// <summary>SCRUM-177 (CA-12): deleting the drafts left when a meeting ends, inside the caller's transaction.</summary>
public sealed class VoteMeetingEndTests
{
    private static readonly DateTimeOffset EndedAt = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeVoteRepository _votes = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _meeting = Guid.CreateVersion7();

    private VoteMeetingEnd Service() => new(_votes, _audit, _unitOfWork);

    private VotingSession Draft(string title)
    {
        var vote = new VotingSession
        {
            MeetingId = _meeting, Title = title, EligibleRoles = [MeetingRole.Member], CreatedById = Guid.CreateVersion7(),
            CreatedAt = EndedAt.AddHours(-1), UpdatedAt = EndedAt.AddHours(-1),
        };
        _votes.Drafts.Add(new VoteDraft(vote, ["Đồng ý", "Không đồng ý"], [Guid.CreateVersion7()]));
        return vote;
    }

    [Fact]
    public async Task Each_draft_is_removed_with_one_audit_row_and_one_change()
    {
        var first = Draft("Biểu quyết 1");
        var second = Draft("Biểu quyết 2");
        var chair = Guid.CreateVersion7();

        var changes = await Service().DeleteDraftsAsync(_meeting, chair, EndedAt);

        Assert.Equal([first, second], _votes.Removed);
        Assert.Equal([first.Id, second.Id], _audit.Rows.Select(e => e.ObjectId));
        Assert.All(_audit.Rows, e =>
        {
            Assert.Equal(AuditActions.VoteDraftDeletedOnMeetingEnd, e.Action);
            Assert.Null(e.ActorId);
            Assert.Contains("\"reason\":\"meeting_ended\"", e.Details);
            Assert.Contains(chair.ToString(), e.Details);
        });
        Assert.Equal([first.Id, second.Id], changes.Select(c => c.VoteId));
        Assert.All(changes, c => Assert.Equal(("DraftDeleted", EndedAt), (c.ChangeType, c.OccurredAt)));
    }

    [Fact]
    public async Task It_joins_the_callers_transaction_and_neither_saves_nor_commits()
    {
        Draft("Biểu quyết");

        // The meeting's own transaction, as MeetingLifecycleService opens it, is the only one committed.
        await _unitOfWork.ExecuteAsync(token => Service().DeleteDraftsAsync(_meeting, null, EndedAt, token),
            TransactionMode.ReadCommitted);

        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal((0, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_storage_failure_reaches_the_caller_and_nothing_is_staged()
    {
        Draft("Biểu quyết");
        _votes.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().DeleteDraftsAsync(_meeting, null, EndedAt));

        Assert.Empty(_votes.Removed);
        Assert.Empty(_audit.Rows);
    }

    [Fact]
    public async Task A_meeting_without_drafts_changes_nothing()
    {
        var changes = await Service().DeleteDraftsAsync(_meeting, null, EndedAt);

        Assert.Empty(changes);
        Assert.Empty(_votes.Removed);
        Assert.Empty(_audit.Rows);
    }
}
