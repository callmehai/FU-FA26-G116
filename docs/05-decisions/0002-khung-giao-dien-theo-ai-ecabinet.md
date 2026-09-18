# ADR-0002: Dựng khung giao diện bám theo AI-eCabinet

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-18
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

[ADR-0001](0001-tach-frontend-backend.md) đã tách `frontend/` và dựng một trang giới thiệu đồ án.
Trang đó không dùng được để phát triển tiếp: nó giới thiệu *về* hệ thống chứ không phải *là* hệ
thống, nên mọi màn hình nghiệp vụ vẫn còn là con số không.

Nhóm có quyền truy cập bản demo nội bộ và mã nguồn tham chiếu của AI-eCabinet, nên thay vì tự
nghĩ ra bố cục, có thể khảo sát hệ thống thật rồi dựng lại khung tương đương.

## Quyết định

Chúng tôi sẽ dựng lại toàn bộ frontend theo khung của AI-eCabinet:

- **Hai không gian làm việc dùng chung một khung** (`AppShell`): quản lý dưới `/mgmt`, lịch dưới
  `/calendar`, chuyển qua lại bằng bộ chuyển trên thanh header. Chỉ sidebar khác nhau.
- **Bản đồ route tập trung** trong `lib/routes.ts` — mọi đường dẫn dựng từ đây.
- **Chia code theo feature** (`features/meetings/`, `features/auth/`, …) chứ không theo loại file.
- **Vòng đời cuộc họp** giữ nguyên chín trạng thái của bản gốc — xem
  [tài liệu vòng đời](../02-design/01-vong-doi-cuoc-hop.md).
- **Bảng màu**: đỏ sơn mài `#a5281b` + vàng triện `#c1a15a` trên nền giấy `#f6f7f9`, font
  Be Vietnam Pro. Đây là ngữ vựng thị giác quen thuộc của hệ thống khu vực công Việt Nam.
- **Route thiếu quyền trả về 404**, không phải 403, để không lộ sự tồn tại của màn hình.

Trang giới thiệu cũ bị xoá; vào ứng dụng là ra thẳng màn đăng nhập.

## Ranh giới đã đặt ra

Repo AI-eCabinet là **private, không có giấy phép mở**. Nhóm dùng nó để hiểu nghiệp vụ, bố cục và
luồng màn hình, sau đó **tự viết** phần hiện thực của TrustCabinet. Không sao chép tệp mã nguồn.

Hai lý do, lý do nào cũng đủ: bản quyền thuộc về Trustsoft, và đồ án này được chấm điểm nên nộp
code người khác là vấn đề liêm chính học thuật. Backend của họ viết bằng Go nên vốn đã không dùng
lại được; rủi ro nằm ở `clients/web`, và đó chính là chỗ nhóm phải tự viết.

## Phương án đã cân nhắc

| Phương án | Vì sao không chọn |
|---|---|
| Giữ trang giới thiệu, dựng app ở `/app` | Phải nuôi hai giao diện; hệ thống nội bộ thật không có landing |
| Tự nghĩ bố cục riêng | Mất thời gian vào việc đã có lời giải tốt, trong khi giá trị của đồ án nằm ở phần gọi thoại/video |
| Sao chép `clients/web` | Vi phạm bản quyền và liêm chính học thuật |

## Hệ quả

- Người dùng chưa đăng nhập vào `/` sẽ bị chuyển tới `/login`.
- Xác thực và RBAC đang **giả lập phía trình duyệt** (`features/auth/AuthProvider.tsx`,
  `types/user.ts`). Đây là nợ kỹ thuật có chủ đích, phải trả khi backend có bảng người dùng.
- Danh sách cuộc họp đang đọc dữ liệu mẫu trong `features/meetings/sampleData.ts`. Khi có
  `GET /api/meetings`, **lọc phải chuyển về phía server** — bản ghi người dùng không được xem thì
  không được phép gửi xuống trình duyệt ngay từ đầu.
- Dropdown tự dựng (`components/Select.tsx`) thay cho `<select>` gốc, vì popup của `<select>` do
  hệ điều hành vẽ và không nhận CSS của trang.
