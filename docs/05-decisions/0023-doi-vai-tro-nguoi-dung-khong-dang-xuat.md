# ADR-0023: Đổi vai trò của một người dùng cũng không đăng xuất — báo và áp dụng quyền mới ngay

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-22
- **Người quyết định:** PO (22/10/2026), nhóm SEP490-G116
- **Liên quan:** `SCRUM-105`; mở rộng [ADR-0022](0022-sua-quyen-vai-tro-khong-dang-xuat.md), thay thế
  dòng *"Không đổi: đổi vai trò … của một người … vẫn huỷ phiên"* của ADR đó

## Bối cảnh

ADR-0022 bỏ việc đăng xuất khi sửa quyền của **một vai trò**, nhưng vẫn giữ việc huỷ phiên khi đổi
vai trò của **một người dùng** (`PUT /api/users/{id}`). PO yêu cầu ngày 22/10/2026: không tài khoản nào
phải đăng xuất vì được thêm, bớt quyền hay đổi vai trò. Người dùng chỉ thấy thông báo, và các nút họ
không còn quyền dùng biến mất ngay mà không cần tải lại trang.

Lập luận của ADR-0022 áp dụng y hệt cho trường hợp này. `CapabilityGate` đọc lại quyền từ database ở
mỗi request, nên quyền bị mất đã bị từ chối ngay; huỷ refresh token không thêm lớp bảo vệ nào.

## Quyết định

Chúng tôi sẽ không huỷ phiên khi đổi vai trò của một tài khoản. Sau khi lưu, server gửi
`capabilitiesChanged` tới đúng tài khoản đó, cùng sự kiện ADR-0022 đã dùng. Trình duyệt nhận được thì
hiện thông báo `MSG287` và refresh token, nên menu, nút và route đổi theo ngay.

Đổi vai trò cũng không còn thu hồi liên kết đặt lại mật khẩu đang chờ. Việc thu hồi này trước đây chỉ
đi kèm tín hiệu huỷ phiên, không cần cho an toàn.

Vẫn huỷ phiên như cũ: **đổi email** (access token mang claim email, liên kết đặt lại mật khẩu gắn
với địa chỉ), **vô hiệu hoá tài khoản** và **đặt lại mật khẩu**.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ huỷ phiên khi đổi vai trò (như ADR-0022) | Không đổi code | Người bị đổi vai trò bị đăng xuất | Trái yêu cầu PO; không thêm bảo vệ nào |
| Không huỷ phiên, không push | Diff nhỏ nhất | Quyền mới chỉ hiện khi đổi tab, gặp `403` hoặc token sắp hết hạn | Trái yêu cầu "ẩn ngay, không tải lại trang" |

## Hệ quả

- Không còn thao tác phân quyền nào (sửa ma trận, đổi vai trò) đăng xuất người dùng.
- Kết luận an toàn vẫn dựa vào điều kiện của ADR-0022: mọi endpoint gắn quyền phải đi qua
  `RequireCapability`.
- Đổi vai trò **và** email trong cùng một lần lưu thì vẫn đăng xuất, vì có đổi email.
- Hợp đồng: [02-quan-ly-nguoi-dung.md](../03-api/02-quan-ly-nguoi-dung.md),
  [06-thong-bao-trong-ung-dung.md](../03-api/06-thong-bao-trong-ung-dung.md).
