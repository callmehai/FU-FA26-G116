# ADR-0043: Lấy thành phần giao diện của AI-eCabinet, giữ sidebar dọc

- **Trạng thái:** Đề xuất — PO 30/10/2026
- **Ngày:** 2026-10-30
- **Người quyết định:** Hải (PO, FE), Lộc góp ý
- **Thay:** phần kích thước nút, ô nhập và bo góc của [ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md);
  phần hình nhãn trạng thái và mức "Thường" của [ADR-0028](0028-trang-thai-la-icon-va-chu.md). Khung
  sidebar dọc và bảng màu của ADR-0012 giữ nguyên.
- **Theo:** AI-eCabinet `origin/main` 0ed9d434: `clients/web/src/components/StatusChip.tsx`, `LevelChip.tsx`,
  `components/ui/button.tsx`, `features/meetings/detail/MeetingProgress.tsx`, `index.css`

## Bối cảnh

PO muốn giao diện TrustCabinet gần với AI-eCabinet hơn, vì các thành phần của eCab đã được làm kỹ. Khi so
14 màn của hai bên (30/10/2026), màu đã gần như trùng (cùng xanh `#2a4d94`, cùng vàng, cùng nền xám),
nhưng thành phần khác hẳn:

- eCab vẽ trạng thái cuộc họp là **chip bo tròn có chấm**, "Đang họp" nền đặc có chấm nháy. TrustCabinet
  (ADR-0028) vẽ trạng thái là icon và chữ, không khung.
- Việc người dùng cần làm ngay (Vào phòng họp, Duyệt, Tham dự) trên eCab là **nút vàng**, nút quan trọng
  nhất có gợn sóng. Trên TrustCabinet chúng là nút xanh như mọi nút khác.
- Nút eCab cao 34px, ô nhập 38px, bo góc 8–10px; TrustCabinet 40px, bo 6px.
- Màn chi tiết eCab có dải "Tiến trình cuộc họp" thay khung lịch sử; hộp thoại tạo cuộc họp chia 2 cột.

Bản đầu của quyết định này đưa cả khung eCab sang (thanh xanh trên cùng thay sidebar). Lộc thấy bố cục cũ
dễ dùng hơn và khung eCab có nhiều dấu hiệu giao diện do AI làm. Đo bằng sloplint, eCab có 258 dấu hiệu,
TrustCabinet 2. PO chốt: **giữ sidebar dọc, chỉ lấy các thành phần hay**.

## Quyết định

Chúng tôi giữ khung của ADR-0012 (sidebar dọc 256px, thu gọn 72px, chuông và tài khoản ở chân sidebar).
Bên trong khung, chúng tôi lấy các thành phần của eCab, viết lại bằng CSS và component của TrustCabinet
(không chép mã Tailwind/shadcn):

- **Kích thước**: nút 34px (nút ở đầu trang 38px), ô nhập 38px, cả hai lên 40px trên màn cảm ứng; bo góc
  8px cho control, 10px cho thẻ và menu. Tiêu đề trang dùng chữ Noto Serif 24px, như eCab.
- **Trạng thái cuộc họp** là chip bo tròn có chấm màu. Nháp và Chờ duyệt viền đứt; "Đang diễn ra" nền xanh
  đặc, chấm nháy. **Mức độ** luôn có chip bo góc 4px kèm icon, kể cả "Thường".
- **Việc người dùng phải làm ngay là nút vàng**: Tham dự, Gửi duyệt, Duyệt, Công bố, Bắt đầu họp (có gợn
  sóng). "Vào phòng họp" là nút vàng có gợn sóng và mũi tên nhích, trên danh sách, lịch và trang chi tiết.
  Chuyển động này có chủ ý: PO xác định đây là lời gọi hành động, không phải trang trí. Nó dừng khi máy
  bật `prefers-reduced-motion`, và được khai trong `.slop.json`.
- **Danh sách**: thanh lọc nằm trên nền trang, bảng và phân trang trong một thẻ trắng. Dòng cuộc họp có
  hình thức và địa điểm dưới tên; bỏ cột STT và cột Hình thức. Trên điện thoại mỗi cuộc họp là một thẻ, các
  ô lọc gom sau nút "Bộ lọc" có số bộ lọc đang bật.
- **Chi tiết cuộc họp**: dải "Tiến trình cuộc họp" (Soạn thảo → Trình duyệt → Phê duyệt → Công bố → Họp →
  Kết thúc) thay khung lịch sử. Rê chuột lên từng bước sẽ thấy việc gì, lúc nào, ai làm. Ba bước duyệt chỉ
  hiện với người tổ chức và người duyệt. Thông tin chung xếp 3 cột.
- **Hộp thoại tạo/sửa cuộc họp** chia 2 cột. Trái: tên, mô tả gập, thời gian kèm nút nhanh 30 phút / 1 giờ
  / 2 giờ, người dự họp. Phải: khung xám cho hình thức, địa điểm, link và mức độ mật.

Những gì của eCab **không** đưa sang:

- Thanh xanh trên cùng, menu ☰, khu Quản trị có cột menu riêng: giữ sidebar theo ADR-0012.
- Trợ lý AI, Tìm giờ họp, Quản lý chung, sơ đồ chỗ ngồi, "Công việc hôm nay": TrustCabinet không có
  các chức năng này.
- Cột "Chức danh · Đơn vị": không có đơn vị ([ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md)).
- Ô đếm kiểu KPI (icon trong ô màu, số tô màu), vạch màu ở mép trái thẻ, bóng đổ dưới thẻ, dòng phụ nhắc lại
  tiêu đề: sloplint coi đây là dấu hiệu giao diện do AI làm.
- Danh sách cuộn liên tục: API của TrustCabinet phân trang, nên vẫn giữ thanh phân trang.
- Khung biểu quyết trong cuộc họp: chờ endpoint của SCRUM-28, sẽ lấy theo eCab khi có.

## Hệ quả

- `.slop.json` khai báo các ngoại lệ mới: chuyển động của nút gọi hành động và chấm "Đang diễn ra" (M1), chấm
  trên chip trạng thái (L9), chip "Thường" lặp trên mọi dòng (L10, eCab cũng làm vậy).
- `AGENTS.md` và skill `check-responsive`, `frontend-ui-design` được sửa cùng thay đổi này.
