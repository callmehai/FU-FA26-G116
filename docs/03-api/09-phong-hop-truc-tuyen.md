# SCRUM-39 · SCRUM-41 · SCRUM-42 · SCRUM-43 · SCRUM-59 · Phòng họp trực tuyến

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

### Khách (SCRUM-59)

Khách gọi `GET …/call` và `POST …/call/join` bằng **token Khách** của UC-04
([16-truy-cap-khach-uc04.md](16-truy-cap-khach-uc04.md)), cùng đường dẫn với thành viên. Khách vào được khi:

- phiên Khách còn hiệu lực (chưa đăng xuất, chưa hết hạn), lời mời chưa bị thu hồi, đúng cuộc họp của token;
- đã khai tên hiển thị (UC-04 bước 5);
- cuộc họp `InProgress`, hình thức `Online`/`Hybrid` — như thành viên (UC-37). Khi cuộc họp `Published`,
  `GET …/call` trả `NotStarted`; khi `Ended`/`Cancelled` Khách mất toàn bộ quyền với cuộc họp nên nhận `404`;
- **xác nhận đã đọc thông báo ghi âm**: body `{ "recordingNoticeAcknowledged": true }`, thiếu thì `400 MSG380`.
  Chỉ giá trị JSON `true` được tính; `"true"` dạng chuỗi, JSON hỏng hay `Content-Type` khác cũng là `400 MSG380`.
  Thành viên không cần gửi body.

Mỗi lần cấp token cho Khách ghi một dòng `audit_log` `call.guest_admitted` (`object_type = GuestSession`,
`object_id` = phiên, `actor_id` rỗng, `ip_address` của Khách, `details` gồm `meetingId`, `invitationId`, tên tự
khai, `recordingNoticeAcknowledged: true`) — đây là bằng chứng Khách đã thấy và xác nhận thông báo ghi âm, ghi
**trước** khi có token. Không ghi được thì không cấp token (`500 MSG118`). Micro của Khách chỉ được ghi theo câu
(SCRUM-129) khi phiên đó có dòng này ([contract](12-ban-ghi-cuoc-hop.md#khách-scrum-129-adr-0051)).

Khách có quyền trong phòng như thành viên (ADR-0016): micro, camera, chia sẻ màn hình, chat, xem ai đang trong
phòng. Không tắt mic người khác: `POST …/mute` bằng token Khách trả `403`. Chủ toạ tắt mic Khách như thành viên, bằng
danh tính `guest-{guestSessionId}` lấy từ phòng. Khách **không** được ghi vào
`meeting_participants`, không có vai trò chủ toạ/thư ký và không tính vào điều kiện nào của cuộc họp.

Thu hồi lời mời Khách (`DELETE …/guest-invitations/{id}`) hoặc gỡ thành viên đối soát ngay phòng của cuộc họp đó
(`IMeetingParticipantAccessPublisher`): người không còn quyền bị đưa ra khỏi phòng, không chờ job 30 giây. Việc này
chỉ chạy khi phòng có phiên `Active`, và chờ LiveKit tối đa 2 giây — quá thì job đối soát làm nốt, thao tác thu hồi vẫn
thành công. Phiên hết hạn hay Khách đăng xuất thì job đối soát đưa ra trong tối đa 30 giây. Ai vào lại bằng vé
LiveKit cũ còn hạn sau khi mất quyền (Khách hay thành viên vừa bị gỡ) bị webhook `participant_joined` đưa ra ngay.
Mỗi người có tối đa một lượt vào/ra đang mở trong một phiên phòng (unique index
`ux_call_participants_open_user_stint` / `ux_call_participants_open_guest_stint`, lọc `left_at IS NULL`): webhook và
lần đối soát ghi cùng lúc thì lần sau bị bỏ qua.

### Danh tính trong LiveKit

Tên phòng LiveKit là `meeting-{meetingId}`. Danh tính (`identity`) là `userId` với thành viên và
`guest-{guestSessionId}` với Khách — cả hai do server đặt, nên danh tính Khách không bao giờ đọc ra một `userId`.
Mỗi phiên Khách một kết nối (như BR-328): mở cùng phiên ở tab khác thì tab cũ bị ngắt; hai thiết bị đã xác minh
riêng là hai phiên, hai kết nối. Tên hiển thị của Khách là tên **tự khai**. `metadata` của người trong phòng là
`{"meetingRole":"Chair"|"Secretary"|"Member"|"Guest"}` — FE đọc để hiện huy hiệu chủ toạ, và nhãn Khách (tên tự khai).

## REST API

| Method | Path | Kết quả |
|---|---|---|
| `GET` | `/api/meetings/{id}/call` | `200 { meetingId, status, myRole, available, participantCount, retentionDays }` — thành viên hoặc Khách |
| `POST` | `/api/meetings/{id}/call/join` | `200 { serverUrl, token, roomName, identity, expiresAt }` — thành viên hoặc Khách; Khách gửi `{ "recordingNoticeAcknowledged": true }` |
| `POST` | `/api/meetings/{id}/call/participants/{identity}/mute` | `204` — `identity` là danh tính LiveKit của người bị tắt: `userId` hoặc `guest-{guestSessionId}` |
| `POST` | `/api/livekit/webhook` | Chỉ LiveKit gọi, xác thực bằng chữ ký |

Chi tiết từng trường của `GET …/call`:

- **`status`**: `Open`, `NotStarted`, `Ended`, `Cancelled` hoặc `InPersonOnly`. Tính từ trạng thái và
  hình thức cuộc họp; `InPersonOnly` xét trước. `Cancelled` là cuộc họp đã huỷ (SCRUM-99), màn chờ báo `MSG350`;
  như quyền xem cuộc họp, chỉ cuộc họp bị huỷ **sau khi công bố** mới trả `Cancelled`, huỷ trước đó vẫn là `404`.
- **`myRole`**: vai trò của người gọi trong cuộc họp; Khách là `Guest`.
- **`available`**: `false` khi máy chủ chưa cấu hình LiveKit.
- **`participantCount`**: số người đang trong phòng. `null` khi phòng chưa mở hoặc không hỏi được
  LiveKit — trường hợp này không làm hỏng cả response.
- **`retentionDays`**: số ngày giữ những gì ghi được trong phòng (`Recordings:RetentionDays`, mặc định 180). Màn chờ của
  Khách nói thời hạn này trong thông báo ghi âm, vì Khách không đọc được danh sách câu ghi âm.

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
| mọi endpoint (trừ webhook) | `403` | — | Tài khoản bị vô hiệu hoá; `POST …/mute` bằng token Khách |
| mọi endpoint (trừ webhook) | `404` | — | Cuộc họp không tồn tại, hoặc người gọi không được vào phòng — không phân biệt, để không lộ cuộc họp. Với Khách: token của cuộc họp khác, phiên/lời mời hết hiệu lực, chưa khai tên, cuộc họp đã kết thúc hoặc huỷ |
| `GET …/call` | `500` | `MSG283` | Lỗi database |
| `POST …/join` | `409` | `MSG280` | Cuộc họp `InPerson` |
| `POST …/join` | `409` | `MSG117` | Cuộc họp không `InProgress` |
| `POST …/join` | `503` | `MSG281` | Chưa cấu hình LiveKit |
| `POST …/join` | `400` | `MSG380` | Khách chưa xác nhận thông báo ghi âm: thiếu body, `recordingNoticeAcknowledged` khác JSON `true`, hoặc body không đọc được |
| `POST …/join` | `500` | `MSG118` | Lỗi database, hoặc không ghi được bằng chứng xác nhận ghi âm của Khách |
| `POST …/mute` | `403` | `MSG123` | Người gọi được mời nhưng không phải chủ toạ |
| `POST …/mute` | `409` | `MSG117` | Phòng không mở |
| `POST …/mute` | `409` | `MSG282` | Người bị tắt không còn trong phòng, hoặc không có micro |
| `POST …/mute` | `503` | `MSG281` | Chưa cấu hình LiveKit |
| `POST …/mute` | `502` | `MSG124` | LiveKit không phản hồi |
| `POST …/mute` | `500` | `MSG284` | Lỗi database |

Câu `message` của từng mã ở [danh mục](00-danh-muc-ma-thong-bao.md#phòng-họp-trực-tuyến--scrum-39-scrum-41-apimeetingsidcall).

## Phía trình duyệt

Các mã `MSG328`–`MSG343` và `MSG350` **FE tự hiển thị**, API không trả: bảng ở
[danh mục](00-danh-muc-ma-thong-bao.md#phòng-họp-trực-tuyến--scrum-39-scrum-41-apimeetingsidcall). Hành vi dưới
đây không đi qua endpoint nào, nên không có mã HTTP.

- **Rời phòng (UC-38).** Nút Rời phòng hỏi xác nhận rồi ngắt kết nối và về màn chi tiết; không gọi
  API, không đổi trạng thái cuộc họp. Rời từ cửa sổ nổi thì không hỏi, vì cửa sổ chính đang bị che.
  Người đã rời vào lại được trong khi cuộc họp còn `InProgress`.
- **Bị ngắt kết nối.** Lý do do LiveKit trả, FE đổi thành câu và quyết định có cho vào lại không:
  `CLIENT_INITIATED` → `MSG328` (vào lại được); `DUPLICATE_IDENTITY` → `MSG329` (vào
  lại được); `PARTICIPANT_REMOVED` → `MSG330` (không vào lại); `ROOM_DELETED`/`ROOM_CLOSED` →
  `MSG331` (không vào lại); còn lại → `MSG332` (vào lại được). Đang kết nối lại thì dải báo `MSG333`.
- **Một tài khoản một kết nối (BR-328).** LiveKit đóng kết nối cũ khi cùng `identity` vào phòng lần
  nữa; tab cũ hiện `MSG329`.
- **Chat (UC-42, BR-327).** Text stream của LiveKit, tối đa 1.000 ký tự, không đi qua API và không
  lưu; người vào sau không thấy tin trước. Gửi lỗi thì giữ nguyên nội dung và hiện `MSG343`.
- **Thiết bị (UC-39d, BR-331).** Đổi micro/loa/camera bằng `switchActiveDevice`; lựa chọn nằm trong
  `localStorage` (`trustcabinet.call.prefs`) của trình duyệt, không gửi lên server.
- **Chia sẻ màn hình (UC-39c, BR-326).** Khi người khác publish track `SCREEN_SHARE`, webhook tắt
  track của người đang trình bày; FE nhận `LocalTrackUnpublished` và hiện `MSG340`.
- **Cửa sổ nổi (UC-78, BR-332).** Document Picture-in-Picture, dùng lại đúng `Room` đang mở — không
  gọi `join` lần hai và không tạo dòng `call_participants` mới.
- **Vai trò hiển thị (BR-329).** `metadata.meetingRole` chỉ đặt lúc cấp token; FE đọc lại danh sách
  thành phần mỗi lần `participantsChanged` (`roomRoles.ts`) nên đổi Chủ toạ giữa họp (UC-73) có hiệu lực
  ngay trong phòng, không cần vào lại. PO 26/10/2026: không khoá đổi vai trò khi cuộc họp đang diễn ra.
- **Sức chứa (BR-336).** `room.max_participants: 50` trong cấu hình LiveKit (`docker/livekit.dev.yaml`,
  `deploy/livekit.yaml`); người thứ 51 bị LiveKit từ chối khi kết nối, FE chưa có mã riêng cho trường hợp này.
- **Chưa mở khi chưa họp.** Màn chờ khi cuộc họp chưa `InProgress` hiện `MSG117` và tự mở khi nhận
  thông báo `MeetingStarted` (SCRUM-69); đã `Ended` hiện `MSG331`, đã `Cancelled` hiện `MSG350`.

## Webhook và đối soát

LiveKit gửi `POST /api/livekit/webhook`:

- `Content-Type: application/webhook+json`;
- header `Authorization` là JWT trần (không có `Bearer `), ký bằng API key/secret;
- claim `sha256` là SHA-256 của body.

Chữ ký sai hoặc hết hạn → `401`. Chưa cấu hình → `503`. Lỗi database → `500`, để LiveKit gửi lại.

| Sự kiện | Xử lý |
|---|---|
| `participant_joined` | Tạo `call_sessions` `Active` nếu phòng chưa có, mở một dòng `call_participants` (lặp lại không tạo trùng). Khách mở phòng thì `started_by_id` rỗng. Khách vào bằng token cũ sau khi đã mất quyền thì vẫn ghi lượt vào, rồi bị đưa ra ngay |
| `participant_left`, `participant_connection_aborted` | Đặt `left_at` cho dòng đang mở |
| `room_finished` | Phiên `Ended`, đóng mọi dòng đang mở |
| `track_published` nguồn `SCREEN_SHARE` | Tắt phần chia sẻ màn hình của **người khác** trong phòng — mỗi lúc một người trình bày |

Tên phòng không phải `meeting-…` hoặc danh tính không phải `userId`/`guest-{guestSessionId}` thì bỏ qua.

Webhook **không đảm bảo tới nơi**. Job `CallRoomJanitor` chạy mỗi 30 giây và:

- đóng phòng của cuộc họp đã rời `InProgress` (mọi người nhận "phòng đã đóng");
- đưa ra khỏi phòng người không còn được vào — tài khoản bị vô hiệu hoá, bị gỡ khỏi thành phần
  tham dự; Khách có phiên hết hạn, đã đăng xuất hoặc lời mời bị thu hồi. Tắt được bằng
  `LiveKit:EnforceRoster = false`, chỉ dùng khi load test;
- bù các lượt vào/ra có webhook bị mất.

Mỗi cuộc họp chỉ có **một** phiên `Active`: index duy nhất có điều kiện
`ux_call_sessions_active_meeting_room`.

## Dữ liệu cá nhân

Chỉ lưu ai vào/ra phòng và lúc nào. Không lưu âm thanh, hình, chat; không ghi âm phía server
(Report 1 LI-01).

Lượt vào/ra của Khách nằm trong `call_participants` với `guest_session_id` (không có `user_id`); tên, email đi qua
phiên → lời mời, IP và bằng chứng thông báo ghi âm nằm ở dòng audit `call.guest_admitted`. Lượt của Khách xoá theo
phiên (`ON DELETE CASCADE`), tức theo lời mời; audit giữ theo chính sách lưu giữ audit như với người dùng nội bộ.

## Hợp đồng tích hợp FE

- Types: `frontend/src/types/call.ts`. API: `frontend/src/api/calls.ts`. UI:
  `frontend/src/features/calls/room/` (`MeetingRoomPage.tsx`, `RoomView.tsx`, `roomSupport.ts`,
  `pipWindow.ts`).
- Phân nhánh theo `code`, không theo `message`.
- `404` từ `GET …/call` → hiện `NotFound` như cuộc họp không tồn tại.
- Mã `MSG328`–`MSG343`, `MSG350` chỉ có ở FE; đừng chờ backend trả chúng, và đừng
  thêm nhánh `isApiError` cho chúng.
- Khách (SCRUM-59): trang Khách gọi `GET …/call` và `POST …/call/join` bằng token Khách. Màn chờ của Khách phải
  hiện thông báo ghi âm (mục đích, thời hạn lưu) và chỉ gửi `recordingNoticeAcknowledged: true` khi Khách bấm xác
  nhận; `400 MSG380` nghĩa là chưa gửi. Không có nút tắt mic người khác cho Khách. `404` → coi như phiên/lời mời
  đã hết hiệu lực, cùng xử lý với các tab khác của trang Khách.
- Đã làm (SCRUM-127): phòng của Khách ở `/guest/{invitationId}/room` (`features/guest/GuestRoomPage.tsx`), dùng lại
  màn chờ và `RoomView` của thành viên. Trang Khách hiện nút **Vào phòng họp** khi `status = Open`, còn `NotStarted`
  / `InPersonOnly` thì hiện `MSG117` / `MSG280`. Bắt đầu cuộc họp không báo gì cho Khách (chỉ có thông báo cho thành
  viên), nên khi phòng chưa mở FE hỏi lại `GET …/call` mỗi 10 giây và khi quay lại tab. Thời hạn trong thông báo ghi âm
  lấy từ `retentionDays` của `GET …/call`; phòng vừa mở thì trang kiểm lại phiên ngay để huy hiệu trạng thái đổi cùng
  nút. Khách được ghi theo micro từ SCRUM-129 như thành viên (tab Khách gọi `GET`/`PUT …/speech-clips`, phòng hiện
  "Đang ghi lời bạn"), xem [bản ghi cuộc họp](12-ban-ghi-cuoc-hop.md#khách-scrum-129-adr-0051). Bị LiveKit đưa ra
  (`PARTICIPANT_REMOVED`) thì trang Khách kiểm lại phiên ngay: lời mời bị thu hồi → màn "Cuộc họp đã kết thúc hoặc lời
  mời đã bị thu hồi", không vào lại được; phiên hết hạn hoặc Khách đăng xuất trên thiết bị khác (job đối soát đưa ra
  trong khoảng 20–30 giây) → lời mời vẫn còn, nên về màn nhập mã với `MSG378` như UC-04 (xác minh lại thì vào lại
  được). Khách bấm Thoát ở một tab khác của **cùng trình duyệt** thì token trong `localStorage` dùng chung đã bị xoá:
  tab đang trong phòng hiện màn "Bạn đã thoát khỏi cuộc họp" như tab kia, không kèm `MSG378` và không tự gửi mã. Token
  quá hạn nhận `401` không có body; FE vẫn hiện câu của `MSG378` cho trường hợp này. Cuộc họp kết thúc thì hiện
  `MSG331` không kèm nút "Về trang cuộc họp" (trang cuộc họp của Khách cũng đã đóng) và trang Khách kiểm lại phiên ngay,
  chuyển sang màn kết thúc; ở màn chờ (chưa vào phòng) thì lần kiểm lại phiên kế tiếp (tối đa 20 giây) làm việc này.

SCRUM-20: cấp quyền phòng và danh sách cho phép của job đối soát cùng dùng `ParticipantRules.ActiveExpression`. Gỡ rồi mời lại chỉ xét assignment Current mới; dòng lịch sử không tạo quyền hoặc làm truy vấn trả nhiều dòng.

## Tích hợp End Meeting — SCRUM-69

Sau commit End, lifecycle gọi CloseMeetingRoomAsync cho Online/Hybrid; gateway DeleteRoom theo CallRoomNames, kể cả chưa có active CallSession. Provider xác nhận đóng rồi mới kết thúc session/stint còn mở; room không tồn tại được xem đã đóng, không tạo session giả. InPerson/chưa cấu hình LiveKit không chặn lifecycle. Lỗi provider/bookkeeping không đổi MSG111: janitor đối soát chu kỳ 30 giây, gồm orphan room. Không chạy đối soát toàn bộ trong request End. Join ticket mới từ chối Ended. JWT cũ không thu hồi được và LiveKit tự tạo lại room cho người còn giữ nó, nên webhook `participant_joined` kiểm tra lại: nếu Meeting đã rời InProgress (hoặc là InPerson) thì ghi lượt vào đó làm bằng chứng, xóa room và kết thúc session ngay, không chờ đối soát. [Bàn giao và giới hạn](10-scrum69-ban-giao.md).

## Bổ sung sau review — thời gian đóng CallSession

Giờ End của Meeting/history là mốc nghiệp vụ; CallSession dùng giờ server sau khi provider xác nhận DeleteRoom. Không truyền mốc End của Meeting vào CloseMeetingRoomAsync nữa. Repository lấy mốc kết thúc hiệu lực không sớm hơn StartedAt, mọi JoinedAt và LeftAt đã lưu, rồi dùng cùng mốc đó để đóng các stint còn mở; không sửa stint đã đóng. Quy tắc này cũng bảo vệ khi timestamp provider lệch về tương lai hoặc sự kiện đến trễ. Reconciliation lấy giờ sau DeleteRoom cho session cần đóng. Không thay schema hoặc ghi đè ActualEndedAt/history của Meeting.
