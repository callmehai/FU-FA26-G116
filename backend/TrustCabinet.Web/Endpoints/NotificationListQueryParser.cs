using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace TrustCabinet.Web.Endpoints;

internal static class NotificationListQueryParser
{
    public static bool TryParse(
        IQueryCollection values,
        out int page,
        out int pageSize,
        out IReadOnlyDictionary<string, string>? errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        page = Parse(values, "page", 1, int.MaxValue, 1, fieldErrors);
        pageSize = Parse(values, "pageSize", 1, 100, 20, fieldErrors);
        errors = fieldErrors.Count == 0 ? null : fieldErrors;
        return errors is null;
    }

    private static int Parse(
        IQueryCollection values,
        string name,
        int minimum,
        int maximum,
        int defaultValue,
        IDictionary<string, string> errors)
    {
        if (!values.TryGetValue(name, out StringValues raw) || raw.Count == 0
            || (raw.Count == 1 && string.IsNullOrWhiteSpace(raw[0])))
        {
            return defaultValue;
        }

        if (raw.Count != 1
            || !int.TryParse(raw[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < minimum
            || value > maximum)
        {
            errors[name] = name == "page"
                ? "page phải là số nguyên từ 1 trở lên."
                : "pageSize phải là số nguyên từ 1 đến 100.";
            return defaultValue;
        }

        return value;
    }
}
