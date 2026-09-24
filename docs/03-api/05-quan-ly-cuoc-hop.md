# 05 · Tạo, đọc và sửa cuộc họp

**UC-11 Create Meeting / UC-12 Update Meeting — SCRUM-17.** Mã UC ghi kèm tên vì UC-11/UC-12
trong tài liệu quản lý vai trò đang dùng cho use case khác.

## Phạm vi

Các endpoint cho người tạo khởi tạo cuộc họp nháp, chọn người duyệt và sửa trước khi gửi duyệt.
Người có quan hệ hợp lệ với cuộc họp có thể đọc chi tiết. Danh sách `GET /api/meetings` thuộc
SCRUM-18, chưa nằm trong contract này; danh sách UI hiện tại vẫn dùng dữ liệu mẫu. Sau khi tạo,
form hiển thị xác nhận và mã cuộc họp.

Tiêu đề và Thời gian bắt đầu bắt buộc theo BR-24. Người duyệt bắt buộc khi tạo mới theo ERD và
tiêu chí chấp nhận SCRUM-17. Thời gian kết thúc tùy chọn; nếu nhập thì phải sau thời gian bắt đầu
theo BR-25. Form khởi tạo Hình thức là `InPerson` và Mức độ mật là `Normal`.

## Quyền truy cập

POST, `GET /api/meetings/approvers` và PUT yêu cầu capability `MeetingCreator`; cổng đọc claim và
xác minh lại capability từ database theo SCRUM-12, không kiểm theo tên vai trò. GET chi tiết yêu cầu
đăng nhập và tài khoản còn hoạt động, sau đó kiểm tra quan hệ cuộc họp theo BR-29.

GET luôn cho người tạo đọc. Người duyệt được gán chỉ đọc từ khi meeting rời `Draft`; thành phần tham
dự (Chair, Secretary và Participant) chỉ đọc từ `Published` trở đi. Vì vậy meeting `Draft` chỉ người
tạo thấy, còn các trạng thái trước khi công bố không lộ cho thành phần tham dự. Không đủ quyền đọc
thì trả `404` rỗng, giống ID không tồn tại. PUT vẫn chỉ cho người tạo/người tổ chức sửa; ID không tồn
tại và meeting của người tổ chức khác cùng trả `404` rỗng.

Sửa chỉ được ở `Draft` và `Rejected`. Hệ thống đọc trạng thái mới nhất sau khi khóa dòng và trước
khi validate nội dung. Trạng thái khác trả `409 MSG03`. Sửa `Rejected` giữ nguyên trạng thái và
không thêm dòng lịch sử trạng thái.

## Contract

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

`title`, `startsAt` và `assignedApproverId` là bắt buộc. `endsAt` có thể `null` hoặc bị bỏ qua;
nếu có giá trị, nó phải lớn hơn `startsAt`. Nếu `mode` hoặc `sensitivity` bị bỏ qua, service dùng
giá trị khởi tạo của form (`InPerson`, `Normal`). Người duyệt phải là tài khoản đang hoạt động có
capability `MeetingApprover`.

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm thông tin cuộc họp, `assignedApproverId`, `status`, `createdById`,
`createdAt`, `updatedAt`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope. `mode`, `sensitivity` và `status` là chuỗi
enum. Thời gian trong response luôn UTC.

### PUT `/api/meetings/{id}`

PUT thay toàn bộ tám field thông tin cuộc họp bằng dữ liệu request. Trả `200 OK`, envelope
`{ "code": "MSG02", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Khi lưu cuộc họp bị từ chối, trạng thái vẫn là `Rejected`.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
| `startsAt` | Bắt buộc, ISO 8601 có `Z` hoặc offset rõ ràng |
| `endsAt` | Tùy chọn; nếu có, ISO 8601 có `Z` hoặc offset rõ ràng và phải lớn hơn giờ bắt đầu |
| `assignedApproverId` | Bắt buộc; tài khoản đang hoạt động và có capability `MeetingApprover` |
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

Endpoint trả đồng thời mọi lỗi field phát hiện được. Với PUT, hệ thống khóa meeting và kiểm tra
trạng thái trước khi parse JSON: nếu meeting không còn sửa được thì trả `409 MSG03`, kể cả body JSON
sai cú pháp hoặc sai kiểu. Với meeting còn sửa được, JSON sai cú pháp/sai kiểu trả `400` và code
`MEETING_INVALID_INFORMATION` không có `errors`. POST JSON lỗi cũng trả `400`.

## Mã phản hồi

| HTTP | Code | Ý nghĩa |
|---:|---|---|
| 201 | `MEETING_CREATED` | Tạo nháp thành công |
| 200 | `MSG02` | Sửa thông tin thành công |
| 400 | `MEETING_INVALID_INFORMATION` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 401 | — | Chưa đăng nhập |
| 403 | — | Không có `MeetingCreator` theo gate hai lớp |
| 404 | — | GET: không tồn tại hoặc caller không có quan hệ; PUT: không tồn tại hoặc caller không phải người tạo; body rỗng |
| 409 | `MSG03` | Trạng thái hiện tại không cho sửa |
| 503 | `MSG01` | Không thể lưu khi tạo; thử lại |
| 503 | `MSG04` | Không thể lưu khi sửa; thử lại |
| 503 | `MEETING_LOAD_FAILED` | Không tải được chi tiết; thử lại |

`MSG01`–`MSG04` đến từ mô tả use case được cung cấp. `MEETING_CREATED`,
`MEETING_INVALID_INFORMATION`, `MEETING_LOAD_FAILED` và cách gắn status `503` là mã/status baseline
được thêm vì sequence chưa định danh cụ thể các nhánh thành công tạo, validation và đọc lỗi. BA cần
đồng bộ chúng vào tài liệu yêu cầu/message catalog.

## Persistence và transaction

Cột `ends_at` nullable và FK nullable `assigned_approver_id` giữ được các meeting cũ; API POST vẫn
bắt buộc gán người duyệt cho meeting mới theo ERD. POST lưu meeting `Draft` cùng người duyệt và một
dòng `MeetingStatusHistory` (`FromStatus = null`, `ToStatus = Draft`, `ActorId = creator`). EF Core
lưu các thay đổi này trong cùng transaction.

PUT chạy transaction `ReadCommitted`, khóa đúng meeting row bằng PostgreSQL `FOR UPDATE`, rồi đọc
lại meeting và trạng thái trong transaction. Transaction kết thúc trước response. Cập nhật thông tin
không thay status hoặc lịch sử. Nếu body sau chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng
không ghi DB hoặc đổi `updatedAt`. Dữ liệu cho phép sửa giữa hai form cũ dùng “lần ghi sau cùng
thắng”; contract chưa có ETag/version để từ chối stale content.

## Hợp đồng tích hợp FE

- Gọi API qua client hiện có bằng đường dẫn tương đối `/api/...` để tự mang token và refresh phiên.
- Khi nhận `403`, xử lý như quyền hệ thống đã thay đổi; GET chi tiết trả `404` nếu caller không có
  quan hệ với meeting.
- Chỉ hiển thị thao tác sửa cho người có `MeetingCreator`, đúng `createdById`, và trạng thái
  `Draft`/`Rejected`. Backend vẫn kiểm tra lại quyền sở hữu và trạng thái khi PUT.
- Nếu PUT trả `409 MSG03`, hiển thị nội dung lỗi và tải lại trạng thái hiện tại trước khi cho sửa tiếp.
- Sau khi POST hoặc PUT thành công, có thể mở `meeting.id` bằng GET chi tiết. Tìm cuộc họp từ
  danh sách cần API riêng thuộc SCRUM-18.
- UC hiện tại chưa có API lịch sử trạng thái, nên FE chưa thể hiển thị lịch sử thật.

## Checklist BA cần hoàn tất

- Đồng bộ nội dung BR-24–BR-29 chính thức vào tài liệu BA và map từng luật vào validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Đồng bộ tính bắt buộc của người duyệt và tính tùy chọn của `endsAt` vào UC-11/UC-12, cùng giới hạn length và format timezone.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Kiểm tra lại định danh UC theo convention Jira để tránh nhầm UC-11/UC-12 quản lý vai trò.
