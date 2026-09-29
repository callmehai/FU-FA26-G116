# 04 · Quản lý vai trò

> **Trạng thái: BE đã làm; FE đã cập nhật theo mục 8 và chờ Tester xác minh tích hợp.** Người làm BE:
> Hoàng Đức Lộc (`SCRUM-63`). Quyết định nền: [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md).
> Sequence diagram: [CreateRole](../06-sequencediagram/CreateRole.png),
> [UpdateRole](../06-sequencediagram/UpdateRole.png), [DeleteRole](../06-sequencediagram/DeleteRole.png).

Quản trị viên **tạo vai trò mới, đổi tên, xoá** vai trò tự tạo, không phải chờ deploy. Vai trò là
bảng `roles` khoá `Guid`; mọi chỗ trước đây dùng enum `SystemRole` giờ dùng `roleId`; bốn vai trò cũ
là bốn dòng **mặc định** có id cố định. `Capability` **vẫn là enum** — lý do ở ADR-0008.

---

## 1. Phạm vi

**Có:**

- Bảng `roles`, migration `RolesAsData` chuyển dữ liệu cũ sang (không mất dòng nào)
- `GET /api/roles`, `POST /api/roles`, `PUT /api/roles/{id}`, `DELETE /api/roles/{id}`
- Đổi hợp đồng các endpoint đang trả/nhận vai trò: `/api/users`, `/api/auth/*`,
  `/api/roles/capabilities` (mục 4.5)
- Nhật ký kiểm toán `role.created`, `role.updated`, `role.deleted`

**Không có:**

- Tạo quyền mới — `Capability` vẫn là enum (xem `03-ma-tran-phan-quyen.md`, mục *Không tạo được
  quyền mới*)
- Đổi tên, đổi mô tả hoặc xoá **vai trò mặc định** (`Chủ toạ`, `Thư ký`, `Thành viên`,
  `Quản trị viên`). Quyền của chúng vẫn sửa được như `SCRUM-60`
- "Chuyển hết người sang vai trò khác rồi xoá" trong một bước. Quản trị viên đổi vai trò từng người
  ở màn hình người dùng, xong mới xoá được
- Giới hạn số vai trò tối đa. Vai trò là **dòng** của ma trận, không phải cột

---

## 2. Mô hình dữ liệu

### Bảng `roles`

| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | `Guid.CreateVersion7()` cho vai trò tự tạo; id **cố định** cho vai trò mặc định |
| `name` | `citext` | `NOT NULL`, **unique** (`ix_roles_name`) | `citext` như `users.email`: unique index tự không phân biệt hoa/thường. Tối đa 64 ký tự **chỉ kiểm ở service** — `citext` bỏ qua độ dài khai báo |
| `description` | `varchar(256)` | `NULL` | |
| `is_built_in` | `boolean` | `NOT NULL` | |
| `created_at`, `updated_at` | `timestamptz` | `NOT NULL` | |

### `users` và `role_capabilities`

| Bảng | Trước | Sau |
|---|---|---|
| `users` | `role varchar(32)` (tên enum) | `role_id uuid NOT NULL` → FK `roles.id` **`ON DELETE RESTRICT`**, index `ix_users_role_id` |
| `role_capabilities` | PK `(role, capability)` | PK `(role_id, capability)`, `role_id` → FK `roles.id` **`ON DELETE CASCADE`** |

- **`RESTRICT` ở `users`** là chốt chặn cuối của luật "chỉ xoá vai trò không còn ai giữ": có người
  được gán vào vai trò chen giữa lúc kiểm và lúc xoá thì database từ chối (`23503`), service bắt lỗi
  đó và trả `MSG25`, không thành `500`.
- **`CASCADE` ở `role_capabilities`**: xoá vai trò thì quyền của nó đi theo.

### Vai trò mặc định — `BuiltInRoles`

[`backend/TrustCabinet.Domain/Access/BuiltInRoles.cs`](../../backend/TrustCabinet.Domain/Access/BuiltInRoles.cs)
giữ bốn id cố định, sinh một lần rồi dán cứng:

| Vai trò | `id` | `name` |
|---|---|---|
| Chair | `01a0ce78-7202-7945-be13-d2022cf25203` | Chủ toạ |
| Secretary | `01a0ce78-7203-7c7f-8326-7a0baebf4c22` | Thư ký |
| Member | `01a0ce78-7204-7558-b181-e908a08e83a3` | Thành viên |
| Admin | `01a0ce78-7205-7486-b976-d52d428d5451` | Quản trị viên |

Backend dùng `BuiltInRoles.IsDefault` để đánh dấu `Member` là vai trò mặc định khi tạo tài khoản;
frontend chỉ đọc cờ `isDefault`. Các quy tắc phân quyền vẫn dựa trên capability, không dựa trên tên
vai trò.

---

## 3. Migration `RolesAsData` — đã sửa tay

EF sinh ra `DropColumn("role")` + `AddColumn("role_id")` mặc định `0000…` — chạy nguyên như vậy là
mất vai trò của mọi tài khoản và cả ma trận. File
[`20260923134138_RolesAsData.cs`](../../backend/TrustCabinet.Infrastructure/Migrations/20260923134138_RolesAsData.cs)
đã được sắp lại:

1. Tạo `roles` + unique index trên `name`
2. `INSERT` bốn vai trò mặc định (không `HasData`). Id viết **literal** trong migration, bằng đúng
   `BuiltInRoles` nhưng không tham chiếu tới nó: migration đã chạy là bất biến, không được đổi theo
   code. `RolesAsDataMigrationTests` kiểm hai bên vẫn khớp
3. Với `users` rồi `role_capabilities`: thêm `role_id` **nullable** → `UPDATE … CASE role` → nếu còn
   dòng `NULL` (giá trị `role` lạ) thì `RAISE EXCEPTION` và **dừng**, không đoán → `NOT NULL`
4. Index + FK `RESTRICT` cho `users`; đổi PK + FK `CASCADE` cho `role_capabilities`; bỏ cột `role`

`Down()` đưa bốn vai trò mặc định về chuỗi enum; **từ chối** (`RAISE EXCEPTION`) nếu còn vai trò tự
tạo, thay vì âm thầm đổi người giữ nó thành `Member`.

Đã kiểm trên DB dev có dữ liệu (7 tài khoản, ma trận 5 dòng): đủ người đúng vai trò, đủ 5 dòng ma
trận; `Down()` rồi `Up()` lại vẫn giữ nguyên. `RolesAsDataMigrationTests` kiểm lại việc này trên
PostgreSQL thật mỗi lần chạy test.

`ReferenceDataSeeder` chèn vai trò mặc định **còn thiếu** theo id (không bao giờ cập nhật dòng đã
có), rồi mới đến ma trận (chỉ khi bảng rỗng).

---

## 4. Endpoint

Tất cả **yêu cầu `AdminUsers`**, đọc lại tài khoản người gọi từ database ở từng request (như
`/api/users`). Thiếu token `401`, thiếu quyền `403`, id không tồn tại `404` **không body**. Id không
phải Guid (`/api/roles/Chair`) không khớp route → `404`.

| Method | Path | Việc |
|---|---|---|
| `GET` | `/api/roles` | Liệt kê vai trò |
| `POST` | `/api/roles` | Tạo vai trò (UC-11) |
| `PUT` | `/api/roles/{id}` | Đổi tên / mô tả (UC-12) |
| `DELETE` | `/api/roles/{id}` | Xoá (UC-13) |

### 4.1 `GET /api/roles`

```json
[
  { "id": "01a0ce78-7202-…", "name": "Chủ toạ", "description": "Chủ trì và phê duyệt cuộc họp.", "isBuiltIn": true, "isDefault": false, "userCount": 1 },
  { "id": "01a0ce78-7204-…", "name": "Thành viên", "description": null, "isBuiltIn": true, "isDefault": true, "userCount": 5 },
  { "id": "0199…", "name": "Thư ký phó", "description": null, "isBuiltIn": false, "isDefault": false, "userCount": 0 }
]
```

- **Thứ tự cố định:** bốn vai trò mặc định trước (`Chair`, `Secretary`, `Member`, `Admin`), rồi vai
  trò tự tạo theo `created_at`. `roles` trong `GET /api/roles/capabilities` dùng **cùng thứ tự**.
- `userCount` đếm **mọi** tài khoản mang vai trò, kể cả đã vô hiệu hoá — đúng con số chặn việc xoá.
- `isDefault` đánh dấu vai trò được chọn sẵn khi tạo tài khoản; frontend không cần biết id seed.
- Không kèm quyền: quyền ở `GET /api/roles/capabilities`.

### 4.2 `POST /api/roles`

```json
{ "name": "Thư ký phó", "description": "Hỗ trợ thư ký soạn biên bản.", "capabilities": ["MinutesEditor"] }
```

- `name`: bắt buộc; **trim**, gộp khoảng trắng liên tiếp và chuẩn hoá Unicode **NFC** trước khi
  kiểm (gõ Unikey kiểu "Unicode tổ hợp" không tạo được vai trò thứ hai trông y hệt); rỗng hoặc dài
  hơn 64 → `MSG20`.
- Body rỗng hoặc `null` → `MSG20` như form bỏ trống, không phải `400` mặc định của framework.
- `description`: tuỳ chọn, tối đa 256; rỗng/toàn khoảng trắng lưu thành `null`.
- `capabilities`: tuỳ chọn, mặc định `[]` — **rỗng là hợp lệ** (vai trò hoạt động giống `Member`).
  Mã so theo **tên** enum, không phân biệt hoa/thường, trùng lặp bị gộp. Mã lạ — kể cả số như `"1"` —
  → `MSG20`, nêu đúng tên mã sai.
- Mọi lỗi của form trả về **cùng lúc** trong `errors` (`name`, `description`, `capabilities`).
- Trùng tên (không phân biệt hoa/thường, kể cả trùng tên vai trò mặc định) → `MSG21`. Service kiểm
  trước, **và** bắt `23505` của unique index cho trường hợp hai request cùng lúc.
- Ghi `roles` + `role_capabilities` trong **một transaction**, qua `IAdministratorInvariantGuard`.

Thành công: `201`, `Location: /api/roles/{id}`:

```json
{ "code": "MSG19", "message": "Tạo vai trò thành công.", "role": { "id": "…", "name": "Thư ký phó", "description": "…", "isBuiltIn": false, "isDefault": false, "userCount": 0 } }
```

### 4.3 `PUT /api/roles/{id}`

```json
{ "name": "Phó thư ký", "description": null }
```

Thứ tự kiểm theo sequence diagram UC-12:

1. Không tồn tại → `404`
2. Vai trò mặc định → `MSG24` (trước cả validate: không có gì để sửa trong form)
3. Validate như 4.2 → `MSG20`
4. Không đổi gì (so **chính xác**, sau khi chuẩn hoá) → `MSG22` nhưng **không** ghi DB, **không**
   audit — quy ước "không đổi gì thì không ghi gì" của `SCRUM-60`
5. Trùng tên vai trò **khác** → `MSG21`. Đổi hoa/thường tên của chính nó là hợp lệ
6. Lưu → `MSG22` + `role`. Vai trò bị quản trị viên khác xoá chen giữa lúc đọc và lúc lưu → `404`
   (nhánh "Role not found", FE tải lại danh sách), không phải `MSG18`

**Không** nhận `capabilities` (có gửi cũng bị bỏ qua) — quyền chỉ sửa qua
`PUT /api/roles/capabilities`, để luật chống tự khoá canh ở một chỗ. Không đi qua guard: đổi tên
không chạm quyền, không chạm người.

### 4.4 `DELETE /api/roles/{id}`

Thứ tự theo UC-13, chạy trong guard:

1. Không tồn tại → `404`
2. Vai trò mặc định → `MSG24`
3. Còn tài khoản mang (kể cả đã vô hiệu hoá) → `409` `MSG25` kèm `userCount`:

   ```json
   { "code": "MSG25", "message": "Vai trò này vẫn còn 3 tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.", "userCount": 3 }
   ```

4. Xoá; FK `RESTRICT` từ chối vì có người vừa được gán chen vào → vẫn `MSG25` với số đếm mới
5. Thành công: `200` `{ "code": "MSG23", "message": "Xoá vai trò thành công." }`.
   `role_capabilities` của vai trò bị xoá theo (`CASCADE`)

### 4.5 Endpoint đã có đổi hợp đồng

**Thay đổi phá hợp đồng** — FE phải sửa (mục 8).

| Endpoint | Trước | Sau |
|---|---|---|
| `GET /api/users`, `GET /api/users/{id}`, response tạo/sửa/đổi trạng thái | `"role": "Chair"` | `"roleId": "…", "roleName": "Chủ toạ"` |
| `POST /api/users`, `PUT /api/users/{id}` | body `"role": "Chair"` | body `"roleId": "…"`; thiếu / không đọc được / không tồn tại → `MSG09`, key `roleId`, `Vai trò không tồn tại.` |
| `/api/auth/login`, `/google`, `/refresh`, `/me` — object `user` | `"role": "Chair"` | `"roleId": "…", "roleName": "Chủ toạ"` |
| `GET /api/roles/capabilities` — mỗi dòng `roles[]` | `{ "role": "Admin", "capabilities": [...], "isBuiltIn": true }` | `{ "id": "…", "name": "Quản trị viên", "description": "…", "isBuiltIn": true, "capabilities": [...] }` |
| `PUT /api/roles/capabilities` | body `"role": "Admin"` | body `"roleId": "…"`; id lạ → `MSG17`, key `roleId` |
| Claim `role` trong access token | tên enum | **bỏ** — không code nào đọc claim này; quyền nằm ở claim `capability` |

### 4.6 Mã thông báo

| Mã | HTTP | Khi nào | Message |
|---|---|---|---|
| `MSG19` | `201` | Tạo thành công | `Tạo vai trò thành công.` |
| `MSG20` | `400` | Dữ liệu không hợp lệ — kèm `errors` theo field | `Thông tin vai trò không hợp lệ.` |
| `MSG21` | `409` | Trùng tên | `Đã có vai trò dùng tên này. Hãy đặt một tên khác.` |
| `MSG22` | `200` | Sửa thành công (kể cả không đổi gì) | `Cập nhật vai trò thành công.` |
| `MSG23` | `200` | Xoá thành công | `Xoá vai trò thành công.` |
| `MSG24` | `409` | Sửa tên hoặc xoá vai trò mặc định | `Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. Bạn vẫn sửa được quyền của nó.` |
| `MSG25` | `409` | Xoá vai trò còn người mang — kèm `userCount` | `Vai trò này vẫn còn {n} tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.` |
| `MSG18` | `500` | Database từ chối vì lý do khác (mất kết nối, timeout) — nhánh "Unable to …" của diagram | `Không thể tạo / cập nhật / xoá vai trò. Vui lòng thử lại.` |

Diagram không đặt mã cho nhánh "Unable to create/update/delete role"; tạm dùng chung `MSG18` (cùng
nghĩa "lưu không được, thử lại" của UC-10) — **chờ BA chốt**, mục 10.

---

## 5. Luật chống tự khoá và tương tranh

Bất biến ADR-0007 — **còn ít nhất một tài khoản đang hoạt động giữ `AdminUsers`** — không đổi, và
vẫn đếm theo **tài khoản** chứ không theo tên vai trò: một người trong vai trò tự tạo có
`AdminUsers` là quản trị viên thật, nên Admin cũ vô hiệu hoá được (có test).

| Thao tác | Guard? | Lý do |
|---|---|---|
| Tạo vai trò (kèm quyền) | **Có** | Ghi `role_capabilities` — mọi chỗ ghi bảng này đều qua guard |
| Đổi tên / mô tả | Không | Không chạm quyền, không chạm người |
| Xoá vai trò | **Có** | Xoá `role_capabilities` (cascade); FK `RESTRICT` chặn người gán chen vào |

Với **tạo** và **xoá**, ghi DB và audit cùng nằm trong transaction của guard. **Đổi tên** không qua
guard nên audit là một lần lưu riêng sau khi đổi tên đã lưu — đúng quy ước của `AuditService` (audit
lỗi thì thao tác đã xảy ra vẫn giữ, người dùng nhận `500`). Một câu lệnh bị database từ chối
(`23505`, `23503`) không làm hỏng transaction: EF bọc `SaveChanges` trong savepoint và rollback về
đó.

Tạo tài khoản (`POST /api/users`) cũng không qua guard: vai trò bị xoá chen giữa lúc kiểm và lúc lưu
thì FK từ chối (`23503`) và API trả `MSG09` `errors.roleId` như vai trò đã không tồn tại từ đầu.

## 6. Nhật ký kiểm toán

| `action` | `object_type` | `object_id` | `details` |
|---|---|---|---|
| `role.created` | `Role` | id vai trò mới | `{ "Name", "Description", "Capabilities": [...] }` |
| `role.updated` | `Role` | id vai trò | `{ "Before": { "Name", "Description" }, "After": { "Name", "Description" } }` |
| `role.deleted` | `Role` | id vai trò | `{ "Name", "Capabilities": [...] }` — đọc **trước** khi cascade xoá mất |
| `role.permissions.updated` | `Role` | **đổi:** id vai trò (trước là `null`) | `{ "Name", "Before": [...], "After": [...] }` |
| `user.created` / `user.updated` | `User` | — | vai trò ghi cả `RoleId` và `Role` (tên lúc đó) |

## 7. Đăng nhập và phiên

- **Tạo / đổi tên / xoá không kết thúc phiên của ai.** `/api/auth/me` trả tên mới ở lần FE đọc lại.
- Sửa quyền của vai trò vẫn kết thúc phiên người giữ nó, như `SCRUM-60`.
- **Đã bỏ nhánh `MSG04` của UC-01 (và `MSG05` của UC-02)** theo đề xuất: mọi vai trò không có quyền
  nào đều đăng nhập được như `Member`. Refresh cũng không còn cắt phiên vì "hết quyền" — token mới
  đơn giản không mang quyền nào. **Chờ BA sửa UC-01/UC-02** (mục 10). Nếu BA muốn giữ, cần định
  nghĩa mới không dựa vào tên vai trò (ví dụ cờ `can_sign_in` trên `roles`) — phạm vi thêm.

## 8. Phần FE

FE dùng `roleId` làm định danh và tên do backend trả về để hiển thị:

- `types/user.ts` không còn union `Role` hay bảng cứng `ROLE_LABEL`/`ROLES`; `CurrentUser` và
  `UserAccount` có `roleId` + `roleName`.
- `api/roles.ts` gọi bốn endpoint quản lý vai trò; `api/rolePermissions.ts` gửi `roleId` khi lưu ma trận.
- Màn hình **Vai trò** tạo vai trò với quyền ban đầu tuỳ chọn, sửa tên/mô tả vai trò tự tạo, hiển thị
  `userCount`; vai trò mặc định có badge và không có nút sửa/xoá. Xoá vai trò đang có người bị chặn;
  `MSG25` hiển thị số tài khoản từ backend.
- Màn hình **Người dùng & Quyền** tải bộ lọc và lựa chọn vai trò từ `GET /api/roles`; các request tạo/sửa
  người dùng gửi `roleId`.
- Header hiển thị `roleName`; màn hình ma trận dùng id vai trò cho draft, lưu quyền và xử lý trạng thái
  của vai trò hiện tại.
- `Capability` vẫn là enum; không có chức năng thêm quyền. FE chỉ hiển thị/ẩn route; API vẫn kiểm quyền
  server-side và trả `403` khi thiếu quyền. Route bị giới hạn tiếp tục render 404.

Đã chạy `npm run lint` và `npm run build` trong `frontend/`; kiểm tra tích hợp với backend cần được Tester
thực hiện theo luồng ở mục 9 trước khi ticket được đánh dấu Done.

## 9. Test

| File | Loại | Nội dung |
|---|---|---|
| `RoleServiceTests` | Unit, test double | Mọi nhánh alt của 3 sequence diagram + chuẩn hoá tên, mã quyền dạng số, race `23505`/`23503` |
| `RoleEndpointsTests` | HTTP in-memory | `401`/`403` cho cả 4 endpoint, mã MSG, shape JSON, `404` không body |
| `RoleRepositoryTests` | PostgreSQL thật | `citext` với tiếng Việt, unique index → `MSG21`, FK `RESTRICT` → `MSG25`, cascade, `userCount` gồm tài khoản vô hiệu hoá |
| `RolesAsDataMigrationTests` | PostgreSQL thật | Migration giữ đủ người + ma trận (kể cả dòng admin đã sửa), dừng khi gặp giá trị lạ, `Down()` đảo ngược và từ chối khi còn vai trò tự tạo |
| `DatabaseModelTests` | Model | `roles` có trong model, `citext` + unique, `RESTRICT`/`CASCADE`, không `HasData` |
| `ReferenceDataSeederTests` | PostgreSQL thật | Chèn vai trò mặc định còn thiếu, không sửa dòng đã có |
| `UserServiceTests` | Unit | Vai trò tự tạo có `AdminUsers` được tính vào bất biến; `roleId` không tồn tại → `MSG09` |

## 10. Câu hỏi mở cho BA

1. Chốt số UC (hiện dùng UC-11/12/13 theo sequence diagram) và mã `MSG19`–`MSG25`.
2. **Bỏ `MSG04` của UC-01 / `MSG05` của UC-02** — xem ghi chú dưới. **Code đã bỏ**; cần BA quyết giữ
   hay bỏ và sửa đặc tả UC-01/UC-02 theo.
3. Nhánh "Unable to create/update/delete role" dùng chung `MSG18` hay có mã riêng.
4. Vai trò mặc định có cần đổi **mô tả** được không? Hiện khoá cả tên lẫn mô tả.
5. `MSG24` và `MSG25` trả `409` — tester có kỳ vọng mã HTTP khác không?
6. ADR-0008 đang ghi *Đã chấp nhận* ngay trong PR triển khai nó — nhóm xác nhận khi duyệt PR.

### Ghi chú cho BA: vì sao bỏ `MSG04` / `MSG05`

**Luật cũ** (UC-01 bước kiểm quyền, UC-02 tương tự): tài khoản mà vai trò **không có quyền nào** thì
bị từ chối đăng nhập — `MSG04` / `MSG05` "chưa được cấp quyền truy cập hệ thống" — **trừ** vai trò
`Member`, vốn không có quyền nào mà vẫn phải vào được để dự họp.

**Vì sao không giữ được nguyên văn:** chữ "trừ `Member`" là so sánh tên vai trò trong code. Khi vai
trò thành dữ liệu (ADR-0008) thì:

- Vai trò tự tạo **không có quyền nào** là cấu hình hợp lệ và có chủ đích (ví dụ "Khách mời" — chỉ dự
  họp, giống `Member`). Giữ luật cũ thì người mang vai trò đó **không đăng nhập được**.
- Muốn miễn trừ nó thì phải so id vai trò trong code nghiệp vụ — đúng điều ADR-0008 tránh.

**Hành vi hiện tại (đã code):**

- Mọi tài khoản **đang hoạt động** đều đăng nhập được, kể cả vai trò không có quyền nào — vào được như
  `Member`: xem cuộc họp mình được mời, không mở được màn hình quản trị.
- `/refresh` không còn cắt phiên với lý do "hết quyền"; token mới chỉ đơn giản không mang quyền nào.
- Chặn một người đăng nhập vẫn làm được bằng **vô hiệu hoá tài khoản** (UC-07, `MSG03` / `MSG04`).
- Hệ quả BA cần biết: nếu quản trị viên gỡ hết quyền của `Chủ toạ` / `Thư ký` / `Quản trị viên`,
  người mang các vai trò đó **vẫn đăng nhập được** (trước đây bị chặn) — chỉ mất chức năng.

**BA cần chọn một:**

| Phương án | Việc phải làm |
|---|---|
| **A. Chấp nhận bỏ** (đề xuất) | Sửa đặc tả UC-01/UC-02: xoá bước kiểm quyền và `MSG04`/`MSG05` loại này; FE bỏ xử lý hai mã đó |
| B. Giữ luật "không có quyền thì không vào" | Cần định nghĩa không dựa vào tên: ví dụ cờ `can_sign_in` trên `roles` do quản trị viên bật/tắt. Thêm cột, migration, UI — ticket riêng |

## 11. Xong là khi

- [x] Migration chạy trên DB có dữ liệu mà không mất vai trò của ai; có test PostgreSQL thật
- [x] Bốn endpoint mới, và các endpoint đổi hợp đồng ở mục 4.5
- [x] Không còn `SystemRole` trong `backend/` và `tests/`
- [x] Audit cho `role.created` / `role.updated` / `role.deleted`
- [x] `dotnet build TrustCabinet.sln -warnaserror` và `dotnet test` xanh
- [x] Tài liệu liên quan đã cập nhật
- [ ] BA đã trả lời mục 10 (đang chờ — ghi rõ trong PR)
- [ ] Ticket `SCRUM-63` chuyển sang cột **Frontend**

## 12. Note bàn giao BE/BA cho SCRUM-63

FE hiện dùng `roleId` làm định danh vai trò, lấy tên từ backend, hỗ trợ tạo/sửa/xoá vai trò tự tạo,
chọn vai trò động trong màn hình người dùng và cập nhật ma trận theo id. `Capability` vẫn là enum;
không có thao tác tạo capability.

### Gửi BE

Đối chiếu code backend với hợp đồng FE đang dùng, hiện chưa thấy yêu cầu sửa BE mới. Nhờ BE xác nhận
môi trường/nhánh tích hợp cung cấp đúng các điểm sau để Tester chạy end-to-end:

- `GET /api/roles` trả `id`, `name`, `description`, `isBuiltIn`, `userCount`.
- Request tạo/sửa người dùng và cập nhật ma trận nhận `roleId`; response người dùng trả `roleId` và
  `roleName`.
- FE luôn gửi `DELETE /api/roles/{id}` để backend kiểm tra số người hiện tại. Nếu còn tài khoản mang
  vai trò, kể cả đã vô hiệu hoá, BE trả `409 MSG25` kèm `userCount` mới.
- Endpoint quản trị yêu cầu `AdminUsers`, thiếu quyền trả `403`; route UI bị giới hạn tiếp tục trả
  `404`.

Nếu các điểm trên đã ổn định trong môi trường tích hợp thì không cần thay đổi BE riêng cho phần FE.

### Cần BA chốt

- Chốt số UC (hiện tài liệu dùng UC-11/12/13) và dải mã `MSG19`–`MSG25`.
- Chốt bỏ hay giữ nhánh đăng nhập khi vai trò không có capability nào (`MSG04` UC-01 / `MSG05`
  UC-02). Code hiện đã bỏ nhánh này; nếu chấp nhận thì cập nhật đặc tả UC-01/UC-02.
- Chốt dùng chung `MSG18` cho lỗi tạo/sửa/xoá hay cần mã riêng.
- Chốt vai trò mặc định có được sửa mô tả không; hợp đồng hiện khoá cả tên và mô tả.
- Xác nhận `MSG24` và `MSG25` dùng HTTP `409`.
- Xác nhận trạng thái ADR-0008: thông tin ticket SCRUM-63 được chuyển tiếp ghi “Đề xuất”, còn file
  ADR hiện ghi “Đã chấp nhận”.

### Tester cần xác minh

- Tạo vai trò không quyền và có quyền; sửa tên/mô tả; vai trò mặc định không có thao tác sửa/xoá.
- Xoá vai trò trống; từ chối khi còn tài khoản active/inactive; kiểm tra race gán người và hiển thị
  `MSG25` với số người mới do BE trả.
- Gán vai trò tự tạo cho người dùng; kiểm tra ma trận quyền cho vai trò tự tạo.
- Thiếu `AdminUsers`: API trả `403`, route UI trả `404`; xác minh vẫn còn ít nhất một tài khoản active
  giữ `AdminUsers`.

FE đã chạy `npm run lint`, `npm run build` và `git diff --check`. Health trả `200`; hai endpoint
`GET /api/roles` và `GET /api/roles/capabilities` không token trả `401`. Luồng giao diện với tài khoản
có `AdminUsers` vẫn cần Tester xác minh trước khi đánh dấu ticket Done.
