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
- **EX-1**: [Mô tả lỗi hoặc điều kiện thất bại, mã MSG tương ứng — lấy mã kế tiếp trong docs/03-api/00-danh-muc-ma-thong-bao.md, không đánh số lại từ MSG01 cho mỗi use case]
  1. Hệ thống hiển thị thông báo lỗi `[Mã MSG] - [Nội dung thông báo]`.
  2. Actor quay lại bước ...

#### Quy tắc nghiệp vụ liên quan (Business Rules):
- **BR-[XX]**: [Nội dung quy tắc nghiệp vụ áp dụng]
```

---

## 3. Nội dung đặc tả (Detailed Use Case Specifications)

### FEATURE 1: XÁC THỰC & PHIÊN ĐĂNG NHẬP (AUTHENTICATION)

---

### UC-01: Sign in with Email and Password (Đăng nhập bằng Email và Mật khẩu)

- **Use Case ID**: UC-01
- **Use Case Name**: Sign in with Email and Password
- **Primary Actor**: Internal User, System Admin, Workspace Manager, DPO, Auditor
- **Secondary Actor**: Identity Provider
- **Mô tả (Description)**: As an Internal User, I want to sign in using my email and password so that I can securely access the system through my internal account.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user has an existing internal account.
  - PRE-2: The user's account is active.
  - PRE-3: The user is currently on the system login page.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The user is authenticated.
  - POST-2: The system identifies the corresponding internal user.
  - POST-3: The user is redirected to the appropriate authenticated page.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [SignIn.png](../06-sequencediagram/SignIn.png)

#### Luồng sự kiện chính (Main Flow):
1. The user enters their email and password on the login page.
2. The user clicks “Sign In”.
3. The system validates the provided credentials through the authentication service.
4. The system logs the user in and redirects the user to the appropriate authenticated page.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1**: System cannot authenticate the user
  1. The entered email or password is invalid. (BR-01, BR-02)
  2. The system displays MSG01 – “Invalid email or password.”
- **EX-2**: The internal account has been deactivated
  1. The corresponding internal account is inactive or has been deactivated. (BR-03)
  2. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”
- **EX-3**: User is not allowed to access the system
  1. The user does not have an active role or permission required to access the system. (BR-04)
  2. ~~The system displays “You do not currently have access to the system. Please contact your administrator for assistance.”~~ Removed with SCRUM-63 ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)); no message code is assigned.

---

### UC-02: Sign in with Google (Đăng nhập bằng Google)

- **Use Case ID**: UC-02
- **Use Case Name**: Sign in with Google
- **Primary Actor**: Internal User, System Admin, Workspace Manager, DPO, Auditor
- **Secondary Actor**: Google Identity Provider
- **Mô tả (Description)**: As an Internal User, I want to sign in using my Google account so that I can securely and quickly access the system through my existing internal account.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user has an existing internal account.
  - PRE-2: The user's account is active.
  - PRE-3: The user is currently on the system login page.
- **Điều kiện sau (Postconditions)**:
  - POST-1: User is authenticated.
  - POST-2: A valid server-side session is created.
  - POST-3: System identifies the corresponding internal user.
  - POST-4: User is redirected to the appropriate authenticated page.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [SignInWithGoogle.png](../06-sequencediagram/SignInWithGoogle.png)

#### Luồng sự kiện chính (Main Flow):
1. User selects “Continue with Google” on the login page.
2. User signs in using their Google account.
3. System verifies that the Google account is associated with an authorized internal user.
4. System signs the user in and grants access according to the user's assigned role.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1**: User can’t authenticate with Google
  1. The user fails to authenticate with Google or cancels the Google sign-in process. (BR-04)
  2. System displays message MSG03 – “Google sign-in was unsuccessful. Please try again.”
- **EX-2**: System can’t verify the Google account
  1. The system cannot verify the identity information returned by Google. (BR-05)
  2. System displays message MSG04 – “Unable to verify your Google account. Please try again.”
- **EX-3**: Google account is not linked to an internal account
  1. The authenticated Google account is not associated with any existing internal account. (BR-06)
  2. System displays message MSG05 – “This Google account is not authorized. Please contact your administrator for assistance.”
- **EX-4**: The internal account has been deactivated
  1. The internal account associated with the Google account is inactive or has been deactivated. (BR-03)
  2. System displays message MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”
- **EX-5**: User is not allowed to access the system
  1. The internal user does not have an active role or required permission to access the system. (BR-04)
  2. ~~System displays message “You do not currently have access to the system. Please contact your administrator for assistance.”~~ Removed with SCRUM-63 ([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)); no message code is assigned.

---

### UC-03: Sign Out (Đăng xuất)

- **Use Case ID**: UC-03
- **Use Case Name**: Sign Out
- **Primary Actor**: Internal User, System Admin, Workspace Manager, DPO, Auditor
- **Secondary Actor**: None
- **Mô tả (Description)**: As an authenticated user, I want to sign out of the system so that I can securely end my current access to the system.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is currently signed in to the system.
  - PRE-2: The user is currently accessing an authenticated area of the system.
- **Điều kiện sau (Postconditions)**:
  - POST-1: User is signed out of the system.
  - POST-2: The user's current authentication state is invalidated.
  - POST-3: User is redirected to the login page.
  - POST-4: Protected system functions are no longer accessible until the user signs in again.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [SignOut.png](../06-sequencediagram/SignOut.png)

#### Luồng sự kiện chính (Main Flow):
1. User selects “Sign Out”.
2. System displays a confirmation message.
3. User confirms the sign-out action.
4. System signs the user out.
5. System redirects the user to the login page.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1**: System can’t complete the sign-out action
  1. The system cannot complete the sign-out operation. (BR-09)
  2. System displays message MSG06 – “Unable to sign out. Please try again.”


### UC-11: Tạo cuộc họp (SCRUM-17)

Cuộc họp được tạo ở trạng thái `Draft` và chưa có Assigned Meeting Approver. Organizer chọn người
duyệt hợp lệ ở action `SubmitForApproval`; người đó phải là tài khoản đang hoạt động có capability
`MeetingApprover`. Việc chưa chọn người duyệt không ngăn lưu bản nháp.

- `title` bắt buộc sau trim, tối đa 300 ký tự. `startsAt` bắt buộc và phải là timestamp ISO 8601
  có `Z` hoặc offset múi giờ rõ ràng; timestamp được chuẩn hóa sang UTC trước khi lưu.
- `endsAt` là trường tùy chọn. Khi bỏ qua hoặc gửi `null`, cuộc họp được tạo với giờ kết thúc dự
  kiến chưa xác định. Khi có giá trị, giờ kết thúc phải sau `startsAt`.
- `mode` mặc định `InPerson`; hợp lệ: `InPerson`/`Online`/`Hybrid`. `sensitivity` mặc định `Normal`;
  hợp lệ: `Normal`/`Internal`/`Confidential`/`Secret`.
- `location` tùy chọn, trim và tối đa 300 ký tự. `description` tùy chọn, tối đa 4.000 ký tự.

Luồng và mã phản hồi chi tiết: [hợp đồng API tạo cuộc họp](../03-api/05-quan-ly-cuoc-hop.md#post-apimeetings).
Sơ đồ hiện có: [CreateMeeting.png](../06-sequencediagram/meeting-management/CreateMeeting.png).

### UC-12: Cập nhật thông tin cuộc họp (SCRUM-17)

PUT thay toàn bộ bảy trường thông tin có thể sửa của cuộc họp; payload không nhận assignment.
Organizer được sửa ở `Draft` hoặc `Rejected`. Sửa thông tin ở `Rejected` không chuyển trạng thái,
không đổi người duyệt và không thay đổi rejection history/reason. Muốn gửi duyệt lại, Organizer phải
qua action `ReturnToDraft`; assignment cycle cũ được xóa và người duyệt được chọn ở lần Submit tiếp theo. `endsAt` có thể
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
2. Organizer chọn một tài khoản khác mình, đang hoạt động và hiện có capability `MeetingApprover`.
3. Hệ thống khóa/đọc lại cuộc họp, chạy lại cùng readiness validator và kiểm tra lại người duyệt.
4. Hệ thống gán người duyệt, chuyển `Draft → WaitingApproval`, ghi action
   `SubmitForApproval` và tạo thông báo một cách nguyên tử.

#### Luồng sửa và gửi lại sau từ chối

1. Một cuộc họp `Rejected` không được submit trực tiếp sang `WaitingApproval`.
2. Meeting Organizer thực hiện action `ReturnToDraft`; hệ thống chuyển `Rejected → Draft`, ghi đúng
   một lifecycle history, giữ nguyên rejection history/reason cũ và không tạo invitation.
3. Organizer có thể sửa thông tin ở `Rejected` trước bước 2 hoặc ở `Draft` sau đó, rồi thực hiện lại luồng chính. Organizer có thể chọn một người
   duyệt hợp lệ khác cho cycle mới.

Sequence diagram UC-15 chỉ cho submit từ `Draft`. Nếu meeting đang `Rejected`, sơ đồ thể hiện riêng
`ReturnToDraft` trước khi submit lại; sửa thông tin có thể thực hiện ở `Rejected` hoặc `Draft`: [sơ đồ](../assets/diagrams/meeting-management/SubmitMeetingForApproval.png), [source Mermaid](../assets/diagrams/meeting-management/SubmitMeetingForApproval.mmd).
Quy tắc liên quan: BR-29, BR-33, BR-34, BR-35, BR-38.

#### Luồng khôi phục khi người duyệt không còn hợp lệ

1. Meeting đang `WaitingApproval`, nhưng Assigned Approver inactive hoặc mất `MeetingApprover`.
2. Organizer còn active và có `MeetingCreator` thực hiện `ReturnToDraft`. Hệ thống kiểm tra lại
   điều kiện trong transaction; approver còn hợp lệ hoặc trạng thái đã đổi thì từ chối thao tác.
3. Hệ thống chuyển `WaitingApproval → Draft`, xóa assignment và ghi đúng một history với actor,
   thời điểm và trạng thái nguồn. Giữ nguyên history cũ, không tạo notification/thư mời mới.
4. Organizer sửa nháp và thực hiện luồng chính để chọn một người duyệt hợp lệ mới. Không gán lại
   trực tiếp trong WaitingApproval; không yêu cầu khôi phục quyền người duyệt cũ để tiếp tục.

Readiness dùng field bắt buộc/giới hạn của schema Agenda, không tự áp thêm rule
thứ tự liên tục, thời lượng dương, presenter bắt buộc hoặc trạng thái Pending trước SCRUM-25.

### UC-16: Phê duyệt hoặc từ chối cuộc họp (SCRUM-19)

- **Actor chính:** Meeting Approver đang được gán cho approval cycle hiện tại.
- **Điều kiện tiên quyết:** Actor đã đăng nhập, tài khoản còn hoạt động, vẫn có capability
  `MeetingApprover`, đúng `Meeting.AssignedApproverId`, khác Organizer và cuộc họp đang `WaitingApproval`.
- **Điều kiện sau khi thành công:** Approve chuyển sang `Approved`; Reject chuyển sang `Rejected`
  và lưu rejection reason bắt buộc. Mỗi quyết định tạo đúng một lifecycle history.

Reason của Reject sau khi trim phải dài 1–1.000 ký tự. Status và history được ghi trong cùng một
transaction; nếu lưu hoặc commit thất bại, transaction rollback và không để lại quyết định một phần.
Actor, trạng thái `WaitingApproval`, assignment hiện tại, tài khoản active và capability
`MeetingApprover` được kiểm tra lại trước khi ghi.

Chair chỉ là relationship với meeting và không tự có quyền Approve/Reject. Nếu một Chair đồng thời
là current Assigned Approver thì quyền đến từ assignment cùng capability `MeetingApprover`, và
người đó phải khác Organizer. Cấm tự duyệt ở mọi mức độ mật; assignment cũ trỏ về Organizer cũng
không được Approve/Reject. Submit cho chính Organizer trả `400 MSG52`; quyết định tự duyệt trả
`403 MSG55`, không thay đổi status/history/jobs.

Meeting Organizer và current Assigned Approver được đọc toàn bộ lifecycle history, gồm rejection
reason. Khi assignment đổi, người duyệt cũ không tiếp tục có quyền này chỉ vì từng là approver;
history snapshot cũ không phải một access grant. Participant được quyền đọc meeting từ Published
trở đi có thể xem timeline nhưng không nhận rejection reason.

Quy tắc liên quan: BR-29, BR-35, BR-36, BR-38.
Sơ đồ UC-16 có transaction boundary và nhánh rollback: [sơ đồ](../assets/diagrams/meeting-management/ReviewMeetingApproval.png), [source Mermaid](../assets/diagrams/meeting-management/ReviewMeetingApproval.mmd).

### UC-17: Công bố cuộc họp (SCRUM-19)

- **Actor chính:** Meeting Organizer.
- **Điều kiện tiên quyết:** Actor đã đăng nhập, tài khoản còn hoạt động, là người tổ chức được phép
  thao tác cuộc họp, và cuộc họp đang `Approved`.
- **Điều kiện sau khi thành công:** Cuộc họp chuyển sang `Published`, có lifecycle history; hệ thống
  đã persist notification và invitation jobs cho recipient snapshot trong cùng transaction.

Chair không tự có quyền Publish. Nếu Chair đồng thời là Meeting Organizer thì quyền Publish đến từ
quan hệ Organizer.

Trong transaction Publish, hệ thống snapshot participant row theo policy SCRUM-20/BR-54, chỉ chọn
row có user active và `Response != Delegated`. Không tự thêm delegate chỉ vì `DelegatedToUserId`
trỏ tới họ; participant row riêng, active và chưa Delegated vẫn đủ điều kiện. Cùng transaction đó
cập nhật `Published`, thêm lifecycle history, tạo notification và persist invitation jobs với
recipient/content snapshot. Nếu bất kỳ ghi nào hoặc commit thất bại, rollback toàn bộ và không gửi
invitation. Sau commit, email job có `EmailStatus = Queued`, `SentAt = null`; Publish `200` không
xác nhận email đã được giao. Worker SCRUM-15 xử lý/retry bất đồng bộ; delivery failure không đổi
meeting khỏi `Published`. Thay đổi participant sau commit không sửa batch đã tạo.

Quy tắc liên quan: BR-29, BR-37–BR-45, BR-54.
Sơ đồ UC-17 thể hiện snapshot, queue và transaction rollback: [sơ đồ](../assets/diagrams/meeting-management/PublishMeeting.png), [source Mermaid](../assets/diagrams/meeting-management/PublishMeeting.mmd).
Sơ đồ lớp domain dùng chung cho UC-15–17: [Meeting lifecycle classes](../assets/diagrams/meeting-management/MeetingLifecycleClasses.png), [source Mermaid](../assets/diagrams/meeting-management/MeetingLifecycleClasses.mmd).

