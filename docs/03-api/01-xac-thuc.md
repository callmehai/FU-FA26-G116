# 03 · Xác thực — đăng nhập và đăng xuất

Hiện thực **UC-01, UC-02 và UC-03 (SCRUM-11)**: đăng nhập bằng email + mật khẩu, đăng nhập bằng
Google, và đăng xuất. Chỉ hai phương thức đăng nhập này, không dựng IdP riêng — xem
[ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md).

Sequence diagram gốc chia bốn participant: `Login Page` → `:Auth Controller` →
`:Authentication Service` → `:User Repository` → `Database`. Code đi theo đúng bốn lớp đó:

| Participant trong diagram | Code |
|---|---|
| `:Auth Controller` | [`backend/TrustCabinet.Web/Endpoints/AuthEndpoints.cs`](../../backend/TrustCabinet.Web/Endpoints/AuthEndpoints.cs) |
| `:Authentication Service` | [`backend/TrustCabinet.Infrastructure/Authentication/AuthenticationService.cs`](../../backend/TrustCabinet.Infrastructure/Authentication/AuthenticationService.cs) |
| `:User Repository` | [`backend/TrustCabinet.Infrastructure/Authentication/UserRepository.cs`](../../backend/TrustCabinet.Infrastructure/Authentication/UserRepository.cs) |
| `:Google` (UC-02) | [`backend/TrustCabinet.Infrastructure/Authentication/GoogleTokenVerifier.cs`](../../backend/TrustCabinet.Infrastructure/Authentication/GoogleTokenVerifier.cs) |
| `Database` | bảng `users` và `role_capabilities` |

### Thay đổi database

Migration `AddGoogleSignIn` (bắt buộc cho UC-02, vì `password_hash` trước đó là `NOT NULL` nên tài
khoản chỉ đăng nhập bằng Google không lưu được):

- `users.password_hash` → **nullable**
- thêm `users.google_subject_id`, unique có filter `IS NOT NULL`
- check constraint `ck_users_has_a_way_to_sign_in`: `password_hash IS NOT NULL OR google_subject_id
  IS NOT NULL`

Check constraint đặt ở database chứ không chỉ trong application: migration, seed và console của DBA
đều ghi vào bảng này, nên một dòng không có cách nào đăng nhập là dòng không nên tồn tại. Unique có
filter để nhiều tài khoản chỉ dùng mật khẩu (`google_subject_id` null) vẫn hợp lệ, nhưng một Google
identity không thể liên kết vào hai tài khoản.

**`Down` cố tình từ chối chạy** khi còn tài khoản chỉ đăng nhập bằng Google. Rollback sẽ xoá cột
`google_subject_id` và đặt `password_hash = ''` (giá trị này thoả `NOT NULL`, nên rollback *thành
công* trong khi phá credential): kết quả là tài khoản không ai đăng nhập được nữa và liên kết Google
đã mất, không cứu lại được. Nên nó `RAISE EXCEPTION` kèm số tài khoản bị ảnh hưởng, buộc người
rollback tự quyết định trước — cấp mật khẩu cho họ, hoặc xoá hẳn dòng đó.

## Endpoint

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/auth/login` | không cần | Đăng nhập bằng mật khẩu, cấp AT + RT |
| POST | `/api/auth/google` | không cần | Đăng nhập bằng Google, cấp AT + RT |
| POST | `/api/auth/refresh` | không cần (RT là thứ chứng minh) | Đổi RT cũ lấy cặp mới |
| POST | `/api/auth/logout` | không cần | Huỷ RT ở phía server |
| GET | `/api/auth/me` | Bearer AT | Thông tin người đang đăng nhập |

### Refresh token nằm trong cookie `tc_refresh_token`

`/login`, `/google` và `/refresh` khi thành công đều đặt RT vào cookie:

```
Set-Cookie: tc_refresh_token=8Kx2v…; max-age=604800; path=/api/auth; secure; samesite=strict; httponly
```

| Thuộc tính | Vì sao |
|---|---|
| `HttpOnly` | JavaScript không đọc được, nên lỗi XSS trên trang không lấy được RT |
| `Secure` | Chỉ gửi qua HTTPS. Trình duyệt coi `http://localhost` là ngữ cảnh an toàn nên dev vẫn chạy |
| `SameSite=Strict` | Trang khác không gọi `/api/auth/refresh` bằng cookie của người dùng được |
| `Path=/api/auth` | Cookie chỉ đi kèm request tới `/api/auth/*`, không bám theo mọi request API |
| `Max-Age` | Bằng tuổi RT (`RefreshTokenDays`, mặc định 7 ngày = `604800` giây) |

Không dùng tiền tố `__Host-` vì tiền tố đó bắt buộc `Path=/`, tức gửi RT kèm mọi request.

RT **chỉ** nằm trong cookie: response body không có `refreshToken`, và `/refresh`, `/logout` chỉ đọc
cookie — gửi RT trong body bị bỏ qua. Không có đường lùi qua body là cố ý: RT mà JavaScript gửi
được là RT mà JavaScript giữ được, đúng thứ cookie `HttpOnly` sinh ra để ngăn. `/refresh` không có
cookie thì trả `401 MSG01`, y như RT hết hạn.

Chuyển sang cookie theo comment của SCRUM-11, gộp cả ba bước (backend đặt cookie, FE bỏ
`localStorage`, backend bỏ RT khỏi body) vào một PR. Phiên đăng nhập từ trước PR này chỉ có RT
trong `localStorage`, không có cookie, nên người dùng phải đăng nhập lại **một lần**; FE tự xoá key
cũ.

## UC-01 · Đăng nhập bằng email và mật khẩu

Sequence diagram: [SignIn.png](../06-sequencediagram/SignIn.png).

### `POST /api/auth/login`

```json
{ "email": "thuky@trustcabinet.local", "password": "…" }
```

`200 OK`, kèm `Set-Cookie: tc_refresh_token=…` (xem trên):

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs…",
  "accessTokenExpiresAt": "2026-09-21T10:15:00+00:00",
  "user": {
    "id": "01999f2e-…",
    "email": "thuky@trustcabinet.local",
    "fullName": "Nguyễn Thư Ký",
    "roleId": "01a0ce78-7203-7c7f-8326-7a0baebf4c22",
    "roleName": "Thư ký",
    "capabilities": ["MeetingCreator", "MinutesEditor"]
  }
}
```

Vai trò trả về bằng `roleId` + `roleName` (tên hiện tại trong bảng `roles`), không còn chuỗi enum
`"role": "Secretary"` — đổi từ `SCRUM-63`, xem [04-quan-ly-vai-tro.md](04-quan-ly-vai-tro.md). Shape
này dùng chung cho `/login`, `/google`, `/refresh` và `/me`.

Các nhánh lỗi đều trả `401` kèm mã thông báo của UC-01. FE khớp theo `code`, không khớp theo
`message` — đổi chữ trong `message` không được làm FE vỡ.

| `code` | Khi nào | `message` |
|---|---|---|
| `MSG01` | Email không tồn tại, **hoặc** sai mật khẩu, **hoặc** bỏ trống | `Email hoặc mật khẩu không đúng.` |
| `MSG03` | Tài khoản đã bị vô hiệu hoá (`users.is_active = false`) | `Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.` |

> **`MSG04` ("chưa được cấp quyền truy cập hệ thống") đã bỏ từ `SCRUM-63`.** Nó từ chối vai trò
> không có capability nào trừ `Member`; khi vai trò thành dữ liệu (ADR-0008), vai trò tự tạo không
> có quyền nào là cấu hình hợp lệ, hoạt động y như `Member`. Giờ mọi vai trò không có quyền đều đăng
> nhập được, token đơn giản không mang capability nào. **Chờ BA sửa đặc tả UC-01.**

Vài điểm cố ý, đừng "sửa" lại:

- **`MSG01` gộp ba tình huống.** Tiêu chí nghiệm thu của SCRUM-11: lỗi chung chung, không tiết lộ
  email có tồn tại hay không. Tách ra thành "email không tồn tại" là làm hỏng yêu cầu này.
- **Kiểm mật khẩu trước, kiểm `is_active` sau.** Nếu trả `MSG03` trước khi so mật khẩu thì người
  không biết mật khẩu vẫn biết được địa chỉ đó là tài khoản thật.
- **Email không tồn tại vẫn phải băm một lần.** Băm mật khẩu chậm có chủ đích, nên bỏ qua bước này
  khi không tìm thấy user sẽ làm nhánh "không có tài khoản" trả lời nhanh hơn nhánh "sai mật
  khẩu" — chênh lệch thời gian đó đủ để dò ra email nào đã đăng ký. Xem `DummyHash`.
- **Vai trò không có capability nào vẫn đăng nhập được** — `Member`, hay một vai trò tự tạo chưa
  gán quyền. Dự họp, đọc tài liệu và biểu quyết không việc nào cần capability (xem
  `ReferenceData.RoleCapabilities`).

## UC-02 · Đăng nhập bằng Google

Sequence diagram: [SignInWithGoogle.png](../06-sequencediagram/SignInWithGoogle.png).

Chia việc giữa FE và BE đúng như diagram: `LoginPage` mở Google và tự xử lý nhánh ngoài (Google
lỗi hoặc người dùng huỷ), **xong rồi** mới gọi `AuthController`. Nghĩa là nhánh ngoài không bao giờ
đến backend — không có ID token nào để gửi lên, nên MSG01 của nhánh đó do FE tự hiển thị.

### `POST /api/auth/google`

```json
{ "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6..." }
```

`200 OK` trả về **đúng shape** của `/api/auth/login` — client không cần biết phiên được tạo bằng
cách nào, chỉ cần biết đã có phiên.

> **Chú ý mã thông báo:** UC-02 đánh số khác UC-01 cho cùng tình huống. Tài khoản bị vô hiệu hoá là
> `MSG03` ở UC-01 nhưng `MSG04` ở đây. ("Không có quyền truy cập" — `MSG04` ở UC-01, `MSG05` ở đây —
> đã bỏ ở cả hai từ `SCRUM-63`.) Đây là lý do code có **hai** enum outcome riêng (`SignInOutcome` và `GoogleSignInOutcome`)
> chứ không dùng chung — gộp lại là một trong hai use case bị lấy mã của cái kia. Đừng "dọn" cho
> giống nhau.

| `code` | Khi nào | `message` |
|---|---|---|
| `MSG01` | ID token không verify được: thiếu, sai định dạng, hết hạn, sai audience, sai chữ ký, hoặc email chưa verified | `Đăng nhập bằng Google không thành công.` |
| `MSG03` | Google xác thực OK nhưng **chưa có tài khoản nội bộ nào liên kết** với Google identity đó, và cũng không có tài khoản nào tự liên kết được (xem dưới) | `Tài khoản Google này chưa được cấp quyền truy cập hệ thống.` |
| `MSG04` | Tài khoản bị vô hiệu hoá: đã liên kết, **hoặc** chưa liên kết nhưng email trùng email Google đã xác minh | `Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.` |

### Liên kết theo `sub`, không theo email

Tài khoản nội bộ liên kết với Google qua cột `users.google_subject_id` — chính là claim `sub` của
ID token.

Đã liên kết rồi thì **chỉ** so theo `sub`, không theo email. `sub` được Google bảo đảm ổn định và
duy nhất suốt đời tài khoản, còn địa chỉ email thì đổi được, và trên domain workspace còn có thể bị
cấp lại cho người khác. Match theo email mỗi lần nghĩa là người được cấp lại địa chỉ đó sẽ vào được
tài khoản của người cũ.

### Tự liên kết ở lần đăng nhập đầu

Email chỉ được dùng **một lần** cho mỗi tài khoản: lần đăng nhập Google đầu tiên
([ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md)). Không tìm thấy theo
`sub` thì tìm tài khoản thoả cả ba: `email` trùng email trong token (`citext`), email đó Google đã
xác minh, `google_subject_id IS NULL` và `is_active = true`. Có thì lưu `sub` vào đó (một câu
`UPDATE ... WHERE`), ghi audit `user.google_linked`, rồi đi tiếp như bình thường.

Tài khoản trùng email nhưng **đã bị vô hiệu hoá trước khi kịp liên kết** thì không liên kết, và trả
`MSG04` (không phải `MSG03`): tài khoản có tồn tại, bảo "chưa được cấp quyền" là sai. Người gọi đã
được Google xác minh là chủ email đó, nên cho họ biết tài khoản của mình bị khoá không lộ gì. Tài
khoản trùng email nhưng đã liên kết với `sub` **khác** thì vẫn `MSG03` — người gọi không phải chủ.

Nghĩa là: admin tạo tài khoản bằng Gmail của ai (`POST /api/users`) thì người đó bấm **Tiếp tục với
Google** là vào, không cần thao tác DB. Tài khoản đã liên kết thì không bao giờ bị liên kết lại.

### Không tự tạo tài khoản

Google identity chưa ai liên kết thì trả `MSG03`, **không** tạo tài khoản mới. Cấp tài khoản là
việc của quản trị viên; cho tự đăng ký ở đây nghĩa là bất kỳ ai có Gmail đều vào được hệ thống.

Liên kết một tài khoản mà email **khác** Gmail của người dùng (ví dụ tài khoản seed
`thuky@trustcabinet.local`) thì vẫn phải làm bằng SQL:

```sql
update users set google_subject_id = '<sub từ ID token>'
where email = 'thuky@trustcabinet.local';
```

Lấy `sub` bằng cách decode ID token (jwt.io hoặc log của FE). Tài khoản seed **chưa** liên kết sẵn:
một `sub` bịa thì Google không bao giờ cấp, nên seed một giá trị giả chỉ tạo ra dòng dữ liệu gây
hiểu nhầm.

### Verify ID token

Dùng `GoogleJsonWebSignature.ValidateAsync` của `Google.Apis.Auth`, không tự viết. Verify đúng
nghĩa là phải tải certificate đang hiệu lực của Google, theo kịp việc họ rotate khoá, rồi kiểm tra
chữ ký, issuer, audience và thời hạn. Thư viện làm hết và cache certificate; bản tự viết là chỗ dự
án kết thúc với việc chấp nhận token không có chữ ký vì chỉ decode phần payload.

Bốn điểm bắt buộc:

- **`Audience` phải set.** Thiếu nó thì token chỉ cần là *một* token Google hợp lệ bất kỳ: token
  cấp cho ứng dụng khác cũng qua, và ai chạy ứng dụng đó đều đăng nhập được vào đây với tư cách
  user của họ.
- **`EmailVerified` phải true.** Email chưa verify chỉ chứng minh có người gõ vào, không phải hệ
  thống đã xác nhận.
- **`Subject` phải có.** `ValidationSettings` không có tuỳ chọn đòi `sub`, và `Payload.Subject` là
  `string` nullable, nên token thiếu claim đó vẫn đi qua verify. Phải chặn ngay tại đây: xuống dưới,
  `WHERE google_subject_id = NULL` bị EF dịch thành `IS NULL` — **khớp** mọi tài khoản chưa liên
  kết Google, tức là cấp token cho dòng đầu tiên PostgreSQL trả về. `UserRepository` chặn lần hai.
- **Mọi lỗi token phải về một kết quả.** Chỉ catch `InvalidJwtException` là chưa đủ:
  `FormatException` (segment không phải base64url, ví dụ `"a.b.c"`) lọt ra thành HTTP 500 kèm stack
  trace cho caller chưa xác thực, và làm token sai định dạng phân biệt được với token không hợp lệ.

`HttpRequestException` khi không tải được certificate của Google thì **cố tình không** catch: đó là
sự cố phía ta, không phải token xấu, trả "đăng nhập thất bại" sẽ đẩy người dùng đi kiểm tra lại mật
khẩu vốn không có vấn đề gì. Hiện thành 500; đổi sang 503 là việc nên làm, đã ghi ở phần còn nợ.

### Phần chưa test được bằng unit test

`GoogleJsonWebSignature.ValidationSettings` chỉ cho set `Audience`, `HostedDomain`, `Clock`,
`ForceGoogleCertRefresh` và hai clock tolerance — **không có cách thay signing key**. Nghĩa là
không thể tạo token trong unit test mà đi qua được bước kiểm chữ ký, nên **kiểm tra `Audience` và
`Issuer` không có test nào phủ** — muốn phủ phải có integration test với token Google thật.

Vì vậy hai luật ở tầng payload (`EmailVerified`, `Subject`) được tách ra
`GoogleTokenVerifier.IdentityFrom` — `internal`, có `InternalsVisibleTo` cho project test — để
chúng được test trực tiếp thay vì nằm trong phần không test được.

### Bật/tắt

Mặc định **tắt**, nên app vẫn khởi động được trên máy chưa cấu hình Google — `/api/auth/google` trả
`404` và đăng nhập bằng mật khẩu không bị ảnh hưởng.

`404` chứ không phải `403` hay `501`: tính năng không được cung cấp thì nên trông như một URL không
tồn tại, cùng lý lẽ với `RequireCapability` render 404 thay vì 403.

```json
"Authentication": {
  "Google": {
    "Enabled": true,
    "ClientId": "<client id>.apps.googleusercontent.com"
  }
}
```

`ClientId` **không phải secret** — trình duyệt gửi nó cho Google công khai, nên để trong
`appsettings.json` được. Client *secret* thì luồng này không dùng đến: trình duyệt lấy ID token,
backend chỉ verify.

Bật `Enabled` mà thiếu `ClientId` thì app từ chối khởi động kèm hướng dẫn, chứ không chạy với
audience rỗng.

## UC-03 · Đăng xuất

Sequence diagram: [SignOut.png](../06-sequencediagram/SignOut.png).

UC-03 chỉ có `User` và `Authenticated Page` — **không có participant backend nào**. Việc của nó là
xoá JWT ở trình duyệt, hiện hộp xác nhận trước khi xoá, và MSG01 khi không xoá được. Toàn bộ là
việc của FE, làm cùng SCRUM-46.

Backend không có việc mới. `POST /api/auth/logout` đã có sẵn và bổ sung cho UC-03 phần diagram
không vẽ nhưng tiêu chí nghiệm thu SCRUM-11 yêu cầu: **huỷ phiên ở phía server**, để một client
giữ lại RT vẫn không dùng lại được. Từ khi RT nằm trong cookie `HttpOnly`, đây cũng là **cách duy
nhất** xoá được cookie — JavaScript không đụng tới nó. Vì vậy FE coi `/logout` không tới được server
là "không xoá được" — `MSG01` của UC-03 — và giữ nguyên phiên để người dùng thử lại; nếu cứ xoá phía
trình duyệt thì F5 sẽ đăng nhập lại ngay bằng cookie còn sót.

Logout chỉ huỷ RT của phiên đó — đăng xuất trên laptop không đăng xuất trên điện thoại. Huỷ mọi
thiết bị thì đã có `RevokeAllForUserAsync`, hiện chỉ dùng khi tài khoản bị vô hiệu hoá; chưa expose
thành endpoint vì chưa story nào yêu cầu.

### `POST /api/auth/refresh`

Không cần body: RT lấy từ cookie `tc_refresh_token`.

Trả về đúng shape của `login` và đặt cookie mới. RT **dùng một lần** (rotation): mỗi lần refresh sẽ
huỷ RT vừa dùng và cấp RT mới, nên RT bị đánh cắp sẽ hết tác dụng ngay khi client thật refresh lần
kế tiếp.

**Gửi lại một RT đã dùng rồi thì mọi phiên của tài khoản đó bị huỷ**, không chỉ trả `401`. Một RT
xuất hiện hai lần nghĩa là có hai bên cùng giữ nó — kẻ trộm và người dùng thật, không biết bên nào
trước — nên không bên nào được giữ phiên. Xem [phát hiện RT bị dùng lại](#phát-hiện-rt-bị-dùng-lại).

Mỗi lần refresh đều đọc lại `users` và `role_capabilities`, nên vô hiệu hoá tài khoản hay đổi quyền
có hiệu lực ở lần refresh tiếp theo — không phải chờ hết 7 ngày. Khi gặp tài khoản đã bị vô hiệu
hoá, **toàn bộ** RT của người đó bị huỷ, không chỉ cái vừa gửi lên.

### `POST /api/auth/logout`

Không cần body: RT lấy từ cookie, như `/refresh`.

`204 No Content`, **luôn luôn** — kể cả khi RT đã hết hạn hoặc chưa từng tồn tại. Gọi hai lần không
phải lỗi; trả `400` ở lần thứ hai chỉ để lộ trạng thái token cho người gọi.

Cũng **luôn luôn** xoá cookie (`Set-Cookie: tc_refresh_token=; max-age=0; …`), kể cả khi RT không
còn hợp lệ: sau khi đăng xuất thì cookie đó không còn dùng vào việc gì.

Logout chỉ huỷ RT của *phiên đó*. Đăng xuất trên laptop không đăng xuất trên điện thoại.

### `GET /api/auth/me`

Cần header `Authorization: Bearer <accessToken>`.

Đọc **từ database**, không đọc từ claim trong AT. Trước đây đọc từ claim cho rẻ, và đó là sai:
AT là ảnh chụp lúc đăng nhập, nên tài khoản vừa bị vô hiệu hoá — hoặc vai trò vừa bị gỡ quyền qua
màn hình SCRUM-60 — vẫn được báo là còn nguyên quyền cho tới khi AT hết hạn. Đây lại đúng là
endpoint mà giao diện dựa vào để render, nên câu trả lời cũ sẽ vẽ ra một màn hình quản trị đầy đủ
cho người mà mọi endpoint khác đều từ chối (SCRUM-12).

Trả `401` khi AT không có `sub` đọc được, khi tài khoản không còn tồn tại, hoặc khi tài khoản đã bị
vô hiệu hoá — không phải `403`: phiên đã chấm dứt, việc cần làm là đăng nhập lại.

## Access token và refresh token

Hai token, hai mục đích khác nhau:

| | Access token (AT) | Refresh token (RT) |
|---|---|---|
| Dạng | JWT có chữ ký HS256 | chuỗi ngẫu nhiên 256 bit, không mang thông tin |
| Sống bao lâu | 15 phút (cấu hình được) | 7 ngày (cấu hình được) |
| Lưu ở đâu | không lưu — tự chứng minh bằng chữ ký | Redis |
| Huỷ được không | **không**, cho tới khi hết hạn | có, ngay lập tức |

Đây là lý do AT ngắn: JWT không thu hồi được trước khi hết hạn, nên tài khoản vừa bị vô hiệu hoá
vẫn gọi API được **tối đa 15 phút**. Nới `AccessTokenMinutes` là nới đúng khoảng đó.

Ngược lại, RT chỉ có giá trị khi Redis còn giữ. Đó là thứ làm cho "đăng xuất huỷ phiên ở phía
server, không chỉ xoá ở trình duyệt" (tiêu chí nghiệm thu SCRUM-11) là sự thật chứ không phải lời
tuyên bố.

AT mang sẵn `capabilities` trong claim, nhưng claim **không phải** nguồn có thẩm quyền: nó chỉ là
lớp lọc đầu tiên, rẻ. Mọi endpoint nghiệp vụ đọc lại tài khoản từ database trước khi chạy, nên đổi
quyền có hiệu lực ngay ở request kế tiếp chứ không phải chờ lần refresh — xem
[`04-phan-quyen-phia-server.md`](04-phan-quyen-phia-server.md).

### Vì sao RT nằm ở Redis mà không phải một bảng

Mọi thứ RT cần đều là việc Redis làm sẵn: token tự hết hạn bằng TTL (không cần job dọn rác), mỗi
thao tác là một lần tra key, và `GETDEL` đọc–xoá trong **một** lệnh nguyên tử. Chỗ cuối cùng mới là
quan trọng: nếu tách thành `GET` rồi `DEL`, hai request refresh đồng thời cùng một RT bị đánh cắp sẽ
**cùng** đọc thấy hợp lệ trước khi cái nào kịp xoá.

Ba loại key, cả ba đều tự hết hạn:

```
rt:{sha256(token)}       → user id         "token này còn hợp lệ không, của ai?"
rt-used:{sha256(token)}  → user id         "token này đã bị dùng rồi chưa?" → phát hiện dùng lại
rt-user:{userId}         → set các hash    "người này đang có những phiên nào?" → để huỷ tất cả
```

### Phát hiện RT bị dùng lại

Rotation một mình chưa đủ: trước đây một lần replay chỉ bị *bỏ qua*. Kẻ trộm refresh trước thì giữ
được phiên, còn người dùng thật bị đẩy ra.

Giờ mỗi lần refresh thành công, hash của RT vừa dùng được giữ trong `rt-used:{hash}` với TTL **bằng
tuổi RT** (`RefreshTokenDays`), không phải thời gian còn lại của token — marker phải sống ít nhất bằng RT
được cấp ra ở chính lần refresh đó. Gặp lại một RT có trong `rt-used` thì gọi `RevokeAllForUserAsync`: mọi phiên
của tài khoản đó chết, kể cả phiên kẻ trộm vừa lấy được, và ghi log `Warning`.

Đọc–xoá `rt:` và ghi `rt-used:` chạy trong **một Lua script** (`RedisRefreshTokenStore.ConsumeScript`)
chứ không phải `GETDEL` rồi `SET`: giữa hai lệnh, một lần replay sẽ không thấy key nào và bị coi là
token lạ thay vì bị bắt.

Hệ quả cho FE: hai request refresh song song với cùng một RT giờ **đăng xuất người dùng trên mọi
thiết bị**, không chỉ làm một request thất bại. FE đã có single-flight + Web Lock nên không gặp;
đừng bỏ chúng.

Logout (`RevokeAsync`) không ghi `rt-used`: RT bị huỷ bằng đăng xuất mà bị gửi lại chỉ nhận `401`.

Redis lưu **hash** của token, không lưu token. Dump Redis ra không được phép là một nắm session
dùng được ngay. Ở đây dùng SHA-256 trần, không salt, không stretching — khác với mật khẩu — vì đầu
vào đã là 256 bit ngẫu nhiên từ CSPRNG, không có gì để brute-force hay tra rainbow table.

Mất dữ liệu Redis chỉ có nghĩa là mọi người phải đăng nhập lại, nên container `redis` trong
`docker-compose.yml` cố ý **không** có volume.

## Mật khẩu

Băm bằng `PasswordHasher<T>` của ASP.NET Core: PBKDF2-HMAC-SHA512, 210 000 vòng, salt 128 bit riêng
cho từng mật khẩu. Thuật toán và tham số nằm **trong** chuỗi hash, nên hash cũ vẫn verify được sau
khi default thay đổi — vì vậy bảng `users` không có cột salt riêng.

Không tự lắp bằng `SHA256` hay `HMACSHA256`: đó là cách một dự án kết thúc với SHA-256 không salt.

Hash cùng một mật khẩu hai lần ra hai kết quả khác nhau (salt ngẫu nhiên). Nếu không có salt, hai
người đặt trùng mật khẩu sẽ có hash giống nhau và một lần lộ bảng `users` là lộ cả hai.

## Cấu hình

Cần **hai** thứ không nằm trong repo. Thiếu là app không khởi động được, cố ý:

```bash
# Khoá ký AT. Tối thiểu 32 ký tự — HMAC-SHA256 với khoá ngắn hơn output là khoá yếu, và
# thư viện JWT từ chối thẳng.
dotnet user-secrets set "Authentication:SigningKey" "$(openssl rand -base64 48)" \
  --project backend/TrustCabinet.Web

dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" \
  --project backend/TrustCabinet.Web
```

`SigningKey` là credential đúng nghĩa: ai có nó đều ký được token cho **bất kỳ** tài khoản nào, với
role nào. Đối xử với nó như mật khẩu database — user-secrets ở máy cá nhân, environment variable khi
triển khai, không bao giờ vào `appsettings.json` vì file đó được commit.

Phần không phải secret nằm trong `appsettings.json`:

```json
"Authentication": {
  "Issuer": "TrustCabinet",
  "Audience": "TrustCabinet",
  "AccessTokenMinutes": 15,
  "RefreshTokenDays": 7
}
```

Redis chạy bằng `docker compose up -d` (đã thêm service `redis` cạnh `postgres`).

## Tài khoản demo

`DevelopmentDataSeeder` giờ băm mật khẩu thật, nên bốn tài khoản seed đăng nhập được. Mật khẩu chung:

```
TrustCabinet@2026
```

| Email | Role | Capabilities |
|---|---|---|
| `chutoa@trustcabinet.local` | Chair | MeetingCreator, MeetingApprover |
| `thuky@trustcabinet.local` | Secretary | MeetingCreator, MinutesEditor |
| `thanhvien@trustcabinet.local` | Member | *(không có — vẫn đăng nhập được)* |
| `quantri@trustcabinet.local` | Admin | AdminUsers |

Chỉ chạy ở Development (xem `MigrateAndSeedDevelopmentAsync`). Công khai mật khẩu này không sao, vì
nó chỉ tồn tại trong một database toàn cuộc họp bịa.

## Còn nợ

- ~~Toàn bộ phần FE của cả ba use case~~ — **đã làm**: màn đăng nhập gọi `/login` và `/google`
  (nút Google dùng Google Identity Services), hộp xác nhận đăng xuất gọi `/logout` trước khi xoá
  token, `AuthProvider` khôi phục phiên bằng `/refresh` khi tải lại trang. Token nằm ở
  `frontend/src/features/auth/session.ts`: access token chỉ trong bộ nhớ, refresh token trong
  cookie `HttpOnly` (FE không thấy); refresh chạy single-flight, có Web Lock để hai tab không
  tranh nhau một RT dùng-một-lần. Google `client_id` đang lặp lại ở FE vì backend chưa có
  endpoint trả nó.
- ~~**Liên kết tài khoản Google.**~~ — **đã làm**: tự liên kết ở lần đăng nhập đầu theo email đã
  xác minh ([ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md)). Còn thiếu:
  gỡ / đổi liên kết của một tài khoản đã liên kết — hiện vẫn bằng SQL, chưa có ticket.
- ~~**RT trả trong response body, FE lưu ở `localStorage`.**~~ — **đã làm**: RT chỉ còn trong cookie
  `HttpOnly`, xem [trên](#refresh-token-nằm-trong-cookie-tc_refresh_token).
- **Chưa giới hạn số lần thử sai.** Chưa có rate limit cho `/api/auth/login`, nên chưa chặn dò mật
  khẩu. Nên làm trước khi hệ thống ra khỏi môi trường nội bộ — và phần chống dò email ở trên mất
  phần lớn giá trị nếu thiếu nó.
- ~~**Chưa phát hiện refresh token bị dùng lại.**~~ — **đã làm**, xem
  [phát hiện RT bị dùng lại](#phát-hiện-rt-bị-dùng-lại).
- **Timing oracle với hash sai định dạng.** `PasswordHasher.Verify` bắt `FormatException` rồi trả
  `false` ngay, không tốn công băm: hash thật ~34 ms, hash sai định dạng ~0,002 ms. Tài khoản có
  `password_hash` hỏng vì thế phân biệt được cả với tài khoản thường lẫn email không tồn tại. Sửa
  bằng cách băm với `DummyHash` trước khi trả `false`.
- **`Verify` chưa trả 503 khi không gọi được Google** (xem trên).
- **`Audience`/`Issuer` của Google chưa có test** (xem trên) — cần integration test với token thật.
- **Chưa ghi audit log.** Bảng `audit_log` đã có; đăng nhập thành công/thất bại là thứ đáng ghi.
- **Chưa rehash khi tham số băm đổi.** `PasswordHasher` coi `SuccessRehashNeeded` là thành công
  nhưng không băm lại — xem comment `ponytail:` trong file đó.
