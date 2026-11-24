# Report 1 – Project Introduction

> **GENERATED — DO NOT EDIT.** Snapshot từ DOCX local. Sửa [`source/Report_1_Project_Introduction.docx`](source/Report_1_Project_Introduction.docx) rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report 1`. Sửa tay file này sẽ mất ở lần sync sau. Chi tiết: [08-reports/README.md](README.md).

CAPSTONE PROJECT REPORT

Report 1 – Project Introduction

– Hanoi, Sep 2026 –

Table of Contents

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 18/09/2026 | A | KhanhPG | Add Overview, Product Background, Existing Systems |
| 19/09/2026 | A, M | LocHD | Add Project Scope & Limitations; align Project Purpose, Existing Solutions summary and Proposed Solution with the project scope |
| 27/09/2026 | M | KhanhPG | Update the Fellow, Project Purpose and Product Background sections. |
| 27/09/2026 | M, D | LocHD | Remove FE-12 Task & Action Item Lifecycle Management and renumber FE-13 to FE-15 as FE-12 to FE-14 |
| 27/09/2026 | M | LocHD | Update stakeholder roles: Tran Viet Hai becomes Team Leader and Frontend Lead; Pham Gia Khanh becomes Business Analyst |
| 05/10/2026 | M, D | KhanhPG | Remove Department & Team Management; split Meeting & Schedule Management into FE-04 Meeting, FE-05 Meeting Session, FE-06 Agenda and FE-07 Participant Management; renumber the rest to FE-08–FE-16. |
| 05/10/2026 | M | KhanhPG | Sync the PO-confirmed Master UC baseline: personal document library, two-source meeting documents, Secretary uploads meeting audio; attendance, summaries, comments, audit log and minutes-revision scope. |
| 08/10/2026 | M, D | KhanhPG | Remove FE-04; consolidate Meeting and former Meeting Session scope in FE-05; Agenda, Vote and Resolution attach to Meeting. |
| 09/10/2026 | M, D | KhanhPG | Renumber active features FE-01–FE-15; in-meeting voting with Chair conclusions (no time-box, quorum or separate Resolution scope); audio safeguards kept; schedule and effort preserved. |
| 13/10/2026 | M | HaiTV | FE-07: no document versions, one immutable file, Chair/Secretary manage meeting documents, library folders, browser previews (ADR-0018). FE-11: Chair-selected voters, Guests vote, ending a Meeting needs no closed Votes (ADR-0016/17). |
| 03/11/2026 | M, D | KhanhPG | Remove stakeholder emails and phone numbers from the public report; keep names and roles only. |
| 15/11/2026 | M, D | KhanhPG | SCRUM-207 (note 51): drop FE-14 Personal Dashboard & Meeting Workspace and account unlock; sign-in keeps per-minute attempt limits only. |
| 24/11/2026 | M, A | LocHD | Supervisor review: align with the built system: minutes are approved, not signed (FE-12); AI environment decided (ADR-0056); in-meeting recording, Voice ID, notifications and reports as built; add FE-16 (ADR-0058). |

*A - Added M - Modified D - Deleted

## Definition and Acronyms

| Acronym | Definition |
| --- | --- |
| IMDMS | TrustCabinet — Internal Meeting and Document Management System for TrustSoft Company |
| FE | Feature |
| LI | Limitation |
| EX | Exclusion |

## I. Project Overview

### 1. Project Information

- English name: TrustCabinet — Internal Meeting and Document Management System for TrustSoft Company

- Vietnamese name: TrustCabinet – Hệ thống quản lý cuộc họp và tài liệu nội bộ cho Công ty TrustSoft

- Project code: IMDMS

- Group name: SEP490-G116

- Software type: Web App

### 2. Project Purpose

The purpose of this project is to develop TrustCabinet, a centralized Internal Meeting and Document Management System for TrustSoft. At present, meeting-related documents are scattered across many separate applications such as chat tools, email and shared drives; TrustCabinet brings agendas, supporting files, recordings and minutes into a single platform, so employees no longer have to search through multiple tools to find the information they need. By handling document preparation and the approval of meetings and minutes digitally, the system also removes the reliance on paper copies for meeting preparation and record keeping. Because the platform is hosted internally and protected by user and role management, access control and audit logging, TrustSoft retains full control over its data and ensures that confidential internal information is accessible only to authorized people. In addition, TrustCabinet gives the company better control over its meeting schedule: every Meeting follows a clear scheduling, approval and publication process, which lets organizers easily arrange, organize and schedule meetings, coordinate participants and guests, and prepare agendas and documents in advance. Finally, the system provides tools that support the compilation of meeting minutes, including in-meeting recording or audio upload, speaker-attributed transcription with Voice ID and AI draft generation, which the Chair and the Secretary review, edit and consolidate into formal minutes.

### 3. Project Stakeholders

| Full Name | Role |
| --- | --- |
| Nguyen Quang Hung | Supervisor |
| Dinh Van Toi | Product Owner (TrustSoft) |
| Tran Viet Hai | Team Leader, Frontend Lead |
| Pham Gia Khanh | Business Analyst, UI/UX |
| Nguyen Quang Huy | Member (Backend, Frontend) |
| Hoang Duc Loc | Backend Lead, Architecture |
| Nguyen Truong Huy | Test Lead, Quality Assurance |

## II. Product Background

TrustSoft is a Hanoi-based technology company where project teams run three to five internal meetings per week, including sprint planning, client syncs, and internal reviews. These meetings are currently coordinated through a patchwork of tools: Google Calendar for scheduling, Zalo or Teams for sharing meeting links and updates, and Google Docs, paper notes, or personal digital notes for recording discussion outcomes.

Meeting-related documents are also handled separately from the meeting process. Agendas, specifications, reports, and other supporting files may be shared through chat messages, document links, or shared folders before a meeting. As a result, participants may need to search across several conversations or folders to find the latest version of a document, confirm what was reviewed, or locate materials from an earlier meeting. There is no single place where the meeting, its supporting documents, discussion records, and subsequent results can be accessed together.

A typical project lead at TrustSoft spends considerable time before each meeting reviewing previous documents, notes, and chat history to recall what was agreed, what remains unresolved, and what needs to be discussed again. During the meeting itself, whoever is designated to take notes participates less actively in the discussion, while important details—such as decisions, responsibilities, and deadlines—depend on that person capturing everything correctly in real time.

Decision-making is similarly fragmented. When the team needs to vote on matters such as approving a technical approach or prioritizing a backlog item, voting is often handled informally through verbal responses or chat messages such as “yes” or “agree.” The result is not maintained as a structured voting record linked to the meeting or the issue being decided. If the decision is questioned later, the team may have to search through chat history or meeting notes to determine who participated, what options were considered, and what conclusion was reached.

The same problem continues after the meeting. Meeting minutes, when prepared, are usually created manually from notes and shared separately through a document link. This makes it difficult to maintain a consistent record that brings together meeting information, discussion outcomes, voting results, decisions, and assigned actions. For client-facing meetings, verbal commitments may also be missed or recorded incompletely, becoming visible only when the client follows up and the team has no reliable meeting record to refer to.

Meeting notes may reach the team one or two days later and are often presented as long, unstructured text. Action items discussed verbally may never be transferred into the team's task-tracking process unless someone does so manually, while unresolved items can reappear in later meetings because their status was not followed up consistently. Documents, notes, voting results, and meeting outcomes therefore remain distributed across different tools rather than forming a connected meeting record.

For a growing technology company managing multiple concurrent client projects, this fragmentation creates more than administrative overhead. Team members spend time searching for information, checking document versions, reconstructing previous decisions, and manually consolidating meeting results. More importantly, the gap between what is prepared before a meeting, what is decided during it, and what is recorded and followed up afterward creates a direct risk to project timelines, accountability, and client trust.

This gap has become more practical to address as speech recognition, automatic summarization, and task-extraction technologies have matured. Combined with centralized meeting documents, structured voting records, and consolidated meeting minutes, these technologies make it possible to connect what was prepared, discussed, decided, and assigned within a single meeting workflow without adding significant manual overhead for TrustSoft's project teams.

## III. Existing Solutions

### 1. Fellow

Fellow is an AI meeting assistant that supports meeting recording, transcription, and AI-generated meeting recaps. It can capture live meetings or process uploaded audio and video recordings, then generate transcripts, summaries, key decisions, and other meeting outcomes.

| Website / Link | https://fellow.ai |
| --- | --- |
| Target Users | Teams and organizations that need AI-assisted meeting capture, documentation, and follow-up |
| Core Features | Meeting recording; audio/video upload; transcription; AI-generated and editable summaries; key decision extraction; collaborative meeting notes; searchable meeting history |
| Strengths | Mature meeting capture workflow; supports both live recordings and uploaded recordings; AI-generated summaries and decisions can be reviewed and edited after the meeting |
| Weaknesses | Voting is documented mainly through reactions and informal pulse checks rather than a structured voting process with formal voting sessions and recorded voting outcomes |
| GAP for this project | Fellow focuses on AI meeting recaps and collaborative notes. TrustCabinet focuses on producing editable meeting minutes from the speaker-attributed transcript of the meeting recording, bringing together discussion content, decisions, and structured voting results into a single meeting record. |

### 2. Fireflies.ai

Fireflies.ai is an AI meeting assistant that automatically records, transcribes, and analyzes voice conversations across multiple video conferencing platforms.

| Website / Link | https://fireflies.ai |
| --- | --- |
| Target Users | Remote teams, sales professionals, and project managers |
| Core Features | Automated recording/transcription, AI summaries, sentiment analysis, conversation intelligence, integrations with CRM and task management tools |
| Strengths | Excellent multi-platform integration (Zoom, Teams, Google Meet), strong transcription accuracy, and conversational search |
| Weaknesses | Third-party cloud processing raises data privacy concerns; ongoing subscription costs; limited native document workflow management |
| GAP for this project | Fireflies.ai focuses primarily on meeting transcription and analytics rather than providing a complete, self-hosted, end-to-end document governance and voting system required by TrustSoft. |

### 3. Asana + Zoom + AI Studio

| Website / Link | https://asana.com / https://zoom.us |
| --- | --- |
| Target Users | Tech-savvy project teams combining disparate tools |
| Core Features | Video conferencing via Zoom, custom script processing via AI Studio, and project task tracking in Asana |
| Strengths | High flexibility, powerful standalone tools for each specific step of the process |
| Weaknesses | Requires manual effort to bridge data across platforms, fragmented user experience, high overhead cost for multiple SaaS subscriptions |
| GAP for this project | Combining separate platforms lacks a cohesive workflow, leading to data silos, extra manual work to sync information, and missing integrated security mechanisms like role-based access to meeting records and audit logging. |

### 4. ClickUp

ClickUp is an all-in-one productivity platform that integrates task management, document collaboration, and AI-powered meeting tools.

| Website / Link | https://clickup.com |
| --- | --- |
| Target Users | Cross-functional teams, project managers, and agile organizations |
| Core Features | ClickUp Brain features (meeting transcript analysis, automatic generation of follow-up tasks with assignees and deadlines, AI Notetaker, Action Item Extractor Agent), and Docs integration |
| Strengths | Unified environment for task management and documentation with strong AI automation capabilities |
| Weaknesses | High platform complexity, recurring subscription costs, and lack of specialized internal governance workflows |
| GAP for this project | ClickUp is a general-purpose cloud SaaS platform that lacks self-hosted enterprise deployment options, a formal approval workflow for meeting minutes, and specialized organizational meeting governance tailored specifically for TrustSoft. |

### 5. Section Summary

Existing meeting-intelligence and productivity products address capture, summaries, collaboration and task follow-up, while document-governance platforms emphasize approvals, access control and signatures. TrustCabinet combines meeting preparation and approval, a browser-based meeting room, meeting documents, in-meeting voting with recorded Chair conclusions, and reviewed post-meeting transcripts, summaries and approved minutes around one independent Meeting. Its scope ends at the formal meeting record rather than extending into task tracking.

## IV. Proposed Solution

TrustCabinet is an internally hosted meeting and document management platform for TrustSoft. A Member prepares a Meeting and submits it to an assigned Approver; publication follows approval and issues invitations. The Secretary prepares the agenda and maintains participants, attendance and authorized documents. The Chair starts and ends the Meeting, controls agenda execution, and opens Votes while the Meeting is InProgress. Each Vote belongs directly to the Meeting, does not require an Agenda Item and records a Chair conclusion after closing. The Secretary records the meeting room or uploads an audio file, and in the online room each person's microphone is recorded sentence by sentence once everyone has acknowledged the recording notice. The Secretary corrects the speaker-attributed transcript, and the Chair or the Secretary approves an AI draft of the meeting content. Meeting-level minutes combine those records with vote results and Chair conclusions; the Secretary sends them to the Chair, who approves and issues them or returns them for correction.

The system provides tailored benefits across all organizational roles at TrustSoft. Meeting Organizers and Chairs spend significantly less time manually compiling agendas, getting meetings approved, and chasing meeting information across chat threads. Participants can focus fully on active discussion without the burden of real-time note-taking and receive a reviewed AI-generated summary once the meeting audio has been processed, while Secretaries edit and approve a draft instead of writing minutes from scratch. Management gains complete transparency through centralized meeting archives, approved and issued minutes, and clear audit trails of key decisions.

A self-hosted web application gives TrustSoft control over the core meeting application, database, documents and meeting-room infrastructure. Google sign-in is optional alongside email/password sign-in, and outbound mail is sent through the Resend email service, with SMTP as an alternative. AI processing combines self-hosted and external services: speech recognition, speaker attribution and Voice ID always run on a self-hosted speech service, and the AI draft of the minutes runs on a self-hosted language model. An administrator may additionally send the drafting of Normal meetings to an external language-model API; Internal and Secret meetings always stay on the self-hosted model. Any external provider must process data only for the stated purpose and must not retain it or reuse it for other purposes.

The web interface supports browser-based participation and review without a separate native mobile application. Operational settings, access rules, audio consent, retention dates and recorded erasure are part of the deployment and security requirements.

## V. Project Scope & Limitations

For traceability, the gaps identified in Section III – Existing Solutions are consolidated and given the identifiers below. Every feature listed in Section V.1 is mapped to one or more of these gaps, and no feature is in scope unless it closes at least one of them.

| ID | Gap | Description |
| --- | --- | --- |
| GAP-01 | No self-hosted deployment option | Confidential meeting content — audio, transcripts, minutes and strategic decisions — is captured automatically (typically by a recording bot that joins the call), processed on third-party cloud infrastructure and locked behind recurring subscription fees. None of the reviewed products can be deployed inside the company's own infrastructure, and none leaves the decision to capture a conversation under explicit human control. (Fellow, Fireflies.ai, ClickUp, Asana + Zoom + AI Studio) |
| GAP-02 | Conferencing is an external dependency | The reviewed meeting-intelligence tools attach to a separate conferencing service rather than providing the meeting room themselves, so the conversation, its recording and its supporting documents are split across different vendors and account systems. (Fellow, Fireflies.ai, ClickUp, Asana + Zoom + AI Studio) |
| GAP-03 | Fragmented toolchain and data silos | Agenda, supporting documents, minutes and follow-up tasks are held in different platforms, which forces manual synchronisation, fragments the user experience and leaves no single source of truth for a given meeting. (Asana + Zoom + AI Studio, ClickUp, Fellow) |
| GAP-04 | Discussion outcomes are not turned into a structured record | What is said in a meeting depends on one note-taker capturing it in real time, reaches participants late as long unstructured text, and is not consolidated with the decisions and votes that followed, so meeting outcomes are hard to find, verify or report on afterwards. (Section III – Section Summary, Fellow) |
| GAP-05 | No formal meeting governance | In-meeting voting with recorded tallies and Chair conclusions, approved and issued meeting minutes and an audit trail of governance actions are needed to maintain formal meeting outcomes within the same meeting record. TrustCabinet does not create a separate Resolution object. |
| GAP-06 | Meeting intelligence and document governance never coexist | Vietnamese E-Office platforms provide document management, approval flows, role-based access and digital signatures but treat meetings only as scheduling; AI meeting platforms provide transcription and summarisation but lack organisation-specific access control, archiving and management reporting. (Section III – Section Summary, Fellow, ClickUp) |

### 1. Major Features

| ID | Feature Name | Description | Addressed Gaps |
| --- | --- | --- | --- |
| FE-01 | Authentication & Account Security | Internal users sign in with their email and password, or with a Google account that is already linked to an existing internal account; a Google sign-in never creates a new account. Accounts are protected by a password policy, session expiry and per-minute limits on sign-in attempts, and all sign-in activity is logged. External guests hold no system account: they can access only the specific meetings to which they have been invited, with limited meeting functions. | GAP-01 |
| FE-02 | User & Profile Management | Administrators create, update and deactivate accounts, assign each user a system role, and maintain the profile information used across meetings, minutes and reports. | GAP-06 |
| FE-03 | Role-Based Access Control | Every meeting, document, audio file and report is visible only to the users entitled to it, according to their system role (System Admin, Member, Approver) and the role they hold in a specific meeting (Chair, Secretary, Participant, Guest). Permission sets are configurable by the company without vendor involvement, access decisions are recorded in an audit log, and the System Admin can view, search and filter relevant business and security audit events. | GAP-01, GAP-05, GAP-06 |
| FE-04 | Meeting Management | A Member creates and updates an independent Meeting with planned start/end times, mode, location, confidentiality and description, submits it for approval and publishes it after approval. The assigned Approver approves or rejects with a recorded reason. A Meeting that will not take place is cancelled with a reason before it starts, and invitees are notified. Permitted scheduling changes are audited and affected participants are notified. The Chair explicitly starts a Published Meeting and ends an InProgress Meeting, even while a Vote is still Open; that Vote accepts no more ballots and the Chair can still close it. Planned and actual times and append-only lifecycle history are retained. Agenda Items, participants, documents, Votes and minutes belong directly to the Meeting. There is no nested Meeting Session lifecycle. | GAP-03 |
| FE-05 | Agenda Management | The assigned Secretary creates, updates, deletes and reorders Agenda Items directly under a Meeting while it is Draft, WaitingApproval, Rejected, Approved, Published or InProgress. Agenda management is prohibited once it has Ended. The Chair controls execution by setting the current Agenda Item and completing it; actual times and progress are recorded separately from planning permissions. | GAP-03, GAP-05 |
| FE-06 | Meeting Participant Management | The Member who creates a Meeting chooses its initial participants, including the Chair and the Secretary. After that, the assigned Secretary manages internal participants and external Guest invitations, meeting-specific roles, removals, invitation responses and official attendance; from publication onward only the current Chair hands the Chair role to another participant. Attendance remains distinct from invitation response and online-room presence; room presence may be used only as supporting evidence or pre-fill for Online/Hybrid meetings. | GAP-03, GAP-05 |
| FE-07 | Personal Document Library & Meeting Document Management | Each internal user has a personal document library where PDF and DOCX documents can be uploaded, organised into folders (as in AI-eCabinet), renamed, moved, reused and deleted. The Chair and the Secretary of a meeting can add a document to it either by uploading a file from their computer or by selecting an existing document from their personal library, and can remove it until the meeting ends. Documents are not versioned or copied: the library and the meeting each keep a reference to the same immutable file, so deleting a document from the library does not affect a meeting that uses it, and files no longer referenced are cleaned up automatically. Meeting documents are previewed in the browser without any server-side conversion (download is blocked for Secret meetings), searchable and kept with the meeting record. Meeting participants, including verified Guests, may comment on meeting documents. | GAP-03, GAP-06 |
| FE-08 | Online Meeting Room Management | Participants, including verified Guests, join a browser-based meeting room hosted by TrustCabinet itself, with audio, video, screen sharing, text chat and a floating call window, so the meeting is held on the company's own platform rather than a vendor's. Participants raise a hand to register to speak and the Chair invites them one at a time; the Chair may also mute another person's microphone. Recording in the room follows FE-09: no recording bot joins the call. | GAP-02 |
| FE-09 | Meeting Recording | During an In-person or Hybrid Meeting, the current Secretary records the meeting room through the browser in one or more takes; in an Online or Hybrid Meeting each person's microphone is recorded sentence by sentence, and after an Online or Hybrid Meeting ends the system combines those sentences into one recording. For an In-person Meeting the Secretary may instead upload an audio file recorded outside the system. Before anything is recorded, attendees are informed: the Secretary confirms that they were told and did not object and states the purpose, and in the online room everyone acknowledges the recording notice. Each person can review, replay and delete the sentences recorded from their own microphone. Authorized participants play the recordings and the Chair or Secretary downloads them as MP3. Recordings are erased after 180 days, or earlier by the Secretary, and every erasure is recorded. Video is not recorded. | GAP-01, GAP-02 |
| FE-10 | Transcription, Speaker Identification & Meeting Summary | Each saved recording is transcribed automatically and its speech is separated by speaker; sentences from personal microphones are transcribed within seconds while the Meeting is running. With a consent they can withdraw, users record their own voice samples (Voice ID), or a user holding the collection permission records them for attendees, so the system suggests or fills in the names of consenting speakers; voice embeddings are kept only in encrypted form. The Secretary corrects the transcript and the speaker attribution. The Chair or the Secretary asks the AI for a three-section draft of the meeting content from the transcript, the agenda and the vote results, edits it and approves it; once approved, everyone who may read the Meeting can read it. | GAP-04, GAP-06 |
| FE-11 | Voting Management | The current Chair or Secretary prepares Vote drafts before or during the Meeting, and only the Chair opens a Vote, one at a time, while the Meeting is InProgress. A Vote belongs directly to one Meeting and does not require an Agenda Item. For each Vote the Chair selects which meeting roles may vote (Chair, Secretary, Participant, verified Guest); each eligible person confirms reading every Vote document and may submit one ballot while the Meeting is InProgress; submitted ballots cannot be changed or withdrawn. Public and secret ballots are supported. After closing and tallying, the Chair records Passed, NotPassed or Deferred on that Vote. There is no time-boxed voting, deadline, quorum or separate Resolution object. Ending the Meeting does not require closing Votes: no ballot is accepted after it ends, and the Chair may still close an Open Vote and record its conclusion. Tallies and Chair conclusions are included in its minutes. | GAP-05 |
| FE-12 | Meeting Minutes & Approval | After the Meeting ends, the current Chair and the current Secretary build the minutes from one fixed template, the Meeting details, official attendance, agenda outcomes, the reviewed draft, vote tallies and Chair conclusions, and edit them. The Secretary sends the minutes to the Chair, who approves and issues them or returns them with a reason; issued minutes are read-only and are corrected only through a new revision that is approved again. The minutes are not signed. Each Meeting has its own formal minutes; no separate Resolution workflow is maintained. | GAP-05, GAP-06 |
| FE-13 | Email & In-App Notification | Meeting approval requests and results, invitations, role changes, rescheduling, cancellations, removals, pre-meeting reminders, the start of a Meeting, added documents, agenda presenter assignments, voting calls, minutes waiting for approval, returned or issued, calendar-sharing requests and voice samples collected for a user are delivered as in-application notifications in real time, each linking directly to the item that requires action. Invitations and changes to them are also sent by email, and Guests receive their invitation link and verification code by email. | GAP-03, GAP-04 |
| FE-15 | Reporting & Statistics | Every signed-in internal user views aggregated statistics on the meetings they may read over a chosen period: meetings per month, by status and by sensitivity, the average attendance rate, the average meeting duration and the average time from the end of a meeting to the issue of its minutes. Each table can be exported to Excel. No separate Resolution statistics or task-completion metrics are included. | GAP-06, GAP-04 |
| FE-16 | Working Calendar & Meeting Time Search | Each internal user sees their meetings on a working calendar. A user who may create meetings asks others to share their calendar, or is given a share directly; a share shows only busy or free time, never meeting details, and Secret meetings are never shared. From the shared calendars of up to 10 people the system finds common free slots within working hours and creates a Draft Meeting from the chosen slot, which then follows the normal approval flow. There is no AI assistant. | GAP-03 |

### 2. Limitations & Exclusions

| ID | Excluded Item | Reason |
| --- | --- | --- |
| LI-01 | Recording bots and server-side meeting recording | No recording bot joins the meeting room and the server never captures audio on its own. Audio is captured only in participants' browsers and under explicit human control: the Secretary turns the room microphone on and off, and personal microphones are recorded only after everyone has acknowledged the recording notice (FE-09). Recording without that control is excluded from the scope of this project, not merely deferred. |
| LI-02 | Real-time transcription, live captions and in-meeting AI assistance | Recorded speech is transcribed once it is saved: a room take when the Secretary stops it, and each personal-microphone sentence within seconds of being spoken (FE-10). The text is not shown as live captions in the room, and the AI draft is written only on request. A continuous streaming pipeline for captions or in-meeting AI assistance is excluded. |
| LI-03 | Video recording upload and video-based analysis | Only audio files are accepted for processing (FE-09). Video substantially increases storage and processing cost without improving either the transcript or the summary, which depend on speech alone. |
| LI-04 | Multi-language support, translation and subtitles | v1 operates in Vietnamese: the user interface, speech transcription and AI-generated summaries all use Vietnamese. The system performs no translation of any kind — neither of documents nor of meeting content — and produces no subtitles. Additional languages are deferred to a later version. |
| LI-05 | Integration with external conferencing platforms (Zoom, Google Meet, Microsoft Teams) | TrustCabinet provides its own meeting room (FE-08). Bridging into third-party conferencing services would reintroduce the external dependency identified in GAP-02; meetings held on those platforms are supported only by uploading their audio afterwards (FE-09). |
| LI-06 | Electronic and certificate-authority digital signatures (USB token / CA-issued certificates) | v1 does not sign the minutes, neither with an internal electronic signature nor with a CA-issued certificate: the Chair approves and issues them (FE-12). Signatures issued by a licensed certification authority require a commercial agreement and formal certification that are outside the scope of a capstone project. |
| LI-07 | Integration with external systems (HRM, ERP, Google Calendar, Outlook, chat platforms) and a public API | v1 has no integration layer or public API for HRM, ERP, external calendars or chat platforms. Supporting interfaces are limited to optional Google sign-in, email delivery through Resend (or SMTP), the self-hosted LiveKit room service and the AI services. Speech recognition runs on a self-hosted speech service; the AI draft of the minutes runs on a self-hosted language model, and only the drafting of Normal meetings may be sent to an external language-model API when an administrator selects it, under the data-processing safeguards. |
| LI-08 | Large-scale conferencing and advanced media features (webinars, live streaming, breakout rooms, virtual backgrounds) | The online meeting room targets internal team meetings of ordinary size. Broadcast-scale and media-production features do not address any gap identified in Section III. |
| LI-09 | Conversation analytics (sentiment analysis, talk-time coaching, engagement scoring) | These capabilities serve meeting coaching rather than meeting governance and are not traceable to any gap in Section III. |
| LI-10 | Native mobile application | v1 is delivered as a responsive web application, which covers mobile participation and review. A native iOS/Android client is deferred until post-launch usage data justifies the additional effort. |
| LI-11 | Multi-tenant SaaS deployment | The system is installed for a single organisation on TrustSoft's own infrastructure, consistent with the self-hosted approach described in Section IV. Multi-tenancy and tenant isolation are out of scope. |
| LI-12 | Task assignment, action-item tracking and deadline management | Action items agreed in a meeting are recorded as text in the approved summary and the minutes, but they are not converted into tracked tasks with an owner, a deadline or a status, and later meetings do not update them. This is consistent with the project purpose in Section I.2. |
