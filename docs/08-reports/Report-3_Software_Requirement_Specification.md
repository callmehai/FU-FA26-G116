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
| 12/10/2026 | M | KhanhPG | Refine FE-06 use cases to business-level flows; align Chair/Secretary/Member viewing access, response identity visibility, bulk participant additions, Chair replacement, complete attendance and End Meeting requirements. Update BR/MSG references, participant screens and UC-22 sequence. Preserve existing diagram images. |

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

Conceptual ERD for the independent Meeting model. Vote belongs directly to Meeting; Agenda Item also belongs directly to Meeting. Cardinalities shown are logical business relationships and should be confirmed against the final database design. FE-06 refinement: an internal participant assignment may have zero or more MeetingParticipantPermission grants, each granted by one Secretary account. Both internal participants and Guest invitations carry optional official attendance with the recording Secretary and save time. These are conceptual requirements; they do not assert that runtime schema changes have been implemented.

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
| 3 | PermissionCode | string |  | No | System-defined identifier for View meeting participants or View invitation responses. One grant of each permission per participant assignment. |
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

One item on the agenda of a meeting. The Chair runs the meeting item by item; documents, votes, notes and speaking requests may refer to an agenda item.

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

One question put to a vote within a meeting, optionally linked to an agenda item. Covers both live voting during the meeting and voting with a deadline.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the voting session. |
| 2 | MeetingId | uuid | FK | No | Meeting the vote belongs to (references Meeting). |
| 3 | AgendaItemId | uuid | FK | Yes | Agenda item the vote decides (references AgendaItem). Null when the vote is not tied to an item. |
| 4 | Title | string |  | No | Question or title of the vote. |
| 5 | IsAnonymous | bool |  | No | True for a secret ballot: no record links a voter to the option chosen. |
| 6 | Status | enum |  | No | State of the vote. Values: Draft, Open, Closed. |
| 7 | OpenedAt | timestamptz |  | Yes | Date and time voting opened. |
| 8 | ClosedAt | timestamptz |  | Yes | Date and time voting closed. |
| 9 | EligibleCount | int |  | No | Number of eligible voters, fixed when the vote opens so the result stays reproducible. |
| 10 | Conclusion | enum |  | Yes | Conclusion recorded by the Chair after the vote closes. Values: Passed, NotPassed, Deferred. Null until concluded. |
| 11 | ConclusionNote | string |  | Yes | Explanatory note for the conclusion. |
| 12 | ConcludedById | uuid | FK | Yes | Chair who recorded the conclusion (references User). |

Entity 15: VotingOption

One answer that voters can choose in a voting session.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the option. |
| 2 | VotingSessionId | uuid | FK | No | Voting session the option belongs to (references VotingSession). |
| 3 | Position | int | UK | No | Display order of the option, starting at 1; unique within the voting session. |
| 4 | Label | string |  | No | Text of the option shown to voters. |

Entity 16: VoteReceipt

Records that a user has voted in a voting session, never what they chose. Enforces one vote per eligible voter per session.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the receipt. |
| 2 | VotingSessionId | uuid | FK | No | Voting session voted in (references VotingSession). |
| 3 | VoterId | uuid | FK | No | User who voted (references User); unique together with VotingSessionId. |
| 4 | CastAt | timestamptz |  | No | Date and time the vote was cast. |

Entity 17: VoteChoice

The option actually chosen in a vote; these rows are counted to produce the result.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the choice. |
| 2 | VotingSessionId | uuid | FK | No | Voting session the choice belongs to (references VotingSession). |
| 3 | VotingOptionId | uuid | FK | No | Option chosen (references VotingOption). |
| 4 | VoterId | uuid | FK | Yes | User who made the choice (references User). Always null in an anonymous voting session. |
| 5 | CastAt | timestamptz |  | No | Date and time the choice was cast. |

Entity 18: Document

Document in a user's personal library. The file content is kept in versions; uploading a replacement adds a new version instead of overwriting.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the document. |
| 2 | OwnerId | uuid | FK | No | User who owns the document (references User). |
| 3 | Title | string |  | No | Title of the document. |
| 4 | Category | enum |  | No | Category of the document. Values: Material, Draft, Administrative, Other. |
| 5 | CurrentVersionId | uuid | FK | Yes | Version shown by default (references DocumentVersion). |

Entity 19: DocumentVersion

One uploaded file of a document. Versions are never edited or deleted.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the version. |
| 2 | DocumentId | uuid | FK | No | Document the version belongs to (references Document). |
| 3 | VersionNumber | int | UK | No | Version number, starting at 1; unique within the document. |
| 4 | FileName | string |  | No | Original file name, shown to users and used when downloading. |
| 5 | StoragePath | string |  | No | Location of the file in the document store. |
| 6 | ContentType | string |  | No | File type detected from the file content. |
| 7 | ByteSize | bigint |  | No | File size in bytes. |
| 8 | ContentSha256 | string |  | No | SHA-256 hash of the stored file, used to detect changes to the file. |
| 9 | UploadedById | uuid | FK | No | User who uploaded the version (references User). |

Entity 20: MeetingDocument

Attaches a specific version of a document to a meeting, optionally to one agenda item, so participants see the exact version that was shared.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the meeting document. |
| 2 | MeetingId | uuid | FK | No | Meeting the document is attached to (references Meeting). |
| 3 | DocumentId | uuid | FK | No | Document attached (references Document). |
| 4 | DocumentVersionId | uuid | FK | No | Version of the document pinned to the meeting (references DocumentVersion). |
| 5 | AgendaItemId | uuid | FK | Yes | Agenda item the document supports (references AgendaItem). Null when it applies to the whole meeting. |
| 6 | AddedById | uuid | FK | No | User who attached the document (references User). |
| 7 | AddedAt | timestamptz |  | No | Date and time the document was attached. |

Entity 21: DocumentComment

Comment left by a participant on a meeting document. Comments are threaded: a reply refers to its parent comment.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the comment. |
| 2 | MeetingDocumentId | uuid | FK | No | Meeting document commented on (references MeetingDocument). |
| 3 | ParentCommentId | uuid | FK | Yes | Comment being replied to (references DocumentComment). Null for a top-level comment. |
| 4 | AuthorId | uuid | FK | No | User who wrote the comment (references User). |
| 5 | PageNumber | int |  | Yes | Page of the document the comment refers to, when known. |
| 6 | Body | string |  | No | Content of the comment. |
| 7 | IsResolved | bool |  | No | True when the comment has been resolved. |

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

#### 3.3 Data Business Rules

| Rule | Description | Enforced On |
| --- | --- | --- |
| Unique username | A username identifies at most one internal user account. | Account creation/change |
| Unique email | An internal email address identifies at most one account. | Account creation/change |
| Meeting schedule | Each Meeting has planned start and end times; planned end follows planned start. | Meeting creation/update |
| Single meeting lifecycle | A Meeting has exactly one lifecycle. No Meeting Session entity or nested session lifecycle exists. | Meeting lifecycle |
| Agenda ownership | Each Agenda Item belongs to exactly one Meeting. Reordering is within that Meeting. | Agenda management |
| Vote ownership | Each Vote belongs directly to one Meeting and does not require an Agenda Item. | Voting management |
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
| 10 | Email / SMTP Service | An external outbound email service used to deliver password-reset messages, meeting invitations, guest-access emails, notifications, and minutes-signing related emails. It supports flows such as UC-04 Reset Password, UC-21 Publish Meeting, UC-80 Submit Meeting Minutes for Signing, and notification delivery under FE-13. |

#### 4.2 Use Cases (UC)

| ID | Use Case | Feature | Use Case Description |
| --- | --- | --- | --- |
| UC-01 | Sign in with Email and Password | Authentication & Account Security | Authenticate users using their registered email and password. |
| UC-02 | Sign in with Google | Authentication & Account Security | Authenticate users using their authorized Google account. |
| UC-03 | Sign Out | Authentication & Account Security | End the user's current authenticated session. |
| UC-04 | Access Meeting via Guest Invitation | Authentication & Account Security | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
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
| UC-16 | View Meeting Details | Meeting Management | View authorized Meeting details, including planned schedule, actual start/end times and Meeting lifecycle status. |
| UC-17 | View Meeting List | Meeting Management | View, search and filter Meetings by their own schedule, status, name and confidentiality level. |
| UC-18 | Submit Meeting for Approval | Meeting Management | Submit a Draft or Rejected Meeting with a valid planned schedule for approval. |
| UC-19 | Review Meeting Approval | Meeting Management | Approve or reject an assigned meeting approval request. |
| UC-20 | Publish Meeting | Meeting Management | Publish an Approved Meeting with valid planned time and required Chair/Secretary assignments, and initiate invitations. |
| UC-21 | Start Meeting | Meeting Management | Start a Published Meeting and record actual start time. |
| UC-22 | End Meeting | Meeting Management | End an InProgress Meeting and record actual end time. |
| UC-23 | Manage Agenda Items | Agenda Management | Manage Agenda planning as Secretary: add Pending Items at the end; edit all permitted Pending fields but only Title/Description after start; delete only Pending and reorder only existing Pending positions. |
| UC-24 | Set Current Agenda Item | Agenda Management | Chair starts the first Pending Item in the current order when no Item is InProgress. |
| UC-25 | Complete Agenda Item | Agenda Management | Chair completes the current Item regardless of Planned Duration, records actual duration and does not automatically start the next Item. |
| UC-26 | View Meeting Agenda | Agenda Management | Authorized viewers see the complete Agenda and automatically receive its successfully saved changes. |
| UC-27 | View Meeting Participants | Meeting Participant Management | View the current internal participants and external Guests associated with a meeting, together with their meeting-specific role or guest access type. |
| UC-28 | Manage Meeting Participants | Meeting Participant Management | Add participants, manage meeting roles and additional permissions, replace the Chair, or remove participants; see UC-28a–UC-28c. |
| UC-29 | Respond to Meeting Invitation | Meeting Participant Management | Allow an invited internal participant or external Guest to accept or decline their own Meeting invitation, and change a previous response while responses are allowed. |
| UC-30 | View Participant Response Status | Meeting Participant Management | View the latest invitation response of current internal participants and external Guests and a response-status summary. |
| UC-31 | Record Attendance | Meeting Participant Management | Record or revise Present/Absent attendance for every current internal participant and external Guest together while the Meeting is InProgress. |
| UC-32 | View Meeting Documents | Personal Document Library & Meeting Document Management | View Meeting Documents, their version history, and document previews; see UC-32a–UC-32c. |
| UC-33 | Manage Meeting Documents | Personal Document Library & Meeting Document Management | Add a document to a Meeting or add a new Meeting document version; see UC-33a–UC-33b. |
| UC-34 | View Personal Document Library | Personal Document Library & Meeting Document Management | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-35 | Comment on Meeting Document | Personal Document Library & Meeting Document Management | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
| UC-36 | Upload Document to Personal Library | Personal Document Library & Meeting Document Management | Placeholder only - detailed Use Case Specification will be elaborated in the iteration that delivers this feature. |
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
| UC-52 | View Meeting Votes | Voting Management | View Meeting votes and their current status/results; see UC-52a–UC-52b. |
| UC-53 | Create / Configure Vote | Voting Management | Allow the Chair to create and configure a Vote directly under a Meeting while it is InProgress, without vote tally or deadline. |
| UC-54 | Open Vote | Voting Management | Allow the Chair to open a configured vote while the Meeting is InProgress. |
| UC-55 | Cast Vote | Voting Management | Cast an eligible vote for a Vote belonging to the Meeting. |
| UC-56 | Close Vote | Voting Management | Close an open Vote belonging to the Meeting. |
| UC-57 | Record Vote Conclusion | Voting Management | Allow the Chair to record Passed, NotPassed or Deferred after a Vote closes. |
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

#### 4.3 Use Case Diagrams

##### 4.3.1 UCs for System Admin

The System Admin manages user accounts (create, update, deactivate/reactivate, reset password, unlock, assign system roles), configures roles and permissions, and reviews the audit log. The Admin can also view meetings and their records, reports and statistics, notifications, the personal dashboard and the personal document library.

##### 4.3.2 UCs for Meeting Organizer

The Meeting Organizer creates and updates meetings, submits them for approval and publishes approved meetings, adds participants and Guests, and attaches meeting documents and new versions. The Organizer can also view meeting records, comment on documents, use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace.

##### 4.3.3 UCs for Meeting Approver

The Meeting Approver reviews assigned meeting approval requests and approves or rejects them. The Approver can view meeting details and related records (agenda, documents, media, votes, minutes and signature status), use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace.

##### 4.3.4 UCs for Chair

The Chair responds to the invitation, starts and ends the meeting, steers the agenda (set current / complete item), joins and moderates the online room, and creates, opens and closes votes and records each vote's conclusion. The Chair also comments on meeting documents, signs or requests revision of the minutes, and can view all records of the chaired meeting. The current assigned Chair may cast a vote (UC-55) while the Meeting is InProgress and the Vote is Open, under BR-160 and BR-165.

##### 4.3.5 UCs for Secretary

The Secretary prepares the meeting: agenda items, participants and meeting roles, invitation responses, attendance and meeting documents, and also responds to the invitation and takes part in the online room. After the meeting, the Secretary uploads audio, corrects the AI speaker-attributed transcript, generates, edits, approves and publishes the summary, and generates, edits, submits and finalizes the minutes for digital signing, signing or requesting revision when designated as a signer. The current assigned Secretary may cast a vote (UC-55) while the Meeting is InProgress and the Vote is Open, under BR-160 and BR-165.

##### 4.3.6 UCs for Participant

The Participant responds to the meeting invitation, joins the online room, views meetings, agenda, documents and media, comments on documents, casts votes when eligible, and signs or requests revision of the minutes when designated as a signer. The Participant can also view reports, notifications and the personal workspace.

##### 4.3.7 UCs for Guest

The Guest is an external invitee without an internal account who accesses a specific meeting through an emailed invitation verified by a one-time code. Within that verified Guest session the Guest can respond to the invitation, view documents shared with Guests, and join and take part in the online meeting room.

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
| Meeting | View details | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Meeting | Submit for approval | No | Restricted ² | No | No | No | No | No |
| Meeting | Approve / Reject | No | No | Restricted ⁴ | No | No | No | No |
| Meeting | Publish | No | Restricted ⁵ | No | No | No | No | No |
| Meeting | Start / End | No | No | No | Restricted ⁶ | No | No | No |
| Agenda item | View | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Agenda item | Create / Update / Delete / Reorder | No | No | No | No | Restricted ⁸ | No | No |
| Agenda item | Set current / Complete | No | No | No | Restricted ⁹ | No | No | No |
| Participant | View participants / response status | No | No | No | Full from Published onward | Restricted ³: roster under existing rules; responses from Published onward | Restricted ³: from Published, each view requires its matching additional permission | No |
| Participant | Add participant / Invite Guest | No | Restricted ³ | No | No | Restricted ³ | No | No |
| Participant | Manage meeting role & additional permissions | No | No | No | No | Restricted ³ | No | No |
| Participant | Remove participant / Revoke Guest | No | No | No | No | Restricted ³ | No | No |
| Invitation response | Respond (accept / decline) | No | No | No | Restricted ¹⁰ | Restricted ¹⁰ | Restricted ¹⁰ | Restricted ¹¹ |
| Attendance | Record | No | No | No | No | Restricted ³ (InProgress only) | No | No |
| Personal document library | View | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Personal document library | Upload | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Meeting document | View list | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹³ |
| Meeting document | Add document / Add new version | No | Restricted ³ | No | No | Restricted ³ | No | No |
| Meeting document | Preview / View version history | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Meeting document | Comment | No | Restricted ³ | No | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Audio recording | View media library / Play | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Audio recording | Upload | No | No | No | No | Restricted ³ | No | No |
| Transcript | View / Edit (speaker attribution) | No | No | No | No | Restricted ³ | No | No |
| Summary | Generate / Regenerate | No | No | No | No | Restricted ³ | No | No |
| Summary | View / Edit | No | No | No | No | Restricted ³ | No | No |
| Summary | Approve / Publish | No | No | No | No | Restricted ³ | No | No |
| Vote | Create / Configure | No | No | No | Restricted ³ | No | No | No |
| Vote | Open / Close | No | No | No | Restricted ³ | No | No | No |
| Vote | Cast (in-meeting) | No | No | No | Restricted ¹⁴ | Restricted ¹⁴ | Restricted ¹⁴ | No |
| Vote | View votes / status | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Vote | Record vote conclusion | No | No | No | Restricted ³ | No | No | No |
| Vote | View vote conclusion | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
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

FE-06 permission note: meeting-role default permissions may be supplemented only by the two permitted viewing permissions for one Meeting. Both Meeting status and current participation remain mandatory. These grants do not change system roles or permit Secretary/Chair-only actions. (BR-173–BR-177)

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

¹³ Only documents of the invited meeting that are explicitly shared with Guests, within a verified Guest session.

¹⁴ Only the current assigned Chair, Secretary and Members, and only while their Meeting is InProgress and its Vote is Open (BR-160, BR-165). Guest is excluded.

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
| 1.3 | Guest Verification | FE-01 Authentication & Account Security | Allows an external Guest without an internal account to verify the invited email with a 6-digit one-time code before responding to the invitation, viewing documents shared with guests or joining the room. |
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
| 6.2 | Add Participant Dialog | FE-06 Meeting Participant Management | Allows the Secretary to add internal participants together with a shared Meeting Role and optional permissions, or invite one Guest; the Organizer may assign the first Secretary. |
| 6.3 | Attendance Check | FE-06 Meeting Participant Management | Allows the Secretary to record Present or Absent for every current internal participant and Guest and save the complete list during an InProgress Meeting. |
| 6.4 | Invitation Response | FE-06 Meeting Participant Management | Allows an invited Chair, Secretary, Participant or verified Guest to view the invitation and accept or decline it, and to change the response until the meeting ends. |
| 7.1 | Document Library – Current Build | FE-07 Personal Document Library & Meeting Document Management | Shows the Document Library page as currently built: a page header and an empty-state panel explaining that the personal library is not active yet until the backend document API is available. |
| 7.2 | Personal Document Library | FE-07 Personal Document Library & Meeting Document Management | Allows an internal user to browse, search and filter his/her private documents by folder and file type, create folders, upload files and see where each document is used in meetings. |
| 7.3 | Meeting Detail – Documents Tab | FE-07 Personal Document Library & Meeting Document Management | Allows users related to a meeting to view its documents, filtered by agenda item, with version, uploader and comment count, and the Meeting Organizer or Secretary to add documents. |
| 7.4 | Add Document to Meeting Dialog | FE-07 Personal Document Library & Meeting Document Management | Allows the Meeting Organizer or Secretary to attach documents to a meeting from the computer or the personal library, link them to an agenda item and choose whether Guests may view them. |
| 7.5 | Document Viewer – Preview, Comments & Versions | FE-07 Personal Document Library & Meeting Document Management | Allows users related to a meeting to preview a meeting document page by page, read and add comments and view its version history; the Meeting Organizer or Secretary uploads a new version. |
| 8.1 | Meeting Lobby | FE-08 Online Meeting Room Management | Allows the Chair, Secretary, Participant or a verified Guest to preview camera and microphone, choose audio and video devices and enter the online room of an Online or Hybrid meeting. |
| 8.2 | Online Meeting Room – Video Grid | FE-08 Online Meeting Room Management | Allows users connected to the online room to see participants in a video grid, see who is speaking, turn their own microphone and camera on or off, raise a hand and leave the room. |
| 8.3 | In-Room Participants & Moderation Panel | FE-08 Online Meeting Room Management | Allows users in the online room to view who is connected and their room presence, and allows the Chair to manage the raise-hand queue, invite speakers, mute participants and remove users from the room. |
| 8.4 | Screen Sharing & Chat | FE-08 Online Meeting Room Management | Allows users connected to the online room to share their screen on the main stage, follow a shared screen and exchange text chat messages with everyone currently in the room. |
| 8.5 | In-Room Meeting Panel | FE-08 Online Meeting Room Management | Allows users in the online room to follow the meeting agenda, open meeting documents and, for eligible Chair, Secretary and Participants, cast their vote on an open Vote without leaving the room. |
| 9.1 | Meeting Detail – Recordings Tab | FE-09 Meeting Audio Upload & Media Library | Allows the Secretary to upload the meeting audio after the meeting has ended, see processing status and uploaded files, review the speaker-attributed transcript and start generating the AI summary. |
| 9.2 | Upload Audio Dialog | FE-09 Meeting Audio Upload & Media Library | Allows the Secretary to select or drag an MP3, M4A or WAV audio file of up to 500 MB, confirm recording consent and upload it to an Ended meeting for transcription. |
| 10.1 | Speaker-Attributed Transcript | FE-10 Speaker-Attributed Transcription & Meeting Summary | Allows the Secretary to play the uploaded meeting audio, search and correct the transcript text, and assign unknown voices to meeting attendees before generating the meeting summary. |
| 10.2 | Meeting Summary | FE-10 Speaker-Attributed Transcription & Meeting Summary | Allows the Secretary to review and edit the AI-generated draft summary, regenerate it, approve it and then publish the approved summary to attendees as a separate step. |
| 11.1 | Meeting Detail – Voting Tab | FE-11 Voting Management | Allows the Chair to view all Votes of an InProgress Meeting with their status and turnout, create, edit and open draft Votes, and close an open Vote; other involved users view read-only. |
| 11.2 | Create & Open Vote Dialog | FE-11 Voting Management | Allows the Chair to define a Vote for the current Agenda Item of an InProgress Meeting – content, options, ballot type and eligible voters – and save it as a draft or open it immediately. |
| 11.3 | Cast Vote | FE-11 Voting Management | Allows an eligible Chair, Secretary or Participant to view an open Vote of the InProgress Meeting, choose one option and submit a single ballot that cannot be changed afterwards. |
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
| 1 | Process notifications | Scheduled worker | Mỗi 5 giây, kiểm tra nhắc lịch đến hạn và gửi thông báo mới đến người dùng đang kết nối. |
| 2 | Deliver meeting invitation emails | Queue worker | Khi SMTP được bật, xử lý hàng đợi email mời họp và thử gửi lại email lỗi theo chính sách retry. |

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
| Description | As an invited Guest, I want to verify my meeting invitation so that I can access only the meeting for which I was invited. |  |  |
| Preconditions | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked or expired. |  |  |
| Postconditions | ● POST-1: A meeting-scoped Guest session is established after successful verification.<br>● POST-2: The Guest can continue to permitted invitation, document, and room actions for that meeting. |  |  |
| Normal Flow | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system identifies the invitation and requests the one-time verification code sent to the invited email address.<br>3. The Guest enters the code.<br>4. The system validates the code, invitation status, and meeting scope.<br>5. The system establishes a Guest session and displays the invited meeting. |  |  |
| Alternative Flows | ALT-1: If the Guest reopens a valid link during an active verified session, the system opens the same invited meeting without requesting another code. |  |  |
| Exception Flows | EX-1: The invitation is missing, expired, or revoked: the system denies access and explains that the invitation is no longer valid.<br>EX-2: The code is incorrect, expired, or already used: the system denies verification and allows the Guest to request or enter a valid code.<br>EX-3: The verification service is unavailable: the system reports that verification could not be completed and does not establish a session. |  |  |

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
| Postconditions | ● POST-1: The selected user is associated with the selected system role.<br>● POST-2: The user receives the capabilities associated with the selected system role.<br>● POST-3: The role assignment or change is recorded in the audit log. |  |  |
| Normal Flow | Assign System Role to User<br>1. The System Admin opens User Management.<br>2. The System Admin selects a user account.<br>3. The system displays the user information and the current system role, if any.<br>4. The System Admin selects Assign System Role.<br>5. The system displays the available system roles.<br>6. The System Admin selects a role and confirms the assignment.<br>7. The system assigns the selected role to the user.<br>8. The system records the role assignment in the audit log.<br>9. The system displays MSG10 – “User account updated successfully.” |  |  |
| Alternative Flows | AF-1: Change Existing System Role<br>1. At Step 4 of the Main Flow, if the user already has a system role, the System Admin selects Change System Role.<br>2. The System Admin selects a different role.<br>3. The System Admin confirms the change.<br>4. The system replaces the current role with the selected role.<br>5. The system records the role change in the audit log.<br>6. The system displays MSG10 – “User account updated successfully.” |  |  |
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
| Postconditions | ● POST-1: The selected role is associated with the updated set of default permissions.<br>● POST-2: The permission configuration change is recorded in the audit log. |  |  |
| Normal Flow | Configure Role Permissions<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the Role & Permission Matrix.<br>3. The System Admin configures the permissions assigned to a predefined role.<br>4. The system validates and saves the updated permission configuration (BR-23).<br>5. The system records the configuration change in the audit log (BR-16).<br>6. The system displays MSG16 – “Role permissions updated successfully.” |  |  |
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
| Postconditions | ● POST-1: The Meeting Name, planned time range, actual times where available, mode, location, confidentiality, description and lifecycle status are displayed read-only.<br>● POST-2: The Meeting is not modified. |  |  |
| Normal Flow | View Meeting Details<br>1. The user requests the selected Meeting.<br>2. The system verifies that the Meeting exists and the user has access. (BR-29)<br>3. The system retrieves the Meeting information and planned/actual time fields.<br>4. The system displays Meeting Name, planned start/end time, actual start/end time when available, Meeting Mode, Location, Confidentiality Level, Description, and Status.<br>5. The user reviews the Meeting details. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting Does Not Exist or Is Not Accessible<br>1. At Step 2, the system cannot find the selected meeting or the user is not authorized to view it.<br>2. The system does not disclose whether the meeting exists.<br>3. The use case ends.<br>EX-2 – Meeting Details Cannot Be Retrieved<br>1. At Step 3, the meeting exists and is accessible, but the system cannot retrieve its meeting-level information.<br>2. The system displays MSG37 – "Unable to load meeting details. Please try again."<br>3. The use case ends. |  |  |

#### 4.4 UC-17 – View Meeting List

| Primary Actors | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment. The assigned Secretary can view the Meeting before publication under BR-118. External Guests do not use the internal Meeting list. |  |  |
| Preconditions | ● PRE-1: The user is authenticated. |  |  |
| Postconditions | ● POST-1: The system displays the list of meetings that the user is authorized to view.<br>● POST-2: No meeting information is modified. |  |  |
| Normal Flow | View Meeting List<br>1. The user selects Meetings from the navigation menu.<br>2. The system identifies only meetings the user may view under BR-29: meetings created by the user at any status; meetings assigned to the user as Meeting Approver from WaitingApproval onward; and meetings assigned to the user as an active Secretary at any status, including before publication (BR-118); and meetings where the user is assigned as Chair or Member from Published onward. External Guests access only their invited Meeting through UC-04 and do not use this internal list.<br>3. The system sorts and displays only those authorized meetings.<br>4. The user browses the displayed meetings. |  |  |
| Alternative Flows | AF-1 – Search Meetings<br>1. At Step 4 of the Main Flow, the user enters a meeting name in the search field.<br>2. The system finds meetings matching the entered search criteria within the meetings available to the user.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – Filter Meetings<br>1. At Step 4 of the Main Flow, the user selects one or more filter criteria, such as Meeting Status, Confidentiality Level, or Time Range.<br>2. The system applies the selected criteria to the meetings available to the user. For Time Range, a Meeting matches when its own planned start/end time overlaps the selected time range. (BR-30)<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Main Flow.<br>AF-3 – No Matching Meetings<br>1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.<br>2. The system displays an empty meeting list and the message "No meetings match the filter."<br>3. The user may change or clear the current search/filter criteria.<br>4. The flow resumes at Step 4 of the Main Flow. |  |  |
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
| Postconditions | ● POST-1: The Meeting Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history. |  |  |
| Normal Flow | Review Meeting Approval<br>1. The Meeting Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Meeting Approver.<br>3. The system displays the meeting information for review.<br>4. The Meeting Approver selects Approve.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to approve the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status from WaitingApproval to Approved.<br>7. The system records the approval decision, acting user, and timestamp in the meeting lifecycle history. The decision, status change and history must all be completed together; otherwise EX-3 applies.<br>8. The system displays MSG43 – "Meeting approved successfully." |  |  |
| Alternative Flows | AF-1 – Reject Meeting<br>1. At Step 4 of the Main Flow, the Meeting Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Meeting Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters.<br>5. The system re-verifies that:<br>- the Meeting Approver is still authorized to reject the selected meeting; and<br>- the meeting is still in WaitingApproval status.<br>6. The system changes the meeting status to Rejected.<br>7. The system records the rejection decision, rejection reason, acting user, and timestamp. The decision, reason, status change and history must all be completed together; otherwise EX-3 applies.<br>8. The rejection reason becomes available to the Meeting Organizer and assigned Secretary so the Organizer can correct and resubmit the Meeting.<br>9. The system displays MSG44 – "Meeting rejected successfully." |  |  |
| Exception Flows | EX-1 – Rejection Reason Is Missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-36)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting Is No Longer Awaiting Approval<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status.<br>2. No approval or rejection decision is applied.<br>3. The meeting remains in its current status.<br>4. The system displays "This meeting is no longer awaiting approval."<br>5. The use case ends.<br>EX-3 – Approval Decision Cannot Be Completed<br>1. At Steps 6–7 of the Main Flow or AF-1, the system cannot complete the approval or rejection decision, resulting status or decision history.<br>2. The Meeting status, approval assignment, previous decision information and history remain unchanged.<br>3. No new approval or rejection decision is recorded, no notification or invitation is issued for the unsuccessful action, and the action is not reported as successful.<br>4. The system displays "Unable to complete the approval decision. Please try again."<br>5. The use case ends.<br>EX-4 – User Is No Longer Authorized to Review the Meeting<br>1. At Step 5 of the Main Flow or Step 5 of AF-1, the system determines that the Meeting Approver is no longer authorized to approve or reject the selected meeting. (BR-35)<br>2. No approval or rejection decision is applied.<br>3. The meeting status remains unchanged.<br>4. The system informs the Meeting Approver that they are no longer authorized to review the meeting.<br>5. The use case ends.<br>EX-5 – Rejection Reason Is Too Long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Meeting Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |  |  |

#### 4.7 UC-20 – Publish Meeting

| Primary Actors | Meeting Organizer | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Meeting Organizer to publish an Approved meeting and initiate invitation delivery to current internal participants and external Guests. |  |  |
| Preconditions | ● PRE-1: The Meeting Organizer is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Meeting Organizer is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-124, BR-125, BR-129) |  |  |
| Postconditions | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history.<br>● POST-3: Current internal participants are notified in the application and are sent invitation emails. Each external Guest with a current invitation is sent a separate email invitation to verify their invited email address and access only this Meeting. Unsuccessful delivery is retried without changing Published status. Successful publication does not mean every invitation has already been received. (BR-39, BR-40, BR-152, BR-153) |  |  |
| Normal Flow | Publish Meeting<br>1. The Meeting Organizer selects an Approved meeting.<br>2. The Meeting Organizer selects Publish Meeting.<br>3. The system verifies that the Meeting Organizer remains authorized, the meeting is still Approved with a valid planned schedule, exactly one current Chair has an active account, and at least one current Secretary has an active account.  (BR-37, BR-124, BR-125, BR-129)<br>4. The system changes the meeting status from Approved to Published. (BR-37)<br>5. The system records the publication action, acting user, and timestamp in the meeting lifecycle history. The Published status and publication history must be completed together before invitations are issued; otherwise EX-2 applies. (BR-38)<br>6. The system notifies current internal participants in the application and sends them invitation emails. Each external Guest with a current invitation receives a separate email invitation to verify their invited email address and access only this Meeting. Sending invitations does not delay confirmation of publication. If delivery fails, the system retries delivery and the Meeting remains Published. (BR-39, BR-40, BR-152, BR-153, BR-156)<br>7. The system displays MSG45 – "Meeting published successfully." |  |  |
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
| Description | Allows the assigned Chair to end an InProgress Meeting after all Votes are closed and every current participant and Guest has official attendance recorded. |  |  |
| Preconditions | ● PRE-1: The actor is the current authorized Chair.<br>● PRE-2: The Meeting is InProgress. (BR-130, BR-132) |  |  |
| Postconditions | ● POST-1: The Meeting becomes Ended and its Actual End Time is recorded.<br>● POST-2: Any InProgress Agenda Item is completed; remaining Pending Items stay Pending. (BR-159)<br>● POST-3: Online Meeting Room access is closed under existing lifecycle rules. (BR-143) |  |  |
| Normal Flow | End Meeting<br>1. The Chair selects End Meeting.<br>2. The system checks the Chair’s authority and Meeting status.<br>3. The system confirms no Vote is Open and every current participant and Guest has official attendance recorded. (BR-159, BR-179)<br>4. The system completes the Meeting and any InProgress Agenda Item, recording required end information.<br>5. The system closes Meeting Room access and confirms the Meeting has ended. (MSG111) |  |  |
| Alternative Flows | None. |  |  |
| Exception Flows | EX-1 – Meeting cannot be ended: the actor or Meeting does not meet the required conditions. No end is recorded. (MSG114)<br>EX-2 – Open Vote remains: the Meeting is not ended until all Open Votes are closed. (BR-159, MSG139)<br>EX-3 – Attendance is incomplete: at least one current participant or Guest has no official attendance value. The Meeting remains InProgress; the Chair is asked to have attendance completed. (BR-179, MSG155)<br>EX-4 – End cannot be completed: the previous Meeting and Agenda states are retained. (MSG116) |  |  |

### 5. FE-05 – Agenda Management

#### 5.1 UC-23 – Manage Agenda Items

##### 5.1.1 UC-23a – Create Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to create a new Agenda Item while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including during the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-65, BR-74) |  |  |
| Postconditions | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting and any selected documents belong to the same Meeting. |  |  |
| Normal Flow | Create Agenda Item<br>1. The Secretary opens the Meeting Agenda and selects Add Agenda Item.<br>2. The Secretary enters Title, Planned Duration, optional Presenter and Description, and selects available Meeting documents.<br>3. The system verifies the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and document access. (BR-64, BR-65, BR-66, BR-74)<br>4. The system creates the Item as Pending after the last position in the Meeting agenda. Existing Item positions are unchanged. (BR-67)<br>5. The system displays MSG60 – "Agenda item created successfully." and automatically refreshes the complete Agenda for authorized users currently viewing it. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid Agenda Item Information<br>1. At Step 3, required values or selected documents are invalid.<br>2. No Item is created. The system displays MSG66 – "Invalid agenda item information." with the applicable field messages.<br>EX-2 – Agenda Planning No Longer Permitted<br>1. At Step 3, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. No Item is created. The system displays MSG67 – "The agenda for this meeting can no longer be modified."<br>EX-3 – Create Cannot Be Saved<br>1. At Step 4, the system cannot save the Item or its document associations.<br>2. The saved Agenda and its order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG68 – "Unable to create the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

##### 5.1.2 UC-23b – Update Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to update a Pending Agenda Item, or update only Title and Description after the Item has started. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>● PRE-3: A Pending Item permits changes to its planning fields; an InProgress or Done Item permits only Title and Description changes. (BR-68) |  |  |
| Postconditions | ● POST-1: The permitted Item information is updated.<br>● POST-2: Meeting membership, Item execution status, actual timestamps and Meeting lifecycle remain unchanged.<br>● POST-3: Authorized users currently viewing the Agenda automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Update Agenda Item<br>1. The Secretary selects an Item and chooses Edit.<br>2. For Pending, the system permits changes to Title, Planned Duration, optional Presenter, Description and document associations. For InProgress or Done, only Title and Description are editable. (BR-68)<br>3. The Secretary enters permitted changes and submits.<br>4. The system rechecks the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), current Item status, permitted fields, required values and any selected Meeting documents. (BR-65, BR-66, BR-68, BR-74)<br>5. The system saves the permitted changes together, preserving execution status and actual timestamps.<br>6. The system displays MSG61 – "Agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid Agenda Item Information<br>1. At Step 4, the submitted values or documents are invalid.<br>2. The Item remains unchanged. The system displays MSG66 with the applicable field messages.<br>EX-2 – Editing No Longer Permitted<br>1. At Step 4, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The Item remains unchanged. The system displays MSG67.<br>EX-3 – Update Cannot Be Saved<br>1. At Step 5, the system cannot save the permitted changes.<br>2. Item information, document associations, execution status, actual timestamps and order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG69 – "Unable to update the agenda item. Please try again." The use case ends unsuccessfully.<br>EX-4 – Changes Not Permitted for the Current Item Status<br>1. At Step 4, the Item is InProgress or Done and the request changes a field other than Title or Description, including when the Item started after the edit form was opened.<br>2. No changes are saved. The system displays MSG141 – "Only Title and Description can be changed after an agenda item has started."<br>3. The system refreshes the permitted fields. The Secretary may revise the request and continue from Step 3. |  |  |

##### 5.1.3 UC-23c – Delete Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to delete only a Pending Agenda Item from the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to a Meeting in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-65, BR-68)<br>● PRE-3: The Item is Pending. InProgress and Done Items cannot be deleted. (BR-68) |  |  |
| Postconditions | ● POST-1: The selected Pending Item is removed.<br>● POST-2: Remaining Item positions and execution data, and the Meeting lifecycle, are unchanged.<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Delete Agenda Item<br>1. The Secretary selects a Pending Item, chooses Delete and confirms.<br>2. The system verifies the active Secretary assignment, permitted Meeting status, Item membership and that the Item is still Pending. (BR-64, BR-65, BR-68)<br>3. The system removes the Item while retaining every remaining Item position.<br>4. The system displays MSG62 – "Agenda item deleted successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Planning No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. Nothing is deleted. The system displays MSG67.<br>EX-2 – Agenda Item Has Already Started<br>1. At Step 2, the Item is InProgress or Done.<br>2. The Item and Agenda order remain unchanged. The system displays MSG142 – "Only Pending agenda items can be deleted."<br>EX-3 – Deletion Cannot Be Saved<br>1. At Step 3, the system cannot complete deletion.<br>2. The Item and the saved Agenda order remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG70 – "Unable to delete the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

##### 5.1.4 UC-23d – Reorder Agenda Items

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to exchange Pending Agenda Item positions within the same Meeting, keeping InProgress and Done positions unchanged. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains Pending Items to reorder. (BR-65, BR-68) |  |  |
| Postconditions | ● POST-1: Pending Items occupy the same set of positions in the requested order.<br>● POST-2: InProgress and Done positions, Item membership and execution data remain unchanged.<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Reorder Agenda Items<br>1. The Secretary changes the relative order of Pending Items. Positions occupied by InProgress or Done Items are fixed.<br>2. The system verifies the active Secretary assignment, permitted Meeting status and the latest saved Agenda. (BR-65, BR-74)<br>3. The system verifies that each current Pending Item appears exactly once, all Items belong to this Meeting and only the existing Pending positions are exchanged. InProgress and Done positions must remain unchanged. (BR-64, BR-67, BR-68)<br>4. The system saves the new Pending order together.<br>5. The system displays MSG63 – "Agenda order updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Reordering No Longer Permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary.<br>2. The saved order remains unchanged. The system displays MSG67.<br>EX-2 – Invalid or Outdated Agenda Order<br>1. At Step 3, the request duplicates or omits a current Pending Item, includes an Item from another Meeting, changes an InProgress or Done position, or no longer matches the current Pending Items and positions.<br>2. No order changes are saved. The system displays MSG143 – "Only Pending agenda items can be reordered within their existing positions. Refresh the agenda and try again."<br>3. The system refreshes the complete Agenda. The Secretary may revise the order and continue from Step 1.<br>EX-3 – Order Cannot Be Saved<br>1. At Step 4, the system cannot save the requested order.<br>2. All Item positions remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG71 – "Unable to update the agenda order. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.2 UC-24 – Set Current Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to start the first Pending Agenda Item in the current Meeting order. |  |  |
| Preconditions | ● PRE-1: The Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Item is the first Pending Item in its current order. (BR-69, BR-70)<br>● PRE-3: No other Item of the Meeting is InProgress. (BR-71) |  |  |
| Postconditions | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |  |  |
| Normal Flow | Set Current Agenda Item<br>1. The Chair selects the first Pending Item in the current Meeting order and chooses Set as Current.<br>2. The system rechecks the active Chair assignment, Meeting InProgress status and that the selected Item belongs to the Meeting and remains Pending. (BR-64, BR-69)<br>3. The system verifies that no other Item is InProgress and that the selected Item is still the first Pending Item in the latest saved order. (BR-70, BR-71)<br>4. The system changes the Item to InProgress and records its actual start time together. If competing requests arrive, only one Item may start. (BR-70, BR-71, BR-72)<br>5. The system displays MSG64 – "Current agenda item updated successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Item Cannot Become Current<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the selected Item does not belong to the Meeting or is not Pending.<br>2. The Agenda remains unchanged. The system displays MSG73 – "This agenda item cannot be set as current."<br>EX-2 – Another Agenda Item Is InProgress<br>1. At Step 3 or Step 4, another Item is already InProgress.<br>2. The selected Item remains Pending. The system displays MSG72 – "Another agenda item is already in progress. Complete it before starting another agenda item."<br>EX-3 – Selected Item Is Not the First Pending Item<br>1. At Step 3 or Step 4, a different Item is first in the current Pending order.<br>2. The Agenda remains unchanged. The system displays MSG144 – "Start the first Pending agenda item in the current order." and refreshes the Agenda.<br>EX-4 – Current Agenda Item Cannot Be Saved<br>1. At Step 4, the system cannot save the Item status or actual start time.<br>2. The selected Item remains Pending; its timestamps and all other Agenda data remain unchanged. No successful change is announced to viewers.<br>3. The system displays MSG75 – "Unable to set the current agenda item. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.3 UC-25 – Complete Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to complete the current Item regardless of how its actual duration compares with its planned duration. |  |  |
| Preconditions | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |  |  |
| Postconditions | ● POST-1: The current Item becomes Done with its actual end time and actual duration.<br>● POST-2: The Meeting remains InProgress. No next Item starts automatically. (BR-72)<br>● POST-3: Authorized viewers automatically see the complete updated Agenda. (BR-73) |  |  |
| Normal Flow | Complete Agenda Item<br>1. The Chair selects Complete for the current Item.<br>2. The system verifies the active Chair assignment, Meeting InProgress status, Item membership and current InProgress status. Planned Duration does not restrict completion. (BR-64, BR-69, BR-70, BR-72)<br>3. The system records actual end time and elapsed duration and changes the Item to Done together. (BR-70, BR-72)<br>4. The Meeting stays InProgress with no current Item. The Chair must explicitly start the first remaining Pending Item through UC-24.<br>5. The system displays MSG65 – "Agenda item completed successfully." and automatically refreshes the complete Agenda for authorized viewers. (BR-73) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda Item Cannot Be Completed<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the Item does not belong to the Meeting or is not its current InProgress Item.<br>2. Nothing changes. The system displays MSG74 – "This agenda item cannot be completed."<br>EX-2 – Agenda Item Completion Cannot Be Saved<br>1. At Step 3, the system cannot save completion, actual end time or actual duration.<br>2. The Item remains InProgress with unchanged timestamps. The Meeting remains InProgress. No successful change is announced to viewers.<br>3. The system displays MSG76 – "Unable to complete the agenda item. Please try again." The use case ends unsuccessfully. |  |  |

#### 5.4 UC-26 – View Meeting Agenda

| Primary Actors | Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view the complete Meeting Agenda and automatically see saved changes while the Agenda remains open. |  |  |
| Preconditions | ● PRE-1: The user may view the selected Meeting. (BR-29) |  |  |
| Postconditions | ● POST-1: Agenda title, presenter, planned duration, order, execution status and recorded actual times are shown read-only.<br>● POST-2: The complete Agenda automatically reflects successful creation, editing, deletion, reordering, Item start/completion and completion of the current Item when the Meeting ends. (BR-73)<br>● POST-3: Viewing does not modify the Agenda. |  |  |
| Normal Flow | View Meeting Agenda<br>1. The user opens the Meeting Agenda tab.<br>2. The system verifies Meeting access and retrieves the complete saved Agenda in its current order. (BR-29, BR-64)<br>3. The system shows Item title, presenter, planned duration, status, order and recorded actual times.<br>4. While the user continues viewing, the system automatically refreshes the complete Agenda after a successful Agenda change, including changes made by another authorized user. Manual page refresh is not required. (BR-73)<br>5. The system rechecks access before displaying refreshed Agenda data. Failed changes are not shown as successful updates. |  |  |
| Alternative Flows | AF-1 – Empty Meeting Agenda<br>1. At Step 3 or Step 4, no Agenda Items exist.<br>2. The system displays the empty Agenda state.<br>3. While the view remains open, it automatically reflects subsequent saved Agenda changes. The use case succeeds. |  |  |
| Exception Flows | EX-1 – Meeting Not Accessible<br>1. At Step 2 or Step 5, the Meeting does not exist or the user lacks access.<br>2. No further Agenda information is disclosed. The use case ends.<br>EX-2 – Meeting Agenda Cannot Be Retrieved<br>1. At Step 2 or Step 4, the system cannot retrieve the latest Agenda.<br>2. The system displays MSG128 – "Unable to load the meeting agenda. Please try again." A retrieval failure is not displayed as an empty Agenda.<br>3. No Agenda data changes; previously displayed information is not presented as the latest saved Agenda. The user may retry. |  |  |

### 6. FE-06 – Meeting Participant Management

#### 6.1 UC-27 – View Meeting Participants

| Primary Actors | Secretary, Chair, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized internal participant to view and search the current Meeting roster, including internal participants and Guests. |  |  |
| Preconditions | ● PRE-1: The actor has an eligible current Meeting assignment.<br>● PRE-2: Secretary access follows existing prepublication rules; from Published onward, the Chair may view by default and a Member needs View Meeting Participants permission. (BR-117–BR-121, BR-176) |  |  |
| Postconditions | ● POST-1: The authorized current roster is displayed.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Meeting Participants<br>1. The actor opens the selected Meeting’s Participants view.<br>2. The system confirms that the actor may view the roster. (BR-176)<br>3. The system displays current internal participants and Guests with identifying information and Meeting Role or Guest access; removed or revoked entries are excluded. (BR-103)<br>4. The actor reviews the roster. |  |  |
| Alternative Flows | AF-1 – Search participants: the actor searches by name or email and reviews matching current entries. |  |  |
| Exception Flows | EX-1 – Roster is not accessible: the system does not disclose the roster. (BR-121)<br>EX-2 – Roster cannot be retrieved: the system reports that the roster could not be loaded. (MSG95) |  |  |

#### 6.2 UC-28 – Manage Meeting Participants

##### 6.2.1 UC-28a – Add Meeting Participant

| Primary Actors | Meeting Organizer, Secretary | Secondary Actors | Email / SMTP Service |
| --- | --- | --- | --- |
| Description | Allows the Organizer to assign the first Secretary, or the assigned Secretary to add internal participants together or invite one external Guest. |  |  |
| Preconditions | ● PRE-1: The actor is the eligible Organizer or a current Secretary.<br>● PRE-2: The Meeting permits participant management. (BR-78–BR-80) |  |  |
| Postconditions | ● POST-1: The selected internal participants are all added, or one Guest invitation is created.<br>● POST-2: Invitation Response starts as Pending.<br>● POST-3: Meeting Role and eligible additional permissions apply to this Meeting only; System Roles are unchanged. |  |  |
| Normal Flow | Add Internal Participants<br>1. The actor selects Add Participants for the Meeting.<br>2. The actor selects internal users, one shared Meeting Role and any permitted shared additional permissions. For the first Secretary, the Organizer selects exactly one Secretary without extras.<br>3. The actor confirms the selection.<br>4. The system checks the actor, Meeting, selected users, role and permissions against applicable rules. (BR-78–BR-85, BR-168, BR-174)<br>5. The system adds all selected eligible users together or adds none, with Pending responses. (BR-168)<br>6. For a Published or InProgress Meeting, invitations are arranged under existing notification rules; the system confirms the addition. (BR-90, BR-94, MSG96) |  |  |
| Alternative Flows | AF-1 – Invite a Guest: the Secretary chooses Invite Guest and supplies one Guest’s name and email. The system creates one eligible Guest invitation with Pending response; Guest verification remains in UC-04. (BR-84, BR-148–BR-156) |  |  |
| Exception Flows | EX-1 – Selection is not allowed: no user in an internal batch is added, or no invalid Guest invitation is created. (MSG97 or MSG152)<br>EX-2 – Participant management is not allowed: no addition occurs. (BR-78–BR-80, MSG98)<br>EX-3 – Addition cannot be completed: no partial internal batch is added. (MSG99) |  |  |

##### 6.2.2 UC-28b – Manage Meeting Role & Additional Permissions

| Primary Actor | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to change a participant’s Meeting Role or eligible additional permissions, including replacing the current Chair. |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting permits participant management and the selected person is a current internal participant. (BR-79, BR-80) |  |  |
| Postconditions | ● POST-1: Approved role or additional-permission changes are saved for this Meeting.<br>● POST-2: Chair replacement changes both affected Meeting Roles together. System Role and Invitation Response remain unchanged. |  |  |
| Normal Flow | Change Role or Permissions<br>1. The Secretary selects a current internal participant.<br>2. The system displays the participant’s current Meeting Role and additional permissions.<br>3. The Secretary selects the desired role or permitted additional permissions and confirms.<br>4. The system checks the selection against Meeting role and permission rules. (BR-122, BR-123, BR-174, BR-177)<br>5. The system saves the approved changes and confirms the update. (MSG100 or MSG153) |  |  |
| Alternative Flows | AF-1 – Replace Chair: the Secretary selects a current internal participant as the new Chair and confirms. The former Chair becomes a Member and the selected participant becomes Chair together. (BR-169, MSG100) |  |  |
| Exception Flows | EX-1 – Role or permissions are not allowed: no changes are saved; the Secretary may revise the selection. (MSG101, MSG109 or MSG152)<br>EX-2 – Meeting or actor is no longer eligible: no changes are applied. (MSG98)<br>EX-3 – Update cannot be completed: existing roles and permissions remain unchanged. (MSG102) |  |  |

##### 6.2.3 UC-28c – Remove Meeting Participant

| Primary Actor | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an assigned Secretary to remove a current internal participant or revoke a Guest invitation while preserving required Meeting records. |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting permits participant management. (BR-79, BR-80) |  |  |
| Postconditions | ● POST-1: The selected participant is no longer current or the Guest invitation is revoked.<br>● POST-2: Access through the removed relationship ends.<br>● POST-3: Required historical invitation and attendance evidence is retained. (BR-97, BR-100–BR-103) |  |  |
| Normal Flow | Remove Participant<br>1. The Secretary selects a current participant or Guest invitation.<br>2. The Secretary confirms removal or revocation.<br>3. The system checks that the action is allowed and required Meeting roles remain assigned. (BR-122, BR-126)<br>4. The system removes the assignment or revokes Guest access while retaining required history.<br>5. The system confirms the removal. (MSG103) |  |  |
| Alternative Flows | None. |  |  |
| Exception Flows | EX-1 – Removal is not allowed: the actor, Meeting status or required roles prohibit the action. Nothing is removed. (MSG98 or MSG109)<br>EX-2 – Removal cannot be completed: the prior assignment or invitation remains effective. (MSG104) |  |  |

#### 6.3 UC-29 – Respond to Meeting Invitation

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an invitee to accept or decline their own Meeting invitation, or change an earlier response while responses are allowed. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published or InProgress.<br>● PRE-2: The actor owns a current invitation; a Guest has verified access through UC-04. (BR-104–BR-106, BR-148–BR-156) |  |  |
| Postconditions | ● POST-1: The latest response and response time are recorded.<br>● POST-2: Meeting Role, attendance and additional permissions are unchanged. |  |  |
| Normal Flow | Respond to Invitation<br>1. The invitee opens their Meeting invitation.<br>2. The system identifies the invitee and displays the current response.<br>3. The invitee selects Accepted or Declined.<br>4. The system checks that the invitation can still be answered and records the response.<br>5. The system confirms the update. (MSG105) |  |  |
| Alternative Flows | AF-1 – Change previous response: the invitee changes an earlier response while the Meeting still allows responses, then continues at Step 3. |  |  |
| Exception Flows | EX-1 – Invitation is not answerable: no change is made. Guest verification remains in UC-04. (MSG107)<br>EX-2 – Response is invalid or cannot be recorded: the prior response is retained. (MSG106 or MSG108) |  |  |

#### 6.4 UC-30 – View Participant Response Status

| Primary Actors | Secretary, Chair, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows authorized internal participants to view current invitation responses and totals for the Meeting. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published and remains readable under its lifecycle rules.<br>● PRE-2: A current Secretary or Chair may view by default; a Member needs View Invitation Responses permission for this Meeting. (BR-176) |  |  |
| Postconditions | ● POST-1: Latest responses for current invitees and totals by response status are displayed.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Participant Response Status<br>1. The actor selects Response Status for an accessible Meeting.<br>2. The system checks that the actor may view responses. (BR-176)<br>3. The system displays each current invitee’s name, email and latest response with totals by status. Response-view permission includes identity in response rows but does not grant roster browsing or search. (BR-103, BR-115, BR-176)<br>4. The actor reviews the summary. |  |  |
| Alternative Flows | None. An all-Pending result is a normal response summary. |  |  |
| Exception Flows | EX-1 – Responses are not accessible: the system does not disclose unauthorized response information. (BR-121)<br>EX-2 – Responses cannot be retrieved: the system reports that the data could not be loaded. (MSG95) |  |  |

#### 6.5 UC-31 – Record Attendance

| Primary Actor | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to record or revise official attendance for the full current roster while a Meeting is InProgress. |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary.<br>● PRE-2: The Meeting is InProgress. (BR-170) |  |  |
| Postconditions | ● POST-1: Each current internal participant and Guest has official attendance recorded as Present or Absent.<br>● POST-2: The complete list is saved together; invitation responses remain unchanged. (BR-116, BR-172) |  |  |
| Normal Flow | Record Attendance<br>1. The Secretary opens Attendance for an InProgress Meeting.<br>2. The system displays the current internal participants and Guests with their recorded values, if any.<br>3. The Secretary selects Present or Absent for every current person and submits the list.<br>4. The system checks that the list is complete and attendance can be recorded. (BR-170–BR-172)<br>5. The system saves the complete attendance list and confirms the result. (MSG147) |  |  |
| Alternative Flows | AF-1 – Revise attendance: the Secretary changes previously recorded values and continues at Step 3 while the Meeting remains InProgress. |  |  |
| Exception Flows | EX-1 – List is incomplete or invalid: no attendance is saved; the Secretary corrects the list. (MSG148)<br>EX-2 – Recording is no longer allowed: no changes are made. (MSG149)<br>EX-3 – Attendance cannot be saved: previously saved attendance remains unchanged. (MSG150) |  |  |

### 7. FE-07 – Personal Document Library & Meeting Document Management

#### 7.1 UC-32 – View Meeting Documents

##### 7.1.1 UC-32a – View Meeting Documents

Placeholder only – detailed Use Case Specification will be added later.

##### 7.1.2 UC-32b – View Document Version History

Placeholder only – detailed Use Case Specification will be added later.

##### 7.1.3 UC-32c – Preview Meeting Document

Placeholder only – detailed Use Case Specification will be added later.

#### 7.2 UC-33 – Manage Meeting Documents

##### 7.2.1 UC-33a – Add Document to Meeting

Placeholder only – detailed Use Case Specification will be added later.

##### 7.2.2 UC-33b – Add New Meeting Document Version

Placeholder only – detailed Use Case Specification will be added later.

#### 7.3 UC-34 – View Personal Document Library

Placeholder only – detailed Use Case Specification will be added later.

#### 7.4 UC-35 – Comment on Meeting Document

Placeholder only – detailed Use Case Specification will be added later.

#### 7.5 UC-36 – Upload Document to Personal Library

Placeholder only – detailed Use Case Specification will be added later.

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

Scope: Votes are created, opened, cast and closed only while a Meeting is InProgress. No time-boxed or pre/post-meeting voting, quorum or separate Resolution. The Chair records Passed, NotPassed or Deferred after closing the Vote.

#### 11.1 UC-52 – View Meeting Votes

##### 11.1.1 UC-52a – View Meeting Votes

Placeholder only – detailed Use Case Specification will be added later.

##### 11.1.2 UC-52b – View Vote Status

Placeholder only – detailed Use Case Specification will be added later.

#### 11.2 UC-53 – Create / Configure Vote

Placeholder only – detailed Use Case Specification will be added later.

#### 11.3 UC-54 – Open Vote

Placeholder only – detailed Use Case Specification will be added later.

#### 11.4 UC-55 – Cast Vote

Placeholder only – detailed Use Case Specification will be added later.

#### 11.5 UC-56 – Close Vote

Placeholder only – detailed Use Case Specification will be added later.

#### 11.6 UC-57 – Record Vote Conclusion

[Placeholder] Detailed Use Case Specification will be added later. Chair records Passed, NotPassed or Deferred after Vote closes.

Placeholder only – detailed Use Case Specification will be added later.

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

The Guest Verification screen is opened by an external Guest, who has no internal account, from the link in the meeting invitation email; it uses a single-column layout that also fits a phone. The Guest proves control of the invited email with a 6-digit one-time code before s/he can see the meeting, respond to the invitation, view the documents shared with guests or join the online room (UC-04).

This screen allows the Guest to:

- View Invitation: see that TrustCabinet has invited him/her and the title of the invited meeting (UC-04).

- Enter Verification Code: type the 6-digit code sent to the invited email address (UC-04).

- Verify: submit the code; on success a meeting-scoped Guest session is established and the Guest continues to the invited meeting and the Invitation Response screen (UC-04, UC-29).

- Resend Code: request a new code to the invited email when the code was not received or has expired (UC-04).

On the screen, s/he can also:

- Read Recording and Scope Notices: see that the meeting may be recorded for the minutes and that a Guest can only access this meeting and the documents shared with guests.

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
| Guest scope notice | Read-only text: “Guests can only view this meeting and the documents shared with guests.” [Khách mời chỉ xem được cuộc họp này và tài liệu được chia sẻ cho khách.] (BR-120). |
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
| Save draft [Lưu nháp] | Primary button. Validates the fields (BR-24, BR-62, BR-162, BR-163); invalid input shows MSG31 “Invalid meeting information.” and highlights the fields. Create: the Meeting is saved with status Draft and the creator recorded as Meeting Organizer (BR-26), toast MSG29 “Meeting created successfully.”, and the detail opens on the Participants tab to add the first Secretary; failure shows MSG35. Edit: allowed only in Draft or Rejected and does not change the status (BR-27, BR-28); toast MSG30 “Meeting updated successfully.”; MSG33 “This meeting can no longer be edited.” if the status has changed; failure MSG36. |

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
| Action buttons | Change with status and role: Draft/Rejected – Edit [Sửa thông tin] and Submit for approval [Gửi duyệt] (Meeting Organizer); Pending Approval – Reject [Từ chối] and Approve [Duyệt] (assigned Meeting Approver); Approved – Publish [Công bố] (Meeting Organizer); Published – Reschedule [Dời lịch] (Meeting Organizer) and Start meeting [Bắt đầu cuộc họp] (Chair); In Progress – End meeting [Kết thúc cuộc họp] (Chair), which asks for confirmation and is blocked while a Vote is open (MSG139). Buttons the user may not use are hidden. |
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

- End Meeting: end the Meeting when no Vote is Open, complete any current InProgress Item and leave remaining Pending Items Pending (UC-22).

On the screen, s/he can also:

- Join Online Room: click “Join online room” to enter the online room of an Online or Hybrid Meeting (UC-37).

- View Room Preview: see who is currently connected to the online room in the small video grid.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: title with status chip In Progress [Đang họp] and confidentiality chip; second line “Started 08:32 · 21 minutes elapsed” [Bắt đầu … · đã họp … phút] and “Present 4/5” [Có mặt 4/5] taken from attendance (6.3). |
| Join online room [Vào phòng trực tuyến] | Secondary button; shown for Online or Hybrid Meetings; opens the Meeting Lobby (8.1). |
| End meeting [Kết thúc cuộc họp] | Red outline button; Chair only (BR-130). Asks for confirmation because an ended Meeting cannot be reopened. Blocked with MSG139 “Close all open votes before ending the meeting.” while a Vote is Open; an item still in discussion is completed automatically (BR-159). Ends the Meeting, records Actual End Time and closes the online room (BR-132, BR-143); MSG111 success, MSG114 / MSG116 on failure. Remaining Pending Items stay Pending; they are not automatically completed. |
| Agenda list | One row per agenda item: order number, title, presenter, planned duration and a status/action cell. A Done item shows its actual time range (e.g. 08:32–08:44); the current item is highlighted and shows “In discussion since 08:44 · 9 minutes” [Đang thảo luận từ … · … phút]. |
| Item status | Done or InProgress status is shown read-only. Only the first Pending Item offers Set as Current to the active assigned Chair when no Item is InProgress. |
| Set as current [Đặt hiện tại] | Chair only, while Meeting is InProgress. Starts only the first Pending Item in the current order and records actual start time. MSG72 reports another current Item; MSG144 reports selecting a later Pending Item; MSG73 reports other ineligible conditions; MSG75 reports save failure; MSG64 confirms success. (BR-69, BR-70, BR-71) |
| Current item card [Mục hiện tại · 2] | Read-only: order number and title of the current item and its attached documents (e.g. “2 documents · De-xuat-bieu-quyet.docx, So-sanh-eCabinet.xlsx”); file names open the Document Viewer (7.5). |
| Create a vote for this item [Tạo biểu quyết cho mục này] | Secondary button in the current item card; Chair only, while the Meeting is InProgress (BR-160, BR-165); opens the Create & Open Vote Dialog (11.2) linked to the current item. |
| Complete item [Hoàn thành mục] | Chair only, for the current InProgress Item. Completion is allowed regardless of Planned Duration and records actual end time and elapsed duration. The Meeting stays InProgress and the next Item does not start automatically. MSG65 confirms success; MSG74 reports ineligible conditions; MSG76 reports save failure. (BR-72) |
| Online room preview | Read-only grid of tiles with the names of users connected to the online room; a tile such as “Room 2.1” [Phòng 2.1] shows the in-person room of a Hybrid Meeting, and “Waiting to join” [Chờ vào] marks an empty slot. |
| Real-time update | The complete Agenda automatically refreshes for authorized users viewing the Meeting after successful creation, editing, deletion, reordering, start, completion or completion of the current Item when the Meeting ends. Failed changes do not produce a successful Agenda update. (BR-73) |

### 6. FE-06 – Meeting Participant Management

#### 6.1 Meeting Detail – Participants Tab

The Meeting Detail – Participants tab shows current internal participants and external Guests. The assigned Secretary manages the roster, roles and additional permissions; the Organizer may assign the first Secretary. From Published onward, the Chair may view the roster and invitation responses by default, while a Member needs the relevant additional permission. A Member with View Invitation Responses can see invitee names and email in response rows without gaining roster browsing or search access. View-only access does not permit changes. (UC-27, UC-28a–UC-30, BR-173–BR-176)

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

- Add First Secretary: (Meeting Organizer, Meeting without a Secretary) click “Add Secretary” to add the first internal participant with the Secretary role (UC-28a).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Participants [Thành phần tham dự] is selected. Other tabs: General Information, Meeting Content, Documents, Voting, Recordings, Summary, Minutes. |
| Search [Tìm người tham dự] | Text input, optional, empty by default; filters the roster by full name or email. |
| Invite external guest [Mời khách ngoài] | Secondary button; opens the Add Participant Dialog on the External guests tab. Secretary only. |
| Add participants [Thêm thành phần] | Primary button; opens the Add Participant Dialog. Shown to the Secretary; when the Meeting has no Secretary it is replaced for the Meeting Organizer by “Add Secretary” [Thêm Thư ký] (BR-78, BR-79). Hidden while the Meeting is Pending Approval or Ended (BR-80, BR-118). |
| Participant [Người tham dự] | Column: avatar initials, full name and email; an external Guest shows “· external guest” [· khách ngoài] after the email. Guests have no internal account or system role (BR-148, BR-150). |
| Meeting role [Vai trò trong cuộc họp] | Internal participants have Chair [Chủ trì], Secretary [Thư ký] or Member [Thành viên]; Guests have fixed Guest [Khách] access. Edit Role & Permissions [Sửa vai trò và quyền] opens the role and additional-permission editor for the assigned Secretary. Replacing the Chair requires confirmation showing the current Chair becoming Member and the selected person becoming Chair; both changes are saved together. At least one Secretary must remain. Success MSG100; replacement failure MSG146; required-role restriction MSG109. (BR-85, BR-86, BR-122, BR-123, BR-169) |
| Row action | Secretary only: Edit Role & Permissions for internal participants, Remove for internal participants, and Revoke Invitation for Guests. View-only users see no management actions. Removal ends the assignment’s additional permissions and access, while independent valid access remains. Required-role restriction MSG109; removal success MSG103; failure MSG104; status restriction MSG98. (BR-80, BR-97, BR-126, BR-157, BR-177) |
| Response [Phản hồi] | Shown after publication to the assigned Secretary and current Chair by default, and to a Member with View Invitation Responses. Shows each current invitee’s name, email, latest response, response time and totals. This permission displays identity in response rows but does not grant roster browsing or search. Responses do not set attendance. (UC-30, BR-115, BR-116, BR-176) |
| Roster footer | Read-only: number of people and of external Guests, e.g. “5 people · 1 external guest” [5 người · 1 khách ngoài], and the note “Invitations are sent when the meeting is published” [Lời mời gửi khi công bố] (BR-90, BR-91). |
| Error states | A Meeting the user is not authorized for shows the not-found page without roster data (BR-121); load failure shows MSG95 “Unable to load meeting participants. Please try again.” |
| Additional permissions [Quyền bổ sung] | A Secretary may grant or remove View Meeting Participants and View Invitation Responses for current Members. The Chair has both views by default from Published onward; Secretary access follows existing rules. These permissions apply only to this Meeting. Guests have no such controls. View Invitation Responses includes names and email in response rows but does not grant roster browsing or search. (BR-173–BR-177) |

#### 6.2 Add Participant Dialog

The Add Participant Dialog opens from the Participants tab with “Add participants” (Internal users tab) or “Invite external guest” (External guests tab). The Secretary searches active internal users, ticks the people to add and chooses their meeting role, or invites an external Guest by name and email. When the Meeting has no Secretary yet, the Meeting Organizer uses the same dialog to add the first Secretary (UC-28a).

This screen allows the Secretary (or the Meeting Organizer when adding the first Secretary) to:

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
| User result list | Select one or more active internal accounts by checkbox. Current participants cannot be selected. All selected users are added together, or none are added if any selection is invalid. No organisational-unit invitation. For the first Secretary, select exactly one user. (BR-81–BR-83, BR-168) |
| Meeting role [Vai trò trong cuộc họp] | Required; Member [Thành viên] by default, Chair [Chủ trì] or Secretary [Thư ký]. One shared role applies to the entire selection. The first Secretary assignment is one user as Secretary, without additional permissions. A selection must not create a second Chair; use Edit Role & Permissions to replace the current Chair. (BR-78, BR-85, BR-123, BR-169, BR-175) |
| Guest name | External guests tab. Text input, required. |
| Guest email | External guests tab. Email input, required, valid format. One current invitation per email per Meeting and the email must not belong to a current internal participant (BR-149, BR-151). The Guest receives an email with a verification link once the Meeting is Published or InProgress (BR-91, BR-152). |
| Cancel [Huỷ] | Text button; closes the dialog. |
| Add <n> people [Thêm <n> người] | Shows the number selected and submits the entire selection. New invitations start Pending. All selected people are saved together; no one is added when validation or saving fails. Success MSG96 and the number added; invalid participant information MSG97/MSG145; invalid additional permissions MSG152; status restriction MSG98; save failure MSG99. Invitation delivery failure does not cancel successful additions. (BR-88, BR-90, BR-94, BR-168) |
| Additional permissions [Quyền bổ sung] | Optional checkboxes for Chairs and Members: View meeting participants [Xem thành phần tham dự] and View invitation responses [Xem phản hồi lời mời]. Apply to all selected users for this Meeting only. No additional permissions for the first Secretary or external Guests. Secretary role already provides these viewing permissions. (BR-173–BR-175) |

#### 6.3 Attendance Check

The Attendance Check screen shows all current internal participants and external Guests while the Meeting is InProgress. The assigned Secretary marks each person Present or Absent and saves the complete list together. A person not yet recorded has no official attendance value. Invitation responses and online-room presence never determine attendance. Saved attendance is available for the meeting minutes. (UC-31, BR-116, BR-170–BR-172, BR-178)

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
| Save attendance [Lưu điểm danh] | Secretary only while InProgress. Every current internal participant and Guest must have a Present or Absent choice. Save the full list together with the recording Secretary and save time; failure leaves all previous values unchanged. Success MSG147; incomplete choices MSG148; Meeting status restriction MSG149; save failure MSG150; changed participant list MSG151 requires refresh. (BR-170–BR-172, BR-178) |
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

#### 7.1 Document Library – Current Build

The Document Library – Current Build screen is what the application displays today when a signed-in internal user opens Document Library [Kho tài liệu] from the sidebar. The personal document library is not yet connected to the backend, so the page shows only its header and an empty-state panel explaining how the library will work; no sample data is displayed. It will be replaced by the Personal Document Library (7.2) once the document API is available (UC-34).

This screen allows any signed-in internal user (Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant) to:

- Open Document Library: click Document Library [Kho tài liệu] in the sidebar to open his/her own library page (UC-34).

- View Library Status: see that the library is not active yet and read how it will be used (UC-34).

Field Description

| Field Name | Description |
| --- | --- |
| Sidebar item [Kho tài liệu] | Navigation item in the Business [Nghiệp vụ] group; highlighted when this page is open. |
| Page title | Read-only “Document Library” [Kho tài liệu]. |
| Page subtitle | Read-only: “Your private documents — only you can see and manage what is here.” [Tài liệu riêng của bạn — chỉ bạn thấy và quản lý được những gì ở đây.] |
| Empty-state panel | Dashed panel with a document icon; no table or buttons are shown. |
| Empty-state title | Read-only: “The document library is not active yet” [Kho tài liệu chưa hoạt động]. |
| Explanation text | Read-only: you will upload documents, put them into folders and attach them to meetings where you are Secretary or Chair; a document not attached to any meeting can be read only by you. |
| Implementation note | Read-only small text: “Will be implemented when the backend provides GET /api/documents.” [Sẽ hiện thực khi backend có GET /api/documents.] |

#### 7.2 Personal Document Library

The Personal Document Library is the proposed Document Library page that replaces the current placeholder (7.1) once the document API is available. Each internal user has a private library: folders on the left and the file table on the right, with version, size, last update and the number of meetings that use each file. Uploading a file to the library does not attach it to a meeting; files are attached through the Add Document to Meeting Dialog (7.4). External Guests have no personal library (UC-34, UC-36).

This screen allows any signed-in internal user (Meeting Organizer, Meeting Approver, System Admin, Chair, Secretary, Participant) to:

- View Personal Document Library: view his/her own documents with file name, version, size, last update and meeting usage (UC-34).

- Browse Folders: select All documents or a folder in the left panel to list the files it contains (UC-34).

- Search and Filter Documents: search by file name and filter by file type (UC-34).

- Upload Document: click “Upload” and choose a file from the computer to store it in the library (UC-36).

On the screen, s/he can also:

- Create Folder: click “New folder” to add a folder for organising documents.

- Use Row Actions: use the icon buttons of a row to work with that file.

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Read-only title “Document Library” [Kho tài liệu] with the subtitle “Your private documents — only you can see and manage what is here.” Only the owner’s own documents are listed. |
| New folder [Thư mục mới] | Secondary button; creates a new folder in the left panel. |
| Upload [Tải lên] | Primary button; opens the file picker and uploads the chosen file as version v1 into the selected folder (UC-36). A file that cannot be stored (e.g. type or size not allowed) is rejected with an error and nothing is saved. |
| Folder panel | List of All documents [Tất cả tài liệu] (default, selected) and the user’s folders (e.g. “Board Q4” [HĐQT quý IV], “Budget 2027” [Ngân sách 2027], “Administration” [Hành chính]), each with the number of files. |
| Search [Tìm theo tên tệp…] | Text input, optional, empty by default; matches the file name within the selected folder. |
| File type filter [Mọi loại tệp] | Dropdown. Values: All file types (default) and each supported type, e.g. PDF, Word, Excel. |
| File name [Tên tệp] | Column: file icon and file name, e.g. “Ke-hoach-kinh-doanh-2027.docx”. |
| Version [Phiên bản] | Column chip with the latest version of the file, e.g. v3. |
| Size [Dung lượng] | Column: file size, e.g. 1,2 MB / 640 KB. |
| Updated [Cập nhật] | Column: date of the latest version, format dd/mm/yyyy. |
| Used in [Đang dùng trong] | Column: number of meetings the file has been attached to, e.g. “2 meetings” [2 cuộc họp], or “Not used” [Chưa dùng] in italics. A file not attached to any meeting can be read only by its owner. |
| Actions [Thao tác] | Two icon buttons per row giving the actions for that file (e.g. open preview, further options). |

#### 7.3 Meeting Detail – Documents Tab

The Meeting Detail – Documents tab lists the documents attached to the selected Meeting. Each document is either linked to one agenda item or is a general document of the Meeting, and the Meeting keeps exactly the version that was added. Users related to the meeting view the list and open a document in the Document Viewer (7.5); the Meeting Organizer and the Secretary add documents from the computer or from their personal library through the Add Document to Meeting Dialog (7.4) (UC-32a, UC-32c, UC-33a).

This screen allows users related to the meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- View Meeting Documents: view the documents of the Meeting with file name, size, linked agenda item, version, the person who added it and the number of comments (UC-32a).

- Filter by Agenda Item: choose an agenda item, or general documents, to show only the related documents (UC-32a).

- Open Document: click a document to open it in the Document Viewer with preview, comments and versions (UC-32c).

- Add Document from Library: (Meeting Organizer, Secretary) click “Choose from Document Library” to attach files from the personal library (UC-33a).

- Upload Document from Computer: (Meeting Organizer, Secretary) click “Upload from computer” to attach a file from the device (UC-33a).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Documents [Tài liệu] is selected. Other tabs: General Information, Participants, Meeting Content, Voting, Recordings, Summary, Minutes. |
| Agenda item filter [Tất cả mục nghị sự] | Dropdown. Values: All agenda items (default), each agenda item of the Meeting by order and title, and General documents [Tài liệu chung]. |
| Choose from Document Library [Chọn từ Kho tài liệu] | Secondary button; opens the Add Document to Meeting Dialog on the “From my library” tab. Visible only to the Meeting Organizer and the Secretary of the Meeting. |
| Upload from computer [Tải lên từ máy] | Primary button; opens the Add Document to Meeting Dialog on the “From computer” tab. Visible only to the Meeting Organizer and the Secretary. |
| Document [Tài liệu] | Column: file thumbnail, file name and size, e.g. “Bao-cao-Sprint5.pdf · 2,4 MB”. Clicking the row opens the Document Viewer (7.5). |
| Linked item [Gắn với mục] | Column: the agenda item the document belongs to (e.g. “1. Sprint 5 results report”), or “General document” [Tài liệu chung] in grey when no item is linked. |
| Version [Phiên bản] | Column chip with the version pinned to the Meeting, e.g. v2. Later changes in the owner’s personal library do not change it; a new version is added from the Document Viewer (UC-33b). |
| Added by [Người thêm] | Column: full name of the user who added the document to the Meeting. |
| Comments [Góp ý] | Column: number of comments on the document, e.g. 3 (UC-35). |
| Access rules | Only users related to the Meeting see this tab (BR-29). An external Guest, within a verified Guest session, sees only the documents explicitly shared with guests (BR-120, BR-154). |

#### 7.4 Add Document to Meeting Dialog

The Add Document to Meeting Dialog opens from the Documents tab with “Choose from Document Library” or “Upload from computer”. The Meeting Organizer or the Secretary selects files from his/her personal library or uploads a file from the computer, optionally links them to one agenda item and chooses whether external Guests may view them. Each addition pins exactly one version of the file to the Meeting (UC-33a).

This screen allows the Meeting Organizer or the Secretary to:

- Select Documents from Library: on the “From my library” tab tick one or more files of his/her personal library (UC-33a).

- Upload from Computer: on the “From computer” tab choose a file from the device to add to the Meeting (UC-33a).

- Link to Agenda Item: choose the agenda item the documents belong to, or leave it empty for a general document (UC-33a).

- Share with Guests: switch on “Allow guests to view” so that external Guests of the Meeting can see the documents (UC-33a).

- Add Documents: click “Add <n> documents” to attach the selected files to the Meeting (UC-33a).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without adding anything.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Add documents” [Thêm tài liệu]. |
| Tabs | From computer [Từ máy tính] / From my library [Từ kho của tôi]; the tab is preselected by the button used on the Documents tab. |
| Library file list | From my library tab. Rows with checkbox, file icon, file name and “version · date” of the latest version (e.g. “v1 · 09/10”). Only the user’s own library files are listed. At least one file must be selected. |
| Link to agenda item [Gắn với mục nghị sự] | Dropdown, optional. Lists the agenda items of the Meeting by order and title (e.g. “3. Minutes signing and sealing procedure”). Hint: “Leave empty for a general document.” [Để trống thì là tài liệu chung.] |
| Guests [Khách mời] – Allow guests to view [Cho khách mời xem] | Toggle switch. When on, external Guests of the Meeting can view the documents in their verified Guest session; when off, only internal users related to the Meeting can see them (BR-120, BR-154). |
| Version notice | Info box: “The meeting keeps exactly version v1. Editing the file in the library later does not change the meeting document.” [Cuộc họp giữ đúng phiên bản v1 …] |
| Cancel [Huỷ] | Text button; closes the dialog. |
| Add <n> documents [Thêm <n> tài liệu] | Primary button showing the number of selected files (e.g. “Add 1 document”). Attaches each file with its pinned version, linked item and guest visibility, records the user as “Added by” and refreshes the Documents tab. If the file cannot be added, an error is shown and nothing is attached. |

#### 7.5 Document Viewer – Preview, Comments & Versions

The Document Viewer opens when a user clicks a document on the Documents tab, on an agenda item or in the current item card. It shows the selected version of the document in a page-by-page preview with zoom, and a side panel with the Comments and Versions tabs. Users related to the meeting preview the document and view its versions; they add comments that refer to a page; the Meeting Organizer and the Secretary can add a new version of the meeting document (UC-32b, UC-32c, UC-33b, UC-35).

This screen allows users related to the meeting (Meeting Organizer, Meeting Approver, Chair, Secretary, Participant) to:

- Preview Meeting Document: read the document page by page and zoom in or out (UC-32c).

- View Comments: read the comments on the document with author, page and time (UC-35).

- Comment on Document: (Meeting Organizer, Chair, Secretary, Participant) type a comment and click “Send” (UC-35).

- View Version History: open the Versions tab to see every version with uploader and time and open an earlier version (UC-32b).

- Add New Version: (Meeting Organizer, Secretary) click “Upload new version” to add a new version of the meeting document (UC-33b).

On the screen, s/he can also:

- Back to Documents: click “‹ Documents” to return to the Documents tab of the Meeting.

Field Description

| Field Name | Description |
| --- | --- |
| Back link [‹ Tài liệu] | Returns to the Documents tab of the Meeting. |
| Document header | Read-only: file name (e.g. “Du-thao-nghi-quyet-HDQT.docx”), chip of the version being viewed (e.g. v2) and confidentiality chip of the Meeting (e.g. Secret [Mật] in red). |
| Page indicator [Trang 2 / 6] | Read-only current page and total number of pages; updates while scrolling. |
| Zoom − / + | Two square buttons that decrease or increase the zoom level of the preview. |
| Upload new version [Tải phiên bản mới] | Secondary button; visible only to the Meeting Organizer and the Secretary. Uploads a file as the next version (e.g. v3); earlier versions are kept in the Versions tab. |
| Preview pane | Read-only rendering of the document pages; a highlighted passage marks the area a comment refers to. For a Secret document the viewer’s name is proposed as a watermark on every page. |
| Comments tab [Bình luận 4] | Tab with the number of comments; selected by default. Each comment shows avatar initials, author, page and relative time (e.g. “Page 2 · 2 hours ago”) and the comment text. |
| Comment box [Viết bình luận…] | Textarea, required to send, empty by default; the comment is linked to the page being viewed. Hidden for users who may not comment (Meeting Approver, Guest). |
| Send [Gửi] | Primary button; posts the comment, which appears at the top of the list and increases the comment count on the Documents tab (UC-35). |
| Versions tab [Phiên bản 2] | Tab with the number of versions; lists v1, v2 … with uploader and upload time. Selecting a version opens it in the preview (UC-32b). |

### 8. FE-08 – Online Meeting Room Management

#### 8.1 Meeting Lobby

The Meeting Lobby is the pre-join screen of the TrustCabinet Online Meeting Room. It opens when an invited user clicks “Join room” on an Online or Hybrid meeting (from the meeting detail page, the dashboard or a notification), or when an external Guest continues after Guest Verification. Before entering, the user checks the camera preview and selects the microphone, speaker and camera; the lobby also states clearly that the room is never recorded (UC-37, UC-39a, UC-39b, UC-04).

This screen allows the Chair, Secretary, Participant and a verified Guest to:

- View Meeting Status: see the meeting title, its live status with the actual start time and how many people are already in the room (UC-37).

- Preview Camera: see the camera preview of the selected camera before entering the room (UC-39b).

- Select Devices: choose the microphone, speaker and camera to be used in the room (UC-39a, UC-39b).

- Set Initial Microphone and Camera State: decide whether to enter with the microphone on or off and the camera on or off (UC-39a, UC-39b).

- Join Meeting Room: click “Join room” to enter the Online Meeting Room – Video Grid (UC-37).

On the screen, s/he can also:

- Read the No-Recording Notice: see that the online room records neither audio nor video, and that any recording needed for the minutes is uploaded by the Secretary after the meeting (FE-09).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting status line | Read-only eyebrow text, e.g. “ĐANG HỌP · BẮT ĐẦU 13:02” [In progress · started 13:02], showing the lifecycle status and the actual start time recorded by Start Meeting (UC-21). |
| Meeting title | Read-only, e.g. “Họp Hội đồng quản trị quý IV”. |
| Room presence count | Read-only, e.g. “9 người đang trong phòng” [9 people in the room]; number of users currently connected to the room (UC-40). |
| Camera preview | Video area showing the local camera image of the selected camera; shows an empty placeholder when the camera is off or no permission has been given by the browser. |
| Micro: Bật / Camera: Tắt [Mic: On / Camera: Off] | Two toggle buttons under the preview; each click switches the state and the label. Default shown: microphone on, camera off. The chosen state is applied when the user enters the room (UC-39a, UC-39b). |
| Micro [Microphone] | Dropdown, required; lists the microphones detected by the browser, default the system device (e.g. “Micro MacBook Pro”). Can still be changed inside the room. |
| Loa [Speaker] | Dropdown, required; lists available audio output devices, e.g. “Loa ngoài” [External speaker]. |
| Camera | Dropdown; lists available cameras, e.g. “FaceTime HD”. Changing it refreshes the preview immediately. |
| No-recording notice | Read-only blue info box: “Phòng họp trực tuyến không ghi âm, không ghi hình. Nếu cần bản ghi để làm biên bản, Thư ký tải lên sau cuộc họp.” [The online room records neither audio nor video. If a recording is needed for the minutes, the Secretary uploads it after the meeting.] The system never records the room. |
| Vào phòng họp [Join room] | Primary button. The system re-checks room-entry eligibility: the user must be a current Chair, Secretary or Participant of the meeting, or a Guest within a valid, meeting-scoped Guest access (BR-152 – BR-157, BR-120); removed or revoked invitees are refused (BR-97, BR-100). Joining is refused once the meeting is Ended (BR-143). On success the user opens the Online Meeting Room – Video Grid with the chosen devices. |

#### 8.2 Online Meeting Room – Video Grid

The Online Meeting Room – Video Grid is the full-screen main view of the online room, shown after the user clicks “Join room” in the Meeting Lobby. It displays the connected people as a video grid with the active speaker highlighted, and a bottom toolbar from which the user controls his/her own microphone and camera, opens the People, Chat and Meeting side panels, or leaves the room. Leaving the room does not change the meeting lifecycle; only the Chair can end the meeting (UC-38, UC-39a, UC-39b, UC-40).

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- View Video Grid: see the camera image or initials avatar of the people in the room, with the current speaker highlighted and muted people marked (UC-40).

- Manage Microphone: turn his/her own microphone on or off (UC-39a).

- Manage Camera: turn his/her own camera on or off (UC-39b).

- Leave Meeting Room: click “Leave room” to disconnect from the room without ending the meeting (UC-38).

On the screen, s/he can also:

- Raise Hand: click “Raise hand” to ask for the floor; the request appears in the Chair’s speaking queue in the In-Room Participants & Moderation Panel.

- Open Side Panels: open the People panel (UC-40, UC-41), the Chat panel (UC-42), screen sharing (UC-39c) or the In-Room Meeting Panel (UC-26, UC-55, UC-32a).

Field Description

| Field Name | Description |
| --- | --- |
| Room header | Read-only top bar: meeting title (e.g. “Họp Hội đồng quản trị quý IV”), the current Agenda Item (e.g. “Mục 2 · Báo cáo kế hoạch kinh doanh 2027”) updated in real time when the Chair changes it (BR-73), elapsed time “Đã họp 00:42:17” [Elapsed] counted from the actual start time, and the confidentiality chip (Normal / Internal / Restricted / Secret [Mật]). |
| Video tile | One tile per connected user: camera image when the camera is on, otherwise a circle with the user’s initials (e.g. “PL”), plus a name label (e.g. “Phạm Thị Lan”). The grid adapts to the number of tiles (e.g. 3 × 2). |
| Active speaker highlight | Blue border around the tile of the person currently speaking. |
| Muted indicator | Red crossed-out icon before the name label when that person’s microphone is off (e.g. “Trần Thu Hà”). |
| Overflow tile | “+4 người” [+4 people] tile shown when more people are connected than the grid can display; the full list is in the People panel. |
| Tắt mic [Mute mic] | Toggle button; switches the user’s own microphone off/on. A user muted by the Chair can unmute himself/herself again (UC-39a, UC-41). |
| Camera | Toggle button; switches the user’s own camera on/off (UC-39b). |
| Chia sẻ [Share] | Starts or stops sharing the user’s screen; opens the Screen Sharing & Chat layout (UC-39c). |
| Giơ tay [Raise hand] | Toggle button; raises or lowers the user’s hand. A raised hand is listed with its time in the Chair’s speaking queue. |
| Người (11) [People (n)] | Opens the In-Room Participants & Moderation Panel; the number is the count of users currently connected (UC-40). |
| Chat / Cuộc họp [Meeting] | Open the in-room Chat panel (UC-42) and the In-Room Meeting Panel with Agenda, Voting and Documents tabs (UC-26, UC-55, UC-32a). |
| Rời phòng [Leave room] | Red button. Disconnects the user from the room and returns to the meeting detail page; the meeting stays In Progress and the user may join again while the meeting is In Progress (UC-38). When the Chair ends the meeting, every connected user is disconnected automatically (BR-143). |

#### 8.3 In-Room Participants & Moderation Panel

The In-Room Participants & Moderation Panel is the right-hand side panel of the online room, opened with the “People (n)” button on the room toolbar. It lists the users currently connected to the room with their meeting role or room presence and the queue of raised hands. Every connected user can view the list; only the assigned Chair sees and uses the moderation controls (UC-40, UC-41).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Meeting Room Participants: see the number and list of users currently in the room, each with initials, full name and meeting role (Chair, Secretary, Guest) or presence such as “Speaking” (UC-40).

- View Speaking Requests: see the users who have raised their hand and the time of the request (UC-40).

On the screen, s/he can also:

- Invite to Speak (Chair only): click “Invite to speak” next to a raised hand to give that person the floor; the request leaves the queue (UC-41).

- Mute a Participant or Everyone (Chair only): mute the microphone of one connected user or click “Mute all” (UC-41).

- Remove from Room (Chair only): remove a connected user from the online room; this does not remove him/her from the meeting roster (UC-41).

Field Description

| Field Name | Description |
| --- | --- |
| Người trong phòng 11 [People in the room] | Panel header with the count of currently connected users (UC-40). |
| Tắt mic tất cả [Mute all] | Red outline button, visible only to the Chair. Mutes every connected user except the Chair after a confirmation; users can unmute themselves afterwards (UC-41). |
| XIN PHÁT BIỂU (2) [Speaking requests] | Section listing raised hands in request order, each with initials, name and “Giơ tay hh:mm” [Raised hand at]. Hidden when nobody has raised a hand. |
| Mời nói [Invite to speak] | Primary button on each request, visible only to the Chair. Gives the floor to that user, lowers the hand and removes the entry from the queue (UC-41). |
| TRONG PHÒNG [In the room] | Section listing every connected user: initials avatar, full name and a sub-label with the meeting role (Chủ trì [Chair], Thư ký [Secretary], Khách mời [Guest]) or the current presence (Đang phát biểu [Speaking]). Removed or revoked invitees disappear from the list when their room-entry access ends (BR-101). |
| Row moderation icons | Two icon buttons per row, active only for the Chair: mute this user’s microphone and remove this user from the room (UC-41). For other users the icons only show the person’s microphone and camera state. |
| Visibility rule | All connected users can view this panel; moderation actions are limited to the assigned Chair of the meeting (permission matrix, Online meeting room – Moderate). |

#### 8.4 Screen Sharing & Chat

The Screen Sharing & Chat layout of the online room appears when a connected user starts sharing his/her screen with the “Share” button and/or opens the Chat panel from the room toolbar. The shared screen takes the main stage, the video tiles shrink into a side column, and the Chat panel on the right shows the text messages exchanged in the room; chat messages are kept with the meeting (UC-39c, UC-42).

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- Manage Screen Sharing: start sharing his/her screen or a window, and stop sharing with the same button (UC-39c).

- View Shared Screen: follow the screen shared by another user on the main stage (UC-39c).

- Use In-room Chat: read the messages sent in the room and send a text message to everyone currently in the room (UC-42).

On the screen, s/he can also:

- Close the Chat Panel: close the panel with the icon in its header to return to the full stage.

Field Description

| Field Name | Description |
| --- | --- |
| Room header | Read-only top bar: meeting title (e.g. “Họp Hội đồng quản trị quý IV”), the current Agenda Item (e.g. “Mục 2 · Báo cáo kế hoạch kinh doanh 2027”) updated in real time when the Chair changes it (BR-73), elapsed time “Đã họp 00:42:17” [Elapsed] counted from the actual start time, and the confidentiality chip (Normal / Internal / Restricted / Secret [Mật]). |
| Shared screen stage | Main area showing the shared screen; caption at the bottom “Phạm Thị Lan đang chia sẻ màn hình” [Phạm Thị Lan is sharing the screen]. |
| Video tile column | Narrow column of video tiles beside the shared screen (name label, initials avatar, active-speaker border, muted icon) as in the Video Grid. |
| Chia sẻ [Share] | Toolbar toggle, highlighted while the user is sharing; the browser asks which screen or window to share. Clicking again stops sharing and the room returns to the grid layout (UC-39c). |
| Chat panel header | Title “Chat” with a close icon. |
| Message list | Read-only list in time order; each message shows the sender name in bold, the time (hh:mm) and the text, e.g. “Đỗ Văn Tùng 13:38 – Slide 4 số liệu chi phí là của quý III hay cả năm ạ?”. Only users currently connected to the room can read and send messages (UC-42). |
| Nhắn cho mọi người… [Message everyone…] | Text input at the bottom of the panel; Enter sends the message to everyone in the room. Empty messages are not sent. Chat is text only. |
| Chat [toolbar] | Toolbar toggle, highlighted while the Chat panel is open. |

#### 8.5 In-Room Meeting Panel

The In-Room Meeting Panel is the right-hand side panel opened with the “Meeting” button on the room toolbar. It reuses the components of the meeting detail page in three tabs – Agenda, Voting and Documents – so that people in the room can follow the agenda, read the meeting documents and vote without leaving the room. When the Chair opens a Vote, everyone in the room receives a pop-up notice and the panel opens automatically on the Voting tab (UC-26, UC-55, UC-32a).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Meeting Agenda: see the Agenda Items of the meeting and which item is currently in progress (UC-26).

- View Meeting Votes: see the open Vote, the agenda item it belongs to and how many eligible voters have voted (UC-52).

- Cast Vote: choose one option of the open Vote and submit the ballot once; available only to the current Chair, Secretary and Participants, not to Guests (UC-55).

- View Meeting Documents: open the documents attached to the meeting; a Guest sees only the documents shared with Guests (UC-32a).

Field Description

| Field Name | Description |
| --- | --- |
| Cuộc họp [Meeting] | Panel title. |
| Tabs | Nghị sự [Agenda], Biểu quyết [Voting] with a badge showing the number of Open votes (e.g. 1), Tài liệu [Documents]. The Voting tab is selected automatically when a Vote is opened. |
| Vote status line | Status chip “Đang mở” [Open] (green) followed by “Mở lúc 13:58 · Mục 2” [Opened at 13:58 · Item 2]: opening time and related Agenda Item. |
| Vote question | Read-only bold text, e.g. “Thông qua kế hoạch kinh doanh năm 2027”. |
| Vote options | Radio cards, exactly one may be selected: Đồng ý [Agree], Không đồng ý [Disagree], Không ý kiến [Abstain]. No option is selected by default. |
| Gửi phiếu [Submit vote] | Primary button, enabled after an option is selected. Records the ballot once for the voter; voting is possible only while the meeting is In Progress and the Vote is Open, and only for the current Chair, Secretary and Participants – Guests do not see the button (BR-160, BR-165). After submission the options become read-only with the user’s choice shown. |
| Ballot progress | Read-only text “Đã bỏ phiếu 7/11 · phiếu kín” [Voted 7/11 · secret ballot]: number of ballots cast out of eligible voters; for a secret ballot individual choices are not shown. |
| Agenda tab | Read-only list of Agenda Items in order with presenter, planned duration and status (Pending / In Progress / Done); the current item is highlighted and updated in real time (BR-64, BR-73). |
| Documents tab | List of meeting documents; clicking a document opens the Document Viewer. A Guest sees only documents explicitly shared with Guests (UC-32a). |

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

Scope: Votes are created, opened, cast and closed only while a Meeting is InProgress. No time-boxed or pre/post-meeting voting, quorum or separate Resolution. The Chair records Passed, NotPassed or Deferred after closing the Vote.

#### 11.1 Meeting Detail – Voting Tab

The Meeting Detail – Voting tab [Biểu quyết] lists every Vote that belongs directly to the selected Meeting. It is used while the Meeting is InProgress: the header shows the actual start time, elapsed time and the current Agenda Item. The Chair creates, opens and closes Votes from here; other users involved in the Meeting see the same list read-only. Votes are never held before or after the Meeting and have no deadline or quorum (UC-52a, UC-52b, UC-53, UC-54, UC-56).

This screen allows the Chair to:

- View Meeting Votes: view all Votes of the Meeting with status, linked Agenda Item, ballot type and opening time (UC-52a).

- View Vote Status: follow the turnout of an open Vote (ballots cast / eligible voters) in real time (UC-52b).

- Edit Draft Vote: click “Edit” on a Draft Vote to change its content, options or ballot type in the Create & Open Vote Dialog (UC-53).

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
| Turnout bar [Đã bỏ phiếu] | Progress bar with “cast / eligible” count (e.g. 3/4), shown for an open Vote and updated as ballots arrive. Individual choices are not shown while the Vote is open. |
| Eligibility note | Read-only text, e.g. “4 người đủ điều kiện: Chủ trì, Thư ký, Thành viên có mặt. Khách không bỏ phiếu.” Only the current Chair, Secretary and Participants [Thành viên] may vote; Guests never vote (BR-160, BR-165). |
| Close voting [Đóng phiếu] | Red outline button on an open Vote, Chair only. Ends balloting and triggers the tally (BR-161). While any Vote is open the Meeting cannot be ended: End Meeting shows MSG139 “Close all open votes before ending the meeting.” (BR-159). |
| Edit [Sửa] | Button on a Draft Vote, Chair only; opens the dialog pre-filled. An open or closed Vote can no longer be edited. |
| Open vote [Mở biểu quyết] | Primary button on a Draft Vote, Chair only, enabled while the Meeting is InProgress (BR-165). Eligible voters see the Vote immediately in Cast Vote. |

#### 11.2 Create & Open Vote Dialog

The Create & Open Vote Dialog opens over the Voting tab when the Chair clicks “Create vote” (or “Edit” on a Draft Vote). The title shows the Agenda Item the Vote is linked to, normally the current item (“Tạo biểu quyết · Mục 2”). The Vote belongs directly to the Meeting, can only be created while the Meeting is InProgress and has no deadline or quorum (UC-53, UC-54).

This screen allows the Chair to:

- Create / Configure Vote: enter the motion, define and order the voting options, choose public or secret ballot and the eligible voter group (UC-53).

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
| Eligible voters [Người được bỏ phiếu] | Dropdown; default “Chair, Secretary, Participants” [Chủ trì, Thư ký, Thành viên]. Only current Chair, Secretary and Participants can be eligible; Guests cannot be selected (BR-160, BR-165). |
| Cancel [Huỷ] | Link button; closes the dialog, nothing is saved. |
| Save draft [Lưu nháp] | Button; saves the Vote with status Draft. Allowed only while the Meeting is InProgress and the user is its current Chair (BR-165). |
| Open vote [Mở biểu quyết] | Primary button; saves and opens the Vote (status Open, opening time recorded). If the Meeting is no longer InProgress the Vote is not created and the reason is shown (BR-160). |

#### 11.3 Cast Vote

The Cast Vote screen (shown here in its compact/mobile layout) is opened by an eligible voter from the open Vote card, the Meeting Workspace, the Personal Dashboard or the in-room meeting panel. It shows one open Vote of the InProgress Meeting and lets the voter submit exactly one ballot (UC-55).

This screen allows the Chair, Secretary and Participant (eligible voters) to:

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
| Submit ballot [Gửi phiếu] | Primary button, enabled when an option is selected. Accepted only while the Meeting is InProgress, the Vote is Open and the user is a current Chair, Secretary or Participant who has not voted yet; Guests are not eligible (BR-160, BR-165). After submission the screen shows the recorded choice and the turnout. |
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
| Result summary | Read-only, e.g. “4/4 người đã bỏ phiếu · công khai · mở 08:51, đóng 08:55”: voters / eligible, ballot type, opening and closing time. |
| Option result rows | One row per option: option label, bar and “count · percentage”, e.g. Agree 3 · 75%, Disagree 0 · 0%, Abstain 1 · 25%. Calculated by the system when the Vote is closed (BR-161). |
| Conclusion [Kết luận] * | Segmented control, required: Passed [Thông qua], NotPassed [Không thông qua], Deferred [Hoãn]. Enabled only for the current Chair and only after the Vote is closed (BR-161, BR-165). |
| Conclusion note [Ghi chú kết luận] | Text input, optional, e.g. “Áp dụng từ Sprint 6.”; included with the conclusion in the minutes. |
| Record conclusion [Ghi kết luận] | Primary button, Chair only; saves the conclusion on the Vote. Other users see the conclusion read-only. |
| Public ballots [Phiếu công khai] | Panel shown only for a public Vote: avatar, name and chosen option of each person; “Did not vote · guest” [Không bỏ phiếu · khách] for Guests, who cannot vote (BR-160); attendance status such as Absent [Vắng] for absent people. Hidden for a secret ballot. |
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
| Item documents | Chips with file name and pinned version (e.g. “Ke-hoach-kinh-doanh-2027.docx · v3”); click opens the Document Viewer – Preview, Comments & Versions. |
| Next [Tiếp theo] | Read-only: order, title and presenter of the next Agenda Item. |
| Open vote card [Biểu quyết] | Shown only while a Vote is Open: chip Open [Đang mở] and the motion text. |
| Vote [Bỏ phiếu] | Primary button, shown only to eligible users (current Chair, Secretary, Participants) who have not voted; opens Cast Vote (11.3). Guests do not see it (BR-160, BR-165). |
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
| Attendance rate [Tỉ lệ tham dự] | KPI tile: share of invitees recorded Present in attendance of Ended Meetings, e.g. 87%; based on attendance, not on invitation responses (BR-116). |
| Average duration [Thời lượng trung bình] | KPI tile: average actual duration (actual end − actual start) of Ended Meetings, e.g. “1 giờ 48”. |
| Minutes sealed within 3 days [Biên bản niêm phong trong 3 ngày] | KPI tile: share of Ended Meetings whose minutes were sealed within 3 days of the Meeting end, e.g. 81%. |
| Meetings per month [Số cuộc họp theo tháng] | Bar chart, one bar per month of the period (T5…T10) with the value above each bar; the current, incomplete month is drawn lighter with the note “Tháng 10 tính tới ngày 14.” |
| By status [Theo trạng thái] | Table with columns Status [Trạng thái] (chip, e.g. Ended, Published, Pending Approval, Draft), Count [Số] and % of all Meetings in the period. |
| Scope and errors | Figures and exports include only Meetings the user may access; System Admin sees all. If statistics cannot be loaded or the export fails, an error message with a retry option is shown and no file is produced. |

### 16. Background Job Inventory

#### 16.1 Unlock Expired Locked Accounts (JOB-01)

placeholder

#### 16.2 Clean Up Old Audit Log Entries (JOB-02)

placeholder

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
| Status | Accepted design (ADR-0010, docs/02-design/08-ke-hoach-phong-hop-truc-tuyen.md); not yet implemented in the main branch. |

#### 17.5 POST: /api/livekit/webhook – LiveKit webhook (API-05) – planned

| URL | POST: /api/livekit/webhook |
| --- | --- |
| Purpose | Receives room events from LiveKit so the system can record who joined/left the online meeting and close finished rooms. |
| Caller | LiveKit server → TrustCabinet backend (planned CallEndpoints). |
| Auth | Anonymous route verified by signature: Authorization header carries a raw HS256 JWT (iss = API key) signed with the LiveKit API secret, containing a sha256 claim of the request body. |
| Request / Response | Events: participant_joined, participant_left, participant_connection_aborted, room_finished, track_published (SCREEN_SHARE). 200 OK; 401 bad signature; 503 not configured; 500 on database error so LiveKit retries. |
| Failure handling | Handlers are idempotent; timestamps taken from the payload; lost webhooks are compensated by the 30-second janitor reconciliation. |
| Status | Planned (ADR-0010); not yet implemented. |

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
| Search and list views | Paginated meeting, document, user, notification and audit-log lists, including search, filter and sort, return the first page within 2 s for 95% of requests | Data volume up to 10,000 meetings, 50,000 document versions and 1,000,000 audit-log entries |
| Document upload and preview | Documents up to 50 MB per file are accepted; after the transfer completes the new version is stored and listed within 5 s; in-browser preview of a document up to 10 MB opens within 3 s | Up to 10 simultaneous uploads; transfer time depends on the user's network |
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
| External Guest access scope | A Guest gets access only through a secure, meeting-specific invitation and only after proving control of the invited email address with a one-time verification code that expires within 10 minutes (BR-152, BR-153). Guest access is limited to the invited meeting and to the functions granted to Guests (view meeting details, respond Accepted/Declined, join the meeting room) (BR-120, BR-150, BR-154), expires after the configured validity period (BR-155), and is invalidated immediately when the invitation is revoked (BR-157). |
| Encryption of data in transit | All communication between browsers and the server, between the server and external services (Google, SMTP, any external speech/AI provider) and all meeting media must be encrypted in transit (TLS 1.2 or later; encrypted WebRTC media). The system must not be served over unencrypted HTTP in production. |
| Protection of stored data and backups | Uploaded documents and meeting audio are stored only on TrustSoft's own infrastructure and are reachable only through the application's access checks, never through a public or guessable URL. Secrets (signing keys, database, Redis and SMTP credentials) are kept out of the source code and configuration repository. The database and file storage are backed up at least daily; backups are stored separately from the production host with access restricted to authorised administrators. |
| Audit logging | The system records security and governance events - sign-in success/failure, lockout and unlock, password reset, Google account linking, user and role changes, meeting lifecycle transitions, access to Restricted/Secret documents, audio upload and deletion, summary approval, vote closing, minutes signing and sealing - with actor, time, action, target object and source IP address (BR-16, BR-38). Audit entries cannot be modified or deleted through the application; only the System Admin can view and search them (UC-92). Passwords, tokens and verification codes are never written to any log. |
| Personal data protection and audio consent (Decree 13/2023/ND-CP) | Personal data (account details, Guest name and email, voices in meeting audio, attendance) is processed only for the stated meeting-management purpose. A meeting audio file can be uploaded only after the Secretary confirms that participants were informed and consented and states the purpose; each recording has a retention date, after which (or on a justified request) it is erased and the erasure is recorded. The online meeting room stores only join/leave times, never media or chat content. If an external speech/AI provider is selected, audio and transcripts are sent only to that provider, only for processing, and must not be retained or used by it for other purposes. |
| Integrity of signed minutes, vote conclusions and document versions | Once a Vote Conclusion is recorded or minutes are sealed, their content cannot be changed by anyone, including administrators; any change to minutes before sealing invalidates signatures collected for the previous content version. Each signature is bound to the exact content version signed so that later tampering is detectable, and each stored document version can be verified against the fingerprint taken at upload. Because v1 uses internal electronic signatures without a certification authority (Report 1, LI-06), the user interface must state that the signature has no legal weight outside TrustCabinet. |
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
| BR-39 | After successful publication, current internal participants must be notified in the application and sent invitation emails. Each external Guest with a current invitation must receive a separate email invitation to verify their invited email address and access only that Meeting. Invitation delivery must not delay confirmation of publication. | UC-20 |  |  |
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
| BR-66 | An Agenda Item requires Title and Planned Duration greater than zero. Presenter and Description are optional; attached documents must belong to the Meeting. | UC-23a, UC-23b |  |  |
| BR-67 | A new Agenda Item is always Pending and is appended after the last existing position, including while the Meeting is InProgress. Existing Item positions are not changed. Reordering exchanges only current Pending Items within the same Meeting. | UC-23a, UC-23d |  |  |
| BR-68 | While Agenda planning is permitted, the active assigned Secretary may edit all planning fields of a Pending Item. After an Item becomes InProgress or Done, only Title and Description may change; Presenter, Planned Duration, document associations and other planning fields are fixed. Only Pending Items may be deleted or reordered. Deletion preserves remaining Item positions. Reordering exchanges Pending Items only within their existing positions; InProgress and Done positions stay fixed. Planning changes never alter execution status, actual timestamps or the Meeting lifecycle. No Agenda changes are permitted after the Meeting is Ended. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| BR-69 | Only the current Chair may set or complete the current Agenda Item while Meeting is InProgress. | UC-24, UC-25 |  |  |
| BR-70 | Only the first Pending Agenda Item in the current Meeting order may be started. Starting it changes Pending to InProgress; completing the current Item changes InProgress to Done. The order and Item status must be checked again when the change is saved. | UC-24, UC-25 |  |  |
| BR-71 | At most one Agenda Item of a Meeting may be InProgress at a time. Concurrent requests must not start two Items; an unsuccessful request leaves the Agenda unchanged. | UC-24 |  |  |
| BR-72 | Starting and completing an Agenda Item record its actual start/end times and elapsed duration. The Chair may complete the current Item before, at or after its Planned Duration. Completion does not automatically start another Item or end the Meeting. | UC-24, UC-25 |  |  |
| BR-73 | After an Agenda change is successfully saved, the complete Agenda is automatically refreshed for authorized users currently viewing the same Meeting. This applies to creation, editing, deletion, reordering, setting the current Item, completing an Item and completing the current Item when the Meeting ends. Refreshing must respect current Meeting access; an unsuccessful change must not be shown as a successful update. | UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26, UC-22 |  |  |
| BR-74 | Agenda Management is permitted in Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including while approval is pending and while the Meeting is running. It is locked in Ended and subsequent lifecycle states. The server rechecks status and Secretary assignment when saving. | UC-23a, UC-23b, UC-23c, UC-23d |  |  |
| BR-75 | Rescheduling a Published Meeting requires a planned start time later than the current time. | UC-15 |  |  |
| BR-76 | Rescheduling a Published Meeting must notify all current invitees; delivery failures do not undo the change. | UC-15 |  |  |
| BR-78 | When a meeting has no Secretary, only the Meeting Organizer may add the first internal participant, and that participant must be assigned the Secretary Meeting Role. | UC-28a |  |  |
| BR-79 | After the first Secretary has been assigned, only an assigned Secretary may manage the meeting roster. | UC-28a, UC-28b, UC-28c |  |  |
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
| BR-122 | After the first Secretary has been assigned, a Meeting must retain at least one current internal participant assigned as Secretary. | UC-28a, UC-28b, UC-28c |  |  |
| BR-123 | A Meeting may have at most one current Chair at a time. | UC-28a, UC-28b |  |  |
| BR-124 | Publishing a Meeting requires exactly one current Chair whose internal account is active. | UC-20 |  |  |
| BR-125 | Publishing a Meeting requires at least one current Secretary whose internal account is active. | UC-20 |  |  |
| BR-126 | A Published or InProgress Meeting must retain its current Chair. The Secretary may replace the Chair only by changing both affected roles together while preserving the required Secretary. (BR-169) | UC-28b, UC-28c |  |  |
| BR-127 | Starting a Meeting requires exactly one current Chair and at least one current Secretary whose internal accounts are active. | UC-21 |  |  |
| BR-129 | Invitation responses do not change readiness requirements for the assigned active Chair and Secretary when publishing or starting a Meeting. | UC-20, UC-21 |  |  |
| BR-130 | Only an active current Chair assigned to the Meeting may explicitly start or end that Meeting. | UC-21, UC-22 |  |  |
| BR-131 | A Meeting may be started only while it is Published. | UC-21 |  |  |
| BR-132 | A Meeting may be ended only while it is InProgress. | UC-22 |  |  |
| BR-143 | When a Meeting becomes Ended, its Online Meeting Room stops accepting joins and disconnects currently connected users. | UC-22, UC-37 |  |  |
| BR-148 | An external Guest is identified by Guest Name and Email and does not require an internal user account. | UC-27, UC-28a, UC-29 |  |  |
| BR-149 | Within one Meeting, a Guest email may have at most one current Guest invitation. | UC-28a |  |  |
| BR-150 | An external Guest has fixed Guest access and receives no system Role or privileged Meeting Role. | UC-28a, UC-37 |  |  |
| BR-151 | A Guest invitation must not be created when the same email is already represented by a current internal participant of that Meeting. | UC-28a |  |  |
| BR-152 | External Guest access requires a secure, meeting-specific invitation. | UC-20, UC-28a, UC-29, UC-37 |  |  |
| BR-153 | An external Guest must verify control of the invited email before Guest access is established. | UC-29, UC-37 |  |  |
| BR-154 | External Guest access is limited to the invited Meeting. | UC-29, UC-37 |  |  |
| BR-155 | External Guest access expires according to the configured validity period. | UC-29, UC-37 |  |  |
| BR-156 | External Guest access does not require an internal account login. | UC-28a, UC-29, UC-37 |  |  |
| BR-157 | Revoking an external Guest invitation immediately invalidates access granted by that invitation. | UC-28c, UC-29, UC-37 |  |  |
| BR-158 | Only the Meeting Organizer may reschedule a Published Meeting; only planned times or location may change. | UC-15 | PO 09/10/2026 | Meeting / Voting Management |
| BR-159 | A Meeting cannot end while any Vote is Open. Completing an InProgress Agenda Item (status Done, actual end time and duration), changing the Meeting to Ended, recording Actual End Time and its lifecycle history must succeed atomically. If any part fails, both Meeting and Agenda Item retain their previous states and timestamps; no successful end history is recorded and room closure is not triggered. Any remaining Pending Agenda Items retain Pending status when the Meeting ends; they are not automatically marked Done. | UC-22 | PO 09/10/2026 | Meeting / Voting Management |
| BR-160 | Votes may be created, opened, cast, tallied, and closed only while the associated Meeting is InProgress. Only current Chair, Secretary and Members may cast a vote; Guest cannot vote. No time-boxed voting, quorum or separate Resolution object exists. (See BR-165.) | UC-52a, UC-52b, UC-53, UC-54, UC-55, UC-56 | PO 09/10/2026 | Meeting / Voting Management |
| BR-161 | After a Vote is closed and tallied, the Chair records the conclusion Passed, NotPassed or Deferred on that Meeting Vote. The tally and Chair conclusion are included in Meeting Minutes, not in a separate Resolution entity. | UC-52b, UC-56, UC-57, UC-59 | PO 09/10/2026 | Meeting / Voting Management |
| BR-162 | When a new Meeting is created, Planned Start Time must be later than the current time. | UC-14 | PO Q7 / BF-02 | Meeting / Voting / Minutes services |
| BR-163 | Online/Hybrid Meetings use the internal TrustCabinet Online Meeting Room. Online Link is managed by the system as part of Online Meeting Room functionality and may be empty until generated. Users cannot enter or modify it during Meeting creation, editing or rescheduling. Link generation is outside Create Meeting and Update Meeting. | UC-14, UC-15 | PO Q4 / BF-02 | Meeting / Voting / Minutes services |
| BR-164 | Starting a Meeting creates an in-app notification for its currently invited internal participants; notification failure does not roll back the successful Meeting start. | UC-21 | PO Q11 / BF-03 | Meeting / Voting / Minutes services |
| BR-165 | A vote may be created and opened only when its Meeting is InProgress, may be cast only by the current assigned Chair, Secretary and Members, and must be closed before the Chair records Passed, NotPassed or Deferred. Guests are not eligible. | UC-53, UC-54, UC-55, UC-56, UC-57 | PO D2 / BF-03 | Meeting / Voting / Minutes services |
| BR-166 | When designated minutes signers request revision or minutes content changes after signing begins, previous signatures on the earlier content version are invalidated and the revised version requires a new signing cycle. Only the assigned Secretary may finalize and seal the completed minutes after all designated signers sign. | UC-60, UC-61, UC-62, UC-64, UC-65 | PO / BF-04 | Meeting / Voting / Minutes services |
| BR-167 | User account list sorting must use an allowed sort field/order, and current search/filter/sort criteria are preserved across pages. | UC-05 | UC-05 user-list acceptance criteria | User Management |
| BR-168 | A submission adding multiple internal Meeting Participants must be saved as one indivisible roster change. If any selected account, assigned Meeting Role or additional Meeting Permission is invalid, no account in the submitted batch may be added. Invitation dispatch occurs only after a successful roster save; delivery errors follow BR-94. | UC-28a | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-169 | Replacing the Chair changes the outgoing Chair to Member and the incoming current internal participant to Chair together. The final list must retain the required Secretary and Chair. If either change fails, both people keep their previous roles. | UC-28b | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-170 | Only an active current assigned Secretary may save or revise official attendance while the Meeting is InProgress. For each current internal participant or current Guest, the only official saved attendance values are Present or Absent. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-171 | A person whose attendance has not been recorded remains unmarked. An unmarked person must not be counted as Absent. Only Present and Absent may be saved as official attendance. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-172 | Saving or revising attendance must cover exactly all current internal participant and Guest entries, with a valid Present or Absent value for each. The entire list is saved together, with no partial changes; if validation, roster consistency or saving fails, every previous attendance value remains unchanged. | UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-173 | Each current internal participant has exactly one meeting role: Chair, Secretary or Member. Each role provides predefined permissions for that Meeting. Meeting roles and additional meeting permissions are independent of the person’s system role. | UC-27, UC-28a, UC-28b, UC-29, UC-30 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-174 | The Secretary may grant current internal Chairs or Members only View meeting participants and View invitation responses as additional permissions, separately for each Meeting. These do not allow participant management, attendance recording, meeting lifecycle actions, agenda management, voting administration or document access beyond the existing rules. Guests cannot receive additional permissions. All Meeting status, sensitivity and membership restrictions still apply. | UC-27, UC-28a, UC-28b, UC-30 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-175 | Only a currently assigned Secretary may grant, change or revoke additional Meeting Permissions on a current internal participant while Meeting roster management is allowed. The Meeting Organizer's initial Secretary assignment does not grant optional extras. | UC-28a, UC-28b | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-176 | Before publication, a Chair or Member cannot view the participant list or invitation responses. From Published onward, the current Chair may view both by default; the assigned Secretary retains access under existing visibility rules; a current Member needs the corresponding additional permission. View Invitation Responses includes invitee name and email in response rows but does not grant roster browsing or search. Guests cannot view either internal list. | UC-27, UC-30 | FE-06 refinement, 12/10/2026 | Meeting Participant Management |
| BR-177 | Role and additional-permission changes within a Meeting are saved consistently. On role change, retained extras must remain valid for the resulting role or the requested change is rejected. Removing an internal Meeting assignment immediately ends the effect of its additional permissions; historical audit evidence may remain. Published/InProgress changes follow the existing notification rules. | UC-28a, UC-28b, UC-28c | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-178 | Official attendance must be recordable and retrievable for both current internal Meeting Participant records and current external Guest invitations. An attendance record includes the saved status, the responsible Secretary and the save time; historical evidence is retained when a published Meeting's participant or Guest is removed/revoked. | UC-28c, UC-31 | FE-06 revision, 12/10/2026 | Meeting Participant Management |
| BR-179 | The Chair cannot end an InProgress Meeting until every current internal participant and Guest has official attendance recorded as Present or Absent. | UC-22, UC-31 | FE-06 refinement, 12/10/2026 | Meeting Participant Management |

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
| 103 | MSG97 | Field error / Form error | UC-28a – Add Meeting Participant / Exception Flows | Invalid meeting participant information.<br>Field messages:<br>- The selected user is not available.<br>- This user is already a meeting participant.<br>- A valid meeting role is required.<br>- The Meeting Organizer must assign a Secretary as the first meeting participant.<br>- Guest name and email are required.<br>- Guest email is invalid.<br>- This guest email is already invited to the meeting.<br>- This email is already represented by an internal meeting participant. |
| 104 | MSG98 | Dialog | UC-28a, UC-28b, UC-28c / Exception Flows | The meeting participant list cannot be modified in its current status. |
| 105 | MSG99 | Toast | UC-28a – Add Meeting Participant / Exception Flows | Unable to add the meeting participant. Please try again. |
| 106 | MSG100 | Toast | UC-28b – Manage Meeting Role & Additional Permissions / Main Flow | Meeting role updated successfully. |
| 107 | MSG101 | Field error | UC-28b – Manage Meeting Role & Additional Permissions / Exception Flows | A valid meeting role is required. |
| 108 | MSG102 | Toast | UC-28b – Manage Meeting Role & Additional Permissions / Exception Flows | Unable to update the meeting role. Please try again. |
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
| 119 | MSG113 | Dialog | UC-44 – End Meeting / Exception Flows | Complete or cancel all unfinished meetings before ending the meeting. |
| 120 | MSG114 | Dialog | UC-22 – End Meeting / Exception Flows | This meeting cannot be ended. |
| 121 | MSG115 | Toast | UC-21 – Start Meeting / Exception Flows | Unable to start the meeting. Please try again. |
| 122 | MSG116 | Toast | UC-22 – End Meeting / Exception Flows | Unable to end the meeting. Please try again. |
| 132 | MSG126 | Inline / Form error | UC-04 – Access Meeting via Guest Invitation / Exception Flows; UC-29 – Respond to Meeting Invitation / Exception Flows | Unable to verify the guest invitation. Check the code and try again. |
| 134 | MSG128 | Page / Panel message | UC-26 – View Meeting Agenda / Exception Flows | Unable to load the meeting agenda. Please try again. |
| 113 | MSG138 | Information | UC-15 – Meeting rescheduled successfully | Meeting rescheduled successfully. Invitees will be notified. |
| 114 | MSG139 | Form error | UC-22 – Cannot end meeting with open vote | Close all open votes before ending the meeting. |
| 135 | MSG140 | Form error | UC-18 – Submit Meeting for Approval / EX-5 | No eligible meeting approver is available. Please contact the administrator. |
| 136 | MSG141 | Dialog / Form error | UC-23b / EX-4 | Only Title and Description can be changed after an agenda item has started. |
| 137 | MSG142 | Dialog / Form error | UC-23c / EX-2 | Only Pending agenda items can be deleted. |
| 138 | MSG143 | Dialog / Form error | UC-23d / EX-2 | Only Pending agenda items can be reordered within their existing positions. Refresh the agenda and try again. |
| 139 | MSG144 | Dialog / Form error | UC-24 / EX-3 | Start the first Pending agenda item in the current order. |
| 140 | MSG145 | Form error | UC-28a / EX-1 | One or more selected participants are invalid. No participants were added. |
| 141 | MSG146 | Toast | UC-28b / EX-5 | Unable to replace the Chair. Meeting roles remain unchanged. |
| 142 | MSG147 | Toast | UC-31 / Main Flow | Meeting attendance saved successfully. |
| 143 | MSG148 | Field error | UC-31 / EX-1 | Select Present or Absent for every current participant and Guest. |
| 144 | MSG149 | Dialog | UC-31 / EX-2 | Attendance can only be recorded while the meeting is in progress. |
| 145 | MSG150 | Toast | UC-31 / EX-5 | Unable to save attendance. No changes were saved. |
| 146 | MSG151 | Dialog | UC-31 / EX-3 | The meeting participant list has changed. Refresh it and try again. |
| 147 | MSG152 | Field / Form error | UC-28a / EX-3; UC-28b / EX-1 | One or more selected meeting permissions are not allowed. |
| 148 | MSG153 | Toast | UC-28b / Main Flow | Meeting permissions updated successfully. |
| 149 | MSG154 | Information | UC-28b / EX-4 | No changes to save. |
| 150 | MSG155 | Form error | UC-22 – End Meeting / EX-3 | Record Present or Absent for every current participant and Guest before ending the meeting. |

### 3. Other Requirements

…
