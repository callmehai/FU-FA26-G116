# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

Danh mục này đồng bộ nguyên văn mục *V.1 Business Rules* của [Report 3 — SRS](../08-reports/Report-3_Software_Requirement_Specification.md) (bản DOCX cập nhật 14/10/2026, ADR-0018, ADR-0019 và quyết định PO về FE-07/FE-11). Có **230 BR đang dùng** trong dải `BR-01` → `BR-274`; mã `BR` độc lập với mã thông báo `MSG`.

## Quy ước đồng bộ

- Report 3 là nguồn chuẩn. Sửa BR trong DOCX trước, chạy `python scripts/sync_reports.py`, rồi đồng bộ lại file này.
- Số UC trong cột *Applies To* là số UC **sau khi gom và đánh số lại** (UC-01 → UC-72). Bảng đổi số cũ → mới nằm ở [Master Use Case Inventory §5](00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
- **Không đánh lại số BR.** BR đã ngừng dùng giữ nguyên mã, không tái cấp (danh sách ở cuối file).
- UC Specification và Sequence Diagram dẫn chiếu đúng BR ID tại bước áp dụng, không chép lại nội dung.
- Khi BR dẫn đến một thông báo, liên kết tình huống với mã trong [danh mục mã thông báo](../03-api/00-danh-muc-ma-thong-bao.md).

## Danh mục BR đang dùng

| BR-ID | Rule Statement (nguyên văn Report 3) | Applies To | Rationale | Enforced By |
|---|---|---|---|---|
| BR-ID | Rule Statement | Applies To | Rationale | Enforced By |
| `BR-01` | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. | UC-01, UC-07 |  |  |
| `BR-02` | Authentication failure messages must not reveal whether a specific username or email exists in the system. | UC-01 |  |  |
| `BR-03` | A deactivated user account must not be allowed to sign in or access authenticated system functions. | UC-01, UC-02, UC-06c, UC-07 |  |  |
| `BR-04` | A user without active system access or the required active role/permission must not be allowed to access the system. | UC-01, UC-02 |  |  |
| `BR-05` | Identity information returned by Google must be successfully verified before it can be used for authentication. | UC-02 |  |  |
| `BR-06` | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. | UC-02 |  |  |
| `BR-07` | Signing in with Google must not automatically create a new internal user account. | UC-02 |  |  |
| `BR-08` | User permissions are determined by the system and must not be selected manually during the sign-in process. | UC-01, UC-02 |  |  |
| `BR-09` | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. | UC-03 |  |  |
| `BR-10` | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. | UC-07 |  |  |
| `BR-11` | List views must support pagination when the number of records exceeds the configured page size. | UC-05, UC-17, UC-66 |  |  |
| `BR-12` | When a search or filter returns no matching records, the system must display an appropriate no-results message instead of treating the result as a system error. | UC-05, UC-17 |  |  |
| `BR-13` | Username and email must each be unique across user accounts. | UC-06a |  |  |
| `BR-14` | All mandatory user-account information must be provided and valid before an account can be created or updated. | UC-06a, UC-06b |  |  |
| `BR-15` | A newly created user account must have Active status by default. | UC-06a |  |  |
| `BR-16` | Successful administrative changes to user accounts or role-permission configuration must be recorded in the audit log. | UC-06a, UC-06b, UC-06c, UC-07, UC-11, UC-12a, UC-12b, UC-12c |  |  |
| `BR-17` | General user-account updates may modify Full Name, Job Title, and Contact Information. Email, System Role, and password credentials must be managed separately. | UC-06b |  |  |
| `BR-18` | When a user account is deactivated, all active authenticated sessions for that account must be terminated. | UC-06c |  |  |
| `BR-19` | Deactivating a user account must not delete historical records associated with that user. | UC-06c |  |  |
| `BR-20` | A password-reset link must have a limited validity period and must not be reusable after a successful password reset. | UC-07 |  |  |
| `BR-21` | An administrator must not be able to view a user's existing password. | UC-07 |  |  |
| `BR-22` | Password reset may only be initiated for an account that is eligible to use password-based authentication. | UC-07 |  |  |
| `BR-23` | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. | UC-11 |  |  |
| `BR-24` | All required meeting information must be provided before a meeting can be created. Planned End Time must be after Planned Start Time. | UC-14 |  |  |
| `BR-25` | Planned Start Time and Planned End Time are required meeting-level schedule fields; each Meeting represents one sitting. | UC-14, UC-15, UC-16, UC-17, UC-18, UC-20 |  |  |
| `BR-26` | A newly created meeting must have Draft status. | UC-14 |  |  |
| `BR-27` | Meeting information may be edited in Draft or Rejected; in Published, the Meeting Organizer may reschedule only times or location. | UC-15 |  |  |
| `BR-28` | Updating meeting information must not implicitly change the meeting lifecycle status. | UC-15 |  |  |
| `BR-29` | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting. | UC-16, UC-17, UC-26, UC-27, UC-30, UC-32a, UC-37, UC-68 |  |  |
| `BR-30` | Meeting time-range search matches when its own planned start/end interval overlaps the selected range. | UC-17 |  |  |
| `BR-31` | When multiple search or filter criteria are applied, a record must satisfy all applicable criteria to appear in the result. | UC-05, UC-17 |  |  |
| `BR-32` | The meeting list must support sorting, and the current search, filter, and sorting criteria must be preserved when navigating between pages. | UC-17 |  |  |
| `BR-33` | Meeting approval submission requires all mandatory information including the planned schedule and Draft or Rejected status. | UC-18 |  |  |
| `BR-34` | Submitting a Draft or Rejected Meeting for approval changes its status to WaitingApproval. General Meeting information cannot be edited until an approval decision is made. This restriction does not apply to Agenda Management: the assigned Secretary may create, update, delete and reorder Agenda Items under BR-65, BR-68 and BR-74. | UC-15, UC-18, UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| `BR-35` | For each approval cycle, an eligible Meeting Approver must be assigned. If no eligible approver is available, submission cannot proceed: the Meeting remains Draft or Rejected, its existing assignment and history are unchanged, and no submission notification is issued. Only the currently assigned Meeting Approver with the required permission may approve or reject the Meeting. | UC-18, UC-19 |  |  |
| `BR-36` | Approving a meeting changes its status from WaitingApproval to Approved. Rejecting a meeting changes its status from WaitingApproval to Rejected. A rejection reason is mandatory. | UC-19 |  |  |
| `BR-37` | Only an authorized Meeting Organizer may publish a meeting that is in Approved status and satisfies the publication readiness requirements. Publishing changes the meeting status from Approved to Published and initiates participant invitations. | UC-20 |  |  |
| `BR-38` | Every meeting lifecycle transition must have a history record containing at least the acting user, timestamp, action and resulting status; a rejection also includes its reason. For submission, approval, rejection and publication, the corresponding assignment, decision, status and history must be completed together. If any part cannot be completed, all previous Meeting information and history remain unchanged, no new notification or invitation is issued, and the action is not reported as successful. Existing history must never be changed or removed. | UC-18, UC-19, UC-20, UC-21, UC-22 |  |  |
| `BR-39` | After successful publication, current internal participants must be notified in the application and sent invitation emails. Each external Guest with a current invitation must receive a separate email invitation to verify their invited email address and access only that Meeting. Invitation delivery must not delay confirmation of publication. | UC-20 |  |  |
| `BR-40` | If an invitation notification or email cannot be delivered, the system must retry delivery. The Meeting remains Published; delivery failure does not undo successful publication. | UC-20 |  |  |
| `BR-41` | Meeting publication scope is strictly limited to meetings in Approved status and cannot be bypassed by direct state manipulation. | UC-20 |  |  |
| `BR-42` | All participant invitation notifications must include a direct link to the published meeting details and attached materials. | UC-20, UC-28a |  |  |
| `BR-43` | System logging must track the queuing and delivery status of all asynchronous invitation messages for auditability. | UC-20, UC-28a |  |  |
| `BR-44` | Re-publishing or updating invitation delivery for an already published meeting requires explicit organizer action and creates a distinct lifecycle record. | UC-15, UC-20 |  |  |
| `BR-45` | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time. | UC-20, UC-66 |  |  |
| `BR-46` | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. | UC-66 |  |  |
| `BR-47` | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. | UC-67a, UC-67b, UC-68 |  |  |
| `BR-48` | The notification bell shows how many of the current user's notifications are unread. | UC-66, UC-67b, UC-68 |  |  |
| `BR-49` | Opening the notification list does not mark any notification as read. | UC-66 |  |  |
| `BR-50` | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. | UC-68 |  |  |
| `BR-51` | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. | UC-67b |  |  |
| `BR-52` | A meeting notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. | UC-68 |  |  |
| `BR-53` | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. | UC-68 |  |  |
| `BR-62` | A Meeting Planned End Time must be later than Planned Start Time. | UC-14, UC-15, UC-18 |  |  |
| `BR-64` | Each Agenda Item belongs directly to one Meeting. Reordering is within that Meeting. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26 |  |  |
| `BR-65` | Only the active assigned Secretary may create, update, delete or reorder Agenda Items while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Chair-only execution actions remain governed by BR-69. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| `BR-66` | An Agenda Item requires Title and Planned Duration greater than zero. Presenter and Description are optional; attached documents must belong to the Meeting. | UC-23a, UC-23b |  |  |
| `BR-67` | A new Agenda Item is always Pending and is appended after the last existing position, including while the Meeting is InProgress. Existing Item positions are not changed. Reordering exchanges only current Pending Items within the same Meeting. | UC-23a, UC-23d |  |  |
| `BR-68` | For an InProgress or Done Agenda Item, only Title and Description may be edited as planning fields. | UC-23a, UC-23b, UC-23c, UC-23d | PO 14/10/2026 — atomic rule review |  |
| `BR-69` | Only the current Chair may set or complete the current Agenda Item while Meeting is InProgress. | UC-24, UC-25 |  |  |
| `BR-70` | Only the first Pending Agenda Item in the current Meeting order may be started. Starting it changes Pending to InProgress; completing the current Item changes InProgress to Done. The order and Item status must be checked again when the change is saved. | UC-24, UC-25 |  |  |
| `BR-71` | At most one Agenda Item of a Meeting may be InProgress at a time. Concurrent requests must not start two Items; an unsuccessful request leaves the Agenda unchanged. | UC-24 |  |  |
| `BR-72` | Starting and completing an Agenda Item record its actual start/end times and elapsed duration. The Chair may complete the current Item before, at or after its Planned Duration. Completion does not automatically start another Item or end the Meeting. | UC-24, UC-25 |  |  |
| `BR-73` | After an Agenda change is successfully saved, the complete Agenda is automatically refreshed for authorized users currently viewing the same Meeting. This applies to creation, editing, deletion, reordering, setting the current Item, completing an Item and completing the current Item when the Meeting ends. Refreshing must respect current Meeting access; an unsuccessful change must not be shown as a successful update. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26, UC-22 |  |  |
| `BR-74` | Agenda Management is permitted in Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including while approval is pending and while the Meeting is running. It is locked in Ended and subsequent lifecycle states. The server rechecks status and Secretary assignment when saving. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| `BR-75` | Rescheduling a Published Meeting requires a planned start time later than the current time. | UC-15 |  |  |
| `BR-76` | Rescheduling a Published Meeting must notify all current invitees; delivery failures do not undo the change. | UC-15 |  |  |
| `BR-78` | When a Meeting in Draft, Rejected or Approved has no current Secretary, the Meeting Organizer may assign exactly one active internal user as Secretary, including after an earlier Secretary was removed or changed role. No additional viewing permissions are granted by this recovery assignment; the Organizer gains no other roster-management authority. WaitingApproval remains read-only. (BR-79, BR-80, BR-122) An existing active Member may be assigned Secretary without creating a duplicate participant or resetting response/attendance. | UC-28a | PO 13/10/2026 — follow-up |  |
| `BR-79` | Current assigned Secretaries manage the Meeting roster while its status permits. The Meeting Organizer may assign a Secretary only when no current Secretary exists in Draft, Rejected or Approved, whether initially or to restore the assignment. This exception does not permit other role, permission or removal actions. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 — follow-up |  |
| `BR-80` | Participant management is allowed only while the Meeting is Draft, Rejected, Approved, Published, or InProgress. | UC-28a, UC-28b, UC-28c |  |  |
| `BR-81` | An internal participant must reference an active internal user account. | UC-28a |  |  |
| `BR-82` | An internal user may appear at most once as a current participant in the same Meeting. | UC-27, UC-28a |  |  |
| `BR-83` | Internal users are selected individually, and one or more users may be added in one submission. Organisational-unit invitations are not supported. (BR-168) | UC-28a |  |  |
| `BR-84` | External Guests are represented by meeting-specific Guest invitations rather than internal Meeting Participant records. | UC-27, UC-28a, UC-28c |  |  |
| `BR-85` | Each current internal participant has exactly one Meeting Role: Chair, Secretary, or Member. | UC-28a, UC-28b |  |  |
| `BR-86` | A participant's Meeting Role is independent of the user's system Role. | UC-28a, UC-28b |  |  |
| `BR-87` | An external Guest cannot satisfy a required Chair or Secretary role. | UC-20, UC-21 |  |  |
| `BR-88` | A newly added internal participant or external Guest invitation starts with Pending Invitation Response and no response time. | UC-28a |  |  |
| `BR-90` | When an internal participant is added to a Published or InProgress Meeting, the participant is notified through the applicable invitation channels. | UC-28a |  |  |
| `BR-91` | When an external Guest is invited to a Published or InProgress Meeting, the invitation is delivered by email. | UC-28a |  |  |
| `BR-92` | Changing a meeting role or additional meeting permissions in a Published or InProgress Meeting notifies every affected participant. Replacing the Chair notifies both people. Notification failure does not cancel saved changes. (BR-94, BR-169) | UC-28b |  |  |
| `BR-93` | Removing an internal participant or revoking a Guest invitation from a Published or InProgress Meeting notifies the affected invitee through the applicable channel. | UC-28c |  |  |
| `BR-94` | Failure to deliver a participant or Guest notification does not undo an otherwise successful roster change. | UC-28a, UC-28b, UC-28c |  |  |
| `BR-95` | A participant-removal or Guest-revocation notification must not contain a protected meeting-access link. | UC-28c |  |  |
| `BR-96` | Changing a meeting role or additional meeting permissions does not change the participant’s invitation response or attendance. | UC-28b |  |  |
| `BR-97` | Removing a current internal participant ends the access and additional permissions granted through that assignment. Independent valid access relationships remain effective. | UC-28c, UC-37 |  |  |
| `BR-100` | Revoking an external Guest invitation revokes access granted by that Guest relationship. | UC-28c, UC-37 |  |  |
| `BR-101` | After a roster or Guest-access relationship changes, room-entry eligibility must be re-evaluated; a connected user is disconnected only when no valid room-entry relationship remains. | UC-28c, UC-37 |  |  |
| `BR-102` | Removing or revoking a roster entry after publication preserves its Invitation Response and Attendance evidence as historical data. | UC-27, UC-28c, UC-30 |  |  |
| `BR-103` | Historical or revoked roster records are excluded from the current roster, current access checks, and required-role counts. | UC-27, UC-28c, UC-30 |  |  |
| `BR-104` | Only the owner of a current invitation may respond to that invitation. | UC-29 |  |  |
| `BR-105` | Current internal participants and external Guests may respond only Accepted or Declined. Responding does not transfer or change any Meeting Role. | UC-29 |  |  |
| `BR-106` | Invitation Responses may be changed while the Meeting is Published or InProgress. | UC-29 |  |  |
| `BR-115` | The latest Invitation Response and its response time are recorded for each current invitation. | UC-29, UC-30 |  |  |
| `BR-116` | Invitation Response and Attendance are independent; Accepted does not mean the invitee attended the Meeting. | UC-29, UC-30, UC-31 |  |  |
| `BR-117` | Roster and invitation-response information is accessible only to users whose relationship with the Meeting permits that information. | UC-16, UC-27, UC-29, UC-30 |  |  |
| `BR-118` | An active assigned Secretary may read the Meeting before publication; WaitingApproval is read-only for roster management. | UC-16, UC-17, UC-27, UC-28a, UC-28b, UC-28c |  |  |
| `BR-119` | A Chair or Member assignment alone does not grant meeting-read access before the Meeting is Published. | UC-16, UC-17 |  |  |
| `BR-120` | An external Guest receives access only to the Meeting to which that Guest was invited. | UC-29, UC-37 |  |  |
| `BR-121` | A user who is not authorized to access a Meeting must not receive its roster or invitation-response information. | UC-27, UC-30 |  |  |
| `BR-122` | Draft, Rejected and Approved Meetings may temporarily have no current Secretary. In these statuses the Organizer can assign a Secretary when none remains under BR-78. Published and InProgress Meetings must retain at least one current Secretary, or replace the last Secretary together with the same successful roster change. WaitingApproval roster changes remain prohibited; publication and start still require active Secretary assignments under BR-125 and BR-127. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 — follow-up |  |
| `BR-123` | A Meeting may have at most one current Chair at a time. | UC-28a, UC-28b |  |  |
| `BR-124` | Publishing a Meeting requires exactly one current Chair whose internal account is active. | UC-20 |  |  |
| `BR-125` | Publishing a Meeting requires at least one current Secretary whose internal account is active. | UC-20 |  |  |
| `BR-126` | A Published or InProgress Meeting must retain its current Chair. The Secretary may replace the Chair only by changing both affected roles together while preserving the required Secretary. (BR-169) | UC-28b, UC-28c |  |  |
| `BR-127` | Starting a Meeting requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-21 |  |  |
| `BR-129` | Invitation responses do not change readiness requirements for the assigned active Chair and Secretary when publishing or starting a Meeting. | UC-20, UC-21 |  |  |
| `BR-130` | Only an active current Chair assigned to the Meeting may explicitly start or end that Meeting. | UC-21, UC-22 |  |  |
| `BR-131` | A Meeting may be started only while it is Published. | UC-21 |  |  |
| `BR-132` | A Meeting may be ended only while it is InProgress. | UC-22 |  |  |
| `BR-143` | When a Meeting becomes Ended, its Online Meeting Room stops accepting joins and disconnects currently connected users. | UC-22, UC-37 |  |  |
| `BR-148` | An external Guest is invited using a name and email without an internal account. After email verification, the Guest provides a self-declared display name and optional job title for this Meeting. The invited name remains invitation evidence; only control of the email is verified. (BR-180) | UC-27, UC-28a, UC-29 | PO 13/10/2026 |  |
| `BR-149` | Within one Meeting, a Guest email may have at most one current Guest invitation. | UC-28a |  |  |
| `BR-150` | A Guest cannot hold an internal Meeting Role, including Chair or Secretary. | UC-28a, UC-37 | PO 14/10/2026 — atomic rule review |  |
| `BR-151` | A Guest invitation must not be created when the same email is already represented by a current internal participant of that Meeting. | UC-28a |  |  |
| `BR-152` | External Guest access requires a secure, meeting-specific invitation. | UC-20, UC-28a, UC-29, UC-37 |  |  |
| `BR-153` | An external Guest must verify control of the invited email before Guest access is established. | UC-29, UC-37 |  |  |
| `BR-154` | External Guest access is limited to the invited Meeting. | UC-29, UC-37 |  |  |
| `BR-155` | External Guest access expires according to the configured validity period. | UC-29, UC-37 |  |  |
| `BR-156` | External Guest access does not require an internal account login. | UC-28a, UC-29, UC-37 |  |  |
| `BR-157` | Revoking an external Guest invitation immediately invalidates access granted by that invitation. | UC-28c, UC-29, UC-37 |  |  |
| `BR-158` | Only the Meeting Organizer may reschedule a Published Meeting; only planned times or location may change. | UC-15 | PO 09/10/2026 | Meeting / Voting Management |
| `BR-159` | The Chair may end an InProgress Meeting regardless of open Votes or incomplete attendance; End Meeting neither requires nor automatically closes Votes. Completing any InProgress Agenda Item, changing Meeting status to Ended, recording Actual End Time and append-only lifecycle history succeed together. If saving fails, previous Meeting and Agenda states/timestamps remain and room closure is not triggered. Remaining Pending Agenda Items remain Pending. Existing ballots and Vote status are preserved; Ended Meetings accept no further ballots. The Chair may subsequently close and tally outstanding Open Votes under BR-270. | UC-22 | PO 13/10/2026 — follow-up | Meeting / Voting Management |
| `BR-160` | Only the current Chair may open a Draft Vote, and only while its Meeting is InProgress. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-161` | The current Chair may record Passed, NotPassed or Deferred, with an optional note, for a Closed and tallied Vote while its Meeting is InProgress or Ended. | UC-52b, UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-162` | When a new Meeting is created, Planned Start Time must be later than the current time. | UC-14 | PO Q7 / BF-02 | Meeting / Voting / Minutes services |
| `BR-163` | Online/Hybrid Meetings use the internal TrustCabinet Online Meeting Room. Online Link is managed by the system as part of Online Meeting Room functionality and may be empty until generated. Users cannot enter or modify it during Meeting creation, editing or rescheduling. Link generation is outside Create Meeting and Update Meeting. | UC-14, UC-15 | PO Q4 / BF-02 | Meeting / Voting / Minutes services |
| `BR-164` | Starting a Meeting creates an in-app notification for its currently invited internal participants; notification failure does not roll back the successful Meeting start. | UC-21 | PO Q11 / BF-03 | Meeting / Voting / Minutes services |
| `BR-165` | A ballot may be cast only while the Meeting is InProgress, the Vote is Open and the voter retains current Meeting access. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-166` | When designated minutes signers request revision or minutes content changes after signing begins, previous signatures on the earlier content version are invalidated and the revised version requires a new signing cycle. Only the assigned Secretary may finalize and seal the completed minutes after all designated signers sign. | UC-60, UC-61, UC-62, UC-64, UC-65 | PO / BF-04 | Meeting / Voting / Minutes services |
| `BR-167` | User account list sorting must use an allowed sort field/order, and current search/filter/sort criteria are preserved across pages. | UC-05 | UC-05 user-list acceptance criteria | User Management |
| `BR-168` | A submission adding multiple internal Meeting Participants must be saved as one indivisible roster change. If any selected account, assigned Meeting Role or additional Meeting Permission is invalid, no account in the submitted batch may be added. Invitation dispatch occurs only after a successful roster save; delivery errors follow BR-94. | UC-28a | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-169` | Replacing the Chair changes the outgoing Chair to Member and the incoming current internal participant to Chair together. The final list must retain the required Secretary and Chair. If either change fails, both people keep their previous roles. | UC-28b | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-170` | Only an active current assigned Secretary may save or revise official attendance while the Meeting is InProgress. For each current internal participant or current Guest, the only official saved attendance values are Present or Absent. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-171` | A person whose attendance has not been recorded remains unmarked. An unmarked person must not be counted as Absent. Only Present and Absent may be saved as official attendance. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-172` | Attendance may be saved or revised for one or more selected current internal participants or Guests without covering the full roster. Every submitted entry must identify a current person and contain Present or Absent; omitted entries retain their saved or unmarked value. All submitted changes succeed together or none are saved, with the responsible Secretary and save time. Revalidate submitted entries and current authority/status when saving; unrelated roster additions do not require attendance for those people. Attendance completeness never prevents End Meeting. | UC-31 | PO 13/10/2026 | Meeting Participant Management |
| `BR-173` | Each current internal participant has exactly one meeting role: Chair, Secretary or Member. Each role provides predefined permissions for that Meeting. Meeting roles and additional meeting permissions are independent of the person’s system role. | UC-27, UC-28a, UC-28b, UC-29, UC-30 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-174` | A current assigned Secretary may grant additional Meeting permissions to current internal participants and current Guest invitations, supplementing the permissions already provided by their Meeting role. View meeting participants and View invitation responses are examples, not an exhaustive list. The available permission catalogue and role-to-permission grant matrix remain to be specified; this rule does not invent either. Grants apply to this Meeting only and do not change system roles or internal Meeting roles. Guest-specific exclusions remain under BR-181. | UC-27, UC-28a, UC-28b, UC-30 | PO 13/10/2026 | Meeting Participant Management |
| `BR-175` | Only a current assigned Secretary may grant, change or revoke individual additional Meeting permissions while roster management is permitted. An internal participant retains their existing Meeting role; a Guest retains Guest access without acquiring an internal role. Organizer Secretary initialization/recovery does not itself grant permission-management authority. | UC-28a, UC-28b | PO 13/10/2026 | Meeting Participant Management |
| `BR-176` | Before publication, a Chair, Member or Guest cannot view the invited participant roster or invitation responses. From Published onward, Chair has both views by default and Secretary access follows existing rules; a current Member or verified current Guest needs the matching additional permission. View Invitation Responses includes names and emails in response rows but does not grant roster browsing/search. The connected-room list in UC-40 is available to connected Members and Guests without either additional grant. | UC-27, UC-30 | PO 13/10/2026 | Meeting Participant Management |
| `BR-177` | Internal Meeting role changes and individual additional-permission changes are saved consistently. Effective Meeting permissions combine role defaults and valid additional grants; changing a role does not by itself require discarding compatible grants. Removed assignments and revoked Guest invitations immediately lose their grants and access, while historical evidence is retained. Published/InProgress changes follow existing notifications. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 | Meeting Participant Management |
| `BR-178` | Official attendance must be recordable and retrievable for both current internal Meeting Participant records and current external Guest invitations. An attendance record includes the saved status, the responsible Secretary and the save time; historical evidence is retained when a published Meeting's participant or Guest is removed/revoked. | UC-28c, UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| `BR-180` | After verifying the invited email, a Guest provides a display name and job title for presentation in the Meeting. These are self-declared information; only control of the invited email is verified. Changing display information does not change the invitation identity or the invited email. Required-field rules and detailed profile validation are not finalized here. | UC-04, UC-27, UC-37, UC-40 | PO 13/10/2026 | Guest participation / Meeting Participant Management |
| `BR-181` | A Guest has the same participation rights as a Participant within the invited Meeting. | UC-04, UC-27, UC-28a, UC-28b, UC-29, UC-30, UC-31, UC-32a, UC-32c, UC-35, UC-37–UC-42, UC-52a, UC-52b, UC-55 | PO 14/10/2026 — atomic rule review | Guest participation / Meeting Participant Management |
| `BR-182` | Inviting a Guest into a Confidential or Secret Meeting displays a clear inline warning that the Guest will receive Member participation and Meeting-document access. Invitation creation uses the ordinary invite flow; no extra confirmation or separate sensitivity approval flow is required. Record the inviting Secretary, Meeting sensitivity and invitation time for accountability. | UC-28a | PO 13/10/2026 | Guest participation / Meeting Participant Management |
| `BR-183` | An uploaded file's content cannot be changed. | UC-32a, UC-32c, UC-33a, UC-33c, UC-34, UC-36, UC-71 | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-184` | Only the current Chair or Secretary may add or remove documents in their Meeting. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-185` | A Meeting document cannot be removed once Meeting Minutes exist. | UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-186` | Deleting a personal-library document does not remove the document from Meetings. | UC-33c, UC-71a | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-187` | Only PDF and DOCX documents may be uploaded. | UC-33a, UC-36 | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-188` | Documents of a Secret Meeting may be read online but not downloaded. | UC-32c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-189` | The current Chair may delete a Draft Vote while its Meeting is Approved, Published or InProgress. An Open or Closed Vote cannot be deleted. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-190` | The eligible voters are the current holders of the selected meeting roles with valid Meeting access when the Vote opens; the list and EligibleCount are fixed at that moment. Attendance does not determine eligibility. | UC-54, UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-191` | Personal-library folders may be nested at most five levels deep. | UC-34, UC-36, UC-71b, UC-71c, UC-72a, UC-72b, UC-72c, UC-72d | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-192` | An automatically created Meeting folder is named “<Meeting title> - dd/mm/yyyy”, using the upload date in Vietnam time. | UC-33a | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-193` | The same uploaded file may have only one active attachment in a Meeting. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-194` | A direct upload creates both the Meeting document and the personal-library document, or creates neither. | UC-33a | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-195` | Documents may be added in any Meeting state before the Meeting ends. | UC-33a | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-196` | Documents cannot be added to or removed from a completed Agenda Item. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-197` | Each Meeting-document addition or removal records who performed it and when. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-198` | Before publication, only the Meeting Organizer and the current Chair or Secretary may read Meeting documents. | UC-32a, UC-32c | PO 14/10/2026 — atomic rule review | Document Management |
| `BR-199` | A Secretary may grant a Guest the additional Meeting permissions available to an internal Participant. | UC-28b, UC-32a, UC-32c | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-200` | Additional permissions cannot override Guest restrictions or the prohibition on downloading Secret Meeting documents. | UC-28b, UC-32a, UC-32c | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-201` | A Guest receives no system Role. | UC-04, UC-28a | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-202` | A Guest cannot receive delegated authority. | UC-28b | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-203` | A Guest cannot moderate a Meeting room. | UC-41 | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-204` | A Guest cannot use a personal document library. | UC-34, UC-36, UC-71, UC-72 | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-205` | Meeting documents have no separate sharing option for Guests. | UC-32a, UC-32c, UC-33a | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| `BR-206` | Uploaded documents have no versions. | UC-32a, UC-32c, UC-33a, UC-33c, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-208` | Changing document content requires uploading a new file and replacing the old Meeting document. | UC-33a, UC-33c, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-209` | A Meeting document must come from the actor's own personal library or computer. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-210` | A file uploaded directly into a Meeting is also added to the uploader's personal library. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-211` | A removed document is excluded from the Meeting's current document list. | UC-32a, UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-212` | Removing a Meeting document preserves its comments as history. | UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-213` | A file is deleted by periodic cleanup when no personal-library document or active Meeting document uses it. | UC-33c, UC-71a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-214` | An uploaded document must not exceed 50 MB. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-215` | An uploaded document's type is determined from its actual content. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-216` | Uploaded files retain their original format and content. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-219` | A personal-library folder belongs to one user. | UC-34, UC-72 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-220` | A folder name must not be empty. | UC-72a, UC-72b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-221` | Folder names need not be unique. | UC-72a, UC-72b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-222` | A folder cannot be moved into itself or one of its descendants. | UC-72c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-223` | Only an empty folder can be deleted. | UC-72d | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-224` | A user may rename documents in their own personal library. | UC-71b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-225` | A user may move personal-library documents only between their own folders or the library root. | UC-71c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-226` | The first direct upload creates a personal-library folder for that user and Meeting. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-227` | Later uploads to the same Meeting reuse its existing personal-library folder, even after it is renamed or moved. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-228` | If the Meeting's personal-library folder was deleted, the next direct upload creates a new folder at the library root. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-229` | Separate uploads are different documents, even when their names or contents match. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-231` | Removing a Meeting document does not remove it from personal libraries or other Meetings. | UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-232` | File cleanup preserves document metadata and comments, but does not retain the file content for historical reading. | UC-33c, UC-71a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-233` | A current Participant or Guest may read Meeting documents only after the Meeting is published. | UC-32a, UC-32c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-234` | Meeting Approver status alone grants no document-reading access. | UC-32a, UC-32c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| `BR-235` | Declining a Meeting invitation does not revoke document-reading access. | UC-32a, UC-32c | ADR-0006 — cross-review 14/10/2026 | Document / Guest access |
| `BR-236` | All planning fields of a Pending Agenda Item may be edited. | UC-23b | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-237` | Only Pending Agenda Items may be deleted. | UC-23c | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-238` | Only Pending Agenda Items may be reordered. | UC-23d | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-239` | Deleting an Agenda Item preserves the positions of the remaining Items. | UC-23c | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-240` | Reordering exchanges only the existing positions of Pending Agenda Items. | UC-23d | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-241` | Agenda planning changes preserve execution status, actual timestamps and the Meeting lifecycle. | UC-23b, UC-23c, UC-23d | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| `BR-242` | A user may access only their own personal-library documents and folders. | UC-34, UC-36, UC-71a–UC-71c, UC-72a–UC-72d | ADR-0006 / ADR-0018 | Document Management |
| `BR-245` | Moving a personal-library document does not change its Meeting attachments. | UC-71c | ADR-0018 | Document Management |
| `BR-246` | A comment must contain non-whitespace characters. | UC-35 | Report 3 screen 7.5 — required comment text | Document Management |
| `BR-248` | A DOCX comment has no page reference. | UC-35 | ADR-0018 | Document Management |
| `BR-249` | Anyone who can view a Meeting document may comment on it. | UC-35 | PO decision — no separate commenting permission | Document Management |
| `BR-250` | Each comment records its author and posting time. | UC-35 | Report 3 screen 7.5 | Document Management |
| `BR-251` | Personal-library usage details show only Meetings the user may access. | UC-34 | Existing resource confidentiality rule | Document Management |
| `BR-252` | Only the current Chair may create or configure a Draft Vote while its Meeting is Approved, Published or InProgress. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-253` | Only the current Chair may view Draft Votes in the Meeting. | UC-52a, UC-52b, UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-254` | The content, options, ballot type and selected roles of an Open or Closed Vote cannot be edited. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-255` | Vote content must contain non-whitespace characters. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-256` | A Vote must have at least two distinct, non-empty options. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-257` | The Chair selects one or more eligible meeting roles from Chair, Secretary, Member and Guest; all four roles are selected by default. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-258` | Each eligible voter may cast at most one ballot in a Vote. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-259` | A secret Vote records participation without linking a voter to a chosen option; only aggregate results may be disclosed. | UC-52b, UC-55, UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-260` | The status view of an Open Vote shows turnout, without disclosing individual choices or results per option. | UC-52b | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-261` | Closing a Vote tallies all recorded ballots per option; each percentage uses ballots cast as its denominator. With no ballots, option counts are zero and percentages are not shown. | UC-52b, UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-262` | A recorded Vote conclusion and its note cannot be changed or replaced. | UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-263` | The Chair determines a Vote conclusion; the system does not infer it from the tally. | UC-56, UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-264` | Meeting Minutes include the Vote tally and the conclusion recorded by the Chair. | UC-57, UC-59 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-265` | A recorded ballot cannot be changed or withdrawn. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-266` | A Meeting may have at most one Open Vote at a time. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-267` | A Vote cannot be opened if no eligible voters are available. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-268` | Changing a voter's meeting role after opening does not change the fixed eligible-voter list or count. | UC-54, UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-269` | Loss of Meeting access prevents new ballots without deleting previously recorded ballots. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-270` | The current Chair may close and tally an existing Open Vote while its Meeting is InProgress or Ended. | UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-271` | A ballot selects exactly one option belonging to its Vote. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-272` | Each Vote uses either a Public or Secret ballot type. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-273` | After a Public Vote closes, its result shows each eligible voter's recorded choice or that the voter did not vote. | UC-52b | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| `BR-274` | The current Chair may access Vote draft preparation in an Approved Meeting; this does not grant access to other restricted Meeting records. | UC-52a, UC-52b, UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |

## BR đã ngừng dùng

Các mã dưới đây không còn trong Report 3 — phần lớn thuộc Meeting Session, uỷ quyền tham dự (delegation), biểu quyết đặc biệt (time-boxed, quorum, Resolution) và các quy tắc đã gộp vào BR khác sau quyết định PO 09/10/2026 (xem [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md), [ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)). Giữ mã để truy vết, **không tái cấp**:

`BR-54`, `BR-55`, `BR-56`, `BR-57`, `BR-58`, `BR-59`, `BR-60`, `BR-61`, `BR-63`, `BR-77`, `BR-89`, `BR-98`, `BR-99`, `BR-107`, `BR-108`, `BR-109`, `BR-110`, `BR-111`, `BR-112`, `BR-113`, `BR-114`, `BR-128`, `BR-133`, `BR-134`, `BR-135`, `BR-136`, `BR-137`, `BR-138`, `BR-139`, `BR-140`, `BR-141`, `BR-142`, `BR-144`, `BR-145`, `BR-146`, `BR-147`


`BR-179`: ngừng dùng ngày 13/10/2026. Không bắt điểm danh đủ trước khi kết thúc cuộc họp; không tái cấp. Xem [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md).

`BR-243`: ngừng dùng theo yêu cầu PO; bỏ quy tắc tên tài liệu không trống khỏi đặc tả. Không tái cấp mã.

`BR-244`, `BR-247`: ngừng dùng theo yêu cầu PO; không tái cấp mã.

`BR-207`: chuyển về ADR-0018/design. `BR-217`, `BR-218`: chuyển thành UI behavior. Các mã này ngừng dùng, không tái cấp.

`BR-230`: ngừng dùng theo yêu cầu PO; không tái cấp mã.
