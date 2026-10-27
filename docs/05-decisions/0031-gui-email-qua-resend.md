# ADR-0031: Gửi email qua Resend, giữ hàng đợi trong app, nhận kết quả giao thư bằng webhook

- **Trạng thái:** Đề xuất — SCRUM-15, PO 27/10/2026
- **Ngày:** 2026-10-27
- **Người quyết định:** PO (Hải)

## Bối cảnh

SCRUM-15 gửi thư mời họp và email đặt lại mật khẩu qua SMTP. Server test dùng Gmail cá nhân của một thành viên
(App Password). Lần bật đầu tiên (27/10/2026) cho thấy các vấn đề:

- **Thư dội về không ai biết.** Gmail nhận mọi thư rồi mới dội về hộp thư gửi, kể cả thư tới tài khoản demo
  `@trustcabinet.local`. App ghi `Sent` cho thư không bao giờ tới.
- **Không xem được thư nào đã tới tay người nhận.** `email_messages` chỉ biết "đã giao cho SMTP", không biết
  "đã vào hộp thư", "bị dội" hay "bị đánh dấu spam".
- **Gửi bằng hộp thư cá nhân.** Gmail giới hạn khoảng 500 thư/ngày, thư dội nhiều thì bị khoá quyền gửi, người nhận
  thấy thư từ một địa chỉ `@gmail.com` chứ không phải tên miền của hệ thống, và App Password đã bị lộ trong kênh chat
  của nhóm.

Một hệ thống dành cho doanh nghiệp cần gửi từ tên miền của mình (SPF, DKIM), và cần biết thư mời có tới hay không.

## Quyết định

1. **Gửi qua API HTTP của [Resend](https://resend.com)**, từ một địa chỉ trên tên miền đã xác minh (bản ghi DKIM và
   SPF trên DNS). `Email:Provider` chọn `Resend` hoặc `Smtp`. Máy dev vẫn có thể dùng SMTP; server test dùng Resend.
2. **Giữ nguyên hàng đợi trong app** (`email_messages`: claim bằng lease, thử lại 8 lần với độ trễ tăng dần, hết lượt
   thì `Failed` và giữ để tra cứu). Dịch vụ ngoài không thay được nó: lúc gọi Resend vẫn có thể timeout, lỗi 5xx hay
   hết hạn mức, và thư mời phải được ghi cùng transaction công bố cuộc họp rồi gửi sau.
3. **Không gửi trùng khi thử lại.** Mỗi lần gửi kèm `Idempotency-Key: email-message/{id}`. Trong vòng 24 giờ, Resend
   trả lại kết quả lần trước thay vì gửi lần nữa, nên một lần timeout sau khi Resend đã nhận không thành hai thư.
4. **Phân loại lỗi.** `400`/`422` (thư sai: địa chỉ, trường dữ liệu) → `Failed` ngay, vì gửi lại vẫn bị từ chối y như
   vậy. `401`/`403` (sai key, tên miền chưa xác minh), `409`, `429` (quá tốc độ, hết hạn mức ngày) và `5xx` → thử
   lại, vì sửa cấu hình xong thì chính thư đó gửi được.
5. **Kết quả giao thư về qua webhook** `POST /api/email/resend/webhook`, ký bằng Svix (HMAC-SHA256 trên
   `{svix-id}.{svix-timestamp}.{body}`, lệch giờ tối đa 5 phút). Mỗi thư lưu `provider_message_id`, và
   `delivery_state` (`Delivered`, `Delayed`, `Bounced`, `Complained`, `Suppressed`, `Failed`) cùng thời điểm và lý do.
   Báo cáo cũ hơn báo cáo đã lưu thì bỏ qua, vì Resend không gửi lại theo thứ tự. `status` vẫn chỉ nói về việc gửi
   (`Sent` = Resend đã nhận); `delivery_state` là chuyện xảy ra sau đó.
6. **Địa chỉ đã dội hay báo spam thì Resend tự chặn** (suppression list) và báo `email.suppressed`; app không giữ
   danh sách chặn riêng.
7. **Vẫn bỏ qua tên miền không nhận thư** (`Email:UndeliverableDomains`, mặc định `trustcabinet.local` ở
   Development): gửi tới đó chỉ làm tăng tỉ lệ dội, mà tỉ lệ này cao thì Resend khoá tài khoản.

## Hệ quả

- Server test cần: tài khoản Resend, xác minh tên miền gửi trên Namecheap, một API key chỉ có quyền gửi trên đúng tên
  miền đó, và một webhook trỏ về `https://<SITE_ADDRESS>/api/email/resend/webhook`. Các bước ở
  [deploy/README.md §8](../../deploy/README.md#8-gửi-email-thật-tuỳ-chọn).
- Gói miễn phí: 3.000 thư/tháng, **100 thư/ngày**, 1 tên miền. Đủ cho demo; một lượt công bố nhiều cuộc họp lớn có thể
  chạm 100/ngày. Khi đó Resend trả `429`, thư nằm lại trong hàng đợi và được thử lại, không mất. Doanh nghiệp thật
  dùng gói trả phí (Pro: 50.000 thư/tháng).
- Thư mời đi qua một dịch vụ ở nước ngoài. Nội dung thư mời gồm tên cuộc họp, thời gian và địa điểm; cuộc họp `Mật`
  không nên ghi nội dung nhạy cảm trong thư. Triển khai cho cơ quan nhà nước thì thay Resend bằng SMTP nội bộ
  (`Email:Provider=Smtp`), không phải sửa code.
- Chưa có màn hình trong app để xem trạng thái từng thư. Hiện xem trên bảng điều khiển của Resend hoặc bằng SQL
  (`delivery_state` trong `email_messages`). Một màn quản trị là việc riêng, nếu cần.

## Phương án đã cân nhắc

| Phương án | Vì sao không chọn |
|---|---|
| Giữ Gmail SMTP | Các vấn đề ở phần Bối cảnh |
| Resend qua SMTP (`smtp.resend.com`) | Chỉ cần đổi cấu hình, nhưng SMTP không trả id thư, nên không ghép được webhook với thư, và không có khoá chống gửi trùng |
| Bỏ hàng đợi, gọi thẳng Resend | Timeout hay `429` lúc công bố cuộc họp sẽ làm mất thư mời, hoặc làm request công bố chờ dịch vụ ngoài |
| Amazon SES | Rẻ hơn khi gửi nhiều, nhưng phải xin ra khỏi sandbox và tự dựng SNS để nhận báo dội; nặng hơn cho một đồ án |
