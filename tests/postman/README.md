# Postman · kiểm thử API luồng chính

Collection [`TrustCabinet-API.postman_collection.json`](TrustCabinet-API.postman_collection.json) (Postman v2.1)
kiểm thử tích hợp các API chính trên app local: đăng nhập và làm mới token, cuộc họp (tạo, gửi duyệt, duyệt,
công bố, huỷ), chương trình nghị sự, thành phần tham dự, tài liệu (có tải tệp lên), điểm danh, biểu quyết,
thông báo. 35 request, 71 assertion, chạy tuần tự; mỗi request kiểm mã HTTP và mã `MSGnn` theo
[danh mục mã](../../docs/03-api/00-danh-muc-ma-thong-bao.md).

Collection được sinh từ [`build-collection.mjs`](build-collection.mjs): sửa script rồi chạy
`node tests/postman/build-collection.mjs`, không sửa JSON bằng tay.

## Cần gì

- App local chạy ở Development, đã seed (backend `:5225`, Vite `:5173`), SeaweedFS chạy để tải tài liệu
  (`docker compose up -d`). Xem `scripts/dev-up.ps1`.
- Môi trường [`TrustCabinet-local.postman_environment.json`](TrustCabinet-local.postman_environment.json):
  `baseUrl` = `http://localhost:5173` (cùng origin qua proxy Vite), ba tài khoản seed và mật khẩu Development của `DevelopmentDataSeeder`:
  `thanhvien@` (vai trò hệ thống **Thành viên**) tạo và gửi duyệt cuộc họp; `chutoa@` (**Phê duyệt viên**) duyệt cuộc họp;
  `thuky@` (**Phê duyệt viên**) là tài khoản thứ ba. Chủ toạ và Thư ký không phải vai trò hệ thống mà là vai trò **trong cuộc họp**:
  khi tạo họp, `chutoa@` được gán Chủ toạ, `thuky@` được gán Thư ký, `thanhvien@` là Thành viên. `token`,
  `approverToken`, `secretaryToken` để trống: request đăng nhập tự lưu.

## Chạy

Postman: *Import* hai tệp, chọn môi trường `TrustCabinet-local`, *Run collection*. Request API22 tải
`fixtures/tai-lieu-mau.pdf`: trong Postman đặt *Working directory* (Settings → General) là thư mục `tests/postman`,
hoặc chọn lại tệp trong tab *Body*.

```bash
npx newman run tests/postman/TrustCabinet-API.postman_collection.json \
  -e tests/postman/TrustCabinet-local.postman_environment.json --working-dir tests/postman
```

Mỗi lần chạy tạo hai cuộc họp `[Postman] …` mới (một cái bị huỷ, một cái đang diễn ra), nên chạy lặp được.
Kết quả lần chạy 26/11/2026: 35/35 request, 71/71 assertion đạt
([ảnh](../../docs/08-reports/evidence/slides-test/), kèm ảnh chạy trên giao diện Postman).

## Test case tích hợp API

| ID | API | Điều kiện trước | Các bước | Kết quả mong đợi |
|---|---|---|---|---|
| API01 | `POST /api/auth/login` | App local đã seed | Sai mật khẩu → MSG01 — gửi bằng không token, body JSON trong collection | HTTP 401; code = MSG01 |
| API02 | `POST /api/auth/login` | App local đã seed | Đăng nhập Thành viên, người tổ chức (lưu token) — gửi bằng không token, body JSON trong collection | HTTP 200; trả accessToken và user |
| API03 | `POST /api/auth/refresh` | App local đã seed | Làm mới token bằng cookie refresh — gửi bằng không token, không body | HTTP 200; cấp accessToken mới |
| API04 | `GET /api/auth/me` | App local đã seed | Thông tin người đăng nhập — gửi bằng Thành viên, không body | HTTP 200; đúng tài khoản thành viên |
| API05 | `GET /api/meetings` | App local đã seed | Không có token → 401 — gửi bằng không token, không body | HTTP 401 |
| API06 | `POST /api/auth/login` | App local đã seed | Đăng nhập Phê duyệt viên (sẽ làm Chủ toạ cuộc họp) — gửi bằng không token, body JSON trong collection | HTTP 200; trả accessToken và user |
| API07 | `POST /api/auth/login` | App local đã seed | Đăng nhập Phê duyệt viên khác (sẽ làm Thư ký cuộc họp) — gửi bằng không token, body JSON trong collection | HTTP 200; trả accessToken và user |
| API08 | `GET /api/meetings?page=1&pageSize=10` | API02, API06, API07 đã đăng nhập | Danh sách cuộc họp — gửi bằng Thành viên, không body | HTTP 200; có items và totalCount |
| API09 | `POST /api/meetings` | API02, API06, API07 đã đăng nhập | Tạo cuộc họp thiếu tiêu đề → MSG31 — gửi bằng Thành viên, body JSON trong collection | HTTP 400; code = MSG31 |
| API10 | `POST /api/meetings` | API02, API06, API07 đã đăng nhập | Tạo cuộc họp — gửi bằng Thành viên, body JSON trong collection | HTTP 201; code = MSG29; trạng thái Draft |
| API11 | `POST /api/meetings/{{meetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Gửi duyệt cho Phê duyệt viên — gửi bằng Thành viên, body JSON trong collection | HTTP 200; code = MSG78; WaitingApproval |
| API12 | `POST /api/meetings/{{meetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Thành viên không được duyệt cuộc họp → 403 — gửi bằng Thành viên, body JSON trong collection | HTTP 403 |
| API13 | `POST /api/meetings/{{meetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Phê duyệt viên duyệt — gửi bằng Phê duyệt viên, body JSON trong collection | HTTP 200; code = MSG43; Approved |
| API14 | `POST /api/meetings/{{meetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Công bố — gửi bằng Thành viên, body JSON trong collection | HTTP 200; code = MSG45; Published |
| API15 | `POST /api/meetings` | API02, API06, API07 đã đăng nhập | Tạo cuộc họp thứ hai để huỷ — gửi bằng Thành viên, body JSON trong collection | HTTP 201; code = MSG29 |
| API16 | `POST /api/meetings/{{cancelMeetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Huỷ không lý do → MSG262 — gửi bằng Thành viên, body JSON trong collection | HTTP 400; code = MSG262 |
| API17 | `POST /api/meetings/{{cancelMeetingId}}/transitions` | API02, API06, API07 đã đăng nhập | Huỷ cuộc họp — gửi bằng Thành viên, body JSON trong collection | HTTP 200; code = MSG261; Cancelled |
| API18 | `POST /api/meetings/{{meetingId}}/agenda` | Cuộc họp API10 đã Published | Thêm nội dung họp — gửi bằng Phê duyệt viên (Thư ký cuộc họp), body JSON trong collection | HTTP 201; code = MSG60 |
| API19 | `POST /api/meetings/{{meetingId}}/agenda` | Cuộc họp API10 đã Published | Thêm nội dung tiêu đề rỗng → MSG66 — gửi bằng Phê duyệt viên (Thư ký cuộc họp), body JSON trong collection | HTTP 400; code = MSG66 |
| API20 | `GET /api/meetings/{{meetingId}}/agenda` | Cuộc họp API10 đã Published | Đọc chương trình nghị sự — gửi bằng Thành viên, không body | HTTP 200; có 1 nội dung |
| API21 | `GET /api/meetings/{{meetingId}}/participants` | Cuộc họp API10 đã Published | Danh sách thành phần tham dự — gửi bằng Thành viên, không body | HTTP 200; đủ 3 người |
| API22 | `POST /api/meetings/{{meetingId}}/documents/upload` | Cuộc họp API10 đã Published | Tải tài liệu PDF lên — gửi bằng Phê duyệt viên (Thư ký cuộc họp), multipart `files` = tai-lieu-mau.pdf | HTTP 201; code = MSG161; addedCount = 1 |
| API23 | `GET /api/meetings/{{meetingId}}/documents` | Cuộc họp API10 đã Published | Danh sách tài liệu cuộc họp — gửi bằng Thành viên, không body | HTTP 200; có tài liệu vừa tải |
| API24 | `POST /api/meetings/{{meetingId}}/transitions` | Cuộc họp API10 Published, có tài liệu | Chủ toạ bắt đầu cuộc họp — gửi bằng Phê duyệt viên (Chủ toạ cuộc họp), body JSON trong collection | HTTP 200; code = MSG110 |
| API25 | `GET /api/meetings/{{meetingId}}/attendance` | Cuộc họp API10 Published, có tài liệu | Đọc danh sách điểm danh — gửi bằng Phê duyệt viên (Thư ký cuộc họp), không body | HTTP 200 |
| API26 | `POST /api/meetings/{{meetingId}}/attendance` | Cuộc họp API10 Published, có tài liệu | Thư ký lưu điểm danh — gửi bằng Phê duyệt viên (Thư ký cuộc họp), body JSON trong collection | HTTP 200; code = MSG147 |
| API27 | `POST /api/meetings/{{meetingId}}/attendance` | Cuộc họp API10 Published, có tài liệu | Thành viên không được điểm danh → 403 — gửi bằng Thành viên, body JSON trong collection | HTTP 403 |
| API28 | `POST /api/meetings/{{meetingId}}/votes` | Cuộc họp API10 Published, có tài liệu | Chủ toạ tạo và mở biểu quyết — gửi bằng Phê duyệt viên (Chủ toạ cuộc họp), body JSON trong collection | HTTP 200; code = MSG217 |
| API29 | `GET /api/meetings/{{meetingId}}/votes/{{voteId}}` | Cuộc họp API10 Published, có tài liệu | Xem biểu quyết đang mở — gửi bằng Thành viên, không body | HTTP 200; đang Open, có 2 phương án |
| API30 | `POST /api/meetings/{{meetingId}}/votes/{{voteId}}/ballots` | Cuộc họp API10 Published, có tài liệu | Thành viên bỏ phiếu — gửi bằng Thành viên, body JSON trong collection | HTTP 200; code = MSG227 |
| API31 | `POST /api/meetings/{{meetingId}}/votes/{{voteId}}/ballots` | Cuộc họp API10 Published, có tài liệu | Bỏ phiếu lần hai → MSG226 — gửi bằng Thành viên, body JSON trong collection | HTTP 409; code = MSG226 |
| API32 | `POST /api/meetings/{{meetingId}}/votes/{{voteId}}/close` | Cuộc họp API10 Published, có tài liệu | Chủ toạ kết thúc biểu quyết — gửi bằng Phê duyệt viên (Chủ toạ cuộc họp), không body | HTTP 200; code = MSG229 |
| API33 | `GET /api/notifications/?page=1&pageSize=10` | Thành viên đã đăng nhập (API07) | Danh sách thông báo của Thành viên — gửi bằng Thành viên, không body | HTTP 200; có items |
| API34 | `GET /api/notifications/unread-count` | Thành viên đã đăng nhập (API07) | Số thông báo chưa đọc — gửi bằng Thành viên, không body | HTTP 200; là số |
| API35 | `POST /api/notifications/read-all` | Thành viên đã đăng nhập (API07) | Đánh dấu đã đọc tất cả — gửi bằng Thành viên, không body | HTTP 200 |
