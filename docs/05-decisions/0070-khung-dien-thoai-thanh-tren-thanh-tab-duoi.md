# ADR-0070: Trên điện thoại, thanh trên và thanh tab dưới thay sidebar

- **Trạng thái:** Đề xuất — chờ PO (Hải) duyệt trên PR của SCRUM-214
- **Ngày:** 2026-11-20
- **Người quyết định:** Hải (PO)
- **Thay thế một phần:** [ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) — bố cục khung ở màn ≤ 640px
- **Liên quan:** ADR-0043 (cỡ điều khiển, 40px trên màn cảm ứng), SCRUM-214

## Bối cảnh

ADR-0012 cho mọi màn đã đăng nhập một sidebar dọc màu xanh; dưới 720px sidebar luôn thu về dải 72px. Trên iPhone
(Hải, 20/11/2026) dải này ăn gần 1/5 chiều ngang 390px, và lịch, các trang cuộc họp “co giãn” khi cuộn:

- Khung `.shell` cao `100vh`. Trên iOS, `100vh` là chiều cao khi thanh công cụ Safari đã ẩn, nên khung dài hơn phần
  thấy được; tài liệu cuộn thêm đúng phần chênh, mỗi lần như vậy Safari ẩn/hiện thanh công cụ và cả trang đổi cỡ dưới
  ngón tay. Cùng lúc `main.content` cũng là một vùng cuộn: hai vùng cuộn lồng nhau tranh nhau một cú vuốt.
- Lưới tuần rộng tối thiểu 760px trong khung ~280px, cuộn được cả hai chiều. Cột giờ và góc trên dính bên trái che
  mất đầu tiêu đề ngày (“ứ Năm 9/11”), vuốt ngang kéo cả lưới, lộ khoảng trắng ở mép.

## Quyết định

1. **Màn ≤ 640px dùng khung điện thoại**: thanh trên màu `--brand` (logo, chuông, avatar mở menu tài khoản) và thanh
   tab dưới nền trắng (Lịch, Cuộc họp, Tìm giờ nếu có quyền, Báo cáo; “Quản trị” là một tab mở menu khi người dùng có
   quyền quản trị). Cùng mục, cùng lọc theo capability như sidebar. Màn rộng hơn giữ nguyên sidebar của ADR-0012.
2. **Khung cao đúng phần thấy được** (`100dvh`), chỉ `main.content` cuộn, không nảy — PR #453 đã đưa phần này vào
   khung chung cho mọi cỡ màn; khung điện thoại khoá thêm tài liệu (`overflow: hidden` trên `html`, `body`) nên nó
   không cuộn, không kéo ngang được. Vì tài liệu không cuộn, thanh công cụ
   Safari không ẩn/hiện, không gì đổi cỡ khi đọc. Chạm nhanh hai lần không phóng to trang (`touch-action:
   manipulation`); vẫn phóng to bằng hai ngón được.
3. **Lịch tuần trên điện thoại hiện một ngày**: thanh công cụ gọn hai dòng (nút + thay chữ “Tạo cuộc họp”) và một dải
   ngày trong tuần (mỗi ngày kèm số cuộc họp) luôn ở trên khi mở lịch; khung một ngày bên dưới lấp phần màn còn lại, chỉ
   cuộn dọc, mở ở giờ hiện tại hoặc cuộc họp đầu, chú thích màu ở cuối khung. Không có gì cuộn ngang. Chạm một ngày
   hoặc vuốt ngang để đổi ngày; vuốt quá ngày cuối tuần sang tuần sau. Nút “Vào phòng họp” nằm trên dòng giờ của ô để
   tên cuộc họp còn đọc được.
4. **Vùng an toàn**: hai thanh tự đệm `env(safe-area-inset-*)`. `index.html` **chưa** bật `viewport-fit=cover`, vì
   phòng họp và trang đăng nhập khi xoay ngang sẽ nằm dưới tai thỏ; bật nó là việc của lượt sau, cùng với đệm cho các
   màn đó.

## Hệ quả

- Trang có trọn chiều ngang 390px; điều hướng nằm trong tầm ngón cái.
- Mỗi thay đổi giao diện vẫn kiểm ở 390px (skill check-responsive); bảng kiểm nay đo thêm: tài liệu không cuộn, kích
  thước phần tử không đổi khi cuộn, vuốt ngang trên lịch không lộ khoảng trắng.
- Đổi qua lại giữa khung điện thoại và khung có sidebar (xoay ngang một điện thoại nhỏ) dựng lại trang đang mở.
- Phòng họp (đã toàn màn hình) và các trang quản trị không đổi trong lượt này.
