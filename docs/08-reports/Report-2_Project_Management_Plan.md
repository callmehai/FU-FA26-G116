# Report 2 – Project Management Plan

> **GENERATED — DO NOT EDIT.** Snapshot từ DOCX local. Sửa [`source/Report_2_Project_Management_Plan.docx`](source/Report_2_Project_Management_Plan.docx) rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report 2`. Sửa tay file này sẽ mất ở lần sync sau. Chi tiết: [08-reports/README.md](README.md).

CAPSTONE PROJECT REPORT

Report 2 – Project Management Plan

– Hanoi, Sep 2026 –

Table of Contents

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 19/09/2026 | A | LocHD | Add Project Overview and Management Approach (initial version 1.0). |
| 27/09/2026 | M, D | LocHD | Remove Task & Action Item Lifecycle Management and renumber subsequent features. |
| 05/10/2026 | M, D | KhanhPG | Sync plan with PO-confirmed feature and UC baseline (personal library, audio upload, audit, minutes). |
| 09/10/2026 | M, D | KhanhPG | Align meeting-level lifecycle and voting scope; renumber FE-01–FE-15 with effort and dates preserved. |
| 13/10/2026 | M | HaiTV | Align document handling (immutable files, folders, browser previews) and in-meeting voting rules. |
| 30/10/2026 | M | HuyNQ | Resolve v1.2 review findings: close TBDs, refine lifecycle and risks, fix Table of Contents. |
| 15/11/2026 | M, D | KhanhPG | Drop out-of-scope features from Iteration 6; end account lifecycle at deactivation and keep sign-in rate limit. |
| 24/11/2026 | M, A, D | LocHD | Align with supervisor review and built system: approved minutes, AI setup, recording and app/main branches. |
| 25/11/2026 | M | LocHD | Send outgoing email via Resend only, renumber FE-01–FE-15 without gaps and adjust heading page breaks. |
| 26/11/2026 | M | KhanhPG, LocHD | Update quality test metrics and restrict AI drafting to the team's self-hosted language model. |

*A - Added M - Modified D - Deleted

## Definition and Acronyms

| Acronym | Definition |
| --- | --- |
| IMDMS | TrustCabinet — Internal Meeting and Document Management System for TrustSoft Company |
| FE | Feature |
| LI | Limitation |
| EX | Exception |

## I. Project Overview

### 1. Scope and Purpose

This Project Management Plan defines how group SEP490-G116 organises, executes, monitors and controls the development of TrustCabinet (Internal Meeting and Document Management System) for TrustSoft over the fifteen-week capstone period from 14 September 2026 to 27 December 2026. Its purpose is to establish the project scope, work breakdown, schedule, effort commitment, quality objectives, team responsibilities, communication channels and configuration practices required to deliver the system on time.

The product scope comprises fifteen active core features grouped into five functional areas:

●  	Identity and access - FE-01 Authentication & Account Security, FE-02 User & Profile Management, FE-03 Role-Based Access Control.

●  Meeting operations - FE-04 Meeting Management, FE-05 Agenda Management, FE-06 Meeting Participant Management, FE-07 Personal Document Library & Meeting Document Management, FE-08 Online Meeting Room Management.

●  Meeting intelligence - FE-09 Meeting Recording, FE-10 Transcription, Speaker Identification & Meeting Summary.

●  Meeting governance - FE-11 Voting Management and FE-12 Meeting Minutes & Approval.

●  Communication and visibility - FE-13 Email & In-App Notification, FE-14 Reporting & Statistics, FE-15 Working Calendar & Meeting Time Search.

Each feature is elaborated into detailed requirements, designed, implemented and tested incrementally across the planned iterations, with task-level progress tracked continuously by the team.

The following capabilities are explicitly excluded from the project scope: recording bots and server-side recording; real-time transcription, captions or in-meeting AI; video processing; translation and additional languages; bridges to external conferencing platforms; electronic and certificate-authority signatures; HRM, ERP, external calendar or chat integrations and a public API; large-scale or advanced media features; conversation analytics; a native mobile client; multi-tenancy; and task assignment, action-item tracking or deadline management. The project also excludes a separate Meeting Session layer, time-boxed voting, voting deadlines, quorum rules and a separate Resolution object. Google sign-in, email delivery, the self-hosted online meeting room service and self-hosted AI services (speech and drafting) serve purely as supporting interfaces. Any proposed scope addition requires formal change control rather than being absorbed into the committed schedule.

TrustCabinet is a standalone capstone project with no delivery dependency on any other project, and it reuses no existing code base: every component within scope is built by the team over the fifteen-week schedule. Its purpose is to provide TrustSoft with an end-to-end paperless meeting workflow—from scheduling, document preparation and online collaboration to recording, transcription, voting and approved meeting minutes—governed by clear milestones, effort commitments and quality targets.

### 2. Assumptions & Constraints

| No | Description | Note |
| --- | --- | --- |
| Assumptions |  |  |
| 1 | The Product Owner, Dinh Van Toi of TrustSoft, is available within the requirements phase of each iteration to confirm the detailed requirements and acceptance criteria that the Business Analyst elaborates from the scope baselined in Report 1, and to attend the demonstration of each increment. | External Interfaces |
| 2 | Feedback from the supervisor on a submitted report version is received within seven days. If no comment is raised within that period, the version is treated as the current baseline for the following work. | External Interfaces |
| 3 | All five members remain on the team for the whole fifteen weeks and are able to commit the hours stated in Section I.4 - Capacity Planning. | Resources |
| 4 | Meeting audio used to develop and test FE-09 and FE-10 is produced by the team's own recorded meetings in Vietnamese. No real organisational meeting material is used as test data. | Quality / Security |
| 5 | The free and educational licences the team relies on (Jira, GitHub, Google Workspace) remain available for the whole project at no cost. | Resources |
| Constraints |  |  |
| 1 | The project must be delivered within the fifteen-week academic schedule, 14/09/2026 to 27/12/2026. The closing milestone cannot be moved, so scope rather than schedule absorbs any overrun. | Schedule |
| 2 | The project has no budget. Only free, open-source or student-licensed tools, services and infrastructure may be used. | Budget |
| 3 | The team consists of five student members who each commit 40 hours a week to the project (8 h/day, 5 days/week, Section I.4). Each member also carries other courses; these are scheduled outside the committed hours, and any time they take back is absorbed by the 20% buffer of Section I.4. There is no dedicated designer or DevOps engineer. | Resources |
| 4 | The Product Owner is Dinh Van Toi of TrustSoft. Product requirements, priorities and acceptance criteria are confirmed by the Product Owner, and any change to the product scope requires his approval. The supervisor provides academic guidance and reviews the team’s work, and does not take the Product Owner role or approve product requirements. | External Interfaces |
| 5 | The core application, relational database, documents and the self-hosted LiveKit meeting-room service must support deployment inside TrustSoft. Optional Google sign-in must coexist with email/password sign-in; outgoing mail is sent through Resend. Speech recognition, speaker attribution and Voice ID run on a self-hosted speech worker, and the AI draft of the minutes runs on a self-hosted language model; no outside AI service is called. Core self-hosting does not imply that every supporting service runs on-premises. | Technology |
| 6 | Version 1 operates in Vietnamese only, with no translation and no subtitles (Report 1, LI-04). | Scope |
| 7 | No recording bot joins a meeting and the server never records on its own. Audio is captured in the browser under human control after attendees are informed: the Secretary records the room, personal microphones are recorded only after everyone has acknowledged the recording notice, and an audio file may be uploaded instead (Report 1, FE-09, LI-01 and LI-02). | Scope / Security |
| 8 | Meeting minutes are approved and issued by the Chair and are not signed: neither internal electronic signatures nor certificate-authority signatures on a USB token are in scope (Report 1, FE-12 and LI-06). | Scope |
| 9 | The technology stack is fixed in the source code repository: ASP.NET Core 10 (C#) for the backend, React 19 with TypeScript and Vite for the frontend, PostgreSQL 17 through Entity Framework Core 10 as the database and Redis for refresh tokens (Section II.6.3). Speech processing runs in-house in a separate CPU-only worker (decided on 23/10/2026), and the AI draft of the minutes runs on a self-hosted language model, with an external API allowed for Normal meetings only. | Technology |

### 3. Cost & Time Estimations

Effort is expressed in eight-hour person-days (pds). The 300 pds committed below are the effective working capacity derived in Section I.4; the 75 pds buffer remains unallocated. The total allocates 20 pds for initial planning and 40 pds for each of six iterations and the deployment phase. The fifteen active features are FE-01 to FE-14, aligned with the Report 3 SRS baseline of 24/11/2026. Consolidating the Meeting lifecycle and narrowing voting changes work-package content; the iteration dates and closing milestone remain unchanged.

| # | Work Package | Effort<br>(pds) | Due Date |
| --- | --- | --- | --- |
| 0 | Initial Planning (week 1)<br>14/09/2026 - 20/09/2026 | 20 |  |
| 0.1 | Report 1: Project Introduction v0.9 | 4 | 20/09/2026 |
| 0.2 | Technical feasibility study: virtual meeting room and speaker-attributed transcription (proof of concept) | 6 | 20/09/2026 |
| 0.3 | Overall architecture design, repository and development environment setup | 6 | 20/09/2026 |
| 0.4 | UI mock-up for the Iteration 1 scope | 4 | 20/09/2026 |
| 1 | Iteration 1: Identity & Access Foundation - FE-01 to FE-03 (weeks 2-3)<br>21/09/2026 - 04/10/2026 | 40 |  |
| 1.1 | Report 1: Project Introduction v1.0 | 2 | 27/09/2026 |
| 1.2 | Report 2: Project Management Plan v1.0 | 4 | 27/09/2026 |
| 1.3 | Report 3: SRS v0.9 (overall requirements) | 4 | 04/10/2026 |
| 1.4 | FE-01 Authentication & Account Security | 6 | 04/10/2026 |
| 1.5 | FE-02 User & Profile Management | 6 | 04/10/2026 |
| 1.6 | FE-03 Role-Based Access Control - role & permission model | 4 | 04/10/2026 |
| 1.7 | FE-03 Role-Based Access Control - permission configuration & audit | 7 | 04/10/2026 |
| 1.8 | Code package & Report 5.1 Unit Test | 4 | 04/10/2026 |
| 1.9 | Software Package v1.0 (increment 1) | 3 | 04/10/2026 |
| 2 | Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-07, FE-13 (weeks 4-5)<br>05/10/2026 - 18/10/2026 | 40 |  |
| 2.1 | Report 2: Project Management Plan v1.1 | 3 | 11/10/2026 |
| 2.2 | Report 3: SRS v1.0 (detailed requirements for Iterations 1-2) | 4 | 18/10/2026 |
| 2.3 | Report 4: SDS v1.0 (high level design specifications) | 4 | 18/10/2026 |
| 2.4 | Report 5.0: Test Documentation (test planning) | 3 | 18/10/2026 |
| 2.5 | FE-04 to FE-06 Meeting Management, Agenda Management & Meeting Participant Management (including the meeting approval workflow) | 9 | 18/10/2026 |
| 2.6 | FE-07 Personal Document Library & Meeting Document Management | 4 | 18/10/2026 |
| 2.7 | FE-13 Email & In-App Notification | 6 | 18/10/2026 |
| 2.8 | Code package & Report 5.1 Unit Test | 4 | 18/10/2026 |
| 2.9 | Software Package v2.0 (increment 2) | 3 | 18/10/2026 |
| 3 | Iteration 3: Meeting Audio & AI Processing - FE-09, FE-10 (weeks 6-7)19/10/2026 - 01/11/2026 | 40 |  |
| 3.1 | Report 3: SRS v1.1 (detailed requirements for Iteration 3) | 4 | 25/10/2026 |
| 3.2 | Report 4: SDS v1.1 (detailed design of the audio processing pipeline) | 4 | 25/10/2026 |
| 3.3 | FE-09 Meeting Recording - room recording, per-microphone sentences and audio upload | 7 | 01/11/2026 |
| 3.4 | FE-10 Transcription, Speaker Identification & Meeting Summary | 11 | 01/11/2026 |
| 3.5 | Report 5.2: Integration Test cases and Integration Test report | 4 | 01/11/2026 |
| 3.6 | Code package & Report 5.1 Unit Test | 4 | 01/11/2026 |
| 3.7 | Report 2: Project Management Plan v1.2 | 3 | 25/10/2026 |
| 3.8 | Software Package v3.0 (increment 3) | 3 | 01/11/2026 |
| 4 | Iteration 4: Online Meeting Room Management - FE-08 (weeks 8-9)02/11/2026 - 15/11/2026 | 40 |  |
| 4.1 | Report 3: SRS v1.2 (detailed requirements for Iteration 4) | 4 | 08/11/2026 |
| 4.2 | Report 4: SDS v1.2 (detailed design of the meeting room and media flow) | 4 | 08/11/2026 |
| 4.3 | FE-08 Online Meeting Room Management | 16 | 15/11/2026 |
| 4.4 | Report 5.2: Integration Test cases and Integration Test report | 4 | 15/11/2026 |
| 4.5 | Code package & Report 5.1 Unit Test | 6 | 15/11/2026 |
| 4.6 | Report 2: Project Management Plan v1.3 | 3 | 08/11/2026 |
| 4.7 | Software Package v4.0 (increment 4) | 3 | 15/11/2026 |
| 5 | Iteration 5: Meeting Governance - FE-11, FE-12 (weeks 10-11)16/11/2026 - 29/11/2026 | 40 |  |
| 5.1 | Report 3: SRS v1.3 (detailed requirements for Iteration 5) | 4 | 22/11/2026 |
| 5.2 | Report 4: SDS v1.3 | 4 | 22/11/2026 |
| 5.3 | FE-11 Voting Management - in-meeting voting, tallies and Chair conclusions | 9 | 29/11/2026 |
| 5.4 | FE-12 Meeting Minutes & Approval - generation from the template, editing, approval, issue and correction | 10 | 29/11/2026 |
| 5.5 | Report 5.2 and 5.3: Integration Test cases and System Test cases | 5 | 29/11/2026 |
| 5.6 | Code package & Report 5.1 Unit Test | 5 | 29/11/2026 |
| 5.7 | Software Package v5.0 (increment 5) | 3 | 29/11/2026 |
| 6 | Iteration 6: Reporting & Calendar - FE-14, FE-15 (weeks 12-13)30/11/2026 - 13/12/2026 | 40 |  |
| 6.1 | FE-14 Reporting & Statistics | 5 | 13/12/2026 |
| 6.2 | FE-15 Working Calendar & Meeting Time Search | 4 | 13/12/2026 |
| 6.3 | Report 5.3 and 5.4: System Test cases and System Test report (FRs and NFRs) | 6 | 13/12/2026 |
| 6.4 | Report 6: Software User Guides (installation guide, user manual) | 6 | 13/12/2026 |
| 6.5 | Code package & Report 5.1 Unit Test | 4 | 13/12/2026 |
| 6.6 | Report 2: Project Management Plan v1.4 | 3 | 06/12/2026 |
| 6.7 | Software Package v6.0 (increment 6 - feature complete) | 3 | 13/12/2026 |
| 7 | Deployment: Acceptance, Stabilisation & Project Closure (weeks 14-15)<br>14/12/2026 - 27/12/2026 | 40 |  |
| 7.1 | Report 5.5: Acceptance Test scripts and acceptance test support | 7 | 20/12/2026 |
| 7.2 | Defect fixing and system stabilisation | 10 | 27/12/2026 |
| 7.3 | Report 7: Final Project Document | 7 | 27/12/2026 |
| 7.4 | Presentation file and demo rehearsal | 6 | 27/12/2026 |
| 7.5 | Final software package (source code, documents, deployment package) | 7 | 27/12/2026 |
| 7.6 | Report 2: Project Management Plan v1.5 (final update) | 3 | 27/12/2026 |
|  | TOTAL | 300 |  |

### 4. Capacity Planning

| Item | Value |
| --- | --- |
| Hours per person per week | 40 h (8 h/day x 5 days) |
| Team total per week | 5 people x 40 h = 200 h/week |
| Total duration | 15 weeks (14/09/2026 - 27/12/2026) |
| Gross capacity | 200 h/week x 15 weeks = 3,000 h = 375 person-days (8 h/person-day) |
| Buffer (meetings, reviews, unplanned - 20%) | 600 h = 75 person-days |
| Effective working capacity | 2,400 h = 300 person-days |

The buffer of 20% covers the time the team spends in meetings, reviews and retrospectives, the rework that follows a review, and the committed hours that other courses take back from the project (Constraint 3). Because it is excluded before the work packages of Section I.3 are planned, an overrun inside an iteration is absorbed by the buffer rather than pushing unfinished work into the next one.

### 5. Project Objectives

| # | Metric | Unit | Planned | Actual | Notes / References |
| --- | --- | --- | --- | --- | --- |
| 1 | Effort Usage | Person-day | 300 | 300 | Total effective working capacity distributed across planned work packages and tracked in the project tracking workbook. |
| 2 | Review Defects | No of defects | 30 | 142 | Defects identified during document and pull-request code reviews prior to test execution. |
| 3 | Unit Test Defects | No of defects | 40 | 1 | Defects found during developer-written unit testing of individual modules and functions. |
| 4 | Integration Test Defects | No of defects | 30 | 9 | Defects found when integrating frontend, backend APIs and supporting services. |
| 5 | System Test Defects | No of defects | 15 | 1 | Defects identified during end-to-end system testing against functional and non-functional requirements. |
| 6 | Acceptance Test Defects | No of defects | 5 | N/A | Defects found during acceptance testing with the Product Owner; no critical defect may remain open at closure. |
| 7 | Timeliness | % | 90 | 70 | Ratio of deliverables completed on or before their scheduled due date to total planned deliverables. |
| 8 | Requirement Completeness | % | 90 | 93 | Ratio of implemented and accepted requirements to total baselined requirements (threshold: > 75%). |
| 9 | Unit Test Code Coverage | % | 70 | 72 | Line coverage measured on core backend business logic, excluding generated code and configuration. |

### 6. Risks and Mitigations

Risks are reviewed at the end of every iteration. Probability and impact are re-assessed based on observed progress, materialised risks are converted into tracked issues with an owner and deadline, and obsolete risks are closed.

| # | Risk | Prob | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R01 | Real-time media features (online meeting room connection, NAT traversal and multi-party audio/video quality) involve unfamiliar complexity and may take longer than estimated. | High | High | Conduct an early feasibility study, build on a proven open-source media server rather than raw peer-to-peer connections, dedicate sufficient iteration capacity with schedule buffer, and define a reduced fallback scope (audio, screen sharing and chat) if needed. |
| R02 | Automated Vietnamese speech transcription and speaker attribution may produce inaccurate output, affecting downstream meeting summaries and minutes. | Medium | High | Evaluate candidate speech models early against sample recordings, establish a minimum quality threshold, provide manual transcript and speaker-label editing before summary generation, and schedule audio/AI delivery early enough to adjust the approach if necessary. |
| R03 | Self-hosted speech-processing and AI summarisation models may exceed the memory or compute capacity of the primary deployment environment. | Low | High | Decouple AI and speech services behind internal interfaces so workers can run on dedicated host machines with fallback nodes, and ensure the core application degrades gracefully with clear user feedback when an AI worker is unavailable. |
| R04 | Planned scope across the 15-week schedule may exceed available team capacity, especially when academic coursework reduces effective hours, causing unfinished work to roll over across iterations. | High | High | Prioritise features with the Product Owner into must-have and should-have tiers, reserve an unallocated capacity buffer, track actual effort at each iteration review to re-plan early, and manage any scope changes through formal change control. |
| R05 | Workload may be distributed unevenly, or a team member may become unavailable at a critical point, creating single-person knowledge bottlenecks. | Medium | Medium | Assign one accountable owner per work package while ensuring cross-training across modules, monitor workload balance regularly during reviews, and rebalance tasks at the start of the next iteration whenever capacity drifts. |

## II. Management Approach

### 1. Project Process/Methodology

The team applies the Iterative Incremental model to this project. Rather than building the system in a single sequential pass or delivering everything in one release at the end, the product is developed through a series of time-boxed iterations. Each iteration runs through planning, requirements, analysis and design, implementation, testing and evaluation, adding a verified working increment onto the previous baseline.

This model fits the project for two reasons. First, technically uncertain components cannot be estimated reliably upfront and are best validated early through working increments while adjustments are still inexpensive. Second, detailed requirements are progressively refined as stakeholders review each increment, allowing the team to elaborate features one iteration at a time and revisit analysis and design decisions whenever earlier increments reveal needed improvements.

Figure 1: Phases of the Iterative Incremental Model

View the full-size image on Google Drive

In this model, the project lifecycle consists of an initial planning phase, a sequence of fixed-length development iterations and a final deployment phase. Each development iteration executes six standard phases:

●  	Planning - Define the iteration goal, select the target features, break work into tasks with estimates and allocate capacity based on prior iteration velocity.

●  	Requirements - Elaborate the selected features into detailed, testable requirements and confirm acceptance criteria with stakeholders before design begins.

●  	Analysis & design - Translate the iteration requirements into data models, interface contracts and UI designs, updating earlier design decisions when needed.

●  	Implementation - Build and integrate the working increment on top of the previous baseline, adhering to agreed coding standards and the definition of done.

●  	Testing - Execute unit, integration and regression tests to verify both new functionality and compatibility with existing increments.

●  	Evaluation - Demonstrate the increment to stakeholders, assess progress against the iteration goal, review actual effort and defects and feed findings into the next iteration.

The deployment phase concludes the lifecycle by taking the feature-complete system through acceptance testing, defect resolution, stabilisation and final handover.

Across the iterations, work is sequenced by foundational dependencies and technical risk: core infrastructure and high-uncertainty capabilities are tackled in earlier iterations so risks are retired early, while dependent and supporting capabilities are delivered in later iterations.

The phases and iterations and their dates are:

●  	Initial Planning: 14/09/2026 - 20/09/2026

●  	Iteration 1: 21/09/2026 - 04/10/2026

●  	Iteration 2: 05/10/2026 - 18/10/2026

●  	Iteration 3: 19/10/2026 - 01/11/2026

●  	Iteration 4: 02/11/2026 - 15/11/2026

●  	Iteration 5: 16/11/2026 - 29/11/2026

●  	Iteration 6: 30/11/2026 - 13/12/2026

●  	Deployment: 14/12/2026 - 27/12/2026

Initial Planning (1 week)

Time: 14/09/2026 - 20/09/2026

Objectives:

●  	Configure the development environment, repository, issue tracker and shared engineering conventions.

●  	Establish the overall system architecture and component boundaries.

●  	Conduct technical feasibility studies for high-risk real-time media and speech-processing features.

●  	Prepare initial UI mock-ups and baseline the project scope (Report 1 v0.9).

Deliverables:

●  	Configured development environment, repository, Jira workspace and team conventions.

●  	Initial UI mock-ups and Report 1 v0.9.

Iteration 1: Identity & Access Foundation - FE-01 to FE-03 (2 weeks)

Time: 21/09/2026 - 04/10/2026

Iteration goal: Deliver the identity and access foundation for all subsequent features.

Phases of this iteration:

●  	Planning - Select FE-01 to FE-03 and allocate tasks against iteration capacity.

●  	Requirements - Elaborate authentication, user profile and role-permission requirements (Report 3 v0.9).

●  	Analysis & design - Design the identity data model, authentication flows and audit structure.

●  	Implementation - Build authentication, user management and role-based access control.

●  	Testing - Run unit tests and verify core login and authorisation flows (Report 5.1).

●  	Evaluation - Demonstrate account and role management; review effort and defects for Iteration 2.

Increment delivered: Software Package v1.0 (authentication, user management and RBAC).

Reports delivered: Report 1 v1.0, Report 2 v1.0, Report 3 v0.9.

Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-07, FE-13 (2 weeks)

Time: 05/10/2026 - 18/10/2026

Iteration goal: Deliver core meeting management, agenda, participants, documents and notifications.

Phases of this iteration:

●  	Planning - Select FE-04 to FE-07 and FE-13; adjust estimates using Iteration 1 actuals.

●  	Requirements - Elaborate meeting lifecycle, agenda, participants, document library and notification rules (Report 3 v1.0).

●  	Analysis & design - Extend the data model and design document storage and asynchronous notifications (Report 4 v1.0).

●  	Implementation - Build meeting, agenda, participant, document and notification modules.

●  	Testing - Execute unit and integration tests for new modules and create the test plan (Report 5.0).

●  	Evaluation - Demonstrate end-to-end meeting scheduling, documents and notifications to the Product Owner.

Increment delivered: Software Package v2.0 (meeting core, document library and notifications). Reports delivered: Report 2 v1.1, Report 3 v1.0, Report 4 v1.0, Report 5.0.

Iteration 3: Meeting Audio & AI Processing - FE-09, FE-10 (2 weeks)

Time: 19/10/2026 - 01/11/2026

Iteration goal: Deliver meeting audio recording, transcription, speaker identification and AI drafting.

Phases of this iteration:

●  	Planning - Select FE-09 and FE-10 with contingency buffer for model tuning.

●  	Requirements - Define recording, audio upload, consent, retention, transcription and AI draft rules (Report 3 v1.1).

●  	Analysis & design - Design the asynchronous audio-processing pipeline and internal AI provider interfaces (Report 4 v1.1).

●  	Implementation - Build meeting recording, audio upload, transcript editing, Voice ID and AI draft generation.

●  	Testing - Run unit and integration tests across the recording-to-draft pipeline and measure output quality (Report 5.2).

●  	Evaluation - Review transcription and summary quality against thresholds and feed outcomes into Iteration 4.

Increment delivered: Software Package v3.0 (recording, transcription, Voice ID and AI draft). Reports delivered: Report 2 v1.2, Report 3 v1.1, Report 4 v1.1, Report 5.2.

Iteration 4: Online Meeting Room Management - FE-08 (2 weeks)

Time: 02/11/2026 - 15/11/2026

Iteration goal: Deliver the integrated online meeting room.

Phases of this iteration:

●  	Planning - Dedicate the iteration to FE-08 with capacity buffer and confirmed fallback options.

●  	Requirements - Define real-time audio/video, screen sharing, chat, moderation and presence rules (Report 3 v1.2).

●  	Analysis & design - Design media-server integration, signalling and room-to-meeting linkage (Report 4 v1.2).

●  	Implementation - Build the online meeting room and link participant presence to the meeting record.

●  	Testing - Run unit, integration and multi-participant load tests plus regression over prior increments.

●  	Evaluation - Conduct a live team session to verify room stability and confirm final scope.

Increment delivered: Software Package v4.0 (self-hosted online meeting room). Reports delivered: Report 2 v1.3, Report 3 v1.2, Report 4 v1.2, Report 5.2.

Iteration 5: Meeting Governance - FE-11, FE-12 (2 weeks)

Time: 16/11/2026 - 29/11/2026

Iteration goal: Deliver in-meeting voting and the meeting minutes approval workflow.

Phases of this iteration:

●  	Planning - Select FE-11 and FE-12 together as linked governance workflows.

●  	Requirements - Define in-meeting voting rules, Chair conclusions and minutes generation, approval and revision (Report 3 v1.3).

●  	Analysis & design - Model votes, conclusions, minutes revisions and approval notifications (Report 4 v1.3).

●  	Implementation - Build voting management and meeting minutes preparation and approval.

●  	Testing - Verify voting eligibility, ballot rules and the end-to-end minutes approval flow (Reports 5.2, 5.3).

●  	Evaluation - Demonstrate the full meeting lifecycle from voting and conclusions to issued minutes.

Increment delivered: Software Package v5.0 (voting and approved meeting minutes).

Reports delivered: Report 3 v1.3, Report 4 v1.3, Reports 5.2 and 5.3.

Iteration 6: Reporting & Calendar - FE-14, FE-15 (2 weeks)

Time: 30/11/2026 - 13/12/2026

Iteration goal: Deliver reporting and calendar features to achieve feature completeness.

Phases of this iteration:

●  	Planning - Select FE-14 and FE-15 to build on completed core and governance data.

●  	Requirements - Finalise reporting metrics, exports, calendar sharing and meeting time search rules (Report 3 final).

●  	Analysis & design - Design reporting aggregations and free-slot search logic.

●  	Implementation - Build reporting, statistics, working calendar and time search modules.

●  	Testing - Run unit, integration, full system testing (Reports 5.3, 5.4) and full regression.

●  	Evaluation - Demonstrate the feature-complete system and triage remaining defects for deployment.

Increment delivered: Software Package v6.0 (feature complete). Reports delivered: Report 2 v1.4, Reports 5.3 and 5.4, Report 6.

Deployment: Acceptance, Stabilisation & Project Closure (2 weeks)

Time: 14/12/2026 - 27/12/2026

Objectives:

●  	Execute acceptance testing against agreed scripts with the Product Owner (Report 5.5).

●  	Resolve remaining defects and stabilise the production-ready deployment package.

●  	Verify deployment against the user and installation guides.

●  	Finalise all project reports (including Report 7 and Report 2 v1.5) and presentation materials.

●  	Hand over the final software package and conduct project closure review.

Deliverables:

●  	Executed acceptance test results (Report 5.5) and stabilised system deployment.

●  	Finalised project documentation (Report 7, Report 2 v1.5) and presentation package.

●  	Complete source code, documentation and deployment handover.

### 2. Quality Management

Quality is managed iteratively by embedding defect prevention, peer review and multi-level testing within every iteration rather than deferring quality checks to the end. Defects identified at any stage are logged, prioritised by severity and tracked to resolution in Jira against the project quality targets.

#### 2.1 Defect Prevention

●  	Establish shared coding, branching and definition-of-done standards from project start.

●  	Validate architecture boundaries and high-risk features early through feasibility studies.

●  	Enforce automated formatting, static analysis, compiler warnings-as-errors and recurring-defect checklists in CI.

#### 2.2 Reviewing

●  	Review iteration requirements and acceptance criteria with the Product Owner, BA and Test Lead prior to design.

●  	Review design updates for consistency against earlier increments and review all report drafts prior to submission.

●  	Require pull-request review and lead approval against the definition of done before merging any code.

#### 2.3 Unit Testing

●  	Developers write unit tests alongside feature code covering normal, failure and boundary cases, targeting at least 70% coverage of core backend logic.

●  	Automated unit tests and frontend type/lint checks run on every pull request in CI, with results recorded in Report 5.1.

#### 2.4 Integration Testing

●  	Verify frontend–backend API contracts, authorisation rules and cross-module workflows, including audio-to-AI pipelines.

●  	Run automated integration and regression suites across increments and record results in Report 5.2.

#### 2.5 System Testing

●  	Execute end-to-end functional workflows and non-functional checks on the staging environment once feature complete.

●  	Record functional and non-functional results in Reports 5.3 and 5.4; resolve all critical defects before deployment.

#### 2.6 Acceptance Testing

●  	Perform independent manual acceptance testing on staging against baselined criteria using prepared test data (Report 5.5).

●  	Conduct formal acceptance with the Product Owner and ensure no critical or major defects remain open before handover.

### 3. Project Training Plan

| Training Area | Participants | When, Duration |
| --- | --- | --- |
| Iterative Incremental process, work breakdown and Jira (work items, iteration board, defect workflow) | All members | Week 1: 1 day |
| Git branching model and the pull-request review workflow on GitHub | All members | Week 1: 1 day |
| Figma and the UI mock-up convention used across the project | All members, led by KhanhPG | Week 1: 1 day |
| WebRTC and media-server fundamentals, in preparation for FE-08 | All members, led by LocHD | Week 1: 2 days (feasibility study); Week 7: 1 day before Iteration 4 |
| Speech-to-text and speaker diarisation for Vietnamese audio, in preparation for FE-10 | HaiTV, LocHD, HuyNQ | Week 1: 2 days (feasibility study); Week 5: 1 day before Iteration 3 |
| Test case design, test levels and the Report 5 templates | All members, led by HuyNT | Week 3: 2 days |
| ASP.NET Core 10 Minimal API, Entity Framework Core 10 and the Domain / Application / Infrastructure / Web project structure, with the backend coding convention | All members | Week 1-2: 3 days |
| React 19, TypeScript, Vite and React Router, with the feature-based frontend structure | All members | Week 1-2: 3 days |
| PostgreSQL 17 with EF Core code-first migrations, and Redis for refresh tokens | All members | Week 2: 2 days |

### 4. Responsibility Assignments

The team consists of five members working under one supervisor. The supervisor's role is academic: he guides the team and reviews its work, and does not take the Product Owner role. The Product Owner is Dinh Van Toi of TrustSoft, who decides the product requirements and their priority, confirms the acceptance criteria and accepts the product. The team leader also leads the front end; the Business Analyst owns the requirements and the project documentation; and the Backend Lead and the Test Lead own the back end and testing respectively. Because five members cannot cover fifteen active features in specialised silos, and because every iteration passes through requirements, design, implementation and testing within two weeks, each member works across more than one phase and writes code in both layers. A lead role therefore means accountability for the decisions, the review and the quality of that area, not exclusive ownership of the work in it.

Figure 2: Project organisational chart

View the full-size image on Google Drive

| # | Member | Roles |
| --- | --- | --- |
| 1 | Nguyen Quang Hung | Supervisor (Software Engineering Department, FPT University); academic guidance and review of the team's work |
| 2 | Dinh Van Toi | Product Owner (TrustSoft); decides the product requirements and their priority, confirms the acceptance criteria and accepts each increment and the final product |
| 3 | Tran Viet Hai | Team Leader, Frontend Lead; Member (Frontend, Backend) |
| 4 | Pham Gia Khanh | Business Analyst, UI/UX; Member (Frontend) |
| 5 | Hoang Duc Loc | Backend Lead; Member (Backend) |
| 6 | Nguyen Truong Huy | Test Lead, Quality Assurance; Member (Frontend, Test) |
| 7 | Nguyen Quang Huy | Architecture, Member (Backend, Frontend) |

Responsibilities for the work packages listed in Section I.3 are assigned with a RACI chart, in which:

●  	R - Responsible - The member or members who carry out the task and produce the deliverable. Every task has at least one responsible member, and may have several.

●  	A - Accountable - The member who ensures the task is completed properly and on time, delegates the work, sets expectations and reviews the result. Every task has exactly one accountable member.

●  	C - Consulted - Those whose advice, input or feedback is sought before the task is considered done, because the outcome affects their own area.

●  	I - Informed - Those who are kept up to date on progress and results but take no decision and do no work on the task.

The responsibility assignment for this project is therefore as follows.

RACI Chart: R~Responsible, A~Accountable, C~Consulted, I~Informed

| Responsibility | HaiTV | KhanhPG | LocHD | HuyNQ | HuyNT |
| --- | --- | --- | --- | --- | --- |
| Documents and Reports |  |  |  |  |  |
| Report 1: Project Introduction | C | A/R | C | C | C |
| Report 2: Project Management Plan | A/R | C | C | C | C |
| Report 3: Software Requirement Specification | R | A/R | R | R | C |
| Report 4: Software Design Specification | A/R | R | R | R | I |
| Report 5: Test Documentation and test reports | C | R | R | R | A/R |
| Report 6: Software User Guides | A/R | R | R | R | R |
| Report 7: Final Project Document | A/R | R | R | R | R |
| Presentation slides and demo | A/R | R | R | R | R |
| Analysis and Design |  |  |  |  |  |
| System architecture design | C | C | R | A/R | I |
| Use case diagram and main workflow | R | A/R | R | R | C |
| Database design | C | I | A/R | R | I |
| Class, sequence and package diagrams | R | R | A/R | R | I |
| UI design and mock-up | C | A/R | I | R | C |
| Audio processing and AI summary design (FE-09, FE-10) | C | I | A/R | R | C |
| Online meeting room design (FE-08) | C | R | A/R | R | C |
| Implementation |  |  |  |  |  |
| Decide the technology and tools to be used | A | C | R | C | C |
| Coding convention and definition of done | C | R | A/R | R | R |
| Frontend implementation | A/R | R | R | R | R |
| Backend implementation | R | R | A/R | R | R |
| Source code control and pull-request review | R | I | A/R | R | I |
| CI pipeline and deployment to the staging environment | R | I | A/R | R | C |
| Keeping track of development work in Jira | A/R | R | R | R | R |
| Test |  |  |  |  |  |
| Test plan and test strategy | C | I | I | I | A/R |
| Unit test cases and execution | R | R | R | R | A |
| Integration and regression test cases and execution | I | R | R | R | A/R |
| System test cases and execution | C | R | R | R | A/R |
| Acceptance test scripts and support | R | R | R | R | A/R |
| Defect logging, triage and tracking | R | R | R | R | A/R |
| Project Management |  |  |  |  |  |
| Iteration planning and work allocation | A/R | R | R | R | R |
| Iteration evaluation and re-planning | A/R | R | R | R | R |
| Communication with the supervisor | A/R | I | C | I | I |
| Risk monitoring and change request handling | A/R | C | C | C | C |
| Document and configuration management | R | A/R | R | R | R |

### 5. Project Communications

| Communication Item | Who/ Target | Purpose | When, Frequency | Type, Tool, Method(s) |
| --- | --- | --- | --- | --- |
| Team Progress Meeting | All development team members | Report progress on the tasks of the current iteration, raise blockers early, agree who picks up what next, and keep the Jira board matching reality. | Three times a week (Mon, Wed, Fri), 21:00 - 21:30 | Online meeting: Google Meet; board updated in Jira |
| Iteration Planning Meeting | All team members | Open the planning phase of an iteration: set the iteration goal, select the features it will deliver, break them into tasks, estimate them and allocate the work using the effort measured in the previous iteration. | First day of each iteration (2 - 3 hours) | Google Meet or on campus; Jira |
| Requirement Confirmation Meeting | Product Owner (Dinh Van Toi), Business Analyst (KhanhPG), Test Lead (HuyNT), the members building the feature | Close the requirements phase: walk through the detailed requirements written for the current iteration and agree their acceptance criteria before design starts. The confirmed requirements become the baseline of the iteration. | Within the first three days of each iteration (1 - 2 hours) | Google Meet or on campus; Report 3 in Google Docs |
| Design Review Meeting | Backend Lead (LocHD), Frontend Lead (HaiTV), Business Analyst (KhanhPG), Test Lead (HuyNT) | Close the analysis and design phase: review the data model, API contract and screens for the iteration, and check them against the design of the increments already delivered. | After the requirements phase of each iteration (1 - 2 hours) | Google Meet; Report 4 and draw.io |
| Iteration Evaluation Meeting | All team members; Product Owner (Dinh Van Toi) for the increment demonstration | Close the iteration: demonstrate the increment to the Product Owner, assess it against the iteration goal, review the effort consumed, the defects found and the unfinished items, and agree what changes in the next iteration. | Last day of each iteration (1 - 2 hours) | Google Meet or on campus; Jira, notes in Google Docs |
| Supervisor Meeting | Supervisor (HungNQ), all team members | Report progress against the plan, demonstrate the latest increment, raise technical problems and risks, and obtain academic guidance and feedback on the report drafts. The supervisor advises and reviews; he does not decide the product requirements. | Weekly, at the time agreed with the supervisor | On campus or Google Meet; minutes in Google Docs |
| Technical Discussion | The members concerned, led by the relevant technical lead | Resolve a design or integration question that is too detailed for the progress meeting - an API contract, a database change, a media or audio-pipeline problem. | As needed, called by any member | Google Meet; decision recorded in the Jira task or the design document |
| Document Review | Business Analyst (KhanhPG) and the members who wrote the section | Review a report version for content, consistency with Report 1 and Report 3, and template compliance before it is submitted. | Before every report submission | Pull-request review on GitHub and comments on Google Drive; Change Log updated in the document |
| Quick Communication | All team members, supervisor when needed | Ask a short question, share a link or a status update, and get fast feedback on a small issue. | Any time | Messenger, Zalo |

### 6. Configuration Management

#### 6.1 Document Management

All project documents are maintained in a shared Google Drive folder organised by deliverable type, with edit access restricted to team members and view/comment access for the supervisor.

Report sources (.docx and .xlsx) are version-controlled in the Git repository alongside Markdown snapshots for pull-request review, then published to Drive in place so links remain unchanged.

Each document follows semantic versioning (v0.9 for draft, v1.0+ for submitted baselines) and records revisions in its Change Log. Changes impacting scope, schedule or effort require formal approval before updating baselines, under the accountability of the Business Analyst.

#### 6.2 Source Code Management

All application code, migrations, deployment configs and report sources reside in a single GitHub repository using a two-branch model with short-lived task branches:

●  	app - Main integration branch; every pull request runs CI checks and merges auto-deploy to the staging server.

●  	main - Frozen demonstration branch holding the verified release for council review.

●  	task branches - Short-lived branches created from app per task and deleted after merge.

Code enters app only via reviewed pull requests linked to Jira issues with passing CI builds and lead approval; direct pushes are disabled and secrets are excluded from version control.

Merge conflicts are resolved by the branch owner with support from the relevant technical lead, while the Backend Lead oversees repository integrity and branch protection.

#### 6.3 Tools & Infrastructures

| Category | Tools / Infrastructure |
| --- | --- |
| Technology | Backend: ASP.NET Core 10 (C#, Minimal API) in four projects (Domain, Application, Infrastructure, Web), JWT bearer authentication and Google sign-in (Google.Apis.Auth). Frontend: React 19, TypeScript, Vite and React Router. Real-time: SignalR for notifications; a self-hosted LiveKit SFU provides the WebRTC audio, video and screen-sharing room, with backend-authorized access and Chair controls (FE-08, FE-13). Email: Resend. |
| Database | PostgreSQL 17 through Entity Framework Core 10 (Npgsql), code-first migrations; Redis 8 for refresh tokens; SeaweedFS (self-hosted, S3-compatible) for document files |
| AI model / speech processing | Speech: Vietnamese transcription, speaker attribution and Voice ID on a self-hosted CPU-only speech worker (Python, sherpa-onnx). AI draft of the minutes: a self-hosted language model (Ollama) on the team's machines, with a backup machine; no outside AI service is called. The model is recorded with each result; consent, retention and erasure safeguards apply. |
| IDEs / Editors | Visual Studio Code; Visual Studio or JetBrains Rider for the backend; a shared .editorconfig keeps formatting consistent across IDEs |
| Diagramming | draw.io (architecture and UML diagrams), Figma (UI mock-up) |
| Documentation | Microsoft Word and Excel (.docx and .xlsx report sources kept in the repository), Google Drive (published copies and supervisor comments) |
| Version Control | GitHub (source code and report sources), Google Drive (published reports) |
| CI / Build | GitHub Actions: build and test of the backend (with PostgreSQL and Redis services), lint, unit tests and build of the frontend, on every pull request and every push to app or main; each push to app or main is then deployed to its own server |
| Development environment | .NET 10 SDK, Node.js 22, Docker Compose (PostgreSQL 17 and Redis 8 for local development) |
| Deployment server | Two AWS Lightsail instances running Docker Compose behind Caddy, one for app and one for the frozen main demonstration. The speech worker runs on a team machine over Tailscale with a backup worker on the server, and the language model on team machines over Tailscale with a backup machine. |
| Project management | Jira (iteration planning, task, defect and issue management) |
| Communication | Google Meet (team and supervisor meetings), Messenger and Zalo (quick communication) |
