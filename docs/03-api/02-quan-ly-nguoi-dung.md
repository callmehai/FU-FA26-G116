# 02 · Quản lý người dùng

Hiện thực **UC-04, UC-05, UC-06 và UC-07 (SCRUM-13)**: xem danh sách, tạo, sửa và vô hiệu hoá /
kích hoạt lại tài khoản người dùng; **UC-08** đặt lại mật khẩu qua email (dùng hạ tầng SMTP của
SCRUM-15); và **UC-09** gán / đổi vai trò hệ thống — chạy trên chính endpoint sửa tài khoản của UC-06,
xem [mục UC-09](#uc-09--gán--đổi-vai-trò-hệ-thống--dùng-put-apiusersid).

Kịch bản kiểm thử API bằng Postman cho UC-08 và UC-09: [postman/](postman/README.md).

Sequence diagram chia bốn participant: `User Management Page` → `:User Controller` →
`:User Service` → `:User Repository` → `Database`, cộng thêm `:Audit Service` và `:Token
Service`. Code đi theo đúng các lớp đó:

Bản vẽ từng use case:

| Use case | Sequence diagram |
|---|---|
| UC-04 — Xem danh sách tài khoản | [UC04_ViewUserAccount.png](../06-sequencediagram/user-management/UC04_ViewUserAccount.png) |
| UC-05 — Tạo tài khoản | [UC05_CreateUserAccount.png](../06-sequencediagram/user-management/UC05_CreateUserAccount.png) |
| UC-06 — Sửa tài khoản | [UC06_UpdateUserAccount.png](../06-sequencediagram/user-management/UC06_UpdateUserAccount.png) |
| UC-07 — Vô hiệu hoá / kích hoạt lại | [UC07_DeactivateReactivateUserAccount.png](../06-sequencediagram/user-management/UC07_DeactivateReactivateUserAccount.png) |
| UC-08 — Đặt lại mật khẩu | [UC08_ResetUserPassword.png](../06-sequencediagram/user-management/UC08_ResetUserPassword.png) — chỉ vẽ nửa quản trị viên gửi liên kết |
| UC-09 — Gán / đổi vai trò | *chưa có* — đặc tả đang trỏ nhầm sang sơ đồ UC-11 |

| Participant trong diagram | Code |
|---|---|
| `:User Controller` | [`backend/TrustCabinet.Web/Endpoints/UserEndpoints.cs`](../../backend/TrustCabinet.Web/Endpoints/UserEndpoints.cs) |
| `:User Service` | [`backend/TrustCabinet.Infrastructure/Users/UserService.cs`](../../backend/TrustCabinet.Infrastructure/Users/UserService.cs) |
| `:User Repository` | [`backend/TrustCabinet.Infrastructure/Users/UserAccountRepository.cs`](../../backend/TrustCabinet.Infrastructure/Users/UserAccountRepository.cs) |
| `:Audit Service` | [`backend/TrustCabinet.Infrastructure/Audit/AuditService.cs`](../../backend/TrustCabinet.Infrastructure/Audit/AuditService.cs) |
| `:Token Service` | `IRefreshTokenStore.RevokeAllForUserAsync` — bản vẽ chỉ có ở UC-07, code dùng cả ở UC-06 (xem mục 4 bên dưới). Chi tiết: [Vô hiệu hoá phải kết thúc phiên](#vô-hiệu-hoá-phải-kết-thúc-phiên). Bản vẽ trước đây đặt tên là `:Session Service`; đổi thành `:Token Service` vì hệ thống dùng JWT chứ không có session phía server — xem [07-classdiagram/README.md](../07-classdiagram/README.md#phụ-lục--có-dùng-jwt-không-mà-sao-lại-có-session-server) |
| `:Authentication Service` (UC-08 — "Create password reset request") | [`IPasswordResetTokenStore`](../../backend/TrustCabinet.Domain/Authentication/IPasswordResetTokenStore.cs) → [`RedisPasswordResetTokenStore`](../../backend/TrustCabinet.Infrastructure/Authentication/RedisPasswordResetTokenStore.cs) |
| `:Email Service` (UC-08) | [`IPasswordResetEmailSender`](../../backend/TrustCabinet.Domain/Users/IPasswordResetEmailSender.cs) → [`PasswordResetEmailSender`](../../backend/TrustCabinet.Infrastructure/Email/PasswordResetEmailSender.cs) |
| `Database` | bảng `users` và `audit_log`; token đặt lại mật khẩu nằm ở Redis |

### Những chỗ code khác bản vẽ

Bản vẽ là thiết kế nghiệp vụ, không phải đặc tả kỹ thuật đầy đủ. Những chỗ sau code làm khác, đều có
lý do. Liệt kê đủ ở đây là cố ý: người đối chiếu code với bản vẽ mà gặp một chỗ lệch không được
khai báo thì không có cách nào phân biệt "cố tình làm tốt hơn" với "làm sót".

1. **`:User Repository` không kiểm "Username"** (UC-05 vẽ "Check Username and Email availability").
   Bảng `users` **không có cột username** (xem `Domain/Entities/User.cs`, `Email` chính là định
   danh đăng nhập); theo [ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md) đăng nhập
   bằng email hoặc Google. Chỉ kiểm email.
2. **UC-05 kiểm email *sau* khi validate, không phải trước.** Bản vẽ đặt bước "Check … availability"
   **ngoài và trên** khối `alt`, tức là luôn hỏi repository rồi mới rẽ nhánh. Code validate trước,
   có field sai thì trả `MSG09` luôn và **chưa** gọi `EmailExistsAsync`. Hệ quả quan sát được: form
   vừa sai field vừa trùng email nhận `MSG09` chứ không phải `MSG08`. Cố ý — tiết kiệm một round
   trip vào database cho request chắc chắn hỏng, và vẫn trả đủ mọi field sai trong một lần.
3. **UC-06 có thêm nhánh trùng email** mà bản vẽ không có (`alt` của UC-06 chỉ có hai operand).
   Unique index không quan tâm bản vẽ viết gì: thiếu nhánh này thì sửa ai đó sang email đã tồn tại
   sẽ ra `500` thay vì `MSG08`.
4. **UC-06 dùng `:Token Service` mà bản vẽ UC-06 không vẽ lifeline đó** — chỉ UC-07 có. Đổi vai
   trò phải huỷ refresh token vì capability nằm trong access token; không huỷ thì người đó giữ
   nguyên quyền cũ tới khi token hết hạn.
5. **UC-06 và UC-07 có thêm luật "không được xoá sổ quản trị viên cuối cùng"**, xem
   [phần riêng bên dưới](#luật-không-được-xoá-sổ-quản-trị-viên-cuối-cùng). Với **UC-07** thì nó lấp
   vào nhánh *"Status change is not allowed"* bản vẽ đã có sẵn nhưng không nói khi nào. Với
   **UC-06** thì đây là nhánh hoàn toàn mới: bản vẽ UC-06 không có operand "not allowed" nào.
6. **UC-08 có thêm nửa thứ hai mà bản vẽ không có:** người dùng mở liên kết và đặt mật khẩu mới
   (`POST /api/auth/password-reset`, `MSG78`–`MSG80`). Thiếu nửa này thì email gửi đi một liên kết
   không dẫn tới đâu.
7. **UC-08 trả `404` khi không có tài khoản** — bản vẽ không có nhánh này (giả định quản trị viên
   chọn từ danh sách vừa tải), giống các UC khác ở trên.
8. **UC-08 EX-1 ("không đủ điều kiện", `MSG14`) được định nghĩa là: tài khoản đã bị vô hiệu hoá.**
   Đặc tả chỉ nói "cannot use the password reset function" mà không nói khi nào. Tài khoản chỉ đăng
   nhập bằng Google vẫn đủ điều kiện.
9. **BR-20 và BR-22 (UC-08 bước 4) không được định nghĩa ở đâu trong tài liệu.** Code hiểu "secure
   password reset request" là: token ngẫu nhiên 256 bit, chỉ lưu bản băm, hiệu lực 60 phút, dùng
   một lần, mỗi tài khoản chỉ có một liên kết còn hiệu lực (gửi lại thì liên kết cũ chết).
10. **`:Authentication Service` và `:Email Service` của bản vẽ UC-08 không phải hai service riêng.**
    "Create password reset request" là `IPasswordResetTokenStore` (Redis); `:Email Service` gửi
    **đồng bộ** qua SMTP chứ không qua hàng đợi `email_messages` — lý do ở
    [mục UC-08](#uc-08--post-apiusersidpassword-reset). `MSG15` trả HTTP `503`.
11. **Luật mới không có trong đặc tả:** liên kết đang chờ bị thu hồi khi tài khoản bị vô hiệu hoá,
    đổi email hoặc đổi vai trò; đặt lại thành công thì huỷ mọi refresh token của tài khoản.
12. **UC-09 không có endpoint và sequence diagram riêng.** Gán / đổi vai trò chạy trên
    `PUT /api/users/{id}` của UC-06 — xem [mục UC-09](#uc-09--gán--đổi-vai-trò-hệ-thống--dùng-put-apiusersid).
    "Sơ đồ liên quan" của UC-09 trong đặc tả đang trỏ nhầm sang `UC11_ConfigureRolePermissions.png`.

**Không có thay đổi database.** Cả hai bảng đã có sẵn từ migration `ExpandDomainModel`; story này
chỉ đọc và ghi vào chúng. UC-08 cũng không thêm migration: token đặt lại mật khẩu sống ở Redis.

## Endpoint

Tất cả đều đòi capability **`AdminUsers`** — tiêu chí nghiệm thu của SCRUM-13.

| Method | Path | Việc |
|---|---|---|
| GET | `/api/users` | UC-04 — danh sách toàn bộ tài khoản, đã sắp xếp |
| GET | `/api/users/{id}` | UC-06 (nửa đầu) — thông tin để đổ vào form sửa |
| POST | `/api/users` | UC-05 — tạo tài khoản mới, trạng thái active |
| PUT | `/api/users/{id}` | UC-06 (nửa sau) — sửa họ tên, email, vai trò |
| PUT | `/api/users/{id}/status` | UC-07 — vô hiệu hoá hoặc kích hoạt lại |
| POST | `/api/users/{id}/password-reset` | UC-08 — gửi email chứa liên kết đặt lại mật khẩu |

Nửa sau của UC-08 nằm ngoài nhóm này vì nó **ẩn danh**: `POST /api/auth/password-reset`.

### Vì sao `status` là endpoint riêng

SCRUM-13 chỉ ghi `GET/POST/PUT /api/users`, nhưng UC-06 và UC-07 là **hai use case khác nhau, luật
khác nhau**: UC-07 còn phải kết thúc phiên đăng nhập và ghi audit bằng action khác. Gộp vào một
`PUT /api/users/{id}` nghĩa là sửa tên người khác cũng có thể vô hiệu hoá họ như một tác dụng phụ,
mà đường đi đó lại không chạy phần huỷ phiên.

Có test chặn đúng chuyện này: gửi `isActive: false` kèm theo form sửa hồ sơ **không** đổi trạng
thái tài khoản.

### Phân quyền kiểm theo capability, không theo tên role

Policy `AdminUsers` khai báo ở `Program.cs` kiểm claim `cap` trong access token, không so sánh
vai trò của người gọi với Admin. Code nghiệp vụ không bao giờ so sánh vai trò — vai trò chỉ là một
bó quyền có tên ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)). Chuyển `AdminUsers` sang
vai trò khác trong bảng `role_capabilities` — kể cả một vai trò quản trị viên tự tạo — là có hiệu
lực ngay, không cần sửa code.

Policy gắn trên **cả nhóm** `/api/users` chứ không gắn từng route: thêm một endpoint mới vào nhóm
thì nó tự kế thừa, không có chuyện quên.

Người đã đăng nhập nhưng không có `AdminUsers` nhận `403`, người chưa đăng nhập nhận `401`.

**`403` ở đây là có chủ ý, không phải quên quy ước `404`.** Quy ước `404` (AGENTS.md, SCRUM-22)
dùng để giấu **sự tồn tại của một dòng dữ liệu** — trả `403` cho một tài liệu cụ thể chính là xác
nhận tài liệu đó có thật. `/api/users` thì ngược lại: đường dẫn nằm trong chính tài liệu API này,
giấu đi không được gì, mà `404` lại khiến FE không phân biệt nổi "sai quyền" với "gọi sai URL".
Bảng đối chiếu đầy đủ ở mục *403 hay 404?* trong `AGENTS.md`.

## UC-04 · `GET /api/users`

`200 OK` — mảng, sắp xếp theo họ tên rồi email:

```json
[
  {
    "id": "01999f2e-…",
    "fullName": "Lê Thành Viên",
    "email": "thanhvien@trustcabinet.local",
    "roleId": "01a0ce78-7204-7558-b181-e908a08e83a3",
    "roleName": "Thành viên",
    "isActive": true,
    "hasPassword": true,
    "hasGoogleLink": false,
    "createdAt": "2026-09-19T15:07:18+00:00"
  }
]
```

Vài điểm cố ý:

- **Sắp xếp trong database**, không sort trong C#. PostgreSQL dùng collation của nó cho tiếng
  Việt; `OrderBy` mặc định của .NET so sánh ordinal và sẽ xếp "Đ" sau "Z".
- **Tài khoản bị vô hiệu hoá vẫn nằm trong danh sách.** UC-07 kích hoạt lại từ chính danh sách
  này — ẩn đi thì không bao giờ bật lại được.
- **Không phân trang.** Một tổ chức duy nhất (ADR-0004), số nhân sự đếm được; phân trang là bộ máy
  cho một danh sách không ai phải cuộn. Đã đánh dấu `ponytail:` trong code.
- **Vai trò là `roleId` + `roleName`** (từ `SCRUM-63`, trước là `"role": "Member"`). Tên lấy từ bảng
  `roles`, nên vai trò quản trị viên tự tạo cũng hiện đúng tên — FE không giữ bảng nhãn nào. Danh
  sách vai trò để chọn: `GET /api/roles` ([04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md)).
- **`passwordHash` và `googleSubjectId` không bao giờ lên wire.** Response dựng từ `UserAccount`
  (một projection) chứ không serialize thẳng entity `User` — serialize entity nghĩa là mỗi lần ai
  đó thêm field là một lần rò rỉ không ai để ý trong review. `hasPassword` / `hasGoogleLink` chỉ
  cho biết *có tồn tại hay không*, đủ để màn hình hiển thị cách đăng nhập.

## UC-05 · `POST /api/users`

```json
{
  "fullName": "Trần Chủ Toạ",
  "email": "chutoa@trustcabinet.local",
  "password": "MatKhauDaiVaAnToan@2026",
  "roleId": "01a0ce78-7202-7945-be13-d2022cf25203"
}
```

`201 Created`, kèm header `Location: /api/users/{id}`:

```json
{
  "code": "MSG07",
  "message": "Tạo tài khoản thành công.",
  "user": { "…": "như shape ở UC-04" }
}
```

Tài khoản tạo ra **luôn ở trạng thái active** — diagram ghi rõ "Create active user account". Muốn
tắt thì dùng UC-07.

`roleId` là id một dòng của `roles` — vai trò mặc định hoặc vai trò tự tạo. Thiếu, không phải Guid,
hoặc không tồn tại (ví dụ vừa bị quản trị viên khác xoá) thì trả `MSG09` với lỗi ở key `roleId`
(`Vai trò không tồn tại.`) chứ không phải `400` từ model binder — diagram cần "Display invalid
fields", nên phải chỉ ra được *field nào*.

## UC-06 · `PUT /api/users/{id}`

```json
{ "fullName": "Tên Mới", "email": "moi@trustcabinet.local", "roleId": "01a0ce78-7203-7c7f-8326-7a0baebf4c22" }
```

`200 OK` trả `MSG10` cùng shape như trên.

**Không có `password` trong body.** Đổi mật khẩu người khác là UC-08; một màn hình sửa hồ sơ không
nên có khả năng âm thầm đặt mật khẩu cho ai đó.

**Đổi vai trò sẽ kết thúc phiên của người đó.** Capability nằm trong access token, nên nếu không
huỷ refresh token thì họ giữ nguyên quyền cũ cho tới khi token hết hạn. Sửa tên (không đổi role)
thì **không** đăng xuất họ — sửa lỗi chính tả không nên đá ai ra khỏi hệ thống.

## UC-07 · `PUT /api/users/{id}/status`

```json
{ "isActive": false }
```

`200 OK` trả `MSG11` cho cả hai chiều.

Một endpoint cho cả hai nhánh, không tách `/deactivate` và `/reactivate`: hai nhánh trong diagram
chỉ khác nhau ở giá trị ghi xuống và ở chỗ có huỷ phiên hay không, còn message thì chung một mã.

> **`isActive` bắt buộc phải có trong body.** Thiếu field, hoặc gửi sai tên (`is_active`,
> `active`), đều trả `400` kèm `errors.isActive` — **không** mặc định thành `false`. Nếu để kiểu
> `bool` không nullable thì body `{}` sẽ bind thành `false`, tức là vô hiệu hoá người ta, huỷ phiên,
> ghi `user.deactivated` vào audit log vĩnh viễn, rồi trả `200 MSG11 "thành công"`. Một field mà
> giá trị mặc định của nó có tính phá huỷ thì phải bắt khai báo tường minh.

### Vô hiệu hoá phải kết thúc phiên

Đây là bước `:Token Service` trong diagram. Chỉ set `is_active = false` là **chưa vô hiệu hoá**
theo bất kỳ nghĩa nào có ý nghĩa: access token đã cấp vẫn còn hiệu lực tới khi hết hạn. Huỷ toàn bộ
refresh token là thứ chặn việc gia hạn.

`AuthenticationService.RefreshAsync` cũng đọc lại `is_active` mỗi lần refresh, nên dù không huỷ thì
lần refresh sau cũng bị từ chối.

**Nhưng huỷ refresh token *không* làm access token đang cầm mất hiệu lực.** Nó là JWT tự chứa, hợp
lệ tới khi hết hạn (mặc định 15 phút). Đó là lý do nhóm `/api/users` có thêm một endpoint filter
đọc lại `is_active` và capability của **người gọi** từ database ở mỗi request — xem
[Người gọi được kiểm lại mỗi request](#người-gọi-được-kiểm-lại-mỗi-request).

Kích hoạt lại thì không đụng tới phiên nào cả: không có phiên nào để dọn.

### Người gọi được kiểm lại mỗi request

Policy `AdminUsers` chỉ đọc claim trong token — mà token là ảnh chụp của người gọi tại thời điểm
cấp. Nếu chỉ dựa vào nó, một quản trị viên vừa bị vô hiệu hoá vẫn giữ nguyên toàn quyền trên mọi
tài khoản cho tới khi token hết hạn: đủ để vô hiệu hoá ngược lại người vừa khoá mình, hoặc tự tạo
một tài khoản `Admin` mới rồi đăng nhập lại bằng nó.

Nên filter của nhóm đọc lại từ database ở mỗi request: người gọi còn `is_active` không, và role của
họ còn `AdminUsers` trong `role_capabilities` không. Không còn thì **403** — token thật và chưa hết
hạn, nhưng tài khoản đằng sau nó không còn được phép.

Kiểm theo capability chứ không theo tên role, giống hệt policy, chỉ khác nguồn đọc là database thay
vì token.

### Khi nào bị từ chối

| Tình huống | Vì sao |
|---|---|
| Tài khoản **đã ở đúng trạng thái đó** | Hai quản trị viên cùng thao tác trên một danh sách, hoặc form bị submit hai lần. Từ chối chứ không coi là thành công, để audit log không ghi hai lần cho một lần vô hiệu hoá — bằng chứng phải nói đúng việc đã xảy ra. |
| Quản trị viên nhắm vào **chính tài khoản mình** | Tự khoá mình ra ngoài không nên là việc làm được bằng một cú nhấp nhầm. |
| Thao tác sẽ **không còn quản trị viên nào** | Xem dưới. |

### Luật "không được xoá sổ quản trị viên cuối cùng"

Chỉ chặn "tự nhắm vào mình" là **chưa đủ**, và đây là một lỗ hổng thật đã được phát hiện khi review:

1. A và B là hai quản trị viên duy nhất, cả hai đang đăng nhập.
2. B vô hiệu hoá A → thành công. Refresh token của A bị huỷ.
3. **Access token của A vẫn còn hiệu lực** và vẫn mang `cap=AdminUsers`.
4. A vô hiệu hoá B → cũng thành công. Giờ **không còn quản trị viên nào hoạt động**, phải vào
   console database mới cứu được.

Luật đúng không phải "có phải tôi không?" mà là một **bất biến**: luôn còn ít nhất một tài khoản
đang hoạt động giữ `AdminUsers`. `UserService` đếm số người còn lại trước khi cho vô hiệu hoá hoặc
đổi vai trò, và từ chối nếu con số đó về 0.

Đếm bằng cách join `role_capabilities`, **không** so `role == Admin`: bảng đó mới là thứ quyết định
ai được làm gì và có thể trỏ sang role khác mà không cần deploy — so tên role thì guard sẽ im lặng
ngừng hoạt động đúng lúc cấu hình thay đổi.

Vì vậy chuyển vai trò sang một role **cũng có** `AdminUsers` thì được phép: không có capability nào
bị mất. Sửa tên hay email của quản trị viên duy nhất cũng được phép — chỉ đổi vai trò và vô hiệu
hoá mới bị kiểm.

## UC-08 · `POST /api/users/{id}/password-reset`

Quản trị viên bấm "Đặt lại mật khẩu", xác nhận, hệ thống gửi **một liên kết dùng một lần** tới email
đăng ký của tài khoản. Không có body. Quản trị viên **không bao giờ thấy hay đặt** mật khẩu.

`200 OK`:

```json
{
  "code": "MSG13",
  "message": "Đã gửi email đặt lại mật khẩu.",
  "user": { "…": "như shape ở UC-04" }
}
```

| Tình huống | HTTP | `code` |
|---|---|---|
| Đã gửi email | `200` | `MSG13` |
| Tài khoản đã bị vô hiệu hoá (EX-1) | `409` | `MSG14` |
| Không gửi được email (EX-2): SMTP tắt, không kết nối được, hoặc server từ chối | `503` | `MSG15` |
| Không có tài khoản đó | `404` | *(không body)* |

Vài điểm cố ý:

- **Mật khẩu hiện tại vẫn dùng được** cho tới khi người dùng mở liên kết và đặt mật khẩu mới. Bấm
  nút này không bao giờ khoá ai ra khỏi hệ thống, và không đăng xuất ai.
- **"Không đủ điều kiện" (MSG14) chỉ có một nguyên nhân: tài khoản đã bị vô hiệu hoá** (PRE-3 của
  đặc tả). Tài khoản chỉ đăng nhập bằng Google (`hasPassword: false`) **vẫn đủ điều kiện** — đặt
  mật khẩu xong thì nó có thêm cách đăng nhập bằng mật khẩu. Đặc tả chưa nói rõ, đã note cho BA.
- **Gửi lại thì liên kết cũ chết.** Mỗi tài khoản chỉ có một liên kết còn hiệu lực — liên kết mới
  nhất. Bấm hai lần thì hộp thư có hai email nhưng chỉ email sau dùng được.
- **Liên kết đang chờ cũng chết khi tài khoản bị vô hiệu hoá, đổi email hoặc đổi vai trò** (cùng chỗ
  huỷ phiên ở UC-06/UC-07). Không thì vô hiệu hoá rồi kích hoạt lại trong vòng một giờ làm liên kết
  sống lại, và liên kết đã gửi nhầm địa chỉ vẫn dùng được sau khi quản trị viên sửa email. Sửa tên
  thì liên kết vẫn còn.
- **Gửi đồng bộ qua SMTP, không qua hàng đợi `email_messages`.** Hàng đợi lưu cả nội dung email vào
  database, mà nội dung này chứa một credential còn sống — nó sẽ nằm trong bảng rất lâu sau khi
  liên kết hết hạn. Thêm nữa, MSG15 phải trả lời ngay trong request, hàng đợi không làm được. Thời
  gian chờ tối đa bằng `Email:SendAttemptTimeoutSeconds` (mặc định 60 giây).
- **Server đã nhận thư thì coi là đã gửi**, kể cả khi lệnh `QUIT` sau đó lỗi — trước đây
  `SmtpEmailSender` báo lỗi ở bước này, nghĩa là UC-08 sẽ trả MSG15 cho một liên kết đã nằm trong hộp
  thư, còn hàng đợi thư mời (SCRUM-15) sẽ gửi lại lần nữa. Bản sửa áp dụng cho cả hai.
- **Đã gửi mail rồi thì dòng audit luôn được ghi**, kể cả khi quản trị viên đóng tab ngay sau đó.
- **Phải bật `Email:Smtp:Enabled`** (cấu hình như SCRUM-15, xem
  [07-gui-email-thu-moi-note-cai-dat-may-moi.md](07-gui-email-thu-moi-note-cai-dat-may-moi.md)). Tắt
  thì mọi lần gọi đều là `503 MSG15` — không phải "thành công" mà không có mail nào đi.

### Liên kết trong email

```
{Email:ApplicationBaseUrl}/reset-password#token=<43 ký tự base64url>
```

- **Token nằm sau dấu `#` (fragment), không nằm ở query string.** Trình duyệt không bao giờ gửi
  fragment lên server, nên token không rơi vào access log, proxy hay header `Referer`.
- 256 bit ngẫu nhiên, **hiệu lực 60 phút, dùng một lần**. Redis chỉ lưu SHA-256 của token
  (`prt:{hash}` → user id, `prt-user:{userId}` → hash mới nhất), cả hai key tự hết hạn. Dump Redis
  không ra được liên kết nào dùng được.

## Nửa sau của UC-08 · `POST /api/auth/password-reset`

Trang `/reset-password` (FE) đọc token từ fragment rồi gửi cùng mật khẩu mới. **Ẩn danh** — người quên
mật khẩu không có phiên nào để gửi; token chính là bằng chứng.

```json
{ "token": "…", "newPassword": "MatKhauMoiSauReset@2026" }
```

| Tình huống | HTTP | `code` |
|---|---|---|
| Đổi mật khẩu thành công | `200` | `MSG78` |
| Liên kết không hợp lệ, hết hạn, đã dùng, đã bị liên kết mới thay thế, hoặc tài khoản đã bị vô hiệu hoá | `400` | `MSG79` |
| Mật khẩu mới dưới 12 ký tự — kèm `errors.newPassword` | `400` | `MSG80` |

```json
{ "code": "MSG80", "message": "Mật khẩu mới không hợp lệ.", "errors": { "newPassword": "Mật khẩu phải có ít nhất 12 ký tự." } }
```

- **Kiểm liên kết trước, mật khẩu sau.** Liên kết chết thì báo ngay, không để người dùng gõ xong mật
  khẩu hợp lệ rồi mới biết.
- **Mật khẩu sai luật (MSG80) không làm tiêu liên kết** — sửa lại rồi gửi tiếp bằng đúng liên kết đó.
- **Năm nguyên nhân của MSG79 gộp một mã có chủ ý.** Endpoint ẩn danh; phân biệt "hết hạn" với "không
  tồn tại" chỉ giúp người dò liên kết.
- **Thành công thì huỷ toàn bộ refresh token, và không cấp token đăng nhập.** Người dùng đăng nhập
  lại bằng mật khẩu vừa đặt. Huỷ **hai lần**:
  - *trước* khi lưu mật khẩu — nếu Redis lỗi thì mật khẩu cũ và phiên cũ vẫn nguyên như trước, chứ
    không phải mật khẩu mới nằm cạnh đúng những phiên mà người ta đặt lại mật khẩu để chấm dứt;
  - *sau* khi lưu — cho một lần đăng nhập bằng mật khẩu cũ (hoặc một lần refresh đang chạy dở) rơi
    vào khoảng giữa hai bước. Lần này lỗi thì chỉ ghi log: mật khẩu đã đổi, liên kết đã tiêu.
- **Access token đã cấp vẫn dùng được tới khi hết hạn** (tối đa 15 phút), ở mọi endpoint — JWT không
  thu hồi được, và phần kiểm lại người gọi của `/api/users` chỉ xem `is_active` và capability, không
  xem mật khẩu có vừa đổi hay không. Đóng hẳn cửa sổ này cần một "security stamp" trên `users` (cần
  migration); xem [Còn nợ](#còn-nợ).
- Luật mật khẩu giống UC-05: tối thiểu 12 ký tự.

## UC-09 · Gán / đổi vai trò hệ thống — dùng `PUT /api/users/{id}`

**Không có endpoint riêng.** Mọi thứ đặc tả UC-09 đòi hỏi đã có ở `PUT /api/users/{id}` của UC-06
từ SCRUM-13 / SCRUM-63; thêm một `PUT /api/users/{id}/role` chỉ là một đường thứ hai làm cùng một
việc, phải giữ hai bộ luật đồng bộ với nhau.

| Bước trong đặc tả UC-09 | Ở đâu |
|---|---|
| 3 · Hiện thông tin và vai trò hiện tại | `GET /api/users/{id}` → `roleId`, `roleName` |
| 5 · Hiện danh sách vai trò có thể gán | `GET /api/roles` ([04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md)) |
| 7 / AF-1 · Gán hoặc thay vai trò | `PUT /api/users/{id}` với `roleId` mới, giữ nguyên `fullName`, `email` |
| 8 · Ghi audit | `user.updated`, `details` chứa vai trò cũ và mới (id **và** tên) |
| 9 · `MSG10` | `200 MSG10` |
| EX-1 · Vai trò không còn | `400 MSG09`, `errors.roleId = "Vai trò không tồn tại."`, không đổi gì |
| EX-2 · Hạ vai trò quản trị viên cuối cùng | `409 MSG12`, giữ vai trò cũ — xem [luật quản trị viên cuối cùng](#luật-không-được-xoá-sổ-quản-trị-viên-cuối-cùng) |

Thêm một điều đặc tả không nói: **đổi vai trò sẽ đăng xuất người đó** (POST-2 "nhận capability của vai
trò mới" chỉ thật sự đúng khi access token cũ — mang capability cũ — không được gia hạn nữa).

Đặc tả UC-09 đang trỏ "Sơ đồ liên quan" sang `UC11_ConfigureRolePermissions.png` — sơ đồ của use case
khác. UC-09 chưa có sequence diagram riêng; để BA vẽ ở `BA Final Review`.

## Mã thông báo

FE khớp theo `code`, không khớp theo `message` — đổi chữ trong `message` không được làm FE vỡ.

| `code` | HTTP | Khi nào |
|---|---|---|
| `MSG07` | `201` | Tạo tài khoản thành công |
| `MSG08` | `409` | Email đã có tài khoản khác dùng (`message`: `Email đã được sử dụng.`) |
| `MSG09` | `400` | Thông tin không hợp lệ — kèm `errors` |
| `MSG10` | `200` | Cập nhật tài khoản thành công |
| `MSG11` | `200` | Đổi trạng thái thành công (cả hai chiều) |
| `MSG12` | `409` | Không được phép đổi trạng thái / hạ vai trò quản trị viên cuối cùng |
| `MSG13` | `200` | Đã gửi email đặt lại mật khẩu (UC-08) |
| `MSG14` | `409` | Tài khoản không dùng được chức năng đặt lại mật khẩu (đã bị vô hiệu hoá) |
| `MSG15` | `503` | Không gửi được email đặt lại mật khẩu |
| `MSG78` / `MSG79` / `MSG80` | `200` / `400` / `400` | `POST /api/auth/password-reset` — xem [mục riêng](#nửa-sau-của-uc-08--post-apiauthpassword-reset) |
| *(không có code)* | `404` | Không tìm thấy tài khoản |

`400` kèm từng field sai, vì diagram yêu cầu "Display invalid fields":

```json
{
  "code": "MSG09",
  "message": "Thông tin tài khoản không hợp lệ.",
  "errors": {
    "fullName": "Họ tên không được để trống.",
    "email": "Email không hợp lệ.",
    "password": "Mật khẩu phải có ít nhất 12 ký tự."
  }
}
```

**Trả về tất cả field sai cùng lúc**, không phải từng cái một — mỗi lỗi một vòng request là một
cái form khổ sở.

### Vì sao email trùng là `409` chứ không phải `400`

Request hợp lệ về mặt cú pháp; nó xung đột với trạng thái hiện có của hệ thống. Đây cũng là lý do
`MSG12` (không được đổi trạng thái) dùng `409`.

## Trùng email: kiểm hai lần, cố ý

`UserService` hỏi `EmailExistsAsync` trước khi ghi, **và** bắt lỗi unique index khi save
(`SQLSTATE 23505`).

Không thừa: hai request tạo cùng một email đồng thời đều thấy email còn trống rồi cùng ghi. Cái
kiểm trước xử lý trường hợp thường gặp để trả `MSG08` thay vì `500`; unique index mới là thứ **bảo
đảm** tính duy nhất, và nhánh catch biến lần thua cuộc đó thành `MSG08` chứ không phải stack trace.

So sánh không `LOWER()` hai vế: cột là `citext` nên PostgreSQL đã so sánh không phân biệt hoa
thường và vẫn dùng được unique index. Thêm `LOWER()` là biến nó thành sequential scan.

## Audit log

Mỗi thao tác ghi thành công đều ghi một dòng `audit_log`:

| Action | Khi nào |
|---|---|
| `user.created` | UC-05 |
| `user.updated` | UC-06 — `details` chứa **cả giá trị cũ và mới** |
| `user.deactivated` | UC-07 |
| `user.reactivated` | UC-07 |
| `user.password_reset_requested` | UC-08 — quản trị viên gửi liên kết; `details` chỉ có email nhận. Gửi mail hỏng (MSG15) thì **không** ghi |
| `user.password_reset` | UC-08 — liên kết được dùng; **actor là chính tài khoản đó**, không có `details` |

Bốn điểm cố ý:

- **Ghi sau khi lưu thành công**, không phải trước. Ghi trước nghĩa là audit log lưu lại những
  việc chưa chắc đã xảy ra.
- **`actorId` lấy từ token**, không lấy từ body. Một `actorId` do caller tự chọn thì không chứng
  minh được gì, mà đây chính là thứ audit log dùng làm bằng chứng.
- **Không bao giờ có mật khẩu hay token đặt lại mật khẩu trong `details`**, kể cả đã băm. Audit log nhiều người đọc hơn bảng
  `users` và cố ý không bao giờ bị xoá — một credential rơi vào đây là rơi vĩnh viễn. Có test chặn.
- **Tiếng Việt ghi nguyên chữ, không escape.** `System.Text.Json` mặc định escape mọi ký tự ngoài
  ASCII, biến `"Trần Chủ Toạ"` thành `"Trần Chủ Toạ"` — bằng chứng mà không ai đọc
  nổi trong console database.

`AuditService` tự `SaveChanges` riêng, không dùng chung unit of work với nghiệp vụ. Đánh đổi này
có giá: một dòng audit có thể sống sót trong khi thay đổi nghiệp vụ sau đó lưu hỏng. Chiều ngược
lại — tạo được tài khoản mà không có dòng nào ghi lại ai tạo — mới là chiều phá hỏng bằng chứng,
nên đổi theo chiều này.

`ip_address` lấy từ `RemoteIpAddress`, **cố ý không** đọc `X-Forwarded-For`: header đó do caller
gửi lên, tin nó khi chưa cấu hình forwarded-headers middleware nghĩa là ai cũng ghi được IP tuỳ ý
vào bằng chứng. Đã đánh dấu `ponytail:` trong code.

## Còn nợ

- ~~UC-08 — đặt lại mật khẩu~~ — **đã làm**, xem [mục UC-08](#uc-08--post-apiusersidpassword-reset).
  Còn thiếu ở FE: nút trên màn hình quản lý người dùng và trang `/reset-password` — xem
  [note cho FE](02-quan-ly-nguoi-dung-note-cho-fe.md#8-uc-08--đặt-lại-mật-khẩu).
- **Gửi lại liên kết khi email trước gửi hỏng làm chết luôn liên kết trước đó.** Token mới được tạo
  (và thay thế token cũ) *trước* khi gửi mail, vì email phải chứa nó. Nếu lần gửi sau hỏng (MSG15),
  liên kết của lần gửi thành công trước đó cũng không còn dùng được. Chấp nhận: quản trị viên đã thấy
  MSG15 và sẽ gửi lại; chiều ngược lại (giữ nhiều liên kết sống) mới là chiều nới lỏng bảo mật.
- **Access token còn sống tới 15 phút sau khi đặt lại mật khẩu.** Refresh token đã bị huỷ, nhưng JWT
  đã cấp thì không thu hồi được. Muốn đóng hẳn: thêm cột kiểu `security_stamp` / `password_changed_at`
  vào `users`, đưa vào token, và kiểm nó ở refresh cùng `AdminCallerRevalidation`. Chưa làm vì cần
  migration và chạm vào luồng xác thực của mọi endpoint — cùng cửa sổ 15 phút đã chấp nhận ở UC-07.
- **Endpoint ẩn danh `/api/auth/password-reset` chưa có rate limit.** Token 256 bit nên dò không có
  nghĩa; rate limit sẽ làm chung cho cả `/api/auth/*` khi cần.
- **Ghi audit và ghi nghiệp vụ nằm ở hai transaction.** `AuditService` tự `SaveChanges` sau khi
  nghiệp vụ đã commit, nên nếu dòng audit ghi hỏng thì thay đổi nghiệp vụ **đã xảy ra rồi** và
  không rollback lại được: kết quả là 500 kèm một thay đổi không có bằng chứng. Trường hợp duy nhất
  gây ra được điều này là `actor_id` không trỏ tới `users.id` nào (cột có FK `Restrict`) — hiện đã
  chặn được vì filter từ chối request không có claim `sub` hợp lệ. Muốn đóng hẳn thì cho hai lần
  ghi vào chung một transaction; chưa làm vì đánh đổi hiện tại vẫn nghiêng về phía giữ bằng chứng.
  Lưu ý: `AuditServiceTests` dùng EF in-memory provider nên **không** kiểm FK — test ở đó không bắt
  được lỗi này.
- **Liên kết tài khoản với Google từ màn hình quản trị.** Hiện vẫn làm bằng SQL — xem
  [01-xac-thuc.md](01-xac-thuc.md).
- ~~Màn hình FE~~ — **đã làm**: `frontend/src/features/access/UsersPage.tsx` (danh sách, tìm/lọc
  phía client, vô hiệu hoá/kích hoạt có hộp xác nhận) và `UserFormDialog.tsx` (tạo/sửa, lỗi theo
  từng field). `MSG12` ở `/status` được FE phân biệt bằng cách đọc lại tài khoản: đã đúng trạng
  thái thì là danh sách cũ, ngược lại là quản trị viên cuối cùng.
