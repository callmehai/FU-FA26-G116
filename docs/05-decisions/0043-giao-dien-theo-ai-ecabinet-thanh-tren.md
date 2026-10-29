# ADR-0043: Giao diện theo AI-eCabinet: thanh xanh trên cùng thay sidebar dọc

- **Trạng thái:** Đề xuất — PO 30/10/2026
- **Ngày:** 2026-10-30
- **Người quyết định:** Hải (PO, FE)
- **Thay:** phần khung (sidebar dọc 256px, thu gọn 72px) và kích thước của
  [ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md); phần hình nhãn trạng thái và mức "Thường" của
  [ADR-0028](0028-trang-thai-la-icon-va-chu.md). Bảng màu của ADR-0012 giữ nguyên.
- **Theo:** AI-eCabinet `origin/main` 0ed9d434: `clients/web/src/components/shell/` (`TopBar.tsx`,
  `MobileNavMenu.tsx`, `UserMenu.tsx`, `AdminLayout.tsx`), `components/StatusChip.tsx`, `LevelChip.tsx`,
  `components/ui/button.tsx`, `features/meetings/detail/MeetingProgress.tsx`, `index.css`

## Bối cảnh

PO muốn giao diện TrustCabinet giống AI-eCabinet ít nhất 70%, nhiều nhất 90%, vì giao diện eCab đã được làm
kỹ. Khi so 14 màn của hai bên (30/10/2026), màu đã gần như trùng (cùng xanh `#2a4d94`, cùng vàng, cùng nền
xám), nhưng khung và thành phần khác hẳn:

- eCab có **một thanh xanh 60px ở trên cùng**, viền vàng 3px, các mục là chữ. TrustCabinet có sidebar dọc
  256px; trên điện thoại sidebar thu còn dải icon 72px, chiếm gần 1/5 bề ngang.
- eCab vẽ trạng thái cuộc họp là **chip bo tròn có chấm**, "Đang họp" nền đặc có chấm nháy. TrustCabinet
  (ADR-0028) vẽ trạng thái là icon và chữ, không khung.
- Nút eCab cao 34px, ô nhập 38px, bo góc 8–10px; TrustCabinet 40px, bo 6px.
- Màn chi tiết eCab có dải "Tiến trình cuộc họp" thay khung lịch sử; hộp thoại tạo cuộc họp chia 2 cột.

## Quyết định

Chúng tôi đưa giao diện của eCab sang, viết lại bằng CSS và component của TrustCabinet (không chép mã
Tailwind/shadcn):

- **Khung**: một thanh xanh `--chrome` 60px trên cùng, viền vàng 3px. Trái: logo về Lịch. Giữa: *Lịch làm
  việc · Cuộc họp · Kho tài liệu │ Quản trị*. Phải: chuông và avatar kèm tên. Menu avatar có *Giọng nói của
  tôi* và *Đăng xuất*. Dưới 860px các mục gom vào nút ☰.
- **Khu Quản trị** có cột menu trắng 236px bên trái, trên cùng là "‹ Về Lịch làm việc". Dưới 860px cột đó
  thành một hàng tab. Người không có quyền mở link quản trị vẫn thấy trang 404 trơn như link sai, không có
  cột menu.
- **Trang**: rộng tối đa 1400px, lề 28px trên, 24px hai bên (16px trên điện thoại). Tiêu đề trang dùng chữ
  Noto Serif 24px, như eCab.
- **Kích thước**: nút 34px (nút ở đầu trang 38px), ô nhập 38px, cả hai lên 40px trên màn cảm ứng; bo góc
  8px cho control, 10px cho thẻ và menu.
- **Trạng thái cuộc họp** là chip bo tròn có chấm màu. Nháp và Chờ duyệt viền đứt; "Đang diễn ra" nền xanh
  đặc, chấm nháy. **Mức độ** luôn có chip bo góc 4px kèm icon, kể cả "Thường".
- **Việc người dùng phải làm ngay là nút vàng**: Tham dự, Gửi duyệt, Duyệt, Công bố, Bắt đầu họp (có gợn
  sóng). "Vào phòng họp" là nút vàng có gợn sóng và mũi tên nhích, trên danh sách, lịch và trang chi tiết.
  Chuyển động này có chủ ý: PO xác định đây là lời gọi hành động, không phải trang trí. Nó dừng khi máy
  bật `prefers-reduced-motion`, và được khai trong `.slop.json`.
- **Danh sách**: thanh lọc nằm trên nền trang, bảng và phân trang trong một thẻ trắng. Dòng cuộc họp có
  hình thức và địa điểm dưới tên; bỏ cột STT và cột Hình thức.
- **Chi tiết cuộc họp**: dải "Tiến trình cuộc họp" (Soạn thảo → Trình duyệt → Phê duyệt → Công bố → Họp →
  Kết thúc) thay khung lịch sử. Rê chuột lên từng bước sẽ thấy việc gì, lúc nào, ai làm. Ba bước duyệt chỉ
  hiện với người tổ chức và người duyệt. Thông tin chung xếp 3 cột.
- **Hộp thoại tạo/sửa cuộc họp** chia 2 cột. Trái: tên, mô tả gập, thời gian kèm nút nhanh 30 phút / 1 giờ
  / 2 giờ, người dự họp. Phải: khung xám cho hình thức, địa điểm, link và mức độ mật.

Những gì của eCab **không** đưa sang:

- Trợ lý AI, Tìm giờ họp, Quản lý chung, sơ đồ chỗ ngồi, "Công việc hôm nay": TrustCabinet không có
  các chức năng này.
- Cột "Chức danh · Đơn vị": không có đơn vị ([ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md)).
- Ô đếm kiểu KPI (icon trong ô màu, số tô màu), vạch màu ở mép trái thẻ, bóng đổ dưới thẻ, dòng phụ nhắc lại
  tiêu đề: sloplint coi đây là dấu hiệu giao diện do AI làm. Bỏ chúng thì vẫn nằm trong mức giống 70–90%.
- Danh sách cuộn liên tục: API của TrustCabinet phân trang, nên vẫn giữ thanh phân trang.

## Hệ quả

- Trên điện thoại, nội dung rộng đủ 390px thay vì 318px.
- `.slop.json` khai báo các ngoại lệ mới: viền vàng của thanh trên (S3), chuyển động của nút gọi hành động
  (M1, L9). Ngoại lệ gradient sidebar bỏ đi.
- Chip "Thường" lặp trên mọi dòng danh sách (sloplint L10) là chấp nhận được, vì eCab cũng làm vậy.
- `AGENTS.md`, skill `check-responsive` và bản so sánh với AI-eCabinet được sửa cùng thay đổi này.
