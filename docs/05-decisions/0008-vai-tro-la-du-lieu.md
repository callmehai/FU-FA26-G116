# ADR-0008: Vai trò là dữ liệu, không phải enum

- **Trạng thái:** Đã chấp nhận (triển khai trong `SCRUM-63`, migration `RolesAsData`)
- **Ngày:** 2026-09-23
- **Người quyết định:** nhóm SEP490-G116 — người làm: Hoàng Đức Lộc (`SCRUM-63`)
- **Liên quan:** `SCRUM-63` (quản lý vai trò), `SCRUM-60` (ma trận vai trò – quyền),
  [ADR-0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) (ma trận sửa được lúc chạy)

## Bối cảnh

Đến `SCRUM-60`, vai trò là enum `SystemRole` bốn giá trị — `Chair`, `Secretary`, `Member`,
`Admin` — lưu dạng chuỗi trong `users.role` và `role_capabilities.role`. Quản trị viên sửa được
vai trò nào mang quyền nào, nhưng muốn có vai trò thứ năm thì phải sửa code rồi deploy.

Tài liệu của `SCRUM-60` dẫn [ADR-0004](0004-mot-to-chuc-khong-co-don-vi.md) làm lý do cho việc
đó. ADR-0004 nói về "một tổ chức, không có đơn vị", **không nói gì về vai trò** — tức là chưa từng
có quyết định nào chốt "vai trò cố định". Class diagram của BA thì vẽ `Role` là `<<entity>>` có
`roleId`, `roleName`, `description`, `isBuiltIn`: bản vẽ vốn muốn vai trò là dữ liệu.

Có hai thứ trông giống nhau nhưng khác bản chất:

- **Quyền** (`Capability`) chỉ có nghĩa vì có code kiểm nó. Một quyền quản trị viên tự thêm sẽ không
  gác cửa cái gì — nó trông như bảo mật mà không phải bảo mật. Lý do này đúng và vẫn giữ.
- **Vai trò** chỉ là một **bó quyền có tên**. Code nghiệp vụ không bao giờ so sánh tên vai trò —
  comment của `SystemRole` ghi rõ điều đó — nên thêm một bó mới không cần thêm code nào kiểm nó.

Lý do "quyền phải là enum" vì vậy **không** áp được sang vai trò.

## Quyết định

Chúng tôi sẽ chuyển vai trò thành bảng `roles`, khoá `Guid`, và cho quản trị viên tạo, đổi tên, xoá
vai trò qua giao diện.

- `Capability` **vẫn là enum**. Không có nút "Tạo quyền mới".
- Bốn vai trò hiện có thành bốn dòng **mặc định** (`is_built_in = true`) với **id cố định**, khai
  báo một lần trong code (`BuiltInRoles`). Vai trò mặc định không đổi tên, không xoá được; quyền
  của chúng vẫn sửa được như `SCRUM-60`.
- Chỉ xoá được vai trò **không còn tài khoản nào** mang nó, kể cả tài khoản đã vô hiệu hoá. Không có
  "chuyển hết người sang vai trò khác rồi xoá" trong một bước.
- Vai trò mặc định được chèn bằng SQL trong **migration**, không bằng `HasData` — cùng lý do với
  ADR-0007, và vì migration đó còn phải điền `role_id` cho dữ liệu cũ ngay trong cùng bước.

Cách làm chi tiết: [docs/03-api/04-quan-ly-vai-tro.md](../03-api/04-quan-ly-vai-tro.md).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ enum, viết ADR giải thích vì sao vai trò cố định | Không đụng code | Tổ chức muốn thêm vai trò thì phải chờ deploy; lệch class diagram | Lý do "quyền phải là enum" không áp sang vai trò (xem Bối cảnh) — không có lý do thật để giữ |
| Bảng `roles` khoá chuỗi (`code` = `"Chair"`, …), `users.role` thành khoá ngoại tới đó | Migration gần như không phải chuyển dữ liệu; API giữ nguyên `"role": "Chair"` | Vai trò tự tạo phải có một `code` — hoặc quản trị viên tự gõ một định danh (khó hiểu), hoặc hệ thống sinh ra (trông có nghĩa nhưng không); lệch quy ước khoá `Guid.CreateVersion7()` của mọi entity | Tiết kiệm một lần migration nhưng để lại hai kiểu định danh vai trò mãi mãi |
| Bảng `roles` khoá `Guid`, vai trò mặc định có id cố định | Theo đúng quy ước khoá; khớp class diagram; một kiểu định danh duy nhất | Đổi hợp đồng API (`role` → `roleId`) ở `/api/users`, `/api/auth/me`, `/api/roles/capabilities`; migration phải chuyển dữ liệu bằng tay | **Chọn** |

## Hệ quả

- **Đổi hợp đồng API.** FE thôi gửi/nhận chuỗi `"Chair"`, chuyển sang `roleId`. FE bỏ bảng cứng
  `ROLE_LABEL`, lấy tên vai trò từ backend.
- **Migration phải sửa tay.** EF sinh ra `DropColumn("role")` + `AddColumn("role_id")` — chạy nguyên
  như vậy là mất vai trò của mọi tài khoản. Phải chèn bước điền dữ liệu vào giữa.
- **`MSG04` của UC-01 mất nghĩa.** Nó bắn khi "vai trò không phải `Member` mà không có quyền nào";
  với vai trò tự tạo, không có quyền nào là hợp lệ (giống `Member`). Code đã bỏ nhánh này (và
  `MSG05` của UC-02) — BA cần sửa đặc tả UC-01/UC-02 cho khớp.
- **Class diagram không cần đổi `Role` thành enum nữa.** Mục F2 trong
  [07-classdiagram/README.md](../07-classdiagram/README.md) đề nghị BA làm việc đó; quyết định này
  làm đề nghị ấy hết hiệu lực.
- **Luật chống tự khoá không đổi**, nhưng có thêm chỗ ghi `role_capabilities` (tạo vai trò kèm quyền,
  xoá vai trò). Mọi chỗ ghi bảng đó đi qua `IAdministratorInvariantGuard` như ADR-0007 quy định.
