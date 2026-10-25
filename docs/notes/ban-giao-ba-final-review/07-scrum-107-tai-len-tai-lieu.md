# SCRUM-107 — tải tài liệu từ máy lên cuộc họp

**Ngày ghi:** 24/10/2026
**Nguồn:** mục *Deviations from the docs (for BA Final Review)* trong mô tả PR #128
**Khi nào sửa:** lúc **BA Final Review**

PR: https://github.com/callmehai/TrustCabinet/pull/128
Ticket: https://sep490g116.atlassian.net/browse/SCRUM-107

Backend của UC-33a AF-1: `POST /api/meetings/{id}/documents/upload`, byte trên Cloudflare R2 (ADR-0024). Contract đã viết ở `docs/03-api/11-tai-lieu-cuoc-hop.md`. Không có mã thông báo mới: MSG161 (201), MSG162 (403), MSG164 (400), MSG166 (503), MSG172 (409), MSG174 (404), MSG288 (400).

## Lệch tài liệu — cần BA cập nhật

1. **Kiểm quyền trước khi kiểm tệp.** Sequence đang kiểm giới hạn và loại tệp trước. Lý do: người không được tải không được đẩy 200 MB lên đĩa server, và câu trả lời "có được tải không" không phụ thuộc nội dung đã gửi. Hệ quả: Thành viên gửi tệp không hợp lệ nhận MSG162, không phải MSG164.
2. **Kho dev là SeaweedFS, không phải MinIO** (ADR-0024 §7, vẫn *Đề xuất*, đã cập nhật trong PR). MinIO không còn image công khai. Cùng API S3, dev không auth. Bucket phải tạo một lần (`curl -X PUT http://localhost:8333/trustcabinet-dev`): bucket SeaweedFS tự tạo lúc ghi lần đầu thì ghi được nhưng đọc trả NoSuchBucket.
3. **MSG288 là 400** cho cả "> 10 tệp" và "> 200 MB" (kể cả khi `Content-Length` > 201 MB, từ chối trước khi đọc body).
4. **MSG174 404 có body** (đúng ticket) — cùng một body cho cuộc họp bị giấu và cuộc họp không tồn tại.
5. **Không có body / không phải multipart / multipart hỏng → MSG164**, vì không được cấp mã mới.
6. `pageCount` từ trình duyệt chỉ lưu cho PDF và chỉ khi là số nguyên dương; còn lại lưu là không rõ (null), không từ chối request.
7. Lập chương trình nghị sự vẫn giữ "mỗi tài liệu một mục" trên bảng nối mới (ADR-0020 cho phép nhiều mục; phần đó là UC-33b, SCRUM-58).
8. Fixture dev của agenda giờ là dòng `MeetingDocument` không có object trên kho, và chỉ DB mới seed được (seeder bỏ qua cuộc họp đã có, nên DB dev và server test đang chạy mất hai tài liệu fixture mỗi cuộc họp `[SCRUM-25]`). Chúng hiện trong danh sách nhưng không mở được (SCRUM-21).
9. **Đuôi phải là `.pdf` hoặc `.docx` và khớp nội dung**, thêm vào việc nhận diện theo byte, vì SCRUM-21 trả tệp về đúng tên đó. PDF chỉ nhận khi `%PDF-` ở offset 0.
10. Người từng quản lý nhưng mất vai trò rồi không còn xem được cuộc họp (ví dụ Nháp) nhận 404/MSG174, không phải MSG162 — đúng quy tắc 403/404.

## Nguồn khi sửa

- `docs/03-api/11-tai-lieu-cuoc-hop.md`
- `docs/05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md`
- Sequence `docs/06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting`
