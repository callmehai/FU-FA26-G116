# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

Danh mục này đồng bộ nguyên văn mục *V.1 Business Rules* của [Report 3 — SRS](../08-reports/Report-3_Software_Requirement_Specification.md) (bản DOCX ngày 10/10/2026). Có **131 BR đang dùng** trong dải `BR-01` → `BR-167`; mã `BR` độc lập với mã thông báo `MSG`.

## Quy ước đồng bộ

- Report 3 là nguồn chuẩn. Sửa BR trong DOCX trước, chạy `python scripts/sync_reports.py`, rồi đồng bộ lại file này.
- Số UC trong cột *Applies To* là số UC **sau khi gom và đánh số lại** (UC-01 → UC-70). Bảng đổi số cũ → mới nằm ở [Master Use Case Inventory §5](00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
- **Không đánh lại số BR.** BR đã ngừng dùng giữ nguyên mã, không tái cấp (danh sách ở cuối file).
- UC Specification và Sequence Diagram dẫn chiếu đúng BR ID tại bước áp dụng, không chép lại nội dung.
- Khi BR dẫn đến một thông báo, liên kết tình huống với mã trong [danh mục mã thông báo](../03-api/00-danh-muc-ma-thong-bao.md).

## Danh mục BR đang dùng

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To | Rationale | Enforced By |
|---|---|---|---|---|
| `BR-01` | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. | UC-01, UC-07 | — | — |
| `BR-02` | Authentication failure messages must not reveal whether a specific username or email exists in the system. | UC-01 | — | — |
| `BR-03` | A deactivated user account must not be allowed to sign in or access authenticated system functions. | UC-01, UC-02, UC-06c, UC-07 | — | — |
| `BR-04` | A user without active system access or the required active role/permission must not be allowed to access the system. | UC-01, UC-02 | — | — |
| `BR-05` | Identity information returned by Google must be successfully verified before it can be used for authentication. | UC-02 | — | — |
| `BR-06` | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. | UC-02 | — | — |
| `BR-07` | Signing in with Google must not automatically create a new internal user account. | UC-02 | — | — |
| `BR-08` | User permissions are determined by the system and must not be selected manually during the sign-in process. | UC-01, UC-02 | — | — |
| `BR-09` | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. | UC-03 | — | — |
| `BR-10` | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. | UC-07 | — | — |
| `BR-11` | List views must support pagination when the number of records exceeds the configured page size. | UC-05, UC-17, UC-66 | — | — |
| `BR-12` | When a search or filter returns no matching records, the system must display an appropriate no-results message instead of treating the result as a system error. | UC-05, UC-17 | — | — |
| `BR-13` | Username and email must each be unique across user accounts. | UC-06a | — | — |
| `BR-14` | All mandatory user-account information must be provided and valid before an account can be created or updated. | UC-06a, UC-06b | — | — |
| `BR-15` | A newly created user account must have Active status by default. | UC-06a | — | — |
| `BR-16` | Successful administrative changes to user accounts or role-permission configuration must be recorded in the audit log. | UC-06a, UC-06b, UC-06c, UC-07, UC-11, UC-12a, UC-12b, UC-12c | — | — |
| `BR-17` | General user-account updates may modify Full Name, Job Title, and Contact Information. Email, System Role, and password credentials must be managed separately. | UC-06b | — | — |
| `BR-18` | When a user account is deactivated, all active authenticated sessions for that account must be terminated. | UC-06c | — | — |
| `BR-19` | Deactivating a user account must not delete historical records associated with that user. | UC-06c | — | — |
| `BR-20` | A password-reset link must have a limited validity period and must not be reusable after a successful password reset. | UC-07 | — | — |
| `BR-21` | An administrator must not be able to view a user's existing password. | UC-07 | — | — |
| `BR-22` | Password reset may only be initiated for an account that is eligible to use password-based authentication. | UC-07 | — | — |
| `BR-23` | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. | UC-11 | — | — |
| `BR-24` | All required meeting information must be provided before a meeting can be created. Planned End Time must be after Planned Start Time. | UC-14 | — | — |
| `BR-25` | Planned Start Time and Planned End Time are required meeting-level schedule fields; each Meeting represents one sitting. | UC-14, UC-15, UC-16, UC-17, UC-18, UC-20 | — | — |
| `BR-26` | A newly created meeting must have Draft status. | UC-14 | — | — |
| `BR-27` | Meeting information may be edited in Draft or Rejected; in Published, the Meeting Organizer may reschedule only times or location. | UC-15 | — | — |
| `BR-28` | Updating meeting information must not implicitly change the meeting lifecycle status. | UC-15 | — | — |
| `BR-29` | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting. | UC-16, UC-17, UC-26, UC-27, UC-30, UC-32a, UC-37, UC-68 | — | — |
| `BR-30` | Meeting time-range search matches when its own planned start/end interval overlaps the selected range. | UC-17 | — | — |
| `BR-31` | When multiple search or filter criteria are applied, a record must satisfy all applicable criteria to appear in the result. | UC-05, UC-17 | — | — |
| `BR-32` | The meeting list must support sorting, and the current search, filter, and sorting criteria must be preserved when navigating between pages. | UC-17 | — | — |
| `BR-33` | Meeting approval submission requires all mandatory information including the planned schedule and Draft or Rejected status. | UC-18 | — | — |
| `BR-34` | Submitting a Draft or Rejected Meeting for approval changes its status to WaitingApproval. General Meeting information cannot be edited until an approval decision is made. This restriction does not apply to Agenda Management: the assigned Secretary may create, update, delete and reorder Agenda Items under BR-65, BR-68 and BR-74. | UC-15, UC-18, UC-23a, UC-23b, UC-23c, UC-23d | — | — |
| `BR-35` | For each approval cycle, an eligible Meeting Approver must be assigned. If no eligible approver is available, submission cannot proceed: the Meeting remains Draft or Rejected, its existing assignment and history are unchanged, and no submission notification is issued. Only the currently assigned Meeting Approver with the required permission may approve or reject the Meeting. | UC-18, UC-19 | — | — |
| `BR-36` | Approving a meeting changes its status from WaitingApproval to Approved. Rejecting a meeting changes its status from WaitingApproval to Rejected. A rejection reason is mandatory. | UC-19 | — | — |
| `BR-37` | Only an authorized Meeting Organizer may publish a meeting that is in Approved status and satisfies the publication readiness requirements. Publishing changes the meeting status from Approved to Published and initiates participant invitations. | UC-20 | — | — |
| `BR-38` | Every meeting lifecycle transition must have a history record containing at least the acting user, timestamp, action and resulting status; a rejection also includes its reason. For submission, approval, rejection and publication, the corresponding assignment, decision, status and history must be completed together. If any part cannot be completed, all previous Meeting information and history remain unchanged, no new notification or invitation is issued, and the action is not reported as successful. Existing history must never be changed or removed. | UC-18, UC-19, UC-20, UC-21, UC-22 | — | — |
| `BR-39` | After successful publication, current internal participants must be notified in the application and sent invitation emails. Each external Guest with a current invitation must receive a separate email invitation to verify their invited email address and access only that Meeting. Invitation delivery must not delay confirmation of publication. | UC-20 | — | — |
| `BR-40` | If an invitation notification or email cannot be delivered, the system must retry delivery. The Meeting remains Published; delivery failure does not undo successful publication. | UC-20 | — | — |
| `BR-41` | Meeting publication scope is strictly limited to meetings in Approved status and cannot be bypassed by direct state manipulation. | UC-20 | — | — |
| `BR-42` | All participant invitation notifications must include a direct link to the published meeting details and attached materials. | UC-20, UC-28a | — | — |
| `BR-43` | System logging must track the queuing and delivery status of all asynchronous invitation messages for auditability. | UC-20, UC-28a | — | — |
| `BR-44` | Re-publishing or updating invitation delivery for an already published meeting requires explicit organizer action and creates a distinct lifecycle record. | UC-15, UC-20 | — | — |
| `BR-45` | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time. | UC-20, UC-66 | — | — |
| `BR-46` | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. | UC-66 | — | — |
| `BR-47` | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. | UC-67a, UC-67b, UC-68 | — | — |
| `BR-48` | The notification bell shows how many of the current user's notifications are unread. | UC-66, UC-67b, UC-68 | — | — |
| `BR-49` | Opening the notification list does not mark any notification as read. | UC-66 | — | — |
| `BR-50` | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. | UC-68 | — | — |
| `BR-51` | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. | UC-67b | — | — |
| `BR-52` | A meeting notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. | UC-68 | — | — |
| `BR-53` | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. | UC-68 | — | — |
| `BR-62` | A Meeting Planned End Time must be later than Planned Start Time. | UC-14, UC-15, UC-18 | — | — |
| `BR-64` | Each Agenda Item belongs directly to one Meeting. Reordering is within that Meeting. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26 | — | — |
| `BR-65` | Only the active assigned Secretary may create, update, delete or reorder Agenda Items while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Chair-only execution actions remain governed by BR-69. | UC-23a, UC-23b, UC-23c, UC-23d | — | — |
| `BR-66` | An Agenda Item requires Title and Planned Duration greater than zero. Presenter and Description are optional; attached documents must belong to the Meeting. | UC-23a, UC-23b | — | — |
| `BR-67` | A new Agenda Item is Pending with explicit ordering within its Meeting. | UC-23a, UC-23d | — | — |
| `BR-68` | Agenda structure may be created, updated, deleted or reordered by the assigned Secretary in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. These changes do not change the Meeting lifecycle or grant authority to set/complete the current Item. No Agenda modification is permitted in Ended or subsequent states. | UC-23a, UC-23b, UC-23c, UC-23d | — | — |
| `BR-69` | Only the current Chair may set or complete the current Agenda Item while Meeting is InProgress. | UC-24, UC-25 | — | — |
| `BR-70` | For the agenda execution covered by these use cases, setting a Pending Agenda Item as current changes it to InProgress; completing the current Agenda Item changes it from InProgress to Done. | UC-24, UC-25 | — | — |
| `BR-71` | At most one Agenda Item of the Meeting is InProgress at a time. | UC-24 | — | — |
| `BR-72` | Agenda Item start/end times and actual duration are recorded without automatically ending the Meeting. | UC-24, UC-25 | — | — |
| `BR-73` | Agenda execution updates are reflected in real time for authorized viewers of the same Meeting. | UC-24, UC-25 | — | — |
| `BR-74` | Agenda Management is permitted in Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including while approval is pending and while the Meeting is running. It is locked in Ended and subsequent lifecycle states. The server rechecks status and Secretary assignment when saving. | UC-23a, UC-23b, UC-23c, UC-23d | — | — |
| `BR-75` | Rescheduling a Published Meeting requires a planned start time later than the current time. | UC-15 | — | — |
| `BR-76` | Rescheduling a Published Meeting must notify all current invitees; delivery failures do not undo the change. | UC-15 | — | — |
| `BR-78` | When a meeting has no Secretary, only the Meeting Organizer may add the first internal participant, and that participant must be assigned the Secretary Meeting Role. | UC-28a | — | — |
| `BR-79` | After the first Secretary has been assigned, only an assigned Secretary may manage the meeting roster. | UC-28a, UC-28b, UC-28c | — | — |
| `BR-80` | Participant management is allowed only while the Meeting is Draft, Rejected, Approved, Published, or InProgress. | UC-28a, UC-28b, UC-28c | — | — |
| `BR-81` | An internal participant must reference an active internal user account. | UC-28a | — | — |
| `BR-82` | An internal user may appear at most once as a current participant in the same Meeting. | UC-27, UC-28a | — | — |
| `BR-83` | Internal participants are added individually; organisational-unit invitations are not supported. | UC-28a | — | — |
| `BR-84` | External Guests are represented by meeting-specific Guest invitations rather than internal Meeting Participant records. | UC-27, UC-28a, UC-28c | — | — |
| `BR-85` | Each current internal participant has exactly one Meeting Role: Chair, Secretary, or Member. | UC-28a, UC-28b | — | — |
| `BR-86` | A participant's Meeting Role is independent of the user's system Role. | UC-28a, UC-28b | — | — |
| `BR-87` | An external Guest cannot satisfy a required Chair or Secretary role. | UC-20, UC-21 | — | — |
| `BR-88` | A newly added internal participant or external Guest invitation starts with Pending Invitation Response and no response time. | UC-28a | — | — |
| `BR-90` | When an internal participant is added to a Published or InProgress Meeting, the participant is notified through the applicable invitation channels. | UC-28a | — | — |
| `BR-91` | When an external Guest is invited to a Published or InProgress Meeting, the invitation is delivered by email. | UC-28a | — | — |
| `BR-92` | Changing an internal participant's Meeting Role while the Meeting is Published or InProgress notifies that participant. | UC-28b | — | — |
| `BR-93` | Removing an internal participant or revoking a Guest invitation from a Published or InProgress Meeting notifies the affected invitee through the applicable channel. | UC-28c | — | — |
| `BR-94` | Failure to deliver a participant or Guest notification does not undo an otherwise successful roster change. | UC-28a, UC-28b, UC-28c | — | — |
| `BR-95` | A participant-removal or Guest-revocation notification must not contain a protected meeting-access link. | UC-28c | — | — |
| `BR-96` | Changing a Meeting Role does not change the participant's Invitation Response. | UC-28b | — | — |
| `BR-97` | Removing a current internal participant revokes access granted by that participant assignment. | UC-28c, UC-37 | — | — |
| `BR-100` | Revoking an external Guest invitation revokes access granted by that Guest relationship. | UC-28c, UC-37 | — | — |
| `BR-101` | After a roster or Guest-access relationship changes, room-entry eligibility must be re-evaluated; a connected user is disconnected only when no valid room-entry relationship remains. | UC-28c, UC-37 | — | — |
| `BR-102` | Removing or revoking a roster entry after publication preserves its Invitation Response and Attendance evidence as historical data. | UC-27, UC-28c, UC-30 | — | — |
| `BR-103` | Historical or revoked roster records are excluded from the current roster, current access checks, and required-role counts. | UC-27, UC-28c, UC-30 | — | — |
| `BR-104` | Only the owner of a current invitation may respond to that invitation. | UC-29 | — | — |
| `BR-105` | Current internal participants and external Guests may respond only Accepted or Declined. Responding does not transfer or change any Meeting Role. | UC-29 | — | — |
| `BR-106` | Invitation Responses may be changed while the Meeting is Published or InProgress. | UC-29 | — | — |
| `BR-115` | The latest Invitation Response and its response time are recorded for each current invitation. | UC-29, UC-30 | — | — |
| `BR-116` | Invitation Response and Attendance are independent; Accepted does not mean the invitee attended the Meeting. | UC-29, UC-30, UC-31 | — | — |
| `BR-117` | Roster and invitation-response information is accessible only to users whose relationship with the Meeting permits that information. | UC-16, UC-27, UC-29, UC-30 | — | — |
| `BR-118` | An active assigned Secretary may read the Meeting before publication; WaitingApproval is read-only for roster management. | UC-16, UC-17, UC-27, UC-28a, UC-28b, UC-28c | — | — |
| `BR-119` | A Chair or Member assignment alone does not grant meeting-read access before the Meeting is Published. | UC-16, UC-17 | — | — |
| `BR-120` | An external Guest receives access only to the Meeting to which that Guest was invited. | UC-29, UC-37 | — | — |
| `BR-121` | A user who is not authorized to access a Meeting must not receive its roster or invitation-response information. | UC-27, UC-30 | — | — |
| `BR-122` | After the first Secretary has been assigned, a Meeting must retain at least one current internal participant assigned as Secretary. | UC-28a, UC-28b, UC-28c | — | — |
| `BR-123` | A Meeting may have at most one current Chair at a time. | UC-28a, UC-28b | — | — |
| `BR-124` | Publishing a Meeting requires exactly one current Chair whose internal account is active. | UC-20 | — | — |
| `BR-125` | Publishing a Meeting requires at least one current Secretary whose internal account is active. | UC-20 | — | — |
| `BR-126` | A Published or InProgress Meeting must not lose its current Chair unless a valid replacement is assigned. | UC-28b, UC-28c | — | — |
| `BR-127` | Starting a Meeting requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-21 | — | — |
| `BR-129` | Invitation responses do not change readiness requirements for the assigned active Chair and Secretary when publishing or starting a Meeting. | UC-20, UC-21 | — | — |
| `BR-130` | Only an active current Chair assigned to the Meeting may explicitly start or end that Meeting. | UC-21, UC-22 | — | — |
| `BR-131` | A Meeting may be started only while it is Published. | UC-21 | — | — |
| `BR-132` | A Meeting may be ended only while it is InProgress. | UC-22 | — | — |
| `BR-143` | When a Meeting becomes Ended, its Online Meeting Room stops accepting joins and disconnects currently connected users. | UC-22, UC-37 | — | — |
| `BR-148` | An external Guest is identified by Guest Name and Email and does not require an internal user account. | UC-27, UC-28a, UC-29 | — | — |
| `BR-149` | Within one Meeting, a Guest email may have at most one current Guest invitation. | UC-28a | — | — |
| `BR-150` | An external Guest has fixed Guest access and receives no system Role or privileged Meeting Role. | UC-28a, UC-37 | — | — |
| `BR-151` | A Guest invitation must not be created when the same email is already represented by a current internal participant of that Meeting. | UC-28a | — | — |
| `BR-152` | External Guest access requires a secure, meeting-specific invitation. | UC-20, UC-28a, UC-29, UC-37 | — | — |
| `BR-153` | An external Guest must verify control of the invited email before Guest access is established. | UC-29, UC-37 | — | — |
| `BR-154` | External Guest access is scoped only to the invited Meeting. | UC-29, UC-37 | — | — |
| `BR-155` | External Guest access expires according to the configured validity period. | UC-29, UC-37 | — | — |
| `BR-156` | External Guest access does not require an internal account login. | UC-28a, UC-29, UC-37 | — | — |
| `BR-157` | Revoking an external Guest invitation immediately invalidates access granted by that invitation. | UC-28c, UC-29, UC-37 | — | — |
| `BR-158` | Only the Meeting Organizer may reschedule a Published Meeting; only planned times or location may change. | UC-15 | PO 09/10/2026 | Meeting / Voting Management |
| `BR-159` | A Meeting cannot end while any Vote is Open. Completing an InProgress Agenda Item (status Done, actual end time and duration), changing the Meeting to Ended, recording Actual End Time and its lifecycle history must succeed atomically. If any part fails, both Meeting and Agenda Item retain their previous states and timestamps; no successful end history is recorded and room closure is not triggered. | UC-22 | PO 09/10/2026 | Meeting / Voting Management |
| `BR-160` | Votes may be created, opened, cast, tallied, and closed only while the associated Meeting is InProgress. Only current Chair, Secretary and Members may cast a vote; Guest cannot vote. No time-boxed voting, quorum or separate Resolution object exists. (See BR-165.) | UC-52a, UC-52b, UC-53, UC-54, UC-55, UC-56 | PO 09/10/2026 | Meeting / Voting Management |
| `BR-161` | After a Vote is closed and tallied, the Chair records the conclusion Passed, NotPassed or Deferred on that Meeting Vote. The tally and Chair conclusion are included in Meeting Minutes, not in a separate Resolution entity. | UC-52b, UC-56, UC-57, UC-59 | PO 09/10/2026 | Meeting / Voting Management |
| `BR-162` | When a new Meeting is created, Planned Start Time must be later than the current time. | UC-14 | PO Q7 / BF-02 | Meeting / Voting / Minutes services |
| `BR-163` | Online/Hybrid Meetings use the internal TrustCabinet Online Meeting Room. Online Link is managed by the system as part of Online Meeting Room functionality and may be empty until generated. Users cannot enter or modify it during Meeting creation, editing or rescheduling. Link generation is outside Create Meeting and Update Meeting. | UC-14, UC-15 | PO Q4 / BF-02 | Meeting / Voting / Minutes services |
| `BR-164` | Starting a Meeting creates an in-app notification for its currently invited internal participants; notification failure does not roll back the successful Meeting start. | UC-21 | PO Q11 / BF-03 | Meeting / Voting / Minutes services |
| `BR-165` | A vote may be created and opened only when its Meeting is InProgress, may be cast only by the current assigned Chair, Secretary and Members, and must be closed before the Chair records Passed, NotPassed or Deferred. Guests are not eligible. | UC-53, UC-54, UC-55, UC-56, UC-57 | PO D2 / BF-03 | Meeting / Voting / Minutes services |
| `BR-166` | When designated minutes signers request revision or minutes content changes after signing begins, previous signatures on the earlier content version are invalidated and the revised version requires a new signing cycle. Only the assigned Secretary may finalize and seal the completed minutes after all designated signers sign. | UC-60, UC-61, UC-62, UC-64, UC-65 | PO / BF-04 | Meeting / Voting / Minutes services |
| `BR-167` | User account list sorting must use an allowed sort field/order, and current search/filter/sort criteria are preserved across pages. | UC-05 | UC-05 user-list acceptance criteria | User Management |

## BR đã ngừng dùng

Các mã dưới đây không còn trong Report 3 — phần lớn thuộc Meeting Session, uỷ quyền tham dự (delegation), biểu quyết đặc biệt (time-boxed, quorum, Resolution) và các quy tắc đã gộp vào BR khác sau quyết định PO 09/10/2026 (xem [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md), [ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)). Giữ mã để truy vết, **không tái cấp**:

`BR-54`, `BR-55`, `BR-56`, `BR-57`, `BR-58`, `BR-59`, `BR-60`, `BR-61`, `BR-63`, `BR-77`, `BR-89`, `BR-98`, `BR-99`, `BR-107`, `BR-108`, `BR-109`, `BR-110`, `BR-111`, `BR-112`, `BR-113`, `BR-114`, `BR-128`, `BR-133`, `BR-134`, `BR-135`, `BR-136`, `BR-137`, `BR-138`, `BR-139`, `BR-140`, `BR-141`, `BR-142`, `BR-144`, `BR-145`, `BR-146`, `BR-147`
