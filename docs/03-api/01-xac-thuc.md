# 03 · Xác thực — đăng nhập bằng email và mật khẩu

Hiện thực của **UC-01 (SCRUM-11)**, phần đăng nhập bằng mật khẩu. Phần *Continue with Google* chưa
làm, để lại cho lượt sau của cùng story.

Sequence diagram gốc chia bốn participant: `Login Page` → `:Auth Controller` →
`:Authentication Service` → `:User Repository` → `Database`. Code đi theo đúng bốn lớp đó:

| Participant trong diagram | Code |
|---|---|
| `:Auth Controller` | [`backend/TrustCabinet.Web/Endpoints/AuthEndpoints.cs`](../../backend/TrustCabinet.Web/Endpoints/AuthEndpoints.cs) |
| `:Authentication Service` | [`backend/TrustCabinet.Infrastructure/Authentication/AuthenticationService.cs`](../../backend/TrustCabinet.Infrastructure/Authentication/AuthenticationService.cs) |
| `:User Repository` | [`backend/TrustCabinet.Infrastructure/Authentication/UserRepository.cs`](../../backend/TrustCabinet.Infrastructure/Authentication/UserRepository.cs) |
| `Database` | bảng `users` và `role_capabilities` |

## Endpoint

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/auth/login` | không cần | Đăng nhập, cấp AT + RT |
| POST | `/api/auth/refresh` | không cần (RT là thứ chứng minh) | Đổi RT cũ lấy cặp mới |
| POST | `/api/auth/logout` | không cần | Huỷ RT ở phía server |
| GET | `/api/auth/me` | Bearer AT | Thông tin người đang đăng nhập |

### `POST /api/auth/login`

```json
{ "email": "thuky@trustcabinet.local", "password": "…" }
```

`200 OK`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs…",
  "refreshToken": "8Kx2v…",
  "accessTokenExpiresAt": "2026-09-21T10:15:00+00:00",
  "user": {
    "id": "01999f2e-…",
    "email": "thuky@trustcabinet.local",
    "fullName": "Nguyễn Thư Ký",
    "role": "Secretary",
    "capabilities": ["MeetingCreator", "MinutesEditor"]
  }
}
```

Ba nhánh lỗi đều trả `401` kèm mã thông báo của UC-01. FE khớp theo `code`, không khớp theo
`message` — đổi chữ trong `message` không được làm FE vỡ.

| `code` | Khi nào | `message` |
|---|---|---|
| `MSG01` | Email không tồn tại, **hoặc** sai mật khẩu, **hoặc** bỏ trống | `Email hoặc mật khẩu không đúng.` |
| `MSG03` | Tài khoản đã bị vô hiệu hoá (`users.is_active = false`) | `Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.` |
| `MSG04` | Role không còn capability nào trong `role_capabilities` | `Tài khoản chưa được cấp quyền truy cập hệ thống.` |

Vài điểm cố ý, đừng "sửa" lại:

- **`MSG01` gộp ba tình huống.** Tiêu chí nghiệm thu của SCRUM-11: lỗi chung chung, không tiết lộ
  email có tồn tại hay không. Tách ra thành "email không tồn tại" là làm hỏng yêu cầu này.
- **Kiểm mật khẩu trước, kiểm `is_active` sau.** Nếu trả `MSG03` trước khi so mật khẩu thì người
  không biết mật khẩu vẫn biết được địa chỉ đó là tài khoản thật.
- **Email không tồn tại vẫn phải băm một lần.** Băm mật khẩu chậm có chủ đích, nên bỏ qua bước này
  khi không tìm thấy user sẽ làm nhánh "không có tài khoản" trả lời nhanh hơn nhánh "sai mật
  khẩu" — chênh lệch thời gian đó đủ để dò ra email nào đã đăng ký. Xem `DummyHash`.
- **`Member` không có capability nào nhưng vẫn đăng nhập được.** `MSG04` chỉ bắn khi role *khác*
  `Member` mà `role_capabilities` rỗng. Thành viên thường dự họp, đọc tài liệu và biểu quyết —
  không việc nào cần capability (xem `ReferenceData.RoleCapabilities`).

### `POST /api/auth/refresh`

```json
{ "refreshToken": "8Kx2v…" }
```

Trả về đúng shape của `login`. RT **dùng một lần** (rotation): mỗi lần refresh sẽ huỷ RT vừa dùng và
cấp RT mới, nên RT bị đánh cắp sẽ hết tác dụng ngay khi client thật refresh lần kế tiếp.

Mỗi lần refresh đều đọc lại `users` và `role_capabilities`, nên vô hiệu hoá tài khoản hay đổi quyền
có hiệu lực ở lần refresh tiếp theo — không phải chờ hết 7 ngày. Khi gặp tài khoản đã bị vô hiệu
hoá, **toàn bộ** RT của người đó bị huỷ, không chỉ cái vừa gửi lên.

### `POST /api/auth/logout`

```json
{ "refreshToken": "8Kx2v…" }
```

`204 No Content`, **luôn luôn** — kể cả khi RT đã hết hạn hoặc chưa từng tồn tại. Gọi hai lần không
phải lỗi; trả `400` ở lần thứ hai chỉ để lộ trạng thái token cho người gọi.

Logout chỉ huỷ RT của *phiên đó*. Đăng xuất trên laptop không đăng xuất trên điện thoại.

### `GET /api/auth/me`

Cần header `Authorization: Bearer <accessToken>`. Đọc trực tiếp từ claim trong AT, không truy vấn
database — để trình duyệt vừa reload có thể dựng lại session bằng một request rẻ.

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

AT mang sẵn `capabilities` trong claim để endpoint phân quyền không phải query database mỗi
request. Cái giá phải trả: đổi quyền chỉ có hiệu lực ở lần refresh kế tiếp.

### Vì sao RT nằm ở Redis mà không phải một bảng

Mọi thứ RT cần đều là việc Redis làm sẵn: token tự hết hạn bằng TTL (không cần job dọn rác), mỗi
thao tác là một lần tra key, và `GETDEL` đọc–xoá trong **một** lệnh nguyên tử. Chỗ cuối cùng mới là
quan trọng: nếu tách thành `GET` rồi `DEL`, hai request refresh đồng thời cùng một RT bị đánh cắp sẽ
**cùng** đọc thấy hợp lệ trước khi cái nào kịp xoá.

Hai loại key, cả hai đều tự hết hạn:

```
rt:{sha256(token)}   → user id         "token này còn hợp lệ không, của ai?"
rt-user:{userId}     → set các hash    "người này đang có những phiên nào?" → để huỷ tất cả
```

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

- **Continue with Google** — phần còn lại của SCRUM-11. Cần migration mới: `password_hash` thành
  nullable, thêm `google_subject_id` unique nullable, và ràng buộc phải có ít nhất một trong hai.
  Story mô tả rõ trong Jira; migration chưa viết vì lượt này chưa cần.
- **RT đang trả trong response body**, chưa phải cookie `HttpOnly`. Frontend không thuộc phạm vi
  lượt này; khi làm FE (SCRUM-46) thì chuyển sang cookie, vì token nằm trong tầm với của
  JavaScript là token mà bất kỳ lỗi XSS nào trên trang cũng đọc được.
- **Chưa giới hạn số lần thử sai.** Chưa có rate limit cho `/api/auth/login`, nên chưa chặn dò mật
  khẩu. Nên làm trước khi hệ thống ra khỏi môi trường nội bộ.
- **Chưa ghi audit log.** Bảng `audit_log` đã có; đăng nhập thành công/thất bại là thứ đáng ghi.
- **Chưa rehash khi tham số băm đổi.** `PasswordHasher` coi `SuccessRehashNeeded` là thành công
  nhưng không băm lại — xem comment `ponytail:` trong file đó.
