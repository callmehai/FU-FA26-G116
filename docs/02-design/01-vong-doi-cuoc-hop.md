# Vòng đời cuộc họp

Trạng thái của một cuộc họp, theo đúng thứ tự nó đi qua. Đây là xương sống của toàn bộ nghiệp
vụ — gần như mọi màn hình đều đọc hoặc thay đổi trạng thái này.

Tham chiếu từ hệ thống AI-eCabinet (xem [ADR-0002](../05-decisions/0002-khung-giao-dien-theo-ai-ecabinet.md)).
Mã trạng thái giữ nguyên tiếng Anh để dùng trực tiếp trong code; nhãn tiếng Việt chỉ dùng khi hiển thị.

| Mã | Nhãn hiển thị | Ý nghĩa |
|---|---|---|
| `Draft` | Nháp | Organizer và Thư ký đang soạn; người duyệt, Chair, thành viên chưa thấy |
| `WaitingApproval` | Chờ duyệt | Đã gửi, đang đợi người duyệt được phân công |
| `Rejected` | Bị từ chối | Người duyệt trả lại kèm lý do; Organizer sửa rồi gửi lại |
| `Approved` | Đã duyệt | Được chấp thuận, chưa thông báo cho thành phần tham dự |
| `Published` | Đã công bố | Đã gửi thư mời; Organizer chỉ còn dời được giờ, địa điểm, link (*Reschedule*) |
| `InProgress` | Đang diễn ra | Chủ toạ đã bắt đầu cuộc họp; agenda chạy, biểu quyết mở được |
| `Ended` | Đã kết thúc | Cuộc họp đóng; xử lý hậu kỳ (audio, transcript, tóm tắt, biên bản) |

```
Draft ──gửi duyệt──> WaitingApproval ──duyệt──> Approved ──công bố──> Published ──bắt đầu──> InProgress ──kết thúc──> Ended
  ^                        │                                           │  ^
  └──sửa, gửi lại── Rejected <──từ chối                                └──┘ dời lịch (Reschedule)
```

Vòng đời PO chốt kết thúc ở `Ended` (quyết định 09/10/2026). Trạng thái của biên bản nằm ở `MeetingMinutes.Status`
(`Draft → PendingSignature → RevisionRequested → Sealed`), không phải trạng thái cuộc họp. **Không có Meeting Session**:
một cuộc họp là một buổi họp, tự mang `PlannedStartTime` / `PlannedEndTime` / `ActualStartedAt` / `ActualEndedAt`
([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).

Code đã khớp: enum `MeetingStatus` (backend và [types/meeting.ts](../../frontend/src/types/meeting.ts)) chỉ còn bảy
trạng thái trên. Migration `MeetingOwnsScheduleDropSessions` chuyển dữ liệu `MinutesIssued` / `Archived` cũ thành `Ended`
và drop bảng `meeting_sessions`. Dời lịch sau công bố ghi history `Reschedule` (`Published → Published`).

Khi gửi duyệt theo UC-18, người tổ chức chọn một tài khoản đang hoạt động có capability
`MeetingApprover`. Trường `AssignedApproverId` được để trống trong bản nháp cho tới bước này.

## Vì sao cần ghi lại

- **Phân quyền bám theo trạng thái.** Sửa thông tin chung chỉ được phép ở `Draft` và `Rejected`;
  ở `Published` chỉ dời lịch (BR-27, BR-158). Agenda
  sửa được tới hết `InProgress` (BR-74). Biểu quyết chỉ tạo và mở khi `InProgress` (BR-160). Biên bản chỉ sinh được
  sau `Ended`. Kết thúc cuộc họp không bị chặn bởi Vote Open hoặc điểm danh thiếu (BR-159); Ended ngừng nhận phiếu, Chair có thể đóng/kiểm phiếu cũ sau họp.
- **Lịch sử trạng thái là bằng chứng.** Màn chi tiết hiển thị timeline ai chuyển trạng thái, lúc
  nào. Đây là dữ liệu kiểm toán, không phải log kỹ thuật — không được xoá hay sửa.
- **Nhãn trong timeline khác nhãn trạng thái.** Timeline ghi *việc đã xảy ra* ("Đã gửi duyệt"),
  còn ô trạng thái ghi *cuộc họp đang là gì* ("Chờ duyệt"). Cùng một mã, hai cách diễn đạt.

## Mức độ mật

Độc lập với trạng thái, quyết định ai đọc được tài liệu và bản ghi.

| Mã | Nhãn | Ghi chú |
|---|---|---|
| `Normal` | Thường | Mặc định |
| `Internal` | Nội bộ | Trong nội bộ tổ chức |
| `Confidential` | Hạn chế | Chỉ thành phần tham dự |
| `Secret` | Mật | Hiển thị cảnh báo trên màn chi tiết; cân nhắc chặn tải về |

Hiện thực phía frontend: [types/meeting.ts](../../frontend/src/types/meeting.ts).
