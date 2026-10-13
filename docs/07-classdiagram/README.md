# 07 · Class diagram — các điểm cần sửa cho khớp code

## Sơ đồ theo feature

Tên file theo số Feature mới (FE-01 → FE-15, baseline 10/10/2026). Bản vẽ FE-05 *Meeting Session Management* cũ
đã bị xoá cùng Meeting Session ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).

- [FE-01 — Authentication](feature-01-auth-class-diagram.drawio) ([PNG](feature-01-auth-class-diagram.drawio.png)) — UC-01 → UC-04; Guest access (UC-04) tách riêng khỏi đăng nhập nội bộ, Google là hệ thống ngoài.
- [FE-02 — User Management](feature-02-user-management-class-diagram.drawio) ([PNG](feature-02-user-management-class-diagram.drawio.png)) — UC-05 → UC-09, gồm cả UC-07.
- [FE-03 — Role Management](feature-03-role-management-class-diagram.drawio) ([PNG](feature-03-role-management-class-diagram.drawio.png)) — UC-10 → UC-13; `Role` ◆ `RoleCapability`, `Role`–`User`, `User`–`AuditLogEntry`.
- [FE-04 — Meeting Management](feature-04-meeting-management-class-diagram.drawio) ([PNG](feature-04-meeting-management-class-diagram.drawio.png)) — UC-14 → UC-22.

FE-01 → FE-04 được vẽ lại ngày 13/10/2026, lấy sequence diagram trong
[06-sequencediagram](../06-sequencediagram/) làm nguồn chuẩn.
- [FE-05 — Agenda Management](feature-05-agenda-management-class-diagram.drawio) ([PNG](feature-05-agenda-management-class-diagram.drawio.png)) — UC-23a → UC-23d, UC-24 → UC-26; AgendaItem thuộc thẳng Meeting; mô hình đích theo ERD.
- [FE-06 — Meeting Participant Management](feature-06-meeting-participant-management-class-diagram.drawio) ([PNG](feature-06-meeting-participant-management-class-diagram.drawio.png)) — UC-27 → UC-30; mô hình đích theo ERD và business rules FE-06.
- [FE-13 — Notification](feature-13-notification-management-class-diagram.drawio) ([PNG](feature-13-notification-management-class-diagram.drawio.png)) — UC-66 → UC-68.

## Chuẩn trình bày class diagram

Hai cặp file FE-05 và FE-06 ở trên là **mẫu chuẩn** cho class diagram mới và các lần chỉnh sửa
diagram hiện có. Giữ thống nhất các điểm sau:

- Căn giữa stereotype và tên class trong phần đầu; căn trái attributes, operations và các nội dung
  còn lại bên dưới.
- Đặt chiều ngang class vừa sát dòng dài nhất trong nội dung, chừa một lề nhỏ để chữ không chạm
  viền; tránh để khoảng trống ngang dư thừa.
- Giữ màu theo stereotype/layer, đường phân cách phần tên với nội dung, kiểu đường quan hệ và cách
  sắp xếp các layer như trong hai mẫu.
- Mỗi diagram có cả file `.drawio` để tiếp tục chỉnh sửa và PNG được xuất từ chính file đó. Khi cập
  nhật diagram, xuất lại PNG tương ứng để hai file luôn đồng bộ.

Đối chiếu các bản vẽ trong thư mục này với code backend (SCRUM-11 xác thực, SCRUM-13 quản lý người
dùng, SCRUM-60 ma trận phân quyền). Các phần A → F bên dưới mô tả **bản vẽ cũ** trước ngày 13/10/2026.

- [`feature-01-auth-class-diagram.drawio`](feature-01-auth-class-diagram.drawio) — đối
  chiếu tại **21/09/2026**
- [`feature-02-user-management-class-diagram.drawio`](feature-02-user-management-class-diagram.drawio)
  — đối chiếu tại **21/09/2026**; phần UC-07 đã tách sang bản vẽ 02b (**04/10/2026**), nay vẽ lại trong bản 02
- `feature-02b-password-reset-class-diagram.drawio.png` (*file chưa được commit vào repo*)
  — UC-07 đặt lại mật khẩu, vẽ theo code tại **04/10/2026**, xem [PHẦN D](#phần-d--uc-07-đặt-lại-mật-khẩu-đã-tách-sang-bản-vẽ-02b)
- [`feature-03-role-management-class-diagram.drawio.png`](feature-03-role-management-class-diagram.drawio.png)
  — đối chiếu tại **22/09/2026**, phần `Role` đối chiếu lại **23/09/2026** (`SCRUM-63`), xem [PHẦN F](#phần-f--bản-vẽ-03-ma-trận-phân-quyền-scrum-60)

**Kết luận: kiến trúc đúng, chi tiết lệch.** Phân tầng boundary → control → service → repository →
entity khớp đúng với code. Nhưng tên field, tên method và một số quan hệ thì khác, và có **ba chỗ
lệch về bảo mật** — nếu ai đó implement theo bản vẽ thì sẽ tạo ra lỗ hổng thật.

File này liệt kê từng điểm để sửa. Sắp theo mức ưu tiên, không theo thứ tự bản vẽ.

---

## Cách đọc file này

| Ký hiệu | Nghĩa |
|---|---|
| 🔴 | **Phải sửa** — bản vẽ đang mô tả một thiết kế không an toàn hoặc mâu thuẫn |
| 🟡 | **Nên sửa** — bản vẽ thiếu thành phần có thật, người đọc sẽ hiểu sót luồng |
| ⚪ | **Sửa nếu có thời gian** — khác tên gọi, không gây hiểu sai |

Cột "Code" ghi tên thật trong source để BA copy sang bản vẽ cho chính xác.

---

# PHẦN A — 🔴 Ba chỗ phải sửa

## A1. `findByGoogleEmail(email)` → `findByGoogleSubjectId(subject)`

**Bản vẽ 01**, class `UserRepository`.

| Bản vẽ đang ghi | Code thật |
|---|---|
| `+ findByGoogleEmail(email: string): User` | `FindByGoogleSubjectIdAsync(googleSubjectId: string): User?` |

**Vì sao phải sửa, không phải chuyện đặt tên:**

Đăng nhập Google bằng cách tra theo **email** là một lỗ hổng chiếm tài khoản. Email của một tài
khoản Google **đổi được**, và trên Google Workspace thì sau khi một nhân viên nghỉ việc, địa chỉ
đó **cấp lại được cho người mới**. Tra theo email nghĩa là: người mới đăng nhập Google → hệ thống
tìm thấy tài khoản của người cũ → trao toàn bộ quyền của người cũ cho người mới.

Code tra theo `sub` — mã định danh Google cam kết là duy nhất và **không bao giờ đổi** trong suốt
vòng đời tài khoản. Trong bản vẽ nên ghi là `googleSubjectId` hoặc `googleSub`.

**Cần sửa ở 3 chỗ trên bản vẽ 01:**

1. `UserRepository.findByGoogleEmail(email: string)` → `findByGoogleSubjectId(googleSubjectId: string)`
2. `AuthenticationService.signInWithGoogle(googleEmail: string)` → xem A2
3. `AuthController.signInWithGoogle(googleEmail: string)` → xem A2

---

## A2. `signInWithGoogle(googleEmail)` → `signInWithGoogle(idToken)`

**Bản vẽ 01**, class `AuthController` và `AuthenticationService`.

| Bản vẽ đang ghi | Code thật |
|---|---|
| `+ signInWithGoogle(googleEmail: string): User` | `SignInWithGoogleAsync(idToken: string): GoogleSignInResult` |

**Vì sao phải sửa:**

Bản vẽ cho backend nhận **email trần** từ client. Nghĩa là bất kỳ ai cũng gửi được
`{"googleEmail": "giamdoc@congty.vn"}` và đăng nhập thành giám đốc. Không có gì chứng minh người
gửi thật sự sở hữu địa chỉ đó.

Code nhận **ID token** — một chuỗi có chữ ký số của Google. Backend verify chữ ký đó trước, rồi mới
lấy `sub` và `email` từ bên trong token ra dùng. Client không tự chế được token hợp lệ.

**Cần bổ sung vào bản vẽ 01 một collaborator đang thiếu:**

```
<<service>> GoogleTokenVerifier
+ verify(idToken: string): GoogleIdentity
```

với `GoogleIdentity` là một value object `{ subject: string, email: string }`. Mũi tên:
`AuthenticationService` ---uses---> `GoogleTokenVerifier`.

Luồng đúng để vẽ: `AuthenticationService.signInWithGoogle(idToken)` → gọi `GoogleTokenVerifier.verify()`
→ nhận `GoogleIdentity` → gọi `UserRepository.findByGoogleSubjectId(identity.subject)`.

---

## A3. Bản vẽ 02 thiếu hẳn `TokenService` — thiếu một quy tắc bảo mật

**Bản vẽ 02**, class `UserService`.

Bản vẽ không có mũi tên nào từ `UserService` sang thành phần quản lý token đăng nhập. Code có, và
gọi ở **hai** chỗ:

| Khi nào | Vì sao |
|---|---|
| Vô hiệu hoá tài khoản (UC-06c) | Một dòng đánh dấu "đã vô hiệu hoá" mà token của người đó **vẫn dùng được** thì không phải là đã vô hiệu hoá. Phải huỷ hết token. |
| Đổi vai trò (UC-06b) | Token cũ vẫn mang quyền cũ. Hạ một người từ Admin xuống Member mà không huỷ token thì họ vẫn admin cho tới khi token hết hạn. |

**Cần bổ sung vào bản vẽ 02:**

```
<<service>> TokenService
+ revokeAllForUser(userId: UUID): void
```

Mũi tên: `UserService` ---revokes tokens---> `TokenService`.

### ⚠️ Kèm theo: đổi tên `:Session Service` → `:Token Service`

Sequence diagram UC-06c ở [`docs/06-sequencediagram/`](../06-sequencediagram/) **đã có** participant
này, nhưng đang đặt tên là `:Session Service`. Tên đó gây hiểu nhầm là hệ thống dùng session phía
server — **không phải**, hệ thống dùng JWT. Lý do đầy đủ ở [phụ lục cuối file](#phụ-lục--có-dùng-jwt-không-mà-sao-lại-có-session-server).

Đề nghị đổi tên ở **cả hai loại bản vẽ** cho thống nhất:

| Nơi | Đang là | Đổi thành |
|---|---|---|
| Sequence diagram UC-06c | `:Session Service` | `:Token Service` |
| Class diagram 02 (thêm mới) | — | `TokenService` |

> Phía code đã đổi các comment tham chiếu sang `:Token Service` rồi, nên bản vẽ đổi theo là khớp.
> Tên class trong code vẫn là `IRefreshTokenStore` — bản vẽ không bắt buộc trùng tên code.

> Ghi chú thêm: sequence diagram **UC-06b chưa vẽ** participant này, chỉ UC-06c có. Nhưng code gọi ở
> cả hai. Đây là điểm lệch đã ghi trong
> [`docs/03-api/02-quan-ly-nguoi-dung.md`](../03-api/02-quan-ly-nguoi-dung.md), mục "Năm chỗ code
> khác bản vẽ" — nên bổ sung luôn vào UC-06b.

---

# PHẦN B — 🟡 Thành phần có thật nhưng thiếu trên bản vẽ

## B1. Bản vẽ 01 thiếu 4 collaborator và 2 use case

### Bốn interface không được vẽ

Đây không phải chi tiết implement — mỗi cái là một quyết định thiết kế có ADR hoặc có lý do bảo mật.

| Class cần thêm | Method | Việc nó làm |
|---|---|---|
| `<<service>> PasswordHasher` | `hash(password): string`<br>`verify(passwordHash, providedPassword): bool` | Băm mật khẩu. Không bao giờ lưu mật khẩu thật. |
| `<<service>> TokenIssuer` | `createAccessToken(user, capabilities): string`<br>`createRefreshToken(): string` | Phát JWT và refresh token |
| `<<service>> TokenService` | `store(token, userId, lifetime)`<br>`consume(token): UUID?`<br>`revoke(token)`<br>`revokeAllForUser(userId)` | Lưu refresh token phía server (Redis). **Cùng một thành phần với `TokenService` ở mục A3** — vẽ một lần, dùng ở cả hai bản vẽ. |
| `<<service>> GoogleTokenVerifier` | `verify(idToken): GoogleIdentity` | Đã nói ở A2 |

Tất cả đều là mũi tên `AuthenticationService` ---uses---> ...

### Hai use case không được vẽ

`AuthenticationService` trên bản vẽ có 2 method. Code có **4**:

| Bản vẽ | Code | Ghi chú |
|---|---|---|
| `signIn` | `SignInAsync` | ✅ |
| `signInWithGoogle` | `SignInWithGoogleAsync` | ✅ (sửa tham số, xem A2) |
| — | `RefreshAsync(refreshToken)` | ❌ **thiếu** — UC-03 |
| — | `SignOutAsync(refreshToken)` | ❌ **thiếu** — đăng xuất |

Vòng đời refresh token (access token 15 phút, refresh token xoay vòng dùng-một-lần) là phần lớn
nhất của SCRUM-11 mà bản vẽ hiện không mô tả gì.

Tương ứng, `AuthController` cũng cần thêm `refresh()` và `signOut()`.

---

## B2. Bản vẽ 02 thiếu 3 method của `UserRepository`

| Method cần thêm | Việc nó làm |
|---|---|
| `getCapabilities(role): List<Capability>` | Đọc quyền từ bảng `role_capabilities` |
| `countActiveWithCapability(capability, exceptId): int` | **Quan trọng** — xem B3 |
| `discardChanges(user): void` | Vứt bỏ thay đổi trong bộ nhớ sau khi database từ chối lưu |

---

## B3. Bản vẽ 02 thiếu quy tắc "không được xoá quản trị viên cuối cùng"

Đây là một quy tắc nghiệp vụ, không phải chi tiết kỹ thuật, nên đáng được thể hiện trên bản vẽ.

**Vấn đề:** nếu vô hiệu hoá (hoặc hạ vai trò) người quản trị viên cuối cùng còn hoạt động, thì
**không còn ai vào được màn hình quản lý người dùng nữa** — kể cả để sửa lại. Hệ thống tự khoá
chính nó, và cách duy nhất để cứu là can thiệp thẳng vào database.

Đây là lỗ hổng đã được phát hiện khi review PR #9 và đã sửa.

**Cần thêm vào `UserService` trên bản vẽ 02:**

```
- wouldRemoveTheLastAdministrator(user: User, newRoleId: UUID): bool
```

(để `-` vì là private, nhưng nên hiện ra để người đọc biết quy tắc này tồn tại)

Method này gọi `UserRepository.countActiveWithCapability(AdminUsers, exceptId)` ở B2.

**Lưu ý cho BA khi mô tả quy tắc:** đếm theo **capability `AdminUsers`**, không đếm theo tên vai
trò `"Admin"`. Bảng `role_capabilities` là thứ quyết định ai được làm gì, và nó được thiết kế để
đổi được mà không cần deploy lại — nếu sau này cấp `AdminUsers` cho vai trò `Secretary` thì cách
đếm theo tên `"Admin"` sẽ âm thầm sai.

---

## B4. Bản vẽ 02 thiếu `CallerContext`

`AuditService` cần biết **địa chỉ IP** của người thực hiện hành động để ghi vào bằng chứng. IP đó
lấy từ tầng HTTP.

```
<<service>> CallerContext
+ ipAddress: string
```

Mũi tên: `AuditService` ---uses---> `CallerContext`.

---

# PHẦN C — ⚪ Sai tên, sai kiểu dữ liệu

## C1. `username` không tồn tại trong hệ thống

Xuất hiện **6 chỗ** trên hai bản vẽ. Hệ thống đăng nhập bằng **email**, không có khái niệm
username (ADR-0004: đăng nhập là email + mật khẩu, hoặc Google; không có IdP riêng).

| Bản vẽ | Chỗ | Sửa thành |
|---|---|---|
| 01 | `LoginPage.username: string` | `email: string` |
| 01 | `LoginPage.signIn(username, password)` | `signIn(email, password)` |
| 01 | `AuthController.signIn(username, password)` | `signIn(email, password)` |
| 01 | `UserRepository.findByUsername(username)` | `findByEmail(email)` |
| 01 | `User.username: string` | bỏ hẳn (đã có `email`) |
| 02 | `UserRepository.existsByUsername(username)` | bỏ hẳn (đã có `existsByEmail`) |

---

## C2. `AccountStatus` không tồn tại — và hai bản vẽ mâu thuẫn nhau

Bản vẽ 02 dùng kiểu `AccountStatus` ở **ba** chỗ. Bản vẽ 01 lại ghi cùng field đó là `status: string`.
Code thì dùng `isActive: bool`.

Ba bên đang nói ba kiểu khác nhau cho cùng một thứ.

| Bản vẽ | Chỗ | Sửa thành |
|---|---|---|
| 02 | `User.status: AccountStatus` | `isActive: bool` |
| 02 | `UserService.changeStatus(userId, status: AccountStatus)` | `changeStatus(userId, isActive: bool)` |
| 02 | `UserController.changeStatus(userId, status: AccountStatus)` | `changeStatus(userId, isActive: bool)` |
| 02 | `AuditService.recordStatusChange(userId, status: AccountStatus)` | xem C5 |
| 01 | `User.status: string` | `isActive: bool` |

Không cần tạo enum `AccountStatus`. Tài khoản chỉ có hai trạng thái, `bool` là đủ — thêm enum cho
hai giá trị là phức tạp hoá không cần thiết.

---

## C3. Entity `User` — sai 5 field, thiếu 4 field

Đây là chỗ lệch nhiều nhất. Bảng đối chiếu đầy đủ:

| Bản vẽ 02 đang ghi | Code thật | Xử lý |
|---|---|---|
| `userId: UUID` | `Id: Guid` | ⚪ đổi tên cho khớp |
| `username: string` | — | ❌ **bỏ** (C1) |
| `email: string` | `Email: string` | ✅ giữ |
| `fullName: string` | `FullName: string` | ✅ giữ |
| `jobTitle: string` | — | ❌ **bỏ** — không có trong ERD |
| `phone: string` | — | ❌ **bỏ** — không có trong ERD |
| `status: AccountStatus` | `IsActive: bool` | ❌ **sửa** (C2) |
| — | `RoleId: Guid` (+ navigation `Role`) | ➕ **thiếu** — xem bên dưới |
| — | `PasswordHash: string?` | ➕ **thiếu** |
| — | `GoogleSubjectId: string?` | ➕ **thiếu** |
| — | `CreatedAt: DateTimeOffset` | ➕ **thiếu** |

**`Role` là field thiếu nghiêm trọng nhất.** Toàn bộ UC-06b xoay quanh việc đổi vai trò, và quy tắc
"quản trị viên cuối cùng" ở B3 cũng dựa vào nó. Bản vẽ hiện không có field này ở đâu cả.

`Role` có 4 giá trị: `Chair`, `Secretary`, `Member`, `Admin`.

**`PasswordHash` và `GoogleSubjectId` đều cho phép null**, và một tài khoản phải có ít nhất một
trong hai (ràng buộc ở database). Một tài khoản chỉ đăng nhập bằng Google thì không có mật khẩu —
đó là lý do hai field này nullable chứ không phải thiếu sót.

---

## C4. Entity `User` — bỏ 3 method

Bản vẽ 02 vẽ `activate()`, `deactivate()`, `isActive()` trên entity. Bản vẽ 01 vẽ `isActive()` và
`hasSystemAccess()`.

Code để entity **thuần dữ liệu** — không có method nghiệp vụ nào. Mọi quy tắc nằm ở service, vì
việc vô hiệu hoá một tài khoản còn phải huỷ phiên đăng nhập và ghi audit log, mà entity thì không
biết gì về hai thứ đó.

Đề nghị: bỏ hết phần method của `User` ở cả hai bản vẽ.

---

## C5. `AuditService` — gộp 4 method thành 1

| Bản vẽ 02 | Code thật |
|---|---|
| `recordUserCreation(userId)` | `record(action, objectType, objectId, actorId, details)` |
| `recordUserUpdate(userId)` | ↑ cùng một method |
| `recordStatusChange(userId, status)` | ↑ |
| ~~`recordPasswordReset(userId)`~~ | ↑ — đã bỏ khỏi bản vẽ 02, UC-07 vẽ ở bản vẽ 02b (xem D) |

Code có **một** method chung, phân biệt bằng tham số `action` nhận một trong các mã:

- `user.created`
- `user.updated`
- `user.deactivated`
- `user.reactivated`
- `user.password_reset_requested` (UC-07, người thực hiện là quản trị viên)
- `user.password_reset` (UC-07, người thực hiện là chính chủ tài khoản)

Vẽ một method chung sẽ sát thực tế hơn, và cũng thể hiện đúng ý định: audit log là bảng chung cho
toàn hệ thống, các story sau (họp, tài liệu, biên bản) sẽ ghi vào cùng chỗ với cùng method này.

**Một tham số đáng nhắc trên bản vẽ:** `actorId` — **ai** là người thực hiện. Bản vẽ hiện chỉ có
`userId` (người *bị* tác động), thiếu mất người *gây ra* tác động. Audit log không trả lời được
"ai làm" thì mất phần lớn giá trị.

---

## C6. `UserRepository.update()` — bỏ

Bản vẽ 02 có `+ update(user: User): User`. Code không có.

Lý do: entity đã được ORM theo dõi sẵn, nên sửa giá trị rồi gọi `saveChanges()` là đủ. Thêm
`update()` sẽ khiến người đọc tưởng phải gọi tường minh.

Nên thay bằng `+ saveChanges(): void`.

---

## C7. `existsByEmail` thiếu tham số

| Bản vẽ 02 | Code thật |
|---|---|
| `existsByEmail(email: string): bool` | `emailExists(email: string, exceptId: UUID?): bool` |

Tham số thứ hai cần cho UC-06b: khi sửa thông tin mà **giữ nguyên email của chính mình** thì đó
không phải là trùng email. Thiếu tham số này thì không ai sửa được tên của chính mình.

---

# PHẦN D — UC-07 (đặt lại mật khẩu): đã tách sang bản vẽ 02b

**Đã xử lý ngày 04/10/2026, theo Cách 2** của đề xuất cũ: UC-07 đã có code (PR #56, #58, #61), nên
phần đặt lại mật khẩu được vẽ lại theo code ở một bản vẽ riêng,
`feature-02b-password-reset-class-diagram.drawio.png` (file chưa được commit vào repo).
Bản vẽ 02 bỏ các thành phần UC-07 cũ.

| Bản vẽ 02 cũ | Bản vẽ 02b (theo code) |
|---|---|
| `UserManagementPage.resetUserPassword()` | giữ, thêm boundary thứ hai `ResetPasswordPage.submitNewPassword()` — nửa sau của UC-07, người dùng đặt mật khẩu bằng liên kết |
| `UserController.resetPassword(userId)` | `UserController.requestPasswordReset(userId)` + `AuthController.resetPassword(resetCode, newPassword)` (ẩn danh) |
| `UserService.initiatePasswordReset(userId)` | `requestPasswordReset(userId, actorId)` + `resetPassword(resetCode, newPassword)` |
| `UserService.isPasswordResetEligible(user)` | bỏ — điều kiện "đang hoạt động **và** có mật khẩu" (BR-22) được kiểm thẳng trong `requestPasswordReset` và `resetPassword`, không có method riêng |
| `AuthenticationService.createPasswordResetRequest(userId)` | `PasswordResetTokenStore` — `create` / `activate` / `peek` / `consume` / `revoke` |
| `EmailService.sendPasswordResetEmail(user)` | `PasswordResetEmailSender.send(...)`: bool — gửi hỏng là một nhánh nghiệp vụ (MSG15), không phải exception |
| `AuditService.recordPasswordReset(userId)` | `AuditService.record(action, objectType, objectId, actorId, details)` |
| — | thêm `<<service>> TokenService.revokeAllForUser()` (kết thúc phiên khi đặt lại xong), `PasswordHasher.hash()` và `AuthController.signOut()` (AF-1: trình duyệt đang đăng nhập) |

**Mâu thuẫn "hai đường cho cùng một việc"** (`UserService` vừa gọi `EmailService` vừa gọi
`AuthenticationService`) được giải theo code: `UserService` điều phối, gọi riêng
`PasswordResetTokenStore` (tạo / kích hoạt / tiêu liên kết) và `PasswordResetEmailSender` (gửi mail).
Liên kết chỉ được kích hoạt **sau khi** gửi mail thành công, nên thứ tự này nằm ở `UserService`, không
nằm trong kho token.

Sequence diagram tương ứng: `UC07_ResetUserPassword` trong
[`06-sequencediagram/user-management/`](../06-sequencediagram/user-management/).

---

# PHẦN E — Ghi chú chung, không cần sửa

Mấy điểm sau **không phải lỗi**, ghi ra để BA khỏi mất công "sửa" nhầm:

**`Controller` trong code là Minimal API, không phải class `Controller`.** Code dùng
`UserEndpoints` / `AuthEndpoints` — tập hợp các hàm khai báo route, không phải class kế thừa
`ControllerBase`. Về vai trò thì giống hệt `<<control>>` trên bản vẽ, nên **cứ vẽ là Controller**,
người đọc hiểu đúng. Đây là lựa chọn framework, không phải lệch thiết kế.

**Tên method trong code có hậu tố `Async` và trả về `Task<...>`.** `ListAsync`, `CreateAsync`…
Đây là quy ước bắt buộc của C# cho hàm bất đồng bộ. Bản vẽ **không cần** ghi `Async` — sẽ làm rối
mà không thêm thông tin.

**Code tách `IUserService` (interface) và `UserService` (implementation).** Bản vẽ vẽ một class là
đủ.

**Có hai `UserRepository` khác nhau trong code**, cố ý tách:

| Code | Dùng cho | Ghi chú |
|---|---|---|
| `Authentication.IUserRepository` | Đăng nhập | Chỉ đọc. Không có method ghi nào. |
| `Users.IUserAccountRepository` | Quản lý người dùng | Đọc + ghi |

Tách để đường đăng nhập không đụng được vào method ghi — không có code nào trên luồng sign-in tạo
hay sửa được tài khoản.

Hai bản vẽ đang mỗi bên vẽ một `UserRepository` với method khác nhau, **tình cờ lại đúng**. Nếu
muốn rõ hơn thì đổi tên bản vẽ 02 thành `UserAccountRepository`, nhưng không bắt buộc.

---

# PHẦN F — Bản vẽ 03: ma trận phân quyền (SCRUM-60)

Đối chiếu tại **22/09/2026**, sau khi code UC-10 và UC-11.

**Kết luận: phân tầng đúng hoàn toàn.** `Controller` → `Service` → `Repository` → `Database`, cộng
`AuditService`, khớp 1-1 với code. Tên method cũng khớp gần hết. Chỉ lệch ở **mô hình dữ liệu**:
bản vẽ mô tả `Role` và `Permission` là hai bảng, code thì cả hai là enum.

Chi tiết đầy đủ ở [`docs/03-api/03-ma-tran-phan-quyen.md`](../03-api/03-ma-tran-phan-quyen.md),
mục "Những chỗ code khác bản vẽ". Tóm tắt phần cần sửa trên bản vẽ:

## F1. 🔴 `Permission` không phải entity — mâu thuẫn với tiêu chí nghiệm thu

| Bản vẽ 03 đang ghi | Code thật |
|---|---|
| `<<entity>> Permission` với `permissionId: UUID`, `code`, `name`, `description`, `functionalGroup` | `enum Capability` (4 giá trị) + value object `PermissionDescriptor` đọc từ enum |

**Vì sao phải sửa:** tiêu chí nghiệm thu của chính `SCRUM-60` ghi *"Không tạo được quyền mới:
`Capability` là enum trong code. Admin sửa mapping, không thêm capability. Màn hình không được có
nút Tạo quyền mới."* Bản vẽ đang vẽ một bảng `permissions` — tức đúng cái thứ tiêu chí nghiệm thu
cấm.

Không phải chuyện ngại làm bảng: một quyền chỉ có ý nghĩa vì **có code kiểm nó**. Dòng quyền do
quản trị viên tự thêm sẽ không gác cửa cái gì cả — trông như bảo mật mà không phải bảo mật.

**Cần sửa:** đổi `<<entity>> Permission` → `<<value object>> Permission`, **bỏ `permissionId`**,
ghi chú "đọc từ enum `Capability`, không có bảng".

## F2. ✅ ~~`Role` không phải entity, không có bảng `roles`~~ — đã giải quyết bằng `SCRUM-63`

| Bản vẽ 03 đang ghi | Code thật |
|---|---|
| `<<entity>> Role` với `roleId: UUID`, `roleName`, `description`, `isBuiltIn` | `enum SystemRole` — `Chair`, `Secretary`, `Member`, `Admin` |

Theo [ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md): không có bảng `roles`, không
thêm vai trò mới mà không deploy. `role_capabilities` lưu **tên enum** ở cả hai cột.

`isBuiltIn` vẫn giữ trên API (luôn `true`) vì màn hình cần lý do để hiện tên vai trò dạng chữ cố
định thay vì ô sửa được — nhưng nó là thuộc tính của response, không phải cột trong bảng.

**Cần sửa:** đổi `<<entity>> Role` → `<<enum>> SystemRole`, bỏ `roleId` và `description`.

> **Đã giải quyết — BA đừng sửa phần `Role` trên bản vẽ.** ADR-0004 dẫn ở trên không nói gì về vai
> trò. [ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) (đã chấp nhận, `SCRUM-63`) chuyển vai
> trò thành bảng `roles` với `id`, `name`, `description`, `isBuiltIn` — **code giờ đã khớp bản vẽ**
> ([feature-03-role-management-class-diagram.drawio.png](feature-03-role-management-class-diagram.drawio.png):
> `RoleController`, `RoleService`, `RoleRepository`). Bảng phía trên chỉ còn là lịch sử. Phần
> `Permission` ở F1 thì vẫn đúng: `Capability` vẫn là enum.

> Kèm theo: quan hệ `Role 0..* --grants--> Permission 0..*` trên bản vẽ nên ghi rõ là bảng
> `role_capabilities` — đó là bảng **duy nhất** thật sự tồn tại trong cụm này.

## F3. 🟡 `resetRolePermissionsToDefault()` — chưa làm, cần đánh dấu

Bản vẽ có method này ở cả 4 tầng (`Controller`, `Service`, `Repository`, và
`confirmResetToDefault()` ở `Page`). Nhưng ticket **không có tiêu chí nghiệm thu nào** cho nó, và
**không có sequence diagram** (chỉ có UC-10 và UC-11).

Đã thống nhất không làm trong sprint này.

**Cần sửa — chọn một** (UC-07 ở bản vẽ 02 từng có cùng hai lựa chọn này, xem [PHẦN D](#phần-d--uc-07-đặt-lại-mật-khẩu-đã-tách-sang-bản-vẽ-02b)):

- Bỏ hẳn 4 method đó khỏi bản vẽ, hoặc
- Thêm ghi chú `<<chưa implement>>` lên từng cái.

## F4. ⚪ `getPermissionCatalog()` gộp chung vào một endpoint

Bản vẽ tách `getRolePermissionMatrix()` và `getPermissionCatalog()` thành hai method ở
`Controller`. Code trả cả hai trong **một** response của `GET /api/roles/capabilities`.

Màn hình luôn cần cả hai cùng lúc để vẽ lưới. Tách thành hai request chỉ thêm một round trip và
một khoảng thời gian hai nửa dữ liệu lệch nhau.

Ở tầng `Service` thì code **vẫn có hai đường đọc riêng**, nên về phân tầng bản vẽ không sai. Sửa
hay không tuỳ BA; nếu giữ thì nên ghi chú rằng hai method này đi chung một endpoint.

## F5. ⚪ Thiếu `:Token Service` — cùng vấn đề với A3 của bản vẽ 02

Bản vẽ 03 không có mũi tên nào sang thành phần quản lý token, nhưng code **có gọi**: đổi ma trận
xong thì huỷ refresh token của mọi tài khoản đang giữ vai trò đó.

Lý do giống hệt A3: capability nằm **bên trong access token**, nên không huỷ thì người giữ vai trò
đó vẫn dùng bộ quyền cũ tới khi token hết hạn (tối đa 15 phút). Huỷ ở **cả hai chiều** — thêm
quyền cũng phải huỷ, vì token cũ không chứa quyền mới.

**Cần bổ sung:** mũi tên `RolePermissionService` ---revokes tokens---> `TokenService` (cùng một
`TokenService` đã đề nghị thêm ở A3, vẽ một lần dùng chung cả ba bản vẽ).

Sequence diagram UC-11 cũng thiếu participant này — nên bổ sung luôn cho khớp.

## F6. ⚪ Thiếu luật chống tự khoá trên bản vẽ

`RolePermissionService` trên bản vẽ có `validatePermissionConfiguration(permissionIds): bool`, đúng
về vai trò. Nhưng luật quan trọng nhất nó thực thi thì không thể hiện ra:

**không được gỡ `AdminUsers` khỏi vai trò cuối cùng còn giữ nó** — vì `AdminUsers` chính là quyền
mở màn hình này, nên một ma trận không ai giữ nó thì chỉ sửa lại được bằng cách vào thẳng database.

Giống B3 ở bản vẽ 02, đây là **quy tắc nghiệp vụ**, đáng hiện lên bản vẽ:

```
- wouldRemoveTheLastAdminCapability(role, newCapabilities): bool
```

**Lưu ý cho BA khi mô tả:** kiểm trên **toàn ma trận**, không phải "Admin luôn phải giữ
AdminUsers". Chuyển `AdminUsers` sang `Secretary` rồi gỡ khỏi `Admin` là **hợp lệ** — đó chính là
lý do bảng này tồn tại. Để nó không ở đâu cả mới là không hợp lệ.

---

# Tóm tắt checklist

**🔴 Phải sửa (4 — F2 đã rút lại):**

- [ ] A1 — `findByGoogleEmail` → `findByGoogleSubjectId` (bản vẽ 01)
- [ ] A2 — `signInWithGoogle(googleEmail)` → `signInWithGoogle(idToken)` + thêm `GoogleTokenVerifier` (bản vẽ 01)
- [ ] A3 — thêm `TokenService.revokeAllForUser()` (bản vẽ 02) **+ đổi tên `:Session Service` → `:Token Service` ở sequence diagram UC-06c**
- [ ] F1 — `<<entity>> Permission` → `<<value object>>`, bỏ `permissionId` (bản vẽ 03) — **mâu thuẫn tiêu chí nghiệm thu**
- [x] ~~F2 — `<<entity>> Role` → `<<enum>> SystemRole`~~ — rút lại: code đã theo bản vẽ (`SCRUM-63`, ADR-0008)

**🟡 Nên sửa (5):**

- [ ] B1 — thêm `PasswordHasher`, `TokenIssuer`, `TokenService`, `GoogleTokenVerifier` + 2 method `refresh` / `signOut` (bản vẽ 01)
- [ ] B2 — thêm 3 method cho `UserRepository` (bản vẽ 02)
- [ ] B3 — thêm `wouldRemoveTheLastAdministrator()` (bản vẽ 02)
- [ ] B4 — thêm `CallerContext` (bản vẽ 02)
- [ ] F3 — `resetRolePermissionsToDefault()`: bỏ hẳn hoặc đánh dấu `<<chưa implement>>` (bản vẽ 03)

**⚪ Sửa nếu có thời gian (10):**

- [ ] C1 — bỏ `username` ở 6 chỗ
- [ ] C2 — `AccountStatus` → `isActive: bool` ở 5 chỗ
- [ ] C3 — entity `User`: bỏ 3 field, thêm 4 field
- [ ] C4 — bỏ method trên entity `User`
- [ ] C5 — gộp 4 method của `AuditService` thành 1, thêm `actorId`
- [ ] C6 — `update()` → `saveChanges()`
- [ ] C7 — `existsByEmail` thêm tham số `exceptId`
- [ ] F4 — ghi chú `getPermissionCatalog()` đi chung endpoint với `getRolePermissionMatrix()` (bản vẽ 03)
- [ ] F5 — thêm `TokenService` (bản vẽ 03) **+ bổ sung participant đó vào sequence diagram UC-11**
- [ ] F6 — thêm `wouldRemoveTheLastAdminCapability()` (bản vẽ 03)

**Cần quyết (đã xong):**

- [x] D — UC-07: đã tách bản vẽ riêng `feature-02b-password-reset` (04/10/2026)

---

Có gì chưa rõ thì hỏi lại BE, đặc biệt là phần A — ba điểm đó nên thống nhất trước khi FE bắt đầu
làm màn hình theo bản vẽ.

---

# Phụ lục — "Có dùng JWT không mà sao lại có session server?"

Câu hỏi đã được nêu khi đọc bản nháp trước của file này. Ghi lại ở đây để lần sau khỏi hỏi lại.

**Có dùng JWT.** Và thành phần bị gọi nhầm là "session server" thực ra là **kho refresh token** —
không phải session truyền thống. Đó chính là lý do file này đề nghị đổi tên thành `TokenService`.

## Hệ thống phát hai loại token khi đăng nhập

| | Access token | Refresh token |
|---|---|---|
| Là gì | **JWT** — tự chứa thông tin | Chuỗi ngẫu nhiên, vô nghĩa nếu đứng một mình |
| Sống bao lâu | **15 phút** | **7 ngày** |
| Lưu ở đâu | Chỉ ở trình duyệt | Trình duyệt **+ server (Redis)** |
| Dùng làm gì | Gửi kèm mọi request | Chỉ để xin access token mới |
| **Huỷ được không** | **Không** | **Có, ngay lập tức** |

## Vì sao không dùng mỗi JWT?

**JWT không huỷ được** — đó là bản chất, không phải thiếu sót. Server ký một lần rồi thôi; sau đó
chỉ kiểm chữ ký chứ không hỏi lại database. Nhanh, nhưng đánh đổi là: đã phát ra thì còn giá trị
đến khi hết hạn, không rút lại được.

Nếu chỉ có JWT thì ba việc sau **không làm được**:

| Tình huống | Nếu chỉ có JWT |
|---|---|
| Admin vô hiệu hoá tài khoản người vừa nghỉ việc | Người đó **vẫn dùng hệ thống bình thường** tới khi token hết hạn |
| Người dùng bấm "Đăng xuất" | Chỉ xoá token khỏi trình duyệt. Ai copy được token trước đó **vẫn dùng tiếp** |
| Hạ một người từ Admin xuống Member | Token cũ **vẫn mang quyền Admin** |

Tức là "đăng xuất" và "vô hiệu hoá" sẽ chỉ là hình thức.

## Vì sao không dùng mỗi session server?

Thì mỗi request đều phải tra database/Redis để biết người gọi là ai. Chậm, và tạo một điểm phụ
thuộc cho toàn hệ thống.

## Cách kết hợp

- **Access token (JWT, 15 phút)** — kiểm nhanh, không chạm database. Dùng cho hầu hết request.
- **Refresh token (7 ngày, lưu server)** — khi access token hết hạn, trình duyệt tự đổi lấy cái
  mới. **Lúc này server đọc lại tài khoản từ database**: đã bị vô hiệu hoá hoặc đổi vai trò thì
  không cấp nữa.

Con số **15 phút** chính là cái giá của việc dùng JWT: đó là khoảng thời gian tối đa mà một tài
khoản vừa bị vô hiệu hoá vẫn còn dùng được. Ngắn để thu hẹp khoảng trống đó, nhưng không quá ngắn
để khỏi phải làm mới liên tục.

Thêm: refresh token là **dùng một lần** — mỗi lần làm mới sẽ cấp cái mới và huỷ cái cũ. Nên nếu ai
lấy trộm được, nó sẽ hết tác dụng ngay khi người dùng thật làm mới lần kế tiếp.

## Riêng màn hình quản lý người dùng thì chặt hơn

Vì 15 phút vẫn là quá lâu cho một màn hình có quyền tạo/vô hiệu hoá tài khoản, nên mọi endpoint
`/api/users` **đọc lại quyền từ database ở từng request**, không tin vào token. Ai vừa bị hạ quyền
sẽ nhận `403` **ngay lập tức**, không chờ 15 phút.

Đây cũng là lý do mục B3 (quy tắc "không được vô hiệu hoá quản trị viên cuối cùng") đáng được thể
hiện trên bản vẽ: hai lớp bảo vệ đó cùng chặn một kịch bản — hệ thống tự khoá chính nó.
