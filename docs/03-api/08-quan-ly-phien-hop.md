# 08 · API quản lý phiên họp

`SCRUM-68` — Feature Quản lý phiên họp: UC-53 xem danh sách phiên, UC-26 tạo, UC-27 cập nhật,
UC-28 hủy phiên. `SCRUM-64` — UC-29 bắt đầu, UC-30 kết thúc phiên.

## Phạm vi và nguyên tắc

- Meeting Session là phiên nghiệp vụ của một Meeting; không liên quan `CallSession`/LiveKit.
- Tạo, sửa, hủy, bắt đầu, kết thúc phiên **không đổi trạng thái Meeting** (BR-60, BR-92) và không ghi
  `meeting_status_history`. Hủy phiên Planned cuối cùng được phép, kể cả khi Meeting
  Published/InProgress (BR-77); kết thúc phiên cuối cũng không tự End Meeting (BR-61). Meeting chỉ
  bắt đầu qua UC-43 và kết thúc qua UC-44.
- Bắt đầu/kết thúc phiên không mở, đóng hay ngắt phòng họp trực tuyến (BR-95): phòng thuộc Meeting.
- UC-53 chỉ trả dữ liệu cấp phiên, **không tải Agenda**. `GET /api/meetings/{id}` (UC-17) không trả phiên.
- Phiên không bị xoá vật lý: hủy là đổi `Planned → Cancelled` và giữ bản ghi (BR-57).

## Quyền truy cập

Không có capability hệ thống nào gắn với các endpoint này; quyền đi theo quan hệ với cuộc họp. Mọi
endpoint yêu cầu đăng nhập và tài khoản còn active (đọc lại từ database mỗi request).

| Bước | Điều kiện | Kết quả khi không đạt |
|---|---|---|
| 1 | Caller đọc được cuộc họp theo BR-29 (Organizer, Approver khi không còn Draft, Thư ký, hoặc participant khi cuộc họp đã Published trở đi) | `404` không body — không lộ sự tồn tại |
| 2 | Chỉ với tạo/sửa/hủy: caller là **Thư ký được phân công** (participant `Secretary`, không ủy quyền, tài khoản active) — BR-54 | `403` + `MSG54`/`MSG55`/`MSG56` |
| 3 | Chỉ với tạo/sửa/hủy: cuộc họp ở `Draft`, `Rejected`, `Approved`, `Published` hoặc `InProgress` — BR-54 | `409` + `MSG54`/`MSG55`/`MSG56` |
| 4 | Chỉ với sửa/hủy: phiên tồn tại, thuộc đúng cuộc họp trong URL và còn `Planned` — BR-57 | `404` khi không có/khác cuộc họp; `409` khi không còn Planned |
| 5 | Chỉ với tạo/sửa: dữ liệu hợp lệ — BR-62, BR-75 | `400` + `MSG53` |

Bắt đầu/kết thúc phiên (UC-29/UC-30) kiểm tra theo thứ tự:

| Bước | Điều kiện | Kết quả khi không đạt |
|---|---|---|
| 1 | Caller đọc được cuộc họp theo BR-29 | `404` không body |
| 2 | Caller là **Chủ tọa được phân công** (participant `Chair`, không ủy quyền, tài khoản active) — BR-63 | `403` + `MSG58`/`MSG59` |
| 3 | Cuộc họp đang `InProgress` — Published thì phải Start Meeting (UC-43) trước | `409` + `MSG58`/`MSG59` |
| 4 | Phiên tồn tại và thuộc đúng cuộc họp trong URL | `404` không body |
| 5 | Bắt đầu: phiên còn `Planned` và cuộc họp còn ít nhất một Thư ký active (BR-58, BR-89). Kết thúc: phiên đang `InProgress` | `409` + `MSG58`/`MSG59` |
| 6 | Chỉ với bắt đầu: không có phiên nào khác của cuộc họp đang `InProgress` — BR-59 | `409` + `MSG57` |

Organizer **không** tự động được lập lịch phiên: chỉ Thư ký được phân công (BR-54). Thứ tự kiểm tra
là quyền → trạng thái → dữ liệu, nên người không có quyền không nhận được chi tiết validation. Ngoại
lệ: body không đọc được (không phải JSON, sai cú pháp) bị trả `400 MSG53` ngay ở endpoint, trước khi
tra cuộc họp — lỗi này không phụ thuộc cuộc họp nên không lộ gì.

## Contract

Định dạng phiên trả về (mọi endpoint):

```json
{
  "id": "0199…",
  "meetingId": "0199…",
  "title": "Phiên sáng",
  "plannedStartsAt": "2026-10-12T02:00:00+00:00",
  "plannedEndsAt": "2026-10-12T04:00:00+00:00",
  "status": "Planned",
  "actualStartedAt": null,
  "actualEndedAt": null
}
```

`status` ∈ `Planned`, `InProgress`, `Completed`, `Cancelled`. Thời gian luôn trả về ở UTC.
`actualStartedAt`/`actualEndedAt` là giờ server lúc bắt đầu/kết thúc phiên (UC-29/UC-30), `null` khi
chưa xảy ra. Phiên đã InProgress/Completed từ trước khi có cột này (database dev đã seed từ trước) vẫn
là `null`; database seed mới lấy giờ dự kiến làm giờ thực tế cho các phiên seed đó.

### GET `/api/meetings/{meetingId}/sessions` — UC-53

`200` với mảng phiên, sắp theo `plannedStartsAt` rồi `id`; gồm cả phiên `Cancelled`. Cuộc họp chưa
có phiên → `[]`. `404` không body khi không đọc được cuộc họp. `500 MSG127` khi không tải được.

### POST `/api/meetings/{meetingId}/sessions` — UC-26

```json
{ "title": "Phiên sáng", "plannedStartsAt": "2026-10-12T09:00+07:00", "plannedEndsAt": "2026-10-12T11:00+07:00" }
```

`201` → `{ "code": "MSG48", "message": "Tạo phiên họp thành công.", "session": { … } }`. Phiên mới
luôn `Planned`.

### PUT `/api/meetings/{meetingId}/sessions/{sessionId}` — UC-27

Cùng body với POST, gửi đủ ba trường. `200` → `MSG49` + `session`. Gửi lại đúng giá trị cũ vẫn trả
`MSG49` nhưng không ghi gì và không gửi thông báo.

### POST `/api/meetings/{meetingId}/sessions/{sessionId}/cancel` — UC-28

Không có body. `200` → `MSG50` + `session` với `status: "Cancelled"`. Hủy là thao tác đổi trạng
thái nên dùng `POST …/cancel`, không dùng `DELETE`.

### POST `/api/meetings/{meetingId}/sessions/{sessionId}/start` — UC-29

Không có body. `200` → `{ "code": "MSG51", "message": "Bắt đầu phiên họp thành công.", "session": { … } }`
với `status: "InProgress"` và `actualStartedAt`. Phiên có thể bắt đầu sớm hay muộn so với giờ dự kiến.

### POST `/api/meetings/{meetingId}/sessions/{sessionId}/end` — UC-30

Không có body. `200` → `MSG52` + `session` với `status: "Completed"` và `actualEndedAt`. Cuộc họp vẫn
`InProgress` kể cả khi đây là phiên cuối; Chủ tọa kết thúc cuộc họp bằng UC-44. Xác nhận trước khi
kết thúc là việc của FE.

## Validation

| Field | Luật | Thông báo trong `errors` |
|---|---|---|
| `title` | Bắt buộc, trim, tối đa 300 ký tự | `Tên phiên họp không được để trống.` / `… không được vượt quá 300 ký tự.` |
| `plannedStartsAt` | ISO 8601 có `Z` hoặc offset (`2026-10-12T09:00+07:00`; giây tùy chọn). Không có offset bị từ chối thay vì đoán múi giờ. Lưu ở UTC, cắt về micro giây như PostgreSQL | `Thời gian bắt đầu dự kiến không hợp lệ.` |
| `plannedStartsAt` | BR-75: cuộc họp Published/InProgress thì phải sau thời điểm hiện tại. Khi sửa, chỉ áp dụng nếu giờ bắt đầu **thay đổi** — đổi tên phiên Planned đã quá giờ vẫn được | `Thời gian bắt đầu dự kiến phải sau thời điểm hiện tại.` |
| `plannedEndsAt` | Cùng định dạng; phải sau `plannedStartsAt` (BR-62) | `Thời gian kết thúc dự kiến không hợp lệ.` / `… phải sau thời gian bắt đầu dự kiến.` |

Body không phải `application/json` hoặc JSON sai cú pháp → `400 MSG53` với message `Nội dung JSON
không hợp lệ.` và không có `errors`. Field lạ bị bỏ qua.

## Mã phản hồi

Chi tiết ở [danh mục mã](00-danh-muc-ma-thong-bao.md).
Tóm tắt: `MSG48`–`MSG52` thành công; `MSG53` dữ liệu; `MSG54`–`MSG56`, `MSG58`, `MSG59` bị từ chối
(`403` sai người, `409` sai trạng thái); `MSG57` đã có phiên khác đang diễn ra; `MSG127` lỗi tải danh
sách; `MSG131` lỗi kiểm tra tài khoản; `MSG138`–`MSG142` lỗi lưu.

## Thông báo (BR-76)

Khi cuộc họp `Published` hoặc `InProgress`, mỗi lần tạo, sửa (có thay đổi) hoặc hủy phiên tạo một
thông báo trong ứng dụng `MeetingSessionChanged` cho mọi participant active, không ủy quyền — trừ chính
người thao tác. Thông báo trỏ về cuộc họp (`targetType: "Meeting"`), title `Lịch cuộc họp thay
đổi`. Chưa gửi email. Trước khi công bố không gửi gì: lời mời lúc Publish đã mang lịch mới nhất.
Bắt đầu/kết thúc phiên không tạo thông báo.

## Persistence và concurrency

Tạo/sửa/hủy/bắt đầu/kết thúc chạy trong transaction và khóa dòng `meetings` (`SELECT … FOR UPDATE`)
trước khi kiểm tra.
Hai thao tác cùng lúc trên một cuộc họp, hoặc một thao tác phiên và một chuyển trạng thái cuộc họp
(gửi duyệt, kết thúc…), vì vậy chạy tuần tự: bên sau thấy trạng thái mới và nhận `409` nếu không còn
hợp lệ. Lưu phiên và thông báo cùng một lần commit; lỗi lưu thì không còn gì.

Hai request bắt đầu hai phiên khác nhau cùng lúc: request sau chờ khóa, thấy phiên kia đã
`InProgress` và nhận `409 MSG57`. Database còn một unique index lọc
(`ix_meeting_sessions_meeting_id_in_progress`, `status = 'InProgress'`) để không đường ghi nào khác
tạo được hai phiên InProgress cho một cuộc họp (BR-59). Hiện mọi đường ghi đều qua khóa nên index không
bao giờ chặn; nếu có, request đó nhận `500 MSG141` chứ không phải `409 MSG57`.

## Lệch so với đặc tả — cần BA xem ở BA Final Review

1. **Thứ tự kiểm tra**: sơ đồ UC-26/27 validate dữ liệu (`MSG53`) trước khi kiểm tra Thư ký/trạng
   thái (`MSG54`/`MSG55`). Code kiểm tra quyền và trạng thái trước để người không có quyền không
   nhận chi tiết validation.
2. **`403` và `409` dùng chung mã**: đặc tả gộp "không còn là Thư ký" và "sai trạng thái" vào một
   EX; BE trả `403` cho trường hợp đầu, `409` cho trường hợp sau, cùng mã `MSG54`/`MSG55`/`MSG56`.
3. **Mã lỗi lưu mới** `MSG138`–`MSG140`: đặc tả UC-26–28 không có nhánh lỗi lưu.
4. **`MSG77` ngừng dùng**: "không được hủy phiên Planned cuối cùng" trái BR-77.
5. **Giờ thực tế** (`actualStartedAt`/`actualEndedAt`) mà sơ đồ UC-53 và class diagram nhắc tới:
   đã thêm ở `SCRUM-64` (xem bên dưới).
6. **BR-75 khi sửa** chỉ áp dụng khi giờ bắt đầu thay đổi (đặc tả: "rescheduled").
7. Class diagram tách `MeetingSessionService` (đọc) và `MeetingService` (ghi); code gộp cả bốn UC vào
   `MeetingSessionService`, dùng `MeetingRepository` để khóa cuộc họp và ghi thông báo.
8. **Đổi tên phiên cũng gửi thông báo** BR-76 (BR chỉ nêu tạo, dời lịch, hủy): đơn giản hơn và
   participant vẫn thấy lịch đổi tên.
9. **Khóa nhắc "sắp bắt đầu"** thêm giờ bắt đầu dự kiến, để phiên bị dời lịch được nhắc lại.

### SCRUM-64 (UC-29/UC-30)

1. **Cuộc họp phải `InProgress` để bắt đầu phiên**: theo ticket SCRUM-64. Đặc tả UC-29 PRE-2 còn ghi
   "Published or InProgress"; ticket ghi rõ Published thì phải Start Meeting (UC-43) trước.
2. **Kết thúc phiên cũng yêu cầu cuộc họp `InProgress`** (ticket, sơ đồ UC-30); đặc tả UC-30 không
   có precondition này.
3. **`403` và `409` dùng chung mã** `MSG58`/`MSG59`, như `MSG54`–`MSG56`: đặc tả gộp "không còn là
   Chủ tọa" vào EX-2 (UC-29) / EX-1 (UC-30).
4. **Mã lỗi lưu mới** `MSG141`, `MSG142`: đặc tả UC-29/30 không có nhánh lỗi lưu.
5. **BR-89 khi bắt đầu phiên**: theo sơ đồ UC-29, cuộc họp phải còn ít nhất một Thư ký active; không
   còn thì `409 MSG58`. Ticket không nhắc điều kiện này.
6. **Thứ tự kiểm tra** khác sơ đồ: sơ đồ đọc phiên trước rồi mới đọc cuộc họp; code khóa và kiểm tra
   cuộc họp (quyền, trạng thái) trước, rồi mới tìm phiên — phiên không có/khác cuộc họp trả `404`.
7. **Nhắc "sắp bắt đầu" chỉ cho phiên `Planned`** (trước đây: mọi phiên chưa hủy), để phiên đã bắt
   đầu sớm không nhận nhắc "sắp bắt đầu".
8. **Unique index BR-59** ở database, ngoài khóa dòng cuộc họp — không có trong đặc tả.
9. Như SCRUM-68, code đặt UC-29/30 trong `MeetingSessionService` chứ không phải `MeetingService`
   như sơ đồ.

## Ghi chú cho FE

- Tab "Phiên họp" gọi `GET /api/meetings/{meetingId}/sessions`; nút thêm/sửa/hủy chỉ để quyết định
  hiển thị — server vẫn kiểm tra lại. Response list/mutation không có cờ "có quyền sửa", FE suy ra
  từ vai trò Thư ký của user trong cuộc họp nếu cần; `403 MSG54/55/56` là bình thường khi vai trò đổi giữa chừng.
- Gửi thời gian kèm offset (`toISOString()` là đủ). Hiển thị `errors` theo field khi `400 MSG53`.
- Sau `409` hãy tải lại danh sách phiên: trạng thái phiên hoặc cuộc họp đã đổi.
- Loại thông báo mới `MeetingSessionChanged` hiện hiển thị bằng icon chuông mặc định
  (`notificationKinds.ts` có fallback); thêm icon riêng nếu muốn.
- UC-29/30: nút **Bắt đầu phiên** chỉ hiện cho Chủ tọa khi cuộc họp `InProgress`, phiên `Planned` và
  danh sách không có phiên nào `InProgress`; **Kết thúc phiên** cho phiên đang `InProgress`, có hộp
  xác nhận. Server vẫn kiểm tra lại; sau `409 MSG57/58/59` hãy tải lại danh sách.
- Response phiên có thêm `actualStartedAt`, `actualEndedAt` (nullable) — cần thêm vào type FE.
