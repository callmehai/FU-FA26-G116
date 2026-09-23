# 04 · Quản lý vai trò

> **Trạng thái: chưa làm — bản thiết kế cho `SCRUM-63`.** Người làm BE: Hoàng Đức Lộc. Khi code
> xong, sửa file này thành tài liệu của cái **đã làm** (như `03-ma-tran-phan-quyen.md`), xoá các chữ
> "đề xuất", và chuyển [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) sang *Đã chấp nhận*.

Cho quản trị viên **tạo vai trò mới, đổi tên, xoá** vai trò tự tạo. Hiện tại vai trò là enum
`SystemRole` bốn giá trị và không có bảng `roles` — muốn thêm vai trò phải sửa code rồi deploy.
Vì sao đổi, và vì sao `Capability` vẫn là enum: đọc [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)
trước.

Tóm tắt một dòng: **vai trò thành bảng `roles` khoá `Guid`; mọi chỗ đang dùng `SystemRole` chuyển
sang `roleId`; bốn vai trò cũ thành bốn dòng mặc định có id cố định.**

---

## 1. Phạm vi

**Làm:**

- Bảng `roles`, migration chuyển dữ liệu cũ sang
- `GET /api/roles`, `POST /api/roles`, `PUT /api/roles/{id}`, `DELETE /api/roles/{id}`
- Đổi hợp đồng các endpoint đang trả/nhận vai trò: `/api/users`, `/api/auth/*`,
  `/api/roles/capabilities`
- Nhật ký kiểm toán cho tạo / sửa / xoá vai trò

**Không làm:**

- Tạo quyền mới — `Capability` vẫn là enum, lý do ở `03-ma-tran-phan-quyen.md` mục *Không tạo được
  quyền mới*
- Đổi tên hoặc xoá **vai trò mặc định** (`Chủ toạ`, `Thư ký`, `Thành viên`, `Quản trị viên`)
- "Chuyển hết người sang vai trò khác rồi xoá" trong một bước. Quản trị viên đổi vai trò từng người
  ở màn hình người dùng, xong mới xoá được
- Giới hạn số vai trò tối đa. Vai trò là **dòng** của ma trận, không phải cột, nên thêm vai trò
  không làm vỡ bố cục màn hình

## 2. Thứ tự làm

Nên làm theo thứ tự này. Mỗi bước build và test xanh rồi mới sang bước sau:

1. **Đợi `SCRUM-12` (PR #14) merge**, rồi rebase. PR đó đụng `AuthEndpointsTests` và thêm
   `CapabilityGateTests` dùng `SystemRole` — làm trước thì phải sửa hai lần.
2. Entity `Role`, `BuiltInRoles`, configuration, migration (mục 3–4). Chạy migration trên một DB
   **có dữ liệu** (DB dev đã seed), kiểm tra không mất vai trò của ai.
3. Thay `SystemRole` bằng `roleId` trong domain, repository, service (mục 5). Sửa test cũ cho xanh.
4. Endpoint `/api/roles` mới (mục 6) và test.
5. Cập nhật tài liệu (mục 11), chuyển ticket sang cột **Frontend**.

---

## 3. Mô hình dữ liệu

### Bảng `roles` (mới)

| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| `id` | `uuid` | PK | `Guid.CreateVersion7()` cho vai trò tự tạo; id **cố định** cho vai trò mặc định |
| `name` | `citext`, tối đa 64 | `NOT NULL`, **unique** | `citext` như `users.email`: unique index tự không phân biệt hoa/thường |
| `description` | `varchar(256)` | `NULL` | |
| `is_built_in` | `boolean` | `NOT NULL` | |
| `created_at` | `timestamptz` | `NOT NULL` | |
| `updated_at` | `timestamptz` | `NOT NULL` | |

### Bảng `users` và `role_capabilities` (đổi)

| Bảng | Trước | Sau |
|---|---|---|
| `users` | `role varchar(32)` (tên enum) | `role_id uuid NOT NULL` → FK `roles.id` **`ON DELETE RESTRICT`**, có index |
| `role_capabilities` | PK `(role, capability)`, `role` là tên enum | PK `(role_id, capability)`, `role_id` → FK `roles.id` **`ON DELETE CASCADE`** |

- **`RESTRICT` ở `users`** là chốt chặn cuối của luật "chỉ xoá vai trò không còn ai giữ": dù service
  kiểm thiếu, hay có request gán người vào vai trò chen giữa lúc kiểm và lúc xoá, database vẫn từ
  chối (lỗi `23503`). Bắt lỗi đó và trả `MSG25`, không để nó thành `500`.
- **`CASCADE` ở `role_capabilities`**: xoá vai trò thì quyền của nó đi theo. Đó là dữ liệu của chính
  vai trò, không phải của ai khác.
- `capability` vẫn lưu dạng chuỗi tên enum như cũ.

### Vai trò mặc định: `BuiltInRoles`

Tạo `backend/TrustCabinet.Domain/Access/BuiltInRoles.cs` giữ **bốn id cố định**:

```csharp
public static class BuiltInRoles
{
    public static readonly Guid Chair     = new("…");
    public static readonly Guid Secretary = new("…");
    public static readonly Guid Member    = new("…");
    public static readonly Guid Admin     = new("…");
}
```

- Sinh bốn Guid **một lần** (`Guid.CreateVersion7()` trong một file script, hoặc `uuidgen`) rồi
  **dán cứng**. Không tính lúc chạy: migration, seeder, test và mọi database đều phải thấy đúng
  cùng bốn id.
- Đây là chỗ **duy nhất** code được nhắc tới một vai trò cụ thể. Code nghiệp vụ vẫn không bao giờ so
  sánh vai trò — nó hỏi quyền (`Capability`), như comment cũ của `SystemRole` quy định.
- Tên hiển thị của vai trò mặc định (`Chủ toạ`, `Thư ký`, `Thành viên`, `Quản trị viên`) nằm trong
  cột `name`, do migration chèn. FE thôi giữ bảng `ROLE_LABEL`.

---

## 4. Migration — phải sửa tay

⚠️ **Đừng chạy nguyên migration EF sinh ra.** EF thấy `User.Role` biến mất và `User.RoleId` xuất
hiện, nó sinh `DropColumn("role")` và `AddColumn("role_id")` — chạy như vậy là **mất vai trò của
mọi tài khoản**. Tương tự với `role_capabilities`: mất cả ma trận, và vì bảng rỗng nghĩa là không ai
giữ `AdminUsers` nên không ai đăng nhập vào sửa được (xem ADR-0007).

Sinh migration bình thường rồi **sắp lại** `Up()` theo thứ tự sau:

```text
1. CreateTable roles
2. Sql: INSERT INTO roles (id, name, description, is_built_in, created_at, updated_at)
        VALUES ('<Chair>', 'Chủ toạ', …, true, now(), now()), … (đủ 4 dòng)
3. AddColumn users.role_id (uuid, NULLABLE — tạm thời)
4. Sql: UPDATE users SET role_id = CASE role
            WHEN 'Chair' THEN '<Chair>'::uuid
            WHEN 'Secretary' THEN '<Secretary>'::uuid
            WHEN 'Member' THEN '<Member>'::uuid
            WHEN 'Admin' THEN '<Admin>'::uuid
        END
5. AlterColumn users.role_id → NOT NULL      (fail ở đây = còn giá trị role lạ → dừng, đừng đoán)
6. DropColumn users.role; CreateIndex + AddForeignKey (RESTRICT)
7. Làm y hệt 3–6 cho role_capabilities: thêm role_id, UPDATE … CASE, NOT NULL,
   DropPrimaryKey cũ, DropColumn role, AddPrimaryKey (role_id, capability), AddForeignKey (CASCADE)
```

- Chèn vai trò mặc định bằng `migrationBuilder.Sql(...)`, **không** dùng `HasData`. Lý do giống
  ADR-0007, và vì bước 4 cần các dòng đó có sẵn ngay trong cùng migration.
- Id trong SQL lấy từ `BuiltInRoles` (nối chuỗi trong C#), đừng gõ lại lần thứ hai.
- `Down()`: đưa được bốn vai trò mặc định về chuỗi enum. Vai trò tự tạo **không** có chuỗi enum
  tương ứng — cho `Down()` ném lỗi rõ ràng nếu còn vai trò tự tạo, đừng âm thầm đổi họ thành
  `Member`.
- Migration đã lên `main` là **bất biến** (`AGENTS.md`). Sai thì sửa bằng migration mới.

**Kiểm tra trước khi mở PR:** chạy migration trên DB dev đã có seed (bốn tài khoản mẫu + ma trận mặc
định), rồi:

```sql
select u.email, r.name from users u join roles r on r.id = u.role_id;       -- đủ người, đúng vai trò
select r.name, rc.capability from role_capabilities rc join roles r on r.id = rc.role_id;  -- đủ 5 dòng
```

`ReferenceDataSeeder` (lưới an toàn của ADR-0007) cũng phải biết bảng `roles`: nếu thiếu vai trò mặc
định nào thì chèn theo id, **không bao giờ cập nhật** dòng đã có. Chèn vai trò trước, ma trận sau —
ngược lại thì FK từ chối.

---

## 5. Những chỗ code phải sửa

Lấy từ `grep -rn SystemRole backend tests`. Cột cuối là việc cần làm.

**Domain**

| File | Việc |
|---|---|
| `Enums/AccessEnums.cs` | Xoá `SystemRole`. Giữ nguyên `Capability` |
| `Enums/MeetingEnums.cs` | `MeetingRole` có `<see cref="SystemRole"/>` trong comment → trỏ sang `Role` |
| `Entities/Role.cs` | **Mới** — `Id`, `Name`, `Description`, `IsBuiltIn`, `CreatedAt`, `UpdatedAt` |
| `Access/BuiltInRoles.cs` | **Mới** — mục 3 |
| `Entities/User.cs` | `SystemRole Role` → `Guid RoleId` + navigation `Role`. Mặc định `BuiltInRoles.Member` |
| `Entities/RoleCapability.cs` | `SystemRole Role` → `Guid RoleId` |
| `Access/IRolePermissionRepository.cs`, `IRolePermissionService.cs`, `RolePermissionMatrix.cs` | Khoá vai trò đổi từ `SystemRole` sang `Guid` |
| `Authentication/IUserRepository.cs`, `SignInOutcome.cs` | `SystemRole` → `Guid roleId`; kết quả đăng nhập mang thêm tên vai trò |
| `Users/IUserAccountRepository.cs`, `IUserService.cs`, `UserAccount.cs` | Như trên |
| `Access/IRoleService.cs`, `IRoleRepository.cs` | **Mới** — tạo / sửa / xoá / liệt kê |
| `Audit/IAuditService.cs` | Thêm `role.created`, `role.updated`, `role.deleted` (mục 8) |

**Infrastructure**

| File | Việc |
|---|---|
| `Configurations/RoleConfiguration.cs` | **Mới** — `citext` + unique cho `name` |
| `Configurations/UserConfiguration.cs` | Bỏ `HasConversion<string>()` của `Role`; FK `RoleId` `Restrict` |
| `Configurations/RoleCapabilityConfiguration.cs` | PK `(RoleId, Capability)`; FK `Cascade` |
| `TrustCabinetDbContext` | `DbSet<Role> Roles` |
| `Access/RolePermissionService.cs` | `Enum.IsDefined(role)` → kiểm id có trong `roles`. `Build()` đang duyệt `Enum.GetValues<SystemRole>()` → duyệt bảng `roles` (mục 6.1 về thứ tự). Audit `object_id` giờ là `role.Id`, không còn `null` |
| `Access/RolePermissionRepository.cs` | Truy vấn theo `role_id` |
| `Access/RoleService.cs`, `RoleRepository.cs` | **Mới** — mục 6 và 7 |
| `Authentication/AuthenticationService.cs` | Ba chỗ `user.Role != SystemRole.Member` (nhánh `MSG04`) — xem mục 9 |
| `Authentication/JwtTokenIssuer.cs` | Claim `ClaimTypes.Role` mang **id** vai trò thay vì tên. Không code nào đọc claim này, nhưng tên thì đổi được còn id thì không |
| `Authentication/UserRepository.cs`, `Users/UserAccountRepository.cs` | Theo `RoleId`; `Include(u => u.Role)` hoặc join để trả tên vai trò |
| `Users/UserService.cs` | Kiểm `roleId` có tồn tại (thay cho `Enum.IsDefined`). Luật "không tự hạ vai trò quản trị cuối cùng" (`MSG12`) giữ nguyên, vì nó hỏi **quyền** chứ không hỏi tên vai trò |
| `Seed/ReferenceData.cs` | Ma trận mặc định dùng `BuiltInRoles.*` |
| `Seed/DevelopmentDataSeeder.cs` | Bốn tài khoản mẫu dùng `BuiltInRoles.*` |
| `Seed/ReferenceDataSeeder.cs` | Chèn vai trò mặc định còn thiếu (mục 4) |

**Web**

| File | Việc |
|---|---|
| `Endpoints/RoleEndpoints.cs` | **Mới** — `MapRoleEndpoints()`, đăng ký trong `Program.cs` |
| `Endpoints/RolePermissionEndpoints.cs` | `ParseRole(string)` → đọc `roleId` (Guid) |
| `Endpoints/UserEndpoints.cs` | Như trên; response trả `roleId` + `roleName` |
| Endpoint `/api/auth/*` | Object `user` trả `roleId` + `roleName` |

**Tests** — mọi file trong `tests/TrustCabinet.Web.Tests` đang dùng `SystemRole.X` đổi sang
`BuiltInRoles.X`. Nhiều nhất là `UserServiceTests` (57 chỗ) và `RolePermissionServiceTests` (45
chỗ); phần lớn là thay thế cơ học.

---

## 6. Endpoint

Tất cả **yêu cầu `AdminUsers`** và đọc lại tài khoản người gọi từ database ở từng request, giống
`/api/users` và `/api/roles/capabilities`. Thiếu quyền trả `403`; id không tồn tại trả `404` không
body (bảng *403 hay 404* trong `AGENTS.md`).

| Method | Path | Việc |
|---|---|---|
| `GET` | `/api/roles` | Liệt kê vai trò |
| `POST` | `/api/roles` | Tạo vai trò |
| `PUT` | `/api/roles/{id}` | Đổi tên / mô tả |
| `DELETE` | `/api/roles/{id}` | Xoá |

### 6.1 `GET /api/roles`

```json
[
  {
    "id": "0192…",
    "name": "Chủ toạ",
    "description": "Chủ trì và phê duyệt cuộc họp.",
    "isBuiltIn": true,
    "userCount": 3
  },
  {
    "id": "0199…",
    "name": "Thư ký phó",
    "description": null,
    "isBuiltIn": false,
    "userCount": 0
  }
]
```

- **Thứ tự cố định:** bốn vai trò mặc định trước theo thứ tự `Chair`, `Secretary`, `Member`,
  `Admin` (như màn hình đang hiện), rồi vai trò tự tạo theo `created_at`. Dùng **cùng thứ tự này** cho
  `roles` trong `GET /api/roles/capabilities`, để hai màn hình không xếp khác nhau.
- `userCount` đếm **mọi** tài khoản mang vai trò, kể cả đã vô hiệu hoá — đúng con số chặn việc xoá.
  FE dùng nó để ẩn nút xoá và để hiện "3 người".
- Không kèm quyền: quyền đã có ở `GET /api/roles/capabilities`.

### 6.2 `POST /api/roles`

```json
{ "name": "Thư ký phó", "description": "Hỗ trợ thư ký soạn biên bản.", "capabilities": ["MinutesEditor"] }
```

- `name`: bắt buộc. **Trim** hai đầu và gộp khoảng trắng liên tiếp trước khi kiểm. Rỗng sau khi
  trim → `MSG20`. Dài hơn 64 → `MSG20`.
- `description`: tuỳ chọn, tối đa 256. Chuỗi rỗng lưu thành `null`.
- `capabilities`: tuỳ chọn, mặc định `[]`. **Rỗng là hợp lệ** — vai trò không có quyền nào hoạt động
  giống `Member` (dự họp, đọc tài liệu, biểu quyết). Mã quyền lạ → `MSG20`, nêu tên mã sai giống
  `MSG17`.
- Trùng tên (không phân biệt hoa/thường, kể cả trùng tên vai trò mặc định) → `MSG21`. Kiểm trước để
  trả lời đẹp, **và** bắt lỗi unique violation (`23505`) của database cho trường hợp hai request
  cùng lúc — database mới là thứ bảo đảm.
- Ghi `roles` và `role_capabilities` trong **một transaction**, đi qua `IAdministratorInvariantGuard`
  (mục 7).

Thành công: `201`, header `Location: /api/roles/{id}`:

```json
{ "code": "MSG19", "message": "Tạo vai trò thành công.", "role": { "id": "…", "name": "…", "description": "…", "isBuiltIn": false, "userCount": 0 } }
```

### 6.3 `PUT /api/roles/{id}`

```json
{ "name": "Thư ký phó", "description": "…" }
```

- Chỉ đổi `name` và `description`. **Không** nhận `capabilities` — quyền vẫn sửa qua
  `PUT /api/roles/capabilities` để chỉ có một đường sửa ma trận, một chỗ canh luật chống tự khoá.
- Vai trò mặc định → `MSG24`.
- Validate `name`/`description` như mục 6.2. Đổi thành tên của chính nó (khác hoa/thường) là hợp lệ.
- Thành công: `200` + `MSG22` + `role`. Gửi đúng giá trị đang có: vẫn `MSG22` nhưng **không** ghi
  audit — cùng quy ước "không đổi gì thì không ghi gì" của `SCRUM-60`.

### 6.4 `DELETE /api/roles/{id}`

- Vai trò mặc định → `MSG24`.
- Còn tài khoản mang vai trò (kể cả đã vô hiệu hoá) → `409` `MSG25`, body kèm `userCount`:

  ```json
  { "code": "MSG25", "message": "Vai trò này vẫn còn 3 tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.", "userCount": 3 }
  ```

- Không tồn tại → `404` không body.
- Thành công: `200` + `MSG23`. `role_capabilities` của vai trò bị xoá theo (`CASCADE`).

### 6.5 Đổi ở endpoint đang có

Đây là **thay đổi phá hợp đồng** — FE sửa cùng lúc (mục 10).

| Endpoint | Trước | Sau |
|---|---|---|
| `GET /api/users`, `GET /api/users/{id}`, response của tạo/sửa | `"role": "Chair"` | `"roleId": "…", "roleName": "Chủ toạ"` |
| `POST /api/users`, `PUT /api/users/{id}` | body `"role": "Chair"` | body `"roleId": "…"`; id không tồn tại → `MSG09`, key `roleId`, `Vai trò không tồn tại.` |
| `/api/auth/login`, `/google`, `/refresh`, `/me` — object `user` | `"role": "Chair"` | `"roleId": "…", "roleName": "Chủ toạ"` |
| `GET /api/roles/capabilities` — mỗi dòng `roles[]` | `{ "role": "Admin", "capabilities": [...], "isBuiltIn": true }` | `{ "id": "…", "name": "Quản trị viên", "description": …, "isBuiltIn": true, "capabilities": [...] }` |
| `PUT /api/roles/capabilities` | body `"role": "Admin"` | body `"roleId": "…"`; id lạ → `MSG17`, key `roleId` |

### 6.6 Mã thông báo (đề xuất — BA chốt số)

Mã cuối đang dùng là `MSG18` (`SCRUM-60`). Đề xuất nối tiếp:

| Mã | HTTP | Khi nào | Message |
|---|---|---|---|
| `MSG19` | `201` | Tạo thành công | `Tạo vai trò thành công.` |
| `MSG20` | `400` | Dữ liệu không hợp lệ — kèm `errors` theo field (`name`, `description`, `capabilities`) | `Thông tin vai trò không hợp lệ.` |
| `MSG21` | `409` | Trùng tên | `Đã có vai trò dùng tên này. Hãy đặt một tên khác.` |
| `MSG22` | `200` | Sửa thành công | `Cập nhật vai trò thành công.` |
| `MSG23` | `200` | Xoá thành công | `Xoá vai trò thành công.` |
| `MSG24` | `409` | Sửa tên hoặc xoá vai trò mặc định | `Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. Bạn vẫn sửa được quyền của nó.` |
| `MSG25` | `409` | Xoá vai trò còn người mang | `Vai trò này vẫn còn {n} tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.` |

Lỗi database khi ghi (mất kết nối, timeout): `500`, xử lý như `MSG18`. BA chốt có dùng chung mã
`MSG18` không.

---

## 7. Luật chống tự khoá và tương tranh

Bất biến của ADR-0007 — **còn ít nhất một tài khoản đang hoạt động giữ `AdminUsers`** — không đổi.
Nhìn lại từng thao tác mới:

| Thao tác | Ảnh hưởng tới bất biến | Làm gì |
|---|---|---|
| Tạo vai trò (kèm quyền) | Chỉ **thêm** quyền, và vai trò mới chưa ai giữ | Không vi phạm được, nhưng vẫn **đi qua `IAdministratorInvariantGuard`** |
| Đổi tên / mô tả | Không chạm quyền, không chạm người | Không cần guard |
| Xoá vai trò | Vai trò không còn ai giữ → không ai mất quyền | Đi qua guard; FK `RESTRICT` chặn trường hợp có người được gán vào chen giữa |

Quy tắc cho gọn và khỏi phải suy luận lại lần sau: **mọi thao tác ghi `role_capabilities` đều đi qua
`IAdministratorInvariantGuard`**. Guard rẻ (một `pg_advisory_xact_lock`), còn "chỗ này chắc không
cần" chính là kiểu suy luận ADR-0007 đã phải sửa sau review.

Giữ nguyên thứ tự của `SCRUM-60`: validate và ghi **trong** transaction, ghi audit, commit, rồi mới
làm việc gì chạm Redis.

## 8. Nhật ký kiểm toán

| `action` | `object_type` | `object_id` | `details` |
|---|---|---|---|
| `role.created` | `Role` | id vai trò mới | `{ "Name": "…", "Description": "…", "Capabilities": [...] }` |
| `role.updated` | `Role` | id vai trò | `{ "Before": { "Name", "Description" }, "After": { "Name", "Description" } }` |
| `role.deleted` | `Role` | id vai trò | `{ "Name": "…", "Capabilities": [...] }` — ghi lại **trước** khi `CASCADE` xoá mất |
| `role.permissions.updated` (đã có) | `Role` | **đổi:** id vai trò, không còn `null` | Thêm `Name` bên cạnh `Before`/`After` |

`actor_id` lấy từ token đã xác thực, như mọi audit khác.

## 9. Đăng nhập và phiên

- **Tạo / đổi tên / xoá không kết thúc phiên của ai.** Tạo: chưa ai giữ. Xoá: không còn ai giữ. Đổi
  tên: không đổi quyền; `/api/auth/me` trả tên mới ở lần FE đọc lại tiếp theo.
- Sửa quyền của vai trò vẫn kết thúc phiên của người giữ nó, như `SCRUM-60`.
- **Nhánh `MSG04` của UC-01 phải quyết lại.** `AuthenticationService` đang từ chối đăng nhập khi
  *"vai trò không có quyền nào và không phải `Member`"*. Với vai trò tự tạo, không có quyền nào là
  cấu hình hợp lệ (giống `Member`), nên luật đó sẽ khoá ngoài những người không làm gì sai.
  **Đề xuất:** bỏ nhánh này — mọi vai trò không có quyền đều đăng nhập được như `Member`. Cần BA sửa
  UC-01 (bỏ `MSG04`) và `01-xac-thuc.md`. Nếu BA muốn giữ, phải có định nghĩa mới không dựa vào
  tên vai trò — ví dụ một cờ `can_sign_in` trên `roles` — và đó là phạm vi thêm, hỏi trước khi làm.

## 10. Phần FE (làm sau khi ticket sang cột Frontend)

Ghi ở đây để BE biết FE cần gì; FE tự lo phần code.

- `types/user.ts`: bỏ union `Role` và bảng `ROLE_LABEL`/`ROLES`; `CurrentUser` và `UserAccount` mang
  `roleId` + `roleName`.
- `api/roles.ts` mới: `listRoles`, `createRole`, `updateRole`, `deleteRole`.
- Màn hình **Vai trò**: nút *Tạo vai trò* mở dialog (tên, mô tả, chọn quyền); mỗi dòng vai trò tự tạo
  có nút sửa và xoá; vai trò mặc định có badge *Mặc định* và không có hai nút đó; hiện số người giữ.
  `MSG25` hiện ngay trong dialog xác nhận xoá, kèm số người.
- Màn hình **Người dùng & Quyền**: bộ lọc vai trò và ô chọn vai trò trong dialog lấy từ
  `GET /api/roles` thay vì danh sách cứng.

## 11. Tài liệu phải cập nhật cùng PR

- File này: viết lại thành tài liệu của cái đã làm; `03-api/README.md` sửa mô tả tương ứng
- [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) → *Đã chấp nhận*, cập nhật bảng ở
  `05-decisions/README.md`
- [02-design/02-erd.md](../02-design/02-erd.md): thêm `Role`, đổi `User.Role` và
  `RoleCapability.Role` thành `RoleId`; dòng "RBAC phẳng đủ cho 4 vai trò" ở bảng *Bỏ hẳn*
- [03-ma-tran-phan-quyen.md](03-ma-tran-phan-quyen.md) và note cho FE: hợp đồng mới (mục 6.5), bỏ câu
  "không thêm được vai trò mới", audit `object_id`
- [02-quan-ly-nguoi-dung.md](02-quan-ly-nguoi-dung.md), [01-xac-thuc.md](01-xac-thuc.md): `roleId` +
  `roleName`; `MSG04` theo quyết định của BA
- [07-classdiagram/README.md](../07-classdiagram/README.md) mục F2: bản vẽ `Role` là entity giờ
  **đúng** — gỡ đề nghị đổi sang enum
- `AGENTS.md`: câu nhắc "4 vai trò" nếu còn

## 12. Test cần có

Theo lối các test đang có: service test với test double, endpoint test in-memory, repository test
với PostgreSQL thật.

**Migration (PostgreSQL thật)** — quan trọng nhất, vì hỏng ở đây là mất dữ liệu:

- DB có đủ bốn vai trò cũ + ma trận mặc định → sau migration mỗi tài khoản có `role_id` đúng, ma trận
  đủ 5 dòng
- Bốn dòng trong `roles` có đúng id của `BuiltInRoles`
- Một test trong `DatabaseModelTests` canh để không ai thêm lại `HasData` cho `roles`

**`RoleService`**

- Tạo: thành công; tên trùng khác hoa/thường → `MSG21`; trùng tên vai trò mặc định → `MSG21`; rỗng /
  toàn khoảng trắng / 65 ký tự → `MSG20`; mã quyền lạ → `MSG20` có nêu tên mã; không có quyền nào →
  thành công
- Sửa: thành công; vai trò mặc định → `MSG24`; trùng tên vai trò khác → `MSG21`; đổi hoa/thường
  chính tên mình → thành công; không đổi gì → không ghi audit
- Xoá: thành công và `role_capabilities` đi theo; vai trò mặc định → `MSG24`; còn một tài khoản
  **đã vô hiệu hoá** → `MSG25`; không tồn tại → `404`
- Xoá chen với gán người (PostgreSQL thật): FK `RESTRICT` → `MSG25`, không phải `500`

**Endpoint**: mỗi endpoint — không token `401`, thiếu `AdminUsers` `403`, id lạ `404`.

**Hồi quy**: `AdministratorInvariantGuardTests`, `RolePermissionServiceTests`, `UserServiceTests`
vẫn xanh sau khi đổi sang `roleId`; thêm một ca gán người vào vai trò tự tạo có `AdminUsers` rồi vô
hiệu hoá quản trị viên cũ — bất biến vẫn tính đúng.

## 13. Câu hỏi mở cho BA

1. Đánh số UC cho tạo / sửa / xoá vai trò, và chốt mã `MSG19`–`MSG25` (mục 6.6).
2. Bỏ `MSG04` của UC-01 hay giữ với định nghĩa mới (mục 9).
3. Vai trò mặc định có cần đổi **mô tả** được không? Thiết kế trên khoá cả tên lẫn mô tả cho gọn.

## 14. Xong là khi

- [ ] Migration chạy trên DB có dữ liệu mà không mất vai trò của ai; có test PostgreSQL thật
- [ ] Bốn endpoint mới, và các endpoint đổi hợp đồng ở mục 6.5
- [ ] Không còn `SystemRole` trong `backend/` và `tests/` (`grep -rn SystemRole` rỗng)
- [ ] Audit cho `role.created` / `role.updated` / `role.deleted`
- [ ] `dotnet build TrustCabinet.sln -warnaserror` và `dotnet test` xanh
- [ ] Tài liệu ở mục 11 đã cập nhật trong **cùng** PR
- [ ] BA đã trả lời mục 13, hoặc câu trả lời đang chờ được ghi rõ trong PR
- [ ] Ticket `SCRUM-63` chuyển sang cột **Frontend**
