# Chia sẻ lịch — contract Backend SCRUM-142

**Trạng thái:** BE đã triển khai (nhánh `feat/scrum-142-calendar-sharing-be`), 10/11/2026.
**Use case:** UC-84a → UC-84f (chia sẻ lịch), UC-85c (xem lịch được chia sẻ). **BR:** BR-414 → BR-431.
**Nguồn:** [đặc tả sơ bộ](../notes/NOTE-dac-ta-so-bo-scrum-137-142.md), sequence trong
[calendar-sharing/](../06-sequencediagram/calendar-sharing/), quyết định PO 09/11/2026 (Q-A → Q-E, [note 39](../notes/ban-giao-ba-final-review/39-scrum-142-thieu-sequence-chia-se-lich.md)),
[ADR-0058](../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md). Mã thông báo: [danh mục](00-danh-muc-ma-thong-bao.md) mục *Chia sẻ lịch*.
Ghi chú cho FE: [22-chia-se-lich-note-cho-fe.md](22-chia-se-lich-note-cho-fe.md).

## 1. Quy tắc chung

- Mọi route cần đăng nhập (`401`) và tài khoản còn hoạt động (`403` không body). ID người gọi luôn lấy từ token.
- Quyền tạo cuộc họp (`MeetingCreator`, actor *Meeting Organizer*) kiểm **ở server** trong từng thao tác cần nó và trả
  `403 MSG448` có body: xin xem lịch, danh sách chủ lịch để xin, xem tuần. Chủ lịch chỉ cần là tài khoản nội bộ đang hoạt động (BR-430).
- Chia sẻ mà người gọi không phải một trong hai bên → `404` **không body** (không lộ chia sẻ hay tài khoản, POST-3).
- Mọi ngày là `YYYY-MM-DD` theo lịch Việt Nam (`Asia/Ho_Chi_Minh`). "Hôm nay" luôn là ngày ở Việt Nam, không phải giờ máy chủ.
- **Hết ngày kết thúc thì chia sẻ tự hết** (BR-425, PO 09/11 Q-B): không có job, không có trạng thái `Expired`, không thông báo.
  Chia sẻ có `endDate` trước hôm nay bị loại khỏi mọi danh sách và mọi thao tác trên nó trả `409 MSG450`. Lời mời hoặc thay đổi đang chờ
  của nó cũng hết theo.
- `Cache-Control: no-store` cho mọi `GET`.
- Mỗi bước ghi audit (`calendar_share.*`) và thông báo trong ứng dụng cho bên kia (BR-426), cùng một transaction: lưu lỗi thì không ai được báo.

## 2. Trạng thái và hành động

| `status` | Nghĩa |
|---|---|
| `Pending` | Chờ bên được hỏi trả lời (UC-84c) |
| `Effective` | Đang hiệu lực; người xem thấy giờ bận ở mốc đã chia sẻ |
| `Declined`, `Withdrawn`, `Ended` | Đã đóng (từ chối, rút, ngừng) |
| `Stopped` | Dừng hẳn vì người xem mất quyền tạo cuộc họp hoặc một bên bị vô hiệu hoá (BR-424); FE hiện `MSG439` |

Một cặp **có chiều** (chủ lịch → người xem) chỉ có tối đa một chia sẻ `Pending` hoặc `Effective` (BR-423). `Stopped` và các trạng thái
đóng không bao giờ quay lại; muốn xem lại phải xin chia sẻ mới.

`actions` trong mỗi chia sẻ là những gì **người gọi** làm được ngay lúc đó — FE hiện nút theo mảng này:

| Trạng thái | Người gọi | `actions` |
|---|---|---|
| `Pending` | bên được hỏi (chủ lịch nếu `initiatedBy = Viewer`, người xem nếu `Owner`) | `accept`, `decline` — chỉ `decline` khi `senderDeactivated = true` (UC-84c EX-5) |
| `Pending` | bên gửi | `withdraw` |
| `Effective`, không có thay đổi chờ | người xem / chủ lịch | `requestChange`, `end` / `update`, `end` |
| `Effective`, có thay đổi chờ | người xem / chủ lịch | `withdraw`, `end` / `accept`, `decline`, `update`, `end` |
| đã đóng, đã dừng | cả hai | `[]` |

## 3. Đối tượng `Share`

```json
{
  "id": "0196…", "initiatedBy": "Viewer", "level": "Level2",
  "owner":  { "id": "…", "fullName": "Trần Chủ Lịch", "email": "chulich@…" },
  "viewer": { "id": "…", "fullName": "Lê Người Xem", "email": "nguoixem@…" },
  "startDate": "2026-11-10", "endDate": "2026-12-09", "status": "Effective",
  "pendingChange": { "level": "Level2", "endDate": "2027-01-31", "requestedAt": "2026-11-10T03:00:00+00:00" },
  "createdAt": "…", "decidedAt": "…", "closedAt": null,
  "senderDeactivated": false,
  "actions": ["accept", "decline", "update", "end"]
}
```

`level`: `Level1` = họp Thường; `Level2` = Thường + Nội bộ; họp Mật **không bao giờ** được chia sẻ (BR-414). `pendingChange` là `null`
khi không có thay đổi chờ. Không có trường nào về cuộc họp (BR-427).

## 4. Endpoint

### `GET /api/calendar/shares` — UC-84f

`200 { "asOwner": Share[], "asViewer": Share[] }`, mới nhất trước, đã loại chia sẻ quá ngày kết thúc. `asViewer` chỉ có dữ liệu khi
người gọi có `MeetingCreator`; người không có quyền nhận `asViewer = []` nhưng vẫn thấy `asOwner` để trả lời, đổi hoặc ngừng chia sẻ
lịch của mình (xem §6, L8). Đọc lỗi → `500 MSG453`.

### `GET /api/calendar/share-candidates?purpose=request|direct&q=&page=1&pageSize=20` — UC-84a/UC-84b bước 1

- `purpose=request` (UC-84a): cần `MeetingCreator` (`403 MSG448`); trả mọi tài khoản đang hoạt động trừ người gọi.
- `purpose=direct` (UC-84b): mọi tài khoản đang hoạt động; trả người có `MeetingCreator`, trừ người gọi.
- `q` (≤ 200 ký tự) tìm theo tên hoặc email, không phân biệt hoa thường; `%`, `_` là ký tự thường. Sắp theo tên rồi ID.
  `page` từ 1, `pageSize` 1–100 (mặc định 20). Thiếu/sai `purpose`, tham số lạ hoặc lặp, paging sai → `400` không body.

`200 { "items": [{ "id", "fullName", "email", "hasOpenShare" }], "total", "page", "pageSize" }`. `hasOpenShare` = đã có chia sẻ
`Pending`/`Effective` cùng chiều với người này (FE làm mờ); server vẫn kiểm lại khi gửi. Đọc lỗi → `500 MSG467`.

### `POST /api/calendar/shares` — UC-84a

Body `{ "ownerId": "…", "level": "Level1|Level2", "startDate": "YYYY-MM-DD", "endDate": "YYYY-MM-DD" }`.
Kiểm theo thứ tự bước 2 của UC: quyền (`403 MSG448`) → chủ lịch là chính mình / không tồn tại / bị vô hiệu hoá (`404` không body) →
đã có chia sẻ mở cùng chiều (`409 MSG449`) → mốc / khoảng ngày (`400 MSG443`). Khoảng hợp lệ (BR-431): `hôm nay ≤ startDate ≤ endDate`,
tối đa 365 ngày tính cả hai đầu. Thành công `201 { code: "MSG435", message, share }`, báo chủ lịch.
Chia sẻ cũ cùng chiều đã quá ngày kết thúc được đóng lặng lẽ (`Ended`, không thông báo, audit `calendar_share.closed_after_end_date`).

### `POST /api/calendar/shares/direct` — UC-84b

Body `{ "viewerId", "startDate", "endDate" }`, luôn `Level1`. Bên nhận là mình / không tồn tại / bị vô hiệu hoá / không có
`MeetingCreator` → `400 MSG455`; trùng → `409 MSG449`; khoảng sai → `400 MSG443`. Thành công `201 MSG441`, báo bên nhận.

### `POST /api/calendar/shares/{id}/accept` — UC-84c

Body tuỳ chọn `{ "level", "startDate", "endDate" }`.

- Chia sẻ `Pending`, người gọi là bên được hỏi:
  - người gửi đã bị vô hiệu hoá → `409 MSG468` (EX-5; vẫn Từ chối được);
  - yêu cầu UC-84a: chủ lịch có thể hạ mốc / thu hẹp khoảng **trong** khoảng được xin, ngày kết thúc từ hôm nay → sai `400 MSG443`;
  - chia sẻ UC-84b: không được đổi gì (trường gửi lên phải trống hoặc bằng giá trị hiện tại) → sai `400 MSG443`;
  - thành công `200 MSG436`, chia sẻ `Effective`, báo bên gửi.
- Chia sẻ `Effective` có thay đổi chờ, người gọi là chủ lịch: áp dụng đúng mốc và ngày kết thúc bên xem đã xin, kiểm lại BR-431 lúc duyệt →
  `200 MSG442`, báo người xem.
- Người gửi gọi `accept` trên yêu cầu của chính mình → `404` (EX-1). Không có gì đang chờ, đã đóng, đã quá ngày → `409 MSG450`.

### `POST /api/calendar/shares/{id}/decline` — UC-84c

Không body. Từ chối chia sẻ `Pending` → `200 MSG437` (`Declined`); từ chối thay đổi chờ → `200 MSG452` (chia sẻ giữ như cũ). Báo bên gửi /
người xem. Lỗi như `accept`.

### `PATCH /api/calendar/shares/{id}` — UC-84d

Body `{ "level"?, "startDate"?, "endDate"? }`; chỉ trên chia sẻ `Effective` (khác → `409 MSG450`).

- **Người xem** xin thay đổi (Normal Flow): chỉ nâng mốc và/hoặc kéo dài `endDate`; `startDate` (nếu gửi) phải bằng ngày bắt đầu hiện tại;
  phải có ít nhất một thứ tăng; tổng tối đa 365 ngày tính từ ngày bắt đầu. Đã có thay đổi chờ → `409 MSG450`. Thành công `201 MSG454`, báo chủ lịch.
- **Chủ lịch** đổi ngay (AF-1): hạ mốc và/hoặc đặt khoảng mới (`startDate` giữ nguyên hoặc từ hôm nay, `endDate` từ hôm nay, ≤ 365 ngày),
  phải đổi ít nhất một thứ; được làm cả khi người xem đang có thay đổi chờ (thay đổi đó vẫn chờ). Thành công `200 MSG442`, báo người xem.
- Giá trị sai → `400 MSG443`.

### `POST /api/calendar/shares/{id}/withdraw` — UC-84e

Bên gửi rút chia sẻ `Pending` (`Withdrawn`), hoặc người xem rút thay đổi đang chờ (chia sẻ giữ nguyên) → `200 MSG438`, báo bên kia.
Khác → `409 MSG450`.

### `POST /api/calendar/shares/{id}/end` — UC-84e AF-1

Một trong hai bên ngừng chia sẻ `Effective` → `200 MSG451` (`Ended`), báo bên kia. Khác → `409 MSG450`.

### `GET /api/calendar/shared-week?week=YYYY-MM-DD` — UC-85c

Cần `MeetingCreator` (`403 MSG448`). `week` là một ngày bất kỳ của tuần (vắng = tuần hiện tại theo giờ Việt Nam; Chủ nhật thuộc tuần bắt đầu
thứ Hai trước đó); sai định dạng → `400` không body. Đọc lỗi → `500 MSG453`.

```json
{
  "weekStart": "2026-11-09", "weekEnd": "2026-11-13", "dayStart": "08:00", "dayEnd": "17:00",
  "people": [
    { "shareId": "…", "owner": { "id": "…", "fullName": "…", "email": "…" }, "status": "Effective", "level": "Level1",
      "startDate": "2026-11-09", "endDate": "2026-11-12",
      "days": [ { "date": "2026-11-09", "known": true,
                  "busy": [ { "start": "2026-11-09T09:00:00+07:00", "end": "2026-11-09T11:00:00+07:00" } ] },
                { "date": "2026-11-13", "known": false, "busy": [] } ] },
    { "shareId": "…", "owner": { … }, "status": "Stopped", "level": "Level1", "startDate": "…", "endDate": "…", "days": [] }
  ]
}
```

- Mỗi chủ lịch một dòng: chia sẻ mới nhất có `status` `Effective` hoặc `Stopped` và chưa quá ngày kết thúc. `Stopped` → `days = []`.
- Thứ Hai → thứ Sáu; ngày ngoài khoảng chia sẻ → `known = false` ("chưa biết", không phải rảnh, BR-415).
- Giờ bận (BR-419, BR-420): chỉ phân công hiện hành Chủ toạ / Thư ký / Thành viên chưa từ chối (Chủ toạ luôn tính); họp Chờ duyệt, Đã duyệt,
  Đã công bố, Đang họp, Đã kết thúc (đến giờ kết thúc thật). Mốc lọc theo §3. Các khoảng chồng/chạm nhau đã gộp và cắt vào 08:00–17:00.
- Không bao giờ có tên, địa điểm, người dự hay mức mật của cuộc họp (BR-427).

## 5. Thông báo

`targetType = "CalendarShare"`, `targetId` = id chia sẻ, `meetingId = null`. `GET /api/notifications/{id}/target` mở được khi người gọi là
một trong hai bên, `url = /calendar/sharing?share={id}`. Loại: `CalendarShareRequested`, `…Received`, `…Accepted`, `…Declined`,
`…ChangeRequested`, `…Updated`, `…ChangeDeclined`, `…Withdrawn`, `…Ended`, `…Stopped` (gửi cả hai bên, một lần mỗi chia sẻ).

## 6. Dừng chia sẻ (BR-424)

Chạy trong cùng transaction với thao tác của quản trị, sau khi thao tác đã lưu:

| Thao tác | Dừng gì |
|---|---|
| UC-06c vô hiệu hoá tài khoản (`PUT /api/users/{id}/status`) | mọi chia sẻ **Effective** mà tài khoản là chủ lịch hoặc người xem. Lời mời `Pending` được giữ (Q-C), bên được hỏi chỉ Từ chối được |
| UC-08 đổi vai trò (`PUT /api/users/{id}`) sang vai trò không có `MeetingCreator` | chia sẻ **Effective** mà người đó là người xem |
| UC-11 bỏ `MeetingCreator` khỏi vai trò (`PUT /api/roles/capabilities`) | chia sẻ **Effective** mà người giữ vai trò đó đang xem |

Chia sẻ đã quá ngày kết thúc (vẫn ghi `Effective` vì không có job) **không** bị dừng: giữ nguyên, không audit, không thông báo (BR-425, Q-B).
Mở lại tài khoản hoặc có lại quyền không làm chia sẻ chạy lại. Khoá tạm do gõ sai mật khẩu không tính (Q-A). Thao tác nào trên một
chia sẻ `Effective` thấy nó đã "chết" (vì một đường mất quyền chưa có hook) thì dừng nó ngay rồi trả `409 MSG450`.

## 7. Dữ liệu

Bảng `calendar_shares` (migration `AddCalendarShares`), mô tả ở [ERD](../02-design/02-erd.md) mục 10.
