using System.Globalization;
using System.Text.RegularExpressions;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Validation and normalization rules for the create/update meeting form.</summary>
public static partial class MeetingRules
{
    private const int MaxTitleLength = 300;
    private const int MaxLocationLength = 300;
    private const int MaxDescriptionLength = 4000;

    public static bool IsEditable(MeetingStatus status)
        => status is MeetingStatus.Draft or MeetingStatus.Rejected;

    public static bool TryValidate(
        MeetingInput input,
        out ValidatedMeetingInput? validated,
        out IReadOnlyDictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        var title = input.Title?.Trim() ?? string.Empty;
        var location = input.Location?.Trim() ?? string.Empty;
        var description = input.Description ?? string.Empty;

        if (title.Length == 0)
        {
            fieldErrors["title"] = "Tên cuộc họp không được để trống.";
        }
        else if (title.Length > MaxTitleLength)
        {
            fieldErrors["title"] = "Tên cuộc họp không được vượt quá 300 ký tự.";
        }

        if (location.Length > MaxLocationLength)
        {
            fieldErrors["location"] = "Địa điểm không được vượt quá 300 ký tự.";
        }

        if (description.Length > MaxDescriptionLength)
        {
            fieldErrors["description"] = "Mô tả không được vượt quá 4000 ký tự.";
        }

        var startsAtValid = TryParseTimestamp(input.StartsAt, out var startsAt);
        if (!startsAtValid)
        {
            fieldErrors["startsAt"] = "Thời gian bắt đầu không hợp lệ.";
        }

        DateTimeOffset? endsAt = null;
        if (!string.IsNullOrWhiteSpace(input.EndsAt))
        {
            if (!TryParseTimestamp(input.EndsAt, out var parsedEndsAt))
            {
                fieldErrors["endsAt"] = "Thời gian kết thúc không hợp lệ.";
            }
            else if (startsAtValid && parsedEndsAt <= startsAt)
            {
                fieldErrors["endsAt"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            }
            else
            {
                endsAt = parsedEndsAt.ToUniversalTime();
            }
        }

        if (input.AssignedApproverId is null || input.AssignedApproverId == Guid.Empty)
        {
            fieldErrors["assignedApproverId"] = "Vui lòng chọn người duyệt.";
        }

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

        errors = fieldErrors;
        if (fieldErrors.Count > 0)
        {
            validated = null;
            return false;
        }

        validated = new ValidatedMeetingInput(
            title,
            startsAt.ToUniversalTime(),
            endsAt,
            mode,
            location,
            sensitivity,
            description,
            input.AssignedApproverId!.Value);
        return true;
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrEmpty(value) || !IsoTimestampPattern().IsMatch(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);
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

    [GeneratedRegex(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();
}
