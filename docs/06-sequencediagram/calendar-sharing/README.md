# Chia sẻ lịch và tìm giờ họp — FE-16 (sơ bộ, chưa có code)

Các sequence UC-84a → UC-84e, UC-85a → UC-85c tuân theo [convention chung](../README.md). Đặc tả sơ bộ nằm tại
[NOTE-dac-ta-so-bo-scrum-137-142.md](../../notes/NOTE-dac-ta-so-bo-scrum-137-142.md) (SCRUM-142 chia sẻ lịch,
SCRUM-137 tìm giờ họp), quyết định phạm vi ở [ADR-0058](../../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md); hai UC cha UC-84/UC-85 là UC gom, đặc tả nằm ở các UC con.

- Chỉ hiện giờ bận/rảnh: không tên cuộc họp, địa điểm, người dự hay mức mật (BR-427); bước lọc trong UC-85a/UC-85c
  chỉ giữ "meetings at shared levels", không nhắc gì khác.
- Khung trùng họp Mật vẫn cho tạo nháp; kiểm lại khi tạo dùng đúng dữ liệu đã chia sẻ (BR-428, D12).
- UC-84a/UC-84b/UC-84d kết thúc ở bước gửi; câu trả lời (chấp nhận/từ chối, duyệt/từ chối thay đổi) là UC-84c.
  UC-85b `<<extend>>` UC-85a (từ một khung đã tìm mới tạo nháp).
- Số hiệu dựa trên PR #279 merge trước (UC-83, BR-404 → BR-413, `MSG424`–`MSG434`); nếu nhánh này merge
  trước thì #279 dời số. Số UC-86..UC-90 trả lại, không dùng.
- Danh sách sequence (10): UC-84a → UC-84e, `UC84_ViewCalendarShares`, UC-85a → UC-85c, và
  `SYS_ExpireCalendarShares` (tác nhân hệ thống, không phải UC).

| Use Case / Tác nhân | Source |
|---|---|
| UC-84 View Calendar Shares | [UC84_ViewCalendarShares.txt](UC84_ViewCalendarShares.txt) |
| UC-84a Request Calendar Share | [UC84a_RequestCalendarShare.txt](UC84a_RequestCalendarShare.txt) |
| UC-84b Share Calendar Directly | [UC84b_ShareCalendarDirectly.txt](UC84b_ShareCalendarDirectly.txt) |
| UC-84c Respond to Calendar Share | [UC84c_RespondToCalendarShare.txt](UC84c_RespondToCalendarShare.txt) |
| UC-84d Change Calendar Share | [UC84d_ChangeCalendarShare.txt](UC84d_ChangeCalendarShare.txt) |
| UC-84e Withdraw or End Calendar Share | [UC84e_WithdrawOrEndCalendarShare.txt](UC84e_WithdrawOrEndCalendarShare.txt) |
| UC-85a Find Meeting Time Slots | [UC85a_FindMeetingTimeSlots.txt](UC85a_FindMeetingTimeSlots.txt) |
| UC-85b Create Draft Meeting from Slot | [UC85b_CreateDraftMeetingFromSlot.txt](UC85b_CreateDraftMeetingFromSlot.txt) |
| UC-85c View Shared Calendar | [UC85c_ViewSharedCalendar.txt](UC85c_ViewSharedCalendar.txt) |
| System - Expire Calendar Shares | [SYS_ExpireCalendarShares.txt](SYS_ExpireCalendarShares.txt) |

Ngoại lệ actor hệ thống: `SYS_ExpireCalendarShares.txt` dùng actor `System` (theo tiền lệ `UC45c_ProcessTranscriptionQueue.txt`),
không có Boundary, không có người dùng, vì job hết hạn ghi audit và báo hai bên (BR-425, BR-426). Đây là tác nhân hệ thống,
không phải UC, không đánh số UC mới. Chi tiết convention xem [README cha](../README.md) §2.1.

Ngoại lệ hai tương tác Actor → Boundary: UC-84a/UC-84b tải danh sách người để chọn (endpoint `share-candidates`, lọc ở
server, BR-430, `MSG448`/`MSG467`) trước nút gửi, nên có hai tương tác như UC-04; UC-84 chỉ xem hai danh sách, không có
danh sách người, nên phần tải này không dồn sang đó. Chi tiết xem [README cha](../README.md) §2.1.

## Xuất ảnh

Từ thư mục gốc repository:

```bash
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/calendar-sharing/*.txt
```

Lệnh ghép tab vào `TrustCabinet_Sequence_Diagrams.drawio` và xuất ảnh vào `drawio-png/`. Mở từng ảnh kiểm tra
không cắt; chạy validate của drawio-skill trên file gộp, xác nhận chỉ thêm tab mới.