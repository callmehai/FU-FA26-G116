# Report 2 – Project Management Plan

> **GENERATED — DO NOT EDIT.** Snapshot từ DOCX local. Sửa [`source/Report_2_Project_Management_Plan.docx`](source/Report_2_Project_Management_Plan.docx) rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report 2`. Sửa tay file này sẽ mất ở lần sync sau. Chi tiết: [08-reports/README.md](README.md).

CAPSTONE PROJECT REPORT

Report 2 – Project Management Plan

– Hanoi, Sep 2026 –

Table of Contents

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 19/09/2026 | A | LocHD | Add Project Overview and Management Approach - initial version 1.0 |
| 27/09/2026 | M, D | LocHD | Remove FE-12 Task & Action Item Lifecycle Management and renumber FE-13 to FE-15 as FE-12 to FE-14 |
| 05/10/2026 | M, D | KhanhPG | Synchronize the project plan with the revised FE-01 to FE-16 baseline: remove Department & Team Management, split the former Meeting & Schedule Management scope into FE-04 through FE-07, and renumber all later features consistently with Reports 1 and 3. |
| 05/10/2026 | M | KhanhPG | Synchronize the project plan with the PO-confirmed Master UC baseline: broaden FE-08 to include a personal document library and document reuse in meetings; make Secretary the meeting-audio uploader; align attendance, summary generation, audit viewing, document comments and minutes revision with Reports 1 and 3. |
| 08/10/2026 | M, D | KhanhPG | Align planning baseline: FE-04 removed, FE-05 becomes Meeting Management; schedule and voting are meeting-level, with one minutes workflow per Meeting. Preserve committed effort and dates. |
| 09/10/2026 | M, D | KhanhPG | Renumber active features FE-01 to FE-15; limit voting to InProgress Meetings and Chair conclusions; remove time-boxed voting, quorum and separate Resolution scope; align supporting interfaces and audio privacy safeguards. Preserve the committed schedule and effort. |
| 13/10/2026 | M | HaiTV | Align iteration plan with ADR-0018 (no document versions; library and meetings reference one immutable file; library folders; browser-side preview) and with ADR-0016/ADR-0017 for voting (the Chair selects eligible voter roles; verified Guests vote; ending the Meeting does not require closing Votes). |
| 30/10/2026 | M | HuyNQ | Project Management Plan v1.2: Fix review findings - Fifteen features in risk R04, EX defined as Exception as in Report 3, add Cancelled to the Meeting lifecycle, close the [TBD] items (AI model environment per ADR-0026, deployment server per ADR-0015), rewrite the R03 mitigation and lower its probability to Low now that the model host is decided, remove "versioning" from Iteration 2 (ADR-0018), align the Report 3 baseline reference with its 29/10/2026 Change Log, and fix the Table of Contents (add Definition and Acronyms, point I. Project Overview to its own heading). |

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

This Project Management Plan defines how group SEP490-G116 organises, executes, monitors and controls the development of TrustCabinet for TrustSoft over the fifteen-week capstone period from 14 September 2026 to 27 December 2026. It establishes the work packages, committed effort and schedule, quality objectives, responsibilities, communication and configuration practices. The product scope is aligned with Report 1 and the latest Report 3 SRS baseline (Change Log of 29/10/2026). Later scope changes require the established change-control process and a Change Log entry.

The work to be implemented consists of the fifteen active major features baselined in Report 1, Section V.1, each of which is traceable to a gap identified in Report 1, Section III:

●  	Identity and access - FE-01 Authentication & Account Security, FE-02 User & Profile Management, FE-03 Role-Based Access Control.

●  Meeting operations - FE-04 Meeting Management, FE-05 Agenda Management, FE-06 Meeting Participant Management, FE-07 Personal Document Library & Meeting Document Management, FE-08 Online Meeting Room Management.

●  Meeting intelligence - FE-09 Meeting Audio Upload & Media Library, FE-10 Speaker-Attributed Transcription & Meeting Summary.

●  Meeting governance - FE-11 Voting Management and FE-12 Meeting Minutes & Digital Signing.

●  Communication and visibility - FE-13 Email & In-App Notification, FE-14 Personal Dashboard & Meeting Workspace, FE-15 Reporting & Statistics.

Each feature is elaborated into detailed requirements in Report 3 - Software Requirement Specification during the iteration in which it is built, and is broken down into tasks in the Project Tracking workbook. The mapping between a feature and the iteration that delivers it is given in Section I.3 - Cost & Time Estimations and in Section II.1 - Project Process/Methodology.

The exclusions LI-01 to LI-12 in Report 1 remain outside the effort and schedule: automatic/server-side recording; real-time transcription, captions or in-meeting AI; video processing; translation and additional languages; bridges to external conferencing platforms; certificate-authority signatures; HRM, ERP, calendar or chat integrations and a public API; large-scale or advanced media features; conversation analytics; a native mobile client; multi-tenancy; and task assignment, action-item tracking or deadline management. Google sign-in, SMTP, the planned self-hosted LiveKit room service and the selected speech/AI provider are supporting interfaces rather than a general integration layer. The active SRS also excludes a Meeting Session layer, time-boxed voting, voting deadlines, quorum and a separate Resolution object. Any proposed addition follows Section II.6.1 instead of being absorbed into the committed schedule.

TrustCabinet is a standalone capstone project. It is not a sub-project of, and carries no delivery dependency on, any other project, and it reuses no existing code base: every component listed above is built by the team within the fifteen weeks planned here. Its dependencies are documentary rather than technical. This plan is derived from Report 1 - Project Introduction, which baselines the problem, the gaps and the product scope; it drives Report 3 - Software Requirement Specification and Report 4 - Software Design Specification, which elaborate each feature in the iteration that delivers it; and it defines the test scope planned in Report 5 - Test Documentation. The work packages, effort figures and deadlines of Section I.3 are mirrored and tracked at task level in the Project Tracking workbook and in Jira.

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
| 3 | The team consists of five part-time student members who carry other courses in parallel. There is no full-time developer and no dedicated designer or DevOps engineer. | Resources |
| 4 | The Product Owner is Dinh Van Toi of TrustSoft. Product requirements, priorities and acceptance criteria are confirmed by the Product Owner, and any change to the product scope requires his approval. The supervisor provides academic guidance and reviews the team’s work, and does not take the Product Owner role or approve product requirements. | External Interfaces |
| 5 | The core application, relational database, documents and planned LiveKit meeting-room service must support deployment inside TrustSoft. Optional Google sign-in must coexist with email/password sign-in; SMTP is configured for outgoing mail. The speech-processing and summarisation environment remains undecided and may use a self-hosted model service or an external AI/ASR API behind one replaceable internal interface. An external provider may use uploaded audio and transcripts only for the stated processing purpose and must not retain or reuse them. Core self-hosting does not imply that every supporting service runs on-premises. | Technology |
| 6 | Version 1 operates in Vietnamese only, with no translation and no subtitles (Report 1, LI-04). | Scope |
| 7 | The system performs no automatic recording. Meeting audio is uploaded afterwards by the assigned Secretary and processed only once the meeting has ended (Report 1, LI-01 and LI-02). | Scope / Security |
| 8 | Meeting minutes are signed with internal electronic signatures only. Certificate-authority signatures issued on a USB token are out of scope (Report 1, LI-06). | Scope |
| 9 | The technology stack is fixed in the source code repository: ASP.NET Core 10 (C#) for the backend, React 19 with TypeScript and Vite for the frontend, PostgreSQL 17 through Entity Framework Core 10 as the database and Redis for refresh tokens (Section II.6.3). The speech-processing and summarisation model runs in-house in a separate CPU-only worker (ADR-0026, decided by the Product Owner on 23/10/2026). | Technology |

### 3. Cost & Time Estimations

Effort is expressed in eight-hour person-days (pds). The 210 pds committed below are the effective working capacity derived in Section I.4; the 52.5 pds buffer remains unallocated. The total retains 14 pds for initial planning and 28 pds for each of six iterations and the deployment phase. The fifteen active features are FE-01 through FE-15, aligned with the Report 3 SRS baseline of 29/10/2026. Consolidating the Meeting lifecycle and narrowing voting changes work-package content; the committed effort, iteration dates and closing milestone remain unchanged.

| # | Work Package | Effort<br>(pds) | Due Date |
| --- | --- | --- | --- |
| 0 | Initial Planning (week 1)<br>14/09/2026 - 20/09/2026 | 14 |  |
| 0.1 | Report 1: Project Introduction v0.9 | 3 | 20/09/2026 |
| 0.2 | Technical feasibility study: virtual meeting room and speaker-attributed transcription (proof of concept) | 4 | 20/09/2026 |
| 0.3 | Overall architecture design, repository and development environment setup | 4 | 20/09/2026 |
| 0.4 | UI mock-up for the Iteration 1 scope | 3 | 20/09/2026 |
| 1 | Iteration 1: Identity & Access Foundation - FE-01 to FE-03 (weeks 2-3)<br>21/09/2026 - 04/10/2026 | 28 |  |
| 1.1 | Report 1: Project Introduction v1.0 | 1 | 27/09/2026 |
| 1.2 | Report 2: Project Management Plan v1.0 | 3 | 27/09/2026 |
| 1.3 | Report 3: SRS v0.9 (overall requirements) | 3 | 04/10/2026 |
| 1.4 | FE-01 Authentication & Account Security | 4 | 04/10/2026 |
| 1.5 | FE-02 User & Profile Management | 4 | 04/10/2026 |
| 1.6 | FE-03 Role-Based Access Control - role & permission model | 3 | 04/10/2026 |
| 1.7 | FE-03 Role-Based Access Control - permission configuration & audit | 5 | 04/10/2026 |
| 1.8 | Code package & Report 5.1 Unit Test | 3 | 04/10/2026 |
| 1.9 | Software Package v1.0 (increment 1) | 2 | 04/10/2026 |
| 2 | Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-07, FE-13 (weeks 4-5)<br>05/10/2026 - 18/10/2026 | 28 |  |
| 2.1 | Report 2: Project Management Plan v1.1 | 2 | 11/10/2026 |
| 2.2 | Report 3: SRS v1.0 (detailed requirements for Iterations 1-2) | 3 | 18/10/2026 |
| 2.3 | Report 4: SDS v1.0 (high level design specifications) | 3 | 18/10/2026 |
| 2.4 | Report 5.0: Test Documentation (test planning) | 2 | 18/10/2026 |
| 2.5 | FE-04 to FE-06 Meeting Management, Agenda Management & Meeting Participant Management (including the meeting approval workflow) | 6 | 18/10/2026 |
| 2.6 | FE-07 Personal Document Library & Meeting Document Management | 3 | 18/10/2026 |
| 2.7 | FE-13 Email & In-App Notification | 4 | 18/10/2026 |
| 2.8 | Code package & Report 5.1 Unit Test | 3 | 18/10/2026 |
| 2.9 | Software Package v2.0 (increment 2) | 2 | 18/10/2026 |
| 3 | Iteration 3: Meeting Audio & AI Processing - FE-09, FE-10 (weeks 6-7)19/10/2026 - 01/11/2026 | 28 |  |
| 3.1 | Report 3: SRS v1.1 (detailed requirements for Iteration 3) | 3 | 25/10/2026 |
| 3.2 | Report 4: SDS v1.1 (detailed design of the audio processing pipeline) | 3 | 25/10/2026 |
| 3.3 | FE-09 Meeting Audio Upload & Media Library - Secretary post-meeting upload | 5 | 01/11/2026 |
| 3.4 | FE-10 Speaker-Attributed Transcription & Meeting Summary | 7 | 01/11/2026 |
| 3.5 | Report 5.2: Integration Test cases and Integration Test report | 3 | 01/11/2026 |
| 3.6 | Code package & Report 5.1 Unit Test | 3 | 01/11/2026 |
| 3.7 | Report 2: Project Management Plan v1.2 | 2 | 25/10/2026 |
| 3.8 | Software Package v3.0 (increment 3) | 2 | 01/11/2026 |
| 4 | Iteration 4: Online Meeting Room Management - FE-08 (weeks 8-9)02/11/2026 - 15/11/2026 | 28 |  |
| 4.1 | Report 3: SRS v1.2 (detailed requirements for Iteration 4) | 3 | 08/11/2026 |
| 4.2 | Report 4: SDS v1.2 (detailed design of the meeting room and media flow) | 3 | 08/11/2026 |
| 4.3 | FE-08 Online Meeting Room Management | 11 | 15/11/2026 |
| 4.4 | Report 5.2: Integration Test cases and Integration Test report | 3 | 15/11/2026 |
| 4.5 | Code package & Report 5.1 Unit Test | 4 | 15/11/2026 |
| 4.6 | Report 2: Project Management Plan v1.3 | 2 | 08/11/2026 |
| 4.7 | Software Package v4.0 (increment 4) | 2 | 15/11/2026 |
| 5 | Iteration 5: Meeting Governance - FE-11, FE-12 (weeks 10-11)16/11/2026 - 29/11/2026 | 28 |  |
| 5.1 | Report 3: SRS v1.3 (detailed requirements for Iteration 5) | 3 | 22/11/2026 |
| 5.2 | Report 4: SDS v1.3 | 3 | 22/11/2026 |
| 5.3 | FE-11 Voting Management - in-meeting voting, tallies and Chair conclusions | 6 | 29/11/2026 |
| 5.4 | FE-12 Meeting Minutes & Digital Signing - meeting-level generation, signing, revision and sealing | 7 | 29/11/2026 |
| 5.5 | Report 5.2 and 5.3: Integration Test cases and System Test cases | 4 | 29/11/2026 |
| 5.6 | Code package & Report 5.1 Unit Test | 4 | 29/11/2026 |
| 5.7 | Software Package v5.0 (increment 5) | 1 | 29/11/2026 |
| 6 | Iteration 6: Workspace & Reporting - FE-14, FE-15 (weeks 12-13)30/11/2026 - 13/12/2026 | 28 |  |
| 6.1 | FE-14 Personal Dashboard & Meeting Workspace | 6 | 13/12/2026 |
| 6.2 | FE-15 Reporting & Statistics | 6 | 13/12/2026 |
| 6.3 | Report 5.3 and 5.4: System Test cases and System Test report (FRs and NFRs) | 5 | 13/12/2026 |
| 6.4 | Report 6: Software User Guides (installation guide, user manual) | 5 | 13/12/2026 |
| 6.5 | Code package & Report 5.1 Unit Test | 3 | 13/12/2026 |
| 6.6 | Report 2: Project Management Plan v1.4 | 1 | 06/12/2026 |
| 6.7 | Software Package v6.0 (increment 6 - feature complete) | 2 | 13/12/2026 |
| 7 | Deployment: Acceptance, Stabilisation & Project Closure (weeks 14-15)<br>14/12/2026 - 27/12/2026 | 28 |  |
| 7.1 | Report 5.5: Acceptance Test scripts and acceptance test support | 5 | 20/12/2026 |
| 7.2 | Defect fixing and system stabilisation | 7 | 27/12/2026 |
| 7.3 | Report 7: Final Project Document | 5 | 27/12/2026 |
| 7.4 | Presentation file and demo rehearsal | 4 | 27/12/2026 |
| 7.5 | Final software package (source code, documents, deployment package) | 5 | 27/12/2026 |
| 7.6 | Report 2: Project Management Plan v1.5 (final update) | 2 | 27/12/2026 |
|  | TOTAL | 210 |  |

### 4. Capacity Planning

| Item | Value |
| --- | --- |
| Hours per person per week | 28 h (4 h/day x 7 days) |
| Team total per week | 5 people x 28 h = 140 h/week |
| Total duration | 15 weeks (14/09/2026 - 27/12/2026) |
| Gross capacity | 140 h/week x 15 weeks = 2,100 h = 262.5 person-days (8 h/person-day) |
| Buffer (meetings, reviews, unplanned - 20%) | 420 h = 52.5 person-days |
| Effective working capacity | 1,680 h = 210 person-days |

The buffer of 20% covers the time the team spends in meetings, reviews and retrospectives, the rework that follows a review, and unplanned absence caused by the other courses every member carries in parallel. Because it is excluded before the work packages of Section I.3 are planned, an overrun inside an iteration is absorbed by the buffer rather than pushing unfinished work into the next one.

### 5. Project Objectives

| # | Metric | Unit | Planned | Actual | Notes / References |
| --- | --- | --- | --- | --- | --- |
| 1 | Effort Usage | Person-day | 210 |  | Effective working capacity from Section I.4; distributed over the work packages of Section I.3 and tracked per member in the Project Tracking workbook, sheet Scope |
| 2 | Review Defects | No of defects | 30 |  | Defects found in document and code reviews, before any test is executed. A higher count is a good result: it means defects are being caught at the earliest stage |
| 3 | Unit Test Defects | No of defects | 40 |  | Written and executed by the developer who owns the module, recorded in Report 5.1 - Unit Test |
| 4 | Integration Test Defects | No of defects | 30 |  | Found when modules and APIs are combined and exercised together, recorded in Report 5.2 - Integration Test |
| 5 | System Test Defects | No of defects | 15 |  | Found when the whole system is exercised against the functional and non-functional requirements, recorded in Reports 5.3 and 5.4 |
| 6 | Acceptance Test Defects | No of defects | 5 |  | Found during acceptance testing in the deployment phase, recorded in Report 5.5. No critical defect may remain open at closing |
| 7 | Timeliness | % | 90 |  | Total on-time deliverables / Total project deliverables |
| 8 | Requirement Completeness | % | 90 |  | Implemented and accepted requirements / Requirements baselined in Report 3. Failed if <= 75% |
| 9 | Unit Test Code Coverage | % | 70 |  | Measured on the core business logic of the backend, excluding generated code and configuration. Checked in the testing phase of every iteration |

The Planned column is the commitment made at the start of the project. The Actual column is filled in at project closing in the deployment phase and reported in Report 7 - Final Project Document; a deviation of more than 20% on any metric is explained there.

### 6. Risks and Mitigations

Risks are reviewed in the evaluation phase of every iteration. Probability and impact are re-assessed from what that iteration actually showed, a risk that has materialised is converted into an issue with an owner and a deadline in the planning phase of the next iteration, and a risk that no longer applies is closed with a note in the Change Log.

| # | Risk | Prob | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R01 | The online meeting room (FE-08) is the team's first piece of real-time media work. Connection establishment, NAT traversal and multi-party audio and video quality may take substantially longer than estimated, putting the single largest work package of the plan at risk. | High | High | Run a feasibility study in the Initial Planning phase before the estimate is committed, and build on a proven open-source media server rather than on raw peer-to-peer connections. FE-08 is the only major feature of Iteration 4 so that the iteration carries slack for it. If the room is not demonstrable at the Iteration 4 evaluation, fall back to the reduced room agreed by the team in advance and recorded in Report 3: audio, screen sharing and chat, with a capped number of participants. |
| R02 | Speaker-attributed transcription of Vietnamese meeting audio (FE-10) produces output too inaccurate to summarise, making the meeting summary and the minutes built on it unusable. | Medium | High | Evaluate candidate models during the Initial Planning feasibility study against three to five of the team's own recorded meetings, agree an acceptance threshold within the team before implementation starts and record it in Report 3. Keep the transcript editable so that the Secretary can correct speaker labels before the summary is generated, which keeps the feature usable even at imperfect accuracy. FE-09 and FE-10 are scheduled in Iteration 3 rather than late in the project, so that the evaluation phase of that iteration still leaves time to change approach. |
| R03 | The environment in which the speech-processing and summarisation model runs was open when this plan was first written. The Product Owner decided on 23/10/2026 (ADR-0026) that it runs in-house in a separate CPU-only worker, so the remaining risk is the capacity of the deployment server, which is a 1 GB instance today and will move to a larger one. | Low | High | Move the deployment server to a 4 GB instance, or run the speech worker on a team machine that the server reaches over a private network connection (see the deployment guide). The application keeps working while the worker is off, and the functions that need it report MSG312. Keep transcription and summarisation behind an internal interface so the worker can change host without changing the rest of the system. Update Section II.6.3 when the host is fixed. |
| R04 | Fifteen features in fifteen weeks exceeds what five part-time members can build, because every member carries other courses in parallel. The risk surfaces late, as unfinished work carried from one iteration to the next. | High | High | Prioritise the active feature list with the Product Owner into must-have FE-01 to FE-13 and should-have FE-14 and FE-15, recording the agreed priority in Report 3. Measure effort consumed in each iteration evaluation and re-plan against actual effort. The 20% buffer in Section I.4 is excluded from committed work packages. Scope changes follow Section II.6.1 and the Change Log; retired Meeting Session and Resolution work must not be reintroduced silently. |
| R05 | Workload is distributed unevenly, or a member becomes unavailable at a critical point, leaving a module that only one person understands. | Medium | Medium | Give every work package one accountable owner and make sure at least two members have worked on each module, by pairing on the first task of every new module. Log effort per member in Jira and review it in the weekly supervisor meeting. Reassign tasks in the planning phase of the next iteration when the distribution drifts. |

## II. Management Approach

### 1. Project Process/Methodology

The team applies the Iterative Incremental model to this project. The system is not built in a single sequential pass, nor delivered in one release at the end: it is produced in a series of iterations, each of which runs through the full cycle of planning, requirements, analysis and design, implementation, testing and evaluation, and each of which adds a working increment to the product delivered by the previous one.

Two properties of TrustCabinet made this the right model. First, the two features that carry the most uncertainty - the online meeting room (FE-08) and speaker-attributed transcription of Vietnamese audio (FE-10) - cannot be estimated reliably from a document; the team only learns what they really cost by building them. A single sequential pass would expose that cost only after the design of the whole system had been frozen, whereas an iterative cycle exposes it at the end of the iteration that contains it, while there is still time to change approach. Second, the detailed requirements of the later features are expected to be refined as the Product Owner reviews each increment. A single sequential pass would have to freeze every requirement before the first line of code is written, whereas an iterative cycle elaborates only the features of the current iteration and absorbs a later correction one iteration at a time. Compared with a purely incremental delivery, in which each increment is added to a design fixed once at the start, revisiting analysis and design inside every iteration lets the team correct a design decision as soon as the previous increment has shown it to be wrong.

Figure 1: Phases of the Iterative Incremental Model

The project is organised into an initial planning phase, six iterations and a deployment phase. Initial planning is performed once, lasts one week and produces no increment: it sets up the environment, fixes the architecture and carries out the feasibility study on the two risky features. Iterations 1 to 6 last two weeks each and every one of them passes through the six phases of the model:

●  	Planning - set the goal of the iteration, select from Report 1, Section V.1 the features it will deliver, break them into tasks in Jira with an owner and an estimate, and allocate the work across the team using the effort actually consumed in the previous iteration.

●  	Requirements - elaborate the selected features into detailed, testable requirements, confirm them with the Product Owner, the members who will build them and the Test Lead, agree their acceptance criteria, and record them as the next version of Report 3 - Software Requirement Specification. Only the features of the current iteration are elaborated in detail; the rest remain at the level baselined in Report 1.

●  	Analysis & design - turn those requirements into a design: extend the data model, define the API contract and the screens, and record the result as the next version of Report 4 - Software Design Specification. A design decision taken in an earlier iteration is revisited here whenever a new requirement contradicts it, rather than being worked around.

●  	Implementation - build the increment on top of the one delivered by the previous iteration, following the coding convention and the definition of done agreed in initial planning. Every change enters the shared branch through a reviewed pull request.

●  	Testing - execute unit tests on the new modules, integration tests where the new increment meets the previous one, and, from Iteration 4 onwards, regression tests over the earlier increments. Results are recorded in the corresponding Report 5 workbook.

●  	Evaluation - demonstrate the increment to the Product Owner, assess it against the goal set in the planning phase, measure the effort actually consumed and the defects found, and record what is unfinished. The conclusions of this phase are the input to the planning phase of the next iteration.

Deployment is the final phase, in weeks 14 and 15. It takes the feature-complete increment produced by Iteration 6 through acceptance testing, defect fixing and stabilisation, and the handover of the final software package, the documentation and the presentation.

The feature order across the iterations is driven by technical risk and by dependency, not by the order in which the features are listed in Report 1. The identity and access features are built first because every other feature is scoped by a role; the meeting, document and notification core follows because the meeting record is the object that audio, votes and minutes all attach to, and its approval and publication flow already needs notifications to reach the approver and the invited participants. Audio upload and AI processing (FE-09, FE-10) are then brought forward to Iteration 3, ahead of the online meeting room (FE-08), precisely because they carry the highest uncertainty: if the transcription quality turns out to be unusable, the evaluation phase of Iteration 3 reveals it at the half-way point, while there is still time to change approach or reduce the feature. The online meeting room takes Iteration 4 on its own, the governance features follow in Iteration 5, and the supporting features that depend on everything else - the personal dashboard and reporting - are built last in Iteration 6.

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

●  	Set up the development environment, the GitHub repository and the Jira project, and agree the branching convention, the coding convention and the definition of done that every iteration will follow.

●  	Fix the overall system architecture and the boundary between the web application and the audio-processing component, so that the iterations extend a stable structure instead of renegotiating it.

●  Carry out the technical feasibility study on the two features that carry the most uncertainty: a minimal browser-to-browser meeting room (FE-08) and speaker-attributed transcription of a real Vietnamese meeting recording (FE-10).

●  	Agree within the team the acceptance threshold for transcription quality that Iteration 3 will be measured against, and record it in Report 3.

●  	Confirm the technology stack decision (Section II.6.3); the choice of environment for the speech-processing model is carried forward under risk R03.

●  	Produce the UI mock-up for the Iteration 1 scope and the initial feature list that the iterations will draw from.

●  	Prepare Report 1 - Project Introduction v0.9.

Deliverables:

●  	Development environment, repository and Jira project fully configured; conventions and definition of done agreed.

●  	Overall architecture document.

●  	Feasibility study results, with a recommendation and a measured accuracy figure for Vietnamese speaker-attributed transcription.

●  	UI mock-up for Iteration 1.

●  	Report 1 v0.9 completed.

Iteration 1: Identity & Access Foundation - FE-01 to FE-03 (2 weeks)

Time: 21/09/2026 - 04/10/2026

Iteration goal: Deliver the identity and access layer that every later feature is scoped by.

Phases of this iteration:

●  	Planning - Set the iteration goal, select FE-01 to FE-03 from the feature list, break them into Jira tasks with an owner and an estimate, and allocate the work against the capacity in Section I.4.

●  	Requirements - Elaborate FE-01 to FE-03 into detailed requirements: sign-in with email and password and with a Google account linked to an existing internal account; the password policy, session expiry and lockout rules; the account lifecycle from creation through deactivation and unlock; user profile management; and the role-permission matrix for the system actors - System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant and Guest. Recorded as Report 3 - SRS v0.9, together with the overall requirement set.

●  	Analysis & design - Fix the data model for user, role and permission; design the authentication flow and the audit-log record; and turn the mock-up into the screen set for this iteration.

●  	Implementation - Build FE-01 Authentication & Account Security, FE-02 User & Profile Management and FE-03 Role-Based Access Control on the architecture fixed in initial planning.

●  	Testing - Unit tests for each module, covering the normal, failure and boundary cases; manual verification of email and Google sign-in, lockout and of a permission check refusing a role that is not entitled. Results recorded in Report 5.1 - Unit Test.

●  	Evaluation - Demonstrate account creation, role assignment and a permission denial, and confirm the role model before any later feature is scoped against it. Record the effort consumed, the defects found and any unfinished task as input to the planning phase of Iteration 2.

Increment delivered: Software Package v1.0 - a running system in which a user can sign in and an administrator can manage accounts, roles and permissions. Reports delivered: Report 1 v1.0, Report 2 v1.0, Report 3 - SRS v0.9.

Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-07, FE-13 (2 weeks)

Time: 05/10/2026 - 18/10/2026

Iteration goal: Deliver the meeting record that audio, votes and minutes will all attach to, together with its approval workflow and the notifications that carry it to the people involved.

Phases of this iteration:

●  Planning - Set the iteration goal and select FE-04 to FE-07 and FE-13; re-estimate the remaining features using the effort actually consumed in Iteration 1.

●  Requirements - Elaborate one independent Meeting with planned schedule and lifecycle Draft, WaitingApproval, Rejected, Approved, Published, InProgress and Ended, with Cancelled for a Meeting that will not take place; the assigned approver reviews submission, the Organizer publishes after approval, and the Chair starts and ends execution. There is no Meeting Session layer. Define Secretary agenda preparation and participant/Guest coordination, invitation responses and official attendance. Agenda planning is allowed through InProgress and prohibited after Ended; Chair execution actions remain separate. Define the personal document library, two-source meeting attachments, preview, comments and notification events. Recorded as Report 3 SRS v1.0 with the latest approved baseline applied.

●  	Analysis & design - Extend the data model with the meeting, agenda item, approval cycle, lifecycle history, invitation, attendance, personal-document library, library folder, stored file, meeting attachment, document comment and notification entities; define ownership and access rules for personal documents separately from meeting-scoped documents; ensure that the personal library and meeting attachments only reference one immutable stored file, with a periodic cleanup of files no longer referenced; and design notification dispatch as an asynchronous job with retry. Recorded as Report 4 - SDS v1.0.

●  Implementation - Build FE-04 Meeting Management, FE-05 Agenda Management, FE-06 Meeting Participant Management, FE-07 Personal Document Library & Meeting Document Management and FE-13 Email & In-App Notification on top of increment 1. The increment includes manual attendance recording, My Documents, upload to My Documents, personal-library folders, adding meeting documents from either My Documents or the local computer, removing meeting documents, browser preview and document commenting.

●  	Testing - Unit tests for the new modules and integration tests where they meet the access control delivered in Iteration 1. The test plan itself is produced in this iteration as Report 5.0 - Test Documentation.

●  	Evaluation - Demonstrate scheduling a meeting, building its agenda, managing participants and recording attendance, uploading a document into a folder of My Documents, adding one meeting document from My Documents and another directly from the local computer, previewing and commenting on meeting documents, removing a document from the meeting and deleting one from My Documents, submitting the meeting for approval, approving and publishing it, and verifying the email and in-app notifications that follow.

Increment delivered: Software Package v2.0 - increment 1 plus meeting scheduling, the approval workflow, agenda and document management, and email and in-app notification. Reports delivered: Report 2 v1.1, Report 3 - SRS v1.0, Report 4 - SDS v1.0, Report 5.0 - Test Documentation.

Iteration 3: Meeting Audio & AI Processing - FE-09, FE-10 (2 weeks)

Time: 19/10/2026 - 01/11/2026

Iteration goal: Prove and deliver the part of the system that carries the most uncertainty, early enough to change approach if it fails.

Phases of this iteration:

●  Planning - Set the iteration goal and select FE-09 and FE-10. Because this is the riskiest iteration, the plan reserves time inside it for a second attempt with a different model if the first does not meet the threshold agreed in initial planning.

●  Requirements - Elaborate post-meeting, Secretary-only audio upload, audio formats and size limits, informed participant consent, stated purpose, retention dates and recorded erasure. Define editable Vietnamese transcripts and speaker attribution, explicit summary generation/regeneration, review, approval and publication. Processing begins only after the Meeting ends. Select self-hosted or external processing behind a replaceable interface; an external provider may not retain or reuse audio or transcripts. Recorded as Report 3 SRS v1.1.

●  Analysis & design - Design an asynchronous processing pipeline behind one replaceable internal provider interface, the media library, transcript editor, consent evidence, retention and erasure handling. Record the provider/model with each result and control any transfer to an external provider. Recorded as Report 4 SDS v1.1.

●  Implementation - Build FE-09 Meeting Audio Upload & Media Library and FE-10 Speaker-Attributed Transcription & Meeting Summary, with the assigned Secretary uploading the meeting audio after the meeting. Attach the uploaded file to the meeting record delivered by increment 2, connect the audio-upload request/reminder to FE-13, implement transcript correction, and support explicit Generate / Regenerate, Approve and Publish actions for the summary.

●  	Testing - Unit tests for upload handling and job processing; an integration test of the whole upload-to-summary flow, recorded in Report 5.2; and a measurement of transcription and summarisation quality on the team's own Vietnamese recordings against the agreed threshold.

●  	Evaluation - Compare the measured quality against the threshold set in initial planning and decide, before half of the project has been spent, whether the feature is delivered as specified, needs a different model, or must be reduced in scope. The decision and its reasoning are recorded in Report 3, reported to the supervisor at the weekly meeting, and become an input to the planning phase of Iteration 4.

Increment delivered: Software Package v3.0 - increment 2 plus audio upload, the media library, speaker-attributed transcription and the AI meeting summary with Secretary approval. Reports delivered: Report 2 v1.2, Report 3 - SRS v1.1, Report 4 - SDS v1.1, Report 5.2 - Integration Test.

Iteration 4: Online Meeting Room Management - FE-08 (2 weeks)

Time: 02/11/2026 - 15/11/2026

Iteration goal: Deliver the meeting room, the single largest work package in the plan.

Phases of this iteration:

●  Planning - Set the iteration goal and select FE-08 alone, so that the whole iteration is available for it and carries slack. Re-confirm the reduced fallback scope agreed under risk R01 before implementation starts.

●  Requirements - Define what the room must do - audio, video, screen sharing, text chat, joining and leaving an existing meeting - the participant count it targets, and the explicit rule that the room itself records nothing. Official attendance is maintained by the Secretary, with online-room presence available only as supporting evidence/pre-fill, and the Secretary performs the separate FE-09 audio-upload flow after the meeting. Recorded as Report 3 - SRS v1.2.

●  	Analysis & design - Select and design around the media server; design signalling and the room lifecycle; capture room-presence evidence that can pre-fill, but does not replace, the official attendance record maintained by the Secretary; and link the room back to the meeting record built in Iteration 2. Recorded as Report 4 - SDS v1.2.

●  Implementation - Build FE-08 Online Meeting Room Management and connect joining/leaving presence to the meeting created in FE-04 so that the Secretary can use it as supporting evidence when recording official attendance.

●  	Testing - Unit tests; integration tests against the meeting record and the access control; a full-team session to measure audio and video quality at the target participant count; and the first regression pass over the increments of Iterations 1 to 3.

●  	Evaluation - Hold a real team meeting inside the room and measure it against the target participant count. Decide whether the full room is delivered as specified or the reduced fallback of R01 is applied, and record the decision.

Increment delivered: Software Package v4.0 - increment 3 plus a browser-based meeting room hosted by TrustCabinet itself. Reports delivered: Report 2 v1.3, Report 3 - SRS v1.2, Report 4 - SDS v1.2, Report 5.2 updated.

Iteration 5: Meeting Governance - FE-11, FE-12 (2 weeks)

Time: 16/11/2026 - 29/11/2026

Iteration goal: Turn in-meeting vote tallies and recorded Chair conclusions into signed, sealed meeting minutes.

Phases of this iteration:

●  Planning - Select FE-11 Voting Management and FE-12 Meeting Minutes & Digital Signing together because each Meeting has one minutes process incorporating its vote tallies and Chair conclusions. Preserve the allocated effort and dates.

●  Requirements - Limit Vote creation, opening and casting to an InProgress Meeting. A Vote belongs directly to the Meeting and does not require an Agenda Item. The current Chair creates/configures and opens/closes it; eligible voters – holders of the meeting roles the Chair selects for each Vote (Chair, Secretary, Participant, verified Guest) – may cast one immutable ballot. Support public and secret ballots. After closing, the Chair records Passed, NotPassed or Deferred; ending the Meeting does not require closing Votes, and the Chair may close an Open Vote after the Meeting ends. No time-boxed voting, deadline, quorum or separate Resolution object is delivered. Define minutes from meeting information, attendance, agenda outcomes, approved summary, vote tallies and Chair conclusions; retain signing, revision, invalidation of prior signatures after content changes and explicit Secretary finalization. Recorded as Report 3 SRS v1.3.

●  Analysis & design - Model Vote under Meeting with tally and Chair conclusion; keep it independent of Agenda Item and omit a separate Resolution entity. Design versioned minutes and signature records, sealed-content immutability, and the template combining approved records with vote results and conclusions. Connect voting calls and signing requests to FE-13 notifications. Recorded as Report 4 SDS v1.3.

●  Implementation - Build FE-11 Voting Management and FE-12 Meeting Minutes & Digital Signing.

●  Testing - Verify that voting is accepted only for an InProgress Meeting, only holders of the roles the Chair selected for that Vote may vote (verified Guests included when selected), one ballot cannot be changed, a conclusion requires a Closed Vote and End Meeting does not depend on Open Votes. Test the chain from vote tallies and Chair conclusion plus approved summary to versioned, signed and finalized minutes, including revision and signature invalidation. Record integration and system results in Reports 5.2 and 5.3.

●  Evaluation - Demonstrate the lifecycle in order: prepare and publish a Meeting; start it; hold voting, close each Vote and record Chair conclusions; end the Meeting; upload consented audio; review and approve the summary; generate minutes with the tallies and conclusions; sign and finalize them.

Increment delivered: Software Package v5.0 - increment 4 plus in-meeting voting, recorded Chair conclusions and electronically signed meeting minutes. Reports delivered: Report 3 v1.3, Report 4 v1.3, Reports 5.2 and 5.3.

Iteration 6: Workspace & Reporting - FE-14, FE-15 (2 weeks)

Time: 30/11/2026 - 13/12/2026

Iteration goal: Make everything already built visible and actionable, and reach feature completeness.

Phases of this iteration:

●  Planning - Set the iteration goal and select FE-14 and FE-15. Both depend on the earlier increments, which is why they are planned last: there is nothing to display or count until the meeting, audio and governance features exist.

●  	Requirements - Define the content of the personal workspace and the figures the reports must produce, their breakdowns by project or other applicable reporting scope and their export format. Recorded as the final detailed version of Report 3.

●  	Analysis & design - Design the workspace query across meetings, approvals, documents, minutes and votes, and the aggregation behind the reporting figures.

●  Implementation - Build FE-14 Personal Dashboard & Meeting Workspace and FE-15 Reporting & Statistics, completing the feature set baselined in Report 1.

●  	Testing - Unit and integration tests for the new modules, followed by full system testing of the complete product against the functional requirements (Report 5.3) and the non-functional requirements (Report 5.4), and a regression pass over every previous increment.

●  	Evaluation - Demonstrate the feature-complete system and confirm that every requirement baselined in Report 3 is covered. The remaining open defects are recorded and handed to the deployment phase.

Increment delivered: Software Package v6.0 - feature complete. Reports delivered: Report 2 v1.4, Report 5.3 and 5.4 - System Test, Report 6 - Software User Guides.

Deployment: Acceptance, Stabilisation & Project Closure (2 weeks)

Time: 14/12/2026 - 27/12/2026

Objectives:

●  	Run acceptance testing against the scripts in Report 5.5: the Test Lead coordinates the session and each script is executed by a member who did not build the feature under test, working from a prepared demonstration data set that includes real Vietnamese meeting audio.

●  	Record the acceptance result as the state of the product against the requirements baselined in Report 3, and hold the formal acceptance session with the Product Owner, Dinh Van Toi, on the same scripts.

●  	Fix the defects raised in system and acceptance testing and stabilise the system; no critical defect may remain open at closing.

●  	Deploy the final package to the target environment and verify the installation against Report 6 - Software User Guides.

●  	Finalise Report 7 - Final Project Document and the final version of every other report, including Report 2 v1.5, and fill in the Actual column of the metrics in Section I.5.

●  	Prepare and rehearse the presentation and the live demonstration.

●  	Hand over the complete source code, documents and deployment package, and close the project with a final review of what the six iterations produced and the lessons learned.

Deliverables:

●  	Report 5.5: Acceptance Test scripts executed and the acceptance result recorded.

●  	All critical and major defects closed; the system stabilised and deployed.

●  	Report 7 - Final Project Document and Report 2 v1.5 submitted.

●  	Presentation file delivered and the demonstration rehearsed.

●  	Final software package - source code, documents and deployment package - handed over. Project officially closed.

### 2. Quality Management

Quality is managed on the principle that a defect is cheapest to remove in the phase that created it. Because the project is built iteratively, every iteration contains its own prevention, review and testing activity rather than deferring quality work to a single test stage at the end: a defect introduced in one increment is found while that increment is still the one being worked on. The team therefore spends effort on prevention and review before a line of test code is written, and then applies four levels of testing, each with its own report in the Report 5 series. The targets these activities are measured against are the quality metrics in Section I.5 - Project Objectives, and every defect found at any level is logged in Jira with a description, reproduction steps, evidence, a severity level and an assigned handler.

#### 2.1 Defect Prevention

●  	Agree a coding convention, a branching convention and a definition of done during initial planning, before any production code is written, and apply them to every pull request in every iteration.

●  	Review the architecture and the boundary between the web application and the audio-processing component before Iteration 1 starts, so that interface mistakes are caught on paper rather than in the increment.

●  Carry out the feasibility study of the initial planning phase on the two features that carry the most uncertainty, so that the design of FE-08 and FE-10 rests on measured behaviour rather than assumption.

●  	Maintain a shared list of the defects that recur in the team's work - validation gaps, permission checks missed on an endpoint, unhandled file-upload errors, time-zone handling in the calendar - and check new code against it during review.

●  	The Test Lead reviews the defect-prevention list in the evaluation phase of every iteration and adjusts it from what that iteration actually produced.

●  	Technology-specific prevention is automated in the repository: a shared .editorconfig fixes the formatting rules for every IDE, the CI build treats .NET compiler and analyzer warnings as errors (-warnaserror), and the frontend is checked by oxlint and the TypeScript compiler.

#### 2.2 Reviewing

●  	Review every requirement in the requirements phase of the iteration that will build it: the Business Analyst presents it, the Product Owner confirms it, and the members who will build it and the Test Lead agree its acceptance criteria, so that developer and tester share one reading of it before any code is written.

●  	Review each report version before submission: the Business Analyst reviews content and consistency against Report 1 and Report 3, and the whole team reads the document once before it is sent.

●  	Review all code through GitHub pull requests. No branch is merged into dev without at least one approval; front-end changes are reviewed by the Frontend Lead and back-end changes by the Backend Lead.

●  	Check every pull request against the definition of done: the feature works, unit tests exist and pass, the code follows the convention, and the corresponding Jira task is updated.

●  	Review the design of the current iteration against the design of the previous increments, so that a decision that no longer fits is corrected in the analysis and design phase rather than worked around in code.

#### 2.3 Unit Testing

●  	Unit tests are written by the developer who owns the module, in the same iteration as the code, and are run on every push through the CI pipeline.

●  	Each unit under test is covered for the normal case, the failure case and the boundary case - a happy case alone is not accepted at review.

●  	Target coverage is 70% of the core business logic of the backend, excluding generated code and configuration, measured in the testing phase of every iteration.

●  	Results are recorded in Report 5.1 - Unit Test, with the test case ID, preconditions, input and expected output, the verification step (return value, exception, log message), the result type (normal, abnormal, boundary), pass or fail, the execution date and the defect ID where one was raised.

●  	Backend tests are written with xUnit, using Microsoft.AspNetCore.Mvc.Testing for tests that exercise the API end to end and Coverlet to collect coverage; repository tests run against real PostgreSQL and Redis instances in CI rather than fakes. The frontend is checked by the TypeScript compiler and oxlint in CI.

#### 2.4 Integration Testing

●  Integration testing covers the API contract between the front end and the back end, and every point at which the new increment meets the increment delivered by the previous iteration - above all the upload-to-summary flow of FE-09 and FE-10, in which a file uploaded through the web application is processed by the audio component and returns a transcript and a summary.

●  	Each endpoint is checked for the correct status code (200, 400, 401, 403, 404, 500), the structure and accuracy of the returned data, and the behaviour of the permission check under a role that is not entitled to the resource.

●  	From Iteration 4 onwards, the integration suite is also run as a regression pass over the earlier increments, so that a change made for the current iteration cannot silently break what was already accepted.

●  	Integration tests are grouped into a shared collection that any member can run against a local or staging environment.

●  	Results are recorded in Report 5.2 - Integration Test, with the test case ID, description, procedure, preconditions, expected and actual results, execution date and tester.

#### 2.5 System Testing

●  	System testing is executed on the staging environment, against the deployed software package rather than a developer machine, and is led by the Test Lead. It is performed in full in Iteration 6, once the product is feature complete, on the test cases written progressively from Iteration 5 onwards.

●  Functional requirements are tested along the complete flow: an Organizer schedules a Meeting and assigns the first Secretary; the Secretary prepares the agenda, roster and authorized documents; the Meeting Approver reviews it; the Organizer publishes the approved Meeting and invitations are sent. The Chair starts it, attendees participate, eligible internal users vote, Votes are closed and the Chair records conclusions before ending it. Afterwards, the Secretary uploads consented audio, corrects the transcript, generates and approves the summary, and compiles minutes containing the vote tallies and Chair conclusions for signing and finalization.

●  	Non-functional requirements - response time, concurrent participants in a meeting room, audio file size and processing time, and the access-control rules - are tested separately and recorded in Report 5.4.

●  	Results are recorded in Report 5.3 - System Test (FRs) and Report 5.4 - System Test (NFRs), each with the test case ID, description, procedure, preconditions, expected and actual results, test date and tester.

●  	The system moves into the deployment phase only when no critical defect remains open.

#### 2.6 Acceptance Testing

●  	Acceptance criteria are derived from the requirements baselined in Report 3 and from the list of critical use cases agreed with the Product Owner in the requirements phase of each iteration. Each criterion is mapped to a script in Report 5.5.

●  	Acceptance testing is performed in the deployment phase, manually, on the staging environment. The Test Lead coordinates the session and each script is executed by a member who did not build the feature under test, using a prepared demonstration data set that includes real Vietnamese meeting audio.

●  	This internal session establishes that the product meets the requirements baselined in Report 3. Formal acceptance is then given by the Product Owner, Dinh Van Toi, in an acceptance session on the same scripts, and its result is recorded in Report 5.5.

●  	Because each increment has already been demonstrated and assessed in the evaluation phase of its own iteration, acceptance testing confirms the product as a whole rather than discovering its features for the first time.

●  	Defects raised during the session are logged in Jira with the same detail as any other defect and triaged immediately by severity.

●  	When every acceptance criterion has been met and no major defect remains open, the team records the result and the system is prepared for the final handover.

### 3. Project Training Plan

| Training Area | Participants | When, Duration |
| --- | --- | --- |
| Iterative Incremental process, work breakdown and Jira (work items, iteration board, defect workflow) | All members | Week 1: 1 day |
| Git branching model and the pull-request review workflow on GitHub | All members | Week 1: 1 day |
| Figma and the UI mock-up convention used across the project | All members, led by KhanhPG | Week 1: 1 day |
| WebRTC and media-server fundamentals, in preparation for FE-08 | All members, led by LocHD | Week 1: 2 days (feasibility study); Week 7: 1 day before Iteration 4 |
| Speech-to-text and speaker diarisation for Vietnamese audio, in preparation for FE-10 | HaiTV, LocHD, HuyNQ | Week 1: 2 days (feasibility study); Week 5: 1 day before Iteration 3 |
| Test case design, test levels and the Report 5 templates | All members, led by HuyNT | Week 3: 2 days |
| Digital signature concepts: hashing, signing and sealing a document, audit trail | LocHD, HuyNT | Week 9: 1 day before Iteration 5 |
| ASP.NET Core 10 Minimal API, Entity Framework Core 10 and the Domain / Infrastructure / Web project structure, with the backend coding convention | All members | Week 1-2: 3 days |
| React 19, TypeScript, Vite and React Router, with the feature-based frontend structure | All members | Week 1-2: 3 days |
| PostgreSQL 17 with EF Core code-first migrations, and Redis for refresh tokens | All members | Week 2: 2 days |

### 4. Responsibility Assignments

The team consists of five members working under one supervisor. The supervisor's role is academic: he guides the team and reviews its work, and does not take the Product Owner role. The Product Owner is Dinh Van Toi of TrustSoft, who decides the product requirements and their priority, confirms the acceptance criteria and accepts the product. The team leader also leads the front end; the Business Analyst owns the requirements and the project documentation; and the Backend Lead and the Test Lead own the back end and testing respectively. Because five members cannot cover fifteen active features in specialised silos, and because every iteration passes through requirements, design, implementation and testing within two weeks, each member works across more than one phase and writes code in both layers. A lead role therefore means accountability for the decisions, the review and the quality of that area, not exclusive ownership of the work in it.

Figure 2: Project organisational chart

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
| System architecture design | C | C | A/R | R | I |
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
| Document Review | Business Analyst (KhanhPG) and the members who wrote the section | Review a report version for content, consistency with Report 1 and Report 3, and template compliance before it is submitted. | Before every report submission | Google Docs comments; Change Log updated in the document |
| Quick Communication | All team members, supervisor when needed | Ask a short question, share a link or a status update, and get fast feedback on a small issue. | Any time | Messenger, Zalo |

### 6. Configuration Management

#### 6.1 Document Management

All project documents are kept in a single shared Google Drive folder owned by the team and organised into subfolders per report series (Report 1 to Report 7, plus Project Tracking and Meeting Minutes). Only the five members and the supervisor have access; the supervisor has comment and view rights on the current version of every report.

Documents are drafted and reviewed in Google Docs and Google Sheets, which keeps a complete version history showing who changed what and when, so a change can always be traced or reverted and two members editing the same section do not overwrite each other. The final submitted artefact is exported to the .docx or .xlsx format required by the course template, because the template formatting must be preserved exactly.

Every document carries a version number and a Change Log table as its first section. Because the product is built iteratively, the documents are written the same way: Report 3 and Report 4 grow by one version in each iteration, elaborating only the features that iteration delivers, and Report 2 is revised whenever the evaluation phase of an iteration changes the plan. A version is numbered v0.9 while it is a draft under review, v1.0 when it has been reviewed and submitted, and v1.1, v1.2 and so on for each subsequent revision. Every change to a submitted version adds one row to the Change Log recording the date, whether the change is an addition, a modification or a deletion, the member in charge and a description of the change. A submitted file is named in the form Report-N_Name_vX.Y.docx so that the version is visible without opening the file.

A change that affects the product scope, the schedule or the committed effort is not made directly in the document. It is raised as a change request in the evaluation phase of the current iteration, assessed for its impact on the effort and schedule committed in Section I.3, decided by the Team Leader together with the Business Analyst - and, when it alters the product scope, approved by the Product Owner - and reported to the supervisor at the weekly meeting. Only then is it applied - to this plan first, and to the affected reports afterwards, each with its own Change Log entry.

The Business Analyst (KhanhPG) is accountable for document management: for the folder structure, for checking that a version is reviewed before it is submitted, and for keeping the Change Log of every report complete.

#### 6.2 Source Code Management

All source code is kept in a private GitHub repository owned by the team. Only the five members and the supervisor have access. The repository holds the front end, the back end, the audio-processing component, the database migration scripts and the deployment configuration, so that one commit history covers the whole system.

The team follows a three-level branching model:

●  	main - the stable branch. It always holds the latest released increment and is the branch a demonstration or a deployment is taken from. Nothing is committed to main directly; it only receives merges from dev at the end of an iteration, and every merge is tagged with the increment version (v1.0, v2.0 and so on).

●  	dev - the integration branch. Feature branches are merged here through pull requests, and the CI pipeline builds and runs the unit tests on every merge. This is the branch deployed to the staging environment for integration, regression, system and acceptance testing.

●  feature branches - one branch per task, created from dev and named after the Jira issue key and a short description (for example FE-10-speaker-diarisation). A member works only in their own feature branch and pushes to it frequently.

Code enters dev only through a pull request. A pull request must describe what it changes, link to its Jira issue, pass the CI build and unit tests, and be approved by at least one reviewer: the Frontend Lead (HaiTV) for front-end changes and the Backend Lead (LocHD) for back-end and audio-pipeline changes. The reviewer checks the code against the coding convention and the definition of done. main is protected so that it cannot be pushed to directly or force-pushed.

Because every increment is built on the one before it, the tag on main at the end of each iteration is the exact code that was demonstrated and accepted in that iteration's evaluation phase. A defect found later can therefore be traced to the increment that introduced it, and a demonstration can always be given from a known good tag rather than from whatever is currently on dev.

Commit messages follow a fixed form - the Jira issue key, the type of change and a short description - so that the history can be read as a list of what was delivered in each iteration. Secrets, credentials and environment files are never committed; they are held as repository or environment secrets and the repository keeps only the example configuration file.

Merge conflicts are resolved by the member who owns the feature branch, with the technical lead of the affected area when the conflict touches shared code. The Backend Lead is accountable for the integrity of the repository and for the branch protection rules.

#### 6.3 Tools & Infrastructures

| Category | Tools / Infrastructure |
| --- | --- |
| Technology | Backend: ASP.NET Core 10 (C#, Minimal API), JWT bearer authentication and Google sign-in (Google.Apis.Auth). Frontend: React 19, TypeScript, Vite and React Router. Real-time: SignalR for notifications; a planned self-hosted LiveKit SFU provides the WebRTC audio, video and screen-sharing room, with backend-authorized access and Chair controls (FE-08, FE-13). |
| Database | PostgreSQL 17 through Entity Framework Core 10 (Npgsql), code-first migrations; Redis 8 for refresh tokens |
| AI model / speech processing | Post-meeting Vietnamese transcription with speaker attribution and reviewed summaries. Processing environment: self-hosted in an internal CPU-only speech worker (ADR-0026, 23/10/2026), behind a replaceable interface. Record the provider/model per result; implement consent, retention and erasure safeguards and prohibit provider retention or reuse. |
| IDEs / Editors | Visual Studio Code; Visual Studio or JetBrains Rider for the backend; a shared .editorconfig keeps formatting consistent across IDEs |
| Diagramming | draw.io (architecture and UML diagrams), Figma (UI mock-up) |
| Documentation | Google Docs and Google Sheets (drafting and review), Microsoft Office (final .docx and .xlsx deliverables) |
| Version Control | GitHub (source code), Google Drive (documents) |
| CI / Build | GitHub Actions: build and test of the backend (with PostgreSQL and Redis services), lint and build of the frontend, on every pull request and every push to main |
| Development environment | .NET 10 SDK, Node.js 22, Docker Compose (PostgreSQL 17 and Redis 8 for local development) |
| Deployment server | AWS Lightsail instance running Docker Compose behind Caddy (ADR-0015); to be moved to a larger instance for the speech worker (ADR-0026). |
| Project management | Jira (iteration planning, task, defect and issue management) |
| Communication | Google Meet (team and supervisor meetings), Messenger and Zalo (quick communication) |
