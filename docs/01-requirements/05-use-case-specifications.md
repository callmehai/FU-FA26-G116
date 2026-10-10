# Use Case Specifications · TrustCabinet

Đặc tả Use Case của **TrustCabinet**, đồng bộ nguyên văn mục *II. Use Case Specifications* của [Report 3 — SRS](../08-reports/Report-3_Software_Requirement_Specification.md) (bản DOCX ngày 10/10/2026). Nội dung đặc tả giữ tiếng Anh như Report 3.

- **Phạm vi:** 15 Feature (FE-01 → FE-15), 70 Use Case (UC-01 → UC-70). UC gom nhiều thao tác nhỏ được tách thành UC con có hậu tố chữ (ví dụ `UC-06a`, `UC-06b`, `UC-06c`).
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
  - [UC-32b — View Document Version History](#uc-32b--view-document-version-history)
  - [UC-32c — Preview Meeting Document](#uc-32c--preview-meeting-document)
- [UC-33 — Manage Meeting Documents](#uc-33--manage-meeting-documents)
  - [UC-33a — Add Document to Meeting](#uc-33a--add-document-to-meeting)
  - [UC-33b — Add New Meeting Document Version](#uc-33b--add-new-meeting-document-version)
- [UC-34 — View Personal Document Library](#uc-34--view-personal-document-library)
- [UC-35 — Comment on Meeting Document](#uc-35--comment-on-meeting-document)
- [UC-36 — Upload Document to Personal Library](#uc-36--upload-document-to-personal-library)

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

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Guest |
| **Secondary Actors** | Email / SMTP Service |
| **Description** | As an invited Guest, I want to verify my meeting invitation so that I can access only the meeting for which I was invited. |
| **Preconditions** | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked or expired. |
| **Postconditions** | ● POST-1: A meeting-scoped Guest session is established after successful verification.<br>● POST-2: The Guest can continue to permitted invitation, document, and room actions for that meeting. |
| **Normal Flow** | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system identifies the invitation and requests the one-time verification code sent to the invited email address.<br>3. The Guest enters the code.<br>4. The system validates the code, invitation status, and meeting scope.<br>5. The system establishes a Guest session and displays the invited meeting. |
| **Alternative Flows** | ALT-1: If the Guest reopens a valid link during an active verified session, the system opens the same invited meeting without requesting another code. |
| **Exception Flows** | EX-1: The invitation is missing, expired, or revoked: the system denies access and explains that the invitation is no longer valid.<br>EX-2: The code is incorrect, expired, or already used: the system denies verification and allows the Guest to request or enter a valid code.<br>EX-3: The verification service is unavailable: the system reports that verification could not be completed and does not establish a session. |

---

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
| **Description** | Allows an authorized Meeting Organizer to create one independent Meeting as a Draft, including its planned schedule, mode, location, confidentiality and optional Online Link. |
| **Preconditions** | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The Meeting Organizer is authorized to create meetings. |
| **Postconditions** | ● POST-1: A new Meeting exists in Draft status with its planned start and end times.<br>● POST-2: The creator is recorded as Meeting Organizer.<br>● POST-3: The Meeting may subsequently be assigned participants, prepared and submitted for approval. |
| **Normal Flow** | Create Meeting<br>1. The Meeting Organizer selects Create Meeting.<br>2. The system displays the new Meeting form.<br>3. The Organizer enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level, optional Description and optional Online Link (maximum 1,000 characters).<br>4. The Organizer submits the form.<br>5. The system checks the required values, confirms Planned Start Time is later than the current time and Planned End Time is later than Planned Start Time, and validates the optional Online Link length. (BR-24, BR-25, BR-62, BR-162, BR-163)<br>6. The system creates exactly one Meeting in Draft status and records the creator. (BR-26)<br>7. The system displays MSG29 – "Meeting created successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Meeting Information<br>1. At Step 5, a required value is missing/invalid, the planned start is not in the future, the planned end does not follow the start, or Online Link exceeds 1,000 characters. (BR-24, BR-62, BR-162, BR-163)<br>2. The system displays MSG31 – "Invalid meeting information." and highlights the affected fields.<br>3. No Meeting is created. The Organizer may correct the inputs and resubmit at Step 4.<br>EX-2 – Meeting Cannot Be Created<br>1. At Step 6, the system cannot save the Meeting.<br>2. The system displays MSG35 – "Unable to create the meeting. Please try again."<br>3. No Meeting is created; the use case ends unsuccessfully. |

### UC-15 – Update Meeting

Sequence Diagram: [UC15_UpdateMeeting.txt](../06-sequencediagram/meeting-management/UC15_UpdateMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Organizer to update Draft/Rejected Meeting information, or to reschedule only the planned times, location or optional Online Link when the Meeting is Published. |
| **Preconditions** | ● PRE-1: The Meeting exists and the Meeting Organizer is authenticated and authorized.<br>● PRE-2: Full Meeting editing is available only in Draft or Rejected. (BR-27)<br>● PRE-3: If the Meeting is Published, only the Meeting Organizer may invoke Reschedule to change planned times, location or optional Online Link. The Meeting must not have started. (BR-27, BR-158) |
| **Postconditions** | ● POST-1: Authorized updates are saved to the Meeting.<br>● POST-2: Meeting lifecycle and assigned approver are unchanged.<br>● POST-3: When the schedule of a Published Meeting changes, affected participants are notified and the change is recorded. (BR-76) |
| **Normal Flow** | Update Meeting<br>1. The Meeting Organizer opens a Draft or Rejected Meeting and selects Edit.<br>2. The system displays the editable Meeting fields, including the optional Online Link.<br>3. The Organizer changes the fields and submits.<br>4. The system verifies current Meeting status, permissions, required information, Planned End Time after Planned Start Time, and Online Link length at most 1,000 characters. (BR-24, BR-27, BR-62, BR-163)<br>5. The system saves the updates without changing Meeting status and shows the success message. |
| **Alternative Flows** | AF-1 – Reschedule Published Meeting<br>Entry point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects Reschedule for a Meeting that is Published and has not started.<br>2. The system allows changes only to Planned Start Time, Planned End Time, Location and optional Online Link. All other fields are read-only. (BR-27, BR-158)<br>3. The Organizer enters the changes and submits.<br>4. The system validates that the new start is in the future, the end follows the start, and Online Link is at most 1,000 characters. (BR-62, BR-75, BR-163)<br>5. The system saves the new schedule, retains Published status, records the change and queues notifications to affected invitees. Delivery failure does not roll back the saved change. (BR-38, BR-76, BR-158)<br>6. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified." The use case succeeds. |
| **Exception Flows** | EX-1 – Invalid or Unauthorized Update<br>1. The system determines that meeting information is invalid, Online Link exceeds 1,000 characters, the Organizer is no longer authorized, or the Meeting status does not permit this edit. (BR-27, BR-62, BR-163)<br>2. The system does not save any changes and displays the applicable existing validation/error message.<br>3. The use case ends unsuccessfully or the Organizer corrects input and retries.<br>EX-2 – Reschedule Not Permitted<br>1. During AF-1 the Meeting is not Published, has already started, its planned start is not in the future, or a field outside the allowed Reschedule set was modified. (BR-27, BR-75, BR-158)<br>2. The system rejects the update; the existing schedule remains unchanged.<br>3. The system displays the relevant existing meeting-update error message. The use case ends.<br>EX-3 – Update Cannot Be Saved<br>1. At Main Step 5 or AF-1 Step 5 a persistence error occurs.<br>2. Existing Meeting data/status remain unchanged and no schedule-update notification is sent.<br>3. The system displays the existing update-failure message. The use case ends. |

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
| **Normal Flow** | Submit Meeting for Approval<br>1. The Meeting Organizer selects a Draft meeting and chooses Submit for Approval.<br>2. The system verifies that all required Meeting information and planned schedule are complete and valid for approval submission. (BR-25, BR-33, BR-62)<br>3. The system identifies users who are eligible to act as Meeting Approvers.<br>4. The system displays the eligible Meeting Approvers.<br>5. The Meeting Organizer selects a Meeting Approver.<br>6. The system verifies that the selected user is eligible to review the meeting and is not the Meeting Organizer.<br>7. The system verifies that the meeting is still in Draft or Rejected status.<br>8. The system assigns the selected Meeting Approver to the current approval cycle.<br>9. The system changes the meeting status to WaitingApproval.<br>10. The system records the submission and assigned approver in the meeting lifecycle history.<br>11. The system notifies the assigned Meeting Approver.<br>12. The system displays the submission success message. |
| **Alternative Flows** | AF-1 – Resubmit a Rejected Meeting<br>Entry Point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects a meeting in Rejected status.<br>2. The Meeting Organizer chooses Resubmit for Approval.<br>3. The flow resumes at Step 2 of the Main Flow and completes successfully when the submission requirements are satisfied. |
| **Exception Flows** | EX-1 – Incomplete Meeting-Level Information<br>1. At Step 2, the system determines that mandatory Meeting information and planned schedule is incomplete.<br>2. The system identifies the missing or invalid information.<br>3. The meeting remains Draft or Rejected and the use case ends.<br>EX-2 – Meeting Status Not Eligible for Submission<br>1. At Step 7, the system determines that the meeting is not in Draft or Rejected status.<br>2. The system displays MSG41 – "This meeting can no longer be submitted for approval."<br>3. The use case ends.<br>EX-3 – No Valid Meeting Approver<br>1. At Step 6, the system determines that the selected user is not eligible to review the meeting or is the Meeting Organizer.<br>2. The meeting remains Draft or Rejected and no approval assignment is created.<br>3. The flow resumes at Step 5 of the Main Flow so the Meeting Organizer selects another eligible approver.<br>EX-4 – Submission Cannot Be Completed<br>1. At Step 8 or Step 9, the approval assignment or status change cannot be completed.<br>2. The meeting remains in its previous status and no approval notification is sent.<br>3. The system displays MSG42 – "Unable to submit the meeting for approval. Please try again."<br>4. The use case ends. |

### UC-19 – Review Meeting Approval

Sequence Diagram: [UC19_ReviewMeetingApproval.txt](../06-sequencediagram/meeting-management/UC19_ReviewMeetingApproval.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Approver |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it. |
| **Preconditions** | ● PRE-1: The Meeting Approver is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Approver is currently assigned to review the selected meeting and is not its Meeting Organizer.<br>● PRE-4: The meeting is in WaitingApproval status. |
| **Postconditions** | ● POST-1: The Meeting Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history. |
| **Normal Flow** | Review Meeting Approval<br>1. The Meeting Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Meeting Approver.<br>3. The system displays the meeting information for review.<br>4. The Meeting Approver selects Approve.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to approve the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status from WaitingApproval to Approved.<br>7. The system records the approval decision, acting user, and timestamp in the meeting lifecycle history.<br>8. The system displays MSG43 – "Meeting approved successfully." |
| **Alternative Flows** | AF-1 – Reject Meeting<br>1. At Step 4 of the Main Flow, the Meeting Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Meeting Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to reject the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status to Rejected.<br>7. The system records the rejection decision, rejection reason, acting user, and timestamp.<br>8. The rejection reason becomes available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>9. The system displays MSG44 – "Meeting rejected successfully." |
| **Exception Flows** | EX-1 – Rejection Reason Is Missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-36)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting Is No Longer Awaiting Approval<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status.<br>2. No approval or rejection decision is applied.<br>3. The meeting remains in its current status.<br>4. The system displays "This meeting is no longer awaiting approval."<br>5. The use case ends.<br>EX-3 – Approval Decision Cannot Be Completed<br>1. At Step 6 of the Main Flow or Step 6 of AF-1, the system cannot complete the requested approval or rejection decision.<br>2. The requested status transition is not applied.<br>3. No successful approval or rejection decision is recorded in the meeting lifecycle history.<br>4. The system displays "Unable to complete the approval decision. Please try again."<br>5. The use case ends.<br>EX-4 – User Is No Longer Authorized to Review the Meeting<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the Meeting Approver is no longer authorized to approve or reject the selected meeting. (BR-35)<br>2. No approval or rejection decision is applied.<br>3. The meeting status remains unchanged.<br>4. The system informs the Meeting Approver that they are no longer authorized to review the meeting.<br>5. The use case ends.<br>EX-5 – Rejection Reason Is Too Long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |

### UC-20 – Publish Meeting

Sequence Diagram: [UC20_PublishMeeting.txt](../06-sequencediagram/meeting-management/UC20_PublishMeeting.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | None |
| **Description** | Allows the Meeting Organizer to publish an Approved meeting and initiate invitation delivery to current internal participants and external Guests. |
| **Preconditions** | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-124, BR-125, BR-129) |
| **Postconditions** | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history.<br>● POST-3: The system attempts to initiate invitation delivery for current internal participants and active external Guest invitations when delivery is available. Internal participants may receive in-app and email delivery; external Guests receive email guest-access invitations. (BR-39, BR-40, BR-90, BR-91, BR-152) |
| **Normal Flow** | Publish Meeting<br>1. The Meeting Organizer selects an Approved meeting.<br>2. The Meeting Organizer selects Publish Meeting.<br>3. The system verifies that the Meeting Organizer remains authorized, the meeting is still Approved with a valid planned schedule, exactly one current Chair has an active account, and at least one current Secretary has an active account.  (BR-37, BR-124, BR-125, BR-129)<br>4. The system changes the meeting status from Approved to Published. (BR-37)<br>5. The system records the publication action, acting user, and timestamp in the meeting lifecycle history. (BR-38)<br>6. The system initiates asynchronous invitation delivery for current internal participants and active external Guest invitations. External Guest emails contain a secure meeting-specific guest invitation used for later email verification and guest access. The meeting remains Published if delivery is unavailable. (BR-39, BR-40, BR-90, BR-91, BR-94, BR-152, BR-153, BR-156)<br>7. The system displays MSG45 – "Meeting published successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Is No Longer Eligible for Publication<br>1. At Step 3 of the Main Flow, the system determines that the meeting is no longer Approved, the Meeting Organizer is no longer authorized to publish it, there is not exactly one active current Chair, or there is no active current Secretary. (BR-37, BR-124, BR-125)<br>2. The meeting remains unchanged.<br>3. Invitation delivery is not initiated.<br>4. The system displays MSG46 – "This meeting can no longer be published."<br>5. The use case ends.<br>EX-2 – Meeting Cannot Be Published<br>1. At Step 4 of the Main Flow, the system cannot complete the publication.<br>2. The meeting remains Approved.<br>3. Invitation delivery is not initiated.<br>4. The system displays MSG47 – "Unable to publish the meeting. Please try again."<br>5. The use case ends. |

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
| **Description** | Allows the assigned Chair to end an InProgress Meeting, record Actual End Time and close access to its Online Meeting Room. |
| **Preconditions** | ● PRE-1: The Chair is authenticated, active and assigned to the selected Meeting.<br>● PRE-2: The Meeting is InProgress. (BR-132)<br>Note: An Open Vote is checked at runtime in Step 3 and handled by EX-3; it is not treated as a precondition. |
| **Postconditions** | ● POST-1: Meeting status becomes Ended.<br>● POST-2: Actual End Time, acting Chair and event timestamp are recorded.<br>● POST-3: Any InProgress Agenda Item becomes Done with its actual end time and duration. This update and the Meeting end are saved atomically; failure preserves both previous states. (BR-159)<br>● POST-4: The Online Meeting Room stops accepting joins and connected users are disconnected. (BR-143) |
| **Normal Flow** | End Meeting<br>1. The assigned Chair selects End Meeting.<br>2. The system checks current Chair assignment and Meeting InProgress status. (BR-130, BR-132)<br>3. The system checks that no Vote is Open; otherwise execute EX-3. (BR-159)<br>4. The system prepares completion of any InProgress Agenda Item, including actual end time and duration. (BR-159)<br>5. The system atomically saves the Agenda Item completion, Meeting Actual End Time, Ended status and immutable lifecycle event. If any save fails, all changes are rolled back and EX-2 applies. (BR-38, BR-132, BR-159)<br>6. The Online Meeting Room stops accepting entries and connected users are disconnected. (BR-143)<br>7. The system displays MSG111 – "Meeting ended successfully." |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Meeting Cannot Be Ended<br>1. The Meeting is not InProgress, or the Chair is no longer assigned/active. (BR-130, BR-132)<br>2. The Meeting remains unchanged and the system displays MSG114 – "This meeting cannot be ended." The use case ends.<br>EX-2 – End Cannot Be Completed<br>1. At Step 5, the system cannot save the Meeting end, Agenda Item completion or lifecycle event atomically.<br>2. All changes are rolled back: the Meeting remains InProgress and the Agenda Item retains its previous status and timestamps. No successful end event is recorded and room closure is not triggered.<br>3. The system displays MSG116 – "Unable to end the meeting. Please try again." The use case ends.<br>EX-3 – Open Vote Exists<br>1. At Step 3, a Vote is Open.<br>2. The system displays MSG139 – Close all open votes before ending the meeting.<br>3. Meeting remains InProgress; the use case ends unsuccessfully. (BR-159) |

---

## FE-05 — Agenda Management

### UC-23 – Manage Agenda Items

_UC gom — đặc tả nằm ở các UC con UC-23a, UC-23b… bên dưới._

#### UC-23a – Create Agenda Item

Sequence Diagram: [UC23a_CreateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23a_CreateAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to create a new Agenda Item directly under a Meeting while agenda planning is permitted while Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-65, BR-74) |
| **Postconditions** | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting and any selected documents belong to the same Meeting. |
| **Normal Flow** | Create Agenda Item<br>1. The Secretary opens the selected Meeting’s Agenda view and selects Add Agenda Item.<br>2. The Secretary enters Title, Planned Duration, optional Presenter and Description, and selects available Meeting documents.<br>3. The system checks the Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and document access. (BR-64, BR-65, BR-66, BR-74)<br>4. The system creates the Pending Agenda Item at the end of the Meeting agenda. (BR-67)<br>5. The system shows the updated Meeting agenda. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Agenda Item Information<br>1. Required information or selected documents are invalid. The system identifies the issue and no Agenda Item is created.<br>EX-2 – Meeting Not Open for Agenda Planning<br>1. The Meeting is not Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the user is no longer its Secretary. (BR-65, BR-68)<br>2. No Agenda Item is created. The use case ends.<br>EX-3 – Create Cannot Be Saved<br>1. The system cannot persist the requested Agenda change.<br>2. The last saved Agenda data and order remain unchanged.<br>3. The system displays MSG68 and the use case ends without satisfying its change postcondition. |

#### UC-23b – Update Agenda Item

Sequence Diagram: [UC23b_UpdateAgendaItem.txt](../06-sequencediagram/agenda-management/UC23b_UpdateAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to update an existing Agenda Item belonging directly to a Meeting while that Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Agenda Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68) |
| **Postconditions** | ● POST-1: The Agenda Item is updated without changing the Meeting lifecycle.<br>● POST-2: The Agenda Item remains associated with the same Meeting. |
| **Normal Flow** | Update Agenda Item<br>1. The Secretary selects an Agenda Item in the Meeting Agenda and chooses Edit.<br>2. The Secretary changes descriptive/planning fields and submits the update; this operation does not set or complete the current Item or change its execution status and actual timestamps.<br>3. The system verifies assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and any attached Meeting documents. (BR-65, BR-66, BR-68)<br>4. The system saves the updated Agenda Item and displays it within the Meeting agenda. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Invalid Agenda Item<br>1. The edited fields are invalid; the Agenda Item remains unchanged.<br>EX-2 – Editing No Longer Permitted<br>1. The Meeting is not Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the actor is not its assigned Secretary. (BR-65, BR-68)<br>2. The Agenda Item remains unchanged. The use case ends.<br>EX-3 – Update Cannot Be Saved<br>1. The system cannot persist the requested Agenda change.<br>2. The last saved Agenda data and order remain unchanged.<br>3. The system displays MSG69 and the use case ends without satisfying its change postcondition. |

#### UC-23c – Delete Agenda Item

Sequence Diagram: [UC23c_DeleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC23c_DeleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to remove a planned Agenda Item from a Meeting while that Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. |
| **Preconditions** | ● PRE-1: The user is the assigned Secretary.<br>● PRE-2: The Agenda Item belongs to a Meeting that is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status. (BR-65, BR-68) |
| **Postconditions** | ● POST-1: The Agenda Item is removed from the selected Meeting agenda.<br>● POST-2: The Meeting lifecycle is unchanged. |
| **Normal Flow** | Delete Agenda Item<br>1. The Secretary selects the Agenda Item within the Meeting and confirms Delete.<br>2. The system verifies ownership and that the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>3. The system removes the Agenda Item from the Meeting agenda and displays the updated list. |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Item Cannot Be Deleted<br>1. The Meeting is not Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the user lacks permission.<br>2. The Agenda Item remains unchanged. The use case ends.<br>EX-3 – Deletion Cannot Be Saved<br>1. The system cannot persist the requested Agenda change.<br>2. The last saved Agenda data and order remain unchanged.<br>3. The system displays MSG70 and the use case ends without satisfying its change postcondition. |

#### UC-23d – Reorder Agenda Items

Sequence Diagram: [UC23d_ReorderAgendaItems.txt](../06-sequencediagram/agenda-management/UC23d_ReorderAgendaItems.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to reorder Agenda Items within the same Meeting while it is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. |
| **Preconditions** | ● PRE-1: The user is the assigned Secretary.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains Agenda Items eligible for reordering. (BR-65, BR-68) |
| **Postconditions** | ● POST-1: Agenda Item order is updated within the selected Meeting.<br>● POST-2: Agenda Items remain associated with the same Meeting. |
| **Normal Flow** | Reorder Agenda Items<br>1. The Secretary changes the Agenda Item order in the Meeting Agenda.<br>2. The system validates that the actor is still its assigned Secretary, the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and that all Items belong to that Meeting. (BR-64, BR-65, BR-68)<br>3. The system saves and displays the new order. (BR-67) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Reordering No Longer Permitted<br>1. The Meeting is not Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the Secretary is no longer assigned.<br>2. Original Agenda order remains unchanged. The use case ends.<br>EX-3 – Order Cannot Be Saved<br>1. The system cannot persist the requested Agenda change.<br>2. The last saved Agenda data and order remain unchanged.<br>3. The system displays MSG71 and the use case ends without satisfying its change postcondition. |

### UC-24 – Set Current Agenda Item

Sequence Diagram: [UC24_SetCurrentAgendaItem.txt](../06-sequencediagram/agenda-management/UC24_SetCurrentAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Chair to set the current Agenda Item during an InProgress Meeting. |
| **Preconditions** | ● PRE-1: The Chair is assigned and active for the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is Pending.<br>● PRE-3: No other Agenda Item of the same Meeting is InProgress. (BR-69, BR-71) |
| **Postconditions** | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |
| **Normal Flow** | Set Current Agenda Item<br>1. The Chair selects a Pending Agenda Item in the active Meeting and chooses Set as Current.<br>2. The system verifies Meeting status, Chair assignment, Agenda Item membership and that no other Item is InProgress. (BR-64, BR-69, BR-71)<br>3. The system marks the Item InProgress and records its actual start time. (BR-70, BR-72)<br>4. The system shows the updated current Agenda Item to authorized Meeting users. (BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Item Cannot Become Current<br>1. The Meeting is not InProgress, the actor lacks authorization, or another Item is currently InProgress.<br>2. The Item remains unchanged. The use case ends.<br>EX-2 – Current Agenda Item Cannot Be Saved<br>Entry Point: Step 3 of the Main Flow.<br>1. The system cannot save the Item status or actual start time.<br>2. The Item remains Pending with unchanged timestamps; the current Item reference and all other Items remain unchanged.<br>3. The system displays MSG75 – "Unable to set the current agenda item. Please try again."<br>4. The use case ends without satisfying its postconditions. |

### UC-25 – Complete Agenda Item

Sequence Diagram: [UC25_CompleteAgendaItem.txt](../06-sequencediagram/agenda-management/UC25_CompleteAgendaItem.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Chair to complete the current Agenda Item of an InProgress Meeting, recording the actual time used. |
| **Preconditions** | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |
| **Postconditions** | ● POST-1: The Agenda Item becomes Done with actual end time and duration.<br>● POST-2: Completing the Item does not automatically end the Meeting. (BR-72) |
| **Normal Flow** | Complete Agenda Item<br>1. The Chair selects Complete for the current Agenda Item.<br>2. The system checks Chair assignment, Meeting status and Agenda Item status. (BR-69, BR-70)<br>3. The system records actual end time and duration, marks the Item Done and updates the Meeting agenda. (BR-70, BR-72, BR-73) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – Agenda Item Cannot Be Completed<br>1. The Item is not current or the Meeting is not InProgress.<br>2. The item remains unchanged. The use case ends.<br>EX-2 – Agenda Item Completion Cannot Be Saved<br>Entry Point: Step 3 of the Main Flow.<br>1. The system cannot save completion, actual end time or duration.<br>2. The Item remains InProgress and its timestamps remain unchanged. The Meeting remains InProgress.<br>3. The system displays MSG76 – "Unable to complete the agenda item. Please try again."<br>4. The use case ends without satisfying its completion postcondition. |

### UC-26 – View Meeting Agenda

Sequence Diagram: [UC26_ViewMeetingAgenda.txt](../06-sequencediagram/agenda-management/UC26_ViewMeetingAgenda.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant |
| **Secondary Actors** | None |
| **Description** | Allows an authorized user to view the Agenda Items that belong directly to the selected Meeting. |
| **Preconditions** | ● PRE-1: The user may view the selected Meeting. (BR-29) |
| **Postconditions** | ● POST-1: The Meeting Agenda Items, ordering and current execution status are shown read-only. |
| **Normal Flow** | View Meeting Agenda<br>1. The user opens the selected Meeting’s Agenda tab.<br>2. The system verifies Meeting access and retrieves Agenda Items belonging to the Meeting. (BR-29, BR-64)<br>3. The system shows Agenda Item title, presenter, planned duration, status, ordering and actual times where recorded. |
| **Alternative Flows** | AF-1 – Empty Meeting Agenda<br>1. No Agenda Items exist for the selected Meeting.<br>2. The system displays an empty Agenda state and the use case ends successfully. |
| **Exception Flows** | EX-1 – Meeting Not Accessible<br>1. The Meeting does not exist or the user lacks access.<br>2. No Agenda data is disclosed. The use case ends.<br>EX-2 – Meeting Agenda Cannot Be Retrieved<br>Entry Point: Step 2 of the Main Flow.<br>1. The system cannot retrieve Agenda data.<br>2. The system displays MSG128 – "Unable to load the meeting agenda. Please try again."; a retrieval failure is not displayed as an empty Agenda.<br>3. No data is modified and the use case ends. |

---

## FE-06 — Meeting Participant Management

### UC-27 – View Meeting Participants

Sequence Diagram: [UC27_ViewMeetingParticipants.txt](../06-sequencediagram/meeting-participant-management/UC27_ViewMeetingParticipants.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the Secretary assigned to a meeting to view the current internal participants and external Guests associated with that meeting, together with each internal participant’s Meeting Role or the Guest access type. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.<br>● PRE-2: The selected meeting exists. |
| **Postconditions** | ● POST-1: The current internal participant and external Guest list is displayed.<br>● POST-2: No participant or guest information is modified. |
| **Normal Flow** | View Meeting Participants<br>1. The Secretary opens the selected meeting.<br>2. The Secretary selects the Meeting Participants tab.<br>3. The system verifies that the Secretary is still assigned to the meeting.<br>4. The system retrieves the current internal participants and current external Guest invitations of the selected meeting; records retained only as historical evidence are excluded. (BR-84, BR-103, BR-117, BR-148)<br>5. The system displays Full Name, Email, participant type, and Meeting Role or Guest access type.<br>6. The Secretary reviews the participant list. |
| **Alternative Flows** | AF-1 – No Additional Participants or Guests<br>Entry Point: Step 4 of the Main Flow.<br>1. The current roster contains only the assigned Secretary or Secretaries and no other participants or Guest invitations.<br>2. The system displays the assigned Secretary entries; it does not show an empty roster.<br>Resume Step: Step 6 of the Main Flow. |
| **Exception Flows** | EX-1 – Meeting Is Not Accessible<br>1. At Step 3, the meeting does not exist or the current user is not its assigned Secretary.<br>2. The system displays the same not-found result used for an unknown meeting and does not disclose participant data. (BR-121)<br>3. The use case ends.<br>EX-2 – Participant List Cannot Be Retrieved<br>1. At Step 4, the system cannot retrieve the participant list.<br>2. The system displays MSG95 – "Unable to load meeting participants. Please try again."<br>3. The use case ends. |

### UC-28 – Manage Meeting Participants

_UC gom — đặc tả nằm ở các UC con UC-28a, UC-28b… bên dưới._

#### UC-28a – Add Meeting Participant

Sequence Diagram: [UC28a_AddMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28a_AddMeetingParticipant.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer, Secretary |
| **Secondary Actors** | Email / SMTP Service |
| **Description** | Allows the Meeting Organizer to add and assign the first Secretary when the meeting does not yet have one. After the first Secretary has been assigned, the Secretary manages the meeting roster by adding active internal users or inviting external Guests by name and email. Internal users are selected individually; organisational-unit invitation is not supported. |
| **Preconditions** | ● PRE-1: The actor is authenticated and is either the Meeting Organizer creating the initial Secretary assignment or a Secretary already assigned to the selected meeting.<br>● PRE-2: The selected meeting exists and is in Draft, Rejected, Approved, Published, or InProgress status.<br>● PRE-3: For an internal participant, the selected user has an active internal account and is not already a current participant. For an external Guest, the Guest name and email are valid and that email does not already have a current Guest invitation for the meeting. (BR-81, BR-82, BR-149) |
| **Postconditions** | ● POST-1: The selected internal user is added once, or one current external Guest invitation is created for the supplied email.<br>● POST-2: The invitation response starts as Pending with no response time.<br>● POST-3: An internal participant is assigned exactly one Meeting Role: Chair, Secretary, or Member. An external Guest has fixed Guest access and receives no system role or privileged Meeting Role. (BR-85, BR-150)<br>● POST-4: If the meeting is Published or InProgress, invitation delivery is queued. Internal participants may receive in-app and email notification; external Guests receive a secure guest invitation by email. (BR-90, BR-91, BR-152) |
| **Normal Flow** | Add Internal Meeting Participant<br>1. The authorized actor opens the Meeting Participants tab of the selected meeting.<br>2. The actor selects Add Participant.<br>3. The system displays active internal users who are not already current participants of the meeting. (BR-81, BR-82)<br>4. The actor selects one user and assigns one Meeting Role: Chair, Secretary, or Member.<br>5. The actor submits the participant information.<br>6. The system verifies that: when the meeting has no Secretary, the actor is its Meeting Organizer and the selected Meeting Role is Secretary; when the meeting already has a Secretary, the actor is an assigned Secretary; the meeting still permits participant management; the selected user is active and not already invited; the selected Meeting Role is valid; and assigning Chair would not create a second current Chair. (BR-78, BR-79, BR-80, BR-81, BR-82, BR-85, BR-123)<br>7. The system adds the selected internal user with Pending response status and records the selected Meeting Role. (BR-85, BR-88)<br>8. If the meeting is Published or InProgress, the system queues the applicable in-app notification and invitation email for asynchronous delivery. (BR-39, BR-40, BR-45, BR-90)<br>9. The system displays MSG96 – "Meeting participant added successfully." |
| **Alternative Flows** | AF-1 – Add External Guest by Email<br>Entry Point: Step 3 of the Main Flow.<br>1. The Secretary selects Add External Guest.<br>2. The system displays fields for Guest Name and Email.<br>3. The Secretary enters the Guest Name and Email and submits the invitation.<br>4. The system verifies that the Secretary is still assigned, the meeting still permits participant management, the email format is valid, the email does not already have a current Guest invitation for this meeting, and the email is not already represented by a current internal participant. (BR-79, BR-80, BR-149, BR-151)<br>5. The system creates a current external Guest invitation with Pending response status and fixed Guest access. No internal user account or system role is created. (BR-84, BR-88, BR-148, BR-150, BR-156)<br>6. If the meeting is Published or InProgress, the system generates a secure meeting-specific guest invitation and queues the invitation email. The Guest will later verify access using the invitation and an email one-time code; no internal account login is required. (BR-91, BR-152, BR-153, BR-156)<br>7. The flow resumes at Step 9 of the Main Flow.<br>AF-2 – Invitation Notification Delivery Is Unavailable<br>1. At Step 8 of the Main Flow or Step 6 of AF-1, the meeting is Published or InProgress but the applicable invitation delivery is unavailable.<br>2. The internal participant or external Guest invitation remains successfully added with Pending response status. (BR-40, BR-94)<br>3. The system records the delivery failure for retry.<br>4. The flow resumes at Step 9 of the Main Flow. |
| **Exception Flows** | EX-1 – Invalid Participant or Guest Information<br>1. At Step 6 of the Main Flow or Step 4 of AF-1, the submitted information is invalid. For an internal participant, the user is inactive, already a current participant, the Meeting Role is invalid, or assigning Chair would create a second current Chair. For an external Guest, the name/email is missing or invalid, the email is already invited as a current Guest, or the email is already represented by a current internal participant. (BR-81, BR-82, BR-85, BR-123, BR-148, BR-149, BR-151)<br>2. No participant or Guest invitation is added.<br>3. The system displays MSG97 – "Invalid meeting participant information." with the applicable field message(s), including: "The selected user is not available."; "This user is already a meeting participant."; "A valid meeting role is required."; "The Meeting Organizer must assign a Secretary as the first meeting participant."; "Guest name and email are required."; "Guest email is invalid."; "This guest email is already invited to the meeting."; "This email is already represented by an internal meeting participant."<br>4. The actor corrects the information.<br>5. For an internal participant, the flow resumes at Step 6 of the Main Flow. For an external Guest, the flow resumes at Step 4 of AF-1.<br>EX-2 – Participant List Cannot Be Modified in the Current Status<br>1. At Step 6 of the Main Flow or Step 4 of AF-1, the Meeting is outside Draft, Rejected, Approved, Published or InProgress (including WaitingApproval, Ended and subsequent lifecycle states). (BR-80)<br>2. No participant or Guest invitation is added.<br>3. The system displays MSG98 – "The meeting participant list cannot be modified in its current status."<br>4. The use case ends.<br>EX-3 – Participant or Guest Cannot Be Added<br>1. At Step 7 of the Main Flow or Step 5 of AF-1, the system cannot save the new internal participant or external Guest invitation.<br>2. No new participant or Guest access is created and no invitation is sent.<br>3. The system displays MSG99 – "Unable to add the meeting participant. Please try again."<br>4. The use case ends. |

#### UC-28b – Assign / Change Meeting Role

Sequence Diagram: [UC28b_AssignChangeMeetingRole.txt](../06-sequencediagram/meeting-participant-management/UC28b_AssignChangeMeetingRole.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the Secretary to assign or change an existing internal participant's Meeting Role within one meeting without changing that user's system role. External Guests have fixed Guest access and are not managed through this use case. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.<br>● PRE-2: The selected meeting is in Draft, Rejected, Approved, Published, or InProgress status.<br>● PRE-3: The selected participant is a current internal participant of the selected meeting. |
| **Postconditions** | ● POST-1: When a different valid role is selected, the participant's Meeting Role is updated for the selected meeting only. AF-1 is a no-change outcome and does not satisfy the role-update postcondition.<br>● POST-2: The participant's system role and invitation response remain unchanged.<br>● POST-3: If the role actually changes and the meeting is Published or InProgress, an in-app notification and an email describing the new Meeting Role are queued for the affected participant. |
| **Normal Flow** | Assign / Change Meeting Role<br>1. The Secretary opens the Meeting Participants tab of the selected meeting.<br>2. The Secretary selects Change Role for a current internal participant.<br>3. The system displays the participant's current Meeting Role.<br>4. The Secretary selects Chair, Secretary, or Member.<br>5. The Secretary submits the selected Meeting Role.<br>6. The system verifies that the Secretary is still assigned to the meeting, the meeting still permits participant management, the internal participant still belongs to the meeting, the selected Meeting Role is valid, the change would not remove a required Secretary or Chair, and assigning Chair would not create a second current Chair. (BR-79, BR-80, BR-85, BR-122, BR-123, BR-126)<br>7. The system updates only the participant's Meeting Role for the selected meeting. The user's system Role and Invitation Response remain unchanged. (BR-85, BR-86, BR-96)<br>8. If the meeting is Published or InProgress, the system queues an in-app notification and an email describing the new Meeting Role for asynchronous delivery to the participant. (BR-92)<br>9. The system displays MSG100 – "Meeting role updated successfully." |
| **Alternative Flows** | AF-1 – Selected Role Is Unchanged<br>1. At Step 5, the selected Meeting Role is the participant's current role.<br>2. The system leaves the participant unchanged.<br>3. No update is saved, no role-change notification is queued and MSG100 is not displayed.<br>4. The use case ends with a no-change outcome; the role-update postcondition is not satisfied.<br>AF-2 – Role Change Notification Delivery Is Unavailable<br>1. At Step 8, the Meeting Role has been updated but notification delivery is unavailable.<br>2. The Meeting Role change remains successful. (BR-94)<br>3. The system records the delivery failure for retry.<br>4. The flow resumes at Step 9 of the Main Flow. |
| **Exception Flows** | EX-1 – Invalid Meeting Role<br>1. At Step 6, the submitted Meeting Role is not Chair, Secretary, or Member. (BR-85)<br>2. The participant's Meeting Role remains unchanged.<br>3. The system displays MSG101 – "A valid meeting role is required."<br>4. The Secretary selects a valid Meeting Role.<br>5. The flow resumes at Step 6 of the Main Flow.<br>EX-2 – Required Meeting Role Would Be Removed<br>1. At Step 6, the change would remove the last assigned Secretary, remove the current Chair while the meeting is Published or InProgress, or create a second current Chair. (BR-122, BR-123, BR-126)<br>2. The participant's Meeting Role remains unchanged.<br>3. The system displays MSG109 – "The meeting must retain its required Chair and Secretary. Assign a replacement first."<br>4. The use case ends.<br>EX-3 – Participant List Cannot Be Modified in the Current Status<br>1. At Step 6, the Meeting is outside Draft, Rejected, Approved, Published or InProgress (including WaitingApproval, Ended and subsequent lifecycle states). (BR-80)<br>2. The participant's Meeting Role remains unchanged.<br>3. The system displays MSG98 – "The meeting participant list cannot be modified in its current status."<br>4. The use case ends.<br>EX-4 – Meeting Role Cannot Be Updated<br>1. At Step 7, the system cannot save the Meeting Role change.<br>2. The participant's Meeting Role remains unchanged.<br>3. The system displays MSG102 – "Unable to update the meeting role. Please try again."<br>4. The use case ends. |

#### UC-28c – Remove Meeting Participant

Sequence Diagram: [UC28c_RemoveMeetingParticipant.txt](../06-sequencediagram/meeting-participant-management/UC28c_RemoveMeetingParticipant.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the Secretary to remove a current internal participant or revoke a current external Guest invitation while the meeting has not yet ended. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.<br>● PRE-2: The selected meeting is in Draft, Rejected, Approved, Published, or InProgress status.<br>● PRE-3: The selected target is a current internal participant or a current external Guest invitation of the selected meeting. |
| **Postconditions** | ● POST-1: The selected internal user is no longer a current participant, or the selected external Guest invitation is revoked.<br>● POST-2: Access granted by the removed internal participant assignment or external Guest invitation is revoked. Access independently granted through another valid relationship remains effective.<br>● POST-3: If the meeting is Published or InProgress, the affected internal user receives the applicable in-app/email removal notice; an external Guest may receive an email revocation notice. No removal notification contains a protected meeting link.<br>● POST-4: If an affected user is currently in the online meeting room and no other room-entry access remains, the system disconnects that user. (BR-101) |
| **Normal Flow** | Remove Meeting Participant or Guest<br>1. The Secretary opens the Meeting Participants tab of the selected meeting.<br>2. The Secretary selects Remove for the target internal participant or external Guest.<br>3. The system requests confirmation.<br>4. The Secretary confirms the removal.<br>5. The system verifies that the Secretary is still assigned to the meeting, the meeting still permits participant management, the target still belongs to the meeting roster, and—when the target is an internal participant—the removal would not remove a required Secretary or Chair. (BR-79, BR-80, BR-122, BR-126)<br>6. If the target is an internal participant, the system removes the current participant assignment. If the target is an external Guest, the system revokes the Guest invitation and invalidates access granted by that invitation. For a Published or InProgress meeting, response and attendance evidence is preserved as historical data rather than physically deleted. (BR-97, BR-102, BR-157)<br>7. The system re-evaluates room-entry access for every affected user and disconnects any currently connected user who no longer has access through another valid relationship. (BR-101)<br>8. If the meeting is Published or InProgress, the system queues the applicable removal/revocation notification for asynchronous delivery. Internal participants may receive in-app and email delivery; external Guests receive email only. (BR-93)<br>9. The system displays MSG103 – "Meeting participant removed successfully." |
| **Alternative Flows** | AF-1 – Removal Notification Delivery Is Unavailable<br>1. At Step 8, the participant or Guest access has already been removed/revoked but the applicable notification delivery is unavailable.<br>2. The removal and access revocation remain successful. (BR-94)<br>3. The system records the delivery failure for retry.<br>4. The flow resumes at Step 9 of the Main Flow. |
| **Exception Flows** | EX-1 – Removal Cancelled<br>1. At Step 3, the Secretary does not confirm the removal.<br>2. The target remains unchanged.<br>3. The use case ends without satisfying the removal postcondition.<br>EX-2 – Required Meeting Role Would Be Removed<br>1. At Step 5, an internal participant removal would remove the last assigned Secretary, or would remove the current Chair while the meeting is Published or InProgress. (BR-122, BR-126)<br>2. The participant is not removed.<br>3. The system displays MSG109 – "The meeting must retain its required Chair and Secretary. Assign a replacement first."<br>4. The use case ends.<br>EX-3 – Participant List Cannot Be Modified in the Current Status<br>1. At Step 5, the Meeting is outside Draft, Rejected, Approved, Published or InProgress (including WaitingApproval, Ended and subsequent lifecycle states). (BR-80)<br>2. The target is not removed or revoked.<br>3. The system displays MSG98 – "The meeting participant list cannot be modified in its current status."<br>4. The use case ends.<br>EX-4 – Participant or Guest Cannot Be Removed<br>1. At Step 6, the system cannot remove the internal participant or revoke the external Guest invitation.<br>2. The target remains current and retains existing access.<br>3. The system displays MSG104 – "Unable to remove the meeting participant. Please try again."<br>4. The use case ends. |

### UC-29 – Respond to Meeting Invitation

Sequence Diagram: [UC29_RespondToMeetingInvitation.txt](../06-sequencediagram/meeting-participant-management/UC29_RespondToMeetingInvitation.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Chair, Secretary, Participant, Guest |
| **Secondary Actors** | Email / SMTP Service |
| **Description** | Allows an invited internal participant or external Guest to respond to their own Meeting invitation by selecting Accepted or Declined while responses are allowed. The invitee may change a previous response between these two values. An external Guest responds through a verified meeting-scoped Guest invitation without signing in to an internal account. |
| **Preconditions** | ● PRE-1: For an internal invitee, the user is authenticated, has an active account, and is a current internal participant. For an external Guest, a current Guest invitation exists and the Guest can verify access through the invited email; no internal account login is required. (BR-148, BR-153, BR-156)<br>● PRE-2: The invitee currently owns the selected invitation.<br>● PRE-3: The selected meeting is Published or InProgress. |
| **Postconditions** | ● POST-1: The invitee's latest Invitation Response is Accepted or Declined.<br>● POST-2: The response time is recorded.<br>● POST-3: Responding does not transfer or change any Meeting Role.<br>● POST-4: External Guest access remains scoped to the invited Meeting and does not create an internal user account or system role. (BR-148, BR-150, BR-154, BR-156) |
| **Normal Flow** | Respond to Meeting Invitation<br>1. The invitee opens the invitation or published meeting details. An internal invitee uses the authenticated system; an external Guest opens the secure guest invitation link.<br>2. The system identifies the invitation owner. For an internal invitee, it uses the authenticated account. For an external Guest, it validates the guest invitation and requires a one-time verification code sent to the invited email; successful verification establishes a limited meeting-scoped guest session. (BR-104, BR-152, BR-153, BR-154, BR-155, BR-156)<br>3. The system displays the current Invitation Response.<br>4. The invitee selects Accepted or Declined and submits the response.<br>5. The system verifies that the selected response is allowed, the invitee still owns a current invitation, and the meeting is still Published or InProgress. (BR-104, BR-105, BR-106)<br>6. The system records the selected Invitation Response and response time. Meeting Role and access relationships remain unchanged. (BR-115)<br>7. The system displays MSG105 – "Meeting invitation response recorded successfully." |
| **Alternative Flows** | AF-1 – Change a Previous Response<br>Entry Point: Step 3 of the Main Flow.<br>1. The invitee has already responded Accepted or Declined.<br>2. The invitee selects the other response and submits it.<br>Resume Step: Step 5 of the Main Flow; the system revalidates invitation ownership, Meeting status and Guest access when applicable before saving the new response. (BR-104, BR-105, BR-106) |
| **Exception Flows** | EX-1 – Invalid Invitation Response<br>1. At Step 5 of the Main Flow, the submitted response is neither Accepted nor Declined. (BR-105)<br>2. The previous Invitation Response remains unchanged.<br>3. The system displays MSG106 – "Invalid meeting invitation response." with the applicable validation message.<br>4. The invitee corrects the response.<br>5. The flow resumes at Step 4 of the Main Flow.<br>EX-2 – Invitation Can No Longer Be Answered<br>1. At Step 2 or Step 5, the Meeting is no longer Published or InProgress, the invitation is no longer current, or an external Guest invitation has expired or been revoked. (BR-104, BR-106, BR-155, BR-157)<br>2. The previous Invitation Response remains unchanged.<br>3. The system displays MSG107 – "This meeting invitation can no longer be answered."<br>4. The use case ends.<br>EX-3 – Guest Verification Fails<br>1. At Step 2, an external Guest provides an invalid or expired one-time verification code. (BR-153)<br>2. No Guest session is established and the Invitation Response remains unchanged.<br>3. The system displays MSG126 – "Unable to verify the guest invitation. Check the code and try again."<br>4. The Guest may retry verification while the Guest invitation remains valid.<br>EX-4 – Invitation Response Cannot Be Saved<br>1. At Step 6 of the Main Flow, the system cannot save the Invitation Response and response time.<br>2. The previous Invitation Response, response time and access remain unchanged.<br>3. The system displays MSG108 – "Unable to save the meeting invitation response. Please try again."<br>4. The use case ends. |

### UC-30 – View Participant Response Status

Sequence Diagram: [UC30_ViewParticipantResponseStatus.txt](../06-sequencediagram/meeting-participant-management/UC30_ViewParticipantResponseStatus.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the Secretary assigned to a meeting to review the latest invitation response of every current internal participant and external Guest, together with a summary by response status. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated and assigned to the selected meeting with the Secretary meeting role.<br>● PRE-2: The selected meeting exists. |
| **Postconditions** | ● POST-1: The latest response status of every current internal participant and external Guest is displayed.<br>● POST-2: The number of Pending, Accepted, and Declined responses is displayed.<br>● POST-3: Invitation Response data is not modified. |
| **Normal Flow** | View Participant Response Status<br>1. The Secretary opens the Meeting Participants tab of the selected meeting.<br>2. The Secretary selects Participant Response Status.<br>3. The system verifies that the Secretary is still assigned to the meeting.<br>4. The system retrieves all current internal participants and current external Guest invitations with their latest Invitation Response; records retained only as historical evidence are excluded. (BR-103, BR-115, BR-117, BR-148)<br>5. The system displays each invitee's Full Name, Email, participant type, Meeting Role or Guest access type, Invitation Response, response time.<br>6. The system displays the total number of Pending, Accepted, and Declined responses. (BR-115)<br>7. The Secretary reviews the response status summary. |
| **Alternative Flows** | AF-1 – No Invitee Has Responded<br>1. At Step 4, every current internal participant and external Guest has Pending response status.<br>2. The system displays all invitees as Pending and shows zero for Accepted and Declined.<br>3. The use case ends successfully. |
| **Exception Flows** | EX-1 – Meeting Is Not Accessible<br>1. At Step 3, the meeting does not exist or the current user is not its assigned Secretary.<br>2. The system displays the same not-found result used for an unknown meeting and does not disclose participant response data. (BR-121)<br>3. The use case ends.<br>EX-2 – Participant Response Status Cannot Be Retrieved<br>1. At Step 4, the system cannot retrieve the participant response data.<br>2. The system displays MSG95 – "Unable to load meeting participants. Please try again."<br>3. The use case ends. |

### UC-31 – Record Attendance

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Secretary |
| **Secondary Actors** | None |
| **Description** | Allows the assigned Secretary to record official meeting attendance independently of invitation responses and online-room presence. |
| **Preconditions** | ● PRE-1: The Secretary is authenticated, active and assigned to the selected Meeting.<br>● PRE-2: The target is a current participant or Guest invitation of that Meeting.<br>● PRE-3: The Meeting is InProgress. Attendance may be recorded or corrected only in this state; it is locked in Draft, WaitingApproval, Rejected, Approved, Published, Ended and subsequent states. |
| **Postconditions** | ● POST-1: The selected participant's attendance status and, when available, attendance time are saved.<br>● POST-2: The invitation response remains unchanged. |
| **Normal Flow** | Record Attendance<br>1. The Secretary opens the meeting's Participants or Attendance view.<br>2. The system displays current participants and their recorded attendance status.<br>3. The user selects a participant and sets the observed attendance status.<br>4. The system revalidates the active assigned Secretary, current target membership and Meeting InProgress status, then saves the attendance record. Invitation Response is unchanged.<br>5. The system refreshes the attendance view and confirms the update. |
| **Alternative Flows** | AF-1 – Revise Recorded Attendance<br>Entry Point: Step 3 of the Main Flow.<br>1. The Secretary selects a participant or Guest with an existing attendance record.<br>2. While the Meeting remains InProgress, the Secretary corrects the observed attendance status and submits it.<br>Resume Step: Step 4 of the Main Flow; the system revalidates authorization and Meeting state before saving. |
| **Exception Flows** | EX-1: The meeting or participant is unavailable, or the user lacks the required meeting role: the system denies the update.<br>EX-2: At Step 4, the Meeting is no longer InProgress: the system retains the previous record and explains the restriction.<br>EX-3: The save fails: the system keeps the last successfully saved status and reports the failure. |

---

## FE-07 — Personal Document Library & Meeting Document Management

### UC-32 – View Meeting Documents

_UC gom — đặc tả nằm ở các UC con UC-32a, UC-32b… bên dưới._

#### UC-32a – View Meeting Documents

Placeholder only – detailed Use Case Specification will be added later.

#### UC-32b – View Document Version History

Placeholder only – detailed Use Case Specification will be added later.

#### UC-32c – Preview Meeting Document

Placeholder only – detailed Use Case Specification will be added later.

### UC-33 – Manage Meeting Documents

_UC gom — đặc tả nằm ở các UC con UC-33a, UC-33b… bên dưới._

#### UC-33a – Add Document to Meeting

Placeholder only – detailed Use Case Specification will be added later.

#### UC-33b – Add New Meeting Document Version

Placeholder only – detailed Use Case Specification will be added later.

### UC-34 – View Personal Document Library

Placeholder only – detailed Use Case Specification will be added later.

### UC-35 – Comment on Meeting Document

Placeholder only – detailed Use Case Specification will be added later.

### UC-36 – Upload Document to Personal Library

Placeholder only – detailed Use Case Specification will be added later.

---

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
Scope: Votes are created, opened, cast and closed only while a Meeting is InProgress. No time-boxed or pre/post-meeting voting, quorum or separate Resolution. The Chair records Passed, NotPassed or Deferred after closing the Vote.

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

[Placeholder] Detailed Use Case Specification will be added later. Chair records Passed, NotPassed or Deferred after Vote closes.

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

