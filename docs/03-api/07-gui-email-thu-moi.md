# Gửi email thư mời cuộc họp — SCRUM-15

**Cập nhật SCRUM-20:** Publish và roster mutations ghi `MeetingInvitationDispatch` cùng domain transaction, kể cả SMTP disabled. Worker materialize sau commit khi enabled, rồi EmailQueueProcessor hiện có gửi SMTP. Current active internal và Guest invitation hợp lệ là recipients; legacy Guest/Delegated không còn là current assignment. Gỡ mềm/re-add dùng key theo assignment mới; migration giữ key cũ để không gửi lại. Legacy reconciliation kiểm từng recipient, không bỏ qua cả meeting chỉ vì có một EmailMessage. [Contract mới](09-thanh-phan-tham-du.md#5-transaction-và-delivery) và [UC-04 dependency](09-thanh-phan-tham-du-guest-uc04.md) thay mô tả producer trực tiếp trong các đoạn lịch sử bên dưới.

> **Một cuộc họp là một buổi họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).** Từ migration
> `MeetingOwnsScheduleDropSessions`, lịch nằm trên Meeting (`plannedStartTime`, `plannedEndTime`, `onlineLink`) và code
> không còn `MeetingSession`. Các đoạn lịch sử bên dưới nhắc Meeting Session là mô tả thời điểm viết.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> mã BR và UC trong file là mã mới theo [bảng 09](../01-requirements/09-bang-anh-xa-br-dot-1.md) và [bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md) (ADR-0079).

SCRUM-15 không thêm endpoint nào cho người dùng; endpoint duy nhất là webhook Resend gọi để báo kết quả giao thư (xem [Gửi qua Resend](#gửi-qua-resend-và-kết-quả-giao-thư--adr-0031)). Luồng công bố của SCRUM-19 (và các mutation roster: dời lịch, huỷ, thu hồi, đổi vai trò) gọi
`MeetingInvitationEvents.Stage(...)` ghi một dòng `meeting_invitation_dispatches` ở trạng thái
`Pending` cùng domain transaction với thay đổi meeting/roster — kể cả khi transport tắt. Sau commit,
`MeetingInvitationDispatchProcessor` của `EmailDeliveryWorker` materialize từng dispatch thành một dòng
`email_messages` ở trạng thái `Queued` (render sẵn Subject/Body/html/`.ics` cùng `DeduplicationKey`), rồi
`EmailQueueProcessor` gửi từng thư; request công bố chỉ ghi dispatch và không chờ SMTP. Chỉ assignment
hiện tại còn hiệu lực mới thành thư: internal theo `ParticipantRules.ActiveExpression` (tài khoản đang
hoạt động), Khách theo lời mời chưa bị thu hồi; legacy Guest/Delegated không còn là current assignment
nên không nhận. `deduplication_key` có unique index; thư mời công bố dùng `meeting:{meetingId}:published:{userId}`.
Gọi lại cùng sự kiện không tạo thư trùng (dispatch đã `Materialized` thì bỏ qua), nhưng một sự kiện
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

**Thời hạn giữ nội dung** ([ADR-0067](../05-decisions/0067-don-du-lieu-dinh-ky.md)): thư `Sent` hoặc `Failed` tạo quá `Retention:EmailBodyDays` ngày
(mặc định 30) bị job `DataRetentionWorker` xoá trắng `body`, `html_body` và `calendar_attachment` (tên, chức vụ, giờ, địa điểm
họp của người nhận). Dòng giữ lại người nhận, tiêu đề, `sent_at`, kết quả giao và lỗi cuối để vẫn trả lời được "thư có đến
tay không". Thư `Queued`/`Processing` không bị đụng.

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
tuyến TrustCabinet" khi không có link. Dời lịch gửi thư "Cuộc họp đã dời lịch" (bảng dưới) kèm chuông trong ứng
dụng. Thư huỷ đính kèm `huy-hop.ics` (`METHOD:CANCEL`) để trình lịch gỡ sự kiện.

## Điểm tích hợp với SCRUM-19

Service công bố dùng cùng scoped `TrustCabinetDbContext`, nhưng transaction publication và bước
khởi tạo queue có ranh giới riêng. Thứ tự bắt buộc:

1. Khóa và kiểm tra meeting đang ở `Approved`.
2. Chuyển sang `Published`, thêm `MeetingStatusHistory` bất biến.
3. Tạo notifications và gọi `MeetingInvitationEvents.Stage(...)` cho từng recipient, SaveChanges và
   commit transaction publication — dispatch nằm cùng transaction với status/history/notification.
4. Sau commit, khi transport được bật: `MeetingInvitationDispatchProcessor` materialize từng dispatch
   thành `email_messages` (Subject/Body/html/`.ics` cùng DeduplicationKey); `EmailQueueProcessor`
   chỉ gửi nội dung đã lưu, không render lại từ meeting/participant hiện tại.
5. Thư nhắc họp do `MeetingReminderEmails.QueueDueAsync` xếp cùng nhịp chuông nhắc trong ứng dụng
   (trong 15 phút trước giờ bắt đầu), không qua dispatch công bố.

Nếu lưu publication lỗi thì rollback status/history/notification/dispatch và trả `MSG47`. Lỗi
materialize/gửi sau commit thì giữ Published, trả `MSG45`, ghi log; dispatch ở lại `Pending` hoặc
`BlockedDependency` để worker thử lại. Deduplication key
giữ retry idempotent và không tạo thêm publication history. Không materialize/gửi trước khi trạng thái
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

Mỗi thư gửi cả hai phần: `text` (lưu trong `email_messages.body`) và `html`. Phần `html` là mẫu thư thiết kế
(`EmailTemplates` trên khung `EmailLayout`, theo bản demo email 15/11/2026): tên TrustCabinet ở đầu, loại thư và tên
cuộc họp, bảng thông tin, nút hành động kèm đường dẫn ghi nguyên để chép được, chân thư. Chỉ dùng bảng và CSS inline vì
Gmail, Outlook bỏ thẻ `<style>`, flexbox và SVG; mọi chữ người dùng gõ (tên họp, mô tả, lý do huỷ, tên người) đều được
escape. Thư cho Khách không có địa điểm và mô tả, nút dẫn tới trang xác minh (UC-04); thư huỷ và thư thu hồi không có
link; thư dời lịch gạch thời gian cũ cạnh thời gian mới. Thư về cuộc họp ghi Chủ toạ và người tổ chức (người tạo cuộc
họp) để người nhận biết hỏi ai, đọc lúc dựng thư; người trong hệ thống thấy cả email người tổ chức, Khách chỉ thấy tên.
Thư mã xác minh không có link: trang xác minh tự gửi mã mới mỗi lần mở, link trong thư mã sẽ làm chính mã đó mất hiệu lực.

| Thư | Ai nhận | Nút |
|---|---|---|
| Lời mời tham dự | người trong roster, Khách | Xem cuộc họp / Xác minh email để vào cuộc họp |
| Vai trò đã thay đổi | người trong roster | Xem cuộc họp |
| Cuộc họp đã dời lịch | người trong roster (cả người tổ chức vừa dời), Khách | Xem cuộc họp / Xác minh email để vào cuộc họp |
| Cuộc họp đã bị huỷ | người đã nhận lời mời, Khách | không |
| Lời mời đã được thu hồi | người đã nhận lời mời, Khách | không |
| Phân công trình bày | Khách được gán trình bày | Xác minh email để vào cuộc họp |
| Mã xác minh vào cuộc họp | Khách | không (mã hiện to) |
| Đặt lại mật khẩu | người được quản trị viên đặt lại | Đặt mật khẩu mới |
| Cuộc họp sắp bắt đầu | người được mời đang hoạt động, Khách hiện tại | Vào phòng họp (phòng TrustCabinet) / Xem cuộc họp / Xác minh email để vào cuộc họp (Khách) |
| Biên bản đã ban hành, bản đính chính đã ban hành | đúng những người nhận chuông "Biên bản đã ban hành", trừ Chủ toạ vừa duyệt | Xem biên bản (mở tab Biên bản) |

**Thêm theo PO 15/11/2026** (ngoài UC-76, UC-72, UC-73, UC-74 và quy tắc dời lịch hiện ghi chỉ trong ứng dụng, chờ BA cập nhật):
- Dời lịch: trước chỉ Khách nhận email, người nội bộ chỉ có chuông. Giờ mọi người trong roster nhận email, gạch thời gian
  cũ cạnh thời gian mới; chuông vẫn không gửi cho người tổ chức vừa dời.
- Nhắc họp: `MeetingReminderEmails` chạy cùng nhịp `NotificationWorker` với chuông nhắc, trong 15 phút trước giờ bắt đầu
  của cuộc họp Đã công bố. Khoá chống trùng `email-starting-soon:{meetingId}:{giờ bắt đầu}:{người}` giữ giờ bắt đầu, nên dời
  lịch thì nhắc lại theo giờ mới. Khác chuông, thư nhắc tới cả Khách (Khách không có chuông), kèm link xác minh; không tạo
  được link thì không gửi thư cho Khách đó. Ai được mời trong chính 15 phút đó (thư mời tạo sau giờ bắt đầu trừ 15 phút)
  thì không nhận thư nhắc, vì thư mời vừa tới đã ghi giờ họp; chuông nhắc vẫn có.
- Biên bản ban hành: đưa vào hàng đợi trong cùng giao dịch duyệt biên bản, khoá `email-minutes-issued:{minutesId}:{người}`.
  Thư biên bản họp Mật không nhắc tải Word/PDF vì biên bản Mật chỉ đọc trên màn hình.

Phần `html` dựng lúc đưa thư vào hàng đợi, lưu ở `email_messages.html_body` (migration `EmailHtmlBody`), vì lúc gửi
không còn đủ dữ kiện để dựng lại. Thư đặt lại mật khẩu và mã OTP vẫn gửi thẳng, không lưu, nên link và mã vẫn không nằm
trong DB. Dòng cũ có `html_body` rỗng thì gửi phần `html` dựng từ `text` như trước (`EmailHtml`): mỗi địa chỉ web thành
thẻ `<a>`, vì trình đọc thư không tự nhận link trong văn bản thuần thì Khách phải chép tay link mời dài (E2E Khách
12/11/2026, U6). Dấu câu ngay sau địa chỉ (`.`, `)`…) không tính vào link. Xem trước mọi mẫu: chạy
`EMAIL_PREVIEW_DIR=<thư mục> dotnet test --filter EmailTemplatesTests` rồi mở `index.html` trong thư mục đó.

**Click/open tracking của Resend phải tắt** trên domain gửi: link đặt lại mật khẩu và link mời Khách mang token sau `#`, tracking sẽ ghi lại cả URL đó.

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
trong database. Khi bật SMTP, lỗi materialize/gửi sau commit không rollback publication; dispatch
ở lại hàng đợi để worker thử lại và request Publish vẫn trả `200 MSG45`.

Giới hạn cấu hình: `MaxAttempts` từ 1–20; retry/polling từ 1–3.600 giây; lease từ 10–3.600 giây;
timeout SMTP và deadline toàn bộ lần gửi từ 1–300 giây. `ProcessingLeaseSeconds` phải dài hơn
`SendAttemptTimeoutSeconds`. Khi bật transport đã chọn,
backend kiểm tra cấu hình ngay lúc khởi động và dừng với lỗi rõ ràng nếu thiếu URL frontend; với Resend: `ApiUrl`
https, `ApiKey`, `FromAddress`; với SMTP: host, sender, security mode hợp lệ và cặp username/password đầy đủ.
