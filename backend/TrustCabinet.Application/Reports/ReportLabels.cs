namespace TrustCabinet.Application.Reports;

/// <summary>
/// The words a report uses for codes, in the order a table lists them — the same words as the screens (meeting status,
/// mode, roles, attendance, vote conclusion, minutes, recordings). The Excel files and the report query read them from
/// here, so a figure is labelled the same everywhere.
/// </summary>
public static class ReportLabels
{
    public static readonly IReadOnlyDictionary<string, string> MeetingStatus = Ordered(
        ("Draft", "Nháp"), ("WaitingApproval", "Chờ duyệt"), ("Rejected", "Bị từ chối"), ("Approved", "Đã duyệt"),
        ("Published", "Đã công bố"), ("InProgress", "Đang diễn ra"), ("Ended", "Đã kết thúc"), ("Cancelled", "Đã huỷ"));

    public static readonly IReadOnlyDictionary<string, string> Sensitivity = Ordered(
        ("Normal", "Thường"), ("Internal", "Nội bộ"), ("Secret", "Mật"));

    public static readonly IReadOnlyDictionary<string, string> Mode = Ordered(
        ("InPerson", "Trực tiếp"), ("Online", "Trực tuyến"), ("Hybrid", "Kết hợp"));

    public static readonly IReadOnlyDictionary<string, string> Role = Ordered(
        ("Chair", "Chủ toạ"), ("Secretary", "Thư ký"), ("Member", "Thành viên"), ("Guest", "Khách"));

    public static readonly IReadOnlyDictionary<string, string> Attendance = Ordered(
        ("Present", "Có mặt"), ("Absent", "Vắng"), ("Excused", "Vắng có phép"), ("Delegated", "Ủy quyền"),
        ("Unknown", "Chưa điểm danh"));

    public static readonly IReadOnlyDictionary<string, string> Response = Ordered(
        ("Accepted", "Tham dự"), ("Declined", "Từ chối"), ("Delegated", "Ủy quyền"), ("Pending", "Chưa trả lời"));

    public static readonly IReadOnlyDictionary<string, string> Conclusion = Ordered(
        ("Passed", "Thông qua"), ("NotPassed", "Không thông qua"), ("Deferred", "Hoãn"), (None, "Chưa kết luận"));

    public static readonly IReadOnlyDictionary<string, string> VoteStatus = Ordered(
        ("Draft", "Nháp"), ("Open", "Đang mở"), ("Closed", "Đã đóng"));

    public static readonly IReadOnlyDictionary<string, string> Ballot = Ordered(
        ("public", "Công khai"), ("anonymous", "Kín"));

    public static readonly IReadOnlyDictionary<string, string> FileType = Ordered(
        ("pdf", "PDF"), ("word", "Word"), ("excel", "Excel"), ("powerpoint", "PowerPoint"), ("image", "Ảnh"),
        ("other", "Khác"));

    public static readonly IReadOnlyDictionary<string, string> MinutesStatus = Ordered(
        ("Draft", "Nháp"), ("WaitingApproval", "Chờ duyệt"), ("Rejected", "Bị trả lại"), ("Issued", "Đã ban hành"),
        ("Superseded", "Đã đính chính"));

    public static readonly IReadOnlyDictionary<string, string> Source = Ordered(
        ("BrowserCapture", "Mic phòng"), ("Upload", "Tải lên"), ("CallRoom", "Phòng trực tuyến"), ("Combined", "Tổng hợp"));

    public static readonly IReadOnlyDictionary<string, string> Transcript = Ordered(
        ("Completed", "Đã gỡ băng"), ("Processing", "Đang gỡ băng"), ("Pending", "Chờ gỡ băng"), ("Failed", "Lỗi gỡ băng"),
        (None, "Chưa gỡ băng"));

    /// <summary>The value of a column that has nothing to show for a row: no conclusion yet, no transcript.</summary>
    public const string None = "None";

    /// <summary>"2026-01" as people read it: 01/2026.</summary>
    public static string Month(string month) => $"{month[5..]}/{month[..4]}";

    /// <summary>Keys in the order given: a <see cref="Dictionary{TKey,TValue}"/> enumerates in insertion order when nothing is removed.</summary>
    private static Dictionary<string, string> Ordered(params (string Key, string Label)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Label);
}
