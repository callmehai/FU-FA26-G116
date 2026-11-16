using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.CalendarSharing;

/// <summary>
/// SCRUM-142 — UC-85c: the week of busy hours people shared with the caller, Monday to Friday, 08:00–17:00 in Vietnam.
/// Busy blocks only — no title, place, attendee or level of any meeting (BR-427). A stopped share is listed with its
/// status and shows no hours (BR-424); a share past its end date is left out altogether (BR-425, PO 09/11/2026 Q-B).
/// </summary>
public sealed class SharedCalendarService(
    ICalendarShareRepository calendarShares,
    IUnitOfWork unitOfWork,
    CalendarBusyReader busy,
    TimeProvider clock,
    ILogger<SharedCalendarService> logger) : ISharedCalendarService
{
    internal static readonly TimeOnly DayStart = new(8, 0);
    internal static readonly TimeOnly DayEnd = new(17, 0);
    private const int WorkingDays = 5;

    public async Task<CalendarShareRead<SharedWeek>> ReadWeekAsync(Guid actorId, DateOnly? week,
        CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync<CalendarShareRead<SharedWeek>>(async token =>
            {
                if (!await calendarShares.IsOrganizerAsync(actorId, token))
                    return new(null, CalendarShareFailure.NotOrganizer);

                var today = VietnamTime.Today(clock.GetUtcNow());
                var monday = VietnamTime.Monday(week ?? today);
                var friday = monday.AddDays(WorkingDays - 1);

                var shares = await calendarShares.FindSharedWithAsync(actorId, today, token);
                // One line per owner: their newest share among the ones that say something about their hours.
                var latest = shares.GroupBy(s => s.OwnerId)
                    .Select(g => g.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).First())
                    .OrderBy(s => s.Owner.FullName, StringComparer.CurrentCulture).ThenBy(s => s.Owner.Email)
                    .ToList();

                string StatusOf(CalendarShare share)
                {
                    // A deactivated owner whose stop has not been written yet still shows nothing.
                    return share.Status == CalendarShareStatus.Effective && !share.Owner.IsActive
                        ? nameof(CalendarShareStatus.Stopped)
                        : share.Status.ToString();
                }

                var live = latest.Where(s => StatusOf(s) == nameof(CalendarShareStatus.Effective)).ToList();
                var intervals = await busy.ReadAsync(
                    live.ToDictionary(s => s.OwnerId, s => (CalendarShareLevel?)s.Level),
                    VietnamTime.At(monday, DayStart), VietnamTime.At(friday, DayEnd), token);

                var people = latest.Select(share =>
                {
                    var status = StatusOf(share);
                    IReadOnlyList<SharedDay> days = status == nameof(CalendarShareStatus.Effective)
                        ? [.. Enumerable.Range(0, WorkingDays).Select(offset => Day(share, monday.AddDays(offset), intervals))]
                        : [];
                    return new SharedPerson(share.Id, new SharePerson(share.Owner.Id, share.Owner.FullName, share.Owner.Email),
                        status, share.Level.ToString(), share.StartDate, share.EndDate, days);
                }).ToList();

                return new(new SharedWeek(monday, friday, DayStart.ToString("HH:mm"), DayEnd.ToString("HH:mm"), people));
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException error)
        {
            logger.LogError(error.InnerException ?? error, "Could not read the shared week of {UserId}", actorId);
            return new(null, CalendarShareFailure.ReadFailed);
        }
    }

    private static SharedDay Day(CalendarShare share, DateOnly date,
        IReadOnlyDictionary<Guid, IReadOnlyList<BusyInterval>> intervals)
    {
        // BR-415: a day outside the share's range is unknown — neither free nor busy.
        if (date < share.StartDate || date > share.EndDate) return new(date, false, []);
        var blocks = intervals.TryGetValue(share.OwnerId, out var found) ? found : [];
        return new(date, true, [.. CalendarBusyRules.Clip(blocks, VietnamTime.At(date, DayStart), VietnamTime.At(date, DayEnd))
            .Select(i => new BusyInterval(VietnamTime.ToLocal(i.Start), VietnamTime.ToLocal(i.End)))]);
    }
}
