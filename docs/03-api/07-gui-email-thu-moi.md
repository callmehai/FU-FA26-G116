# Gửi email thư mời cuộc họp — SCRUM-15

SCRUM-15 không thêm HTTP endpoint riêng. Luồng công bố của SCRUM-19 gọi
`IMeetingInvitationQueue.QueueForPublishedMeetingAsync(meetingId)` sau khi transaction đã commit
meeting sang `Published`. Khi bật SMTP, mỗi thành phần tham dự tạo một dòng
`email_messages` ở trạng thái `Queued`; request công bố chỉ ghi queue và không chờ SMTP. Chỉ tài khoản
đang hoạt động mới được nhận thư; producer không lọc `Response` của participant. `deduplication_key`
có unique index; thư mời công bố dùng `meeting:{meetingId}:published:{userId}`.
Gọi lại cùng sự kiện không tạo thư trùng, nhưng một sự kiện
đổi lịch, hủy họp hoặc phát hành biên bản sau này vẫn có thể tạo thư mới cho cùng người nhận.

Phần FE cần tích hợp được tóm tắt riêng tại
[`07-gui-email-thu-moi-note-cho-fe.md`](07-gui-email-thu-moi-note-cho-fe.md).

## Luồng xử lý nền

`EmailDeliveryWorker` thử đối soát một meeting Published chưa có invitation job trong scope riêng.
Lỗi đối soát được log nhưng không chặn hàng đợi đã lưu: worker luôn mở một scope/DbContext mới để
đọc `email_messages` theo `created_at`, rồi `id`, và gửi từng thư một. Worker
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

Mỗi thư Publish gồm nội dung tiếng Việt, thông tin Meeting, địa điểm/hình thức và đường dẫn trực tiếp
tới `/mgmt/meetings/{id}`. Thư không ghi Planned Start/End Time và không tạo tệp `.ics`, vì lịch dự
kiến thuộc từng Meeting Session chứ không thuộc Meeting.

## Điểm tích hợp với SCRUM-19

Service công bố dùng cùng scoped `TrustCabinetDbContext`, nhưng transaction publication và bước
khởi tạo queue có ranh giới riêng. Thứ tự bắt buộc:

1. Khóa và kiểm tra meeting đang ở `Approved`.
2. Chuyển sang `Published`, thêm `MeetingStatusHistory` bất biến.
3. Tạo notifications, SaveChanges và commit transaction publication.
4. Gọi `QueueForPublishedMeetingAsync(meetingId)`; producer đọc meeting/participants, render và lưu
   Subject/Body cùng DeduplicationKey khi SMTP được bật; `CalendarAttachment` là null.
5. Worker chỉ gửi nội dung đã lưu; không render lại từ meeting/participant hiện tại.

Nếu lưu publication lỗi thì rollback status/history/notification và trả `MSG47`. Nếu bước queue lỗi
sau commit thì giữ Published, trả `MSG45`, ghi log và để worker đối soát thử lại. Deduplication key
giữ retry idempotent và không tạo thêm publication history. Không gọi queue trước khi trạng thái
`Published` được commit, và không gọi SMTP trực tiếp từ request.

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
khởi động và lần công bố meeting không tạo dòng email mới; các dòng đã có trong queue vẫn được giữ
trong database. Khi bật SMTP, lỗi producer hoặc lưu queue không rollback publication; worker thử
khởi tạo lại từ meeting Published và request Publish vẫn trả `200 MSG45`.

Giới hạn cấu hình: `MaxAttempts` từ 1–20; retry/polling từ 1–3.600 giây; lease từ 10–3.600 giây;
timeout SMTP và deadline toàn bộ lần gửi từ 1–300 giây. `ProcessingLeaseSeconds` phải dài hơn
`SendAttemptTimeoutSeconds`. Khi bật SMTP,
backend kiểm tra cấu hình ngay lúc khởi động và dừng với lỗi rõ ràng nếu thiếu host, sender, URL
frontend, security mode không hợp lệ hoặc cặp username/password không đầy đủ.
