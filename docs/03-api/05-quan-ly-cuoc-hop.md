# 05 · API quản lý cuộc họp

**UC-15 Create Meeting / UC-16 Update Meeting — SCRUM-17; UC-17 View Meeting Details /
UC-18 View Meeting List — SCRUM-18; UC-19 Submit / UC-20 Review / UC-21 Publish — SCRUM-19.**

## Phạm vi

Các endpoint cho người tạo khởi tạo cuộc họp nháp và sửa thông tin cấp cuộc họp trước khi gửi duyệt.
Meeting-level information gồm tên, hình thức, địa điểm, mức độ mật và mô tả; lịch dự kiến thuộc
Meeting Sessions theo BR-25. Tạo/sửa Meeting không chọn hoặc gán người duyệt; việc chọn và gán người
duyệt diễn ra trong UC-19. Cập nhật Meeting không làm thay đổi trạng thái vòng đời (BR-28).
Người có quan hệ hợp lệ với cuộc họp có thể đọc chi tiết và lấy trang danh sách. `GET /api/meetings`
được bổ sung trong SCRUM-18. Màn danh sách, tạo, sửa và chi tiết đều gọi API thật: tạo xong thì báo
thành công và mở trang chi tiết của cuộc họp mới. FE đã bỏ dropdown người duyệt khỏi form tạo/sửa;
gửi duyệt, duyệt, từ chối và lịch sử trạng thái nằm trên trang chi tiết — xem
[Hợp đồng tích hợp FE](#hợp-đồng-tích-hợp-fe).

Meeting creation yêu cầu các thông tin cấp Meeting theo BR-24; không bao gồm Planned Start Time hay
Planned End Time. Meeting Session được lập lịch riêng theo UC-26/UC-27. Form khởi tạo Hình thức là
`InPerson` và Mức độ mật là `Normal`.

## Quyền truy cập

POST, `GET /api/meetings/approvers` và PUT yêu cầu capability `MeetingCreator`; cổng đọc claim và
xác minh lại capability từ database theo SCRUM-12, không kiểm theo tên vai trò. GET chi tiết yêu cầu
đăng nhập và tài khoản còn hoạt động, sau đó kiểm tra quan hệ cuộc họp theo BR-29.

Quyền đọc theo BR-29, đã được BA chốt: người tạo đọc mọi trạng thái; người duyệt hiện được gán đọc từ
`WaitingApproval` trở đi; người được gán vào meeting với vai trò `Chair`, `Secretary`, `Member` hoặc
`Guest` đọc từ `Published` trở đi. Trạng thái lời mời không làm mất quyền xem meeting đã công bố.
Vì vậy `Draft` chỉ người tạo thấy; trước khi công bố, thành phần tham dự chưa đọc được. Không đủ quyền
đọc thì trả `404` rỗng, giống ID không tồn tại. PUT vẫn chỉ cho người tạo/người tổ chức sửa; ID không tồn
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
| SubmitForApproval | Meeting Organizer (`CreatedById`) + `MeetingCreator`; meeting Draft hoặc Rejected và ready | Không |
| ReturnToDraft | Meeting Organizer active + `MeetingCreator`; Rejected hoặc WaitingApproval có assignment không hợp lệ (thiếu người duyệt, trỏ về Organizer, inactive hoặc mất `MeetingApprover`) | Không |
| Approve/Reject | Current `AssignedApproverId` + capability `MeetingApprover`, khác Organizer; meeting WaitingApproval | Không; chỉ được nếu Chair đồng thời là current Assigned Approver và không phải Organizer |
| Publish | Meeting Organizer + `MeetingCreator`; meeting Approved; đúng một Chair active và ít nhất một Secretary active. Invitation response không ảnh hưởng readiness; Meeting Session là tùy chọn theo BR-56 | Không; chỉ được nếu Chair đồng thời là Organizer |
| TransferOrganizer | Actor active có `AdminUsers`; meeting Approved và Organizer hiện tại inactive; Organizer mới active có `MeetingCreator`, khác người cũ và current Assigned Approver | Không |
| Đọc full lifecycle history/rejection reason | Meeting Organizer hoặc current Assigned Approver | Chair relationship riêng chỉ cấp timeline đã redacted |
| Đọc history/rejection reason của Secretary | Participant của chính meeting có role Secretary, user active, chưa ủy quyền (`Response != Delegated`, `DelegatedToUserId = null`) | Không; Secretary không được xem snapshot Organizer cũ/mới nếu không đồng thời có full-history permission |
| Đọc timeline redacted | Chair và Member từ Published; mọi quan hệ đọc hợp lệ khác không có full-history permission | Không có rejection reason hoặc organizer-transfer metadata |

Người từng là Assigned Approver không giữ quyền đọc history chỉ vì assignment cũ xuất hiện trong
một history row. Authorization dùng assignment hiện tại; snapshot cũ là bằng chứng kiểm toán,
không phải access grant. Organizer/current Assigned Approver được đọc full history ở mọi lifecycle
status (Assigned Approver không được đọc Draft). Secretary được gán hợp lệ có thể đọc history/lý do
từ chối trước Published theo ngoại lệ F08 ghi trong tài liệu Business Rules. Chair được gán chỉ đọc
chi tiết/danh sách/timeline từ Published và không thấy rejection reason hoặc transfer IDs.
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
| `status` | Tên enum PascalCase trong chín `MeetingStatus` |
| `sensitivity` | `Normal`, `Internal`, `Confidential` hoặc `Secret` |
| `q` | Trim; tìm chuỗi con của `title` không phân biệt hoa/thường. `%`, `_`, `\` là ký tự thường, không phải wildcard |
| `from`, `to` | ISO 8601 có `Z` hoặc offset; giây tùy chọn, ví dụ `2026-10-01T09:00+07:00`; chuẩn hóa UTC và lọc theo lịch dự kiến của Meeting Sessions không bị hủy, gồm cả hai đầu; có thể chỉ gửi một đầu. Meeting không có Meeting Session phù hợp không khớp Time Range (BR-30) |
| `page`, `pageSize` | Mặc định `1`, `20`; `page` từ `1` đến `2.147.483.647`, `pageSize` từ `1` đến `100` |
| `sortBy` | `title`, `createdAt` |
| `sortDir` | `asc`, `desc` |

Khi nhiều dòng có cùng giá trị sort, xếp tiếp theo `id asc`. Ví dụ:
`GET /api/meetings?status=Published&sensitivity=Internal&q=giao&from=2026-10-01T00%3A00%3A00Z&page=2&pageSize=20&sortBy=createdAt&sortDir=desc`.
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
`MSG32`, `message` là `Tiêu chí danh sách cuộc họp không hợp lệ.` theo catalogue; backend hiện trả
`Tham số truy vấn không hợp lệ.` nên cần sửa code để khớp catalogue. `errors` liệt kê theo tên tham số.
Bao gồm enum/sort lạ, thời gian thiếu offset, `from > to`, trang hoặc kích thước trang ngoài
giới hạn. Lỗi truy xuất database, kể cả ở bước kiểm tra tài khoản còn hoạt động, trả `500` với
`code: MSG38` và thông điệp
`Không thể tải danh sách cuộc họp. Vui lòng thử lại.` như EX-1 của UC-18.

Sequence UC-18 hiện ghi BR-29 và BR-32 ở bước repository; BA cần bổ sung BR-30/31 và nhánh query
sai `400` vào diagram/spec. Đây là nhánh validation của HTTP, không thay đổi hai nhánh truy xuất
thành công/thất bại trong diagram hiện tại.

### GET `/api/meetings/approvers`

Trả danh sách tài khoản đang hoạt động có capability `MeetingApprover`, loại người đang gọi API,
chỉ gồm `id` và `fullName`; endpoint phục vụ bước chọn người duyệt của UC-19 và kế thừa gate
`MeetingCreator`. Danh sách sắp xếp theo tên. Nếu hệ thống không đọc được danh sách do lỗi database
hoặc timeout, trả `503 Service Unavailable` với `{ "code": "MSG130", "message":
"Không thể tải danh sách người duyệt. Vui lòng thử lại." }`.

### POST `/api/meetings`

Request body của POST và PUT có các thông tin cấp Meeting; không có lịch phiên họp hoặc người duyệt:

```json
{
  "title": "Giao ban tuần",
  "mode": "InPerson",
  "location": "Phòng họp A",
  "sensitivity": "Internal",
  "description": "Thảo luận kế hoạch tuần."
}
```

`title` là bắt buộc theo BR-24. Người duyệt được chọn và gán trong UC-19 trước khi chuyển meeting sang
`WaitingApproval`. Nếu `mode` hoặc
`sensitivity` bị bỏ qua, service dùng giá trị khởi tạo của form (`InPerson`, `Normal`).

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm thông tin cuộc họp, `status`, `createdById`,
`createdAt`, `updatedAt`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope. Meeting object không có Planned Start Time
hoặc Planned End Time; lịch thuộc Meeting Sessions. `mode`, `sensitivity` và `status` là chuỗi enum.

### PUT `/api/meetings/{id}`

PUT cập nhật thông tin cấp cuộc họp bằng dữ liệu request. Trả `200 OK`, envelope
`{ "code": "MSG30", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Body không gán approver. Meeting `Draft` và `Rejected` đều được sửa trực tiếp.
PUT không chuyển trạng thái; khi sửa `Rejected`, người duyệt hiện tại và rejection history/reason
được giữ nguyên. Muốn gửi duyệt lại, Organizer gọi trực tiếp `SubmitForApproval` từ `Rejected`
với người duyệt được chọn cho cycle mới; không bắt buộc `ReturnToDraft`.

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
| `Publish` | Không | Approved → Published; Meeting Session là tùy chọn theo BR-56 |
| `TransferOrganizer` | `organizerId` GUID khác rỗng, bắt buộc | Approved → Approved; đổi Organizer khi người cũ inactive |

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
history qua GET chỉ vì có AdminUsers. Participant timeline ẩn hai field Organizer snapshot này cùng
rejection reason. Quyền Organizer cũ kết thúc sau transfer; participant relationship riêng vẫn áp dụng.

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
ảnh hưởng readiness. Meeting Session là tùy chọn: meeting Approved có không, một hoặc nhiều phiên
đều có thể được công bố theo BR-56; Publish không dùng lịch phiên làm điều kiện chuyển trạng thái.

Publish tạo notification theo BR-54: chỉ participant có user active và `Response != Delegated`;
không tự thêm người được ủy quyền chỉ từ `DelegatedToUserId`. Delegate cần participant row riêng
hợp lệ để nhận notification. Email dùng producer SCRUM-15: gọi
`QueueForPublishedMeetingAsync(meetingId)` sau khi transaction công bố đã commit; producer đọc
meeting đã Published và chọn các participant có user active, không lọc `Response`.
Producer lưu `ToAddress`, `Subject`, `Body` và `CalendarAttachment` khi tạo job; thay đổi dữ liệu
sau commit không sửa nội dung job đã lưu. Khi SMTP được bật, job mới có trạng thái `Queued`;
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
Seeder không tự thêm Meeting Session; việc không có phiên không chặn Publish theo BR-56.
Khi bật SMTP, Publish thành công sẽ tạo ba invitation jobs
`Queued`, không xác nhận email đã gửi. Có thể kiểm tra history/notification bằng API; participant khác
Secretary đọc được rejection reason theo ngoại lệ F08; Chair và Member chỉ thấy meeting từ Published.

Seeder chỉ chèn meeting này một lần khi đủ tài khoản demo; trên database Development cũ có các
tài khoản đó, khởi động lại app sẽ thêm demo còn thiếu. Các lần khởi động sau không reset status,
agenda, participant, assignment hay history đã thao tác. Không tự tái tạo một Draft sau khi demo
được Publish; muốn chạy lại từ đầu, dùng một database Development riêng mới.

### GET `/api/meetings/{id}/submission-readiness` — SCRUM-19

Đọc readiness từ cùng validator mà SubmitForApproval gọi lại trong transaction. Meeting chỉ ready
khi thông tin bắt buộc ở cấp meeting đầy đủ (UC-19/BR-33). Không thêm Agenda-required rule vì UC-19
chưa yêu cầu; Meeting Session không bắt buộc lúc gửi duyệt theo BR-55. Endpoint chỉ đọc,
không đổi status/assignment hoặc tạo history/notification. `200 OK`
trả `{ meetingId, status, ready, errors }`; dữ liệu chưa ready là `ready: false` cùng map lỗi, không
phải HTTP error. Chỉ Organizer có `MeetingCreator` được gọi khi meeting ở `Draft` hoặc `Rejected`.

Readiness áp dụng rule hiện tại trên dữ liệu đã persist; validator Meeting dùng lại
`MeetingRules`. Bảng dưới được suy ra từ entity/configuration `Meeting`; field nullable/không bắt buộc không
được biến thành điều kiện submit mới.

| Entity.field | Điều kiện khi submit | Schema/rule |
|---|---|---|
| `Meeting.Title` | Bắt buộc sau trim, 1–300 ký tự | `required`, EF `IsRequired`, max 300 |
| `Meeting.StartsAt` | Bắt buộc và hợp lệ | `DateTimeOffset` non-null; request tạo/sửa phải có ISO 8601 với zone |
| `Meeting.EndsAt` | Tùy chọn; nếu có phải sau `StartsAt` | Nullable theo entity/BR-25 |
| `Meeting.Mode`, `Meeting.Sensitivity` | Phải là enum đã định nghĩa | Non-null, lưu chuỗi |
| `Meeting.Location` | Không bắt buộc; tối đa 300 ký tự | Không thêm yêu cầu theo mode |
| `Meeting.Description` | Không bắt buộc; tối đa 4.000 ký tự | Chuỗi optional về nghiệp vụ |
| Agenda, Meeting Sessions | Không bắt buộc khi submit hoặc Publish | Validation thuộc các story quản lý agenda/phiên; BR-56 xác định session là tùy chọn |
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
thư/.ics, `MeetingId` và trạng thái delivery vẫn giữ. `Notification.OriginHistoryId` không thay đổi.
Database notification rows được tạo ở đây; realtime push theo SCRUM-14 là phần tích hợp riêng.
Recipient filter của notification đã chốt theo BR-54 và được gom thành shared policy để SCRUM-20 dùng lại. Writer
participant thuộc story tương ứng phải khóa meeting cha trong cùng transaction trước khi ghi thay đổi;
protocol này bảo vệ snapshot người nhận khi Publish đồng thời.
Update sau commit không đổi rows đã persist.

Validation agenda thuộc SCRUM-25/BA; SCRUM-19 không dùng agenda để chặn gửi duyệt.
Model/migration Meeting Session đã có để chuẩn bị cho UC-26–30; API tạo/sửa/hủy/bắt đầu/kết thúc
phiên vẫn thuộc phần quản lý phiên họp. Migration không tự sinh lịch cho dữ liệu cũ và Publish không
phụ thuộc vào số lượng phiên theo BR-56.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
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
| 200 | `MSG30` | Sửa thông tin thành công |
| 400 | `MSG31` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 400 | `MSG32` | Query danh sách không hợp lệ; có `errors` theo tham số |
| 401 | — | Chưa đăng nhập |
| 403 | — | Không có `MeetingCreator` theo gate hai lớp |
| 404 | — | GET: không tồn tại hoặc caller không có quan hệ; PUT: không tồn tại hoặc caller không phải người tạo; body rỗng |
| 409 | `MSG33` | Trạng thái hiện tại không cho sửa |
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
thực hiện trong UC-19. Planned Start Time và Planned End Time được quản lý riêng bởi MeetingSession.
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
Transaction kết thúc trước response. Cập nhật thông tin không thay status hoặc lịch sử. Nếu body sau
chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng không ghi DB hoặc đổi `updatedAt`. PUT không
xóa assignment hiện có, kể cả dữ liệu Draft cũ hoặc `Rejected` có current approver. Dữ liệu cho
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
- FE mở form và nút chỉnh sửa ở cả `Draft` và `Rejected` theo contract PUT. Sửa nội dung ở
  Rejected giữ nguyên trạng thái, assignment và rejection history; gửi duyệt lại trực tiếp bằng
  `SubmitForApproval`. `ReturnToDraft` là tùy chọn.
- Publish không yêu cầu Meeting Session; FE không chặn nút công bố chỉ vì chưa có phiên.
- **Lịch thuộc Meeting Session (SCRUM-70):** form tạo/sửa chỉ gửi `title`, `mode`, `location`,
  `sensitivity`, `description`. Danh sách không còn cột "Thời gian diễn ra"; sắp xếp theo `title`
  hoặc `createdAt`, mặc định `createdAt desc`. Link cũ có `sortBy=startsAt` rơi về mặc định thay vì
  gửi lên và nhận `400 MSG32`. Bộ lọc ngày vẫn gửi `from`/`to`; backend chỉ khớp meeting có phiên
  chưa hủy chồng lấp khoảng đó, nên meeting chưa có phiên không hiện khi lọc theo ngày. Trang chi
  tiết bỏ thời gian bắt đầu/kết thúc khỏi "Thông tin chung" và không lấy giờ từ phiên bù vào; tab
  "Phiên họp" là placeholder chờ `GET /api/meetings/{meetingId}/sessions` (SCRUM-68).

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
  lại" (UC-19 AF-1) — gửi thẳng, không qua `ReturnToDraft`.
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
- Readiness chỉ kiểm tra thông tin cấp cuộc họp; agenda và Meeting Session không bắt buộc (UC-19).

## Checklist BA cần hoàn tất

- Đồng bộ nội dung BR-24–BR-29 chính thức vào tài liệu BA và map từng luật vào validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Người duyệt được chọn trong UC-19; Planned Start Time và Planned End Time thuộc Meeting Session.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Định danh Use Case đã được chuẩn hóa theo danh mục hiện tại: UC-15 → UC-21 cho Meeting Management.
