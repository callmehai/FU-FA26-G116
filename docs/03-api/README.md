# 03 · Hợp đồng API

> Số UC trong các file API theo baseline 70 UC ngày 10/10/2026 — xem [bảng đổi số cũ → mới](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới). Code đã bỏ `MeetingSession` theo [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md) (migration `MeetingOwnsScheduleDropSessions`). Danh mục mã thông báo vẫn ghi số UC cũ ở cột UC.

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Đã có:

- [`00-danh-muc-ma-thong-bao.md`](00-danh-muc-ma-thong-bao.md) — **danh mục mọi mã `MSGnn`**:
  quy tắc đặt mã, mã tiếp theo còn trống, từng mã trả ở endpoint nào với HTTP và câu thông báo gì,
  và bảng đổi mã cũ → mới (kể cả mã trong sequence diagram). Thêm mã mới thì sửa file này trước.
- [`01-xac-thuc.md`](01-xac-thuc.md) — đăng nhập và đăng xuất (UC-01, UC-02, UC-03 / SCRUM-11):
  `/api/auth/login`, `/google`, `/refresh`, `/logout`, `/me`; access token và refresh token; mã
  thông báo `MSG01`–`MSG06`
- [`01-xac-thuc-note-cho-fe.md`](01-xac-thuc-note-cho-fe.md) — note cho FE khi làm SCRUM-46: những
  chỗ dễ làm sai mà lỗi lại trông như bug backend (chữ hoa/thường của `role`, hai kiểu 401, refresh
  token dùng một lần, thứ tự gọi khi đăng xuất)
- [`02-quan-ly-nguoi-dung.md`](02-quan-ly-nguoi-dung.md) — quản lý người dùng (UC-05 … UC-08 /
  SCRUM-13): `/api/users` xem, tạo, sửa và vô hiệu hoá/kích hoạt lại tài khoản; mã `MSG07`–`MSG12`;
  vì sao đổi trạng thái là endpoint riêng, và vì sao vô hiệu hoá phải kết thúc phiên. UC-07 đặt lại
  mật khẩu qua liên kết email (`POST /api/users/{id}/password-reset`, `POST /api/auth/password-reset`,
  mã `MSG13`–`MSG15`, `MSG92`–`MSG94`); UC-08 gán vai trò chạy trên `PUT /api/users/{id}`
- [`postman/`](postman/README.md) — collection Postman kiểm thử API UC-07 và UC-08 trên backend local,
  kèm cách chạy bằng newman
- [`02-quan-ly-nguoi-dung-note-cho-fe.md`](02-quan-ly-nguoi-dung-note-cho-fe.md) — note cho FE khi
  làm màn hình quản lý người dùng: `isActive` bắt buộc gửi đúng tên field, ba nguyên nhân khác nhau
  của cùng mã `MSG12`, và vì sao `403` giữa phiên là chuyện bình thường chứ không phải bug
- [`03-ma-tran-phan-quyen.md`](03-ma-tran-phan-quyen.md) — ma trận vai trò ↔ quyền (UC-10, UC-11 /
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
  `/api/roles/capabilities`, mã `MSG19`–`MSG28`, và phần FE cần làm
- [`05-quan-ly-cuoc-hop.md`](05-quan-ly-cuoc-hop.md) — contract tạo, đọc, sửa và lấy danh sách cuộc
  họp (`SCRUM-17`/`SCRUM-18`, UC-14–UC-17): quyền theo từng cuộc họp, lọc và phân trang ở
  server, validation baseline và các điểm cần BA bổ sung; mã `MSG29`–`MSG38`
- [`06-scrum19-ban-giao-tich-hop.md`](06-scrum19-ban-giao-tich-hop.md) — contract FE/Tester và tích hợp producer SCRUM-15 qua `meetingId`, tạo email job khi SMTP bật
- [`06-thong-bao-trong-ung-dung.md`](06-thong-bao-trong-ung-dung.md) — SCRUM-14: API danh sách,
  đánh dấu đã đọc, kiểm tra đích, SignalR và các nguồn tạo thông báo; mã `MSG39`–`MSG40`; cách
  chuông thông báo ở FE dùng các API và hub đó
- [`07-gui-email-thu-moi.md`](07-gui-email-thu-moi.md) — hàng đợi email bền vững của `SCRUM-15`,
  worker gửi tuần tự, retry và log trạng thái, cấu hình SMTP và điểm tích hợp vào
  transaction công bố của `SCRUM-19`
- [`07-gui-email-thu-moi-note-cho-fe.md`](07-gui-email-thu-moi-note-cho-fe.md) — note bàn giao cho FE:
  ý nghĩa thật của kết quả công bố, route từ email, cách xử lý `MSG45`–`MSG47` và những trạng thái
  queue/SMTP không thuộc contract frontend
- [`07-gui-email-thu-moi-note-cai-dat-may-moi.md`](07-gui-email-thu-moi-note-cai-dat-may-moi.md) —
  checklist cấu hình database, User Secrets, SMTP, migration và kiểm tra worker trên máy mới

Dự kiến có thêm:

- `05-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `06-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `07-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).
