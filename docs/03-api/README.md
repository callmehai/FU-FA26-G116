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
- [`04-phan-quyen-phia-server.md`](04-phan-quyen-phia-server.md) — phân quyền phía server
  (SCRUM-12): luật chung mà **mọi** endpoint nghiệp vụ phải theo. Cổng quyền hai lớp (policy trên
  claim + đọc lại từ database) và vì sao một lớp là không đủ; khi nào trả `401`/`403`/`404`; vì sao
  endpoint danh sách phải lọc ở server; các bước thêm một endpoint mới
- [`04-quan-ly-vai-tro.md`](04-quan-ly-vai-tro.md) — quản lý vai trò (`SCRUM-63`, BE đã xong):
  `GET/POST/PUT/DELETE /api/roles` — tạo, đổi tên, xoá vai trò; vai trò là bảng `roles` khoá `Guid`
  thay cho enum `SystemRole` ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)). Migration
  sửa tay giữ dữ liệu cũ, hợp đồng mới `roleId` + `roleName` ở `/api/users`, `/api/auth/*`,
  `/api/roles/capabilities`, mã `MSG19`–`MSG25`, và phần FE cần làm
- [`05-quan-ly-cuoc-hop.md`](05-quan-ly-cuoc-hop.md) — contract tạo, đọc, sửa và lấy danh sách cuộc
  họp (`SCRUM-17`/`SCRUM-18`, UC-11/UC-12/UC-14): quyền theo từng cuộc họp, lọc và phân trang ở
  server, validation baseline và các điểm cần BA bổ sung

Dự kiến có thêm:

- `05-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `06-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `07-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
