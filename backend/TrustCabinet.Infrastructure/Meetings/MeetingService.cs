using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Meeting rules and persistence orchestration for UC-11, UC-12 and UC-14.</summary>
public sealed class MeetingService(
    IMeetingRepository meetings,
    ILogger<MeetingService> logger) : IMeetingService
{
    public async Task<MeetingResult> CreateAsync(
        MeetingInput input, Guid actorId, CancellationToken ct = default)
    {
        if (!MeetingRules.TryValidate(input, out var valid, out var errors))
        {
            return MeetingResult.Invalid(errors);
        }

        var now = DateTimeOffset.UtcNow;
        var meeting = new Meeting
        {
            Title = valid!.Title,
            StartsAt = valid.StartsAt,
            EndsAt = valid.EndsAt,
            Mode = valid.Mode,
            Location = valid.Location,
            Sensitivity = valid.Sensitivity,
            Description = valid.Description,
            Status = MeetingStatus.Draft,
            CreatedById = actorId,
            AssignedApproverId = valid.AssignedApproverId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            Meeting = meeting,
            FromStatus = null,
            ToStatus = MeetingStatus.Draft,
            ActorId = actorId,
            Reason = string.Empty,
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });

        try
        {
            if (valid!.AssignedApproverId is Guid approverId
                && !await meetings.IsEligibleApproverAsync(approverId, ct))
            {
                return MeetingResult.Invalid(new Dictionary<string, string>
                {
                    ["assignedApproverId"] = "Người duyệt không hợp lệ hoặc không còn quyền duyệt cuộc họp.",
                });
            }

            await meetings.AddAsync(meeting, ct);
            await meetings.SaveChangesAsync(ct);
            return MeetingResult.Success(MeetingDetails.From(meeting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not create a meeting for actor {ActorId}.", actorId);
            return MeetingResult.Failure(MeetingOutcome.SaveFailed);
        }
    }

    public async Task<MeetingResult> GetAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindAccessibleAsync(id, actorId, ct);
            return meeting is null
                ? MeetingResult.Failure(MeetingOutcome.NotFound)
                : MeetingResult.Success(MeetingDetails.From(meeting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not read meeting {MeetingId} for actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.ReadFailed);
        }
    }

    public async Task<MeetingListResult> ListAsync(
        MeetingListQuery query, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return MeetingListResult.Success(await meetings.FindAuthorizedPageAsync(actorId, query, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not list meetings for actor {ActorId}.", actorId);
            return MeetingListResult.ReadFailed();
        }
    }

    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(
        CancellationToken ct = default)
        => meetings.ListEligibleApproversAsync(ct);

    public async Task<MeetingResult> CheckUpdateAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindOwnedAsync(id, actorId, ct);
            if (meeting is null)
            {
                return MeetingResult.Failure(MeetingOutcome.NotFound);
            }

            return MeetingRules.IsEditable(meeting.Status)
                ? MeetingResult.Success(MeetingDetails.From(meeting))
                : MeetingResult.Failure(MeetingOutcome.NotEditable);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not check whether meeting {MeetingId} can be updated by actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.ReadFailed);
        }
    }

    public Task<MeetingResult> UpdateAsync(
        Guid id, MeetingInput input, Guid actorId, CancellationToken ct = default)
        => UpdateAsync(id, _ => Task.FromResult(input), actorId, ct);

    public async Task<MeetingResult> UpdateAsync(
        Guid id,
        Func<CancellationToken, Task<MeetingInput>> inputFactory,
        Guid actorId,
        CancellationToken ct = default)
    {
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindOwnedForUpdateAsync(id, actorId, token);
                if (meeting is null)
                {
                    return MeetingResult.Failure(MeetingOutcome.NotFound);
                }

                // Match UC-12: check the current, locked state before validating submitted fields.
                if (!MeetingRules.IsEditable(meeting.Status))
                {
                    return MeetingResult.Failure(MeetingOutcome.NotEditable);
                }

                var input = await inputFactory(token);
                if (!MeetingRules.TryValidate(input, out var valid, out var errors))
                {
                    return MeetingResult.Invalid(errors);
                }

                if (valid!.AssignedApproverId is Guid approverId
                    && valid.AssignedApproverId != meeting.AssignedApproverId
                    && !await meetings.IsEligibleApproverAsync(approverId, token))
                {
                    return MeetingResult.Invalid(new Dictionary<string, string>
                    {
                        ["assignedApproverId"] = "Người duyệt không hợp lệ hoặc không còn quyền duyệt cuộc họp.",
                    });
                }

                if (HasChanges(meeting, valid!))
                {
                    meeting.Title = valid.Title;
                    meeting.StartsAt = valid.StartsAt;
                    meeting.EndsAt = valid.EndsAt;
                    meeting.Mode = valid.Mode;
                    meeting.Location = valid.Location;
                    meeting.Sensitivity = valid.Sensitivity;
                    meeting.Description = valid.Description;
                    meeting.AssignedApproverId = valid.AssignedApproverId;
                    meeting.UpdatedAt = DateTimeOffset.UtcNow;
                    await meetings.SaveChangesAsync(token);
                }

                return MeetingResult.Success(MeetingDetails.From(meeting));
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not update meeting {MeetingId} for actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.SaveFailed);
        }
    }

    private static bool HasChanges(Meeting meeting, ValidatedMeetingInput input)
        => !string.Equals(meeting.Title, input.Title, StringComparison.Ordinal)
            || meeting.StartsAt != input.StartsAt
            || meeting.EndsAt != input.EndsAt
            || meeting.Mode != input.Mode
            || !string.Equals(meeting.Location, input.Location, StringComparison.Ordinal)
            || meeting.Sensitivity != input.Sensitivity
            || !string.Equals(meeting.Description, input.Description, StringComparison.Ordinal)
            || meeting.AssignedApproverId != input.AssignedApproverId;

    private static bool IsPersistenceFailure(Exception exception)
        => exception is DbUpdateException or NpgsqlException or TimeoutException;
}
