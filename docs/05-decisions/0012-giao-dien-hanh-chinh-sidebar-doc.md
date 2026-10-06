# ADR-0012: Giao diện phong cách hành chính, một sidebar dọc

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-06
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

[ADR-0002](0002-khung-giao-dien-theo-ai-ecabinet.md) dựng khung bám theo AI-eCabinet: hai không gian
làm việc (`/mgmt`, `/calendar`) chuyển qua lại bằng bộ chuyển trên app bar 60px, mỗi không gian một
sidebar trắng, màu chủ đạo đỏ sơn mài `#a5281b`.

Dùng thực tế thì khung đó có ba chỗ vướng:

- **Hai không gian là một tầng điều hướng thừa.** Người dùng phải biết "Lịch" nằm ở không gian khác
  với "Cuộc họp" rồi mới bấm tới được, trong khi cả hai đều là việc hằng ngày của cùng một người.
- **Đỏ vừa là màu thương hiệu vừa là màu cảnh báo.** Nút chính, chip "Mật", nút xoá và lỗi cùng một
  sắc đỏ, nên mắt không phân biệt được "hành động chính" với "nguy hiểm".
- **Trạng thái cuộc họp chỉ khác nhau ở màu chip.** Người mù màu hoặc màn chiếu kém không phân biệt
  được "Chờ duyệt" với "Đã công bố".

Nhóm đã có một bộ thiết kế mới ("Phong cách hành chính") gồm năm màn chính, năm màn đang tải và một
bảng quy chuẩn màu, chữ, thành phần.

## Quyết định

Chúng tôi sẽ dựng lại frontend theo bộ thiết kế đó:

- **Một shell, một sidebar dọc**, không còn app bar và bộ chuyển không gian. Sidebar xanh (gradient
  `#3f63b3 → #304f97`), rộng 256px, thu gọn còn 72px (nhớ lựa chọn trong trình duyệt; dưới 720px luôn
  thu gọn). Trên cùng là logo; giữa là hai nhóm **Nghiệp vụ** (Lịch làm việc, Cuộc họp, Kho tài liệu)
  và **Hệ thống** (Quản trị › Người dùng & Quyền, Vai trò); dưới cùng là tài khoản và chuông thông
  báo. Panel thông báo mở sang phải sidebar, neo ở đáy.
- **URL giữ nguyên** (`/mgmt/...`, `/calendar`) để link cũ và link trong thông báo/email vẫn mở được.
- **Màu chủ đạo xanh `#2a4d94`** cho nút chính, mục đang chọn, liên kết. **Vàng kim `#c99a2e` chỉ làm
  viền nhấn** (vòng logo, dải trên màn đăng nhập). **Đỏ `#b4232a` chỉ cho "Mật", giờ hiện tại và cảnh
  báo/xoá.** Nền trang `#f3f5f8`, chữ `#1a2433`, đường kẻ `#d9dee6`.
- **Mỗi loại nhãn có hình riêng, không chỉ màu riêng**:
  - trạng thái cuộc họp: bo tròn, có chấm; "Đang diễn ra" là chip nền đặc duy nhất;
  - mức độ (Thường / Nội bộ / Hạn chế / Mật): bo góc 4px, mỗi mức một icon (địa cầu, toà nhà, khiên,
    ổ khoá).
- **Kích thước**: control cao 40px (nút chính ở header trang 44px), bo góc 6px, chữ nội dung 14px,
  không dùng chữ dưới 12px. Bảng danh sách nằm trong một khung trắng gồm thanh lọc, bảng và phân
  trang; tiêu đề cột viết hoa 12px.
- Vẫn **chỉ có giao diện sáng**, vẫn dùng Be Vietnam Pro.
- **Icon lấy từ [Lucide](https://lucide.dev)** (`lucide-react`, giấy phép ISC) — cùng bộ với AI-eCabinet,
  nên nét và góc bo khớp bản tham chiếu. Màn hình vẫn chỉ gọi `components/Icon.tsx` theo *ý nghĩa*
  (`<Icon name="key" />`), không import Lucide trực tiếp, để đổi một icon chỉ sửa một chỗ.
- **Lịch làm việc có hai chế độ trên cùng dữ liệu**: lưới tuần (`?mode=calendar`, mặc định) và danh
  sách theo ngày (`?mode=list`) — mỗi ngày một mục, mỗi phiên một thẻ có giờ, địa điểm, trạng thái,
  mức độ. Ngày không có phiên ghi rõ "Không có cuộc họp".

Màn nào trong thiết kế cần dữ liệu backend chưa có thì **dựng khung và nói rõ đang chờ endpoint
nào**, không bịa dữ liệu mẫu:

| Màn / khối | Chờ |
|---|---|
| Lịch tuần | `GET /api/meetings/{id}/sessions` (SCRUM-68, PR #72). Khi có, lịch tự lên dữ liệu |
| Kho tài liệu | `GET /api/documents` ([ADR-0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md)) |
| Biểu quyết trên tab "Thông tin chung", "Công việc hôm nay" | Chưa có API biểu quyết, phiên họp |

Cột "Chức danh · Đơn vị" ở màn Người dùng của bản thiết kế **không** làm: hệ thống không có đơn vị
([ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md)) và tài khoản không có trường chức danh.

Lịch không có endpoint "phiên theo khoảng ngày", nên tạm ghép ba endpoint sẵn có:
`GET /api/meetings?from&to` (cuộc họp có phiên trong tuần, đã lọc quyền xem ở server), rồi với từng
cuộc họp gọi `GET /api/meetings/{id}/sessions` và `GET /api/meetings/{id}`. Tức 2N + 1 request mỗi
tuần — chấp nhận được cho tuần làm việc, và là lý do nên đề nghị BE thêm một endpoint đọc phiên theo
khoảng ngày. Khi endpoint phiên trả 404 cho mọi cuộc họp vừa liệt kê, lịch báo "chưa có API" thay vì
hiện tuần trống.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ khung ADR-0002, chỉ đổi màu | Ít sửa | Vẫn còn tầng "không gian làm việc" thừa | Không giải quyết vấn đề điều hướng |
| Sidebar dọc nhưng giữ app bar cho chuông và tài khoản | Quen với bản cũ | Mất 60px chiều cao cho hai nút | Chân sidebar đủ chỗ cho cả hai |
| Đổi URL thành `/meetings`, `/documents`… | URL gọn | Vỡ link trong email mời và thông báo đã gửi | Không đáng đánh đổi |

## Hệ quả

- `CalendarSidebar` và prop `sidebar`/`wide` của `AppShell` bị bỏ; `AppSidebar` nhận `collapsed`.
- Token màu đổi tên theo nghĩa mới: `--lacquer` → `--brand`, `--seal-gold` → `--gold`. Code mới dùng
  tên mới; AGENTS.md cập nhật theo.
- Vùng cuộn của nội dung là `main.content` (trước là `.shell-main`).
- Khi có API phiên họp, biểu quyết, tài liệu, các màn chờ ở bảng trên được nối vào khung đã dựng, không
  phải dựng lại bố cục.
- ADR-0002 chuyển sang *Thay thế một phần bởi ADR-0012*: phần route tập trung, chia code theo feature,
  vòng đời chín trạng thái và route thiếu quyền trả 404 vẫn giữ nguyên.
