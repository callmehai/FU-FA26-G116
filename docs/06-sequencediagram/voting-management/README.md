# Sequence Diagram — Biểu quyết (FE-11)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho toàn bộ UC thuộc FE-11
(UC-52a → UC-57, UC-76a, UC-76b).

| Use Case | Source |
|---|---|
| UC-52a View Meeting Votes | [UC52a_ViewMeetingVotes.txt](UC52a_ViewMeetingVotes.txt) |
| UC-52b View Vote Status | [UC52b_ViewVoteStatus.txt](UC52b_ViewVoteStatus.txt) |
| UC-53 Create / Configure Vote | [UC53_CreateConfigureVote.txt](UC53_CreateConfigureVote.txt) |
| UC-54 Open Vote | [UC54_OpenVote.txt](UC54_OpenVote.txt) |
| UC-55 Cast Vote | [UC55_CastVote.txt](UC55_CastVote.txt) |
| UC-56 Close Vote | [UC56_CloseVote.txt](UC56_CloseVote.txt) |
| UC-57 Record Vote Conclusion | [UC57_RecordVoteConclusion.txt](UC57_RecordVoteConclusion.txt) |
| UC-76a Attach Documents to Vote | [UC76a_AttachDocumentsToVote.txt](UC76a_AttachDocumentsToVote.txt) |
| UC-76b Detach Document from Vote | [UC76b_DetachDocumentFromVote.txt](UC76b_DetachDocumentFromVote.txt) |

Chair hoặc Secretary hiện hành gắn / gỡ tài liệu của biểu quyết `Draft` khi cuộc họp `Approved`, `Published` hoặc
`InProgress` (BR-252, BR-288). Tài liệu lấy từ tài liệu cuộc họp, tải từ máy hoặc từ Tài liệu của tôi; mọi tài liệu gửi
cùng lúc được gắn cùng lúc hoặc không tài liệu nào (BR-304). Gỡ khỏi biểu quyết không gỡ khỏi cuộc họp (BR-305).
Danh sách tài liệu được chốt khi mở biểu quyết. Biểu quyết `Draft` chỉ Chair và Secretary hiện hành thấy (BR-253).

Quy ước riêng của nhóm này:

- Mọi UC đọc biểu quyết kiểm tra quyền xem qua `:Meeting Access Policy`; Biểu quyết `Draft` chỉ hiện với Chair
  và Secretary hiện hành (BR-253), nên UC-52a lọc ở `:Vote Repository` còn UC-52b trả MSG205 như một Vote không tồn tại.
- Phiếu tách hai bản ghi: `VoteReceipt` (ai đã bỏ phiếu) và `VoteChoice` (chọn gì). Với biểu quyết kín, `VoteChoice`
  không có người bỏ phiếu và không liên kết tới `VoteReceipt` (BR-259, UC-55).
- Danh sách người đủ điều kiện, số lượng và danh sách tài liệu được chốt khi mở (UC-54); UC-55, UC-52b chỉ đọc lại.
- `ref` (UC-53 mở từ form → UC-54, UC-55 xác nhận đã đọc → UC-74) là khung `ref over` trong source `.txt`; trong
  tab draw.io (tạo từ Mermaid, không có ref) thể hiện bằng một self-message `ref ...`.
- Bước hệ thống kiểm tra đồng thời (BR-266, BR-258, BR-262) vẽ thành "Recheck" ngay trong thao tác lưu.

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/voting-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
