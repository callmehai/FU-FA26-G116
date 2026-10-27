# ADR-0031: Hạn mức lưu trữ toàn hệ thống 10 GB và Quản trị viên đọc kho của tài khoản đã khoá

- **Trạng thái:** Đề xuất — SCRUM-58 (SCRUM-111), PO 27/10/2026; hạn mức đổi từ 1 GB mỗi người sang 10 GB toàn hệ thống cùng ngày
- **Ngày:** 2026-10-27
- **Người quyết định:** Hải (PO), ghi ở SCRUM-58; KhanhPG (BA) chuyển hạn mức về toàn hệ thống

## Bối cảnh

[ADR-0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) để ngỏ hai câu hỏi: hạn mức dung lượng kho cá nhân, và ai đọc được kho
của người đã rời tổ chức. Cả hai chặn việc code SCRUM-111 (tài liệu trong kho). Đặc tả UC-36 cũng chưa ghi giới hạn số tệp
mỗi lần tải, trong khi UC-33a AF-1 đã có (`MSG288`, BR-319).

PO ban đầu chốt 1 GB mỗi người. Cùng ngày, hạn mức được chuyển thành **tổng 10 GB cho cả hệ thống, không giới hạn riêng từng
người**: dung lượng thật sự bị chặn bởi ổ lưu trữ chung (Cloudflare R2, [ADR-0024](0024-luu-file-tai-lieu-tren-cloudflare-r2.md)),
nên giới hạn theo người không bảo vệ được gì thêm mà còn phải đo từng kho.

Tài liệu trong kho có thể chứa dữ liệu cá nhân. Quyền của Quản trị viên đọc kho vì thế phải có dấu vết kiểm toán.

## Quyết định

1. **Tổng dung lượng mọi tệp tài liệu trong hệ thống tối đa 10 GB; mỗi người không có hạn mức riêng.** Dung lượng đã dùng là
   tổng kích thước của các tệp tài liệu còn được lưu (tài liệu cuộc họp và kho cá nhân). Một lần tải làm tổng vượt 10 GB thì
   **từ chối cả lần tải**, không lưu tệp nào (cùng nguyên tắc tất cả hoặc không của BR-286). Áp cho mọi đường tải tệp lên:
   UC-33a AF-1, UC-33b AF-1 và UC-36. Từ chối bằng **một mã mới** lấy từ "Mã tiếp theo" của catalogue (`MSG345`), người code
   cấp cùng commit. Màn kho hiện dung lượng đã dùng của hệ thống trên 10 GB.
2. **Tài khoản đã khoá thì Quản trị viên đọc được kho của người đó, mỗi lần đọc ghi nhật ký kiểm toán.** Chỉ đọc (xem danh sách,
   đọc tệp); không sửa, xoá hay chuyển. Với người dùng còn hoạt động, kho vẫn riêng tư (BR-242). Nếu không ghi được nhật ký
   thì không cho đọc. Đặc tả: UC-34b, BR-334, BR-335 ở Report 3.
3. **UC-36 và UC-33b AF-1 áp cùng giới hạn với UC-33a:** tối đa 10 tệp và 200 MB mỗi lần tải, mỗi tệp ≤ 50 MB; vượt thì dùng lại
   `MSG288`. BR-319 mở rộng cho cả ba UC.
4. **UC-33b/33d dùng phương án (b):** gắn/gỡ tài liệu vào mục nghị sự là chức năng riêng, Chủ toạ và Thư ký đều dùng được
   (đã ghi ở SCRUM-112).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Không đặt hạn mức nào | Ít việc nhất | Ổ lưu trữ chung có thể đầy mà không ai biết | Cần một trần cho cả hệ thống |
| 1 GB mỗi người (quyết định đầu của PO) | Công bằng giữa người dùng | Phải đo dung lượng từng kho; một người dùng ít vẫn để trống phần của mình; tổng vẫn không bị chặn khi nhiều người | Thay bằng trần chung 10 GB |
| Hạn mức mềm: cho tải, chỉ cảnh báo | Người dùng không bị chặn | Không chặn được ổ đầy | Cần từ chối thật |
| Cắt bớt: lưu tệp vừa đủ, bỏ phần dư | Tận dụng đủ chỗ | Kết quả khó đoán, trái "tất cả hoặc không" | Lệch BR-286 |
| Xoá kho khi khoá tài khoản | Không ai đọc được dữ liệu cá nhân | Mất tài liệu chưa gắn vào cuộc họp nào | PO chọn giữ và cho Quản trị viên đọc |

## Hệ quả

- Một người có thể dùng hết 10 GB và chặn mọi người khác tải lên. Chấp nhận ở quy mô đồ án; Quản trị viên xem dung lượng đã dùng
  và đọc kho tài khoản khoá khi cần. Muốn chống chuyện này thì thêm hạn mức mỗi người bằng ADR mới.
- Dung lượng chỉ tính tệp tài liệu; bản ghi cuộc họp và bản gỡ băng không tính vào 10 GB (chưa có quyết định về dung lượng của chúng).
- UC-33a AF-1 đã code (SCRUM-107) chưa kiểm hạn mức: cần sửa cùng SCRUM-111 để cả ba đường tải dùng chung một kiểm tra.
- SCRUM-111 thêm kiểm dung lượng hệ thống vào mọi đường tải và màn kho; cần mã `MSG345` trong catalogue cùng commit code.
- Cần một dịch vụ đọc nhanh tổng dung lượng (tổng `StoredFile.SizeBytes` còn tham chiếu) để không quét cả bảng mỗi lần tải; hai
  lần tải đồng thời phải không cùng vượt trần (khoá hoặc kiểm trong transaction).
- BA đã viết UC-34b, BR-333–BR-335 và `MSG345` vào Report 3. Cần thêm quyền của Quản trị viên vào Permission Matrix (chưa làm,
  ghi trong note bàn giao).
- Sequence UC-36 và UC-33b AF-1 có thêm nhánh `MSG288` và `MSG345`.
- Phần "Chưa quyết" của ADR-0006 được chốt ở đây; ADR-0006 không bị sửa.
