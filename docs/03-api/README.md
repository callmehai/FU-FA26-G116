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
- [`02-quan-ly-nguoi-dung-note-cho-fe.md`](02-quan-ly-nguoi-dung-note-cho-fe.md) — note cho FE khi
  làm màn hình quản lý người dùng: `isActive` bắt buộc gửi đúng tên field, ba nguyên nhân khác nhau
  của cùng mã `MSG12`, và vì sao `403` giữa phiên là chuyện bình thường chứ không phải bug
- [`03-ma-tran-phan-quyen.md`](03-ma-tran-phan-quyen.md) — ma trận vai trò ↔ quyền (UC-09, UC-10 /
  SCRUM-60): `GET`/`PUT /api/roles/capabilities`; mã `MSG16`–`MSG18`; luật chống tự khoá hệ thống,
  vì sao đổi ma trận phải kết thúc phiên, và năm chỗ code khác class diagram cần BA sửa. Kèm cảnh
  báo về bước seed sau khi bỏ `HasData` ([ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md))
- [`03-ma-tran-phan-quyen-note-cho-fe.md`](03-ma-tran-phan-quyen-note-cho-fe.md) — note cho FE khi
  làm màn hình phân quyền: `capabilities` là trạng thái cuối chứ không phải delta, mã quyền
  PascalCase khác bảng cứng đang có trong `user.ts`, vì sao đừng tự đoán luật chống tự khoá ở FE,
  và vì sao route phải trả `404` trong khi API trả `403`

Dự kiến có thêm:

- `04-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `05-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `06-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
