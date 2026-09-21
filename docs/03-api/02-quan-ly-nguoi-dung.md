# 02 · Quản lý người dùng

Hiện thực **UC-04, UC-05, UC-06 và UC-07 (SCRUM-13)**: xem danh sách, tạo, sửa và vô hiệu hoá /
kích hoạt lại tài khoản người dùng.

**UC-08 (đặt lại mật khẩu) chưa làm** — chưa có hạ tầng gửi email, và làm nửa vời sẽ để lại một
endpoint báo "đã gửi mail" trong khi không có mail nào được gửi. Xem phần [Còn nợ](#còn-nợ).

Sequence diagram chia bốn participant: `User Management Page` → `:User Controller` →
`:User Service` → `:User Repository` → `Database`, cộng thêm `:Audit Service` và `:Session
Service`. Code đi theo đúng các lớp đó:

Bản vẽ từng use case:

| Use case | Sequence diagram |
|---|---|
| UC-04 — Xem danh sách tài khoản | [ViewUserAccount.png](../06-sequencediagram/ViewUserAccount.png) |
| UC-05 — Tạo tài khoản | [CreateUserAccount.png](../06-sequencediagram/CreateUserAccount.png) |
| UC-06 — Sửa tài khoản | [UpdateUserAccount.png](../06-sequencediagram/UpdateUserAccount.png) |
| UC-07 — Vô hiệu hoá / kích hoạt lại | [DeactivateReactivateUserAccount.png](../06-sequencediagram/DeactivateReactivateUserAccount.png) |
| UC-08 — Đặt lại mật khẩu | [ResetUserPassword.png](../06-sequencediagram/ResetUserPassword.png) — **chưa hiện thực**, xem [Còn nợ](#còn-nợ) |

| Participant trong diagram | Code |
|---|---|
| `:User Controller` | [`backend/TrustCabinet.Web/Endpoints/UserEndpoints.cs`](../../backend/TrustCabinet.Web/Endpoints/UserEndpoints.cs) |
| `:User Service` | [`backend/TrustCabinet.Infrastructure/Users/UserService.cs`](../../backend/TrustCabinet.Infrastructure/Users/UserService.cs) |
| `:User Repository` | [`backend/TrustCabinet.Infrastructure/Users/UserAccountRepository.cs`](../../backend/TrustCabinet.Infrastructure/Users/UserAccountRepository.cs) |
| `:Audit Service` | [`backend/TrustCabinet.Infrastructure/Audit/AuditService.cs`](../../backend/TrustCabinet.Infrastructure/Audit/AuditService.cs) |
| `:Session Service` | `IRefreshTokenStore.RevokeAllForUserAsync` — xem [Vô hiệu hoá phải kết thúc phiên](#vô-hiệu-hoá-phải-kết-thúc-phiên) |
| `Database` | bảng `users` và `audit_log` |

### Ba chỗ code khác bản vẽ

Bản vẽ là thiết kế nghiệp vụ, không phải đặc tả kỹ thuật đầy đủ. Ba chỗ sau code làm khác, đều có
lý do và đều đã được ghi lại:

1. **`:User Repository` không kiểm "Username"** (UC-05 vẽ "Check Username and Email availability").
   Hệ thống **không có username** — đăng nhập bằng email hoặc Google, theo
   [ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md). Chỉ kiểm email.
2. **UC-06 có thêm nhánh trùng email** mà bản vẽ không có. Unique index không quan tâm bản vẽ viết
   gì: thiếu nhánh này thì sửa ai đó sang email đã tồn tại sẽ ra `500` thay vì `MSG08`.
3. **UC-06 và UC-07 có thêm luật "không được xoá sổ quản trị viên cuối cùng"**, xem
   [phần riêng bên dưới](#luật-không-được-xoá-sổ-quản-trị-viên-cuối-cùng). Bản vẽ UC-07 có sẵn
   nhánh *"Status change is not allowed"* nhưng không nói khi nào — đây là một trong các điều kiện
   lấp vào nhánh đó.

**Không có thay đổi database.** Cả hai bảng đã có sẵn từ migration `ExpandDomainModel`; story này
chỉ đọc và ghi vào chúng.

## Endpoint

Tất cả đều đòi capability **`AdminUsers`** — tiêu chí nghiệm thu của SCRUM-13.

| Method | Path | Việc |
|---|---|---|
| GET | `/api/users` | UC-04 — danh sách toàn bộ tài khoản, đã sắp xếp |
| GET | `/api/users/{id}` | UC-06 (nửa đầu) — thông tin để đổ vào form sửa |
| POST | `/api/users` | UC-05 — tạo tài khoản mới, trạng thái active |
| PUT | `/api/users/{id}` | UC-06 (nửa sau) — sửa họ tên, email, vai trò |
| PUT | `/api/users/{id}/status` | UC-07 — vô hiệu hoá hoặc kích hoạt lại |

### Vì sao `status` là endpoint riêng

SCRUM-13 chỉ ghi `GET/POST/PUT /api/users`, nhưng UC-06 và UC-07 là **hai use case khác nhau, luật
khác nhau**: UC-07 còn phải kết thúc phiên đăng nhập và ghi audit bằng action khác. Gộp vào một
`PUT /api/users/{id}` nghĩa là sửa tên người khác cũng có thể vô hiệu hoá họ như một tác dụng phụ,
mà đường đi đó lại không chạy phần huỷ phiên.

Có test chặn đúng chuyện này: gửi `isActive: false` kèm theo form sửa hồ sơ **không** đổi trạng
thái tài khoản.

### Phân quyền kiểm theo capability, không theo tên role

Policy `AdminUsers` khai báo ở `Program.cs` kiểm claim `cap` trong access token, không so sánh
`role == "Admin"`. Đây là điều `AccessEnums` đã ghi: *"what it allows is expressed as capabilities,
never checked by comparing against the role name in business code"*. Chuyển `AdminUsers` sang role
khác trong bảng `role_capabilities` là có hiệu lực ngay, không cần sửa code.

Policy gắn trên **cả nhóm** `/api/users` chứ không gắn từng route: thêm một endpoint mới vào nhóm
thì nó tự kế thừa, không có chuyện quên.

Người đã đăng nhập nhưng không có `AdminUsers` nhận `403`, người chưa đăng nhập nhận `401`.

## UC-04 · `GET /api/users`

`200 OK` — mảng, sắp xếp theo họ tên rồi email:

```json
[
  {
    "id": "01999f2e-…",
    "fullName": "Lê Thành Viên",
    "email": "thanhvien@trustcabinet.local",
    "role": "Member",
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
  "role": "Chair"
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

`role` nhận **không phân biệt hoa thường** (`"chair"` cũng được): quy ước viết hoa của JSON hai đầu
khác nhau, và chữ thường không nên thành lỗi validation. Giá trị enum không biết thì trả lỗi
`role` chứ không phải `400` từ model binder — diagram cần "Display invalid fields", nên phải chỉ ra
được *field nào*.

## UC-06 · `PUT /api/users/{id}`

```json
{ "fullName": "Tên Mới", "email": "moi@trustcabinet.local", "role": "Secretary" }
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

Đây là bước `:Session Service` trong diagram. Chỉ set `is_active = false` là **chưa vô hiệu hoá**
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

## Mã thông báo

FE khớp theo `code`, không khớp theo `message` — đổi chữ trong `message` không được làm FE vỡ.

| `code` | HTTP | Khi nào |
|---|---|---|
| `MSG07` | `201` | Tạo tài khoản thành công |
| `MSG08` | `409` | Email đã có tài khoản khác dùng |
| `MSG09` | `400` | Thông tin không hợp lệ — kèm `errors` |
| `MSG10` | `200` | Cập nhật tài khoản thành công |
| `MSG11` | `200` | Đổi trạng thái thành công (cả hai chiều) |
| `MSG12` | `409` | Không được phép đổi trạng thái |
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

Bốn điểm cố ý:

- **Ghi sau khi lưu thành công**, không phải trước. Ghi trước nghĩa là audit log lưu lại những
  việc chưa chắc đã xảy ra.
- **`actorId` lấy từ token**, không lấy từ body. Một `actorId` do caller tự chọn thì không chứng
  minh được gì, mà đây chính là thứ audit log dùng làm bằng chứng.
- **Không bao giờ có mật khẩu trong `details`**, kể cả đã băm. Audit log nhiều người đọc hơn bảng
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

- **UC-08 — đặt lại mật khẩu** ([ResetUserPassword.png](../06-sequencediagram/ResetUserPassword.png)).
  Bản vẽ đã có, code thì chưa: cần `:Email Service` mà hạ tầng gửi mail chưa có, và `MSG15 - Unable
  to send reset email` trong bản vẽ chỉ có nghĩa khi thật sự có chỗ gửi mail. Bảng `email_messages`
  đã sẵn sàng làm hàng đợi (SCRUM-15), `:Authentication Service` trong bản vẽ đã tồn tại, và token
  đặt lại mật khẩu có thể dùng chung Redis với refresh token. Làm khi SCRUM-15 xong.
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
- **Màn hình FE** (`frontend/src/features/access/UsersPage.tsx`) vẫn là placeholder. Story này chỉ
  làm backend.
