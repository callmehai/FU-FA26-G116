# Use Case Specifications · TrustCabinet

Tài liệu đặc tả chi tiết các Use Case (UC Specification) cho hệ thống họp không giấy tờ **TrustCabinet**.

---

## 1. Danh mục Use Case (Use Case Index)

### 1.1 Quản lý truy cập & Người dùng (Authentication & User Management)
- **UC-01**: Đăng nhập (Sign In with Email/Password)
- **UC-02**: Đăng nhập bằng Google (Sign In with Google)
- **UC-03**: Đăng xuất (Sign Out)
- **UC-04**: Tạo tài khoản người dùng (Create User Account)
- **UC-05**: Xem thông tin tài khoản người dùng (View User Account)
- **UC-06**: Cập nhật tài khoản người dùng (Update User Account)
- **UC-07**: Khóa / Mở khóa tài khoản (Deactivate / Reactivate User Account)
- **UC-08**: Đặt lại mật khẩu người dùng (Reset User Password)

### 1.2 Phân quyền & Vai trò (Role & Permission Management)
- **UC-09**: Tạo vai trò mới (Create Role)
- **UC-10**: Xem ma trận quyền hạn (View Role Permission Matrix)
- **UC-10b**: Cập nhật vai trò (Update Role)
- **UC-10c**: Cấu hình phân quyền vai trò (Configure Role Permissions)
- **UC-10d**: Xóa vai trò (Delete Role)

### 1.3 Quản lý cuộc họp (Meeting Management)
- **UC-11**: Tạo cuộc họp (Create Meeting)
- **UC-12**: Cập nhật thông tin cuộc họp (Update Meeting)
- **UC-13**: Xem chi tiết cuộc họp (View Meeting Details)
- **UC-14**: Xem danh sách cuộc họp (View Meeting List)
- **UC-15**: Gửi duyệt cuộc họp (Submit Meeting for Approval)
- **UC-16**: Phê duyệt / Từ chối cuộc họp (Review Meeting Approval)
- **UC-17**: Phát hành / Công bố cuộc họp (Publish Meeting)

### 1.4 Quản lý thông báo (Notification Management)
- **UC-18**: Xem danh sách thông báo (View Notifications)
- **UC-19**: Đánh dấu đã đọc một thông báo (Mark Notification as Read)
- **UC-20**: Đánh dấu đã đọc tất cả thông báo (Mark All Notifications as Read)
- **UC-21**: Điều hướng đến đối tượng thông báo (Open Notification Target)

---

## 2. Đặc tả chi tiết từng Use Case (Template & Specifications)

<!-- Dưới đây là khung mẫu chi tiết cho từng UC. Bạn có thể sao chép hoặc điền thông tin chi tiết vào từng mục -->

### Mẫu đặc tả Use Case (Template)

```markdown
### UC-[XX]: [Tên Use Case]

- **Actor chính**: [Tên actor thực hiện, ví dụ: Meeting Organizer / Admin / ...]
- **Mô tả**: [Mô tả tóm tắt mục đích của Use Case]
- **Điều kiện tiên quyết (Preconditions)**:
  - [Điều kiện 1, ví dụ: Người dùng đã đăng nhập vào hệ thống]
  - [Điều kiện 2, ví dụ: Có quyền hạn `AdminMeetings`]
- **Điều kiện sau (Postconditions)**:
  - [Kết quả sau khi thực hiện thành công]
- **Sơ đồ liên quan**:
  - Sequence Diagram: [Tên file Sequence Diagram](file://../06-sequencediagram/...)

#### Luồng sự kiện chính (Main Flow / Basic Flow):
1. Actor thực hiện hành động A...
2. Hệ thống kiểm tra và xác thực dữ liệu...
3. Hệ thống lưu trạng thái mới vào cơ sở dữ liệu...
4. Hệ thống phản hồi thông báo thành công cho Actor.

#### Luồng sự kiện thay thế (Alternative Flows):
- **AF-1**: [Mô tả luồng thay thế khi có lựa chọn khác]
  1. ...

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1**: [Mô tả lỗi hoặc điều kiện thất bại, mã MSG tương ứng]
  1. Hệ thống hiển thị thông báo lỗi `[Mã MSG] - [Nội dung thông báo]`.
  2. Actor quay lại bước ...

#### Quy tắc nghiệp vụ liên quan (Business Rules):
- **BR-[XX]**: [Nội dung quy tắc nghiệp vụ áp dụng]
```

---

## 3. Nội dung đặc tả (Điền thông tin chi tiết các UC bên dưới)

<!-- Bắt đầu điền nội dung UC chi tiết tại đây -->

### UC-11: Tạo cuộc họp (SCRUM-17)

Điều chỉnh yêu cầu ngày 27/09/2026: người duyệt không còn bắt buộc khi tạo bản nháp.

- `assignedApproverId` có thể bỏ qua hoặc gửi `null`. Nếu được gán sớm, tài khoản đó phải đang hoạt
  động và có capability `MeetingApprover`. Cuộc họp phải được gán một người duyệt hợp lệ ở UC-15
  trước khi gửi duyệt.
- `endsAt` là trường tùy chọn. Khi bỏ qua hoặc gửi `null`, cuộc họp được tạo với giờ kết thúc dự
  kiến chưa xác định. Khi có giá trị, giờ kết thúc phải sau `startsAt`.

Luồng và mã phản hồi chi tiết: [hợp đồng API tạo cuộc họp](../03-api/05-quan-ly-cuoc-hop.md#post-apimeetings).
Sơ đồ hiện có: [CreateMeeting.png](../06-sequencediagram/meeting-management/CreateMeeting.png).

### UC-12: Cập nhật thông tin cuộc họp (SCRUM-17)

PUT thay toàn bộ thông tin có thể sửa của cuộc họp. `assignedApproverId` có thể bỏ qua hoặc gửi
`null`; một người duyệt mới chỉ được gán nếu tài khoản đang hoạt động và có capability
`MeetingApprover`. `endsAt` cũng có thể bỏ qua hoặc gửi `null`; nếu có giá trị, nó phải sau
`startsAt`. Validation áp dụng khi cuộc họp còn ở trạng thái được phép sửa; trạng thái không cho
sửa được kiểm tra trước nội dung request.

Luồng và mã phản hồi chi tiết: [hợp đồng API cập nhật cuộc họp](../03-api/05-quan-ly-cuoc-hop.md#put-apimeetingsid).
Sơ đồ hiện có: [UpdateMeeting.png](../06-sequencediagram/meeting-management/UpdateMeeting.png).

### UC-15: Gửi duyệt cuộc họp

Người tổ chức chọn một tài khoản đang hoạt động có capability `MeetingApprover` khi gửi cuộc họp
để duyệt. Draft chưa có người duyệt không được chuyển sang `WaitingApproval`.

