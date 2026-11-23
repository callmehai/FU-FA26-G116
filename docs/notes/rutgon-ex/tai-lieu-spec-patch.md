# tai-lieu-spec-patch — rút gọn Exception Flow (document-management)
n> **Lưu ý:** nếu mục này lệch với `tai-lieu-spec-patch.md` thì theo file `tai-lieu-ex-final.md` (sinh từ diagram sau lượt chọn cuối của BA).

Áp dụng cho các UC đã viết lại `.txt`. UC chưa xử lý (ba-chon) không có mục ở đây.

## UC-32a — View Meeting Documents
- Đã rút gọn ở nhánh base; không đổi thêm.
- EX giữ: EX-1 MSG156 (không truy cập được tài liệu cuộc họp).
- EX bỏ: EX-2 MSG157 (không tải được, hạ tầng).

## UC-32c — Preview Meeting Document
- EX giữ:
  - EX-1 MSG156 — tài liệu không còn truy cập được.
  - EX-2 MSG159 — cấm tải tài liệu cuộc họp Mật.
  - EX-3 MSG382 — tệp lưu trữ không còn.
- EX bỏ/gộp:
  - EX-2 MSG158 (không hiển thị được, hạ tầng) — bỏ.
  - EX-4 MSG160 (không cung cấp được tệp tải, hạ tầng) — bỏ.
  - EX-6 MSG383 (trình duyệt không hiển thị được, phía máy khách) — bỏ.
- Tham chiếu cần sửa trong spec: audit entry không lưu được (EX-2/EX-4 cũ) không còn; "flow resumes at Step 4" của EX-3 cũ nay là EX-2.

## UC-34 — View Personal Document Library
- EX giữ:
  - EX-1 MSG175 — thư mục đã chọn không còn.
- EX bỏ:
  - EX-1 MSG176 (không tải được thư viện, hạ tầng) — bỏ.
- Tham chiếu: old EX-2 MSG175 nay là EX-1.

## UC-34b — View a Locked Account's Personal Library
- EX giữ:
  - EX-1 MSG175 — tài khoản không tồn tại hoặc không bị khóa.
- EX bỏ:
  - EX-2 MSG176 (không ghi được audit, hạ tầng) — bỏ.
  - EX-3 MSG158 (không đọc được tệp / không ghi audit, hạ tầng, trong ghi chú AF-1) — bỏ.

## UC-71a — Delete Document from Personal Library
- EX giữ:
  - EX-1 MSG175 — tài liệu không tồn tại hoặc không còn thuộc người dùng.
- EX bỏ:
  - EX-2 MSG181 (không xóa được, hạ tầng) — bỏ.

## UC-71b — Rename Document
- EX giữ:
  - EX-1 MSG175 — tài liệu không tồn tại hoặc không còn thuộc người dùng.
  - EX-2 MSG347 — tên tài liệu không hợp lệ.
- EX bỏ:
  - EX-2 MSG184 (không đổi tên được, hạ tầng) — bỏ.
- Tham chiếu: old EX-3 MSG347 nay là EX-2.

## UC-71c — Move Document to Folder
- EX giữ:
  - EX-1 MSG175 — tài liệu không tồn tại hoặc không còn thuộc người dùng.
  - EX-2 MSG179 — thư mục đích không còn.
- EX bỏ:
  - EX-3 MSG186 (không di chuyển được, hạ tầng) — bỏ.

## UC-72b — Rename Folder
- EX giữ:
  - EX-1 MSG175 — thư mục không tồn tại hoặc không thuộc người dùng.
  - EX-2 MSG188 — tên thư mục trống.
  - EX-3 MSG348 — tên thư mục vượt độ dài.
- EX bỏ:
  - EX-3 MSG192 (không đổi tên được, hạ tầng) — bỏ.
- Tham chiếu: old EX-4 MSG348 nay là EX-3.

## UC-72d — Delete Folder
- EX giữ:
  - EX-1 MSG175 — thư mục không tồn tại hoặc không thuộc người dùng.
  - EX-2 MSG196 — thư mục không rỗng.
- EX bỏ:
  - EX-3 MSG198 (không xóa được, hạ tầng) — bỏ.