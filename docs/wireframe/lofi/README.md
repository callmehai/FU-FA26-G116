# Wireframe phác thảo (lo-fi)

Bản đen trắng, vẽ tay kiểu người thiết kế đưa cho người code: 1 sơ đồ luồng và 20 màn chính. Mỗi tờ
gồm màn hình bên trái và ghi chú bên phải. Nội dung rút gọn từ
[wireframe v11](../TrustCabinet-wireframe-v11.html) ngày 02/11/2026, nên v11 và web thật vẫn là nơi
tra chi tiết: đủ 96 màn, câu chữ và trạng thái.

| Thành phần | Đường dẫn | Vai trò |
|---|---|---|
| Nguồn | [screens.js](screens.js) | Khai báo từng tờ. Mọi thay đổi bắt đầu ở đây. |
| HTML | [TrustCabinet-wireframe-lofi.html](TrustCabinet-wireframe-lofi.html) | Mở bằng trình duyệt để xem cả 21 tờ. |
| Ảnh | [screens/](screens/) | PNG từng tờ, sinh từ HTML. |
| Script chụp ảnh | [shots.mjs](shots.mjs) | Chạy `node docs/wireframe/lofi/shots.mjs` từ thư mục gốc repo. |
| Script dựng Figma | [figma-render.js](figma-render.js) | Dán sau `screens.js` vào tool `use_figma` của Figma MCP. |
| Figma | [TrustCabinet Wireframe Lo-fi](https://www.figma.com/design/IMt6sXprK6tALSHvUUKPiN) | Trang "Wireframe phác thảo", mỗi tờ một frame. |

## Quy ước vẽ

| Ký hiệu | Nghĩa |
|---|---|
| Ô vuông nhỏ | Icon. Tên chức năng ghi ở ghi chú. |
| Ô tròn | Ảnh đại diện. |
| Hình chữ nhật gạch chéo | Vùng nội dung: trang tài liệu, video. |
| Các đường kẻ ngang | Đoạn chữ không cần viết ra. |
| Nền đen | Nút chính hoặc mục đang chọn. |
| Viền đứt | Trạng thái nháp, hoặc nút chưa dùng được. |
| Số trong chấm đen | Trỏ tới ghi chú cùng số. Ghi chú có dấu gạch ngang áp dụng cho cả màn. |

Chỉ dùng đen và trắng, font Mali (Google Fonts, có tiếng Việt). Màu và kích thước thật do code quyết
định theo [ADR-0012](../../05-decisions/0012-giao-dien-hanh-chinh-sidebar-doc.md).

## Danh sách tờ

| Tờ | Nội dung | Màn tương ứng trong v11 |
|---|---|---|
| 00 | Luồng màn hình | — |
| 01 | Đăng nhập | 1.1 |
| 02 | Lịch làm việc | 4.1 |
| 03 | Danh sách cuộc họp | 4.3 |
| 04 | Tạo cuộc họp | 4.4 |
| 05 | Chi tiết – Thông tin chung | 4.5 |
| 06 | Thành phần tham dự | 6.1, 6.3 |
| 07 | Nội dung cuộc họp | 5.1 |
| 08 | Tài liệu cuộc họp | 7.3 |
| 09 | Xem tài liệu & góp ý | 7.5 |
| 10 | Biểu quyết | 11.1 |
| 11 | Bản ghi | 9.1 |
| 12 | Phòng họp trực tuyến | 8.2 |
| 13 | Kho tài liệu | 7.2 |
| 14 | Người dùng & Vai trò | 2.1, 3.1 |
| 15 | Biên bản & ký (web chưa có) | 12.1, 12.2, 12.3 |
| 16 | Giọng nói của tôi | 9.4, 9.4b |
| 17 | Ma trận phân quyền | 3.2 |
| 18 | Panel thông báo | 13.1 |
| 19 | Phòng chờ | 8.1 |
| 20 | Bản gỡ băng | 9.1b, 10.1 |

## Khi sửa

1. Sửa `screens.js`, mở HTML để xem lại, rồi chạy `shots.mjs`.
2. Dựng lại frame đã đổi trên Figma bằng `figma-render.js`. Script này tạo frame mới, nên xoá frame
   cũ trước.
3. Giữ ghi chú đúng với v11 và code. Việc v11 chưa chốt thì không vẽ vào đây.
