# Use Case Specifications · TrustCabinet

> **GENERATED — DO NOT EDIT.** Bản trích từ Report 3 §II. Sửa DOCX trong
> [`docs/08-reports/source/`](../08-reports/source/README.md) rồi chạy
> `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
> (hoặc `python scripts/sync_requirement_extracts.py`). Sửa tay file này sẽ mất ở lần sync sau.
> Chi tiết: [08-reports/README.md](../08-reports/README.md).

Đặc tả Use Case của **TrustCabinet**, đồng bộ nguyên văn mục *II. Use Case Specifications* của [Report 3 — SRS](../08-reports/Report-3_Software_Requirement_Specification.md) (bản DOCX cập nhật 24/10/2026, gồm ADR-0020 cho tài liệu cuộc họp và đợt BA Final Review 24/10/2026). Nội dung đặc tả giữ tiếng Anh như Report 3.

- **Phạm vi:** 15 Feature (FE-01 → FE-15), Use Case UC-01 → UC-56 và UC-28, UC-80 → UC-81 (UC-75 thuộc SCRUM-33, chưa vào Report 3; UC-32b đã bỏ; UC-36b được tái cấp cho gắn tài liệu vào mục nghị sự theo ADR-0020, bổ sung UC-36d để bỏ gắn một mục; UC-34 → UC-56 thêm sau baseline 72 UC; UC-28 thêm ngày 19/11/2026; UC-80, UC-81 của FE-15 (trước ngày 25/11/2026 là FE-16) thêm ngày 07/11/2026). UC gom nhiều thao tác nhỏ được tách thành UC con có hậu tố chữ (ví dụ `UC-06a`, `UC-06b`, `UC-06c`).
- **Đồng bộ:** file này là bản trích của Report 3 §II; sinh lại bằng pipeline ở banner trên (thêm
  `--check` trên `sync_requirement_extracts.py` để chỉ xem lệch), **không sửa tay** bảng UC.
- **Đổi số:** danh sách UC đã được gom và đánh số lại sau quyết định PO 09/10/2026 (bỏ Meeting Session, biểu quyết chỉ trong cuộc họp). Bảng đổi số cũ → mới nằm ở [Master Use Case Inventory §5](00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
- **Business Rules:** [06-business-rules.md](06-business-rules.md). **Mã thông báo:** chuẩn là [danh mục mã thông báo](../03-api/00-danh-muc-ma-thong-bao.md) — khi mã `MSGnn` trong đặc tả lệch với danh mục, danh mục thắng.
- UC ghi *Placeholder* là UC đã có trong phạm vi nhưng Report 3 chưa đặc tả chi tiết; BE viết đặc tả cơ bản khi nhận ticket (xem [DEVELOPMENT_WORKFLOW](../DEVELOPMENT_WORKFLOW.md)).
- Quy ước viết UC Specification ở [README](README.md#2-quy-ước-viết-use-case-specification-business-oriented).

## Mục lục

### FE-01 — Authentication & Account Security

- [UC-01 — Sign in with Email and Password](#uc-01--sign-in-with-email-and-password)
- [UC-02 — Sign in with Google](#uc-02--sign-in-with-google)
- [UC-03 — Sign Out](#uc-03--sign-out)
- [UC-04 — Access Meeting via Guest Invitation](#uc-04--access-meeting-via-guest-invitation)

### FE-02 — User & Profile Management

- [UC-05 — View User Accounts](#uc-05--view-user-accounts)
- [UC-06 — Manage User Accounts](#uc-06--manage-user-accounts)
  - [UC-06a — Create User Account](#uc-06a--create-user-account)
  - [UC-06b — Update User Account](#uc-06b--update-user-account)
  - [UC-06c — Deactivate / Reactivate User Account](#uc-06c--deactivate--reactivate-user-account)
- [UC-07 — Reset User Password](#uc-07--reset-user-password)
- [UC-08 — Assign System Role to User](#uc-08--assign-system-role-to-user)

### FE-03 — Role-Based Access Control

- [UC-09 — View Role & Permission Matrix](#uc-09--view-role--permission-matrix)
- [UC-10 — Configure Role Permissions](#uc-10--configure-role-permissions)
- [UC-11 — Manage Roles](#uc-11--manage-roles)
  - [UC-11a — Create Role](#uc-11a--create-role)
  - [UC-11b — Update Role](#uc-11b--update-role)
  - [UC-11c — Delete Role](#uc-11c--delete-role)
- [UC-12 — View Audit Log](#uc-12--view-audit-log)

### FE-04 — Meeting Management

- [UC-13 — Create Meeting](#uc-13--create-meeting)
- [UC-14 — Update Meeting](#uc-14--update-meeting)
- [UC-15 — View Meeting Details](#uc-15--view-meeting-details)
- [UC-16 — View Meeting List](#uc-16--view-meeting-list)
- [UC-17 — Submit Meeting for Approval](#uc-17--submit-meeting-for-approval)
- [UC-18 — Review Meeting Approval](#uc-18--review-meeting-approval)
- [UC-19 — Publish Meeting](#uc-19--publish-meeting)
- [UC-20 — Start Meeting](#uc-20--start-meeting)
- [UC-21 — End Meeting](#uc-21--end-meeting)
- [UC-22 — Cancel Meeting](#uc-22--cancel-meeting)

### FE-05 — Agenda Management

- [UC-23 — Manage Agenda Items](#uc-23--manage-agenda-items)
  - [UC-23a — Create Agenda Item](#uc-23a--create-agenda-item)
  - [UC-23b — Update Agenda Item](#uc-23b--update-agenda-item)
  - [UC-23c — Delete Agenda Item](#uc-23c--delete-agenda-item)
  - [UC-23d — Reorder Agenda Items](#uc-23d--reorder-agenda-items)
- [UC-24 — Set Current Agenda Item](#uc-24--set-current-agenda-item)
- [UC-25 — Complete Agenda Item](#uc-25--complete-agenda-item)
- [UC-26 — View Meeting Agenda](#uc-26--view-meeting-agenda)
- [UC-27 — Record Agenda Item Conclusion](#uc-27--record-agenda-item-conclusion)
- [UC-28 — Take Meeting Notes](#uc-28--take-meeting-notes)

### FE-06 — Meeting Participant Management

- [UC-29 — View Meeting Participants](#uc-29--view-meeting-participants)
- [UC-30 — Manage Meeting Participants](#uc-30--manage-meeting-participants)
  - [UC-30a — Add Meeting Participant](#uc-30a--add-meeting-participant)
  - [UC-30b — Manage Meeting Role](#uc-30b--manage-meeting-role)
  - [UC-30c — Remove Meeting Participant](#uc-30c--remove-meeting-participant)
- [UC-31 — Respond to Meeting Invitation](#uc-31--respond-to-meeting-invitation)
- [UC-32 — View Participant Response Status](#uc-32--view-participant-response-status)
- [UC-33 — Record Attendance](#uc-33--record-attendance)
- [UC-34 — Hand Over Chair to Another Participant](#uc-34--hand-over-chair-to-another-participant)

### FE-07 — Personal Document Library & Meeting Document Management

- [UC-35 — View Meeting Documents](#uc-35--view-meeting-documents)
  - [UC-35a — View Meeting Documents](#uc-35a--view-meeting-documents)
  - [UC-35b — Preview Meeting Document](#uc-35b--preview-meeting-document)
- [UC-36 — Manage Meeting Documents](#uc-36--manage-meeting-documents)
  - [UC-36a — Add Document to Meeting](#uc-36a--add-document-to-meeting)
  - [UC-36b — Attach Documents to Agenda Item](#uc-36b--attach-documents-to-agenda-item)
  - [UC-36c — Remove Document from Meeting](#uc-36c--remove-document-from-meeting)
  - [UC-36d — Detach Document from Agenda Item](#uc-36d--detach-document-from-agenda-item)
- [UC-37 — View Personal Document Library](#uc-37--view-personal-document-library)
  - [UC-37a — View a Deactivated Account’s Personal Library](#uc-37a--view-a-deactivated-accounts-personal-library)
- [UC-38 — Comment on Meeting Document](#uc-38--comment-on-meeting-document)
- [UC-39 — Upload Document to Personal Library](#uc-39--upload-document-to-personal-library)
- [UC-40 — Manage Personal Documents](#uc-40--manage-personal-documents)
  - [UC-40a — Delete Document from Personal Library](#uc-40a--delete-document-from-personal-library)
  - [UC-40b — Rename Document](#uc-40b--rename-document)
  - [UC-40c — Move Document to Folder](#uc-40c--move-document-to-folder)
- [UC-41 — Manage Library Folders](#uc-41--manage-library-folders)
  - [UC-41a — Create Folder](#uc-41a--create-folder)
  - [UC-41b — Rename Folder](#uc-41b--rename-folder)
  - [UC-41c — Move Folder](#uc-41c--move-folder)
  - [UC-41d — Delete Folder](#uc-41d--delete-folder)
- [UC-42 — Confirm Document Read](#uc-42--confirm-document-read)

### FE-08 — Online Meeting Room Management

- [UC-43 — Join Meeting Room](#uc-43--join-meeting-room)
- [UC-44 — Leave Meeting Room](#uc-44--leave-meeting-room)
- [UC-45 — Manage Meeting Room Controls](#uc-45--manage-meeting-room-controls)
  - [UC-45a — Manage Microphone](#uc-45a--manage-microphone)
  - [UC-45b — Manage Camera](#uc-45b--manage-camera)
  - [UC-45c — Manage Screen Sharing](#uc-45c--manage-screen-sharing)
  - [UC-45d — Choose Audio and Video Devices](#uc-45d--choose-audio-and-video-devices)
- [UC-46 — View Meeting Room Participants](#uc-46--view-meeting-room-participants)
- [UC-47 — Moderate Meeting Room](#uc-47--moderate-meeting-room)
- [UC-48 — Use In-room Chat](#uc-48--use-in-room-chat)
- [UC-49 — Keep Meeting in a Floating Window](#uc-49--keep-meeting-in-a-floating-window)
- [UC-50 — Register to Speak](#uc-50--register-to-speak)

### FE-09 — Meeting Recording

- [UC-51 — View & Play Meeting Recordings](#uc-51--view--play-meeting-recordings)
  - [UC-51a — View Meeting Recordings](#uc-51a--view-meeting-recordings)
  - [UC-51b — Play Meeting Recording](#uc-51b--play-meeting-recording)
  - [UC-51c — Download Meeting Recording](#uc-51c--download-meeting-recording)
- [UC-52 — Record Room Audio](#uc-52--record-room-audio)
  - [UC-52a — Turn Room Microphone On/Off](#uc-52a--turn-room-microphone-onoff)
  - [UC-52c — Upload Meeting Audio File](#uc-52c--upload-meeting-audio-file)
  - [UC-52d — Delete Recording Take](#uc-52d--delete-recording-take)
  - [UC-52e — Erase Meeting Recording](#uc-52e--erase-meeting-recording)
- [UC-53 — Manage My Recorded Speech](#uc-53--manage-my-recorded-speech)

### FE-10 — Transcription, Speaker Identification & Meeting Summary

- [UC-54 — View Meeting Transcript](#uc-54--view-meeting-transcript)
  - [UC-54a — View Recording Transcript](#uc-54a--view-recording-transcript)
  - [UC-54b — Export Meeting Transcript](#uc-54b--export-meeting-transcript)
- [UC-55 — Edit Transcript & Speaker Attribution](#uc-55--edit-transcript--speaker-attribution)
  - [UC-55a — Edit Transcript Text](#uc-55a--edit-transcript-text)
  - [UC-55b — Assign Speaker](#uc-55b--assign-speaker)
  - [UC-55c — Exclude or Restore a Sentence](#uc-55c--exclude-or-restore-a-sentence)
  - [UC-55d — Delete a Sentence](#uc-55d--delete-a-sentence)
- [UC-56 — Manage My Voice ID](#uc-56--manage-my-voice-id)
  - [UC-56a — View My Voice ID](#uc-56a--view-my-voice-id)
  - [UC-56b — Give Voice ID Consent](#uc-56b--give-voice-id-consent)
  - [UC-56c — Record Voice Sample](#uc-56c--record-voice-sample)
  - [UC-56d — Play Voice Sample](#uc-56d--play-voice-sample)
  - [UC-56e — Delete Voice Sample](#uc-56e--delete-voice-sample)
  - [UC-56f — Withdraw Voice ID Consent](#uc-56f--withdraw-voice-id-consent)
- [UC-57 — Collect Voice Samples for Attendees](#uc-57--collect-voice-samples-for-attendees)
  - [UC-57a — View Voice Collection](#uc-57a--view-voice-collection)
  - [UC-57b — Record Voice Sample for Attendee](#uc-57b--record-voice-sample-for-attendee)
  - [UC-57c — Play Collected Voice Sample](#uc-57c--play-collected-voice-sample)
  - [UC-57d — Delete Collected Voice Sample](#uc-57d--delete-collected-voice-sample)
- [UC-58 — Generate / Regenerate Meeting Summary](#uc-58--generate--regenerate-meeting-summary)
- [UC-59 — View Meeting Summary](#uc-59--view-meeting-summary)
- [UC-60 — Edit Meeting Summary](#uc-60--edit-meeting-summary)
- [UC-61 — Approve Meeting Summary](#uc-61--approve-meeting-summary)

### FE-11 — Voting Management

- [UC-62 — View Meeting Votes](#uc-62--view-meeting-votes)
  - [UC-62a — View Meeting Votes](#uc-62a--view-meeting-votes)
  - [UC-62b — View Vote Status](#uc-62b--view-vote-status)
- [UC-63 — Create / Configure Vote](#uc-63--create--configure-vote)
- [UC-64 — Open Vote](#uc-64--open-vote)
- [UC-65 — Cast Vote](#uc-65--cast-vote)
- [UC-66 — Close Vote](#uc-66--close-vote)
- [UC-67 — Record Vote Conclusion](#uc-67--record-vote-conclusion)
- [UC-68 — Manage Vote Documents](#uc-68--manage-vote-documents)
  - [UC-68a — Attach Documents to Vote](#uc-68a--attach-documents-to-vote)
  - [UC-68b — Detach Document from Vote](#uc-68b--detach-document-from-vote)

### FE-12 — Meeting Minutes & Approval

- [UC-69 — View Meeting Minutes](#uc-69--view-meeting-minutes)
- [UC-70 — Generate Meeting Minutes](#uc-70--generate-meeting-minutes)
- [UC-71 — Edit Meeting Minutes](#uc-71--edit-meeting-minutes)
- [UC-72 — Submit Meeting Minutes for Approval](#uc-72--submit-meeting-minutes-for-approval)
- [UC-73 — Approve and Issue Meeting Minutes](#uc-73--approve-and-issue-meeting-minutes)
- [UC-74 — Return Meeting Minutes for Revision](#uc-74--return-meeting-minutes-for-revision)
- [UC-75 — Correct Issued Meeting Minutes](#uc-75--correct-issued-meeting-minutes)

### FE-13 — Email & In-App Notification

- [UC-76 — View Notifications](#uc-76--view-notifications)
- [UC-77 — Mark Notifications as Read](#uc-77--mark-notifications-as-read)
  - [UC-77a — Mark Notification as Read](#uc-77a--mark-notification-as-read)
  - [UC-77b — Mark All Notifications as Read](#uc-77b--mark-all-notifications-as-read)
- [UC-78 — Open Notification Target](#uc-78--open-notification-target)

### FE-14 — Reporting & Statistics

- [UC-79 — View & Export Reports](#uc-79--view--export-reports)
  - [UC-79a — View Reports & Statistics](#uc-79a--view-reports--statistics)
  - [UC-79b — Export Report](#uc-79b--export-report)

### FE-15 — Working Calendar & Meeting Time Search

- [UC-80 — Manage Calendar Sharing](#uc-80--manage-calendar-sharing)
  - [UC-80a — Request Calendar Share](#uc-80a--request-calendar-share)
  - [UC-80b — Share Calendar Directly](#uc-80b--share-calendar-directly)
  - [UC-80c — Respond to Calendar Share](#uc-80c--respond-to-calendar-share)
  - [UC-80d — Change Calendar Share](#uc-80d--change-calendar-share)
  - [UC-80e — Withdraw or End Calendar Share](#uc-80e--withdraw-or-end-calendar-share)
  - [UC-80f — View Calendar Shares](#uc-80f--view-calendar-shares)
- [UC-81 — Find Meeting Time](#uc-81--find-meeting-time)
  - [UC-81a — Find Meeting Time Slots](#uc-81a--find-meeting-time-slots)
  - [UC-81b — Create Draft Meeting from Slot](#uc-81b--create-draft-meeting-from-slot)
  - [UC-81c — View Shared Calendar](#uc-81c--view-shared-calendar)

---

## FE-01 — Authentication & Account Security

### UC-01 – Sign in with Email and Password

Sequence Diagram: [UC01_SignIn.txt](../06-sequencediagram/auth/UC01_SignIn.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | As an internal system user, I want to sign in using my email and password so that I can securely access the system through my internal account. |
| **Preconditions** | ● PRE-1: The user has an existing internal account; any role may sign in, including a role with no capabilities. (BR-002)<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |
| **Postconditions** | ● POST-1: The user is authenticated.<br>● POST-2: The system identifies the corresponding internal user.<br>● POST-3: The user lands on the calendar, or on the page they opened from a link before signing in.<br>● POST-4: The sign-in, successful or failed for an existing account, is recorded in the audit log. (BR-009) |
| **Normal Flow** | Sign in with Email and Password<br>1. The user enters their email and password on the login page. An empty email, a malformed email or an empty password is pointed out beside its field before the sign-in is sent.<br>2. The user clicks “Sign In”.<br>3. The system validates the provided credentials.<br>4. The system records the successful sign-in, and the method used, in the audit log. (BR-009)<br>5. The system signs the user in and opens the landing page, the calendar, unless the sign-in continued a page opened while signed out. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – System cannot authenticate the user<br>1. The entered email or password is invalid. (BR-004)<br>2. If the email belongs to an existing account, the system records the failed sign-in against that account as the actor, with the reason and without the password; an email that matches no account is not recorded. (BR-009)<br>3. The system displays MSG01 – “Invalid email or password.”<br>4. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – The internal account has been deactivated<br>1. The corresponding internal account is inactive or has been deactivated. (BR-001)<br>2. The system records the failed sign-in against that account as the actor, because the account is deactivated. (BR-009)<br>3. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”<br>4. The use case ends. |

### UC-02 – Sign in with Google

Sequence Diagram: [UC02_SignInWithGoogle.txt](../06-sequencediagram/auth/UC02_SignInWithGoogle.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | OAuth Provider |
| **Description** | As an internal system user, I want to sign in using my Google account so that I can securely and quickly access the system through my existing internal account. |
| **Preconditions** | ● PRE-1: The user has an existing internal account; any role may sign in, including a role with no capabilities. (BR-002)<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |
| **Postconditions** | ● POST-1: User is authenticated.<br>● POST-2: System identifies the corresponding internal user.<br>● POST-3: The user lands on the calendar, on the same terms as UC-01 POST-3.<br>● POST-4: The sign-in with Google, successful or failed for an existing account, is recorded in the audit log. (BR-009) |
| **Normal Flow** | Sign in with Google<br>1. User selects “Continue with Google” on the login page.<br>2. User signs in using their Google account.<br>3. System verifies that the Google account is associated with an authorized internal user.<br>4. The system records the successful sign-in with Google in the audit log. (BR-009)<br>5. System signs the user in, grants access according to the user's assigned role and opens the calendar, unless the sign-in continued a page opened while signed out. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – User can’t authenticate with Google<br>1. The user fails to authenticate with Google or cancels the Google sign-in process. (BR-002)<br>2. The system displays MSG03 – “Google sign-in was unsuccessful.”<br>3. The use case ends.<br>EX-2 – System can’t verify the Google account<br>1. The system cannot verify the identity information returned by Google. (BR-003)<br>2. The system displays MSG03 – “Google sign-in was unsuccessful.”<br>3. The use case ends.<br>EX-3 – Google account is not linked to an internal account<br>1. The authenticated Google account is not associated with any existing internal account. (BR-003)<br>2. The system displays MSG05 – “This Google account has not been granted access to the system.”<br>3. The use case ends.<br>EX-4 – The internal account has been deactivated<br>1. The internal account associated with the Google account is inactive or has been deactivated. (BR-001)<br>2. The system records the failed sign-in against that account as the actor, because the account is deactivated. (BR-009)<br>3. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”<br>4. The use case ends. |

### UC-03 – Sign Out

Sequence Diagram: [UC03_SignOut.txt](../06-sequencediagram/auth/UC03_SignOut.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | As an authenticated user, I want to sign out of the system so that I can securely end my current access to the system. |
| **Preconditions** | ● PRE-1: The user is currently signed in to the system.<br>● PRE-2: The user is currently accessing an authenticated area of the system. |
| **Postconditions** | ● POST-1: User is signed out of the system.<br>● POST-2: The user's current authentication state is invalidated.<br>● POST-3: User is redirected to the login page.<br>● POST-4: Protected system functions are no longer accessible until the user signs in again. |
| **Normal Flow** | Sign Out<br>1. User selects “Sign Out”.<br>2. System displays a confirmation message.<br>3. User confirms the sign-out action.<br>4. System signs the user out.<br>5. System redirects the user to the login page. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – System can’t complete the sign-out action<br>1. The system cannot complete the sign-out operation.<br>2. The system displays MSG06 – “Unable to sign out. Please try again.”<br>3. The use case ends. |

### UC-04 – Access Meeting via Guest Invitation

Sequence Diagram: [Access Meeting via Guest Invitation](../06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt) (một luồng đầu-cuối gồm cấp mã, xác minh email và vào cuộc họp). Source và ảnh PNG: [auth/README](../06-sequencediagram/auth/README.md).



| Mục | Nội dung |
|---|---|
| **Primary Actors** | Guest |
| **Secondary Actors** | Resend Email |
| **Description** | Allows an invited Guest to verify control of the invited email and enter self-declared display information for the invited Meeting. |
| **Preconditions** | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked, and the invited Meeting is still Published or InProgress. |
| **Postconditions** | ● POST-1: Verified Guest access is limited to the invited Meeting and ends 12 hours after verification, or earlier when the Guest signs out, the invitation is revoked or the Meeting ends. Signing out ends only that session. (BR-052, BR-053, BR-054)<br>● POST-2: The Guest may use Member participation functions. Email control is verified; display name and job title are self-declared.<br>● POST-3: The Guest's successful verification is recorded in the audit log. (BR-009) |
| **Normal Flow** | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system checks the invitation and sends a one-time code to the invited email. (BR-053, BR-054)<br>3. The Guest enters the code.<br>4. The system validates the code and the invitation, which verifies control of the invited email. (BR-053, BR-054)<br>5. The system records the Guest's successful verification in the audit log. (BR-009)<br>6. The Guest enters a display name, required, and a job title, optional. The system labels them as self-declared; they are declared once for the invitation and cannot be changed afterwards.<br>7. The system displays MSG376 – “Display information saved.”<br>8. The system opens Meeting-scoped Guest access and displays the invited Meeting. (BR-052) |
| **Alternative Flows** | AF-1 – Guest already verified<br>1. At Step 1, the Guest still has an active verified session for this invitation.<br>2. The system opens the invited Meeting without asking for another code.<br>3. The use case ends.<br>AF-2 – Guest asks for a new code<br>1. At Step 3, the Guest asks for a new code.<br>2. The system sends a new code to the invited email; the previous code can no longer be used. (BR-053, BR-054)<br>3. The system displays MSG375 – “A verification code was sent to the invited email.”<br>4. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Guest signs out<br>1. After Step 8, the Guest signs out from the Meeting page.<br>2. The system ends this session only and removes the Meeting data from the screen. A session on another device keeps working. (BR-054)<br>3. While the Meeting is still Published or In Progress, the Guest may verify again with a new code; the flow then resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – Invitation is no longer valid<br>1. At Step 2 or Step 4, the invitation is missing or revoked, or the Meeting is no longer Published or In Progress. An invitation has no expiry date of its own.<br>2. The system denies access without saying which cause applies and displays MSG371 – “The guest invitation is no longer valid.” (BR-052, BR-054)<br>3. The use case ends.<br>EX-2 – Code is not accepted<br>1. At Step 4, the code is incorrect, expired or already used.<br>2. No session is opened. The system displays MSG126 – “Unable to verify the guest invitation. Check the code and try again.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Too many incorrect codes<br>1. At Step 4, the code has been entered incorrectly 5 times. (BR-053)<br>2. The code can no longer be used and no session is opened. The system displays MSG372 – “Too many incorrect codes. Request a new code.”<br>3. The flow resumes at Step 1 of AF-2.<br>EX-4 – New code requested too soon<br>1. At Step 1 of AF-2, the previous code was sent less than 1 minute ago.<br>2. No code is sent. The system displays MSG373 – “Wait before requesting another code.” and shows how long to wait.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-5 – Too many codes requested<br>1. At Step 1 of AF-2, more than 20 codes were sent for this invitation within 24 hours.<br>2. No code is sent. The system displays MSG379 – “Too many codes requested. Try again later or contact the Meeting Secretary.”<br>3. The use case ends.<br>EX-6 – Display information is invalid<br>1. At Step 6, the display name is missing or longer than 200 characters, the job title is longer than 200 characters, or either contains control characters.<br>2. Nothing is saved. The system displays MSG377 – “Invalid display information.”<br>3. The flow resumes at Step 6 of the Normal Flow.<br>EX-7 – Display information already declared<br>1. At Step 6, the invitation already has a display name and the Guest submits a different display name or job title.<br>2. Nothing is changed. The system displays MSG381 – “The display name was already declared.”<br>3. The Guest continues with the information declared earlier; the flow resumes at Step 8 of the Normal Flow.<br>EX-8 – Guest session has ended<br>1. After Step 8, the 12-hour session expires, the invitation is revoked or the Meeting is no longer open. (BR-054)<br>2. The system removes the Meeting data from the screen and displays MSG378 – “The guest session has ended.”<br>3. The use case ends. |

## FE-02 — User & Profile Management

### UC-05 – View User Accounts

Sequence Diagram: [UC05_ViewUserAccount.txt](../06-sequencediagram/user-management/UC05_ViewUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to view, search, filter, and browse user accounts in the system. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to view user accounts. |
| **Postconditions** | ● POST-1: The user account list is displayed.<br>● POST-2: No user account information is modified. |
| **Normal Flow** | View User Accounts<br>1. The System Admin selects User Accounts from the navigation menu.<br>2. The system retrieves the user accounts.<br>3. The system sorts the user accounts according to the default sorting rule.<br>4. The system displays the user account list showing Full Name, Username/Email, System Role, and Account Status, one account per row.<br>5. The System Admin browses the user account list page by page. |
| **Alternative Flows** | AF-1 – Search user accounts<br>1. At Step 4 of the Normal Flow, the System Admin enters a Name, Username, or Email into the search field.<br>2. The system searches for user accounts matching the entered criteria, ignoring letter case and Vietnamese diacritics.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Filter user accounts<br>1. At Step 4 of the Normal Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>AF-3 – No user accounts match the current criteria<br>1. The applied search or filter criteria produce no matching user accounts.<br>2. The system displays an empty list and a no-results message.<br>3. The System Admin may clear or adjust the search/filter parameters.<br>4. The flow resumes at Step 5 of the Normal Flow. |
| **Exception Flows** | N/A |

### UC-06 – Manage User Accounts

_UC gom — đặc tả nằm ở các UC con UC-06a, UC-06b… bên dưới._

#### UC-06a – Create User Account

Sequence Diagram: [UC06a_CreateUserAccount.txt](../06-sequencediagram/user-management/UC06a_CreateUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to create a new user account by providing the required account information. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to create user accounts. |
| **Postconditions** | ● POST-1: A new user account is created successfully.<br>● POST-2: The new user account appears in User Management.<br>● POST-3: The account creation is recorded in the audit log. |
| **Normal Flow** | Create User Account<br>1. The System Admin selects Create User from the User Accounts screen.<br>2. The system displays the Create User form.<br>3. The System Admin enters the required user information.<br>4. The System Admin submits the user information.<br>5. The system validates the submitted information, including required fields and Email validity and uniqueness (BR-005).<br>6. The system creates the new user account with Active status (BR-006).<br>7. The system records the account creation in the audit log (BR-009).<br>8. The system displays MSG07 – "User account created successfully." and returns to the User Accounts list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid user information<br>1. At Step 5 of the Normal Flow, the system determines that one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." with a message for each invalid field.<br>3. The System Admin corrects the information and resubmits the form.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-2 – Username or Email already exists<br>1. At Step 5 of the Normal Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use." beside the Email field.<br>3. The System Admin changes the conflicting information and resubmits the form.<br>4. The flow resumes at Step 5 of the Normal Flow. |

#### UC-06b – Update User Account

Sequence Diagram: [UC06b_UpdateUserAccount.txt](../06-sequencediagram/user-management/UC06b_UpdateUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to update the Full Name, Email and System Role of an existing user account in one save. Changing the System Role here gives the same result, under the same rules, as UC-08. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to update the selected user account.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: The selected user account information is updated successfully.<br>● POST-2: The update action is recorded in the audit log. |
| **Normal Flow** | Update User Account<br>1. The System Admin selects a user account from the User Accounts list and selects Edit Account.<br>2. The system displays the current user account information.<br>3. The System Admin modifies the editable information: Full Name, Email and System Role.<br>4. The System Admin submits the updated information.<br>5. The system validates the submitted information, including required fields, data format and Email uniqueness, and, when the System Role changes, the rules of UC-08, so that at least one active account keeps the User administration permission. (BR-005)<br>6. The system saves the Full Name, Email and System Role together. A new role takes effect from the user's next action without ending the session, and the user is notified (BR-008); a changed Email ends every active session of the account instead, and no notification is sent.<br>7. The system records the update in the audit log (BR-009).<br>8. The system displays MSG10 – "User account updated successfully." and returns to the User Accounts list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid user information<br>1. At Step 5 of the Normal Flow, one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." with a message for each invalid field.<br>3. The System Admin corrects the information and resubmits it.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-2 – Email already in use<br>1. At Step 5 of the Normal Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use." beside the Email field.<br>3. The System Admin changes the Email and resubmits the information.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-3 – Change would remove the last active administrator<br>1. At Step 5 of the Normal Flow, the system determines that, after the role change, no active account would hold the User administration permission.<br>2. Nothing is saved; the user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends. |

#### UC-06c – Deactivate / Reactivate User Account

Sequence Diagram: [UC06c_DeactivateReactivateUserAccount.txt](../06-sequencediagram/user-management/UC06c_DeactivateReactivateUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to deactivate or reactivate a user account while preserving the user's historical records. Deactivation ends the user's Voice ID and the calendar shares of the account; reactivation restores neither. (BR-117, BR-142, BR-143) |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage user account status.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: The user account status is changed to Inactive when deactivated or Active when reactivated.<br>● POST-2: A deactivated user can no longer access authenticated system functions. (BR-001)<br>● POST-3: The status change is recorded in the audit log. (BR-009)<br>● POST-4: Deactivation ends the user's Voice ID and stops the Effective calendar shares of the account; reactivating the account restores neither. (BR-117, BR-142, BR-143) |
| **Normal Flow** | Deactivate / Reactivate User Account<br>1. The System Admin selects a user account from the User Accounts list.<br>2. The System Admin selects Deactivate Account or Reactivate Account.<br>3. The system requests confirmation of the account status change.<br>4. The System Admin confirms the action.<br>5. The system verifies that the requested status change is allowed. (BR-001)<br>6. The system updates the user account status and records the change in the audit log. (BR-009)<br>7. If the account is deactivated, the user is signed out everywhere, and the deactivation ends the user's Voice ID and the calendar shares of the account. (BR-001, BR-117, BR-142, BR-143)<br>8. The system displays MSG11 – “User account status changed successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Status change is not allowed<br>1. At Step 5 of the Normal Flow, the system determines that the requested account status change is not allowed.<br>2. The system displays MSG12 – “Unable to change the status of this user account.”<br>3. The account status remains unchanged.<br>4. The use case ends. |

### UC-07 – Reset User Password

Sequence Diagram: [UC07_ResetUserPassword.txt](../06-sequencediagram/user-management/UC07_ResetUserPassword.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | User (the owner of the selected account), who completes the reset from the email |
| **Description** | Allows the System Admin to send the owner of an eligible user account a single-use password reset link by email. The account owner opens the link and sets a new password without signing in. The current password keeps working until the new one is set. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to reset user passwords.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: A password reset link valid for 60 minutes is sent to the user's registered email address, and any link sent to that user earlier stops working.<br>● POST-2: The password reset request is recorded in the audit log. (BR-009)<br>● POST-3: Until the link is used, the user's current password and active sessions remain unchanged.<br>● POST-4: When the account owner completes the reset, the password is replaced, every active session of that account is ended, the link can no longer be used, and the reset is recorded in the audit log. (BR-009) |
| **Normal Flow** | Reset User Password<br>1. The System Admin selects a user account and selects Reset Password.<br>2. The system asks for confirmation, showing the email address the link will be sent to.<br>3. The System Admin confirms.<br>4. The system checks that the account is active and signs in with a password.<br>5. The system sends the reset link to the user's registered email address; a link sent earlier stops working.<br>6. The system records the password reset request in the audit log. (BR-009)<br>7. The system displays MSG13 – “Password reset email sent successfully.”<br>Complete Password Reset<br>8. The account owner opens the link from the email; no sign-in is required.<br>9. The account owner enters the new password and its confirmation and submits them.<br>10. The system checks that the link is still valid, that the account is still active and signs in with a password (BR-001), and that the new password follows the password rule of account creation (UC-06a) and matches its confirmation.<br>11. The system replaces the password, ends every active session of the account and makes the link unusable.<br>12. The system records the completed reset in the audit log, with the account owner as the actor. (BR-009)<br>13. The system displays MSG92 – “Password reset successfully. Please sign in with your new password.” and opens the sign-in page. The user is not signed in automatically. |
| **Alternative Flows** | AF-1 – The browser is already signed in<br>1. At Step 13, the browser used to complete the reset still has a signed-in session.<br>2. The system signs that session out before showing the sign-in page.<br>3. If the sign-out succeeds, the flow resumes at Step 13 of the Normal Flow.<br>4. If the sign-out fails, the system still displays MSG92 – “Password reset successfully. Please sign in with your new password.” and also displays MSG06 – “Unable to sign out. Please try again.” The session stays signed in, and the new password is already in effect.<br>5. The use case ends. |
| **Exception Flows** | EX-1 – User account is not available for password reset<br>1. At Step 4 of the Normal Flow, the account no longer exists, is deactivated, or has no password because it signs in with Google only.<br>2. No link is created and no email is sent.<br>3. When the account no longer exists, the system says so and refreshes the list. Otherwise it displays MSG14 – “Password reset is not available for this account.” and states the reason when it is known.<br>4. The use case ends.<br>EX-2 – Password reset link is not valid<br>1. At Step 10 or 11, the link is unknown, has expired, has already been used, has been replaced by a newer link, or belongs to an account that is now deactivated or has no password.<br>2. The password is not changed.<br>3. The system displays MSG93 – “The password reset link is invalid or has expired. Please contact an administrator.” without saying which reason applies.<br>4. The use case ends.<br>EX-3 – New password is invalid<br>1. At Step 10, the new password breaks the password rule (at least 12 characters), or the confirmation does not match it.<br>2. The password is not changed and the link remains usable.<br>3. The system displays MSG94 – “Invalid new password.” with the reason beside the field concerned.<br>4. The flow resumes at Step 9 of the Normal Flow. |

### UC-08 – Assign System Role to User

Sequence Diagram: [UC08_AssignSystemRoleToUser.txt](../06-sequencediagram/user-management/UC08_AssignSystemRoleToUser.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to assign a system role to a user account or change the user’s existing system role. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to assign system roles.<br>● PRE-3: The selected user account exists.<br>● PRE-4: At least one system role is available for assignment. |
| **Postconditions** | ● POST-1: The selected user is associated with the selected system role.<br>● POST-2: The user receives the capabilities associated with the selected system role.<br>● POST-3: The role assignment or change is recorded in the audit log. (BR-009)<br>● POST-4: The user keeps their active sessions, is notified of the change, and the new role's permissions apply from their next action without signing in again. (BR-008)<br>● POST-5: A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143) |
| **Normal Flow** | Assign System Role to User<br>1. The System Admin opens User Management.<br>2. The System Admin selects a user account.<br>3. The system displays the user information and the current system role, if any.<br>4. The System Admin selects Assign System Role.<br>5. The system displays the available system roles.<br>6. The System Admin selects a role and confirms the assignment.<br>7. The system assigns the selected role to the user.<br>8. The system records the role assignment in the audit log. (BR-009)<br>9. The system notifies the user that their access changed; the new role's permissions apply without signing in again. (BR-008)<br>10. A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143)<br>11. The system displays MSG10 – “User account updated successfully.” |
| **Alternative Flows** | AF-1 – Change existing system role<br>1. At Step 4 of the Normal Flow, if the user already has a system role, the System Admin selects Change System Role.<br>2. The System Admin selects a different role and confirms the change.<br>3. The system replaces the current role with the selected role.<br>4. The flow resumes at Step 8 of the Normal Flow. |
| **Exception Flows** | EX-1 – Selected role is not available<br>1. The selected role is no longer available for assignment.<br>2. The system displays MSG09 – “Invalid account information.” with the field error “The role does not exist.”<br>3. No role assignment is changed. The use case ends.<br>EX-2 – Assignment would remove the last active administrator<br>1. At Step 7 of the Normal Flow or Step 3 of AF-1, the system determines that, after the change, no active account would hold the user administration permission.<br>2. The user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends. |

## FE-03 — Role-Based Access Control

### UC-09 – View Role & Permission Matrix

Sequence Diagram: [UC09_ViewRolePermissionMatrix.txt](../06-sequencediagram/role-management/UC09_ViewRolePermissionMatrix.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to view the predefined roles and their default permissions. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated. |
| **Postconditions** | ● POST-1: No system data is modified. |
| **Normal Flow** | View Role & Permission Matrix<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the role and permission matrix. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Role and permission information cannot be retrieved<br>1. At Step 2 of the Normal Flow, the system cannot retrieve the role and permission information.<br>2. The system displays an error message; no catalogue message is assigned to this case.<br>3. The use case ends. |

### UC-10 – Configure Role Permissions

Sequence Diagram: [UC10_ConfigureRolePermissions.txt](../06-sequencediagram/role-management/UC10_ConfigureRolePermissions.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to configure the default permissions assigned to a predefined system role. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage role permissions. |
| **Postconditions** | ● POST-1: The selected role is associated with the updated set of default permissions.<br>● POST-2: The permission configuration change is recorded in the audit log. (BR-009)<br>● POST-3: Users holding the role keep their active sessions; the updated permissions apply to them from their next action without signing in again. (BR-008)<br>● POST-4: A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143) |
| **Normal Flow** | Configure Role Permissions<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the Role & Permission Matrix.<br>3. The System Admin configures the permissions assigned to a predefined role.<br>4. The system validates and saves the updated permission configuration. (BR-007)<br>5. The system records the configuration change in the audit log. (BR-009)<br>6. The system notifies the users currently holding the role; the updated permissions apply without signing in again. (BR-008)<br>7. A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143)<br>8. The system displays MSG16 – “Role permissions updated successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid permission configuration<br>1. At Step 4 of the Normal Flow, the system determines that the updated permission configuration violates a permission rule.<br>2. The system does not save the changes.<br>3. The system displays MSG17 – “Invalid permission configuration.”<br>4. The use case ends. |

### UC-11 – Manage Roles

_UC gom — đặc tả nằm ở các UC con UC-11a, UC-11b… bên dưới._

#### UC-11a – Create Role

Sequence Diagram: [UC11a_CreateRole.txt](../06-sequencediagram/role-management/UC11a_CreateRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to create a new custom role with a name, an optional description and an optional initial set of permissions, without changing code or redeploying the system. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles. |
| **Postconditions** | ● POST-1: A new custom role is created and appears in the role list and in the Role & Permission Matrix.<br>● POST-2: The new role is associated with the selected permissions (the set may be empty).<br>● POST-3: The role creation is recorded in the audit log. |
| **Normal Flow** | Create Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The System Admin selects “Create Role”.<br>3. The system displays the role creation form.<br>4. The System Admin enters the role name and an optional description, selects the permissions to grant to the role, and submits the form.<br>5. The system validates the role information: the name is required and has at most 64 characters after trimming, the description has at most 256 characters, and every selected permission exists in the permission catalog.<br>6. The system checks that no existing role, including the predefined roles, has the same name (case-insensitive).<br>7. The system saves the new role together with its permissions.<br>8. The system records the role creation in the audit log (BR-009).<br>9. The system displays MSG19 – “Role created successfully.” |
| **Alternative Flows** | AF-1 – Role created without permissions<br>1. At Step 4 of the Normal Flow, the System Admin does not select any permission.<br>2. The system accepts an empty permission set; user accounts assigned to this role have the same access as a Member.<br>3. The use case continues at Step 5 of the Normal Flow. |
| **Exception Flows** | EX-1 – Invalid role information<br>1. At Step 5 of the Normal Flow, the system determines that the name is empty or longer than 64 characters, the description is longer than 256 characters, or a selected permission does not exist.<br>2. The system does not save the role.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends.<br>EX-2 – Role name already exists<br>1. At Step 6 or Step 7 of the Normal Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the role.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends. |

#### UC-11b – Update Role

Sequence Diagram: [UC11b_UpdateRole.txt](../06-sequencediagram/role-management/UC11b_UpdateRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to change the name and description of a custom role. Predefined roles cannot be renamed; the permissions of any role are configured through UC-10. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |
| **Postconditions** | ● POST-1: The name and/or description of the selected role are updated; its permissions and the user accounts holding it remain unchanged.<br>● POST-2: The change is recorded in the audit log with the values before and after the update.<br>● POST-3: No user session is terminated; users holding the role see the new name the next time their information is loaded. |
| **Normal Flow** | Update Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles.<br>3. The System Admin selects “Edit” on a custom role.<br>4. The system displays the edit form pre-filled with the current name and description.<br>5. The System Admin modifies the name and/or description and submits the form.<br>6. The system verifies that the selected role exists and is a custom role.<br>7. The system validates the role information using the same rules as UC-11 (name required, at most 64 characters after trimming; description at most 256 characters).<br>8. The system checks that no other role has the same name (case-insensitive). Changing only the letter case of the role’s own name is allowed.<br>9. The system saves the updated role.<br>10. The system records the change in the audit log (BR-009).<br>11. The system displays MSG22 – “Role updated successfully.” |
| **Alternative Flows** | AF-1 – No changes submitted<br>1. At Step 7 of the Normal Flow, the system determines that the submitted name and description are identical to the current values.<br>2. The system does not modify any data and does not record an audit log entry.<br>3. The system displays MSG22 – “Role updated successfully.”<br>4. The use case ends. |
| **Exception Flows** | EX-1 – Role is a predefined role<br>1. At Step 6 of the Normal Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not save the changes.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends.<br>EX-2 – Invalid role information<br>1. At Step 7 of the Normal Flow, the system determines that the role information is invalid.<br>2. The system does not save the changes.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends.<br>EX-3 – Role name already exists<br>1. At Step 8 or Step 9 of the Normal Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the changes.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends. |

#### UC-11c – Delete Role

Sequence Diagram: [UC11c_DeleteRole.txt](../06-sequencediagram/role-management/UC11c_DeleteRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to delete a custom role that is no longer held by any user account. Predefined roles cannot be deleted. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |
| **Postconditions** | ● POST-1: The selected role and its permission assignments are removed from the system.<br>● POST-2: The deletion, including the role name and the permissions it held, is recorded in the audit log.<br>● POST-3: No user account and no user session is affected.<br>● POST-4: Deleting a role never restarts a stopped calendar share. Because a role can be deleted only when no account holds it, the deletion itself stops no share; a reassignment that takes the meeting-creation right away is handled in UC-08. (BR-142, BR-143) |
| **Normal Flow** | Delete Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles with the number of user accounts holding each role.<br>3. The System Admin selects “Delete” on a custom role.<br>4. The system asks for confirmation.<br>5. The System Admin confirms the deletion.<br>6. The system verifies that the selected role exists, is a custom role, and is not held by any user account, including deactivated accounts.<br>7. The system deletes the role together with its permission assignments.<br>8. The system records the role deletion in the audit log (BR-009).<br>9. The system displays MSG23 – “Role deleted successfully.” |
| **Alternative Flows** | AF-1 – Deletion cancelled<br>1. At Step 5 of the Normal Flow, the System Admin cancels the deletion.<br>2. No data is changed.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Role is a predefined role<br>1. At Step 6 of the Normal Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not delete the role.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends.<br>EX-2 – Role is still held by user accounts<br>1. At Step 6 or Step 7 of the Normal Flow, the system determines that one or more user accounts (active or deactivated) still hold the role, including an account assigned to the role while the deletion was in progress.<br>2. The system does not delete the role.<br>3. The system displays MSG25 – “This role is still held by {n} user account(s). Reassign them to another role before deleting.”, where {n} is the number of user accounts holding the role.<br>4. The use case ends. |

### UC-12 – View Audit Log

Sequence Diagram: [UC12_ViewAuditLog.txt](../06-sequencediagram/role-management/UC12_ViewAuditLog.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to review the recorded business and security events — who did what, to which object, when and from which access address — for accountability and investigation. The audit log is read-only. (BR-012) |
| **Preconditions** | ● PRE-1: The System Admin is signed in.<br>● PRE-2: The System Admin's role holds the User administration permission. (BR-012) |
| **Postconditions** | ● POST-1: The audit entries matching the chosen period and filters are displayed, newest first.<br>● POST-2: No audit entry is changed or deleted. (BR-012)<br>● POST-3: The System Admin gains no access to the Meetings, documents or minutes the entries are about, even though the entries name their objects. |
| **Normal Flow** | View Audit Log<br>1. The System Admin opens Audit Log from the Administration menu.<br>2. The system checks that the System Admin's role holds the User administration permission. (BR-012)<br>3. The system retrieves the audit entries, newest first, one page at a time.<br>4. The system displays, for each entry, the time, the actor (a user account, a Guest or the system), the event, the object and the access address. The object is shown by its name, or only as “Secret meeting” for anything of a Secret meeting.<br>5. The System Admin browses the pages; the chosen period and filters are kept across pages. |
| **Alternative Flows** | AF-1 – Filter the audit log<br>1. At Step 4 of the Normal Flow, the System Admin chooses a period, an actor (a user account, Guests or the system), an event type, or enters a keyword; the keyword matches the object id, the object name — except the name of anything of a Secret meeting — and the recorded details, ignoring case and Vietnamese marks.<br>2. The system retrieves the entries that match all the chosen criteria.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – View the details of an entry<br>1. At Step 4 of the Normal Flow, the System Admin selects an entry.<br>2. The system displays the details recorded with that entry, read-only; the fields differ between event types.<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-3 – No entry matches<br>1. At Step 3 of the Normal Flow, no audit entry matches the period and filters.<br>2. The system displays an empty list and a no-results message.<br>3. The System Admin may change the period or the filters; the flow resumes at Step 1 of AF-1. |
| **Exception Flows** | EX-1 – The System Admin's role does not hold the User administration permission<br>1. At Step 2 of the Normal Flow, the System Admin's role does not hold the User administration permission, or no longer holds it.<br>2. The system returns no audit entry and the Audit Log page is not shown.<br>3. The use case ends.<br>EX-2 – The chosen period or filters are not valid<br>1. At Step 3 of the Normal Flow, the start of the period is after its end, or a filter value is not valid.<br>2. No entry is retrieved. The system displays MSG469 – “The audit log filter is invalid. Check the period and the selected values.”<br>3. The flow resumes at Step 1 of AF-1. |

---

## FE-04 — Meeting Management

### UC-13 – Create Meeting

Sequence Diagram: [UC13_CreateMeeting.txt](../06-sequencediagram/meeting-management/UC13_CreateMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | None |
| **Description** | Allows an authorized Member to create one independent Meeting as a Draft, including its planned schedule, mode, location, confidentiality and its initial participants. An Approver can also do this, because the Approver role also holds the create-meeting permission. |
| **Preconditions** | ● PRE-1: The Member is authenticated.<br>● PRE-2: The Member is authorized to create meetings. |
| **Postconditions** | ● POST-1: A new Meeting exists in Draft status with its planned start and end times.<br>● POST-2: The creator is recorded as Member.<br>● POST-3: The selected participants are saved with the Meeting, each with a Pending invitation response; no invitation is sent while the Meeting is Draft. The Meeting may subsequently be prepared and submitted for approval, and a Secretary may still be assigned later through UC-30a.<br>● POST-4: The creation is recorded in the Meeting's lifecycle history and in the audit log. (BR-022, BR-023) |
| **Normal Flow** | Create Meeting<br>1. The Member selects Create Meeting.<br>2. The system displays the new Meeting form.<br>3. The Member enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description, and chooses the initial participants with a Meeting Role for each: exactly one Chair, and optionally a Secretary and Members. For an Online or Hybrid Meeting an external Online Link of at most 1,000 characters may be entered; left blank, the TrustCabinet online meeting room is used.<br>4. The Member submits the form.<br>5. The system checks the required values and the planned times, and checks the chosen participants: each is an existing active internal user listed once, with a valid Meeting Role, exactly one is the Chair, and the list holds 1 to 100 participants. Guests cannot be added at creation, and the creator need not be a participant. (BR-013)<br>6. The system creates exactly one Meeting in Draft status, records the creator and saves the chosen participants with Pending responses. (BR-013)<br>7. The system records the creation in the Meeting's lifecycle history and in the audit log. (BR-022, BR-023)<br>8. The system displays MSG29 – "Meeting created successfully." |
| **Alternative Flows** | AF-1 – Create Draft from a found time slot<br>1. At Step 1, the Member chooses a time slot found in UC-81a instead.<br>2. The Draft Meeting is created through UC-81b, which completes the use case. |
| **Exception Flows** | EX-1 – Meeting information is invalid<br>1. At Step 5, a required value is missing/invalid, the planned start is not in the future, or the planned end does not follow the start. (BR-013)<br>2. The system displays MSG31 – "Invalid meeting information." and highlights the affected fields.<br>3. No Meeting is created. The Member may correct the inputs.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – Participants are invalid<br>1. At Step 5, no participant is chosen, a participant is not an existing active internal user or is listed twice, a Meeting Role is invalid, or the choice does not contain exactly one Chair.<br>2. The system displays MSG277 – "The meeting participant list is not valid." and highlights the participant list.<br>3. No Meeting is created. The Member may correct the participants.<br>4. The flow resumes at Step 4 of the Normal Flow. |

### UC-14 – Update Meeting

Sequence Diagram: [UC14_UpdateMeeting.txt](../06-sequencediagram/meeting-management/UC14_UpdateMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | None |
| **Description** | Allows the Member to update Draft/Rejected Meeting information, or to reschedule only the planned times or location when the Meeting is Published. An Approver can also do this, because the Approver role also holds the create-meeting permission. |
| **Preconditions** | ● PRE-1: The Meeting exists and the Member is authenticated and authorized.<br>● PRE-2: Full Meeting editing is available only in Draft or Rejected. (BR-014)<br>● PRE-3: If the Meeting is Published, only the Member may invoke Reschedule to change planned times, location or online link. The Meeting must not have started. (BR-015) |
| **Postconditions** | ● POST-1: Authorized updates are saved to the Meeting.<br>● POST-2: Meeting lifecycle and assigned approver are unchanged.<br>● POST-3: When the schedule of a Published Meeting changes, the change is recorded and the invitees are notified. (BR-022, BR-023, BR-031, BR-035) |
| **Normal Flow** | Update Meeting<br>1. The Member opens a Draft or Rejected Meeting and selects Edit.<br>2. The system displays the editable Meeting fields.<br>3. The Member changes the fields and submits.<br>4. The system verifies current Meeting status, permissions, required information, and confirms Planned End Time is after Planned Start Time. (BR-013, BR-014)<br>5. The system saves the updates without changing Meeting status. (BR-014)<br>6. The system displays MSG30 – “Meeting updated successfully.” |
| **Alternative Flows** | AF-1 – Reschedule a published meeting<br>1. At Step 1 of the Normal Flow, the Member selects Reschedule for a Meeting that is Published and has not started.<br>2. The system allows changes only to Planned Start Time, Planned End Time, Location and Online Link; all other fields are read-only. (BR-015)<br>3. The Member enters the changes and submits.<br>4. The system validates that the new start is in the future and the end follows the start. (BR-013, BR-015)<br>5. The system saves the new schedule, keeps the Meeting Published and records the change in the Meeting's history and in the audit log. (BR-022, BR-023)<br>6. The invitees are notified of the new schedule; the Member who rescheduled receives the email but no in-app notification. A delivery failure does not undo the saved change. (BR-031, BR-032, BR-035)<br>7. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified." The use case ends.<br>AF-2 – Nothing has changed<br>1. At Step 3 of the Normal Flow or Step 3 of AF-1, the Member submits without changing any value; spaces added at the start or end of a text value do not count as a change.<br>2. Nothing is saved and no notification is sent. The system displays MSG154 – "No changes to save." The use case ends.<br>AF-3 – Planned start has already passed<br>1. At Step 3 of the Normal Flow, the Member sets the Planned Start Time of a Draft or Rejected Meeting to a time that has passed, by less than 24 hours.<br>2. The system warns that a Meeting not started within 24 hours of its planned start is cancelled. (BR-029)<br>3. The Member may still submit; the flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Invalid or unauthorized update<br>1. At Step 4, the meeting information is invalid, or the Meeting status no longer permits this edit. (BR-013, BR-014)<br>2. The system does not save any changes and displays MSG31 – “Invalid meeting information.” with the field errors, or MSG33 – “This meeting can no longer be edited.” when the status does not permit the edit.<br>3. For invalid information, the Member corrects it and the flow resumes at Step 3 of the Normal Flow; otherwise the use case ends.<br>EX-2 – Reschedule not permitted<br>1. At Step 4 of AF-1, the Meeting is no longer Published or has already started, its planned start is not in the future, or a field outside the allowed Reschedule set was modified. (BR-015)<br>2. The existing schedule remains unchanged. The system displays MSG33 – “This meeting can no longer be edited.” when the Meeting can no longer be rescheduled, or MSG31 – “Invalid meeting information.” with the field errors otherwise.<br>3. The use case ends.<br>EX-3 – Planned start is more than 24 hours ago<br>1. At Step 3 of the Normal Flow, the Planned Start Time of a Draft or Rejected Meeting is more than 24 hours in the past.<br>2. The update is not submitted, because such a Meeting would be cancelled at once (BR-029). The system asks for another start time.<br>3. The flow resumes at Step 3 of the Normal Flow. |

### UC-15 – View Meeting Details

Sequence Diagram: [UC15_ViewMeetingDetails.txt](../06-sequencediagram/meeting-management/UC15_ViewMeetingDetails.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows authorized users to view the details of one Meeting, including planned schedule, actual start/end times when available, and meeting-level status. Agenda and other records remain accessible through their corresponding views. |
| **Preconditions** | ● PRE-1: The user is authenticated.<br>● PRE-2: A meeting has been selected or identified. |
| **Postconditions** | ● POST-1: The Meeting Name, planned time range, actual times where available, mode, location, confidentiality, description and lifecycle status are displayed read-only. For a Cancelled Meeting, the cancellation reason, who cancelled it and when are also displayed.<br>● POST-2: The Meeting is not modified.<br>● POST-3: The status history of the Meeting is displayed beside its details; a rejection reason is shown only to the people allowed to read it. (BR-022, BR-023) |
| **Normal Flow** | View Meeting Details<br>1. The user requests the selected Meeting.<br>2. The system verifies that the Meeting exists and the user has access. (BR-016, BR-017)<br>3. The system retrieves the Meeting information and planned/actual time fields.<br>4. The system displays Meeting Name, planned start/end time, actual start/end time when available, Meeting Mode, Location, Confidentiality Level, Description, and Status. For a Cancelled Meeting, it also displays the cancellation reason, who cancelled it and when.<br>5. The user reviews the Meeting details. |
| **Alternative Flows** | AF-1 – View the status history<br>1. At Step 5 of the Normal Flow, the user reviews the status history shown beside the Meeting details.<br>2. The system shows every recorded status change, oldest first, with its action, resulting status, time and actor. (BR-022, BR-023)<br>3. A rejection reason is shown only to the Member who created the Meeting, its assigned Approver and its current Chair and Secretary; other readers see the same changes without it.<br>4. The flow resumes at Step 5 of the Normal Flow. |
| **Exception Flows** | EX-1 – Meeting details cannot be loaded<br>1. At Step 3, the meeting exists and is accessible, but the system cannot retrieve its meeting-level information.<br>2. The system displays MSG37 – "Unable to load meeting details. Please try again."<br>3. The use case ends.<br>EX-2 – Status history cannot be loaded<br>1. At Step 2 of AF-1, the system cannot retrieve the status history.<br>2. The Meeting details stay displayed. The system displays MSG136 – “Unable to load the meeting workflow data. Please try again.”<br>3. The use case ends. |

### UC-16 – View Meeting List

Sequence Diagram: [UC16_ViewMeetingList.txt](../06-sequencediagram/meeting-management/UC16_ViewMeetingList.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment. The assigned Secretary can view the Meeting before publication under BR-030. External Guests do not use the internal Meeting list. |
| **Preconditions** | ● PRE-1: The user is authenticated. |
| **Postconditions** | ● POST-1: The system displays the list of meetings that the user is authorized to view.<br>● POST-2: No meeting information is modified. |
| **Normal Flow** | View Meeting List<br>1. The user selects Meetings from the navigation menu. The list opens unfiltered, showing every status at any time.<br>2. The system selects only the meetings the user may view: those the user created, at any status; those assigned to the user as Approver, from WaitingApproval onward; those where the user is Chair or active Secretary, at any status; and those where the user is a Member, from Published onward. (BR-016, BR-017, BR-030)<br>3. The system displays those meetings, including Cancelled Meetings and Meetings the user has declined, sorted by planned start time, newest first. (BR-028, BR-047, BR-048)<br>4. The user browses the displayed meetings. |
| **Alternative Flows** | AF-1 – Search meetings<br>1. At Step 4 of the Normal Flow, the user enters a meeting name in the search field.<br>2. The system finds the meetings whose name contains the entered text within the meetings available to the user, ignoring letter case and Vietnamese diacritics.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Filter meetings<br>1. At Step 4 of the Normal Flow, the user selects one or more filter criteria: Meeting Status (including Cancelled), Confidentiality Level, or Time (Today, This week, Not yet held, Already held).<br>2. The system applies the selected criteria to the meetings available to the user. Today and This week match a Meeting whose planned time overlaps that day or week; Not yet held matches a Meeting whose planned start is later than now; Already held matches one whose planned start is now or earlier, so a Meeting in progress counts as Already held.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – No matching meetings<br>1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.<br>2. The system displays an empty meeting list and the message "No meetings match the filter."<br>3. The user may change or clear the current search/filter criteria; the flow resumes at Step 4 of the Normal Flow.<br>AF-4 – View meetings on the calendar<br>1. At Step 1 of the Normal Flow, the user opens the Calendar instead of the list.<br>2. The system shows the authorized meetings by week, grouped by status, and omits the Meetings the user has declined through UC-31. (BR-047, BR-048) Cancelled Meetings form their own group, hidden by default. (BR-028)<br>3. The user browses the displayed meetings.<br>4. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Criteria are invalid<br>1. The system determines that the selected search, filter, sort, or display criteria are invalid.<br>2. The system displays MSG32 – “The meeting list criteria are invalid.”<br>3. The user may clear or adjust the search/filter criteria.<br>4. The use case ends. |

### UC-17 – Submit Meeting for Approval

Sequence Diagram: [UC17_SubmitMeetingForApproval.txt](../06-sequencediagram/meeting-management/UC17_SubmitMeetingForApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | None |
| **Description** | Allows the Member to submit a Draft meeting or resubmit a previously Rejected meeting for review by an assigned Approver based on the Meeting information and planned schedule. An Approver can also do this, because the Approver role also holds the create-meeting permission. |
| **Preconditions** | ● PRE-1: The Member is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Member is authorized to submit the selected meeting for approval.<br>● PRE-4: The meeting is in Draft or Rejected status. |
| **Postconditions** | ● POST-1: The meeting status is WaitingApproval.<br>● POST-2: A valid Approver is assigned to the current approval cycle.<br>● POST-3: The assigned Approver is granted access to the meeting for approval purposes.<br>● POST-4: The submission is recorded in the meeting lifecycle history.<br>● POST-5: The assigned Approver is notified of the pending approval request.<br>● POST-6: The submission and the assigned approver are recorded in the audit log. (BR-022, BR-023) |
| **Normal Flow** | Submit Meeting for Approval<br>1. The Member selects a Draft meeting and chooses Submit for Approval.<br>2. The system verifies that the Meeting information and planned schedule are complete and valid for submission. (BR-013)<br>3. The system displays the eligible Approvers with their name and email; the Member may search the list by name or email.<br>4. The Member selects an Approver and confirms.<br>5. The system checks that the Meeting is still Draft or Rejected and that the chosen Approver is still eligible and is not the Member. (BR-018, BR-019)<br>6. The system assigns the Approver, changes the Meeting to WaitingApproval and records the submission in the status history and the audit log. (BR-018, BR-022, BR-023)<br>7. The assigned Approver is notified.<br>8. The system displays MSG78 – “Meeting submitted for approval.” |
| **Alternative Flows** | AF-1 – Resubmit a rejected meeting<br>1. At Step 1 of the Normal Flow, the Member selects a meeting in Rejected status and chooses Resubmit for Approval.<br>2. At Step 3, the Approver of the previous approval cycle is preselected while still eligible; the Member may keep or change the selection.<br>3. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Incomplete meeting information<br>1. At Step 2, mandatory Meeting information or the planned schedule is incomplete or invalid.<br>2. The system displays MSG81 – “The meeting does not have enough information to be submitted for approval.” and identifies the missing or invalid information.<br>3. The meeting remains Draft or Rejected and the use case ends.<br>EX-2 – Meeting status not eligible<br>1. At Step 5, the system determines that the meeting is not in Draft or Rejected status.<br>2. The system displays MSG41 – "This meeting can no longer be submitted for approval."<br>3. The use case ends.<br>EX-3 – No eligible approver available<br>1. At Step 3, the system finds no eligible Approver. (BR-019)<br>2. The system displays MSG140 – "No eligible meeting approver is available. Please contact the administrator."<br>3. The Meeting remains Draft or Rejected; no assignment, submission or notification is made.<br>4. The use case ends.<br>EX-4 – Chosen Approver is no longer eligible<br>1. At Step 5, the chosen Approver is no longer active, can no longer approve meetings, or is the Member. (BR-019)<br>2. Nothing is changed. The system displays MSG82 – “The approver is not valid or can no longer approve meetings.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

### UC-18 – Review Meeting Approval

Sequence Diagram: [UC18_ReviewMeetingApproval.txt](../06-sequencediagram/meeting-management/UC18_ReviewMeetingApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Approver |
| **Secondary Actors** | None |
| **Description** | Allows the Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it. |
| **Preconditions** | ● PRE-1: The Approver is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Approver is currently assigned to review the selected meeting and is not its Member.<br>● PRE-4: The meeting is in WaitingApproval status. |
| **Postconditions** | ● POST-1: The Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Member and assigned Secretary so the Member can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history.<br>● POST-6: The Member is notified in the application of the approval or rejection; a rejection notification includes the rejection reason. No email is sent for the decision.<br>● POST-7: The approval or rejection is recorded in the audit log. (BR-022, BR-023) |
| **Normal Flow** | Review Meeting Approval<br>1. The Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Approver.<br>3. The system displays the meeting information for review.<br>4. The Approver selects Approve.<br>5. The system re-verifies that the Approver may still approve the meeting and that it is still in WaitingApproval status. (BR-019)<br>6. The system changes the meeting status from WaitingApproval to Approved and records the approval, the actor and the time in the status history and in the audit log. (BR-020, BR-022, BR-023)<br>7. The Member is notified in the application that the meeting has been approved.<br>8. The system displays MSG43 – "Meeting approved successfully." |
| **Alternative Flows** | AF-1 – Reject the meeting<br>1. At Step 4 of the Normal Flow, the Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters. (BR-020)<br>5. The system re-verifies that the Approver may still reject the meeting and that it is still in WaitingApproval status. (BR-019)<br>6. The system changes the meeting status to Rejected and records the rejection, its reason, the actor and the time in the status history and in the audit log. (BR-020, BR-022, BR-023)<br>7. The Member is notified in the application that the meeting has been rejected, with the reason; the reason is also available to the assigned Secretary so the Member can correct and resubmit the Meeting.<br>8. The system displays MSG44 – "Meeting rejected successfully." |
| **Exception Flows** | EX-1 – Rejection reason is missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-020)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting is no longer awaiting approval<br>1. At Step 5 of the Normal Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status, or the Approver is no longer assigned to it.<br>2. No approval or rejection decision is applied; the meeting remains in its current status.<br>3. The system displays MSG132 – “The meeting status has changed or no longer allows this action.”<br>4. The use case ends.<br>EX-3 – Rejection reason is too long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |

### UC-19 – Publish Meeting

Sequence Diagram: [UC19_PublishMeeting.txt](../06-sequencediagram/meeting-management/UC19_PublishMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | None |
| **Description** | Allows the Member to publish an Approved meeting, which invites its current internal participants and its external Guests. |
| **Preconditions** | ● PRE-1: The Member is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Member is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-021, BR-024) |
| **Postconditions** | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history and in the audit log. (BR-022, BR-023)<br>● POST-3: The current internal participants and the Guests with a current invitation are invited; each Guest is invited to verify the invited email address and access only this Meeting. Publication does not wait for the invitations to be delivered, and a delivery failure does not undo it. (BR-031, BR-032, BR-052, BR-053, BR-054) |
| **Normal Flow** | Publish Meeting<br>1. The Member selects an Approved meeting.<br>2. The Member selects Publish Meeting.<br>3. The system verifies that the Member remains authorized, the meeting is still Approved with a valid planned schedule, and it has exactly one active current Chair and at least one active current Secretary. (BR-021, BR-024)<br>4. The system changes the meeting status from Approved to Published and records the publication, the actor and the time in the meeting lifecycle history and in the audit log. (BR-021, BR-022, BR-023)<br>5. The current internal participants are notified in the application and by email, except that the Member who published receives no in-app notification about their own action; each Guest with a current invitation receives an email invitation. (BR-031, BR-035, BR-052, BR-053)<br>6. The system displays MSG45 – "Meeting published successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting is no longer eligible for publication<br>1. At Step 3 of the Normal Flow, the system determines that the meeting is no longer Approved, the Member is no longer authorized to publish it, there is not exactly one active current Chair, or there is no active current Secretary. (BR-021)<br>2. The meeting remains unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG46 – "This meeting can no longer be published."<br>5. The use case ends. |

### UC-20 – Start Meeting

Sequence Diagram: [UC20_StartMeeting.txt](../06-sequencediagram/meeting-management/UC20_StartMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Chair or Secretary to start one Published Meeting, set the Meeting lifecycle to InProgress and record its actual start time. The Meeting has no subordinate scheduling objects. |
| **Preconditions** | ● PRE-1: The Chair or the Secretary is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The meeting is Published.<br>● PRE-4: The meeting has exactly one current Chair and at least one current Secretary with active accounts.  (BR-024) |
| **Postconditions** | ● POST-1: Meeting status is InProgress.<br>● POST-2: Actual Start Time and the starter (Chair or Secretary) are recorded in the status history and in the audit log. (BR-022, BR-023)<br>● POST-3: The current invited internal participants are notified that the Meeting started, except the person who started it. (BR-035)<br>● POST-4: Online Meeting Room access is governed by the Meeting mode and current status. |
| **Normal Flow** | Start Meeting<br>1. The assigned Chair or Secretary selects Start Meeting for the Published Meeting.<br>2. The system verifies the starter is an active current Chair or Secretary, one active current Chair and at least one active Secretary exist, and the Meeting remains Published. (BR-024)<br>3. The system changes status to InProgress, saves Actual Start Time, and records the start in the status history and in the audit log. (BR-022, BR-023, BR-024)<br>4. The current invited internal participants are notified, except the person who started the Meeting; when the Secretary starts it, the Chair is notified. A delivery failure does not undo the start. (BR-032, BR-035)<br>5. The system displays MSG110 – "Meeting started successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting cannot be started<br>1. At Step 2, the meeting is no longer Published, the starter is no longer an assigned active Chair or Secretary, or the required active Chair/Secretary assignments are no longer valid. (BR-024)<br>2. The meeting remains unchanged.<br>3. The system displays MSG112 – "This meeting cannot be started."<br>4. The use case ends. |

### UC-21 – End Meeting

Sequence Diagram: [UC21_EndMeeting.txt](../06-sequencediagram/meeting-management/UC21_EndMeeting.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to end an InProgress Meeting regardless of open Votes, unmarked attendance or remaining Pending Agenda Items. |
| **Preconditions** | ● PRE-1: The actor is the current authorized Chair.<br>● PRE-2: The Meeting is InProgress. (BR-024, BR-025) |
| **Postconditions** | ● POST-1: The Meeting becomes Ended and its Actual End Time is recorded.<br>● POST-2: Any InProgress Agenda Item is completed; remaining Pending Items stay Pending. (BR-026)<br>● POST-3: Online Meeting Room access is closed. (BR-025)<br>● POST-4: Draft Votes that were never opened are deleted. (BR-086, BR-087)<br>● POST-5: The end of the Meeting is recorded in the status history and in the audit log. (BR-022, BR-023) |
| **Normal Flow** | End Meeting<br>1. The Chair selects End Meeting and confirms.<br>2. The system checks current Chair authority and that the Meeting is InProgress. Open Votes, unmarked attendance and Pending Agenda Items do not block the end. (BR-024, BR-025, BR-026)<br>3. The system changes the Meeting to Ended, records Actual End Time and records the end in the status history and in the audit log. (BR-022, BR-023)<br>4. The system completes any InProgress Agenda Item and deletes Draft Votes that were never opened; Pending Items stay Pending and opened Votes and their ballots are unchanged. (BR-026, BR-087)<br>5. The system closes room access and displays MSG111 – "Meeting ended successfully." (BR-025)<br>6. Votes stop accepting ballots because the Meeting is Ended; the Chair may later close outstanding Votes to finalize their tally. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor or Meeting is not eligible<br>1. At Step 2, the actor is no longer the current Chair or the Meeting is no longer InProgress. (BR-025)<br>2. No end is recorded. The system displays MSG114 – "This meeting cannot be ended."<br>3. The use case ends. |

### UC-22 – Cancel Meeting

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | Resend Email |
| **Description** | Allows the Member to cancel a Meeting that will no longer take place, before it starts. A Cancelled Meeting stays in the system as a read-only record with its cancellation reason; it cannot be edited, published or started again. (BR-028) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the Member of the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved or Published. An InProgress Meeting is ended through UC-21 instead. (BR-028) |
| **Postconditions** | ● POST-1: The Meeting is Cancelled, and the reason, the actor and the cancellation time are recorded in its status history. (BR-022, BR-023, BR-028)<br>● POST-2: Agenda, participants, documents and Votes are kept read-only, and any pending approval request is closed. (BR-028)<br>● POST-3: If the Meeting was Published, every invited participant and Guest is notified; if it was awaiting approval, its assigned Approver is notified. (BR-031)<br>● POST-4: The cancellation is recorded in the audit log. (BR-022, BR-023) |
| **Normal Flow** | Cancel Meeting<br>1. The Member opens the Meeting details and selects Cancel meeting.<br>2. The system asks for confirmation with a required cancellation reason.<br>3. The Member enters the reason and confirms.<br>4. The system rechecks that the actor is the Member, the Meeting is Draft, WaitingApproval, Rejected, Approved or Published, and the reason is not blank and not longer than 1,000 characters. (BR-028)<br>5. The system changes the Meeting to Cancelled, closes any pending approval request, and records the reason, the actor and the time in the status history and in the audit log. (BR-022, BR-023, BR-028)<br>6. If the Meeting was Published, every invited internal participant and Guest is notified with the reason, and the event is removed from their calendars; the Member who cancelled receives no in-app notification. If the Meeting was WaitingApproval, its assigned Approver is notified that nothing is left to review. In any other status nobody is notified. A delivery failure does not undo the cancellation. (BR-031, BR-032, BR-035)<br>7. The system displays MSG261 – "Meeting cancelled successfully." and shows the Meeting as Cancelled. |
| **Alternative Flows** | AF-1 – Member keeps the Meeting<br>1. At Step 3, the Member chooses Back instead of confirming.<br>2. The Meeting and its status remain unchanged. The use case ends. |
| **Exception Flows** | EX-1 – Cancellation reason is missing<br>1. At Step 4, the reason is blank or contains only spaces.<br>2. The Meeting is not cancelled. The system displays MSG262 – "Enter a reason for cancelling the meeting."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Cancellation reason is too long<br>1. At Step 4, the reason is longer than 1,000 characters.<br>2. The Meeting is not cancelled. The system displays MSG345 – "Cancellation reason must not exceed 1000 characters."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting can no longer be cancelled<br>1. At Step 4, the Meeting has started, has ended or is already Cancelled. (BR-028)<br>2. The Meeting remains unchanged. The system displays MSG263 – "This meeting can no longer be cancelled."<br>3. The use case ends. |

## FE-05 — Agenda Management

### UC-23 – Manage Agenda Items

_UC gom — đặc tả nằm ở các UC con UC-23a, UC-23b… bên dưới._

#### UC-23a – Create Agenda Item

Sequence Diagram: [UC23a_CreateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23a_CreateAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to create a new Agenda Item while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including during the Meeting. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-058, BR-059) |
| **Postconditions** | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting. Its document links are added afterwards through UC-36b, and creating an Item never changes another Item's links.<br>● POST-3: A Presenter, when set, is a current participant or Guest of the Meeting. (BR-064, BR-065) |
| **Normal Flow** | Create Agenda Item<br>1. The Secretary opens the Meeting Agenda and selects Add Agenda Item.<br>2. The Secretary enters Title, Description and an optional Presenter chosen from the current internal participants and Guests of the Meeting. Documents are attached to the Item afterwards through UC-36b. (BR-064, BR-065)<br>3. The system verifies the active Secretary assignment, that the Meeting status still permits Agenda planning, that the Title is given and that the Presenter is valid; Description and Presenter are optional. (BR-058, BR-059, BR-064, BR-065)<br>4. The system creates the Item as Pending after the last Item of the Agenda; the other Items keep their positions. If the Meeting is Published or InProgress and the Presenter is not the Secretary, the Presenter is notified. A Presenter who has declined the Meeting invitation may still be chosen and may still join. (BR-047, BR-048, BR-035)<br>5. The system displays MSG60 – "Agenda item created successfully." Authorized users viewing the Agenda see the new Item. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid agenda item information<br>1. At Step 3, required values are invalid.<br>2. No Item is created. The system displays MSG66 – "Invalid agenda item information." with the applicable field messages.<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Agenda planning no longer permitted<br>1. At Step 3, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. No Item is created. The system displays MSG67 – "The agenda for this meeting can no longer be modified."<br>3. The use case ends. |

#### UC-23b – Update Agenda Item

Sequence Diagram: [UC23b_UpdateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23b_UpdateAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to update the Title, Presenter and Description of an Agenda Item, whatever its execution status, while Agenda planning is permitted. Document links are not part of this form; they change only through UC-36b and UC-36d. |
| **Preconditions** | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-058, BR-059)<br>● PRE-3: Title, Presenter and Description may be changed whether the Item is Pending, InProgress or Done. |
| **Postconditions** | ● POST-1: The Item information is updated; its document links are unchanged.<br>● POST-2: Meeting membership, Item execution status, actual timestamps and Meeting lifecycle remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |
| **Normal Flow** | Update Agenda Item<br>1. The Secretary selects an Item and chooses Edit.<br>2. The system displays the current Title, Presenter and Description for editing, whatever the Item's execution status. Document links are not changed here: they are added or removed until the Meeting ends through UC-36b and UC-36d. The Presenter is chosen from the current internal participants and Guests of the Meeting. (BR-064, BR-065, BR-070, BR-071, BR-078)<br>3. The Secretary enters permitted changes and submits.<br>4. The system rechecks the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and the Presenter. (BR-058, BR-059, BR-064, BR-065)<br>5. The system saves the changes, keeping the execution status, actual times and position. If the Presenter was assigned or changed while the Meeting is Published or InProgress, the new Presenter is notified unless it is the Secretary who made the change. A Presenter who has declined the Meeting invitation may still be chosen and may still join. (BR-047, BR-048, BR-035)<br>6. The system displays MSG61 – "Agenda item updated successfully." Authorized users viewing the Agenda see the change. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid agenda item information<br>1. At Step 4, the submitted values are invalid.<br>2. The Item remains unchanged. The system displays MSG66 – “Invalid agenda item information.” with the applicable field messages.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Editing no longer permitted<br>1. At Step 4, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. The Item remains unchanged. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends. |

#### UC-23c – Delete Agenda Item

Sequence Diagram: [UC23c_DeleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC23c_DeleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to delete only a Pending Agenda Item from the Meeting. |
| **Preconditions** | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to a Meeting in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-058, BR-059)<br>● PRE-3: The Item is Pending (not started). Only not-started Items can be deleted; InProgress and Done Items cannot. (BR-063) |
| **Postconditions** | ● POST-1: The selected Pending Item and its document links are removed. The documents stay in the Meeting and keep links to other Items; a document with no remaining links becomes a general Meeting document.<br>● POST-2: Remaining Item positions and execution data, and the Meeting lifecycle, are unchanged.<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |
| **Normal Flow** | Delete Agenda Item<br>1. The Secretary selects a Pending Item, chooses Delete and confirms.<br>2. The system verifies the active Secretary assignment, permitted Meeting status, Item membership and that the Item is still Pending. (BR-058, BR-059, BR-063)<br>3. The system removes the Item and its document links, keeps the documents in the Meeting and preserves the remaining Item positions.<br>4. The system displays MSG62 – "Agenda item deleted successfully." Authorized users viewing the Agenda see the change. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda planning no longer permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. Nothing is deleted. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends.<br>EX-2 – Agenda item has already started<br>1. At Step 2, the Item is InProgress or Done.<br>2. The Item and Agenda order remain unchanged. The system displays MSG142 – "Only Pending agenda items can be deleted."<br>3. The use case ends. |

#### UC-23d – Reorder Agenda Items

Sequence Diagram: [UC23d_ReorderAgendaItems.txt](../06-sequencediagram/agenda-management/UC23d_ReorderAgendaItems.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to change the order of the Agenda Items within the same Meeting, whatever their execution status; Done Items may also be reordered. |
| **Preconditions** | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains at least two Agenda Items. (BR-058, BR-059) |
| **Postconditions** | ● POST-1: The Items of the Meeting are in the requested order.<br>● POST-2: Item membership, execution status and actual times remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the new order. |
| **Normal Flow** | Reorder Agenda Items<br>1. The Secretary changes the relative order of the Agenda Items.<br>2. The system verifies the active Secretary assignment, permitted Meeting status and the latest saved Agenda. (BR-058, BR-059)<br>3. The system verifies that each current Item of the Meeting appears exactly once and all Items belong to this Meeting. (BR-058, BR-059)<br>4. The system saves the new order; no Item is left in a partial order.<br>5. The system displays MSG63 – "Agenda order updated successfully." Authorized users viewing the Agenda see the new order. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Reordering no longer permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. The saved order remains unchanged. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends.<br>EX-2 – Invalid or outdated agenda order<br>1. At Step 3, the order submitted duplicates or omits a current Item, includes an Item from another Meeting, or no longer matches the current Agenda.<br>2. No order changes are saved. The system displays MSG143 – "The agenda order is invalid or out of date. Refresh the agenda and try again." and shows the latest Agenda.<br>3. The flow resumes at Step 1 of the Normal Flow. |

### UC-24 – Set Current Agenda Item

Sequence Diagram: [UC24_SetCurrentAgendaItem.txt](../06-sequencediagram/agenda-management/UC24_SetCurrentAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to start any Pending Agenda Item as the current Item, whatever its position in the Agenda. An Item must be started before it can be finished (UC-25). |
| **Preconditions** | ● PRE-1: The Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Item is Pending. (BR-060, BR-061, BR-062)<br>● PRE-3: No other Item of the Meeting is InProgress. (BR-060, BR-061, BR-062) |
| **Postconditions** | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |
| **Normal Flow** | Set Current Agenda Item<br>1. The Chair selects a Pending Item and chooses Set as Current.<br>2. The system rechecks the active Chair assignment, Meeting InProgress status and that the selected Item belongs to the Meeting and remains Pending. (BR-058, BR-059, BR-060, BR-061, BR-062)<br>3. The system verifies that no other Item is InProgress. The position of the selected Item does not matter. (BR-060, BR-061, BR-062)<br>4. The system changes the Item to InProgress and records its actual start time. Only one Item of the Meeting can be InProgress at a time. (BR-060, BR-061, BR-062)<br>5. The system displays MSG64 – "Current agenda item updated successfully." Authorized users viewing the Agenda see the current Item. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda item cannot become current<br>1. At Step 2, the actor is not the active assigned Chair, the Meeting is not InProgress, or the selected Item does not belong to the Meeting or is not Pending.<br>2. The Agenda remains unchanged. The system displays MSG73 – “This agenda item cannot be set as current.”<br>3. The use case ends.<br>EX-2 – Another agenda item is in progress<br>1. At Step 3 or Step 4, another Item is already InProgress.<br>2. The selected Item remains Pending. The system displays MSG72 – “Another agenda item is already in progress. Complete it before starting another agenda item.”<br>3. The use case ends. |

### UC-25 – Complete Agenda Item

Sequence Diagram: [UC25_CompleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC25_CompleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to complete the current Agenda Item at any time. An Item must have been started (InProgress) before it can be finished; a Pending Item cannot be completed directly. |
| **Preconditions** | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |
| **Postconditions** | ● POST-1: The current Item becomes Done with its actual end time.<br>● POST-2: The Meeting remains InProgress. No next Item starts automatically. (BR-060, BR-061, BR-062)<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |
| **Normal Flow** | Complete Agenda Item<br>1. The Chair selects Complete for the current Item.<br>2. The system verifies the active Chair assignment, Meeting InProgress status, Item membership and current InProgress status. (BR-058, BR-059, BR-060, BR-061, BR-062)<br>3. The system records the actual end time and changes the Item to Done. (BR-060, BR-061, BR-062)<br>4. The Meeting stays InProgress with no current Item. The Chair may start any remaining Pending Item through UC-24.<br>5. The system displays MSG65 – "Agenda item completed successfully." Authorized users viewing the Agenda see the change. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda item cannot be completed<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the Item does not belong to the Meeting or is not its current InProgress Item.<br>2. Nothing changes. An Item that does not belong to this Meeting is treated as not found and no message is displayed. Otherwise the system displays MSG74 – “This agenda item cannot be completed.”<br>3. The use case ends. |

### UC-26 – View Meeting Agenda

Sequence Diagram: [UC26_ViewMeetingAgenda.txt](../06-sequencediagram/agenda-management/UC26_ViewMeetingAgenda.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view the complete Meeting Agenda and automatically see saved changes while the Agenda remains open. |
| **Preconditions** | ● PRE-1: The user may view the selected Meeting. (BR-016, BR-017) A Guest with a verified, current invitation sees the Agenda read-only while the Meeting is Published or InProgress; once the Meeting has ended, the Guest can no longer view it. (BR-055) |
| **Postconditions** | ● POST-1: Agenda title, presenter, order, execution status, recorded actual times and any recorded Item conclusion are shown read-only.<br>● POST-2: The Agenda shown reflects every saved change: creation, editing, deletion, reordering, Item start and completion, and completion of the current Item when the Meeting ends.<br>● POST-3: Viewing does not modify the Agenda. |
| **Normal Flow** | View Meeting Agenda<br>1. The user opens the Agenda of the Meeting.<br>2. The system verifies Meeting access and retrieves the complete saved Agenda in its current order. (BR-016, BR-017, BR-058, BR-059)<br>3. The system shows Item title, presenter, status, order, recorded actual times and any Item conclusion with who last saved it and when.<br>4. While the user keeps the Agenda open, every saved change made by an authorized user appears without reloading the page, as long as the user may still view the Meeting. Changes that failed are never shown. |
| **Alternative Flows** | AF-1 – Empty meeting agenda<br>1. At Step 3, no Agenda Items exist.<br>2. The system displays the empty Agenda state.<br>3. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Meeting not accessible<br>1. At Step 2 or Step 4, the Meeting does not exist or the user lacks access.<br>2. No further Agenda information is disclosed.<br>3. The use case ends. |

### UC-27 – Record Agenda Item Conclusion

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to record or update the conclusion of an Agenda Item: what was concluded or agreed for that item. Item conclusions are used when the minutes draft is generated (UC-70). Vote results and conclusions remain recorded through UC-67. |
| **Preconditions** | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary.<br>● PRE-2: The selected Agenda Item belongs to the Meeting and has been started, that is InProgress or Done; a Pending Item has no conclusion. (BR-058, BR-059, BR-066, BR-067)<br>● PRE-3: The Meeting is InProgress, or Ended and its minutes draft has not yet been generated. (BR-066, BR-067) |
| **Postconditions** | ● POST-1: The Item has one conclusion, replacing any earlier one, with the actor and time of the last save recorded. (BR-066, BR-067)<br>● POST-2: Item status, actual times, presenter, documents and position remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the saved conclusion. |
| **Normal Flow** | Record Agenda Item Conclusion<br>1. The actor selects an InProgress or Done Agenda Item and chooses Record conclusion.<br>2. The system displays the current conclusion of the Item, if any, for editing.<br>3. The actor enters or edits the conclusion and saves.<br>4. The system rechecks the Chair or Secretary assignment, the Meeting status and minutes state, the Item status, and that the conclusion is not blank and does not exceed 2,000 characters. (BR-066, BR-067)<br>5. The system saves the conclusion with the actor and the time. (BR-066, BR-067)<br>6. The system displays MSG271 – "Agenda item conclusion saved." Authorized users viewing the Agenda see the conclusion. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Conclusion is blank or too long<br>1. At Step 4, the conclusion is blank or longer than 2,000 characters. (BR-066, BR-067)<br>2. Nothing is saved. The system displays MSG272 – "Enter a conclusion of up to 2,000 characters."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Conclusion can no longer be changed<br>1. At Step 4, the actor is no longer the current Chair or Secretary, the Item is Pending, the Meeting is not InProgress or Ended, or the minutes draft has already been generated. (BR-066, BR-067)<br>2. Nothing is saved; any earlier conclusion remains. The system displays MSG273 – "The conclusion of this agenda item can no longer be changed."<br>3. The use case ends. |

### UC-28 – Take Meeting Notes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to type running notes during the sitting, each attached to the agenda item under discussion or to the Meeting as a whole, so that the minutes are written from what was noted at the time rather than from memory. (BR-134) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-134)<br>● PRE-2: The Meeting is InProgress, or it has Ended and no minutes draft has been built yet. (BR-134) |
| **Postconditions** | ● POST-1: The note of the chosen agenda item, or of the Meeting as a whole, holds the text the actor typed, with the time it was saved.<br>● POST-2: Text that could not reach the system yet stays on the actor's device until it is sent; for a Secret Meeting, only until the page is closed.<br>● POST-3: No agenda item, conclusion, minutes or other Meeting record is changed. |
| **Normal Flow** | Take Meeting Notes<br>1. During the sitting, the actor opens the Notes panel — on the Meeting page or inside the online room, without leaving the call.<br>2. The system loads the actor's notes for the Meeting and opens the note of the agenda item that is InProgress.<br>3. The actor types. A line may start with a mark — Opinion, Conclusion or Task. When the minutes draft is later built without an AI draft (UC-70), each item's note goes under that item in section II, lines marked Conclusion go to section III and lines marked Task go to “Tasks, deliverables and deadlines”. (BR-133)<br>4. Shortly after the actor stops typing, the system checks that the actor is still the current Secretary, that the Meeting still accepts notes, that the item still belongs to the Meeting, that the text is within 20,000 characters and that the note was not saved elsewhere since the actor read it. (BR-134)<br>5. The system saves the note and shows the time it was saved: MSG415 – “Note saved.” |
| **Alternative Flows** | AF-1 – Note on another item or on the Meeting as a whole<br>1. At Step 2 or Step 3, the actor selects another agenda item, or “Chung” [General].<br>2. The system opens the actor's note of that choice.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – The Chair moves to another item<br>1. After Step 2, the Chair sets another agenda item as current (UC-24).<br>2. The system keeps the text already typed with the earlier item and opens the note of the new current item for what is typed next.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – The connection is lost<br>1. At Step 4, the text cannot reach the system.<br>2. The system keeps the text on the actor's device and displays MSG423 – “You are offline. The note is kept on this device and will be saved when the connection returns.”<br>3. When the connection returns, the flow resumes at Step 4 of the Normal Flow.<br>AF-4 – The Chair reads the notes<br>1. At Step 1, the actor is the current Chair.<br>2. The system shows the current Secretary's notes read-only. (BR-134)<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Actor is not the current secretary<br>1. At Step 4, the actor is no longer the current Secretary. (BR-134)<br>2. Nothing is saved; the typed text stays in the panel so it can be copied. The system displays MSG416 – “Only the Secretary may write meeting notes.”<br>3. The use case ends.<br>EX-2 – Meeting no longer accepts notes<br>1. At Step 4, the Meeting is not InProgress, or it has Ended and its minutes draft has been built. (BR-134)<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG417 – “Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.”<br>3. The use case ends.<br>EX-3 – Note was changed in another window<br>1. At Step 4, the note was saved from another window or device after the actor read it.<br>2. Nothing is overwritten. The system shows the latest text next to the actor's unsaved text and displays MSG418 – “This note was changed in another window. Reload to see the latest version.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

---

## FE-06 — Meeting Participant Management

### UC-29 – View Meeting Participants

Sequence Diagram: [UC29_ViewMeetingParticipants.txt](../06-sequencediagram/meeting-participant-management/UC29_ViewMeetingParticipants.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized internal participant or verified Guest to view and search the current Meeting roster, including internal participants and Guests. |
| **Preconditions** | ● PRE-1: The actor has current authorized Meeting participation or verified Guest access.<br>● PRE-2: Roster visibility follows the actor’s current Meeting permissions. (BR-016, BR-017, BR-030, BR-052) |
| **Postconditions** | ● POST-1: The authorized current roster is displayed.<br>● POST-2: No data is changed. |
| **Normal Flow** | View Meeting Participants<br>1. The actor opens the Participants view of an accessible Meeting.<br>2. The system rechecks current Meeting access and roster-view permission. (BR-016, BR-017)<br>3. The system displays current internal participants and Guests with invitation identity and Meeting Role or Guest participation type. Where available, a Guest’s self-declared display name and job title are shown separately from the verified email. A Guest actor sees names and roles but not other people’s emails, only their own. Removed or revoked entries are excluded. (BR-045, BR-016, BR-017)<br>4. The actor reviews the roster. |
| **Alternative Flows** | AF-1 – Search participants<br>1. At Step 1, the actor enters a name or email in the search field; a Guest actor’s search matches names only. (BR-016, BR-017)<br>2. The system filters the current authorized roster to matching entries, ignoring letter case and Vietnamese diacritics.<br>3. The actor reviews the matching entries; the flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Roster is not accessible<br>1. At Step 2, the actor lacks current Meeting access or roster-view permission.<br>2. The system discloses no roster data and treats the Meeting as not found. (BR-017)<br>3. The use case ends. |

### UC-30 – Manage Meeting Participants

#### UC-30a – Add Meeting Participant

Sequence Diagram: [UC30a_AddMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC30a_AddMeetingParticipant.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair |
| **Secondary Actors** | Resend Email |
| **Description** | Allows a current Secretary to add internal participants or invite a Guest to the Meeting. The initial participants of a new Meeting are chosen in the Create Meeting form (UC-13); this use case adds people afterwards. |
| **Preconditions** | ● PRE-1: The actor is a current assigned Secretary or a current Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits roster management. WaitingApproval remains read-only. (BR-036, BR-037, BR-040) |
| **Postconditions** | ● POST-1: The selected internal participants or Guest invitation is saved.<br>● POST-2: New invitations start with Pending response.<br>● POST-3: Guest receives no internal or system Role. (BR-049, BR-050, BR-051) |
| **Normal Flow** | Add Internal Participants<br>1. The actor opens participant management for the Meeting.<br>2. A current Secretary selects one or more active internal users, searching by name or email when needed, and assigns the shared Meeting Role.<br>3. The actor confirms the selection.<br>4. The system rechecks actor authority, Meeting status, selected accounts, uniqueness and required roles. (BR-036, BR-037, BR-038, BR-049, BR-050, BR-051, BR-039, BR-040)<br>5. The system adds the selected participants with a Pending response; either all of them are added or none is. When the Meeting is Published or InProgress, they are invited.<br>6. The system displays MSG96 – “Meeting participant added successfully.” |
| **Alternative Flows** | AF-1 – Invite external Guest<br>1. At Step 2, the Secretary selects Invite Guest and enters the invitation name and email.<br>2. For a Secret Meeting, the system warns that the Meeting is Secret without asking for a further confirmation.<br>3. At Step 4, the system validates the invited email and its uniqueness against current internal participants and Guest invitations.<br>4. At Step 5, the system creates one Guest invitation with a Pending response and, when the Meeting is Published or InProgress, sends it by email. The Guest later verifies the email through UC-04.<br>5. The system displays MSG96 – “Meeting participant added successfully.” The use case ends. (BR-049, BR-050, BR-051, BR-052, BR-053, BR-054, BR-044, BR-055) |
| **Exception Flows** | EX-1 – Participant information or selection is invalid<br>1. At Step 4, an internal account is inactive, already current, or has an invalid Meeting Role; or the Guest name/email is missing, invalid or already represented in the Meeting.<br>2. No participant batch or Guest invitation is partially saved. The system displays MSG97 – “Invalid meeting participant information.” with the applicable field message.<br>3. The actor corrects the selection. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not allowed to manage participants<br>1. At Step 4, the actor can view the Meeting but is not the current Secretary or the current Chair.<br>2. No change is saved. The system displays MSG85 – “You do not have permission to perform this action.” The use case ends.<br>EX-3 – Participant management is no longer permitted<br>1. At Step 4, the Meeting status no longer permits the requested roster action.<br>2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends. |

#### UC-30b – Manage Meeting Role

Sequence Diagram: [UC30b_AssignChangeMeetingRole.txt](../06-sequencediagram/meeting-participant-management/UC30b_AssignChangeMeetingRole.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to change internal Meeting Roles. Before the Meeting is Published, the Secretary may also replace the Chair. From publication onward, only the current Chair can give the Chair role to someone else, through UC-34; the only exception is that the Secretary may replace the Chair when the Chair's account is locked. After publication, the Chair and the Secretary do not swap roles. The Chair and the Secretary may both manage participants and roles while the Meeting status permits roster changes. To change the Secretary, give the Secretary role to the new person, then change the previous Secretary to Member. (BR-041, BR-042) |
| **Preconditions** | ● PRE-1: The actor is a current authorized Secretary or a current authorized Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits participant management, and the selected target is a current internal participant. (BR-036, BR-037) |
| **Postconditions** | ● POST-1: Approved role changes are saved for this Meeting.<br>● POST-2: A Chair replacement changes both affected internal Meeting Roles together. System Role and Invitation Response remain unchanged. (BR-043, BR-041, BR-042) |
| **Normal Flow** | Manage Meeting Role<br>1. The Secretary opens a current participant’s role details.<br>2. The system displays the current Meeting Role.<br>3. The Secretary changes the internal participant’s Meeting Role.<br>4. The Secretary confirms the changes.<br>5. The system rechecks current authority, Meeting status, role constraints and required roles. Draft, Rejected and Approved may have no Secretary; Published and InProgress must retain or replace the last Secretary in the same save. (BR-040, BR-016, BR-017)<br>6. The system saves all requested changes together. When the Meeting is Published or InProgress, the affected participants are notified.<br>7. If no requested change is made, the system displays MSG154 – “No changes to save.” Otherwise it displays MSG100 – “Meeting role updated successfully.” |
| **Alternative Flows** | AF-1 – Replace the chair<br>1. At Step 3, while the Meeting is not yet Published, or once it is Published or InProgress and the Chair's account is deactivated, the Secretary selects a current active internal participant as the new Chair. After publication the selected participant must not be the Secretary. (BR-041, BR-042) Before saving, the system asks for confirmation and names the participant who will become a Member.<br>2. The system changes the former Chair to Member and the selected participant to Chair in the same save, subject to the current Secretary requirements.<br>3. The system notifies the affected participants when required, but never the actor who made the change: a Chair reassigning his or her own role notifies only the new Chair, while a Secretary replacing the Chair notifies both the former and the new Chair. (BR-035) The system displays MSG100 – “Meeting role updated successfully.” The use case ends. (BR-040, BR-041, BR-042) |
| **Exception Flows** | EX-1 – Meeting role is invalid<br>1. At Step 5, the selected Meeting Role is invalid.<br>2. No requested change is saved. The system displays MSG101 – “A valid meeting role is required.”<br>3. The Secretary may correct the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Required meeting role would be lost<br>1. At Step 5, the change would violate the Chair/Secretary requirement for the current Meeting status.<br>2. No role change is saved. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.” The Secretary may revise the change. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Actor, target or meeting is no longer eligible<br>1. At Step 5, the Secretary assignment, target relationship or Meeting status changed after the details were opened.<br>2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends. |

#### UC-30c – Remove Meeting Participant

Sequence Diagram: [UC30c_RemoveMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC30c_RemoveMeetingParticipant.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair |
| **Secondary Actors** | None |
| **Description** | Allows an assigned Secretary to remove a current internal participant or revoke a Guest invitation while preserving required Meeting records. |
| **Preconditions** | ● PRE-1: The actor is a current authorized Secretary or a current authorized Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits participant management. The selected internal participant or Guest invitation is current. (BR-036, BR-037) |
| **Postconditions** | ● POST-1: The selected participant is no longer current, or the Guest invitation is revoked.<br>● POST-2: Access granted by the removed relationship ends, including every active session of a revoked Guest invitation; independent valid access remains effective. A Guest in the online room leaves the room when the invitation is revoked. (BR-053, BR-054)<br>● POST-3: Invitation Response and attendance history are retained. (BR-044, BR-045)<br>● POST-4: Agenda Items presented by the removed participant or revoked Guest no longer have a Presenter. (BR-064, BR-065) |
| **Normal Flow** | Remove Meeting Participant<br>1. The Secretary selects a current internal participant or Guest invitation and confirms removal or revocation.<br>2. The system rechecks the Secretary assignment, Meeting status, selected relationship and required Meeting Roles. The Meeting must retain its required Chair and Secretary; a Secretary may be removed only together with a valid replacement in the same save. (BR-036, BR-037, BR-040, BR-041, BR-042)<br>3. The system removes the internal assignment or revokes the Guest invitation, ends access granted by that relationship, and retains historical evidence.<br>4. When the Meeting is Published or InProgress, the removed person is notified; the notification does not open the Meeting.<br>5. The system displays MSG103 – “Meeting participant removed successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Removal is no longer permitted<br>1. At Step 2, the Meeting status no longer permits roster changes, or the selected relationship is no longer current.<br>2. Nothing is removed or revoked. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends.<br>EX-2 – Removal would violate a required meeting role<br>1. At Step 2, removing the selected participant would leave the Meeting without its required Chair or Secretary and no valid replacement is included in the same save.<br>2. The participant or Guest invitation remains current and access remains effective. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.”<br>3. The Secretary may revise the change to include the required replacement. The flow resumes at Step 1 of the Normal Flow.<br>EX-3 – Actor is not allowed to manage participants<br>1. At Step 2, the actor can view the Meeting but is not the current Secretary or the current Chair.<br>2. Nothing is removed or revoked. The system displays MSG85 – “You do not have permission to perform this action.” The use case ends. |

### UC-31 – Respond to Meeting Invitation

Sequence Diagram: [UC31_RespondToMeetingInvitation.txt](../06-sequencediagram/meeting-participant-management/UC31_RespondToMeetingInvitation.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an invitee other than the Chair to confirm in advance whether they will attend, by accepting or declining their own Meeting invitation. The answer is given once and cannot be changed afterwards. The Chair does not respond because the Chair runs the Meeting. (BR-046) An invitee of a meeting created from a found time slot (UC-81b) accepts or declines it like any other invitation. |
| **Preconditions** | ● PRE-1: The Meeting is Published or InProgress.<br>● PRE-2: The actor owns a current invitation and is not the Chair of the Meeting; a Guest has verified access through UC-04. (BR-046, BR-049, BR-050, BR-051, BR-052, BR-053, BR-054) |
| **Postconditions** | ● POST-1: The response and its response time are recorded once and are not changed afterwards.<br>● POST-2: Meeting Role, attendance and the invitee's access to the Meeting, its documents and its online room are unchanged. A Declined response only hides the Meeting from the invitee's own calendar, and an invitee who has not answered is treated as attending. (BR-043, BR-047, BR-048) |
| **Normal Flow** | Respond to Meeting Invitation<br>1. The invitee, who has not answered yet, sees the attendance question on the Meeting in the calendar and, while the Meeting is Published, on the Meeting details; a verified Guest sees it on the Guest Meeting page. An answer is still accepted while the Meeting is InProgress.<br>2. The system identifies the invitee and offers the question only while the invitation has no answer.<br>3. The invitee selects Accepted, or selects Declined and confirms it. The confirmation states that the answer is given once and cannot be changed and, for an internal invitee, that the Meeting will no longer appear on their calendar. (BR-046, BR-047, BR-048)<br>4. The system rechecks invitation ownership, that the actor is not the Chair, Meeting status and response value, and compares the value with any response already recorded. (BR-046)<br>5. The system saves the response and response time once.<br>6. The system confirms the update with MSG105 – “Meeting invitation response recorded successfully.” |
| **Alternative Flows** | AF-1 – Same response submitted again<br>1. At Step 4, the invitee submits the same response that is already recorded.<br>2. The system keeps the recorded response and response time unchanged and displays MSG154 – "No changes to save." The use case ends. (BR-046)<br>AF-2 – Invitee does not confirm declining<br>1. At Step 3, the invitee selects Declined, then does not confirm it.<br>2. No response is recorded, and the attendance question stays available. The use case ends. |
| **Exception Flows** | EX-1 – Invitation is no longer answerable<br>1. At Step 4, the Meeting status or invitation state no longer permits a response.<br>2. The prior response remains unchanged. For a Guest, email verification and Guest-access failures remain in UC-04.<br>3. The system displays MSG107 – “This meeting invitation can no longer be answered.” The use case ends.<br>EX-2 – Response value is invalid<br>1. At Step 4, the submitted value is not Accepted or Declined.<br>2. The prior response and response time remain unchanged. The system displays MSG106 – “Invalid meeting invitation response.” The invitee may correct the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Different response after answering<br>1. At Step 4, the invitee has already answered and submits a different response. (BR-046)<br>2. The recorded response and response time remain unchanged. The system displays MSG285 – “You have already answered this invitation. Your answer cannot be changed.” The use case ends. |

### UC-32 – View Participant Response Status

Sequence Diagram: [UC32_ViewParticipantResponseStatus.txt](../06-sequencediagram/meeting-participant-management/UC32_ViewParticipantResponseStatus.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows authorized internal participants and verified Guests to view the current invitation responses of the Meeting, shown as a label beside each invitee's name. |
| **Preconditions** | ● PRE-1: The Meeting is Published or a later status that remains readable. A Guest can read it only while the Meeting is Published or InProgress. (BR-055)<br>● PRE-2: The actor can currently view the Meeting. |
| **Postconditions** | ● POST-1: The responses of current invitees are displayed as labels beside their names. The Chair and an invitee who has not answered have no label. No totals are shown.<br>● POST-2: No data is changed. |
| **Normal Flow** | View Participant Response Status<br>1. The actor opens the participant list of an accessible Meeting.<br>2. The system checks that the actor may view the Meeting. (BR-055)<br>3. The system displays each current invitee’s name and email (a Guest actor sees names only, apart from their own email) with a label for the response given, Accepted or Declined; an invitee who has not answered and the Chair have no label. No totals by status are shown.<br>4. No data is changed. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Responses are not accessible<br>1. At Step 2, the actor lacks current Meeting access.<br>2. The system discloses no invitee or response and treats the Meeting as not found. (BR-016, BR-017)<br>3. The use case ends. |

### UC-33 – Record Attendance

Phạm vi implementation: **SCRUM-26**, tách khỏi SCRUM-20. SCRUM-20 vẫn phải giữ lịch sử response/attendance khi xoá/revoke.

Sequence Diagram: [UC33_RecordAttendance.txt](../06-sequencediagram/meeting-participant-management/UC33_RecordAttendance.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to record or revise attendance for one or more selected current internal participants or Guests while the Meeting is InProgress. |
| **Preconditions** | ● PRE-1: The actor is an active current assigned Secretary.<br>● PRE-2: The Meeting is InProgress. (BR-056, BR-057) |
| **Postconditions** | ● POST-1: Submitted people have Present or Absent saved with the recording Secretary and save time.<br>● POST-2: Unsubmitted people retain their saved value or remain unmarked; Invitation Responses are unchanged.<br>● POST-3: All submitted changes succeed together or none are saved. Attendance completeness never gates End Meeting. (BR-043, BR-056, BR-057) |
| **Normal Flow** | Record Attendance<br>1. The Secretary opens attendance for an InProgress Meeting, from the Meeting page or from the online meeting room.<br>2. The system displays current internal participants and Guests with their saved attendance; people never recorded remain unmarked. No Present/Absent totals are shown.<br>3. The Secretary selects Present or Absent for one or more people and submits only those entries.<br>4. The system rechecks current authority, Meeting status, that each submitted person is still current and that their attendance was not changed by someone else since it was shown, and each submitted value. Omitted people are not treated as errors. (BR-056, BR-057)<br>5. The system saves all submitted changes or none of them, records the Secretary and save time, keeps every omitted person’s saved or unmarked value, and displays MSG147 – “Meeting attendance saved successfully.” |
| **Alternative Flows** | AF-1 – Revise saved attendance<br>1. At Step 3, the Secretary changes the saved attendance for one or more current people.<br>2. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Record the full roster<br>1. At Step 3, the Secretary selects every current internal participant and Guest. Recording the full roster is optional; it is not required to save attendance or to end the Meeting.<br>2. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Submitted attendance value is invalid<br>1. At Step 4, a submitted value is not Present or Absent.<br>2. No submitted change is saved. The system displays MSG148 – “Select Present or Absent for each submitted participant or Guest.” The Secretary corrects the value. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Meeting is no longer in progress<br>1. At Step 4, the Meeting changed from InProgress before the submission was saved.<br>2. No submitted change is saved. The system displays MSG149 – “Attendance can only be recorded while the meeting is in progress.” The use case ends.<br>EX-3 – Secretary is no longer authorized<br>1. At Step 4, the actor is no longer the active current assigned Secretary.<br>2. No submitted change is saved. The system denies the operation under current authorization; existing attendance remains unchanged. The use case ends. |

### UC-34 – Hand Over Chair to Another Participant

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair to give the Chair role to another internal participant in one step, for example before leaving the Meeting. The system changes both roles together: the selected participant becomes Chair and the former Chair becomes Member. The Chair does not need to change his or her own role separately. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting.<br>● PRE-2: The Meeting is Published or InProgress. (BR-041, BR-042) |
| **Postconditions** | ● POST-1: The selected participant is the Chair and the former Chair is a Member, saved together. (BR-041, BR-042)<br>● POST-2: The Meeting still has exactly one Chair and its required Secretary. System Roles, Invitation Responses, attendance and recorded ballots remain unchanged. (BR-041, BR-042)<br>● POST-3: The new Chair is notified in the application. The former Chair, who made the change, is not notified in the application but still receives the email. (BR-035)<br>● POST-4: A role change takes effect in the online room without the people rejoining, when the room is open; it is not blocked while the Meeting is in progress. |
| **Normal Flow** | Hand Over Chair to Another Participant<br>1. The Chair opens the Meeting participant list and chooses to change the role of the participant who will receive the Chair role.<br>2. The system displays the Meeting Roles, each with a short description of what the role allows. The Chair role is offered only for a current active internal participant who is neither a Guest nor the current Secretary. (BR-041, BR-042)<br>3. The Chair selects the Chair role and chooses Hand over Chair. The system asks once more, stating that the Chair will become a Member and no longer run the Meeting, and the Chair confirms.<br>4. The system rechecks that the actor is still the current Chair, the Meeting is Published or InProgress, and the selected person can still receive the Chair role. (BR-041, BR-042)<br>5. The system changes the selected participant to Chair and the former Chair to Member in the same save. (BR-041, BR-042)<br>6. The system notifies the new Chair in the application. The former Chair, who made the change, is not notified in the application but still receives the email. (BR-035)<br>7. The system displays MSG257 – "Chair role handed over successfully." The former Chair stays in the Meeting as a Member and may leave the meeting room. (BR-041, BR-042) |
| **Alternative Flows** | AF-1 – No participant can receive the Chair role<br>1. At Step 2, the Meeting has no current active internal participant other than the Chair and the Secretary.<br>2. The system offers the Chair role for no participant. No role changes.<br>3. The use case ends.<br>AF-2 – Chair does not confirm the hand-over<br>1. At Step 3, the Chair does not confirm.<br>2. No role changes and the flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – Selected participant can no longer receive the chair role<br>1. At Step 4, the selected person was removed from the Meeting, became the Secretary or is no longer an active internal user. (BR-041, BR-042)<br>2. No role is changed. The system displays MSG259 – "The selected participant can no longer receive the Chair role. Select another participant."<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Hand-over is no longer permitted<br>1. At Step 4, the actor is no longer the current Chair, or the Meeting is no longer Published or InProgress.<br>2. No role is changed. The system displays MSG260 – "You can no longer hand over the Chair role for this meeting."<br>3. The use case ends. |

## FE-07 — Personal Document Library & Meeting Document Management

ADR-0020 — 15/10/2026: Sixteen detailed Document use cases. UC-36b is reassigned to linking an existing Meeting document to an Agenda Item; UC-36d removes only one Item link. UC-32b remains retired. Meeting-document reading follows Meeting viewing access. Document additions, removals and Item-link changes stop when the Meeting ends; Agenda Item status does not lock document links. PO follow-up: Meeting documents and comments are read-only after the Meeting ends. Deleting an Agenda Item keeps its documents in the Meeting. Readding a removed document does not restore previous comments. Personal-library quotas remain undecided.

SCRUM-21/22/23 — 22/10/2026 (BA, tham khảo AI-eCabinet; PO duyệt và đã đồng bộ Report 3 DOCX cùng ngày): UC-35a shows documents grouped by Agenda Item, with Vote and general groups (AF-1) and the online meeting room's Documents panel (AF-3); UC-35b can be opened from the room without leaving it; UC-38 adds replies (AF-2). UC-35b records views and downloads of Confidential/Secret documents in the audit log (BR-318, from SCRUM-22). BR-315 → BR-317 approved by PO 22/10/2026. UC-36a adds EX-8: at most 10 files and 200 MB per upload (BR-319, MSG288, PO 22/10/2026).

### UC-35 – View Meeting Documents

#### UC-35a – View Meeting Documents

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to browse the documents currently attached to one Meeting. Guest document access follows Participant rules. (BR-055) |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected Meeting exists and the user can currently view the Meeting. (BR-055, BR-079, BR-080, BR-053, BR-054, BR-044) |
| **Postconditions** | ● POST-1: Only currently attached documents available to the user are displayed.<br>● POST-2: Meeting documents, personal libraries and Meeting status remain unchanged. |
| **Normal Flow** | View Meeting Documents<br>1. The user opens the documents of the selected Meeting.<br>2. The system checks the user's current permission to view the Meeting, including the current invitation for a Guest. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. A Guest with a verified, current invitation may read the documents while the Meeting is Published or InProgress (UC-04); once the Meeting has ended, the Guest can no longer open them. (BR-055, BR-079, BR-080)<br>3. The system displays the Meeting documents grouped by Agenda Item, in agenda order, with their names, file types, the people who added them and comment counts. A document linked to several Agenda Items appears under each of them; a document linked to no Agenda Item and to no Vote appears as a general Meeting document, and a document linked to a Vote is listed in the Vote grouping only. Removed documents are excluded. Agenda Items with no documents are listed as well, so a document can still be attached to them (UC-36b). Changes to the document list appear without reloading. After the Meeting ends, documents and comments are read-only. (BR-074, BR-075, BR-076, BR-079, BR-080)<br>4. The user reviews the displayed groups or empty result, and may switch to another grouping or finish viewing. Selecting a document opens UC-35b. |
| **Alternative Flows** | AF-1 – View by Vote or general documents<br>1. At Step 4, the user chooses to view the documents attached to Votes, or the general Meeting documents. The Vote grouping lists, in the order they were created, the Votes the user can see that have documents; a Draft Vote is shown only to the current Chair and Secretary. (BR-086, BR-087)<br>2. The system displays the documents attached to each Vote, each marked Read once the user has confirmed reading it, or Not read while the user can still confirm it as a voter of an Open Vote in an InProgress Meeting (UC-42); or it displays the general documents, which are the documents linked to no Agenda Item and to no Vote, because a document of a Vote is listed in the Vote grouping only.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – No documents to display<br>1. At Step 3, the Meeting has no currently attached documents, or the grouping chosen in AF-1 has no documents.<br>2. The system displays an empty result and explains that no documents are available for the current grouping.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – View documents from the online meeting room<br>1. At Step 1, the user is in the online meeting room of an InProgress Meeting and opens the documents there.<br>2. The system applies the same access check and displays the same documents, starting with the current Agenda Item, then the other Items and the Votes that have documents. Documents cannot be added, linked or removed from the room. (BR-079, BR-080)<br>3. The flow resumes at Step 4 of the Normal Flow; selecting a document opens UC-35b without leaving the room. |
| **Exception Flows** | EX-1 – Meeting documents are not accessible<br>1. At Step 2, the Meeting is unavailable or the user can no longer view the Meeting, including removal of a participant or revocation or expiry of a Guest invitation.<br>2. No document names or contents are disclosed. No Meeting information is changed. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends. |

#### UC-35b – Preview Meeting Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to read a Meeting document online and download the same file when permitted. Comments are covered separately in UC-38. A Guest with a verified, current invitation may read and download documents while the Meeting is Published or InProgress (UC-04), but not after the Meeting has ended. (BR-055, BR-081) |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected document is currently attached to the Meeting and the user can currently view the Meeting. (BR-055, BR-074, BR-075, BR-076, BR-079, BR-080, BR-053, BR-054, BR-044) |
| **Postconditions** | ● POST-1: The selected document is displayed for online reading; in AF-1, the user also receives the permitted download.<br>● POST-2: The file content, its Meeting association and its comments remain unchanged.<br>● POST-3: When the user reaches the last page of a Vote document, reading can be confirmed in UC-42. |
| **Normal Flow** | Preview Meeting Document<br>1. The user selects a document of the Meeting, of an Agenda Item, or in the online meeting room.<br>2. The system checks current permission to view the Meeting and that the document is still attached to this Meeting. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. (BR-055, BR-074, BR-075, BR-076, BR-079, BR-080)<br>3. The system opens the document over the current page and displays its name, its content and the Meeting sensitivity; the user stays on the page and, in the online meeting room, in the call. For a Secret Meeting, every view is recorded in the audit log, and a view that cannot be recorded is not shown. (BR-010)<br>4. The user reads the document. When the user reaches the last page of a Vote document opened from that Vote, reading can be confirmed through UC-42. After the Meeting ends, the document and comments are read-only. Downloading remains available when the Meeting is not Secret. (BR-081, BR-079, BR-080) |
| **Alternative Flows** | AF-1 – Download the document<br>1. At Step 4, the user chooses Download for a Meeting that is not Secret; downloading is not offered for a Secret Meeting.<br>2. The system rechecks current access, that the document remains attached and the current Meeting sensitivity. (BR-055, BR-074, BR-075, BR-076, BR-081, BR-079, BR-080)<br>3. The system provides the same uploaded file to the user. (BR-068, BR-010)<br>4. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Document is no longer accessible<br>1. At Step 2, or during the access check in AF-1, the document was removed, the user can no longer view the Meeting, or the document is linked only to a Draft Vote and to no Agenda Item and the actor is not the current Chair or Secretary. (BR-079, BR-080)<br>2. No further document content or download is provided. The file and Meeting remain unchanged. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Download requested for a Secret Meeting<br>1. During Step 2 of AF-1, the Meeting is Secret, including when its sensitivity changed after the viewer opened.<br>2. No download is provided. The document remains unchanged. The system displays MSG159 – “Secret Meeting documents can only be viewed online and cannot be downloaded.” (BR-081)<br>3. The flow resumes at Step 4 of the Normal Flow for online reading.<br>EX-3 – The stored file no longer exists<br>1. At Step 3, or at Step 3 of AF-1, the document is still attached to the Meeting but its file is no longer in storage.<br>2. Nothing is changed. The system displays MSG382 – “The file of this document is no longer in storage and cannot be opened. Please contact the administrator.” and does not offer to try again.<br>3. The use case ends. |

### UC-36 – Manage Meeting Documents

#### UC-36a – Add Document to Meeting

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to add one or more documents from their own personal library or computer to a Meeting in one submission; files from the computer are added one at a time. Linking documents to Agenda Items is handled separately in UC-36b. (BR-070, BR-071, BR-072, BR-073) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the selected Meeting.<br>● PRE-2: The actor can currently view the Meeting. (BR-070, BR-071)<br>● PRE-3: The Meeting has not ended. (BR-070, BR-071) |
| **Postconditions** | ● POST-1: All submitted documents appear in the Meeting as general documents, with their names copied at addition and the actor and addition time recorded.<br>● POST-2: Selected personal-library documents stay unchanged. Files uploaded from the computer are also available in the uploader's personal library. (BR-072, BR-073)<br>● POST-3: A removed document added again is a new addition without its previous comments. (BR-074, BR-075, BR-076)<br>● POST-4: Each file uploaded from the computer is added as a whole or not at all, and a file that cannot be added does not stop the other files. Documents chosen from the personal library are added together or none are. Existing documents and Meeting status remain unchanged. |
| **Normal Flow** | Add Documents from Personal Library<br>1. The Chair or Secretary opens the Meeting documents and chooses Add Documents.<br>2. The system offers selection from the actor's personal library or upload from the computer.<br>3. The actor selects one or more documents from their own library and submits them.<br>4. The system checks the current Chair or Secretary, Meeting access and status, and that every selected document belongs to the actor, is available and is not already attached to this Meeting. (BR-070, BR-071, BR-072, BR-073)<br>5. The system adds all selected documents to the Meeting under their current names and records who added them and when; either all are added or none is. Personal-library documents stay unchanged; a document added again does not recover old comments. (BR-011, BR-074, BR-075, BR-076)<br>6. The system shows the number added and displays MSG161 – “Documents added to the Meeting successfully.”. |
| **Alternative Flows** | AF-1 – Upload files from the computer<br>1. At Step 3, the actor selects one or more files from the computer: at most 10 files totalling at most 100 MB at once. Each file is added on its own.<br>2. Before reading any file, the system performs the authority, Meeting access and status checks from Step 4 of the Normal Flow. An actor who is not the current Chair or Secretary receives MSG162 – “You no longer have permission to add documents to this Meeting.” whatever the files are. (BR-070, BR-071)<br>3. The system checks that every file is no larger than 10 MB and is a valid PDF or DOCX whose content matches its extension, and identifies any file that fails. (BR-069)<br>4. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it to the Meeting and to the uploader's personal library, recording the number of pages of a PDF. The file goes into a folder named after the Meeting title and planned start date; an existing folder is reused and a deleted one is created again. (BR-072, BR-073)<br>5. A file that cannot be added is added to neither place, and the other files continue; a file that fails the checks of Step 3 follows EX-2. Otherwise, the flow resumes at Step 6 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 4, or at Step 2 of AF-1, the actor can still view the Meeting but is no longer its current Chair or Secretary.<br>2. None of the submitted documents are added. The system displays MSG162 – “You no longer have permission to add documents to this Meeting.”<br>3. The use case ends.<br>EX-2 – Selection is empty, unavailable or already attached, or a file is invalid<br>1. At Step 4, no document was selected, or any selected document is unavailable or does not belong to the actor. Or a selected document is already in the Meeting. Or, in AF-1, a file is missing or invalid.<br>2. None are added. The system identifies the unavailable selections within the actor's library and displays MSG249 – “Select one or more available documents from your personal library.” When a selected document is already in the Meeting, the system displays MSG165 – “One or more selected documents are already attached to the Meeting.” In AF-1, when a file is not a valid PDF or DOCX or is larger than 10 MB, the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.”.<br>3. The actor revises the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting no longer accepts documents<br>1. At Step 4, or at Step 2 of AF-1, the Meeting has ended, including while the actor was selecting or uploading files. (BR-070, BR-071)<br>2. None of the submitted documents are added. The system displays MSG172 – “Documents cannot be added after the Meeting has ended.”<br>3. The use case ends. |

#### UC-36b – Attach Documents to Agenda Item

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to attach one or more documents to an Agenda Item of the Meeting before the Meeting ends. Documents may be chosen from the Meeting documents, uploaded from the computer or chosen from My Documents; documents not yet in the Meeting are added to it and attached in the same save. |
| **Preconditions** | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-070, BR-071)<br>● PRE-2: The selected Agenda Item belongs to the Meeting. (BR-078)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-070, BR-071) |
| **Postconditions** | ● POST-1: The selected documents are attached to the Agenda Item; their links to other Items remain unchanged. An attached document stops being a general Meeting document, and a document whose last Item link is removed through UC-36d becomes general again.<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. A file uploaded from the computer is added and attached on its own, and a file that fails does not stop the others; if a document chosen from My Documents cannot be added or attached, none of that selection is added or attached.<br>● POST-3: Document contents and comments remain unchanged. |
| **Normal Flow** | Attach Documents to Agenda Item<br>1. The actor chooses to attach documents to an Agenda Item.<br>2. The system offers three sources: Meeting documents, Upload from computer and My Documents.<br>3. The actor chooses Meeting documents. The system lists every Meeting document not yet attached to this Item, general documents included.<br>4. The actor selects one or more documents and confirms.<br>5. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the Item belongs to the Meeting, and that every selected document is still in the Meeting and not yet attached to the Item. (BR-070, BR-071, BR-078)<br>6. The system attaches all selected documents to the Item; either all are attached or none is.<br>7. The system displays MSG238 – “Documents attached to the Agenda Item successfully.” |
| **Alternative Flows** | AF-1 – Upload from computer<br>1. At Step 3, the actor chooses Upload from computer and selects one or more files. Each file is added on its own.<br>2. The system checks that at most 10 files totalling at most 100 MB were chosen and that every file is a valid PDF or DOCX no larger than 10 MB, and performs the checks of Step 5 that do not concern existing Meeting documents. (BR-070, BR-071, BR-069)<br>3. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it to the Meeting and to the uploader's personal library and attaches it to the Agenda Item, as a whole or not at all. (BR-072, BR-073)<br>4. If the file is invalid or cannot be added or attached, it is not added or attached and the other files continue; the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.” as in UC-36a EX-2. Otherwise, the flow resumes at Step 7 of the Normal Flow.<br>AF-2 – Choose from My Documents<br>1. At Step 3, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the remaining checks of Step 5. (BR-070, BR-071, BR-072, BR-073)<br>3. The system adds the documents to the Meeting and attaches them to the Agenda Item in the same save. Personal-library documents stay unchanged.<br>4. If any document cannot be added or attached, nothing is added or attached; the system displays MSG249 – “Select one or more available documents from your personal library.” as in UC-36a EX-2. A document already in the Meeting is attached through Step 3. Otherwise, the flow resumes at Step 7 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 5, the actor is no longer the current Chair or Secretary.<br>2. Nothing is attached. The system displays MSG240 – “You no longer have permission to link documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Agenda Item or a document is no longer in the Meeting, or already linked<br>1. At Step 5, a selected document is no longer attached to this Meeting. Or the Agenda Item is no longer in the Meeting, or a selected document is already linked to it.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.” When the Agenda Item is no longer in the Meeting, the system displays MSG242 – “Select an available Agenda Item in this Meeting.”; when a document is already linked, MSG243 – “One or more selected documents are already attached to the Agenda Item.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting no longer accepts document links<br>1. At Step 5, or the corresponding check in AF-1 or AF-2, the Meeting has ended. (BR-070, BR-071)<br>2. Nothing is added or attached. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends. |

#### UC-36c – Remove Document from Meeting

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to remove a Meeting document before the Meeting ends, regardless of Minutes existence or Agenda Item status. Removal affects every Agenda Item link in this Meeting. (BR-070, BR-071, BR-074, BR-075, BR-076) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the Meeting. (BR-070, BR-071)<br>● PRE-2: The selected document is currently attached to this Meeting.<br>● PRE-3: The actor can currently view the Meeting and the Meeting has not ended. (BR-070, BR-071) |
| **Postconditions** | ● POST-1: The document no longer appears in this Meeting's current document list or any of its Agenda Items and is no longer available through this attachment. (BR-074, BR-075, BR-076)<br>● POST-2: Its comments remain in the removal history and do not return if the document is added again; personal-library documents and documents in other Meetings remain unchanged. (BR-074, BR-075, BR-076, BR-011)<br>● POST-3: The actor and removal time are recorded. Meeting lifecycle status is unchanged. |
| **Normal Flow** | Remove Document from Meeting<br>1. The Chair or Secretary selects an attached document and chooses Remove from Meeting.<br>2. The system displays a confirmation naming the document and explaining that it will leave this Meeting's list, while its comments remain as history and personal-library documents and other Meetings are unaffected.<br>3. The actor confirms removal.<br>4. The system rechecks the current Chair or Secretary assignment, that the document is still attached, that the actor can currently view the Meeting and that the Meeting has not ended. Minutes existence and Agenda Item status do not block removal. (BR-070, BR-071)<br>5. The system removes the document from this Meeting's current list and every Agenda Item in this Meeting, preserves its comments as history and records the actor and removal time. (BR-074, BR-075, BR-076, BR-011)<br>6. The system displays MSG167 – “Document removed from the Meeting successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 4, the actor is no longer the current Chair or Secretary. (BR-070, BR-071)<br>2. No document is removed and comments remain unchanged. The system displays MSG169 – “You no longer have permission to remove documents from this Meeting.”<br>3. The use case ends.<br>EX-2 – Document is no longer attached<br>1. At Step 4, the selected document is no longer currently attached to this Meeting.<br>2. No further removal or comment change is made. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The use case ends; the actor may refresh the list through UC-35a.<br>EX-3 – Meeting no longer accepts changes<br>1. At Step 4, the Meeting has ended, including when it ended after the confirmation was displayed. (BR-070, BR-071)<br>2. The document remains attached and its comments remain unchanged. The system displays MSG248 – “Documents cannot be removed after the Meeting has ended.”<br>3. The use case ends. |

#### UC-36d – Detach Document from Agenda Item

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to detach one document from an Agenda Item before the Meeting ends. The document stays in the Meeting; removing it from the Meeting is UC-36c. |
| **Preconditions** | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-070, BR-071)<br>● PRE-2: The document is attached to the selected Agenda Item of the same Meeting. (BR-078)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-070, BR-071) |
| **Postconditions** | ● POST-1: Only the selected link is removed; the document remains in the Meeting and any other Agenda Item links remain unchanged. With no links left, it is a general Meeting document.<br>● POST-2: The document, its comments and its other Agenda Item links remain unchanged. |
| **Normal Flow** | Detach Document from Agenda Item<br>1. The actor selects a document attached to an Agenda Item, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the document is still in the Meeting, that the Item belongs to the Meeting, and that the link still exists. (BR-070, BR-071, BR-078)<br>3. The system removes only that link; the document stays in the Meeting with its other links unchanged, or becomes a general Meeting document when no links remain.<br>4. The system refreshes the Agenda Item documents and displays MSG239 – “Document detached from the Agenda Item successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 2, the actor is no longer the current Chair or Secretary.<br>2. No links are changed. The system displays MSG241 – “You no longer have permission to unlink documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Document, Agenda Item or link no longer exists<br>1. At Step 2, the link has already been removed.<br>2. No further links are changed and the document remains in the Meeting. The system displays MSG246 – “This document is no longer linked to the selected Agenda Item. Refresh the document list.”<br>3. The use case ends.<br>EX-3 – Meeting no longer accepts changes<br>1. At Step 2, the Meeting has ended, including when it ended after selection. (BR-070, BR-071)<br>2. No links are changed. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends. |

### UC-37 – View Personal Document Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to browse their own folders and documents and see permitted Meeting-usage details. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |
| **Postconditions** | ● POST-1: The user sees only their own library items and permitted Meeting-usage details.<br>● POST-2: Documents, folders and Meeting attachments remain unchanged. |
| **Normal Flow** | View Personal Document Library<br>1. The user opens Personal Document Library.<br>2. The system checks personal-library access. (BR-055, BR-083)<br>3. The system displays the user's folders and documents with names, file types and permitted Meeting-usage details. Other users' items and unauthorized Meeting details are excluded. (BR-083)<br>4. The user selects a folder or the library root.<br>5. The system displays the documents at that location. The user may browse the list or empty result, change the location or finish viewing. |
| **Alternative Flows** | AF-1 – Search or filter documents<br>1. At Step 5, the user enters a document-name search or selects a file-type filter.<br>2. The system applies the criteria within the user's own displayed library location, matching document and folder names without regard to case or Vietnamese marks.<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Empty library or no matching documents<br>1. At Step 3 or Step 5, no documents match the location and criteria.<br>2. The system displays an empty result without treating it as a loading failure.<br>3. The flow resumes at Step 5 of the Normal Flow. |
| **Exception Flows** | EX-1 – Selected folder is no longer available<br>1. At Step 4, the folder was deleted or is no longer available in the user's library.<br>2. No information changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The user returns to the library root. The flow resumes at Step 4 of the Normal Flow. |

#### UC-37a – View a Deactivated Account’s Personal Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows a System Admin to read, without changing anything, the folders and documents in the personal library of a deactivated account, for example to recover Meeting materials after the owner has left the organization. Every read is recorded in the audit log. The Admin does not see the files of Secret Meetings. (BR-083, BR-085, BR-011) |
| **Preconditions** | ● PRE-1: The System Admin is signed in and has the capability to manage users.<br>● PRE-2: The selected account exists and is deactivated. The library of an active account is private to its owner. (BR-083, BR-085) |
| **Postconditions** | ● POST-1: The System Admin has seen the folders and documents of the deactivated account’s library that BR-085 allows. Nothing in the library has changed. (BR-085)<br>● POST-2: Each read has an audit entry stating who read, whose library, which folder or document and when. (BR-011) |
| **Normal Flow** | View a Deactivated Account’s Personal Library<br>1. The System Admin opens the user list and chooses to view the personal library of a deactivated account. The library of an active account cannot be opened. (BR-085)<br>2. The system rechecks the System Admin’s capability and that the account is still deactivated, and that the selected folder, if any, is in that library and is not hidden by BR-085. (BR-085)<br>3. The system records the read in the audit log before showing anything. (BR-011)<br>4. The system displays the folders and documents of the account’s library at the root, read-only. The files of Secret Meetings and each such Meeting’s upload folder with its subfolders are left out. (BR-085)<br>5. The System Admin opens a folder. The system repeats Steps 2 and 3 for that read and displays the folder’s contents. |
| **Alternative Flows** | AF-1 – Read a document<br>1. At Step 4 or 5, the System Admin chooses a document to preview or download.<br>2. The system repeats the checks of Step 2, opens the file, records an audit entry naming the document and only then returns the file read-only. (BR-085, BR-011)<br>3. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Account is missing or is not deactivated<br>1. At Step 2, the account is no longer deactivated (or was reactivated while viewing), or the selected folder or document does not exist in that library or is hidden by BR-085.<br>2. Nothing is shown. The system displays MSG175 – “This personal-library item is not available.” and navigates back to the user list if the account was reactivated, or returns to the library root if a folder was deleted or hidden.<br>3. The use case ends. |

### UC-38 – Comment on Meeting Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a user who can view a Meeting document to read its comments and add a comment before the Meeting ends. After the Meeting ends, the document and comments are read-only. (BR-082, BR-079, BR-080) |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a current valid invitation. (BR-053, BR-054, BR-044)<br>● PRE-2: The document is currently in the Meeting and the user can view that Meeting. (BR-079, BR-080)<br>● PRE-3: The Meeting has not ended. (BR-079, BR-080) |
| **Postconditions** | ● POST-1: The new comment is displayed with its author and posting time, and the comment count is updated.<br>● POST-2: Existing comments and the document remain unchanged. |
| **Normal Flow** | Comment on Meeting Document<br>1. The user opens a Meeting document and its comments.<br>2. The system checks viewing access and displays the existing comments. (BR-079, BR-080)<br>3. The user enters a comment and chooses Send.<br>4. The system checks that the user may still view the document, the Meeting has not ended, and the comment is not blank. (BR-082, BR-079, BR-080)<br>5. The system records the comment with its author and posting time; a Guest author is recorded by their invitation, shown with the name they entered. A PDF comment may refer to a page; a DOCX comment has no page reference.<br>6. The system displays the new comment, updates the comment count and displays MSG199 – “Comment added successfully.” |
| **Alternative Flows** | AF-1 – Include a PDF page reference<br>1. At Step 3, the user comments on a page being viewed in a PDF.<br>2. The comment includes that page reference; a page reference is not required for a document-wide comment.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Reply to a comment<br>1. At Step 3, the user chooses Reply on an existing comment and enters the reply.<br>2. The reply is attached to the top-level comment of that thread in the same Meeting document; replying to a reply joins the same thread.<br>3. The flow resumes at Step 4 of the Normal Flow; the reply is shown under its thread. |
| **Exception Flows** | EX-1 – Comment is empty or too long<br>1. At Step 4, the text is blank or contains only whitespace.<br>2. No comment is saved. The system displays MSG200 – “Enter a comment.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Document is no longer available<br>1. At Step 2 or Step 4, the document is no longer attached, or the user can no longer view it, including after Guest invitation revocation or expiry.<br>2. No comment is saved. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-3 – Meeting no longer accepts comments<br>1. At Step 2 or Step 4, the Meeting has ended, including when it ended while the user was writing. (BR-079, BR-080) A Guest loses all access to the Meeting when it ends, so EX-2 applies to a Guest instead. (BR-055)<br>2. No comment is added. Existing comments remain available for reading. The system displays MSG251 – “Comments are read-only after the Meeting has ended.”<br>3. The use case ends. |

### UC-39 – Upload Document to Personal Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to upload one or more PDF or DOCX files to the selected location in their personal library. Uploading to the library does not add them to a Meeting. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |
| **Postconditions** | ● POST-1: Each uploaded file is available as a separate document at the selected library location. (BR-068)<br>● POST-2: A file that cannot be added is not added at all, and does not stop the other files. Existing library documents and Meeting documents remain unchanged. |
| **Normal Flow** | Upload Documents to Personal Library<br>1. The user opens their library root or a folder and chooses Upload.<br>2. The user selects one or more files from the computer: at most 10 files totalling at most 100 MB at once, within the space left in the system's document storage.<br>3. As the files are chosen, the system checks that each one is a PDF or DOCX file, not empty and no larger than 10 MB. (BR-069)<br>4. The user confirms the upload. Each valid file is uploaded on its own.<br>5. For each file, the system checks library access, the selected destination and that the file is a valid PDF or DOCX whose content matches its extension; a PDF must open without a password and contain at least one page. (BR-069, BR-083)<br>6. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it as a separate document at that location with its original name and content. (BR-068)<br>7. The system shows the number added and displays MSG177 – “Documents uploaded to your personal library successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Some files are invalid<br>1. At Step 3, one or more files are not PDF or DOCX, are empty or are larger than 10 MB; or, at Step 5, the content of a file is not a valid PDF or DOCX. (BR-069)<br>2. The invalid files are not added and are named; the valid files are still uploaded. The system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.”.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – Too many files or too large a selection<br>1. At Step 2, more than 10 files, or more than 100 MB in total, are chosen at once.<br>2. The whole selection is blocked and no file is uploaded. The system displays MSG288 – “Upload up to 10 files at a time, no larger than 100 MB in total.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Destination folder is no longer available<br>1. At Step 5, the selected folder was deleted or does not belong to the user. (BR-083)<br>2. No file is added to that folder. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-4 – System storage is full<br>1. At Step 2, the chosen files do not fit in the space left in the system's document storage; or, at Step 6, adding a file would take the total storage of all documents in the system above 250 GB.<br>2. At Step 2 the whole selection is blocked; at Step 6 that file is not added. Existing library and Meeting documents remain unchanged. The system displays MSG346 – “The system’s document storage is full. No documents were uploaded. Please contact an administrator.”<br>3. The use case ends. |

### UC-40 – Manage Personal Documents

#### UC-40a – Delete Document from Personal Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to delete a document from their personal library without removing documents already attached to Meetings. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The selected document is in the user's library. |
| **Postconditions** | ● POST-1: The document no longer appears in the personal library.<br>● POST-2: Documents already added to Meetings remain available to their authorized readers. Their history and comments are preserved. (BR-074, BR-075, BR-076, BR-077) |
| **Normal Flow** | Delete Document from Personal Library<br>1. The user selects a personal-library document and chooses Delete.<br>2. The system requests confirmation, naming the document and explaining that deletion affects the personal library only.<br>3. The user confirms deletion.<br>4. The system rechecks ownership and that the library document still exists. (BR-083)<br>5. The system deletes the personal-library document without removing Meeting attachments. (BR-074, BR-075, BR-076)<br>6. The system refreshes the library and displays MSG180 – “Document deleted from your personal library successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No library document is deleted; Meeting documents remain unchanged. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends. |

#### UC-40b – Rename Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows the owner to rename a personal-library document without changing its file content. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The document exists in the user's library. |
| **Postconditions** | ● POST-1: The document is displayed under the new name in the personal library.<br>● POST-2: File content and library location remain unchanged. Names already copied into Meetings remain unchanged. (BR-068) |
| **Normal Flow** | Rename Document<br>1. The user chooses Rename for a personal-library document.<br>2. The system displays its current name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership and document availability. (BR-083)<br>5. The system saves the new personal-library document name. File content and names already copied into Meetings remain unchanged. (BR-068)<br>6. The system refreshes the displayed name and displays MSG183 – “Document renamed successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No document name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Document name is invalid<br>1. At Step 4, the entered name is empty, contains only whitespace, contains only an extension, contains control characters, exceeds 255 characters, or changes the original file extension (.pdf/.docx).<br>2. No document name changes. The system displays MSG347 – “The document name is not valid. It must not be blank and must keep the .pdf or .docx extension of the file.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

#### UC-40c – Move Document to Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows the owner to move a personal-library document to another owned folder or the library root. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The document exists in the user's library. |
| **Postconditions** | ● POST-1: The document is available at the selected library location.<br>● POST-2: Document name, file content and Meeting attachments remain unchanged. |
| **Normal Flow** | Move Document to Folder<br>1. The user chooses Move for a personal-library document.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system rechecks document ownership and availability and destination ownership and availability. (BR-083)<br>5. The system moves only the library document to that location.<br>6. The system refreshes the library and displays MSG185 – “Document moved successfully.” |
| **Alternative Flows** | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of a folder.<br>2. The document is moved outside all folders.<br>3. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. The document remains at its previous location. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Destination is missing or does not belong to the actor<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-083)<br>2. The document stays at its previous location. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

### UC-41 – Manage Library Folders

#### UC-41a – Create Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to create a folder at the library root or inside an owned folder. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |
| **Postconditions** | ● POST-1: A new owned folder is available at the selected location.<br>● POST-2: Existing folders, documents and Meeting attachments remain unchanged. |
| **Normal Flow** | Create Folder<br>1. The user selects the library root or an owned parent folder and chooses New Folder.<br>2. The user enters a folder name and submits it.<br>3. The system checks ownership, parent availability, a nonblank name and a resulting depth of at most five levels. Duplicate folder names are permitted. (BR-083)<br>4. The system creates the folder at that location.<br>5. The system refreshes the folder list and displays MSG187 – “Folder created successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder name is blank or too long<br>1. At Step 3, the name is empty or contains only whitespace.<br>2. No folder is created. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Parent folder is missing or not owned by the user<br>1. At Step 3, the selected parent was deleted or does not belong to the user. (BR-083)<br>2. No folder is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-3 – New folder would be deeper than level five<br>1. At Step 3, the new folder would be deeper than level five.<br>2. No folder is created. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The user selects a shallower parent. The flow resumes at Step 1 of the Normal Flow. |

#### UC-41b – Rename Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows the owner to rename a folder without moving it or changing its contents. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists in the user's library. |
| **Postconditions** | ● POST-1: The folder displays the new name.<br>● POST-2: Its location and contents remain unchanged; an automatic Meeting folder remains reusable. |
| **Normal Flow** | Rename Folder<br>1. The user chooses Rename for an owned folder.<br>2. The system displays the current folder name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership, availability and a nonblank name; duplicate names are permitted. (BR-083)<br>5. The system saves the new folder name without changing its contents or location.<br>6. The system refreshes the folder name and displays MSG191 – “Folder renamed successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder is missing or not owned by the user<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder name is blank<br>1. At Step 4, the name is empty or contains only whitespace.<br>2. The previous name remains. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Folder name exceeds 400 characters<br>1. At Step 4, the entered folder name exceeds 400 characters.<br>2. The previous name remains. The system displays MSG348 – “The text is longer than allowed.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

#### UC-41c – Move Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows the owner to move a folder with its contents to another owned folder or the library root. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists in the user's library. |
| **Postconditions** | ● POST-1: The folder and its contents are available at the selected location, within five levels.<br>● POST-2: Folder and document names, file content and Meeting attachments remain unchanged. An automatic Meeting folder remains reusable. |
| **Normal Flow** | Move Folder<br>1. The user chooses Move for an owned folder.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system checks that the user owns the selected folder and destination and that both are available. The destination cannot be the selected folder or one of its subfolders, and the moved folder and its subfolders must remain within five levels. (BR-083, BR-084)<br>5. The system moves the folder with its documents and subfolders, without changing their names or Meeting attachments.<br>6. The system refreshes the folder tree and displays MSG194 – “Folder moved successfully.” |
| **Alternative Flows** | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of another folder.<br>2. The moved folder becomes a first-level folder; its subfolders remain subject to the depth limit.<br>3. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Folder or destination is missing or not owned by the user<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-083)<br>2. No folder or contained item moves. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Destination is the folder itself or one of its subfolders<br>1. At Step 4, the destination is the folder itself or one of its subfolders. (BR-084)<br>2. No folder or contained item moves. The system displays MSG193 – “A folder cannot be moved into itself or one of its descendants.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Moved folders would be deeper than level five<br>1. At Step 4, the moved folder or one of its subfolders would be deeper than level five.<br>2. No folder or contained item moves. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

#### UC-41d – Delete Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows the owner to delete an empty personal-library folder. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists and contains no documents or subfolders. (BR-084) |
| **Postconditions** | ● POST-1: The empty folder no longer appears in the library.<br>● POST-2: Other folders, documents and Meeting attachments remain unchanged. |
| **Normal Flow** | Delete Folder<br>1. The user chooses Delete for an owned folder.<br>2. The system asks for confirmation and names the folder.<br>3. The user confirms deletion.<br>4. The system rechecks current ownership and availability and that the folder contains neither documents nor subfolders. (BR-083, BR-084)<br>5. The system deletes only the empty folder.<br>6. The system refreshes the folder tree and displays MSG197 – “Folder deleted successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder is missing or not owned by the user<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder is deleted. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder contains a document or subfolder<br>1. At Step 4, the folder contains a document or subfolder, including an item added after confirmation was displayed. (BR-084)<br>2. The folder and its contents remain unchanged. The system displays MSG196 – “Only an empty folder can be deleted.”<br>3. The use case ends. |

### UC-42 – Confirm Document Read

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person who may vote to confirm that he or she has read a document linked to a Vote of the Meeting, after reaching its last page. The confirmation is required before casting a ballot in UC-65. It records that the person confirmed reading, not how carefully the document was read. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor can currently view the Meeting, the document is attached to the Meeting and linked to a Vote of that Meeting, and the actor is on the eligible-voter list of an Open Vote linked to that document. (BR-055, BR-079, BR-080, BR-091)<br>● PRE-3: The actor has opened the document in the viewer through UC-35b. |
| **Postconditions** | ● POST-1: A read confirmation for the actor and the document is recorded with the confirmation time.<br>● POST-2: The confirmation counts for every Vote of the Meeting that links the same document and cannot be withdrawn.<br>● POST-3: No ballot, Vote or document content changes, and the confirmation is not linked to any ballot choice. (BR-096, BR-097) |
| **Normal Flow** | Confirm Document Read<br>1. From the Vote documents or the Meeting documents, the actor opens a Vote document through UC-35b.<br>2. The actor reads the document through to its last page.<br>3. Once the last page is reached, the system lets the actor confirm reading.<br>4. The actor confirms reading.<br>5. The system rechecks current Meeting access, that the document is still attached to the Meeting and linked to a Vote, and that the actor reached the last page. (BR-055, BR-079, BR-080)<br>6. The system records the read confirmation with the actor, the document and the confirmation time. (BR-096, BR-097)<br>7. The system displays MSG253 – "Document read confirmed." and shows the document as read. When every document of an Open Vote is confirmed, the actor may cast a ballot in UC-65. (BR-094, BR-095) |
| **Alternative Flows** | AF-1 – Document already confirmed<br>1. At Step 1, the actor has already confirmed reading this document, including for another Vote of the Meeting that links it.<br>2. The system shows the document as read with the time of the existing confirmation, and does not offer confirmation again.<br>3. The use case ends without a new confirmation. |
| **Exception Flows** | EX-1 – Last page not reached<br>1. At Step 4 or Step 5, the actor has not reached the last page, including when the viewer was closed before the last page.<br>2. No confirmation is recorded. The system displays MSG254 – "Scroll to the last page of the document before confirming that you have read it."<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Document or Meeting is no longer accessible<br>1. At Step 5, the actor can no longer view the Meeting, or the document is no longer attached to the Meeting or linked to a Vote.<br>2. No confirmation is recorded. The system displays MSG156 – "Meeting documents are not accessible."<br>3. The use case ends.<br>EX-3 – Not an eligible voter, or no Open Vote remains<br>1. At Step 5, the actor is not on the fixed eligible-voter list of any Open Vote linked to this document. Or no Vote linked to this document is Open any more.<br>2. No confirmation is recorded. The system displays MSG224 – "You are not eligible to cast a ballot in this vote." When no Open Vote remains, the system displays MSG225 – “This vote is no longer accepting ballots.”<br>3. The use case ends. |

## FE-08 — Online Meeting Room Management

### UC-43 – Join Meeting Room

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person who currently holds access to an In Progress Online or Hybrid Meeting to check the camera and microphone in the lobby and enter its online meeting room. Joining does not start or change the Meeting. A person who opens the room before the Meeting starts waits in the lobby until it starts. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation. (BR-052, BR-053, BR-054, BR-044, BR-055)<br>● PRE-2: The Meeting is In Progress and its mode is Online or Hybrid. (BR-025, BR-055)<br>● PRE-3: The actor holds a current participant assignment of the Meeting, or a valid Guest invitation for it. Being the Member who created the Meeting or its Approver alone is not enough. (BR-044, BR-100)<br>● PRE-4: Fewer than 50 people are connected to the room. |
| **Postconditions** | ● POST-1: The actor is in the meeting room and appears in the Participants panel of everyone in the room. (BR-101)<br>● POST-2: The Meeting lifecycle, its roster, its roles and its attendance are unchanged; presence in the room is never recorded as attendance. (BR-101)<br>● POST-3: The actor stays in the room until leaving (UC-44) or until the Meeting ends.<br>● POST-4: A Guest who joins from two devices is in the room twice, and revoking the invitation removes both. The Guest's confirmation of the recording notice is recorded in the audit log. |
| **Normal Flow** | Join Meeting Room<br>1. On the Meeting detail of an In Progress Online or Hybrid Meeting, the actor chooses to join the online room.<br>2. The system checks that the actor may still view the Meeting, that it is In Progress, that it has an online room and that the actor may enter the room. (BR-044, BR-025, BR-100)<br>3. The system opens the Meeting Lobby with a camera preview; the microphone is on and the camera is off by default. When recording applies to the Meeting, the lobby tells the actor that the meeting is recorded for the minutes and that the actor's own speech is recorded while the microphone is on. (UC-45a, UC-53)<br>4. The actor checks the preview and may choose other devices (UC-45d) or turn the microphone or camera on or off (UC-45a, UC-45b).<br>5. The actor chooses to join the room. A Guest first confirms the recording notice, and the system records that confirmation in the audit log.<br>6. The system checks again that the actor may enter the room and lets the actor in with the actor's current Meeting Role. (BR-100)<br>7. The system shows the online meeting room with the people already in it; no separate success message is displayed. (UC-46) |
| **Alternative Flows** | AF-1 – Rejoin after a lost connection<br>1. At Step 1, the actor returns to the room after the connection was lost while the Meeting is still In Progress.<br>2. The system offers to rejoin; no new room and no new Meeting are created. (BR-101)<br>3. The flow resumes at Step 6 of the Normal Flow.<br>AF-2 – Join from a Guest invitation<br>1. At Step 1, the Guest continues from the verified invitation instead of the Meeting detail.<br>2. The system checks the invitation and that the Meeting is In Progress. (BR-052, BR-053, BR-054, BR-044, BR-055)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Opened before the Meeting starts<br>1. At Step 2, the Meeting is Published and has not been started.<br>2. The system shows the waiting lobby with MSG117 – “The meeting room opens only while the meeting is in progress.” and does not let the actor join yet. (BR-025)<br>3. When the Chair or the Secretary starts the Meeting (UC-20), the lobby opens and the flow resumes at Step 3 of the Normal Flow.<br>AF-4 – The browser blocks audio playback<br>1. At Step 7, the browser does not play sound until the actor interacts with the page.<br>2. The system displays MSG338 – “The browser is blocking audio playback.” and asks the actor to allow sound.<br>3. The actor allows sound and the flow resumes at Step 7 of the Normal Flow. |
| **Exception Flows** | EX-1 – Meeting has ended or was cancelled<br>1. At Step 2 or Step 6, the Meeting has ended, including while the actor was in the lobby, or it was cancelled. (BR-028)<br>2. The actor does not enter the room. When the Meeting has ended, the system displays MSG331 – “The meeting has ended, the meeting room is closed.” and offers a way back to the Meeting page (BR-025); a Guest sees the end screen of UC-04 instead. When the Meeting was cancelled, the system displays MSG350 – “The meeting was cancelled, so the meeting room does not open.”<br>3. The use case ends.<br>EX-2 – Meeting is held in person<br>1. At Step 2, the Meeting mode is In Person, so it has no online room.<br>2. No lobby is opened. The system displays MSG280 – “This meeting is held in person, so it has no online meeting room.”<br>3. The use case ends.<br>EX-3 – The Guest session ended while waiting or joining<br>1. At Step 2 or Step 5, the Guest session expired.<br>2. The Guest does not enter the room, the Meeting data is removed from the screen and the Guest returns to the verification screen with MSG378 – “The guest session has ended.” The invitation is still valid, so the Guest may verify again; when the invitation was revoked, UC-04 applies instead.<br>3. The use case ends.<br>EX-4 – Recording notice not confirmed<br>1. At Step 5, the Guest has not confirmed the recording notice.<br>2. The Guest does not enter the room. The system displays MSG380 – “Confirm that you have read the recording notice before joining the meeting room.”<br>3. The flow resumes at Step 5 of the Normal Flow. |

### UC-44 – Leave Meeting Room

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person connected to the online meeting room to disconnect. Leaving never changes the Meeting lifecycle: the Meeting stays In Progress for everyone else, and the person may join again while it is In Progress. Only the Chair ends the Meeting, through UC-21. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of the Meeting. (UC-43)<br>● PRE-3: The Meeting is In Progress. |
| **Postconditions** | ● POST-1: The actor is disconnected and no longer appears in the Participants panel. (BR-101)<br>● POST-2: The Meeting stays In Progress with its roster, roles, attendance and recorded ballots unchanged. (BR-101)<br>● POST-3: The actor may join again through UC-43 while the Meeting is In Progress. |
| **Normal Flow** | Leave Meeting Room<br>1. The actor chooses to leave the room.<br>2. The system asks the actor to confirm, reminding that the meeting continues with the others and that the actor can rejoin while it is in progress.<br>3. The actor confirms.<br>4. The system disconnects the actor, stops the camera and microphone and shows the Meeting detail.<br>5. The Meeting stays In Progress for everyone still connected; no separate success message is displayed. (BR-101) |
| **Alternative Flows** | AF-1 – Stay in the room<br>1. At Step 2, the actor chooses to stay.<br>2. No connection is closed and the flow resumes at Step 1 of the Normal Flow. |
| **Exception Flows** | EX-1 – Meeting has ended while in the room<br>1. At Step 1, the Chair ended the Meeting (UC-21) and the room closed.<br>2. The actor is disconnected and cannot rejoin. The system displays MSG331 – “The meeting has ended, the meeting room is closed.” A Guest sees the end screen of UC-04 instead.<br>3. The use case ends.<br>EX-2 – Same account joined elsewhere<br>1. At Step 1, the actor joins the room from another tab or device.<br>2. The earlier connection is closed and displays MSG329 – “You joined this room from another tab or device, so this tab left the room.” It may rejoin, which closes the newer connection in turn.<br>3. The use case ends.<br>EX-3 – Actor is no longer on the participant list<br>1. At Step 1, the actor was removed from the roster or the Guest invitation was revoked while connected.<br>2. The actor is disconnected and cannot rejoin. The system displays MSG330 – “You are no longer on the participant list of this meeting room.” A Guest whose invitation was revoked sees the end screen of UC-04 instead.<br>3. The use case ends. |

### UC-45 – Manage Meeting Room Controls

_UC gom — đặc tả nằm ở các UC con UC-45a, UC-45b… bên dưới._

#### UC-45a – Manage Microphone

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person in the Meeting Lobby or in the meeting room to turn his or her own microphone on or off. It changes only that person's microphone: turning another person's microphone off is UC-47. While recording applies to the Meeting, the actor's own speech is recorded while the microphone is on (AF-2). For the Secretary of a Hybrid Meeting the microphone is the room microphone, so turning it on or off turns the room recording on or off (UC-52a). |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser reports at least one microphone. |
| **Postconditions** | ● POST-1: The actor's microphone is on or off as chosen, for that actor only.<br>● POST-2: The on/off state is remembered for the next visit on that device.<br>● POST-3: No other person's audio, the Meeting lifecycle and attendance are unchanged.<br>● POST-4: While recording applies to the Meeting, a microphone that is on means the actor’s own speech is recorded for the minutes; turning it off stops it. (AF-2, UC-53, BR-113) |
| **Normal Flow** | Manage Microphone<br>1. The actor turns his or her microphone on or off in the lobby or in the room.<br>2. The system turns the microphone on or off and shows the new state to the actor and to everyone in the room; no separate message is displayed. (UC-46)<br>3. The system remembers the new state for the next visit on this device. |
| **Alternative Flows** | AF-1 – Choose another microphone<br>1. At Step 1, the actor chooses another microphone through UC-45d.<br>2. The system switches to that microphone, keeps the on/off state and remembers the device.<br>3. The flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Speech recorded while the microphone is on<br>1. At Step 2, the actor turns the microphone on in an Online or Hybrid Meeting that is In Progress. This applies to a Guest with a valid invitation as well, once the Guest has confirmed the recording notice. It does not apply to the Secretary of a Hybrid Meeting, whose microphone records the whole room (UC-52a). (BR-107, BR-113, BR-111, BR-112)<br>2. While the microphone is on, the system records the actor’s speech sentence by sentence as text, with its time and the actor’s name. (BR-113)<br>3. A sentence that repeats a neighbouring person’s speech picked up by the actor’s microphone is marked as overlapping, not hidden.<br>4. The actor can review, play and delete his or her saved sentences in My Recorded Speech (UC-53); they are kept for the retention period. (BR-110, BR-113)<br>5. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – Browser blocks the microphone<br>1. At Step 2, the browser has not granted microphone permission.<br>2. The microphone stays off and the actor stays in the room and can still hear everyone. The system displays MSG334 – “The browser is blocking the {device}. Click the lock icon beside the address bar, choose Allow and reload the page. You can still join the room to listen.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – No microphone is available<br>1. At Step 1, the browser reports no microphone, or the chosen microphone was disconnected.<br>2. The microphone cannot be chosen and the actor can still join the room to listen. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The use case ends.<br>EX-3 – Meeting cannot be recorded now<br>1. At Step 2 of AF-2, the Meeting is held in person, has not started, or ended more than 7 days ago. (BR-107)<br>2. The sentence is not kept. The system displays MSG320 – “In-person meetings do not record per-mic speech, or the meeting is no longer running.”<br>3. The use case ends. |

#### UC-45b – Manage Camera

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person in the Meeting Lobby or in the meeting room to turn his or her own camera on or off. It changes only that person's camera. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser reports at least one camera. |
| **Postconditions** | ● POST-1: The actor's camera is on or off as chosen, for that actor only; the camera is off when the actor first joins.<br>● POST-2: The on/off state is remembered for the next visit on that device.<br>● POST-3: No other person's video, the Meeting lifecycle and attendance are unchanged. |
| **Normal Flow** | Manage Camera<br>1. The actor turns his or her camera on or off in the lobby or in the room.<br>2. The system turns the camera on or off; while it is off, everyone in the room sees the actor's initials instead of a video image. No separate message is displayed. (UC-46)<br>3. The system remembers the new state for the next visit on this device. |
| **Alternative Flows** | AF-1 – Choose another camera<br>1. At Step 1, the actor chooses another camera through UC-45d.<br>2. The system switches to that camera, keeps the on/off state and remembers the device.<br>3. The flow resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – The browser blocks the camera<br>1. At Step 2, the browser has not granted camera permission.<br>2. The camera stays off and the actor stays in the room with audio. The system displays MSG334 – “The browser is blocking the camera. Click the lock icon beside the address bar, choose Allow and reload the page.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – No camera is available<br>1. At Step 1, the browser reports no camera, or the chosen camera was disconnected.<br>2. The camera cannot be chosen and the actor can still join the room to listen. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The use case ends. |

#### UC-45c – Manage Screen Sharing

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person connected to the room to present a screen, window or tab to everyone and to stop presenting. One person presents at a time: when someone else starts, the previous presentation stops. Nothing is recorded; the shared content exists only while the share lasts. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The device can share a screen: the browser offers screen capture and the device is not touch-only. |
| **Postconditions** | ● POST-1: The actor's screen is shown on the main stage of everyone in the room, or the presentation has stopped.<br>● POST-2: At most one person is presenting at any time.<br>● POST-3: Nothing is recorded or stored; the Meeting and its attendance are unchanged. |
| **Normal Flow** | Manage Screen Sharing<br>1. The actor chooses to share the screen.<br>2. The actor picks a screen, window or tab and confirms.<br>3. The system shows the shared content on the main stage for everyone in the room and tells the actor that he or she is presenting to everyone.<br>4. The actor stops sharing.<br>5. The system ends the presentation and returns everyone to the video grid; no separate message is displayed. |
| **Alternative Flows** | AF-1 – Someone else starts presenting<br>1. At Step 3, while the actor is presenting, another person starts a share.<br>2. The system stops the actor's presentation and keeps the new one.<br>3. The system displays MSG340 – “{name} started presenting, so your screen share was stopped.” and the flow resumes at Step 3 of the Normal Flow.<br>AF-2 – The device cannot share a screen<br>1. At Step 1, the device cannot share a screen.<br>2. The system does not offer screen sharing; watching another person present still works.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Sharing cannot start<br>1. At Step 2, the share cannot start.<br>2. Nothing is shared and the stage stays as it was. The system displays MSG341 – “Unable to share your screen. Please try again.”<br>3. The flow resumes at Step 1 of the Normal Flow. |

#### UC-45d – Choose Audio and Video Devices

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person in the Meeting Lobby or in the meeting room to choose which microphone, speaker and camera the browser uses. The choice applies to that person's browser on that device only: it changes no Meeting setting and is not stored with the Meeting. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room. (UC-43)<br>● PRE-3: The browser lists at least one device of the kind being chosen, or reports why it cannot. |
| **Postconditions** | ● POST-1: The chosen device is used for the actor's microphone, speaker or camera from then on.<br>● POST-2: The browser remembers the choice for the next visit on that device.<br>● POST-3: No other person's devices, the Meeting and its attendance are unchanged. |
| **Normal Flow** | Choose Audio and Video Devices<br>1. The actor chooses to change the microphone, speaker or camera in the lobby or in the room.<br>2. The system lists the devices of that kind found on the computer and marks the system default.<br>3. The actor chooses a device.<br>4. The system switches the microphone, speaker or camera to that device and keeps the current on/off state.<br>5. The system shows the chosen device and remembers it for the next visit. |
| **Alternative Flows** | AF-1 – The speaker cannot be chosen<br>1. At Step 1, the browser cannot send sound to a chosen speaker.<br>2. The speaker is not offered and sound keeps playing through the system output.<br>3. The use case ends.<br>AF-2 – No device is listed<br>1. At Step 2, the browser has not granted permission or reports no device of that kind.<br>2. No device can be chosen, and the actor can still join the room to listen.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – The chosen device is not found<br>1. At Step 4, the chosen device is no longer connected to the computer.<br>2. The previous device stays in use. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – The device cannot be opened<br>1. At Step 4, the browser cannot open the chosen device for another reason.<br>2. The previous device stays in use and the actor can still join the room to listen. The system displays MSG337 – “Unable to open the {device}. Check the permission at the lock icon beside the address bar. You can still join the room to listen.”<br>3. The flow resumes at Step 1 of the Normal Flow. |

### UC-46 – View Meeting Room Participants

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person in the room to see who is connected at that moment, with each person's Meeting Role, who is speaking and whose microphone is off. It shows the room, not the roster: someone who has not joined, or has left, is not listed. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43) |
| **Postconditions** | ● POST-1: The panel lists everyone connected at that moment, ordered by Meeting Role and then by name.<br>● POST-2: The list follows people joining, leaving, speaking, muting (UC-47) and changing role (UC-34) while it is open. (BR-101, BR-102)<br>● POST-3: Presence in the list is not attendance and changes no stored data. (BR-101) |
| **Normal Flow** | View Meeting Room Participants<br>1. The actor opens the list of people in the room.<br>2. The system shows the number of people currently connected.<br>3. The system lists each connected person with name, Meeting Role, whether the person is speaking and the microphone state; the actor's own row is marked as the actor. (BR-101)<br>4. The system orders the rows by Meeting Role (Chair, Secretary, Member, Guest) and then by name.<br>5. The actor closes the list; no separate message is displayed. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – The connection is lost<br>1. At Step 3, the connection to the room is lost.<br>2. The list stops updating and the room shows MSG333 – “Connection lost, reconnecting…”; if the connection cannot be restored, the room reports it through UC-44.<br>3. The use case ends. |

### UC-47 – Moderate Meeting Room

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair to mute another person's microphone in the room. Muting is one-way: the Chair cannot unmute that person, who may turn the microphone on again. Raising a hand and giving the floor to a speaker is UC-50; muting everyone and removing a person from the room are not provided. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-102)<br>● PRE-2: The Meeting is In Progress and the actor is connected to its room. (UC-43)<br>● PRE-3: The target is a person currently connected to the room whose microphone is on. (BR-102) |
| **Postconditions** | ● POST-1: The target's microphone is off and everyone in the room sees it as off. (BR-102)<br>● POST-2: The target stays in the room and may turn the microphone on again. (BR-102)<br>● POST-3: The target's camera, roles, attendance and the Meeting lifecycle are unchanged. |
| **Normal Flow** | Mute a Participant<br>1. The Chair opens the list of people in the room and finds a person whose microphone is on. (UC-46)<br>2. The Chair chooses to mute that person.<br>3. The system checks that the actor is still the current Chair and that the target is still in the room with the microphone on. (BR-102)<br>4. The system mutes the target's microphone for everyone in the room. (BR-102)<br>5. The system displays MSG342 – “Muted the microphone of {name}.” to the Chair, and MSG339 – “The Chair has muted your microphone. You can turn it back on when you need to speak.” to the target. |
| **Alternative Flows** | AF-1 – The target turns the microphone on again<br>1. After Step 4, the target turns the microphone on through UC-45a.<br>2. The microphone is on again and everyone sees it; the Chair may mute again. (BR-102)<br>3. The flow resumes at Step 1 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is no longer the chair<br>1. At Step 3, the Chair role was handed over through UC-34, so the actor is no longer the Chair. (BR-102)<br>2. No microphone is muted. The system displays MSG123 – “Only the Chair can mute another person's microphone.”<br>3. The use case ends.<br>EX-2 – Meeting is no longer in progress<br>1. At Step 3, the Meeting ended.<br>2. No microphone is muted. The system displays MSG117 – “The meeting room opens only while the meeting is in progress.”<br>3. The use case ends.<br>EX-3 – Target is no longer in the room or has no microphone on<br>1. At Step 3, the target left, was disconnected or turned the microphone off.<br>2. No microphone is muted. The system displays MSG282 – “This person is no longer in the room or has no microphone on.”<br>3. The flow resumes at Step 1 of the Normal Flow. |

### UC-48 – Use In-room Chat

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows a person in the room to read the messages sent since joining and to send a text message to everyone currently in the room. Messages live only in the room: they are not stored, and a person who joins later does not see earlier messages. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43) |
| **Postconditions** | ● POST-1: The message is shown to everyone connected at that moment, with its sender and time.<br>● POST-2: Nothing is stored; the message disappears when the room closes. (BR-103)<br>● POST-3: No Meeting data, attendance or document changes. |
| **Normal Flow** | Use In-room Chat<br>1. The actor opens the chat of the room.<br>2. The system shows the messages sent since the actor joined, in time order, and tells the actor that messages are visible only to people in the room and are not kept after the meeting. (BR-103)<br>3. The actor writes a message of at most 1,000 characters.<br>4. The actor sends the message.<br>5. The system checks that the message is not blank and sends it to everyone connected. (BR-103)<br>6. The system shows the message to everyone with the sender and the time; no separate success message is displayed. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – The message cannot be sent<br>1. At Step 5, the message cannot be sent.<br>2. The message is kept so the actor can try again. The system displays MSG343 – “Unable to send the message. Please try again.”<br>3. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – The connection is lost<br>1. At Step 5, the connection to the room is lost.<br>2. The message is not sent. The room displays MSG333 – “Connection lost, reconnecting…” and, if the connection cannot be restored, reports it through UC-44. (BR-103)<br>3. The use case ends. |

### UC-49 – Keep Meeting in a Floating Window

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Extends UC-43. Allows a person connected to the room to keep the call in a small always-on-top window while working in other tabs, and to bring it back to the main window. The floating window shows the same room connection; it is not a second join. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser can show the call in a floating window. |
| **Postconditions** | ● POST-1: The call is shown in a floating window and the main window says the meeting is in the floating window.<br>● POST-2: The actor stays in the room with the same microphone, camera and connection.<br>● POST-3: The floating window closes when the actor leaves the room or closes it, and no second connection is created. |
| **Normal Flow** | Keep Meeting in a Floating Window<br>1. The actor chooses to move the call to a floating window.<br>2. The system opens a small floating window with the current presentation or speaker, the actor's own camera and the call controls, on the same room connection.<br>3. The main window shows that the meeting is in the floating window.<br>4. The actor brings the call back to the main window, or closes the floating window.<br>5. The system returns the call to the main window; the connection is unchanged. No separate message is displayed. |
| **Alternative Flows** | AF-1 – The browser opens the floating window on its own<br>1. At Step 1, the actor switches to another tab and the browser offers to keep the call in a floating window.<br>2. The system opens the floating window and the flow resumes at Step 3 of the Normal Flow.<br>AF-2 – Leave from the floating window<br>1. At Step 3, the actor leaves the room from the floating window.<br>2. The system leaves the room through UC-44 without asking for confirmation, because the main window is not in view, and closes the floating window.<br>3. The use case ends.<br>AF-3 – The browser does not support it<br>1. At Step 1, the browser cannot show a floating window.<br>2. The system does not offer it and the room works as in UC-43.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – The floating window is refused<br>1. At Step 2, the browser refuses to open the window, for example because it was not opened by a direct action.<br>2. The call stays in the main window and no message is displayed.<br>3. The use case ends. |

### UC-50 – Register to Speak

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Extends UC-43. Allows a person in the online room to raise a hand to ask for the floor, and allows the current Chair to see the raised hands in order, invite one person at a time to speak and close each turn. Raising a hand does not turn any microphone on or off: the invited person turns his or her own microphone on (UC-45a). The requests are kept with the Meeting as a record of who spoke and for how long; they are not tasks. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: To invite, reorder or close a turn, the actor is the current Chair of the Meeting. (BR-104, BR-105, BR-106) |
| **Postconditions** | ● POST-1: A raised hand is queued behind the earlier ones, or the hand is lowered. (BR-104, BR-105, BR-106)<br>● POST-2: An invited person is the only one holding the floor until the turn is closed; the invitation and the start and end of the turn are kept with the Meeting. (BR-104, BR-105, BR-106)<br>● POST-3: Microphones, Meeting Roles, attendance and the Meeting lifecycle are unchanged. (BR-102, BR-101) |
| **Normal Flow** | Register to Speak<br>1. The actor raises a hand. When an agenda item is under discussion, the request is linked to it.<br>2. The system checks that the actor is in the room, that the Meeting is In Progress and that the actor has no open request, then puts the request at the end of the queue and shows the raised hand to everyone in the room. (UC-46, BR-104, BR-105, BR-106)<br>3. The Chair sees the queue of raised hands in order, with each person’s name and the time of the request.<br>4. The Chair invites a person to speak, usually the first in the queue. (BR-104, BR-105, BR-106)<br>5. The system marks the request as invited and shows the person that it is his or her turn.<br>6. The person turns the microphone on (UC-45a) and starts speaking; the system marks the turn as speaking and keeps its start time. (BR-104, BR-105, BR-106)<br>7. The person finishes speaking, or the Chair closes the turn; the system keeps the end time, lowers the hand and removes the request from the queue. The Chair may invite the next person from Step 4. (BR-104, BR-105, BR-106) |
| **Alternative Flows** | AF-1 – Lower the hand<br>1. At any step before Step 6, the person lowers the hand.<br>2. The system cancels the request, lowers the hand and removes it from the queue. (BR-104, BR-105, BR-106)<br>3. The use case ends.<br>AF-2 – The Chair reorders the queue<br>1. At Step 3, the Chair moves a request up or down in the queue.<br>2. The system keeps the new order for everyone who sees the queue. (BR-104, BR-105, BR-106)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – The Chair declines a request<br>1. At Step 4, the Chair removes a request without inviting the person.<br>2. The system cancels the request and lowers the hand. (BR-104, BR-105, BR-106)<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – Meeting is no longer in progress<br>1. At Step 2 or Step 4, the Meeting ended.<br>2. No request is created or changed. The system displays MSG117 – “The meeting room opens only while the meeting is in progress.”<br>3. The use case ends.<br>EX-2 – Actor already has an open request<br>1. At Step 2, the actor’s earlier request is still open.<br>2. Nothing changes and the hand stays raised. (BR-104, BR-105, BR-106)<br>3. The use case ends.<br>EX-3 – Actor is no longer the chair<br>1. At Step 4, or when reordering, declining or closing another person’s turn, the actor is not the current Chair, for instance because the Chair role was handed over through UC-34. (BR-104, BR-105, BR-106)<br>2. Nothing is changed. The system displays MSG457 – “Only the current Chair may run the speaking turns.”<br>3. The use case ends.<br>EX-4 – Actor is not in the room<br>1. At Step 2 or Step 6, the actor is not connected to the meeting room.<br>2. No request is created or changed. The system displays MSG458 – “You must be in the meeting room to register to speak.”<br>3. The use case ends.<br>EX-5 – Someone already holds the floor<br>1. At Step 4, another person is already invited or speaking. (BR-104, BR-105, BR-106)<br>2. Nobody is invited. The system displays MSG459 – “Someone is already invited or speaking. Close that turn first.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-6 – The request has changed<br>1. At any step, the request is no longer in a state that allows the action, for instance it was lowered, finished or not yet invited.<br>2. Nothing is changed. The system displays MSG460 – “This speaking request has changed. Reload the list.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

---

## FE-09 — Meeting Recording

### UC-51 – View & Play Meeting Recordings

_UC gom — đặc tả nằm ở các UC con UC-51a, UC-51b… bên dưới._

#### UC-51a – View Meeting Recordings

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a person who currently holds a Chair, Secretary or Participant assignment of a Meeting to view what was recorded in it: the sentences recorded from each person's own microphone, the room recordings and, for a Hybrid or Online Meeting that has ended, one combined recording. |
| **Preconditions** | ● PRE-1: The actor is signed in as an internal user and may view the Meeting.<br>● PRE-2: The actor currently holds a Chair, Secretary or Participant assignment of the Meeting. Being the Member who created the Meeting or its Approver alone is not enough. (BR-111, BR-112) |
| **Postconditions** | ● POST-1: The actor sees the recorded sentences and the room recordings that apply to the Meeting's format. Nothing is changed.<br>● POST-2: A Guest, or a person who is not on the current roster, sees no recording and cannot tell that any exists. Erased recordings are not listed. (BR-111, BR-112)<br>● POST-3: A sentence excluded from the record appears to everyone except the Chair and the Secretary as an empty entry, without its text. (BR-114, BR-115) |
| **Normal Flow** | View Meeting Recordings<br>1. The actor opens the recordings of the Meeting.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and loads the recorded sentences and the room recordings of the Meeting. (BR-111, BR-112)<br>3. The system shows the recorded sentences grouped by speaker, each with its time; the actor may select a sentence to play it (UC-51b). A sentence that repeats speech picked up by another microphone is marked as overlapping.<br>4. For an In-person or Hybrid Meeting, the system also shows each room recording with its state (Recording or Available), its source (room microphone, uploaded file or combined), its length, the date it will be erased, its takes and the state of its transcript. (BR-107, BR-110)<br>5. While the room microphone is on, the system shows everyone viewing the recordings a Recording indicator. |
| **Alternative Flows** | AF-1 – Online meeting<br>1. At Step 4, the Meeting is held online.<br>2. The system shows only the recorded sentences. No room recording and no audio upload is offered, because an Online Meeting records each person's own microphone. (BR-107)<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Sentence excluded from the record<br>1. At Step 3, the Chair or the Secretary has excluded a sentence from the record.<br>2. The Chair and the Secretary see the sentence with its text and the reason. Everyone else sees an empty entry in its place: they can tell that a sentence once existed, but cannot read or play it. (BR-114, BR-115)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – Combined recording<br>1. At Step 4, the Meeting is Hybrid or Online, has Ended and is not Secret.<br>2. The system also lists one combined recording, labelled Combined, which follows the real time of the Meeting and leaves out the sentences excluded from the record. It can be played and downloaded (UC-51b, UC-51c) but has no transcript. (BR-122)<br>3. The flow resumes at Step 5 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is not on the current roster<br>1. At Step 2, the actor is a Guest or no longer holds a Chair, Secretary or Participant assignment. (BR-111, BR-112)<br>2. Nothing is shown and the system answers as if the Meeting had no recordings, so the actor cannot tell that any exist.<br>3. The use case ends. |

#### UC-51b – Play Meeting Recording

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a person who currently holds a Chair, Secretary or Participant assignment of a Meeting to listen to a room recording, the combined recording or to a single recorded sentence. |
| **Preconditions** | ● PRE-1: The actor is viewing the recordings of a Meeting in which the actor currently holds a Chair, Secretary or Participant assignment. (UC-51a, BR-111, BR-112)<br>● PRE-2: The recording or the sentence has not been erased or deleted. |
| **Postconditions** | ● POST-1: The audio plays for the actor. Nothing is saved to the actor's device and the record is not changed.<br>● POST-2: Only the Chair and the Secretary can play a sentence excluded from the record. (BR-114, BR-115) |
| **Normal Flow** | Play Meeting Recording<br>1. The actor selects Play on a room recording, the combined recording or on a recorded sentence.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and may hear this item. (BR-111, BR-112, BR-114, BR-115)<br>3. The system plays the recording, or the chosen sentence, from its beginning.<br>4. The actor can pause, move to another position, change the volume and change the playback speed. While playing, the words being said are highlighted. |
| **Alternative Flows** | AF-1 – Play an excluded sentence<br>1. At Step 1, the actor is the current Chair or Secretary and selects a sentence excluded from the record.<br>2. The system confirms that the actor is the Chair or the Secretary and allows the playback. (BR-114, BR-115)<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor no longer has access, or recording not Available<br>1. At Step 2, the actor is no longer on the roster, or the sentence is excluded from the record and the actor is neither the Chair nor the Secretary. (BR-111, BR-112, BR-114, BR-115)<br>2. Nothing plays and the system answers as if the audio did not exist.<br>3. The use case ends. |

#### UC-51c – Download Meeting Recording

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to download a room recording or the combined recording to the computer as an MP3 file. |
| **Preconditions** | ● PRE-1: The actor is signed in and currently holds the Chair or Secretary assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting is not Secret. (BR-111, BR-112)<br>● PRE-3: The recording is Available, not Erased. |
| **Postconditions** | ● POST-1: An MP3 file is saved on the actor's device.<br>● POST-2: The download is recorded in the audit log. (BR-111, BR-112) |
| **Normal Flow** | Download Meeting Recording<br>1. The actor selects Download on an available room recording or the combined recording.<br>2. The system checks that the actor is the current Chair or Secretary and that the Meeting is not Secret. (BR-111, BR-112)<br>3. The system records the download in the audit log. (BR-111, BR-112)<br>4. The system delivers the recording as an MP3 file. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is a Participant, not the Chair or the Secretary. (BR-111, BR-112)<br>2. No file is delivered and nothing is logged. The system displays MSG355 – “Only the Chair or Secretary may download meeting recordings and transcripts.”<br>3. The use case ends.<br>EX-2 – Secret meeting<br>1. At Step 2, the Meeting is Secret. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG356 – “Recordings and transcripts of Secret meetings cannot be downloaded.”<br>3. The use case ends. |

### UC-52 – Record Room Audio

_UC gom — đặc tả nằm ở các UC con UC-52a, UC-52b… bên dưới._

#### UC-52a – Turn Room Microphone On/Off

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to record the sound of the meeting room: turning the room microphone on starts a take, and turning it off saves the take. The Meeting's room recording is made of its takes. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The Meeting is In-person or Hybrid and is In Progress. (BR-107) |
| **Postconditions** | ● POST-1: While the microphone is on, a take is open, the room sound is recorded and everyone viewing the Meeting's recordings sees the Recording indicator.<br>● POST-2: When the microphone is turned off, the take is saved into the Meeting's room recording and the recording is transcribed again; text corrected and names assigned on earlier takes carry over where they still match. (UC-54a, BR-125)<br>● POST-3: The confirmation that attendees were informed, with its time and purpose, is kept with the recording. (BR-109) |
| **Normal Flow** | Turn Room Microphone On/Off<br>1. The Secretary turns the room microphone on from the recording card of an In-person Meeting.<br>2. If this is the first take of the Meeting's room recording, the system asks the Secretary to confirm that attendees were informed and nobody objected, and to enter the purpose of the recording (optional). (BR-109)<br>3. The Secretary confirms.<br>4. The system checks the Secretary's role, the format and the status of the Meeting and that the room microphone is not on elsewhere, opens a new take and keeps the time of the confirmation and the purpose. (BR-107, BR-108, BR-109)<br>5. The room sound is recorded while the microphone is on. Everyone viewing the Meeting's recordings sees the Recording indicator, and the recording card stays visible on the Secretary's other pages.<br>6. The Secretary turns the room microphone off.<br>7. The system saves the take into the Meeting's room recording and displays MSG363 – “Room recording take saved.” The Recording indicator disappears and the recording is transcribed. (UC-54a) |
| **Alternative Flows** | AF-1 – Hybrid meeting<br>1. At Step 1, the Meeting is Hybrid.<br>2. The Secretary turns on his or her microphone in the online meeting room. For the Secretary of a Hybrid Meeting that microphone is the room microphone, so the room has one switch and no separate recording card. (UC-45a)<br>3. Turning that microphone off later, including when the Chair turns the Secretary's microphone off in the online room, turns the room microphone off and saves the take as in Steps 6–7.<br>4. If the online meeting room cannot be used, the recording card on the Meeting page is the switch, as for an In-person Meeting.<br>5. The flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Reopening the Meeting while a take is open on this device<br>1. At Step 1, the Secretary closed the page or the browser while the room microphone was on and opens the Meeting again on the same device.<br>2. The system offers Continue recording or Save recorded part.<br>3. If the Secretary continues, the recording carries on in the same take and the flow resumes at Step 5 of the Normal Flow.<br>4. If the Secretary saves the recorded part, the flow resumes at Step 7 of the Normal Flow.<br>AF-3 – Take left open on another device<br>1. At Step 1, the system shows that the room is still being recorded on another device.<br>2. The Secretary chooses to stop the recording on the other device.<br>3. The system saves the part recorded on the other device as a take, so that it is transcribed. (BR-108)<br>4. The flow resumes at Step 1 of the Normal Flow, on this device.<br>AF-4 – Later take of the same Meeting<br>1. At Step 2, the Meeting already has a room recording, so an earlier take of it already received the confirmation.<br>2. The system does not ask again. If that room recording was erased (UC-52e), the confirmation is asked again. (BR-109)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-5 – No speech in the take<br>1. At Step 7, the take received no sound at all.<br>2. The system discards the take and leaves the recording unchanged. It displays MSG365 – “Microphone detected no speech; take was not saved.”<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Confirmation missing or information invalid<br>1. At Step 4, the confirmation is missing or the recording information is incomplete.<br>2. No take is opened. The system displays MSG291 – “Invalid recording information.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4 or Step 7, the actor is not the current Secretary. (BR-108)<br>2. No take is opened or saved. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Meeting is not in progress<br>1. At Step 4, the Meeting is not In Progress. (BR-107)<br>2. No take is opened. The system displays MSG359 – “Room microphone can only be turned on while the meeting is in progress.”<br>3. The use case ends.<br>EX-4 – Room microphone is on at another device<br>1. At Step 4, a take of the Meeting is still open on another device.<br>2. No take is opened. The system displays MSG360 – “Room microphone is currently active on another device.” The Secretary may stop that recording first (AF-3).<br>3. The use case ends.<br>EX-5 – Take already saved<br>1. At Step 7, the take was already saved by another action, for instance from another device.<br>2. Nothing is added. The system displays MSG362 – “This recording take has been closed and cannot accept additional audio.”<br>3. The use case ends. |

#### UC-52c – Upload Meeting Audio File

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to add an audio file recorded outside the system to the recordings of an In-person Meeting. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The Meeting is In-person and is In Progress or Ended. (BR-107) |
| **Postconditions** | ● POST-1: The audio file is kept as a recording of its own and is transcribed. Takes recorded with the room microphone are never added to it. (UC-54a)<br>● POST-2: The confirmation that attendees were informed, with its time and purpose, is kept with the recording, and the upload is recorded in the audit log. (BR-109) |
| **Normal Flow** | Upload Meeting Audio File<br>1. The Secretary selects Upload audio file on the recordings of the Meeting.<br>2. The Secretary chooses an audio file from the computer.<br>3. The Secretary confirms that attendees were informed and nobody objected, and may enter the purpose of the recording. The system reminds the Secretary that the file must have been recorded with the meeting room microphone used for the voice samples. (BR-109)<br>4. The Secretary selects Upload.<br>5. The system checks the Secretary's role, the format and the status of the Meeting, the type and the size of the file, and keeps the file. (BR-107, BR-108, BR-109)<br>6. The system shows the file as uploaded and starts its transcription. (UC-54a) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – File or recording information is invalid<br>1. At Step 5, the confirmation is missing or the recording information is incomplete.<br>2. No file is kept. The system displays MSG291 – “Invalid recording information.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 5, the actor is not the current Secretary. (BR-108)<br>2. No file is kept. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Meeting does not allow uploads<br>1. At Step 5, the Meeting is neither In Progress nor Ended. (BR-107)<br>2. No file is kept. The system displays MSG294 – “Recordings may only be saved while the meeting is in progress or has ended.”<br>3. The use case ends. |

#### UC-52d – Delete Recording Take

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to remove one take from a room recording: the sound and the sentences of that take are cut out while the other takes are kept. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The recording has at least two takes.<br>● PRE-3: The recording is Available; a take still being recorded cannot be deleted. |
| **Postconditions** | ● POST-1: The sound and the sentences of the deleted take are removed from the recording and the later takes move up to close the gap.<br>● POST-2: Text edited and names assigned on the other takes are kept. A speaker heard only in the deleted take loses its assigned name. (BR-125)<br>● POST-3: The deletion is recorded in the audit log. |
| **Normal Flow** | Delete Recording Take<br>1. The Secretary opens the takes of a room recording and chooses to delete one of them.<br>2. The system warns that the sound and the sentences of this take will be removed and asks for confirmation.<br>3. The Secretary confirms.<br>4. The system removes the sound and the sentences of the take, moves the later takes up and records the deletion in the audit log. (BR-108)<br>5. The system displays MSG366 – “Recording take deleted.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Recording has only one take<br>1. At Step 4, the recording has only one take.<br>2. Nothing is removed. The system displays MSG367 – “Recording contains only one take. Erase the entire recording to remove it.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4, the actor is not the current Secretary. (BR-108)<br>2. Nothing is removed. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |

#### UC-52e – Erase Meeting Recording

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to erase the audio and the transcript of a recording – a room recording, an uploaded file or the combined recording – while keeping the fact that it existed. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The recording is Available; it is not a take that is still being recorded. Another take of the Meeting may be open at the same time. |
| **Postconditions** | ● POST-1: The audio and the transcript of the recording are permanently removed. (BR-110)<br>● POST-2: The recording no longer appears among the Meeting's recordings. The audit log keeps that it existed, who erased it and when. (BR-110)<br>● POST-3: After the room recording of a Meeting is erased, the next take of that Meeting asks again for the confirmation that attendees were informed. (UC-52a)<br>● POST-4: Erasing the room recording of a Meeting also erases its combined recording.<br>● POST-5: An erased combined recording is not built again. |
| **Normal Flow** | Erase Meeting Recording<br>1. The Secretary selects Erase recording on a recording: a room recording, an uploaded file or the combined recording.<br>2. The system asks for confirmation and explains that the audio and the transcript will be permanently removed.<br>3. The Secretary confirms.<br>4. The system removes the audio and the transcript and records the erasure and the Secretary in the audit log. (BR-110)<br>5. The system displays MSG296 – “Meeting recording erased.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Room microphone is still recording this take<br>1. At Step 4, the recording is a take that is still being recorded.<br>2. Nothing is erased. The system displays MSG364 – “Room microphone is currently recording. Turn off the microphone before erasing.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4, the actor is not the current Secretary. (BR-108)<br>2. Nothing is erased. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |

### UC-53 – Manage My Recorded Speech

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a person to review the sentences recorded from his or her own microphone, listen to them again and delete a sentence of his or her own: an internal user while signed in and holding a Chair, Secretary or Participant assignment, or a Guest while the verified invitation session lasts. (BR-113, BR-111, BR-112) |
| **Preconditions** | ● PRE-1: The actor is signed in as an internal user and currently holds a Chair, Secretary or Participant assignment of the Meeting, or is a Guest with a verified, current invitation to it. A Guest sees and deletes only the sentences of their own microphone. (BR-113, BR-111, BR-112) |
| **Postconditions** | ● POST-1: A deleted sentence, with its text and its audio, is permanently removed from the Meeting record. (BR-113) |
| **Normal Flow** | Manage My Recorded Speech<br>1. In the online room or on the recordings of the Meeting, the actor opens My Recorded Speech.<br>2. The system lists the actor's sentences: those still waiting on this device to be delivered and those already saved, each with its time and its text.<br>3. The actor selects a sentence to listen to it.<br>4. The actor selects Delete on a sentence and confirms.<br>5. The system permanently removes the text and the audio of the sentence and records the deletion in the audit log. (BR-113)<br>6. The system displays MSG322 – “Spoken sentence deleted.” and refreshes the list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is neither the speaker nor the Secretary<br>1. At Step 5, the sentence belongs to another speaker and the actor is not the Secretary. (BR-113)<br>2. Nothing is removed. For an internal user, the system displays MSG323 – “Only the speaker or the Secretary may delete this sentence.” A Guest gets no message, as if the sentence did not exist.<br>3. The use case ends. |

---

## FE-10 — Transcription, Speaker Identification & Meeting Summary

### UC-54 – View Meeting Transcript

_UC gom — đặc tả nằm ở các UC con UC-54a, UC-54b… bên dưới._

#### UC-54a – View Recording Transcript

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a person who currently holds a Chair, Secretary or Participant assignment to view the transcript of a recording: its state, the text with times and speakers, the dividers between takes and the text corrected by a person. A saved take or an uploaded audio file is transcribed without any action; the Secretary may also run the transcription. |
| **Preconditions** | ● PRE-1: The actor is signed in as an internal user and currently holds a Chair, Secretary or Participant assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting has a room recording or an uploaded audio file that has not been erased. |
| **Postconditions** | ● POST-1: The actor sees the state of the transcript and, once it is Completed, its passages with times, speakers and take dividers.<br>● POST-2: Suggestions are visible only to the Secretary. A name that is assigned – including one the system filled in by itself – is part of the transcript, so everyone who may read the transcript sees it; only the Secretary may change or clear it. (BR-111, BR-112, BR-120, BR-123)<br>● POST-3: When the Secretary runs the transcription, the transcript is made again from the whole recording and ends Completed or Failed with the reason; names assigned by hand, corrected text and moved sentences carry over where they still match, and what cannot be matched stays only in the audit log. (BR-121, BR-125, BR-154) |
| **Normal Flow** | View Recording Transcript<br>1. The actor selects View transcript on a recording.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and loads the transcript. (BR-111, BR-112)<br>3. The system shows the state of the transcript: Pending, Processing, Completed or Failed. For a Failed transcript it also shows the reason.<br>4. When the transcript is Completed, the system shows each passage with its time and its speaker (Speaker N, or the name assigned), a divider where one take ends and the next begins, and marks the text corrected by a person. While the recording plays, the words being said are highlighted, and words the system was unsure of are flagged.<br>5. For the Secretary, the system also shows the suggested names next to each unnamed speaker and the control to assign a speaker (UC-55b). (BR-120, BR-123) |
| **Alternative Flows** | AF-1 – Transcript is not completed yet<br>1. At Step 3, the transcript is Pending or Processing.<br>2. The system shows the state without text.<br>3. When the transcript is Completed, the flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Secretary runs the transcription<br>1. At Step 3, the actor is the current Secretary and the recording has no transcript yet, or its transcript is Failed.<br>2. The Secretary chooses to transcribe the recording, or to run it again after a failure.<br>3. The system checks that the actor is the current Secretary and that the recording is not already waiting or being transcribed. (BR-108)<br>4. The system puts the recording in line for transcription and displays MSG300 – “Recording queued for transcription.”<br>5. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Secretary transcribes a completed transcript again<br>1. At Step 4, the actor is the current Secretary and chooses to transcribe the recording again.<br>2. The system warns that the transcript will be made again and that text corrected and names assigned are kept only where they still match, and asks for confirmation. (BR-125, BR-154)<br>3. The Secretary confirms.<br>4. The flow resumes at Step 3 of AF-2. |
| **Exception Flows** | EX-1 – Recording already waiting or being transcribed<br>1. At Step 3 of AF-2, the recording is waiting or is being transcribed.<br>2. Nothing changes. The system displays MSG301 – “The recording is currently queued or being transcribed and cannot be rerun yet.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3 of AF-2, the actor is not the current Secretary. (BR-108)<br>2. Nothing changes. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Combined recording<br>1. At Step 3 of AF-2, the recording is the combined recording of the Meeting. (BR-122)<br>2. Nothing is queued. The system displays MSG479 – “The combined recording is for listening only. Its speech has already been transcribed from the room and per-microphone recordings.”<br>3. The use case ends. |

#### UC-54b – Export Meeting Transcript

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to export the transcript of a room recording, or the sentences recorded from each person's own microphone, as a text file. |
| **Preconditions** | ● PRE-1: The actor is signed in and currently holds the Chair or Secretary assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting is not Secret. (BR-111, BR-112)<br>● PRE-3: For a room recording, its transcript is Completed. |
| **Postconditions** | ● POST-1: A text file with the transcript is delivered to the actor; a speaker assigned to a Guest appears under that Guest's self-declared display name, or under the invited name while the Guest has not declared one.<br>● POST-2: Sentences excluded from the record are left out of the file.<br>● POST-3: The export is recorded in the audit log. (BR-111, BR-112) |
| **Normal Flow** | Export Meeting Transcript<br>1. The actor chooses to export the transcript of a recording, or the recorded sentences.<br>2. The system checks that the actor is the current Chair or Secretary, that the Meeting is not Secret and that the transcript is Completed. (BR-111, BR-112)<br>3. The system builds the text file without the sentences excluded from the record.<br>4. The system records the export in the audit log and delivers the file. (BR-111, BR-112) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Participant tries to export<br>1. At Step 2, the actor is a Participant, not the Chair or the Secretary. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG355 – “Only the Chair or Secretary may download meeting recordings and transcripts.”<br>3. The use case ends.<br>EX-2 – Secret meeting<br>1. At Step 2, the Meeting is Secret. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG356 – “Recordings and transcripts of Secret meetings cannot be downloaded.”<br>3. The use case ends.<br>EX-3 – Transcript is not ready<br>1. At Step 2, the transcript is Pending, Processing or Failed.<br>2. No file is delivered. The system displays MSG357 – “Transcript is not ready for download.”<br>3. The use case ends. |

### UC-55 – Edit Transcript & Speaker Attribution

_UC gom — đặc tả nằm ở các UC con UC-55a, UC-55b… bên dưới._

#### UC-55a – Edit Transcript Text

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to correct the text of a transcript passage or of a recorded sentence. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-114, BR-115)<br>● PRE-2: The passage or the sentence exists. |
| **Postconditions** | ● POST-1: The corrected text is saved with the name of the Secretary and the time of the change, to be carried onto the matching new sentence when the transcript is made again. (BR-114, BR-115, BR-125) |
| **Normal Flow** | Edit Transcript Text<br>1. In the transcript or on the timeline of recorded sentences, the Secretary selects a passage or a sentence to edit.<br>2. The Secretary changes the text and saves.<br>3. The system checks that the text is not empty and has at most 8,000 characters, saves it with the name of the Secretary and the time of the change, clears the uncertain-word marks on that passage, and records the change in the audit log. (BR-114, BR-115) A later run carries the corrected text onto the matching new sentence. (BR-125)<br>4. The system displays MSG302 – “Transcript segment saved.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Text is empty or too long<br>1. At Step 3, the text is empty or longer than 8,000 characters.<br>2. The text is not saved. The system displays MSG303 – “Transcript text is invalid.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3, the actor is not the current Secretary. (BR-114, BR-115)<br>2. The text stays as it was. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |

#### UC-55b – Assign Speaker

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to give a speaker of a transcript the name of a person on the Meeting roster or of an invited Guest whose invitation is still valid, or to clear it, helped by the suggested names. A name the system filled in for a voice that matches a consenting person clearly may be kept, changed or cleared. A Guest is never suggested, because a Guest has no voice sample. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The transcript of the room recording is Completed. |
| **Postconditions** | ● POST-1: The chosen name is shown on every passage of that speaker, or the name is cleared.<br>● POST-2: The change is recorded in the audit log.<br>● POST-3: A name the system filled in for a clearly matching voice is marked in the audit log as assigned automatically. (BR-120, BR-123)<br>● POST-4: A sentence moved to another voice is marked with who moved it and when, so a later run carries it again. (BR-125) |
| **Normal Flow** | Assign Speaker<br>1. In the transcript, the Secretary sees each speaker as Speaker N or with the name assigned, and the suggested name that matches the voice best. (BR-120, BR-123)<br>2. The Secretary chooses a person from the Meeting roster or a Guest with a current invitation of the Meeting, or clears the name. (BR-120)<br>3. The system checks that the person is on the current roster, or that the Guest has a current invitation of the Meeting. (BR-120)<br>4. The system applies the name to every passage of that speaker and records the change in the audit log.<br>5. The system displays MSG316 – “Speaker assigned.” |
| **Alternative Flows** | AF-1 – Several voices of the same person<br>1. At Step 2, the suggestions show that two or more speakers may be the same person.<br>2. The Secretary assigns the person to the whole group of speakers in one action, or clears a name the system filled in. (BR-120, BR-123)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – Change the speaker of one sentence<br>1. At Step 2, the Secretary selects one sentence of the transcript and chooses another speaker for it.<br>2. The Secretary chooses a person on the current roster, a Guest whose invitation is still valid, or any voice of this recording.<br>3. The system moves only that sentence to the chosen person's voice, creating a new voice when the person has none yet, and marks the sentence with the Secretary and the time of the move. A voice left with no sentence is hidden. (BR-125)<br>4. The system records the change in the audit log and displays MSG407 – “The speaker of this sentence has been changed.”<br>5. The flow resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – Person is not on the current roster<br>1. At Step 3, the chosen person is not on the current roster of the Meeting, or the chosen Guest has no current invitation of the Meeting. (BR-120)<br>2. The name is not assigned. The system displays MSG317 – “Assigned person must be on the current meeting roster.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3, the actor is not the current Secretary. (BR-108)<br>2. Nothing is changed. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – The voice is not in this recording<br>1. At Step 3, the chosen voice does not belong to this recording.<br>2. Nothing is changed. The system answers as if the recording had no such voice, without a message.<br>3. The use case ends. |

#### UC-55c – Exclude or Restore a Sentence

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to exclude a recorded sentence from the record, for instance because it picked up a neighbour's speech, or to restore a sentence that was excluded. |
| **Preconditions** | ● PRE-1: The actor is the current Chair or Secretary of the Meeting. (BR-114, BR-115)<br>● PRE-2: The sentence exists on the timeline of recorded sentences. |
| **Postconditions** | ● POST-1: An excluded sentence is marked with its reason. It is not deleted: the Chair and the Secretary can still read and play it, while everyone else sees an empty entry in its place. (BR-114, BR-115)<br>● POST-2: Excluded sentences are left out of exported transcripts. |
| **Normal Flow** | Exclude or Restore a Sentence<br>1. On the timeline of recorded sentences, the actor chooses to exclude a sentence.<br>2. The system marks the sentence as excluded, keeps the reason, the actor and the time, and records the change in the audit log. (BR-114, BR-115)<br>3. The system displays MSG351 – “Sentence excluded from meeting record.” |
| **Alternative Flows** | AF-1 – Restore an excluded sentence<br>1. At Step 1, the actor chooses to restore an excluded sentence.<br>2. The system clears the exclusion, so the sentence is part of the record again, and displays MSG352 – “Sentence restored to meeting record.” (BR-114, BR-115)<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is neither the current Chair nor the current Secretary. (BR-114, BR-115)<br>2. The sentence is unchanged. The system displays MSG353 – “Only the Chair or Secretary may exclude a sentence from the meeting record.”<br>3. The use case ends. |

#### UC-55d – Delete a Sentence

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Secretary to permanently delete any recorded sentence, with its audio, from the timeline. Unlike excluding a sentence (UC-55c), a deleted sentence cannot be brought back. |
| **Preconditions** | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-113)<br>● PRE-2: The sentence exists on the timeline of recorded sentences. |
| **Postconditions** | ● POST-1: The text and the audio of the sentence are permanently removed. (BR-113) |
| **Normal Flow** | Delete a Sentence<br>1. On the timeline of recorded sentences, the Secretary selects Delete on a sentence.<br>2. The system asks for confirmation and warns that the text and the audio will be permanently destroyed.<br>3. The Secretary confirms.<br>4. The system removes the text and the audio of the sentence and records the deletion in the audit log. (BR-113)<br>5. The system displays MSG322 – “Spoken sentence deleted.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is neither the Secretary nor the speaker<br>1. At Step 4, the actor is neither the Secretary nor the person who spoke the sentence (who deletes it through UC-53). (BR-113)<br>2. The sentence is unchanged. The system displays MSG323 – “Only the speaker or the Secretary may delete this sentence.”<br>3. The use case ends. |

### UC-56 – Manage My Voice ID

_UC gom — đặc tả nằm ở các UC con UC-56a, UC-56b… bên dưới._

#### UC-56a – View My Voice ID

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to see his or her own Voice ID: whether consent was given, which version of the notice was accepted and which voice samples it holds, both those the actor recorded and those recorded for the actor on a meeting room microphone. Voice ID belongs to internal users only: a Guest never has a voice profile and is never suggested as a speaker by voice. Deactivating the account ends Voice ID the same way withdrawing does (UC-06c, BR-117). |
| **Preconditions** | ● PRE-1: The actor is signed in as an internal user. |
| **Postconditions** | ● POST-1: The actor sees the consent status, the accepted notice version and the list of voice samples. Nothing is changed. |
| **Normal Flow** | View My Voice ID<br>1. The actor opens the My voice page.<br>2. The system loads the actor's consent and voice samples.<br>3. The system shows the consent status, the notice version the actor accepted, the voice samples the actor recorded (length and date recorded) and, while fewer than 5 samples exist, the option to record a new one. (BR-116, BR-124, BR-118, BR-119) |
| **Alternative Flows** | AF-1 – Notice has been updated<br>1. At Step 3, the actor consented under an earlier version of the notice than the current one.<br>2. The system tells the actor that the terms have changed and offers to accept the new terms (UC-56b). Until the actor accepts, the actor's name is only suggested, never filled in automatically. (BR-116, BR-124)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – View samples recorded by a Secretary<br>1. At Step 3, the actor chooses to view the samples recorded by Secretaries, or opens the My voice page from the notification that a Secretary recorded a sample.<br>2. The system shows the samples that Secretaries recorded for the actor on the meeting room microphone, each with its Meeting, the Secretary who recorded it, its length and date, and how many of the allowed samples are used. The actor cannot record a sample there. (BR-118, BR-119)<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | N/A |

#### UC-56b – Give Voice ID Consent

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to give separate, explicit consent to Voice ID, or to accept the current version of the notice when it changed. This consent is not part of the consent to recording. |
| **Preconditions** | ● PRE-1: The actor is signed in as an internal user and is on the My voice page.<br>● PRE-2: The actor has not consented yet, or consented under an earlier version of the notice. (BR-116, BR-124) |
| **Postconditions** | ● POST-1: The consent is recorded with the version of the notice the actor accepted, the purpose and the time. (BR-116, BR-124) |
| **Normal Flow** | Give Voice ID Consent<br>1. On the My voice page, the actor reads the Voice ID notice: its purpose (suggesting the person's name, and filling it in when the voice matches clearly, including from samples collected for the person), the voice data that is kept, who controls it, the actor's rights including withdrawal, and that voice data is sensitive personal data. (BR-116, BR-121, BR-118, BR-119, BR-124)<br>2. The actor gives consent explicitly; nothing is selected by default.<br>3. The system records the consent with the current version of the notice and the time, and records it in the audit log. (BR-116, BR-124)<br>4. The system displays MSG305 – “Voice identification enabled for your account.” |
| **Alternative Flows** | AF-1 – Accept the updated notice<br>1. At Step 1, the actor consented under an earlier version of the notice.<br>2. The actor reads the current notice and accepts it.<br>3. The system renews the consent on the current version of the notice and keeps the voice samples already recorded. The new terms apply from then on, including names filled in automatically and samples collected for the actor (UC-57b). (BR-116, BR-124, BR-120, BR-123, BR-118, BR-119)<br>4. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Notice version read is not the current one<br>1. At Step 3, the version of the notice the actor read is not the current one. (BR-116, BR-124)<br>2. No consent is recorded. The system displays MSG313 – “Invalid submission data.”<br>3. The use case ends. |

#### UC-56c – Record Voice Sample

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user with Voice ID consent to record a short reading, listen to it and save it, so that the system can suggest his or her name, and fill it in when the voice matches clearly, when the Secretary names the speakers of a transcript. (BR-120, BR-123) |
| **Preconditions** | ● PRE-1: The actor has given Voice ID consent and has not withdrawn it. (BR-116)<br>● PRE-2: The actor has fewer than 5 voice samples the actor recorded. Samples collected for the actor are counted separately toward their own limit of fifty. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: A new voice sample is stored protected and the features of the voice are kept for matching. (BR-118, BR-119, BR-121) |
| **Normal Flow** | Record Voice Sample<br>1. On the My voice page, the actor selects Record sample.<br>2. The system shows a passage to read and asks the actor to read it aloud.<br>3. The actor selects Start recording, reads the passage and selects Stop. Nothing is sent yet: the system plays the recording back, and the actor either selects Save sample or Record again.<br>4. When the actor saves, the system receives the recording.<br>5. The system checks that the recording has 6 to 60 seconds of clear speech from one person. (BR-118, BR-119)<br>6. The system learns the features of the voice from the recording and the system stores the sample protected. (BR-118, BR-119, BR-121)<br>7. The system displays MSG307 – “Voice sample saved.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – No standing consent<br>1. At Step 4, the actor has no active Voice ID consent. (BR-116)<br>2. The sample is not kept. The system displays MSG308 – “Voice identification consent is required before recording a voice sample.”<br>3. The use case ends.<br>EX-2 – Audio is unsupported or fails the quality rules<br>1. At Step 5, the recording is too short, too long, too quiet, distorted or has more than one speaker. (BR-118, BR-119) Or the recording is not of a supported audio type.<br>2. The sample is not kept. The system displays MSG309 – “Voice sample did not meet quality requirements. Please read the full sample text clearly.” An unsupported audio type shows MSG292 – “Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Sample limit reached<br>1. At Step 1, the actor already has 5 voice samples the actor recorded. (BR-118, BR-119)<br>2. No recording starts. The system displays MSG310 – “Maximum of 5 voice samples reached. Delete an existing sample before recording a new one.”<br>3. The use case ends. |

#### UC-56d – Play Voice Sample

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to listen to one of his or her own voice samples, recorded by himself or herself or collected for the person. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the owner of the voice sample. (BR-118, BR-119)<br>● PRE-2: The recording of the sample was kept. Samples recorded under an earlier notice kept only the features of the voice and cannot be played. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: The actor hears the sample. Nothing is changed and nobody else can play it, except a holder of the Voice sample collection permission playing a sample collected for the person (UC-57c). (BR-118, BR-119) |
| **Normal Flow** | Play Voice Sample<br>1. On the My voice page, the actor chooses to play one of his or her voice samples.<br>2. The system checks that the actor owns the sample and plays it to the actor. (BR-118, BR-119) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Sample is not the actor's, or its recording is not available<br>1. At Step 2, the sample is not the actor's, or its recording was not kept or cannot be read.<br>2. Nothing plays. No catalogue message is assigned to this case yet.<br>3. The use case ends. |

#### UC-56e – Delete Voice Sample

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to delete one of his or her own voice samples, recorded by himself or herself or collected for the person. |
| **Preconditions** | ● PRE-1: The actor is signed in and owns the voice sample. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: The sample, its recording and the features of its voice are permanently deleted.<br>● POST-2: The actor's other samples are unchanged. Each remaining sample is still used on its own to suggest the actor's name.<br>● POST-3: A sample collected for the actor no longer appears to the collector, and no longer counts toward the 50 collected samples for the actor. (BR-118, BR-119) |
| **Normal Flow** | Delete Voice Sample<br>1. On the My voice page, the actor chooses to delete one of his or her voice samples.<br>2. The system asks for confirmation.<br>3. The actor confirms.<br>4. The system deletes the sample, its recording and the features of its voice, and records the deletion in the audit log.<br>5. The system displays MSG311 – “Voice sample deleted.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | N/A |

#### UC-56f – Withdraw Voice ID Consent

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to withdraw Voice ID consent. Withdrawing deletes all of the actor's voice samples, every speaker suggestion and every name the system filled in automatically for the actor. An administrator deactivating the account ends Voice ID the same way (UC-06c, BR-117). |
| **Preconditions** | ● PRE-1: The actor is signed in and has active Voice ID consent. (BR-116) |
| **Postconditions** | ● POST-1: All voice samples of the actor, both those the actor recorded and those recorded for the actor, with their recordings and voice features, are permanently deleted. (BR-117)<br>● POST-2: Every speaker suggestion that points to the actor, in every Meeting, and every name the system filled in automatically for the actor, is cleared. (BR-117)<br>● POST-3: Names already assigned by Secretaries are kept. (BR-117) |
| **Normal Flow** | Withdraw Voice ID Consent<br>1. On the My voice page, the actor chooses to withdraw consent.<br>2. The system asks for confirmation, explaining that all of the actor's voice samples will be permanently deleted.<br>3. The actor confirms.<br>4. The system deletes all of the actor's voice samples, clears the speaker suggestions and the names it filled in automatically for the actor, marks the consent as withdrawn and records it in the audit log. (BR-117)<br>5. The system displays MSG306 – “Voice identification disabled and all voice samples deleted.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | N/A |

### UC-57 – Collect Voice Samples for Attendees

_UC gom — đặc tả nằm ở các UC con UC-57a, UC-57b… bên dưới._

#### UC-57a – View Voice Collection

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Voice sample collection permission holder (by default, Administrator) |
| **Secondary Actors** | None |
| **Description** | Allows a holder of the Voice sample collection permission to see, for each person, whether the person agreed to Voice ID and which samples were collected for the person. |
| **Preconditions** | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: The actor sees the collection list. Nothing is changed. |
| **Normal Flow** | View Voice Collection<br>1. On the Users screen, the actor opens voice collection for a person.<br>2. The system checks the actor. (BR-118, BR-119)<br>3. The system shows the person’s standing consent and the samples collected for the person. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – No access: no Voice sample collection permission<br>1. At Step 2, the actor lacks the permission.<br>2. Nothing is done and the system answers as if the person did not exist. (BR-118, BR-119)<br>3. The use case ends. |

#### UC-57b – Record Voice Sample for Attendee

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Voice sample collection permission holder (by default, Administrator) |
| **Secondary Actors** | None |
| **Description** | Allows a holder of the Voice sample collection permission to record a voice sample for a person on the Users screen, under that person’s own Voice ID consent and with per-take agreement. |
| **Preconditions** | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119)<br>● PRE-2: The person has standing Voice ID consent 1.4 or later. (BR-118, BR-119, BR-124) |
| **Postconditions** | ● POST-1: A new voice sample is stored protected for the person, with the collector and the per-take agreement, and the person is notified. (BR-118, BR-119, BR-011) |
| **Normal Flow** | Record Voice Sample for Attendee<br>1. Next to a person, the actor chooses to record a sample.<br>2. The system shows a passage to read.<br>3. The actor confirms that the person agreed to this take, and the person reads the passage while the actor records. Nothing is sent yet: the system plays the recording back, and the actor saves the sample or records again.<br>4. When the actor saves, the system receives the recording.<br>5. The system checks the actor, that the person agreed under notice 1.4 or later with per-take agreement, and that fewer than 50 samples were collected for the person. (BR-118, BR-119, BR-124)<br>6. The system checks that the recording has 6 to 60 seconds of clear speech from one person. (BR-118, BR-119)<br>7. The system learns the features of the voice and stores the sample protected, as the person’s own data, with the collector and the per-take agreement. (BR-118, BR-119, BR-121)<br>8. The system notifies the person and records the sample in the audit log. (BR-011)<br>9. The system displays MSG473 – “Voice sample recorded for the attendee.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Person has not agreed under the current terms<br>1. At Step 5, the person has no active consent under notice 1.4 or later. (BR-118, BR-119, BR-124)<br>2. The sample is not kept. The system displays MSG475 – “This person has not agreed to Voice ID under the current terms. They can enable it on the My voice page.”<br>3. The use case ends.<br>EX-2 – Person already has 50 collected samples<br>1. At Step 5, 50 samples were already collected for the person. (BR-118, BR-119)<br>2. The sample is not kept. The system displays MSG476 – “This person already has 50 collected voice samples. Delete a collected sample before recording a new one.”<br>3. The use case ends.<br>EX-3 – Audio is unsupported or fails the quality rules<br>1. At Step 4, the recording is not of a supported audio type. Or the recording is too short, too long, too quiet, distorted or has more than one speaker.<br>2. The sample is not kept. The system displays MSG292 – “Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG.” A recording that fails the quality rules shows MSG309 – “Voice sample did not meet quality requirements. Please read the full sample text clearly.”<br>3. The flow resumes at Step 1 of the Normal Flow. |

#### UC-57c – Play Collected Voice Sample

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Voice sample collection permission holder (by default, Administrator) |
| **Secondary Actors** | None |
| **Description** | Allows a holder of the Voice sample collection permission to play a voice sample collected for a person. |
| **Preconditions** | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: The actor hears the sample. The playback is recorded in the audit log. Nothing else is changed. (BR-011) |
| **Normal Flow** | Play Collected Voice Sample<br>1. The actor selects Play next to a collected sample.<br>2. The system checks the actor and the sample. (BR-118, BR-119)<br>3. The system records the playback in the audit log and plays the sample. (BR-011) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – No access: no Voice sample collection permission<br>1. At Step 2, the actor lacks the permission or the sample does not exist.<br>2. Nothing plays, as if the sample did not exist. The use case ends. |

#### UC-57d – Delete Collected Voice Sample

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Voice sample collection permission holder (by default, Administrator) |
| **Secondary Actors** | None |
| **Description** | Allows a holder of the Voice sample collection permission to delete a voice sample collected for a person, so a new one can be recorded instead. |
| **Preconditions** | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |
| **Postconditions** | ● POST-1: The sample, its recording and the features of its voice are permanently deleted, and the deletion is recorded in the audit log. (BR-118, BR-119, BR-011) |
| **Normal Flow** | Delete Collected Voice Sample<br>1. The actor selects Delete on a collected sample.<br>2. The system asks for confirmation.<br>3. The actor confirms.<br>4. The system checks as in UC-57c, deletes the sample, its recording and the features of the voice, and records the deletion in the audit log. (BR-118, BR-119, BR-011)<br>5. The system displays MSG474 – “Voice sample deleted.” |
| **Alternative Flows** | AF-1 – Record again<br>1. After Step 5, the actor may record a new sample for the person (UC-57b). Recording again always means delete then record. |
| **Exception Flows** | EX-1 – No access: no Voice sample collection permission, or the sample was recorded by the person<br>1. At Step 4, the actor lacks the permission or the sample does not exist.<br>2. Nothing is deleted, as if the sample did not exist. The use case ends. |

---

### UC-58 – Generate / Regenerate Meeting Summary

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to ask the system to write a first draft of the meeting's content in three sections — I. Objectives and scope of the meeting, II. Discussion and assessment, III. Conclusions and assignments — from the meeting's own record. The draft is written by AI and is a proposal only: a person reviews it before it is used. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: The Meeting has Ended. (BR-126, BR-127)<br>● PRE-3: The in-house language model is available.<br>● PRE-4: No draft of this Meeting is waiting to be written or being written. |
| **Postconditions** | ● POST-1: The request is accepted; any earlier draft stays readable until the new one is ready.<br>● POST-2: A draft of the three sections exists, marked as written by AI, with the time the AI read the transcript and the person who asked for it.<br>● POST-3: The new draft replaces the earlier one whole; its corrections and its review are cleared.<br>● POST-4: Attendance, votes and every other Meeting record are unchanged; the vote results in section III are copied from the closed votes. (BR-128) |
| **Normal Flow** | Generate Meeting Summary<br>1. The Chair or the Secretary opens the draft of the Meeting's minutes. (BR-126, BR-127)<br>2. The system checks the current assignment and the Meeting status and shows what a draft would be based on: sentences recorded, voices not yet named, transcripts still running or failed, agenda items and how many were timed, and closed votes. (BR-126, BR-127, BR-128)<br>3. The actor asks for a draft, or for a new one when a draft already exists.<br>4. The system accepts the request, keeps the earlier draft readable and displays MSG384 – “The request was sent. The draft appears when the AI has written it.”<br>5. The system writes the three sections from the transcript, the agenda items and the conclusions the Chair recorded; section II follows the agenda items, and the result of each closed vote is copied as recorded, not written by AI. (BR-128)<br>6. The system marks the draft as written by AI, clears any earlier review and shows the draft as soon as it is ready. |
| **Alternative Flows** | AF-1 – First draft<br>1. At Step 3, no draft exists yet.<br>2. The system accepts the request and the flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Regenerate and replace<br>1. At Step 3, a draft already exists, including a reviewed one.<br>2. The system asks the actor to confirm that the new draft will replace the current text and its review.<br>3. The actor confirms and the flow resumes at Step 4 of the Normal Flow; the earlier draft stays readable until the new one is ready.<br>AF-3 – No agenda, or items never timed<br>1. At Step 5, the Meeting has no agenda, or none of its items was started.<br>2. The system writes section II from the whole transcript instead of by agenda item.<br>3. The flow resumes at Step 6 of the Normal Flow.<br>AF-4 – Little recorded<br>1. At Step 5, the Meeting has only a sentence or two.<br>2. The system still writes a draft from what was recorded, rather than inventing content. (BR-128)<br>3. The flow resumes at Step 6 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. No request is queued. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – The Meeting has not ended<br>1. At Step 2, the Meeting has not Ended. (BR-126, BR-127)<br>2. No request is queued. The system displays MSG386 – “A draft can be created only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – A draft is already being written<br>1. At Step 4, a request is waiting or the AI is writing.<br>2. The earlier draft stays as it is and no second request is queued. The system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The use case ends. |

### UC-59 – View Meeting Summary

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary; every other person who may read the Meeting, once the draft is reviewed |
| **Secondary Actors** | None |
| **Description** | Allows a person who may read the Meeting to read the AI draft. Until the draft is reviewed only the current Chair and the current Secretary see it; after review the other people who may read the Meeting see it as well, without any control to change it. (BR-126, BR-127) |
| **Preconditions** | ● PRE-1: The internal user is signed in and may read the Meeting.<br>● PRE-2: For anyone other than the current Chair or the current Secretary, the draft has been reviewed. (BR-126, BR-127) |
| **Postconditions** | ● POST-1: The draft, its state and where each part came from are displayed.<br>● POST-2: No draft, review or Meeting record is changed. |
| **Normal Flow** | View Meeting Summary<br>1. The actor opens the minutes of the Meeting; they open only after the Meeting has Ended.<br>2. The system checks that the actor may read the Meeting and whether the actor is its current Chair or current Secretary. (BR-126, BR-127)<br>3. The system displays the draft labelled “AI-written — needs review”, its three sections I–III, the time the AI read the transcript and the person who asked for it.<br>4. The system shows the review state — who reviewed it and when, and who last corrected it and when — and, for each part of section II, the stretch of the Meeting the part was written from, so the reader can check it against the recording.<br>5. While a draft is waiting or being written, the system shows that state; the earlier draft stays readable.<br>6. When voices are still unnamed, the current Chair or Secretary may name them from the draft, under the same rules as UC-55b. |
| **Alternative Flows** | AF-1 – No draft yet<br>1. At Step 3, no draft exists, or none has finished being written.<br>2. The system shows an empty state; for the current Chair and Secretary it also shows what a draft would be based on (BR-128), and everyone else sees nothing about the draft.<br>3. The use case ends.<br>AF-2 – The last draft failed<br>1. At Step 3, the last attempt was marked as failed.<br>2. The system shows the reason. The current Chair or Secretary may ask again (UC-58); the flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Read a reviewed draft as another participant<br>1. At Step 2, the actor is not the current Chair or Secretary and the draft has been reviewed.<br>2. The system applies Steps 3–4 and offers no correction or review control. (BR-126, BR-127)<br>3. The use case ends.<br>AF-4 – The draft is out of date<br>1. At Step 3, the transcript changed after the AI read it — a sentence was added, corrected, excluded or deleted, a voice was named or lost its name because consent was withdrawn (BR-117), a sentence moved to another voice, or the recording was deleted.<br>2. The system marks the draft as out of date and offers to write it again (UC-58); the draft itself is unchanged.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its draft.<br>3. The use case ends. |

### UC-60 – Edit Meeting Summary

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to correct the text of the three sections before the draft is used. Saving the correction withdraws the review, so the other people who may read the Meeting stop seeing the draft until it is reviewed again. (BR-126, BR-127) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: A finished draft exists. The AI is not writing the draft again. |
| **Postconditions** | ● POST-1: The corrected text of sections I–III is saved with the person and the time of the correction.<br>● POST-2: The review is cleared, so only the current Chair and Secretary see the draft again.<br>● POST-3: No other Meeting record is changed. |
| **Normal Flow** | Edit Meeting Summary<br>1. The actor chooses to edit the draft.<br>2. The system checks the assignment and that a finished draft exists, then shows the three sections for editing. (BR-126, BR-127)<br>3. The actor changes the text and saves.<br>4. The system checks that no section is empty and that each is within its limit: I 2,000, II 12,000 and III 8,000 characters.<br>5. The system saves the three sections, records the person and the time, and clears the review.<br>6. The system displays MSG388 – “Draft saved.” and the draft without its review. |
| **Alternative Flows** | AF-1 – Correct a reviewed draft<br>1. At Step 3, the draft had been reviewed.<br>2. The system warns that saving withdraws the review and that the other participants stop seeing the draft until it is reviewed again.<br>3. The actor confirms and the flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. Nothing is saved. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – No finished draft, or the AI is writing it again<br>1. At Step 2, no draft has finished being written. Or the AI is still writing the draft.<br>2. Nothing is saved. The system displays MSG390 – “There is no draft to correct or review.” When the AI is still writing, the system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The use case ends.<br>EX-3 – A section is empty or too long<br>1. At Step 4, a section is empty or longer than its limit.<br>2. Nothing is saved. The system displays MSG389 – “The draft content is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

### UC-61 – Approve Meeting Summary

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to vouch for the draft. From that moment the other people who may read the Meeting see the draft, and the minutes may use its three sections. (BR-126, BR-127) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: A finished draft exists. The AI is not writing the draft again. |
| **Postconditions** | ● POST-1: The reviewer and the time of the review are saved.<br>● POST-2: Everyone who may read the Meeting can read the draft. (BR-126, BR-127)<br>● POST-3: The draft text is unchanged. |
| **Normal Flow** | Approve Meeting Summary<br>1. The actor chooses to approve the draft.<br>2. The system checks the assignment and that a finished draft exists. (BR-126, BR-127)<br>3. The actor confirms.<br>4. The system records the reviewer and the time, leaves the text unchanged and displays MSG391 – “Draft approved.”<br>5. From then on everyone who may read the Meeting can read the draft, and the minutes use sections I–III. |
| **Alternative Flows** | AF-1 – Approve again<br>1. At Step 2, the draft has already been reviewed and has not been corrected since.<br>2. The system applies Steps 3–4 again; the later reviewer and time replace the earlier ones.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. Nothing is reviewed. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – No finished draft<br>1. At Step 2, no draft has finished being written.<br>2. Nothing is reviewed. The system displays MSG390 – “There is no draft to correct or review.”<br>3. The use case ends.<br>EX-3 – The AI is writing the draft again<br>1. At Step 2, a new draft is being written for this Meeting.<br>2. Nothing is reviewed. The system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The flow resumes at Step 1 of the Normal Flow when the draft is ready. |

## FE-11 — Voting Management
Scope: the current Chair may prepare Vote drafts while the Meeting is Approved, Published or InProgress. Opening and casting require InProgress. Each Meeting permits at most one Open Vote, and opening requires at least one eligible voter. End Meeting is independent of Vote closure; the Chair may close and tally an existing Open Vote and record its conclusion in InProgress or Ended. Each Vote belongs directly to its Meeting. No post-meeting ballot casting, time-boxed voting, quorum or separate Resolution is provided. (BR-159, BR-160, BR-165, BR-252, BR-266, BR-267, BR-270)

### UC-62 – View Meeting Votes

_UC gom — đặc tả nằm ở UC-62a và UC-62b bên dưới._

#### UC-62a – View Meeting Votes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to browse the Votes of a Meeting. Draft Votes are visible only to the current Chair and the current Secretary. Open and Closed Votes remain viewable by users with Meeting viewing permission, including after the Meeting ends. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-016, BR-017, BR-055, BR-086, BR-087) |
| **Postconditions** | ● POST-1: The actor sees only Votes that the actor is authorized to view.<br>● POST-2: No Vote, ballot or conclusion is changed. |
| **Normal Flow** | View Meeting Votes<br>1. The actor opens the Voting area of a Meeting.<br>2. The system checks current viewing permission; the current Chair or Secretary may access Vote preparation in Approved without gaining access to other restricted Meeting records. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>3. The system lists the Votes belonging to this Meeting and excludes Draft Votes unless the actor is the current Chair or the current Secretary. Displayed counts include only visible Votes. (BR-086, BR-087)<br>4. The system displays each visible Vote with its content, ballot type and status. Open Votes show turnout; Closed Votes offer View result and show any recorded conclusion. No Agenda Item is selected or displayed as Vote ownership. (BR-086, BR-087)<br>5. The actor browses the Votes. Selecting a Vote continues to UC-62b – View Vote Status. |
| **Alternative Flows** | AF-1 – No visible votes<br>1. At Step 3, no Vote is visible to the actor, including when only Draft Votes restricted to the Chair and Secretary exist.<br>2. The system displays MSG207 – "No votes to display." without revealing hidden Draft Votes.<br>3. No data changes. The use case ends. |
| **Exception Flows** | EX-1 – Meeting votes are not accessible<br>1. At Step 2, the Meeting is unavailable or the actor no longer has permission to view its Votes, including after Guest invitation expiry or revocation.<br>2. The system displays MSG205 – "Meeting votes are not accessible." and does not reveal Vote information.<br>3. No data changes. The use case ends. |

#### UC-62b – View Vote Status

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member, Approver, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view the configuration of a visible Draft Vote, the turnout of an Open Vote, or the tally and any Chair conclusion of a Closed Vote. Viewing a result does not change that result. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>● PRE-3: The selected Vote belongs to that Meeting and is visible to the actor. Draft visibility is restricted to the current Chair and the current Secretary. (BR-086, BR-087) |
| **Postconditions** | ● POST-1: The current Vote status and the information allowed for its ballot type are displayed.<br>● POST-2: No Vote, ballot or conclusion is changed. |
| **Normal Flow** | View Open Vote Status<br>1. The actor selects an Open Vote from the Meeting Voting area.<br>2. The system verifies current Vote viewing permission and that the Vote is visible to the actor. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>3. The system displays its Meeting, content, options, public or secret ballot type, selected roles, opening time, the fixed eligible-voter count and its Vote documents. (BR-091, BR-094, BR-086, BR-087)<br>4. The system displays the number of ballots recorded against that fixed count. While the Vote is Open, individual choices and results per option are not displayed.<br>5. The actor sees the turnout change as ballots are recorded. For a secret Vote, the change does not identify the person who just voted or link that person to a choice. (BR-096, BR-097)<br>6. If the Meeting has ended while the Vote remains Open, the system shows that no further ballots are accepted; the recorded turnout remains viewable. (BR-092, BR-093) |
| **Alternative Flows** | AF-1 – View a draft vote<br>1. At Step 1, the current Chair or Secretary selects a Draft Vote.<br>2. The system applies Step 2, then displays its saved content, options, ballot type, selected roles and Vote documents. No eligible-voter list has been fixed and no ballots exist yet. (BR-091, BR-086, BR-087)<br>3. The use case ends without changing the Draft Vote.<br>AF-2 – View a closed vote<br>1. At Step 1, the actor selects View result for a Closed Vote, including a Vote with a recorded conclusion.<br>2. The system applies Step 2, then displays opening and closing times, the fixed eligible count, recorded-ballot count and tally per option. Percentages use the ballots cast; with no ballots, counts are zero and no percentage is shown. (BR-098)<br>3. For a public Vote, the system also displays each eligible voter's choice or that the person did not vote. For a secret Vote, it displays only totals and no voter-choice links. (BR-096, BR-097)<br>4. The system displays any saved Chair conclusion and optional note, or states that a conclusion has not yet been recorded. The system does not determine the conclusion from the tally. (BR-099)<br>5. The use case ends without changing the result. |
| **Exception Flows** | EX-1 – Meeting or Vote unavailable or Draft not visible<br>1. At Step 2, the Meeting or Vote is unavailable, access has been lost, or the actor attempts to view a Draft Vote without being the current Chair or Secretary.<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing the Vote.<br>3. No data changes. The use case ends. |

### UC-63 – Create / Configure Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to prepare a Vote draft, and to change or delete it, while the Meeting is Approved, Published or InProgress. Only the current Chair may open the Vote straight from the form during the Meeting (UC-64). A Vote belongs to the Meeting, not to an Agenda Item; its documents are managed in UC-68. (BR-086, BR-087, BR-090) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current authorized Chair or Secretary of the Meeting.<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: For editing or deletion, the selected Vote is Draft and belongs to this Meeting. (BR-088, BR-086, BR-087) |
| **Postconditions** | ● POST-1: Saving a draft creates or updates a Draft Vote; deleting removes only the selected Draft Vote.<br>● POST-2: Opening through UC-64 instead creates or updates an Open Vote and fixes its eligible-voter list. It does not also produce a separate saved draft.<br>● POST-3: Other Votes, previously recorded ballots and conclusions remain unchanged. |
| **Normal Flow** | Create Vote Draft<br>1. The actor selects Create Vote in the Meeting's voting area.<br>2. The system checks that the actor is the current Chair or Secretary and that the Meeting status allows drafts, then shows the Vote form. By default the Vote is Public, with the options Agree, Disagree and Abstain and all four meeting roles selected. (BR-086, BR-087)<br>3. The actor enters the content, defines and orders the options, chooses the ballot type and selects one or more eligible roles.<br>4. The actor selects Save draft.<br>5. The system checks again that the actor may still prepare drafts, and checks the content, at least two distinct non-empty options, the ballot type and at least one role. (BR-086, BR-087, BR-089)<br>6. The system saves the Vote as Draft under the Meeting. Voting is not opened and no voter list is fixed. (BR-091)<br>7. The system displays MSG210 – “Vote draft created successfully.” and the saved Draft Vote. |
| **Alternative Flows** | AF-1 – Configure an existing Draft Vote<br>1. At Step 1, the actor selects Edit on a Draft Vote.<br>2. The system shows its saved content, options, ballot type and roles. The actor changes them and selects Save draft. (BR-088)<br>3. The system performs the checks of Step 5 and checks that the Vote is still Draft. (BR-088)<br>4. The system updates the draft and displays MSG211 – “Vote draft updated successfully.” No voting is opened. The use case ends.<br>AF-2 – Delete a draft vote<br>1. At Step 1, the actor selects Delete on a Draft Vote and confirms.<br>2. The system checks that the actor may still manage drafts and that the Vote is still Draft. (BR-086, BR-087)<br>3. The system removes only that Draft Vote and displays MSG212 – “Vote draft deleted successfully.” No other Vote, ballot or conclusion changes. The use case ends.<br>AF-3 – Open immediately<br>1. At Step 4, or when saving in AF-1, the current Chair chooses to open the Vote instead of saving a draft while the Meeting is InProgress. Only the Chair is offered this choice. (BR-090)<br>2. Choosing to open confirms the opening; the flow continues with UC-64 AF-1, without a separate draft save or another confirmation.<br>3. The use case ends with the outcome of UC-64. If opening fails, no new draft or draft change is saved and the entered information stays for correction. |
| **Exception Flows** | EX-1 – Invalid Vote information<br>1. At Step 5, or the same check in AF-1, the content, options, ballot type or roles are invalid. (BR-089)<br>2. No new draft or change is saved. The system displays MSG209 – “Invalid vote information. Check the highlighted fields.” with the invalid fields identified.<br>3. The actor corrects the information. The flow resumes at Step 3 of the Normal Flow, or at Step 2 of AF-1.<br>EX-2 – Draft operation is no longer allowed<br>1. At Step 2 or Step 5, or in AF-1 or AF-2, the actor is no longer the current Chair or Secretary, the Meeting is not Approved, Published or InProgress, or the selected Vote is no longer Draft. (BR-086, BR-087, BR-088)<br>2. The draft is not created, changed or deleted. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-3 – Meeting or Vote is not accessible<br>1. At Step 2, or before an action completes, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is never disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. Nothing is changed. The system displays MSG205 – “Meeting votes are not accessible.” without revealing hidden Vote information.<br>3. The use case ends. |

### UC-64 – Open Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair to open a valid Vote during an InProgress Meeting. Opening fixes the eligible-voter list and count. Only one Vote may be Open in a Meeting at a time. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress. (BR-090)<br>● PRE-3: The selected Vote belongs to this Meeting and is Draft, or the Chair is opening a new Vote from the form in UC-63. (BR-086, BR-087)<br>● PRE-4: No other Vote in the Meeting is Open. (BR-090) |
| **Postconditions** | ● POST-1: The Vote is Open with its opening time recorded.<br>● POST-2: Its eligible-voter list and count are fixed from the selected roles at opening. (BR-091)<br>● POST-3: Eligible voters can submit one ballot while the Meeting remains InProgress and the Vote remains Open. |
| **Normal Flow** | Open Vote<br>1. The Chair selects Open on a Draft Vote.<br>2. The system shows the content, ballot type, options and selected roles of the Vote for confirmation.<br>3. The Chair confirms opening.<br>4. The system checks again that the actor is the current Chair, the Meeting is InProgress, the Vote is a valid Draft of this Meeting and no other Vote in the Meeting is Open. (BR-086, BR-087, BR-089, BR-090)<br>5. The system fixes the eligible-voter list from the people who currently hold the selected meeting roles and may access the Meeting; attendance does not count, and the list must name at least one person. (BR-090, BR-091)<br>6. The system opens the Vote and records the opening time. (BR-091)<br>7. The system displays MSG217 – “Voting opened successfully.” and authorized Meeting users see the Open Vote. |
| **Alternative Flows** | AF-1 – Open from the create or edit form<br>1. At Step 1, the Chair chooses to open the Vote from the form in UC-63. The form already shows the Vote information, so this choice confirms opening; Steps 2–3 are not repeated.<br>2. The system uses the entered information and performs Steps 4–5. An edited Vote must still be Draft. (BR-088)<br>3. Nothing from the form is saved unless the Vote opens.<br>4. The flow resumes at Step 6 of the Normal Flow. |
| **Exception Flows** | EX-1 – Configuration is invalid<br>1. At Step 4, the configuration does not satisfy required content, options, ballot type or selected roles. (BR-089)<br>2. The system displays MSG209 – "Invalid vote information. Check the highlighted fields." Voting is not opened; any saved draft remains unchanged.<br>3. The Chair returns to UC-63 to correct the draft or entered configuration. The use case ends.<br>EX-2 – Opening is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or an existing Vote is no longer Draft. (BR-090)<br>2. The system displays MSG218 – "Only the current Chair can open a Draft vote while the meeting is InProgress." No Vote is opened and existing Vote data remains unchanged.<br>3. The use case ends.<br>EX-3 – Another vote is open<br>1. At Step 4, another Vote in this Meeting is Open. (BR-090)<br>2. The system displays MSG219 – "Another vote is already open in this meeting. Close it before opening this vote." The selected Vote is not opened and the other Vote is unchanged.<br>3. The use case ends.<br>EX-4 – No eligible voter<br>1. At Step 5, nobody currently holds the selected roles with access to the Meeting. (BR-090)<br>2. Voting is not opened and any saved draft stays unchanged. The system displays MSG220 – “No eligible voters are available for the selected roles. Voting was not opened.”<br>3. The use case ends. |

### UC-65 – Cast Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an eligible person to submit one choice in an Open Vote during the Meeting. Eligibility comes from the list fixed at opening and requires current Meeting access. Attendance is not a condition. A saved ballot cannot be changed or withdrawn. |
| **Preconditions** | ● PRE-1: The internal actor is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor retains current Meeting access and is on the Vote's fixed eligible-voter list. (BR-092, BR-093, BR-091)<br>● PRE-3: The Meeting is InProgress and the Vote is Open. (BR-092, BR-093)<br>● PRE-4: The actor has not already cast a ballot in this Vote. (BR-092, BR-093)<br>● PRE-5: The actor has confirmed reading every Vote document that is still attached to the Meeting, through UC-42. (BR-094, BR-095) |
| **Postconditions** | ● POST-1: One ballot for one option is accepted, and the actor is marked as having voted.<br>● POST-2: Turnout increases by one, and the ballot cannot be changed or withdrawn.<br>● POST-3: A secret ballot does not associate the voter with the chosen option. (BR-096, BR-097) |
| **Normal Flow** | Cast Vote<br>1. The actor opens an Open Vote.<br>2. The system checks that the actor may vote now, then shows the content, the options, the ballot type and the notice that a submitted ballot cannot be changed or withdrawn. If the Vote has documents, it lists each one with the actor's read status, and the ballot can be submitted only when every document is confirmed. (BR-091, BR-092, BR-093, BR-095)<br>3. The actor selects one option and selects Submit ballot.<br>4. The system checks again the option, the Meeting and Vote status, the actor's place on the fixed voter list and current access, the read confirmations and that the actor has not voted yet. (BR-091, BR-092, BR-093, BR-095)<br>5. The system accepts the ballot, marks the actor as having voted and updates turnout. For a secret Vote, it does not associate the actor with the chosen option. (BR-092, BR-093, BR-096, BR-097)<br>6. The system displays MSG227 – “Your ballot has been recorded.” and the current turnout. The actor cannot submit another ballot; for a secret Vote, the actor sees only that a ballot was submitted. (BR-093, BR-096) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – No option selected, or the option is not valid<br>1. At Step 3, the actor attempts to submit without selecting exactly one option, or submits an option that does not belong to this Vote. (BR-092, BR-093)<br>2. No ballot is recorded. The system displays MSG222 – "Select one option before submitting your ballot." When the option does not belong to this Vote, the system displays MSG223 – “The selected option is not available for this vote.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Actor is not eligible, or has not confirmed reading the documents<br>1. At Step 2 or Step 4, the actor can view the Vote but is not on its fixed eligible list. A role change alone does not remove a person from that list; a person added after opening is not added to the list. (BR-091, BR-092, BR-093) Or, at Step 4, the actor has not confirmed that they have read every vote document.<br>2. No new ballot is recorded and previously saved ballots remain unchanged. The system displays MSG224 – "You are not eligible to cast a ballot in this vote." When the documents are not all confirmed, the system displays MSG252 – “Confirm that you have read all vote documents before casting your ballot.” and lists the documents still to read.<br>3. The use case ends.<br>EX-3 – Balloting has ended, or the actor already voted<br>1. At Step 2 or Step 4, the Vote is no longer Open or the Meeting is no longer InProgress, even if the Vote still appears Open. (BR-092, BR-093) Or, at Step 4, the actor has already voted, including two submissions sent at the same time.<br>2. No ballot is recorded and turnout is unchanged. The system displays MSG225 – "This vote is no longer accepting ballots." When the actor has already voted, the system displays MSG226 – “Your ballot has already been recorded. It cannot be changed or withdrawn.” and the recorded ballot stays unchanged.<br>3. The use case ends. |

### UC-66 – Close Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair to close an existing Open Vote and obtain its tally while the Meeting is InProgress or Ended. Closing a Vote does not end the Meeting and does not determine the Chair's conclusion. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended. (BR-098)<br>● PRE-3: The Vote belongs to the Meeting and is Open. |
| **Postconditions** | ● POST-1: The Vote is Closed and its closing time is recorded; the tally is available from the recorded ballots.<br>● POST-2: No further ballots are accepted and all previously recorded ballots are preserved.<br>● POST-3: No Passed, NotPassed or Deferred conclusion is automatically recorded. (BR-099) |
| **Normal Flow** | Close Vote<br>1. The Chair selects Close voting on an Open Vote.<br>2. The system checks current access to the Vote, displays its current turnout and asks for confirmation.<br>3. The Chair confirms closing.<br>4. The system verifies the current Chair, Meeting InProgress or Ended and Vote Open. (BR-098)<br>5. The system stops accepting ballots, records the closing time and tallies the recorded ballots per option; percentages are based on the ballots cast. (BR-098)<br>6. Every recorded ballot is kept, and a secret Vote stays secret. (BR-096)<br>7. The system displays MSG229 – “Voting closed. The result is available.” and the result (UC-62b). The Chair may then record a conclusion (UC-67). |
| **Alternative Flows** | AF-1 – Close an outstanding Vote after Meeting end<br>1. At Step 1, the Meeting is Ended and this Vote is still Open.<br>2. No ballots have been accepted since the Meeting ended. End Meeting did not close the Vote or alter its ballots. (BR-026, BR-092, BR-093)<br>3. The flow resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – Closing is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Open. (BR-098)<br>2. The system displays MSG230 – "Only the current Chair can close an Open vote while the meeting is InProgress or Ended." The Vote, ballots and any existing result remain unchanged.<br>3. The use case ends.<br>EX-2 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |

### UC-67 – Record Vote Conclusion

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair to record the business conclusion of a Closed and tallied Vote as Passed, NotPassed or Deferred, with an optional note. The Chair decides the conclusion; the system records it rather than inferring it from a majority. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended.<br>● PRE-3: The Vote is Closed and its tally is available. (BR-099)<br>● PRE-4: No conclusion has been recorded for this Vote. (BR-099) |
| **Postconditions** | ● POST-1: The selected conclusion, optional note, recording Chair and recording time are saved.<br>● POST-2: The conclusion and note cannot subsequently be edited or replaced.<br>● POST-3: The tally and conclusion are available for Meeting Minutes; the saved ballots and tally remain unchanged. |
| **Normal Flow** | Record Vote Conclusion<br>1. The Chair opens the result of a Closed Vote and reviews the tally.<br>2. The system checks current access, that the actor is the current Chair, and that the Vote is Closed without a conclusion. It then offers Passed, NotPassed and Deferred and an optional conclusion note. (BR-099)<br>3. The Chair selects one conclusion, optionally enters a note and confirms recording.<br>4. The system verifies the current Chair, Meeting InProgress or Ended, the Closed and tallied Vote, an allowed conclusion and that no conclusion has already been recorded. (BR-099)<br>5. The system records the conclusion and optional note together with the Chair and recording time. It does not change ballots or the tally. (BR-099)<br>6. The system displays MSG235 – "Vote conclusion recorded successfully." and makes the conclusion read-only. The tally and conclusion are available for Meeting Minutes. |
| **Alternative Flows** | AF-1 – Record after Meeting end<br>1. At Step 1, the Meeting is Ended and the Vote has been Closed and tallied, including a Vote closed after Meeting end.<br>2. The Chair records the outcome of that existing Vote. No new voting is opened or ballot accepted.<br>3. The flow resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – Conclusion is invalid<br>1. At Step 4, no conclusion is selected or the selection is not Passed, NotPassed or Deferred. (BR-099)<br>2. No conclusion is recorded. The system displays MSG232 – "Select Passed, NotPassed or Deferred."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Conclusion already recorded<br>1. At Step 2 or Step 4, the Vote already has a saved conclusion. (BR-099)<br>2. The existing conclusion, note and tally remain unchanged. The system displays MSG233 – "The vote conclusion has already been recorded and cannot be changed."<br>3. The use case ends.<br>EX-3 – Recording is no longer allowed<br>1. At Step 2 or Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Closed with a tally available. (BR-099)<br>2. No conclusion is recorded and existing Vote data remains unchanged. The system displays MSG234 – "Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended."<br>3. The use case ends. |

### UC-68 – Manage Vote Documents

_UC gom — đặc tả nằm ở các UC con UC-68a, UC-68b… bên dưới._

#### UC-68a – Attach Documents to Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to attach one or more documents to a Draft Vote, so that eligible voters must confirm reading them before casting a ballot (UC-42, UC-65). Documents come from the Meeting documents, from the computer or from My Documents, as in UC-36b; documents not yet in the Meeting are added to it in the same save. |
| **Preconditions** | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-070, BR-071, BR-086, BR-087)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: The selected Vote belongs to the Meeting and is Draft. (BR-094, BR-095) |
| **Postconditions** | ● POST-1: The selected documents are Vote documents of the Draft Vote; its other Vote documents remain unchanged.<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. If any document cannot be added or attached, nothing from the submission is added or attached.<br>● POST-3: The Vote content, options, ballot type and roles remain unchanged. The Vote document list is fixed when the Vote opens. (BR-094, BR-095) |
| **Normal Flow** | Attach Documents to Vote<br>1. The actor chooses to attach documents to a Draft Vote and chooses the Meeting documents as the source.<br>2. The system lists the Meeting documents not yet attached to this Vote.<br>3. The actor selects one or more documents and confirms.<br>4. The system checks again the actor's assignment, the Meeting status, that the Vote is still Draft and that every selected document is still in the Meeting and not yet attached to the Vote. (BR-070, BR-071, BR-086, BR-087, BR-094)<br>5. The system attaches all selected documents together; if one cannot be attached, none is.<br>6. The system displays MSG265 – “Documents attached to the vote successfully.” |
| **Alternative Flows** | AF-1 – Upload from computer<br>1. At Step 1, the actor chooses to upload files from the computer and submits them.<br>2. The system checks that every file is a PDF or DOCX no larger than 10 MB and performs the checks of Step 4 that apply. (BR-069, BR-070, BR-071, BR-094)<br>3. The system adds the files to the Meeting and to the uploader's personal library and attaches them to the Vote in the same save. (BR-072, BR-073)<br>4. If a file is invalid or cannot be added, nothing from the submission is added or attached and the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.” Otherwise the flow resumes at Step 6 of the Normal Flow.<br>AF-2 – Choose from My Documents<br>1. At Step 1, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the checks of Step 4. (BR-070, BR-071, BR-072, BR-073, BR-094)<br>3. The system adds the documents to the Meeting and attaches them to the Vote in the same save; the personal library is unchanged.<br>4. If a document cannot be added, nothing is added or attached and the system displays MSG249 – “Select one or more available documents from your personal library.” A document already in the Meeting is attached through the Normal Flow. Otherwise the flow resumes at Step 6 of the Normal Flow. |
| **Exception Flows** | EX-1 – Vote documents can no longer be changed<br>1. At Step 4, or the same check in AF-1 or AF-2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-086, BR-087, BR-094)<br>2. Nothing is added or attached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer in the Meeting<br>1. At Step 4, a selected document is no longer in the Meeting.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Document is already attached<br>1. At Step 4, one or more selected documents are already attached to the Vote, including by another user after selection.<br>2. Nothing is attached and no duplicate is created. The system displays MSG266 – “One or more selected documents are already attached to the vote.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

#### UC-68b – Detach Document from Vote

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to detach one document from a Draft Vote. The document stays in the Meeting; removing it from the Meeting is UC-36c. |
| **Preconditions** | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-070, BR-071, BR-086, BR-087)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: The document is a Vote document of a Draft Vote of the Meeting. (BR-094, BR-095) |
| **Postconditions** | ● POST-1: Only the selected Vote document is detached from the Vote. (BR-094, BR-095)<br>● POST-2: The document stays in the Meeting; its Agenda Item links, contents, comments and read confirmations remain unchanged. |
| **Normal Flow** | Detach Document from Vote<br>1. The actor selects a document of a Draft Vote, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, the permitted Meeting status, that the Vote is still Draft and that the document is still attached to it. (BR-070, BR-071, BR-086, BR-087, BR-094, BR-095)<br>3. The system detaches only that document from the Vote; the document stays in the Meeting.<br>4. The system refreshes the Vote documents and displays MSG268 – “Document detached from the vote successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Vote documents can no longer be changed<br>1. At Step 2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-086, BR-087, BR-094, BR-095)<br>2. Nothing is detached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer attached to the Vote<br>1. At Step 2, the document has already been detached from the Vote or removed from the Meeting.<br>2. Nothing else changes. The system displays MSG269 – “This document is no longer attached to the vote. Refresh the list.”<br>3. The use case ends.<br>EX-3 – Meeting or Vote is not accessible<br>1. Before displaying or saving, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. Nothing is detached. The system displays MSG205 – "Meeting votes are not accessible."<br>3. The use case ends. |

---

## FE-12 — Meeting Minutes & Approval

### UC-69 – View Meeting Minutes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary; every other internal person who may read the Meeting |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to read the latest revision of the minutes, and every other internal person who may read the Meeting to read the latest issued revision. Drafts, revisions waiting for approval and returned revisions are seen by the current Chair and Secretary only. Issued and superseded revisions can be downloaded as a Word or PDF file, except for a Secret Meeting. (BR-131, BR-132, BR-139) |
| **Preconditions** | ● PRE-1: The actor is signed in and may read the Meeting. (BR-139)<br>● PRE-2: The actor is its current Chair or current Secretary, and a revision of the minutes exists; or the actor is anyone else who may read the Meeting, and an issued revision exists. (BR-131, BR-139) |
| **Postconditions** | ● POST-1: The revision of the minutes the actor may read and its state are displayed.<br>● POST-2: No content, state or Meeting record is changed.<br>● POST-3: Nobody learns of a draft, a revision waiting for approval, a returned revision or a correction that is not issued yet. (BR-139) |
| **Normal Flow** | View Meeting Minutes<br>1. The actor opens the minutes of a Meeting that has Ended.<br>2. The system checks the actor's assignment or access to the Meeting and finds the revision the actor may read: the latest revision for the current Chair and Secretary, the latest issued revision for everyone else. (BR-131, BR-139)<br>3. The system shows the minutes as they will be printed.<br>4. The system shows the revision number, its status, who created it, when it was last saved, and who approved and issued it and when. The current Chair and Secretary also see who submitted or returned it and the reason of the latest return. (BR-135, BR-136)<br>5. When the minutes have several revisions, the actor may open another one: the current Chair and Secretary any revision, everyone else an issued or superseded one. (BR-138, BR-139)<br>6. The actor may download an issued or superseded revision as a Word or PDF file. (BR-139) |
| **Alternative Flows** | AF-1 – No minutes yet<br>1. At Step 2, no revision exists that the actor may read.<br>2. The current Chair or Secretary is offered to build the draft (UC-70); everyone else sees that no minutes have been issued yet.<br>3. The use case ends.<br>AF-2 – Read an earlier revision<br>1. At Step 5, the actor chooses an earlier revision.<br>2. The system shows the chosen revision with its own number and status; a revision replaced by a correction is shown as superseded and stays readable. (BR-137, BR-138)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – Download an issued revision as a reader<br>1. At Step 6, the actor is neither the current Chair nor the current Secretary, and the revision is issued or superseded.<br>2. The system checks that the actor may read the Meeting, is not a Guest and that the Meeting is not Secret, and delivers the file built from the same content. (BR-132, BR-139)<br>3. The revision is unchanged. The use case ends. |
| **Exception Flows** | EX-1 – Revision is not readable by the actor<br>1. At Step 2 or Step 5, the actor is neither the current Chair nor the current Secretary and asks for a draft, a revision waiting for approval, a returned revision or a correction that is not issued yet. (BR-139)<br>2. The system answers as if the revision did not exist and discloses nothing about it.<br>3. The use case ends.<br>EX-2 – Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends.<br>EX-3 – Minutes are Secret and download requested<br>1. At Step 6 or AF-3 Step 2, the Meeting is Secret. (BR-132)<br>2. No file is delivered; the minutes are read on the screen only. The system displays MSG405 – “The minutes of a Secret meeting cannot be downloaded.”<br>3. The use case ends. |

### UC-70 – Generate Meeting Minutes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to build the draft of the minutes from the one fixed template and the Meeting's records, using the meeting's current AI draft, reviewed or not, for sections I–III when there is one. (BR-130) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The Meeting has Ended. (BR-131)<br>● PRE-3: No minutes exist yet, or the latest revision is a draft or was returned by the Chair. (BR-135, BR-136) |
| **Postconditions** | ● POST-1: A draft of the minutes exists, filled from the Meeting's records.<br>● POST-2: Building again replaces the whole content of the draft, including every correction.<br>● POST-3: No other Meeting record is changed. |
| **Normal Flow** | Generate Meeting Minutes<br>1. The actor asks to build the draft of the minutes.<br>2. The system checks the assignment, the Meeting status and that no minutes exist yet or the latest revision is a draft or was returned. (BR-131, BR-135, BR-136)<br>3. The system fills the fixed template from the Meeting's records — time, place, attendance, Chair, Secretary, agenda with the recorded conclusions, and the results and conclusions of closed votes — and fills sections I–III from the Meeting's current AI draft when there is one. (BR-130)<br>4. The system saves the draft with its revision number and displays MSG395 – “Minutes draft created.” |
| **Alternative Flows** | AF-1 – Build again over an existing draft<br>1. At Step 1, a draft or a returned revision already exists.<br>2. The system warns that every correction will be replaced and asks for confirmation.<br>3. The actor confirms and the flow resumes at Step 2 of the Normal Flow.<br>AF-2 – No AI draft<br>1. At Step 3, no AI draft exists yet, or the AI has not finished writing it.<br>2. The system leaves blanks where the Secretary must write.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – AI draft not yet reviewed<br>1. At Step 3, the Meeting's AI draft exists but has not been reviewed.<br>2. The system uses the draft as it is, marks the minutes as built from an unreviewed draft and offers the way to review it (UC-61).<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-4 – AI draft changed after the minutes were built<br>1. At Step 1, the AI draft was made again or corrected after the minutes were built, or appeared after minutes built without one.<br>2. The system shows that the minutes are out of date and offers to build them again as in AF-1.<br>3. The use case ends. |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is neither the current Chair nor the current Secretary. (BR-131)<br>2. No draft is built. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – Meeting has not ended<br>1. At Step 2, the Meeting has not Ended. (BR-131)<br>2. No draft is built. The system displays MSG398 – “The minutes can be written only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – A draft already exists<br>1. At Step 2, a draft exists and the actor did not ask to replace it.<br>2. Nothing is built and the existing draft stays as it is. The system displays MSG399 – “This meeting already has a minutes draft.”<br>3. The use case ends. |

### UC-71 – Edit Meeting Minutes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to correct the content of the minutes draft, see the paper as it will print, save it, and download the same content as a Word or PDF file. |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The latest revision of the minutes is a draft or was returned by the Chair; a correction draft counts. (BR-135, BR-136) |
| **Postconditions** | ● POST-1: The corrected text is saved with the new time it was saved.<br>● POST-2: No other revision or Meeting record is changed.<br>● POST-3: Downloading a Word or PDF file leaves the draft unchanged. |
| **Normal Flow** | Edit Meeting Minutes<br>1. The actor opens the minutes draft and chooses to edit it.<br>2. The system shows the content in an editor next to the paper preview built from the same content; the preview follows the text as the actor types.<br>3. The actor corrects the text and saves it.<br>4. The system checks that the revision is still a draft or was returned, that the content is valid and that nobody has saved since the actor read it. (BR-135, BR-136)<br>5. The system saves the content with the new save time and displays MSG396 – “Minutes saved.” |
| **Alternative Flows** | AF-1 – Download the minutes as a Word file<br>1. At Step 1 or after saving, the actor downloads the minutes as a Word file. The current Chair and Secretary may download any revision; everyone else who may read the Meeting an issued or superseded one. (BR-139)<br>2. The system checks that the actor may download that revision and that the Meeting is not Secret, and delivers the Word file built from the same content. (BR-132)<br>3. The revision is unchanged. The use case ends.<br>AF-2 – Download the minutes as a PDF file<br>1. At Step 1 or after saving, the actor downloads the minutes as a PDF file, with the same rights as in AF-1. (BR-139)<br>2. The system checks that the actor may download that revision and that the Meeting is not Secret, and delivers the PDF file built from the same content. (BR-132)<br>3. The revision is unchanged. The use case ends. |
| **Exception Flows** | EX-1 – Someone else saved first<br>1. At Step 4, the draft was saved by another person after the actor read it.<br>2. Nothing is overwritten. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – Content is invalid<br>1. At Step 4, the content is empty, longer than 300,000 characters or not well formed.<br>2. Nothing is saved. The system displays MSG402 – “The minutes content is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Minutes can no longer be changed<br>1. At Step 4, the revision is waiting for approval or has been issued.<br>2. Nothing is saved. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends. |

### UC-72 – Submit Meeting Minutes for Approval

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | Chair (notified) |
| **Description** | Allows the current Secretary to send the finished minutes to the current Chair for approval. From then on nobody can correct or rebuild the revision until the Chair approves or returns it. The Secretary cannot take back a revision once it is waiting for approval (out of scope). (BR-135, BR-136) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-135, BR-136)<br>● PRE-2: The Meeting has Ended. (BR-135, BR-136)<br>● PRE-3: The latest revision of the minutes is a draft or was returned by the Chair. (BR-135, BR-136) |
| **Postconditions** | ● POST-1: The revision is waiting for approval, with who submitted it and when.<br>● POST-2: The revision can no longer be corrected or rebuilt.<br>● POST-3: The current Chair is notified.<br>● POST-4: The Meeting status is unchanged. (BR-137, BR-138)<br>● POST-5: If the submission fails, nothing changes and nobody is notified. |
| **Normal Flow** | Submit Meeting Minutes for Approval<br>1. The actor opens the minutes, chooses to submit them for approval and confirms.<br>2. The system checks the assignment, that the Meeting has Ended, that the latest revision is a draft or was returned, and that it is the copy the actor read. (BR-135, BR-136)<br>3. The system marks the revision as waiting for approval, records who submitted it and when, and notifies the current Chair.<br>4. The system displays MSG424 – “Minutes submitted to the Chair for approval.” |
| **Alternative Flows** | AF-1 – Places are still left blank<br>1. At Step 1, the text still holds “……” where the Secretary was to write.<br>2. The system warns that some places are still blank and asks for confirmation; blanks do not block the submission.<br>3. The actor confirms and the flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Submit again after the Chair returned the minutes<br>1. At Step 1, the latest revision was returned; the system shows the Chair's reason with the minutes. (BR-135, BR-136)<br>2. The actor corrects the text (UC-71) or submits it unchanged.<br>3. The flow resumes at Step 2 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is the current Chair, not the Secretary<br>1. At Step 2, the actor is the current Chair but not the current Secretary. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG425 – “Only the Secretary may submit the minutes for approval.”<br>3. The use case ends.<br>EX-2 – Meeting has not ended<br>1. At Step 2, the Meeting is still InProgress. (BR-135, BR-136)<br>2. The revision stays a draft. The system displays MSG426 – “The minutes can be submitted only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – Revision was already submitted or issued<br>1. At Step 2, the latest revision is waiting for approval or has been issued.<br>2. Nothing changes. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends. |

### UC-73 – Approve and Issue Meeting Minutes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary, and everyone who may read the issued minutes (notified) |
| **Description** | Allows the current Chair to approve the minutes waiting for approval. Approval issues them at once: they are locked, the people who may read the Meeting are notified, and the issue is recorded in the audit log. (BR-135, BR-136, BR-137, BR-138, BR-010) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-135, BR-136)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is issued, with who approved it and when, and can never be changed again. (BR-137, BR-138)<br>● POST-2: When the revision corrects an earlier issued one, the earlier one is marked superseded and stays readable. (BR-137, BR-138)<br>● POST-3: Everyone who may read the issued minutes except the Chair who issued them is notified. (BR-139)<br>● POST-4: The issue is recorded in the audit log with who issued it. (BR-010)<br>● POST-5: The Meeting stays Ended. (BR-137, BR-138) |
| **Normal Flow** | Approve and Issue Meeting Minutes<br>1. The actor opens the minutes waiting for approval, from the notification or the Meeting, and reads them as they will be printed.<br>2. The actor chooses to approve and issue them and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval and that it is the copy the actor read. (BR-135, BR-136)<br>4. The system marks the revision as issued and records who approved it and when. (BR-137, BR-138)<br>5. The system records the issue in the audit log with who issued it. (BR-010)<br>6. The system notifies everyone who may read the issued minutes, except the Chair who issued them and Guests, in the application and by email; a correction states its number. (BR-139)<br>7. The system displays MSG427 – “Minutes approved and issued.” |
| **Alternative Flows** | AF-1 – Issue a correction<br>1. At Step 4, the revision corrects an earlier issued revision.<br>2. The system marks the earlier revision as superseded; it stays readable in the list of revisions. (BR-137, BR-138)<br>3. The flow resumes at Step 5 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – Minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has already been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – Minutes changed after the actor opened them<br>1. At Step 3, the revision is not the copy the actor read.<br>2. Nothing is issued. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Normal Flow. |

### UC-74 – Return Meeting Minutes for Revision

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | Secretary (notified) |
| **Description** | Allows the current Chair to send the minutes waiting for approval back to the Secretary with a reason, so the Secretary can correct them and submit them again. Each return keeps only its own reason. (BR-135, BR-136) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-135, BR-136)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |
| **Postconditions** | ● POST-1: The revision is returned, with the reason, who returned it and when. (BR-135, BR-136)<br>● POST-2: The reason of this return replaces the reason of an earlier one. (BR-135, BR-136)<br>● POST-3: The current Chair and the current Secretary can correct the revision again and the Secretary can submit it again. (BR-135, BR-136)<br>● POST-4: The current Secretary is notified. If the return fails, nothing changes and nobody is notified. |
| **Normal Flow** | Return Meeting Minutes for Revision<br>1. The actor chooses to return the minutes waiting for approval.<br>2. The system asks for the reason; the actor enters it and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval, that it is the copy the actor read and that the reason is not empty and within 2,000 characters. (BR-135, BR-136)<br>4. The system marks the revision as returned and records the reason, who returned it and when; the reason of any earlier return is replaced. (BR-135, BR-136)<br>5. The system notifies the current Secretary.<br>6. The system displays MSG430 – “Minutes returned to the Secretary.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – Minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – Reason is missing or too long<br>1. At Step 3, the reason is empty or longer than 2,000 characters. (BR-135, BR-136)<br>2. Nothing changes. The system displays MSG431 – “Enter a reason for returning the minutes, up to 2,000 characters.”<br>3. The flow resumes at Step 2 of the Normal Flow. |

### UC-75 – Correct Issued Meeting Minutes

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or the current Secretary to start a correction of issued minutes. The correction is a new revision that starts from the issued text; the issued revision is never changed and stays the one people read until the correction is issued. (BR-137, BR-138) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The selected revision of the minutes is issued. (BR-137, BR-138) |
| **Postconditions** | ● POST-1: A new draft revision exists, numbered after the issued one, holding the issued text and pointing at the revision it corrects. (BR-137, BR-138)<br>● POST-2: The issued revision is unchanged and stays the one other people read. (BR-137, BR-138)<br>● POST-3: Nobody is notified until the correction is issued (UC-73). |
| **Normal Flow** | Correct Issued Meeting Minutes<br>1. The actor chooses to create a correction of the issued revision and confirms.<br>2. The system checks the assignment, that the selected revision is issued and that no later revision of these minutes is still unissued. (BR-131, BR-137, BR-138)<br>3. The system creates the new draft revision from the issued text, numbered after it. (BR-137, BR-138)<br>4. The system shows the correction draft, ready to be edited (UC-71), and displays MSG432 – “Correction draft created.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the selected revision (it is issued or superseded) but is neither the current Chair nor the current Secretary. (BR-131, BR-139)<br>2. No revision is created. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – Selected revision is not issued<br>1. At Step 2, the selected revision is a draft, waiting for approval, returned, or superseded by a later issued revision. (BR-137, BR-138)<br>2. No revision is created. The system displays MSG433 – “Only issued minutes can be corrected.”<br>3. The use case ends.<br>EX-3 – A correction is already in progress<br>1. At Step 2, the selected revision is issued, but a later revision correcting it has not been issued yet, or a second actor starts a correction of the same revision at the same moment. (BR-137, BR-138)<br>2. No second revision is created. The system displays MSG434 – “A correction of these minutes is already in progress.”<br>3. The use case ends. |

---

## FE-13 — Email & In-App Notification

### UC-76 – View Notifications

Sequence Diagram: [UC76_ViewNotifications.txt](../06-sequencediagram/notification-management/UC76_ViewNotifications.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authenticated user to view their list of in-app notifications and check the unread notification count. |
| **Preconditions** | ● PRE-1: The user is authenticated in the system. |
| **Postconditions** | ● POST-1: The system displays the user's notifications and unread badge count.<br>● POST-2: Opening the notification list does not change any notification’s read status. |
| **Normal Flow** | View Notifications<br>1. The user selects the notification bell from the navigation header.<br>2. The system retrieves the notifications and unread badge count for the user's account.<br>3. The system displays the user's notifications, indicates which ones are unread, and shows the unread count. Notifications are kept for later viewing; a new notification and the new unread count appear while the user is signed in. |
| **Alternative Flows** | N/A |
| **Exception Flows** | N/A |

### UC-77 – Mark Notifications as Read

_UC gom — đặc tả nằm ở các UC con UC-77a, UC-77b… bên dưới._

#### UC-77a – Mark Notification as Read

Sequence Diagram: [UC77a_MarkNotificationAsRead.txt](../06-sequencediagram/notification-management/UC77a_MarkNotificationAsRead.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authenticated user to mark a specific unread notification as read, updating its status. |
| **Preconditions** | ● PRE-1: The user is authenticated in the system.<br>● PRE-2: The user has at least one unread notification. |
| **Postconditions** | ● POST-1: The selected notification status is changed to Read.<br>● POST-2: The unread count reflects the latest saved notification statuses. |
| **Normal Flow** | Mark Notification as Read<br>1. The user views their notification list.<br>2. The user selects "Mark as Read" for a notification.<br>3. The system verifies that the notification belongs to the user's account. (BR-033, BR-034)<br>4. The system marks the notification as read.<br>5. The system displays the notification as read and shows the updated unread count. |
| **Alternative Flows** | N/A |
| **Exception Flows** | N/A |

#### UC-77b – Mark All Notifications as Read

Sequence Diagram: [UC77b_MarkAllNotificationsAsRead.txt](../06-sequencediagram/notification-management/UC77b_MarkAllNotificationsAsRead.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a user to mark all of their unread notifications as read. |
| **Preconditions** | ● PRE-1: The user is signed in. |
| **Postconditions** | ● POST-1: All notifications belonging to the user's account that were unread when the request was processed are marked as read.<br>● POST-2: The displayed unread count reflects the latest saved state. |
| **Normal Flow** | Mark All Notifications as Read<br>1. The user selects the notification bell and opens the notification list.<br>2. The user selects "Mark All as Read".<br>3. The system marks all unread notifications belonging to the user's account as read, including those not currently displayed. (BR-033, BR-034)<br>4. The system refreshes the displayed read statuses and unread count. |
| **Alternative Flows** | AF-1 – No unread notifications<br>1. At Step 3 of the Normal Flow, the system finds no unread notifications belonging to the user's account.<br>2. The system displays the current list and an unread count of zero.<br>3. The use case ends. |
| **Exception Flows** | N/A |

### UC-78 – Open Notification Target

Sequence Diagram: [UC78_OpenNotificationTarget.txt](../06-sequencediagram/notification-management/UC78_OpenNotificationTarget.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a user to open the meeting linked to one of their notifications. |
| **Preconditions** | ● PRE-1: The user is signed in. |
| **Postconditions** | ● POST-1: The user sees the linked meeting.<br>● POST-2: The selected notification is marked as read; opening the meeting does not depend on that. |
| **Normal Flow** | Open Notification Target<br>1. The user opens the notification list and selects a notification.<br>2. The system checks that the notification belongs to the user's account and finds its linked meeting. (BR-033, BR-034)<br>3. The system marks the notification as read; if the read status cannot be saved, the meeting still opens.<br>4. The system checks whether the user currently has permission to view the linked meeting. (BR-016, BR-017, BR-033, BR-034)<br>5. The system opens the meeting. |
| **Alternative Flows** | AF-1 – Notification is already read<br>1. At Step 3 of the Normal Flow, the notification is already read, so its status and the unread count remain unchanged.<br>2. The flow resumes at Step 4 of the Normal Flow. |
| **Exception Flows** | N/A |

---

## FE-14 — Reporting & Statistics

### UC-79 – View & Export Reports

_UC gom — đặc tả nằm ở các UC con UC-79a, UC-79b… bên dưới._

#### UC-79a – View Reports & Statistics

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | N/A |
| **Description** | Allows a signed-in internal user to see aggregate figures about the meetings they may read over a chosen period — meetings per month, by status and by sensitivity, the average attendance rate, the average meeting duration in minutes (BR-151) and the average time from the end of a meeting to the issue of its minutes — so that meeting activity can be followed without opening each meeting. The page then lets the actor query the main tables (meetings, participants, votes, documents, minutes, recordings) by choosing the table, the grouping, the top groups, the filters and the ordering, with a Total row. (BR-149, BR-150, BR-152, BR-151, BR-153) |
| **Preconditions** | ● PRE-1: The actor is signed in with an internal account. (BR-149, BR-150) |
| **Postconditions** | ● POST-1: The actor sees the figures of the chosen period, computed only from the Meetings the actor may read. (BR-149, BR-150)<br>● POST-2: The manager figures show no meeting title, participant or content. In the queried table, a row grouped by person or by meeting shows only the person’s name or the meeting code and title — only for meetings the actor may read — and never the meeting content; a Guest appears by name only, without email. (BR-152)<br>● POST-3: Nothing in the system is changed. |
| **Normal Flow** | View Reports & Statistics<br>1. The actor opens the Reports page.<br>2. The system selects the 12 months ending with the current month and the default table with its default grouping.<br>3. The system checks the period and the actor's choice of table, grouping, ordering, number of groups and filters.<br>4. The system counts the Meetings the actor may read whose planned start falls within the period and shows them per month, by status and by sensitivity, with the average attendance rate, the average meeting duration in minutes and the average time from the end of a Meeting to the issue of its minutes, each with the number of Meetings it is based on. (BR-149, BR-150, BR-151)<br>5. The system shows the chosen table with only what the actor may see, grouped, ordered and limited to the chosen top groups, with a Total row. (BR-151, BR-153) |
| **Alternative Flows** | AF-1 – Choose another period<br>1. At Step 2 or after Step 5, the actor chooses a start month and an end month.<br>2. The flow resumes at Step 3 of the Normal Flow with that period.<br>AF-2 – No Meeting in the period<br>1. At Step 4, the actor may read no Meeting whose planned start falls within the period.<br>2. The system shows that there are no meetings and displays MSG466 – “There are no meetings you can view in this period.” No all-zero tables are shown.<br>3. The use case ends.<br>AF-3 – An average has no data<br>1. At Step 4, no Ended Meeting in the period had attendance taken, none has issued minutes, or none has both an actual start and an actual end.<br>2. The system shows that average as having no data instead of a number. (BR-151)<br>3. The use case ends.<br>AF-4 – Choose another table, grouping or filter<br>1. At Step 2 or after Step 5, the actor chooses another table, grouping, ordering, number of groups or filter.<br>2. The flow resumes at Step 3 of the Normal Flow with that choice. |
| **Exception Flows** | EX-1 – The period is not valid<br>1. At Step 3, the start month is after the end month, or the period is longer than 24 months.<br>2. No figure is computed. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The flow resumes at Step 1 of AF-1.<br>EX-2 – The report choice is not valid<br>1. At Step 3, the chosen table, grouping, ordering, number of groups or filter is not available for that table.<br>2. Nothing is read. The system displays MSG484 – “The report parameters are invalid. Choose the data, grouping or filter again.”<br>3. The use case ends. |

#### UC-79b – Export Report

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Member, Approver, Chair, Secretary, Participant |
| **Secondary Actors** | N/A |
| **Description** | Allows the actor to download the table shown on the page for the chosen period as an Excel file, to keep it or to work on it outside the system. The file is named bao-cao-<table>_<from>_<to>.xlsx. (BR-010) |
| **Preconditions** | ● PRE-1: The actor is signed in with an internal account. (BR-149, BR-150)<br>● PRE-2: The actor is on the Reports page with a period and a table chosen. (UC-79a) |
| **Postconditions** | ● POST-1: The actor receives an Excel file holding the same table as the page for that period and that query, with its Total row, computed at the time of export. (BR-010)<br>● POST-2: The file holds no meeting content; rows grouped by person or by meeting follow the same rule as UC-79a POST-2. (BR-152)<br>● POST-3: The export is recorded with the actor, the table, the grouping, the filters, the period and the time. (BR-010) |
| **Normal Flow** | Export Report<br>1. The actor selects Export to Excel.<br>2. The system checks the period and the choices of the table shown.<br>3. The system builds the Excel file of that table with its Total row and records the export. (BR-152, BR-010)<br>4. The actor receives the file. |
| **Alternative Flows** | AF-1 – No Meeting in the period<br>1. At Step 2, the actor may read no Meeting in the period.<br>2. The file is still built, with zero counts and no averages.<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – The period is not valid<br>1. At Step 2, the period is not valid.<br>2. No file is built. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The use case ends.<br>EX-2 – The report choice is not valid<br>1. At Step 2, the chosen table, grouping, ordering, number of groups or filter is not available for that table.<br>2. No file is built. The system displays MSG484 – “The report parameters are invalid. Choose the data, grouping or filter again.”<br>3. The use case ends. |

## FE-15 — Working Calendar & Meeting Time Search

### UC-80 – Manage Calendar Sharing

_UC gom — đặc tả nằm ở các UC con UC-80a, UC-80b… bên dưới._

#### UC-80a – Request Calendar Share

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member (the person who will create the meeting) |
| **Secondary Actors** | User — the calendar owner (every active internal account except the requester) |
| **Description** | Allows a Member to ask another user to share working hours as busy/free, so that free slots can be found without asking each person. The owner answers in UC-80c. (BR-140, BR-141, BR-142, BR-143) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141)<br>● PRE-2: No share of the owner’s calendar to the actor is Pending or Effective. (BR-142, BR-143) |
| **Postconditions** | ● POST-1: A Pending share exists and the owner is notified. (BR-142, BR-143, BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Request Calendar Share<br>1. The actor chooses the calendar owner among the other active internal accounts, a level (Level 1 or Level 2) and a date range; the request can also start from a person shown as not sharing in UC-81a.<br>2. The system checks that the actor is still a Member (else EX-1), that the owner is an active internal account different from the actor, that no Pending or Effective share of the owner’s calendar to the actor exists (else EX-2) and that the range is valid (else EX-3). A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends. (BR-140, BR-141, BR-142, BR-143)<br>3. The system saves the Pending share, records it in the audit log and notifies the owner. (BR-010) The system displays MSG435 – “Calendar share request sent.” The owner answers in UC-80c. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Requester has no right to create meetings<br>1. At Step 2, the actor is no longer a Member. (BR-140, BR-141)<br>2. Nothing is saved. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the owner’s calendar to the actor already exists. (BR-142, BR-143)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 2, the date range is invalid.<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends. |

#### UC-80b – Share Calendar Directly

| Mục | Nội dung |
|---|---|
| **Primary Actors** | User — the calendar owner (every active internal account) |
| **Secondary Actors** | Member — the recipient |
| **Description** | Allows a calendar owner to share working hours directly with a Member, always at Level 1. The recipient answers in UC-80c; the recipient asks for Level 2 through UC-80d after accepting. (BR-140, BR-141, BR-142, BR-143) |
| **Preconditions** | ● PRE-1: The actor is signed in with an active internal account. (BR-140, BR-141)<br>● PRE-2: No share of the actor’s calendar to the recipient is Pending or Effective. (BR-142, BR-143) |
| **Postconditions** | ● POST-1: A Pending share at Level 1 exists and the recipient is notified. (BR-140, BR-141, BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141) |
| **Normal Flow** | Share Calendar Directly<br>1. The actor chooses a recipient who is a Member and a date range; the level is always Level 1.<br>2. The system checks the recipient (active account, still a Member — else EX-1), that no Pending or Effective share of the actor’s calendar to the recipient exists (else EX-2) and that the range is valid (else EX-3). A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends. (BR-140, BR-141, BR-142, BR-143)<br>3. The system saves the Pending share at Level 1, records it in the audit log and notifies the recipient. (BR-010) The system displays MSG441 – “Calendar shared. Waiting for acceptance.” The recipient answers in UC-80c. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Recipient cannot receive a share<br>1. At Step 2, the recipient is not a Member or is not active.<br>2. Nothing is saved. The system displays MSG455 – “This person cannot receive a calendar share.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the actor’s calendar to the recipient already exists. (BR-142, BR-143)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 2, the date range is invalid.<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends. |

#### UC-80c – Respond to Calendar Share

| Mục | Nội dung |
|---|---|
| **Primary Actors** | User — the asked side (the calendar owner, for a UC-80a request or a UC-80d change; the receiving Member, for a UC-80b direct share) |
| **Secondary Actors** | The sender (receives a notification about the answer) |
| **Description** | Allows the asked person to accept or decline a calendar share request, a direct share or a pending change. Accepting a UC-80a request may lower the level or shorten the range. A Pending request whose sender’s account is deactivated can only be declined (BR-142, BR-143). (BR-140, BR-141, BR-142, BR-143, BR-010) |
| **Preconditions** | ● PRE-1: The actor is the asked side of a Pending share or pending change.<br>● PRE-2: The share or change is still Pending. (BR-142, BR-143) |
| **Postconditions** | ● POST-1: The share is Effective with the chosen level/range, or closed, or kept as it was for a declined change; the sender is notified in every case. (BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Respond to Calendar Share<br>1. The actor opens the Pending request, from the notification or the list of shares, and sees who sent it, the level and the date range.<br>2. The system checks that the actor is the asked side (else EX-1), that the share is still Pending (else EX-2). An Effective share whose viewer lost the Member right was stopped at that moment; a Pending one is kept and can only be declined (BR-142, BR-143).<br>3. On accept the system marks the share Effective — for a UC-80a request the owner may choose a lower level or a shorter range within the requested one (BR-140, BR-141) — records it in the audit log and notifies the sender. The system displays MSG436 – “Calendar share accepted.”<br>4. On decline, after confirming, the system closes the share, records it in the audit log and notifies the sender. The system displays MSG437 – “Calendar share request declined.” |
| **Alternative Flows** | AF-1 – Answer a pending change (UC-80d)<br>1. At Step 3, the pending item is a change (raise to Level 2 or extend the range) instead of a new share.<br>2. On approval, the system checks that the requested range still fits the current start date; if it does not, the change cannot be approved (MSG450 – “This action is not allowed in the current state of the share”). Otherwise the system applies the new level or range, records it in the audit log, notifies the viewer and displays MSG442 – “Calendar share updated.”<br>3. On decline, everything stays as it was and both sides are notified. The system displays MSG452 – “Calendar share change declined.” |
| **Exception Flows** | EX-1 – Actor is not the asked side<br>1. At Step 2, the actor is not the asked side of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Share is no longer Pending<br>1. At Step 2, the share or change is no longer Pending, or its end date has passed (BR-142, BR-143), or the viewer lost the Member right (BR-140, BR-141). (BR-142, BR-143)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 3, the owner shortens the range to an invalid range.<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow. |

#### UC-80d – Change Calendar Share

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member (the viewer) and User — the calendar owner (depending on who acts) |
| **Secondary Actors** | The other side of the share (receives a notification; answers a pending change in UC-80c) |
| **Description** | Allows the viewer to ask for a higher level or a longer range (answered in UC-80c); the owner lowers the level or changes the dates at once. A stopped share shows its state and reveals no hours; a share past its end date is no longer listed (BR-142, BR-143). (BR-142, BR-143, BR-010) |
| **Preconditions** | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-140, BR-141)<br>● PRE-2: The share is Effective; a Pending share is withdrawn in UC-80e and asked for again instead. (BR-142, BR-143) |
| **Postconditions** | ● POST-1: The share holds a pending change, or the new level/range; the other side is notified in both cases. (BR-010)<br>● POST-2: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Change Calendar Share<br>1. The viewer opens their share and asks for Level 2 (raise) or a longer range (extend); only one pending change at a time. A longer range may keep the share's original start date and still covers at most 365 days counting both ends.<br>2. The system saves the pending change, audits and notifies the owner. MSG454 – “Calendar share change request sent.” The use case ends; the owner’s answer is UC-80c. |
| **Alternative Flows** | AF-1 – The owner lowers the level or changes the dates<br>1. At any time on an Effective share, the owner chooses a lower level or a new date range, even while the viewer has a change pending; that change stays pending and the owner still answers it in UC-80c.<br>2. The system applies it at once, audits and notifies the viewer. MSG442 – “Calendar share updated.” |
| **Exception Flows** | EX-1 – Actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Change is not allowed<br>1. At Step 1 (or AF-1 Step 1), the share is not Effective, or its end date has passed (BR-142, BR-143), or — at Step 1 only — the viewer already has a change pending. (BR-142, BR-143)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 1 (or AF-1 Step 1), the new date range is invalid.<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes at Step 1 of the Normal Flow, or at Step 1 of AF-1. |

#### UC-80e – Withdraw or End Calendar Share

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member (the viewer) and User — the calendar owner (depending on who acts) |
| **Secondary Actors** | The other side of the share (receives a notification) |
| **Description** | Allows the sender to withdraw a Pending share or a pending change; either side ends an Effective share. A stopped share stays listed with its state and reveals no hours; a share past its end date is no longer listed. (BR-142, BR-143, BR-010) |
| **Preconditions** | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-140, BR-141)<br>● PRE-2: Withdrawing needs a Pending share or change; ending needs an Effective share. (BR-142, BR-143) |
| **Postconditions** | ● POST-1: The request is closed (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), or the share is ended; the other side is notified in every case. (BR-010)<br>● POST-2: A stopped share reveals no busy hours and never comes back; viewing again needs a new share. (BR-142, BR-143)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Withdraw Calendar Share<br>1. The side that sent the request or the change confirms and withdraws it.<br>2. The system closes the request (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), audits and notifies the other side. MSG438 – “Calendar share request withdrawn.” |
| **Alternative Flows** | AF-1 – Either side ends an Effective share (“Stop sharing” / “Stop viewing”)<br>1. Either side confirms and ends the share.<br>2. The system closes it, audits and notifies the other side. Hours already found are not removed from anyone’s screen, but no new lookup uses this share. MSG451 – “Calendar sharing ended.” |
| **Exception Flows** | EX-1 – Actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Nothing to withdraw or end<br>1. At Step 1 (or AF-1 Step 1), there is nothing Pending to withdraw, or the share is not Effective to end, or its end date has passed (BR-142, BR-143).<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends. |

#### UC-80f – View Calendar Shares

| Mục | Nội dung |
|---|---|
| **Primary Actors** | User — the calendar owner and, when holding the Member right, the viewer |
| **Secondary Actors** | The other side of each share |
| **Description** | Allows a signed-in user to see the calendar shares they take part in: who can see their calendar (“People who can see my calendar”) and, for a Member, who shared with them (“Calendars shared with me”). Each share shows its state and only the actions the user can take now; the actions themselves are UC-80a to UC-80e. (BR-142, BR-143, BR-140, BR-141) |
| **Preconditions** | ● PRE-1: The actor is signed in with an active internal account. (BR-140, BR-141) |
| **Postconditions** | ● POST-1: The actor sees only shares where they are one of the two people; nothing about other people’s shares is disclosed. (BR-142, BR-143)<br>● POST-2: A stopped share stays listed and reveals no hours; a share past its end date is not listed. (BR-142, BR-143) |
| **Normal Flow** | View Calendar Shares<br>1. The actor opens the list of calendar shares.<br>2. The system checks that the actor is an active internal account, loads the shares where the actor is the owner and, if the actor holds the Member right, the shares where the actor is the viewer; it leaves out every share whose end date is before today. (BR-140, BR-141, BR-142, BR-143)<br>3. The system shows the two lists, open shares (Pending, Effective) before closed ones (Declined, Withdrawn, Stopped, Ended), newest first. Each share shows the other person, the level, the date range, its state, any pending change and only the actions the actor may take now. (BR-142, BR-143)<br>4. A Pending request whose sender was deactivated can only be declined. Opening a stopped share shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” (BR-142, BR-143) |
| **Alternative Flows** | AF-1 – Nobody has shared yet<br>1. At Step 2, the actor has no share in either list.<br>2. The system shows empty lists and offers to ask for a share (UC-80a) or to share directly (UC-80b). The use case ends.<br>AF-2 – The actor is not a member<br>1. At Step 2, the actor does not hold the Member right.<br>2. The system shows only the list of people who can see the actor's calendar, and asking for a share is not offered. The actor can still answer, change or end the shares of their own calendar and share directly. (BR-140, BR-141)<br>AF-3 – Open a share link whose row is gone<br>1. At Step 1, the actor opens a share from a notification, but that share is not in the lists.<br>2. The system reloads both lists once; if the share is still missing, it shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” when the viewer lost the Member right, otherwise that the share no longer exists. The use case ends.<br>AF-4 – A share changes while viewing<br>1. While the actor views either list, someone answers, changes or ends a share with the actor.<br>2. The actor sees the change without reloading the page.<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | N/A |

### UC-81 – Find Meeting Time

_UC gom — đặc tả nằm ở các UC con UC-81a, UC-81b… bên dưới._

#### UC-81a – Find Meeting Time Slots

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member (will become the Secretary of the meeting created in UC-81b) |
| **Secondary Actors** | User — the invitees (a person who has not shared is shown as not sharing) |
| **Description** | Allows a Member to find slots where everyone invited is free, using only shared calendars. From anyone shown “Calendar not shared”, the finder may start UC-80a. (BR-140, BR-141, BR-144, BR-145, BR-146, BR-147, BR-148) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141) |
| **Postconditions** | ● POST-1: The actor sees common slots by day, or — when none fits everyone — the slots with the most people free and who is busy. Attendee names come from the actor’s own invite list; no meeting titles, locations, attendee lists or sensitivity levels are shown. (BR-144, BR-145, BR-140, BR-141)<br>● POST-2: Days outside a share and people who never shared show “unknown” / “Calendar not shared”, never free. (BR-144, BR-145) |
| **Normal Flow** | Find Meeting Time Slots<br>1. The actor opens the meeting-time search and enters the invitees (at most 10 people besides the actor, optionally counting the actor's own calendar), the duration (15 minutes to 8 hours), the date range (starting today, no more than 365 days ahead and at most 31 days long), the weekdays, the working hours with a lunch break, the buffer kept around every busy period (0 to 4 hours) and the rounding step for start times (5 minutes to 2 hours).<br>2. The system checks the search values.<br>3. The system builds each invitee's busy hours from the Effective shares of that invitee's calendar to the actor, at shared levels only, adds the actor's own Meetings when the actor asked to be counted, keeps the buffer around every busy period and never proposes a start time in the past; days outside a share are unknown. (BR-140, BR-141, BR-142, BR-143, BR-144, BR-145, BR-146, BR-147)<br>4. The system lists the common slots by day. |
| **Alternative Flows** | AF-1 – No slot fits everyone<br>1. At Step 4, no slot fits all invitees.<br>2. The system shows the slots with the most people free, with who is busy, per day. MSG445 – “No time slot fits everyone. Showing the slots with the most people free.” |
| **Exception Flows** | EX-1 – Search parameters are invalid<br>1. At Step 2, the search values are outside the limits of Step 1 (too many people, bad duration, range over 31 days, bad hours/buffer/step).<br>2. Nothing is searched. The system displays MSG444 – “The meeting-time search is invalid. Check the highlighted fields.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – Actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Member.<br>2. Nothing is searched. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |

#### UC-81b – Create Draft Meeting from Slot

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member (the finder in UC-81a, will become the Secretary of the created meeting) |
| **Secondary Actors** | User — the invitees in the chosen slot |
| **Description** | Allows a Member to create a Draft meeting from a slot found in UC-81a, reusing the UC-13 creation flow. A slot that overlaps an undisclosed Secret meeting still allows a draft; the recheck before saving uses the same shared data. (BR-148) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141)<br>● PRE-2: The actor has just found the slot in UC-81a. |
| **Postconditions** | ● POST-1: A chosen slot creates a Draft meeting through UC-13 (MSG29) with the first listed person as Chair, the rest as Member and the actor as Secretary; it follows the normal approval flow.<br>● POST-2: No signal — no distinct error, warning, notification or audit — differs only because a Secret meeting overlaps. (BR-148) |
| **Normal Flow** | Create Draft Meeting from Slot<br>1. The actor chooses a slot; the system opens the meeting form filled in with the slot and the invitees.<br>2. The actor confirms; the system checks the slot again against the same shared data as the UC-81a search, never against undisclosed meetings. (BR-148)<br>3. The system creates the Draft meeting through the creation steps of UC-13: the first listed invitee becomes Chair, the others Members and the actor Secretary. A value UC-13 rejects, such as a start time that has passed since the search, follows UC-13 EX-1.<br>4. The system displays MSG29 – “Meeting created successfully.” The meeting follows the normal approval flow. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Slot is no longer free<br>1. At Step 2, the slot no longer fits the shared data (someone’s shared calendar changed).<br>2. No meeting is created. The system displays MSG446 – “This time slot is no longer free. Please choose another slot.” The message is the same whether or not an undisclosed meeting overlaps. (BR-148)<br>3. The use case ends; the actor may choose another slot from the UC-81a results.<br>EX-2 – Actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Member.<br>2. No meeting is created. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |

#### UC-81c – View Shared Calendar

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Member |
| **Secondary Actors** | User — the people who shared their calendars with the actor |
| **Description** | Allows a Member to see a weekly calendar of busy hours from people who shared with them. Each person has one row; no meeting titles are ever shown. People whose share stopped stay listed with their state label and contribute no hours; a share past its end date is no longer listed. (BR-140, BR-141, BR-144, BR-145, BR-142, BR-143) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141) |
| **Postconditions** | ● POST-1: The actor sees only busy blocks of Effective shares at shared levels; nothing else. (BR-140, BR-141)<br>● POST-2: Stopped shares stay listed with their state label and reveal no hours; they never come back. (BR-142, BR-143) |
| **Normal Flow** | View Shared Calendar<br>1. The actor opens the calendars shared with him or her and chooses a week. (BR-140, BR-141)<br>2. The system loads the actor's Effective or Stopped shares whose end date is today or later, keeping the newest share per person, and the busy Meetings of those people at shared levels. (BR-140, BR-141, BR-142, BR-143, BR-146, BR-147)<br>3. The system shows the week with one row per person and only busy blocks; days outside a share's range show as unknown, never as free. (BR-144, BR-145)<br>4. People whose share stopped are listed with that state and contribute no hours; opening one shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” No title, location, attendee or level is ever shown. (BR-140, BR-141, BR-142, BR-143) |
| **Alternative Flows** | AF-1 – No Effective or Stopped share in range<br>1. At Step 2, the actor has no Effective or Stopped share whose end date is on or after today.<br>2. The system shows that nobody has shared a calendar with the actor and offers to ask for one (UC-80a). The use case ends.<br>AF-2 – A share changes while viewing<br>1. While the actor views the week, someone accepts, changes or stops sharing with the actor.<br>2. The actor sees the change without reloading the page.<br>3. The flow resumes at Step 3 of the Normal Flow. |
| **Exception Flows** | EX-1 – Actor cannot create meetings<br>1. At Step 2, the actor does not hold the Member right. (BR-140, BR-141)<br>2. Nothing is shown. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |
