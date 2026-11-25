# Report 3 – Software Requirement Specification

> **GENERATED — DO NOT EDIT.** Snapshot từ DOCX local. Sửa [`source/Report_3_Software_Requirement_Specification.docx`](source/Report_3_Software_Requirement_Specification.docx) rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report 3`. Sửa tay file này sẽ mất ở lần sync sau. Chi tiết: [08-reports/README.md](README.md).

CAPSTONE PROJECT REPORT

Report 3 – Software Requirement Specification

– Hanoi, Sep 2026 –

Table of Contents

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 27/09/2026 | A | KhanhPG | Initial SRS: context diagram, actors, Authentication use cases. |
| 30/09/2026 | M | KhanhPG | Add Meeting Session and Agenda Management use cases (planning, live control), with business rules and system messages; the Secretary plans sessions. |
| 01/10/2026 | M | KhanhPG | Session deletion becomes cancellation; planned sessions stay editable after publication; scheduling, notification and lifecycle rules. |
| 03/10/2026 | M | KhanhPG | Sync Meeting Management and Notifications with the code (UC-37–UC-42, BR-01–BR-89, MSG95–MSG109); planned scheduling moves to Meeting Session; publication requirements updated. |
| 03/10/2026 | M | KhanhPG | Refine participant management: external Guest invitations without login (UC-21/37–42/45); add BR-101–BR-103 and MSG126. |
| 03/10/2026 | M | KhanhPG | Decouple Meeting, Meeting Session and Online Room lifecycles; sessions optional for publication; explicit Start/End and room use cases. |
| 04/10/2026 | M | KhanhPG | Add UC-53/UC-54 read use cases under their features (UC-17 stays meeting-level); drop the temporary Read Views feature. |
| 05/10/2026 | M | KhanhPG | Map UC groups to the PO-confirmed FE-01–FE-15 baseline (FE-04–FE-06 split); add UC placeholders through UC-94, Personal Document Library, Secretary audio upload; new UC details deferred. |
| 05/10/2026 | M | KhanhPG | Rewrite BR-78 onward as atomic testable rules; one current Chair, exactly one active Chair for publication. |
| 08/10/2026 | M, D | KhanhPG | Remove FE-04 and the Meeting Session layer (PO decision); Agenda, Vote and Resolution attach directly to Meeting. |
| 09/10/2026 | M, D | KhanhPG | Restore FE-04, retire FE-05 and six session UCs; in-meeting voting with Chair conclusion only. |
| 09/10/2026 | M | KhanhPG | Align UC flows, BR/MSG traceability, screens and BF-02/03/04 with single Meeting + in-meeting voting; retired IDs kept. |
| 12/10/2026 | M | KhanhPG | Revise FE-06 participant management (UC-27–UC-31) to business-level flows: bulk add, Chair replacement, full Present/Absent attendance, viewing access; update rules, messages, screens and sequences. |
| 13/10/2026 | M, D | HaiTV | Apply ADR-0018 (no document versions): one immutable file per document; drop UC-32b/33b; add UC-33c/71/72; browser-side previews; Chair/Secretary manage meeting documents; Chair-selected voters, Guests vote (FE-11). |
| 14/10/2026 | M | LocHD | Update screens to wireframe v4 (ADR-0018, Chair-selected voters); rewrite the 7.1 empty state; fix examples in 5.3, 7.5, 11.1, 11.4. |
| 14/10/2026 | M | LocHD | Align 8.5, footnote 14 and BR-165 with Chair-selected voters (selected Guests vote); retire MSG113; describe the two background jobs; drop build status from API-04/05. |
| 14/10/2026 | M | KhanhPG | Complete FE-07/FE-11 use case specs (UC-52a/b, UC-53–UC-57): vote preparation states, one open vote per meeting, fixed eligibility; screens stay with their author. |
| 15/10/2026 | M | KhanhPG | Align UC/BR/MSG with ADR-0020 (access by meeting right): add UC-33b/33d, meeting-based reading, end-of-meeting locks; retire MSG237; clarify BR-159/192/228 and multi-file upload (BR-286). Screens handed over separately. |
| 16/10/2026 | M | LocHD | SCRUM-19 decision (16/10): the Organizer gets an in-app notice on approval/rejection (reason included), no decision email; no invitation notice to the publishing Organizer; all current participants still emailed. |
| 22/10/2026 | M | LocHD | SCRUM-21/22/23: group documents by Agenda Item, Vote and general; in-room Documents panel; audit Confidential/Secret views; one-level comment replies; UC-33a 10-file/200 MB limit (BR-315–319, MSG288). |
| 23/10/2026 | M | LocHD | Rewrite screens 7.3, 7.4, 7.5 and 8.1–8.6 to wireframe v7 and the deployed room; add the 8.6 In-Room Documents panel. |
| 24/10/2026 | M | KhanhPG | BA Final Review SCRUM-18/20/26/97/107: align UC-14/17/28–31/33a/73, their rules and messages with the code; screens redrawn to wireframe v8. |
| 27/10/2026 | M | KhanhPG | PO 27/10 on FE-07 (SCRUM-58, ADR-0033–335): add UC-34b, BR-333–335 — 10 GB system document cap (MSG346), audited locked-account library reading; UC-33a/33b/36 share the 10-file/200 MB limit. |
| 28/10/2026 | M | KhanhPG | BA Final Review FE-07 (SCRUM-23/58/110–113): align UC-71b/72/35/33a–d/34b with the code; exception flows MSG347–349; locked-account reading in the permission matrix. |
| 29/10/2026 | M | KhanhPG | BA Final Review FE-04/05/13 (SCRUM-99/69/15/25/98): UC-75 auto-cancels overdue meetings (AF-2, EX-4 MSG345); BR-301 rewritten (24 h), BR-302 retired; UC-17 Cancelled group; UC-26 read-only Guest view. |
| 29/10/2026 | M | KhanhPG | BA Final Review FE-08 (SCRUM-39/41/42/43): specify room UC-37–42/78 as built; add UC-79 Register to Speak; drop one-to-one calls; BR-323–339, MSG328–343/350. |
| 30/10/2026 | M | HuyNQ | Fix review findings: correct Actors-table UC IDs; AI provider is an in-house worker; replace UC-09/13 placeholders; add the feature priority table; ALT-1 becomes AF-1; keep design out of requirements; refresh the ToC. |
| 30/10/2026 | M, A | KhanhPG | Reorganize FE-09/10 to the as-built recording/transcription/Voice ID; add UC-80 (recorded speech) and UC-81 (Voice ID). |
| 01/11/2026 | M | KhanhPG | Align FE-09/10 with the code: takes replace edited text and names (BR-345/360); erasure is audit-only; 180-day retention from the first take (BR-347); external actors locked to OAuth, Google AI, Resend, R2. |
| 02/11/2026 | M, A | KhanhPG | BA Final Review SCRUM-116 (Guest access, UC-04): code-based verification flow (MSG371–379), 12-hour access ending with the meeting; display name declared once, no cross emails (PO 01/11, MSG381, BR-180). |
| 02/11/2026 | M | LocHD | Align FE-07 with the 02/11 E2E run (PR #224): UC-32a/32c/33a/34b/35 checks as built (MSG382/383); PDF page counting, password refusal; screens 7.3/7.4/7.5/8.6 rewritten as built. |
| 03/11/2026 | M | KhanhPG | PR #201: the agenda form no longer picks documents (UC-23a/b); attach from the row paperclip menu (UC-33b/76a); sign-in lands on the calendar; BR-280/185/277–279 updated. |
| 03/11/2026 | M | KhanhPG | Guest wave (PR #212/213/215/218): UC-04 sign-out flow and end screens; UC-29/32c/37/38/41 Guest rules with recording-notice confirmation (MSG380); Guest speaker assignment (UC-46b/81a); ERD gains GuestSession and TranscriptSpeaker. |
| 03/11/2026 | M | KhanhPG | Post-review fixes (PR #237/#238): 12-hour session limit on UC-04; endpoint 403 before the Chair check; Secretary-only speaker suggestions (UC-45a); second-voice grouping paths (BR-367: 0.7/0.8, ADR-0050); ERD target model settled. |
| 03/11/2026 | M, A | KhanhPG | SCRUM-130 + PR #225 (SCRUM-35): Voice ID ends with account deactivation (UC-06c, BR-365/372); auto-naming for clear matches of notice-1.2 users (BR-362/366/367); notice 1.2 with listen-before-save (UC-81). |
| 03/11/2026 | M | KhanhPG | PO 03/11 on general documents (SCRUM-21, PR #249): general means linked to no Agenda Item and no Vote (BR-280); Draft-Vote-only documents hidden from Members/Guests (BR-275, MSG156); screens 7.3/8.6 follow. |
| 03/11/2026 | M | KhanhPG | SCRUM-129 (PR #245, ADR-0051): room Guests record from their own mic and manage own sentences during the session; BR-355/370, UC-39a AF-2, UC-80. |
| 04/11/2026 | M | KhanhPG | Post-review fixes (PR #254): Draft-Vote-only documents hidden from all but Chair/Secretary (BR-275, UC-32c); Guests get 404 for others' sentences (UC-80); section 3.2 gains Entities 31–35. |
| 04/11/2026 | M | HuyNT | Replace the section 3.1 ERD picture with the code-drawn 41-table model; rewrite the section text; rename VotingEligibleVoter to VoteEligibleVoter. |
| 07/11/2026 | A |  | Add FE-16 Working Calendar & Meeting Time Search (Should-have): UC-84a–e, UC-85a–c (BR-414–430, MSG435–455) for SCRUM-137/142; UC-82/83 numbering reserved; no screens yet. |
| 08/11/2026 | M, A | HuyNQ | Post-review fixes (PR #282): BR-419 drops delegated attendance; BR-424/426 drop UC-12c; new BR-431 share date range; ordered owner–viewer pairs; viewer-loses-right stops the share (MSG439 list-only); UC-84d/84e/85b flows named. |
| 08/11/2026 | M | HuyNQ | Point FE-16/UC-84/85/BR-414–430 to ADR-0058 (PO 07/11: sharing + find-a-time, no AI assistant); find-a-time may overlap Secret meetings, invitee declines. |
| 08/11/2026 | M | KhánhPG | Three sensitivity levels (ADR-0057): Normal/Internal/Secret; Confidential removed; PO confirms the 365-day share limit and the busy-status list. |
| 08/11/2026 | M | KhánhPG | BA Final Review SCRUM-28/96: UC-52a/b drop System Admin; UC-56 tallies from ballots; Guests need no verification at opening (UC-54); UC-74/76a exceptions (MSG166); draft votes auto-delete at End with audit (UC-22); diagrams in sync. |
| 09/11/2026 | A | HuyNT | SCRUM-38: specify UC-70a/70b reports (BR-432–438, MSG463–466 reserved). |
| 10/11/2026 | M, A | LocHD | SCRUM-16 + PR #311 as built: UC-13 audit log rewritten (actors, filters, entry details, EX-3; BR-440–446 incl. sign-in/status/minutes records; MSG469/470 reserved); failed sign-ins name the account (BR-444); screen 3.3 unchanged. |
| 10/11/2026 | M, A | LocHD | E2E run 09/11 (SCRUM-147–151): confirmations for UC-75/17/18/15/29/73/28b; case-insensitive search (BR-439); approver email shown; UC AF/EX additions. |
| 11/11/2026 | M, A | KhanhPG | BA Final Review SCRUM-33 (PR #297): UC-58–60 approval-aligned; new UC-61/64/65/83; BR-404–413; MSG424–434/456; FE-12 renamed; screens 12.2–12.4 pending. |
| 11/11/2026 | M, A | KhanhPG | BA Final Review SCRUM-16 (PR #311): audit steps into UC-01/02/04/14/15/18–22/64/75 (BR-444–446); screen 3.3 pending. |
| 11/11/2026 | M, A | KhanhPG | BA Final Review SCRUM-142 (PO 09–10/11): BR-424–426 drop the nightly job (no Expired status or notification); new UC-84f; MSG439/467/468; UC-06c/08/11 stop Effective shares only. |
| 11/11/2026 | M, A | KhanhPG | Post-review fixes (PR #322, note 43): UC-15/58/61/64/01/02/84c/84f/85c/70a-b aligned with code and PO Q1/Q2 (BR-447 average duration); UC diagram renewed; sequences re-exported. |
| 15/11/2026 | M, A | KhanhPG | SCRUM-152 (note 44, ADR-0060): the Secretary collects Voice ID samples for attendees (new UC-86a–d, BR-448–454, MSG473–477); one consent covers both sample sets; withdrawal deletes both. |
| 15/11/2026 | M, A | KhanhPG | SCRUM-153 (note 45, ADR-0061): recording follows the meeting format (uploads: In-person only, EX-8 MSG478); Hybrid Ended gains a system-built combined recording (AF-3, MSG479; BR-340/342 rewritten, BR-455–458). |
| 15/11/2026 | M, A | KhanhPG | PR #332 (note 46, ADR-0062, PO 13/11): the system ends meetings left In Progress 24 h after planned end (BR-460); UC-22 unchanged; BR-302 points to BR-460. |
| 15/11/2026 | M, D | KhanhPG | PO 14–15/11 (SCRUM-37/106): drop minutes signing incl. simulation — remove UC-62/63, retire BR-166; Chair approval issues minutes (UC-64); no feedback-list UC (note 47). |
| 15/11/2026 | M, D | HaiTV | PO 15/11 (SCRUM-37): drop MinutesSignature from the database (migration DropMinutesSignature); section 3.2 loses Entity 27, Entity 26 rewritten to the approval flow. |
| 15/11/2026 | M, D | KhanhPG | SCRUM-207 (note 50): align Report 3 and API docs with QA-built behavior 15/11 (drop UC-09/UC-69, fix UC/BR/MSG mismatches). |
| 15/11/2026 | M, D | KhanhPG | SCRUM-207 (note 51): drop remaining FE-14 mentions; features are Must-have FE-01 to FE-13, Should-have FE-15 to FE-16. |
| 19/11/2026 | M | KhanhPG | [PR docs note 54] Sync Report 3 UC/BR/messages/screens with the shipped UI audit and the In-Progress busy rule — done. (source: note 54, PR #396–#399, BR-420) |
| 19/11/2026 | M | KhanhPG | BA Final Review note 55: retire UC-51, add UC-82 (Take Meeting Notes) to FE-05 with BR-397–BR-403/BR-459/BR-461 and MSG415–MSG423; promote UC-84/85 to official; update UC-59 (BR-402) and UC-24 (AF-2); sync inventory and UC diagram. |
| 21/11/2026 | M, A | KhanhPG | [PR docs note 57] Bug-fix round 19-21/11 (PR #420-#448): Secretary starts meetings, audit/search, AI draft gating, upload limits, calendar sharing, MP3 and combined recordings (BR-462, MSG481/482). |
| 22/11/2026 | D | BA | [note 58] Post-trial 21/11 changes (PR #466-#471, #473, #474, #476): three system roles only with Chair/Secretary as meeting roles (ADR-0075); proxy voice collection by permission holders with per-take agreement and MSG483 (ADR-0073); 0.6/0.05/0.7/0.7/0.8 thresholds with 0.6 auto-assign (ADR-0074); digits in transcripts (ADR-0076); one-switch hybrid room mic with 5-minute takes (ADR-0072). |
| 23/11/2026 | M | BA | [note 59] Report query (PR #488, SCRUM-38): UC-70a/70b query flow with EX-3 MSG484, BR-437/BR-438 update, new BR-468; keep hand corrections on re-transcription (ADR-0077, SCRUM-31/35): UC-44d/45c/46a/46b, BR-345/349/360 update, new BR-467, two transcript sentence columns. |
| 24/11/2026 | M | HuyNQ | [plan 08, ADR-0079] Business Rules cut from 406 to 153 and renumbered BR-001–153; UI, validation and technical limits moved into the use cases and section IV. Actors Meeting Organizer and Meeting Approver renamed Member and Approver. Earlier rows keep the old BR IDs (see 09-bang-anh-xa-br-dot-1). |
| 24/11/2026 | M | HuyNQ | [ADR-0079, UC batch 1] Use cases renumbered UC-01–UC-81 in the order of section II, sub-cases lettered again without gaps; UC-86 made a parent of its own; retired UC-51 removed. Earlier rows keep the old ids (see 10-bang-anh-xa-uc-dot-1). |
| 24/11/2026 | M, A | HuyNQ | [decisions after PR #509] BR-125 split (new BR-154: unmatched carry only in the audit log); combined recordings add no hours moved from BR-153 to BR-151; UC-54c states the carry thresholds; UC-06b edits Full Name, Email and System Role in one save, as built (EX-3, MSG12). |
| 24/11/2026 | M | LocHD | [BF review] BF-02 to BF-04 aligned with the built system: BF-02 adds finding a common time (UC-81) and cancelling (UC-22); BF-03 adds room recording (UC-52); BF-04 adds summary approval (UC-61) and minutes from the fixed template (UC-70). |
| 24/11/2026 | M | HuyNQ | [PR #531] UC diagrams follow strict UML: only 8 real <<extend>> kept, the rest connect to the actor; actors renamed Member/Approver, Cloudflare R2 → File Storage (ADR-0084); §1, §4.1 and §4.3 updated. |
| 25/11/2026 | M | HuyNQ | [PR #537] File Storage is no longer an actor (SeaweedFS runs on the app server, ADR-0084): §1, §4.1, context and UC diagrams; §17.7 says SeaweedFS; UC-42 to the actor (7 <<extend>>); AI Drafting Service drawn on UC-58. |
| 25/11/2026 | M, A, D | KhanhPG | [note 60, note 61] UC-52b and UC-54c removed: UC-52a renamed Turn Room Microphone On/Off, UC-54a gains AF-2/AF-3; system-run steps moved to Report 4 part IV, JOB-03–JOB-10 added; UC tables in one style; every figure captioned, overview UC diagram in §4.3. |
| 25/11/2026 | M | KhanhPG | [PR #507] §IV.2.3 External Guest access scope: the one-time code expires within 15 minutes, as in the code (GuestAccessOptions.CodeLifetimeMinutes). |
| 25/11/2026 | M | LocHD | [PR #547] Audio uploads limited to 300 MB with no duration limit; audit log (UC-12) without CSV export or before/after values; the server never records on its own; CPU-only speech worker; Jira keys removed. |

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
| UC | Use Case |
| FE | Feature |
| API | Application Programming Interface |
| JOB | Background Job |
| ADR | Architecture Decision Record |
| Organizer | The person who created a given Meeting, a Member or an Approver. Business rules name this relationship; it is not a separate actor and differs from the meeting role Member. |

## I. Overall Requirements

### 1. Context Diagram

TrustCabinet is an Internal Meeting and Document Management System that interacts with the following human/internal actors: System Admin, Member, Approver, Participant, and Guest. Chair and Secretary are meeting roles, not system roles (ADR-0075). The context diagram also shows three service actors outside the TrustCabinet backend: OAuth Provider, AI drafting service and Resend Email. Uploaded files are kept in the team's own SeaweedFS store on the app server (ADR-0084), inside the system boundary, so it is not an actor.

- OAuth Provider supports Google-based sign-in and provides verified identity information for users accessing TrustCabinet.

- The AI drafting service writes an editable draft of the Meeting content from the transcript, agenda and vote results (UC-58). The System Admin chooses the team's own AI machine or an external API; only Normal meetings may be sent to the API, Internal and Secret meetings always stay on the team's machine (ADR-0056). Only text is sent: audio and voice samples never leave the system (BR-121). Transcription and speaker identification run inside the system and do not use any external service. The Secretary may correct transcript text and speaker attribution before generating or regenerating the summary. Formal meeting minutes are prepared separately from approved meeting records and are not generated by the AI service from secretary notes or arbitrary meeting documents.

- Resend Email handles outbound email delivery, including password-reset messages, meeting invitations, and other meeting-related communications.

Through these interactions, TrustCabinet acts as the central point connecting meeting participants, meeting records, documents, voting results, and post-meeting minutes, while external services are used only for specific supporting functions outside the main system boundary.

Figure 1: Context Diagram

View the full-size image on Google Drive

### 2. Main Business Flow

#### 2.1 System Access & Administration (BF-01)

Trigger: An internal staff member needs to be granted access to TrustCabinet, or needs their existing access changed.

End condition: The user has a valid account with the appropriate system role and permissions and can access the functions they are allowed to use.

Figure 2: Business Flow BF-01 – System Access & Administration

View the full-size image on Google Drive

#### 2.2 Meeting Preparation, Approval & Participant Coordination (BF-02)

Trigger: A Member identifies the need to hold a new meeting.

End condition: The meeting is Published, invitations have been issued, invitees can respond, and the meeting is ready to be conducted.

Figure 3: Business Flow BF-02 – Meeting Preparation, Approval & Participant Coordination

View the full-size image on Google Drive

#### 2.3 Meeting Execution & Decision Making (BF-03)

Trigger: The Chair decides to start a meeting that is in Published status.

End condition: The meeting is Ended; its agenda outcomes and meeting-level voting results and decisions have been recorded.

Figure 4: Business Flow BF-03 – Meeting Execution & Decision Making

View the full-size image on Google Drive

#### 2.4 Post-Meeting Processing & Formal Records (BF-04)

Trigger: A meeting has Ended and its post-meeting records need to be processed.

End condition: The meeting has formal minutes that have been approved and issued, and the meeting records can be accessed according to permissions.

Figure 5: Business Flow BF-04 – Post-Meeting Processing & Formal Records

View the full-size image on Google Drive

### 3. Conceptual Data Model

#### 3.1 Entity Relationship Diagram

Figure 6 shows the data model of TrustCabinet as 42 tables: the 34 entities described in 3.2; two entities that 3.2 does not detail, AiProviderSetting (the AI drafting service chosen by the System Admin) and CalendarShare (calendar sharing between users, FE-16); the four junction tables MeetingDocumentAgendaItem, VoteEligibleVoter, VoteDocument and DocumentReadConfirmation; and two supporting tables, MeetingInvitationDispatch and GuestVerificationChallenge, for invitation email delivery and Guest email verification. Vote and Agenda Item each belong directly to Meeting.

Figure 6: Entity Relationship Diagram

View the full-size image on Google Drive

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
| 4 | Status | enum |  | No | Lifecycle status. Values: Draft, WaitingApproval, Rejected, Approved, Published, InProgress, Ended, Cancelled. |
| 5 | Sensitivity | enum |  | No | Confidentiality level of the meeting and its records. Values: Normal, Internal, Secret. |
| 6 | Mode | enum |  | No | How the meeting is held. Values: InPerson, Online, Hybrid. |
| 7 | Location | string |  | No | Room name or address where the meeting is held in person. |
| 8 | OnlineLink | string |  | Yes | Link to the online meeting room; used for Online and Hybrid meetings. |
| 9 | PlannedStartTime | timestamptz |  | No | Planned start date and time. |
| 10 | PlannedEndTime | timestamptz |  | No | Planned end date and time; must be later than PlannedStartTime. |
| 11 | ActualStartedAt | timestamptz |  | Yes | Date and time the Chair actually started the meeting. |
| 12 | ActualEndedAt | timestamptz |  | Yes | Date and time the meeting actually ended. |
| 13 | CreatedById | uuid | FK | No | Member who created the meeting (references User). |
| 14 | AssignedApproverId | uuid | FK | Yes | Approver assigned to review the meeting in the current approval cycle (references User). |
| 15 | CancelledAt | timestamptz |  | Yes | Date and time the meeting was cancelled. |
| 16 | CancelledById | uuid | FK | Yes | User who cancelled the meeting (references User). Null when the system cancelled it automatically. |
| 17 | CancellationReason | string |  | No | Reason the meeting was cancelled; empty when it was not cancelled. |
| 18 | CancelledFromStatus | enum |  | Yes | Status the meeting had when it was cancelled; decides whether participants may still read it. |

Entity 6: MeetingStatusHistory

Append-only history of every status transition of a meeting: who changed it, when and why. Used to show the meeting timeline and as audit evidence.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the history record. |
| 2 | MeetingId | uuid | FK | No | Meeting whose status changed (references Meeting). |
| 3 | FromStatus | string |  | Yes | Status before the transition. Null for the record that represents creation of the meeting. |
| 4 | ToStatus | string |  | No | Status after the transition. |
| 5 | ActorId | uuid | FK | Yes | User who performed the transition (references User). Null when the system performed it, for example the automatic cancellation of an overdue meeting. |
| 6 | Reason | string |  | Yes | Reason given for the transition; required when a meeting is rejected or cancelled. |
| 7 | OccurredAt | timestamptz |  | No | Date and time of the transition. |

Entity 7: MeetingParticipant

Internal user assigned to a meeting, together with the meeting role, the invitation response and the actual attendance. Removed participants are kept for history.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the participant record. |
| 2 | MeetingId | uuid | FK | No | Meeting the user is assigned to (references Meeting). |
| 3 | UserId | uuid | FK | No | Internal user assigned to the meeting (references User). |
| 4 | MeetingRole | enum |  | No | Role of the user in this meeting, independent of the system role. Values: Chair, Secretary, Member. |
| 5 | InvitationResponse | enum |  | No | Answer to the meeting invitation, recorded once and not changed afterwards. Values: Pending, Accepted, Declined. The Chair's response is not displayed; a role change does not reset the response. |
| 6 | RespondedAt | timestamptz |  | Yes | Date and time the participant answered the invitation; set once together with InvitationResponse. |
| 7 | Attendance | enum |  | Yes | Official attendance: Present or Absent. Empty until recorded by the assigned Secretary. Invitation response and room presence do not set attendance. |
| 8 | AssignmentStatus | enum |  | No | Whether the participant is still assigned to the meeting. Values: Current, Removed. |
| 9 | RemovedAt | timestamptz |  | Yes | Date and time the participant was removed from the meeting. |
| 10 | AttendanceRecordedById | uuid | FK | Yes | Secretary who saved the official attendance (references User). Empty until recorded. |
| 11 | AttendanceRecordedAt | timestamptz |  | Yes | Time official attendance was last saved. Empty until recorded. |
| No | Field | Data Type | Key | Nullable | Description |
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
| 5 | InvitationResponse | string |  | No | Guest's answer to the invitation, recorded once and not changed afterwards. |
| 6 | RespondedAt | timestamptz |  | Yes | Date and time the guest answered the invitation; set once together with InvitationResponse. |
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
| 6 | PresenterId | uuid | FK | Yes | Internal participant who presents the item (references User). Null when no internal presenter is assigned. |
| 7 | PresenterGuestInvitationId | uuid | FK | Yes | Guest invitation of the Guest who presents the item (references MeetingGuestInvitation). At most one of the two presenter fields is set. |
| 8 | LegacyPresenterName | string |  | No | Presenter name typed before presenters were chosen from the Meeting’s people; kept as read-only history, empty otherwise. |
| 9 | Status | enum |  | No | Progress of the item during the meeting. Values: Pending, InProgress, Done. |
| 10 | ActualStartedAt | timestamptz |  | Yes | Date and time the Chair opened the item. |
| 11 | ActualEndedAt | timestamptz |  | Yes | Date and time the item was completed. |
| 12 | Conclusion | string |  | Yes | What was concluded or agreed for the item, at most 2,000 characters; replaced when saved again. |
| 13 | ConclusionUpdatedById | uuid | FK | Yes | User who last saved the conclusion (references User). |
| 14 | ConclusionUpdatedAt | timestamptz |  | Yes | Date and time the conclusion was last saved. |

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

Online call: the online room of a meeting. Only call metadata is stored; audio and video are not stored.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the call. |
| 2 | Kind | enum |  | No | Type of call. Always the online room of a Meeting. |
| 3 | MeetingId | uuid | FK | No | Meeting the room belongs to (references Meeting). |
| 4 | Status | enum |  | No | State of the call. Values: Active, Ended. |
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

Entity 14: VotingSession

A Vote belongs to one Meeting and is not linked to an Agenda Item. The current Chair prepares Draft Votes in Approved, Published or InProgress; opening and balloting require InProgress. Closing and recording a conclusion may finish in Ended. The fixed eligible-voter list is represented by VoteEligibleVoter; it is not reconstructed from current roles. No time-boxed voting or quorum exists.

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
| 9 | EligibleCount | int |  | No | Number of VoteEligibleVoter entries fixed when opening succeeds. It does not change when roles, attendance or access later change. |
| 10 | Conclusion | enum |  | Yes | Conclusion recorded by the Chair after the vote closes. Values: Passed, NotPassed, Deferred. Null until concluded. |
| 11 | ConclusionNote | string |  | Yes | Explanatory note for the conclusion. |
| 12 | ConcludedById | uuid | FK | Yes | Chair who recorded the conclusion (references User). |

Entity 14a: VoteEligibleVoter

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

Entry in a user's personal library. It refers to one immutable Stored File; documents have no versions, so changing a document means uploading a new file.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the document. |
| 2 | OwnerId | uuid | FK | No | User who owns the document (references User). |
| 3 | FolderId | uuid | FK | Yes | Folder holding the entry (references Folder). Null at the root of the library. |
| 4 | StoredFileId | uuid | FK | No | Stored file the entry refers to (references StoredFile). |
| 5 | FileName | string |  | No | File name shown in the library. |
| 6 | Category | enum |  | No | Category of the document. Values: Material, Draft, Administrative, Other. |

Entity 19: StoredFile

One uploaded file in object storage. It is immutable; personal library entries and Meeting documents only refer to it. A periodic cleanup deletes the object once nothing refers to it.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the stored file. |
| 2 | StorageKey | string |  | No | Key of the original file in object storage. |
| 3 | ContentType | string |  | No | File type detected from the file content (PDF or DOCX). The file is stored as uploaded; the browser renders the preview. |
| 4 | ByteSize | bigint |  | No | File size in bytes. |
| 5 | ContentSha256 | string |  | No | SHA-256 hash of the stored file, used to detect changes to the file. |
| 6 | PageCount | int |  | Yes | Number of pages of a PDF, counted by the system from the file’s structure when it is uploaded; stored only for a PDF. Null for a DOCX, which is previewed as one continuous document, and for a PDF uploaded before the system counted pages itself. A PDF whose pages cannot be counted is rejected at upload. |
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
| 8 | RemovedAt | timestamptz |  | Yes | Date and time the document was removed from the meeting (UC-36c); null while it is shown. |
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

Audio of a meeting: the room microphone's takes appended into one recording, a file uploaded afterwards, or the combined recording of a hybrid or online meeting built by the server. Consent and retention fields support personal data obligations. How takes are saved and the combined recording is built is described in Report 4, IV.4 and IV.5; automatic erasure in Report 4, IV.6.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the recording. |
| 2 | MeetingId | uuid | FK | No | Meeting the recording belongs to (references Meeting). |
| 3 | Source | enum |  | No | How the recording entered the system. Values: BrowserCapture (room microphone takes), Upload (file uploaded afterwards), Combined (hybrid or online meeting mixed into one track by the server), CallRoom (reserved, not produced in this version). |
| 4 | Status | enum |  | No | State of the recording. Values: Pending (a room take still being streamed), Available, Failed, Deleted. |
| 5 | FileName | string |  | No | File name shown in the list and used when the recording is downloaded. |
| 6 | StoragePath | string |  | No | Location of the audio file in recording storage. |
| 7 | ContentType | string |  | No | Media type of the stored file, for example audio/wav. |
| 8 | ByteSize | bigint |  | No | Size of the stored file in bytes. A room recording of 300 MB or more takes no more takes; the next take starts a new one (Report 4, IV.4). |
| 9 | DurationMs | bigint |  | No | Duration of the audio in milliseconds. |
| 10 | ConsentObtainedAt | timestamptz |  | Yes | Date and time participants were told recording was about to start. Null means no consent is on file, and capture must not start. |
| 11 | ConsentPurpose | string |  | No | Stated purpose of the recording, as shown to participants. |
| 12 | RetainUntil | timestamptz |  | Yes | Date and time the audio is due to be erased: 180 days after the first take; a combined recording takes the date of its earliest source (BR-110, Report 4, IV.6). |
| 13 | DeletedAt | timestamptz |  | Yes | Date and time the audio was erased; the row is kept as evidence. |
| 14 | Takes | jsonb |  | Yes | Room-microphone takes in this recording, in order: offset in the audio, duration and the time each was spoken, so the transcript reads on the meeting's clock (Report 4, IV.4). Null for an uploaded file. |
| 15 | CreatedById | uuid | FK | No | User who started the first take or uploaded the file; for a combined recording, the creator of its room recording, or the meeting's Secretary (references User). |

Entity 23: Transcript

Speech-to-text result with speaker separation for one recording. It is processed in the background, so its state, progress and error are stored; its life cycle is Pending, Processing, then Completed or Failed (Report 4, IV.1).

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the transcript. |
| 2 | RecordingId | uuid | FK, UK | No | Recording transcribed (references Recording); unique, one transcript per recording. |
| 3 | Status | enum |  | No | Processing state. Values: Pending, Processing, Completed, Failed. A new take, an upload or the Secretary's request to run it again sets it back to Pending (Report 4, IV.1). |
| 4 | Provider | string |  | No | Service or model that produced the transcript. |
| 5 | Language | string |  | No | Language of the transcript (vi). |
| 6 | ProgressPercent | int |  | No | Processing progress: 0–99 while the speech worker runs, 100 once the result is saved. |
| 7 | ProcessingStartedAt | timestamptz |  | Yes | Date and time the current or last run was taken off the queue; null while it waits. |
| 8 | ErrorMessage | string |  | No | Reason for failure, shown to the Secretary with the option to run it again; empty otherwise. |
| 9 | CompletedAt | timestamptz |  | Yes | Date and time the last run finished. |

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
| 10 | SpeakerAssignedById | uuid | FK | Yes | Secretary who moved the sentence to another voice, so a later run carries it again |
| 11 | SpeakerAssignedAt | timestamptz |  | Yes | Time the sentence was moved to another voice |

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

Formal minutes of a meeting, compiled from the approved summary, decisions and vote results. The Secretary submits a revision, the Chair approves it, which issues it, or returns it with a reason. An issued revision is locked and never edited; a correction is a new revision.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the minutes revision. |
| 2 | MeetingId | uuid | FK | No | Meeting the minutes belong to (references Meeting). |
| 3 | RevisionNumber | int | UK | No | Revision number, starting at 1; unique within the meeting. |
| 4 | SupersedesId | uuid | FK | Yes | Issued revision that this revision corrects (references MeetingMinutes). Null for the first revision. |
| 5 | Content | string |  | No | Content of the minutes. |
| 6 | BuiltFromDraftAt | timestamptz |  | Yes | Last change of the AI draft the content was built from; null when built without one. |
| 7 | Status | enum |  | No | Approval state of the revision. Values: Draft, WaitingApproval, Rejected, Issued, Superseded. |
| 8 | CreatedById | uuid | FK | No | User who prepared the revision (references User). |
| 9 | SubmittedById | uuid | FK | Yes | Secretary who last submitted the revision for approval (references User). |
| 10 | SubmittedAt | timestamptz |  | Yes | Date and time the revision was last submitted for approval. |
| 11 | ApprovedById | uuid | FK | Yes | Chair who approved the revision, which issued it (references User). |
| 12 | ApprovedAt | timestamptz |  | Yes | Date and time the revision was approved. |
| 13 | RejectionReason | string |  | No | Reason the Chair gave when returning the revision, up to 2,000 characters; empty otherwise. |
| 14 | ReturnedById | uuid | FK | Yes | Chair who last returned the revision (references User). |
| 15 | ReturnedAt | timestamptz |  | Yes | Date and time the revision was last returned. |
| 16 | IssuedAt | timestamptz |  | Yes | Date and time the revision was issued; from then on it is locked. |

Entity 27: Notification

In-app notification delivered to one user; opening it navigates to the related resource.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the notification. |
| 2 | UserId | uuid | FK | No | Recipient of the notification (references User). |
| 3 | MeetingId | uuid | FK | Yes | Meeting the notification is about (references Meeting). Null when not related to a meeting. |
| 4 | TargetType | string |  | No | Type of the resource opened from the notification. |
| 5 | TargetId | uuid |  | No | Identifier of the resource opened from the notification. |
| 6 | ReadAt | timestamptz |  | Yes | Date and time the notification was read. Null while unread. |

Entity 28: EmailMessage

Outgoing email kept in a queue so it can be sent in the background, retried on failure and traced later.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the email message. |
| 2 | MeetingId | uuid | FK | Yes | Meeting that caused the email (references Meeting). Null when not related to a meeting. |
| 3 | DeduplicationKey | string | UK | No | Key of the business event that created the message; unique, so the same event never sends twice. |
| 4 | Status | string |  | No | Delivery state of the message (for example queued, processing, sent or failed). |
| 5 | RetryCount | int |  | No | Number of send attempts made. |
| 6 | CreatedAt | timestamptz |  | No | Date and time the message was queued. |

Entity 29: Folder

Folder in a user’s personal library. Folders nest at most five levels; a folder created for a meeting upload keeps a link to that Meeting so later uploads find it after a rename or move.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the folder. |
| 2 | OwnerId | uuid | FK | No | User who owns the folder (references User). |
| 3 | ParentId | uuid | FK | Yes | Folder that contains this folder (references Folder). Null at the root of the library. |
| 4 | Name | string |  | No | Folder name; must not be empty and need not be unique. |
| 5 | MeetingId | uuid | FK | Yes | Meeting this folder was created for when a file was uploaded straight into it (references Meeting); null for folders the user created. |
| 6 | CreatedAt | timestamptz |  | No | Date and time the folder was created. |

Entity 30: TranscriptSpeaker

One voice of a transcript and the person it belongs to. The machine may suggest a member whose voice sample matches and, from 03/11/2026, fills the name in when the score reaches the auto-assign threshold; the Secretary may change or clear it. A voice belongs to a member or to a Guest with a current invitation. No voice vector is stored.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the voice. |
| 2 | TranscriptId | uuid | FK | No | Transcript the voice belongs to. |
| 3 | Label | string | UK | No | Label the transcription gives the voice, e.g. SPEAKER_00. |
| 4 | SpeechMs | bigint |  | No | Total speech of the voice in the recording, in milliseconds. |
| 5 | SuggestedUserId | uuid | FK | Yes | Member whose voice sample matched most closely; null when the machine suggests nobody. |
| 6 | SuggestionScore | double |  | Yes | Match score of the suggestion, from 0 to 1; null when there is no suggestion. |
| 7 | AssignedUserId | uuid | FK | Yes | Member the voice is assigned to, either by the machine or by the Secretary. |
| 8 | AssignedGuestId | uuid | FK | Yes | Guest invitation the voice is assigned to; only while that invitation is current. |
| 9 | AssignedById | uuid | FK | Yes | Secretary who assigned the name; null when the machine filled it in. |
| 10 | AssignedAt | timestamptz |  | Yes | Time the name was assigned. |

Entity 31: GuestSession

Verified session of an external Guest, giving access to one Meeting without an account. A row is itself the evidence that the invited email was verified; one invitation may open several sessions, one per device.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the session; it is the session identifier carried by the guest token. |
| 2 | InvitationId | uuid | FK | No | Guest invitation that opened the session. |
| 3 | ExpiresAt | timestamptz |  | No | Time the session expires; 12 hours after it was opened. |
| 4 | RevokedAt | timestamptz |  | Yes | Time the session ended early: the Guest signed out, the invitation was revoked or the Meeting ended. |

Entity 32: SpeechClip

One sentence a person said into their own microphone in an online or hybrid Meeting, transcribed as soon as they paused and kept with its audio so the meeting can be heard again sentence by sentence. The speaker is the owner of the microphone: a member of the Meeting or a Guest invited to it, never both. It is personal data and is erased with the Meeting's recordings.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the sentence. |
| 2 | MeetingId | uuid | FK | No | Meeting the sentence was spoken in. |
| 3 | SpeakerId | uuid | FK | Yes | Member who owns the microphone; null when a Guest spoke. |
| 4 | GuestInvitationId | uuid | FK | Yes | Guest invitation that spoke; null when a member spoke. |
| 5 | ClientClipId | uuid | UK | No | Identifier the browser gave the sentence, so a retry after a lost response stores it once. |
| 6 | StartedAt | timestamptz |  | No | Time the sentence began, by the speaker's clock, kept inside the Meeting's time. |
| 7 | DurationMs | bigint |  | No | Length of the sentence in milliseconds; greater than zero. |
| 8 | Text | string |  | No | Transcribed text of the sentence. |
| 9 | EditedById | uuid | FK | Yes | Secretary who corrected the machine's text. |
| 10 | EditedAt | timestamptz |  | Yes | Time the text was last corrected. |
| 11 | Segments | jsonb |  | Yes | Stretches of speech found in the sentence, with how many words each holds and how loud it reached the microphone; numbers only, never audio or a voice vector. |
| 12 | ExcludedAt | timestamptz |  | Yes | Time the Chair or the Secretary took the sentence out of the record; null while it counts. |
| 13 | ExcludedById | uuid | FK | Yes | Chair or Secretary who took the sentence out of the record. |
| 14 | ExclusionReason | string |  | Yes | Why the sentence was taken out: Leak (another attendee's words, overheard) or Other. |
| 15 | AudioPath | string |  | Yes | Where the sentence's audio lies in the recordings store. |
| 16 | AudioContentType | string |  | Yes | Media type of the stored audio. |
| 17 | Peaks | bytea |  | Yes | One loudness value per 100 ms, to draw the waveform without fetching any audio. |

Entity 33: VoiceConsent

A person's consent to Voice ID: the system may keep voice samples and use them to suggest that person's name for a voice. The consent is separate from consent to being recorded, is given by the person themself, and is kept with the notice they read and the purpose they agreed to. A voice used to recognise someone is sensitive personal data.

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the consent. |
| 2 | UserId | uuid | FK | No | Person who gave the consent. |
| 3 | GrantedAt | timestamptz |  | No | Time the consent was given. |
| 4 | NoticeVersion | string |  | No | Which wording of the notice was shown, so a later change of wording does not rewrite what was agreed. |
| 5 | Purpose | string |  | No | Purpose the person agreed to: suggesting their name for a voice in meetings they attend. |
| 6 | WithdrawnAt | timestamptz |  | Yes | Time the consent was withdrawn; a withdrawn consent is never revived and every sample of that person is erased. |

Entity 34: VoiceSample

One enrolment sample of a person's voice, kept only encrypted, used to suggest that person's name for a voice in the meetings they attend. The recording itself is kept too, encrypted, so its owner can hear it back. Erased with every other sample when the person withdraws consent. A sample the person recorded stands alone; a collected sample carries the collector granted the Voice sample collection permission (the Meeting is kept only for samples collected at a Meeting before ADR-0075).

| No | Field | Data Type | Key | Nullable | Description |
| --- | --- | --- | --- | --- | --- |
| 1 | Id | uuid | PK | No | Unique identifier of the sample. |
| 2 | UserId | uuid | FK | No | Person the voice belongs to. |
| 3 | ConsentId | uuid | FK | No | Consent the sample falls under. |
| 4 | Ciphertext | bytea |  | No | Encrypted embedding: nonce, ciphertext and tag, bound to the person, the sample and the model. |
| 5 | EmbeddingModel | string |  | No | Model that produced the embedding, e.g. eres2net-base-200k@ with the model's checksum; embeddings only compare within one model. |
| 6 | SpeechMs | bigint |  | No | How much speech the sample held after silence was cut, in milliseconds. |
| 7 | AudioCiphertext | bytea |  | Yes | The recording as uploaded, encrypted the same way; null for a sample taken before it was kept. |
| 8 | AudioContentType | string |  | Yes | Media type of the recording, to play it back as it was sent. |
| 9 | CollectedById | uuid | FK | Yes | Holder of the Voice sample collection permission who collected this sample for the person; null for a sample the person recorded. |
| 10 | MeetingId | uuid | FK | Yes | Only kept for a sample collected at a Meeting before ADR-0075; null for every new sample. |

#### 3.3 Data Business Rules

| Rule | Description | Enforced On |
| --- | --- | --- |
| Unique username | A username identifies at most one internal user account. | Account creation/change |
| Unique email | An internal email address identifies at most one account. | Account creation/change |
| Meeting schedule | Each Meeting has planned start and end times; planned end follows planned start. | Meeting creation/update |
| Invitation response | Each invitation has one response: Pending until the invitee answers Accepted or Declined. The answer is recorded once with its response time and is not changed afterwards; the Chair's response is not displayed, and a role change does not reset an earlier answer. The initial participants of a new Meeting are saved with the Meeting in one save, each Pending. | Meeting creation; Invitation response |
| Single meeting lifecycle | A Meeting has exactly one lifecycle. No Meeting Session entity or nested session lifecycle exists. | Meeting lifecycle |
| Agenda ownership | Each Agenda Item belongs to exactly one Meeting. Reordering is within that Meeting. | Agenda management |
| Vote ownership | Each Vote belongs to one Meeting and is not linked to an Agenda Item. | Voting management |
| Vote conclusion ownership | A Vote belongs to one Meeting, whose Minutes incorporate vote tallies and the Chair’s conclusions. | Vote Conclusion creation |
| Minutes scope | Each Meeting has its own Minutes process with versioned revision history. | Minutes governance |
| Meeting audit trail | Meeting lifecycle, approval, publication and permitted schedule changes remain traceable. | Meeting audit |

### 4. User Requirements

#### 4.1 Actors and External Systems

| # | Actor | Description |
| --- | --- | --- |
| 1 | Guest | An external person who does not have an internal user account. A Guest is invited to a specific meeting by name and email and accesses only that meeting through a secure meeting-specific guest invitation and email verification, without signing in to an internal account. Guest access is limited to permitted meeting functions. |
| 2 | Member (Thành viên) | An internal user whose system role may create meetings. A Member creates, updates, submits for approval and publishes the meetings they organise. An Approver or a System Admin can do the same, because both roles also hold the create-meeting permission. |
| 3 | Approver (Phê duyệt viên) | A system-authorized user who may be assigned to review a specific meeting approval request and decide whether to approve or reject it. An approval permission alone does not grant access to all meetings. |
| 4 | System Admin | An administrator responsible for managing user accounts, roles, permissions and system-wide configuration, including viewing relevant business and security audit events. |
| 5 | Chair | A user assigned to chair a meeting, start/end it, control agenda execution, open/close Votes and record their conclusions, and moderate authorized meeting-room interactions. |
| 6 | Secretary | A user assigned to support a specific meeting by preparing agenda items, managing the meeting roster and official attendance, managing authorized meeting documents, uploading the post-meeting audio, reviewing transcripts and summaries, and preparing/finalizing meeting minutes. |
| 7 | Participant | An internal user assigned to participate in a specific meeting. A Participant may perform authorized meeting activities such as joining the online meeting room, viewing or commenting on meeting documents, responding to invitations, and voting. Chair and Secretary are meeting-specific roles with additional responsibilities. |
| 8 | OAuth Provider | An external identity provider used for Google-based authentication and verified identity data exchange. It participates primarily in UC-02 Sign in with Google and supports the authentication flow shown in the context diagram. |
| 9 | AI drafting service | A generative-AI service that writes the three sections of the Meeting draft from the transcript, the agenda and the vote results, supporting UC-58 Generate / Regenerate Meeting Summary. It receives text only, never an audio recording. The team's own server writes the draft for every meeting that is not Normal; for a Normal meeting the administrator may choose an outside AI service, and only then does the transcript leave the system (BR-129, ADR-0056). |
| 10 | Resend Email | An external outbound email service used to deliver password-reset messages, meeting invitations, guest-access emails and notification emails. It supports flows such as UC-07 Reset Password, UC-19 Publish Meeting and notification delivery under FE-13. |

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
| UC-09 | View Role & Permission Matrix | Role-Based Access Control | View system roles and their assigned permissions. |
| UC-10 | Configure Role Permissions | Role-Based Access Control | Configure permissions assigned to a system role. |
| UC-11 | Manage Roles | Role-Based Access Control | Create, update, or delete custom roles; see the detailed UC-11a–UC-11c specifications. |
| UC-12 | View Audit Log | Role-Based Access Control | Review recorded system events for security, accountability and operational investigation. |
| UC-13 | Create Meeting | Meeting Management | Create one independent Meeting with its planned start and end times, mode, location, confidentiality, other required details and its initial participants. |
| UC-14 | Update Meeting | Meeting Management | Update Meeting information while Draft/Rejected, or reschedule a Published Meeting before it begins when authorized. |
| UC-15 | View Meeting Details | Meeting Management | View authorized Meeting details, including planned schedule, actual start/end times, Meeting lifecycle status and, for a Cancelled Meeting, its cancellation reason. |
| UC-16 | View Meeting List | Meeting Management | View, search and filter Meetings by their own schedule, status (including Cancelled), name and confidentiality level. The list opens filtered to In Progress Meetings; the time filter offers Today, This week, Not yet held and Already held. |
| UC-17 | Submit Meeting for Approval | Meeting Management | Submit a Draft or Rejected Meeting with a valid planned schedule for approval. |
| UC-18 | Review Meeting Approval | Meeting Management | Approve or reject an assigned meeting approval request. |
| UC-19 | Publish Meeting | Meeting Management | Publish an Approved Meeting with valid planned time and required Chair/Secretary assignments, and initiate invitations. |
| UC-20 | Start Meeting | Meeting Management | Start a Published Meeting and record actual start time. |
| UC-21 | End Meeting | Meeting Management | End an InProgress Meeting and record actual end time. |
| UC-22 | Cancel Meeting | Meeting Management | The Member cancels a Meeting that will no longer take place, before it starts, with a required reason; invitees of a Published Meeting are notified. |
| UC-23 | Manage Agenda Items | Agenda Management | Manage Agenda planning as Secretary: add Pending Items at the end; edit Title, Presenter and Description in any status; delete only Pending Items; reorder Items in any status. The Presenter is a current participant or Guest and is notified once the Meeting is published. |
| UC-24 | Set Current Agenda Item | Agenda Management | Chair starts any Pending Item as the current Item when no Item is InProgress. |
| UC-25 | Complete Agenda Item | Agenda Management | Chair completes the current Item at any time, records its actual end time and does not automatically start the next Item. |
| UC-26 | View Meeting Agenda | Agenda Management | Authorized viewers see the complete Agenda and automatically receive its successfully saved changes. |
| UC-27 | Record Agenda Item Conclusion | Agenda Management | The current Chair or Secretary records or updates what was concluded for an InProgress or Done Agenda Item, until the minutes draft is generated; minutes include each Item conclusion. |
| UC-29 | View Meeting Participants | Meeting Participant Management | View the current internal participants and external Guests associated with a meeting, together with their meeting-specific role or guest access type. |
| UC-30 | Manage Meeting Participants | Meeting Participant Management | Add participants after the Meeting is created (the initial participants are chosen in UC-13), manage meeting roles and additional permissions, replace the Chair before publication, or remove participants; see UC-30a–UC-30c. From publication onward the Chair hands over the role through UC-34. |
| UC-31 | Respond to Meeting Invitation | Meeting Participant Management | Allow an invited internal participant (other than the Chair) or external Guest to accept or decline their own Meeting invitation once; the answer cannot be changed afterwards. |
| UC-32 | View Participant Response Status | Meeting Participant Management | View the invitation response of current internal participants and external Guests as a label beside each name. |
| UC-33 | Record Attendance | Meeting Participant Management | Record or revise Present/Absent attendance for one or more selected current internal participants or external Guests while the Meeting is InProgress. |
| UC-34 | Hand Over Chair to Another Participant | Meeting Participant Management | The current Chair gives the Chair role to another internal participant in one step; the former Chair becomes a Member. |
| UC-35 | View Meeting Documents | Personal Document Library & Meeting Document Management | View Meeting documents through UC-35a and preview or download through UC-35b. Anyone who can view the Meeting can read its documents; Secret Meeting documents cannot be downloaded. |
| UC-36 | Manage Meeting Documents | Personal Document Library & Meeting Document Management | Manage Meeting documents through UC-36a Add one or more documents, UC-36b Attach documents to an Agenda Item, UC-36c Remove from Meeting and UC-36d Detach from one Item. Current Chair or Secretary acts before Meeting end; documents are attached from the Meeting, the computer or My Documents, and a multiple-document submission succeeds as a whole or changes nothing. Item status does not lock links. |
| UC-37 | View Personal Document Library | Personal Document Library & Meeting Document Management | Browse the user's personal document library, search or filter documents and view permitted Meeting-usage details. |
| UC-37a | View a Deactivated Account’s Personal Library | Personal Document Library & Meeting Document Management | A System Admin reads, read-only, the folders and documents in the personal library of a deactivated account. Every read is written to the audit log. |
| UC-38 | Comment on Meeting Document | Personal Document Library & Meeting Document Management | Read and add Meeting-document comments with the same viewing access, before Meeting end. After end, documents and comments are read-only. |
| UC-39 | Upload Document to Personal Library | Personal Document Library & Meeting Document Management | Upload one or more PDF or DOCX files together to the user's personal library, each at most 10 MB. The whole submission succeeds or no documents are added. |
| UC-40 | Manage Personal Documents | Personal Document Library & Meeting Document Management | Delete, rename or move personal-library documents through UC-40a, UC-40b and UC-40c, preserving Meeting documents already added. |
| UC-41 | Manage Library Folders | Personal Document Library & Meeting Document Management | Create, rename, move or delete personal-library folders through UC-41a to UC-41d; only empty folders may be deleted. |
| UC-42 | Confirm Document Read | Personal Document Library & Meeting Document Management | A person who may vote confirms reading a Vote document after scrolling to its last page; required before casting a ballot. |
| UC-43 | Join Meeting Room | Online Meeting Room Management | Join the online room of an authorized Online or Hybrid meeting; an external Guest may join through a verified meeting-specific guest invitation without an internal account login. |
| UC-44 | Leave Meeting Room | Online Meeting Room Management | Leave the online meeting room without changing the Meeting lifecycle. |
| UC-45 | Manage Meeting Room Controls | Online Meeting Room Management | Control microphone, camera, screen sharing and the audio and video devices within the Meeting Room; see UC-45a–UC-45d. |
| UC-46 | View Meeting Room Participants | Online Meeting Room Management | View the users currently connected to the online meeting room and their current room presence. |
| UC-47 | Moderate Meeting Room | Online Meeting Room Management | Allow the current Chair to mute another person's microphone in the meeting room; muting is one-way and the person may turn the microphone on again. |
| UC-48 | Use In-room Chat | Online Meeting Room Management | Exchange text messages with authorized users currently connected to the online meeting room. |
| UC-49 | Keep Meeting in a Floating Window | Online Meeting Room Management | A person connected to the online room keeps the call in a small always-on-top window while working in other tabs and brings it back to the main window; the floating window uses the same room connection. |
| UC-50 | Register to Speak | Online Meeting Room Management | A person in the online room raises a hand to ask for the floor; the Chair sees the raised hands in order, invites one person at a time to speak and closes each turn. |
| UC-51 | View & Play Meeting Recordings | Meeting Recording | View meeting recordings and play room recordings or per-mic speech clips; see UC-51a–UC-51c. |
| UC-52 | Record Room Audio | Meeting Recording | Record room audio through the browser or upload an audio file; see UC-52a–UC-52e. |
| UC-53 | Manage My Recorded Speech | Meeting Recording | View sentences recorded from the user's own microphone, listen to playback and withdraw/delete personal speech. |
| UC-54 | View Meeting Transcript | Transcription, Speaker Identification & Meeting Summary | View and export the transcript of meeting recordings, and run their transcription; see UC-54a and UC-54b. |
| UC-55 | Edit Transcript & Speaker Attribution | Transcription, Speaker Identification & Meeting Summary | Edit transcript text, assign speakers to voices, exclude sentences or delete sentences; see UC-55a–UC-55d. |
| UC-56 | Manage My Voice ID | Transcription, Speaker Identification & Meeting Summary | Manage personal voice biometric consent, record and listen to voice samples, or withdraw consent; see UC-56a–UC-56f. |
| UC-57 | Collect Voice Samples for Attendees | Transcription, Speaker Identification & Meeting Summary | A holder of the Voice sample collection permission collects voice samples for a person on the Users screen: view the collection (UC-57a), record a sample (UC-57b), play a collected sample (UC-57c) and delete a collected sample (UC-57d). |
| UC-58 | Generate / Regenerate Meeting Summary | Transcription, Speaker Identification & Meeting Summary | Ask the AI for a draft of the Meeting content in three sections (I. Objectives and scope, II. Discussion and assessment, III. Conclusions and assignments) from the transcript, the agenda and the vote results; asking again replaces the draft. |
| UC-59 | View Meeting Summary | Transcription, Speaker Identification & Meeting Summary | Read the AI draft, its AI label and its review state; before review only the current Chair and Secretary see it, and after review every reader of the Meeting does. |
| UC-60 | Edit Meeting Summary | Transcription, Speaker Identification & Meeting Summary | Correct the text of the three sections; saving withdraws the review until the draft is approved again. |
| UC-61 | Approve Meeting Summary | Transcription, Speaker Identification & Meeting Summary | Approve the draft so the other participants can read it and the minutes can use sections I-III. |
| UC-62 | View Meeting Votes | Voting Management | View visible Meeting Votes and their status or result through UC-62a and UC-62b. |
| UC-63 | Create / Configure Vote | Voting Management | Allows the current Chair or Secretary to prepare a Vote draft and configure or delete an existing draft; only the Chair may proceed to opening the Vote. Draft preparation is allowed before the Meeting starts, while it is Approved or Published, and during InProgress. Each Vote belongs directly to its Meeting; its documents are managed through UC-68. |
| UC-64 | Open Vote | Voting Management | Allows the current Chair to open a valid Vote during an InProgress Meeting. Opening fixes the eligible-voter list and count. Only one Vote may be Open in a Meeting at a time. |
| UC-65 | Cast Vote | Voting Management | Allows an eligible person to submit one choice in an Open Vote during the Meeting, after confirming reading of every Vote document. Eligibility comes from the list fixed at opening and requires current Meeting access. Attendance is not a condition. A saved ballot cannot be changed or withdrawn. |
| UC-66 | Close Vote | Voting Management | Allows the current Chair to close an existing Open Vote and obtain its tally while the Meeting is InProgress or Ended. Closing a Vote does not end the Meeting and does not determine the Chair's conclusion. |
| UC-67 | Record Vote Conclusion | Voting Management | Allows the current Chair to record the business conclusion of a Closed and tallied Vote as Passed, NotPassed or Deferred, with an optional note. The Chair decides the conclusion; the system records it rather than inferring it from a majority. |
| UC-68 | Manage Vote Documents | Voting Management | Attach documents to, or detach them from, a Draft Vote through UC-68a and UC-68b; documents come from the Meeting, the computer or My Documents. |
| UC-69 | View Meeting Minutes | Meeting Minutes & Approval | Read the latest draft of the minutes as the paper that will be printed; only the current Chair and Secretary see a draft. |
| UC-70 | Generate Meeting Minutes | Meeting Minutes & Approval | Build the minutes from one fixed template (Decree 30, Mẫu 1.4) and the Meeting records, using the approved AI draft for sections I-III. |
| UC-71 | Edit Meeting Minutes | Meeting Minutes & Approval | Correct the content of the draft in a word-processor editor with a live preview, save it, and download the same content as a Word or PDF file; the minutes of a Secret Meeting cannot be downloaded. |
| UC-72 | Submit Meeting Minutes for Approval | Meeting Minutes & Approval | Send the finished minutes to the current Chair for approval; the revision is locked until the Chair approves or returns it. |
| UC-73 | Approve and Issue Meeting Minutes | Meeting Minutes & Approval | The current Chair approves the minutes waiting for approval; approval issues them at once. |
| UC-74 | Return Meeting Minutes for Revision | Meeting Minutes & Approval | The current Chair returns the minutes with a reason so the Secretary can correct and submit them again. |
| UC-75 | Correct Issued Meeting Minutes | Meeting Minutes & Approval | Start a correction of issued minutes as a new revision; the issued revision stays readable until the correction is issued. |
| UC-76 | View Notifications | Email & In-App Notification | View personal notifications and the unread notification count. |
| UC-77 | Mark Notifications as Read | Email & In-App Notification | Mark one notification or all unread notifications as read; see UC-77a–UC-77b. |
| UC-78 | Open Notification Target | Email & In-App Notification | Open the meeting linked to a selected notification. |
| UC-79 | View & Export Reports | Reporting & Statistics | View reporting statistics and export the corresponding report; see UC-79a–UC-79b. |
| UC-80 | Manage Calendar Sharing | Working Calendar & Meeting Time Search | Manage calendar sharing through UC-80a request, UC-80b direct share, UC-80c response, UC-80d change, UC-80e withdrawal or end and UC-80f viewing the share lists; see the detailed UC-80a–UC-80f specifications and ADR-0058. |
| UC-80f | View Calendar Shares | Working Calendar & Meeting Time Search | See the calendar shares you take part in, with state and the actions you can take now. |
| UC-81 | Find Meeting Time | Working Calendar & Meeting Time Search | Find common free slots through UC-81a, create a Draft meeting from a slot through UC-81b and view shared calendars through UC-81c; see the detailed UC-81a–UC-81c specifications and ADR-0058. |

Feature priority. The features are prioritised with the Product Owner: FE-01 to FE-13 are Must-have and FE-15 to FE-16 are Should-have. FE-16 follows ADR-0058: calendar sharing shows busy or free only, and there is no AI assistant.

| Feature | Name | Priority |
| --- | --- | --- |
| FE-01 | Authentication & Account Security | Must-have |
| FE-02 | User & Profile Management | Must-have |
| FE-03 | Role-Based Access Control | Must-have |
| FE-04 | Meeting Management | Must-have |
| FE-05 | Agenda Management | Must-have |
| FE-06 | Meeting Participant Management | Must-have |
| FE-07 | Personal Document Library & Meeting Document Management | Must-have |
| FE-08 | Online Meeting Room Management | Must-have |
| FE-09 | Meeting Recording | Must-have |
| FE-10 | Transcription, Speaker Identification & Meeting Summary | Must-have |
| FE-11 | Voting Management | Must-have |
| FE-12 | Meeting Minutes & Approval | Must-have |
| FE-13 | Email & In-App Notification | Must-have |
| FE-15 | Reporting & Statistics | Should-have |
| FE-16 | Working Calendar & Meeting Time Search | Should-have |

#### 4.3 Use Case Diagrams

Figure 7 shows the whole system: each actor is linked to the features (FE) whose use cases it takes part in. Chair and Secretary are kinds of Participant, and Approver and System Admin are kinds of Member. The diagrams in 4.3.1 to 4.3.7 show the use cases of each actor.

Figure 7: System Overview Use Case Diagram

View the full-size image on Google Drive

##### 4.3.1 UCs for System Admin

The System Admin manages user accounts (create, update, deactivate/reactivate, reset password, assign system roles), configures roles and permissions, and reviews the audit log. Because the System Admin role also holds the create-meeting permission, the Admin can create, update, submit and publish meetings as a Member does (Figure 7). The Admin can also view meetings and their records, reports and statistics, notifications and the personal document library.

Figure 8: Use Case Diagram – System Admin

View the full-size image on Google Drive

##### 4.3.2 UCs for Member

The Member creates and updates meetings, submits them for approval and publishes approved meetings, adds participants and Guests. The Member can also view meeting records, comment on documents, use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace. The Member shares calendars and finds common free slots (UC-80, UC-81).

Figure 9: Use Case Diagram – Member

View the full-size image on Google Drive

##### 4.3.3 UCs for Approver

The Approver reviews assigned meeting approval requests and approves or rejects them. Because the Approver role also holds the create-meeting permission, the Approver can create, update, submit and publish meetings as a Member does (Figure 7). The Approver can view meeting details and related records (agenda, documents, media, votes and minutes), use the online room (controls, room participants, in-room chat, leave room), and view reports, notifications and the personal workspace.

Figure 10: Use Case Diagram – Approver

View the full-size image on Google Drive

##### 4.3.4 UCs for Chair

The Chair starts and ends the meeting, steers the agenda (set current / complete item), joins and moderates the online room, and creates, opens and closes votes and records each vote's conclusion. The Chair does not respond to the invitation because the Chair runs the meeting. The Chair also adds and removes meeting documents, comments on meeting documents, and can view all records of the chaired meeting. The current assigned Chair may cast a vote (UC-65) while the Meeting is InProgress and the Vote is Open, under BR-090 and BR-092.

Figure 11: Use Case Diagram – Chair

View the full-size image on Google Drive

##### 4.3.5 UCs for Secretary

The Secretary prepares the meeting: agenda items, participants and meeting roles, invitation responses, attendance and meeting documents, and also responds to the invitation and takes part in the online room. After the meeting, the Secretary uploads audio, corrects the AI speaker-attributed transcript, generates, edits, approves and publishes the summary, and generates, edits and submits the minutes for approval. The current assigned Secretary may cast a vote (UC-65) while the Meeting is InProgress and the Vote is Open, under BR-090 and BR-092.

Figure 12: Use Case Diagram – Secretary

View the full-size image on Google Drive

##### 4.3.6 UCs for Participant

The Participant responds to the meeting invitation, joins the online room, views meetings, agenda, documents and media, comments on documents and casts votes when eligible. The Participant can also view reports, notifications and the personal workspace.

Figure 13: Use Case Diagram – Participant

View the full-size image on Google Drive

##### 4.3.7 UCs for Guest

The Guest verifies the invited email by OTP, then supplies a self-declared display name and optional job title. In the invited Meeting, the Guest participates like a Member: room media/chat/list, all attached documents and comments, invitation response, voting and attendance inclusion, plus separately granted roster/response viewing. The Guest cannot be Chair/Secretary, receive delegated authority, moderate the room or use a personal library. (BR-055)

Figure 14: Use Case Diagram – Guest

View the full-size image on Google Drive

#### 4.4 Permission Matrix

Full = unrestricted  ·  Restricted = conditions apply (see footnotes)  ·  No = denied

| Entity | Action | System Admin | Member | Approver | Chair | Secretary | Participant | Guest |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| User account | View / search | Full | No | No | No | No | No | No |
| User account | Create | Full | No | No | No | No | No | No |
| User account | Update | Full | No | No | No | No | No | No |
| User account | Deactivate / Reactivate | Full | No | No | No | No | No | No |
| User account | Reset password | Full | No | No | No | No | No | No |
| User account | Assign system role | Full | No | No | No | No | No | No |
| Role & permission | View matrix | Full | No | No | No | No | No | No |
| Role & permission | Configure permissions | Full | No | No | No | No | No | No |
| Role & permission | Create role | Full | No | No | No | No | No | No |
| Role & permission | Update role | Restricted ¹ | No | No | No | No | No | No |
| Role & permission | Delete role | Restricted ¹ | No | No | No | No | No | No |
| Audit log | View | Full | No | No | No | No | No | No |
| Authentication | Sign in (email / Google) / Sign out | Full | Full | Full | Full | Full | Full | No |
| Authentication | Access meeting via Guest invitation | No | No | No | No | No | No | Full |
| Meeting | Create | Full | Full | Full | No | No | No | No |
| Meeting | Update / Reschedule | Restricted ² | Restricted ² | Restricted ² | No | No | No | No |
| Meeting | View list | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Meeting | View details | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Meeting | Submit for approval | Restricted ² | Restricted ² | Restricted ² | No | No | No | No |
| Meeting | Approve / Reject | No | No | Restricted ⁴ | No | No | No | No |
| Meeting | Publish | Restricted ⁵ | Restricted ⁵ | Restricted ⁵ | No | No | No | No |
| Meeting | Start / End | No | No | No | Restricted ⁶ | No | No | No |
| Agenda item | View | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Agenda item | Create / Update / Delete / Reorder | No | No | No | No | Restricted ⁸ | No | No |
| Agenda item | Set current / Complete | No | No | No | Restricted ⁹ | No | No | No |
| Participant | View participants / response status | No | No | No | Full from Published onward | Restricted ³: roster under existing rules; responses from Published onward | Restricted ³: from Published, each view requires its matching additional permission | Restricted ¹¹: from Published, each view requires its matching additional permission |
| Participant | Add participant / Invite Guest | Restricted ³ | Restricted ³ | Restricted ³ | No | Restricted ³ | No | No |
| Participant | Manage meeting role | No | No | No | No | Restricted ³ | No | No |
| Participant | Remove participant / Revoke Guest | No | No | No | No | Restricted ³ | No | No |
| Invitation response | Respond (accept / decline) | No | No | No | No | Restricted ¹⁰ | Restricted ¹⁰ | Restricted ¹¹ |
| Attendance | Record | No | No | No | No | Restricted ³ (InProgress only) | No | No |
| Personal document library | View | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Personal document library | Upload / Organize (folders, rename, move, delete) | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Personal document library | View deactivated account library (System Admin only) | Restricted ²⁰ | No | No | No | No | No | No |
| Meeting document | View list | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹³ |
| Meeting document | Add / Remove document; Link / Unlink Agenda Item | No | No | No | Restricted ³: current Chair, before Meeting end | Restricted ³: current Secretary, before Meeting end | No | No |
| Meeting document | Preview / Download (no download in a Secret Meeting) | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Meeting document | Comment before Meeting end | Full: only when the document is readable | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ¹¹: Member rules within the invited Meeting |
| Audio recording | View media library / Play | Full | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | Restricted ³ | No |
| Audio recording | Upload | No | No | No | No | Restricted ³ | No | No |
| Transcript | View / Edit (speaker attribution) | No | No | No | No | Restricted ³ | No | No |
| Voice ID | Collect voice samples for attendees | No | No | No | No | Restricted | No | No |
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
| Minutes | Submit for approval | No | No | No | No | Restricted ¹⁵ | No | No |
| Notification | View | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Notification | Mark as read / Mark all / Open target | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | Restricted ¹² | No |
| Report & statistics | View | Full | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | No |
| Report & statistics | Export | Full | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | Restricted ¹⁸ | No |
| Online meeting room | Join | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Leave | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Microphone / Camera / Screen sharing | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | View room participants / In-room chat | No | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹⁹ | Restricted ¹¹ |
| Online meeting room | Moderate | No | No | No | Restricted ¹⁹ | No | No | No |

Additional Meeting permissions supplement the permissions provided by an existing Meeting role. ViewParticipants and ViewResponses are examples, not a closed set. The final permission catalogue and grant matrix remain to be specified. Guest grants follow the same principle subject to the approved Guest exclusions. (BR-055)

Notes:

¹ Only custom roles; built-in system roles cannot be modified or deleted, and a role can be deleted only when no user is assigned to it.

² Only meetings the user created. Submit for approval and general information edits are permitted only in Draft or Rejected. A Published Meeting may be rescheduled before it starts under UC-14 and BR-015; only planned times, location and optional Online Link may change, and the new planned start must be later than the current time.

³ Only Meetings within the user’s authorized relationship and lifecycle scope: Organizer at any status; assigned Approver from WaitingApproval onward; active assigned Secretary before and after publication (BR-016, BR-017, BR-030); Chair at any status, or Participant from Published onward. Each action still requires its own permission and status checks. Guests do not use the internal Meeting list.

⁴ Only approval requests assigned to the user while the meeting is Pending Approval.

⁵ Only meetings the user created that have been Approved.

⁶ Only in the meeting the user chairs and only when the lifecycle state allows it (start a Published meeting and end an InProgress meeting).

⁸ Only the assigned Secretary may create, update, delete or reorder Agenda Items while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Agenda management is prohibited in Ended and all subsequent lifecycle states. This permission does not grant Chair-only agenda execution actions.

⁹ Only the active assigned Chair may set or complete the current Agenda Item while the Meeting is InProgress; at most one Item may be InProgress.

¹⁰ Only the user's own invitation, while the Meeting is Published or In Progress; each invitation is answered once and the answer cannot be changed afterwards. The Chair does not respond. (BR-046)

¹¹ Only within a valid meeting-scoped Guest session, established by email-code verification (UC-04), for the meeting the Guest was invited to.

¹² Only the user's own items (own notifications, own personal documents).

¹³ Anyone who can view a Meeting can read all its current documents; there is no separate document-reading permission or publication gate. Guests still require a valid verified invitation for that Meeting. Secret Meeting documents cannot be downloaded.

¹⁴ Only people on the eligible list fixed when the Vote opens and retaining current Meeting access may cast a ballot, and only while the Meeting is InProgress and the Vote is Open. A role change does not change that list. Guest email verification and invitation validity are required. (BR-092, BR-093, BR-091)

¹⁵ Only in the assigned meeting while the minutes are in Draft or Revision Requested status.

¹⁸ Statistics and exports are limited to meetings within the user's own scope, for the System Admin as for anyone else. (BR-149, BR-150, PO 09/11/2026 Q1).

¹⁹ Join requires a valid current participant assignment for an Online or Hybrid Meeting whose lifecycle permits room entry (UC-43). Organizer or Approver status alone does not grant room entry. Leave, controls, room participants and chat require a connected authorized user.

### 5. System Functionalities

#### 5.1 Screen Inventory

##### a. Screen Flow

Figure 15: Screen Flow

View the full-size image on Google Drive

##### b. Screen List

| # | Screen Name | Feature | Description |
| --- | --- | --- | --- |
| 1.1 | Sign In | FE-01 Authentication & Account Security | Allows every internal user (System Admin, Member, Approver, Chair, Secretary, Participant) to sign in with email and password or Google, see the redirect reason and sign-in errors, and sign out. |
| 1.2 | Reset Password | FE-01 Authentication & Account Security | Allows the account owner who opened the one-time, 60-minute reset link emailed by the System Admin to set a new password, which signs out all devices; invalid links show an error page. |
| 1.3 | Guest Verification | FE-01 Authentication & Account Security | Allows a Guest to verify the invited email with a one-time code, then enter a self-declared display name and optional job title before using Member participation functions in this Meeting. (UC-04, BR-055) |
| 2.1 | User List | FE-02 User & Profile Management | Allows the System Admin to search and filter user accounts by role and status, create accounts, and edit, deactivate/reactivate or send a password reset link to each account. |
| 2.2 | Create / Edit User Account Dialog | FE-02 User & Profile Management | Allows the System Admin to create a new account with full name, email, a generated initial password and system role, or edit an existing account’s full name, email and system role. |
| 2.3 | Send Password Reset Link Dialog | FE-02 User & Profile Management | Allows the System Admin to confirm sending a one-time, 60-minute password reset link to a user’s registered email; the same dialog style confirms deactivating an account. |
| 3.1 | Role List | FE-03 Role-Based Access Control | Allows the System Admin to view roles with their account counts and to create, edit and delete custom roles (name, description, initial permissions), with Default roles protected from change. |
| 3.2 | Permission Matrix | FE-03 Role-Based Access Control | Allows the System Admin to view and grant or revoke permissions for each role in a role-by-permission matrix and to save or undo the changes row by row. |
| 3.3 | System Audit Log | FE-03 Role-Based Access Control | Allows the System Admin to view, filter and search the read-only log of business and security events, and open the details recorded with each entry. |
| 4.1 | Meeting Calendar – Calendar View | FE-04 Meeting Management | Allows any internal user related to a meeting to browse accessible Meetings on a weekly day/hour grid, placed by their planned start and end time and coloured by status, and open one. |
| 4.2 | Meeting Calendar – List View | FE-04 Meeting Management | Allows any internal user related to a meeting to view the accessible Meetings of a week as cards grouped by day, with planned time, mode, location, status and confidentiality level, open one, and answer the attendance question of a Published or In Progress Meeting directly on its card. Meetings the user has declined are not listed. |
| 4.3 | Meeting List | FE-04 Meeting Management | Allows any internal user related to a meeting to view, search, filter by status, confidentiality level and time (Today, This week, Not yet held, Already held), and page through the Meetings s/he organizes, is invited to or must approve, and open a Meeting. The list opens filtered to In Progress Meetings. |
| 4.4 | Create / Edit Meeting Dialog | FE-04 Meeting Management | Allows the Member to create a Draft Meeting in a dialog with its name, description, planned date and start/end time, mode, confidentiality level, location, optional online link and initial participants with their Meeting Roles, or edit a Draft or Rejected Meeting in a dialog (without the participants). |
| 4.5 | Meeting Detail – General Information | FE-04 Meeting Management | Allows any internal user related to a meeting to view its schedule, mode, location, online link, confidentiality, organizer, approver, description, lifecycle progress, readiness checklist and status history, run the status-dependent actions, and (except the Chair) answer the attendance question of a Published or In Progress Meeting. |
| 4.6 | Submit Meeting for Approval | FE-04 Meeting Management | Allows the Member to submit a complete Draft Meeting, or resubmit a Rejected one, by choosing an eligible Approver and adding an optional message, after which the Meeting is locked. |
| 4.7 | Approve / Reject Meeting | FE-04 Meeting Management | Allows the assigned Approver to review a Pending Approval Meeting read-only and approve it, or reject it with a mandatory reason that is sent to the Member. |
| 4.8 | Publish Meeting | FE-04 Meeting Management | Allows the Member to publish an Approved Meeting after checking that an active Chair and Secretary are assigned, which sends invitations to internal participants and external Guests. |
| 4.9 | Reschedule Meeting | FE-04 Meeting Management | Allows the Member to reschedule a Published Meeting that has not started by changing only its date, time, location and online link, and notify all invitees; the Chair can start the Meeting here. |
| 5.1 | Meeting Detail – Meeting Content Tab | FE-05 Agenda Management | Allows users related to a meeting to view its agenda items with presenter, duration and planned time, and the Secretary to add, edit, delete and reorder items while the meeting is Draft or Rejected. |
| 5.2 | Add / Edit Agenda Item Drawer | FE-05 Agenda Management | Allows the Secretary to add or edit an agenda item of a Draft or Rejected meeting with content, presenter, duration, note, a needs-vote flag and documents chosen from the meeting documents. |
| 5.3 | Meeting Facilitation | FE-05 Agenda Management | Allows the Chair to run an InProgress meeting: set the current agenda item, complete it with actual times recorded, create a vote for the item, join the online room and end the meeting. |
| 6.1 | Meeting Detail – Participants Tab | FE-06 Meeting Participant Management | Allows the Secretary to manage the roster, roles and permissions, shows each invitee’s response as a label beside the name, and lets the Chair hand over the Chair role; the Chair and authorized Members have read access under BR-017. |
| 6.1b | Change Role Dialog | FE-06 Meeting Participant Management | Allows the Secretary (or the Chair, to hand over the Chair role) to change the Meeting Role of one internal participant by choosing one role from a list that describes what each role allows; choosing Chair shows Hand over Chair. |
| 6.2 | Add Participant Dialog | FE-06 Meeting Participant Management | Allows the Secretary to add internal participants together with a shared Meeting Role and optional permissions, or invite one Guest; the Organizer may assign the missing Secretary. |
| 6.3 | Attendance Check | FE-06 Meeting Participant Management | Allows the Secretary to record Present or Absent for any current internal participant or Guest in the Attendance column of the Participants tab during an InProgress Meeting; choosing a value saves it. Other participants read the attendance. No totals are shown. |
| 6.4 | Invitation Response | FE-06 Meeting Participant Management | Allows an invited Secretary, Participant or verified Guest to view the invitation on a phone and accept or decline it once; the answer cannot be changed afterwards and the Chair does not answer. |
| 7.1 | Document Library – Empty State | FE-07 Personal Document Library & Meeting Document Management | Shows the Document Library of a user who has no folders or files yet: an empty-state panel explaining how the library is used and the accepted file types, with New folder and Upload buttons at the top of the page and inside the panel. |
| 7.2 | Personal Document Library | FE-07 Personal Document Library & Meeting Document Management | Allows an internal user to browse folders, search and filter his/her private documents by file type, upload files, organise files and folders, delete files and see where each document is used in meetings. |
| 7.3 | Meeting Detail – Documents Tab | FE-07 Personal Document Library & Meeting Document Management | Allows users who can view a meeting to see its documents grouped by Agenda Item, Vote or general, with page and comment counts, and the Chair or Secretary to add, link, unlink and remove documents until the meeting ends. |
| 7.4 | Add Document to Meeting Dialog | FE-07 Personal Document Library & Meeting Document Management | Allows the Chair or Secretary to add documents to a meeting from the computer (up to 10 files and 100 MB at a time) or from the personal library, generally or for one Agenda Item. |
| 7.5 | Document Viewer – Preview & Comments | FE-07 Personal Document Library & Meeting Document Management | Allows users who can view a meeting to preview a meeting document page by page, download it unless the Meeting is Secret, and read, write and reply to comments. |
| 8.1 | Meeting Lobby | FE-08 Online Meeting Room Management | Allows the Chair, Secretary, Participant or a verified Guest to preview camera and microphone, choose audio and video devices and enter the online room of an In Progress Online or Hybrid meeting. |
| 8.2 | Online Meeting Room – Video Grid | FE-08 Online Meeting Room Management | Allows users connected to the online room to see participants in a video grid, see who is speaking, turn their own microphone and camera on or off, share the screen, use a floating window, open the side panels and leave the room. |
| 8.3 | In-Room Participants Panel | FE-08 Online Meeting Room Management | Allows users in the online room to view who is connected with their meeting role, who is speaking and who is muted, allows the Chair to mute a participant, and, in a separate Attendance panel, lets the Secretary record Present or Absent without leaving the room. |
| 8.4 | Screen Sharing & Chat | FE-08 Online Meeting Room Management | Allows users connected to the online room to present their screen on the main stage, follow a shared screen and exchange text chat messages that are not kept after the meeting. |
| 8.5 | In-Room Voting Panel | FE-08 Online Meeting Room Management | Planned: allows eligible voters in the online room to follow the open Vote and cast a ballot without leaving the room. |
| 8.6 | In-Room Documents Panel | FE-08 Online Meeting Room Management | Built: allows users in the online room to read the meeting documents, current Agenda Item first, opening each document in a new tab so the call continues. |
| 8.7 | In-Room Attendance Panel | FE-08 Online Meeting Room Management | Allows the Secretary to record Present or Absent for any current internal participant or Guest from a separate Attendance panel without leaving the online room; choosing a value saves it. Other users see the same attendance read-only. No totals are shown. |
| 9.1 | Meeting Detail – Recordings Tab | FE-09 Meeting Recording | Allows the Secretary to upload the meeting audio after the meeting has ended, see processing status and uploaded files, review the speaker-attributed transcript and start generating the AI summary. |
| 9.2 | Upload Audio Dialog | FE-09 Meeting Recording | Allows the Secretary to select or drag an MP3, M4A or WAV audio file of up to 300 MB, confirm recording consent and upload it to an Ended meeting for transcription. |
| 10.1 | Speaker-Attributed Transcript | FE-10 Transcription, Speaker Identification & Meeting Summary | Allows the Secretary to play the uploaded meeting audio, search and correct the transcript text, and assign unknown voices to meeting attendees before generating the meeting summary. |
| 10.2 | Meeting Summary | FE-10 Transcription, Speaker Identification & Meeting Summary | Allows the current Chair and the current Secretary to read the AI draft of the Meeting content in three sections, ask for it again, correct it and approve it; other participants read it once it is approved. The minutes use only an approved draft. The summary is not published outside the Meeting. |
| 11.1 | Meeting Detail – Voting Tab | FE-11 Voting Management | Allows the Chair to view all Votes of an InProgress Meeting with their status and turnout, create, edit, delete and open Draft Votes, and close an open Vote; other involved users view read-only. |
| 11.2 | Create & Open Vote Dialog | FE-11 Voting Management | Allows the Chair to define a Vote for the current Agenda Item of an InProgress Meeting – content, options, ballot type and eligible voter roles – and save it as a draft or open it immediately. |
| 11.3 | Cast Vote | FE-11 Voting Management | Allows an eligible Chair, Secretary, Participant or verified Guest to view an open Vote of the InProgress Meeting, choose one option and submit a single ballot that cannot be changed afterwards. |
| 11.4 | Vote Result & Conclusion | FE-11 Voting Management | Allows the Chair to view the tallied result of a closed Vote, including public ballots, and record its conclusion – Passed, NotPassed or Deferred – with a note carried into the minutes. |
| 12.1 | Minutes Editor | FE-12 Meeting Minutes & Approval | Allows the current Chair and the current Secretary to build the minutes from one fixed template (Decree 30, Mẫu 1.4) and the Meeting records, correct the plain text with a live preview, save it, and download it as a Word or PDF file; the minutes of a Secret Meeting cannot be downloaded. The Secretary sends the minutes to the Chair for approval; the Chair approves and issues them, or returns them with a reason. The minutes carry no digital signature. |
| 12.2 | Finalized Meeting Minutes | FE-12 Meeting Minutes & Approval | Allows users involved in the Meeting to view the issued, read-only minutes and download the minutes as a PDF. |
| 13.1 | Notification Panel | FE-13 Email & In-App Notification | Allows every signed-in internal user to view personal in-app notifications and the unread count, mark one or all as read, and open the meeting a notification refers to. |
| 13.2 | Meeting Invitation Email | FE-13 Email & In-App Notification | Allows an invited internal participant or external Guest to read the meeting invitation – time, location, Chair and role – and open the response or meeting details from the email. |
| 15.1 | Reports & Statistics | FE-15 Reporting & Statistics | Allows users to view meeting statistics for a chosen period – meeting count, attendance rate, average duration and average turnaround, monthly and status breakdowns – and export them to Excel. |

#### 5.2 External API Inventory

| # | API Name | Calling Path | Description |
| --- | --- | --- | --- |
| 1 | Google Identity Services (Sign in with Google, browser SDK) | Sign-in page → Google Identity Services SDK in the browser → Google ID token returned to the SPA | Google's sign-in popup authenticates the user's Google account in the browser and hands the SPA a signed ID token; no client secret and no server-side OAuth redirect is used (API-01). |
| 2 | Google ID token verification (OpenID Connect) | SPA → TrustCabinet backend (Google sign-in) → Google's published signing keys | The backend verifies the Google ID token (signature, issuer, audience = configured Client ID, expiry, verified email) and signs in the active account with the same email; no account is created this way (API-02). |
| 3 | Email delivery – Resend email API, or an SMTP server | Email queue worker, password reset and guest verification → Resend Email or SMTP server | Sends the system's emails: meeting invitations, updates and cancellations with an .ics calendar file, meeting reminders, the issued-minutes notice and guest presenter notices from the email queue with retries; password-reset links and guest verification codes are sent at once (API-03). |
| 4 | Email delivery webhook (Resend) | Resend → TrustCabinet backend (delivery events) | Inbound, signed callback that reports what happened to an accepted email (delivered, delayed, bounced, marked as spam, failed, suppressed) and records it on the queued email (API-09). |
| 5 | LiveKit server – RoomService API and access tokens | TrustCabinet backend (room join, chair controls, room reconciliation) → LiveKit server; browser → LiveKit real-time connection | Self-hosted LiveKit SFU for the online meeting room: the backend issues short-lived LiveKit access tokens after its permission checks and calls RoomService (list rooms and participants, mute a track, remove a participant, close a room); media and signalling flow between the browser and LiveKit (API-04). |
| 6 | LiveKit webhook | LiveKit server → TrustCabinet backend (room events) | Inbound, signed callback on participant joined, left or connection aborted, room finished and track published, used to record call sessions and attendance and to enforce the one-screen-share rule (API-05). |
| 7 | Speech worker – speech recognition and Voice ID (in-house) | Transcription queue, live sentence recording and Voice ID enrolment → TrustCabinet backend → speech worker over the private network (a backup worker when the main one cannot be reached) | The team's own speech service: turns a recording or a sentence into timed text with speaker separation, reports the progress of a long run, and turns a voice sample into a voice vector. Audio and voice data never leave the system (API-08). |
| 8 | AI drafting service – language model (chat completions) | Summary queue worker → TrustCabinet backend → the team's own language model server, or for a Normal Meeting the outside AI API the System Admin chose | Writes the three sections of the AI draft of the Meeting content from the transcript, the agenda and the vote results, for the Chair and the Secretary to review (UC-58). Internal, Secret and Top Secret Meetings always use the team's own server; the model used is stored with every draft (API-06). |
| 9 | File Storage – SeaweedFS, S3-compatible object storage | Document upload, view and download → TrustCabinet backend → File Storage (SeaweedFS) | Stores the bytes of uploaded personal and meeting documents in a private bucket; the backend reads them back after checking the permission of the caller, and a periodic clean-up removes files no document uses any more (API-07). |

#### 5.3 Background Job Inventory

| # | Job Name | Group | Description |
| --- | --- | --- | --- |
| 1 | Process notifications (JOB-01) | Scheduled worker | Every 5 seconds: creates the “meeting starting soon” reminders and their emails, and pushes new in-app notifications to connected users. |
| 2 | Deliver queued emails (JOB-02) | Queue worker | When email sending is enabled (Resend or SMTP): queues the invitation emails of published Meetings and sends queued emails one at a time, retrying failures; polls every 5 seconds when the queue is empty. |
| 3 | Transcribe recordings (JOB-03) | Queue worker | Every 3 seconds: transcribes one waiting recording with the speech worker, suggests speaker names and keeps the Secretary's manual corrections. |
| 4 | Draft meeting summaries (JOB-04) | Queue worker | Every 3 seconds: writes one requested AI draft of the Meeting content. |
| 5 | Build combined recordings (JOB-05) | Scheduled worker | Every 30 seconds: builds or rebuilds the combined recording of an ended hybrid or online Meeting. |
| 6 | Erase expired recordings (JOB-06) | Scheduled worker | Every 15 minutes: erases recordings and recorded speech past their 180-day retention and saves room-microphone takes left open for 12 hours. |
| 7 | Cancel or end overdue meetings (JOB-07) | Scheduled worker | Every 15 minutes: cancels Meetings not started 24 hours after their planned start and ends Meetings still in progress 24 hours after their planned end. |
| 8 | Remove expired personal data (JOB-08) | Scheduled worker | Every 60 minutes: removes unused audio, Draft votes of Cancelled Meetings, old notifications, guest verification codes and the bodies of sent emails. |
| 9 | Purge unused document files (JOB-09) | Scheduled worker | Every 60 minutes: deletes stored document files no document uses any more, after 24 hours. |
| 10 | Reconcile online meeting rooms (JOB-10) | Scheduled worker | Every 30 seconds: closes the rooms of Meetings no longer in progress and removes people who may no longer be in a room. |

## II. Use Case Specifications

### 1. FE-01 – Authentication & Account Security

#### 1.1 UC-01 – Sign in with Email and Password

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | As an internal system user, I want to sign in using my email and password so that I can securely access the system through my internal account. |  |  |
| Preconditions | ● PRE-1: The user has an existing internal account; any role may sign in, including a role with no capabilities. (BR-002)<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |  |  |
| Postconditions | ● POST-1: The user is authenticated.<br>● POST-2: The system identifies the corresponding internal user.<br>● POST-3: The user is signed in and lands on the calendar [Lịch làm việc]. A page opened from a link while signed out opens instead after sign-in, and a session ended by the system returns to the page the user was on; after a deliberate sign-out the next sign-in lands on the calendar. When the account cannot open the requested page, the user lands on the calendar too.<br>● POST-4: The sign-in, successful or failed for an existing account, is recorded in the audit log. (BR-009) |  |  |
| Normal Flow | Sign in with Email and Password<br>1. The user enters their email and password on the login page. An empty email, a malformed email or an empty password is pointed out beside its field before the sign-in is sent.<br>2. The user clicks “Sign In”.<br>3. The system validates the provided credentials.<br>4. The system records the successful sign-in, and the method used, in the audit log. (BR-009)<br>5. The system signs the user in and opens the landing page, the calendar [Lịch làm việc], unless the sign-in continued a page opened while signed out. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – System cannot authenticate the user<br>1. The entered email or password is invalid. (BR-004)<br>2. If the email belongs to an existing account, the system records the failed sign-in against that account as the actor, with the reason and without the password; an email that matches no account is not recorded. (BR-009)<br>3. The system displays MSG01 – “Invalid email or password.”<br>4. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – The internal account has been deactivated<br>1. The corresponding internal account is inactive or has been deactivated. (BR-001)<br>2. The system records the failed sign-in against that account as the actor, because the account is deactivated. (BR-009)<br>3. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”<br>4. The use case ends. |  |  |

#### 1.2 UC-02 – Sign in with Google

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | OAuth Provider |
| --- | --- | --- | --- |
| Description | As an internal system user, I want to sign in using my Google account so that I can securely and quickly access the system through my existing internal account. |  |  |
| Preconditions | ● PRE-1: The user has an existing internal account; any role may sign in, including a role with no capabilities. (BR-002)<br>● PRE-2: The user's account is active.<br>● PRE-3: The user is currently on the system login page. |  |  |
| Postconditions | ● POST-1: User is authenticated.<br>● POST-2: System identifies the corresponding internal user.<br>● POST-3: The user lands on the calendar [Lịch làm việc], on the same terms as UC-01 POST-3.<br>● POST-4: The sign-in with Google, successful or failed for an existing account, is recorded in the audit log. (BR-009) |  |  |
| Normal Flow | Sign in with Google<br>1. User selects “Continue with Google” on the login page.<br>2. User signs in using their Google account.<br>3. System verifies that the Google account is associated with an authorized internal user.<br>4. The system records the successful sign-in with Google in the audit log. (BR-009)<br>5. System signs the user in, grants access according to the user's assigned role and opens the calendar [Lịch làm việc], unless the sign-in continued a page opened while signed out. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – User can’t authenticate with Google<br>1. The user fails to authenticate with Google or cancels the Google sign-in process. (BR-002)<br>2. The system displays MSG03 – “Google sign-in was unsuccessful.”<br>3. The use case ends.<br>EX-2 – System can’t verify the Google account<br>1. The system cannot verify the identity information returned by Google. (BR-003)<br>2. The system displays MSG03 – “Google sign-in was unsuccessful.”<br>3. The use case ends.<br>EX-3 – Google account is not linked to an internal account<br>1. The authenticated Google account is not associated with any existing internal account. (BR-003)<br>2. The system displays MSG05 – “This Google account has not been granted access to the system.”<br>3. The use case ends.<br>EX-4 – The internal account has been deactivated<br>1. The internal account associated with the Google account is inactive or has been deactivated. (BR-001)<br>2. The system records the failed sign-in against that account as the actor, because the account is deactivated. (BR-009)<br>3. The system displays MSG02 – “Your account is currently unavailable. Please contact your administrator for assistance.”<br>4. The use case ends. |  |  |

#### 1.3 UC-03 – Sign Out

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | As an authenticated user, I want to sign out of the system so that I can securely end my current access to the system. |  |  |
| Preconditions | ● PRE-1: The user is currently signed in to the system.<br>● PRE-2: The user is currently accessing an authenticated area of the system. |  |  |
| Postconditions | ● POST-1: User is signed out of the system.<br>● POST-2: The user's current authentication state is invalidated.<br>● POST-3: User is redirected to the login page.<br>● POST-4: Protected system functions are no longer accessible until the user signs in again. |  |  |
| Normal Flow | Sign Out<br>1. User selects “Sign Out”.<br>2. System displays a confirmation message.<br>3. User confirms the sign-out action.<br>4. System signs the user out.<br>5. System redirects the user to the login page. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – System can’t complete the sign-out action<br>1. The system cannot complete the sign-out operation.<br>2. The system displays MSG06 – “Unable to sign out. Please try again.”<br>3. The use case ends. |  |  |

#### 1.4 UC-04 – Access Meeting via Guest Invitation

| Primary Actors | Guest | Secondary Actors | Resend Email |
| --- | --- | --- | --- |
| Description | Allows an invited Guest to verify control of the invited email and enter self-declared display information for the invited Meeting. |  |  |
| Preconditions | ● PRE-1: The Guest has received a valid invitation for a meeting.<br>● PRE-2: The invitation has not been revoked, and the invited Meeting is still Published or InProgress. |  |  |
| Postconditions | ● POST-1: Verified Guest access is limited to the invited Meeting and ends 12 hours after verification, or earlier when the Guest signs out, the invitation is revoked or the Meeting ends. Signing out ends only that session. (BR-052, BR-053, BR-054)<br>● POST-2: The Guest may use Member participation functions. Email control is verified; display name and job title are self-declared.<br>● POST-3: The Guest's successful verification is recorded in the audit log. (BR-009) |  |  |
| Normal Flow | Access Meeting via Guest Invitation<br>1. The Guest opens the invitation link.<br>2. The system checks the invitation and sends a one-time code to the invited email. (BR-053, BR-054)<br>3. The Guest enters the code.<br>4. The system validates the code and the invitation, which verifies control of the invited email. (BR-053, BR-054)<br>5. The system records the Guest's successful verification in the audit log. (BR-009)<br>6. The Guest enters a display name, required, and a job title, optional. The system labels them as self-declared; they are declared once for the invitation and cannot be changed afterwards.<br>7. The system displays MSG376 – “Display information saved.”<br>8. The system opens Meeting-scoped Guest access and displays the invited Meeting. (BR-052) |  |  |
| Alternative Flows | AF-1 – Guest already verified<br>1. At Step 1, the Guest still has an active verified session for this invitation.<br>2. The system opens the invited Meeting without asking for another code.<br>3. The use case ends.<br>AF-2 – Guest asks for a new code<br>1. At Step 3, the Guest asks for a new code.<br>2. The system sends a new code to the invited email; the previous code can no longer be used. (BR-053, BR-054)<br>3. The system displays MSG375 – “A verification code was sent to the invited email.”<br>4. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Guest signs out<br>1. After Step 8, the Guest signs out from the Meeting page.<br>2. The system ends this session only and removes the Meeting data from the screen. A session on another device keeps working. (BR-054)<br>3. While the Meeting is still Published or In Progress, the Guest may verify again with a new code; the flow then resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Invitation is no longer valid<br>1. At Step 2 or Step 4, the invitation is missing or revoked, or the Meeting is no longer Published or In Progress. An invitation has no expiry date of its own.<br>2. The system denies access without saying which cause applies and displays MSG371 – “The guest invitation is no longer valid.” (BR-052, BR-054)<br>3. The use case ends.<br>EX-2 – Code is not accepted<br>1. At Step 4, the code is incorrect, expired or already used.<br>2. No session is opened. The system displays MSG126 – “Unable to verify the guest invitation. Check the code and try again.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Too many incorrect codes<br>1. At Step 4, the code has been entered incorrectly 5 times. (BR-053)<br>2. The code can no longer be used and no session is opened. The system displays MSG372 – “Too many incorrect codes. Request a new code.”<br>3. The flow resumes at Step 1 of AF-2.<br>EX-4 – New code requested too soon<br>1. At Step 1 of AF-2, the previous code was sent less than 1 minute ago.<br>2. No code is sent. The system displays MSG373 – “Wait before requesting another code.” and shows how long to wait.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-5 – Too many codes requested<br>1. At Step 1 of AF-2, more than 20 codes were sent for this invitation within 24 hours.<br>2. No code is sent. The system displays MSG379 – “Too many codes requested. Try again later or contact the Meeting Secretary.”<br>3. The use case ends.<br>EX-6 – Display information is invalid<br>1. At Step 6, the display name is missing or longer than 200 characters, the job title is longer than 200 characters, or either contains control characters.<br>2. Nothing is saved. The system displays MSG377 – “Invalid display information.”<br>3. The flow resumes at Step 6 of the Normal Flow.<br>EX-7 – Display information already declared<br>1. At Step 6, the invitation already has a display name and the Guest submits a different display name or job title.<br>2. Nothing is changed. The system displays MSG381 – “The display name was already declared.”<br>3. The Guest continues with the information declared earlier; the flow resumes at Step 8 of the Normal Flow.<br>EX-8 – Guest session has ended<br>1. After Step 8, the 12-hour session expires, the invitation is revoked or the Meeting is no longer open. (BR-054)<br>2. The system removes the Meeting data from the screen and displays MSG378 – “The guest session has ended.”<br>3. The use case ends. |  |  |

This single end-to-end sequence covers code request, invited-email verification, self-declared display information and access to the invited Meeting; these steps remain part of UC-04, not additional Use Cases.

### 2. FE-02 – User & Profile Management

#### 2.1 UC-05 – View User Accounts

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to view, search, filter, and browse user accounts in the system. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to view user accounts. |  |  |
| Postconditions | ● POST-1: The user account list is displayed.<br>● POST-2: No user account information is modified. |  |  |
| Normal Flow | View User Accounts<br>1. The System Admin selects User Accounts from the navigation menu.<br>2. The system retrieves the user accounts.<br>3. The system sorts the user accounts according to the default sorting rule.<br>4. The system displays the user account list showing Full Name, Username/Email, System Role, and Account Status, one account per row.<br>5. The System Admin browses the user account list page by page. |  |  |
| Alternative Flows | AF-1 – Search user accounts<br>1. At Step 4 of the Normal Flow, the System Admin enters a Name, Username, or Email into the search field.<br>2. The system searches for user accounts matching the entered criteria, ignoring letter case and Vietnamese diacritics.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Filter user accounts<br>1. At Step 4 of the Normal Flow, the System Admin selects one or more filter criteria, such as Account Status or System Role.<br>2. The system filters the user accounts according to the selected criteria.<br>3. The system displays the matching user accounts.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>AF-3 – No user accounts match the current criteria<br>1. The applied search or filter criteria produce no matching user accounts.<br>2. The system displays an empty list and a no-results message.<br>3. The System Admin may clear or adjust the search/filter parameters.<br>4. The flow resumes at Step 5 of the Normal Flow. |  |  |
| Exception Flows | N/A |  |  |

#### 2.2 UC-06 – Manage User Accounts

##### 2.2.1 UC-06a – Create User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to create a new user account by providing the required account information. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin has permission to create user accounts. |  |  |
| Postconditions | ● POST-1: A new user account is created successfully.<br>● POST-2: The new user account appears in User Management.<br>● POST-3: The account creation is recorded in the audit log. |  |  |
| Normal Flow | Create User Account<br>1. The System Admin selects Create User from the User Accounts screen.<br>2. The system displays the Create User form.<br>3. The System Admin enters the required user information.<br>4. The System Admin submits the user information.<br>5. The system validates the submitted information, including required fields and Email validity and uniqueness (BR-005).<br>6. The system creates the new user account with Active status (BR-006).<br>7. The system records the account creation in the audit log (BR-009).<br>8. The system displays MSG07 – "User account created successfully." and returns to the User Accounts list. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid user information<br>1. At Step 5 of the Normal Flow, the system determines that one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." with a message for each invalid field.<br>3. The System Admin corrects the information and resubmits the form.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-2 – Username or Email already exists<br>1. At Step 5 of the Normal Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use." beside the Email field.<br>3. The System Admin changes the conflicting information and resubmits the form.<br>4. The flow resumes at Step 5 of the Normal Flow. |  |  |

##### 2.2.2 UC-06b – Update User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to update the Full Name, Email and System Role of an existing user account in one save. Changing the System Role here gives the same result, under the same rules, as UC-08. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to update the selected user account.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: The selected user account information is updated successfully.<br>● POST-2: The update action is recorded in the audit log. |  |  |
| Normal Flow | Update User Account<br>1. The System Admin selects a user account from the User Accounts list and selects Edit Account.<br>2. The system displays the current user account information.<br>3. The System Admin modifies the editable information: Full Name, Email and System Role.<br>4. The System Admin submits the updated information.<br>5. The system validates the submitted information, including required fields, data format and Email uniqueness, and, when the System Role changes, the rules of UC-08, so that at least one active account keeps the User administration permission. (BR-005)<br>6. The system saves the Full Name, Email and System Role together. A new role takes effect from the user's next action without ending the session, and the user is notified (BR-008); a changed Email ends every active session of the account instead, and no notification is sent.<br>7. The system records the update in the audit log (BR-009).<br>8. The system displays MSG10 – "User account updated successfully." and returns to the User Accounts list. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid user information<br>1. At Step 5 of the Normal Flow, one or more submitted values are missing or invalid.<br>2. The system displays MSG09 – "Invalid account information." with a message for each invalid field.<br>3. The System Admin corrects the information and resubmits it.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-2 – Email already in use<br>1. At Step 5 of the Normal Flow, the system determines that the Email is already associated with another account.<br>2. The system displays MSG08 – "Email is already in use." beside the Email field.<br>3. The System Admin changes the Email and resubmits the information.<br>4. The flow resumes at Step 5 of the Normal Flow.<br>EX-3 – Change would remove the last active administrator<br>1. At Step 5 of the Normal Flow, the system determines that, after the role change, no active account would hold the User administration permission.<br>2. Nothing is saved; the user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends. |  |  |

##### 2.2.3 UC-06c – Deactivate / Reactivate User Account

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to deactivate or reactivate a user account while preserving the user's historical records. Deactivation ends the user's Voice ID and the calendar shares of the account; reactivation restores neither. (BR-117, BR-142, BR-143) |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage user account status.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: The user account status is changed to Inactive when deactivated or Active when reactivated.<br>● POST-2: A deactivated user can no longer access authenticated system functions. (BR-001)<br>● POST-3: The status change is recorded in the audit log. (BR-009)<br>● POST-4: Deactivation ends the user's Voice ID and stops the Effective calendar shares of the account; reactivating the account restores neither. (BR-117, BR-142, BR-143) |  |  |
| Normal Flow | Deactivate / Reactivate User Account<br>1. The System Admin selects a user account from the User Accounts list.<br>2. The System Admin selects Deactivate Account or Reactivate Account.<br>3. The system requests confirmation of the account status change.<br>4. The System Admin confirms the action.<br>5. The system verifies that the requested status change is allowed. (BR-001)<br>6. The system updates the user account status and records the change in the audit log. (BR-009)<br>7. If the account is deactivated, the user is signed out everywhere, and the deactivation ends the user's Voice ID and the calendar shares of the account. (BR-001, BR-117, BR-142, BR-143)<br>8. The system displays MSG11 – “User account status changed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Status change is not allowed<br>1. At Step 5 of the Normal Flow, the system determines that the requested account status change is not allowed.<br>2. The system displays MSG12 – “Unable to change the status of this user account.”<br>3. The account status remains unchanged.<br>4. The use case ends. |  |  |

#### 2.3 UC-07 – Reset User Password

| Primary Actors | System Admin | Secondary Actors | User (the owner of the selected account), who completes the reset from the email |
| --- | --- | --- | --- |
| Description | Allows the System Admin to send the owner of an eligible user account a single-use password reset link by email. The account owner opens the link and sets a new password without signing in. The current password keeps working until the new one is set. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to reset user passwords.<br>● PRE-3: The selected user account exists. |  |  |
| Postconditions | ● POST-1: A password reset link valid for 60 minutes is sent to the user's registered email address, and any link sent to that user earlier stops working.<br>● POST-2: The password reset request is recorded in the audit log. (BR-009)<br>● POST-3: Until the link is used, the user's current password and active sessions remain unchanged.<br>● POST-4: When the account owner completes the reset, the password is replaced, every active session of that account is ended, the link can no longer be used, and the reset is recorded in the audit log. (BR-009) |  |  |
| Normal Flow | Reset User Password<br>1. The System Admin selects a user account and selects Reset Password.<br>2. The system asks for confirmation, showing the email address the link will be sent to.<br>3. The System Admin confirms.<br>4. The system checks that the account is active and signs in with a password.<br>5. The system sends the reset link to the user's registered email address; a link sent earlier stops working.<br>6. The system records the password reset request in the audit log. (BR-009)<br>7. The system displays MSG13 – “Password reset email sent successfully.”<br>Complete Password Reset<br>8. The account owner opens the link from the email; no sign-in is required.<br>9. The account owner enters the new password and its confirmation and submits them.<br>10. The system checks that the link is still valid, that the account is still active and signs in with a password (BR-001), and that the new password follows the password rule of account creation (UC-06a) and matches its confirmation.<br>11. The system replaces the password, ends every active session of the account and makes the link unusable.<br>12. The system records the completed reset in the audit log, with the account owner as the actor. (BR-009)<br>13. The system displays MSG92 – “Password reset successfully. Please sign in with your new password.” and opens the sign-in page. The user is not signed in automatically. |  |  |
| Alternative Flows | AF-1 – The browser is already signed in<br>1. At Step 13, the browser used to complete the reset still has a signed-in session.<br>2. The system signs that session out before showing the sign-in page.<br>3. If the sign-out succeeds, the flow resumes at Step 13 of the Normal Flow.<br>4. If the sign-out fails, the system still displays MSG92 – “Password reset successfully. Please sign in with your new password.” and also displays MSG06 – “Unable to sign out. Please try again.” The session stays signed in, and the new password is already in effect.<br>5. The use case ends. |  |  |
| Exception Flows | EX-1 – User account is not available for password reset<br>1. At Step 4 of the Normal Flow, the account no longer exists, is deactivated, or has no password because it signs in with Google only.<br>2. No link is created and no email is sent.<br>3. When the account no longer exists, the system says so and refreshes the list. Otherwise it displays MSG14 – “Password reset is not available for this account.” and states the reason when it is known.<br>4. The use case ends.<br>EX-2 – Password reset link is not valid<br>1. At Step 10 or 11, the link is unknown, has expired, has already been used, has been replaced by a newer link, or belongs to an account that is now deactivated or has no password.<br>2. The password is not changed.<br>3. The system displays MSG93 – “The password reset link is invalid or has expired. Please contact an administrator.” without saying which reason applies.<br>4. The use case ends.<br>EX-3 – New password is invalid<br>1. At Step 10, the new password breaks the password rule (at least 12 characters), or the confirmation does not match it.<br>2. The password is not changed and the link remains usable.<br>3. The system displays MSG94 – “Invalid new password.” with the reason beside the field concerned.<br>4. The flow resumes at Step 9 of the Normal Flow. |  |  |

#### 2.4 UC-08 – Assign System Role to User

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to assign a system role to a user account or change the user’s existing system role. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to assign system roles.<br>● PRE-3: The selected user account exists.<br>● PRE-4: At least one system role is available for assignment. |  |  |
| Postconditions | ● POST-1: The selected user is associated with the selected system role.<br>● POST-2: The user receives the capabilities associated with the selected system role.<br>● POST-3: The role assignment or change is recorded in the audit log. (BR-009)<br>● POST-4: The user keeps their active sessions, is notified of the change, and the new role's permissions apply from their next action without signing in again. (BR-008)<br>● POST-5: A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143) |  |  |
| Normal Flow | Assign System Role to User<br>1. The System Admin opens User Management.<br>2. The System Admin selects a user account.<br>3. The system displays the user information and the current system role, if any.<br>4. The System Admin selects Assign System Role.<br>5. The system displays the available system roles.<br>6. The System Admin selects a role and confirms the assignment.<br>7. The system assigns the selected role to the user.<br>8. The system records the role assignment in the audit log. (BR-009)<br>9. The system notifies the user that their access changed; the new role's permissions apply without signing in again. (BR-008)<br>10. A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143)<br>11. The system displays MSG10 – “User account updated successfully.” |  |  |
| Alternative Flows | AF-1 – Change existing system role<br>1. At Step 4 of the Normal Flow, if the user already has a system role, the System Admin selects Change System Role.<br>2. The System Admin selects a different role and confirms the change.<br>3. The system replaces the current role with the selected role.<br>4. The flow resumes at Step 8 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Selected role is not available<br>1. The selected role is no longer available for assignment.<br>2. The system displays MSG09 – “Invalid account information.” with the field error “The role does not exist.”<br>3. No role assignment is changed. The use case ends.<br>EX-2 – Assignment would remove the last active administrator<br>1. At Step 7 of the Normal Flow or Step 3 of AF-1, the system determines that, after the change, no active account would hold the user administration permission.<br>2. The user keeps the previous role.<br>3. The system displays MSG12 – “Unable to change the status of this user account.”<br>4. The use case ends. |  |  |

### 3. FE-03 – Role-Based Access Control

#### 3.1 UC-09 – View Role & Permission Matrix

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to view the predefined roles and their default permissions. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated. |  |  |
| Postconditions | ● POST-1: No system data is modified. |  |  |
| Normal Flow | View Role & Permission Matrix<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the role and permission matrix. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Role and permission information cannot be retrieved<br>1. At Step 2 of the Normal Flow, the system cannot retrieve the role and permission information.<br>2. The system displays an error message; no catalogue message is assigned to this case.<br>3. The use case ends. |  |  |

#### 3.2 UC-10 – Configure Role Permissions

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to configure the default permissions assigned to a predefined system role. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage role permissions. |  |  |
| Postconditions | ● POST-1: The selected role is associated with the updated set of default permissions.<br>● POST-2: The permission configuration change is recorded in the audit log. (BR-009)<br>● POST-3: Users holding the role keep their active sessions; the updated permissions apply to them from their next action without signing in again. (BR-008)<br>● POST-4: A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143) |  |  |
| Normal Flow | Configure Role Permissions<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the Role & Permission Matrix.<br>3. The System Admin configures the permissions assigned to a predefined role.<br>4. The system validates and saves the updated permission configuration. (BR-007)<br>5. The system records the configuration change in the audit log. (BR-009)<br>6. The system notifies the users currently holding the role; the updated permissions apply without signing in again. (BR-008)<br>7. A change that takes the meeting-creation right away from a viewer of calendar shares stops the shares that viewer watches. (BR-142, BR-143)<br>8. The system displays MSG16 – “Role permissions updated successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid permission configuration<br>1. At Step 4 of the Normal Flow, the system determines that the updated permission configuration violates a permission rule.<br>2. The system does not save the changes.<br>3. The system displays MSG17 – “Invalid permission configuration.”<br>4. The use case ends. |  |  |

#### 3.3 UC-11 – Manage Roles

##### 3.3.1 UC-11a – Create Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to create a new custom role with a name, an optional description and an optional initial set of permissions, without changing code or redeploying the system. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles. |  |  |
| Postconditions | ● POST-1: A new custom role is created and appears in the role list and in the Role & Permission Matrix.<br>● POST-2: The new role is associated with the selected permissions (the set may be empty).<br>● POST-3: The role creation is recorded in the audit log. |  |  |
| Normal Flow | Create Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The System Admin selects “Create Role”.<br>3. The system displays the role creation form.<br>4. The System Admin enters the role name and an optional description, selects the permissions to grant to the role, and submits the form.<br>5. The system validates the role information: the name is required and has at most 64 characters after trimming, the description has at most 256 characters, and every selected permission exists in the permission catalog.<br>6. The system checks that no existing role, including the predefined roles, has the same name (case-insensitive).<br>7. The system saves the new role together with its permissions.<br>8. The system records the role creation in the audit log (BR-009).<br>9. The system displays MSG19 – “Role created successfully.” |  |  |
| Alternative Flows | AF-1 – Role created without permissions<br>1. At Step 4 of the Normal Flow, the System Admin does not select any permission.<br>2. The system accepts an empty permission set; user accounts assigned to this role have the same access as a Member.<br>3. The use case continues at Step 5 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Invalid role information<br>1. At Step 5 of the Normal Flow, the system determines that the name is empty or longer than 64 characters, the description is longer than 256 characters, or a selected permission does not exist.<br>2. The system does not save the role.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends.<br>EX-2 – Role name already exists<br>1. At Step 6 or Step 7 of the Normal Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the role.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends. |  |  |

##### 3.3.2 UC-11b – Update Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to change the name and description of a custom role. Predefined roles cannot be renamed; the permissions of any role are configured through UC-10. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |  |  |
| Postconditions | ● POST-1: The name and/or description of the selected role are updated; its permissions and the user accounts holding it remain unchanged.<br>● POST-2: The change is recorded in the audit log with the values before and after the update.<br>● POST-3: No user session is terminated; users holding the role see the new name the next time their information is loaded. |  |  |
| Normal Flow | Update Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles.<br>3. The System Admin selects “Edit” on a custom role.<br>4. The system displays the edit form pre-filled with the current name and description.<br>5. The System Admin modifies the name and/or description and submits the form.<br>6. The system verifies that the selected role exists and is a custom role.<br>7. The system validates the role information using the same rules as UC-11 (name required, at most 64 characters after trimming; description at most 256 characters).<br>8. The system checks that no other role has the same name (case-insensitive). Changing only the letter case of the role’s own name is allowed.<br>9. The system saves the updated role.<br>10. The system records the change in the audit log (BR-009).<br>11. The system displays MSG22 – “Role updated successfully.” |  |  |
| Alternative Flows | AF-1 – No changes submitted<br>1. At Step 7 of the Normal Flow, the system determines that the submitted name and description are identical to the current values.<br>2. The system does not modify any data and does not record an audit log entry.<br>3. The system displays MSG22 – “Role updated successfully.”<br>4. The use case ends. |  |  |
| Exception Flows | EX-1 – Role is a predefined role<br>1. At Step 6 of the Normal Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not save the changes.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends.<br>EX-2 – Invalid role information<br>1. At Step 7 of the Normal Flow, the system determines that the role information is invalid.<br>2. The system does not save the changes.<br>3. The system displays MSG20 – “Invalid role information.” and highlights the invalid fields.<br>4. The System Admin may correct the information and resubmit; otherwise the use case ends.<br>EX-3 – Role name already exists<br>1. At Step 8 or Step 9 of the Normal Flow, the system determines that another role already uses the same name (case-insensitive).<br>2. The system does not save the changes.<br>3. The system displays MSG21 – “A role with this name already exists. Please choose a different name.”<br>4. The System Admin may enter a different name and resubmit; otherwise the use case ends. |  |  |

##### 3.3.3 UC-11c – Delete Role

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to delete a custom role that is no longer held by any user account. Predefined roles cannot be deleted. |  |  |
| Preconditions | ● PRE-1: The System Admin is authenticated in the system.<br>● PRE-2: The System Admin is authorized to manage roles.<br>● PRE-3: At least one custom role exists in the system. |  |  |
| Postconditions | ● POST-1: The selected role and its permission assignments are removed from the system.<br>● POST-2: The deletion, including the role name and the permissions it held, is recorded in the audit log.<br>● POST-3: No user account and no user session is affected.<br>● POST-4: Deleting a role never restarts a stopped calendar share. Because a role can be deleted only when no account holds it, the deletion itself stops no share; a reassignment that takes the meeting-creation right away is handled in UC-08. (BR-142, BR-143) |  |  |
| Normal Flow | Delete Role<br>1. The System Admin accesses Role & Permission Management.<br>2. The system displays the list of roles with the number of user accounts holding each role.<br>3. The System Admin selects “Delete” on a custom role.<br>4. The system asks for confirmation.<br>5. The System Admin confirms the deletion.<br>6. The system verifies that the selected role exists, is a custom role, and is not held by any user account, including deactivated accounts.<br>7. The system deletes the role together with its permission assignments.<br>8. The system records the role deletion in the audit log (BR-009).<br>9. The system displays MSG23 – “Role deleted successfully.” |  |  |
| Alternative Flows | AF-1 – Deletion cancelled<br>1. At Step 5 of the Normal Flow, the System Admin cancels the deletion.<br>2. No data is changed.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Role is a predefined role<br>1. At Step 6 of the Normal Flow, the system determines that the selected role is a predefined system role.<br>2. The system does not delete the role.<br>3. The system displays MSG24 – “This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions.”<br>4. The use case ends.<br>EX-2 – Role is still held by user accounts<br>1. At Step 6 or Step 7 of the Normal Flow, the system determines that one or more user accounts (active or deactivated) still hold the role, including an account assigned to the role while the deletion was in progress.<br>2. The system does not delete the role.<br>3. The system displays MSG25 – “This role is still held by {n} user account(s). Reassign them to another role before deleting.”, where {n} is the number of user accounts holding the role.<br>4. The use case ends. |  |  |

#### 3.4 UC-12 – View Audit Log

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the System Admin to review the recorded business and security events — who did what, to which object, when and from which access address — for accountability and investigation. The audit log is read-only. (BR-012) |  |  |
| Preconditions | ● PRE-1: The System Admin is signed in.<br>● PRE-2: The System Admin's role holds the User administration permission. (BR-012) |  |  |
| Postconditions | ● POST-1: The audit entries matching the chosen period and filters are displayed, newest first.<br>● POST-2: No audit entry is changed or deleted. (BR-012)<br>● POST-3: The System Admin gains no access to the Meetings, documents or minutes the entries are about, even though the entries name their objects. |  |  |
| Normal Flow | View Audit Log<br>1. The System Admin opens Nhật ký hệ thống [Audit Log] from the Administration menu.<br>2. The system checks that the System Admin's role holds the User administration permission. (BR-012)<br>3. The system retrieves the audit entries, newest first, one page at a time.<br>4. The system displays, for each entry, the time, the actor (a user account, a Guest or the system), the event, the object and the access address. The object is shown by its name, or only as “Họp Mật” for anything of a Secret meeting.<br>5. The System Admin browses the pages; the chosen period and filters are kept across pages. |  |  |
| Alternative Flows | AF-1 – Filter the audit log<br>1. At Step 4 of the Normal Flow, the System Admin chooses a period, an actor (a user account, Guests or the system), an event type, or enters a keyword; the keyword matches the object id, the object name — except the name of anything of a Secret meeting — and the recorded details, ignoring case and Vietnamese marks.<br>2. The system retrieves the entries that match all the chosen criteria.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – View the details of an entry<br>1. At Step 4 of the Normal Flow, the System Admin selects an entry.<br>2. The system displays the details recorded with that entry, read-only; the fields differ between event types.<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-3 – No entry matches<br>1. At Step 3 of the Normal Flow, no audit entry matches the period and filters.<br>2. The system displays an empty list and a no-results message.<br>3. The System Admin may change the period or the filters; the flow resumes at Step 1 of AF-1. |  |  |
| Exception Flows | EX-1 – The System Admin's role does not hold the User administration permission<br>1. At Step 2 of the Normal Flow, the System Admin's role does not hold the User administration permission, or no longer holds it.<br>2. The system returns no audit entry and the Audit Log page is not shown.<br>3. The use case ends.<br>EX-2 – The chosen period or filters are not valid<br>1. At Step 3 of the Normal Flow, the start of the period is after its end, or a filter value is not valid.<br>2. No entry is retrieved. The system displays MSG469 – “The audit log filter is invalid. Check the period and the selected values.”<br>3. The flow resumes at Step 1 of AF-1. |  |  |

### 4. FE-04 – Meeting Management

#### 4.1 UC-13 – Create Meeting

| Primary Actors | Member | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized Member to create one independent Meeting as a Draft, including its planned schedule, mode, location, confidentiality and its initial participants. An Approver can also do this, because the Approver role also holds the create-meeting permission. |  |  |
| Preconditions | ● PRE-1: The Member is authenticated.<br>● PRE-2: The Member is authorized to create meetings. |  |  |
| Postconditions | ● POST-1: A new Meeting exists in Draft status with its planned start and end times.<br>● POST-2: The creator is recorded as Member.<br>● POST-3: The selected participants are saved with the Meeting, each with a Pending invitation response; no invitation is sent while the Meeting is Draft. The Meeting may subsequently be prepared and submitted for approval, and a Secretary may still be assigned later through UC-30a.<br>● POST-4: The creation is recorded in the Meeting's lifecycle history and in the audit log. (BR-022, BR-023) |  |  |
| Normal Flow | Create Meeting<br>1. The Member selects Create Meeting.<br>2. The system displays the new Meeting form.<br>3. The Member enters Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location, Confidentiality Level and optional Description, and chooses the initial participants with a Meeting Role for each: exactly one Chair, and optionally a Secretary and Members. For an Online or Hybrid Meeting an external Online Link of at most 1,000 characters may be entered; left blank, the TrustCabinet online meeting room is used.<br>4. The Member submits the form.<br>5. The system checks the required values and the planned times, and checks the chosen participants: each is an existing active internal user listed once, with a valid Meeting Role, exactly one is the Chair, and the list holds 1 to 100 participants. Guests cannot be added at creation, and the creator need not be a participant. (BR-013)<br>6. The system creates exactly one Meeting in Draft status, records the creator and saves the chosen participants with Pending responses. (BR-013)<br>7. The system records the creation in the Meeting's lifecycle history and in the audit log. (BR-022, BR-023)<br>8. The system displays MSG29 – "Meeting created successfully." |  |  |
| Alternative Flows | AF-1 – Create Draft from a found time slot<br>1. At Step 1, the Member chooses a time slot found in UC-81a instead.<br>2. The Draft Meeting is created through UC-81b, which completes the use case. |  |  |
| Exception Flows | EX-1 – Meeting information is invalid<br>1. At Step 5, a required value is missing/invalid, the planned start is not in the future, or the planned end does not follow the start. (BR-013)<br>2. The system displays MSG31 – "Invalid meeting information." and highlights the affected fields.<br>3. No Meeting is created. The Member may correct the inputs.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – Participants are invalid<br>1. At Step 5, no participant is chosen, a participant is not an existing active internal user or is listed twice, a Meeting Role is invalid, or the choice does not contain exactly one Chair.<br>2. The system displays MSG277 – "The meeting participant list is not valid." and highlights the participant list.<br>3. No Meeting is created. The Member may correct the participants.<br>4. The flow resumes at Step 4 of the Normal Flow. |  |  |

#### 4.2 UC-14 – Update Meeting

| Primary Actors | Member | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Member to update Draft/Rejected Meeting information, or to reschedule only the planned times or location when the Meeting is Published. An Approver can also do this, because the Approver role also holds the create-meeting permission. |  |  |
| Preconditions | ● PRE-1: The Meeting exists and the Member is authenticated and authorized.<br>● PRE-2: Full Meeting editing is available only in Draft or Rejected. (BR-014)<br>● PRE-3: If the Meeting is Published, only the Member may invoke Reschedule to change planned times, location or online link. The Meeting must not have started. (BR-015) |  |  |
| Postconditions | ● POST-1: Authorized updates are saved to the Meeting.<br>● POST-2: Meeting lifecycle and assigned approver are unchanged.<br>● POST-3: When the schedule of a Published Meeting changes, the change is recorded and the invitees are notified. (BR-022, BR-023, BR-031, BR-035) |  |  |
| Normal Flow | Update Meeting<br>1. The Member opens a Draft or Rejected Meeting and selects Edit.<br>2. The system displays the editable Meeting fields.<br>3. The Member changes the fields and submits.<br>4. The system verifies current Meeting status, permissions, required information, and confirms Planned End Time is after Planned Start Time. (BR-013, BR-014)<br>5. The system saves the updates without changing Meeting status. (BR-014)<br>6. The system displays MSG30 – “Meeting updated successfully.” |  |  |
| Alternative Flows | AF-1 – Reschedule a published meeting<br>1. At Step 1 of the Normal Flow, the Member selects Reschedule for a Meeting that is Published and has not started.<br>2. The system allows changes only to Planned Start Time, Planned End Time, Location and Online Link; all other fields are read-only. (BR-015)<br>3. The Member enters the changes and submits.<br>4. The system validates that the new start is in the future and the end follows the start. (BR-013, BR-015)<br>5. The system saves the new schedule, keeps the Meeting Published and records the change in the Meeting's history and in the audit log. (BR-022, BR-023)<br>6. The invitees are notified of the new schedule; the Member who rescheduled receives the email but no in-app notification. A delivery failure does not undo the saved change. (BR-031, BR-032, BR-035)<br>7. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified." The use case ends.<br>AF-2 – Nothing has changed<br>1. At Step 3 of the Normal Flow or Step 3 of AF-1, the Member submits without changing any value; spaces added at the start or end of a text value do not count as a change.<br>2. Nothing is saved and no notification is sent. The system displays MSG154 – "No changes to save." The use case ends.<br>AF-3 – Planned start has already passed<br>1. At Step 3 of the Normal Flow, the Member sets the Planned Start Time of a Draft or Rejected Meeting to a time that has passed, by less than 24 hours.<br>2. The system warns that a Meeting not started within 24 hours of its planned start is cancelled. (BR-029)<br>3. The Member may still submit; the flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Invalid or unauthorized update<br>1. At Step 4, the meeting information is invalid, or the Meeting status no longer permits this edit. (BR-013, BR-014)<br>2. The system does not save any changes and displays MSG31 – “Invalid meeting information.” with the field errors, or MSG33 – “This meeting can no longer be edited.” when the status does not permit the edit.<br>3. For invalid information, the Member corrects it and the flow resumes at Step 3 of the Normal Flow; otherwise the use case ends.<br>EX-2 – Reschedule not permitted<br>1. At Step 4 of AF-1, the Meeting is no longer Published or has already started, its planned start is not in the future, or a field outside the allowed Reschedule set was modified. (BR-015)<br>2. The existing schedule remains unchanged. The system displays MSG33 – “This meeting can no longer be edited.” when the Meeting can no longer be rescheduled, or MSG31 – “Invalid meeting information.” with the field errors otherwise.<br>3. The use case ends.<br>EX-3 – Planned start is more than 24 hours ago<br>1. At Step 3 of the Normal Flow, the Planned Start Time of a Draft or Rejected Meeting is more than 24 hours in the past.<br>2. The update is not submitted, because such a Meeting would be cancelled at once (BR-029). The system asks for another start time.<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

#### 4.3 UC-15 – View Meeting Details

| Primary Actors | Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows authorized users to view the details of one Meeting, including planned schedule, actual start/end times when available, and meeting-level status. Agenda and other records remain accessible through their corresponding views. |  |  |
| Preconditions | ● PRE-1: The user is authenticated.<br>● PRE-2: A meeting has been selected or identified. |  |  |
| Postconditions | ● POST-1: The Meeting Name, planned time range, actual times where available, mode, location, confidentiality, description and lifecycle status are displayed read-only. For a Cancelled Meeting, the cancellation reason, who cancelled it and when are also displayed.<br>● POST-2: The Meeting is not modified.<br>● POST-3: The status history of the Meeting is displayed beside its details; a rejection reason is shown only to the people allowed to read it. (BR-022, BR-023) |  |  |
| Normal Flow | View Meeting Details<br>1. The user requests the selected Meeting.<br>2. The system verifies that the Meeting exists and the user has access. (BR-016, BR-017)<br>3. The system retrieves the Meeting information and planned/actual time fields.<br>4. The system displays Meeting Name, planned start/end time, actual start/end time when available, Meeting Mode, Location, Confidentiality Level, Description, and Status. For a Cancelled Meeting, it also displays the cancellation reason, who cancelled it and when.<br>5. The user reviews the Meeting details. |  |  |
| Alternative Flows | AF-1 – View the status history<br>1. At Step 5 of the Normal Flow, the user reviews the status history shown beside the Meeting details.<br>2. The system shows every recorded status change, oldest first, with its action, resulting status, time and actor. (BR-022, BR-023)<br>3. A rejection reason is shown only to the Member who created the Meeting, its assigned Approver and its current Chair and Secretary; other readers see the same changes without it.<br>4. The flow resumes at Step 5 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Meeting details cannot be loaded<br>1. At Step 3, the meeting exists and is accessible, but the system cannot retrieve its meeting-level information.<br>2. The system displays MSG37 – "Unable to load meeting details. Please try again."<br>3. The use case ends.<br>EX-2 – Status history cannot be loaded<br>1. At Step 2 of AF-1, the system cannot retrieve the status history.<br>2. The Meeting details stay displayed. The system displays MSG136 – “Unable to load the meeting workflow data. Please try again.”<br>3. The use case ends. |  |  |

#### 4.4 UC-16 – View Meeting List

| Primary Actors | Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view and browse only the meetings to which the user has an authorized relationship or assignment. The assigned Secretary can view the Meeting before publication under BR-030. External Guests do not use the internal Meeting list. |  |  |
| Preconditions | ● PRE-1: The user is authenticated. |  |  |
| Postconditions | ● POST-1: The system displays the list of meetings that the user is authorized to view.<br>● POST-2: No meeting information is modified. |  |  |
| Normal Flow | View Meeting List<br>1. The user selects Meetings from the navigation menu. The list opens unfiltered, showing every status at any time.<br>2. The system selects only the meetings the user may view: those the user created, at any status; those assigned to the user as Approver, from WaitingApproval onward; those where the user is Chair or active Secretary, at any status; and those where the user is a Member, from Published onward. (BR-016, BR-017, BR-030)<br>3. The system displays those meetings, including Cancelled Meetings and Meetings the user has declined, sorted by planned start time, newest first. (BR-028, BR-047, BR-048)<br>4. The user browses the displayed meetings. |  |  |
| Alternative Flows | AF-1 – Search meetings<br>1. At Step 4 of the Normal Flow, the user enters a meeting name in the search field.<br>2. The system finds the meetings whose name contains the entered text within the meetings available to the user, ignoring letter case and Vietnamese diacritics.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Filter meetings<br>1. At Step 4 of the Normal Flow, the user selects one or more filter criteria: Meeting Status (including Cancelled), Confidentiality Level, or Time (Today, This week, Not yet held, Already held).<br>2. The system applies the selected criteria to the meetings available to the user. Today and This week match a Meeting whose planned time overlaps that day or week; Not yet held matches a Meeting whose planned start is later than now; Already held matches one whose planned start is now or earlier, so a Meeting in progress counts as Already held.<br>3. The system displays the matching meetings.<br>4. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – No matching meetings<br>1. The applied search or filter criteria produce no matching meetings within the meetings available to the user.<br>2. The system displays an empty meeting list and the message "No meetings match the filter."<br>3. The user may change or clear the current search/filter criteria; the flow resumes at Step 4 of the Normal Flow.<br>AF-4 – View meetings on the calendar<br>1. At Step 1 of the Normal Flow, the user opens the Calendar instead of the list.<br>2. The system shows the authorized meetings by week, grouped by status, and omits the Meetings the user has declined through UC-31. (BR-047, BR-048) Cancelled Meetings form their own group, hidden by default. (BR-028)<br>3. The user browses the displayed meetings.<br>4. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Criteria are invalid<br>1. The system determines that the selected search, filter, sort, or display criteria are invalid.<br>2. The system displays MSG32 – “The meeting list criteria are invalid.”<br>3. The user may clear or adjust the search/filter criteria.<br>4. The use case ends. |  |  |

#### 4.5 UC-17 – Submit Meeting for Approval

| Primary Actors | Member | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Member to submit a Draft meeting or resubmit a previously Rejected meeting for review by an assigned Approver based on the Meeting information and planned schedule. An Approver can also do this, because the Approver role also holds the create-meeting permission. |  |  |
| Preconditions | ● PRE-1: The Member is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Member is authorized to submit the selected meeting for approval.<br>● PRE-4: The meeting is in Draft or Rejected status. |  |  |
| Postconditions | ● POST-1: The meeting status is WaitingApproval.<br>● POST-2: A valid Approver is assigned to the current approval cycle.<br>● POST-3: The assigned Approver is granted access to the meeting for approval purposes.<br>● POST-4: The submission is recorded in the meeting lifecycle history.<br>● POST-5: The assigned Approver is notified of the pending approval request.<br>● POST-6: The submission and the assigned approver are recorded in the audit log. (BR-022, BR-023) |  |  |
| Normal Flow | Submit Meeting for Approval<br>1. The Member selects a Draft meeting and chooses Submit for Approval.<br>2. The system verifies that the Meeting information and planned schedule are complete and valid for submission. (BR-013)<br>3. The system displays the eligible Approvers with their name and email; the Member may search the list by name or email.<br>4. The Member selects an Approver and confirms.<br>5. The system checks that the Meeting is still Draft or Rejected and that the chosen Approver is still eligible and is not the Member. (BR-018, BR-019)<br>6. The system assigns the Approver, changes the Meeting to WaitingApproval and records the submission in the status history and the audit log. (BR-018, BR-022, BR-023)<br>7. The assigned Approver is notified.<br>8. The system displays MSG78 – “Meeting submitted for approval.” |  |  |
| Alternative Flows | AF-1 – Resubmit a rejected meeting<br>1. At Step 1 of the Normal Flow, the Member selects a meeting in Rejected status and chooses Resubmit for Approval.<br>2. At Step 3, the Approver of the previous approval cycle is preselected while still eligible; the Member may keep or change the selection.<br>3. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Incomplete meeting information<br>1. At Step 2, mandatory Meeting information or the planned schedule is incomplete or invalid.<br>2. The system displays MSG81 – “The meeting does not have enough information to be submitted for approval.” and identifies the missing or invalid information.<br>3. The meeting remains Draft or Rejected and the use case ends.<br>EX-2 – Meeting status not eligible<br>1. At Step 5, the system determines that the meeting is not in Draft or Rejected status.<br>2. The system displays MSG41 – "This meeting can no longer be submitted for approval."<br>3. The use case ends.<br>EX-3 – No eligible approver available<br>1. At Step 3, the system finds no eligible Approver. (BR-019)<br>2. The system displays MSG140 – "No eligible meeting approver is available. Please contact the administrator."<br>3. The Meeting remains Draft or Rejected; no assignment, submission or notification is made.<br>4. The use case ends.<br>EX-4 – Chosen Approver is no longer eligible<br>1. At Step 5, the chosen Approver is no longer active, can no longer approve meetings, or is the Member. (BR-019)<br>2. Nothing is changed. The system displays MSG82 – “The approver is not valid or can no longer approve meetings.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

#### 4.6 UC-18 – Review Meeting Approval

| Primary Actors | Approver | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Approver assigned to a meeting approval cycle to review the meeting and decide whether to approve or reject it. |  |  |
| Preconditions | ● PRE-1: The Approver is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Approver is currently assigned to review the selected meeting and is not its Member.<br>● PRE-4: The meeting is in WaitingApproval status. |  |  |
| Postconditions | ● POST-1: The Approver's decision is recorded.<br>● POST-2: If approved, the meeting status is Approved.<br>● POST-3: If rejected, the meeting status is Rejected.<br>● POST-4: If rejected, the rejection reason is recorded and available to the Member and assigned Secretary so the Member can correct and resubmit the Meeting.<br>● POST-5: The approval or rejection is recorded in the meeting lifecycle history.<br>● POST-6: The Member is notified in the application of the approval or rejection; a rejection notification includes the rejection reason. No email is sent for the decision.<br>● POST-7: The approval or rejection is recorded in the audit log. (BR-022, BR-023) |  |  |
| Normal Flow | Review Meeting Approval<br>1. The Approver selects a meeting assigned to them that is awaiting approval.<br>2. The system verifies that the current user is the assigned Approver.<br>3. The system displays the meeting information for review.<br>4. The Approver selects Approve.<br>5. The system re-verifies that the Approver may still approve the meeting and that it is still in WaitingApproval status. (BR-019)<br>6. The system changes the meeting status from WaitingApproval to Approved and records the approval, the actor and the time in the status history and in the audit log. (BR-020, BR-022, BR-023)<br>7. The Member is notified in the application that the meeting has been approved.<br>8. The system displays MSG43 – "Meeting approved successfully." |  |  |
| Alternative Flows | AF-1 – Reject the meeting<br>1. At Step 4 of the Normal Flow, the Approver selects Reject.<br>2. The system requests a rejection reason.<br>3. The Approver enters the rejection reason.<br>4. The system trims the rejection reason and validates that it is not empty and does not exceed 1000 characters. (BR-020)<br>5. The system re-verifies that the Approver may still reject the meeting and that it is still in WaitingApproval status. (BR-019)<br>6. The system changes the meeting status to Rejected and records the rejection, its reason, the actor and the time in the status history and in the audit log. (BR-020, BR-022, BR-023)<br>7. The Member is notified in the application that the meeting has been rejected, with the reason; the reason is also available to the assigned Secretary so the Member can correct and resubmit the Meeting.<br>8. The system displays MSG44 – "Meeting rejected successfully." |  |  |
| Exception Flows | EX-1 – Rejection reason is missing<br>1. At Step 4 of AF-1, the system determines that the trimmed rejection reason is empty. (BR-020)<br>2. The system displays MSG83 – "Rejection reason is required."<br>3. The meeting remains in WaitingApproval status.<br>4. The Approver enters a valid rejection reason.<br>5. The flow resumes at Step 4 of AF-1.<br>EX-2 – Meeting is no longer awaiting approval<br>1. At Step 5 of the Normal Flow or Step 5 of AF-1, the system determines that the meeting is no longer in WaitingApproval status, or the Approver is no longer assigned to it.<br>2. No approval or rejection decision is applied; the meeting remains in its current status.<br>3. The system displays MSG132 – “The meeting status has changed or no longer allows this action.”<br>4. The use case ends.<br>EX-3 – Rejection reason is too long<br>1. At Step 4 of AF-1, the trimmed rejection reason exceeds 1000 characters.<br>2. The system displays MSG84 – "Rejection reason must not exceed 1000 characters."<br>3. The meeting remains in WaitingApproval status.<br>4. The Approver enters a valid rejection reason and the flow resumes at Step 4 of AF-1. |  |  |

#### 4.7 UC-19 – Publish Meeting

| Primary Actors | Member | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the Member to publish an Approved meeting, which invites its current internal participants and its external Guests. |  |  |
| Preconditions | ● PRE-1: The Member is authenticated.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The Member is authorized to publish the selected meeting.<br>● PRE-4: The meeting is in Approved status.<br>● PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation responses are not publication prerequisites. (BR-021, BR-024) |  |  |
| Postconditions | ● POST-1: The meeting status is Published.<br>● POST-2: The publication is recorded in the meeting lifecycle history and in the audit log. (BR-022, BR-023)<br>● POST-3: The current internal participants and the Guests with a current invitation are invited; each Guest is invited to verify the invited email address and access only this Meeting. Publication does not wait for the invitations to be delivered, and a delivery failure does not undo it. (BR-031, BR-032, BR-052, BR-053, BR-054) |  |  |
| Normal Flow | Publish Meeting<br>1. The Member selects an Approved meeting.<br>2. The Member selects Publish Meeting.<br>3. The system verifies that the Member remains authorized, the meeting is still Approved with a valid planned schedule, and it has exactly one active current Chair and at least one active current Secretary. (BR-021, BR-024)<br>4. The system changes the meeting status from Approved to Published and records the publication, the actor and the time in the meeting lifecycle history and in the audit log. (BR-021, BR-022, BR-023)<br>5. The current internal participants are notified in the application and by email, except that the Member who published receives no in-app notification about their own action; each Guest with a current invitation receives an email invitation. (BR-031, BR-035, BR-052, BR-053)<br>6. The system displays MSG45 – "Meeting published successfully." |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting is no longer eligible for publication<br>1. At Step 3 of the Normal Flow, the system determines that the meeting is no longer Approved, the Member is no longer authorized to publish it, there is not exactly one active current Chair, or there is no active current Secretary. (BR-021)<br>2. The meeting remains unchanged.<br>3. No publication notification or invitation is issued and publication is not reported as successful.<br>4. The system displays MSG46 – "This meeting can no longer be published."<br>5. The use case ends. |  |  |

#### 4.8 UC-20 – Start Meeting

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Chair or Secretary to start one Published Meeting, set the Meeting lifecycle to InProgress and record its actual start time. The Meeting has no subordinate scheduling objects. |  |  |
| Preconditions | ● PRE-1: The Chair or the Secretary is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting exists.<br>● PRE-3: The meeting is Published.<br>● PRE-4: The meeting has exactly one current Chair and at least one current Secretary with active accounts.  (BR-024) |  |  |
| Postconditions | ● POST-1: Meeting status is InProgress.<br>● POST-2: Actual Start Time and the starter (Chair or Secretary) are recorded in the status history and in the audit log. (BR-022, BR-023)<br>● POST-3: The current invited internal participants are notified that the Meeting started, except the person who started it. (BR-035)<br>● POST-4: Online Meeting Room access is governed by the Meeting mode and current status. |  |  |
| Normal Flow | Start Meeting<br>1. The assigned Chair or Secretary selects Start Meeting for the Published Meeting.<br>2. The system verifies the starter is an active current Chair or Secretary, one active current Chair and at least one active Secretary exist, and the Meeting remains Published. (BR-024)<br>3. The system changes status to InProgress, saves Actual Start Time, and records the start in the status history and in the audit log. (BR-022, BR-023, BR-024)<br>4. The current invited internal participants are notified, except the person who started the Meeting; when the Secretary starts it, the Chair is notified. A delivery failure does not undo the start. (BR-032, BR-035)<br>5. The system displays MSG110 – "Meeting started successfully." |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Meeting cannot be started<br>1. At Step 2, the meeting is no longer Published, the starter is no longer an assigned active Chair or Secretary, or the required active Chair/Secretary assignments are no longer valid. (BR-024)<br>2. The meeting remains unchanged.<br>3. The system displays MSG112 – "This meeting cannot be started."<br>4. The use case ends. |  |  |

#### 4.9 UC-21 – End Meeting

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to end an InProgress Meeting regardless of open Votes, unmarked attendance or remaining Pending Agenda Items. |  |  |
| Preconditions | ● PRE-1: The actor is the current authorized Chair.<br>● PRE-2: The Meeting is InProgress. (BR-024, BR-025) |  |  |
| Postconditions | ● POST-1: The Meeting becomes Ended and its Actual End Time is recorded.<br>● POST-2: Any InProgress Agenda Item is completed; remaining Pending Items stay Pending. (BR-026)<br>● POST-3: Online Meeting Room access is closed. (BR-025)<br>● POST-4: Draft Votes that were never opened are deleted. (BR-086, BR-087)<br>● POST-5: The end of the Meeting is recorded in the status history and in the audit log. (BR-022, BR-023) |  |  |
| Normal Flow | End Meeting<br>1. The Chair selects End Meeting and confirms.<br>2. The system checks current Chair authority and that the Meeting is InProgress. Open Votes, unmarked attendance and Pending Agenda Items do not block the end. (BR-024, BR-025, BR-026)<br>3. The system changes the Meeting to Ended, records Actual End Time and records the end in the status history and in the audit log. (BR-022, BR-023)<br>4. The system completes any InProgress Agenda Item and deletes Draft Votes that were never opened; Pending Items stay Pending and opened Votes and their ballots are unchanged. (BR-026, BR-087)<br>5. The system closes room access and displays MSG111 – "Meeting ended successfully." (BR-025)<br>6. Votes stop accepting ballots because the Meeting is Ended; the Chair may later close outstanding Votes to finalize their tally. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor or Meeting is not eligible<br>1. At Step 2, the actor is no longer the current Chair or the Meeting is no longer InProgress. (BR-025)<br>2. No end is recorded. The system displays MSG114 – "This meeting cannot be ended."<br>3. The use case ends. |  |  |

#### 4.10 UC-22 – Cancel Meeting

| Primary Actors | Member | Secondary Actors | Resend Email |
| --- | --- | --- | --- |
| Description | Allows the Member to cancel a Meeting that will no longer take place, before it starts. A Cancelled Meeting stays in the system as a read-only record with its cancellation reason; it cannot be edited, published or started again. (BR-028) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the Member of the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved or Published. An InProgress Meeting is ended through UC-21 instead. (BR-028) |  |  |
| Postconditions | ● POST-1: The Meeting is Cancelled, and the reason, the actor and the cancellation time are recorded in its status history. (BR-022, BR-023, BR-028)<br>● POST-2: Agenda, participants, documents and Votes are kept read-only, and any pending approval request is closed. (BR-028)<br>● POST-3: If the Meeting was Published, every invited participant and Guest is notified; if it was awaiting approval, its assigned Approver is notified. (BR-031)<br>● POST-4: The cancellation is recorded in the audit log. (BR-022, BR-023) |  |  |
| Normal Flow | Cancel Meeting<br>1. The Member opens the Meeting details and selects Cancel meeting.<br>2. The system asks for confirmation with a required cancellation reason.<br>3. The Member enters the reason and confirms.<br>4. The system rechecks that the actor is the Member, the Meeting is Draft, WaitingApproval, Rejected, Approved or Published, and the reason is not blank and not longer than 1,000 characters. (BR-028)<br>5. The system changes the Meeting to Cancelled, closes any pending approval request, and records the reason, the actor and the time in the status history and in the audit log. (BR-022, BR-023, BR-028)<br>6. If the Meeting was Published, every invited internal participant and Guest is notified with the reason, and the event is removed from their calendars; the Member who cancelled receives no in-app notification. If the Meeting was WaitingApproval, its assigned Approver is notified that nothing is left to review. In any other status nobody is notified. A delivery failure does not undo the cancellation. (BR-031, BR-032, BR-035)<br>7. The system displays MSG261 – "Meeting cancelled successfully." and shows the Meeting as Cancelled. |  |  |
| Alternative Flows | AF-1 – Member keeps the Meeting<br>1. At Step 3, the Member chooses Back instead of confirming.<br>2. The Meeting and its status remain unchanged. The use case ends. |  |  |
| Exception Flows | EX-1 – Cancellation reason is missing<br>1. At Step 4, the reason is blank or contains only spaces.<br>2. The Meeting is not cancelled. The system displays MSG262 – "Enter a reason for cancelling the meeting."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Cancellation reason is too long<br>1. At Step 4, the reason is longer than 1,000 characters.<br>2. The Meeting is not cancelled. The system displays MSG345 – "Cancellation reason must not exceed 1000 characters."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting can no longer be cancelled<br>1. At Step 4, the Meeting has started, has ended or is already Cancelled. (BR-028)<br>2. The Meeting remains unchanged. The system displays MSG263 – "This meeting can no longer be cancelled."<br>3. The use case ends. |  |  |

### 5. FE-05 – Agenda Management

#### 5.1 UC-23 – Manage Agenda Items

##### 5.1.1 UC-23a – Create Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to create a new Agenda Item while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress, including during the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary is authenticated and assigned to the Meeting.<br>● PRE-2: The Meeting exists, is in Draft, WaitingApproval, Rejected, Approved, Published or InProgress status and is eligible for agenda planning. (BR-058, BR-059) |  |  |
| Postconditions | ● POST-1: A Pending Agenda Item is created under the Meeting.<br>● POST-2: The Agenda Item is ordered within that Meeting. Its document links are added afterwards through UC-36b, and creating an Item never changes another Item's links.<br>● POST-3: A Presenter, when set, is a current participant or Guest of the Meeting. (BR-064, BR-065) |  |  |
| Normal Flow | Create Agenda Item<br>1. The Secretary opens the Meeting Agenda and selects Add Agenda Item.<br>2. The Secretary enters Title, Description and an optional Presenter chosen from the current internal participants and Guests of the Meeting. Documents are attached to the Item afterwards through UC-36b. (BR-064, BR-065)<br>3. The system verifies the active Secretary assignment, that the Meeting status still permits Agenda planning, that the Title is given and that the Presenter is valid; Description and Presenter are optional. (BR-058, BR-059, BR-064, BR-065)<br>4. The system creates the Item as Pending after the last Item of the Agenda; the other Items keep their positions. If the Meeting is Published or InProgress and the Presenter is not the Secretary, the Presenter is notified. A Presenter who has declined the Meeting invitation may still be chosen and may still join. (BR-047, BR-048, BR-035)<br>5. The system displays MSG60 – "Agenda item created successfully." Authorized users viewing the Agenda see the new Item. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid agenda item information<br>1. At Step 3, required values are invalid.<br>2. No Item is created. The system displays MSG66 – "Invalid agenda item information." with the applicable field messages.<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Agenda planning no longer permitted<br>1. At Step 3, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. No Item is created. The system displays MSG67 – "The agenda for this meeting can no longer be modified."<br>3. The use case ends. |  |  |

##### 5.1.2 UC-23b – Update Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to update the Title, Presenter and Description of an Agenda Item, whatever its execution status, while Agenda planning is permitted. Document links are not part of this form; they change only through UC-36b and UC-36d. |  |  |
| Preconditions | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to the Meeting and the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-058, BR-059)<br>● PRE-3: Title, Presenter and Description may be changed whether the Item is Pending, InProgress or Done. |  |  |
| Postconditions | ● POST-1: The Item information is updated; its document links are unchanged.<br>● POST-2: Meeting membership, Item execution status, actual timestamps and Meeting lifecycle remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |  |  |
| Normal Flow | Update Agenda Item<br>1. The Secretary selects an Item and chooses Edit.<br>2. The system displays the current Title, Presenter and Description for editing, whatever the Item's execution status. Document links are not changed here: they are added or removed until the Meeting ends through UC-36b and UC-36d. The Presenter is chosen from the current internal participants and Guests of the Meeting. (BR-064, BR-065, BR-070, BR-071, BR-078)<br>3. The Secretary enters permitted changes and submits.<br>4. The system rechecks the active Secretary assignment, Meeting status (Draft, WaitingApproval, Rejected, Approved, Published or InProgress), required values and the Presenter. (BR-058, BR-059, BR-064, BR-065)<br>5. The system saves the changes, keeping the execution status, actual times and position. If the Presenter was assigned or changed while the Meeting is Published or InProgress, the new Presenter is notified unless it is the Secretary who made the change. A Presenter who has declined the Meeting invitation may still be chosen and may still join. (BR-047, BR-048, BR-035)<br>6. The system displays MSG61 – "Agenda item updated successfully." Authorized users viewing the Agenda see the change. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Invalid agenda item information<br>1. At Step 4, the submitted values are invalid.<br>2. The Item remains unchanged. The system displays MSG66 – “Invalid agenda item information.” with the applicable field messages.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Editing no longer permitted<br>1. At Step 4, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. The Item remains unchanged. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends. |  |  |

##### 5.1.3 UC-23c – Delete Agenda Item

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to delete only a Pending Agenda Item from the Meeting. |  |  |
| Preconditions | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Item belongs to a Meeting in Draft, WaitingApproval, Rejected, Approved, Published or InProgress. (BR-058, BR-059)<br>● PRE-3: The Item is Pending (not started). Only not-started Items can be deleted; InProgress and Done Items cannot. (BR-063) |  |  |
| Postconditions | ● POST-1: The selected Pending Item and its document links are removed. The documents stay in the Meeting and keep links to other Items; a document with no remaining links becomes a general Meeting document.<br>● POST-2: Remaining Item positions and execution data, and the Meeting lifecycle, are unchanged.<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |  |  |
| Normal Flow | Delete Agenda Item<br>1. The Secretary selects a Pending Item, chooses Delete and confirms.<br>2. The system verifies the active Secretary assignment, permitted Meeting status, Item membership and that the Item is still Pending. (BR-058, BR-059, BR-063)<br>3. The system removes the Item and its document links, keeps the documents in the Meeting and preserves the remaining Item positions.<br>4. The system displays MSG62 – "Agenda item deleted successfully." Authorized users viewing the Agenda see the change. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda planning no longer permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. Nothing is deleted. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends.<br>EX-2 – Agenda item has already started<br>1. At Step 2, the Item is InProgress or Done.<br>2. The Item and Agenda order remain unchanged. The system displays MSG142 – "Only Pending agenda items can be deleted."<br>3. The use case ends. |  |  |

##### 5.1.4 UC-23d – Reorder Agenda Items

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Secretary to change the order of the Agenda Items within the same Meeting, whatever their execution status; Done Items may also be reordered. |  |  |
| Preconditions | ● PRE-1: The Secretary or the Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress and contains at least two Agenda Items. (BR-058, BR-059) |  |  |
| Postconditions | ● POST-1: The Items of the Meeting are in the requested order.<br>● POST-2: Item membership, execution status and actual times remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the new order. |  |  |
| Normal Flow | Reorder Agenda Items<br>1. The Secretary changes the relative order of the Agenda Items.<br>2. The system verifies the active Secretary assignment, permitted Meeting status and the latest saved Agenda. (BR-058, BR-059)<br>3. The system verifies that each current Item of the Meeting appears exactly once and all Items belong to this Meeting. (BR-058, BR-059)<br>4. The system saves the new order; no Item is left in a partial order.<br>5. The system displays MSG63 – "Agenda order updated successfully." Authorized users viewing the Agenda see the new order. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Reordering no longer permitted<br>1. At Step 2, the Meeting no longer permits Agenda planning or the actor is no longer its active assigned Secretary or Chair.<br>2. The saved order remains unchanged. The system displays MSG67 – “The agenda for this meeting can no longer be modified.”<br>3. The use case ends.<br>EX-2 – Invalid or outdated agenda order<br>1. At Step 3, the order submitted duplicates or omits a current Item, includes an Item from another Meeting, or no longer matches the current Agenda.<br>2. No order changes are saved. The system displays MSG143 – "The agenda order is invalid or out of date. Refresh the agenda and try again." and shows the latest Agenda.<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

#### 5.2 UC-24 – Set Current Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to start any Pending Agenda Item as the current Item, whatever its position in the Agenda. An Item must be started before it can be finished (UC-25). |  |  |
| Preconditions | ● PRE-1: The Chair is authenticated, active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Item is Pending. (BR-060, BR-061, BR-062)<br>● PRE-3: No other Item of the Meeting is InProgress. (BR-060, BR-061, BR-062) |  |  |
| Postconditions | ● POST-1: The Agenda Item becomes InProgress and the current item for that Meeting.<br>● POST-2: Its actual start time is recorded. |  |  |
| Normal Flow | Set Current Agenda Item<br>1. The Chair selects a Pending Item and chooses Set as Current.<br>2. The system rechecks the active Chair assignment, Meeting InProgress status and that the selected Item belongs to the Meeting and remains Pending. (BR-058, BR-059, BR-060, BR-061, BR-062)<br>3. The system verifies that no other Item is InProgress. The position of the selected Item does not matter. (BR-060, BR-061, BR-062)<br>4. The system changes the Item to InProgress and records its actual start time. Only one Item of the Meeting can be InProgress at a time. (BR-060, BR-061, BR-062)<br>5. The system displays MSG64 – "Current agenda item updated successfully." Authorized users viewing the Agenda see the current Item. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda item cannot become current<br>1. At Step 2, the actor is not the active assigned Chair, the Meeting is not InProgress, or the selected Item does not belong to the Meeting or is not Pending.<br>2. The Agenda remains unchanged. The system displays MSG73 – “This agenda item cannot be set as current.”<br>3. The use case ends.<br>EX-2 – Another agenda item is in progress<br>1. At Step 3 or Step 4, another Item is already InProgress.<br>2. The selected Item remains Pending. The system displays MSG72 – “Another agenda item is already in progress. Complete it before starting another agenda item.”<br>3. The use case ends. |  |  |

#### 5.3 UC-25 – Complete Agenda Item

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the active assigned Chair to complete the current Agenda Item at any time. An Item must have been started (InProgress) before it can be finished; a Pending Item cannot be completed directly. |  |  |
| Preconditions | ● PRE-1: The Chair is active and assigned to the Meeting.<br>● PRE-2: The Meeting is InProgress and the selected Agenda Item is currently InProgress. |  |  |
| Postconditions | ● POST-1: The current Item becomes Done with its actual end time.<br>● POST-2: The Meeting remains InProgress. No next Item starts automatically. (BR-060, BR-061, BR-062)<br>● POST-3: Authorized users viewing the Agenda see the updated Agenda. |  |  |
| Normal Flow | Complete Agenda Item<br>1. The Chair selects Complete for the current Item.<br>2. The system verifies the active Chair assignment, Meeting InProgress status, Item membership and current InProgress status. (BR-058, BR-059, BR-060, BR-061, BR-062)<br>3. The system records the actual end time and changes the Item to Done. (BR-060, BR-061, BR-062)<br>4. The Meeting stays InProgress with no current Item. The Chair may start any remaining Pending Item through UC-24.<br>5. The system displays MSG65 – "Agenda item completed successfully." Authorized users viewing the Agenda see the change. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Agenda item cannot be completed<br>1. At Step 2, the Chair is no longer active or assigned, the Meeting is not InProgress, or the Item does not belong to the Meeting or is not its current InProgress Item.<br>2. Nothing changes. An Item that does not belong to this Meeting is treated as not found and no message is displayed. Otherwise the system displays MSG74 – “This agenda item cannot be completed.”<br>3. The use case ends. |  |  |

#### 5.4 UC-26 – View Meeting Agenda

| Primary Actors | Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view the complete Meeting Agenda and automatically see saved changes while the Agenda remains open. |  |  |
| Preconditions | ● PRE-1: The user may view the selected Meeting. (BR-016, BR-017) A Guest with a verified, current invitation sees the Agenda read-only while the Meeting is Published or InProgress; once the Meeting has ended, the Guest can no longer view it. (BR-055) |  |  |
| Postconditions | ● POST-1: Agenda title, presenter, order, execution status, recorded actual times and any recorded Item conclusion are shown read-only.<br>● POST-2: The Agenda shown reflects every saved change: creation, editing, deletion, reordering, Item start and completion, and completion of the current Item when the Meeting ends.<br>● POST-3: Viewing does not modify the Agenda. |  |  |
| Normal Flow | View Meeting Agenda<br>1. The user opens the Agenda of the Meeting.<br>2. The system verifies Meeting access and retrieves the complete saved Agenda in its current order. (BR-016, BR-017, BR-058, BR-059)<br>3. The system shows Item title, presenter, status, order, recorded actual times and any Item conclusion with who last saved it and when.<br>4. While the user keeps the Agenda open, every saved change made by an authorized user appears without reloading the page, as long as the user may still view the Meeting. Changes that failed are never shown. |  |  |
| Alternative Flows | AF-1 – Empty meeting agenda<br>1. At Step 3, no Agenda Items exist.<br>2. The system displays the empty Agenda state.<br>3. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Meeting not accessible<br>1. At Step 2 or Step 4, the Meeting does not exist or the user lacks access.<br>2. No further Agenda information is disclosed.<br>3. The use case ends. |  |  |

#### 5.5 UC-27 – Record Agenda Item Conclusion

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to record or update the conclusion of an Agenda Item: what was concluded or agreed for that item. Item conclusions are used when the minutes draft is generated (UC-70). Vote results and conclusions remain recorded through UC-67. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary.<br>● PRE-2: The selected Agenda Item belongs to the Meeting and has been started, that is InProgress or Done; a Pending Item has no conclusion. (BR-058, BR-059, BR-066, BR-067)<br>● PRE-3: The Meeting is InProgress, or Ended and its minutes draft has not yet been generated. (BR-066, BR-067) |  |  |
| Postconditions | ● POST-1: The Item has one conclusion, replacing any earlier one, with the actor and time of the last save recorded. (BR-066, BR-067)<br>● POST-2: Item status, actual times, presenter, documents and position remain unchanged.<br>● POST-3: Authorized users viewing the Agenda see the saved conclusion. |  |  |
| Normal Flow | Record Agenda Item Conclusion<br>1. The actor selects an InProgress or Done Agenda Item and chooses Record conclusion.<br>2. The system displays the current conclusion of the Item, if any, for editing.<br>3. The actor enters or edits the conclusion and saves.<br>4. The system rechecks the Chair or Secretary assignment, the Meeting status and minutes state, the Item status, and that the conclusion is not blank and does not exceed 2,000 characters. (BR-066, BR-067)<br>5. The system saves the conclusion with the actor and the time. (BR-066, BR-067)<br>6. The system displays MSG271 – "Agenda item conclusion saved." Authorized users viewing the Agenda see the conclusion. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Conclusion is blank or too long<br>1. At Step 4, the conclusion is blank or longer than 2,000 characters. (BR-066, BR-067)<br>2. Nothing is saved. The system displays MSG272 – "Enter a conclusion of up to 2,000 characters."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Conclusion can no longer be changed<br>1. At Step 4, the actor is no longer the current Chair or Secretary, the Item is Pending, the Meeting is not InProgress or Ended, or the minutes draft has already been generated. (BR-066, BR-067)<br>2. Nothing is saved; any earlier conclusion remains. The system displays MSG273 – "The conclusion of this agenda item can no longer be changed."<br>3. The use case ends. |  |  |

#### 5.6 UC-28 – Take Meeting Notes

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to type running notes during the sitting, each attached to the agenda item under discussion or to the Meeting as a whole, so that the minutes are written from what was noted at the time rather than from memory. (BR-134) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-134)<br>● PRE-2: The Meeting is InProgress, or it has Ended and no minutes draft has been built yet. (BR-134) |  |  |
| Postconditions | ● POST-1: The note of the chosen agenda item, or of the Meeting as a whole, holds the text the actor typed, with the time it was saved.<br>● POST-2: Text that could not reach the system yet stays on the actor's device until it is sent; for a Secret Meeting, only until the page is closed.<br>● POST-3: No agenda item, conclusion, minutes or other Meeting record is changed. |  |  |
| Normal Flow | Take Meeting Notes<br>1. During the sitting, the actor opens the Ghi chú panel — on the Meeting page or inside the online room, without leaving the call.<br>2. The system loads the actor's notes for the Meeting and opens the note of the agenda item that is InProgress.<br>3. The actor types. A line may start with a mark — Ý kiến, Kết luận or Nhiệm vụ. When the minutes draft is later built without an AI draft (UC-70), each item's note goes under that item in section II, lines marked Kết luận go to section III and lines marked Nhiệm vụ go to “Nhiệm vụ, sản phẩm và thời hạn”. (BR-133)<br>4. Shortly after the actor stops typing, the system checks that the actor is still the current Secretary, that the Meeting still accepts notes, that the item still belongs to the Meeting, that the text is within 20,000 characters and that the note was not saved elsewhere since the actor read it. (BR-134)<br>5. The system saves the note and shows the time it was saved: MSG415 – “Note saved.” |  |  |
| Alternative Flows | AF-1 – Note on another item or on the Meeting as a whole<br>1. At Step 2 or Step 3, the actor selects another agenda item, or “Chung” [General].<br>2. The system opens the actor's note of that choice.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – The Chair moves to another item<br>1. After Step 2, the Chair sets another agenda item as current (UC-24).<br>2. The system keeps the text already typed with the earlier item and opens the note of the new current item for what is typed next.<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – The connection is lost<br>1. At Step 4, the text cannot reach the system.<br>2. The system keeps the text on the actor's device and displays MSG423 – “You are offline. The note is kept on this device and will be saved when the connection returns.”<br>3. When the connection returns, the flow resumes at Step 4 of the Normal Flow.<br>AF-4 – The Chair reads the notes<br>1. At Step 1, the actor is the current Chair.<br>2. The system shows the current Secretary's notes read-only. (BR-134)<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Actor is not the current secretary<br>1. At Step 4, the actor is no longer the current Secretary. (BR-134)<br>2. Nothing is saved; the typed text stays in the panel so it can be copied. The system displays MSG416 – “Only the Secretary may write meeting notes.”<br>3. The use case ends.<br>EX-2 – Meeting no longer accepts notes<br>1. At Step 4, the Meeting is not InProgress, or it has Ended and its minutes draft has been built. (BR-134)<br>2. Nothing is saved; the typed text stays in the panel. The system displays MSG417 – “Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created.”<br>3. The use case ends.<br>EX-3 – Note was changed in another window<br>1. At Step 4, the note was saved from another window or device after the actor read it.<br>2. Nothing is overwritten. The system shows the latest text next to the actor's unsaved text and displays MSG418 – “This note was changed in another window. Reload to see the latest version.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

### 6. FE-06 – Meeting Participant Management

#### 6.1 UC-29 – View Meeting Participants

| Primary Actors | Secretary, Chair, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized internal participant or verified Guest to view and search the current Meeting roster, including internal participants and Guests. |  |  |
| Preconditions | ● PRE-1: The actor has current authorized Meeting participation or verified Guest access.<br>● PRE-2: Roster visibility follows the actor’s current Meeting permissions. (BR-016, BR-017, BR-030, BR-052) |  |  |
| Postconditions | ● POST-1: The authorized current roster is displayed.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Meeting Participants<br>1. The actor opens the Participants view of an accessible Meeting.<br>2. The system rechecks current Meeting access and roster-view permission. (BR-016, BR-017)<br>3. The system displays current internal participants and Guests with invitation identity and Meeting Role or Guest participation type. Where available, a Guest’s self-declared display name and job title are shown separately from the verified email. A Guest actor sees names and roles but not other people’s emails, only their own. Removed or revoked entries are excluded. (BR-045, BR-016, BR-017)<br>4. The actor reviews the roster. |  |  |
| Alternative Flows | AF-1 – Search participants<br>1. At Step 1, the actor enters a name or email in the search field; a Guest actor’s search matches names only. (BR-016, BR-017)<br>2. The system filters the current authorized roster to matching entries, ignoring letter case and Vietnamese diacritics.<br>3. The actor reviews the matching entries; the flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Roster is not accessible<br>1. At Step 2, the actor lacks current Meeting access or roster-view permission.<br>2. The system discloses no roster data and treats the Meeting as not found. (BR-017)<br>3. The use case ends. |  |  |

#### 6.2 UC-30 – Manage Meeting Participants

##### 6.2.1 UC-30a – Add Meeting Participant

| Primary Actors | Secretary, Chair | Secondary Actors | Resend Email |
| --- | --- | --- | --- |
| Description | Allows a current Secretary to add internal participants or invite a Guest to the Meeting. The initial participants of a new Meeting are chosen in the Create Meeting form (UC-13); this use case adds people afterwards. |  |  |
| Preconditions | ● PRE-1: The actor is a current assigned Secretary or a current Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits roster management. WaitingApproval remains read-only. (BR-036, BR-037, BR-040) |  |  |
| Postconditions | ● POST-1: The selected internal participants or Guest invitation is saved.<br>● POST-2: New invitations start with Pending response.<br>● POST-3: Guest receives no internal or system Role. (BR-049, BR-050, BR-051) |  |  |
| Normal Flow | Add Internal Participants<br>1. The actor opens participant management for the Meeting.<br>2. A current Secretary selects one or more active internal users, searching by name or email when needed, and assigns the shared Meeting Role.<br>3. The actor confirms the selection.<br>4. The system rechecks actor authority, Meeting status, selected accounts, uniqueness and required roles. (BR-036, BR-037, BR-038, BR-049, BR-050, BR-051, BR-039, BR-040)<br>5. The system adds the selected participants with a Pending response; either all of them are added or none is. When the Meeting is Published or InProgress, they are invited.<br>6. The system displays MSG96 – “Meeting participant added successfully.” |  |  |
| Alternative Flows | AF-1 – Invite external Guest<br>1. At Step 2, the Secretary selects Invite Guest and enters the invitation name and email.<br>2. For a Secret Meeting, the system warns that the Meeting is Secret without asking for a further confirmation.<br>3. At Step 4, the system validates the invited email and its uniqueness against current internal participants and Guest invitations.<br>4. At Step 5, the system creates one Guest invitation with a Pending response and, when the Meeting is Published or InProgress, sends it by email. The Guest later verifies the email through UC-04.<br>5. The system displays MSG96 – “Meeting participant added successfully.” The use case ends. (BR-049, BR-050, BR-051, BR-052, BR-053, BR-054, BR-044, BR-055) |  |  |
| Exception Flows | EX-1 – Participant information or selection is invalid<br>1. At Step 4, an internal account is inactive, already current, or has an invalid Meeting Role; or the Guest name/email is missing, invalid or already represented in the Meeting.<br>2. No participant batch or Guest invitation is partially saved. The system displays MSG97 – “Invalid meeting participant information.” with the applicable field message.<br>3. The actor corrects the selection. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not allowed to manage participants<br>1. At Step 4, the actor can view the Meeting but is not the current Secretary or the current Chair.<br>2. No change is saved. The system displays MSG85 – “You do not have permission to perform this action.” The use case ends.<br>EX-3 – Participant management is no longer permitted<br>1. At Step 4, the Meeting status no longer permits the requested roster action.<br>2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends. |  |  |

##### 6.2.2 UC-30b – Manage Meeting Role

| Primary Actors | Secretary, Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to change internal Meeting Roles. Before the Meeting is Published, the Secretary may also replace the Chair. From publication onward, only the current Chair can give the Chair role to someone else, through UC-34; the only exception is that the Secretary may replace the Chair when the Chair's account is locked. After publication, the Chair and the Secretary do not swap roles. The Chair and the Secretary may both manage participants and roles while the Meeting status permits roster changes. To change the Secretary, give the Secretary role to the new person, then change the previous Secretary to Member. (BR-041, BR-042) |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary or a current authorized Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits participant management, and the selected target is a current internal participant. (BR-036, BR-037) |  |  |
| Postconditions | ● POST-1: Approved role changes are saved for this Meeting.<br>● POST-2: A Chair replacement changes both affected internal Meeting Roles together. System Role and Invitation Response remain unchanged. (BR-043, BR-041, BR-042) |  |  |
| Normal Flow | Manage Meeting Role<br>1. The Secretary opens a current participant’s role details.<br>2. The system displays the current Meeting Role.<br>3. The Secretary changes the internal participant’s Meeting Role.<br>4. The Secretary confirms the changes.<br>5. The system rechecks current authority, Meeting status, role constraints and required roles. Draft, Rejected and Approved may have no Secretary; Published and InProgress must retain or replace the last Secretary in the same save. (BR-040, BR-016, BR-017)<br>6. The system saves all requested changes together. When the Meeting is Published or InProgress, the affected participants are notified.<br>7. If no requested change is made, the system displays MSG154 – “No changes to save.” Otherwise it displays MSG100 – “Meeting role updated successfully.” |  |  |
| Alternative Flows | AF-1 – Replace the chair<br>1. At Step 3, while the Meeting is not yet Published, or once it is Published or InProgress and the Chair's account is deactivated, the Secretary selects a current active internal participant as the new Chair. After publication the selected participant must not be the Secretary. (BR-041, BR-042) Before saving, the system asks for confirmation and names the participant who will become a Member.<br>2. The system changes the former Chair to Member and the selected participant to Chair in the same save, subject to the current Secretary requirements.<br>3. The system notifies the affected participants when required, but never the actor who made the change: a Chair reassigning his or her own role notifies only the new Chair, while a Secretary replacing the Chair notifies both the former and the new Chair. (BR-035) The system displays MSG100 – “Meeting role updated successfully.” The use case ends. (BR-040, BR-041, BR-042) |  |  |
| Exception Flows | EX-1 – Meeting role is invalid<br>1. At Step 5, the selected Meeting Role is invalid.<br>2. No requested change is saved. The system displays MSG101 – “A valid meeting role is required.”<br>3. The Secretary may correct the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Required meeting role would be lost<br>1. At Step 5, the change would violate the Chair/Secretary requirement for the current Meeting status.<br>2. No role change is saved. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.” The Secretary may revise the change. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Actor, target or meeting is no longer eligible<br>1. At Step 5, the Secretary assignment, target relationship or Meeting status changed after the details were opened.<br>2. No change is saved. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends. |  |  |

##### 6.2.3 UC-30c – Remove Meeting Participant

| Primary Actors | Secretary, Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an assigned Secretary to remove a current internal participant or revoke a Guest invitation while preserving required Meeting records. |  |  |
| Preconditions | ● PRE-1: The actor is a current authorized Secretary or a current authorized Chair, while the Meeting status permits roster changes.<br>● PRE-2: The Meeting permits participant management. The selected internal participant or Guest invitation is current. (BR-036, BR-037) |  |  |
| Postconditions | ● POST-1: The selected participant is no longer current, or the Guest invitation is revoked.<br>● POST-2: Access granted by the removed relationship ends, including every active session of a revoked Guest invitation; independent valid access remains effective. A Guest in the online room leaves the room when the invitation is revoked. (BR-053, BR-054)<br>● POST-3: Invitation Response and attendance history are retained. (BR-044, BR-045)<br>● POST-4: Agenda Items presented by the removed participant or revoked Guest no longer have a Presenter. (BR-064, BR-065) |  |  |
| Normal Flow | Remove Meeting Participant<br>1. The Secretary selects a current internal participant or Guest invitation and confirms removal or revocation.<br>2. The system rechecks the Secretary assignment, Meeting status, selected relationship and required Meeting Roles. The Meeting must retain its required Chair and Secretary; a Secretary may be removed only together with a valid replacement in the same save. (BR-036, BR-037, BR-040, BR-041, BR-042)<br>3. The system removes the internal assignment or revokes the Guest invitation, ends access granted by that relationship, and retains historical evidence.<br>4. When the Meeting is Published or InProgress, the removed person is notified; the notification does not open the Meeting.<br>5. The system displays MSG103 – “Meeting participant removed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Removal is no longer permitted<br>1. At Step 2, the Meeting status no longer permits roster changes, or the selected relationship is no longer current.<br>2. Nothing is removed or revoked. The system displays MSG98 – “The meeting participant list cannot be modified in its current status.” The use case ends.<br>EX-2 – Removal would violate a required meeting role<br>1. At Step 2, removing the selected participant would leave the Meeting without its required Chair or Secretary and no valid replacement is included in the same save.<br>2. The participant or Guest invitation remains current and access remains effective. The system displays MSG109 – “The meeting must retain its required Chair and Secretary. Assign a replacement first.”<br>3. The Secretary may revise the change to include the required replacement. The flow resumes at Step 1 of the Normal Flow.<br>EX-3 – Actor is not allowed to manage participants<br>1. At Step 2, the actor can view the Meeting but is not the current Secretary or the current Chair.<br>2. Nothing is removed or revoked. The system displays MSG85 – “You do not have permission to perform this action.” The use case ends. |  |  |

#### 6.3 UC-31 – Respond to Meeting Invitation

| Primary Actors | Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an invitee other than the Chair to confirm in advance whether they will attend, by accepting or declining their own Meeting invitation. The answer is given once and cannot be changed afterwards. The Chair does not respond because the Chair runs the Meeting. (BR-046) An invitee of a meeting created from a found time slot (UC-81b) accepts or declines it like any other invitation. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published or InProgress.<br>● PRE-2: The actor owns a current invitation and is not the Chair of the Meeting; a Guest has verified access through UC-04. (BR-046, BR-049, BR-050, BR-051, BR-052, BR-053, BR-054) |  |  |
| Postconditions | ● POST-1: The response and its response time are recorded once and are not changed afterwards.<br>● POST-2: Meeting Role, attendance and the invitee's access to the Meeting, its documents and its online room are unchanged. A Declined response only hides the Meeting from the invitee's own calendar, and an invitee who has not answered is treated as attending. (BR-043, BR-047, BR-048) |  |  |
| Normal Flow | Respond to Meeting Invitation<br>1. The invitee, who has not answered yet, sees the attendance question on the Meeting in the calendar and, while the Meeting is Published, on the Meeting details; a verified Guest sees it on the Guest Meeting page. An answer is still accepted while the Meeting is InProgress.<br>2. The system identifies the invitee and offers the question only while the invitation has no answer.<br>3. The invitee selects Accepted, or selects Declined and confirms it. The confirmation states that the answer is given once and cannot be changed and, for an internal invitee, that the Meeting will no longer appear on their calendar. (BR-046, BR-047, BR-048)<br>4. The system rechecks invitation ownership, that the actor is not the Chair, Meeting status and response value, and compares the value with any response already recorded. (BR-046)<br>5. The system saves the response and response time once.<br>6. The system confirms the update with MSG105 – “Meeting invitation response recorded successfully.” |  |  |
| Alternative Flows | AF-1 – Same response submitted again<br>1. At Step 4, the invitee submits the same response that is already recorded.<br>2. The system keeps the recorded response and response time unchanged and displays MSG154 – "No changes to save." The use case ends. (BR-046)<br>AF-2 – Invitee does not confirm declining<br>1. At Step 3, the invitee selects Declined, then does not confirm it.<br>2. No response is recorded, and the attendance question stays available. The use case ends. |  |  |
| Exception Flows | EX-1 – Invitation is no longer answerable<br>1. At Step 4, the Meeting status or invitation state no longer permits a response.<br>2. The prior response remains unchanged. For a Guest, email verification and Guest-access failures remain in UC-04.<br>3. The system displays MSG107 – “This meeting invitation can no longer be answered.” The use case ends.<br>EX-2 – Response value is invalid<br>1. At Step 4, the submitted value is not Accepted or Declined.<br>2. The prior response and response time remain unchanged. The system displays MSG106 – “Invalid meeting invitation response.” The invitee may correct the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Different response after answering<br>1. At Step 4, the invitee has already answered and submits a different response. (BR-046)<br>2. The recorded response and response time remain unchanged. The system displays MSG285 – “You have already answered this invitation. Your answer cannot be changed.” The use case ends. |  |  |

#### 6.4 UC-32 – View Participant Response Status

| Primary Actors | Secretary, Chair, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows authorized internal participants and verified Guests to view the current invitation responses of the Meeting, shown as a label beside each invitee's name. |  |  |
| Preconditions | ● PRE-1: The Meeting is Published or a later status that remains readable. A Guest can read it only while the Meeting is Published or InProgress. (BR-055)<br>● PRE-2: The actor can currently view the Meeting. |  |  |
| Postconditions | ● POST-1: The responses of current invitees are displayed as labels beside their names. The Chair and an invitee who has not answered have no label. No totals are shown.<br>● POST-2: No data is changed. |  |  |
| Normal Flow | View Participant Response Status<br>1. The actor opens the participant list of an accessible Meeting.<br>2. The system checks that the actor may view the Meeting. (BR-055)<br>3. The system displays each current invitee’s name and email (a Guest actor sees names only, apart from their own email) with a label for the response given, Accepted or Declined; an invitee who has not answered and the Chair have no label. No totals by status are shown.<br>4. No data is changed. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Responses are not accessible<br>1. At Step 2, the actor lacks current Meeting access.<br>2. The system discloses no invitee or response and treats the Meeting as not found. (BR-016, BR-017)<br>3. The use case ends. |  |  |

#### 6.5 UC-33 – Record Attendance

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the assigned Secretary to record or revise attendance for one or more selected current internal participants or Guests while the Meeting is InProgress. |  |  |
| Preconditions | ● PRE-1: The actor is an active current assigned Secretary.<br>● PRE-2: The Meeting is InProgress. (BR-056, BR-057) |  |  |
| Postconditions | ● POST-1: Submitted people have Present or Absent saved with the recording Secretary and save time.<br>● POST-2: Unsubmitted people retain their saved value or remain unmarked; Invitation Responses are unchanged.<br>● POST-3: All submitted changes succeed together or none are saved. Attendance completeness never gates End Meeting. (BR-043, BR-056, BR-057) |  |  |
| Normal Flow | Record Attendance<br>1. The Secretary opens attendance for an InProgress Meeting, from the Meeting page or from the online meeting room.<br>2. The system displays current internal participants and Guests with their saved attendance; people never recorded remain unmarked. No Present/Absent totals are shown.<br>3. The Secretary selects Present or Absent for one or more people and submits only those entries.<br>4. The system rechecks current authority, Meeting status, that each submitted person is still current and that their attendance was not changed by someone else since it was shown, and each submitted value. Omitted people are not treated as errors. (BR-056, BR-057)<br>5. The system saves all submitted changes or none of them, records the Secretary and save time, keeps every omitted person’s saved or unmarked value, and displays MSG147 – “Meeting attendance saved successfully.” |  |  |
| Alternative Flows | AF-1 – Revise saved attendance<br>1. At Step 3, the Secretary changes the saved attendance for one or more current people.<br>2. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Record the full roster<br>1. At Step 3, the Secretary selects every current internal participant and Guest. Recording the full roster is optional; it is not required to save attendance or to end the Meeting.<br>2. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Submitted attendance value is invalid<br>1. At Step 4, a submitted value is not Present or Absent.<br>2. No submitted change is saved. The system displays MSG148 – “Select Present or Absent for each submitted participant or Guest.” The Secretary corrects the value. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Meeting is no longer in progress<br>1. At Step 4, the Meeting changed from InProgress before the submission was saved.<br>2. No submitted change is saved. The system displays MSG149 – “Attendance can only be recorded while the meeting is in progress.” The use case ends.<br>EX-3 – Secretary is no longer authorized<br>1. At Step 4, the actor is no longer the active current assigned Secretary.<br>2. No submitted change is saved. The system denies the operation under current authorization; existing attendance remains unchanged. The use case ends. |  |  |

Delivery scope: UC-33 attendance is delivered separately from the roster, invitation and response use cases. Removal or revocation of a participant must preserve historical response and attendance evidence. Shared FE-06 documentation does not merge these deliveries.

#### 6.6 UC-34 – Hand Over Chair to Another Participant

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to give the Chair role to another internal participant in one step, for example before leaving the Meeting. The system changes both roles together: the selected participant becomes Chair and the former Chair becomes Member. The Chair does not need to change his or her own role separately. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair of the Meeting.<br>● PRE-2: The Meeting is Published or InProgress. (BR-041, BR-042) |  |  |
| Postconditions | ● POST-1: The selected participant is the Chair and the former Chair is a Member, saved together. (BR-041, BR-042)<br>● POST-2: The Meeting still has exactly one Chair and its required Secretary. System Roles, Invitation Responses, attendance and recorded ballots remain unchanged. (BR-041, BR-042)<br>● POST-3: The new Chair is notified in the application. The former Chair, who made the change, is not notified in the application but still receives the email. (BR-035)<br>● POST-4: A role change takes effect in the online room without the people rejoining, when the room is open; it is not blocked while the Meeting is in progress. |  |  |
| Normal Flow | Hand Over Chair to Another Participant<br>1. The Chair opens the Meeting participant list and chooses to change the role of the participant who will receive the Chair role.<br>2. The system displays the Meeting Roles, each with a short description of what the role allows. The Chair role is offered only for a current active internal participant who is neither a Guest nor the current Secretary. (BR-041, BR-042)<br>3. The Chair selects the Chair role and chooses Hand over Chair. The system asks once more, stating that the Chair will become a Member and no longer run the Meeting, and the Chair confirms.<br>4. The system rechecks that the actor is still the current Chair, the Meeting is Published or InProgress, and the selected person can still receive the Chair role. (BR-041, BR-042)<br>5. The system changes the selected participant to Chair and the former Chair to Member in the same save. (BR-041, BR-042)<br>6. The system notifies the new Chair in the application. The former Chair, who made the change, is not notified in the application but still receives the email. (BR-035)<br>7. The system displays MSG257 – "Chair role handed over successfully." The former Chair stays in the Meeting as a Member and may leave the meeting room. (BR-041, BR-042) |  |  |
| Alternative Flows | AF-1 – No participant can receive the Chair role<br>1. At Step 2, the Meeting has no current active internal participant other than the Chair and the Secretary.<br>2. The system offers the Chair role for no participant. No role changes.<br>3. The use case ends.<br>AF-2 – Chair does not confirm the hand-over<br>1. At Step 3, the Chair does not confirm.<br>2. No role changes and the flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Selected participant can no longer receive the chair role<br>1. At Step 4, the selected person was removed from the Meeting, became the Secretary or is no longer an active internal user. (BR-041, BR-042)<br>2. No role is changed. The system displays MSG259 – "The selected participant can no longer receive the Chair role. Select another participant."<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Hand-over is no longer permitted<br>1. At Step 4, the actor is no longer the current Chair, or the Meeting is no longer Published or InProgress.<br>2. No role is changed. The system displays MSG260 – "You can no longer hand over the Chair role for this meeting."<br>3. The use case ends. |  |  |

### 7. FE-07 – Personal Document Library & Meeting Document Management

15/10/2026: Sixteen detailed Document use cases. UC-36b links an existing Meeting document to an Agenda Item; UC-36d removes only one Item link. Meeting-document reading follows Meeting viewing access. Document additions, removals and Item-link changes stop when the Meeting ends; Agenda Item status does not lock document links. PO follow-up: Meeting documents and comments are read-only after the Meeting ends. Deleting an Agenda Item keeps its documents in the Meeting. Readding a removed document does not restore previous comments. Personal-library quotas remain undecided.

#### 7.1 UC-35 – View Meeting Documents

##### 7.1.1 UC-35a – View Meeting Documents

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to browse the documents currently attached to one Meeting. Guest document access follows Participant rules. (BR-055) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected Meeting exists and the user can currently view the Meeting. (BR-055, BR-079, BR-080, BR-053, BR-054, BR-044) |  |  |
| Postconditions | ● POST-1: Only currently attached documents available to the user are displayed.<br>● POST-2: Meeting documents, personal libraries and Meeting status remain unchanged. |  |  |
| Normal Flow | View Meeting Documents<br>1. The user opens the documents of the selected Meeting.<br>2. The system checks the user's current permission to view the Meeting, including the current invitation for a Guest. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. A Guest with a verified, current invitation may read the documents while the Meeting is Published or InProgress (UC-04); once the Meeting has ended, the Guest can no longer open them. (BR-055, BR-079, BR-080)<br>3. The system displays the Meeting documents grouped by Agenda Item, in agenda order, with their names, file types, the people who added them and comment counts. A document linked to several Agenda Items appears under each of them; a document linked to no Agenda Item and to no Vote appears as a general Meeting document, and a document linked to a Vote is listed in the Vote grouping only. Removed documents are excluded. Agenda Items with no documents are listed as well, so a document can still be attached to them (UC-36b). Changes to the document list appear without reloading. After the Meeting ends, documents and comments are read-only. (BR-074, BR-075, BR-076, BR-079, BR-080)<br>4. The user reviews the displayed groups or empty result, and may switch to another grouping or finish viewing. Selecting a document opens UC-35b. |  |  |
| Alternative Flows | AF-1 – View by Vote or general documents<br>1. At Step 4, the user chooses to view the documents attached to Votes, or the general Meeting documents. The Vote grouping lists, in the order they were created, the Votes the user can see that have documents; a Draft Vote is shown only to the current Chair and Secretary. (BR-086, BR-087)<br>2. The system displays the documents attached to each Vote, each marked Read once the user has confirmed reading it, or Not read while the user can still confirm it as a voter of an Open Vote in an InProgress Meeting (UC-42); or it displays the general documents, which are the documents linked to no Agenda Item and to no Vote, because a document of a Vote is listed in the Vote grouping only.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – No documents to display<br>1. At Step 3, the Meeting has no currently attached documents, or the grouping chosen in AF-1 has no documents.<br>2. The system displays an empty result and explains that no documents are available for the current grouping.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – View documents from the online meeting room<br>1. At Step 1, the user is in the online meeting room of an InProgress Meeting and opens the documents there.<br>2. The system applies the same access check and displays the same documents, starting with the current Agenda Item, then the other Items and the Votes that have documents. Documents cannot be added, linked or removed from the room. (BR-079, BR-080)<br>3. The flow resumes at Step 4 of the Normal Flow; selecting a document opens UC-35b without leaving the room. |  |  |
| Exception Flows | EX-1 – Meeting documents are not accessible<br>1. At Step 2, the Meeting is unavailable or the user can no longer view the Meeting, including removal of a participant or revocation or expiry of a Guest invitation.<br>2. No document names or contents are disclosed. No Meeting information is changed. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends. |  |  |

##### 7.1.2 UC-35b – Preview Meeting Document

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to read a Meeting document online and download the same file when permitted. Comments are covered separately in UC-38. A Guest with a verified, current invitation may read and download documents while the Meeting is Published or InProgress (UC-04), but not after the Meeting has ended. (BR-055, BR-081) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.<br>● PRE-2: The selected document is currently attached to the Meeting and the user can currently view the Meeting. (BR-055, BR-074, BR-075, BR-076, BR-079, BR-080, BR-053, BR-054, BR-044) |  |  |
| Postconditions | ● POST-1: The selected document is displayed for online reading; in AF-1, the user also receives the permitted download.<br>● POST-2: The file content, its Meeting association and its comments remain unchanged.<br>● POST-3: When the user reaches the last page of a Vote document, reading can be confirmed in UC-42. |  |  |
| Normal Flow | Preview Meeting Document<br>1. The user selects a document of the Meeting, of an Agenda Item, or in the online meeting room.<br>2. The system checks current permission to view the Meeting and that the document is still attached to this Meeting. Anyone who can currently view the Meeting may read its documents; there is no separate document-reading permission. (BR-055, BR-074, BR-075, BR-076, BR-079, BR-080)<br>3. The system opens the document over the current page and displays its name, its content and the Meeting sensitivity; the user stays on the page and, in the online meeting room, in the call. For a Secret Meeting, every view is recorded in the audit log, and a view that cannot be recorded is not shown. (BR-010)<br>4. The user reads the document. When the user reaches the last page of a Vote document opened from that Vote, reading can be confirmed through UC-42. After the Meeting ends, the document and comments are read-only. Downloading remains available when the Meeting is not Secret. (BR-081, BR-079, BR-080) |  |  |
| Alternative Flows | AF-1 – Download the document<br>1. At Step 4, the user chooses Download for a Meeting that is not Secret; downloading is not offered for a Secret Meeting.<br>2. The system rechecks current access, that the document remains attached and the current Meeting sensitivity. (BR-055, BR-074, BR-075, BR-076, BR-081, BR-079, BR-080)<br>3. The system provides the same uploaded file to the user. (BR-068, BR-010)<br>4. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Document is no longer accessible<br>1. At Step 2, or during the access check in AF-1, the document was removed, the user can no longer view the Meeting, or the document is linked only to a Draft Vote and to no Agenda Item and the actor is not the current Chair or Secretary. (BR-079, BR-080)<br>2. No further document content or download is provided. The file and Meeting remain unchanged. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-2 – Download requested for a Secret Meeting<br>1. During Step 2 of AF-1, the Meeting is Secret, including when its sensitivity changed after the viewer opened.<br>2. No download is provided. The document remains unchanged. The system displays MSG159 – “Secret Meeting documents can only be viewed online and cannot be downloaded.” (BR-081)<br>3. The flow resumes at Step 4 of the Normal Flow for online reading.<br>EX-3 – The stored file no longer exists<br>1. At Step 3, or at Step 3 of AF-1, the document is still attached to the Meeting but its file is no longer in storage.<br>2. Nothing is changed. The system displays MSG382 – “The file of this document is no longer in storage and cannot be opened. Please contact the administrator.” and does not offer to try again.<br>3. The use case ends. |  |  |

#### 7.2 UC-36 – Manage Meeting Documents

##### 7.2.1 UC-36a – Add Document to Meeting

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to add one or more documents from their own personal library or computer to a Meeting in one submission; files from the computer are added one at a time. Linking documents to Agenda Items is handled separately in UC-36b. (BR-070, BR-071, BR-072, BR-073) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the selected Meeting.<br>● PRE-2: The actor can currently view the Meeting. (BR-070, BR-071)<br>● PRE-3: The Meeting has not ended. (BR-070, BR-071) |  |  |
| Postconditions | ● POST-1: All submitted documents appear in the Meeting as general documents, with their names copied at addition and the actor and addition time recorded. (BR-011)<br>● POST-2: Selected personal-library documents stay unchanged. Files uploaded from the computer are also available in the uploader's personal library. (BR-072, BR-073)<br>● POST-3: A removed document added again is a new addition without its previous comments. (BR-074, BR-075, BR-076)<br>● POST-4: Each file uploaded from the computer is added as a whole or not at all, and a file that cannot be added does not stop the other files. Documents chosen from the personal library are added together or none are. Existing documents and Meeting status remain unchanged. |  |  |
| Normal Flow | Add Documents from Personal Library<br>1. The Chair or Secretary opens the Meeting documents and chooses Add Documents.<br>2. The system offers selection from the actor's personal library or upload from the computer.<br>3. The actor selects one or more documents from their own library and submits them.<br>4. The system checks the current Chair or Secretary, Meeting access and status, and that every selected document belongs to the actor, is available and is not already attached to this Meeting. (BR-070, BR-071, BR-072, BR-073)<br>5. The system adds all selected documents to the Meeting under their current names and records who added them and when; either all are added or none is. Personal-library documents stay unchanged; a document added again does not recover old comments. (BR-011, BR-074, BR-075, BR-076)<br>6. The system shows the number added and displays MSG161 – “Documents added to the Meeting successfully.”. |  |  |
| Alternative Flows | AF-1 – Upload files from the computer<br>1. At Step 3, the actor selects one or more files from the computer: at most 10 files totalling at most 100 MB at once. Each file is added on its own.<br>2. Before reading any file, the system performs the authority, Meeting access and status checks from Step 4 of the Normal Flow. An actor who is not the current Chair or Secretary receives MSG162 – “You no longer have permission to add documents to this Meeting.” whatever the files are. (BR-070, BR-071)<br>3. The system checks that every file is no larger than 10 MB and is a valid PDF or DOCX whose content matches its extension, and identifies any file that fails. (BR-069)<br>4. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it to the Meeting and to the uploader's personal library, recording the number of pages of a PDF. The file goes into a folder named after the Meeting title and planned start date; an existing folder is reused and a deleted one is created again. (BR-072, BR-073)<br>5. A file that cannot be added is added to neither place, and the other files continue; a file that fails the checks of Step 3 follows EX-2. Otherwise, the flow resumes at Step 6 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 4, or at Step 2 of AF-1, the actor can still view the Meeting but is no longer its current Chair or Secretary.<br>2. None of the submitted documents are added. The system displays MSG162 – “You no longer have permission to add documents to this Meeting.”<br>3. The use case ends.<br>EX-2 – Selection is empty, unavailable or already attached, or a file is invalid<br>1. At Step 4, no document was selected, or any selected document is unavailable or does not belong to the actor. Or a selected document is already in the Meeting. Or, in AF-1, a file is missing or invalid.<br>2. None are added. The system identifies the unavailable selections within the actor's library and displays MSG249 – “Select one or more available documents from your personal library.” When a selected document is already in the Meeting, the system displays MSG165 – “One or more selected documents are already attached to the Meeting.” In AF-1, when a file is not a valid PDF or DOCX or is larger than 10 MB, the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.”.<br>3. The actor revises the selection. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting no longer accepts documents<br>1. At Step 4, or at Step 2 of AF-1, the Meeting has ended, including while the actor was selecting or uploading files. (BR-070, BR-071)<br>2. None of the submitted documents are added. The system displays MSG172 – “Documents cannot be added after the Meeting has ended.”<br>3. The use case ends. |  |  |

##### 7.2.2 UC-36b – Attach Documents to Agenda Item

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to attach one or more documents to an Agenda Item of the Meeting before the Meeting ends. Documents may be chosen from the Meeting documents, uploaded from the computer or chosen from My Documents; documents not yet in the Meeting are added to it and attached in the same save. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-070, BR-071)<br>● PRE-2: The selected Agenda Item belongs to the Meeting. (BR-078)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-070, BR-071) |  |  |
| Postconditions | ● POST-1: The selected documents are attached to the Agenda Item; their links to other Items remain unchanged. An attached document stops being a general Meeting document, and a document whose last Item link is removed through UC-36d becomes general again.<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. A file uploaded from the computer is added and attached on its own, and a file that fails does not stop the others; if a document chosen from My Documents cannot be added or attached, none of that selection is added or attached.<br>● POST-3: Document contents and comments remain unchanged. |  |  |
| Normal Flow | Attach Documents to Agenda Item<br>1. The actor chooses to attach documents to an Agenda Item.<br>2. The system offers three sources: Meeting documents, Upload from computer and My Documents.<br>3. The actor chooses Meeting documents. The system lists every Meeting document not yet attached to this Item, general documents included.<br>4. The actor selects one or more documents and confirms.<br>5. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the Item belongs to the Meeting, and that every selected document is still in the Meeting and not yet attached to the Item. (BR-070, BR-071, BR-078)<br>6. The system attaches all selected documents to the Item; either all are attached or none is.<br>7. The system displays MSG238 – “Documents attached to the Agenda Item successfully.” |  |  |
| Alternative Flows | AF-1 – Upload from computer<br>1. At Step 3, the actor chooses Upload from computer and selects one or more files. Each file is added on its own.<br>2. The system checks that at most 10 files totalling at most 100 MB were chosen and that every file is a valid PDF or DOCX no larger than 10 MB, and performs the checks of Step 5 that do not concern existing Meeting documents. (BR-070, BR-071, BR-069)<br>3. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it to the Meeting and to the uploader's personal library and attaches it to the Agenda Item, as a whole or not at all. (BR-072, BR-073)<br>4. If the file is invalid or cannot be added or attached, it is not added or attached and the other files continue; the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.” as in UC-36a EX-2. Otherwise, the flow resumes at Step 7 of the Normal Flow.<br>AF-2 – Choose from My Documents<br>1. At Step 3, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the remaining checks of Step 5. (BR-070, BR-071, BR-072, BR-073)<br>3. The system adds the documents to the Meeting and attaches them to the Agenda Item in the same save. Personal-library documents stay unchanged.<br>4. If any document cannot be added or attached, nothing is added or attached; the system displays MSG249 – “Select one or more available documents from your personal library.” as in UC-36a EX-2. A document already in the Meeting is attached through Step 3. Otherwise, the flow resumes at Step 7 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 5, the actor is no longer the current Chair or Secretary.<br>2. Nothing is attached. The system displays MSG240 – “You no longer have permission to link documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Agenda Item or a document is no longer in the Meeting, or already linked<br>1. At Step 5, a selected document is no longer attached to this Meeting. Or the Agenda Item is no longer in the Meeting, or a selected document is already linked to it.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.” When the Agenda Item is no longer in the Meeting, the system displays MSG242 – “Select an available Agenda Item in this Meeting.”; when a document is already linked, MSG243 – “One or more selected documents are already attached to the Agenda Item.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Meeting no longer accepts document links<br>1. At Step 5, or the corresponding check in AF-1 or AF-2, the Meeting has ended. (BR-070, BR-071)<br>2. Nothing is added or attached. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends. |  |  |

##### 7.2.3 UC-36c – Remove Document from Meeting

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to remove a Meeting document before the Meeting ends, regardless of Minutes existence or Agenda Item status. Removal affects every Agenda Item link in this Meeting. (BR-070, BR-071, BR-074, BR-075, BR-076) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or Secretary of the Meeting. (BR-070, BR-071)<br>● PRE-2: The selected document is currently attached to this Meeting.<br>● PRE-3: The actor can currently view the Meeting and the Meeting has not ended. (BR-070, BR-071) |  |  |
| Postconditions | ● POST-1: The document no longer appears in this Meeting's current document list or any of its Agenda Items and is no longer available through this attachment. (BR-074, BR-075, BR-076)<br>● POST-2: Its comments remain in the removal history and do not return if the document is added again; personal-library documents and documents in other Meetings remain unchanged. (BR-074, BR-075, BR-076, BR-011)<br>● POST-3: The actor and removal time are recorded. Meeting lifecycle status is unchanged. |  |  |
| Normal Flow | Remove Document from Meeting<br>1. The Chair or Secretary selects an attached document and chooses Remove from Meeting.<br>2. The system displays a confirmation naming the document and explaining that it will leave this Meeting's list, while its comments remain as history and personal-library documents and other Meetings are unaffected.<br>3. The actor confirms removal.<br>4. The system rechecks the current Chair or Secretary assignment, that the document is still attached, that the actor can currently view the Meeting and that the Meeting has not ended. Minutes existence and Agenda Item status do not block removal. (BR-070, BR-071)<br>5. The system removes the document from this Meeting's current list and every Agenda Item in this Meeting, preserves its comments as history and records the actor and removal time. (BR-074, BR-075, BR-076, BR-011)<br>6. The system displays MSG167 – “Document removed from the Meeting successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 4, the actor is no longer the current Chair or Secretary. (BR-070, BR-071)<br>2. No document is removed and comments remain unchanged. The system displays MSG169 – “You no longer have permission to remove documents from this Meeting.”<br>3. The use case ends.<br>EX-2 – Document is no longer attached<br>1. At Step 4, the selected document is no longer currently attached to this Meeting.<br>2. No further removal or comment change is made. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The use case ends; the actor may refresh the list through UC-35a.<br>EX-3 – Meeting no longer accepts changes<br>1. At Step 4, the Meeting has ended, including when it ended after the confirmation was displayed. (BR-070, BR-071)<br>2. The document remains attached and its comments remain unchanged. The system displays MSG248 – “Documents cannot be removed after the Meeting has ended.”<br>3. The use case ends. |  |  |

##### 7.2.4 UC-36d – Detach Document from Agenda Item

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to detach one document from an Agenda Item before the Meeting ends. The document stays in the Meeting; removing it from the Meeting is UC-36c. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can currently view the Meeting and is its current Chair or Secretary. (BR-070, BR-071)<br>● PRE-2: The document is attached to the selected Agenda Item of the same Meeting. (BR-078)<br>● PRE-3: The Meeting has not ended. Pending, InProgress and Done Agenda Items are all eligible. (BR-070, BR-071) |  |  |
| Postconditions | ● POST-1: Only the selected link is removed; the document remains in the Meeting and any other Agenda Item links remain unchanged. With no links left, it is a general Meeting document.<br>● POST-2: The document, its comments and its other Agenda Item links remain unchanged. |  |  |
| Normal Flow | Detach Document from Agenda Item<br>1. The actor selects a document attached to an Agenda Item, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, that the Meeting has not ended, that the document is still in the Meeting, that the Item belongs to the Meeting, and that the link still exists. (BR-070, BR-071, BR-078)<br>3. The system removes only that link; the document stays in the Meeting with its other links unchanged, or becomes a general Meeting document when no links remain.<br>4. The system refreshes the Agenda Item documents and displays MSG239 – “Document detached from the Agenda Item successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is no longer the Chair or Secretary<br>1. At Step 2, the actor is no longer the current Chair or Secretary.<br>2. No links are changed. The system displays MSG241 – “You no longer have permission to unlink documents in this Meeting.”<br>3. The use case ends.<br>EX-2 – Document, Agenda Item or link no longer exists<br>1. At Step 2, the link has already been removed.<br>2. No further links are changed and the document remains in the Meeting. The system displays MSG246 – “This document is no longer linked to the selected Agenda Item. Refresh the document list.”<br>3. The use case ends.<br>EX-3 – Meeting no longer accepts changes<br>1. At Step 2, the Meeting has ended, including when it ended after selection. (BR-070, BR-071)<br>2. No links are changed. The system displays MSG247 – “Document links cannot be changed after the Meeting has ended.”<br>3. The use case ends. |  |  |

#### 7.3 UC-37 – View Personal Document Library

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to browse their own folders and documents and see permitted Meeting-usage details. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |  |  |
| Postconditions | ● POST-1: The user sees only their own library items and permitted Meeting-usage details.<br>● POST-2: Documents, folders and Meeting attachments remain unchanged. |  |  |
| Normal Flow | View Personal Document Library<br>1. The user opens Personal Document Library.<br>2. The system checks personal-library access. (BR-055, BR-083)<br>3. The system displays the user's folders and documents with names, file types and permitted Meeting-usage details. Other users' items and unauthorized Meeting details are excluded. (BR-083)<br>4. The user selects a folder or the library root.<br>5. The system displays the documents at that location. The user may browse the list or empty result, change the location or finish viewing. |  |  |
| Alternative Flows | AF-1 – Search or filter documents<br>1. At Step 5, the user enters a document-name search or selects a file-type filter.<br>2. The system applies the criteria within the user's own displayed library location, matching document and folder names without regard to case or Vietnamese marks.<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Empty library or no matching documents<br>1. At Step 3 or Step 5, no documents match the location and criteria.<br>2. The system displays an empty result without treating it as a loading failure.<br>3. The flow resumes at Step 5 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Selected folder is no longer available<br>1. At Step 4, the folder was deleted or is no longer available in the user's library.<br>2. No information changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The user returns to the library root. The flow resumes at Step 4 of the Normal Flow. |  |  |

##### 7.3.1 UC-37a – View a Deactivated Account’s Personal Library

| Primary Actors | System Admin | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a System Admin to read, without changing anything, the folders and documents in the personal library of a deactivated account, for example to recover Meeting materials after the owner has left the organization. Every read is recorded in the audit log. The Admin does not see the files of Secret Meetings. (BR-083, BR-085, BR-011) |  |  |
| Preconditions | ● PRE-1: The System Admin is signed in and has the capability to manage users.<br>● PRE-2: The selected account exists and is deactivated. The library of an active account is private to its owner. (BR-083, BR-085) |  |  |
| Postconditions | ● POST-1: The System Admin has seen the folders and documents of the deactivated account’s library that BR-085 allows. Nothing in the library has changed. (BR-085)<br>● POST-2: Each read has an audit entry stating who read, whose library, which folder or document and when. (BR-011) |  |  |
| Normal Flow | View a Deactivated Account’s Personal Library<br>1. The System Admin opens the user list and chooses to view the personal library of a deactivated account. The library of an active account cannot be opened. (BR-085)<br>2. The system rechecks the System Admin’s capability and that the account is still deactivated, and that the selected folder, if any, is in that library and is not hidden by BR-085. (BR-085)<br>3. The system records the read in the audit log before showing anything. (BR-011)<br>4. The system displays the folders and documents of the account’s library at the root, read-only. The files of Secret Meetings and each such Meeting’s upload folder with its subfolders are left out. (BR-085)<br>5. The System Admin opens a folder. The system repeats Steps 2 and 3 for that read and displays the folder’s contents. |  |  |
| Alternative Flows | AF-1 – Read a document<br>1. At Step 4 or 5, the System Admin chooses a document to preview or download.<br>2. The system repeats the checks of Step 2, opens the file, records an audit entry naming the document and only then returns the file read-only. (BR-085, BR-011)<br>3. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Account is missing or is not deactivated<br>1. At Step 2, the account is no longer deactivated (or was reactivated while viewing), or the selected folder or document does not exist in that library or is hidden by BR-085.<br>2. Nothing is shown. The system displays MSG175 – “This personal-library item is not available.” and navigates back to the user list if the account was reactivated, or returns to the library root if a folder was deleted or hidden.<br>3. The use case ends. |  |  |

#### 7.4 UC-38 – Comment on Meeting Document

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user who can view a Meeting document to read its comments and add a comment before the Meeting ends. After the Meeting ends, the document and comments are read-only. (BR-082, BR-079, BR-080) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a current valid invitation. (BR-053, BR-054, BR-044)<br>● PRE-2: The document is currently in the Meeting and the user can view that Meeting. (BR-079, BR-080)<br>● PRE-3: The Meeting has not ended. (BR-079, BR-080) |  |  |
| Postconditions | ● POST-1: The new comment is displayed with its author and posting time, and the comment count is updated.<br>● POST-2: Existing comments and the document remain unchanged. |  |  |
| Normal Flow | Comment on Meeting Document<br>1. The user opens a Meeting document and its comments.<br>2. The system checks viewing access and displays the existing comments. (BR-079, BR-080)<br>3. The user enters a comment and chooses Send.<br>4. The system checks that the user may still view the document, the Meeting has not ended, and the comment is not blank. (BR-082, BR-079, BR-080)<br>5. The system records the comment with its author and posting time; a Guest author is recorded by their invitation, shown with the name they entered. A PDF comment may refer to a page; a DOCX comment has no page reference.<br>6. The system displays the new comment, updates the comment count and displays MSG199 – “Comment added successfully.” |  |  |
| Alternative Flows | AF-1 – Include a PDF page reference<br>1. At Step 3, the user comments on a page being viewed in a PDF.<br>2. The comment includes that page reference; a page reference is not required for a document-wide comment.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Reply to a comment<br>1. At Step 3, the user chooses Reply on an existing comment and enters the reply.<br>2. The reply is attached to the top-level comment of that thread in the same Meeting document; replying to a reply joins the same thread.<br>3. The flow resumes at Step 4 of the Normal Flow; the reply is shown under its thread. |  |  |
| Exception Flows | EX-1 – Comment is empty or too long<br>1. At Step 4, the text is blank or contains only whitespace.<br>2. No comment is saved. The system displays MSG200 – “Enter a comment.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Document is no longer available<br>1. At Step 2 or Step 4, the document is no longer attached, or the user can no longer view it, including after Guest invitation revocation or expiry.<br>2. No comment is saved. The system displays MSG156 – “Meeting documents are not accessible.”<br>3. The use case ends.<br>EX-3 – Meeting no longer accepts comments<br>1. At Step 2 or Step 4, the Meeting has ended, including when it ended while the user was writing. (BR-079, BR-080) A Guest loses all access to the Meeting when it ends, so EX-2 applies to a Guest instead. (BR-055)<br>2. No comment is added. Existing comments remain available for reading. The system displays MSG251 – “Comments are read-only after the Meeting has ended.”<br>3. The use case ends. |  |  |

#### 7.5 UC-39 – Upload Document to Personal Library

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to upload one or more PDF or DOCX files to the selected location in their personal library. Uploading to the library does not add them to a Meeting. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |  |  |
| Postconditions | ● POST-1: Each uploaded file is available as a separate document at the selected library location. (BR-068)<br>● POST-2: A file that cannot be added is not added at all, and does not stop the other files. Existing library documents and Meeting documents remain unchanged. |  |  |
| Normal Flow | Upload Documents to Personal Library<br>1. The user opens their library root or a folder and chooses Upload.<br>2. The user selects one or more files from the computer: at most 10 files totalling at most 100 MB at once, within the space left in the system's document storage.<br>3. As the files are chosen, the system checks that each one is a PDF or DOCX file, not empty and no larger than 10 MB. (BR-069)<br>4. The user confirms the upload. Each valid file is uploaded on its own.<br>5. For each file, the system checks library access, the selected destination and that the file is a valid PDF or DOCX whose content matches its extension; a PDF must open without a password and contain at least one page. (BR-069, BR-083)<br>6. The system checks that the file fits within the 250 GB storage limit of the whole system, then adds it as a separate document at that location with its original name and content. (BR-068)<br>7. The system shows the number added and displays MSG177 – “Documents uploaded to your personal library successfully.”. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Some files are invalid<br>1. At Step 3, one or more files are not PDF or DOCX, are empty or are larger than 10 MB; or, at Step 5, the content of a file is not a valid PDF or DOCX. (BR-069)<br>2. The invalid files are not added and are named; the valid files are still uploaded. The system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.”.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – Too many files or too large a selection<br>1. At Step 2, more than 10 files, or more than 100 MB in total, are chosen at once.<br>2. The whole selection is blocked and no file is uploaded. The system displays MSG288 – “Upload up to 10 files at a time, no larger than 100 MB in total.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Destination folder is no longer available<br>1. At Step 5, the selected folder was deleted or does not belong to the user. (BR-083)<br>2. No file is added to that folder. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-4 – System storage is full<br>1. At Step 2, the chosen files do not fit in the space left in the system's document storage; or, at Step 6, adding a file would take the total storage of all documents in the system above 250 GB.<br>2. At Step 2 the whole selection is blocked; at Step 6 that file is not added. Existing library and Meeting documents remain unchanged. The system displays MSG346 – “The system’s document storage is full. No documents were uploaded. Please contact an administrator.”<br>3. The use case ends. |  |  |

#### 7.6 UC-40 – Manage Personal Documents

##### 7.6.1 UC-40a – Delete Document from Personal Library

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to delete a document from their personal library without removing documents already attached to Meetings. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The selected document is in the user's library. |  |  |
| Postconditions | ● POST-1: The document no longer appears in the personal library.<br>● POST-2: Documents already added to Meetings remain available to their authorized readers. Their history and comments are preserved. (BR-074, BR-075, BR-076, BR-077) |  |  |
| Normal Flow | Delete Document from Personal Library<br>1. The user selects a personal-library document and chooses Delete.<br>2. The system requests confirmation, naming the document and explaining that deletion affects the personal library only.<br>3. The user confirms deletion.<br>4. The system rechecks ownership and that the library document still exists. (BR-083)<br>5. The system deletes the personal-library document without removing Meeting attachments. (BR-074, BR-075, BR-076)<br>6. The system refreshes the library and displays MSG180 – “Document deleted from your personal library successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No library document is deleted; Meeting documents remain unchanged. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends. |  |  |

##### 7.6.2 UC-40b – Rename Document

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to rename a personal-library document without changing its file content. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The document exists in the user's library. |  |  |
| Postconditions | ● POST-1: The document is displayed under the new name in the personal library.<br>● POST-2: File content and library location remain unchanged. Names already copied into Meetings remain unchanged. (BR-068) |  |  |
| Normal Flow | Rename Document<br>1. The user chooses Rename for a personal-library document.<br>2. The system displays its current name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership and document availability. (BR-083)<br>5. The system saves the new personal-library document name. File content and names already copied into Meetings remain unchanged. (BR-068)<br>6. The system refreshes the displayed name and displays MSG183 – “Document renamed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. No document name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Document name is invalid<br>1. At Step 4, the entered name is empty, contains only whitespace, contains only an extension, contains control characters, exceeds 255 characters, or changes the original file extension (.pdf/.docx).<br>2. No document name changes. The system displays MSG347 – “The document name is not valid. It must not be blank and must keep the .pdf or .docx extension of the file.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

##### 7.6.3 UC-40c – Move Document to Folder

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to move a personal-library document to another owned folder or the library root. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The document exists in the user's library. |  |  |
| Postconditions | ● POST-1: The document is available at the selected library location.<br>● POST-2: Document name, file content and Meeting attachments remain unchanged. |  |  |
| Normal Flow | Move Document to Folder<br>1. The user chooses Move for a personal-library document.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system rechecks document ownership and availability and destination ownership and availability. (BR-083)<br>5. The system moves only the library document to that location.<br>6. The system refreshes the library and displays MSG185 – “Document moved successfully.” |  |  |
| Alternative Flows | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of a folder.<br>2. The document is moved outside all folders.<br>3. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Document is missing or no longer belongs to the actor<br>1. At Step 4, the selected document is missing or no longer belongs to the user.<br>2. The document remains at its previous location. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Destination is missing or does not belong to the actor<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-083)<br>2. The document stays at its previous location. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

#### 7.7 UC-41 – Manage Library Folders

##### 7.7.1 UC-41a – Create Folder

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to create a folder at the library root or inside an owned folder. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083) |  |  |
| Postconditions | ● POST-1: A new owned folder is available at the selected location.<br>● POST-2: Existing folders, documents and Meeting attachments remain unchanged. |  |  |
| Normal Flow | Create Folder<br>1. The user selects the library root or an owned parent folder and chooses New Folder.<br>2. The user enters a folder name and submits it.<br>3. The system checks ownership, parent availability, a nonblank name and a resulting depth of at most five levels. Duplicate folder names are permitted. (BR-083)<br>4. The system creates the folder at that location.<br>5. The system refreshes the folder list and displays MSG187 – “Folder created successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder name is blank or too long<br>1. At Step 3, the name is empty or contains only whitespace.<br>2. No folder is created. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Parent folder is missing or not owned by the user<br>1. At Step 3, the selected parent was deleted or does not belong to the user. (BR-083)<br>2. No folder is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-3 – New folder would be deeper than level five<br>1. At Step 3, the new folder would be deeper than level five.<br>2. No folder is created. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The user selects a shallower parent. The flow resumes at Step 1 of the Normal Flow. |  |  |

##### 7.7.2 UC-41b – Rename Folder

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to rename a folder without moving it or changing its contents. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists in the user's library. |  |  |
| Postconditions | ● POST-1: The folder displays the new name.<br>● POST-2: Its location and contents remain unchanged; an automatic Meeting folder remains reusable. |  |  |
| Normal Flow | Rename Folder<br>1. The user chooses Rename for an owned folder.<br>2. The system displays the current folder name.<br>3. The user enters a new name and submits it.<br>4. The system rechecks ownership, availability and a nonblank name; duplicate names are permitted. (BR-083)<br>5. The system saves the new folder name without changing its contents or location.<br>6. The system refreshes the folder name and displays MSG191 – “Folder renamed successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder is missing or not owned by the user<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder name changes. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder name is blank<br>1. At Step 4, the name is empty or contains only whitespace.<br>2. The previous name remains. The system displays MSG188 – “Enter a folder name.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Folder name exceeds 400 characters<br>1. At Step 4, the entered folder name exceeds 400 characters.<br>2. The previous name remains. The system displays MSG348 – “The text is longer than allowed.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

##### 7.7.3 UC-41c – Move Folder

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to move a folder with its contents to another owned folder or the library root. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists in the user's library. |  |  |
| Postconditions | ● POST-1: The folder and its contents are available at the selected location, within five levels.<br>● POST-2: Folder and document names, file content and Meeting attachments remain unchanged. An automatic Meeting folder remains reusable. |  |  |
| Normal Flow | Move Folder<br>1. The user chooses Move for an owned folder.<br>2. The system displays destinations within the user's library.<br>3. The user selects an owned destination folder and confirms.<br>4. The system checks that the user owns the selected folder and destination and that both are available. The destination cannot be the selected folder or one of its subfolders, and the moved folder and its subfolders must remain within five levels. (BR-083, BR-084)<br>5. The system moves the folder with its documents and subfolders, without changing their names or Meeting attachments.<br>6. The system refreshes the folder tree and displays MSG194 – “Folder moved successfully.” |  |  |
| Alternative Flows | AF-1 – Move to the library root<br>1. At Step 3, the user selects the library root instead of another folder.<br>2. The moved folder becomes a first-level folder; its subfolders remain subject to the depth limit.<br>3. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Folder or destination is missing or not owned by the user<br>1. At Step 4, the destination was deleted or does not belong to the user. (BR-083)<br>2. No folder or contained item moves. The system displays MSG179 – “The selected destination folder is not available in your personal library.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Destination is the folder itself or one of its subfolders<br>1. At Step 4, the destination is the folder itself or one of its subfolders. (BR-084)<br>2. No folder or contained item moves. The system displays MSG193 – “A folder cannot be moved into itself or one of its descendants.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Moved folders would be deeper than level five<br>1. At Step 4, the moved folder or one of its subfolders would be deeper than level five.<br>2. No folder or contained item moves. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

##### 7.7.4 UC-41d – Delete Folder

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the owner to delete an empty personal-library folder. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in.<br>● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-055, BR-083)<br>● PRE-3: The folder exists and contains no documents or subfolders. (BR-084) |  |  |
| Postconditions | ● POST-1: The empty folder no longer appears in the library.<br>● POST-2: Other folders, documents and Meeting attachments remain unchanged. |  |  |
| Normal Flow | Delete Folder<br>1. The user chooses Delete for an owned folder.<br>2. The system asks for confirmation and names the folder.<br>3. The user confirms deletion.<br>4. The system rechecks current ownership and availability and that the folder contains neither documents nor subfolders. (BR-083, BR-084)<br>5. The system deletes only the empty folder.<br>6. The system refreshes the folder tree and displays MSG197 – “Folder deleted successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Folder is missing or not owned by the user<br>1. At Step 4, the selected folder is missing or no longer belongs to the user.<br>2. No folder is deleted. The system displays MSG175 – “This personal-library item is not available.”<br>3. The use case ends.<br>EX-2 – Folder contains a document or subfolder<br>1. At Step 4, the folder contains a document or subfolder, including an item added after confirmation was displayed. (BR-084)<br>2. The folder and its contents remain unchanged. The system displays MSG196 – “Only an empty folder can be deleted.”<br>3. The use case ends. |  |  |

#### 7.8 UC-42 – Confirm Document Read

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who may vote to confirm that he or she has read a document linked to a Vote of the Meeting, after reaching its last page. The confirmation is required before casting a ballot in UC-65. It records that the person confirmed reading, not how carefully the document was read. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor can currently view the Meeting, the document is attached to the Meeting and linked to a Vote of that Meeting, and the actor is on the eligible-voter list of an Open Vote linked to that document. (BR-055, BR-079, BR-080, BR-091)<br>● PRE-3: The actor has opened the document in the viewer through UC-35b. |  |  |
| Postconditions | ● POST-1: A read confirmation for the actor and the document is recorded with the confirmation time.<br>● POST-2: The confirmation counts for every Vote of the Meeting that links the same document and cannot be withdrawn.<br>● POST-3: No ballot, Vote or document content changes, and the confirmation is not linked to any ballot choice. (BR-096, BR-097) |  |  |
| Normal Flow | Confirm Document Read<br>1. From the Vote documents or the Meeting documents, the actor opens a Vote document through UC-35b.<br>2. The actor reads the document through to its last page.<br>3. Once the last page is reached, the system lets the actor confirm reading.<br>4. The actor confirms reading.<br>5. The system rechecks current Meeting access, that the document is still attached to the Meeting and linked to a Vote, and that the actor reached the last page. (BR-055, BR-079, BR-080)<br>6. The system records the read confirmation with the actor, the document and the confirmation time. (BR-096, BR-097)<br>7. The system displays MSG253 – "Document read confirmed." and shows the document as read. When every document of an Open Vote is confirmed, the actor may cast a ballot in UC-65. (BR-094, BR-095) |  |  |
| Alternative Flows | AF-1 – Document already confirmed<br>1. At Step 1, the actor has already confirmed reading this document, including for another Vote of the Meeting that links it.<br>2. The system shows the document as read with the time of the existing confirmation, and does not offer confirmation again.<br>3. The use case ends without a new confirmation. |  |  |
| Exception Flows | EX-1 – Last page not reached<br>1. At Step 4 or Step 5, the actor has not reached the last page, including when the viewer was closed before the last page.<br>2. No confirmation is recorded. The system displays MSG254 – "Scroll to the last page of the document before confirming that you have read it."<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Document or Meeting is no longer accessible<br>1. At Step 5, the actor can no longer view the Meeting, or the document is no longer attached to the Meeting or linked to a Vote.<br>2. No confirmation is recorded. The system displays MSG156 – "Meeting documents are not accessible."<br>3. The use case ends.<br>EX-3 – Not an eligible voter, or no Open Vote remains<br>1. At Step 5, the actor is not on the fixed eligible-voter list of any Open Vote linked to this document. Or no Vote linked to this document is Open any more.<br>2. No confirmation is recorded. The system displays MSG224 – "You are not eligible to cast a ballot in this vote." When no Open Vote remains, the system displays MSG225 – “This vote is no longer accepting ballots.”<br>3. The use case ends. |  |  |

### 8. FE-08 – Online Meeting Room Management

#### 8.1 UC-43 – Join Meeting Room

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who currently holds access to an In Progress Online or Hybrid Meeting to check the camera and microphone in the lobby and enter its online meeting room. Joining does not start or change the Meeting. A person who opens the room before the Meeting starts waits in the lobby until it starts. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation. (BR-052, BR-053, BR-054, BR-044, BR-055)<br>● PRE-2: The Meeting is In Progress and its mode is Online or Hybrid. (BR-025, BR-055)<br>● PRE-3: The actor holds a current participant assignment of the Meeting, or a valid Guest invitation for it. Being the Member who created the Meeting or its Approver alone is not enough. (BR-044, BR-100)<br>● PRE-4: Fewer than 50 people are connected to the room. |  |  |
| Postconditions | ● POST-1: The actor is in the meeting room and appears in the Participants panel of everyone in the room. (BR-101)<br>● POST-2: The Meeting lifecycle, its roster, its roles and its attendance are unchanged; presence in the room is never recorded as attendance. (BR-101)<br>● POST-3: The actor stays in the room until leaving (UC-44) or until the Meeting ends.<br>● POST-4: A Guest who joins from two devices is in the room twice, and revoking the invitation removes both. The Guest's confirmation of the recording notice is recorded in the audit log. |  |  |
| Normal Flow | Join Meeting Room<br>1. On the Meeting detail of an In Progress Online or Hybrid Meeting, the actor chooses to join the online room.<br>2. The system checks that the actor may still view the Meeting, that it is In Progress, that it has an online room and that the actor may enter the room. (BR-044, BR-025, BR-100)<br>3. The system opens the Meeting Lobby with a camera preview; the microphone is on and the camera is off by default. When recording applies to the Meeting, the lobby tells the actor that the meeting is recorded for the minutes and that the actor's own speech is recorded while the microphone is on. (UC-45a, UC-53)<br>4. The actor checks the preview and may choose other devices (UC-45d) or turn the microphone or camera on or off (UC-45a, UC-45b).<br>5. The actor chooses to join the room. A Guest first confirms the recording notice, and the system records that confirmation in the audit log.<br>6. The system checks again that the actor may enter the room and lets the actor in with the actor's current Meeting Role. (BR-100)<br>7. The system shows the online meeting room with the people already in it; no separate success message is displayed. (UC-46) |  |  |
| Alternative Flows | AF-1 – Rejoin after a lost connection<br>1. At Step 1, the actor returns to the room after the connection was lost while the Meeting is still In Progress.<br>2. The system offers to rejoin; no new room and no new Meeting are created. (BR-101)<br>3. The flow resumes at Step 6 of the Normal Flow.<br>AF-2 – Join from a Guest invitation<br>1. At Step 1, the Guest continues from the verified invitation instead of the Meeting detail.<br>2. The system checks the invitation and that the Meeting is In Progress. (BR-052, BR-053, BR-054, BR-044, BR-055)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Opened before the Meeting starts<br>1. At Step 2, the Meeting is Published and has not been started.<br>2. The system shows the waiting lobby with MSG117 – “The meeting room opens only while the meeting is in progress.” and does not let the actor join yet. (BR-025)<br>3. When the Chair or the Secretary starts the Meeting (UC-20), the lobby opens and the flow resumes at Step 3 of the Normal Flow.<br>AF-4 – The browser blocks audio playback<br>1. At Step 7, the browser does not play sound until the actor interacts with the page.<br>2. The system displays MSG338 – “The browser is blocking audio playback.” and asks the actor to allow sound.<br>3. The actor allows sound and the flow resumes at Step 7 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Meeting has ended or was cancelled<br>1. At Step 2 or Step 6, the Meeting has ended, including while the actor was in the lobby, or it was cancelled. (BR-028)<br>2. The actor does not enter the room. When the Meeting has ended, the system displays MSG331 – “The meeting has ended, the meeting room is closed.” and offers a way back to the Meeting page (BR-025); a Guest sees the end screen of UC-04 instead. When the Meeting was cancelled, the system displays MSG350 – “The meeting was cancelled, so the meeting room does not open.”<br>3. The use case ends.<br>EX-2 – Meeting is held in person<br>1. At Step 2, the Meeting mode is In Person, so it has no online room.<br>2. No lobby is opened. The system displays MSG280 – “This meeting is held in person, so it has no online meeting room.”<br>3. The use case ends.<br>EX-3 – The Guest session ended while waiting or joining<br>1. At Step 2 or Step 5, the Guest session expired.<br>2. The Guest does not enter the room, the Meeting data is removed from the screen and the Guest returns to the verification screen with MSG378 – “The guest session has ended.” The invitation is still valid, so the Guest may verify again; when the invitation was revoked, UC-04 applies instead.<br>3. The use case ends.<br>EX-4 – Recording notice not confirmed<br>1. At Step 5, the Guest has not confirmed the recording notice.<br>2. The Guest does not enter the room. The system displays MSG380 – “Confirm that you have read the recording notice before joining the meeting room.”<br>3. The flow resumes at Step 5 of the Normal Flow. |  |  |

#### 8.2 UC-44 – Leave Meeting Room

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person connected to the online meeting room to disconnect. Leaving never changes the Meeting lifecycle: the Meeting stays In Progress for everyone else, and the person may join again while it is In Progress. Only the Chair ends the Meeting, through UC-21. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of the Meeting. (UC-43)<br>● PRE-3: The Meeting is In Progress. |  |  |
| Postconditions | ● POST-1: The actor is disconnected and no longer appears in the Participants panel. (BR-101)<br>● POST-2: The Meeting stays In Progress with its roster, roles, attendance and recorded ballots unchanged. (BR-101)<br>● POST-3: The actor may join again through UC-43 while the Meeting is In Progress. |  |  |
| Normal Flow | Leave Meeting Room<br>1. The actor chooses to leave the room.<br>2. The system asks the actor to confirm, reminding that the meeting continues with the others and that the actor can rejoin while it is in progress.<br>3. The actor confirms.<br>4. The system disconnects the actor, stops the camera and microphone and shows the Meeting detail.<br>5. The Meeting stays In Progress for everyone still connected; no separate success message is displayed. (BR-101) |  |  |
| Alternative Flows | AF-1 – Stay in the room<br>1. At Step 2, the actor chooses to stay.<br>2. No connection is closed and the flow resumes at Step 1 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Meeting has ended while in the room<br>1. At Step 1, the Chair ended the Meeting (UC-21) and the room closed.<br>2. The actor is disconnected and cannot rejoin. The system displays MSG331 – “The meeting has ended, the meeting room is closed.” A Guest sees the end screen of UC-04 instead.<br>3. The use case ends.<br>EX-2 – Same account joined elsewhere<br>1. At Step 1, the actor joins the room from another tab or device.<br>2. The earlier connection is closed and displays MSG329 – “You joined this room from another tab or device, so this tab left the room.” It may rejoin, which closes the newer connection in turn.<br>3. The use case ends.<br>EX-3 – Actor is no longer on the participant list<br>1. At Step 1, the actor was removed from the roster or the Guest invitation was revoked while connected.<br>2. The actor is disconnected and cannot rejoin. The system displays MSG330 – “You are no longer on the participant list of this meeting room.” A Guest whose invitation was revoked sees the end screen of UC-04 instead.<br>3. The use case ends. |  |  |

#### 8.3 UC-45 – Manage Meeting Room Controls

##### 8.3.1 UC-45a – Manage Microphone

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person in the Meeting Lobby or in the meeting room to turn his or her own microphone on or off. It changes only that person's microphone: turning another person's microphone off is UC-47. While recording applies to the Meeting, the actor's own speech is recorded while the microphone is on (AF-2). For the Secretary of a Hybrid Meeting the microphone is the room microphone, so turning it on or off turns the room recording on or off (UC-52a). |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser reports at least one microphone. |  |  |
| Postconditions | ● POST-1: The actor's microphone is on or off as chosen, for that actor only.<br>● POST-2: The on/off state is remembered for the next visit on that device.<br>● POST-3: No other person's audio, the Meeting lifecycle and attendance are unchanged.<br>● POST-4: While recording applies to the Meeting, a microphone that is on means the actor’s own speech is recorded for the minutes; turning it off stops it. (AF-2, UC-53, BR-113) |  |  |
| Normal Flow | Manage Microphone<br>1. The actor turns his or her microphone on or off in the lobby or in the room.<br>2. The system turns the microphone on or off and shows the new state to the actor and to everyone in the room; no separate message is displayed. (UC-46)<br>3. The system remembers the new state for the next visit on this device. |  |  |
| Alternative Flows | AF-1 – Choose another microphone<br>1. At Step 1, the actor chooses another microphone through UC-45d.<br>2. The system switches to that microphone, keeps the on/off state and remembers the device.<br>3. The flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Speech recorded while the microphone is on<br>1. At Step 2, the actor turns the microphone on in an Online or Hybrid Meeting that is In Progress. This applies to a Guest with a valid invitation as well, once the Guest has confirmed the recording notice. It does not apply to the Secretary of a Hybrid Meeting, whose microphone records the whole room (UC-52a). (BR-107, BR-113, BR-111, BR-112)<br>2. While the microphone is on, the system records the actor’s speech sentence by sentence as text, with its time and the actor’s name. (BR-113)<br>3. A sentence that repeats a neighbouring person’s speech picked up by the actor’s microphone is marked as overlapping, not hidden.<br>4. The actor can review, play and delete his or her saved sentences in My Recorded Speech (UC-53); they are kept for the retention period. (BR-110, BR-113)<br>5. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Browser blocks the microphone<br>1. At Step 2, the browser has not granted microphone permission.<br>2. The microphone stays off and the actor stays in the room and can still hear everyone. The system displays MSG334 – “The browser is blocking the {device}. Click the lock icon beside the address bar, choose Allow and reload the page. You can still join the room to listen.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – No microphone is available<br>1. At Step 1, the browser reports no microphone, or the chosen microphone was disconnected.<br>2. The microphone cannot be chosen and the actor can still join the room to listen. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The use case ends.<br>EX-3 – Meeting cannot be recorded now<br>1. At Step 2 of AF-2, the Meeting is held in person, has not started, or ended more than 7 days ago. (BR-107)<br>2. The sentence is not kept. The system displays MSG320 – “In-person meetings do not record per-mic speech, or the meeting is no longer running.”<br>3. The use case ends. |  |  |

##### 8.3.2 UC-45b – Manage Camera

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person in the Meeting Lobby or in the meeting room to turn his or her own camera on or off. It changes only that person's camera. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser reports at least one camera. |  |  |
| Postconditions | ● POST-1: The actor's camera is on or off as chosen, for that actor only; the camera is off when the actor first joins.<br>● POST-2: The on/off state is remembered for the next visit on that device.<br>● POST-3: No other person's video, the Meeting lifecycle and attendance are unchanged. |  |  |
| Normal Flow | Manage Camera<br>1. The actor turns his or her camera on or off in the lobby or in the room.<br>2. The system turns the camera on or off; while it is off, everyone in the room sees the actor's initials instead of a video image. No separate message is displayed. (UC-46)<br>3. The system remembers the new state for the next visit on this device. |  |  |
| Alternative Flows | AF-1 – Choose another camera<br>1. At Step 1, the actor chooses another camera through UC-45d.<br>2. The system switches to that camera, keeps the on/off state and remembers the device.<br>3. The flow resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – The browser blocks the camera<br>1. At Step 2, the browser has not granted camera permission.<br>2. The camera stays off and the actor stays in the room with audio. The system displays MSG334 – “The browser is blocking the camera. Click the lock icon beside the address bar, choose Allow and reload the page.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – No camera is available<br>1. At Step 1, the browser reports no camera, or the chosen camera was disconnected.<br>2. The camera cannot be chosen and the actor can still join the room to listen. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The use case ends. |  |  |

##### 8.3.3 UC-45c – Manage Screen Sharing

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person connected to the room to present a screen, window or tab to everyone and to stop presenting. One person presents at a time: when someone else starts, the previous presentation stops. Nothing is recorded; the shared content exists only while the share lasts. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The device can share a screen: the browser offers screen capture and the device is not touch-only. |  |  |
| Postconditions | ● POST-1: The actor's screen is shown on the main stage of everyone in the room, or the presentation has stopped.<br>● POST-2: At most one person is presenting at any time.<br>● POST-3: Nothing is recorded or stored; the Meeting and its attendance are unchanged. |  |  |
| Normal Flow | Manage Screen Sharing<br>1. The actor chooses to share the screen.<br>2. The actor picks a screen, window or tab and confirms.<br>3. The system shows the shared content on the main stage for everyone in the room and tells the actor that he or she is presenting to everyone.<br>4. The actor stops sharing.<br>5. The system ends the presentation and returns everyone to the video grid; no separate message is displayed. |  |  |
| Alternative Flows | AF-1 – Someone else starts presenting<br>1. At Step 3, while the actor is presenting, another person starts a share.<br>2. The system stops the actor's presentation and keeps the new one.<br>3. The system displays MSG340 – “{name} started presenting, so your screen share was stopped.” and the flow resumes at Step 3 of the Normal Flow.<br>AF-2 – The device cannot share a screen<br>1. At Step 1, the device cannot share a screen.<br>2. The system does not offer screen sharing; watching another person present still works.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Sharing cannot start<br>1. At Step 2, the share cannot start.<br>2. Nothing is shared and the stage stays as it was. The system displays MSG341 – “Unable to share your screen. Please try again.”<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

##### 8.3.4 UC-45d – Choose Audio and Video Devices

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person in the Meeting Lobby or in the meeting room to choose which microphone, speaker and camera the browser uses. The choice applies to that person's browser on that device only: it changes no Meeting setting and is not stored with the Meeting. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is in the Meeting Lobby or connected to the meeting room. (UC-43)<br>● PRE-3: The browser lists at least one device of the kind being chosen, or reports why it cannot. |  |  |
| Postconditions | ● POST-1: The chosen device is used for the actor's microphone, speaker or camera from then on.<br>● POST-2: The browser remembers the choice for the next visit on that device.<br>● POST-3: No other person's devices, the Meeting and its attendance are unchanged. |  |  |
| Normal Flow | Choose Audio and Video Devices<br>1. The actor chooses to change the microphone, speaker or camera in the lobby or in the room.<br>2. The system lists the devices of that kind found on the computer and marks the system default.<br>3. The actor chooses a device.<br>4. The system switches the microphone, speaker or camera to that device and keeps the current on/off state.<br>5. The system shows the chosen device and remembers it for the next visit. |  |  |
| Alternative Flows | AF-1 – The speaker cannot be chosen<br>1. At Step 1, the browser cannot send sound to a chosen speaker.<br>2. The speaker is not offered and sound keeps playing through the system output.<br>3. The use case ends.<br>AF-2 – No device is listed<br>1. At Step 2, the browser has not granted permission or reports no device of that kind.<br>2. No device can be chosen, and the actor can still join the room to listen.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – The chosen device is not found<br>1. At Step 4, the chosen device is no longer connected to the computer.<br>2. The previous device stays in use. The system displays MSG335 – “No {device} was found on this computer.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – The device cannot be opened<br>1. At Step 4, the browser cannot open the chosen device for another reason.<br>2. The previous device stays in use and the actor can still join the room to listen. The system displays MSG337 – “Unable to open the {device}. Check the permission at the lock icon beside the address bar. You can still join the room to listen.”<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

#### 8.4 UC-46 – View Meeting Room Participants

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person in the room to see who is connected at that moment, with each person's Meeting Role, who is speaking and whose microphone is off. It shows the room, not the roster: someone who has not joined, or has left, is not listed. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43) |  |  |
| Postconditions | ● POST-1: The panel lists everyone connected at that moment, ordered by Meeting Role and then by name.<br>● POST-2: The list follows people joining, leaving, speaking, muting (UC-47) and changing role (UC-34) while it is open. (BR-101, BR-102)<br>● POST-3: Presence in the list is not attendance and changes no stored data. (BR-101) |  |  |
| Normal Flow | View Meeting Room Participants<br>1. The actor opens the list of people in the room.<br>2. The system shows the number of people currently connected.<br>3. The system lists each connected person with name, Meeting Role, whether the person is speaking and the microphone state; the actor's own row is marked as the actor. (BR-101)<br>4. The system orders the rows by Meeting Role (Chair, Secretary, Member, Guest) and then by name.<br>5. The actor closes the list; no separate message is displayed. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – The connection is lost<br>1. At Step 3, the connection to the room is lost.<br>2. The list stops updating and the room shows MSG333 – “Connection lost, reconnecting…”; if the connection cannot be restored, the room reports it through UC-44.<br>3. The use case ends. |  |  |

#### 8.5 UC-47 – Moderate Meeting Room

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to mute another person's microphone in the room. Muting is one-way: the Chair cannot unmute that person, who may turn the microphone on again. Raising a hand and giving the floor to a speaker is UC-50; muting everyone and removing a person from the room are not provided. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-102)<br>● PRE-2: The Meeting is In Progress and the actor is connected to its room. (UC-43)<br>● PRE-3: The target is a person currently connected to the room whose microphone is on. (BR-102) |  |  |
| Postconditions | ● POST-1: The target's microphone is off and everyone in the room sees it as off. (BR-102)<br>● POST-2: The target stays in the room and may turn the microphone on again. (BR-102)<br>● POST-3: The target's camera, roles, attendance and the Meeting lifecycle are unchanged. |  |  |
| Normal Flow | Mute a Participant<br>1. The Chair opens the list of people in the room and finds a person whose microphone is on. (UC-46)<br>2. The Chair chooses to mute that person.<br>3. The system checks that the actor is still the current Chair and that the target is still in the room with the microphone on. (BR-102)<br>4. The system mutes the target's microphone for everyone in the room. (BR-102)<br>5. The system displays MSG342 – “Muted the microphone of {name}.” to the Chair, and MSG339 – “The Chair has muted your microphone. You can turn it back on when you need to speak.” to the target. |  |  |
| Alternative Flows | AF-1 – The target turns the microphone on again<br>1. After Step 4, the target turns the microphone on through UC-45a.<br>2. The microphone is on again and everyone sees it; the Chair may mute again. (BR-102)<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is no longer the chair<br>1. At Step 3, the Chair role was handed over through UC-34, so the actor is no longer the Chair. (BR-102)<br>2. No microphone is muted. The system displays MSG123 – “Only the Chair can mute another person's microphone.”<br>3. The use case ends.<br>EX-2 – Meeting is no longer in progress<br>1. At Step 3, the Meeting ended.<br>2. No microphone is muted. The system displays MSG117 – “The meeting room opens only while the meeting is in progress.”<br>3. The use case ends.<br>EX-3 – Target is no longer in the room or has no microphone on<br>1. At Step 3, the target left, was disconnected or turned the microphone off.<br>2. No microphone is muted. The system displays MSG282 – “This person is no longer in the room or has no microphone on.”<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

#### 8.6 UC-48 – Use In-room Chat

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person in the room to read the messages sent since joining and to send a text message to everyone currently in the room. Messages live only in the room: they are not stored, and a person who joins later does not see earlier messages. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43) |  |  |
| Postconditions | ● POST-1: The message is shown to everyone connected at that moment, with its sender and time.<br>● POST-2: Nothing is stored; the message disappears when the room closes. (BR-103)<br>● POST-3: No Meeting data, attendance or document changes. |  |  |
| Normal Flow | Use In-room Chat<br>1. The actor opens the chat of the room.<br>2. The system shows the messages sent since the actor joined, in time order, and tells the actor that messages are visible only to people in the room and are not kept after the meeting. (BR-103)<br>3. The actor writes a message of at most 1,000 characters.<br>4. The actor sends the message.<br>5. The system checks that the message is not blank and sends it to everyone connected. (BR-103)<br>6. The system shows the message to everyone with the sender and the time; no separate success message is displayed. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – The message cannot be sent<br>1. At Step 5, the message cannot be sent.<br>2. The message is kept so the actor can try again. The system displays MSG343 – “Unable to send the message. Please try again.”<br>3. The flow resumes at Step 4 of the Normal Flow.<br>EX-2 – The connection is lost<br>1. At Step 5, the connection to the room is lost.<br>2. The message is not sent. The room displays MSG333 – “Connection lost, reconnecting…” and, if the connection cannot be restored, reports it through UC-44. (BR-103)<br>3. The use case ends. |  |  |

#### 8.7 UC-49 – Keep Meeting in a Floating Window

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Extends UC-43. Allows a person connected to the room to keep the call in a small always-on-top window while working in other tabs, and to bring it back to the main window. The floating window shows the same room connection; it is not a second join. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: The browser can show the call in a floating window. |  |  |
| Postconditions | ● POST-1: The call is shown in a floating window and the main window says the meeting is in the floating window.<br>● POST-2: The actor stays in the room with the same microphone, camera and connection.<br>● POST-3: The floating window closes when the actor leaves the room or closes it, and no second connection is created. |  |  |
| Normal Flow | Keep Meeting in a Floating Window<br>1. The actor chooses to move the call to a floating window.<br>2. The system opens a small floating window with the current presentation or speaker, the actor's own camera and the call controls, on the same room connection.<br>3. The main window shows that the meeting is in the floating window.<br>4. The actor brings the call back to the main window, or closes the floating window.<br>5. The system returns the call to the main window; the connection is unchanged. No separate message is displayed. |  |  |
| Alternative Flows | AF-1 – The browser opens the floating window on its own<br>1. At Step 1, the actor switches to another tab and the browser offers to keep the call in a floating window.<br>2. The system opens the floating window and the flow resumes at Step 3 of the Normal Flow.<br>AF-2 – Leave from the floating window<br>1. At Step 3, the actor leaves the room from the floating window.<br>2. The system leaves the room through UC-44 without asking for confirmation, because the main window is not in view, and closes the floating window.<br>3. The use case ends.<br>AF-3 – The browser does not support it<br>1. At Step 1, the browser cannot show a floating window.<br>2. The system does not offer it and the room works as in UC-43.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – The floating window is refused<br>1. At Step 2, the browser refuses to open the window, for example because it was not opened by a direct action.<br>2. The call stays in the main window and no message is displayed.<br>3. The use case ends. |  |  |

#### 8.8 UC-50 – Register to Speak

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Extends UC-43. Allows a person in the online room to raise a hand to ask for the floor, and allows the current Chair to see the raised hands in order, invite one person at a time to speak and close each turn. Raising a hand does not turn any microphone on or off: the invited person turns his or her own microphone on (UC-45a). The requests are kept with the Meeting as a record of who spoke and for how long; they are not tasks. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor is connected to the meeting room of an In Progress Meeting. (UC-43)<br>● PRE-3: To invite, reorder or close a turn, the actor is the current Chair of the Meeting. (BR-104, BR-105, BR-106) |  |  |
| Postconditions | ● POST-1: A raised hand is queued behind the earlier ones, or the hand is lowered. (BR-104, BR-105, BR-106)<br>● POST-2: An invited person is the only one holding the floor until the turn is closed; the invitation and the start and end of the turn are kept with the Meeting. (BR-104, BR-105, BR-106)<br>● POST-3: Microphones, Meeting Roles, attendance and the Meeting lifecycle are unchanged. (BR-102, BR-101) |  |  |
| Normal Flow | Register to Speak<br>1. The actor raises a hand. When an agenda item is under discussion, the request is linked to it.<br>2. The system checks that the actor is in the room, that the Meeting is In Progress and that the actor has no open request, then puts the request at the end of the queue and shows the raised hand to everyone in the room. (UC-46, BR-104, BR-105, BR-106)<br>3. The Chair sees the queue of raised hands in order, with each person’s name and the time of the request.<br>4. The Chair invites a person to speak, usually the first in the queue. (BR-104, BR-105, BR-106)<br>5. The system marks the request as invited and shows the person that it is his or her turn.<br>6. The person turns the microphone on (UC-45a) and starts speaking; the system marks the turn as speaking and keeps its start time. (BR-104, BR-105, BR-106)<br>7. The person finishes speaking, or the Chair closes the turn; the system keeps the end time, lowers the hand and removes the request from the queue. The Chair may invite the next person from Step 4. (BR-104, BR-105, BR-106) |  |  |
| Alternative Flows | AF-1 – Lower the hand<br>1. At any step before Step 6, the person lowers the hand.<br>2. The system cancels the request, lowers the hand and removes it from the queue. (BR-104, BR-105, BR-106)<br>3. The use case ends.<br>AF-2 – The Chair reorders the queue<br>1. At Step 3, the Chair moves a request up or down in the queue.<br>2. The system keeps the new order for everyone who sees the queue. (BR-104, BR-105, BR-106)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – The Chair declines a request<br>1. At Step 4, the Chair removes a request without inviting the person.<br>2. The system cancels the request and lowers the hand. (BR-104, BR-105, BR-106)<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Meeting is no longer in progress<br>1. At Step 2 or Step 4, the Meeting ended.<br>2. No request is created or changed. The system displays MSG117 – “The meeting room opens only while the meeting is in progress.”<br>3. The use case ends.<br>EX-2 – Actor already has an open request<br>1. At Step 2, the actor’s earlier request is still open.<br>2. Nothing changes and the hand stays raised. (BR-104, BR-105, BR-106)<br>3. The use case ends.<br>EX-3 – Actor is no longer the chair<br>1. At Step 4, or when reordering, declining or closing another person’s turn, the actor is not the current Chair, for instance because the Chair role was handed over through UC-34. (BR-104, BR-105, BR-106)<br>2. Nothing is changed. The system displays MSG457 – “Only the current Chair may run the speaking turns.”<br>3. The use case ends.<br>EX-4 – Actor is not in the room<br>1. At Step 2 or Step 6, the actor is not connected to the meeting room.<br>2. No request is created or changed. The system displays MSG458 – “You must be in the meeting room to register to speak.”<br>3. The use case ends.<br>EX-5 – Someone already holds the floor<br>1. At Step 4, another person is already invited or speaking. (BR-104, BR-105, BR-106)<br>2. Nobody is invited. The system displays MSG459 – “Someone is already invited or speaking. Close that turn first.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-6 – The request has changed<br>1. At any step, the request is no longer in a state that allows the action, for instance it was lowered, finished or not yet invited.<br>2. Nothing is changed. The system displays MSG460 – “This speaking request has changed. Reload the list.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

### 9. FE-09 – Meeting Recording

#### 9.1 UC-51 – View & Play Meeting Recordings

##### 9.1.1 UC-51a – View Meeting Recordings

| Primary Actors | Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who currently holds a Chair, Secretary or Participant assignment of a Meeting to view what was recorded in it: the sentences recorded from each person's own microphone, the room recordings and, for a Hybrid or Online Meeting that has ended, one combined recording. |  |  |
| Preconditions | ● PRE-1: The actor is signed in as an internal user and may view the Meeting.<br>● PRE-2: The actor currently holds a Chair, Secretary or Participant assignment of the Meeting. Being the Member who created the Meeting or its Approver alone is not enough. (BR-111, BR-112) |  |  |
| Postconditions | ● POST-1: The actor sees the recorded sentences and the room recordings that apply to the Meeting's format. Nothing is changed.<br>● POST-2: A Guest, or a person who is not on the current roster, sees no recording and cannot tell that any exists. Erased recordings are not listed. (BR-111, BR-112)<br>● POST-3: A sentence excluded from the record appears to everyone except the Chair and the Secretary as an empty entry, without its text. (BR-114, BR-115) |  |  |
| Normal Flow | View Meeting Recordings<br>1. The actor opens the recordings of the Meeting.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and loads the recorded sentences and the room recordings of the Meeting. (BR-111, BR-112)<br>3. The system shows the recorded sentences grouped by speaker, each with its time; the actor may select a sentence to play it (UC-51b). A sentence that repeats speech picked up by another microphone is marked as overlapping.<br>4. For an In-person or Hybrid Meeting, the system also shows each room recording with its state (Recording or Available), its source (room microphone, uploaded file or combined), its length, the date it will be erased, its takes and the state of its transcript. (BR-107, BR-110)<br>5. While the room microphone is on, the system shows everyone viewing the recordings a Recording indicator. |  |  |
| Alternative Flows | AF-1 – Online meeting<br>1. At Step 4, the Meeting is held online.<br>2. The system shows only the recorded sentences. No room recording and no audio upload is offered, because an Online Meeting records each person's own microphone. (BR-107)<br>3. The flow resumes at Step 5 of the Normal Flow.<br>AF-2 – Sentence excluded from the record<br>1. At Step 3, the Chair or the Secretary has excluded a sentence from the record.<br>2. The Chair and the Secretary see the sentence with its text and the reason. Everyone else sees an empty entry in its place: they can tell that a sentence once existed, but cannot read or play it. (BR-114, BR-115)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – Combined recording<br>1. At Step 4, the Meeting is Hybrid or Online, has Ended and is not Secret.<br>2. The system also lists one combined recording, labelled Combined, which follows the real time of the Meeting and leaves out the sentences excluded from the record. It can be played and downloaded (UC-51b, UC-51c) but has no transcript. (BR-122)<br>3. The flow resumes at Step 5 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is not on the current roster<br>1. At Step 2, the actor is a Guest or no longer holds a Chair, Secretary or Participant assignment. (BR-111, BR-112)<br>2. Nothing is shown and the system answers as if the Meeting had no recordings, so the actor cannot tell that any exist.<br>3. The use case ends. |  |  |

##### 9.1.2 UC-51b – Play Meeting Recording

| Primary Actors | Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who currently holds a Chair, Secretary or Participant assignment of a Meeting to listen to a room recording, the combined recording or to a single recorded sentence. |  |  |
| Preconditions | ● PRE-1: The actor is viewing the recordings of a Meeting in which the actor currently holds a Chair, Secretary or Participant assignment. (UC-51a, BR-111, BR-112)<br>● PRE-2: The recording or the sentence has not been erased or deleted. |  |  |
| Postconditions | ● POST-1: The audio plays for the actor. Nothing is saved to the actor's device and the record is not changed.<br>● POST-2: Only the Chair and the Secretary can play a sentence excluded from the record. (BR-114, BR-115) |  |  |
| Normal Flow | Play Meeting Recording<br>1. The actor selects Play on a room recording, the combined recording or on a recorded sentence.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and may hear this item. (BR-111, BR-112, BR-114, BR-115)<br>3. The system plays the recording, or the chosen sentence, from its beginning.<br>4. The actor can pause, move to another position, change the volume and change the playback speed. While playing, the words being said are highlighted. |  |  |
| Alternative Flows | AF-1 – Play an excluded sentence<br>1. At Step 1, the actor is the current Chair or Secretary and selects a sentence excluded from the record.<br>2. The system confirms that the actor is the Chair or the Secretary and allows the playback. (BR-114, BR-115)<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor no longer has access, or recording not Available<br>1. At Step 2, the actor is no longer on the roster, or the sentence is excluded from the record and the actor is neither the Chair nor the Secretary. (BR-111, BR-112, BR-114, BR-115)<br>2. Nothing plays and the system answers as if the audio did not exist.<br>3. The use case ends. |  |  |

##### 9.1.3 UC-51c – Download Meeting Recording

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to download a room recording or the combined recording to the computer as an MP3 file. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and currently holds the Chair or Secretary assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting is not Secret. (BR-111, BR-112)<br>● PRE-3: The recording is Available, not Erased. |  |  |
| Postconditions | ● POST-1: An MP3 file is saved on the actor's device.<br>● POST-2: The download is recorded in the audit log. (BR-111, BR-112) |  |  |
| Normal Flow | Download Meeting Recording<br>1. The actor selects Download on an available room recording or the combined recording.<br>2. The system checks that the actor is the current Chair or Secretary and that the Meeting is not Secret. (BR-111, BR-112)<br>3. The system records the download in the audit log. (BR-111, BR-112)<br>4. The system delivers the recording as an MP3 file. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is a Participant, not the Chair or the Secretary. (BR-111, BR-112)<br>2. No file is delivered and nothing is logged. The system displays MSG355 – “Only the Chair or Secretary may download meeting recordings and transcripts.”<br>3. The use case ends.<br>EX-2 – Secret meeting<br>1. At Step 2, the Meeting is Secret. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG356 – “Recordings and transcripts of Secret meetings cannot be downloaded.”<br>3. The use case ends. |  |  |

#### 9.2 UC-52 – Record Room Audio

##### 9.2.1 UC-52a – Turn Room Microphone On/Off

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to record the sound of the meeting room: turning the room microphone on starts a take, and turning it off saves the take. The Meeting's room recording is made of its takes. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The Meeting is In-person or Hybrid and is In Progress. (BR-107) |  |  |
| Postconditions | ● POST-1: While the microphone is on, a take is open, the room sound is recorded and everyone viewing the Meeting's recordings sees the Recording indicator.<br>● POST-2: When the microphone is turned off, the take is saved into the Meeting's room recording and the recording is transcribed again; text corrected and names assigned on earlier takes carry over where they still match. (UC-54a, BR-125)<br>● POST-3: The confirmation that attendees were informed, with its time and purpose, is kept with the recording. (BR-109) |  |  |
| Normal Flow | Turn Room Microphone On/Off<br>1. The Secretary turns the room microphone on from the recording card of an In-person Meeting.<br>2. If this is the first take of the Meeting's room recording, the system asks the Secretary to confirm that attendees were informed and nobody objected, and to enter the purpose of the recording (optional). (BR-109)<br>3. The Secretary confirms.<br>4. The system checks the Secretary's role, the format and the status of the Meeting and that the room microphone is not on elsewhere, opens a new take and keeps the time of the confirmation and the purpose. (BR-107, BR-108, BR-109)<br>5. The room sound is recorded while the microphone is on. Everyone viewing the Meeting's recordings sees the Recording indicator, and the recording card stays visible on the Secretary's other pages.<br>6. The Secretary turns the room microphone off.<br>7. The system saves the take into the Meeting's room recording and displays MSG363 – “Room recording take saved.” The Recording indicator disappears and the recording is transcribed. (UC-54a) |  |  |
| Alternative Flows | AF-1 – Hybrid meeting<br>1. At Step 1, the Meeting is Hybrid.<br>2. The Secretary turns on his or her microphone in the online meeting room. For the Secretary of a Hybrid Meeting that microphone is the room microphone, so the room has one switch and no separate recording card. (UC-45a)<br>3. Turning that microphone off later, including when the Chair turns the Secretary's microphone off in the online room, turns the room microphone off and saves the take as in Steps 6–7.<br>4. If the online meeting room cannot be used, the recording card on the Meeting page is the switch, as for an In-person Meeting.<br>5. The flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Reopening the Meeting while a take is open on this device<br>1. At Step 1, the Secretary closed the page or the browser while the room microphone was on and opens the Meeting again on the same device.<br>2. The system offers Continue recording or Save recorded part.<br>3. If the Secretary continues, the recording carries on in the same take and the flow resumes at Step 5 of the Normal Flow.<br>4. If the Secretary saves the recorded part, the flow resumes at Step 7 of the Normal Flow.<br>AF-3 – Take left open on another device<br>1. At Step 1, the system shows that the room is still being recorded on another device.<br>2. The Secretary chooses to stop the recording on the other device.<br>3. The system saves the part recorded on the other device as a take, so that it is transcribed. (BR-108)<br>4. The flow resumes at Step 1 of the Normal Flow, on this device.<br>AF-4 – Later take of the same Meeting<br>1. At Step 2, the Meeting already has a room recording, so an earlier take of it already received the confirmation.<br>2. The system does not ask again. If that room recording was erased (UC-52e), the confirmation is asked again. (BR-109)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-5 – No speech in the take<br>1. At Step 7, the take received no sound at all.<br>2. The system discards the take and leaves the recording unchanged. It displays MSG365 – “Microphone detected no speech; take was not saved.”<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Confirmation missing or information invalid<br>1. At Step 4, the confirmation is missing or the recording information is incomplete.<br>2. No take is opened. The system displays MSG291 – “Invalid recording information.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4 or Step 7, the actor is not the current Secretary. (BR-108)<br>2. No take is opened or saved. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Meeting is not in progress<br>1. At Step 4, the Meeting is not In Progress. (BR-107)<br>2. No take is opened. The system displays MSG359 – “Room microphone can only be turned on while the meeting is in progress.”<br>3. The use case ends.<br>EX-4 – Room microphone is on at another device<br>1. At Step 4, a take of the Meeting is still open on another device.<br>2. No take is opened. The system displays MSG360 – “Room microphone is currently active on another device.” The Secretary may stop that recording first (AF-3).<br>3. The use case ends.<br>EX-5 – Take already saved<br>1. At Step 7, the take was already saved by another action, for instance from another device.<br>2. Nothing is added. The system displays MSG362 – “This recording take has been closed and cannot accept additional audio.”<br>3. The use case ends. |  |  |

##### 9.2.2 UC-52c – Upload Meeting Audio File

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to add an audio file recorded outside the system to the recordings of an In-person Meeting. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The Meeting is In-person and is In Progress or Ended. (BR-107) |  |  |
| Postconditions | ● POST-1: The audio file is kept as a recording of its own and is transcribed. Takes recorded with the room microphone are never added to it. (UC-54a)<br>● POST-2: The confirmation that attendees were informed, with its time and purpose, is kept with the recording, and the upload is recorded in the audit log. (BR-109) |  |  |
| Normal Flow | Upload Meeting Audio File<br>1. The Secretary selects Upload audio file on the recordings of the Meeting.<br>2. The Secretary chooses an audio file from the computer.<br>3. The Secretary confirms that attendees were informed and nobody objected, and may enter the purpose of the recording. The system reminds the Secretary that the file must have been recorded with the meeting room microphone used for the voice samples. (BR-109)<br>4. The Secretary selects Upload.<br>5. The system checks the Secretary's role, the format and the status of the Meeting, the type and the size of the file, and keeps the file. (BR-107, BR-108, BR-109)<br>6. The system shows the file as uploaded and starts its transcription. (UC-54a) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – File or recording information is invalid<br>1. At Step 5, the confirmation is missing or the recording information is incomplete.<br>2. No file is kept. The system displays MSG291 – “Invalid recording information.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 5, the actor is not the current Secretary. (BR-108)<br>2. No file is kept. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Meeting does not allow uploads<br>1. At Step 5, the Meeting is neither In Progress nor Ended. (BR-107)<br>2. No file is kept. The system displays MSG294 – “Recordings may only be saved while the meeting is in progress or has ended.”<br>3. The use case ends. |  |  |

##### 9.2.3 UC-52d – Delete Recording Take

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to remove one take from a room recording: the sound and the sentences of that take are cut out while the other takes are kept. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The recording has at least two takes.<br>● PRE-3: The recording is Available; a take still being recorded cannot be deleted. |  |  |
| Postconditions | ● POST-1: The sound and the sentences of the deleted take are removed from the recording and the later takes move up to close the gap.<br>● POST-2: Text edited and names assigned on the other takes are kept. A speaker heard only in the deleted take loses its assigned name. (BR-125)<br>● POST-3: The deletion is recorded in the audit log. |  |  |
| Normal Flow | Delete Recording Take<br>1. The Secretary opens the takes of a room recording and chooses to delete one of them.<br>2. The system warns that the sound and the sentences of this take will be removed and asks for confirmation.<br>3. The Secretary confirms.<br>4. The system removes the sound and the sentences of the take, moves the later takes up and records the deletion in the audit log. (BR-108)<br>5. The system displays MSG366 – “Recording take deleted.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Recording has only one take<br>1. At Step 4, the recording has only one take.<br>2. Nothing is removed. The system displays MSG367 – “Recording contains only one take. Erase the entire recording to remove it.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4, the actor is not the current Secretary. (BR-108)<br>2. Nothing is removed. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |  |  |

##### 9.2.4 UC-52e – Erase Meeting Recording

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to erase the audio and the transcript of a recording – a room recording, an uploaded file or the combined recording – while keeping the fact that it existed. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The recording is Available; it is not a take that is still being recorded. Another take of the Meeting may be open at the same time. |  |  |
| Postconditions | ● POST-1: The audio and the transcript of the recording are permanently removed. (BR-110)<br>● POST-2: The recording no longer appears among the Meeting's recordings. The audit log keeps that it existed, who erased it and when. (BR-110)<br>● POST-3: After the room recording of a Meeting is erased, the next take of that Meeting asks again for the confirmation that attendees were informed. (UC-52a)<br>● POST-4: Erasing the room recording of a Meeting also erases its combined recording.<br>● POST-5: An erased combined recording is not built again. |  |  |
| Normal Flow | Erase Meeting Recording<br>1. The Secretary selects Erase recording on a recording: a room recording, an uploaded file or the combined recording.<br>2. The system asks for confirmation and explains that the audio and the transcript will be permanently removed.<br>3. The Secretary confirms.<br>4. The system removes the audio and the transcript and records the erasure and the Secretary in the audit log. (BR-110)<br>5. The system displays MSG296 – “Meeting recording erased.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Room microphone is still recording this take<br>1. At Step 4, the recording is a take that is still being recorded.<br>2. Nothing is erased. The system displays MSG364 – “Room microphone is currently recording. Turn off the microphone before erasing.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 4, the actor is not the current Secretary. (BR-108)<br>2. Nothing is erased. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |  |  |

#### 9.3 UC-53 – Manage My Recorded Speech

| Primary Actors | Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person to review the sentences recorded from his or her own microphone, listen to them again and delete a sentence of his or her own: an internal user while signed in and holding a Chair, Secretary or Participant assignment, or a Guest while the verified invitation session lasts. (BR-113, BR-111, BR-112) |  |  |
| Preconditions | ● PRE-1: The actor is signed in as an internal user and currently holds a Chair, Secretary or Participant assignment of the Meeting, or is a Guest with a verified, current invitation to it. A Guest sees and deletes only the sentences of their own microphone. (BR-113, BR-111, BR-112) |  |  |
| Postconditions | ● POST-1: A deleted sentence, with its text and its audio, is permanently removed from the Meeting record. (BR-113) |  |  |
| Normal Flow | Manage My Recorded Speech<br>1. In the online room or on the recordings of the Meeting, the actor opens My Recorded Speech.<br>2. The system lists the actor's sentences: those still waiting on this device to be delivered and those already saved, each with its time and its text.<br>3. The actor selects a sentence to listen to it.<br>4. The actor selects Delete on a sentence and confirms.<br>5. The system permanently removes the text and the audio of the sentence and records the deletion in the audit log. (BR-113)<br>6. The system displays MSG322 – “Spoken sentence deleted.” and refreshes the list. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is neither the speaker nor the Secretary<br>1. At Step 5, the sentence belongs to another speaker and the actor is not the Secretary. (BR-113)<br>2. Nothing is removed. For an internal user, the system displays MSG323 – “Only the speaker or the Secretary may delete this sentence.” A Guest gets no message, as if the sentence did not exist.<br>3. The use case ends. |  |  |

### 10. FE-10 – Transcription, Speaker Identification & Meeting Summary

#### 10.1 UC-54 – View Meeting Transcript

##### 10.1.1 UC-54a – View Recording Transcript

| Primary Actors | Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who currently holds a Chair, Secretary or Participant assignment to view the transcript of a recording: its state, the text with times and speakers, the dividers between takes and the text corrected by a person. A saved take or an uploaded audio file is transcribed without any action; the Secretary may also run the transcription. |  |  |
| Preconditions | ● PRE-1: The actor is signed in as an internal user and currently holds a Chair, Secretary or Participant assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting has a room recording or an uploaded audio file that has not been erased. |  |  |
| Postconditions | ● POST-1: The actor sees the state of the transcript and, once it is Completed, its passages with times, speakers and take dividers.<br>● POST-2: Suggestions are visible only to the Secretary. A name that is assigned – including one the system filled in by itself – is part of the transcript, so everyone who may read the transcript sees it; only the Secretary may change or clear it. (BR-111, BR-112, BR-120, BR-123)<br>● POST-3: When the Secretary runs the transcription, the transcript is made again from the whole recording and ends Completed or Failed with the reason; names assigned by hand, corrected text and moved sentences carry over where they still match, and what cannot be matched stays only in the audit log. (BR-121, BR-125, BR-154) |  |  |
| Normal Flow | View Recording Transcript<br>1. The actor selects View transcript on a recording.<br>2. The system checks that the actor currently holds a Chair, Secretary or Participant assignment and loads the transcript. (BR-111, BR-112)<br>3. The system shows the state of the transcript: Pending, Processing, Completed or Failed. For a Failed transcript it also shows the reason.<br>4. When the transcript is Completed, the system shows each passage with its time and its speaker (Speaker N, or the name assigned), a divider where one take ends and the next begins, and marks the text corrected by a person. While the recording plays, the words being said are highlighted, and words the system was unsure of are flagged.<br>5. For the Secretary, the system also shows the suggested names next to each unnamed speaker and the control to assign a speaker (UC-55b). (BR-120, BR-123) |  |  |
| Alternative Flows | AF-1 – Transcript is not completed yet<br>1. At Step 3, the transcript is Pending or Processing.<br>2. The system shows the state without text.<br>3. When the transcript is Completed, the flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Secretary runs the transcription<br>1. At Step 3, the actor is the current Secretary and the recording has no transcript yet, or its transcript is Failed.<br>2. The Secretary chooses to transcribe the recording, or to run it again after a failure.<br>3. The system checks that the actor is the current Secretary and that the recording is not already waiting or being transcribed. (BR-108)<br>4. The system puts the recording in line for transcription and displays MSG300 – “Recording queued for transcription.”<br>5. The flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Secretary transcribes a completed transcript again<br>1. At Step 4, the actor is the current Secretary and chooses to transcribe the recording again.<br>2. The system warns that the transcript will be made again and that text corrected and names assigned are kept only where they still match, and asks for confirmation. (BR-125, BR-154)<br>3. The Secretary confirms.<br>4. The flow resumes at Step 3 of AF-2. |  |  |
| Exception Flows | EX-1 – Recording already waiting or being transcribed<br>1. At Step 3 of AF-2, the recording is waiting or is being transcribed.<br>2. Nothing changes. The system displays MSG301 – “The recording is currently queued or being transcribed and cannot be rerun yet.”<br>3. The use case ends.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3 of AF-2, the actor is not the current Secretary. (BR-108)<br>2. Nothing changes. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – Combined recording<br>1. At Step 3 of AF-2, the recording is the combined recording of the Meeting. (BR-122)<br>2. Nothing is queued. The system displays MSG479 – “The combined recording is for listening only. Its speech has already been transcribed from the room and per-microphone recordings.”<br>3. The use case ends. |  |  |

##### 10.1.2 UC-54b – Export Meeting Transcript

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to export the transcript of a room recording, or the sentences recorded from each person's own microphone, as a text file. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and currently holds the Chair or Secretary assignment of the Meeting. (BR-111, BR-112)<br>● PRE-2: The Meeting is not Secret. (BR-111, BR-112)<br>● PRE-3: For a room recording, its transcript is Completed. |  |  |
| Postconditions | ● POST-1: A text file with the transcript is delivered to the actor; a speaker assigned to a Guest appears under that Guest's self-declared display name, or under the invited name while the Guest has not declared one.<br>● POST-2: Sentences excluded from the record are left out of the file.<br>● POST-3: The export is recorded in the audit log. (BR-111, BR-112) |  |  |
| Normal Flow | Export Meeting Transcript<br>1. The actor chooses to export the transcript of a recording, or the recorded sentences.<br>2. The system checks that the actor is the current Chair or Secretary, that the Meeting is not Secret and that the transcript is Completed. (BR-111, BR-112)<br>3. The system builds the text file without the sentences excluded from the record.<br>4. The system records the export in the audit log and delivers the file. (BR-111, BR-112) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Participant tries to export<br>1. At Step 2, the actor is a Participant, not the Chair or the Secretary. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG355 – “Only the Chair or Secretary may download meeting recordings and transcripts.”<br>3. The use case ends.<br>EX-2 – Secret meeting<br>1. At Step 2, the Meeting is Secret. (BR-111, BR-112)<br>2. No file is delivered. The system displays MSG356 – “Recordings and transcripts of Secret meetings cannot be downloaded.”<br>3. The use case ends.<br>EX-3 – Transcript is not ready<br>1. At Step 2, the transcript is Pending, Processing or Failed.<br>2. No file is delivered. The system displays MSG357 – “Transcript is not ready for download.”<br>3. The use case ends. |  |  |

#### 10.2 UC-55 – Edit Transcript & Speaker Attribution

##### 10.2.1 UC-55a – Edit Transcript Text

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to correct the text of a transcript passage or of a recorded sentence. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-114, BR-115)<br>● PRE-2: The passage or the sentence exists. |  |  |
| Postconditions | ● POST-1: The corrected text is saved with the name of the Secretary and the time of the change, to be carried onto the matching new sentence when the transcript is made again. (BR-114, BR-115, BR-125) |  |  |
| Normal Flow | Edit Transcript Text<br>1. In the transcript or on the timeline of recorded sentences, the Secretary selects a passage or a sentence to edit.<br>2. The Secretary changes the text and saves.<br>3. The system checks that the text is not empty and has at most 8,000 characters, saves it with the name of the Secretary and the time of the change, clears the uncertain-word marks on that passage, and records the change in the audit log. (BR-114, BR-115) A later run carries the corrected text onto the matching new sentence. (BR-125)<br>4. The system displays MSG302 – “Transcript segment saved.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Text is empty or too long<br>1. At Step 3, the text is empty or longer than 8,000 characters.<br>2. The text is not saved. The system displays MSG303 – “Transcript text is invalid.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3, the actor is not the current Secretary. (BR-114, BR-115)<br>2. The text stays as it was. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends. |  |  |

##### 10.2.2 UC-55b – Assign Speaker

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to give a speaker of a transcript the name of a person on the Meeting roster or of an invited Guest whose invitation is still valid, or to clear it, helped by the suggested names. A name the system filled in for a voice that matches a consenting person clearly may be kept, changed or cleared. A Guest is never suggested, because a Guest has no voice sample. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-108)<br>● PRE-2: The transcript of the room recording is Completed. |  |  |
| Postconditions | ● POST-1: The chosen name is shown on every passage of that speaker, or the name is cleared.<br>● POST-2: The change is recorded in the audit log.<br>● POST-3: A name the system filled in for a clearly matching voice is marked in the audit log as assigned automatically. (BR-120, BR-123)<br>● POST-4: A sentence moved to another voice is marked with who moved it and when, so a later run carries it again. (BR-125) |  |  |
| Normal Flow | Assign Speaker<br>1. In the transcript, the Secretary sees each speaker as Speaker N or with the name assigned, and the suggested name that matches the voice best. (BR-120, BR-123)<br>2. The Secretary chooses a person from the Meeting roster or a Guest with a current invitation of the Meeting, or clears the name. (BR-120)<br>3. The system checks that the person is on the current roster, or that the Guest has a current invitation of the Meeting. (BR-120)<br>4. The system applies the name to every passage of that speaker and records the change in the audit log.<br>5. The system displays MSG316 – “Speaker assigned.” |  |  |
| Alternative Flows | AF-1 – Several voices of the same person<br>1. At Step 2, the suggestions show that two or more speakers may be the same person.<br>2. The Secretary assigns the person to the whole group of speakers in one action, or clears a name the system filled in. (BR-120, BR-123)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – Change the speaker of one sentence<br>1. At Step 2, the Secretary selects one sentence of the transcript and chooses another speaker for it.<br>2. The Secretary chooses a person on the current roster, a Guest whose invitation is still valid, or any voice of this recording.<br>3. The system moves only that sentence to the chosen person's voice, creating a new voice when the person has none yet, and marks the sentence with the Secretary and the time of the move. A voice left with no sentence is hidden. (BR-125)<br>4. The system records the change in the audit log and displays MSG407 – “The speaker of this sentence has been changed.”<br>5. The flow resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Person is not on the current roster<br>1. At Step 3, the chosen person is not on the current roster of the Meeting, or the chosen Guest has no current invitation of the Meeting. (BR-120)<br>2. The name is not assigned. The system displays MSG317 – “Assigned person must be on the current meeting roster.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-2 – Actor is not the current Secretary<br>1. At Step 3, the actor is not the current Secretary. (BR-108)<br>2. Nothing is changed. The system displays MSG295 – “Only the meeting Secretary may manage recordings.”<br>3. The use case ends.<br>EX-3 – The voice is not in this recording<br>1. At Step 3, the chosen voice does not belong to this recording.<br>2. Nothing is changed. The system answers as if the recording had no such voice, without a message.<br>3. The use case ends. |  |  |

##### 10.2.3 UC-55c – Exclude or Restore a Sentence

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to exclude a recorded sentence from the record, for instance because it picked up a neighbour's speech, or to restore a sentence that was excluded. |  |  |
| Preconditions | ● PRE-1: The actor is the current Chair or Secretary of the Meeting. (BR-114, BR-115)<br>● PRE-2: The sentence exists on the timeline of recorded sentences. |  |  |
| Postconditions | ● POST-1: An excluded sentence is marked with its reason. It is not deleted: the Chair and the Secretary can still read and play it, while everyone else sees an empty entry in its place. (BR-114, BR-115)<br>● POST-2: Excluded sentences are left out of exported transcripts. |  |  |
| Normal Flow | Exclude or Restore a Sentence<br>1. On the timeline of recorded sentences, the actor chooses to exclude a sentence.<br>2. The system marks the sentence as excluded, keeps the reason, the actor and the time, and records the change in the audit log. (BR-114, BR-115)<br>3. The system displays MSG351 – “Sentence excluded from meeting record.” |  |  |
| Alternative Flows | AF-1 – Restore an excluded sentence<br>1. At Step 1, the actor chooses to restore an excluded sentence.<br>2. The system clears the exclusion, so the sentence is part of the record again, and displays MSG352 – “Sentence restored to meeting record.” (BR-114, BR-115)<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is neither the current Chair nor the current Secretary. (BR-114, BR-115)<br>2. The sentence is unchanged. The system displays MSG353 – “Only the Chair or Secretary may exclude a sentence from the meeting record.”<br>3. The use case ends. |  |  |

##### 10.2.4 UC-55d – Delete a Sentence

| Primary Actors | Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to permanently delete any recorded sentence, with its audio, from the timeline. Unlike excluding a sentence (UC-55c), a deleted sentence cannot be brought back. |  |  |
| Preconditions | ● PRE-1: The actor is the current Secretary of the Meeting. (BR-113)<br>● PRE-2: The sentence exists on the timeline of recorded sentences. |  |  |
| Postconditions | ● POST-1: The text and the audio of the sentence are permanently removed. (BR-113) |  |  |
| Normal Flow | Delete a Sentence<br>1. On the timeline of recorded sentences, the Secretary selects Delete on a sentence.<br>2. The system asks for confirmation and warns that the text and the audio will be permanently destroyed.<br>3. The Secretary confirms.<br>4. The system removes the text and the audio of the sentence and records the deletion in the audit log. (BR-113)<br>5. The system displays MSG322 – “Spoken sentence deleted.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is neither the Secretary nor the speaker<br>1. At Step 4, the actor is neither the Secretary nor the person who spoke the sentence (who deletes it through UC-53). (BR-113)<br>2. The sentence is unchanged. The system displays MSG323 – “Only the speaker or the Secretary may delete this sentence.”<br>3. The use case ends. |  |  |

#### 10.3 UC-56 – Manage My Voice ID

##### 10.3.1 UC-56a – View My Voice ID

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to see his or her own Voice ID: whether consent was given, which version of the notice was accepted and which voice samples it holds, both those the actor recorded and those recorded for the actor on a meeting room microphone. Voice ID belongs to internal users only: a Guest never has a voice profile and is never suggested as a speaker by voice. Deactivating the account ends Voice ID the same way withdrawing does (UC-06c, BR-117). |  |  |
| Preconditions | ● PRE-1: The actor is signed in as an internal user. |  |  |
| Postconditions | ● POST-1: The actor sees the consent status, the accepted notice version and the list of voice samples. Nothing is changed. |  |  |
| Normal Flow | View My Voice ID<br>1. The actor opens the My voice page.<br>2. The system loads the actor's consent and voice samples.<br>3. The system shows the consent status, the notice version the actor accepted, the voice samples the actor recorded (length and date recorded) and, while fewer than 5 samples exist, the option to record a new one. (BR-116, BR-124, BR-118, BR-119) |  |  |
| Alternative Flows | AF-1 – Notice has been updated<br>1. At Step 3, the actor consented under an earlier version of the notice than the current one.<br>2. The system tells the actor that the terms have changed and offers to accept the new terms (UC-56b). Until the actor accepts, the actor's name is only suggested, never filled in automatically. (BR-116, BR-124)<br>3. The flow resumes at Step 3 of the Normal Flow.<br>AF-2 – View samples recorded by a Secretary<br>1. At Step 3, the actor chooses to view the samples recorded by Secretaries, or opens the My voice page from the notification that a Secretary recorded a sample.<br>2. The system shows the samples that Secretaries recorded for the actor on the meeting room microphone, each with its Meeting, the Secretary who recorded it, its length and date, and how many of the allowed samples are used. The actor cannot record a sample there. (BR-118, BR-119)<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | N/A |  |  |

##### 10.3.2 UC-56b – Give Voice ID Consent

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to give separate, explicit consent to Voice ID, or to accept the current version of the notice when it changed. This consent is not part of the consent to recording. |  |  |
| Preconditions | ● PRE-1: The actor is signed in as an internal user and is on the My voice page.<br>● PRE-2: The actor has not consented yet, or consented under an earlier version of the notice. (BR-116, BR-124) |  |  |
| Postconditions | ● POST-1: The consent is recorded with the version of the notice the actor accepted, the purpose and the time. (BR-116, BR-124) |  |  |
| Normal Flow | Give Voice ID Consent<br>1. On the My voice page, the actor reads the Voice ID notice: its purpose (suggesting the person's name, and filling it in when the voice matches clearly, including from samples collected for the person), the voice data that is kept, who controls it, the actor's rights including withdrawal, and that voice data is sensitive personal data. (BR-116, BR-121, BR-118, BR-119, BR-124)<br>2. The actor gives consent explicitly; nothing is selected by default.<br>3. The system records the consent with the current version of the notice and the time, and records it in the audit log. (BR-116, BR-124)<br>4. The system displays MSG305 – “Voice identification enabled for your account.” |  |  |
| Alternative Flows | AF-1 – Accept the updated notice<br>1. At Step 1, the actor consented under an earlier version of the notice.<br>2. The actor reads the current notice and accepts it.<br>3. The system renews the consent on the current version of the notice and keeps the voice samples already recorded. The new terms apply from then on, including names filled in automatically and samples collected for the actor (UC-57b). (BR-116, BR-124, BR-120, BR-123, BR-118, BR-119)<br>4. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Notice version read is not the current one<br>1. At Step 3, the version of the notice the actor read is not the current one. (BR-116, BR-124)<br>2. No consent is recorded. The system displays MSG313 – “Invalid submission data.”<br>3. The use case ends. |  |  |

##### 10.3.3 UC-56c – Record Voice Sample

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user with Voice ID consent to record a short reading, listen to it and save it, so that the system can suggest his or her name, and fill it in when the voice matches clearly, when the Secretary names the speakers of a transcript. (BR-120, BR-123) |  |  |
| Preconditions | ● PRE-1: The actor has given Voice ID consent and has not withdrawn it. (BR-116)<br>● PRE-2: The actor has fewer than 5 voice samples the actor recorded. Samples collected for the actor are counted separately toward their own limit of fifty. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: A new voice sample is stored protected and the features of the voice are kept for matching. (BR-118, BR-119, BR-121) |  |  |
| Normal Flow | Record Voice Sample<br>1. On the My voice page, the actor selects Record sample.<br>2. The system shows a passage to read and asks the actor to read it aloud.<br>3. The actor selects Start recording, reads the passage and selects Stop. Nothing is sent yet: the system plays the recording back, and the actor either selects Save sample or Record again.<br>4. When the actor saves, the system receives the recording.<br>5. The system checks that the recording has 6 to 60 seconds of clear speech from one person. (BR-118, BR-119)<br>6. The system learns the features of the voice from the recording and the system stores the sample protected. (BR-118, BR-119, BR-121)<br>7. The system displays MSG307 – “Voice sample saved.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – No standing consent<br>1. At Step 4, the actor has no active Voice ID consent. (BR-116)<br>2. The sample is not kept. The system displays MSG308 – “Voice identification consent is required before recording a voice sample.”<br>3. The use case ends.<br>EX-2 – Audio is unsupported or fails the quality rules<br>1. At Step 5, the recording is too short, too long, too quiet, distorted or has more than one speaker. (BR-118, BR-119) Or the recording is not of a supported audio type.<br>2. The sample is not kept. The system displays MSG309 – “Voice sample did not meet quality requirements. Please read the full sample text clearly.” An unsupported audio type shows MSG292 – “Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Sample limit reached<br>1. At Step 1, the actor already has 5 voice samples the actor recorded. (BR-118, BR-119)<br>2. No recording starts. The system displays MSG310 – “Maximum of 5 voice samples reached. Delete an existing sample before recording a new one.”<br>3. The use case ends. |  |  |

##### 10.3.4 UC-56d – Play Voice Sample

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to listen to one of his or her own voice samples, recorded by himself or herself or collected for the person. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the owner of the voice sample. (BR-118, BR-119)<br>● PRE-2: The recording of the sample was kept. Samples recorded under an earlier notice kept only the features of the voice and cannot be played. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: The actor hears the sample. Nothing is changed and nobody else can play it, except a holder of the Voice sample collection permission playing a sample collected for the person (UC-57c). (BR-118, BR-119) |  |  |
| Normal Flow | Play Voice Sample<br>1. On the My voice page, the actor chooses to play one of his or her voice samples.<br>2. The system checks that the actor owns the sample and plays it to the actor. (BR-118, BR-119) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Sample is not the actor's, or its recording is not available<br>1. At Step 2, the sample is not the actor's, or its recording was not kept or cannot be read.<br>2. Nothing plays. No catalogue message is assigned to this case yet.<br>3. The use case ends. |  |  |

##### 10.3.5 UC-56e – Delete Voice Sample

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to delete one of his or her own voice samples, recorded by himself or herself or collected for the person. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and owns the voice sample. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: The sample, its recording and the features of its voice are permanently deleted.<br>● POST-2: The actor's other samples are unchanged. Each remaining sample is still used on its own to suggest the actor's name.<br>● POST-3: A sample collected for the actor no longer appears to the collector, and no longer counts toward the 50 collected samples for the actor. (BR-118, BR-119) |  |  |
| Normal Flow | Delete Voice Sample<br>1. On the My voice page, the actor chooses to delete one of his or her voice samples.<br>2. The system asks for confirmation.<br>3. The actor confirms.<br>4. The system deletes the sample, its recording and the features of its voice, and records the deletion in the audit log.<br>5. The system displays MSG311 – “Voice sample deleted.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | N/A |  |  |

##### 10.3.6 UC-56f – Withdraw Voice ID Consent

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an internal user to withdraw Voice ID consent. Withdrawing deletes all of the actor's voice samples, every speaker suggestion and every name the system filled in automatically for the actor. An administrator deactivating the account ends Voice ID the same way (UC-06c, BR-117). |  |  |
| Preconditions | ● PRE-1: The actor is signed in and has active Voice ID consent. (BR-116) |  |  |
| Postconditions | ● POST-1: All voice samples of the actor, both those the actor recorded and those recorded for the actor, with their recordings and voice features, are permanently deleted. (BR-117)<br>● POST-2: Every speaker suggestion that points to the actor, in every Meeting, and every name the system filled in automatically for the actor, is cleared. (BR-117)<br>● POST-3: Names already assigned by Secretaries are kept. (BR-117) |  |  |
| Normal Flow | Withdraw Voice ID Consent<br>1. On the My voice page, the actor chooses to withdraw consent.<br>2. The system asks for confirmation, explaining that all of the actor's voice samples will be permanently deleted.<br>3. The actor confirms.<br>4. The system deletes all of the actor's voice samples, clears the speaker suggestions and the names it filled in automatically for the actor, marks the consent as withdrawn and records it in the audit log. (BR-117)<br>5. The system displays MSG306 – “Voice identification disabled and all voice samples deleted.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | N/A |  |  |

#### 10.4 UC-57 – Collect Voice Samples for Attendees

##### 10.4.1 UC-57a – View Voice Collection

| Primary Actors | Voice sample collection permission holder (by default, Administrator) | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a holder of the Voice sample collection permission to see, for each person, whether the person agreed to Voice ID and which samples were collected for the person. |  |  |
| Preconditions | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: The actor sees the collection list. Nothing is changed. |  |  |
| Normal Flow | View Voice Collection<br>1. On the Users screen, the actor opens voice collection for a person.<br>2. The system checks the actor. (BR-118, BR-119)<br>3. The system shows the person’s standing consent and the samples collected for the person. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – No access: no Voice sample collection permission<br>1. At Step 2, the actor lacks the permission.<br>2. Nothing is done and the system answers as if the person did not exist. (BR-118, BR-119)<br>3. The use case ends. |  |  |

##### 10.4.2 UC-57b – Record Voice Sample for Attendee

| Primary Actors | Voice sample collection permission holder (by default, Administrator) | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a holder of the Voice sample collection permission to record a voice sample for a person on the Users screen, under that person’s own Voice ID consent and with per-take agreement. |  |  |
| Preconditions | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119)<br>● PRE-2: The person has standing Voice ID consent 1.4 or later. (BR-118, BR-119, BR-124) |  |  |
| Postconditions | ● POST-1: A new voice sample is stored protected for the person, with the collector and the per-take agreement, and the person is notified. (BR-118, BR-119, BR-011) |  |  |
| Normal Flow | Record Voice Sample for Attendee<br>1. Next to a person, the actor chooses to record a sample.<br>2. The system shows a passage to read.<br>3. The actor confirms that the person agreed to this take, and the person reads the passage while the actor records. Nothing is sent yet: the system plays the recording back, and the actor saves the sample or records again.<br>4. When the actor saves, the system receives the recording.<br>5. The system checks the actor, that the person agreed under notice 1.4 or later with per-take agreement, and that fewer than 50 samples were collected for the person. (BR-118, BR-119, BR-124)<br>6. The system checks that the recording has 6 to 60 seconds of clear speech from one person. (BR-118, BR-119)<br>7. The system learns the features of the voice and stores the sample protected, as the person’s own data, with the collector and the per-take agreement. (BR-118, BR-119, BR-121)<br>8. The system notifies the person and records the sample in the audit log. (BR-011)<br>9. The system displays MSG473 – “Voice sample recorded for the attendee.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Person has not agreed under the current terms<br>1. At Step 5, the person has no active consent under notice 1.4 or later. (BR-118, BR-119, BR-124)<br>2. The sample is not kept. The system displays MSG475 – “This person has not agreed to Voice ID under the current terms. They can enable it on the My voice page.”<br>3. The use case ends.<br>EX-2 – Person already has 50 collected samples<br>1. At Step 5, 50 samples were already collected for the person. (BR-118, BR-119)<br>2. The sample is not kept. The system displays MSG476 – “This person already has 50 collected voice samples. Delete a collected sample before recording a new one.”<br>3. The use case ends.<br>EX-3 – Audio is unsupported or fails the quality rules<br>1. At Step 4, the recording is not of a supported audio type. Or the recording is too short, too long, too quiet, distorted or has more than one speaker.<br>2. The sample is not kept. The system displays MSG292 – “Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG.” A recording that fails the quality rules shows MSG309 – “Voice sample did not meet quality requirements. Please read the full sample text clearly.”<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

##### 10.4.3 UC-57c – Play Collected Voice Sample

| Primary Actors | Voice sample collection permission holder (by default, Administrator) | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a holder of the Voice sample collection permission to play a voice sample collected for a person. |  |  |
| Preconditions | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: The actor hears the sample. The playback is recorded in the audit log. Nothing else is changed. (BR-011) |  |  |
| Normal Flow | Play Collected Voice Sample<br>1. The actor selects Play next to a collected sample.<br>2. The system checks the actor and the sample. (BR-118, BR-119)<br>3. The system records the playback in the audit log and plays the sample. (BR-011) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – No access: no Voice sample collection permission<br>1. At Step 2, the actor lacks the permission or the sample does not exist.<br>2. Nothing plays, as if the sample did not exist. The use case ends. |  |  |

##### 10.4.4 UC-57d – Delete Collected Voice Sample

| Primary Actors | Voice sample collection permission holder (by default, Administrator) | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a holder of the Voice sample collection permission to delete a voice sample collected for a person, so a new one can be recorded instead. |  |  |
| Preconditions | ● PRE-1: The actor holds the Voice sample collection permission. (BR-118, BR-119) |  |  |
| Postconditions | ● POST-1: The sample, its recording and the features of its voice are permanently deleted, and the deletion is recorded in the audit log. (BR-118, BR-119, BR-011) |  |  |
| Normal Flow | Delete Collected Voice Sample<br>1. The actor selects Delete on a collected sample.<br>2. The system asks for confirmation.<br>3. The actor confirms.<br>4. The system checks as in UC-57c, deletes the sample, its recording and the features of the voice, and records the deletion in the audit log. (BR-118, BR-119, BR-011)<br>5. The system displays MSG474 – “Voice sample deleted.” |  |  |
| Alternative Flows | AF-1 – Record again<br>1. After Step 5, the actor may record a new sample for the person (UC-57b). Recording again always means delete then record. |  |  |
| Exception Flows | EX-1 – No access: no Voice sample collection permission, or the sample was recorded by the person<br>1. At Step 4, the actor lacks the permission or the sample does not exist.<br>2. Nothing is deleted, as if the sample did not exist. The use case ends. |  |  |

#### 10.5 UC-58 – Generate / Regenerate Meeting Summary

| Primary Actors | Chair, Secretary | Secondary Actors | AI drafting service — the team's own server, or an outside AI service for a Normal meeting when the administrator has chosen one |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to ask the system to write a first draft of the meeting's content in three sections — I. Mục tiêu và phạm vi cuộc họp, II. Nội dung thảo luận và đánh giá, III. Kết luận và phân công thực hiện — from the meeting's own record. The draft is written by AI and is a proposal only: a person reviews it before it is used. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: The Meeting has Ended. (BR-126, BR-127)<br>● PRE-3: The drafting service is available to the system.<br>● PRE-4: No draft of this Meeting is waiting to be written or being written. |  |  |
| Postconditions | ● POST-1: The request is accepted; any earlier draft stays readable until the new one is ready.<br>● POST-2: A draft of the three sections exists, marked as written by AI, with the time the AI read the transcript and the person who asked for it.<br>● POST-3: The new draft replaces the earlier one whole; its corrections and its review are cleared.<br>● POST-4: Attendance, votes and every other Meeting record are unchanged; the vote results in section III are copied from the closed votes. (BR-128) |  |  |
| Normal Flow | Generate Meeting Summary<br>1. The Chair or the Secretary opens the draft of the Meeting's minutes. (BR-126, BR-127)<br>2. The system checks the current assignment and the Meeting status and shows what a draft would be based on: sentences recorded, voices not yet named, transcripts still running or failed, agenda items and how many were timed, and closed votes. (BR-126, BR-127, BR-128)<br>3. The actor asks for a draft, or for a new one when a draft already exists.<br>4. The system accepts the request, keeps the earlier draft readable and displays MSG384 – “The request was sent. The draft appears when the AI has written it.”<br>5. The system writes the three sections from the transcript, the agenda items and the conclusions the Chair recorded; section II follows the agenda items, and the result of each closed vote is copied as recorded, not written by AI. (BR-128)<br>6. The system marks the draft as written by AI, clears any earlier review and shows the draft as soon as it is ready. |  |  |
| Alternative Flows | AF-1 – First draft<br>1. At Step 3, no draft exists yet.<br>2. The system accepts the request and the flow resumes at Step 4 of the Normal Flow.<br>AF-2 – Regenerate and replace<br>1. At Step 3, a draft already exists, including a reviewed one.<br>2. The system asks the actor to confirm that the new draft will replace the current text and its review.<br>3. The actor confirms and the flow resumes at Step 4 of the Normal Flow; the earlier draft stays readable until the new one is ready.<br>AF-3 – No agenda, or items never timed<br>1. At Step 5, the Meeting has no agenda, or none of its items was started.<br>2. The system writes section II from the whole transcript instead of by agenda item.<br>3. The flow resumes at Step 6 of the Normal Flow.<br>AF-4 – Little recorded<br>1. At Step 5, the Meeting has only a sentence or two.<br>2. The system still writes a draft from what was recorded, rather than inventing content. (BR-128)<br>3. The flow resumes at Step 6 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. No request is queued. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – The Meeting has not ended<br>1. At Step 2, the Meeting has not Ended. (BR-126, BR-127)<br>2. No request is queued. The system displays MSG386 – “A draft can be created only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – A draft is already being written<br>1. At Step 4, a request is waiting or the AI is writing.<br>2. The earlier draft stays as it is and no second request is queued. The system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The use case ends. |  |  |

#### 10.6 UC-59 – View Meeting Summary

| Primary Actors | Chair, Secretary; every other person who may read the Meeting, once the draft is reviewed | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a person who may read the Meeting to read the AI draft. Until the draft is reviewed only the current Chair and the current Secretary see it; after review the other people who may read the Meeting see it as well, without any control to change it. (BR-126, BR-127) |  |  |
| Preconditions | ● PRE-1: The internal user is signed in and may read the Meeting.<br>● PRE-2: For anyone other than the current Chair or the current Secretary, the draft has been reviewed. (BR-126, BR-127) |  |  |
| Postconditions | ● POST-1: The draft, its state and where each part came from are displayed.<br>● POST-2: No draft, review or Meeting record is changed. |  |  |
| Normal Flow | View Meeting Summary<br>1. The actor opens the minutes of the Meeting; they open only after the Meeting has Ended.<br>2. The system checks that the actor may read the Meeting and whether the actor is its current Chair or current Secretary. (BR-126, BR-127)<br>3. The system displays the draft labelled “Nội dung do AI tạo — cần rà soát”, its three sections I–III, the time the AI read the transcript and the person who asked for it.<br>4. The system shows the review state — who reviewed it and when, and who last corrected it and when — and, for each part of section II, the stretch of the Meeting the part was written from, so the reader can check it against the recording.<br>5. While a draft is waiting or being written, the system shows that state; the earlier draft stays readable.<br>6. When voices are still unnamed, the current Chair or Secretary may name them from the draft, under the same rules as UC-55b. |  |  |
| Alternative Flows | AF-1 – No draft yet<br>1. At Step 3, no draft exists, or none has finished being written.<br>2. The system shows an empty state; for the current Chair and Secretary it also shows what a draft would be based on (BR-128), and everyone else sees nothing about the draft.<br>3. The use case ends.<br>AF-2 – The last draft failed<br>1. At Step 3, the last attempt was marked as failed.<br>2. The system shows the reason. The current Chair or Secretary may ask again (UC-58); the flow resumes at Step 3 of the Normal Flow.<br>AF-3 – Read a reviewed draft as another participant<br>1. At Step 2, the actor is not the current Chair or Secretary and the draft has been reviewed.<br>2. The system applies Steps 3–4 and offers no correction or review control. (BR-126, BR-127)<br>3. The use case ends.<br>AF-4 – The draft is out of date<br>1. At Step 3, the transcript changed after the AI read it — a sentence was added, corrected, excluded or deleted, a voice was named or lost its name because consent was withdrawn (BR-117), a sentence moved to another voice, or the recording was deleted.<br>2. The system marks the draft as out of date and offers to write it again (UC-58); the draft itself is unchanged.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – The Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its draft.<br>3. The use case ends. |  |  |

#### 10.7 UC-60 – Edit Meeting Summary

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to correct the text of the three sections before the draft is used. Saving the correction withdraws the review, so the other people who may read the Meeting stop seeing the draft until it is reviewed again. (BR-126, BR-127) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: A finished draft exists. The AI is not writing the draft again. |  |  |
| Postconditions | ● POST-1: The corrected text of sections I–III is saved with the person and the time of the correction.<br>● POST-2: The review is cleared, so only the current Chair and Secretary see the draft again.<br>● POST-3: No other Meeting record is changed. |  |  |
| Normal Flow | Edit Meeting Summary<br>1. The actor chooses to edit the draft.<br>2. The system checks the assignment and that a finished draft exists, then shows the three sections for editing. (BR-126, BR-127)<br>3. The actor changes the text and saves.<br>4. The system checks that no section is empty and that each is within its limit: I 2,000, II 12,000 and III 8,000 characters.<br>5. The system saves the three sections, records the person and the time, and clears the review.<br>6. The system displays MSG388 – “Draft saved.” and the draft without its review. |  |  |
| Alternative Flows | AF-1 – Correct a reviewed draft<br>1. At Step 3, the draft had been reviewed.<br>2. The system warns that saving withdraws the review and that the other participants stop seeing the draft until it is reviewed again.<br>3. The actor confirms and the flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. Nothing is saved. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – No finished draft, or the AI is writing it again<br>1. At Step 2, no draft has finished being written. Or the AI is still writing the draft.<br>2. Nothing is saved. The system displays MSG390 – “There is no draft to correct or review.” When the AI is still writing, the system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The use case ends.<br>EX-3 – A section is empty or too long<br>1. At Step 4, a section is empty or longer than its limit.<br>2. Nothing is saved. The system displays MSG389 – “The draft content is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

#### 10.8 UC-61 – Approve Meeting Summary

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to vouch for the draft. From that moment the other people who may read the Meeting see the draft, and the minutes may use its three sections. (BR-126, BR-127) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-126, BR-127)<br>● PRE-2: A finished draft exists. The AI is not writing the draft again. |  |  |
| Postconditions | ● POST-1: The reviewer and the time of the review are saved.<br>● POST-2: Everyone who may read the Meeting can read the draft. (BR-126, BR-127)<br>● POST-3: The draft text is unchanged. |  |  |
| Normal Flow | Approve Meeting Summary<br>1. The actor chooses to approve the draft.<br>2. The system checks the assignment and that a finished draft exists. (BR-126, BR-127)<br>3. The actor confirms.<br>4. The system records the reviewer and the time, leaves the text unchanged and displays MSG391 – “Draft approved.”<br>5. From then on everyone who may read the Meeting can read the draft, and the minutes use sections I–III. |  |  |
| Alternative Flows | AF-1 – Approve again<br>1. At Step 2, the draft has already been reviewed and has not been corrected since.<br>2. The system applies Steps 3–4 again; the later reviewer and time replace the earlier ones.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the Meeting but is neither its current Chair nor its current Secretary. (BR-126, BR-127)<br>2. Nothing is reviewed. The system displays MSG385 – “Only the Chair or Secretary may create, edit and review the draft.”<br>3. The use case ends.<br>EX-2 – No finished draft<br>1. At Step 2, no draft has finished being written.<br>2. Nothing is reviewed. The system displays MSG390 – “There is no draft to correct or review.”<br>3. The use case ends.<br>EX-3 – The AI is writing the draft again<br>1. At Step 2, a new draft is being written for this Meeting.<br>2. Nothing is reviewed. The system displays MSG387 – “The AI is writing the draft, please wait.”<br>3. The flow resumes at Step 1 of the Normal Flow when the draft is ready. |  |  |

### 11. FE-11 – Voting Management

Scope: the current Chair or the current Secretary may prepare Vote drafts while the Meeting is Approved, Published or InProgress. Only the current Chair opens a Vote, and opening and casting require InProgress. Each Meeting permits at most one Open Vote, and opening requires at least one eligible voter. End Meeting is independent of Vote closure; the Chair may close and tally an existing Open Vote and record its conclusion in InProgress or Ended. Each Vote belongs directly to its Meeting. No post-meeting ballot casting, time-boxed voting, quorum or separate Resolution is provided. (BR-026, BR-090, BR-092, BR-093, BR-086, BR-087, BR-098)

#### 11.1 UC-62 – View Meeting Votes

##### 11.1.1 UC-62a – View Meeting Votes

| Primary Actors | Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to browse the Votes of a Meeting. Draft Votes are visible only to the current Chair and the current Secretary. Open and Closed Votes remain viewable by users with Meeting viewing permission, including after the Meeting ends. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-016, BR-017, BR-055, BR-086, BR-087) |  |  |
| Postconditions | ● POST-1: The actor sees only Votes that the actor is authorized to view.<br>● POST-2: No Vote, ballot or conclusion is changed. |  |  |
| Normal Flow | View Meeting Votes<br>1. The actor opens the Voting area of a Meeting.<br>2. The system checks current viewing permission; the current Chair or Secretary may access Vote preparation in Approved without gaining access to other restricted Meeting records. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>3. The system lists the Votes belonging to this Meeting and excludes Draft Votes unless the actor is the current Chair or the current Secretary. Displayed counts include only visible Votes. (BR-086, BR-087)<br>4. The system displays each visible Vote with its content, ballot type and status. Open Votes show turnout; Closed Votes offer View result and show any recorded conclusion. No Agenda Item is selected or displayed as Vote ownership. (BR-086, BR-087)<br>5. The actor browses the Votes. Selecting a Vote continues to UC-62b – View Vote Status. |  |  |
| Alternative Flows | AF-1 – No visible votes<br>1. At Step 3, no Vote is visible to the actor, including when only Draft Votes restricted to the Chair and Secretary exist.<br>2. The system displays MSG207 – "No votes to display." without revealing hidden Draft Votes.<br>3. No data changes. The use case ends. |  |  |
| Exception Flows | EX-1 – Meeting votes are not accessible<br>1. At Step 2, the Meeting is unavailable or the actor no longer has permission to view its Votes, including after Guest invitation expiry or revocation.<br>2. The system displays MSG205 – "Meeting votes are not accessible." and does not reveal Vote information.<br>3. No data changes. The use case ends. |  |  |

##### 11.1.2 UC-62b – View Vote Status

| Primary Actors | Member, Approver, Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authorized user to view the configuration of a visible Draft Vote, the turnout of an Open Vote, or the tally and any Chair conclusion of a Closed Vote. Viewing a result does not change that result. |  |  |
| Preconditions | ● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor has current permission to view the Meeting's Votes. The current Chair or Secretary may access Vote preparation in Approved; other Meeting records follow their own access rules. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>● PRE-3: The selected Vote belongs to that Meeting and is visible to the actor. Draft visibility is restricted to the current Chair and the current Secretary. (BR-086, BR-087) |  |  |
| Postconditions | ● POST-1: The current Vote status and the information allowed for its ballot type are displayed.<br>● POST-2: No Vote, ballot or conclusion is changed. |  |  |
| Normal Flow | View Open Vote Status<br>1. The actor selects an Open Vote from the Meeting Voting area.<br>2. The system verifies current Vote viewing permission and that the Vote is visible to the actor. (BR-016, BR-017, BR-055, BR-086, BR-087)<br>3. The system displays its Meeting, content, options, public or secret ballot type, selected roles, opening time, the fixed eligible-voter count and its Vote documents. (BR-091, BR-094, BR-086, BR-087)<br>4. The system displays the number of ballots recorded against that fixed count. While the Vote is Open, individual choices and results per option are not displayed.<br>5. The actor sees the turnout change as ballots are recorded. For a secret Vote, the change does not identify the person who just voted or link that person to a choice. (BR-096, BR-097)<br>6. If the Meeting has ended while the Vote remains Open, the system shows that no further ballots are accepted; the recorded turnout remains viewable. (BR-092, BR-093) |  |  |
| Alternative Flows | AF-1 – View a draft vote<br>1. At Step 1, the current Chair or Secretary selects a Draft Vote.<br>2. The system applies Step 2, then displays its saved content, options, ballot type, selected roles and Vote documents. No eligible-voter list has been fixed and no ballots exist yet. (BR-091, BR-086, BR-087)<br>3. The use case ends without changing the Draft Vote.<br>AF-2 – View a closed vote<br>1. At Step 1, the actor selects View result for a Closed Vote, including a Vote with a recorded conclusion.<br>2. The system applies Step 2, then displays opening and closing times, the fixed eligible count, recorded-ballot count and tally per option. Percentages use the ballots cast; with no ballots, counts are zero and no percentage is shown. (BR-098)<br>3. For a public Vote, the system also displays each eligible voter's choice or that the person did not vote. For a secret Vote, it displays only totals and no voter-choice links. (BR-096, BR-097)<br>4. The system displays any saved Chair conclusion and optional note, or states that a conclusion has not yet been recorded. The system does not determine the conclusion from the tally. (BR-099)<br>5. The use case ends without changing the result. |  |  |
| Exception Flows | EX-1 – Meeting or Vote unavailable or Draft not visible<br>1. At Step 2, the Meeting or Vote is unavailable, access has been lost, or the actor attempts to view a Draft Vote without being the current Chair or Secretary.<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing the Vote.<br>3. No data changes. The use case ends. |  |  |

#### 11.2 UC-63 – Create / Configure Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to prepare a Vote draft, and to change or delete it, while the Meeting is Approved, Published or InProgress. Only the current Chair may open the Vote straight from the form during the Meeting (UC-64). A Vote belongs to the Meeting, not to an Agenda Item; its documents are managed in UC-68. (BR-086, BR-087, BR-090) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair or Secretary of the Meeting.<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: For editing or deletion, the selected Vote is Draft and belongs to this Meeting. (BR-088, BR-086, BR-087) |  |  |
| Postconditions | ● POST-1: Saving a draft creates or updates a Draft Vote; deleting removes only the selected Draft Vote.<br>● POST-2: Opening through UC-64 instead creates or updates an Open Vote and fixes its eligible-voter list. It does not also produce a separate saved draft.<br>● POST-3: Other Votes, previously recorded ballots and conclusions remain unchanged. |  |  |
| Normal Flow | Create Vote Draft<br>1. The actor selects Create Vote in the Meeting's voting area.<br>2. The system checks that the actor is the current Chair or Secretary and that the Meeting status allows drafts, then shows the Vote form. By default the Vote is Public, with the options Agree, Disagree and Abstain and all four meeting roles selected. (BR-086, BR-087)<br>3. The actor enters the content, defines and orders the options, chooses the ballot type and selects one or more eligible roles.<br>4. The actor selects Save draft.<br>5. The system checks again that the actor may still prepare drafts, and checks the content, at least two distinct non-empty options, the ballot type and at least one role. (BR-086, BR-087, BR-089)<br>6. The system saves the Vote as Draft under the Meeting. Voting is not opened and no voter list is fixed. (BR-091)<br>7. The system displays MSG210 – “Vote draft created successfully.” and the saved Draft Vote. |  |  |
| Alternative Flows | AF-1 – Configure an existing Draft Vote<br>1. At Step 1, the actor selects Edit on a Draft Vote.<br>2. The system shows its saved content, options, ballot type and roles. The actor changes them and selects Save draft. (BR-088)<br>3. The system performs the checks of Step 5 and checks that the Vote is still Draft. (BR-088)<br>4. The system updates the draft and displays MSG211 – “Vote draft updated successfully.” No voting is opened. The use case ends.<br>AF-2 – Delete a draft vote<br>1. At Step 1, the actor selects Delete on a Draft Vote and confirms.<br>2. The system checks that the actor may still manage drafts and that the Vote is still Draft. (BR-086, BR-087)<br>3. The system removes only that Draft Vote and displays MSG212 – “Vote draft deleted successfully.” No other Vote, ballot or conclusion changes. The use case ends.<br>AF-3 – Open immediately<br>1. At Step 4, or when saving in AF-1, the current Chair chooses to open the Vote instead of saving a draft while the Meeting is InProgress. Only the Chair is offered this choice. (BR-090)<br>2. Choosing to open confirms the opening; the flow continues with UC-64 AF-1, without a separate draft save or another confirmation.<br>3. The use case ends with the outcome of UC-64. If opening fails, no new draft or draft change is saved and the entered information stays for correction. |  |  |
| Exception Flows | EX-1 – Invalid Vote information<br>1. At Step 5, or the same check in AF-1, the content, options, ballot type or roles are invalid. (BR-089)<br>2. No new draft or change is saved. The system displays MSG209 – “Invalid vote information. Check the highlighted fields.” with the invalid fields identified.<br>3. The actor corrects the information. The flow resumes at Step 3 of the Normal Flow, or at Step 2 of AF-1.<br>EX-2 – Draft operation is no longer allowed<br>1. At Step 2 or Step 5, or in AF-1 or AF-2, the actor is no longer the current Chair or Secretary, the Meeting is not Approved, Published or InProgress, or the selected Vote is no longer Draft. (BR-086, BR-087, BR-088)<br>2. The draft is not created, changed or deleted. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-3 – Meeting or Vote is not accessible<br>1. At Step 2, or before an action completes, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is never disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. Nothing is changed. The system displays MSG205 – “Meeting votes are not accessible.” without revealing hidden Vote information.<br>3. The use case ends. |  |  |

#### 11.3 UC-64 – Open Vote

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to open a valid Vote during an InProgress Meeting. Opening fixes the eligible-voter list and count. Only one Vote may be Open in a Meeting at a time. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress. (BR-090)<br>● PRE-3: The selected Vote belongs to this Meeting and is Draft, or the Chair is opening a new Vote from the form in UC-63. (BR-086, BR-087)<br>● PRE-4: No other Vote in the Meeting is Open. (BR-090) |  |  |
| Postconditions | ● POST-1: The Vote is Open with its opening time recorded.<br>● POST-2: Its eligible-voter list and count are fixed from the selected roles at opening. (BR-091)<br>● POST-3: Eligible voters can submit one ballot while the Meeting remains InProgress and the Vote remains Open. |  |  |
| Normal Flow | Open Vote<br>1. The Chair selects Open on a Draft Vote.<br>2. The system shows the content, ballot type, options and selected roles of the Vote for confirmation.<br>3. The Chair confirms opening.<br>4. The system checks again that the actor is the current Chair, the Meeting is InProgress, the Vote is a valid Draft of this Meeting and no other Vote in the Meeting is Open. (BR-086, BR-087, BR-089, BR-090)<br>5. The system fixes the eligible-voter list from the people who currently hold the selected meeting roles and may access the Meeting; attendance does not count, and the list must name at least one person. (BR-090, BR-091)<br>6. The system opens the Vote and records the opening time. (BR-091)<br>7. The system displays MSG217 – “Voting opened successfully.” and authorized Meeting users see the Open Vote. |  |  |
| Alternative Flows | AF-1 – Open from the create or edit form<br>1. At Step 1, the Chair chooses to open the Vote from the form in UC-63. The form already shows the Vote information, so this choice confirms opening; Steps 2–3 are not repeated.<br>2. The system uses the entered information and performs Steps 4–5. An edited Vote must still be Draft. (BR-088)<br>3. Nothing from the form is saved unless the Vote opens.<br>4. The flow resumes at Step 6 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Configuration is invalid<br>1. At Step 4, the configuration does not satisfy required content, options, ballot type or selected roles. (BR-089)<br>2. The system displays MSG209 – "Invalid vote information. Check the highlighted fields." Voting is not opened; any saved draft remains unchanged.<br>3. The Chair returns to UC-63 to correct the draft or entered configuration. The use case ends.<br>EX-2 – Opening is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or an existing Vote is no longer Draft. (BR-090)<br>2. The system displays MSG218 – "Only the current Chair can open a Draft vote while the meeting is InProgress." No Vote is opened and existing Vote data remains unchanged.<br>3. The use case ends.<br>EX-3 – Another vote is open<br>1. At Step 4, another Vote in this Meeting is Open. (BR-090)<br>2. The system displays MSG219 – "Another vote is already open in this meeting. Close it before opening this vote." The selected Vote is not opened and the other Vote is unchanged.<br>3. The use case ends.<br>EX-4 – No eligible voter<br>1. At Step 5, nobody currently holds the selected roles with access to the Meeting. (BR-090)<br>2. Voting is not opened and any saved draft stays unchanged. The system displays MSG220 – “No eligible voters are available for the selected roles. Voting was not opened.”<br>3. The use case ends. |  |  |

#### 11.4 UC-65 – Cast Vote

| Primary Actors | Chair, Secretary, Participant, Guest | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an eligible person to submit one choice in an Open Vote during the Meeting. Eligibility comes from the list fixed at opening and requires current Meeting access. Attendance is not a condition. A saved ballot cannot be changed or withdrawn. |  |  |
| Preconditions | ● PRE-1: The internal actor is signed in, or the Guest has verified the invited email and has a valid invitation.<br>● PRE-2: The actor retains current Meeting access and is on the Vote's fixed eligible-voter list. (BR-092, BR-093, BR-091)<br>● PRE-3: The Meeting is InProgress and the Vote is Open. (BR-092, BR-093)<br>● PRE-4: The actor has not already cast a ballot in this Vote. (BR-092, BR-093)<br>● PRE-5: The actor has confirmed reading every Vote document that is still attached to the Meeting, through UC-42. (BR-094, BR-095) |  |  |
| Postconditions | ● POST-1: One ballot for one option is accepted, and the actor is marked as having voted.<br>● POST-2: Turnout increases by one, and the ballot cannot be changed or withdrawn.<br>● POST-3: A secret ballot does not associate the voter with the chosen option. (BR-096, BR-097) |  |  |
| Normal Flow | Cast Vote<br>1. The actor opens an Open Vote.<br>2. The system checks that the actor may vote now, then shows the content, the options, the ballot type and the notice that a submitted ballot cannot be changed or withdrawn. If the Vote has documents, it lists each one with the actor's read status, and the ballot can be submitted only when every document is confirmed. (BR-091, BR-092, BR-093, BR-095)<br>3. The actor selects one option and selects Submit ballot.<br>4. The system checks again the option, the Meeting and Vote status, the actor's place on the fixed voter list and current access, the read confirmations and that the actor has not voted yet. (BR-091, BR-092, BR-093, BR-095)<br>5. The system accepts the ballot, marks the actor as having voted and updates turnout. For a secret Vote, it does not associate the actor with the chosen option. (BR-092, BR-093, BR-096, BR-097)<br>6. The system displays MSG227 – “Your ballot has been recorded.” and the current turnout. The actor cannot submit another ballot; for a secret Vote, the actor sees only that a ballot was submitted. (BR-093, BR-096) |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – No option selected, or the option is not valid<br>1. At Step 3, the actor attempts to submit without selecting exactly one option, or submits an option that does not belong to this Vote. (BR-092, BR-093)<br>2. No ballot is recorded. The system displays MSG222 – "Select one option before submitting your ballot." When the option does not belong to this Vote, the system displays MSG223 – “The selected option is not available for this vote.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Actor is not eligible, or has not confirmed reading the documents<br>1. At Step 2 or Step 4, the actor can view the Vote but is not on its fixed eligible list. A role change alone does not remove a person from that list; a person added after opening is not added to the list. (BR-091, BR-092, BR-093) Or, at Step 4, the actor has not confirmed that they have read every vote document.<br>2. No new ballot is recorded and previously saved ballots remain unchanged. The system displays MSG224 – "You are not eligible to cast a ballot in this vote." When the documents are not all confirmed, the system displays MSG252 – “Confirm that you have read all vote documents before casting your ballot.” and lists the documents still to read.<br>3. The use case ends.<br>EX-3 – Balloting has ended, or the actor already voted<br>1. At Step 2 or Step 4, the Vote is no longer Open or the Meeting is no longer InProgress, even if the Vote still appears Open. (BR-092, BR-093) Or, at Step 4, the actor has already voted, including two submissions sent at the same time.<br>2. No ballot is recorded and turnout is unchanged. The system displays MSG225 – "This vote is no longer accepting ballots." When the actor has already voted, the system displays MSG226 – “Your ballot has already been recorded. It cannot be changed or withdrawn.” and the recorded ballot stays unchanged.<br>3. The use case ends. |  |  |

#### 11.5 UC-66 – Close Vote

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to close an existing Open Vote and obtain its tally while the Meeting is InProgress or Ended. Closing a Vote does not end the Meeting and does not determine the Chair's conclusion. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended. (BR-098)<br>● PRE-3: The Vote belongs to the Meeting and is Open. |  |  |
| Postconditions | ● POST-1: The Vote is Closed and its closing time is recorded; the tally is available from the recorded ballots.<br>● POST-2: No further ballots are accepted and all previously recorded ballots are preserved.<br>● POST-3: No Passed, NotPassed or Deferred conclusion is automatically recorded. (BR-099) |  |  |
| Normal Flow | Close Vote<br>1. The Chair selects Close voting on an Open Vote.<br>2. The system checks current access to the Vote, displays its current turnout and asks for confirmation.<br>3. The Chair confirms closing.<br>4. The system verifies the current Chair, Meeting InProgress or Ended and Vote Open. (BR-098)<br>5. The system stops accepting ballots, records the closing time and tallies the recorded ballots per option; percentages are based on the ballots cast. (BR-098)<br>6. Every recorded ballot is kept, and a secret Vote stays secret. (BR-096)<br>7. The system displays MSG229 – “Voting closed. The result is available.” and the result (UC-62b). The Chair may then record a conclusion (UC-67). |  |  |
| Alternative Flows | AF-1 – Close an outstanding Vote after Meeting end<br>1. At Step 1, the Meeting is Ended and this Vote is still Open.<br>2. No ballots have been accepted since the Meeting ended. End Meeting did not close the Vote or alter its ballots. (BR-026, BR-092, BR-093)<br>3. The flow resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Closing is no longer allowed<br>1. At Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Open. (BR-098)<br>2. The system displays MSG230 – "Only the current Chair can close an Open vote while the meeting is InProgress or Ended." The Vote, ballots and any existing result remain unchanged.<br>3. The use case ends.<br>EX-2 – Meeting or Vote is not accessible<br>1. Before displaying Vote information or completing the action, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. The system displays MSG205 – "Meeting votes are not accessible." without revealing hidden Vote information. No Vote, ballot or conclusion is changed.<br>3. The use case ends. |  |  |

#### 11.6 UC-67 – Record Vote Conclusion

| Primary Actors | Chair | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair to record the business conclusion of a Closed and tallied Vote as Passed, NotPassed or Deferred, with an optional note. The Chair decides the conclusion; the system records it rather than inferring it from a majority. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current authorized Chair of the Meeting.<br>● PRE-2: The Meeting is InProgress or Ended.<br>● PRE-3: The Vote is Closed and its tally is available. (BR-099)<br>● PRE-4: No conclusion has been recorded for this Vote. (BR-099) |  |  |
| Postconditions | ● POST-1: The selected conclusion, optional note, recording Chair and recording time are saved.<br>● POST-2: The conclusion and note cannot subsequently be edited or replaced.<br>● POST-3: The tally and conclusion are available for Meeting Minutes; the saved ballots and tally remain unchanged. |  |  |
| Normal Flow | Record Vote Conclusion<br>1. The Chair opens the result of a Closed Vote and reviews the tally.<br>2. The system checks current access, that the actor is the current Chair, and that the Vote is Closed without a conclusion. It then offers Passed, NotPassed and Deferred and an optional conclusion note. (BR-099)<br>3. The Chair selects one conclusion, optionally enters a note and confirms recording.<br>4. The system verifies the current Chair, Meeting InProgress or Ended, the Closed and tallied Vote, an allowed conclusion and that no conclusion has already been recorded. (BR-099)<br>5. The system records the conclusion and optional note together with the Chair and recording time. It does not change ballots or the tally. (BR-099)<br>6. The system displays MSG235 – "Vote conclusion recorded successfully." and makes the conclusion read-only. The tally and conclusion are available for Meeting Minutes. |  |  |
| Alternative Flows | AF-1 – Record after Meeting end<br>1. At Step 1, the Meeting is Ended and the Vote has been Closed and tallied, including a Vote closed after Meeting end.<br>2. The Chair records the outcome of that existing Vote. No new voting is opened or ballot accepted.<br>3. The flow resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Conclusion is invalid<br>1. At Step 4, no conclusion is selected or the selection is not Passed, NotPassed or Deferred. (BR-099)<br>2. No conclusion is recorded. The system displays MSG232 – "Select Passed, NotPassed or Deferred."<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-2 – Conclusion already recorded<br>1. At Step 2 or Step 4, the Vote already has a saved conclusion. (BR-099)<br>2. The existing conclusion, note and tally remain unchanged. The system displays MSG233 – "The vote conclusion has already been recorded and cannot be changed."<br>3. The use case ends.<br>EX-3 – Recording is no longer allowed<br>1. At Step 2 or Step 4, the actor is no longer the current Chair, the Meeting is not InProgress or Ended, or the Vote is not Closed with a tally available. (BR-099)<br>2. No conclusion is recorded and existing Vote data remains unchanged. The system displays MSG234 – "Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended."<br>3. The use case ends. |  |  |

#### 11.7 UC-68 – Manage Vote Documents

##### 11.7.1 UC-68a – Attach Documents to Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to attach one or more documents to a Draft Vote, so that eligible voters must confirm reading them before casting a ballot (UC-42, UC-65). Documents come from the Meeting documents, from the computer or from My Documents, as in UC-36b; documents not yet in the Meeting are added to it in the same save. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-070, BR-071, BR-086, BR-087)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: The selected Vote belongs to the Meeting and is Draft. (BR-094, BR-095) |  |  |
| Postconditions | ● POST-1: The selected documents are Vote documents of the Draft Vote; its other Vote documents remain unchanged.<br>● POST-2: Documents uploaded from the computer or chosen from My Documents are also added to the Meeting; uploaded files are also added to the uploader's personal library. If any document cannot be added or attached, nothing from the submission is added or attached.<br>● POST-3: The Vote content, options, ballot type and roles remain unchanged. The Vote document list is fixed when the Vote opens. (BR-094, BR-095) |  |  |
| Normal Flow | Attach Documents to Vote<br>1. The actor chooses to attach documents to a Draft Vote and chooses the Meeting documents as the source.<br>2. The system lists the Meeting documents not yet attached to this Vote.<br>3. The actor selects one or more documents and confirms.<br>4. The system checks again the actor's assignment, the Meeting status, that the Vote is still Draft and that every selected document is still in the Meeting and not yet attached to the Vote. (BR-070, BR-071, BR-086, BR-087, BR-094)<br>5. The system attaches all selected documents together; if one cannot be attached, none is.<br>6. The system displays MSG265 – “Documents attached to the vote successfully.” |  |  |
| Alternative Flows | AF-1 – Upload from computer<br>1. At Step 1, the actor chooses to upload files from the computer and submits them.<br>2. The system checks that every file is a PDF or DOCX no larger than 10 MB and performs the checks of Step 4 that apply. (BR-069, BR-070, BR-071, BR-094)<br>3. The system adds the files to the Meeting and to the uploader's personal library and attaches them to the Vote in the same save. (BR-072, BR-073)<br>4. If a file is invalid or cannot be added, nothing from the submission is added or attached and the system displays MSG164 – “Select valid PDF or DOCX files, each no larger than 10 MB.” Otherwise the flow resumes at Step 6 of the Normal Flow.<br>AF-2 – Choose from My Documents<br>1. At Step 1, the actor chooses My Documents and selects one or more documents from his or her personal library.<br>2. The system checks that each document belongs to the actor, is available and is not already in the Meeting, and performs the checks of Step 4. (BR-070, BR-071, BR-072, BR-073, BR-094)<br>3. The system adds the documents to the Meeting and attaches them to the Vote in the same save; the personal library is unchanged.<br>4. If a document cannot be added, nothing is added or attached and the system displays MSG249 – “Select one or more available documents from your personal library.” A document already in the Meeting is attached through the Normal Flow. Otherwise the flow resumes at Step 6 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Vote documents can no longer be changed<br>1. At Step 4, or the same check in AF-1 or AF-2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-086, BR-087, BR-094)<br>2. Nothing is added or attached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer in the Meeting<br>1. At Step 4, a selected document is no longer in the Meeting.<br>2. Nothing is attached. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”<br>3. The flow resumes at Step 2 of the Normal Flow.<br>EX-3 – Document is already attached<br>1. At Step 4, one or more selected documents are already attached to the Vote, including by another user after selection.<br>2. Nothing is attached and no duplicate is created. The system displays MSG266 – “One or more selected documents are already attached to the vote.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

##### 11.7.2 UC-68b – Detach Document from Vote

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or Secretary to detach one document from a Draft Vote. The document stays in the Meeting; removing it from the Meeting is UC-36c. |  |  |
| Preconditions | ● PRE-1: The actor is signed in, can view the Meeting and is its current Chair or Secretary. (BR-070, BR-071, BR-086, BR-087)<br>● PRE-2: The Meeting is Approved, Published or InProgress. (BR-086, BR-087)<br>● PRE-3: The document is a Vote document of a Draft Vote of the Meeting. (BR-094, BR-095) |  |  |
| Postconditions | ● POST-1: Only the selected Vote document is detached from the Vote. (BR-094, BR-095)<br>● POST-2: The document stays in the Meeting; its Agenda Item links, contents, comments and read confirmations remain unchanged. |  |  |
| Normal Flow | Detach Document from Vote<br>1. The actor selects a document of a Draft Vote, chooses Detach and confirms.<br>2. The system rechecks current Meeting access, the Chair or Secretary assignment, the permitted Meeting status, that the Vote is still Draft and that the document is still attached to it. (BR-070, BR-071, BR-086, BR-087, BR-094, BR-095)<br>3. The system detaches only that document from the Vote; the document stays in the Meeting.<br>4. The system refreshes the Vote documents and displays MSG268 – “Document detached from the vote successfully.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Vote documents can no longer be changed<br>1. At Step 2, the Vote is no longer Draft, the actor is no longer the current Chair or Secretary, or the Meeting is no longer Approved, Published or InProgress. (BR-086, BR-087, BR-094, BR-095)<br>2. Nothing is detached. The system displays MSG213 – “Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress.”<br>3. The use case ends.<br>EX-2 – Document is no longer attached to the Vote<br>1. At Step 2, the document has already been detached from the Vote or removed from the Meeting.<br>2. Nothing else changes. The system displays MSG269 – “This document is no longer attached to the vote. Refresh the list.”<br>3. The use case ends.<br>EX-3 – Meeting or Vote is not accessible<br>1. Before displaying or saving, the Meeting or Vote is unavailable or the actor no longer has access. A Draft Vote is not disclosed to anyone other than the current Chair and the current Secretary. (BR-086, BR-087)<br>2. Nothing is detached. The system displays MSG205 – "Meeting votes are not accessible."<br>3. The use case ends. |  |  |

### 12. FE-12 – Meeting Minutes & Approval

#### 12.1 UC-69 – View Meeting Minutes

| Primary Actors | Chair, Secretary; every other internal person who may read the Meeting | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to read the latest revision of the minutes, and every other internal person who may read the Meeting to read the latest issued revision. Drafts, revisions waiting for approval and returned revisions are seen by the current Chair and Secretary only. Issued and superseded revisions can be downloaded as a Word or PDF file, except for a Secret Meeting. (BR-131, BR-132, BR-139) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and may read the Meeting. (BR-139)<br>● PRE-2: The actor is its current Chair or current Secretary, and a revision of the minutes exists; or the actor is anyone else who may read the Meeting, and an issued revision exists. (BR-131, BR-139) |  |  |
| Postconditions | ● POST-1: The revision of the minutes the actor may read and its state are displayed.<br>● POST-2: No content, state or Meeting record is changed.<br>● POST-3: Nobody learns of a draft, a revision waiting for approval, a returned revision or a correction that is not issued yet. (BR-139) |  |  |
| Normal Flow | View Meeting Minutes<br>1. The actor opens the minutes of a Meeting that has Ended.<br>2. The system checks the actor's assignment or access to the Meeting and finds the revision the actor may read: the latest revision for the current Chair and Secretary, the latest issued revision for everyone else. (BR-131, BR-139)<br>3. The system shows the minutes as they will be printed.<br>4. The system shows the revision number, its status, who created it, when it was last saved, and who approved and issued it and when. The current Chair and Secretary also see who submitted or returned it and the reason of the latest return. (BR-135, BR-136)<br>5. When the minutes have several revisions, the actor may open another one: the current Chair and Secretary any revision, everyone else an issued or superseded one. (BR-138, BR-139)<br>6. The actor may download an issued or superseded revision as a Word or PDF file. (BR-139) |  |  |
| Alternative Flows | AF-1 – No minutes yet<br>1. At Step 2, no revision exists that the actor may read.<br>2. The current Chair or Secretary is offered to build the draft (UC-70); everyone else sees that no minutes have been issued yet.<br>3. The use case ends.<br>AF-2 – Read an earlier revision<br>1. At Step 5, the actor chooses an earlier revision.<br>2. The system shows the chosen revision with its own number and status; a revision replaced by a correction is shown as superseded and stays readable. (BR-137, BR-138)<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – Download an issued revision as a reader<br>1. At Step 6, the actor is neither the current Chair nor the current Secretary, and the revision is issued or superseded.<br>2. The system checks that the actor may read the Meeting, is not a Guest and that the Meeting is not Secret, and delivers the file built from the same content. (BR-132, BR-139)<br>3. The revision is unchanged. The use case ends. |  |  |
| Exception Flows | EX-1 – Revision is not readable by the actor<br>1. At Step 2 or Step 5, the actor is neither the current Chair nor the current Secretary and asks for a draft, a revision waiting for approval, a returned revision or a correction that is not issued yet. (BR-139)<br>2. The system answers as if the revision did not exist and discloses nothing about it.<br>3. The use case ends.<br>EX-2 – Meeting is not accessible<br>1. At Step 2, the Meeting is unavailable or the actor may no longer read it.<br>2. The system answers as if the Meeting did not exist and discloses nothing about its minutes.<br>3. The use case ends.<br>EX-3 – Minutes are Secret and download requested<br>1. At Step 6 or AF-3 Step 2, the Meeting is Secret. (BR-132)<br>2. No file is delivered; the minutes are read on the screen only. The system displays MSG405 – “The minutes of a Secret meeting cannot be downloaded.”<br>3. The use case ends. |  |  |

#### 12.2 UC-70 – Generate Meeting Minutes

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to build the draft of the minutes from the one fixed template and the Meeting's records, using the meeting's current AI draft, reviewed or not, for sections I–III when there is one. (BR-130) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The Meeting has Ended. (BR-131)<br>● PRE-3: No minutes exist yet, or the latest revision is a draft or was returned by the Chair. (BR-135, BR-136) |  |  |
| Postconditions | ● POST-1: A draft of the minutes exists, filled from the Meeting's records.<br>● POST-2: Building again replaces the whole content of the draft, including every correction.<br>● POST-3: No other Meeting record is changed. |  |  |
| Normal Flow | Generate Meeting Minutes<br>1. The actor asks to build the draft of the minutes.<br>2. The system checks the assignment, the Meeting status and that no minutes exist yet or the latest revision is a draft or was returned. (BR-131, BR-135, BR-136)<br>3. The system fills the fixed template from the Meeting's records — time, place, attendance, Chair, Secretary, agenda with the recorded conclusions, and the results and conclusions of closed votes — and fills sections I–III from the Meeting's current AI draft when there is one. (BR-130)<br>4. The system saves the draft with its revision number and displays MSG395 – “Minutes draft created.” |  |  |
| Alternative Flows | AF-1 – Build again over an existing draft<br>1. At Step 1, a draft or a returned revision already exists.<br>2. The system warns that every correction will be replaced and asks for confirmation.<br>3. The actor confirms and the flow resumes at Step 2 of the Normal Flow.<br>AF-2 – No AI draft<br>1. At Step 3, no AI draft exists yet, or the AI has not finished writing it.<br>2. The system leaves blanks where the Secretary must write.<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-3 – AI draft not yet reviewed<br>1. At Step 3, the Meeting's AI draft exists but has not been reviewed.<br>2. The system uses the draft as it is, marks the minutes as built from an unreviewed draft and offers the way to review it (UC-61).<br>3. The flow resumes at Step 4 of the Normal Flow.<br>AF-4 – AI draft changed after the minutes were built<br>1. At Step 1, the AI draft was made again or corrected after the minutes were built, or appeared after minutes built without one.<br>2. The system shows that the minutes are out of date and offers to build them again as in AF-1.<br>3. The use case ends. |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor is neither the current Chair nor the current Secretary. (BR-131)<br>2. No draft is built. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – Meeting has not ended<br>1. At Step 2, the Meeting has not Ended. (BR-131)<br>2. No draft is built. The system displays MSG398 – “The minutes can be written only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – A draft already exists<br>1. At Step 2, a draft exists and the actor did not ask to replace it.<br>2. Nothing is built and the existing draft stays as it is. The system displays MSG399 – “This meeting already has a minutes draft.”<br>3. The use case ends. |  |  |

#### 12.3 UC-71 – Edit Meeting Minutes

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to correct the content of the minutes draft, see the paper as it will print, save it, and download the same content as a Word or PDF file. |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The latest revision of the minutes is a draft or was returned by the Chair; a correction draft counts. (BR-135, BR-136) |  |  |
| Postconditions | ● POST-1: The corrected text is saved with the new time it was saved.<br>● POST-2: No other revision or Meeting record is changed.<br>● POST-3: Downloading a Word or PDF file leaves the draft unchanged. |  |  |
| Normal Flow | Edit Meeting Minutes<br>1. The actor opens the minutes draft and chooses to edit it.<br>2. The system shows the content in an editor next to the paper preview built from the same content; the preview follows the text as the actor types.<br>3. The actor corrects the text and saves it.<br>4. The system checks that the revision is still a draft or was returned, that the content is valid and that nobody has saved since the actor read it. (BR-135, BR-136)<br>5. The system saves the content with the new save time and displays MSG396 – “Minutes saved.” |  |  |
| Alternative Flows | AF-1 – Download the minutes as a Word file<br>1. At Step 1 or after saving, the actor downloads the minutes as a Word file. The current Chair and Secretary may download any revision; everyone else who may read the Meeting an issued or superseded one. (BR-139)<br>2. The system checks that the actor may download that revision and that the Meeting is not Secret, and delivers the Word file built from the same content. (BR-132)<br>3. The revision is unchanged. The use case ends.<br>AF-2 – Download the minutes as a PDF file<br>1. At Step 1 or after saving, the actor downloads the minutes as a PDF file, with the same rights as in AF-1. (BR-139)<br>2. The system checks that the actor may download that revision and that the Meeting is not Secret, and delivers the PDF file built from the same content. (BR-132)<br>3. The revision is unchanged. The use case ends. |  |  |
| Exception Flows | EX-1 – Someone else saved first<br>1. At Step 4, the draft was saved by another person after the actor read it.<br>2. Nothing is overwritten. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – Content is invalid<br>1. At Step 4, the content is empty, longer than 300,000 characters or not well formed.<br>2. Nothing is saved. The system displays MSG402 – “The minutes content is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow.<br>EX-3 – Minutes can no longer be changed<br>1. At Step 4, the revision is waiting for approval or has been issued.<br>2. Nothing is saved. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends. |  |  |

#### 12.4 UC-72 – Submit Meeting Minutes for Approval

| Primary Actors | Secretary | Secondary Actors | Chair (notified) |
| --- | --- | --- | --- |
| Description | Allows the current Secretary to send the finished minutes to the current Chair for approval. From then on nobody can correct or rebuild the revision until the Chair approves or returns it. The Secretary cannot take back a revision once it is waiting for approval (out of scope, PO 11/11/2026). (BR-135, BR-136) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Secretary of the Meeting. (BR-135, BR-136)<br>● PRE-2: The Meeting has Ended. (BR-135, BR-136)<br>● PRE-3: The latest revision of the minutes is a draft or was returned by the Chair. (BR-135, BR-136) |  |  |
| Postconditions | ● POST-1: The revision is waiting for approval, with who submitted it and when.<br>● POST-2: The revision can no longer be corrected or rebuilt.<br>● POST-3: The current Chair is notified.<br>● POST-4: The Meeting status is unchanged. (BR-137, BR-138)<br>● POST-5: If the submission fails, nothing changes and nobody is notified. |  |  |
| Normal Flow | Submit Meeting Minutes for Approval<br>1. The actor opens the minutes, chooses to submit them for approval and confirms.<br>2. The system checks the assignment, that the Meeting has Ended, that the latest revision is a draft or was returned, and that it is the copy the actor read. (BR-135, BR-136)<br>3. The system marks the revision as waiting for approval, records who submitted it and when, and notifies the current Chair.<br>4. The system displays MSG424 – “Minutes submitted to the Chair for approval.” |  |  |
| Alternative Flows | AF-1 – Places are still left blank<br>1. At Step 1, the text still holds “……” where the Secretary was to write.<br>2. The system warns that some places are still blank and asks for confirmation; blanks do not block the submission.<br>3. The actor confirms and the flow resumes at Step 2 of the Normal Flow.<br>AF-2 – Submit again after the Chair returned the minutes<br>1. At Step 1, the latest revision was returned; the system shows the Chair's reason with the minutes. (BR-135, BR-136)<br>2. The actor corrects the text (UC-71) or submits it unchanged.<br>3. The flow resumes at Step 2 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is the current Chair, not the Secretary<br>1. At Step 2, the actor is the current Chair but not the current Secretary. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG425 – “Only the Secretary may submit the minutes for approval.”<br>3. The use case ends.<br>EX-2 – Meeting has not ended<br>1. At Step 2, the Meeting is still InProgress. (BR-135, BR-136)<br>2. The revision stays a draft. The system displays MSG426 – “The minutes can be submitted only after the meeting has ended.”<br>3. The use case ends.<br>EX-3 – Revision was already submitted or issued<br>1. At Step 2, the latest revision is waiting for approval or has been issued.<br>2. Nothing changes. The system displays MSG400 – “The minutes have been submitted or issued and can no longer be changed.”<br>3. The use case ends. |  |  |

#### 12.5 UC-73 – Approve and Issue Meeting Minutes

| Primary Actors | Chair | Secondary Actors | Secretary, and everyone who may read the issued minutes (notified) |
| --- | --- | --- | --- |
| Description | Allows the current Chair to approve the minutes waiting for approval. Approval issues them at once: they are locked, the people who may read the Meeting are notified, and the issue is recorded in the audit log. (BR-135, BR-136, BR-137, BR-138, BR-010) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-135, BR-136)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |  |  |
| Postconditions | ● POST-1: The revision is issued, with who approved it and when, and can never be changed again. (BR-137, BR-138)<br>● POST-2: When the revision corrects an earlier issued one, the earlier one is marked superseded and stays readable. (BR-137, BR-138)<br>● POST-3: Everyone who may read the issued minutes except the Chair who issued them is notified. (BR-139)<br>● POST-4: The issue is recorded in the audit log with who issued it. (BR-010)<br>● POST-5: The Meeting stays Ended. (BR-137, BR-138) |  |  |
| Normal Flow | Approve and Issue Meeting Minutes<br>1. The actor opens the minutes waiting for approval, from the notification or the Meeting, and reads them as they will be printed.<br>2. The actor chooses to approve and issue them and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval and that it is the copy the actor read. (BR-135, BR-136)<br>4. The system marks the revision as issued and records who approved it and when. (BR-137, BR-138)<br>5. The system records the issue in the audit log with who issued it. (BR-010)<br>6. The system notifies everyone who may read the issued minutes, except the Chair who issued them and Guests, in the application and by email; a correction states its number. (BR-139)<br>7. The system displays MSG427 – “Minutes approved and issued.” |  |  |
| Alternative Flows | AF-1 – Issue a correction<br>1. At Step 4, the revision corrects an earlier issued revision.<br>2. The system marks the earlier revision as superseded; it stays readable in the list of revisions. (BR-137, BR-138)<br>3. The flow resumes at Step 5 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – Minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has already been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – Minutes changed after the actor opened them<br>1. At Step 3, the revision is not the copy the actor read.<br>2. Nothing is issued. The system displays MSG401 – “The minutes were just changed by someone else. Reload to see the latest version.”<br>3. The flow resumes at Step 1 of the Normal Flow. |  |  |

#### 12.6 UC-74 – Return Meeting Minutes for Revision

| Primary Actors | Chair | Secondary Actors | Secretary (notified) |
| --- | --- | --- | --- |
| Description | Allows the current Chair to send the minutes waiting for approval back to the Secretary with a reason, so the Secretary can correct them and submit them again. Each return keeps only its own reason. (BR-135, BR-136) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair of the Meeting. (BR-135, BR-136)<br>● PRE-2: The latest revision of the minutes is waiting for approval. |  |  |
| Postconditions | ● POST-1: The revision is returned, with the reason, who returned it and when. (BR-135, BR-136)<br>● POST-2: The reason of this return replaces the reason of an earlier one. (BR-135, BR-136)<br>● POST-3: The current Chair and the current Secretary can correct the revision again and the Secretary can submit it again. (BR-135, BR-136)<br>● POST-4: The current Secretary is notified. If the return fails, nothing changes and nobody is notified. |  |  |
| Normal Flow | Return Meeting Minutes for Revision<br>1. The actor chooses to return the minutes waiting for approval.<br>2. The system asks for the reason; the actor enters it and confirms.<br>3. The system checks the assignment, that the revision is still waiting for approval, that it is the copy the actor read and that the reason is not empty and within 2,000 characters. (BR-135, BR-136)<br>4. The system marks the revision as returned and records the reason, who returned it and when; the reason of any earlier return is replaced. (BR-135, BR-136)<br>5. The system notifies the current Secretary.<br>6. The system displays MSG430 – “Minutes returned to the Secretary.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is the current Secretary, not the Chair<br>1. At Step 3, the actor is the current Secretary but not the current Chair. (BR-135, BR-136)<br>2. The revision stays as it is. The system displays MSG428 – “Only the Chair may approve or return the minutes.”<br>3. The use case ends.<br>EX-2 – Minutes are not waiting for approval<br>1. At Step 3, the revision is a draft, was returned, or has been issued.<br>2. Nothing changes. The system displays MSG429 – “These minutes are not waiting for approval.”<br>3. The use case ends.<br>EX-3 – Reason is missing or too long<br>1. At Step 3, the reason is empty or longer than 2,000 characters. (BR-135, BR-136)<br>2. Nothing changes. The system displays MSG431 – “Enter a reason for returning the minutes, up to 2,000 characters.”<br>3. The flow resumes at Step 2 of the Normal Flow. |  |  |

#### 12.7 UC-75 – Correct Issued Meeting Minutes

| Primary Actors | Chair, Secretary | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows the current Chair or the current Secretary to start a correction of issued minutes. The correction is a new revision that starts from the issued text; the issued revision is never changed and stays the one people read until the correction is issued. (BR-137, BR-138) |  |  |
| Preconditions | ● PRE-1: The actor is signed in and is the current Chair or the current Secretary of the Meeting. (BR-131)<br>● PRE-2: The selected revision of the minutes is issued. (BR-137, BR-138) |  |  |
| Postconditions | ● POST-1: A new draft revision exists, numbered after the issued one, holding the issued text and pointing at the revision it corrects. (BR-137, BR-138)<br>● POST-2: The issued revision is unchanged and stays the one other people read. (BR-137, BR-138)<br>● POST-3: Nobody is notified until the correction is issued (UC-73). |  |  |
| Normal Flow | Correct Issued Meeting Minutes<br>1. The actor chooses to create a correction of the issued revision and confirms.<br>2. The system checks the assignment, that the selected revision is issued and that no later revision of these minutes is still unissued. (BR-131, BR-137, BR-138)<br>3. The system creates the new draft revision from the issued text, numbered after it. (BR-137, BR-138)<br>4. The system shows the correction draft, ready to be edited (UC-71), and displays MSG432 – “Correction draft created.” |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Actor is not the current Chair or Secretary<br>1. At Step 2, the actor may read the selected revision (it is issued or superseded) but is neither the current Chair nor the current Secretary. (BR-131, BR-139)<br>2. No revision is created. The system displays MSG397 – “Only the Chair or Secretary may write the minutes.”<br>3. The use case ends.<br>EX-2 – Selected revision is not issued<br>1. At Step 2, the selected revision is a draft, waiting for approval, returned, or superseded by a later issued revision. (BR-137, BR-138)<br>2. No revision is created. The system displays MSG433 – “Only issued minutes can be corrected.”<br>3. The use case ends.<br>EX-3 – A correction is already in progress<br>1. At Step 2, the selected revision is issued, but a later revision correcting it has not been issued yet, or a second actor starts a correction of the same revision at the same moment. (BR-137, BR-138)<br>2. No second revision is created. The system displays MSG434 – “A correction of these minutes is already in progress.”<br>3. The use case ends. |  |  |

### 13. FE-13 – Email & In-App Notification

#### 13.1 UC-76 – View Notifications

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authenticated user to view their list of in-app notifications and check the unread notification count. |  |  |
| Preconditions | ● PRE-1: The user is authenticated in the system. |  |  |
| Postconditions | ● POST-1: The system displays the user's notifications and unread badge count.<br>● POST-2: Opening the notification list does not change any notification’s read status. |  |  |
| Normal Flow | View Notifications<br>1. The user selects the notification bell from the navigation header.<br>2. The system retrieves the notifications and unread badge count for the user's account.<br>3. The system displays the user's notifications, indicates which ones are unread, and shows the unread count. Notifications are kept for later viewing; a new notification and the new unread count appear while the user is signed in. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | N/A |  |  |

#### 13.2 UC-77 – Mark Notifications as Read

##### 13.2.1 UC-77a – Mark Notification as Read

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows an authenticated user to mark a specific unread notification as read, updating its status. |  |  |
| Preconditions | ● PRE-1: The user is authenticated in the system.<br>● PRE-2: The user has at least one unread notification. |  |  |
| Postconditions | ● POST-1: The selected notification status is changed to Read.<br>● POST-2: The unread count reflects the latest saved notification statuses. |  |  |
| Normal Flow | Mark Notification as Read<br>1. The user views their notification list.<br>2. The user selects "Mark as Read" for a notification.<br>3. The system verifies that the notification belongs to the user's account. (BR-033, BR-034)<br>4. The system marks the notification as read.<br>5. The system displays the notification as read and shows the updated unread count. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | N/A |  |  |

##### 13.2.2 UC-77b – Mark All Notifications as Read

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user to mark all of their unread notifications as read. |  |  |
| Preconditions | ● PRE-1: The user is signed in. |  |  |
| Postconditions | ● POST-1: All notifications belonging to the user's account that were unread when the request was processed are marked as read.<br>● POST-2: The displayed unread count reflects the latest saved state. |  |  |
| Normal Flow | Mark All Notifications as Read<br>1. The user selects the notification bell and opens the notification list.<br>2. The user selects "Mark All as Read".<br>3. The system marks all unread notifications belonging to the user's account as read, including those not currently displayed. (BR-033, BR-034)<br>4. The system refreshes the displayed read statuses and unread count. |  |  |
| Alternative Flows | AF-1 – No unread notifications<br>1. At Step 3 of the Normal Flow, the system finds no unread notifications belonging to the user's account.<br>2. The system displays the current list and an unread count of zero.<br>3. The use case ends. |  |  |
| Exception Flows | N/A |  |  |

#### 13.3 UC-78 – Open Notification Target

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | None |
| --- | --- | --- | --- |
| Description | Allows a user to open the meeting linked to one of their notifications. |  |  |
| Preconditions | ● PRE-1: The user is signed in. |  |  |
| Postconditions | ● POST-1: The user sees the linked meeting.<br>● POST-2: The selected notification is marked as read; opening the meeting does not depend on that. |  |  |
| Normal Flow | Open Notification Target<br>1. The user opens the notification list and selects a notification.<br>2. The system checks that the notification belongs to the user's account and finds its linked meeting. (BR-033, BR-034)<br>3. The system marks the notification as read; if the read status cannot be saved, the meeting still opens.<br>4. The system checks whether the user currently has permission to view the linked meeting. (BR-016, BR-017, BR-033, BR-034)<br>5. The system opens the meeting. |  |  |
| Alternative Flows | AF-1 – Notification is already read<br>1. At Step 3 of the Normal Flow, the notification is already read, so its status and the unread count remain unchanged.<br>2. The flow resumes at Step 4 of the Normal Flow. |  |  |
| Exception Flows | N/A |  |  |

### 15. FE-15 – Reporting & Statistics

#### 15.1 UC-79 – View & Export Reports

##### 15.1.1 UC-79a – View Reports & Statistics

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | N/A |
| --- | --- | --- | --- |
| Description | Allows a signed-in internal user to see aggregate figures about the meetings they may read over a chosen period — meetings per month, by status and by sensitivity, the average attendance rate, the average meeting duration in minutes (BR-151) and the average time from the end of a meeting to the issue of its minutes — so that meeting activity can be followed without opening each meeting. The page then lets the actor query the main tables (meetings, participants, votes, documents, minutes, recordings) by choosing the table, the grouping, the top groups, the filters and the ordering, with a Total row. (BR-149, BR-150, BR-152, BR-151, BR-153) |  |  |
| Preconditions | ● PRE-1: The actor is signed in with an internal account. (BR-149, BR-150) |  |  |
| Postconditions | ● POST-1: The actor sees the figures of the chosen period, computed only from the Meetings the actor may read. (BR-149, BR-150)<br>● POST-2: The manager figures show no meeting title, participant or content. In the queried table, a row grouped by person or by meeting shows only the person’s name or the meeting code and title — only for meetings the actor may read — and never the meeting content; a Guest appears by name only, without email. (BR-152)<br>● POST-3: Nothing in the system is changed. |  |  |
| Normal Flow | View Reports & Statistics<br>1. The actor opens the Báo cáo [Reports] page.<br>2. The system selects the 12 months ending with the current month and the default table with its default grouping.<br>3. The system checks the period and the actor's choice of table, grouping, ordering, number of groups and filters.<br>4. The system counts the Meetings the actor may read whose planned start falls within the period and shows them per month, by status and by sensitivity, with the average attendance rate, the average meeting duration in minutes and the average time from the end of a Meeting to the issue of its minutes, each with the number of Meetings it is based on. (BR-149, BR-150, BR-151)<br>5. The system shows the chosen table with only what the actor may see, grouped, ordered and limited to the chosen top groups, with a Total row. (BR-151, BR-153) |  |  |
| Alternative Flows | AF-1 – Choose another period<br>1. At Step 2 or after Step 5, the actor chooses a start month and an end month.<br>2. The flow resumes at Step 3 of the Normal Flow with that period.<br>AF-2 – No Meeting in the period<br>1. At Step 4, the actor may read no Meeting whose planned start falls within the period.<br>2. The system shows that there are no meetings and displays MSG466 – “There are no meetings you can view in this period.” No all-zero tables are shown.<br>3. The use case ends.<br>AF-3 – An average has no data<br>1. At Step 4, no Ended Meeting in the period had attendance taken, none has issued minutes, or none has both an actual start and an actual end.<br>2. The system shows that average as having no data instead of a number. (BR-151)<br>3. The use case ends.<br>AF-4 – Choose another table, grouping or filter<br>1. At Step 2 or after Step 5, the actor chooses another table, grouping, ordering, number of groups or filter.<br>2. The flow resumes at Step 3 of the Normal Flow with that choice. |  |  |
| Exception Flows | EX-1 – The period is not valid<br>1. At Step 3, the start month is after the end month, or the period is longer than 24 months.<br>2. No figure is computed. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The flow resumes at Step 1 of AF-1.<br>EX-2 – The report choice is not valid<br>1. At Step 3, the chosen table, grouping, ordering, number of groups or filter is not available for that table.<br>2. Nothing is read. The system displays MSG484 – “The report parameters are invalid. Choose the data, grouping or filter again.”<br>3. The use case ends. |  |  |

##### 15.1.2 UC-79b – Export Report

| Primary Actors | System Admin, Member, Approver, Chair, Secretary, Participant | Secondary Actors | N/A |
| --- | --- | --- | --- |
| Description | Allows the actor to download the table shown on the page for the chosen period as an Excel file, to keep it or to work on it outside the system. The file is named bao-cao-<table>_<from>_<to>.xlsx. (BR-010) |  |  |
| Preconditions | ● PRE-1: The actor is signed in with an internal account. (BR-149, BR-150)<br>● PRE-2: The actor is on the Báo cáo page with a period and a table chosen. (UC-79a) |  |  |
| Postconditions | ● POST-1: The actor receives an Excel file holding the same table as the page for that period and that query, with its Total row, computed at the time of export. (BR-010)<br>● POST-2: The file holds no meeting content; rows grouped by person or by meeting follow the same rule as UC-79a POST-2. (BR-152)<br>● POST-3: The export is recorded with the actor, the table, the grouping, the filters, the period and the time. (BR-010) |  |  |
| Normal Flow | Export Report<br>1. The actor selects Xuất Excel [Export to Excel].<br>2. The system checks the period and the choices of the table shown.<br>3. The system builds the Excel file of that table with its Total row and records the export. (BR-152, BR-010)<br>4. The actor receives the file. |  |  |
| Alternative Flows | AF-1 – No Meeting in the period<br>1. At Step 2, the actor may read no Meeting in the period.<br>2. The file is still built, with zero counts and no averages.<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – The period is not valid<br>1. At Step 2, the period is not valid.<br>2. No file is built. The system displays MSG463 – “The report period is invalid. Choose up to 24 months, with the start month not after the end month.”<br>3. The use case ends.<br>EX-2 – The report choice is not valid<br>1. At Step 2, the chosen table, grouping, ordering, number of groups or filter is not available for that table.<br>2. No file is built. The system displays MSG484 – “The report parameters are invalid. Choose the data, grouping or filter again.”<br>3. The use case ends. |  |  |

### 16. FE-16 – Working Calendar & Meeting Time Search

#### 16.1 UC-80 – Manage Calendar Sharing

##### 16.1.1 UC-80a – Request Calendar Share

| Primary Actors | Member (the person who will create the meeting) | Secondary Actors | User — the calendar owner (every active internal account except the requester) |
| --- | --- | --- | --- |
| Description | Allows a Member to ask another user to share working hours as busy/free, so that free slots can be found without asking each person. The owner answers in UC-80c. (BR-140, BR-141, BR-142, BR-143) |  |  |
| Preconditions | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141)<br>● PRE-2: No share of the owner’s calendar to the actor is Pending (Chờ đồng ý) or Effective (Đang hiệu lực). (BR-142, BR-143) |  |  |
| Postconditions | ● POST-1: A Pending share exists and the owner is notified. (BR-142, BR-143, BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |  |  |
| Normal Flow | Request Calendar Share<br>1. The actor chooses the calendar owner among the other active internal accounts, a level (Mốc 1 or Mốc 2) and a date range; the request can also start from a person shown as not sharing in UC-81a.<br>2. The system checks that the actor is still a Member (else EX-1), that the owner is an active internal account different from the actor, that no Pending or Effective share of the owner’s calendar to the actor exists (else EX-2) and that the range is valid (else EX-3). A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends. (BR-140, BR-141, BR-142, BR-143)<br>3. The system saves the Pending share, records it in the audit log and notifies the owner. (BR-010) The system displays MSG435 – “Calendar share request sent.” The owner answers in UC-80c. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Requester has no right to create meetings<br>1. At Step 2, the actor is no longer a Member. (BR-140, BR-141)<br>2. Nothing is saved. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the owner’s calendar to the actor already exists. (BR-142, BR-143)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 2, the date range is invalid.<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends. |  |  |

##### 16.1.2 UC-80b – Share Calendar Directly

| Primary Actors | User — the calendar owner (every active internal account) | Secondary Actors | Member — the recipient |
| --- | --- | --- | --- |
| Description | Allows a calendar owner to share working hours directly with a Member, always at Mốc 1. The recipient answers in UC-80c; the recipient asks for Mốc 2 through UC-80d after accepting. (BR-140, BR-141, BR-142, BR-143) |  |  |
| Preconditions | ● PRE-1: The actor is signed in with an active internal account. (BR-140, BR-141)<br>● PRE-2: No share of the actor’s calendar to the recipient is Pending (Chờ đồng ý) or Effective (Đang hiệu lực). (BR-142, BR-143) |  |  |
| Postconditions | ● POST-1: A Pending share at Mốc 1 exists and the recipient is notified. (BR-140, BR-141, BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141) |  |  |
| Normal Flow | Share Calendar Directly<br>1. The actor chooses a recipient who is a Member and a date range; the level is always Mốc 1.<br>2. The system checks the recipient (active account, still a Member — else EX-1), that no Pending or Effective share of the actor’s calendar to the recipient exists (else EX-2) and that the range is valid (else EX-3). A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends. (BR-140, BR-141, BR-142, BR-143)<br>3. The system saves the Pending share at Mốc 1, records it in the audit log and notifies the recipient. (BR-010) The system displays MSG441 – “Calendar shared. Waiting for acceptance.” The recipient answers in UC-80c. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Recipient cannot receive a share<br>1. At Step 2, the recipient is not a Member or is not active.<br>2. Nothing is saved. The system displays MSG455 – “This person cannot receive a calendar share.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the actor’s calendar to the recipient already exists. (BR-142, BR-143)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 2, the date range is invalid.<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends. |  |  |

##### 16.1.3 UC-80c – Respond to Calendar Share

| Primary Actors | User — the asked side (the calendar owner, for a UC-80a request or a UC-80d change; the receiving Member, for a UC-80b direct share) | Secondary Actors | The sender (receives a notification about the answer) |
| --- | --- | --- | --- |
| Description | Allows the asked person to accept or decline a calendar share request, a direct share or a pending change. Accepting a UC-80a request may lower the level or shorten the range. A Pending request whose sender’s account is deactivated can only be declined (BR-142, BR-143). (BR-140, BR-141, BR-142, BR-143, BR-010) |  |  |
| Preconditions | ● PRE-1: The actor is the asked side of a Pending (Chờ đồng ý) share or pending change.<br>● PRE-2: The share or change is still Pending. (BR-142, BR-143) |  |  |
| Postconditions | ● POST-1: The share is Effective (Đang hiệu lực) with the chosen level/range, or closed, or kept as it was for a declined change; the sender is notified in every case. (BR-010)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-140, BR-141)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |  |  |
| Normal Flow | Respond to Calendar Share<br>1. The actor opens the Pending request, from the notification or the list of shares, and sees who sent it, the level and the date range.<br>2. The system checks that the actor is the asked side (else EX-1), that the share is still Pending (else EX-2). An Effective share whose viewer lost the Member right was stopped at that moment; a Pending one is kept and can only be declined (BR-142, BR-143).<br>3. On accept the system marks the share Effective — for a UC-80a request the owner may choose a lower level or a shorter range within the requested one (BR-140, BR-141) — records it in the audit log and notifies the sender. The system displays MSG436 – “Calendar share accepted.”<br>4. On decline, after confirming, the system closes the share, records it in the audit log and notifies the sender. The system displays MSG437 – “Calendar share request declined.” |  |  |
| Alternative Flows | AF-1 – Answer a pending change (UC-80d)<br>1. At Step 3, the pending item is a change (raise to Mốc 2 or extend the range) instead of a new share.<br>2. On approval, the system checks that the requested range still fits the current start date; if it does not, the change cannot be approved (MSG450 – “This action is not allowed in the current state of the share”). Otherwise the system applies the new level or range, records it in the audit log, notifies the viewer and displays MSG442 – “Calendar share updated.”<br>3. On decline, everything stays as it was and both sides are notified. The system displays MSG452 – “Calendar share change declined.” |  |  |
| Exception Flows | EX-1 – Actor is not the asked side<br>1. At Step 2, the actor is not the asked side of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Share is no longer Pending<br>1. At Step 2, the share or change is no longer Pending, or its end date has passed (BR-142, BR-143), or the viewer lost the Member right (BR-140, BR-141). (BR-142, BR-143)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 3, the owner shortens the range to an invalid range.<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |

##### 16.1.4 UC-80d – Change Calendar Share

| Primary Actors | Member (the viewer) and User — the calendar owner (depending on who acts) | Secondary Actors | The other side of the share (receives a notification; answers a pending change in UC-80c) |
| --- | --- | --- | --- |
| Description | Allows the viewer to ask for a higher level or a longer range (answered in UC-80c); the owner lowers the level or changes the dates at once. A stopped share shows its state and reveals no hours; a share past its end date is no longer listed (BR-142, BR-143). (BR-142, BR-143, BR-010) |  |  |
| Preconditions | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-140, BR-141)<br>● PRE-2: The share is Effective (Đang hiệu lực); a Pending (Chờ đồng ý) share is withdrawn in UC-80e and asked for again instead. (BR-142, BR-143) |  |  |
| Postconditions | ● POST-1: The share holds a pending change, or the new level/range; the other side is notified in both cases. (BR-010)<br>● POST-2: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |  |  |
| Normal Flow | Change Calendar Share<br>1. The viewer opens their share and asks for Mốc 2 (raise) or a longer range (extend); only one pending change at a time. A longer range may keep the share's original start date and still covers at most 365 days counting both ends.<br>2. The system saves the pending change, audits and notifies the owner. MSG454 – “Calendar share change request sent.” The use case ends; the owner’s answer is UC-80c. |  |  |
| Alternative Flows | AF-1 – The owner lowers the level or changes the dates<br>1. At any time on an Effective share, the owner chooses a lower level or a new date range, even while the viewer has a change pending; that change stays pending and the owner still answers it in UC-80c.<br>2. The system applies it at once, audits and notifies the viewer. MSG442 – “Calendar share updated.” |  |  |
| Exception Flows | EX-1 – Actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Change is not allowed<br>1. At Step 1 (or AF-1 Step 1), the share is not Effective, or its end date has passed (BR-142, BR-143), or — at Step 1 only — the viewer already has a change pending. (BR-142, BR-143)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – Range is invalid<br>1. At Step 1 (or AF-1 Step 1), the new date range is invalid.<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes at Step 1 of the Normal Flow, or at Step 1 of AF-1. |  |  |

##### 16.1.5 UC-80e – Withdraw or End Calendar Share

| Primary Actors | Member (the viewer) and User — the calendar owner (depending on who acts) | Secondary Actors | The other side of the share (receives a notification) |
| --- | --- | --- | --- |
| Description | Allows the sender to withdraw a Pending share or a pending change; either side ends an Effective share. A stopped share stays listed with its state and reveals no hours; a share past its end date is no longer listed. (BR-142, BR-143, BR-010) |  |  |
| Preconditions | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-140, BR-141)<br>● PRE-2: Withdrawing needs a Pending (Chờ đồng ý) share or change; ending needs an Effective (Đang hiệu lực) share. (BR-142, BR-143) |  |  |
| Postconditions | ● POST-1: The request is closed (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), or the share is ended; the other side is notified in every case. (BR-010)<br>● POST-2: A stopped share reveals no busy hours and never comes back; viewing again needs a new share. (BR-142, BR-143)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |  |  |
| Normal Flow | Withdraw Calendar Share<br>1. The side that sent the request or the change confirms and withdraws it.<br>2. The system closes the request (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), audits and notifies the other side. MSG438 – “Calendar share request withdrawn.” |  |  |
| Alternative Flows | AF-1 – Either side ends an Effective share (“Ngừng chia sẻ” / “Thôi xem lịch”)<br>1. Either side confirms and ends the share.<br>2. The system closes it, audits and notifies the other side. Hours already found are not removed from anyone’s screen, but no new lookup uses this share. MSG451 – “Calendar sharing ended.” |  |  |
| Exception Flows | EX-1 – Actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Nothing to withdraw or end<br>1. At Step 1 (or AF-1 Step 1), there is nothing Pending to withdraw, or the share is not Effective to end, or its end date has passed (BR-142, BR-143).<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends. |  |  |

##### 16.1.6 UC-80f – View Calendar Shares

| Primary Actors | User — the calendar owner and, when holding the Member right, the viewer | Secondary Actors | The other side of each share |
| --- | --- | --- | --- |
| Description | Allows a signed-in user to see the calendar shares they take part in: who can see their calendar (“Người xem được lịch của tôi”) and, for a Member, who shared with them (“Lịch được chia sẻ với tôi”). Each share shows its state and only the actions the user can take now; the actions themselves are UC-80a to UC-80e. (BR-142, BR-143, BR-140, BR-141) |  |  |
| Preconditions | ● PRE-1: The actor is signed in with an active internal account. (BR-140, BR-141) |  |  |
| Postconditions | ● POST-1: The actor sees only shares where they are one of the two people; nothing about other people’s shares is disclosed. (BR-142, BR-143)<br>● POST-2: A stopped (Đã dừng) share stays listed and reveals no hours; a share past its end date is not listed. (BR-142, BR-143) |  |  |
| Normal Flow | View Calendar Shares<br>1. The actor opens the list of calendar shares.<br>2. The system checks that the actor is an active internal account, loads the shares where the actor is the owner and, if the actor holds the Member right, the shares where the actor is the viewer; it leaves out every share whose end date is before today. (BR-140, BR-141, BR-142, BR-143)<br>3. The system shows the two lists, open shares (Pending, Effective) before closed ones (Declined, Withdrawn, Stopped, Ended), newest first. Each share shows the other person, the level, the date range, its state, any pending change and only the actions the actor may take now. (BR-142, BR-143)<br>4. A Pending request whose sender was deactivated can only be declined. Opening a stopped share shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” (BR-142, BR-143) |  |  |
| Alternative Flows | AF-1 – Nobody has shared yet<br>1. At Step 2, the actor has no share in either list.<br>2. The system shows empty lists and offers to ask for a share (UC-80a) or to share directly (UC-80b). The use case ends.<br>AF-2 – The actor is not a member<br>1. At Step 2, the actor does not hold the Member right.<br>2. The system shows only the list of people who can see the actor's calendar, and asking for a share is not offered. The actor can still answer, change or end the shares of their own calendar and share directly. (BR-140, BR-141; PO 10/11/2026)<br>AF-3 – Open a share link whose row is gone<br>1. At Step 1, the actor opens a share from a notification, but that share is not in the lists.<br>2. The system reloads both lists once; if the share is still missing, it shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” when the viewer lost the Member right, otherwise that the share no longer exists. The use case ends.<br>AF-4 – A share changes while viewing<br>1. While the actor views either list, someone answers, changes or ends a share with the actor.<br>2. The actor sees the change without reloading the page.<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | N/A |  |  |

#### 16.2 UC-81 – Find Meeting Time

##### 16.2.1 UC-81a – Find Meeting Time Slots

| Primary Actors | Member (will become the Secretary of the meeting created in UC-81b) | Secondary Actors | User — the invitees (a person who has not shared is shown as not sharing) |
| --- | --- | --- | --- |
| Description | Allows a Member to find slots where everyone invited is free, using only shared calendars. From anyone shown “Chưa được chia sẻ lịch”, the finder may start UC-80a. (BR-140, BR-141, BR-144, BR-145, BR-146, BR-147, BR-148) |  |  |
| Preconditions | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141) |  |  |
| Postconditions | ● POST-1: The actor sees common slots by day, or — when none fits everyone — the slots with the most people free and who is busy. Attendee names come from the actor’s own invite list; no meeting titles, locations, attendee lists or sensitivity levels are shown. (BR-144, BR-145, BR-140, BR-141)<br>● POST-2: Days outside a share and people who never shared show “unknown” / “Chưa được chia sẻ lịch”, never free. (BR-144, BR-145) |  |  |
| Normal Flow | Find Meeting Time Slots<br>1. The actor opens the meeting-time search and enters the invitees (at most 10 people besides the actor, optionally counting the actor's own calendar), the duration (15 minutes to 8 hours), the date range (starting today, no more than 365 days ahead and at most 31 days long), the weekdays, the working hours with a lunch break, the buffer kept around every busy period (0 to 4 hours) and the rounding step for start times (5 minutes to 2 hours).<br>2. The system checks the search values.<br>3. The system builds each invitee's busy hours from the Effective shares of that invitee's calendar to the actor, at shared levels only, adds the actor's own Meetings when the actor asked to be counted, keeps the buffer around every busy period and never proposes a start time in the past; days outside a share are unknown. (BR-140, BR-141, BR-142, BR-143, BR-144, BR-145, BR-146, BR-147)<br>4. The system lists the common slots by day. |  |  |
| Alternative Flows | AF-1 – No slot fits everyone<br>1. At Step 4, no slot fits all invitees.<br>2. The system shows the slots with the most people free, with who is busy, per day. MSG445 – “No time slot fits everyone. Showing the slots with the most people free.” |  |  |
| Exception Flows | EX-1 – Search parameters are invalid<br>1. At Step 2, the search values are outside the limits of Step 1 (too many people, bad duration, range over 31 days, bad hours/buffer/step).<br>2. Nothing is searched. The system displays MSG444 – “The meeting-time search is invalid. Check the highlighted fields.”<br>3. The flow resumes at Step 1 of the Normal Flow.<br>EX-2 – Actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Member.<br>2. Nothing is searched. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |  |  |

##### 16.2.2 UC-81b – Create Draft Meeting from Slot

| Primary Actors | Member (the finder in UC-81a, will become the Secretary of the created meeting) | Secondary Actors | User — the invitees in the chosen slot |
| --- | --- | --- | --- |
| Description | Allows a Member to create a Draft meeting from a slot found in UC-81a, reusing the UC-13 creation flow. A slot that overlaps an undisclosed Secret meeting still allows a draft; the recheck before saving uses the same shared data. (BR-148) |  |  |
| Preconditions | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141)<br>● PRE-2: The actor has just found the slot in UC-81a. |  |  |
| Postconditions | ● POST-1: A chosen slot creates a Draft meeting through UC-13 (MSG29) with the first listed person as Chair, the rest as Member and the actor as Secretary; it follows the normal approval flow.<br>● POST-2: No signal — no distinct error, warning, notification or audit — differs only because a Secret meeting overlaps. (BR-148) |  |  |
| Normal Flow | Create Draft Meeting from Slot<br>1. The actor chooses a slot; the system opens the meeting form filled in with the slot and the invitees.<br>2. The actor confirms; the system checks the slot again against the same shared data as the UC-81a search, never against undisclosed meetings. (BR-148)<br>3. The system creates the Draft meeting through the creation steps of UC-13: the first listed invitee becomes Chair, the others Members and the actor Secretary. A value UC-13 rejects, such as a start time that has passed since the search, follows UC-13 EX-1.<br>4. The system displays MSG29 – “Meeting created successfully.” The meeting follows the normal approval flow. |  |  |
| Alternative Flows | N/A |  |  |
| Exception Flows | EX-1 – Slot is no longer free<br>1. At Step 2, the slot no longer fits the shared data (someone’s shared calendar changed).<br>2. No meeting is created. The system displays MSG446 – “This time slot is no longer free. Please choose another slot.” The message is the same whether or not an undisclosed meeting overlaps. (BR-148)<br>3. The use case ends; the actor may choose another slot from the UC-81a results.<br>EX-2 – Actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Member.<br>2. No meeting is created. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |  |  |

##### 16.2.3 UC-81c – View Shared Calendar

| Primary Actors | Member | Secondary Actors | User — the people who shared their calendars with the actor |
| --- | --- | --- | --- |
| Description | Allows a Member to see a weekly calendar of busy hours from people who shared with them. Each person has one row; no meeting titles are ever shown. People whose share stopped stay listed with their state label and contribute no hours; a share past its end date is no longer listed. (BR-140, BR-141, BR-144, BR-145, BR-142, BR-143) |  |  |
| Preconditions | ● PRE-1: The actor is signed in as a Member. (BR-140, BR-141) |  |  |
| Postconditions | ● POST-1: The actor sees only busy blocks of Effective (Đang hiệu lực) shares at shared levels; nothing else. (BR-140, BR-141)<br>● POST-2: Stopped (Đã dừng) shares stay listed with their state label and reveal no hours; they never come back. (BR-142, BR-143) |  |  |
| Normal Flow | View Shared Calendar<br>1. The actor opens the calendars shared with him or her and chooses a week. (BR-140, BR-141)<br>2. The system loads the actor's Effective or Stopped shares whose end date is today or later, keeping the newest share per person, and the busy Meetings of those people at shared levels. (BR-140, BR-141, BR-142, BR-143, BR-146, BR-147)<br>3. The system shows the week with one row per person and only busy blocks; days outside a share's range show as unknown, never as free. (BR-144, BR-145)<br>4. People whose share stopped are listed with that state and contribute no hours; opening one shows MSG439 – “This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share.” No title, location, attendee or level is ever shown. (BR-140, BR-141, BR-142, BR-143) |  |  |
| Alternative Flows | AF-1 – No Effective or Stopped share in range<br>1. At Step 2, the actor has no Effective or Stopped share whose end date is on or after today.<br>2. The system shows that nobody has shared a calendar with the actor and offers to ask for one (UC-80a). The use case ends.<br>AF-2 – A share changes while viewing<br>1. While the actor views the week, someone accepts, changes or stops sharing with the actor.<br>2. The actor sees the change without reloading the page.<br>3. The flow resumes at Step 3 of the Normal Flow. |  |  |
| Exception Flows | EX-1 – Actor cannot create meetings<br>1. At Step 2, the actor does not hold the Member right. (BR-140, BR-141)<br>2. Nothing is shown. The system displays MSG448 – “Only a Member can do this.”<br>3. The use case ends. |  |  |

## III. Functional Requirements

### 1. FE-01 – Authentication & Account Security

#### 1.1 Sign In

Figure 16: Sign In

The Sign In screen is the entry point of the system for every internal user (System Admin, Member, Approver, Chair, Secretary and Participant). It is displayed when a signed-out user opens the application or a meeting link, or when the sign-in session has expired, and supports signing in with email and password or with a Google account; signing out is confirmed from the account menu (UC-01, UC-02, UC-03).

This screen allows the System Admin, Member, Approver, Chair, Secretary and Participant to:

- Sign in with Email and Password: enter the registered email and password and click “Sign in with password” to access the system (UC-01). Each field is checked under its own input (empty email, malformed email, empty password) and focus moves to the first invalid field; the backend still answers every email/password failure with the same generic MSG01.

- Sign in with Google: click “Continue with Google” and choose the Google account whose email is registered in the system (UC-02).

- View Redirect Reason: see why s/he was sent to the sign-in page (a meeting link opened while signed out, or an expired sign-in session).

- View Sign-in Errors: see the reason a sign-in attempt failed, with a hint to choose the registered Google email when the Google account is not linked (UC-01, UC-02).

- Sign Out: choose Sign Out from the account menu at the bottom of the sidebar and confirm in the dialog to end the sign-in session and return to this screen (UC-03).

Field Description

| Field Name | Description |
| --- | --- |
| Application header | Read-only: TrustCabinet logo and name with the subtitle “Sign in to the paperless meeting room system” [Đăng nhập hệ thống phòng họp không giấy]. |
| Information banner [Hộp thông báo] | Read-only blue box, shown only when there is a reason: “Sign in to open the meeting.” [Đăng nhập để mở cuộc họp.] when a meeting link was opened while signed out, or “Your session has expired. Please sign in again.” [Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.] (MSG140) after the sign-in session ended. Hidden otherwise. |
| Error box [Hộp lỗi] | Red box displayed above the form when sign-in fails: MSG01 “Invalid email or password.”, MSG02 (account deactivated/unavailable), MSG03 (Google sign-in unsuccessful), MSG04 (Google account cannot be verified), MSG05 (Google account not granted access) followed by a hint to choose the Google account with the registered email. Messages never reveal whether an email exists (BR-004). |
| Email | Text input, required, email format. No initial value. Each field is checked under its own input — empty email, malformed email, empty password — and focus moves to the first invalid field. |
| Password [Mật khẩu] | Password input (masked), required. No initial value; the password is never displayed or stored in plain text. |
| Sign in with password [Đăng nhập bằng mật khẩu] | Primary button. Validates the credentials; a deactivated account cannot sign in (BR-001). On success the user is redirected to the authenticated home page, or to the meeting whose link was opened. |
| Divider “or” [hoặc] | Read-only separator between the password form and the Google option. |
| Continue with Google [Tiếp tục sử dụng dịch vụ bằng Google] | Button with the Google icon that opens Google sign-in. The verified Google email must be linked to an existing active internal account; no new account is created automatically (BR-003). If the Google sign-in service cannot be loaded, an error text replaces the button. |
| Sign Out confirmation dialog | Opened from the account menu: “Are you sure you want to sign out? The session on this device will end.” with Cancel / Sign Out buttons. On failure shows MSG06 “Unable to sign out. Please try again.” After sign-out protected functions are no longer accessible. |

#### 1.2 Reset Password

Figure 17: Reset Password

The Reset Password screen is opened by the account owner from the one-time link in the password-reset email that the System Admin sent from the Send Password Reset Link Dialog. The link is valid once, for 60 minutes, and lets the user set a new password without signing in; this is the completion part of Reset User Password (UC-07).

This screen allows the account owner (System Admin, Member, Approver, Chair, Secretary or Participant) to:

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
| Set new password [Đặt mật khẩu mới] | Primary button. Saves the new password as a salted hash, ends all sign-in sessions of the account, invalidates the link, records the reset in the audit log (BR-009, BR-010, BR-011) and redirects to Sign In with MSG92 “Password reset successfully. Please sign in with your new password.” If this browser is still signed in, it is signed out first; if that fails MSG06 is also shown. |
| Invalid link page | Shown instead of the form when the link is expired (older than 60 minutes), already used or superseded: “This link can no longer be used.” [Liên kết này không dùng được nữa.] (MSG93) with guidance to contact the System Admin. When the link has no reset code, MSG129 is displayed. |

#### 1.3 Guest Verification

After OTP verification, Guest enters display name and job title. State that only the invited email is verified and display information is self-declared. This revision does not prescribe additional required fields, validation messages or profile-save gates.

Figure 18: Guest Verification

Guest opens the invitation and verifies the invited email by OTP, then enters self-declared display name and job title. The Guest can use Member participation functions only in the invited Meeting. Only email control is verified; display information is not a verified legal identity. (UC-04, BR-055)

This screen allows the Guest to:

- View Invitation: see that TrustCabinet has invited him/her and the title of the invited meeting (UC-04).

- Enter Verification Code: type the 6-digit code sent to the invited email address (UC-04).

- Verify: submit the code; on success a meeting-scoped Guest session is established and the Guest continues to the invited meeting and the Invitation Response screen (UC-04, UC-31).

- Resend Code: request a new code to the invited email when the code was not received or has expired (UC-04).

On the screen, s/he can also:

- Read scope and recording notices: this invitation grants Member participation only in this Meeting. Display name and job title are self-declared; email is verified. Existing audio notice/consent requirements remain independent of profile entry.

Field Description

| Field Name | Description |
| --- | --- |
| Application header | Read-only bar with the TrustCabinet logo and name; no sign-in menu, as the Guest has no internal account (BR-052). |
| Invitation caption [TrustCabinet mời bạn họp] | Read-only uppercase caption “TrustCabinet invites you to a meeting”. |
| Meeting title | Read-only: title of the invited meeting, e.g. “Sprint 6 planning meeting” [Họp kế hoạch Sprint 6]. Only the invited meeting is shown (BR-052). |
| Code destination | Read-only text “Enter the 6-digit code just sent to v***@doitac.vn to view the meeting.” [Nhập mã 6 số vừa gửi tới … để xem cuộc họp.] showing the invited email partially masked. |
| Verification code | Six single-digit input boxes, required, digits only, exactly 6 digits; the cursor moves to the next box after each digit. A wrong, expired or already-used code shows MSG126 “Unable to verify the guest invitation. Check the code and try again.” |
| Verify [Xác minh] | Primary button. Verifies control of the invited email (BR-053, BR-054) and establishes a Guest session limited to the invited meeting that expires according to the configured validity period (BR-052, BR-053, BR-054); no internal account sign-in is needed (BR-052). A missing, expired or revoked invitation cannot be verified and the Guest is told the invitation is no longer valid (BR-044). If the verification service is unavailable no Guest session is created. |
| Resend code [Gửi lại mã] | Text link next to Verify; sends a new one-time code to the invited email address. |
| Recording notice | Read-only yellow info box: “The meeting may be recorded to prepare the minutes. Your information is used only for this meeting.” [Cuộc họp có thể được ghi âm để lập biên bản. Thông tin của bạn chỉ dùng cho cuộc họp này.] |
| Guest scope notice | Read-only scope notice: “Khách chỉ truy cập cuộc họp được mời và toàn bộ tài liệu gắn trong cuộc họp theo quyền Thành viên. Email đã được xác minh; tên hiển thị và chức vụ do khách tự khai.” (BR-055) |
| Already verified | When the Guest reopens a valid link during an active verified Guest session, this screen is skipped and the invited meeting opens directly (UC-04 AF-1). |

### 2. FE-02 – User & Profile Management

#### 2.1 User List

Figure 19: User List

The User List screen is the System Admin’s account management page, reached from Administration › Users [Quản trị › Người dùng] in the sidebar. It lists all user accounts, one per row, and provides the per-account actions edit (including the system role), deactivate/reactivate and send password reset link (UC-05, UC-06a–UC-06c, UC-07, UC-08).

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
| Page header | Read-only breadcrumb “Administration” [Quản trị], title “Users” [Người dùng] and summary “Manage users and system permissions · 7 accounts, 6 active.” [Quản lý người dùng và phân quyền hệ thống · … tài khoản, … đang hoạt động.] |
| Create account [Tạo tài khoản] | Primary button; opens the Create / Edit User Account Dialog in create mode. |
| Search [Tìm theo tên hoặc email…] | Text input, optional, empty by default. Matches full name or email. |
| Role filter [Tất cả vai trò] | Dropdown. Values: All roles (default) and every system role, e.g. [Chủ toạ], [Thư ký], [Thành viên], [Quản trị viên] and custom roles such as [Kiểm soát viên]. |
| Status filter [Mọi trạng thái] | Dropdown. Values: All statuses (default), Active, Deactivated. Combined filters must all match. |
| Full name [Họ tên] | Column: avatar initials, full name and email below, one account per row; a long name or email is cut with “…” and shows a tooltip only when cut. The signed-in System Admin’s own row is tagged “You” [Bạn]. |
| Role [Vai trò] | Column chip: name of the system role held by the account. |
| Sign-in method [Đăng nhập] | Column: Password [Mật khẩu], Google, or Password · Google (account linked to Google). Passwords and tokens are never displayed. |
| Status [Trạng thái] | Column chip: Active [Hoạt động] (green) or Deactivated [Đã vô hiệu hoá] (grey). |
| Created date [Ngày tạo] | Column, format dd/mm/yyyy. |
| Actions [Thao tác] | Three icon buttons: Edit · Deactivate / Reactivate · Send password reset link. A button is greyed out when not applicable and its tooltip gives the reason: the System Admin cannot change the status of his/her own account; a Google-only or deactivated account cannot receive a password reset link. |
| Deactivate / Reactivate confirmation | Dialog: deactivation signs the user out of all devices and keeps meeting history (BR-001); reactivation restores sign-in with the current role. Success shows MSG11 “User account status changed successfully.”; deactivating the last active account holding the User administration permission is blocked with MSG12. |
| No-result message | “No accounts match the filter.” shown in the table when search/filter returns nothing; “No accounts yet.” when the list is empty. |
| Pagination [Phân trang] | Client-side paging 20/50/100 over the full GET /api/users response (temporary decision; the server does not page yet, so this is not a server paging contract). |

#### 2.2 Create / Edit User Account Dialog

Figure 20: Create / Edit User Account Dialog

The Create / Edit User Account Dialog opens over the User List when the System Admin clicks “Create account” or the Edit action of an account. In create mode it collects full name, email, an initial password and the system role; in edit mode it changes full name, email and system role only (UC-06a, UC-06b, UC-08).

This screen allows the System Admin to:

- Create User Account: enter full name, email, password and role to create a new active account (UC-06a).

- Generate Password: click “Generate password” to fill a random password, then show or copy it (UC-06a).

- Update User Account: change the full name and email of an existing account (UC-06b).

- Assign System Role: select or change the system role of the account (UC-08).

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Create account” [Tạo tài khoản] in create mode, “Edit account” [Sửa tài khoản] in edit mode. |
| Information note | Create mode: “The new account is active and can sign in immediately with this email and password. If the email is a Google account, the user can also sign in with Google.” (BR-006). Edit mode: first Google sign-in with the same email links automatically; passwords are not changed here. |
| Full name [Họ tên] | Text input, required (*), max 200 characters. Errors: “Full name is required.”, “Full name must not exceed 200 characters.” (MSG09). |
| Email | Text input, required (*), valid email format, unique among accounts (BR-005). Errors: “Email is required.”, “Email is invalid.” (MSG09); “Email is already in use.” (MSG08). |
| Password [Mật khẩu] | Create mode only. Masked input, required (*), minimum 12 characters (MSG09 “Password must be at least 12 characters.”). Has an eye button to show/hide and a copy button. Hint: “Minimum 12 characters. Send the password to the user through a private channel, not a shared group chat.” [Tối thiểu 12 ký tự. Gửi mật khẩu cho người dùng qua kênh riêng, không qua nhóm chat chung.] |
| Generate password [Tạo mật khẩu] | Button next to Password; fills the field with a random password that satisfies the rule. |
| Role [Vai trò] | Dropdown, required (*); initial value [Thành viên] in create mode, current role in edit mode. Lists existing roles only (MSG09 “The role does not exist.” otherwise). Changing another user’s role signs that user out of all devices; changing the role of the last active account holding the User administration permission is blocked (MSG12). |
| Self-edit warning | Edit mode only. Shown when the System Admin changes his/her own role or email: after saving s/he will be signed out and must sign in again. |
| Cancel [Huỷ] | Secondary button; closes the dialog without saving. |
| Create account / Save [Tạo tài khoản / Lưu] | Primary button. Validates all fields, saves and records the change in the audit log (BR-009, BR-010, BR-011). Success toast MSG07 “User account created successfully.” or MSG10 “User account updated successfully.” |

#### 2.3 Send Password Reset Link Dialog

Figure 21: Send Password Reset Link Dialog

The Send Password Reset Link Dialog opens when the System Admin clicks the reset-password action of an account in the User List. It confirms sending a one-time reset link to the user’s registered email; the user then completes the reset on the Reset Password screen (UC-07). The same dialog style is used to confirm deactivating an account (UC-06c).

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
| Validity note | Read-only, in the same paragraph: the link can be used once within 60 minutes; any link sent earlier becomes invalid; the current password keeps working until the user (named in the text) sets a new one (UC-07 POST-4). |
| Cancel [Huỷ] | Secondary button; closes the dialog, no link is created. |
| Send link [Gửi liên kết] | Primary button. Allowed only for an active account that signs in with a password, otherwise MSG14 “Password reset is not available for this account.” Success closes the dialog and shows toast MSG13 “Password reset email sent successfully.”; email failure shows MSG15 “Unable to send the password reset email. Please try again.” and no new link becomes usable. The request is recorded in the audit log (BR-009, BR-010, BR-011). |
| Deactivate account dialog [Vô hiệu hoá tài khoản] | Same layout: “Deactivate <full name>’s account? This person will be signed out of all devices… Their meeting history is kept.” [… Lịch sử cuộc họp của họ vẫn được giữ nguyên.] with Cancel / Deactivate buttons (BR-001); success MSG11, blocked change MSG12. |

### 3. FE-03 – Role-Based Access Control

#### 3.1 Role List

Figure 22: Role List

The Role List screen is reached from Administration › Roles [Quản trị › Vai trò] in the sidebar and is the first tab of the role management page. The System Admin views the system roles with the number of accounts holding each, creates custom roles, and edits or deletes custom roles; predefined (Default) roles are protected (UC-09, UC-11a, UC-11b, UC-11c).

This screen allows the System Admin to:

- View Role List: view every role with its name, description and number of accounts (UC-09).

- Create Role: click “Create role” and enter a name, description and initial permissions (UC-11a).

- Update Role: edit the name and description of a custom role (UC-11b).

- Delete Role: delete a custom role that is no longer held by any account, after confirmation (UC-11c).

On the screen, s/he can also:

- Open Permission Matrix: switch to the “Permission matrix” tab to configure the permissions of each role (UC-09, UC-10).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Breadcrumb “Administration” [Quản trị], title “Roles” [Vai trò] and description: “Each role is a group of permissions; assign roles to accounts on the User screen.” [Mỗi vai trò là một nhóm quyền; gán vai trò cho tài khoản ở màn Người dùng.] |
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

Figure 23: Permission Matrix

The Permission Matrix screen is the second tab of the Roles page. It shows every role against every system permission as a matrix of toggles so that the System Admin can grant or revoke permissions, saving or undoing changes role by role (UC-09, UC-10).

This screen allows the System Admin to:

- View Role & Permission Matrix: view which permissions each role holds, grouped by area (UC-09).

- Grant / Revoke Permission: click a cell to switch a permission on or off for a role (UC-10).

- Save Role Permissions: click “Save” on a changed row to apply that role’s new permission set (UC-10).

- Undo Changes: click “Undo” on a changed row to restore its saved permissions (UC-10).

On the screen, s/he can also:

- Create Role: click “Create role” in the page header, which stays available on this tab (UC-11a).

Field Description

| Field Name | Description |
| --- | --- |
| Page header and tabs | Same header as the Role List (title “Roles” [Vai trò], Create role button); the “Permission matrix” [Ma trận phân quyền] tab is selected. |
| Role [Vai trò] | Row header: role name with “Default” [Mặc định] or “Your role” [Vai trò của bạn] badge; custom roles such as [Kiểm soát viên] have no badge. |
| Meeting [Cuộc họp] group | Permission columns: Create meeting [Tạo cuộc họp], Approve meeting [Phê duyệt cuộc họp]. |
| Minutes [Biên bản] group | Permission column: Prepare minutes [Soạn biên bản]. |
| Administration [Quản trị] group | Permission column: User administration [Quản trị người dùng]. |
| Permission cell | Toggle (checked = granted). Initial value = saved configuration. A changed cell is highlighted in yellow and its row is tinted until saved or undone. Only permissions defined by the system can be set (BR-007). |
| Actions [Thao tác] | “Undo” [Hoàn tác] and “Save” [Lưu] appear only on a row that has unsaved changes; each row is saved independently. Success toast MSG16 “Role permissions updated successfully.”; the change is audit-logged (BR-009, BR-010, BR-011). |
| Inline row error | Removing “User administration” from the last role that holds it for an active account is blocked and an error is shown under the row (MSG17 “Invalid permission configuration.”); save failure shows MSG18 “Unable to update role permissions. Please try again.” |

#### 3.3 System Audit Log

Figure 24: System Audit Log

The System Audit Log screen is reached from Administration › Audit Log [Quản trị › Nhật ký hệ thống] in the sidebar. It shows the read-only log of business and security events (not technical logs) so that the System Admin can trace who did what and when, and open the details recorded with each entry (UC-12).

This screen allows the System Admin to:

- View Audit Log: view events, newest first, with time, actor, event type, target object — shown with its name, or only as “Họp Mật” for anything of a Secret meeting — and IP address (UC-12).

- Filter Audit Log: filter by date range, actor and event type (UC-12).

- Search by Object: enter a keyword to find events of a target object by its name or id, or by the recorded details, ignoring case and Vietnamese marks; the name of anything of a Secret meeting never matches (UC-12).

- View Event Details: click a row to open a right-side panel with the actor, event code, object, IP address and the details recorded with the entry (UC-12).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Breadcrumb “Administration” [Quản trị], title “Audit Log” [Nhật ký hệ thống] and note “Business and security events. Cannot be edited or deleted.” [Sự kiện nghiệp vụ và bảo mật. Không sửa, không xoá được.] |
| Date range | Date range picker dd/mm/yyyy – dd/mm/yyyy (e.g. 01/10/2026 – 06/10/2026); limits events to the selected period. An invalid range is reported and not applied. |
| Actor filter [Mọi người thực hiện] | Dropdown; default All actors; lists users. |
| Event type filter [Mọi loại sự kiện] | Dropdown; default All event types (e.g. change role permissions, approve meeting, failed sign-in, download document, send password reset link). |
| Search [Tìm theo đối tượng…] | Text input, optional; matches the target object name or id, or the recorded details, ignoring case and Vietnamese marks — except the name of anything of a Secret meeting, which never matches. All criteria must match. |
| Time [Thời điểm] | Column, format dd/mm hh:mm, newest first. |
| Actor [Người thực hiện] | Column: avatar initials and full name of the user who performed the action. |
| Event [Sự kiện] | Column chip with the event type, e.g. “Changed role permissions” [Đổi quyền vai trò], “Approved meeting” [Duyệt cuộc họp], “Downloaded document” [Tải tài liệu], “Sent password reset link” [Gửi liên kết đặt lại mật khẩu]. Security events such as “Failed sign-in” [Đăng nhập thất bại] use a red chip and the whole row is highlighted in red. |
| Object [Đối tượng] | Column: the object name — meeting title, document file name, vote title, voice owner name, and so on — or the last 8 characters of its id when it has no name, or only “Họp Mật” for anything of a Secret meeting. |
| IP address [Địa chỉ IP] | Column: IP address of the request. |
| Detail panel | Right-side drawer opened by clicking a row; shows the actor, event code, object with its id, IP address and the details recorded with the entry, field by field. Read-only. |
| Pagination | “Showing 1–5 of 1,284” [Hiển thị 1–5 trong 1.284] with previous/next and page-number buttons. No matching entries shows an empty-result message so the filters can be revised. |

### 4. FE-04 – Meeting Management

#### 4.1 Meeting Calendar – Calendar View

Figure 25: Meeting Calendar – Calendar View

The Meeting Calendar – Calendar View is the work-schedule page reached from “Calendar” [Lịch làm việc] in the sidebar. The toolbar is the first line of the page (there is no page title). It places every Meeting the user is allowed to see on a weekly day/hour grid according to the Meeting’s own planned start and end time, colours each Meeting by its lifecycle status, and opens the Meeting detail when a block is clicked (UC-16, UC-15).

This screen allows any signed-in internal user related to a meeting (Member, Approver, Chair, Secretary, Participant) to:

- View Weekly Calendar: view the accessible Meetings of the selected week as blocks on a day/hour grid, positioned by their planned start and end time (UC-16). A Meeting the user has declined is not shown. (BR-047, BR-048)

- Navigate Weeks: go to the current week with “Today” or move to the previous/next week (UC-16).

- Switch View Mode: switch between the Calendar and List views of the same week (UC-16).

- Change Week Range: switch between the work week (Monday–Friday) and the full week (UC-16).

- Open Meeting Detail: click a Meeting block to open the Meeting Detail – General Information screen (UC-15).

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create Meeting dialog (4.4); the button is shown only to the Member, i.e. a user holding the create-meeting permission (UC-13).

Field Description

| Field Name | Description |
| --- | --- |
| Today [Hôm nay] | Button; jumps to the current week. |
| Previous / Next week (‹ ›) | Buttons; move one week back or forward. |
| Week label | Read-only date range of the displayed week, e.g. 12/10 – 16/10/2026. |
| View toggle [Lịch / Danh sách] | Segmented control Calendar [Lịch] (selected) / List [Danh sách]. The selected mode is kept in the URL. |
| Week range [Tuần làm việc] | Dropdown: Work week (default, Monday–Friday) or Full week. |
| Create meeting [Tạo cuộc họp] | Primary button; visible only to users with the create-meeting permission (Member). Opens the Create Meeting dialog over the calendar. |
| Day header | Weekday name and date (dd/mm) for each column, e.g. “Thứ Hai 12/10”; the current day is highlighted and labelled “Today” [Hôm nay]. |
| Time grid | Hour rows covering 24 hours; the grid scrolls inside its frame. For the current week it scrolls automatically to a red line marking the current time, with the time shown in a red badge on the hour axis (e.g. 13:47). |
| Meeting block | One block per Meeting, placed by its planned start and end time (BR-013): time range (hh:mm – hh:mm), Meeting title and mode · location (e.g. “Trực tiếp · Phòng họp A1”; Online Meetings show only “Trực tuyến”). A “Secret” [Mật] chip is shown for Secret Meetings. The block is coloured by status: Pending Approval has a dashed amber border and a Rejected Meeting is red with its title struck through. Only Meetings the user may access are shown (BR-016, BR-017), and a Meeting the user has declined through UC-31 is left out of the user’s own calendar; the Chair’s Meeting is never hidden (BR-047, BR-048). Clicking opens a quick view with the title, status, time, place, Chair and participants; an invitee who may still answer finds “Xác nhận tham dự?” [Confirm attendance?] with the buttons Attend [Tham dự] and Not attending [Không tham dự] there, and “View details” [Xem chi tiết] opens the Meeting detail; Back returns to the same week and mode. |
| Status legend | Read-only colour legend below the grid: In Progress [Đang họp], Published [Đã công bố], Approved [Đã duyệt], Pending Approval [Chờ duyệt], Draft [Nháp], Ended [Đã kết thúc], Rejected [Bị từ chối]. |
| Error state | If the Meetings cannot be loaded the grid shows MSG38 “Unable to load meetings. Please try again.” |

#### 4.2 Meeting Calendar – List View

Figure 26: Meeting Calendar – List View

The Meeting Calendar – List View shows the same weekly Meetings as the Calendar View, but as cards grouped by day in chronological order. It is opened with the List [Danh sách] toggle of the calendar toolbar and uses the same data and access rules as the calendar (UC-16, UC-15). A Meeting the user has declined is not listed (BR-047, BR-048). On a card of a Published or In Progress Meeting the invitee may also answer the invitation once (UC-31).

This screen allows any signed-in internal user related to a meeting (Member, Approver, Chair, Secretary, Participant) to:

- View Weekly Meeting List: view the accessible Meetings of the selected week grouped by day, each with planned start/end time, mode, location, status and confidentiality level (UC-16).

- Navigate Weeks: go to the current week or to the previous/next week (UC-16).

- Switch View Mode: return to the Calendar view of the same week (UC-16).

- Open Meeting Detail: click “View details” on a card to open the Meeting Detail – General Information screen (UC-15).

- Respond to Invitation: on a card that shows “Xác nhận tham dự?” [Confirm attendance?], click Attend [Tham dự] or Not attending [Không tham dự]; the answer is recorded once and cannot be changed (UC-31). The Chair has no buttons.

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create Meeting dialog; shown only to the Member (UC-13).

Field Description

| Field Name | Description |
| --- | --- |
| Toolbar | Same controls as the Calendar View: Today [Hôm nay], ‹ ›, week label, Calendar / List toggle (List selected, kept in the URL as ?mode=list), Work week [Tuần làm việc] dropdown and Create meeting [Tạo cuộc họp] button (Member only; opens the Create Meeting dialog). |
| Day group header | Weekday and full date (dd/mm/yyyy), e.g. “Thứ Hai 12/10/2026”, with the number of Meetings that day on the right (e.g. “1 cuộc họp”). The current day carries a “Today” [Hôm nay] badge. |
| Time column | Planned start time (bold) and planned end time below it (hh:mm), with a dot coloured by the Meeting status. |
| Meeting title | Name of the Meeting; struck through when the Meeting is Rejected. |
| Mode · location | Meeting mode In-person [Trực tiếp] / Online [Trực tuyến] / Hybrid [Kết hợp] followed by the location; Online Meetings show the mode only. |
| Status chip | Meeting status: Draft [Nháp], Pending Approval [Chờ duyệt], Approved [Đã duyệt], Rejected [Bị từ chối], Published [Đã công bố], In Progress [Đang họp], Ended [Đã kết thúc] (green). The time dot and the card’s left border keep the status tone, muted for a past meeting. |
| Confidentiality chip | Normal [Thường] / Internal [Nội bộ] / Secret [Mật]. |
| Response chip [Đã xác nhận tham dự / Không tham dự] | Shown on a card once the user has answered: “Đã xác nhận tham dự” [Attending] or “Không tham dự” [Not attending]. Nothing is shown while the user has not answered and for the Chair. (UC-31, UC-32) |
| Attendance question [Xác nhận tham dự?] | Shown on a card of a Published or In Progress Meeting that the user may still answer, with the buttons Attend [Tham dự] (primary, green) and Not attending [Không tham dự] (outline). One click sends the answer without a confirmation step; after it the buttons disappear and the card shows the Response chip. Choosing Not attending also removes the Meeting from the user’s calendar and the toast adds “Cuộc họp không còn hiện trên lịch của bạn.” Messages: MSG105 on success, MSG154 when the same answer is sent again, MSG285 when the invitation was already answered, MSG286 for the Chair, MSG107 when the Meeting no longer accepts answers (BR-046, BR-047, BR-048). |
| View details [Xem chi tiết ›] | Button on each card; opens the Meeting detail. Only Meetings the user may access are listed (BR-016, BR-017). |
| Empty day | A day without Meetings shows a dashed box “No meetings” [Không có cuộc họp]. The list scrolls within the page. |
| Error state | If the Meetings cannot be loaded the list shows MSG38 “Unable to load meetings. Please try again.” |

#### 4.3 Meeting List

Figure 27: Meeting List

Figure 28: Meeting List – time filter open

The Meeting List screen is reached from “Meetings” [Cuộc họp] in the sidebar. It lists only the Meetings the user has an authorized relationship with – Meetings s/he created (any status), Meetings assigned to him/her for approval from Pending Approval onward, and Meetings where s/he is active assigned Secretary before publication (BR-016, BR-017, BR-030), or Chair at any status, or Participant from Published onward; Guests do not use the internal list – with search, filters and paging. Visibility is enforced on the server (UC-16, UC-15).

This screen allows any signed-in internal user related to a meeting (Member, Approver, Chair, Secretary, Participant) to:

- View Meeting List: view the accessible Meetings with name, Chair, planned time, mode · location, confidentiality level and status (UC-16).

- Search Meetings: enter a Meeting name to search within the accessible Meetings (UC-16).

- Filter Meetings: filter the list by status, confidentiality level and time; the list opens unfiltered (every status, any time), and the time filter offers Today, This week, Not yet held and Already held (UC-16).

- Open Meeting Detail: click a row to open the Meeting Detail – General Information screen (UC-15).

On the screen, s/he can also:

- Create Meeting: click “Create meeting” to open the Create / Edit Meeting screen; shown only to the Member (UC-13).

- Change Page: move between pages of the result with the pager (UC-16).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Title “Meetings” [Cuộc họp] with the subtitle “Meetings you organize, are invited to or need to approve” [Các cuộc họp bạn tổ chức, được mời hoặc cần duyệt]. |
| Create meeting [Tạo cuộc họp] | Primary button; visible only to users with the create-meeting permission (Member). |
| Search [Tìm theo tên cuộc họp] | Text input, optional, empty by default; matches the Meeting name. |
| Status filter [Trạng thái] | Dropdown, with “All statuses” [Mọi trạng thái] (default, showing every status) and no “Hôm nay” [Today] choice — “Hôm nay” lives in the Time filter, not here. Options: Draft, Pending Approval, Approved, Rejected, Published, In Progress, Ended, Cancelled. The status is kept in the URL (status=all means every status). |
| Confidentiality filter [Mức độ] | Dropdown: “Mọi mức độ” [Any level] (default), Normal [Thường], Internal [Nội bộ], Secret [Mật]. |
| Time filter [Thời điểm] | Dropdown opening with “Mọi thời điểm” [Any time] (no time criterion). Other choices: This week [Tuần này], Not yet held [Chưa diễn ra], Already held [Đã diễn ra]. There is no date-range or month input. Today and This week match a Meeting whose own planned start/end interval overlaps that day or week; Not yet held matches a Meeting whose planned start time is later than now; Already held matches a Meeting whose planned start time is now or earlier, so a Meeting in progress counts as Already held. Combined criteria must all match. Selecting a status in the Status dropdown keeps the current Time criterion (PO 16/11/2026, note 52 Q1). |
| Meeting name [Tên cuộc họp] | Column: Meeting name in bold; second line shows the Chair (“Chủ trì: <name>”), “No Chair yet” [Chưa có chủ trì] when none is assigned, or the rejection reason (“Lý do từ chối: …”) for a Rejected Meeting. |
| Time [Thời gian] | Column: planned start–end time (hh:mm–hh:mm) with weekday and date (dd/mm/yyyy) below. |
| Mode · Location [Hình thức · Địa điểm] | Column: In-person / Online / Hybrid and the location; Online Meetings show the mode only. |
| Confidentiality [Mức độ] | Column chip: Normal [Thường] / Internal [Nội bộ] / Secret [Mật]. |
| Status [Trạng thái] | Column chip: Draft [Nháp], Pending Approval [Chờ duyệt], Approved [Đã duyệt], Rejected [Bị từ chối], Published [Đã công bố], In Progress [Đang họp], Ended [Đã kết thúc] (green). |
| Row link | The whole row is a link to the Meeting detail. Only Meetings the user may access are returned (BR-016, BR-017). |
| Pagination | “Showing 1–6 of 23 meetings” [Hiển thị 1–6 trong 23 cuộc họp] with ‹, page numbers and ›. Search, filter and sort criteria are kept when moving between pages. |
| Messages | “Hôm nay không có cuộc họp nào.” [There are no meetings today] with the button “Xem mọi cuộc họp” [View all Meetings] when the opening Today filter finds nothing; “No meetings match the filter.” when other criteria match no row; MSG38 “Unable to load meetings. Please try again.” on load failure; MSG32 “The meeting list criteria are invalid.” for invalid criteria. A Meeting the user has declined still appears in the list (BR-047, BR-048). |
| Summary cards [Thẻ tóm tắt] | Four cards above the list: Họp hôm nay [Meetings today], Đã công bố [Published], Cần xử lý [To do], Bản nháp [Drafts]. Họp hôm nay reads “N đang diễn ra” or “Chưa có cuộc họp đang diễn ra” and counts only today’s In-Progress meetings. Choosing a card filters the list to that card; choosing it again clears the filter. |
| Sort [Sắp xếp] | Default planned start time, newest first — except the Họp hôm nay and Cần xử lý views, which default to planned start time, oldest first. A manual sort choice is kept in the URL (sortSet) and when moving between pages. |

#### 4.4 Create / Edit Meeting

Figure 29: Create Meeting dialog

Figure 30: Edit Meeting dialog

The Create / Edit Meeting dialog opens over the current page with “Create meeting” from the calendar or the Meeting List, or with “Edit” [Sửa thông tin] on the detail of a Draft or Rejected Meeting. It is a dialog, not a separate page. The planned schedule (date, start and end time) is entered directly on the Meeting, which represents one sitting. A new Meeting is saved as Draft and is visible only to its Member until it is submitted for approval (UC-13, UC-14). Rescheduling a Published Meeting is done in the Reschedule Meeting dialog (4.9).

This screen allows the Member to:

- Create Meeting: enter Meeting name, description, meeting date, start and end time, mode, confidentiality level, location and optional online link, choose the initial participants with a Meeting Role for each (exactly one Chair), then save the Meeting as Draft (UC-13).

- Update Meeting: change the information of a Draft or Rejected Meeting without changing its status (UC-14). The edit dialog has no participant section; participants are changed on the Participants tab.

- Cancel: leave the form; if there are unsaved inputs a dialog asks to discard the changes or keep editing.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Tạo cuộc họp” [Create meeting] for a new Meeting, “Chỉnh sửa cuộc họp” [Edit meeting] when editing. A new Meeting is saved as Draft and only its Organizer sees it until it is submitted for approval (BR-013). |
| Meeting name [Tên cuộc họp] * | Text input, required, max 300 characters; e.g. “Họp kế hoạch Sprint 6”. Errors under MSG31: “Meeting title is required.”, “Meeting title must not exceed 300 characters.” |
| Description [Mô tả] | Textarea, optional, max 4,000 characters (MSG31 “Description must not exceed 4000 characters.”). |
| Meeting date [Ngày họp] * | Date picker (dd/mm/yyyy), required; the date of the sitting. |
| Start [Bắt đầu] * | Time picker (hh:mm), required. Together with the date it forms the Planned Start Time (BR-013); for a new Meeting it must be later than the current time (BR-013). |
| End [Kết thúc] * | Time picker (hh:mm), required; forms the Planned End Time, which must be later than the Planned Start Time (BR-013). |
| Mode [Hình thức] * | Segmented control: In-person [Trực tiếp], Online [Trực tuyến], Hybrid [Kết hợp]. Invalid values are rejected (MSG31 “Invalid meeting mode.”). |
| Confidentiality level [Mức độ mật] | Segmented control: Normal [Thường] (default), Internal [Nội bộ], Secret [Mật]. |
| Location [Địa điểm / Địa điểm họp trực tiếp] | Text input, max 300 characters, e.g. “Phòng 2.1, tầng 2”, hint “Phòng họp hoặc địa chỉ.”; shown for In-person and Hybrid Meetings and required for them (BR-013); labelled “Địa điểm họp trực tiếp” for a Hybrid Meeting. Error MSG31 “Location must not exceed 300 characters.” |
| Online link [Link họp trực tuyến] | Text/URL input, optional, max 1,000 characters (hint “Không bắt buộc, tối đa 1.000 ký tự.”); shown for Online and Hybrid Meetings. When left blank, the TrustCabinet online meeting room is used by default and the field shows the placeholder “Để trống: phòng họp TrustCabinet”. |
| Participants [Người dự họp] * | Only when creating. Search box “Tìm theo tên hoặc email để thêm người dự họp” lists active internal users who are not chosen yet (“Không có ai khớp, hoặc người đó đã được chọn.” when none); picking a person adds one row with the person’s name and email (“(Tôi)” [me] beside the signed-in user), a Meeting Role dropdown (Chair [Chủ tọa], Secretary [Thư ký], Member [Thành viên]; Member by default) and a remove button “Bỏ khỏi danh sách”. Choosing Chair for one person turns the previous Chair into Member, so there is exactly one Chair; a Secretary is optional; the Organizer need not be on the list; Guests cannot be added here. “Chưa chọn ai.” [No one chosen yet] when empty. Invalid list (nobody chosen, no Chair, a person listed twice, an inactive user) shows MSG277 “The meeting participant list is not valid.” under the field. Participants are saved with the Meeting in one save, each with a Pending response, and no invitation is sent while the Meeting is Draft. |
| Cancel [Huỷ] | Outline button; with unsaved inputs shows the confirm dialog “Bỏ cuộc họp đang soạn?” [Discard the Meeting being drafted?] (edit: “Bỏ thay đổi chưa lưu?” [Discard unsaved changes?]) with the buttons “Bỏ cuộc họp” / “Bỏ thay đổi” and “Tiếp tục soạn” / “Tiếp tục sửa” [Keep editing]. |
| Create meeting [Tạo cuộc họp] / Save changes [Lưu thay đổi] | Primary button (“Tạo cuộc họp” when creating, “Lưu thay đổi” when editing). Validates the fields and the participant list (BR-013); invalid input shows MSG31 “Invalid meeting information.” and highlights the fields, an invalid participant list shows MSG277. Create: the Meeting is saved with status Draft, the creator is recorded as Member and the chosen participants are saved with it (BR-013), toast MSG29 “Meeting created successfully.”, and the Meeting detail opens; a Secretary can still be assigned later on the Participants tab; failure shows MSG35. Edit: allowed only in Draft or Rejected and does not change the status (BR-014); toast MSG30 “Meeting updated successfully.”; MSG33 “This meeting can no longer be edited.” if the status has changed; failure MSG36. |

#### 4.5 Meeting Detail – General Information

Figure 31: Meeting Detail – General Information of a Draft Meeting (Member view)

Figure 32: Meeting Detail – General Information of a Published Meeting with the invitation response banner

Figure 33: Meeting Detail – General Information after accepting the invitation

The Meeting Detail – General Information tab is the default tab of the Meeting detail page, opened from the calendar, the Meeting List or a notification. Only users with an authorized relationship to the Meeting can open it. The header shows the Meeting name, status, confidentiality, the user’s own invitation response and schedule, a lifecycle bar and the action buttons allowed for the current status and the user’s role; from publication onward an invitee other than the Chair who has not answered also sees the attendance question under the lifecycle bar; the tab itself shows the Meeting information, a readiness checklist and the status history (UC-15, UC-31).

This screen allows any signed-in internal user related to a meeting (Member, Approver, Chair, Secretary, Participant) to:

- View Meeting Details: view the planned time, mode, location, online link, confidentiality level, Member, Approver and description of the Meeting (UC-15).

- View Lifecycle Progress: see the current stage of the Meeting on the lifecycle bar Draft – Pending Approval – Approved – Published – In Progress – Ended (UC-15).

- View Status History: view the timeline of lifecycle actions with actor and time (UC-15).

- Navigate Detail Tabs: switch to the Participants, Meeting Content (agenda), Documents, Voting, Recordings, Summary and Minutes tabs.

On the screen, s/he can also:

- View Readiness Checklist: (Member, Draft or Rejected Meeting) see which required items are complete and which are still missing, with where to complete them (UC-17).

- Run Status-dependent Actions: use the header buttons allowed for the current status and role: Edit and Submit for approval (Member, Draft/Rejected – UC-14, UC-17), Approve / Reject (assigned Approver – UC-18), Publish (Member, Approved – UC-19), Reschedule (Member, Published – UC-14), Start Meeting (Chair or Secretary, Published – UC-20) and End Meeting (Chair, In Progress – UC-21).

- Respond to Invitation: (invitee other than the Chair, Published Meeting, not yet answered) answer “Bạn có tham dự cuộc họp này?” [Will you attend this meeting?] with Attend [Tham dự] or Not attending [Không tham dự]; the answer is recorded once and cannot be changed (UC-31).

- Back to Meetings: click “‹ Meetings” to return to the previous list or calendar.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with status chip (e.g. Draft [Nháp]), confidentiality chip (e.g. Normal [Thường]) and, once the user has answered the invitation, a response chip “Đã xác nhận tham dự” [Attending] or “Không tham dự” [Not attending]; second line: weekday, date and planned time range (e.g. “Thứ Năm, 15/10/2026 · 08:30–09:30”) and mode · location. |
| Action buttons | Change with status and role: Draft/Rejected – Edit [Sửa thông tin] and Submit for approval [Gửi duyệt] (Member); Pending Approval – Reject [Từ chối] and Approve [Duyệt] (assigned Approver); Approved – Publish [Công bố] (Member); Published – Reschedule [Dời lịch] (Member) and Start meeting [Bắt đầu cuộc họp] (Chair or Secretary); In Progress – red End meeting [Kết thúc cuộc họp] (Chair), which asks for confirmation and remains available with open Votes or incomplete attendance. Buttons the user may not use are hidden. |
| Lifecycle bar | Read-only steps Draft [Nháp] · Pending Approval [Chờ duyệt] · Approved [Đã duyệt] · Published [Đã công bố] · In Progress [Đang họp] · Ended [Đã kết thúc]; the current step is highlighted. |
| Attendance question [Bạn có tham dự cuộc họp này?] | Bar under the lifecycle bar and above the tabs, with the buttons Attend [Tham dự] and Not attending [Không tham dự]. Shown only while the server says the user may answer: the user has a current invitation, is not the Chair, has not answered, and the Meeting is Published. From InProgress on, the detail no longer asks (attendance takes its place); the server still accepts an answer in InProgress. One click sends the answer; the bar then disappears and the response chip appears beside the title. There is no way to change the answer. Choosing Not attending adds “Cuộc họp không còn hiện trên lịch của bạn.” to the toast. Messages: MSG105 (recorded), MSG154 (same answer again), MSG285 (already answered), MSG286 (Chair), MSG107 (no longer answerable). (UC-31, BR-046, BR-047, BR-048) |
| Tabs | General Information [Thông tin chung] (selected), Participants [Thành phần tham dự], Meeting Content [Nội dung cuộc họp], Documents [Tài liệu], Voting [Biểu quyết], Recordings [Bản ghi], Summary [Tóm tắt], Minutes [Biên bản]. The Bản ghi tab stays locked until the Meeting starts (“Mở khi cuộc họp bắt đầu”) and the Biên bản tab until it has Ended (“Mở khi cuộc họp kết thúc”); a Cancelled Meeting locks both (“Cuộc họp đã huỷ”). The Secretary collecting voice samples keeps Bản ghi open before the start. |
| Time [Thời gian] | Read-only planned date and time range of the Meeting; once the Meeting has started, the actual start/end times are also shown (UC-15). |
| Mode [Hình thức] / Location [Địa điểm] | Read-only: In-person / Online / Hybrid and the in-person location (e.g. “Phòng 2.1, tầng 2”). |
| Online link [Link trực tuyến] | Read-only external link, or “Phòng họp trực tuyến TrustCabinet” when the internal online room is used. |
| Confidentiality [Mức độ] | Read-only chip: Normal / Internal / Secret. |
| Organizer [Người tổ chức] | Read-only name of the Member. |
| Approver [Người duyệt] | Read-only name of the assigned Approver; shows “Chosen when submitting for approval” [Chọn khi gửi duyệt] before submission. |
| Description [Mô tả] | Read-only multi-line text. |
| Readiness checklist [Sẵn sàng gửi duyệt] | Shown to the Member while Draft or Rejected. Each item is marked Complete [Đủ] (green) or Missing [Thiếu] (amber) with a hint where to fix it: time, mode and location set (required for submission); Secretary assigned; Chair assigned (“assigned by the Secretary on the Participants tab”); number of Meeting Content items. Chair and Secretary are mandatory at publication (BR-021). |
| History [Lịch sử] | Read-only timeline of lifecycle actions, e.g. “Draft created – Nguyễn Minh Anh · 09/10 15:02”, followed by submission, approval/rejection (with reason), publication, rescheduling, start and end, each with actor and time. History records cannot be changed (BR-022, BR-023). |
| Error states | A missing or inaccessible Meeting shows a not-found page without revealing whether it exists (BR-016, BR-017); load failure shows MSG37 “Unable to load meeting details. Please try again.” |

#### 4.6 Submit Meeting for Approval

Figure 34: Submit Meeting for Approval

The Submit Meeting for Approval dialog opens over the Meeting detail when the Member clicks “Submit for approval” [Gửi duyệt] on a Draft Meeting, or resubmits a Rejected Meeting after correcting it. The Organizer chooses the Approver and may leave a message; after submission the Meeting becomes Pending Approval and locks general Meeting information until a decision is made; the assigned Secretary can still manage Agenda Items under BR-059 (UC-17).

This screen allows the Member to:

- Select Approver: choose the Approver from the list of eligible active approvers (UC-17).

- Add Message to Approver: optionally write a message to the Approver (UC-17).

- Submit for Approval: confirm to send the Meeting for approval; the status becomes Pending Approval and the approver is notified (UC-17).

- Resubmit a Rejected Meeting: after correcting a Rejected Meeting according to the rejection reason, open the same dialog and submit it again (UC-17).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without submitting.

Field Description

| Field Name | Description |
| --- | --- |
| Submit for approval [Gửi duyệt] (header) | Primary button on the detail of a Draft or Rejected Meeting, visible to its Member; opens the dialog. Submission requires the mandatory Meeting information and a valid planned schedule (BR-013); otherwise the Meeting stays Draft/Rejected and the missing items are shown in the readiness checklist. |
| Dialog title | “Submit meeting for approval” [Gửi cuộc họp để duyệt]. |
| Approver [Người duyệt] * | Dropdown, required; each option shows name · position, e.g. “Võ Thị Lan · Trưởng phòng Kỹ thuật”. Hint: “Only users who may approve meetings are listed, excluding you.” [Chỉ hiện người có quyền duyệt cuộc họp, không gồm bạn]. Lists active accounts holding the approve-meeting permission, never the Member (BR-019). |
| Message to approver [Lời nhắn cho người duyệt] | Textarea, optional (placeholder “Không bắt buộc”); delivered to the Approver together with the approval request. |
| Information notice | Read-only blue box: “After submitting, you cannot edit general Meeting information until the meeting is approved or rejected; the assigned Secretary may still manage Agenda Items.” [Sau khi gửi, bạn không sửa được thông tin chung cho tới khi cuộc họp được duyệt hoặc bị từ chối; Secretary được phân công vẫn có thể quản lý Agenda Items.] (BR-018). |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Submit [Gửi duyệt] | Primary button. Re-checks that the Meeting is still Draft or Rejected and the approver is eligible, assigns the approver, changes the status to Pending Approval, records the submission in the history and notifies the approver (BR-018, BR-019, BR-022, BR-023); a success toast is shown. Errors: MSG41 “This meeting can no longer be submitted for approval.”, MSG42 “Unable to submit the meeting for approval. Please try again.”; an ineligible approver must be replaced by another eligible one. |

#### 4.7 Approve / Reject Meeting

Figure 35: Approve / Reject Meeting

The Approve / Reject Meeting function appears on the Meeting detail of a Pending Approval Meeting for its assigned Approver, who usually reaches it from the “pending approval” notification. The approver reviews all tabs in read-only mode and either approves the Meeting or rejects it with a reason. Approval does not publish the Meeting; a rejected Meeting goes back to the Member for correction and resubmission (UC-18).

This screen allows the Approver to:

- Review Meeting: read the Meeting information on all tabs in read-only mode, together with who submitted it and when (UC-18).

- Approve Meeting: click “Approve” to move the Meeting from Pending Approval to Approved (UC-18).

- Reject Meeting: click “Reject”, enter a mandatory reason and confirm to move the Meeting to Rejected (UC-18).

On the screen, s/he can also:

- Cancel Rejection: click “Cancel” in the reject dialog to return to the Meeting without a decision.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Pending Approval [Chờ duyệt] chip and confidentiality chip; second line: planned date and time and “Submitted by <Member> · dd/mm hh:mm” [Gửi bởi …]. |
| Approve [Duyệt] | Primary button, visible only to the Approver assigned to the current approval cycle (BR-019). Re-checks authorization and Pending Approval status, changes the status to Approved and records the decision with actor and time (BR-020, BR-022, BR-023); toast MSG43 “Meeting approved successfully.” |
| Reject [Từ chối] | Red outlined button, visible only to the assigned Approver; opens the reject dialog. |
| Dialog title | “Reject meeting” [Từ chối cuộc họp]. |
| Rejection reason [Lý do từ chối] * | Textarea, required, trimmed, max 1,000 characters; e.g. “Mục 3 chưa có tài liệu quy trình ký. Bổ sung rồi gửi lại.” Errors: MSG83 “Rejection reason is required.” (BR-020), MSG84 “Rejection reason must not exceed 1000 characters.” |
| Warning notice | Read-only amber box: “The meeting moves to Rejected. The organizer is notified with the reason.” [Cuộc họp chuyển sang Bị từ chối. Người tổ chức nhận thông báo kèm lý do.] |
| Cancel [Huỷ] | Text button; closes the dialog without a decision. |
| Reject (dialog) [Từ chối] | Red primary button. Changes the status to Rejected and stores the reason in the status history (BR-020, BR-022, BR-023); toast MSG44 “Meeting rejected successfully.” |
| Error messages | “This meeting is no longer awaiting approval.” when the status has changed; “Unable to complete the approval decision. Please try again.” when the decision cannot be saved; a notice when the user is no longer authorized to review the Meeting (BR-019). In each case the status is unchanged. |

#### 4.8 Publish Meeting

Figure 36: Publish Meeting

The Publish Meeting dialog opens over the Meeting detail when the Member clicks “Publish” [Công bố] on an Approved Meeting. It checks that the Meeting has an active Chair and an active Secretary, states how many internal participants and external Guests will be invited, and warns that after publication the Organizer may change only planned time, location and link; the assigned Secretary may still manage Agenda Items under BR-059. Confirming moves the Meeting to Published and starts invitation delivery (UC-19).

This screen allows the Member to:

- Check Publication Readiness: see whether the required Chair and Secretary are assigned and active, and who is still missing (UC-19).

- Review Invitation Recipients: see how many internal participants and external Guests will receive the invitation (UC-19).

- Publish Meeting: click “Publish and send invitations” to change the status to Published and send the invitations (UC-19).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog; the Meeting stays Approved.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Approved [Đã duyệt] chip and confidentiality chip; second line: planned date and time and “Approved by <Approver> · dd/mm hh:mm” [Duyệt bởi …]. |
| Publish [Công bố] (header) | Primary button, visible only to the Member of an Approved Meeting (BR-021); opens the dialog. |
| Dialog title | “Publish meeting” [Công bố cuộc họp]. |
| Chair check [Chủ trì] | Read-only item marked Complete [Đủ] with the Chair’s name (e.g. “Chủ trì: Trần Quốc Bảo”), or Missing [Thiếu] with a hint that the Secretary must assign the Chair on the Participants tab. Exactly one current Chair with an active account is required (BR-021). |
| Secretary check [Thư ký] | Read-only item marked Complete [Đủ] with the Secretary’s name (e.g. “Thư ký: Đỗ Hải Yến”), or Missing [Thiếu]. At least one current Secretary with an active account is required (BR-021). Invitation responses do not affect these checks (BR-024). |
| Information notice | Read-only blue box, e.g. “Invitations will be sent to 4 internal people and 1 external guest. After publishing, only the time, location and link can be rescheduled.” [Lời mời sẽ gửi tới 4 người nội bộ và 1 khách ngoài. Sau khi công bố chỉ còn dời được thời gian, địa điểm và link.] (BR-015). |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Publish and send invitations [Công bố và gửi lời mời] | Primary button; disabled while a check is Missing. Re-checks that the Meeting is still Approved with a valid planned schedule and the required Chair/Secretary (BR-021), changes the status to Published and records the action in the history (BR-022, BR-023). Invitations are sent asynchronously: in-app notification and email to internal participants, an email with a secure meeting-specific guest invitation to external Guests (BR-031, BR-032, BR-052); a delivery failure is retried and does not revert the Published status. Messages: MSG45 “Meeting published successfully.”, MSG46 “This meeting can no longer be published.”, MSG47 “Unable to publish the meeting. Please try again.” |

#### 4.9 Reschedule Meeting

Figure 37: Reschedule Meeting

The Reschedule Meeting dialog opens over the detail of a Published Meeting when the Member clicks “Reschedule” [Dời lịch]. It is the reschedule path of Update Meeting: only the planned date and time, the location and the online link can be changed, while the Meeting name and confidentiality level are locked. Saving keeps the Meeting Published and notifies every invitee of the new schedule (UC-14). The same header carries “Start meeting” for the Chair (UC-20).

This screen allows the Member to:

- Change Planned Time: choose a new meeting date and new start and end times for the Published Meeting (UC-14).

- Change Location and Online Link: change the in-person location and the optional online link (UC-14).

- Save and Notify Invitees: click “Save and notify” to save the new schedule and send the update to all invitees (UC-14).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog; the existing schedule is kept.

- Start Meeting: (Chair or Secretary) click “Start meeting” on the header of the Published Meeting to change its status to In Progress and record the actual start time (UC-20).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Meeting name with the Published [Đã công bố] chip and confidentiality chip; second line: current planned date and time and mode · location. |
| Reschedule [Dời lịch] (header) | Secondary button, visible only to the Member while the Meeting is Published and has not started (BR-015); opens the dialog. |
| Start meeting [Bắt đầu cuộc họp] (header) | Primary button for the assigned Chair or Secretary of a Published Meeting. Requires exactly one active Chair and at least one active Secretary (BR-024); sets status In Progress, records the actual start time and notifies invited internal participants in-app (BR-022, BR-023); the Meeting Content tab switches to the running mode. Messages: MSG110 “Meeting started successfully.”, MSG112 “This meeting cannot be started.”, MSG115 “Unable to start the meeting. Please try again.” |
| Meeting date [Ngày họp] | Date picker (dd/mm/yyyy), pre-filled with the current date of the Meeting. |
| Start [Bắt đầu] / End [Kết thúc] | Time pickers (hh:mm), pre-filled. The new planned start must be later than the current time (BR-015) and the end must be later than the start (BR-013). |
| Location [Địa điểm] | Text input, max 300 characters, pre-filled, e.g. “Phòng 3.2, tầng 3”. |
| Online link [Link trực tuyến] | Text/URL input, optional, max 1,000 characters; blank means the TrustCabinet online meeting room. |
| Meeting name [Tên cuộc họp] | Read-only (greyed) with the hint “Published, cannot be changed” [Đã công bố, không đổi được] (BR-015). |
| Confidentiality level [Mức độ] | Read-only (greyed) with the hint “Published, cannot be changed” [Đã công bố, không đổi được] (BR-015). |
| Information notice | Read-only blue box, e.g. “5 invitees will receive a notification and an email with the new schedule.” [5 người được mời sẽ nhận thông báo và email lịch mới.] Existing invitation responses are kept. |
| Cancel [Huỷ] | Text button; closes the dialog without changes. |
| Save and notify [Lưu và thông báo] | Primary button. Validates the fields (BR-013, BR-015, MSG31 for invalid input), saves the new schedule, keeps the Published status, records the change in the history (BR-022, BR-023) and queues notifications and emails to all current invitees; a delivery failure does not undo the change. Success: MSG138 “Meeting rescheduled successfully. Invitees will be notified.” If the Meeting is no longer Published or has started: MSG33 “This meeting can no longer be edited.”; save failure: MSG36 “Unable to update the meeting. Please try again.” |

### 5. FE-05 – Agenda Management

#### 5.1 Meeting Detail – Meeting Content Tab

Figure 38: Meeting Detail – Meeting Content tab

The Meeting Detail – Meeting Content tab shows the agenda of the selected Meeting. Agenda items belong directly to the Meeting and are listed in order with presenter and the number of attached documents; an item has no duration or planned time. Every user related to the meeting can view the agenda; the Secretary builds it (add, edit, delete, drag-and-drop reorder) while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. When the Meeting is InProgress, the same tab switches to the Meeting Facilitation screen (5.3) for the Chair (UC-26, UC-23a, UC-23b, UC-23c, UC-23d). Title, Presenter, Description and document links can be edited, and Items can be reordered, whatever their execution status, Done Items included; only Pending Items can be deleted. The complete Agenda automatically reflects successfully saved changes for its authorized viewers.

This screen allows users related to the meeting (Member, Approver, Chair, Secretary, Participant) to:

- View Meeting Agenda: view the complete Agenda in its saved order with Item information, planning and actual execution data. The complete view automatically refreshes after a successful Agenda change (UC-26).

- Add Agenda Item: (Secretary) click “Add item” to open the Add / Edit Agenda Item Drawer (UC-23a).

- Edit Agenda Item: (Secretary) edit Title, Presenter, Description and document links of an Item whatever its status, Pending, InProgress or Done (UC-23b).

- Delete Agenda Item: (Secretary) delete only a Pending Item after confirmation. Remaining Item positions are preserved (UC-23c).

- Reorder Agenda Items: (Secretary) drag any Item, including InProgress and Done Items, to a new position (UC-23d).

On the screen, s/he can also:

- Switch Detail Tabs: open another tab of the meeting detail, e.g. Documents to manage the files that agenda items refer to.

- Back to Meetings: click “‹ Meetings” to return to the meeting list or calendar.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Meeting Content [Nội dung cuộc họp] is selected. Other tabs: General Information, Participants, Documents, Voting, Recordings, Summary, Minutes. |
| Add content [Thêm nội dung] | Primary button; opens the Add / Edit Agenda Item Drawer. Visible only to the assigned Secretary and only while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress (BR-058, BR-059). |
| Drag handle | Shown on every row, including Done Items, to the active assigned Secretary while Agenda planning is permitted. Items of any execution status can be reordered; the new order is saved as one change (UC-23d). MSG63 confirms success; MSG143 reports invalid or outdated order; MSG71 reports a save failure. |
| # (Order number) | The saved position of the Item in the Meeting agenda. Deleting a Pending Item does not renumber remaining positions; gaps are permitted. New Items are appended after the last position. |
| Content [Nội dung] | Column: agenda item title in bold. The Documents [Tài liệu] column shows the number of attached meeting documents (e.g. “2 tài liệu”, “Chưa có”). While the Meeting is InProgress a Status [Trạng thái] column shows the execution chips Done [Đã xong], In discussion [Đang bàn] and Concluded [Đã kết luận]. |
| Presenter [Người trình bày] | Column: name of the presenter; an external Guest is marked “(guest)” [(khách)]. Empty when no presenter is set. Editable whatever the Item’s status. |
| Row action | Secretary only, while Agenda planning is permitted. Edit is offered on every row and permits Title, Presenter, Description and document links whatever the Item’s status (UC-23b). Delete is offered only on Pending rows (UC-23c, BR-063); MSG142 reports an attempted deletion of an Item that has started. MSG67 applies when planning is no longer permitted; MSG61/MSG62 confirm success and MSG69/MSG70 report save failure. |
| Empty and error states | When the Meeting has no agenda items an empty agenda message is shown (UC-26 AF-1). A meeting the user may not access shows the not-found page and no agenda data (BR-016, BR-017); load failure shows MSG128 “Unable to load the meeting agenda. Please try again.” |

5.1b Meeting Content Tab – Meeting InProgress

Figure 39: Meeting Content tab while the Meeting is InProgress (Secretary view)

Once the Meeting is InProgress the agenda list gains a Status [Trạng thái] column (Done [Đã xong], Concluded [Đã kết luận], In discussion [Đang bàn]) and the Secretary keeps full control of it. Every row, including Done and InProgress Items, has a drag handle and an Edit action; Delete [Xoá] appears only on Pending rows (UC-23b, UC-23c, UC-23d, BR-063). The Chair’s execution controls are on the Meeting Facilitation screen (5.3).

#### 5.2 Add / Edit Agenda Item Drawer

Figure 40: Add / Edit Agenda Item Drawer

The Add / Edit Agenda Item Drawer slides in from the right of the Meeting Content tab, so the agenda stays visible behind it, when the Secretary clicks “Add content” or chooses Edit on an item. It collects the content to discuss (title), an optional presenter, a note and a checklist of the Meeting’s documents linked to the item, with Cancel and Save buttons. Items can be added or edited only while the Meeting is Draft, WaitingApproval, Rejected, Approved, Published or InProgress (UC-23a, UC-23b). All four fields stay editable whatever the Item’s status.

This screen allows the Secretary to:

- Create Agenda Item: enter the item information and click “Add item” to add it in Pending status at the end of the Meeting agenda (UC-23a).

- Update Agenda Item: edit Title, Presenter, Description and document links of an Item whatever its status (UC-23b).

- Choose Presenter: optionally choose an internal participant or external Guest; the Presenter can be left empty and chosen later (UC-23a, UC-23b, BR-064, BR-065).

- Attach Item Documents: tick the documents of this Meeting in the checklist; documents are uploaded in the Documents tab, not here (UC-23a, UC-23b, UC-36b).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the drawer without saving.

Field Description

| Field Name | Description |
| --- | --- |
| Drawer title | “Add content” [Thêm nội dung] in create mode; the edit mode reuses the drawer with the current values. |
| Content to discuss [Nội dung cần bàn] * | Text input, required (*); the title of the agenda item. Empty shows the field error “Agenda item title is required.” (MSG66). |
| Presenter [Người trình bày] | Dropdown, optional; the hint “Không bắt buộc, có thể chọn sau.” is shown under it. Lists the current internal participants and external Guests of the Meeting; a Guest is shown with “(guest)” [(khách)], e.g. “Ngô Thanh Vân (khách)”. Editable whatever the Item’s status. (BR-064, BR-065) |
| Note [Ghi chú] | Textarea, optional; description of what will be presented or discussed. Editable whatever the Item’s status. |
| Documents [Tài liệu] | Checklist of the documents already uploaded to this Meeting (file type badge and file name); the ticked ones are linked to the item (FE-07, UC-36b). The drawer has no “+ Add document” menu and no upload: files are uploaded in the Documents tab. A document not available to the Meeting gives “One or more selected documents are not available to this meeting.” (MSG66). Editable whatever the Item’s status. |
| Cancel [Huỷ] | Text button; closes the drawer without saving. |
| Save [Lưu] | Primary button. Validates the fields and saves. Create: a Pending item is added at the end of the Meeting agenda, toast MSG60; Edit: toast MSG61. MSG67 if the Meeting is no longer Draft, WaitingApproval, Rejected, Approved, Published or InProgress or the user is no longer its Secretary (BR-058, BR-059); failure MSG68 / MSG69. Successful changes automatically refresh the complete Agenda for authorized viewers. |

#### 5.3 Meeting Facilitation

Figure 41: Meeting Facilitation

The Meeting Facilitation screen is the Meeting Content tab of a Meeting that is InProgress, after the Chair or the Secretary has started the Meeting (UC-20). It shows the agenda with the execution status and actual times of each item, a card for the current agenda item and a preview of the online room. The Chair sets which item is being discussed, completes it and moves to the next one, may create a vote for the current item and finally ends the Meeting; the assigned Secretary may continue to create, update, delete and reorder Agenda Items, while agenda execution remains Chair-only; other authorized viewers follow the same progress read-only and in real time (UC-24, UC-25, UC-26, UC-21). The Chair must start the first Pending Item in the current order and may complete it regardless of Planned Duration; the next Item never starts automatically. Pending Items stay Pending when the Meeting ends.

This screen allows the Chair to:

- Follow Meeting Progress: view the actual start time, elapsed time and attendance count of the Meeting, and the status and actual times of every agenda item (UC-26).

- Set Current Agenda Item: start only the first Pending Item in the current order, when no Item is InProgress. Its actual start time is recorded (UC-24).

- Complete Agenda Item: complete the current Item regardless of its planned duration; record actual end time and duration. The next Item does not start automatically (UC-25).

- Create Vote for Current Item: click “Create a vote for this item” to open the Create & Open Vote Dialog for the current agenda item (UC-63).

- End Meeting: click the red End button to end the Meeting regardless of open Votes or incomplete attendance, complete any current InProgress Item and leave remaining Pending Items Pending (UC-21).

On the screen, s/he can also:

- Join Online Room: click “Join online room” to open the online room of an Online or Hybrid Meeting in a new browser tab (UC-43).

- View Room Preview: see who is currently connected to the online room in the small video grid.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: title with status chip In Progress [Đang họp] and confidentiality chip; second line “Started 08:32 · 21 minutes elapsed” [Bắt đầu … · đã họp … phút] and “Present 4/5” [Có mặt 4/5] taken from attendance (6.3). |
| Join online room [Vào phòng trực tuyến] | Secondary button; shown for Online or Hybrid Meetings; opens the Meeting Lobby (8.1). |
| End meeting [Kết thúc cuộc họp] | Chair-only End Meeting confirmation; available while InProgress regardless of open Votes, attendance completeness or remaining Pending Items. End records actual time/history and completes only the InProgress Agenda Item; it closes room access but preserves Votes and ballots without automatically closing them. No new ballots after Ended. MSG111 success; MSG114/MSG116 on authority/status/save failure. (BR-024, BR-025, BR-026) |
| Agenda list | One row per agenda item: order number, title, presenter, number of documents and a status/action cell; there is no duration column. A Done item shows its actual time range (e.g. 08:32–08:44); the current item is highlighted and shows “In discussion since 08:44 · 9 minutes” [Đang thảo luận từ … · … phút]. The panel is 360px wide and has no drag handles; reordering is done in the Meeting Content tab (5.1). |
| Item status | Done or InProgress status is shown read-only. Only the first Pending Item offers Set as Current to the active assigned Chair when no Item is InProgress. |
| Set as current [Đặt hiện tại] | Chair only, while Meeting is InProgress. Starts only the first Pending Item in the current order and records actual start time. MSG72 reports another current Item; MSG144 reports selecting a later Pending Item; MSG73 reports other ineligible conditions; MSG75 reports save failure; MSG64 confirms success. (BR-060, BR-061, BR-062) |
| Current item card [Mục hiện tại · 2] | Read-only: order number and title of the current item and its attached documents (e.g. “2 documents · De-xuat-bieu-quyet.docx, So-sanh-eCabinet.pdf”); file names open the Document Viewer (7.5). |
| Create a vote for this item [Tạo biểu quyết cho mục này] | Secondary button in the current item card; Chair only, while the Meeting is InProgress (BR-090, BR-092, BR-093); opens the Create & Open Vote Dialog (11.2) linked to the current item. |
| Complete item [Hoàn thành mục] | Chair only, for the current InProgress Item. Completion is allowed regardless of Planned Duration and records actual end time and elapsed duration. The Meeting stays InProgress and the next Item does not start automatically. MSG65 confirms success; MSG74 reports ineligible conditions; MSG76 reports save failure. (BR-060, BR-061, BR-062) |
| Online room preview | Read-only grid of tiles with the names of users connected to the online room; a tile such as “Room 2.1” [Phòng 2.1] shows the in-person room of a Hybrid Meeting, and “Waiting to join” [Chờ vào] marks an empty slot. |
| Real-time update | The complete Agenda automatically refreshes for authorized users viewing the Meeting after successful creation, editing, deletion, reordering, start, completion or completion of the current Item when the Meeting ends. Failed changes do not produce a successful Agenda update. |

### 6. FE-06 – Meeting Participant Management

#### 6.1 Meeting Detail – Participants Tab

Figure 42: Meeting Detail – Participants tab (Secretary view)

Figure 43: Change Role dialog – the Chair hands over the Chair role

Figure 44: Change Role dialog – the Secretary changes a role

The Meeting Detail – Participants tab shows current internal participants and external Guests. The assigned Secretary manages the roster, roles and additional permissions; the Organizer may assign the missing Secretary. From Published onward, the Chair may view the roster and invitation responses by default, while a Member or verified Guest needs the relevant additional permission. A Member or verified Guest with View Invitation Responses can see invitee names and email in response rows without gaining roster browsing or search access. View-only access does not permit changes. (UC-29, UC-30a–UC-32, BR-039, BR-016, BR-017)

This screen allows the Secretary to:

- View Meeting Participants: view the internal participants and external Guests with name, email and meeting role or Guest access (UC-29).

- Search Participants: type a name or email to filter the roster (UC-29).

- Manage Meeting Role: click the pencil button “Đổi vai trò” [Change role] on the row of a current internal participant, choose one role in the Change Role dialog and click Save [Lưu]; before publication the Secretary may also make the person Chair, and the former Chair becomes Member in the same save (UC-30b).

- Hand Over Chair: (Chair, from publication onward) click “Đổi vai trò” on the row of the person who will take over, choose Chair [Chủ tọa] and click “Giao Chủ tọa” [Hand over Chair]; there is no separate hand-over button and no list of recipients (UC-34).

- Remove Participant: click “Remove” and confirm to remove an internal participant from the Meeting (UC-30c).

- Revoke Guest Invitation: click “Revoke invitation” and confirm to revoke the invitation of an external Guest (UC-30c).

- View Response Status: after the Meeting is Published, view each invitee’s invitation response as a label beside the name; there are no totals (UC-32).

On the screen, s/he can also:

- Add Participants: click “Add participants” to open the Add Participant Dialog on the Internal users tab (UC-30a).

- Invite External Guest: click “Invite external guest” to open the Add Participant Dialog on the External guests tab (UC-30a).

- Assign or Restore Secretary: in Draft/Rejected/Approved with no current Secretary, Organizer selects an active new user or an existing Member to assign Secretary; no duplicate participant is created (UC-30a).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Participants [Thành phần tham dự] is selected. Other tabs: General Information, Meeting Content, Documents, Voting, Recordings, Summary, Minutes. |
| Search [Tìm người tham dự] | Text input, optional, empty by default; filters the roster by full name or email. |
| Invite external guest [Mời khách ngoài] | Secondary button; opens the Add Participant Dialog on the External guests tab. Secretary only. |
| Add participants [Thêm thành phần] | Primary button; opens the Add Participant Dialog. Shown to the Secretary; when the Meeting has no Secretary it is replaced for the Member by “Add Secretary” [Thêm Thư ký] (BR-036, BR-037). Hidden while the Meeting is Pending Approval or Ended (BR-036, BR-037, BR-016, BR-017, BR-030). |
| Participant [Thành phần] | Column: internal full name, with “(Tôi)” [me] after the name of the signed-in user, or Guest self-declared display name, optional Guest job title and invited email; the response label of UC-32 sits beside the name; each email is shown on one line cut with “…” when too long, and a long email wraps before “@” or “.” rather than in the middle of a word. On a phone the attendance and the role chip share one row; use invitation name before profile completion. Mark Guest explicitly; do not represent display information as verified identity. |
| Meeting role [Vai trò trong cuộc họp] | Internal participants have Chair [Chủ trì], Secretary [Thư ký] or Member [Thành viên]; Guests have fixed Guest [Khách] access. Change role [Đổi vai trò] opens the Change Role dialog (6.1b, 6.1c). Giving the Chair role changes both roles together: the current Chair becomes Member and the selected person becomes Chair, as the Chair option itself states “Bạn sẽ thành Thành viên.” A Secretary must remain in Published/InProgress; preparation may temporarily have none. Success MSG100; replacement failure MSG146; required-role restriction MSG109. (BR-039, BR-040, BR-041, BR-042) |
| Row action [Thao tác] | Secretary and Chair: pencil button “Đổi vai trò” [Change role] and trash button “Gỡ khỏi cuộc họp” [Remove from the meeting] for internal participants; trash button “Thu hồi lời mời” [Revoke invitation] only for Guests (a Guest has no role to change). The Chair’s own row has no actions: to move the Chair role, use Change role on the recipient’s row. View-only users see no management actions. Removal ends the assignment’s additional permissions and access, while independent valid access remains. Required-role restriction MSG109; removal success MSG103; failure MSG104; status restriction MSG98. (BR-036, BR-037, BR-044, BR-041, BR-042) |
| Response label [Đã xác nhận tham dự / Không tham dự] | Shown beside the name after publication to the assigned Secretary and current Chair by default, and to a Member or verified Guest with View Invitation Responses. Shows “Đã xác nhận tham dự” [Attending] or “Không tham dự” [Not attending] for each current invitee who answered; an invitee who has not answered and the Chair have no label. There are no totals, and nobody answers on this tab. This permission displays identity in response rows but does not grant roster browsing or search. Responses do not set attendance. (UC-32, BR-043, BR-016, BR-017, BR-047, BR-048) |
| Pagination | “Showing 1–6 of 6 participants” [Hiển thị 1–6 trong 6 thành phần] with page-size choices 20, 50 and 100 (invitations are still sent when the Meeting is published, BR-031, BR-032). |
| Error states | A Meeting the user is not authorized for shows the not-found page without roster data (BR-016, BR-017); load failure shows MSG95 “Unable to load meeting participants. Please try again.” |
| Additional permissions [Quyền bổ sung] | The Secretary may grant or revoke additional Meeting permissions on current internal participants or Guest invitations without changing their role. Available grants are not limited to View Participants and View Responses; the final catalogue and grant matrix are pending. These two views continue to check their respective permission. (BR-016, BR-017) |

Change Role Dialog. The Change Role dialog opens from the “Đổi vai trò” [Change role] button on one internal participant’s row (UC-30b, UC-34). The Secretary (Draft, Rejected, Approved, Published, InProgress) and the Chair of a Published or InProgress Meeting use it. A person is given exactly one role; there is no list of recipients and no separate Hand over Chair button.

| Field Name | Description |
| --- | --- |
| Dialog title | “Đổi vai trò” [Change role]. |
| Person | Read-only avatar, full name and email of the participant whose role changes. |
| Role [Vai trò] | One radio per role, each with a short description of what the role allows: Chair [Chủ tọa] “Điều hành cuộc họp, chuyển mục nghị sự, tắt micro người dự.”; Secretary [Thư ký] “Soạn chương trình nghị sự, quản lý thành phần.”; Member [Thành viên] “Xem cuộc họp và vào phòng họp.” The person’s current role is selected when the dialog opens. |
| Chair option | Offered only when the signed-in user may move the Chair role and this person may take it: the person is a current active internal Member (not a Guest, not the Secretary, account not locked) (BR-041, BR-042), and either the signed-in user is the Chair of a Published or InProgress Meeting, or the Meeting is not yet Published, or the Chair’s account is locked and the signed-in user is the Secretary (BR-041, BR-042). When it is not offered (6.1c) the list shows only Secretary and Member. The line under Chair adds “Bạn sẽ thành Thành viên.” when the signed-in user is the Chair handing over, or “<name> sẽ thành Thành viên.” naming the Chair who steps down. If nobody on the roster can take the Chair role, no row offers it; MSG258 is returned by the API but never shown. |
| Cancel [Huỷ] | Outline button; closes the dialog without changes. |
| Save [Lưu] / Hand over Chair [Giao Chủ tọa] | Primary button, disabled until a role different from the current one is chosen; labelled “Giao Chủ tọa” when Chair is chosen and “Lưu” otherwise; shows “Đang lưu…” while sending. Success closes the dialog and shows a toast: MSG257 “Giao vai trò Chủ tọa thành công.” after a hand-over, MSG100 “Cập nhật vai trò cuộc họp thành công.” for other changes, MSG154 when nothing changed. Refusals show a toast after the dialog closes: MSG256 (a Secretary tried to move the Chair role while the Chair’s account is active), MSG259 (the recipient is not an eligible active internal Member), MSG260 (the user is no longer the Chair), MSG109 (the Meeting would lose its required Chair or Secretary); MSG102 “Unable to update the meeting role. Please try again.” appears inside the dialog when saving fails. The roster is then read again. |

#### 6.2 Add Participant Dialog

For a Guest invitation, enter name/email and optional additional Meeting permissions. Secret shows inline warning: “Khách được truy cập tài liệu và tham gia như Thành viên của cuộc họp này.” No extra confirmation/approval step. Guest has no Chair/Secretary role selector.

Figure 45: Add Participant Dialog

The Add Participant Dialog opens from the Participants tab with “Add participants” (Internal users tab) or “Invite external guest” (External guests tab). The Secretary searches active internal users, ticks the people to add and chooses their meeting role, or invites an external Guest by name and email. When the Meeting has no Secretary yet, the Member uses the same dialog to add the missing Secretary (UC-30a).

This screen allows the Secretary (or the Member when adding the missing Secretary) to:

- Search Internal Users: type a name or email to find active internal users (UC-30a).

- Add Internal Participants: tick one or more users, choose the meeting role and click “Add <n> people” (UC-30a).

- Invite External Guest: on the External guests tab enter the Guest’s name and email and submit the invitation (UC-30a).

On the screen, s/he can also:

- Cancel: click “Cancel” to close the dialog without adding anyone.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Add participants” [Thêm thành phần]. |
| Tabs | Internal users [Người trong hệ thống] (default) / External guests [Khách mời ngoài]. |
| User search | Text input on the Internal users tab, e.g. “Phạm”; filters active internal users by name or email. |
| User result list | Select one or more active internal accounts by checkbox. Current participants cannot be added again. Organizer recovery may instead assign Secretary to one existing active Member without creating a duplicate. All selected users are added together, or none are added if any selection is invalid. No organisational-unit invitation. For the missing Secretary, select exactly one user. (BR-038) |
| Meeting role [Vai trò trong cuộc họp] | Required; Member [Thành viên] by default, Chair [Chủ trì] or Secretary [Thư ký]. One shared role applies to the entire selection. The missing Secretary assignment is one user as Secretary, without additional permissions. A selection must not create a second Chair; use Edit Role & Permissions to replace the current Chair. (BR-039, BR-040, BR-041, BR-042) |
| Guest name | External guests tab. Text input, required. |
| Guest email | External guests tab. Email input, required, valid format. One current invitation per email per Meeting and the email must not belong to a current internal participant (BR-049, BR-050, BR-051). The Guest receives an email with a verification link once the Meeting is Published or InProgress (BR-052). |
| Cancel [Huỷ] | Text button; closes the dialog. |
| Add <n> people [Thêm <n> người] | Shows the number selected and submits the entire selection. New invitations start Pending. All selected people are saved together; no one is added when validation or saving fails. Success MSG96 and the number added; invalid participant information MSG97/MSG145; invalid additional permissions MSG152; status restriction MSG98; save failure MSG99. Invitation delivery failure does not cancel successful additions. (BR-031, BR-032) |
| Additional permissions [Quyền bổ sung] | Additional permissions for current internal participants and Guest invitations supplement existing role permissions for this Meeting. Display the grantable permissions from the agreed catalogue; do not hardcode a two-option ceiling. View Participants and View Responses are examples. The catalogue and grant matrix are pending. |

#### 6.3 Attendance Check

Figure 46: Attendance Check – the Secretary records attendance

Figure 47: Attendance Check – participant view

Attendance Check is the Attendance [Điểm danh] column of the participant table on the Participants tab of the Meeting detail, shown while the Meeting is InProgress or Ended; it is not a separate screen. The assigned Secretary marks each current internal participant and external Guest Present or Absent by choosing a value in the cell; choosing a value saves it at once, and there is no Save button and no Present/Absent total. Other participants read the attendance. A person not yet recorded has no official attendance value and is shown as “Chưa điểm danh”, which is not Absent. Invitation responses and online-room presence never determine attendance. Saved attendance is available for the meeting minutes. In the online meeting room the same attendance is also available in the Attendance panel (8.7). (UC-33, BR-043, BR-056, BR-057)

This screen allows the Secretary to:

- View Attendance: view every participant and Guest of the Meeting with meeting role, invitation response label and current attendance in the participant table (UC-33).

- Record Attendance: (Secretary) choose Present [Có mặt] or Absent [Vắng] in the Attendance cell of a person; each choice is saved immediately (UC-33).

- Revise Attendance: (Secretary) choose another value for the same person while attendance editing is still permitted (UC-33).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: title with status chip In Progress [Đang họp] and confidentiality chip; second line actual start time (e.g. “Started 08:32”) and mode · location. |
| Save result | There is no Save button: choosing a value sends that person’s attendance at once, and the cell shows the chosen value while the request runs. Success MSG147 “Meeting attendance saved successfully.”; invalid value MSG148; status restriction MSG149. MSG151 “The participant or attendance data has changed. Please reload and review before saving.” when someone else changed the same person’s attendance or the person is no longer on the roster: nothing is saved, the table is reloaded and the Secretary chooses again, no unsaved choice is kept. MSG150 “Unable to confirm the attendance save result. Please reload the data before trying again.” when the result cannot be confirmed; the Secretary reloads to see what was saved. MSG289 “Unable to load attendance data.” with a Retry button when the attendance cannot be read; a failed load is never shown as an empty roster. Attendance completeness never blocks End Meeting. (BR-056, BR-057) |
| Participant [Thành phần] | Column: avatar initials, full name (with “(Tôi)” for the signed-in user) and email of each current internal participant and external Guest, with the response label of UC-32 beside the name. |
| Role [Vai trò] | Read-only column: Chair [Chủ tọa], Secretary [Thư ký], Member [Thành viên] or Guest [Khách], each as a square chip of its own tint. |
| Attendance [Điểm danh] | For the Secretary a small dropdown: dot and text Present [Có mặt] (green dot) or Absent [Vắng] (grey dot); a dashed empty dropdown reading “Điểm danh” while the person has never been recorded. For everyone else a read-only dot and text, or “Chưa điểm danh” when never recorded. The tooltip reads “<name> điểm danh lúc <hh:mm dd/mm/yyyy>” (who recorded it and when). Each person’s attendance has its own version. Invitation responses and online-room connections never select or save official attendance automatically. Revision is allowed only while InProgress. (BR-043, BR-056, BR-057) On a phone the attendance control and the role chip share one row. |

#### 6.4 Invitation Response

Figure 48: Invitation Response – before answering

Figure 49: Invitation Response – after answering

The Invitation Response screen is a mobile-friendly page opened from the invitation email; an external Guest reaches it after Guest Verification (1.3). Internal users normally answer on the card in the calendar list (4.2) or at the top of the Meeting detail (4.5); this page gives the same answer from a phone. The invitee sees the meeting title, status, confidentiality level, planned time, mode and location, his/her own meeting role and the Chair, and answers Accept or Decline once; after that the answer is shown and cannot be changed. The Chair does not answer. An invitee who never answers is treated as attending (UC-31, UC-04, BR-046, BR-047, BR-048).

This screen allows an invited Secretary, Participant or a verified external Guest to:

- View Invitation: view the meeting title, status, confidentiality level, planned time, mode and location, his/her meeting role and the Chair (UC-31).

- Accept Invitation: click “Attend” to record the response Accepted (UC-31).

- Decline Invitation: click “Not attending” to record the response Declined (UC-31).

Field Description

| Field Name | Description |
| --- | --- |
| App bar | TrustCabinet logo and name; the page needs no sidebar so it works on a phone. |
| Invitation header | Read-only: eyebrow “Meeting invitation” [Lời mời họp], meeting title, status chip (e.g. Published [Đã công bố]) and confidentiality chip (e.g. Normal [Thường]). |
| Time [Thời gian] | Read-only planned start and end of the Meeting, e.g. “Thursday, 15/10/2026 · 08:30–09:30”. |
| Mode [Hình thức] | Read-only mode and location, e.g. “Hybrid · Room 2.1” [Kết hợp · Phòng 2.1]. |
| Your role [Vai trò của bạn] | Read-only meeting role of the invitee: Chair, Secretary, Participant [Thành viên] or Guest. |
| Chair [Chủ trì] | Read-only name of the Chair of the Meeting. |
| Attend [Tham dự] | Button; records Accepted. Shown only while the invitation has no answer. Only the owner of a current invitation can respond (BR-046). |
| Not attending [Không tham dự] | Button; records Declined; the Meeting then disappears from the invitee’s own calendar. Shown only while the invitation has no answer. A Guest may answer only Accept or Decline (BR-046). |
| Answer note [Bạn chỉ trả lời một lần] | While unanswered, an info box: “You answer only once and cannot change it. If you do not answer you are still counted as attending.” After answering, the buttons are replaced by a green box, e.g. “Bạn đã chọn: Tham dự · 10/10 09:02. Câu trả lời không đổi lại được.” The response and time are recorded once (BR-046). |
| Messages | Success MSG105 “Meeting invitation response recorded successfully.”; MSG154 “No changes to save.” when the same answer is sent again; MSG285 “You have already answered this invitation. Your answer cannot be changed.” when a different answer is sent after answering; MSG286 for the Chair; MSG106 invalid response; MSG107 “This meeting invitation can no longer be answered.” when the Meeting is no longer Published/InProgress or the invitation was revoked or expired (BR-053, BR-054, BR-044); MSG108 save failure. |

### 7. FE-07 – Personal Document Library & Meeting Document Management

#### 7.1 Document Library – Empty State

Figure 50: Document Library – Empty State

The Document Library – Empty State screen is what a signed-in internal user sees on opening Document Library [Kho tài liệu] from the sidebar before he/she has created any folder or uploaded any file. The folder panel holds only All documents with 0 files, and the file area shows an empty-state panel explaining how the library is used. The New folder and Upload buttons inside the panel do the same as those at the top of the page; a file uploaded while All documents is selected is stored at the root of the library. Once the library holds folders or files, the page is the Personal Document Library (7.2). External Guests have no personal library (UC-37, UC-39, UC-41a).

This screen allows any signed-in internal user (Member, Approver, System Admin, Chair, Secretary, Participant) to:

- Open Document Library: click Document Library [Kho tài liệu] in the sidebar to open his/her own library page (UC-37).

- View Empty Library: see that his/her library has no folders or files yet and read how it is used (UC-37).

- Upload Document: click “Upload” at the top of the page or in the empty-state panel to store a file at the root of the library (UC-39).

- Create Folder: click “New folder” at the top of the page or in the empty-state panel to create a folder at the root of the library (UC-41a).

Field Description

| Field Name | Description |
| --- | --- |
| Sidebar item [Kho tài liệu] | Navigation item in the Business [Nghiệp vụ] group; highlighted when this page is open. |
| Page title | Read-only “Document Library” [Kho tài liệu]. |
| Page subtitle | Read-only: “Your private documents — only you can see and manage what is here.” [Tài liệu riêng của bạn — chỉ bạn thấy và quản lý được những gì ở đây.] |
| New folder [Thư mục mới] / Upload [Tải lên] | Shown both in the page header and inside the empty-state panel; both pairs do the same. New folder creates a folder at the root of the library (UC-41a); Upload opens the file picker and stores the chosen file at the root of the library (UC-39, BR-069). |
| Folder panel | Shows only All documents [Tất cả tài liệu] with the count 0, selected; no folder exists yet. |
| Empty-state panel | Dashed panel with a document icon in the file area, holding the title, explanation text, file hint and the New folder and Upload buttons. |
| Empty-state title | Read-only: “No documents yet” [Chưa có tài liệu nào]. |
| Explanation text | Read-only: “Upload documents to the library, put them into folders, then attach them to meetings where you are Chair or Secretary. A document not attached to any meeting can be read only by you.” [Tải tài liệu lên kho, xếp vào thư mục, rồi gắn vào cuộc họp mà bạn làm Chủ trì hoặc Thư ký. Tài liệu chưa gắn cuộc họp nào chỉ mình bạn đọc được.] |
| File hint | Read-only small text: “Accepts PDF and DOCX, up to 10 MB per file.” [Nhận PDF và DOCX, tối đa 10 MB mỗi tệp.] (BR-069) |

#### 7.2 Personal Document Library

Figure 51: Personal Document Library

The Personal Document Library is the Document Library page once the user’s library holds folders or files; an empty library shows the empty state (7.1). Each internal user has a private library: folders on the left and the file table on the right, with size, upload date and the number of meetings that use each file. Files have no versions; to change a document the user uploads a new file. Uploading a file to the library does not attach it to a meeting; files are attached through the Add Document to Meeting Dialog (7.4). A file uploaded straight into a meeting lands in a folder named after that meeting. External Guests have no personal library (UC-37, UC-39, UC-40, UC-41).

This screen allows any signed-in internal user (Member, Approver, System Admin, Chair, Secretary, Participant) to:

- View Personal Document Library: view his/her own documents with file name, size, upload date and meeting usage (UC-37).

- Browse Folders: select All documents or a folder in the left panel to list the files and sub-folders it contains (UC-37).

- Search and Filter Documents: search by file name and filter by file type (UC-37).

- Upload Document: click “Upload” and choose a file from the computer to store it in the selected folder (UC-39).

- Organise Documents: rename a file, move it to another folder or delete it from the library; meetings that already use the file keep it (UC-40).

- Manage Folders: click “New folder” to create a folder, and rename, move or delete an empty folder from its menu; folders nest at most five levels (UC-41).

On the screen, s/he can also:

- Use Row Actions: use the icon buttons of a row to work with that file.

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Read-only title “Document Library” [Kho tài liệu] with the subtitle “Your private documents — only you can see and manage what is here.” Only the owner’s own documents are listed. |
| New folder [Thư mục mới] | Secondary button; creates a folder inside the selected folder, or at the root when All documents is selected. Nesting is limited to five levels (UC-41a). |
| Upload [Tải lên] | Primary button; opens the file picker and uploads the chosen file into the selected folder (UC-39). Only PDF and DOCX files up to 10 MB are accepted (BR-069); a file that cannot be stored is rejected with an error and nothing is saved. |
| Folder panel | Tree of All documents [Tất cả tài liệu] (default, selected) and the user’s folders, each with the number of files. A folder named after a meeting (e.g. “Họp kế hoạch Sprint 6 - 15/10/2026”) holds files uploaded straight into that meeting. Each folder has a menu: Rename, Move, Delete (only when empty) (UC-41). |
| Search [Tìm theo tên tệp…] | Text input, optional, empty by default; matches the file name within the selected folder. |
| File type filter [Mọi loại tệp] | Dropdown. Values: All file types (default), PDF and Word (DOCX). |
| File name [Tên tệp] | Column: file icon and file name, e.g. “Ke-hoach-kinh-doanh-2027.docx”. |
| Size [Dung lượng] | Column: file size, e.g. 1,2 MB / 640 KB. |
| Uploaded [Ngày tải lên] | Column: upload date, format dd/mm/yyyy. |
| Used in [Đang dùng trong] | Column: number of meetings the file has been attached to, e.g. “2 meetings” [2 cuộc họp], or “Not used” [Chưa dùng] in italics. A file not attached to any meeting can be read only by its owner. |
| Actions [Thao tác] | Icon buttons per row: open preview, download, rename, move to another folder and delete from the library (UC-40). Delete asks for confirmation and states that meetings using the file keep it (BR-074, BR-075, BR-076). |

#### 7.3 Meeting Detail – Documents Tab

Figure 52: Meeting Detail – Documents Tab

The Meeting Detail – Documents tab lists the documents attached to the selected Meeting in three groups: By meeting content, with one block per Agenda Item in agenda order; Voting, with the documents of each Vote the user can see and the user's own read state; and General documents, linked to no Agenda Item. A file linked to several Agenda Items appears under each of them and is uploaded only once; a document linked to a Vote appears only in the Voting group, and a document linked only to a Draft Vote is not shown to a Member or Guest at all (BR-079, BR-080). The Meeting keeps its own reference to each file, so deleting the file from a personal library does not remove it from the Meeting. Anyone who can view the Meeting reads its documents; until the Meeting ends the Chair and the Secretary add documents from the computer or their personal library, attach them to an Agenda Item, unlink them from an item and remove them from the Meeting (UC-35a, UC-35b, UC-36a, UC-36b, UC-36c, UC-36d, BR-079, BR-080).

This screen allows users who can view the meeting (Member, Approver, Chair, Secretary, Participant, and a verified Guest) to:

- View Meeting Documents: view the documents grouped by Agenda Item, by Vote or general, each with file name, page count when known and number of comments (UC-35a).

- Switch Grouping: choose By meeting content, Voting or General documents (UC-35a).

- Open Document: click a document to open it in the Document Viewer with preview and comments (UC-35b).

On the screen, the Chair and the Secretary can also:

- Add Document: click “Thêm tài liệu chung” [Add general document] to add files from the computer or the personal library through the Add Document to Meeting Dialog (UC-36a).

- Attach to Agenda Item: click the paperclip of an Agenda Item block to attach Meeting documents, files from the computer or files from the personal library to that item (UC-36b).

- Unlink or Remove Document: use the row buttons Remove from this item [Gỡ khỏi mục này], shown under an Agenda Item, and Remove from the meeting [Gỡ khỏi cuộc họp]; both ask for confirmation (UC-36d, UC-36c).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link “‹ Meetings” [‹ Cuộc họp], meeting title with status chip (e.g. Draft [Nháp], In Progress [Đang họp]) and confidentiality chip (e.g. Normal [Thường]); second line: planned date and time and mode · location (e.g. “Thursday, 15/10/2026 · 08:30–09:30 · Hybrid · Room 2.1, floor 2”). |
| Tabs | Documents [Tài liệu] is selected. Other tabs: General Information, Participants, Meeting Content, Voting, Recordings, Summary, Minutes. |
| Grouping [Theo nội dung họp / Biểu quyết / Tài liệu chung] | Segmented control: By meeting content (default), Voting and General documents; each segment shows its number of documents, a document counted once even when it is under several items (UC-35a). On a phone the labels shorten to “Nội dung” and “Chung”. |
| Agenda Item block | Collapsible block per Agenda Item, numbered by its place in the agenda list rather than the stored position, with title and number of documents; a block that has documents is open and an empty one is folded. Tooltip: “A document can be attached to several items.” [Một tài liệu có thể gắn ở nhiều mục.]. |
| Attach to item | Paperclip button on each Agenda Item block, Chair and Secretary only, tooltip “Thêm tài liệu cho mục này” [Add documents to this item]; opens the Add Document to Meeting Dialog (7.4) for that item, with the sources In the meeting [Trong cuộc họp], From the computer [Tải từ máy] and From my library [Từ kho của tôi] (UC-36b). |
| Document row | File type badge, file name, page count and number of comments, e.g. “Ke-hoach-kinh-doanh-2027.pdf · 14 pages · 4 comments”. The page count is shown only when it is known (a PDF whose page count was recorded) and is left out otherwise. Who added the document and when are shown in a tooltip on the name. Clicking the row opens the Document Viewer (7.5). A Download [Tải về] button follows the name unless the Meeting is Secret (BR-081); on a phone the row buttons move under the name together. |
| Vote documents | In the Voting group, one block per Vote with its title and status chip, in the order the Votes were created; a Draft Vote is listed only for the Chair and the Secretary (BR-086, BR-087). Each document shows Read [Đã đọc], with the confirmation time in a tooltip, once the user has confirmed reading it, and Not read [Chưa đọc] only while the user can still confirm it as a voter of the Open Vote in an In Progress Meeting (UC-42). Documents are attached to a Vote on the Voting tab (UC-68), not here. Empty state: “Chưa có biểu quyết nào có tài liệu.” [No Vote has documents yet.] |
| Row actions [Gỡ khỏi mục này / Gỡ khỏi cuộc họp] | Icon buttons for the Chair and the Secretary until the Meeting ends: Remove from this item [Gỡ khỏi mục này], under an Agenda Item only, unlinks the document from that item (UC-36d); Remove from the meeting [Gỡ khỏi cuộc họp] removes the document from every item. Both ask for confirmation; its comments are kept as history but no longer listed (UC-36c, BR-074, BR-075, BR-076). |
| Add document [Thêm tài liệu] | Button “Thêm tài liệu chung” [Add general document], Chair and Secretary only; opens the Add Document to Meeting Dialog (7.4) to add files from the computer or the personal library. |
| Access rules | Anyone who can currently view the Meeting reads its documents, including the Approver and a verified Guest while the Meeting is Published or In Progress (BR-055). There is no separate reading permission (BR-079, BR-080). After the Meeting ends every add, link and remove control is hidden and the documents are read-only (BR-070, BR-071, BR-079, BR-080). |

#### 7.4 Add Document to Meeting Dialog

Figure 53: Add Document to Meeting Dialog

The Add Document to Meeting Dialog opens from “Thêm tài liệu chung” on the Documents tab, from the paperclip of an Agenda Item block, or for a Draft Vote on the Voting tab. Its source switch offers From the computer [Tải từ máy] and From my library [Từ kho của tôi], and also In the meeting [Trong cuộc họp] when it was opened for an Agenda Item or a Vote. A document selected from the personal library stays in that library; a file uploaded from the computer is also stored in the uploader’s personal library, in the folder of the meeting. Files from the computer are queued and sent one at a time, so the dialog closes at once and a tray in the corner follows the uploads; documents chosen from the library or the meeting are added together, or none is added (UC-36a, UC-36b, UC-68a, BR-070, BR-071).

This screen allows the Chair or the Secretary to:

- Select Documents from Library: on From my library, open the folders and tick one or more files of his/her personal library (UC-36a).

- Upload from Computer: drop or choose up to 10 PDF or DOCX files at a time (UC-36a). The extension must be .pdf or .docx and must match the file’s content; a file of the wrong type, an empty file, a file over 10 MB or a file name that cannot be used is named in the error and that file is not added (MSG164). A PDF must also open without a password and contain at least one page; the system counts its pages itself.

- Add Documents: click “Add <n> documents” to attach the selected files to the Meeting (UC-36a).

On the screen, s/he can also:

- Cancel: click “Huỷ” [Cancel] to close the dialog without adding anything; when files are already chosen, the dialog asks before discarding them.

Field Description

| Field Name | Description |
| --- | --- |
| Dialog title | “Thêm tài liệu chung” [Add general document], “Thêm tài liệu cho mục <n>” [Add documents to item n] or “Thêm tài liệu cho biểu quyết” [Add documents to the Vote]. |
| Tabs | Segmented control “Lấy tài liệu từ” [Take documents from]: In the meeting [Trong cuộc họp] (for an Agenda Item or a Vote only), From the computer [Tải từ máy] and From my library [Từ kho của tôi]. |
| Library file list | From my library: one folder at a time with a breadcrumb back to the library root; rows with checkbox, file type badge, file name, page count and size. Only the user’s own library files are listed, and the choice is kept while moving between folders. At least one file must be selected (MSG249); a file already in this Meeting is refused with MSG165. The note under the list says the Meeting uses the document’s current name, and renaming it in the library later does not rename it in the Meeting. |
| Upload area | Drop zone “Drag files here or click to choose” [Kéo tệp vào đây hoặc bấm để chọn]. Only PDF or DOCX, at most 10 MB each, and at most 10 files totalling 100 MB per submission; the extension must be .pdf or .docx and must match the file’s content. Before reading any file the system checks the user’s authority: a user who is no longer the Chair or Secretary receives MSG162 whatever the files are, and one who can no longer view the Meeting receives MSG174. A submission of more than 10 files or more than 100 MB, or a request that declares more than 101 MB, shows MSG288 before the files are received; a missing file, an invalid request, a file name that cannot be used, a file of the wrong type, or a PDF that needs a password, has no page or cannot be read shows MSG164 naming the files at fault; in every case nothing is added (MSG162, MSG164, MSG174, MSG288, BR-069). |
| Access notice | Info box: “Ai xem được cuộc họp đều đọc được tài liệu đã thêm.” [Anyone who can view the Meeting can read the added documents.] (BR-079, BR-080) |
| Cancel [Huỷ] | Text button “Huỷ” [Cancel]; closes the dialog, after asking when files are already chosen. |
| Add <n> documents [Thêm <n> tài liệu] | Primary button showing the number of selected files, e.g. “Thêm 1 tài liệu” [Add 1 document], or “Gắn 1 tài liệu” [Attach 1 document] for In the meeting. Files from the computer go to the upload tray and are sent one at a time; a file that fails is reported in the tray, with “Thử lại” [Retry] when retrying can help, and does not stop the others. Documents from the library or the meeting are added together or not at all. |

#### 7.5 Document Viewer – Preview & Comments

Figure 54: Document Viewer – Preview & Comments

The Document Viewer opens when a user clicks a document on the Documents tab, on an Agenda Item or in the Documents panel of the online room; opened from the room, it opens in a new browser tab so the call continues. It renders the document in the browser – a PDF page by page, a DOCX as one continuous scrollable document – with zoom, next to the comments panel. Users who can view the Meeting preview the document and download it unless the Meeting is Secret (UC-35b), and comment and reply until the Meeting ends (UC-38). Opened from a Vote the user has still to cast, it also offers read confirmation once the last page is reached (UC-42).

This screen allows users who can view the meeting (Member, Approver, Chair, Secretary, Participant, verified Guest) to:

- Preview Meeting Document: read the document (a PDF page by page) and zoom in or out (UC-35b).

- View Comments: read the comments with author, page and time, each followed by its replies (UC-38).

- Comment on Document: type a comment and click “Send”; for a PDF it refers to the page being viewed (UC-38).

- Reply to Comment: click “Reply” under a comment; a reply to a reply joins the same thread, one level deep (UC-38).

- Download Document: click “Download” to save the original file; not available in a Secret Meeting, where documents can only be viewed online (UC-35b).

On the screen, s/he can also:

- Back to Documents: click “‹ Documents” to return to the Documents tab of the Meeting, or to close the tab opened from the room.

Field Description

| Field Name | Description |
| --- | --- |
| Back link [‹ Tài liệu] | Returns to the Documents tab of the Meeting; in a tab opened from the room it closes that tab. |
| Document header | Read-only: file type badge, file name (e.g. “Du-thao-nghi-quyet-HDQT.pdf”), confidentiality chip of the Meeting (e.g. Secret [Mật] in red) and the meeting title. |
| Page indicator [Trang 2 / 6] | Read-only current page and total number of pages; updates while scrolling. Shown for PDF documents only. |
| Zoom − / + | Two square buttons that decrease or increase the zoom level of the preview. |
| Download [Tải về] | Secondary button; downloads the original file. In a Secret Meeting it is replaced by the chip “View online only” [Chỉ xem trực tuyến] (BR-081). |
| Preview pane | Read-only rendering made in the browser with no server conversion – PDF pages, or a DOCX as one continuous scrollable document; a comment refers to a page, not to a highlighted passage. A file the browser cannot draw shows MSG383 and a file no longer in storage shows MSG382, both without a retry button (UC-35b). No name or time is stamped on the document, and a Secret document is not given a watermark. Each view of a Secret Meeting’s document is recorded in the audit log (BR-009, BR-010, BR-011). |
| Comments panel [Góp ý] | Panel beside the document with refresh and close buttons. Each comment shows the author, the page of a PDF comment and the time, the text and a “Trả lời” [Reply] link; replies are indented under it, one level deep. A Guest’s comment shows the name the Guest gave with the label Guest [Khách]. Empty state: “Chưa có góp ý nào.” [No comments yet.] (UC-38) |
| Comment box [Viết góp ý…] | Textarea, required to send, at most 4,000 characters (MSG348); for a PDF the option “Gắn trang <n>” [Attach page n] links the comment to the page being viewed, a DOCX comment has no page. Ctrl+Enter sends. Hidden after the Meeting ends, when comments are read-only (BR-079, BR-080, MSG251). |
| Send [Gửi góp ý] | Primary button; posts the comment or reply, which appears in the list and increases the comment count on the Documents tab (UC-38). |

### 8. FE-08 – Online Meeting Room Management

#### 8.1 Meeting Lobby

Figure 55: Meeting Lobby

Figure 56: Meeting Lobby – microphone list open

The Meeting Lobby is the pre-join screen of the Online Meeting Room, laid out like Google Meet. It opens when a user clicks “Join online room” on an In Progress Online or Hybrid meeting, or when a verified Guest continues to the room. The page has its own top bar with the TrustCabinet mark and the signed-in account; the left side shows the camera preview with the microphone and camera switches and the device pickers, the right side the meeting and the Join button (UC-43, UC-45a, UC-45b, UC-45d, UC-04). The second image shows the open device tray (UC-45d) and, when recording applies to the Meeting, the notice that the meeting is recorded for the minutes and the recording is deleted automatically after 180 days.

This screen allows the Chair, Secretary, Participant and a verified Guest to:

- Preview Camera and Microphone: see the camera preview and a voice level indicator while the microphone is on (UC-45a, UC-45b).

- Set Initial Microphone and Camera State: switch the microphone (on by default) and the camera (off by default) before entering; the browser remembers the choice for the next time (UC-45a, UC-45b).

- Select Devices: choose the microphone, speaker and camera (UC-45d).

- Join Meeting Room: click “Join room” to enter the Online Meeting Room – Video Grid (UC-43).

On the screen, s/he can also:

- Back to the Meeting: click “‹ Back to the meeting page” [Quay lại trang cuộc họp].

Field Description

| Field Name | Description |
| --- | --- |
| Top bar | TrustCabinet mark on the left; full name, email and initials avatar of the signed-in user on the right. |
| Camera preview | Dark rounded area showing the selected camera; “Camera is off” [Camera đang tắt] when the camera is off or not permitted by the browser. |
| Microphone / Camera switches | Two round icon buttons on the bottom edge of the preview, with tooltips such as “Turn off microphone” [Tắt micro]; a switched-off device is shown in light red. A voice level indicator shows while the microphone receives sound. |
| Device pickers [Micro / Loa / Camera] | Three pill buttons under the preview, each opening the devices detected by the browser (e.g. “MacBook Pro Microphone”). Without browser permission they read e.g. “No permission to use the microphone, or no microphone” and are disabled; the Speaker picker is hidden in browsers that cannot choose an audio output. In the room the same pickers open from the arrow beside the microphone and camera buttons (UC-45d). |
| Heading | “Ready to join?” [Sẵn sàng tham gia?] followed by the meeting title. |
| Confidentiality chip | Shown only for a Secret Meeting. |
| Room presence | e.g. “6 people in the room” [Đang có 6 người trong phòng], or “No one else is here” [Không có người nào khác ở đây]. |
| Vào phòng [Join room] | Primary pill button; reads “Joining…” while connecting. The system re-checks that the user can still view the Meeting, that it is In Progress and that it has an online room (BR-025, BR-052, BR-053, BR-054, BR-044). On success the user enters the Video Grid with the chosen devices. Pressing Join while the room cannot be reached (never entered) keeps the lobby and shows MSG480 under the button; pressing Join tries again (UC-43 EX-11). |
| Room unavailable message | When the Meeting has not started, the page waits with “The room opens only while the meeting is in progress.” [Phòng họp chỉ mở khi cuộc họp đang diễn ra.] and opens the lobby by itself when the Meeting starts. When the Meeting has ended or was cancelled, or is held in person only, the page shows only a message and “Back to the meeting page”; after a dropped connection it also offers “Rejoin” [Vào lại]. Device-permission messages show once in the lobby; {device} is “micro và camera” when both are blocked; the listen sentence is omitted when only the camera is blocked (MSG334–MSG337). |

#### 8.2 Online Meeting Room – Video Grid

Figure 57: Online Meeting Room – Video Grid

Figure 58: Online Meeting Room – call in a floating window

The Online Meeting Room – Video Grid is the full-screen main view of the online room, laid out like Google Meet: a one-line blue header with a gold bottom edge, a navy stage with the video tiles, the call controls centred at the bottom as icons with tooltips, and the People and Chat panel buttons at the bottom right. The header back button opens the Meeting detail without leaving the call. Leaving the room does not change the meeting lifecycle; only the Chair ends the meeting (UC-43, UC-44, UC-45a, UC-45b, UC-46, UC-49, UC-50). The second image shows the call in the floating window (UC-49): the main window says that the meeting is in the floating window and offers to bring it back.

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- View Video Grid: see the camera image or initials of the people in the room, with the current speaker highlighted, muted people marked and the Chair tagged; the grid pages when more people are connected than fit (UC-46).

- Manage Microphone: turn his/her own microphone on or off (Ctrl+D) and choose the microphone and speaker (UC-45a, UC-45d).

- Manage Camera: turn his/her own camera on or off (Ctrl+E) and choose the camera (UC-45b, UC-45d).

- Leave Meeting Room: click “Leave” and confirm to disconnect, close the room browser tab and show the Meeting detail, without ending the meeting (UC-44).

On the screen, s/he can also:

- Share Screen: present a screen, window or tab to everyone (8.4, UC-45c).

- Open Floating Window: move the call into a small picture-in-picture window to work in other tabs while staying in the room (UC-49).

- Raise Hand: ask for the floor and, when the Chair invites the person, start and finish speaking (UC-50).

- Open Side Panels: open the Participants panel (8.3) or the Chat panel (8.4).

Field Description

| Field Name | Description |
| --- | --- |
| Room header | Back button ‹ (asks for confirmation, as Leave does), meeting title, chip “6 people in the room” [6 người trong phòng] and the confidentiality chip for a Secret Meeting. |
| Video tile | Camera image, or a circle with the user’s initials when the camera is off; name label with a crossed microphone when muted, “(Bạn)” [You] for the local user and the gold tag “Chủ toạ” [Chair]. |
| Active speaker highlight | Blue border around the tile of the person currently speaking. |
| Pager ‹ 1 / 2 › | Shown when more people are connected than the stage can display. |
| Microphone [Tắt micro / Bật micro] | Icon button, red when off; the arrow ⌃ beside it opens the microphone and speaker pickers. A user muted by the Chair is told so and may turn the microphone on again (UC-45a, UC-47). |
| Camera [Tắt camera / Bật camera] | Icon button, red when off; the arrow ⌃ opens the camera picker (UC-45b). |
| Share screen [Chia sẻ màn hình] | Icon button, highlighted while sharing (8.4, UC-45c). |
| Floating window [Mở cửa sổ nổi] | Icon button in browsers that support it; while the call is in the floating window the stage shows “The meeting is in a floating window” with “Bring the meeting back to this window”. |
| Raise hand [Giơ tay] | Icon button; the raised hand shows on the person’s tile and in the Participants panel. The Chair sees the queue of raised hands in order and invites one person at a time to speak (UC-50). |
| Leave [Rời phòng] | Red button. Confirmation “Leave the meeting room – The meeting continues with the others. You can rejoin while the meeting is in progress.” with Stay [Ở lại] and Leave [Rời phòng]. The meeting stays In Progress; when the Chair ends the meeting every connected user is disconnected (UC-44, BR-025). |
| Participants / Chat [Người dự / Chat] | Icon buttons at the bottom right that open the side panels (8.3, 8.4); Chat shows the number of unread messages. |
| Connection banners | “Connection lost, reconnecting…” [Mất kết nối, đang kết nối lại…] while reconnecting (MSG333); when the connection cannot be restored the room shows MSG332 with Rejoin — only for a drop while already in the room (a Join press that never connected stays in the lobby with MSG480). A banner with a button appears when the browser blocks audio playback. |

#### 8.3 In-Room Participants Panel

Figure 59: In-Room Participants Panel

Figure 60: In-Room Participants Panel – attendance

The In-Room Participants Panel is the white 360 px panel on the right of the online room, opened with the Participants icon at the bottom right. It lists the users currently connected to the room with their meeting role. Every connected user can view the list; only the Chair can mute another user (UC-46, UC-47). A separate Attendance [Điểm danh] panel beside Participants and Chat lets the Secretary record attendance without leaving the room (UC-33).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Meeting Room Participants: see the number and the list of users in the room, sorted by meeting role (Chair, Secretary, Member, Guest) and then by name, with who is speaking and whose microphone is off (UC-46).

On the screen, the Chair can also:

- Mute a Participant: click “Mute” next to a user whose microphone is on (UC-47).

On the screen, the Secretary can also:

- Record Attendance: open the Attendance [Điểm danh] panel and choose Present [Có mặt] or Absent [Vắng] for a person; each choice is saved at once (UC-33). Other users read the attendance in the same panel.

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Người dự (6)] | Title with the number of users currently connected (UC-46). |
| Participant row | Initials avatar, full name with “(Bạn)” [You] for the local user, and the meeting role below: Chủ toạ [Chair], Thư ký [Secretary], Thành viên [Member], Khách mời [Guest]. |
| Speaking indicator | Green dot while the user is speaking. |
| Microphone state | Microphone icon; shown red and crossed out when the microphone is off. |
| Tắt mic [Mute] | Outline button shown only to the Chair, on other users whose microphone is on. The user is told “The Chair has muted your microphone. You can turn it back on when you need to speak.” (UC-47). |
| Not provided | Muting everyone and removing a user from the room are not provided; raising a hand is UC-50. |
| Attendance panel [Điểm danh] | Opened with its own icon at the bottom right, beside Participants and Chat. One row per current internal participant and Guest, ordered Chair, Secretary, Member, Guest and then by name: avatar, full name, meeting role and the Attendance cell. The Secretary sees the dashed dropdown “Điểm danh” (never recorded), “Có mặt” or “Vắng”; everyone else reads a dot and text, or “Chưa điểm danh”. Same data, same cell and same rules as the Attendance column of the Participants tab (III.6.3): no Save button, no Present/Absent totals, joining the room never marks anyone Present; MSG151, MSG150 and MSG289 as in III.6.3. “Bạn không còn xem được điểm danh của cuộc họp này.” when access is lost. |

#### 8.4 Screen Sharing & Chat

Figure 61: Screen Sharing & Chat

The Screen Sharing & Chat layout appears when a connected user presents his/her screen and/or opens the Chat panel. The shared screen takes the main stage and the video tiles move to a strip beside it. Chat lives only in the room: messages are seen by the people currently in the room and are not kept after the meeting (UC-45c, UC-48).

This screen allows the Chair, Secretary, Participant and a verified Guest connected to the room to:

- Manage Screen Sharing: start or stop presenting a screen, window or tab; one person presents at a time, and when someone else starts, the previous presenter’s share stops and that user is told (UC-45c).

- View Shared Screen: follow the screen shared by another user on the main stage (UC-45c).

- Use In-room Chat: read the messages sent since joining and send a text message to everyone currently in the room (UC-48).

On the screen, s/he can also:

- Close the Chat Panel: click the Chat icon again to return to the full stage.

Field Description

| Field Name | Description |
| --- | --- |
| Shared screen stage | Main area showing the shared screen; caption “Phạm Thị Lan is presenting” [Phạm Thị Lan đang trình bày]; the presenter sees “You are presenting to everyone” [Bạn đang trình bày cho mọi người]. |
| Video tile strip | Video tiles beside the shared screen, as in the Video Grid. |
| Share screen [Chia sẻ màn hình / Dừng chia sẻ] | Toolbar toggle, highlighted while sharing; the browser asks which screen, window or tab to share (UC-45c). |
| Chat panel title | “Chat”. |
| Chat notice | “Messages are visible only to people in the room and are not kept after the meeting.” [Tin nhắn chỉ hiện với người đang trong phòng và không được lưu lại sau cuộc họp.] |
| Message list | Messages in time order with sender (“Bạn” [You] for the user’s own messages) and time hh:mm; “No messages yet.” [Chưa có tin nhắn nào.] when empty. A user who joins later does not see earlier messages. |
| Message box | Textarea “Type a message — Enter to send, Shift+Enter for a new line”, at most 1,000 characters with a counter (e.g. “0/1000”). |
| Gửi [Send] | Primary button, enabled when the message is not blank. If sending fails: “Could not send the message. Please try again.” [Không gửi được tin nhắn. Vui lòng thử lại.] |

#### 8.5 In-Room Voting Panel

Figure 62: In-Room Voting Panel

The In-Room Voting Panel opens from the Voting icon next to Participants and Chat and shows the Voting tab of the meeting detail page in a side panel. When the Chair opens a Vote, everyone in the room who has not voted in it sees a notice with “Vote” [Bỏ phiếu] that opens the panel (UC-62b, UC-65).

This screen allows users connected to the room (Chair, Secretary, Participant, Guest) to:

- View Vote Status: see the open Vote, when it opened, whether the ballot is secret and how many eligible voters have voted (UC-62b).

- Cast Vote: choose one option and submit the ballot once; available only to users on the eligible list fixed when the Vote opened, i.e. current holders of the meeting roles the Chair selected, verified Guests included when Guest is selected (UC-65, BR-091).

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Biểu quyết] | Title of the panel; label “Planned” [Chưa làm] in the wireframe. |
| Vote status line | Status chip “Open” [Đang mở] (green) followed by “Opened at 13:58 · secret ballot”. A Vote belongs to the Meeting, not to an Agenda Item. |
| Vote question | Read-only bold text, e.g. “Thông qua kế hoạch kinh doanh năm 2027”. |
| Ballot progress | “Voted 7/11” [Đã bỏ phiếu 7/11]: ballots cast out of eligible voters; no option results while the Vote is open, and for a secret ballot no indication of who has just voted. |
| Vote options | Radio cards, exactly one may be selected: Đồng ý [Agree], Không đồng ý [Disagree], Không ý kiến [Abstain]. |
| Gửi phiếu [Submit vote] | Primary button, shown only to eligible users who still have access and have not voted. Hint: “A submitted ballot cannot be changed or withdrawn.” After submission the panel only confirms the ballot (BR-090, BR-092, BR-093, BR-091). |

#### 8.6 In-Room Documents Panel

Figure 63: In-Room Documents Panel

The In-Room Documents Panel opens from the Documents icon in the bottom bar of the online room. It shows the Documents tab in a room layout and is read-only: the current Agenda Item comes first with its documents open and the other items folded, and a document opens in the Document Viewer in a new browser tab so the call is not interrupted (UC-35a, UC-35b). On a phone the panel sits behind the ⋯ menu together with Meeting content and Attendance.

This screen allows users connected to the room (Chair, Secretary, Participant, and a verified Guest) to:

- View Meeting Documents: see the documents of the current Agenda Item first, the other items and the Votes with documents collapsed, and the general documents (UC-35a).

- Open Document: click a document to read it in the on-page viewer without leaving the room — it closes with the ✕ button, Esc or Back; the viewer there has the comments panel (UC-35b, UC-38).

Field Description

| Field Name | Description |
| --- | --- |
| Panel title [Tài liệu (5)] | Title with the number of Meeting documents, each counted once. |
| Online-only chip [Chỉ xem trực tuyến] | Shown for a Secret Meeting, whose documents cannot be downloaded (BR-081). |
| Current item group | “Current item · 2. Báo cáo kế hoạch kinh doanh 2027” [Mục đang bàn]; follows the Chair when the current Agenda Item changes. |
| Document row | File type badge, file name, page count and number of comments; tooltip “Opens in a new tab · you stay in the room” [Mở ở tab mới · bạn vẫn ở trong phòng]. |
| Other items [Mục khác] | Collapsed rows with order number, title and number of documents; expanding one lists its documents. |
| Votes [Biểu quyết] | Shown when a Vote the user can see has documents: one collapsed block per Vote with its status and documents, each with Read [Đã đọc] or Not read [Chưa đọc] as on the Documents tab (7.3). |
| General documents [Tài liệu chung] | Documents linked to no Agenda Item and to no Vote; a document of a Vote is listed in the Votes group only. |
| Read-only rule | No add, link or remove actions in the room; documents added from the Documents tab appear in the panel without reloading. To show a page to everyone, the presenter shares the screen (8.4). |

### 9. FE-09 – Meeting Recording

#### 9.1 Meeting Detail – Recordings Tab

Figure 64: Meeting Detail – Recordings Tab

The Meeting Detail – Recordings tab [Bản ghi] is the media library of a meeting. TrustCabinet never records meetings itself: after the meeting has Ended, the Secretary uploads the audio file(s) recorded outside the system, the system transcribes each file and shows the speaker-attributed transcript here, and the Secretary then generates the meeting summary from it. Authorized users of the meeting can view the media library and play the audio; the transcript and the upload controls are available only to the assigned Secretary (UC-51a, UC-51b, UC-52, UC-54, UC-55, UC-58).

This screen allows the Secretary to:

- View Media Library: see the audio files uploaded for the meeting with duration, size and uploader (UC-51a).

- Upload Meeting Audio: drag and drop an audio file or click “Choose file” to open the Upload Audio Dialog (UC-52).

- View Processing Status: see whether the transcript of a file is being processed, processed or failed, and reprocess a failed file (UC-54).

- View Speaker-Attributed Transcript: read the transcript segments with time, speaker and text (UC-54).

- Correct Speaker Attribution: change the speaker of a segment directly in its dropdown (UC-55).

- Generate Meeting Summary: click “Generate summary from transcript” to have AI create a draft summary (UC-58).

On the screen, s/he can also:

- Open the Full Transcript Editor: go to the Speaker-Attributed Transcript screen to play the audio, search, edit text and assign unknown speakers (UC-51b, UC-54, UC-55).

- Navigate Detail Tabs: switch to the other meeting detail tabs.

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Back link “‹ Cuộc họp”, meeting title with status chip (e.g. Đã kết thúc [Ended]) and confidentiality chip (e.g. Thường [Normal]); second line: actual time and date “08:32–09:41 · 15/10/2026” and mode · location “Kết hợp · Phòng 2.1, tầng 2” [Hybrid · Room 2.1, floor 2]. |
| Tabs | Thông tin chung, Thành phần tham dự, Nội dung cuộc họp, Tài liệu, Biểu quyết, Bản ghi [Recordings] (selected), Tóm tắt [Summary], Biên bản [Minutes]. |
| Transcript panel header | “Transcript · hop-sprint6.m4a”: the file whose transcript is shown, with a processing status chip: Đang xử lý [Processing], Xử lý xong [Processed] (green) or Lỗi [Failed] with a “Xử lý lại” [Reprocess] button. |
| Transcript segment | One row per segment: start time (hh:mm:ss, e.g. 00:00:12), speaker dropdown and the transcribed text. Transcription runs on the uploaded file after upload; there is no real-time transcription and no translation. |
| Speaker dropdown | Lists the meeting’s internal participants and Guests; changing it re-assigns that segment (UC-55). A voice not yet identified is shown as “Người nói 3” [Speaker 3] with an amber border. |
| Transcript footer | Read-only: “Đoạn 1–5 trong 214” [Segments 1–5 of 214] and “Sửa lần cuối 15/10 10:20” [Last edited]. |
| Upload drop zone | Dashed area “Kéo thả file audio vào đây” [Drag and drop an audio file here], hint “MP3, M4A, WAV · tối đa 300 MB”, button “Chọn file” [Choose file] that opens the Upload Audio Dialog. Shown only to the Secretary and only after the meeting has Ended (UC-52). |
| File đã tải lên [Uploaded files] | Media library list; each file shows the name (e.g. hop-sprint6.m4a) and “1 giờ 09 phút · 64 MB · Đỗ Hải Yến” [duration · size · uploaded by] (UC-51a). Audio files only – the system stores no video. |
| Rejected file message | Red inline box for a file that failed validation, e.g. “ghi-am-phong.wma: định dạng không hỗ trợ. Chọn file MP3, M4A hoặc WAV.” [unsupported format. Choose an MP3, M4A or WAV file.]; files over 300 MB are rejected in the same way. The user can choose another file. |
| Tạo tóm tắt từ transcript [Generate summary from transcript] | Primary button, enabled when at least one transcript is Processed. Sends the edited transcript to the AI provider to create a Draft summary and opens the Meeting Summary tab (UC-58). |
| Visibility rule | Other authorized users of the meeting (Member, Approver, Chair, Participant, System Admin) see only the media library and can play the audio (UC-51a, UC-51b); the transcript, upload and summary actions are shown only to the assigned Secretary. |

#### 9.2 Upload Audio Dialog

Figure 65: Upload Audio Dialog

The Upload Audio Dialog [Tải audio lên] is opened by the Secretary from the Recordings tab with “Choose file” or by dropping a file on the upload area. Because the system never records meetings, this dialog is the only way to add a recording: the Secretary chooses an audio file (no video), confirms that attendees were informed of and agreed to the recording, and uploads it. After a successful upload the system starts transcription automatically and the progress is shown on the Recordings tab (UC-52).

This screen allows the Secretary to:

- Select Audio File: drag a file into the drop area or click it to choose an MP3, M4A or WAV file of up to 300 MB (UC-52).

- Confirm Recording Consent: tick the confirmation that attendees were informed and consented to the recording (UC-52).

- Upload Meeting Audio: click “Upload” to send the file and follow the upload progress (UC-52).

- Cancel: close the dialog without uploading, or stop an upload in progress.

Field Description

| Field Name | Description |
| --- | --- |
| Tải audio lên [Upload audio] | Dialog title. The dialog is available only to the assigned Secretary of the meeting and only when the meeting is Ended. |
| Drop zone | Dashed area “Kéo tệp vào đây hoặc bấm để chọn” [Drag a file here or click to choose] with the hint “MP3, M4A, WAV · tối đa 300 MB”. Accepts one audio file at a time. |
| File validation | A file with another format (e.g. .wma or any video file) or larger than 300 MB is rejected immediately with an inline error naming the file and the accepted formats; the user can choose another file. |
| Selected file row | File name and size (e.g. “hop-sprint6.m4a · 64 MB”) with a progress bar and percentage (e.g. 64%) while uploading. |
| Recording consent [Xác nhận đồng ý ghi âm] | Checkbox, required, unticked by default: “Tôi xác nhận người tham dự đã được thông báo và đồng ý ghi âm theo Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP.” [I confirm that attendees were informed and consented to the recording under Law 91/2025/QH15 on Personal Data Protection and Decree 356/2025/ND-CP.] Required because the recording is personal data. |
| Huỷ [Cancel] | Ghost button; closes the dialog and discards the selected file; an upload in progress is cancelled. |
| Tải lên [Upload] | Primary button, enabled when a valid file is selected and the consent box is ticked. Uploads the file, adds it to the media library with uploader and upload time, and starts transcription automatically (status Processing on the Recordings tab). If the upload fails, an error is shown and the Secretary can try again. |

### 10. FE-10 – Transcription, Speaker Identification & Meeting Summary

#### 10.1 Speaker-Attributed Transcript

Figure 66: Speaker-Attributed Transcript

The Speaker-Attributed Transcript screen is the transcript editor inside the Recordings tab, used by the Secretary after the system has transcribed an uploaded audio file. The transcript is split into timed segments; the system separates the voices as “Speaker 1, 2, …” and may suggest a name for each voice, but never assigns a person by itself: the Secretary confirms the suggestion or assigns each unknown voice to a person from the meeting attendee list – once for all segments of that voice – and corrects the wording before generating the summary. There is no real-time transcription and no translation (UC-51b, UC-54, UC-55, UC-58).

This screen allows the Secretary to:

- Select Audio File: choose which uploaded file’s transcript to work on (UC-54).

- Play Meeting Audio: play, pause and seek the audio, or click a segment time to listen to exactly that segment (UC-51b).

- View Speaker-Attributed Transcript: read the segments with start time, speaker and text, and search the transcript (UC-54).

- Edit Transcript Text: click a segment to correct its text (UC-55).

- Assign Speaker: confirm the name the system suggests for a voice, assign an unknown voice to an attendee for all of its segments, or change the speaker of a single segment (UC-55).

- Generate Meeting Summary: click “Generate summary” to create the AI draft summary from the edited transcript (UC-58).

Field Description

| Field Name | Description |
| --- | --- |
| Audio file selector | Dropdown listing the uploaded audio files of the meeting, e.g. “hop-sprint6.m4a”; default the latest file. |
| Processing status chip | Đang xử lý [Processing] / Xử lý xong [Processed] / Lỗi [Failed]. Editing is possible only when the file is Processed. |
| Tìm trong transcript [Search in transcript] | Text input; filters or highlights segments containing the keyword. |
| Tạo tóm tắt [Generate summary] | Primary button; sends the current edited transcript to the AI provider and opens the Meeting Summary tab with a new Draft (UC-58). |
| Audio player | Play/pause button, current position (e.g. 00:14:02), seek bar and total duration (e.g. 01:09:12) (UC-51b). |
| Segment time | Start time of the segment (hh:mm:ss); clicking it moves the player to that point and plays it. The segment being played is highlighted. |
| Speaker dropdown | Per segment; lists the meeting’s internal participants and Guests. Changing it affects only that segment. A name the system filled in automatically is shown as assigned and marked as filled in by the system, and the Secretary may change or clear it. An unassigned voice is shown as “Người nói 3” [Speaker 3] with an amber border (UC-55). |
| Segment text | Transcribed text; click to edit inline, saved when leaving the field. The last-edit time is updated (UC-55). |
| Gán “Người nói 3” [Assign “Speaker 3”] card | Opened when an unassigned voice is selected; hint “Chọn trong danh sách tham dự. Áp dụng cho mọi đoạn của giọng này (12 đoạn).” [Choose from the attendee list. Applies to all segments of this voice (12 segments).] Radio list of attendees, e.g. “Ngô Thanh Vân · khách” [Guest], “Phạm Đức Long”, “Lê Thu Hà”; one must be selected. |
| Gán cho 12 đoạn [Assign to 12 segments] | Primary button; assigns the selected attendee to every segment of that voice in one step and refreshes the transcript (UC-55). |
| Visibility rule | Viewing and editing the transcript is limited to the assigned Secretary of the meeting (permission matrix, Transcript – View / Edit). |

#### 10.2 Meeting Summary

Figure 67: Meeting Summary

The Meeting Summary card [Tóm tắt] on the Biên bản tab shows the AI draft of the Meeting's content, written from the Meeting's own record, and the review that makes it usable. The draft always has three sections — I. Mục tiêu và phạm vi cuộc họp, II. Nội dung thảo luận và đánh giá, III. Kết luận và phân công thực hiện — and is always labelled as written by AI. Only the current Chair and Secretary see and correct it; after they approve it the other participants read it. Correcting it withdraws the approval, and the minutes use only an approved draft. Vote results are copied from the data and are never written by the model; action items stay plain text with no assignee or deadline. There is no publish step (UC-58, UC-59, UC-60, UC-61).

This screen allows the current Chair and the current Secretary to:

- View Meeting Summary: read the three sections, the label saying the text was written by AI, the model and time, who asked for it, who approved it and who last corrected it (UC-59).

- Generate Meeting Summary: click “Tạo dự thảo” [Generate summary], or “Tạo lại” [Regenerate] over an existing draft, to queue a new draft from the Meeting's transcript (UC-58). The page asks again every 5 seconds while the AI writes.

- Edit Meeting Summary: correct the text of the three sections before approving it (UC-60).

- Approve Meeting Summary: click “Duyệt dự thảo” [Approve draft] to vouch for the content; the other participants can read it from then on (UC-61).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: back link, Meeting title, status chip Đã kết thúc [Ended], confidentiality chip, actual time and place. |
| Nhãn AI [AI label] | Read-only chip “Nội dung do AI tạo — cần rà soát” [AI-written — needs review] on every draft, reviewed or not. |
| Trạng thái dự thảo [Draft status] | Read-only: Chờ AI [Queued], AI đang viết [Running], Đã có dự thảo [Ready], Viết không được [Failed] with the reason, and an empty state when there is none. |
| Tạo dự thảo / Tạo lại [Generate summary / Regenerate] | Primary button, shown to the current Chair and Secretary while the Meeting is InProgress or Ended. Queues the request; the earlier draft stays readable while the AI writes, and the page asks again every 5 seconds. |
| Ba mục [Sections] | Three text boxes — I. Mục tiêu và phạm vi cuộc họp, II. Nội dung thảo luận và đánh giá, III. Kết luận và phân công thực hiện — limited to 2,000, 12,000 and 8,000 characters. |
| Sửa dự thảo [Edit draft] | Secondary button on a finished draft; saving withdraws the review. |
| Lưu [Save] | Saves the three sections and withdraws the review, so the other participants stop seeing the draft until it is approved again. |
| Duyệt dự thảo [Approve draft] | Secondary button on a finished draft; records the reviewer and the time. From then on everyone who may read the Meeting reads the draft. |
| Người duyệt và người sửa [Reviewer and editor] | Read-only: who approved the draft and when, and who last corrected it and when. |
| Máy AI và thời điểm [Model and time] | Read-only: the model name, the time the draft was written and the person who asked for it. |
| Nguồn từng phần [Sources] | Read-only, for each part of section II: the agenda item title and the stretch of the Meeting the part was written from, e.g. “09:12–09:31 · 42 câu”, to check against the Bản ghi tab. |
| Cơ sở dự thảo [What the draft is based on] | Read-only, shown to the current Chair and Secretary before asking: sentences recorded, voices not yet named, transcripts running or failed, agenda items and how many were timed, closed votes. |
| Quyền xem [Visibility rule] | Read-only note: before approval only the current Chair and Secretary see the draft; after approval everyone who may read the Meeting sees it read-only; correcting it withdraws the approval. |
| Không có bước công bố [No publish step] | No Publish button: the summary is not published outside the Meeting. |

### 11. FE-11 – Voting Management

Scope: create/open/cast only while Meeting is InProgress. End Meeting is independent of Vote closure. Chair can close and tally existing Open Votes in InProgress or Ended, then record the conclusion. No post-meeting ballot casting, time-boxed voting or quorum. (BR-026, BR-090, BR-092, BR-093)

#### 11.1 Meeting Detail – Voting Tab

Figure 68: Meeting Detail – Voting Tab

The Meeting Detail – Voting tab [Biểu quyết] lists every Vote that belongs directly to the selected Meeting. It remains readable after Ended; casting stops at Meeting end. While InProgress, the header shows the actual start time, elapsed time and the current Agenda Item. The Chair creates/opens during InProgress, and may close outstanding Open Votes during InProgress or after Ended; other users involved in the Meeting see the same list read-only. Votes are never held before or after the Meeting and have no deadline or quorum (UC-62a, UC-62b, UC-63, UC-64, UC-66).

This screen allows the Chair to:

- View Meeting Votes: view all Votes of the Meeting with status, linked Agenda Item, ballot type and opening time (UC-62a).

- View Vote Status: follow the turnout of an open Vote (ballots cast / eligible voters) in real time (UC-62b).

- Edit Draft Vote: click “Edit” on a Draft Vote to change its content, options or ballot type in the Create & Open Vote Dialog (UC-63).

- Delete Draft Vote: click “Delete” on a Draft Vote to remove it; an open or closed Vote cannot be deleted (UC-63).

- Open Vote: click “Open vote” on a Draft Vote so that eligible people can cast their ballots (UC-64).

- Close Vote: click “Close voting” on an open Vote to stop balloting; the system tallies the result and the Vote moves to Vote Result & Conclusion (UC-66).

On the screen, s/he can also:

- Create Vote: click “Create vote” to open the Create & Open Vote Dialog for the current Agenda Item (UC-63).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: Meeting title, status chip InProgress [Đang họp], confidentiality chip (e.g. Normal [Thường]), actual start time and elapsed time (“Bắt đầu 08:32 · đã họp 22 phút”) and the current Agenda Item (“Mục hiện tại: 2. …”). |
| Tabs | General Information · Participants · Meeting Agenda · Documents · Voting · Recordings · Summary · Minutes [Thông tin chung · Thành phần tham dự · Nội dung cuộc họp · Tài liệu · Biểu quyết · Bản ghi · Tóm tắt · Biên bản]; Voting is selected. |
| Vote count [n biểu quyết] | Read-only number of Votes that belong to the Meeting. |
| Create vote [Tạo biểu quyết] | Primary button, visible only to the current Chair and enabled only while the Meeting is InProgress (BR-090, BR-092, BR-093). Opens the Create & Open Vote Dialog. |
| Vote status chip | Draft [Nháp] (dashed card), Open [Đang mở], Closed [Đã đóng]; after the Chair records a conclusion the Vote shows its conclusion. Lifecycle: Draft → Open → Closed → Concluded. |
| Vote content | Bold text of the motion, e.g. “Thông qua phạm vi biểu quyết chỉ trong cuộc họp”. |
| Vote meta | Read-only: linked Agenda Item (“Mục 2”), ballot type public [công khai] or secret [bỏ phiếu kín] and opening time (“mở 08:51”). |
| Turnout bar [Đã bỏ phiếu] | Progress bar with “cast / eligible” count (e.g. 3/5), shown for an open Vote and updated as ballots arrive. Individual choices are not shown while the Vote is open. |
| Eligibility note | Read-only text, e.g. “5 người đủ điều kiện: Chủ trì, Thư ký, Thành viên, Khách mời.” Counts the current holders of the roles selected for this Vote; the list is fixed when the Vote opens, and attendance does not decide eligibility (BR-090, BR-055, BR-091). |
| Close voting [Đóng phiếu] | Chair only; close and tally an existing Open Vote while Meeting is InProgress or Ended. End Meeting is independently available and never requires this action. After Meeting Ended no new ballot is accepted, even if Vote remains Open. (BR-026, BR-090, BR-099, BR-092, BR-093) |
| Edit [Sửa] | Button on a Draft Vote, Chair only; opens the dialog pre-filled. An open or closed Vote can no longer be edited. |
| Delete [Xoá] | Button on a Draft Vote, Chair only; asks for confirmation and deletes the Draft Vote. An open or closed Vote cannot be deleted (UC-63, BR-086, BR-087). |
| Open vote [Mở biểu quyết] | Primary button on a Draft Vote, Chair only, enabled while the Meeting is InProgress (BR-092, BR-093). Eligible voters see the Vote immediately in Cast Vote. |

#### 11.2 Create & Open Vote Dialog

Figure 69: Create & Open Vote Dialog

The Create & Open Vote Dialog opens over the Voting tab when the Chair clicks “Create vote” (or “Edit” on a Draft Vote). The title shows the Agenda Item the Vote is linked to, normally the current item (“Tạo biểu quyết · Mục 2”). The Vote belongs directly to the Meeting, can only be created while the Meeting is InProgress and has no deadline or quorum (UC-63, UC-64).

This screen allows the Chair to:

- Create / Configure Vote: enter the motion, define and order the voting options, choose public or secret ballot and the meeting roles allowed to vote (UC-63).

- Save as Draft: click “Save draft” to keep the Vote in Draft status on the Voting tab for opening later (UC-63).

- Open Vote: click “Open vote” to save the Vote and open it at once for eligible voters (UC-64).

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
| Eligible voters [Người được bỏ phiếu] | Multi-select of meeting roles: Chair [Chủ trì], Secretary [Thư ký], Member [Thành viên], Guest [Khách mời]; all selected by default and at least one required. Eligible voters are the current holders of the selected roles when the Vote opens; the list and its count are then fixed. A Guest votes with the invitation identity, not the display name or room connection. (BR-090, BR-055, BR-091) |
| Cancel [Huỷ] | Link button; closes the dialog, nothing is saved. |
| Save draft [Lưu nháp] | Button; saves the Vote with status Draft. Allowed only while the Meeting is InProgress and the user is its current Chair (BR-092, BR-093). |
| Open vote [Mở biểu quyết] | Primary button; saves and opens the Vote (status Open, opening time recorded). If the Meeting is no longer InProgress the Vote is not created and the reason is shown (BR-090). |

#### 11.3 Cast Vote

Figure 70: Cast Vote

The Cast Vote screen (shown here in its compact/mobile layout) is opened by an eligible voter from the open Vote card, the in-room meeting panel. It shows one open Vote of the InProgress Meeting and lets the voter submit exactly one ballot (UC-65).

This screen allows the Chair, Secretary, Participant and verified Guest (eligible voters) to:

- View Open Vote: view the motion, the Meeting and Agenda Item it belongs to, the ballot type and the opening time (UC-62b).

- Cast Vote: select one option and click “Submit ballot”; each eligible voter can vote only once and the ballot cannot be edited (UC-65).

Field Description

| Field Name | Description |
| --- | --- |
| Screen title [Biểu quyết] | Read-only header. |
| Status chip | Open [Đang mở] with opening time (“Mở lúc 08:51”). When the Vote has been closed the options are disabled and the screen states that voting has ended. |
| Vote content | Read-only bold text of the motion. |
| Vote context | Read-only: Meeting title · Agenda Item · ballot type, e.g. “Họp kế hoạch Sprint 6 · Mục 2 · công khai”. |
| Options | Radio group, required, one choice only; values are the options defined by the Chair (e.g. Agree [Đồng ý], Disagree [Không đồng ý], Abstain [Không ý kiến]). The selected option is highlighted. |
| Submit ballot [Gửi phiếu] | Primary button, enabled when an option is selected. Accepted only while the Meeting is InProgress, the Vote is Open and the user is on the eligible list fixed when the Vote opened – a current Chair, Secretary, Participant or verified Guest in a selected role – and has not voted yet (BR-090, BR-092, BR-093, BR-091). After submission the screen shows the recorded choice and the turnout. |
| Immutability note [Phiếu đã gửi không sửa được.] | Read-only hint: a submitted ballot cannot be changed or withdrawn. |

#### 11.4 Vote Result & Conclusion

Figure 71: Vote Result & Conclusion

The Vote Result & Conclusion screen is shown on the Voting tab after the Chair closes a Vote. It presents the tally per option and, for a public Vote, each person’s choice. The Chair then records the conclusion Passed, NotPassed or Deferred; the tally and conclusion are carried into the Meeting Minutes (UC-62b, UC-67).

This screen allows the Chair to:

- View Vote Result: view the number of voters, ballot type, opening/closing time and the count and percentage of each option (UC-62b).

- View Public Ballots: for a public Vote, see how each eligible person voted and who did not vote (UC-62b).

- Record Vote Conclusion: select Passed, NotPassed or Deferred, optionally add a note and click “Record conclusion” (UC-67).

Field Description

| Field Name | Description |
| --- | --- |
| Vote content | Read-only bold text of the motion. |
| Status chip | Closed [Đã đóng]; after the conclusion is saved the chip shows the conclusion. |
| Result summary | Read-only, e.g. “4/5 người đã bỏ phiếu · công khai · mở 08:51, đóng 08:55”: voters / eligible, ballot type, opening and closing time. |
| Option result rows | One row per option: option label, bar and “count · percentage”, e.g. Agree 3 · 75%, Disagree 0 · 0%, Abstain 1 · 25%; percentages are calculated on ballots cast. Calculated by the system when the Vote is closed (BR-099). |
| Conclusion [Kết luận] * | Segmented control, required: Passed [Thông qua], NotPassed [Không thông qua], Deferred [Hoãn]. Enabled only for the current Chair and only after the Vote is closed (BR-099, BR-092, BR-093). |
| Conclusion note [Ghi chú kết luận] | Text input, optional, e.g. “Áp dụng từ Sprint 6.”; included with the conclusion in the minutes. |
| Record conclusion [Ghi kết luận] | Primary button, Chair only; saves the conclusion on the Vote. Other users see the conclusion read-only. |
| Public ballots [Phiếu công khai] | For a public Vote, list every person on the eligible list, internal or Guest, with the saved choice, or “Không bỏ phiếu” [Did not vote] for someone who cast no ballot (e.g. Phạm Đức Long · Thành viên – Không bỏ phiếu). Guest uses the self-declared display profile without exposing a legal identity claim. Attendance is reference only. A secret Vote hides all voter-choice links. (BR-090, BR-092, BR-093, BR-055) |
| Minutes note [Kết luận đưa vào biên bản (FE-12).] | Read-only hint: the tally and the recorded conclusion are included in the Meeting Minutes; no separate resolution is created (BR-099). |

### 12. FE-12 – Meeting Minutes & Approval

#### 12.1 Minutes Editor

Figure 72: Minutes Editor

The Minutes Editor is the Biên bản tab of an InProgress or Ended Meeting as seen by its current Chair or Secretary. The draft is filled from one fixed template — Biên bản cuộc họp theo Mẫu 1.4 Nghị định 30/2020/NĐ-CP — with the Meeting's information, attendance, each agenda item's recorded conclusion, the Vote results and conclusions, and the three sections of the approved AI draft when there is one. The Secretary edits the HTML text in the word-processor editor with a live preview of the paper, saves explicitly, and downloads the same text as a Word file; the minutes of a Secret Meeting cannot be downloaded. The Secretary sends the finished minutes to the Chair for approval (UC-72); the Chair approves and issues them (UC-73), or returns them with a reason (UC-74).

This screen allows the current Chair and the current Secretary to:

- View Meeting Minutes: read the latest revision as the paper that will be printed, with its revision number, status and last save time (UC-69).

- Generate Meeting Minutes: click “Tạo biên bản” [Create minutes], or “Điền từ dữ liệu cuộc họp” [Fill from meeting data] over an existing draft, to fill the fixed template from the Meeting's records and the approved AI draft (UC-70).

- Edit Meeting Minutes: correct the text in the word-processor editor; the preview on the right follows the text (UC-71).

- Save: click “Lưu” [Save] to save the text; the draft is not saved automatically.

Download Word: click “Tải Word” [Download Word] to save “Biên bản - <tên cuộc họp>.docx”; not available for a Secret Meeting (UC-71).

Field Description

| Field Name | Description |
| --- | --- |
| Meeting header | Read-only: Meeting title, status chip, confidentiality chip, actual time and place. |
| Mẫu biên bản [Template] | Read-only: one fixed template, Biên bản cuộc họp theo Mẫu 1.4 Nghị định 30/2020/NĐ-CP. There is no template chooser. |
| Trạng thái và lần sửa [Status and revision] | Read-only: revision number, status Bản nháp [Draft], who created the minutes and when, and when they were last saved. A correction that is not issued yet is labelled “Đính chính · bản nháp / chờ duyệt / bị trả lại” [Correction · draft / waiting for approval / returned]. |
| Tạo biên bản / Tạo lại [Create minutes / Rebuild] | Button; fills the template from the Meeting's records and the approved AI draft. Building again replaces all corrections after confirmation, and is not offered once the minutes are submitted or issued. |
| Soạn thảo HTML [HTML editor] | Word-processor area: toolbar with paragraph style, font size, colour, alignment, lists, tables, page break, find and replace; switches to a plain-text view and back, and the preview follows the text. The server filters the HTML before saving. Template marks: “## ” section heading, “### ” sub-heading, “@@ left \| right” two-column line, “**bold**”, “- ” bullet, “^ ” centred and “~ ” small line. |
| Xem trước [Preview] | Read-only paper that follows the text as it is typed and is laid out as it will print: heading, two-column opening, sections I–III, closing line and the Secretary and Chair name block with space to sign the printed copy by hand. |
| Lưu [Save] | Saves the text with the time the draft was read. If someone else saved first, the page says so and the newer text is not overwritten. |
| Tải Word [Download Word] | Downloads “Biên bản - <tên cuộc họp>.docx” built from the same text. Not available for a Secret Meeting, where the minutes are read on the screen only. |
| Quyền xem [Visibility rule] | Read-only note: only the current Chair and Secretary see and correct a draft; everyone else sees the minutes only after they are issued; someone who is neither receives no draft at all. |
| Ghi chú nhiệm vụ [Task note] | Read-only: “2. Nhiệm vụ, sản phẩm và thời hạn” is text in the minutes; no assignee, due date or progress is tracked. |

#### 12.2 Finalized Meeting Minutes

Figure 73: Finalized Meeting Minutes

The Finalized Meeting Minutes screen is the Minutes tab after the Chair has approved and issued the minutes. The issued minutes are the official record of the Meeting and can no longer be edited; the Meeting itself remains Ended. Every user allowed to view the Meeting’s minutes sees this read-only version (UC-69, UC-73).

This screen allows the System Admin, Member, Approver, Chair, Secretary and Participant (within their meeting scope) to:

- View Meeting Minutes: read the issued minutes (UC-69).

On the screen, s/he can also:

- Download PDF: click “Download PDF” to save the issued minutes as a PDF file.

Field Description

| Field Name | Description |
| --- | --- |
| Status chip | Issued [Đã ban hành]. |
| Seal summary | Read-only, e.g. “Lần sửa 2 · ban hành bởi Chủ tọa lúc 15/10/2026 14:40”: issued revision, Chair who issued and issue time (UC-73). |
| Download PDF [Tải PDF] | Button; downloads the issued revision as PDF. |
| Information banner | Read-only: “Biên bản đã ban hành là hồ sơ chính thức của cuộc họp: không sửa được, mọi sửa đổi thực hiện bằng lần sửa mới (UC-75).” – the issued minutes cannot be edited; a correction is a new revision (BR-137, BR-138). |
| Minutes document | Read-only page: title “BIÊN BẢN CUỘC HỌP”, Meeting name, date and actual time, full content (attendance, content from the published summary, vote conclusions). |
| Empty state [Trạng thái trống] | When no revision is issued yet, a reader sees that no minutes have been issued yet and no Draft card. |

### 13. FE-13 – Email & In-App Notification

#### 13.1 Notification Panel

Figure 74: Notification Panel

The Notification Panel opens from the bell at the bottom of the sidebar and is anchored next to it, over the current page. Every signed-in user uses it to read in-app notifications, mark them as read and open the related meeting (UC-76, UC-77a, UC-77b, UC-78).

This screen allows the Member, Approver, System Admin, Chair, Secretary and Participant to:

- View Notifications: view personal notifications and the unread count; opening the panel does not mark anything as read (UC-76).

- Mark Notification as Read: mark a single unread notification as read (UC-77a).

- Mark All Notifications as Read: mark all of the user’s unread notifications as read, including those not displayed (UC-77b).

- Open Notification Target: click a notification to open the linked meeting; the client marks the notification as read in a separate request (UC-78).

Field Description

| Field Name | Description |
| --- | --- |
| Notification bell | Icon in the sidebar footer next to the user’s name, with a badge showing the unread count; updated in real time without reloading the page. |
| Panel header [Thông báo] | Title with “n new” chip [2 mới] = unread count. Opening the panel does not change any read status. |
| Mark all as read [Đánh dấu tất cả đã đọc] | Link button; applies to all unread notifications of the user, including those not displayed (BR-033, BR-034); with no unread items the count simply stays 0. |
| Notification icon | Icon at the left of each item indicating the event type. |
| Notification title | Event name, e.g. Meeting published [Cuộc họp đã được công bố], Meeting approved [Cuộc họp đã được duyệt], Meeting rejected [Cuộc họp bị từ chối]; shown bold while unread. |
| Notification message | Detail text including the meeting name, e.g. “Bạn được mời tham dự cuộc họp ‘Họp Hội đồng quản trị quý IV’.” |
| Time | Relative time, e.g. “51 minutes ago” [51 phút trước], “3 hours ago” [3 giờ trước], “Yesterday” [Hôm qua]. |
| Unread indicator | Blue dot and tinted background on unread items; removed when the item is read. Marking an already-read item leaves the count unchanged. |
| Notification item (click) | Marks the item as read if unread and opens the linked meeting after re-checking the user’s access; protected content is not shown if access is denied (BR-016, BR-017, BR-033, BR-034). A failure to save the read status does not block opening an authorized meeting. |
| Messages | MSG40 “Unable to load notifications. Please try again.”; MSG39 “The requested notification page is unavailable.” |

#### 13.2 Meeting Invitation Email

Figure 75: Meeting Invitation Email

This email is sent asynchronously to internal participants and external Guests when a Meeting is published, or when a person is added to a Published or InProgress Meeting. Its buttons lead to the Invitation Response screen (6.4); a Guest first verifies the invited email with a one-time code on Guest Verification (1.3) (UC-31, UC-04).

This email allows the invited Chair, Secretary, Participant or Guest to:

- Read Invitation: view the meeting name, the recipient’s meeting role and the planned time, location and Chair.

- Respond to Meeting Invitation: choose Attend or Decline, which opens the Invitation Response screen (6.4) with that choice pre-selected (UC-31). The answer is recorded once; the Chair is not asked to respond.

- View Details: open the meeting details after signing in or after Guest verification.

- Access Meeting via Guest Invitation: a Guest opens the meeting-specific link and verifies the email with a code before any access (UC-04).

Field Description

| Field Name | Description |
| --- | --- |
| Subject / heading [Thư mời họp] | “Meeting invitation: <meeting name>”, e.g. “Thư mời họp: Họp Hội đồng quản trị quý IV”, under the TrustCabinet logo. |
| Salutation | Recipient name and meeting role, e.g. “Kính gửi ông Ngô Đình Phúc, … với vai trò Khách mời” (Guest). |
| Meeting information | Box with the Meeting’s planned date and time range, location and Chair, e.g. “14/10/2026 · 13:00 – 15:00”, “Phòng họp A3, tầng 5”, “Chủ trì: Nguyễn Minh Châu”. |
| Attend [Tham dự] | Primary button link; opens the Invitation Response screen with Attend pre-selected. |
| Decline [Không tham dự] | Button link; opens the Invitation Response screen with Decline pre-selected. Internal participants other than the Chair and Guests answer once and cannot change the answer afterwards (BR-046). |
| View details [Xem chi tiết] | Link; opens the meeting details (after sign-in or Guest verification). Access is limited to the invited Meeting (BR-052). |
| Security note | “Link chỉ dùng cho địa chỉ email này. Khi mở, ông sẽ được yêu cầu nhập mã xác minh.” Guest links are meeting-specific, require email verification, expire after the configured period and stop working when the invitation is revoked (BR-052, BR-053, BR-054, BR-044). |
| Delivery | Sent through a background queue; a delivery failure is retried and does not undo publishing or the roster change (BR-031, BR-032). |

### 15. FE-15 – Reporting & Statistics

#### 15.1 Reports & Statistics

Figure 76: Reports & Statistics

The Reports & Statistics screen is reached from Reports [Báo cáo] in the sidebar. It shows key figures on Meetings for a selected period, a monthly chart and a breakdown by Meeting status, and exports the same report as an Excel file. Every signed-in internal user sees only the Meetings they may read, the System Admin as for anyone else (BR-149, BR-150, PO 09/11/2026 Q1). (UC-79a, UC-79b).

This screen allows the System Admin, Member, Approver, Chair, Secretary and Participant (within their own scope) to:

- View Reports & Statistics: view the key figures, the meetings-per-month chart and the status breakdown for the selected period (UC-79a).

- Change Report Period: pick a start and end month; all figures are recalculated (UC-79a).

- Export Report: click “Export Excel” to download the report for the current period as an Excel file (UC-79b).

Field Description

| Field Name | Description |
| --- | --- |
| Page header | Title “Reports & Statistics” [Báo cáo & thống kê] with the period covered, e.g. “Số liệu 05/2026 – 10/2026”. |
| Period [Từ tháng – Đến tháng] | Month pickers (Từ tháng, Đến tháng), required; the start month must not be after the end month, at most 24 months. A Meeting belongs to the month of its planned start. (BR-149, BR-150) |
| Export Excel [Xuất Excel] | Button; downloads the report for the current period and scope as an Excel file (UC-79b). |
| Meetings [Cuộc họp] | KPI tile: number of Meetings in the period, e.g. 75. |
| Attendance rate [Tỉ lệ tham dự] | KPI tile: share of invitees recorded Present in attendance of Ended Meetings, e.g. 87%; based on attendance, not on invitation responses (BR-043). Unmarked invitees are not Absent; display an unmarked count so incomplete attendance is visible. |
| Average duration [Thời lượng trung bình] | KPI tile: average meeting duration in minutes with one decimal, from the actual start to the actual end; meetings missing either time are left out, e.g. 95.5 (BR-151). |
| Average turnaround [Thời gian ban hành trung bình] | KPI tile: average time in hours from the actual end of a Meeting to the first issue of its minutes, e.g. 30.5 (BR-151). |
| Meetings per month [Số cuộc họp theo tháng] | Bar chart, one bar per month of the period (T5…T10) with the value above each bar; the current, incomplete month is drawn lighter with the note “Tháng 10 tính tới ngày 14.” |
| By status [Theo trạng thái] | Table with columns Status [Trạng thái] (chip, e.g. Ended, Published, Pending Approval, Draft), Count [Số] and % of all Meetings in the period. |
| Scope and errors | Figures and exports include only Meetings the user may access, for the System Admin as for anyone else (BR-149, BR-150). If statistics cannot be loaded or the export fails, an error message with a retry option is shown and no file is produced. |

### 16. Background Job Inventory

#### 16.1 Process Notifications (JOB-01)

Runs inside the backend every 5 seconds. Each cycle creates the “meeting starting soon” reminder, once per recipient, for Meetings that start within the next 15 minutes, and queues the matching reminder email, then pushes up to 100 pending in-app notifications to their recipients over the notification hub and marks them as pushed; deactivated users receive no push. A missed push is not lost, because the notification list is read from the database (UC-76). A failed cycle is logged and retried on the next tick.

#### 16.2 Deliver Queued Emails (JOB-02)

Runs inside the backend only when email sending is enabled, through Resend or an SMTP server. It queues the invitation emails of Published Meetings that have not been queued yet, then sends queued emails (invitations, updates, cancellations, reminders, the issued-minutes notice) one at a time, and waits 5 seconds before polling again when the queue is empty. A failed email is retried with exponential back-off up to the configured number of attempts (8 by default) and then marked permanently failed (API-03, 17.3).

#### 16.3 Transcribe Recordings (JOB-03)

Runs inside the backend every 3 seconds and takes one waiting transcript at a time, so a long recording never slows the screens down. It sends the audio to the in-house speech recognition, cleans the result, suggests or fills in speaker names from Voice ID, keeps the Secretary's manual corrections and marks the transcript Completed or Failed with a reason the Secretary sees (UC-54a). A run whose recording changed meanwhile is discarded, and a transcript left half done by a restart is queued again. Details: Report 4, IV.1–IV.3.

#### 16.4 Draft Meeting Summaries (JOB-04)

Runs inside the backend every 3 seconds and drafts one requested meeting summary at a time (UC-58–UC-61) with the team's own language model, or, for a Normal Meeting, with the outside AI API the System Admin chose (API-06, BR-129). A failure is shown on the summary with the option to try again. Details: Report 4, IV.10.

#### 16.5 Build Combined Recordings (JOB-05)

Runs inside the backend every 30 seconds. For a hybrid or online Meeting that has ended and whose recordings have stopped changing, it builds one recording that holds the meeting room and every online participant on the real clock, with long silences shortened, and rebuilds it when a source changes within 8 days (UC-51a). It keeps the purpose and retention date of its sources and is not transcribed again. Details: Report 4, IV.5.

#### 16.6 Erase Expired Recordings (JOB-06)

Runs inside the backend every 15 minutes. It erases recordings and recorded speech whose retention period of 180 days has ended, together with their transcripts, keeping only the record that a recording existed (BR-110), and saves a room-microphone take that has received no sound for 12 hours. Details: Report 4, IV.4 and IV.6.

#### 16.7 Cancel or End Overdue Meetings (JOB-07)

Runs inside the backend every 15 minutes. It cancels a Meeting nobody started 24 hours after its planned start (BR-029) and ends a Meeting still in progress 24 hours after its planned end (BR-027), recording the system as the actor in the status history. Details: Report 4, IV.6.

#### 16.8 Remove Expired Personal Data (JOB-08)

Runs inside the backend every 60 minutes. It removes data that has outlived its purpose: audio no recording uses any more, Draft votes of Cancelled Meetings, read notifications after 90 days and all notifications after 180 days, guest verification codes after one day and the bodies of sent emails after 30 days. A run that removed something writes one audit entry per rule (BR-010). Details: Report 4, IV.6.

#### 16.9 Purge Unused Document Files (JOB-09)

Runs inside the backend every 60 minutes. It deletes stored document files that no personal library entry and no meeting document uses any more, after a grace period of 24 hours (BR-077). Details: Report 4, IV.6.

#### 16.10 Reconcile Online Meeting Rooms (JOB-10)

Runs inside the backend every 30 seconds. It compares the online meeting rooms with the Meetings, closes the room of a Meeting that is no longer in progress and removes people who may no longer be in it, such as a deactivated user or a participant who was removed (UC-43). Details: Report 4, IV.10.

### 17. External API Inventory

#### 17.1 Google Identity Services – Sign in with Google (API-01)

| URL | Browser loads https://accounts.google.com/gsi/client (Google Identity Services JavaScript SDK) |
| --- | --- |
| Purpose | Lets a user prove their Google identity in a Google popup so the SPA receives a signed Google ID token. |
| Caller | TrustCabinet frontend → Google Identity Services; the backend is not involved at this step. |
| Auth | Public OAuth 2.0 Client ID only; no client secret is used by this flow. |
| Request / Response | In: client_id. Out: callback with response.credential = Google ID token (JWT); a cancelled popup or Google error yields no credential and the login page shows MSG03 itself. |
| Failure handling | If the GIS script fails to load or no credential is returned, the SPA shows 'Đăng nhập bằng Google không thành công' (MSG03) without calling the backend. |
| Configuration | The public Google Client ID, which must be the same in the frontend and the backend. |

#### 17.2 POST: /api/auth/google – Google ID token verification (API-02)

| URL | POST: /api/auth/google (inbound from SPA); outbound: Google's published OAuth 2.0 signing certificates |
| --- | --- |
| Purpose | Signs a user in with a verified Google account and issues TrustCabinet's own access and refresh tokens. |
| Caller | SPA → TrustCabinet backend; backend → Google Identity Platform. |
| Auth | Anonymous endpoint; the Google ID token in the body is the credential. Token must be signed by Google, audience = configured Client ID, not expired, email_verified = true, non-empty sub. |
| Request / Response | Request: { idToken }. 200: access token + user in body, refresh token in HttpOnly/Secure/SameSite=Strict cookie tc_refresh_token (Path=/api/auth). 401 + MSG03 (invalid token), MSG05 (Google account not linked/authorised), MSG02 (account deactivated). 404 when Google sign-in is disabled. |
| Failure handling | Invalid/malformed tokens collapse to MSG03 and are logged without the token. First sign-in auto-links the account by verified email and writes an audit entry. Network failure fetching Google certificates is not caught (surfaces as HTTP 500). |
| Configuration | Google sign-in can be switched on or off, and the Google Client ID is set by configuration; the system checks it at startup when sign-in is enabled. |

#### 17.3 Email Delivery – Resend or SMTP (API-03)

| URL | Resend's email API over HTTPS; or the configured SMTP host and port (default port 587) when SMTP is used instead |
| --- | --- |
| Purpose | Delivers the system's emails: meeting invitations, updates and cancellations with an .ics calendar file, meeting reminders, the issued-minutes notice, guest presenter notices, guest verification codes and password-reset links. |
| Caller | TrustCabinet backend → Resend Email, or the SMTP server when SMTP is chosen. Queued emails go through the email queue worker (JOB-02); password-reset links and guest verification codes are sent at once. |
| Auth | Resend API key sent as a bearer token over HTTPS; when SMTP is used instead, username/password over STARTTLS (default), SSL-on-connect or none (internal relay only). Credentials are kept in user-secrets/environment variables. |
| Request / Response | MIME message: From (FromName/FromAddress), To, Subject, plain-text body; invitations add text/calendar METHOD:REQUEST attachment 'thu-moi-hop.ics' and cancellations add text/calendar METHOD:CANCEL attachment 'huy-hop.ics'. Reset links are {ApplicationBaseUrl}/reset-password#token=… (token in URL fragment). |
| Failure handling | Queued emails are retried: up to 8 attempts with exponential back-off, then marked permanently failed. Password-reset emails and guest codes are sent immediately; a failed reset email is reported to the administrator (MSG15). Delivery runs only when email sending is enabled; Resend reports later delivery events through API-09. |
| Configuration | Resend API address and key (or SMTP host, port, security mode and credentials), sender address and name, retry limits and timeouts, and the application base URL used in links; set by configuration, with credentials kept out of the code base. |

#### 17.4 LiveKit server – RoomService API and access tokens (API-04)

| URL | Backend → the LiveKit server's management API (list rooms, list participants, mute a track, remove a participant, close a room); browser → the LiveKit server's real-time connection. Exposed through the meeting room endpoints (view room, join, mute a participant). |
| --- | --- |
| Purpose | Runs the online meeting room on a self-hosted LiveKit SFU while the backend decides who may join and enforces chair controls. |
| Caller | TrustCabinet backend → LiveKit server; browser → LiveKit server with a backend-issued token. |
| Auth | LiveKit API key and secret; the backend issues short-lived access tokens (about 10 minutes) that identify the user and the Meeting room; the secret is kept outside the code base. |
| Failure handling | The backend still starts without LiveKit configuration and the room endpoints answer 503; LiveKit errors or timeouts answer 502; the room reconciliation (JOB-10) brings the rooms in line with the Meetings every 30 seconds. |
| Configuration | LiveKit addresses, API key and secret, token lifetime and whether the roster is enforced; set by configuration. |
| Status | Built. |

#### 17.5 POST: /api/livekit/webhook – LiveKit webhook (API-05)

| URL | POST: /api/livekit/webhook |
| --- | --- |
| Purpose | Receives room events from LiveKit so the system can record who joined/left the online meeting and close finished rooms. |
| Caller | LiveKit server → TrustCabinet backend. |
| Auth | Anonymous route verified by signature: the request carries a token signed with the LiveKit API secret that covers the request body. |
| Request / Response | Events: participant_joined, participant_left, participant_connection_aborted, room_finished, track_published (SCREEN_SHARE). 200 OK; 401 bad signature; 503 not configured; 500 on database error so LiveKit retries. |
| Failure handling | Handlers are idempotent; timestamps taken from the payload; lost webhooks are compensated by a periodic reconciliation. |
| Status | Built. |

#### 17.6 AI drafting service – draft of the Meeting content (API-06)

| URL | The team's own AI server, reached over the team's private network, or an outside AI API over HTTPS with the address, model and key the System Admin sets on the AI configuration screen. |
| --- | --- |
| Purpose | Writes the three sections of the AI draft of the Meeting content from the transcript, the agenda and the vote results (UC-58). Transcription and speaker identification are not part of this interface: they run inside the system. |
| Caller | TrustCabinet backend background processing → AI drafting service. |
| Auth | The outside API key is entered by the System Admin and stored encrypted; it is never returned to the browser or written to a log. The team's own server is reachable only inside the team's private network. |
| Request / Response | Input: the transcript text, the agenda and the closed vote results of one Meeting, never audio or voice data. Output: the draft of the three sections; the model used is stored with the draft. |
| Failure handling | A draft waits in a queue and is written one Meeting at a time (Queued, Running, Ready, Failed); a failed draft is shown and can be asked for again. When no AI server is ready the request is refused (MSG394). The draft never becomes the minutes by itself: the Chair or the Secretary reviews it first. |
| Status | Only a Normal Meeting may be sent to the outside API; Internal, Secret and Top Secret Meetings always use the team's own server (BR-129, ADR-0056). |

#### 17.7 File Storage – SeaweedFS (API-07)

| URL | SeaweedFS over an S3-compatible API, through the configured service address and bucket, reachable only on the app server's Docker network (a local SeaweedFS container in development; ADR-0084). |
| --- | --- |
| Purpose | Keeps the bytes of every uploaded document in the team's self-hosted S3 store (SeaweedFS) on the app server's own disk, separate from the application process (ADR-0084). Files are served only through the application, which checks the permission of the caller on every request. |
| Caller | TrustCabinet backend (document upload, view and download) → File Storage (SeaweedFS). |
| Auth | Access key and secret key kept in user-secrets/environment variables, never in the code base; the bucket is private and has no public address. |
| Request / Response | Input: the file bytes with their stored name. Output: the stored bytes when the file is read; the system never gives the browser a direct address of the bucket. |
| Failure handling | If the store cannot be reached, the upload or download is refused with a message and nothing is recorded as stored; files that were written but never linked to a document are removed by a periodic clean-up. |
| Status | Built. |

#### 17.8 Speech Worker – speech recognition and Voice ID (API-08)

| URL | The team's own speech worker over HTTP on the private network: POST /v1/transcribe (a recording or one sentence, with speaker separation on or off), GET /v1/progress (progress of a running transcription), POST /v1/embed (voice vector of one Voice ID sample). A second worker can be configured as backup. |
| --- | --- |
| Purpose | Speech recognition, speaker separation and voice vectors for Voice ID, run inside the system so that audio and voice data never leave it (BR-121). |
| Caller | TrustCabinet backend: the transcription queue (JOB-03), the live sentence recording of each participant's microphone, and Voice ID sample enrolment (UC-56c). |
| Auth | A shared key sent as a bearer token when the worker is configured with one; the key is kept outside the code base. The worker is reachable only on the private network. |
| Request / Response | In: the audio file and the run parameters. Out: timed lines with a voice label, one voice vector per voice and a confidence score per word; for /v1/embed, one voice vector after the sample's quality checks. |
| Failure handling | When the main worker cannot be reached, the request goes to the backup worker, which is then asked first for a short while (ADR-0081). A worker error marks the transcript Failed with a reason the Secretary sees (UC-54a); a sample that fails the checks is refused with a message. |
| Configuration | Worker address, backup address, shared key and timeouts; set by configuration. |

#### 17.9 POST: /api/email/resend/webhook – Email delivery webhook (API-09)

| URL | POST: /api/email/resend/webhook |
| --- | --- |
| Purpose | Records what Resend reports after it accepted an email, so the email queue shows whether each email was delivered. |
| Caller | Resend → TrustCabinet backend. |
| Auth | Anonymous route verified by signature: Resend signs the raw body (Svix signature) with the configured webhook secret; an old timestamp is refused as a replay. Without a secret nothing is accepted. |
| Request / Response | Events: email.delivered, email.delivery_delayed, email.bounced, email.complained, email.failed, email.suppressed. Unknown events and emails the system does not track are acknowledged and ignored. |
| Failure handling | A request with a bad or missing signature is refused and changes nothing. |
| Configuration | Webhook secret, set with the Resend settings outside the code base. |

## IV. Non-Functional Requirements

### 1. External Interfaces

TrustCabinet is a self-hosted web application installed for a single organisation (TrustSoft) on its own infrastructure. Users interact with it only through a web browser; the server communicates with a small number of backing services and external systems. This section states what each interface must provide so that the system communicates correctly with its users and with the hardware and software around it. Detailed protocols and APIs are given in Section I.5.2 (External API Inventory) and in Report 4 (SDS).

- User interface: A single-page web application opened in a standard web browser; no client installation, plug-in or browser extension is required. All screens, labels, system messages (catalogued in Section V.2 with MSGnn codes), notifications and emails are in Vietnamese in v1. Dates are shown as dd/MM/yyyy and times in 24-hour format in Vietnam time (UTC+7). The interface is designed desktop-first (laptop and desktop screens of 1280 px width or more) and remains usable at 1024 px; the external-Guest invitation and email-verification screens, invitation response, voting, notifications and document viewing are also usable on mobile browsers (360 px width or more). Native mobile apps are out of scope (Report 1, LI-10).

- Hardware interface: The system requires no special-purpose hardware. Clients: any PC, laptop, tablet or phone able to run a supported browser; joining the online meeting room additionally requires a microphone and speakers/headset, and optionally a camera, granted to the browser by the user. The system never activates a user's microphone or camera on its own and never records on its own: audio is captured only in the browser, under the user's control (Report 1, LI-01). Server: a single x86-64 host (physical or virtual) running Linux inside TrustSoft's network; recommended baseline 8 vCPU, 16 GB RAM and 500 GB of storage for documents and meeting audio, excluding speech/AI processing. Speech recognition runs in a separate CPU-only speech worker, on the same host or on another machine the server reaches; no GPU is needed (Report 2, risk R03).

- Software interfaces: (1) PostgreSQL 17 - the system's single relational data store for all business data, audit log and outbox queues. (2) Redis - short-lived security data (refresh-session and password-reset credentials) with automatic expiry. (3) Resend Email (outbound email API; any standards-compliant SMTP server can be used instead) - outgoing invitation (with .ics calendar attachment), reminder, approval and password-reset emails. (4) Google Identity Services / Google OpenID Connect - optional 'Sign in with Google' for internal users whose Google email is linked to an existing account; email/password sign-in must remain available when Google is unreachable. (5) Self-hosted media server (LiveKit SFU) - audio, video and screen-sharing for the online meeting room. (6) AI drafting service - writes the draft of the Meeting content from the transcript, the agenda and the vote results. It is the team's own AI server, or for a Normal Meeting an outside AI API chosen by the System Admin; it receives text only, and the model used is recorded with every draft. Speech recognition and speaker identification run inside the system and send nothing outside. No other third-party system (HRM, ERP, calendar, chat) is integrated in v1 (Report 1, LI-07).

- Communication interfaces: All browser-server traffic uses HTTPS (TLS 1.2 or later) on a single origin; plain HTTP is redirected to HTTPS or refused. Data is exchanged as JSON (UTF-8) over a REST-style API. Real-time in-app notifications, agenda and vote updates use a WebSocket channel secured with TLS (WSS) and fall back to automatic reconnection when the connection drops. Online meeting media is transmitted with WebRTC and is always encrypted in transit (DTLS-SRTP); a relay (TURN) path over TLS is available for clients behind restrictive firewalls. Outgoing email is sent over SMTP with TLS (implicit TLS or STARTTLS). Calls from the server to Google and to any external speech/AI provider use HTTPS only. A health-check endpoint is exposed for monitoring the running instance.

### 2. Quality Attributes

The quality attributes below are stated as measurable targets for a single-organisation, self-hosted deployment of TrustCabinet serving up to about 200 internal users, and will be verified in Report 5.3/5.4 (System Test) and Report 5.5 (Acceptance Test).

#### 2.1 Usability

| Requirement | Description |
| --- | --- |
| Learnability and training time | An internal Member with basic computer skills must be able to find an upcoming meeting, respond to an invitation, open a meeting document, cast a vote after a guided introduction of no more than 30 minutes. A Member, Secretary, Chair or System Admin must be able to complete their full role workflow (e.g. create, submit and publish a meeting; manage sessions, agenda and participants; upload audio and approve the summary; issue minutes) after no more than 2 hours of training. An external Guest must be able to verify their email and respond to an invitation without any training. |
| Vietnamese user interface | All user-facing text - menus, labels, buttons, field hints, validation and system messages, notifications, emails and exported reports - is in Vietnamese with correct diacritics (UTF-8). Searches must handle Vietnamese text with diacritics correctly. Terminology must be consistent with the glossary of this SRS (e.g. the same Vietnamese term for Meeting, Agenda Item, Secretary throughout). |
| Consistency | All screens share the same layout (navigation menu, header with notification bell and user menu, content area), the same placement of primary and secondary actions, the same date/time format, and the same visual badge for each lifecycle status (meeting, session, agenda item, vote, minutes). Lists use the same search, filter, sort and pagination controls, and empty results show a no-results message rather than an error. |
| Responsive, desktop-first layout | All features are fully usable on desktop/laptop screens of 1280 px width or more and remain usable at 1024 px. The external-Guest invitation and email-verification screens, Respond to Meeting Invitation, voting, notification list and document viewing must be usable on mobile browsers of 360 px width or more without horizontal scrolling or loss of function. |
| Browser support | The application must work correctly on the two most recent stable versions of Google Chrome, Microsoft Edge, Mozilla Firefox and Safari (desktop), and on Chrome for Android and Safari on iOS for the mobile-capable screens. The online meeting room requires a WebRTC-capable browser; when the browser or device permissions are not suitable, the system tells the user what is missing instead of failing silently. |
| Accessibility basics | Screens should follow the basic WCAG 2.1 Level AA practices: every form field has a visible label, all functions are reachable with the keyboard with a visible focus indicator, text has a contrast ratio of at least 4.5:1, status and errors are not conveyed by colour alone, and pages remain usable when the browser zoom is set to 200%. |
| Clear error messages with codes | Every error or warning shown to a user uses the wording and code defined in the System Messages catalogue (Section V.2, MSGnn), explains in plain Vietnamese what happened and what the user can do next, is displayed next to the affected field when it concerns a field, and never shows stack traces or technical internals. Data already entered in a form is preserved after a validation error. |
| Confirmation of destructive and irreversible actions | Before any action that deletes, cancels, revokes or cannot be undone - e.g. deleting an agenda item or document, cancelling a session, removing a participant or revoking a Guest, deactivating an account, rejecting a meeting, closing a vote, issuing minutes - the system asks the user to confirm in a dialog that names the affected object and states the consequence. Actions that cannot be reversed must say so explicitly. |
| Feedback on long-running operations | Any operation that takes longer than 1 second shows a loading indicator, and the user cannot submit the same form twice. File uploads show progress, and audio processing (transcription and summary generation) shows its current status (e.g. queued, processing, completed, failed) so that the Secretary does not need to wait on the page. |

#### 2.2 Performance

| Requirement | Target | Condition |
| --- | --- | --- |
| Page load time | Initial load of the application ≤ 3 s; navigation between screens and display of a screen's data ≤ 2 s for 95% of requests | Internal network ≥ 10 Mbps, up to 100 users active simultaneously |
| Server response time | 95% of API requests (view, create, update, approve, respond, vote) complete within 1 s; no ordinary form submission takes longer than 3 s | Up to 100 users active simultaneously, excluding file transfer time and AI processing |
| Search and list views | Paginated meeting, document, user, notification and audit-log lists, including search, filter and sort, return the first page within 2 s for 95% of requests | Data volume up to 10,000 meetings, 50,000 documents and 1,000,000 audit-log entries |
| Document upload and preview | Documents up to 10 MB per file are accepted; after the transfer completes the file is stored and listed within 5 s; in-browser preview of a document up to 10 MB opens within 3 s | Up to 10 simultaneous uploads; transfer time depends on the user's network |
| Meeting audio upload and AI processing | Audio files up to 300 MB are accepted. The upload is acknowledged and the file queued within 5 s after transfer. The speaker-attributed transcript is ready within 1.5 x the audio duration (e.g. a 60-minute recording within 90 minutes) after processing starts, and a summary is generated within 5 minutes of the Secretary's request | One recording processed at a time per processing worker; speech recognition always runs on the in-house speech worker, and the summary target applies to whichever language model is selected (in-house, or the outside API for a Normal meeting); queued jobs wait their turn without blocking other functions |
| Notification and real-time update latency | In-app notifications and the unread count reach a connected recipient within 10 s of the triggering event; current-agenda and vote-status changes are visible to other users in the same session within 2 s; an email is handed to the email service within 2 minutes; 'starting soon' reminders are sent 15 minutes (± 1 minute) before a session starts | Normal operation, mail server reachable; email retries do not delay in-app delivery |
| Concurrent users | The system remains fully functional with 200 signed-in users, of whom up to 100 perform operations at the same time, without errors or time-outs and within the response-time targets above | Single-server Docker deployment with the recommended baseline hardware |
| Online meeting room capacity | Each meeting room supports up to 30 participants with audio and video plus one screen share, with a hard limit of 50 connected users; joining the room takes ≤ 5 s; mouth-to-ear audio delay ≤ 400 ms; at least 3 rooms can run at the same time. The 50-connection limit is enforced by the LiveKit room service, which refuses the next connection while the room is full. | Participants inside the company network or on a stable connection of ≥ 2 Mbps per participant; self-hosted media server on the deployment host |
| Report generation and export | A statistics report covering up to 12 months of data is displayed within 5 s and exported to a file within 10 s | Data volume as in 'Search and list views'; up to 5 users generating reports at the same time |
| Speaker recognition and transcript clean-up | By default a voice is suggested and its name filled in at a similarity of 0.6 or more, a second voice joins a person at 0.7 and an unnamed voice inherits a person's name at 0.8. A room-mic passage under 10 characters, or under 3 s and under 20 characters, with no matched voice or only filler words is left out; a per-microphone sentence of filler words only is not stored; words recognised below 40% confidence are flagged; numbers are written in digits under the Vietnamese ITN rules. | In-house speech worker with its default configuration; the thresholds are configurable |

#### 2.3 Security

| Requirement | Description |
| --- | --- |
| Authentication required | Every page and API function requires a signed-in internal user, except the sign-in page, the password-reset page reached from a valid reset link, the external-Guest invitation and email-verification pages, and the health check. Sign-in with Google is accepted only after Google's identity assertion has been verified and only for an existing, linked internal account; it never creates an account (BR-003). Sign-in failure messages must not reveal whether an account or email exists (BR-004). |
| Password policy and protection | Passwords must be at least 12 characters long; no mandatory character-class rules are imposed, and long passphrases must be accepted. Passwords are never stored or logged in a readable or reversible form and are never displayed, including to administrators. A password-reset link is valid for at most 60 minutes and can be used only once. |
| Sign-in rate limiting | Sign-in attempts are limited per minute (MSG414): at most 5 password sign-in attempts per email address and 60 auth attempts per IP address in each 60-second window; exceeding the limit returns 429 with a Retry-After hint, and the message shown does not reveal the failure count. No account is locked for failed sign-in and there is no unlock action. Repeated failed attempts against guest-verification codes and password-reset requests are likewise throttled. |
| Session and token expiry | Short-lived access credentials expire after at most 15 minutes and are renewed silently while the user remains signed in; after at most 7 days the user must sign in again. Signing out immediately ends the session. Deactivating an account terminates all its active sessions (BR-001), and changes to a user's roles or status take effect on the user's next request. Session credentials must not be readable by page scripts and must not be sent to other sites. Sign-out revokes only the refresh token, so an access token already issued stays usable until it expires. |
| Role-based access control and least privilege | Every request is authorised on the server against the user's current system-role permissions and their relationship with the meeting (Organizer, Approver, Chair, Secretary, Member or Guest) (BR-016, BR-017, BR-030); anything not explicitly permitted is denied. Hiding a button in the user interface is never the only protection. A user who is not authorised for a meeting is told it is not found, so its existence is not disclosed. Only the System Admin can change role-permission configuration, and every change is audited (BR-009, BR-010, BR-011, BR-007). |
| Document security levels | Every document carries one security level: Normal, Internal, Secret. Access to view, preview, download, comment on or attach a document is granted only when both the user's relationship with the document (owner or authorised meeting relationship) and the document's level permit it; documents a user may not access never appear in their search results, lists, notifications or emails. Viewing or downloading a Secret document is recorded in the audit log, and Secret content is never included in email bodies or attachments. |
| External Guest access scope | A Guest requires a secure current Meeting invitation and invited-email verification by a one-time code expiring within 15 minutes. After providing self-declared display information, the Guest uses Member participation rights only in this Meeting, including all attached documents, comments, voting and room controls. Access expires and revocation takes effect on each protected request. No internal account, Chair/Secretary role, delegated authority, room moderation or personal library is granted. For one invitation a new verification code can be requested at most once per minute, and at most 20 codes are sent in any 24 hours. (BR-049, BR-050, BR-051, BR-052, BR-053, BR-054, BR-044, BR-055) |
| Encryption of data in transit | All communication between browsers and the server, between the server and external services (Google, SMTP, any external speech/AI provider) and all meeting media must be encrypted in transit (TLS 1.2 or later; encrypted WebRTC media). The system must not be served over unencrypted HTTP in production. |
| Protection of stored data and backups | Uploaded documents and meeting audio are stored only on TrustSoft's own infrastructure and are reachable only through the application's access checks, never through a public or guessable URL. Secrets (signing keys, database, Redis and SMTP credentials) are kept out of the source code and configuration repository. The database and file storage are backed up at least daily; backups are stored separately from the production host with access restricted to authorised administrators. |
| Audit logging | The system records security and governance events - sign-in success/failure, sign-in rate limiting, password reset, Google account linking, user and role changes, meeting lifecycle transitions, access to Secret documents, audio upload and deletion, summary approval, vote closing, the issue of minutes - with actor, time, action, target object and source IP address (BR-009, BR-010, BR-011, BR-022, BR-023). Audit entries cannot be modified or deleted through the application; only the System Admin can view and search them (UC-12). Passwords, tokens and verification codes are never written to any log. |
| Personal data protection and audio consent (Decree 13/2023/ND-CP) | Personal data (account details, Guest name and email, voices in meeting audio, attendance) is processed only for the stated meeting-management purpose. A meeting audio file can be uploaded only after the Secretary confirms that participants were informed and consented and states the purpose; each recording has a retention date, after which (or on a justified request) it is erased and the erasure is recorded. The online meeting room stores only join/leave times, never media or chat content. If an external speech/AI provider is selected, audio and transcripts are sent only to that provider, only for processing, and must not be retained or used by it for other purposes. |
| Integrity of issued minutes, vote conclusions and stored documents | Once a Vote Conclusion is recorded or minutes are issued, their content cannot be changed by anyone, including administrators. Issued minutes, vote conclusions and lifecycle histories are retained as immutable records, and each stored document version can be verified against the fingerprint taken at upload. |
| Upload safety | Uploaded files are accepted only within the allowed size limits and file types; the file type is determined from the file content, not from its name. Files are never executed by the server, and audio uploads accept audio formats only (no video, Report 1, LI-03). All stored documents together take at most 250 GB, counting files not yet purged; a file that would exceed it is not stored. Minutes content is HTML filtered by the server before saving, so nothing in it can run in a browser; it holds at most 300,000 characters and 64 levels of nesting, and the Word and PDF files keep only the first 63 columns of a table. |

#### 2.4 Reliability

| Requirement | Description |
| --- | --- |
| No loss of committed data | A business operation (e.g. approving a meeting, recording a vote, issuing minutes) is either completed in full or not at all; a failure part-way never leaves partially saved data. After an unplanned restart no confirmed transaction is lost. |
| Reliable asynchronous delivery | Invitation and notification emails are queued and retried automatically (up to 8 attempts with increasing intervals) until delivered or marked failed; a failure to deliver never reverts the meeting state that triggered it (BR-031, BR-032). Delivery status is traceable. |
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
| Configurability without code change | Operational values - token lifetimes, sign-in rate limits, Guest access validity, upload size limits, email retry policy, reminder lead time, audio retention period - are configurable through settings or environment variables, with safe defaults validated at start-up. |
| Schema evolution and documentation | Database changes are applied through versioned migrations; APIs, message codes and architectural decisions are documented in the repository and kept consistent with Reports 3 and 4. |

#### 2.7 Portability / Deployability

| Requirement | Description |
| --- | --- |
| Self-hosted containerised deployment | The complete system (application, PostgreSQL, Redis and media server) can be deployed on a single Linux host inside TrustSoft's network using Docker Compose, without any paid or cloud-only service; outgoing email can use the organisation's own SMTP server instead of Resend, and the outside language-model API for Normal meetings is optional (Report 2, constraints 2 and 5). |
| Single deployable application | The web front end and the API are delivered as one application served from the same origin; a new environment can be installed from the documented procedure within 1 working day by a person with basic Linux and Docker knowledge. |
| Environment independence | All environment-specific values (URLs, credentials, SMTP and Google settings, speech/AI provider endpoint) are supplied by configuration, so the same build runs in development, test and production. |

#### 2.8 Compliance

| Requirement | Description |
| --- | --- |
| Personal data protection | Processing of personal data, including voices in uploaded meeting audio, complies with Decree 13/2023/ND-CP on personal data protection: stated purpose, recorded consent, defined retention and recorded erasure. |
| Issued minutes and records | Minutes are approved and issued by the Chair (UC-73) with a complete audit trail; they carry no CA-issued digital signature. Issued minutes, vote conclusions and lifecycle histories are retained as immutable records. |
| Licensing | All third-party libraries, tools and services used must be free, open-source or student-licensed, with licences compatible with internal deployment at TrustSoft (Report 2, constraint 2). |

## V. Requirement Appendix

### 1. Business Rules

| ID | Rule |
| --- | --- |
| BR-001 | A deactivated account cannot sign in or use any function; all its active sessions are ended and its historical data is kept. |
| BR-002 | An active account can sign in even when its role has no capability. |
| BR-003 | Google sign-in succeeds only when the email matches an existing internal account; no account is created automatically. |
| BR-004 | A sign-in error message does not reveal whether the email exists. |
| BR-005 | An email address belongs to at most one account. |
| BR-006 | A newly created account is Active. |
| BR-007 | Only capabilities defined by the system can be granted to a role. |
| BR-008 | A change to a role's capabilities or to a user's role takes effect from the user's next action, without ending the session. |
| BR-009 | Account and role administration changes and every sign-in are recorded in the audit log. |
| BR-010 | Issuing minutes, viewing a document of a Secret Meeting, exporting a report and sharing a calendar are recorded in the audit log. |
| BR-011 | Recording or playing a voice sample, adding or removing a meeting document, and reading a deactivated account's library are recorded in the audit log. |
| BR-012 | Only a role with the user-administration capability views the audit log; the audit log is read-only and nobody can edit or delete it. |
| BR-013 | A new Meeting starts in Draft; its planned start is later than now and its planned end is later than its planned start. |
| BR-014 | Meeting information can be edited only in Draft or Rejected; editing does not change the status. |
| BR-015 | For a Published Meeting, only the Organizer changes the schedule or the location; the new start must be later than now. |
| BR-016 | Only the Organizer, the Approver, the Chair, the Secretary or a participant of a Meeting can access that Meeting. |
| BR-017 | The participant list and the invitation responses are shown only to people who can view the Meeting. |
| BR-018 | Submitting for approval moves a Draft or Rejected Meeting to WaitingApproval; while it waits, its general information cannot be edited. |
| BR-019 | Each approval round has one valid Approver, and only that Approver approves or rejects; without an Approver the Meeting cannot be submitted. |
| BR-020 | Approval moves a WaitingApproval Meeting to Approved; rejection moves it to Rejected and requires a reason. |
| BR-021 | Only the Organizer publishes a Meeting, only from Approved, and only when it has exactly one Chair and at least one active Secretary. |
| BR-022 | Every status change is recorded in the history with the actor, time, action, new status and reason. |
| BR-023 | The status history is append-only; no entry is edited or deleted. |
| BR-024 | Only an active Chair or Secretary starts a Meeting, only from Published and only when both a Chair and a Secretary are assigned. |
| BR-025 | Only the Chair ends a Meeting, only while it is InProgress; ending the Meeting closes its online room. |
| BR-026 | Ending a Meeting does not close its Votes; ballots already cast are kept. |
| BR-027 | A Meeting still InProgress 24 hours after its planned end is ended automatically. |
| BR-028 | Only the Organizer cancels a Meeting, only before it starts; Cancelled is final and read-only. |
| BR-029 | A Meeting not started 24 hours after its planned start is cancelled automatically by the system. |
| BR-030 | Read access by status: a Member reads only Published, InProgress, Ended, or Cancelled after Published; the Chair, Secretary and Organizer read every status; the Approver reads from submission, never a Draft. |
| BR-031 | Once a Meeting is published, people are notified when invited, when their role changes or they are removed, and when the Meeting is rescheduled or cancelled. |
| BR-032 | A failure to send a notification does not undo the action that triggered it. |
| BR-033 | Each person reads and changes the status of their own notifications only. |
| BR-034 | Opening a link in a notification checks access again. |
| BR-035 | Nobody receives an in-app notification about their own action. |
| BR-036 | Only the current Secretary and the Chair manage the participant list; the Organizer has no separate right to it. |
| BR-037 | The participant list can be edited in Draft, Rejected, Approved, Published and InProgress; in WaitingApproval it is read-only. |
| BR-038 | An internal participant is an active account and appears at most once in a Meeting. |
| BR-039 | Each participant has exactly one meeting role, Chair, Secretary or Member, independent of their system role. |
| BR-040 | A Meeting has at most one Chair; a Published or InProgress Meeting always has a Chair and at least one Secretary. |
| BR-041 | Before publication, the Secretary assigns or changes the Chair. |
| BR-042 | From publication, only the current Chair hands the Chair role to an active internal participant who is not a Secretary, and the former Chair becomes a Member; if the Chair is deactivated, the Secretary may replace them. |
| BR-043 | A role change does not change the invitation response or the attendance; response and attendance are independent of each other. |
| BR-044 | Removing a participant or revoking a Guest invitation ends the access gained through that relationship; other independent access remains. |
| BR-045 | The responses and attendance of a removed person are kept as history and do not count towards the current list, access or role counts. |
| BR-046 | Only the invitee answers an invitation, only Accepted or Declined, once, while the Meeting is Published or InProgress, and cannot change it; the Chair does not answer. |
| BR-047 | An invitation not yet answered counts as attending. |
| BR-048 | Declining only hides the Meeting from that person's calendar; it does not change their role or access, including reading documents. |
| BR-049 | A Guest is a meeting-specific invitation, not an internal participant. |
| BR-050 | A Guest has no system role and cannot be the Chair or a Secretary. |
| BR-051 | Within one Meeting an email has at most one Guest invitation, and no Guest invitation is made for the email of an internal participant. |
| BR-052 | A Guest enters through their own invitation, only into that Meeting, without an internal account. |
| BR-053 | A Guest must prove control of the invited email address. |
| BR-054 | Guest access lasts 12 hours from verification without extension, and ends earlier on sign-out, invitation revocation or the end of the Meeting. |
| BR-055 | While the Meeting is Published or InProgress, a Guest has the rights of a participant, except moderating the room, a personal library and sharing documents. |
| BR-056 | Only an active Secretary records or corrects official attendance, while the Meeting is InProgress. |
| BR-057 | Attendance covers internal participants and Guests as Present or Absent; a person not yet recorded stays blank and is not counted as absent. |
| BR-058 | An agenda item belongs to one Meeting. |
| BR-059 | The Secretary or the Chair adds, edits, deletes and reorders agenda items from Draft to InProgress; from Ended the agenda is locked. |
| BR-060 | Only the Chair starts or completes an agenda item. |
| BR-061 | Any Pending item can be started, but at most one item is InProgress at a time. |
| BR-062 | An item must be started before it is completed; completing it neither starts another item nor ends the Meeting. |
| BR-063 | Only an item that has not started (Pending) can be deleted. |
| BR-064 | A Presenter is a current participant or a Guest of the Meeting. |
| BR-065 | Removing a person from the Meeting leaves their agenda items without a Presenter. |
| BR-066 | Each agenda item has at most one conclusion. |
| BR-067 | The Chair or the Secretary records the conclusion of an InProgress or Done item until the minutes draft is built. |
| BR-068 | An uploaded file never changes and has no versions; to change it, a new file is uploaded to replace the meeting document. |
| BR-069 | Only PDF and DOCX files are accepted. |
| BR-070 | Only the Chair or the Secretary adds or removes meeting documents and links them to agenda items. |
| BR-071 | Documents are added or removed from the time the Meeting can be viewed until it ends. |
| BR-072 | A meeting document comes from the adder's personal library or their computer; a file uploaded straight into a Meeting also goes into the personal library. |
| BR-073 | A file has at most one active attachment in a Meeting. |
| BR-074 | Removing a document from a Meeting does not delete it from the personal library or other Meetings, and unlinks it from every agenda item. |
| BR-075 | Comments on a removed document are kept as history; adding the document again starts afresh, without the old comments. |
| BR-076 | Deleting a document from the personal library does not remove it from a Meeting. |
| BR-077 | A file no longer used anywhere is purged periodically; its history and comments are kept. |
| BR-078 | A document links only to agenda items of the same Meeting. |
| BR-079 | Anyone who can view a Meeting reads its documents, except a document attached only to a draft Vote, which only the Chair and the Secretary see. |
| BR-080 | After the Meeting ends, its documents and comments are read-only. |
| BR-081 | A document of a Secret Meeting can be read only online and never downloaded. |
| BR-082 | Anyone who can view a document can comment on it until the Meeting ends. |
| BR-083 | A personal library and its folders belong to one person, and only that person accesses them. |
| BR-084 | A folder cannot be moved into itself or one of its subfolders; only an empty folder can be deleted. |
| BR-085 | The Admin only reads the library of a deactivated account, and never sees files of a Secret Meeting. |
| BR-086 | Only the Chair or the Secretary creates, configures, views and deletes draft Votes, while the Meeting is Approved, Published or InProgress; an opened or closed Vote cannot be deleted. |
| BR-087 | A Vote belongs to the Meeting, not to an agenda item; draft Votes never opened are deleted when the Meeting ends. |
| BR-088 | The content, options, type and voting roles of an opened or closed Vote cannot be changed. |
| BR-089 | A Vote has at least two distinct options and is either Public or Secret. |
| BR-090 | Only the Chair opens a draft Vote, only while the Meeting is InProgress; at most one Vote is Open at a time and it must have eligible voters. |
| BR-091 | The voter list is fixed at opening from the chosen roles and current access, not from attendance; later role changes do not alter it. |
| BR-092 | A ballot is cast only while the Meeting is InProgress, the Vote is Open and the voter still has access. |
| BR-093 | Each voter casts one ballot for one option of the Vote and cannot change or withdraw it; losing access only blocks new ballots. |
| BR-094 | Vote documents are attached only while the Vote is Draft and are fixed when it opens. |
| BR-095 | A voter must confirm reading every Vote document before casting a ballot. |
| BR-096 | A Secret Vote never links a voter to an option, read confirmations included; only totals are published. |
| BR-097 | After a Public Vote closes, each voter's choice is shown. |
| BR-098 | Only the Chair closes and tallies a Vote, while the Meeting is InProgress or Ended; percentages are based on the ballots cast. |
| BR-099 | The Chair records Passed, NotPassed or Deferred for a closed Vote and cannot change it afterwards; the system never derives it from the result. |
| BR-100 | Only assigned participants and valid Guests join the meeting room; being only the Organizer or the Approver is not enough. |
| BR-101 | Leaving the room or losing the connection does not change the Meeting, list, roles, attendance or Votes; a room connection is not attendance. |
| BR-102 | Only the Chair mutes another person's microphone; that person may unmute again. |
| BR-103 | Chat messages in the room are not stored. |
| BR-104 | Each person has at most one speaking request. |
| BR-105 | Only the Chair orders, invites, declines and closes speaking turns. |
| BR-106 | The speaking history stays with the Meeting and is not a task. |
| BR-107 | The recording method follows the meeting mode and is not chosen by the user; the room is recorded only while InProgress, and audio files only for in-person Meetings. |
| BR-108 | Only the Secretary records the room, uploads audio files, deletes takes or deletes recordings. |
| BR-109 | Before recording, participants must be informed; the Secretary confirms this and the system stores the time and purpose. |
| BR-110 | Recordings, transcripts and recorded sentences are deleted automatically 180 days after the first take; later takes do not extend this, and deleting a recording deletes its audio and transcript. |
| BR-111 | The Chair, the Secretary and the participants can listen to the recordings. |
| BR-112 | Only the Chair or the Secretary downloads a recording or exports a transcript, never for a Secret Meeting; a Guest only reviews their own microphone sentences. |
| BR-113 | An online Meeting is recorded only while a microphone is on; each person withdraws their own sentences, and the Secretary deletes any sentence. |
| BR-114 | The Chair or the Secretary excludes a sentence from the record with a reason, without deleting it; only they read excluded sentences. |
| BR-115 | Only the Secretary edits the text of the transcript. |
| BR-116 | Voice ID needs its own consent, separate from recording consent; a changed notice requires consent again. |
| BR-117 | Withdrawing consent or deactivating the account deletes all voice samples, suggestions and names filled in by the system; names the Secretary assigned stay. |
| BR-118 | Each person has two voice sample sets under one consent, up to 5 self-recorded and up to 5 collected on their behalf; only the owner plays the self-recorded samples. |
| BR-119 | A holder of the collection right records, plays and deletes collected samples, only of people who consented, and asks the person at each collection. |
| BR-120 | Speaker suggestions compare only consenting people on the current list, are shown only to the Secretary, never suggest a Guest, and are assigned only to people on the list. |
| BR-121 | Audio and voice samples are processed inside the system and never sent to an outside service. |
| BR-122 | The combined recording is only for listening and downloading, and is never transcribed. |
| BR-123 | The system fills in a name when a voice clearly matches a consenting person on the current list. |
| BR-124 | Only people who consented to notice version 1.2 or later have names filled in automatically, others are only suggested; collection on someone's behalf needs version 1.4. |
| BR-125 | When transcription runs again, hand-assigned names, corrected text and moved sentences carry over to the matching sentence or voice. |
| BR-126 | Only the Chair or the Secretary requests, edits and reviews the AI draft, only when the Meeting is Ended and has at least one recorded sentence. |
| BR-127 | Other people read the AI draft only after it has been reviewed. |
| BR-128 | The AI writes only from the meeting record; it does not write attendance, guess names or write vote results. |
| BR-129 | By default the AI runs on the team's own server; the Admin may choose an outside API for Normal Meetings; access keys are stored encrypted. |
| BR-130 | Minutes follow one fixed template (Form 1.4, Decree 30/2020/ND-CP); templates are not managed. |
| BR-131 | Only the Chair or the Secretary reads and edits the minutes draft, only after the Meeting is Ended; other people see only issued minutes. |
| BR-132 | Minutes of a Secret Meeting cannot be downloaded as Word or PDF. |
| BR-133 | The system does not track assignees, due dates or task progress; the Tasks section is plain text. |
| BR-134 | Only the Secretary writes meeting notes, from InProgress until the minutes draft is built; the Chair can read them. |
| BR-135 | Only the Secretary submits the minutes for approval, only after the Meeting is Ended. |
| BR-136 | Only the Chair approves or returns the minutes; returning them requires a reason. |
| BR-137 | Approval issues the minutes immediately; the Meeting stays Ended. |
| BR-138 | Issued minutes never change; a correction creates a revision from the issued version, with at most one revision not yet issued. |
| BR-139 | Issued and superseded minutes are read and downloaded by everyone who can read the Meeting except Guests; drafts only by the Chair and the Secretary. |
| BR-140 | Calendar sharing has two levels: level 1 covers Normal Meetings, level 2 Normal and Internal; Secret Meetings are never shared. |
| BR-141 | The viewer sees only busy, free, unknown or not shared, never the title, location, attendees or sensitivity; only Organizers use calendar sharing. |
| BR-142 | Each pair of calendar owner and viewer has at most one Pending or Effective share. |
| BR-143 | A share stops when the viewer is no longer an Organizer or is deactivated, and expires after its end date. |
| BR-144 | Days outside the shared range are unknown; a person who has not shared counts as neither free nor busy. |
| BR-145 | A Meeting above the shared level counts as free; the searcher's own Meetings always count. |
| BR-146 | Only a Chair, Secretary or Member assignment in a WaitingApproval, Approved, Published, InProgress or Ended Meeting counts as busy. |
| BR-147 | A person who declined, and Draft, Rejected or Cancelled Meetings, do not count as busy. |
| BR-148 | A slot that touches a Secret Meeting can still create a Draft, without a special warning. |
| BR-149 | Internal users view and export reports; Guests do not. |
| BR-150 | A report counts only Meetings the user can read, by month of planned start, current status and current sensitivity. |
| BR-151 | Attendance rate, time to minutes, meeting duration and recorded hours follow their formulas; combined recordings add no hours; Meetings missing the data are left out of the averages. |
| BR-152 | Header figures show numbers only; query tables show a person's name or a Meeting's code and title, only for Meetings the viewer reads; a Guest shows only a name. |
| BR-153 | The report query counts only records the viewer can read: draft Votes and unissued minutes for the Chair and Secretary, recordings for participants. |
| BR-154 | When transcription runs again, what cannot be matched leaves only a trace in the audit log, without the transcript text. |

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
| 8 | MSG01 | Redirect to sign-in | Common – Session Renewal | Your session has expired. Please sign in again. |
| 9 | MSG02 | Redirect to sign-in | Common – Session Renewal | Your account is currently unavailable. Please contact your administrator for assistance. |
| 10 | MSG07 | Toast | UC-06a – Create User Account / Normal Flow | User account created successfully. |
| 11 | MSG08 | Field error | UC-06a – Create User Account / Exception Flows | Email is already in use. |
| 12 | MSG09 | Field error / Form error | UC-06a – Create User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- fullName: Full name is required. / Full name must not exceed 200 characters.<br>- email: Email is required. / Email is invalid.<br>- password: Password must be at least 12 characters.<br>- roleId: The role does not exist. |
| 13 | MSG10 | Toast | UC-06b – Update User Account / Normal Flow | User account updated successfully. |
| 14 | MSG08 | Field error | UC-06b – Update User Account / Exception Flows | Email is already in use. |
| 15 | MSG09 | Field error / Form error | UC-06b – Update User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- fullName: Full name is required. / Full name must not exceed 200 characters.<br>- email: Email is required. / Email is invalid.<br>- password: Password must be at least 12 characters.<br>- roleId: The role does not exist. |
| 16 | MSG11 | Toast | UC-06c – Deactivate / Reactivate User Account / Normal Flow | User account status changed successfully. |
| 17 | MSG09 | Form error | UC-06c – Deactivate / Reactivate User Account / Exception Flows | Invalid account information.<br>Field errors:<br>- isActive: The status to set for the account is missing. |
| 18 | MSG12 | Dialog | UC-06c – Deactivate / Reactivate User Account / Exception Flows | Unable to change the status of this user account. |
| 19 | MSG13 | Toast | UC-07 – Reset User Password / Normal Flow | Password reset email sent successfully. |
| 20 | MSG92 | Toast | UC-07 – Reset User Password / Normal Flow | Password reset successfully. Please sign in with your new password. |
| 21 | MSG06 | Toast | UC-07 – Reset User Password / Alternative Flows | Unable to sign out. Please try again. |
| 22 | MSG14 | Form error | UC-07 – Reset User Password / Exception Flows | Password reset is not available for this account. |
| 23 | MSG15 | Toast | UC-07 – Reset User Password / Exception Flows | Unable to send the password reset email. Please try again. |
| 24 | MSG93 | Page | UC-07 – Reset User Password / Exception Flows | The password reset link is invalid or has expired. Please contact an administrator. |
| 25 | MSG94 | Field error | UC-07 – Reset User Password / Exception Flows | Invalid new password.<br>Field errors:<br>- newPassword: Password must be at least 12 characters. |
| 26 | MSG129 | Page | UC-07 – Reset User Password / Exception Flows | No password reset code was found. Please open the link in the email again. |
| 27 | MSG10 | Toast | UC-08 – Assign System Role to User / Normal Flow, Alternative Flows | User account updated successfully. |
| 28 | MSG09 | Field error / Form error | UC-08 – Assign System Role to User / Exception Flows | Invalid account information.<br>Field errors:<br>- roleId: The role does not exist. |
| 29 | MSG12 | Form error | UC-08 – Assign System Role to User / Exception Flows | Unable to change the status of this user account. |
| 30 | MSG16 | Toast | UC-10 – Configure Role Permissions / Normal Flow | Role permissions updated successfully. |
| 31 | MSG17 | Inline error (under role row) | UC-10 – Configure Role Permissions / Exception Flows | Invalid permission configuration.<br>Field errors:<br>- roleId: Invalid role.<br>- capabilities: The list of permissions to set for the role is missing. / Invalid permissions: {permission codes}. / The permission list contains invalid values.<br>- adminUsers: Cannot save this configuration: after saving, no active account would hold the user administration permission, so no one could access the permission screen. |
| 32 | MSG18 | Inline error (under role row) | UC-10 – Configure Role Permissions / Exception Flows | Unable to update role permissions. Please try again. |
| 33 | MSG19 | Toast | UC-11a – Create Role / Normal Flow | Role created successfully. |
| 34 | MSG20 | Field error / Form error | UC-11a – Create Role / Exception Flows | Invalid role information.<br>Field errors:<br>- name: Role name is required. / Role name must not exceed 64 characters.<br>- description: Description must not exceed 256 characters.<br>- capabilities: Invalid permissions: {permission codes}. |
| 35 | MSG21 | Field error | UC-11a – Create Role / Exception Flows | A role with this name already exists. Please choose a different name. |
| 36 | MSG26 | Form error | UC-11a – Create Role / Exception Flows | Unable to create the role. Please try again. |
| 37 | MSG22 | Toast | UC-11b – Update Role / Normal Flow | Role updated successfully. |
| 38 | MSG22 | Toast | UC-11b – Update Role / Alternative Flows | Role updated successfully. |
| 39 | MSG24 | Form error | UC-11b – Update Role / Exception Flows | This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions. |
| 40 | MSG20 | Field error / Form error | UC-11b – Update Role / Exception Flows | Invalid role information.<br>Field errors:<br>- name: Role name is required. / Role name must not exceed 64 characters.<br>- description: Description must not exceed 256 characters.<br>- capabilities: Invalid permissions: {permission codes}. |
| 41 | MSG21 | Field error | UC-11b – Update Role / Exception Flows | A role with this name already exists. Please choose a different name. |
| 42 | MSG27 | Form error | UC-11b – Update Role / Exception Flows | Unable to update the role. Please try again. |
| 43 | MSG23 | Toast | UC-11c – Delete Role / Normal Flow | Role deleted successfully. |
| 44 | MSG24 | Dialog | UC-11c – Delete Role / Exception Flows | This is a predefined system role and cannot be renamed or deleted. You can still configure its permissions. |
| 45 | MSG25 | Dialog | UC-11c – Delete Role / Exception Flows | This role is still held by {n} user account(s). Reassign them to another role before deleting. |
| 46 | MSG28 | Dialog | UC-11c – Delete Role / Exception Flows | Unable to delete the role. Please try again. |
| 47 | MSG29 | Toast | UC-13 – Create Meeting / Normal Flow | Meeting created successfully. |
| 48 | MSG31 | Field error / Form error | UC-13 – Create Meeting / Exception Flows | Invalid meeting information.<br>Field errors:<br>- title: Meeting title is required. / Meeting title must not exceed 300 characters.<br>- location: Location must not exceed 300 characters.<br>- description: Description must not exceed 4000 characters.<br>- mode: Invalid meeting mode.<br>- sensitivity: Invalid sensitivity level. |
| 49 | MSG35 | Form error | UC-13 – Create Meeting / Exception Flows | Unable to create the meeting. Please try again. |
| 50 | MSG30 | Toast | UC-14 – Update Meeting / Normal Flow | Meeting updated successfully. |
| 51 | MSG31 | Field error / Form error | UC-14 – Update Meeting / Exception Flows | Invalid meeting information.<br>Field errors:<br>- title: Meeting title is required. / Meeting title must not exceed 300 characters.<br>- location: Location must not exceed 300 characters.<br>- description: Description must not exceed 4000 characters.<br>- mode: Invalid meeting mode.<br>- sensitivity: Invalid sensitivity level. |
| 52 | MSG31 | Form error | UC-14 – Update Meeting / Exception Flows (Malformed JSON request body; same MSG31 validation category.) | Invalid meeting information. |
| 53 | MSG34 | API error | Technical API response – outside UC-14 business flow | Content-Type must be application/json. |
| 54 | MSG33 | Dialog | UC-14 – Update Meeting / Exception Flows | This meeting can no longer be edited. |
| 55 | MSG36 | Form error | UC-14 – Update Meeting / Exception Flows | Unable to update the meeting. Please try again. |
| 56 | —<br>(HTTP 404) | Page | UC-15 – View Meeting Details / Exception Flows | Meeting not found. |
| 57 | MSG37 | Page | UC-15 – View Meeting Details / Exception Flows | Unable to load meeting details. Please try again. |
| 58 | — | Table message | UC-16 – View Meeting List / Alternative Flows | No meetings match the filter. |
| 59 | MSG38 | Table message | UC-16 – View Meeting List / Exception Flows | Unable to load meetings. Please try again. |
| 60 | MSG32 | Table message | UC-16 – View Meeting List / Exception Flows | The meeting list criteria are invalid. |
| 61 | MSG41 | Dialog | UC-17 – Submit Meeting for Approval / Exception Flows | This meeting can no longer be submitted for approval. |
| 62 | MSG42 | Toast | UC-17 – Submit Meeting for Approval / Exception Flows | Unable to submit the meeting for approval. Please try again. |
| 63 | MSG43 | Toast | UC-18 – Review Meeting Approval / Normal Flow | Meeting approved successfully. |
| 64 | MSG44 | Toast | UC-18 – Review Meeting Approval / Alternative Flows | Meeting rejected successfully. |
| 65 | MSG45 | Toast | UC-19 – Publish Meeting / Normal Flow | Meeting published successfully. |
| 66 | MSG46 | Dialog | UC-19 – Publish Meeting / Exception Flows | This meeting can no longer be published. |
| 67 | MSG47 | Toast | UC-19 – Publish Meeting / Exception Flows | Unable to publish the meeting. Please try again. |
| 68 | MSG39 | List message | UC-76 – View Notifications / Exception Flows | The requested notification page is unavailable. |
| 69 | MSG40 | List message | UC-76 – View Notifications / Exception Flows | Unable to load notifications. Please try again. |
| 82 | MSG60 | Toast | UC-23a – Create Agenda Item / Normal Flow | Agenda item created successfully. |
| 83 | MSG61 | Toast | UC-23b – Update Agenda Item / Normal Flow | Agenda item updated successfully. |
| 84 | MSG62 | Toast | UC-23c – Delete Agenda Item / Normal Flow | Agenda item deleted successfully. |
| 85 | MSG63 | Toast | UC-23d – Reorder Agenda Items / Normal Flow | Agenda order updated successfully. |
| 86 | MSG64 | Toast | UC-24 – Set Current Agenda Item / Normal Flow | Current agenda item updated successfully. |
| 87 | MSG65 | Toast | UC-25 – Complete Agenda Item / Normal Flow | Agenda item completed successfully. |
| 88 | MSG66 | Field error / Form error | UC-23a – Create Agenda Item / Exception Flows; UC-23b – Update Agenda Item / Exception Flows | Invalid agenda item information.<br>Field errors:<br>- title: Agenda item title is required.<br>- documents: One or more selected documents are not available to this meeting. |
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
| 99 | MSG83 | Field error | UC-18 – Review Meeting Approval / Exception Flows | Rejection reason is required. |
| 100 | MSG84 | Field error | UC-18 – Review Meeting Approval / Exception Flows | Rejection reason must not exceed 1000 characters. |
| 101 | MSG95 | Page / Table message | UC-29 – View Meeting Participants / Exception Flows; UC-32 – View Participant Response Status / Exception Flows | Unable to load meeting participants. Please try again. |
| 102 | MSG96 | Toast | UC-30a – Add Meeting Participant / Normal Flow | Meeting participant added successfully. |
| 103 | MSG97 | Field error / Form error | UC-30a – Add Meeting Participant / Exception Flows | Invalid meeting participant information.Field messages:- The selected user is not available.- This user is already a meeting participant.- A valid meeting role is required.- The Member may only assign a missing Secretary during preparation.- Guest name and email are required.- Guest email is invalid.- This guest email is already invited to the meeting.- This email is already represented by an internal meeting participant. |
| 104 | MSG98 | Dialog | UC-30a, UC-30b, UC-30c / Exception Flows | The meeting participant list cannot be modified in its current status. |
| 105 | MSG99 | Toast | UC-30a – Add Meeting Participant / Exception Flows | Unable to add the meeting participant. Please try again. |
| 106 | MSG100 | Toast | UC-30b – Manage Meeting Role & Additional Permissions / Normal Flow | Meeting role updated successfully. |
| 107 | MSG101 | Field error | UC-30b – Manage Meeting Role & Additional Permissions / Exception Flows | A valid meeting role is required. |
| 108 | MSG102 | Toast | UC-30b – Manage Meeting Role & Additional Permissions / Exception Flows; UC-34 / EX-3 | Unable to update the meeting role. Please try again. |
| 109 | MSG103 | Toast | UC-30c – Remove Meeting Participant / Normal Flow | Meeting participant removed successfully. |
| 110 | MSG104 | Toast | UC-30c – Remove Meeting Participant / Exception Flows | Unable to remove the meeting participant. Please try again. |
| 111 | MSG105 | Toast | UC-31 – Respond to Meeting Invitation / Normal Flow | Meeting invitation response recorded successfully. |
| 112 | MSG106 | Field error / Form error | UC-31 – Respond to Meeting Invitation / Exception Flows | Invalid meeting invitation response. |
| 113 | MSG107 | Dialog | UC-31 – Respond to Meeting Invitation / Exception Flows | This meeting invitation can no longer be answered. |
| 114 | MSG108 | Toast | UC-31 – Respond to Meeting Invitation / Exception Flows | Unable to save the meeting invitation response. Please try again. |
| 115 | MSG109 | Dialog | UC-30b – Manage Meeting Role & Additional Permissions / Exception Flows; UC-30c – Remove Meeting Participant / Exception Flows | The meeting must retain its required Chair and Secretary. Assign a replacement first. |
| 116 | MSG110 | Toast | UC-20 – Start Meeting / Normal Flow | Meeting started successfully. |
| 117 | MSG111 | Toast | UC-21 – End Meeting / Normal Flow | Meeting ended successfully. |
| 118 | MSG112 | Dialog | UC-20 – Start Meeting / Exception Flows | This meeting cannot be started. |
| 119 | MSG113 | Dialog | Retired; formerly UC-52 (Meeting Session) / End Meeting gate | Retired with Meeting Sessions (ADR-0013); formerly “Complete or cancel all unfinished meeting sessions before ending the meeting.” This code must not be reused. |
| 120 | MSG114 | Dialog | UC-21 – End Meeting / Exception Flows | This meeting cannot be ended. |
| 121 | MSG115 | Toast | UC-20 – Start Meeting / Exception Flows | Unable to start the meeting. Please try again. |
| 122 | MSG116 | Toast | UC-21 – End Meeting / Exception Flows | Unable to end the meeting. Please try again. |
| 132 | MSG126 | Inline / Form error | UC-04 / EX-2 | Unable to verify the guest invitation. Check the code and try again. |
| 134 | MSG128 | Page / Panel message | UC-26 – View Meeting Agenda / Exception Flows | Unable to load the meeting agenda. Please try again. |
| 113 | MSG138 | Information | UC-14 – Meeting rescheduled successfully | Meeting rescheduled successfully. Invitees will be notified. |
| 114 | MSG139 | Form error | Retired; formerly UC-21 / Open Vote gate | Retired on 13/10/2026: open Votes do not block End Meeting. This code must not be reused. |
| 135 | MSG140 | Form error | UC-17 – Submit Meeting for Approval / EX-5 | No eligible meeting approver is available. Please contact the administrator. |
| 136 | MSG141 | Retired | Retired; formerly UC-23b / EX-4 | Retired on 18/10/2026 (PO 16/10/2026): all Agenda Item fields may be edited in any execution status. This code must not be reused. |
| 137 | MSG142 | Dialog / Form error | UC-23c / EX-2 | Only Pending agenda items can be deleted. |
| 138 | MSG143 | Dialog / Form error | UC-23d / EX-2 | The agenda order is invalid or out of date. Refresh the agenda and try again. |
| 139 | MSG144 | Retired | Retired; formerly UC-24 / EX-3 | Retired on 18/10/2026 (PO 16/10/2026): any Pending Agenda Item may be started, whatever its position. This code must not be reused. |
| 140 | MSG145 | Form error | UC-30a / Detailed selection validation; reserved | One or more selected participants are invalid. No participants were added. |
| 141 | MSG146 | Toast | UC-30b / Reserved; refined UC uses MSG102 | Unable to replace the Chair. Meeting roles remain unchanged. |
| 142 | MSG147 | Toast | UC-33 / Normal Flow | Meeting attendance saved successfully. |
| 143 | MSG148 | Field error | UC-33 / EX-1 | Select Present or Absent for each submitted participant or Guest. |
| 144 | MSG149 | Dialog | UC-33 / EX-2 | Attendance can only be recorded while the meeting is in progress. |
| 145 | MSG150 | Toast | UC-33 / EX-5 | Unable to confirm the attendance save result. Please reload the data before trying again. |
| 146 | MSG151 | Dialog | UC-33 / EX-4 / Changed submitted participant or attendance | The participant or attendance data has changed. Please reload and review before saving. |
| 147 | MSG152 | Retired | Retired; formerly UC-30a / EX-1; UC-30b / EX-1 | Retired on 20/10/2026 (PO 20/10/2026): additional Meeting permissions per participant were removed. This code must not be reused. |
| 148 | MSG153 | Retired | Retired; formerly UC-30b / Normal Flow | Retired on 20/10/2026 (PO 20/10/2026): additional Meeting permissions per participant were removed. This code must not be reused. |
| 149 | MSG154 | Information | UC-30b / Unchanged selection UI; no separate exception flow. UC-31 / AF-1 – the same response is submitted again. UC-14 / AF-2 – nothing has changed | No changes to save. |
| 150 | MSG155 | Form error | Retired on 13/10/2026; formerly UC-21 / EX-3 | Retired: attendance incompleteness must not block End Meeting. This code must not be reused. |
| 151 | MSG156 | Form error | UC-35a / EX-1; UC-35b / EX-1; UC-38 / EX-2 | Meeting documents are not accessible. |
| 152 | MSG157 | Form error | UC-35a / EX-2 | Unable to load the document list. Please try again. |
| 153 | MSG158 | Form error | UC-35b / EX-2; UC-37a / EX-3 | Unable to display the document. Please try again. |
| 154 | MSG159 | Form error | UC-35b / EX-3 | Secret Meeting documents can only be viewed online and cannot be downloaded. |
| 155 | MSG160 | Form error | UC-35b / EX-4 | Unable to download the document. Please try again. |
| 156 | MSG161 | Toast | UC-36a / Normal Flow | Documents added to the Meeting successfully. |
| 157 | MSG162 | Form error | UC-36a / EX-1 | You no longer have permission to add documents to this Meeting. |
| 158 | MSG163 | Retired | ADR-0020 | Retired: document addition and Agenda Item linking are separate operations under ADR-0020. Do not reuse. |
| 159 | MSG164 | Field error / Form error | UC-36a / EX-2, UC-36b / AF-1, UC-39 / EX-1, UC-68a / AF-1 | Select valid PDF or DOCX files, each no larger than 10 MB. |
| 160 | MSG165 | Form error | UC-36a / EX-4 | One or more selected documents are already attached to the Meeting. |
| 161 | MSG166 | Form error | UC-36a / EX-5 | Unable to add the documents. No documents were added. Please try again. |
| 162 | MSG167 | Toast | UC-36c / Normal Flow | Document removed from the Meeting successfully. |
| 163 | MSG168 | Retired | ADR-0020 | Retired: Meeting Minutes no longer lock document removal under ADR-0020. Do not reuse. |
| 164 | MSG169 | Form error | UC-36c / EX-2 | You no longer have permission to remove documents from this Meeting. |
| 165 | MSG170 | Form error | UC-36b, UC-36d / EX-2; UC-36c / EX-3; UC-68a / EX-2 | This document is no longer attached to the Meeting. Refresh the document list. |
| 166 | MSG171 | Form error | UC-36c / EX-4 | Unable to remove the document. It remains attached to the Meeting. Please try again. |
| 167 | MSG172 | Form error | UC-36a / EX-6 | Documents cannot be added after the Meeting has ended. |
| 168 | MSG173 | Retired | ADR-0020 | Retired: Agenda Item status no longer locks document links under ADR-0020. Do not reuse. |
| 169 | MSG174 | Form error | UC-36a / EX-7 (also a former Chair or Secretary who can no longer view the Meeting) and UC-27 / EX-4 · Implemented; UC-36b / EX-7, UC-36c / EX-5, UC-36d / EX-7 reserved | This Meeting is not accessible. |
| 170 | MSG175 | Form error | UC-37 / EX-2; UC-40a / EX-1; UC-40b / EX-1; UC-40c / EX-1; UC-41b / EX-1; UC-41c / EX-1; UC-41d / EX-1; UC-37a / EX-1 | This personal-library item is not available. |
| 171 | MSG176 | Form error | UC-37 / EX-1; UC-37a / EX-2 | Unable to load your personal document library. Please try again. |
| 172 | MSG177 | Toast | UC-39 / Normal Flow | Documents uploaded to your personal library successfully. |
| 173 | MSG178 | Form error | UC-39 / EX-3 | Unable to upload the files. No documents were added. Please try again. |
| 174 | MSG179 | Form error | UC-39 / EX-3, UC-40c / EX-2, UC-41a / EX-2, UC-41c / EX-1 | The selected destination folder is not available in your personal library. |
| 175 | MSG180 | Toast | UC-40a / Normal Flow | Document deleted from your personal library successfully. |
| 176 | MSG181 | Form error | UC-40a / EX-2 | Unable to delete the document. Your personal library remains unchanged. |
| 177 | MSG182 | Form error | Retired — document-name constraint removed | Retired: no document-name validation is specified under this code. Do not reuse. |
| 178 | MSG183 | Toast | UC-40b / Normal Flow | Document renamed successfully. |
| 179 | MSG184 | Form error | UC-40b / EX-2 | Unable to rename the document. Its name remains unchanged. |
| 180 | MSG185 | Toast | UC-40c / Normal Flow | Document moved successfully. |
| 181 | MSG186 | Form error | UC-40c / EX-3 | Unable to move the document. Its location remains unchanged. |
| 182 | MSG187 | Toast | UC-41a / Normal Flow | Folder created successfully. |
| 183 | MSG188 | Form error | UC-41a / EX-1; UC-41b / EX-2 | Enter a folder name. |
| 184 | MSG189 | Form error | UC-41a / EX-3; UC-41c / EX-4 | Folders cannot be nested more than five levels deep. |
| 185 | MSG190 | Form error | UC-41a / EX-4 | Unable to create the folder. No folder was created. |
| 186 | MSG191 | Toast | UC-41b / Normal Flow | Folder renamed successfully. |
| 187 | MSG192 | Form error | UC-41b / EX-3 | Unable to rename the folder. Its name remains unchanged. |
| 188 | MSG193 | Form error | UC-41c / EX-3 | A folder cannot be moved into itself or one of its descendants. |
| 189 | MSG194 | Toast | UC-41c / Normal Flow | Folder moved successfully. |
| 190 | MSG195 | Form error | UC-41c / EX-5 | Unable to move the folder. Its location remains unchanged. |
| 191 | MSG196 | Form error | UC-41d / EX-2 | Only an empty folder can be deleted. |
| 192 | MSG197 | Toast | UC-41d / Normal Flow | Folder deleted successfully. |
| 193 | MSG198 | Form error | UC-41d / EX-3 | Unable to delete the folder. The folder remains unchanged. |
| 194 | MSG199 | Toast | UC-38 / Normal Flow | Comment added successfully. |
| 195 | MSG200 | Form error | UC-38 / EX-1 | Enter a comment. |
| 196 | MSG201 | Form error | Retired — no separate commenting permission | Retired: document-viewing access also permits commenting. Do not reuse. |
| 197 | MSG202 | Form error | UC-38 / EX-3 | Unable to add the comment. No comment was saved. Please try again. |
| 198 | MSG203 | Form error | Retired — mandatory PDF page reference removed | Retired: a PDF comment does not require a page reference. Do not reuse. |
| 199 | MSG204 | Form error | UC-38 / EX-4 | Unable to load the document comments. Please try again. |
| 200 | MSG205 | Form error | UC-62a, UC-62b / EX-1; UC-63, UC-64 / EX-6; UC-65 / EX-7; UC-66 / EX-3; UC-67 / EX-5; UC-68a / EX-5; UC-68b / EX-4 | Meeting votes are not accessible. |
| 201 | MSG206 | Form error | UC-62a / EX-2 | Unable to load the meeting votes. Please try again. |
| 202 | MSG207 | Notification | UC-62a / AF-1 | No votes to display. |
| 203 | MSG208 | Form error | UC-62b / EX-2 | Unable to load the vote status or result. Please try again. |
| 204 | MSG209 | Form error | UC-63, UC-64 / Invalid configuration | Invalid vote information. Check the highlighted fields. |
| 205 | MSG210 | Notification | UC-63 / Normal Flow | Vote draft created successfully. |
| 206 | MSG211 | Notification | UC-63 / AF-1 | Vote draft updated successfully. |
| 207 | MSG212 | Notification | UC-63 / AF-2 | Vote draft deleted successfully. |
| 208 | MSG213 | Form error | UC-63 / EX-2; UC-68a, UC-68b / EX-1 | Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress. |
| 209 | MSG214 | Form error | UC-63 / EX-3 | Unable to create the vote draft. No draft was created. Please try again. |
| 210 | MSG215 | Form error | UC-63 / EX-4 | Unable to update the vote draft. The draft remains unchanged. Please try again. |
| 211 | MSG216 | Form error | UC-63 / EX-5 | Unable to delete the vote draft. The draft remains unchanged. Please try again. |
| 212 | MSG217 | Notification | UC-64 / Normal Flow | Voting opened successfully. |
| 213 | MSG218 | Form error | UC-64 / EX-2 | Only the current Chair can open a Draft vote while the meeting is InProgress. |
| 214 | MSG219 | Form error | UC-64 / EX-3 | Another vote is already open in this meeting. Close it before opening this vote. |
| 215 | MSG220 | Form error | UC-64 / EX-4 | No eligible voters are available for the selected roles. Voting was not opened. |
| 216 | MSG221 | Form error | UC-64 (opening the Vote cannot be saved) | Unable to open voting. Voting was not opened. Please try again. |
| 217 | MSG222 | Form error | UC-65 / EX-1 | Select one option before submitting your ballot. |
| 218 | MSG223 | Form error | UC-65 / EX-2 | The selected option is not available for this vote. |
| 219 | MSG224 | Form error | UC-65 / EX-3 | You are not eligible to cast a ballot in this vote. |
| 220 | MSG225 | Form error | UC-65 / EX-4 | This vote is no longer accepting ballots. |
| 221 | MSG226 | Form error | UC-65 / EX-5 | Your ballot has already been recorded. It cannot be changed or withdrawn. |
| 222 | MSG227 | Notification | UC-65 / Normal Flow | Your ballot has been recorded. |
| 223 | MSG228 | Form error | UC-65 / EX-6 | Unable to record your ballot. No ballot was recorded. Please try again. |
| 224 | MSG229 | Notification | UC-66 / Normal Flow | Voting closed. The result is available. |
| 225 | MSG230 | Form error | UC-66 / EX-1 | Only the current Chair can close an Open vote while the meeting is InProgress or Ended. |
| 226 | MSG231 | Form error | UC-66 / EX-2 | Unable to close voting. The vote and saved ballots remain unchanged. Please try again. |
| 227 | MSG232 | Form error | UC-67 / EX-1 | Select Passed, NotPassed or Deferred. |
| 228 | MSG233 | Form error | UC-67 / EX-2 | The vote conclusion has already been recorded and cannot be changed. |
| 229 | MSG234 | Form error | UC-67 / EX-3 | Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended. |
| 230 | MSG235 | Notification | UC-67 / Normal Flow | Vote conclusion recorded successfully. |
| 231 | MSG236 | Form error | UC-67 / EX-4 | Unable to record the vote conclusion. No conclusion was recorded. Please try again. |
| 232 | MSG237 | Retired | FE-11 | Retired: closing or cancelling the UI does not require a system message. Do not reuse. |
| 213 | MSG238 | Dành sẵn, chưa triển khai | UC-36b / Normal Flow | Documents attached to the Agenda Item successfully. |
| 214 | MSG239 | Dành sẵn, chưa triển khai | UC-36d / Normal Flow | Document detached from the Agenda Item successfully. |
| 215 | MSG240 | Dành sẵn, chưa triển khai | UC-36b / EX-1 | You no longer have permission to link documents in this Meeting. |
| 216 | MSG241 | Dành sẵn, chưa triển khai | UC-36d / EX-1 | You no longer have permission to unlink documents in this Meeting. |
| 217 | MSG242 | Dành sẵn, chưa triển khai | UC-36b, UC-36d / EX-3 | Select an available Agenda Item in this Meeting. |
| 218 | MSG243 | Dành sẵn, chưa triển khai | UC-36b / EX-4 | One or more selected documents are already attached to the Agenda Item. |
| 219 | MSG244 | Dành sẵn, chưa triển khai | UC-36b / EX-5 | Unable to link the document. No links were changed. Please try again. |
| 220 | MSG245 | Dành sẵn, chưa triển khai | UC-36d / EX-5 | Unable to unlink the document. No links were changed. Please try again. |
| 221 | MSG246 | Dành sẵn, chưa triển khai | UC-36d / EX-4 | This document is no longer linked to the selected Agenda Item. Refresh the document list. |
| 222 | MSG247 | Dành sẵn, chưa triển khai | UC-36b, UC-36d / EX-6 | Document links cannot be changed after the Meeting has ended. |
| 223 | MSG248 | Dành sẵn, chưa triển khai | UC-36c / EX-1 | Documents cannot be removed after the Meeting has ended. |
| 224 | MSG249 | Dành sẵn, chưa triển khai | UC-36a / EX-2 | Select one or more available documents from your personal library. |
| 225 | MSG250 | Retired | Retired; formerly UC-23b / EX-4 | Retired on 18/10/2026 (PO 16/10/2026): Agenda Items no longer have a Planned Duration and Presenter is not locked. This code must not be reused. |
| 226 | MSG251 | Dành sẵn, chưa triển khai | UC-38 / EX-5 | Comments are read-only after the Meeting has ended. |
| 233 | MSG252 | Form error | UC-65 / EX-8 | Confirm that you have read all vote documents before casting your ballot. |
| 234 | MSG253 | Toast | UC-42 / Normal Flow | Document read confirmed. |
| 235 | MSG254 | Form error | UC-42 / EX-1 | Scroll to the last page of the document before confirming that you have read it. |
| 236 | MSG255 | Toast | UC-42 / EX-3 | Unable to confirm document reading. Please try again. |
| 237 | MSG256 | Dialog | UC-30b / EX-5 | From publication onward, only the current Chair can hand over the Chair role while the Chair's account is active. |
| 238 | MSG257 | Toast | UC-34 / Normal Flow | Chair role handed over successfully. |
| 239 | MSG258 | Not displayed | UC-34 / AF-1 (returned by the API; the screen offers no Chair option instead of showing it) | No participant can receive the Chair role. Add an internal participant first. |
| 240 | MSG259 | Form error | UC-34 / EX-1 | The selected participant can no longer receive the Chair role. Select another participant. |
| 241 | MSG260 | Dialog | UC-34 / EX-2 | You can no longer hand over the Chair role for this meeting. |
| 242 | MSG261 | Toast | UC-22 / Normal Flow | Meeting cancelled successfully. |
| 243 | MSG262 | Form error | UC-22 / EX-1 | Enter a reason for cancelling the meeting. |
| 244 | MSG263 | Dialog | UC-22 / EX-2 | This meeting can no longer be cancelled. |
| 245 | MSG264 | Toast | UC-22 / EX-3 | Unable to cancel the meeting. Please try again. |
| 246 | MSG265 | Toast | UC-68a / Normal Flow | Documents attached to the vote successfully. |
| 247 | MSG266 | Form error | UC-68a / EX-3 | One or more selected documents are already attached to the vote. |
| 248 | MSG267 | Toast | UC-68a / EX-4 | Unable to attach the documents. No documents were attached. Please try again. |
| 249 | MSG268 | Toast | UC-68b / Normal Flow | Document detached from the vote successfully. |
| 250 | MSG269 | Dialog | UC-68b / EX-2 | This document is no longer attached to the vote. Refresh the list. |
| 251 | MSG270 | Toast | UC-68b / EX-3 | Unable to detach the document. No changes were made. Please try again. |
| 252 | MSG271 | Toast | UC-27 / Normal Flow | Agenda item conclusion saved. |
| 253 | MSG272 | Form error | UC-27 / EX-1 | Enter a conclusion of up to 2,000 characters. |
| 254 | MSG273 | Dialog | UC-27 / EX-2 | The conclusion of this agenda item can no longer be changed. |
| 255 | MSG274 | Toast | UC-27 / EX-3 | Unable to save the agenda item conclusion. Please try again. |
| 256 | MSG288 | Form error | UC-39 / EX-2 | Upload up to 10 files at a time, no larger than 100 MB in total. |
| 257 | MSG289 | Toast | UC-33 / EX-6 | Unable to load attendance data. |
| 258 | MSG285 | Toast | UC-31 – Respond to Meeting Invitation / EX-4 | You have already answered this invitation. Your answer cannot be changed. |
| 259 | MSG286 | Toast | UC-31 – Respond to Meeting Invitation / EX-5 | The Chair runs the Meeting, so no attendance confirmation is needed. |
| 260 | MSG277 | Form error | UC-13 – Create Meeting / EX-3 | The meeting participant list is not valid. |
| 261 | MSG78 | Toast | UC-17 – Submit Meeting for Approval / Normal Flow | Meeting submitted for approval. |
| 262 | MSG79 | Toast | Meeting workflow – Return to Draft / Normal Flow | Meeting returned to Draft. |
| 263 | MSG80 | Form error | Meeting workflow – Status change request / Validation (API) | The status change request is not valid. |
| 264 | MSG81 | Form error | UC-17 – Submit Meeting for Approval / Exception Flows | The meeting does not have enough information to be submitted for approval. |
| 265 | MSG82 | Form error | UC-17 – Submit Meeting for Approval / Exception Flows | The approver is not valid or can no longer approve meetings. |
| 266 | MSG85 | Dialog | Meeting workflow and participant actions / Authorization (API); UC-22 / EX-5 | You do not have permission to perform this action. |
| 267 | MSG86 | Form error | Meeting workflow – View status history / Validation (API) | The query parameters are not valid. |
| 268 | MSG87 | Form error | Meeting workflow and Agenda Management / Validation (API) | The request content is larger than 64 KiB. |
| 269 | MSG88 | Toast | Meeting workflow – Approve, Reject, Return to Draft, Transfer Organizer / Exception Flows | Unable to change the meeting status. Please try again. |
| 270 | MSG89 | Toast | Meeting workflow – Transfer Organizer / Normal Flow | Meeting transferred to the new organizer. |
| 271 | MSG117 | Banner | UC-43 – Join Meeting Room / Alternative Flows (AF-3); UC-47 – Moderate Meeting Room / Exception Flows; UC-50 – Register to Speak / Exception Flows | The meeting room opens only while the meeting is in progress. |
| 272 | MSG118 | Toast | UC-43 – Join Meeting Room / Exception Flows | Unable to join the meeting room. Please try again. |
| 273 | MSG123 | Dialog | UC-47 – Moderate Meeting Room / Exception Flows | Only the Chair can mute another person's microphone. |
| 274 | MSG124 | Toast | UC-47 – Moderate Meeting Room / Exception Flows; UC-50 – Register to Speak / Exception Flows | The meeting room server did not respond. Please try again. |
| 275 | MSG130 | Toast | Meeting workflow – Approver list / Exception Flows | Unable to load the approver list. Please try again. |
| 276 | MSG131 | Toast | Meeting workflow / Exception Flows | Unable to verify the accounts for this meeting action. Please try again. |
| 277 | MSG132 | Dialog | UC-18 – Review Meeting Approval / Exception Flows | The meeting status has changed or no longer allows this action. |
| 278 | MSG133 | Dialog | Meeting workflow – Return to Draft / Exception Flows | The meeting status has changed or no longer allows this action. |
| 279 | MSG134 | Form error | Meeting workflow – Transfer Organizer / Exception Flows | The new organizer is not valid or can no longer create meetings. |
| 280 | MSG135 | Dialog | Meeting workflow – Transfer Organizer / Exception Flows | The meeting status has changed or no longer allows this action. |
| 281 | MSG136 | Toast | UC-15 / EX-2; Meeting workflow – Readiness and status history / Exception Flows | Unable to load the meeting workflow data. Please try again. |
| 282 | MSG137 | Form error | UC-19 – Publish Meeting / Exception Flows | The meeting needs exactly one active Chair and at least one active Secretary before it can be published. |
| 283 | MSG275 | Dialog | UC-30 – Manage Meeting Participants / Exception Flows | The participant list has changed. Please reload and try again. |
| 284 | MSG276 | Form error | UC-30 – Manage Meeting Participants / Validation (API) | The query or If-Match header is not valid. Please reload the data. |
| 285 | MSG278 | Form error | UC-33 – Record Attendance / Validation (API) | The attendance paging parameters are not valid. |
| 286 | MSG280 | Dialog | UC-43 – Join Meeting Room / Exception Flows | This meeting is held in person, so it has no online meeting room. |
| 287 | MSG281 | Dialog | UC-43 – Join Meeting Room / Exception Flows | The online meeting room is not ready. Please contact the administrator. |
| 288 | MSG282 | Toast | UC-47 – Moderate Meeting Room / Exception Flows | This person is no longer in the room or has no microphone on. |
| 289 | MSG283 | Toast | UC-43 – Join Meeting Room / Exception Flows | Unable to load the meeting room status. Please try again. |
| 290 | MSG284 | Toast | UC-47 – Moderate Meeting Room / Exception Flows | Unable to mute the microphone. Please try again. |
| 291 | MSG287 | Toast | Common – Access update (front-end event: access rights changed) | Your access rights have just been updated. |
| 292 | MSG346 | Form error | UC-39 / EX-4 | The system’s document storage is full. No documents were uploaded. Please contact an administrator. |
| 293 | MSG347 | Form error | UC-40b / EX-3 (document name invalid: blank, extension only, control characters, exceeds 255 chars, or changes extension) | The document name is not valid. It must not be blank and must keep the .pdf or .docx extension of the file. |
| 294 | MSG348 | Form error | UC-41a / EX-5; UC-41b / EX-4 (folder name exceeds 400 characters); UC-38 / EX-6 (comment exceeds 4,000 characters) | The text is longer than allowed. |
| 295 | MSG349 | Form error | UC-36a / EX-10; UC-36b / EX-8; UC-36c / EX-6; UC-36d / EX-8; UC-38 / EX-7 (meeting is cancelled, BR-028) | This meeting was cancelled and can no longer be modified. |
| 296 | MSG122 | Dialog | Retired; formerly UC-46 (View Meeting Room Participants) / Exception Flows | Retired: the Participants panel reads the people connected straight from the room, so nothing has to be loaded or retried. This code must not be reused. |
| 297 | MSG328 | Dialog | UC-44 – Leave Meeting Room / Exception Flows (EX-5) | You have left the meeting room. |
| 298 | MSG329 | Dialog | UC-44 – Leave Meeting Room / Exception Flows (EX-2) | You joined this room from another tab or device, so this tab left the room. |
| 299 | MSG330 | Dialog | UC-44 – Leave Meeting Room / Exception Flows (EX-3) | You are no longer on the participant list of this meeting room. |
| 300 | MSG331 | Dialog | UC-43 – Join Meeting Room / Exception Flows (EX-1); UC-44 – Leave Meeting Room / Exception Flows (EX-1) | The meeting has ended, the meeting room is closed. |
| 301 | MSG332 | Dialog | UC-44 – Leave Meeting Room / Exception Flows (EX-4) | Connection to the meeting room lost. |
| 302 | MSG333 | Banner | UC-44 – Leave Meeting Room / Exception Flows (EX-4); UC-46 – View Meeting Room Participants / Exception Flows (EX-1); UC-48 – Use In-room Chat / Exception Flows (EX-2) | Connection lost, reconnecting… |
| 303 | MSG334 | Inline / Form error | UC-45a – Manage Microphone / Exception Flows (EX-1); UC-45b – Manage Camera / Exception Flows (EX-1) | The browser is blocking the {device} (“microphone and camera” when both are blocked; the lobby shows the message once). Click the lock icon beside the address bar, choose Allow and reload the page. You can still join the room to listen — this sentence is omitted when only the camera is blocked. |
| 304 | MSG335 | Inline / Form error | UC-45a, UC-45b, UC-45d / Exception Flows (device not found) | No {device} was found on this computer. |
| 305 | MSG336 | Inline / Form error | UC-45a, UC-45b, UC-45d / Exception Flows (device in use) | The {device} is being used by another application (Zoom, Teams…). Close that application and try again. |
| 306 | MSG337 | Inline / Form error | UC-45d – Choose Audio and Video Devices / Exception Flows (EX-3) | Unable to open the {device}. Check the permission at the lock icon beside the address bar. You can still join the room to listen — this sentence is omitted when only the camera is blocked. |
| 307 | MSG338 | Banner | UC-43 – Join Meeting Room / Alternative Flows (AF-4) | The browser is blocking audio playback. |
| 308 | MSG339 | Toast | UC-47 – Moderate Meeting Room / Normal Flow | The Chair has muted your microphone. You can turn it back on when you need to speak. |
| 309 | MSG340 | Toast | UC-45c – Manage Screen Sharing / Alternative Flows (AF-1) | {name} started presenting, so your screen share was stopped. |
| 310 | MSG341 | Toast | UC-45c – Manage Screen Sharing / Exception Flows (EX-1) | Unable to share your screen. Please try again. |
| 311 | MSG342 | Toast | UC-47 – Moderate Meeting Room / Normal Flow | Muted the microphone of {name}. |
| 312 | MSG343 | Inline / Form error | UC-48 – Use In-room Chat / Exception Flows (EX-1) | Unable to send the message. Please try again. |
| 313 | MSG350 | Dialog | UC-43 – Join Meeting Room / Exception Flows (EX-6) | The meeting was cancelled, so the meeting room does not open. |
| 314 | MSG345 | Form error | UC-22 / EX-4 | Cancellation reason must not exceed 1000 characters. |
| 315 | MSG290 | Toast / Inline success | UC-52c / Normal Flow | Meeting recording saved. |
| 316 | MSG291 | Form error | UC-52a / EX-1, UC-52c / EX-1 | Invalid recording information. |
| 317 | MSG292 | Form error | UC-52c / EX-2, UC-56c / EX-2, UC-57b / EX-5 | Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG. |
| 318 | MSG293 | Form error | UC-52c (the audio file is larger than 300 MB) | Recording exceeds 300 MB. |
| 319 | MSG294 | Dialog / Banner | UC-52c / EX-3 | Recordings may only be saved while the meeting is in progress or has ended. |
| 320 | MSG295 | Inline error | UC-52a / EX-2, UC-52c / EX-2, UC-52d / EX-2, UC-52e / EX-2, UC-54a / EX-2, UC-55a / EX-2, UC-55b / EX-2 | Only the meeting Secretary may manage recordings. |
| 321 | MSG296 | Toast / Inline success | UC-52e / Normal Flow | Meeting recording erased. |
| 322 | MSG297 | Inline error | UC-51a / EX-1, UC-51b / EX-1 | Unable to load meeting recordings. |
| 323 | MSG298 | Inline error | UC-52a, UC-52c, UC-52d, UC-52e (the recording change cannot be saved) | Unable to save recording changes. Please try again. |
| 324 | MSG299 | Inline error | UC-51a / EX-2, UC-54a / EX-1 | Unable to load transcript. |
| 325 | MSG300 | Toast / Inline success | UC-54a / AF-2 | Recording queued for transcription. |
| 326 | MSG301 | Dialog | UC-54a / EX-1 | The recording is currently queued or being transcribed and cannot be rerun yet. |
| 327 | MSG302 | Toast / Inline success | UC-55a / Normal Flow | Transcript segment saved. |
| 328 | MSG303 | Form error | UC-55a / EX-1 | Transcript text is invalid. |
| 329 | MSG304 | Inline error | UC-53, UC-55a, UC-55b, UC-55d (the transcript change cannot be saved) | Unable to save transcript changes. Please try again. |
| 330 | MSG305 | Toast / Inline success | UC-56b / Normal Flow | Voice identification enabled for your account. |
| 331 | MSG306 | Toast / Inline success | UC-56f / Normal Flow | Voice identification disabled and all voice samples deleted. |
| 332 | MSG307 | Toast / Inline success | UC-56c / Normal Flow | Voice sample saved. |
| 333 | MSG308 | Inline error | UC-56c / EX-1 | Voice identification consent is required before recording a voice sample. |
| 334 | MSG309 | Inline error | UC-56c / EX-3, UC-57b / EX-6 | Voice sample did not meet quality requirements. Please read the full sample text clearly. |
| 335 | MSG310 | Inline error | UC-56c / EX-4 | Maximum of 5 voice samples reached. Delete an existing sample before recording a new one. |
| 336 | MSG311 | Toast / Inline success | UC-56e / Normal Flow | Voice sample deleted. |
| 337 | MSG312 | Inline error | UC-45a / EX-4, UC-56c / EX-5, UC-57b / EX-7 | Speech recognition service is currently unavailable. Please try again later. |
| 338 | MSG313 | Inline error | UC-56b / EX-1, UC-56c / EX-6 | Invalid submission data. |
| 339 | MSG314 | Inline error | UC-56a / EX-1 | Unable to load voice profile information. |
| 340 | MSG315 | Inline error | UC-56b / EX-2, UC-56c / EX-7, UC-56e / EX-1, UC-56f / EX-1, UC-57d / EX-2 | Unable to save changes. Please try again. |
| 341 | MSG316 | Toast / Inline success | UC-55b / Normal Flow | Speaker assigned. |
| 342 | MSG317 | Inline error | UC-55b / EX-1 (person not on the roster, Guest without a current invitation, or both userId and guestId sent) | Assigned person must be on the current meeting roster. |
| 343 | MSG318 | Toast / Inline success | System-level, not shown in a Use Case (UC-45a / AF-2 saves the sentence) | Spoken sentence recorded. |
| 344 | MSG319 | Inline error | System-level, not shown in a Use Case (UC-45a / AF-2) | Spoken sentence audio is invalid. |
| 345 | MSG320 | Dialog / Banner | UC-45a / EX-5 | In-person meetings do not record per-mic speech, or the meeting is no longer running. |
| 346 | MSG321 | Inline error | System-level, not shown in a Use Case (UC-45a / AF-2) | A spoken sentence cannot exceed 60 seconds. |
| 347 | MSG322 | Toast / Inline success | UC-55d / Normal Flow, UC-53 / Normal Flow | Spoken sentence deleted. |
| 348 | MSG323 | Inline error | UC-55d / EX-1, UC-53 / EX-1 | Only the speaker or the Secretary may delete this sentence. |
| 349 | MSG324 | Retired | Retired — the recording method follows the Meeting format (ADR-0040) | Retired: nobody chooses the recording method any more; it follows the Meeting format. |
| 350 | MSG325 | Retired | Retired — the recording method follows the Meeting format (ADR-0040) | Retired: nobody chooses the recording method any more; it follows the Meeting format. |
| 351 | MSG326 | Retired | Retired — the recording method follows the Meeting format (ADR-0040) | Retired: nobody chooses the recording method any more; it follows the Meeting format. |
| 352 | MSG327 | Retired | Retired — the recording method follows the Meeting format (ADR-0040) | Retired: nobody chooses the recording method any more; it follows the Meeting format. |
| 353 | MSG344 | Retired | Retired — the online room no longer records the whole call (ADR-0040) | Retired: the online room records each person’s own microphone instead of the whole call. |
| 354 | MSG351 | Toast / Inline success | UC-55c / Normal Flow | Sentence excluded from meeting record. |
| 355 | MSG352 | Toast / Inline success | UC-55c / AF-1 | Sentence restored to meeting record. |
| 356 | MSG353 | Inline error | UC-55c / EX-1 | Only the Chair or Secretary may exclude a sentence from the meeting record. |
| 357 | MSG354 | Inline error | UC-55c / EX-2 | Invalid sentence exclusion reason. |
| 358 | MSG355 | Inline error | UC-51c / EX-1, UC-54b / EX-1 | Only the Chair or Secretary may download meeting recordings and transcripts. |
| 359 | MSG356 | Inline error | UC-51c / EX-2, UC-54b / EX-2 | Recordings and transcripts of Secret meetings cannot be downloaded. |
| 360 | MSG357 | Inline error | UC-54b / EX-3 | Transcript is not ready for download. |
| 361 | MSG358 | Dialog / Banner | UC-52a, UC-52c (the Meeting is Online, PRE-2 not met) | Online meetings record per-mic speech; room recordings are not available. |
| 362 | MSG359 | Dialog / Banner | UC-52a / EX-3 | Room microphone can only be turned on while the meeting is in progress. |
| 363 | MSG360 | Dialog / Banner | UC-52a / EX-4 | Room microphone is currently active on another device. |
| 364 | MSG361 | Inline error | UC-52a (the recorded audio cannot be read) | The recorded audio is not valid. |
| 365 | MSG362 | Inline error | UC-52a / EX-5 | This recording take has been closed and cannot accept additional audio. |
| 366 | MSG363 | Toast / Inline success | UC-52a / Normal Flow | Room recording take saved. |
| 367 | MSG364 | Dialog / Banner | UC-52e / EX-1 | Room microphone is currently recording. Turn off the microphone before erasing. |
| 368 | MSG365 | Toast / Inline notice | UC-52a / AF-5 | Microphone detected no speech; take was not saved. |
| 369 | MSG366 | Toast / Inline success | UC-52d / Normal Flow | Recording take deleted. |
| 370 | MSG367 | Dialog / Banner | UC-52d / EX-1 | Recording contains only one take. Erase the entire recording to remove it. |
| 371 | MSG371 | Page / Panel message | UC-04 / EX-1 | The guest invitation is no longer valid. |
| 372 | MSG372 | Inline / Form error | UC-04 / EX-4 | Too many incorrect codes. Request a new code. |
| 373 | MSG373 | Inline / Form error | UC-04 / EX-5 | Wait before requesting another code. |
| 374 | MSG374 | Page / Panel message | UC-04 / EX-3 | Verification could not be completed. |
| 375 | MSG375 | Toast | UC-04 / AF-2 | A verification code was sent to the invited email. |
| 376 | MSG376 | Toast | UC-04 / Normal Flow | Display information saved. |
| 377 | MSG377 | Field error / Form error | UC-04 / EX-7 | Invalid display information. |
| 378 | MSG378 | Page / Panel message | UC-04 / EX-8 | The guest session has ended. |
| 379 | MSG379 | Inline / Form error | UC-04 / EX-6 | Too many codes requested. Try again later or contact the Meeting Secretary. |
| 380 | MSG380 | Inline / Form error | UC-43 / EX-9 | Confirm that you have read the recording notice before joining the meeting room. |
| 381 | MSG381 | Field error / Form error | UC-04 / EX-9 | The display name was already declared. |
| 382 | MSG382 | Page / Panel message | UC-35b / EX-5; UC-37a / EX-4 | The file of this document is no longer in storage and cannot be opened. Please contact the administrator. |
| 383 | MSG383 | Page / Panel message | UC-35b / EX-6 | The browser cannot display this file. It may be damaged or in an unsupported format. |
| 384 | MSG384 | Toast | UC-58 / Step 4 | The request was sent. The draft appears when the AI has written it. |
| 385 | MSG385 | Form error | UC-58 / EX-1; UC-60 / EX-3; UC-61 / EX-2 | Only the Chair or Secretary may create, edit and review the draft. |
| 386 | MSG386 | Form error | UC-58 / EX-2 | A draft can be created only after the meeting has ended. |
| 387 | MSG387 | Form error | UC-58 / EX-3 | The AI is writing the draft, please wait. |
| 388 | MSG388 | Toast / Inline success | UC-60 / Step 6 | Draft saved. |
| 389 | MSG389 | Field error / Form error | UC-60 / EX-1 | The draft content is invalid. |
| 390 | MSG390 | Form error | UC-60 / EX-2; UC-61 / EX-1 | There is no draft to correct or review. |
| 391 | MSG391 | Toast / Inline success | UC-61 / Step 4 | Draft approved. |
| 392 | MSG392 | Page / Panel message | UC-58 / EX-7; UC-59 / EX-1 | Unable to load the draft. |
| 393 | MSG393 | Form error | UC-58 / EX-5; UC-60 / EX-4; UC-61 / EX-3 | Unable to save the draft. Please try again. |
| 394 | MSG394 | Form error | UC-58 / EX-4 | The AI server is not ready. Contact the system administrator. |
| 395 | MSG395 | Toast / Inline success | UC-70 / Step 5 | Minutes draft created. |
| 396 | MSG396 | Toast / Inline success | UC-71 / Step 5 | Minutes saved. |
| 397 | MSG397 | Form error | UC-70 / EX-1; UC-71 / EX-4 | Only the Chair or Secretary may write the minutes. |
| 398 | MSG398 | Form error | UC-70 / EX-2 | The minutes can be written only after the meeting has ended. |
| 399 | MSG399 | Form error | UC-70 / EX-3 | This meeting already has a minutes draft. |
| 400 | MSG400 | Form error | UC-70 / EX-4; UC-71 / EX-3 | The minutes have been submitted or issued and can no longer be changed. |
| 401 | MSG401 | Form error | UC-71 / EX-1 | The minutes were just changed by someone else. Reload to see the latest version. |
| 402 | MSG402 | Field error / Form error | UC-71 / EX-2 | The minutes content is invalid. |
| 403 | MSG403 | Page / Panel message | UC-69 / EX-1 | Unable to load the minutes. |
| 404 | MSG404 | Form error | UC-70 / EX-5; UC-71 / EX-5 | Unable to save the minutes. Please try again. |
| 405 | MSG405 | Form error | UC-71 / EX-6 | The minutes of a Secret meeting cannot be downloaded. |
| 406 | MSG406 | Form error | UC-71 / EX-8 | Download the minutes as a Word (docx) or PDF file only. |
| 407 | MSG407 | Toast | UC-55b / AF-2 | The speaker of this sentence has been changed. |
| 424 | MSG424 | Toast | UC-72 / Normal Flow | Minutes submitted to the Chair for approval. |
| 425 | MSG425 | Form error | UC-72 / EX-1 | Only the Secretary may submit the minutes for approval. |
| 426 | MSG426 | Form error | UC-72 / EX-2 | The minutes can be submitted only after the meeting has ended. |
| 427 | MSG427 | Toast | UC-73 / Normal Flow | Minutes approved and issued. |
| 428 | MSG428 | Form error | UC-73 / EX-1; UC-74 / EX-1 | Only the Chair may approve or return the minutes. |
| 429 | MSG429 | Form error | UC-73 / EX-2; UC-74 / EX-2 | These minutes are not waiting for approval. |
| 430 | MSG430 | Toast | UC-74 / Normal Flow | Minutes returned to the Secretary. |
| 431 | MSG431 | Field error / Form error | UC-74 / EX-3 | Enter a reason for returning the minutes, up to 2,000 characters. |
| 432 | MSG432 | Toast | UC-75 / Normal Flow | Correction draft created. |
| 433 | MSG433 | Form error | UC-75 / EX-2 | Only issued minutes can be corrected. |
| 434 | MSG434 | Form error | UC-75 / EX-3 | A correction of these minutes is already in progress. |
| 456 | MSG456 | Form error | UC-70 / EX-7 | A correction cannot be rebuilt from the template. Correct it by hand. |
| 457 | MSG457 | Inline error | UC-50 / EX-3 | Only the current Chair may run the speaking turns. |
| 458 | MSG458 | Inline error | UC-50 / EX-4 | You must be in the meeting room to register to speak. |
| 459 | MSG459 | Inline error | UC-50 / EX-5 | Someone is already invited or speaking. Close that turn first. |
| 460 | MSG460 | Inline error | UC-50 / EX-6 | This speaking request has changed. Reload the list. |
| 461 | MSG461 | Inline error | UC-50 (the speaking queue cannot be loaded) | Unable to load the speaking queue. Please try again. |
| 462 | MSG462 | Inline error | UC-50 (the speaking turn change cannot be saved) | Unable to save the speaking turn change. Please try again. |
| 435 | MSG435 | Toast | UC-80a / Normal Flow | Calendar share request sent. |
| 436 | MSG436 | Toast | UC-80c / Normal Flow | Calendar share accepted. |
| 437 | MSG437 | Toast | UC-80c / Normal Flow | Calendar share request declined. |
| 438 | MSG438 | Toast | UC-80e / Normal Flow | Calendar share request withdrawn. |
| 439 | MSG439 | List message | UC-80f, UC-81c / Normal Flow | This calendar share was stopped because the viewer is no longer a Member or an account was deactivated. Ask for a new share. |
| 440 | MSG440 | Retired | Retired; formerly UC-80 (share list), UC-81c / Normal Flow | Retired on 09/11/2026 (PO 09/11/2026 Q-B): a share past its end date is no longer listed, so no message is shown. This code must not be reused. |
| 441 | MSG441 | Toast | UC-80b / Normal Flow | Calendar shared. Waiting for acceptance. |
| 442 | MSG442 | Toast | UC-80c / AF-1, UC-80d / AF-1 | Calendar share updated. |
| 443 | MSG443 | Form error | UC-80a / EX-3, UC-80b / EX-3, UC-80c / EX-3, UC-80d / EX-3 | The date range is invalid. |
| 444 | MSG444 | Form error | UC-81a / EX-1 | The meeting-time search is invalid. Check the highlighted fields. |
| 445 | MSG445 | Information | UC-81a / AF-1 | No time slot fits everyone. Showing the slots with the most people free. |
| 446 | MSG446 | Toast | UC-81b / EX-1 | This time slot is no longer free. Please choose another slot. |
| 447 | MSG447 | Toast | UC-80a / EX-5, UC-80b / EX-4, UC-80c / EX-4, UC-80d / EX-4, UC-80e / EX-3 | Unable to save the calendar sharing data. Please try again. |
| 448 | MSG448 | Form error | UC-80a / EX-1, UC-81a / EX-2, UC-81b / EX-2 | Only a Member can do this. |
| 449 | MSG449 | Form error | UC-80a / EX-2, UC-80b / EX-2 | There is already a pending or effective share with this person. |
| 450 | MSG450 | Form error | UC-80c / EX-2, UC-80c / AF-1, UC-80d / EX-2, UC-80e / EX-2 | This action is not allowed in the current state of the share. |
| 451 | MSG451 | Toast | UC-80e / AF-1 | Calendar sharing ended. |
| 452 | MSG452 | Toast | UC-80c / AF-1 | Calendar share change declined. |
| 453 | MSG453 | Page | UC-80f / EX-1, UC-81a / EX-3, UC-81b / EX-3, UC-81c / EX-1 | Unable to load the calendar sharing data. Please try again. |
| 454 | MSG454 | Toast | UC-80d / Normal Flow | Calendar share change request sent. |
| 455 | MSG455 | Form error | UC-80b / EX-1 | This person cannot receive a calendar share. |
| 463 | MSG463 | Dành sẵn, chưa triển khai | UC-79a / EX-1, UC-79b / EX-1 | The report period is invalid. Choose up to 24 months, with the start month not after the end month. |
| 464 | MSG464 | Dành sẵn, chưa triển khai | UC-79a / EX-2 | Unable to load the report. Please try again. |
| 465 | MSG465 | Dành sẵn, chưa triển khai | UC-79b / EX-2 | Unable to export the report. Please try again. |
| 466 | MSG466 | Table message | UC-79a / AF-2 | There are no meetings you can view in this period. |
| 467 | MSG467 | Form error | UC-80a / EX-6, UC-80b / EX-6 | Unable to load the candidate list. |
| 468 | MSG468 | Toast | UC-80c / EX-5 | The sender's account has been deactivated. You cannot accept this share. |
| 469 | MSG469 | Dành sẵn, chưa triển khai | UC-12 / EX-1 | The audit log filter is invalid. Check the period and the selected values. |
| 470 | MSG470 | Dành sẵn, chưa triển khai | UC-12 / EX-2 | Unable to load the audit log. Please try again. |
| 471 | MSG473 | Toast / Inline success | UC-57b / Normal Flow | Voice sample recorded for the attendee. |
| 472 | MSG474 | Toast / Inline success | UC-57d / Normal Flow | Voice sample deleted. |
| 473 | MSG475 | Inline error | UC-57b / EX-2 | This person has not agreed to Voice ID under the current terms. They can enable it on the My voice page. |
| 474 | MSG476 | Inline error | UC-57b / EX-3 | This person already has 50 collected voice samples. Delete a collected sample before recording a new one. |
| 475 | MSG477 | Inline error | UC-57b / EX-4 | Retired: the meeting-path collection was removed (ADR-0075). Do not reuse this code. |
| 491 | MSG483 | Inline error | UC-57b / EX-4 | Confirm the person agreed to this recording. |
| 477 | MSG478 | Dialog / Banner | UC-52c / EX-8 | Hybrid Meetings are recorded live with the room microphone to match the timing of remote speech; audio files cannot be uploaded. |
| 478 | MSG479 | Dialog | UC-54a / EX-3 | The combined recording is for listening only. Its speech has already been transcribed from the room and per-microphone recordings. |
| 479 | MSG414 | API error (429 + Retry-After) | UC-01 – Sign in with Email and Password / UC-02 – Sign in with Google – rate limiting | Too many attempts. Please try again in a few minutes. |
| 480 | MSG480 | Inline error (lobby) | UC-43 – Join Meeting Room / Exception Flows (EX-11) | Could not connect to the meeting room. Please try again. |
| 481 | MSG415 | Success (200) | UC-28 Normal Flow | Note saved. |
| 482 | MSG416 | Error (403) | UC-28 EX-1 | Only the Secretary may write meeting notes. |
| 483 | MSG417 | Error (409) | UC-28 EX-2 | Notes can be written only while the meeting is in progress, or after it has ended until the minutes draft is created. |
| 484 | MSG418 | Error (409) | UC-28 EX-3 | This note was changed in another window. Reload to see the latest version. |
| 485 | MSG419 | Error (400) | UC-28 EX-4 | The note is too long. A note can hold up to 20,000 characters. |
| 486 | MSG420 | Error (409) | UC-28 EX-5 | The selected agenda item is no longer part of this meeting. |
| 487 | MSG421 | Error (500) | UC-28 EX-6 | Unable to load the meeting notes. |
| 488 | MSG422 | Error (503) | UC-28 EX-7 | Unable to save the note. It is kept on this device and will be sent again. |
| 489 | MSG423 | FE (client) | UC-28 AF-3 | You are offline. The note is kept on this device and will be saved when the connection returns. |
| 490 | MSG481 | Form error | UC-58 / EX-9 | The meeting has no spoken sentences for the AI to summarise. |
| 491 | MSG482 | Form error | UC-51c / EX-3 | The recording could not be converted to MP3. Please try again later. |
| 492 | MSG484 | Form error | UC-79a / EX-3, UC-79b / EX-3 | The report parameters are invalid. Choose the data, grouping or filter again. |

### 3. Other Requirements

…

²⁰ Only the personal library of a deactivated account, read-only without modifying anything, subject to audit logging and excluding documents and folders of Secret Meetings (UC-37a, BR-085, BR-009, BR-010, BR-011).
