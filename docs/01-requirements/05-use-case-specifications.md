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

Cuộc họp được tạo ở trạng thái `Draft` và chưa có Assigned Meeting Approver. Organizer chọn người
duyệt hợp lệ ở action `SubmitForApproval`; người đó phải là tài khoản đang hoạt động có capability
`MeetingApprover`. Việc chưa chọn người duyệt không ngăn lưu bản nháp.
- `endsAt` là trường tùy chọn. Khi bỏ qua hoặc gửi `null`, cuộc họp được tạo với giờ kết thúc dự
  kiến chưa xác định. Khi có giá trị, giờ kết thúc phải sau `startsAt`.

Luồng và mã phản hồi chi tiết: [hợp đồng API tạo cuộc họp](../03-api/05-quan-ly-cuoc-hop.md#post-apimeetings).
Sơ đồ hiện có: [CreateMeeting.png](../06-sequencediagram/meeting-management/CreateMeeting.png).

### UC-12: Cập nhật thông tin cuộc họp (SCRUM-17)

PUT thay toàn bộ bảy trường thông tin có thể sửa của cuộc họp; payload không nhận assignment. Chỉ
meeting ở `Draft` được sửa. Meeting `Rejected` phải qua action `ReturnToDraft` trước; sau đó
assignment cycle cũ đã được xóa và người duyệt mới được chọn ở lần Submit tiếp theo. `endsAt` có thể
bỏ qua hoặc gửi `null`; nếu có, giá trị phải sau `startsAt`. Trạng thái được kiểm tra trước nội dung
request.

Luồng và mã phản hồi chi tiết: [hợp đồng API cập nhật cuộc họp](../03-api/05-quan-ly-cuoc-hop.md#put-apimeetingsid).
Sơ đồ hiện có: [UpdateMeeting.png](../06-sequencediagram/meeting-management/UpdateMeeting.png).

### UC-15: Gửi duyệt cuộc họp (SCRUM-19)

- **Actor chính:** Meeting Organizer.
- **Điều kiện tiên quyết:** Actor đã đăng nhập, tài khoản còn hoạt động, là người tổ chức của cuộc
  họp và có capability `MeetingCreator`.
- **Điều kiện sau khi thành công:** Cuộc họp chuyển từ `Draft` sang `WaitingApproval`; người duyệt
  hợp lệ được gán cho approval cycle hiện tại; hệ thống ghi lifecycle history và tạo thông báo chờ
  duyệt cho người đó trong cùng transaction.

#### Luồng chính

1. Hệ thống kiểm tra cuộc họp đang ở `Draft` và toàn bộ mandatory meeting information cùng required
   agenda information đã đầy đủ theo BR-33. Agenda required không được rỗng.
2. Organizer chọn một tài khoản đang hoạt động và hiện có capability `MeetingApprover`.
3. Hệ thống khóa/đọc lại cuộc họp, chạy lại cùng readiness validator và kiểm tra lại người duyệt.
4. Hệ thống gán người duyệt, chuyển `Draft → WaitingApproval`, ghi action
   `SubmitForApproval` và tạo thông báo một cách nguyên tử.

#### Luồng sửa và gửi lại sau từ chối

1. Một cuộc họp `Rejected` không được submit trực tiếp sang `WaitingApproval`.
2. Meeting Organizer thực hiện action `ReturnToDraft`; hệ thống chuyển `Rejected → Draft`, ghi đúng
   một lifecycle history, giữ nguyên rejection history/reason cũ và không tạo invitation.
3. Organizer sửa meeting ở `Draft`, rồi thực hiện lại luồng chính. Organizer có thể chọn một người
   duyệt hợp lệ khác cho cycle mới.

Sequence diagram UC-15 chỉ cho submit từ `Draft`. Nếu meeting đang `Rejected`, sơ đồ thể hiện riêng
`ReturnToDraft` trước khi Organizer sửa và submit lại: [sơ đồ](../06-sequencediagram/meeting-management/SubmitMeetingForApproval.png), [source Mermaid](../06-sequencediagram/meeting-management/SubmitMeetingForApproval.mmd).
Quy tắc liên quan: BR-29, BR-33, BR-34, BR-35, BR-38.

### UC-16: Phê duyệt hoặc từ chối cuộc họp (SCRUM-19)

- **Actor chính:** Meeting Approver đang được gán cho approval cycle hiện tại.
- **Điều kiện tiên quyết:** Actor đã đăng nhập, tài khoản còn hoạt động, vẫn có capability
  `MeetingApprover`, đúng `Meeting.AssignedApproverId`, và cuộc họp đang `WaitingApproval`.
- **Điều kiện sau khi thành công:** Approve chuyển sang `Approved`; Reject chuyển sang `Rejected`
  và lưu rejection reason bắt buộc. Mỗi quyết định tạo đúng một lifecycle history.

Chair chỉ là relationship với meeting và không tự có quyền Approve/Reject. Nếu một Chair đồng thời
là current Assigned Approver thì quyền đến từ assignment cùng capability `MeetingApprover`.

Meeting Organizer và current Assigned Approver được đọc toàn bộ lifecycle history, gồm rejection
reason. Khi assignment đổi, người duyệt cũ không tiếp tục có quyền này chỉ vì từng là approver;
history snapshot cũ không phải một access grant. Participant được quyền đọc meeting từ Published
trở đi có thể xem timeline nhưng không nhận rejection reason.

Quy tắc liên quan: BR-29, BR-35, BR-36, BR-38.
Sơ đồ hiện có: [ReviewMeetingApproval.png](../06-sequencediagram/meeting-management/ReviewMeetingApproval.png).

### UC-17: Công bố cuộc họp (SCRUM-19)

- **Actor chính:** Meeting Organizer.
- **Điều kiện tiên quyết:** Actor đã đăng nhập, tài khoản còn hoạt động, là người tổ chức được phép
  thao tác cuộc họp, và cuộc họp đang `Approved`.
- **Điều kiện sau khi thành công:** Cuộc họp chuyển sang `Published`, có lifecycle history; hệ thống
  đã persist notification và invitation jobs cho recipient snapshot trong cùng transaction.

Chair không tự có quyền Publish. Nếu Chair đồng thời là Meeting Organizer thì quyền Publish đến từ
quan hệ Organizer.

Recipient được snapshot trong transaction Publish bằng policy do SCRUM-20 sở hữu. Chỉ participant
hợp lệ tại thời điểm snapshot được tạo notification/invitation; thay đổi participant sau commit
không sửa batch đã tạo. Theo BR-54, chỉ participant có tài khoản active và response khác `Delegated`
được nhận. Không tự thêm người được ủy quyền chỉ dựa trên `DelegatedToUserId`; nếu người đó có
participant row riêng, active và chưa Delegated thì row đó đủ điều kiện như bình thường.
Việc gửi email chạy bất đồng bộ theo SCRUM-15; lỗi delivery được retry và không đưa meeting từ
`Published` về trạng thái cũ.

Quy tắc liên quan: BR-29, BR-37–BR-45, BR-54.
Sơ đồ hiện có: [PublishMeeting.png](../06-sequencediagram/meeting-management/PublishMeeting.png).

