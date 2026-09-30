# 05 · API quản lý cuộc họp

**UC-11 Create Meeting / UC-12 Update Meeting — SCRUM-17; UC-14 View Meeting List — SCRUM-18;
UC-15 Submit / UC-16 Review / UC-17 Publish — SCRUM-19.** Mã UC ghi kèm tên vì UC-11/UC-12
trong tài liệu quản lý vai trò đang dùng cho use case khác.

## Phạm vi

Các endpoint cho người tạo khởi tạo cuộc họp nháp, chọn người duyệt khi gửi duyệt, sửa, chuyển lifecycle và đọc
lịch sử. Người có quan hệ hợp lệ với cuộc họp có thể đọc chi tiết và lấy trang danh sách. Danh sách
`GET /api/meetings` thuộc SCRUM-18. Danh sách, tạo, sửa và chi tiết đã gọi API thật; form tạo/sửa
chỉ gửi bảy field thông tin, không chọn hoặc gửi approver. Người duyệt được chọn khi gửi duyệt.
Các type/API wrapper cho readiness, transitions và history đã có; màn thao tác workflow và timeline
thật thuộc bước tích hợp FE của SCRUM-19.

Tiêu đề và Thời gian bắt đầu bắt buộc theo BR-24; Thời gian kết thúc tùy chọn, nếu nhập thì phải
sau thời gian bắt đầu theo BR-25. Cuộc họp mới ở `Draft` chưa có approver; Organizer chọn approver
hợp lệ trong action `SubmitForApproval`. Form khởi tạo Hình thức là `InPerson` và Mức độ mật là `Normal`.

## Quyền truy cập

POST, `GET /api/meetings/approvers` và PUT yêu cầu capability `MeetingCreator`; cổng đọc claim và
xác minh lại capability từ database theo SCRUM-12, không kiểm theo tên vai trò. GET chi tiết yêu cầu
đăng nhập và tài khoản còn hoạt động, sau đó kiểm tra quan hệ cuộc họp theo BR-29.

GET luôn cho người tạo đọc. Người duyệt được gán chỉ đọc từ khi meeting rời `Draft`; thành phần tham
dự (Chair, Secretary và Participant) chỉ đọc từ `Published` trở đi. Vì vậy meeting `Draft` chỉ người
tạo thấy, còn các trạng thái trước khi công bố không lộ cho thành phần tham dự. Không đủ quyền đọc
thì trả `404` rỗng, giống ID không tồn tại. PUT vẫn chỉ cho người tạo/người tổ chức sửa; ID không tồn
tại và meeting của người tổ chức khác cùng trả `404` rỗng.

GET danh sách dùng đúng quy tắc đọc trên **trong truy vấn database**, trước `COUNT`, sắp xếp và phân
trang. Endpoint chỉ yêu cầu đăng nhập và tài khoản còn hoạt động; không đòi capability
`MeetingCreator`. `totalCount` không bao gồm cuộc họp caller không được xem.

Organizer được sửa thông tin khi meeting ở `Draft` hoặc `Rejected`. PUT ở `Rejected` giữ nguyên
trạng thái, current Assigned Approver và rejection history/reason; không tạo lifecycle history,
notification hoặc invitation. Hệ thống đọc trạng thái mới nhất sau khi khóa dòng và trước khi
validate nội dung. Trạng thái khác `Draft`/`Rejected` trả `409 MSG33`.

### Authorization matrix cho SCRUM-19

| Hành vi | Quan hệ/capability bắt buộc | Chair có tự được phép không? |
|---|---|---|
| SubmitForApproval | Meeting Organizer (`CreatedById`) + `MeetingCreator`; meeting Draft và ready | Không |
| ReturnToDraft | Meeting Organizer + `MeetingCreator`; meeting Rejected | Không |
| Approve/Reject | Current `AssignedApproverId` + capability `MeetingApprover`, khác Organizer; meeting WaitingApproval | Không; chỉ được nếu Chair đồng thời là current Assigned Approver và không phải Organizer |
| Publish | Meeting Organizer + `MeetingCreator`; meeting Approved | Không; chỉ được nếu Chair đồng thời là Organizer |
| Đọc full lifecycle history/rejection reason | Meeting Organizer hoặc current Assigned Approver | Chair relationship riêng không cấp quyền ở bất kỳ trạng thái nào |
| Đọc timeline sau Publish | Người có quyền đọc meeting đã công bố | Chỉ timeline; rejection reason bị ẩn |

Người từng là Assigned Approver không giữ quyền đọc history chỉ vì assignment cũ xuất hiện trong
một history row. Authorization dùng assignment hiện tại; snapshot cũ là bằng chứng kiểm toán,
không phải access grant. Organizer/current Assigned Approver được đọc full history ở mọi lifecycle
status. Participant có thể đọc timeline sau Publish theo meeting read policy; rejection reason luôn
được ẩn với họ, trừ khi đồng thời là Organizer hoặc current Assigned Approver. Former approver không
có quyền chỉ vì assignment cũ, nhưng nếu họ là participant thì vẫn nhận timeline đã redacted sau
Publish. Người không đủ quyền đọc meeting nhận `404` body rỗng như một meeting không tồn tại.

## Contract

### GET `/api/meetings`

Các tham số query đều tùy chọn. Giá trị rỗng tương đương không gửi; mỗi tham số chỉ được gửi một
lần. Tất cả bộ lọc được kết hợp bằng AND (BR-30/BR-31).

| Tham số | Quy tắc |
|---|---|
| `status` | Tên enum PascalCase trong chín `MeetingStatus` |
| `sensitivity` | `Normal`, `Internal`, `Confidential` hoặc `Secret` |
| `q` | Trim; tìm chuỗi con của `title` không phân biệt hoa/thường. `%`, `_`, `\` là ký tự thường, không phải wildcard |
| `from`, `to` | ISO 8601 có `Z` hoặc offset; lọc `startsAt` trong khoảng UTC, gồm cả hai đầu; có thể chỉ gửi một đầu |
| `page`, `pageSize` | Mặc định `1`, `20`; `page ≥ 1`, `pageSize` từ `1` đến `100` |
| `sortBy` | `title`, `startsAt`, `createdAt`; mặc định `startsAt` |
| `sortDir` | `asc`, `desc`; mặc định `asc` cho `title`, `desc` cho hai cột thời gian |

Khi nhiều dòng có cùng giá trị sort, xếp tiếp theo `id asc`. Ví dụ:
`GET /api/meetings?status=Published&sensitivity=Internal&q=giao&from=2026-10-01T00%3A00%3A00Z&page=2&pageSize=20&sortBy=startsAt&sortDir=desc`.
Đổi trang phải gửi lại bộ lọc và sort hiện tại (BR-32). FE giữ toàn bộ tiêu chí trên URL của màn
danh sách (`/mgmt/meetings?q=…&status=…&page=…`), nên tải lại trang, chia sẻ đường dẫn hay quay lại từ
trang chi tiết đều giữ nguyên bộ lọc và trang đang xem.

Khi gửi timezone có dấu `+` trong query, mã hóa dấu này thành `%2B`, ví dụ
`from=2026-10-01T09%3A00%2B07%3A00`. Phần giây có thể bỏ qua; nếu có giây, phần thập phân
tối đa bảy chữ số. Timestamp thiếu timezone trả `400 MSG32`.

`200 OK` trả:

```json
{
  "items": [
    {
      "id": "dc36f21a-c030-4ddb-a6ec-9d1254230001",
      "title": "Giao ban tuần",
      "startsAt": "2026-10-01T02:00:00+00:00",
      "createdAt": "2026-09-25T02:00:00+00:00",
      "status": "Published",
      "sensitivity": "Internal"
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
`MSG32`, `message` là `Tham số truy vấn không hợp lệ.` và `errors` theo tên tham số.
Bao gồm enum/sort lạ, thời gian thiếu offset, `from > to`, trang hoặc kích thước trang ngoài giới
hạn. Lỗi truy xuất database, kể cả ở bước kiểm tra tài khoản còn hoạt động, trả `500` với
`code: MSG38` và thông điệp
`Không thể tải danh sách cuộc họp. Vui lòng thử lại.` như EX-1 của UC-14.

Sequence UC-14 hiện ghi BR-29 và BR-32 ở bước repository; BA cần bổ sung BR-30/31 và nhánh query
sai `400` vào diagram/spec. Đây là nhánh validation của HTTP, không thay đổi hai nhánh truy xuất
thành công/thất bại trong diagram hiện tại.

### GET `/api/meetings/approvers`

Trả danh sách tài khoản đang hoạt động có capability `MeetingApprover`, sắp xếp theo tên. Response
chỉ gồm `id` và `fullName`; endpoint kế thừa gate `MeetingCreator`. Nếu hệ thống không đọc được
danh sách do lỗi database hoặc timeout, trả `503 Service Unavailable` với
`{ "code": "MSG61", "message": "Không thể tải danh sách người duyệt. Vui lòng thử lại." }`.

### POST `/api/meetings`

Request body của POST và PUT có các field:

```json
{
  "title": "Giao ban tuần",
  "startsAt": "2026-10-01T09:00:00+07:00",
  "endsAt": "2026-10-01T10:30:00+07:00",
  "mode": "InPerson",
  "location": "Phòng họp A",
  "sensitivity": "Internal",
  "description": "Thảo luận kế hoạch tuần."
}
```

`title` và `startsAt` là bắt buộc. `endsAt` có thể `null` hoặc bị bỏ qua;
nếu có giá trị, nó phải lớn hơn `startsAt`. Nếu `mode` hoặc `sensitivity` bị bỏ qua, service dùng
giá trị khởi tạo của form (`InPerson`, `Normal`). Body không gán approver; `assignedApproverId`
nếu được gửi sẽ không tạo assignment.

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm thông tin cuộc họp, `assignedApproverId`, `status`, `createdById`,
`createdAt`, `updatedAt`; `assignedApproverId` là `null` khi meeting ở `Draft`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope. `mode`, `sensitivity` và `status` là chuỗi
enum. Thời gian trong response luôn UTC.

### PUT `/api/meetings/{id}`

PUT thay toàn bộ bảy field thông tin cuộc họp bằng dữ liệu request. Trả `200 OK`, envelope
`{ "code": "MSG30", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Body không gán approver. Meeting `Draft` và `Rejected` đều được sửa trực tiếp.
PUT không chuyển trạng thái; khi sửa `Rejected`, người duyệt hiện tại và rejection history/reason
được giữ nguyên. Muốn gửi duyệt lại, Organizer thực hiện `ReturnToDraft`, rồi `SubmitForApproval`
với người duyệt được chọn cho cycle mới.

### POST `/api/meetings/{id}/transitions` — SCRUM-19

Các endpoint SCRUM-19 trong mục này là contract đã chốt để triển khai và bàn giao kiểm thử; chúng
không tự khẳng định code đã được merge hay đạt Backend Ready.

Endpoint nhận một action, không nhận actor, source/target status hoặc timestamp từ client:

| Action | Body bổ sung | Transition |
|---|---|---|
| `SubmitForApproval` | `assignedApproverId` bắt buộc | Draft → WaitingApproval |
| `Approve` | Không | WaitingApproval → Approved |
| `Reject` | `reason` bắt buộc, sau trim 1–1.000 ký tự | WaitingApproval → Rejected |
| `ReturnToDraft` | Không | Rejected → Draft |
| `Publish` | Không | Approved → Published |

`ReturnToDraft` chỉ do Organizer thực hiện, tạo đúng một lifecycle history, giữ nguyên rejection
history/reason cũ, xóa current assignment và không tạo invitation. Organizer có thể sửa thông tin
trước khi ReturnToDraft (ở Rejected) hoặc sau đó (ở Draft), rồi submit lại bằng flow bình thường;
`Rejected → WaitingApproval` trực tiếp không hợp lệ. Cycle mới có thể chọn một current Assigned
Approver khác.

Không cho Organizer tự duyệt ở mọi mức độ mật: `assignedApproverId == CreatedById` khi submit trả
`400 MSG52`, kể cả Organizer là Chair và có cả hai capability. Assignment cũ trỏ về Organizer cũng
không được Approve/Reject (`403 MSG55`); không tự sửa dữ liệu/ghi thêm history khi từ chối action.

Giữ rule không rút `WaitingApproval` về Draft và không đổi approver trong trạng thái này.
`ReturnToDraft` vẫn chỉ nhận `Rejected`; gọi khi `WaitingApproval` trả `409 MSG64` mà không ghi
history/jobs. Nếu approver mất quyền hoặc inactive, cuộc họp tiếp tục chờ duyệt; cần khôi phục
quyền/active của người được gán qua quản trị hoặc team chốt một flow mới ở ticket riêng. Không
được bypass bằng PUT, submit lại, Chair relationship hay chỉnh assignment trực tiếp.

Transition thành công trả `200 OK` với `{ code, message, meeting, history }`. Code thành công theo
action lần lượt là `MSG48` (SubmitForApproval), `MSG43` (Approve), `MSG44` (Reject), `MSG49`
(ReturnToDraft) và `MSG45` (Publish). FE nên dùng `code` để xử lý tình huống; `message` chỉ dành
cho hiển thị.

Publish snapshot recipient bên trong transaction và persist notification/invitation jobs từ chính
snapshot đó. Theo BR-54, snapshot chỉ gồm participant có user active và `Response != Delegated`;
row đã Delegated không nhận invitation, và API không tự thêm người được ủy quyền chỉ từ
`DelegatedToUserId`. Người đó vẫn đủ điều kiện nếu có participant row riêng, active và chưa Delegated.
Thay đổi participant sau commit không sửa batch đã tạo. Transition và toàn bộ notification/outbox
rows được commit cùng nhau; HTTP 200 xác nhận email job đã được xếp hàng, không xác nhận email đã
được gửi. Worker SCRUM-15 xử lý gửi bất đồng bộ và quản lý retry/failure theo contract của worker.
Subject thư mời tối đa 300 ký tự UTF-16 và được rút gọn không tách đôi surrogate pair; tiêu đề đầy
đủ trong payload snapshot không bị thay đổi.
Các writer participant thuộc SCRUM-20 phải dùng cùng meeting-row lock/serialization protocol để một
lần Publish và thay đổi participant đồng thời có ranh giới snapshot xác định.

### GET `/api/meetings/{id}/history` — SCRUM-19

Trả lifecycle history theo `occurredAt ASC`, rồi `id ASC`, có pagination. Mỗi item tối thiểu có
`id`, `meetingId`, `action`, `fromStatus`, `resultingStatus`, `actorId`, `actorName`, `occurredAt`,
`rejectionReason` và snapshot `assignedApproverId` khi action là SubmitForApproval.

Meeting Organizer và current Assigned Approver đọc được toàn bộ history và rejection reason.
Participant có quyền đọc meeting từ Published trở đi nhận timeline với `rejectionReason: null`;
participant relationship không cấp quyền action. Former approver không có quyền chỉ nhờ assignment
trước đây, nhưng một quan hệ participant độc lập vẫn cho timeline đã redacted sau Publish. API không
có endpoint update/delete history; các row là immutable audit evidence.
GET history đọc meeting bằng truy vấn không tracking, không dùng `FOR UPDATE`. Một transaction đang
khóa meeting để PUT/transition không làm request history phải chờ khóa ghi; GET chỉ đọc dữ liệu đã commit.
Query `page`/`pageSize` mặc định `1`/`20`; `page >= 1`, `pageSize` từ `1` đến `100`. Trả `200`
với `{ items, page, pageSize, totalCount, totalPages }`; trang vượt cuối trả `items: []`, kể cả
khi offset lớn hơn `Int32.MaxValue`. Repository kiểm tra tổng số dòng trước khi ép offset sang `int`.
Nếu offset vượt `Int32.MaxValue` nhưng vẫn nằm trong tập history hiện có, trả `400 MSG56`
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
để Approve/Reject, rồi dùng Organizer để ReturnToDraft hoặc Publish theo trạng thái hiện tại.
Publish tạo ba invitation jobs `Queued`, không xác nhận email đã gửi. Có thể kiểm tra history và
notification bằng API; participant chỉ thấy meeting sau Publish và reason được redacted.

Seeder chỉ chèn meeting này một lần khi đủ tài khoản demo; trên database Development cũ có các
tài khoản đó, khởi động lại app sẽ thêm demo còn thiếu. Các lần khởi động sau không reset status,
agenda, participant, assignment hay history đã thao tác. Không tự tái tạo một Draft sau khi demo
được Publish; muốn chạy lại từ đầu, dùng một database Development riêng mới.

### GET `/api/meetings/{id}/submission-readiness` — SCRUM-19

Đọc readiness từ cùng validator mà SubmitForApproval gọi lại trong transaction. Meeting chỉ ready
khi mandatory meeting information và required agenda information đầy đủ; agenda required không
được rỗng. Endpoint chỉ đọc, không đổi status/assignment hoặc tạo history/notification. `200 OK`
trả `{ meetingId, status, ready, errors }`; dữ liệu chưa ready là `ready: false` cùng map lỗi, không
phải HTTP error. Chỉ Organizer có `MeetingCreator` được gọi khi meeting ở `Draft`.

Readiness áp dụng rule hiện tại trên dữ liệu đã persist; validator Meeting dùng lại
`MeetingRules`, validator Agenda thuộc `Domain.Agenda`. Agenda phải có ít nhất một item. Bảng dưới
được suy ra từ entity/configuration `Meeting` và `AgendaItem`; field nullable/không bắt buộc không
được biến thành điều kiện submit mới.

| Entity.field | Điều kiện khi submit | Schema/rule |
|---|---|---|
| `Meeting.Title` | Bắt buộc sau trim, 1–300 ký tự | `required`, EF `IsRequired`, max 300 |
| `Meeting.StartsAt` | Bắt buộc và hợp lệ | `DateTimeOffset` non-null; request tạo/sửa phải có ISO 8601 với zone |
| `Meeting.EndsAt` | Tùy chọn; nếu có phải sau `StartsAt` | Nullable theo entity/BR-25 |
| `Meeting.Mode`, `Meeting.Sensitivity` | Phải là enum đã định nghĩa | Non-null, lưu chuỗi |
| `Meeting.Location` | Không bắt buộc; tối đa 300 ký tự | Không thêm yêu cầu theo mode |
| `Meeting.Description` | Không bắt buộc; tối đa 4.000 ký tự | Chuỗi optional về nghiệp vụ |
| `AgendaItems` | Có ít nhất một item | Agenda là required cho submit |
| `AgendaItem.Position` | Không thêm điều kiện readiness | `int` non-null; index không unique để hỗ trợ reorder |
| `AgendaItem.Title` | Bắt buộc sau trim, 1–300 ký tự | `required`, EF `IsRequired`, max 300 |
| `AgendaItem.Description` | Không bắt buộc; tối đa 4.000 ký tự | EF max 4000 |
| `AgendaItem.PlannedDurationMinutes` | Không thêm điều kiện readiness | Schema `int` không có constraint lớn hơn 0 |
| `AgendaItem.PresenterId` / `PresenterName` | Không bắt buộc; tên khách sau trim tối đa 200 | Nullable; không thêm yêu cầu đúng một presenter |
| `AgendaItem.Status` | Phải là enum hợp lệ | Không ép `Pending` khi submit |
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
| 400 | `MSG50` | JSON/action/GUID/field sai hoặc field không áp dụng cho action |
| 400 | `MSG51` | Submit thiếu meeting/agenda information; trả `errors` theo field |
| 400 | `MSG52` | Approver không còn active, thiếu `MeetingApprover` hoặc là chính Organizer |
| 400 | `MSG53` | Reject không có reason hoặc reason chỉ có whitespace |
| 400 | `MSG54` | Reason sau trim dài hơn 1.000 ký tự |
| 400 | `MSG56` | Query pagination history không hợp lệ |
| 401 | — | Chưa đăng nhập |
| 403 | `MSG55` | Đọc được meeting nhưng không phải actor/relationship/capability của action, hoặc assignment cũ cho Organizer tự duyệt |
| 403 | — | Tài khoản inactive theo active-user gate |
| 404 | — | Meeting không tồn tại hoặc caller không được phép đọc/action trên tài nguyên; body rỗng |
| 409 | `MSG41`, `MSG46`, `MSG63`, `MSG64` | Submit, Publish, quyết định duyệt hoặc ReturnToDraft không còn hợp lệ; không ghi history/jobs |
| 413 | `MSG58` | JSON request vượt 64 KiB |
| 415 | `MSG34` | POST không dùng `application/json`; dùng chung code với PUT |
| 503 | `MSG42`, `MSG47`, `MSG59` | Lỗi database/transaction khi transition; transaction rollback |
| 503 | `MSG60`, `MSG62` | Không tải được readiness/history hoặc không kiểm tra được tài khoản active |

`GET submission-readiness` trả `409 MSG41` nếu meeting không còn ở `Draft`;
history/readiness trả `404` nếu caller không được phép xem resource. Retry action sau khi commit thường
trả `409` do state đã đổi. Client nên tải lại meeting/history để xác định kết quả đã commit, và không
được suy diễn rằng `409` có nghĩa lần gọi trước thất bại.

### Ranh giới tích hợp

SCRUM-19 ghi database notification cho approver/participants và durable email jobs trong cùng
transaction với transition. Email được gửi sau commit bởi worker của SCRUM-15; cho đến khi worker đó
được tích hợp và kiểm thử, response Publish chỉ xác nhận meeting đã Published và jobs đã xếp hàng.
Database notification rows được tạo ở đây; realtime push theo SCRUM-14 là phần tích hợp riêng.
Recipient filter đã chốt theo BR-54 và được gom thành shared policy để SCRUM-20 dùng lại. Writer
participant/agenda thuộc các story tương ứng phải tham gia meeting-row lock protocol trước khi có thể
xác nhận concurrency test của snapshot/readiness; update sau commit không đổi rows đã persist.

Các rule agenda về thứ tự liên tục, thời lượng dương, presenter bắt buộc và chỉ cho `Pending` thuộc
SCRUM-25/BA; SCRUM-19 không tự chốt các rule đó trước. Readiness vẫn bắt buộc agenda không rỗng,
title và giới hạn field theo schema thông qua validator sở hữu bởi domain Agenda.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
| `startsAt` | Bắt buộc, ISO 8601 có `Z` hoặc offset rõ ràng |
| `endsAt` | Tùy chọn; nếu có, ISO 8601 có `Z` hoặc offset rõ ràng và phải lớn hơn giờ bắt đầu |
| `mode` | Một trong `InPerson`, `Online`, `Hybrid`; mặc định `InPerson` nếu bỏ qua |
| `sensitivity` | Một trong `Normal`, `Internal`, `Confidential`, `Secret`; mặc định `Normal` nếu bỏ qua |
| `location` | Tùy chọn; trim; tối đa 300 ký tự |
| `description` | Tùy chọn; tối đa 4.000 ký tự; giữ xuống dòng |

Thời gian được chuyển UTC trước khi lưu và trả UTC ở response. Khi tích hợp, FE có thể hiển thị/nhập
giờ local của thiết bị. Không kiểm tra giờ quá khứ, trùng lịch hoặc trùng tên vì yêu cầu hiện tại chưa quy định các luật
này.

Lỗi validation trả `400`:

```json
{
  "code": "MSG31",
  "message": "Thông tin cuộc họp không hợp lệ.",
  "errors": {
    "title": "Tên cuộc họp không được để trống.",
    "endsAt": "Thời gian kết thúc phải sau thời gian bắt đầu."
  }
}
```

Endpoint trả đồng thời mọi lỗi field phát hiện được. Với PUT, hệ thống khóa meeting và kiểm tra
trạng thái trước khi parse JSON: nếu meeting không còn sửa được thì trả `409 MSG33`, kể cả body JSON
sai cú pháp hoặc sai kiểu. Request PUT có Content-Type thiếu hoặc không hỗ trợ trả `415` với code
`MSG34`. Với meeting còn sửa được và Content-Type hợp lệ, JSON sai cú
pháp/sai kiểu trả `400` và code `MSG31` không có `errors`. Body PUT lớn hơn
64 KiB trả `413 MSG58`. POST JSON lỗi cũng trả `400`.

## Mã phản hồi

| HTTP | Code | Ý nghĩa |
|---:|---|---|
| 201 | `MSG29` | Tạo nháp thành công |
| 200 | `MSG30` | Sửa thông tin thành công |
| 400 | `MSG31` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 400 | `MSG32` | Query danh sách không hợp lệ; có `errors` theo tham số |
| 401 | — | Chưa đăng nhập |
| 403 | — | Không có `MeetingCreator` theo gate hai lớp |
| 404 | — | GET: không tồn tại hoặc caller không có quan hệ; PUT: không tồn tại hoặc caller không phải người tạo; body rỗng |
| 409 | `MSG33` | Trạng thái hiện tại không cho sửa |
| 413 | `MSG58` | Body PUT vượt quá 64 KiB |
| 415 | `MSG34` | PUT không gửi JSON hoặc Content-Type không hỗ trợ |
| 500 | `MSG35` | Không thể lưu khi tạo; thử lại |
| 500 | `MSG36` | Không thể lưu khi sửa; thử lại |
| 500 | `MSG37` | Không tải được chi tiết; thử lại |
| 500 | `MSG38` | Không tải được danh sách; thử lại |

Mã thuộc dải chung của dự án, xem [danh mục mã](00-danh-muc-ma-thong-bao.md). Các lỗi persistence
của CRUD/list trả `500`; lỗi workflow SCRUM-19 giữ contract `503` để client nhận biết lỗi tạm thời
của transaction hoặc capability lookup.

## Persistence và transaction

Cột `ends_at` nullable và FK nullable `assigned_approver_id` giữ được các meeting cũ; Draft không có
current assignment. POST lưu meeting `Draft` không assignment và một
dòng `MeetingStatusHistory` (`FromStatus = null`, `ToStatus = Draft`, `ActorId = creator`). EF Core
lưu các thay đổi này trong cùng transaction.

PUT kiểm tra quyền sở hữu và trạng thái hiện tại bằng truy vấn không khóa trước khi chờ body. Sau khi
xác nhận meeting còn sửa được, hệ thống kiểm tra Content-Type rồi đọc body vào buffer ngoài
transaction khóa ghi. Body bị giới hạn 64 KiB ngay cả khi không có `Content-Length`; quá giới hạn
thì ngừng đọc và trả `413`, không tạo thêm bản sao buffer. Khi xử lý cập nhật, transaction
`ReadCommitted` khóa đúng meeting row bằng
PostgreSQL `FOR UPDATE`, đọc lại trạng thái và trả `409 MSG33` nếu trạng thái đổi trong lúc body đang
được gửi. Chỉ sau lần kiểm tra khóa này hệ thống mới parse JSON, validate và lưu; không có network
read trong lúc giữ row lock.
Transaction kết thúc trước response. Cập nhật thông tin không thay status hoặc lịch sử. Nếu body sau
chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng không ghi DB hoặc đổi `updatedAt`, kể cả ở
`Rejected` có current approver. Dữ liệu cho
phép sửa giữa hai form cũ dùng “lần ghi sau cùng thắng”; contract chưa có ETag/version để từ chối
stale content.

## Hợp đồng tích hợp FE

- Gọi API qua client hiện có bằng đường dẫn tương đối `/api/...` để tự mang token và refresh phiên.
- Khi nhận `403`, xử lý như quyền hệ thống đã thay đổi; GET chi tiết trả `404` nếu caller không có
  quan hệ với meeting.
- Chỉ hiển thị thao tác sửa cho người có `MeetingCreator`, đúng `createdById`, và trạng thái
  `Draft` hoặc `Rejected`. Backend kiểm tra lại quyền sở hữu và trạng thái khi PUT.
- Nếu PUT trả `409 MSG33`, hiển thị nội dung lỗi và tải lại trạng thái hiện tại trước khi cho sửa tiếp.
- Sau khi POST hoặc PUT thành công, FE hiển thị message và mở chi tiết `meeting.id`. Danh sách
  SCRUM-18 đã tích hợp lọc, sort và pagination server-side; tiêu chí được giữ trên URL.
- Trước khi gửi duyệt, lấy danh sách bằng `GET /api/meetings/approvers` rồi gửi `assignedApproverId`
  trong action `SubmitForApproval`; trường này không thuộc POST/PUT tạo hoặc sửa thông tin meeting.
- SCRUM-19 cung cấp contract history ở trên; việc tích hợp timeline thật thuộc task FE riêng.
- `src/api/meetings.ts` đã có `transitionMeeting`, `getMeetingSubmissionReadiness` và
  `getMeetingHistory`; các request/response type tương ứng nằm trong `src/types/meeting.ts`.
- FE mở form và nút chỉnh sửa ở cả `Draft` và `Rejected` theo contract PUT. Sửa nội dung ở
  Rejected giữ nguyên trạng thái, assignment và rejection history; gửi duyệt lại vẫn cần
  `ReturnToDraft` trước `SubmitForApproval`.

## Checklist BA cần hoàn tất

- Đồng bộ nội dung BR-24–BR-29 chính thức vào tài liệu BA và map từng luật vào validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Đồng bộ hai quy tắc đã ghi trong UC-11/UC-12 vào Jira SCRUM-17, cùng giới hạn length và format
  timezone khi có quyền cập nhật Jira.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Kiểm tra lại định danh UC theo convention Jira để tránh nhầm UC-11/UC-12 quản lý vai trò.
