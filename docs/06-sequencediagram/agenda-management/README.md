# Agenda Management Sequence Diagrams

Sơ đồ Sequence Diagram cho Feature FE-05 Quản lý chương trình nghị sự: UC-23a → UC-23d (Tạo, Cập nhật, Xoá, Sắp xếp
mục agenda), UC-24 Đặt mục hiện tại, UC-25 Hoàn thành mục, UC-26 Xem chương trình nghị sự.

Agenda Item thuộc **trực tiếp** Meeting (không còn Meeting Session — ADR-0013). Secretary được sửa agenda khi Meeting ở
`Draft`, `WaitingApproval`, `Rejected`, `Approved`, `Published` hoặc `InProgress` (BR-65, BR-68, BR-74); Chair đặt / hoàn
thành mục hiện tại khi Meeting `InProgress` (BR-69 → BR-73).
