# Kế hoạch triển khai BE — SCRUM-137 Tìm giờ họp (UC-85a, UC-85b)

**Ngày:** 11/11/2026 · **Người viết:** BE (Huy) · **Nhánh:** `feat/scrum-137-find-slots-be` (tách từ `main` `2ee7b058`)

| Jira | Tên | Phần của note này |
|---|---|---|
| [SCRUM-137](https://sep490g116.atlassian.net/browse/SCRUM-137) | Tìm giờ họp: tìm khung giờ mọi người cùng rảnh | Toàn bộ BE của UC-85a (tìm khung) và UC-85b (tạo nháp từ khung) |
| [SCRUM-142](https://sep490g116.atlassian.net/browse/SCRUM-142) | Chia sẻ lịch làm việc | Đã vào `main` (#315, #318). Note này **chỉ dùng lại** `calendar_shares`, `CalendarBusyReader`, `CalendarShareRules`, `VietnamTime` — không sửa |

Note này viết đủ chi tiết để một người (hoặc một model AI nhỏ) làm theo từng bước mà không phải tự đoán nghiệp vụ. Làm đúng
thứ tự §9. Chỗ nào note và Report 3 khác nhau thì **Report 3 thắng**, trừ các lệch đã ghi ở §12 (đã được người phụ trách chốt).

Nguồn chuẩn, đọc trước khi code:

- Report 3 (bản sinh, không sửa tay): [05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md) mục UC-85a,
  UC-85b và UC-14 AF-1; [06-business-rules.md](../01-requirements/06-business-rules.md) BR-414 → BR-422, BR-427 → BR-430.
- Sequence: [UC85a_FindMeetingTimeSlots.txt](../06-sequencediagram/calendar-sharing/UC85a_FindMeetingTimeSlots.txt),
  [UC85b_CreateDraftMeetingFromSlot.txt](../06-sequencediagram/calendar-sharing/UC85b_CreateDraftMeetingFromSlot.txt).
- Đặc tả sơ bộ: [NOTE-dac-ta-so-bo-scrum-137-142.md](NOTE-dac-ta-so-bo-scrum-137-142.md) (D12, D14, D15, D17; §5 mã; §6 API dự kiến).
- [ADR-0058](../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md) (số eCab, chỉ bận/rảnh, Mật không bao giờ chia sẻ).
- Kế hoạch SCRUM-142: [NOTE-ke-hoach-be-scrum-142.md](NOTE-ke-hoach-be-scrum-142.md) §7 (giờ bận) — phần này đã có code.
- Contract chia sẻ: [23-chia-se-lich.md](../03-api/23-chia-se-lich.md).
- Quy ước: [AGENTS.md](../../AGENTS.md), [DEVELOPMENT_WORKFLOW.md](../DEVELOPMENT_WORKFLOW.md), skill `add-endpoint`,
  [danh mục mã](../03-api/00-danh-muc-ma-thong-bao.md).

## 0. Quyết định đã chốt trước khi code

| # | Quyết định | Ai chốt |
|---|---|---|
| P1 | UC-85b là **endpoint mới** `POST /api/calendar/slots/draft` (khớp "Meeting Slot Controller" trong sequence). Nó kiểm lại khung rồi gọi lại `IMeetingService.CreateAsync` (bước 5–6 của UC-14). **Không sửa** `POST /api/meetings` | Huy, 11/11/2026 |
| P2 | "Khung không còn trống" (MSG446) = **có thêm người** (trong số người được tính) nay bận hoặc chưa biết, so với lúc tìm. FE gửi lại `acceptedBusyUserIds` = người đã hiện bận/chưa biết ở khung đó lúc tìm. Nhờ vậy khung của AF-1 (đã có người bận) vẫn tạo nháp được. Áp cùng `bufferMinutes` như lúc tìm | Huy, 11/11/2026 |
| P3 | Khung **không bao giờ chồng giờ nghỉ trưa** (đúng BR-422). Cho phép tắt nghỉ trưa (`lunchBreak: false`) để tìm cuộc họp dài hơn một buổi | Huy, 11/11/2026 |
| P4 | Ngày ngoài khoảng chia sẻ của một người là **chưa biết ≠ rảnh** (BR-415): khung có người chưa biết **không** là khung chung; chỉ có thể hiện ở AF-1, không tính vào số người rảnh. Người **chưa chia sẻ** (BR-416) đứng ngoài phép tính, được liệt kê riêng | Huy, 11/11/2026 |
| P5 | Trả **tối đa 200 khung**, theo thứ tự thời gian, kèm cờ `truncated` | Huy, 11/11/2026 |
| P6 | Không đổi database: không bảng, không cột, không migration. Không thêm capability, vai trò, loại thông báo hay audit action | BE |
| P7 | Mã: dùng `MSG444`, `MSG445`, `MSG446` (đã dành sẵn), dùng lại `MSG448`, `MSG453` (SCRUM-142) và `MSG29`, `MSG31`, `MSG35`, `MSG277` (UC-14). **Không cấp mã mới** — "Mã tiếp theo" giữ `MSG471` | BE |

## 1. Hiện trạng code có sẵn (dùng lại, không sửa)

| Thành phần | File | Dùng để |
|---|---|---|
| `CalendarBusyRules` (`BusyInterval`, `BusyStatuses`, `IsBusyAssignment`, `Interval`, `Merge`, `Clip`) | `backend/TrustCabinet.Domain/CalendarSharing/CalendarBusyRules.cs` | BR-419, BR-420: ai, trạng thái nào, khoảng nào tính bận |
| `CalendarShareRules.Covers(level, sensitivity)` | `…/CalendarShareRules.cs` | BR-414, BR-417, BR-418 (`level = null` = lịch của chính người tìm, mọi mức) |
| `CalendarShareRules.IsLapsed` | như trên | BR-425 (quá ngày kết thúc) |
| `VietnamTime.Today / At / ToLocal / Monday` | `…/VietnamTime.cs` | "Hôm nay" và giờ theo `Asia/Ho_Chi_Minh` |
| `CalendarBusyReader.ReadAsync(people, from, to)` | `backend/TrustCabinet.Infrastructure/CalendarSharing/CalendarBusyReader.cs` | Một truy vấn lấy khoảng bận đã gộp, đã cắt theo `[from, to)`, đã lọc mốc |
| `IMeetingService.CreateAsync(input, actorId, participants)` | `backend/TrustCabinet.Infrastructure/Meetings/MeetingService.cs` | Tạo cuộc họp Draft + roster (UC-14 bước 5–6), trả `MeetingOutcome` |
| `MeetingResponse`, `MeetingMutationResponse`, `MeetingErrorResponse` | `backend/TrustCabinet.Web/Endpoints/MeetingEndpoints.cs` (public record) | Hình dạng response `201 MSG29` giống hệt `POST /api/meetings` |
| `CapabilityGate.RequireActiveUser`, `GatedUserId` | `backend/TrustCabinet.Web/Endpoints/CapabilityGate.cs` | Gác đăng nhập + tài khoản còn hoạt động |
| `GET /api/meetings/participant-options` | `ParticipantEndpoints.cs` | FE lấy danh sách người để mời (đã gác `MeetingCreator`) — không viết endpoint mới cho việc này |
| `CalendarSharingFixture` | `tests/TrustCabinet.Web.Tests/CalendarSharing/CalendarSharingFixture.cs` | PostgreSQL thật, đồng hồ giả CN 08/11/2026 10:00 VN, `Seed()`, `AddShare()`, `Client()`, `Failures` |

## 2. Quy tắc nghiệp vụ trong code

### 2.1 Tham số tìm (UC-85a bước 1–2, BR-421, BR-422, ADR-0058 §7)

Mọi giờ là giờ Việt Nam, dạng chuỗi `"HH:mm"` (24 giờ). Mọi ngày là `"YYYY-MM-DD"`. Trường bỏ trống (không gửi hoặc `null`)
thì dùng mặc định.

| Trường JSON | Kiểu | Mặc định | Hợp lệ khi | Lỗi (key trong `errors`) |
|---|---|---|---|---|
| `userIds` | `Guid[]` | — (bắt buộc) | 1 → 10 phần tử (D14: tối đa 10 người **ngoài** người tìm), không trùng, không `Guid.Empty`, không chứa chính người tìm, mỗi id là tài khoản **tồn tại và đang hoạt động** | `userIds` (số lượng / trùng / chính mình), `userIds[i]` (không tồn tại hoặc bị vô hiệu hoá) |
| `includeMe` | `bool` | `false` | — | — |
| `durationMinutes` | `int` | `120` | `15 ≤ x ≤ 480` và `x % 5 == 0` | `durationMinutes` |
| `fromDate` | `DateOnly` | hôm nay (VN) | `hôm nay ≤ fromDate ≤ hôm nay + 365` (`MaxDaysAhead`, chặn tràn ở hai đầu lịch) | `fromDate` |
| `toDate` | `DateOnly` | thứ Sáu của tuần chứa `fromDate`; nếu `fromDate` là thứ Bảy/CN thì thứ Sáu tuần sau | `toDate ≥ fromDate` và `toDate - fromDate + 1 ≤ 31` | `toDate` |
| `weekdays` | `int[]` | `[1,2,3,4,5]` | không rỗng, không trùng, mỗi phần tử `1..5` (1 = thứ Hai … 5 = thứ Sáu; BR-422 chỉ thứ 2–6) | `weekdays` |
| `workStart` | `"HH:mm"` | `"08:00"` | parse được | `workStart` |
| `workEnd` | `"HH:mm"` | `"17:00"` | parse được, `workEnd > workStart` | `workEnd` |
| `lunchBreak` | `bool` | `true` | — | — |
| `lunchStart` | `"HH:mm"` | `"12:00"` | (chỉ khi `lunchBreak`) parse được, `workStart ≤ lunchStart` | `lunchStart` |
| `lunchEnd` | `"HH:mm"` | `"13:00"` | (chỉ khi `lunchBreak`) parse được, `lunchStart < lunchEnd ≤ workEnd` | `lunchEnd` |
| `bufferMinutes` | `int` | `30` | `0 ≤ x ≤ 240` và `x % 5 == 0` | `bufferMinutes` |
| `stepMinutes` | `int` | `30` | `5 ≤ x ≤ 120` và `x % 5 == 0` | `stepMinutes` |

Thêm một kiểm chéo (ticket: "giờ kết thúc > giờ bắt đầu + thời lượng"): thời lượng phải **vừa trong ít nhất một buổi làm việc**.
Mỗi buổi tính từ giờ bắt đầu đầu tiên trên lưới bước: `g(x) = ceil(x / step) * step`. Không nghỉ trưa:
`g(workStart) + duration ≤ workEnd`. Có nghỉ trưa: `g(workStart) + duration ≤ lunchStart` **hoặc** `g(lunchEnd) + duration ≤ workEnd`. Sai → `errors["durationMinutes"] = "Thời lượng dài hơn một buổi làm việc. Rút ngắn hoặc bỏ nghỉ trưa."`.
Chỉ kiểm chéo này khi các giờ đã hợp lệ.

Câu lỗi tiếng Việt cho từng key (đặt trong `MeetingSlotRules`, FE hiện cạnh ô tương ứng):

| Key | Câu |
|---|---|
| `userIds` | `Chọn từ 1 đến 10 người, không trùng và không chọn chính mình.` |
| `userIds[i]` | `Người dùng không tồn tại hoặc đã ngừng hoạt động.` |
| `durationMinutes` | `Thời lượng từ 15 phút đến 8 giờ, bội số của 5 phút.` (hoặc câu kiểm chéo ở trên) |
| `fromDate` | `Ngày bắt đầu từ hôm nay đến tối đa 365 ngày sau.` |
| `toDate` | `Khoảng ngày tối đa 31 ngày, ngày kết thúc không trước ngày bắt đầu.` |
| `weekdays` | `Chọn ít nhất một ngày từ thứ Hai đến thứ Sáu.` |
| `workStart` / `workEnd` | `Giờ làm việc không hợp lệ.` |
| `lunchStart` / `lunchEnd` | `Giờ nghỉ trưa phải nằm trong giờ làm việc.` |
| `bufferMinutes` | `Cách cuộc họp khác từ 0 đến 4 giờ, bội số của 5 phút.` |
| `stepMinutes` | `Làm tròn giờ bắt đầu từ 5 phút đến 2 giờ, bội số của 5 phút.` |

Bất kỳ lỗi nào → `400 MSG444` kèm `errors` (một `Dictionary<string,string>`), không tìm gì.

### 2.2 Ai được tính và tính thế nào (BR-414 → BR-420)

- **Người được tính** (counted people):
  - mỗi người trong `userIds` **có chia sẻ hiệu lực** cho người tìm: một dòng `calendar_shares` với `OwnerId = người đó`,
    `ViewerId = người tìm`, `Status = Effective`, `EndDate ≥ hôm nay` (BR-425), và chủ lịch `IsActive` (chủ lịch bị vô hiệu
    hoá mà chưa kịp ghi `Stopped` coi như không có chia sẻ — giống `SharedCalendarService`). Nếu có nhiều dòng (không xảy ra vì
    unique index, nhưng cứ phòng), lấy dòng `CreatedAt` mới nhất. Mốc xem = `share.Level`; cửa sổ biết = `[StartDate, EndDate]`.
  - người tìm, **chỉ khi `includeMe = true`**: mốc `null` (mọi mức, kể cả Mật — BR-418), cửa sổ biết = mọi ngày.
- **Người chưa chia sẻ** (BR-416): người trong `userIds` không có chia sẻ như trên. Không bận, không rảnh, **không tham gia**
  phép tính; trả về trong `people` với `status = "NotShared"`. Không phân biệt "chưa từng xin", "đang chờ", "đã dừng", "bị từ chối"
  — chỉ một trạng thái (không lộ gì thêm).
- Giờ bận: gọi `CalendarBusyReader.ReadAsync(people, from, to)` với `people` = counted people → mốc (`Level` hoặc `null`),
  `from = VietnamTime.At(fromDate, workStart) - buffer`, `to = VietnamTime.At(toDate, workEnd) + buffer`. Reader đã áp BR-419
  (Chủ toạ/Thư ký/Thành viên hiện hành, không từ chối, không uỷ quyền), BR-420 (Chờ duyệt, Đã duyệt, Đã công bố, Đang họp, Đã kết
  thúc đến giờ kết thúc thật; Đang họp ít nhất đến bây giờ), BR-414/417 (mốc; Mật không bao giờ), gộp và cắt.

### 2.3 Sinh khung (BR-415, BR-422, D15)

Hàm thuần `MeetingSlotFinder.Find(search, now, persons)` trong Domain. Giả mã:

```
candidates = []
for d in fromDate..toDate:
    if isoWeekday(d) not in weekdays: continue            // 1 = Monday … 7 = Sunday
    firstMinute = ceil(workStart.minutesOfDay / step) * step   // làm tròn theo bước tính từ 00:00 (D15)
    for m = firstMinute; m + duration <= workEnd.minutesOfDay; m += step:
        if lunchBreak and m < lunchEnd.minutesOfDay and m + duration > lunchStart.minutesOfDay: continue
        start = VietnamTime.At(d, TimeOnly.FromTimeSpan(minutes m))
        end   = start + duration
        if start <= now: continue                          // không đề xuất giờ đã qua (BR-422)
        free = [], busy = [], unknown = []
        for p in persons:                                  // counted people, thứ tự: người tìm trước, rồi theo userIds
            if p.Window is (s, e) and (d < s or d > e): unknown += p.Id          // BR-415
            elif any b in p.Busy where b.Start < end + buffer and b.End > start - buffer: busy += p.Id   // đệm hai phía (D15)
            else: free += p.Id
        candidates += Slot(d, start, end, free.Count, persons.Count, busy, unknown)
```

Ví dụ phải đúng (BR-422): họp kết thúc 09:10, đệm 30, bước 30 → khung sớm nhất 10:00 (09:30 bị loại vì `09:10 > 09:30 - 30`).
Lưu ý: đệm **chỉ** áp với giờ bận, không áp với đầu/cuối giờ làm việc hay giờ nghỉ trưa.

Chọn kết quả:

1. `persons` rỗng (không tính lịch mình và không ai chia sẻ) → không khung nào, `allFree = false`, không mã. (Không coi là
   "mọi người rảnh".) FE chọn câu trạng thái trống theo `people[].status`: trường hợp 3 bên dưới với `max == 0` cũng ra
   `days = []` (vd. người duy nhất có chia sẻ nhưng khoảng chia sẻ không chạm khoảng tìm).
2. Có khung mà `busy` và `unknown` đều rỗng → **khung chung**: lấy đúng các khung đó, `allFree = true`, không mã (UC-85a bước 4).
3. Không có khung chung: `max = max(freeCount)` trên mọi candidate. `max == 0` hoặc không có candidate → rỗng, `allFree = false`,
   không mã. Ngược lại → các khung có `freeCount == max`, `allFree = false`, **`MSG445`** (UC-85a AF-1).
4. Sắp theo `start` tăng dần; giữ tối đa `MaxSlots = 200`; `truncated = true` nếu bị cắt (P5). Gom theo ngày.

### 2.4 Kiểm lại khi tạo nháp (UC-85b bước 2, BR-428, P2)

- Đọc lại chia sẻ và giờ bận như §2.2 cho `userIds` (+ người tìm nếu `includeMe`), khoảng
  `[plannedStart - buffer, plannedEnd + buffer)`.
- Trước khi tính gì trên giờ: giờ bắt đầu phải sau hiện tại và trước 00:00 của ngày `hôm nay + 365 + 31`, kết thúc sau bắt đầu và
  dài tối đa 8 giờ (`MeetingSlotRules.CheckSlotTimes`, `400 MSG31`). So sánh bằng thời điểm, không đổi sang giờ VN (đổi giờ gần năm
  9999 cũng tràn).
- Với mỗi người được tính: `unknown` nếu ngày (VN) của `plannedStart − buffer` **hoặc** của `plannedEnd + buffer − 1 tick` nằm ngoài
  cửa sổ — mọi ngày mà phép kiểm bận đọc tới đều phải "biết" (BR-415; khoảng kết thúc đúng 00:00 thuộc ngày trước). Áp đúng như
  vậy cho tìm khung (§2.3); `busy` nếu có giờ bận
  chồng `[plannedStart - buffer, plannedEnd + buffer)`; còn lại `free`.
- Nếu có người `busy` hoặc `unknown` mà **không** nằm trong `acceptedBusyUserIds` → `409 MSG446`. Câu luôn giống nhau, không kể
  có họp Mật hay không (BR-428 — mà Mật của người được mời vốn không bao giờ được đọc).
- Người chưa chia sẻ: bỏ qua (BR-416). Không kiểm giờ làm việc/nghỉ trưa/bước — chỉ kiểm dữ liệu đã chia sẻ.
- Không có audit, thông báo hay mã riêng nào khác đi vì một họp Mật (BR-428 POST-2).

### 2.5 Tạo nháp (UC-85b bước 3, BR-429)

- Roster: `userIds[0]` → `Chair`; `userIds[1..]` → `Member`; người tìm → `Secretary` (luôn có, kể cả khi `includeMe = false`).
  Thứ tự `userIds` từ FE là thứ tự "người đầu tiên làm Chủ toạ".
- Gọi `IMeetingService.CreateAsync(new MeetingInput(title, plannedStartTime, plannedEndTime, mode, location, onlineLink, sensitivity,
  description), finderId, seeds)`. Service này tự kiểm giá trị (BR-24, BR-25, BR-62, BR-162 → `MSG31`, kể cả giờ bắt đầu đã qua kể
  từ lúc tìm), roster (BR-320 → `MSG277`), lưu trong transaction (lỗi → `MSG35`), tạo ở `Draft`. Không kiểm trùng lịch (BR-429).
- Cuộc họp đi luồng duyệt bình thường (UC-15 …); không có gì khác cuộc họp tạo tay.

## 3. API

Contract chính thức: `docs/03-api/24-tim-gio-hop.md` (viết ở bước Docs). Tóm tắt:

### 3.1 `POST /api/calendar/slots` — UC-85a

Gác: `RequireActiveUser` (lỗi đọc tài khoản → `500 MSG453`); trong service kiểm `MeetingCreator` → `403 MSG448` (đúng sequence:
kiểm quyền **trước** kiểm tham số). Header `Cache-Control: no-store`.

Body ví dụ:

```json
{
  "userIds": ["0193…a1", "0193…b2"],
  "includeMe": true,
  "durationMinutes": 60,
  "fromDate": "2026-11-09",
  "toDate": "2026-11-13",
  "weekdays": [1, 2, 3, 4, 5],
  "workStart": "08:00", "workEnd": "17:00",
  "lunchBreak": true, "lunchStart": "12:00", "lunchEnd": "13:00",
  "bufferMinutes": 30,
  "stepMinutes": 30
}
```

`200` ví dụ (AF-1):

```json
{
  "code": "MSG445",
  "message": "Không có khung giờ nào hợp với tất cả. Đang hiện các khung nhiều người dự được nhất.",
  "allFree": false,
  "truncated": false,
  "people": [
    { "id": "…me", "fullName": "Lê Tổ Chức", "email": "…", "status": "Own", "level": null, "startDate": null, "endDate": null },
    { "id": "…a1", "fullName": "Trần A", "email": "…", "status": "Shared", "level": "Level1", "startDate": "2026-11-08", "endDate": "2026-12-07" },
    { "id": "…b2", "fullName": "Võ B", "email": "…", "status": "NotShared", "level": null, "startDate": null, "endDate": null }
  ],
  "days": [
    { "date": "2026-11-09", "slots": [
      { "start": "2026-11-09T08:00:00+07:00", "end": "2026-11-09T09:00:00+07:00",
        "freeCount": 1, "countedCount": 2, "busyUserIds": ["…a1"], "unknownUserIds": [] }
    ] }
  ]
}
```

- `code`/`message` là `null` khi có khung chung hoặc không có khung nào.
- `people`: người tìm trước (chỉ khi `includeMe`, `status = "Own"`), rồi đúng thứ tự `userIds`. Tên/email là của **người trong
  danh sách mời** (BR-427 cho phép); `level`/`startDate`/`endDate` là của chia sẻ mà người tìm vốn là một bên.
- Không bao giờ có tên, địa điểm, người dự, mức mật hay id của cuộc họp nào (BR-427).
- Lỗi: `400 MSG444` + `errors`; `403 MSG448`; `500 MSG453` (đọc chia sẻ/cuộc họp/người lỗi). JSON hỏng hoặc sai kiểu
  (vd. `"durationMinutes": "abc"`) → `400` không mã (framework), như các endpoint chia sẻ lịch.

### 3.2 `POST /api/calendar/slots/draft` — UC-85b

Gác như 3.1. Body:

```json
{
  "title": "Họp triển khai quý IV",
  "plannedStartTime": "2026-11-10T08:00:00+07:00",
  "plannedEndTime": "2026-11-10T09:00:00+07:00",
  "mode": "InPerson", "location": "Phòng 301", "onlineLink": null, "sensitivity": "Normal", "description": "",
  "userIds": ["…chair", "…member"],
  "includeMe": true,
  "bufferMinutes": 30,
  "acceptedBusyUserIds": ["…member"]
}
```

Thứ tự xử lý (lệch nhẹ so với sequence, xem §12 L2):

1. Không có `MeetingCreator` → `403 MSG448`.
2. Kiểm cấu trúc khung: `userIds` 1–10, không trùng/rỗng/chính mình → lỗi `400 MSG277` + `errors["userIds"]`;
   `plannedStartTime`/`plannedEndTime` parse được (ISO có offset) và end > start, `bufferMinutes` 0–240 bội 5,
   `acceptedBusyUserIds` (mặc định `[]`) là tập con của `userIds` ∪ {người tìm} → lỗi `400 MSG31` + `errors` theo key.
   (Tài khoản không tồn tại/bị vô hiệu hoá để `CreateAsync` báo `MSG277` ở bước 4.)
3. Kiểm lại khung (§2.4): lỗi đọc → `500 MSG453`; không còn trống → `409 MSG446`.
4. `CreateAsync` (§2.5): `201 MSG29` + `Location: /api/meetings/{id}` + body `MeetingMutationResponse` (giống hệt
   `POST /api/meetings`); `400 MSG31` + `errors`; `400 MSG277` + `errors`; `500 MSG35`.

Lỗi dùng `MeetingErrorResponse(code, message, errors)` như `MeetingEndpoints`.

## 4. File cần tạo / sửa

### 4.1 Domain — `backend/TrustCabinet.Domain/CalendarSharing/MeetingSlotContracts.cs` (mới)

```csharp
namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>A meeting-time search as sent (UC-85a step 1); every field optional, checked by MeetingSlotRules.</summary>
public sealed record SlotSearchInput(
    IReadOnlyList<Guid>? UserIds, bool? IncludeMe, int? DurationMinutes, DateOnly? FromDate, DateOnly? ToDate,
    IReadOnlyList<int>? Weekdays, string? WorkStart, string? WorkEnd, bool? LunchBreak, string? LunchStart,
    string? LunchEnd, int? BufferMinutes, int? StepMinutes);

/// <summary>A search after defaults and checks (BR-421, BR-422). Times are Vietnam wall-clock.</summary>
public sealed record SlotSearch(
    IReadOnlyList<Guid> UserIds, bool IncludeMe, int DurationMinutes, DateOnly FromDate, DateOnly ToDate,
    IReadOnlyList<DayOfWeek> Weekdays, TimeOnly WorkStart, TimeOnly WorkEnd, TimeOnly? LunchStart, TimeOnly? LunchEnd,
    int BufferMinutes, int StepMinutes);

/// <summary>The days a person's calendar is known on, both ends included (BR-415).</summary>
public readonly record struct KnownDays(DateOnly Start, DateOnly End) { public bool Contains(DateOnly date) => Start <= date && date <= End; }

/// <summary>One person whose calendar is counted: their busy periods and the days known (null = every day).</summary>
public sealed record SlotPerson(Guid Id, KnownDays? Window, IReadOnlyList<BusyInterval> Busy);

public sealed record FoundSlot(DateOnly Date, DateTimeOffset Start, DateTimeOffset End, int FreeCount, int CountedCount,
    IReadOnlyList<Guid> BusyUserIds, IReadOnlyList<Guid> UnknownUserIds);

/// <summary>What MeetingSlotFinder chose: common slots (AllFree), the best partial ones (MostFree, MSG445), or nothing.</summary>
public enum SlotResultKind { None, AllFree, MostFree }

public sealed record SlotFinding(SlotResultKind Kind, IReadOnlyList<FoundSlot> Slots, bool Truncated);

/// <summary>A person of the search as the finder may see them (BR-416, BR-427). Status: Own, Shared, NotShared.</summary>
public sealed record SlotPersonView(Guid Id, string FullName, string Email, string Status, string? Level,
    DateOnly? StartDate, DateOnly? EndDate);

public sealed record SlotView(DateTimeOffset Start, DateTimeOffset End, int FreeCount, int CountedCount,
    IReadOnlyList<Guid> BusyUserIds, IReadOnlyList<Guid> UnknownUserIds);

public sealed record SlotDayView(DateOnly Date, IReadOnlyList<SlotView> Slots);

public sealed record SlotSearchResult(SlotResultKind Kind, bool Truncated, IReadOnlyList<SlotPersonView> People,
    IReadOnlyList<SlotDayView> Days);

public enum MeetingSlotFailure { None, NotOrganizer, InvalidSearch, ReadFailed, SlotTaken, InvalidSlot, InvalidInvitees }

public sealed record SlotSearchOutcome(MeetingSlotFailure Failure, SlotSearchResult? Result = null,
    IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>UC-85b request: the meeting fields of UC-14 plus the slot to recheck (P2).</summary>
public sealed record SlotDraftInput(
    string? Title, string? PlannedStartTime, string? PlannedEndTime, string? Mode, string? Location, string? OnlineLink,
    string? Sensitivity, string? Description, IReadOnlyList<Guid>? UserIds, bool? IncludeMe, int? BufferMinutes,
    IReadOnlyList<Guid>? AcceptedBusyUserIds);

/// <summary>Failure before the meeting service ran, or the meeting service's own result.</summary>
public sealed record SlotDraftOutcome(MeetingSlotFailure Failure, MeetingResult? Meeting = null,
    IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>SCRUM-137 — UC-85a, UC-85b. The caller id always comes from the validated token.</summary>
public interface IMeetingSlotService
{
    Task<SlotSearchOutcome> FindAsync(Guid actorId, SlotSearchInput input, CancellationToken ct = default);
    Task<SlotDraftOutcome> CreateDraftAsync(Guid actorId, SlotDraftInput input, CancellationToken ct = default);
}
```

`MeetingResult` nằm ở `TrustCabinet.Domain.Meetings` — thêm `using`.

### 4.2 Domain — `backend/TrustCabinet.Domain/CalendarSharing/MeetingSlotRules.cs` (mới, thuần)

- Hằng số: `MaxPeople = 10`, `MinDuration = 15`, `MaxDuration = 480`, `DefaultDuration = 120`, `MaxRangeDays = 31`,
  `MaxBuffer = 240`, `DefaultBuffer = 30`, `MinStep = 5`, `MaxStep = 120`, `DefaultStep = 30`, `Grain = 5`, `MaxSlots = 200`,
  `DefaultWorkStart = 08:00`, `DefaultWorkEnd = 17:00`, `DefaultLunchStart = 12:00`, `DefaultLunchEnd = 13:00`.
- `static bool TryValidate(SlotSearchInput input, Guid finderId, DateOnly today, out SlotSearch? search,
  out IReadOnlyDictionary<string,string> errors)` — đúng bảng §2.1, **trừ** kiểm "tài khoản tồn tại/hoạt động" (cần DB, làm ở service).
- `static DateOnly DefaultToDate(DateOnly from)` — thứ Sáu của tuần chứa `from`; thứ Bảy/CN → thứ Sáu tuần sau.
- `static bool TryParseClock(string? value, out TimeOnly time)` — `TimeOnly.TryParseExact(value, "HH:mm", InvariantCulture, …)`.
- `static string Message(string key)` hoặc các hằng câu lỗi (§2.1).

### 4.3 Domain — `backend/TrustCabinet.Domain/CalendarSharing/MeetingSlotFinder.cs` (mới, thuần)

- `static SlotFinding Find(SlotSearch search, DateTimeOffset now, IReadOnlyList<SlotPerson> persons)` — đúng §2.3.
- Giờ bận của mỗi người được sắp xếp và gộp một lần (`CalendarBusyRules.Merge`), rồi kiểm "có bận gần khung không" bằng binary
  search (`AnyOverlap`): khoảng đầu tiên có `End > from` phải có `Start < to` (sửa theo review, tránh quét tuyến tính).
- `static (IReadOnlyList<Guid> Busy, IReadOnlyList<Guid> Unknown) Check(DateTimeOffset start, DateTimeOffset end, int bufferMinutes,
  IReadOnlyList<SlotPerson> persons)` — dùng chung cho §2.3 và §2.4 (một chỗ tính bận/chưa biết). Ngày "biết" của một khoảng:
  ngày VN của `start` và của `end - 1 tick` đều trong cửa sổ.

### 4.4 Infrastructure — `backend/TrustCabinet.Infrastructure/CalendarSharing/MeetingSlotService.cs` (mới)

```csharp
public sealed class MeetingSlotService(TrustCabinetDbContext db, CalendarBusyReader busy, IMeetingService meetings,
    TimeProvider clock, ILogger<MeetingSlotService> logger) : IMeetingSlotService
```

`FindAsync`:

1. `try { … } catch (Exception e) when (e is DbUpdateException or NpgsqlException or TimeoutException)` → log +
   `ReadFailed` (giống `SharedCalendarService`). **Không** bắt `OperationCanceledException`.
2. Kiểm người tìm: `db.Users.AnyAsync(u => u.Id == actorId && u.IsActive && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId
   && rc.Capability == Capability.MeetingCreator))` → sai thì `NotOrganizer`.
3. `today = VietnamTime.Today(now)`; `MeetingSlotRules.TryValidate` → sai thì `InvalidSearch` + errors.
4. Đọc người mời: `db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(id, FullName, Email, IsActive)`; id nào không có
   hoặc `!IsActive` → `errors[$"userIds[{i}]"]` → `InvalidSearch`. Đọc thêm người tìm (tên/email) nếu `includeMe`.
5. Đọc chia sẻ: `db.CalendarShares.AsNoTracking().Where(s => s.ViewerId == actorId && ids.Contains(s.OwnerId)
   && s.Status == CalendarShareStatus.Effective && s.EndDate >= today && s.Owner.IsActive)`, nhóm theo `OwnerId`, lấy mới nhất.
6. Dựng `people` cho reader (`ownerId → share.Level`, người tìm → `null`), gọi `busy.ReadAsync(people, from, to, ct)` (§2.2).
7. Dựng `SlotPerson` (người tìm trước, rồi theo `userIds`, chỉ người có chia sẻ), gọi `MeetingSlotFinder.Find`.
8. Dựng `SlotSearchResult`: `people` (Own/Shared/NotShared), `days` gom theo `Date`, giờ đổi `VietnamTime.ToLocal` (để JSON
   có `+07:00`).

`CreateDraftAsync`:

1. Cùng `try/catch` cho phần đọc (bước 2–4); **không** bọc lời gọi `meetings.CreateAsync` (service đó tự trả `MeetingOutcome`
   cho lỗi lưu).
2. Kiểm người tìm → `NotOrganizer`.
3. Kiểm cấu trúc (§3.2 bước 2) → `InvalidInvitees` (+ errors, map `MSG277`) hoặc `InvalidSlot` (+ errors, map `MSG31`).
   Parse giờ bằng `DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, …)` và yêu cầu chuỗi có offset
   (cùng cách `MeetingRules` — xem `TryParseTimestamp` trong `MeetingRules.cs`; nếu hàm đó `internal`/`private` thì viết lại y hệt
   trong `MeetingSlotRules`, không đổi `MeetingRules`).
4. Đọc chia sẻ + giờ bận cho khoảng `[start - buffer, end + buffer)` như `FindAsync` bước 5–6 (người không tồn tại/bị vô hiệu
   hoá chỉ đơn giản không có chia sẻ ở bước này; `CreateAsync` sẽ từ chối họ bằng `MSG277`).
5. `MeetingSlotFinder.Check(...)`; ai bận/chưa biết mà không nằm trong `acceptedBusyUserIds` → `SlotTaken`.
6. Dựng seeds (§2.5) và `return new(None, await meetings.CreateAsync(input, actorId, seeds, ct))`.
   Nếu kết quả là `InvalidParticipants`, đổi key lỗi `participants[i].userId` → `userIds[i]` (với `i < userIds.Length`; key
   khác → `userIds`) để FE tô đúng người đã gửi (`InviteeErrors`, sửa theo review).

Tách phần dùng chung của bước đọc (kiểm người tìm, đọc chia sẻ, gọi reader) thành method `private` để hai luồng không lệch nhau.

### 4.5 Infrastructure — đăng ký DI

`CalendarSharingServiceCollectionExtensions.AddTrustCabinetCalendarSharing()`: thêm
`services.AddScoped<IMeetingSlotService, MeetingSlotService>();` và sửa XML doc thành "…và tìm giờ họp (UC-85a, UC-85b)".
`IMeetingService` đã được đăng ký bởi `AddTrustCabinetMeetings()` (kiểm trong `Program.cs`).

### 4.6 Web — `backend/TrustCabinet.Web/Endpoints/MeetingSlotEndpoints.cs` (mới)

- Hằng số ở đầu file: `Msg29Created = "MSG29"`, `Msg31InvalidInformation = "MSG31"`, `Msg35CreateFailed = "MSG35"`,
  `Msg277InvalidParticipants = "MSG277"`, `Msg444InvalidSearch = "MSG444"`, `Msg445MostFree = "MSG445"`,
  `Msg446SlotTaken = "MSG446"`, `Msg448NotOrganizer = "MSG448"`, `Msg453ReadFailed = "MSG453"`. Không dùng literal `"MSGnn"` ở chỗ khác.
- Body record `internal sealed record SearchBody(...)` và `DraftBody(...)` đúng tên trường §3 (camelCase do serializer mặc định).
- `MapMeetingSlotEndpoints(this IEndpointRouteBuilder app)`:
  `app.MapGroup("/api/calendar/slots").WithTags("MeetingSlots").RequireActiveUser(Error(500, Msg453ReadFailed, "Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại."))`;
  `MapPost("", …)` và `MapPost("/draft", …)`.
- Response tìm: `Results.Ok(new { code, message, allFree, truncated, people, days })` — `code`/`message` chỉ có giá trị khi
  `Kind == MostFree`. Đặt `Cache-Control: no-store`.
- Map lỗi: `NotOrganizer → 403 MSG448`; `InvalidSearch → 400 MSG444 + errors`; `ReadFailed → 500 MSG453`;
  `SlotTaken → 409 MSG446 "Khung giờ này không còn trống. Hãy chọn khung khác."`; `InvalidSlot → 400 MSG31 + errors`;
  `InvalidInvitees → 400 MSG277 + errors`. Kết quả `MeetingResult`: `Success → Results.Created($"/api/meetings/{id}",
  new MeetingMutationResponse(Msg29Created, "Tạo cuộc họp thành công.", MeetingResponse.From(meeting)))`;
  `InvalidInformation → 400 MSG31 "Thông tin cuộc họp không hợp lệ."`; `InvalidParticipants → 400 MSG277 "Danh sách thành phần tham dự không hợp lệ."`;
  còn lại `500 MSG35 "Không thể tạo cuộc họp. Vui lòng thử lại."`.
- Lỗi dùng `MeetingErrorResponse` (đã public).

### 4.7 Web — `Program.cs`

Ngay sau `app.MapCalendarSharingEndpoints();` thêm `app.MapMeetingSlotEndpoints();`. Không thêm dòng `AddScoped` nào ở `Program.cs`
(đã đăng ký trong extension §4.5).

## 5. Không làm / không sửa

- Không sửa `POST /api/meetings`, `MeetingService`, `MeetingRules`, `CalendarBusyReader`, `SharedCalendarService`, mọi endpoint chia sẻ.
- Không migration, không đổi `02-erd.md` (không có bảng mới).
- Không thêm capability, audit action, loại thông báo, job.
- Không sửa Report 3 / extract / sequence (việc của BA Final Review; ghi lệch lên Jira, §12).
- Không động FE (người khác làm, xem §11).

## 6. Bảo mật và quyền riêng tư (đối chiếu AGENTS.md)

- Mọi request qua `RequireActiveUser`; `MeetingCreator` kiểm lại ở service mỗi lần (UI không phải ranh giới bảo mật).
- Endpoint cố định trong tài liệu API → thiếu quyền trả `403` (bảng 403/404 AGENTS.md), có mã `MSG448` như `shared-week`.
- Chỉ đọc lịch của người đã chia sẻ **cho chính người gọi** (`ViewerId == actorId`); mốc lọc trong reader; Mật của người khác không
  bao giờ đọc. Response không có trường nào về cuộc họp (BR-427). Test khẳng định JSON không chứa tiêu đề/địa điểm.
- Lỗi `userIds[i]` chỉ nói "không tồn tại hoặc đã ngừng hoạt động" — đúng điều `participant-options` vốn đã lộ.
- `Cache-Control: no-store` cho kết quả tìm (dữ liệu cá nhân, Luật 91/2025/QH15).
- Không lưu kết quả tìm, không log giờ bận.

## 7. Test

Dùng lại `CalendarSharingFixture` (cùng PostgreSQL thật, đồng hồ giả **CN 08/11/2026 10:00 VN**, tuần làm việc kế tiếp bắt đầu
thứ Hai 09/11/2026). Mỗi test gọi `f.Seed()` để có người riêng. Thêm vào fixture một helper `Meeting(...)` dùng chung (đang nằm
private trong `SharedWeekEndpointTests`) — hoặc chép helper đó vào file test mới; **không** sửa test cũ nếu không cần.

| File | Loại | Phủ |
|---|---|---|
| `tests/TrustCabinet.Web.Tests/CalendarSharing/MeetingSlotRulesTests.cs` | unit | Từng dòng bảng §2.1: mặc định (toDate = thứ Sáu; từ thứ Bảy → thứ Sáu tuần sau), biên 15/480/14/481/bội 5, 0/240/241 đệm, 5/120/4/121 bước, 31/32 ngày, fromDate hôm qua, weekdays rỗng/6/7/trùng, giờ sai định dạng, workEnd ≤ workStart, nghỉ trưa ngoài giờ làm, kiểm chéo thời lượng (có/không nghỉ trưa), 0/11 người, trùng, chứa chính mình, `Guid.Empty` |
| `tests/TrustCabinet.Web.Tests/CalendarSharing/MeetingSlotFinderTests.cs` | unit | Ví dụ BR-422 (09:10 + đệm 30 + bước 30 → 10:00); làm tròn theo bước từ 00:00 khi `workStart = 08:10`; không chồng nghỉ trưa; tắt nghỉ trưa thì khung 4 giờ qua trưa có; giờ đã qua bị bỏ (hôm nay 10:00 → khung sớm nhất 10:00 không có, 10:30 có); chỉ thứ đã chọn; ngày ngoài cửa sổ → unknown, không là khung chung; AF-1 chọn đúng max freeCount; max = 0 → rỗng; persons rỗng → rỗng; cắt 200 + `truncated`; `Check` dùng cho recheck |
| `tests/TrustCabinet.Web.Tests/CalendarSharing/MeetingSlotEndpointTests.cs` | tích hợp | `401` không token; `403 MSG448` (người Thành viên); `400 MSG444` + `errors` (vài trường, kể cả `userIds[0]` của người bị vô hiệu hoá); khung chung khi mọi người rảnh (`code` null, `allFree` true); họp Thường của người chia sẻ Mốc 1 làm bận, họp Nội bộ không (Mốc 1) nhưng có (Mốc 2), họp **Mật** không bao giờ; lịch của chính mình chỉ tính khi `includeMe` và tính cả Mật; Draft/Cancelled/người từ chối không bận; người chưa chia sẻ → `NotShared` và không ảnh hưởng khung; chia sẻ `Stopped`/`Pending`/quá ngày → `NotShared`; ngày ngoài khoảng chia sẻ → khung có `unknownUserIds`, AF-1 `200 MSG445`; JSON không chứa tiêu đề/địa điểm/`sensitivity`; `Cache-Control: no-store`; lỗi đọc `calendar_shares` → `500 MSG453` |
| `tests/TrustCabinet.Web.Tests/CalendarSharing/SlotDraftEndpointTests.cs` | tích hợp | `201 MSG29` + roster (người đầu Chair, còn lại Member, người tìm Secretary), trạng thái `Draft`; khung AF-1 với `acceptedBusyUserIds` đúng → `201`; người rảnh nay bận → `409 MSG446`, không tạo cuộc họp; trùng họp **Mật** của người được mời → vẫn `201` (BR-428); `403 MSG448`; `400 MSG277` (rỗng, 11 người, chính mình, người bị vô hiệu hoá qua `CreateAsync`); `400 MSG31` (thiếu tiêu đề, giờ bắt đầu đã qua, `acceptedBusyUserIds` lạ); lỗi đọc → `500 MSG453`; lỗi lưu (`Failures.FailSave`) → `500 MSG35` |

`MessageCodeCatalogueTests` tự đòi: mỗi mã trong file endpoint có dòng catalogue có HTTP đúng và có ít nhất một test chứa chuỗi
`"MSGnn"` — các test trên phải assert `"MSG444"`, `"MSG445"`, `"MSG446"`, `"MSG448"`, `"MSG453"`, `"MSG29"`, `"MSG31"`, `"MSG35"`, `"MSG277"`.

## 8. Tài liệu cập nhật cùng commit code

- `docs/03-api/24-tim-gio-hop.md` (mới) — contract tiếng Việt §3 đầy đủ: quy tắc, tham số + mặc định + lỗi, response, ví dụ.
- `docs/03-api/24-tim-gio-hop-note-cho-fe.md` (mới) — ghi chú cho người làm FE (§11).
- `docs/03-api/README.md` — thêm hai dòng.
- `docs/03-api/00-danh-muc-ma-thong-bao.md`:
  - mục mới "Tìm giờ họp — SCRUM-137 (`/api/calendar/slots`)" với bảng có HTTP: `MSG444` 400, `MSG445` 200, `MSG446` 409,
    `MSG448` 403, `MSG453` 500, `MSG29` 201, `MSG31` 400, `MSG35` 500, `MSG277` 400 (cột endpoint ghi rõ route mới);
  - dòng tham chiếu `MSG444`–`MSG446`: "Dành sẵn" → "Đã triển khai SCRUM-137 (BE)"; `MSG448`, `MSG453` ghi thêm SCRUM-137;
  - câu "Mã tiếp theo": bỏ "`MSG444`–`MSG446` dành sẵn, chưa có code", ghi đã triển khai cho SCRUM-137. Mã tiếp theo vẫn `MSG471`.
- `docs/notes/README.md` — dòng cho note này.
- `docs/06-sequencediagram/calendar-sharing/README.md` — **không sửa** (BA). Lệch ghi Jira.

## 9. Thứ tự làm (checklist)

Mỗi bước `dotnet build TrustCabinet.sln` sạch trước khi sang bước sau.

1. Domain: `MeetingSlotContracts.cs`, `MeetingSlotRules.cs`, `MeetingSlotFinder.cs`.
2. Unit test: `MeetingSlotRulesTests.cs`, `MeetingSlotFinderTests.cs` → chạy `dotnet test --filter "FullyQualifiedName~MeetingSlot"`.
3. Infrastructure: `MeetingSlotService.cs`; đăng ký DI (§4.5).
4. Web: `MeetingSlotEndpoints.cs`; `Program.cs` (§4.7).
5. Integration test: `MeetingSlotEndpointTests.cs`, `SlotDraftEndpointTests.cs`.
6. Docs (§8).
7. Kiểm (§10); chạy agent `authz-reviewer`, `docs-sync-reviewer` và sửa theo kết quả.
8. Comment Jira SCRUM-137 các lệch §12 (skill `jira-deviation-note`). Không chuyển trạng thái. Không commit/push/merge khi chưa
   được đồng ý.

## 10. Kiểm trước khi báo xong

```bash
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
```

Cần PostgreSQL chạy (`docker compose up -d`) và `POSTGRES_PASSWORD` trong `.env` hoặc `TRUSTCABINET_TEST_CONNECTION_STRING`.
Toàn bộ test (không chỉ test mới) phải xanh — để chắc các chức năng khác không bị ảnh hưởng.

## 11. Ghi chú cho FE (người làm màn Tìm giờ họp)

Chi tiết ở `docs/03-api/24-tim-gio-hop-note-cho-fe.md`. Tóm tắt:

- Mục sidebar **"Tìm giờ họp"** chỉ hiện với capability `MeetingCreator` (ticket AC1); server vẫn trả `403 MSG448` nếu mất quyền.
  Màn gồm hai tab (PO D2): **Tìm khung giờ** (mới, API này) và **Lịch được chia sẻ** (UC-85c, `GET /api/calendar/shared-week`,
  đã có ở `/calendar/sharing`). Chọn nơi đặt tab thứ hai là việc của FE (dùng lại component tuần đã có).
- Chọn người: dùng `GET /api/meetings/participant-options` (đã có). Tối đa 10 người, ô "Tính cả tôi" (`includeMe`).
- Form mặc định: 120 phút; hôm nay → thứ Sáu; T2–T6; 08:00–17:00; nghỉ trưa 12:00–13:00 (có công tắc tắt); "Cách cuộc họp khác"
  30 phút (0–4 giờ); "Làm tròn giờ bắt đầu" 30 phút (5 phút–2 giờ). Server cũng tự áp mặc định nếu FE không gửi.
- Luôn hiện câu cố định (BR-428): `Chỉ tính lịch đã được chia sẻ. Người được mời vẫn có thể bận.`
- `400 MSG444` có `errors` theo tên trường (`durationMinutes`, `userIds[2]`…): tô đỏ ô tương ứng.
- Người `status = "NotShared"`: hiện "Chưa được chia sẻ lịch" + nút "Xin xem lịch" mở hộp thoại UC-84a (`POST /api/calendar/shares`).
- `unknownUserIds` = "chưa biết" (ngày ngoài khoảng chia sẻ), **không** vẽ như rảnh. `busyUserIds` = bận. Tên lấy từ `people`.
- `code = "MSG445"` → hiện câu đó phía trên danh sách (không phải lỗi). `truncated = true` → gợi ý thu hẹp khoảng ngày.
- "Tạo nháp": mở hộp thoại tạo cuộc họp, **khoá giờ** theo khung, roster xem trước: người đầu = Chủ toạ, còn lại Thành viên,
  mình = Thư ký (cho đổi thứ tự để chọn Chủ toạ). Gửi `POST /api/calendar/slots/draft` với
  `acceptedBusyUserIds = slot.busyUserIds ∪ slot.unknownUserIds`, cùng `includeMe` và `bufferMinutes` đã tìm.
  `409 MSG446` → đóng hộp thoại, tìm lại; `400 MSG31`/`MSG277` → tô ô theo `errors`; `201 MSG29` → mở chi tiết cuộc họp
  (`meeting.id`), cuộc họp ở Nháp và đi luồng duyệt bình thường.
- Không bao giờ hiện tên/địa điểm cuộc họp của người khác (API không trả).

## 12. Lệch khỏi ticket / tài liệu — ghi lên Jira SCRUM-137

| # | Lệch | Lý do |
|---|---|---|
| L1 | UC-85b là endpoint riêng `POST /api/calendar/slots/draft`, không phải `POST /api/meetings` như §6 đặc tả sơ bộ | Khớp sequence UC-85b (Meeting Slot Controller); không đụng luồng UC-14 đang chạy (P1) |
| L2 | UC-85b kiểm cấu trúc (giờ parse được, `userIds`, đệm) **trước** khi kiểm lại khung; sequence vẽ kiểm lại khung trước rồi mới kiểm giá trị UC-14 | Không parse được giờ thì không kiểm lại khung được. Giá trị UC-14 còn lại (tiêu đề, giờ đã qua…) vẫn kiểm sau, đúng sequence |
| L3 | MSG446 chỉ khi **có thêm** người bận/chưa biết so với lúc tìm (`acceptedBusyUserIds`), để khung AF-1 vẫn tạo nháp được | UC-85b EX-1 "slot no longer fits the shared data" — khung AF-1 vốn đã có người bận (P2) |
| L4 | Ngày ngoài khoảng chia sẻ là "chưa biết": khung có người chưa biết không là khung chung, chỉ hiện ở AF-1 (`unknownUserIds`), không tính rảnh | BR-415 + UC-85a POST-2 (P4) |
| L5 | Thêm `lunchBreak: false` để tắt nghỉ trưa; khung không bao giờ chồng nghỉ trưa | BR-422 + thời lượng tối đa 8 giờ dài hơn một buổi (P3) |
| L6 | Tối đa 200 khung, cờ `truncated` | 31 ngày × bước 5 phút có thể ra > 2.000 khung (P5) |
| L7 | Người tìm không tính lịch mình và không ai chia sẻ → trả rỗng, không `MSG445` | Không có ai để tính thì không thể nói "mọi người rảnh" |
| L8 | Số lẻ: thời lượng/đệm/bước phải là bội 5 phút; `includeMe` mặc định `false`; `fromDate` không trước hôm nay | Đặc tả không nói; chọn để khung thẳng hàng với bước nhỏ nhất (5 phút) |
| L9 | `400 MSG277` cho `userIds` sai ở UC-85b; `400 MSG31` cho giờ/đệm/`acceptedBusyUserIds` sai | Dùng lại mã UC-14 vì UC-85b đi qua bước 5 của UC-14; không cấp mã mới |
| L10 | Ticket mô tả còn ghi "không có ủy quyền xem lịch" và "tab Lịch bận chung để sau" | Đã lỗi thời theo PO D1/D2 (comment 10461); BA sửa mô tả |

## 13. Nhật ký quyết định

| Ngày | Quyết định | Nguồn | Ảnh hưởng code |
|---|---|---|---|
| 08/11/2026 | Q9 (BR-420 trạng thái tính bận) | PO, Jira 10474 | Đã có trong `CalendarBusyRules` |
| 11/11/2026 | P1 → P5 (endpoint riêng, recheck theo `acceptedBusyUserIds`, nghỉ trưa, chưa biết ≠ rảnh, tối đa 200 khung) | Huy (BE), trước khi code | §2, §3 |
| 11/11/2026 | Sửa theo review của đối tác (commit `c4f542ea`): key lỗi `MSG277` của UC-85b đổi từ `participants[i].userId` (roster do server dựng) sang `userIds[i]`; kiểm bận bằng binary search trên giờ bận đã sắp xếp và gộp (trường hợp xấu nhất ~1 giây CPU → O(khung × log giờ bận)); thêm test chia sẻ `Pending`, mốc Level1 + họp Nội bộ khi tạo nháp, chủ lịch chỉ chia sẻ cho người khác, khoảng chia sẻ ngoài khoảng tìm, giờ bận trước `workStart` trong khoảng đệm; contract "kết thúc không quá `workEnd`" | Review đối tác | `MeetingSlotService.InviteeErrors`, `MeetingSlotFinder.AnyOverlap` |
| 12/11/2026 | Sửa theo review PR #319 (callmehai, CHANGES_REQUESTED): (1) ngày "biết" tính trên cả phần đệm `[start − buffer, end + buffer)`, để đệm vắt qua nửa đêm không đọc được giờ bận của ngày ngoài khoảng chia sẻ (BR-415), áp cho cả tìm và kiểm lại; (2) `fromDate ≤ hôm nay + 365`, và UC-85b từ chối giờ bắt đầu đã qua / quá xa / dài quá 8 giờ bằng `MSG31` trước mọi phép tính, nên năm 0001/9999 ra `400` thay vì `500`; thêm: kiểm "vừa một buổi" theo lưới bước, bỏ "SCRUM-137 dùng lại" ở dòng `MSG447`, dùng chung `MeetingRules.TryParseTimestamp` | Review PR #319 | `MeetingSlotFinder.CheckSorted`, `MeetingSlotRules.TryValidate` / `CheckSlotTimes` / `FitsOneBlock` |
