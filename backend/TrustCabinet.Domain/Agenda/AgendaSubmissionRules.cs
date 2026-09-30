using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Agenda;

/// <summary>Submission readiness rules owned by the agenda domain.</summary>
public static class AgendaSubmissionRules
{
    public static IReadOnlyDictionary<string, string> Validate(IEnumerable<AgendaItem> source)
    {
        var items = source.ToArray();
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (items.Length == 0)
        {
            errors["agendaItems"] = "Cần có ít nhất một mục chương trình nghị sự.";
            return errors;
        }

        foreach (var item in items)
        {
            var prefix = $"agendaItems[{item.Id}]";
            var title = item.Title?.Trim() ?? string.Empty;
            if (title.Length is 0 or > 300)
            {
                errors[$"{prefix}.title"] = "Tiêu đề mục phải có từ 1 đến 300 ký tự.";
            }

            if (item.Description.Length > 4000)
            {
                errors[$"{prefix}.description"] = "Mô tả mục không được vượt quá 4.000 ký tự.";
            }

            var presenterName = item.PresenterName?.Trim() ?? string.Empty;
            if (presenterName.Length > 200)
            {
                errors[$"{prefix}.presenterName"] = "Tên người trình bày không được vượt quá 200 ký tự.";
            }

            if (!Enum.IsDefined(item.Status))
            {
                errors[$"{prefix}.status"] = "Trạng thái mục không hợp lệ.";
            }
        }

        return errors;
    }
}
