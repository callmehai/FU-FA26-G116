using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Endpoints;

/// <summary>Turns untrusted meeting-list query strings into bounded, canonical criteria.</summary>
public static partial class MeetingListQueryParser
{
    public static bool TryParse(
        IQueryCollection values,
        out MeetingListQuery? query,
        out IReadOnlyDictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        var rawStatus = Read(values, "status", fieldErrors);
        var rawSensitivity = Read(values, "sensitivity", fieldErrors);
        var rawSearch = Read(values, "q", fieldErrors);
        var rawFrom = Read(values, "from", fieldErrors);
        var rawTo = Read(values, "to", fieldErrors);
        var rawPage = Read(values, "page", fieldErrors);
        var rawPageSize = Read(values, "pageSize", fieldErrors);
        var rawSortBy = Read(values, "sortBy", fieldErrors);
        var rawSortDir = Read(values, "sortDir", fieldErrors);

        MeetingStatus? status = null;
        if (rawStatus is not null)
        {
            if (TryParseEnum(rawStatus, out MeetingStatus parsed))
            {
                status = parsed;
            }
            else
            {
                fieldErrors["status"] = "Trạng thái cuộc họp không hợp lệ.";
            }
        }

        Sensitivity? sensitivity = null;
        if (rawSensitivity is not null)
        {
            if (TryParseEnum(rawSensitivity, out Sensitivity parsed))
            {
                sensitivity = parsed;
            }
            else
            {
                fieldErrors["sensitivity"] = "Mức độ mật không hợp lệ.";
            }
        }

        var from = ParseTimestamp(rawFrom, "from", fieldErrors);
        var to = ParseTimestamp(rawTo, "to", fieldErrors);
        if (from is not null && to is not null && from > to)
        {
            fieldErrors["to"] = "Thời gian kết thúc bộ lọc phải từ thời gian bắt đầu trở đi.";
        }

        var page = ParsePositiveInt(rawPage, "page", 1, int.MaxValue, fieldErrors);
        var pageSize = ParsePositiveInt(rawPageSize, "pageSize", 20, 100, fieldErrors);

        var sortBy = rawSortBy switch
        {
            null or "startsAt" => MeetingSortBy.StartsAt,
            "title" => MeetingSortBy.Title,
            "createdAt" => MeetingSortBy.CreatedAt,
            _ => MeetingSortBy.StartsAt,
        };
        if (rawSortBy is not null && rawSortBy is not ("title" or "startsAt" or "createdAt"))
        {
            fieldErrors["sortBy"] = "Trường sắp xếp không hợp lệ.";
        }

        var defaultDirection = sortBy == MeetingSortBy.Title
            ? MeetingSortDirection.Asc
            : MeetingSortDirection.Desc;
        var sortDirection = rawSortDir switch
        {
            "asc" => MeetingSortDirection.Asc,
            "desc" => MeetingSortDirection.Desc,
            _ => defaultDirection,
        };
        if (rawSortDir is not null && rawSortDir is not ("asc" or "desc"))
        {
            fieldErrors["sortDir"] = "Chiều sắp xếp không hợp lệ.";
        }

        errors = fieldErrors;
        if (fieldErrors.Count > 0)
        {
            query = null;
            return false;
        }

        query = new MeetingListQuery(
            status, sensitivity, rawSearch?.Trim(), from, to, page, pageSize, sortBy, sortDirection);
        return true;
    }

    private static string? Read(
        IQueryCollection values, string key, IDictionary<string, string> errors)
    {
        if (!values.TryGetValue(key, out StringValues value))
        {
            return null;
        }

        if (value.Count != 1)
        {
            errors[key] = "Chỉ được gửi một giá trị cho tham số này.";
            return null;
        }

        return string.IsNullOrWhiteSpace(value[0]) ? null : value[0];
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum result)
        where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: false, out result)
            && Enum.IsDefined(result)
            && string.Equals(value, result.ToString(), StringComparison.Ordinal);

    private static DateTimeOffset? ParseTimestamp(
        string? raw, string key, IDictionary<string, string> errors)
    {
        if (raw is null)
        {
            return null;
        }

        if (IsoTimestampPattern().IsMatch(raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        errors[key] = "Thời gian phải là ISO 8601 và có múi giờ.";
        return null;
    }

    private static int ParsePositiveInt(
        string? raw, string key, int defaultValue, int maximum, IDictionary<string, string> errors)
    {
        if (raw is null)
        {
            return defaultValue;
        }

        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value >= 1 && value <= maximum)
        {
            return value;
        }

        errors[key] = $"Giá trị phải là số nguyên từ 1 đến {maximum}.";
        return defaultValue;
    }

    [GeneratedRegex(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();
}
