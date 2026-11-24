# ADR-0084: Hạn mức lưu trữ tài liệu của cả hệ thống lên 250 GB

- **Trạng thái:** Chấp nhận — Hải 24/11/2026
- **Ngày:** 2026-11-24
- **Người quyết định:** Hải
- **Thay:** chỉ mức mặc định 10 GB của [ADR-0034](0034-han-muc-luu-tru-toan-he-thong.md); cách đếm và chặn của ADR-0034 giữ nguyên
- **Liên quan:** [ADR-0065](0065-hai-nhanh-hai-server.md) (kho tệp tự host `files`), [ADR-0082](0082-go-server-demo-main.md) (gỡ server demo dùng Cloudflare R2)

## Bối cảnh

ADR-0034 đặt hạn mức tổng 10 GB cho mọi tài liệu vì khi đó byte nằm trên Cloudflare R2 và 10 GB là gói miễn phí của R2.
Nay server duy nhất là `app`; tệp nằm trong kho S3 tự host (SeaweedFS, service `files`) trên SSD 309 GB của chính
server đó. Server demo dùng R2 đã gỡ ngày 24/11/2026 (ADR-0082), nên gói miễn phí của R2 không còn là giới hạn.

Database PostgreSQL nằm trên cùng đĩa. Nếu tài liệu tải lên làm đầy đĩa thì database không ghi được nữa và cả hệ thống
dừng, không chỉ phần tài liệu.

## Quyết định

1. **Mặc định 250 GB** cho tổng dung lượng tài liệu của cả hệ thống (`FileStorage:MaxTotalGigabytes`). Chừa khoảng
   35 GB của đĩa cho database, bản ghi cuộc họp, image Docker và hệ điều hành.
2. **Giữ cơ chế của ADR-0034**: đếm mọi `StoredFile` chưa bị dọn, kiểm dưới khoá chung trước khi đẩy byte, đầy thì
   `409 MSG346` và không tệp nào được thêm. Hạn mức giờ để bảo vệ đĩa mà database dùng chung, không còn để khớp gói
   miễn phí.
3. **Đổi được không sửa code**: đặt `STORAGE_MAX_GB` trong `deploy/.env` của server.

## Hệ quả

- Default trong `FileStorageOptions`, `deploy/docker-compose.yml` và `deploy/.env.example` đổi sang 250. Server `app`
  nhận 250 GB ở lần deploy tiếp theo, trừ khi `deploy/.env` của nó đặt `STORAGE_MAX_GB`.
- Kho cá nhân hiện "x / 250 GB" vì FE đọc `storageLimitBytes` từ API, không cần sửa FE.
- Test đọc hạn mức từ `FileStorageOptions`, không ghi cứng 10 GB, nên không đổi.
- UC Specification trong DOCX Report 3 còn ghi 10 GB ở UC-36a, UC-36b, UC-39; BA sửa DOCX rồi sinh lại.
- Bản ghi cuộc họp cũng nằm trên đĩa này nhưng không tính vào hạn mức (như ADR-0034). Nếu đĩa đầy vì bản ghi thì hạ
  `STORAGE_MAX_GB` hoặc dọn bản ghi; theo dõi bằng `df -h` trên server.
