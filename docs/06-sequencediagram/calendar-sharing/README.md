# Chia sẻ lịch và tìm giờ họp — FE-15 (sơ bộ, chưa có code)

Các sequence UC-80a → UC-80f, UC-81a → UC-81c tuân theo [convention chung](../README.md). Đặc tả sơ bộ nằm tại
[NOTE-dac-ta-so-bo-scrum-137-142.md](../../notes/NOTE-dac-ta-so-bo-scrum-137-142.md) (SCRUM-142 chia sẻ lịch,
SCRUM-137 tìm giờ họp), quyết định phạm vi ở [ADR-0058](../../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md); hai UC cha UC-80, UC-81 là UC gom, đặc tả nằm ở các UC con.

- Chỉ hiện giờ bận/rảnh: không tên cuộc họp, địa điểm, người dự hay mức mật (BR-140, BR-141); bước lọc trong UC-81a, UC-81c
  chỉ giữ "meetings at shared levels", không nhắc gì khác.
- Khung trùng họp Mật vẫn cho tạo nháp; kiểm lại khi tạo dùng đúng dữ liệu đã chia sẻ (BR-148, D12).
- UC-80a, UC-80b, UC-80d kết thúc ở bước gửi; câu trả lời (chấp nhận/từ chối, duyệt/từ chối thay đổi) là UC-80c.
  UC-81b bắt đầu từ một khung đã tìm ở UC-81a (không vẽ `extend`: tạo nháp là mục tiêu riêng, xem inventory §1).
- Số hiệu dựa trên PR #279 merge trước (UC-75, BR-135–BR-139, `MSG424`–`MSG434`); nếu nhánh này merge
  trước thì #279 dời số. Số UC-57..UC-57d trả lại, không dùng.
- Danh sách sequence (9): UC-80a → UC-80f, UC-81a → UC-81c. Job `SYS_ExpireCalendarShares` đã bỏ theo PO
  09/11/2026 (hết hạn tính lúc đọc, không job, không trạng thái Expired).

| Use Case / Tác nhân | Source |
|---|---|
| UC-80f View Calendar Shares | [UC80f_ViewCalendarShares.txt](UC80f_ViewCalendarShares.txt) |
| UC-80a Request Calendar Share | [UC80a_RequestCalendarShare.txt](UC80a_RequestCalendarShare.txt) |
| UC-80b Share Calendar Directly | [UC80b_ShareCalendarDirectly.txt](UC80b_ShareCalendarDirectly.txt) |
| UC-80c Respond to Calendar Share | [UC80c_RespondToCalendarShare.txt](UC80c_RespondToCalendarShare.txt) |
| UC-80d Change Calendar Share | [UC80d_ChangeCalendarShare.txt](UC80d_ChangeCalendarShare.txt) |
| UC-80e Withdraw or End Calendar Share | [UC80e_WithdrawOrEndCalendarShare.txt](UC80e_WithdrawOrEndCalendarShare.txt) |
| UC-81a Find Meeting Time Slots | [UC81a_FindMeetingTimeSlots.txt](UC81a_FindMeetingTimeSlots.txt) |
| UC-81b Create Draft Meeting from Slot | [UC81b_CreateDraftMeetingFromSlot.txt](UC81b_CreateDraftMeetingFromSlot.txt) |
| UC-81c View Shared Calendar | [UC81c_ViewSharedCalendar.txt](UC81c_ViewSharedCalendar.txt) |

Màn Chia sẻ lịch mở cho mọi tài khoản đang hoạt động; phần "được chia sẻ với tôi", Xin xem lịch, xem tuần và Tìm giờ họp đòi `MeetingCreator` (BR-140, BR-141). Mất quyền thì phần "tôi chia sẻ cho ai" vẫn còn (PO 10/11/2026, L8).

Ngoại lệ hai tương tác Actor → Boundary: UC-80a, UC-80b tải danh sách người để chọn (endpoint `share-candidates`, lọc ở
server, BR-140, BR-141, `MSG448`/`MSG467`) trước nút gửi, nên có hai tương tác như UC-04; UC-80f chỉ xem hai danh sách, không có
danh sách người, nên phần tải này không dồn sang đó. Chi tiết xem [README cha](../README.md) §2.1.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Notification Repository` (`INotificationRepository.add`) giữa `:Notification Service` và `:Database` ở UC-80a → UC-80e. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.

## Xuất ảnh

Một lệnh xuất đủ **hai** ảnh của mỗi sơ đồ — truyền mã UC hoặc đường dẫn `.txt` đã sửa, từ thư mục gốc repository:

```bash
python scripts/export_sequence.py UC84b UC84f
```

1. `drawio-png/<uc>.drawio.png` và tab trong `TrustCabinet_Sequence_Diagrams.drawio` (`txt_to_sequence_drawio_png.py`).
2. `<UC>.png` cạnh file `.txt`, ảnh sequencediagram.org (`export-diagrams.js`, cần truy cập `sequencediagram.org`).

`--only drawio` / `--only seqorg` xuất riêng một bộ. Chỉ truyền file đã sửa: lệnh không nhận thư mục, và không chạy
`scripts/sync_sequence_drawio_png.py` (xuất lại toàn bộ `drawio-png/`, draw.io CLI cắt nửa dưới sơ đồ rất dài như
UC-80b). Mở từng ảnh kiểm tra không cắt; chạy validate của drawio-skill trên file gộp, xác nhận chỉ đổi tab đã sửa.

