# 03 · Hợp đồng API

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Đã có:

- [`01-xac-thuc.md`](01-xac-thuc.md) — đăng nhập và đăng xuất (UC-01, UC-02, UC-03 / SCRUM-11):
  `/api/auth/login`, `/google`, `/refresh`, `/logout`, `/me`; access token và refresh token; mã
  thông báo của từng use case (lưu ý UC-01 và UC-02 đánh số khác nhau)

Dự kiến có thêm:

- `02-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `03-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `04-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
