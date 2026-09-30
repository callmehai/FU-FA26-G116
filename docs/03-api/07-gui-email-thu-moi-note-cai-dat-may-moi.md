# Note chạy gửi email trên máy mới — SCRUM-15

Tài liệu này là checklist thiết lập một máy phát triển mới. Không chép `secrets.json`, `.env` hoặc
mật khẩu từ máy khác và không commit credential vào Git.

## 1. Phần mềm cần có

- .NET SDK theo `global.json` (hiện là .NET 10.0.401 hoặc bản minor tương thích).
- Docker Desktop đang chạy.
- Node.js nếu cần chạy frontend.

MailKit đã là dependency của project, `dotnet restore` sẽ tự tải; không cần cài riêng. Queue email
dùng PostgreSQL, không cần RabbitMQ hoặc Hangfire.

## 2. Khởi động PostgreSQL và Redis

Tại thư mục gốc repository:

```powershell
Copy-Item .env.example .env
```

Mở `.env`, thay `POSTGRES_PASSWORD` bằng mật khẩu riêng của máy đó, rồi chạy:

```powershell
docker compose up -d
docker compose ps
```

Hai service `postgres` và `redis` phải ở trạng thái healthy. `.env` đã được gitignore.

## 3. Cấu hình database và xác thực

Chạy tại thư mục gốc repository; thay các placeholder bằng giá trị của máy đang cài:

```powershell
dotnet user-secrets set "ConnectionStrings:TrustCabinet" "Host=localhost;Port=5432;Database=trustcabinet;Username=trustcabinet;Password=<POSTGRES_PASSWORD_TRONG_FILE_ENV>" --project backend/TrustCabinet.Web
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project backend/TrustCabinet.Web
dotnet user-secrets set "Authentication:SigningKey" "<CHUOI_NGAU_NHIEN_IT_NHAT_32_KY_TU>" --project backend/TrustCabinet.Web
```

Mật khẩu trong connection string phải trùng `POSTGRES_PASSWORD` của `.env`. Nếu đổi mật khẩu trong
`.env` sau khi volume PostgreSQL đã được tạo, container không tự đổi mật khẩu của user cũ; khi đó
phải đổi password trong PostgreSQL hoặc tạo lại database dev có chủ đích.

## 4. Cấu hình SMTP

Ví dụ Gmail dùng cổng 587 và STARTTLS. `Email:Smtp:Password` phải là App Password dành cho SMTP,
không dùng mật khẩu đăng nhập Gmail thông thường. Email phải là chuỗi thuần, không ghi dạng
`[email](mailto:email)`.

```powershell
dotnet user-secrets set "Email:Smtp:Host" "smtp.gmail.com" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Port" "587" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Security" "StartTls" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Username" "<EMAIL_GUI>" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Password" "<APP_PASSWORD>" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:FromAddress" "<EMAIL_GUI>" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:FromName" "TrustCabinet" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:ApplicationBaseUrl" "http://localhost:5173" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Enabled" "true" --project backend/TrustCabinet.Web
```

Đặt `Enabled=true` cuối cùng, sau khi các giá trị còn lại đã đầy đủ. Code hiện dùng `Security`, không
dùng key cũ `UseSsl`. Với nhà cung cấp khác, thay host/port/security theo tài liệu của nhà cung cấp;
không cần sửa code.

## 5. Build và chạy

```powershell
dotnet restore
dotnet build TrustCabinet.sln -warnaserror
dotnet run --project backend/TrustCabinet.Web
```

Profile mặc định chạy ở `Development`; backend tự áp dụng migration. Log phải có dòng:

```text
The invitation email delivery worker started.
```

Kiểm tra API ở terminal khác:

```powershell
Invoke-RestMethod http://localhost:5225/api/health
```

Không cần chạy `dotnet ef database update` cho luồng thông thường. Design-time factory của repo dùng
biến `TRUSTCABINET_CONNECTION`, không đọc User Secrets; vì vậy gọi `dotnet ef` trực tiếp mà không đặt
biến này sẽ báo thiếu password dù `dotnet run` vẫn đọc secret đúng.

## 6. Điều kiện để có email thật

- `Email:Smtp:Enabled` là `true` trước khi backend khởi động.
- Người tham dự có địa chỉ email thật và hợp lệ.
- Máy cho phép kết nối outbound tới SMTP host/port.
- Luồng công bố của SCRUM-19 gọi
  `IMeetingInvitationQueue.QueueForPublishedMeetingAsync(meetingId)` trong cùng transaction chuyển
  meeting sang `Published`.

Nếu SCRUM-19 chưa tích hợp thì worker vẫn chạy nhưng không có job mới để gửi.

## 7. Cấu hình khi deploy

Server không dùng User Secrets. Đặt secret bằng secret manager của nền tảng hoặc biến môi trường:

```text
ConnectionStrings__TrustCabinet=...
ConnectionStrings__Redis=...
Authentication__SigningKey=...
Email__Smtp__Enabled=true
Email__Smtp__Host=...
Email__Smtp__Port=587
Email__Smtp__Security=StartTls
Email__Smtp__Username=...
Email__Smtp__Password=...
Email__Smtp__FromAddress=...
Email__Smtp__FromName=TrustCabinet
Email__ApplicationBaseUrl=https://<TEN_MIEN_FRONTEND>
```

Không đặt credential thật trong `appsettings.json`, `appsettings.Development.json`, `.env.example`
hoặc bất kỳ file nào được Git theo dõi.

## 8. Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| Không thấy log worker | `Email:Smtp:Enabled` phải là `true`, sau đó restart backend |
| SMTP báo sai mật khẩu | Dùng App Password/SMTP password, không dùng mật khẩu đăng nhập thông thường |
| Timeout | Kiểm tra host, port, firewall và `Security` |
| Backend báo cấu hình email không hợp lệ | Kiểm tra đủ host, sender, URL, username/password và lease dài hơn `SendAttemptTimeoutSeconds` |
| PostgreSQL báo thiếu password | So sánh connection string trong User Secrets với `.env` |
| Queue ở `Failed` | Xem `attempt_count` và `last_error` trong `email_messages` |
