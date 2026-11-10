# Nhật ký hệ thống — SCRUM-16 (UC-13)

**Trạng thái:** BE và FE đã triển khai 10/11/2026 · Đặc tả: [UC-13](../01-requirements/05-use-case-specifications.md),
BR-440 → BR-446, [đặc tả sơ bộ](../notes/NOTE-dac-ta-so-bo-scrum-16.md),
[sequence](../06-sequencediagram/role-management/UC13_ViewAuditLog.txt).

Quản trị xem lại các việc quan trọng đã được ghi: ai làm, việc gì, trên đối tượng nào, lúc nào, từ IP nào. Nhật ký **chỉ
đọc**: không endpoint nào sửa hay xoá một dòng, và `TrustCabinetDbContext` từ chối mọi lần lưu sửa/xoá `audit_log`
(BR-440). Xem nhật ký không tự ghi thêm dòng nào.

## Quyền

Cả hai endpoint nằm sau `RequireAdminUsers()` — dùng lại quyền *Quản trị người dùng*, không có quyền riêng (Q2):

| Trường hợp | Kết quả |
|---|---|
| Chưa đăng nhập | `401` |
| Vai trò không có `AdminUsers` (kể cả token còn claim nhưng database đã gỡ) | `403`, không mã |
| Route giao diện `/mgmt/admin/audit-log` thiếu quyền | FE hiện `404` như mọi route bị hạn chế |

## `GET /api/audit-log`

| Tham số | Kiểu | Ý nghĩa |
|---|---|---|
| `from`, `to` | ISO 8601 **có múi giờ** (`2026-11-10T08:00:00+07:00`; trong URL viết `%2B07:00` — dấu `+` không mã hoá thành khoảng trắng và bị `MSG469`) | Khoảng `occurredAt`, tính cả hai đầu. `from` không được sau `to` |
| `actor` | Guid tài khoản, `guest` hoặc `system` | Một tài khoản; mọi dòng của Khách; mọi dòng hệ thống tự ghi |
| `action` | mã sự kiện | Phải là một `code` của `/event-types` |
| `q` | 1–100 ký tự | Tìm không phân biệt hoa thường trong mã đối tượng, **tên** đối tượng khi nó là tài khoản (họ tên) hoặc vai trò (tên vai trò) — đúng `objectName` — và các **giá trị** trong `details` (không khớp tên trường hay `null`); `%`, `_`, `\` được tìm đúng ký tự đó. Không khớp tên người thực hiện, tiêu đề cuộc họp hay tên tài liệu (BR-443). Có dấu phải gõ đúng dấu |
| `page`, `pageSize` | số nguyên | Mặc định `1`, `20`; `pageSize` tối đa `100` |

Mọi tiêu chí phải cùng khớp (BR-31). Thứ tự: `occurredAt` giảm dần, rồi `id` giảm dần. Trang vượt quá số trang trả
`items` rỗng.

**Lỗi**

| Mã | HTTP | Khi nào |
|---|---|---|
| `MSG469` | 400 | Thời điểm sai định dạng hoặc thiếu múi giờ, `from` sau `to`, `actor`/`action` không hợp lệ, `q` quá 100 ký tự, `page`/`pageSize` ngoài khoảng, tham số lạ, tham số gửi hai lần. `errors` theo tên tham số |
| `MSG470` | 500 | Không đọc được database |

Không có dòng nào khớp (AF-3) là `200` với `items: []`, `totalCount: 0`, `totalPages: 0` — màn hình tự hiện câu theo
BR-12.

**Response `200`**

```json
{
  "items": [
    {
      "id": "0192f3a1-…",
      "occurredAt": "2026-11-10T03:12:45.123+00:00",
      "actor": { "kind": "user", "id": "0192…", "name": "Nguyễn Văn A", "email": "a@example.com" },
      "action": "user.updated",
      "actionLabel": "Sửa tài khoản",
      "objectType": "User",
      "objectId": "0192…",
      "objectName": "Trần Thị B",
      "ipAddress": "203.0.113.7",
      "details": { "Email": "b@example.com", "IsActive": false }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1284,
  "totalPages": 65
}
```

- `actor.kind`: `user`, `guest` hoặc `system` (BR-441). Với `guest`, `id` là mã **lời mời** và `name`/`email` là tên,
  email được mời (không phải tên Khách tự khai). Với `system`, `id`, `name`, `email` đều `null`.
- `objectId` có thể `null`: sự kiện không gắn với một đối tượng (đổi quyền của nhiều vai trò một lúc).
- `objectName` chỉ có với `User` (họ tên) và `Role` (tên vai trò; `null` nếu vai trò đã xoá — tên cũ nằm trong
  `details`). Các loại khác luôn `null`: nhật ký không tra tiêu đề cuộc họp hay tên tài liệu (BR-443).
- `details` là JSON nguyên như lúc ghi; tên trường khác nhau giữa các loại sự kiện (có loại PascalCase, có loại
  camelCase). Hiện dạng key–value chỉ đọc, đừng dựa vào một tên trường cố định.
- `ipAddress` là địa chỉ của request đã gây ra việc đó, rỗng khi không có request (job tự huỷ cuộc họp). Nháp biểu
  quyết bị xoá khi kết thúc họp mang IP của Chủ toạ đã bấm kết thúc, dù người thực hiện là hệ thống.
- `actionLabel` là tên tiếng Việt; mã chưa có tên thì là chính mã đó.

## `GET /api/audit-log/event-types`

Danh sách cho ô lọc loại sự kiện, theo nhóm: `[{ "code": "auth.signed_in", "label": "Đăng nhập", "group": "Đăng nhập" }, …]`.
Tên nằm ở backend (`AuditEventCatalog`); `AuditEventCatalogTests` hỏng khi một mã trong `AuditActions` chưa có tên.

## Màn hình (FE)

`/mgmt/admin/audit-log`, mục *Nhật ký hệ thống* dưới Quản trị — `features/audit/AuditLogPage.tsx`. Theo màn 3.3 trừ
D3–D7 (không Xuất CSV; người thực hiện có thêm Khách, Hệ thống; không "5 lần liên tiếp"; cột Đối tượng là loại + tên
tài khoản/vai trò hoặc 8 ký tự đầu của mã).

- Mọi tiêu chí nằm trên URL (`from`, `to` dạng `YYYY-MM-DD`, `actor`, `action`, `q`, `page`, `pageSize`); giá trị sai
  dạng bị bỏ qua. Khoảng ngày là trọn ngày theo giờ máy người xem, gửi lên dạng UTC. Ngày bắt đầu sau ngày kết thúc
  thì báo dưới ô và **không** áp dụng.
- Ô "Tìm theo đối tượng…" (màn 3.3): `q` khớp tên ở cột Đối tượng (tài khoản, vai trò), mã đối tượng và giá trị
  trong `details`.
- Ô người thực hiện: Khách, Hệ thống, rồi mọi tài khoản từ `GET /api/users` (hai người trùng tên thì hiện email; mã
  tài khoản trên URL không có trong danh sách thì hiện "Tài khoản không rõ"). Ngày không có thật trên URL bị bỏ qua.
- `auth.sign_in_failed`: chip đỏ, dòng nền đỏ nhạt.
- Bấm một dòng mở ngăn bên phải, chỉ đọc: thời điểm tới giây, người thực hiện, mã sự kiện, mã đối tượng đầy đủ, IP, rồi `details` từng
  khoá như đã ghi.
- `MSG469` (link sửa tay) → "Bộ lọc trên đường dẫn không hợp lệ." và *Đặt lại bộ lọc*; `MSG470` → câu của backend và
  *Thử lại*; `403` giữa phiên → `useAccessLost`.

## Nhận diện Khách

Khách không có tài khoản, nên dòng của Khách có `actor_id = null` và mã lời mời nằm trong `details`, dưới khoá mà từng
chỗ ghi đã dùng: `guestInvitationId` (xem tài liệu Mật, Khách xác minh), `invitationId` (Khách vào phòng họp),
`GuestInvitationId` (Khách tự xoá câu nói), `guest.InvitationId` (Khách trả lời lời mời). `actor_id = null` mà không có
khoá nào là hệ thống. Chỗ ghi mới dùng `guestInvitationId`.

## Sự kiện mới được ghi trong SCRUM-16

| Mã | Luật | Người thực hiện / đối tượng | `details` |
|---|---|---|---|
| `auth.signed_in` | BR-444 | Tài khoản / `User` | `method`: `password` hoặc `google` |
| `auth.sign_in_failed` | BR-444 | Tài khoản bị thử / `User` | `method`, `reason`: `invalid_password` hoặc `account_deactivated` |
| `guest.verified` | BR-444 | Khách / `GuestSession` | `meetingId`, `guestInvitationId` |
| `meeting.<thao tác>` | BR-445 | Người chuyển trạng thái, hệ thống khi tự huỷ / `Meeting` | `historyId`, `fromStatus`, `toStatus`, `reason`, người duyệt / người tổ chức cũ và mới nếu có |
| `minutes.issued` | BR-446 | Chủ toạ / `MeetingMinutes` | `meetingId`, `revisionNumber`, `supersedesId` (bản đính chính) |

`meeting.<thao tác>` là một trong `meeting.created`, `submitted_for_approval`, `approved`, `rejected`,
`returned_to_draft`, `published`, `organizer_transferred`, `rescheduled`, `started`, `ended`, `cancelled`,
`auto_cancelled`, `status_changed`. Dòng này do `TrustCabinetDbContext` tự thêm cho **mỗi** dòng
`meeting_status_history` mới, trong cùng lần lưu, nên mọi chỗ chuyển trạng thái (kể cả job tự huỷ) đều được ghi và một
lần chuyển lưu hỏng thì không còn dòng nào. Dữ liệu `meeting_status_history` cũ không được chuyển sang.

Không bao giờ ghi mật khẩu, Google ID token, refresh token hay mã OTP. Email không khớp tài khoản nào thì không ghi
(có thể là mật khẩu gõ nhầm vào ô email) — đổi lại nhánh "sai mật khẩu" chậm hơn nhánh "email lạ" vài mili giây vì có
thêm một lần ghi; đã chấp nhận ngày 10/11/2026.

## Chỗ lệch khỏi đặc tả — đã đưa vào Report 3 ngày 10/11/2026

BR-444 và BR-445 trong Report 3 đã sửa theo hai dòng dưới (UC-14, UC-15 thêm vào *Applies to* của BR-445), kèm một dòng
Change Log. Chú thích `AuditLogEntry.ActorId` trong code đã sửa theo D8.

| # | Đặc tả | Code | Lý do |
|---|---|---|---|
| D8 | `AuditLogEntry.ActorId`: "null cho lần đăng nhập thất bại" | `auth.sign_in_failed` ghi người thực hiện là tài khoản bị thử | Không có người thực hiện thì dòng hiện là "Hệ thống" (sai BR-441); lọc theo một tài khoản thấy được cả các lần bị thử sai |
| D9 | BR-445: "mỗi lần đổi trạng thái" | Ghi cả dòng lịch sử không đổi trạng thái: tạo cuộc họp, đổi lịch, chuyển người tổ chức | Cùng nằm trong `meeting_status_history`; Quản trị không mở được cuộc họp để xem lịch sử đó |
