# Ghi chú và bàn giao tài liệu

Thư mục chứa note rà soát và bàn giao việc cập nhật tài liệu giữa các thành viên.
Mỗi note ghi ngày, phạm vi cần xử lý và link tới nguồn chuẩn; không thay thế yêu cầu trong Jira,
UC Specification, Business Rules hoặc ADR đã chốt.

## Danh sách

- [Kế hoạch BE SCRUM-137](NOTE-ke-hoach-be-scrum-137.md) — 11/11/2026; kế hoạch triển khai từng bước tìm giờ họp (UC-85a, UC-85b):
  tham số và mặc định (BR-421, BR-422), cách sinh khung, kiểm lại khung khi tạo nháp (BR-428), API, test, chỗ lệch cần ghi Jira.
- [Kế hoạch BE SCRUM-142](NOTE-ke-hoach-be-scrum-142.md) — 08/11/2026, sửa theo PO 09/11/2026; kế hoạch triển khai từng bước chia sẻ lịch
  (UC-84a → UC-84f, UC-85c): bảng `calendar_shares`, quy tắc BR-414 → BR-431 trong code, API, thông báo/audit, dừng (BR-424), test, chỗ lệch cần ghi Jira.
- [Đặc tả sơ bộ SCRUM-16](NOTE-dac-ta-so-bo-scrum-16.md) — 10/11/2026, **đã chốt và vào Report 3 cùng ngày**; nhật ký
  kiểm toán: đối chiếu 6 tiêu chí của ticket, UC-13 viết lại, BR-440 → BR-446, `MSG469`–`MSG470`, sequence UC-13 vẽ
  lại. BE đã làm 10/11/2026 (PR #311, [contract](../03-api/21-nhat-ky-kiem-toan.md)): `GET /api/audit-log`, ghi đăng
  nhập, chuyển trạng thái cuộc họp, ban hành biên bản. FE đã làm cùng ngày (màn `/mgmt/admin/audit-log`); còn đặc tả
  màn 3.3.

- [Refactor backend sang Clean Architecture](NOTE-refactor-clean-architecture.md) — 08/11/2026, cập nhật 14/11/2026; thời điểm chốt ở ADR-0059 (tách sau buổi báo cáo giảng viên 13/11/2026);
  hiện trạng (use case nằm trong Infrastructure, dính EF/Npgsql), kiến trúc đích Domain / Application / Infrastructure / Web,
  port `IUnitOfWork` và exception lưu trữ, `ArchitectureTests`, bảng 39 lớp Infrastructure và 7 publisher Web đang đọc `DbContext`,
  lộ trình 5 giai đoạn theo module (thêm CalendarSharing, Reports, Speaking), rủi ro, 4 câu hỏi cần chốt.
- [Đặc tả sơ bộ SCRUM-30](NOTE-dac-ta-so-bo-scrum-30.md) — 06/11/2026, sửa theo review #271; bước BE viết UC cơ bản:
  UC-82 Ghi chú cuộc họp, BR-395 → BR-403 đề xuất, `MSG415`–`MSG423` dành sẵn, sequence `.txt`, chỗ lệch khỏi ticket
  (không theo dõi nhiệm vụ) và 2 câu hỏi cho PO/BA.
- [Đặc tả sơ bộ SCRUM-33](NOTE-dac-ta-so-bo-scrum-33.md) — 07/11/2026, sửa theo review #279 ngày 08/11; luồng đã chốt: một người duyệt, duyệt là ban
  hành luôn, không tách bước ký. UC-61 gửi duyệt, UC-64 duyệt và ban hành, UC-65 trả lại, UC-83 đính chính (mới),
  UC-58 mở rộng; BR-404 → BR-413 đề xuất, `MSG424`–`MSG434` và `MSG456` dành sẵn, 4 sequence `.txt`, chỗ lệch khỏi ticket
  (cuộc họp giữ `Ended`, đổi nghĩa UC-61/64/65, UC-62/63 để cho ký số SCRUM-37) và câu hỏi cho PO/BA.
- [Đặc tả sơ bộ SCRUM-137/SCRUM-142](NOTE-dac-ta-so-bo-scrum-137-142.md) — 07/11/2026 (tách UC Respond + tách mã, đổi số tránh PR #279, tách UC tạo nháp, gộp UC theo mẫu Report 3, 07/11/2026); bước BE viết UC cơ bản:
  UC-84 gom *Quản lý chia sẻ lịch* (UC-84a xin xem, UC-84b chủ lịch tự chia sẻ, UC-84c trả lời, UC-84d thay đổi,
  UC-84e rút/ngừng) + UC-85 gom *Tìm giờ họp* (UC-85a tìm giờ, UC-85b tạo nháp từ khung, UC-85c xem lịch),
  BR-414 → BR-431 đề xuất, `MSG435`–`MSG455` dành sẵn (mã tiếp theo `MSG456`), 8 sequence `.txt`
  + tab và ảnh, quyết định mới D12 (khung trùng Mật vẫn cho tạo nháp), số dựa trên PR #279 merge trước,
  comment cần đăng lên Jira (chưa đăng).
- [Đặc tả sơ bộ SCRUM-38](NOTE-dac-ta-so-bo-scrum-38.md) — 09/11/2026, **đã chốt và vào Report 3 cùng ngày**: UC-70a Xem
  báo cáo, UC-70b Xuất Excel, BR-432 → BR-438, `MSG463`–`MSG466`, 2 sequence, chỗ lệch khỏi ticket và Report 3 (mọi
  người dùng nội bộ xem được, Quản trị không thấy toàn hệ thống, bỏ PDF); còn đặc tả màn 15.1 chờ FE.
- [Danh sách unit test theo module](NOTE-danh-sach-unit-test.md) — 03/11/2026; 124 case thiết kế theo
  sáu cột của Report 5.1 (No, Module Name, Method Name, Sheet Name, Description, Pre-Condition) cho
  22 module đang chạy thật, kèm bảng Source trỏ file test trong `tests/`.
- [Phòng họp trực tuyến — rà soát FE-08 25/10/2026](NOTE-phong-hop-truc-tuyen-25-10-2026.md) — đặc tả
  UC-37 → UC-42, UC-39d, UC-78, BR-323 → BR-332 và mã FE `MSG328`–`MSG344`, `MSG350` (gộp main 29/10/2026);
  việc còn lại của BE: Guest chưa vào được phòng (chờ SCRUM-25).
- [Bàn giao FE-07 cho Lộc — kho tài liệu cá nhân, gắn/gỡ, bình luận, xác nhận đọc](NOTE-ban-giao-fe07-kho-tai-lieu-cho-loc.md) — 27/10/2026;
  hiện trạng ticket và code, chỗ lệch (UC-33a luồng chính chưa có ticket, UC-33b/33d đang chạy qua API agenda),
  SCRUM-58 tách thành SCRUM-110/111/112, hai nhánh docs và kế hoạch từng bước vẽ sequence (SCRUM-91) và class diagram
  (SCRUM-92) kèm checklist review.
- [Review PR #136 và Bổ sung BA Final Review](ban-giao-ba-final-review/10-review-pr136-va-bo-sung-sau-merge.md) — 25/10/2026;
  tổng hợp toàn bộ feedback review PR #136 (CI đỏ .mmd, conflict main, MSG174, BR-115, UC-28b, UC-73, số EX trong sequence)
  và các chỉ đạo PO mới nhất từ PR #133 (không tự gửi thông báo), #134 (rủi ro BR-188), #135/SCRUM-98 (BR-72/238/307) và #141 (FE màn 5.2).
- [Bàn giao BA Final Review 24/10/2026](ban-giao-ba-final-review/README.md) — bảy note của leader,
  xử lý lần lượt: SCRUM-18/20, SCRUM-97, Voice ID, SCRUM-26, SCRUM-107, Chương trình nghị sự (SCRUM-98),
  và Review PR #136 / bổ sung sau merge.
- [Tài liệu cuộc họp — SCRUM-21/22/23](NOTE-tai-lieu-scrum-21-22-23.md) — 22/10/2026; UC-32a/32c/35, BR-315 → BR-318,
  sequence, class diagram FE-07, wireframe v7; so sánh với AI-eCabinet, câu hỏi cho PO và phần còn phải đồng bộ.
- [Bàn giao cập nhật màn hình theo Report 3 ngày 18/10/2026](NOTE-cap-nhat-man-hinh-18-10-2026.md) — màn 4.x, 5.x,
  6.1, 7.5, 8.3, 11.x cần sửa theo UC/BR mới (agenda, vote gắn tài liệu, giao chủ toạ, huỷ cuộc họp); SCRUM-100.
- [Thay đổi để dành cho UC chưa đặc tả](NOTE-uc-chua-dac-ta-cho-sua.md) — 18/10/2026; phần ý PO/hội đồng chạm UC
  placeholder (FE-08, FE-09, FE-10, FE-12) và các vùng Report 3 chưa sửa trong đợt này.
- [Báo cáo nhận diện giọng nói theo góp ý hội đồng](NOTE-bao-cao-nhan-dien-giong-noi.md) — 23/10/2026; trả lời mục 1–3
  của góp ý lần 1: Voice ID có đồng ý, độ chính xác đo trên VIVOS và một cuộc họp thật, bảng chi phí AI, đối chiếu
  Luật 91/2025 và NĐ 356/2025, việc còn lại.
- [Góp ý của hội đồng — lần review 1](NOTE-gop-y-hoi-dong-lan-1.md) — ghi nhận 18/10/2026; xác định người nói,
  chi phí AI, huỷ/quá hạn cuộc họp, người ký và ban hành biên bản, uỷ quyền, nhắc lịch, NĐ 13/2023, chữ ký số. Đã áp dụng một phần vào Report 3 18/10/2026.
- [Bàn giao FE-08 đăng ký phát biểu (giơ tay, mời phát biểu) cho BE (SCRUM-27)](NOTE-ban-giao-fe08-dang-ky-phat-bieu-cho-be.md) — 06/11/2026; việc BE theo UC-79, chỗ spec chưa khớp code (Khách không có UserId), câu hỏi mở.
- [Bàn giao FE-11 biểu quyết cho BE (SCRUM-96)](NOTE-ban-giao-fe11-bieu-quyet-cho-be.md) — 28/10/2026;
  thứ tự đọc contract/ERD/sequence/class/SM, việc tách PR code schema, checklist BE và chỗ còn chờ chốt.
- [Hồ sơ theo dõi và review UC, BR, MSG của FE-11](NOTE-review-fe11-uc-br-msg.md) — bản tiếng Việt dễ đọc của UC-52a/52b, UC-53–57, BR liên quan và MSG205–236; có liên kết đến các danh mục chuẩn để đối chiếu.
- [Bàn giao cập nhật đặc tả màn hình FE-11](NOTE-cap-nhat-man-hinh-fe11.md) — cập nhật 15/10/2026,
  có bảng nút → UC → MSG và quyền theo trạng thái; đặc tả màn hình/ảnh chờ người phụ trách cập nhật.
- [Bàn giao cập nhật đặc tả màn hình FE-07 và các điểm liên quan ADR-0020](NOTE-cap-nhat-man-hinh-adr0020.md)
  — ngày 15/10/2026; quyền đọc, thêm/gỡ/gắn tài liệu, bình luận và ngày đặt tên thư mục.

Quyết định PO ngày 15/10/2026 về tài liệu và biểu quyết nằm ở
[ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md). Các note chỉ theo dõi phần màn hình
pending; UC/BR/MSG và mô tả dữ liệu đích đã đồng bộ trong Report 3 local.

## Nguồn liên quan

- [Quy trình phát triển](../DEVELOPMENT_WORKFLOW.md).
- [Yêu cầu và đặc tả UC](../01-requirements/README.md).
- [Quyết định đã chốt](../05-decisions/README.md).
- [Nguồn report và quy trình đồng bộ](../08-reports/README.md).
