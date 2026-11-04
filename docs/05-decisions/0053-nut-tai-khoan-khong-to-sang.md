# ADR-0053: Nút tài khoản ở chân sidebar không tô sáng khi đang ở trang cá nhân

- **Trạng thái:** Đề xuất — PO (Hải) 04/11/2026
- **Ngày:** 2026-11-04
- **Người quyết định:** Hải (PO)
- **Thay một phần:** [ADR-0046](0046-trang-ca-nhan-vao-menu-tai-khoan.md) mục 2 (câu "Đang ở một trang cá nhân thì nút
  tài khoản sáng lên thay cho mục sidebar")

## Bối cảnh

ADR-0046 chuyển **Kho tài liệu** và **Giọng nói của tôi** vào menu tài khoản, và cho nút tài khoản sáng lên (nền
`--sidebar-active` cùng viền mờ) khi đang ở một trong hai trang đó, để sidebar vẫn có chỗ báo "đang ở đâu". Khi dùng
thật (Lộc báo lỗi ngày 04/11), cái nền đó trông như phần hồ sơ bị **đóng khung sẫm màu**, không ai đọc ra là "đang ở
trang này". Tiêu đề trang đã nói người dùng đang ở đâu.

## Quyết định

Nút tài khoản không còn trạng thái "đang chọn": chỉ đổi nền khi trỏ chuột (hover) và có viền focus khi dùng bàn phím,
ở mọi trang. Hai trang cá nhân vẫn nằm trong menu tài khoản, URL giữ nguyên.

## Hệ quả

- Chân sidebar trông giống nhau ở mọi trang, không còn khung sẫm.
- Ở Kho tài liệu và Giọng nói của tôi, sidebar không có mục nào sáng; người dùng biết mình ở đâu nhờ tiêu đề trang.
