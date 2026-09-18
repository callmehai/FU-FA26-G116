# Tài liệu TrustCabinet

Nơi chứa mọi thứ **không phải code** của đồ án SEP490-G116: phạm vi, thiết kế, hợp đồng API,
biên bản họp nhóm và các quyết định kỹ thuật.

Viết bằng Markdown, để cùng repo với code để mỗi thay đổi lớn đi kèm commit tương ứng —
tránh cảnh tài liệu trên Drive lệch hẳn với hệ thống thật.

## Cấu trúc

| Thư mục | Chứa gì |
|---|---|
| [00-overview/](00-overview/) | Bối cảnh bài toán, các bên liên quan, thuật ngữ, so sánh với AI-eCabinet |
| [01-requirements/](01-requirements/) | Phạm vi chốt (MoSCoW), user story, tiêu chí nghiệm thu, yêu cầu phi chức năng |
| [02-design/](02-design/) | Kiến trúc hệ thống, sơ đồ lớp/ERD, luồng nghiệp vụ, thiết kế giao diện |
| [03-api/](03-api/) | Hợp đồng REST & SignalR hub — FE và BE thống nhất ở đây trước khi code |
| [04-meetings/](04-meetings/) | Biên bản họp nhóm, họp với giảng viên hướng dẫn, ghi chú sprint |
| [05-decisions/](05-decisions/) | ADR — mỗi quyết định kỹ thuật một file, kèm lý do và phương án đã loại |
| [assets/](assets/) | Ảnh, sơ đồ, file nguồn (`.drawio`, `.excalidraw`) được các tài liệu trên nhúng |

## Quy ước

- **Đặt tên file:** `NN-ten-ngan-gon.md`, số thứ tự để giữ đúng trình tự đọc.
  Ví dụ `01-requirements/02-user-stories.md`.
- **Biên bản họp:** `04-meetings/YYYY-MM-DD-chu-de.md`.
- **ADR:** `05-decisions/NNNN-tieu-de.md`, đánh số tăng dần, không sửa nội dung ADR cũ —
  muốn đổi ý thì viết ADR mới và đánh dấu ADR cũ là *Thay thế bởi*. Dùng
  [mẫu ADR](05-decisions/0000-template.md).
- **Ngày tháng:** ghi tuyệt đối (`2026-09-18`), không ghi "tuần trước", "sprint vừa rồi".
- **Sơ đồ:** commit cả file nguồn lẫn ảnh xuất ra, để người sau còn sửa được.
- **Ngôn ngữ:** tài liệu trong `docs/` viết tiếng Việt; giữ nguyên thuật ngữ kỹ thuật tiếng Anh
  (*endpoint*, *migration*, *hub*…) thay vì dịch nửa vời. Riêng phần trong code — comment,
  docstring, tên PR — viết tiếng Anh, xem bảng quy ước trong [CLAUDE.md](../CLAUDE.md).

## Bắt đầu từ đâu

Người mới vào nhóm đọc theo thứ tự: [00-overview/](00-overview/) →
[01-requirements/](01-requirements/) → [02-design/](02-design/) → [CLAUDE.md](../CLAUDE.md).
