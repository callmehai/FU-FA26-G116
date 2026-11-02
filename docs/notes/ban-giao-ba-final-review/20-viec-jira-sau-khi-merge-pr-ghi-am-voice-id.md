# Việc Jira sau khi merge PR BA Final Review ghi âm / Voice ID

**Người ghi:** KhanhPG (BA), 01/11/2026. **Nhánh:** `docs/ba-final-review-ghi-am-voice-id` (đã đẩy lên origin).
**PR:** [#205](https://github.com/callmehai/FU-FA26-G116/pull/205) "docs: BA Final Review for FE-09 (Meeting Recording) and FE-10 (Transcription & Voice ID)", base `main`, **đã merge** (`46a79a5`).
Làm **sau khi PR #205 merge vào `main`**. Trạng thái Jira đọc ngày 01/11/2026.

**Cập nhật 02/11/2026:** mục 1 và 2 đã làm xong. Đã comment cả 8 ticket theo mục 2, rồi chuyển **`In Review`**
(kiểm thử): SCRUM-29, 31, 35, 109; chuyển **`Done`**: SCRUM-102, 103, 125, 126. Comment của 29, 31, 35, 109 liệt kê
luôn các việc mục 3 còn treo. Description của SCRUM-102, 103 chưa sửa, chỉ ghi trong comment là lỗi thời và trỏ sang
note 19. Mục 3 vẫn còn nguyên.

Code của SCRUM-29, 31, 35, 109 đã merge từ 29/10 (PR #184 → #193); PR này chỉ là phần tài liệu BA.

## 1. Ticket cần cập nhật

| Ticket | Hiện tại | Việc sau khi PR merge | Kết quả 02/11/2026 |
|---|---|---|---|
| SCRUM-29 Ghi âm phiên họp | BA Final Review | Comment kết quả đối chiếu (mục 2), chuyển **kiểm thử** | **Đã chuyển In Review** |
| SCRUM-31 Chuyển giọng nói thành văn bản | BA Final Review | Như trên | **Đã chuyển In Review** |
| SCRUM-35 Tách người nói trong bản ghi | BA Final Review | Như trên | **Đã chuyển In Review** |
| SCRUM-109 Ghi âm theo micro từng người | BA Final Review | Như trên | **Đã chuyển In Review** |
| SCRUM-102 Đặc tả UC FE-09 Meeting Recording | To Do | Comment danh sách UC đã đặc tả, chuyển **Done** | **Đã chuyển Done** |
| SCRUM-103 Đặc tả UC FE-10 | To Do | Như trên; ghi UC-47–51 còn Placeholder | **Đã chuyển Done** |
| SCRUM-125 Class Diagram FE-09 (tạo 01/11/2026, dưới SCRUM-55) | To Do | Diagram đã có trong PR #205; comment link `feature-09-…drawio`, chuyển Done | **Đã chuyển Done** |
| SCRUM-126 Class Diagram FE-10 (tạo 01/11/2026, dưới SCRUM-55) | To Do | Diagram đã có trong PR #205; comment link `feature-10-…drawio`, chuyển Done | **Đã chuyển Done** |

SCRUM-102 và 103 đã đổi title ngày 01/11/2026. Description của hai ticket vẫn viết theo yêu cầu cũ
(FE-09 "Upload audio & Thư viện media", "không dùng Voice ID", NĐ 13/2023); sửa lại hoặc ghi một dòng
"description lỗi thời, xem note 19" trước khi đóng.

## 2. Nội dung comment (gửi cho 29, 31, 35, 109; 102 và 103 chỉ lấy dòng tương ứng)

- UC: FE-09 UC-43a–c, 44a–e, 80; FE-10 UC-45a–c, 46a–d, 81a–f; UC-39a thêm AF ghi theo câu. UC-47–51 giữ Placeholder.
- BR-340 → BR-370 (BR-370 còn **NEEDS REVIEW PO**: Guest họp Online không có lời trong bản ghi).
- System Messages: MSG290–MSG323 và MSG351–MSG367 vào Report 3 §V.2; MSG301, MSG320 đổi nghĩa;
  MSG324–327, MSG344 ngừng dùng.
- 24 sequence và 2 class diagram as-built (feature-09, feature-10).
- Lệch với yêu cầu ban đầu, phải ghi vào ticket (AGENTS.md, DEVELOPMENT_WORKFLOW §2.1): ghi âm suy ra từ hình thức họp,
  bỏ chọn chế độ thu; lưu bản ghi giọng mẫu mã hoá (thay "chỉ lưu vector" của ADR-0027).

## 3. Chưa xong, đừng đóng ticket nếu còn

- Đối chiếu `docs/03-api/12-ban-ghi-cuoc-hop.md`, `13-nhan-dien-giong-noi.md`, `05-quan-ly-cuoc-hop.md` với UC.
- Sửa 4 note cũ theo [note 16 §4](16-ghi-am-voice-id-28-29-10-2026.md#4-note-cũ-đã-lỗi-thời).
- Report 3 §III màn hình (tab Bản ghi, thẻ mic phòng, Ghi âm của tôi, Giọng nói của tôi) còn tên FE cũ: làm qua wireframe,
  ngoài phạm vi plan 19 §9. Không chặn chuyển kiểm thử, nhưng ghi vào comment.
- Đổi số FE-11 → FE-16 (tách Meeting Summary) đang **hoãn**, chờ BA báo PO.
