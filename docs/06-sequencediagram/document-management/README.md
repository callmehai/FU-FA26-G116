# Sequence Diagram — Tài liệu cuộc họp (FE-07)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-07 đã có sequence.
Các UC tài liệu khác (UC-33a → UC-33d, UC-34, UC-36, UC-71, UC-72) chưa có sequence.

| Use Case | Source | Ticket |
|---|---|---|
| UC-32a View Meeting Documents | [UC32a_ViewMeetingDocuments.txt](UC32a_ViewMeetingDocuments.txt) | SCRUM-21, SCRUM-22 |
| UC-32c Preview Meeting Document | [UC32c_PreviewMeetingDocument.txt](UC32c_PreviewMeetingDocument.txt) | SCRUM-21, SCRUM-22 |
| UC-35 Comment on Meeting Document | [UC35_CommentOnMeetingDocument.txt](UC35_CommentOnMeetingDocument.txt) | SCRUM-23 |
| UC-74 Confirm Document Read | [UC74_ConfirmDocumentRead.txt](UC74_ConfirmDocumentRead.txt) | — |

UC-32a, UC-32c, UC-35 vẽ ngày 22/10/2026 theo đặc tả sửa cùng ngày (nhóm theo mục nghị sự, panel Tài liệu
trong phòng họp, trả lời góp ý, BR-315 → BR-318). Boundary của UC-32a gộp tab Tài liệu và panel trong phòng vì
cùng một request; nhánh AF-3 chỉ đổi thứ tự hiển thị. Source, PNG, tab trong file draw.io gộp và ảnh
`drawio-png/` đã có (tab thêm 23/10/2026 bằng bộ chuyển Mermaid của draw.io, style chuẩn hoá như tab UC-74).

Xác nhận đã đọc chỉ áp dụng cho tài liệu gắn với một biểu quyết của cuộc họp; checkbox chỉ bật khi người dùng đã cuộn
tới trang cuối (BR-290). Một xác nhận tính cho mọi biểu quyết cùng gắn tài liệu đó, không rút lại được và không gắn
với lựa chọn trong phiếu (BR-291, BR-293). Đặc tả: [Report 3, UC-74](../../08-reports/Report-3_Software_Requirement_Specification.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
