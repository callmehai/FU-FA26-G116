# ADR-0034: Hạn mức lưu trữ tài liệu 10 GB cho cả hệ thống

- **Trạng thái:** Đề xuất — SCRUM-58, PO 27/10/2026
- **Ngày:** 2026-10-27
- **Người quyết định:** PO (Hải)

## Bối cảnh

Sáng 27/10/2026, PO chốt hạn mức **1 GB cho kho tài liệu của mỗi người**. PR #169 đã cài như vậy, với mã `MSG346`. Còn lại
một câu hỏi: tài liệu tải từ máy lên cuộc họp cũng tự vào kho của người tải (BR-192, BR-210), vậy có bị chặn khi kho đầy
không?

Khi trả lời câu đó, PO nhận ra hạn mức theo người không khớp với giới hạn thật của hệ thống:

- **Giới hạn thật là gói miễn phí của Cloudflare R2: 10 GB cho cả bucket** (ADR-0024). Mọi tệp tài liệu đều nằm ở đây,
  không phân biệt của ai.
- 1 GB mỗi người thì chỉ 10 người là đã làm đầy R2. Ngược lại, một người dùng nhiều mà hệ thống còn trống lại bị chặn vô cớ.
- Đơn vị nhận bàn giao sẽ tự trả tiền R2 theo nhu cầu. Con số phải đổi được mà không sửa code.

## Quyết định

1. **Bỏ hạn mức theo người. Thay bằng một hạn mức cho cả hệ thống**:
   - cấu hình bằng `FileStorage:MaxTotalGigabytes`, mặc định **10**;
   - trên server đặt qua `STORAGE_MAX_GB` trong `deploy/.env`.
2. **Đếm đúng thứ R2 tính tiền**: mọi dòng `StoredFile` chưa bị dọn (`PurgedAt` trống).
   - Gồm cả tệp đã xoá khỏi kho hoặc gỡ khỏi cuộc họp mà job dọn chưa xoá (BR-213), vì object vẫn còn trên R2.
   - Bản ghi âm nằm trên ổ đĩa server, không lên R2, nên không tính.
3. **Kiểm ở một chỗ cho mọi lần tải lên** (kho, cuộc họp, mục nghị sự): `StoredFileWriter`, đúng lúc ghi các dòng `StoredFile`.
   - Việc kiểm và ghi chạy dưới một khoá chung của hệ thống (`pg_advisory_xact_lock`), nên hai lần tải cùng lúc không cùng
     lọt.
   - Các dòng `StoredFile` được ghi trước khi đẩy byte, nên chúng **giữ chỗ** cho lần tải. Lần tải lỗi thì xoá object và
     đánh dấu các dòng đó đã dọn, trả lại chỗ ngay, không chờ job dọn 24 giờ.
4. **Đầy thì từ chối cả lần tải**:
   - trả `409 MSG346` "Bộ nhớ tài liệu của hệ thống đã đầy. Chưa có tài liệu nào được tải lên. Hãy báo quản trị viên.";
   - `MSG346` chưa lên `main`, nên được đổi nghĩa từ "kho 1 GB" sang tình huống này;
   - 409 chứ không 507: hàng đợi tải lên (ADR-0033) tự thử lại lỗi 5xx, mà đầy thì thử lại cũng vô ích.
5. **Màn Kho tài liệu hiện "x / 10 GB" của cả hệ thống**, có tooltip ghi rõ.
   - Hộp tải lên kiểm trước bằng chỗ còn trống, để không phải gửi 200 MB rồi mới bị từ chối.
   - `GET /api/documents` trả `storageUsedBytes` / `storageLimitBytes`, thay cho `usedBytes` / `quotaBytes`.

## Khác với đặc tả

- Quyết định "1 GB mỗi người" sáng 27/10/2026 (SCRUM-111) **bị thay**. BA cập nhật đặc tả UC-36 và UC-33a ở BA Final Review.
- Không ai bị chặn vì kho riêng của mình nhiều. Hệ thống đầy thì mọi người đều bị chặn, kể cả người chưa tải gì.
  Quản trị viên xử lý bằng cách nâng hạn mức hoặc dọn dữ liệu.

## Hệ quả

- **Khi bảo vệ:** hạn mức 10 GB khớp gói miễn phí của R2. Khi bàn giao, bên nhận đặt `STORAGE_MAX_GB` theo gói R2 họ chấp
  nhận trả, không phải sửa code.
- Không cần migration.
- Mọi lần tải lên của cả hệ thống xếp hàng qua một khoá, nhưng chỉ trong khoảnh khắc ghi các dòng `StoredFile`, không trong
  lúc truyền byte.
- Tổng dung lượng của cả hệ thống hiện cho mọi người dùng đã đăng nhập. Đây không phải dữ liệu nhạy cảm.
