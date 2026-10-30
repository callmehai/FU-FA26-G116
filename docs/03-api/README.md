# 03 · Hợp đồng API

> Số UC trong các file API theo baseline 70 UC ngày 10/10/2026 — xem [bảng đổi số cũ → mới](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới). Code đã bỏ `MeetingSession` theo [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md) (migration `MeetingOwnsScheduleDropSessions`). Danh mục mã thông báo vẫn ghi số UC cũ ở cột UC.

FE và BE thống nhất ở đây **trước khi** code, để hai bên chạy song song không phải chờ nhau.

Đã có:

- [SCRUM-69 — bàn giao Start/End](10-scrum69-ban-giao.md), [báo cáo kiểm thử](10-scrum69-kiem-thu.md), [Postman](postman/SCRUM-69-Meeting-Lifecycle.postman_collection.json).
- [`10-diem-danh.md`](10-diem-danh.md) — Backend SCRUM-26 / UC-31; [bàn giao FE/BA](10-diem-danh-ban-giao.md), [kết quả kiểm thử](10-diem-danh-ket-qua-kiem-thu.md). Version riêng từng người, batch atomic, realtime theo quyền roster.

- [`08-chuong-trinh-nghi-su.md`](08-chuong-trinh-nghi-su.md) — contract Backend SCRUM-98, UC-23a–d/24/25/26/77; [bàn giao SCRUM-25 lịch sử](08-chuong-trinh-nghi-su-ban-giao.md).
- [`08-agenda-contract-tich-hop-guest-uc04.md`](08-agenda-contract-tich-hop-guest-uc04.md) — adapter identity/session, quyền đọc Agenda và hub Guest; phần UC-04 cần triển khai để tích hợp.

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
- [`09-phong-hop-truc-tuyen.md`](09-phong-hop-truc-tuyen.md) — phòng họp trực tuyến trên LiveKit
  (`SCRUM-39`/`SCRUM-41`): ai được vào phòng, cấp token, chủ toạ tắt mic, webhook và job đối soát;
  mã `MSG117`, `MSG118`, `MSG123`, `MSG124`, `MSG280`–`MSG284`

- [`09-thanh-phan-tham-du.md`](09-thanh-phan-tham-du.md) — SCRUM-20: roster, Guest invitation, quyền Chair/Secretary, Chair handover và phản hồi lời mời; ETag/validation/delivery/migration
- [`09-thanh-phan-tham-du-ban-giao.md`](09-thanh-phan-tham-du-ban-giao.md) — FE, Postman, fixture và các bước test thủ công
- [`09-thanh-phan-tham-du-guest-uc04.md`](09-thanh-phan-tham-du-guest-uc04.md) — port và gate tích hợp OTP/session Guest production
- [`09-thanh-phan-tham-du-ket-qua-kiem-thu.md`](09-thanh-phan-tham-du-ket-qua-kiem-thu.md) — kết quả build/test, performance và giới hạn nghiệm thu
- [`11-tai-lieu-cuoc-hop.md`](11-tai-lieu-cuoc-hop.md) — SCRUM-107: tải tệp từ máy lên cuộc họp
  (UC-33a AF-1), kiểm loại theo nội dung, tất cả hoặc không, thư mục kho tự tạo, lưu trên R2, job dọn
  file mồ côi; mã `MSG161`, `MSG162`, `MSG164`, `MSG166`, `MSG172`, `MSG174`, `MSG288`; note cho FE.
  SCRUM-21: danh sách tài liệu nhóm theo mục nghị sự (UC-32a), xem / tải về đúng byte gốc, chặn tải về cuộc họp
  Mật, nhật ký kiểm toán Confidential/Secret (UC-32c), event `documentsChanged`; mã `MSG156`–`MSG160`; note cho FE.
  SCRUM-58 (SCRUM-112): thêm từ kho (UC-33a), gắn mục nghị sự ba nguồn (UC-33b), gỡ khỏi cuộc họp (UC-33c), gỡ khỏi
  mục (UC-33d). SCRUM-23: góp ý và trả lời một cấp trên tài liệu (UC-35). Cuộc họp đã huỷ chỉ còn đọc (`MSG349`)
- [`14-kho-tai-lieu-ca-nhan.md`](14-kho-tai-lieu-ca-nhan.md) — SCRUM-58 (SCRUM-110, SCRUM-111): thư mục kho cá nhân
  (UC-72a–d), xem / tải lên / đổi tên / chuyển / xoá tài liệu trong kho (UC-34, UC-36, UC-71a–c), hạn mức 10 GB
  của cả hệ thống (ADR-0034), Quản trị viên đọc kho của tài khoản đã khoá có kiểm toán; mã `MSG175`–`MSG198`, `MSG346`–`MSG348`
- [`10-scrum97-ket-qua-kiem-thu.md`](10-scrum97-ket-qua-kiem-thu.md) — kết quả riêng của phần SCRUM-97 bổ sung trên baseline SCRUM-20; kèm change note Jira để owner đăng.
- [`12-ban-ghi-cuoc-hop.md`](12-ban-ghi-cuoc-hop.md) — SCRUM-29: lưu, nghe (link ký sẵn), xoá bản ghi cuộc họp; đồng ý, mục đích, hạn lưu trữ và job tự xoá; mã `MSG290`–`MSG298`. SCRUM-31: hàng chờ chuyển thành chữ trên worker tự chạy, xem, chạy lại, sửa đoạn; mã `MSG299`–`MSG304`
- [`13-nhan-dien-giong-noi.md`](13-nhan-dien-giong-noi.md) — SCRUM-35: Voice ID của chính mình (đồng ý, giọng mẫu mã hoá, rút đồng ý), gợi ý và gán tên người nói trong bản gỡ băng; nghĩa vụ dữ liệu sinh trắc học; mã `MSG305`–`MSG317`
- [`15-bieu-quyet.md`](15-bieu-quyet.md) — thiết kế contract FE-11 / SCRUM-96 (chưa triển khai): danh sách/nháp/mở/bỏ phiếu/đóng/kết luận, gắn-gỡ tài liệu Vote (UC-76), xác nhận đã đọc (UC-74); mã `MSG205`–`MSG236`, `MSG252`–`MSG255`, `MSG265`–`MSG270`. Note bàn giao: [NOTE-ban-giao-fe11-bieu-quyet-cho-be.md](../notes/NOTE-ban-giao-fe11-bieu-quyet-cho-be.md)

Dự kiến có thêm:

- `05-quy-uoc.md` — quy ước chung: đặt tên đường dẫn, phân trang, định dạng lỗi, mã trạng thái
- `06-rest.md` — từng endpoint: method, path, request/response, quyền yêu cầu
- `07-signalr-hubs.md` — hub, phương thức client/server, payload sự kiện realtime

Khi backend bật OpenAPI, tài liệu sinh tự động đóng vai trò tham chiếu chi tiết; các file ở đây
tập trung vào ý định thiết kế và phần OpenAPI không diễn tả được (luồng, quyền, tình huống lỗi).

SCRUM-98 thay thế contract Agenda SCRUM-25: [bàn giao BE → FE/BA](08-scrum98-ban-giao.md), [báo cáo kiểm thử riêng](08-scrum98-ket-qua-kiem-thu.md). UC-77, presenter Internal/Guest, bỏ duration và reorder mọi trạng thái. Báo cáo SCRUM-25 là lịch sử, không ghi đè.
