# Gửi email thư mời cuộc họp — SCRUM-15

SCRUM-15 không thêm HTTP endpoint riêng. Luồng công bố của SCRUM-19 gọi
`IMeetingInvitationQueue.QueueForPublishedMeetingAsync(publicationSnapshot)` sau khi đã chuyển meeting sang
`Published`, nhưng trước khi commit transaction. Mỗi thành phần tham dự tạo một dòng
`email_messages` ở trạng thái `Queued`; request công bố chỉ ghi queue và không chờ SMTP. Chỉ participant có tài khoản
active và Response khác Delegated theo BR-54 tại transaction Publish mới được nhận thư. `deduplication_key` có unique index và định danh đúng sự kiện nghiệp
vụ, `meeting:{meetingId}:published:{historyId}:{userId}` cho một publication/recipient. Gọi lại cùng sự kiện không tạo thư trùng, nhưng một sự kiện
đổi lịch, hủy họp hoặc phát hành biên bản sau này vẫn có thể tạo thư mới cho cùng người nhận.

Phần FE cần tích hợp được tóm tắt riêng tại
[`07-gui-email-thu-moi-note-cho-fe.md`](07-gui-email-thu-moi-note-cho-fe.md).

## Luồng xử lý nền

`EmailDeliveryWorker` đọc `email_messages` theo `created_at`, rồi `id`, và gửi từng thư một. Worker
claim một dòng bằng `FOR UPDATE SKIP LOCKED`, chuyển sang `Processing` và cấp lease trước khi kết nối
SMTP. Vì vậy nhiều instance backend không cùng lấy một email; lease hết hạn giúp phục hồi job nếu
instance đang gửi bị dừng. Mỗi lần claim được ghi vào `attempt_count`:

1. Gửi thành công: chuyển sang `Sent`, đặt `sent_at`, xoá `last_error`.
2. Gửi thất bại nhưng còn lượt thử: về `Queued`, lưu lỗi và đặt `next_attempt_at` theo exponential
   backoff; worker tiếp tục xử lý thư khác thay vì chặn cả hàng đợi.
3. Dùng hết lượt thử: chuyển sang `Failed` và giữ lỗi cuối để truy vết.

Một lần gửi có deadline tổng thể ngắn hơn lease. Vì vậy thao tác SMTP chậm bị hủy và đưa về lịch retry
trước khi worker khác được phép claim dòng đó. Dòng `Queued` hoặc lease đã hết hạn mà đạt
`MaxAttempts` được chuyển sang `Failed`, không được claim vô hạn. Sau khi SMTP trả thành công, bước ghi
`Sent` không dùng cancellation token dừng host để shutdown thông thường không bỏ mất cập nhật này.

Lỗi SMTP không thay đổi trạng thái `Published` của meeting. Queue nằm trong PostgreSQL nên các job
`Queued` còn lại tiếp tục được xử lý sau khi ứng dụng khởi động lại. Cách gửi có ngữ nghĩa
at-least-once: nếu tiến trình dừng ngay sau khi SMTP đã nhận thư nhưng trước khi trạng thái `Sent`
được lưu, lần chạy sau có thể gửi lại thư đó.

SMTP được kết nối bằng MailKit và không phụ thuộc Gmail hay một nhà cung cấp cụ thể. Chọn
`StartTls` cho cổng submission thông dụng (thường là 587), `SslOnConnect` cho TLS ngay khi kết nối
(thường là 465), hoặc `None` chỉ cho SMTP nội bộ không mã hoá.

Mỗi thư gồm nội dung tiếng Việt, đường dẫn trực tiếp tới
`/mgmt/meetings/{id}` và tệp `thu-moi-hop.ics` có `METHOD:REQUEST`, người tham dự, thời gian, địa điểm
và URL cuộc họp. Nội dung thư hiển thị giờ Việt Nam (`Asia/Ho_Chi_Minh`); tệp lịch giữ thời gian UTC
theo RFC 5545 để Outlook và Google Calendar tự đổi sang múi giờ của người dùng. Các parameter tên
người trong tệp lịch được quote và encode theo RFC 5545/RFC 6868.

## Điểm tích hợp với SCRUM-19

Service công bố phải dùng cùng scoped `TrustCabinetDbContext` và gọi queue trong transaction đang
chuyển trạng thái. Thứ tự bắt buộc:

1. Khóa và kiểm tra meeting đang ở `Approved`.
2. Chuyển sang `Published`, thêm `MeetingStatusHistory` bất biến.
3. Chốt recipient và nội dung vào snapshot; tạo notifications và SaveChanges trong transaction còn mở.
4. Gọi `QueueForPublishedMeetingAsync` với snapshot và Publish history ID; producer render Body/.ics, gán DeduplicationKey và persist Queued jobs.
5. Commit transaction. Worker chỉ gửi Body/CalendarAttachment đã lưu; không render lại từ meeting/participant hiện tại.

Nếu transaction rollback thì các dòng email cũng rollback. Không gọi queue trước khi trạng thái
`Published` được lưu, và không gọi SMTP trực tiếp từ request.

## Cấu hình

Các giá trị không bí mật nằm dưới section `Email` trong `appsettings.json`:

```json
{
  "Email": {
    "ApplicationBaseUrl": "https://hop.example.vn",
    "MaxAttempts": 8,
    "RetryDelaySeconds": 60,
    "PollingIntervalSeconds": 5,
    "ProcessingLeaseSeconds": 120,
    "SendAttemptTimeoutSeconds": 60,
    "Smtp": {
      "Enabled": true,
      "Host": "smtp.example.vn",
      "Port": 587,
      "Security": "StartTls",
      "Username": "",
      "Password": "",
      "FromAddress": "no-reply@example.vn",
      "FromName": "TrustCabinet",
      "TimeoutSeconds": 30
    }
  }
}
```

Không commit username/password SMTP. Đặt chúng bằng user-secrets khi phát triển:

```powershell
dotnet user-secrets set "Email:Smtp:Enabled" "true" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Host" "smtp.example.vn" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Port" "587" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Security" "StartTls" --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Username" "..." --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:Password" "..." --project backend/TrustCabinet.Web
dotnet user-secrets set "Email:Smtp:FromAddress" "no-reply@example.vn" --project backend/TrustCabinet.Web
```

Production dùng các biến môi trường tương ứng, ví dụ
`Email__Smtp__Password` và `Email__ApplicationBaseUrl`. Khi `Enabled` là `false`, worker không được
khởi động; Publish vẫn tạo Queued jobs. URL frontend và sender phải hợp lệ để render invitation
ngay cả khi worker tắt. Cấu hình producer thiếu/sai khiến Publish rollback và trả 503 MSG47.

Giới hạn cấu hình: `MaxAttempts` từ 1–20; retry/polling từ 1–3.600 giây; lease từ 10–3.600 giây;
timeout SMTP và deadline toàn bộ lần gửi từ 1–300 giây. `ProcessingLeaseSeconds` phải dài hơn
`SendAttemptTimeoutSeconds`. Khi bật SMTP,
backend kiểm tra cấu hình ngay lúc khởi động và dừng với lỗi rõ ràng nếu thiếu host, sender, URL
frontend, security mode không hợp lệ hoặc cặp username/password không đầy đủ.
