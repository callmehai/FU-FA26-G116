# 05 · Tạo, đọc và sửa cuộc họp

**UC-11 Create Meeting / UC-12 Update Meeting — SCRUM-17.** Mã UC ghi kèm tên vì UC-11/UC-12
trong tài liệu quản lý vai trò đang dùng cho use case khác.

## Phạm vi

Ba endpoint cho người tạo khởi tạo cuộc họp nháp, đọc cuộc họp của mình và sửa trước khi gửi duyệt.
Danh sách `GET /api/meetings` thuộc SCRUM-18, chưa nằm trong contract này. Form sau khi tạo mở thẳng
chi tiết; danh sách UI hiện tại vẫn dùng dữ liệu mẫu.

> **Trạng thái BA:** chưa có Class Diagram cuộc họp và chưa có nội dung BR-24–BR-28 trong repo.
> Bảng validation và một số message code dưới đây là baseline kỹ thuật để triển khai task theo yêu
> cầu đã cung cấp; BA cần đối chiếu, bổ sung BR và sequence/class diagram sau.

## Quyền truy cập

Cả ba endpoint nằm trong group có `RequireCapability(MeetingCreator)`. Cổng đọc claim và xác minh
lại capability từ database theo SCRUM-12; không kiểm theo tên vai trò.

GET và PUT lọc `Id` **và** `CreatedById` trong truy vấn. ID không tồn tại và ID thuộc người tạo khác
cùng trả `404` không body. Vì task này chỉ dựng luồng Meeting Organizer, phạm vi đọc giới hạn cho
người tạo; quyền người dự/người duyệt cần được chốt cùng use case tương ứng.

Sửa chỉ được ở `Draft` và `Rejected`. Hệ thống đọc trạng thái mới nhất sau khi khóa dòng và trước
khi validate nội dung. Trạng thái khác trả `409 MSG03`. Sửa `Rejected` giữ nguyên trạng thái và
không thêm dòng lịch sử trạng thái.

## Contract

### POST `/api/meetings`

Request body của POST và PUT đều có cùng bảy field:

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

POST trả `201 Created`, header `Location: /api/meetings/{id}` và envelope `code`, `message`,
`meeting`. `meeting` gồm `id`, bảy field, `status`, `createdById`, `createdAt`, `updatedAt`.

### GET `/api/meetings/{id}`

Trả `200 OK` và object meeting phẳng, không bọc envelope. `mode`, `sensitivity` và `status` là chuỗi
enum. Thời gian trong response luôn UTC.

### PUT `/api/meetings/{id}`

PUT thay toàn bộ bảy field thông tin cuộc họp bằng dữ liệu request. Trả `200 OK`, envelope
`{ "code": "MSG02", "message": "Cập nhật cuộc họp thành công.", "meeting": { ... } }`.

Không chấp nhận quyền thay đổi `id`, `createdById`, `status`, `createdAt`, `updatedAt` hay dữ liệu
con qua body. Khi lưu cuộc họp bị từ chối, trạng thái vẫn là `Rejected`.

## Validation baseline

| Field | Quy tắc |
|---|---|
| `title` | Bắt buộc sau trim; tối đa 300 ký tự |
| `startsAt`, `endsAt` | Bắt buộc, ISO 8601 có `Z` hoặc offset rõ ràng; kết thúc phải lớn hơn bắt đầu |
| `mode` | Một trong `InPerson`, `Online`, `Hybrid` |
| `sensitivity` | Một trong `Normal`, `Internal`, `Confidential`, `Secret` |
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

Endpoint trả đồng thời mọi lỗi field phát hiện được. JSON sai cú pháp hoặc sai kiểu trả lỗi binding
`400`; client cần hiển thị lỗi khi response không có `errors`.

## Mã phản hồi

| HTTP | Code | Ý nghĩa |
|---:|---|---|
| 201 | `MEETING_CREATED` | Tạo nháp thành công |
| 200 | `MSG02` | Sửa thông tin thành công |
| 400 | `MEETING_INVALID_INFORMATION` | Thông tin không hợp lệ; có thể có `errors` theo field |
| 401 | — | Chưa đăng nhập |
| 403 | — | Không có `MeetingCreator` theo gate hai lớp |
| 404 | — | Không tồn tại hoặc không phải người tạo; body rỗng |
| 409 | `MSG03` | Trạng thái hiện tại không cho sửa |
| 503 | `MSG01` | Không thể lưu khi tạo; thử lại |
| 503 | `MSG04` | Không thể lưu khi sửa; thử lại |
| 503 | `MEETING_LOAD_FAILED` | Không tải được chi tiết; thử lại |

`MSG01`–`MSG04` đến từ mô tả use case được cung cấp. `MEETING_CREATED`,
`MEETING_INVALID_INFORMATION`, `MEETING_LOAD_FAILED` và cách gắn status `503` là mã/status baseline
được thêm vì sequence chưa định danh cụ thể các nhánh thành công tạo, validation và đọc lỗi. BA cần
đồng bộ chúng vào tài liệu yêu cầu/message catalog.

## Persistence và transaction

Không cần migration: entity `Meeting` và cấu hình hiện có đã có đủ field của contract. POST lưu
meeting `Draft` cùng một dòng `MeetingStatusHistory` (`FromStatus = null`, `ToStatus = Draft`,
`ActorId = creator`). EF Core lưu các thay đổi này trong cùng transaction.

PUT chạy transaction `ReadCommitted`, khóa đúng meeting row bằng PostgreSQL `FOR UPDATE`, rồi đọc
lại meeting và trạng thái trong transaction. Transaction kết thúc trước response. Cập nhật thông tin
không thay status hoặc lịch sử. Nếu body sau chuẩn hóa giống dữ liệu đã lưu, trả thành công nhưng
không ghi DB hoặc đổi `updatedAt`. Dữ liệu cho phép sửa giữa hai form cũ dùng “lần ghi sau cùng
thắng”; contract chưa có ETag/version để từ chối stale content.

## Hợp đồng tích hợp FE (chưa triển khai trong task backend này)

- Gọi API qua client hiện có bằng đường dẫn tương đối `/api/...` để tự mang token và refresh phiên.
- Khi nhận `403`, xử lý như quyền đã thay đổi; `404` ở GET/PUT không phân biệt ID không tồn tại
  với ID của người tạo khác.
- Chỉ hiển thị thao tác sửa cho người có `MeetingCreator`, đúng `createdById`, và trạng thái
  `Draft`/`Rejected`. Backend vẫn kiểm tra lại quyền sở hữu và trạng thái khi PUT.
- Nếu PUT trả `409 MSG03`, hiển thị nội dung lỗi và tải lại trạng thái hiện tại trước khi cho sửa tiếp.
- Sau khi POST hoặc PUT thành công, có thể mở `meeting.id` bằng GET chi tiết. Tìm cuộc họp từ
  danh sách cần API riêng thuộc SCRUM-18.
- UC hiện tại chưa có API lịch sử trạng thái, nên FE chưa thể hiển thị lịch sử thật.

## Checklist BA cần hoàn tất

- Bổ sung nội dung chính thức BR-24–BR-28, map từng luật vào bảng validation/lifecycle.
- Vẽ Class Diagram tạo/đọc/sửa và ghi Controller trong sequence là ASP.NET Minimal API.
- Xác nhận field bắt buộc/optional, giới hạn length và format timezone theo BA.
- Ghi rõ quyền GET, ownership và hành vi khi sửa `Rejected`.
- Bổ sung mã thành công/lỗi chưa có vào sequence/message catalog.
- Kiểm tra lại định danh UC theo convention Jira để tránh nhầm UC-11/UC-12 quản lý vai trò.
