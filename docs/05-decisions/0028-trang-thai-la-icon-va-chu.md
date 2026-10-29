# ADR-0028: Trạng thái cuộc họp là icon và chữ, mức "Thường" không gắn chip

- **Trạng thái:** Đề xuất — thay thế bởi [ADR-0043](0043-giao-dien-theo-ai-ecabinet-thanh-tren.md)
- **Ngày:** 2026-10-23
- **Người quyết định:** Hải (FE)

## Bối cảnh

[ADR-0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) quy định trạng thái cuộc họp là chip bo tròn
có chấm màu, còn mức độ là chip bo góc 4px có icon. Mục đích là để hai loại nhãn khác nhau cả ở
hình dạng chứ không chỉ ở màu, và mục đích đó vẫn đúng.

Khi rà giao diện bằng sloplint (bộ đo dấu hiệu "giao diện do AI làm"), hai chỗ bị bắt lại:

- **Chip bo tròn có chấm** là một trong những mẫu lặp nhiều nhất ở trang do công cụ AI dựng. Nó
  đứng cạnh chip mức độ vuông góc, nên một dòng có hai hình chip, hai chiều cao khác nhau.
- **Cột "Mức độ" có chip "Thường" trên mọi dòng** (20/20 dòng ở danh sách cuộc họp). Khi dòng nào
  cũng có chip giống hệt nhau thì không còn gì nổi lên. Đây là cùng một lỗi đã sửa ở màn Người
  dùng (chip "Hoạt động").

Các design system lớn làm khác: Cloudscape (AWS) và Carbon (IBM) hiển thị trạng thái bằng *status
indicator*, tức một icon có màu đặt cạnh chữ, không có khung. Cloudscape khuyên không đặt icon
trong badge.

## Quyết định

Chúng tôi thay phần "hình của từng loại nhãn" trong ADR-0012:

- **Trạng thái cuộc họp** là icon có màu đặt cạnh chữ màu mực, không có khung hay nền. Mỗi trạng
  thái có một icon riêng: Nháp (bút), Chờ duyệt (đồng hồ), Bị từ chối (dấu X tròn), Đã duyệt (dấu
  tích tròn), Đã công bố (máy bay giấy), Đã kết thúc (hai dấu tích). Như vậy trạng thái khác nhau
  cả ở hình chứ không chỉ ở màu.
- **"Đang diễn ra" vẫn là nhãn nền đặc duy nhất**, bo 6px như mọi control, chỉ có chữ. Nền đặc đã
  là hình riêng của nó; thêm icon vào nhãn có nền là thêm một lớp trang trí (Cloudscape: không đặt
  icon trong badge). Cuộc họp đang diễn ra vẫn là thứ dễ thấy nhất trên mọi màn.
- **Mức độ** giữ chip bo góc 4px có icon cho Nội bộ, Hạn chế và Mật. **"Thường" là chữ thường**
  trong bảng và trong ô "Mức độ". Trong hàng chip (đầu trang chi tiết, thẻ lịch, khung xem nhanh)
  thì bỏ hẳn "Thường", giống phòng họp vẫn làm: không có nhãn nghĩa là mức thường.
- **Dòng lịch sử trạng thái** chỉ hiện chữ. Chấm trên đường thời gian đã mang màu của trạng thái.

Trạng thái và mức độ vẫn khác nhau về hình: một bên là icon và chữ không khung, một bên là chip
vuông. Quy tắc trong AGENTS.md ("differ by shape as well as colour") vẫn giữ nguyên.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ chip bo tròn có chấm (ADR-0012) | Không phải sửa | Mẫu điển hình của giao diện AI; hai hình chip trên một dòng | Bộ đo và người dùng đều đánh giá là "AI slop" |
| Chip vuông 4px cho cả trạng thái | Một hình chip duy nhất | Trạng thái và mức độ lại chỉ khác nhau ở màu, trái mục đích của ADR-0012 | Mất phân biệt bằng hình |
| Chỉ chữ có màu, không icon | Gọn nhất | Người mù màu không phân biệt được trạng thái | Cần hình, không chỉ màu |
| **Icon có màu cạnh chữ, "Đang diễn ra" nền đặc** | Theo Cloudscape/Carbon; vẫn phân biệt bằng hình; bảng nhẹ hơn | Trạng thái kém nổi hơn chip | **Chọn** |

## Hệ quả

- `StatusBadge` thêm icon theo trạng thái. Các class `.status-*` chỉ đổi màu icon. `--dot-*` vẫn
  dùng cho chấm trên đường thời gian và trên lịch.
- `SensitivityBadge` trả chữ thường cho "Thường". Ba hàng chip tự bỏ mức "Thường".
- Mục "Sanctioned" trong skill `anti-ai-slop` bỏ dòng chip bo tròn có chấm.
- Màn mới hiển thị trạng thái thì dùng `StatusBadge`, không tự dựng chip.
