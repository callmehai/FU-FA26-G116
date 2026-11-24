# Sequence Diagram — Biên bản (FE-12)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho bảy UC của FE-12 đã có code:
UC-69, UC-70, UC-71, UC-72, UC-73, UC-74, UC-75. UC-62 cũ, UC-63 cũ đã bỏ (PO 14/11/2026 bỏ ký số kể cả mô phỏng, SCRUM-37 đóng) nên không có sơ đồ.

Đặc tả: [05-use-case-specifications.md §FE-12](../../01-requirements/05-use-case-specifications.md). Hợp đồng API
(`docs/03-api/17-du-thao-bien-ban.md`), ADR-0054/ADR-0055 và ADR-0013 đã có trên `main`. Note BA:
[note 41](../../notes/ban-giao-ba-final-review/41-pr297-scrum-33-duyet-ban-hanh-bien-ban.md).

| Use Case | Source | Hàm trong code |
|---|---|---|
| UC-69 View Meeting Minutes | [UC69_ViewMeetingMinutes.txt](UC69_ViewMeetingMinutes.txt) | `MinutesService.GetAsync`, `GetRevisionAsync`, `ExportAsync` |
| UC-70 Generate Meeting Minutes | [UC70_GenerateMeetingMinutes.txt](UC70_GenerateMeetingMinutes.txt) | `MinutesService.CreateAsync` |
| UC-71 Edit Meeting Minutes (gồm tải Word và PDF) | [UC71_EditMeetingMinutes.txt](UC71_EditMeetingMinutes.txt) | `MinutesService.SaveAsync`, `ExportAsync` |
| UC-72 Submit Meeting Minutes for Approval | [UC72_SubmitMeetingMinutesForApproval.txt](UC72_SubmitMeetingMinutesForApproval.txt) | `MinutesService.SubmitAsync` |
| UC-73 Approve and Issue Meeting Minutes | [UC73_ApproveAndIssueMeetingMinutes.txt](UC73_ApproveAndIssueMeetingMinutes.txt) | `MinutesService.ApproveAsync` |
| UC-74 Return Meeting Minutes for Revision | [UC74_ReturnMeetingMinutesForRevision.txt](UC74_ReturnMeetingMinutesForRevision.txt) | `MinutesService.ReturnAsync` |
| UC-75 Correct Issued Meeting Minutes | [UC75_CorrectIssuedMeetingMinutes.txt](UC75_CorrectIssuedMeetingMinutes.txt) | `MinutesService.CorrectAsync` |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Minutes Endpoints` là `MinutesEndpoints` (Minimal API), `:Minutes Service` là
  `IMinutesService`, `:Minutes Template` là lớp tĩnh `MinutesTemplate`, `:Minutes Word File` là `MinutesDocx`,
  `:Minutes PDF File` là `MinutesPdf` (QuestPDF).
- Bốn thao tác của SCRUM-33 đi qua `TransitionAsync`: khoá dòng cuộc họp và dòng biên bản (`FOR UPDATE`) trong cùng
  transaction, lưu trạng thái và thông báo trong **một lần lưu** — lưu lỗi thì không đổi gì và không ai nhận thông báo
  (`MSG404`). Tên thông điệp dùng đúng tên hàm code (`SubmitAsync`, `ApproveAsync`, `ReturnAsync`, `CorrectAsync`).
- UC-71 có thêm `opt` tải Word (AF-1) hoặc PDF (AF-2) thay vì một sơ đồ riêng, vì cùng một màn hình và cùng một quyền.
  Bản đã ban hành và bản đã thay thế thì mọi người đọc được cuộc họp đều tải được, trừ Khách (BR-139).
- Bản nháp biên bản **không** lộ cho người ngoài: bản nháp, bản chờ duyệt, bản bị trả lại và bản đính chính chưa ban
  hành trả lời "không tìm thấy" (404, không có mã MSG) với người không phải Chủ toạ/Thư ký; `MSG397` khi họ gửi `PUT`
  hoặc xin đính chính một bản đã ban hành.
- Sơ đồ vẽ theo code ngày 11/11/2026: một mẫu cố định (Mẫu 1.4 Nghị định 30), trình soạn kiểu Word (HTML server lọc),
  xuất Word và PDF, một người duyệt và duyệt là ban hành luôn — không còn bước ký. Bản đính chính tạo đồng thời trả
  `MSG434`; tạo lại bản đính chính từ mẫu trả `MSG456`.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Minutes Repository` (`IMeetingMinutesRepository`) và `:Minutes Source Query` (`IMinutesSourceQuery`, UC-70). Bước khoá ở UC-72, UC-73, UC-74, UC-75 giữ một message "Lock the Meeting and the revision" qua `:Minutes Repository`; trong code Meeting được khoá qua `IMeetingRepository.FindWorkflowAsync(forUpdate)` rồi bản biên bản qua `IMeetingMinutesRepository.LockAsync`, sơ đồ gộp hai bước để giữ nguyên câu cũ. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.

## Xuất ảnh

Từ thư mục gốc repository, sau khi sửa `.txt` — chỉ truyền file đã sửa:

```bash
python scripts/export_sequence.py UC58 UC59 UC60 UC61 UC64 UC65 UC83
```

Lệnh xuất cả ảnh sequencediagram.org cạnh `.txt` lẫn tab trong
`TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/`. Mở từng ảnh kiểm tra không cắt.
