using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Agenda;

public static class AgendaRules
{
    public static bool GuestCanViewMeeting(MeetingStatus status) => status is MeetingStatus.Published
        or MeetingStatus.InProgress or MeetingStatus.Ended;

    public static bool PlanningAllowed(MeetingStatus status) => status is MeetingStatus.Draft
        or MeetingStatus.WaitingApproval or MeetingStatus.Rejected or MeetingStatus.Approved
        or MeetingStatus.Published or MeetingStatus.InProgress;

    public static bool CurrentParticipant(MeetingParticipant participant) => participant.User.IsActive
        && participant.AssignmentStatus == Participants.AssignmentStatus.Current && participant.Role != MeetingRole.Guest
        && participant.Response != InvitationResponse.Delegated && participant.DelegatedToUserId is null;

    public static bool HasRole(Meeting meeting, Guid actor, MeetingRole role) => meeting.Participants
        .Any(p => p.MeetingId == meeting.Id && p.UserId == actor && p.Role == role && CurrentParticipant(p));

    public static bool CanReadDocuments(Meeting meeting, Guid actor) => meeting.Participants
        .Any(p => p.MeetingId == meeting.Id && p.UserId == actor && p.Role != MeetingRole.Guest && CurrentParticipant(p));

    public static AgendaInput Normalize(AgendaInput input) => input with
    {
        Title = input.Title?.Trim() ?? "",
        Description = input.Description ?? "",
        PresenterName = input.PresenterName?.Trim() ?? "",
        DocumentIds = input.DocumentIds ?? []
    };

    public static IReadOnlyDictionary<string, string[]> Validate(AgendaInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Title is not { Length: > 0 and <= 300 }) errors["title"] = ["Tiêu đề phải có từ 1 đến 300 ký tự."];
        if (input.Description?.Length > 4000) errors["description"] = ["Mô tả không quá 4000 ký tự."];
        if (input.PlannedDurationMinutes is not > 0) errors["plannedDurationMinutes"] = ["Thời lượng phải là số nguyên dương."];
        if (input.PresenterName?.Length > 200 || input.PresenterId == Guid.Empty
            || (input.PresenterId.HasValue && !string.IsNullOrEmpty(input.PresenterName)))
            errors["presenterId"] = ["Chọn người trình bày hoặc nhập tên, không đồng thời cả hai."];
        if (input.DocumentIds is { } ids && (ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count))
            errors["documentIds"] = ["Danh sách tài liệu không hợp lệ."];
        return errors;
    }

    public static string OrderVersion(IEnumerable<AgendaItem> items)
    {
        var canonical = string.Join('\n', items.OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => $"{i.Id:D}:{i.Position.ToString(CultureInfo.InvariantCulture)}:{i.Status}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static AgendaActions Actions(Meeting meeting, Guid actor, AgendaItem item, IReadOnlyList<AgendaItem> items)
    {
        var planning = PlanningAllowed(meeting.Status) && HasRole(meeting, actor, MeetingRole.Secretary);
        var chair = meeting.Status == MeetingStatus.InProgress && HasRole(meeting, actor, MeetingRole.Chair);
        var pending = item.Status == AgendaItemStatus.Pending;
        var first = items.FirstOrDefault(i => i.Status == AgendaItemStatus.Pending);
        return new(planning, planning && pending, planning && pending,
            chair && pending && first?.Id == item.Id && !items.Any(i => i.Status == AgendaItemStatus.InProgress),
            chair && item.Status == AgendaItemStatus.InProgress);
    }
}

public static class AgendaExecution
{
    public static DateTimeOffset UtcMicroseconds(DateTimeOffset time)
        => new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);

    public static bool TryComplete(AgendaItem item, DateTimeOffset endedAt)
    {
        endedAt = UtcMicroseconds(endedAt);
        if (item.Status != AgendaItemStatus.InProgress || item.ActualStartedAt is not { } start || endedAt < start)
            return false;
        item.Status = AgendaItemStatus.Done;
        item.ActualEndedAt = endedAt;
        item.UpdatedAt = endedAt;
        return true;
    }
}
