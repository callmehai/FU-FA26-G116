# PR #260 (SCRUM-36 + SCRUM-32) — dự thảo AI và biên bản; SCRUM-137 Tìm giờ họp

**Người ghi:** KhánhPG/BA, 05/11/2026. **Nguồn:** PR #260 và comment Jira của SCRUM-36, SCRUM-32; tin nhắn Zalo.

PR #260 có mấy chỗ làm khác ticket, đã ghi trong comment Jira của 2 ticket. Lúc BA Final Review nhờ Khánh cập nhật:

## 1. UC-47–UC-50 (dự thảo AI)

- Dự thảo có 3 mục I–III như eCab.
- Chỉ Chủ tọa/Thư ký xem, sửa, duyệt; người khác chỉ thấy sau khi duyệt; sửa xong phải duyệt lại.
- Kết quả biểu quyết chép từ dữ liệu, AI không viết.

## 2. UC-58–UC-60 (biên bản)

- Chỉ 1 mẫu cố định (Mẫu 1.4 NĐ30), không có màn quản lý mẫu.
- Mới xuất Word, chưa có PDF.
- Soạn bằng văn bản thuần, có xem trước.
- Cuộc họp Mật không cho tải về.

## 3. Sequence diagram + BR

- Theo `docs/03-api/17-du-thao-bien-ban.md` và ADR-0054.
- Mã thông báo MSG384–MSG405.

## 4. SCRUM-137 Tìm giờ họp

- Khánh nhờ tạo ticket "tìm họp" trên Zalo; đã tạo SCRUM-137 Tìm giờ họp (theo màn "Tìm giờ họp" của eCab, US #352) và giao cho Khánh.
- Phần BE: tìm các khung giờ mà mọi người được chọn cùng rảnh; chỉ trả bận/rảnh, không lộ nội dung cuộc họp của người khác.
PY