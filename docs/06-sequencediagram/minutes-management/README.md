# Sequence Diagram — Biên bản (FE-12)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho bảy UC của FE-12 đã có code:
UC-58, UC-59, UC-60, UC-61, UC-64, UC-65 và UC-83. UC-62, UC-63 (ký số mô phỏng, SCRUM-37) chưa có sơ đồ.

Đặc tả: [05-use-case-specifications.md §FE-12](../../01-requirements/05-use-case-specifications.md). Hợp đồng API
(`docs/03-api/17-du-thao-bien-ban.md`), ADR-0054/ADR-0055 và ADR-0013 đã có trên `main`. Note BA:
[note 41](../../notes/ban-giao-ba-final-review/41-pr297-scrum-33-duyet-ban-hanh-bien-ban.md).

| Use Case | Source | Hàm trong code |
|---|---|---|
| UC-58 View Meeting Minutes | [UC58_ViewMeetingMinutes.txt](UC58_ViewMeetingMinutes.txt) | `MinutesService.GetAsync`, `GetRevisionAsync`, `ExportAsync` |
| UC-59 Generate Meeting Minutes | [UC59_GenerateMeetingMinutes.txt](UC59_GenerateMeetingMinutes.txt) | `MinutesService.CreateAsync` |
| UC-60 Edit Meeting Minutes (gồm tải Word và PDF) | [UC60_EditMeetingMinutes.txt](UC60_EditMeetingMinutes.txt) | `MinutesService.SaveAsync`, `ExportAsync` |
| UC-61 Submit Meeting Minutes for Approval | [UC61_SubmitMeetingMinutesForApproval.txt](UC61_SubmitMeetingMinutesForApproval.txt) | `MinutesService.SubmitAsync` |
| UC-64 Approve and Issue Meeting Minutes | [UC64_ApproveAndIssueMeetingMinutes.txt](UC64_ApproveAndIssueMeetingMinutes.txt) | `MinutesService.ApproveAsync` |
| UC-65 Return Meeting Minutes for Revision | [UC65_ReturnMeetingMinutesForRevision.txt](UC65_ReturnMeetingMinutesForRevision.txt) | `MinutesService.ReturnAsync` |
| UC-83 Correct Issued Meeting Minutes | [UC83_CorrectIssuedMeetingMinutes.txt](UC83_CorrectIssuedMeetingMinutes.txt) | `MinutesService.CorrectAsync` |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Minutes Controller` là `MinutesEndpoints` (Minimal API), `:Minutes Service` là
  `IMinutesService`, `:Minutes Template` là lớp tĩnh `MinutesTemplate`, `:Minutes Word File` là `MinutesDocx`,
  `:Minutes PDF File` là `MinutesPdf` (QuestPDF).
- Bốn thao tác của SCRUM-33 đi qua `TransitionAsync`: khoá dòng cuộc họp và dòng biên bản (`FOR UPDATE`) trong cùng
  transaction, lưu trạng thái và thông báo trong **một lần lưu** — lưu lỗi thì không đổi gì và không ai nhận thông báo
  (`MSG404`). Tên thông điệp dùng đúng tên hàm code (`SubmitAsync`, `ApproveAsync`, `ReturnAsync`, `CorrectAsync`).
- UC-60 có thêm `opt` tải Word (AF-1) hoặc PDF (AF-2) thay vì một sơ đồ riêng, vì cùng một màn hình và cùng một quyền.
  Bản đã ban hành và bản đã thay thế thì mọi người đọc được cuộc họp đều tải được, trừ Khách (BR-412).
- Bản nháp biên bản **không** lộ cho người ngoài: bản nháp, bản chờ duyệt, bản bị trả lại và bản đính chính chưa ban
  hành trả lời "không tìm thấy" (404, không có mã MSG) với người không phải Chủ toạ/Thư ký; `MSG397` khi họ gửi `PUT`
  hoặc xin đính chính một bản đã ban hành.
- Sơ đồ vẽ theo code ngày 11/11/2026: một mẫu cố định (Mẫu 1.4 Nghị định 30), trình soạn kiểu Word (HTML server lọc),
  xuất Word và PDF, một người duyệt và duyệt là ban hành luôn — không còn bước ký. Bản đính chính tạo đồng thời trả
  `MSG434`; tạo lại bản đính chính từ mẫu trả `MSG456`.

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/minutes-management
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
