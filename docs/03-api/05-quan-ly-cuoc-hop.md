# 05 · API quản lý cuộc họp

> **Một cuộc họp là một buổi họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).**
> Từ migration `MeetingOwnsScheduleDropSessions`, lịch nằm trên Meeting (`plannedStartTime`, `plannedEndTime`,
> `actualStartedAt`, `actualEndedAt`, `onlineLink`); bảng `meeting_sessions` đã bị drop, vòng đời dừng ở `Ended`.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng ([danh mục BR](../01-requirements/06-business-rules.md)).

**UC-14 Create Meeting / UC-15 Update Meeting — SCRUM-17; UC-16 View Meeting Details /
UC-17 View Meeting List — SCRUM-18; UC-18 Submit / UC-19 Review / UC-20 Publish — SCRUM-19.**

## Phạm vi

Các endpoint cho người tạo khởi tạo cuộc họp nháp, sửa thông tin trước khi gửi duyệt và dời lịch
sau khi công bố. Thông tin cuộc họp gồm tên, lịch dự kiến (BR-25), hình thức, địa điểm, link trực
tuyến, mức độ mật và mô tả. Tạo/sửa Meeting không chọn hoặc gán người duyệt; việc chọn và gán người
duyệt diễn ra trong UC-18. Cập nhật Meeting không làm thay đổi trạng thái vòng đời (BR-28).
Người có quan hệ hợp lệ với cuộc họp có thể đọc chi tiết và lấy trang danh sách. `GET /api/meetings`
được bổ sung trong SCRUM-18. Màn danh sách, tạo, sửa và chi tiết đều gọi API thật: tạo xong thì báo
thành công và mở trang chi tiết của cuộc họp mới. FE đã bỏ dropdown người duyệt khỏi form tạo/sửa;
gửi duyệt, duyệt, từ chối và lịch sử trạng thái nằm trên trang chi tiết — xem
[Hợp đồng tích hợp FE](#hợp-đồng-tích-hợp-fe).

Tạo cuộc họp yêu cầu tên và lịch dự kiến (BR-24, BR-25): giờ bắt đầu ở tương lai (BR-162), giờ kết
thúc sau giờ bắt đầu (BR-62). Form khởi tạo Hình thức là `InPerson` và Mức độ mật là `Normal`.
Trường `captureMode` (`PerMic` mặc định, hoặc `Room`) chọn cách ghi âm — xem
[12 · Ghi âm theo micro từng người](12-ban-ghi-cuoc-hop.md#ghi-âm-theo-micro-từng-người-scrum-109). Bỏ trống khi
sửa thì giữ nguyên; cuộc họp đã công bố đổi ở tab Bản ghi, không qua form dời lịch.

## Quyền truy cập

POST, `GET /api/meetings/approvers` và PUT yêu cầu capability `MeetingCreator`; cổng đọc claim và
xác minh lại capability từ database theo SCRUM-12, không kiểm theo tên vai trò. GET chi tiết yêu cầu
đăng nhập và tài khoản còn hoạt động, sau đó kiểm tra quan hệ cuộc họp theo BR-29.

Theo PO 20/10/2026, current Chair và Secretary đọc mọi trạng thái. Current Member đọc từ Published; Guest dùng invitation/session hợp lệ. Creator đọc mọi trạng thái; current approver đọc từ WaitingApproval trở đi. Quyền đọc không phụ thuộc Accepted/Declined. Không đủ quyền đọc trả 404 rỗng. PUT vẫn yêu cầu Organizer + MeetingCreator; quản lý roster chỉ theo Chair/Secretary active, không theo Organizer/admin.

GET danh sách dùng đúng quy tắc đọc trên **trong truy vấn database**, trước `COUNT`, sắp xếp và phân
trang. Endpoint chỉ yêu cầu đăng nhập và tài khoản còn hoạt động; không đòi capability
`MeetingCreator`. `totalCount` không bao gồm cuộc họp caller không được xem.

Organizer được sửa thông tin khi meeting ở `Draft` hoặc `Rejected`. PUT ở `Rejected` giữ nguyên
trạng thái, current Assigned Approver và rejection history/reason; không tạo lifecycle history,
notification hoặc invitation. Ở `Published`, cùng PUT là **dời lịch** (UC-15 AF-1, BR-158): chỉ
đổi được thời gian, địa điểm, link. Hệ thống đọc trạng thái mới nhất sau khi khóa dòng và trước khi
validate nội dung. Trạng thái khác `Draft`/`Rejected`/`Published` trả `409 MSG33`.

### Authorization matrix cho SCRUM-19 và SCRUM-69

| Hành vi | Quan hệ/capability bắt buộc | Chair có tự được phép không? |
|---|---|---|
| SubmitForApproval | Meeting Organizer (`CreatedById`) + `MeetingCreator`; meeting Draft hoặc Rejected và ready | Không |
| ReturnToDraft | Meeting Organizer active + `MeetingCreator`; Rejected hoặc WaitingApproval có assignment không hợp lệ (thiếu người duyệt, trỏ về Organizer, inactive hoặc mất `MeetingApprover`) | Không |
| Approve/Reject | Current `AssignedApproverId` + capability `MeetingApprover`, khác Organizer; meeting WaitingApproval | Không; chỉ được nếu Chair đồng thời là current Assigned Approver và không phải Organizer |
| Publish | Meeting Organizer + `MeetingCreator`; meeting Approved; đúng một Chair active và ít nhất một Secretary active. Invitation response không ảnh hưởng readiness | Không; chỉ được nếu Chair đồng thời là Organizer |
| TransferOrganizer | Actor active có `AdminUsers`; meeting Approved và Organizer hiện tại inactive; Organizer mới active có `MeetingCreator`, khác người cũ và current Assigned Approver | Không |
| Start | Active current Chair; Published; đúng một active current Chair và ít nhất một active current Secretary | Có; không cần system capability |
| End | Active current Chair; InProgress; không gate Secretary/Vote/attendance/agenda Pending | Có; không cần system capability |
| Đọc full lifecycle history/rejection reason | Meeting Organizer hoặc current Assigned Approver | Chair được đọc rejection reason; organizer-transfer metadata vẫn redacted |
| Đọc history/rejection reason của Chair/Secretary | Participant current của chính meeting có role Chair hoặc Secretary, user active, chưa ủy quyền (`Response != Delegated`, `DelegatedToUserId = null`) | Có; không xem snapshot Organizer cũ/mới nếu không đồng thời có full-history permission |
| Đọc timeline redacted | Member từ Published; mọi quan hệ đọc hợp lệ khác không có full-history permission | Không có rejection reason hoặc organizer-transfer metadata |

Người từng là Assigned Approver không giữ quyền đọc history chỉ vì assignment cũ xuất hiện trong
một history row. Authorization dùng assignment hiện tại; snapshot cũ là bằng chứng kiểm toán,
không phải access grant. Organizer/current Assigned Approver được đọc full history ở mọi lifecycle
status (Assigned Approver không được đọc Draft). Chair/Secretary current active đọc history/lý do từ chối trước Published theo PO 20/10/2026; không tự có quyền thấy transfer IDs.
Member thường có thể đọc timeline từ Published theo meeting read policy; rejection reason
được ẩn với họ, trừ khi đồng thời là Organizer hoặc current Assigned Approver. Former approver không
có quyền chỉ vì assignment cũ, nhưng nếu họ là participant thì vẫn nhận timeline đã redacted sau
Publish. Người không đủ quyền đọc meeting nhận `404` body rỗng như một meeting không tồn tại.

## Contract

### GET `/api/meetings`

Các tham số query đều tùy chọn. Giá trị rỗng tương đương không gửi; mỗi tham số chỉ được gửi một
lần. Tất cả bộ lọc được kết hợp bằng AND (BR-30/BR-31).

| Tham số | Quy tắc |
|---|---|
| `status` | Tên enum PascalCase trong bảy `MeetingStatus` (`Draft` … `Ended`); `MinutesIssued`, `Archived` đã bỏ và trả `400 MSG32` |
| `sensitivity` | `Normal`, `Internal`, `Confidential` hoặc `Secret` |
| `q` | Trim; tìm chuỗi con của `title` không phân biệt hoa/thường. `%`, `_`, `\` là ký tự thường, không phải wildcard |
| `from`, `to` | ISO 8601 có `Z` hoặc offset; giây tùy chọn, ví dụ `2026-10-01T09:00+07:00`; chuẩn hóa UTC. Meeting khớp khi lịch dự kiến của chính nó chồng lấp khoảng lọc (BR-30): `from` dùng `plannedEndTime >= from`, `to` dùng `plannedStartTime <= to` |
| `page`, `pageSize` | Mặc định `1`, `20`; `page` từ `1` đến `2.147.483.647`, `pageSize` từ `1` đến `100` |
| `sortBy` | `title`, `plannedStartTime`, `createdAt`; mặc định `plannedStartTime` |
| `sortDir` | `asc`, `desc`; mặc định `asc` với title, `desc` với hai cột thời gian |
| `excludeDeclined` | `true` hoặc `false` (mặc định). `true` bỏ các cuộc họp người gọi đã trả lời **Không tham dự** — lịch làm việc dùng tham số này, giống AI-eCabinet. Màn danh sách cuộc họp không gửi, nên vẫn thấy cuộc họp đã từ chối. Giá trị khác trả `400 MSG32` |
| `timing` | `upcoming` (giờ bắt đầu dự kiến sau thời điểm server nhận request) hoặc `past` (bắt đầu từ thời điểm đó trở về trước, nên cuộc họp đang diễn ra tính là `past`), như bộ lọc “Thời điểm” của AI-eCabinet. Không gửi thì không lọc. Màn danh sách dùng cho “Chưa diễn ra” / “Đã diễn ra”; “Hôm nay” và “Tuần này” vẫn gửi `from`/`to` theo múi giờ người xem. Giá trị khác trả `400 MSG32` |

Khi nhiều dòng có cùng giá trị sort, xếp tiếp theo `id asc`. Ví dụ:
`GET /api/meetings?status=Published&sensitivity=Internal&q=giao&from=2026-10-01T00%3A00%3A00Z&page=2&pageSize=20&sortBy=createdAt&sortDir=desc`.
Đổi trang phải gửi lại bộ lọc và sort hiện tại (BR-32). FE giữ toàn bộ tiêu chí trên URL của màn
danh sách (`/mgmt/meetings?q=…&status=…&page=…`), nên tải lại trang, chia sẻ đường dẫn hay quay lại từ
trang chi tiết đều giữ nguyên bộ lọc và trang đang xem.

Khi gửi timezone có dấu `+` trong query, mã hóa dấu này thành `%2B`, ví dụ
`from=2026-10-01T09%3A00%2B07%3A00`. Phần giây có thể bỏ qua; nếu có giây, phần thập phân
tối đa bảy chữ số. Timestamp thiếu timezone hoặc `sortBy=startsAt` (tên cũ trước SCRUM-70) trả
`400 MSG32`.

`200 OK` trả:

```json
{
  "items": [
    {
      "id": "dc36f21a-c030-4ddb-a6ec-9d1254230001",
      "title": "Giao ban tuần",
      "plannedStartTime": "2026-10-15T01:30:00+00:00",
      "plannedEndTime": "2026-10-15T02:30:00+00:00",
      "mode": "Hybrid",
      "location": "Phòng họp A",
      "createdAt": "2026-09-25T02:00:00+00:00",
      "status": "Published",
      "sensitivity": "Internal",
      "myResponse": null,
      "canRespond": true
    }
  ],
  "page": 2,
  "pageSize": 20,
  "totalCount": 21,
  "totalPages": 2
}
```

Không có kết quả hoặc trang vượt quá trang cuối, kể cả khi offset lớn hơn giới hạn `int`, vẫn trả
`200` với `items: []` (BR-12). Khi không có
kết quả, `totalCount` và `totalPages` đều bằng `0`. Query sai trả `400` với `code` là
`MSG32`, `message` là `Tiêu chí danh sách cuộc họp không hợp lệ.` theo catalogue. `errors` liệt kê
theo tên tham số.
Bao gồm enum/sort lạ, thời gian thiếu offset, `from > to`, trang hoặc kích thước trang ngoài
giới hạn. Lỗi truy xuất database, kể cả ở bước kiểm tra tài khoản còn hoạt động, trả `500` với
`code: MSG38` và thông điệp
`Không thể tải danh sách cuộc họp. Vui lòng thử lại.` như EX-1 của UC-17.

Sequence UC-17 hiện ghi BR-29 và BR-32 ở bước repository; BA cần bổ sung BR-30/31 và nhánh query
sai `400` vào diagram/spec. Đây là nhánh validation của HTTP, không thay đổi hai nhánh truy xuất
thành công/thất bại trong diagram hiện tại.

### GET `/api/meetings/approvers`

Trả danh sách tài khoản đang hoạt động có capability `MeetingApprover`, loại người đang gọi API,
chỉ gồm `id` và `fullName`; endpoint phục vụ bước chọn người duyệt của UC-18 và kế thừa gate
`MeetingCreator`. Danh sách sắp xếp theo tên. Nếu hệ thống không đọc được danh sách do lỗi database
hoặc timeout, trả `503 Service Unavailable` với `{ "code": "MSG130", "message":
"Không thể tải danh sách người duyệt. Vui lòng thử lại." }`.

### POST `/api/meetings`

Request body tạo họp; không có người duyệt. PUT dùng các trường thông tin tương tự nhưng không nhận participants. Thời gian là ISO 8601 **có** `Z` hoặc offset
và có giây; backend chuẩn hóa về UTC:

```json
{
  "title": "Giao ban tuần",
  "plannedStartTime": "2026-10-15T08:30:00+07:00",
  "plannedEndTime": "2026-10-15T09:30:00+07:00",
  "mode": "Hybrid",
  "location": "Phòng họp A",
  "onlineLink": null,
  "sensitivity": "Internal",
  "description": "Thảo luận kế hoạch tuần.",
  "participants": [{"userId":"11111111-1111-4111-8111-111111111111","role":"Chair"}]
}
```

`title`, `plannedStartTime`, `plannedEndTime` là bắt buộc theo BR-24/BR-25. Khi tạo, giờ bắt đầu phải
sau thời điểm hiện tại (BR-162). `onlineLink` tùy chọn, tối đa 1.000 ký tự; rỗng hoặc chỉ khoảng trắng
lưu `null`, nghĩa là dùng phòng họp trực tuyến TrustCabinet (BR-163). Người duyệt được chọn và gán trong UC-18 trước khi chuyển meeting sang
`WaitingApproval`. Nếu `mode` hoặc
`sensitivity` bị bỏ qua, service dùng giá trị khởi tạo của form (`InPerson`, `Normal`).

Theo PO 20/10/2026, `participants` bắt buộc 1..100 phần tử `{userId,role}`: GUID không rỗng/trùng, user tồn tại active, role Chair/Secretary/Member phân biệt hoa thường, đúng một Chair. Creator không bắt buộc tham dự; Secretary chưa bắt buộc; không thêm Guest lúc tạo. Validation roster trả 400/MSG277 với `errors` dictionary string theo key `participants`, `participants[i].userId` hoặc `participants[i].role`. Meeting/history/roster lưu cùng transaction, revision0, response Pending, không dispatch/notification ở Draft.

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm thông tin cuộc họp, lịch dự kiến, `actualStartedAt`/`actualEndedAt` (null
tới khi Chair bắt đầu/kết thúc, UC-21/UC-22), `status`, `createdById`, `createdAt`, `updatedAt`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope: `plannedStartTime`, `plannedEndTime`,
`actualStartedAt`, `actualEndedAt`, `onlineLink` cùng các trường thông tin. `mode`, `sensitivity` và
`status` là chuỗi enum. Chi tiết và response tạo họp có thêm `chair:{userId,fullName}|null`, `secretaries:[{userId,fullName}]`, `myMeetingRole:Chair|Secretary|Member|null`, chỉ tính current participants. Chair chỉ null với dữ liệu chưa backfill. Đây không phải quyền quản lý workflow của creator/approver.

Chi tiết, response tạo/sửa họp và mỗi dòng của `GET /api/meetings` còn có câu trả lời lời mời của chính người gọi (SCRUM-20, theo AI-eCabinet):

- `myResponse: "Accepted" | "Declined" | null` — `null` khi chưa trả lời (coi như tham dự), khi người gọi không có trong thành phần, và luôn `null` với Chủ tọa (Chủ tọa không được hỏi).
- `canRespond: boolean` — `true` khi nên hỏi "Bạn có tham dự cuộc họp này?": người gọi là thành phần nội bộ hiện tại, tài khoản đang hoạt động, không phải Chủ tọa, cuộc họp `Published`/`InProgress` và chưa trả lời. FE hiện hai nút trả lời theo cờ này; endpoint trả lời vẫn tự kiểm tra lại.

### PUT `/api/meetings/{id}`

PUT cập nhật thông tin cấp cuộc họp bằng dữ liệu request. Gửi field participants bị từ chối 400/MSG31; sửa roster qua nhóm participant endpoints. Trả `200 OK`, envelope
`{ "code": "MSG30", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Body không gán approver. Meeting `Draft` và `Rejected` đều được sửa trực tiếp.
PUT không chuyển trạng thái; khi sửa `Rejected`, người duyệt hiện tại và rejection history/reason
được giữ nguyên. Muốn gửi duyệt lại, Organizer gọi trực tiếp `SubmitForApproval` từ `Rejected`
với người duyệt được chọn cho cycle mới; không bắt buộc `ReturnToDraft`.

**Dời lịch — meeting `Published` (UC-15 AF-1).** Body giữ nguyên shape. Chỉ `plannedStartTime`,
`plannedEndTime`, `location`, `onlineLink` được đổi (BR-158); `title`, `mode`, `sensitivity`,
`description` khác giá trị đang lưu thì trả `400 MSG31` với `errors` đúng các field đó. Giờ bắt đầu mới
phải ở tương lai (BR-75). Có thay đổi thì trong cùng transaction: lưu lịch mới, giữ `Published`, ghi
một history `Reschedule` (`Published → Published`) và một notification `MeetingRescheduled` cho mỗi
participant active, chưa Delegated, trừ chính người dời lịch (BR-76); trả `200 MSG138`. Body không đổi gì trả `200 MSG30` và
không ghi history hay notification. Guest hiện tại có durable dispatch Rescheduled cùng transaction (SCRUM-20); link xác minh thật còn phụ thuộc SCRUM-59. Internal nhận notification như trước.

### POST `/api/meetings/{id}/transitions` — SCRUM-19

Các endpoint SCRUM-19 trong mục này là contract đã chốt để triển khai và bàn giao kiểm thử; chúng
không tự khẳng định code đã được merge hay đạt Backend Ready.

Endpoint nhận một action, không nhận actor, source/target status hoặc timestamp từ client:

| Action | Body bổ sung | Transition |
|---|---|---|
| `SubmitForApproval` | `assignedApproverId` bắt buộc | Draft hoặc Rejected → WaitingApproval |
| `Approve` | Không | WaitingApproval → Approved |
| `Reject` | `reason` bắt buộc, sau trim 1–1.000 ký tự | WaitingApproval → Rejected |
| `ReturnToDraft` | Không | Rejected → Draft; hoặc WaitingApproval → Draft khi approver hiện tại không còn hợp lệ |
| `Publish` | Không | Approved → Published |
| `TransferOrganizer` | `organizerId` GUID khác rỗng, bắt buộc | Approved → Approved; đổi Organizer khi người cũ inactive |
| `Start` | Không — SCRUM-69 | Published → InProgress |
| `End` | Không — SCRUM-69 | InProgress → Ended |

`TransferOrganizer` là đường khôi phục khi meeting đã duyệt nhưng Organizer bị vô hiệu hóa.
Gửi `{"action":"TransferOrganizer","organizerId":"<GUID người tổ chức mới>"}` qua endpoint này.
Backend đọc lại quyền `AdminUsers` của actor, khóa meeting rồi kiểm tra trạng thái/Organizer cũ và
đích đến trong transaction. Chỉ đổi `CreatedById` (Organizer hiện tại); trạng thái vẫn Approved,
assignment và nội dung đã duyệt giữ nguyên. Người tạo ban đầu vẫn được chứng minh bằng actor của
history Create. Không thêm notification/email jobs. Organizer mới Publish bằng flow bình thường.

Success trả `200 MSG89` với `{ code, message, meetingId, status, organizerId, history }`.
`status` là `Approved`, `organizerId` là Organizer mới; response không chứa object `meeting`,
tiêu đề, mô tả, địa điểm, thời gian hoặc mức độ mật của cuộc họp. Quyền recovery không cấp
quyền đọc nội dung. `history` chỉ là event `TransferOrganizer` vừa tạo với actor/time từ server,
`previousOrganizerId`, `newOrganizerId`, `fromStatus = resultingStatus = Approved`. Không sửa history
cũ. Organizer mới cùng current Assigned Approver đọc full history; admin không tự được đọc meeting/
history qua GET chỉ vì có AdminUsers. Participant timeline ẩn hai field Organizer snapshot này; Chair/Secretary active vẫn đọc rejection reason. Quyền Organizer cũ kết thúc sau transfer; participant relationship riêng vẫn áp dụng.

Không phải admin hiện tại trả `404` để không lộ resource. Người nhận thiếu/inactive/không có
MeetingCreator, trùng Organizer cũ hoặc current Assigned Approver trả `400 MSG134`. Meeting không
còn Approved hoặc Organizer cũ vẫn active trả `409 MSG135`, kể cả khi gọi lại sau transfer thành công.
Sai shape/thiếu organizerId trả `400 MSG80`; lỗi persistence/timeout rollback toàn bộ trả `503 MSG88`.
Các action khác không nhận organizerId. Quyền Chair hoặc MeetingApprover riêng không cho transfer/Publish.

`ReturnToDraft` chỉ do Organizer thực hiện, tạo đúng một lifecycle history, giữ nguyên rejection
history/reason cũ, xóa current assignment và không tạo invitation. Đây là lựa chọn tùy ý cho Rejected;
Organizer được sửa thông tin rồi submit trực tiếp từ Rejected. Submit ghi đúng một event
`Rejected → WaitingApproval`, thay assignment hiện tại bằng người duyệt được chọn, giữ nguyên
mọi history/lý do từ chối cũ. Cycle mới có thể chọn một current Assigned Approver khác.

Notification in-app theo action, ghi cùng transaction với history (`OriginHistoryId` = history đó):
`SubmitForApproval` → `MeetingWaitingApproval` cho người duyệt được chọn; `Approve` →
`MeetingApproved` cho Organizer (`CreatedById`); `Reject` → `MeetingRejected` cho Organizer, body
kèm lý do từ chối (Organizer vốn được đọc lý do). Các action này không tạo email.

Không cho Organizer tự duyệt ở mọi mức độ mật: `assignedApproverId == CreatedById` khi submit trả
`400 MSG82`, kể cả Organizer là Chair và có cả hai capability. Assignment cũ trỏ về Organizer cũng
không được Approve/Reject (`403 MSG85`); không tự sửa dữ liệu/ghi thêm history khi từ chối action.

Organizer active, còn `MeetingCreator` được dùng `ReturnToDraft` để rút `WaitingApproval` khi
assignment không hợp lệ: dữ liệu cũ chưa có người duyệt, người duyệt là chính Organizer, hoặc
current Assigned Approver đã inactive/mất capability `MeetingApprover`. Server khóa meeting
và đọc lại assignment/eligibility hiện tại trong transaction. Nếu approver là người khác Organizer,
còn active và có `MeetingApprover`, trả `409 MSG133`, không ghi history/jobs.
Rút thành công trả `200 MSG79`, chuyển sang Draft, xóa current assignment và ghi đúng một history
`ReturnToDraft` với `fromStatus = WaitingApproval`, actor/time từ server. History cũ giữ nguyên;
không tạo notification/invitation mới. Organizer sửa nháp và chọn người duyệt mới qua
`SubmitForApproval` bình thường. Không gán lại trực tiếp trong WaitingApproval hoặc dùng PUT để
bypass lifecycle; Approve/Reject vẫn kiểm tra current assignment và quyền hiện tại.

Các action ngoài `TransferOrganizer` thành công trả `200 OK` với `{ code, message, meeting, history }`.
`TransferOrganizer` dùng response tối thiểu đã mô tả ở trên. Code thành công theo
action lần lượt là `MSG78` (SubmitForApproval), `MSG43` (Approve), `MSG44` (Reject), `MSG79`
(ReturnToDraft), `MSG45` (Publish) và `MSG89` (TransferOrganizer). FE nên dùng `code` để xử lý tình huống; `message` chỉ dành
cho hiển thị.

Publish khóa meeting, kiểm tra quyền, trạng thái Approved, đúng một Chair active và ít nhất một
Secretary active trong transaction trước khi ghi bất kỳ thay đổi nào. Invitation response không
ảnh hưởng readiness.

Publish tạo notification theo BR-54: chỉ participant có user active và `Response != Delegated`;
không tự thêm người được ủy quyền chỉ từ `DelegatedToUserId`. Delegate cần participant row riêng
hợp lệ để nhận notification. Người thực hiện Publish không nhận notification in-app, kể cả khi họ
là participant. Email dùng producer SCRUM-15: gọi
`QueueForPublishedMeetingAsync(meetingId)` sau khi transaction công bố đã commit; producer đọc
meeting đã Published và chọn **toàn bộ** participant có user active, không lọc `Response` và không
loại người Publish (người tạo là participant thì vẫn nhận email).
Producer lưu `ToAddress`, `Subject`, `Body` (có giờ bắt đầu/kết thúc theo giờ Việt Nam) và tệp
`.ics` (`DTSTART`/`DTEND` từ lịch dự kiến) khi tạo job. Thay đổi dữ liệu sau commit không sửa nội
dung job đã lưu. Khi SMTP được bật, job mới có trạng thái `Queued`;
SMTP tắt thì không tạo job mới. HTTP 200 chỉ xác nhận công bố thành công, không xác nhận email đã
được gửi. Nếu producer chưa khởi tạo được job, API vẫn trả `200 MSG45`, meeting và publication
history vẫn Published; backend ghi log và worker tự đối soát meeting Published chưa có job để thử
lại. Deduplication key bảo đảm retry không tạo email trùng hoặc publication history mới. Lỗi lưu
status/history/notification trước commit vẫn rollback và trả `503 MSG47`. Worker SCRUM-15 xử lý
gửi bất đồng bộ và quản lý retry/failure theo contract của worker.
Các writer participant thuộc SCRUM-20 phải dùng cùng meeting-row lock/serialization protocol để một
lần Publish và thay đổi participant đồng thời có ranh giới snapshot xác định.

### GET `/api/meetings/{id}/history` — SCRUM-19

Trả lifecycle history theo `occurredAt ASC`, rồi `id ASC`, có pagination. Mỗi item tối thiểu có
`id`, `meetingId`, `action`, `fromStatus`, `resultingStatus`, `actorId`, `actorName`, `occurredAt`,
`rejectionReason` và snapshot `assignedApproverId` khi action là SubmitForApproval.

History TransferOrganizer còn có `previousOrganizerId` và `newOrganizerId`; các action khác trả null.

Meeting Organizer và current Assigned Approver đọc được toàn bộ history và rejection reason.
Secretary được gán hợp lệ theo matrix trên đọc được history/rejection reason cả trước Published;
hai field `previousOrganizerId`/`newOrganizerId` vẫn null nếu không có full-history permission.
Participant khác có quyền đọc meeting từ Published trở đi nhận timeline với `rejectionReason: null`;
participant relationship không cấp quyền action. Former approver không có quyền chỉ nhờ assignment
trước đây, nhưng một quan hệ participant độc lập vẫn cho timeline đã redacted sau Publish. API không
có endpoint update/delete history; các row là immutable audit evidence.
GET history đọc meeting bằng truy vấn không tracking, không dùng `FOR UPDATE`. Một transaction đang
khóa meeting để PUT/transition không làm request history phải chờ khóa ghi; GET chỉ đọc dữ liệu đã commit.
Query `page`/`pageSize` mặc định `1`/`20`; `page >= 1`, `pageSize` từ `1` đến `100`. Trả `200`
với `{ items, page, pageSize, totalCount, totalPages }`; trang vượt cuối trả `items: []`, kể cả
khi offset lớn hơn `Int32.MaxValue`. Repository kiểm tra tổng số dòng trước khi ép offset sang `int`.
Nếu offset vượt `Int32.MaxValue` nhưng vẫn nằm trong tập history hiện có, trả `400 MSG86`
vì EF paging không thể biểu diễn vị trí đó an toàn.

### Dữ liệu Development để kiểm thử workflow

Development seeder bổ sung một meeting `Draft` có sẵn agenda và ba participant active/chưa Delegated,
để test API trước khi endpoint quản lý agenda và participant được tích hợp:

- ID: `0199ac54-d000-7000-8000-000000000019`.
- Tiêu đề: `SCRUM-19 — Luồng duyệt và công bố (demo)`.
- Organizer: `thuky@trustcabinet.local`; approver có thể chọn `chutoa@trustcabinet.local` từ GET approvers.
- Participant: thư ký, chủ tọa và `thanhvien@trustcabinet.local`.
- Tài khoản demo dùng mật khẩu Development đã ghi ở `DevelopmentDataSeeder.DemoPassword`.

Đăng nhập Organizer, gọi readiness (ban đầu `ready: true`), submit với ID approver, đăng nhập approver
để Approve/Reject, rồi dùng Organizer gửi lại trực tiếp nếu Rejected hoặc tùy chọn ReturnToDraft.
Seeder đặt lịch demo vào ngày hôm sau, kéo dài một giờ.
Publish thành công ghi durable invitation dispatch trong transaction; worker materialize email khi SMTP bật, không xác nhận email đã gửi. Có thể kiểm tra history/notification bằng API. Chair và Secretary đọc meeting/history/rejection reason từ Draft; Member chỉ đọc từ Published.

Seeder chỉ chèn meeting này một lần khi đủ tài khoản demo; trên database Development cũ có các
tài khoản đó, khởi động lại app sẽ thêm demo còn thiếu. Các lần khởi động sau không reset status,
agenda, participant, assignment hay history đã thao tác. Không tự tái tạo một Draft sau khi demo
được Publish; muốn chạy lại từ đầu, dùng một database Development riêng mới.

### GET `/api/meetings/{id}/submission-readiness` — SCRUM-19

Đọc readiness từ cùng validator mà SubmitForApproval gọi lại trong transaction. Meeting chỉ ready
khi thông tin bắt buộc ở cấp meeting, gồm lịch dự kiến hợp lệ, đầy đủ (UC-18/BR-33). Không thêm
Agenda-required rule vì UC-18 chưa yêu cầu. Readiness không đòi giờ bắt đầu ở tương lai. Endpoint chỉ đọc,
không đổi status/assignment hoặc tạo history/notification. `200 OK`
trả `{ meetingId, status, ready, errors }`; dữ liệu chưa ready là `ready: false` cùng map lỗi, không
phải HTTP error. Chỉ Organizer có `MeetingCreator` được gọi khi meeting ở `Draft` hoặc `Rejected`.

Readiness áp dụng rule hiện tại trên dữ liệu đã persist; validator Meeting dùng lại
`MeetingRules`. Bảng dưới được suy ra từ entity/configuration `Meeting`; field nullable/không bắt buộc không
được biến thành điều kiện submit mới.

| Entity.field | Điều kiện khi submit | Schema/rule |
|---|---|---|
| `Meeting.Title` | Bắt buộc sau trim, 1–300 ký tự | `required`, EF `IsRequired`, max 300 |
| `Meeting.PlannedStartTime`, `Meeting.PlannedEndTime` | Bắt buộc; kết thúc sau bắt đầu (BR-62) | Non-null |
| `Meeting.Mode`, `Meeting.Sensitivity` | Phải là enum đã định nghĩa | Non-null, lưu chuỗi |
| `Meeting.Location` | Không bắt buộc; tối đa 300 ký tự | Không thêm yêu cầu theo mode |
| `Meeting.OnlineLink` | Không bắt buộc; tối đa 1.000 ký tự | Nullable |
| `Meeting.Description` | Không bắt buộc; tối đa 4.000 ký tự | Chuỗi optional về nghiệp vụ |
| Agenda | Không bắt buộc khi submit hoặc Publish | Validation thuộc story quản lý agenda |
| Actual times, documents, voting sessions | Không bắt buộc | Phát sinh trong phiên họp hoặc thuộc flow khác |

`assignedApproverId` được chọn trong action SubmitForApproval chứ không quyết định readiness; Submit
kiểm tra lại user active, capability `MeetingApprover` và khác Organizer bên trong transaction. GET readiness không
giữ transaction khi Organizer chọn người duyệt, nên Submit luôn chạy lại toàn bộ kiểm tra.

### Lỗi workflow SCRUM-19

Lỗi workflow có MSG code trả body `{ code, message, errors? }`. `401`, `403` từ active-user gate và
`404` che giấu tài nguyên không có body lỗi này. `errors` là map field -> message và chỉ có
trên lỗi readiness/validation. Actor lấy từ phiên đăng nhập; client không gửi actor, source/target
status hoặc timestamp. POST chỉ nhận JSON object với các field đúng theo action, không nhận field
lạ/trùng, enum dạng số, hoặc field không áp dụng cho action. `action` phải khớp chính xác một tên
trong bảng action: không nhận chuỗi số (`"4"`), danh sách tên (`"SubmitForApproval,Publish"`),
khoảng trắng bao quanh hoặc tên khác chữ hoa/chữ thường. Request body tối đa 64 KiB.

| HTTP | Code | Khi nào |
|---:|---|---|
| 400 | `MSG80` | JSON/action/GUID/field sai hoặc field không áp dụng cho action |
| 400 | `MSG81` | Submit thiếu thông tin cấp meeting; trả `errors` theo field |
| 400 | `MSG82` | Approver không còn active, thiếu `MeetingApprover` hoặc là chính Organizer |
| 400 | `MSG83` | Reject không có reason hoặc reason chỉ có whitespace |
| 400 | `MSG84` | Reason sau trim dài hơn 1.000 ký tự |
| 400 | `MSG86` | Query pagination history không hợp lệ |
| 401 | — | Chưa đăng nhập |
| 403 | `MSG85` | Đọc được meeting nhưng không phải actor/relationship/capability của action, hoặc assignment cũ cho Organizer tự duyệt |
| 403 | — | Tài khoản inactive theo active-user gate |
| 404 | — | Meeting không tồn tại hoặc caller không được phép đọc/action trên tài nguyên; body rỗng |
| 403 | `MSG46` | Organizer còn đọc được meeting nhưng đã mất `MeetingCreator`; không công bố hoặc tạo history/jobs |
| 409 | `MSG41`, `MSG46`, `MSG132`, `MSG133` | Submit, Publish, quyết định duyệt hoặc ReturnToDraft không còn hợp lệ do trạng thái; không ghi history/jobs |
| 409 | `MSG137` | Publish thiếu đúng một Chair active hoặc thiếu Secretary active; không ghi history, notification hoặc email job |
| 200/400/409 | `MSG89` / `MSG134` / `MSG135` | TransferOrganizer thành công / đích không hợp lệ / trạng thái hoặc Organizer cũ không cho transfer |
| 413 | `MSG87` | JSON request vượt 64 KiB |
| 415 | `MSG34` | POST không dùng `application/json`; dùng chung code với PUT |
| 503 | `MSG42`, `MSG47`, `MSG88` | Lỗi database/transaction khi transition; transaction rollback |
| 503 | `MSG130`, `MSG131`, `MSG136` | Không tải được readiness/history/approvers hoặc không kiểm tra được tài khoản active |

`GET submission-readiness` trả `409 MSG41` nếu meeting không còn ở `Draft` hoặc `Rejected`;
history/readiness trả `404` nếu caller không được phép xem resource. Retry action sau khi commit thường
trả `409` do state đã đổi. Client nên tải lại meeting/history để xác định kết quả đã commit, và không
được suy diễn rằng `409` có nghĩa lần gọi trước thất bại.

### Ranh giới tích hợp

SCRUM-19 ghi status, history, database notification và gọi producer SCRUM-15 bằng meeting ID trong
cùng scoped DbContext/transaction. Producer chọn participant có user active, lưu nội dung email
và dùng `DeduplicationKey = meeting:{meetingId}:published:{userId}`. Worker gửi nội dung đã lưu sau
commit. SMTP `Enabled=false` không tạo job mới; các job cũ vẫn được giữ. Producer/persistence/timeout
lỗi lưu publication trả `503 MSG47` và rollback toàn bộ transaction. Lỗi khởi tạo email sau commit
không rollback publication; worker đối soát và thử lại. Bốn cột email không dùng `OriginHistoryId`,
`RecipientUserId`, `MessageType`, `PayloadSnapshot` đã được bỏ bằng migration mới; key, nội dung
thư, `MeetingId` và trạng thái delivery vẫn giữ. Invitation Publish có tệp `.ics` theo lịch dự kiến;
`Notification.OriginHistoryId` không thay đổi.
Database notification rows được tạo ở đây; realtime push theo SCRUM-14 là phần tích hợp riêng.
Recipient filter của notification đã chốt theo BR-54 và được gom thành shared policy để SCRUM-20 dùng lại. Writer
participant thuộc story tương ứng phải khóa meeting cha trong cùng transaction trước khi ghi thay đổi;
protocol này bảo vệ snapshot người nhận khi Publish đồng thời.
Update sau commit không đổi rows đã persist.

Validation agenda thuộc SCRUM-25/BA; SCRUM-19 không dùng agenda để chặn gửi duyệt.
Migration `MeetingOwnsScheduleDropSessions` (ADR-0013) chép lịch từ `meeting_sessions` về `meetings`:
giờ bắt đầu sớm nhất và giờ kết thúc muộn nhất trong các phiên không `Cancelled` (phiên nào cũng hủy
thì lấy khoảng của chúng, không có phiên thì `created_at` + 1 giờ), rồi drop bảng. `actual_*` để null
vì phiên cũ không ghi bằng chứng giờ thực tế. `MinutesIssued`/`Archived` chuyển thành `Ended`, cả
trong `meeting_status_history`; trigger append-only chỉ được tắt quanh đúng lệnh đổi nhãn đó.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
| `mode` | Một trong `InPerson`, `Online`, `Hybrid`; mặc định `InPerson` nếu bỏ qua |
| `sensitivity` | Một trong `Normal`, `Internal`, `Confidential`, `Secret`; mặc định `Normal` nếu bỏ qua |
| `plannedStartTime` | Bắt buộc; ISO 8601 có giây và offset; tạo mới và dời lịch phải ở tương lai (BR-162, BR-75) |
| `plannedEndTime` | Bắt buộc; cùng định dạng; sau `plannedStartTime` (BR-62) |
| `location` | Tùy chọn; trim; tối đa 300 ký tự |
| `onlineLink` | Tùy chọn; trim, rỗng thành `null`; tối đa 1.000 ký tự (BR-163) |
| `description` | Tùy chọn; tối đa 4.000 ký tự; giữ xuống dòng |

Sửa nháp hoặc meeting bị từ chối không đòi giờ bắt đầu ở tương lai; chỉ tạo mới và dời lịch đòi.
Không kiểm tra trùng tên vì yêu cầu hiện tại chưa quy định luật này.

Lỗi validation trả `400`:

```json
{
  "code": "MSG31",
  "message": "Thông tin cuộc họp không hợp lệ.",
  "errors": {
    "title": "Tên cuộc họp không được để trống."
  }
}
```

Endpoint trả đồng thời mọi lỗi field phát hiện được. Với PUT, hệ thống khóa meeting và kiểm tra
trạng thái trước khi parse JSON: nếu meeting không còn sửa được thì trả `409 MSG33`, kể cả body JSON
sai cú pháp hoặc sai kiểu. Request PUT có Content-Type thiếu hoặc không hỗ trợ trả `415` với code
`MSG34`. Với meeting còn sửa được và Content-Type hợp lệ, JSON sai cú
pháp/sai kiểu trả `400` và code `MSG31` không có `errors`. Body PUT lớn hơn
64 KiB trả `413 MSG87`. POST JSON lỗi cũng trả `400`.

## Mã phản hồi

| HTTP | Code | Ý nghĩa |
|---:|---|---|
| 201 | `MSG29` | Tạo nháp thành công |
| 200 | `MSG30` | Sửa thông tin thành công (hoặc PUT dời lịch không đổi gì) |
| 200 | `MSG138` | Dời lịch meeting `Published` thành công; người được mời đã có notification |
| 400 | `MSG31` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 400 | `MSG32` | Query danh sách không hợp lệ; có `errors` theo tham số |
| 401 | — | Chưa đăng nhập |
| 403 | — | Không có `MeetingCreator` theo gate hai lớp |
| 404 | — | GET: không tồn tại hoặc caller không có quan hệ; PUT: không tồn tại hoặc caller không phải người tạo; body rỗng |
| 409 | `MSG33` | Trạng thái hiện tại không cho sửa (khác `Draft`, `Rejected`, `Published`) |
| 413 | `MSG87` | Body PUT vượt quá 64 KiB |
| 415 | `MSG34` | PUT không gửi JSON hoặc Content-Type không hỗ trợ |
| 500 | `MSG35` | Không thể lưu khi tạo; thử lại |
| 500 | `MSG36` | Không thể lưu khi sửa; thử lại |
| 500 | `MSG37` | Không tải được chi tiết; thử lại |
| 500 | `MSG38` | Không tải được danh sách; thử lại |

Mã thuộc dải chung của dự án, xem [danh mục mã](00-danh-muc-ma-thong-bao.md). Các lỗi persistence
của CRUD/list trả `500`; lỗi workflow SCRUM-19 giữ contract `503` để client nhận biết lỗi tạm thời
của transaction hoặc capability lookup.

## Persistence và transaction

Cột `assigned_approver_id` nullable. POST lưu meeting `Draft`, chưa gán người duyệt; assignment được
thực hiện trong UC-18. `planned_start_time`, `planned_end_time` non-null, có index `(status, planned_start_time)`.
POST lưu meeting cùng một dòng `MeetingStatusHistory` (`FromStatus = null`, `ToStatus = Draft`, `ActorId = creator`). EF Core
lưu các thay đổi này trong cùng transaction.

PUT kiểm tra quyền sở hữu và trạng thái hiện tại bằng truy vấn không khóa trước khi chờ body. Sau khi
xác nhận meeting còn sửa được, hệ thống kiểm tra Content-Type rồi đọc body vào buffer ngoài
transaction khóa ghi. Body bị giới hạn 64 KiB ngay cả khi không có `Content-Length`; quá giới hạn
thì ngừng đọc và trả `413`, không tạo thêm bản sao buffer. Khi xử lý cập nhật, transaction
`ReadCommitted` khóa đúng meeting row bằng
PostgreSQL `FOR UPDATE`, đọc lại trạng thái và trả `409 MSG33` nếu trạng thái đổi trong lúc body đang
được gửi. Chỉ sau lần kiểm tra khóa này hệ thống mới parse JSON, validate và lưu; không có network
read trong lúc giữ row lock.
Transaction kết thúc trước response. Cập nhật thông tin ở `Draft`/`Rejected` không thay status hoặc
lịch sử; dời lịch ghi history `Reschedule` và notification trong cùng transaction. Nếu body sau
chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng không ghi DB hoặc đổi `updatedAt`. PUT không
xóa assignment hiện có, kể cả dữ liệu Draft cũ hoặc `Rejected` có current approver. Dữ liệu cho
phép sửa giữa hai form cũ dùng “lần ghi sau cùng thắng”; contract chưa có ETag/version để từ chối
stale content.

## Hợp đồng tích hợp FE

- Gọi API qua client hiện có bằng đường dẫn tương đối `/api/...` để tự mang token và refresh phiên.
- Khi nhận `403`, xử lý như quyền hệ thống đã thay đổi; GET chi tiết trả `404` nếu caller không có
  quan hệ với meeting.
- Chỉ hiển thị thao tác sửa cho người có `MeetingCreator`, đúng `createdById`, và trạng thái
  `Draft` hoặc `Rejected`; ở `Published` nút là "Dời lịch", mở cùng form với tên, mô tả, hình thức,
  mức độ bị khoá. Backend kiểm tra lại quyền sở hữu và trạng thái khi PUT.
- Nếu PUT trả `409 MSG33`, hiển thị nội dung lỗi và tải lại trạng thái hiện tại trước khi cho sửa tiếp.
- Sau khi POST hoặc PUT thành công, FE hiển thị message và mở chi tiết `meeting.id`. Danh sách
  SCRUM-18 đã tích hợp lọc, sort và pagination server-side; tiêu chí được giữ trên URL.
- Màn danh sách mở sẵn bộ lọc **Đang diễn ra** (`status=InProgress`); đường dẫn không có `status`
  nghĩa là bộ lọc mặc định này, còn `status=all` là bỏ lọc trạng thái. Bộ lọc **Thời điểm** chỉ có
  các lựa chọn sẵn như AI-eCabinet: Hôm nay, Tuần này (Thứ Hai–Chủ Nhật) gửi `from`/`to` theo múi
  giờ người xem; Chưa diễn ra, Đã diễn ra gửi `timing`. Không còn ô chọn khoảng ngày.
- FE mở form và nút chỉnh sửa ở cả `Draft` và `Rejected` theo contract PUT. Sửa nội dung ở
  Rejected giữ nguyên trạng thái, assignment và rejection history; gửi duyệt lại trực tiếp bằng
  `SubmitForApproval`. `ReturnToDraft` là tùy chọn.
- **Lịch trên Meeting (ADR-0013):** form có Ngày họp, Bắt đầu, Kết thúc (một ngày, giờ địa phương,
  gửi UTC ISO); Địa điểm hiện với Trực tiếp/Kết hợp, Link họp trực tuyến hiện với Trực tuyến/Kết hợp,
  phần bị ẩn không được gửi. Danh sách có cột "Thời gian" (sắp xếp được, mặc định
  `plannedStartTime desc`) và "Hình thức · Địa điểm". Trang chi tiết hiện lịch dự kiến, giờ thực tế khi
  có, link trực tuyến; tab "Phiên họp" đã bỏ.
- **Lịch làm việc (SCRUM-47):** đọc `GET /api/meetings?from&to` với `pageSize=100`, sắp theo
  `plannedStartTime asc`, và đọc **đủ mọi trang** của khoảng đang xem (dạng tháng có thể quá 100
  cuộc họp). Dạng tháng gửi `from` = Thứ Hai đầu lưới, `to` = cuối Chủ Nhật cuối lưới. Bộ lọc
  trạng thái của lịch theo AI-eCabinet: sáu nhóm (Chờ duyệt, Đã duyệt, Đã công bố, Đang diễn ra, Đã
  kết thúc — gồm cả nháp, Bị từ chối), chọn được nhiều nhóm, **mặc định chỉ bật Đã duyệt, Đã công
  bố, Đang diễn ra** và luôn giữ ít nhất một nhóm; mức độ chọn một. FE lọc trên tập đã tải (server
  đã lọc quyền xem theo BR-29) thay vì gửi `status`/`sensitivity`. URL của lịch:
  `?span=work|full|month`, `week=YYYY-MM-DD`, `month=YYYY-MM`, `mode=list`,
  `status=waiting,approved,published,live,ended,rejected` (không có là nhóm mặc định, `all` là mọi
  nhóm), `sensitivity=Secret`.

### Luồng duyệt trên trang chi tiết (SCRUM-19)

`transitionMeeting`, `getMeetingSubmissionReadiness` và `getMeetingHistory` nằm trong
`src/api/meetings.ts`; giao diện ở `src/features/meetings/workflow/`. Request tạo/sửa không còn
`assignedApproverId`.

- **Thanh trạng thái** giữa tiêu đề và các tab, chỉ hiện cho Organizer (có `MeetingCreator`) và
  current Assigned Approver (có `MeetingApprover`, khác Organizer), từ `Draft` tới `Approved`. Thanh
  nói cuộc họp đang ở đâu, đang chờ ai, bước tiếp theo, kèm đúng nút của bước đó. Vai trò suy từ
  `createdById`, `assignedApproverId` và capability do server trả; server vẫn kiểm tra lại mọi action.
- **Draft và Rejected (Organizer):** gọi readiness mỗi khi meeting đổi (`updatedAt`). Chưa ready thì
  liệt kê các câu lỗi của backend (đã gộp trùng) và khoá nút gửi; không gọi được readiness thì vẫn cho
  gửi vì Submit kiểm tra lại. Ở Rejected, thanh hiện người từ chối, thời điểm, lý do và nút "Gửi duyệt
  lại" (UC-18 AF-1) — gửi thẳng, không qua `ReturnToDraft`.
- **Gửi duyệt / Gửi duyệt lại:** dialog đọc lại `GET /approvers` mỗi lần mở và bắt buộc chọn một
  người; gửi lại được chọn cùng người hoặc người khác. Danh sách
  rỗng thì báo cần quản trị viên phân quyền và khoá nút. `MSG81` hiện danh sách thiếu sót trong
  dialog; `MSG82` báo dưới ô chọn và tải lại danh sách.
- **WaitingApproval (Organizer):** "Đang chờ {tên} duyệt" cùng giờ gửi. Người duyệt hiện tại không
  còn trong `GET /approvers` (bị vô hiệu hoá hoặc mất quyền) thì thanh chuyển sang cảnh báo kèm
  "Rút về nháp"; backend trả `409` nếu người duyệt thật ra vẫn hợp lệ.
- **WaitingApproval (Approver):** "Duyệt" mở dialog xác nhận; "Từ chối" mở dialog bắt buộc lý do
  1–1.000 ký tự, có bộ đếm. `MSG83`/`MSG84` hiện dưới ô lý do; lý do đã gõ được giữ khi lỗi.
- **Approved:** hiện người duyệt và thời điểm; Organizer có "Công bố". Cách xử lý `MSG45`–`MSG47` theo
  [note FE của SCRUM-15](07-gui-email-thu-moi-note-cho-fe.md#trạng-thái-tích-hợp-fe). Approver thấy
  quyết định của chính họ ở cả Rejected và Approved, kèm lý do khi từ chối.
- **Kết quả:** thành công thì toast `message` của backend, thay meeting và thêm dòng history từ
  response, không gọi lại API. `409`, `403 MSG85` và `403 MSG46` (Organizer mất `MeetingCreator` lúc
  công bố) đóng dialog, toast lý do, rồi tải lại meeting và history; hai mã `403` tải lại cả phiên. `403` không có code đi theo
  luồng mất quyền chung; `404` hiện trang không tìm thấy. Lỗi `503` ở lại trong dialog để thử lại.
- **Lịch sử trạng thái** (cột phải, mọi tab): cũ nhất trước như API, mỗi dòng là badge hành động
  theo màu trạng thái, người thực hiện, thời gian; dòng gửi duyệt ghi người duyệt (khi biết tên), dòng
  từ chối trích lý do giữ xuống dòng. Gửi duyệt từ Rejected ghi "Gửi duyệt lại". `ReturnToDraft` từ
  WaitingApproval ghi "Rút về nháp"; dòng từ Rejected (dữ liệu cũ, backend vẫn nhận action này) ghi
  "Đưa về nháp". Có skeleton, trạng thái rỗng, lỗi kèm "Thử lại" và "Xem thêm" khi quá
  50 dòng.
- **Chưa có giao diện:** `TransferOrganizer`. Admin không đọc được meeting và chưa có API liệt kê
  meeting `Approved` có Organizer bị vô hiệu hoá, nên FE chưa có chỗ hợp lý để đặt thao tác này.
- Readiness chỉ kiểm tra thông tin cấp cuộc họp, gồm lịch dự kiến; agenda không bắt buộc (UC-18).

## Checklist BA cần hoàn tất

- Đồng bộ nội dung BR-24–BR-29 chính thức vào tài liệu BA và map từng luật vào validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Người duyệt được chọn trong UC-18; Planned Start Time và Planned End Time nằm trên Meeting.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Định danh Use Case đã được chuẩn hóa theo danh mục hiện tại: UC-14 → UC-20 cho Meeting Management.

## Bàn giao thay đổi roster PO 20/10/2026

Form FE hiện có phải bổ sung participants khi tạo. Chi tiết contract, lỗi MSG277 và UC-73: [API roster](09-thanh-phan-tham-du.md), [handoff](09-thanh-phan-tham-du-ban-giao.md). Thay đổi này không sửa quyền submit/approve/publish/reschedule.

## SCRUM-69 — Start/End (UC-21/22)

`POST /api/meetings/{id}/transitions` nhận `{"action":"Start"}` hoặc `{"action":"End"}`.
Chỉ nhận `action`, đúng casing; property lạ/trùng (kể cả actor, timestamp, reason) bị từ chối.
Giới hạn 64 KiB, Content-Type application/json. Request End là xác nhận của người dùng.

- Start: active current Chair, Published → InProgress; đúng một active current Chair và ít nhất một active current Secretary. Không yêu cầu Accepted/Present, giờ dự kiến, agenda hoặc LiveKit.
- End: active current Chair, InProgress → Ended; không gate Secretary, Vote Open, attendance hay Pending agenda. Current agenda thành Done trong cùng transaction; Pending/Done khác giữ nguyên.
- Thành công: `MeetingTransitionResponse { code, message, meeting, history }`; Start `200/MSG110`, End `200/MSG111`. Server ghi actualStartedAt/actualEndedAt UTC microsecond và một history Start/End; End giữ actualStartedAt.
- Không đăng nhập: 401; inactive: 403 theo gate; lookup gate lỗi: 503/MSG131.
- Không tồn tại/không được đọc: 404 body rỗng. Đọc được nhưng không phải Chair: 403/MSG112 (Start), MSG114 (End). Sai trạng thái/readiness: 409 cùng mã. Gọi lặp cũng 409, không lặp side effect.
- Persistence thất bại trước commit: 503/MSG115 (Start), MSG116 (End); timestamp agenda không hợp lệ cũng MSG116 và rollback. JSON/action sai 400/MSG80, Content-Type sai 415/MSG34, body quá lớn 413/MSG87.

Meeting, current agenda và history được ghi dưới khóa Meeting trong một transaction ReadCommitted.
Notification Start, agendaChanged/MeetingEnded và đóng LiveKit chạy sau commit với budget độc lập request.
Lỗi side effect không đổi kết quả thành công; notification creation best effort, room được janitor đối soát.
Không có lifecycle event Start mới. Xem [bàn giao FE/Tester](10-scrum69-ban-giao.md).

## SCRUM-99 — Huỷ cuộc họp (UC-75) và tự huỷ khi quá hạn

Quyết định: [ADR-0032](../05-decisions/0032-huy-cuoc-hop-va-tu-huy-khi-qua-han.md).

`POST /api/meetings/{id}/transitions` nhận `{"action":"Cancel","reason":"…"}`.

- Người gọi phải là Organizer (`createdById`) còn capability `MeetingCreator`; cuộc họp ở `Draft`, `WaitingApproval`,
  `Rejected`, `Approved` hoặc `Published` (BR-297).
- `reason` bắt buộc, cắt khoảng trắng hai đầu, tối đa 1.000 ký tự.
- Thành công `200/MSG261`, `MeetingTransitionResponse`; `meeting.status = "Cancelled"`, `meeting.cancellation` có
  `cancelledAt`, `cancelledById`, `cancelledByName`, `reason`; `history.action = "Cancel"`, `history.cancellationReason`.
- Thiếu/rỗng lý do `400/MSG262`; quá 1.000 ký tự `400/MSG345`; đã bắt đầu/kết thúc/đã huỷ `409/MSG263`; đọc được nhưng không
  phải Organizer `403/MSG263`; không lưu được `503/MSG264`; không đọc được `404`.
- Huỷ từ `Published`: trong cùng transaction, mỗi người tham dự đang hoạt động và mỗi Guest chưa thu hồi có một dispatch
  `Cancelled`; người tham dự nội bộ (trừ người huỷ) có thông báo `MeetingCancelled`. Email gửi sau đó, chỉ cho người đã nhận
  thư mời, kèm lý do và `.ics` `METHOD:CANCEL` cùng UID để lịch tự gỡ sự kiện. Lời mời chưa gửi của cuộc họp đã huỷ bị bỏ.
- Huỷ từ `WaitingApproval`: người duyệt được phân công nhận thông báo `MeetingCancelled`. Huỷ từ trạng thái khác: không báo ai.
- Thành viên/Chair thấy cuộc họp `Cancelled` khi nó đã được công bố trước khi huỷ (`cancelledFromStatus = Published`);
  cuộc họp huỷ trước khi công bố vẫn ẩn với họ như trước.

**Tự huỷ (BR-301, PO 27/10/2026):** `MeetingOverdueWorker` chạy 15 phút một lần, huỷ cuộc họp chưa bắt đầu (5 trạng thái trên)
khi `plannedStartTime` đã qua `Meetings:AutoCancelAfterHours` giờ (mặc định 24). History `AutoCancel`, `actorId = null`,
`actorName = "Hệ thống"`, lý do “Không diễn ra theo lịch: quá 24 giờ sau giờ bắt đầu dự kiến mà chưa bắt đầu.”; chỉ Organizer
nhận thông báo. Quy tắc tự `Ended` sau 4 giờ (BR-302) **bỏ** theo PO.

`GET /api/meetings/{id}/history`: `actorId` có thể `null` (Hệ thống); thêm `cancellationReason` trên dòng `Cancel`/`AutoCancel`,
ai đọc được cuộc họp cũng thấy. `GET /api/meetings?status=Cancelled` lọc được trạng thái mới.

