# UC-04 — Truy cập cuộc họp qua lời mời Khách (SCRUM-116)

Contract Backend cho Khách ngoài hệ thống: mở link mời → nhận OTP ở email được mời → xác minh → khai
hồ sơ tự khai → dùng token Khách gọi các API của đúng cuộc họp được mời. Không tạo dòng `users` hay
`meeting_participants`; Khách luôn gắn với một dòng `meeting_guest_invitations` (SCRUM-20).

Đặc tả nguồn: UC-04, [sequence](../06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt),
BR-148, BR-152–BR-157, BR-180–BR-182, [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md).
Adapter tích hợp: [Agenda](08-agenda-contract-tich-hop-guest-uc04.md),
[Thành phần tham dự](09-thanh-phan-tham-du-guest-uc04.md).

## 1. Các điểm đã chốt (SCRUM-117, 30/10/2026)

| Điểm | Giá trị | Cấu hình (`GuestAccess:*`) |
|---|---|---|
| Hiệu lực mã OTP | 15 phút kể từ lúc tạo | `CodeLifetimeMinutes` = 15 |
| Nhập sai tối đa | 5 lần cho một mã; lần sai thứ 5 vô hiệu mã | `MaxFailedAttempts` = 5 |
| Gửi lại mã | cách lần gửi trước tối thiểu 1 phút | `ResendIntervalSeconds` = 60 |
| Hiệu lực phiên Khách (BR-155) | 12 giờ kể từ lúc xác minh; token hết hạn cùng lúc, không có refresh | `SessionLifetimeHours` = 12 |
| Khách sau khi cuộc họp kết thúc | mất mọi quyền đọc (Agenda, thành phần, realtime…) khi cuộc họp `Ended` | — |

Phiên Khách còn hiệu lực khi **tất cả** đúng: phiên chưa hết hạn, chưa bị thu hồi (đăng xuất); lời mời
chưa bị thu hồi; cuộc họp đang `Published` hoặc `InProgress`. Agenda, Thành phần tham dự và realtime
còn đòi Khách đã khai tên hiển thị (bước 5 của UC-04). Mỗi request đọc lại trạng thái từ database,
không tin quyền nằm trong JWT.

"Lời mời hết hạn" (EX-1) = cuộc họp không còn `Published`/`InProgress`: lời mời không có cột hạn riêng.

## 2. Link mời

Thư mời Khách (SCRUM-20) chứa link:

```
{Email:ApplicationBaseUrl}/guest/{invitationId}#token={linkToken}
```

- `linkToken` là HMAC-SHA256 của `invitationId` bằng khoá suy ra từ `Authentication:SigningKey`, mã
  hoá base64url. Không lưu trong database, không đổi khi dispatch retry; thu hồi lời mời là đủ để link
  chết (mời lại sinh `invitationId` mới).
- Token nằm sau `#` nên không tới server, log hay `Referer`. FE đọc fragment rồi gửi trong body.
- Đoán `invitationId` không đủ: mọi endpoint ẩn danh dưới đây đều đòi `linkToken` đúng.

## 3. Endpoint ẩn danh (chưa có token Khách)

Cả ba nhận body JSON `{ "token": "<linkToken>" }` (verify thêm `code`). `invitationId` sai, token sai,
lời mời bị thu hồi, cuộc họp không `Published`/`InProgress` đều trả cùng một câu `404/MSG371` (EX-1),
không cho biết lý do cụ thể.

### 3.1 `POST /api/guest-access/invitations/{invitationId}` — mở lời mời

Chỉ đọc. FE gọi khi mở link để hiện tên cuộc họp và email bị che (màn 1.3).

```json
200 {
  "invitationId": "…",
  "meeting": { "id": "…", "title": "…", "status": "Published", "plannedStartTime": "…", "plannedEndTime": "…" },
  "maskedEmail": "ng***@example.com"
}
```

### 3.2 `POST /api/guest-access/invitations/{invitationId}/code` — gửi / gửi lại mã

Tạo mã 6 chữ số, gửi ngay tới **email được mời** (BR-153), không qua hàng đợi email và không lưu mã
thô (chỉ lưu HMAC). Mã mới vô hiệu mã cũ. Hai request đồng thời được xếp hàng bằng khoá dòng lời mời,
nên chỉ một mã được tạo.

| HTTP | Body |
|---|---|
| 200 | `{ "code": "MSG375", "message", "expiresInSeconds": 900, "resendAfterSeconds": 60 }` |
| 429 | `{ "code": "MSG373", "message", "retryAfterSeconds": n }` + header `Retry-After` — FE đếm ngược theo `retryAfterSeconds`, không tự tính |
| 404 | `MSG371` |
| 503 | `MSG374` — không lưu được mã hoặc không gửi được email (EX-3). Mã chưa gửi được bị xoá, nên bấm gửi lại được ngay |

### 3.3 `POST /api/guest-access/invitations/{invitationId}/verify` — xác minh mã

Body `{ "token": "…", "code": "123456" }`. Chỉ mã mới nhất của lời mời được xét.

| HTTP | Tình huống |
|---|---|
| 200 | `{ "accessToken", "expiresAt", "meetingId", "profileCompleted": false }` — mã bị tiêu thụ, phiên mới được tạo |
| 400 `MSG126` | Mã sai, hết hạn, đã dùng, chưa từng gửi, hoặc không phải 6 chữ số (EX-2). Mã sai làm tăng số lần sai |
| 400 `MSG372` | Mã đã bị khoá vì sai đủ 5 lần (kể cả lần sai thứ 5 và khi sau đó nhập đúng). Phải gửi mã mới |
| 404 `MSG371` | EX-1, kể cả khi lời mời bị thu hồi giữa lúc gửi mã và xác minh |
| 503 `MSG374` | Không lưu được kết quả xác minh (EX-3); không có phiên nào được tạo |

Tiêu thụ mã chạy trong transaction có khoá dòng lời mời: hai request cùng mã đúng thì chỉ một nhận
token, request kia nhận `MSG126`.

## 4. Endpoint của phiên Khách

Gửi `Authorization: Bearer <accessToken>`. Token nội bộ gọi các endpoint này nhận `403`; thiếu hoặc
hết hạn token nhận `401`.

### 4.1 `GET /api/guest-access/session` — phiên hiện tại (ALT-1)

FE còn token thì gọi endpoint này trước: `200` thì vào thẳng cuộc họp, không gửi mã mới.

```json
200 {
  "sessionId": "…", "expiresAt": "…",
  "meeting": { "id": "…", "title": "…", "status": "InProgress", "plannedStartTime": "…", "plannedEndTime": "…" },
  "email": "nguoi.duoc.moi@example.com", "invitedName": "Tên Thư ký đã nhập",
  "displayName": null, "jobTitle": null, "profileCompleted": false
}
```

`404/MSG378` khi phiên hết hạn, đã đăng xuất, lời mời bị thu hồi hoặc cuộc họp không còn
`Published`/`InProgress` — FE xoá token và hiện màn kết thúc / mở lại link.

### 4.2 `PUT /api/guest-access/session/profile` — hồ sơ tự khai (BR-180)

Body `{ "displayName": "…", "jobTitle": "…" }`. `displayName` bắt buộc, cắt khoảng trắng hai đầu, tối
đa 200 ký tự; `jobTitle` tuỳ chọn, tối đa 200 ký tự, rỗng thành `null`. Lưu vào
`meeting_guest_invitations.self_declared_display_name` / `self_declared_job_title`, **không** ghi đè
`invited_name` hay `email`. Khai lại thì ghi đè lần khai trước (dùng chung cho mọi phiên của lời mời).

| HTTP | Body |
|---|---|
| 200 | `{ "code": "MSG376", "message", "session": { …như 4.1 } }` |
| 400 | `{ "code": "MSG377", "message", "errors": { "displayName": [...], "jobTitle": [...] } }` |
| 404 | `MSG378` |
| 503 | `MSG374` |

### 4.3 `DELETE /api/guest-access/session` — đăng xuất Khách

Thu hồi đúng phiên của token (`revoked_at`). `204`, gọi lặp vẫn `204`. Các phiên khác của cùng lời mời
không bị ảnh hưởng. Token đã thu hồi vẫn hợp lệ về chữ ký tới khi hết hạn nhưng mọi API đều từ chối.

## 5. Token Khách

JWT ký cùng khoá, issuer, audience với token nội bộ:

| Claim | Giá trị |
|---|---|
| `sub` | `guest:{sessionId}` — không parse được thành Guid nên mọi endpoint nội bộ (`RequireActiveUser`, `RequireCapability`, `/api/auth/me`, hub thông báo) đều từ chối |
| `guest_meeting`, `guest_invitation`, `guest_session` | ba Guid của `MeetingGuestSession` |
| `exp` | đúng `expires_at` của phiên (≤ 12 giờ) |

Không có claim `cap`, `email`, `name`. `IMeetingGuestIdentityResolver` (Web) chỉ đọc các claim này
từ principal đã xác thực; principal có `sub` là Guid mà lại mang claim Khách bị coi là Khách lỗi (ba
Guid rỗng) và bị từ chối. Hub `/hubs/meetings` nhận token qua `access_token` như người nội bộ và đóng
kết nối khi token hết hạn.

Khách dùng token này cho: `GET /api/meetings/{id}/agenda`, các GET của thành phần tham dự
(`/participant-context`, `/participants`, `/invitation-response`), `PUT /invitation-response` (UC-29),
`GET /attendance` và hub `/hubs/meetings`. Mọi endpoint khác trả `403` (endpoint cố định) hoặc `404`
(tài nguyên cụ thể) như người không có quyền.

## 6. Lưu trữ

| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `guest_verification_challenges` | `invitation_id`, `code_hash`, `expires_at`, `failed_attempts`, `consumed_at` | HMAC-SHA256 của mã, không lưu mã thô. Xoá theo lời mời |
| `guest_sessions` | `invitation_id`, `expires_at`, `revoked_at` | Một dòng mỗi lần xác minh thành công; `id` là `SessionId` ổn định khi reconnect |
| `meeting_guest_invitations` | thêm `self_declared_display_name`, `self_declared_job_title` | Nullable; tách khỏi `invited_name`/`email` |

Không ghi mã OTP, link token hay access token vào log, audit hay `email_messages`. Link mời nằm trong
`email_messages` của thư mời như mọi link khác (quyền đọc bảng giới hạn như outbox hiện có).

## 7. Mã thông báo

| Mã | HTTP | Tình huống | Thông báo |
|---|---|---|---|
| `MSG126` | 400 | EX-2: mã sai, hết hạn, đã dùng | Không xác minh được lời mời. Vui lòng kiểm tra mã và thử lại. |
| `MSG371` | 404 | EX-1: lời mời không tồn tại, sai token, bị thu hồi hoặc cuộc họp không còn mở | Lời mời không còn hiệu lực. Vui lòng liên hệ Thư ký cuộc họp. |
| `MSG372` | 400 | Mã bị khoá sau 5 lần sai | Bạn đã nhập sai quá số lần cho phép. Vui lòng gửi lại mã mới. |
| `MSG373` | 429 | Gửi lại mã trong vòng 1 phút | Vui lòng chờ trước khi gửi lại mã. |
| `MSG374` | 503 | EX-3: không lưu/đọc được dữ liệu hoặc không gửi được email | Không thể xác minh lúc này. Vui lòng thử lại sau. |
| `MSG375` | 200 | Đã gửi mã | Đã gửi mã xác minh tới email được mời. |
| `MSG376` | 200 | Lưu hồ sơ tự khai | Đã lưu thông tin hiển thị. |
| `MSG377` | 400 | Hồ sơ tự khai không hợp lệ | Thông tin hiển thị không hợp lệ. |
| `MSG378` | 404 | Phiên Khách không còn hiệu lực | Phiên truy cập đã kết thúc. Vui lòng mở lại liên kết trong email. |

## 8. Ngoài phạm vi

- Thư ký thu hồi từng phiên riêng lẻ của Khách: chưa có màn hình; thu hồi lời mời đã cắt mọi phiên.
- Đọc tài liệu, góp ý, biểu quyết, phòng họp trực tuyến cho Khách: ticket của từng feature dùng lại
  phiên này.
- Giới hạn số lần gửi mã theo giờ/ngày ngoài khoảng chờ 1 phút.
