using System.Globalization;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Validation of planned Meeting Sessions and the parent statuses that allow planning them.</summary>
public static partial class MeetingSessionRules
{
    public const int MaximumTitleLength = 300;

    public static bool IsValidPlanned(MeetingSession session, Guid meetingId)
        => meetingId != Guid.Empty
            && session.Id != Guid.Empty
            && session.MeetingId == meetingId
            && session.Status == MeetingSessionStatus.Planned
            && !string.IsNullOrWhiteSpace(session.Title)
            && session.Title.Length <= MaximumTitleLength
            && session.PlannedStartsAt != default
            && session.PlannedEndsAt != default
            && session.PlannedEndsAt > session.PlannedStartsAt;

    /// <summary>BR-54: sessions are planned only while the parent meeting is in one of these statuses.</summary>
    public static bool AllowsPlanning(MeetingStatus status)
        => status is MeetingStatus.Draft
            or MeetingStatus.Rejected
            or MeetingStatus.Approved
            or MeetingStatus.Published
            or MeetingStatus.InProgress;

    /// <summary>BR-75: after publication a new or rescheduled session must start in the future.</summary>
    public static bool RequiresFutureStart(MeetingStatus status)
        => status is MeetingStatus.Published or MeetingStatus.InProgress;

    /// <summary>
    /// Validates the session form (BR-62, and BR-75 when <paramref name="mustStartAfter"/> is set).
    /// Timestamps must carry <c>Z</c> or an explicit offset so the server never guesses a time zone.
    /// A start equal to <paramref name="unchangedStart"/> is not a reschedule, so BR-75 skips it.
    /// </summary>
    public static bool TryValidate(
        MeetingSessionInput input,
        DateTimeOffset? mustStartAfter,
        out ValidatedMeetingSessionInput? validated,
        out IReadOnlyDictionary<string, string> errors,
        DateTimeOffset? unchangedStart = null)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            fieldErrors["title"] = "Tên phiên họp không được để trống.";
        }
        else if (title.Length > MaximumTitleLength)
        {
            fieldErrors["title"] = "Tên phiên họp không được vượt quá 300 ký tự.";
        }

        var startValid = TryParseTimestamp(input.PlannedStartsAt, out var startsAt);
        if (!startValid)
        {
            fieldErrors["plannedStartsAt"] = "Thời gian bắt đầu dự kiến không hợp lệ.";
        }
        else if (mustStartAfter is { } now && startsAt <= now && startsAt != unchangedStart)
        {
            fieldErrors["plannedStartsAt"] = "Thời gian bắt đầu dự kiến phải sau thời điểm hiện tại.";
        }

        if (!TryParseTimestamp(input.PlannedEndsAt, out var endsAt))
        {
            fieldErrors["plannedEndsAt"] = "Thời gian kết thúc dự kiến không hợp lệ.";
        }
        else if (startValid && endsAt <= startsAt)
        {
            fieldErrors["plannedEndsAt"] = "Thời gian kết thúc dự kiến phải sau thời gian bắt đầu dự kiến.";
        }

        errors = fieldErrors;
        if (fieldErrors.Count > 0)
        {
            validated = null;
            return false;
        }

        validated = new ValidatedMeetingSessionInput(title, startsAt.ToUniversalTime(), endsAt.ToUniversalTime());
        return true;
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return !string.IsNullOrEmpty(value)
            && IsoTimestampPattern().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    [GeneratedRegex(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();
}
