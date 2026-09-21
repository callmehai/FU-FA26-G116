# 03 · Hợp đồng API

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Đã có:

- [`01-xac-thuc.md`](01-xac-thuc.md) — đăng nhập bằng email + mật khẩu (UC-01 / SCRUM-11):
  `/api/auth/login`, `/refresh`, `/logout`, `/me`; access token và refresh token; mã `MSG01`,
  `MSG03`, `MSG04`

Dự kiến có thêm:

- `02-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `03-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `04-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
