# ADR-0043: Lấy một số thành phần của AI-eCabinet, giữ sidebar dọc và nhãn trạng thái cũ

- **Trạng thái:** Đề xuất — PO 30/10/2026
- **Ngày:** 2026-10-30
- **Người quyết định:** Hải (PO, FE), Lộc góp ý
- **Thay:** phần kích thước nút, ô nhập và bo góc của [ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md);
  hai điểm của [ADR-0028](0028-trang-thai-la-icon-va-chu.md): chữ trạng thái màu mực, và bỏ "Thường" ở
  đầu trang chi tiết. Khung sidebar dọc, bảng màu của ADR-0012 và hình nhãn của ADR-0028 giữ nguyên.
- **Theo:** AI-eCabinet `origin/main` 0ed9d434: `clients/web/src/components/ui/button.tsx`,
  `features/meetings/MeetingsPage.tsx`, `features/meetings/EditMeetingModal.tsx`, `index.css`

## Bối cảnh

PO muốn lấy những gì AI-eCabinet làm tốt sang TrustCabinet. Bản đầu của quyết định này đưa gần như cả giao
diện eCab sang: thanh xanh trên cùng thay sidebar, chip trạng thái bo tròn có chấm, nút vàng cho việc cần làm
ngay, tiêu đề chữ serif, dải "Tiến trình cuộc họp" thay khung lịch sử. Khi xem bản đó cạnh bản cũ:

- Lộc thấy bố cục cũ dễ dùng hơn, và khung eCab có nhiều dấu hiệu giao diện do AI làm. Đo bằng sloplint, eCab
  có 258 dấu hiệu, TrustCabinet 2.
- PO thấy chip con nhộng, nút vàng, nút bo tròn 4 góc và chữ serif không hợp với app. Nhãn trạng thái cũ, nút
  "Tham dự" xanh nhạt và khung lịch sử trạng thái đẹp hơn.
- PO cần nhìn lướt là biết cuộc họp nào đã huỷ, cuộc nào đang diễn ra. Chữ trạng thái màu mực của ADR-0028 chưa
  đủ nổi.

## Quyết định

Giữ khung, nhãn và màu cũ. Chỉ lấy những thành phần sau của eCab, viết lại bằng CSS và component của
TrustCabinet (không chép mã Tailwind/shadcn):

- **Kích thước**: nút 34px (nút ở đầu trang 38px), ô nhập 38px, cả hai lên 40px trên màn cảm ứng; bo góc
  8px cho control, 10px cho thẻ và menu.
- **Gọi hành động bằng gợn sóng (ping)**: "Vào phòng họp" và "Bắt đầu họp" giữ màu xanh lá của "Đang diễn ra",
  có thêm một vòng gợn sóng cùng màu. Ô lịch của cuộc họp trực tuyến đang diễn ra có một dải "Vào phòng họp"
  liền đáy ô, không phải nút nổi bo tròn. Chuyển động có chủ ý (PO), dừng khi máy bật `prefers-reduced-motion`,
  và được khai trong `.slop.json`.
- **Trạng thái nhìn lướt là biết** (thay một điểm của ADR-0028): icon và chữ cùng màu của trạng thái, chữ đậm,
  vẫn không có khung. Cả dòng trong danh sách đổi theo trạng thái: đang diễn ra có nền xanh nhạt; đã huỷ mờ đi
  và gạch tên; đã kết thúc chữ xám.
- **Danh sách cuộc họp**: thanh lọc nằm trên nền trang, bảng và phân trang trong một thẻ trắng; địa điểm hiện
  dưới tên. Giữ cột STT và Hình thức. Trên điện thoại mỗi cuộc họp là một thẻ, các ô lọc gom sau nút "Bộ lọc"
  có số bộ lọc đang bật.
- **Đầu trang chi tiết** luôn có chip mức độ, kể cả "Thường" (thay một điểm của ADR-0028), nên phần thông tin
  chung bỏ ô "Mức độ". Khung "Lịch sử trạng thái" giữ bên phải.
- **Hộp thoại tạo/sửa cuộc họp** chia 2 cột và cao gần hết màn. Trái: tên, mô tả, thời gian kèm nút nhanh
  30 phút / 1 giờ / 2 giờ, người dự họp. Phải: khung xám cho hình thức, địa điểm, link, và mức độ mật dạng danh
  sách, mỗi mức có một dòng nói nó làm gì.

Những gì của eCab **không** đưa sang:

- Thanh xanh trên cùng, menu ☰, khu Quản trị có cột menu riêng: giữ sidebar theo ADR-0012.
- Chip trạng thái bo tròn có chấm: giữ hình của ADR-0028.
- Nút vàng, nút bo tròn 4 góc: màu vàng chỉ là viền nhấn mảnh như ADR-0012.
- Tiêu đề chữ serif: giữ Be Vietnam Pro ở mọi chỗ, kể cả tên tab.
- Dải "Tiến trình cuộc họp": giữ khung lịch sử trạng thái.
- Trợ lý AI, Tìm giờ họp, Quản lý chung, sơ đồ chỗ ngồi, "Công việc hôm nay": TrustCabinet không có các chức
  năng này.
- Ô đếm kiểu KPI, vạch màu ở mép trái thẻ, bóng đổ dưới thẻ, dòng phụ nhắc lại tiêu đề: sloplint coi đây là dấu
  hiệu giao diện do AI làm.
- Danh sách cuộn liên tục: API của TrustCabinet phân trang.
- Tab Biểu quyết và nội dung tab Biên bản: chờ endpoint (SCRUM-28, SCRUM-32).

## Hệ quả

- `.slop.json` khai báo chuyển động của nút và dải gọi hành động (M1).
- `SensitivityBadge` có thêm `chip` để vẽ "Thường" thành chip ở chỗ chỉ xuất hiện một lần.
- `AGENTS.md` và skill `check-responsive`, `frontend-ui-design` được sửa cùng thay đổi này.
