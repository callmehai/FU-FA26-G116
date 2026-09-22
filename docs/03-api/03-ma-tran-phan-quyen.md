# 03 · Ma trận vai trò – quyền

Hiện thực **UC-09 và UC-10 (SCRUM-60)**: xem ma trận vai trò ↔ quyền, và sửa quyền của từng vai
trò mà không phải deploy lại.

Sequence diagram chia bốn participant: `Role & Permission Management Page` →
`:Role Permission Controller` → `:Role Permission Service` → `:Role Permission Repository` →
`Database`, cộng thêm `:Audit Service`. Code đi theo đúng các lớp đó:

| Participant trong diagram | Code |
|---|---|
| `:Role Permission Controller` | [`backend/TrustCabinet.Web/Endpoints/RolePermissionEndpoints.cs`](../../backend/TrustCabinet.Web/Endpoints/RolePermissionEndpoints.cs) |
| `:Role Permission Service` | [`backend/TrustCabinet.Infrastructure/Access/RolePermissionService.cs`](../../backend/TrustCabinet.Infrastructure/Access/RolePermissionService.cs) |
| `:Role Permission Repository` | [`backend/TrustCabinet.Infrastructure/Access/RolePermissionRepository.cs`](../../backend/TrustCabinet.Infrastructure/Access/RolePermissionRepository.cs) |
| `:Audit Service` | [`backend/TrustCabinet.Infrastructure/Audit/AuditService.cs`](../../backend/TrustCabinet.Infrastructure/Audit/AuditService.cs) |
| `Database` | bảng `role_capabilities` và `audit_log` |

Class diagram: [`feature-03-role-permission-management-class-diagram.drawio.png`](../07-classdiagram/feature-03-role-permission-management-class-diagram.drawio.png).

---

## Endpoint

Cả hai đều **yêu cầu quyền `AdminUsers`**, và backend kiểm lại quyền đó ở **từng request** bằng
cách đọc lại tài khoản người gọi từ database — không tin vào token. Lý do ở
[mục bên dưới](#vì-sao-phải-đọc-lại-quyền-ở-từng-request).

| Method | Path | Use case |
|---|---|---|
| `GET` | `/api/roles/capabilities` | UC-09 — xem ma trận |
| `PUT` | `/api/roles/capabilities` | UC-10 — đặt quyền cho một vai trò |

### `GET /api/roles/capabilities`

Trả về cả **cột** (danh mục quyền) và **dòng** (quyền của từng vai trò), vì màn hình vẽ một lưới:

```json
{
  "capabilities": [
    {
      "code": "MeetingCreator",
      "name": "Tạo cuộc họp",
      "description": "Tạo cuộc họp mới và gửi lời mời cho người tham dự.",
      "functionalGroup": "Cuộc họp"
    }
  ],
  "roles": [
    { "role": "Admin", "capabilities": ["AdminUsers"], "isBuiltIn": true },
    { "role": "Member", "capabilities": [], "isBuiltIn": true }
  ]
}
```

Hai điểm FE cần lưu ý:

- **Mọi vai trò đều xuất hiện**, kể cả vai trò không có quyền nào (`Member`). Dòng rỗng khác với
  dòng thiếu.
- **Nhãn cột lấy từ backend**, không hardcode ở FE. Thêm một quyền vào enum mà FE giữ bảng nhãn
  riêng thì cột đó ra màn hình trống trơn.

### `PUT /api/roles/capabilities`

```json
{ "role": "Secretary", "capabilities": ["MeetingCreator", "MinutesEditor"] }
```

**`capabilities` là trạng thái cuối cùng của vai trò, không phải phần thay đổi.** Gửi đủ mọi quyền
vai trò đó cần có *sau* khi sửa, kể cả những quyền nó đã có sẵn. Gửi mảng rỗng là hợp lệ và sẽ gỡ
sạch quyền của vai trò.

Chọn PUT một dòng thay vì PUT cả ma trận: màn hình bật/tắt từng ô, và PUT cả ma trận sẽ khiến hai
quản trị viên sửa hai dòng khác nhau cùng lúc ghi đè lẫn nhau.

Thành công trả `200` kèm ma trận mới, nên màn hình không cần gọi `GET` lần hai:

```json
{ "code": "MSG16", "message": "Cập nhật phân quyền thành công.", "matrix": { } }
```

### Mã thông báo

| Mã | HTTP | Khi nào |
|---|---|---|
| `MSG16` | `200` | Cập nhật thành công |
| `MSG17` | `400` | Cấu hình không hợp lệ — kèm `errors` theo từng field |
| `MSG18` | `500` | Database từ chối ghi. Không phải lỗi của người dùng: nên thử lại |

`MSG17` có ba nguyên nhân, phân biệt bằng key trong `errors`:

| Key | Nguyên nhân |
|---|---|
| `role` | Tên vai trò không có trong enum |
| `capabilities` | Có mã quyền không tồn tại, **hoặc** thiếu hẳn field `capabilities`, **hoặc** vi phạm luật chống tự khoá bên dưới |

---

## Luật: không được gỡ `AdminUsers` khỏi vai trò cuối cùng còn giữ nó

`AdminUsers` chính là quyền mở màn hình này. Một ma trận mà **không vai trò nào** giữ nó thì không
sửa lại được bằng ứng dụng — chỉ còn cách vào thẳng database.

Nên backend từ chối request nào dẫn tới trạng thái đó, với `MSG17`.

**Kiểm trên toàn ma trận, không phải "Admin luôn phải giữ AdminUsers".** Lý do bảng
`role_capabilities` tồn tại là để chuyển quyền sang vai trò khác được. Chuyển `AdminUsers` sang
`Secretary` rồi gỡ khỏi `Admin` là hợp lệ; để nó không ở đâu cả mới là không hợp lệ.

**Kiểm ở server.** Tiêu chí nghiệm thu ghi rõ *"Kiểm ở server, không phải ẩn nút"* — FE cứ ẩn/khoá
ô checkbox cho dễ dùng, nhưng đó là tiện lợi, không phải bảo vệ.

---

## Thay đổi có hiệu lực ngay, không chờ token hết hạn

Capability nằm **bên trong access token** lúc phát. Nên khi ma trận đổi, người đang giữ vai trò đó
vẫn cầm token mang bộ quyền cũ cho tới khi token hết hạn (tối đa
`Authentication:AccessTokenMinutes`, mặc định 15 phút).

Ticket yêu cầu *"có hiệu lực ngay với phiên đang đăng nhập, hoặc nói rõ là phải đăng nhập lại"*.
Code làm **cả hai**:

1. **Huỷ refresh token của mọi tài khoản đang giữ vai trò đó.** Phiên kết thúc ở lần refresh kế
   tiếp thay vì chờ hết hạn tự nhiên. Giống hệt cách UC-06 xử lý khi đổi vai trò của một người.
   - Huỷ ở **cả hai chiều** — thêm quyền cũng huỷ, không riêng gỡ quyền. Người vừa được cấp thêm
     quyền đang cầm token **không** chứa quyền đó, nên quyền mới cũng chưa dùng được.
   - Tài khoản đã bị vô hiệu hoá thì bỏ qua: họ không có phiên nào dùng được để mà kết thúc.
2. **Các endpoint quản trị đọc lại quyền từ database ở từng request**, nên đường nguy hiểm nhất
   đóng ngay lập tức — xem mục kế tiếp.

### Vì sao phải đọc lại quyền ở từng request

`/api/users` và `/api/roles/capabilities` dùng chung
[`AdminCallerRevalidation`](../../backend/TrustCabinet.Web/Endpoints/AdminCallerRevalidation.cs):
sau khi policy kiểm token, filter đọc lại tài khoản người gọi từ database và từ chối nếu họ đã bị
vô hiệu hoá hoặc vai trò của họ không còn `AdminUsers`.

Không có bước này thì SCRUM-60 mở ra một lỗ: quản trị viên A gỡ `AdminUsers` khỏi vai trò của B,
nhưng token của B vẫn còn hiệu lực tới 15 phút — đủ để B gỡ lại quyền của A.

Trả `403` chứ không phải `401`: token thật và chưa hết hạn, chỉ là tài khoản đằng sau nó không còn
được làm việc này nữa. **FE gặp `403` giữa phiên là chuyện bình thường**, không phải bug — xử lý
như [note của SCRUM-13](02-quan-ly-nguoi-dung-note-cho-fe.md).

---

## Ghi nhật ký kiểm toán

Mỗi lần đổi thành công ghi một dòng vào `audit_log`:

| Cột | Giá trị |
|---|---|
| `action` | `role.permissions.updated` |
| `object_type` | `Role` |
| `object_id` | `null` — vai trò là giá trị enum, không phải `Guid`; tên vai trò nằm trong `details` |
| `actor_id` | Quản trị viên thực hiện, lấy từ token đã xác thực |
| `details` | `{ "Role": "...", "Before": [...], "After": [...] }` |

Có đủ **giá trị trước và sau**, theo đúng tiêu chí nghiệm thu.

**Request không đổi gì thì không ghi gì.** Gửi lên đúng bộ quyền vai trò đang có sẽ trả `MSG16`
(hệ thống đang ở đúng trạng thái được yêu cầu) nhưng không ghi audit log và không kết thúc phiên
của ai — không có thay đổi nào để mà ghi.

---

## Không tạo được quyền mới

`Capability` là **enum trong code**, không phải bảng. Quản trị viên sửa *mapping*, không thêm
quyền. Màn hình **không được có** nút "Tạo quyền mới".

Lý do không phải là ngại làm: một quyền chỉ có ý nghĩa vì có code kiểm nó. Một dòng quyền do quản
trị viên tự thêm sẽ không gác cửa cái gì cả — nó trông như bảo mật mà không phải bảo mật.

Danh mục quyền đọc từ enum qua
[`PermissionCatalog`](../../backend/TrustCabinet.Domain/Access/PermissionCatalog.cs). Thêm giá trị
vào enum mà quên mô tả thì `PermissionCatalog` ném lỗi ngay, thay vì âm thầm để lọt một cột trống
ra màn hình quản trị viên.

---

## Thay đổi database

**Không thêm bảng, không đổi cột.** Nhưng có một thay đổi quan trọng về **nguồn dữ liệu** của
`role_capabilities`, ghi ở
[ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md):

Trước đây model khai báo các dòng mặc định bằng `HasData`. Không dùng được nữa, vì EF so seed data
với **migration trước** chứ không so với database — nên migration sau sẽ sinh `UPDATE`/`DELETE` đè
lên đúng những dòng quản trị viên đã sửa trên production.

Từ nay `ReferenceData.RoleCapabilities` là **giá trị khởi tạo**, không còn là sự thật của hệ thống
đang chạy. Muốn biết một deployment đang thực thi gì thì đọc bảng.

**Migration `RoleCapabilitiesEditable` cố ý để trống.** EF scaffold ra 5 lệnh `DeleteData`; đã xoá
bằng tay, vì chạy chúng sẽ xoá ma trận khỏi hệ thống đang chạy. Đã kiểm thực tế trên một database
có dữ liệu quản trị viên đã chỉnh: sau khi apply migration, dữ liệu **giữ nguyên không đổi**.

**Database migrate từ đầu vẫn có sẵn ma trận** — migration `InitialCreate` có `InsertData` chèn 5
dòng đó và đã lên `main` nên không sửa được nữa. Luồng thông thường không phải làm gì thêm.

[`ReferenceDataSeeder`](../../backend/TrustCabinet.Infrastructure/Seed/ReferenceDataSeeder.cs) là
**lưới an toàn** cho trường hợp bảng rỗng (restore một bản dump không kèm dữ liệu đó, hoặc dựng
schema bằng cách khác), bật bằng `Database:SeedReferenceDataOnStartup`. Nó chỉ chèn khi bảng rỗng
hoàn toàn nên không bao giờ đè lên chỉnh sửa của quản trị viên.

> ⚠️ Bảng `role_capabilities` rỗng nghĩa là **không vai trò nào có `AdminUsers`** → không ai đăng
> nhập vào sửa được, chỉ còn cách `INSERT` thẳng vào database.

---

## Những chỗ code khác bản vẽ

Bản vẽ là thiết kế nghiệp vụ, không phải đặc tả kỹ thuật đầy đủ. Liệt kê đủ ở đây là cố ý: người
đối chiếu code với bản vẽ mà gặp một chỗ lệch không được khai báo thì không phân biệt được "cố
tình làm tốt hơn" với "làm sót".

### 1. `Permission` **không phải** entity, và không có bảng `permissions`

Class diagram vẽ `Permission` là `<<entity>>` với `permissionId: UUID`, `code`, `name`,
`description`, `functionalGroup` — tức một bảng riêng.

Code **không tạo bảng đó**. `Capability` là enum, và `PermissionDescriptor` là value object đọc từ
enum ra. Không có `permissionId`: **tên enum chính là định danh**, và đó cũng là thứ
`role_capabilities` lưu.

Lý do là tiêu chí nghiệm thu của chính ticket: *"Không tạo được quyền mới: `Capability` là enum
trong code"*. Bản vẽ và tiêu chí nghiệm thu đang mâu thuẫn nhau; đã chọn theo tiêu chí nghiệm thu.

**→ Đề nghị BA sửa bản vẽ:** đổi `<<entity>> Permission` thành `<<value object>>`, bỏ
`permissionId`, ghi chú rằng danh mục đọc từ enum.

### 2. `resetRolePermissionsToDefault()` **chưa làm**

Bản vẽ có method này ở cả 4 tầng (`Controller`, `Service`, `Repository`, và
`confirmResetToDefault()` ở `Page`). Nhưng:

- Ticket không nhắc tới nó ở bất kỳ tiêu chí nghiệm thu nào,
- Không có sequence diagram cho nó (chỉ có UC-09 và UC-10).

Đã thống nhất **không làm** trong sprint này.

**→ Đề nghị BA:** bỏ khỏi bản vẽ, hoặc đánh dấu `<<chưa implement>>` như cách đã làm với UC-08 ở
bản vẽ 02.

### 3. `Role` không phải entity có `roleId: UUID`

Bản vẽ vẽ `Role` là `<<entity>>` với `roleId: UUID`, `roleName`, `description`, `isBuiltIn`.

Code dùng enum `SystemRole` bốn giá trị (`Chair`, `Secretary`, `Member`, `Admin`) theo
[ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md) — không có bảng `roles`, không thêm
được vai trò mới mà không deploy.

`isBuiltIn` vẫn có trên API (luôn `true`) vì bản vẽ yêu cầu và vì màn hình cần lý do để hiển thị
tên vai trò dạng chữ cố định thay vì ô sửa được.

**→ Đề nghị BA:** tương tự mục 1 — ghi rõ `Role` là enum, hoặc thêm ghi chú
`<<enum, không phải bảng>>`.

### 4. UC-10 có thêm nhánh "không đổi gì"

Bản vẽ UC-10 chỉ có hai nhánh: hợp lệ và không hợp lệ. Code có thêm một đường: request trùng khớp
trạng thái hiện tại thì trả `MSG16` nhưng **không ghi audit log và không huỷ phiên của ai**.

Không phải tối ưu vặt: ghi một dòng audit "đã đổi quyền" cho một lần không đổi gì sẽ làm bẩn đúng
thứ bằng chứng mà `SCRUM-16` cần chính xác, và sẽ đá văng phiên của cả một nhóm người mà không vì
lý do gì.

### 5. `getPermissionCatalog()` gộp vào `GET /api/roles/capabilities`

Bản vẽ tách `getRolePermissionMatrix()` và `getPermissionCatalog()` thành hai method. Code trả cả
hai trong **một** response.

Màn hình luôn cần cả hai cùng lúc để vẽ lưới, nên tách thành hai request chỉ tạo thêm một vòng
round trip và một khoảng thời gian hai nửa dữ liệu lệch nhau. Về mặt phân tầng thì không khác gì:
service vẫn có hai đường đọc riêng.

---

## Còn nợ

- **Màn hình FE chưa làm.** Ticket đang ở trạng thái `Backend`; phần
  [`types/rolePermission.ts`](../../frontend/src/types/rolePermission.ts) và
  [`api/rolePermissions.ts`](../../frontend/src/api/rolePermissions.ts) đã sẵn sàng cho FE dùng.
- **`ROLE_CAPABILITIES` trong [`frontend/src/types/user.ts`](../../frontend/src/types/user.ts) vẫn
  là bản sao cứng** của ma trận, dùng chữ thường gạch dưới (`admin_users`) khác với backend
  (`AdminUsers`). Đó là scaffolding sẽ bị xoá ở `SCRUM-46`. Từ giờ nó **có thể sai**, vì quản trị
  viên đổi được ma trận thật lúc chạy — đừng dùng nó để quyết định gì ngoài việc dựng giao diện.
