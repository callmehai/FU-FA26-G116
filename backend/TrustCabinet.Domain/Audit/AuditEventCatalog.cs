using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Audit;

/// <summary>One kind of audit event, as the audit log screen's event filter lists it (UC-13 AF-1).</summary>
public sealed record AuditEventType(string Code, string Label, string Group);

/// <summary>
/// The Vietnamese name of every event code the application writes (SCRUM-16). Kept on the server, like
/// <c>PermissionCatalog</c>: a new code cannot reach the screen without a name, and the tester reads the same words as
/// the administrator. <c>AuditEventCatalogTests</c> fails when an <see cref="AuditActions"/> code has no entry.
/// </summary>
public static class AuditEventCatalog
{
    private const string Accounts = "Tài khoản";
    private const string SignIn = "Đăng nhập";
    private const string Roles = "Vai trò và quyền";
    private const string Meetings = "Cuộc họp";
    private const string Participants = "Thành phần và điểm danh";
    private const string Documents = "Tài liệu";
    private const string Votes = "Biểu quyết";
    private const string Minutes = "Biên bản";
    private const string Recordings = "Bản ghi và gỡ băng";
    private const string Voice = "Voice ID";
    private const string Settings = "Cấu hình";
    private const string CalendarSharing = "Chia sẻ lịch";
    private const string Reports = "Báo cáo";

    public static IReadOnlyList<AuditEventType> All { get; } =
    [
        new(AuditActions.UserCreated, "Tạo tài khoản", Accounts),
        new(AuditActions.UserUpdated, "Sửa tài khoản", Accounts),
        new(AuditActions.UserDeactivated, "Vô hiệu hoá tài khoản", Accounts),
        new(AuditActions.UserReactivated, "Kích hoạt lại tài khoản", Accounts),
        new(AuditActions.UserGoogleLinked, "Liên kết tài khoản Google", Accounts),
        new(AuditActions.UserPasswordResetRequested, "Gửi liên kết đặt lại mật khẩu", Accounts),
        new(AuditActions.UserPasswordReset, "Đặt lại mật khẩu", Accounts),

        new(AuditActions.SignedIn, "Đăng nhập", SignIn),
        new(AuditActions.SignInFailed, "Đăng nhập thất bại", SignIn),
        new(AuditActions.GuestVerified, "Khách xác minh mã", SignIn),

        new(AuditActions.RolePermissionsUpdated, "Đổi quyền của vai trò", Roles),
        new(AuditActions.RoleCreated, "Tạo vai trò", Roles),
        new(AuditActions.RoleUpdated, "Sửa vai trò", Roles),
        new(AuditActions.RoleDeleted, "Xoá vai trò", Roles),

        History(MeetingHistoryAction.Create, "Tạo cuộc họp"),
        History(MeetingHistoryAction.SubmitForApproval, "Gửi duyệt cuộc họp"),
        History(MeetingHistoryAction.Approve, "Duyệt cuộc họp"),
        History(MeetingHistoryAction.Reject, "Từ chối cuộc họp"),
        History(MeetingHistoryAction.ReturnToDraft, "Trả cuộc họp về nháp"),
        History(MeetingHistoryAction.Publish, "Công bố cuộc họp"),
        History(MeetingHistoryAction.TransferOrganizer, "Chuyển người tổ chức"),
        History(MeetingHistoryAction.Reschedule, "Đổi lịch cuộc họp"),
        History(MeetingHistoryAction.LegacyTransition, "Đổi trạng thái cuộc họp"),
        History(MeetingHistoryAction.Start, "Bắt đầu cuộc họp"),
        History(MeetingHistoryAction.End, "Kết thúc cuộc họp"),
        History(MeetingHistoryAction.Cancel, "Huỷ cuộc họp"),
        History(MeetingHistoryAction.AutoCancel, "Hệ thống tự huỷ cuộc họp"),

        Participant(ParticipantOperation.AddInternal, "Thêm thành phần"),
        Participant(ParticipantOperation.AddGuest, "Mời Khách"),
        Participant(ParticipantOperation.ChangeAssignments, "Đổi vai trò trong cuộc họp"),
        Participant(ParticipantOperation.HandOverChair, "Giao quyền chủ toạ"),
        Participant(ParticipantOperation.RemoveInternal, "Gỡ thành phần"),
        Participant(ParticipantOperation.RevokeGuest, "Thu hồi lời mời Khách"),
        Participant(ParticipantOperation.Respond, "Trả lời lời mời họp"),
        new(AuditActions.MeetingAttendanceRecorded, "Điểm danh", Participants),
        new(AuditActions.CallGuestAdmitted, "Khách vào phòng họp", Participants),

        new(AuditActions.DocumentViewed, "Xem tài liệu cuộc họp Mật", Documents),
        new(AuditActions.PersonalLibraryRead, "Quản trị đọc kho tài liệu cá nhân", Documents),

        new(AuditActions.VoteDraftDeletedOnMeetingEnd, "Xoá biểu quyết nháp khi kết thúc họp", Votes),

        new(AuditActions.MinutesIssued, "Ban hành biên bản", Minutes),

        new(AuditActions.RecordingUploaded, "Tải lên bản ghi", Recordings),
        new(AuditActions.RecordingErased, "Xoá âm thanh bản ghi", Recordings),
        new(AuditActions.RecordingTakeStarted, "Bật micro phòng họp", Recordings),
        new(AuditActions.RecordingTakeSaved, "Lưu đoạn ghi", Recordings),
        new(AuditActions.RecordingTakeDeleted, "Xoá đoạn ghi", Recordings),
        new(AuditActions.RecordingDownloaded, "Tải về bản ghi", Recordings),
        new(AuditActions.TranscriptSpeakerAssigned, "Gán người nói", Recordings),
        new(AuditActions.TranscriptSegmentSpeakerAssigned, "Gán người nói cho một đoạn", Recordings),
        new(AuditActions.TranscriptSegmentEdited, "Sửa đoạn gỡ băng", Recordings),
        new(AuditActions.TranscriptRerun, "Gỡ băng lại", Recordings),
        new(AuditActions.TranscriptExported, "Xuất bản gỡ băng", Recordings),
        new(AuditActions.SpeechClipEdited, "Sửa câu nói", Recordings),
        new(AuditActions.SpeechClipDeleted, "Xoá câu nói", Recordings),
        new(AuditActions.SpeechClipExcluded, "Loại câu nói khỏi biên bản", Recordings),
        new(AuditActions.SpeechClipRestored, "Đưa câu nói trở lại", Recordings),
        new(AuditActions.SpeechClipsExported, "Xuất các câu nói", Recordings),

        new(AuditActions.VoiceConsentGranted, "Đồng ý Voice ID", Voice),
        new(AuditActions.VoiceConsentRenewed, "Đồng ý lại Voice ID", Voice),
        new(AuditActions.VoiceConsentWithdrawn, "Rút đồng ý Voice ID", Voice),
        new(AuditActions.VoiceSampleAdded, "Thêm mẫu giọng", Voice),
        new(AuditActions.VoiceSampleDeleted, "Xoá mẫu giọng", Voice),

        new(AuditActions.AiSettingsUpdated, "Đổi cấu hình AI", Settings),

        new(AuditActions.CalendarShareRequested, "Xin xem lịch", CalendarSharing),
        new(AuditActions.CalendarShareShared, "Tự chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareAccepted, "Chấp nhận chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareDeclined, "Từ chối chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareChangeRequested, "Xin thay đổi chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareChangeAccepted, "Duyệt thay đổi chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareChangeDeclined, "Từ chối thay đổi chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareUpdated, "Chủ lịch đổi chia sẻ", CalendarSharing),
        new(AuditActions.CalendarShareWithdrawn, "Rút yêu cầu chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareChangeWithdrawn, "Rút thay đổi chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareEnded, "Ngừng chia sẻ lịch", CalendarSharing),
        new(AuditActions.CalendarShareStopped, "Dừng chia sẻ lịch (mất quyền hoặc vô hiệu hoá)", CalendarSharing),
        new(AuditActions.CalendarShareClosedAfterEndDate, "Đóng chia sẻ lịch đã quá hạn khi có chia sẻ mới", CalendarSharing),
        // SCRUM-38 (BR-438): only the export is recorded, not viewing the report.
        new(AuditActions.ReportExported, "Xuất báo cáo cuộc họp", Reports),
    ];

    private static readonly Dictionary<string, AuditEventType> ByCode = All.ToDictionary(e => e.Code, StringComparer.Ordinal);

    public static bool Contains(string code) => ByCode.ContainsKey(code);

    /// <summary>The event's name, or the code itself for one written before it had a name.</summary>
    public static string Label(string code) => ByCode.TryGetValue(code, out var e) ? e.Label : code;

    private static AuditEventType History(MeetingHistoryAction action, string label)
        => new(AuditActions.MeetingHistory(action), label, Meetings);

    private static AuditEventType Participant(ParticipantOperation operation, string label)
        => new(AuditActions.MeetingParticipants(operation), label, Participants);
}
