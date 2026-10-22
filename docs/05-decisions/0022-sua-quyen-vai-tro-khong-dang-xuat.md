# ADR-0022: Sửa quyền của vai trò không đăng xuất ai — báo qua hub để trình duyệt tự refresh token

- **Trạng thái:** Đề xuất — dòng *"Không đổi"* về đổi vai trò của một người thay thế bởi
  [ADR-0023](0023-doi-vai-tro-nguoi-dung-khong-dang-xuat.md)
- **Ngày:** 2026-10-22
- **Người quyết định:** Nhóm SEP490-G116
- **Liên quan:** `SCRUM-105`, `SCRUM-60`; thay thế phần *"Kèm theo: thu hồi session nằm ngoài
  transaction"* của [ADR-0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) đối với việc sửa ma
  trận quyền

## Bối cảnh

Capability nằm trong access token lúc phát. Để thay đổi ma trận *"có hiệu lực ngay"* (`SCRUM-60`),
`RolePermissionService` từng huỷ refresh token của mọi tài khoản thuộc vai trò vừa sửa, ở cả hai
chiều. Hệ quả (`SCRUM-105`): ai đang đăng nhập với vai trò đó đều bị đăng xuất khi quay lại tab, kể
cả khi chỉ vừa được **thêm** quyền.

Bước huỷ đó không thêm lớp bảo vệ nào:

- `CapabilityGate.RequireCapability` đọc lại quyền từ database ở **mỗi request**, nên quyền vừa bị
  gỡ đã bị từ chối (`403`) ngay ở request kế tiếp, kể cả với access token cũ.
- Huỷ refresh token không làm access token đang dùng mất hiệu lực.
- `/api/auth/refresh` đã đọc lại quyền, nên token mới luôn mang đúng bộ quyền hiện tại.

## Quyết định

Chúng tôi sẽ **không** huỷ phiên khi sửa quyền của một vai trò. Sau khi transaction commit, server
gửi sự kiện `capabilitiesChanged` (không payload) trên `/hubs/notifications` tới mọi tài khoản đang
hoạt động thuộc vai trò đó. Trình duyệt nhận được thì đọc `/me` rồi refresh token, menu và route đổi
theo quyền mới. Mỗi lần hub kết nối lại, trình duyệt cũng làm như vậy để bù sự kiện bị lỡ.

Gửi lỗi thì ghi log rồi bỏ qua. Sự kiện chỉ là tín hiệu; server vẫn tự chặn đúng.

Không đổi: đổi vai trò hoặc email của **một người**, vô hiệu hoá tài khoản và đặt lại mật khẩu
**vẫn** huỷ phiên của tài khoản đó.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ huỷ phiên (như ADR-0007) | Không cần realtime | Đăng xuất cả nhóm người, kể cả khi chỉ thêm quyền | Không thêm bảo vệ nào so với đọc lại quyền ở mỗi request |
| Chỉ bỏ huỷ phiên, không push | Diff nhỏ nhất | Quyền mới chỉ hiện khi đổi tab, gặp `403` hoặc token sắp hết hạn (≤ 15 phút) | Không đạt "dùng được ngay mà không cần F5" |
| Kiểm "phiên bản quyền" trong token ở mỗi request | Đóng mọi khả năng token cũ dùng được | Thêm cột, thêm claim, thêm logic | Lớp đọc lại DB của `CapabilityGate` đã đóng việc này |

## Hệ quả

- Không ai bị đăng xuất vì quản trị viên sửa ma trận quyền; quyền mới áp dụng ngay.
- Kết luận "an toàn" dựa vào điều kiện: **mọi endpoint gắn quyền phải đi qua `RequireCapability`**
  (đọc lại DB), không được chỉ kiểm claim. Reviewer giữ điều kiện này.
- Ngay sau khi bị gỡ quyền, giao diện có thể còn hiện nút cũ trong chốc lát; bấm vào thì nhận `403`.
- Hợp đồng: [03-ma-tran-phan-quyen.md](../03-api/03-ma-tran-phan-quyen.md),
  [06-thong-bao-trong-ung-dung.md](../03-api/06-thong-bao-trong-ung-dung.md).
