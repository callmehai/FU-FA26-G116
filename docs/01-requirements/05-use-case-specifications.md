# Use Case Specifications · TrustCabinet

Đặc tả Use Case của **TrustCabinet**, đồng bộ nguyên văn mục *II. Use Case Specifications* của [Report 3 — SRS](../08-reports/Report-3_Software_Requirement_Specification.md) (bản DOCX ngày 13/10/2026, cập nhật ADR-0018). Nội dung đặc tả giữ tiếng Anh như Report 3.

- **Phạm vi:** 15 Feature (FE-01 → FE-15), 72 Use Case (UC-01 → UC-72; UC-32b và UC-33b đã bỏ). UC gom nhiều thao tác nhỏ được tách thành UC con có hậu tố chữ (ví dụ `UC-06a`, `UC-06b`, `UC-06c`).
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
- [UC-09 — Unlock User Account](#uc-09--unlock-user-account)

### FE-03 — Role-Based Access Control

- [UC-10 — View Role & Permission Matrix](#uc-10--view-role--permission-matrix)
- [UC-11 — Configure Role Permissions](#uc-11--configure-role-permissions)
- [UC-12 — Manage Roles](#uc-12--manage-roles)
  - [UC-12a — Create Role](#uc-12a--create-role)
  - [UC-12b — Update Role](#uc-12b--update-role)
  - [UC-12c — Delete Role](#uc-12c--delete-role)
- [UC-13 — View Audit Log](#uc-13--view-audit-log)

### FE-04 — Meeting Management

- [UC-14 — Create Meeting](#uc-14--create-meeting)
- [UC-15 — Update Meeting](#uc-15--update-meeting)
- [UC-16 — View Meeting Details](#uc-16--view-meeting-details)
- [UC-17 — View Meeting List](#uc-17--view-meeting-list)
- [UC-18 — Submit Meeting for Approval](#uc-18--submit-meeting-for-approval)
- [UC-19 — Review Meeting Approval](#uc-19--review-meeting-approval)
- [UC-20 — Publish Meeting](#uc-20--publish-meeting)
- [UC-21 — Start Meeting](#uc-21--start-meeting)
- [UC-22 — End Meeting](#uc-22--end-meeting)

### FE-05 — Agenda Management

- [UC-23 — Manage Agenda Items](#uc-23--manage-agenda-items)
  - [UC-23a — Create Agenda Item](#uc-23a--create-agenda-item)
  - [UC-23b — Update Agenda Item](#uc-23b--update-agenda-item)
  - [UC-23c — Delete Agenda Item](#uc-23c--delete-agenda-item)
  - [UC-23d — Reorder Agenda Items](#uc-23d--reorder-agenda-items)
- [UC-24 — Set Current Agenda Item](#uc-24--set-current-agenda-item)
- [UC-25 — Complete Agenda Item](#uc-25--complete-agenda-item)
- [UC-26 — View Meeting Agenda](#uc-26--view-meeting-agenda)

### FE-06 — Meeting Participant Management

- [UC-27 — View Meeting Participants](#uc-27--view-meeting-participants)
- [UC-28 — Manage Meeting Participants](#uc-28--manage-meeting-participants)
  - [UC-28a — Add Meeting Participant](#uc-28a--add-meeting-participant)
  - [UC-28b — Assign / Change Meeting Role](#uc-28b--assign--change-meeting-role)
  - [UC-28c — Remove Meeting Participant](#uc-28c--remove-meeting-participant)
- [UC-29 — Respond to Meeting Invitation](#uc-29--respond-to-meeting-invitation)
- [UC-30 — View Participant Response Status](#uc-30--view-participant-response-status)
- [UC-31 — Record Attendance](#uc-31--record-attendance)

### FE-07 — Personal Document Library & Meeting Document Management

- [UC-32 — View Meeting Documents](#uc-32--view-meeting-documents)
  - [UC-32a — View Meeting Documents](#uc-32a--view-meeting-documents)
  - [UC-32c — Preview Meeting Document](#uc-32c--preview-meeting-document)
- [UC-33 — Manage Meeting Documents](#uc-33--manage-meeting-documents)
  - [UC-33a — Add Document to Meeting](#uc-33a--add-document-to-meeting)
  - [UC-33c — Remove Document from Meeting](#uc-33c--remove-document-from-meeting)
- [UC-34 — View Personal Document Library](#uc-34--view-personal-document-library)
- [UC-35 — Comment on Meeting Document](#uc-35--comment-on-meeting-document)
- [UC-36 — Upload Document to Personal Library](#uc-36--upload-document-to-personal-library)
- [UC-71 — Manage Personal Documents](#uc-71--manage-personal-documents)
  - [UC-71a — Delete Document from Personal Library](#uc-71a--delete-document-from-personal-library)
  - [UC-71b — Rename Document](#uc-71b--rename-document)
  - [UC-71c — Move Document to Folder](#uc-71c--move-document-to-folder)
- [UC-72 — Manage Library Folders](#uc-72--manage-library-folders)
  - [UC-72a — Create Folder](#uc-72a--create-folder)
  - [UC-72b — Rename Folder](#uc-72b--rename-folder)
  - [UC-72c — Move Folder](#uc-72c--move-folder)
  - [UC-72d — Delete Folder](#uc-72d--delete-folder)

### FE-08 — Online Meeting Room Management

- [UC-37 — Join Meeting Room](#uc-37--join-meeting-room)
- [UC-38 — Leave Meeting Room](#uc-38--leave-meeting-room)
- [UC-39 — Manage Meeting Room Controls](#uc-39--manage-meeting-room-controls)
  - [UC-39a — Manage Microphone](#uc-39a--manage-microphone)
  - [UC-39b — Manage Camera](#uc-39b--manage-camera)
  - [UC-39c — Manage Screen Sharing](#uc-39c--manage-screen-sharing)
- [UC-40 — View Meeting Room Participants](#uc-40--view-meeting-room-participants)
- [UC-41 — Moderate Meeting Room](#uc-41--moderate-meeting-room)
- [UC-42 — Use In-room Chat](#uc-42--use-in-room-chat)

### FE-09 — Meeting Audio Upload & Media Library

- [UC-43 — View & Play Meeting Audio](#uc-43--view--play-meeting-audio)
  - [UC-43a — View Media Library](#uc-43a--view-media-library)
  - [UC-43b — Play Meeting Audio](#uc-43b--play-meeting-audio)
- [UC-44 — Upload Meeting Audio](#uc-44--upload-meeting-audio)

### FE-10 — Speaker-Attributed Transcription & Meeting Summary

- [UC-45 — View Speaker-Attributed Transcript](#uc-45--view-speaker-attributed-transcript)
- [UC-46 — Edit Transcript & Speaker Attribution](#uc-46--edit-transcript--speaker-attribution)
- [UC-47 — Generate / Regenerate Meeting Summary](#uc-47--generate--regenerate-meeting-summary)
- [UC-48 — View Meeting Summary](#uc-48--view-meeting-summary)
- [UC-49 — Edit Meeting Summary](#uc-49--edit-meeting-summary)
- [UC-50 — Approve Meeting Summary](#uc-50--approve-meeting-summary)
- [UC-51 — Publish Meeting Summary](#uc-51--publish-meeting-summary)

### FE-11 — Voting Management

- [UC-52 — View Meeting Votes](#uc-52--view-meeting-votes)
  - [UC-52a — View Meeting Votes](#uc-52a--view-meeting-votes)
  - [UC-52b — View Vote Status](#uc-52b--view-vote-status)
- [UC-53 — Create / Configure Vote](#uc-53--create--configure-vote)
- [UC-54 — Open Vote](#uc-54--open-vote)
- [UC-55 — Cast Vote](#uc-55--cast-vote)
- [UC-56 — Close Vote](#uc-56--close-vote)
- [UC-57 — Record Vote Conclusion](#uc-57--record-vote-conclusion)

### FE-12 — Meeting Minutes & Digital Signing

- [UC-58 — View Meeting Minutes](#uc-58--view-meeting-minutes)
- [UC-59 — Generate Meeting Minutes](#uc-59--generate-meeting-minutes)
- [UC-60 — Edit Meeting Minutes](#uc-60--edit-meeting-minutes)
- [UC-61 — Submit Meeting Minutes for Signing](#uc-61--submit-meeting-minutes-for-signing)
- [UC-62 — Sign Meeting Minutes](#uc-62--sign-meeting-minutes)
- [UC-63 — View Signature Status & Audit Trail](#uc-63--view-signature-status--audit-trail)
- [UC-64 — Finalize Meeting Minutes](#uc-64--finalize-meeting-minutes)
- [UC-65 — Request Revision of Meeting Minutes](#uc-65--request-revision-of-meeting-minutes)

### FE-13 — Email & In-App Notification

- [UC-66 — View Notifications](#uc-66--view-notifications)
- [UC-67 — Mark Notifications as Read](#uc-67--mark-notifications-as-read)
  - [UC-67a — Mark Notification as Read](#uc-67a--mark-notification-as-read)
  - [UC-67b — Mark All Notifications as Read](#uc-67b--mark-all-notifications-as-read)
- [UC-68 — Open Notification Target](#uc-68--open-notification-target)

### FE-14 — Personal Dashboard & Meeting Workspace

- [UC-69 — View Personal Dashboard & Meeting Workspace](#uc-69--view-personal-dashboard--meeting-workspace)

### FE-15 — Reporting & Statistics

- [UC-70 — View & Export Reports](#uc-70--view--export-reports)
  - [UC-70a — View Reports & Statistics](#uc-70a--view-reports--statistics)
  - [UC-70b — Export Report](#uc-70b--export-report)

---

## FE-01 — Authentication & Account Security

### UC-01 – Sign in with Email and Password

Sequence Diagram: [UC01_SignIn.txt](../06-sequencediagram/auth/UC01_SignIn.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | As an internal system user, I want to sign in using my email and password so that I can securely access the system through my internal account. |
| **Preconditions** | ● PRE-1: The user has an existing internal account.<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |
| **Postconditions** | ● POST-1: The user is authenticated.<br>● POST-2: The system identifies the corresponding internal user.<br>● POST-3: The user is redirected to the appropriate authenticated page. |
| **Normal Flow** | Sign in with Email and Password<br>1. The user enters their email and password on the login page.<br>2. The user clicks “Sign In”.<br>3. The system validates the provided credentials through the authentication service.<br>4. The system logs the user in and redirects the user to the appropriate authenticated page. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: System cannot authenticate the user<br>1. The entered email or password is invalid. (BR-01, BR-02)<br>2. The system displays MSG01 – “Invalid email or password.”<br>EX-2: The internal account has been deactivated<br>1. The corresponding internal account is inactive or has been deactivated. (BR-03)<br>2. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.” |

### UC-02 – Sign in with Google

Sequence Diagram: [UC02_SignInWithGoogle.txt](../06-sequencediagram/auth/UC02_SignInWithGoogle.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | Google Identity Provider |
| **Description** | As an internal system user, I want to sign in using my Google account so that I can securely and quickly access the system through my existing internal account. |
| **Preconditions** | ● PRE-1: The user has an existing internal account.<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |
| **Postconditions** | ● POST-1: User is authenticated.<br>● POST-2: A valid server-side session is created.<br>● POST-3: System identifies the corresponding internal user.<br>● POST-4: User is redirected to the appropriate authenticated page. |
| **Normal Flow** | Sign in with Google<br>1. User selects “Continue with Google” on the login page.<br>2. User signs in using their Google account.<br>3. System verifies that the Google account is associated with an authorized internal user.<br>4. System signs the user in and grants access according to the user's assigned role. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: User can’t authenticate with Google<br>1. The user fails to authenticate with Google or cancels the Google sign-in process. (BR-04)<br>2. System displays message MSG03 – “Google sign-in was unsuccessful.”<br>EX-2: System can’t verify the Google account<br>1. The system cannot verify the identity information returned by Google. (BR-05)<br>2. System displays message MSG04 – “Unable to verify your Google account. Please try again.”<br>EX-3: Google account is not linked to an internal account<br>1. The authenticated Google account is not associated with any existing internal account. (BR-06)<br>2. System displays message MSG05 – “This Google account has not been granted access to the system.”<br>EX-4: The internal account has been deactivated<br>1. The internal account associated with the Google account is inactive or has been deactivated. (BR-03)<br>2. System displays message MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.” |

### UC-03 – Sign Out

Sequence Diagram: [UC03_SignOut.txt](../06-sequencediagram/auth/UC03_SignOut.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | As an authenticated user, I want to sign out of the system so that I can securely end my current access to the system. |
| **Preconditions** | ● PRE-1: The user is currently signed in to the system.<br>● PRE-2: The user is currently accessing an authenticated area of the system. |
| **Postconditions** | ● POST-1: User is signed out of the system.<br>● POST-2: The user's current authentication state is invalidated.<br>● POST-3: User is redirected to the login page.<br>● POST-4: Protected system functions are no longer accessible until the user signs in again. |
| **Normal Flow** | Sign Out<br>1. User selects “Sign Out”.<br>2. System displays a confirmation message.<br>3. User confirms the sign-out action.<br>4. System signs the user out.<br>5. System redirects the user to the login page. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: System can’t complete the sign-out action<br>1. The system cannot complete the sign-out operation. (BR-09)<br>2. System displays message MSG06 – “Unable to sign out. Please try again.” |

### UC-04 – Access Meeting via Guest Invitation

Sequence Diagram: [Access Meeting via Guest Invitation](../06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt) (một luồng đầu-cuối gồm cấp mã, xác minh email và vào cuộc họp). Source và ảnh PNG: [auth/README](../06-sequencediagram/auth/README.md).



| Mục | Nội dung |
|---|---|
| **Primary Actors** | Guest |
| **Secondary Actors** | Email / SMTP Service |
| **Description** | Allows an invited Guest to verify control of the invited email and enter self-declared display information for the invited Meeting. |
| **Preconditions** | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked or expired. |
| **Postconditions** | POST-1: Verified Guest access is limited to the invited Meeting.<br>POST-2: The Guest may use Member participation functions and receive additional Meeting permissions. Email control is verified; display name and job title are self-declared. |
| **Normal Flow** | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system checks the invitation and requests the one-time code sent to the invited email.<br>3. The Guest enters the code.<br>4. The system validates the code and invitation and verifies control of the invited email. (BR-153)<br>5. The Guest enters the display name and job title for presentation in the Meeting; the system labels this information as self-declared. (BR-180)<br>6. The system establishes Meeting-scoped Guest access and displays the invited Meeting. |
| **Alternative Flows** | ALT-1: If the Guest reopens a valid link during an active verified session, the system opens the same invited meeting without requesting another code. |
| **Exception Flows** | EX-1: The invitation is missing, expired, or revoked: the system denies access and explains that the invitation is no longer valid.<br>EX-2: The code is incorrect, expired, or already used: the system denies verification and allows the Guest to request or enter a valid code.<br>EX-3: The verification service is unavailable: the system reports that verification could not be completed and does not establish a session. |

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
| **Normal Flow** | View User Accounts<br>1. The System Admin selects User Accounts from the navigation menu.<br>2. The system retrieves the user accounts.<br>3. The system sorts the user accounts according to the default sorting rule (BR-167).<br>4. The system displays the user account list showing Full Name, Username/Email, System Role, and Account Status.<br>5. The System Admin browses the user account list using pagination controls. |
| **Alternative Flows** | AF-1: Search User Accounts<br>1. At Step 4 of the Main Flow, the System Admin enters a Name, Username, or Email into the search field.<br>2. The system searches for user accounts matching the entered criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow.<br>AF-2: Filter User Accounts<br>1. At Step 4 of the Main Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow.<br>AF-3: No User Accounts Match the Current Criteria<br>1. The applied search or filter criteria produce no matching user accounts.<br>2. The system displays an empty list and a no-results message according to BR-12.<br>3. The System Admin may clear or adjust the search/filter parameters.<br>4. The flow resumes at Step 5 of the Main Flow.<br>5. At Step 4 of the Main Flow, the System Admin enters a Name, Username, or Email into the search field.<br>6. The system searches for user accounts matching the entered criteria.<br>7. The system displays the matching user accounts.<br>8. The flow resumes at Step 5 of the Main Flow.<br>AF-2: Filter User Accounts<br>1. At Step 4 of the Main Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow. |
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
| **Normal Flow** | Create User Account<br>1. The System Admin selects Create User from the User Accounts screen.<br>2. The system displays the Create User form.<br>3. The System Admin enters the required user information.<br>4. The System Admin submits the user information.<br>5. The system validates the submitted information, including required fields and Username/Email validity and uniqueness (BR-13, BR-14).<br>6. The system creates the new user account with Active status (BR-15).<br>7. The system records the account creation in the audit log (BR-16).<br>8. The system displays MSG07 – "User account created successfully." and returns to the User Accounts list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: Invalid User Information<br>1. At Step 5 of the Main Flow, the system determines that one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.<br>3. The System Admin corrects the information and resubmits the form.<br>4. The flow resumes at Step 5 of the Main Flow.<br>EX-2: Duplicate User Account<br>1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use."<br>3. The System Admin changes the conflicting information and resubmits the form.<br>4. The flow resumes at Step 5 of the Main Flow. |

#### UC-06b – Update User Account

Sequence Diagram: [UC06b_UpdateUserAccount.txt](../06-sequencediagram/user-management/UC06b_UpdateUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to update editable information of an existing user account within their permitted management scope. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to update the selected user account.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: The selected user account information is updated successfully.<br>● POST-2: The update action is recorded in the audit log. |
| **Normal Flow** | Update User Account<br>1. The System Admin selects a user account from the User Accounts list and selects Edit Account.<br>2. The system displays the current user account information.<br>3. The System Admin modifies the editable information, such as Full Name, Job Title, or Contact Information.<br>4. The System Admin submits the updated information.<br>5. The system validates the submitted information, including required fields and data format (BR-14, BR-17).<br>6. The system updates the user account information.<br>7. The system records the update in the audit log (BR-16).<br>8. The system displays MSG10 – "User account updated successfully." and returns to the User Accounts list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: Invalid User Information<br>1. At Step 5 of the Main Flow, one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.<br>3. The System Admin corrects the information and resubmits it.<br>4. The flow resumes at Step 5 of the Main Flow.<br>EX-2: Email Already in Use<br>1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use."<br>3. The System Admin changes the Email and resubmits the information.<br>4. The flow resumes at Step 5 of the Main Flow. |

#### UC-06c – Deactivate / Reactivate User Account

Sequence Diagram: [UC06c_DeactivateReactivateUserAccount.txt](../06-sequencediagram/user-management/UC06c_DeactivateReactivateUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to deactivate or reactivate a user account while preserving the user's historical records. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage user account status.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: The user account status is changed to Inactive when deactivated or Active when reactivated.<br>● POST-2: A deactivated user can no longer access authenticated system functions.<br>● POST-3: The status change is recorded in the audit log. |
| **Normal Flow** | Deactivate / Reactivate User Account<br>1. The System Admin selects a user account from the User Accounts list.<br>2. The System Admin selects Deactivate Account or Reactivate Account.<br>3. The system requests confirmation of the account status change.<br>4. The System Admin confirms the action.<br>5. The system verifies that the requested status change is allowed (BR-03).<br>6. The system updates the user account status.<br>7. If the account is deactivated, the system ends the user's active sessions (BR-18).<br>8. The system records the status change in the audit log (BR-16).<br>9. The system displays MSG11 – “User account status changed successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: Status Change Is Not Allowed<br>1. At Step 5 of the Main Flow, the system determines that the requested account status change is not allowed.<br>2. The system displays MSG12 – “Unable to change the status of this user account.”<br>3. The account status remains unchanged.<br>EX-2: Invalid Status Request<br>1. At Step 5 of the Main Flow, the system determines that the request does not specify the account status to set.<br>2. The account status remains unchanged.<br>3. The system displays MSG09 – “Invalid account information.”<br>4. The use case ends unsuccessfully. |

### UC-07 – Reset User Password

Sequence Diagram: [UC07_ResetUserPassword.txt](../06-sequencediagram/user-management/UC07_ResetUserPassword.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | User (the owner of the selected account), who completes the reset from the email |
| **Description** | Allows the System Admin to send the owner of an eligible user account a secure, single-use password reset link by email. The account owner then opens the link and sets a new password without signing in. The current password keeps working until the new one is set. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to reset user passwords.<br>● PRE-3: The selected user account exists. |
| **Postconditions** | ● POST-1: A password reset link valid for 60 minutes is created for the selected user, and any link sent to that user earlier stops working (BR-20).<br>● POST-2: A password reset email containing the link is sent to the user's registered email address.<br>● POST-3: The password reset request is recorded in the audit log (BR-16).<br>● POST-4: Until the link is used, the user's current password and active sessions remain unchanged.<br>● POST-5: When the account owner completes the reset, the password is replaced, every active session of that account is ended, the link can no longer be used, and the reset is recorded in the audit log (BR-01, BR-16, BR-20). |
| **Normal Flow** | Reset User Password<br>1. The System Admin selects a user account and selects Reset Password.<br>2. The system requests confirmation, showing the email address the link will be sent to.<br>3. The System Admin confirms the password reset.<br>4. The system verifies that the selected account is eligible for password reset: it is active and it signs in with a password (BR-22).<br>5. The system creates a secure password reset request for the selected user (BR-20).<br>6. The system sends a password reset email containing the link to the user's registered email address.<br>7. The system makes the new link usable; any link sent to the user earlier stops working (BR-20).<br>8. The system records the password reset request in the audit log (BR-16).<br>9. The system displays MSG13 – “Password reset email sent successfully.”<br>Complete Password Reset<br>The account owner continues the use case from the email. No sign-in is required: the link is the proof of identity.<br>10. The user opens the password reset link from the email.<br>11. The system displays the new password form.<br>12. The user enters the new password and its confirmation and submits them.<br>13. The system verifies that the link is valid: it exists, it has not expired, it has not been used, and no newer link has replaced it (BR-20).<br>14. The system validates the new password: it must follow the same password rule as account creation (UC-06) and match its confirmation.<br>15. The system marks the link as used, so it cannot be used again (BR-20).<br>16. The system verifies that the account is still eligible: it is active and it signs in with a password (BR-03, BR-22).<br>17. The system replaces the password with the new one (BR-01) and ends every active session of the account (BR-20).<br>18. The system records the completed password reset in the audit log, with the account owner as the actor (BR-16).<br>19. The system displays MSG92 – “Password reset successfully. Please sign in with your new password.” and takes the user to the sign-in page. The user is not signed in automatically. |
| **Alternative Flows** | AF-1: The Browser Is Already Signed In<br>1. At Step 19, the browser used to complete the reset still has a signed-in session (of this or another account).<br>2. The system signs that session out before showing the sign-in page.<br>3. If the sign-out succeeds, the flow resumes at Step 19.<br>4. If the sign-out cannot be completed, the system still displays MSG92 – “Password reset successfully. Please sign in with your new password.” and also displays MSG06 – “Unable to sign out. Please try again.” The browser's session remains signed in, and the new password is already in effect.<br>5. The use case ends. |
| **Exception Flows** | EX-1: User Account Is Not Eligible for Password Reset<br>1. At Step 4 of the Main Flow, the system determines that the selected account is deactivated, or that it has no password because it only signs in with Google (BR-22). The User Accounts list already makes Reset Password unavailable for such accounts, so this happens when the list is out of date.<br>2. No password reset link is created and no email is sent.<br>3. The system displays MSG14 – “Password reset is not available for this account.” When the system can tell which reason applies, it updates the account in the list and states the reason instead (the account is deactivated, or it signs in with Google only).<br>4. The use case ends.<br>EX-2: Password Reset Email Cannot Be Sent<br>1. At Step 6 of the Main Flow, the system cannot send the password reset email.<br>2. The new link is never made usable. A link sent to the user earlier, if any, keeps working.<br>3. The system displays MSG15 – “Unable to send the password reset email. Please try again.”<br>4. The use case ends.<br>EX-3: User Account No Longer Exists<br>1. At Step 4 of the Main Flow, the system cannot find the selected account.<br>2. No password reset link is created and no email is sent.<br>3. The system informs the System Admin that the account no longer exists and refreshes the User Accounts list. This situation has no message code.<br>4. The use case ends.<br>EX-4: Password Reset Link Is Not Valid<br>1. At Step 13, 15 or 16, the system determines that the link is unknown, has expired, has already been used (including by a simultaneous submission), has been replaced by a newer link, or belongs to an account that is now deactivated or has no password (BR-20, BR-22).<br>2. The password is not changed. A link that reached Step 15 is used up.<br>3. The system hides the new password form and displays MSG93 – “The password reset link is invalid or has expired. Please contact an administrator.” It does not say which of these reasons applies.<br>4. The use case ends.<br>EX-5: New Password Is Invalid<br>1. At Step 14, the new password does not satisfy the password rule, or the confirmation does not match it.<br>2. The password is not changed and the link remains usable.<br>3. The system shows the reason next to the field concerned. A password that breaks the password rule is MSG94 – “Invalid new password.”, shown through its field reason (at least 12 characters); a confirmation that does not match is reported before the link is checked.<br>4. The user corrects the new password and resubmits it.<br>5. The flow resumes at Step 13.<br>EX-6: Password Reset Page Opened Without a Reset Code<br>1. At Step 11, the page has no reset code. This happens when the link is incomplete, or when the page is reloaded or restored after the code was removed from the address bar.<br>2. The system does not check the link and does not say that it has expired, because the link itself may still be usable.<br>3. The system displays MSG129 – “No password reset code was found. Please open the link in the email again.” with a hint that the link still works if it has not been used, is less than 60 minutes old and has not been replaced by a newer link.<br>4. The use case ends. Opening the link from the email again starts a new execution at Step 10. |

### UC-08 – Assign System Role to User

Sequence Diagram: [UC08_AssignSystemRoleToUser.txt](../06-sequencediagram/user-management/UC08_AssignSystemRoleToUser.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to assign a system role to a user account or change the user’s existing system role. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to assign system roles.<br>● PRE-3: The selected user account exists.<br>● PRE-4: At least one system role is available for assignment. |
| **Postconditions** | ● POST-1: The selected user is associated with the selected system role.<br>● POST-2: The user receives the capabilities associated with the selected system role.<br>● POST-3: The role assignment or change is recorded in the audit log. |
| **Normal Flow** | Assign System Role to User<br>1. The System Admin opens User Management.<br>2. The System Admin selects a user account.<br>3. The system displays the user information and the current system role, if any.<br>4. The System Admin selects Assign System Role.<br>5. The system displays the available system roles.<br>6. The System Admin selects a role and confirms the assignment.<br>7. The system assigns the selected role to the user.<br>8. The system records the role assignment in the audit log.<br>9. The system displays MSG10 – “User account updated successfully.” |
| **Alternative Flows** | AF-1: Change Existing System Role<br>1. At Step 4 of the Main Flow, if the user already has a system role, the System Admin selects Change System Role.<br>2. The System Admin selects a different role.<br>3. The System Admin confirms the change.<br>4. The system replaces the current role with the selected role.<br>5. The system records the role change in the audit log.<br>6. The system displays MSG10 – “User account updated successfully.” |
| **Exception Flows** | EX-1: Selected Role Is Not Available<br>1. The selected role is no longer available for assignment.<br>2. The system displays MSG09 – “Invalid account information.” with the field error “The role does not exist.”<br>3. No role assignment is changed. The use case ends.<br>EX-2: Last Administrator Cannot Be Demoted<br>1. At Step 7 of the Main Flow or Step 4 of AF-1, the system determines that, after the change, no active account would hold the user administration permission.<br>2. The user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends unsuccessfully. |

### UC-09 – Unlock User Account

Sequence Diagram: [UC09_UnlockUserAccount.txt](../06-sequencediagram/user-management/UC09_UnlockUserAccount.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to unlock a user account that has been locked by the authentication security controls. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated and authorized to manage user accounts.<br>● PRE-2: The selected user account exists and is currently locked. |
| **Postconditions** | ● POST-1: The account lock is cleared and the user can attempt to sign in, subject to account status and credentials.<br>● POST-2: The unlock action is recorded in the audit log. |
| **Normal Flow** | Unlock User Account<br>1. The System Admin opens User Accounts and selects the locked account.<br>2. The system displays the account status and available account actions.<br>3. The System Admin selects Unlock Account and confirms the action.<br>4. The system clears the lock state and resets the failed-attempt lock counter.<br>5. The system confirms the unlock and records the action. |
| **Alternative Flows** | ALT-1: If the account is already unlocked when the request is processed, the system leaves it unchanged and refreshes the displayed status. |
| **Exception Flows** | EX-1: The account does not exist or is outside the Admin's scope: the system denies the action.<br>EX-2: The account is inactive: the system does not make it sign-in eligible and explains that reactivation is required.<br>EX-3: The update fails: the system retains the existing lock state and reports that the account could not be unlocked. |

---

## FE-03 — Role-Based Access Control

### UC-10 – View Role & Permission Matrix

Sequence Diagram: [UC10_ViewRolePermissionMatrix.txt](../06-sequencediagram/role-management/UC10_ViewRolePermissionMatrix.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to view the predefined roles and their default permissions. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated. |
| **Postconditions** | ● POST-1: No system data is modified. |
| **Normal Flow** | View Role & Permission Matrix<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the role and permission matrix. |
| **Alternative Flows** | None. |
| **Exception Flows** | E1 — Role and permission information cannot be retrieved<br>1. The system cannot retrieve the role and permission information.<br>2. The system displays an error message.<br>3. The use case ends unsuccessfully. |

### UC-11 – Configure Role Permissions

Sequence Diagram: [UC11_ConfigureRolePermissions.txt](../06-sequencediagram/role-management/UC11_ConfigureRolePermissions.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to configure the default permissions assigned to a predefined system role. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage role permissions. |
| **Postconditions** | ● POST-1: The selected role is associated with the updated set of default permissions.<br>● POST-2: The permission configuration change is recorded in the audit log. |
| **Normal Flow** | Configure Role Permissions<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the Role & Permission Matrix.<br>3. The System Admin configures the permissions assigned to a predefined role.<br>4. The system validates and saves the updated permission configuration (BR-23).<br>5. The system records the configuration change in the audit log (BR-16).<br>6. The system displays MSG16 – “Role permissions updated successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: Invalid Permission Configuration<br>1. At Step 4 of the Main Flow, the system determines that the updated permission configuration violates a permission rule.<br>2. The system does not save the changes.<br>3. The system displays MSG17 – “Invalid permission configuration.”<br>4. The use case ends unsuccessfully.<br>EX-2: Permission Configuration Cannot Be Saved<br>1. At Step 4 of the Main Flow, the system cannot save the updated permission configuration.<br>2. The existing role permissions remain unchanged.<br>3. The system displays MSG18 – “Unable to update role permissions. Please try again.”<br>4. The use case ends unsuccessfully. |

### UC-12 – Manage Roles

_UC gom — đặc tả nằm ở các UC con UC-12a, UC-12b… bên dưới._

#### UC-12a – Create Role

Sequence Diagram: [UC12a_CreateRole.txt](../06-sequencediagram/role-management/UC12a_CreateRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to create a new custom role with a name, an optional description and an optional initial set of permissions, without changing code or redeploying the system. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles. |
| **Postconditions** | ● POST-1: A new custom role is created and appears in the role list and in the Role & Permission Matrix.<br>● POST-2: The new role is associated with the selected permissions (the set may be empty).<br>● POST-3: The role creation is recorded in the audit log. |
| **Normal Flow** | Create Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The System Admin selects “Create Role”.<br>3. The system displays the role creation form.<br>4. The System Admin enters the role name and an optional description, selects the permissions to grant to the role, and submits the form.<br>5. The system validates the role information: the name is required and has at most 64 characters after trimming, the description has at most 256 characters, and every selected permission exists in the permission catalog.<br>6. The system checks that no existing role, including the predefined roles, has the same name (case-insensitive).<br>7. The system saves the new role together with its permissions.<br>8. The system records the role creation in the audit log (BR-16).<br>9. The system displays MSG19 – “Role created successfully.” |
| **Alternative Flows** | AF-1: Role Created Without Permissions<br>1. At Step 4 of the Main Flow, the System Admin does not select any permission.<br>2. The system accepts an empty permission set; user accounts assigned to this role have the same access as a Member.<br>3. The use case continues at Step 5 of the Main Flow. |
| **Exception Flows** | EX-1: Invalid Role Information<br>1. At Step 5 of the Main Flow, the system determines that the name is empty or longer than 64 characters, the description is longer than 256 characters, or a selected permission does not exist.<br>2. The system does not save the role.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.<br>EX-2: Duplicate Role Name<br>1. At Step 6 or Step 7 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the role.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.<br>EX-3: Role Cannot Be Saved<br>1. At Step 7 of the Main Flow, the system cannot save the new role.<br>2. No role is created.<br>3. The system displays MSG26 – “Unable to create the role. Please try again.”<br>4. The use case ends unsuccessfully. |

#### UC-12b – Update Role

Sequence Diagram: [UC12b_UpdateRole.txt](../06-sequencediagram/role-management/UC12b_UpdateRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to change the name and description of a custom role. Predefined roles cannot be renamed; the permissions of any role are configured through UC-11. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |
| **Postconditions** | ● POST-1: The name and/or description of the selected role are updated; its permissions and the user accounts holding it remain unchanged.<br>● POST-2: The change is recorded in the audit log with the values before and after the update.<br>● POST-3: No user session is terminated; users holding the role see the new name the next time their information is loaded. |
| **Normal Flow** | Update Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles.<br>3. The System Admin selects “Edit” on a custom role.<br>4. The system displays the edit form pre-filled with the current name and description.<br>5. The System Admin modifies the name and/or description and submits the form.<br>6. The system verifies that the selected role exists and is a custom role.<br>7. The system validates the role information using the same rules as UC-12 (name required, at most 64 characters after trimming; description at most 256 characters).<br>8. The system checks that no other role has the same name (case-insensitive). Changing only the letter case of the role’s own name is allowed.<br>9. The system saves the updated role.<br>10. The system records the change in the audit log (BR-16).<br>11. The system displays MSG22 – “Role updated successfully.” |
| **Alternative Flows** | AF-1: No Changes Submitted<br>1. At Step 7 of the Main Flow, the system determines that the submitted name and description are identical to the current values.<br>2. The system does not modify any data and does not record an audit log entry.<br>3. The system displays MSG22 – “Role updated successfully.”<br>4. The use case ends. |
| **Exception Flows** | EX-1: Predefined Role<br>1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not save the changes.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends unsuccessfully.<br>EX-2: Invalid Role Information<br>1. At Step 7 of the Main Flow, the system determines that the role information is invalid.<br>2. The system does not save the changes.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.<br>EX-3: Duplicate Role Name<br>1. At Step 8 or Step 9 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the changes.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.<br>EX-4: Role Cannot Be Saved<br>1. At Step 9 of the Main Flow, the system cannot save the updated role.<br>2. The existing role information remains unchanged.<br>3. The system displays MSG27 – “Unable to update the role. Please try again.”<br>4. The use case ends unsuccessfully. |

#### UC-12c – Delete Role

Sequence Diagram: [UC12c_DeleteRole.txt](../06-sequencediagram/role-management/UC12c_DeleteRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to delete a custom role that is no longer held by any user account. Predefined roles cannot be deleted. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |
| **Postconditions** | ● POST-1: The selected role and its permission assignments are removed from the system.<br>● POST-2: The deletion, including the role name and the permissions it held, is recorded in the audit log.<br>● POST-3: No user account and no user session is affected. |
| **Normal Flow** | Delete Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles with the number of user accounts holding each role.<br>3. The System Admin selects “Delete” on a custom role.<br>4. The system displays a confirmation dialog.<br>5. The System Admin confirms the deletion.<br>6. The system verifies that the selected role exists, is a custom role, and is not held by any user account, including deactivated accounts.<br>7. The system deletes the role together with its permission assignments.<br>8. The system records the role deletion in the audit log (BR-16).<br>9. The system displays MSG23 – “Role deleted successfully.” |
| **Alternative Flows** | AF-1: Deletion Cancelled<br>1. At Step 5 of the Main Flow, the System Admin cancels the deletion.<br>2. The system closes the confirmation dialog without changing any data.<br>3. The use case ends. |
| **Exception Flows** | EX-1: Predefined Role<br>1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not delete the role.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends unsuccessfully.<br>EX-2: Role Still Held by User Accounts<br>1. At Step 6 or Step 7 of the Main Flow, the system determines that one or more user accounts (active or deactivated) still hold the role, including an account assigned to the role while the deletion was in progress.<br>2. The system does not delete the role.<br>3. The system displays MSG25 – “This role is still held by {n} user account(s). Reassign them to another role before deleting.” in the confirmation dialog, where {n} is the number of user accounts holding the role.<br>4. The use case ends unsuccessfully.<br>EX-3: Role Cannot Be Deleted<br>1. At Step 7 of the Main Flow, the system cannot delete the role.<br>2. The role and its permission assignments remain unchanged.<br>3. The system displays MSG28 – “Unable to delete the role. Please try again.”<br>4. The use case ends unsuccessfully. |

### UC-13 – View Audit Log

Sequence Diagram: [UC13_ViewAuditLog.txt](../06-sequencediagram/role-management/UC13_ViewAuditLog.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | None |
| **Description** | Allows the System Admin to review recorded system events for security, accountability, and operational investigation. |
| **Preconditions** | ● PRE-1: The System Admin is authenticated and has permission to view the audit log. |
| **Postconditions** | ● POST-1: The requested audit entries are displayed read-only.<br>● POST-2: No audit entry is changed by viewing or filtering the log. |
| **Normal Flow** | View Audit Log<br>1. The System Admin opens Audit Log.<br>2. The system retrieves audit entries within the Admin's requested date range and filters.<br>3. The system displays entries in reverse chronological order with actor, event, target, time, and outcome where recorded.<br>4. The System Admin opens an entry or changes filters/pages the results.<br>5. The system displays the matching entries and preserves the read-only state. |
| **Alternative Flows** | ALT-1: If no entries match the filters, the system displays an empty-result message and permits the Admin to revise the filters. |
| **Exception Flows** | EX-1: Invalid filter or date-range values: the system identifies the invalid criteria and does not apply them.<br>EX-2: The audit log cannot be loaded: the system reports the failure and provides a retry action. |

---

## FE-04 — Meeting Management

### UC-14 – Create Meeting

Sequence Diagram: [UC14_CreateMeeting.txt](../06-sequencediagram/meeting-management/UC14_CreateMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows an authorized Meeting Organizer to create one independent Meeting as a Draft, including its planned schedule, mode, location and confidentiality. |
| **Preconditions** | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The Meeting Organizer is authorized to create meetings. |
| **Postconditions** | ● POST-1: A new Meeting exists in Draft status with its planned start and end times.<br>● POST-2: The creator is recorded as Meeting Organizer.<br>● POST-3: The Meeting may subsequently be assigned participants, prepared and submitted for approval. |
| **Normal Flow** | Create Meeting<br>1. The Meeting Organizer selects Create Meeting.<br>2. The system displays the new Meeting form.<br>3. The Organizer enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description.<br>4. The Organizer submits the form.<br>5. The system checks the required values, confirms Planned Start Time is later than the current time and Planned End Time is later than Planned Start Time. (BR-24, BR-25, BR-62, BR-162)<br>6. The system creates exactly one Meeting in Draft status and records the creator. (BR-26)<br>7. The system displays MSG29 – "Meeting created successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Meeting Information<br>1. At Step 5, a required value is missing/invalid, the planned start is not in the future, or the planned end does not follow the start. (BR-24, BR-62, BR-162)<br>2. The system displays MSG31 – "Invalid meeting information." and highlights the affected fields.<br>3. No Meeting is created. The Organizer may correct the inputs and resubmit at Step 4.<br>EX-2 – Meeting Cannot Be Created<br>1. At Step 6, the system cannot save the Meeting.<br>2. The system displays MSG35 – "Unable to create the meeting. Please try again."<br>3. No Meeting is created; the use case ends unsuccessfully. |

### UC-15 – Update Meeting

Sequence Diagram: [UC15_UpdateMeeting.txt](../06-sequencediagram/meeting-management/UC15_UpdateMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Organizer to update Draft/Rejected Meeting information, or to reschedule only the planned times or location when the Meeting is Published. |
| **Preconditions** | ● PRE-1: The Meeting exists and the Meeting Organizer is authenticated and authorized.<br>● PRE-2: Full Meeting editing is available only in Draft or Rejected. (BR-27)<br>● PRE-3: If the Meeting is Published, only the Meeting Organizer may invoke Reschedule to change planned times or location. The Meeting must not have started. (BR-27, BR-158) |
| **Postconditions** | ● POST-1: Authorized updates are saved to the Meeting.<br>● POST-2: Meeting lifecycle and assigned approver are unchanged.<br>● POST-3: When the schedule of a Published Meeting changes, affected participants are notified and the change is recorded. (BR-76) |
| **Normal Flow** | Update Meeting<br>1. The Meeting Organizer opens a Draft or Rejected Meeting and selects Edit.<br>2. The system displays the editable Meeting fields.<br>3. The Organizer changes the fields and submits.<br>4. The system verifies current Meeting status, permissions, required information, and confirms Planned End Time is after Planned Start Time. (BR-24, BR-27, BR-62)<br>5. The system saves the updates without changing Meeting status and shows the success message. |
| **Alternative Flows** | AF-1 – Reschedule Published Meeting<br>Entry point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects Reschedule for a Meeting that is Published and has not started.<br>2. The system allows changes only to Planned Start Time, Planned End Time and Location. All other fields are read-only. (BR-27, BR-158)<br>3. The Organizer enters the changes and submits.<br>4. The system validates that the new start is in the future and the end follows the start. (BR-62, BR-75)<br>5. The system saves the new schedule, retains Published status, records the change and queues notifications to affected invitees. Delivery failure does not roll back the saved change. (BR-38, BR-76, BR-158)<br>6. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified." The use case succeeds. |
| **Exception Flows** | EX-1 – Invalid or Unauthorized Update<br>1. The system determines that meeting information is invalid, the Organizer is no longer authorized, or the Meeting status does not permit this edit. (BR-27, BR-62)<br>2. The system does not save any changes and displays the applicable existing validation/error message.<br>3. The use case ends unsuccessfully or the Organizer corrects input and retries.<br>EX-2 – Reschedule Not Permitted<br>1. During AF-1 the Meeting is not Published, has already started, its planned start is not in the future, or a field outside the allowed Reschedule set was modified. (BR-27, BR-75, BR-158)<br>2. The system rejects the update; the existing schedule remains unchanged.<br>3. The system displays the relevant existing meeting-update error message. The use case ends.<br>EX-3 – Update Cannot Be Saved<br>1. At Main Step 5 or AF-1 Step 5 a persistence error occurs.<br>2. Existing Meeting data/status remain unchanged and no schedule-update notification is sent.<br>3. The system displays the existing update-failure message. The use case ends. |

### UC-16 – View Meeting Details

Sequence Diagram: [UC16_ViewMeetingDetails.txt](../06-sequencediagram/meeting-management/UC16_ViewMeetingDetails.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows authorized users to view the details of one Meeting, including planned schedule, actual start/end times when available, and meeting-level status. Agenda and other records remain accessible through their corresponding views. |
| **Preconditions** | ● PRE-1: The user is authenticated.<br>● PRE-2: A meeting has been selected or identified. |
| **Postconditions** | ● POST-1: The Meeting Name, planned time range, actual times where available, mode, location, confidentiality, description and lifecycle status are displayed read-only.<br>● POST-2: The Meeting is not modified. |
| **Normal Flow** | View Meeting Details<br>1. The user requests the selected Meeting.<br>2. The system verifies that the Meeting exists and the user has access. (BR-29)<br>3. The system retrieves the Meeting information and planned/actual time fields.<br>4. The system displays Meeting Name, planned start/end time, actual start/end time when available, Meeting Mode, Location, Confidentiality Level, Description, and Status.<br>5. The user reviews the Meeting details. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Does Not Exist or Is Not Accessible<br>1. At Step 2, the system cannot find the selected meeting or the user is not authorized to view it.<br>2. The system does not disclose whether the meeting exists.<br>3. The use case ends.<br>EX-2 – Meeting Details Cannot Be Retrieved<br>1. At Step 3, the meeting exists and is accessible, but the system cannot retrieve its meeting-level information.<br>2. The system displays MSG37 – "Unable to load meeting details. Please try again."<br>3. The use case ends. |

### UC-17 – View Meeting List

Sequence Diagram: [UC17_ViewMeetingList.txt](../06-sequencediagram/meeting-management/UC17_ViewMeetingList.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment. The assigned Secretary can view the Meeting before publication under BR-118. External Guests do not use the internal Meeting list. |
| **Preconditions** | ● PRE-1: The user is authenticated. |
| **Postconditions** | ● POST-1: The system displays the list of meetings that the user is authorized to view.<br>● POST-2: No meeting information is modified. |
| **Normal Flow** | View Meeting List<br>1. The user selects Meetings from the navigation menu.<br>2. The system identifies only meetings the user may view under BR-29: meetings created by the user at any status; meetings assigned to the user as Meeting Approver from WaitingApproval onward; and meetings assigned to the user as an active Secretary at any status, including before publication (BR-118); and meetings where the user is assigned as Chair or Member from Published onward. External Guests access only their invited Meeting through UC-04 and do not use this internal list.<br>3. The system sorts and displays only those authorized meetings.<br>4. The user browses the displayed meetings. |
| **Alternative Flows** | AF-1 – Search Meetings<br>1. At Step 4 of the Main Flow, the user enters a meeting name in the search field.<br>2. The system finds meetings matching the entered search criteria within the meetings available to the user.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – Filter Meetings<br>1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status, Confidentiality Level, or Time Range.<br>2. The system applies the selected criteria to the meetings available to the user. For Time Range, a Meeting matches when its own planned start/end time overlaps the selected time range. (BR-30)<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-3 – No Matching Meetings<br>1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.<br>2. The system displays an empty meeting list and the message "No meetings match the filter."<br>3. The user may change or clear the current search/filter criteria.<br>4. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Meeting List Cannot Be Retrieved<br>1. At Step 2 or Step 3 of the Main Flow, the system cannot retrieve the meeting list.<br>2. The system displays MSG38 – "Unable to load meetings. Please try again."<br>3. The use case ends.<br>EX-2 – Invalid Meeting List Criteria<br>1. The system determines that the selected search, filter, sort, or display criteria are invalid.<br>2. The system displays MSG32 – "Tiêu chí danh sách cuộc họp không hợp lệ."<br>3. The user may clear or adjust the search/filter criteria.<br>4. The use case ends. |

### UC-18 – Submit Meeting for Approval

Sequence Diagram: [UC18_SubmitMeetingForApproval.txt](../06-sequencediagram/meeting-management/UC18_SubmitMeetingForApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Organizer to submit a Draft meeting or resubmit a previously Rejected meeting for review by an assigned Meeting Approver based on the Meeting information and planned schedule. |
| **Preconditions** | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to submit the selected meeting for approval.<br>● PRE-4: The meeting is in Draft or Rejected status. |
| **Postconditions** | ● POST-1: The meeting status is WaitingApproval.<br>● POST-2: A valid Meeting Approver is assigned to the current approval cycle.<br>● POST-3: The assigned Meeting Approver is granted access to the meeting for approval purposes.<br>● POST-4: The submission is recorded in the meeting lifecycle history.<br>● POST-5: The assigned Meeting Approver is notified of the pending approval request. |
| **Normal Flow** | Submit Meeting for Approval<br>1. The Meeting Organizer selects a Draft meeting and chooses Submit for Approval.<br>2. The system verifies that all required Meeting information and planned schedule are complete and valid for approval submission. (BR-25, BR-33, BR-62)<br>3. The system identifies users who are eligible to act as Meeting Approvers.<br>4. The system displays the eligible Meeting Approvers.<br>5. The Meeting Organizer selects a Meeting Approver.<br>6. The system verifies that the selected user is eligible to review the meeting and is not the Meeting Organizer.<br>7. The system verifies that the meeting is still in Draft or Rejected status.<br>8. The system assigns the selected Meeting Approver to the current approval cycle.<br>9. The system changes the meeting status to WaitingApproval.<br>10. The system records the submission and assigned approver in the meeting lifecycle history. The assignment, status change and history must all be completed together; otherwise EX-4 applies.<br>11. The system notifies the assigned Meeting Approver.<br>12. The system displays the submission success message. |
| **Alternative Flows** | AF-1 – Resubmit a Rejected Meeting<br>Entry Point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects a meeting in Rejected status.<br>2. The Meeting Organizer chooses Resubmit for Approval.<br>3. The flow resumes at Step 2 of the Main Flow and completes successfully when the submission requirements are satisfied. |
| **Exception Flows** | EX-1 – Incomplete Meeting-Level Information<br>1. At Step 2, the system determines that mandatory Meeting information and planned schedule is incomplete.<br>2. The system identifies the missing or invalid information.<br>3. The meeting remains Draft or Rejected and the use case ends.<br>EX-2 – Meeting Status Not Eligible for Submission<br>1. At Step 7, the system determines that the meeting is not in Draft or Rejected status.<br>2. The system displays MSG41 – "This meeting can no longer be submitted for approval."<br>3. The use case ends.<br>EX-3 – Selected Meeting Approver Is Not Eligible<br>1. At Step 6, the system determines that the selected user is not eligible to review the meeting or is the Meeting Organizer.<br>2. The meeting remains Draft or Rejected and no approval assignment is created.<br>3. The flow resumes at Step 5 of the Main Flow so the Meeting Organizer selects another eligible approver.<br>EX-4 – Submission Cannot Be Completed<br>1. At Steps 8–10, the system cannot complete the approval assignment, status change or submission history.<br>2. The Meeting, its previous approver assignment and its history remain unchanged. No approval notification is issued and the submission is not reported as successful.<br>3. The system displays MSG42 – "Unable to submit the meeting for approval. Please try again."<br>4. The use case ends.<br>EX-5 – No Eligible Meeting Approver Available<br>1. At Step 3, the system finds no eligible Meeting Approver.<br>2. The system displays MSG140 – "No eligible meeting approver is available. Please contact the administrator."<br>3. The Meeting remains Draft or Rejected. Its existing approver assignment and history remain unchanged; no new assignment, submission event or approval notification is created.<br>4. The use case ends unsuccessfully. The Organizer may try again when an eligible approver becomes available. |

### UC-19 – Review Meeting Approval

Sequence Diagram: [UC19_ReviewMeetingApproval.txt](../06-sequencediagram/meeting-management/UC19_ReviewMeetingApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Approver |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it. |
| **Preconditions** | ● PRE-1: The Meeting Approver is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Approver is currently assigned to review the selected meeting and is not its Meeting Organizer.<br>● PRE-4: The meeting is in WaitingApproval status. |
| **Postconditions** | ● POST-1: The Meeting Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history. |
| **Normal Flow** | Review Meeting Approval<br>1. The Meeting Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Meeting Approver.<br>3. The system displays the meeting information for review.<br>4. The Meeting Approver selects Approve.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to approve the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status from WaitingApproval to Approved.<br>7. The system records the approval decision, acting user, and timestamp in the meeting lifecycle history. The decision, status change and history must all be completed together; otherwise EX-3 applies.<br>8. The system displays MSG43 – "Meeting approved successfully." |
| **Alternative Flows** | AF-1 – Reject Meeting<br>1. At Step 4 of the Main Flow, the Meeting Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Meeting Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to reject the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status to Rejected.<br>7. The system records the rejection decision, rejection reason, acting user, and timestamp. The decision, reason, status change and history must all be completed together; otherwise EX-3 applies.<br>8. The rejection reason becomes available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>9. The system displays MSG44 – "Meeting rejected successfully." |
| **Exception Flows** | EX-1 – Rejection Reason Is Missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-36)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting Is No Longer Awaiting Approval<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status.<br>2. No approval or rejection decision is applied.<br>3. The meeting remains in its current status.<br>4. The system displays "This meeting is no longer awaiting approval."<br>5. The use case ends.<br>EX-3 – Approval Decision Cannot Be Completed<br>1. At Steps 6–7 of the Main Flow or AF-1, the system cannot complete the approval or rejection decision, resulting status or decision history.<br>2. The Meeting status, approval assignment, previous decision information and history remain unchanged.<br>3. No new approval or rejection decision is recorded, no notification or invitation is issued for the unsuccessful action, and the action is not reported as successful.<br>4. The system displays "Unable to complete the approval decision. Please try again."<br>5. The use case ends.<br>EX-4 – User Is No Longer Authorized to Review the Meeting<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the Meeting Approver is no longer authorized to approve or reject the selected meeting. (BR-35)<br>2. No approval or rejection decision is applied.<br>3. The meeting status remains unchanged.<br>4. The system informs the Meeting Approver that they are no longer authorized to review the meeting.<br>5. The use case ends.<br>EX-5 – Rejection Reason Is Too Long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |

### UC-20 – Publish Meeting

Sequence Diagram: [UC20_PublishMeeting.txt](../06-sequencediagram/meeting-management/UC20_PublishMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Organizer to publish an Approved meeting and initiate invitation delivery to current internal participants and external Guests. |
| **Preconditions** | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-124, BR-125, BR-129) |
| **Postconditions** | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history.<br>● POST-3: Current internal participants are notified in the application and are sent invitation emails. Each external Guest with a current invitation is sent a separate email invitation to verify their invited email address and access only this Meeting. Unsuccessful delivery is retried without changing Published status. Successful publication does not mean every invitation has already been received. (BR-39, BR-40, BR-152, BR-153) |
| **Normal Flow** | Publish Meeting<br>1. The Meeting Organizer selects an Approved meeting.<br>2. The Meeting Organizer selects Publish Meeting.<br>3. The system verifies that the Meeting Organizer remains authorized, the meeting is still Approved with a valid planned schedule, exactly one current Chair has an active account, and at least one current Secretary has an active account.  (BR-37, BR-124, BR-125, BR-129)<br>4. The system changes the meeting status from Approved to Published. (BR-37)<br>5. The system records the publication action, acting user, and timestamp in the meeting lifecycle history. The Published status and publication history must be completed together before invitations are issued; otherwise EX-2 applies. (BR-38)<br>6. The system notifies current internal participants in the application and sends them invitation emails. Each external Guest with a current invitation receives a separate email invitation to verify their invited email address and access only this Meeting. Sending invitations does not delay confirmation of publication. If delivery fails, the system retries delivery and the Meeting remains Published. (BR-39, BR-40, BR-152, BR-153, BR-156)<br>7. The system displays MSG45 – "Meeting published successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Is No Longer Eligible for Publication<br>1. At Step 3 of the Main Flow, the system determines that the meeting is no longer Approved, the Meeting Organizer is no longer authorized to publish it, there is not exactly one active current Chair, or there is no active current Secretary. (BR-37, BR-124, BR-125)<br>2. The meeting remains unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG46 – "This meeting can no longer be published."<br>5. The use case ends.<br>EX-2 – Meeting Cannot Be Published<br>1. At Steps 4–5 of the Main Flow, the system cannot complete the Published status or publication history.<br>2. The Meeting remains Approved; its previous assignment, decision information and history remain unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG47 – "Unable to publish the meeting. Please try again."<br>5. The use case ends. |

### UC-21 – Start Meeting

Sequence Diagram: [UC21_StartMeeting.txt](../06-sequencediagram/meeting-management/UC21_StartMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Chair to start one Published Meeting, set the Meeting lifecycle to InProgress and record its actual start time. The Meeting has no subordinate scheduling objects. |
| **Preconditions** | ● PRE-1: The Chair is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The meeting is Published.<br>● PRE-4: The meeting has exactly one current Chair and at least one current Secretary with active accounts.  (BR-127) |
| **Postconditions** | ● POST-1: Meeting status is InProgress.<br>● POST-2: Actual Start Time, acting Chair and immutable lifecycle event timestamp are recorded. (BR-38)<br>● POST-3: In-app Meeting-start notifications are created for currently invited internal participants. (BR-164)<br>● POST-4: Online Meeting Room access is governed by the Meeting mode and current status. |
| **Normal Flow** | Start Meeting<br>1. The assigned Chair selects Start Meeting for the Published Meeting.<br>2. The system verifies the Chair is active/current, one active current Chair and at least one active Secretary exist, and the Meeting remains Published. (BR-127, BR-130, BR-131)<br>3. The system changes status to InProgress, saves Actual Start Time and records the lifecycle event. (BR-38, BR-131)<br>4. The system creates in-app notifications for current invited internal participants; a notification delivery problem does not undo a successfully started Meeting. (BR-164)<br>5. The system displays MSG110 – "Meeting started successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Cannot Be Started<br>1. At Step 2, the meeting is no longer Published, the Chair is no longer assigned or active, or the required active Chair/Secretary assignments are no longer valid. (BR-127, BR-130, BR-131)<br>2. The meeting remains unchanged.<br>3. The system displays MSG112 – "This meeting cannot be started."<br>4. The use case ends.<br>EX-2 – Start Cannot Be Completed<br>1. At Step 3, the system cannot complete the lifecycle transition.<br>2. The meeting remains Published.<br>3. The system displays MSG115 – "Unable to start the meeting. Please try again."<br>4. The use case ends. |

### UC-22 – End Meeting

Sequence Diagram: [UC22_EndMeeting.txt](../06-sequencediagram/meeting-management/UC22_EndMeeting.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to end an InProgress Meeting regardless of open Votes, unmarked attendance or remaining Pending Agenda Items. |
| **Preconditions** | ● PRE-1: The actor is the current authorized Chair.<br>● PRE-2: The Meeting is InProgress. (BR-130, BR-132) |
| **Postconditions** | ● POST-1: The Meeting becomes Ended and its Actual End Time is recorded.<br>● POST-2: Any InProgress Agenda Item is completed; remaining Pending Items stay Pending. (BR-159)<br>● POST-3: Online Meeting Room access is closed under existing lifecycle rules. (BR-143) |
| **Normal Flow** | End Meeting<br>1. The Chair selects End Meeting and confirms.<br>2. The system checks current Chair authority and that the Meeting is InProgress. No Vote-closure or attendance-completeness validation is performed. (BR-130, BR-132, BR-159)<br>3. The system completes any InProgress Agenda Item, changes the Meeting to Ended and records Actual End Time and lifecycle history together. Pending Items remain Pending and Votes/ballots remain unchanged.<br>4. The system closes room access and confirms MSG111 – "Meeting ended successfully."<br>5. Voting controls stop accepting ballots because the Meeting is Ended; the Chair may later close outstanding Votes to finalize their tally. |
| **Alternative Flows** | None. |
| **Exception Flows** | EX-1 – Actor or Meeting is not eligible: no end is recorded. MSG114 – "This meeting cannot be ended." The use case ends.<br>EX-2 – End cannot be saved: previous Meeting and Agenda states/timestamps remain unchanged and room closure is not triggered. MSG116 – "Unable to end the meeting. Please try again." The Chair may retry. |

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
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-65, BR-74) |
| **Postconditions** | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting and any selected documents belong to the same Meeting. |
| **Normal Flow** | Create Agenda Item<br>1. The Secretary opens the Meeting Agenda and selects Add Agenda Item.<br>2. The Secretary enters Title, Planned Duration, optional Presenter and Description, and selects available Meeting documents.<br>3. The system verifies the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and document access. (BR-64, BR-65, BR-66, BR-74)<br>4. The system creates the Item as Pending after the last position in the Meeting agenda. Existing Item positions are unchanged. (BR-67)<br>5. The system displays MSG60 – "Agenda item created successfully." and automatically refreshes the complete Agenda for authorized users currently viewing it. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Agenda Item Information<br>1. At Step 3, required values or selected documents are invalid.<br>2. No Item is created. The system displays MSG66 – "Invalid agenda item information." with the applicable field messages.<br>EX-2 – Agenda Planning No Longer Permitted<br>1. At Step 3, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. No Item is created. The system displays MSG67 – "The agenda for this meeting can no longer be modified."<br>EX-3 – Create Cannot Be Saved<br>1. At Step 4, the system cannot save the Item or its document associations.<br>2. The saved Agenda and its order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG68 – "Unable to create the agenda item. Please try again." The use case ends unsuccessfully. |

#### UC-23b – Update Agenda Item

Sequence Diagram: [UC23b_UpdateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23b_UpdateAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to update a Pending Agenda Item, or update only Title and Description after the Item has started. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>● PRE-3: A Pending Item permits changes to its planning fields; an InProgress or Done Item permits only Title and Description changes. (BR-68) |
| **Postconditions** | ● POST-1: The permitted Item information is updated.<br>● POST-2: Meeting membership, Item execution status, actual timestamps and Meeting lifecycle remain unchanged.<br>● POST-3: Authorized users currently viewing the Agenda automatically see the complete updated Agenda. (BR-73) |
| **Normal Flow** | Update Agenda Item<br>1. The Secretary selects an Item and chooses Edit.<br>2. For Pending, the system permits changes to Title, Planned Duration, optional Presenter, Description and document associations. For InProgress or Done, only Title and Description are editable. (BR-68)<br>3. The Secretary enters permitted changes and submits.<br>4. The system rechecks the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), current Item status, permitted fields, required values and any selected Meeting documents. (BR-65, BR-66, BR-68, BR-74)<br>5. The system saves the permitted changes together, preserving execution status and actual timestamps.<br>6. The system displays MSG61 – "Agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Agenda Item Information<br>1. At Step 4, the submitted values or documents are invalid.<br>2. The Item remains unchanged. The system displays MSG66 with the applicable field messages.<br>EX-2 – Editing No Longer Permitted<br>1. At Step 4, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The Item remains unchanged. The system displays MSG67.<br>EX-3 – Update Cannot Be Saved<br>1. At Step 5, the system cannot save the permitted changes.<br>2. Item information, document associations, execution status, actual timestamps and order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG69 – "Unable to update the agenda item. Please try again." The use case ends unsuccessfully.<br>EX-4 – Changes Not Permitted for the Current Item Status<br>1. At Step 4, the Item is InProgress or Done and the request changes a field other than Title or Description, including when the Item started after the edit form was opened.<br>2. No changes are saved. The system displays MSG141 – "Only Title and Description can be changed after an agenda item has started."<br>3. The system refreshes the permitted fields. The Secretary may revise the request and continue from Step 3. |

#### UC-23c – Delete Agenda Item

Sequence Diagram: [UC23c_DeleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC23c_DeleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to delete only a Pending Agenda Item from the Meeting. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to a Meeting in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>● PRE-3: The Item is Pending. InProgress and Done Items cannot be deleted. (BR-68) |
| **Postconditions** | ● POST-1: The selected Pending Item is removed.<br>● POST-2: Remaining Item positions and execution data, and the Meeting lifecycle, are unchanged.<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |
| **Normal Flow** | Delete Agenda Item<br>1. The Secretary selects a Pending Item, chooses Delete and confirms.<br>2. The system verifies the active Secretary assignment, permitted Meeting status, Item membership and that the Item is still Pending. (BR-64, BR-65, BR-68)<br>3. The system removes the Item while retaining every remaining Item position.<br>4. The system displays MSG62 – "Agenda item deleted successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Planning No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. Nothing is deleted. The system displays MSG67.<br>EX-2 – Agenda Item Has Already Started<br>1. At Step 2, the Item is InProgress or Done.<br>2. The Item and Agenda order remain unchanged. The system displays MSG142 – "Only Pending agenda items can be deleted."<br>EX-3 – Deletion Cannot Be Saved<br>1. At Step 3, the system cannot complete deletion.<br>2. The Item and the saved Agenda order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG70 – "Unable to delete the agenda item. Please try again." The use case ends unsuccessfully. |

#### UC-23d – Reorder Agenda Items

Sequence Diagram: [UC23d_ReorderAgendaItems.txt](../06-sequencediagram/agenda-management/UC23d_ReorderAgendaItems.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Secretary to exchange Pending Agenda Item positions within the same Meeting, keeping InProgress and Done positions unchanged. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains Pending Items to reorder. (BR-65, BR-68) |
| **Postconditions** | ● POST-1: Pending Items occupy the same set of positions in the requested order.<br>● POST-2: InProgress and Done positions, Item membership and execution data remain unchanged.<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |
| **Normal Flow** | Reorder Agenda Items<br>1. The Secretary changes the relative order of Pending Items. Positions occupied by InProgress or Done Items are fixed.<br>2. The system verifies the active Secretary assignment, permitted Meeting status and the latest saved Agenda. (BR-65, BR-74)<br>3. The system verifies that each current Pending Item appears exactly once, all Items belong to this Meeting and only the existing Pending positions are exchanged. InProgress and Done positions must remain unchanged. (BR-64, BR-67, BR-68)<br>4. The system saves the new Pending order together.<br>5. The system displays MSG63 – "Agenda order updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Reordering No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The saved order remains unchanged. The system displays MSG67.<br>EX-2 – Invalid or Outdated Agenda Order<br>1. At Step 3, the request duplicates or omits a current Pending Item, includes an Item from another Meeting, changes an InProgress or Done position, or no longer matches the current Pending Items and positions.<br>2. No order changes are saved. The system displays MSG143 – "Only Pending agenda items can be reordered within their existing positions. Refresh the agenda and try again."<br>3. The system refreshes the complete Agenda. The Secretary may revise the order and continue from Step 1.<br>EX-3 – Order Cannot Be Saved<br>1. At Step 4, the system cannot save the requested order.<br>2. All Item positions remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG71 – "Unable to update the agenda order. Please try again." The use case ends unsuccessfully. |

### UC-24 – Set Current Agenda Item

Sequence Diagram: [UC24_SetCurrentAgendaItem.txt](../06-sequencediagram/agenda-management/UC24_SetCurrentAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to start the first Pending Agenda Item in the current Meeting order. |
| **Preconditions** | ● PRE-1: The Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Item is the first Pending Item in its current order. (BR-69, BR-70)<br>● PRE-3: No other Item of the Meeting is InProgress. (BR-71) |
| **Postconditions** | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |
| **Normal Flow** | Set Current Agenda Item<br>1. The Chair selects the first Pending Item in the current Meeting order and chooses Set as Current.<br>2. The system rechecks the active Chair assignment, Meeting InProgress status and that the selected Item belongs to the Meeting and remains Pending. (BR-64, BR-69)<br>3. The system verifies that no other Item is InProgress and that the selected Item is still the first Pending Item in the latest saved order. (BR-70, BR-71)<br>4. The system changes the Item to InProgress and records its actual start time together. If competing requests arrive, only one Item may start. (BR-70, BR-71, BR-72)<br>5. The system displays MSG64 – "Current agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Item Cannot Become Current<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the selected Item does not belong to the Meeting or is not Pending.<br>2. The Agenda remains unchanged. The system displays MSG73 – "This agenda item cannot be set as current."<br>EX-2 – Another Agenda Item Is InProgress<br>1. At Step 3 or Step 4, another Item is already InProgress.<br>2. The selected Item remains Pending. The system displays MSG72 – "Another agenda item is already in progress. Complete it before starting another agenda item."<br>EX-3 – Selected Item Is Not the First Pending Item<br>1. At Step 3 or Step 4, a different Item is first in the current Pending order.<br>2. The Agenda remains unchanged. The system displays MSG144 – "Start the first Pending agenda item in the current order." and refreshes the Agenda.<br>EX-4 – Current Agenda Item Cannot Be Saved<br>1. At Step 4, the system cannot save the Item status or actual start time.<br>2. The selected Item remains Pending; its timestamps and all other Agenda data remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG75 – "Unable to set the current agenda item. Please try again." The use case ends unsuccessfully. |

### UC-25 – Complete Agenda Item

Sequence Diagram: [UC25_CompleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC25_CompleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the active assigned Chair to complete the current Item regardless of how its actual duration compares with its planned duration. |
| **Preconditions** | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |
| **Postconditions** | ● POST-1: The current Item becomes Done with its actual end time and actual duration.<br>● POST-2: The Meeting remains InProgress. No next Item starts automatically. (BR-72)<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |
| **Normal Flow** | Complete Agenda Item<br>1. The Chair selects Complete for the current Item.<br>2. The system verifies the active Chair assignment, Meeting InProgress status, Item membership and current InProgress status. Planned Duration does not restrict completion. (BR-64, BR-69, BR-70, BR-72)<br>3. The system records actual end time and elapsed duration and changes the Item to Done together. (BR-70, BR-72)<br>4. The Meeting stays InProgress with no current Item. The Chair must explicitly start the first remaining Pending Item through UC-24.<br>5. The system displays MSG65 – "Agenda item completed successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Item Cannot Be Completed<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the Item does not belong to the Meeting or is not its current InProgress Item.<br>2. Nothing changes. The system displays MSG74 – "This agenda item cannot be completed."<br>EX-2 – Agenda Item Completion Cannot Be Saved<br>1. At Step 3, the system cannot save completion, actual end time or actual duration.<br>2. The Item remains InProgress with unchanged timestamps. The Meeting remains InProgress. No successful change is announced to viewers.<br>3. The system displays MSG76 – "Unable to complete the agenda item. Please try again." The use case ends unsuccessfully. |

### UC-26 – View Meeting Agenda

Sequence Diagram: [UC26_ViewMeetingAgenda.txt](../06-sequencediagram/agenda-management/UC26_ViewMeetingAgenda.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view the complete Meeting Agenda and automatically see saved changes while the Agenda remains open. |
| **Preconditions** | ● PRE-1: The user may view the selected Meeting. (BR-29) |
| **Postconditions** | ● POST-1: Agenda title, presenter, planned duration, order, execution status and recorded actual times are shown read-only.<br>● POST-2: The complete Agenda automatically reflects successful creation, editing, deletion, reordering, Item start/completion and completion of the current Item when the Meeting ends. (BR-73)<br>● POST-3: Viewing does not modify the Agenda. |
| **Normal Flow** | View Meeting Agenda<br>1. The user opens the Meeting Agenda tab.<br>2. The system verifies Meeting access and retrieves the complete saved Agenda in its current order. (BR-29, BR-64)<br>3. The system shows Item title, presenter, planned duration, status, order and recorded actual times.<br>4. While the user continues viewing, the system automatically refreshes the complete Agenda after a successful Agenda change, including changes made by another authorized user. Manual page refresh is not required. (BR-73)<br>5. The system rechecks access before displaying refreshed Agenda data. Failed changes are not shown as successful updates. |
| **Alternative Flows** | AF-1 – Empty Meeting Agenda<br>1. At Step 3 or Step 4, no Agenda Items exist.<br>2. The system displays the empty Agenda state.<br>3. While the view remains open, it automatically reflects subsequent saved Agenda changes. The use case succeeds. |
| **Exception Flows** | EX-1 – Meeting Not Accessible<br>1. At Step 2 or Step 5, the Meeting does not exist or the user lacks access.<br>2. No further Agenda information is disclosed. The use case ends.<br>EX-2 – Meeting Agenda Cannot Be Retrieved<br>1. At Step 2 or Step 4, the system cannot retrieve the latest Agenda.<br>2. The system displays MSG128 – "Unable to load the meeting agenda. Please try again." A retrieval failure is not displayed as an empty Agenda.<br>3. No Agenda data changes; previously displayed information is not presented as the latest saved Agenda. The user may retry. |

---

## FE-06 — Meeting Participant Management

### UC-27 – View Meeting Participants

Sequence Diagram: [UC27_ViewMeetingParticipants.txt](../06-sequencediagram/meeting-participant-management/UC27_ViewMeetingParticipants.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized internal participant or verified Guest to view and search the current Meeting roster, including internal participants and Guests. |
| **Preconditions** | PRE-1: The actor has current authorized Meeting participation or verified Guest access.<br>PRE-2: Secretary access follows existing prepublication rules; from Published onward Chair may view by default, while Member or Guest needs View Meeting Participants for this Meeting. (BR-117–BR-121, BR-176) |
| **Postconditions** | ● POST-1: The authorized current roster is displayed.<br>● POST-2: No data is changed. |
| **Normal Flow** | View Meeting Participants<br>1. The actor opens the Participants view of an accessible Meeting.<br>2. The system rechecks current Meeting access and roster-view permission. (BR-176)<br>3. The system displays current internal participants and Guests with invitation identity, Meeting Role or Guest participation type; for a Guest with a completed profile show the self-declared display name and optional job title separately from verified email. Removed/revoked entries are excluded. (BR-103, BR-180)<br>4. The actor reviews the roster. |
| **Alternative Flows** | AF-1 – Search participants: the actor searches by name or email and reviews matching current entries. |
| **Exception Flows** | EX-1 – Roster is not accessible: the system does not disclose the roster. (BR-121)<br>EX-2 – Roster cannot be retrieved: the system reports that the roster could not be loaded. (MSG95) |

### UC-28 – Manage Meeting Participants

#### UC-28a – Add Meeting Participant

Sequence Diagram: [UC28a_AddMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28a_AddMeetingParticipant.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Secretary |
| **Secondary Actors** | Email / SMTP Service |
| **Description** | Allows the Organizer to assign a Secretary when none is current in Draft, Rejected or Approved, initially or after removal; current Secretaries add internal participants or invite a Guest. |
| **Preconditions** | PRE-1: The actor is a current assigned Secretary, or the Organizer restoring/initializing a missing Secretary in Draft, Rejected or Approved.<br>PRE-2: Roster management is permitted. WaitingApproval remains read-only. (BR-78–BR-80, BR-122) |
| **Postconditions** | POST-1: All selected internal participants or the one Guest invitation are saved.<br>POST-2: Invitation Response starts Pending.<br>POST-3: Eligible additional viewing permissions apply to this Meeting only. Internal Meeting Roles are assigned only to internal participants; Guest receives no internal/system Role. (BR-150, BR-174) |
| **Normal Flow** | Add Internal Participants<br>1. The actor selects Add Participants.<br>2. A current Secretary selects active internal users, a shared Meeting Role and eligible additional permissions. When no Secretary is current in Draft, Rejected or Approved, the Organizer selects exactly one active internal user as Secretary without extras. This recovery is available even if a Secretary was previously assigned. (BR-78, BR-79, BR-122)<br>3. The actor confirms.<br>4. The system rechecks current authority, status, selected accounts, uniqueness, required roles and permitted grants. (BR-80–BR-85, BR-122, BR-168)<br>5. The system saves all selected participants together, initializes Pending responses and arranges invitations when Published/InProgress.<br>6. The system displays MSG96 – "Meeting participant added successfully." |
| **Alternative Flows** | AF-1 – Invite external Guest: at Step 2, the Secretary enters an invitation name and email and optional permitted additional Meeting permissions. For Confidential/Secret, the ordinary form displays an inline warning, without another confirmation step. The system validates uniqueness, saves the invitation and requested permissions together, and arranges email delivery when Published or InProgress. The Guest later verifies email and self-declares display information in UC-04. The use case succeeds with MSG96 – "Meeting participant added successfully." (BR-148–BR-157, BR-174, BR-180–BR-182)AF-2 – Restore Secretary from an existing Member: at Step 2, when no current Secretary exists in Draft/Rejected/Approved, the Organizer selects one active current Member. The system validates recovery authority and changes that Member to Secretary without adding a duplicate or resetting invitation response/attendance. No new optional grants are made. The use case succeeds with MSG100 – "Meeting role updated successfully." |
| **Exception Flows** | EX-1 – Selection is not allowed: no user in an internal batch is added, or no invalid Guest invitation is created. (MSG97 or MSG152)<br>EX-2 – Participant management is not allowed: no addition occurs. (BR-78–BR-80, MSG98)<br>EX-3 – Addition cannot be completed: no partial internal batch is added. (MSG99) |

#### UC-28b – Manage Meeting Role & Additional Permissions

Sequence Diagram: [UC28b_AssignChangeMeetingRole.txt](../06-sequencediagram/meeting-participant-management/UC28b_AssignChangeMeetingRole.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to change internal Meeting Roles or eligible additional permissions, including Guest permission-only changes and internal Chair replacement. |
| **Preconditions** | PRE-1: The actor is a current authorized Secretary.<br>PRE-2: The Meeting permits participant management and the selected target is a current internal participant or current Guest invitation. (BR-79, BR-80) |
| **Postconditions** | ● POST-1: Approved role or additional-permission changes are saved for this Meeting.<br>● POST-2: Chair replacement changes both affected Meeting Roles together. System Role and Invitation Response remain unchanged. |
| **Normal Flow** | Change Internal Role or Permissions<br>1. The current Secretary selects a current internal participant.<br>2. The system displays the participant’s role and additional permissions.<br>3. The Secretary selects changes and confirms.<br>4. The system checks current authority/status, at-most-one Chair and permitted grants. Draft/Rejected/Approved may end with no Secretary; Published/InProgress must retain or atomically replace the last Secretary. (BR-122, BR-123, BR-174–BR-177)<br>5. The system saves consistently and confirms MSG100 – "Meeting role updated successfully." or MSG153 – "Meeting permissions updated successfully." |
| **Alternative Flows** | AF-1 – Replace Chair: at Step 3, the Secretary selects a current internal participant as the new Chair and confirms. The system changes the former Chair to Member and the selected internal participant to Chair together, retaining required Secretary roles. The use case succeeds with MSG100 – "Meeting role updated successfully." (BR-122, BR-169)AF-2 – Change Guest permissions: at Step 1, the Secretary selects a current Guest invitation. The system shows grantable additional Meeting permissions; the additional Meeting permissions are examples; no internal role selector is available. The Secretary edits the grants; the system validates and saves them under BR-174–BR-177 and notifies the Guest by email when Published/InProgress. The use case succeeds with MSG153 – "Meeting permissions updated successfully." |
| **Exception Flows** | EX-1 – Role or permissions are not allowed: no changes are saved; the Secretary may revise the selection. (MSG101, MSG109 or MSG152)<br>EX-2 – Meeting or actor is no longer eligible: no changes are applied. (MSG98)<br>EX-3 – Update cannot be completed: existing roles and permissions remain unchanged. (MSG102) |

#### UC-28c – Remove Meeting Participant

Sequence Diagram: [UC28c_RemoveMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28c_RemoveMeetingParticipant.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows an assigned Secretary to remove a current internal participant or revoke a Guest invitation while preserving required Meeting records. |
| **Preconditions** | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting permits participant management. (BR-79, BR-80) |
| **Postconditions** | ● POST-1: The selected participant is no longer current or the Guest invitation is revoked.<br>● POST-2: Access through the removed relationship ends.<br>● POST-3: Required historical invitation and attendance evidence is retained. (BR-97, BR-100–BR-103) |
| **Normal Flow** | Remove Participant<br>1. The current Secretary selects an internal participant or Guest invitation and confirms removal/revocation.<br>2. The system rechecks authority and editable status. Draft/Rejected/Approved may temporarily have no Secretary; Published/InProgress must retain a Secretary and the current Chair under their replacement rules. (BR-79, BR-80, BR-122, BR-126)<br>3. The system removes/revokes the selected relationship and its access, retaining historical evidence.<br>4. The system displays MSG103 – "Meeting participant removed successfully." If no Secretary remains in preparation, the Organizer may assign one through UC-28a. |
| **Alternative Flows** | None. |
| **Exception Flows** | EX-1 – Removal is not allowed: the actor, Meeting status or required roles prohibit the action. Nothing is removed. (MSG98 or MSG109)<br>EX-2 – Removal cannot be completed: the prior assignment or invitation remains effective. (MSG104) |

### UC-29 – Respond to Meeting Invitation

Sequence Diagram: [UC29_RespondToMeetingInvitation.txt](../06-sequencediagram/meeting-participant-management/UC29_RespondToMeetingInvitation.txt)

| **Primary Actors** | Chair, Secretary, Participant, Guest | **Secondary Actors** | None |
| --- | --- | --- | --- |
| **Description** | Allows an invitee to accept or decline their own Meeting invitation, or change an earlier response while responses are allowed. |  |  |
| **Preconditions** | ● PRE-1: The Meeting is Published or InProgress.<br>● PRE-2: The actor owns a current invitation; a Guest has verified access through UC-04. (BR-104–BR-106, BR-148–BR-156) |  |  |
| **Postconditions** | ● POST-1: The latest response and response time are recorded.<br>● POST-2: Meeting Role, attendance and additional permissions are unchanged. |  |  |
| **Normal Flow** | Respond to Invitation<br>1. The invitee opens their Meeting invitation.<br>2. The system identifies the invitee and displays the current response.<br>3. The invitee selects Accepted or Declined.<br>4. The system checks that the invitation can still be answered and records the response.<br>5. The system confirms the update. (MSG105) |  |  |
| **Alternative Flows** | AF-1 – Change previous response: the invitee changes an earlier response while the Meeting still allows responses, then continues at Step 3. |  |  |
| **Exception Flows** | EX-1 – Invitation is not answerable: no change is made. Guest verification remains in UC-04. (MSG107)<br>EX-2 – Response is invalid or cannot be recorded: the prior response is retained. (MSG106 or MSG108) |  |  |

### UC-30 – View Participant Response Status

Sequence Diagram: [UC30_ViewParticipantResponseStatus.txt](../06-sequencediagram/meeting-participant-management/UC30_ViewParticipantResponseStatus.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary, Chair, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows authorized internal participants and verified Guests to view current invitation responses and totals for the Meeting. |
| **Preconditions** | PRE-1: The Meeting is Published or a later status that remains readable.<br>PRE-2: Current Secretary or Chair may view by default; Member or verified current Guest needs View Invitation Responses for this Meeting. (BR-176) |
| **Postconditions** | ● POST-1: Latest responses for current invitees and totals by response status are displayed.<br>● POST-2: No data is changed. |
| **Normal Flow** | View Participant Response Status<br>1. The actor selects Response Status for an accessible Meeting.<br>2. The system checks that the actor may view responses. (BR-176)<br>3. The system displays each current invitee’s name, email and latest response with totals by status. Response-view permission includes identity in response rows but does not grant roster browsing or search. (BR-103, BR-115, BR-176)<br>4. The actor reviews the summary. |
| **Alternative Flows** | None. An all-Pending result is a normal response summary. |
| **Exception Flows** | EX-1 – Responses are not accessible: the system does not disclose unauthorized response information. (BR-121)<br>EX-2 – Responses cannot be retrieved: the system reports that the data could not be loaded. (MSG95) |

### UC-31 – Record Attendance

Phạm vi implementation: **SCRUM-26**, tách khỏi SCRUM-20. SCRUM-20 vẫn phải giữ lịch sử response/attendance khi xoá/revoke.

Sequence Diagram: [UC31_RecordAttendance.txt](../06-sequencediagram/meeting-participant-management/UC31_RecordAttendance.txt)


| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to record or revise attendance for one or more selected current internal participants or Guests while the Meeting is InProgress. |
| **Preconditions** | PRE-1: The actor is an active current assigned Secretary.<br>PRE-2: The Meeting is InProgress. (BR-170) |
| **Postconditions** | POST-1: Submitted people have Present or Absent saved with the recording Secretary and save time.<br>POST-2: Unsubmitted people retain their previous value, including unmarked. Invitation responses remain unchanged.<br>POST-3: All changes in this submission succeed together or none are saved; completeness never gates End Meeting. (BR-116, BR-171, BR-172, BR-178) |
| **Normal Flow** | Record Attendance<br>1. The Secretary opens Attendance for an InProgress Meeting.<br>2. The system displays current internal participants and Guests with saved attendance; never-recorded people are unmarked.<br>3. The Secretary selects Present or Absent for one or more people and submits only those entries.<br>4. The system rechecks authority, Meeting status, each submitted person’s current membership and valid value. People omitted from the submission are not validation errors. (BR-170–BR-172)<br>5. The system saves the submitted changes together, retains every other person’s saved or unmarked value, and displays MSG147 – "Meeting attendance saved successfully." |
| **Alternative Flows** | AF-1 – Revise saved attendance: at Step 3, the Secretary changes one or more saved values and continues at Step 4 while the Meeting remains InProgress.<br>AF-2 – Record the full roster: at Step 3, the Secretary selects every current person; the same validation and save rules apply, then the flow resumes at Step 4. |
| **Exception Flows** | EX-1 – Submitted value or person is invalid: no submitted change is saved. MSG148 – "Select Present or Absent for each submitted participant or Guest." The flow resumes at Step 3.<br>EX-2 – Recording is no longer permitted: authority or Meeting status changed; nothing is saved. For status restriction show MSG149 – "Attendance can only be recorded while the meeting is in progress." The use case ends.<br>EX-3 – Save fails: all previous values remain unchanged. MSG150 – "Unable to save attendance. No changes were saved." The flow resumes at Step 3.<br>EX-4 – A submitted participant changed or was removed/revoked: no submitted change is saved. MSG151 – "The meeting participant list has changed. Refresh it and try again." Refresh the current roster and resume at Step 2. Unrelated new roster entries do not make the submission incomplete. |

## FE-07 — Personal Document Library & Meeting Document Management

Review draft — 14/10/2026: All fourteen detailed Document use cases are specified for review. BR statements are atomic and active MSG wording is English. The document-name constraint is removed. Anyone who can view a Meeting document may comment, without a separate commenting permission. MSG182 and MSG201 are retired. Commenting lifecycle limits, text/name length limits and personal-library quotas are not newly decided here. Guest additional-permission catalogue and grant matrix remain outside these Document specifications. Cancel actions are omitted.

### UC-32 – View Meeting Documents

#### UC-32a – View Meeting Documents

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to browse the documents currently attached to one Meeting. Guest document access follows Participant rules. (BR-181, BR-205) |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected Meeting exists and the user is currently entitled to read its documents. (BR-181, BR-198, BR-233, BR-234, BR-153, BR-155, BR-157) |
| **Postconditions** | ● POST-1: Only currently attached documents available to the user are displayed.<br>● POST-2: Meeting documents, personal libraries and Meeting status remain unchanged. |
| **Normal Flow** | View Meeting Documents<br>1. The user opens the Documents tab of the selected Meeting.<br>2. The system checks the user's current Meeting-document access, including the current invitation for a Guest. Organizer, Chair and Secretary access is available before publication; Participant and Guest access starts with publication. Approver status alone grants no document-reading access. (BR-181, BR-198, BR-233, BR-234, BR-200)<br>3. The system displays currently attached documents with their names, file types, associated Agenda Items where present, the people who added them and comment counts. Removed documents are excluded. (BR-211)<br>4. The user reviews the displayed list or empty result, and may adjust the Agenda Item filter or finish viewing. Selecting a document opens UC-32c. |
| **Alternative Flows** | AF-1 – Filter by Agenda Item<br>1. At Step 4, the user selects an Agenda Item.<br>2. The system displays only available documents associated with that item.<br>3. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – No documents to display<br>1. At Step 3, the Meeting has no currently attached documents, or the filter in AF-1 has no matches.<br>2. The system displays an empty list and explains that no documents are available for the current selection.<br>3. The user may clear the filter. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Meeting documents are not accessible<br>1. At Step 2, the Meeting is unavailable or the user's document-reading access has ended, including removal of a participant or revocation or expiry of a Guest invitation.<br>2. No document names or contents are disclosed. No Meeting information is changed. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Document list cannot be loaded<br>1. At Step 3, the system cannot retrieve the available document list.<br>2. No Meeting information is changed. The system displays MSG157 – “Unable to load the document list. Please try again.”; the failed load is not presented as an empty list.<br>3. The use case ends. |

#### UC-32c – Preview Meeting Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to read a Meeting document online and download the same file when permitted. Comments are covered separately in UC-35. (BR-181, BR-188) |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected document is currently attached to the Meeting and the user has current document-reading access. (BR-181, BR-211, BR-198, BR-233, BR-234, BR-153, BR-155, BR-157) |
| **Postconditions** | ● POST-1: The selected document is displayed for online reading; in AF-1, the user also receives the permitted download.<br>● POST-2: The file content, its Meeting association and its comments remain unchanged. |
| **Normal Flow** | Preview Meeting Document<br>1. The user selects a document in the Meeting Documents tab or the documents of an Agenda Item.<br>2. The system checks current document-reading access and that the document is still attached to this Meeting. Organizer, Chair and Secretary may read before publication; Participant and Guest may read from publication onward; Approver status alone grants no reading access. (BR-181, BR-211, BR-198, BR-233, BR-234, BR-200)<br>3. The system opens the document viewer and displays the file name, its content and the Meeting sensitivity. A PDF is shown page by page and a DOCX as a continuous scrollable document. <br>4. The user reads the document. The system offers downloading only when the Meeting is not Secret. (BR-188) |
| **Alternative Flows** | AF-1 – Download the document<br>1. At Step 4, the user chooses Download for a Meeting that is not Secret.<br>2. The system rechecks current access, that the document remains attached and the current Meeting sensitivity. (BR-181, BR-211, BR-188, BR-198, BR-233, BR-200)<br>3. The system provides the same uploaded file to the user. (BR-183, BR-216)<br>4. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Document is no longer accessible<br>1. At Step 2, or during the access check in AF-1, the document was removed or the user's Meeting-document access is no longer valid.<br>2. No further document content or download is provided. The file and Meeting remain unchanged. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Preview cannot be displayed<br>1. At Step 3, the system cannot display the selected file.<br>2. The document remains attached and its content is unchanged. The system displays MSG158 – “Unable to display the document. Please try again.”<br>3. The use case ends.<br>EX-3 – Download is prohibited<br>1. During Step 2 of AF-1, the Meeting is Secret, including when its sensitivity changed after the viewer opened.<br>2. No download is provided. The document remains unchanged. The system displays MSG159 – “Secret Meeting documents can only be viewed online and cannot be downloaded.” (BR-188)<br>3. The flow resumes at Step 4 of the Main Flow for online reading.<br>EX-4 – Download cannot be provided<br>1. At Step 3 of AF-1, the system cannot provide the file.<br>2. The file and its Meeting association remain unchanged. The system displays MSG160 – “Unable to download the document. Please try again.”<br>3. The flow resumes at Step 4 of the Main Flow. |

### UC-33 – Manage Meeting Documents

#### UC-33a – Add Document to Meeting

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to add a document from their own personal library or computer to a Meeting, optionally associating it with an Agenda Item. A file is not revised in place: replacing content requires adding a new file and removing the old document through UC-33c. (BR-184, BR-206, BR-208) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the selected Meeting.<br>● PRE-2: The Meeting exists. (BR-184)<br>● PRE-3: The Meeting has not ended; any selected Agenda Item is Pending or InProgress. (BR-195, BR-196) |
| **Postconditions** | ● POST-1: The selected document appears in the Meeting Documents list with its optional Agenda Item, the actor and addition time.<br>● POST-2: A file selected from the personal library stays there unchanged; a file uploaded from the computer is also available in the actor's personal library. (BR-183, BR-210, BR-192, BR-194)<br>● POST-3: Meeting lifecycle status is unchanged. |
| **Normal Flow** | Add Document from Personal Library<br>1. The Chair or Secretary opens Meeting Documents and chooses Add Document.<br>2. The system offers selection from the actor's personal library or upload from the computer.<br>3. The actor selects a document from their own library, optionally selects an Agenda Item of this Meeting and submits the addition.<br>4. The system rechecks the current Chair or Secretary assignment, ownership and availability of the selected document, that the Meeting has not ended, and that any selected Agenda Item is currently Pending or InProgress. A file already attached to this Meeting is not added a second time. (BR-184, BR-209, BR-193, BR-195, BR-196)<br>5. The system adds the document to the Meeting without changing the file or removing it from the personal library, and records the actor and addition time. (BR-183, BR-197)<br>6. The system updates the Meeting Documents list and displays MSG161 – “Document added to the Meeting successfully.” |
| **Alternative Flows** | AF-1 – Upload from the computer<br>1. At Step 3, the actor chooses a file from the computer, optionally selects an Agenda Item and submits the addition.<br>2. The system checks that the actual file is PDF or DOCX and does not exceed 50 MB. (BR-187, BR-214, BR-215)<br>3. The system performs the current-authority, Meeting lifecycle and Agenda Item checks in Step 4. (BR-184, BR-195, BR-196)<br>4. The system adds the document to the Meeting and places it in the uploader's personal library, in the folder “<Meeting title> - dd/mm/yyyy” The date is the upload date in Vietnam time. The existing folder for that user and Meeting is reused even if renamed or moved. If it was deleted, a new folder is created at the library root using this upload date. Later uploads do not rename an existing folder. (BR-210, BR-192, BR-226, BR-227, BR-228)<br>5. Both additions must complete together; otherwise EX-5 applies. (BR-194) The flow resumes at Step 6 of the Main Flow. |
| **Exception Flows** | EX-1 – Actor is no longer authorized<br>1. At Step 4, or the corresponding check in AF-1, the actor is no longer the current Chair or Secretary.<br>2. No document is added to the Meeting or personal library. Existing documents remain unchanged. The system displays MSG162 – “You no longer have permission to add documents to this Meeting.”<br>3. The use case ends.<br>EX-2 – Selection is missing or no longer available<br>1. At Step 4, no document was selected, the library document is unavailable or does not belong to the actor, or the selected Agenda Item is unavailable in this Meeting.<br>2. No document is added. The system identifies the invalid selection and displays MSG163 – “Select your own available document and, if applicable, an available Agenda Item in this Meeting.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Uploaded file is invalid<br>1. At Step 2 of AF-1, no file was selected, the actual file is not a valid PDF or DOCX, or it exceeds 50 MB. (BR-187, BR-214, BR-215)<br>2. No document is added to the Meeting or personal library. The system displays MSG164 – “Select a valid PDF or DOCX file no larger than 50 MB.”<br>3. The flow resumes at Step 1 of AF-1.<br>EX-4 – Document is already attached<br>1. At Step 4, the same previously uploaded file is already attached to this Meeting. A matching name or identical content in a separate upload is not a duplicate. (BR-193, BR-229)<br>2. No duplicate is added and existing documents remain unchanged. The system displays MSG165 – “This document is already attached to the Meeting.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – Addition cannot be completed<br>1. At Step 5 or Step 4 of AF-1, the system cannot complete the addition.<br>2. No new Meeting document or personal-library entry is recorded (BR-194); existing documents remain unchanged. The system displays MSG166 – “Unable to add the document. No document was added. Please try again.”<br>3. The use case ends.<br>EX-6 – Meeting has ended<br>1. At Step 4, or the corresponding check in AF-1, the Meeting is Ended or in a subsequent lifecycle state, including when it ended after the Add Document dialog opened. (BR-195)<br>2. No document is added to the Meeting or personal library. Existing documents remain unchanged. The system displays MSG172 – “Documents cannot be added after the Meeting has ended.”<br>3. The use case ends.<br>EX-7 – Agenda Item is completed<br>1. At Step 4, or the corresponding check in AF-1, the selected Agenda Item is Done, including when it completed after selection. (BR-196)<br>2. No document is added to the Meeting or personal library. The system displays MSG173 – “Documents cannot be added to or removed from a completed Agenda Item.”<br>3. The actor may select a Pending or InProgress Item or omit the optional association. The flow resumes at Step 3 of the Main Flow, or Step 1 of AF-1 for a computer upload.<br>EX-8 – Meeting is unavailable<br>1. Before displaying or completing the addition, the selected Meeting cannot be accessed.<br>2. No document is added to the Meeting or personal library. The system displays MSG174 – “This Meeting is not accessible.” without disclosing whether the Meeting exists.<br>3. The use case ends. |

#### UC-33c – Remove Document from Meeting

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the current Chair or Secretary to remove a Meeting document when no Minutes exist and the document is not associated with a completed Agenda Item. (BR-184, BR-185, BR-196) |
| **Preconditions** | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the Meeting. (BR-184)<br>● PRE-2: The selected document is currently attached to this Meeting.<br>● PRE-3: No Meeting Minutes exist for this Meeting. (BR-185)<br>● PRE-4: The document is not associated with a Done Agenda Item. (BR-196) |
| **Postconditions** | ● POST-1: The document no longer appears in this Meeting's current document list and is no longer available through this attachment.<br>● POST-2: Its comments remain as history; personal-library entries and attachments in other Meetings remain unchanged. (BR-212, BR-231, BR-197)<br>● POST-3: The actor and removal time are recorded. Meeting lifecycle status is unchanged. |
| **Normal Flow** | Remove Document from Meeting<br>1. The Chair or Secretary selects an attached document and chooses Remove from Meeting.<br>2. The system displays a confirmation naming the document and explaining that it will leave this Meeting's list, while its comments remain as history and personal-library documents and other Meetings are unaffected.<br>3. The actor confirms removal.<br>4. The system rechecks the current Chair or Secretary assignment, that the document is still attached, that no Meeting Minutes exist, and that the document is not associated with a Done Agenda Item. Pending and InProgress Items do not block removal. (BR-184, BR-185, BR-196)<br>5. The system removes the document from this Meeting's current list, preserves its comments as history and records the actor and removal time. (BR-211, BR-212, BR-231, BR-197)<br>6. The system displays MSG167 – “Document removed from the Meeting successfully.” |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Minutes already exist<br>1. At Step 4, Meeting Minutes exist, including Minutes created after the confirmation was displayed. (BR-185)<br>2. The document remains attached and its comments remain unchanged. The system displays MSG168 – “The document cannot be removed because Meeting Minutes already exist.”<br>3. The use case ends.<br>EX-2 – Actor is no longer authorized<br>1. At Step 4, the actor is no longer the current Chair or Secretary. (BR-184)<br>2. No document is removed and comments remain unchanged. The system displays MSG169 – “You no longer have permission to remove documents from this Meeting.”<br>3. The use case ends.<br>EX-3 – Document is no longer attached<br>1. At Step 4, the selected document is no longer currently attached to this Meeting.<br>2. No further removal or comment change is made. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The use case ends; the actor may refresh the list through UC-32a.<br>EX-4 – Removal cannot be completed<br>1. At Step 5, the system cannot complete the removal.<br>2. The document remains attached and its comments remain unchanged; no successful removal is recorded. The system displays MSG171 – “Unable to remove the document. It remains attached to the Meeting. Please try again.”<br>3. The use case ends.<br>EX-5 – Agenda Item is completed<br>1. At Step 4, the document is associated with a Done Agenda Item, including when the Item completed after the confirmation was displayed. (BR-196)<br>2. The document remains attached and its comments remain unchanged. The system displays MSG173 – “Documents cannot be added to or removed from a completed Agenda Item.”<br>3. The use case ends.<br>EX-6 – Meeting is unavailable<br>1. Before displaying or completing the removal, the selected Meeting cannot be accessed.<br>2. No document is removed and comments remain unchanged. The system displays MSG174 – “This Meeting is not accessible.” without disclosing whether the Meeting exists.<br>3. The use case ends. |

### UC-34 – View Personal Document Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to browse their own folders and documents and see permitted Meeting-usage details. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |
| **Postconditions** | ● POST-1: The user sees only their own library items and permitted Meeting-usage details.<br>● POST-2: Documents, folders and Meeting attachments remain unchanged. |
| **Normal Flow** | View Personal Document Library<br>1. The user opens Personal Document Library.<br>2. The system checks personal-library access. (BR-204, BR-242)<br>3. The system displays the user's folders and documents with names, file types and permitted Meeting-usage details. Other users' items and unauthorized Meeting details are excluded. (BR-219, BR-242, BR-251)<br>4. The user selects a folder or the library root.<br>5. The system displays the documents at that location. The user may browse the list or empty result, change the location or finish viewing. |
| **Alternative Flows** | AF-1 – Search or filter documents<br>1. At Step 5, the user enters a document-name search or selects a file-type filter.<br>2. The system applies the criteria within the user's own displayed library location.<br>3. The flow resumes at Step 5 of the Main Flow.<br>AF-2 – Empty library or no matching documents<br>1. At Step 3 or Step 5, no documents match the location and criteria.<br>2. The system displays an empty result without treating it as a loading failure.<br>3. The flow resumes at Step 5 of the Main Flow. |
| **Exception Flows** | EX-1 – Library cannot be loaded<br>1. At Step 2, Step 3 or Step 5, the system cannot retrieve the library.<br>2. No library or Meeting information changes. The system displays MSG176 – “Unable to load your personal document library. Please try again.”.<br>3. The use case ends.<br>EX-2 – Selected folder is unavailable<br>1. At Step 4, the folder was deleted or is no longer available in the user's library.<br>2. No information changes. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The user returns to the library root. The flow resumes at Step 4 of the Main Flow. |

### UC-35 – Comment on Meeting Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Chair, Secretary, Participant, Guest |
| **Secondary Actors** | None |
| **Description** | Allows anyone who can view a Meeting document to read and add comments. No separate commenting permission is required. PDF comments may include a page reference; DOCX comments have no page reference. |
| **Preconditions** | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a current valid invitation. (BR-153, BR-155, BR-157)<br>● PRE-2: The document is currently attached and the user has current read access. The same document-viewing access permits commenting; no separate commenting permission is required. (BR-198, BR-233, BR-234, BR-249) |
| **Postconditions** | ● POST-1: A comment is recorded with its author and posting time; a PDF comment may include a page reference and a DOCX comment has no page reference. (BR-248, BR-250)<br>● POST-2: The comment list and count are refreshed. Document content and Meeting status remain unchanged. |
| **Normal Flow** | Comment on Meeting Document<br>1. The user opens the Meeting document viewer and its comments.<br>2. The system checks current document access and displays existing comments with author and posting time. (BR-249, BR-250)<br>3. The user enters a comment and chooses Send.<br>4. The system rechecks current read access, that the document remains attached, that the comment is not blank. (BR-246, BR-249)<br>5. The system saves the comment, author and posting time, with an optional page reference for PDF and no page reference for DOCX. (BR-248, BR-250)<br>6. The system refreshes the comments and count and displays MSG199 – “Comment added successfully.”. |
| **Alternative Flows** | AF-1 – Include a PDF page reference<br>1. At Step 3, the user comments on a page being viewed in a PDF.<br>2. The comment includes that page reference; a page reference is not required for a document-wide comment.<br>3. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Comment is empty<br>1. At Step 4, the text is blank or contains only whitespace. (BR-246)<br>2. No comment is saved. The system displays MSG200 – “Enter a comment.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Document is no longer available<br>1. At Step 2 or Step 4, the document is no longer attached, or the user can no longer view it, including after Guest invitation revocation or expiry.<br>2. No comment is saved. The system displays MSG156 – “Meeting documents are not accessible.”.<br>3. The use case ends.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. No new comment is saved; existing comments remain unchanged. The system displays MSG202 – “Unable to add the comment. No comment was saved. Please try again.”.<br>3. The use case ends.<br>EX-4 – Comments cannot be loaded<br>1. At Step 2, current document access is valid but the system cannot retrieve comments.<br>2. No comment or document changes. The system displays MSG204 – “Unable to load the document comments. Please try again.”.<br>3. The use case ends. |

### UC-36 – Upload Document to Personal Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to upload one PDF or DOCX to the selected location in their personal library. Uploading does not attach it to a Meeting. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |
| **Postconditions** | ● POST-1: A new personal-library document is available at the selected location.<br>● POST-2: The uploaded file retains its content and format; existing documents and Meeting attachments remain unchanged. (BR-183, BR-216, BR-229) |
| **Normal Flow** | Upload Document to Personal Library<br>1. The user opens their library root or a folder and chooses Upload.<br>2. The user selects one file from the computer and submits it.<br>3. The system checks library access, the destination ownership and availability, and that the actual file is PDF or DOCX and no larger than 50 MB. (BR-187, BR-214, BR-215, BR-242)<br>4. The system adds a new document at the selected location with the file's name and original content. Separate uploads remain separate documents. (BR-183, BR-216, BR-229)<br>5. The system refreshes the library and displays MSG177 – “Document uploaded to your personal library successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – File is invalid<br>1. At Step 3, no file is selected, the actual content is not a valid PDF or DOCX, or the file exceeds 50 MB. (BR-187, BR-214, BR-215)<br>2. No library document is created. The system displays MSG164 – “Select a valid PDF or DOCX file no larger than 50 MB.”.<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Destination is unavailable<br>1. At Step 3, the selected folder was deleted or does not belong to the user. (BR-242)<br>2. No library document is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.<br>3. The user selects an available folder or the library root. The flow resumes at Step 1 of the Main Flow.<br>EX-3 – Operation cannot be completed<br>1. At Step 4, the system cannot complete the operation.<br>2. No new library document is created; existing documents and Meeting attachments remain unchanged. The system displays MSG178 – “Unable to upload the document. No document was added. Please try again.”.<br>3. The use case ends. |

### UC-71 – Manage Personal Documents

#### UC-71a – Delete Document from Personal Library

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to delete a document from their personal library without removing documents already attached to Meetings. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The selected document is in the user's library. |
| **Postconditions** | ● POST-1: The document no longer appears in the personal library.<br>● POST-2: Existing Meeting attachments remain readable; eventual cleanup applies only when no active use remains. Historical metadata and comments are retained. (BR-186, BR-213, BR-232) |
| **Normal Flow** | Delete Document from Personal Library<br>1. The user selects a personal-library document and chooses Delete.<br>2. The system requests confirmation, naming the document and explaining that deletion affects the personal library only.<br>3. The user confirms deletion.<br>4. The system rechecks ownership and that the library document still exists. (BR-242)<br>5. The system deletes the personal-library document without removing Meeting attachments. (BR-186)<br>6. The system refreshes the library and displays MSG180 – “Document deleted from your personal library successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No library document is deleted; Meeting documents remain unchanged. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The library document remains available and Meeting documents remain unchanged. The system displays MSG181 – “Unable to delete the document. Your personal library remains unchanged.”.<br>3. The use case ends. |

#### UC-71b – Rename Document

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows the owner to rename a personal-library document without changing its file content. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The document exists in the user's library. |
| **Postconditions** | ● POST-1: The document is displayed under the new name in the personal library.<br>● POST-2: File content and library location remain unchanged. (BR-183) |
| **Normal Flow** | Rename Document<br>1. The user chooses Rename for a personal-library document.<br>2. The system displays its current name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership and document availability. (BR-242)<br>5. The system saves the new personal-library document name. File content remains unchanged. (BR-183, BR-224)<br>6. The system refreshes the displayed name and displays MSG183 – “Document renamed successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No document name changes. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The previous name, content and Meeting attachments remain unchanged. The system displays MSG184 – “Unable to rename the document. Its name remains unchanged.”.<br>3. The use case ends. |

#### UC-71c – Move Document to Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows the owner to move a personal-library document to another owned folder or the library root. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The document exists in the user's library. |
| **Postconditions** | ● POST-1: The document is available at the selected library location.<br>● POST-2: Document name, file content and Meeting attachments remain unchanged. (BR-225, BR-245) |
| **Normal Flow** | Move Document to Folder<br>1. The user chooses Move for a personal-library document.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system rechecks document ownership and availability and destination ownership and availability. (BR-225, BR-242)<br>5. The system moves only the library document to that location. (BR-245)<br>6. The system refreshes the library and displays MSG185 – “Document moved successfully.”. |
| **Alternative Flows** | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of a folder.<br>2. The move has no destination folder. (BR-225)<br>3. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. The document remains at its previous location. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Destination is unavailable<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-225, BR-242)<br>2. The document stays at its previous location. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The document stays at its previous location and its Meeting attachments are unchanged. The system displays MSG186 – “Unable to move the document. Its location remains unchanged.”.<br>3. The use case ends. |

### UC-72 – Manage Library Folders

#### UC-72a – Create Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows an internal user to create a folder at the library root or inside an owned folder. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |
| **Postconditions** | ● POST-1: A new owned folder is available at the selected location.<br>● POST-2: Existing folders, documents and Meeting attachments remain unchanged. |
| **Normal Flow** | Create Folder<br>1. The user selects the library root or an owned parent folder and chooses New Folder.<br>2. The user enters a folder name and submits it.<br>3. The system checks ownership, parent availability, a nonblank name and a resulting depth of at most five levels. Duplicate folder names are permitted. (BR-191, BR-219, BR-220, BR-221, BR-242)<br>4. The system creates the folder at that location.<br>5. The system refreshes the folder list and displays MSG187 – “Folder created successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder name is blank<br>1. At Step 3, the name is empty or contains only whitespace. (BR-220)<br>2. No folder is created. The system displays MSG188 – “Enter a folder name.”.<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Parent is unavailable<br>1. At Step 3, the selected parent was deleted or does not belong to the user. (BR-219, BR-242)<br>2. No folder is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-3 – Folder depth exceeds the limit<br>1. At Step 3, the new folder would be deeper than level five. (BR-191)<br>2. No folder is created. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”.<br>3. The user selects a shallower parent. The flow resumes at Step 1 of the Main Flow.<br>EX-4 – Operation cannot be completed<br>1. At Step 4, the system cannot complete the operation.<br>2. No folder is created; existing library items remain unchanged. The system displays MSG190 – “Unable to create the folder. No folder was created.”.<br>3. The use case ends. |

#### UC-72b – Rename Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows the owner to rename a folder without moving it or changing its contents. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists in the user's library. |
| **Postconditions** | ● POST-1: The folder displays the new name.<br>● POST-2: Its location and contents remain unchanged; an automatic Meeting folder remains reusable. (BR-227) |
| **Normal Flow** | Rename Folder<br>1. The user chooses Rename for an owned folder.<br>2. The system displays the current folder name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership, availability and a nonblank name; duplicate names are permitted. (BR-219, BR-220, BR-221, BR-242)<br>5. The system saves the new folder name without changing its contents or location.<br>6. The system refreshes the folder name and displays MSG191 – “Folder renamed successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder name changes. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Folder name is blank<br>1. At Step 4, the name is empty or contains only whitespace. (BR-220)<br>2. The previous name remains. The system displays MSG188 – “Enter a folder name.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder name, location and contents remain unchanged. The system displays MSG192 – “Unable to rename the folder. Its name remains unchanged.”.<br>3. The use case ends. |

#### UC-72c – Move Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows the owner to move a folder with its contents to another owned folder or the library root. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists in the user's library. |
| **Postconditions** | ● POST-1: The folder and its contents are available at the selected location, within five levels.<br>● POST-2: Folder and document names, file content and Meeting attachments remain unchanged. An automatic Meeting folder remains reusable. (BR-191, BR-227) |
| **Normal Flow** | Move Folder<br>1. The user chooses Move for an owned folder.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system rechecks source and destination ownership and availability. The destination cannot be the source or its descendant, and the moved folder and every descendant must remain within five levels. (BR-191, BR-219, BR-222, BR-242)<br>5. The system moves the folder with its documents and subfolders, without changing their names or Meeting attachments.<br>6. The system refreshes the folder tree and displays MSG194 – “Folder moved successfully.”. |
| **Alternative Flows** | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of another folder.<br>2. The moved folder becomes a first-level folder; its descendants remain subject to the depth limit. (BR-191)<br>3. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder or contained item moves. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Destination is unavailable<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-219, BR-242)<br>2. No folder or contained item moves. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Destination creates a cycle<br>1. At Step 4, the destination is the folder itself or one of its descendants. (BR-222)<br>2. No folder or contained item moves. The system displays MSG193 – “A folder cannot be moved into itself or one of its descendants.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-4 – Resulting depth exceeds the limit<br>1. At Step 4, the moved folder or any descendant would be deeper than level five. (BR-191)<br>2. No folder or contained item moves. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”.<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder and all contained items remain at their previous locations. The system displays MSG195 – “Unable to move the folder. Its location remains unchanged.”.<br>3. The use case ends. |

#### UC-72d – Delete Folder

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Internal user |
| **Secondary Actors** | None |
| **Description** | Allows the owner to delete an empty personal-library folder. If an automatic Meeting folder is deleted, a later direct Meeting upload recreates it. |
| **Preconditions** | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists and contains no documents or subfolders. (BR-223) |
| **Postconditions** | ● POST-1: The empty folder no longer appears in the library.<br>● POST-2: Other folders, documents and Meeting attachments remain unchanged. Future recreation of an automatic Meeting folder follows its naming and location rules. (BR-192, BR-228) |
| **Normal Flow** | Delete Folder<br>1. The user chooses Delete for an owned folder.<br>2. The system asks for confirmation and names the folder.<br>3. The user confirms deletion.<br>4. The system rechecks current ownership and availability and that the folder contains neither documents nor subfolders. (BR-219, BR-223, BR-242)<br>5. The system deletes only the empty folder.<br>6. The system refreshes the folder tree and displays MSG197 – “Folder deleted successfully.”. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder is deleted. The system displays MSG175 – “This personal-library item is not available.”.<br>3. The use case ends.<br>EX-2 – Folder is no longer empty<br>1. At Step 4, the folder contains a document or subfolder, including an item added after confirmation was displayed. (BR-223)<br>2. The folder and its contents remain unchanged. The system displays MSG196 – “Only an empty folder can be deleted.”.<br>3. The use case ends.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder and all other library items remain unchanged. The system displays MSG198 – “Unable to delete the folder. The folder remains unchanged.”.<br>3. The use case ends. |

## FE-08 — Online Meeting Room Management

### UC-37 – Join Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

### UC-38 – Leave Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

### UC-39 – Manage Meeting Room Controls

_UC gom — đặc tả nằm ở các UC con UC-39a, UC-39b… bên dưới._

#### UC-39a – Manage Microphone

Placeholder only – detailed Use Case Specification will be added later.

#### UC-39b – Manage Camera

Placeholder only – detailed Use Case Specification will be added later.

#### UC-39c – Manage Screen Sharing

Placeholder only – detailed Use Case Specification will be added later.

### UC-40 – View Meeting Room Participants

Placeholder only – detailed Use Case Specification will be added later.

### UC-41 – Moderate Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

### UC-42 – Use In-room Chat

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-09 — Meeting Audio Upload & Media Library

### UC-43 – View & Play Meeting Audio

_UC gom — đặc tả nằm ở các UC con UC-43a, UC-43b… bên dưới._

#### UC-43a – View Media Library

Placeholder only – detailed Use Case Specification will be added later.

#### UC-43b – Play Meeting Audio

Placeholder only – detailed Use Case Specification will be added later.

### UC-44 – Upload Meeting Audio

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-10 — Speaker-Attributed Transcription & Meeting Summary

### UC-45 – View Speaker-Attributed Transcript

Placeholder only – detailed Use Case Specification will be added later.

### UC-46 – Edit Transcript & Speaker Attribution

Placeholder only – detailed Use Case Specification will be added later.

### UC-47 – Generate / Regenerate Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

### UC-48 – View Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

### UC-49 – Edit Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

### UC-50 – Approve Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

### UC-51 – Publish Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-11 — Voting Management
Scope: create/open/cast only while Meeting is InProgress. End Meeting is independent of Vote closure. Chair can close and tally existing Open Votes in InProgress or Ended, then record the conclusion. No post-meeting ballot casting, time-boxed voting or quorum. (BR-159, BR-160, BR-165)

### UC-52 – View Meeting Votes

_UC gom — đặc tả nằm ở các UC con UC-52a, UC-52b… bên dưới._

#### UC-52a – View Meeting Votes

Placeholder only – detailed Use Case Specification will be added later.

#### UC-52b – View Vote Status

Placeholder only – detailed Use Case Specification will be added later.

### UC-53 – Create / Configure Vote

Placeholder only – detailed Use Case Specification will be added later.

### UC-54 – Open Vote

Placeholder only – detailed Use Case Specification will be added later.

### UC-55 – Cast Vote

Placeholder only – detailed Use Case Specification will be added later.

### UC-56 – Close Vote

Placeholder only – detailed Use Case Specification will be added later.

### UC-57 – Record Vote Conclusion

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-12 — Meeting Minutes & Digital Signing

### UC-58 – View Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### UC-59 – Generate Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### UC-60 – Edit Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### UC-61 – Submit Meeting Minutes for Signing

Placeholder only – detailed Use Case Specification will be added later.

### UC-62 – Sign Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### UC-63 – View Signature Status & Audit Trail

Placeholder only – detailed Use Case Specification will be added later.

### UC-64 – Finalize Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### UC-65 – Request Revision of Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-13 — Email & In-App Notification

### UC-66 – View Notifications

Sequence Diagram: [UC66_ViewNotifications.txt](../06-sequencediagram/notification-management/UC66_ViewNotifications.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authenticated user to view their list of in-app notifications and check the unread notification count. |
| **Preconditions** | ● PRE-1: The user is authenticated in the system. |
| **Postconditions** | ● POST-1: The system displays the user's notifications and unread badge count.<br>● POST-2: Opening the notification list does not change any notification’s read status. |
| **Normal Flow** | View Notifications<br>1. The user selects the notification bell from the navigation header.<br>2. The system retrieves the notifications and unread badge count for the user's account.<br>3. The system displays the user's notifications, indicates which ones are unread, and shows the unread count. (BR-48, BR-49) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1: Notifications Cannot Be Retrieved<br>1. At Step 2 of the Main Flow, the system cannot load notification records.<br>2. The system displays MSG40 – "Unable to load notifications. Please try again."<br>3. The use case ends unsuccessfully.<br>EX-2: Requested Notification Page Is Unavailable<br>1. At Step 2 of the Main Flow, the system cannot display the requested portion of the notification list.<br>2. The system displays MSG39 – "The requested notification page is unavailable."<br>3. The use case ends unsuccessfully. |

### UC-67 – Mark Notifications as Read

_UC gom — đặc tả nằm ở các UC con UC-67a, UC-67b… bên dưới._

#### UC-67a – Mark Notification as Read

Sequence Diagram: [UC67a_MarkNotificationAsRead.txt](../06-sequencediagram/notification-management/UC67a_MarkNotificationAsRead.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authenticated user to mark a specific unread notification as read, updating its status. |
| **Preconditions** | ● PRE-1: The user is authenticated in the system.<br>● PRE-2: The user has at least one unread notification. |
| **Postconditions** | ● POST-1: The selected notification status is changed to Read.<br>● POST-2: The unread count reflects the latest saved notification statuses. |
| **Normal Flow** | Mark Notification as Read<br>1. The user views their notification list.<br>2. The user selects "Mark as Read" for a notification.<br>3. The system verifies that the notification belongs to the user's account. (BR-47)<br>4. The system marks the notification as read.<br>5. The system displays the notification as read and shows the updated unread count. |
| **Alternative Flows** | N/A |
| **Exception Flows** | N/A |

#### UC-67b – Mark All Notifications as Read

Sequence Diagram: [UC67b_MarkAllNotificationsAsRead.txt](../06-sequencediagram/notification-management/UC67b_MarkAllNotificationsAsRead.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a user to mark all of their unread notifications as read. |
| **Preconditions** | ● PRE-1: The user is signed in. |
| **Postconditions** | ● POST-1: All notifications belonging to the user's account that were unread when the request was processed are marked as read.<br>● POST-2: The displayed unread count reflects the latest saved state. |
| **Normal Flow** | Mark All Notifications as Read<br>1. The user selects the notification bell and opens the notification list.<br>2. The user selects "Mark All as Read".<br>3. The system marks all unread notifications belonging to the user's account as read, including those not currently displayed. (BR-47, BR-51)<br>4. The system refreshes the displayed read statuses and unread count. (BR-48) |
| **Alternative Flows** | AF-1 – No Unread Notifications<br>Entry Point: Step 3 of the Main Flow.<br>1. The system finds no unread notifications belonging to the user's account.<br>2. The system displays the current list and an unread count of zero.<br>3. The use case ends successfully. |
| **Exception Flows** | N/A |

### UC-68 – Open Notification Target

Sequence Diagram: [UC68_OpenNotificationTarget.txt](../06-sequencediagram/notification-management/UC68_OpenNotificationTarget.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows a user to open the meeting linked to one of their notifications. |
| **Preconditions** | ● PRE-1: The user is signed in. |
| **Postconditions** | ● POST-1: The user sees the linked meeting.<br>● POST-2: The selected notification is marked as read if successfully saved. |
| **Normal Flow** | Open Notification Target<br>1. The user selects the notification bell and clicks on a specific notification.<br>2. The system checks that the notification belongs to the user's account and identifies its linked meeting. (BR-47, BR-52)<br>3. The system marks the notification as read if needed and updates the unread count. (BR-48, BR-50, BR-53)<br>4. The system checks whether the user currently has permission to view the linked meeting. (BR-29, BR-52)<br>5. The system opens the meeting. |
| **Alternative Flows** | AF-1 – Notification Is Already Read<br>Entry Point: Step 3 of the Main Flow.<br>1. The notification is already read, so its status and the unread count remain unchanged. (BR-50)<br>2. The flow resumes at Step 4 of the Main Flow. |
| **Exception Flows** | N/A |

---

## FE-14 — Personal Dashboard & Meeting Workspace

### UC-69 – View Personal Dashboard & Meeting Workspace

Placeholder only – detailed Use Case Specification will be added later.

---

## FE-15 — Reporting & Statistics

### UC-70 – View & Export Reports

_UC gom — đặc tả nằm ở các UC con UC-70a, UC-70b… bên dưới._

#### UC-70a – View Reports & Statistics

Placeholder only – detailed Use Case Specification will be added later.

#### UC-70b – Export Report

Placeholder only – detailed Use Case Specification will be added later.
