# 03 · Ma trận vai trò – quyền

Hiện thực **UC-10 và UC-11 (SCRUM-60)**: xem ma trận vai trò ↔ quyền, và sửa quyền của từng vai
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

Class diagram: [`feature-03-role-management-class-diagram.drawio.png`](../07-classdiagram/feature-03-role-management-class-diagram.drawio.png).
Tạo / đổi tên / xoá vai trò (`SCRUM-63`): [04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md).

---

## Endpoint

Cả hai đều **yêu cầu quyền `AdminUsers`**, và backend kiểm lại quyền đó ở **từng request** bằng
cách đọc lại tài khoản người gọi từ database — không tin vào token. Lý do ở
[mục bên dưới](#vì-sao-phải-đọc-lại-quyền-ở-từng-request).

| Method | Path | Use case |
|---|---|---|
| `GET` | `/api/roles/capabilities` | UC-10 — xem ma trận |
| `PUT` | `/api/roles/capabilities` | UC-11 — đặt quyền cho một vai trò |

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
    {
      "id": "01a0ce78-7204-7558-b181-e908a08e83a3",
      "name": "Thành viên",
      "description": "Dự họp, đọc tài liệu và biểu quyết.",
      "isBuiltIn": true,
      "capabilities": []
    },
    {
      "id": "01a0ce78-7205-7486-b976-d52d428d5451",
      "name": "Quản trị viên",
      "description": "Quản lý tài khoản người dùng và phân quyền.",
      "isBuiltIn": true,
      "capabilities": ["AdminUsers"]
    }
  ]
}
```

Hai điểm FE cần lưu ý:

- **Mọi vai trò đều xuất hiện**, kể cả vai trò không có quyền nào (`Member`). Dòng rỗng khác với
  dòng thiếu. Thứ tự cố định: bốn vai trò mặc định (`Chair`, `Secretary`, `Member`, `Admin`) rồi vai
  trò tự tạo theo thời gian tạo — cùng thứ tự với `GET /api/roles`.
- **Vai trò định danh bằng `id` (Guid)** từ `SCRUM-63` ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)).
  Tên hiển thị lấy từ `name`, không còn bảng nhãn ở FE.
- **Nhãn cột lấy từ backend**, không hardcode ở FE. Thêm một quyền vào enum mà FE giữ bảng nhãn
  riêng thì cột đó ra màn hình trống trơn.

### `PUT /api/roles/capabilities`

```json
{ "roleId": "01a0ce78-7203-7c7f-8326-7a0baebf4c22", "capabilities": ["MeetingCreator", "MinutesEditor"] }
```

**`capabilities` là trạng thái cuối cùng của vai trò, không phải phần thay đổi.** Gửi đủ mọi quyền
vai trò đó cần có *sau* khi sửa, kể cả những quyền nó đã có sẵn. Gửi mảng rỗng là hợp lệ và sẽ gỡ
sạch quyền của vai trò.

Chọn PUT một dòng thay vì PUT cả ma trận: màn hình bật/tắt từng ô, và PUT cả ma trận sẽ khiến hai
quản trị viên sửa hai dòng khác nhau cùng lúc ghi đè lẫn nhau.

> Lưu ý: PUT từng dòng chỉ tránh được việc **ghi đè dòng của nhau**. Nó **không** tự bảo vệ luật
> chống tự khoá bên dưới, vì luật đó vắt qua nhiều dòng — hai request hợp lệ độc lập vẫn cộng lại
> thành một ma trận không ai quản trị được. Chỗ đó do transaction + khoá bảng lo, xem
> [mục tương tranh](#hai-quản-trị-viên-sửa-cùng-lúc).

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

`MSG17` có ba nguyên nhân, phân biệt bằng key trong `errors` — **mỗi nguyên nhân một key riêng**,
để màn hình hiển thị đúng thông báo cho từng trường hợp:

| Key | Nguyên nhân | Message mẫu |
|---|---|---|
| `roleId` | Không có vai trò nào mang id này (thiếu, không phải Guid, hoặc đã bị xoá) | `Vai trò không hợp lệ.` |
| `capabilities` | Có mã quyền không tồn tại, hoặc thiếu hẳn field `capabilities` | `Quyền không hợp lệ: DeleteEverything.` — **có nêu tên mã sai**, kể cả khi sai nhiều mã |
| `adminUsers` | Vi phạm luật chống tự khoá bên dưới | `Không thể lưu cấu hình này: sau khi lưu sẽ không còn tài khoản đang hoạt động nào giữ quyền quản trị người dùng…` |

`adminUsers` tách riêng khỏi `capabilities` là có chủ đích: "bạn gõ sai tên quyền" và "thao tác này
sẽ khoá tất cả mọi người ra ngoài" là hai chuyện rất khác nhau, mà cái thứ hai là thông báo quản
trị viên cần đọc đúng nhất.

---

## Luật: phải còn ít nhất một **tài khoản đang hoạt động** giữ `AdminUsers`

`AdminUsers` chính là quyền mở màn hình này. Nếu sau khi lưu mà không còn ai vào được, thì không
sửa lại được bằng ứng dụng — chỉ còn cách `INSERT` thẳng vào database.

Nên backend từ chối request nào dẫn tới trạng thái đó, với `MSG17`, key lỗi `adminUsers`.

**Đếm theo tài khoản đang hoạt động, không phải theo vai trò.** Đây là điểm dễ làm sai nhất và đã
từng sai ở bản đầu: kiểm "còn vai trò nào giữ `AdminUsers` không" **không đủ**. Hai request hợp lệ
là khoá được hệ thống:

1. `PUT {"roleId":"<Member>","capabilities":["AdminUsers"]}` — hợp lệ, `Member` giữ `AdminUsers`
2. `PUT {"roleId":"<Admin>","capabilities":[]}` — kiểm theo vai trò sẽ **cho qua**, vì `Member` đang giữ

Nếu không tài khoản nào mang vai trò `Member` thì xong: quyền nằm ở một vai trò không ai có, không
ai quản trị được nữa. Nên luật đếm **số tài khoản đang hoạt động** sẽ giữ `AdminUsers` sau khi
lưu, và từ chối khi con số đó bằng 0.

Luật này khớp với luật bên `UserService` (không được vô hiệu hoá / hạ vai trò quản trị viên cuối
cùng) — cả hai cùng đếm theo tài khoản. Hai luật **phải** thống nhất, vì hệ thống sẽ bị khoá qua
cái nào lỏng hơn.

**Vẫn chuyển quyền sang vai trò khác được.** Lý do bảng `role_capabilities` tồn tại là vậy. Chuyển
`AdminUsers` sang `Secretary` rồi gỡ khỏi `Admin` là hợp lệ — **miễn là có tài khoản đang hoạt động
mang vai trò `Secretary`**.

**Kiểm ở server.** Tiêu chí nghiệm thu ghi rõ *"Kiểm ở server, không phải ẩn nút"* — FE cứ ẩn/khoá
ô checkbox cho dễ dùng, nhưng đó là tiện lợi, không phải bảo vệ.

## Hai quản trị viên sửa cùng lúc

Luật trên vắt qua nhiều dòng, nên đọc–kiểm–ghi phải nằm trong **một transaction** có khoá.

Không có khoá thì hai request đồng thời đều đọc được ma trận cũ, đều thấy "vai trò kia vẫn giữ
`AdminUsers`", đều qua validate, rồi đều ghi — kết quả là không ai giữ nữa:

| | Request A | Request B |
|---|---|---|
| Đọc | Admin và Secretary đều giữ `AdminUsers` | Admin và Secretary đều giữ `AdminUsers` |
| Kiểm | OK, vì Secretary còn giữ | OK, vì Admin còn giữ |
| Ghi | Gỡ khỏi Admin | Gỡ khỏi Secretary |

**Khoá dùng chung với `/api/users`, không phải khoá riêng bảng `role_capabilities`.** Bất biến này
do **hai bảng** quyết định: `role_capabilities` nói vai trò nào mang `AdminUsers`, `users` nói ai
đang hoạt động và mang vai trò gì. Vô hiệu hoá quản trị viên cuối cùng qua
`PUT /api/users/{id}/status` gỡ mất một người giữ quyền mà **không đụng gì tới**
`role_capabilities` — khoá riêng bảng này sẽ không bao giờ chặn được đường đó.

Nên cả hai phía cùng xếp hàng sau một advisory lock:

```sql
SELECT pg_advisory_xact_lock(hashtext('admin-users-invariant'))
```

gọi ở `RolePermissionService.UpdateAsync`, `UserService.UpdateAsync` và
`UserService.ChangeStatusAsync`. Request `GET` (UC-10) không lấy khoá nên vẫn đọc bình thường; chỉ
request ghi thứ hai phải đợi. Chi tiết và lý do chọn isolation `Read Committed` ở
[ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md).

Đã có test với PostgreSQL thật chạy hai update đồng thời và khẳng định **chỉ một cái thành công**
(`RolePermissionRepositoryTests`), cộng với `AdministratorInvariantGuardTests` cho chính cái khoá.
Đã kiểm ngược lại bằng cách bỏ dòng khoá đi: test fail, tức nó bắt được lỗi thật chứ không phải
assert cho có.

---

## Thay đổi có hiệu lực ngay, không chờ token hết hạn

Capability nằm **bên trong access token** lúc phát. Nên khi ma trận đổi, người đang giữ vai trò đó
vẫn cầm token mang bộ quyền cũ cho tới khi token hết hạn (tối đa
`Authentication:AccessTokenMinutes`, mặc định 15 phút).

Ticket yêu cầu *"có hiệu lực ngay với phiên đang đăng nhập, hoặc nói rõ là phải đăng nhập lại"*.
Code làm cho thay đổi **có hiệu lực ngay, không đăng xuất ai** (SCRUM-105):

1. **Server đọc lại quyền từ database ở từng request** (`CapabilityGate`, xem mục kế tiếp). Quyền
   vừa bị gỡ bị từ chối (`403`) ngay ở request kế tiếp, kể cả khi người đó còn cầm access token
   cũ. `/api/auth/refresh` cũng đọc lại quyền, nên token mới luôn mang đúng bộ quyền hiện tại.
2. **Báo cho mọi tài khoản đang hoạt động thuộc vai trò đó** qua sự kiện `capabilitiesChanged`
   trên `/hubs/notifications` ([06-thong-bao-trong-ung-dung.md](06-thong-bao-trong-ung-dung.md)).
   Trình duyệt nhận được thì refresh token ngay, nên menu và route đổi theo quyền mới mà không cần
   đăng nhập lại.
   - Báo ở **cả hai chiều**. Người vừa được thêm quyền đang cầm token **không** chứa quyền đó, nên
     chưa dùng được cho tới khi refresh. Người vừa bị gỡ quyền thì cần menu bỏ mục đó đi.
   - **Gửi sau khi transaction commit, không nằm trong đó.** Báo trước khi commit thì trình duyệt
     có thể refresh, đọc phải ma trận cũ, rồi không còn sự kiện nào để chờ nữa. Gửi lỗi thì ghi log
     rồi bỏ qua: cấu hình đã lưu đúng, và server vẫn chặn đúng. Cái mất chỉ là trình duyệt nhận quyền
     mới muộn hơn: lúc tab được mở lại, lúc gặp `403`, hoặc lúc token sắp hết hạn.

**Trước SCRUM-105**, backend huỷ refresh token của mọi tài khoản thuộc vai trò, nên ai đang đăng
nhập cũng bị đăng xuất khi quay lại tab. Việc huỷ đó không thêm lớp bảo vệ nào: huỷ refresh token
không làm access token đang dùng mất hiệu lực, còn quyền bị gỡ thì bước 1 đã chặn. Đổi vai trò hoặc
email của **một người** (UC-06b), vô hiệu hoá tài khoản và đặt lại mật khẩu thì **vẫn** huỷ phiên
như cũ.

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

### `403` ở API, `404` ở route — hai chỗ khác nhau

Tiêu chí nghiệm thu ghi *"route thiếu quyền trả 404"*. Chỗ đó nói về **route phía FE**, không phải
endpoint API:

| Tầng | Trả gì | Vì sao |
|---|---|---|
| Route FE (`/mgmt/admin/roles`) | **404**, giống hệt URL không tồn tại | Màn hình bị hạn chế thì không được lộ ra là nó tồn tại — quy ước của `AGENTS.md`, dùng `RequireCapability` |
| Endpoint API (`/api/roles/capabilities`) | **403** | Giống `/api/users` và `docs/03-api/01-xac-thuc.md`. FE cần phân biệt "hết quyền giữa chừng" với "gọi sai URL" để xử lý đúng |

Route `/mgmt/admin/roles` hiện đã được bọc bằng `RequireCapability cap="AdminUsers"` trong FE;
route ẩn quyền trả `404`. Đây là quy tắc riêng của route UI. API `/api/roles/capabilities` vẫn trả
`403` khi người gọi đã đăng nhập nhưng thiếu quyền.

---

## Ghi nhật ký kiểm toán

Mỗi lần đổi thành công ghi một dòng vào `audit_log`:

| Cột | Giá trị |
|---|---|
| `action` | `role.permissions.updated` |
| `object_type` | `Role` |
| `object_id` | Id vai trò (từ `SCRUM-63`; trước đó là `null` vì vai trò còn là enum) |
| `actor_id` | Quản trị viên thực hiện, lấy từ token đã xác thực |
| `details` | `{ "Name": "...", "Before": [...], "After": [...] }` — `Name` là tên vai trò **lúc đó**, vì tên đổi được |

Có đủ **giá trị trước và sau**, theo đúng tiêu chí nghiệm thu.

**Request không đổi gì thì không ghi gì.** Gửi lên đúng bộ quyền vai trò đang có sẽ trả `MSG16`
(hệ thống đang ở đúng trạng thái được yêu cầu) nhưng không ghi audit log và không gửi
`capabilitiesChanged` cho ai — không có thay đổi nào để mà ghi.

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
- Không có sequence diagram cho nó (chỉ có UC-10 và UC-11).

Đã thống nhất **không làm** trong sprint này.

**→ Đề nghị BA:** bỏ khỏi bản vẽ, hoặc đánh dấu `<<chưa implement>>` như cách đã làm với UC-07 ở
bản vẽ 02.

### 3. `Role` không phải entity có `roleId: UUID`

Bản vẽ vẽ `Role` là `<<entity>>` với `roleId: UUID`, `roleName`, `description`, `isBuiltIn`.

> **Đã giải quyết — code giờ khớp bản vẽ.** Trước `SCRUM-63` code dùng enum `SystemRole`; từ
> [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) vai trò là bảng `roles` khoá `Guid` với
> `name`, `description`, `isBuiltIn` — đúng `<<entity>> Role` của bản vẽ. BA không cần sửa gì ở
> mục này.

### 4. UC-11 có thêm nhánh "không đổi gì"

Bản vẽ UC-11 chỉ có hai nhánh: hợp lệ và không hợp lệ. Code có thêm một đường: request trùng khớp
trạng thái hiện tại thì trả `MSG16` nhưng **không ghi audit log và không báo cho ai**.

Không phải tối ưu vặt: ghi một dòng audit "đã đổi quyền" cho một lần không đổi gì sẽ làm bẩn đúng
thứ bằng chứng mà `SCRUM-16` cần chính xác, và sẽ bắt cả một nhóm người refresh token mà không vì
lý do gì.

### 5. `getPermissionCatalog()` gộp vào `GET /api/roles/capabilities`

Bản vẽ tách `getRolePermissionMatrix()` và `getPermissionCatalog()` thành hai method. Code trả cả
hai trong **một** response.

Màn hình luôn cần cả hai cùng lúc để vẽ lưới, nên tách thành hai request chỉ tạo thêm một vòng
round trip và một khoảng thời gian hai nửa dữ liệu lệch nhau. Về mặt phân tầng thì không khác gì:
service vẫn có hai đường đọc riêng.

---

## Còn nợ

Cả 8 tiêu chí nghiệm thu đã có code; phần FE nằm ở
[`frontend/src/features/access/RolesPage.tsx`](../../frontend/src/features/access/RolesPage.tsx):

| Tiêu chí nghiệm thu | Trạng thái |
|---|---|
| Màn hình ma trận vai trò × capability, bật/tắt từng ô | ✅ danh sách vai trò và capability lấy từ API; sửa nháp theo từng vai trò, lưu từng vai trò bằng một `PUT` |
| Route thiếu quyền trả `404` | ✅ `/mgmt/admin/roles` bọc `RequireCapability cap="AdminUsers"` |
| Chỉ người có `admin_users` vào được | ✅ ở tầng API |
| Chặn tự khoá, kiểm ở server | ✅ |
| Ghi audit log kèm trước/sau | ✅ |
| Có hiệu lực ngay với phiên đang đăng nhập | ✅ |
| Không tạo được quyền mới | ✅ |
| Quyết `HasData` + viết ADR | ✅ [ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) |

`ROLE_CAPABILITIES` (bản sao cứng của ma trận trong `frontend/src/types/user.ts`) **đã bị xoá**;
FE dùng mã PascalCase của backend và lấy `capabilities` của người dùng từ response đăng nhập.
