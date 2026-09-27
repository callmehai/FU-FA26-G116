# 5.1 Business Rules

| ID | Rule Definition |
|---|---|
| BR-01 | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. |
| BR-02 | Authentication failure messages must not reveal whether a specific username or email exists in the system. |
| BR-03 | A deactivated user account must not be allowed to sign in or access authenticated system functions. |
| BR-04 | A user without active system access or the required active role/permission must not be allowed to access the system. |
| BR-05 | Identity information returned by Google must be successfully verified before it can be used for authentication. |
| BR-06 | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. |
| BR-07 | Signing in with Google must not automatically create a new internal user account. |
| BR-08 | User permissions are determined by the system and must not be selected manually during the sign-in process. |
| BR-09 | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. |
| BR-10 | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. |
| BR-11 | List views must support pagination when the number of records exceeds the configured page size. |
| BR-12 | When a search or filter returns no matching records, the system must display an appropriate no-results message instead of treating the result as a system error. |
| BR-13 | Username and email must each be unique across user accounts. |
| BR-14 | All mandatory user-account information must be provided and valid before an account can be created or updated. |
| BR-15 | A newly created user account must have Active status by default. |
| BR-16 | Successful administrative changes to user accounts or role-permission configuration must be recorded in the audit log. |
| BR-17 | General user-account updates may modify Full Name, Job Title, and Contact Information. Email, System Role, and password credentials must be managed separately. |
| BR-18 | When a user account is deactivated, all active authenticated sessions for that account must be terminated. |
| BR-19 | Deactivating a user account must not delete historical records associated with that user. |
| BR-20 | A password-reset link must have a limited validity period and must not be reusable after a successful password reset. |
| BR-21 | An administrator must not be able to view a user's existing password. |
| BR-22 | Password reset may only be initiated for an account that is eligible to use password-based authentication. |
| BR-23 | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. |
| BR-24 | All required meeting information must be provided before a meeting can be created. |
| BR-25 | A meeting's Planned End Time must be later than its Planned Start Time. |
| BR-26 | A newly created meeting must have Draft status. |
| BR-27 | Meeting information may be edited only while the meeting is in Draft status. A Rejected meeting must first be returned to Draft by its Meeting Organizer. |
| BR-28 | Updating meeting information must not implicitly change the meeting lifecycle status. |
| BR-29 | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, current assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting, and a relationship does not grant actions reserved for another relationship. The Meeting Organizer and current assigned Meeting Approver may read the complete lifecycle history, including rejection reasons. Other users who may read a Published meeting may see its lifecycle timeline without rejection reasons. A former Meeting Approver does not retain full-history/reason access solely because of a previous assignment. |
| BR-30 | The meeting list may be searched by Meeting Name and filtered by Meeting Status, Confidentiality Level, and Time Range. |
| BR-31 | When multiple search or filter criteria are applied, a record must satisfy all applicable criteria to appear in the result. |
| BR-32 | The meeting list must support sorting, and the current search, filter, and sorting criteria must be preserved when navigating between pages. |
| BR-33 | A meeting may be submitted for approval only when all mandatory meeting information and required agenda information are complete and the meeting is in Draft status. |
| BR-34 | Submitting a meeting for approval changes its status from Draft to WaitingApproval. A meeting in WaitingApproval status cannot be edited until an approval decision is made. A Rejected meeting cannot be submitted directly: only its Meeting Organizer may explicitly return it from Rejected to Draft, which creates a lifecycle-history record, preserves the earlier rejection reason, and does not initiate invitation delivery. The Organizer may then edit the Draft and submit it through the normal Draft-to-WaitingApproval flow with a currently eligible Meeting Approver. |
| BR-35 | An Assigned Meeting Approver is selected and recorded only when the Meeting Organizer submits a ready Draft for approval. A Draft has no current Assigned Meeting Approver; returning a Rejected meeting to Draft clears the previous cycle's assignment. For each approval cycle, only its current Assigned Meeting Approver holding the meeting_approver permission may approve or reject. Being Chair alone does not grant approval authority. |
| BR-36 | Approving a meeting changes its status from WaitingApproval to Approved. Rejecting a meeting changes its status from WaitingApproval to Rejected. A rejection reason is mandatory. |
| BR-37 | Only an authorized Meeting Organizer may publish a meeting that is currently in Approved status. Being Chair alone does not grant publication authority. Publishing changes the meeting status from Approved to Published. Invitation delivery to meeting participants is initiated automatically and only after the meeting has been successfully published. |
| BR-38 | Every meeting lifecycle transition must create a history record containing at least the acting user, timestamp, action, and resulting status. A rejection history record must also contain the rejection reason. Meeting lifecycle history records are immutable and must not be updated or deleted. |
| BR-39 | Invitation delivery must be executed asynchronously via a background task queue to prevent blocking the user publication flow. |
| BR-40 | Failure of an asynchronous invitation delivery task must trigger retry logic without reverting the Published state of the meeting. |
| BR-41 | Meeting publication scope is strictly limited to meetings in Approved status and cannot be bypassed by direct state manipulation. |
| BR-42 | All participant invitation notifications must include a direct link to the published meeting details and attached materials. |
| BR-43 | System logging must track the queuing and delivery status of all asynchronous invitation messages for auditability. |
| BR-44 | Re-publishing or updating invitation delivery for an already published meeting requires explicit organizer action and creates a distinct lifecycle record. |
| BR-45 | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time, or when a task is assigned to them. |
| BR-46 | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. |
| BR-47 | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. |
| BR-48 | The notification bell shows how many of the current user's notifications are unread. |
| BR-49 | Opening the notification list does not mark any notification as read. |
| BR-50 | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. |
| BR-51 | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. |
| BR-52 | A meeting or task notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. |
| BR-53 | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. |
| BR-54 | When a meeting is first published, the system snapshots only meeting participant rows whose user accounts are active and whose invitation response is not Delegated. A delegate is not added only because a DelegatedToUserId points to them; if they also have their own active, non-Delegated participant row, that row qualifies normally. Changes after the Publish transaction commits do not change that batch. |
