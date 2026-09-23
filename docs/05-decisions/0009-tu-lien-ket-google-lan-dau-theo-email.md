# ADR-0009: Tự liên kết tài khoản Google ở lần đăng nhập đầu, theo email đã xác minh

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-23
- **Người quyết định:** nhóm SEP490-G116 — người làm: Hoàng Đức Lộc (`SCRUM-11`)
- **Liên quan:** `SCRUM-11` (đăng nhập), `SCRUM-13` (quản lý người dùng),
  [ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md) (email + mật khẩu hoặc Google, không IdP riêng)

## Bối cảnh

`/api/auth/google` tìm tài khoản nội bộ theo `users.google_subject_id` (claim `sub` của ID token),
**không** theo email — vì email đổi được, và trên domain workspace còn có thể bị cấp lại cho người
khác. Quyết định đó được ghi trong `docs/03-api/01-xac-thuc.md`.

Nhưng không có đường nào **ghi** vào cột đó: `POST /api/users` và `PUT /api/users/{id}` không đụng
tới nó, `/api/auth/google` không tự liên kết, và chưa có màn hình liên kết. Hệ quả (comment mới nhất
của `SCRUM-11`): admin tạo tài khoản bằng một Gmail, người đó bấm **Tiếp tục với Google** bằng đúng
Gmail ấy và nhận `MSG03`. Trên môi trường không có quyền vào DB, nút Google trả `MSG03` với **mọi
người dùng**.

## Quyết định

Chúng tôi sẽ tự liên kết ở **lần đăng nhập Google đầu tiên**. Khi không tìm thấy tài khoản theo
`sub`, `/api/auth/google` tìm tài khoản thoả **cả ba**:

1. `email` trùng email trong token (cột `citext`, không phân biệt hoa thường);
2. email đó Google đã xác minh (`email_verified = true` — `GoogleTokenVerifier` vốn đã từ chối token
   thiếu điều này, nên mọi `GoogleIdentity.Email` đều đã xác minh);
3. `google_subject_id IS NULL` và `is_active = true`.

Có thì lưu `sub` vào tài khoản đó bằng **một câu `UPDATE ... WHERE`** (kiểm và ghi cùng lúc, hai lần
đăng nhập đồng thời không cùng liên kết được), ghi audit `user.google_linked`, rồi đi tiếp các nhánh
như bình thường. Từ lần sau chỉ so theo `sub`.

Giữ nguyên:

- **Admin vẫn là người cấp quyền.** Gmail đó vào được chỉ vì admin đã chủ động tạo tài khoản bằng
  đúng email ấy. Gmail lạ vẫn nhận `MSG03`; vẫn không tự tạo tài khoản.
- **Tài khoản đã liên kết không bao giờ bị liên kết lại.** Email đổi hay bị cấp lại sau lần đầu cũng
  không chuyển được tài khoản sang Google identity khác.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Endpoint để admin nhập `sub` | Không ghép theo email lần nào | Admin phải lấy `sub` của người dùng — một chuỗi ~21 số chỉ thấy khi decode ID token; cần thêm màn hình | Không làm được trong thực tế nếu không có người dùng ngồi cạnh |
| Người dùng tự liên kết sau khi đăng nhập bằng mật khẩu | Chứng minh sở hữu cả hai | Tài khoản chỉ-Google (không mật khẩu) không bao giờ liên kết được; cần thêm màn hình | Loại trừ đúng loại tài khoản mà migration của `SCRUM-11` sinh ra để phục vụ |
| Giữ nguyên, liên kết bằng SQL | Không đổi gì | Nút Google vô dụng ngoài máy dev | Là chính lỗi đang sửa |

## Hệ quả

- Tài khoản do admin tạo bằng một Gmail đăng nhập được bằng Google mà không cần thao tác DB. FE không
  phải sửa gì.
- **Rủi ro còn lại:** nếu địa chỉ email bị cấp lại cho người khác **trước** lần đăng nhập Google đầu
  tiên của chủ cũ, người mới sẽ liên kết được. Cửa sổ đó chỉ mở từ lúc admin tạo tài khoản đến lần
  đăng nhập Google đầu tiên; admin tạo tài khoản bằng email của người đang làm việc thì cửa sổ ấy
  thực tế không có.
- Tài khoản bị vô hiệu hoá trước lần đăng nhập Google đầu tiên thì **không** được liên kết, nhưng
  trả `MSG04` ("Tài khoản không khả dụng") chứ không phải `MSG03`: tài khoản có tồn tại, bảo "chưa
  được cấp quyền" sẽ khiến người dùng đi xin một quyền họ đã có. Người gọi đã được Google xác minh
  là chủ email, nên không lộ gì. Tài khoản trùng email nhưng đã liên kết với `sub` khác vẫn `MSG03`.
- Muốn đổi Google identity của một tài khoản đã liên kết thì vẫn phải sửa bằng SQL. Chưa có ticket;
  cần thì là một nút "gỡ liên kết" trong màn sửa người dùng, sau đó lần đăng nhập kế tiếp tự liên kết.
