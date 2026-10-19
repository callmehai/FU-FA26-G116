# Agenda Management Sequence Diagrams

Sơ đồ Sequence Diagram cho Feature FE-05 Quản lý chương trình nghị sự: UC-23a → UC-23d (Tạo, Cập nhật, Xoá, Sắp xếp
mục agenda), UC-24 Đặt mục hiện tại, UC-25 Hoàn thành mục, UC-26 Xem chương trình nghị sự, UC-77 Ghi kết luận của mục.

Agenda Item thuộc **trực tiếp** Meeting (không còn Meeting Session — ADR-0013). Secretary được sửa agenda khi Meeting ở
`Draft`, `WaitingApproval`, `Rejected`, `Approved`, `Published` hoặc `InProgress` (BR-65, BR-68, BR-74); Chair đặt / hoàn
thành mục hiện tại khi Meeting `InProgress` (BR-69 → BR-73).

Theo quyết định PO 16/10/2026 và góp ý hội đồng 18/10/2026: mục nghị sự không còn thời lượng dự kiến; Title, Presenter,
Description sửa được ở mọi trạng thái (BR-68); sắp xếp được mọi mục (BR-238); Chair bắt đầu **bất kỳ** mục `Pending`
nào, miễn không có mục khác `InProgress` (BR-70, BR-71) — MSG141 và MSG144 không còn trong sequence. Presenter là
thành phần tham dự hoặc Guest hiện tại và được báo khi cuộc họp đã công bố (BR-306, BR-307). Chair hoặc Secretary ghi
kết luận cho mục `InProgress`/`Done` tới khi sinh nháp biên bản (UC-77, BR-309 → BR-311).
