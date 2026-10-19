# Sequence Diagram — Biểu quyết (FE-11)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-11 đã có sequence.
UC-52a → UC-57 chưa có sequence.

| Use Case | Source |
|---|---|
| UC-76a Attach Documents to Vote | [UC76a_AttachDocumentsToVote.txt](UC76a_AttachDocumentsToVote.txt) |
| UC-76b Detach Document from Vote | [UC76b_DetachDocumentFromVote.txt](UC76b_DetachDocumentFromVote.txt) |

Chair hoặc Secretary hiện hành gắn / gỡ tài liệu của biểu quyết `Draft` khi cuộc họp `Approved`, `Published` hoặc
`InProgress` (BR-252, BR-288). Tài liệu lấy từ tài liệu cuộc họp, tải từ máy hoặc từ Tài liệu của tôi; mọi tài liệu gửi
cùng lúc được gắn cùng lúc hoặc không tài liệu nào (BR-304). Gỡ khỏi biểu quyết không gỡ khỏi cuộc họp (BR-305).
Danh sách tài liệu được chốt khi mở biểu quyết. Biểu quyết `Draft` chỉ Chair và Secretary hiện hành thấy (BR-253).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/voting-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
