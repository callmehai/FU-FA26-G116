# Agenda Management Sequence Diagrams

Sơ đồ Sequence Diagram cho Feature FE-05 Quản lý chương trình nghị sự: UC-23a → UC-23d (Tạo, Cập nhật, Xoá, Sắp xếp
mục agenda), UC-24 Đặt mục hiện tại, UC-25 Hoàn thành mục, UC-26 Xem chương trình nghị sự, UC-27 Ghi kết luận của mục.

Agenda Item thuộc **trực tiếp** Meeting (không còn Meeting Session — ADR-0013). Secretary được sửa agenda khi Meeting ở
`Draft`, `WaitingApproval`, `Rejected`, `Approved`, `Published` hoặc `InProgress` (BR-058, BR-059, UC-23b); Chair đặt / hoàn
thành mục hiện tại khi Meeting `InProgress` (BR-060–BR-062).

Theo quyết định PO 16/10/2026 và góp ý hội đồng 18/10/2026: mục nghị sự không còn thời lượng dự kiến; Title, Presenter,
Description sửa được ở mọi trạng thái (UC-23b); sắp xếp được mọi mục (UC-23d); Chair bắt đầu **bất kỳ** mục `Pending`
nào, miễn không có mục khác `InProgress` (BR-060–BR-062) — MSG141 và MSG144 không còn trong sequence. Presenter là
thành phần tham dự hoặc Guest hiện tại và được báo khi cuộc họp đã công bố (BR-064, BR-065, UC-23a, UC-23b). Chair hoặc Secretary ghi
kết luận cho mục `InProgress`/`Done` tới khi sinh nháp biên bản (UC-27, BR-066, BR-067).

**UC-28 Ghi chú cuộc họp** (SCRUM-30, đặc tả sơ bộ 06/11/2026; BE và FE đã làm theo sequence này 07/11/2026,
[contract](../../03-api/19-ghi-chu-cuoc-hop.md)):
[UC28_TakeMeetingNotes.txt](UC28_TakeMeetingNotes.txt). Đặc tả, BR-134 (đề xuất) và mã `MSG415`–`MSG423`
nằm ở [note đặc tả sơ bộ](../../notes/NOTE-dac-ta-so-bo-scrum-30.md), chưa vào Report 3. Chưa xuất PNG và chưa có tab
draw.io. Lifeline `:Notes Controller` / `:Notes Service` là tên dự kiến.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Meeting Note Repository` (`IMeetingNoteRepository`: `add` hoặc `tryUpdate`) ở UC-28. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.
