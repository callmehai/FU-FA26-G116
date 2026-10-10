# Tài liệu TrustCabinet

Nơi chứa mọi thứ **không phải code** của đồ án SEP490-G116: phạm vi, thiết kế, hợp đồng API,
biên bản họp nhóm và các quyết định kỹ thuật.

Viết bằng Markdown, để cùng repo với code để mỗi thay đổi lớn đi kèm commit tương ứng —
tránh cảnh tài liệu trên Drive lệch hẳn với hệ thống thật.

## Quy trình làm việc

[DEVELOPMENT_WORKFLOW.md](DEVELOPMENT_WORKFLOW.md) — Sprint 1 tuần, đường ống
BE → FE → BA chốt tài liệu → Tester, Definition of Done. Đọc trước khi bắt tay vào việc đầu tiên.

## Cấu trúc

| Thư mục | Chứa gì |
|---|---|
| [00-overview/](00-overview/) | Bối cảnh bài toán, các bên liên quan, thuật ngữ, so sánh với AI-eCabinet |
| [01-requirements/](01-requirements/) | Danh mục 70 Use Case, đặc tả UC và Business Rules — trích từ Report 3 |
| [02-design/](02-design/) | Kiến trúc hệ thống, sơ đồ lớp/ERD, luồng nghiệp vụ, thiết kế giao diện |
| [03-api/](03-api/) | Hợp đồng REST & SignalR hub — FE và BE thống nhất ở đây trước khi code |
| [04-meetings/](04-meetings/) | Biên bản họp nhóm, họp với giảng viên hướng dẫn, ghi chú sprint |
| [05-decisions/](05-decisions/) | ADR — mỗi quyết định kỹ thuật một file, kèm lý do và phương án đã loại |
| [06-sequencediagram/](06-sequencediagram/) | Sơ đồ tuần tự theo từng UC (tên file theo số UC mới) |
| [07-classdiagram/](07-classdiagram/) | Sơ đồ lớp chi tiết của hệ thống |
| [08-reports/](08-reports/) | Bản Markdown của Report 1, 2, 3 (nguồn là DOCX trên Drive, đồng bộ bằng `scripts/sync_reports.py`) |
| [09-bussinessflow/](09-bussinessflow/) | Swimlane 4 luồng nghiệp vụ chính BF-01 → BF-04 |
| [assets/](assets/) | Ảnh, sơ đồ, file nguồn (`.drawio`, `.excalidraw`) được các tài liệu trên nhúng |

## Quy ước

- **Đặt tên file:** `NN-ten-ngan-gon.md`, số thứ tự để giữ đúng trình tự đọc.
  Ví dụ `02-design/01-vong-doi-cuoc-hop.md`.
- **Biên bản họp:** `04-meetings/YYYY-MM-DD-chu-de.md`.
- **ADR:** `05-decisions/NNNN-tieu-de.md`, đánh số tăng dần, không sửa nội dung ADR cũ —
  muốn đổi ý thì viết ADR mới và đánh dấu ADR cũ là *Thay thế bởi*. Dùng
  [mẫu ADR](05-decisions/0000-template.md).
- **Ngày tháng:** ghi tuyệt đối (`2026-09-18`), không ghi "tuần trước", "sprint vừa rồi".
- **Sơ đồ:** commit cả file nguồn lẫn ảnh xuất ra, để người sau còn sửa được.
- **Ngôn ngữ:** tài liệu trong `docs/` viết tiếng Việt; giữ nguyên thuật ngữ kỹ thuật tiếng Anh
  (*endpoint*, *migration*, *hub*…) thay vì dịch nửa vời. Riêng phần trong code — comment,
  docstring, tên PR — viết tiếng Anh, xem bảng quy ước trong [AGENTS.md](../AGENTS.md).
  Ngoại lệ duy nhất: [DEVELOPMENT_WORKFLOW.md](DEVELOPMENT_WORKFLOW.md) viết tiếng Anh, vì các
  AI coding agent đọc file đó như hướng dẫn vận hành.

## Bắt đầu từ đâu

Người mới vào nhóm đọc theo thứ tự: [DEVELOPMENT_WORKFLOW.md](DEVELOPMENT_WORKFLOW.md) →
[00-overview/](00-overview/) → [09-bussinessflow/](09-bussinessflow/) → [01-requirements/](01-requirements/) →
[02-design/](02-design/) →
[AGENTS.md](../AGENTS.md).
