# ADR-0052: Mọi chip cùng bo góc 4px; câu giải thích nằm sau icon (i), không giấu trong tooltip của chữ

- **Trạng thái:** Đề xuất — PO (Hải) 03/11/2026
- **Ngày:** 2026-11-03
- **Người quyết định:** Hải (PO)
- **Thay một phần:** [ADR-0028](0028-trang-thai-la-icon-va-chu.md) (mức độ mật tách khỏi chip khác bằng góc vuông);
  bổ sung cho skill `minimize-text` (đưa chi tiết vào tooltip)

## Bối cảnh

**Bo góc.** Đầu trang chi tiết cuộc họp có ba chip đứng cạnh nhau: "Đang diễn ra" (`.status-live`), "Thường"
(`.level`) và "Đã xác nhận tham dự" (`.badge`). ADR-0028 cố ý cho mức độ mật góc vuông (4px) để khác chip trạng thái
(8px), nhưng nhìn một hàng ba chip ba kiểu góc thì thấy lệch, PO thấy khó chịu. Mức độ mật vốn đã có icon riêng và
viền riêng, nên góc không cần gánh việc phân biệt.

**Tooltip.** Skill `minimize-text` đưa thông tin phụ vào `data-tooltip`. Có chỗ tooltip nằm trên một dòng chữ bình
thường và chứa cả một câu giải thích, ví dụ dòng "Cuộc họp được ghi âm để lập biên bản · tự xoá sau 180 ngày" ở phòng
chờ. Không ai trỏ chuột vào một câu để chờ thêm chữ, nên khi tooltip hiện ra thì bất ngờ; trên màn cảm ứng không có
hover thì không bao giờ thấy.

## Quyết định

1. **Một bo góc cho mọi chip:** token `--radius-chip: 4px`, dùng cho `.badge`, `.status-live`, `.level` và
   `.role-chip`. Nút, ô nhập và thẻ giữ bo góc của ADR-0043 (`--radius` 8px, `--radius-lg` 10px).
2. **Tooltip chỉ nằm trên thứ mời người ta trỏ vào:** nút, link, icon, avatar, tên bị cắt "…", mốc giờ, dấu "đã sửa".
   Không đặt tooltip lên chữ thường hay một câu.
3. **Câu giải thích nằm sau icon (i):** component `InfoTip` ở cuối dòng. Trỏ chuột hoặc focus thì hiện chữ như
   tooltip; bấm (chạm trên điện thoại) thì hiện ngay. Vùng chạm 40px trên màn cảm ứng.
4. Đã áp cho: dòng ghi âm ở phòng chờ (cả ô tick của Khách), "Chưa có giọng mẫu", mic phòng "Đang ghi", biểu quyết
   "Kín/Công khai", danh tính Khách ở trang Khách, thanh bộ nhớ của Kho tài liệu, tiêu đề cột quyền ở ma trận vai trò.

## Hệ quả

- Hàng chip đọc như một bộ; mức độ mật vẫn nhận ra bằng icon, màu và viền.
- Người dùng thấy trước chỗ nào có giải thích; điện thoại cũng đọc được.
- Tooltip trên icon, avatar, tên bị cắt, mốc giờ và dấu "đã sửa" giữ nguyên.
- Lời giải thích mới phải dùng `InfoTip`; skill `minimize-text` và `check-responsive` đã ghi luật này.
