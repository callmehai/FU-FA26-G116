# Kế hoạch triển khai BE — SCRUM-142 Chia sẻ lịch làm việc (UC-84a → UC-84f, UC-85c)

**Ngày:** 08/11/2026, sửa 10/11/2026 theo PO 09/11 · **Người viết:** BE (Huy) · **Nhánh:** `feat/scrum-142-calendar-sharing-be`
(tách từ `main` `098d9bf5`, đã gộp `main` `162b92b1`)

> **Sửa 10/11/2026.** PR #295 / [note 39](ban-giao-ba-final-review/39-scrum-142-thieu-sequence-chia-se-lich.md) bổ sung sequence còn
> thiếu và áp quyết định PO 09/11 (Q-A → Q-E). Note này đã sửa theo; chỗ nào khác bản 08/11 ghi rõ ở **§0b**. Contract chính thức:
> [23-chia-se-lich.md](../03-api/23-chia-se-lich.md).

## 0b. Quyết định PO 09/11/2026 đã áp vào code

| # | Quyết định | Trong code |
|---|---|---|
| Q-A | BR-424 chỉ tính tài khoản **bị vô hiệu hoá**; khoá tạm do sai mật khẩu không tính | Hook chỉ ở UC-06c (`IsActive`), không đụng luồng đăng nhập |
| Q-B | Hết ngày kết thúc thì chia sẻ tự hết: **không job, không trạng thái `Expired`, không thông báo** (`MSG440` ngừng dùng) | Bỏ worker, `ICalendarShareExpiry`, `Expired`. `CalendarShareRules.IsLapsed` = `EndDate < today`: đọc loại ra, thao tác trả `409 MSG450`, không ghi gì. Gửi chia sẻ mới cho cùng cặp thì dòng quá hạn được đóng `Ended` lặng lẽ (audit `calendar_share.closed_after_end_date`) để nhường unique index |
| Q-C | Vô hiệu hoá: dừng chia sẻ **Effective**; lời mời **Pending giữ lại**, bên được hỏi chỉ Từ chối; server chặn Chấp nhận | Stopper chỉ `status = 'Effective'`; `accept` khi người gửi bị vô hiệu hoá → `409 MSG468` (UC-84c EX-5); `senderDeactivated` + `actions = ["decline"]` |
| Q-D | Danh sách chia sẻ là UC con **UC-84f** | `GET /api/calendar/shares` |
| Q-E → **L8 (PO 10/11/2026, Jira 10498)** | Thay Q-E: người không có `MeetingCreator` vẫn vào màn Chia sẻ lịch, nhưng chỉ phần "lịch của tôi" (đang chia sẻ cho ai, đồng ý/từ chối yêu cầu, tự chia sẻ). "Được chia sẻ với tôi", Xin xem lịch, xem tuần và Tìm giờ họp chỉ cho người có `MeetingCreator` | Đúng như code: `asViewer` chỉ cho người có quyền, xin xem lịch / `share-candidates?purpose=request` / `shared-week` → `403 MSG448`; `asOwner` và tự chia sẻ mở cho mọi tài khoản. Không đổi code |
| — | Danh sách người để chọn: **một** endpoint `share-candidates?purpose=request\|direct&q=&page=&pageSize=`, lỗi đọc `MSG467` | Thay cho `owner-options`/`viewer-options` của bản 08/11 |

| Jira | Tên | Phần của note này |
|---|---|---|
| [SCRUM-142](https://sep490g116.atlassian.net/browse/SCRUM-142) | Chia sẻ lịch làm việc — ủy quyền xem bận/rảnh | Toàn bộ BE: UC-84a → UC-84e, dừng (BR-424), hết hạn (BR-425), UC-85c |
| [SCRUM-137](https://sep490g116.atlassian.net/browse/SCRUM-137) | Tìm giờ họp | Chỉ dùng lại `CalendarBusyReader` viết ở đây; UC-85a/85b **không** làm trong note này |

Note này viết đủ chi tiết để một người (hoặc một model AI nhỏ) làm theo từng bước mà không phải tự đoán nghiệp vụ.
Nguồn chuẩn, đọc trước khi code:

- Report 3 (bản sinh): [05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md) mục UC-84a → UC-84e, UC-85c,
  và các dòng mới của UC-06c (POST-5, bước 10), UC-08 (POST-5, bước 10, AF-1 bước 7), UC-11 (POST-4, bước 7), UC-12c (POST-4);
  [06-business-rules.md](../01-requirements/06-business-rules.md) BR-414 → BR-431. **Report 3 thắng note** khi hai bên khác nhau
  (ví dụ UC-84d AF-1: chủ lịch được đổi ngay cả khi bên xem đang có thay đổi chờ).
- Sequence: [docs/06-sequencediagram/calendar-sharing/](../06-sequencediagram/calendar-sharing/) — UC84a … UC84e, UC85c.
- Đặc tả sơ bộ: [NOTE-dac-ta-so-bo-scrum-137-142.md](NOTE-dac-ta-so-bo-scrum-137-142.md) (D12–D16, §5 mã, §6 API dự kiến).
- [ADR-0058](../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md), [ADR-0057](../05-decisions/0057-ba-muc-mat-nhu-ai-ecabinet.md).
- Quy ước: [AGENTS.md](../../AGENTS.md), [DEVELOPMENT_WORKFLOW.md](../DEVELOPMENT_WORKFLOW.md), skill `add-endpoint`,
  [danh mục mã](../03-api/00-danh-muc-ma-thong-bao.md).

## 0. Quyết định đã chốt trước khi code

| # | Quyết định | Ai chốt |
|---|---|---|
| P1 | Làm luôn UC-85c (`GET /api/calendar/shared-week`) trong SCRUM-142: AC7 cần nó; phần tính giờ bận viết một lần cho SCRUM-137 dùng lại | Huy, 08/11/2026 |
| P2 | "Hôm nay" luôn là ngày theo giờ Việt Nam (`Asia/Ho_Chi_Minh`, UTC+7), không theo giờ máy chủ (server test chạy UTC). ~~Worker quét hết hạn mỗi 15 phút~~ — bỏ theo PO 09/11 Q-B: không job | Huy, 08/11/2026 |
| P3 | Thiếu sequence cho bước "dừng chia sẻ" trong UC-06c/08/11, cho job hết hạn, cho danh sách chia sẻ và danh sách người để chọn: BE làm theo chữ Report 3 + BR-424/425/426 và ghi lệch lên Jira cho BA Final Review | Huy, 08/11/2026 |
| P4 | Q9 (BR-420) và Q10 (BR-431, 365 ngày) đã chốt theo đề xuất — comment Jira `10473` | PO (Hải), 08/11/2026 |
| P5 | Chỉ thêm **một bảng** `calendar_shares` (ADR-0058 bắt buộc). Loại thông báo mới chỉ là giá trị enum lưu dạng chuỗi (`notifications.type` là `varchar(64)`), không cần migration | BE |

## 1. Dữ liệu

### 1.1 Enum — `backend/TrustCabinet.Domain/Enums/CalendarSharingEnums.cs`

```csharp
public enum CalendarShareLevel { Level1, Level2 }          // BR-414: Mốc 1 = Normal; Mốc 2 = Normal + Internal; Secret không bao giờ
public enum CalendarShareStatus { Pending, Effective, Declined, Withdrawn, Ended, Stopped }   // không có Expired (PO 09/11 Q-B)
public enum CalendarShareInitiator { Viewer, Owner }      // Viewer = UC-84a (người xem xin); Owner = UC-84b (chủ lịch tự chia sẻ)
public enum CalendarShareStopReason { ViewerLostMeetingCreator, AccountDeactivated }   // ghi vào audit khi dừng
```

Trạng thái "mở" (chặn chia sẻ mới cho cùng cặp, BR-423) là `Pending` và `Effective`. Các trạng thái còn lại là cuối, không
bao giờ quay lại `Pending`/`Effective` (BR-424).

### 1.2 Entity — `backend/TrustCabinet.Domain/Entities/CalendarShare.cs`

| Thuộc tính | Kiểu | Ý nghĩa |
|---|---|---|
| `Id`, `CreatedAt`, `UpdatedAt` | từ `Entity` | khoá `Guid.CreateVersion7()` |
| `OwnerId` / `Owner` | `Guid` / `User` | chủ lịch |
| `ViewerId` / `Viewer` | `Guid` / `User` | người được xem (Meeting Organizer) |
| `InitiatedBy` | `CalendarShareInitiator` | ai gửi: quyết định ai trả lời (UC-84c) và ai rút (UC-84e) |
| `Level` | `CalendarShareLevel` | mốc đang áp dụng (hoặc đang xin, khi `Pending`) |
| `StartDate`, `EndDate` | `DateOnly` | khoảng ngày, tính cả hai đầu (BR-431) |
| `Status` | `CalendarShareStatus` | |
| `PendingLevel`, `PendingEndDate` | `CalendarShareLevel?`, `DateOnly?` | thay đổi bên xem đang xin (UC-84d); cả hai null khi không có |
| `PendingRequestedAt` | `DateTimeOffset?` | lúc xin thay đổi; **khác null ⇔ đang có thay đổi chờ** |
| `DecidedAt` | `DateTimeOffset?` | lúc bên được hỏi chấp nhận/từ chối |
| `ClosedAt`, `ClosedById` | `DateTimeOffset?`, `Guid?` | lúc đóng (Declined/Withdrawn/Ended/Stopped); người đóng, null khi hệ thống |

Thay đổi bên xem chỉ **nâng mốc** và/hoặc **kéo dài ngày kết thúc**; ngày bắt đầu không đổi (BR-431: "a changed range … may keep
its original start date"). Không có cột lưu mốc/khoảng xin ban đầu: khi chủ lịch thu hẹp lúc chấp nhận, dòng audit giữ cả hai.

### 1.3 Cấu hình EF — `backend/TrustCabinet.Infrastructure/Configurations/CalendarShareConfiguration.cs`

- Ba enum `HasConversion<string>().HasMaxLength(16)` (dự án lưu enum dạng chuỗi).
- `HasIndex(s => new { s.OwnerId, s.ViewerId }).IsUnique().HasFilter("status IN ('Pending', 'Effective')")`
  tên `ux_calendar_shares_open_pair` — BR-423, **có chiều**: (A chủ, B xem) khác (B chủ, A xem).
- `HasIndex(s => new { s.ViewerId, s.Status })` (UC-85c, danh sách "ai chia sẻ cho tôi"); `HasIndex(s => new { s.OwnerId, s.Status })`.
- Check constraints: `ck_calendar_shares_not_self` = `owner_id <> viewer_id`; `ck_calendar_shares_range` = `start_date <= end_date`;
  `ck_calendar_shares_pending_change` = `(pending_requested_at IS NULL) = (pending_level IS NULL) AND (pending_requested_at IS NULL) = (pending_end_date IS NULL)`.
- `Owner`, `Viewer`: `HasForeignKey`, `OnDelete(DeleteBehavior.Restrict)` (user không bao giờ bị xoá, chỉ khoá).
- `ClosedById`: FK tới `users`, `Restrict`, nullable.
- Thêm `public DbSet<CalendarShare> CalendarShares => Set<CalendarShare>();` vào `TrustCabinetDbContext`.

Migration (một migration duy nhất, commit cùng `TrustCabinetDbContextModelSnapshot`):

```bash
dotnet dotnet-ef migrations add AddCalendarShares --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir Migrations
```

Mở file migration sinh ra và kiểm: chỉ `CreateTable("calendar_shares")` + index + check, **không** đụng bảng khác. Nếu có
thay đổi lạ ở bảng khác thì dừng lại, tìm nguyên nhân (model lệch snapshot), không commit.

## 2. Quy tắc nghiệp vụ trong code

Đặt toàn bộ quy tắc thuần (không DB) trong `backend/TrustCabinet.Domain/CalendarSharing/CalendarShareRules.cs` để unit test được.

### 2.1 "Hôm nay" — `VietnamTime`

`backend/TrustCabinet.Domain/CalendarSharing/VietnamTime.cs`: `Zone` (thử `Asia/Ho_Chi_Minh`, rơi về `SE Asia Standard Time`, cuối
cùng `TimeZoneInfo.CreateCustomTimeZone("ICT", +7h)` — như `SummaryWriter.FindHanoi`), `Today(DateTimeOffset utcNow)`,
`StartOfDay(DateOnly)`, `At(DateOnly, TimeOnly)` trả `DateTimeOffset` có offset +07:00. Mọi so sánh ngày dùng `TimeProvider` được
inject (test thay đồng hồ).

### 2.2 Khoảng ngày (BR-431) — `MaxDays = 365`

`Length(start, end) = end.DayNumber - start.DayNumber + 1`.

| Thao tác | Hợp lệ khi | Sai → |
|---|---|---|
| Chia sẻ mới (UC-84a, UC-84b) | `today <= start <= end` và `Length <= 365` | `400 MSG443` |
| Chủ lịch thu hẹp khi chấp nhận yêu cầu UC-84a (UC-84c) | `mức mới <= mức xin`, `start_xin <= start <= end <= end_xin`, `end >= today` | `400 MSG443` |
| Bên xem chấp nhận chia sẻ UC-84b | không được đổi gì: trường gửi lên phải trống hoặc bằng đúng giá trị hiện tại | `400 MSG443` |
| Bên xem xin thay đổi (UC-84d Normal Flow) | `mức mới >= mức cũ`, `end mới >= end cũ`, có ít nhất một thứ tăng, `startDate` (nếu gửi) = start cũ, `end >= today`, `Length(start cũ, end mới) <= 365` | `400 MSG443` |
| Chủ lịch đổi ngay (UC-84d AF-1) | `mức mới <= mức cũ`, `start mới == start cũ` **hoặc** `start mới >= today`, `start <= end`, `end >= today`, `Length <= 365`, có ít nhất một thứ đổi | `400 MSG443` |
| Chủ lịch duyệt thay đổi đang chờ (UC-84c AF-1) | kiểm lại lúc duyệt: `PendingEndDate >= today`, `Length(start, PendingEndDate) <= 365`; trường gửi lên phải trống | `400 MSG443` |

Mốc (`level`) nhận chuỗi `"Level1"`/`"Level2"` đúng chữ hoa thường; giá trị khác cũng là `400 MSG443` (lệch: MSG443 nay gồm cả
"mốc không hợp lệ" — ghi Jira, xem §9).

### 2.3 Chia sẻ còn "sống" (BR-424)

Một chia sẻ `Effective` chỉ còn sống khi: chủ lịch đang hoạt động **và** người xem đang hoạt động **và** vai trò người xem còn
`MeetingCreator`. Dừng được lưu lại (`Stopped`) ngay trong thao tác làm mất điều kiện (§5); ngoài ra, mọi thao tác ghi trên một
chia sẻ `Effective` mà thấy nó đã chết thì **dừng nó ngay** (lưu `Stopped` + audit + báo hai bên) rồi trả `409 MSG450` — phòng khi
có đường làm mất quyền nào chưa gắn hook. Chia sẻ `Pending` **không** bị dừng (PO 09/11 Q-C): chấp nhận lời mời có người gửi bị vô
hiệu hoá → `409 MSG468`; chấp nhận yêu cầu mà người xem đã mất `MeetingCreator` → `409 MSG450`; từ chối luôn được.

### 2.4 Hết ngày kết thúc (BR-425, PO 09/11 Q-B)

Không job, không trạng thái, không thông báo. `IsLapsed(share, today)` = `EndDate < today`. Đọc (UC-84f, UC-85c, `hasOpenShare`):
lọc `end_date >= today`. Ghi: thao tác trên chia sẻ quá ngày trả `409 MSG450`, **không** ghi gì. Gửi chia sẻ mới cho một cặp mà dòng
cũ còn `Pending`/`Effective` nhưng đã quá ngày: đóng dòng cũ thành `Ended` (người đóng null, audit `calendar_share.closed_after_end_date`,
không thông báo) trong cùng transaction rồi tạo mới — nếu không unique index (§1.3) sẽ chặn cặp đó mãi.

### 2.5 Ai làm gì (bảng hành động)

"Bên gửi" = người xem nếu `InitiatedBy = Viewer`, chủ lịch nếu `InitiatedBy = Owner`. "Bên được hỏi" = bên còn lại.

| Trạng thái | Người gọi | Được làm (`actions` trong response) |
|---|---|---|
| `Pending` | bên được hỏi | `accept`, `decline` — chỉ `decline` khi người gửi bị vô hiệu hoá (`senderDeactivated`) |
| `Pending` | bên gửi | `withdraw` |
| `Effective`, không có thay đổi chờ | người xem | `requestChange`, `end` |
| `Effective`, không có thay đổi chờ | chủ lịch | `update`, `end` |
| `Effective`, có thay đổi chờ | người xem | `withdraw`, `end` |
| `Effective`, có thay đổi chờ | chủ lịch | `accept`, `decline`, `update`, `end` |
| trạng thái cuối, hoặc quá ngày kết thúc | cả hai | không có gì (`[]`) |

Người không phải một trong hai bên: mọi endpoint theo id trả `404` không body (POST-3: không lộ gì về chia sẻ hay tài khoản).
Người là một bên nhưng gọi hành động không có trong bảng: `409 MSG450`, **trừ** `accept`/`decline` khi có thứ đang chờ mà người
gọi không phải bên được hỏi → `404` (UC-84c EX-1).

## 3. API — `backend/TrustCabinet.Web/Endpoints/CalendarSharingEndpoints.cs`

Mọi route: `RequireActiveUser(databaseFailure)` (chưa đăng nhập `401`, tài khoản bị khoá `403` không body). Đọc dùng
`databaseFailure = 500 MSG453`, ghi dùng `503 MSG447`. Quyền `MeetingCreator` kiểm **trong service** để trả `403 MSG448` có body
(không dùng `RequireCapability`, vì nó trả `403` trống). ID người gọi luôn lấy từ token (`CapabilityGate.GatedUserId`).

Khai báo mã ở đầu file, đúng mẫu: `private const string Msg435Requested = "MSG435";` … — không có literal `"MSGnn"` trong thân hàm.

| Method | Path | UC | Body | Thành công | Lỗi |
|---|---|---|---|---|---|
| `GET` | `/api/calendar/shares` | UC-84f | — | `200 { asOwner: Share[], asViewer: Share[] }`, mới nhất trước, bỏ chia sẻ quá ngày; `asViewer` rỗng nếu không có `MeetingCreator` | `500 MSG453` |
| `GET` | `/api/calendar/share-candidates?purpose=request\|direct&q=&page=&pageSize=` | UC-84a/84b bước 1 | — | `200 { items: [{ id, fullName, email, hasOpenShare }], total, page, pageSize }`; `request` = mọi tài khoản hoạt động trừ mình, `direct` = người có `MeetingCreator` trừ mình | `403 MSG448` (`request`), `400` không body (query sai), `500 MSG467` |
| `POST` | `/api/calendar/shares` | UC-84a | `{ ownerId, level, startDate, endDate }` | `201 { code: MSG435, message, share }` | `403 MSG448` → `404` (chủ lịch là mình/không có/bị khoá) → `409 MSG449` → `400 MSG443`; `503 MSG447` |
| `POST` | `/api/calendar/shares/direct` | UC-84b | `{ viewerId, startDate, endDate }` (luôn Mốc 1) | `201 { code: MSG441, … }` | `400 MSG455` (bên nhận là mình/không có/bị khoá/không có `MeetingCreator`) → `409 MSG449` → `400 MSG443`; `503 MSG447` |
| `POST` | `/api/calendar/shares/{id}/accept` | UC-84c | tuỳ chọn `{ level?, startDate?, endDate? }` | `200 MSG436` (chia sẻ mới hiệu lực) hoặc `200 MSG442` (duyệt thay đổi) | `404`, `409 MSG468` (người gửi bị vô hiệu hoá, EX-5), `409 MSG450`, `400 MSG443`, `503 MSG447` |
| `POST` | `/api/calendar/shares/{id}/decline` | UC-84c | — | `200 MSG437` (từ chối chia sẻ) hoặc `200 MSG452` (từ chối thay đổi) | `404`, `409 MSG450`, `503 MSG447` |
| `PATCH` | `/api/calendar/shares/{id}` | UC-84d | `{ level?, startDate?, endDate? }` | người xem: `201 MSG454` (thay đổi chờ); chủ lịch: `200 MSG442` (áp dụng ngay) | `404`, `409 MSG450`, `400 MSG443`, `503 MSG447` |
| `POST` | `/api/calendar/shares/{id}/withdraw` | UC-84e | — | `200 MSG438` | `404`, `409 MSG450`, `503 MSG447` |
| `POST` | `/api/calendar/shares/{id}/end` | UC-84e AF-1 | — | `200 MSG451` | `404`, `409 MSG450`, `503 MSG447` |
| `GET` | `/api/calendar/shared-week?week=YYYY-MM-DD` | UC-85c | — | `200 SharedWeek` | `403 MSG448`, `500 MSG453`; `week` sai định dạng → `400` của framework |

Một `PATCH` duy nhất cho UC-84d, server phân biệt theo người gọi — đúng sequence `changeShare(shareId, actorId, …)`.

Body JSON đọc bằng record có trường nullable (`string? Level`, `DateOnly? StartDate`, …); thiếu trường bắt buộc → `400 MSG443`.

### 3.1 Hình dạng `Share`

```json
{
  "id": "…", "initiatedBy": "Viewer", "level": "Level2",
  "owner":  { "id": "…", "fullName": "Trần Chủ Lịch", "email": "…" },
  "viewer": { "id": "…", "fullName": "Lê Người Xem", "email": "…" },
  "startDate": "2026-11-08", "endDate": "2026-12-07",
  "status": "Effective",
  "pendingChange": { "level": "Level2", "endDate": "2027-01-31", "requestedAt": "…" },
  "createdAt": "…", "decidedAt": "…", "closedAt": null,
  "actions": ["accept", "decline", "update", "end"]
}
```

`pendingChange` là `null` khi không có. Chia sẻ quá ngày kết thúc không bao giờ được trả (§2.4). `senderDeactivated` = lời mời có người gửi bị vô hiệu hoá (UC-84c EX-5). Không bao giờ có trường nào về cuộc họp (BR-427).

### 3.2 Hình dạng `SharedWeek` (UC-85c)

```json
{
  "weekStart": "2026-11-09", "weekEnd": "2026-11-13", "dayStart": "08:00", "dayEnd": "17:00",
  "people": [
    { "shareId": "…", "owner": { "id": "…", "fullName": "…", "email": "…" }, "status": "Effective",
      "level": "Level1", "startDate": "…", "endDate": "…",
      "days": [ { "date": "2026-11-09", "known": true, "busy": [ { "start": "2026-11-09T09:00:00+07:00", "end": "2026-11-09T10:30:00+07:00" } ] },
                { "date": "2026-11-13", "known": false, "busy": [] } ] },
    { "shareId": "…", "owner": { … }, "status": "Stopped", "level": "Level1", "startDate": "…", "endDate": "…", "days": [] }
  ]
}
```

- `week` vắng → tuần hiện tại theo giờ VN; một ngày bất kỳ được đưa về thứ Hai của tuần đó.
- Mỗi chủ lịch xuất hiện **một lần**: lấy chia sẻ mới nhất (theo `CreatedAt`) của cặp (chủ lịch, người gọi) có trạng thái
  `Effective` hoặc `Stopped` và chưa quá ngày kết thúc. `Pending`, `Declined`, `Withdrawn`, `Ended` không hiện.
- `Stopped`: `days = []` (không có giờ nào). FE hiện nhãn "Đã dừng" và câu `MSG439` khi bấm vào. Chia sẻ quá ngày kết thúc không hiện.
- `Effective`: năm ngày thứ Hai → thứ Sáu; ngày ngoài `[StartDate, EndDate]` có `known = false` (BR-415, "chưa biết").
- `busy` đã gộp các khoảng chồng/chạm nhau và cắt vào 08:00–17:00 (BR-427: không lộ có mấy cuộc họp, không tên, không địa điểm).
- Không có người nào → `people = []` (AF-1).

## 4. Thông báo và audit (BR-426)

### 4.1 Loại thông báo — thêm vào cuối `NotificationType` (`PlatformEnums.cs`)

`CalendarShareRequested`, `CalendarShareReceived`, `CalendarShareAccepted`, `CalendarShareDeclined`, `CalendarShareChangeRequested`,
`CalendarShareUpdated`, `CalendarShareChangeDeclined`, `CalendarShareWithdrawn`, `CalendarShareEnded`, `CalendarShareStopped` (không có loại hết hạn — PO Q-B).

Mọi thông báo: `TargetType = "CalendarShare"`, `TargetId = share.Id`, `MeetingId = null`. Câu chữ (tiếng Việt, ngày `dd/MM/yyyy`):

| Sự kiện | Gửi ai | Title | Body |
|---|---|---|---|
| UC-84a gửi yêu cầu | chủ lịch | `Yêu cầu xem lịch` | `{người xem} xin xem lịch bận/rảnh của bạn ({mốc}, {từ}–{đến}).` |
| UC-84b chia sẻ thẳng | người xem | `Được chia sẻ lịch` | `{chủ lịch} chia sẻ lịch bận/rảnh với bạn (Mốc 1, {từ}–{đến}). Hãy đồng ý hoặc từ chối.` |
| UC-84c chấp nhận | bên gửi | `Chia sẻ lịch đã được chấp nhận` | `{người trả lời} đã chấp nhận chia sẻ lịch ({mốc}, {từ}–{đến}).` |
| UC-84c từ chối | bên gửi | `Chia sẻ lịch bị từ chối` | `{người trả lời} đã từ chối chia sẻ lịch.` |
| UC-84d bên xem xin thay đổi | chủ lịch | `Yêu cầu thay đổi chia sẻ lịch` | `{người xem} xin đổi chia sẻ lịch thành {mốc}, đến {đến}.` |
| UC-84d AF-1 / UC-84c AF-1 duyệt | người xem | `Chia sẻ lịch đã thay đổi` | `{chủ lịch} đã cập nhật chia sẻ lịch: {mốc}, {từ}–{đến}.` |
| UC-84c AF-1 từ chối thay đổi | người xem | `Thay đổi chia sẻ lịch bị từ chối` | `{chủ lịch} đã từ chối thay đổi chia sẻ lịch. Chia sẻ giữ như cũ.` |
| UC-84e rút | bên còn lại | `Yêu cầu chia sẻ lịch đã được rút` | `{người rút} đã rút yêu cầu chia sẻ lịch.` |
| UC-84e AF-1 ngừng | bên còn lại | `Chia sẻ lịch đã ngừng` | `{người ngừng} đã ngừng chia sẻ lịch.` |
| BR-424 dừng | **cả hai** | `Chia sẻ lịch đã dừng` | `Chia sẻ lịch giữa {chủ lịch} và {người xem} đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị khoá.` |

`DeduplicationKey`: `null` cho thông báo do người bấm (mỗi lần bấm là một sự kiện); `calendar-share-stopped:{shareId:D}:{userId:D}`
cho dừng (mỗi chia sẻ chỉ dừng một lần).

Bấm thông báo: `NotificationService.GetTargetAsync` mở được `CalendarShare` khi người nhận là chủ lịch hoặc người xem của chia sẻ đó;
`NotificationTargetResponse.BuildUrl("CalendarShare", id)` = `/calendar/sharing?share={id}` (FE có thể đổi route, báo BE để sửa một dòng).

### 4.2 Audit — thêm vào `AuditActions`

`calendar_share.requested`, `calendar_share.shared`, `calendar_share.accepted`, `calendar_share.declined`,
`calendar_share.change_requested`, `calendar_share.change_accepted`, `calendar_share.change_declined`, `calendar_share.updated`,
`calendar_share.withdrawn`, `calendar_share.change_withdrawn`, `calendar_share.ended`, `calendar_share.stopped`,
`calendar_share.closed_after_end_date` (§2.4); `CalendarShareObjectType = "CalendarShare"`. `ObjectId` = id chia sẻ; `details` có `ownerId`, `viewerId`,
`level`, `startDate`, `endDate`, và `before`/`after` cho thay đổi, `reason` (`viewerLostMeetingCreator` | `accountDeactivated`) cho dừng.
Actor: người bấm; `null` khi hệ thống đóng; người quản trị cho dừng.

### 4.3 Thứ tự trong một thao tác ghi (giống sequence: kiểm → lưu + audit → báo)

1. Mở transaction Read Committed (nếu chưa có transaction bao ngoài — xem `AdministratorInvariantGuard`).
2. `SELECT * FROM calendar_shares WHERE id = {id} FOR UPDATE` (FromSqlInterpolated, tracked).
3. Kiểm theo bảng §2 (404 → quá ngày kết thúc / chết → 409 → 400).
4. Sửa entity, `db.Notifications.Add(...)`, rồi `audit.RecordAsync(...)` — `AuditService` tự `SaveChangesAsync` nên dòng chia sẻ,
   thông báo và audit lưu chung một lần trong transaction. Commit.
5. Lỗi DB (`DbUpdateException`, `NpgsqlException`, `TimeoutException`) → `503 MSG447`, không ai được báo (transaction rollback).
   Vi phạm `ux_calendar_shares_open_pair` khi insert (hai yêu cầu cùng lúc) → `409 MSG449`.

`NotificationWorker` có sẵn đẩy các dòng đã commit qua SignalR — không cần gọi hub.

## 5. Dừng chia sẻ (BR-424) — gắn vào ba chỗ đã có

Interface `ICalendarShareStopper` (Domain) / `CalendarShareStopper` (Infrastructure):

```csharp
/// Stops every open share touching these users that is no longer live (BR-424). Runs inside the caller's transaction.
Task<int> StopDeadSharesAsync(IReadOnlyCollection<Guid> userIds, Guid? actorId, CalendarShareStopReason reason, CancellationToken ct);
```

Cách làm: `SELECT * FROM calendar_shares WHERE status = 'Effective' AND end_date >= {today} AND (owner_id = ANY({ids}) OR viewer_id = ANY({ids})) ORDER BY id FOR UPDATE` (chia sẻ quá ngày kết thúc giữ nguyên, không báo — review PR #315),
đọc trạng thái hiện tại của hai bên (active + `MeetingCreator` của người xem, **đọc sau khi thay đổi đã `SaveChanges`** trong cùng
transaction), dừng chia sẻ nào chết: `Status = Stopped`, `ClosedAt`, `ClosedById = actor`, xoá thay đổi chờ, báo cả hai, audit
`calendar_share.stopped`. Không mở transaction riêng: chạy trong transaction của `IAdministratorInvariantGuard`.

| Chỗ gắn | File | Khi nào gọi |
|---|---|---|
| UC-06c khoá tài khoản | `UserService.ChangeStatusWithinTransactionAsync` | sau `users.SaveChangesAsync` + audit, chỉ khi `!isActive`; `reason = AccountDeactivated` |
| UC-08 đổi vai trò | `UserService.UpdateWithinTransactionAsync` | sau khi lưu, chỉ khi `before.RoleId != role.Id`; `reason = ViewerLostMeetingCreator` |
| UC-11 sửa ma trận quyền | `RolePermissionService.UpdateWithinTransactionAsync` | sau audit, với `FindActiveUserIdsInRoleAsync(roleId)`, chỉ khi `before` có `MeetingCreator` mà `requested` không có |

Mở khoá tài khoản hoặc có lại quyền **không** làm gì với chia sẻ đã dừng (BR-424). Xoá vai trò (UC-12c) không gọi gì (POST-4).

Thêm tham số mới vào constructor `UserService` và `RolePermissionService`, đăng ký `services.AddScoped<ICalendarShareStopper, CalendarShareStopper>()`
trong `AddTrustCabinetCalendarSharing()`. Các test dựng service bằng tay hoặc thay repo bằng fake phải truyền/đăng ký
`FakeCalendarShareStopper` (§7.3).

## 6. Hết ngày kết thúc (BR-425) — không có job

PO 09/11 Q-B bỏ worker và trạng thái `Expired` (bản 08/11 có `CalendarShareExpiryWorker` 15 phút — đã xoá). Chỉ còn quy tắc đọc ở §2.4.

## 7. UC-85c — giờ bận (BR-414 → BR-420, BR-427)

`backend/TrustCabinet.Domain/CalendarSharing/CalendarBusyRules.cs` (thuần, unit test được):

- `BusyStatuses` = `WaitingApproval, Approved, Published, InProgress, Ended` (BR-420). `Draft, Rejected, Cancelled` không bận.
- Người tính bận (BR-419): dòng `meeting_participants` có `AssignmentStatus = Current`, `Role ∈ {Chair, Secretary, Member}`,
  không uỷ quyền (`Response != Delegated`, `DelegatedToUserId == null`), và (`Role == Chair` **hoặc** `Response != Declined`) —
  Chủ toạ không bao giờ được hỏi nên câu trả lời cũ của họ không tính (như `MeetingRepository`). Người tạo, người duyệt, Khách không tính.
- Mốc (BR-414, BR-417): `Level1` thấy `Normal`; `Level2` thấy `Normal` + `Internal`; `Secret` không bao giờ. Lịch của chính người
  tìm (SCRUM-137, BR-418) truyền `level = null` = mọi mức.
- Khoảng bận của một cuộc họp: bắt đầu `ActualStartedAt ?? PlannedStartTime` (khi đã bắt đầu) hoặc `PlannedStartTime`; kết thúc
  `ActualEndedAt ?? PlannedEndTime` khi `Ended`, `max(PlannedEndTime, now)` khi `InProgress`, `PlannedEndTime` các trạng thái khác.
- `Merge(intervals)`: sắp theo đầu, gộp khoảng chồng **hoặc chạm** nhau.

`backend/TrustCabinet.Infrastructure/CalendarSharing/CalendarBusyReader.cs` — **public, SCRUM-137 dùng lại**:

```csharp
Task<IReadOnlyDictionary<Guid, IReadOnlyList<BusyInterval>>> ReadAsync(
    IReadOnlyDictionary<Guid, CalendarShareLevel?> people, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
```

Một truy vấn `MeetingParticipants` (AsNoTracking) lọc người, vai trò, trạng thái, `Sensitivity != Secret` khi có mốc, và chồng
lấn thô `[from, to)`; tính khoảng chính xác và lọc mốc trong bộ nhớ; gộp theo người. `SharedCalendarService` dùng nó rồi cắt từng ngày.

## 8. Thứ tự làm (checklist cho người code)

Mỗi bước build được trước khi sang bước sau (`dotnet build TrustCabinet.sln`).

1. **Domain** — `Enums/CalendarSharingEnums.cs`; `Entities/CalendarShare.cs`; `CalendarSharing/VietnamTime.cs`;
   `CalendarSharing/CalendarShareRules.cs`; `CalendarSharing/CalendarBusyRules.cs`; `CalendarSharing/CalendarSharingContracts.cs`
   (record view, command, `CalendarShareFailure`, `CalendarShareOutcome`, interface `ICalendarSharingService`, `ISharedCalendarService`,
   `ICalendarShareStopper`, `CandidateQuery`/`ShareCandidatePage`); `NotificationType` + `AuditActions` thêm giá trị.
2. **EF** — `CalendarShareConfiguration.cs`, `DbSet`, migration `AddCalendarShares` (§1.3), kiểm file migration.
3. **Services** — `CalendarShareNotifications.cs` (dựng `Notification` + câu chữ §4.1), `CalendarSharingService.cs` (danh sách,
   `share-candidates`, UC-84a → UC-84e), `CalendarShareCloser.cs`, `CalendarShareStopper.cs`, `CalendarBusyReader.cs`,
   `SharedCalendarService.cs`, `CalendarSharingServiceCollectionExtensions.AddTrustCabinetCalendarSharing()`.
4. **Hook** — `UserService`, `RolePermissionService` (§5); `NotificationService.CanOpenTargetAsync` + `BuildUrl` (§4.1).
5. **Web** — `CalendarSharingEndpoints.cs` (§3), `Program.cs` (`AddTrustCabinetCalendarSharing()`, `app.MapCalendarSharingEndpoints()`).
6. **Test** (§9) rồi **Docs** (§10).
7. Kiểm (§11), chạy `authz-reviewer`, `ef-migration-reviewer`, `docs-sync-reviewer`.

Không sửa: `GET /api/meetings`, luồng tạo cuộc họp, FE. Không thêm capability, vai trò hay bảng nào khác.

## 9. Test

Fixture mới `tests/TrustCabinet.Web.Tests/CalendarSharing/CalendarSharingFixture.cs`: PostgreSQL thật (`PostgreSqlTestDatabase`,
`TestSchema.Migrated`) như `SummaryFixture`; đồng hồ giả (`TimeProvider`) đặt 08/11/2026 10:00 giờ VN; `Seed()` tạo mỗi lần một bộ
người riêng (email ngẫu nhiên) để các test chạy song song không giẫm nhau: `Organizer` (vai trò Thư ký — có `MeetingCreator`),
`Organizer2` (Chủ toạ), `Owner` (Thành viên — không có `MeetingCreator`), `Owner2`, `Locked` (đã khoá), `Admin` (Quản trị);
`Client(userId)` cấp token thật.

| File | Loại | Phủ |
|---|---|---|
| `CalendarShareRulesTests.cs` | unit | BR-431 từng dòng bảng §2.2 (biên 365/366 ngày, hôm nay, quá khứ), `VietnamTime.Today` qua nửa đêm UTC+7, bảng hành động §2.5 |
| `CalendarBusyRulesTests.cs` | unit | BR-414/417 mốc, BR-419 vai trò/từ chối/Chủ toạ, BR-420 trạng thái, khoảng InProgress/Ended, `Merge` chồng/chạm/rời |
| `CalendarShareEndpointTests.cs` | tích hợp | UC-84a (201 MSG435 + thông báo chủ lịch + audit; 403 MSG448; 404 cho chính mình/không có/bị khoá; 409 MSG449; 400 MSG443; cặp ngược chiều vẫn được), UC-84b (201 MSG441 Mốc 1; 400 MSG455; 409), UC-84c (chấp nhận 200 MSG436, thu hẹp hợp lệ/không hợp lệ, từ chối 200 MSG437, bên gửi gọi accept → 404, người lạ 404, đã đóng 409 MSG450), UC-84d (201 MSG454, thay đổi thứ hai 409, duyệt 200 MSG442, từ chối 200 MSG452, chủ lịch đổi ngay 200 MSG442 kể cả khi có thay đổi chờ, giá trị sai 400), UC-84e (rút yêu cầu/rút thay đổi 200 MSG438, ngừng 200 MSG451 cả hai bên, sai trạng thái 409), danh sách + `actions`, options, 401, hết hạn tính ngay khi đọc/ghi, lỗi DB 503 MSG447 và 500 MSG453, thông báo mở được qua `/api/notifications/{id}/target` |
| `CalendarShareStopTests.cs` | tích hợp | khoá chủ lịch / người xem (`PUT /api/users/{id}/status`) → `Stopped` + báo hai bên; mở khoá không chạy lại; đổi vai trò người xem sang Thành viên (`PUT /api/users/{id}`); bỏ `MeetingCreator` khỏi vai trò (`PUT /api/roles/capabilities`); chia sẻ `Pending` cũng dừng; chia sẻ người không liên quan không đổi |
| `SharedWeekEndpointTests.cs` | tích hợp | 403 MSG448; tuần trống; khối bận đúng mốc (Internal ẩn ở Mốc 1, hiện ở Mốc 2, Secret không bao giờ), ngày ngoài khoảng `known=false`, Stopped không có giờ, chia sẻ quá ngày không hiện, gộp khoảng, không có trường tên/địa điểm trong JSON, Draft/Cancelled/người từ chối không bận |
| `DatabaseModelTests.cs` (sửa) | model | thêm `calendar_shares` vào danh sách bảng; enum lưu text; unique có filter; FK `Restrict` |
| `UserEndpointsTests`, `RolePermissionEndpointsTests`, `CapabilityGateTests`, `UserServiceTests`, `RolePermissionServiceTests` (sửa) | — | đăng ký/truyền `FakeCalendarShareStopper`; thêm assert stopper được gọi trong guard khi khoá / đổi vai trò / bỏ `MeetingCreator` |

`MessageCodeCatalogueTests` tự đòi: mỗi mã BE khai báo phải có dòng catalogue có HTTP và có ít nhất một test chứa chuỗi `"MSGnn"`.

## 10. Tài liệu cập nhật cùng commit code

- `docs/03-api/23-chia-se-lich.md` — contract §3 (tiếng Việt), và `docs/03-api/23-chia-se-lich-note-cho-fe.md` — ghi chú cho FE (§12);
  thêm hai dòng vào `docs/03-api/README.md`.
- `docs/03-api/00-danh-muc-ma-thong-bao.md` — mục mới "Chia sẻ lịch — SCRUM-142" với dòng có HTTP cho mã BE gửi; `MSG439`
  dòng `—` trạng thái FE (`MSG440` đã ngừng dùng); sửa các dòng "Dành sẵn" `MSG435`–`MSG455` của SCRUM-142 thành "Đã triển khai SCRUM-142"; câu ở "Mã tiếp theo".
  Không cấp mã mới (mã tiếp theo vẫn `MSG457`).
- `docs/02-design/02-erd.md` — mục mới "10. Chia sẻ lịch" (bảng, ràng buộc, lý do), dòng "Trạng thái triển khai".
- `docs/notes/README.md` — dòng cho note này.
- Không sửa Report 3 / extract (sinh từ DOCX — việc của BA).

## 11. Kiểm trước khi báo xong

```bash
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
```

Cần PostgreSQL chạy (`docker compose up -d`) và `POSTGRES_PASSWORD` trong `.env` hoặc `TRUSTCABINET_TEST_CONNECTION_STRING`.
Không commit, không push, không merge khi chưa được đồng ý.

## 12. Ghi chú cho FE (người làm FE-16)

Chi tiết ở `docs/03-api/23-chia-se-lich-note-cho-fe.md`. Tóm tắt:

- Nút nào hiện thì đọc `actions` của từng chia sẻ, không tự suy từ trạng thái.
- `MSG439` là câu FE tự hiện cho nhãn "Đã dừng" (BE chỉ trả `status`). Không có "Hết hạn": chia sẻ quá ngày biến khỏi danh sách.
- Lời mời có `senderDeactivated = true`: chỉ nút Từ chối, hiện câu `MSG468`. Yêu cầu mà người xin đã mất quyền tạo cuộc họp cũng chỉ còn Từ chối (`actions = ["decline"]`).
- Thông báo chia sẻ lịch có `targetType = "CalendarShare"`; `/api/notifications/{id}/target` trả `url = /calendar/sharing?share={id}`.
- Mọi ngày là `YYYY-MM-DD` theo lịch Việt Nam; giờ bận là ISO có `+07:00`.
- UC-85c: `known = false` là "chưa biết", không được vẽ như rảnh.

## 13. Lệch khỏi ticket / tài liệu — ghi lên Jira SCRUM-142

| # | Lệch | Lý do |
|---|---|---|
| L1 | "Hôm nay" theo giờ Việt Nam (`Asia/Ho_Chi_Minh`), không theo giờ máy chủ. Không job (PO Q-B) | Server test chạy UTC: nửa đêm máy chủ là 07:00 sáng VN |
| L2 | `MSG443` dùng cho mọi giá trị chia sẻ không hợp lệ: khoảng ngày **và** mốc (mốc lạ, bên xem xin hạ mốc, chủ lịch "đổi" mà không đổi gì, bên xem sửa giá trị khi chấp nhận chia sẻ UC-84b) | Không thêm mã; FE chỉ có hai lựa chọn mốc |
| L3 | UC-84d dùng một `PATCH`, server phân theo người gọi; bên xem nhận `201 MSG454`, chủ lịch `200 MSG442` | Khớp sequence UC84d (`changeShare`) |
| L4 | `share-candidates` trả thêm `email` (đặc tả: "cân nhắc bỏ") để phân biệt người trùng tên; query sai trả `400` không mã; bỏ query `userIds` của `shared-week` (FE lọc tại chỗ) | Cần cho FE |
| L5 | ~~Thiếu sequence~~ — đã bổ sung ở PR #295 (note 39), 09/11/2026 | — |
| L6 | Bên gửi gọi `accept`/`decline` trên yêu cầu của chính mình → `404` (UC-84c EX-1), người là một bên gọi hành động sai trạng thái → `409 MSG450` | Theo đúng EX-1/EX-2 |
| L7 | Từ chối thay đổi (UC-84c AF-1) chỉ báo người xem (sequence), không báo người bấm như câu "both sides are notified" | Người bấm đã thấy `MSG452` |
| L8 | UC-84f gác cả màn bằng `MeetingCreator` (Q-E), trái BR-430 | **Đã chốt PO 10/11/2026 (Jira 10498):** chỉ gác phần "được chia sẻ với tôi", Xin xem lịch, xem tuần, Tìm giờ họp — đúng code. BA sửa UC-84f và §6 đặc tả sơ bộ |
| L9 | Gửi chia sẻ mới cho một cặp mà dòng cũ đã quá ngày nhưng vẫn `Pending`/`Effective`: dòng cũ thành `Ended` (người đóng null, audit `calendar_share.closed_after_end_date`, không thông báo) | Không có job nên phải nhường chỗ trong unique index (BR-423) |
| L10 | Chấp nhận yêu cầu UC-84a mà người xem đã mất `MeetingCreator` (chưa bị vô hiệu hoá) → `409 MSG450`; chia sẻ `Pending` không bị dừng | Đặc tả chỉ nói trường hợp người gửi bị vô hiệu hoá (EX-5) |

## 14. Nhật ký quyết định

| Ngày | Quyết định | Nguồn | Ảnh hưởng code |
|---|---|---|---|
| 08/11/2026 | Q9 (BR-420 trạng thái tính bận), Q10 (BR-431 tối đa 365 ngày) | PO, Jira 10473 | Theo đề xuất |
| 09/11/2026 | Q-A → Q-E: chỉ vô hiệu hoá mới dừng; không job, không `Expired`; dừng chỉ Effective, Pending giữ, chặn Chấp nhận `MSG468`; UC-84f; (Q-E thay bởi L8) | PO, Jira 10490, PR #295 | Đã làm (§0b) |
| 10/11/2026 | Stopper không dừng chia sẻ đã quá ngày kết thúc (không audit, không báo) | Review PR #315 (callmehai) | `CalendarShareStopper` lọc `end_date >= today` |
| 10/11/2026 | **L8**: người không có `MeetingCreator` vẫn vào màn Chia sẻ lịch, chỉ phần "lịch của tôi" | PO, Jira 10498 | Không đổi (code đã đúng) |
| 10/11/2026 | Yêu cầu có người xin đã mất quyền tạo cuộc họp: danh sách chỉ còn Từ chối (khớp với `409 MSG450` khi Chấp nhận) | BE, theo phương án A của P3 | `CalendarShareRules.Actions(…, acceptBlocked)` |
| 11/11/2026 | Duyệt thay đổi chờ không còn hợp với khoảng hiện tại (chủ lịch dời ngày bắt đầu qua ngày kết thúc được xin, ngày xin đã qua, quá 365 ngày) → `409 MSG450`, `actions` bỏ `accept`. Trước đây: `503 MSG447` khi đụng `ck_calendar_shares_range`, `400 MSG443` khi ngày xin đã qua | Review của đối tác 11/11/2026 | `ValidPendingApproval` thêm `end >= StartDate`; `AcceptAsync` tách giá trị gửi sai (`MSG443`) với thay đổi không còn hợp (`MSG450`); `Actions` |
| 11/11/2026 | Chấp nhận một chia sẻ `Pending` giữ khoá `FOR SHARE` trên hàng `users` của hai bên tới khi commit, nên UC-06c/UC-08 chạy cùng lúc hoặc chờ rồi dừng chia sẻ, hoặc được thấy và chặn (`MSG468`/`MSG450`). UC-11 (đổi quyền của vai trò) không ghi vào `users` nên chưa được khoá này che; vẫn còn kiểm `IsLive` mỗi lần thao tác và `StatusOf` của tuần | Review của đối tác 11/11/2026 | `CalendarSharePeople.LockAndLoadAsync` |
| 11/11/2026 | Không đổi: tuần được chia sẻ vẫn hiện "Đã dừng" từ chia sẻ `Stopped` cũ dù người xem đã "Thôi xem" chia sẻ mới hơn (đúng câu chữ contract §UC-85c); thông báo `Stopped` cho người mất quyền mở trang không có dòng (L8, ghi cho FE) | Review của đối tác 11/11/2026 | Không; cần thì BA/PO quyết |
| *chờ* | **P1** `share-candidates` có trả `email`; **P2** đóng dòng quá hạn có ghi audit; **P3** mã lỗi khi chấp nhận yêu cầu của người mất quyền; **P4** duyệt thay đổi sau khi chủ lịch đã hạ mốc (cũng gồm: chủ lịch kéo dài khoảng nên thay đổi chờ thành *rút ngắn*) | [PR #315 comment](https://github.com/callmehai/FU-FA26-G116/pull/315#issuecomment-6770385798) | Phương án A = code hiện tại; chọn B thì sửa theo cột cuối của bảng câu hỏi |
