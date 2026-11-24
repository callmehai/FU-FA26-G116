# Sequence Diagram — Biểu quyết (FE-11)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho toàn bộ UC thuộc FE-11
(UC-62a → UC-67, UC-68a, UC-68b).

| Use Case | Source |
|---|---|
| UC-62a View Meeting Votes | [UC62a_ViewMeetingVotes.txt](UC62a_ViewMeetingVotes.txt) |
| UC-62b View Vote Status | [UC62b_ViewVoteStatus.txt](UC62b_ViewVoteStatus.txt) |
| UC-63 Create / Configure Vote | [UC63_CreateConfigureVote.txt](UC63_CreateConfigureVote.txt) |
| UC-64 Open Vote | [UC64_OpenVote.txt](UC64_OpenVote.txt) |
| UC-65 Cast Vote | [UC65_CastVote.txt](UC65_CastVote.txt) |
| UC-66 Close Vote | [UC66_CloseVote.txt](UC66_CloseVote.txt) |
| UC-67 Record Vote Conclusion | [UC67_RecordVoteConclusion.txt](UC67_RecordVoteConclusion.txt) |
| UC-68a Attach Documents to Vote | [UC68a_AttachDocumentsToVote.txt](UC68a_AttachDocumentsToVote.txt) |
| UC-68b Detach Document from Vote | [UC68b_DetachDocumentFromVote.txt](UC68b_DetachDocumentFromVote.txt) |

Chair hoặc Secretary hiện hành gắn / gỡ tài liệu của biểu quyết `Draft` khi cuộc họp `Approved`, `Published` hoặc
`InProgress` (BR-086, BR-087, BR-094, BR-095). Tài liệu lấy từ tài liệu cuộc họp, tải từ máy hoặc từ Tài liệu của tôi; mọi tài liệu gửi
cùng lúc được gắn cùng lúc hoặc không tài liệu nào (UC-36b). Gỡ khỏi biểu quyết không gỡ khỏi cuộc họp (UC-68b).
Danh sách tài liệu được chốt khi mở biểu quyết. Biểu quyết `Draft` chỉ Chair và Secretary hiện hành thấy (BR-086, BR-087).

Quy ước riêng của nhóm này:

- Mọi UC đọc biểu quyết kiểm tra quyền xem qua `:Meeting Access Policy`; Biểu quyết `Draft` chỉ hiện với Chair
  và Secretary hiện hành (BR-086, BR-087), nên UC-62a lọc ở `:Vote Repository` còn UC-62b trả MSG205 như một Vote không tồn tại.
- Phiếu tách hai bản ghi: `VoteReceipt` (ai đã bỏ phiếu) và `VoteChoice` (chọn gì). Với biểu quyết kín,
  **không ghi dòng nào vào `vote_choices`** — chỉ ghi `VoteReceipt` và tăng `voting_options.ballot_count`
  trong cùng transaction (BR-096, BR-097, UC-65; xem docs 15 §7 và `VoteService.cs:495-496`). Kiểm phiếu đọc bộ đếm này;
  `vote_choices` chỉ còn phiếu công khai, mỗi dòng ghi người chọn.
- Danh sách người đủ điều kiện, số lượng và danh sách tài liệu được chốt khi mở (UC-64); UC-65, UC-62b chỉ đọc lại.
- `ref` (UC-63 mở từ form → UC-64, UC-65 xác nhận đã đọc → UC-42) là khung `ref over` trong source `.txt`; trong
  tab draw.io (tạo từ Mermaid, không có ref) thể hiện bằng một self-message `ref ...`.
- Bước hệ thống kiểm tra đồng thời (BR-090, BR-092, BR-093, BR-099) vẽ thành "Recheck" ngay trong thao tác lưu.

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/voting-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
