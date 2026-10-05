using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Validation and normalization rules for the create/update meeting form.</summary>
public static class MeetingRules
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

        MergeErrors(
            fieldErrors,
            ValidateInformation(title, mode, location, sensitivity, description));

        errors = fieldErrors;
        if (fieldErrors.Count > 0)
        {
            validated = null;
            return false;
        }

        validated = new ValidatedMeetingInput(
            title,
            mode,
            location,
            sensitivity,
            description);
        return true;
    }

    public static IReadOnlyDictionary<string, string> ValidatePersistedMeetingInformation(Meeting meeting)
        => ValidateInformation(
            meeting.Title,
            meeting.Mode,
            meeting.Location,
            meeting.Sensitivity,
            meeting.Description);

    private static IReadOnlyDictionary<string, string> ValidateInformation(
        string? title,
        MeetingMode mode,
        string? location,
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

    private static bool TryParseDefinedEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrEmpty(value)
            && Enum.TryParse(value, ignoreCase: false, out result)
            && Enum.IsDefined(result)
            && string.Equals(value, result.ToString(), StringComparison.Ordinal);
    }
}
