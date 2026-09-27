# CAPSTONE PROJECT REPORT

## Report 1 – Project Introduction

**Project Code:** IMDMS  
**Group:** SEP490-G116  
**Location & Time:** Hanoi, Sep 2026  

---

## Table of Contents

- [Change Log](#change-log)
- [Definitions and Acronyms](#definitions-and-acronyms)
- [I. Project Overview](#i-project-overview)
  - [1. Project Information](#1-project-information)
  - [2. Project Purpose](#2-project-purpose)
  - [3. Project Stakeholders](#3-project-stakeholders)
- [II. Product Background](#ii-product-background)
- [III. Existing Solutions](#iii-existing-solutions)
  - [1. Fellow](#1-fellow)
  - [2. Fireflies.ai](#2-firefliesai)
  - [3. Asana + Zoom + AI Studio](#3-asana--zoom--ai-studio)
  - [4. ClickUp](#4-clickup)
  - [5. Section Summary](#5-section-summary)
- [IV. Proposed Solution](#iv-proposed-solution)
- [V. Project Scope & Limitations](#v-project-scope--limitations)
  - [1. Major Features](#1-major-features)
  - [2. Limitations & Exclusions](#2-limitations--exclusions)

---

## Change Log

| Date | A\*, M, D | In charge | Change Description |
| :--- | :--- | :--- | :--- |
| 14/09/2026 | A | KhanhPG | Add Overview, Product Background, Existing Systems |
| 15/09/2026 | A, M | LocHD | Add Project Scope & Limitations (gap reference, major features, limitations); align Project Purpose, Existing Solutions summary and Proposed Solution with the project scope |
| 27/09/2026 | M | KhanhPG | Update Fellow section and Project Purpose section, Product background section |

*\*A - Added, M - Modified, D - Deleted*

---

## Definitions and Acronyms

| Acronym | Definition |
| :--- | :--- |
| **IMDMS** | TrustCabinet — Internal Meeting and Document Management System for TrustSoft Company |
| **FE** | Feature |
| **LI** | Limitation |
| **EX** | Exclusion |

---

## I. Project Overview

### 1. Project Information

* **English name:** *TrustCabinet — Internal Meeting and Document Management System for TrustSoft Company*
* **Vietnamese name:** *TrustCabinet – Hệ thống quản lý cuộc họp và tài liệu nội bộ cho Công ty TrustSoft*
* **Project code:** *IMDMS*
* **Group name:** *SEP490-G116*
* **Software type:** *Web App*

### 2. Project Purpose

The purpose of this project is to develop the **Internal Meeting and Document Management System (IMDMS)**, a centralized platform designed to support the management of internal organizational meetings and their related documents. The system supports key meeting activities including meeting scheduling and preparation, document management, participant management, discussion records, voting, and post-meeting documentation. 

After a meeting is completed, the system uses AI to assist in generating an editable draft of the meeting minutes by consolidating available meeting information such as uploaded meeting recordings, secretary notes, meeting documents, discussion outcomes, decisions, and voting results. The generated minutes can then be reviewed and edited by authorized users before being finalized. 

In addition, the platform provides user and role management, access control, notifications, reporting, and audit logging to improve the security, accountability, and traceability of meeting information within the organization. The current project scope focuses on meeting and document management and does not include task assignment, action-item tracking, or deadline management. This document defines the project scope, objectives, and intended functions of the system and serves as a guideline for its design and implementation.

### 3. Project Stakeholders

| Full Name | Role | Email | Mobile |
| :--- | :--- | :--- | :--- |
| Nguyen Quang Hung | Lecturer / Supervisor | HungNQ40@fe.edu.vn | 0912345678 |
| Tran Viet Hai | Leader / BA | kdodjeksdkkd@gmail.com | 0947361416 |
| Pham Gia Khanh | Member / Frontend Lead | phamgiakhanh0709@gmail.com | 0903476776 |
| Nguyen Quang Huy | Member / Dev | luckyhuy100@gmail.com | 0335245834 |
| Hoang Duc Loc | Member / Backend Lead | ducloc110805@gmail.com | 0946718111 |
| Nguyen Truong Huy | Member / Test Lead | nguyentruonghuy294@gmail.com | 0969483804 |

---

## II. Product Background

TrustSoft is a Hanoi-based technology company where project teams run three to five internal meetings per week, including sprint planning, client syncs, and internal reviews. These meetings are currently coordinated through a patchwork of tools: Google Calendar for scheduling, Zalo or Teams for sharing meeting links and updates, and Google Docs, paper notes, or personal digital notes for recording discussion outcomes.

Meeting-related documents are also handled separately from the meeting process. Agendas, specifications, reports, and other supporting files may be shared through chat messages, document links, or shared folders before a meeting. As a result, participants may need to search across several conversations or folders to find the latest version of a document, confirm what was reviewed, or locate materials from an earlier meeting. There is no single place where the meeting, its supporting documents, discussion records, and subsequent results can be accessed together.

A typical project lead at TrustSoft spends considerable time before each meeting reviewing previous documents, notes, and chat history to recall what was agreed, what remains unresolved, and what needs to be discussed again. During the meeting itself, whoever is designated to take notes participates less actively in the discussion, while important details—such as decisions, responsibilities, and deadlines—depend on that person capturing everything correctly in real time.

Decision-making is similarly fragmented. When the team needs to vote on matters such as approving a technical approach or prioritizing a backlog item, voting is often handled informally through verbal responses or chat messages such as “yes” or “agree.” The result is not maintained as a structured voting record linked to the meeting or the issue being decided. If the decision is questioned later, the team may have to search through chat history or meeting notes to determine who participated, what options were considered, and what conclusion was reached.

The same problem continues after the meeting. Meeting minutes, when prepared, are usually created manually from notes and shared separately through a document link. This makes it difficult to maintain a consistent record that brings together meeting information, discussion outcomes, voting results, decisions, and assigned actions. For client-facing meetings, verbal commitments may also be missed or recorded incompletely, becoming visible only when the client follows up and the team has no reliable meeting record to refer to.

Meeting notes may reach the team one or two days later and are often presented as long, unstructured text. Action items discussed verbally may never be transferred into the team's task-tracking process unless someone does so manually, while unresolved items can reappear in later meetings because their status was not followed up consistently. Documents, notes, voting results, and meeting outcomes therefore remain distributed across different tools rather than forming a connected meeting record.

For a growing technology company managing multiple concurrent client projects, this fragmentation creates more than administrative overhead. Team members spend time searching for information, checking document versions, reconstructing previous decisions, and manually consolidating meeting results. More importantly, the gap between what is prepared before a meeting, what is decided during it, and what is recorded and followed up afterward creates a direct risk to project timelines, accountability, and client trust.

This gap has become more practical to address as speech recognition, automatic summarization, and task-extraction technologies have matured. Combined with centralized meeting documents, structured voting records, and consolidated meeting minutes, these technologies make it possible to connect what was prepared, discussed, decided, and assigned within a single meeting workflow without adding significant manual overhead for TrustSoft's project teams.

---

## III. Existing Solutions

### 1. Fellow

Fellow is an AI meeting assistant that supports meeting recording, transcription, and AI-generated meeting recaps. It can capture live meetings or process uploaded audio and video recordings, then generate transcripts, summaries, key decisions, and other meeting outcomes.

| Criteria | Detail |
| :--- | :--- |
| **Website / Link** | [https://fellow.ai](https://fellow.ai) |
| **Target Users** | Teams and organizations that need AI-assisted meeting capture, documentation, and follow-up |
| **Core Features** | Meeting recording; audio/video upload; transcription; AI-generated and editable summaries; key decision extraction; collaborative meeting notes; searchable meeting history |
| **Strengths** | Mature meeting capture workflow; supports both live recordings and uploaded recordings; AI-generated summaries and decisions can be reviewed and edited after the meeting |
| **Weaknesses** | Voting is documented mainly through reactions and informal pulse checks rather than a structured voting process with formal voting sessions and recorded voting outcomes |
| **GAP for this project** | Fellow focuses on AI meeting recaps and collaborative notes. TrustCabinet focuses on producing an editable consolidated meeting minute from meeting recordings and secretary notes, bringing together discussion content, decisions, and structured voting results into a single meeting record. |

### 2. Fireflies.ai

Fireflies.ai is an AI meeting assistant that automatically records, transcribes, and analyzes voice conversations across multiple video conferencing platforms.

| Criteria | Detail |
| :--- | :--- |
| **Website / Link** | [https://fireflies.ai](https://fireflies.ai) |
| **Target Users** | Remote teams, sales professionals, and project managers |
| **Core Features** | Automated recording/transcription, AI summaries, sentiment analysis, conversation intelligence, integrations with CRM and task management tools |
| **Strengths** | Excellent multi-platform integration (Zoom, Teams, Google Meet), strong transcription accuracy, and conversational search |
| **Weaknesses** | Third-party cloud processing raises data privacy concerns; ongoing subscription costs; limited native document workflow management |
| **GAP for this project** | Fireflies.ai focuses primarily on meeting transcription and analytics rather than providing a complete, self-hosted, end-to-end document governance and voting system required by TrustSoft. |

### 3. Asana + Zoom + AI Studio

| Criteria | Detail |
| :--- | :--- |
| **Website / Link** | [https://asana.com](https://asana.com) / [https://zoom.us](https://zoom.us) |
| **Target Users** | Tech-savvy project teams combining disparate tools |
| **Core Features** | Video conferencing via Zoom, custom script processing via AI Studio, and project task tracking in Asana |
| **Strengths** | High flexibility, powerful standalone tools for each specific step of the process |
| **Weaknesses** | Requires manual effort to bridge data across platforms, fragmented user experience, high overhead cost for multiple SaaS subscriptions |
| **GAP for this project** | Combining separate platforms lacks a cohesive workflow, leading to data silos, extra manual work to sync information, and missing integrated security mechanisms like digital signatures. |

### 4. ClickUp

ClickUp is an all-in-one productivity platform that integrates task management, document collaboration, and AI-powered meeting tools.

| Criteria | Detail |
| :--- | :--- |
| **Website / Link** | [https://clickup.com](https://clickup.com) |
| **Target Users** | Cross-functional teams, project managers, and agile organizations |
| **Core Features** | ClickUp Brain features (meeting transcript analysis, automatic generation of follow-up tasks with assignees and deadlines, AI Notetaker, Action Item Extractor Agent), and Docs integration |
| **Strengths** | Unified environment for task management and documentation with strong AI automation capabilities |
| **Weaknesses** | High platform complexity, recurring subscription costs, and lack of specialized internal governance workflows |
| **GAP for this project** | ClickUp is a general-purpose cloud SaaS platform that lacks self-hosted enterprise deployment options, native digital signing for meeting minutes, and specialized organizational meeting governance tailored specifically for TrustSoft. |

### 5. Section Summary

Existing enterprise platforms such as Microsoft Teams and dedicated meeting-governance solutions such as Decisions provide comprehensive meeting, document, decision, and approval capabilities, while AI meeting platforms such as Fellow, Fireflies.ai, and ClickUp can extract action items from meeting transcripts and support task follow-up. However, these solutions generally address meeting intelligence, task management, or document governance as separate capabilities, rather than providing a unified closed-loop workflow in which meeting conversations generate tasks, subsequent discussions automatically identify task progress or blockers, proposed status changes are reviewed by users, and unresolved tasks can inform future meeting agendas. 

Vietnamese E-Office platforms provide strong document, approval, role-based access, and digital-signature support, but their meeting functionality is primarily focused on scheduling and administration rather than AI-assisted meeting intelligence. 

This gap motivates TrustCabinet as an integrated internal meeting and document management platform that connects meeting discussions, AI-generated insights, task lifecycle management, document governance, and formal meeting outcomes within a single workflow tailored to the needs of a Vietnamese technology company.

---

## IV. Proposed Solution

TrustCabinet is an integrated, internally hosted meeting and document management platform designed specifically to streamline and digitize the end-to-end meeting lifecycle for TrustSoft Company. It bridges the critical gap between verbal meeting discussions and post-meeting execution by providing structured tools for pre-meeting agenda and document preparation, voting during or after sessions, and speaker-attributed transcription followed by automated AI extraction of summary notes, decisions, and action items once the meeting audio has been uploaded. By directly connecting meeting outcomes to task tracking and document governance with digital signing support, TrustCabinet ensures full accountability and eliminates the manual overhead of handling disconnected tools.

The system provides tailored benefits across all organizational roles at TrustSoft:
* **Project Leads and Meeting Hosts:** Spend significantly less time manually compiling agendas, capturing discussions, and chasing task updates across chat threads.
* **Team Members and Meeting Attendees:** Can focus fully on active participation without the burden of real-time note-taking, receiving clear AI-generated summaries and structured action items assigned directly to them once the meeting audio has been processed.
* **Management and Executives:** Gain complete transparency through centralized meeting archives, digitally signed minutes, and clear audit trails of key decisions and project commitments.

A self-hosted web application is the ideal approach for TrustCabinet as it guarantees full data privacy and security by retaining sensitive corporate documents, meeting transcripts, and strategic decisions within TrustSoft's own infrastructure. Additionally, it eliminates recurring SaaS subscription fees while giving TrustSoft complete freedom to adapt and scale the system's workflows to match its evolving internal operational processes.

---

## V. Project Scope & Limitations

For traceability, the gaps identified in Section III – Existing Solutions are consolidated and given the identifiers below. Every feature listed in Section V.1 is mapped to one or more of these gaps, and no feature is in scope unless it closes at least one of them.

| ID | Gap | Description |
| :--- | :--- | :--- |
| **GAP-01** | No self-hosted deployment option | Confidential meeting content — audio, transcripts, minutes and strategic decisions — is captured automatically (typically by a recording bot that joins the call), processed on third-party cloud infrastructure and locked behind recurring subscription fees. None of the reviewed products can be deployed inside the company's own infrastructure, and none leaves the decision to capture a conversation under explicit human control. *(Fellow, Fireflies.ai, ClickUp, Asana + Zoom + AI Studio)* |
| **GAP-02** | Conferencing is an external dependency | The reviewed meeting-intelligence tools attach to a separate conferencing service rather than providing the meeting room themselves, so the conversation, its recording and its supporting documents are split across different vendors and account systems. *(Fellow, Fireflies.ai, ClickUp, Asana + Zoom + AI Studio)* |
| **GAP-03** | Fragmented toolchain and data silos | Agenda, supporting documents, minutes and follow-up tasks are held in different platforms, which forces manual synchronisation, fragments the user experience and leaves no single source of truth for a given meeting. *(Asana + Zoom + AI Studio, ClickUp, Fellow)* |
| **GAP-04** | Broken loop between discussion and execution | Meeting output is not connected to the lifecycle of the tasks it creates: later discussions do not update task progress or blockers, and items left unresolved do not flow back into the agenda of the following meeting. *(Section III – Section Summary)* |
| **GAP-05** | No formal meeting governance | Structured voting on an agenda item, an immutable record of resolutions, digitally signed minutes and an audit trail of who approved what are absent from the reviewed products, so formally binding meeting outcomes cannot be produced inside the tool. *(Fireflies.ai, Asana + Zoom + AI Studio, ClickUp)* |
| **GAP-06** | Meeting intelligence and document governance never coexist | Vietnamese E-Office platforms provide document management, approval flows, role-based access and digital signatures but treat meetings only as scheduling; AI meeting platforms provide transcription and summarisation but lack organisation-specific access control, archiving and management reporting. *(Section III – Section Summary, Fellow, ClickUp)* |

### 1. Major Features

| ID | Feature Name | Description | Addressed Gaps |
| :--- | :--- | :--- | :--- |
| **FE-01** | Authentication & Account Security | Users sign in with a company account under a password policy, session expiry and automatic lockout after repeated failed attempts, with all sign-in activity logged. Authentication is served by TrustSoft's own instance rather than an external identity service. | GAP-01 |
| **FE-02** | User & Profile Management | Administrators create, update, deactivate and unlock accounts, assign each user to a department and a role, and maintain the profile information used across meetings, minutes and reports. | GAP-06 |
| **FE-03** | Department & Team Management | Administrators define TrustSoft's organisational structure — departments, teams and their members — so that meetings, documents and access rights can be scoped to the company's real reporting lines. | GAP-06 |
| **FE-04** | Role-Based Access Control | Every meeting, document, audio file and report is visible only to the roles entitled to it (Administrator, Project Lead / Host, Secretary, Attendee, Executive). Permission sets are configurable by the company without vendor involvement, and access decisions are recorded in an audit log. | GAP-01, GAP-05, GAP-06 |
| **FE-05** | Meeting & Schedule Management | Hosts create one-off or recurring meetings, build the agenda item by item, invite internal participants and track invitation responses. A shared calendar view shows every meeting a user is involved in and flags scheduling conflicts. | GAP-03 |
| **FE-06** | Meeting Document Management | Agenda attachments, reference materials and meeting outputs are uploaded to the meeting they belong to, versioned, previewed in the browser and kept in a searchable archive, so that a meeting and its documents remain one record. | GAP-03, GAP-06 |
| **FE-07** | Virtual Meeting Room | Participants join a browser-based meeting room hosted by TrustCabinet itself, with audio, video, screen sharing and text chat, so the meeting is held on the company's own platform rather than a vendor's. The room performs no recording of its own; audio capture is handled separately and deliberately, as described in FE-08. | GAP-02 |
| **FE-08** | Meeting Audio Upload & Media Library | The system never captures audio by itself. For security reasons, a designated participant records the meeting on their own device and uploads the audio file afterwards; the file is attached to its meeting, stored under the same access rules, played back in the browser and queued for processing. Audio from meetings held outside TrustCabinet can be uploaded the same way. | GAP-01, GAP-02 |
| **FE-09** | Speaker-Attributed Transcription & Meeting Summary | Once an audio file has been uploaded, the system transcribes it and separates the speech by speaker, producing a transcript in the form “Speaker A said… / Speaker B said…” rather than one undifferentiated block of text. From that attributed transcript it generates a structured summary of discussion points, decisions and proposed action items, which the host reviews and approves before it is published to participants. Processing runs after the meeting has ended, never during it. | GAP-04, GAP-06 |
| **FE-10** | Voting & Resolution Management | Hosts open a vote on an agenda item live during a meeting, or publish a time-boxed vote that participants answer before or after it, with configurable options, an eligible-voter list, open or anonymous ballots and a quorum rule. The outcome is recorded as a resolution attached to the meeting and cannot be altered afterwards. | GAP-05 |
| **FE-11** | Meeting Minutes & Digital Signing | Approved summaries, decisions and voting results are compiled into formal minutes that designated participants sign electronically. Signed minutes are sealed against further modification and carry an audit trail of who signed what and when. | GAP-05, GAP-06 |
| **FE-12** | Task & Action Item Lifecycle Management | Action items taken from an approved meeting summary become tracked tasks with an owner, a deadline and a status. Progress and blockers identified in the summary of a later meeting are matched back to the corresponding task, and items still open are proposed as agenda candidates for the next meeting — closing the loop from discussion to execution. | GAP-04, GAP-03 |
| **FE-13** | Email & In-App Notification | Invitations, agenda changes, pre-meeting reminders, requests to upload the meeting audio, requests to sign minutes, voting calls and task deadline alerts are delivered by email and as in-application notifications, each linking directly to the item that requires action. | GAP-03, GAP-04 |
| **FE-14** | Personal Dashboard & Meeting Workspace | Each user opens one workspace showing upcoming meetings, documents awaiting review, minutes awaiting signature, open votes and tasks assigned to them, so that no one has to check several tools to know what is expected of them. | GAP-03, GAP-04 |
| **FE-15** | Reporting & Statistics | Managers and executives obtain aggregated figures on meetings held, attendance, decisions and resolutions recorded, action-item completion rates and overdue tasks, broken down by department or project and exportable for internal reporting. | GAP-06, GAP-04 |

### 2. Limitations & Exclusions

| ID | Excluded Item | Reason |
| :--- | :--- | :--- |
| **LI-01** | Automatic or server-side meeting recording | The system does not capture audio by itself, and no recording bot joins the meeting room. For security reasons the decision to record stays under explicit human control: a designated participant records the session and uploads the audio afterwards (FE-08). Automated capture is excluded from the scope of this project, not merely deferred. |
| **LI-02** | Real-time transcription, live captions and in-meeting AI assistance | Transcription, speaker attribution and summarisation are performed only after an audio file has been uploaded (FE-09). Live processing would require a continuous streaming pipeline fed by automatic capture, which LI-01 excludes. |
| **LI-03** | Video recording upload and video-based analysis | Only audio files are accepted for processing (FE-08). Video substantially increases storage and processing cost without improving either the transcript or the summary, which depend on speech alone. |
| **LI-04** | Multi-language support, translation and subtitles | v1 operates in Vietnamese: the user interface, speech transcription and AI-generated summaries all use Vietnamese. The system performs no translation of any kind — neither of documents nor of meeting content — and produces no subtitles. Additional languages are deferred to a later version. |
| **LI-05** | Integration with external conferencing platforms (Zoom, Google Meet, Microsoft Teams) | TrustCabinet provides its own meeting room (FE-07). Bridging into third-party conferencing services would reintroduce the external dependency identified in GAP-02; meetings held on those platforms are supported only by uploading their audio afterwards (FE-08). |
| **LI-06** | Certificate-authority digital signatures (USB token / CA-issued certificates) | v1 implements internal electronic signing with a complete audit trail (FE-11). Signatures issued by a licensed certification authority require a commercial agreement and formal certification that are outside the scope of a capstone project. |
| **LI-07** | Integration with external systems (HRM, ERP, Google Calendar, Outlook, chat platforms) and a public API | v1 operates as a self-contained internal system. An integration layer is deferred until the core meeting-to-task workflow has been validated in real use. |
| **LI-08** | Large-scale conferencing and advanced media features (webinars, live streaming, breakout rooms, virtual backgrounds) | The virtual meeting room targets internal team meetings of ordinary size. Broadcast-scale and media-production features do not address any gap identified in Section III. |
| **LI-09** | Conversation analytics (sentiment analysis, talk-time coaching, engagement scoring) | These capabilities serve meeting coaching rather than meeting governance and are not traceable to any gap in Section III. |
| **LI-10** | Native mobile application | v1 is delivered as a responsive web application, which covers mobile participation and review. A native iOS/Android client is deferred until post-launch usage data justifies the additional effort. |
| **LI-11** | Multi-tenant SaaS deployment | The system is installed for a single organisation on TrustSoft's own infrastructure, consistent with the self-hosted approach described in Section IV. Multi-tenancy and tenant isolation are out of scope. |
