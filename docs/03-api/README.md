# 03 · Hợp đồng API

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Dự kiến có:

- `01-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `02-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `03-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
