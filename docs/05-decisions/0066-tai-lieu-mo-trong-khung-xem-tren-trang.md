# ADR-0066: Tài liệu mở trong khung xem phủ trên trang, không mở tab mới

- **Trạng thái:** Đề xuất — PO (Hải) 19/11/2026, sau buổi họp thử thật
- **Ngày:** 2026-11-20
- **Người quyết định:** Hải (PO)
- **Thay một phần:** [ADR-0011](0011-tinh-nang-hop-la-panel-dung-chung.md) mục 6 trong "Tiêu chí" ("Link mở tab mới" cho
  tài liệu). Ghi chú dài từ phòng họp vẫn mở tab mới như ADR-0011.

## Bối cảnh

ADR-0011 cho tài liệu mở từ phòng họp sang **tab mới**, để cuộc gọi ở tab cũ không bị ngắt (BR-315). Ở trang chi tiết
cuộc họp và kho tài liệu cá nhân, bấm tài liệu thì **chuyển hẳn** sang màn xem toàn màn hình rồi bấm "‹ Tài liệu" để về.

Buổi họp thử ngày 19/11/2026: mỗi lần đọc tài liệu trong phòng lại mọc thêm một tab, người dùng lạc giữa các tab và
quay về nhầm. Ở trang chi tiết, quay lại thì mục nghị sự đang mở, vị trí cuộn đều mất. Hải: "mở panel để đọc tài liệu
thôi ko mở tab mới".

## Quyết định

1. **Bấm tên tài liệu thì mở khung xem phủ trên trang đang đứng**: một `<dialog>` modal cách mép cửa sổ 24px (điện thoại:
   toàn màn hình), bên trong là đúng màn xem cũ (PDF/DOCX, phóng to, Góp ý, Tải về hoặc "Chỉ xem trực tuyến", xác nhận đã
   đọc của biểu quyết). Áp cho mọi chỗ mở tài liệu cuộc họp — tab Tài liệu, Nội dung, Biểu quyết, ba panel tương ứng trong
   phòng họp, trang của Khách — và tài liệu trong kho cá nhân (cả kho của tài khoản đã khoá mà quản trị viên đọc).
2. **Trang bên dưới giữ nguyên**: phòng họp vẫn trong cuộc gọi, danh sách giữ thư mục, bộ lọc, mục đang mở, vị trí cuộn.
3. **Đóng bằng nút ✕ ở góc phải trên, phím Esc, hoặc nút Back** của trình duyệt/điện thoại. Tài liệu đang mở nằm trong
   `state` của mục lịch sử, nên Back đóng khung xem chứ không rời trang (trong phòng họp: không rời cuộc gọi).
4. **Route toàn màn hình giữ nguyên** (`/mgmt/meetings/{id}/documents/{docId}`, `/mgmt/documents/{id}`,
   `/guest/{inv}/documents/{docId}`): link vẫn có `href` tới đó, nên Ctrl/⌘-click hay "Mở trong tab mới" vẫn mở tab riêng
   khi người dùng tự muốn; link cũ đã gửi đi vẫn mở được.
5. Đóng khung xem phát sự kiện để panel Biểu quyết đọc lại, thay cho sự kiện `focus` của cửa sổ khi trình xem còn là tab
   riêng: đọc xong tài liệu thì "Gửi phiếu" mở khoá ngay.

## Hệ quả

- BR-315 ("mở tài liệu từ phòng họp mà vẫn ở trong phòng") vẫn đúng, chỉ khác cách làm. UC-32a, UC-32c cần ghi "mở trong
  khung xem trên trang" thay cho "mở ở tab mới" (BA Final Review).
- Khung xem che video trong phòng họp khi đang đọc; tiếng vẫn chạy. Muốn vừa xem video vừa đọc thì Ctrl/⌘-click để mở tab
  riêng như trước.
- Thông báo nổi (toast) là popover nằm trên khung xem, để "Đã xác nhận đọc tài liệu" hay lỗi không bị lớp phủ che.
- Không đổi API, không đổi quyền: server vẫn kiểm tra mỗi lần đọc nội dung tệp như cũ.
