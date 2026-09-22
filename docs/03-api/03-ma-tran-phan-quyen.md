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
| `role` | Tên vai trò không có trong enum | `Vai trò không hợp lệ.` |
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

1. `PUT {"role":"Member","capabilities":["AdminUsers"]}` — hợp lệ, `Member` giữ `AdminUsers`
2. `PUT {"role":"Admin","capabilities":[]}` — kiểm theo vai trò sẽ **cho qua**, vì `Member` đang giữ

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

Luật trên vắt qua nhiều dòng, nên đọc–kiểm–ghi phải nằm trong **một transaction** có khoá bảng
(`LOCK TABLE role_capabilities IN EXCLUSIVE MODE`, isolation `Serializable`).

Không có khoá thì hai request đồng thời đều đọc được ma trận cũ, đều thấy "vai trò kia vẫn giữ
`AdminUsers`", đều qua validate, rồi đều ghi — kết quả là không ai giữ nữa:

| | Request A | Request B |
|---|---|---|
| Đọc | Admin và Secretary đều giữ `AdminUsers` | Admin và Secretary đều giữ `AdminUsers` |
| Kiểm | OK, vì Secretary còn giữ | OK, vì Admin còn giữ |
| Ghi | Gỡ khỏi Admin | Gỡ khỏi Secretary |

Khoá ở mức `EXCLUSIVE` chứ không phải `ACCESS EXCLUSIVE`: request `GET` (UC-09) vẫn đọc bình
thường, chỉ request ghi thứ hai phải xếp hàng đợi.

Đã có test với PostgreSQL thật (`RolePermissionRepositoryTests`) chạy hai update đồng thời và
khẳng định **chỉ một cái thành công**. Đã kiểm ngược lại bằng cách bỏ dòng khoá đi: test fail 3/3
lần, tức nó bắt được lỗi thật chứ không phải assert cho có.

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

### `403` ở API, `404` ở route — hai chỗ khác nhau

Tiêu chí nghiệm thu ghi *"route thiếu quyền trả 404"*. Chỗ đó nói về **route phía FE**, không phải
endpoint API:

| Tầng | Trả gì | Vì sao |
|---|---|---|
| Route FE (`/mgmt/roles`) | **404**, giống hệt URL không tồn tại | Màn hình bị hạn chế thì không được lộ ra là nó tồn tại — quy ước của `AGENTS.md`, dùng `RequireCapability` |
| Endpoint API (`/api/roles/capabilities`) | **403** | Giống `/api/users` và `docs/03-api/01-xac-thuc.md`. FE cần phân biệt "hết quyền giữa chừng" với "gọi sai URL" để xử lý đúng |

⚠️ **Phần `404` chưa được hiện thực và chưa có test**, vì màn hình FE chưa làm (xem
[Còn nợ](#còn-nợ)). Người làm FE phải bọc route bằng `RequireCapability` như các màn hình quản trị
khác; đừng nhìn các test đang assert `403` mà kết luận tiêu chí này đã xong — chúng kiểm tầng API,
không phải tầng route.

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

**Chưa đóng được `SCRUM-60`.** Phần backend đã xong, nhưng 2 trong 8 tiêu chí nghiệm thu thuộc về
FE và chưa làm:

| Tiêu chí nghiệm thu | Trạng thái |
|---|---|
| Màn hình ma trận 4×4, bật/tắt từng ô | ❌ **chưa làm** — chưa có page component, chưa có route |
| Route thiếu quyền trả `404` | ❌ **chưa làm** — chưa có route nào để gác |
| Chỉ người có `admin_users` vào được | ✅ ở tầng API |
| Chặn tự khoá, kiểm ở server | ✅ |
| Ghi audit log kèm trước/sau | ✅ |
| Có hiệu lực ngay với phiên đang đăng nhập | ✅ |
| Không tạo được quyền mới | ✅ |
| Quyết `HasData` + viết ADR | ✅ [ADR-0007](../05-decisions/0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) |

[`types/rolePermission.ts`](../../frontend/src/types/rolePermission.ts) và
[`api/rolePermissions.ts`](../../frontend/src/api/rolePermissions.ts) đã sẵn sàng cho FE dùng —
xem thêm [note cho FE](03-ma-tran-phan-quyen-note-cho-fe.md). Hiện hai file đó **chưa có ai
import**, đúng như dự kiến: chúng là hợp đồng để FE bắt đầu, không phải code chết.
- **`ROLE_CAPABILITIES` trong [`frontend/src/types/user.ts`](../../frontend/src/types/user.ts) vẫn
  là bản sao cứng** của ma trận, dùng chữ thường gạch dưới (`admin_users`) khác với backend
  (`AdminUsers`). Đó là scaffolding sẽ bị xoá ở `SCRUM-46`. Từ giờ nó **có thể sai**, vì quản trị
  viên đổi được ma trận thật lúc chạy — đừng dùng nó để quyết định gì ngoài việc dựng giao diện.
