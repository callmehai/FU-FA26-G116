# Postman · kiểm thử API

| Collection | Phạm vi |
|---|---|
| [`SCRUM-26-Attendance.postman_collection.json`](SCRUM-26-Attendance.postman_collection.json) | UC-31; GET rồi chọn entry và POST. Điền biến trong môi trường local; không dùng flow UC-07/08 bên dưới. [Contract](../10-diem-danh.md) |
| [`scrum20-participants.postman_collection.json`](scrum20-participants.postman_collection.json) | Participants; folder `UC-73 — smoke tuần tự` gồm 9 request trên DB riêng, kiểm GET/POST/context/retry/stale/search. Đặt `token` là Chair, `memberToken` là Member duy nhất trong meeting Published/InProgress; không export token. Các request còn lại chạy thủ công theo [bàn giao](../09-thanh-phan-tham-du-ban-giao.md). |
| [`SCRUM-25-Agenda.postman_collection.json`](SCRUM-25-Agenda.postman_collection.json) | UC-23a–d/24/25/26; chạy thủ công theo [hướng dẫn Agenda](../08-chuong-trinh-nghi-su-ban-giao.md), không áp dụng quy trình UC-07/08 bên dưới |
| [`uc07-uc08-quan-ly-nguoi-dung.postman_collection.json`](uc07-uc08-quan-ly-nguoi-dung.postman_collection.json) | UC-07 đặt lại mật khẩu (cả hai nửa) và UC-08 gán vai trò — 35 bước (51 request kể cả request phụ trong script), 73 assertion |

Collection chạy **theo thứ tự** trên backend local, trên **một tài khoản thử cố định**
`uc08-postman@trustcabinet.local`. Tài khoản không xoá được (ERD), nên collection không tạo tài khoản
mới mỗi lần chạy — DB dev không đầy dần:

- lần chạy đầu tiên tạo tài khoản; các lần sau tìm thấy thì bỏ qua bước tạo;
- bước 04–07 đưa nó về trạng thái biết trước: kích hoạt lại, vai trò Thành viên, và đặt mật khẩu ban
  đầu bằng chính luồng UC-07 (lần chạy trước đã đổi mật khẩu của nó);
- bước cuối vô hiệu hoá nó lại.

## Cần gì

1. Backend chạy ở môi trường **Development**, đã seed dữ liệu demo (`quantri@trustcabinet.local` /
   `TrustCabinet@2026`).
2. **SMTP trỏ vào [Mailpit](https://mailpit.axllent.org/)** — một SMTP giả, giữ thư lại cho xem thay
   vì gửi đi. Collection đọc liên kết đặt lại mật khẩu từ API của Mailpit, nên không cần hộp thư
   thật và không có email nào rời khỏi máy.

```bash
docker run -d --name tc-mailpit -p 1025:1025 -p 8025:8025 \
  -e MP_SMTP_AUTH_ACCEPT_ANY=1 -e MP_SMTP_AUTH_ALLOW_INSECURE=1 axllent/mailpit

# Biến môi trường ghi đè user-secrets cho riêng lần chạy này.
Email__Smtp__Enabled=true Email__Smtp__Host=localhost Email__Smtp__Port=1025 \
Email__Smtp__Security=None Email__Smtp__FromAddress=no-reply@trustcabinet.local \
  dotnet run --project backend/TrustCabinet.Web
```

Hộp thư xem được ở <http://localhost:8025>.

## Chạy

Trong Postman: *Import* file JSON rồi *Run collection*. Hoặc bằng dòng lệnh:

```bash
npx newman run docs/03-api/postman/uc07-uc08-quan-ly-nguoi-dung.postman_collection.json
```

Biến của collection (`baseUrl`, `mailpitUrl`, email và mật khẩu demo) sửa được ở tab *Variables*.

## Lưu ý

- Request **44** (hạ vai trò quản trị viên cuối cùng → `MSG12`) chỉ chạy khi tài khoản quản trị viên
  đang đăng nhập là tài khoản **duy nhất** còn hoạt động giữ capability `AdminUsers` (qua bất kỳ vai
  trò nào — đếm giống server, đọc từ `GET /api/roles/capabilities`); nếu không nó tự bỏ qua, vì khi
  đó request sẽ hạ vai trò thật.
- `MSG14` chỉ được thử ở nhánh **tài khoản bị vô hiệu hoá**. Nhánh **tài khoản chưa có mật khẩu**
  (chỉ đăng nhập bằng Google) không nằm trong collection: API không tạo được tài khoản như vậy. Muốn
  thử thì `UPDATE users SET password_hash = NULL, google_subject_id = '<sub>' WHERE …` rồi gọi
  `POST /api/users/{id}/password-reset` — nhận `409 MSG14`. Có test tự động trong `UserEndpointsTests`.
- `MSG15` (không gửi được email) không nằm trong collection: muốn thử thì `docker stop tc-mailpit`
  rồi gọi lại `POST /api/users/{id}/password-reset` — nhận `503 MSG15` sau vài giây.
