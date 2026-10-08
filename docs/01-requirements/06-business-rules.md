# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

Danh mục này tập trung toàn bộ các quy tắc `BR-01` đến `BR-157` được đồng bộ từ Report 3 (Software Requirement Specification). Đây là nguồn nội dung chuẩn để UC Specification và Sequence Diagram dẫn chiếu; mã `BR` độc lập với mã System Message `MSG`.

## Quy ước đồng bộ

- UC Specification dẫn chiếu BR cạnh điều kiện, bước luồng hoặc ngoại lệ chịu tác động; không tự định nghĩa một bản sao khác của quy tắc.
- Sequence Diagram dùng đúng BR ID tại message nơi hệ thống kiểm tra hoặc áp dụng quy tắc, theo [Sequence Diagram Convention](../06-sequencediagram/README.md).
- System Message giữ mã `MSG` riêng. Khi BR dẫn đến một thông báo, UC/sequence liên kết tình huống với MSG trong [danh mục thông báo](../03-api/00-danh-muc-ma-thong-bao.md); không đổi BR ID thành MSG ID.
- Khi Report 3 chưa cung cấp `Rationale` hoặc `Enforced By`, ghi rõ theo nguồn Report 3.
- Cập nhật câu chữ hoặc phạm vi BR tại file này trước, sau đó rà lại các UC, sequence và message có liên quan để thống nhất.

---

## 1. BR-01–BR-23 — Xác thực, người dùng và vai trò

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-01` | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. | UC-01, UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-02` | Authentication failure messages must not reveal whether a specific username or email exists in the system. | UC-01 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-03` | A deactivated user account must not be allowed to sign in or access authenticated system functions. | UC-01, UC-02, UC-07, UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-04` | A user without active system access or the required active role/permission must not be allowed to access the system. | UC-01, UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-05` | Identity information returned by Google must be successfully verified before it can be used for authentication. | UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-06` | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. | UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-07` | Signing in with Google must not automatically create a new internal user account. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-08` | User permissions are determined by the system and must not be selected manually during the sign-in process. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-09` | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. | UC-03 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-10` | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. | UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-11` | List views must support pagination when the number of records exceeds the configured page size. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-12` | When a search or filter returns no matching records, the system must display an appropriate no-results message instead of treating the result as a system error. | UC-04 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-13` | Username and email must each be unique across user accounts. | UC-05 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-14` | All mandatory user-account information must be provided and valid before an account can be created or updated. | UC-05, UC-06 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-15` | A newly created user account must have Active status by default. | UC-05 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-16` | Successful administrative changes to user accounts or role-permission configuration must be recorded in the audit log. | UC-05, UC-06, UC-07, UC-08, UC-11, UC-12, UC-13, UC-14 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-17` | General user-account updates may modify Full Name, Job Title, and Contact Information. Email, System Role, and password credentials must be managed separately. | UC-06 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-18` | When a user account is deactivated, all active authenticated sessions for that account must be terminated. | UC-07 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-19` | Deactivating a user account must not delete historical records associated with that user. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-20` | A password-reset link must have a limited validity period and must not be reusable after a successful password reset. | UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-21` | An administrator must not be able to view a user's existing password. | UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-22` | Password reset may only be initiated for an account that is eligible to use password-based authentication. | UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-23` | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. | UC-11 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

**Quy tắc TrustCabinet đã được BA xác nhận:** tài khoản nội bộ đang hoạt động được phép đăng nhập dù chưa được gán role hoặc chưa có capability. Capability chỉ quyết định chức năng nào được phép dùng sau khi đăng nhập. Quy tắc này thay thế cách áp dụng BR-04 nguyên văn Report 3 ở trên; không yêu cầu thêm quyền riêng để đăng nhập.

**Quy tắc TrustCabinet đã được BA xác nhận — UC-08 (đặt lại mật khẩu):** BA chốt trong SCRUM-13 (BA Final Review). Các quy tắc dưới đây cụ thể hoá BR-20 và BR-22 cho TrustCabinet:
- **BR-20 — liên kết đặt lại mật khẩu:**
  - hiệu lực **60 phút** kể từ khi liên kết được tạo (ngay trước khi gửi email);
  - **dùng một lần**: liên kết bị tiêu ngay khi được gửi kèm một mật khẩu mới hợp lệ;
  - mỗi tài khoản chỉ có **một liên kết dùng được** — liên kết của email gửi thành công gần nhất; gửi liên kết mới thì liên kết cũ hết hiệu lực;
  - gửi email thất bại thì liên kết mới **không** dùng được, còn liên kết cũ (nếu có) **vẫn** dùng được;
  - liên kết đang chờ bị thu hồi khi tài khoản bị **vô hiệu hoá** (UC-07), **đổi email** (UC-06) hoặc **đổi vai trò hệ thống** (UC-09);
  - yêu cầu đặt lại mật khẩu **không** đổi gì trên tài khoản cho tới khi liên kết được dùng;
  - đặt mật khẩu mới thành công thì **mọi phiên đăng nhập** của tài khoản bị kết thúc, và hệ thống **không** tự đăng nhập người dùng.
- **BR-22 — tài khoản đủ điều kiện:** tài khoản đang **hoạt động** và **đăng nhập bằng mật khẩu**. Tài khoản chỉ đăng nhập bằng Google (chưa có mật khẩu) **không** đủ điều kiện.
- **Mật khẩu mới** theo cùng quy tắc với khi tạo tài khoản (UC-05): tối thiểu 12 ký tự.
- **Audit (BR-16):** ghi hai sự kiện — yêu cầu đặt lại (người thực hiện là quản trị viên) và đặt lại thành công (người thực hiện là chính chủ tài khoản).
- Liên kết không hợp lệ đều nhận **cùng một thông báo** `MSG93`.

---

## 2. BR-24–BR-44 — Cuộc họp và vòng đời phê duyệt

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-24` | All required meeting information must be provided before a meeting can be created. | UC-15 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-25` | Planned Start Time and Planned End Time are not stored as meeting-level schedule fields. A meeting's planned schedule is defined by its Meeting Sessions. | UC-15, UC-16, UC-17, UC-18, UC-26, UC-27, UC-53 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-26` | A newly created meeting must have Draft status. | UC-15 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-27` | Meeting information may be edited only while the meeting is in Draft or Rejected status. | UC-16 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-28` | Updating meeting information must not implicitly change the meeting lifecycle status. | UC-16 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-29` | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting. | UC-17, UC-25, UC-53, UC-54 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-30` | The meeting list may be searched by Meeting Name and filtered by Meeting Status, Confidentiality Level, and Time Range. A meeting matches a Time Range when at least one non-cancelled Meeting Session has a planned schedule that overlaps the selected range; meetings without a scheduled Meeting Session do not match a Time Range filter. | UC-18 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-31` | When multiple search or filter criteria are applied, a record must satisfy all applicable criteria to appear in the result. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-32` | The meeting list must support sorting, and the current search, filter, and sorting criteria must be preserved when navigating between pages. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-33` | A meeting may be submitted or resubmitted for approval only when all mandatory meeting-level information is complete and the meeting is in Draft or Rejected status. Meeting Sessions are not required for approval submission. | UC-19 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-34` | Submitting a Draft or Rejected meeting for approval changes its status to WaitingApproval. A meeting in WaitingApproval status cannot be edited until an approval decision is made. | UC-19 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-35` | For each approval cycle, a Meeting Approver must be assigned. Only the Meeting Approver currently assigned to that approval cycle and holding the meeting_approver permission may approve or reject the meeting. | UC-20 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-36` | Approving a meeting changes its status from WaitingApproval to Approved. Rejecting a meeting changes its status from WaitingApproval to Rejected. A rejection reason is mandatory. | UC-20 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-37` | Only an authorized Meeting Organizer may publish a meeting that is in Approved status and satisfies the publication readiness requirements. Publishing changes the meeting status from Approved to Published and initiates participant invitations. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-38` | Every meeting lifecycle transition must create a history record containing at least the acting user, timestamp, action, and resulting status. A rejection history record must also contain the rejection reason. Meeting lifecycle history records are immutable and must not be updated or deleted. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-39` | Invitation delivery must be executed asynchronously via a background task queue to prevent blocking the user publication flow. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-40` | Failure of an asynchronous invitation delivery task must trigger retry logic without reverting the Published state of the meeting. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-41` | Meeting publication scope is strictly limited to meetings in Approved status and cannot be bypassed by direct state manipulation. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-42` | All participant invitation notifications must include a direct link to the published meeting details and attached materials. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-43` | System logging must track the queuing and delivery status of all asynchronous invitation messages for auditability. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-44` | Re-publishing or updating invitation delivery for an already published meeting requires explicit organizer action and creates a distinct lifecycle record. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

**Áp dụng trong TrustCabinet — BA đã chốt (UC-17, UC-18 và mở đích notification):**
- Người tạo (`CreatedById`) được đọc ở mọi trạng thái.
- Người duyệt đang được gán được đọc từ `WaitingApproval` trở đi.
- Secretary hiện hành có tài khoản hoạt động được đọc từ `Draft` để chuẩn bị cuộc họp theo FE-07; ở `WaitingApproval` được xem nhưng không được thêm/sửa/xóa participant.
- Người được gán với vai trò `Chair`, `Member` hoặc `Guest` được đọc từ `Published` trở đi.
- Trạng thái lời mời (`Pending`, `Accepted`, `Declined`, `Delegated`) không làm mất quyền đọc meeting đã công bố.

---

## 3. BR-45–BR-53 — Thông báo

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-45` | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-46` | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-47` | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. | UC-23, UC-24, UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-48` | The notification bell shows how many of the current user's notifications are unread. | UC-22, UC-24, UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-49` | Opening the notification list does not mark any notification as read. | UC-22 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-50` | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-51` | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. | UC-24 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-52` | A meeting notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-53` | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 4. BR-54–BR-63 — Phiên họp và vòng đời cuộc họp

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-54` | Meeting Sessions may be created, updated, or cancelled only by the Secretary assigned to the parent meeting. The selected or newly created session must be Planned, and the parent meeting must be Draft, Rejected, Approved, Published, or InProgress. Session planning is not allowed while the meeting is WaitingApproval or after it is Ended. | UC-26, UC-27, UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-55` | A meeting does not need a Meeting Session in order to be submitted or resubmitted for approval. | UC-19 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-56` | Meeting Sessions are optional for publication. An Approved meeting may be published with zero Meeting Sessions when all other publication requirements are satisfied. | UC-21, UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-57` | Meeting Sessions are not permanently deleted. A Planned Meeting Session that will no longer take place is changed to Cancelled and retained for traceability. An InProgress, Completed, or Cancelled Meeting Session cannot be updated or cancelled. | UC-27, UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-58` | A Meeting Session follows the lifecycle Planned → InProgress → Completed, with Planned → Cancelled as a terminal cancellation path. A Cancelled Meeting Session cannot be started. | UC-28, UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-59` | Only one Meeting Session of the same meeting may be InProgress at a time. | UC-29 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-60` | A Planned Meeting Session may be started only while its parent Meeting is InProgress. Starting or ending a Meeting Session does not change the parent Meeting lifecycle status. | UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-61` | Completing or cancelling the last unfinished Meeting Session does not automatically end the parent Meeting. Ending the Meeting is an explicit Chair action defined by UC-44. | UC-28, UC-30, UC-44 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-62` | A Meeting Session's Planned End Time must be later than its Planned Start Time. | UC-26, UC-27 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-63` | Only the Chair assigned to a meeting may start or end a Meeting Session of that meeting. | UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 5. BR-64–BR-77 — Chương trình nghị sự

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-64` | Each Agenda Item belongs to exactly one Meeting Session. Reordering changes the order only within that Meeting Session and does not move an Agenda Item to another session. | UC-31, UC-34, UC-35, UC-36, UC-54 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-65` | Only the Secretary assigned to the parent meeting may create, update, delete, or reorder Agenda Items, and only while the selected Meeting Session is Planned. | UC-31, UC-32, UC-33, UC-34 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-66` | An Agenda Item must have a Title and a Planned Duration greater than zero. Presenter and Description may be provided. Any attached documents must be available to the parent meeting. | UC-31, UC-32 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-67` | A newly created Agenda Item starts in Pending status and is assigned an explicit order within its Meeting Session. The Secretary may change that order while the session remains Planned. | UC-31, UC-34 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-68` | After a Meeting Session leaves Planned status, its agenda structure cannot be created, updated, deleted, or reordered. Only agenda execution state may change during the active session. | UC-31, UC-32, UC-33, UC-34 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-69` | Only the Chair assigned to the parent meeting may set or complete the current Agenda Item while the Meeting Session is InProgress. | UC-35, UC-36 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-70` | For the agenda execution covered by these use cases, setting a Pending Agenda Item as current changes it to InProgress; completing the current Agenda Item changes it from InProgress to Done. | UC-35, UC-36 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-71` | Only one Agenda Item of the same Meeting Session may be InProgress at a time. The current Agenda Item must be completed before another Agenda Item can be set as current. | UC-35 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-72` | Setting an Agenda Item as current records its actual start time. Completing it records its actual end time and actual time used. Completing an Agenda Item does not automatically end the Meeting Session. | UC-35, UC-36 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-73` | Changes to the current or completed Agenda Item must be reflected in real time for authorized users viewing the same Meeting Session. | UC-35, UC-36 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-74` | Agenda planning for a Meeting Session remains available while that Meeting Session is Planned, including while the parent meeting is Published or InProgress. Agenda planning ends when the Meeting Session starts or is cancelled. | UC-31, UC-32, UC-33, UC-34 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-75` | When the parent meeting is Published or InProgress, a newly created or rescheduled Planned Meeting Session must have a Planned Start Time later than the current time. This prevents retroactive session scheduling after publication. | UC-26, UC-27 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-76` | Creating, rescheduling, or cancelling a Meeting Session after the parent meeting has been Published must notify affected participants of the schedule change. | UC-26, UC-27, UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-77` | A Published or InProgress meeting may have zero Planned Meeting Sessions. Cancelling the last Planned Meeting Session is allowed and does not change the parent Meeting lifecycle status. | UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 6. BR-78–BR-129 — Quản lý thành phần tham dự & vai trò cuộc họp (FE-07)

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-78` | When a meeting has no Secretary, only the Meeting Organizer may add the first internal participant, and that participant must be assigned the Secretary Meeting Role. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-79` | After the first Secretary has been assigned, only an assigned Secretary may manage the meeting roster. | UC-38, UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-80` | Participant management is allowed only while the Meeting is Draft, Rejected, Approved, Published, or InProgress. | UC-38, UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-81` | An internal participant must reference an active internal user account. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-82` | An internal user may appear at most once as a current participant in the same Meeting. | UC-37, UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-83` | Internal participants are added individually; organisational-unit invitations are not supported. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-84` | External Guests are represented by meeting-specific Guest invitations rather than internal Meeting Participant records. | UC-37, UC-38, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-85` | Each current internal participant has exactly one Meeting Role: Chair, Secretary, or Member. | UC-38, UC-39 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-86` | A participant's Meeting Role is independent of the user's system Role. | UC-38, UC-39 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-87` | An external Guest cannot satisfy a required Chair or Secretary role. | UC-21, UC-29, UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-88` | A newly added internal participant or external Guest invitation starts with Pending Invitation Response and no response time. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-89` | Delegation is available only to current internal participants. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-90` | When an internal participant is added to a Published or InProgress Meeting, the participant is notified through the applicable invitation channels. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-91` | When an external Guest is invited to a Published or InProgress Meeting, the invitation is delivered by email. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-92` | Changing an internal participant's Meeting Role while the Meeting is Published or InProgress notifies that participant. | UC-39 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-93` | Removing an internal participant or revoking a Guest invitation from a Published or InProgress Meeting notifies the affected invitee through the applicable channel. | UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-94` | Failure to deliver a participant or Guest notification does not undo an otherwise successful roster change. | UC-38, UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-95` | A participant-removal or Guest-revocation notification must not contain a protected meeting-access link. | UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-96` | Changing a Meeting Role does not change the participant's Invitation Response. | UC-39 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-97` | Removing a current internal participant revokes access granted by that participant assignment. | UC-40, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-98` | Removing a current internal participant ends any outgoing delegation created by that participant. | UC-40, UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-99` | Removing a participant's own assignment does not invalidate an independent incoming delegation granted to that user. | UC-40, UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-100` | Revoking an external Guest invitation revokes access granted by that Guest relationship. | UC-40, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-101` | After a roster, delegation, or Guest-access relationship changes, room-entry eligibility must be re-evaluated; a connected user is disconnected only when no valid room-entry relationship remains. | UC-40, UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-102` | Removing or revoking a roster entry after publication preserves its Invitation Response and Attendance evidence as historical data. | UC-37, UC-40, UC-42 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-103` | Historical or revoked roster records are excluded from the current roster, current access checks, and required-role counts. | UC-37, UC-40, UC-42 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-104` | Only the owner of a current invitation may respond to that invitation. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-105` | An internal participant may respond Accepted, Declined, or Delegated; an external Guest may respond only Accepted or Declined. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-106` | Invitation Responses may be changed while the Meeting is Published or InProgress. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-107` | A delegation target must be another active internal user. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-108` | A participant cannot delegate their invitation to themself. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-109` | Delegation does not create another Meeting Participant record or transfer the original participant's Meeting Role. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-110` | While delegation is effective, the original participant retains meeting-read access, but the original participant's own assignment does not grant room-entry access. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-111` | An effective delegate receives meeting-read and room-entry access granted by the delegation. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-112` | If the selected delegate is already a Meeting Participant, the delegate keeps their existing Meeting Role. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-113` | A user may represent at most one other participant through delegation in the same Meeting. | UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-114` | Replacing or withdrawing a delegation revokes access granted solely by the previous delegation. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-115` | The latest Invitation Response and its response time are recorded for each current invitation. | UC-41, UC-42 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-116` | Invitation Response and Attendance are independent; Accepted does not mean the invitee attended the Meeting. | UC-41, UC-42, UC-88 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-117` | Roster and invitation-response information is accessible only to users whose relationship with the Meeting permits that information. | UC-17, UC-37, UC-41, UC-42 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-118` | An active assigned Secretary may read the Meeting before publication; WaitingApproval is read-only for roster management. | UC-17, UC-37, UC-38, UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-119` | A Chair or Member assignment alone does not grant meeting-read access before the Meeting is Published. | UC-17, UC-18 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-120` | An external Guest receives access only to the Meeting to which that Guest was invited. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-121` | A user who is not authorized to access a Meeting must not receive its roster or invitation-response information. | UC-37, UC-42 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-122` | After the first Secretary has been assigned, a Meeting must retain at least one current internal participant assigned as Secretary. | UC-38, UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-123` | A Meeting may have at most one current Chair at a time. | UC-38, UC-39 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-124` | Publishing a Meeting requires exactly one current Chair whose internal account is active. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-125` | Publishing a Meeting requires at least one current Secretary whose internal account is active. | UC-21 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-126` | A Published or InProgress Meeting must not lose its current Chair unless a valid replacement is assigned. | UC-39, UC-40 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-127` | Starting a Meeting requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-128` | Starting a Meeting Session requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-29 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-129` | Invitation Response does not affect Chair or Secretary readiness checks. | UC-21, UC-29, UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 7. BR-130–BR-136 — Vòng đời cuộc họp (Bắt đầu / Kết thúc cuộc họp)

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-130` | Only an active current Chair assigned to the Meeting may explicitly start or end that Meeting. | UC-43, UC-44 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-131` | A Meeting may be started only while it is Published. | UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-132` | A Meeting may be ended only while it is InProgress. | UC-44 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-133` | A Meeting may start with zero Meeting Sessions. | UC-43 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-134` | A Meeting may end only when no Meeting Session remains Planned or InProgress; a Meeting with zero sessions satisfies this condition. | UC-44 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-135` | Changing the Meeting lifecycle does not change any Meeting Session status. | UC-43, UC-44 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-136` | Changing a Meeting Session lifecycle does not change the parent Meeting status. | UC-28, UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 8. BR-137–BR-147 — Quản lý phòng họp trực tuyến (FE-09)

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-137` | The Online Meeting Room belongs to the Meeting rather than to a Meeting Session. | UC-45, UC-46, UC-47, UC-48, UC-49, UC-50, UC-51, UC-52 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-138` | A Meeting Session is not required to use the Online Meeting Room. | UC-45, UC-46, UC-47, UC-48, UC-49, UC-50, UC-51, UC-52 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-139` | The Online Meeting Room is available only for Online or Hybrid Meetings whose lifecycle status is Published or InProgress. | UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-140` | Room entry requires a valid current internal participant relationship, effective delegation, or verified current external Guest relationship. | UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-141` | Joining, leaving, microphone/camera control, screen sharing, room moderation, and in-room chat do not change the Meeting or Meeting Session lifecycle. | UC-45, UC-46, UC-47, UC-48, UC-49, UC-51, UC-52 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-142` | The same Online Meeting Room may remain available across multiple Meeting Sessions of the same Meeting. | UC-45, UC-46, UC-47, UC-48, UC-49, UC-50, UC-51, UC-52 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-143` | When a Meeting becomes Ended, its Online Meeting Room stops accepting joins and disconnects currently connected users. | UC-44, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-144` | A room user may control only their own microphone and camera; the assigned Chair may mute or remove another connected user but may not enable another user's microphone or camera. | UC-47, UC-48, UC-51 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-145` | At most one screen share may be active in the same Meeting Room at a time; starting a new share stops the previous active share. | UC-49 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-146` | Meeting Room presence represents users currently connected to the room and is distinct from the Meeting Participant roster. | UC-50 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-147` | In-room chat is scoped to the selected Meeting Room and is available only to authorized users currently connected to that room. | UC-52 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

---

## 9. BR-148–BR-157 — Lời mời & Truy cập Guest bên ngoài

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-148` | An external Guest is identified by Guest Name and Email and does not require an internal user account. | UC-37, UC-38, UC-41 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-149` | Within one Meeting, a Guest email may have at most one current Guest invitation. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-150` | An external Guest has fixed Guest access and receives no system Role or privileged Meeting Role. | UC-38, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-151` | A Guest invitation must not be created when the same email is already represented by a current internal participant of that Meeting. | UC-38 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-152` | External Guest access requires a secure, meeting-specific invitation. | UC-21, UC-38, UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-153` | An external Guest must verify control of the invited email before Guest access is established. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-154` | External Guest access is scoped only to the invited Meeting. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-155` | External Guest access expires according to the configured validity period. | UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-156` | External Guest access does not require an internal account login. | UC-38, UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-157` | Revoking an external Guest invitation immediately invalidates access granted by that invitation. | UC-40, UC-41, UC-45 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
