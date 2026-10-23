# Report 3 – Software Requirement Specification

> Nguồn: [Tài liệu nguồn](source/Report_3_Software_Requirement_Specification.docx)

CAPSTONE PROJECT REPORT

Report 3 – Software Requirement Specification

– Hanoi, Aug 2026 –

Table of Contents

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 27/09/2026 | A | KhanhPG | Initial changes: add context diagram and actors, use case specification for Authentication |
| 30/09/2026 | M | KhanhPG | Add Meeting Session use cases, assign Meeting Session planning to the Secretary, complete Meeting Session business rules and system messages, and refine affected Meeting Management flows. |
| 30/09/2026 | M | KhanhPG | Add Agenda Management use cases linked to Meeting Sessions, including agenda planning, live agenda control, business rules, and system messages. |
| 01/10/2026 | M | KhanhPG | Replace Meeting Session deletion with cancellation; allow planned-session changes after publication; add post-publication scheduling, notification, and lifecycle rules. |
| 03/10/2026 | M | KhanhPG | Synchronize Meeting Management and Notification updates; move planned scheduling from Meeting to Meeting Session; verify BR-01 to BR-77 alignment. |
| 03/10/2026 | M | KhanhPG | Refine Meeting Participant Management actors and support external Guest invitations by email without an internal account login; align UC-21, UC-37–UC-42, UC-45, BR-78–BR-89/BR-94, add BR-101–BR-103 and MSG126. |
| 03/10/2026 | M | KhanhPG | Synchronize the latest two GitHub commits for Meeting Participant Management: add UC-37 to UC-42, add BR-78 to BR-89 and MSG95 to MSG109, and update affected Meeting Management publication requirements. |
| 03/10/2026 | M | KhanhPG | Decouple Meeting, Meeting Session, and Online Meeting Room lifecycles; make Meeting Sessions optional for publication; add explicit Start/End Meeting and Online Meeting Room use cases; align affected business rules and system messages. |
| 04/10/2026 | M | KhanhPG | Refine UC-17 to meeting-level details only; add UC-53 View Meeting Sessions and UC-54 View Meeting Agenda as separate read use cases; align related BR references and system messages. |
| 04/10/2026 | M | KhanhPG | Move UC-53 View Meeting Sessions into Meeting Session Management and UC-54 View Meeting Agenda into Agenda Management; remove temporary Read Views feature. |
| 05/10/2026 | M | KhanhPG | Synchronize the feature taxonomy with Reports 1 and 2: map existing UC groups to the FE-01 through FE-15 baseline, split the former Meeting & Schedule Management scope into FE-04 through FE-06, and keep all existing UC IDs unchanged. |
| 05/10/2026 | M | KhanhPG | Synchronize the Use Case inventory with the PO-confirmed FE-01 to FE-15 baseline. Add placeholder names through UC-94, introduce Personal Document Library, make Secretary the meeting-audio uploader, and reserve the approved Attendance, Summary Generation, Document Comment, Audit Log and Minutes Revision use cases. Detailed specifications for newly added UCs are intentionally deferred. |
| 05/10/2026 | M | KhanhPG | Refactor BR-78 onward into atomic, independently testable business rules; align Chair cardinality with the approved publication rule (at most one current Chair and exactly one active Chair required for publication); update affected Use Case business-rule references. |
| 08/10/2026 | M, D | KhanhPG | Remove FE-04 and the Meeting Session layer; consolidate independent Meeting lifecycle into FE-05. Move Agenda, Vote and Resolution directly to Meeting; align UC-78 Minutes generation, BRs, messages and permissions. |
| 09/10/2026 | M, D | KhanhPG | Align SRS with PO decisions: restore FE-04, retire FE-05 and six session UCs; limit voting to in-meeting Vote and Chair conclusion, update rules/messages and preserve placeholders. |
| 09/10/2026 | M | KhanhPG | Align active UC flows, BR/MSG traceability, screens and embedded BF-02/03/04 with PO-approved single Meeting and in-meeting Voting. Retain retired IDs and intentional UC placeholders. |
| 12/10/2026 | M | KhanhPG | Revise FE-06 meeting participant management: clarify UC-27–UC-31, support adding multiple internal participants together, replacing the Chair, meeting-specific additional viewing permissions, and complete-list Present/Absent attendance for internal participants and Guests. Align business rules, messages, screen descriptions and sequence diagram sources; preserve all existing images. Primary Actors list actor names only. |
| 12/10/2026 | M | KhanhPG | Refine FE-06 use cases to business-level flows; align Chair/Secretary/Member viewing access, response identity visibility, bulk participant additions, Chair replacement, complete attendance and End Meeting requirements. Update BR/MSG references, participant screens, and affected Agenda and Participant sequences, including UC-22; align AF/EX references and reserved message contexts. Regenerate only changed sequence images and preserve all existing embedded images. |
| 13/10/2026 | M, D | HaiTV | Apply ADR-0018 (no document versions): library entries and Meeting documents refer to one immutable stored file; drop UC-32b and UC-33b; add UC-33c Remove Document from Meeting, UC-71 Manage Personal Documents and UC-72 Manage Library Folders; previews are rendered in the browser without server conversion; only the Chair and Secretary add or remove Meeting documents; add library folders as in AI-eCabinet, drop the Guest sharing toggle. Align FE-11 screens with Guest voting, Chair-selected eligible voter roles per Vote and deleting a Draft Vote. Add BR-183 to BR-192 and the Folder entity. |
| 14/10/2026 | M | LocHD | Update screen images to wireframe v4 (ADR-0018, Chair-selected eligible voter roles); rewrite 7.1 as the empty Document Library state; correct examples in 5.3, 7.5, 11.1 and 11.4; align the 14.2 Vote button and the 7.5 watermark with the wireframe. |
| 14/10/2026 | M | LocHD | Align 8.5, footnote 14 and BR-165 with Chair-selected eligible voter roles (Guests vote when selected); retire MSG113 (ADR-0013); describe the two implemented background jobs in 16.1–16.2 and I.5.3; drop implementation status from API-04/API-05; update the screen flow for the 7.1 and 7.5 names; fix bold bullets in 7.5 and 11.1. |
| 14/10/2026 | M | KhanhPG | Complete business-level Use Case Specifications for FE-07 Personal Document Library & Meeting Document Management and FE-11 Voting Management (UC-52a, UC-52b, UC-53–UC-57); align related Business Rules and System Messages. Clarify Vote draft preparation in Approved, Published and InProgress, one Open Vote per Meeting, rejection when no voters are eligible, and fixed eligibility with current access checks. Preserve existing screen specifications; leave screen updates to their responsible author. |
| 15/10/2026 | M | KhanhPG | Align UC Specifications, Business Rules and System Messages with ADR-0020; add UC-33b and UC-33d, Meeting-based document reading, Meeting-end document/link locks, multiple Agenda Item links, Meeting-start folder dates and independent Meeting document names. Screen and data descriptions are handed over in a separate note. Record PO follow-up: read-only documents/comments after Meeting end, preserve Meeting documents when deleting an Agenda Item, and start a new addition without previous comments when readding a removed document. Review all sixteen FE-07 UC Specifications for business wording. Review FE-11 against ADR-0016/0017/0019/0020: clarify Meeting-owned Votes, draft versus immediate opening, voting checks, secret-ballot completion and inaccessible-resource flows; retire cancellation MSG237. Hand over UI action/state alignment in the FE-11 screen note. Clarify BR-192/228 and UC-33a under ADR-0020: automatic Meeting folders use the planned Meeting start date in Vietnam time, including recreation after deletion. Synchronize the non-screen FE-07/FE-11 handover: actor lists, message contexts, UC inventory and target data relationships for Guest identities, fixed voter eligibility and multiple document-Item links; simplify BR-159. Screen specifications and illustrations remain pending in the handover notes. Support one or more files per UC-33a/36 submission under BR-286: validate each file, add the whole selection or none, and identify invalid selections. Align the reserved message wording with multiple-file outcomes. |
| 16/10/2026 | M | LocHD | Align UC-19, UC-20 and BR-39 with the SCRUM-19 team decision of 16/10/2026: the Meeting Organizer is notified in the application when a Meeting is approved or rejected (a rejection includes its reason), together with the decision and its history, and no email is sent for the decision; on publication, the publishing Meeting Organizer receives no in-app invitation notification, while every current internal participant, including that Organizer, is still sent the invitation email. |
| 22/10/2026 | M | LocHD | Align UC-32a, UC-32c and UC-35 with SCRUM-21/22/23: documents grouped by Agenda Item, Vote and general, Documents panel in the online meeting room, audit of views and downloads of Confidential/Secret documents, one-level replies to comments; UC-33a EX-8 upload limit of 10 files and 200 MB per submission; add BR-315 to BR-319 and MSG288; update the ERD image (MeetingDocumentAgendaItem, Guest comment author). |
| 23/10/2026 | M | LocHD | Rewrite the screen specifications 7.3 Documents Tab, 7.4 Add Document dialog, 7.5 Document Viewer and 8.1–8.5 of the online room to match wireframe v7 and the room as deployed (no raise hand, mute all or saved chat); rename 8.3 and 8.5, add 8.6 In-Room Documents Panel (planned); update their images and the Screen List. |

*A - Added M - Modified D - Deleted

## Definition and Acronyms

| Acronym | Definition |
| --- | --- |
| IMDMS | Internal Meeting and Document Management System |
| AF | Alternative Flow |
| EX | Exception |
| BF | Business Flow |
| BR | Business Rule |
| MSG | Message |

## I. Overall Requirements

### 1. Context Diagram

TrustCabinet is an Internal Meeting and Document Management System that interacts with the following human/internal actors: Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant, and Guest. The context diagram also shows three external service actors: Google Identity Provider, AI / Speech Processing Provider, and Email / SMTP Service.

- Google Identity Provider supports Google-based sign-in and provides verified identity information for users accessing TrustCabinet.

- AI / Speech Processing Provider processes meeting audio after the meeting to produce a speaker-attributed transcript and an editable draft summary derived from that transcript. The Secretary may correct transcript text and speaker attribution before generating or regenerating the summary. Formal meeting minutes are prepared separately from approved meeting records and are not directly generated by the AI provider from secretary notes or arbitrary meeting documents.

- Email / SMTP Service handles outbound email delivery, including password-reset messages, meeting invitations, and other meeting-related communications.

Through these interactions, TrustCabinet acts as the central point connecting meeting participants, meeting records, documents, voting results, and post-meeting minutes, while external services are used only for specific supporting functions outside the main system boundary.

### 2. Main Business Flow

#### 2.1 System Access & Administration (BF-01)

Trigger: An internal staff member needs to be granted access to TrustCabinet, or needs their existing access changed.

End condition: The user has a valid account with the appropriate system role and permissions and can access the functions they are allowed to use.

#### 2.2 Meeting Preparation, Approval & Participant Coordination (BF-02)

Trigger: A Meeting Organizer identifies the need to hold a new meeting.

End condition: The meeting is Published, invitations have been issued, invitees can respond, and the meeting is ready to be conducted.

#### 2.3 Meeting Execution & Decision Making (BF-03)

Trigger: The Chair decides to start a meeting that is in Published status.

End condition: The meeting is Ended; its agenda outcomes and meeting-level voting results and decisions have been recorded.

#### 2.4 Post-Meeting Processing & Formal Records (BF-04)

Trigger: A meeting has Ended and its post-meeting records need to be processed.

End condition: The meeting has formal minutes that have completed signing, finalization and sealing, and the meeting records can be accessed according to permissions.

### 3. Conceptual Data Model

#### 3.1 Entity Relationship Diagram

Conceptual ERD for the independent Meeting model. Vote belongs directly to Meeting; Agenda Item also belongs directly to Meeting. Cardinalities shown are logical business relationships and should be confirmed against the final database design. FE-06 refinement: an internal participant assignment may have zero or more MeetingParticipantPermission grants, each granted by one Secretary account. Both internal participants and Guest invitations carry optional official attendance with the recording Secretary and save time. These are conceptual requirements; they do not assert that runtime schema changes have been implemented. The decisions about Guest display information, additional permissions and voting are requirements; storage changes and their detailed design are deferred.

#### 3.2 Entity Descriptions

This section describes each table of the data model shown in the Entity Relationship Diagram above. Key: PK = primary key, FK = foreign key, UK = unique key. Enumerated fields list their allowed values in the Description column.

Entity 1: User

Internal account of an organization member. A user signs in with email and password, with Google, or both; the account is deactivated rather than deleted so historical records stay attributable.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the user account. |
| 2 | FullName | string |  | No | Full display name of the user. |
| 3 | Email | citext | UK | No | Sign-in email address; unique and compared case-insensitively. |
| 4 | PasswordHash | string |  | Yes | Salted hash of the password (the plain password is never stored). Null for an account that only signs in with Google. |
| 5 | GoogleSubjectId | string | UK | Yes | Google account subject identifier of a linked Google account; unique when present. Null when no Google account is linked. |
| 6 | RoleId | uuid | FK | No | System role assigned to the user (references Role). |
| 7 | IsActive | bool |  | No | True while the account is active; a deactivated account cannot sign in or use authenticated functions. |

Entity 2: Role

Named system role that bundles a set of capabilities and is assigned to users. Built-in roles ship with the system; additional roles can be created by the System Admin.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the role. |
| 2 | Name | citext | UK | No | Role name; unique and compared case-insensitively. |
| 3 | Description | string |  | No | Free-text description of the role. |
| 4 | IsBuiltIn | bool |  | No | True for a built-in role, which cannot be renamed or deleted (its capabilities can still be configured). |

Entity 3: RoleCapability

Role and permission matrix: each row grants one capability to one role. Rows are removed together with their role.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | RoleId | uuid | PK, FK | No | Role that holds the capability (references Role); part of the composite primary key. |
| 2 | Capability | string | PK | No | Code of the system capability granted to the role; part of the composite primary key. |

Entity 4: AuditLogEntry

Append-only business audit trail of significant actions such as sign-in, meeting lifecycle changes, access to confidential documents and permission changes. Rows are never updated or deleted.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the audit entry. |
| 2 | ActorId | uuid | FK | Yes | User who performed the action (references User). Null when there is no signed-in actor, for example a failed sign-in. |
| 3 | Action | string |  | No | Stable code of the action performed, for example meeting.published. |
| 4 | ObjectType | string |  | No | Type of the affected object, for example Meeting, Document or User. |
| 5 | ObjectId | uuid |  | No | Identifier of the affected object. |
| 6 | IpAddress | string |  | No | IP address the request came from. |
| 7 | Details | json |  | No | Additional information about the action, stored as JSON. |
| 8 | OccurredAt | timestamptz |  | No | Date and time the action occurred. |

Entity 5: Meeting

Central entity of the system: one independent meeting with its schedule, mode, sensitivity and lifecycle. The meeting status decides who may edit it, when voting can open and when minutes can be produced.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the meeting. |
| 2 | Title | string |  | No | Meeting title; titles may repeat. |
| 3 | Description | string |  | No | Description of the meeting purpose and content. |
| 4 | Status | enum |  | No | Lifecycle status. Values: Draft, WaitingApproval, Rejected, Approved, Published, InProgress, Ended. |
| 5 | Sensitivity | enum |  | No | Confidentiality level of the meeting and its records. Values: Normal, Internal, Confidential, Secret. |
| 6 | Mode | enum |  | No | How the meeting is held. Values: InPerson, Online, Hybrid. |
| 7 | Location | string |  | No | Room name or address where the meeting is held in person. |
| 8 | OnlineLink | string |  | Yes | Link to the online meeting room; used for Online and Hybrid meetings. |
| 9 | PlannedStartTime | timestamptz |  | No | Planned start date and time. |
| 10 | PlannedEndTime | timestamptz |  | No | Planned end date and time; must be later than PlannedStartTime. |
| 11 | ActualStartedAt | timestamptz |  | Yes | Date and time the Chair actually started the meeting. |
| 12 | ActualEndedAt | timestamptz |  | Yes | Date and time the meeting actually ended. |
| 13 | CreatedById | uuid | FK | No | Meeting Organizer who created the meeting (references User). |
| 14 | AssignedApproverId | uuid | FK | Yes | Meeting Approver assigned to review the meeting in the current approval cycle (references User). |

Entity 6: MeetingStatusHistory

Append-only history of every status transition of a meeting: who changed it, when and why. Used to show the meeting timeline and as audit evidence.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the history record. |
| 2 | MeetingId | uuid | FK | No | Meeting whose status changed (references Meeting). |
| 3 | FromStatus | string |  | Yes | Status before the transition. Null for the record that represents creation of the meeting. |
| 4 | ToStatus | string |  | No | Status after the transition. |
| 5 | ActorId | uuid | FK | No | User who performed the transition (references User). |
| 6 | Reason | string |  | Yes | Reason given for the transition; required when a meeting is rejected. |
| 7 | OccurredAt | timestamptz |  | No | Date and time of the transition. |

Entity 7: MeetingParticipant

Internal user assigned to a meeting, together with the meeting role, the invitation response and the actual attendance. Removed participants are kept for history.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the participant record. |
| 2 | MeetingId | uuid | FK | No | Meeting the user is assigned to (references Meeting). |
| 3 | UserId | uuid | FK | No | Internal user assigned to the meeting (references User). |
| 4 | MeetingRole | enum |  | No | Role of the user in this meeting, independent of the system role. Values: Chair, Secretary, Member. |
| 5 | InvitationResponse | enum |  | No | Answer to the meeting invitation. Values: Pending, Accepted, Declined. |
| 6 | RespondedAt | timestamptz |  | Yes | Date and time the participant responded to the invitation. |
| 7 | Attendance | enum |  | Yes | Official attendance: Present or Absent. Empty until recorded by the assigned Secretary. Invitation response and room presence do not set attendance. |
| 8 | AssignmentStatus | enum |  | No | Whether the participant is still assigned to the meeting. Values: Current, Removed. |
| 9 | RemovedAt | timestamptz |  | Yes | Date and time the participant was removed from the meeting. |
| 10 | AttendanceRecordedById | uuid | FK | Yes | Secretary who saved the official attendance (references User). Empty until recorded. |
| 11 | AttendanceRecordedAt | timestamptz |  | Yes | Time official attendance was last saved. Empty until recorded. |

Entity 7a: MeetingParticipantPermission

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the additional meeting permission. |
| 2 | MeetingParticipantId | uuid | FK | No | Current internal participant assignment receiving the permission; the permission applies only to that assignment’s Meeting. |
| 3 | PermissionCode | string |  | No | Identifier from the system-defined Meeting permission catalogue. ViewParticipants and ViewResponses are examples, not the only possible additional permissions. The catalogue and grant matrix have not been finalized. |
| 4 | GrantedById | uuid | FK | No | Assigned Secretary who granted the additional permission (references User). |
| 5 | GrantedAt | timestamptz |  | No | Time the additional permission was granted. |

Entity 8: MeetingGuestInvitation

Invitation that lets an external Guest without an internal account join a meeting through a secure invitation link.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the guest invitation. |
| 2 | MeetingId | uuid | FK | No | Meeting the guest is invited to (references Meeting). |
| 3 | GuestName | string |  | No | Display name of the guest. |
| 4 | GuestEmail | citext |  | No | Email address the invitation is sent to; compared case-insensitively. |
| 5 | InvitationResponse | string |  | No | Guest's answer to the invitation. |
| 6 | RespondedAt | timestamptz |  | Yes | Date and time the guest responded. |
| 7 | InvitationStatus | string |  | No | Current state of the invitation link (for example active, expired or revoked). |
| 8 | TokenHash | string |  | No | Hash of the invitation token carried in the link; the plain token is never stored. |
| 9 | ExpiresAt | timestamptz |  | No | Date and time after which the invitation link can no longer be used. |
| 10 | VerifiedAt | timestamptz |  | Yes | Date and time the guest verified the invitation. |
| 11 | RevokedAt | timestamptz |  | Yes | Date and time the invitation was revoked. |
| 12 | AttendanceRecordedById | uuid | FK | Yes | Secretary who saved the official attendance (references User). Empty until recorded. |
| 13 | AttendanceRecordedAt | timestamptz |  | Yes | Time official attendance was last saved. Empty until recorded. |
| 14 | Attendance | enum |  | Yes | Official Guest attendance: Present or Absent. Empty until recorded. Retained with historical evidence after revocation. |

Entity 9: AgendaItem

One item on the agenda of a Meeting. Documents may be linked to multiple Items of the same Meeting through MeetingDocumentAgendaItem; notes and speaking requests may refer to an Item. Votes belong only to the Meeting.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the agenda item. |
| 2 | MeetingId | uuid | FK | No | Meeting the agenda item belongs to (references Meeting). |
| 3 | Position | int | UK | No | Order of the item on the agenda, starting at 1; unique within the meeting. |
| 4 | Title | string |  | No | Title of the agenda item. |
| 5 | Description | string |  | Yes | Detailed description of the agenda item. |
| 6 | PlannedDurationMinutes | int |  | No | Planned duration of the item in minutes. |
| 7 | PresenterId | uuid | FK | Yes | Internal user who presents the item (references User). Null when the presenter has no account. |
| 8 | PresenterName | string |  | No | Display name of the presenter, used when the presenter is not an internal user. |
| 9 | Status | enum |  | No | Progress of the item during the meeting. Values: Pending, InProgress, Done. |
| 10 | ActualStartedAt | timestamptz |  | Yes | Date and time the Chair opened the item. |
| 11 | ActualEndedAt | timestamptz |  | Yes | Date and time the item was completed. |

Entity 10: SpeakingRequest

Request from a participant to speak during the meeting, and how the Chair handled it. Kept after the meeting so the minutes can report who spoke and for how long.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the speaking request. |
| 2 | MeetingId | uuid | FK | No | Meeting in which the request was raised (references Meeting). |
| 3 | AgendaItemId | uuid | FK | Yes | Agenda item under discussion when the request was raised (references AgendaItem). |
| 4 | UserId | uuid | FK | No | Participant who asked to speak (references User). |
| 5 | RequestedAt | timestamptz |  | No | Date and time the request was raised. |
| 6 | QueuePosition | int |  | No | Position in the speaking queue; defaults to request order and may be reordered by the Chair. |
| 7 | Status | enum |  | No | Processing state of the request. Values: Queued, Invited, Speaking, Finished, Cancelled. |
| 8 | StartedAt | timestamptz |  | Yes | Date and time the participant started speaking. |
| 9 | EndedAt | timestamptz |  | Yes | Date and time the participant finished speaking. |

Entity 11: MeetingNote

Running notes taken during the meeting, either for the meeting as a whole or for one agenda item.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the note. |
| 2 | MeetingId | uuid | FK | No | Meeting the note belongs to (references Meeting). |
| 3 | AgendaItemId | uuid | FK | Yes | Agenda item the note refers to (references AgendaItem). Null for a note about the whole meeting. |
| 4 | AuthorId | uuid | FK | No | User who wrote the note (references User). |
| 5 | Body | string |  | No | Content of the note. |

Entity 12: CallSession

Online call: either a direct call between users or the online room of a meeting. Only call metadata is stored; audio and video are not stored.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the call. |
| 2 | Kind | enum |  | No | Type of call. Values: Direct, MeetingRoom. |
| 3 | MeetingId | uuid | FK | Yes | Meeting the room belongs to (references Meeting). Null for a direct call. |
| 4 | Status | enum |  | No | State of the call. Values: Ringing, Active, Ended, Missed. |
| 5 | StartedById | uuid | FK | Yes | User who started the call (references User). |
| 6 | StartedAt | timestamptz |  | No | Date and time the call started. |
| 7 | EndedAt | timestamptz |  | Yes | Date and time the call ended. |

Entity 13: CallParticipant

One connection of a user or guest to a call. Leaving and rejoining creates a new row, so the connected time of each person can be calculated.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the call participation. |
| 2 | CallSessionId | uuid | FK | No | Call the participant joined (references CallSession). |
| 3 | UserId | uuid | FK | Yes | Internal user who joined (references User). Null when the participant is a guest. |
| 4 | GuestInvitationId | uuid | FK | Yes | Guest invitation used to join (references MeetingGuestInvitation). Null when the participant is an internal user. |
| 5 | JoinedAt | timestamptz |  | No | Date and time the participant joined. |
| 6 | LeftAt | timestamptz |  | Yes | Date and time the participant left. |
| 7 | Declined | bool |  | No | True when the user declined a direct call instead of joining. |

Entity 14: VotingSession

A Vote belongs to one Meeting and is not linked to an Agenda Item. The current Chair prepares Draft Votes in Approved, Published or InProgress; opening and balloting require InProgress. Closing and recording a conclusion may finish in Ended. The fixed eligible-voter list is represented by VotingEligibleVoter; it is not reconstructed from current roles. No time-boxed voting or quorum exists.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the voting session. |
| 2 | MeetingId | uuid | FK | No | Meeting the vote belongs to (references Meeting). |
| 3 | Title | string |  | No | Question or title of the vote. |
| 4 | IsAnonymous | bool |  | No | True for a secret ballot: no record links a voter to the option chosen. |
| 5 | EligibleRoles | string[] |  | No | Meeting roles selected by the Chair: Chair, Secretary, Member and Guest; at least one. Used to establish the eligible-voter list when opening, not to recalculate that list later. |
| 6 | Status | enum |  | No | State of the vote. Values: Draft, Open, Closed. |
| 7 | OpenedAt | timestamptz |  | Yes | Date and time voting opened. |
| 8 | ClosedAt | timestamptz |  | Yes | Date and time voting closed. |
| 9 | EligibleCount | int |  | No | Number of VotingEligibleVoter entries fixed when opening succeeds. It does not change when roles, attendance or access later change. |
| 10 | Conclusion | enum |  | Yes | Conclusion recorded by the Chair after the vote closes. Values: Passed, NotPassed, Deferred. Null until concluded. |
| 11 | ConclusionNote | string |  | Yes | Explanatory note for the conclusion. |
| 12 | ConcludedById | uuid | FK | Yes | Chair who recorded the conclusion (references User). |

Entity 14a: VotingEligibleVoter

Fixed eligibility for one person in one Vote, created only when opening succeeds. Exactly one of VoterId and GuestInvitationId is set; each identity appears at most once per Vote. The Guest invitation must belong to the same Meeting. Entries remain unchanged after role changes, removal, expiry or revocation; current access is checked separately before accepting a ballot. This is the target logical relationship for the class diagram, not a claim that migrations already exist.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of this eligible-voter entry. |
| 2 | VotingSessionId | uuid | FK | No | Vote whose eligible-voter list contains this identity (references VotingSession). |
| 3 | VoterId | uuid | FK | Yes | Eligible internal User, if applicable; unique within this Vote. |
| 4 | GuestInvitationId | uuid | FK | Yes | Eligible Guest invitation identity, if applicable (references MeetingGuestInvitation); unique within this Vote. |

Entity 15: VotingOption

One answer that voters can choose in a voting session.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the option. |
| 2 | VotingSessionId | uuid | FK | No | Voting session the option belongs to (references VotingSession). |
| 3 | Position | int | UK | No | Display order of the option, starting at 1; unique within the voting session. |
| 4 | Label | string |  | No | Text of the option shown to voters. |

Entity 16: VoteReceipt

Records that one eligible internal User or Guest invitation identity has voted, never their chosen option. Exactly one of VoterId and GuestInvitationId is set. The identity must belong to the fixed eligible-voter list for this Vote, and may have only one receipt in it. Rejoining does not create another identity or ballot.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the receipt. |
| 2 | VotingSessionId | uuid | FK | No | Voting session voted in (references VotingSession). |
| 3 | VoterId | uuid | FK | Yes | Internal User who voted. Set only for an internal voter; exactly one of VoterId and GuestInvitationId is set. Unique per VotingSessionId and internal voter. |
| 4 | CastAt | timestamptz |  | No | Date and time the vote was cast. |
| 5 | GuestInvitationId | uuid | FK | Yes | Guest identity who voted (references MeetingGuestInvitation in the same Meeting). Set only for a Guest; unique per VotingSessionId and invitation. Retained after revocation or expiry. |

Entity 17: VoteChoice

The option chosen in a Vote. For a Public Vote, exactly one of VoterId and GuestInvitationId identifies the voter. For a Secret Vote, both are empty and no receipt or eligible-voter reference is stored with the choice. All recorded choices contribute to the tally.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the choice. |
| 2 | VotingSessionId | uuid | FK | No | Voting session the choice belongs to (references VotingSession). |
| 3 | VotingOptionId | uuid | FK | No | Option chosen (references VotingOption). |
| 4 | VoterId | uuid | FK | Yes | For a Public Vote, the internal User who chose this option, if applicable. Empty for every Secret Vote and for a Guest choice. |
| 5 | CastAt | timestamptz |  | No | Date and time the choice was cast. |
| 6 | GuestInvitationId | uuid | FK | Yes | For a Public Vote, the Guest invitation identity who chose this option (references MeetingGuestInvitation in the same Meeting), if applicable. Empty for every Secret Vote and for an internal choice. |

Entity 18: Document

Entry in a user's personal library. It refers to one immutable Stored File; documents have no versions, so changing a document means uploading a new file (ADR-0018).

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the document. |
| 2 | OwnerId | uuid | FK | No | User who owns the document (references User). |
| 3 | FolderId | uuid | FK | Yes | Folder holding the entry (references Folder). Null at the root of the library. |
| 4 | StoredFileId | uuid | FK | No | Stored file the entry refers to (references StoredFile). |
| 5 | FileName | string |  | No | File name shown in the library. |
| 6 | Category | enum |  | No | Category of the document. Values: Material, Draft, Administrative, Other. |

Entity 19: StoredFile

One uploaded file in object storage. It is immutable; personal library entries and Meeting documents only refer to it. A periodic cleanup deletes the object once nothing refers to it (ADR-0018).

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the stored file. |
| 2 | StorageKey | string |  | No | Key of the original file in object storage. |
| 3 | ContentType | string |  | No | File type detected from the file content (PDF or DOCX). The file is stored as uploaded; the browser renders the preview. |
| 4 | ByteSize | bigint |  | No | File size in bytes. |
| 5 | ContentSha256 | string |  | No | SHA-256 hash of the stored file, used to detect changes to the file. |
| 6 | PageCount | int |  | Yes | Number of pages of a PDF; null for a DOCX, which is previewed as one continuous document. |
| 7 | UploadedById | uuid | FK | No | User who uploaded the file (references User). |
| 8 | UploadedAt | timestamptz |  | No | Date and time the file was uploaded. |
| 9 | PurgedAt | timestamptz |  | Yes | Date and time the cleanup deleted the object; null while the file is stored. |

Entity 20: MeetingDocument

One addition of a stored file to a Meeting. A document may be linked to several Agenda Items through MeetingDocumentAgendaItem; with no links, it is a general Meeting document. Removing it clears its current Item links but preserves this addition and its comments as history. Readding the same file creates a new addition and does not restore the old comments. Deleting the personal-library entry does not remove this Meeting document.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the meeting document. |
| 2 | MeetingId | uuid | FK | No | Meeting the document is attached to (references Meeting). |
| 3 | StoredFileId | uuid | FK | No | Stored file shown in the meeting (references StoredFile). |
| 4 | DocumentId | uuid | FK | Yes | Library entry the file was added from (references Document). Null once that entry is deleted. |
| 5 | AddedById | uuid | FK | No | User who attached the document (references User). |
| 6 | AddedAt | timestamptz |  | No | Date and time the document was attached. |
| 7 | FileName | string |  | No | Document name copied when this addition is created. Later renaming in the personal library does not change it. |
| 8 | RemovedAt | timestamptz |  | Yes | Date and time the document was removed from the meeting (UC-33c); null while it is shown. |
| 9 | RemovedById | uuid | FK | Yes | User who removed the document (references User). |

Entity 20a: MeetingDocumentAgendaItem

Links an active MeetingDocument addition to one Agenda Item of the same Meeting. The pair is unique. A document may have several links. Removing one link or deleting its Item preserves the document and its other links. Removing the document from the Meeting clears all its current links. This is the target logical relationship for the class diagram.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | MeetingDocumentId | uuid | PK, FK | No | Meeting document addition (references MeetingDocument). |
| 2 | AgendaItemId | uuid | PK, FK | No | Linked Item (references AgendaItem); its Meeting must match the Meeting document. |

Entity 21: DocumentComment

Comment on one MeetingDocument addition, by an internal User or Guest invitation identity. Exactly one of AuthorId and GuestInvitationId is set. Comments are read-only after the Meeting ends. Removed additions retain their comments as history; a new addition of the same file does not inherit them. Existing reply references remain within the same addition.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the comment. |
| 2 | MeetingDocumentId | uuid | FK | No | Meeting document commented on (references MeetingDocument). |
| 3 | ParentCommentId | uuid | FK | Yes | Comment being replied to (references DocumentComment). Null for a top-level comment. |
| 4 | AuthorId | uuid | FK | Yes | Internal User who wrote the comment, if applicable. Exactly one of AuthorId and GuestInvitationId is set. |
| 5 | PageNumber | int |  | Yes | Page of the document the comment refers to, when known. |
| 6 | Body | string |  | No | Content of the comment. |
| 7 | IsResolved | bool |  | No | True when the comment has been resolved. |
| 8 | GuestInvitationId | uuid | FK | Yes | Guest author (references MeetingGuestInvitation for the same Meeting). Set only for Guest comments. Historical authorship is retained after expiry or revocation. |

Entity 22: Recording

Audio file of a meeting uploaded by a participant after the meeting; the system does not record meetings itself. Consent and retention fields support personal data obligations.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the recording. |
| 2 | MeetingId | uuid | FK | No | Meeting the recording belongs to (references Meeting). |
| 3 | UploadedById | uuid | FK | No | User who uploaded the file (references User). |
| 4 | Source | enum |  | No | How the recording entered the system. Values: Upload. |
| 5 | Status | enum |  | No | State of the recording file. Values: Pending, Available, Failed, Deleted. |
| 6 | StoragePath | string |  | No | Location of the audio file in storage. |
| 7 | DurationMs | bigint |  | No | Duration of the audio in milliseconds. |
| 8 | ConsentObtainedAt | timestamptz |  | No | Date and time participants' consent to the recording was confirmed. |
| 9 | ConsentPurpose | string |  | No | Stated purpose of the recording, as shown to participants. |
| 10 | RetainUntil | timestamptz |  | Yes | Date and time the file is due to be erased. Null when retention has not been decided. |
| 11 | DeletedAt | timestamptz |  | Yes | Date and time the audio file was erased; the row is kept as evidence. |

Entity 23: Transcript

Speech-to-text result with speaker separation for one recording. Processing runs in the background, so its state and error are stored.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the transcript. |
| 2 | RecordingId | uuid | FK, UK | No | Recording transcribed (references Recording); unique, one transcript per recording. |
| 3 | Status | enum |  | No | Processing state. Values: Pending, Processing, Completed, Failed. |
| 4 | Provider | string |  | No | Service or model that produced the transcript. |
| 5 | Language | string |  | No | Language of the transcript (Vietnamese in version 1). |
| 6 | ProgressPercent | int |  | No | Processing progress from 0 to 100. |
| 7 | ErrorMessage | string |  | Yes | Reason for failure, shown to the user with the option to retry. |

Entity 24: TranscriptSegment

One stretch of speech in a transcript, with timestamps and speaker label, in the form "Speaker A says / Speaker B says". No voiceprint is stored.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the segment. |
| 2 | TranscriptId | uuid | FK | No | Transcript the segment belongs to (references Transcript). |
| 3 | Position | int | UK | No | Order of the segment within the transcript; unique within the transcript. |
| 4 | StartMs | bigint |  | No | Start offset of the segment in the recording, in milliseconds. |
| 5 | EndMs | bigint |  | No | End offset of the segment in the recording, in milliseconds. |
| 6 | Text | string |  | No | Transcribed text of the segment. |
| 7 | SpeakerLabel | string |  | No | Speaker label assigned by speaker separation within this recording, for example Speaker 1. |
| 8 | SpeakerUserId | uuid | FK | Yes | Internal user mapped to the speaker label by hand (references User). |
| 9 | EditedById | uuid | FK | Yes | User who corrected the transcribed text (references User). |

Entity 25: MeetingSummary

AI-generated summary of a meeting, produced from the transcript after the meeting ends. The Secretary reviews and approves it before it is published or used for the minutes.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the summary. |
| 2 | MeetingId | uuid | FK, UK | No | Meeting summarized (references Meeting); unique, one summary per meeting. |
| 3 | Content | string |  | No | Content of the summary. |
| 4 | Model | string |  | No | AI model that generated the summary. |
| 5 | Status | enum |  | No | Review state of the summary. Values: Draft, Approved, Published. |
| 6 | GeneratedAt | timestamptz |  | No | Date and time the summary was generated. |
| 7 | ApprovedById | uuid | FK | Yes | Secretary who approved the summary (references User). |
| 8 | ApprovedAt | timestamptz |  | Yes | Date and time the summary was approved. |
| 9 | PublishedAt | timestamptz |  | Yes | Date and time the summary was published to participants. |

Entity 26: MeetingMinutes

Formal minutes of a meeting, compiled from the approved summary, decisions and vote results. Each correction creates a new revision; a sealed revision is locked and never edited.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the minutes revision. |
| 2 | MeetingId | uuid | FK | No | Meeting the minutes belong to (references Meeting). |
| 3 | RevisionNumber | int | UK | No | Revision number, starting at 1; unique within the meeting. |
| 4 | SupersedesId | uuid | FK | Yes | Previous revision that this revision replaces (references MeetingMinutes). Null for the first revision. |
| 5 | Content | string |  | No | Content of the minutes. |
| 6 | Status | enum |  | No | Signing state of the revision. Values: Draft, PendingSignature, RevisionRequested, Sealed, Superseded. |
| 7 | CreatedById | uuid | FK | No | User who prepared the revision (references User). |
| 8 | RevisionNote | string |  | Yes | Explanation of what was changed in this revision. |
| 9 | SealedById | uuid | FK | Yes | User who sealed the minutes (references User). |
| 10 | SealedAt | timestamptz |  | Yes | Date and time the minutes were sealed; once set, the revision is locked. |

Entity 27: MinutesSignature

Digital signature of one signer on a minutes revision. The stored hash proves the content has not changed since signing.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the signature. |
| 2 | MinutesId | uuid | FK | No | Minutes revision signed (references MeetingMinutes). |
| 3 | SignerId | uuid | FK | No | User who signed (references User). |
| 4 | SignedAt | timestamptz |  | No | Date and time of signing. |
| 5 | ContentSha256 | string |  | No | SHA-256 hash of the minutes content at signing time. |
| 6 | Signature | string |  | No | Signature value. |
| 7 | PublicKey | string |  | No | Public key of the signer, used to verify the signature. |
| 8 | InvalidatedAt | timestamptz |  | Yes | Date and time the signature became invalid, for example when a revision is requested. |

Entity 28: Notification

In-app notification delivered to one user; opening it navigates to the related resource.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the notification. |
| 2 | UserId | uuid | FK | No | Recipient of the notification (references User). |
| 3 | MeetingId | uuid | FK | Yes | Meeting the notification is about (references Meeting). Null when not related to a meeting. |
| 4 | TargetType | string |  | No | Type of the resource opened from the notification. |
| 5 | TargetId | uuid |  | No | Identifier of the resource opened from the notification. |
| 6 | ReadAt | timestamptz |  | Yes | Date and time the notification was read. Null while unread. |

Entity 29: EmailMessage

Outgoing email kept in a queue so it can be sent in the background, retried on failure and traced later.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the email message. |
| 2 | MeetingId | uuid | FK | Yes | Meeting that caused the email (references Meeting). Null when not related to a meeting. |
| 3 | DeduplicationKey | string | UK | No | Key of the business event that created the message; unique, so the same event never sends twice. |
| 4 | Status | string |  | No | Delivery state of the message (for example queued, processing, sent or failed). |
| 5 | RetryCount | int |  | No | Number of send attempts made. |
| 6 | CreatedAt | timestamptz |  | No | Date and time the message was queued. |

Entity 30: Folder

Folder in a user’s personal library. Folders nest at most five levels; a folder created for a meeting upload keeps a link to that Meeting so later uploads find it after a rename or move (ADR-0018).

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the folder. |
| 2 | OwnerId | uuid | FK | No | User who owns the folder (references User). |
| 3 | ParentId | uuid | FK | Yes | Folder that contains this folder (references Folder). Null at the root of the library. |
| 4 | Name | string |  | No | Folder name; must not be empty and need not be unique. |
| 5 | MeetingId | uuid | FK | Yes | Meeting this folder was created for when a file was uploaded straight into it (references Meeting); null for folders the user created. |
| 6 | CreatedAt | timestamptz |  | No | Date and time the folder was created. |

#### 3.3 Data Business Rules

| Rule | Description | Enforced On |
| --- | --- | --- |
| Unique username | A username identifies at most one internal user account. | Account creation/change |
| Unique email | An internal email address identifies at most one account. | Account creation/change |
| Meeting schedule | Each Meeting has planned start and end times; planned end follows planned start. | Meeting creation/update |
| Single meeting lifecycle | A Meeting has exactly one lifecycle. No Meeting Session entity or nested session lifecycle exists. | Meeting lifecycle |
| Agenda ownership | Each Agenda Item belongs to exactly one Meeting. Reordering is within that Meeting. | Agenda management |
| Vote ownership | Each Vote belongs to one Meeting and is not linked to an Agenda Item. | Voting management |
| Vote conclusion ownership | A Vote belongs to one Meeting, whose Minutes incorporate vote tallies and the Chair’s conclusions. | Vote Conclusion creation |
| Minutes scope | Each Meeting has its own Minutes process with versioned signing and revision history. | Minutes governance |
| Meeting audit trail | Meeting lifecycle, approval, publication and permitted schedule changes remain traceable. | Meeting audit |

### 4. User Requirements

#### 4.1 Actors and External Systems

| # | Actor | Description |
| --- | --- | --- |
| 1 | Guest | An external person who does not have an internal user account. A Guest is invited to a specific meeting by name and email and accesses only that meeting through a secure meeting-specific guest invitation and email verification, without signing in to an internal account. Guest access is limited to permitted meeting functions. |
| 2 | Meeting Organizer | A system-authorized user responsible for creating, updating, submitting, organizing, and publishing meetings under their management. |
| 3 | Meeting Approver | A system-authorized user who may be assigned to review a specific meeting approval request and decide whether to approve or reject it. An approval permission alone does not grant access to all meetings. |
| 4 | System Admin | An administrator responsible for managing user accounts, roles, permissions and system-wide configuration, including viewing relevant business and security audit events. |
| 5 | Chair | A user assigned to chair a meeting, start/end it, control agenda execution, open/close Votes and record their conclusions, and moderate authorized meeting-room interactions. |
| 6 | Secretary | A user assigned to support a specific meeting by preparing agenda items, managing the meeting roster and official attendance, managing authorized meeting documents, uploading the post-meeting audio, reviewing transcripts and summaries, and preparing/finalizing meeting minutes. |
| 7 | Participant | An internal user assigned to participate in a specific meeting. A Participant may perform authorized meeting activities such as joining the online meeting room, viewing or commenting on meeting documents, responding to invitations, and voting. Chair and Secretary are meeting-specific roles with additional responsibilities. |
| 8 | Google Identity Provider | An external identity provider used for Google-based authentication and verified identity data exchange. It participates primarily in UC-02 Sign in with Google and supports the authentication flow shown in the context diagram. |
| 9 | AI / Speech Processing Provider | An external processing provider that receives approved Meeting audio and returns speaker-attributed transcript data and draft summaries. It supports transcript and summary processing such as UC-62 Upload Meeting Audio, UC-64 View Speaker-Attributed Transcript, UC-65 Edit Transcript & Speaker Attribution, and UC-90 Generate / Regenerate Meeting Summary. |
| 10 | Email / SMTP Service | An external outbound email service used to deliver password-reset messages, meeting invitations, guest-access emails, notifications, and minutes-signing related emails. It supports flows such as UC-07 Reset Password, UC-21 Publish Meeting, UC-80 Submit Meeting Minutes for Signing, and notification delivery under FE-13. |

#### 4.2 Use Cases (UC)

| ID | Use Case | Feature | Use Case Description |
| --- | --- | --- | --- |
| UC-01 | Sign in with Email and Password | Authentication & Account Security | Authenticate users using their registered email and password. |
| UC-02 | Sign in with Google | Authentication & Account Security | Authenticate users using their authorized Google account. |
| UC-03 | Sign Out | Authentication & Account Security | End the user's current authenticated session. |
| UC-04 | Access Meeting via Guest Invitation | Authentication & Account Security | Allows an invited Guest to verify control of the invited email and enter self-declared display information for the invited Meeting. |
| UC-05 | View User Accounts | User & Profile Management | View, search, and filter user accounts in the system. |
| UC-06 | Manage User Accounts | User & Profile Management | Create, update, deactivate, or reactivate user accounts; the operation is specified in UC-06a–UC-06c. |
| UC-07 | Reset User Password | User & Profile Management | Initiate a password reset for an eligible user account. |
| UC-08 | Assign System Role to User | User & Profile Management | Assign or change the system role of a user account. |
| UC-09 | Unlock User Account | User & Profile Management | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-10 | View Role & Permission Matrix | Role-Based Access Control | View system roles and their assigned permissions. |
| UC-11 | Configure Role Permissions | Role-Based Access Control | Configure permissions assigned to a system role. |
| UC-12 | Manage Roles | Role-Based Access Control | Create, update, or delete custom roles; see the detailed UC-12a–UC-12c specifications. |
| UC-13 | View Audit Log | Role-Based Access Control | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-14 | Create Meeting | Meeting Management | Create one independent Meeting with its planned start and end times, mode, location, confidentiality and other required details. |
| UC-15 | Update Meeting | Meeting Management | Update Meeting information while Draft/Rejected, or reschedule a Published Meeting before it begins when authorized. |
| UC-16 | View Meeting Details | Meeting Management | View authorized Meeting details, including planned schedule, actual start/end times, Meeting lifecycle status and, for a Cancelled Meeting, its cancellation reason. |
| UC-17 | View Meeting List | Meeting Management | View, search and filter Meetings by their own schedule, status (including Cancelled), name and confidentiality level. |
| UC-18 | Submit Meeting for Approval | Meeting Management | Submit a Draft or Rejected Meeting with a valid planned schedule for approval. |
| UC-19 | Review Meeting Approval | Meeting Management | Approve or reject an assigned meeting approval request. |
| UC-20 | Publish Meeting | Meeting Management | Publish an Approved Meeting with valid planned time and required Chair/Secretary assignments, and initiate invitations. |
| UC-21 | Start Meeting | Meeting Management | Start a Published Meeting and record actual start time. |
| UC-22 | End Meeting | Meeting Management | End an InProgress Meeting and record actual end time. |
| UC-23 | Manage Agenda Items | Agenda Management | Manage Agenda planning as Secretary: add Pending Items at the end; edit Title, Presenter and Description in any status; delete only Pending Items; reorder Items in any status. The Presenter is a current participant or Guest and is notified once the Meeting is published. |
| UC-24 | Set Current Agenda Item | Agenda Management | Chair starts any Pending Item as the current Item when no Item is InProgress. |
| UC-25 | Complete Agenda Item | Agenda Management | Chair completes the current Item at any time, records its actual end time and does not automatically start the next Item. |
| UC-26 | View Meeting Agenda | Agenda Management | Authorized viewers see the complete Agenda and automatically receive its successfully saved changes. |
| UC-27 | View Meeting Participants | Meeting Participant Management | View the current internal participants and external Guests associated with a meeting, together with their meeting-specific role or guest access type. |
| UC-28 | Manage Meeting Participants | Meeting Participant Management | Add participants, manage meeting roles and additional permissions, replace the Chair before publication, or remove participants; see UC-28a–UC-28c. From publication onward the Chair hands over the role through UC-73. |
| UC-29 | Respond to Meeting Invitation | Meeting Participant Management | Allow an invited internal participant or external Guest to accept or decline their own Meeting invitation, and change a previous response while responses are allowed. |
| UC-30 | View Participant Response Status | Meeting Participant Management | View the latest invitation response of current internal participants and external Guests and a response-status summary. |
| UC-31 | Record Attendance | Meeting Participant Management | Record or revise Present/Absent attendance for one or more selected current internal participants or external Guests while the Meeting is InProgress. |
| UC-32 | View Meeting Documents | Personal Document Library & Meeting Document Management | View Meeting documents through UC-32a and preview or download through UC-32c. Anyone who can view the Meeting can read its documents; Secret Meeting documents cannot be downloaded. UC-32b remains retired. |
| UC-33 | Manage Meeting Documents | Personal Document Library & Meeting Document Management | Manage Meeting documents through UC-33a Add one or more documents, UC-33b Attach documents to an Agenda Item, UC-33c Remove from Meeting and UC-33d Detach from one Item. Current Chair or Secretary acts before Meeting end; documents are attached from the Meeting, the computer or My Documents, and a multiple-document submission succeeds as a whole or changes nothing. Item status does not lock links. |
| UC-34 | View Personal Document Library | Personal Document Library & Meeting Document Management | Browse the user's personal document library, search or filter documents and view permitted Meeting-usage details. |
| UC-35 | Comment on Meeting Document | Personal Document Library & Meeting Document Management | Read and add Meeting-document comments with the same viewing access, before Meeting end. After end, documents and comments are read-only. |
| UC-36 | Upload Document to Personal Library | Personal Document Library & Meeting Document Management | Upload one or more PDF or DOCX files together to the user's personal library, each at most 50 MB. The whole submission succeeds or no documents are added. |
| UC-37 | Join Meeting Room | Online Meeting Room Management | Join the online room of an authorized Online or Hybrid meeting; an external Guest may join through a verified meeting-specific guest invitation without an internal account login. |
| UC-38 | Leave Meeting Room | Online Meeting Room Management | Leave the online meeting room without changing the Meeting lifecycle. |
| UC-39 | Manage Meeting Room Controls | Online Meeting Room Management | Control microphone, camera, and screen sharing within the Meeting Room; see UC-39a–UC-39c. |
| UC-40 | View Meeting Room Participants | Online Meeting Room Management | View the users currently connected to the online meeting room and their current room presence. |
| UC-41 | Moderate Meeting Room | Online Meeting Room Management | Allow the assigned Chair to perform permitted moderation actions on users currently in the meeting room. |
| UC-42 | Use In-room Chat | Online Meeting Room Management | Exchange text messages with authorized users currently connected to the online meeting room. |
| UC-43 | View & Play Meeting Audio | Meeting Audio Upload & Media Library | View the Meeting media library and play Meeting audio; see UC-43a–UC-43b. |
| UC-44 | Upload Meeting Audio | Meeting Audio Upload & Media Library | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-45 | View Speaker-Attributed Transcript | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-46 | Edit Transcript & Speaker Attribution | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-47 | Generate / Regenerate Meeting Summary | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-48 | View Meeting Summary | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-49 | Edit Meeting Summary | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-50 | Approve Meeting Summary | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-51 | Publish Meeting Summary | Speaker-Attributed Transcription & Meeting Summary | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-52 | View Meeting Votes | Voting Management | View visible Meeting Votes and their status or result through UC-52a and UC-52b. |
| UC-53 | Create / Configure Vote | Voting Management | Allows the current Chair or Secretary to prepare a Vote draft and configure or delete an existing draft; only the Chair may proceed to opening the Vote. Draft preparation is allowed before the Meeting starts, while it is Approved or Published, and during InProgress. Each Vote belongs directly to its Meeting; its documents are managed through UC-76. |
| UC-54 | Open Vote | Voting Management | Allows the current Chair to open a valid Vote during an InProgress Meeting. Opening fixes the eligible-voter list and count. Only one Vote may be Open in a Meeting at a time. |
| UC-55 | Cast Vote | Voting Management | Allows an eligible person to submit one choice in an Open Vote during the Meeting, after confirming reading of every Vote document. Eligibility comes from the list fixed at opening and requires current Meeting access. Attendance is not a condition. A saved ballot cannot be changed or withdrawn. |
| UC-56 | Close Vote | Voting Management | Allows the current Chair to close an existing Open Vote and obtain its tally while the Meeting is InProgress or Ended. Closing a Vote does not end the Meeting and does not determine the Chair's conclusion. |
| UC-57 | Record Vote Conclusion | Voting Management | Allows the current Chair to record the business conclusion of a Closed and tallied Vote as Passed, NotPassed or Deferred, with an optional note. The Chair decides the conclusion; the system records it rather than inferring it from a majority. |
| UC-58 | View Meeting Minutes | Meeting Minutes & Digital Signing | View the current Meeting Minutes and permitted signing status. |
| UC-59 | Generate Meeting Minutes | Meeting Minutes & Digital Signing | Generate draft Meeting Minutes from the approved summary, attendance, agenda and vote results with conclusions recorded by the Chair. |
| UC-60 | Edit Meeting Minutes | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-61 | Submit Meeting Minutes for Signing | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-62 | Sign Meeting Minutes | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-63 | View Signature Status & Audit Trail | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-64 | Finalize Meeting Minutes | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-65 | Request Revision of Meeting Minutes | Meeting Minutes & Digital Signing | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-66 | View Notifications | Email & In-App Notification | View personal notifications and the unread notification count. |
| UC-67 | Mark Notifications as Read | Email & In-App Notification | Mark one notification or all unread notifications as read; see UC-67a–UC-67b. |
| UC-68 | Open Notification Target | Email & In-App Notification | Open the meeting linked to a selected notification. |
| UC-69 | View Personal Dashboard & Meeting Workspace | Personal Dashboard & Meeting Workspace | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-70 | View & Export Reports | Reporting & Statistics | View reporting statistics and export the corresponding report; see UC-70a–UC-70b. |
| UC-71 | Manage Personal Documents | Personal Document Library & Meeting Document Management | Delete, rename or move personal-library documents through UC-71a, UC-71b and UC-71c, preserving Meeting documents already added. |
| UC-72 | Manage Library Folders | Personal Document Library & Meeting Document Management | Create, rename, move or delete personal-library folders through UC-72a to UC-72d; only empty folders may be deleted. |
| UC-73 | Hand Over Chair to Another Participant | Meeting Participant Management | The current Chair gives the Chair role to another internal participant in one step; the former Chair becomes a Member. |
| UC-74 | Confirm Document Read | Personal Document Library & Meeting Document Management | A person who may vote confirms reading a Vote document after scrolling to its last page; required before casting a ballot. |
| UC-75 | Cancel Meeting | Meeting Management | The Meeting Organizer cancels a Meeting that will no longer take place, before it starts, with a required reason; invitees of a Published Meeting are notified. |
| UC-76 | Manage Vote Documents | Voting Management | Attach documents to, or detach them from, a Draft Vote through UC-76a and UC-76b; documents come from the Meeting, the computer or My Documents. |
| UC-77 | Record Agenda Item Conclusion | Agenda Management | The current Chair or Secretary records or updates what was concluded for an InProgress or Done Agenda Item, until the minutes draft is generated; minutes include each Item conclusion. |

#### 4.3 Use Case Diagrams

##### 4.3.1 UCs for System Admin

The System Admin manages user accounts (create, update, deactivate/reactivate, reset password, unlock, assign system roles), configures roles and permissions, and reviews the audit log. The Admin can also view meetings and their records, reports and statistics, notifications, the personal dashboard and the personal document library.

##### 4.3.2 UCs for Meeting Organizer

The Meeting Organizer creates and updates meetings, submits them for approval and publishes approved meetings, adds participants and Guests. The Organizer can also view meeting records, comment on documents, use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace.

##### 4.3.3 UCs for Meeting Approver

The Meeting Approver reviews assigned meeting approval requests and approves or rejects them. The Approver can view meeting details and related records (agenda, documents, media, votes, minutes and signature status), use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace.

##### 4.3.4 UCs for Chair

The Chair responds to the invitation, starts and ends the meeting, steers the agenda (set current / complete item), joins and moderates the online room, and creates, opens and closes votes and records each vote's conclusion. The Chair also adds and removes meeting documents, comments on meeting documents, signs or requests revision of the minutes, and can view all records of the chaired meeting. The current assigned Chair may cast a vote (UC-55) while the Meeting is InProgress and the Vote is Open, under BR-160 and BR-165.

##### 4.3.5 UCs for Secretary

The Secretary prepares the meeting: agenda items, participants and meeting roles, invitation responses, attendance and meeting documents, and also responds to the invitation and takes part in the online room. After the meeting, the Secretary uploads audio, corrects the AI speaker-attributed transcript, generates, edits, approves and publishes the summary, and generates, edits, submits and finalizes the minutes for digital signing, signing or requesting revision when designated as a signer. The current assigned Secretary may cast a vote (UC-55) while the Meeting is InProgress and the Vote is Open, under BR-160 and BR-165.

##### 4.3.6 UCs for Participant

The Participant responds to the meeting invitation, joins the online room, views meetings, agenda, documents and media, comments on documents, casts votes when eligible, and signs or requests revision of the minutes when designated as a signer. The Participant can also view reports, notifications and the personal workspace.

##### 4.3.7 UCs for Guest

The Guest verifies the invited email by OTP, then supplies a self-declared display name and optional job title. In the invited Meeting, the Guest participates like a Member: room media/chat/list, all attached documents and comments, invitation response, voting and attendance inclusion, plus separately granted roster/response viewing. The Guest cannot be Chair/Secretary, receive delegated authority, moderate the room or use a personal library. (BR-180, BR-181)

#### 4.4 Permission Matrix

Full = unrestricted  ·  Restricted = conditions apply (see footnotes)  ·  No = denied

| Entity | Action | System Admin | Meeting Organizer | Meeting Approver | Chair | Secretary | Participant | Guest |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| User account | View / search | Full | No | No | No | No | No | No |
| User account | Create | Full | No | No | No | No | No | No |
| User account | Update | Full | No | No | No | No | No | No |
| User account | Deactivate / Reactivate | Full | No | No | No | No | No | No |
| User account | Reset password | Full | No | No | No | No | No | No |
| User account | Unlock | Full | No | No | No | No | No | No |
| User account | Assign system role | Full | No | No | No | No | No | No |
| Role & permission | View matrix | Full | No | No | No | No | No | No |
| Role & permission | Configure permissions | Full | No | No | No | No | No | No |
| Role & permission | Create role | Full | No | No | No | No | No | No |
| Role & permission | Update role | Restricted ¹ | No | No | No | No | No | No |
| Role & permission | Delete role | Restricted ¹ | No | No | No | No | No | No |
| Audit log | View | Full | No | No | No | No | No | No |
| Authentication | Sign in (email / Google) / Sign out | Full | Full | Full | Full | Full | Full | No |
| Authentication | Access meeting via Guest invitation | No | No | No | No | No | No | Full |
| Meeting | Create | No | Full | No | No | No | No | No |
| Meeting | Update / Reschedule | No | Restricted ² | No | No | No | No | No |
| Meeting | View list | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Meeting | View details | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Meeting | Submit for approval | No | Restricted ² | No | No | No | No | No |
| Meeting | Approve / Reject | No | No | Restricted ⁴ | No | No | No | No |
| Meeting | Publish | No | Restricted ⁵ | No | No | No | No | No |
| Meeting | Start / End | No | No | No | Restricted ⁶ | No | No | No |
| Agenda item | View | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Agenda item | Create / Update / Delete / Reorder | No | No | No | No | Restricted ⁸ | No | No |
| Agenda item | Set current / Complete | No | No | No | Restricted ⁹ | No | No | No |
| Participant | View participants / response status | No | No | No | Full from Published onward | Restricted ³: roster under existing rules; responses from Published onward | Restricted ³: from Published, each view requires its matching additional permission | Restricted ¹¹: from Published, each view requires its matching additional permission |
| Participant | Add participant / Invite Guest | No | Restricted ³ | No | No | Restricted ³ | No | No |
| Participant | Manage meeting role & additional permissions | No | No | No | No | Restricted ³ | No | No |
| Participant | Remove participant / Revoke Guest | No | No | No | No | Restricted ³ | No | No |
| Invitation response | Respond (accept / decline) | No | No | No | Restricted ¹⁰ | Restricted ¹⁰ | Restricted ¹⁰ | Restricted ¹¹ |
| Attendance | Record | No | No | No | No | Restricted ³ (InProgress only) | No | No |
| Personal document library | View | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Personal document library | Upload / Organize (folders, rename, move, delete) | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Meeting document | View list | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹³ |
| Meeting document | Add / Remove document; Link / Unlink Agenda Item | No | No | No | Restricted ³: current Chair, before Meeting end | Restricted ³: current Secretary, before Meeting end | No | No |
| Meeting document | Preview / Download (no download in a Secret Meeting) | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Meeting document | Comment before Meeting end | Full: only when the document is readable | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Audio recording | View media library / Play | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Audio recording | Upload | No | No | No | No | Restricted ³ | No | No |
| Transcript | View / Edit (speaker attribution) | No | No | No | No | Restricted ³ | No | No |
| Summary | Generate / Regenerate | No | No | No | No | Restricted ³ | No | No |
| Summary | View / Edit | No | No | No | No | Restricted ³ | No | No |
| Summary | Approve / Publish | No | No | No | No | Restricted ³ | No | No |
| Vote | Create / Configure / Delete Draft | No | No | No | Restricted: current Chair; Approved, Published or InProgress | No | No | No |
| Vote | Open / Close | No | No | No | Restricted: current Chair; open in InProgress, close in InProgress or Ended | No | No | No |
| Vote | Cast (in-meeting) | No | No | No | Restricted ¹⁴ | Restricted ¹⁴ | Restricted ¹⁴ | Restricted ¹⁴: verified current Guest |
| Vote | View votes / status | Full: Open/Closed only | Restricted ³: Open/Closed only | Restricted ³: Open/Closed only | Restricted ³; current Chair may also view Drafts in Approved | Restricted ³: Open/Closed only | Restricted ³: Open/Closed only | Restricted ¹¹: Open/Closed only |
| Vote | Record vote conclusion | No | No | No | Restricted: current Chair; InProgress or Ended; Closed Vote without a conclusion | No | No | No |
| Vote | View vote conclusion | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Minutes | View | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Minutes | Generate / Edit | No | No | No | No | Restricted ¹⁵ | No | No |
| Minutes | Submit for signing | No | No | No | No | Restricted ¹⁵ | No | No |
| Minutes | Sign | No | No | No | Restricted ¹⁶ | Restricted ¹⁶ | Restricted ¹⁶ | No |
| Minutes | Request revision | No | No | No | Restricted ¹⁶ | Restricted ¹⁶ | Restricted ¹⁶ | No |
| Minutes | View signature status & audit trail | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Minutes | Finalize / Seal | No | No | No | No | Restricted ¹⁷ | No | No |
| Notification | View | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Notification | Mark as read / Mark all / Open target | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Dashboard | View personal dashboard & workspace | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Report & statistics | View | Full | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | No |
| Report & statistics | Export | Full | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | No |
| Online meeting room | Join | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Leave | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Microphone / Camera / Screen sharing | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | View room participants / In-room chat | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Moderate | No | No | No | Restricted ¹⁹ | No | No | No |

Additional Meeting permissions supplement the permissions provided by an existing Meeting role. ViewParticipants and ViewResponses are examples, not a closed set. The final permission catalogue and grant matrix remain to be specified. Guest grants follow the same principle subject to the approved Guest exclusions. (BR-174, BR-175, BR-181)

Notes:

¹ Only custom roles; built-in system roles cannot be modified or deleted, and a role can be deleted only when no user is assigned to it.

² Only meetings created by the Meeting Organizer. Submit for approval and general information edits are permitted only in Draft or Rejected. A Published Meeting may be rescheduled before it starts under UC-15 and BR-158; only planned times, location and optional Online Link may change, and the new planned start must be later than the current time.

³ Only Meetings within the user’s authorized relationship and lifecycle scope: Organizer at any status; assigned Approver from WaitingApproval onward; active assigned Secretary before and after publication (BR-118); Chair or Participant from Published onward. Each action still requires its own permission and status checks. Guests do not use the internal Meeting list.

⁴ Only approval requests assigned to the user while the meeting is Pending Approval.

⁵ Only meetings the user created that have been Approved.

⁶ Only in the meeting the user chairs and only when the lifecycle state allows it (start a Published meeting and end an InProgress meeting).

⁸ Only the assigned Secretary may create, update, delete or reorder Agenda Items while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Agenda management is prohibited in Ended and all subsequent lifecycle states. This permission does not grant Chair-only agenda execution actions.

⁹ Only the active assigned Chair may set or complete the current Agenda Item while the Meeting is InProgress; at most one Item may be InProgress.

¹⁰ Only the user's own invitation, while the meeting is still open for responses.

¹¹ Only within a valid meeting-scoped Guest session, established by email-code verification (UC-04), for the meeting the Guest was invited to.

¹² Only the user's own items (own notifications, own personal documents, own dashboard).

¹³ Anyone who can view a Meeting can read all its current documents; there is no separate document-reading permission or publication gate. Guests still require a valid verified invitation for that Meeting. Secret Meeting documents cannot be downloaded.

¹⁴ Only people on the eligible list fixed when the Vote opens and retaining current Meeting access may cast a ballot, and only while the Meeting is InProgress and the Vote is Open. A role change does not change that list. Guest email verification and invitation validity are required. (BR-165, BR-190, BR-268, BR-269)

¹⁵ Only in the assigned meeting while the minutes are in Draft or Revision Requested status.

¹⁶ Only users designated as signers of the minutes, and only while the minutes are pending signature.

¹⁷ Only after all designated signers have signed the minutes.

¹⁸ Statistics and exports are limited to meetings within the user's own scope; System Admin sees system-wide data.

¹⁹ Join requires a valid current participant assignment for an Online or Hybrid Meeting whose lifecycle permits room entry (UC-37). Organizer or Approver status alone does not grant room entry. Leave, controls, room participants and chat require a connected authorized user.

### 5. System Functionalities

#### 5.1 Screen Inventory

##### a. Screen Flow

##### b. Screen List

| # | Screen Name | Feature | Description |
| --- | --- | --- | --- |
| 1.1 | Sign In | FE-01 Authentication & Account Security | Allows every internal user (System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to sign in with email and password or Google, see the redirect reason and sign-in errors, and sign out. |
| 1.2 | Reset Password | FE-01 Authentication & Account Security | Allows the account owner who opened the one-time, 60-minute reset link emailed by the System Admin to set a new password, which signs out all devices; invalid links show an error page. |
| 1.3 | Guest Verification | FE-01 Authentication & Account Security | Allows a Guest to verify the invited email with a one-time code, then enter a self-declared display name and optional job title before using Member participation functions in this Meeting. (UC-04, BR-180, BR-181) |
| 2.1 | User & Permission List | FE-02 User & Profile Management | Allows the System Admin to search and filter user accounts by role and status, create accounts, and edit, deactivate/reactivate or send a password reset link to each account. |
| 2.2 | Create / Edit User Account Dialog | FE-02 User & Profile Management | Allows the System Admin to create a new account with full name, email, a generated initial password and system role, or edit an existing account’s full name, email and system role. |
| 2.3 | Send Password Reset Link Dialog | FE-02 User & Profile Management | Allows the System Admin to confirm sending a one-time, 60-minute password reset link to a user’s registered email; the same dialog style confirms deactivating an account. |
| 3.1 | Role List | FE-03 Role-Based Access Control | Allows the System Admin to view roles with their account counts and to create, edit and delete custom roles (name, description, initial permissions), with Default roles protected from change. |
| 3.2 | Permission Matrix | FE-03 Role-Based Access Control | Allows the System Admin to view and grant or revoke permissions for each role in a role-by-permission matrix and to save or undo the changes row by row. |
| 3.3 | System Audit Log | FE-03 Role-Based Access Control | Allows the System Admin to view, filter, search and export to CSV the read-only log of business and security events, and open the before/after details of each change. |
| 4.1 | Meeting Calendar – Calendar View | FE-04 Meeting Management | Allows any internal user related to a meeting to browse accessible Meetings on a weekly day/hour grid, placed by their planned start and end time and coloured by status, and open one. |
| 4.2 | Meeting Calendar – List View | FE-04 Meeting Management | Allows any internal user related to a meeting to view the accessible Meetings of a week as cards grouped by day, with planned time, mode, location, status and confidentiality level, and open one. |
| 4.3 | Meeting List | FE-04 Meeting Management | Allows any internal user related to a meeting to view, search, filter by status and month, and page through the Meetings s/he organizes, is invited to or must approve, and open a Meeting. |
| 4.4 | Create / Edit Meeting | FE-04 Meeting Management | Allows the Meeting Organizer to create a Draft Meeting with its name, description, planned date and start/end time, mode, confidentiality level, location and optional online link, or edit a Draft or Rejected Meeting. |
| 4.5 | Meeting Detail – General Information | FE-04 Meeting Management | Allows any internal user related to a meeting to view its schedule, mode, location, online link, confidentiality, organizer, approver, description, lifecycle progress, readiness checklist and status history, and run the status-dependent actions. |
| 4.6 | Submit Meeting for Approval | FE-04 Meeting Management | Allows the Meeting Organizer to submit a complete Draft Meeting, or resubmit a Rejected one, by choosing an eligible Meeting Approver and adding an optional message, after which the Meeting is locked. |
| 4.7 | Approve / Reject Meeting | FE-04 Meeting Management | Allows the assigned Meeting Approver to review a Pending Approval Meeting read-only and approve it, or reject it with a mandatory reason that is sent to the Meeting Organizer. |
| 4.8 | Publish Meeting | FE-04 Meeting Management | Allows the Meeting Organizer to publish an Approved Meeting after checking that an active Chair and Secretary are assigned, which sends invitations to internal participants and external Guests. |
| 4.9 | Reschedule Meeting | FE-04 Meeting Management | Allows the Meeting Organizer to reschedule a Published Meeting that has not started by changing only its date, time, location and online link, and notify all invitees; the Chair can start the Meeting here. |
| 5.1 | Meeting Detail – Meeting Content Tab | FE-05 Agenda Management | Allows users related to a meeting to view its agenda items with presenter, duration and planned time, and the Secretary to add, edit, delete and reorder items while the meeting is Draft or Rejected. |
| 5.2 | Add / Edit Agenda Item Drawer | FE-05 Agenda Management | Allows the Secretary to add or edit an agenda item of a Draft or Rejected meeting with content, presenter, duration, note, a needs-vote flag and documents chosen from the meeting documents. |
| 5.3 | Meeting Facilitation | FE-05 Agenda Management | Allows the Chair to run an InProgress meeting: set the current agenda item, complete it with actual times recorded, create a vote for the item, join the online room and end the meeting. |
| 6.1 | Meeting Detail – Participants Tab | FE-06 Meeting Participant Management | Allows the Secretary to manage the roster, roles and permissions; the Chair and authorized Members have read access under BR-176. |
| 6.2 | Add Participant Dialog | FE-06 Meeting Participant Management | Allows the Secretary to add internal participants together with a shared Meeting Role and optional permissions, or invite one Guest; the Organizer may assign the missing Secretary. |
| 6.3 | Attendance Check | FE-06 Meeting Participant Management | Allows the Secretary to record Present or Absent for one or more selected current internal participants or Guests during an InProgress Meeting. |
| 6.4 | Invitation Response | FE-06 Meeting Participant Management | Allows an invited Chair, Secretary, Participant or verified Guest to view the invitation and accept or decline it, and to change the response until the meeting ends. |
| 7.1 | Document Library – Empty State | FE-07 Personal Document Library & Meeting Document Management | Shows the Document Library of a user who has no folders or files yet: an empty-state panel explaining how the library is used and the accepted file types, with New folder and Upload buttons at the top of the page and inside the panel. |
| 7.2 | Personal Document Library | FE-07 Personal Document Library & Meeting Document Management | Allows an internal user to browse folders, search and filter his/her private documents by file type, upload files, organise files and folders, delete files and see where each document is used in meetings. |
| 7.3 | Meeting Detail – Documents Tab | FE-07 Personal Document Library & Meeting Document Management | Allows users who can view a meeting to see its documents grouped by Agenda Item, Vote or general, with page and comment counts, and the Chair or Secretary to add, link, unlink and remove documents until the meeting ends. |
| 7.4 | Add Document to Meeting Dialog | FE-07 Personal Document Library & Meeting Document Management | Allows the Chair or Secretary to add documents to a meeting from the computer (up to 10 files and 200 MB at a time) or from the personal library, generally or for one Agenda Item. |
| 7.5 | Document Viewer – Preview & Comments | FE-07 Personal Document Library & Meeting Document Management | Allows users who can view a meeting to preview a meeting document page by page, download it unless the Meeting is Secret, and read, write and reply to comments. |
| 8.1 | Meeting Lobby | FE-08 Online Meeting Room Management | Allows the Chair, Secretary, Participant or a verified Guest to preview camera and microphone, choose audio and video devices and enter the online room of an In Progress Online or Hybrid meeting. |
| 8.2 | Online Meeting Room – Video Grid | FE-08 Online Meeting Room Management | Allows users connected to the online room to see participants in a video grid, see who is speaking, turn their own microphone and camera on or off, share the screen, use a floating window, open the side panels and leave the room. |
| 8.3 | In-Room Participants Panel | FE-08 Online Meeting Room Management | Allows users in the online room to view who is connected with their meeting role, who is speaking and who is muted, and allows the Chair to mute a participant. |
| 8.4 | Screen Sharing & Chat | FE-08 Online Meeting Room Management | Allows users connected to the online room to present their screen on the main stage, follow a shared screen and exchange text chat messages that are not kept after the meeting. |
| 8.5 | In-Room Voting Panel | FE-08 Online Meeting Room Management | Planned: allows eligible voters in the online room to follow the open Vote and cast a ballot without leaving the room. |
| 8.6 | In-Room Documents Panel | FE-08 Online Meeting Room Management | Planned: allows users in the online room to read the meeting documents, current Agenda Item first, opening each document in a new tab so the call continues. |
| 9.1 | Meeting Detail – Recordings Tab | FE-09 Meeting Audio Upload & Media Library | Allows the Secretary to upload the meeting audio after the meeting has ended, see processing status and uploaded files, review the speaker-attributed transcript and start generating the AI summary. |
| 9.2 | Upload Audio Dialog | FE-09 Meeting Audio Upload & Media Library | Allows the Secretary to select or drag an MP3, M4A or WAV audio file of up to 500 MB, confirm recording consent and upload it to an Ended meeting for transcription. |
| 10.1 | Speaker-Attributed Transcript | FE-10 Speaker-Attributed Transcription & Meeting Summary | Allows the Secretary to play the uploaded meeting audio, search and correct the transcript text, and assign unknown voices to meeting attendees before generating the meeting summary. |
| 10.2 | Meeting Summary | FE-10 Speaker-Attributed Transcription & Meeting Summary | Allows the Secretary to review and edit the AI-generated draft summary, regenerate it, approve it and then publish the approved summary to attendees as a separate step. |
| 11.1 | Meeting Detail – Voting Tab | FE-11 Voting Management | Allows the Chair to view all Votes of an InProgress Meeting with their status and turnout, create, edit, delete and open Draft Votes, and close an open Vote; other involved users view read-only. |
| 11.2 | Create & Open Vote Dialog | FE-11 Voting Management | Allows the Chair to define a Vote for the current Agenda Item of an InProgress Meeting – content, options, ballot type and eligible voter roles – and save it as a draft or open it immediately. |
| 11.3 | Cast Vote | FE-11 Voting Management | Allows an eligible Chair, Secretary, Participant or verified Guest to view an open Vote of the InProgress Meeting, choose one option and submit a single ballot that cannot be changed afterwards. |
| 11.4 | Vote Result & Conclusion | FE-11 Voting Management | Allows the Chair to view the tallied result of a closed Vote, including public ballots, and record its conclusion – Passed, NotPassed or Deferred – with a note carried into the minutes. |
| 12.1 | Minutes Editor | FE-12 Meeting Minutes & Digital Signing | Allows the Secretary to generate draft minutes of an Ended Meeting from meeting information, attendance, the published summary and vote conclusions, edit the text, and submit it for signing. |
| 12.2 | Sign Minutes Dialog | FE-12 Meeting Minutes & Digital Signing | Allows a designated signer to read the minutes pending signature, see who has already signed, and either sign the current revision or request a revision with a reason. |
| 12.3 | Signing Status & Finalization | FE-12 Meeting Minutes & Digital Signing | Allows the Secretary to track each signer’s status on the current revision and the full signing history, and to seal the minutes once every designated signer has signed. |
| 12.4 | Finalized Meeting Minutes | FE-12 Meeting Minutes & Digital Signing | Allows users involved in the Meeting to view the sealed, read-only minutes with all signatures, open the signing history and download the minutes as a PDF. |
| 13.1 | Notification Panel | FE-13 Email & In-App Notification | Allows every signed-in internal user to view personal in-app notifications and the unread count, mark one or all as read, and open the meeting a notification refers to. |
| 13.2 | Meeting Invitation Email | FE-13 Email & In-App Notification | Allows an invited internal participant or external Guest to read the meeting invitation – time, location, Chair and role – and open the response or meeting details from the email. |
| 14.1 | Personal Dashboard | FE-14 Personal Dashboard & Meeting Workspace | Allows every signed-in internal user to see today’s meetings, counters of pending invitations, open votes, minutes to sign and this week’s meetings, and jump straight to each waiting task. |
| 14.2 | Meeting Workspace | FE-14 Personal Dashboard & Meeting Workspace | Allows the Chair, Secretary and Participants of an InProgress Meeting to follow the current agenda item and its documents, see what comes next, vote on an open Vote and keep private notes. |
| 15.1 | Reports & Statistics | FE-15 Reporting & Statistics | Allows users to view meeting statistics for a chosen period – meeting count, attendance rate, average duration, timely sealing, monthly and status breakdowns – and export them to Excel or PDF. |

#### 5.2 External API Inventory

| # | API Name | Calling Path | Description |
| --- | --- | --- | --- |
| 1 | Google Identity Services (Sign in with Google, browser SDK) | Frontend GoogleSignInButton (src/features/auth) → loads https://accounts.google.com/gsi/client → returns Google ID token (credential) to the SPA | Google's sign-in popup authenticates the user's Google account in the browser and hands the SPA a signed ID token; no client secret and no server-side OAuth redirect/callback is used. |
| 2 | Google Identity Platform – ID token verification (OpenID Connect) | AuthEndpoints → POST /api/auth/google → AuthenticationService.SignInWithGoogleAsync → GoogleTokenVerifier (Infrastructure/Authentication, Google.Apis.Auth) | Backend verifies the Google ID token (signature against Google's published signing certificates, issuer, audience = configured Client ID, expiry, email_verified, sub) and then signs the user in, auto-linking the Google account on first use by verified email (ADR-0009). |
| 3 | SMTP mail server (any provider, e.g. Gmail SMTP) | EmailDeliveryWorker → EmailQueueProcessor → SmtpEmailSender (Infrastructure/Email, MailKit); PasswordResetEmailSender → SmtpEmailSender | Sends meeting invitation emails (with .ics calendar attachment) from the email_messages outbox queue, and password-reset link emails sent directly when an administrator resets a password. |
| 4 | LiveKit server – RoomService API & client signalling (planned) | Planned: CallEndpoints → POST /api/meetings/{id}/call/join → CallRoomService → LiveKitGateway (Infrastructure/Calls, Livekit.Server.Sdk.Dotnet); browser livekit-client → LiveKit WebSocket /rtc | Self-hosted LiveKit SFU for the online meeting room: backend issues LiveKit access tokens after permission checks and calls RoomService (list/mute/remove participants, delete room); media and signalling flow browser ↔ LiveKit. Designed and accepted but not yet in the main codebase. |
| 5 | LiveKit webhook (planned) | Planned: LiveKit server → POST /api/livekit/webhook → CallEndpoints → CallRoomService.HandleWebhookAsync | Inbound callback from LiveKit on participant_joined/left, room_finished and track_published, used to record call sessions/attendance and enforce the one-screen-share rule. |
| 6 | AI / Speech Processing Provider (planned – provider not yet decided) | Planned background processing of an uploaded Recording → Transcript / TranscriptSegment → MeetingSummary (no client component exists yet) | Converts uploaded meeting audio into a speaker-attributed (diarized) transcript and a draft meeting summary for the Secretary to review; whether the model is self-hosted or an external API is still open, so the provider name/model is stored per result. |

#### 5.3 Background Job Inventory

| # | Job Name | Group | Description |
| --- | --- | --- | --- |
| 1 | Process notifications | Scheduled worker | Every 5 seconds, creates due meeting reminders and pushes new in-app notifications to connected users. |
| 2 | Deliver meeting invitation emails | Queue worker | When SMTP is enabled, processes the meeting invitation email queue and retries failed emails under the retry policy. |

## II. Use Case Specifications

### 1. FE-01 – Authentication & Account Security

#### 1.1 UC-01 – Sign in with Email and Password

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | As an internal system user, I want to sign in using my email and password so that I can securely access the system through my internal account. |  |  |
| Preconditions | ● PRE-1: The user has an existing internal account.<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |  |  |
| Postconditions | ● POST-1: The user is authenticated.<br>● POST-2: The system identifies the corresponding internal user.<br>● POST-3: The user is redirected to the appropriate authenticated page. |  |  |
| Normal Flow | Sign in with Email and Password<br>1. The user enters their email and password on the login page.<br>2. The user clicks “Sign In”.<br>3. The system validates the provided credentials through the authentication service.<br>4. The system logs the user in and redirects the user to the appropriate authenticated page. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: System cannot authenticate the user<br>1. The entered email or password is invalid. (BR-01, BR-02)<br>2. The system displays MSG01 – “Invalid email or password.”<br>EX-2: The internal account has been deactivated<br>1. The corresponding internal account is inactive or has been deactivated. (BR-03)<br>2. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.” |  |  |

#### 1.2 UC-02 – Sign in with Google

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | Google Identity Provider |
| --- | --- | --- | --- |
| Description | As an internal system user, I want to sign in using my Google account so that I can securely and quickly access the system through my existing internal account. |  |  |
| Preconditions | ● PRE-1: The user has an existing internal account.<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |  |  |
| Postconditions | ● POST-1: User is authenticated.<br>● POST-2: A valid server-side session is created.<br>● POST-3: System identifies the corresponding internal user.<br>● POST-4: User is redirected to the appropriate authenticated page. |  |  |
| Normal Flow | Sign in with Google<br>1. User selects “Continue with Google” on the login page.<br>2. User signs in using their Google account.<br>3. System verifies that the Google account is associated with an authorized internal user.<br>4. System signs the user in and grants access according to the user's assigned role. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: User can’t authenticate with Google<br>1. The user fails to authenticate with Google or cancels the Google sign-in process. (BR-04)<br>2. System displays message MSG03 – “Google sign-in was unsuccessful.”<br>EX-2: System can’t verify the Google account<br>1. The system cannot verify the identity information returned by Google. (BR-05)<br>2. System displays message MSG04 – “Unable to verify your Google account. Please try again.”<br>EX-3: Google account is not linked to an internal account<br>1. The authenticated Google account is not associated with any existing internal account. (BR-06)<br>2. System displays message MSG05 – “This Google account has not been granted access to the system.”<br>EX-4: The internal account has been deactivated<br>1. The internal account associated with the Google account is inactive or has been deactivated. (BR-03)<br>2. System displays message MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.” |  |  |

#### 1.3 UC-03 – Sign Out

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | As an authenticated user, I want to sign out of the system so that I can securely end my current access to the system. |  |  |
| Preconditions | ● PRE-1: The user is currently signed in to the system.<br>● PRE-2: The user is currently accessing an authenticated area of the system. |  |  |
| Postconditions | ● POST-1: User is signed out of the system.<br>● POST-2: The user's current authentication state is invalidated.<br>● POST-3: User is redirected to the login page.<br>● POST-4: Protected system functions are no longer accessible until the user signs in again. |  |  |
| Normal Flow | Sign Out<br>1. User selects “Sign Out”.<br>2. System displays a confirmation message.<br>3. User confirms the sign-out action.<br>4. System signs the user out.<br>5. System redirects the user to the login page. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: System can’t complete the sign-out action<br>1. The system cannot complete the sign-out operation. (BR-09)<br>2. System displays message MSG06 – “Unable to sign out. Please try again.” |  |  |

#### 1.4 UC-04 – Access Meeting via Guest Invitation

| Primary Actors | Guest | Secondary Actors | Email / SMTP Service |
| --- | --- | --- | --- |
| Description | Allows an invited Guest to verify control of the invited email and enter self-declared display information for the invited Meeting. |  |  |
| Preconditions | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked or expired. |  |  |
| Postconditions | POST-1: Verified Guest access is limited to the invited Meeting.<br>POST-2: The Guest may use Member participation functions and receive additional Meeting permissions. Email control is verified; display name and job title are self-declared. |  |  |
| Normal Flow | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system checks the invitation and requests the one-time code sent to the invited email.<br>3. The Guest enters the code.<br>4. The system validates the code and invitation and verifies control of the invited email. (BR-153)<br>5. The Guest enters the display name and job title for presentation in the Meeting; the system labels this information as self-declared. (BR-180)<br>6. The system establishes Meeting-scoped Guest access and displays the invited Meeting. |  |  |
| Alternative Flows | ALT-1: If the Guest reopens a valid link during an active verified session, the system opens the same invited meeting without requesting another code. |  |  |
| Exception Flows | EX-1: The invitation is missing, expired, or revoked: the system denies access and explains that the invitation is no longer valid.<br>EX-2: The code is incorrect, expired, or already used: the system denies verification and allows the Guest to request or enter a valid code.<br>EX-3: The verification service is unavailable: the system reports that verification could not be completed and does not establish a session. |  |  |

Sequence source and PNG: docs/06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation. This single end-to-end sequence covers code request, invited-email verification, self-declared display information and access to the invited Meeting; these steps remain part of UC-04, not additional Use Cases.

### 2. FE-02 – User & Profile Management

#### 2.1 UC-05 – View User Accounts

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to view, search, filter, and browse user accounts in the system. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to view user accounts. |  |  |
| Postconditions | ● POST-1: The user account list is displayed.<br>● POST-2: No user account information is modified. |  |  |
| Normal Flow | View User Accounts<br>1. The System Admin selects User Accounts from the navigation menu.<br>2. The system retrieves the user accounts.<br>3. The system sorts the user accounts according to the default sorting rule (BR-167).<br>4. The system displays the user account list showing Full Name, Username/Email, System Role, and Account Status.<br>5. The System Admin browses the user account list using pagination controls. |  |  |
| Alternative Flows | AF-1: Search User Accounts<br>1. At Step 4 of the Main Flow, the System Admin enters a Name, Username, or Email into the search field.<br>2. The system searches for user accounts matching the entered criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow.<br>AF-2: Filter User Accounts<br>1. At Step 4 of the Main Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow.<br>AF-3: No User Accounts Match the Current Criteria<br>1. The applied search or filter criteria produce no matching user accounts.<br>2. The system displays an empty list and a no-results message according to BR-12.<br>3. The System Admin may clear or adjust the search/filter parameters.<br>4. The flow resumes at Step 5 of the Main Flow.<br>5. At Step 4 of the Main Flow, the System Admin enters a Name, Username, or Email into the search field.<br>6. The system searches for user accounts matching the entered criteria.<br>7. The system displays the matching user accounts.<br>8. The flow resumes at Step 5 of the Main Flow.<br>AF-2: Filter User Accounts<br>1. At Step 4 of the Main Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Main Flow. |  |  |
| Exception Flows | N/A |  |  |

#### 2.2 UC-06 – Manage User Accounts

##### 2.2.1 UC-06a – Create User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to create a new user account by providing the required account information. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to create user accounts. |  |  |
| Postconditions | ● POST-1: A new user account is created successfully.<br>● POST-2: The new user account appears in User Management.<br>● POST-3: The account creation is recorded in the audit log. |  |  |
| Normal Flow | Create User Account<br>1. The System Admin selects Create User from the User Accounts screen.<br>2. The system displays the Create User form.<br>3. The System Admin enters the required user information.<br>4. The System Admin submits the user information.<br>5. The system validates the submitted information, including required fields and Username/Email validity and uniqueness (BR-13, BR-14).<br>6. The system creates the new user account with Active status (BR-15).<br>7. The system records the account creation in the audit log (BR-16).<br>8. The system displays MSG07 – "User account created successfully." and returns to the User Accounts list. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: Invalid User Information<br>1. At Step 5 of the Main Flow, the system determines that one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.<br>3. The System Admin corrects the information and resubmits the form.<br>4. The flow resumes at Step 5 of the Main Flow.<br>EX-2: Duplicate User Account<br>1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use."<br>3. The System Admin changes the conflicting information and resubmits the form.<br>4. The flow resumes at Step 5 of the Main Flow. |  |  |

##### 2.2.2 UC-06b – Update User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to update editable information of an existing user account within their permitted management scope. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to update the selected user account.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: The selected user account information is updated successfully.<br>● POST-2: The update action is recorded in the audit log. |  |  |
| Normal Flow | Update User Account<br>1. The System Admin selects a user account from the User Accounts list and selects Edit Account.<br>2. The system displays the current user account information.<br>3. The System Admin modifies the editable information, such as Full Name, Job Title, or Contact Information.<br>4. The System Admin submits the updated information.<br>5. The system validates the submitted information, including required fields and data format (BR-14, BR-17).<br>6. The system updates the user account information.<br>7. The system records the update in the audit log (BR-16).<br>8. The system displays MSG10 – "User account updated successfully." and returns to the User Accounts list. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: Invalid User Information<br>1. At Step 5 of the Main Flow, one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." and the corresponding validation messages for the invalid fields.<br>3. The System Admin corrects the information and resubmits it.<br>4. The flow resumes at Step 5 of the Main Flow.<br>EX-2: Email Already in Use<br>1. At Step 5 of the Main Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use."<br>3. The System Admin changes the Email and resubmits the information.<br>4. The flow resumes at Step 5 of the Main Flow. |  |  |

##### 2.2.3 UC-06c – Deactivate / Reactivate User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to deactivate or reactivate a user account while preserving the user's historical records. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage user account status.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: The user account status is changed to Inactive when deactivated or Active when reactivated.<br>● POST-2: A deactivated user can no longer access authenticated system functions.<br>● POST-3: The status change is recorded in the audit log. |  |  |
| Normal Flow | Deactivate / Reactivate User Account<br>1. The System Admin selects a user account from the User Accounts list.<br>2. The System Admin selects Deactivate Account or Reactivate Account.<br>3. The system requests confirmation of the account status change.<br>4. The System Admin confirms the action.<br>5. The system verifies that the requested status change is allowed (BR-03).<br>6. The system updates the user account status.<br>7. If the account is deactivated, the system ends the user's active sessions (BR-18).<br>8. The system records the status change in the audit log (BR-16).<br>9. The system displays MSG11 – “User account status changed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: Status Change Is Not Allowed<br>1. At Step 5 of the Main Flow, the system determines that the requested account status change is not allowed.<br>2. The system displays MSG12 – “Unable to change the status of this user account.”<br>3. The account status remains unchanged.<br>EX-2: Invalid Status Request<br>1. At Step 5 of the Main Flow, the system determines that the request does not specify the account status to set.<br>2. The account status remains unchanged.<br>3. The system displays MSG09 – “Invalid account information.”<br>4. The use case ends unsuccessfully. |  |  |

#### 2.3 UC-07 – Reset User Password

| Primary Actors | System Admin | Secondary Actors | User (the owner of the selected account), who completes the reset from the email |
| --- | --- | --- | --- |
| Description | Allows the System Admin to send the owner of an eligible user account a secure, single-use password reset link by email. The account owner then opens the link and sets a new password without signing in. The current password keeps working until the new one is set. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to reset user passwords.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: A password reset link valid for 60 minutes is created for the selected user, and any link sent to that user earlier stops working (BR-20).<br>● POST-2: A password reset email containing the link is sent to the user's registered email address.<br>● POST-3: The password reset request is recorded in the audit log (BR-16).<br>● POST-4: Until the link is used, the user's current password and active sessions remain unchanged.<br>● POST-5: When the account owner completes the reset, the password is replaced, every active session of that account is ended, the link can no longer be used, and the reset is recorded in the audit log (BR-01, BR-16, BR-20). |  |  |
| Normal Flow | Reset User Password<br>1. The System Admin selects a user account and selects Reset Password.<br>2. The system requests confirmation, showing the email address the link will be sent to.<br>3. The System Admin confirms the password reset.<br>4. The system verifies that the selected account is eligible for password reset: it is active and it signs in with a password (BR-22).<br>5. The system creates a secure password reset request for the selected user (BR-20).<br>6. The system sends a password reset email containing the link to the user's registered email address.<br>7. The system makes the new link usable; any link sent to the user earlier stops working (BR-20).<br>8. The system records the password reset request in the audit log (BR-16).<br>9. The system displays MSG13 – “Password reset email sent successfully.”<br>Complete Password Reset<br>The account owner continues the use case from the email. No sign-in is required: the link is the proof of identity.<br>10. The user opens the password reset link from the email.<br>11. The system displays the new password form.<br>12. The user enters the new password and its confirmation and submits them.<br>13. The system verifies that the link is valid: it exists, it has not expired, it has not been used, and no newer link has replaced it (BR-20).<br>14. The system validates the new password: it must follow the same password rule as account creation (UC-06) and match its confirmation.<br>15. The system marks the link as used, so it cannot be used again (BR-20).<br>16. The system verifies that the account is still eligible: it is active and it signs in with a password (BR-03, BR-22).<br>17. The system replaces the password with the new one (BR-01) and ends every active session of the account (BR-20).<br>18. The system records the completed password reset in the audit log, with the account owner as the actor (BR-16).<br>19. The system displays MSG92 – “Password reset successfully. Please sign in with your new password.” and takes the user to the sign-in page. The user is not signed in automatically. |  |  |
| Alternative Flows | AF-1: The Browser Is Already Signed In<br>1. At Step 19, the browser used to complete the reset still has a signed-in session (of this or another account).<br>2. The system signs that session out before showing the sign-in page.<br>3. If the sign-out succeeds, the flow resumes at Step 19.<br>4. If the sign-out cannot be completed, the system still displays MSG92 – “Password reset successfully. Please sign in with your new password.” and also displays MSG06 – “Unable to sign out. Please try again.” The browser's session remains signed in, and the new password is already in effect.<br>5. The use case ends. |  |  |
| Exception Flows | EX-1: User Account Is Not Eligible for Password Reset<br>1. At Step 4 of the Main Flow, the system determines that the selected account is deactivated, or that it has no password because it only signs in with Google (BR-22). The User Accounts list already makes Reset Password unavailable for such accounts, so this happens when the list is out of date.<br>2. No password reset link is created and no email is sent.<br>3. The system displays MSG14 – “Password reset is not available for this account.” When the system can tell which reason applies, it updates the account in the list and states the reason instead (the account is deactivated, or it signs in with Google only).<br>4. The use case ends.<br>EX-2: Password Reset Email Cannot Be Sent<br>1. At Step 6 of the Main Flow, the system cannot send the password reset email.<br>2. The new link is never made usable. A link sent to the user earlier, if any, keeps working.<br>3. The system displays MSG15 – “Unable to send the password reset email. Please try again.”<br>4. The use case ends.<br>EX-3: User Account No Longer Exists<br>1. At Step 4 of the Main Flow, the system cannot find the selected account.<br>2. No password reset link is created and no email is sent.<br>3. The system informs the System Admin that the account no longer exists and refreshes the User Accounts list. This situation has no message code.<br>4. The use case ends.<br>EX-4: Password Reset Link Is Not Valid<br>1. At Step 13, 15 or 16, the system determines that the link is unknown, has expired, has already been used (including by a simultaneous submission), has been replaced by a newer link, or belongs to an account that is now deactivated or has no password (BR-20, BR-22).<br>2. The password is not changed. A link that reached Step 15 is used up.<br>3. The system hides the new password form and displays MSG93 – “The password reset link is invalid or has expired. Please contact an administrator.” It does not say which of these reasons applies.<br>4. The use case ends.<br>EX-5: New Password Is Invalid<br>1. At Step 14, the new password does not satisfy the password rule, or the confirmation does not match it.<br>2. The password is not changed and the link remains usable.<br>3. The system shows the reason next to the field concerned. A password that breaks the password rule is MSG94 – “Invalid new password.”, shown through its field reason (at least 12 characters); a confirmation that does not match is reported before the link is checked.<br>4. The user corrects the new password and resubmits it.<br>5. The flow resumes at Step 13.<br>EX-6: Password Reset Page Opened Without a Reset Code<br>1. At Step 11, the page has no reset code. This happens when the link is incomplete, or when the page is reloaded or restored after the code was removed from the address bar.<br>2. The system does not check the link and does not say that it has expired, because the link itself may still be usable.<br>3. The system displays MSG129 – “No password reset code was found. Please open the link in the email again.” with a hint that the link still works if it has not been used, is less than 60 minutes old and has not been replaced by a newer link.<br>4. The use case ends. Opening the link from the email again starts a new execution at Step 10. |  |  |

#### 2.4 UC-08 – Assign System Role to User

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to assign a system role to a user account or change the user’s existing system role. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to assign system roles.<br>● PRE-3: The selected user account exists.<br>● PRE-4: At least one system role is available for assignment. |  |  |
| Postconditions | ● POST-1: The selected user is associated with the selected system role.<br>● POST-2: The user receives the capabilities associated with the selected system role.<br>● POST-3: The role assignment or change is recorded in the audit log.<br>● POST-4: The user keeps their active sessions, is notified of the change, and the new role's permissions apply from their next action without signing in again (BR-314). |  |  |
| Normal Flow | Assign System Role to User<br>1. The System Admin opens User Management.<br>2. The System Admin selects a user account.<br>3. The system displays the user information and the current system role, if any.<br>4. The System Admin selects Assign System Role.<br>5. The system displays the available system roles.<br>6. The System Admin selects a role and confirms the assignment.<br>7. The system assigns the selected role to the user.<br>8. The system records the role assignment in the audit log.<br>9. The system notifies the user that their access changed, so their sessions apply the new role's permissions without signing in again (BR-314).<br>10. The system displays MSG10 – “User account updated successfully.” |  |  |
| Alternative Flows | AF-1: Change Existing System Role<br>1. At Step 4 of the Main Flow, if the user already has a system role, the System Admin selects Change System Role.<br>2. The System Admin selects a different role.<br>3. The System Admin confirms the change.<br>4. The system replaces the current role with the selected role.<br>5. The system records the role change in the audit log.<br>6. The system notifies the user that their access changed, so their sessions apply the new role's permissions without signing in again (BR-314).<br>7. The system displays MSG10 – “User account updated successfully.” |  |  |
| Exception Flows | EX-1: Selected Role Is Not Available<br>1. The selected role is no longer available for assignment.<br>2. The system displays MSG09 – “Invalid account information.” with the field error “The role does not exist.”<br>3. No role assignment is changed. The use case ends.<br>EX-2: Last Administrator Cannot Be Demoted<br>1. At Step 7 of the Main Flow or Step 4 of AF-1, the system determines that, after the change, no active account would hold the user administration permission.<br>2. The user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends unsuccessfully. |  |  |

#### 2.5 UC-09 – Unlock User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to unlock a user account that has been locked by the authentication security controls. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated and authorized to manage user accounts.<br>● PRE-2: The selected user account exists and is currently locked. |  |  |
| Postconditions | ● POST-1: The account lock is cleared and the user can attempt to sign in, subject to account status and credentials.<br>● POST-2: The unlock action is recorded in the audit log. |  |  |
| Normal Flow | Unlock User Account<br>1. The System Admin opens User Accounts and selects the locked account.<br>2. The system displays the account status and available account actions.<br>3. The System Admin selects Unlock Account and confirms the action.<br>4. The system clears the lock state and resets the failed-attempt lock counter.<br>5. The system confirms the unlock and records the action. |  |  |
| Alternative Flows | ALT-1: If the account is already unlocked when the request is processed, the system leaves it unchanged and refreshes the displayed status. |  |  |
| Exception Flows | EX-1: The account does not exist or is outside the Admin's scope: the system denies the action.<br>EX-2: The account is inactive: the system does not make it sign-in eligible and explains that reactivation is required.<br>EX-3: The update fails: the system retains the existing lock state and reports that the account could not be unlocked. |  |  |

### 3. FE-03 – Role-Based Access Control

#### 3.1 UC-10 – View Role & Permission Matrix

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to view the predefined roles and their default permissions. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated. |  |  |
| Postconditions | ● POST-1: No system data is modified. |  |  |
| Normal Flow | View Role & Permission Matrix<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the role and permission matrix. |  |  |
| Alternative Flows | None. |  |  |
| Exception Flows | E1 — Role and permission information cannot be retrieved<br>1. The system cannot retrieve the role and permission information.<br>2. The system displays an error message.<br>3. The use case ends unsuccessfully. |  |  |

#### 3.2 UC-11 – Configure Role Permissions

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to configure the default permissions assigned to a predefined system role. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage role permissions. |  |  |
| Postconditions | ● POST-1: The selected role is associated with the updated set of default permissions.<br>● POST-2: The permission configuration change is recorded in the audit log.<br>● POST-3: Users holding the role keep their active sessions; the updated permissions apply to them from their next action without signing in again (BR-313). |  |  |
| Normal Flow | Configure Role Permissions<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the Role & Permission Matrix.<br>3. The System Admin configures the permissions assigned to a predefined role.<br>4. The system validates and saves the updated permission configuration (BR-23).<br>5. The system records the configuration change in the audit log (BR-16).<br>6. The system notifies the users currently holding the role, so their sessions apply the updated permissions without signing in again (BR-313).<br>7. The system displays MSG16 – “Role permissions updated successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: Invalid Permission Configuration<br>1. At Step 4 of the Main Flow, the system determines that the updated permission configuration violates a permission rule.<br>2. The system does not save the changes.<br>3. The system displays MSG17 – “Invalid permission configuration.”<br>4. The use case ends unsuccessfully.<br>EX-2: Permission Configuration Cannot Be Saved<br>1. At Step 4 of the Main Flow, the system cannot save the updated permission configuration.<br>2. The existing role permissions remain unchanged.<br>3. The system displays MSG18 – “Unable to update role permissions. Please try again.”<br>4. The use case ends unsuccessfully. |  |  |

#### 3.3 UC-12 – Manage Roles

##### 3.3.1 UC-12a – Create Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to create a new custom role with a name, an optional description and an optional initial set of permissions, without changing code or redeploying the system. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles. |  |  |
| Postconditions | ● POST-1: A new custom role is created and appears in the role list and in the Role & Permission Matrix.<br>● POST-2: The new role is associated with the selected permissions (the set may be empty).<br>● POST-3: The role creation is recorded in the audit log. |  |  |
| Normal Flow | Create Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The System Admin selects “Create Role”.<br>3. The system displays the role creation form.<br>4. The System Admin enters the role name and an optional description, selects the permissions to grant to the role, and submits the form.<br>5. The system validates the role information: the name is required and has at most 64 characters after trimming, the description has at most 256 characters, and every selected permission exists in the permission catalog.<br>6. The system checks that no existing role, including the predefined roles, has the same name (case-insensitive).<br>7. The system saves the new role together with its permissions.<br>8. The system records the role creation in the audit log (BR-16).<br>9. The system displays MSG19 – “Role created successfully.” |  |  |
| Alternative Flows | AF-1: Role Created Without Permissions<br>1. At Step 4 of the Main Flow, the System Admin does not select any permission.<br>2. The system accepts an empty permission set; user accounts assigned to this role have the same access as a Member.<br>3. The use case continues at Step 5 of the Main Flow. |  |  |
| Exception Flows | EX-1: Invalid Role Information<br>1. At Step 5 of the Main Flow, the system determines that the name is empty or longer than 64 characters, the description is longer than 256 characters, or a selected permission does not exist.<br>2. The system does not save the role.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.<br>EX-2: Duplicate Role Name<br>1. At Step 6 or Step 7 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the role.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.<br>EX-3: Role Cannot Be Saved<br>1. At Step 7 of the Main Flow, the system cannot save the new role.<br>2. No role is created.<br>3. The system displays MSG26 – “Unable to create the role. Please try again.”<br>4. The use case ends unsuccessfully. |  |  |

##### 3.3.2 UC-12b – Update Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to change the name and description of a custom role. Predefined roles cannot be renamed; the permissions of any role are configured through UC-11. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |  |  |
| Postconditions | ● POST-1: The name and/or description of the selected role are updated; its permissions and the user accounts holding it remain unchanged.<br>● POST-2: The change is recorded in the audit log with the values before and after the update.<br>● POST-3: No user session is terminated; users holding the role see the new name the next time their information is loaded. |  |  |
| Normal Flow | Update Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles.<br>3. The System Admin selects “Edit” on a custom role.<br>4. The system displays the edit form pre-filled with the current name and description.<br>5. The System Admin modifies the name and/or description and submits the form.<br>6. The system verifies that the selected role exists and is a custom role.<br>7. The system validates the role information using the same rules as UC-12 (name required, at most 64 characters after trimming; description at most 256 characters).<br>8. The system checks that no other role has the same name (case-insensitive). Changing only the letter case of the role’s own name is allowed.<br>9. The system saves the updated role.<br>10. The system records the change in the audit log (BR-16).<br>11. The system displays MSG22 – “Role updated successfully.” |  |  |
| Alternative Flows | AF-1: No Changes Submitted<br>1. At Step 7 of the Main Flow, the system determines that the submitted name and description are identical to the current values.<br>2. The system does not modify any data and does not record an audit log entry.<br>3. The system displays MSG22 – “Role updated successfully.”<br>4. The use case ends. |  |  |
| Exception Flows | EX-1: Predefined Role<br>1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not save the changes.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends unsuccessfully.<br>EX-2: Invalid Role Information<br>1. At Step 7 of the Main Flow, the system determines that the role information is invalid.<br>2. The system does not save the changes.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends unsuccessfully.<br>EX-3: Duplicate Role Name<br>1. At Step 8 or Step 9 of the Main Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the changes.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends unsuccessfully.<br>EX-4: Role Cannot Be Saved<br>1. At Step 9 of the Main Flow, the system cannot save the updated role.<br>2. The existing role information remains unchanged.<br>3. The system displays MSG27 – “Unable to update the role. Please try again.”<br>4. The use case ends unsuccessfully. |  |  |

##### 3.3.3 UC-12c – Delete Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to delete a custom role that is no longer held by any user account. Predefined roles cannot be deleted. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |  |  |
| Postconditions | ● POST-1: The selected role and its permission assignments are removed from the system.<br>● POST-2: The deletion, including the role name and the permissions it held, is recorded in the audit log.<br>● POST-3: No user account and no user session is affected. |  |  |
| Normal Flow | Delete Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles with the number of user accounts holding each role.<br>3. The System Admin selects “Delete” on a custom role.<br>4. The system displays a confirmation dialog.<br>5. The System Admin confirms the deletion.<br>6. The system verifies that the selected role exists, is a custom role, and is not held by any user account, including deactivated accounts.<br>7. The system deletes the role together with its permission assignments.<br>8. The system records the role deletion in the audit log (BR-16).<br>9. The system displays MSG23 – “Role deleted successfully.” |  |  |
| Alternative Flows | AF-1: Deletion Cancelled<br>1. At Step 5 of the Main Flow, the System Admin cancels the deletion.<br>2. The system closes the confirmation dialog without changing any data.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1: Predefined Role<br>1. At Step 6 of the Main Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not delete the role.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends unsuccessfully.<br>EX-2: Role Still Held by User Accounts<br>1. At Step 6 or Step 7 of the Main Flow, the system determines that one or more user accounts (active or deactivated) still hold the role, including an account assigned to the role while the deletion was in progress.<br>2. The system does not delete the role.<br>3. The system displays MSG25 – “This role is still held by {n} user account(s). Reassign them to another role before deleting.” in the confirmation dialog, where {n} is the number of user accounts holding the role.<br>4. The use case ends unsuccessfully.<br>EX-3: Role Cannot Be Deleted<br>1. At Step 7 of the Main Flow, the system cannot delete the role.<br>2. The role and its permission assignments remain unchanged.<br>3. The system displays MSG28 – “Unable to delete the role. Please try again.”<br>4. The use case ends unsuccessfully. |  |  |

#### 3.4 UC-13 – View Audit Log

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to review recorded system events for security, accountability, and operational investigation. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated and has permission to view the audit log. |  |  |
| Postconditions | ● POST-1: The requested audit entries are displayed read-only.<br>● POST-2: No audit entry is changed by viewing or filtering the log. |  |  |
| Normal Flow | View Audit Log<br>1. The System Admin opens Audit Log.<br>2. The system retrieves audit entries within the Admin's requested date range and filters.<br>3. The system displays entries in reverse chronological order with actor, event, target, time, and outcome where recorded.<br>4. The System Admin opens an entry or changes filters/pages the results.<br>5. The system displays the matching entries and preserves the read-only state. |  |  |
| Alternative Flows | ALT-1: If no entries match the filters, the system displays an empty-result message and permits the Admin to revise the filters. |  |  |
| Exception Flows | EX-1: Invalid filter or date-range values: the system identifies the invalid criteria and does not apply them.<br>EX-2: The audit log cannot be loaded: the system reports the failure and provides a retry action. |  |  |

### 4. FE-04 – Meeting Management

#### 4.1 UC-14 – Create Meeting

| Primary Actors | Meeting Organizer | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized Meeting Organizer to create one independent Meeting as a Draft, including its planned schedule, mode, location and confidentiality. |  |  |
| Preconditions | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The Meeting Organizer is authorized to create meetings. |  |  |
| Postconditions | ● POST-1: A new Meeting exists in Draft status with its planned start and end times.<br>● POST-2: The creator is recorded as Meeting Organizer.<br>● POST-3: The Meeting may subsequently be assigned participants, prepared and submitted for approval. |  |  |
| Normal Flow | Create Meeting<br>1. The Meeting Organizer selects Create Meeting.<br>2. The system displays the new Meeting form.<br>3. The Organizer enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description.<br>4. The Organizer submits the form.<br>5. The system checks the required values, confirms Planned Start Time is later than the current time and Planned End Time is later than Planned Start Time. (BR-24, BR-25, BR-62, BR-162)<br>6. The system creates exactly one Meeting in Draft status and records the creator. (BR-26)<br>7. The system displays MSG29 – "Meeting created successfully." |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid Meeting Information<br>1. At Step 5, a required value is missing/invalid, the planned start is not in the future, or the planned end does not follow the start. (BR-24, BR-62, BR-162)<br>2. The system displays MSG31 – "Invalid meeting information." and highlights the affected fields.<br>3. No Meeting is created. The Organizer may correct the inputs and resubmit at Step 4.<br>EX-2 – Meeting Cannot Be Created<br>1. At Step 6, the system cannot save the Meeting.<br>2. The system displays MSG35 – "Unable to create the meeting. Please try again."<br>3. No Meeting is created; the use case ends unsuccessfully. |  |  |

#### 4.2 UC-15 – Update Meeting

| Primary Actors | Meeting Organizer | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Meeting Organizer to update Draft/Rejected Meeting information, or to reschedule only the planned times or location when the Meeting is Published. |  |  |
| Preconditions | ● PRE-1: The Meeting exists and the Meeting Organizer is authenticated and authorized.<br>● PRE-2: Full Meeting editing is available only in Draft or Rejected. (BR-27)<br>● PRE-3: If the Meeting is Published, only the Meeting Organizer may invoke Reschedule to change planned times or location. The Meeting must not have started. (BR-27, BR-158) |  |  |
| Postconditions | ● POST-1: Authorized updates are saved to the Meeting.<br>● POST-2: Meeting lifecycle and assigned approver are unchanged.<br>● POST-3: When the schedule of a Published Meeting changes, affected participants are notified and the change is recorded. (BR-76) |  |  |
| Normal Flow | Update Meeting<br>1. The Meeting Organizer opens a Draft or Rejected Meeting and selects Edit.<br>2. The system displays the editable Meeting fields.<br>3. The Organizer changes the fields and submits.<br>4. The system verifies current Meeting status, permissions, required information, and confirms Planned End Time is after Planned Start Time. (BR-24, BR-27, BR-62)<br>5. The system saves the updates without changing Meeting status and shows the success message. |  |  |
| Alternative Flows | AF-1 – Reschedule Published Meeting<br>Entry point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects Reschedule for a Meeting that is Published and has not started.<br>2. The system allows changes only to Planned Start Time, Planned End Time and Location. All other fields are read-only. (BR-27, BR-158)<br>3. The Organizer enters the changes and submits.<br>4. The system validates that the new start is in the future and the end follows the start. (BR-62, BR-75)<br>5. The system saves the new schedule, retains Published status, records the change and queues notifications to affected invitees. Delivery failure does not roll back the saved change. (BR-38, BR-76, BR-158)<br>6. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified." The use case succeeds. |  |  |
| Exception Flows | EX-1 – Invalid or Unauthorized Update<br>1. The system determines that meeting information is invalid, the Organizer is no longer authorized, or the Meeting status does not permit this edit. (BR-27, BR-62)<br>2. The system does not save any changes and displays the applicable existing validation/error message.<br>3. The use case ends unsuccessfully or the Organizer corrects input and retries.<br>EX-2 – Reschedule Not Permitted<br>1. During AF-1 the Meeting is not Published, has already started, its planned start is not in the future, or a field outside the allowed Reschedule set was modified. (BR-27, BR-75, BR-158)<br>2. The system rejects the update; the existing schedule remains unchanged.<br>3. The system displays the relevant existing meeting-update error message. The use case ends.<br>EX-3 – Update Cannot Be Saved<br>1. At Main Step 5 or AF-1 Step 5 a persistence error occurs.<br>2. Existing Meeting data/status remain unchanged and no schedule-update notification is sent.<br>3. The system displays the existing update-failure message. The use case ends. |  |  |

#### 4.3 UC-16 – View Meeting Details

| Primary Actors | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows authorized users to view the details of one Meeting, including planned schedule, actual start/end times when available, and meeting-level status. Agenda and other records remain accessible through their corresponding views. |  |  |
| Preconditions | ● PRE-1: The user is authenticated.<br>● PRE-2: A meeting has been selected or identified. |  |  |
| Postconditions | ● POST-1: The Meeting Name, planned time range, actual times where available, mode, location, confidentiality, description and lifecycle status are displayed read-only. For a Cancelled Meeting, the cancellation reason, who cancelled it and when are also displayed. (BR-298)<br>● POST-2: The Meeting is not modified. |  |  |
| Normal Flow | View Meeting Details<br>1. The user requests the selected Meeting.<br>2. The system verifies that the Meeting exists and the user has access. (BR-29)<br>3. The system retrieves the Meeting information and planned/actual time fields.<br>4. The system displays Meeting Name, planned start/end time, actual start/end time when available, Meeting Mode, Location, Confidentiality Level, Description, and Status. For a Cancelled Meeting, it also displays the cancellation reason, who cancelled it and when. (BR-298)<br>5. The user reviews the Meeting details. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting Does Not Exist or Is Not Accessible<br>1. At Step 2, the system cannot find the selected meeting or the user is not authorized to view it.<br>2. The system does not disclose whether the meeting exists.<br>3. The use case ends.<br>EX-2 – Meeting Details Cannot Be Retrieved<br>1. At Step 3, the meeting exists and is accessible, but the system cannot retrieve its meeting-level information.<br>2. The system displays MSG37 – "Unable to load meeting details. Please try again."<br>3. The use case ends. |  |  |

#### 4.4 UC-17 – View Meeting List

| Primary Actors | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment. The assigned Secretary can view the Meeting before publication under BR-118. External Guests do not use the internal Meeting list. |  |  |
| Preconditions | ● PRE-1: The user is authenticated. |  |  |
| Postconditions | ● POST-1: The system displays the list of meetings that the user is authorized to view.<br>● POST-2: No meeting information is modified. |  |  |
| Normal Flow | View Meeting List<br>1. The user selects Meetings from the navigation menu.<br>2. The system identifies only meetings the user may view under BR-29: meetings created by the user at any status; meetings assigned to the user as Meeting Approver from WaitingApproval onward; and meetings assigned to the user as an active Secretary at any status, including before publication (BR-118); and meetings where the user is assigned as Chair or Member from Published onward. External Guests access only their invited Meeting through UC-04 and do not use this internal list.<br>3. The system sorts and displays only those authorized meetings, including Cancelled Meetings with their status. (BR-297)<br>4. The user browses the displayed meetings. |  |  |
| Alternative Flows | AF-1 – Search Meetings<br>1. At Step 4 of the Main Flow, the user enters a meeting name in the search field.<br>2. The system finds meetings matching the entered search criteria within the meetings available to the user.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – Filter Meetings<br>1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status (including Cancelled), Confidentiality Level, or Time Range.<br>2. The system applies the selected criteria to the meetings available to the user. For Time Range, a Meeting matches when its own planned start/end time overlaps the selected time range. (BR-30)<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-3 – No Matching Meetings<br>1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.<br>2. The system displays an empty meeting list and the message "No meetings match the filter."<br>3. The user may change or clear the current search/filter criteria.<br>4. The flow resumes at Step 4 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Meeting List Cannot Be Retrieved<br>1. At Step 2 or Step 3 of the Main Flow, the system cannot retrieve the meeting list.<br>2. The system displays MSG38 – "Unable to load meetings. Please try again."<br>3. The use case ends.<br>EX-2 – Invalid Meeting List Criteria<br>1. The system determines that the selected search, filter, sort, or display criteria are invalid.<br>2. The system displays MSG32 – "Tiêu chí danh sách cuộc họp không hợp lệ."<br>3. The user may clear or adjust the search/filter criteria.<br>4. The use case ends. |  |  |

#### 4.5 UC-18 – Submit Meeting for Approval

| Primary Actors | Meeting Organizer | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Meeting Organizer to submit a Draft meeting or resubmit a previously Rejected meeting for review by an assigned Meeting Approver based on the Meeting information and planned schedule. |  |  |
| Preconditions | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to submit the selected meeting for approval.<br>● PRE-4: The meeting is in Draft or Rejected status. |  |  |
| Postconditions | ● POST-1: The meeting status is WaitingApproval.<br>● POST-2: A valid Meeting Approver is assigned to the current approval cycle.<br>● POST-3: The assigned Meeting Approver is granted access to the meeting for approval purposes.<br>● POST-4: The submission is recorded in the meeting lifecycle history.<br>● POST-5: The assigned Meeting Approver is notified of the pending approval request. |  |  |
| Normal Flow | Submit Meeting for Approval<br>1. The Meeting Organizer selects a Draft meeting and chooses Submit for Approval.<br>2. The system verifies that all required Meeting information and planned schedule are complete and valid for approval submission. (BR-25, BR-33, BR-62)<br>3. The system identifies users who are eligible to act as Meeting Approvers.<br>4. The system displays the eligible Meeting Approvers.<br>5. The Meeting Organizer selects a Meeting Approver.<br>6. The system verifies that the selected user is eligible to review the meeting and is not the Meeting Organizer.<br>7. The system verifies that the meeting is still in Draft or Rejected status.<br>8. The system assigns the selected Meeting Approver to the current approval cycle.<br>9. The system changes the meeting status to WaitingApproval.<br>10. The system records the submission and assigned approver in the meeting lifecycle history. The assignment, status change and history must all be completed together; otherwise EX-4 applies.<br>11. The system notifies the assigned Meeting Approver.<br>12. The system displays the submission success message. |  |  |
| Alternative Flows | AF-1 – Resubmit a Rejected Meeting<br>Entry Point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects a meeting in Rejected status.<br>2. The Meeting Organizer chooses Resubmit for Approval.<br>3. The flow resumes at Step 2 of the Main Flow and completes successfully when the submission requirements are satisfied. |  |  |
| Exception Flows | EX-1 – Incomplete Meeting-Level Information<br>1. At Step 2, the system determines that mandatory Meeting information and planned schedule is incomplete.<br>2. The system identifies the missing or invalid information.<br>3. The meeting remains Draft or Rejected and the use case ends.<br>EX-2 – Meeting Status Not Eligible for Submission<br>1. At Step 7, the system determines that the meeting is not in Draft or Rejected status.<br>2. The system displays MSG41 – "This meeting can no longer be submitted for approval."<br>3. The use case ends.<br>EX-3 – Selected Meeting Approver Is Not Eligible<br>1. At Step 6, the system determines that the selected user is not eligible to review the meeting or is the Meeting Organizer.<br>2. The meeting remains Draft or Rejected and no approval assignment is created.<br>3. The flow resumes at Step 5 of the Main Flow so the Meeting Organizer selects another eligible approver.<br>EX-4 – Submission Cannot Be Completed<br>1. At Steps 8–10, the system cannot complete the approval assignment, status change or submission history.<br>2. The Meeting, its previous approver assignment and its history remain unchanged. No approval notification is issued and the submission is not reported as successful.<br>3. The system displays MSG42 – "Unable to submit the meeting for approval. Please try again."<br>4. The use case ends.<br>EX-5 – No Eligible Meeting Approver Available<br>1. At Step 3, the system finds no eligible Meeting Approver.<br>2. The system displays MSG140 – "No eligible meeting approver is available. Please contact the administrator."<br>3. The Meeting remains Draft or Rejected. Its existing approver assignment and history remain unchanged; no new assignment, submission event or approval notification is created.<br>4. The use case ends unsuccessfully. The Organizer may try again when an eligible approver becomes available. |  |  |

#### 4.6 UC-19 – Review Meeting Approval

| Primary Actors | Meeting Approver | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Meeting Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it. |  |  |
| Preconditions | ● PRE-1: The Meeting Approver is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Approver is currently assigned to review the selected meeting and is not its Meeting Organizer.<br>● PRE-4: The meeting is in WaitingApproval status. |  |  |
| Postconditions | ● POST-1: The Meeting Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history.<br>● POST-6: The Meeting Organizer is notified in the application of the approval or rejection; a rejection notification includes the rejection reason. No email is sent for the decision. |  |  |
| Normal Flow | Review Meeting Approval<br>1. The Meeting Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Meeting Approver.<br>3. The system displays the meeting information for review.<br>4. The Meeting Approver selects Approve.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to approve the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status from WaitingApproval to Approved.<br>7. The system records the approval decision, acting user, and timestamp in the meeting lifecycle history and notifies the Meeting Organizer in the application that the meeting has been approved. The decision, status change, history and notification must all be completed together; otherwise EX-3 applies.<br>8. The system displays MSG43 – "Meeting approved successfully." |  |  |
| Alternative Flows | AF-1 – Reject Meeting<br>1. At Step 4 of the Main Flow, the Meeting Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Meeting Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to reject the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status to Rejected.<br>7. The system records the rejection decision, rejection reason, acting user, and timestamp, and notifies the Meeting Organizer in the application that the meeting has been rejected, including the rejection reason. The decision, reason, status change, history and notification must all be completed together; otherwise EX-3 applies.<br>8. The rejection reason becomes available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>9. The system displays MSG44 – "Meeting rejected successfully." |  |  |
| Exception Flows | EX-1 – Rejection Reason Is Missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-36)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting Is No Longer Awaiting Approval<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status.<br>2. No approval or rejection decision is applied.<br>3. The meeting remains in its current status.<br>4. The system displays "This meeting is no longer awaiting approval."<br>5. The use case ends.<br>EX-3 – Approval Decision Cannot Be Completed<br>1. At Steps 6–7 of the Main Flow or AF-1, the system cannot complete the approval or rejection decision, resulting status, decision history or Meeting Organizer notification.<br>2. The Meeting status, approval assignment, previous decision information and history remain unchanged.<br>3. No new approval or rejection decision is recorded, no notification or invitation is issued for the unsuccessful action, and the action is not reported as successful.<br>4. The system displays "Unable to complete the approval decision. Please try again."<br>5. The use case ends.<br>EX-4 – User Is No Longer Authorized to Review the Meeting<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the Meeting Approver is no longer authorized to approve or reject the selected meeting. (BR-35)<br>2. No approval or rejection decision is applied.<br>3. The meeting status remains unchanged.<br>4. The system informs the Meeting Approver that they are no longer authorized to review the meeting.<br>5. The use case ends.<br>EX-5 – Rejection Reason Is Too Long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |  |  |

#### 4.7 UC-20 – Publish Meeting

| Primary Actors | Meeting Organizer | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Meeting Organizer to publish an Approved meeting and initiate invitation delivery to current internal participants and external Guests. |  |  |
| Preconditions | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-124, BR-125, BR-129) |  |  |
| Postconditions | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history.<br>● POST-3: Current internal participants, except the Meeting Organizer who published the meeting, are notified in the application. All current internal participants, including that Meeting Organizer when they are also a participant, are sent invitation emails. Each external Guest with a current invitation is sent a separate email invitation to verify their invited email address and access only this Meeting. Unsuccessful delivery is retried without changing Published status. Successful publication does not mean every invitation has already been received. (BR-39, BR-40, BR-152, BR-153) |  |  |
| Normal Flow | Publish Meeting<br>1. The Meeting Organizer selects an Approved meeting.<br>2. The Meeting Organizer selects Publish Meeting.<br>3. The system verifies that the Meeting Organizer remains authorized, the meeting is still Approved with a valid planned schedule, exactly one current Chair has an active account, and at least one current Secretary has an active account.  (BR-37, BR-124, BR-125, BR-129)<br>4. The system changes the meeting status from Approved to Published. (BR-37)<br>5. The system records the publication action, acting user, and timestamp in the meeting lifecycle history. The Published status and publication history must be completed together before invitations are issued; otherwise EX-2 applies. (BR-38)<br>6. The system notifies current internal participants in the application, except the Meeting Organizer who published the meeting, and sends invitation emails to all current internal participants, including that Meeting Organizer when they are also a participant. Each external Guest with a current invitation receives a separate email invitation to verify their invited email address and access only this Meeting. Sending invitations does not delay confirmation of publication. If delivery fails, the system retries delivery and the Meeting remains Published. (BR-39, BR-40, BR-152, BR-153, BR-156)<br>7. The system displays MSG45 – "Meeting published successfully." |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting Is No Longer Eligible for Publication<br>1. At Step 3 of the Main Flow, the system determines that the meeting is no longer Approved, the Meeting Organizer is no longer authorized to publish it, there is not exactly one active current Chair, or there is no active current Secretary. (BR-37, BR-124, BR-125)<br>2. The meeting remains unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG46 – "This meeting can no longer be published."<br>5. The use case ends.<br>EX-2 – Meeting Cannot Be Published<br>1. At Steps 4–5 of the Main Flow, the system cannot complete the Published status or publication history.<br>2. The Meeting remains Approved; its previous assignment, decision information and history remain unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG47 – "Unable to publish the meeting. Please try again."<br>5. The use case ends. |  |  |

#### 4.8 UC-21 – Start Meeting

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Chair to start one Published Meeting, set the Meeting lifecycle to InProgress and record its actual start time. The Meeting has no subordinate scheduling objects. |  |  |
| Preconditions | ● PRE-1: The Chair is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The meeting is Published.<br>● PRE-4: The meeting has exactly one current Chair and at least one current Secretary with active accounts.  (BR-127) |  |  |
| Postconditions | ● POST-1: Meeting status is InProgress.<br>● POST-2: Actual Start Time, acting Chair and immutable lifecycle event timestamp are recorded. (BR-38)<br>● POST-3: In-app Meeting-start notifications are created for currently invited internal participants. (BR-164)<br>● POST-4: Online Meeting Room access is governed by the Meeting mode and current status. |  |  |
| Normal Flow | Start Meeting<br>1. The assigned Chair selects Start Meeting for the Published Meeting.<br>2. The system verifies the Chair is active/current, one active current Chair and at least one active Secretary exist, and the Meeting remains Published. (BR-127, BR-130, BR-131)<br>3. The system changes status to InProgress, saves Actual Start Time and records the lifecycle event. (BR-38, BR-131)<br>4. The system creates in-app notifications for current invited internal participants; a notification delivery problem does not undo a successfully started Meeting. (BR-164)<br>5. The system displays MSG110 – "Meeting started successfully." |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting Cannot Be Started<br>1. At Step 2, the meeting is no longer Published, the Chair is no longer assigned or active, or the required active Chair/Secretary assignments are no longer valid. (BR-127, BR-130, BR-131)<br>2. The meeting remains unchanged.<br>3. The system displays MSG112 – "This meeting cannot be started."<br>4. The use case ends.<br>EX-2 – Start Cannot Be Completed<br>1. At Step 3, the system cannot complete the lifecycle transition.<br>2. The meeting remains Published.<br>3. The system displays MSG115 – "Unable to start the meeting. Please try again."<br>4. The use case ends. |  |  |

#### 4.9 UC-22 – End Meeting

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to end an InProgress Meeting regardless of open Votes, unmarked attendance or remaining Pending Agenda Items. |  |  |
| Preconditions | ● PRE-1: The actor is the current authorized Chair.<br>● PRE-2: The Meeting is InProgress. (BR-130, BR-132) |  |  |
| Postconditions | ● POST-1: The Meeting becomes Ended and its Actual End Time is recorded.<br>● POST-2: Any InProgress Agenda Item is completed; remaining Pending Items stay Pending. (BR-159)<br>● POST-3: Online Meeting Room access is closed under existing lifecycle rules. (BR-143) |  |  |
| Normal Flow | End Meeting<br>1. The Chair selects End Meeting and confirms.<br>2. The system checks current Chair authority and that the Meeting is InProgress. No Vote-closure or attendance-completeness validation is performed. (BR-130, BR-132, BR-159)<br>3. The system completes any InProgress Agenda Item, changes the Meeting to Ended and records Actual End Time and lifecycle history together. Pending Items remain Pending and Votes/ballots remain unchanged.<br>4. The system closes room access and confirms MSG111 – "Meeting ended successfully."<br>5. Voting controls stop accepting ballots because the Meeting is Ended; the Chair may later close outstanding Votes to finalize their tally. |  |  |
| Alternative Flows | None. |  |  |
| Exception Flows | EX-1 – Actor or Meeting is not eligible: no end is recorded. MSG114 – "This meeting cannot be ended." The use case ends.<br>EX-2 – End cannot be saved: previous Meeting and Agenda states/timestamps remain unchanged and room closure is not triggered. MSG116 – "Unable to end the meeting. Please try again." The Chair may retry. |  |  |

#### 4.10 UC-75 – Cancel Meeting

| Primary Actors | Meeting Organizer | Secondary Actors | Email / SMTP Service |
| --- | --- | --- | --- |
| Description | Allows the Meeting Organizer to cancel a Meeting that will no longer take place, before it starts. A Cancelled Meeting stays in the system as a read-only record with its cancellation reason; it cannot be edited, published or started again. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the Meeting Organizer of the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved or Published. An InProgress Meeting is ended through UC-22 instead. (BR-297) |  |  |
| Postconditions | ● POST-1: The Meeting is Cancelled, and the reason, the actor and the cancellation time are recorded in its status history. (BR-38, BR-297, BR-298)<br>● POST-2: Agenda, participants, documents and Votes are kept read-only, and any pending approval request is closed. (BR-299)<br>● POST-3: If the Meeting was Published, every invited participant and Guest is notified. (BR-300) |  |  |
| Normal Flow | Cancel Meeting<br>1. The Meeting Organizer opens the Meeting details and selects Cancel meeting.<br>2. The system displays a confirmation with a required cancellation reason.<br>3. The Organizer enters the reason and confirms.<br>4. The system rechecks that the actor is the Meeting Organizer, the Meeting is Draft, WaitingApproval, Rejected, Approved or Published, and the reason is not blank. (BR-297, BR-298)<br>5. The system changes the Meeting to Cancelled and records the reason, the actor and the time in the status history together. A pending approval request is closed. (BR-38, BR-298, BR-299)<br>6. If the Meeting was Published, the system notifies every invited internal participant in the application and by email, and every invited Guest by email, including the reason. A notification failure does not undo the cancellation. (BR-300)<br>7. The system displays MSG261 – "Meeting cancelled successfully." and shows the Meeting as Cancelled. |  |  |
| Alternative Flows | AF-1 – Organizer keeps the Meeting<br>1. At Step 3, the Organizer chooses Back instead of confirming.<br>2. The Meeting and its status remain unchanged. The use case ends. |  |  |
| Exception Flows | EX-1 – Cancellation reason is missing<br>1. At Step 4, the reason is blank or contains only spaces.<br>2. The Meeting is not cancelled. The system displays MSG262 – "Enter a reason for cancelling the meeting."<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Meeting can no longer be cancelled<br>1. At Step 4, the Meeting has started, has ended or is already Cancelled, or the actor is no longer its Meeting Organizer. (BR-297)<br>2. The Meeting remains unchanged. The system displays MSG263 – "This meeting can no longer be cancelled."<br>3. The use case ends.<br>EX-3 – Cancellation cannot be saved<br>1. At Step 5, the system cannot save the status change and its history together.<br>2. The Meeting remains unchanged and no notification is sent. The system displays MSG264 – "Unable to cancel the meeting. Please try again."<br>3. The use case ends. |  |  |

### 5. FE-05 – Agenda Management

#### 5.1 UC-23 – Manage Agenda Items

##### 5.1.1 UC-23a – Create Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to create a new Agenda Item while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including during the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-65, BR-74) |  |  |
| Postconditions | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting and any selected documents belong to the same Meeting.<br>● POST-3: A Presenter, when set, is a current participant or Guest of the Meeting. (BR-306) |  |  |
| Normal Flow | Create Agenda Item<br>1. The Secretary opens the Meeting Agenda and selects Add Agenda Item.<br>2. The Secretary enters Title, Description and an optional Presenter chosen from the current internal participants and Guests of the Meeting, and selects available Meeting documents. (BR-306)<br>3. The system verifies the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values, the Presenter and document access. (BR-64, BR-65, BR-66, BR-74, BR-306)<br>4. The system creates the Item as Pending after the last position in the Meeting agenda. Existing Item positions are unchanged. If a Presenter was set while the Meeting is Published or InProgress, the Presenter is notified. (BR-67, BR-307)<br>5. The system displays MSG60 – "Agenda item created successfully." and automatically refreshes the complete Agenda for authorized users currently viewing it. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid Agenda Item Information<br>1. At Step 3, required values or selected documents are invalid.<br>2. No Item is created. The system displays MSG66 – "Invalid agenda item information." with the applicable field messages.<br>EX-2 – Agenda Planning No Longer Permitted<br>1. At Step 3, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. No Item is created. The system displays MSG67 – "The agenda for this meeting can no longer be modified."<br>EX-3 – Create Cannot Be Saved<br>1. At Step 4, the system cannot save the Item or its document associations.<br>2. The saved Agenda and its order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG68 – "Unable to create the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

##### 5.1.2 UC-23b – Update Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to update the Title, Presenter, Description and document associations of an Agenda Item, whatever its execution status, while Agenda planning is permitted. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>● PRE-3: Title, Presenter, Description and document associations may be changed whether the Item is Pending, InProgress or Done. (BR-68, BR-279) |  |  |
| Postconditions | ● POST-1: The Item information is updated.<br>● POST-2: Meeting membership, Item execution status, actual timestamps and Meeting lifecycle remain unchanged.<br>● POST-3: Authorized users currently viewing the Agenda automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Update Agenda Item<br>1. The Secretary selects an Item and chooses Edit.<br>2. The system displays the current Title, Presenter, Description and document associations for editing; the Presenter is chosen from the current internal participants and Guests of the Meeting (BR-306), whatever the Item's execution status. Document links may be added or removed until the Meeting ends through UC-33b and UC-33d. (BR-68, BR-185, BR-277, BR-278, BR-279)<br>3. The Secretary enters permitted changes and submits.<br>4. The system rechecks the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values, the Presenter and any selected Meeting documents. (BR-65, BR-66, BR-68, BR-74, BR-306)<br>5. The system saves the changes together, preserving execution status, actual timestamps and position. If the Presenter was assigned or changed while the Meeting is Published or InProgress, the new Presenter is notified; a notification failure does not undo the change. (BR-68, BR-307)<br>6. The system displays MSG61 – "Agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid Agenda Item Information<br>1. At Step 4, the submitted values or documents are invalid.<br>2. The Item remains unchanged. The system displays MSG66 with the applicable field messages.<br>EX-2 – Editing No Longer Permitted<br>1. At Step 4, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The Item remains unchanged. The system displays MSG67.<br>EX-3 – Update Cannot Be Saved<br>1. At Step 5, the system cannot save the changes.<br>2. Item information, document associations, execution status, actual timestamps and order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG69 – "Unable to update the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

##### 5.1.3 UC-23c – Delete Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to delete only a Pending Agenda Item from the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to a Meeting in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-74)<br>● PRE-3: The Item is Pending. InProgress and Done Items cannot be deleted. (BR-237) |  |  |
| Postconditions | ● POST-1: The selected Pending Item and its document links are removed. The documents stay in the Meeting and keep links to other Items; a document with no remaining links becomes a general Meeting document. (BR-280, BR-283)<br>● POST-2: Remaining Item positions and execution data, and the Meeting lifecycle, are unchanged.<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Delete Agenda Item<br>1. The Secretary selects a Pending Item, chooses Delete and confirms.<br>2. The system verifies the active Secretary assignment, permitted Meeting status, Item membership and that the Item is still Pending. (BR-64, BR-65, BR-237)<br>3. The system removes the Item and its document links, keeps the documents in the Meeting and preserves the remaining Item positions. (BR-283)<br>4. The system displays MSG62 – "Agenda item deleted successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Planning No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. Nothing is deleted. The system displays MSG67.<br>EX-2 – Agenda Item Has Already Started<br>1. At Step 2, the Item is InProgress or Done.<br>2. The Item and Agenda order remain unchanged. The system displays MSG142 – "Only Pending agenda items can be deleted."<br>EX-3 – Deletion Cannot Be Saved<br>1. At Step 3, the system cannot complete deletion.<br>2. The Item and the saved Agenda order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG70 – "Unable to delete the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

##### 5.1.4 UC-23d – Reorder Agenda Items

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to change the order of the Agenda Items within the same Meeting, whatever their execution status. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains at least two Agenda Items. (BR-65, BR-74) |  |  |
| Postconditions | ● POST-1: The Items of the Meeting are in the requested order.<br>● POST-2: Item membership, execution status and actual times remain unchanged. (BR-238)<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Reorder Agenda Items<br>1. The Secretary changes the relative order of the Agenda Items.<br>2. The system verifies the active Secretary assignment, permitted Meeting status and the latest saved Agenda. (BR-65, BR-74)<br>3. The system verifies that each current Item of the Meeting appears exactly once and all Items belong to this Meeting. (BR-64, BR-238)<br>4. The system saves the new order together.<br>5. The system displays MSG63 – "Agenda order updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Reordering No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The saved order remains unchanged. The system displays MSG67.<br>EX-2 – Invalid or Outdated Agenda Order<br>1. At Step 3, the request duplicates or omits a current Item, includes an Item from another Meeting, or no longer matches the current Agenda.<br>2. No order changes are saved. The system displays MSG143 – "The agenda order is invalid or out of date. Refresh the agenda and try again."<br>3. The system refreshes the complete Agenda. The Secretary may revise the order and continue from Step 1.<br>EX-3 – Order Cannot Be Saved<br>1. At Step 4, the system cannot save the requested order.<br>2. All Item positions remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG71 – "Unable to update the agenda order. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.2 UC-24 – Set Current Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to start any Pending Agenda Item as the current Item, whatever its position in the Agenda. |  |  |
| Preconditions | ● PRE-1: The Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Item is Pending. (BR-69, BR-70)<br>● PRE-3: No other Item of the Meeting is InProgress. (BR-71) |  |  |
| Postconditions | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |  |  |
| Normal Flow | Set Current Agenda Item<br>1. The Chair selects a Pending Item and chooses Set as Current.<br>2. The system rechecks the active Chair assignment, Meeting InProgress status and that the selected Item belongs to the Meeting and remains Pending. (BR-64, BR-69)<br>3. The system verifies that no other Item is InProgress. The position of the selected Item does not matter. (BR-70, BR-71)<br>4. The system changes the Item to InProgress and records its actual start time together. If competing requests arrive, only one Item may start. (BR-70, BR-71, BR-72)<br>5. The system displays MSG64 – "Current agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Item Cannot Become Current<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the selected Item does not belong to the Meeting or is not Pending.<br>2. The Agenda remains unchanged. The system displays MSG73 – "This agenda item cannot be set as current."<br>EX-2 – Another Agenda Item Is InProgress<br>1. At Step 3 or Step 4, another Item is already InProgress.<br>2. The selected Item remains Pending. The system displays MSG72 – "Another agenda item is already in progress. Complete it before starting another agenda item."<br>EX-3 – Current Agenda Item Cannot Be Saved<br>1. At Step 4, the system cannot save the Item status or actual start time.<br>2. The selected Item remains Pending; its timestamps and all other Agenda data remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG75 – "Unable to set the current agenda item. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.3 UC-25 – Complete Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to complete the current Agenda Item at any time. |  |  |
| Preconditions | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |  |  |
| Postconditions | ● POST-1: The current Item becomes Done with its actual end time.<br>● POST-2: The Meeting remains InProgress. No next Item starts automatically. (BR-72)<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Complete Agenda Item<br>1. The Chair selects Complete for the current Item.<br>2. The system verifies the active Chair assignment, Meeting InProgress status, Item membership and current InProgress status. (BR-64, BR-69, BR-70, BR-72)<br>3. The system records the actual end time and changes the Item to Done together. (BR-70, BR-72)<br>4. The Meeting stays InProgress with no current Item. The Chair may start any remaining Pending Item through UC-24.<br>5. The system displays MSG65 – "Agenda item completed successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Item Cannot Be Completed<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the Item does not belong to the Meeting or is not its current InProgress Item.<br>2. Nothing changes. The system displays MSG74 – "This agenda item cannot be completed."<br>EX-2 – Agenda Item Completion Cannot Be Saved<br>1. At Step 3, the system cannot save completion or the actual end time.<br>2. The Item remains InProgress with unchanged timestamps. The Meeting remains InProgress. No successful change is announced to viewers.<br>3. The system displays MSG76 – "Unable to complete the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.4 UC-26 – View Meeting Agenda

| Primary Actors | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view the complete Meeting Agenda and automatically see saved changes while the Agenda remains open. |  |  |
| Preconditions | ● PRE-1: The user may view the selected Meeting. (BR-29) |  |  |
| Postconditions | ● POST-1: Agenda title, presenter, order, execution status, recorded actual times and any recorded Item conclusion are shown read-only. (BR-311)<br>● POST-2: The complete Agenda automatically reflects successful creation, editing, deletion, reordering, Item start/completion and completion of the current Item when the Meeting ends. (BR-73)<br>● POST-3: Viewing does not modify the Agenda. |  |  |
| Normal Flow | View Meeting Agenda<br>1. The user opens the Meeting Agenda tab.<br>2. The system verifies Meeting access and retrieves the complete saved Agenda in its current order. (BR-29, BR-64)<br>3. The system shows Item title, presenter, status, order, recorded actual times and any Item conclusion with who last saved it and when. (BR-311)<br>4. While the user continues viewing, the system automatically refreshes the complete Agenda after a successful Agenda change, including changes made by another authorized user. Manual page refresh is not required. (BR-73)<br>5. The system rechecks access before displaying refreshed Agenda data. Failed changes are not shown as successful updates. |  |  |
| Alternative Flows | AF-1 – Empty Meeting Agenda<br>1. At Step 3 or Step 4, no Agenda Items exist.<br>2. The system displays the empty Agenda state.<br>3. While the view remains open, it automatically reflects subsequent saved Agenda changes. The use case succeeds. |  |  |
| Exception Flows | EX-1 – Meeting Not Accessible<br>1. At Step 2 or Step 5, the Meeting does not exist or the user lacks access.<br>2. No further Agenda information is disclosed. The use case ends.<br>EX-2 – Meeting Agenda Cannot Be Retrieved<br>1. At Step 2 or Step 4, the system cannot retrieve the latest Agenda.<br>2. The system displays MSG128 – "Unable to load the meeting agenda. Please try again." A retrieval failure is not displayed as an empty Agenda.<br>3. No Agenda data changes; previously displayed information is not presented as the latest saved Agenda. The user may retry. |  |  |

#### 5.5 UC-77 – Record Agenda Item Conclusion

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to record or update the conclusion of an Agenda Item: what was concluded or agreed for that item. Item conclusions are used when the minutes draft is generated (UC-59). Vote results and conclusions remain recorded through UC-57. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary.<br>● PRE-2: The selected Agenda Item belongs to the Meeting and is InProgress or Done. (BR-64, BR-310)<br>● PRE-3: The Meeting is InProgress, or Ended and its minutes draft has not yet been generated. (BR-310) |  |  |
| Postconditions | ● POST-1: The Item has one conclusion, replacing any earlier one, with the actor and time of the last save recorded. (BR-309, BR-311)<br>● POST-2: Item status, actual times, presenter, documents and position remain unchanged.<br>● POST-3: Authorized users currently viewing the Agenda automatically see the saved conclusion. (BR-73) |  |  |
| Normal Flow | Record Agenda Item Conclusion<br>1. The actor selects an InProgress or Done Agenda Item and chooses Record conclusion.<br>2. The system displays the current conclusion of the Item, if any, for editing.<br>3. The actor enters or edits the conclusion and saves.<br>4. The system rechecks the Chair or Secretary assignment, the Meeting status and minutes state, the Item status, and that the conclusion is not blank and does not exceed 2,000 characters. (BR-309, BR-310)<br>5. The system saves the conclusion with the actor and the time. (BR-309, BR-311)<br>6. The system displays MSG271 – "Agenda item conclusion saved." and automatically refreshes the Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Conclusion is blank or too long<br>1. At Step 4, the conclusion is blank or longer than 2,000 characters. (BR-309)<br>2. Nothing is saved. The system displays MSG272 – "Enter a conclusion of up to 2,000 characters."<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Conclusion can no longer be changed<br>1. At Step 4, the actor is no longer the current Chair or Secretary, the Item is Pending, the Meeting is not InProgress or Ended, or the minutes draft has already been generated. (BR-310)<br>2. Nothing is saved; any earlier conclusion remains. The system displays MSG273 – "The conclusion of this agenda item can no longer be changed."<br>3. The use case ends.<br>EX-3 – Conclusion cannot be saved<br>1. At Step 5, the system cannot save the conclusion.<br>2. Any earlier conclusion remains unchanged. The system displays MSG274 – "Unable to save the agenda item conclusion. Please try again."<br>3. The use case ends.<br>EX-4 – Meeting is unavailable<br>1. Before displaying or saving, the Meeting cannot be accessed or the Item no longer exists.<br>2. No Agenda information is disclosed and nothing is saved. The system displays MSG174 – "This Meeting is not accessible."<br>3. The use case ends. |  |  |

### 6. FE-06 – Meeting Participant Management

#### 6.1 UC-27 – View Meeting Participants

| Primary Actors | Secretary, Chair, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized internal participant or verified Guest to view and search the current Meeting roster, including internal participants and Guests. |  |  |
| Preconditions | ● PRE-1: The actor has current authorized Meeting participation or verified Guest access.<br>● PRE-2: Roster visibility follows the actor’s current Meeting permissions. (BR-117–BR-121, BR-176) |  |  |
| Postconditions | ● POST-1: The authorized current roster is displayed.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Meeting Participants<br>1. The actor opens the Participants view of an accessible Meeting.<br>2. The system rechecks current Meeting access and roster-view permission. (BR-176)<br>3. The system displays current internal participants and Guests with invitation identity and Meeting Role or Guest participation type. Where available, a Guest’s self-declared display name and job title are shown separately from the verified email. Removed or revoked entries are excluded. (BR-103, BR-180)<br>4. The actor reviews the roster. |  |  |
| Alternative Flows | AF-1 – Search participants<br>1. At Step 1, the actor enters a name or email in the search field.<br>2. The system filters the current authorized roster to matching entries.<br>3. The actor reviews the matching entries; the use case resumes at Step 4. |  |  |
| Exception Flows | EX-1 – Roster is not accessible<br>1. At Step 2, the actor lacks current Meeting access or roster-view permission.<br>2. The system does not return or disclose roster data and responds with the generic not-found result required by BR-121. The use case ends unsuccessfully.<br>EX-2 – Roster cannot be retrieved<br>1. At Step 3, the roster service or data source cannot return the current roster.<br>2. No partial roster is presented as complete. The system displays MSG95 – “Unable to load meeting participants. Please try again.”<br>3. The use case ends unsuccessfully; the actor may retry by reopening the Participants view. |  |  |

#### 6.2 UC-28 – Manage Meeting Participants

##### 6.2.1 UC-28a – Add Meeting Participant

| Primary Actors | Meeting Organizer, Secretary | Secondary Actors | Email / SMTP Service |
| --- | --- | --- | --- |
| Description | Allows the Organizer to assign a Secretary when none is current in Draft, Rejected or Approved, initially or after removal; current Secretaries add internal participants or invite a Guest. |  |  |
| Preconditions | ● PRE-1: The actor is either a current assigned Secretary, or the Meeting Organizer restoring/initializing a missing Secretary in Draft, Rejected or Approved.<br>● PRE-2: The Meeting permits roster management. WaitingApproval remains read-only. (BR-78–BR-80, BR-122) |  |  |
| Postconditions | ● POST-1: The selected internal participants, Guest invitation, or Secretary recovery is saved.<br>● POST-2: New invitations start with Pending response.<br>● POST-3: Approved additional Meeting permissions apply to this Meeting only; Guest receives no internal or system Role. (BR-88, BR-150, BR-174) |  |  |
| Normal Flow | Add Internal Participants<br>1. The actor opens participant management for the Meeting.<br>2. A current Secretary selects one or more active internal users, assigns the shared Meeting Role and any permitted additional Meeting permissions.<br>3. The actor confirms the selection.<br>4. The system rechecks actor authority, Meeting status, selected accounts, uniqueness, required roles and permitted grants. (BR-80–BR-85, BR-122, BR-168, BR-174)<br>5. The system saves all selected participants together, initializes Pending responses and arranges invitations when the Meeting is Published or InProgress. No partial batch is saved.<br>6. The system displays MSG96 – “Meeting participant added successfully.” |  |  |
| Alternative Flows | AF-1 – Invite external Guest<br>1. At Step 2, the Secretary selects Invite Guest and enters the invitation name and email, plus any permitted additional Meeting permissions.<br>2. For a Confidential or Secret Meeting, the form displays the inline warning without adding a confirmation step.<br>3. At Step 4, the system validates the invited email and uniqueness against current internal participants and Guest invitations.<br>4. At Step 5, the system creates one Guest invitation with Pending response, saves the selected grants for this Meeting and arranges email delivery when the Meeting is Published or InProgress. The Guest later verifies the email through UC-04.<br>5. The system displays MSG96 – “Meeting participant added successfully.” The use case ends successfully. (BR-84, BR-88, BR-91, BR-148–BR-157, BR-174, BR-180–BR-182)<br>AF-2 – Restore Secretary<br>1. At Step 2, when no current Secretary exists in Draft, Rejected or Approved, the Organizer selects one active internal user. The user may be new to the Meeting or an existing current Member.<br>2. At Step 4, the system verifies the Organizer may restore the missing Secretary and the selected account is active.<br>3. At Step 5, the system adds the selected user as Secretary, or changes the existing Member’s role to Secretary without creating a duplicate or resetting response/attendance. No additional permissions are granted through recovery.<br>4. The system displays MSG100 – “Meeting role updated successfully.” The use case ends successfully. (BR-78, BR-79, BR-122) |  |  |
| Exception Flows | EX-1 – Participant information or selection is invalid1. At Step 4, an internal account is inactive, already current, or has an invalid Meeting Role; or the Guest name/email is missing, invalid or already represented in the Meeting.2. No participant batch or Guest invitation is partially saved. The system displays MSG97 – “Invalid meeting participant information.” with the applicable field message.3. The actor corrects the selection and resumes at Step 2.EX-2 – Additional permission is not allowed1. At Step 4, one or more selected additional Meeting permissions cannot be granted to the selected participant or Guest.2. No participant or permission change is saved. The system displays MSG152 – “One or more selected meeting permissions are not allowed.”3. The actor revises the grants and resumes at Step 2.EX-3 – Participant management is no longer permitted1. At Step 4, the Meeting status or actor assignment no longer permits the requested roster action.2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends unsuccessfully.EX-4 – Addition or recovery cannot be saved1. At Step 5, the system cannot save the internal batch, Guest invitation or Secretary recovery.2. No partial batch or invitation is retained; no invitation email is sent for an unsaved Guest invitation. Existing participant roles and saved response/attendance remain unchanged.3. The system displays MSG99 – “Unable to add the meeting participant. Please try again.” for a failed addition, or MSG102 – “Unable to update the meeting role. Please try again.” when Secretary recovery changes an existing Member’s role. The use case ends unsuccessfully. |  |  |

##### 6.2.2 UC-28b – Manage Meeting Role & Additional Permissions

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to change internal Meeting Roles or eligible additional permissions, including Guest permission-only changes. Before the Meeting is Published, the Secretary may also replace the Chair. From publication onward, only the current Chair can give the Chair role to someone else, through UC-73. (BR-126, BR-294) |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting permits participant management, and the selected target is a current internal participant or Guest invitation. (BR-79, BR-80) |  |  |
| Postconditions | ● POST-1: Approved role or additional-permission changes are saved for this Meeting.<br>● POST-2: A Chair replacement before publication changes both affected internal Meeting Roles together. System Role and Invitation Response remain unchanged. (BR-96, BR-169, BR-177) |  |  |
| Normal Flow | Manage Meeting Role and Additional Permissions<br>1. The Secretary opens a current participant’s role and additional-permission details.<br>2. The system displays the current Meeting Role and additional permissions.<br>3. The Secretary changes the internal participant’s Meeting Role or additional permissions, or selects a Guest invitation to change its additional permissions.<br>4. The Secretary confirms the changes.<br>5. The system rechecks current authority, Meeting status, role constraints and permission grants. Draft, Rejected and Approved may have no Secretary; Published and InProgress must retain or replace the last Secretary in the same save. (BR-122, BR-123, BR-174–BR-177)<br>6. The system saves all requested changes consistently and retains compatible existing grants. Published/InProgress changes trigger the applicable notifications; notification failure does not undo the saved changes.<br>7. The system displays MSG100 – “Meeting role updated successfully.” for role changes or MSG153 – “Meeting permissions updated successfully.” for permission-only changes. |  |  |
| Alternative Flows | AF-1 – Replace Chair<br>1. At Step 3, while the Meeting is not yet Published, the Secretary selects a current internal participant as the new Chair. (BR-294)<br>2. The system changes the former Chair to Member and the selected participant to Chair in the same save, subject to the current Secretary requirements.<br>3. The system notifies both affected participants when required and displays MSG100 – “Meeting role updated successfully.” The use case ends successfully. (BR-92, BR-122, BR-169)<br>AF-2 – Change Guest permissions<br>1. At Step 3, the Secretary selects a current Guest invitation. The system displays its additional Meeting permissions; no internal Meeting Role selector is available.<br>2. The Secretary changes the Guest’s permitted grants and confirms.<br>3. The system saves the grants for this Meeting, preserves Guest identity and access type, and arranges notification when required.<br>4. The system displays MSG153 – “Meeting permissions updated successfully.” The use case ends successfully. (BR-92, BR-174–BR-177, BR-181) |  |  |
| Exception Flows | EX-1 – Meeting Role or additional permission is invalid<br>1. At Step 5, the selected Meeting Role is invalid or one or more additional permissions cannot be granted.<br>2. No requested change is saved. For an invalid role, the system displays MSG101 – “A valid meeting role is required.” For a disallowed permission grant, it displays MSG152 – “One or more selected meeting permissions are not allowed.”<br>3. The Secretary may correct the selection and resume at Step 3.<br>EX-2 – Required Meeting Role would be lost<br>1. At Step 5, the change would violate the Chair/Secretary requirement for the current Meeting status.<br>2. No role or permission change is saved. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.” The Secretary may revise the change and resume at Step 3.<br>EX-3 – Actor, target or Meeting is no longer eligible<br>1. At Step 5, the Secretary assignment, target relationship or Meeting status changed after the details were opened.<br>2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends unsuccessfully.<br>EX-4 – Changes cannot be saved<br>1. At Step 6, the system cannot save the requested role or permission changes.<br>2. All prior roles and permissions remain unchanged; no successful change is announced.<br>3. The system displays MSG102 – “Unable to update the meeting role. Please try again.” The use case ends unsuccessfully.<br>EX-5 – Chair replacement after publication<br>1. At Step 5, the Meeting is Published or InProgress and the change would replace the current Chair, give the Chair role to another participant or change the current Chair to another Meeting Role. (BR-126)<br>2. No role or permission change is saved. The system displays MSG256 – "From publication onward, only the current Chair can hand over the Chair role."<br>3. The Secretary may revise the change and resume at Step 3. |  |  |

##### 6.2.3 UC-28c – Remove Meeting Participant

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an assigned Secretary to remove a current internal participant or revoke a Guest invitation while preserving required Meeting records. |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting permits participant management. The selected internal participant or Guest invitation is current. (BR-79, BR-80) |  |  |
| Postconditions | ● POST-1: The selected participant is no longer current, or the Guest invitation is revoked.<br>● POST-2: Access and additional permissions granted by the removed relationship end; independent valid access remains effective.<br>● POST-3: Invitation Response and attendance history are retained. (BR-97, BR-100–BR-103, BR-177)<br>● POST-4: Agenda Items presented by the removed participant or revoked Guest no longer have a Presenter. (BR-308) |  |  |
| Normal Flow | Remove Meeting Participant<br>1. The Secretary selects a current internal participant or Guest invitation and confirms removal or revocation.<br>2. The system rechecks the Secretary assignment, Meeting status, selected relationship and required Meeting Roles. Draft, Rejected and Approved may temporarily have no Secretary; Published and InProgress must retain the current Chair and retain or replace the last Secretary in the same save. (BR-79, BR-80, BR-122, BR-126)<br>3. The system removes the internal assignment or revokes the Guest invitation, ends access and additional permissions granted by that relationship, and retains historical evidence.<br>4. The system arranges the applicable notification for a Published/InProgress Meeting. Notification failure does not undo the roster change.<br>5. The system displays MSG103 – “Meeting participant removed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Removal is no longer permitted<br>1. At Step 2, the actor is no longer the current Secretary, the Meeting status no longer permits roster changes, or the selected relationship is no longer current.<br>2. Nothing is removed or revoked. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends unsuccessfully.<br>EX-2 – Removal would violate a required Meeting Role<br>1. At Step 2, removing the selected participant would leave a Published/InProgress Meeting without its required Chair or Secretary and no valid replacement is included in the same save.<br>2. The participant or Guest invitation remains current and access remains effective. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.”<br>3. The Secretary revises the requested change to include the required replacement and may resume at Step 1.<br>EX-3 – Removal cannot be saved<br>1. At Step 3, the system cannot remove the assignment or revoke the Guest invitation.<br>2. The relationship, access and grants remain effective; historical records remain unchanged. No success notification is sent.<br>3. The system displays MSG104 – “Unable to remove the meeting participant. Please try again.” The use case ends unsuccessfully. |  |  |

#### 6.3 UC-29 – Respond to Meeting Invitation

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an invitee to accept or decline their own Meeting invitation, or change an earlier response while responses are allowed. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published or InProgress.<br>● PRE-2: The actor owns a current invitation; a Guest has verified access through UC-04. (BR-104–BR-106, BR-148–BR-156) |  |  |
| Postconditions | ● POST-1: The latest response and response time are recorded.<br>● POST-2: Meeting Role, attendance and additional permissions are unchanged. (BR-96, BR-115) |  |  |
| Normal Flow | Respond to Meeting Invitation<br>1. The invitee opens their current Meeting invitation.<br>2. The system identifies the invitee and displays the latest response, if any.<br>3. The invitee selects Accepted or Declined.<br>4. The system rechecks invitation ownership, Meeting status and response value.<br>5. The system saves the latest response and response time.<br>6. The system confirms the update with MSG105 – “Meeting invitation response recorded successfully.” |  |  |
| Alternative Flows | AF-1 – Change previous response1. At Step 2, the invitee has already responded and the Meeting is still Published or InProgress.2. The invitee selects a different response and continues at Step 3. The selected response and current response time replace the prior current values. (BR-106, BR-115) |  |  |
| Exception Flows | EX-1 – Invitation is no longer answerable<br>1. At Step 4, the Meeting status or invitation state no longer permits a response.<br>2. The prior response remains unchanged. For a Guest, email verification and Guest-access failures remain in UC-04.<br>3. The system displays MSG107 – “This meeting invitation can no longer be answered.” The use case ends unsuccessfully.<br>EX-2 – Response value is invalid<br>1. At Step 4, the submitted value is not Accepted or Declined.<br>2. The prior response and response time remain unchanged. The system displays MSG106 – “Invalid meeting invitation response.” The invitee may correct the selection and resume at Step 3.<br>EX-3 – Response cannot be saved<br>1. At Step 5, the system cannot persist the response.<br>2. The prior response and response time remain unchanged.<br>3. The system displays MSG108 – “Unable to save the meeting invitation response. Please try again.” The use case ends unsuccessfully. |  |  |

#### 6.4 UC-30 – View Participant Response Status

| Primary Actors | Secretary, Chair, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows authorized internal participants and verified Guests to view current invitation responses and totals for the Meeting. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published or a later status that remains readable.<br>● PRE-2: The current Secretary or Chair may view by default; a Member or verified current Guest needs View Invitation Responses for this Meeting. (BR-176) |  |  |
| Postconditions | ● POST-1: Latest responses for current invitees and totals by response status are displayed.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Participant Response Status<br>1. The actor selects Response Status for an accessible Meeting.<br>2. The system checks that the actor may view responses. (BR-176)<br>3. The system displays each current invitee’s name, email and latest response with totals by status. Response-view permission includes identity in response rows but does not grant roster browsing or search. (BR-103, BR-115, BR-176)<br>4. The actor reviews the summary. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Responses are not accessible<br>1. At Step 2, the actor lacks current Meeting access or permission to view invitation responses.<br>2. The system does not disclose response identities or totals and returns the generic not-found result required for inaccessible Meeting data. The use case ends unsuccessfully. (BR-121)<br>EX-2 – Responses cannot be retrieved<br>1. At Step 3, the system cannot retrieve the current response data.<br>2. No partial response summary is presented as complete. The system displays MSG95 – “Unable to load meeting participants. Please try again.”<br>3. The use case ends unsuccessfully; the actor may retry by reopening Response Status. |  |  |

#### 6.5 UC-31 – Record Attendance

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to record or revise attendance for one or more selected current internal participants or Guests while the Meeting is InProgress. |  |  |
| Preconditions | ● PRE-1: The actor is an active current assigned Secretary.<br>● PRE-2: The Meeting is InProgress. (BR-170) |  |  |
| Postconditions | ● POST-1: Submitted people have Present or Absent saved with the recording Secretary and save time.<br>● POST-2: Unsubmitted people retain their saved value or remain unmarked; Invitation Responses are unchanged.<br>● POST-3: All submitted changes succeed together or none are saved. Attendance completeness never gates End Meeting. (BR-116, BR-171, BR-172, BR-178) |  |  |
| Normal Flow | Record Attendance<br>1. The Secretary opens Attendance for an InProgress Meeting.<br>2. The system displays current internal participants and Guests with saved attendance; never-recorded people remain unmarked.<br>3. The Secretary selects Present or Absent for one or more people and submits only those entries.<br>4. The system rechecks current authority, Meeting status, submitted people’s current membership and each submitted value. Omitted people are not treated as validation errors. (BR-170–BR-172)<br>5. The system saves all submitted changes together, records the Secretary and save time, preserves every omitted person’s saved or unmarked value, and displays MSG147 – “Meeting attendance saved successfully.” |  |  |
| Alternative Flows | AF-1 – Revise saved attendance<br>1. At Step 3, the Secretary changes the saved attendance for one or more current people.<br>2. The system applies the same validation and atomic-save rules at Steps 4–5. (BR-172)<br>AF-2 – Record the full roster<br>1. At Step 3, the Secretary selects every current internal participant and Guest.<br>2. The system applies the same validation and atomic-save rules at Steps 4–5. A full-roster submission is optional; it is not required to save attendance or end the Meeting. (BR-172) |  |  |
| Exception Flows | EX-1 – Submitted attendance value is invalid<br>1. At Step 4, a submitted value is not Present or Absent.<br>2. No submitted change is saved. The system displays MSG148 – “Select Present or Absent for each submitted participant or Guest.” The Secretary corrects the value and resumes at Step 3.<br>EX-2 – Meeting is no longer InProgress<br>1. At Step 4, the Meeting changed from InProgress before the submission was saved.<br>2. No submitted change is saved. The system displays MSG149 – “Attendance can only be recorded while the meeting is in progress.” The use case ends unsuccessfully.<br>EX-3 – Secretary is no longer authorized<br>1. At Step 4, the actor is no longer the active current assigned Secretary.<br>2. No submitted change is saved. The system denies the operation under current authorization; existing attendance remains unchanged. The use case ends unsuccessfully.<br>EX-4 – A submitted person is no longer current<br>1. At Step 4, one or more submitted internal participants were removed or the Guest invitation was revoked after the roster was displayed.<br>2. No submitted change is saved. The system displays MSG151 – “The meeting participant list has changed. Refresh it and try again.” The Secretary refreshes the roster and resumes at Step 2. Unrelated new roster entries do not invalidate the submission.<br>EX-5 – Attendance cannot be saved<br>1. At Step 5, the system cannot persist all submitted changes.<br>2. Every prior attendance value remains unchanged; no partial attendance changes are saved.<br>3. The system displays MSG150 – “Unable to save attendance. No changes were saved.” The use case ends unsuccessfully. |  |  |

Delivery scope: UC-31 attendance remains in SCRUM-26, separate from SCRUM-20 roster/invitation/response implementation. SCRUM-20 removal/revocation must preserve historical response and attendance evidence. Shared FE-06 documentation does not merge the implementation tasks.

#### 6.6 UC-73 – Hand Over Chair to Another Participant

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to give the Chair role to another internal participant in one step, for example before leaving the Meeting. The system changes both roles together: the selected participant becomes Chair and the former Chair becomes Member. The Chair does not need to change his or her own role separately. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair of the Meeting.<br>● PRE-2: The Meeting is Published or InProgress. (BR-126)<br>● PRE-3: The Meeting has at least one current internal participant who is neither the Chair nor the current Secretary. (BR-295) |  |  |
| Postconditions | ● POST-1: The selected participant is the Chair and the former Chair is a Member, saved together. (BR-169)<br>● POST-2: The Meeting still has exactly one Chair and its required Secretary. System Roles, Invitation Responses, attendance and recorded ballots remain unchanged. (BR-126, BR-169)<br>● POST-3: Both people are notified. (BR-92) |  |  |
| Normal Flow | Hand Over Chair to Another Participant<br>1. The Chair opens the Meeting participant list and selects Hand over Chair.<br>2. The system displays the current internal participants who can receive the Chair role. Guests and the current Secretary are not listed. (BR-295)<br>3. The Chair selects one participant and confirms the hand-over.<br>4. The system rechecks that the actor is still the current Chair, the Meeting is Published or InProgress, and the selected person can still receive the Chair role. (BR-126, BR-295)<br>5. The system changes the selected participant to Chair and the former Chair to Member in the same save. (BR-169)<br>6. The system notifies both people. A notification failure does not undo the saved change. (BR-92)<br>7. The system displays MSG257 – "Chair role handed over successfully." The former Chair stays in the Meeting as a Member and may leave the meeting room. (BR-296) |  |  |
| Alternative Flows | AF-1 – No participant can receive the Chair role<br>1. At Step 2, no current internal participant other than the Chair and the current Secretary exists.<br>2. The system displays MSG258 – "No participant can receive the Chair role. Add an internal participant first." No role changes.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Selected participant can no longer receive the Chair role<br>1. At Step 4, the selected person was removed from the Meeting, became the Secretary or is no longer an active internal user. (BR-295)<br>2. No role is changed. The system displays MSG259 – "The selected participant can no longer receive the Chair role. Select another participant."<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Hand-over is no longer permitted<br>1. At Step 4, the actor is no longer the current Chair, or the Meeting is no longer Published or InProgress.<br>2. No role is changed. The system displays MSG260 – "You can no longer hand over the Chair role for this meeting."<br>3. The use case ends.<br>EX-3 – Hand-over cannot be saved<br>1. At Step 5, the system cannot save both role changes.<br>2. Both people keep their previous roles; no notification is sent. The system displays MSG102 – "Unable to update the meeting role. Please try again."<br>3. The use case ends. |  |  |

### 7. FE-07 – Personal Document Library & Meeting Document Management

ADR-0020 — 15/10/2026: Sixteen detailed Document use cases. UC-33b is reassigned to linking an existing Meeting document to an Agenda Item; UC-33d removes only one Item link. UC-32b remains retired. Meeting-document reading follows Meeting viewing access. Document additions, removals and Item-link changes stop when the Meeting ends; Agenda Item status does not lock document links. PO follow-up: Meeting documents and comments are read-only after the Meeting ends. Deleting an Agenda Item keeps its documents in the Meeting. Readding a removed document does not restore previous comments. Personal-library quotas remain undecided.

#### 7.1 UC-32 – View Meeting Documents

##### 7.1.1 UC-32a – View Meeting Documents

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to browse the documents currently attached to one Meeting. Guest document access follows Participant rules. (BR-181, BR-205) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected Meeting exists and the user can currently view the Meeting. (BR-181, BR-275, BR-153, BR-155, BR-157) |  |  |
| Postconditions | ● POST-1: Only currently attached documents available to the user are displayed.<br>● POST-2: Meeting documents, personal libraries and Meeting status remain unchanged. |  |  |
| Normal Flow | View Meeting Documents<br>1. The user opens the Documents tab of the selected Meeting.<br>2. The system checks the user's current permission to view the Meeting, including the current invitation for a Guest. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. (BR-181, BR-275, BR-200)<br>3. The system displays the Meeting documents grouped by Agenda Item, in agenda order, with their names, file types, the people who added them and comment counts. A document linked to several Agenda Items appears under each of them; a document with no Agenda Item link appears as a general Meeting document. Removed documents are excluded. After the Meeting ends, documents and comments are read-only. (BR-211, BR-277, BR-280, BR-282)<br>4. The user reviews the displayed groups or empty result, and may switch to another grouping or finish viewing. Selecting a document opens UC-32c. |  |  |
| Alternative Flows | AF-1 – View by Vote or general documents<br>1. At Step 4, the user chooses to view the documents attached to Votes, or the general Meeting documents.<br>2. The system displays the documents attached to each Vote of the Meeting, with the user's read confirmation for each, or only the documents with no Agenda Item link. (BR-280, BR-291)<br>3. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – No documents to display<br>1. At Step 3, the Meeting has no currently attached documents, or the grouping chosen in AF-1 has no documents.<br>2. The system displays an empty result and explains that no documents are available for the current grouping.<br>3. The flow resumes at Step 4 of the Main Flow.<br>AF-3 – View documents from the online meeting room<br>1. At Step 1, the user is in the online meeting room of an InProgress Meeting and opens the room's Documents panel instead of the Documents tab.<br>2. The system applies the same access check and displays the same documents, with those of the current Agenda Item first. Adding, linking and removing documents are not offered in the panel. (BR-275, BR-316)<br>3. The flow resumes at Step 4 of the Main Flow; selecting a document opens UC-32c without leaving the room. (BR-315) |  |  |
| Exception Flows | EX-1 – Meeting documents are not accessible<br>1. At Step 2, the Meeting is unavailable or the user can no longer view the Meeting, including removal of a participant or revocation or expiry of a Guest invitation.<br>2. No document names or contents are disclosed. No Meeting information is changed. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Document list cannot be loaded<br>1. At Step 3, the system cannot retrieve the available document list.<br>2. No Meeting information is changed. The system displays MSG157 – “Unable to load the document list. Please try again.”; the failed load is not presented as an empty list.<br>3. The use case ends. |  |  |

##### 7.1.2 UC-32c – Preview Meeting Document

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to read a Meeting document online and download the same file when permitted. Comments are covered separately in UC-35. (BR-181, BR-188) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected document is currently attached to the Meeting and the user can currently view the Meeting. (BR-181, BR-211, BR-275, BR-153, BR-155, BR-157) |  |  |
| Postconditions | ● POST-1: The selected document is displayed for online reading; in AF-1, the user also receives the permitted download.<br>● POST-2: The file content, its Meeting association and its comments remain unchanged.<br>● POST-3: When the user reaches the last page in the viewer, the system records it for that user and document; this enables read confirmation in UC-74. (BR-290) |  |  |
| Normal Flow | Preview Meeting Document<br>1. The user selects a document in the Meeting Documents tab, the documents of an Agenda Item, or the Documents panel of the online meeting room.<br>2. The system checks current permission to view the Meeting and that the document is still attached to this Meeting. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. (BR-181, BR-211, BR-275, BR-200)<br>3. The system opens the document viewer and displays the file name, its content and the Meeting sensitivity. A viewer opened from the online meeting room opens separately, so the user stays in the room. For a Confidential or Secret Meeting, the system records the view in the audit log. (BR-315, BR-318)<br>4. The user reads the document. When the user scrolls to the last page, the system records it and, for a Vote document, offers read confirmation through UC-74. (BR-290) After the Meeting ends, the document and comments are read-only. Downloading remains available when the Meeting is not Secret. (BR-188, BR-282) |  |  |
| Alternative Flows | AF-1 – Download the document<br>1. At Step 4, the user chooses Download for a Meeting that is not Secret.<br>2. The system rechecks current access, that the document remains attached and the current Meeting sensitivity. (BR-181, BR-211, BR-188, BR-275, BR-200)<br>3. The system provides the same uploaded file to the user and, for a Confidential Meeting, records the download in the audit log. (BR-183, BR-216, BR-318)<br>4. The flow resumes at Step 4 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Document is no longer accessible<br>1. At Step 2, or during the access check in AF-1, the document was removed or the user can no longer view the Meeting.<br>2. No further document content or download is provided. The file and Meeting remain unchanged. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Preview cannot be displayed<br>1. At Step 3, the system cannot display the selected file.<br>2. The document remains attached and its content is unchanged. The system displays MSG158 – “Unable to display the document. Please try again.”<br>3. The use case ends.<br>EX-3 – Download is prohibited<br>1. During Step 2 of AF-1, the Meeting is Secret, including when its sensitivity changed after the viewer opened.<br>2. No download is provided. The document remains unchanged. The system displays MSG159 – “Secret Meeting documents can only be viewed online and cannot be downloaded.” (BR-188)<br>3. The flow resumes at Step 4 of the Main Flow for online reading.<br>EX-4 – Download cannot be provided<br>1. At Step 3 of AF-1, the system cannot provide the file.<br>2. The file and its Meeting association remain unchanged. The system displays MSG160 – “Unable to download the document. Please try again.”<br>3. The flow resumes at Step 4 of the Main Flow. |  |  |

#### 7.2 UC-33 – Manage Meeting Documents

##### 7.2.1 UC-33a – Add Document to Meeting

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to add one or more documents from their own personal library or computer to a Meeting in one submission. Linking documents to Agenda Items is handled separately in UC-33b. (BR-184, BR-209, BR-286) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the selected Meeting.<br>● PRE-2: The actor can currently view the Meeting. (BR-184, BR-195)<br>● PRE-3: The Meeting has not ended. (BR-185, BR-195) |  |  |
| Postconditions | ● POST-1: All submitted documents appear in the Meeting as general documents, with their names copied at addition and the actor and addition time recorded. (BR-197, BR-276, BR-286)<br>● POST-2: Selected personal-library documents stay unchanged. Files uploaded from the computer are also available in the uploader's personal library. (BR-194, BR-210)<br>● POST-3: A removed document added again is a new addition without its previous comments. (BR-212, BR-276, BR-284)<br>● POST-4: If any document cannot be added, none of the documents in the submission are added to the Meeting or newly added to the personal library. Existing documents and Meeting status remain unchanged. (BR-286) |  |  |
| Normal Flow | Add Documents from Personal Library<br>1. The Chair or Secretary opens Meeting Documents and chooses Add Documents.<br>2. The system offers selection from the actor's personal library or upload from the computer.<br>3. The actor selects one or more documents from their own library and submits them together.<br>4. The system checks the current Chair or Secretary, Meeting access and status, and ownership and availability of every selected document. It checks that none is already attached to this Meeting. (BR-184, BR-193, BR-195, BR-209)<br>5. The system adds all selected documents to the Meeting under their current names and records who added them and when. Personal-library documents stay unchanged; readded documents do not recover old comments. If any addition fails, none of the submitted additions are saved. (BR-197, BR-276, BR-284, BR-286)<br>6. The system refreshes the document list, shows the number added and displays MSG161 – “Documents added to the Meeting successfully.”. |  |  |
| Alternative Flows | AF-1 – Upload files from the computer<br>1. At Step 3, the actor selects one or more files from the computer and submits them together.<br>2. The system checks every file is a valid PDF or DOCX and each file is no larger than 50 MB, and that the submission has at most 10 files totalling at most 200 MB. It identifies any file that fails validation. (BR-187, BR-214, BR-215, BR-319)<br>3. The system performs the authority, Meeting access and status checks from Step 4 of the Main Flow. (BR-184, BR-195)<br>4. The system adds all submitted files to the Meeting and the uploader's personal library together. A newly created or recreated Meeting folder uses the Meeting title and planned start date in Vietnam time; an existing folder is reused. (BR-192, BR-194, BR-210, BR-226, BR-227, BR-228, BR-286)<br>5. If any file cannot be added, no document from this submission is added to either place; EX-5 applies. Otherwise, the flow resumes at Step 6 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Actor is no longer authorized<br>1. At Step 4, or the corresponding check in AF-1, the actor is no longer the current Chair or Secretary.<br>2. None of the submitted documents are added. The system displays MSG162 – “You no longer have permission to add documents to this Meeting.”<br>3. The use case ends.<br>EX-2 – Library selection is missing or unavailable<br>1. At Step 4, no document was selected, or any selected document is unavailable or does not belong to the actor.<br>2. None are added. The system identifies the unavailable selections within the actor's library and displays MSG249 – “Select one or more available documents from your personal library.”.<br>3. The actor revises the selection. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Uploaded file is invalid<br>1. At Step 2 of AF-1, no file was selected or one or more files are not valid PDF/DOCX files or exceed 50 MB each. (BR-187, BR-214, BR-215)<br>2. None of the submitted documents are added to the Meeting or personal library. The system identifies the invalid files and displays MSG164 – “Select valid PDF or DOCX files, each no larger than 50 MB.”.<br>3. The actor removes or replaces those files. The flow resumes at Step 1 of AF-1.<br>EX-4 – Document is already attached<br>1. At Step 4, one or more selected library documents are already attached to this Meeting. Separate uploads with the same name or content remain different documents. (BR-193, BR-229)<br>2. None of the submitted additions are saved. The system identifies the already attached documents and displays MSG165 – “One or more selected documents are already attached to the Meeting.”.<br>3. The actor revises the selection. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – Addition cannot be completed<br>1. At Step 5 or Step 4 of AF-1, the system cannot complete one or more additions.<br>2. No document from this submission is added to the Meeting or newly added to the personal library. Existing documents remain unchanged. The system displays MSG166 – “Unable to add the documents. No documents were added. Please try again.”. (BR-194, BR-286)<br>3. The use case ends.<br>EX-6 – Meeting has ended<br>1. At Step 4, or the corresponding check in AF-1, the Meeting has ended, including while the actor was selecting files. (BR-195)<br>2. None of the submitted documents are added. The system displays MSG172 – “Documents cannot be added after the Meeting has ended.”<br>3. The use case ends.<br>EX-7 – Meeting is unavailable<br>1. Before displaying or completing the addition, the Meeting cannot be accessed.<br>2. No Meeting information is disclosed and no submitted documents are added. The system displays MSG174 – “This Meeting is not accessible.”<br>3. The use case ends.<br>EX-8 – Upload exceeds the submission limit<br>1. At Step 2 of AF-1, the actor submits more than 10 files, or the files total more than 200 MB. (BR-319)<br>2. None of the submitted documents are added to the Meeting or personal library. The system displays MSG288 – “Upload up to 10 files at a time, no larger than 200 MB in total.”.<br>3. The actor removes some files. The flow resumes at Step 1 of AF-1. |  |  |

##### 7.2.2 UC-33b – Attach Documents to Agenda Item

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to attach one or more documents to an Agenda Item of the Meeting before the Meeting ends. Documents may be chosen from the Meeting documents, uploaded from the computer or chosen from My Documents; documents not yet in the Meeting are added to it and attached in the same save. (BR-304) |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-184)<br>● PRE-2: The selected Agenda Item belongs to the Meeting. (BR-278)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-185, BR-279) |  |  |
| Postconditions | ● POST-1: The selected documents are attached to the Agenda Item; their links to other Items remain unchanged. (BR-277)<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. If any document cannot be added or attached, nothing from the submission is added or attached. (BR-194, BR-286, BR-304)<br>● POST-3: Document contents and comments remain unchanged. |  |  |
| Normal Flow | Attach Documents to Agenda Item<br>1. The actor selects an Agenda Item and chooses Add document.<br>2. The system offers three sources: Meeting documents, Upload from computer and My Documents. (BR-304)<br>3. The actor chooses Meeting documents. The system lists the Meeting documents not yet attached to this Agenda Item.<br>4. The actor selects one or more documents and confirms.<br>5. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the Item belongs to the Meeting, and that every selected document is still in the Meeting and not yet attached to the Item. (BR-184, BR-185, BR-277, BR-278, BR-279)<br>6. The system attaches all selected documents to the Item together; if any cannot be saved, none is attached. (BR-304)<br>7. The system refreshes the Agenda Item documents and displays MSG238 – “Documents attached to the Agenda Item successfully.” |  |  |
| Alternative Flows | AF-1 – Upload from computer<br>1. At Step 3, the actor chooses Upload from computer, selects one or more files and submits them.<br>2. The system checks every file is a valid PDF or DOCX no larger than 50 MB, and performs the checks of Step 5 that do not concern existing Meeting documents. (BR-184, BR-185, BR-187, BR-214, BR-215)<br>3. The system adds the files to the Meeting and to the uploader's personal library, and attaches them to the Agenda Item, in the same save. (BR-194, BR-210, BR-286, BR-304)<br>4. If any file is invalid or cannot be added or attached, nothing from the submission is added or attached; the system displays MSG164 or MSG166 as in UC-33a EX-3 and EX-5. Otherwise, the flow resumes at Step 7.<br>AF-2 – Choose from My Documents<br>1. At Step 3, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the remaining checks of Step 5. (BR-184, BR-185, BR-193, BR-209)<br>3. The system adds the documents to the Meeting and attaches them to the Agenda Item in the same save. Personal-library documents stay unchanged. (BR-276, BR-286, BR-304)<br>4. If any document cannot be added or attached, nothing is added or attached; the system displays MSG249, MSG165 or MSG166 as in UC-33a EX-2, EX-4 and EX-5. A document already in the Meeting is attached through Step 3. Otherwise, the flow resumes at Step 7. |  |  |
| Exception Flows | EX-1 – Actor is no longer authorized<br>1. At Step 5, the actor is no longer the current Chair or Secretary.<br>2. Nothing is attached. The system displays MSG240 – “You no longer have permission to link documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Document is no longer in the Meeting<br>1. At Step 5, a selected document is no longer attached to this Meeting.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The flow resumes at Step 3.<br>EX-3 – Agenda Item is unavailable<br>1. At Step 5, the Agenda Item no longer exists in this Meeting. (BR-278)<br>2. Nothing is attached. The system displays MSG242 – “Select an available Agenda Item in this Meeting.”<br>3. The use case ends.<br>EX-4 – Document is already attached<br>1. At Step 5, one or more selected documents are already attached to the Item, including by another user after selection.<br>2. Nothing is attached and no duplicate is created. The system displays MSG243 – “One or more selected documents are already attached to the Agenda Item.”<br>3. The flow resumes at Step 4.<br>EX-5 – Change cannot be saved<br>1. At Step 6, the system cannot complete the change.<br>2. All links, the Meeting documents and their comments remain unchanged. The system displays MSG244 – “Unable to link the document. No links were changed. Please try again.”<br>3. The use case ends.<br>EX-6 – Meeting has ended<br>1. At Step 5, or the corresponding check in AF-1 or AF-2, the Meeting has ended. (BR-185)<br>2. Nothing is added or attached. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends.<br>EX-7 – Meeting is unavailable<br>1. Before displaying or saving, the Meeting cannot be accessed.<br>2. No document or Item information is disclosed and nothing is attached. The system displays MSG174 – “This Meeting is not accessible.”<br>3. The use case ends. |  |  |

##### 7.2.3 UC-33c – Remove Document from Meeting

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to remove a Meeting document before the Meeting ends, regardless of Minutes existence or Agenda Item status. Removal affects every Agenda Item link in this Meeting. (BR-184, BR-185, BR-279, BR-281) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the Meeting. (BR-184)<br>● PRE-2: The selected document is currently attached to this Meeting.<br>● PRE-3: The actor can currently view the Meeting and the Meeting has not ended. (BR-184, BR-185) |  |  |
| Postconditions | ● POST-1: The document no longer appears in this Meeting's current document list or any of its Agenda Items and is no longer available through this attachment. (BR-281)<br>● POST-2: Its comments remain in the removal history and do not return if the document is added again; personal-library documents and documents in other Meetings remain unchanged. (BR-212, BR-231, BR-197, BR-281, BR-284)<br>● POST-3: The actor and removal time are recorded. Meeting lifecycle status is unchanged. |  |  |
| Normal Flow | Remove Document from Meeting<br>1. The Chair or Secretary selects an attached document and chooses Remove from Meeting.<br>2. The system displays a confirmation naming the document and explaining that it will leave this Meeting's list, while its comments remain as history and personal-library documents and other Meetings are unaffected.<br>3. The actor confirms removal.<br>4. The system rechecks the current Chair or Secretary assignment, that the document is still attached, that the actor can currently view the Meeting and that the Meeting has not ended. Minutes existence and Agenda Item status do not block removal. (BR-184, BR-185, BR-279)<br>5. The system removes the document from this Meeting's current list and every Agenda Item in this Meeting, preserves its comments as history and records the actor and removal time. (BR-211, BR-212, BR-231, BR-197, BR-281, BR-284)<br>6. The system displays MSG167 – “Document removed from the Meeting successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting has ended<br>1. At Step 4, the Meeting has ended, including when it ended after the confirmation was displayed. (BR-185)<br>2. The document remains attached and its comments remain unchanged. The system displays MSG248 – “Documents cannot be removed after the Meeting has ended.”<br>3. The use case ends.<br>EX-2 – Actor is no longer authorized<br>1. At Step 4, the actor is no longer the current Chair or Secretary. (BR-184)<br>2. No document is removed and comments remain unchanged. The system displays MSG169 – “You no longer have permission to remove documents from this Meeting.”<br>3. The use case ends.<br>EX-3 – Document is no longer attached<br>1. At Step 4, the selected document is no longer currently attached to this Meeting.<br>2. No further removal or comment change is made. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The use case ends; the actor may refresh the list through UC-32a.<br>EX-4 – Removal cannot be completed<br>1. At Step 5, the system cannot complete the removal.<br>2. The document remains attached and its comments remain unchanged; no successful removal is recorded. The system displays MSG171 – “Unable to remove the document. It remains attached to the Meeting. Please try again.”<br>3. The use case ends.<br>EX-5 – Meeting is unavailable<br>1. Before displaying or completing the removal, the selected Meeting cannot be accessed.<br>2. No document is removed and comments remain unchanged. The system displays MSG174 – “This Meeting is not accessible.” without disclosing whether the Meeting exists.<br>3. The use case ends. |  |  |

##### 7.2.4 UC-33d – Detach Document from Agenda Item

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to detach one document from an Agenda Item before the Meeting ends. The document stays in the Meeting; removing it from the Meeting is UC-33c. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-184)<br>● PRE-2: The document is attached to the selected Agenda Item of the same Meeting. (BR-278)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-185, BR-279) |  |  |
| Postconditions | ● POST-1: Only the selected link is removed; the document remains in the Meeting and any other Agenda Item links remain unchanged. With no links left, it is a general Meeting document. (BR-280)<br>● POST-2: The document, its comments and its other Agenda Item links remain unchanged. |  |  |
| Normal Flow | Detach Document from Agenda Item<br>1. The actor selects a document attached to an Agenda Item, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the document is still in the Meeting, that the Item belongs to the Meeting, and that the link still exists. (BR-184, BR-185, BR-277, BR-278, BR-279)<br>3. The system removes only that link; the document stays in the Meeting with its other links unchanged, or becomes a general Meeting document when no links remain. (BR-280)<br>4. The system refreshes the Agenda Item documents and displays MSG239 – “Document detached from the Agenda Item successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is no longer authorized<br>1. At Step 2, the actor is no longer the current Chair or Secretary.<br>2. No links are changed. The system displays MSG241 – “You no longer have permission to unlink documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Document is no longer in the Meeting<br>1. At Step 2, the document is no longer attached to this Meeting.<br>2. No links are changed. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The use case ends.<br>EX-3 – Agenda Item is unavailable<br>1. At Step 2, the Agenda Item no longer exists in this Meeting. (BR-278)<br>2. No links are changed. The system displays MSG242 – “Select an available Agenda Item in this Meeting.”<br>3. The use case ends.<br>EX-4 – Link no longer exists<br>1. At Step 2, the link has already been removed.<br>2. No further links are changed and the document remains in the Meeting. The system displays MSG246 – “This document is no longer linked to the selected Agenda Item. Refresh the document list.”<br>3. The use case ends.<br>EX-5 – Change cannot be saved<br>1. At Step 3, the system cannot complete the change.<br>2. All links, the Meeting document and its comments remain unchanged. The system displays MSG245 – “Unable to unlink the document. No links were changed. Please try again.”<br>3. The use case ends.<br>EX-6 – Meeting has ended<br>1. At Step 2, the Meeting has ended, including when it ended after selection. (BR-185)<br>2. No links are changed. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends.<br>EX-7 – Meeting is unavailable<br>1. Before displaying or saving, the Meeting cannot be accessed.<br>2. No document or Item information is disclosed and no links are changed. The system displays MSG174 – “This Meeting is not accessible.”<br>3. The use case ends. |  |  |

#### 7.3 UC-34 – View Personal Document Library

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to browse their own folders and documents and see permitted Meeting-usage details. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |  |  |
| Postconditions | ● POST-1: The user sees only their own library items and permitted Meeting-usage details.<br>● POST-2: Documents, folders and Meeting attachments remain unchanged. |  |  |
| Normal Flow | View Personal Document Library<br>1. The user opens Personal Document Library.<br>2. The system checks personal-library access. (BR-204, BR-242)<br>3. The system displays the user's folders and documents with names, file types and permitted Meeting-usage details. Other users' items and unauthorized Meeting details are excluded. (BR-219, BR-242, BR-251)<br>4. The user selects a folder or the library root.<br>5. The system displays the documents at that location. The user may browse the list or empty result, change the location or finish viewing. |  |  |
| Alternative Flows | AF-1 – Search or filter documents<br>1. At Step 5, the user enters a document-name search or selects a file-type filter.<br>2. The system applies the criteria within the user's own displayed library location.<br>3. The flow resumes at Step 5 of the Main Flow.<br>AF-2 – Empty library or no matching documents<br>1. At Step 3 or Step 5, no documents match the location and criteria.<br>2. The system displays an empty result without treating it as a loading failure.<br>3. The flow resumes at Step 5 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Library cannot be loaded<br>1. At Step 2, Step 3 or Step 5, the system cannot retrieve the library.<br>2. No library or Meeting information changes. The system displays MSG176 – “Unable to load your personal document library. Please try again.”<br>3. The use case ends.<br>EX-2 – Selected folder is unavailable<br>1. At Step 4, the folder was deleted or is no longer available in the user's library.<br>2. No information changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The user returns to the library root. The flow resumes at Step 4 of the Main Flow. |  |  |

#### 7.4 UC-35 – Comment on Meeting Document

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user who can view a Meeting document to read its comments and add a comment before the Meeting ends. After the Meeting ends, the document and comments are read-only. (BR-249, BR-282) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a current valid invitation. (BR-153, BR-155, BR-157)<br>● PRE-2: The document is currently in the Meeting and the user can view that Meeting. (BR-275)<br>● PRE-3: The Meeting has not ended. (BR-282) |  |  |
| Postconditions | ● POST-1: The new comment is displayed with its author and posting time, and the comment count is updated. (BR-250)<br>● POST-2: Existing comments and the document remain unchanged. |  |  |
| Normal Flow | Comment on Meeting Document<br>1. The user opens a Meeting document and its comments.<br>2. The system checks viewing access and displays the existing comments. (BR-275)<br>3. The user enters a comment and chooses Send.<br>4. The system checks that the user may still view the document, the Meeting has not ended, and the comment is not blank. (BR-246, BR-249, BR-282)<br>5. The system records the comment with its author and posting time; a Guest author is recorded by their invitation, shown with the name they entered. A PDF comment may refer to a page; a DOCX comment has no page reference. (BR-248, BR-250)<br>6. The system displays the new comment, updates the comment count and displays MSG199 – “Comment added successfully.” |  |  |
| Alternative Flows | AF-1 – Include a PDF page reference<br>1. At Step 3, the user comments on a page being viewed in a PDF.<br>2. The comment includes that page reference; a page reference is not required for a document-wide comment.<br>3. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – Reply to a comment<br>1. At Step 3, the user chooses Reply on an existing comment and enters the reply.<br>2. The reply is attached to the top-level comment of that thread in the same Meeting document; replying to a reply joins the same thread. (BR-317)<br>3. The flow resumes at Step 4 of the Main Flow; the reply is shown under its thread. |  |  |
| Exception Flows | EX-1 – Comment is empty<br>1. At Step 4, the text is blank or contains only whitespace. (BR-246)<br>2. No comment is saved. The system displays MSG200 – “Enter a comment.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Document is no longer available<br>1. At Step 2 or Step 4, the document is no longer attached, or the user can no longer view it, including after Guest invitation revocation or expiry.<br>2. No comment is saved. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. No new comment is saved; existing comments remain unchanged. The system displays MSG202 – “Unable to add the comment. No comment was saved. Please try again.”<br>3. The use case ends.<br>EX-4 – Comments cannot be loaded<br>1. At Step 2, current document access is valid but the system cannot retrieve comments.<br>2. No comment or document changes. The system displays MSG204 – “Unable to load the document comments. Please try again.”<br>3. The use case ends.<br>EX-5 – Meeting has ended<br>1. At Step 2 or Step 4, the Meeting has ended, including when it ended while the user was writing. (BR-282)<br>2. No comment is added. Existing comments remain available for reading. The system displays MSG251 – “Comments are read-only after the Meeting has ended.”<br>3. The use case ends. |  |  |

#### 7.5 UC-36 – Upload Document to Personal Library

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to upload one or more PDF or DOCX files together to the selected location in their personal library. Uploading to the library does not add them to a Meeting. (BR-286) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |  |  |
| Postconditions | ● POST-1: All submitted files are available as separate documents at the selected library location. (BR-229, BR-286)<br>● POST-2: If any file cannot be added, none of the documents in this submission are added. Existing library documents and Meeting documents remain unchanged. (BR-286) |  |  |
| Normal Flow | Upload Documents to Personal Library<br>1. The user opens their library root or a folder and chooses Upload.<br>2. The user selects one or more files from the computer and submits them together.<br>3. The system checks library access, the selected destination, and every file. Each file must be a valid PDF or DOCX and no larger than 50 MB. (BR-187, BR-214, BR-215, BR-242)<br>4. The system adds all submitted files as separate documents at that location with their original names and content. If any addition fails, none of the submitted documents are added. (BR-183, BR-216, BR-229, BR-286)<br>5. The system refreshes the library, shows the number added and displays MSG177 – “Documents uploaded to your personal library successfully.”. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – File selection is invalid<br>1. At Step 3, no file was selected, or one or more files are not valid PDF/DOCX files or exceed 50 MB each. (BR-187, BR-214, BR-215)<br>2. None of the submitted documents are added. The system identifies the invalid files and displays MSG164 – “Select valid PDF or DOCX files, each no larger than 50 MB.”.<br>3. The actor revises the selection. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Destination is unavailable<br>1. At Step 3, the selected folder was deleted or does not belong to the user. (BR-242)<br>2. None of the submitted documents are added. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The user selects an available destination. The flow resumes at Step 1 of the Main Flow.<br>EX-3 – Upload cannot be completed<br>1. At Step 4, the system cannot add one or more submitted files.<br>2. None of the submitted documents are added. Existing library and Meeting documents remain unchanged. The system displays MSG178 – “Unable to upload the files. No documents were added. Please try again.”. (BR-286)<br>3. The use case ends. |  |  |

#### 7.6 UC-71 – Manage Personal Documents

##### 7.6.1 UC-71a – Delete Document from Personal Library

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to delete a document from their personal library without removing documents already attached to Meetings. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The selected document is in the user's library. |  |  |
| Postconditions | ● POST-1: The document no longer appears in the personal library.<br>● POST-2: Documents already added to Meetings remain available to their authorized readers. Their history and comments are preserved. (BR-186, BR-232) |  |  |
| Normal Flow | Delete Document from Personal Library<br>1. The user selects a personal-library document and chooses Delete.<br>2. The system requests confirmation, naming the document and explaining that deletion affects the personal library only.<br>3. The user confirms deletion.<br>4. The system rechecks ownership and that the library document still exists. (BR-242)<br>5. The system deletes the personal-library document without removing Meeting attachments. (BR-186)<br>6. The system refreshes the library and displays MSG180 – “Document deleted from your personal library successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No library document is deleted; Meeting documents remain unchanged. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The library document remains available and Meeting documents remain unchanged. The system displays MSG181 – “Unable to delete the document. Your personal library remains unchanged.”<br>3. The use case ends. |  |  |

##### 7.6.2 UC-71b – Rename Document

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to rename a personal-library document without changing its file content. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The document exists in the user's library. |  |  |
| Postconditions | ● POST-1: The document is displayed under the new name in the personal library.<br>● POST-2: File content and library location remain unchanged. Names already copied into Meetings remain unchanged. (BR-183, BR-276) |  |  |
| Normal Flow | Rename Document<br>1. The user chooses Rename for a personal-library document.<br>2. The system displays its current name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership and document availability. (BR-242)<br>5. The system saves the new personal-library document name. File content and names already copied into Meetings remain unchanged. (BR-183, BR-224, BR-276)<br>6. The system refreshes the displayed name and displays MSG183 – “Document renamed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No document name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The previous name, content and Meeting attachments remain unchanged. The system displays MSG184 – “Unable to rename the document. Its name remains unchanged.”<br>3. The use case ends. |  |  |

##### 7.6.3 UC-71c – Move Document to Folder

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to move a personal-library document to another owned folder or the library root. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The document exists in the user's library. |  |  |
| Postconditions | ● POST-1: The document is available at the selected library location.<br>● POST-2: Document name, file content and Meeting attachments remain unchanged. (BR-225, BR-245) |  |  |
| Normal Flow | Move Document to Folder<br>1. The user chooses Move for a personal-library document.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system rechecks document ownership and availability and destination ownership and availability. (BR-225, BR-242)<br>5. The system moves only the library document to that location. (BR-245)<br>6. The system refreshes the library and displays MSG185 – “Document moved successfully.” |  |  |
| Alternative Flows | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of a folder.<br>2. The document is moved outside all folders. (BR-225)<br>3. The flow resumes at Step 4 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Document is unavailable<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. The document remains at its previous location. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Destination is unavailable<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-225, BR-242)<br>2. The document stays at its previous location. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The document stays at its previous location and its Meeting attachments are unchanged. The system displays MSG186 – “Unable to move the document. Its location remains unchanged.”<br>3. The use case ends. |  |  |

#### 7.7 UC-72 – Manage Library Folders

##### 7.7.1 UC-72a – Create Folder

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to create a folder at the library root or inside an owned folder. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242) |  |  |
| Postconditions | ● POST-1: A new owned folder is available at the selected location.<br>● POST-2: Existing folders, documents and Meeting attachments remain unchanged. |  |  |
| Normal Flow | Create Folder<br>1. The user selects the library root or an owned parent folder and chooses New Folder.<br>2. The user enters a folder name and submits it.<br>3. The system checks ownership, parent availability, a nonblank name and a resulting depth of at most five levels. Duplicate folder names are permitted. (BR-191, BR-219, BR-220, BR-221, BR-242)<br>4. The system creates the folder at that location.<br>5. The system refreshes the folder list and displays MSG187 – “Folder created successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder name is blank<br>1. At Step 3, the name is empty or contains only whitespace. (BR-220)<br>2. No folder is created. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Parent is unavailable<br>1. At Step 3, the selected parent was deleted or does not belong to the user. (BR-219, BR-242)<br>2. No folder is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-3 – Folder depth exceeds the limit<br>1. At Step 3, the new folder would be deeper than level five. (BR-191)<br>2. No folder is created. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The user selects a shallower parent. The flow resumes at Step 1 of the Main Flow.<br>EX-4 – Operation cannot be completed<br>1. At Step 4, the system cannot complete the operation.<br>2. No folder is created; existing library items remain unchanged. The system displays MSG190 – “Unable to create the folder. No folder was created.”<br>3. The use case ends. |  |  |

##### 7.7.2 UC-72b – Rename Folder

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to rename a folder without moving it or changing its contents. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists in the user's library. |  |  |
| Postconditions | ● POST-1: The folder displays the new name.<br>● POST-2: Its location and contents remain unchanged; an automatic Meeting folder remains reusable. (BR-227) |  |  |
| Normal Flow | Rename Folder<br>1. The user chooses Rename for an owned folder.<br>2. The system displays the current folder name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership, availability and a nonblank name; duplicate names are permitted. (BR-219, BR-220, BR-221, BR-242)<br>5. The system saves the new folder name without changing its contents or location.<br>6. The system refreshes the folder name and displays MSG191 – “Folder renamed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder name is blank<br>1. At Step 4, the name is empty or contains only whitespace. (BR-220)<br>2. The previous name remains. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder name, location and contents remain unchanged. The system displays MSG192 – “Unable to rename the folder. Its name remains unchanged.”<br>3. The use case ends. |  |  |

##### 7.7.3 UC-72c – Move Folder

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to move a folder with its contents to another owned folder or the library root. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists in the user's library. |  |  |
| Postconditions | ● POST-1: The folder and its contents are available at the selected location, within five levels.<br>● POST-2: Folder and document names, file content and Meeting attachments remain unchanged. An automatic Meeting folder remains reusable. (BR-191, BR-227) |  |  |
| Normal Flow | Move Folder<br>1. The user chooses Move for an owned folder.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system checks that the user owns the selected folder and destination and that both are available. The destination cannot be the selected folder or one of its subfolders, and the moved folder and its subfolders must remain within five levels. (BR-191, BR-219, BR-222, BR-242)<br>5. The system moves the folder with its documents and subfolders, without changing their names or Meeting attachments.<br>6. The system refreshes the folder tree and displays MSG194 – “Folder moved successfully.” |  |  |
| Alternative Flows | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of another folder.<br>2. The moved folder becomes a first-level folder; its subfolders remain subject to the depth limit. (BR-191)<br>3. The flow resumes at Step 4 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder or contained item moves. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Destination is unavailable<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-219, BR-242)<br>2. No folder or contained item moves. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-3 – Destination is the same folder or one of its subfolders<br>1. At Step 4, the destination is the folder itself or one of its subfolders. (BR-222)<br>2. No folder or contained item moves. The system displays MSG193 – “A folder cannot be moved into itself or one of its descendants.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-4 – Resulting depth exceeds the limit<br>1. At Step 4, the moved folder or one of its subfolders would be deeper than level five. (BR-191)<br>2. No folder or contained item moves. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-5 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder and all contained items remain at their previous locations. The system displays MSG195 – “Unable to move the folder. Its location remains unchanged.”<br>3. The use case ends. |  |  |

##### 7.7.4 UC-72d – Delete Folder

| Primary Actors | Internal user | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to delete an empty personal-library folder. If an automatic Meeting folder is deleted, a later direct Meeting upload recreates it. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)<br>● PRE-3: The folder exists and contains no documents or subfolders. (BR-223) |  |  |
| Postconditions | ● POST-1: The empty folder no longer appears in the library.<br>● POST-2: Other folders, documents and Meeting attachments remain unchanged. Future recreation of an automatic Meeting folder follows its naming and location rules. (BR-192, BR-228) |  |  |
| Normal Flow | Delete Folder<br>1. The user chooses Delete for an owned folder.<br>2. The system asks for confirmation and names the folder.<br>3. The user confirms deletion.<br>4. The system rechecks current ownership and availability and that the folder contains neither documents nor subfolders. (BR-219, BR-223, BR-242)<br>5. The system deletes only the empty folder.<br>6. The system refreshes the folder tree and displays MSG197 – “Folder deleted successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder is unavailable<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder is deleted. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder is no longer empty<br>1. At Step 4, the folder contains a document or subfolder, including an item added after confirmation was displayed. (BR-223)<br>2. The folder and its contents remain unchanged. The system displays MSG196 – “Only an empty folder can be deleted.”<br>3. The use case ends.<br>EX-3 – Operation cannot be completed<br>1. At Step 5, the system cannot complete the operation.<br>2. The folder and all other library items remain unchanged. The system displays MSG198 – “Unable to delete the folder. The folder remains unchanged.”<br>3. The use case ends. |  |  |

#### 7.8 UC-74 – Confirm Document Read

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who may vote to confirm that he or she has read a document linked to a Vote of the Meeting, after scrolling to its last page. The confirmation is required before casting a ballot in UC-55. It records that the person confirmed reading, not how carefully the document was read. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor can currently view the Meeting, and the document is attached to the Meeting and linked to a Vote of that Meeting. (BR-181, BR-275, BR-287)<br>● PRE-3: The actor has opened the document in the viewer through UC-32c. |  |  |
| Postconditions | ● POST-1: A read confirmation for the actor and the document is recorded with the confirmation time. (BR-290)<br>● POST-2: The confirmation counts for every Vote of the Meeting that links the same document and cannot be withdrawn. (BR-291)<br>● POST-3: No ballot, Vote or document content changes, and the confirmation is not linked to any ballot choice. (BR-293) |  |  |
| Normal Flow | Confirm Document Read<br>1. From the Vote document list or the Meeting documents, the actor opens a Vote document through UC-32c.<br>2. The actor scrolls through the document to its last page.<br>3. The system records that the actor reached the last page and enables the "I have read this document" checkbox. (BR-290)<br>4. The actor ticks "I have read this document".<br>5. The system rechecks current Meeting access, that the document is still attached to the Meeting and linked to a Vote, and that the actor reached the last page. (BR-181, BR-275, BR-287, BR-290)<br>6. The system records the read confirmation with the actor, the document and the confirmation time. (BR-290, BR-291, BR-293)<br>7. The system displays MSG253 – "Document read confirmed." and updates the read status in the Vote document list. When every document of an Open Vote is confirmed, Submit ballot becomes available in UC-55. (BR-289) |  |  |
| Alternative Flows | AF-1 – Document already confirmed<br>1. At Step 1, the actor has already confirmed reading this document, including for another Vote of the Meeting that links it.<br>2. The system shows the document as read with its confirmation time and does not show the checkbox again. (BR-291)<br>3. The use case ends successfully without a new confirmation. |  |  |
| Exception Flows | EX-1 – Last page not reached<br>1. At Step 4 or Step 5, the actor has not scrolled to the last page, including when the viewer was closed before the last page.<br>2. No confirmation is recorded. The system displays MSG254 – "Scroll to the last page of the document before confirming that you have read it."<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-2 – Document or Meeting is no longer accessible<br>1. At Step 5, the actor can no longer view the Meeting, or the document is no longer attached to the Meeting or linked to a Vote.<br>2. No confirmation is recorded. The system displays MSG156 – "Meeting documents are not accessible."<br>3. The use case ends.<br>EX-3 – Confirmation cannot be saved<br>1. At Step 6, the system cannot save the confirmation.<br>2. No confirmation is recorded and the read status is unchanged. The system displays MSG255 – "Unable to confirm document reading. Please try again."<br>3. The use case ends. |  |  |

### 8. FE-08 – Online Meeting Room Management

#### 8.1 UC-37 – Join Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

#### 8.2 UC-38 – Leave Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

#### 8.3 UC-39 – Manage Meeting Room Controls

##### 8.3.1 UC-39a – Manage Microphone

Placeholder only – detailed Use Case Specification will be added later.

##### 8.3.2 UC-39b – Manage Camera

Placeholder only – detailed Use Case Specification will be added later.

##### 8.3.3 UC-39c – Manage Screen Sharing

Placeholder only – detailed Use Case Specification will be added later.

#### 8.4 UC-40 – View Meeting Room Participants

Placeholder only – detailed Use Case Specification will be added later.

#### 8.5 UC-41 – Moderate Meeting Room

Placeholder only – detailed Use Case Specification will be added later.

#### 8.6 UC-42 – Use In-room Chat

Placeholder only – detailed Use Case Specification will be added later.

### 9. FE-09 – Meeting Audio Upload & Media Library

#### 9.1 UC-43 – View & Play Meeting Audio

##### 9.1.1 UC-43a – View Media Library

Placeholder only – detailed Use Case Specification will be added later.

##### 9.1.2 UC-43b – Play Meeting Audio

Placeholder only – detailed Use Case Specification will be added later.

#### 9.2 UC-44 – Upload Meeting Audio

Placeholder only – detailed Use Case Specification will be added later.

### 10. FE-10 – Speaker-Attributed Transcription & Meeting Summary

#### 10.1 UC-45 – View Speaker-Attributed Transcript

Placeholder only – detailed Use Case Specification will be added later.

#### 10.2 UC-46 – Edit Transcript & Speaker Attribution

Placeholder only – detailed Use Case Specification will be added later.

#### 10.3 UC-47 – Generate / Regenerate Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

#### 10.4 UC-48 – View Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

#### 10.5 UC-49 – Edit Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

#### 10.6 UC-50 – Approve Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

#### 10.7 UC-51 – Publish Meeting Summary

Placeholder only – detailed Use Case Specification will be added later.

### 11. FE-11 – Voting Management

Scope: the current Chair or the current Secretary may prepare Vote drafts while the Meeting is Approved, Published or InProgress. Only the current Chair opens a Vote, and opening and casting require InProgress. Each Meeting permits at most one Open Vote, and opening requires at least one eligible voter. End Meeting is independent of Vote closure; the Chair may close and tally an existing Open Vote and record its conclusion in InProgress or Ended. Each Vote belongs directly to its Meeting. No post-meeting ballot casting, time-boxed voting, quorum or separate Resolution is provided. (BR-159, BR-160, BR-165, BR-252, BR-266, BR-267, BR-270)

#### 11.1 UC-52 – View Meeting Votes

##### 11.1.1 UC-52a – View Meeting Votes

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to browse the Votes of a Meeting. Draft Votes are visible only to the current Chair and the current Secretary. Open and Closed Votes remain viewable by users with Meeting viewing permission, including after the Meeting ends. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-29, BR-181, BR-274) |  |  |
| Postconditions | ● POST-1: The actor sees only Votes that the actor is authorized to view.<br>● POST-2: No Vote, ballot or conclusion is changed. |  |  |
| Normal Flow | View Meeting Votes<br>1. The actor opens the Voting area of a Meeting.<br>2. The system checks current viewing permission; the current Chair or Secretary may access Vote preparation in Approved without gaining access to other restricted Meeting records. (BR-29, BR-181, BR-274)<br>3. The system lists the Votes belonging to this Meeting and excludes Draft Votes unless the actor is the current Chair or the current Secretary. Displayed counts include only visible Votes. (BR-253, BR-285)<br>4. The system displays each visible Vote with its content, ballot type and status. Open Votes show turnout; Closed Votes offer View result and show any recorded conclusion. No Agenda Item is selected or displayed as Vote ownership. (BR-260, BR-285)<br>5. The actor browses the Votes. Selecting a Vote continues to UC-52b – View Vote Status. |  |  |
| Alternative Flows | AF-1 – No visible Votes<br>1. At Step 3, no Vote is visible to the actor, including when only Draft Votes restricted to the Chair and Secretary exist.<br>2. The system displays MSG207 – "No votes to display." without revealing hidden Draft Votes.<br>3. No data changes. The use case ends successfully. |  |  |
| Exception Flows | EX-1 – Meeting votes are not accessible<br>1. At Step 2, the Meeting is unavailable or the actor no longer has permission to view its Votes, including after Guest invitation expiry or revocation.<br>2. The system displays MSG205 – "Meeting votes are not accessible." and does not reveal Vote information.<br>3. No data changes. The use case ends.<br>EX-2 – Votes cannot be loaded<br>1. At Step 3, current access is valid but the system cannot retrieve the Votes.<br>2. The system displays MSG206 – "Unable to load the meeting votes. Please try again."<br>3. No data changes. The use case ends. |  |  |

##### 11.1.2 UC-52b – View Vote Status

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view the configuration of a visible Draft Vote, the turnout of an Open Vote, or the tally and any Chair conclusion of a Closed Vote. Viewing a result does not change that result. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-29, BR-181, BR-274)<br>● PRE-3: The selected Vote belongs to that Meeting and is visible to the actor. Draft visibility is restricted to the current Chair and the current Secretary. (BR-253) |  |  |
| Postconditions | ● POST-1: The current Vote status and the information allowed for its ballot type are displayed.<br>● POST-2: No Vote, ballot or conclusion is changed. |  |  |
| Normal Flow | View Open Vote Status<br>1. The actor selects an Open Vote from the Meeting Voting area.<br>2. The system verifies current Vote viewing permission and that the Vote is visible to the actor. (BR-29, BR-181, BR-253, BR-274)<br>3. The system displays its Meeting, content, options, public or secret ballot type, selected roles, opening time, the fixed eligible-voter count and its Vote documents. (BR-288) (BR-190, BR-285)<br>4. The system displays the number of ballots recorded against that fixed count. While the Vote is Open, individual choices and results per option are not displayed. (BR-260)<br>5. The system updates turnout as ballots are recorded. For a secret Vote, the update does not identify the person who just voted or link that person to a choice. (BR-259)<br>6. If the Meeting has ended while the Vote remains Open, the system shows that no further ballots are accepted; the recorded turnout remains viewable. (BR-165) |  |  |
| Alternative Flows | AF-1 – View a Draft Vote<br>1. At Step 1, the current Chair or Secretary selects a Draft Vote.<br>2. The system applies Step 2, then displays its saved content, options, ballot type, selected roles and Vote documents. No eligible-voter list has been fixed and no ballots exist yet. (BR-190, BR-253)<br>3. The use case ends successfully without changing the Draft Vote.<br>AF-2 – View a Closed Vote<br>1. At Step 1, the actor selects View result for a Closed Vote, including a Vote with a recorded conclusion.<br>2. The system applies Step 2, then displays opening and closing times, the fixed eligible count, recorded-ballot count and tally per option. Percentages use the ballots cast; with no ballots, counts are zero and no percentage is shown. (BR-261)<br>3. For a public Vote, the system also displays each eligible voter's choice or that the person did not vote. For a secret Vote, it displays only totals and no voter-choice links. (BR-259, BR-273)<br>4. The system displays any saved Chair conclusion and optional note, or states that a conclusion has not yet been recorded. The system does not determine the conclusion from the tally. (BR-161, BR-263)<br>5. The use case ends successfully without changing the result. |  |  |
| Exception Flows | EX-1 – Vote is not accessible<br>1. At Step 2, the Meeting or Vote is unavailable, access has been lost, or the actor attempts to view a Draft Vote without being the current Chair or Secretary.<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing the Vote.<br>3. No data changes. The use case ends.<br>EX-2 – Status or result cannot be loaded<br>1. At Step 3 or in AF-1/AF-2, current access is valid but the system cannot retrieve the status or result.<br>2. The system displays MSG208 – "Unable to load the vote status or result. Please try again."<br>3. The Vote, ballots and conclusion remain unchanged. The use case ends. |  |  |

#### 11.2 UC-53 – Create / Configure Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to prepare a Vote draft and to configure or delete an existing draft. Draft preparation is allowed before the Meeting starts, while it is Approved or Published, and during InProgress. Only the current Chair may proceed from the form to opening the Vote during the Meeting (UC-54). Each Vote belongs to the Meeting and is not linked to an Agenda Item. Vote documents are attached and detached through UC-76. (BR-160, BR-285) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair or Secretary of the Meeting.<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-252)<br>● PRE-3: For editing or deletion, the selected Vote is Draft and belongs to this Meeting. (BR-254, BR-189) |  |  |
| Postconditions | ● POST-1: Saving a draft creates or updates a Draft Vote; deleting removes only the selected Draft Vote.<br>● POST-2: Opening through UC-54 instead creates or updates an Open Vote and fixes its eligible-voter list. It does not also produce a separate saved draft.<br>● POST-3: Other Votes, previously recorded ballots and conclusions remain unchanged. |  |  |
| Normal Flow | Create Vote Draft<br>1. The actor selects Create Vote in the Meeting Voting area.<br>2. The system checks that the actor is the current Chair or Secretary and the permitted Meeting status, then displays a form for content, options, public or secret ballot type and eligible meeting roles. No Agenda Item is requested. (BR-252, BR-285) It defaults to Public, the options Agree / Disagree / Abstain and all four roles selected. (BR-257)<br>3. The actor enters the content, defines and orders the options, chooses the ballot type and selects one or more eligible roles.<br>4. The actor selects Save draft.<br>5. The system verifies the current Chair or Secretary and Meeting status, required content, at least two distinct non-empty options, a valid ballot type and at least one role. (BR-252, BR-255, BR-256, BR-257, BR-272)<br>6. The system saves the Vote as Draft under the Meeting. It does not open voting or fix the eligible-voter list. (BR-190, BR-253)<br>7. The system displays MSG210 – "Vote draft created successfully." and the saved Draft Vote. |  |  |
| Alternative Flows | AF-1 – Configure an existing Draft Vote<br>1. At Step 1, the actor selects Edit on a Draft Vote.<br>2. The system checks current access, the current Chair or Secretary and permitted Meeting status and that the Vote is still Draft, then displays its saved content, options, ballot type and roles. The actor changes them and selects Save draft. (BR-252, BR-253, BR-254)<br>3. The system performs Step 5 and verifies that the Vote is still Draft. (BR-254)<br>4. The system updates that draft and displays MSG211 – "Vote draft updated successfully." No voting is opened. The use case ends successfully.<br>AF-2 – Delete a Draft Vote<br>1. At Step 1, the actor selects Delete on a Draft Vote and confirms.<br>2. The system verifies the current Chair or Secretary, the permitted Meeting status and that the Vote is still Draft. (BR-189)<br>3. The system removes only that Draft Vote and displays MSG212 – "Vote draft deleted successfully."<br>4. No other Vote, ballot or conclusion changes. The use case ends successfully.<br>AF-3 – Open immediately<br>1. At Step 4 or in the edit form in AF-1, the current Chair selects Open vote while the Meeting is InProgress instead of Save draft. Open vote is not offered to the Secretary. (BR-160)<br>2. The content and choices are already shown in that form; selecting Open vote confirms opening. The system continues with UC-54 AF-1 without a separate draft-save step or another confirmation.<br>3. The use case ends with the UC-54 outcome. On failure, the entered information stays in the form for correction; no new draft or draft changes are saved. |  |  |
| Exception Flows | EX-1 – Invalid Vote information<br>1. At Step 5 or during validation in AF-1, content, options, ballot type or roles are invalid. (BR-255, BR-256, BR-257, BR-272)<br>2. The system displays MSG209 – "Invalid vote information. Check the highlighted fields." with the invalid fields identified. No new draft or configuration changes are saved.<br>3. The actor corrects the information. The flow resumes at Step 3 of the Main Flow, or the editing step of AF-1.<br>EX-2 – Draft operation is no longer allowed<br>1. At Step 2 or Step 5, AF-1 or AF-2, the actor is no longer the current Chair or Secretary, the Meeting is not Approved, Published or InProgress, or the selected Vote is no longer Draft. (BR-252, BR-254, BR-189)<br>2. The system displays MSG213 – "Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress." The draft is not created, changed or deleted.<br>3. The use case ends.<br>EX-3 – Draft creation cannot be completed<br>1. At Step 6, the system cannot save the new draft.<br>2. No draft is created. The system displays MSG214 – "Unable to create the vote draft. No draft was created. Please try again."<br>3. The use case ends.<br>EX-4 – Draft update cannot be completed<br>1. During AF-1, the system cannot save the configured changes.<br>2. The previously saved draft remains unchanged. The system displays MSG215 – "Unable to update the vote draft. The draft remains unchanged. Please try again."<br>3. The use case ends.<br>EX-5 – Draft deletion cannot be completed<br>1. During AF-2, the system cannot complete deletion.<br>2. The Draft Vote remains unchanged. The system displays MSG216 – "Unable to delete the vote draft. The draft remains unchanged. Please try again."<br>3. The use case ends.<br>EX-6 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |  |  |

#### 11.3 UC-54 – Open Vote

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to open a valid Vote during an InProgress Meeting. Opening fixes the eligible-voter list and count. Only one Vote may be Open in a Meeting at a time. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress. (BR-160)<br>● PRE-3: The selected Vote belongs to this Meeting and is Draft, or the Chair is opening a new Vote from the form in UC-53. (BR-285)<br>● PRE-4: No other Vote in the Meeting is Open. (BR-266) |  |  |
| Postconditions | ● POST-1: The Vote is Open with its opening time recorded.<br>● POST-2: Its eligible-voter list and count are fixed from the selected roles at opening. (BR-190)<br>● POST-3: Eligible voters can submit one ballot while the Meeting remains InProgress and the Vote remains Open. |  |  |
| Normal Flow | Open Vote<br>1. The Chair selects Open on a Draft Vote.<br>2. The system checks current access to the Vote, then displays its content, ballot type, options and selected roles for confirmation.<br>3. The Chair confirms opening.<br>4. The system checks the current Chair, Meeting InProgress and valid Vote information. An existing Vote must belong to this Meeting and still be Draft. No other Vote in the Meeting may be Open. (BR-160, BR-255, BR-256, BR-257, BR-266, BR-272, BR-285)<br>5. The system identifies the current holders of the selected meeting roles who have valid Meeting access. A Guest must have verified the invited email and retain a valid invitation. At least one eligible voter must exist. Attendance does not determine eligibility. (BR-190, BR-267)<br>6. The system opens the Vote with the confirmed information, records the opening time and fixes its eligible-voter list and count. (BR-190)<br>7. The system displays MSG217 – "Voting opened successfully." and makes the Open Vote available to authorized Meeting users. |  |  |
| Alternative Flows | AF-1 – Open from the create or edit form<br>1. At Step 1, the Chair selects Open vote on the form in UC-53. The form already shows the Vote information and this action confirms opening; Steps 2–3 are not repeated.<br>2. The system uses the entered information and performs Steps 4–5. If editing, it also checks that the existing Vote belongs to the Meeting and is still Draft. (BR-254, BR-285)<br>3. The new Vote information or existing draft changes are recorded only as part of successful opening at Step 6. On failure, no new Vote or changes to the saved draft are recorded.<br>4. The flow resumes at Step 6 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Configuration is invalid<br>1. At Step 4, the configuration does not satisfy required content, options, ballot type or selected roles. (BR-255, BR-256, BR-257, BR-272)<br>2. The system displays MSG209 – "Invalid vote information. Check the highlighted fields." Voting is not opened; any saved draft remains unchanged.<br>3. The Chair returns to UC-53 to correct the draft or entered configuration. The use case ends.<br>EX-2 – Opening is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or an existing Vote is no longer Draft. (BR-160)<br>2. The system displays MSG218 – "Only the current Chair can open a Draft vote while the meeting is InProgress." No Vote is opened and existing Vote data remains unchanged.<br>3. The use case ends.<br>EX-3 – Another Vote is Open<br>1. At Step 4, another Vote in this Meeting is Open. (BR-266)<br>2. The system displays MSG219 – "Another vote is already open in this meeting. Close it before opening this vote." The selected Vote is not opened and the other Vote is unchanged.<br>3. The use case ends.<br>EX-4 – No eligible voters<br>1. At Step 5, no current person with valid access holds a selected role. (BR-267)<br>2. The system displays MSG220 – "No eligible voters are available for the selected roles. Voting was not opened." Voting is not opened and any saved draft remains unchanged.<br>3. The use case ends.<br>EX-5 – Opening cannot be completed<br>1. At Step 6, the system cannot complete opening the Vote.<br>2. No opening time or eligible list is recorded; no new Vote is created or draft configuration changed. The system displays MSG221 – "Unable to open voting. Voting was not opened. Please try again."<br>3. The use case ends.<br>EX-6 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |  |  |

#### 11.4 UC-55 – Cast Vote

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an eligible person to submit one choice in an Open Vote during the Meeting. Eligibility comes from the list fixed at opening and requires current Meeting access. Attendance is not a condition. A saved ballot cannot be changed or withdrawn. |  |  |
| Preconditions | ● PRE-1: The internal actor is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor retains current Meeting access and is on the Vote's fixed eligible-voter list. (BR-165, BR-190, BR-268, BR-269)<br>● PRE-3: The Meeting is InProgress and the Vote is Open. (BR-165)<br>● PRE-4: The actor has not already cast a ballot in this Vote. (BR-258)<br>● PRE-5: The actor has confirmed reading every Vote document that is still attached to the Meeting, through UC-74. (BR-289, BR-292) |  |  |
| Postconditions | ● POST-1: One ballot for one option is accepted, and the actor is marked as having voted.<br>● POST-2: Turnout increases by one, and the ballot cannot be changed or withdrawn.<br>● POST-3: A secret ballot does not associate the voter with the chosen option. (BR-259) |  |  |
| Normal Flow | Cast Vote<br>1. The actor opens an Open Vote.<br>2. The system checks current Vote access, eligibility, Meeting and Vote status, and whether the actor has already voted. If voting is permitted, it displays the content, options, ballot type and the notice that a submitted ballot cannot be changed or withdrawn. If the Vote has documents, it also lists each document with the actor's read status; Submit ballot is available only when every document is confirmed. (BR-289, BR-292) (BR-165, BR-258, BR-265, BR-268, BR-269)<br>3. The actor selects one option and selects Submit ballot.<br>4. The system checks again that the actor can access the Vote, the option belongs to this Vote, the Meeting is InProgress, the Vote is Open, the actor is on the fixed eligible list, current access remains valid, every Vote document still attached to the Meeting has a read confirmation from the actor, and no ballot has already been recorded for that actor. (BR-165, BR-258, BR-268, BR-269, BR-271)<br>5. The system accepts the ballot, marks the actor as having voted and updates turnout. For a secret Vote, it does not associate the actor with the chosen option. (BR-258, BR-259)<br>6. The system displays MSG227 – "Your ballot has been recorded." and the current turnout. The actor can no longer submit another ballot. For a secret Vote, the completion state shows that a ballot was submitted without displaying a saved choice. (BR-259, BR-265) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – No option selected<br>1. At Step 3, the actor attempts to submit without selecting exactly one option. (BR-271)<br>2. No ballot is recorded. The system displays MSG222 – "Select one option before submitting your ballot."<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Option is not valid<br>1. At Step 4, the selected option does not belong to this Vote. (BR-271)<br>2. No ballot is recorded and turnout is unchanged. The system displays MSG223 – "The selected option is not available for this vote."<br>3. The flow resumes at Step 2 of the Main Flow.<br>EX-3 – Actor is no longer eligible<br>1. At Step 2 or Step 4, the actor can view the Vote but is not on its fixed eligible list. A role change alone does not remove a person from that list; a person added after opening is not added to the list. (BR-268, BR-269)<br>2. No new ballot is recorded and previously saved ballots remain unchanged. The system displays MSG224 – "You are not eligible to cast a ballot in this vote."<br>3. The use case ends.<br>EX-4 – Balloting has ended<br>1. At Step 2 or Step 4, the Vote is no longer Open or the Meeting is no longer InProgress, even if the Vote still appears Open. (BR-165)<br>2. No ballot is recorded and turnout is unchanged. The system displays MSG225 – "This vote is no longer accepting ballots."<br>3. The use case ends.<br>EX-5 – Ballot already recorded<br>1. At Step 2 or Step 4, the actor has already voted, including after reopening the screen or rejoining the Meeting.<br>2. The existing ballot remains unchanged and no additional ballot is counted. The system displays MSG226 – "Your ballot has already been recorded. It cannot be changed or withdrawn."<br>3. For a secret Vote, only submission confirmation is shown; the saved choice is not disclosed through the voter's identity. (BR-259)<br>4. The use case ends.<br>EX-6 – Ballot cannot be recorded<br>1. At Step 5, the system cannot complete recording the ballot.<br>2. No ballot is accepted, the actor is not marked as having voted and turnout is unchanged. The system displays MSG228 – "Unable to record your ballot. No ballot was recorded. Please try again."<br>3. The use case ends.<br>EX-7 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends.<br>EX-8 – Vote documents not yet confirmed as read<br>1. At Step 2 or Step 4, the actor has not confirmed reading every Vote document that is still attached to the Meeting. (BR-289, BR-292)<br>2. No ballot is recorded and turnout is unchanged. The system displays MSG252 – "Confirm that you have read all vote documents before casting your ballot." and lists the documents not yet confirmed.<br>3. The actor may confirm each document through UC-74. The flow resumes at Step 2 of the Main Flow. |  |  |

#### 11.5 UC-56 – Close Vote

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to close an existing Open Vote and obtain its tally while the Meeting is InProgress or Ended. Closing a Vote does not end the Meeting and does not determine the Chair's conclusion. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended. (BR-270)<br>● PRE-3: The Vote belongs to the Meeting and is Open. |  |  |
| Postconditions | ● POST-1: The Vote is Closed and its closing time and tally are recorded.<br>● POST-2: No further ballots are accepted and all previously recorded ballots are preserved.<br>● POST-3: No Passed, NotPassed or Deferred conclusion is automatically recorded. (BR-263) |  |  |
| Normal Flow | Close Vote<br>1. The Chair selects Close voting on an Open Vote.<br>2. The system checks current access to the Vote, displays its current turnout and asks for confirmation.<br>3. The Chair confirms closing.<br>4. The system verifies the current Chair, Meeting InProgress or Ended and Vote Open. (BR-270)<br>5. The system stops accepting ballots, records the closing time and tallies all successfully recorded ballots per option. Percentages are based on ballots cast, not eligible voters; with no ballots, counts are zero and percentages are not shown. (BR-261)<br>6. The system records the Closed status and result together, preserving every recorded ballot and secret-ballot confidentiality. (BR-259, BR-270)<br>7. The system displays MSG229 – "Voting closed. The result is available." and the result under UC-52b. The Chair may proceed to UC-57 to record a conclusion. |  |  |
| Alternative Flows | AF-1 – Close an outstanding Vote after Meeting end<br>1. At Step 1, the Meeting is Ended and this Vote is still Open.<br>2. No ballots have been accepted since the Meeting ended. End Meeting did not close the Vote or alter its ballots. (BR-159, BR-165)<br>3. The flow resumes at Step 2 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Closing is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Open. (BR-270)<br>2. The system displays MSG230 – "Only the current Chair can close an Open vote while the meeting is InProgress or Ended." The Vote, ballots and any existing result remain unchanged.<br>3. The use case ends.<br>EX-2 – Closing cannot be completed<br>1. At Steps 5–6, the system cannot complete closing and tallying.<br>2. The Vote remains Open with no new closing time or tally recorded; all saved ballots remain unchanged. The system displays MSG231 – "Unable to close voting. The vote and saved ballots remain unchanged. Please try again."<br>3. If the Meeting is Ended, new ballots remain disallowed despite the Vote remaining Open. The use case ends.<br>EX-3 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |  |  |

#### 11.6 UC-57 – Record Vote Conclusion

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to record the business conclusion of a Closed and tallied Vote as Passed, NotPassed or Deferred, with an optional note. The Chair decides the conclusion; the system records it rather than inferring it from a majority. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended.<br>● PRE-3: The Vote is Closed and its tally is available. (BR-161)<br>● PRE-4: No conclusion has been recorded for this Vote. (BR-262) |  |  |
| Postconditions | ● POST-1: The selected conclusion, optional note, recording Chair and recording time are saved.<br>● POST-2: The conclusion and note cannot subsequently be edited or replaced.<br>● POST-3: The tally and conclusion are available for Meeting Minutes; the saved ballots and tally remain unchanged. (BR-264) |  |  |
| Normal Flow | Record Vote Conclusion<br>1. The Chair opens the result of a Closed Vote and reviews the tally.<br>2. The system checks current access, that the actor is the current Chair, and that the Vote is Closed without a conclusion. It then offers Passed, NotPassed and Deferred and an optional conclusion note. (BR-161, BR-262)<br>3. The Chair selects one conclusion, optionally enters a note and confirms recording.<br>4. The system verifies the current Chair, Meeting InProgress or Ended, the Closed and tallied Vote, an allowed conclusion and that no conclusion has already been recorded. (BR-161, BR-262)<br>5. The system records the conclusion and optional note together with the Chair and recording time. It does not change ballots or the tally. (BR-262, BR-263)<br>6. The system displays MSG235 – "Vote conclusion recorded successfully." and makes the conclusion read-only. The tally and conclusion are available for Meeting Minutes. (BR-264) |  |  |
| Alternative Flows | AF-1 – Record after Meeting end<br>1. At Step 1, the Meeting is Ended and the Vote has been Closed and tallied, including a Vote closed after Meeting end.<br>2. The Chair records the outcome of that existing Vote. No new voting is opened or ballot accepted.<br>3. The flow resumes at Step 2 of the Main Flow. |  |  |
| Exception Flows | EX-1 – Conclusion is invalid<br>1. At Step 4, no conclusion is selected or the selection is not Passed, NotPassed or Deferred. (BR-161)<br>2. No conclusion is recorded. The system displays MSG232 – "Select Passed, NotPassed or Deferred."<br>3. The flow resumes at Step 3 of the Main Flow.<br>EX-2 – Conclusion already recorded<br>1. At Step 2 or Step 4, the Vote already has a saved conclusion. (BR-262)<br>2. The existing conclusion, note and tally remain unchanged. The system displays MSG233 – "The vote conclusion has already been recorded and cannot be changed."<br>3. The use case ends.<br>EX-3 – Recording is no longer allowed<br>1. At Step 2 or Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Closed with a tally available. (BR-161)<br>2. No conclusion is recorded and existing Vote data remains unchanged. The system displays MSG234 – "Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended."<br>3. The use case ends.<br>EX-4 – Conclusion cannot be recorded<br>1. At Step 5, the system cannot complete recording.<br>2. No conclusion, note, recording Chair or recording time is added; ballots and tally remain unchanged. The system displays MSG236 – "Unable to record the vote conclusion. No conclusion was recorded. Please try again."<br>3. The use case ends.<br>EX-5 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |  |  |

#### 11.7 UC-76 – Manage Vote Documents

##### 11.7.1 UC-76a – Attach Documents to Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to attach one or more documents to a Draft Vote, so that eligible voters must confirm reading them before casting a ballot (UC-74, UC-55). Documents may be chosen from the Meeting documents, uploaded from the computer or chosen from My Documents; documents not yet in the Meeting are added to it and attached in the same save. (BR-287, BR-304) |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-184, BR-252)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-252)<br>● PRE-3: The selected Vote belongs to the Meeting and is Draft. (BR-288) |  |  |
| Postconditions | ● POST-1: The selected documents are Vote documents of the Draft Vote; its other Vote documents remain unchanged. (BR-287)<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. If any document cannot be added or attached, nothing from the submission is added or attached. (BR-194, BR-286, BR-304)<br>● POST-3: The Vote content, options, ballot type and roles remain unchanged. The Vote document list is fixed when the Vote opens. (BR-288) |  |  |
| Normal Flow | Attach Documents to Vote<br>1. The actor selects a Draft Vote and chooses Add document.<br>2. The system offers three sources: Meeting documents, Upload from computer and My Documents. (BR-304)<br>3. The actor chooses Meeting documents. The system lists the Meeting documents not yet attached to this Vote.<br>4. The actor selects one or more documents and confirms.<br>5. The system rechecks current Meeting access, the Chair or Secretary assignment, the permitted Meeting status, that the Vote is still Draft, and that every selected document is still in the Meeting and not yet attached to the Vote. (BR-184, BR-252, BR-287, BR-288)<br>6. The system attaches all selected documents to the Vote together; if any cannot be saved, none is attached. (BR-304)<br>7. The system refreshes the Vote documents and displays MSG265 – “Documents attached to the vote successfully.” |  |  |
| Alternative Flows | AF-1 – Upload from computer<br>1. At Step 3, the actor chooses Upload from computer, selects one or more files and submits them.<br>2. The system checks every file is a valid PDF or DOCX no larger than 50 MB, and performs the checks of Step 5 that do not concern existing Meeting documents. (BR-184, BR-187, BR-214, BR-215, BR-288)<br>3. The system adds the files to the Meeting and to the uploader's personal library, and attaches them to the Vote, in the same save. (BR-194, BR-210, BR-286, BR-304)<br>4. If any file is invalid or cannot be added or attached, nothing from the submission is added or attached; the system displays MSG164 or MSG166 as in UC-33a EX-3 and EX-5. Otherwise, the flow resumes at Step 7.<br>AF-2 – Choose from My Documents<br>1. At Step 3, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the remaining checks of Step 5. (BR-184, BR-193, BR-209, BR-288)<br>3. The system adds the documents to the Meeting and attaches them to the Vote in the same save. Personal-library documents stay unchanged. (BR-276, BR-286, BR-304)<br>4. If any document cannot be added or attached, nothing is added or attached; the system displays MSG249, MSG165 or MSG166 as in UC-33a EX-2, EX-4 and EX-5. A document already in the Meeting is attached through Step 3. Otherwise, the flow resumes at Step 7. |  |  |
| Exception Flows | EX-1 – Vote documents can no longer be changed<br>1. At Step 5, or the corresponding check in AF-1 or AF-2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-252, BR-288)<br>2. Nothing is added or attached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer in the Meeting<br>1. At Step 5, a selected document is no longer attached to this Meeting.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The flow resumes at Step 3.<br>EX-3 – Document is already attached<br>1. At Step 5, one or more selected documents are already attached to the Vote, including by another user after selection.<br>2. Nothing is attached and no duplicate is created. The system displays MSG266 – “One or more selected documents are already attached to the vote.”<br>3. The flow resumes at Step 4.<br>EX-4 – Change cannot be saved<br>1. At Step 6, the system cannot complete the change.<br>2. The Vote documents remain unchanged. The system displays MSG267 – “Unable to attach the documents. No documents were attached. Please try again.”<br>3. The use case ends.<br>EX-5 – Meeting or Vote is not accessible<br>1. Before displaying or saving, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. Nothing is attached. The system displays MSG205 – "Meeting votes are not accessible."<br>3. The use case ends. |  |  |

##### 11.7.2 UC-76b – Detach Document from Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to detach one document from a Draft Vote. The document stays in the Meeting; removing it from the Meeting is UC-33c. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-184, BR-252)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-252)<br>● PRE-3: The document is a Vote document of a Draft Vote of the Meeting. (BR-287, BR-288) |  |  |
| Postconditions | ● POST-1: Only the selected Vote document is detached from the Vote. (BR-288)<br>● POST-2: The document stays in the Meeting; its Agenda Item links, contents, comments and read confirmations remain unchanged. (BR-305) |  |  |
| Normal Flow | Detach Document from Vote<br>1. The actor selects a document of a Draft Vote, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, the permitted Meeting status, that the Vote is still Draft and that the document is still attached to it. (BR-184, BR-252, BR-288)<br>3. The system detaches only that document from the Vote; the document stays in the Meeting. (BR-305)<br>4. The system refreshes the Vote documents and displays MSG268 – “Document detached from the vote successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Vote documents can no longer be changed<br>1. At Step 2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-252, BR-288)<br>2. Nothing is detached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer attached to the Vote<br>1. At Step 2, the document has already been detached from the Vote or removed from the Meeting.<br>2. Nothing else changes. The system displays MSG269 – “This document is no longer attached to the vote. Refresh the list.”<br>3. The use case ends.<br>EX-3 – Change cannot be saved<br>1. At Step 3, the system cannot complete the change.<br>2. The Vote documents remain unchanged. The system displays MSG270 – “Unable to detach the document. No changes were made. Please try again.”<br>3. The use case ends.<br>EX-4 – Meeting or Vote is not accessible<br>1. Before displaying or saving, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-253)<br>2. Nothing is detached. The system displays MSG205 – "Meeting votes are not accessible."<br>3. The use case ends. |  |  |

### 12. FE-12 – Meeting Minutes & Digital Signing

#### 12.1 UC-58 – View Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

#### 12.2 UC-59 – Generate Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

#### 12.3 UC-60 – Edit Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

#### 12.4 UC-61 – Submit Meeting Minutes for Signing

Placeholder only – detailed Use Case Specification will be added later.

#### 12.5 UC-62 – Sign Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

#### 12.6 UC-63 – View Signature Status & Audit Trail

Placeholder only – detailed Use Case Specification will be added later.

#### 12.7 UC-64 – Finalize Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

#### 12.8 UC-65 – Request Revision of Meeting Minutes

Placeholder only – detailed Use Case Specification will be added later.

### 13. FE-13 – Email & In-App Notification

#### 13.1 UC-66 – View Notifications

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authenticated user to view their list of in-app notifications and check the unread notification count. |  |  |
| Preconditions | ● PRE-1: The user is authenticated in the system. |  |  |
| Postconditions | ● POST-1: The system displays the user's notifications and unread badge count.<br>● POST-2: Opening the notification list does not change any notification’s read status. |  |  |
| Normal Flow | View Notifications<br>1. The user selects the notification bell from the navigation header.<br>2. The system retrieves the notifications and unread badge count for the user's account.<br>3. The system displays the user's notifications, indicates which ones are unread, and shows the unread count. (BR-48, BR-49) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1: Notifications Cannot Be Retrieved<br>1. At Step 2 of the Main Flow, the system cannot load notification records.<br>2. The system displays MSG40 – "Unable to load notifications. Please try again."<br>3. The use case ends unsuccessfully.<br>EX-2: Requested Notification Page Is Unavailable<br>1. At Step 2 of the Main Flow, the system cannot display the requested portion of the notification list.<br>2. The system displays MSG39 – "The requested notification page is unavailable."<br>3. The use case ends unsuccessfully. |  |  |

#### 13.2 UC-67 – Mark Notifications as Read

##### 13.2.1 UC-67a – Mark Notification as Read

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authenticated user to mark a specific unread notification as read, updating its status. |  |  |
| Preconditions | ● PRE-1: The user is authenticated in the system.<br>● PRE-2: The user has at least one unread notification. |  |  |
| Postconditions | ● POST-1: The selected notification status is changed to Read.<br>● POST-2: The unread count reflects the latest saved notification statuses. |  |  |
| Normal Flow | Mark Notification as Read<br>1. The user views their notification list.<br>2. The user selects "Mark as Read" for a notification.<br>3. The system verifies that the notification belongs to the user's account. (BR-47)<br>4. The system marks the notification as read.<br>5. The system displays the notification as read and shows the updated unread count. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | N/A |  |  |

##### 13.2.2 UC-67b – Mark All Notifications as Read

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user to mark all of their unread notifications as read. |  |  |
| Preconditions | ● PRE-1: The user is signed in. |  |  |
| Postconditions | ● POST-1: All notifications belonging to the user's account that were unread when the request was processed are marked as read.<br>● POST-2: The displayed unread count reflects the latest saved state. |  |  |
| Normal Flow | Mark All Notifications as Read<br>1. The user selects the notification bell and opens the notification list.<br>2. The user selects "Mark All as Read".<br>3. The system marks all unread notifications belonging to the user's account as read, including those not currently displayed. (BR-47, BR-51)<br>4. The system refreshes the displayed read statuses and unread count. (BR-48) |  |  |
| Alternative Flows | AF-1 – No Unread Notifications<br>Entry Point: Step 3 of the Main Flow.<br>1. The system finds no unread notifications belonging to the user's account.<br>2. The system displays the current list and an unread count of zero.<br>3. The use case ends successfully. |  |  |
| Exception Flows | N/A |  |  |

#### 13.3 UC-68 – Open Notification Target

| Primary Actors | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user to open the meeting linked to one of their notifications. |  |  |
| Preconditions | ● PRE-1: The user is signed in. |  |  |
| Postconditions | ● POST-1: The user sees the linked meeting.<br>● POST-2: The selected notification is marked as read if successfully saved. |  |  |
| Normal Flow | Open Notification Target<br>1. The user selects the notification bell and clicks on a specific notification.<br>2. The system checks that the notification belongs to the user's account and identifies its linked meeting. (BR-47, BR-52)<br>3. The system marks the notification as read if needed and updates the unread count. (BR-48, BR-50, BR-53)<br>4. The system checks whether the user currently has permission to view the linked meeting. (BR-29, BR-52)<br>5. The system opens the meeting. |  |  |
| Alternative Flows | AF-1 – Notification Is Already Read<br>Entry Point: Step 3 of the Main Flow.<br>1. The notification is already read, so its status and the unread count remain unchanged. (BR-50)<br>2. The flow resumes at Step 4 of the Main Flow. |  |  |
| Exception Flows | N/A |  |  |

### 14. FE-14 – Personal Dashboard & Meeting Workspace

#### 14.1 UC-69 – View Personal Dashboard & Meeting Workspace

Placeholder only – detailed Use Case Specification will be added later.

### 15. FE-15 – Reporting & Statistics

#### 15.1 UC-70 – View & Export Reports

##### 15.1.1 UC-70a – View Reports & Statistics

Placeholder only – detailed Use Case Specification will be added later.

##### 15.1.2 UC-70b – Export Report

Placeholder only – detailed Use Case Specification will be added later.

## III. Functional Requirements

### 1. FE-01 – Authentication & Account Security

#### 1.1 Sign In

The Sign In screen is the entry point of the system for every internal user (System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary and Participant). It is displayed when a signed-out user opens the application or a meeting link, or when the sign-in session has expired, and supports signing in with email and password or with a Google account; signing out is confirmed from the account menu (UC-01, UC-02, UC-03).

This screen allows the System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary and Participant to:

- Sign in with Email and Password: enter the registered email and password and click “Sign in with password” to access the system (UC-01).

- Sign in with Google: click “Continue with Google” and choose the Google account whose email is registered in the system (UC-02).

- View Redirect Reason: see why s/he was sent to the sign-in page (a meeting link opened while signed out, or an expired sign-in session).

- View Sign-in Errors: see the reason a sign-in attempt failed, with a hint to choose the registered Google email when the Google account is not linked (UC-01, UC-02).

On the screen, s/he can also:

- Sign Out: choose Sign Out from the account menu at the bottom of the sidebar and confirm in the dialog to end the sign-in session and return to this screen (UC-03).

Field Description

| Field Name | Description |
| --- | --- |
| Application header | Read-only: TrustCabinet logo and name with the subtitle “Sign in to the paperless meeting room system” [Đăng nhập hệ thống phòng họp không giấy]. |
| Information banner [Hộp thông báo] | Read-only blue box, shown only when there is a reason: “Sign in to open the meeting.” [Đăng nhập để mở cuộc họp.] when a meeting link was opened while signed out, or “Your session has expired. Please sign in again.” [Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.] (MSG140) after the sign-in session ended. Hidden otherwise. |
| Error box [Hộp lỗi] | Red box displayed above the form when sign-in fails: MSG01 “Invalid email or password.”, MSG02 (account deactivated/unavailable), MSG03 (Google sign-in unsuccessful), MSG04 (Google account cannot be verified), MSG05 (Google account not granted access) followed by a hint to choose the Google account with the registered email. Messages never reveal whether an email exists (BR-02). |
| Email | Text input, required, email format. No initial value. Leaving Email or Password empty shows “Please enter email and password.” |
| Password [Mật khẩu] | Password input (masked), required. No initial value; the password is never displayed or stored in plain text (BR-01). |
| Sign in with password [Đăng nhập bằng mật khẩu] | Primary button. Validates the credentials; a deactivated account cannot sign in (BR-03). On success the user is redirected to the authenticated home page, or to the meeting whose link was opened. |
| Divider “or” [hoặc] | Read-only separator between the password form and the Google option. |
| Continue with Google [Tiếp tục sử dụng dịch vụ bằng Google] | Button with the Google icon that opens Google sign-in. The verified Google email must be linked to an existing active internal account; no new account is created automatically (BR-05, BR-06, BR-07). If the Google sign-in service cannot be loaded, an error text replaces the button. |
| Sign Out confirmation dialog | Opened from the account menu: “Are you sure you want to sign out? The session on this device will end.” with Cancel / Sign Out buttons. On failure shows MSG06 “Unable to sign out. Please try again.” After sign-out protected functions are no longer accessible (BR-09). |

#### 1.2 Reset Password

The Reset Password screen is opened by the account owner from the one-time link in the password-reset email that the System Admin sent from the Send Password Reset Link Dialog. The link is valid once, for 60 minutes, and lets the user set a new password without signing in; this is the completion part of Reset User Password (UC-07).

This screen allows the account owner (System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary or Participant) to:

- Enter New Password: type a new password that satisfies the password rule shown under the field (UC-07).

- Confirm New Password: re-type the new password to avoid typing mistakes (UC-07).

- Set New Password: submit the new password; the old password is replaced and every device signed in to the account is signed out (UC-07).

- View Invalid Link Notice: see an error page when the link has expired, has already been used or has been replaced by a newer link (UC-07).

Field Description

| Field Name | Description |
| --- | --- |
| Page title | Read-only: “Reset password” [Đặt lại mật khẩu] under the TrustCabinet logo. |
| Instruction text | Read-only: “Choose a new password for your account.” [Chọn mật khẩu mới cho tài khoản của bạn.] |
| New password [Mật khẩu mới] | Password input (masked), required (*). Hint below: “Password must be at least 12 characters.” [Mật khẩu phải có ít nhất 12 ký tự.]; a shorter value shows the same text as a field error (MSG94). |
| Confirm password [Nhập lại mật khẩu] | Password input (masked), required (*). Must be identical to New password; otherwise the field is outlined in red with the error “The re-entered password does not match.” [Mật khẩu nhập lại không khớp.] |
| Sign-out notice | Read-only text: “After the reset, every device currently signed in to this account will be signed out.” [Đặt lại xong, mọi thiết bị đang đăng nhập tài khoản này sẽ bị đăng xuất.] (UC-07 POST-5). |
| Set new password [Đặt mật khẩu mới] | Primary button. Saves the new password as a salted hash (BR-01), ends all sign-in sessions of the account, invalidates the link (BR-20), records the reset in the audit log (BR-16) and redirects to Sign In with MSG92 “Password reset successfully. Please sign in with your new password.” If this browser is still signed in, it is signed out first; if that fails MSG06 is also shown. |
| Invalid link page | Shown instead of the form when the link is expired (older than 60 minutes), already used or superseded: “This link can no longer be used.” [Liên kết này không dùng được nữa.] (MSG93) with guidance to contact the System Admin. When the link has no reset code, MSG129 is displayed. |

#### 1.3 Guest Verification

After OTP verification, Guest enters display name and job title. State that only the invited email is verified and display information is self-declared. This revision does not prescribe additional required fields, validation messages or profile-save gates. (BR-180)

Guest opens the invitation and verifies the invited email by OTP, then enters self-declared display name and job title. The Guest can use Member participation functions only in the invited Meeting. Only email control is verified; display information is not a verified legal identity. (UC-04, BR-180, BR-181)

This screen allows the Guest to:

- View Invitation: see that TrustCabinet has invited him/her and the title of the invited meeting (UC-04).

- Enter Verification Code: type the 6-digit code sent to the invited email address (UC-04).

- Verify: submit the code; on success a meeting-scoped Guest session is established and the Guest continues to the invited meeting and the Invitation Response screen (UC-04, UC-29).

- Resend Code: request a new code to the invited email when the code was not received or has expired (UC-04).

On the screen, s/he can also:

- Read scope and recording notices: this invitation grants Member participation only in this Meeting. Display name and job title are self-declared; email is verified. Existing audio notice/consent requirements remain independent of profile entry.

Field Description

| Field Name | Description |
| --- | --- |
| Application header | Read-only bar with the TrustCabinet logo and name; no sign-in menu, as the Guest has no internal account (BR-156). |
| Invitation caption [TrustCabinet mời bạn họp] | Read-only uppercase caption “TrustCabinet invites you to a meeting”. |
| Meeting title | Read-only: title of the invited meeting, e.g. “Sprint 6 planning meeting” [Họp kế hoạch Sprint 6]. Only the invited meeting is shown (BR-120, BR-154). |
| Code destination | Read-only text “Enter the 6-digit code just sent to v***@doitac.vn to view the meeting.” [Nhập mã 6 số vừa gửi tới … để xem cuộc họp.] showing the invited email partially masked. |
| Verification code | Six single-digit input boxes, required, digits only, exactly 6 digits; the cursor moves to the next box after each digit. A wrong, expired or already-used code shows MSG126 “Unable to verify the guest invitation. Check the code and try again.” |
| Verify [Xác minh] | Primary button. Verifies control of the invited email (BR-153) and establishes a Guest session limited to the invited meeting that expires according to the configured validity period (BR-154, BR-155); no internal account sign-in is needed (BR-156). A missing, expired or revoked invitation cannot be verified and the Guest is told the invitation is no longer valid (BR-157). If the verification service is unavailable no Guest session is created. |
| Resend code [Gửi lại mã] | Text link next to Verify; sends a new one-time code to the invited email address. |
| Recording notice | Read-only yellow info box: “The meeting may be recorded to prepare the minutes. Your information is used only for this meeting.” [Cuộc họp có thể được ghi âm để lập biên bản. Thông tin của bạn chỉ dùng cho cuộc họp này.] |
| Guest scope notice | Read-only scope notice: “Khách chỉ truy cập cuộc họp được mời và toàn bộ tài liệu gắn trong cuộc họp theo quyền Thành viên. Email đã được xác minh; tên hiển thị và chức vụ do khách tự khai.” (BR-180, BR-181) |
| Already verified | When the Guest reopens a valid link during an active verified Guest session, this screen is skipped and the invited meeting opens directly (UC-04 ALT-1). |

### 2. FE-02 – User & Profile Management

#### 2.1 User & Permission List

The User & Permission List screen is the System Admin’s account management page, reached from Administration › User & Permissions [Quản trị › Người dùng & Quyền] in the sidebar. It lists all user accounts and provides the per-account actions edit (including the system role), deactivate/reactivate and send password reset link (UC-05, UC-06a–UC-06c, UC-07, UC-08).

This screen allows the System Admin to:

- View User List: view all user accounts with full name, email, system role, sign-in method, status and creation date (UC-05).

- Search Users: enter a keyword to search accounts by full name or email (UC-05).

- Filter User List: filter accounts by system role and by account status (UC-05).

- Edit Account: open the Edit Account dialog to change full name, email or system role (UC-06b, UC-08).

- Deactivate / Reactivate Account: change the status of an account after confirming in a dialog (UC-06c).

- Send Password Reset Link: open the Send Password Reset Link Dialog for an active account that signs in with a password (UC-07).

On the screen, s/he can also:

- Create Account: click “Create account” to open the Create / Edit User Account Dialog in create mode (UC-06a).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Read-only breadcrumb “Administration” [Quản trị], title “User & Permissions” [Người dùng & Quyền] and summary “Manage users and system permissions · 7 accounts, 6 active.” [Quản lý người dùng và phân quyền hệ thống · … tài khoản, … đang hoạt động.] |
| Create account [Tạo tài khoản] | Primary button; opens the Create / Edit User Account Dialog in create mode. |
| Search [Tìm theo tên hoặc email…] | Text input, optional, empty by default. Matches full name or email. |
| Role filter [Tất cả vai trò] | Dropdown. Values: All roles (default) and every system role, e.g. [Chủ toạ], [Thư ký], [Thành viên], [Quản trị viên] and custom roles such as [Kiểm soát viên]. |
| Status filter [Mọi trạng thái] | Dropdown. Values: All statuses (default), Active, Deactivated. Combined filters must all match (BR-31). |
| Full name [Họ tên] | Column: avatar initials, full name and email below. The signed-in System Admin’s own row is tagged “You” [Bạn]. |
| Role [Vai trò] | Column chip: name of the system role held by the account. |
| Sign-in method [Đăng nhập] | Column: Password [Mật khẩu], Google, or Password · Google (account linked to Google). Passwords and tokens are never displayed (BR-10, BR-21). |
| Status [Trạng thái] | Column chip: Active [Hoạt động] (green) or Deactivated [Đã vô hiệu hoá] (grey). |
| Created date [Ngày tạo] | Column, format dd/mm/yyyy. |
| Actions [Thao tác] | Three icon buttons: Edit · Deactivate / Reactivate · Send password reset link. A button is greyed out when not applicable and its tooltip gives the reason: the System Admin cannot change the status of his/her own account; a Google-only or deactivated account cannot receive a password reset link (BR-22). Unlocking a locked account (UC-09) is not yet provided in this version of the screen. |
| Deactivate / Reactivate confirmation | Dialog: deactivation signs the user out of all devices and keeps meeting history (BR-18, BR-19); reactivation restores sign-in with the current role. Success shows MSG11 “User account status changed successfully.”; deactivating the last active account holding the User administration permission is blocked with MSG12. |
| No-result message | “No accounts match the filter.” shown in the table when search/filter returns nothing (BR-12); “No accounts yet.” when the list is empty. |

#### 2.2 Create / Edit User Account Dialog

The Create / Edit User Account Dialog opens over the User & Permission List when the System Admin clicks “Create account” or the Edit action of an account. In create mode it collects full name, email, an initial password and the system role; in edit mode it changes full name, email and system role only (UC-06a, UC-06b, UC-08).

This screen allows the System Admin to:

- Create User Account: enter full name, email, password and role to create a new active account (UC-06a).

- Generate Password: click “Generate password” to fill a random password, then show or copy it (UC-06a).

- Update User Account: change the full name and email of an existing account (UC-06b).

- Assign System Role: select or change the system role of the account (UC-08).

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Create account” [Tạo tài khoản] in create mode, “Edit account” [Sửa tài khoản] in edit mode. |
| Information note | Create mode: “The new account is active and can sign in immediately with this email and password. If the email is a Google account, the user can also sign in with Google.” (BR-15). Edit mode: first Google sign-in with the same email links automatically; passwords are not changed here. |
| Full name [Họ tên] | Text input, required (*), max 200 characters. Errors: “Full name is required.”, “Full name must not exceed 200 characters.” (MSG09). |
| Email | Text input, required (*), valid email format, unique among accounts (BR-13). Errors: “Email is required.”, “Email is invalid.” (MSG09); “Email is already in use.” (MSG08). |
| Password [Mật khẩu] | Create mode only. Masked input, required (*), minimum 12 characters (MSG09 “Password must be at least 12 characters.”). Has an eye button to show/hide and a copy button. Hint: “Minimum 12 characters. Send the password to the user through a private channel, not a shared group chat.” [Tối thiểu 12 ký tự. Gửi mật khẩu cho người dùng qua kênh riêng, không qua nhóm chat chung.] |
| Generate password [Tạo mật khẩu] | Button next to Password; fills the field with a random password that satisfies the rule. |
| Role [Vai trò] | Dropdown, required (*); initial value [Thành viên] in create mode, current role in edit mode. Lists existing roles only (MSG09 “The role does not exist.” otherwise). Changing another user’s role signs that user out of all devices; changing the role of the last active account holding the User administration permission is blocked (MSG12). |
| Self-edit warning | Edit mode only. Shown when the System Admin changes his/her own role or email: after saving s/he will be signed out and must sign in again. |
| Cancel [Huỷ] | Secondary button; closes the dialog without saving. |
| Create account / Save [Tạo tài khoản / Lưu] | Primary button. Validates all fields (BR-14), saves and records the change in the audit log (BR-16). Success toast MSG07 “User account created successfully.” or MSG10 “User account updated successfully.” |

#### 2.3 Send Password Reset Link Dialog

The Send Password Reset Link Dialog opens when the System Admin clicks the reset-password action of an account in the User & Permission List. It confirms sending a one-time reset link to the user’s registered email; the user then completes the reset on the Reset Password screen (UC-07). The same dialog style is used to confirm deactivating an account (UC-06c).

This screen allows the System Admin to:

- Confirm Password Reset: check the email address the link will be sent to and click “Send link” (UC-07).

- Cancel: close the dialog without sending anything.

On the screen, s/he can also:

- Confirm Account Deactivation: in the Deactivate account dialog of the same style, confirm that the user will be signed out of all devices while meeting history is kept (UC-06c).

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Reset password” [Đặt lại mật khẩu]. |
| Confirmation text | Read-only: “Send a password reset link to <email>?” [Gửi liên kết đặt lại mật khẩu tới …?] with the registered email of the selected account in bold. |
| Validity note | Read-only, in the same paragraph: the link can be used once within 60 minutes; any link sent earlier becomes invalid; the current password keeps working until the user (named in the text) sets a new one (BR-20, UC-07 POST-4). |
| Cancel [Huỷ] | Secondary button; closes the dialog, no link is created. |
| Send link [Gửi liên kết] | Primary button. Allowed only for an active account that signs in with a password (BR-22), otherwise MSG14 “Password reset is not available for this account.” Success closes the dialog and shows toast MSG13 “Password reset email sent successfully.”; email failure shows MSG15 “Unable to send the password reset email. Please try again.” and no new link becomes usable. The request is recorded in the audit log (BR-16). |
| Deactivate account dialog [Vô hiệu hoá tài khoản] | Same layout: “Deactivate <full name>’s account? This person will be signed out of all devices… Their meeting history is kept.” [… Lịch sử cuộc họp của họ vẫn được giữ nguyên.] with Cancel / Deactivate buttons (BR-18, BR-19); success MSG11, blocked change MSG12. |

### 3. FE-03 – Role-Based Access Control

#### 3.1 Role List

The Role List screen is reached from Administration › Roles [Quản trị › Vai trò] in the sidebar and is the first tab of the role management page. The System Admin views the system roles with the number of accounts holding each, creates custom roles, and edits or deletes custom roles; predefined (Default) roles are protected (UC-10, UC-12a, UC-12b, UC-12c).

This screen allows the System Admin to:

- View Role List: view every role with its name, description and number of accounts (UC-10).

- Create Role: click “Create role” and enter a name, description and initial permissions (UC-12a).

- Update Role: edit the name and description of a custom role (UC-12b).

- Delete Role: delete a custom role that is no longer held by any account, after confirmation (UC-12c).

On the screen, s/he can also:

- Open Permission Matrix: switch to the “Permission matrix” tab to configure the permissions of each role (UC-10, UC-11).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Breadcrumb “Administration” [Quản trị], title “Roles” [Vai trò] and description: “Each role is a group of permissions; assign roles to accounts on the User & Permissions screen.” [Mỗi vai trò là một nhóm quyền; gán vai trò cho tài khoản ở màn Người dùng & Quyền.] |
| Tabs | “Role list” [Danh sách vai trò] with the role count, e.g. 5 (default tab), and “Permission matrix” [Ma trận phân quyền]. |
| Create role [Tạo vai trò] | Primary button; opens the role form in create mode. |
| Role name [Tên vai trò] | Column. Predefined roles ([Chủ toạ], [Thư ký], [Thành viên], [Quản trị viên]) carry the “Default” [Mặc định] badge; the role of the signed-in System Admin carries the “Your role” [Vai trò của bạn] badge. |
| Description [Mô tả] | Column: short description of the role. |
| Accounts [Tài khoản] | Column: number of accounts holding the role, including deactivated accounts. |
| Actions [Thao tác] | Edit and Delete icon buttons for custom roles only (e.g. [Kiểm soát viên]); Default roles show no actions and cannot be renamed or deleted (MSG24). |
| Role form – Name | Text input, required, max 64 characters after trimming, unique (case-insensitive) among all roles. Errors: MSG20 “Invalid role information.” (“Role name is required.”, “Role name must not exceed 64 characters.”), MSG21 “A role with this name already exists. Please choose a different name.” |
| Role form – Description | Textarea, optional, max 256 characters (MSG20). |
| Role form – Initial permissions | Create mode only. Checkboxes of the system permissions, optional; an empty set gives the same access as the [Thành viên] role. Success MSG19 “Role created successfully.”; edit success MSG22 “Role updated successfully.” |
| Delete role dialog [Xoá vai trò] | Confirmation dialog “Cannot delete role ‘<name>’ yet.” [Chưa thể xoá vai trò …] when accounts still hold the role, with a yellow warning: “This role is still held by {n} account(s), including deactivated accounts. Move them to another role before deleting.” (MSG25); the Delete role [Xoá vai trò] button is then disabled. Buttons: Close [Đóng], Delete role. Success MSG23 “Role deleted successfully.”; failure MSG28. |

#### 3.2 Permission Matrix

The Permission Matrix screen is the second tab of the Roles page. It shows every role against every system permission as a matrix of toggles so that the System Admin can grant or revoke permissions, saving or undoing changes role by role (UC-10, UC-11).

This screen allows the System Admin to:

- View Role & Permission Matrix: view which permissions each role holds, grouped by area (UC-10).

- Grant / Revoke Permission: click a cell to switch a permission on or off for a role (UC-11).

- Save Role Permissions: click “Save” on a changed row to apply that role’s new permission set (UC-11).

- Undo Changes: click “Undo” on a changed row to restore its saved permissions (UC-11).

On the screen, s/he can also:

- Create Role: click “Create role” in the page header, which stays available on this tab (UC-12a).

Field Description

| Field Name | Description |
| --- | --- |
| Page header and tabs | Same header as the Role List (title “Roles” [Vai trò], Create role button); the “Permission matrix” [Ma trận phân quyền] tab is selected. |
| Role [Vai trò] | Row header: role name with “Default” [Mặc định] or “Your role” [Vai trò của bạn] badge; custom roles such as [Kiểm soát viên] have no badge. |
| Meeting [Cuộc họp] group | Permission columns: Create meeting [Tạo cuộc họp], Approve meeting [Phê duyệt cuộc họp]. |
| Minutes [Biên bản] group | Permission column: Prepare minutes [Soạn biên bản]. |
| Administration [Quản trị] group | Permission column: User administration [Quản trị người dùng]. |
| Permission cell | Toggle (checked = granted). Initial value = saved configuration. A changed cell is highlighted in yellow and its row is tinted until saved or undone. Only permissions defined by the system can be set (BR-23). |
| Actions [Thao tác] | “Undo” [Hoàn tác] and “Save” [Lưu] appear only on a row that has unsaved changes; each row is saved independently. Success toast MSG16 “Role permissions updated successfully.”; the change is audit-logged (BR-16). |
| Inline row error | Removing “User administration” from the last role that holds it for an active account is blocked and an error is shown under the row (MSG17 “Invalid permission configuration.”); save failure shows MSG18 “Unable to update role permissions. Please try again.” |

#### 3.3 System Audit Log

The System Audit Log screen is reached from Administration › Audit Log [Quản trị › Nhật ký hệ thống] in the sidebar. It shows the read-only log of business and security events (not technical logs) so that the System Admin can trace who did what and when, open the before/after details of a change and export the result to CSV (UC-13).

This screen allows the System Admin to:

- View Audit Log: view events, newest first, with time, actor, event type, target object and IP address (UC-13).

- Filter Audit Log: filter by date range, actor and event type (UC-13).

- Search by Object: enter a keyword to find events of a target object (UC-13).

- View Event Details: click a row to open a right-side panel with the values before and after the change (UC-13).

- Export CSV: download the events matching the current filters as a CSV file (UC-13).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Breadcrumb “Administration” [Quản trị], title “Audit Log” [Nhật ký hệ thống] and note “Business and security events. Cannot be edited or deleted.” [Sự kiện nghiệp vụ và bảo mật. Không sửa, không xoá được.] |
| Export CSV [Xuất CSV] | Secondary button; exports the events matching the current filters to a CSV file. |
| Date range | Date range picker dd/mm/yyyy – dd/mm/yyyy (e.g. 01/10/2026 – 06/10/2026); limits events to the selected period. An invalid range is reported and not applied. |
| Actor filter [Mọi người thực hiện] | Dropdown; default All actors; lists users. |
| Event type filter [Mọi loại sự kiện] | Dropdown; default All event types (e.g. change role permissions, approve meeting, failed sign-in, download document, send password reset link). |
| Search [Tìm theo đối tượng…] | Text input, optional; matches the target object name. All criteria must match (BR-31). |
| Time [Thời điểm] | Column, format dd/mm hh:mm, newest first. |
| Actor [Người thực hiện] | Column: avatar initials and full name of the user who performed the action. |
| Event [Sự kiện] | Column chip with the event type, e.g. “Changed role permissions” [Đổi quyền vai trò], “Approved meeting” [Duyệt cuộc họp], “Downloaded document” [Tải tài liệu], “Sent password reset link” [Gửi liên kết đặt lại mật khẩu]. Security events such as “Failed sign-in” [Đăng nhập thất bại] use a red chip and the whole row is highlighted in red. |
| Object [Đối tượng] | Column: affected object (role, meeting, document, user account) or event detail, e.g. “5 consecutive attempts” [5 lần liên tiếp]. |
| IP address [Địa chỉ IP] | Column: IP address of the request. |
| Detail panel | Right-side drawer opened by clicking a row; shows the before/after values of the change. Read-only. |
| Pagination | “Showing 1–5 of 1,284” [Hiển thị 1–5 trong 1.284] with previous/next and page-number buttons (BR-11). No matching entries shows an empty-result message so the filters can be revised. |

### 4. FE-04 – Meeting Management

#### 4.1 Meeting Calendar – Calendar View

The Meeting Calendar – Calendar View is the work-schedule page reached from “Calendar” [Lịch làm việc] in the sidebar. The toolbar is the first line of the page (there is no page title). It places every Meeting the user is allowed to see on a weekly day/hour grid according to the Meeting’s own planned start and end time, colours each Meeting by its lifecycle status, and opens the Meeting detail when a block is clicked (UC-17, UC-16).

This screen allows any signed-in internal user related to a meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Weekly Calendar: view the accessible Meetings of the selected week as blocks on a day/hour grid, positioned by their planned start and end time (UC-17).

- Navigate Weeks: go to the current week with “Today” or move to the previous/next week (UC-17).

- Switch View Mode: switch between the Calendar and List views of the same week (UC-17).

- Change Week Range: switch between the work week (Monday–Friday) and the full week (UC-17).

- Open Meeting Detail: click a Meeting block to open the Meeting Detail – General Information screen (UC-16).

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create / Edit Meeting screen; the button is shown only to the Meeting Organizer, i.e. a user holding the create-meeting permission (UC-14).

Field Description

| Field Name | Description |
| --- | --- |
| Today [Hôm nay] | Button; jumps to the current week. |
| Previous / Next week (‹ ›) | Buttons; move one week back or forward. |
| Week label | Read-only date range of the displayed week, e.g. 12/10 – 16/10/2026. |
| View toggle [Lịch / Danh sách] | Segmented control Calendar [Lịch] (selected) / List [Danh sách]. The selected mode is kept in the URL. |
| Week range [Tuần làm việc] | Dropdown: Work week (default, Monday–Friday) or Full week. |
| Create meeting [Tạo cuộc họp] | Primary button; visible only to users with the create-meeting permission (Meeting Organizer). Opens the Create / Edit Meeting screen. |
| Day header | Weekday name and date (dd/mm) for each column, e.g. “Thứ Hai 12/10”; the current day is highlighted and labelled “Today” [Hôm nay]. |
| Time grid | Hour rows covering 24 hours; the grid scrolls inside its frame. For the current week it scrolls automatically to a red line marking the current time, with the time shown in a red badge on the hour axis (e.g. 13:47). |
| Meeting block | One block per Meeting, placed by its planned start and end time (BR-25): time range (hh:mm – hh:mm), Meeting title and mode · location (e.g. “Trực tiếp · Phòng họp A1”; Online Meetings show only “Trực tuyến”). A “Secret” [Mật] chip is shown for Secret Meetings. The block is coloured by status: Pending Approval has a dashed amber border and a Rejected Meeting is red with its title struck through. Only Meetings the user may access are shown (BR-29). Clicking opens the Meeting detail; Back returns to the same week and mode. |
| Status legend | Read-only colour legend below the grid: In Progress [Đang họp], Published [Đã công bố], Approved [Đã duyệt], Pending Approval [Chờ duyệt], Draft [Nháp], Ended [Đã kết thúc], Rejected [Bị từ chối]. |
| Error state | If the Meetings cannot be loaded the grid shows MSG38 “Unable to load meetings. Please try again.” |

#### 4.2 Meeting Calendar – List View

The Meeting Calendar – List View shows the same weekly Meetings as the Calendar View, but as cards grouped by day in chronological order. It is opened with the List [Danh sách] toggle of the calendar toolbar and uses the same data and access rules as the calendar (UC-17, UC-16).

This screen allows any signed-in internal user related to a meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Weekly Meeting List: view the accessible Meetings of the selected week grouped by day, each with planned start/end time, mode, location, status and confidentiality level (UC-17).

- Navigate Weeks: go to the current week or to the previous/next week (UC-17).

- Switch View Mode: return to the Calendar view of the same week (UC-17).

- Open Meeting Detail: click “View details” on a card to open the Meeting Detail – General Information screen (UC-16).

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create / Edit Meeting screen; shown only to the Meeting Organizer (UC-14).

Field Description

| Field Name | Description |
| --- | --- |
| Toolbar | Same controls as the Calendar View: Today [Hôm nay], ‹ ›, week label, Calendar / List toggle (List selected, kept in the URL as ?mode=list), Work week [Tuần làm việc] dropdown and Create meeting [Tạo cuộc họp] button (Meeting Organizer only). |
| Day group header | Weekday and full date (dd/mm/yyyy), e.g. “Thứ Hai 12/10/2026”, with the number of Meetings that day on the right (e.g. “1 cuộc họp”). The current day carries a “Today” [Hôm nay] badge. |
| Time column | Planned start time (bold) and planned end time below it (hh:mm), with a dot coloured by the Meeting status. |
| Meeting title | Name of the Meeting; struck through when the Meeting is Rejected. |
| Mode · location | Meeting mode In-person [Trực tiếp] / Online [Trực tuyến] / Hybrid [Kết hợp] followed by the location; Online Meetings show the mode only. |
| Status chip | Meeting status: Draft [Nháp], Pending Approval [Chờ duyệt], Approved [Đã duyệt], Rejected [Bị từ chối], Published [Đã công bố], In Progress [Đang họp], Ended [Đã kết thúc]. The card’s left border uses the same status colour. |
| Confidentiality chip | Normal [Thường] / Internal [Nội bộ] / Restricted [Hạn chế] / Secret [Mật]. |
| View details [Xem chi tiết ›] | Button on each card; opens the Meeting detail. Only Meetings the user may access are listed (BR-29). |
| Empty day | A day without Meetings shows a dashed box “No meetings” [Không có cuộc họp]. The list scrolls within the page. |
| Error state | If the Meetings cannot be loaded the list shows MSG38 “Unable to load meetings. Please try again.” |

#### 4.3 Meeting List

The Meeting List screen is reached from “Meetings” [Cuộc họp] in the sidebar. It lists only the Meetings the user has an authorized relationship with – Meetings s/he created (any status), Meetings assigned to him/her for approval from Pending Approval onward, and Meetings where s/he is active assigned Secretary before publication (BR-118), or Chair or Participant from Published onward; Guests do not use the internal list – with search, filters and paging. Visibility is enforced on the server (UC-17, UC-16).

This screen allows any signed-in internal user related to a meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Meeting List: view the accessible Meetings with name, Chair, planned time, mode · location, confidentiality level and status (UC-17).

- Search Meetings: enter a Meeting name to search within the accessible Meetings (UC-17).

- Filter Meetings: filter the list by status and by time range (month) (UC-17).

- Open Meeting Detail: click a row to open the Meeting Detail – General Information screen (UC-16).

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create / Edit Meeting screen; shown only to the Meeting Organizer (UC-14).

- Change Page: move between pages of the result with the pager (UC-17).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Title “Meetings” [Cuộc họp] with the subtitle “Meetings you organize, are invited to or need to approve” [Các cuộc họp bạn tổ chức, được mời hoặc cần duyệt]. |
| Create meeting [Tạo cuộc họp] | Primary button; visible only to users with the create-meeting permission (Meeting Organizer). |
| Search [Tìm theo tên cuộc họp] | Text input, optional, empty by default; matches the Meeting name. |
| Status filter [Tất cả trạng thái] | Dropdown: All statuses (default), Draft, Pending Approval, Approved, Rejected, Published, In Progress, Ended. |
| Time range [Tháng 10/2026] | Dropdown to choose the month; defaults to the current month. A Meeting matches when its own planned start/end interval overlaps the selected range (BR-30). Combined criteria must all match (BR-31). |
| Meeting name [Tên cuộc họp] | Column: Meeting name in bold; second line shows the Chair (“Chủ trì: <name>”), “No Chair yet” [Chưa có chủ trì] when none is assigned, or the rejection reason (“Lý do từ chối: …”) for a Rejected Meeting. |
| Time [Thời gian] | Column: planned start–end time (hh:mm–hh:mm) with weekday and date (dd/mm/yyyy) below. |
| Mode · Location [Hình thức · Địa điểm] | Column: In-person / Online / Hybrid and the location; Online Meetings show the mode only. |
| Confidentiality [Mức độ] | Column chip: Normal [Thường] / Internal [Nội bộ] / Restricted [Hạn chế] / Secret [Mật]. |
| Status [Trạng thái] | Column chip: Draft [Nháp], Pending Approval [Chờ duyệt], Approved [Đã duyệt], Rejected [Bị từ chối], Published [Đã công bố], In Progress [Đang họp], Ended [Đã kết thúc]. |
| Row link | The whole row is a link to the Meeting detail. Only Meetings the user may access are returned (BR-29). |
| Pagination | “Showing 1–6 of 23 meetings” [Hiển thị 1–6 trong 23 cuộc họp] with ‹, page numbers and ›. Search, filter and sort criteria are kept when moving between pages (BR-32). |
| Messages | “No meetings match the filter.” when no row matches; MSG38 “Unable to load meetings. Please try again.” on load failure; MSG32 “The meeting list criteria are invalid.” for invalid criteria. |

#### 4.4 Create / Edit Meeting

The Create / Edit Meeting screen is opened with “Create meeting” from the calendar or the Meeting List, or with “Edit” [Sửa thông tin] on the detail of a Draft or Rejected Meeting. The planned schedule (date, start and end time) is entered directly on the Meeting, which represents one sitting. A new Meeting is saved as Draft and is visible only to its Meeting Organizer until it is submitted for approval (UC-14, UC-15). Rescheduling a Published Meeting is done in the Reschedule Meeting dialog (4.9).

This screen allows the Meeting Organizer to:

- Create Meeting: enter Meeting name, description, meeting date, start and end time, mode, confidentiality level, location and optional online link, then save the Meeting as Draft (UC-14).

- Update Meeting: change the information of a Draft or Rejected Meeting without changing its status (UC-15).

- Cancel: leave the form; if there are unsaved inputs a dialog asks to discard the changes or keep editing.

On the screen, s/he can also:

- Back to Meetings: click “‹ Meetings” to return to the Meeting List.

Field Description

| Field Name | Description |
| --- | --- |
| Back link [‹ Cuộc họp] | Link; returns to the Meeting List. |
| Page header | Title “Create meeting” [Tạo cuộc họp] (edit mode: the Meeting name) with the note “The meeting is saved as Draft and only you can see it until it is submitted for approval.” [Cuộc họp được lưu ở trạng thái Nháp, chỉ bạn nhìn thấy cho tới khi gửi duyệt] (BR-26). |
| Meeting name [Tên cuộc họp] * | Text input, required, max 300 characters; e.g. “Họp kế hoạch Sprint 6”. Errors under MSG31: “Meeting title is required.”, “Meeting title must not exceed 300 characters.” |
| Description [Mô tả] | Textarea, optional, max 4,000 characters (MSG31 “Description must not exceed 4000 characters.”). |
| Meeting date [Ngày họp] * | Date picker (dd/mm/yyyy), required; the date of the sitting. |
| Start [Bắt đầu] * | Time picker (hh:mm), required. Together with the date it forms the Planned Start Time (BR-25); for a new Meeting it must be later than the current time (BR-162). |
| End [Kết thúc] * | Time picker (hh:mm), required; forms the Planned End Time, which must be later than the Planned Start Time (BR-24, BR-62). |
| Mode [Hình thức] * | Segmented control: In-person [Trực tiếp], Online [Trực tuyến], Hybrid [Kết hợp]. Invalid values are rejected (MSG31 “Invalid meeting mode.”). |
| Confidentiality level [Mức độ] * | Dropdown: Normal [Thường] (default), Internal [Nội bộ], Restricted [Hạn chế], Secret [Mật]. Hint: “Secret level shows a warning and blocks document download.” [Mức Mật hiển thị cảnh báo và chặn tải tài liệu]. |
| Location [Địa điểm] * | Text input, max 300 characters, e.g. “Phòng 2.1, tầng 2”; required for In-person and Hybrid Meetings (BR-24). Error MSG31 “Location must not exceed 300 characters.” |
| Online link [Link họp trực tuyến] | Text/URL input, optional, max 1,000 characters (hint “Không bắt buộc, tối đa 1.000 ký tự.”). When left blank, the TrustCabinet online meeting room is used by default for Online/Hybrid Meetings and the field shows “Phòng họp trực tuyến TrustCabinet (tạo tự động)” (BR-163). |
| Cancel [Huỷ] | Text button; with unsaved inputs shows the confirm dialog “Discard changes / Keep editing”. |
| Save draft [Lưu nháp] | Primary button. Validates the fields (BR-24, BR-62, BR-162, BR-163); invalid input shows MSG31 “Invalid meeting information.” and highlights the fields. Create: the Meeting is saved with status Draft and the creator recorded as Meeting Organizer (BR-26), toast MSG29 “Meeting created successfully.”, and the detail opens on the Participants tab to add the missing Secretary; failure shows MSG35. Edit: allowed only in Draft or Rejected and does not change the status (BR-27, BR-28); toast MSG30 “Meeting updated successfully.”; MSG33 “This meeting can no longer be edited.” if the status has changed; failure MSG36. |

#### 4.5 Meeting Detail – General Information

The Meeting Detail – General Information tab is the default tab of the Meeting detail page, opened from the calendar, the Meeting List or a notification. Only users with an authorized relationship to the Meeting can open it. The header shows the Meeting name, status, confidentiality and schedule, a lifecycle bar and the action buttons allowed for the current status and the user’s role; the tab itself shows the Meeting information, a readiness checklist and the status history (UC-16).

This screen allows any signed-in internal user related to a meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Meeting Details: view the planned time, mode, location, online link, confidentiality level, Meeting Organizer, Meeting Approver and description of the Meeting (UC-16).

- View Lifecycle Progress: see the current stage of the Meeting on the lifecycle bar Draft – Pending Approval – Approved – Published – In Progress – Ended (UC-16).

- View Status History: view the timeline of lifecycle actions with actor and time (UC-16).

- Navigate Detail Tabs: switch to the Participants, Meeting Content (agenda), Documents, Voting, Recordings, Summary and Minutes tabs.

On the screen, s/he can also:

- View Readiness Checklist: (Meeting Organizer, Draft or Rejected Meeting) see which required items are complete and which are still missing, with where to complete them (UC-18).

- Run Status-dependent Actions: use the header buttons allowed for the current status and role: Edit and Submit for approval (Meeting Organizer, Draft/Rejected – UC-15, UC-18), Approve / Reject (assigned Meeting Approver – UC-19), Publish (Meeting Organizer, Approved – UC-20), Reschedule (Meeting Organizer, Published – UC-15), Start Meeting (Chair, Published – UC-21) and End Meeting (Chair, In Progress – UC-22).

- Back to Meetings: click “‹ Meetings” to return to the previous list or calendar.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with status chip (e.g. Draft [Nháp]) and confidentiality chip (e.g. Normal [Thường]); second line: weekday, date and planned time range (e.g. “Thứ Năm, 15/10/2026 · 08:30–09:30”) and mode · location. |
| Action buttons | Change with status and role: Draft/Rejected – Edit [Sửa thông tin] and Submit for approval [Gửi duyệt] (Meeting Organizer); Pending Approval – Reject [Từ chối] and Approve [Duyệt] (assigned Meeting Approver); Approved – Publish [Công bố] (Meeting Organizer); Published – Reschedule [Dời lịch] (Meeting Organizer) and Start meeting [Bắt đầu cuộc họp] (Chair); In Progress – End meeting [Kết thúc cuộc họp] (Chair), which asks for confirmation and remains available with open Votes or incomplete attendance. Buttons the user may not use are hidden. |
| Lifecycle bar | Read-only steps Draft [Nháp] · Pending Approval [Chờ duyệt] · Approved [Đã duyệt] · Published [Đã công bố] · In Progress [Đang họp] · Ended [Đã kết thúc]; the current step is highlighted. |
| Tabs | General Information [Thông tin chung] (selected), Participants [Thành phần tham dự], Meeting Content [Nội dung cuộc họp], Documents [Tài liệu], Voting [Biểu quyết], Recordings [Bản ghi], Summary [Tóm tắt], Minutes [Biên bản]. |
| Time [Thời gian] | Read-only planned date and time range of the Meeting; once the Meeting has started, the actual start/end times are also shown (UC-16). |
| Mode [Hình thức] / Location [Địa điểm] | Read-only: In-person / Online / Hybrid and the in-person location (e.g. “Phòng 2.1, tầng 2”). |
| Online link [Link trực tuyến] | Read-only external link, or “Phòng họp trực tuyến TrustCabinet” when the internal online room is used (BR-163). |
| Confidentiality [Mức độ] | Read-only chip: Normal / Internal / Restricted / Secret. |
| Organizer [Người tổ chức] | Read-only name of the Meeting Organizer. |
| Approver [Người duyệt] | Read-only name of the assigned Meeting Approver; shows “Chosen when submitting for approval” [Chọn khi gửi duyệt] before submission. |
| Description [Mô tả] | Read-only multi-line text. |
| Readiness checklist [Sẵn sàng gửi duyệt] | Shown to the Meeting Organizer while Draft or Rejected. Each item is marked Complete [Đủ] (green) or Missing [Thiếu] (amber) with a hint where to fix it: time, mode and location set (required for submission, BR-33); Secretary assigned; Chair assigned (“assigned by the Secretary on the Participants tab”); number of Meeting Content items. Chair and Secretary are mandatory at publication (BR-124, BR-125). |
| History [Lịch sử] | Read-only timeline of lifecycle actions, e.g. “Draft created – Nguyễn Minh Anh · 09/10 15:02”, followed by submission, approval/rejection (with reason), publication, rescheduling, start and end, each with actor and time. History records cannot be changed (BR-38). |
| Error states | A missing or inaccessible Meeting shows a not-found page without revealing whether it exists (BR-29); load failure shows MSG37 “Unable to load meeting details. Please try again.” |

#### 4.6 Submit Meeting for Approval

The Submit Meeting for Approval dialog opens over the Meeting detail when the Meeting Organizer clicks “Submit for approval” [Gửi duyệt] on a Draft Meeting, or resubmits a Rejected Meeting after correcting it. The Organizer chooses the Meeting Approver and may leave a message; after submission the Meeting becomes Pending Approval and locks general Meeting information until a decision is made; the assigned Secretary can still manage Agenda Items under BR-65, BR-68 and BR-74 (UC-18).

This screen allows the Meeting Organizer to:

- Select Meeting Approver: choose the Meeting Approver from the list of eligible active approvers (UC-18).

- Add Message to Approver: optionally write a message to the Meeting Approver (UC-18).

- Submit for Approval: confirm to send the Meeting for approval; the status becomes Pending Approval and the approver is notified (UC-18).

- Resubmit a Rejected Meeting: after correcting a Rejected Meeting according to the rejection reason, open the same dialog and submit it again (UC-18).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without submitting.

Field Description

| Field Name | Description |
| --- | --- |
| Submit for approval [Gửi duyệt] (header) | Primary button on the detail of a Draft or Rejected Meeting, visible to its Meeting Organizer; opens the dialog. Submission requires the mandatory Meeting information and a valid planned schedule (BR-25, BR-33, BR-62); otherwise the Meeting stays Draft/Rejected and the missing items are shown in the readiness checklist. |
| Dialog title | “Submit meeting for approval” [Gửi cuộc họp để duyệt]. |
| Approver [Người duyệt] * | Dropdown, required; each option shows name · position, e.g. “Võ Thị Lan · Trưởng phòng Kỹ thuật”. Hint: “Only users who may approve meetings are listed, excluding you.” [Chỉ hiện người có quyền duyệt cuộc họp, không gồm bạn]. Lists active accounts holding the approve-meeting permission, never the Meeting Organizer (BR-35). |
| Message to approver [Lời nhắn cho người duyệt] | Textarea, optional (placeholder “Không bắt buộc”); delivered to the Meeting Approver together with the approval request. |
| Information notice | Read-only blue box: “After submitting, you cannot edit general Meeting information until the meeting is approved or rejected; the assigned Secretary may still manage Agenda Items.” [Sau khi gửi, bạn không sửa được thông tin chung cho tới khi cuộc họp được duyệt hoặc bị từ chối; Secretary được phân công vẫn có thể quản lý Agenda Items.] (BR-34). |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Submit [Gửi duyệt] | Primary button. Re-checks that the Meeting is still Draft or Rejected and the approver is eligible, assigns the approver, changes the status to Pending Approval, records the submission in the history and notifies the approver (BR-34, BR-35, BR-38); a success toast is shown. Errors: MSG41 “This meeting can no longer be submitted for approval.”, MSG42 “Unable to submit the meeting for approval. Please try again.”; an ineligible approver must be replaced by another eligible one. |

#### 4.7 Approve / Reject Meeting

The Approve / Reject Meeting function appears on the Meeting detail of a Pending Approval Meeting for its assigned Meeting Approver, who usually reaches it from the “pending approval” notification. The approver reviews all tabs in read-only mode and either approves the Meeting or rejects it with a reason. Approval does not publish the Meeting; a rejected Meeting goes back to the Meeting Organizer for correction and resubmission (UC-19).

This screen allows the Meeting Approver to:

- Review Meeting: read the Meeting information on all tabs in read-only mode, together with who submitted it and when (UC-19).

- Approve Meeting: click “Approve” to move the Meeting from Pending Approval to Approved (UC-19).

- Reject Meeting: click “Reject”, enter a mandatory reason and confirm to move the Meeting to Rejected (UC-19).

On the screen, s/he can also:

- Cancel Rejection: click “Cancel” in the reject dialog to return to the Meeting without a decision.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Pending Approval [Chờ duyệt] chip and confidentiality chip; second line: planned date and time and “Submitted by <Meeting Organizer> · dd/mm hh:mm” [Gửi bởi …]. |
| Approve [Duyệt] | Primary button, visible only to the Meeting Approver assigned to the current approval cycle (BR-35). Re-checks authorization and Pending Approval status, changes the status to Approved and records the decision with actor and time (BR-36, BR-38); toast MSG43 “Meeting approved successfully.” |
| Reject [Từ chối] | Red outlined button, visible only to the assigned Meeting Approver; opens the reject dialog. |
| Dialog title | “Reject meeting” [Từ chối cuộc họp]. |
| Rejection reason [Lý do từ chối] * | Textarea, required, trimmed, max 1,000 characters; e.g. “Mục 3 chưa có tài liệu quy trình ký. Bổ sung rồi gửi lại.” Errors: MSG83 “Rejection reason is required.” (BR-36), MSG84 “Rejection reason must not exceed 1000 characters.” |
| Warning notice | Read-only amber box: “The meeting moves to Rejected. The organizer is notified with the reason.” [Cuộc họp chuyển sang Bị từ chối. Người tổ chức nhận thông báo kèm lý do.] |
| Cancel [Huỷ] | Text button; closes the dialog without a decision. |
| Reject (dialog) [Từ chối] | Red primary button. Changes the status to Rejected and stores the reason in the status history (BR-36, BR-38); toast MSG44 “Meeting rejected successfully.” |
| Error messages | “This meeting is no longer awaiting approval.” when the status has changed; “Unable to complete the approval decision. Please try again.” when the decision cannot be saved; a notice when the user is no longer authorized to review the Meeting (BR-35). In each case the status is unchanged. |

#### 4.8 Publish Meeting

The Publish Meeting dialog opens over the Meeting detail when the Meeting Organizer clicks “Publish” [Công bố] on an Approved Meeting. It checks that the Meeting has an active Chair and an active Secretary, states how many internal participants and external Guests will be invited, and warns that after publication the Organizer may change only planned time, location and link; the assigned Secretary may still manage Agenda Items under BR-65, BR-68 and BR-74. Confirming moves the Meeting to Published and starts invitation delivery (UC-20).

This screen allows the Meeting Organizer to:

- Check Publication Readiness: see whether the required Chair and Secretary are assigned and active, and who is still missing (UC-20).

- Review Invitation Recipients: see how many internal participants and external Guests will receive the invitation (UC-20).

- Publish Meeting: click “Publish and send invitations” to change the status to Published and send the invitations (UC-20).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog; the Meeting stays Approved.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Approved [Đã duyệt] chip and confidentiality chip; second line: planned date and time and “Approved by <Meeting Approver> · dd/mm hh:mm” [Duyệt bởi …]. |
| Publish [Công bố] (header) | Primary button, visible only to the Meeting Organizer of an Approved Meeting (BR-37); opens the dialog. |
| Dialog title | “Publish meeting” [Công bố cuộc họp]. |
| Chair check [Chủ trì] | Read-only item marked Complete [Đủ] with the Chair’s name (e.g. “Chủ trì: Trần Quốc Bảo”), or Missing [Thiếu] with a hint that the Secretary must assign the Chair on the Participants tab. Exactly one current Chair with an active account is required (BR-124). |
| Secretary check [Thư ký] | Read-only item marked Complete [Đủ] with the Secretary’s name (e.g. “Thư ký: Đỗ Hải Yến”), or Missing [Thiếu]. At least one current Secretary with an active account is required (BR-125). Invitation responses do not affect these checks (BR-129). |
| Information notice | Read-only blue box, e.g. “Invitations will be sent to 4 internal people and 1 external guest. After publishing, only the time, location and link can be rescheduled.” [Lời mời sẽ gửi tới 4 người nội bộ và 1 khách ngoài. Sau khi công bố chỉ còn dời được thời gian, địa điểm và link.] (BR-27, BR-158). |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Publish and send invitations [Công bố và gửi lời mời] | Primary button; disabled while a check is Missing. Re-checks that the Meeting is still Approved with a valid planned schedule and the required Chair/Secretary (BR-37, BR-124, BR-125), changes the status to Published and records the action in the history (BR-38). Invitations are sent asynchronously: in-app notification and email to internal participants, an email with a secure meeting-specific guest invitation to external Guests (BR-39, BR-152); a delivery failure is retried and does not revert the Published status (BR-40). Messages: MSG45 “Meeting published successfully.”, MSG46 “This meeting can no longer be published.”, MSG47 “Unable to publish the meeting. Please try again.” |

#### 4.9 Reschedule Meeting

The Reschedule Meeting dialog opens over the detail of a Published Meeting when the Meeting Organizer clicks “Reschedule” [Dời lịch]. It is the reschedule path of Update Meeting: only the planned date and time, the location and the online link can be changed, while the Meeting name and confidentiality level are locked. Saving keeps the Meeting Published and notifies every invitee of the new schedule (UC-15). The same header carries “Start meeting” for the Chair (UC-21).

This screen allows the Meeting Organizer to:

- Change Planned Time: choose a new meeting date and new start and end times for the Published Meeting (UC-15).

- Change Location and Online Link: change the in-person location and the optional online link (UC-15).

- Save and Notify Invitees: click “Save and notify” to save the new schedule and send the update to all invitees (UC-15).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog; the existing schedule is kept.

- Start Meeting: (Chair) click “Start meeting” on the header of the Published Meeting to change its status to In Progress and record the actual start time (UC-21).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Published [Đã công bố] chip and confidentiality chip; second line: current planned date and time and mode · location. |
| Reschedule [Dời lịch] (header) | Secondary button, visible only to the Meeting Organizer while the Meeting is Published and has not started (BR-27, BR-158); opens the dialog. |
| Start meeting [Bắt đầu cuộc họp] (header) | Primary button for the assigned Chair of a Published Meeting. Requires exactly one active Chair and at least one active Secretary (BR-127, BR-130, BR-131); sets status In Progress, records the actual start time and notifies invited internal participants in-app (BR-38, BR-164); the Meeting Content tab switches to the running mode. Messages: MSG110 “Meeting started successfully.”, MSG112 “This meeting cannot be started.”, MSG115 “Unable to start the meeting. Please try again.” |
| Meeting date [Ngày họp] | Date picker (dd/mm/yyyy), pre-filled with the current date of the Meeting. |
| Start [Bắt đầu] / End [Kết thúc] | Time pickers (hh:mm), pre-filled. The new planned start must be later than the current time (BR-75) and the end must be later than the start (BR-62). |
| Location [Địa điểm] | Text input, max 300 characters, pre-filled, e.g. “Phòng 3.2, tầng 3”. |
| Online link [Link trực tuyến] | Text/URL input, optional, max 1,000 characters; blank means the TrustCabinet online meeting room (BR-163). |
| Meeting name [Tên cuộc họp] | Read-only (greyed) with the hint “Published, cannot be changed” [Đã công bố, không đổi được] (BR-158). |
| Confidentiality level [Mức độ] | Read-only (greyed) with the hint “Published, cannot be changed” [Đã công bố, không đổi được] (BR-158). |
| Information notice | Read-only blue box, e.g. “5 invitees will receive a notification and an email with the new schedule.” [5 người được mời sẽ nhận thông báo và email lịch mới.] Existing invitation responses are kept. |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Save and notify [Lưu và thông báo] | Primary button. Validates the fields (BR-62, BR-75, BR-163; MSG31 for invalid input), saves the new schedule, keeps the Published status, records the change in the history (BR-38) and queues notifications and emails to all current invitees; a delivery failure does not undo the change (BR-76). Success: MSG138 “Meeting rescheduled successfully. Invitees will be notified.” If the Meeting is no longer Published or has started: MSG33 “This meeting can no longer be edited.”; save failure: MSG36 “Unable to update the meeting. Please try again.” |

### 5. FE-05 – Agenda Management

#### 5.1 Meeting Detail – Meeting Content Tab

The Meeting Detail – Meeting Content tab shows the agenda of the selected Meeting. Agenda items belong directly to the Meeting and are listed in order with presenter, planned duration and a planned time slot calculated cumulatively from the Meeting’s planned start time. Every user related to the meeting can view the agenda; the Secretary builds it (add, edit, delete, drag-and-drop reorder) while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. When the Meeting is InProgress, the same tab switches to the Meeting Facilitation screen (5.3) for the Chair (UC-26, UC-23a, UC-23b, UC-23c, UC-23d). Pending Items may be planned freely; InProgress and Done permit only Title and Description changes and cannot be deleted or moved. The complete Agenda automatically reflects successfully saved changes for its authorized viewers.

This screen allows users related to the meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Meeting Agenda: view the complete Agenda in its saved order with Item information, planning and actual execution data. The complete view automatically refreshes after a successful Agenda change (UC-26).

- View Agenda Summary: see the number of items and the total planned duration compared with the planned length of the Meeting (UC-26).

- Add Agenda Item: (Secretary) click “Add item” to open the Add / Edit Agenda Item Drawer (UC-23a).

- Edit Agenda Item: (Secretary) edit all permitted planning fields while Pending; once InProgress or Done, edit only Title and Description (UC-23b).

- Delete Agenda Item: (Secretary) delete only a Pending Item after confirmation. Remaining Item positions are preserved (UC-23c).

- Reorder Agenda Items: (Secretary) exchange Pending Items within the existing Pending positions. InProgress and Done positions remain fixed (UC-23d).

On the screen, s/he can also:

- Switch Detail Tabs: open another tab of the meeting detail, e.g. Documents to manage the files that agenda items refer to.

- Back to Meetings: click “‹ Meetings” to return to the meeting list or calendar.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Meeting Content [Nội dung cuộc họp] is selected. Other tabs: General Information, Participants, Documents, Voting, Recordings, Summary, Minutes. |
| Agenda summary [4 mục · 60 phút / khung 60 phút] | Read-only: number of items · total planned duration / planned length of the Meeting (planned end minus planned start). When the total exceeds the Meeting length a warning is shown, but saving is not blocked. |
| Add item [Thêm mục] | Primary button; opens the Add / Edit Agenda Item Drawer. Visible only to the assigned Secretary and only while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress (BR-65, BR-74). |
| Drag handle | Available only for Pending Items to the active assigned Secretary while Agenda planning is permitted. Reordering exchanges Pending Items within their existing positions and leaves InProgress and Done positions unchanged. MSG63 confirms success; MSG143 reports invalid or outdated order; MSG71 reports a save failure. (BR-68) |
| # (Order number) | The saved position of the Item in the Meeting agenda. Deleting a Pending Item does not renumber remaining positions; gaps are permitted. New Items are appended after the last position. |
| Content [Nội dung] | Column: agenda item title in bold; second line shows the number of attached meeting documents (e.g. “2 documents” [2 tài liệu], “No documents yet” [Chưa có tài liệu]) and “needs a vote” [cần biểu quyết] when the item is flagged for voting. |
| Presenter [Người trình bày] | Column: name of the presenter; an external Guest is marked “(guest)” [(khách)]. Empty when no presenter is set. Editable only when creating an Item or editing a Pending Item; read-only for InProgress and Done. (BR-68) |
| Duration [Thời lượng] | Column: planned duration in minutes, e.g. “20 minutes” [20 phút]. |
| Planned time [Giờ dự kiến] | Read-only column calculated automatically: the first item starts at the Meeting’s planned start time and each following item starts when the previous one ends (e.g. 08:40–09:00). |
| Row action | Secretary only, while Agenda planning is permitted. Edit permits all planning fields for Pending but only Title and Description for InProgress/Done. Delete is offered only for Pending. MSG141 reports an attempted restricted field change; MSG142 reports an attempted deletion of an Item that has started. MSG67 applies when planning is no longer permitted; MSG61/MSG62 confirm success and MSG69/MSG70 report save failure. |
| Empty and error states | When the Meeting has no agenda items an empty agenda message is shown (UC-26 AF-1). A meeting the user may not access shows the not-found page and no agenda data (BR-29); load failure shows MSG128 “Unable to load the meeting agenda. Please try again.” |

#### 5.2 Add / Edit Agenda Item Drawer

The Add / Edit Agenda Item Drawer slides in from the right of the Meeting Content tab, so the agenda stays visible behind it, when the Secretary clicks “Add item” or chooses Edit on an item. It collects the content (title), presenter, planned duration, note, a needs-vote flag and the meeting documents linked to the item. Items can be added or edited only while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress (UC-23a, UC-23b). When editing an InProgress or Done Item, only Title and Description are editable; all other planning fields remain fixed.

This screen allows the Secretary to:

- Create Agenda Item: enter the item information and click “Add item” to add it in Pending status at the end of the Meeting agenda (UC-23a).

- Update Agenda Item: edit permitted planning fields of a Pending Item; only Title and Description remain editable for an InProgress or Done Item (UC-23b).

- Choose Presenter: choose an internal participant or external Guest when creating an Item or editing a Pending Item; the Presenter is fixed after the Item starts (UC-23a, UC-23b).

- Attach Item Documents: select documents of this Meeting when creating an Item or editing a Pending Item. Document associations are fixed after the Item starts (UC-23a, UC-23b).

On the screen, s/he can also:

- Preview Planned Time: read the planned time slot calculated from the duration and the item’s position in the agenda.

- Cancel: click “Cancel” to close the drawer without saving.

Field Description

| Field Name | Description |
| --- | --- |
| Drawer title | “Add agenda item” [Thêm mục nghị sự] in create mode or “Edit agenda item” in edit mode. |
| Content [Nội dung] | Text input, required (*); the title of the agenda item. Empty shows the field error “Agenda item title is required.” (MSG66, BR-66). |
| Presenter [Người trình bày] | Dropdown, optional. Lists the current internal participants and external Guests of the Meeting; a Guest is shown with “(guest)” [(khách)], e.g. “Ngô Thanh Vân (guest)”. Editable only when creating an Item or editing a Pending Item; read-only for InProgress and Done. (BR-68) |
| Duration (minutes) [Thời lượng (phút)] | Integer input, required (*). Must be greater than 0; otherwise “Planned duration must be greater than 0.” (MSG66, BR-66). Editable only when creating an Item or editing a Pending Item; read-only for InProgress and Done. (BR-68) |
| Planned time hint | Read-only hint under Duration, e.g. “Planned time calculated automatically: 09:00–09:20.” [Giờ dự kiến tự tính: …]; updates when the duration changes. |
| Note [Ghi chú] | Textarea, optional; description of what will be presented or discussed. |
| This item needs a vote [Mục này cần biểu quyết] | Checkbox, unchecked by default. Marks the item as one for which the Chair is expected to create a vote during the Meeting; it does not create a vote by itself (votes are created only while the Meeting is InProgress, BR-160). Editable only when creating an Item or editing a Pending Item; read-only for InProgress and Done. (BR-68) |
| Item documents [Tài liệu của mục] | List of linked documents with remove; the link “+ Choose from meeting documents” [+ Chọn từ tài liệu cuộc họp] opens a picker limited to documents already added to this Meeting (FE-07). A document not available to the Meeting gives “One or more selected documents are not available to this meeting.” (MSG66). Editable only when creating an Item or editing a Pending Item; read-only for InProgress and Done. (BR-68) |
| Cancel [Huỷ] | Text button; closes the drawer without saving. |
| Add item / Save [Thêm mục] | Primary button. Validates the fields and saves. Create: a Pending item is added at the end of the Meeting agenda (BR-67), toast MSG60; Edit: toast MSG61. MSG67 if the Meeting is no longer Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the user is no longer its Secretary (BR-65, BR-68); failure MSG68 / MSG69. Edit requests are checked against the current Item status; a restricted field change is rejected with MSG141. Successful changes automatically refresh the complete Agenda for authorized viewers. (BR-68, BR-73) |

#### 5.3 Meeting Facilitation

The Meeting Facilitation screen is the Meeting Content tab of a Meeting that is InProgress, after the Chair has clicked “Start meeting” (UC-21). It shows the agenda with the execution status and actual times of each item, a card for the current agenda item and a preview of the online room. The Chair sets which item is being discussed, completes it and moves to the next one, may create a vote for the current item and finally ends the Meeting; the assigned Secretary may continue to create, update, delete and reorder Agenda Items, while agenda execution remains Chair-only; other authorized viewers follow the same progress read-only and in real time (UC-24, UC-25, UC-26, UC-22). The Chair must start the first Pending Item in the current order and may complete it regardless of Planned Duration; the next Item never starts automatically. Pending Items stay Pending when the Meeting ends.

This screen allows the Chair to:

- Follow Meeting Progress: view the actual start time, elapsed time and attendance count of the Meeting, and the status and actual times of every agenda item (UC-26).

- Set Current Agenda Item: start only the first Pending Item in the current order, when no Item is InProgress. Its actual start time is recorded (UC-24).

- Complete Agenda Item: complete the current Item regardless of its planned duration; record actual end time and duration. The next Item does not start automatically (UC-25).

- Create Vote for Current Item: click “Create a vote for this item” to open the Create & Open Vote Dialog for the current agenda item (UC-53).

- End Meeting: end the Meeting regardless of open Votes or incomplete attendance, complete any current InProgress Item and leave remaining Pending Items Pending (UC-22).

On the screen, s/he can also:

- Join Online Room: click “Join online room” to enter the online room of an Online or Hybrid Meeting (UC-37).

- View Room Preview: see who is currently connected to the online room in the small video grid.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: title with status chip In Progress [Đang họp] and confidentiality chip; second line “Started 08:32 · 21 minutes elapsed” [Bắt đầu … · đã họp … phút] and “Present 4/5” [Có mặt 4/5] taken from attendance (6.3). |
| Join online room [Vào phòng trực tuyến] | Secondary button; shown for Online or Hybrid Meetings; opens the Meeting Lobby (8.1). |
| End meeting [Kết thúc cuộc họp] | Chair-only End Meeting confirmation; available while InProgress regardless of open Votes, attendance completeness or remaining Pending Items. End records actual time/history and completes only the InProgress Agenda Item; it closes room access but preserves Votes and ballots without automatically closing them. No new ballots after Ended. MSG111 success; MSG114/MSG116 on authority/status/save failure. (BR-130, BR-132, BR-143, BR-159) |
| Agenda list | One row per agenda item: order number, title, presenter, planned duration and a status/action cell. A Done item shows its actual time range (e.g. 08:32–08:44); the current item is highlighted and shows “In discussion since 08:44 · 9 minutes” [Đang thảo luận từ … · … phút]. |
| Item status | Done or InProgress status is shown read-only. Only the first Pending Item offers Set as Current to the active assigned Chair when no Item is InProgress. |
| Set as current [Đặt hiện tại] | Chair only, while Meeting is InProgress. Starts only the first Pending Item in the current order and records actual start time. MSG72 reports another current Item; MSG144 reports selecting a later Pending Item; MSG73 reports other ineligible conditions; MSG75 reports save failure; MSG64 confirms success. (BR-69, BR-70, BR-71) |
| Current item card [Mục hiện tại · 2] | Read-only: order number and title of the current item and its attached documents (e.g. “2 documents · De-xuat-bieu-quyet.docx, So-sanh-eCabinet.pdf”); file names open the Document Viewer (7.5). |
| Create a vote for this item [Tạo biểu quyết cho mục này] | Secondary button in the current item card; Chair only, while the Meeting is InProgress (BR-160, BR-165); opens the Create & Open Vote Dialog (11.2) linked to the current item. |
| Complete item [Hoàn thành mục] | Chair only, for the current InProgress Item. Completion is allowed regardless of Planned Duration and records actual end time and elapsed duration. The Meeting stays InProgress and the next Item does not start automatically. MSG65 confirms success; MSG74 reports ineligible conditions; MSG76 reports save failure. (BR-72) |
| Online room preview | Read-only grid of tiles with the names of users connected to the online room; a tile such as “Room 2.1” [Phòng 2.1] shows the in-person room of a Hybrid Meeting, and “Waiting to join” [Chờ vào] marks an empty slot. |
| Real-time update | The complete Agenda automatically refreshes for authorized users viewing the Meeting after successful creation, editing, deletion, reordering, start, completion or completion of the current Item when the Meeting ends. Failed changes do not produce a successful Agenda update. (BR-73) |

### 6. FE-06 – Meeting Participant Management

#### 6.1 Meeting Detail – Participants Tab

The Meeting Detail – Participants tab shows current internal participants and external Guests. The assigned Secretary manages the roster, roles and additional permissions; the Organizer may assign the missing Secretary. From Published onward, the Chair may view the roster and invitation responses by default, while a Member or verified Guest needs the relevant additional permission. A Member or verified Guest with View Invitation Responses can see invitee names and email in response rows without gaining roster browsing or search access. View-only access does not permit changes. (UC-27, UC-28a–UC-30, BR-173–BR-176)

This screen allows the Secretary to:

- View Meeting Participants: view the internal participants and external Guests with name, email and meeting role or Guest access (UC-27).

- Search Participants: type a name or email to filter the roster (UC-27).

- Manage Meeting Role & Additional Permissions: select “Edit Role & Permissions” for a current internal participant, change the meeting role or permitted additional permissions, and confirm Chair replacement when needed (UC-28b).

- Remove Participant: click “Remove” and confirm to remove an internal participant from the Meeting (UC-28c).

- Revoke Guest Invitation: click “Revoke invitation” and confirm to revoke the invitation of an external Guest (UC-28c).

- View Response Status: after the Meeting is Published, view each invitee’s latest invitation response and the response totals (UC-30).

On the screen, s/he can also:

- Add Participants: click “Add participants” to open the Add Participant Dialog on the Internal users tab (UC-28a).

- Invite External Guest: click “Invite external guest” to open the Add Participant Dialog on the External guests tab (UC-28a).

- Assign or Restore Secretary: in Draft/Rejected/Approved with no current Secretary, Organizer selects an active new user or an existing Member to assign Secretary; no duplicate participant is created (UC-28a).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Participants [Thành phần tham dự] is selected. Other tabs: General Information, Meeting Content, Documents, Voting, Recordings, Summary, Minutes. |
| Search [Tìm người tham dự] | Text input, optional, empty by default; filters the roster by full name or email. |
| Invite external guest [Mời khách ngoài] | Secondary button; opens the Add Participant Dialog on the External guests tab. Secretary only. |
| Add participants [Thêm thành phần] | Primary button; opens the Add Participant Dialog. Shown to the Secretary; when the Meeting has no Secretary it is replaced for the Meeting Organizer by “Add Secretary” [Thêm Thư ký] (BR-78, BR-79). Hidden while the Meeting is Pending Approval or Ended (BR-80, BR-118). |
| Participant [Người tham dự] | Column: internal full name or Guest self-declared display name, optional Guest job title and invited email; use invitation name before profile completion. Mark Guest explicitly; do not represent display information as verified identity. (BR-148, BR-180) |
| Meeting role [Vai trò trong cuộc họp] | Internal participants have Chair [Chủ trì], Secretary [Thư ký] or Member [Thành viên]; Guests have fixed Guest [Khách] access. Edit Role & Permissions [Sửa vai trò và quyền] opens the role and additional-permission editor for the assigned Secretary. Replacing the Chair requires confirmation showing the current Chair becoming Member and the selected person becoming Chair; both changes are saved together. A Secretary must remain in Published/InProgress; preparation may temporarily have none. Success MSG100; replacement failure MSG146; required-role restriction MSG109. (BR-85, BR-86, BR-122, BR-123, BR-169) |
| Row action | Secretary only: Edit Role & Permissions for internal participants; Edit Permissions only for Guests, Remove for internal participants, and Revoke Invitation for Guests. View-only users see no management actions. Removal ends the assignment’s additional permissions and access, while independent valid access remains. Required-role restriction MSG109; removal success MSG103; failure MSG104; status restriction MSG98. (BR-80, BR-97, BR-126, BR-157, BR-177) |
| Response [Phản hồi] | Shown after publication to the assigned Secretary and current Chair by default, and to a Member or verified Guest with View Invitation Responses. Shows each current invitee’s name, email, latest response, response time and totals. This permission displays identity in response rows but does not grant roster browsing or search. Responses do not set attendance. (UC-30, BR-115, BR-116, BR-176) |
| Roster footer | Read-only: number of people and of external Guests, e.g. “5 people · 1 external guest” [5 người · 1 khách ngoài], and the note “Invitations are sent when the meeting is published” [Lời mời gửi khi công bố] (BR-90, BR-91). |
| Error states | A Meeting the user is not authorized for shows the not-found page without roster data (BR-121); load failure shows MSG95 “Unable to load meeting participants. Please try again.” |
| Additional permissions [Quyền bổ sung] | The Secretary may grant or revoke additional Meeting permissions on current internal participants or Guest invitations without changing their role. Available grants are not limited to View Participants and View Responses; the final catalogue and grant matrix are pending. These two views continue to check their respective permission. (BR-174–BR-177) |

#### 6.2 Add Participant Dialog

For a Guest invitation, enter name/email and optional additional Meeting permissions. Confidential/Secret shows inline warning: “Khách được truy cập tài liệu và tham gia như Thành viên của cuộc họp này.” No extra confirmation/approval step. Guest has no Chair/Secretary role selector. (BR-174, BR-182)

The Add Participant Dialog opens from the Participants tab with “Add participants” (Internal users tab) or “Invite external guest” (External guests tab). The Secretary searches active internal users, ticks the people to add and chooses their meeting role, or invites an external Guest by name and email. When the Meeting has no Secretary yet, the Meeting Organizer uses the same dialog to add the missing Secretary (UC-28a).

This screen allows the Secretary (or the Meeting Organizer when adding the missing Secretary) to:

- Search Internal Users: type a name or email to find active internal users (UC-28a).

- Add Internal Participants: tick one or more users, choose the meeting role and click “Add <n> people” (UC-28a).

- Invite External Guest: on the External guests tab enter the Guest’s name and email and submit the invitation (UC-28a).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without adding anyone.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Add participants” [Thêm thành phần]. |
| Tabs | Internal users [Người trong hệ thống] (default) / External guests [Khách mời ngoài]. |
| User search | Text input on the Internal users tab, e.g. “Phạm”; filters active internal users by name or email. |
| User result list | Select one or more active internal accounts by checkbox. Current participants cannot be added again. Organizer recovery may instead assign Secretary to one existing active Member without creating a duplicate. All selected users are added together, or none are added if any selection is invalid. No organisational-unit invitation. For the missing Secretary, select exactly one user. (BR-81–BR-83, BR-168) |
| Meeting role [Vai trò trong cuộc họp] | Required; Member [Thành viên] by default, Chair [Chủ trì] or Secretary [Thư ký]. One shared role applies to the entire selection. The missing Secretary assignment is one user as Secretary, without additional permissions. A selection must not create a second Chair; use Edit Role & Permissions to replace the current Chair. (BR-78, BR-85, BR-123, BR-169, BR-175) |
| Guest name | External guests tab. Text input, required. |
| Guest email | External guests tab. Email input, required, valid format. One current invitation per email per Meeting and the email must not belong to a current internal participant (BR-149, BR-151). The Guest receives an email with a verification link once the Meeting is Published or InProgress (BR-91, BR-152). |
| Cancel [Huỷ] | Text button; closes the dialog. |
| Add <n> people [Thêm <n> người] | Shows the number selected and submits the entire selection. New invitations start Pending. All selected people are saved together; no one is added when validation or saving fails. Success MSG96 and the number added; invalid participant information MSG97/MSG145; invalid additional permissions MSG152; status restriction MSG98; save failure MSG99. Invitation delivery failure does not cancel successful additions. (BR-88, BR-90, BR-94, BR-168) |
| Additional permissions [Quyền bổ sung] | Additional permissions for current internal participants and Guest invitations supplement existing role permissions for this Meeting. Display the grantable permissions from the agreed catalogue; do not hardcode a two-option ceiling. View Participants and View Responses are examples. The catalogue and grant matrix are pending. (BR-174, BR-175) |

#### 6.3 Attendance Check

The Attendance Check screen shows all current internal participants and external Guests while the Meeting is InProgress. The assigned Secretary marks selected people Present or Absent and saves only the submitted changes together; omitted entries retain their saved or unmarked values. A person not yet recorded has no official attendance value. Invitation responses and online-room presence never determine attendance. Saved attendance is available for the meeting minutes. (UC-31, BR-116, BR-170–BR-172, BR-178)

This screen allows the Secretary to:

- View Attendance List: view every participant and Guest of the Meeting with meeting role, invitation response and current attendance status (UC-31).

- Record Attendance: set Present or Absent for each person (UC-31).

- Save Attendance: click “Save attendance” to store the official attendance of the Meeting (UC-31).

- Revise Attendance: change and save the attendance again while attendance editing is still permitted (UC-31).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: title with status chip In Progress [Đang họp] and confidentiality chip; second line actual start time (e.g. “Started 08:32”) and mode · location. |
| Panel title | “Official attendance” [Điểm danh chính thức]. |
| Attendance summary | Displays Present [Có mặt], Absent [Vắng] and not yet recorded [Chưa điểm danh] counts. Unsaved choices are distinguished from the last saved official attendance. Unmarked people are never counted as Absent. (BR-171) |
| Save attendance [Lưu điểm danh] | Secretary only while InProgress. Save one or more selected Present/Absent changes; omitted people retain saved or unmarked attendance. Validate submitted entries only and save that submission together. Success MSG147; invalid submitted value MSG148; status restriction MSG149; save failure MSG150; changed submitted participant MSG151. Attendance completeness never blocks End Meeting. (BR-170–BR-172, BR-178) |
| Participant [Người tham dự] | Column: avatar initials and full name of each current internal participant and external Guest. |
| Role [Vai trò] | Read-only column: Chair [Chủ trì], Secretary [Thư ký], Participant [Thành viên] or Guest [Khách] (greyed). |
| Invitation response [Phản hồi lời mời] | Read-only chip: Accepted [Tham dự] (green), Declined [Không tham dự] (red) or Pending. Shown for reference only; it does not set attendance (BR-116). |
| Attendance [Điểm danh] | Present [Có mặt] or Absent [Vắng] for every current internal participant and Guest. Shows the last saved value; if never recorded, neither choice is selected. Invitation responses and online-room connections never select or save official attendance automatically. Revision is allowed only while InProgress. (BR-116, BR-170–BR-172) |

#### 6.4 Invitation Response

The Invitation Response screen is a mobile-friendly page opened from the invitation email or the in-app notification; an external Guest reaches it after Guest Verification (1.3). The invitee sees the meeting title, status, confidentiality level, planned time, mode and location, his/her own meeting role and the Chair, and answers Accept or Decline. The current response is shown and can be changed while the Meeting is Published or InProgress (UC-29, UC-04).

This screen allows an invited Chair, Secretary, Participant or a verified external Guest to:

- View Invitation: view the meeting title, status, confidentiality level, planned time, mode and location, his/her meeting role and the Chair (UC-29).

- Accept Invitation: click “Attend” to record the response Accepted (UC-29).

- Decline Invitation: click “Not attending” to record the response Declined (UC-29).

- Change Response: choose the other option later; the latest response and its time are kept (UC-29).

Field Description

| Field Name | Description |
| --- | --- |
| App bar | TrustCabinet logo and name; the page needs no sidebar so it works on a phone. |
| Invitation header | Read-only: eyebrow “Meeting invitation” [Lời mời họp], meeting title, status chip (e.g. Published [Đã công bố]) and confidentiality chip (e.g. Normal [Thường]). |
| Time [Thời gian] | Read-only planned start and end of the Meeting, e.g. “Thursday, 15/10/2026 · 08:30–09:30”. |
| Mode [Hình thức] | Read-only mode and location, e.g. “Hybrid · Room 2.1” [Kết hợp · Phòng 2.1]. |
| Your role [Vai trò của bạn] | Read-only meeting role of the invitee: Chair, Secretary, Participant [Thành viên] or Guest. |
| Chair [Chủ trì] | Read-only name of the Chair of the Meeting. |
| Attend [Tham dự] | Button; records Accepted. Shown as primary (filled) when it is the current response. Only the owner of a current invitation can respond (BR-104). |
| Not attending [Không tham dự] | Button; records Declined. Shown as primary when it is the current response. A Guest may answer only Accept or Decline (BR-105). |
| Current response notice | Info box, e.g. “You chose: Attend · 10/10 09:02. You can change it before the meeting ends.” Responses can be changed while the Meeting is Published or InProgress (BR-106); the latest response and time are recorded (BR-115). |
| Messages | Success MSG105 “Meeting invitation response recorded successfully.”; MSG106 invalid response; MSG107 “This meeting invitation can no longer be answered.” when the Meeting is no longer Published/InProgress or the invitation was revoked or expired (BR-155, BR-157); MSG108 save failure. |

### 7. FE-07 – Personal Document Library & Meeting Document Management

#### 7.1 Document Library – Empty State

The Document Library – Empty State screen is what a signed-in internal user sees on opening Document Library [Kho tài liệu] from the sidebar before he/she has created any folder or uploaded any file. The folder panel holds only All documents with 0 files, and the file area shows an empty-state panel explaining how the library is used. The New folder and Upload buttons inside the panel do the same as those at the top of the page; a file uploaded while All documents is selected is stored at the root of the library. Once the library holds folders or files, the page is the Personal Document Library (7.2). External Guests have no personal library (UC-34, UC-36, UC-72a).

This screen allows any signed-in internal user (Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant) to:

- Open Document Library: click Document Library [Kho tài liệu] in the sidebar to open his/her own library page (UC-34).

- View Empty Library: see that his/her library has no folders or files yet and read how it is used (UC-34).

- Upload Document: click “Upload” at the top of the page or in the empty-state panel to store a file at the root of the library (UC-36).

- Create Folder: click “New folder” at the top of the page or in the empty-state panel to create a folder at the root of the library (UC-72a).

Field Description

| Field Name | Description |
| --- | --- |
| Sidebar item [Kho tài liệu] | Navigation item in the Business [Nghiệp vụ] group; highlighted when this page is open. |
| Page title | Read-only “Document Library” [Kho tài liệu]. |
| Page subtitle | Read-only: “Your private documents — only you can see and manage what is here.” [Tài liệu riêng của bạn — chỉ bạn thấy và quản lý được những gì ở đây.] |
| New folder [Thư mục mới] / Upload [Tải lên] | Shown both in the page header and inside the empty-state panel; both pairs do the same. New folder creates a folder at the root of the library (UC-72a); Upload opens the file picker and stores the chosen file at the root of the library (UC-36, BR-187). |
| Folder panel | Shows only All documents [Tất cả tài liệu] with the count 0, selected; no folder exists yet. |
| Empty-state panel | Dashed panel with a document icon in the file area, holding the title, explanation text, file hint and the New folder and Upload buttons. |
| Empty-state title | Read-only: “No documents yet” [Chưa có tài liệu nào]. |
| Explanation text | Read-only: “Upload documents to the library, put them into folders, then attach them to meetings where you are Chair or Secretary. A document not attached to any meeting can be read only by you.” [Tải tài liệu lên kho, xếp vào thư mục, rồi gắn vào cuộc họp mà bạn làm Chủ trì hoặc Thư ký. Tài liệu chưa gắn cuộc họp nào chỉ mình bạn đọc được.] |
| File hint | Read-only small text: “Accepts PDF and DOCX, up to 50 MB per file.” [Nhận PDF và DOCX, tối đa 50 MB mỗi tệp.] (BR-187) |

#### 7.2 Personal Document Library

The Personal Document Library is the Document Library page once the user’s library holds folders or files; an empty library shows the empty state (7.1). Each internal user has a private library: folders on the left and the file table on the right, with size, upload date and the number of meetings that use each file. Files have no versions; to change a document the user uploads a new file. Uploading a file to the library does not attach it to a meeting; files are attached through the Add Document to Meeting Dialog (7.4). A file uploaded straight into a meeting lands in a folder named after that meeting. External Guests have no personal library (UC-34, UC-36, UC-71, UC-72).

This screen allows any signed-in internal user (Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant) to:

- View Personal Document Library: view his/her own documents with file name, size, upload date and meeting usage (UC-34).

- Browse Folders: select All documents or a folder in the left panel to list the files and sub-folders it contains (UC-34).

- Search and Filter Documents: search by file name and filter by file type (UC-34).

- Upload Document: click “Upload” and choose a file from the computer to store it in the selected folder (UC-36).

- Organise Documents: rename a file, move it to another folder or delete it from the library; meetings that already use the file keep it (UC-71).

- Manage Folders: click “New folder” to create a folder, and rename, move or delete an empty folder from its menu; folders nest at most five levels (UC-72).

On the screen, s/he can also:

- Use Row Actions: use the icon buttons of a row to work with that file.

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Read-only title “Document Library” [Kho tài liệu] with the subtitle “Your private documents — only you can see and manage what is here.” Only the owner’s own documents are listed. |
| New folder [Thư mục mới] | Secondary button; creates a folder inside the selected folder, or at the root when All documents is selected. Nesting is limited to five levels (UC-72a, BR-191). |
| Upload [Tải lên] | Primary button; opens the file picker and uploads the chosen file into the selected folder (UC-36). Only PDF and DOCX files up to 50 MB are accepted (BR-187); a file that cannot be stored is rejected with an error and nothing is saved. |
| Folder panel | Tree of All documents [Tất cả tài liệu] (default, selected) and the user’s folders, each with the number of files. A folder named after a meeting (e.g. “Họp kế hoạch Sprint 6 - 15/10/2026”) holds files uploaded straight into that meeting. Each folder has a menu: Rename, Move, Delete (only when empty) (UC-72, BR-191, BR-192). |
| Search [Tìm theo tên tệp…] | Text input, optional, empty by default; matches the file name within the selected folder. |
| File type filter [Mọi loại tệp] | Dropdown. Values: All file types (default), PDF and Word (DOCX). |
| File name [Tên tệp] | Column: file icon and file name, e.g. “Ke-hoach-kinh-doanh-2027.docx”. |
| Size [Dung lượng] | Column: file size, e.g. 1,2 MB / 640 KB. |
| Uploaded [Ngày tải lên] | Column: upload date, format dd/mm/yyyy. |
| Used in [Đang dùng trong] | Column: number of meetings the file has been attached to, e.g. “2 meetings” [2 cuộc họp], or “Not used” [Chưa dùng] in italics. A file not attached to any meeting can be read only by its owner. |
| Actions [Thao tác] | Icon buttons per row: open preview, download, rename, move to another folder and delete from the library (UC-71). Delete asks for confirmation and states that meetings using the file keep it (BR-186). |

#### 7.3 Meeting Detail – Documents Tab

The Meeting Detail – Documents tab lists the documents attached to the selected Meeting in three groups, as in AI-eCabinet: By meeting content, with one block per Agenda Item in agenda order; Voting, with the documents attached to each Vote; and General documents, linked to no Agenda Item. A file linked to several Agenda Items appears under each of them and is uploaded only once. The Meeting keeps its own reference to each file, so deleting the file from a personal library does not remove it from the Meeting. Anyone who can view the Meeting reads its documents; the Chair and the Secretary add, link, unlink and remove documents until the Meeting ends (UC-32a, UC-32c, UC-33a, UC-33b, UC-33c, UC-33d, BR-275, BR-277).

This screen allows users who can view the meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, verified Guest) to:

- View Meeting Documents: view the documents grouped by Agenda Item, Vote or general, each with file name, page count for a PDF and number of comments (UC-32a).

- Switch Grouping: choose By meeting content, Voting or General documents (UC-32a).

- Open Document: click a document to open it in the Document Viewer with preview and comments (UC-32c).

On the screen, the Chair and the Secretary can also:

- Add General Document: click “Add general document” to add documents that belong to no Agenda Item through the Add Document to Meeting Dialog (UC-33a).

- Attach to Agenda Item: open the paperclip menu of an Agenda Item and choose Upload from computer, Choose from meeting documents or Choose from my library (UC-33a, UC-33b).

- Unlink or Remove Document: open the ⋯ menu of a row and choose Remove from this item or Remove from the meeting (UC-33d, UC-33c).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Documents [Tài liệu] is selected. Other tabs: General Information, Participants, Meeting Content, Voting, Recordings, Summary, Minutes. |
| Grouping [Theo nội dung họp / Biểu quyết / Tài liệu chung] | Segmented control: By meeting content (default), Voting, General documents; each segment shows its number of documents (UC-32a). |
| Agenda Item block | Collapsible block per Agenda Item in agenda order with order number, title and number of documents; during an In Progress Meeting the current item is expanded first. Tooltip: “A document can be attached to several items.” [Một tài liệu có thể gắn ở nhiều mục.] (BR-277). |
| Attach [paperclip icon] | Icon button on each Agenda Item block, Chair and Secretary only, until the Meeting ends. Menu: Upload from computer [Tải tệp lên từ máy], Choose from meeting documents [Chọn từ tài liệu cuộc họp], Choose from my library [Chọn từ Tài liệu của tôi] (UC-33a, UC-33b). |
| Document row | File type badge, file name, page count for a PDF and number of comments, e.g. “Ke-hoach-kinh-doanh-2027.pdf · 14 pages · 4 comments”. Who added the document and when are shown in a tooltip on the name. Clicking the row opens the Document Viewer (7.5). |
| Vote documents | In the Voting group, each Vote lists its documents with the user’s own state Read [Đã đọc] or Not read [Chưa đọc] (UC-74). |
| Row menu [⋯] | Chair and Secretary only, until the Meeting ends. Remove from this item [Gỡ khỏi mục này] unlinks the document from that Agenda Item only (UC-33d). Remove from the meeting [Gỡ khỏi cuộc họp] asks for confirmation and removes the document from every item; its comments are kept as history but no longer listed (UC-33c, BR-281). |
| Add general document [Thêm tài liệu chung] | Button, Chair and Secretary only; opens the Add Document to Meeting Dialog (7.4). |
| Access rules | Anyone who can currently view the Meeting reads its documents, including verified Guests and the Meeting Approver; there is no separate reading permission (BR-275). After the Meeting ends every add, link and remove control is hidden and the documents are read-only (BR-185, BR-282). |

#### 7.4 Add Document to Meeting Dialog

The Add Document to Meeting Dialog opens from “Add general document” or from the paperclip menu of an Agenda Item on the Documents tab. The Chair or the Secretary selects files from his/her personal library or uploads files from the computer. A document selected from the personal library remains in that library. A file uploaded from the computer is also stored in the uploader’s personal library, in the folder named after the meeting. All submitted documents are added together, or none is added (UC-33a, BR-184, BR-192, BR-286).

This screen allows the Chair or the Secretary to:

- Select Documents from Library: on the “From my library” tab tick one or more files of his/her personal library (UC-33a).

- Upload from Computer: on the “From computer” tab drop or choose up to 10 PDF or DOCX files at a time (UC-33a, BR-319).

- Add Documents: click “Add <n> documents” to attach the selected files to the Meeting (UC-33a).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without adding anything.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Add documents” [Thêm tài liệu]. |
| Tabs | From computer [Từ máy tính] / From my library [Từ kho của tôi]. |
| Library file list | From my library tab: folder picker, search by file name and rows with checkbox, file type badge, file name and upload date (e.g. “09/10”). Only the user’s own library files are listed; a file already in this Meeting is greyed out with “Already in the meeting” [Đã có trong cuộc họp]. At least one file must be selected. |
| Upload area | From computer tab: drop zone “Drag files here or click to choose” [Kéo tệp vào đây hoặc bấm để chọn]. Only PDF or DOCX, at most 50 MB each, and at most 10 files totalling 200 MB per submission; files that break a limit are named in an error and nothing is added (MSG164, MSG288, BR-187, BR-215, BR-319). |
| Agenda Item [Gắn với mục nghị sự] | Shown with the Agenda Item when the dialog opens from that item’s paperclip menu; the documents are then also linked to it (UC-33b). Opened from “Add general document”, the documents are general documents. |
| Access notice | Info box: “Everyone in the Meeting, including verified Guests, can read the added documents.” [Mọi người trong cuộc họp, kể cả khách mời đã xác minh, đều đọc được tài liệu đã thêm.] (BR-275) |
| Cancel [Huỷ] | Text button; closes the dialog. |
| Add <n> documents [Thêm <n> tài liệu] | Primary button showing the number of selected files (e.g. “Add 1 document”). Adds all files, records the user as the one who added them and refreshes the Documents tab. If any file cannot be added, an error is shown and nothing is attached (BR-286). |

#### 7.5 Document Viewer – Preview & Comments

The Document Viewer opens when a user clicks a document on the Documents tab, on an Agenda Item or in the Documents panel of the online room; opened from the room, it opens in a new browser tab so the call continues (BR-315). It renders the document in the browser – a PDF page by page, a DOCX as one continuous scrollable document – with zoom, and a side panel with its comments. Users who can view the Meeting preview the document, download it unless the Meeting is Secret, comment, and reply to a comment in one level (UC-32c, UC-35).

This screen allows users who can view the meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, verified Guest) to:

- Preview Meeting Document: read the document (a PDF page by page) and zoom in or out (UC-32c).

- View Comments: read the comments with author, page and time, each followed by its replies (UC-35).

- Comment on Document: type a comment and click “Send”; for a PDF it refers to the page being viewed (UC-35).

- Reply to Comment: click “Reply” under a comment; a reply to a reply joins the same thread, one level deep (UC-35, BR-317).

- Download Document: click “Download” to save the original file; not available in a Secret Meeting, where documents can only be viewed online (UC-32c).

On the screen, s/he can also:

- Back to Documents: click “‹ Documents” to return to the Documents tab of the Meeting, or to close the tab opened from the room.

Field Description

| Field Name | Description |
| --- | --- |
| Back link [‹ Tài liệu] | Returns to the Documents tab of the Meeting; in a tab opened from the room it closes that tab. |
| Document header | Read-only: file type badge, file name (e.g. “Du-thao-nghi-quyet-HDQT.pdf”), confidentiality chip of the Meeting (e.g. Secret [Mật] in red) and the meeting title. |
| Page indicator [Trang 2 / 6] | Read-only current page and total number of pages; updates while scrolling. Shown for PDF documents only. |
| Zoom − / + | Two square buttons that decrease or increase the zoom level of the preview. |
| Download [Tải về] | Secondary button; downloads the original file. In a Secret Meeting it is replaced by the chip “View online only” [Chỉ xem trực tuyến] (BR-188). |
| Preview pane | Read-only rendering made in the browser with no server conversion – PDF pages, or a DOCX as one continuous scrollable document; a highlighted passage marks the area a comment refers to. For a Secret document the viewer’s name and viewing time are printed as a watermark on every page. Each view or download of a Restricted or Secret document is recorded in the audit log (BR-318). |
| Comments tab [Bình luận 4] | Tab with the number of comments. Each comment shows avatar initials, author, page and relative time (e.g. “Page 2 · 2 hours ago”), the text and a “Reply” [Trả lời] link; replies are indented under it. A Guest’s comment shows the name the Guest gave with the label Guest [Khách mời] (ADR-0016). |
| Comment box [Viết bình luận…] | Textarea, required to send; for a PDF the comment is linked to the page being viewed, a DOCX comment has no page. Hidden after the Meeting ends, when comments are read-only (BR-282, MSG251). |
| Send [Gửi] | Primary button; posts the comment or reply, which appears in the list and increases the comment count on the Documents tab (UC-35). |

### 8. FE-08 – Online Meeting Room Management

#### 8.1 Meeting Lobby

The Meeting Lobby is the pre-join screen of the Online Meeting Room, laid out like Google Meet. It opens when a user clicks “Join online room” on an In Progress Online or Hybrid meeting, or when a verified Guest continues to the room. The page has its own top bar with the TrustCabinet mark and the signed-in account; the left side shows the camera preview with the microphone and camera switches and the device pickers, the right side the meeting and the Join button (UC-37, UC-39a, UC-39b, UC-04).

This screen allows the Chair, Secretary, Participant and a verified Guest to:

- Preview Camera and Microphone: see the camera preview and a voice level indicator while the microphone is on (UC-39a, UC-39b).

- Set Initial Microphone and Camera State: switch the microphone (on by default) and the camera (off by default) before entering; the browser remembers the choice for the next time (UC-39a, UC-39b).

- Select Devices: choose the microphone, speaker and camera (UC-39a, UC-39b).

- Join Meeting Room: click “Join room” to enter the Online Meeting Room – Video Grid (UC-37).

On the screen, s/he can also:

- Back to the Meeting: click “‹ Back to the meeting page” [Quay lại trang cuộc họp].

Field Description

| Field Name | Description |
| --- | --- |
| Top bar | TrustCabinet mark on the left; full name, email and initials avatar of the signed-in user on the right. |
| Camera preview | Dark rounded area showing the selected camera; “Camera is off” [Camera đang tắt] when the camera is off or not permitted by the browser. |
| Microphone / Camera switches | Two round icon buttons on the bottom edge of the preview, with tooltips such as “Turn off microphone” [Tắt micro]; a switched-off device is shown in light red. A voice level indicator shows while the microphone receives sound. |
| Device pickers [Micro / Loa / Camera] | Three pill buttons under the preview, each opening the devices detected by the browser (e.g. “MacBook Pro Microphone”). Without browser permission they read e.g. “No permission to use the microphone, or no microphone” and are disabled; the Speaker picker is hidden in browsers that cannot choose an audio output. |
| Heading | “Ready to join?” [Sẵn sàng tham gia?] followed by the meeting title. |
| Confidentiality chip | Shown only for a Restricted or Secret Meeting. |
| Room presence | e.g. “6 people in the room” [Đang có 6 người trong phòng], or “No one else is here” [Không có người nào khác ở đây]. |
| Vào phòng [Join room] | Primary pill button; reads “Joining…” while connecting. The system re-checks that the user can still view the Meeting, that it is In Progress and that it has an online room (BR-143, BR-152 – BR-157, BR-120). On success the user enters the Video Grid with the chosen devices. |
| Room unavailable message | When the Meeting has not started, has ended or is held in person only, the page shows only a message such as “The room opens only while the meeting is in progress.” [Phòng họp chỉ mở khi cuộc họp đang diễn ra.] and “Back to the meeting page”; after a dropped connection it also offers “Rejoin” [Vào lại]. |

#### 8.2 Online Meeting Room – Video Grid

The Online Meeting Room – Video Grid is the full-screen main view of the online room, laid out like Google Meet: a one-line blue header with a gold bottom edge, a navy stage with the video tiles, the call controls centred at the bottom as icons with tooltips, and the People and Chat panel buttons at the bottom right. Leaving the room does not change the meeting lifecycle; only the Chair ends the meeting (UC-37, UC-38, UC-39a, UC-39b, UC-40).

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- View Video Grid: see the camera image or initials of the people in the room, with the current speaker highlighted, muted people marked and the Chair tagged; the grid pages when more people are connected than fit (UC-40).

- Manage Microphone: turn his/her own microphone on or off (Ctrl+D) and choose the microphone and speaker (UC-39a).

- Manage Camera: turn his/her own camera on or off (Ctrl+E) and choose the camera (UC-39b).

- Leave Meeting Room: click “Leave” and confirm to disconnect without ending the meeting (UC-38).

On the screen, s/he can also:

- Share Screen: present a screen, window or tab to everyone (8.4, UC-39c).

- Open Floating Window: move the call into a small picture-in-picture window to work in other tabs while staying in the room.

- Open Side Panels: open the Participants panel (8.3) or the Chat panel (8.4).

Field Description

| Field Name | Description |
| --- | --- |
| Room header | Back button ‹ (asks for confirmation, as Leave does), meeting title, chip “6 people in the room” [6 người trong phòng] and the confidentiality chip for a Restricted or Secret Meeting. |
| Video tile | Camera image, or a circle with the user’s initials when the camera is off; name label with a crossed microphone when muted, “(Bạn)” [You] for the local user and the gold tag “Chủ toạ” [Chair]. |
| Active speaker highlight | Blue border around the tile of the person currently speaking. |
| Pager ‹ 1 / 2 › | Shown when more people are connected than the stage can display. |
| Microphone [Tắt micro / Bật micro] | Icon button, red when off; the arrow ⌃ beside it opens the microphone and speaker pickers. A user muted by the Chair is told so and may turn the microphone on again (UC-39a, UC-41). |
| Camera [Tắt camera / Bật camera] | Icon button, red when off; the arrow ⌃ opens the camera picker (UC-39b). |
| Share screen [Chia sẻ màn hình] | Icon button, highlighted while sharing (8.4, UC-39c). |
| Floating window [Mở cửa sổ nổi] | Icon button in browsers that support it; while the call is in the floating window the stage shows “The meeting is in a floating window” with “Bring the meeting back to this window”. |
| Leave [Rời phòng] | Red button. Confirmation “Leave the meeting room – The meeting continues with the others. You can rejoin while the meeting is in progress.” with Stay [Ở lại] and Leave [Rời phòng]. The meeting stays In Progress; when the Chair ends the meeting every connected user is disconnected (UC-38, BR-143). |
| Participants / Chat [Người dự / Chat] | Icon buttons at the bottom right that open the side panels (8.3, 8.4); Chat shows the number of unread messages. |
| Connection banners | “Connection lost, reconnecting…” [Mất kết nối, đang kết nối lại…] while reconnecting, and a banner with a button when the browser blocks audio playback. |

#### 8.3 In-Room Participants Panel

The In-Room Participants Panel is the white 360 px panel on the right of the online room, opened with the Participants icon at the bottom right. It lists the users currently connected to the room with their meeting role. Every connected user can view the list; only the Chair can mute another user (UC-40, UC-41).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Meeting Room Participants: see the number and the list of users in the room, sorted by meeting role (Chair, Secretary, Member, Guest) and then by name, with who is speaking and whose microphone is off (UC-40).

On the screen, the Chair can also:

- Mute a Participant: click “Mute” next to a user whose microphone is on (UC-41).

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Người dự (6)] | Title with the number of users currently connected (UC-40). |
| Participant row | Initials avatar, full name with “(Bạn)” [You] for the local user, and the meeting role below: Chủ toạ [Chair], Thư ký [Secretary], Thành viên [Member], Khách mời [Guest]. |
| Speaking indicator | Green dot while the user is speaking. |
| Microphone state | Microphone icon; shown red and crossed out when the microphone is off. |
| Tắt mic [Mute] | Outline button shown only to the Chair, on other users whose microphone is on. The user is told “The Chair has muted your microphone. You can turn it back on when you need to speak.” (UC-41). |
| Not provided | Raising a hand, muting everyone and removing a user from the room are not part of the current version. |

#### 8.4 Screen Sharing & Chat

The Screen Sharing & Chat layout appears when a connected user presents his/her screen and/or opens the Chat panel. The shared screen takes the main stage and the video tiles move to a strip beside it. Chat lives only in the room: messages are seen by the people currently in the room and are not kept after the meeting (UC-39c, UC-42).

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- Manage Screen Sharing: start or stop presenting a screen, window or tab; one person presents at a time, and when someone else starts, the previous presenter’s share stops and that user is told (UC-39c).

- View Shared Screen: follow the screen shared by another user on the main stage (UC-39c).

- Use In-room Chat: read the messages sent since joining and send a text message to everyone currently in the room (UC-42).

On the screen, s/he can also:

- Close the Chat Panel: click the Chat icon again to return to the full stage.

Field Description

| Field Name | Description |
| --- | --- |
| Shared screen stage | Main area showing the shared screen; caption “Phạm Thị Lan is presenting” [Phạm Thị Lan đang trình bày]; the presenter sees “You are presenting to everyone” [Bạn đang trình bày cho mọi người]. |
| Video tile strip | Video tiles beside the shared screen, as in the Video Grid. |
| Share screen [Chia sẻ màn hình / Dừng chia sẻ] | Toolbar toggle, highlighted while sharing; the browser asks which screen, window or tab to share (UC-39c). |
| Chat panel title | “Chat”. |
| Chat notice | “Messages are visible only to people in the room and are not kept after the meeting.” [Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau cuộc họp.] |
| Message list | Messages in time order with sender (“Bạn” [You] for the user’s own messages) and time hh:mm; “No messages yet.” [Chưa có tin nhắn nào.] when empty. A user who joins later does not see earlier messages. |
| Message box | Textarea “Type a message — Enter to send, Shift+Enter for a new line”, at most 1,000 characters with a counter (e.g. “0/1000”). |
| Gửi [Send] | Primary button, enabled when the message is not blank. If sending fails: “Could not send the message. Please try again.” [Không gửi được tin nhắn. Vui lòng thử lại.] |

#### 8.5 In-Room Voting Panel

The In-Room Voting Panel is planned (SCRUM-96) and not part of the current version. It adds a Voting icon next to Participants and Chat; the white 360 px panel reuses the components of the Voting tab of the meeting detail page (ADR-0011). When the Chair opens a Vote, the panel opens for everyone in the room (UC-52b, UC-55).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Vote Status: see the open Vote, when it opened, whether the ballot is secret and how many eligible voters have voted (UC-52b).

- Cast Vote: choose one option and submit the ballot once; available only to users on the eligible list fixed when the Vote opened, i.e. current holders of the meeting roles the Chair selected, verified Guests included when Guest is selected (UC-55, BR-190).

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Biểu quyết] | Title of the panel; label “Planned” [Chưa làm] in the wireframe. |
| Vote status line | Status chip “Open” [Đang mở] (green) followed by “Opened at 13:58 · secret ballot”. A Vote belongs to the Meeting, not to an Agenda Item (ADR-0019). |
| Vote question | Read-only bold text, e.g. “Thông qua kế hoạch kinh doanh năm 2027”. |
| Ballot progress | “Voted 7/11” [Đã bỏ phiếu 7/11]: ballots cast out of eligible voters; no option results while the Vote is open, and for a secret ballot no indication of who has just voted. |
| Vote options | Radio cards, exactly one may be selected: Đồng ý [Agree], Không đồng ý [Disagree], Không ý kiến [Abstain]. |
| Gửi phiếu [Submit vote] | Primary button, shown only to eligible users who still have access and have not voted. Hint: “A submitted ballot cannot be changed or withdrawn.” After submission the panel only confirms the ballot (BR-160, BR-165, BR-190). |

#### 8.6 In-Room Documents Panel

The In-Room Documents Panel is planned (SCRUM-21) and not part of the current version. It adds a Documents icon next to Participants and Chat; the white 360 px panel reuses the Documents tab component in its room variant (ADR-0011). It is read-only: the documents of the current Agenda Item come first, and a document opens in the Document Viewer in a new browser tab so the call is not interrupted (UC-32a, UC-32c, BR-315, BR-316).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Meeting Documents: see the documents of the current Agenda Item first, the other items collapsed and the general documents (UC-32a).

- Open Document: click a document to read and comment on it in a new tab while staying in the room (UC-32c, UC-35, BR-315).

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Tài liệu (5)] | Title with the number of Meeting documents; label “Planned” [Chưa làm] in the wireframe. |
| Online-only chip [Chỉ xem trực tuyến] | Shown for a Secret Meeting, whose documents cannot be downloaded (BR-188). |
| Current item group | “Current item · 2. Báo cáo kế hoạch kinh doanh 2027” [Mục đang bàn]; follows the Chair when the current Agenda Item changes. |
| Document row | File type badge, file name, page count and number of comments; tooltip “Opens in a new tab · you stay in the room” [Mở ở tab mới · bạn vẫn ở trong phòng]. |
| Other items [Mục khác] | Collapsed rows with order number, title and number of documents; expanding one lists its documents. |
| General documents [Tài liệu chung] | Documents linked to no Agenda Item. |
| Read-only rule | No add, link or remove actions in the room (BR-316); documents added from the Documents tab appear in the panel without reloading. To show a page to everyone, the presenter shares the screen (8.4). |

### 9. FE-09 – Meeting Audio Upload & Media Library

#### 9.1 Meeting Detail – Recordings Tab

The Meeting Detail – Recordings tab [Bản ghi] is the media library of a meeting. TrustCabinet never records meetings itself: after the meeting has Ended, the Secretary uploads the audio file(s) recorded outside the system, the system transcribes each file and shows the speaker-attributed transcript here, and the Secretary then generates the meeting summary from it. Authorized users of the meeting can view the media library and play the audio; the transcript and the upload controls are available only to the assigned Secretary (UC-43a, UC-43b, UC-44, UC-45, UC-46, UC-47).

This screen allows the Secretary to:

- View Media Library: see the audio files uploaded for the meeting with duration, size and uploader (UC-43a).

- Upload Meeting Audio: drag and drop an audio file or click “Choose file” to open the Upload Audio Dialog (UC-44).

- View Processing Status: see whether the transcript of a file is being processed, processed or failed, and reprocess a failed file (UC-45).

- View Speaker-Attributed Transcript: read the transcript segments with time, speaker and text (UC-45).

- Correct Speaker Attribution: change the speaker of a segment directly in its dropdown (UC-46).

- Generate Meeting Summary: click “Generate summary from transcript” to have AI create a draft summary (UC-47).

On the screen, s/he can also:

- Open the Full Transcript Editor: go to the Speaker-Attributed Transcript screen to play the audio, search, edit text and assign unknown speakers (UC-43b, UC-45, UC-46).

- Navigate Detail Tabs: switch to the other meeting detail tabs.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Back link “‹ Cuộc họp”, meeting title with status chip (e.g. Đã kết thúc [Ended]) and confidentiality chip (e.g. Thường [Normal]); second line: actual time and date “08:32–09:41 · 15/10/2026” and mode · location “Kết hợp · Phòng 2.1, tầng 2” [Hybrid · Room 2.1, floor 2]. |
| Tabs | Thông tin chung, Thành phần tham dự, Nội dung cuộc họp, Tài liệu, Biểu quyết, Bản ghi [Recordings] (selected), Tóm tắt [Summary], Biên bản [Minutes]. |
| Transcript panel header | “Transcript · hop-sprint6.m4a”: the file whose transcript is shown, with a processing status chip: Đang xử lý [Processing], Xử lý xong [Processed] (green) or Lỗi [Failed] with a “Xử lý lại” [Reprocess] button. |
| Transcript segment | One row per segment: start time (hh:mm:ss, e.g. 00:00:12), speaker dropdown and the transcribed text. Transcription runs on the uploaded file after upload; there is no real-time transcription and no translation. |
| Speaker dropdown | Lists the meeting’s internal participants and Guests; changing it re-assigns that segment (UC-46). A voice not yet identified is shown as “Người nói 3” [Speaker 3] with an amber border. |
| Transcript footer | Read-only: “Đoạn 1–5 trong 214” [Segments 1–5 of 214] and “Sửa lần cuối 15/10 10:20” [Last edited]. |
| Upload drop zone | Dashed area “Kéo thả file audio vào đây” [Drag and drop an audio file here], hint “MP3, M4A, WAV · tối đa 500 MB”, button “Chọn file” [Choose file] that opens the Upload Audio Dialog. Shown only to the Secretary and only after the meeting has Ended (UC-44). |
| File đã tải lên [Uploaded files] | Media library list; each file shows the name (e.g. hop-sprint6.m4a) and “1 giờ 09 phút · 64 MB · Đỗ Hải Yến” [duration · size · uploaded by] (UC-43a). Audio files only – the system stores no video. |
| Rejected file message | Red inline box for a file that failed validation, e.g. “ghi-am-phong.wma: định dạng không hỗ trợ. Chọn file MP3, M4A hoặc WAV.” [unsupported format. Choose an MP3, M4A or WAV file.]; files over 500 MB are rejected in the same way. The user can choose another file. |
| Tạo tóm tắt từ transcript [Generate summary from transcript] | Primary button, enabled when at least one transcript is Processed. Sends the edited transcript to the AI provider to create a Draft summary and opens the Meeting Summary tab (UC-47). |
| Visibility rule | Other authorized users of the meeting (Meeting Organizer, Meeting Approver, Chair, Participant, System Admin) see only the media library and can play the audio (UC-43a, UC-43b); the transcript, upload and summary actions are shown only to the assigned Secretary. |

#### 9.2 Upload Audio Dialog

The Upload Audio Dialog [Tải audio lên] is opened by the Secretary from the Recordings tab with “Choose file” or by dropping a file on the upload area. Because the system never records meetings, this dialog is the only way to add a recording: the Secretary chooses an audio file (no video), confirms that attendees were informed of and agreed to the recording, and uploads it. After a successful upload the system starts transcription automatically and the progress is shown on the Recordings tab (UC-44).

This screen allows the Secretary to:

- Select Audio File: drag a file into the drop area or click it to choose an MP3, M4A or WAV file of up to 500 MB (UC-44).

- Confirm Recording Consent: tick the confirmation that attendees were informed and consented to the recording (UC-44).

- Upload Meeting Audio: click “Upload” to send the file and follow the upload progress (UC-44).

- Cancel: close the dialog without uploading, or stop an upload in progress.

Field Description

| Field Name | Description |
| --- | --- |
| Tải audio lên [Upload audio] | Dialog title. The dialog is available only to the assigned Secretary of the meeting and only when the meeting is Ended. |
| Drop zone | Dashed area “Kéo tệp vào đây hoặc bấm để chọn” [Drag a file here or click to choose] with the hint “MP3, M4A, WAV · tối đa 500 MB”. Accepts one audio file at a time. |
| File validation | A file with another format (e.g. .wma or any video file) or larger than 500 MB is rejected immediately with an inline error naming the file and the accepted formats; the user can choose another file. |
| Selected file row | File name and size (e.g. “hop-sprint6.m4a · 64 MB”) with a progress bar and percentage (e.g. 64%) while uploading. |
| Recording consent [Xác nhận đồng ý ghi âm] | Checkbox, required, unticked by default: “Tôi xác nhận người tham dự đã được thông báo và đồng ý ghi âm theo Nghị định 13/2023/NĐ-CP.” [I confirm that attendees were informed and consented to the recording under Decree 13/2023/ND-CP.] Required because the recording is personal data. |
| Huỷ [Cancel] | Ghost button; closes the dialog and discards the selected file; an upload in progress is cancelled. |
| Tải lên [Upload] | Primary button, enabled when a valid file is selected and the consent box is ticked. Uploads the file, adds it to the media library with uploader and upload time, and starts transcription automatically (status Processing on the Recordings tab). If the upload fails, an error is shown and the Secretary can try again. |

### 10. FE-10 – Speaker-Attributed Transcription & Meeting Summary

#### 10.1 Speaker-Attributed Transcript

The Speaker-Attributed Transcript screen is the transcript editor inside the Recordings tab, used by the Secretary after the system has transcribed an uploaded audio file. The transcript is split into timed segments; the system separates the voices as “Speaker 1, 2, …” but does not identify people automatically, so the Secretary assigns each unknown voice to a person from the meeting attendee list – once for all segments of that voice – and corrects the wording before generating the summary. There is no real-time transcription and no translation (UC-43b, UC-45, UC-46, UC-47).

This screen allows the Secretary to:

- Select Audio File: choose which uploaded file’s transcript to work on (UC-45).

- Play Meeting Audio: play, pause and seek the audio, or click a segment time to listen to exactly that segment (UC-43b).

- View Speaker-Attributed Transcript: read the segments with start time, speaker and text, and search the transcript (UC-45).

- Edit Transcript Text: click a segment to correct its text (UC-46).

- Assign Speaker: assign an unknown voice to an attendee for all of its segments, or change the speaker of a single segment (UC-46).

- Generate Meeting Summary: click “Generate summary” to create the AI draft summary from the edited transcript (UC-47).

Field Description

| Field Name | Description |
| --- | --- |
| Audio file selector | Dropdown listing the uploaded audio files of the meeting, e.g. “hop-sprint6.m4a”; default the latest file. |
| Processing status chip | Đang xử lý [Processing] / Xử lý xong [Processed] / Lỗi [Failed]. Editing is possible only when the file is Processed. |
| Tìm trong transcript [Search in transcript] | Text input; filters or highlights segments containing the keyword. |
| Tạo tóm tắt [Generate summary] | Primary button; sends the current edited transcript to the AI provider and opens the Meeting Summary tab with a new Draft (UC-47). |
| Audio player | Play/pause button, current position (e.g. 00:14:02), seek bar and total duration (e.g. 01:09:12) (UC-43b). |
| Segment time | Start time of the segment (hh:mm:ss); clicking it moves the player to that point and plays it. The segment being played is highlighted. |
| Speaker dropdown | Per segment; lists the meeting’s internal participants and Guests. Changing it affects only that segment. An unassigned voice is shown as “Người nói 3” [Speaker 3] with an amber border (UC-46). |
| Segment text | Transcribed text; click to edit inline, saved when leaving the field. The last-edit time is updated (UC-46). |
| Gán “Người nói 3” [Assign “Speaker 3”] card | Opened when an unassigned voice is selected; hint “Chọn trong danh sách tham dự. Áp dụng cho mọi đoạn của giọng này (12 đoạn).” [Choose from the attendee list. Applies to all segments of this voice (12 segments).] Radio list of attendees, e.g. “Ngô Thanh Vân · khách” [Guest], “Phạm Đức Long”, “Lê Thu Hà”; one must be selected. |
| Gán cho 12 đoạn [Assign to 12 segments] | Primary button; assigns the selected attendee to every segment of that voice in one step and refreshes the transcript (UC-46). |
| Visibility rule | Viewing and editing the transcript is limited to the assigned Secretary of the meeting (permission matrix, Transcript – View / Edit). |

#### 10.2 Meeting Summary

The Meeting Summary tab [Tóm tắt] shows the summary that AI generated from the edited transcript of an Ended meeting. The summary goes through three states – Draft, Approved, Published – and approving and publishing are two separate actions of the Secretary: the Secretary reviews and edits the draft, approves it, and only then publishes it, which notifies the attendees. Action items in the summary are plain text with no assignee or deadline (UC-47, UC-48, UC-49, UC-50, UC-51).

This screen allows the Secretary to:

- View Meeting Summary: read the summary sections and see its source and status history (UC-48).

- Edit Meeting Summary: correct the text of the draft summary before approving it (UC-49).

- Regenerate Meeting Summary: click “Regenerate” to create a new AI draft from the current transcript (UC-47).

- Approve Meeting Summary: click “Approve summary” to confirm the content (UC-50).

- Publish Meeting Summary: click “Publish” after approval to release the summary and notify attendees (UC-51).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Back link, meeting title, status chip Đã kết thúc [Ended], confidentiality chip, actual time and date, mode and location – as on the other detail tabs. |
| Tạo lại [Regenerate] | Secondary button. Generates a new draft from the current edited transcript through the AI provider and replaces the current draft; the status returns to Draft (UC-47). |
| Duyệt tóm tắt [Approve summary] | Secondary button, enabled while the summary is Draft. Sets the status to Approved and records approver and time (UC-50). |
| Công bố [Publish] | Primary button, disabled (greyed) until the summary is Approved. Sets the status to Published and sends a notification to the attendees (UC-51). |
| Bản nháp tóm tắt [Draft summary] | Card title with status chip Bản nháp [Draft] (amber), Đã duyệt [Approved] or Đã công bố [Published]. |
| Nội dung chính [Main content] | Summary text of the discussion, e.g. “Sprint 5 hoàn thành 18/21 story; …”. Editable by the Secretary while the summary is Draft (UC-49). |
| Kết luận [Conclusions] | Text of the conclusions reached, e.g. “Mục 2: Thông qua phạm vi biểu quyết chỉ trong cuộc họp (3 đồng ý, 1 không ý kiến).” Editable while Draft. |
| Việc cần làm (văn bản) [Action items (text)] | Free text only, e.g. “Cập nhật tài liệu FE-12; chốt lịch demo ngày 22/10.”; no assignee, deadline or task tracking. |
| Nguồn [Source] | Read-only: “Transcript đã sửa · 214 đoạn” [Edited transcript · 214 segments] and “Tạo bởi AI lúc 15/10 10:31” [Generated by AI at]. |
| Status timeline | Read-only three steps with actor/time: Bản nháp [Draft] – “AI tạo · 15/10 10:31”; Đã duyệt [Approved] – “Chưa” [Not yet]; Đã công bố [Published] – “Chưa”. The current step is marked in amber. |
| Approval notice | Read-only info box: “Duyệt xong mới công bố được. Công bố sẽ gửi thông báo cho người tham dự.” [The summary can be published only after approval. Publishing sends a notification to attendees.] |
| Visibility rule | Generating, viewing, editing, approving and publishing the summary are limited to the assigned Secretary (permission matrix, Summary). |

### 11. FE-11 – Voting Management

Scope: create/open/cast only while Meeting is InProgress. End Meeting is independent of Vote closure. Chair can close and tally existing Open Votes in InProgress or Ended, then record the conclusion. No post-meeting ballot casting, time-boxed voting or quorum. (BR-159, BR-160, BR-165)

#### 11.1 Meeting Detail – Voting Tab

The Meeting Detail – Voting tab [Biểu quyết] lists every Vote that belongs directly to the selected Meeting. It remains readable after Ended; casting stops at Meeting end. While InProgress, the header shows the actual start time, elapsed time and the current Agenda Item. The Chair creates/opens during InProgress, and may close outstanding Open Votes during InProgress or after Ended; other users involved in the Meeting see the same list read-only. Votes are never held before or after the Meeting and have no deadline or quorum (UC-52a, UC-52b, UC-53, UC-54, UC-56).

This screen allows the Chair to:

- View Meeting Votes: view all Votes of the Meeting with status, linked Agenda Item, ballot type and opening time (UC-52a).

- View Vote Status: follow the turnout of an open Vote (ballots cast / eligible voters) in real time (UC-52b).

- Edit Draft Vote: click “Edit” on a Draft Vote to change its content, options or ballot type in the Create & Open Vote Dialog (UC-53).

- Delete Draft Vote: click “Delete” on a Draft Vote to remove it; an open or closed Vote cannot be deleted (UC-53).

- Open Vote: click “Open vote” on a Draft Vote so that eligible people can cast their ballots (UC-54).

- Close Vote: click “Close voting” on an open Vote to stop balloting; the system tallies the result and the Vote moves to Vote Result & Conclusion (UC-56).

On the screen, s/he can also:

- Create Vote: click “Create vote” to open the Create & Open Vote Dialog for the current Agenda Item (UC-53).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: Meeting title, status chip InProgress [Đang họp], confidentiality chip (e.g. Normal [Thường]), actual start time and elapsed time (“Bắt đầu 08:32 · đã họp 22 phút”) and the current Agenda Item (“Mục hiện tại: 2. …”). |
| Tabs | General Information · Participants · Meeting Agenda · Documents · Voting · Recordings · Summary · Minutes [Thông tin chung · Thành phần tham dự · Nội dung cuộc họp · Tài liệu · Biểu quyết · Bản ghi · Tóm tắt · Biên bản]; Voting is selected. |
| Vote count [n biểu quyết] | Read-only number of Votes that belong to the Meeting. |
| Create vote [Tạo biểu quyết] | Primary button, visible only to the current Chair and enabled only while the Meeting is InProgress (BR-160, BR-165). Opens the Create & Open Vote Dialog. |
| Vote status chip | Draft [Nháp] (dashed card), Open [Đang mở], Closed [Đã đóng]; after the Chair records a conclusion the Vote shows its conclusion. Lifecycle: Draft → Open → Closed → Concluded. |
| Vote content | Bold text of the motion, e.g. “Thông qua phạm vi biểu quyết chỉ trong cuộc họp”. |
| Vote meta | Read-only: linked Agenda Item (“Mục 2”), ballot type public [công khai] or secret [bỏ phiếu kín] and opening time (“mở 08:51”). |
| Turnout bar [Đã bỏ phiếu] | Progress bar with “cast / eligible” count (e.g. 3/5), shown for an open Vote and updated as ballots arrive. Individual choices are not shown while the Vote is open. |
| Eligibility note | Read-only text, e.g. “5 người đủ điều kiện: Chủ trì, Thư ký, Thành viên, Khách mời.” Counts the current holders of the roles selected for this Vote; the list is fixed when the Vote opens, and attendance does not decide eligibility (BR-160, BR-181, BR-190). |
| Close voting [Đóng phiếu] | Chair only; close and tally an existing Open Vote while Meeting is InProgress or Ended. End Meeting is independently available and never requires this action. After Meeting Ended no new ballot is accepted, even if Vote remains Open. (BR-159–BR-161, BR-165) |
| Edit [Sửa] | Button on a Draft Vote, Chair only; opens the dialog pre-filled. An open or closed Vote can no longer be edited. |
| Delete [Xoá] | Button on a Draft Vote, Chair only; asks for confirmation and deletes the Draft Vote. An open or closed Vote cannot be deleted (UC-53, BR-189). |
| Open vote [Mở biểu quyết] | Primary button on a Draft Vote, Chair only, enabled while the Meeting is InProgress (BR-165). Eligible voters see the Vote immediately in Cast Vote. |

#### 11.2 Create & Open Vote Dialog

The Create & Open Vote Dialog opens over the Voting tab when the Chair clicks “Create vote” (or “Edit” on a Draft Vote). The title shows the Agenda Item the Vote is linked to, normally the current item (“Tạo biểu quyết · Mục 2”). The Vote belongs directly to the Meeting, can only be created while the Meeting is InProgress and has no deadline or quorum (UC-53, UC-54).

This screen allows the Chair to:

- Create / Configure Vote: enter the motion, define and order the voting options, choose public or secret ballot and the meeting roles allowed to vote (UC-53).

- Save as Draft: click “Save draft” to keep the Vote in Draft status on the Voting tab for opening later (UC-53).

- Open Vote: click “Open vote” to save the Vote and open it at once for eligible voters (UC-54).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without saving.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title [Tạo biểu quyết · Mục n] | Read-only; shows the linked Agenda Item of the Meeting (by default the current Agenda Item). |
| Vote content [Nội dung biểu quyết] * | Text input, required; the motion put to the vote, e.g. “Thông qua phạm vi biểu quyết chỉ trong cuộc họp”. Empty content blocks saving. |
| Options [Phương án] | List of text inputs, pre-filled with Agree [Đồng ý], Disagree [Không đồng ý], Abstain [Không ý kiến]. Each option must be non-empty and options must be distinct; at least two options are required. |
| Drag handle | Icon at the left of each option; drag to reorder the options shown to voters. |
| Remove option | Icon at the right of each option; removes that option (not allowed below two options). |
| Add option [+ Thêm phương án] | Link button; appends an empty option row. |
| Ballot type [Hình thức] | Segmented control: Public [Công khai] (default) – each voter’s choice is shown with the result; Secret ballot [Bỏ phiếu kín] – only totals are shown. |
| Eligible voters [Người được bỏ phiếu] | Multi-select of meeting roles: Chair [Chủ trì], Secretary [Thư ký], Member [Thành viên], Guest [Khách mời]; all selected by default and at least one required. Eligible voters are the current holders of the selected roles when the Vote opens; the list and its count are then fixed. A Guest votes with the invitation identity, not the display name or room connection. (BR-160, BR-181, BR-190) |
| Cancel [Huỷ] | Link button; closes the dialog, nothing is saved. |
| Save draft [Lưu nháp] | Button; saves the Vote with status Draft. Allowed only while the Meeting is InProgress and the user is its current Chair (BR-165). |
| Open vote [Mở biểu quyết] | Primary button; saves and opens the Vote (status Open, opening time recorded). If the Meeting is no longer InProgress the Vote is not created and the reason is shown (BR-160). |

#### 11.3 Cast Vote

The Cast Vote screen (shown here in its compact/mobile layout) is opened by an eligible voter from the open Vote card, the Meeting Workspace, the Personal Dashboard or the in-room meeting panel. It shows one open Vote of the InProgress Meeting and lets the voter submit exactly one ballot (UC-55).

This screen allows the Chair, Secretary, Participant and verified Guest (eligible voters) to:

- View Open Vote: view the motion, the Meeting and Agenda Item it belongs to, the ballot type and the opening time (UC-52b).

- Cast Vote: select one option and click “Submit ballot”; each eligible voter can vote only once and the ballot cannot be edited (UC-55).

Field Description

| Field Name | Description |
| --- | --- |
| Screen title [Biểu quyết] | Read-only header. |
| Status chip | Open [Đang mở] with opening time (“Mở lúc 08:51”). When the Vote has been closed the options are disabled and the screen states that voting has ended. |
| Vote content | Read-only bold text of the motion. |
| Vote context | Read-only: Meeting title · Agenda Item · ballot type, e.g. “Họp kế hoạch Sprint 6 · Mục 2 · công khai”. |
| Options | Radio group, required, one choice only; values are the options defined by the Chair (e.g. Agree [Đồng ý], Disagree [Không đồng ý], Abstain [Không ý kiến]). The selected option is highlighted. |
| Submit ballot [Gửi phiếu] | Primary button, enabled when an option is selected. Accepted only while the Meeting is InProgress, the Vote is Open and the user is on the eligible list fixed when the Vote opened – a current Chair, Secretary, Participant or verified Guest in a selected role – and has not voted yet (BR-160, BR-165, BR-190). After submission the screen shows the recorded choice and the turnout. |
| Immutability note [Phiếu đã gửi không sửa được.] | Read-only hint: a submitted ballot cannot be changed or withdrawn. |

#### 11.4 Vote Result & Conclusion

The Vote Result & Conclusion screen is shown on the Voting tab after the Chair closes a Vote. It presents the tally per option and, for a public Vote, each person’s choice. The Chair then records the conclusion Passed, NotPassed or Deferred; the tally and conclusion are carried into the Meeting Minutes (UC-52b, UC-57).

This screen allows the Chair to:

- View Vote Result: view the number of voters, ballot type, opening/closing time and the count and percentage of each option (UC-52b).

- View Public Ballots: for a public Vote, see how each eligible person voted and who did not vote (UC-52b).

- Record Vote Conclusion: select Passed, NotPassed or Deferred, optionally add a note and click “Record conclusion” (UC-57).

Field Description

| Field Name | Description |
| --- | --- |
| Vote content | Read-only bold text of the motion. |
| Status chip | Closed [Đã đóng]; after the conclusion is saved the chip shows the conclusion. |
| Result summary | Read-only, e.g. “4/5 người đã bỏ phiếu · công khai · mở 08:51, đóng 08:55”: voters / eligible, ballot type, opening and closing time. |
| Option result rows | One row per option: option label, bar and “count · percentage”, e.g. Agree 3 · 75%, Disagree 0 · 0%, Abstain 1 · 25%; percentages are calculated on ballots cast. Calculated by the system when the Vote is closed (BR-161). |
| Conclusion [Kết luận] * | Segmented control, required: Passed [Thông qua], NotPassed [Không thông qua], Deferred [Hoãn]. Enabled only for the current Chair and only after the Vote is closed (BR-161, BR-165). |
| Conclusion note [Ghi chú kết luận] | Text input, optional, e.g. “Áp dụng từ Sprint 6.”; included with the conclusion in the minutes. |
| Record conclusion [Ghi kết luận] | Primary button, Chair only; saves the conclusion on the Vote. Other users see the conclusion read-only. |
| Public ballots [Phiếu công khai] | For a public Vote, list every person on the eligible list, internal or Guest, with the saved choice, or “Không bỏ phiếu” [Did not vote] for someone who cast no ballot (e.g. Phạm Đức Long · Thành viên – Không bỏ phiếu). Guest uses the self-declared display profile without exposing a legal identity claim. Attendance is reference only. A secret Vote hides all voter-choice links. (BR-160, BR-165, BR-181) |
| Minutes note [Kết luận đưa vào biên bản (FE-12).] | Read-only hint: the tally and the recorded conclusion are included in the Meeting Minutes; no separate resolution is created (BR-161). |

### 12. FE-12 – Meeting Minutes & Digital Signing

#### 12.1 Minutes Editor

The Minutes Editor is the Minutes tab [Biên bản] of an Ended Meeting as seen by the assigned Secretary while the minutes are Draft or Revision Requested. The draft is filled from the Meeting information, attendance, the published Meeting Summary and the Vote conclusions recorded by the Chair; the Secretary edits the wording and submits it to the designated signers (UC-58, UC-59, UC-60, UC-61).

This screen allows the Secretary to:

- View Meeting Minutes: view the current draft, its status, revision number and last auto-save time (UC-58).

- Generate Meeting Minutes: click “Fill from meeting data” to (re)build the draft from the Meeting information, attendance, published summary and Vote results with conclusions (UC-59).

- Edit Meeting Minutes: edit the content in the rich-text editor; changes are saved automatically (UC-60).

- Submit Meeting Minutes for Signing: click “Submit for signing” when all conditions are met; the minutes become Pending Signature and signers are notified (UC-61).

On the screen, s/he can also:

- Check Signers and Conditions: review the designated signers and the checklist of conditions that must be met before submission.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: Meeting title, status Ended [Đã kết thúc], confidentiality chip, actual time and date (“08:32–09:41 · 15/10/2026”), mode and location (“Kết hợp · Phòng 2.1, tầng 2”). |
| Minutes status chip | Draft [Nháp] or Revision Requested; minutes lifecycle: Draft → Pending Signature → Revision Requested → Sealed. The Meeting itself stays Ended. |
| Revision & auto-save | Read-only, e.g. “Lần sửa 1 · tự lưu 10:42”: current revision number and time of the last automatic save. |
| Fill from meeting data [Điền từ dữ liệu cuộc họp] | Button; generates the draft from Meeting information, attendance (FE-06), the published summary (FE-10) and closed Votes with conclusions (BR-161). Asks for confirmation before replacing edited text. |
| Submit for signing [Gửi ký] | Primary button; enabled only when every condition in “Conditions for signing” is met and the minutes are Draft or Revision Requested. Starts a new signing cycle on the current revision (BR-166). |
| Editor toolbar | Buttons B, I, U, H1, H2, bulleted list, numbered list, Table [Bảng] for formatting the content. |
| Minutes content | Rich-text body: title “BIÊN BẢN CUỘC HỌP”, Meeting name/date/time, I. Attendance [Thành phần] (e.g. “Có mặt 4/5 (theo điểm danh) · Chủ trì … · Thư ký … · Vắng …”), II. Content [Nội dung] taken from the published summary, III. Vote conclusions [Kết luận biểu quyết] with tally and note per Vote. Editable only by the assigned Secretary while Draft or Revision Requested. |
| Signers [Người ký] | Panel listing designated signers with name, meeting role and “Required” [Bắt buộc] chip; the Chair and the Secretary are always required signers. |
| Conditions for signing [Điều kiện gửi ký] | Checklist with green/red dots: Chair and Secretary are signers; the Meeting Summary has been published; every Vote is closed and has a conclusion. A red item explains why Submit for signing is disabled. |

#### 12.2 Sign Minutes Dialog

The Sign Minutes screen is the Minutes tab of an Ended Meeting as seen by a designated signer (here the Chair) while the minutes are Pending Signature. It is opened from the signing notification, the Personal Dashboard or the Minutes tab. The signer reads the current revision and either signs it or requests a revision; a revision request voids all signatures and the Secretary must resubmit (UC-58, UC-62, UC-63, UC-65).

This screen allows the Chair, Secretary or Participant designated as a signer to:

- View Meeting Minutes: read the current revision of the minutes (UC-58).

- View Signature Status: see how many signers have signed and when (UC-63).

- Sign Meeting Minutes: click “Sign minutes” and confirm to sign the current revision (UC-62).

- Request Revision of Meeting Minutes: click “Request revision”, enter a mandatory reason and submit; every signature already given becomes void (UC-65).

Field Description

| Field Name | Description |
| --- | --- |
| Request revision [Yêu cầu sửa] | Red outline button, visible only to designated signers while the minutes are Pending Signature. Opens a reason input (required). On submit the minutes become Revision Requested, existing signatures on this revision are invalidated and the Secretary is notified (BR-166). |
| Sign minutes [Ký biên bản] | Primary button, visible only to a designated signer who has not yet signed the current revision; asks for confirmation, then records the signature with timestamp. |
| Minutes header | Read-only “Biên bản · lần sửa 2” with status chip Pending Signature [Chờ ký]. |
| Minutes content | Read-only document: title, Meeting name, date, time and location, Attendance [Thành phần], Content [Nội dung] (from the published summary), Vote conclusions [Kết luận biểu quyết] with tally. |
| Signatures [Chữ ký · n/m] | Panel with the number of signatures collected on the current revision, e.g. 1/2. |
| Signer rows | Avatar, name and meeting role; status Waiting [Chờ ký] or Signed [Đã ký] chip with signing time (dd/mm hh:mm). |
| Information box | Read-only, e.g. “Thư ký niêm phong khi đủ 2/2 chữ ký. Lần sửa 1 đã bị huỷ chữ ký do yêu cầu sửa của Chủ trì.”: explains that only the Secretary seals after all signatures and why earlier signatures were voided (BR-166). |
| Seal minutes [Niêm phong biên bản] | Button shown disabled to signers; only the assigned Secretary can seal, and only after all designated signers have signed (BR-166). |

#### 12.3 Signing Status & Finalization

The Signing Status & Finalization screen is the Minutes tab seen by the assigned Secretary after the minutes have been submitted for signing. It shows the signature of every designated signer on the current revision and a history grouped by revision, including revision requests and voided signatures. When all signers have signed, the Secretary seals the minutes (UC-63, UC-64, UC-61, UC-65).

This screen allows the Secretary to:

- View Signature Status & Audit Trail: view each signer’s status and signing time on the current revision and the signing history of all revisions (UC-63).

- Finalize Meeting Minutes: click “Seal minutes” and confirm, after all designated signers have signed, to lock the minutes as the official record (UC-64).

- Review Revision Requests: read who requested a revision, when and why, and resubmit the corrected minutes for signing from the Minutes Editor (UC-65, UC-61).

On the screen, s/he can also:

- View Minutes: click “View minutes” to read the signed revision (UC-58).

Field Description

| Field Name | Description |
| --- | --- |
| Signing status chip | Fully signed [Đã ký đủ] when all designated signers have signed, otherwise Pending Signature [Chờ ký]. |
| Revision summary | Read-only, e.g. “Lần sửa 2 (hiện tại) · 2/2 người ký đã ký”. |
| View minutes [Xem biên bản] | Button; opens the read-only minutes of the current revision. |
| Seal minutes [Niêm phong biên bản] | Primary button, Secretary only; enabled only when every designated signer has signed the current revision (BR-166). A confirmation dialog warns that sealing locks the minutes permanently. |
| Signer table | Columns Signer [Người ký] (avatar, name), Role [Vai trò] (Chair / Secretary / …), Revision n [Lần sửa n] (Signed [Đã ký] / Waiting [Chờ ký] chip), Time [Thời điểm] (dd/mm hh:mm). |
| Signing history [Lịch sử ký] | Read-only timeline grouped by revision, newest first. Current revision events (e.g. Chair signed, Secretary signed, Secretary resubmitted for signing); earlier revisions are labelled “signatures void” [chữ ký đã huỷ] and show events such as a revision request with its reason, the Secretary’s edit that voided all signatures, and the voided signatures (BR-166). History entries cannot be changed. |

#### 12.4 Finalized Meeting Minutes

The Finalized Meeting Minutes screen is the Minutes tab after the Secretary has sealed the minutes. The sealed minutes are the official record of the Meeting and can no longer be edited or resubmitted; the Meeting itself remains Ended. Every user allowed to view the Meeting’s minutes sees this read-only version (UC-58, UC-63, UC-64).

This screen allows the System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary and Participant (within their meeting scope) to:

- View Meeting Minutes: read the sealed minutes with the signature block of every signer (UC-58).

- View Signature Status & Audit Trail: click “View signing history” to open the signing history of all revisions (UC-63).

On the screen, s/he can also:

- Download PDF: click “Download PDF” to save the sealed minutes as a PDF file.

Field Description

| Field Name | Description |
| --- | --- |
| Status chip | Sealed [Đã niêm phong]. |
| Seal summary | Read-only, e.g. “Lần sửa 2 · niêm phong bởi Đỗ Hải Yến lúc 15/10/2026 14:40”: sealed revision, Secretary who sealed and seal time (UC-64). |
| View signing history [Xem lịch sử ký] | Button; opens the signing history timeline (as on Signing Status & Finalization). |
| Download PDF [Tải PDF] | Button; downloads the sealed revision as PDF. |
| Information banner | Read-only: “Biên bản đã niêm phong là hồ sơ chính thức của cuộc họp: không sửa, không gửi ký lại được.” – editing, revision requests and resubmission are no longer available (BR-166). |
| Minutes document | Read-only page: title “BIÊN BẢN CUỘC HỌP”, Meeting name, date and actual time, full content (attendance, content from the published summary, vote conclusions). |
| Signature blocks | One block per signer: meeting role (CHAIR [CHỦ TRÌ], SECRETARY [THƯ KÝ]), “Signed dd/mm hh:mm” [Đã ký …] and signer name. |

### 13. FE-13 – Email & In-App Notification

#### 13.1 Notification Panel

The Notification Panel opens from the bell at the bottom of the sidebar and is anchored next to it, over the current page. Every signed-in user uses it to read in-app notifications, mark them as read and open the related meeting (UC-66, UC-67a, UC-67b, UC-68).

This screen allows the Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary and Participant to:

- View Notifications: view personal notifications and the unread count; opening the panel does not mark anything as read (UC-66).

- Mark Notification as Read: mark a single unread notification as read (UC-67a).

- Mark All Notifications as Read: mark all of the user’s unread notifications as read, including those not displayed (UC-67b).

- Open Notification Target: click a notification to mark it read and open the linked meeting (UC-68).

Field Description

| Field Name | Description |
| --- | --- |
| Notification bell | Icon in the sidebar footer next to the user’s name, with a badge showing the unread count; updated in real time without reloading the page (BR-46, BR-48). |
| Panel header [Thông báo] | Title with “n new” chip [2 mới] = unread count. Opening the panel does not change any read status (BR-49). |
| Mark all as read [Đánh dấu tất cả đã đọc] | Link button; applies to all unread notifications of the user, including those not displayed (BR-47, BR-51); with no unread items the count simply stays 0. |
| Notification icon | Icon at the left of each item indicating the event type. |
| Notification title | Event name, e.g. Meeting published [Cuộc họp đã được công bố], Meeting approved [Cuộc họp đã được duyệt], Meeting rejected [Cuộc họp bị từ chối]; shown bold while unread. |
| Notification message | Detail text including the meeting name, e.g. “Bạn được mời tham dự cuộc họp ‘Họp Hội đồng quản trị quý IV’.” |
| Time | Relative time, e.g. “51 minutes ago” [51 phút trước], “3 hours ago” [3 giờ trước], “Yesterday” [Hôm qua]. |
| Unread indicator | Blue dot and tinted background on unread items; removed when the item is read. Marking an already-read item leaves the count unchanged (BR-50). |
| Notification item (click) | Marks the item as read if unread (BR-50, BR-53) and opens the linked meeting after re-checking the user’s access; protected content is not shown if access is denied (BR-29, BR-52). A failure to save the read status does not block opening an authorized meeting (BR-53). |
| Messages | MSG40 “Unable to load notifications. Please try again.”; MSG39 “The requested notification page is unavailable.” |

#### 13.2 Meeting Invitation Email

This email is sent asynchronously to internal participants and external Guests when a Meeting is published, or when a person is added to a Published or InProgress Meeting. Its buttons lead to the Invitation Response screen (6.4); a Guest first verifies the invited email with a one-time code on Guest Verification (1.3) (UC-29, UC-04).

This email allows the invited Chair, Secretary, Participant or Guest to:

- Read Invitation: view the meeting name, the recipient’s meeting role and the planned time, location and Chair.

- Respond to Meeting Invitation: choose Attend or Decline, which opens the Invitation Response screen (6.4) with that choice pre-selected (UC-29).

- View Details: open the meeting details after signing in or after Guest verification.

- Access Meeting via Guest Invitation: a Guest opens the meeting-specific link and verifies the email with a code before any access (UC-04).

Field Description

| Field Name | Description |
| --- | --- |
| Subject / heading [Thư mời họp] | “Meeting invitation: <meeting name>”, e.g. “Thư mời họp: Họp Hội đồng quản trị quý IV”, under the TrustCabinet logo. |
| Salutation | Recipient name and meeting role, e.g. “Kính gửi ông Ngô Đình Phúc, … với vai trò Khách mời” (Guest). |
| Meeting information | Box with the Meeting’s planned date and time range, location and Chair, e.g. “14/10/2026 · 13:00 – 15:00”, “Phòng họp A3, tầng 5”, “Chủ trì: Nguyễn Minh Châu”. |
| Attend [Tham dự] | Primary button link; opens the Invitation Response screen with Attend pre-selected. |
| Decline [Không tham dự] | Button link; opens the Invitation Response screen with Decline pre-selected. Internal participants and Guests may accept or decline and change their own response while responses are allowed (BR-105). |
| View details [Xem chi tiết] | Link; opens the meeting details (after sign-in or Guest verification). Access is limited to the invited Meeting (BR-120, BR-154). |
| Security note | “Link chỉ dùng cho địa chỉ email này. Khi mở, ông sẽ được yêu cầu nhập mã xác minh.” Guest links are meeting-specific, require email verification, expire after the configured period and stop working when the invitation is revoked (BR-152 – BR-157). |
| Delivery | Sent through a background queue; a delivery failure is retried and does not undo publishing or the roster change (BR-39, BR-40, BR-94). |

### 14. FE-14 – Personal Dashboard & Meeting Workspace

#### 14.1 Personal Dashboard

The Personal Dashboard [Tổng quan] is the landing page after sign-in, reached from Overview in the sidebar. It summarises the signed-in user’s own day: today’s Meetings and the tasks waiting for him/her, each with a shortcut to the screen where the task is done. Only items belonging to the user are shown (UC-69).

This screen allows the Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary and Participant to:

- View Personal Dashboard: view the greeting, today’s date, the summary counters, today’s Meetings and the list of waiting tasks (UC-69).

- Open Filtered List: click a counter tile to open the corresponding filtered list (UC-69).

- Open Waiting Task: click the action of a task (Sign, Vote, Respond, Open) to go directly to the screen where it is completed (UC-69).

- Join Meeting Room: click “Join meeting room” on an InProgress Meeting of today (UC-37).

On the screen, s/he can also:

- Open Calendar: click “Open calendar ›” to go to the Meeting Calendar (4.1) (UC-17).

Field Description

| Field Name | Description |
| --- | --- |
| Greeting | Read-only “Good morning, <first name>” [Chào buổi sáng, Thu Hà] with the current weekday and date (dd/mm/yyyy). |
| Pending invitations [Chờ tôi phản hồi lời mời] | Counter tile: number of the user’s invitations still Pending; click opens the filtered list. |
| Open votes [Biểu quyết đang mở] | Counter tile: open Votes in InProgress Meetings for which the user is eligible and has not voted yet. |
| Minutes to sign [Biên bản chờ tôi ký] | Counter tile: minutes Pending Signature on which the user is a designated signer and has not signed. |
| Meetings this week [Cuộc họp tuần này] | Counter tile: Meetings the user is involved in that are planned in the current week. |
| Today [Hôm nay] | Panel of today’s Meetings the user may view (BR-29): planned time range, Meeting title, mode/location and status chip (e.g. Pending Approval [Chờ duyệt], InProgress [Đang họp]). |
| Join meeting room [Vào phòng họp] | Primary button on a row whose Meeting is InProgress; opens the Meeting Lobby (8.1). Room entry follows the Meeting mode and the user’s access (UC-37). |
| Open calendar [Mở lịch ›] | Link; opens the Meeting Calendar – Calendar View. |
| Waiting tasks [Việc đang chờ bạn] | List of the user’s own pending actions, each with title, context and action button: Sign minutes [Ký biên bản] → Sign [Ký] (12.2); Vote [Bỏ phiếu] → Vote (11.3); Respond to invitation [Phản hồi lời mời] → Respond [Phản hồi] (6.4); Assign speakers [Gán người nói] → Open [Mở] (10.1). Only tasks of the signed-in user are listed. |
| Empty state | When a panel has nothing to show it displays a short “nothing waiting” text instead of the list. |

#### 14.2 Meeting Workspace

When a Meeting is InProgress, its General Information tab turns into the Meeting Workspace. It is designed mainly for people attending in the meeting room in person, without joining the online room: it shows the Agenda Item being discussed, its documents, the next item and any open Vote, and offers a private note pad. Online attendees can join the room from the same screen (UC-69, UC-26, UC-32c, UC-52a, UC-55, UC-37).

This screen allows the Chair, Secretary and Participant to:

- View Meeting Workspace: view the live status, actual start time and elapsed time of the Meeting (UC-69).

- View Current Agenda Item: view the item being discussed, its presenter, planned duration and actual start, and the next item (UC-26).

- Open Agenda Item Documents: click a document chip to open it in the Document Viewer (UC-32c).

- Cast Vote: see the open Vote and click “Vote” to cast a ballot (UC-52a, UC-55).

- Join Meeting Room: click “Join meeting room” to enter the online room (UC-37).

On the screen, s/he can also:

- Write Personal Notes: type private notes in “My notes”, visible only to the author.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: Meeting title, status InProgress [Đang họp], confidentiality chip (e.g. Confidential [Mật]), actual start time, mode and location (“Trực tiếp · Phòng họp A3, tầng 5”). |
| Live banner | Status chip InProgress with “Bắt đầu 13:02 · đã họp 45 phút” (actual start and elapsed time, updated live). |
| Join meeting room [Vào phòng họp] | Primary button; opens the Meeting Lobby (8.1). Available only for an Online or Hybrid Meeting the user is invited to (UC-37). |
| Current item [Mục đang thảo luận] | Read-only: order and title of the current Agenda Item set by the Chair, presenter, planned duration and actual start (“Phạm Thị Lan · 40 phút · bắt đầu 13:12”). |
| Item documents | Chips with file name (e.g. “Ke-hoach-kinh-doanh-2027.docx”); click opens the Document Viewer – Preview & Comments. |
| Next [Tiếp theo] | Read-only: order, title and presenter of the next Agenda Item. |
| Open vote card [Biểu quyết] | Shown only while a Vote is Open: chip Open [Đang mở] and the motion text. |
| Vote [Bỏ phiếu] | Primary button, shown only to users on the eligible list fixed when the Vote opened (current holders of the meeting roles the Chair selected, verified Guests included when Guest is selected) who have not voted yet; opens Cast Vote (11.3) (BR-160, BR-165, BR-190). |
| My notes [Ghi chú của tôi] | Multi-line text input, optional, saved automatically; hint “Chỉ mình bạn thấy.” – visible only to the author and not part of the minutes. |

### 15. FE-15 – Reporting & Statistics

#### 15.1 Reports & Statistics

The Reports & Statistics screen is reached from Reports [Báo cáo] in the sidebar. It shows key figures on Meetings for a selected period, a monthly chart and a breakdown by Meeting status, and exports the same report. The System Admin sees system-wide data; other users see only Meetings within their own scope (UC-70a, UC-70b).

This screen allows the System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary and Participant (within their own scope) to:

- View Reports & Statistics: view the key figures, the meetings-per-month chart and the status breakdown for the selected period (UC-70a).

- Change Report Period: pick a start and end date; all figures are recalculated (UC-70a).

- Export Report: click “Export Excel” or “Export PDF” to download the report for the current period (UC-70b).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Title “Reports & Statistics” [Báo cáo & thống kê] with the period covered, e.g. “Số liệu từ 01/05/2026 đến 14/10/2026”. |
| Period [01/05/2026 – 14/10/2026] | Date-range picker (dd/mm/yyyy – dd/mm/yyyy), required; the end date must not be before the start date. A Meeting is counted when its planned time overlaps the period. |
| Export Excel [Xuất Excel] | Button; downloads the report for the current period and scope as an Excel file (UC-70b). |
| Export PDF [Xuất PDF] | Button; downloads the same report as a PDF file (UC-70b). |
| Meetings [Cuộc họp] | KPI tile: number of Meetings in the period, e.g. 75. |
| Attendance rate [Tỉ lệ tham dự] | KPI tile: share of invitees recorded Present in attendance of Ended Meetings, e.g. 87%; based on attendance, not on invitation responses (BR-116). Unmarked invitees are not Absent; display an unmarked count so incomplete attendance is visible. |
| Average duration [Thời lượng trung bình] | KPI tile: average actual duration (actual end − actual start) of Ended Meetings, e.g. “1 giờ 48”. |
| Minutes sealed within 3 days [Biên bản niêm phong trong 3 ngày] | KPI tile: share of Ended Meetings whose minutes were sealed within 3 days of the Meeting end, e.g. 81%. |
| Meetings per month [Số cuộc họp theo tháng] | Bar chart, one bar per month of the period (T5…T10) with the value above each bar; the current, incomplete month is drawn lighter with the note “Tháng 10 tính tới ngày 14.” |
| By status [Theo trạng thái] | Table with columns Status [Trạng thái] (chip, e.g. Ended, Published, Pending Approval, Draft), Count [Số] and % of all Meetings in the period. |
| Scope and errors | Figures and exports include only Meetings the user may access; System Admin sees all. If statistics cannot be loaded or the export fails, an error message with a retry option is shown and no file is produced. |

### 16. Background Job Inventory

#### 16.1 Process Notifications (JOB-01)

Runs inside the backend every 5 seconds. Each cycle creates the “meeting starting soon” reminder, once per recipient, for Meetings that start within the next 15 minutes, then pushes up to 100 pending in-app notifications to their recipients over the notification hub and marks them as pushed; deactivated users receive no push. A missed push is not lost, because the notification list is read from the database (UC-66). A failed cycle is logged and retried on the next tick.

#### 16.2 Deliver Meeting Invitation Emails (JOB-02)

Runs inside the backend only when Email:Smtp:Enabled is true. It queues the invitation emails of Published Meetings that have not been queued yet, then sends queued emails one at a time from the email outbox, and waits Email:PollingIntervalSeconds before polling again when the queue is empty. A failed email is retried with exponential back-off up to Email:MaxAttempts times (8 by default) and then marked permanently failed (API-03, 17.3).

### 17. External API Inventory

#### 17.1 Google Identity Services – Sign in with Google (API-01)

| URL | Browser loads https://accounts.google.com/gsi/client (Google Identity Services JavaScript SDK) |
| --- | --- |
| Purpose | Lets a user prove their Google identity in a Google popup so the SPA receives a signed Google ID token. |
| Caller | TrustCabinet frontend (GoogleSignInButton) → Google Identity Services; the backend is not involved at this step. |
| Auth | Public OAuth 2.0 Client ID only (google.accounts.id.initialize); no client secret is used by this flow. |
| Request / Response | In: client_id. Out: callback with response.credential = Google ID token (JWT); a cancelled popup or Google error yields no credential and the login page shows MSG03 itself. |
| Failure handling | If the GIS script fails to load or no credential is returned, the SPA shows 'Đăng nhập bằng Google không thành công' (MSG03) without calling the backend. |
| Configuration | Client ID constant in frontend GoogleSignInButton.tsx and Authentication:Google:ClientId in appsettings.json (must match). |

#### 17.2 POST: /api/auth/google – Google ID token verification (API-02)

| URL | POST: /api/auth/google (inbound from SPA); outbound: Google's published OAuth 2.0 signing certificates, fetched and cached by Google.Apis.Auth GoogleJsonWebSignature.ValidateAsync |
| --- | --- |
| Purpose | Signs a user in with a verified Google account and issues TrustCabinet's own access and refresh tokens. |
| Caller | SPA → TrustCabinet backend (AuthEndpoints); backend GoogleTokenVerifier → Google Identity Platform. |
| Auth | Anonymous endpoint; the Google ID token in the body is the credential. Token must be signed by Google, audience = configured Client ID, not expired, email_verified = true, non-empty sub. |
| Request / Response | Request: { idToken }. 200: access token + user in body, refresh token in HttpOnly/Secure/SameSite=Strict cookie tc_refresh_token (Path=/api/auth). 401 + MSG03 (invalid token), MSG05 (Google account not linked/authorised), MSG02 (account deactivated). 404 when Google sign-in is disabled. |
| Failure handling | Invalid/malformed tokens collapse to MSG03 and are logged without the token. First sign-in auto-links the account by verified email (ADR-0009) and writes an audit entry. Network failure fetching Google certificates is not caught (surfaces as HTTP 500). |
| Configuration | Authentication:Google:Enabled, Authentication:Google:ClientId (validated on startup when enabled). |

#### 17.3 SMTP mail server – outbound email (API-03)

| URL | smtp://{Email:Smtp:Host}:{Email:Smtp:Port} (default port 587; provider-neutral, e.g. smtp.gmail.com in setup notes) |
| --- | --- |
| Purpose | Delivers meeting invitation emails with an .ics calendar invite and password-reset link emails. |
| Caller | TrustCabinet backend (EmailDeliveryWorker/EmailQueueProcessor and PasswordResetEmailSender via SmtpEmailSender, MailKit) → SMTP server. |
| Auth | SMTP username/password (e.g. Gmail App Password) over STARTTLS (default), SSL-on-connect or none (internal relay only); credentials kept in user-secrets/environment variables. |
| Request / Response | MIME message: From (FromName/FromAddress), To, Subject, plain-text body; invitations add text/calendar METHOD:REQUEST attachment 'thu-moi-hop.ics'. Reset links are {ApplicationBaseUrl}/reset-password#token=… (token in URL fragment). |
| Failure handling | Invitations go through a database outbox (email_messages) with leasing (120 s), per-attempt timeout 60 s, SMTP client timeout 30 s, up to 8 attempts with exponential back-off from 60 s; then marked permanently failed. Failed QUIT after acceptance is treated as sent. Password-reset emails are sent synchronously with the same timeout; failure returns false (MSG15 to the admin). Worker only runs when Email:Smtp:Enabled = true. |
| Configuration | Email:ApplicationBaseUrl, Email:MaxAttempts, Email:RetryDelaySeconds, Email:PollingIntervalSeconds, Email:ProcessingLeaseSeconds, Email:SendAttemptTimeoutSeconds, Email:Smtp:{Enabled, Host, Port, Security, Username, Password, FromAddress, FromName, TimeoutSeconds}. |

#### 17.4 LiveKit server – RoomService API and access tokens (API-04) – planned

| URL | Backend → {LiveKit:ApiUrl} (http(s)://…:7880, RoomService: ListParticipants, MutePublishedTrack, RemoveParticipant, DeleteRoom); browser → {LiveKit:PublicUrl} (ws(s)://…:7880/rtc). Exposed via planned GET /api/meetings/{id}/call, POST /api/meetings/{id}/call/join, POST /api/meetings/{id}/call/participants/{userId}/mute. |
| --- | --- |
| Purpose | Runs the online meeting room on a self-hosted LiveKit SFU while the backend decides who may join and enforces chair controls. |
| Caller | TrustCabinet backend (planned LiveKitGateway) → LiveKit server; browser (livekit-client) → LiveKit server with a backend-issued token. |
| Auth | LiveKit API key/secret; backend signs HS256 JWT access tokens (identity = userId, room = meeting-{meetingId}, ~10-minute lifetime); secret only in user-secrets/environment. |
| Failure handling | Planned: backend still starts without LiveKit config, call endpoints return 503; LiveKit errors/timeouts (HTTP client timeout 5 s) return 502; a 30-second CallRoomJanitor reconciles room state. |
| Configuration | LiveKit:{PublicUrl, ApiUrl, ApiKey, ApiSecret, TokenTtlMinutes, EnforceRoster} (planned; docker compose profile 'calls'). |
| Status | Accepted design, planned (ADR-0010, docs/02-design/08-ke-hoach-phong-hop-truc-tuyen.md). |

#### 17.5 POST: /api/livekit/webhook – LiveKit webhook (API-05) – planned

| URL | POST: /api/livekit/webhook |
| --- | --- |
| Purpose | Receives room events from LiveKit so the system can record who joined/left the online meeting and close finished rooms. |
| Caller | LiveKit server → TrustCabinet backend (planned CallEndpoints). |
| Auth | Anonymous route verified by signature: Authorization header carries a raw HS256 JWT (iss = API key) signed with the LiveKit API secret, containing a sha256 claim of the request body. |
| Request / Response | Events: participant_joined, participant_left, participant_connection_aborted, room_finished, track_published (SCREEN_SHARE). 200 OK; 401 bad signature; 503 not configured; 500 on database error so LiveKit retries. |
| Failure handling | Handlers are idempotent; timestamps taken from the payload; lost webhooks are compensated by the 30-second janitor reconciliation. |
| Status | Accepted design, planned (ADR-0010). |

#### 17.6 AI / Speech Processing Provider – transcription, diarization and summary (API-06) – planned

| URL | To be decided: either a self-hosted model service or an external AI/ASR API; no endpoint is configured in the code yet. |
| --- | --- |
| Purpose | Turns meeting audio uploaded by the Secretary into a speaker-attributed transcript and a draft meeting summary. |
| Caller | TrustCabinet backend background processing (planned) → AI / Speech Processing Provider. |
| Auth | To be decided with the provider (e.g. API key or internal network access). |
| Request / Response | Input: uploaded audio of a Recording (language default 'vi'). Output: Transcript with timed TranscriptSegments per speaker, and a MeetingSummary draft; the provider name (Transcript.Provider) and model (MeetingSummary.Model) are stored per result. |
| Failure handling | Transcript has status Pending/Processing/Completed/Failed, ProgressPercent and ErrorMessage so a failure is shown and can be re-run; the AI never writes formal minutes and summaries stay marked unreviewed until the Secretary reviews them. |
| Status | Planned (FE-10, UC-62/64/90); hosting decision open — voice data leaves the system only if an external service is chosen, which is why the provider is recorded. |

## IV. Non-Functional Requirements

### 1. External Interfaces

TrustCabinet is a self-hosted web application installed for a single organisation (TrustSoft) on its own infrastructure. Users interact with it only through a web browser; the server communicates with a small number of backing services and external systems. This section states what each interface must provide so that the system communicates correctly with its users and with the hardware and software around it. Detailed protocols and APIs are given in Section I.5.2 (External API Inventory) and in Report 4 (SDS).

- User interface: A single-page web application opened in a standard web browser; no client installation, plug-in or browser extension is required. All screens, labels, system messages (catalogued in Section V.2 with MSGnn codes), notifications and emails are in Vietnamese in v1. Dates are shown as dd/MM/yyyy and times in 24-hour format in Vietnam time (UTC+7). The interface is designed desktop-first (laptop and desktop screens of 1280 px width or more) and remains usable at 1024 px; the external-Guest invitation and email-verification screens, invitation response, voting, notifications and document viewing are also usable on mobile browsers (360 px width or more). Native mobile apps are out of scope (Report 1, LI-10).

- Hardware interface: The system requires no special-purpose hardware. Clients: any PC, laptop, tablet or phone able to run a supported browser; joining the online meeting room additionally requires a microphone and speakers/headset, and optionally a camera, granted to the browser by the user. The system never activates a user's microphone or camera on its own and never records the meeting (Report 1, LI-01). Server: a single x86-64 host (physical or virtual) running Linux inside TrustSoft's network; recommended baseline 8 vCPU, 16 GB RAM and 500 GB of storage for documents and meeting audio, excluding speech/AI processing. If the speech-processing model is self-hosted, additional compute (possibly a GPU) is sized once the processing environment is decided (Report 2, risk R03).

- Software interfaces: (1) PostgreSQL 17 - the system's single relational data store for all business data, audit log and outbox queues. (2) Redis - short-lived security data (refresh-session and password-reset credentials) with automatic expiry. (3) SMTP mail server (any standards-compliant provider configured by TrustSoft) - outgoing invitation (with .ics calendar attachment), reminder, approval, signing and password-reset emails. (4) Google Identity Services / Google OpenID Connect - optional 'Sign in with Google' for internal users whose Google email is linked to an existing account; email/password sign-in must remain available when Google is unreachable. (5) Self-hosted media server (LiveKit SFU, planned) - audio, video and screen-sharing for the online meeting room. (6) Speech-processing and summarisation provider - converts uploaded meeting audio into a speaker-attributed Vietnamese transcript and a draft summary. Whether this runs self-hosted or as an external API is not yet decided; the provider must be reachable through one internal interface so it can be replaced without changing the rest of the system, and the provider/model used is recorded with every result. No other third-party system (HRM, ERP, calendar, chat) is integrated in v1 (Report 1, LI-07).

- Communication interfaces: All browser-server traffic uses HTTPS (TLS 1.2 or later) on a single origin; plain HTTP is redirected to HTTPS or refused. Data is exchanged as JSON (UTF-8) over a REST-style API. Real-time in-app notifications, agenda and vote updates use a WebSocket channel secured with TLS (WSS) and fall back to automatic reconnection when the connection drops. Online meeting media is transmitted with WebRTC and is always encrypted in transit (DTLS-SRTP); a relay (TURN) path over TLS is available for clients behind restrictive firewalls. Outgoing email is sent over SMTP with TLS (implicit TLS or STARTTLS). Calls from the server to Google and to any external speech/AI provider use HTTPS only. A health-check endpoint is exposed for monitoring the running instance.

### 2. Quality Attributes

The quality attributes below are stated as measurable targets for a single-organisation, self-hosted deployment of TrustCabinet serving up to about 200 internal users, and will be verified in Report 5.3/5.4 (System Test) and Report 5.5 (Acceptance Test).

#### 2.1 Usability

| Requirement | Description |
| --- | --- |
| Learnability and training time | An internal Member with basic computer skills must be able to find an upcoming meeting, respond to an invitation, open a meeting document, cast a vote and sign minutes after a guided introduction of no more than 30 minutes. A Meeting Organizer, Secretary, Chair or System Admin must be able to complete their full role workflow (e.g. create, submit and publish a meeting; manage sessions, agenda and participants; upload audio and approve the summary; seal minutes) after no more than 2 hours of training. An external Guest must be able to verify their email and respond to an invitation without any training. |
| Vietnamese user interface | All user-facing text - menus, labels, buttons, field hints, validation and system messages, notifications, emails and exported reports - is in Vietnamese with correct diacritics (UTF-8). Searches must handle Vietnamese text with diacritics correctly. Terminology must be consistent with the glossary of this SRS (e.g. the same Vietnamese term for Meeting, Agenda Item, Secretary throughout). |
| Consistency | All screens share the same layout (navigation menu, header with notification bell and user menu, content area), the same placement of primary and secondary actions, the same date/time format, and the same visual badge for each lifecycle status (meeting, session, agenda item, vote, minutes). Lists use the same search, filter, sort and pagination controls (BR-11, BR-31, BR-32), and empty results show a no-results message rather than an error (BR-12). |
| Responsive, desktop-first layout | All features are fully usable on desktop/laptop screens of 1280 px width or more and remain usable at 1024 px. The external-Guest invitation and email-verification screens, Respond to Meeting Invitation, voting, notification list and document viewing must be usable on mobile browsers of 360 px width or more without horizontal scrolling or loss of function. |
| Browser support | The application must work correctly on the two most recent stable versions of Google Chrome, Microsoft Edge, Mozilla Firefox and Safari (desktop), and on Chrome for Android and Safari on iOS for the mobile-capable screens. The online meeting room requires a WebRTC-capable browser; when the browser or device permissions are not suitable, the system tells the user what is missing instead of failing silently. |
| Accessibility basics | Screens should follow the basic WCAG 2.1 Level AA practices: every form field has a visible label, all functions are reachable with the keyboard with a visible focus indicator, text has a contrast ratio of at least 4.5:1, status and errors are not conveyed by colour alone, and pages remain usable when the browser zoom is set to 200%. |
| Clear error messages with codes | Every error or warning shown to a user uses the wording and code defined in the System Messages catalogue (Section V.2, MSGnn), explains in plain Vietnamese what happened and what the user can do next, is displayed next to the affected field when it concerns a field, and never shows stack traces or technical internals. Data already entered in a form is preserved after a validation error. |
| Confirmation of destructive and irreversible actions | Before any action that deletes, cancels, revokes or cannot be undone - e.g. deleting an agenda item or document, cancelling a session, removing a participant or revoking a Guest, deactivating an account, rejecting a meeting, closing a vote, signing or sealing minutes - the system asks the user to confirm in a dialog that names the affected object and states the consequence. Actions that cannot be reversed must say so explicitly. |
| Feedback on long-running operations | Any operation that takes longer than 1 second shows a loading indicator, and the user cannot submit the same form twice. File uploads show progress, and audio processing (transcription and summary generation) shows its current status (e.g. queued, processing, completed, failed) so that the Secretary does not need to wait on the page. |

#### 2.2 Performance

| Requirement | Target | Condition |
| --- | --- | --- |
| Page load time | Initial load of the application ≤ 3 s; navigation between screens and display of a screen's data ≤ 2 s for 95% of requests | Internal network ≥ 10 Mbps, up to 100 users active simultaneously |
| Server response time | 95% of API requests (view, create, update, approve, respond, vote) complete within 1 s; no ordinary form submission takes longer than 3 s | Up to 100 users active simultaneously, excluding file transfer time and AI processing |
| Search and list views | Paginated meeting, document, user, notification and audit-log lists, including search, filter and sort, return the first page within 2 s for 95% of requests | Data volume up to 10,000 meetings, 50,000 documents and 1,000,000 audit-log entries |
| Document upload and preview | Documents up to 50 MB per file are accepted; after the transfer completes the file is stored and listed within 5 s; in-browser preview of a document up to 10 MB opens within 3 s | Up to 10 simultaneous uploads; transfer time depends on the user's network |
| Meeting audio upload and AI processing | Audio files up to 500 MB and up to 4 hours long are accepted. The upload is acknowledged and the file queued within 5 s after transfer. The speaker-attributed transcript is ready within 1.5 x the audio duration (e.g. a 60-minute recording within 90 minutes) after processing starts, and a summary is generated within 5 minutes of the Secretary's request | One recording processed at a time per processing worker; targets apply whichever processing environment (self-hosted or external) is selected; queued jobs wait their turn without blocking other functions |
| Notification and real-time update latency | In-app notifications and the unread count reach a connected recipient within 10 s of the triggering event; current-agenda and vote-status changes are visible to other users in the same session within 2 s (BR-73); an email is handed to the SMTP server within 2 minutes; 'starting soon' reminders are sent 15 minutes (± 1 minute) before a session starts | Normal operation, mail server reachable; email retries do not delay in-app delivery |
| Concurrent users | The system remains fully functional with 200 signed-in users, of whom up to 100 perform operations at the same time, without errors or time-outs and within the response-time targets above | Single-server Docker deployment with the recommended baseline hardware |
| Online meeting room capacity | Each meeting room supports up to 30 participants with audio and video plus one screen share, with a hard limit of 50 connected users; joining the room takes ≤ 5 s; mouth-to-ear audio delay ≤ 400 ms; at least 3 rooms can run at the same time | Participants inside the company network or on a stable connection of ≥ 2 Mbps per participant; self-hosted media server on the deployment host |
| Report generation and export | A statistics report covering up to 12 months of data is displayed within 5 s and exported to a file within 10 s | Data volume as in 'Search and list views'; up to 5 users generating reports at the same time |

#### 2.3 Security

| Requirement | Description |
| --- | --- |
| Authentication required | Every page and API function requires a signed-in internal user, except the sign-in page, the password-reset page reached from a valid reset link, the external-Guest invitation and email-verification pages, and the health check. Sign-in with Google is accepted only after Google's identity assertion has been verified and only for an existing, linked internal account; it never creates an account (BR-05, BR-06, BR-07). Sign-in failure messages must not reveal whether an account or email exists (BR-02). |
| Password policy and protection | Passwords must be at least 12 characters long; no mandatory character-class rules are imposed, and long passphrases must be accepted. Passwords are never stored or logged in a readable or reversible form and are never displayed, including to administrators (BR-01, BR-10, BR-21). A password-reset link is valid for at most 60 minutes and can be used only once (BR-20). |
| Account lockout | After 5 consecutive failed password sign-in attempts the account is placed in Locked status for 15 minutes, after which it is unlocked automatically; a System Admin can unlock it earlier (UC-55). Lockout and unlock events are recorded in the audit log, and the message shown does not reveal the failure count. Repeated failed attempts against guest-verification codes and password-reset requests are limited in the same way. |
| Session and token expiry | Short-lived access credentials expire after at most 15 minutes and are renewed silently while the user remains signed in; after at most 7 days the user must sign in again. Signing out immediately ends the session (BR-09). Deactivating an account terminates all its active sessions (BR-18), and changes to a user's roles or status take effect on the user's next request. Session credentials must not be readable by page scripts and must not be sent to other sites. |
| Role-based access control and least privilege | Every request is authorised on the server against the user's current system-role permissions and their relationship with the meeting (Organizer, Approver, Chair, Secretary, Member or Guest) (BR-29, BR-117, BR-119); anything not explicitly permitted is denied. Hiding a button in the user interface is never the only protection. A user who is not authorised for a meeting is told it is not found, so its existence is not disclosed. Only the System Admin can change role-permission configuration, and every change is audited (BR-16, BR-23). |
| Document security levels | Every document carries one security level: Normal, Internal, Restricted or Secret. Access to view, preview, download, comment on or attach a document is granted only when both the user's relationship with the document (owner or authorised meeting relationship) and the document's level permit it; documents a user may not access never appear in their search results, lists, notifications or emails. Viewing or downloading a Restricted or Secret document is recorded in the audit log, and Secret content is never included in email bodies or attachments. |
| External Guest access scope | A Guest requires a secure current Meeting invitation and invited-email verification by a one-time code expiring within 10 minutes. After providing self-declared display information, the Guest uses Member participation rights only in this Meeting, including all attached documents, comments, voting and room controls. Access expires and revocation takes effect on each protected request. No internal account, Chair/Secretary role, delegated authority, room moderation or personal library is granted. (BR-150, BR-152–BR-157, BR-180, BR-181) |
| Encryption of data in transit | All communication between browsers and the server, between the server and external services (Google, SMTP, any external speech/AI provider) and all meeting media must be encrypted in transit (TLS 1.2 or later; encrypted WebRTC media). The system must not be served over unencrypted HTTP in production. |
| Protection of stored data and backups | Uploaded documents and meeting audio are stored only on TrustSoft's own infrastructure and are reachable only through the application's access checks, never through a public or guessable URL. Secrets (signing keys, database, Redis and SMTP credentials) are kept out of the source code and configuration repository. The database and file storage are backed up at least daily; backups are stored separately from the production host with access restricted to authorised administrators. |
| Audit logging | The system records security and governance events - sign-in success/failure, lockout and unlock, password reset, Google account linking, user and role changes, meeting lifecycle transitions, access to Restricted/Secret documents, audio upload and deletion, summary approval, vote closing, minutes signing and sealing - with actor, time, action, target object and source IP address (BR-16, BR-38). Audit entries cannot be modified or deleted through the application; only the System Admin can view and search them (UC-92). Passwords, tokens and verification codes are never written to any log. |
| Personal data protection and audio consent (Decree 13/2023/ND-CP) | Personal data (account details, Guest name and email, voices in meeting audio, attendance) is processed only for the stated meeting-management purpose. A meeting audio file can be uploaded only after the Secretary confirms that participants were informed and consented and states the purpose; each recording has a retention date, after which (or on a justified request) it is erased and the erasure is recorded. The online meeting room stores only join/leave times, never media or chat content. If an external speech/AI provider is selected, audio and transcripts are sent only to that provider, only for processing, and must not be retained or used by it for other purposes. |
| Integrity of signed minutes, vote conclusions and stored documents | Once a Vote Conclusion is recorded or minutes are sealed, their content cannot be changed by anyone, including administrators; any change to minutes before sealing invalidates signatures collected for the previous content version. Each signature is bound to the exact content version signed so that later tampering is detectable, and each stored document version can be verified against the fingerprint taken at upload. Because v1 uses internal electronic signatures without a certification authority (Report 1, LI-06), the user interface must state that the signature has no legal weight outside TrustCabinet. |
| Upload safety | Uploaded files are accepted only within the allowed size limits and file types; the file type is determined from the file content, not from its name. Files are never executed by the server, and audio uploads accept audio formats only (no video, Report 1, LI-03). |

#### 2.4 Reliability

| Requirement | Description |
| --- | --- |
| No loss of committed data | A business operation (e.g. approving a meeting, recording a vote, signing minutes) is either completed in full or not at all; a failure part-way never leaves partially saved data. After an unplanned restart no confirmed transaction is lost. |
| Reliable asynchronous delivery | Invitation and notification emails are queued and retried automatically (up to 8 attempts with increasing intervals) until delivered or marked failed; a failure to deliver never reverts the meeting state that triggered it (BR-39, BR-40, BR-94). Delivery status is traceable (BR-43). |
| Recoverable AI processing | If transcription or summary generation fails or the provider is unavailable, the uploaded audio is kept, the job is marked failed with a reason, and the Secretary can retry without re-uploading. AI output is always a draft until reviewed and approved by the Secretary. |
| Defect target | No Critical defect may remain open at acceptance (Report 2 quality objectives); system-level defects are tracked in Reports 5.3-5.5. |

#### 2.5 Availability

| Requirement | Description |
| --- | --- |
| Service availability | The system is available at least 99% of the time during TrustSoft working hours (Monday-Friday, 08:00-18:00, UTC+7), excluding planned maintenance announced to users at least 24 hours in advance and performed outside working hours where possible. |
| Recovery time and data loss | After a server failure the service can be restored from backup within 4 hours (RTO), losing at most the last 24 hours of data (RPO). |
| Graceful degradation | If Google sign-in, the mail server, the media server or the speech/AI provider is unavailable, the rest of the system keeps working: users can still sign in with email and password, emails are queued for later delivery, and meeting, document, voting and minutes functions remain usable. |
| Health monitoring | The running instance exposes a health check reporting status and version so that operators can detect an outage within 5 minutes. |

#### 2.6 Maintainability

| Requirement | Description |
| --- | --- |
| Layered, replaceable design | The code is organised in separate presentation, application, domain and infrastructure layers; external services (email, Google identity, media server, speech/AI provider) sit behind internal interfaces so that one can be replaced without changing business logic (Report 2, R03 mitigation). |
| Code quality and test coverage | Unit-test coverage of core backend business logic is at least 70% (Report 2); every change passes the automated CI pipeline (build with warnings treated as errors, linting, type checking and tests) before it is merged to the main branch. |
| Configurability without code change | Operational values - token lifetimes, lockout threshold and duration, Guest access validity, upload size limits, email retry policy, reminder lead time, audio retention period - are configurable through settings or environment variables, with safe defaults validated at start-up. |
| Schema evolution and documentation | Database changes are applied through versioned migrations; APIs, message codes and architectural decisions are documented in the repository and kept consistent with Reports 3 and 4. |

#### 2.7 Portability / Deployability

| Requirement | Description |
| --- | --- |
| Self-hosted containerised deployment | The complete system (application, PostgreSQL, Redis and media server) can be deployed on a single Linux host inside TrustSoft's network using Docker Compose, without any paid or cloud-only service (Report 2, constraints 2 and 5). |
| Single deployable application | The web front end and the API are delivered as one application served from the same origin; a new environment can be installed from the documented procedure within 1 working day by a person with basic Linux and Docker knowledge. |
| Environment independence | All environment-specific values (URLs, credentials, SMTP and Google settings, speech/AI provider endpoint) are supplied by configuration, so the same build runs in development, test and production. |

#### 2.8 Compliance

| Requirement | Description |
| --- | --- |
| Personal data protection | Processing of personal data, including voices in uploaded meeting audio, complies with Decree 13/2023/ND-CP on personal data protection: stated purpose, recorded consent, defined retention and recorded erasure. |
| Electronic signatures and records | Minutes signing in v1 is an internal electronic-signature workflow with a complete audit trail; it does not claim the legal status of a CA-issued digital signature (Report 1, LI-06). Sealed minutes, vote conclusions and lifecycle histories are retained as immutable records. |
| Licensing | All third-party libraries, tools and services used must be free, open-source or student-licensed, with licences compatible with internal deployment at TrustSoft (Report 2, constraint 2). |

## V. Requirement Appendix

### 1. Business Rules

| BR-ID | Rule Statement | Applies To | Rationale | Enforced By |
| --- | --- | --- | --- | --- |
| BR-01 | Passwords must never be stored in plain text. Password-based credentials must be protected using a secure salted password-hashing mechanism. | UC-01, UC-07 |  |  |
| BR-02 | Authentication failure messages must not reveal whether a specific username or email exists in the system. | UC-01 |  |  |
| BR-03 | A deactivated user account must not be allowed to sign in or access authenticated system functions. | UC-01, UC-02, UC-06c, UC-07 |  |  |
| BR-04 | A user without active system access or the required active role/permission must not be allowed to access the system. | UC-01, UC-02 |  |  |
| BR-05 | Identity information returned by Google must be successfully verified before it can be used for authentication. | UC-02 |  |  |
| BR-06 | A Google-authenticated email may be used to sign in only when it is linked to an existing internal user account. | UC-02 |  |  |
| BR-07 | Signing in with Google must not automatically create a new internal user account. | UC-02 |  |  |
| BR-08 | User permissions are determined by the system and must not be selected manually during the sign-in process. | UC-01, UC-02 |  |  |
| BR-09 | After a successful sign-out, the current authentication state must no longer grant access to protected system functions. The user must authenticate again before protected functions can be accessed. | UC-03 |  |  |
| BR-10 | Sensitive authentication information, including passwords, password hashes, reset credentials, and authentication tokens, must not be displayed in User Management. | UC-07 |  |  |
| BR-11 | List views must support pagination when the number of records exceeds the configured page size. | UC-05, UC-17, UC-66 |  |  |
| BR-12 | When a search or filter returns no matching records, the system must display an appropriate no-results message instead of treating the result as a system error. | UC-05, UC-17 |  |  |
| BR-13 | Username and email must each be unique across user accounts. | UC-06a |  |  |
| BR-14 | All mandatory user-account information must be provided and valid before an account can be created or updated. | UC-06a, UC-06b |  |  |
| BR-15 | A newly created user account must have Active status by default. | UC-06a |  |  |
| BR-16 | Successful administrative changes to user accounts or role-permission configuration must be recorded in the audit log. | UC-06a, UC-06b, UC-06c, UC-07, UC-11, UC-12a, UC-12b, UC-12c |  |  |
| BR-17 | General user-account updates may modify Full Name, Job Title, and Contact Information. Email, System Role, and password credentials must be managed separately. | UC-06b |  |  |
| BR-18 | When a user account is deactivated, all active authenticated sessions for that account must be terminated. | UC-06c |  |  |
| BR-19 | Deactivating a user account must not delete historical records associated with that user. | UC-06c |  |  |
| BR-20 | A password-reset link must have a limited validity period and must not be reusable after a successful password reset. | UC-07 |  |  |
| BR-21 | An administrator must not be able to view a user's existing password. | UC-07 |  |  |
| BR-22 | Password reset may only be initiated for an account that is eligible to use password-based authentication. | UC-07 |  |  |
| BR-23 | A role-permission configuration may contain only permissions defined by the system and must satisfy the system's permission-configuration rules before it can be applied. | UC-11 |  |  |
| BR-24 | All required meeting information must be provided before a meeting can be created. Planned End Time must be after Planned Start Time. | UC-14 |  |  |
| BR-25 | Planned Start Time and Planned End Time are required meeting-level schedule fields; each Meeting represents one sitting. | UC-14, UC-15, UC-16, UC-17, UC-18, UC-20 |  |  |
| BR-26 | A newly created meeting must have Draft status. | UC-14 |  |  |
| BR-27 | Meeting information may be edited in Draft or Rejected; in Published, the Meeting Organizer may reschedule only times or location. | UC-15 |  |  |
| BR-28 | Updating meeting information must not implicitly change the meeting lifecycle status. | UC-15 |  |  |
| BR-29 | A user may access a meeting only when the user has an authorized relationship with that meeting as its Meeting Organizer, assigned Meeting Approver, Chair, Secretary, or Participant. Authentication alone does not grant access to a meeting. | UC-16, UC-17, UC-26, UC-27, UC-30, UC-32a, UC-37, UC-68 |  |  |
| BR-30 | Meeting time-range search matches when its own planned start/end interval overlaps the selected range. | UC-17 |  |  |
| BR-31 | When multiple search or filter criteria are applied, a record must satisfy all applicable criteria to appear in the result. | UC-05, UC-17 |  |  |
| BR-32 | The meeting list must support sorting, and the current search, filter, and sorting criteria must be preserved when navigating between pages. | UC-17 |  |  |
| BR-33 | Meeting approval submission requires all mandatory information including the planned schedule and Draft or Rejected status. | UC-18 |  |  |
| BR-34 | Submitting a Draft or Rejected Meeting for approval changes its status to WaitingApproval. General Meeting information cannot be edited until an approval decision is made. This restriction does not apply to Agenda Management: the assigned Secretary may create, update, delete and reorder Agenda Items under BR-65, BR-68 and BR-74. | UC-15, UC-18, UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| BR-35 | For each approval cycle, an eligible Meeting Approver must be assigned. If no eligible approver is available, submission cannot proceed: the Meeting remains Draft or Rejected, its existing assignment and history are unchanged, and no submission notification is issued. Only the currently assigned Meeting Approver with the required permission may approve or reject the Meeting. | UC-18, UC-19 |  |  |
| BR-36 | Approving a meeting changes its status from WaitingApproval to Approved. Rejecting a meeting changes its status from WaitingApproval to Rejected. A rejection reason is mandatory. | UC-19 |  |  |
| BR-37 | Only an authorized Meeting Organizer may publish a meeting that is in Approved status and satisfies the publication readiness requirements. Publishing changes the meeting status from Approved to Published and initiates participant invitations. | UC-20 |  |  |
| BR-38 | Every meeting lifecycle transition must have a history record containing at least the acting user, timestamp, action and resulting status; a rejection also includes its reason. For submission, approval, rejection and publication, the corresponding assignment, decision, status and history must be completed together. If any part cannot be completed, all previous Meeting information and history remain unchanged, no new notification or invitation is issued, and the action is not reported as successful. Existing history must never be changed or removed. | UC-18, UC-19, UC-20, UC-21, UC-22 |  |  |
| BR-39 | After successful publication, current internal participants other than the user who published the Meeting must be notified in the application, and all current internal participants must be sent invitation emails. Each external Guest with a current invitation must receive a separate email invitation to verify their invited email address and access only that Meeting. Invitation delivery must not delay confirmation of publication. | UC-20 |  |  |
| BR-40 | If an invitation notification or email cannot be delivered, the system must retry delivery. The Meeting remains Published; delivery failure does not undo successful publication. | UC-20 |  |  |
| BR-41 | Meeting publication scope is strictly limited to meetings in Approved status and cannot be bypassed by direct state manipulation. | UC-20 |  |  |
| BR-42 | All participant invitation notifications must include a direct link to the published meeting details and attached materials. | UC-20, UC-28a |  |  |
| BR-43 | System logging must track the queuing and delivery status of all asynchronous invitation messages for auditability. | UC-20, UC-28a |  |  |
| BR-44 | Re-publishing or updating invitation delivery for an already published meeting requires explicit organizer action and creates a distinct lifecycle record. | UC-15, UC-20 |  |  |
| BR-45 | The system creates an in-app notification for the intended recipient when they are invited to a meeting, when a meeting they are involved in is approaching its start time. | UC-20, UC-66 |  |  |
| BR-46 | Notifications are saved for later viewing. If the recipient is connected, a new notification and the unread count are updated in real time without a page reload. | UC-66 |  |  |
| BR-47 | A user may retrieve, open, or change the read status of only notifications belonging to that user's account. | UC-67a, UC-67b, UC-68 |  |  |
| BR-48 | The notification bell shows how many of the current user's notifications are unread. | UC-66, UC-67b, UC-68 |  |  |
| BR-49 | Opening the notification list does not mark any notification as read. | UC-66 |  |  |
| BR-50 | Marking an already-read notification as read leaves its status unchanged and does not reduce the unread count again. | UC-68 |  |  |
| BR-51 | Mark All as Read applies to all notifications belonging to the current recipient that are unread when the request is processed, including notifications not displayed on the current page. | UC-67b |  |  |
| BR-52 | A meeting notification links to its corresponding target. Opening the link must check the recipient’s current access permission and must not reveal protected content when access is denied. | UC-68 |  |  |
| BR-53 | Selecting a notification to open its target attempts to mark the notification as read. Failure to save the read status does not block opening an authorized target. | UC-68 |  |  |
| BR-62 | A Meeting Planned End Time must be later than Planned Start Time. | UC-14, UC-15, UC-18 |  |  |
| BR-64 | Each Agenda Item belongs directly to one Meeting. Reordering is within that Meeting. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26 |  |  |
| BR-65 | Only the active assigned Secretary may create, update, delete or reorder Agenda Items while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Chair-only execution actions remain governed by BR-69. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| BR-66 | An Agenda Item requires a Title. Presenter and Description are optional; attached documents must belong to the Meeting. | UC-23a, UC-23b | PO 16/10/2026 — simplified Agenda (no planned time, no start order) |  |
| BR-67 | A new Agenda Item is always Pending and is appended after the last existing position, including while the Meeting is InProgress. Existing Item positions are not changed. | UC-23a | PO 16/10/2026 — simplified Agenda (no planned time, no start order) |  |
| BR-68 | The Title, Presenter and Description of an Agenda Item may be edited in any execution status while Agenda Management is permitted. Editing does not change its execution status, actual times, position or the Meeting lifecycle. | UC-23b | PO 16/10/2026 — simplified Agenda (no planned time, no start order) |  |
| BR-69 | Only the current Chair may set or complete the current Agenda Item while Meeting is InProgress. | UC-24, UC-25 |  |  |
| BR-70 | Any Pending Agenda Item may be started, whatever its position. Starting it changes Pending to InProgress; completing the current Item changes InProgress to Done. The Item status must be checked again when the change is saved. | UC-24, UC-25 | PO 16/10/2026 — simplified Agenda (no planned time, no start order) |  |
| BR-71 | At most one Agenda Item of a Meeting may be InProgress at a time. Concurrent requests must not start two Items; an unsuccessful request leaves the Agenda unchanged. | UC-24 |  |  |
| BR-72 | Starting and completing an Agenda Item record its actual start and end times. The Chair may complete the current Item at any time. Completion does not automatically start another Item or end the Meeting. | UC-24, UC-25 | PO 16/10/2026 — simplified Agenda (no planned time, no start order) |  |
| BR-73 | After an Agenda change is successfully saved, the complete Agenda is automatically refreshed for authorized users currently viewing the same Meeting. This applies to creation, editing, deletion, reordering, setting the current Item, completing an Item and completing the current Item when the Meeting ends. Refreshing must respect current Meeting access; an unsuccessful change must not be shown as a successful update. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26, UC-22 |  |  |
| BR-74 | Agenda Management is permitted in Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including while approval is pending and while the Meeting is running. It is locked in Ended and subsequent lifecycle states. The server rechecks status and Secretary assignment when saving. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| BR-75 | Rescheduling a Published Meeting requires a planned start time later than the current time. | UC-15 |  |  |
| BR-76 | Rescheduling a Published Meeting must notify all current invitees; delivery failures do not undo the change. | UC-15 |  |  |
| BR-78 | When a Meeting in Draft, Rejected or Approved has no current Secretary, the Meeting Organizer may assign exactly one active internal user as Secretary, including after an earlier Secretary was removed or changed role. No additional viewing permissions are granted by this recovery assignment; the Organizer gains no other roster-management authority. WaitingApproval remains read-only. (BR-79, BR-80, BR-122) An existing active Member may be assigned Secretary without creating a duplicate participant or resetting response/attendance. | UC-28a | PO 13/10/2026 — follow-up |  |
| BR-79 | Current assigned Secretaries manage the Meeting roster while its status permits. The Meeting Organizer may assign a Secretary only when no current Secretary exists in Draft, Rejected or Approved, whether initially or to restore the assignment. This exception does not permit other role, permission or removal actions. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 — follow-up |  |
| BR-80 | Participant management is allowed only while the Meeting is Draft, Rejected, Approved, Published, or InProgress. | UC-28a, UC-28b, UC-28c |  |  |
| BR-81 | An internal participant must reference an active internal user account. | UC-28a |  |  |
| BR-82 | An internal user may appear at most once as a current participant in the same Meeting. | UC-27, UC-28a |  |  |
| BR-83 | Internal users are selected individually, and one or more users may be added in one submission. Organisational-unit invitations are not supported. (BR-168) | UC-28a |  |  |
| BR-84 | External Guests are represented by meeting-specific Guest invitations rather than internal Meeting Participant records. | UC-27, UC-28a, UC-28c |  |  |
| BR-85 | Each current internal participant has exactly one Meeting Role: Chair, Secretary, or Member. | UC-28a, UC-28b |  |  |
| BR-86 | A participant's Meeting Role is independent of the user's system Role. | UC-28a, UC-28b |  |  |
| BR-87 | An external Guest cannot satisfy a required Chair or Secretary role. | UC-20, UC-21 |  |  |
| BR-88 | A newly added internal participant or external Guest invitation starts with Pending Invitation Response and no response time. | UC-28a |  |  |
| BR-90 | When an internal participant is added to a Published or InProgress Meeting, the participant is notified through the applicable invitation channels. | UC-28a |  |  |
| BR-91 | When an external Guest is invited to a Published or InProgress Meeting, the invitation is delivered by email. | UC-28a |  |  |
| BR-92 | Changing a meeting role or additional meeting permissions in a Published or InProgress Meeting notifies every affected participant. Replacing the Chair notifies both people. Notification failure does not cancel saved changes. (BR-94, BR-169) | UC-28b |  |  |
| BR-93 | Removing an internal participant or revoking a Guest invitation from a Published or InProgress Meeting notifies the affected invitee through the applicable channel. | UC-28c |  |  |
| BR-94 | Failure to deliver a participant or Guest notification does not undo an otherwise successful roster change. | UC-28a, UC-28b, UC-28c |  |  |
| BR-95 | A participant-removal or Guest-revocation notification must not contain a protected meeting-access link. | UC-28c |  |  |
| BR-96 | Changing a meeting role or additional meeting permissions does not change the participant’s invitation response or attendance. | UC-28b |  |  |
| BR-97 | Removing a current internal participant ends the access and additional permissions granted through that assignment. Independent valid access relationships remain effective. | UC-28c, UC-37 |  |  |
| BR-100 | Revoking an external Guest invitation revokes access granted by that Guest relationship. | UC-28c, UC-37 |  |  |
| BR-101 | After a roster or Guest-access relationship changes, room-entry eligibility must be re-evaluated; a connected user is disconnected only when no valid room-entry relationship remains. | UC-28c, UC-37 |  |  |
| BR-102 | Removing or revoking a roster entry after publication preserves its Invitation Response and Attendance evidence as historical data. | UC-27, UC-28c, UC-30 |  |  |
| BR-103 | Historical or revoked roster records are excluded from the current roster, current access checks, and required-role counts. | UC-27, UC-28c, UC-30 |  |  |
| BR-104 | Only the owner of a current invitation may respond to that invitation. | UC-29 |  |  |
| BR-105 | Current internal participants and external Guests may respond only Accepted or Declined. Responding does not transfer or change any Meeting Role. | UC-29 |  |  |
| BR-106 | Invitation Responses may be changed while the Meeting is Published or InProgress. | UC-29 |  |  |
| BR-115 | The latest Invitation Response and its response time are recorded for each current invitation. | UC-29, UC-30 |  |  |
| BR-116 | Invitation Response and Attendance are independent; Accepted does not mean the invitee attended the Meeting. | UC-29, UC-30, UC-31 |  |  |
| BR-117 | Roster and invitation-response information is accessible only to users whose relationship with the Meeting permits that information. | UC-16, UC-27, UC-29, UC-30 |  |  |
| BR-118 | An active assigned Secretary may read the Meeting before publication; WaitingApproval is read-only for roster management. | UC-16, UC-17, UC-27, UC-28a, UC-28b, UC-28c |  |  |
| BR-119 | A Chair or Member assignment alone does not grant meeting-read access before the Meeting is Published. | UC-16, UC-17 |  |  |
| BR-120 | An external Guest receives access only to the Meeting to which that Guest was invited. | UC-29, UC-37 |  |  |
| BR-121 | A user who is not authorized to access a Meeting must not receive its roster or invitation-response information. | UC-27, UC-30 |  |  |
| BR-122 | Draft, Rejected and Approved Meetings may temporarily have no current Secretary. In these statuses the Organizer can assign a Secretary when none remains under BR-78. Published and InProgress Meetings must retain at least one current Secretary, or replace the last Secretary together with the same successful roster change. WaitingApproval roster changes remain prohibited; publication and start still require active Secretary assignments under BR-125 and BR-127. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 — follow-up |  |
| BR-123 | A Meeting may have at most one current Chair at a time. | UC-28a, UC-28b |  |  |
| BR-124 | Publishing a Meeting requires exactly one current Chair whose internal account is active. | UC-20 |  |  |
| BR-125 | Publishing a Meeting requires at least one current Secretary whose internal account is active. | UC-20 |  |  |
| BR-126 | A Published or InProgress Meeting must retain its current Chair. From publication onward, the Chair role changes only when the current Chair hands it over through UC-73; the Secretary and the Meeting Organizer cannot replace the Chair. (BR-169) | UC-28b, UC-28c, UC-73 | PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair |  |
| BR-127 | Starting a Meeting requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-21 |  |  |
| BR-129 | Invitation responses do not change readiness requirements for the assigned active Chair and Secretary when publishing or starting a Meeting. | UC-20, UC-21 |  |  |
| BR-130 | Only an active current Chair assigned to the Meeting may explicitly start or end that Meeting. | UC-21, UC-22 |  |  |
| BR-131 | A Meeting may be started only while it is Published. | UC-21 |  |  |
| BR-132 | A Meeting may be ended only while it is InProgress. | UC-22 |  |  |
| BR-143 | When a Meeting becomes Ended, its Online Meeting Room stops accepting joins and disconnects currently connected users. | UC-22, UC-37 |  |  |
| BR-148 | An external Guest is invited using a name and email without an internal account. After email verification, the Guest provides a self-declared display name and optional job title for this Meeting. The invited name remains invitation evidence; only control of the email is verified. (BR-180) | UC-27, UC-28a, UC-29 | PO 13/10/2026 |  |
| BR-149 | Within one Meeting, a Guest email may have at most one current Guest invitation. | UC-28a |  |  |
| BR-150 | A Guest cannot hold an internal Meeting Role, including Chair or Secretary. | UC-28a, UC-37 | PO 14/10/2026 — atomic rule review |  |
| BR-151 | A Guest invitation must not be created when the same email is already represented by a current internal participant of that Meeting. | UC-28a |  |  |
| BR-152 | External Guest access requires a secure, meeting-specific invitation. | UC-20, UC-28a, UC-29, UC-37 |  |  |
| BR-153 | An external Guest must verify control of the invited email before Guest access is established. | UC-29, UC-37 |  |  |
| BR-154 | External Guest access is limited to the invited Meeting. | UC-29, UC-37 |  |  |
| BR-155 | External Guest access expires according to the configured validity period. | UC-29, UC-37 |  |  |
| BR-156 | External Guest access does not require an internal account login. | UC-28a, UC-29, UC-37 |  |  |
| BR-157 | Revoking an external Guest invitation immediately invalidates access granted by that invitation. | UC-28c, UC-29, UC-37 |  |  |
| BR-158 | Only the Meeting Organizer may reschedule a Published Meeting; only planned times or location may change. | UC-15 | PO 09/10/2026 | Meeting / Voting Management |
| BR-159 | Ending a Meeting neither requires nor automatically closes its Votes and preserves previously recorded ballots. | UC-22 | PO 13/10/2026 — follow-up | Meeting / Voting Management |
| BR-160 | Only the current Chair may open a Draft Vote, and only while its Meeting is InProgress. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-161 | The current Chair may record Passed, NotPassed or Deferred, with an optional note, for a Closed and tallied Vote while its Meeting is InProgress or Ended. | UC-52b, UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-162 | When a new Meeting is created, Planned Start Time must be later than the current time. | UC-14 | PO Q7 / BF-02 | Meeting / Voting / Minutes services |
| BR-163 | Online/Hybrid Meetings use the internal TrustCabinet Online Meeting Room. Online Link is managed by the system as part of Online Meeting Room functionality and may be empty until generated. Users cannot enter or modify it during Meeting creation, editing or rescheduling. Link generation is outside Create Meeting and Update Meeting. | UC-14, UC-15 | PO Q4 / BF-02 | Meeting / Voting / Minutes services |
| BR-164 | Starting a Meeting creates an in-app notification for its currently invited internal participants; notification failure does not roll back the successful Meeting start. | UC-21 | PO Q11 / BF-03 | Meeting / Voting / Minutes services |
| BR-165 | A ballot may be cast only while the Meeting is InProgress, the Vote is Open and the voter retains current Meeting access. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-166 | When designated minutes signers request revision or minutes content changes after signing begins, previous signatures on the earlier content version are invalidated and the revised version requires a new signing cycle. Only the assigned Secretary may finalize and seal the completed minutes after all designated signers sign. | UC-60, UC-61, UC-62, UC-64, UC-65 | PO / BF-04 | Meeting / Voting / Minutes services |
| BR-167 | User account list sorting must use an allowed sort field/order, and current search/filter/sort criteria are preserved across pages. | UC-05 | UC-05 user-list acceptance criteria | User Management |
| BR-168 | A submission adding multiple internal Meeting Participants must be saved as one indivisible roster change. If any selected account, assigned Meeting Role or additional Meeting Permission is invalid, no account in the submitted batch may be added. Invitation dispatch occurs only after a successful roster save; delivery errors follow BR-94. | UC-28a | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-169 | Replacing or handing over the Chair changes the outgoing Chair to Member and the incoming current internal participant to Chair together. The final list must retain the required Secretary and Chair. If either change fails, both people keep their previous roles. | UC-28b, UC-73 | FE-06 revision, 12/10/2026; PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair | Meeting Participant Management |
| BR-170 | Only an active current assigned Secretary may save or revise official attendance while the Meeting is InProgress. For each current internal participant or current Guest, the only official saved attendance values are Present or Absent. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-171 | A person whose attendance has not been recorded remains unmarked. An unmarked person must not be counted as Absent. Only Present and Absent may be saved as official attendance. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-172 | Attendance may be saved or revised for one or more selected current internal participants or Guests without covering the full roster. Every submitted entry must identify a current person and contain Present or Absent; omitted entries retain their saved or unmarked value. All submitted changes succeed together or none are saved, with the responsible Secretary and save time. Revalidate submitted entries and current authority/status when saving; unrelated roster additions do not require attendance for those people. Attendance completeness never prevents End Meeting. | UC-31 | PO 13/10/2026 | Meeting Participant Management |
| BR-173 | Each current internal participant has exactly one meeting role: Chair, Secretary or Member. Each role provides predefined permissions for that Meeting. Meeting roles and additional meeting permissions are independent of the person’s system role. | UC-27, UC-28a, UC-28b, UC-29, UC-30 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-174 | A current assigned Secretary may grant additional Meeting permissions to current internal participants and current Guest invitations, supplementing the permissions already provided by their Meeting role. View meeting participants and View invitation responses are examples, not an exhaustive list. The available permission catalogue and role-to-permission grant matrix remain to be specified; this rule does not invent either. Grants apply to this Meeting only and do not change system roles or internal Meeting roles. Guest-specific exclusions remain under BR-181. | UC-27, UC-28a, UC-28b, UC-30 | PO 13/10/2026 | Meeting Participant Management |
| BR-175 | Only a current assigned Secretary may grant, change or revoke individual additional Meeting permissions while roster management is permitted. An internal participant retains their existing Meeting role; a Guest retains Guest access without acquiring an internal role. Organizer Secretary initialization/recovery does not itself grant permission-management authority. | UC-28a, UC-28b | PO 13/10/2026 | Meeting Participant Management |
| BR-176 | Before publication, a Chair, Member or Guest cannot view the invited participant roster or invitation responses. From Published onward, Chair has both views by default and Secretary access follows existing rules; a current Member or verified current Guest needs the matching additional permission. View Invitation Responses includes names and emails in response rows but does not grant roster browsing/search. The connected-room list in UC-40 is available to connected Members and Guests without either additional grant. | UC-27, UC-30 | PO 13/10/2026 | Meeting Participant Management |
| BR-177 | Internal Meeting role changes and individual additional-permission changes are saved consistently. Effective Meeting permissions combine role defaults and valid additional grants; changing a role does not by itself require discarding compatible grants. Removed assignments and revoked Guest invitations immediately lose their grants and access, while historical evidence is retained. Published/InProgress changes follow existing notifications. | UC-28a, UC-28b, UC-28c | PO 13/10/2026 | Meeting Participant Management |
| BR-178 | Official attendance must be recordable and retrievable for both current internal Meeting Participant records and current external Guest invitations. An attendance record includes the saved status, the responsible Secretary and the save time; historical evidence is retained when a published Meeting's participant or Guest is removed/revoked. | UC-28c, UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-179 | Retired on 13/10/2026 by PO decision. Attendance completeness is not a prerequisite for ending a Meeting. This ID must not be reused. | Retired; formerly UC-22, UC-31 | PO 13/10/2026 | Meeting Participant Management |
| BR-180 | After verifying the invited email, a Guest provides a display name and job title for presentation in the Meeting. These are self-declared information; only control of the invited email is verified. Changing display information does not change the invitation identity or the invited email. Required-field rules and detailed profile validation are not finalized here. | UC-04, UC-27, UC-37, UC-40 | PO 13/10/2026 | Guest participation / Meeting Participant Management |
| BR-181 | A Guest has the same participation rights as a Participant within the invited Meeting. | UC-04, UC-27, UC-28a, UC-28b, UC-29, UC-30, UC-31, UC-32a, UC-32c, UC-35, UC-37–UC-42, UC-52a, UC-52b, UC-55 | PO 14/10/2026 — atomic rule review | Guest participation / Meeting Participant Management |
| BR-182 | Inviting a Guest into a Confidential or Secret Meeting displays a clear inline warning that the Guest will receive Member participation and Meeting-document access. Invitation creation uses the ordinary invite flow; no extra confirmation or separate sensitivity approval flow is required. Record the inviting Secretary, Meeting sensitivity and invitation time for accountability. | UC-28a | PO 13/10/2026 | Guest participation / Meeting Participant Management |
| BR-183 | An uploaded file's content cannot be changed. | UC-32a, UC-32c, UC-33a, UC-33c, UC-34, UC-36, UC-71 | PO 14/10/2026 — atomic rule review | Document Management |
| BR-184 | Only the current Chair or Secretary may add or remove Meeting documents or their Agenda Item links. | UC-33a, UC-33b, UC-33c, UC-33d | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-185 | Meeting documents and their Agenda Item links cannot be added or removed after the Meeting ends. | UC-23b, UC-33a, UC-33b, UC-33c, UC-33d | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-186 | Deleting a personal-library document does not remove the document from Meetings. | UC-33c, UC-71a | PO 14/10/2026 — atomic rule review | Document Management |
| BR-187 | Only PDF and DOCX documents may be uploaded. | UC-33a, UC-36 | PO 14/10/2026 — atomic rule review | Document Management |
| BR-188 | Documents of a Secret Meeting may be read online but not downloaded. | UC-32c | PO 14/10/2026 — atomic rule review | Document Management |
| BR-189 | The current Chair or the current Secretary may delete a Draft Vote while its Meeting is Approved, Published or InProgress. An Open or Closed Vote cannot be deleted. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019; PO 16/10/2026 — Secretary may prepare Vote drafts | Meeting / Voting Management |
| BR-190 | The eligible-voter list and count are fixed when the Vote opens, using the selected meeting roles and valid Meeting access. Attendance does not determine eligibility. | UC-54, UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-191 | Personal-library folders may be nested at most five levels deep. | UC-34, UC-36, UC-71b, UC-71c, UC-72a, UC-72b, UC-72c, UC-72d | PO 14/10/2026 — atomic rule review | Document Management |
| BR-192 | An automatically created Meeting folder is named “<Meeting title> – dd/mm/yyyy”, using the Meeting planned start date in Vietnam time. | UC-33a | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-193 | The same uploaded file may have only one active attachment in a Meeting. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| BR-194 | A direct upload creates both the Meeting document and the personal-library document, or creates neither. | UC-33a | PO 14/10/2026 — atomic rule review | Document Management |
| BR-195 | The current Chair or Secretary may add documents from the time they can view the Meeting until it ends. | UC-33a | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-197 | Each Meeting-document addition or removal records who performed it and when. | UC-33a, UC-33c | PO 14/10/2026 — atomic rule review | Document Management |
| BR-199 | A Secretary may grant a Guest the additional Meeting permissions available to an internal Participant. | UC-28b, UC-32a, UC-32c | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-200 | Additional permissions cannot override Guest restrictions or the prohibition on downloading Secret Meeting documents. | UC-28b, UC-32a, UC-32c | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-201 | A Guest receives no system Role. | UC-04, UC-28a | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-202 | Retired on 18/10/2026: delegated attendance was removed (decision D4, BR-105), so there is no delegated authority to restrict. This ID must not be reused. | Retired; formerly UC-28b | Council review 1 — delegation removed | Document / Guest access |
| BR-203 | A Guest cannot moderate a Meeting room. | UC-41 | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-204 | A Guest cannot use a personal document library. | UC-34, UC-36, UC-71, UC-72 | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-205 | Meeting documents have no separate sharing option for Guests. | UC-32a, UC-32c, UC-33a | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026 | Document / Guest access |
| BR-206 | Uploaded documents have no versions. | UC-32a, UC-32c, UC-33a, UC-33c, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-208 | Changing document content requires uploading a new file and replacing the old Meeting document. | UC-33a, UC-33c, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-209 | A Meeting document must come from the actor's own personal library or computer. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-210 | A file uploaded directly into a Meeting is also added to the uploader's personal library. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-211 | A removed document is excluded from the Meeting's current document list. | UC-32a, UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-212 | Removing a Meeting document preserves its comments as history. | UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-213 | A file is deleted by periodic cleanup when no personal-library document or active Meeting document uses it. | UC-33c, UC-71a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-214 | An uploaded document must not exceed 50 MB. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-215 | An uploaded document's type is determined from its actual content. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-216 | Uploaded files retain their original format and content. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-219 | A personal-library folder belongs to one user. | UC-34, UC-72 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-220 | A folder name must not be empty. | UC-72a, UC-72b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-221 | Folder names need not be unique. | UC-72a, UC-72b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-222 | A folder cannot be moved into itself or one of its descendants. | UC-72c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-223 | Only an empty folder can be deleted. | UC-72d | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-224 | A user may rename documents in their own personal library. | UC-71b | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-225 | A user may move personal-library documents only between their own folders or the library root. | UC-71c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-226 | The first direct upload creates a personal-library folder for that user and Meeting. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-227 | Later uploads to the same Meeting reuse its existing personal-library folder, even after it is renamed or moved. | UC-33a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-228 | If the Meeting's personal-library folder was deleted, the next direct upload recreates it at the library root, named with the Meeting title and planned start date in Vietnam time. | UC-33a | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-229 | Separate uploads are different documents, even when their names or contents match. | UC-33a, UC-36 | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-231 | Removing a Meeting document does not remove it from personal libraries or other Meetings. | UC-33c | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-232 | Deleting an unused file preserves its history and comments, but its content is no longer available. | UC-33c, UC-71a | ADR-0018 / PO 14/10/2026 — atomic rule review | Document / Guest access |
| BR-235 | Declining a Meeting invitation does not revoke document-reading access. | UC-32a, UC-32c | ADR-0006 — cross-review 14/10/2026 | Document / Guest access |
| BR-236 | Retired on 18/10/2026 by PO decision of 16/10/2026: merged into BR-68. This ID must not be reused. | Retired; formerly UC-23b | PO 16/10/2026 | Agenda Management |
| BR-237 | Only Pending Agenda Items may be deleted. | UC-23c | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| BR-238 | Agenda Items may be reordered whatever their execution status; reordering changes only their positions. | UC-23d | PO 16/10/2026 — simplified Agenda (no planned time, no start order) | Agenda Management |
| BR-239 | Deleting an Agenda Item preserves the positions of the remaining Items. | UC-23c | Split from former BR-68 — no change to agenda field restrictions | Agenda Management |
| BR-240 | Retired on 18/10/2026 by PO decision of 16/10/2026: merged into BR-238. This ID must not be reused. | Retired; formerly UC-23d | PO 16/10/2026 | Agenda Management |
| BR-241 | Retired on 18/10/2026 by PO decision of 16/10/2026: merged into BR-68. This ID must not be reused. | Retired; formerly UC-23b, UC-23c, UC-23d | PO 16/10/2026 | Agenda Management |
| BR-242 | A user may access only their own personal-library documents and folders. | UC-34, UC-36, UC-71a–UC-71c, UC-72a–UC-72d | ADR-0006 / ADR-0018 | Document Management |
| BR-245 | Moving a personal-library document does not change its Meeting attachments. | UC-71c | ADR-0018 | Document Management |
| BR-246 | A comment must not be blank. | UC-35 | Report 3 screen 7.5 — required comment text | Document Management |
| BR-248 | A DOCX comment has no page reference. | UC-35 | ADR-0018 | Document Management |
| BR-249 | Anyone who can view a Meeting document may comment on it before the Meeting ends. | UC-35 | PO 15/10/2026 — read-only after Meeting end | Document Management |
| BR-250 | Each comment records its author and posting time. | UC-35 | Report 3 screen 7.5 | Document Management |
| BR-251 | Personal-library usage details show only Meetings the user may access. | UC-34 | Existing resource confidentiality rule | Document Management |
| BR-252 | Only the current Chair or the current Secretary may create or configure a Draft Vote while its Meeting is Approved, Published or InProgress. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019; PO 16/10/2026 — Secretary may prepare Vote drafts | Meeting / Voting Management |
| BR-253 | Only the current Chair and the current Secretary may view Draft Votes in the Meeting. | UC-52a, UC-52b, UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019; PO 16/10/2026 — Secretary may prepare Vote drafts | Meeting / Voting Management |
| BR-254 | The content, options, ballot type and selected roles of an Open or Closed Vote cannot be edited. | UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-255 | Vote content must not be blank. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-256 | A Vote must have at least two distinct, non-empty options. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-257 | The Chair selects one or more eligible meeting roles from Chair, Secretary, Member and Guest; all four roles are selected by default. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-258 | Each eligible voter may cast at most one ballot in a Vote. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-259 | A secret Vote records participation without linking a voter to a chosen option; only aggregate results may be disclosed. | UC-52b, UC-55, UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-260 | The status view of an Open Vote shows turnout, without disclosing individual choices or results per option. | UC-52b | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-261 | Closing a Vote tallies all recorded ballots per option; each percentage uses ballots cast as its denominator. With no ballots, option counts are zero and percentages are not shown. | UC-52b, UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-262 | A recorded Vote conclusion and its note cannot be changed or replaced. | UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-263 | The Chair determines a Vote conclusion; the system does not infer it from the tally. | UC-56, UC-57 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-264 | Meeting Minutes include the Vote tally and the conclusion recorded by the Chair. | UC-57, UC-59 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-265 | A recorded ballot cannot be changed or withdrawn. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-266 | A Meeting may have at most one Open Vote at a time. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-267 | A Vote cannot be opened if no eligible voters are available. | UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-268 | Changing a voter's meeting role after opening does not change the fixed eligible-voter list or count. | UC-54, UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-269 | Loss of Meeting access prevents new ballots without deleting previously recorded ballots. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-270 | The current Chair may close and tally an existing Open Vote while its Meeting is InProgress or Ended. | UC-56 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-271 | A ballot selects exactly one option belonging to its Vote. | UC-55 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-272 | Each Vote uses either a Public or Secret ballot type. | UC-53, UC-54 | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-273 | After a Public Vote closes, its result shows each eligible voter's recorded choice or that the voter did not vote. | UC-52b | PO 14/10/2026 — FE-11 specification / ADR-0019 | Meeting / Voting Management |
| BR-274 | The current Chair or the current Secretary may access Vote draft preparation in an Approved Meeting; this does not grant access to other restricted Meeting records. | UC-52a, UC-52b, UC-53 | PO 14/10/2026 — FE-11 specification / ADR-0019; PO 16/10/2026 — Secretary may prepare Vote drafts | Meeting / Voting Management |
| BR-275 | Anyone who can view a Meeting can read its documents. | UC-32a, UC-32c, UC-35 | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-276 | A Meeting document keeps the name copied when it was added to the Meeting; later personal-library renaming does not change that name. | UC-33a, UC-71b | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-277 | One Meeting document may be linked to multiple Agenda Items. | UC-23b, UC-33b | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-278 | A document may be linked only to Agenda Items of the same Meeting. | UC-23a, UC-23b, UC-33b, UC-33d | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-279 | Pending, InProgress and Done Agenda Items all permit document-link additions and removals until the Meeting ends. | UC-23b, UC-33b, UC-33d | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-280 | Removing one Agenda Item link keeps the document in the Meeting and preserves its other Item links; with no links left, it is a general Meeting document. | UC-33d | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-281 | Removing a document from a Meeting removes it from every Agenda Item of that Meeting. | UC-33c | ADR-0020 / PO 15/10/2026 | Document Management |
| BR-282 | Meeting documents and comments are read-only after the Meeting ends. | UC-32a, UC-32c, UC-33a, UC-33b, UC-33c, UC-33d, UC-35 | PO 15/10/2026 — document follow-up | Document Management |
| BR-283 | Deleting an Agenda Item removes its document links but keeps the documents in the Meeting. | UC-23c, UC-32a, UC-33b, UC-33d | PO 15/10/2026 — document follow-up | Document Management |
| BR-284 | Readding a removed Meeting document starts a new addition without its previous comments. | UC-33a, UC-33c, UC-35 | PO 15/10/2026 — document follow-up | Document Management |
| BR-285 | Each Vote belongs to one Meeting and is not linked to an Agenda Item. | UC-52a, UC-52b, UC-53, UC-54, UC-55, UC-56, UC-57 | ADR-0019 / ADR-0020 | Voting Management |
| BR-286 | All documents submitted together are added together, or none are added. | UC-33a, UC-36 | User confirmation 15/10/2026 — multiple-file upload; existing screen 7.4 all-or-none outcome | Document Management |
| BR-287 | The current Chair or Secretary may attach one or more documents to a Draft Vote as Vote documents. A Vote may have no Vote documents. | UC-52b, UC-74, UC-76a | Review 18/10/2026 — documents by target (Agenda Item / Vote), three sources | Voting Management |
| BR-288 | Vote documents can be attached or detached only while the Vote is Draft. The Vote document list is fixed when the Vote opens. | UC-52b, UC-54, UC-76a, UC-76b | Review 18/10/2026 — documents by target (Agenda Item / Vote), three sources | Voting Management |
| BR-289 | An eligible voter may cast a ballot only after confirming reading of every Vote document of that Vote. | UC-55, UC-74 | PO 16/10/2026 — Vote documents and read confirmation | Voting Management |
| BR-290 | A document read is confirmed only after the user has scrolled to the last page of the document in the viewer and then ticks "I have read this document". The system records the user, the document and the confirmation time. | UC-32c, UC-74 | PO 16/10/2026 — Vote documents and read confirmation | Document Management |
| BR-291 | A read confirmation belongs to one user and one Meeting document. It counts for every Vote of the Meeting that links the same document and cannot be withdrawn. | UC-55, UC-74 | PO 16/10/2026 — Vote documents and read confirmation | Document Management |
| BR-292 | If a Vote document is removed from the Meeting while the Vote is Open, it no longer counts towards the reading requirement of that Vote. | UC-55 | PO 16/10/2026 — Vote documents and read confirmation | Voting Management |
| BR-293 | Read confirmations are stored separately from ballots and never reveal or link a voter's choice in a secret Vote. | UC-55, UC-74 | PO 16/10/2026 — Vote documents and read confirmation | Voting Management |
| BR-294 | Before the Meeting is Published, the assigned Secretary may assign or replace the Chair. | UC-28b | PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair | Meeting Participant Management |
| BR-295 | The Chair role may be handed over only to a current active internal participant of the Meeting who is neither a Guest nor the current Secretary. | UC-73 | PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair | Meeting Participant Management |
| BR-296 | After a hand-over, the former Chair remains a Member of the Meeting; leaving the meeting room does not remove him or her from the Meeting. | UC-73 | PO 16/10/2026 — Chair hands over the Chair role; Secretary does not replace the Chair | Meeting Participant Management |
| BR-297 | A Meeting can be cancelled only by its Meeting Organizer, and only while it is Draft, WaitingApproval, Rejected, Approved or Published. An InProgress or Ended Meeting cannot be cancelled. Cancelled is a final status. | UC-17, UC-75 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-298 | Cancelling a Meeting requires a non-blank reason. The reason, the actor and the cancellation time are recorded in the Meeting status history and shown with the Meeting. | UC-16, UC-75 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-299 | A Cancelled Meeting cannot be edited, submitted, approved, published, rescheduled or started. Its agenda, participants, documents and Votes remain read-only, and any pending approval request is closed. | UC-75 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-300 | When a Published Meeting is cancelled, every invited internal participant is notified in the application and by email, and every invited Guest by email, including the reason. A notification failure does not undo the cancellation. | UC-75 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-301 | If a Published Meeting has not been started 24 hours after its planned end time, the system cancels it with the reason "Not held by the planned time", records System as the actor and notifies its Meeting Organizer. The delay is configurable; the default awaits PO confirmation. | UC-75 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-302 | If an InProgress Meeting has not been ended 4 hours after its planned end time, the system ends it, records System as the actor and notifies its Chair and Meeting Organizer. The delay is configurable; the default awaits PO confirmation. | UC-22 | Council review 1 — cancel and overdue Meetings | Meeting Management |
| BR-303 | The system sends one in-app reminder to each invited active internal participant of a Published Meeting during the 15 minutes before its planned start. If the Meeting is rescheduled, the reminder is sent again for the new start time. Guests do not receive in-app reminders. The lead time is configurable. | UC-66 | Council review 1 — reminder rules; matches the implemented reminder job | Email & In-App Notification |
| BR-304 | Documents attached to an Agenda Item or a Vote may be chosen from the Meeting documents, uploaded from the computer or chosen from the personal library. Documents not yet in the Meeting are added to it and attached in the same save. All documents submitted together are attached together, or none are. | UC-33b, UC-76a | Review 18/10/2026 — documents by target (Agenda Item / Vote), three sources | Document Management |
| BR-305 | Detaching a document from a Vote keeps the document in the Meeting with its Agenda Item links, comments and read confirmations unchanged. | UC-76b | Review 18/10/2026 — documents by target (Agenda Item / Vote), three sources | Voting Management |
| BR-306 | The Presenter of an Agenda Item is optional; when set, it must be a current internal participant or a current Guest of the same Meeting. | UC-23a, UC-23b | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-307 | When a Presenter is assigned or changed while the Meeting is Published or InProgress, the new Presenter is notified: an internal user in the application, a Guest by email. A notification failure does not undo the change. | UC-23a, UC-23b | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-308 | When a participant is removed from the Meeting or a Guest invitation is revoked, the Agenda Items that person presents no longer have a Presenter. | UC-28c | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-309 | An Agenda Item has at most one conclusion: a non-blank text of at most 2,000 characters. Saving again replaces the earlier conclusion. | UC-77 | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-310 | The current Chair or Secretary may record or change an Item conclusion only for an InProgress or Done Item, while the Meeting is InProgress or after it ends until the minutes draft is generated. After that, the conclusion is read-only. | UC-77 | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-311 | The system records who last saved an Item conclusion and when, and shows them with the conclusion. | UC-26, UC-77 | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-312 | Meeting Minutes include each Agenda Item with its Presenter and recorded conclusion. | UC-59, UC-77 | Review 18/10/2026 — Agenda presenter and Item conclusion | Agenda Management |
| BR-313 | A change to a role's permissions applies to the users holding that role from their next action, without ending their active sessions. | UC-11 | SCRUM-105, BA 22/10/2026 | Role-Based Access Control |
| BR-314 | Changing a user's system role applies the new role's permissions to that user from their next action, without ending their active sessions. | UC-08 | SCRUM-105, PO 22/10/2026 | Role-Based Access Control |
| BR-315 | Opening a Meeting document from the online meeting room keeps the user in the room. | UC-32a, UC-32c | SCRUM-21, BA 22/10/2026 — ADR-0011, PO approved 22/10/2026 | Document Management |
| BR-316 | The online meeting room offers Meeting documents for reading and commenting only; adding, linking and removing documents are done outside the room. | UC-32a | SCRUM-21, BA 22/10/2026 — ADR-0011, PO approved 22/10/2026 | Document Management |
| BR-317 | A reply belongs to one top-level comment of the same Meeting document; replies are not nested further. | UC-35 | SCRUM-23, BA 22/10/2026 — PO approved 22/10/2026 | Document Management |
| BR-318 | Each view or download of a document of a Confidential or Secret Meeting is recorded in the audit log with the user and time. | UC-32c | SCRUM-22, PO 13/10/2026 | Document Management |
| BR-319 | One upload from the computer to a Meeting contains at most 10 files with a total size of at most 200 MB. | UC-33a | PO 22/10/2026 — as in AI-eCabinet | Document Management |

### 2. System Messages

| # | Message code | Message Type | Context | Content |
| --- | --- | --- | --- | --- |
| 1 | MSG01 | Form error | UC-01 – Sign in with Email and Password / Exception Flows | Invalid email or password. |
| 2 | MSG02 | Form error | UC-01 – Sign in with Email and Password / Exception Flows | Your account is currently unavailable. Please contact your administrator for assistance. |
| 3 | MSG03 | Form error | UC-02 – Sign in with Google / Exception Flows | Google sign-in was unsuccessful. |
| 4 | MSG04 | Form error | UC-02 – Sign in with Google / Exception Flows | Unable to verify your Google account. Please try again. |
| 5 | MSG05 | Form error | UC-02 – Sign in with Google / Exception Flows | This Google account has not been granted access to the system. |
| 6 | MSG02 | Form error | UC-02 – Sign in with Google / Exception Flows | Your account is currently unavailable. Please contact your administrator for assistance. |
| 7 | MSG06 | Dialog / Toast | UC-03 – Sign Out / Exception Flows | Unable to sign out. Please try again. |
| 8 | MSG140 | Redirect to sign-in | Common – Session Renewal | Your session has expired. Please sign in again. |
| 9 | MSG02 | Redirect to sign-in | Common – Session Renewal | Your account is currently unavailable. Please contact your administrator for assistance. |
| 10 | MSG07 | Toast | UC-06a – Create User Account / Main Flow | User account created successfully. |
| 11 | MSG08 | Field error | UC-06a – Create User Account / Exception Flows | Email is already in use. |
| 12 | MSG09 | Field error / Form error | UC-06a – Create User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- fullName: Full name is required. / Full name must not exceed 200 characters.<br>- email: Email is required. / Email is invalid.<br>- password: Password must be at least 12 characters.<br>- roleId: The role does not exist. |
| 13 | MSG10 | Toast | UC-06b – Update User Account / Main Flow | User account updated successfully. |
| 14 | MSG08 | Field error | UC-06b – Update User Account / Exception Flows | Email is already in use. |
| 15 | MSG09 | Field error / Form error | UC-06b – Update User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- fullName: Full name is required. / Full name must not exceed 200 characters.<br>- email: Email is required. / Email is invalid.<br>- password: Password must be at least 12 characters.<br>- roleId: The role does not exist. |
| 16 | MSG11 | Toast | UC-06c – Deactivate / Reactivate User Account / Main Flow | User account status changed successfully. |
| 17 | MSG09 | Form error | UC-06c – Deactivate / Reactivate User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- isActive: The status to set for the account is missing. |
| 18 | MSG12 | Dialog | UC-06c – Deactivate / Reactivate User Account / Exception Flows | Unable to change the status of this user account. |
| 19 | MSG13 | Toast | UC-07 – Reset User Password / Main Flow | Password reset email sent successfully. |
| 20 | MSG92 | Toast | UC-07 – Reset User Password / Main Flow | Password reset successfully. Please sign in with your new password. |
| 21 | MSG06 | Toast | UC-07 – Reset User Password / Alternative Flows | Unable to sign out. Please try again. |
| 22 | MSG14 | Form error | UC-07 – Reset User Password / Exception Flows | Password reset is not available for this account. |
| 23 | MSG15 | Toast | UC-07 – Reset User Password / Exception Flows | Unable to send the password reset email. Please try again. |
| 24 | MSG93 | Page | UC-07 – Reset User Password / Exception Flows | The password reset link is invalid or has expired. Please contact an administrator. |
| 25 | MSG94 | Field error | UC-07 – Reset User Password / Exception Flows | Invalid new password.<br>Field errors:<br>- newPassword: Password must be at least 12 characters. |
| 26 | MSG129 | Page | UC-07 – Reset User Password / Exception Flows | No password reset code was found. Please open the link in the email again. |
| 27 | MSG10 | Toast | UC-08 – Assign System Role to User / Main Flow, Alternative Flows | User account updated successfully. |
| 28 | MSG09 | Field error / Form error | UC-08 – Assign System Role to User / Exception Flows | Invalid account information.<br>Field errors:<br>- roleId: The role does not exist. |
| 29 | MSG12 | Form error | UC-08 – Assign System Role to User / Exception Flows | Unable to change the status of this user account. |
| 30 | MSG16 | Toast | UC-11 – Configure Role Permissions / Main Flow | Role permissions updated successfully. |
| 31 | MSG17 | Inline error (under role row) | UC-11 – Configure Role Permissions / Exception Flows | Invalid permission configuration.<br>Field errors:<br>- roleId: Invalid role.<br>- capabilities: The list of permissions to set for the role is missing. / Invalid permissions: {permission codes}. / The permission list contains invalid values.<br>- adminUsers: Cannot save this configuration: after saving, no active account would hold the user administration permission, so no one could access the permission screen. |
| 32 | MSG18 | Inline error (under role row) | UC-11 – Configure Role Permissions / Exception Flows | Unable to update role permissions. Please try again. |
| 33 | MSG19 | Toast | UC-12a – Create Role / Main Flow | Role created successfully. |
| 34 | MSG20 | Field error / Form error | UC-12a – Create Role / Exception Flows | Invalid role information.<br>Field errors:<br>- name: Role name is required. / Role name must not exceed 64 characters.<br>- description: Description must not exceed 256 characters.<br>- capabilities: Invalid permissions: {permission codes}. |
| 35 | MSG21 | Field error | UC-12a – Create Role / Exception Flows | A role with this name already exists. Please choose a different name. |
| 36 | MSG26 | Form error | UC-12a – Create Role / Exception Flows | Unable to create the role. Please try again. |
| 37 | MSG22 | Toast | UC-12b – Update Role / Main Flow | Role updated successfully. |
| 38 | MSG22 | Toast | UC-12b – Update Role / Alternative Flows | Role updated successfully. |
| 39 | MSG24 | Form error | UC-12b – Update Role / Exception Flows | This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions. |
| 40 | MSG20 | Field error / Form error | UC-12b – Update Role / Exception Flows | Invalid role information.<br>Field errors:<br>- name: Role name is required. / Role name must not exceed 64 characters.<br>- description: Description must not exceed 256 characters.<br>- capabilities: Invalid permissions: {permission codes}. |
| 41 | MSG21 | Field error | UC-12b – Update Role / Exception Flows | A role with this name already exists. Please choose a different name. |
| 42 | MSG27 | Form error | UC-12b – Update Role / Exception Flows | Unable to update the role. Please try again. |
| 43 | MSG23 | Toast | UC-12c – Delete Role / Main Flow | Role deleted successfully. |
| 44 | MSG24 | Dialog | UC-12c – Delete Role / Exception Flows | This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions. |
| 45 | MSG25 | Dialog | UC-12c – Delete Role / Exception Flows | This role is still held by {n} user account(s). Reassign them to another role before deleting. |
| 46 | MSG28 | Dialog | UC-12c – Delete Role / Exception Flows | Unable to delete the role. Please try again. |
| 47 | MSG29 | Toast | UC-14 – Create Meeting / Main Flow | Meeting created successfully. |
| 48 | MSG31 | Field error / Form error | UC-14 – Create Meeting / Exception Flows | Invalid meeting information.<br>Field errors:<br>- title: Meeting title is required. / Meeting title must not exceed 300 characters.<br>- location: Location must not exceed 300 characters.<br>- description: Description must not exceed 4000 characters.<br>- mode: Invalid meeting mode.<br>- sensitivity: Invalid sensitivity level. |
| 49 | MSG35 | Form error | UC-14 – Create Meeting / Exception Flows | Unable to create the meeting. Please try again. |
| 50 | MSG30 | Toast | UC-15 – Update Meeting / Main Flow | Meeting updated successfully. |
| 51 | MSG31 | Field error / Form error | UC-15 – Update Meeting / Exception Flows | Invalid meeting information.<br>Field errors:<br>- title: Meeting title is required. / Meeting title must not exceed 300 characters.<br>- location: Location must not exceed 300 characters.<br>- description: Description must not exceed 4000 characters.<br>- mode: Invalid meeting mode.<br>- sensitivity: Invalid sensitivity level. |
| 52 | MSG31 | Form error | UC-15 – Update Meeting / Exception Flows (Malformed JSON request body; same MSG31 validation category.) | Invalid meeting information. |
| 53 | MSG34 | API error | Technical API response – outside UC-15 business flow | Content-Type must be application/json. |
| 54 | MSG33 | Dialog | UC-15 – Update Meeting / Exception Flows | This meeting can no longer be edited. |
| 55 | MSG36 | Form error | UC-15 – Update Meeting / Exception Flows | Unable to update the meeting. Please try again. |
| 56 | —<br>(HTTP 404) | Page | UC-16 – View Meeting Details / Exception Flows | Meeting not found. |
| 57 | MSG37 | Page | UC-16 – View Meeting Details / Exception Flows | Unable to load meeting details. Please try again. |
| 58 | — | Table message | UC-17 – View Meeting List / Alternative Flows | No meetings match the filter. |
| 59 | MSG38 | Table message | UC-17 – View Meeting List / Exception Flows | Unable to load meetings. Please try again. |
| 60 | MSG32 | Table message | UC-17 – View Meeting List / Exception Flows | The meeting list criteria are invalid. |
| 61 | MSG41 | Dialog | UC-18 – Submit Meeting for Approval / Exception Flows | This meeting can no longer be submitted for approval. |
| 62 | MSG42 | Toast | UC-18 – Submit Meeting for Approval / Exception Flows | Unable to submit the meeting for approval. Please try again. |
| 63 | MSG43 | Toast | UC-19 – Review Meeting Approval / Main Flow | Meeting approved successfully. |
| 64 | MSG44 | Toast | UC-19 – Review Meeting Approval / Alternative Flows | Meeting rejected successfully. |
| 65 | MSG45 | Toast | UC-20 – Publish Meeting / Main Flow | Meeting published successfully. |
| 66 | MSG46 | Dialog | UC-20 – Publish Meeting / Exception Flows | This meeting can no longer be published. |
| 67 | MSG47 | Toast | UC-20 – Publish Meeting / Exception Flows | Unable to publish the meeting. Please try again. |
| 68 | MSG39 | List message | UC-66 – View Notifications / Exception Flows | The requested notification page is unavailable. |
| 69 | MSG40 | List message | UC-66 – View Notifications / Exception Flows | Unable to load notifications. Please try again. |
| 82 | MSG60 | Toast | UC-23a – Create Agenda Item / Main Flow | Agenda item created successfully. |
| 83 | MSG61 | Toast | UC-23b – Update Agenda Item / Main Flow | Agenda item updated successfully. |
| 84 | MSG62 | Toast | UC-23c – Delete Agenda Item / Main Flow | Agenda item deleted successfully. |
| 85 | MSG63 | Toast | UC-23d – Reorder Agenda Items / Main Flow | Agenda order updated successfully. |
| 86 | MSG64 | Toast | UC-24 – Set Current Agenda Item / Main Flow | Current agenda item updated successfully. |
| 87 | MSG65 | Toast | UC-25 – Complete Agenda Item / Main Flow | Agenda item completed successfully. |
| 88 | MSG66 | Field error / Form error | UC-23a – Create Agenda Item / Exception Flows; UC-23b – Update Agenda Item / Exception Flows | Invalid agenda item information.<br>Field errors:<br>- title: Agenda item title is required.<br>- plannedDurationMinutes: Planned duration must be greater than 0.<br>- documents: One or more selected documents are not available to this meeting. |
| 89 | MSG67 | Dialog | UC-23a – UC-23d / Exception Flows | The agenda for this meeting can no longer be modified. |
| 90 | MSG68 | Toast | UC-23a – Create Agenda Item / Exception Flows | Unable to create the agenda item. Please try again. |
| 91 | MSG69 | Toast | UC-23b – Update Agenda Item / Exception Flows | Unable to update the agenda item. Please try again. |
| 92 | MSG70 | Dialog / Toast | UC-23c – Delete Agenda Item / Exception Flows | Unable to delete the agenda item. Please try again. |
| 93 | MSG71 | Toast | UC-23d – Reorder Agenda Items / Exception Flows | Unable to update the agenda order. Please try again. |
| 94 | MSG72 | Dialog | UC-24 – Set Current Agenda Item / Exception Flows | Another agenda item is already in progress. Complete it before starting another agenda item. |
| 95 | MSG73 | Dialog | UC-24 – Set Current Agenda Item / Exception Flows | This agenda item cannot be set as current. |
| 96 | MSG74 | Dialog | UC-25 – Complete Agenda Item / Exception Flows | This agenda item cannot be completed. |
| 97 | MSG75 | Toast | UC-24 – Set Current Agenda Item / Exception Flows | Unable to set the current agenda item. Please try again. |
| 98 | MSG76 | Toast | UC-25 – Complete Agenda Item / Exception Flows | Unable to complete the agenda item. Please try again. |
| 99 | MSG83 | Field error | UC-19 – Review Meeting Approval / Exception Flows | Rejection reason is required. |
| 100 | MSG84 | Field error | UC-19 – Review Meeting Approval / Exception Flows | Rejection reason must not exceed 1000 characters. |
| 101 | MSG95 | Page / Table message | UC-27 – View Meeting Participants / Exception Flows; UC-30 – View Participant Response Status / Exception Flows | Unable to load meeting participants. Please try again. |
| 102 | MSG96 | Toast | UC-28a – Add Meeting Participant / Main Flow | Meeting participant added successfully. |
| 103 | MSG97 | Field error / Form error | UC-28a – Add Meeting Participant / Exception Flows | Invalid meeting participant information.Field messages:- The selected user is not available.- This user is already a meeting participant.- A valid meeting role is required.- The Meeting Organizer may only assign a missing Secretary during preparation.- Guest name and email are required.- Guest email is invalid.- This guest email is already invited to the meeting.- This email is already represented by an internal meeting participant. |
| 104 | MSG98 | Dialog | UC-28a, UC-28b, UC-28c / Exception Flows | The meeting participant list cannot be modified in its current status. |
| 105 | MSG99 | Toast | UC-28a – Add Meeting Participant / Exception Flows | Unable to add the meeting participant. Please try again. |
| 106 | MSG100 | Toast | UC-28b – Manage Meeting Role & Additional Permissions / Main Flow | Meeting role updated successfully. |
| 107 | MSG101 | Field error | UC-28b – Manage Meeting Role & Additional Permissions / Exception Flows | A valid meeting role is required. |
| 108 | MSG102 | Toast | UC-28b – Manage Meeting Role & Additional Permissions / Exception Flows; UC-73 / EX-3 | Unable to update the meeting role. Please try again. |
| 109 | MSG103 | Toast | UC-28c – Remove Meeting Participant / Main Flow | Meeting participant removed successfully. |
| 110 | MSG104 | Toast | UC-28c – Remove Meeting Participant / Exception Flows | Unable to remove the meeting participant. Please try again. |
| 111 | MSG105 | Toast | UC-29 – Respond to Meeting Invitation / Main Flow | Meeting invitation response recorded successfully. |
| 112 | MSG106 | Field error / Form error | UC-29 – Respond to Meeting Invitation / Exception Flows | Invalid meeting invitation response. |
| 113 | MSG107 | Dialog | UC-29 – Respond to Meeting Invitation / Exception Flows | This meeting invitation can no longer be answered. |
| 114 | MSG108 | Toast | UC-29 – Respond to Meeting Invitation / Exception Flows | Unable to save the meeting invitation response. Please try again. |
| 115 | MSG109 | Dialog | UC-28b – Manage Meeting Role & Additional Permissions / Exception Flows; UC-28c – Remove Meeting Participant / Exception Flows | The meeting must retain its required Chair and Secretary. Assign a replacement first. |
| 116 | MSG110 | Toast | UC-21 – Start Meeting / Main Flow | Meeting started successfully. |
| 117 | MSG111 | Toast | UC-22 – End Meeting / Main Flow | Meeting ended successfully. |
| 118 | MSG112 | Dialog | UC-21 – Start Meeting / Exception Flows | This meeting cannot be started. |
| 119 | MSG113 | Dialog | Retired; formerly UC-44 (Meeting Session) / End Meeting gate | Retired with Meeting Sessions (ADR-0013); formerly “Complete or cancel all unfinished meeting sessions before ending the meeting.” This code must not be reused. |
| 120 | MSG114 | Dialog | UC-22 – End Meeting / Exception Flows | This meeting cannot be ended. |
| 121 | MSG115 | Toast | UC-21 – Start Meeting / Exception Flows | Unable to start the meeting. Please try again. |
| 122 | MSG116 | Toast | UC-22 – End Meeting / Exception Flows | Unable to end the meeting. Please try again. |
| 132 | MSG126 | Inline / Form error | UC-04 / EX-2 | Unable to verify the guest invitation. Check the code and try again. |
| 134 | MSG128 | Page / Panel message | UC-26 – View Meeting Agenda / Exception Flows | Unable to load the meeting agenda. Please try again. |
| 113 | MSG138 | Information | UC-15 – Meeting rescheduled successfully | Meeting rescheduled successfully. Invitees will be notified. |
| 114 | MSG139 | Form error | Retired; formerly UC-22 / Open Vote gate | Retired on 13/10/2026: open Votes do not block End Meeting. This code must not be reused. |
| 135 | MSG140 | Form error | UC-18 – Submit Meeting for Approval / EX-5 | No eligible meeting approver is available. Please contact the administrator. |
| 136 | MSG141 | Retired | Retired; formerly UC-23b / EX-4 | Retired on 18/10/2026 (PO 16/10/2026): all Agenda Item fields may be edited in any execution status. This code must not be reused. |
| 137 | MSG142 | Dialog / Form error | UC-23c / EX-2 | Only Pending agenda items can be deleted. |
| 138 | MSG143 | Dialog / Form error | UC-23d / EX-2 | The agenda order is invalid or out of date. Refresh the agenda and try again. |
| 139 | MSG144 | Retired | Retired; formerly UC-24 / EX-3 | Retired on 18/10/2026 (PO 16/10/2026): any Pending Agenda Item may be started, whatever its position. This code must not be reused. |
| 140 | MSG145 | Form error | UC-28a / Detailed selection validation; reserved | One or more selected participants are invalid. No participants were added. |
| 141 | MSG146 | Toast | UC-28b / Reserved; refined UC uses MSG102 | Unable to replace the Chair. Meeting roles remain unchanged. |
| 142 | MSG147 | Toast | UC-31 / Main Flow | Meeting attendance saved successfully. |
| 143 | MSG148 | Field error | UC-31 / EX-1 | Select Present or Absent for each submitted participant or Guest. |
| 144 | MSG149 | Dialog | UC-31 / EX-2 | Attendance can only be recorded while the meeting is in progress. |
| 145 | MSG150 | Toast | UC-31 / EX-3 | Unable to save attendance. No changes were saved. |
| 146 | MSG151 | Dialog | UC-31 / EX-4, BR-172 / Changed submitted participant | The meeting participant list has changed. Refresh it and try again. |
| 147 | MSG152 | Field / Form error | UC-28a / EX-1; UC-28b / EX-1 | One or more selected meeting permissions are not allowed. |
| 148 | MSG153 | Toast | UC-28b / Main Flow | Meeting permissions updated successfully. |
| 149 | MSG154 | Information | UC-28b / Unchanged selection UI; no separate exception flow | No changes to save. |
| 150 | MSG155 | Form error | Retired on 13/10/2026; formerly UC-22 / EX-3 | Retired: attendance incompleteness must not block End Meeting. This code must not be reused. |
| 151 | MSG156 | Form error | UC-32a / EX-1; UC-32c / EX-1; UC-35 / EX-2 | Meeting documents are not accessible. |
| 152 | MSG157 | Form error | UC-32a / EX-2 | Unable to load the document list. Please try again. |
| 153 | MSG158 | Form error | UC-32c / EX-2 | Unable to display the document. Please try again. |
| 154 | MSG159 | Form error | UC-32c / EX-3 | Secret Meeting documents can only be viewed online and cannot be downloaded. |
| 155 | MSG160 | Form error | UC-32c / EX-4 | Unable to download the document. Please try again. |
| 156 | MSG161 | Toast | UC-33a / Normal Flow | Documents added to the Meeting successfully. |
| 157 | MSG162 | Form error | UC-33a / EX-1 | You no longer have permission to add documents to this Meeting. |
| 158 | MSG163 | Retired | ADR-0020 | Retired: document addition and Agenda Item linking are separate operations under ADR-0020. Do not reuse. |
| 159 | MSG164 | Field error / Form error | UC-33a / EX-3; UC-36 / EX-1 | Select valid PDF or DOCX files, each no larger than 50 MB. |
| 160 | MSG165 | Form error | UC-33a / EX-4 | One or more selected documents are already attached to the Meeting. |
| 161 | MSG166 | Form error | UC-33a / EX-5 | Unable to add the documents. No documents were added. Please try again. |
| 162 | MSG167 | Toast | UC-33c / Normal Flow | Document removed from the Meeting successfully. |
| 163 | MSG168 | Retired | ADR-0020 | Retired: Meeting Minutes no longer lock document removal under ADR-0020. Do not reuse. |
| 164 | MSG169 | Form error | UC-33c / EX-2 | You no longer have permission to remove documents from this Meeting. |
| 165 | MSG170 | Form error | UC-33b, UC-33d / EX-2; UC-33c / EX-3; UC-76a / EX-2 | This document is no longer attached to the Meeting. Refresh the document list. |
| 166 | MSG171 | Form error | UC-33c / EX-4 | Unable to remove the document. It remains attached to the Meeting. Please try again. |
| 167 | MSG172 | Form error | UC-33a / EX-6 | Documents cannot be added after the Meeting has ended. |
| 168 | MSG173 | Retired | ADR-0020 | Retired: Agenda Item status no longer locks document links under ADR-0020. Do not reuse. |
| 169 | MSG174 | Form error | UC-33a / EX-7; UC-33b / EX-7; UC-33c / EX-5; UC-33d / EX-7; UC-77 / EX-4 | This Meeting is not accessible. |
| 170 | MSG175 | Form error | UC-34 / EX-2; UC-71a / EX-1; UC-71b / EX-1; UC-71c / EX-1; UC-72b / EX-1; UC-72c / EX-1; UC-72d / EX-1 | This personal-library item is not available. |
| 171 | MSG176 | Form error | UC-34 / EX-1 | Unable to load your personal document library. Please try again. |
| 172 | MSG177 | Toast | UC-36 / Normal Flow | Documents uploaded to your personal library successfully. |
| 173 | MSG178 | Form error | UC-36 / EX-3 | Unable to upload the files. No documents were added. Please try again. |
| 174 | MSG179 | Form error | UC-36 / EX-2; UC-71c / EX-2; UC-72a / EX-2; UC-72c / EX-2 | The selected destination folder is not available in your personal library. |
| 175 | MSG180 | Toast | UC-71a / Normal Flow | Document deleted from your personal library successfully. |
| 176 | MSG181 | Form error | UC-71a / EX-2 | Unable to delete the document. Your personal library remains unchanged. |
| 177 | MSG182 | Form error | Retired — document-name constraint removed with BR-243 | Retired: no document-name validation is specified under this code. Do not reuse. |
| 178 | MSG183 | Toast | UC-71b / Normal Flow | Document renamed successfully. |
| 179 | MSG184 | Form error | UC-71b / EX-2 | Unable to rename the document. Its name remains unchanged. |
| 180 | MSG185 | Toast | UC-71c / Normal Flow | Document moved successfully. |
| 181 | MSG186 | Form error | UC-71c / EX-3 | Unable to move the document. Its location remains unchanged. |
| 182 | MSG187 | Toast | UC-72a / Normal Flow | Folder created successfully. |
| 183 | MSG188 | Form error | UC-72a / EX-1; UC-72b / EX-2 | Enter a folder name. |
| 184 | MSG189 | Form error | UC-72a / EX-3; UC-72c / EX-4 | Folders cannot be nested more than five levels deep. |
| 185 | MSG190 | Form error | UC-72a / EX-4 | Unable to create the folder. No folder was created. |
| 186 | MSG191 | Toast | UC-72b / Normal Flow | Folder renamed successfully. |
| 187 | MSG192 | Form error | UC-72b / EX-3 | Unable to rename the folder. Its name remains unchanged. |
| 188 | MSG193 | Form error | UC-72c / EX-3 | A folder cannot be moved into itself or one of its descendants. |
| 189 | MSG194 | Toast | UC-72c / Normal Flow | Folder moved successfully. |
| 190 | MSG195 | Form error | UC-72c / EX-5 | Unable to move the folder. Its location remains unchanged. |
| 191 | MSG196 | Form error | UC-72d / EX-2 | Only an empty folder can be deleted. |
| 192 | MSG197 | Toast | UC-72d / Normal Flow | Folder deleted successfully. |
| 193 | MSG198 | Form error | UC-72d / EX-3 | Unable to delete the folder. The folder remains unchanged. |
| 194 | MSG199 | Toast | UC-35 / Normal Flow | Comment added successfully. |
| 195 | MSG200 | Form error | UC-35 / EX-1 | Enter a comment. |
| 196 | MSG201 | Form error | Retired — no separate commenting permission | Retired: document-viewing access also permits commenting. Do not reuse. |
| 197 | MSG202 | Form error | UC-35 / EX-3 | Unable to add the comment. No comment was saved. Please try again. |
| 198 | MSG203 | Form error | Retired — mandatory PDF page reference removed | Retired: a PDF comment does not require a page reference. Do not reuse. |
| 199 | MSG204 | Form error | UC-35 / EX-4 | Unable to load the document comments. Please try again. |
| 200 | MSG205 | Form error | UC-52a, UC-52b / EX-1; UC-53, UC-54 / EX-6; UC-55 / EX-7; UC-56 / EX-3; UC-57 / EX-5; UC-76a / EX-5; UC-76b / EX-4 | Meeting votes are not accessible. |
| 201 | MSG206 | Form error | UC-52a / EX-2 | Unable to load the meeting votes. Please try again. |
| 202 | MSG207 | Notification | UC-52a / AF-1 | No votes to display. |
| 203 | MSG208 | Form error | UC-52b / EX-2 | Unable to load the vote status or result. Please try again. |
| 204 | MSG209 | Form error | UC-53, UC-54 / Invalid configuration | Invalid vote information. Check the highlighted fields. |
| 205 | MSG210 | Notification | UC-53 / Normal Flow | Vote draft created successfully. |
| 206 | MSG211 | Notification | UC-53 / AF-1 | Vote draft updated successfully. |
| 207 | MSG212 | Notification | UC-53 / AF-2 | Vote draft deleted successfully. |
| 208 | MSG213 | Form error | UC-53 / EX-2; UC-76a, UC-76b / EX-1 | Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress. |
| 209 | MSG214 | Form error | UC-53 / EX-3 | Unable to create the vote draft. No draft was created. Please try again. |
| 210 | MSG215 | Form error | UC-53 / EX-4 | Unable to update the vote draft. The draft remains unchanged. Please try again. |
| 211 | MSG216 | Form error | UC-53 / EX-5 | Unable to delete the vote draft. The draft remains unchanged. Please try again. |
| 212 | MSG217 | Notification | UC-54 / Normal Flow | Voting opened successfully. |
| 213 | MSG218 | Form error | UC-54 / EX-2 | Only the current Chair can open a Draft vote while the meeting is InProgress. |
| 214 | MSG219 | Form error | UC-54 / EX-3 | Another vote is already open in this meeting. Close it before opening this vote. |
| 215 | MSG220 | Form error | UC-54 / EX-4 | No eligible voters are available for the selected roles. Voting was not opened. |
| 216 | MSG221 | Form error | UC-54 / EX-5 | Unable to open voting. Voting was not opened. Please try again. |
| 217 | MSG222 | Form error | UC-55 / EX-1 | Select one option before submitting your ballot. |
| 218 | MSG223 | Form error | UC-55 / EX-2 | The selected option is not available for this vote. |
| 219 | MSG224 | Form error | UC-55 / EX-3 | You are not eligible to cast a ballot in this vote. |
| 220 | MSG225 | Form error | UC-55 / EX-4 | This vote is no longer accepting ballots. |
| 221 | MSG226 | Form error | UC-55 / EX-5 | Your ballot has already been recorded. It cannot be changed or withdrawn. |
| 222 | MSG227 | Notification | UC-55 / Normal Flow | Your ballot has been recorded. |
| 223 | MSG228 | Form error | UC-55 / EX-6 | Unable to record your ballot. No ballot was recorded. Please try again. |
| 224 | MSG229 | Notification | UC-56 / Normal Flow | Voting closed. The result is available. |
| 225 | MSG230 | Form error | UC-56 / EX-1 | Only the current Chair can close an Open vote while the meeting is InProgress or Ended. |
| 226 | MSG231 | Form error | UC-56 / EX-2 | Unable to close voting. The vote and saved ballots remain unchanged. Please try again. |
| 227 | MSG232 | Form error | UC-57 / EX-1 | Select Passed, NotPassed or Deferred. |
| 228 | MSG233 | Form error | UC-57 / EX-2 | The vote conclusion has already been recorded and cannot be changed. |
| 229 | MSG234 | Form error | UC-57 / EX-3 | Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended. |
| 230 | MSG235 | Notification | UC-57 / Normal Flow | Vote conclusion recorded successfully. |
| 231 | MSG236 | Form error | UC-57 / EX-4 | Unable to record the vote conclusion. No conclusion was recorded. Please try again. |
| 232 | MSG237 | Retired | FE-11 | Retired: closing or cancelling the UI does not require a system message. Do not reuse. |
| 213 | MSG238 | Dành sẵn, chưa triển khai | UC-33b / Normal Flow | Documents attached to the Agenda Item successfully. |
| 214 | MSG239 | Dành sẵn, chưa triển khai | UC-33d / Normal Flow | Document detached from the Agenda Item successfully. |
| 215 | MSG240 | Dành sẵn, chưa triển khai | UC-33b / EX-1 | You no longer have permission to link documents in this Meeting. |
| 216 | MSG241 | Dành sẵn, chưa triển khai | UC-33d / EX-1 | You no longer have permission to unlink documents in this Meeting. |
| 217 | MSG242 | Dành sẵn, chưa triển khai | UC-33b, UC-33d / EX-3 | Select an available Agenda Item in this Meeting. |
| 218 | MSG243 | Dành sẵn, chưa triển khai | UC-33b / EX-4 | One or more selected documents are already attached to the Agenda Item. |
| 219 | MSG244 | Dành sẵn, chưa triển khai | UC-33b / EX-5 | Unable to link the document. No links were changed. Please try again. |
| 220 | MSG245 | Dành sẵn, chưa triển khai | UC-33d / EX-5 | Unable to unlink the document. No links were changed. Please try again. |
| 221 | MSG246 | Dành sẵn, chưa triển khai | UC-33d / EX-4 | This document is no longer linked to the selected Agenda Item. Refresh the document list. |
| 222 | MSG247 | Dành sẵn, chưa triển khai | UC-33b, UC-33d / EX-6 | Document links cannot be changed after the Meeting has ended. |
| 223 | MSG248 | Dành sẵn, chưa triển khai | UC-33c / EX-1 | Documents cannot be removed after the Meeting has ended. |
| 224 | MSG249 | Dành sẵn, chưa triển khai | UC-33a / EX-2 | Select one or more available documents from your personal library. |
| 225 | MSG250 | Retired | Retired; formerly UC-23b / EX-4 | Retired on 18/10/2026 (PO 16/10/2026): Agenda Items no longer have a Planned Duration and Presenter is not locked. This code must not be reused. |
| 226 | MSG251 | Dành sẵn, chưa triển khai | UC-35 / EX-5 | Comments are read-only after the Meeting has ended. |
| 233 | MSG252 | Form error | UC-55 / EX-8 | Confirm that you have read all vote documents before casting your ballot. |
| 234 | MSG253 | Toast | UC-74 / Main Flow | Document read confirmed. |
| 235 | MSG254 | Form error | UC-74 / EX-1 | Scroll to the last page of the document before confirming that you have read it. |
| 236 | MSG255 | Toast | UC-74 / EX-3 | Unable to confirm document reading. Please try again. |
| 237 | MSG256 | Dialog | UC-28b / EX-5 | From publication onward, only the current Chair can hand over the Chair role. |
| 238 | MSG257 | Toast | UC-73 / Main Flow | Chair role handed over successfully. |
| 239 | MSG258 | Dialog | UC-73 / AF-1 | No participant can receive the Chair role. Add an internal participant first. |
| 240 | MSG259 | Form error | UC-73 / EX-1 | The selected participant can no longer receive the Chair role. Select another participant. |
| 241 | MSG260 | Dialog | UC-73 / EX-2 | You can no longer hand over the Chair role for this meeting. |
| 242 | MSG261 | Toast | UC-75 / Main Flow | Meeting cancelled successfully. |
| 243 | MSG262 | Form error | UC-75 / EX-1 | Enter a reason for cancelling the meeting. |
| 244 | MSG263 | Dialog | UC-75 / EX-2 | This meeting can no longer be cancelled. |
| 245 | MSG264 | Toast | UC-75 / EX-3 | Unable to cancel the meeting. Please try again. |
| 246 | MSG265 | Toast | UC-76a / Main Flow | Documents attached to the vote successfully. |
| 247 | MSG266 | Form error | UC-76a / EX-3 | One or more selected documents are already attached to the vote. |
| 248 | MSG267 | Toast | UC-76a / EX-4 | Unable to attach the documents. No documents were attached. Please try again. |
| 249 | MSG268 | Toast | UC-76b / Main Flow | Document detached from the vote successfully. |
| 250 | MSG269 | Dialog | UC-76b / EX-2 | This document is no longer attached to the vote. Refresh the list. |
| 251 | MSG270 | Toast | UC-76b / EX-3 | Unable to detach the document. No changes were made. Please try again. |
| 252 | MSG271 | Toast | UC-77 / Main Flow | Agenda item conclusion saved. |
| 253 | MSG272 | Form error | UC-77 / EX-1 | Enter a conclusion of up to 2,000 characters. |
| 254 | MSG273 | Dialog | UC-77 / EX-2 | The conclusion of this agenda item can no longer be changed. |
| 255 | MSG274 | Toast | UC-77 / EX-3 | Unable to save the agenda item conclusion. Please try again. |
| 256 | MSG288 | Dành sẵn, chưa triển khai | UC-33a / EX-8 | Upload up to 10 files at a time, no larger than 200 MB in total. |

### 3. Other Requirements

…
