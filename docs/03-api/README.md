# 03 · Hợp đồng API

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Đã có:

- [`01-xac-thuc.md`](01-xac-thuc.md) — đăng nhập và đăng xuất (UC-01, UC-02, UC-03 / SCRUM-11):
  `/api/auth/login`, `/google`, `/refresh`, `/logout`, `/me`; access token và refresh token; mã
  thông báo của từng use case (lưu ý UC-01 và UC-02 đánh số khác nhau)
- [`01-xac-thuc-note-cho-fe.md`](01-xac-thuc-note-cho-fe.md) — note cho FE khi làm SCRUM-46: những
  chỗ dễ làm sai mà lỗi lại trông như bug backend (chữ hoa/thường của `role`, hai kiểu 401, refresh
  token dùng một lần, thứ tự gọi khi đăng xuất)
- [`02-quan-ly-nguoi-dung.md`](02-quan-ly-nguoi-dung.md) — quản lý người dùng (UC-04 … UC-07 /
  SCRUM-13): `/api/users` xem, tạo, sửa và vô hiệu hoá/kích hoạt lại tài khoản; mã `MSG07`–`MSG12`;
  vì sao đổi trạng thái là endpoint riêng, và vì sao vô hiệu hoá phải kết thúc phiên. UC-08 (đặt
  lại mật khẩu) chưa làm — chờ hạ tầng email

Dự kiến có thêm:

- `04-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `03-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `04-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
