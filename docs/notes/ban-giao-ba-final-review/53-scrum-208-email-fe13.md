# Cập nhật đặc tả cho email nhắc họp và email biên bản đã ban hành — SCRUM-208 (FE-13)

**Người ghi:** BA (nhánh 208), 16/11/2026. **Nguồn:** ticket SCRUM-208 (PO chốt 15/11/2026); code đã vào `main` qua PR #368 (mẫu HTML mọi thư), PR #371 (email nhắc họp + email biên bản ban hành), PR #377 (comment bổ sung của Hải 15/11: email dời lịch cho cả người nội bộ, `huy-hop.ics`, bỏ qua người vừa được mời trong 15 phút); đối chiếu lại code trên `main` ngày 16/11/2026. Ticket SCRUM-208 giữ `To Do`, làm sau note 52.

**Phạm vi:** chỉ sửa docs, không đụng code. Các điểm nằm trong Report 3 extract (`05-use-case-specifications.md`, `06-business-rules.md`) thì sửa trong DOCX ở `docs/08-reports/source/` rồi chạy `python scripts/sync_reports.py --report 3`, không sửa tay file extract. KHÔNG cần mã MSG mới.

**Đã khớp, không cần làm gì:** tài liệu API `docs/03-api/07-gui-email-thu-moi.md` đã cập nhật trong PR (bảng 10 loại thư + 3 gạch đầu dòng "Thêm theo PO 15/11/2026"): đối chiếu lại code (`MeetingReminderEmails`, `MeetingInvitationEvents`, `EmailTemplates`, `MinutesService.QueueIssuedEmailsAsync`, `CalendarInvitationBuilder.FileNameOf`) thấy còn khớp, không thiếu gì nên không sửa file này.

## 1. Email nhắc họp — BR-303 (khớp `MeetingReminderEmails`)

- Trước đây BR-303 chỉ ghi chuông trong ứng dụng ("Guests do not receive in-app reminders"), thiếu kênh email.
- Nay BR-303 ghi thêm: cùng lúc với chuông nhắc, hệ thống gửi một email nhắc tới mọi người được mời đang hoạt động (nội bộ) và mọi Khách còn lời mời; dời lịch thì nhắc lại theo giờ mới; Khách nhận email vì Khách không có chuông.
- Ngoại lệ theo code: người vừa được mời trong chính 15 phút trước giờ họp thì BỎ QUA email nhắc (thư mời vừa tới đã ghi giờ họp); chuông nhắc vẫn có.
- Nút thư: "Vào phòng họp" nếu họp trong TrustCabinet, "Xem cuộc họp" nếu họp ở nơi khác/trực tiếp; Khách nhận link xác minh email (không tạo được link thì không gửi thư cho Khách đó).

## 2. Email biên bản đã ban hành — BR-411 + UC-64 bước 6 (khớp `MinutesService`)

- Trước đây UC-64 bước 6 ghi "No email is sent; the notification is in-app only (out of scope, PO 11/11/2026)" — nay PO chốt gửi email nên sửa lại.
- Nay BR-411 ghi thêm: khi ban hành (kể cả bản đính chính), ngoài chuông còn gửi email tới đúng người nhận chuông "Biên bản đã ban hành", trừ Chủ tọa vừa duyệt.
- UC-64 bước 6 (Approve and Issue Meeting Minutes): mở tab Biên bản ("Xem biên bản"); bản đính chính ghi "Đính chính lần N"; họp Mật không nhắc tải Word/PDF (biên bản Mật chỉ đọc trên màn hình, theo BR-412).

## 3. Email dời lịch — BR-76, BR-322, UC-15 AF-1 bước 5 + POST-3 (khớp `MeetingRepository.StageRescheduleInvitationsAsync`)

- Trước đây chỉ Khách nhận email dời lịch, người nội bộ chỉ nhận chuông. Comment Hải 15/11 (PR #377): dời lịch gửi email cho CẢ người nội bộ — mọi người trong thành phần kể cả người vừa dời + Khách hiện tại đều nhận thư "Cuộc họp đã dời lịch", gạch thời gian cũ cạnh thời gian mới.
- Chuông giữ nguyên: không gửi cho người vừa dời (BR-322 đã ghi "Email still goes to the actor too" nên không phải sửa BR-322).
- Sửa UC-15 AF-1 bước 5 + POST-3 theo câu chữ mới.

## 4. Thư hủy họp — BR-300 + UC-75 bước 6 (khớp `CalendarInvitationBuilder.FileNameOf`)

- Thư hủy đính kèm tệp lịch tên `huy-hop.ics` (METHOD:CANCEL) để trình lịch gỡ sự kiện — trước đây DOCX/API chỉ nhắc `thu-moi-hop.ics` chung chung (thư mời METHOD:REQUEST).
- Sửa BR-300 + UC-75 bước 6 (ghi tên file), và dòng Request/Response của bảng giao tiếp ở §16 DOCX (T231R4: hủy họp đính kèm `huy-hop.ics`).

## 5. UC-68 POST-2 + Normal Flow bước 3 — hai round-trip (khớp `NotificationEndpoints` + `NotificationsProvider.openTarget`)

- PHÁT HIỆN THÊM khi đối chiếu: extract UC-68 POST-2/Nomal Flow bước 3 vẫn ghi "đánh dấu đã đọc" gộp một bước, trong khi code `GET /{id}/target` KHÔNG tự mark-read — FE gọi riêng `POST /{id}/read` rồi mới `GET /{id}/target` (thất bại khi lưu trạng thái đã đọc không chặn việc mở).
- Sửa POST-2 + bước 3 thành hai request riêng; AF-1 giữ nguyên (entry point vẫn bước 3). BR-53 ("attempts to mark ... Failure ... does not block") đã đúng code nên không sửa.
- Sửa cùng câu tóm tắt ở § màn hình (P1002) và đoạn worker JOB-01 (P1029: vừa tạo chuông vừa queue email nhắc, BR-303) cho nhất quán.

## 6. Đóng ticket (DEVELOPMENT_WORKFLOW §2.2)

Khi PR docs của note này vào `main`: comment lên SCRUM-208 một dòng (UC/BR đổi gì, PR + commit, còn gì — API doc đã khớp nên không còn việc), rồi chuyển theo luật review (ticket thuần docs). Không viết "đã vào main" từ trang PR — kiểm `git log --oneline origin/main` trước. Không merge, không mở PR từ nhánh này (lead mở PR 208 với base là nhánh note-52 rồi `gh stack link` với #394).
