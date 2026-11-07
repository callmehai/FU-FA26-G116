# Sequence Diagram — Bản nháp biên bản (FE-12)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho ba UC của FE-12 đã có code:
UC-58, UC-59, UC-60. UC-61 → UC-65 (gửi ký, ký, trạng thái ký, hoàn tất, yêu cầu sửa) chưa làm nên
chưa có sơ đồ.

Đặc tả: [05-use-case-specifications.md §FE-12](../../01-requirements/05-use-case-specifications.md). Hợp đồng API
(`docs/03-api/17-du-thao-bien-ban.md`) và ADR-0054 chỉ có trên nhánh PR #260. Note BA:
[note 33](../../notes/ban-giao-ba-final-review/33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md).

| Use Case | Source |
|---|---|
| UC-58 View Meeting Minutes | [UC58_ViewMeetingMinutes.txt](UC58_ViewMeetingMinutes.txt) |
| UC-59 Generate Meeting Minutes | [UC59_GenerateMeetingMinutes.txt](UC59_GenerateMeetingMinutes.txt) |
| UC-60 Edit Meeting Minutes (gồm tải Word) | [UC60_EditMeetingMinutes.txt](UC60_EditMeetingMinutes.txt) |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Minutes Controller` là `MinutesEndpoints`, `:Minutes Service` là `IMinutesService`,
  `:Minutes Template` là lớp tĩnh `MinutesTemplate`, `:Minutes Word File` là `MinutesDocx`.
- UC-60 có thêm `opt` tải Word (AF-1) thay vì một sơ đồ riêng, vì cùng một màn hình và cùng một quyền.
- Bản nháp biên bản **không** lộ cho người ngoài: nhánh người không phải Chủ tọa/Thư ký trả lời "không tìm
  thấy" (404, không có mã MSG) khi bản còn nháp, và `MSG397` khi biên bản đã ban hành (SCRUM-33, chưa làm).
- Sơ đồ vẽ theo đặc tả **đã sửa** ngày 05/11/2026: một mẫu cố định (Mẫu 1.4 Nghị định 30), soạn văn bản
  thuần có xem trước, chỉ xuất Word, không có bước gửi ký.

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/minutes-management
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
