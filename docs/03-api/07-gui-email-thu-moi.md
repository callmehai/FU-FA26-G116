# Gửi email thư mời cuộc họp — SCRUM-15

**Cập nhật SCRUM-20:** Publish và roster mutations ghi `MeetingInvitationDispatch` cùng domain transaction, kể cả SMTP disabled. Worker materialize sau commit khi enabled, rồi EmailQueueProcessor hiện có gửi SMTP. Current active internal và Guest invitation hợp lệ là recipients; legacy Guest/Delegated không còn là current assignment. Gỡ mềm/re-add dùng key theo assignment mới; migration giữ key cũ để không gửi lại. Legacy reconciliation kiểm từng recipient, không bỏ qua cả meeting chỉ vì có một EmailMessage. [Contract mới](09-thanh-phan-tham-du.md#5-transaction-và-delivery) và [UC-04 dependency](09-thanh-phan-tham-du-guest-uc04.md) thay mô tả producer trực tiếp trong các đoạn lịch sử bên dưới.

> **Một cuộc họp là một buổi họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).** Từ migration
> `MeetingOwnsScheduleDropSessions`, lịch nằm trên Meeting (`plannedStartTime`, `plannedEndTime`, `onlineLink`) và code
> không còn `MeetingSession`. Các đoạn lịch sử bên dưới nhắc Meeting Session là mô tả thời điểm viết.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng ([danh mục BR](../01-requirements/06-business-rules.md)).

SCRUM-15 không thêm endpoint nào cho người dùng; endpoint duy nhất là webhook Resend gọi để báo kết quả giao thư (xem [Gửi qua Resend](#gửi-qua-resend-và-kết-quả-giao-thư--adr-0031)). Luồng công bố của SCRUM-19 gọi
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
tới `/mgmt/meetings/{id}`. Thư ghi giờ bắt đầu/kết thúc theo giờ Việt Nam và đính kèm `thu-moi-hop.ics`
(`DTSTART`/`DTEND` từ `plannedStartTime`/`plannedEndTime`). Họp trực tuyến ghi link, hoặc "Phòng họp trực
tuyến TrustCabinet" khi không có link. Dời lịch chưa gửi email cập nhật; người được mời nhận thông báo
trong ứng dụng.

## Điểm tích hợp với SCRUM-19

Service công bố dùng cùng scoped `TrustCabinetDbContext`, nhưng transaction publication và bước
khởi tạo queue có ranh giới riêng. Thứ tự bắt buộc:

1. Khóa và kiểm tra meeting đang ở `Approved`.
2. Chuyển sang `Published`, thêm `MeetingStatusHistory` bất biến.
3. Tạo notifications, SaveChanges và commit transaction publication.
4. Gọi `QueueForPublishedMeetingAsync(meetingId)`; producer đọc meeting/participants, render và lưu
   Subject/Body/`.ics` cùng DeduplicationKey khi SMTP được bật.
5. Worker chỉ gửi nội dung đã lưu; không render lại từ meeting/participant hiện tại.

Nếu lưu publication lỗi thì rollback status/history/notification và trả `MSG47`. Nếu bước queue lỗi
sau commit thì giữ Published, trả `MSG45`, ghi log và để worker đối soát thử lại. Deduplication key
giữ retry idempotent và không tạo thêm publication history. Không gọi queue trước khi trạng thái
`Published` được commit, và không gọi SMTP trực tiếp từ request.

## Gửi qua Resend và kết quả giao thư — ADR-0031

Server gửi qua API của Resend ([ADR-0031](../05-decisions/0031-gui-email-qua-resend.md)); `Email:Provider` chọn
`Resend` hoặc `Smtp`. Hàng đợi ở trên giữ nguyên, chỉ đổi cách gửi một thư:

| Bước | Resend | SMTP |
|---|---|---|
| Gửi | `POST https://api.resend.com/emails`, kèm `Idempotency-Key: email-message/{id}` và tag `email_message_id` | MailKit, `Message-ID` dựng từ `id` |
| Thử lại không gửi trùng | Có: Resend trả kết quả lần trước trong 24 giờ | Không |
| Lỗi không thử lại | `400`, `422` → `Failed` ngay | Không có, mọi lỗi đều thử lại |
| `provider_message_id` | id Resend trả về | trống |
| Kết quả giao thư | Webhook bên dưới | Không biết |

### `POST /api/email/resend/webhook`

Chỉ Resend gọi. Không đăng nhập; Resend chứng minh bằng chữ ký Svix trên đúng các byte của body (header `svix-id`,
`svix-timestamp`, `svix-signature`; HMAC-SHA256 bằng phần sau `whsec_` của `Email:Resend:WebhookSecret`; lệch giờ
tối đa 5 phút). Body tối đa 256 KB. Câu trả lời chỉ có mã trạng thái, không có mã `MSGnn`, vì không có màn hình nào đọc.

| Mã | Khi nào |
|---|---|
| `200` | Đã ghi, hoặc là sự kiện app không theo dõi (`email.sent`, `opened`, `clicked`…), hoặc thư không có dòng trong `email_messages` (email đặt lại mật khẩu) |
| `400` | Chữ ký đúng nhưng body không đọc được; gửi lại cũng vậy |
| `401` | Thiếu hoặc sai chữ ký, hoặc quá 5 phút |
| `413` | Body quá 256 KB |
| `500` | Lỗi khi ghi; Resend sẽ gửi lại |
| `503` | Chưa đặt `Email:Resend:WebhookSecret` |

Sự kiện được ghi vào `email_messages`, tìm theo `provider_message_id` hoặc tag `email_message_id`:

| Sự kiện Resend | `delivery_state` | `delivery_detail` |
|---|---|---|
| `email.delivered` | `Delivered` | |
| `email.delivery_delayed` | `Delayed` | |
| `email.bounced` | `Bounced` | `bounce.type`, `subType`, `message` |
| `email.complained` | `Complained` | |
| `email.suppressed` | `Suppressed` | |
| `email.failed` | `Failed` | `failed.reason` |

`delivery_state_at` là `created_at` của sự kiện; một sự kiện cũ hơn cái đã lưu thì bỏ qua. `status` không đổi theo
webhook: `Sent` nghĩa là Resend đã nhận thư, `delivery_state` là chuyện xảy ra sau đó. Xem thư chưa tới:

```sql
select to_address, subject, delivery_state, delivery_detail, delivery_state_at
from email_messages where delivery_state in ('Bounced', 'Complained', 'Suppressed', 'Failed')
order by delivery_state_at desc;
```

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
    "Provider": "Resend",
    "Resend": {
      "Enabled": true,
      "ApiUrl": "https://api.resend.com/",
      "ApiKey": "",
      "FromAddress": "no-reply@example.vn",
      "FromName": "TrustCabinet",
      "WebhookSecret": ""
    },
    "Smtp": {
      "Enabled": false,
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

Chỉ transport được chọn ở `Provider` cần `Enabled: true`. Không commit `Resend:ApiKey`, `Resend:WebhookSecret`
hay username/password SMTP. Server đặt chúng bằng biến môi trường (`Email__Resend__ApiKey`…, xem
[deploy/README.md §8](../../deploy/README.md#8-gửi-email-thật-tuỳ-chọn)); máy dev dùng user-secrets:

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
`SendAttemptTimeoutSeconds`. Khi bật transport đã chọn,
backend kiểm tra cấu hình ngay lúc khởi động và dừng với lỗi rõ ràng nếu thiếu URL frontend; với Resend: `ApiUrl`
https, `ApiKey`, `FromAddress`; với SMTP: host, sender, security mode hợp lệ và cặp username/password đầy đủ.
