using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Agenda;

public sealed class AgendaService(IMeetingRepository meetings, IAgendaRepository agenda,
    IAgendaDocumentRepository documents, IAgendaChangePublisher publisher, TimeProvider clock,
    ILogger<AgendaService> logger) : IAgendaService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;
    private DateTimeOffset Now => AgendaExecution.UtcMicroseconds(clock.GetUtcNow());

    public Task<AgendaOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
        => ReadAsync(meetingId, actorId, false, ct);
    public Task<AgendaOutcome> EditorOptionsAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
        => ReadAsync(meetingId, actorId, true, ct);

    private async Task<AgendaOutcome> ReadAsync(Guid meetingId, Guid actorId, bool options, CancellationToken ct)
    {
        try
        {
            if (!await meetings.IsUserActiveAsync(actorId, ct)) return new(AgendaFailure.Inactive);
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return new(AgendaFailure.NotFound);
            var secretary = AgendaRules.HasRole(meeting, actorId, MeetingRole.Secretary);
            var planning = secretary && AgendaRules.PlanningAllowed(meeting.Status);
            if (options)
            {
                if (!secretary) return new(AgendaFailure.Forbidden);
                if (!planning) return new(AgendaFailure.Locked);
                return new(Data: new AgendaEditorOptions(meeting.Participants
                    .Where(p => p.MeetingId == meeting.Id && p.Role != MeetingRole.Guest && AgendaRules.CurrentParticipant(p))
                    .Select(p => new AgendaPresenter(p.UserId, p.User.FullName)).DistinctBy(p => p.Id)
                    .OrderBy(p => p.FullName).ThenBy(p => p.Id).ToArray(), await documents.ListAsync(meetingId, ct)));
            }
            var items = await agenda.ListAsync(meetingId, false, ct);
            var canReadDocuments = AgendaRules.CanReadDocuments(meeting, actorId);
            var linked = canReadDocuments ? await documents.ListAsync(meetingId, ct) : [];
            var byItem = linked.Where(d => d.AgendaItemId.HasValue).ToLookup(d => d.AgendaItemId!.Value);
            return new(Data: new AgendaSnapshot(meeting.Id, meeting.Status.ToString(), meeting.PlannedStartTime,
                meeting.PlannedEndTime, Now, AgendaRules.OrderVersion(items), new(planning, planning),
                items.Select(i => new AgendaItemView(i.Id, i.Position, i.Title, i.Description,
                    i.PlannedDurationMinutes, i.PresenterId, i.Presenter?.FullName ?? i.PresenterName, i.Status.ToString(),
                    i.ActualStartedAt, i.ActualEndedAt,
                    i.Status == AgendaItemStatus.Done && i.ActualStartedAt is { } start && i.ActualEndedAt is { } end
                        ? (end - start).TotalSeconds : null,
                    canReadDocuments, byItem[i.Id].Select(d => new AgendaDocument(d.Id, d.Title)).ToArray(),
                    AgendaRules.Actions(meeting, actorId, i, items))).ToArray()));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read agenda {MeetingId}", meetingId);
            return new(AgendaFailure.ReadFailed);
        }
    }

    public Task<AgendaOutcome> CreateAsync(Guid meetingId, Guid actorId, AgendaInput input, CancellationToken ct = default)
        => MutateAsync(meetingId, null, actorId, AgendaOperation.Create, input, ct);
    public Task<AgendaOutcome> UpdateAsync(Guid meetingId, Guid itemId, Guid actorId, AgendaInput input, CancellationToken ct = default)
        => MutateAsync(meetingId, itemId, actorId, AgendaOperation.Update, input, ct);
    public Task<AgendaOutcome> DeleteAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default)
        => MutateAsync(meetingId, itemId, actorId, AgendaOperation.Delete, new(), ct);
    public Task<AgendaOutcome> ReorderAsync(Guid meetingId, Guid actorId, AgendaInput input, CancellationToken ct = default)
        => MutateAsync(meetingId, null, actorId, AgendaOperation.Reorder, input, ct);
    public Task<AgendaOutcome> StartAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default)
        => MutateAsync(meetingId, itemId, actorId, AgendaOperation.Start, new(), ct);
    public Task<AgendaOutcome> CompleteAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default)
        => MutateAsync(meetingId, itemId, actorId, AgendaOperation.Complete, new(), ct);

    private async Task<AgendaOutcome> MutateAsync(Guid meetingId, Guid? itemId, Guid actorId,
        AgendaOperation operation, AgendaInput input, CancellationToken ct)
    {
        AgendaChange? change = null;
        AgendaOutcome outcome;
        try
        {
            outcome = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new AgendaOutcome(AgendaFailure.Inactive);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return new(AgendaFailure.NotFound);
                var items = await agenda.ListAsync(meetingId, true, token);
                var item = items.SingleOrDefault(i => i.Id == itemId);
                if (itemId.HasValue && item is null) return new(AgendaFailure.NotFound);
                var execution = operation is AgendaOperation.Start or AgendaOperation.Complete;
                if (!AgendaRules.HasRole(meeting, actorId, execution ? MeetingRole.Chair : MeetingRole.Secretary))
                    return new(AgendaFailure.Forbidden);
                if (execution ? meeting.Status != MeetingStatus.InProgress : !AgendaRules.PlanningAllowed(meeting.Status))
                    return new(AgendaFailure.Locked);
                if (operation == AgendaOperation.Delete && item!.Status != AgendaItemStatus.Pending)
                    return new(AgendaFailure.DeleteLocked);
                if (operation == AgendaOperation.Start && item!.Status != AgendaItemStatus.Pending)
                    return new(AgendaFailure.CannotStart);
                if (operation == AgendaOperation.Complete && item!.Status != AgendaItemStatus.InProgress)
                    return new(AgendaFailure.CannotComplete);
                if (input.ParseFailure != AgendaFailure.None) return new(input.ParseFailure,
                    Errors: new Dictionary<string, string[]> { ["body"] = ["Nội dung yêu cầu không hợp lệ."] });

                var now = Now;
                var changed = true;
                if (operation is AgendaOperation.Create or AgendaOperation.Update)
                {
                    input = AgendaRules.Normalize(input);
                    var errors = AgendaRules.Validate(input);
                    if (errors.Count != 0) return new(AgendaFailure.Invalid, Errors: errors);
                    var existingDocuments = item is null ? [] : (await documents.ListAsync(meetingId, token))
                        .Where(d => d.AgendaItemId == item.Id).Select(d => d.Id).ToArray();
                    var planningChanged = item is null || item.PlannedDurationMinutes != input.PlannedDurationMinutes
                        || item.PresenterId != input.PresenterId || item.PresenterName != input.PresenterName
                        || !existingDocuments.ToHashSet().SetEquals(input.DocumentIds!);
                    if (item is not null && item.Status != AgendaItemStatus.Pending && planningChanged)
                        return new(AgendaFailure.PlanningFieldsLocked);
                    if (input.PresenterId is { } presenter && (item is null || item.PresenterId != presenter || item.Status == AgendaItemStatus.Pending)
                        && !meeting.Participants.Any(p => p.MeetingId == meetingId && p.UserId == presenter
                            && p.Role != MeetingRole.Guest && AgendaRules.CurrentParticipant(p)))
                        return InvalidField("presenterId", "Người trình bày phải là thành viên nội bộ đang hoạt động của cuộc họp.");
                    if (!await documents.CanAssociateAsync(meetingId, itemId, input.DocumentIds!, token))
                        return InvalidField("documentIds", "Tài liệu không hợp lệ hoặc đã gắn với mục khác.");
                    if (item is null)
                    {
                        var max = items.Count == 0 ? 0 : items.Max(i => i.Position);
                        if (max == int.MaxValue) return new(AgendaFailure.SaveFailed);
                        item = new AgendaItem { MeetingId = meetingId, Title = input.Title!, Position = max + 1,
                            CreatedAt = now, UpdatedAt = now };
                        agenda.Add(item);
                    }
                    else changed = planningChanged || item.Title != input.Title || item.Description != input.Description;
                    if (changed)
                    {
                        item.Title = input.Title!;
                        item.Description = input.Description!;
                        item.PlannedDurationMinutes = input.PlannedDurationMinutes!.Value;
                        item.PresenterId = input.PresenterId;
                        item.PresenterName = input.PresenterName!;
                        item.UpdatedAt = now;
                        await documents.AssociateAsync(meetingId, item.Id, input.DocumentIds!, token);
                    }
                }
                else if (operation == AgendaOperation.Delete) agenda.Remove(item!);
                else if (operation == AgendaOperation.Reorder)
                {
                    if (string.IsNullOrWhiteSpace(input.OrderVersion) || input.PendingItemIds is null
                        || input.PendingItemIds.Contains(Guid.Empty) || input.PendingItemIds.Distinct().Count() != input.PendingItemIds.Count)
                        return new(AgendaFailure.InvalidOrder);
                    if (input.OrderVersion != AgendaRules.OrderVersion(items)) return new(AgendaFailure.StaleOrder);
                    var pending = items.Where(i => i.Status == AgendaItemStatus.Pending).ToArray();
                    if (!pending.Select(i => i.Id).ToHashSet().SetEquals(input.PendingItemIds)) return new(AgendaFailure.InvalidOrder);
                    var positions = pending.Select(i => i.Position).Order().ToArray();
                    var lookup = pending.ToDictionary(i => i.Id);
                    changed = false;
                    for (var index = 0; index < positions.Length; index++)
                    {
                        var moving = lookup[input.PendingItemIds[index]];
                        if (moving.Position == positions[index]) continue;
                        moving.Position = positions[index];
                        moving.UpdatedAt = now;
                        changed = true;
                    }
                }
                else if (operation == AgendaOperation.Start)
                {
                    if (items.Any(i => i.Status == AgendaItemStatus.InProgress)) return new(AgendaFailure.AlreadyCurrent);
                    if (items.First(i => i.Status == AgendaItemStatus.Pending).Id != item!.Id) return new(AgendaFailure.NotFirstPending);
                    item.Status = AgendaItemStatus.InProgress;
                    item.ActualStartedAt = now;
                    item.UpdatedAt = now;
                }
                else if (!AgendaExecution.TryComplete(item!, now))
                {
                    logger.LogError("Invalid actual timestamps on agenda item {AgendaItemId}", itemId);
                    return new(AgendaFailure.SaveFailed);
                }

                if (changed)
                {
                    await meetings.SaveChangesAsync(token);
                    var changeType = operation switch { AgendaOperation.Create => "Created", AgendaOperation.Update => "Updated",
                        AgendaOperation.Delete => "Deleted", AgendaOperation.Reorder => "Reordered", AgendaOperation.Start => "Started", _ => "Completed" };
                    change = new(Guid.CreateVersion7(), meetingId, changeType, item?.Id, item?.Title, now);
                }
                return new(ItemId: item?.Id);
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed agenda {Operation} for meeting {MeetingId}", operation, meetingId);
            return new(AgendaFailure.SaveFailed);
        }
        // A committed write must remain successful even when the client disconnects or push fails.
        if (change is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            // Cancellation is cooperative: drain the publisher before its scoped dependencies are disposed.
            try { await publisher.PublishAsync(change, timeout.Token); }
            catch (Exception ex) { logger.LogWarning(ex, "Agenda committed; realtime delivery failed for {MeetingId}", meetingId); }
        }
        return outcome;
    }

    private static AgendaOutcome InvalidField(string field, string message)
        => new(AgendaFailure.Invalid, Errors: new Dictionary<string, string[]> { [field] = [message] });
}
