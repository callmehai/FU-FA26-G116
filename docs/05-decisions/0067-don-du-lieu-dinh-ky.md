# ADR-0067: Dọn dữ liệu định kỳ

- **Trạng thái:** Đề xuất — PO (Hải) 20/11/2026
- **Ngày:** 2026-11-20
- **Người quyết định:** Hải (PO)
- **Liên quan:** [ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md), [ADR-0027](0027-voice-id-co-dong-y.md),
  [ADR-0061](0061-hop-ket-hop-mot-dong-thoi-gian.md) điểm 5 (bản ghi tổng hợp), [ADR-0062](0062-tu-ket-thuc-cuoc-hop-bi-bo-quen.md)

## Bối cảnh

Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (hiệu lực 01/01/2026, hướng dẫn bởi Nghị định 356/2025/NĐ-CP) yêu cầu dữ liệu cá
nhân chỉ được giữ trong thời gian cần cho mục đích đã nêu, rồi phải xoá (nguyên tắc tối thiểu hoá và giới hạn lưu trữ). Bản
ghi cuộc họp đã có `RetainUntil` và job xoá khi hết hạn (SCRUM-29). Rà soát ngày 20/11/2026 thấy vẫn còn dữ liệu không ai
dọn:

1. **Tệp âm thanh mồ côi.** Tệp dưới `Recordings:StorageRoot` mà không dòng nào trỏ tới: xoá tệp lỗi sau khi dòng đã ghi
   `Deleted`, người dùng ngắt giữa lúc tải lên, server tắt giữa lúc nối, cắt hay trộn tệp. Đó là giọng người họp nằm trên
   đĩa mãi mãi, ngoài mọi thời hạn.
2. **Bản ghi tổng hợp giữ tiếng đã xoá.** Xoá bản ghi mic phòng (theo yêu cầu hay hết hạn) không xoá bản ghi tổng hợp chứa
   chính tiếng đó; xoá một câu quá 8 ngày sau khi họp xong thì bản tổng hợp không được trộn lại, câu đó còn mãi trong đó.
   Thời hạn của bản tổng hợp lại tính từ lúc trộn, nên nó sống lâu hơn nguồn.
3. **Biểu quyết nháp của cuộc họp đã huỷ.** Kết thúc họp thì nháp bị xoá (PO 08/11/2026), huỷ họp thì không.
4. **Thông báo** trong ứng dụng tăng mãi.
5. **Mã OTP của Khách** chỉ được dọn khi lời mời đó gửi mã lần sau.
6. **Nội dung email** (văn bản, HTML, tệp `.ics`) giữ mãi sau khi đã gửi: tên, chức vụ, giờ và địa điểm họp của người nhận.

## Quyết định

Chúng tôi thêm một job nền `DataRetentionWorker`, chạy lúc khởi động rồi mỗi `Retention:IntervalMinutes` (mặc định 60 phút),
và sửa các đường xoá để không để lại phần sót. Mỗi quy tắc chạy riêng: một quy tắc lỗi được ghi log, các quy tắc sau vẫn chạy.

| # | Quy tắc | Mốc (khoá cấu hình, mặc định) |
|---|---|---|
| 1 | Xoá tệp âm thanh dưới `Recordings:StorageRoot` (đuôi của bản ghi hoặc câu nói) không được dòng `recordings` nào chưa `Deleted` và không câu `speech_clips` nào trỏ tới, và không được ghi trong `OrphanAudioGraceHours` giờ qua. Duyệt thư mục theo luồng, đối chiếu database theo lô 500 tệp; bỏ qua mọi symlink (không đi theo), không bao giờ chạm ra ngoài thư mục gốc. Mỗi lần có xoá thì ghi **một** dòng audit `recording.orphan_files_deleted` (số tệp, số byte) | `Retention:OrphanAudioGraceHours` = 24 (tối thiểu 1) |
| 2 | Xoá bản ghi mic phòng (Thư ký xoá hay hết hạn) thì xoá luôn bản ghi tổng hợp của cuộc họp, cùng transaction, audit `recording.erased` lý do `source_erased`. Xoá một câu nói hay cắt một lượt ghi: trong 8 ngày sau khi họp xong thì bản tổng hợp được trộn lại không có phần đó; quá 8 ngày thì xoá bản tổng hợp (lý do `source_removed_after_window`). Bản tổng hợp chỉ còn một phía (mọi câu đã xoá hay đã loại, hoặc không còn bản ghi phòng) cũng bị xoá. `RetainUntil` của bản tổng hợp = ngày sớm nhất trong các nguồn: `RetainUntil` của bản ghi phòng (BR-347, tính từ lượt đầu), hoặc câu sớm nhất + `Recordings:RetentionDays` | `Recordings:RetentionDays` = 180; cửa sổ trộn lại 8 ngày (ADR-0061) |
| 3 | Huỷ cuộc họp (người tổ chức huỷ, hay hệ thống tự huỷ theo BR-301) xoá mọi biểu quyết `Draft` của nó trong cùng transaction, mỗi nháp một dòng audit `vote.draft_deleted_on_meeting_cancel` (`cancelledById`, nội dung nháp), như khi kết thúc họp. Job quét thêm nháp còn sót trong cuộc họp đã huỷ trước đây. Vote Open/Closed giữ nguyên | — |
| 4 | Xoá thông báo đã đọc và tạo quá `ReadNotificationDays` ngày; xoá mọi thông báo tạo quá `NotificationDays` ngày | `Retention:ReadNotificationDays` = 90, `Retention:NotificationDays` = 180 |
| 5 | Xoá mã xác minh của Khách đã hết hạn quá `GuestChallengeDays` ngày (khi đó mã cũng đã ra khỏi cửa sổ đếm 20 mã/24 giờ). Phiên Khách giữ nguyên làm bằng chứng | `Retention:GuestChallengeDays` = 1 |
| 6 | Email đã `Sent` hoặc `Failed` (hết lượt thử) và tạo quá `EmailBodyDays` ngày: xoá trắng `body`, `html_body`, `calendar_attachment`. Dòng giữ lại: gửi cho ai, tiêu đề, lúc nào, kết quả giao | `Retention:EmailBodyDays` = 30 |

Ngoài job, việc tải bản ghi lên bị ngắt sau khi tệp đã ghi xuống đĩa nay xoá tệp ngay; job chỉ còn là lưới an toàn.

**Không bao giờ xoá theo thời gian:**

- `meeting_status_history` và `audit_log` — bằng chứng chỉ ghi thêm (BR-440, BR-445); `DbContext` chặn sửa và xoá. Chính các
  quy tắc trên cũng ghi audit thay vì xoá audit.
- Biên bản đã ban hành — văn bản chính thức của cuộc họp.
- Dòng `recordings` đã xoá — giữ làm bằng chứng "đã có bản ghi và đã xoá ngày …"; chỉ tệp đi.
- Phiên Khách (`guest_sessions`) — bằng chứng Khách đã xác minh và vào họp.
- Mẫu giọng Voice ID — theo **sự đồng ý** chứ không theo đồng hồ (ADR-0027): xoá khi người đó rút đồng ý hoặc xoá mẫu, đúng
  lúc họ làm. Đặt thêm hạn theo thời gian sẽ xoá mẫu của người vẫn đồng ý.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Migration xoá một lần dữ liệu cũ | Sạch ngay khi deploy | Không chặn được dữ liệu sót về sau; dữ liệu test vốn bỏ đi được | Job định kỳ dọn cả cái cũ lẫn cái mới |
| Thêm vào `RecordingRetentionWorker` (15 phút) | Không thêm worker | Trộn việc bản ghi với thông báo, email; quét thư mục mỗi 15 phút là thừa | Một job riêng, mốc tính bằng giờ/ngày |
| Xoá hẳn dòng email cũ | Bảng nhỏ hơn | Mất câu trả lời "thư mời có đến tay không" (SCRUM-15) | Chỉ xoá phần nội dung |
| Luôn trộn lại bản tổng hợp khi nguồn đổi | Giữ được bản nghe lại | Ngoài 8 ngày không còn job trộn; nguồn phòng đã xoá thì không còn gì để trộn | Trộn lại trong cửa sổ, ngoài cửa sổ thì xoá |

## Hệ quả

- Dữ liệu cá nhân không còn nằm ngoài thời hạn: mọi tệp âm thanh hoặc có dòng trỏ tới (và theo `RetainUntil` của dòng đó)
  hoặc biến mất sau tối đa 24 giờ + 1 lần chạy.
- Bản tổng hợp không còn sống lâu hơn nguồn của nó. Bị xoá thì không dựng lại (giữ đúng quy tắc "đã xoá thì thôi"), kể cả khi
  câu đã loại được đưa trở lại sau đó.
- Thông báo cũ hơn 90/180 ngày biến khỏi chuông; nội dung email cũ hơn 30 ngày không xem lại được, dòng gửi vẫn còn.
- **Chưa làm, chờ PO quyết:** xoá tài liệu của cuộc họp đã huỷ và bản sao trong kho cá nhân; chuyển WAV sang Opus; xoá dự
  thảo AI khi bản ghi hết hạn; kho tài liệu của tài khoản đã vô hiệu hoá.
- BA bổ sung các quy tắc trên vào BR (số BR mới) khi chốt tài liệu.
