using System.Globalization;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Validation and normalization rules for the create/update meeting form.</summary>
public static partial class MeetingRules
{
    private const int MaxTitleLength = 300;
    private const int MaxLocationLength = 300;
    private const int MaxOnlineLinkLength = 1000;
    private const int MaxDescriptionLength = 4000;

    public static bool IsEditable(MeetingStatus status)
        => status is MeetingStatus.Draft or MeetingStatus.Rejected;

    /// <summary>UC-75: only before the meeting starts (BR-297). Started, ended or cancelled meetings are final.</summary>
    public static bool IsCancellable(MeetingStatus status)
        => status is MeetingStatus.Draft or MeetingStatus.WaitingApproval or MeetingStatus.Rejected
            or MeetingStatus.Approved or MeetingStatus.Published;

    public const int MaxCancellationReasonLength = 1000;

    /// <summary>UC-15 AF-1: once Published, only the schedule, place and link may move (BR-27, BR-158).</summary>
    public static bool IsReschedulable(MeetingStatus status)
        => status == MeetingStatus.Published;

    /// <param name="startsAfter">
    /// When set, the planned start must be later than this instant: on create (BR-162) and on
    /// reschedule (BR-75). Editing a Draft or Rejected meeting does not re-check it.
    /// </param>
    public static bool TryValidate(
        MeetingInput input,
        DateTimeOffset? startsAfter,
        out ValidatedMeetingInput? validated,
        out IReadOnlyDictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        var title = input.Title?.Trim() ?? string.Empty;
        var location = input.Location?.Trim() ?? string.Empty;
        var onlineLink = string.IsNullOrWhiteSpace(input.OnlineLink) ? null : input.OnlineLink.Trim();
        var description = input.Description ?? string.Empty;
        DateTimeOffset? start = TryParseTimestamp(input.PlannedStartTime, out var parsedStart)
            ? parsedStart.ToUniversalTime()
            : null;
        DateTimeOffset? end = TryParseTimestamp(input.PlannedEndTime, out var parsedEnd)
            ? parsedEnd.ToUniversalTime()
            : null;

        var mode = MeetingMode.InPerson;
        if (input.Mode is not null && !TryParseDefinedEnum(input.Mode, out mode))
        {
            fieldErrors["mode"] = "Hình thức cuộc họp không hợp lệ.";
        }

        var sensitivity = Sensitivity.Normal;
        if (input.Sensitivity is not null && !TryParseDefinedEnum(input.Sensitivity, out sensitivity))
        {
            fieldErrors["sensitivity"] = "Mức độ mật không hợp lệ.";
        }

        CaptureMode? captureMode = null;
        if (input.CaptureMode is not null)
        {
            if (TryParseDefinedEnum<CaptureMode>(input.CaptureMode, out var parsedCapture)) captureMode = parsedCapture;
            else fieldErrors["captureMode"] = "Cách ghi âm không hợp lệ.";
        }

        MergeErrors(
            fieldErrors,
            ValidateInformation(title, start, end, mode, location, onlineLink, sensitivity, description));

        if (start is { } plannedStart && startsAfter is { } now && plannedStart <= now
            && !fieldErrors.ContainsKey("plannedStartTime"))
        {
            fieldErrors["plannedStartTime"] = "Thời gian bắt đầu phải sau thời điểm hiện tại.";
        }

        errors = fieldErrors;
        if (fieldErrors.Count > 0)
        {
            validated = null;
            return false;
        }

        validated = new ValidatedMeetingInput(
            title,
            start!.Value,
            end!.Value,
            mode,
            location,
            onlineLink,
            sensitivity,
            description,
            captureMode);
        return true;
    }

    public static IReadOnlyDictionary<string, string> ValidatePersistedMeetingInformation(Meeting meeting)
        => ValidateInformation(
            meeting.Title,
            meeting.PlannedStartTime == default ? null : meeting.PlannedStartTime,
            meeting.PlannedEndTime == default ? null : meeting.PlannedEndTime,
            meeting.Mode,
            meeting.Location,
            meeting.OnlineLink,
            meeting.Sensitivity,
            meeting.Description);

    private static IReadOnlyDictionary<string, string> ValidateInformation(
        string? title,
        DateTimeOffset? plannedStartTime,
        DateTimeOffset? plannedEndTime,
        MeetingMode mode,
        string? location,
        string? onlineLink,
        Sensitivity sensitivity,
        string? description)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalizedTitle = title?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0)
        {
            errors["title"] = "Tên cuộc họp không được để trống.";
        }
        else if (normalizedTitle.Length > MaxTitleLength)
        {
            errors["title"] = "Tên cuộc họp không được vượt quá 300 ký tự.";
        }

        if (plannedStartTime is null)
        {
            errors["plannedStartTime"] = "Thời gian bắt đầu không hợp lệ.";
        }

        if (plannedEndTime is null)
        {
            errors["plannedEndTime"] = "Thời gian kết thúc không hợp lệ.";
        }
        else if (plannedStartTime is { } start && plannedEndTime <= start)
        {
            errors["plannedEndTime"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
        }

        if (!Enum.IsDefined(mode))
        {
            errors["mode"] = "Hình thức cuộc họp không hợp lệ.";
        }

        if (!Enum.IsDefined(sensitivity))
        {
            errors["sensitivity"] = "Mức độ mật không hợp lệ.";
        }

        if ((location?.Length ?? 0) > MaxLocationLength)
        {
            errors["location"] = "Địa điểm không được vượt quá 300 ký tự.";
        }

        if ((onlineLink?.Length ?? 0) > MaxOnlineLinkLength)
        {
            errors["onlineLink"] = "Link họp trực tuyến không được vượt quá 1.000 ký tự.";
        }

        if ((description?.Length ?? 0) > MaxDescriptionLength)
        {
            errors["description"] = "Mô tả không được vượt quá 4000 ký tự.";
        }

        return errors;
    }

    private static void MergeErrors(
        IDictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var (field, message) in source)
        {
            target[field] = message;
        }
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return !string.IsNullOrEmpty(value)
            && IsoTimestampPattern().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    private static bool TryParseDefinedEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrEmpty(value)
            && Enum.TryParse(value, ignoreCase: false, out result)
            && Enum.IsDefined(result)
            && string.Equals(value, result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>ISO 8601 with an explicit offset, so a time is never read in the server's zone.</summary>
    [GeneratedRegex(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();
}
