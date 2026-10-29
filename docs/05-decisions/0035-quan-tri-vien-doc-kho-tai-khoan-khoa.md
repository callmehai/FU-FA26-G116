# ADR-0035: Quản trị viên đọc kho của tài khoản đã khoá

- **Trạng thái:** Đề xuất — SCRUM-58 (SCRUM-111), PO 27/10/2026
- **Ngày:** 2026-10-27
- **Người quyết định:** Hải (PO), ghi ở SCRUM-58

## Bối cảnh

[ADR-0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) để ngỏ hai câu hỏi: hạn mức dung lượng kho cá nhân, và ai đọc được kho
của người đã rời tổ chức. Câu đầu đã được chốt bởi [ADR-0034](0034-han-muc-luu-tru-toan-he-thong.md) (10 GB cho cả hệ thống,
thay cho 1 GB mỗi người); ADR này chốt câu sau.

Tài liệu trong kho có thể chứa dữ liệu cá nhân và tài liệu của cuộc họp Mật. Quyền đọc của Quản trị viên vì thế phải có dấu vết
kiểm toán và không được thành đường vòng qua quy tắc đọc tài liệu cuộc họp.

## Quyết định

1. **Tài khoản đã khoá thì Quản trị viên đọc được kho của người đó, mỗi lần đọc ghi nhật ký kiểm toán.** Chỉ đọc (xem danh sách,
   đọc tệp); không sửa, xoá hay chuyển. Người dùng còn hoạt động thì kho vẫn riêng tư (BR-242): Quản trị viên nhận cùng lỗi
   "không còn trong kho" như người ngoài.
2. **Quản trị viên không thấy tệp của cuộc họp Confidential hoặc Secret, và thư mục tải lên của cuộc họp đó cùng mọi thư mục con**,
   kể cả khi Quản trị viên xem được cuộc họp. Tài liệu của cuộc họp đó chỉ mở theo quy tắc của chính cuộc họp (không tải về,
   tài liệu đã gỡ không hiện, có nhật ký), kho không được là đường vòng.
3. **Không ghi được nhật ký thì không cho đọc.** Mở tệp lỗi trả `MSG158`; tải danh sách lỗi trả `MSG176`.
4. **UC-36 và UC-33b AF-1 áp cùng giới hạn với UC-33a:** tối đa 10 tệp và 200 MB mỗi lần chọn, mỗi tệp ≤ 50 MB; vượt thì dùng lại
   `MSG288` (BR-319 mở rộng cho cả ba UC).
5. **UC-33b/33d dùng phương án (b):** gắn/gỡ tài liệu vào mục nghị sự là chức năng riêng, Chủ toạ và Thư ký đều dùng được
   (đã ghi ở SCRUM-112).

Hạn mức 10 GB, `MSG346` và cách đếm dung lượng nằm ở ADR-0034; hàng đợi tải lên theo từng tệp nằm ở
[ADR-0033](0033-hang-doi-tai-len-o-trinh-duyet.md). ADR này không nhắc lại.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Xoá kho khi khoá tài khoản | Không ai đọc được dữ liệu cá nhân | Mất tài liệu chưa gắn vào cuộc họp nào | PO chọn giữ và cho Quản trị viên đọc |
| Quản trị viên đọc cả kho, kể cả tệp cuộc họp Mật | Khôi phục được mọi thứ | Vòng qua quy tắc đọc tài liệu cuộc họp Mật | Bị loại: tệp của cuộc họp Confidential/Secret luôn ẩn |
| Đọc kho của mọi người, kể cả tài khoản còn hoạt động | Đơn giản | Phá tính riêng tư của kho (BR-242) | Chỉ tài khoản đã khoá |

## Hệ quả

- Quản trị viên đọc kho tài khoản khoá cần nêu mục đích đọc và hạn lưu nhật ký theo Luật 91/2025/QH15; chưa có quyết định về hạn lưu.
- Report 3 có UC-34b, BR-334, BR-335. Permission Matrix chưa thêm quyền này (ngoài phạm vi sửa lần này).
- Code đã làm ở PR #169 (`PersonalLibraryService`, `GET /api/users/{userId}/library`).
- Phần "Chưa quyết" của ADR-0006 được chốt bởi ADR-0034 và ADR này; ADR-0006 không bị sửa.
