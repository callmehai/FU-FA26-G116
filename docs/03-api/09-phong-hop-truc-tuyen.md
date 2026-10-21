# SCRUM-39 · SCRUM-41 · Phòng họp trực tuyến

Phòng họp chạy trên LiveKit tự host ([ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md)).
Âm thanh, hình và signalling (SDP/ICE) đi thẳng giữa trình duyệt và LiveKit. Backend chỉ quyết
định **ai được vào phòng nào**:

- ký token LiveKit sau khi kiểm quyền;
- tắt mic hộ chủ toạ qua API server của LiveKit;
- ghi ai vào/ra phòng từ webhook, đối soát mỗi 30 giây.

Phòng họp **không có hub SignalR**.

## Ai được vào phòng

Tất cả điều kiện sau phải đúng:

- đã đăng nhập, tài khoản đang hoạt động;
- có assignment nội bộ `Current` trong `meeting_participants` của cuộc họp (không dùng dòng `Removed` hoặc role Guest), chưa uỷ quyền cho ai (`response` khác `Delegated` và `delegated_to_user_id` rỗng) — cùng quy tắc "người dự hiện tại" với agenda;
- cuộc họp `InProgress`;
- hình thức `Online` hoặc `Hybrid`.

Người tạo hay người duyệt cuộc họp mà **không** có tên trong thành phần tham dự thì không vào được.

Tên phòng LiveKit là `meeting-{meetingId}`, danh tính (`identity`) là `userId`, cả hai do server
đặt. `metadata` của người trong phòng là `{"meetingRole":"Chair"|"Secretary"|"Member"|"Guest"}` —
FE đọc để hiện huy hiệu chủ toạ.

## REST API

| Method | Path | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/call` | `200 { meetingId, status, myRole, available, participantCount }` |
| `POST` | `/api/meetings/{id}/call/join` | `200 { serverUrl, token, roomName, identity, expiresAt }` |
| `POST` | `/api/meetings/{id}/call/participants/{userId}/mute` | `204` |
| `POST` | `/api/livekit/webhook` | Chỉ LiveKit gọi, xác thực bằng chữ ký |

Chi tiết từng trường của `GET …/call`:

- **`status`**: `Open`, `NotStarted`, `Ended` hoặc `InPersonOnly`. Tính từ trạng thái và hình thức
  cuộc họp; `InPersonOnly` xét trước.
- **`myRole`**: vai trò của người gọi trong cuộc họp.
- **`available`**: `false` khi máy chủ chưa cấu hình LiveKit.
- **`participantCount`**: số người đang trong phòng. `null` khi phòng chưa mở hoặc không hỏi được
  LiveKit — trường hợp này không làm hỏng cả response.

Chi tiết của `POST …/call/join`:

- **`serverUrl`**: địa chỉ LiveKit cho trình duyệt (`ws://`/`wss://`), lấy từ cấu hình
  `LiveKit:PublicUrl`. FE dùng đúng giá trị này, **không** hardcode.
- **`token`**: sống 10 phút (`LiveKit:TokenTtlMinutes`). Chỉ dùng để kết nối lần đầu; LiveKit tự
  cấp token mới cho client đang kết nối. Vì vậy FE lấy token **lúc bấm "Vào phòng"**, không lấy
  sẵn khi mở trang.

Tắt mic chỉ **tắt**, không bao giờ bật hộ. Người đã tắt sẵn thì vẫn trả `204`.

### Lỗi

| Endpoint | HTTP | Mã | Khi nào |
|---|---|---|---|
| mọi endpoint (trừ webhook) | `401` | — | Chưa đăng nhập |
| mọi endpoint (trừ webhook) | `403` | — | Tài khoản bị vô hiệu hoá |
| mọi endpoint (trừ webhook) | `404` | — | Cuộc họp không tồn tại, hoặc người gọi không được vào phòng — không phân biệt, để không lộ cuộc họp |
| `GET …/call` | `500` | `MSG283` | Lỗi database |
| `POST …/join` | `409` | `MSG280` | Cuộc họp `InPerson` |
| `POST …/join` | `409` | `MSG117` | Cuộc họp không `InProgress` |
| `POST …/join` | `503` | `MSG281` | Chưa cấu hình LiveKit |
| `POST …/join` | `500` | `MSG118` | Lỗi database |
| `POST …/mute` | `403` | `MSG123` | Người gọi được mời nhưng không phải chủ toạ |
| `POST …/mute` | `409` | `MSG117` | Phòng không mở |
| `POST …/mute` | `409` | `MSG282` | Người bị tắt không còn trong phòng, hoặc không có micro |
| `POST …/mute` | `503` | `MSG281` | Chưa cấu hình LiveKit |
| `POST …/mute` | `502` | `MSG124` | LiveKit không phản hồi |
| `POST …/mute` | `500` | `MSG284` | Lỗi database |

Câu `message` của từng mã ở [danh mục](00-danh-muc-ma-thong-bao.md#phòng-họp-trực-tuyến--scrum-39-scrum-41-apimeetingsidcall).

## Webhook và đối soát

LiveKit gửi `POST /api/livekit/webhook`:

- `Content-Type: application/webhook+json`;
- header `Authorization` là JWT trần (không có `Bearer `), ký bằng API key/secret;
- claim `sha256` là SHA-256 của body.

Chữ ký sai hoặc hết hạn → `401`. Chưa cấu hình → `503`. Lỗi database → `500`, để LiveKit gửi lại.

| Sự kiện | Xử lý |
|---|---|
| `participant_joined` | Tạo `call_sessions` `Active` nếu phòng chưa có, mở một dòng `call_participants` (lặp lại không tạo trùng) |
| `participant_left`, `participant_connection_aborted` | Đặt `left_at` cho dòng đang mở |
| `room_finished` | Phiên `Ended`, đóng mọi dòng đang mở |
| `track_published` nguồn `SCREEN_SHARE` | Tắt phần chia sẻ màn hình của **người khác** trong phòng — mỗi lúc một người trình bày |

Tên phòng không phải `meeting-…` hoặc danh tính không phải `userId` thì bỏ qua.

Webhook **không đảm bảo tới nơi**. Job `CallRoomJanitor` chạy mỗi 30 giây và:

- đóng phòng của cuộc họp đã rời `InProgress` (mọi người nhận "phòng đã đóng");
- đưa ra khỏi phòng người không còn được vào — tài khoản bị vô hiệu hoá, bị gỡ khỏi thành phần
  tham dự. Tắt được bằng `LiveKit:EnforceRoster = false`, chỉ dùng khi load test;
- bù các lượt vào/ra có webhook bị mất.

Mỗi cuộc họp chỉ có **một** phiên `Active`: index duy nhất có điều kiện
`ux_call_sessions_active_meeting_room`.

## Dữ liệu cá nhân

Chỉ lưu ai vào/ra phòng và lúc nào. Không lưu âm thanh, hình, chat; không ghi âm phía server
(Report 1 LI-01).

## Hợp đồng tích hợp FE

- Types: `frontend/src/types/call.ts`. API: `frontend/src/api/calls.ts`.
- Phân nhánh theo `code`, không theo `message`.
- `404` từ `GET …/call` → hiện `NotFound` như cuộc họp không tồn tại.

SCRUM-20: cấp quyền phòng và danh sách cho phép của job đối soát cùng dùng `ParticipantRules.ActiveExpression`. Gỡ rồi mời lại chỉ xét assignment Current mới; dòng lịch sử không tạo quyền hoặc làm truy vấn trả nhiều dòng.
