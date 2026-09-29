# 05 · Danh sách, tạo, đọc và sửa cuộc họp

**UC-11 Create Meeting / UC-12 Update Meeting — SCRUM-17; UC-14 View Meeting List — SCRUM-18.** Mã UC ghi kèm tên vì UC-11/UC-12
trong tài liệu quản lý vai trò đang dùng cho use case khác.

## Phạm vi

Các endpoint cho người tạo khởi tạo cuộc họp nháp và sửa trước khi gửi duyệt. Luồng tạo/sửa nháp
không yêu cầu chọn người duyệt. Với PUT, `assignedApproverId` bị bỏ trống hoặc gửi `null` sẽ bỏ
gán người duyệt; muốn giữ người duyệt hiện tại thì gửi lại ID đó. UC-15 yêu cầu chọn người duyệt hợp
lệ trước khi gửi.
Người có quan hệ hợp lệ với cuộc họp có thể đọc chi tiết và lấy trang danh sách. `GET /api/meetings`
được bổ sung trong SCRUM-18. Màn danh sách, tạo, sửa và chi tiết đều gọi API thật: tạo xong thì báo
thành công và mở trang chi tiết của cuộc họp mới.

Tiêu đề và Thời gian bắt đầu bắt buộc theo BR-24. [UC-11 và UC-12](../01-requirements/05-use-case-specifications.md)
ghi nhận thay đổi yêu cầu ngày 27/09/2026: người duyệt chỉ bắt buộc khi gửi duyệt ở UC-15. Thời gian
kết thúc tùy chọn; nếu nhập thì phải sau thời gian bắt đầu theo BR-25. Form khởi tạo Hình thức là
`InPerson` và Mức độ mật là `Normal`.

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

Sửa chỉ được ở `Draft` và `Rejected`. Hệ thống đọc trạng thái mới nhất sau khi khóa dòng và trước
khi validate nội dung. Trạng thái khác trả `409 MEETING_NOT_EDITABLE`. Sửa `Rejected` giữ nguyên
trạng thái và không thêm dòng lịch sử trạng thái.

## Contract

### GET `/api/meetings`

Các tham số query đều tùy chọn. Giá trị rỗng tương đương không gửi; mỗi tham số chỉ được gửi một
lần. Tất cả bộ lọc được kết hợp bằng AND (BR-30/BR-31).

| Tham số | Quy tắc |
|---|---|
| `status` | Tên enum PascalCase trong chín `MeetingStatus` |
| `sensitivity` | `Normal`, `Internal`, `Confidential` hoặc `Secret` |
| `q` | Trim; tìm chuỗi con của `title` không phân biệt hoa/thường. `%`, `_`, `\` là ký tự thường, không phải wildcard |
| `from`, `to` | ISO 8601 có `Z` hoặc offset; giây tùy chọn, ví dụ `2026-10-01T09:00+07:00`; chuẩn hóa UTC, lọc `startsAt` gồm cả hai đầu; có thể chỉ gửi một đầu |
| `page`, `pageSize` | Mặc định `1`, `20`; `page` từ `1` đến `2.147.483.647`, `pageSize` từ `1` đến `100` |
| `sortBy` | `title`, `startsAt`, `createdAt`; mặc định `startsAt` |
| `sortDir` | `asc`, `desc`; mặc định `asc` cho `title`, `desc` cho hai cột thời gian |

Khi nhiều dòng có cùng giá trị sort, xếp tiếp theo `id asc`. Ví dụ:
`GET /api/meetings?status=Published&sensitivity=Internal&q=giao&from=2026-10-01T00%3A00%3A00Z&page=2&pageSize=20&sortBy=startsAt&sortDir=desc`.
Đổi trang phải gửi lại bộ lọc và sort hiện tại (BR-32). FE giữ toàn bộ tiêu chí trên URL của màn
danh sách (`/mgmt/meetings?q=…&status=…&page=…`), nên tải lại trang, chia sẻ đường dẫn hay quay lại từ
trang chi tiết đều giữ nguyên bộ lọc và trang đang xem.

Khi gửi timezone có dấu `+` trong query, mã hóa dấu này thành `%2B`, ví dụ
`from=2026-10-01T09%3A00%2B07%3A00`. Timestamp thiếu timezone trả `400 MEETING_INVALID_QUERY`.

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
`MEETING_INVALID_QUERY`, `message` là `Tham số truy vấn không hợp lệ.` và `errors` theo tên tham số.
Bao gồm enum/sort lạ, thời gian thiếu offset, `from > to`, trang hoặc kích thước trang ngoài
giới hạn. Lỗi truy xuất database, kể cả ở bước kiểm tra tài khoản còn hoạt động, trả `500` với
`code: MEETING_LIST_FAILED` và thông điệp
`Không thể tải danh sách cuộc họp. Vui lòng thử lại.` như EX-1 của UC-14.

Sequence UC-14 hiện ghi BR-29 và BR-32 ở bước repository. Khi ticket đến `BA Final Review`, BA
đồng bộ BR-30/31, định dạng timestamp, giới hạn phân trang và nhánh query sai
`400 MEETING_INVALID_QUERY` vào diagram/spec; Controller kiểm tra query trước khi gọi Service/Repository.
Nhánh lỗi truy xuất dùng `500 MEETING_LIST_FAILED` theo contract hiện hành.

### GET `/api/meetings/approvers`

Trả danh sách tài khoản đang hoạt động có capability `MeetingApprover`, sắp xếp theo tên. Response
chỉ gồm `id` và `fullName`; endpoint kế thừa gate `MeetingCreator`.

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
  "description": "Thảo luận kế hoạch tuần.",
  "assignedApproverId": "dc36f21a-c030-4ddb-a6ec-9d1254230001"
}
```

`title` và `startsAt` là bắt buộc. `assignedApproverId` có thể `null` hoặc bị bỏ qua. Với POST,
nếu được gửi, nó phải là tài khoản đang hoạt động có capability `MeetingApprover`. Với PUT, bỏ qua
hoặc gửi `null` sẽ bỏ gán người duyệt; nếu gửi ID khác ID hiện tại thì người đó phải đang hoạt động
và có capability này. Gửi lại ID hiện tại không chặn cập nhật nếu tài khoản đó đã bị vô hiệu hóa.
UC-15 phải kiểm tra và yêu cầu người duyệt hợp lệ trước khi chuyển meeting sang `WaitingApproval`.
`endsAt` có thể `null` hoặc bị bỏ qua; nếu có giá trị, nó phải lớn hơn `startsAt`. Nếu `mode` hoặc
`sensitivity` bị bỏ qua, service dùng giá trị khởi tạo của form (`InPerson`, `Normal`).

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm thông tin cuộc họp, `assignedApproverId`, `status`, `createdById`,
`createdAt`, `updatedAt`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope. `mode`, `sensitivity` và `status` là chuỗi
enum. Thời gian trong response luôn UTC.

### PUT `/api/meetings/{id}`

PUT thay toàn bộ tám field thông tin cuộc họp bằng dữ liệu request. Trả `200 OK`, envelope
`{ "code": "MEETING_UPDATED", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Khi lưu cuộc họp bị từ chối, trạng thái vẫn là `Rejected`.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
| `startsAt` | Bắt buộc, ISO 8601 có `Z` hoặc offset rõ ràng |
| `endsAt` | Tùy chọn; nếu có, ISO 8601 có `Z` hoặc offset rõ ràng và phải lớn hơn giờ bắt đầu |
| `assignedApproverId` | Tùy chọn khi tạo/sửa nháp; nếu có giá trị thì tài khoản đang hoạt động và có capability `MeetingApprover`. Bắt buộc tại UC-15 trước khi gửi duyệt |
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
  "code": "MEETING_INVALID_INFORMATION",
  "message": "Thông tin cuộc họp không hợp lệ.",
  "errors": {
    "title": "Tên cuộc họp không được để trống.",
    "endsAt": "Thời gian kết thúc phải sau thời gian bắt đầu."
  }
}
```

Endpoint trả đồng thời mọi lỗi field phát hiện được. Với PUT, hệ thống kiểm tra trạng thái trước khi
đọc/parse JSON: nếu meeting đã không còn sửa được thì trả `409 MEETING_NOT_EDITABLE`, kể cả body
JSON sai cú pháp hoặc sai kiểu. Transaction cập nhật vẫn khóa và kiểm tra lại trạng thái trước khi
validate/lưu để xử lý cạnh tranh. Request PUT có Content-Type thiếu hoặc không hỗ trợ trả `415` với code
`MEETING_UNSUPPORTED_CONTENT_TYPE`. Với meeting còn sửa được và Content-Type hợp lệ, JSON sai cú
pháp/sai kiểu trả `400` và code `MEETING_INVALID_INFORMATION` không có `errors`. POST JSON lỗi cũng trả `400`.

## Mã phản hồi

| HTTP | Code | Ý nghĩa |
|---:|---|---|
| 201 | `MEETING_CREATED` | Tạo nháp thành công |
| 200 | `MEETING_UPDATED` | Sửa thông tin thành công |
| 400 | `MEETING_INVALID_INFORMATION` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 400 | `MEETING_INVALID_QUERY` | Query danh sách không hợp lệ; có `errors` theo tham số |
| 401 | — | Chưa đăng nhập |
| 403 | — | POST/PUT/approvers: thiếu `MeetingCreator`; GET list/detail: tài khoản không hoạt động |
| 404 | — | GET: không tồn tại hoặc caller không có quan hệ; PUT: không tồn tại hoặc caller không phải người tạo; body rỗng |
| 409 | `MEETING_NOT_EDITABLE` | Trạng thái hiện tại không cho sửa |
| 415 | `MEETING_UNSUPPORTED_CONTENT_TYPE` | PUT không gửi JSON hoặc Content-Type không hỗ trợ |
| 500 | `MEETING_CREATE_FAILED` | Không thể lưu khi tạo; thử lại |
| 500 | `MEETING_UPDATE_FAILED` | Không thể lưu khi sửa; thử lại |
| 500 | `MEETING_LOAD_FAILED` | Không tải được chi tiết; thử lại |
| 500 | `MEETING_LIST_FAILED` | Không tải được danh sách; thử lại |

Các response code của nhóm endpoint này dùng chung tiền tố `MEETING_`. Lỗi persistence trả `500`.

## Persistence và transaction

Cột `ends_at` và FK `assigned_approver_id` đều nullable. POST lưu meeting `Draft`, có thể chưa có
người duyệt, cùng một dòng `MeetingStatusHistory` (`FromStatus = null`, `ToStatus = Draft`, `ActorId = creator`). EF Core
lưu các thay đổi này trong cùng transaction.

PUT kiểm tra quyền sở hữu và trạng thái hiện tại bằng truy vấn không khóa trước khi đọc body. Sau khi
xác nhận meeting còn sửa được, hệ thống kiểm tra Content-Type rồi deserialize JSON ngoài transaction
khóa ghi. Khi xử lý cập nhật, transaction `ReadCommitted` khóa đúng meeting row bằng PostgreSQL
`FOR UPDATE`, đọc lại trạng thái và trả `409 MEETING_NOT_EDITABLE` nếu trạng thái đã đổi. Sau lần kiểm tra
khóa, hệ thống validate dữ liệu và lưu; không có network read trong lúc giữ row lock.
Transaction kết thúc trước response. Cập nhật thông tin không thay status hoặc lịch sử. Nếu body sau
chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng không ghi DB hoặc đổi `updatedAt`. Dữ liệu cho
phép sửa giữa hai form cũ dùng “lần ghi sau cùng thắng”; contract chưa có ETag/version để từ chối
stale content.

## Hợp đồng tích hợp FE

- Gọi API qua client hiện có bằng đường dẫn tương đối `/api/...` để tự mang token và refresh phiên.
- Khi nhận `403`, xử lý như quyền hệ thống đã thay đổi; GET chi tiết trả `404` nếu caller không có
  quan hệ với meeting.
- Chỉ hiển thị thao tác sửa cho người có `MeetingCreator`, đúng `createdById`, và trạng thái
  `Draft`/`Rejected`. Backend vẫn kiểm tra lại quyền sở hữu và trạng thái khi PUT.
- Nếu PUT trả `409 MEETING_NOT_EDITABLE`, hiển thị nội dung lỗi và tải lại trạng thái hiện tại trước khi cho sửa tiếp.
- Form đổi trường địa điểm theo `mode`: `InPerson` nhập địa điểm; `Online` không có địa điểm vì
  cuộc họp diễn ra trong phòng gọi trực tuyến của TrustCabinet (README: gọi nhóm theo phòng họp), FE
  gửi `location` rỗng; `Hybrid` nhập địa điểm cho người họp trực tiếp, người ở xa vào phòng gọi.
  Không có trường đường dẫn họp bên ngoài.
- Sau khi POST hoặc PUT thành công, có thể mở `meeting.id` bằng GET chi tiết.
- Khoảng thời gian trên màn danh sách chọn theo ngày; FE gửi `from` là 00:00 và `to` là 23:59:59.999
  theo giờ của thiết bị, đổi sang UTC, nên chọn cùng một ngày ở hai đầu sẽ lọc đúng ngày đó.
- UC hiện tại chưa có API lịch sử trạng thái, nên FE chưa thể hiển thị lịch sử thật.

## Checklist BA cần hoàn tất

- Đồng bộ nội dung BR-24–BR-29 chính thức vào tài liệu BA và map từng luật vào validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Tác giả đã ghi các điều chỉnh về người duyệt, `endsAt`, giá trị mặc định và validation vào comment
  của Jira SCRUM-17 ngày 28/09/2026; BA đối chiếu với BR-24–BR-29 và cập nhật tài liệu/diagram nếu cần.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Kiểm tra lại định danh UC theo convention Jira để tránh nhầm UC-11/UC-12 quản lý vai trò.
