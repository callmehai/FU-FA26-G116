# PR #218 — Gán người nói là Khách (theo AI-eCabinet): việc BA Final Review

**Người ghi:** KhanhPG (BA), 02/11/2026. **Nguồn:** tin nhắn TrustCabinet Bot gửi BA ngày 02/11/2026,
[PR #218](https://github.com/callmehai/FU-FA26-G116/pull/218), danh mục mã (dòng MSG317) và
[13-nhan-dien-giong-noi.md](../../03-api/13-nhan-dien-giong-noi.md). Note này gom việc BA, không thay Jira hay đặc tả.

> **Tiến độ 03/11/2026: ĐÃ SỬA TÀI LIỆU** (Report 3 DOCX → snapshot + extract): UC-46b (gán Khách còn lời mời, `userId`/`guestId`
> tối đa một, EX-1 MSG317 ba trường hợp), UC-45b (tên Khách tự khai trong file xuất), UC-81a (Khách không có Voice ID, không được
> gợi ý), §V.2 mở rộng ngữ cảnh MSG317. **Còn lại:** ERD drawio `transcript_speakers.assigned_guest_id` (xem
> [note 24 §4](24-pr213-215-scrum-59-127-khach-phong-hop.md) — cùng lý do chặn).

## 1. Trạng thái

- PR #218 **đã merge vào `main`** (01/11/2026, stack trên #217).
- **PO 02/11/2026:** mic phòng chuyển chữ giữ đúng như AI-eCabinet và `main` (mỗi lượt mới chuyển chữ lại cả bản ghi phòng). Các commit
  trước trong nhánh (chuyển chữ riêng từng lượt, đoạn mẫu nhận lại người nói, MSG381, lý do xoá — ADR-0044) **đã hoàn tác**; chỉ giữ gán
  người nói là Khách, như AI-eCabinet (`GuestID`). Đừng đưa nội dung ADR-0044 đó vào tài liệu (nó chưa từng lên `main`; số 0044 trên `main` nay là [ADR kiểm cấu trúc PDF](../../05-decisions/0044-server-kiem-cau-truc-va-dem-trang-pdf.md) của PR #224, không liên quan).
- Migration `TranscriptSpeakerGuest`: `transcript_speakers.assigned_guest_id` (FK → `meeting_guest_invitations`), check không gán cùng
  lúc người dùng và Khách.
- Không mã mới; MSG317 mở rộng nghĩa. Danh mục mã và `13-nhan-dien-giong-noi.md` đã ghi sẵn trong PR, lấy từ đó.

## 2. Điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Chỗ BA sửa |
|---|---|---|
| 1 | Thư ký chọn được **Khách đã mời, chưa thu hồi lời mời** (hiện tên Khách tự khai) trong ô gán tên cho giọng nói. `PUT /api/recordings/{id}/speakers/{label}` nhận `{ userId, guestId }`, tối đa một; cả hai null là bỏ gán | **UC-46b**: actor/bước chọn người, danh sách gồm thành viên và Khách còn lời mời |
| 2 | **MSG317** thêm 2 trường hợp: Khách không có lời mời còn hiệu lực của cuộc họp; gửi cả `userId` lẫn `guestId` | UC-46b EX MSG317; Report 3 §V.2 dòng MSG317 (theo danh mục) |
| 3 | Khách **không bao giờ được gợi ý Voice ID** (không có giọng mẫu, không có đồng ý) | UC-46b/UC-81 (Voice ID): ghi rõ; BR Voice ID nếu có câu "gợi ý mọi người dự" |
| 4 | Bản gỡ băng và file xuất dùng tên Khách tự khai (`assignedName`) | UC xuất bản gỡ băng: tên người nói có thể là Khách |

## 3. Việc BA

- [x] UC-46b: điểm 1, 2, 3 (gán Khách còn lời mời; MSG317 ba trường hợp; Khách không được gợi ý vì không có giọng mẫu).
- [x] Report 3 §V.2: dòng MSG317 — ngữ cảnh mở rộng, nội dung giữ nguyên.
- [ ] ERD drawio: `transcript_speakers.assigned_guest_id` — **chưa sửa được**, xem [note 24 §4](24-pr213-215-scrum-59-127-khach-phong-hop.md):
      bảng `transcript_speakers` còn không có trong snapshot `hien-tai` (27 bảng, model 41), và `muc-tieu` là mô hình mục tiêu nên không
      thêm `GuestSession`.
- [x] Rà UC xuất gỡ băng (UC-45b POST-1: tên Khách tự khai) và UC-81 (UC-81a: Voice ID chỉ của người nội bộ) — điểm 3, 4.
- [x] Sync Report 3, Change Log (dòng 03/11/2026 / M / KhanhPG, cùng lượt với note 23, 24).
