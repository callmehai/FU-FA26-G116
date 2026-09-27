using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

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

        var ordered = items.OrderBy(item => item.Position).ThenBy(item => item.Id).ToArray();
        if (ordered.Where((item, index) => item.Position != index + 1).Any())
        {
            errors["agendaItems"] = "Vị trí các mục chương trình phải liên tục từ 1.";
        }

        foreach (var item in ordered)
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

            if (item.PlannedDurationMinutes <= 0)
            {
                errors[$"{prefix}.plannedDurationMinutes"] = "Thời lượng dự kiến phải lớn hơn 0.";
            }

            var hasPresenterId = item.PresenterId.HasValue;
            var presenterName = item.PresenterName?.Trim() ?? string.Empty;
            var hasPresenterName = presenterName.Length > 0;
            if (hasPresenterId == hasPresenterName || presenterName.Length > 200)
            {
                errors[$"{prefix}.presenter"] = "Mỗi mục cần đúng một người trình bày nội bộ hoặc khách mời (tối đa 200 ký tự).";
            }

            if (!Enum.IsDefined(item.Status) || item.Status != AgendaItemStatus.Pending)
            {
                errors[$"{prefix}.status"] = "Trạng thái mục phải là Pending trước khi gửi duyệt.";
            }
        }

        return errors;
    }
}
