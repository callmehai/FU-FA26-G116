# Use Case Specifications · TrustCabinet

Tài liệu đặc tả chi tiết các Use Case (UC Specification) cho hệ thống họp không giấy tờ **TrustCabinet**.

**Nguồn quy tắc nghiệp vụ:** [Danh mục Business Rules BR-01 → BR-77](06-business-rules.md). UC trong tài liệu này dẫn chiếu mã BR tại các điều kiện/bước liên quan; nội dung định nghĩa BR được quản lý tập trung ở danh mục đó để tránh các bản sao lệch nhau. Mã `BR` là mã quy tắc nghiệp vụ, độc lập với mã thông báo `MSG`.

---

## 1. Danh mục Use Case (Use Case Index)

### 1.1 Quản lý truy cập & Xác thực (Authentication)
- **UC-01**: Đăng nhập bằng Email/Password (Sign In with Email and Password)
- **UC-02**: Đăng nhập bằng Google (Sign In with Google)
- **UC-03**: Đăng xuất (Sign Out)

### 1.2 Quản lý người dùng (User Management)
- **UC-04**: Xem danh sách tài khoản người dùng (View User Accounts)
- **UC-05**: Tạo tài khoản người dùng (Create User Account)
- **UC-06**: Cập nhật thông tin người dùng (Update User Account)
- **UC-07**: Khóa / Mở khóa tài khoản (Deactivate / Reactivate User Account)
- **UC-08**: Đặt lại mật khẩu người dùng (Reset User Password)
- **UC-09**: Phân quyền vai trò cho người dùng (Assign System Role to User)

### 1.3 Quản lý vai trò & Phân quyền (Role Management)
- **UC-10**: Xem ma trận vai trò & quyền hạn (View Role & Permission Matrix)
- **UC-11**: Cấu hình phân quyền vai trò (Configure Role Permissions)
- **UC-12**: Tạo vai trò mới (Create Role)
- **UC-13**: Cập nhật vai trò (Update Role)
- **UC-14**: Xóa vai trò (Delete Role)

### 1.4 Quản lý cuộc họp (Meeting Management)
- **UC-15**: Tạo cuộc họp (Create Meeting)
- **UC-16**: Cập nhật cuộc họp (Update Meeting)
- **UC-17**: Xem chi tiết cuộc họp (View Meeting Details)
- **UC-18**: Xem danh sách cuộc họp (View Meeting List)
- **UC-19**: Gửi duyệt cuộc họp (Submit Meeting for Approval)
- **UC-20**: Phê duyệt / Từ chối cuộc họp (Review Meeting Approval)
- **UC-21**: Phát hành cuộc họp (Publish Meeting)

### 1.5 Quản lý thông báo (Notification Management)
- **UC-22**: Xem danh sách thông báo (View Notifications)
- **UC-23**: Đánh dấu đã đọc thông báo (Mark Notification as Read)
- **UC-24**: Đánh dấu đã đọc tất cả thông báo (Mark All Notifications as Read)
- **UC-25**: Mở đối tượng liên kết thông báo (Open Notification Target)

### 1.6 Quản lý phiên họp (Meeting Session Management)
- **UC-26**: Tạo phiên họp (Create Meeting Session)
- **UC-27**: Cập nhật phiên họp (Update Meeting Session)
- **UC-28**: Hủy phiên họp (Cancel Meeting Session)
- **UC-29**: Bắt đầu phiên họp (Start Meeting Session)
- **UC-30**: Kết thúc phiên họp (End Meeting Session)

### 1.7 Quản lý chương trình nghị sự (Agenda Management)
- **UC-31**: Tạo mục chương trình nghị sự (Create Agenda Item)
- **UC-32**: Cập nhật mục chương trình nghị sự (Update Agenda Item)
- **UC-33**: Xóa mục chương trình nghị sự (Delete Agenda Item)
- **UC-34**: Sắp xếp lại thứ tự mục chương trình nghị sự (Reorder Agenda Items)
- **UC-35**: Đặt mục chương trình nghị sự hiện tại (Set Current Agenda Item)
- **UC-36**: Hoàn thành mục chương trình nghị sự (Complete Agenda Item)

### 1.8 Thành phần tham dự & Vai trò trong cuộc họp (Meeting Participants & Meeting Roles)
- **UC-37**: Xem thành phần tham dự (View Meeting Participants)
- **UC-38**: Thêm thành phần tham dự (Add Meeting Participant)
- **UC-39**: Gán / Thay đổi vai trò trong cuộc họp (Assign / Change Meeting Role)
- **UC-40**: Xóa thành phần tham dự (Remove Meeting Participant)
- **UC-41**: Phản hồi lời mời họp (Respond to Meeting Invitation)
- **UC-42**: Xem trạng thái phản hồi của thành phần tham dự (View Participant Response Status)

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
  - Sequence Diagram: [UC01_SignIn.png](../06-sequencediagram/auth/UC01_SignIn.png)

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
  - Sequence Diagram: [UC02_SignInWithGoogle.png](../06-sequencediagram/auth/UC02_SignInWithGoogle.png)

#### Luồng sự kiện chính (Main Flow):
1. User selects “Continue with Google” on the login page.
2. User signs in using their Google account.
3. System verifies that the Google identity is linked to an active internal user account.
4. System signs the user in; the user's capabilities determine access to protected functions.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1**: User can’t authenticate with Google
  1. The user fails to authenticate with Google or cancels the Google sign-in process.
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
  - Sequence Diagram: [UC03_SignOut.png](../06-sequencediagram/auth/UC03_SignOut.png)

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

---

### FEATURE 2: QUẢN LÝ NGƯỜI DÙNG (USER MANAGEMENT)

---

### UC-04: View User Accounts (Xem danh sách tài khoản người dùng)

- **Use Case ID**: UC-04
- **Use Case Name**: View User Accounts
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to view, search, filter, and browse user accounts in the system.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin has permission to view user accounts.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The user account list is displayed.
  - POST-2: No user account information is modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC04_ViewUserAccount.png](../06-sequencediagram/user-management/UC04_ViewUserAccount.png)

#### Luồng sự kiện chính (Main Flow – View User Accounts):
1. The System Admin selects User Accounts from the navigation menu.
2. The system retrieves the user accounts.
3. The system sorts the user accounts according to the default sorting rule.
4. The system displays the user account list showing Full Name, Username/Email, System Role, and Account Status.
5. The System Admin browses the user account list using pagination controls.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Search User Accounts**
  1. At Step 4 of the Main Flow, the System Admin enters a Name, Username, or Email into the search field.
  2. The system searches for user accounts matching the entered criteria.
  3. The system displays the matching user accounts.
  4. The flow resumes at Step 5 of the Main Flow.

- **AF-2: Filter User Accounts**
  1. At Step 4 of the Main Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.
  2. The system filters the user accounts according to the selected criteria.
  3. The system displays the matching user accounts.
  4. The flow resumes at Step 5 of the Main Flow.

- **AF-3: No User Accounts Match the Current Criteria**
  1. The applied search or filter criteria produce no matching user accounts.
  2. The system displays an empty list and a no-results message according to BR-12.
  3. The System Admin may clear or adjust the search/filter parameters.
  4. The flow resumes at Step 5 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- N/A

---

### UC-05: Create User Account (Tạo tài khoản người dùng)

- **Use Case ID**: UC-05
- **Use Case Name**: Create User Account
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to create a new user account by providing the required account information.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin has permission to create user accounts.
- **Điều kiện sau (Postconditions)**:
  - POST-1: A new user account is created successfully.
  - POST-2: The new user account appears in User Management.
  - POST-3: The account creation is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC05_CreateUserAccount.png](../06-sequencediagram/user-management/UC05_CreateUserAccount.png)

#### Luồng sự kiện chính (Main Flow – Create User Account):
1. The System Admin selects Create User from the User Accounts screen.
2. The system displays the Create User form.
3. The System Admin enters the required user information.
4. The System Admin submits the user information.
5. The system validates the submitted information, including required fields and Username/Email validity and uniqueness (BR-13, BR-14).
6. The system creates the new user account with Active status (BR-15).
7. The system records the account creation in the audit log (BR-16).
8. The system displays MSG07 – "User account created successfully." and returns to the User Accounts list.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid User Information**
  1. At Step 5 of the Main Flow, the system determines that one or more submitted values are missing or invalid.
  2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.
  3. The System Admin corrects the information and resubmits the form.
  4. The flow resumes at Step 5 of the Main Flow.

- **EX-2: Duplicate User Account**
  1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.
  2. The system displays MSG08 – "Email is already in use."
  3. The System Admin changes the conflicting information and resubmits the form.
  4. The flow resumes at Step 5 of the Main Flow.

---

### UC-06: Update User Account (Cập nhật thông tin người dùng)

- **Use Case ID**: UC-06
- **Use Case Name**: Update User Account
- **Primary Actor**: System Admin, Workspace Manager
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin or Workspace Manager to update editable information of an existing user account within their permitted management scope.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin or Workspace Manager is authenticated in the system.
  - PRE-2: The System Admin or Workspace Manager is authorized to update the selected user account.
  - PRE-3: The selected user account exists.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected user account information is updated successfully.
  - POST-2: The update action is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC06_UpdateUserAccount.png](../06-sequencediagram/user-management/UC06_UpdateUserAccount.png)

#### Luồng sự kiện chính (Main Flow – Update User Account):
1. The System Admin or Workspace Manager selects a user account from the User Accounts list and selects Edit Account.
2. The system displays the current user account information.
3. The System Admin or Workspace Manager modifies the editable information, such as Full Name, Job Title, or Contact Information.
4. The System Admin or Workspace Manager submits the updated information.
5. The system validates the submitted information, including required fields and data format (BR-14, BR-17).
6. The system updates the user account information.
7. The system records the update in the audit log (BR-16).
8. The system displays MSG10 – "User account updated successfully." and returns to the User Accounts list.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid User Information**
  1. At Step 5 of the Main Flow, one or more submitted values are missing or invalid.
  2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.
  3. The System Admin or Workspace Manager corrects the information and resubmits it.
  4. The flow resumes at Step 5 of the Main Flow.

- **EX-2: Email Already in Use**
  1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.
  2. The system displays MSG08 – "Email is already in use."
  3. The System Admin or Workspace Manager changes the Email and resubmits the information.
  4. The flow resumes at Step 5 of the Main Flow.

---

### UC-07: Deactivate / Reactivate User Account (Khóa / Mở khóa tài khoản)

- **Use Case ID**: UC-07
- **Use Case Name**: Deactivate / Reactivate User Account
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to deactivate or reactivate a user account while preserving the user's historical records.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to manage user account status.
  - PRE-3: The selected user account exists.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The user account status is changed to Inactive when deactivated or Active when reactivated.
  - POST-2: A deactivated user can no longer access authenticated system functions.
  - POST-3: The status change is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC07_DeactivateReactivateUserAccount.png](../06-sequencediagram/user-management/UC07_DeactivateReactivateUserAccount.png)

#### Luồng sự kiện chính (Main Flow – Deactivate / Reactivate User Account):
1. The System Admin selects a user account from the User Accounts list.
2. The System Admin selects Deactivate Account or Reactivate Account.
3. The system requests confirmation of the account status change.
4. The System Admin confirms the action.
5. The system verifies that the requested status change is allowed (BR-03).
6. The system updates the user account status.
7. If the account is deactivated, the system ends the user's active sessions (BR-18).
8. The system records the status change in the audit log (BR-16).
9. The system displays MSG11 – "User account status changed successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Status Change Is Not Allowed**
  1. At Step 5 of the Main Flow, the system determines that the requested account status change is not allowed.
  2. The system displays MSG12 – "Unable to change the status of this user account."
  3. The account status remains unchanged.

- **EX-2: Invalid Status Request**
  1. At Step 5 of the Main Flow, the system determines that the request does not specify the account status to set.
  2. The account status remains unchanged.
  3. The system displays MSG09 – "Invalid account information."
  4. The use case ends unsuccessfully.

---

### UC-08: Reset User Password (Đặt lại mật khẩu người dùng)

- **Use Case ID**: UC-08
- **Use Case Name**: Reset User Password
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to initiate a password reset for an active user account by sending a secure password reset link to the user's registered email address.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to reset user passwords.
  - PRE-3: The selected user account exists and is active.
- **Điều kiện sau (Postconditions)**:
  - POST-1: A password reset request is created for the selected user.
  - POST-2: A password reset email is sent to the user's registered email address.
  - POST-3: The password reset request is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC08_ResetUserPassword.png](../06-sequencediagram/user-management/UC08_ResetUserPassword.png)

#### Luồng sự kiện chính (Main Flow – Reset User Password):
1. The System Admin selects a user account and selects Reset Password.
2. The system requests confirmation of the password reset.
3. The System Admin confirms the password reset.
4. The system creates a secure password reset request for the selected user (BR-20, BR-22).
5. The system sends a password reset email to the user's registered email address.
6. The system records the password reset request in the audit log (BR-16).
7. The system displays MSG13 – "Password reset email sent successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: User Account Is Not Eligible for Password Reset**
  1. Before completing Step 4 of the Main Flow, the system determines that the selected account cannot use the password reset function.
  2. The system displays MSG14 – "Password reset is not available for this account."
  3. The use case ends unsuccessfully.

- **EX-2: Password Reset Email Cannot Be Sent**
  1. At Step 5 of the Main Flow, the system cannot send the password reset email.
  2. The system displays MSG15 – "Unable to send the password reset email. Please try again."
  3. The use case ends unsuccessfully.

---

### UC-09: Assign System Role to User (Phân quyền vai trò hệ thống cho người dùng)

- **Use Case ID**: UC-09
- **Use Case Name**: Assign System Role to User
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to assign a system role to a user account or change the user's existing system role.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to assign system roles.
  - PRE-3: The selected user account exists.
  - PRE-4: At least one system role is available for assignment.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected user is associated with the selected system role.
  - POST-2: The user receives the capabilities associated with the selected system role.
  - POST-3: The role assignment or change is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC11_ConfigureRolePermissions.png](../06-sequencediagram/role-management/UC11_ConfigureRolePermissions.png)

#### Luồng sự kiện chính (Main Flow – Assign System Role to User):
1. The System Admin opens User Management.
2. The System Admin selects a user account.
3. The system displays the user information and the current system role, if any.
4. The System Admin selects Assign System Role.
5. The system displays the available system roles.
6. The System Admin selects a role and confirms the assignment.
7. The system assigns the selected role to the user.
8. The system records the role assignment in the audit log.
9. The system displays MSG10 – "User account updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Change Existing System Role**
  1. At Step 4 of the Main Flow, if the user already has a system role, the System Admin selects Change System Role.
  2. The System Admin selects a different role.
  3. The System Admin confirms the change.
  4. The system replaces the current role with the selected role.
  5. The system records the role change in the audit log.
  6. The system displays MSG10 – "User account updated successfully."

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Selected Role Is Not Available**
  1. The selected role is no longer available for assignment.
  2. The system displays MSG09 – "Invalid account information." with the field error "The role does not exist."
  3. No role assignment is changed.
  4. The use case ends.

- **EX-2: Last Administrator Cannot Be Demoted**
  1. At Step 7 of the Main Flow or Step 4 of AF-1, the system determines that, after the change, no active account would hold the user administration permission.
  2. The user keeps the previous role.
  3. The system displays MSG12 – "Unable to change the status of this user account."
  4. The use case ends unsuccessfully.

---

### FEATURE 3: QUẢN LÝ VAI TRÒ & PHÂN QUYỀN (ROLE MANAGEMENT)

---

### UC-10: View Role & Permission Matrix (Xem ma trận vai trò & quyền hạn)

- **Use Case ID**: UC-10
- **Use Case Name**: View Role & Permission Matrix
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to view the predefined roles and their default permissions.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated.
- **Điều kiện sau (Postconditions)**:
  - POST-1: No system data is modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC10_ViewRolePermissionMatrix.png](../06-sequencediagram/role-management/UC10_ViewRolePermissionMatrix.png)

#### Luồng sự kiện chính (Main Flow — View Role & Permission Matrix):
1. The System Admin accesses Role & Permission Management.
2. The system displays the Role & Permission Matrix.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Role and permission information cannot be retrieved**
  1. The system cannot retrieve the role and permission information.
  2. The system displays an error message.
  3. The use case ends unsuccessfully.

---

### UC-11: Configure Role Permissions (Cấu hình phân quyền vai trò)

- **Use Case ID**: UC-11
- **Use Case Name**: Configure Role Permissions
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to configure the default permissions assigned to a predefined system role.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to manage role permissions.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected role is associated with the updated set of default permissions.
  - POST-2: The permission configuration change is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC11_ConfigureRolePermissions.png](../06-sequencediagram/role-management/UC11_ConfigureRolePermissions.png)

#### Luồng sự kiện chính (Main Flow — Configure Role Permissions):
1. The System Admin accesses Role & Permission Management.
2. The system displays the Role & Permission Matrix.
3. The System Admin configures the permissions assigned to a predefined role.
4. The system validates and saves the updated permission configuration (BR-23).
5. The system records the configuration change in the audit log (BR-16).
6. The system displays MSG16 – "Role permissions updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Permission Configuration**
  1. At Step 4 of the Main Flow, the system determines that the updated permission configuration violates a permission rule.
  2. The system does not save the changes.
  3. The system displays MSG17 – "Invalid permission configuration."
  4. The use case ends unsuccessfully.

- **EX-2: Permission Configuration Cannot Be Saved**
  1. At Step 4 of the Main Flow, the system cannot save the updated permission configuration.
  2. The existing role permissions remain unchanged.
  3. The system displays MSG18 – "Unable to update role permissions. Please try again."
  4. The use case ends unsuccessfully.

---

### UC-12: Create Role (Tạo vai trò mới)

- **Use Case ID**: UC-12
- **Use Case Name**: Create Role
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to create a new custom role with a name, an optional description and an optional initial set of permissions, without changing code or redeploying the system.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to manage roles.
- **Điều kiện sau (Postconditions)**:
  - POST-1: A new custom role is created and appears in the role list and in the Role & Permission Matrix.
  - POST-2: The new role is associated with the selected permissions; the set may be empty.
  - POST-3: The role creation is recorded in the audit log.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC12_CreateRole.png](../06-sequencediagram/role-management/UC12_CreateRole.png)

#### Luồng sự kiện chính (Main Flow — Create Role):
1. The System Admin accesses Role & Permission Management.
2. The System Admin selects "Create Role".
3. The system displays the role creation form.
4. The System Admin enters the role name and an optional description, selects the permissions to grant to the role, and submits the form.
5. The system validates the role information: the name is required and has at most 64 characters after trimming, the description has at most 256 characters, and every selected permission exists in the permission catalog.
6. The system checks that no existing role, including the predefined roles, has the same name (case-insensitive).
7. The system saves the new role together with its permissions.
8. The system records the role creation in the audit log (BR-16).
9. The system displays MSG19 – "Role created successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Role Created Without Permissions**
  1. At Step 4 of the Main Flow, the System Admin does not select any permission.
  2. The system accepts an empty permission set; user accounts assigned to this role have the same access as a Member.
  3. The flow continues at Step 5 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Role Information**
  1. At Step 5 of the Main Flow, the system determines that the name is empty or longer than 64 characters, the description is longer than 256 characters, or a selected permission does not exist.
  2. The system does not save the role.
  3. The system displays MSG20 – "Invalid role information." and highlights the invalid fields.
  4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.

- **EX-2: Duplicate Role Name**
  1. At Step 6 or Step 7 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).
  2. The system does not save the role.
  3. The system displays MSG21 – "A role with this name already exists. Please choose a different name."
  4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.

- **EX-3: Role Cannot Be Saved**
  1. At Step 7 of the Main Flow, the system cannot save the new role.
  2. No role is created.
  3. The system displays MSG26 – "Unable to create the role. Please try again."
  4. The use case ends unsuccessfully.

---

### UC-13: Update Role (Cập nhật vai trò)

- **Use Case ID**: UC-13
- **Use Case Name**: Update Role
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to change the name and description of a custom role. Predefined roles cannot be renamed; the permissions of any role are configured through UC-11.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to manage roles.
  - PRE-3: At least one custom role exists in the system.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The name and/or description of the selected role are updated; its permissions and the user accounts holding it remain unchanged.
  - POST-2: The change is recorded in the audit log with the values before and after the update.
  - POST-3: No user session is terminated; users holding the role see the new name the next time their information is loaded.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC13_UpdateRole.png](../06-sequencediagram/role-management/UC13_UpdateRole.png)

#### Luồng sự kiện chính (Main Flow — Update Role):
1. The System Admin accesses Role & Permission Management.
2. The system displays the list of roles.
3. The System Admin selects "Edit" on a custom role.
4. The system displays the edit form pre-filled with the current name and description.
5. The System Admin modifies the name and/or description and submits the form.
6. The system verifies that the selected role exists and is a custom role.
7. The system validates the role information using the same rules as UC-12: the name is required and has at most 64 characters after trimming; the description has at most 256 characters.
8. The system checks that no other role has the same name (case-insensitive). Changing only the letter case of the role's own name is allowed.
9. The system saves the updated role.
10. The system records the change in the audit log (BR-16).
11. The system displays MSG22 – "Role updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: No Changes Submitted**
  1. At Step 7 of the Main Flow, the system determines that the submitted name and description are identical to the current values.
  2. The system does not modify any data and does not record an audit log entry.
  3. The system displays MSG22 – "Role updated successfully."
  4. The use case ends.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Predefined Role**
  1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.
  2. The system does not save the changes.
  3. The system displays MSG24 – "This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions."
  4. The use case ends unsuccessfully.

- **EX-2: Invalid Role Information**
  1. At Step 7 of the Main Flow, the system determines that the role information is invalid.
  2. The system does not save the changes.
  3. The system displays MSG20 – "Invalid role information." and highlights the invalid fields.
  4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.

- **EX-3: Duplicate Role Name**
  1. At Step 8 or Step 9 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).
  2. The system does not save the changes.
  3. The system displays MSG21 – "A role with this name already exists. Please choose a different name."
  4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.

- **EX-4: Role Cannot Be Saved**
  1. At Step 9 of the Main Flow, the system cannot save the updated role.
  2. The existing role information remains unchanged.
  3. The system displays MSG27 – "Unable to update the role. Please try again."
  4. The use case ends unsuccessfully.

---

### UC-14: Delete Role (Xóa vai trò)

- **Use Case ID**: UC-14
- **Use Case Name**: Delete Role
- **Primary Actor**: System Admin
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the System Admin to delete a custom role that is no longer held by any user account. Predefined roles cannot be deleted.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The System Admin is authenticated in the system.
  - PRE-2: The System Admin is authorized to manage roles.
  - PRE-3: At least one custom role exists in the system.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected role and its permission assignments are removed from the system.
  - POST-2: The deletion, including the role name and the permissions it held, is recorded in the audit log.
  - POST-3: No user account and no user session is affected.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC14_DeleteRole.png](../06-sequencediagram/role-management/UC14_DeleteRole.png)

#### Luồng sự kiện chính (Main Flow — Delete Role):
1. The System Admin accesses Role & Permission Management.
2. The system displays the list of roles with the number of user accounts holding each role.
3. The System Admin selects "Delete" on a custom role.
4. The system displays a confirmation dialog.
5. The System Admin confirms the deletion.
6. The system verifies that the selected role exists, is a custom role, and is not held by any user account, including deactivated accounts.
7. The system deletes the role together with its permission assignments.
8. The system records the role deletion in the audit log (BR-16).
9. The system displays MSG23 – "Role deleted successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Deletion Cancelled**
  1. At Step 5 of the Main Flow, the System Admin cancels the deletion.
  2. The system closes the confirmation dialog without changing any data.
  3. The use case ends.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Predefined Role**
  1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.
  2. The system does not delete the role.
  3. The system displays MSG24 – "This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions."
  4. The use case ends unsuccessfully.

- **EX-2: Role Still Held by User Accounts**
  1. At Step 6 or Step 7 of the Main Flow, the system determines that one or more user accounts, active or deactivated, still hold the role, including an account assigned to the role while the deletion was in progress.
  2. The system does not delete the role.
  3. The system displays MSG25 – "This role is still held by {n} user account(s). Reassign them to another role before deleting." in the confirmation dialog, where `{n}` is the number of user accounts holding the role.
  4. The use case ends unsuccessfully.

- **EX-3: Role Cannot Be Deleted**
  1. At Step 7 of the Main Flow, the system cannot delete the role.
  2. The role and its permission assignments remain unchanged.
  3. The system displays MSG28 – "Unable to delete the role. Please try again."
  4. The use case ends unsuccessfully.

---

### FEATURE 4: QUẢN LÝ CUỘC HỌP (MEETING MANAGEMENT)

---

### UC-15: Create Meeting (Tạo cuộc họp)

- **Use Case ID**: UC-15
- **Use Case Name**: Create Meeting
- **Primary Actor**: Meeting Organizer
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Organizer to create a new meeting using the required meeting-level information. Meeting Sessions are planned separately and are not required when the meeting is first created.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Organizer is authenticated.
  - PRE-2: The Meeting Organizer is authorized to create meetings.
- **Điều kiện sau (Postconditions)**:
  - POST-1: A new meeting is created successfully.
  - POST-2: The meeting is created with Draft status.
  - POST-3: The Meeting Organizer is recorded as the creator of the meeting.
  - POST-4: Meeting Sessions may be planned separately when their schedule becomes known.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC15_CreateMeeting.png](../06-sequencediagram/meeting-management/UC15_CreateMeeting.png)

#### Luồng sự kiện chính (Main Flow — Create Meeting):
1. The Meeting Organizer selects Create Meeting.
2. The system displays the meeting creation form.
3. The Meeting Organizer enters the meeting information, including:
   - Meeting Name
   - Planned Start Time
   - Planned End Time, if known (optional; when provided, it must be later than Planned Start Time)
   - Meeting Mode
   - Location
   - Confidentiality Level
   - Description
4. The Meeting Organizer submits the meeting information.
5. The system validates the entered information (BR-24, BR-25).
6. The system creates the meeting with Draft status (BR-26).
7. The system displays MSG29 – "Meeting created successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Meeting Information**
  1. At Step 5, one or more entered values are missing or invalid.
  2. The system identifies the invalid information.
  3. The system displays MSG31 – "Invalid meeting information." and the corresponding validation messages.
  4. The Meeting Organizer corrects the information and resubmits the form.
  5. The flow resumes at Step 5 of the Main Flow.

- **EX-2: Meeting Cannot Be Created**
  1. At Step 6, the system cannot complete the meeting creation.
  2. The system displays MSG35 – "Unable to create the meeting. Please try again."
  3. No meeting is created.
  4. The use case ends.

---

### UC-16: Update Meeting (Cập nhật thông tin cuộc họp)

- **Use Case ID**: UC-16
- **Use Case Name**: Update Meeting
- **Primary Actor**: Meeting Organizer
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Organizer to modify the meeting-level information of an existing meeting while the meeting is still editable. Meeting Session planning is handled separately.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Organizer is authenticated.
  - PRE-2: The selected meeting exists.
  - PRE-3: The Meeting Organizer is authorized to update the selected meeting.
  - PRE-4: At the time the edit action is initiated, the meeting is in Draft or Rejected status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The meeting information is updated successfully.
  - POST-2: The updated information is available when the meeting is viewed again.
  - POST-3: The meeting lifecycle status and current Meeting Approver assignment remain unchanged.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC16_UpdateMeeting.png](../06-sequencediagram/meeting-management/UC16_UpdateMeeting.png)

#### Luồng sự kiện chính (Main Flow — Update Meeting):
1. The Meeting Organizer selects a meeting and chooses Edit Meeting.
2. The system displays the current meeting information.
3. The Meeting Organizer submits the updated information.
4. The system validates the submitted information (BR-25, BR-27).
5. The system updates the meeting (BR-28).
6. The system displays MSG30 – "Meeting updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Meeting Information**
  1. At Step 4, the submitted information is invalid.
  2. The system displays MSG31 – "Invalid meeting information." and validation messages for the invalid fields.
  3. The Meeting Organizer corrects the information.
  4. The flow resumes at Step 4 of the Main Flow.

- **EX-2: Meeting Is No Longer Editable**
  1. At Step 4, the system determines that the meeting status has changed and the meeting is no longer Draft or Rejected.
  2. The system displays MSG33 – "This meeting can no longer be edited."
  3. The meeting remains unchanged.
  4. The use case ends.

- **EX-3: Meeting Cannot Be Updated**
  1. At Step 5, the system cannot complete the update.
  2. The previous meeting information remains unchanged.
  3. The system displays MSG36 – "Unable to update the meeting. Please try again."
  4. The use case ends.

---

### UC-17: View Meeting Details (Xem chi tiết cuộc họp)

- **Use Case ID**: UC-17
- **Use Case Name**: View Meeting Details
- **Primary Actor**: Meeting Organizer, Meeting Approver, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows a user associated with a meeting through an authorized meeting relationship or assignment to view the meeting details, its Meeting Sessions, and the agenda prepared for each session.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is authenticated.
  - PRE-2: A meeting has been selected or identified.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The meeting details, available Meeting Session schedule, and session agendas are displayed.
  - POST-2: No meeting information is modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC17_ViewMeetingDetails.png](../06-sequencediagram/meeting-management/UC17_ViewMeetingDetails.png)

#### Luồng sự kiện chính (Main Flow — View Meeting Details):
1. The user requests to view a meeting.
2. The system verifies that the meeting exists and that the user may view it under BR-29. The system does not reveal which check failed.
3. The system retrieves the meeting information, its Meeting Sessions, and the Agenda Items associated with those sessions.
4. The system displays the meeting details, including Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level, Description, Meeting Status, Meeting Session schedule, and the agenda of each session.
5. The user reviews the meeting information, session schedule, and agendas.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Meeting Does Not Exist or Is Not Accessible**
  1. At Step 2, the system cannot find the selected meeting or the user is not authorized to view it.
  2. The system does not disclose whether the meeting exists.
  3. The use case ends.

- **EX-2: Meeting Details Cannot Be Retrieved**
  1. At Step 4, the meeting exists and is accessible, but the system cannot retrieve its information.
  2. The system displays MSG37 – "Unable to load meeting details. Please try again."
  3. The use case ends.

---

### UC-18: View Meeting List (Xem danh sách cuộc họp)

- **Use Case ID**: UC-18
- **Use Case Name**: View Meeting List
- **Primary Actor**: Meeting Organizer, Meeting Approver, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is authenticated.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The system displays the list of meetings that the user is authorized to view.
  - POST-2: No meeting information is modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC18_ViewMeetingList.png](../06-sequencediagram/meeting-management/UC18_ViewMeetingList.png)

#### Luồng sự kiện chính (Main Flow — View Meeting List):
1. The user selects Meetings from the navigation menu.
2. The system identifies only meetings the user may view under BR-29: meetings created by the user at any status; meetings assigned to the user as Meeting Approver from WaitingApproval onward; and meetings where the user is assigned as Chair, Secretary, Member, or Guest from Published onward.
3. The system sorts and displays only those authorized meetings.
4. The user browses the displayed meetings.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Search Meetings**
  1. At Step 4 of the Main Flow, the user enters a meeting name in the search field.
  2. The system finds meetings matching the entered search criteria within the meetings available to the user.
  3. The system displays the matching meetings.
  4. The flow resumes at Step 4 of the Main Flow.

- **AF-2: Filter Meetings**
  1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status, Confidentiality Level, or Time Range.
  2. The system applies the selected criteria to the meetings available to the user.
  3. The system displays the matching meetings.
  4. The flow resumes at Step 4 of the Main Flow.

- **AF-3: No Matching Meetings**
  1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.
  2. The system displays an empty meeting list and the message "No meetings match the filter."
  3. The user may change or clear the current search/filter criteria.
  4. The flow resumes at Step 4 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Meeting List Cannot Be Retrieved**
  1. At Step 2 or Step 3 of the Main Flow, the system cannot retrieve the meeting list.
  2. The system displays MSG38 – "Unable to load meetings. Please try again."
  3. The use case ends.

- **EX-2: Invalid Meeting List Criteria**
  1. The system determines that the selected search, filter, sort, or display criteria are invalid.
  2. The system displays MSG32 – "Tiêu chí danh sách cuộc họp không hợp lệ."
  3. The user may clear or adjust the search/filter criteria.
  4. The use case ends.

---

### UC-19: Submit Meeting for Approval (Gửi duyệt cuộc họp)

- **Use Case ID**: UC-19
- **Use Case Name**: Submit Meeting for Approval
- **Primary Actor**: Meeting Organizer
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Organizer to submit a Draft meeting or resubmit a previously Rejected meeting for review by an assigned Meeting Approver based on the meeting-level information.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Organizer is authenticated.
  - PRE-2: The selected meeting exists.
  - PRE-3: The Meeting Organizer is authorized to submit the selected meeting for approval.
  - PRE-4: The meeting is in Draft or Rejected status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The meeting status is WaitingApproval.
  - POST-2: A valid Meeting Approver is assigned to the current approval cycle.
  - POST-3: The assigned Meeting Approver is granted access to the meeting for approval purposes.
  - POST-4: The submission is recorded in the meeting lifecycle history.
  - POST-5: The assigned Meeting Approver is notified of the pending approval request.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC19_SubmitMeetingForApproval.png](../06-sequencediagram/meeting-management/UC19_SubmitMeetingForApproval.png)

#### Luồng sự kiện chính (Main Flow — Submit Meeting for Approval):
1. The Meeting Organizer selects a Draft meeting and chooses Submit for Approval.
2. The system verifies that all mandatory meeting-level information is complete. Meeting Sessions are not required for approval submission. (BR-33, BR-55)
3. The system identifies users who are eligible to act as Meeting Approvers.
4. The system displays the eligible Meeting Approvers.
5. The Meeting Organizer selects a Meeting Approver.
6. The system verifies that the selected user is eligible to review the meeting and is not the Meeting Organizer.
7. The system verifies that the meeting is still in Draft or Rejected status.
8. The system assigns the selected Meeting Approver to the current approval cycle.
9. The system changes the meeting status to WaitingApproval.
10. The system records the submission and assigned approver in the meeting lifecycle history.
11. The system notifies the assigned Meeting Approver.
12. The system displays the submission success message.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Resubmit a Rejected Meeting**
  1. At Step 1 of the Main Flow, the Meeting Organizer selects a meeting in Rejected status.
  2. The Meeting Organizer chooses Resubmit for Approval.
  3. The flow resumes at Step 2 of the Main Flow and completes successfully when the submission requirements are satisfied.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Incomplete Meeting-Level Information**
  1. At Step 2, the system determines that mandatory meeting-level information is incomplete.
  2. The system identifies the missing or invalid information.
  3. The meeting remains Draft or Rejected and the use case ends.

- **EX-2: Meeting Status Not Eligible for Submission**
  1. At Step 7, the system determines that the meeting is not in Draft or Rejected status.
  2. The system displays MSG41 – "This meeting can no longer be submitted for approval."
  3. The use case ends.

- **EX-3: No Valid Meeting Approver**
  1. At Step 6, the system determines that the selected user is not eligible to review the meeting or is the Meeting Organizer.
  2. The meeting remains Draft or Rejected and no approval assignment is created.
  3. The Meeting Organizer selects another eligible approver.

- **EX-4: Submission Cannot Be Completed**
  1. At Step 8 or Step 9, the approval assignment or status change cannot be completed.
  2. The meeting remains in its previous status and no approval notification is sent.
  3. The system displays MSG42 – "Unable to submit the meeting for approval. Please try again."
  4. The use case ends.

---

### UC-20: Review Meeting Approval (Phê duyệt / Từ chối cuộc họp)

- **Use Case ID**: UC-20
- **Use Case Name**: Review Meeting Approval
- **Primary Actor**: Meeting Approver
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Approver is authenticated.
  - PRE-2: The selected meeting exists.
  - PRE-3: The Meeting Approver is currently assigned to review the selected meeting and is not its Meeting Organizer.
  - PRE-4: The meeting is in WaitingApproval status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The Meeting Approver's decision is recorded.
  - POST-2: If approved, the meeting status is Approved.
  - POST-3: If rejected, the meeting status is Rejected.
  - POST-4: If rejected, the rejection reason is recorded and available to the Secretary.
  - POST-5: The approval or rejection is recorded in the meeting lifecycle history.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC20_ReviewMeetingApproval.png](../06-sequencediagram/meeting-management/UC20_ReviewMeetingApproval.png)

#### Luồng sự kiện chính (Main Flow — Review Meeting Approval):
1. The Meeting Approver selects a meeting assigned to them that is awaiting approval.
2. The system verifies that the current user is the assigned Meeting Approver.
3. The system displays the meeting information for review.
4. The Meeting Approver selects Approve.
5. The system re-verifies that:
   - the Meeting Approver is still authorized to approve the selected meeting; and
   - the meeting is still in WaitingApproval status.
6. The system changes the meeting status from WaitingApproval to Approved.
7. The system records the approval decision, acting user, and timestamp in the meeting lifecycle history.
8. The system displays MSG43 – "Meeting approved successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Reject Meeting**
  1. At Step 4 of the Main Flow, the Meeting Approver selects Reject.
  2. The system requests a rejection reason.
  3. The Meeting Approver enters the rejection reason.
  4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters.
  5. The system re-verifies that:
     - the Meeting Approver is still authorized to reject the selected meeting; and
     - the meeting is still in WaitingApproval status.
  6. The system changes the meeting status to Rejected.
  7. The system records the rejection decision, rejection reason, acting user, and timestamp.
  8. The rejection reason becomes available to the Secretary.
  9. The system displays MSG44 – "Meeting rejected successfully."

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Rejection Reason Is Missing**
  1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-36)
  2. The system displays MSG83 – "Rejection reason is required."
  3. The meeting remains in WaitingApproval status.
  4. The Meeting Approver enters a valid rejection reason.
  5. The flow resumes at Step 4 of AF-1.

- **EX-5: Rejection Reason Is Too Long**
  1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.
  2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."
  3. The meeting remains in WaitingApproval status.
  4. The Meeting Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1.

- **EX-2: Meeting Is No Longer Awaiting Approval**
  1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status.
  2. No approval or rejection decision is applied.
  3. The meeting remains in its current status.
  4. The system displays "This meeting is no longer awaiting approval."
  5. The use case ends.

- **EX-3: Approval Decision Cannot Be Completed**
  1. At Step 6 of the Main Flow or Step 6 of AF-1, the system cannot complete the requested approval or rejection decision.
  2. The requested status transition is not applied.
  3. No successful approval or rejection decision is recorded in the meeting lifecycle history.
  4. The system displays "Unable to complete the approval decision. Please try again."
  5. The use case ends.

- **EX-4: User Is No Longer Authorized to Review the Meeting**
  1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the Meeting Approver is no longer authorized to approve or reject the selected meeting. (BR-35)
  2. No approval or rejection decision is applied.
  3. The meeting status remains unchanged.
  4. The system informs the Meeting Approver that they are no longer authorized to review the meeting.
  5. The use case ends.

---

### UC-21: Publish Meeting (Phát hành cuộc họp)

- **Use Case ID**: UC-21
- **Use Case Name**: Publish Meeting
- **Primary Actor**: Meeting Organizer
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Organizer to publish an Approved meeting after a valid Meeting Session schedule has been prepared and initiate invitation delivery to the meeting participants.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Organizer is authenticated.
  - PRE-2: The selected meeting exists.
  - PRE-3: The Meeting Organizer is authorized to publish the selected meeting.
  - PRE-4: The meeting is in Approved status.
  - PRE-5: The meeting contains at least one valid Planned Meeting Session. A valid session belongs to the meeting, is in Planned status, has a non-blank title within the allowed length, and has valid planned start/end times with the end after the start. (BR-56)
- **Điều kiện sau (Postconditions)**:
  - POST-1: The meeting status is Published.
  - POST-2: The publication is recorded in the meeting lifecycle history.
  - POST-3: The system attempts to initiate email invitation delivery for active participants when delivery is available (BR-39, BR-40).
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC21_PublishMeeting.png](../06-sequencediagram/meeting-management/UC21_PublishMeeting.png)

#### Luồng sự kiện chính (Main Flow — Publish Meeting):
1. The Meeting Organizer selects an Approved meeting.
2. The Meeting Organizer selects Publish Meeting.
3. The system verifies that the Meeting Organizer remains authorized for the selected meeting, that the meeting is still Approved, and that at least one valid Planned Meeting Session exists. (BR-37, BR-56)
4. The system changes the meeting status from Approved to Published. (BR-37)
5. The system records the publication action, acting user, and timestamp in the meeting lifecycle history. (BR-38)
6. The system attempts to initiate email invitation delivery for active participants. The meeting remains Published if email delivery is unavailable.
7. The system displays MSG45 – "Meeting published successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Meeting Is No Longer Eligible for Publication**
  1. At Step 3 of the Main Flow, the system determines that the meeting is no longer Approved or the Meeting Organizer is no longer authorized to publish it.
  2. The meeting remains unchanged.
  3. Invitation delivery is not initiated.
  4. The system displays MSG46 – "This meeting can no longer be published."
  5. The use case ends.

- **EX-2: Meeting Session Schedule Is Not Ready**
  1. At Step 3 of the Main Flow, the system determines that the meeting has no valid Planned Meeting Session. (BR-56)
  2. The meeting remains Approved.
  3. The system displays MSG90 – "At least one valid planned Meeting Session is required before publication."
  4. Invitation delivery is not initiated.
  5. The use case ends.

- **EX-3: Meeting Cannot Be Published**
  1. At Step 4 of the Main Flow, the system cannot complete the publication.
  2. The meeting remains Approved.
  3. Invitation delivery is not initiated.
  4. The system displays MSG47 – "Unable to publish the meeting. Please try again."
  5. The use case ends.

---

### FEATURE 5: QUẢN LÝ THÔNG BÁO (NOTIFICATION MANAGEMENT)

---

### UC-22: View Notifications (Xem danh sách thông báo)

- **Use Case ID**: UC-22
- **Use Case Name**: View Notifications
- **Primary Actor**: Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows an authenticated user to view their list of in-app notifications and check the unread notification count.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is authenticated in the system.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The system displays the user's notifications and unread badge count.
  - POST-2: Opening the notification list does not change any notification's read status.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC22_ViewNotifications.png](../06-sequencediagram/notification-management/UC22_ViewNotifications.png)

#### Luồng sự kiện chính (Main Flow — View Notifications):
1. The user selects the notification bell from the navigation header.
2. The system retrieves the notifications and unread badge count for the user's account.
3. The system displays the user's notifications, indicates which ones are unread, and shows the unread count. (BR-48, BR-49)

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Notifications Cannot Be Retrieved**
  1. At Step 2 of the Main Flow, the system cannot load notification records.
  2. The system displays MSG40 – "Unable to load notifications. Please try again."
  3. The use case ends unsuccessfully.

- **EX-2: Requested Notification Page Is Unavailable**
  1. At Step 2 of the Main Flow, the system cannot display the requested portion of the notification list.
  2. The system displays MSG39 – "The requested notification page is unavailable."
  3. The use case ends unsuccessfully.

---

### UC-23: Mark Notification as Read (Đánh dấu đã đọc thông báo)

- **Use Case ID**: UC-23
- **Use Case Name**: Mark Notification as Read
- **Primary Actor**: Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows an authenticated user to mark a specific unread notification as read, updating its status.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is authenticated in the system.
  - PRE-2: The user has at least one unread notification.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected notification status is changed to Read.
  - POST-2: The unread count reflects the latest saved notification statuses.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC23_MarkNotificationAsRead.png](../06-sequencediagram/notification-management/UC23_MarkNotificationAsRead.png)

#### Luồng sự kiện chính (Main Flow — Mark Notification as Read):
1. The user views their notification list.
2. The user selects "Mark as Read" for a notification.
3. The system verifies that the notification belongs to the user's account. (BR-47)
4. The system marks the notification as read.
5. The system displays the notification as read and shows the updated unread count.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- N/A

---

### UC-24: Mark All Notifications as Read (Đánh dấu đã đọc tất cả thông báo)

- **Use Case ID**: UC-24
- **Use Case Name**: Mark All Notifications as Read
- **Primary Actor**: Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows a user to mark all of their unread notifications as read.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is signed in.
- **Điều kiện sau (Postconditions)**:
  - POST-1: All notifications belonging to the user's account that were unread when the request was processed are marked as read.
  - POST-2: The displayed unread count reflects the latest saved state.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC24_MarkAllNotificationsAsRead.png](../06-sequencediagram/notification-management/UC24_MarkAllNotificationsAsRead.png)

#### Luồng sự kiện chính (Main Flow — Mark All Notifications as Read):
1. The user selects the notification bell and opens the notification list.
2. The user selects "Mark All as Read".
3. The system marks all unread notifications belonging to the user's account as read, including those not currently displayed. (BR-47, BR-51)
4. The system refreshes the displayed read statuses and unread count. (BR-48)

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: No Unread Notifications**
  1. At Step 3 of the Main Flow, the system finds no unread notifications belonging to the user's account.
  2. The system displays the current list and an unread count of zero.
  3. The use case ends successfully.

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- N/A

---

### UC-25: Open Notification Target (Mở đối tượng liên kết thông báo)

- **Use Case ID**: UC-25
- **Use Case Name**: Open Notification Target
- **Primary Actor**: Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows a user to open the meeting linked to one of their notifications.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The user is signed in.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The user sees the linked meeting.
  - POST-2: The selected notification is marked as read if successfully saved.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC25_OpenNotificationTarget.png](../06-sequencediagram/notification-management/UC25_OpenNotificationTarget.png)

#### Luồng sự kiện chính (Main Flow — Open Notification Target):
1. The user selects the notification bell and clicks on a specific notification.
2. The system checks that the notification belongs to the user's account and identifies its linked meeting. (BR-47, BR-52)
3. The system marks the notification as read if needed and updates the unread count. (BR-48, BR-50, BR-53)
4. The system checks whether the user currently has permission to view the linked meeting. (BR-29, BR-52)
5. The system opens the meeting.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Notification Is Already Read**
  1. At Step 3 of the Main Flow, the notification is already read, so its status and the unread count remain unchanged. (BR-50)
  2. The flow resumes at Step 4 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- N/A

---

### FEATURE 6: QUẢN LÝ PHIÊN HỌP (MEETING SESSION MANAGEMENT)

---

### UC-26: Create Meeting Session (Tạo phiên họp)

- **Use Case ID**: UC-26
- **Use Case Name**: Create Meeting Session
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to create a Planned Meeting Session for a meeting, including an additional future session after publication when operationally required.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated.
  - PRE-2: The selected meeting exists and the Secretary is assigned to it.
  - PRE-3: The meeting is in Draft, Rejected, Approved, Published, or InProgress status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: A new Meeting Session is associated with the selected meeting.
  - POST-2: The Meeting Session is in Planned status.
  - POST-3: The meeting lifecycle status remains unchanged.
  - POST-4: If the meeting is Published or InProgress, affected participants are notified of the session schedule change.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC26_CreateMeetingSession.png](../06-sequencediagram/meeting-session-management/UC26_CreateMeetingSession.png)

#### Luồng sự kiện chính (Main Flow — Create Meeting Session):
1. The Secretary opens the selected meeting.
2. The Secretary selects Add Session.
3. The system displays the Meeting Session information form.
4. The Secretary enters the session title, planned start time, and planned end time.
5. The Secretary submits the session information.
6. The system validates the session information and planned time rules. (BR-62, BR-75)
7. The system verifies that the Secretary is still assigned to the meeting and that the meeting is open for session planning. (BR-54)
8. The system creates the Meeting Session in Planned status. (BR-58)
9. If the meeting is Published or InProgress, the system notifies affected participants of the schedule change. (BR-76)
10. The system displays MSG48 – "Meeting session created successfully." and shows the new Meeting Session in the meeting schedule.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Session Information**
  1. At Step 6, required information is missing or invalid, the planned end time is not later than the planned start time, or a Published/InProgress meeting is given a session start time that is not in the future. (BR-62, BR-75)
  2. The system displays MSG53 – "Invalid meeting session information." and identifies the invalid information.
  3. The Secretary corrects the information and resubmits it.
  4. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Meeting Is Not Open for Session Planning**
  1. At Step 7, the parent meeting is in WaitingApproval or Ended status, or the Secretary is no longer assigned to the meeting. (BR-54)
  2. No Meeting Session is created.
  3. The system displays MSG54 – "Meeting sessions can no longer be added to this meeting."
  4. The use case ends.

---

### UC-27: Update Meeting Session (Cập nhật phiên họp)

- **Use Case ID**: UC-27
- **Use Case Name**: Update Meeting Session
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to update or reschedule a Planned Meeting Session while that session is still eligible for planning, including after the meeting has been published.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated.
  - PRE-2: The selected Meeting Session exists and belongs to a meeting to which the Secretary is assigned.
  - PRE-3: The parent meeting is in Draft, Rejected, Approved, Published, or InProgress status.
  - PRE-4: The Meeting Session is in Planned status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Meeting Session contains the updated information.
  - POST-2: The Meeting Session remains Planned.
  - POST-3: Other Meeting Sessions remain unchanged.
  - POST-4: If the meeting is Published or InProgress, affected participants are notified of the session schedule change.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC27_UpdateMeetingSession.png](../06-sequencediagram/meeting-session-management/UC27_UpdateMeetingSession.png)

#### Luồng sự kiện chính (Main Flow — Update Meeting Session):
1. The Secretary opens the meeting session schedule.
2. The Secretary selects a Planned Meeting Session and chooses Edit.
3. The system displays the current session information.
4. The Secretary modifies the session information.
5. The Secretary submits the changes.
6. The system validates the updated session information and planned time rules. (BR-62, BR-75)
7. The system verifies that the Secretary is still assigned, the parent meeting remains open for session planning, and the Meeting Session is still Planned. (BR-54, BR-57, BR-58)
8. The system updates the Meeting Session while keeping it in Planned status.
9. If the meeting is Published or InProgress, the system notifies affected participants of the schedule change. (BR-76)
10. The system displays MSG49 – "Meeting session updated successfully." and shows the updated session information.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Session Information**
  1. At Step 6, the updated information is incomplete or invalid, or a Published/InProgress meeting is given a session start time that is not in the future. (BR-62, BR-75)
  2. The system displays MSG53 – "Invalid meeting session information." and identifies the invalid information.
  3. The Secretary corrects the information and resubmits it.
  4. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Session Can No Longer Be Updated**
  1. At Step 7, the parent meeting is in WaitingApproval or Ended status, the Secretary is no longer assigned to the meeting, or the selected Meeting Session is no longer Planned. (BR-54, BR-57, BR-58)
  2. The existing Meeting Session remains unchanged.
  3. The system displays MSG55 – "This meeting session can no longer be edited."
  4. The use case ends.

---

### UC-28: Cancel Meeting Session (Hủy phiên họp)

- **Use Case ID**: UC-28
- **Use Case Name**: Cancel Meeting Session
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to cancel a Planned Meeting Session that will no longer take place while preserving the session record for traceability.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated.
  - PRE-2: The selected Meeting Session exists and belongs to a meeting to which the Secretary is assigned.
  - PRE-3: The parent meeting is in Draft, Rejected, Approved, Published, or InProgress status.
  - PRE-4: The Meeting Session is in Planned status.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Meeting Session is in Cancelled status and remains associated with the meeting for traceability.
  - POST-2: Other Meeting Sessions remain unchanged.
  - POST-3: If the meeting is InProgress and no Meeting Session remains Planned or InProgress, the meeting is Ended.
  - POST-4: If the meeting is Published or InProgress, affected participants are notified of the cancellation.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC28_CancelMeetingSession.png](../06-sequencediagram/meeting-session-management/UC28_CancelMeetingSession.png)

#### Luồng sự kiện chính (Main Flow — Cancel Meeting Session):
1. The Secretary selects a Planned Meeting Session.
2. The Secretary chooses Cancel Session.
3. The system requests confirmation.
4. The Secretary confirms the cancellation.
5. The system verifies that the Secretary is still assigned, the parent meeting is open for session planning, and the selected Meeting Session is still Planned. (BR-54, BR-57, BR-58)
6. If the meeting is Published, the system verifies that at least one other Planned Meeting Session will remain after the cancellation. (BR-77)
7. The system changes the Meeting Session status from Planned to Cancelled and retains the session record. (BR-57, BR-58)
8. If the meeting is InProgress and no Meeting Session remains Planned or InProgress, the system changes the meeting status to Ended and records the lifecycle transition. (BR-38, BR-61)
9. If the meeting is Published or InProgress, the system notifies affected participants of the cancellation. (BR-76)
10. The system displays MSG50 – "Meeting session cancelled successfully." and shows the updated meeting schedule.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Session Can No Longer Be Cancelled**
  1. At Step 5, the parent meeting is in WaitingApproval or Ended status, the Secretary is no longer assigned to the meeting, or the selected Meeting Session is no longer Planned. (BR-54, BR-57, BR-58)
  2. The Meeting Session remains unchanged.
  3. The system displays MSG56 – "This meeting session can no longer be cancelled."
  4. The use case ends.

- **EX-2: Last Planned Session of a Published Meeting**
  1. At Step 6, cancelling the selected Meeting Session would leave a Published meeting with no Planned Meeting Session. (BR-77)
  2. The Meeting Session remains Planned.
  3. The system displays MSG77 – "The last planned meeting session of a published meeting cannot be cancelled."
  4. The use case ends.

---

### UC-29: Start Meeting Session (Bắt đầu phiên họp)

- **Use Case ID**: UC-29
- **Use Case Name**: Start Meeting Session
- **Primary Actor**: Chair
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Chair to start a planned Meeting Session so that the activities scheduled for that session can begin.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Chair is authenticated and assigned to the selected meeting.
  - PRE-2: The meeting is Published or InProgress.
  - PRE-3: The selected Meeting Session is Planned.
  - PRE-4: No other Meeting Session of the same meeting is currently InProgress.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Meeting Session is InProgress.
  - POST-2: The actual start time is recorded.
  - POST-3: If the meeting was Published, the meeting is now InProgress.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC29_StartMeetingSession.png](../06-sequencediagram/meeting-session-management/UC29_StartMeetingSession.png)

#### Luồng sự kiện chính (Main Flow — Start Meeting Session):
1. The Chair opens the selected Meeting Session.
2. The Chair selects Start Session.
3. The system verifies that the Chair is still assigned to the meeting, the Meeting Session can be started, and no other Meeting Session of the same meeting is InProgress. (BR-58, BR-59, BR-63)
4. The system changes the Meeting Session status to InProgress. (BR-58)
5. The system records the actual start time.
6. If the meeting is Published, the system changes the meeting status to InProgress. (BR-60)
7. The system displays MSG51 – "Meeting session started successfully." and shows the selected Meeting Session as the current active session.

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Another Session Is In Progress**
  1. At Step 3, another Meeting Session of the same meeting is already InProgress. (BR-59)
  2. The selected Meeting Session remains Planned.
  3. The system displays MSG57 – "Another meeting session is already in progress. End it before starting another session."
  4. The use case ends.

- **EX-2: Session Cannot Be Started**
  1. At Step 3, the meeting or selected Meeting Session no longer satisfies the start conditions, or the Chair is no longer assigned to the meeting. (BR-58, BR-63)
  2. The Meeting Session remains unchanged.
  3. The system displays MSG58 – "This meeting session cannot be started."
  4. The use case ends.

---

### UC-30: End Meeting Session (Kết thúc phiên họp)

- **Use Case ID**: UC-30
- **Use Case Name**: End Meeting Session
- **Primary Actor**: Chair
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Chair to end the current Meeting Session so that its completion is recorded and the meeting can continue with another session or conclude.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Chair is authenticated and assigned to the selected meeting.
  - PRE-2: The selected Meeting Session is InProgress.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Meeting Session is Completed.
  - POST-2: The actual end time is recorded.
  - POST-3: If no Meeting Session remains Planned or InProgress, the meeting is Ended.
  - POST-4: If a Planned or InProgress Meeting Session remains, the meeting stays InProgress.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC30_EndMeetingSession.png](../06-sequencediagram/meeting-session-management/UC30_EndMeetingSession.png)

#### Luồng sự kiện chính (Main Flow — End Meeting Session):
1. The Chair selects End Session for the current Meeting Session.
2. The system requests confirmation.
3. The Chair confirms the action.
4. The system verifies that the Chair is still assigned to the meeting and that the selected Meeting Session is still InProgress. (BR-58, BR-63)
5. The system changes the Meeting Session status to Completed and records the actual end time. (BR-58)
6. The system checks whether any Meeting Session remains Planned or InProgress.
7. If none remains, the system changes the meeting status to Ended. (BR-61)
8. The system displays MSG52 – "Meeting session ended successfully." and shows the updated Meeting Session status.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Other Meeting Sessions Remain**
  1. At Step 6 of the Main Flow, one or more Meeting Sessions remain Planned or InProgress.
  2. The meeting remains InProgress.
  3. The current Meeting Session remains successfully Completed.
  4. The use case ends successfully.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Session Cannot Be Ended**
  1. At Step 4, the selected Meeting Session is no longer InProgress or the Chair is no longer assigned to the meeting. (BR-58, BR-63)
  2. The Meeting Session remains unchanged.
  3. The system displays MSG59 – "This meeting session cannot be ended."
  4. The use case ends.

---

### FEATURE 7: QUẢN LÝ CHƯƠNG TRÌNH NGHỊ SỰ (AGENDA MANAGEMENT)

---

### UC-31: Create Agenda Item (Tạo mục chương trình nghị sự)

- **Use Case ID**: UC-31
- **Use Case Name**: Create Agenda Item
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to add a new Agenda Item to a planned Meeting Session, specifying title, planned duration, optional presenter, optional description, and attached meeting documents.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session exists and belongs to the selected meeting.
  - PRE-3: Selected Meeting Session is Planned.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The new Agenda Item belongs to the selected Meeting Session.
  - POST-2: The new Agenda Item starts in Pending.
  - POST-3: The new Agenda Item appears at the end of that Meeting Session agenda.
  - POST-4: Selected meeting documents are associated with the Agenda Item.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC31_CreateAgendaItem.png](../06-sequencediagram/agenda-management/UC31_CreateAgendaItem.png)

#### Luồng sự kiện chính (Main Flow — Create Agenda Item):
1. The Secretary opens the agenda of a selected Meeting Session.
2. The Secretary selects Add Agenda Item.
3. The system displays the Agenda Item information form.
4. The Secretary enters the agenda information, including:
   - Title
   - Planned Duration
   - optional Presenter
   - optional Description
   - any meeting documents to be attached
5. The Secretary submits the Agenda Item information.
6. The system validates the submitted information and selected documents. (BR-65, BR-66, BR-74)
7. The system creates the Agenda Item in Pending status, associates it with the selected Meeting Session, and places it at the end of the session agenda. (BR-64, BR-67)
8. The system displays MSG60 – "Agenda item created successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Agenda Item Information**
  1. At Step 6, the submitted Agenda Item information violates BR-66.
  2. The system displays MSG66 – "Invalid agenda item information." with the applicable field message(s):
     - "Agenda item title is required."
     - "Planned duration must be greater than 0."
     - "One or more selected documents are not available to this meeting."
  3. The Secretary corrects the invalid information.
  4. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Agenda Can No Longer Be Modified**
  1. At Step 6, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda planning. (BR-65, BR-68, BR-74)
  2. The Agenda Item is not created.
  3. The system displays MSG67 – "The agenda for this meeting session can no longer be modified."
  4. The use case ends.

- **EX-3: Agenda Item Cannot Be Created**
  1. At Step 7, the system cannot create the Agenda Item.
  2. The Agenda Item is not created.
  3. The system displays MSG68 – "Unable to create the agenda item. Please try again."
  4. The use case ends.

---

### UC-32: Update Agenda Item (Cập nhật mục chương trình nghị sự)

- **Use Case ID**: UC-32
- **Use Case Name**: Update Agenda Item
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to update the information and attached documents of an existing Agenda Item in a planned Meeting Session.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.
  - PRE-3: Selected Agenda Item exists, belongs to the Meeting Session, and is Pending.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The Agenda Item information and attached documents are updated.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC32_UpdateAgendaItem.png](../06-sequencediagram/agenda-management/UC32_UpdateAgendaItem.png)

#### Luồng sự kiện chính (Main Flow — Update Agenda Item):
1. The Secretary opens the agenda of a selected Meeting Session.
2. The Secretary selects Edit for the target Agenda Item.
3. The system displays the Agenda Item edit form with current information (Title, Planned Duration, Presenter, Description, attached documents).
4. The Secretary modifies the information (Title, Planned Duration, optional Presenter, optional Description, attached meeting documents).
5. The Secretary submits the updated Agenda Item information.
6. The system validates the updated information and attached documents. (BR-65, BR-66, BR-74)
7. The system saves the updated Agenda Item information. (BR-68)
8. The system displays MSG61 – "Agenda item updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Agenda Item Information**
  1. At Step 6, the updated Agenda Item information violates BR-66.
  2. The system displays MSG66 – "Invalid agenda item information." with the applicable field message(s):
     - "Agenda item title is required."
     - "Planned duration must be greater than 0."
     - "One or more selected documents are not available to this meeting."
  3. The Secretary corrects the invalid information.
  4. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Agenda Item Can No Longer Be Updated**
  1. At Step 6, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda planning. (BR-65, BR-68, BR-74)
  2. The Agenda Item remains unchanged.
  3. The system displays MSG67 – "The agenda for this meeting session can no longer be modified."
  4. The use case ends.

- **EX-3: Agenda Item Cannot Be Updated**
  1. At Step 7, the system cannot update the Agenda Item.
  2. The Agenda Item remains unchanged.
  3. The system displays MSG69 – "Unable to update the agenda item. Please try again."
  4. The use case ends.

---

### UC-33: Delete Agenda Item (Xóa mục chương trình nghị sự)

- **Use Case ID**: UC-33
- **Use Case Name**: Delete Agenda Item
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to remove an Agenda Item from a planned Meeting Session.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.
  - PRE-3: Selected Agenda Item exists, belongs to the Meeting Session, and is Pending.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The Agenda Item is removed from the Meeting Session agenda.
  - POST-2: Remaining Agenda Items in the session retain contiguous explicit ordering.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC33_DeleteAgendaItem.png](../06-sequencediagram/agenda-management/UC33_DeleteAgendaItem.png)

#### Luồng sự kiện chính (Main Flow — Delete Agenda Item):
1. The Secretary opens the agenda of a selected Meeting Session.
2. The Secretary selects Delete for the target Agenda Item.
3. The system requests confirmation.
4. The Secretary confirms the deletion.
5. The system verifies that the Secretary is assigned to the meeting, the Meeting Session is Planned, and the Agenda Item is Pending. (BR-65, BR-68, BR-74)
6. The system removes the Agenda Item and re-indexes remaining items to preserve explicit contiguous ordering.
7. The system displays MSG62 – "Agenda item deleted successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Agenda Item Can No Longer Be Deleted**
  1. At Step 5, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda modification. (BR-65, BR-68, BR-74)
  2. The Agenda Item is not deleted.
  3. The system displays MSG67 – "The agenda for this meeting session can no longer be modified."
  4. The use case ends.

- **EX-2: Agenda Item Cannot Be Deleted**
  1. At Step 6, the system cannot delete the Agenda Item.
  2. The Agenda Item remains unchanged.
  3. The system displays MSG70 – "Unable to delete the agenda item. Please try again."
  4. The use case ends.

---

### UC-34: Reorder Agenda Items (Sắp xếp lại thứ tự mục chương trình nghị sự)

- **Use Case ID**: UC-34
- **Use Case Name**: Reorder Agenda Items
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to reorder Agenda Items within a planned Meeting Session.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.
  - PRE-3: Selected Meeting Session contains two or more Agenda Items.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The new explicit order of Agenda Items is saved within the Meeting Session.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC34_ReorderAgendaItems.png](../06-sequencediagram/agenda-management/UC34_ReorderAgendaItems.png)

#### Luồng sự kiện chính (Main Flow — Reorder Agenda Items):
1. The Secretary opens the agenda of a selected Meeting Session.
2. The Secretary reorders the Agenda Items.
3. The Secretary submits the new agenda order.
4. The system validates that all reordered items belong to the same Meeting Session, the Secretary is assigned to the meeting, and the Meeting Session is Planned. (BR-64, BR-65, BR-67, BR-74)
5. The system saves the updated explicit order of the Agenda Items. (BR-67, BR-68)
6. The system displays MSG63 – "Agenda order updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Agenda Order Can No Longer Be Modified**
  1. At Step 4, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda modification. (BR-65, BR-68, BR-74)
  2. The agenda order remains unchanged.
  3. The system displays MSG67 – "The agenda for this meeting session can no longer be modified."
  4. The use case ends.

- **EX-2: Agenda Order Cannot Be Updated**
  1. At Step 5, the system cannot update the agenda order.
  2. The agenda order remains unchanged.
  3. The system displays MSG71 – "Unable to update the agenda order. Please try again."
  4. The use case ends.

---

### UC-35: Set Current Agenda Item (Đặt mục chương trình nghị sự hiện tại)

- **Use Case ID**: UC-35
- **Use Case Name**: Set Current Agenda Item
- **Primary Actor**: Chair
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Chair to set a Pending Agenda Item as the active/current item of an in-progress Meeting Session, recording its actual start time.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Chair is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session is InProgress.
  - PRE-3: Selected Agenda Item belongs to the Meeting Session and is Pending.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Agenda Item is InProgress and becomes the current Agenda Item of the Meeting Session.
  - POST-2: The actual start time of the Agenda Item is recorded.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC35_SetCurrentAgendaItem.png](../06-sequencediagram/agenda-management/UC35_SetCurrentAgendaItem.png)

#### Luồng sự kiện chính (Main Flow — Set Current Agenda Item):
1. The Chair opens the in-progress Meeting Session agenda.
2. The Chair selects Start / Set as Current for a Pending Agenda Item.
3. The system verifies that:
   - the Chair is still assigned to the meeting;
   - the Meeting Session is InProgress;
   - the selected Agenda Item belongs to that Meeting Session;
   - the selected Agenda Item is Pending; and
   - no other Agenda Item of the same Meeting Session is InProgress.  
   (BR-64, BR-69, BR-70, BR-71)
4. The system changes the selected Agenda Item status to InProgress and records its actual start time. (BR-70, BR-72)
5. The system makes the new current Agenda Item available in real time to authorized users viewing the same Meeting Session. (BR-73)
6. The system displays MSG64 – "Current agenda item updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Another Agenda Item Is In Progress**
  1. At Step 3, another Agenda Item of the same Meeting Session is already InProgress. (BR-71)
  2. The selected Agenda Item remains Pending.
  3. The system displays MSG72 – "Another agenda item is already in progress. Complete it before starting another agenda item."
  4. The use case ends.

- **EX-2: Agenda Item Cannot Be Set as Current**
  1. At Step 3:
     - the selected Agenda Item does not belong to the Meeting Session; or
     - the Meeting Session is no longer InProgress; or
     - the selected Agenda Item is no longer Pending; or
     - the Chair is no longer assigned to the meeting.  
     (BR-64, BR-69, BR-70)
  2. The Agenda Item remains unchanged.
  3. The system displays MSG73 – "This agenda item cannot be set as current."
  4. The use case ends.

- **EX-3: Agenda Item Cannot Be Set as Current Due to System Error**
  1. At Step 4, the system cannot set the current Agenda Item.
  2. The Agenda Item remains unchanged.
  3. The system displays MSG75 – "Unable to set the current agenda item. Please try again."
  4. The use case ends.

---

### UC-36: Complete Agenda Item (Hoàn thành mục chương trình nghị sự)

- **Use Case ID**: UC-36
- **Use Case Name**: Complete Agenda Item
- **Primary Actor**: Chair
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Chair to mark the currently active Agenda Item as Done, recording its actual end time and duration used.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Chair is authenticated and assigned to the selected meeting.
  - PRE-2: Selected Meeting Session is InProgress.
  - PRE-3: Selected Agenda Item belongs to the Meeting Session and is InProgress.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected Agenda Item is Done.
  - POST-2: The actual end time and actual time used for the Agenda Item are recorded.
  - POST-3: The Meeting Session has no current Agenda Item until the Chair starts another Agenda Item.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [UC36_CompleteAgendaItem.png](../06-sequencediagram/agenda-management/UC36_CompleteAgendaItem.png)

#### Luồng sự kiện chính (Main Flow — Complete Agenda Item):
1. The Chair views the current in-progress Agenda Item in the active Meeting Session.
2. The Chair selects Complete Agenda Item.
3. The system verifies that:
   - the Chair is still assigned to the meeting;
   - the Meeting Session is InProgress; and
   - the selected Agenda Item belongs to that Meeting Session and is currently InProgress.  
   (BR-64, BR-69, BR-70)
4. The system changes the Agenda Item status to Done.
5. The system records the actual end time and the actual time used for the Agenda Item. (BR-72)
6. The system makes the completed Agenda Item state available in real time to authorized users viewing the same Meeting Session. (BR-73)
7. The system displays MSG65 – "Agenda item completed successfully."

#### Luồng sự kiện thay thế (Alternative Flows):
- N/A

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Agenda Item Cannot Be Completed**
  1. At Step 3:
     - the selected Agenda Item does not belong to the Meeting Session; or
     - the Meeting Session is no longer InProgress; or
     - the selected Agenda Item is no longer InProgress; or
     - the Chair is no longer assigned to the meeting.  
     (BR-64, BR-69, BR-70)
  2. The Agenda Item remains unchanged.
  3. The system displays MSG74 – "This agenda item cannot be completed."
  4. The use case ends.

- **EX-2: Agenda Item Cannot Be Completed Due to System Error**
  1. At Step 4 or Step 5, the system cannot complete the Agenda Item.
  2. The Agenda Item remains unchanged.
  3. The system displays MSG76 – "Unable to complete the agenda item. Please try again."
  4. The use case ends.

---

### FEATURE 8: THÀNH PHẦN THAM DỰ & VAI TRÒ TRONG CUỘC HỌP (MEETING PARTICIPANTS & MEETING ROLES)

> **Bản nháp để review.** Nội dung được tách từ `SCRUM-20`. Các mã `BR-78`–`BR-89` và
> `MSG95`–`MSG109` dưới đây là mã dự thảo để giữ đặc tả dễ đối chiếu; chưa cập nhật Business Rule
> catalogue, Message Code catalogue hoặc Sequence Diagram cho đến khi đặc tả được duyệt.

---

### UC-37: View Meeting Participants (Xem thành phần tham dự)

- **Use Case ID**: UC-37
- **Use Case Name**: View Meeting Participants
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary assigned to a meeting to view the internal users invited to that meeting and each participant's meeting-specific role.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.
  - PRE-2: The selected meeting exists.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The current participant list and meeting roles are displayed.
  - POST-2: No participant information is modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — View Meeting Participants):
1. The Secretary opens the selected meeting.
2. The Secretary selects the Meeting Participants tab.
3. The system verifies that the Secretary is still assigned to the meeting.
4. The system retrieves the current internal participants of the selected meeting; participant records retained only as historical evidence are excluded. (BR-79, BR-84, BR-88)
5. The system displays each participant's Full Name, Email, and Meeting Role.
6. The Secretary reviews the participant list.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: No Participants Have Been Added**
  1. At Step 4, the meeting has no participants.
  2. The system displays an empty participant list.
  3. The use case ends successfully.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Meeting Is Not Accessible**
  1. At Step 3, the meeting does not exist or the current user is not its assigned Secretary.
  2. The system displays the same not-found result used for an unknown meeting and does not disclose participant data. (BR-88)
  3. The use case ends.

- **EX-2: Participant List Cannot Be Retrieved**
  1. At Step 4, the system cannot retrieve the participant list.
  2. The system displays MSG95 – "Unable to load meeting participants. Please try again."
  3. The use case ends.

---

### UC-38: Add Meeting Participant (Thêm thành phần tham dự)

- **Use Case ID**: UC-38
- **Use Case Name**: Add Meeting Participant
- **Primary Actor**: Meeting Organizer (initial Secretary assignment), Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Meeting Organizer to add and assign the first Secretary when the meeting does not yet have one. After the first Secretary has been assigned, the Secretary manages the participant list and invites active internal users with meeting-specific roles. Participants are selected individually; the system does not support inviting an organisational unit.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The actor is authenticated and is either the Meeting Organizer creating the initial Secretary assignment or a Secretary already assigned to the selected meeting.
  - PRE-2: The selected meeting exists and is in Draft, WaitingApproval, Rejected, Approved, Published, or InProgress status.
  - PRE-3: The selected user has an active internal account and is not already a participant of the meeting.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected user is added to the meeting exactly once.
  - POST-2: The selected user's invitation response is Pending.
  - POST-3: The selected meeting role is recorded independently of the user's system role.
  - POST-4: If the meeting is Published or InProgress, an in-app notification and an invitation email are queued for the new participant.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — Add Meeting Participant):
1. The authorized actor opens the Meeting Participants tab of the selected meeting.
2. The actor selects Add Participant.
3. The system displays active internal users who are not already participants of the meeting. (BR-79)
4. The actor selects one user and assigns one Meeting Role:
   - Chair
   - Secretary
   - Member
   - Guest
5. The actor submits the participant information.
6. The system verifies that:
   - when the meeting has no Secretary, the actor is its Meeting Organizer and the selected Meeting Role is Secretary;
   - when the meeting already has a Secretary, the actor is an assigned Secretary;
   - the meeting still permits participant management;
   - the selected user is active and not already invited; and
   - the selected Meeting Role is valid.
   (BR-78, BR-79, BR-80)
7. The system adds the selected user with Pending response status and records the selected Meeting Role. (BR-80, BR-81)
8. If the meeting is Published or InProgress, the system queues an in-app notification and an invitation email for asynchronous delivery to the new participant. (BR-39, BR-40, BR-45, BR-82)
9. The system displays MSG96 – "Meeting participant added successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Invitation Notification Delivery Is Unavailable**
  1. At Step 8, the meeting is Published or InProgress but in-app or email invitation delivery is unavailable.
  2. The participant remains successfully added with Pending response status. (BR-40, BR-82)
  3. The system records the delivery failure for retry.
  4. The flow resumes at Step 9 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Participant Information**
  1. At Step 6, the selected user is inactive, is already a participant, or the submitted Meeting Role is invalid. (BR-79, BR-80)
  2. The participant is not added.
  3. The system displays MSG97 – "Invalid meeting participant information." with the applicable field message(s):
     - "The selected user is not available."
     - "This user is already a meeting participant."
     - "A valid meeting role is required."
     - "The Meeting Organizer must assign a Secretary as the first meeting participant."
  4. The actor corrects the information.
  5. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Participant List Can No Longer Be Modified**
  1. At Step 6, the meeting is Ended, MinutesIssued, or Archived. (BR-78)
  2. The participant is not added.
  3. The system displays MSG98 – "The meeting participant list can no longer be modified."
  4. The use case ends.

- **EX-3: Participant Cannot Be Added**
  1. At Step 7, the system cannot save the new participant.
  2. The participant is not added and no invitation is sent.
  3. The system displays MSG99 – "Unable to add the meeting participant. Please try again."
  4. The use case ends.

---

### UC-39: Assign / Change Meeting Role (Gán / Thay đổi vai trò trong cuộc họp)

- **Use Case ID**: UC-39
- **Use Case Name**: Assign / Change Meeting Role
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to assign or change an existing participant's role within one meeting without changing that user's system role.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.
  - PRE-2: The selected meeting is in Draft, WaitingApproval, Rejected, Approved, Published, or InProgress status.
  - PRE-3: The selected participant exists in the selected meeting.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The participant's Meeting Role is updated for the selected meeting only.
  - POST-2: The participant's system role and invitation response remain unchanged.
  - POST-3: If the meeting is Published or InProgress, an in-app notification and an email describing the new Meeting Role are queued for the affected participant.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — Assign / Change Meeting Role):
1. The Secretary opens the Meeting Participants tab of the selected meeting.
2. The Secretary selects Change Role for a participant.
3. The system displays the participant's current Meeting Role.
4. The Secretary selects Chair, Secretary, Member, or Guest.
5. The Secretary submits the selected Meeting Role.
6. The system verifies that the Secretary is still assigned to the meeting, the meeting still permits participant management, the participant still belongs to the meeting, the selected Meeting Role is valid, and the change would not remove a required Chair or Secretary. (BR-78, BR-80, BR-89)
7. The system updates only the participant's Meeting Role for the selected meeting. (BR-80, BR-83)
8. If the meeting is Published or InProgress, the system queues an in-app notification and an email describing the new Meeting Role for asynchronous delivery to the participant. (BR-82)
9. The system displays MSG100 – "Meeting role updated successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Selected Role Is Unchanged**
  1. At Step 5, the selected Meeting Role is the participant's current role.
  2. The system leaves the participant unchanged.
  3. The system displays MSG100 – "Meeting role updated successfully."
  4. The use case ends successfully.

- **AF-2: Role Change Notification Delivery Is Unavailable**
  1. At Step 8, the Meeting Role has been updated but notification delivery is unavailable.
  2. The Meeting Role change remains successful. (BR-82)
  3. The system records the delivery failure for retry.
  4. The flow resumes at Step 9 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Meeting Role**
  1. At Step 6, the submitted Meeting Role is not Chair, Secretary, Member, or Guest. (BR-80)
  2. The participant's Meeting Role remains unchanged.
  3. The system displays MSG101 – "A valid meeting role is required."
  4. The Secretary selects a valid Meeting Role.
  5. The flow resumes at Step 6 of the Main Flow.

- **EX-2: Required Meeting Role Would Be Removed**
  1. At Step 6, the change would remove the last assigned Secretary, or would remove the last assigned Chair while the meeting is Published or InProgress. (BR-89)
  2. The participant's Meeting Role remains unchanged.
  3. The system displays MSG109 – "The meeting must retain its required Chair and Secretary. Assign a replacement first."
  4. The use case ends.

- **EX-3: Participant List Can No Longer Be Modified**
  1. At Step 6, the meeting is Ended, MinutesIssued, or Archived. (BR-78)
  2. The participant's Meeting Role remains unchanged.
  3. The system displays MSG98 – "The meeting participant list can no longer be modified."
  4. The use case ends.

- **EX-4: Meeting Role Cannot Be Updated**
  1. At Step 7, the system cannot save the Meeting Role change.
  2. The participant's Meeting Role remains unchanged.
  3. The system displays MSG102 – "Unable to update the meeting role. Please try again."
  4. The use case ends.

---

### UC-40: Remove Meeting Participant (Xóa thành phần tham dự)

- **Use Case ID**: UC-40
- **Use Case Name**: Remove Meeting Participant
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary to remove an internal participant while the meeting has not yet ended.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.
  - PRE-2: The selected meeting is in Draft, WaitingApproval, Rejected, Approved, Published, or InProgress status.
  - PRE-3: The selected participant exists in the selected meeting.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The selected user is no longer an active participant of the meeting.
  - POST-2: Access granted by the removed participant assignment and any delegation made by that participant is revoked. Access granted to the removed user by a separate incoming delegation remains effective.
  - POST-3: If the meeting is Published or InProgress, an in-app notification and a removal email are queued for the removed user.
  - POST-4: If the removed user or a delegate affected by the ended delegation has no remaining participant or delegated access and is currently in the online meeting room, that user is removed from the room.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — Remove Meeting Participant):
1. The Secretary opens the Meeting Participants tab of the selected meeting.
2. The Secretary selects Remove for the target participant.
3. The system requests confirmation.
4. The Secretary confirms the removal.
5. The system verifies that the Secretary is still assigned to the meeting, the meeting still permits participant management, the participant still belongs to the meeting, and the removal would not remove a required Chair or Secretary. (BR-78, BR-84, BR-89)
6. The system removes the active participant assignment, ends any delegation created by that participant, and identifies every user whose access has changed. If the meeting is Published or InProgress, the system retains the response and attendance evidence under an explicitly inactive or removed assignment rather than physically deleting it; that retained record no longer counts as a current participant or required-role holder. (BR-84, BR-89)
7. If the meeting is InProgress, the system re-evaluates room-entry access for the removed user and every delegate affected by the ended delegation. It removes each currently connected affected user who no longer has access through another current participant assignment or effective delegation. (BR-84)
8. If the meeting is Published or InProgress, the system queues an in-app notification without a protected meeting link and a removal email for asynchronous delivery to the removed user. (BR-82)
9. The system displays MSG103 – "Meeting participant removed successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Secretary Cancels the Removal**
  1. At Step 3, the Secretary does not confirm the removal.
  2. The participant remains unchanged.
  3. The use case ends successfully.

- **AF-2: Removal Notification Delivery Is Unavailable**
  1. At Step 8, the participant has been removed but notification delivery is unavailable.
  2. The removal and applicable access revocation remain successful. (BR-82, BR-84)
  3. The system records the delivery failure for retry.
  4. The flow resumes at Step 9 of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Required Meeting Role Would Be Removed**
  1. At Step 5, the removal would remove the last assigned Secretary, or would remove the last assigned Chair while the meeting is Published or InProgress. (BR-89)
  2. The participant is not removed.
  3. The system displays MSG109 – "The meeting must retain its required Chair and Secretary. Assign a replacement first."
  4. The use case ends.

- **EX-2: Participant List Can No Longer Be Modified**
  1. At Step 5, the meeting is Ended, MinutesIssued, or Archived. (BR-78)
  2. The participant is not removed.
  3. The system displays MSG98 – "The meeting participant list can no longer be modified."
  4. The use case ends.

- **EX-3: Participant Cannot Be Removed**
  1. At Step 6, the system cannot remove the participant.
  2. The participant remains assigned to the meeting and retains existing access.
  3. The system displays MSG104 – "Unable to remove the meeting participant. Please try again."
  4. The use case ends.

---

### UC-41: Respond to Meeting Invitation (Phản hồi lời mời họp)

- **Use Case ID**: UC-41
- **Use Case Name**: Respond to Meeting Invitation
- **Primary Actor**: Meeting Participant
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows an invited participant to accept, decline, or delegate their own invitation to another active internal user until the meeting ends.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Meeting Participant is authenticated and has an active account.
  - PRE-2: The Meeting Participant is currently invited to the selected meeting.
  - PRE-3: The selected meeting is Published or InProgress.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The participant's latest Invitation Response is Accepted, Declined, or Delegated.
  - POST-2: The response time is recorded.
  - POST-3: For a Delegated response, the selected delegate is recorded on the original participant's response and receives delegated access without automatically receiving a Meeting Role.
  - POST-4: When a previous delegation is replaced or withdrawn, access granted solely by that delegation is revoked. Any affected user currently connected to the online meeting room is removed only when no room-entry access remains through another current participant assignment or effective delegation.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — Respond to Meeting Invitation):
1. The Meeting Participant opens the invitation or the published meeting details.
2. The system displays the participant's current Invitation Response.
3. The Meeting Participant selects Accepted or Declined.
4. The Meeting Participant submits the response.
5. The system verifies that the submitted response is Accepted or Declined, the current user owns the invitation and remains a current participant of the meeting, and the meeting is still Published or InProgress. (BR-85, BR-88)
6. In one atomic operation, the system records the selected Invitation Response and response time, clears any previous delegate, and revokes access granted solely by the previous delegation. (BR-85, BR-86, BR-87)
7. If the meeting is InProgress, the system re-evaluates room-entry access for the previous delegate and removes that user from the room only when the user is connected and has no room-entry access through another current participant assignment or effective delegation. (BR-86)
8. The system displays MSG105 – "Meeting invitation response recorded successfully."

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: Delegate the Invitation**
  1. At Step 3, the Meeting Participant selects Delegated.
  2. The system displays active internal users who may be selected as delegate.
  3. The Meeting Participant selects another active internal user.
  4. The system verifies that the current user owns the invitation and remains a current participant, the meeting is still Published or InProgress, and the selected delegate is active, is not the responding participant, and is not already representing another participant in the same meeting. (BR-85, BR-86, BR-88)
  5. In one atomic operation, the system records Delegated and `DelegatedToUserId` on the original participant's record, revokes access granted solely to any previous delegate, and grants the selected delegate the applicable access. The system does not automatically create another Meeting Participant row or change an existing participant's Meeting Role. (BR-85, BR-86, BR-87)
  6. After the atomic update, the original participant's own assignment retains meeting read access but no longer grants room-entry access while the Delegated response remains effective. A separate incoming delegation to that participant remains effective. The selected delegate has meeting read access and room-entry access; Chair and Secretary privileges are not transferred. (BR-86, BR-88)
  7. If the meeting is InProgress, the system re-evaluates room-entry access for the original participant and any previous delegate. It removes each currently connected affected user who has no room-entry access through another current participant assignment or effective delegation. (BR-86)
  8. If the selected delegate is already a participant, the system preserves that participant's existing Meeting Role and access.
  9. The flow resumes at Step 8 of the Main Flow.

- **AF-2: Change a Previous Response**
  1. At Step 2, the Meeting Participant has already responded to the invitation.
  2. The Meeting Participant selects another response and submits it.
  3. If the new response is Delegated, the flow continues at Step 2 of AF-1 so that the new delegate is revalidated.
  4. Otherwise, the flow resumes at Step 5 of the Main Flow so that ownership, current participation, meeting status, and the new response are revalidated before the atomic update. (BR-85, BR-86, BR-87)

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Invalid Invitation Response**
  1. At Step 5, the submitted response is not Accepted or Declined; or at AF-1 Step 4, Delegated has no valid delegate, the participant selects themself, or the selected user already represents another participant in the same meeting. (BR-85, BR-86)
  2. The previous Invitation Response remains unchanged.
  3. The system displays MSG106 – "Invalid meeting invitation response." with the applicable validation message.
  4. The Meeting Participant corrects the response.
  5. For an invalid Accepted or Declined response, the flow resumes at Step 3 of the Main Flow. For an invalid delegate, the flow resumes at Step 2 of AF-1.

- **EX-2: Invitation Can No Longer Be Answered**
  1. At Step 5 or AF-1 Step 4, the meeting is no longer Published or InProgress, or the current user no longer owns a current participant invitation for the meeting. (BR-85)
  2. The previous Invitation Response remains unchanged.
  3. The system displays MSG107 – "This meeting invitation can no longer be answered."
  4. The use case ends.

- **EX-3: Invitation Response Cannot Be Saved**
  1. At Step 6 of the Main Flow or Step 5 of AF-1, the system cannot save the Invitation Response or apply the corresponding delegated access atomically.
  2. The previous Invitation Response and access remain unchanged.
  3. The system displays MSG108 – "Unable to save the meeting invitation response. Please try again."
  4. The use case ends.

---

### UC-42: View Participant Response Status (Xem trạng thái phản hồi của thành phần tham dự)

- **Use Case ID**: UC-42
- **Use Case Name**: View Participant Response Status
- **Primary Actor**: Secretary
- **Secondary Actor**: None
- **Mô tả (Description)**: Allows the Secretary assigned to a meeting to review every current participant's invitation response and a summary by response status.
- **Điều kiện tiên quyết (Preconditions)**:
  - PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.
  - PRE-2: The selected meeting exists.
- **Điều kiện sau (Postconditions)**:
  - POST-1: The latest response status of every current participant is displayed.
  - POST-2: The number of Pending, Accepted, Declined, and Delegated responses is displayed.
  - POST-3: Invitation Response data is not modified.
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: To be created after this specification is approved.

#### Luồng sự kiện chính (Main Flow — View Participant Response Status):
1. The Secretary opens the Meeting Participants tab of the selected meeting.
2. The Secretary selects Participant Response Status.
3. The system verifies that the Secretary is still assigned to the meeting.
4. The system retrieves all current participants and their latest Invitation Response; participant records retained only as historical evidence are excluded. (BR-84, BR-87, BR-88)
5. The system displays each participant's Full Name, Meeting Role, Invitation Response, response time, and delegate when applicable.
6. The system displays the total number of Pending, Accepted, Declined, and Delegated responses. (BR-87)
7. The Secretary reviews the response status summary.

#### Luồng sự kiện thay thế (Alternative Flows):

- **AF-1: No Participant Has Responded**
  1. At Step 4, every current participant has Pending response status.
  2. The system displays all participants as Pending and shows zero for Accepted, Declined, and Delegated.
  3. The use case ends successfully.

#### Luồng ngoại lệ / Lỗi (Exception Flows):

- **EX-1: Meeting Is Not Accessible**
  1. At Step 3, the meeting does not exist or the current user is not its assigned Secretary.
  2. The system displays the same not-found result used for an unknown meeting and does not disclose participant response data. (BR-88)
  3. The use case ends.

- **EX-2: Participant Response Status Cannot Be Retrieved**
  1. At Step 4, the system cannot retrieve the participant response data.
  2. The system displays MSG95 – "Unable to load meeting participants. Please try again."
  3. The use case ends.

---

#### Quy tắc nghiệp vụ dự thảo của F08 (Draft Business Rules)

| BR-ID | Draft Rule Statement | Applies To |
|---|---|---|
| `BR-78` | When a meeting has no Secretary, only its Meeting Organizer may add the first participant, and that participant must be assigned the Secretary Meeting Role. After a Secretary has been assigned, only an assigned Secretary may add participants, change Meeting Roles, or remove participants. Participant management is allowed while the meeting is Draft, WaitingApproval, Rejected, Approved, Published, or InProgress, and becomes read-only when the meeting is Ended, MinutesIssued, or Archived. | UC-38, UC-39, UC-40 |
| `BR-79` | Internal participants are selected individually from active user accounts. A user may appear at most once in the same meeting. The system has no organisational-unit invitation, and external link guests are not Meeting Participants. | UC-37, UC-38 |
| `BR-80` | Each internal participant has exactly one meeting-specific role: Chair, Secretary, Member, or Guest. A Meeting Role is independent of the user's system Role. | UC-38, UC-39 |
| `BR-81` | A newly added participant starts with Pending Invitation Response, no delegate, and no response time. | UC-38 |
| `BR-82` | While a meeting is Published or InProgress, adding a participant notifies the added user, changing a Meeting Role notifies that participant, and removing a participant notifies the removed user. Each notification uses both in-app and email delivery. Delivery is asynchronous; delivery failure is recorded for retry and does not roll back the participant change. A removal notification must not contain a protected meeting link. | UC-38, UC-39, UC-40 |
| `BR-83` | Changing a Meeting Role changes only the participant's role in the selected meeting and does not change the user's system Role or Invitation Response. | UC-39 |
| `BR-84` | Removing a participant revokes access granted by that participant assignment and ends any delegation made by that participant. If the removed user is also another participant's delegate, that separate incoming delegation remains effective. Room-entry access must be re-evaluated for both the removed user and every delegate affected by an ended outgoing delegation; a connected user is removed from the room only when no room-entry access remains through another current participant assignment or effective delegation. For a Published or InProgress meeting, response and attendance evidence must be preserved under an explicitly inactive or removed assignment instead of being physically deleted. A retained historical record is excluded from current-participant views, access checks, and required-role counts. Participant assignments cannot be removed after the meeting reaches Ended. | UC-37, UC-40, UC-42 |
| `BR-85` | Only the invited participant may answer their own invitation. A valid response is Accepted, Declined, or Delegated and may be changed while the meeting is Published or InProgress. | UC-41 |
| `BR-86` | A Delegated response records one other active internal user in `DelegatedToUserId` on the original participant row. It does not automatically create another Meeting Participant row or transfer a Chair or Secretary role. While the delegation is effective, the original participant's own assignment retains meeting read access but does not grant room-entry access; room-entry access from a separate incoming delegation remains effective. The selected delegate receives meeting read and room-entry access, and an existing participant keeps their current Meeting Role. A user cannot delegate to themself or represent more than one participant in the same meeting. Replacing or withdrawing a delegation revokes access granted solely by the previous delegation. After any such access change, the original participant and previous delegate must be re-evaluated and a connected user removed from the room only when no other current participant or effective delegation grants room-entry access. The response and access change must be atomic and the one-delegation-per-user rule must be concurrency-safe. | UC-41 |
| `BR-87` | The latest Invitation Response and response time are recorded for each participant. Invitation Response is independent of Attendance; accepting an invitation does not mean the participant actually attended. | UC-41, UC-42 |
| `BR-88` | Participant and response data must be authorized and filtered on the server. Current participants and users holding an effective delegation receive only the meeting access allowed by their relationship; delegated access does not grant Chair or Secretary operations. A caller who may not access a specific meeting receives the same not-found result as an unknown meeting, and no unauthorized participant row is returned to the browser. | UC-37, UC-41, UC-42 |
| `BR-89` | After the initial Secretary assignment, a meeting must always retain at least one current participant assigned as Secretary. The transition to Published requires at least one current Chair and one current Secretary whose accounts are active. A Meeting Role change or participant removal cannot remove the last current Secretary in any editable status, or the last current Chair while the meeting is Published or InProgress; assign a replacement first. Multiple Chairs and Secretaries are allowed, but each participant has only one Meeting Role. Before a Meeting Session starts, the system must revalidate that at least one current Chair and one current Secretary still have active accounts. This start check validates assignment and account status, not Invitation Response. | UC-38, UC-39, UC-40; publication/start checks to be synchronized with UC-21 and UC-29 |

#### Quyết định đã chốt khi review F08

1. **Khởi tạo Secretary đầu tiên**: Meeting Organizer của UC-15, được xác định bằng `Meeting.CreatedById`, chỉ được dùng UC-38 để thêm participant đầu tiên với Meeting Role `Secretary`.
2. **Chuyển quyền quản lý**: sau khi đã có Secretary, việc thêm participant, đổi Meeting Role và xóa participant do Secretary được gán thực hiện; Meeting Organizer không mặc nhiên là Secretary.
3. **Trạng thái được sửa danh sách**: được thêm participant, đổi Meeting Role và xóa participant từ `Draft` đến hết `InProgress`; participant được phản hồi hoặc đổi phản hồi từ `Published` đến hết `InProgress`. Khi meeting chuyển sang `Ended`, `MinutesIssued` hoặc `Archived`, danh sách trở thành hồ sơ chỉ đọc; nếu cần sửa sai phải dùng một luồng đính chính có lưu lịch sử thay vì thay đổi trực tiếp.
4. **Ủy quyền tham dự**: giữ `Delegated` theo SCRUM-20. Người được ủy quyền không tự động có Meeting Role; họ được cấp quyền xem meeting và vào phòng trong phạm vi ủy quyền. Nếu đã là participant thì giữ nguyên role. Chair/Secretary không được chuyển bằng thao tác ủy quyền, và một người chỉ đại diện cho tối đa một participant trong cùng meeting. Quyền vào phòng này thay đổi quyết định hiện hành trong ADR-0010 (đang loại `Delegated`) và BR-29 (chưa cấp quyền cho `DelegatedToUserId`), nên trước khi triển khai phải ghi change note trong SCRUM-20 và viết ADR mới thay thế phần quyền truy cập liên quan; không sửa ADR đã chấp nhận.
5. **Thông báo sau khi Published**: thêm participant, đổi Meeting Role hoặc xóa participant đều queue in-app notification và email cho người bị ảnh hưởng để gửi bất đồng bộ. Lỗi gửi không hoàn tác thay đổi và phải được retry. Thông báo xóa không chứa link mở nội dung đã bị thu hồi. Khi xóa làm kết thúc một ủy quyền, quyền vào phòng của cả participant bị xóa và delegate liên quan đều được tính lại; chỉ đưa một người khỏi phòng nếu họ không còn quyền nào khác từ participant hoặc ủy quyền.
6. **Vai trò bắt buộc**: sau bước khởi tạo, meeting luôn giữ ít nhất một participant hiện hành được gán Secretary. Tại thời điểm chuyển sang `Published`, meeting phải có ít nhất một Chair và một Secretary hiện hành với tài khoản đang hoạt động. Không được xóa hoặc đổi role của Secretary hiện hành cuối cùng ở bất kỳ trạng thái còn sửa được nào; từ `Published` đến hết `InProgress`, cũng không được xóa hoặc đổi role của Chair hiện hành cuối cùng. Trước `Published`, meeting có thể tạm thời chưa có Chair nhưng không thể được publish cho tới khi gán Chair hợp lệ. Participant mang role bắt buộc vẫn có thể chọn `Declined` hoặc `Delegated`; phản hồi không tự động xóa hay chuyển Meeting Role. Trước khi bắt đầu Meeting Session, hệ thống kiểm tra lại rằng vẫn có ít nhất một Chair và một Secretary hiện hành với tài khoản đang hoạt động; theo quyết định này, bước kiểm tra không dựa trên Invitation Response. Có thể có nhiều Chair/Secretary, nhưng mỗi participant chỉ mang một Meeting Role.

