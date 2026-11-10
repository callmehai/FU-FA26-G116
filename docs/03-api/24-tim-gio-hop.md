# Tìm giờ họp — contract Backend SCRUM-137

**Jira:** [SCRUM-137](https://sep490g116.atlassian.net/browse/SCRUM-137) · **Ngày:** 11/11/2026 · **Trạng thái:** BE đã có code
**Use case:** UC-85a (tìm khung giờ), UC-85b (tạo nháp từ khung giờ). **BR:** BR-414 → BR-422, BR-427 → BR-430.
**Kế hoạch BE:** [NOTE-ke-hoach-be-scrum-137.md](../notes/NOTE-ke-hoach-be-scrum-137.md) · **Note cho FE:** [24-tim-gio-hop-note-cho-fe.md](24-tim-gio-hop-note-cho-fe.md)
**Dùng dữ liệu của:** [23-chia-se-lich.md](23-chia-se-lich.md) (chia sẻ lịch, SCRUM-142)

## 1. Quy tắc chung

- Cả hai endpoint cần đăng nhập và tài khoản còn hoạt động; thiếu token → `401`. Quyền tạo cuộc họp (`MeetingCreator`, actor
  *Meeting Organizer*) được kiểm lại ở server **trước** mọi tham số → `403 MSG448` (BR-430).
- Chỉ lịch của người **đã chia sẻ cho chính người gọi** được đọc: chia sẻ `Effective`, chưa quá ngày kết thúc (BR-425), chủ lịch
  còn hoạt động. Mỗi người chỉ ở mốc đã chia sẻ: `Level1` = họp Thường, `Level2` = Thường + Nội bộ; họp **Mật** của người khác
  không bao giờ được đọc (BR-414, BR-417).
- Lịch của chính người gọi chỉ tính khi `includeMe = true`, và khi đó tính **mọi mức**, kể cả Mật (BR-416, BR-418).
- Tính bận (BR-419, BR-420): chỉ phân công hiện hành Chủ toạ / Thư ký / Thành viên chưa từ chối (Chủ toạ luôn tính); họp Chờ duyệt,
  Đã duyệt, Đã công bố, Đang họp (ít nhất đến bây giờ), Đã kết thúc (đến giờ kết thúc thật). Nháp, Bị từ chối, Đã huỷ không tính.
- Không bao giờ trả tên, địa điểm, người dự, mức mật hay id của cuộc họp nào (BR-427). Tên/email chỉ của người trong danh sách mời.
- Mọi ngày `YYYY-MM-DD`, mọi giờ theo lịch Việt Nam (`Asia/Ho_Chi_Minh`); thời điểm trả về là ISO 8601 có `+07:00`.
- Lỗi có dạng `{ "code": "MSGnn", "message": "…", "errors": { "<tên trường>": "<câu>" } | null }`.
- Không thêm bảng hay cột nào; không audit, không thông báo riêng cho việc tìm. Kết quả tìm có `Cache-Control: no-store`.

## 2. `POST /api/calendar/slots` — UC-85a

### 2.1 Body

Mọi trường không gửi (hoặc `null`) dùng mặc định.

| Trường | Kiểu | Mặc định | Hợp lệ khi | Key lỗi |
|---|---|---|---|---|
| `userIds` | `Guid[]` | — | 1–10 người **ngoài** người gọi (D14), không trùng, không `Guid.Empty`, không có người gọi; mỗi người tồn tại và đang hoạt động | `userIds`, `userIds[i]` |
| `includeMe` | `bool` | `false` | — | — |
| `durationMinutes` | `int` | `120` | 15–480, bội 5 | `durationMinutes` |
| `fromDate` | `YYYY-MM-DD` | hôm nay | không trước hôm nay | `fromDate` |
| `toDate` | `YYYY-MM-DD` | thứ Sáu của tuần chứa `fromDate` (thứ Bảy/CN → thứ Sáu tuần sau) | không trước `fromDate`, khoảng ≤ 31 ngày tính cả hai đầu | `toDate` |
| `weekdays` | `int[]` | `[1,2,3,4,5]` | không rỗng, không trùng, mỗi số 1–5 (1 = thứ Hai … 5 = thứ Sáu) | `weekdays` |
| `workStart`, `workEnd` | `"HH:mm"` | `"08:00"`, `"17:00"` | đúng định dạng 24 giờ, `workEnd > workStart` | `workStart`, `workEnd` |
| `lunchBreak` | `bool` | `true` | `false` = không có giờ nghỉ trưa | — |
| `lunchStart`, `lunchEnd` | `"HH:mm"` | `"12:00"`, `"13:00"` | chỉ xét khi `lunchBreak`: `workStart ≤ lunchStart < lunchEnd ≤ workEnd` | `lunchStart`, `lunchEnd` |
| `bufferMinutes` | `int` | `30` | 0–240, bội 5 — "Cách cuộc họp khác" | `bufferMinutes` |
| `stepMinutes` | `int` | `30` | 5–120, bội 5 — "Làm tròn giờ bắt đầu" | `stepMinutes` |

Kiểm chéo: thời lượng phải vừa **một buổi** (không nghỉ trưa: cả ngày làm việc; có nghỉ trưa: buổi sáng hoặc buổi chiều) → nếu
không, `errors.durationMinutes = "Thời lượng dài hơn một buổi làm việc. Rút ngắn hoặc bỏ nghỉ trưa."`.

### 2.2 Cách tính khung (BR-415, BR-422, D15)

- Giờ bắt đầu nằm trên lưới `stepMinutes` tính từ 00:00 (bước 30 → :00, :30), trong giờ làm việc, kết thúc trước `workEnd`.
- Khung **không bao giờ chồng giờ nghỉ trưa**. Không đề xuất giờ bắt đầu đã qua. Chỉ các thứ trong `weekdays`.
- Với mỗi người được tính: **chưa biết** nếu ngày nằm ngoài khoảng chia sẻ (BR-415); **bận** nếu có giờ bận cách khung chưa đủ
  `bufferMinutes` ở **trước hoặc sau** (D15); còn lại **rảnh**. Ví dụ: họp xong 09:10, cách 30, bước 30 → khung sớm nhất 10:00.
- Người trong `userIds` không có chia sẻ hiệu lực cho người gọi là **NotShared**: không rảnh, không bận, không tính (BR-416).
- Có khung mà mọi người được tính đều rảnh → trả đúng các khung đó (`allFree = true`).
- Không có → các khung có **số người rảnh cao nhất**, kèm ai bận / ai chưa biết, và `code = MSG445` (UC-85a AF-1). Người chưa biết
  không bao giờ tính là rảnh.
- Không có ai để tính (không `includeMe` và không ai chia sẻ), hoặc không khung nào có người rảnh → `days = []`, không mã.
- Tối đa **200** khung theo thứ tự thời gian; bị cắt thì `truncated = true`.

### 2.3 Response `200`

```json
{
  "code": "MSG445",
  "message": "Không có khung giờ nào hợp với tất cả. Đang hiện các khung nhiều người dự được nhất.",
  "allFree": false,
  "truncated": false,
  "people": [
    { "id": "…", "fullName": "Lê Tổ Chức", "email": "…", "status": "Own", "level": null, "startDate": null, "endDate": null },
    { "id": "…", "fullName": "Trần A", "email": "…", "status": "Shared", "level": "Level1", "startDate": "2026-11-08", "endDate": "2026-12-07" },
    { "id": "…", "fullName": "Võ B", "email": "…", "status": "NotShared", "level": null, "startDate": null, "endDate": null }
  ],
  "days": [
    { "date": "2026-11-09", "slots": [
      { "start": "2026-11-09T08:00:00+07:00", "end": "2026-11-09T09:00:00+07:00",
        "freeCount": 1, "countedCount": 2, "busyUserIds": ["…"], "unknownUserIds": [] }
    ] }
  ]
}
```

- `code`, `message`: `null` trừ trường hợp AF-1.
- `people`: người gọi trước (chỉ khi `includeMe`, `status = "Own"`), rồi đúng thứ tự `userIds`. `status`: `Own` | `Shared` | `NotShared`.
- `countedCount` = số người được tính (Own + Shared); `freeCount = countedCount - |busyUserIds| - |unknownUserIds|`.

### 2.4 Lỗi

| HTTP | Mã | Khi |
|---|---|---|
| 400 | `MSG444` | Tham số sai (bảng 2.1), kèm `errors` (UC-85a EX-1) |
| 400 | — | JSON hỏng hoặc sai kiểu (framework) |
| 401 | — | Không đăng nhập |
| 403 | `MSG448` | Không có quyền tạo cuộc họp (UC-85a EX-2) |
| 500 | `MSG453` | Lỗi database khi đọc tài khoản, chia sẻ hoặc cuộc họp (UC-85a EX-3) |

## 3. `POST /api/calendar/slots/draft` — UC-85b

### 3.1 Body

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

- Các trường cuộc họp giống `POST /api/meetings` ([05-quan-ly-cuoc-hop.md](05-quan-ly-cuoc-hop.md)), trừ `participants`: roster do
  server dựng (BR-429) — `userIds[0]` làm **Chủ toạ**, các người sau làm **Thành viên**, người gọi làm **Thư ký**.
- `includeMe`, `bufferMinutes` (mặc định `false`, `30`): như lúc tìm.
- `acceptedBusyUserIds` (mặc định `[]`): người đã hiện **bận hoặc chưa biết** ở khung này lúc tìm (`busyUserIds ∪ unknownUserIds`).
  Chỉ được gồm người trong `userIds` hoặc người gọi.

### 3.2 Xử lý

1. Không có `MeetingCreator` → `403 MSG448`.
2. `userIds` không 1–10 / trùng / rỗng / có người gọi → `400 MSG277` (`errors.userIds`). Giờ sai định dạng (ISO có offset), kết thúc
   không sau bắt đầu, `bufferMinutes` sai, `acceptedBusyUserIds` có người lạ → `400 MSG31` (`errors` theo tên trường).
3. **Kiểm lại khung** theo đúng dữ liệu đã chia sẻ như lúc tìm (BR-428): ai được tính mà nay **bận hoặc chưa biết** ở
   `[plannedStartTime − bufferMinutes, plannedEndTime + bufferMinutes)` và **không** nằm trong `acceptedBusyUserIds` → `409 MSG446`.
   Người chưa chia sẻ không được kiểm. Họp Mật của người được mời không bao giờ được đọc, nên không bao giờ chặn (BR-428, D12).
4. Tạo cuộc họp qua bước 5–6 của UC-14: `201 MSG29` + `Location: /api/meetings/{id}` + body giống `POST /api/meetings`
   (`{ code, message, meeting }`), cuộc họp ở **Nháp** và đi luồng duyệt bình thường; không kiểm trùng lịch (BR-429).

### 3.3 Lỗi

| HTTP | Mã | Khi |
|---|---|---|
| 400 | `MSG31` | Giá trị cuộc họp sai (thiếu tiêu đề, giờ bắt đầu đã qua kể từ lúc tìm…) hoặc giờ/đệm/`acceptedBusyUserIds` sai, kèm `errors` |
| 400 | `MSG277` | Danh sách người mời sai, hoặc có người không tồn tại / bị vô hiệu hoá, kèm `errors` |
| 401 | — | Không đăng nhập |
| 403 | `MSG448` | Không có quyền tạo cuộc họp (UC-85b EX-2) |
| 409 | `MSG446` | Khung không còn trống (UC-85b EX-1) — câu giống nhau dù có họp không được tiết lộ hay không |
| 500 | `MSG453` | Lỗi database khi kiểm lại khung (UC-85b EX-3) |
| 500 | `MSG35` | Không lưu được cuộc họp (UC-85b EX-4) |

## 4. Khác đặc tả sơ bộ / sequence (đã ghi Jira SCRUM-137)

Xem [kế hoạch BE §12](../notes/NOTE-ke-hoach-be-scrum-137.md#12-lệch-khỏi-ticket--tài-liệu--ghi-lên-jira-scrum-137): endpoint
riêng cho UC-85b, kiểm cấu trúc trước khi kiểm lại khung, `acceptedBusyUserIds`, ngày chưa biết, `lunchBreak`, tối đa 200 khung,
bội 5 phút, mã `MSG31`/`MSG277` cho UC-85b.
