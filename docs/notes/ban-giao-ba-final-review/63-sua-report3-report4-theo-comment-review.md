# Sửa Report 3 và Report 4 theo comment review (27/11/2026)

**Nhánh:** `docs/fix-report3-4-comments` (base `app`). **Phạm vi:** 26 comment trong hai DOCX
(`docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`: 11, `Report-4_Software Design Specification.docx`: 15).
Mọi comment đã được xử lý rồi xóa khỏi DOCX, nên file này là chỗ duy nhất ghi lại **comment nào, ở đâu, sửa thế nào**.
Người review: đọc từng dòng, mở đúng vị trí trong DOCX, báo lại chỗ nào chưa ổn.

Quy trình đã chạy: sửa DOCX → `check_docx.py` (OK) → `sync_reports.py --report 3` (Markdown snapshot, hai bản trích UC/BR và `uc-map.html`).
Report 4 chưa có script xuất Markdown. Chưa đẩy Drive.

## 1. Report 3

| # | Comment | Vị trí | Đã xử lý | Reviewer kiểm tra |
|---|---|---|---|---|
| 11 | Vẫn nhắc AI PROVIDER SETTINGS dù đã bỏ actor | §I.3.1, đoạn dưới Figure 6 | Bỏ `AiProviderSetting`; 42 bảng thành 41, còn 1 thực thể không mô tả ở 3.2 (`CalendarShare`). Vẽ lại Figure 6 (xem §3) | Số 41 khớp hình |
| 12 | Thừa khoảng trắng dưới ảnh | §I.2 đầu, dòng "View the full-size image" dưới Figure 1 | Dòng link spacing-after = 0, tiêu đề mục 2 spacing-before = 120 | Xem lại trong Word, chưa nhìn trực tiếp |
| 14, 15, 16 | Vẫn nhắc Report 4, mục IV | §I.3.2: Entity Recording (`ByteSize`, `RetainUntil`, `Takes`), Transcript (mô tả, `Status`) | Bỏ mọi "(Report 4, IV.x)"; cũng bỏ ở mô tả Recording ("Report 4, IV.4 to IV.6, describes…") dù không có comment | Không còn câu nào trỏ sang Report 4 |
| 239 | Bỏ "(III.16.6, III.16.8)", mô tả kỹ hơn, còn yêu cầu nào khác | §IV.2 Data retention (đoạn "Data retention:") | Liệt kê đủ thời hạn: ghi âm 180 ngày, thông báo đã đọc 90 ngày, mọi thông báo 180 ngày, mã khách 1 ngày, nội dung email đã gửi 30 ngày, audio/biểu quyết nháp/tệp không dùng; biên bản đã ban hành giữ bất biến | Số ngày khớp JOB-03/05 |
| 23 | Format còn khoảng trống, mô tả máy móc | §III.4.4 Permission Matrix, đoạn "Meeting roles" sau bảng | Viết lại: vai trò trong một cuộc họp chỉ có hiệu lực trong cuộc họp đó, không ai có quyền riêng; Khách như Participant trừ điều hành phòng, thư viện cá nhân, chia sẻ tài liệu; canh trái | Câu có đúng ý BR-055 |
| 28, 208 | Thừa job, chỉ giữ job 6 7 8 1 2 | §III.5.3 bảng Background Job Inventory; §III.16 (16.1–16.5) | Bỏ JOB-03/04/05/09/10 (Transcription, Summary, CombinedRecording, OrphanFilePurge, CallRoomJanitor). **Đánh lại số 1–5**: Notification = JOB-01, Email = JOB-02, RecordingRetention = JOB-03, MeetingOverdue = JOB-04, DataRetention = JOB-05; mục 16.1–16.5 đổi theo | Không nơi nào khác còn mã JOB cũ (JOB-06/07/08) |
| 228 | Viết dài quá, gọn lại Performance + Security | §IV.2.2 Performance (dòng audio/AI, thông báo, phòng họp, nhận dạng người nói); §IV.2.3 Security (dòng 1–7, 9–11, 13) | Rút gọn câu, giữ số liệu và ràng buộc; Audit logging đưa về một đoạn | Không mất ràng buộc nào so với bản cũ |
| 237 | Xem lại đoạn này (MSG20) | §V.2 System Messages, MSG20 (Create Role **và** Update Role) | Gọn còn: "Invalid role information" + 3 dòng lỗi trường (name ≤ 64, description ≤ 256, capabilities không hợp lệ) | Giới hạn 64/256 lấy từ bản cũ, **chưa đối chiếu với code** |

Làm thêm, ngoài comment (theo yêu cầu 27/11):

- **Bảng IV.2:** 8 bảng (Usability, Performance, Security, Reliability, Availability, Maintainability, Portability, Compliance) rộng 10206, lề −590.
- **Bỏ BR/UC/MSG khỏi IV.2:** không còn BR-xxx, UC-12, UC-73, MSG414 trong các bảng này. Mục V (Business Rules) không đổi.

## 2. Report 4

| # | Comment | Vị trí | Đã xử lý | Reviewer kiểm tra |
|---|---|---|---|---|
| 3 | Mở rộng width bảng, đồng bộ trong Report 4 | Mọi bảng 3 cột "No / Component / Aspect / Design…" (25 bảng, từ §I.2 đến §III) | Đưa về 10206 dxa, lề −590 (chuẩn bảng field của §II.3); bảng Change Log (8804) để nguyên | Không bảng nào tràn lề trang |
| 36 | Đồng bộ size, lấy bảng này làm chuẩn | Bảng field, quanh dòng `GuestInvitationId` | Dùng làm chuẩn cho #3 | — |
| 4 | Quá dài, không nhắc đoạn khác, bỏ từ "reconcile" | §I.2.1, bảng thành phần, dòng 16 "Background Workers" | Viết lại ngắn, không trỏ IV.x. Dòng JOB-10 trong IV.10 cũng bỏ "Reconciles" | Dòng 16 vẫn nêu đủ 10 worker |
| 5, 14 | Vẫn nhắc đoạn khác | §I.2.1 dòng 23 "Speech worker"; §I.2.3 dòng 21 Transcription (và dòng 30, Workers, JOB-03 trong IV.10) | Bỏ "IV.1–IV.3" và các câu "Algorithms: IV.x" | Không còn tham chiếu IV.x ở phần I–III |
| 6 | Xem lại đoạn host Singapore | §I.1.2, bullet "Host" | Bỏ "in Singapore": vị trí vùng không phục vụ thiết kế; còn "4 vCPU, 8 GB, Ubuntu 24.04 LTS" (khớp `deploy/README.md`) | Đây có phải ý của comment không |
| 10 | Mô tả lại | §I.2.3, dòng 06 "Abstractions and 23 business modules" | Mô tả: use case service của mỗi module (23), các interface tới lưu trữ/hệ thống ngoài (ports), các DTO | Số 23 module còn đúng |
| 11, 13 | Vẫn nhắc đoạn khác | §I.2.3 dòng 25 Recordings; dòng 17 Recordings | Bỏ "IV.4–IV.6", "Algorithms: IV.4/IV.5" | — |
| 43 | AI slop | §II.3.24 Recording, mô tả entity | Viết lại cho khớp Report 3: ba nguồn audio, nêu consent và ngày giữ; bỏ câu trỏ IV.4–IV.6 | — |
| 52 | Ai hỏi phần này đâu mà trả lời | §II.3.32 TranscriptSpeaker, mô tả | Bỏ chuyện "từ 03/11/2026, tự điền khi đạt ngưỡng"; còn: hệ thống gợi ý, tự điền khi khớp đủ mạnh, Thư ký sửa hoặc xóa được | Mô tả còn đúng với code |
| 241 | AI slop | §III.1.3 Data Protection, dòng 07 Personal data clean-up | Bỏ "the periods are in IV.6" | — |
| 245 | Đây nữa | §III.2.1 Real-Time Updates, dòng 03 Polling fallback | Bỏ "the intervals are listed in IV.7" | — |
| 248 | Mô tả thiết kế, bỏ chuyện đã test | §III.2.3 Capacity Design | Xóa số liệu load-test 03/11/2026; mô tả: 1 máy (4 vCPU, 8 GB) cho 1 cuộc họp 25–30 người, tối đa 50 người/phòng (LiveKit), nhận dạng giọng chỉ chạy CPU, hàng đợi từng bản ghi | Có nơi nào khác (Report 5, Report 6) cần giữ số liệu load-test không |
| 250 | Xem kĩ (Backup and restore) | §III.3 Reliability, dòng 09 | **Bản cũ sai**: ghi "backup hằng ngày ngoài máy, RPO 24h, RTO 4h". Thực tế (`deploy/README.md`): `pg_dump` trước mỗi lần deploy đổi image, giữ 14 bản trên cùng máy, copy ra ngoài là việc tay. Viết lại đúng như vậy, bỏ RPO/RTO | Nếu nhóm thật sự có backup hằng ngày thì báo, vì code/deploy không thể hiện |

**Bảng job (§IV.10):** vẫn đủ 10 job (Report 4 là chi tiết thiết kế). **Mã đã sắp lại để 5 job đầu trùng Report 3**:
JOB-01 Notification, 02 Email, 03 RecordingRetention, 04 MeetingOverdue, 05 DataRetention; JOB-06 Transcription,
07 Summary, 08 CombinedRecording, 09 OrphanFilePurge, 10 CallRoomJanitor. Mô tả JOB-01, 03, 04, 05 viết lại rõ hơn.

## 3. Sơ đồ ERD (Figure 6, Report 3)

- Nguồn: `docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio`. Đã xóa bảng `AiProviderSetting` (+ 7 dòng cột và quan hệ `User` → `AiProviderSetting`), xuất lại PNG, thay ảnh trong DOCX (3794×3135).
- Sơ đồ Chen (`docs/13-erd/chen/`) cũng xóa thực thể và quan hệ "Configures"; 35 thực thể, 70 quan hệ.
- README hai thư mục `13-erd` cập nhật số bảng (41) và ghi rõ bảng vẫn còn trong code.
- **Còn lệch cố ý:** bảng `ai_provider_settings` vẫn còn trong code, migration và `docs/02-design/02-erd.md`; hình và Report 3 không vẽ. Nếu muốn khớp tuyệt đối thì phải gỡ cả code (ngoài phạm vi).

## 4. Việc reviewer nên làm

1. Mở hai DOCX trong Word, kiểm tra **bố cục**: 8 bảng IV.2 và 25 bảng Report 4 không tràn lề; khoảng trắng dưới Figure 1 (#12); xuống dòng trong MSG20.
2. Đọc lại các dòng đã viết lại ở Performance/Security (Report 3) và 3 dòng #6, #248, #250 (Report 4), vì đây là chỗ tôi diễn đạt lại nội dung chứ không chỉ cắt.
3. Đối chiếu #237: 64/256 ký tự có đúng với `RoleEndpoints`/Application không.
4. Xác nhận quyết định **#250** (bỏ RPO/RTO) và **#248** (bỏ số liệu load-test).
5. Quét tay lần cuối: không còn "Report 4, IV" trong Report 3, không còn "IV.x" ở phần I–III của Report 4.
