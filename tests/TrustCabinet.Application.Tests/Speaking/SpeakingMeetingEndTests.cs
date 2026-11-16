using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Speaking;

/// <summary>SCRUM-178 (CA-13): ending the meeting ends every open request, inside the caller's transaction (BR-337).</summary>
public sealed class SpeakingMeetingEndTests
{
    private static readonly DateTimeOffset EndedAt = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeSpeakingRequestRepository _requests = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _meeting = Guid.CreateVersion7();

    private SpeakingMeetingEnd Service() => new(_requests, _unitOfWork);

    private SpeakingRequest Request(SpeakingRequestStatus status)
    {
        var request = new SpeakingRequest
        {
            Id = Guid.CreateVersion7(), MeetingId = _meeting, UserId = Guid.CreateVersion7(), Status = status,
            RequestedAt = EndedAt.AddMinutes(-30), CreatedAt = EndedAt.AddMinutes(-30), UpdatedAt = EndedAt.AddMinutes(-30),
        };
        _requests.Requests.Add(request);
        return request;
    }

    [Fact]
    public async Task A_turn_in_progress_is_finished_and_the_rest_cancelled_at_the_end()
    {
        var speaking = Request(SpeakingRequestStatus.Speaking);
        var queued = Request(SpeakingRequestStatus.Queued);

        await Service().PrepareAsync(_meeting, EndedAt);

        Assert.Equal((SpeakingRequestStatus.Finished, EndedAt), (speaking.Status, speaking.EndedAt));
        Assert.Equal((SpeakingRequestStatus.Cancelled, EndedAt), (queued.Status, queued.EndedAt));
        Assert.Equal([(_meeting, (Guid?)null, (IReadOnlyCollection<Guid>?)null, EndedAt)], _requests.Closed);
    }

    [Fact]
    public async Task It_joins_the_callers_transaction_and_neither_saves_nor_commits()
    {
        Request(SpeakingRequestStatus.Queued);

        // The meeting's own transaction, as MeetingLifecycleService opens it, is the only one committed.
        await _unitOfWork.ExecuteAsync(token => Service().PrepareAsync(_meeting, EndedAt, token), TransactionMode.ReadCommitted);

        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal((0, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_storage_failure_reaches_the_caller()
    {
        var queued = Request(SpeakingRequestStatus.Queued);
        _requests.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().PrepareAsync(_meeting, EndedAt));

        Assert.Equal(SpeakingRequestStatus.Queued, queued.Status);
    }
}
