# Sequence Diagram — Tài liệu cuộc họp (FE-07)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-07 đã có sequence.
Các UC tài liệu khác (UC-32, UC-33a → UC-33d, …) chưa có sequence.

| Use Case | Source |
|---|---|
| UC-74 Confirm Document Read | [UC74_ConfirmDocumentRead.txt](UC74_ConfirmDocumentRead.txt) |

Xác nhận đã đọc chỉ áp dụng cho tài liệu gắn với một biểu quyết của cuộc họp; checkbox chỉ bật khi người dùng đã cuộn
tới trang cuối (BR-290). Một xác nhận tính cho mọi biểu quyết cùng gắn tài liệu đó, không rút lại được và không gắn
với lựa chọn trong phiếu (BR-291, BR-293). Đặc tả: [Report 3, UC-74](../../08-reports/Report-3_Software_Requirement_Specification.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
