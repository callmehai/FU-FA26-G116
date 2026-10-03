# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

Danh mục này tập trung các quy tắc `BR-01` đến `BR-103`: `BR-01`–`BR-77` từ Report 3 và `BR-78`–`BR-103` được đồng bộ từ Report 3 source-of-truth cho F08 và GOF09 ngày 03/10/2026. Đây là nguồn nội dung chuẩn để UC Specification và Sequence Diagram dẫn chiếu; mã `BR` độc lập với mã System Message `MSG`.

## Quy ước đồng bộ

- UC Specification dẫn chiếu BR cạnh điều kiện, bước luồng hoặc ngoại lệ chịu tác động; không tự định nghĩa một bản sao khác của quy tắc.
- Sequence Diagram dùng đúng BR ID tại message nơi hệ thống kiểm tra hoặc áp dụng quy tắc, theo [Sequence Diagram Convention](../06-sequencediagram/README.md).
- System Message giữ mã `MSG` riêng. Khi BR dẫn đến một thông báo, UC/sequence liên kết tình huống với MSG trong [danh mục thông báo](../03-api/00-danh-muc-ma-thong-bao.md); không đổi BR ID thành MSG ID.
- Khi Report 3 chưa cung cấp `Applies To`, `Rationale` hoặc `Enforced By`, ghi rõ chưa có trong nguồn; không suy diễn thêm.
- Cập nhật câu chữ hoặc phạm vi BR tại file này trước, sau đó rà lại các UC, sequence và message có liên quan để thống nhất.

## BR-01–BR-23 — Xác thực, người dùng và vai trò

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-01` | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. | UC-01 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-02` | Authentication failure messages must not reveal whether a specific username or email exists in the system. | UC-01 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-03` | A deactivated user account must not be allowed to sign in or access authenticated system functions. | UC-01, UC-02, UC-07 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-04` | A user without active system access or the required active role/permission must not be allowed to access the system. | UC-01, UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-05` | Identity information returned by Google must be successfully verified before it can be used for authentication. | UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-06` | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. | UC-02 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-07` | Signing in with Google must not automatically create a new internal user account. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-08` | User permissions are determined by the system and must not be selected manually during the sign-in process. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-09` | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. | UC-03 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-10` | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
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
| `BR-21` | An administrator must not be able to view a user's existing password. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-22` | Password reset may only be initiated for an account that is eligible to use password-based authentication. | UC-08 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-23` | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. | UC-11 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

**Quy tắc TrustCabinet đã được BA xác nhận:** tài khoản nội bộ đang hoạt động được phép đăng nhập
dù chưa được gán role hoặc chưa có capability. Capability chỉ quyết định chức năng nào được phép
dùng sau khi đăng nhập. Quy tắc này thay thế cách áp dụng BR-04 nguyên văn Report 3 ở trên; không
yêu cầu thêm quyền riêng để đăng nhập.

## BR-24–BR-44 — Cuộc họp và vòng đời phê duyệt

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-24` | All required meeting information must be provided before a meeting can be created. | UC-15 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-25` | Planned Start Time and Planned End Time are not stored as meeting-level schedule fields. A meeting's planned schedule is defined by its Meeting Sessions. | UC-15, UC-16, UC-17, UC-18, UC-26, UC-27 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-26` | A newly created meeting must have Draft status. | UC-15 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-27` | Meeting information may be edited only while the meeting is in Draft or Rejected status. | UC-16 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-28` | Updating meeting information must not implicitly change the meeting lifecycle status. | UC-16 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-29` | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
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

**Áp dụng trong TrustCabinet — BA đã chốt (UC-17, UC-18 và mở đích notification):** quyền đọc phụ
thuộc cả quan hệ với meeting và trạng thái hiện tại. Người tạo được đọc ở mọi trạng thái. Người duyệt
đang được gán được đọc từ `WaitingApproval` trở đi. Secretary hiện hành có tài khoản hoạt động được đọc từ `Draft` để chuẩn bị cuộc họp theo F08;
ở `WaitingApproval` được xem nhưng không được thêm participant, đổi role hoặc xóa participant.
Người được gán vào meeting chỉ với vai trò `Chair`, `Member` hoặc `Guest` được đọc từ `Published` trở đi. Trạng thái lời mời (`Pending`,
`Accepted`, `Declined` hoặc `Delegated`) không làm mất quyền đọc meeting đã công bố. Notification không
cấp quyền đọc; mở đích phải kiểm tra lại theo BR-29. Quyền đọc tài liệu, recording và dữ liệu nhạy cảm
tuân theo các rule riêng. Ngoại lệ quyền đọc của Secretary trước `Published` là quyết định F08
ngày 03/10/2026, cần đồng bộ vào UC-17/18 và kiểm tra quyền API khi triển khai F08.

**Áp dụng trong TrustCabinet:** Planned Start Time và Planned End Time thuộc về Meeting Session,
không phải thông tin cấp Meeting (BR-25). Khi tạo/sửa Meeting Session, Planned End Time phải sau
Planned Start Time (BR-62).

## BR-45–BR-53 — Thông báo

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-45` | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time, or when a task is assigned to them. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-46` | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-47` | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. | UC-23, UC-24, UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-48` | The notification bell shows how many of the current user's notifications are unread. | UC-22, UC-24, UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-49` | Opening the notification list does not mark any notification as read. | UC-22 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-50` | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-51` | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. | UC-24 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-52` | A meeting or task notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-53` | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. | UC-25 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

## BR-54–BR-63 — Phiên họp và vòng đời cuộc họp

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To (Report 3) | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-54` | Meeting Sessions may be created, updated, or cancelled only by the Secretary assigned to the parent meeting. The selected or newly created session must be Planned, and the parent meeting must be Draft, Rejected, Approved, Published, or InProgress. Session planning is not allowed while the meeting is WaitingApproval or after it is Ended. | UC-26, UC-27, UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-55` | A meeting does not need a Meeting Session in order to be submitted or resubmitted for approval. | UC-19 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-56` | Meeting Sessions are optional and are not required to submit a meeting for approval or to publish an Approved meeting. | UC-19, UC-21 | Report 3 source-of-truth F08/GOF09 (03/10/2026); supersedes original Report 3 wording | UC-19, UC-21 |
| `BR-57` | Meeting Sessions are not permanently deleted. A Planned Meeting Session that will no longer take place is changed to Cancelled and retained for traceability. An InProgress, Completed, or Cancelled Meeting Session cannot be updated or cancelled. | UC-27, UC-28 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-58` | A Meeting Session follows the lifecycle Planned → InProgress → Completed, with Planned → Cancelled as a terminal cancellation path. A Cancelled Meeting Session cannot be started. | UC-28, UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-59` | Only one Meeting Session of the same meeting may be InProgress at a time. | UC-29 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-60` | Starting or ending a Meeting Session changes only that session's lifecycle; it does not implicitly start or end the parent Meeting. The Meeting lifecycle is controlled explicitly under BR-90–BR-92. | UC-29, UC-30, UC-43, UC-44 | Report 3 source-of-truth F08/GOF09 (03/10/2026); supersedes original Report 3 wording | UC-29, UC-30, UC-43, UC-44 |
| `BR-61` | An InProgress meeting may be ended when no Meeting Session remains Planned or InProgress. Completed and Cancelled sessions are terminal and are not considered unfinished; a meeting with zero sessions satisfies this condition. | UC-44 | Report 3 source-of-truth F08/GOF09 (03/10/2026); clarifies original Report 3 wording | UC-44 |
| `BR-62` | A Meeting Session's Planned End Time must be later than its Planned Start Time. | UC-26, UC-27 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-63` | Only the Chair assigned to a meeting may start or end a Meeting Session of that meeting. | UC-29, UC-30 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |

## BR-64–BR-77 — Chương trình nghị sự

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-64` | Each Agenda Item belongs to exactly one Meeting Session. Reordering changes the order only within that Meeting Session and does not move an Agenda Item to another session. | UC-31, UC-34, UC-35, UC-36 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-65` | Only the Secretary assigned to the parent meeting may create, update, delete, or reorder Agenda Items, and only while the selected Meeting Session is Planned. | UC-31, UC-32, UC-33, UC-34 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
| `BR-66` | An Agenda Item must have a Title and a Planned Duration greater than zero. Presenter and Description are optional. Any attached documents must be available to the parent meeting. | UC-31, UC-32 | Chưa ghi trong Report 3 | Chưa ghi trong Report 3 |
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
| `BR-77` | Meeting Sessions are optional. A Published meeting may have no Planned Meeting Session, and cancelling the last Planned Meeting Session is allowed while the Meeting remains Published. | UC-28 | Report 3 source-of-truth F08/GOF09 (03/10/2026); supersedes original Report 3 wording | UC-28 |

## BR-78–BR-89 — Thành phần tham dự và vai trò cuộc họp (F08)

| BR-ID | Rule Statement | Applies To | Nguồn / ghi chú | Enforced By |
|---|---|---|---|---|
| `BR-78` | Before the first Secretary is assigned, only the Organizer may add the first internal participant, who must be Secretary. Afterwards only an assigned Secretary may add internal participants, invite Guests, change internal roles, or remove/revoke roster entries. Editing is allowed in Draft, Rejected, Approved, Published, and InProgress; WaitingApproval, Ended, MinutesIssued, and Archived are read-only. | UC-38, UC-39, UC-40 | Report 3 source-of-truth F08 (03/10/2026) | UC-38–UC-40 |
| `BR-79` | Internal participants are selected individually from active accounts and may appear at most once as a current participant per meeting. No organisational-unit invitations. External Guests use separate meeting-specific invitations, not internal participant rows. | UC-37, UC-38 | Report 3 source-of-truth F08 (03/10/2026) | UC-37, UC-38 |
| `BR-80` | Each current internal participant has exactly one meeting role: Chair, Secretary, or Member, independent of system role. Guests have fixed Guest access and cannot satisfy required Chair/Secretary roles. | UC-38, UC-39 | Report 3 source-of-truth F08 (03/10/2026) | UC-38, UC-39 |
| `BR-81` | A new internal participant or Guest invitation starts Pending, with no response time or delegate. Delegation applies only to internal participants. | UC-38, UC-41 | Report 3 source-of-truth F08 (03/10/2026) | UC-38, UC-41 |
| `BR-82` | While Published or InProgress, internal additions may notify in-app and by email; Guest invitations use email; role changes notify the internal participant; removal/revocation may notify by the applicable channels. Delivery is asynchronous and retryable without rollback. Removal notices must not contain protected meeting links. | UC-21, UC-38, UC-39, UC-40 | Report 3 source-of-truth F08 (03/10/2026) | UC-21, UC-38–UC-40 |
| `BR-83` | Changing a Meeting Role changes only the participant's role in the selected meeting and does not change the user's system Role or Invitation Response. | UC-39 | Report 3 source-of-truth F08 (03/10/2026) | UC-39 |
| `BR-84` | Removing an internal participant ends their outgoing delegation; revoking a Guest invitation invalidates Guest access. Independent incoming access remains. Preserve response/attendance evidence for published meetings, exclude historical records from current access/roster/role counts, and disconnect an affected room user only if no other valid room-entry relationship remains. Removal/revocation is disallowed in WaitingApproval and after Ended. | UC-37, UC-40, UC-42, UC-45 | Report 3 source-of-truth F08 (03/10/2026) | UC-37, UC-40, UC-42, UC-45 |
| `BR-85` | Only the owner of a current invitation may respond. Internal participants may Accept, Decline, or Delegate; Guests may Accept or Decline using their verified meeting invitation. Responses may change while Published or InProgress. | UC-41 | Report 3 source-of-truth F08 (03/10/2026) | UC-41 |
| `BR-86` | Delegation records one other active internal user on the original participant row; it creates no participant row and transfers no Chair/Secretary role. The original retains meeting read access but loses room-entry access while delegated; the delegate receives meeting and room access, and an existing participant keeps their role. A user cannot delegate to themself or represent multiple participants in one meeting. Replacement/withdrawal revokes access granted only by the old delegation; apply access changes atomically and re-evaluate room entry. | UC-41 | Report 3 source-of-truth F08 (03/10/2026) | UC-41 |
| `BR-87` | Record the latest response and response time for each current internal participant and Guest invitation. Response is independent of Attendance; Accepted does not prove attendance. Delegated applies only to internal participants. | UC-41, UC-42 | Report 3 source-of-truth F08 (03/10/2026) | UC-41, UC-42 |
| `BR-88` | Authorize and filter roster/response data server-side. Secretary may read/manage before publication in editable states; WaitingApproval is read-only. Internal Chair/Member alone grant no pre-publication meeting read access. Delegates and verified Guests receive only relationship-scoped access; Guests get no privileged operations. Unauthorized access to a specific meeting returns the same not-found result as an unknown meeting and no unauthorized roster data. | UC-17, UC-18, UC-37, UC-41, UC-42, UC-45 | Report 3 source-of-truth F08 (03/10/2026); meeting visibility applied in TrustCabinet | UC-17, UC-18, UC-37, UC-41, UC-42, UC-45 |
| `BR-89` | Retain a current internal Secretary at all times after initial assignment. Publication and Meeting start require active current Chair and Secretary; Meeting Session start also requires those roles but never changes the parent Meeting lifecycle. Role change/removal cannot remove the last Secretary in editable states or last Chair while Published/InProgress. Multiple Chairs/Secretaries are allowed; each participant has one role. Invitation response does not affect readiness. | UC-21, UC-29, UC-38–UC-40, UC-43 | Report 3 source-of-truth F08 (03/10/2026) | UC-21, UC-29, UC-38–UC-40, UC-43 |

## BR-90–BR-103 — Vòng đời cuộc họp và phòng họp trực tuyến (F08 / GOF09)

| BR-ID | Rule Statement | Applies To | Nguồn / ghi chú | Enforced By |
|---|---|---|---|---|
| `BR-90` | Only an active assigned Chair may explicitly start a Published Meeting or end an InProgress Meeting. | UC-43, UC-44 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-43, UC-44 |
| `BR-91` | Meeting Sessions are optional. Meeting may start with zero sessions and may end when no session remains Planned or InProgress; zero sessions satisfies the condition. | UC-43, UC-44 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-43, UC-44 |
| `BR-92` | Meeting and Meeting Session lifecycles are separate; transitions in one do not change the other's status. | UC-28–UC-30, UC-43, UC-44 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-28–UC-30, UC-43, UC-44 |
| `BR-93` | The Online Meeting Room belongs to the Meeting, not a Meeting Session. A session is not required to create, join, remain in, or leave the room. | UC-45–UC-52 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-45–UC-52 |
| `BR-94` | Room is available only for Online/Hybrid meetings in Published/InProgress. Entry requires a current internal participant relationship, effective delegation, or a successfully verified, current, unexpired, unrevoked Guest invitation. | UC-45 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-45 |
| `BR-95` | Room participation and controls do not change Meeting or Meeting Session lifecycle. The same room may stay available across multiple sessions. | UC-45–UC-52 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-45–UC-52 |
| `BR-96` | When the Meeting becomes Ended, the room stops accepting joins and disconnects connected users. Leaving a room does not end the Meeting or a Meeting Session. | UC-44–UC-46 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-44–UC-46 |
| `BR-97` | Users control only their own microphone and camera. Chair may mute or remove another connected user, but cannot enable their microphone or camera. | UC-47, UC-48, UC-51 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-47, UC-48, UC-51 |
| `BR-98` | At most one screen share is active per room; a new share stops the previous share first. | UC-49 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-49 |
| `BR-99` | Room presence is the set of currently connected users, distinct from the Meeting roster, and may include active-speaker indication. | UC-50 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-50 |
| `BR-100` | In-room chat is scoped to the room and available only to authorized connected users. Chat does not change either lifecycle. | UC-52 | Report 3 source-of-truth GOF09 (03/10/2026) | UC-52 |
| `BR-101` | A Guest has Guest Name and Email, needs no internal account, and has at most one current invitation per email per meeting. Guests have no system or privileged Meeting Role. Do not invite as Guest an email already represented by a current internal participant. | UC-37, UC-38, UC-40–UC-42, UC-45 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-37, UC-38, UC-40–UC-42, UC-45 |
| `BR-102` | Guest access uses a secure high-entropy invitation scoped to one meeting and email verification by one-time code. Guest session expires by configured validity, needs no internal login, and cannot access another meeting. | UC-21, UC-38, UC-41, UC-45 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-21, UC-38, UC-41, UC-45 |
| `BR-103` | Revoking a Guest invitation immediately invalidates its sessions/tokens and disconnects a connected Guest if no other valid room-entry relationship exists. Preserve historical response/attendance evidence when required. | UC-37, UC-40–UC-42, UC-45 | Report 3 source-of-truth F08/GOF09 (03/10/2026) | UC-37, UC-40–UC-42, UC-45 |

## Ghi chú về dữ liệu nguồn

Với BR-01–BR-77, Rule Statement và Applies To được chép từ bảng Report 3 do BA cung cấp. BR-78–BR-103 được đồng bộ từ Report 3 source-of-truth F08/GOF09 ngày 03/10/2026. Không có Rationale hoặc Enforced By trong nguồn cho BR-01–BR-77; với các BR mới, Enforced By liệt kê UC liên quan để dễ rà soát, không khẳng định implementation đã tồn tại.

Nếu một BR chưa có UC tham chiếu, BR vẫn được giữ trong danh mục theo Report 3; việc thiếu Applies To không tự động thêm hành vi vào Use Case hoặc sequence.
