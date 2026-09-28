# CAPSTONE PROJECT REPORT

## Report 2 – Project Management Plan

**Project Code:** IMDMS  
**Group:** SEP490-G116  
**Location & Time:** Hanoi, Sep 2026  

---

## Table of Contents

- [Change Log](#change-log)
- [I. Project Overview](#i-project-overview)
  - [1. Scope and Purpose](#1-scope-and-purpose)
  - [2. Assumptions & Constraints](#2-assumptions--constraints)
  - [3. Cost & Time Estimations](#3-cost--time-estimations)
  - [4. Capacity Planning](#4-capacity-planning)
  - [5. Project Objectives](#5-project-objectives)
  - [6. Risks and Mitigations](#6-risks-and-mitigations)
- [II. Management Approach](#ii-management-approach)
  - [1. Project Process/Methodology](#1-project-processmethodology)
  - [2. Quality Management](#2-quality-management)
    - [2.1 Defect Prevention](#21-defect-prevention)
    - [2.2 Reviewing](#22-reviewing)
    - [2.3 Unit Testing](#23-unit-testing)
    - [2.4 Integration Testing](#24-integration-testing)
    - [2.5 System Testing](#25-system-testing)
    - [2.6 Acceptance Testing](#26-acceptance-testing)
  - [3. Project Training Plan](#3-project-training-plan)
  - [4. Responsibility Assignments](#4-responsibility-assignments)
  - [5. Project Communications](#5-project-communications)
  - [6. Configuration Management](#6-configuration-management)
    - [6.1 Document Management](#61-document-management)
    - [6.2 Source Code Management](#62-source-code-management)
    - [6.3 Tools & Infrastructures](#63-tools--infrastructures)

---

## Change Log

| Date | A\*, M, D | In charge | Change Description |
| :--- | :--- | :--- | :--- |
| 15/09/2026 | A | LocHD | Add Project Overview and Management Approach - initial version 1.0 |

*\*A - Added, M - Modified, D - Deleted*

---

## I. Project Overview

### 1. Scope and Purpose

This Project Management Plan defines how group **SEP490-G116** organises, executes, monitors and controls the development of **TrustCabinet - Internal Meeting and Document Management System for TrustSoft Company** over the fifteen-week capstone period from 14 September 2026 to 27 December 2026. 

Its purpose is to establish a single agreed baseline for the work packages to be produced, the effort and schedule committed to them, the quality objectives against which the team is measured, and the way responsibilities, communication and configuration are managed. The product scope summarised below is taken from **Report 1 - Project Introduction**; this document does not redefine it, and any later change to the product scope must be reflected here and recorded in the Change Log.

The work to be implemented consists of the fifteen major features baselined in Report 1, Section V.1, each of which is traceable to a gap identified in Report 1, Section III:

* **Identity and access:** FE-01 Authentication & Account Security, FE-02 User & Profile Management, FE-03 Department & Team Management, FE-04 Role-Based Access Control.
* **Meeting operations:** FE-05 Meeting & Schedule Management, FE-06 Meeting Document Management, FE-07 Virtual Meeting Room.
* **Meeting intelligence:** FE-08 Meeting Audio Upload & Media Library, FE-09 Speaker-Attributed Transcription & Meeting Summary.
* **Meeting governance:** FE-10 Voting & Resolution Management, FE-11 Meeting Minutes & Digital Signing.
* **Execution and visibility:** FE-12 Task & Action Item Lifecycle Management, FE-13 Email & In-App Notification, FE-14 Personal Dashboard & Meeting Workspace, FE-15 Reporting & Statistics.

Each feature is elaborated into detailed requirements in **Report 3 - Software Requirement Specification** during the iteration in which it is built, and is broken down into tasks in the Project Tracking workbook. The mapping between a feature and the iteration that delivers it is given in Section I.3 - Cost & Time Estimations and in Section II.1 - Project Process/Methodology.

The following items are explicitly excluded from this project and from the effort and schedule committed in Section I.3 (corresponding to exclusions LI-01 to LI-11 in Report 1):
* No automatic or server-side recording of a meeting (LI-01).
* No real-time transcription, live captioning or in-meeting AI assistance; processing begins only after an audio file is uploaded (LI-02).
* Only audio files are accepted for processing, not video (LI-03).
* Version 1 operates in Vietnamese with no translation and no subtitles (LI-04).
* No bridge into Zoom, Google Meet or Microsoft Teams (LI-05).
* Meeting minutes carry internal electronic signatures rather than certificate-authority signatures (LI-06).
* No integration layer or public API for HRM, ERP, calendar or chat platforms (LI-07).
* The meeting room supports neither webinars, live streaming, breakout rooms nor virtual backgrounds (LI-08).
* No conversation analytics such as sentiment analysis or talk-time coaching (LI-09).
* No native mobile application; mobile use is covered by the responsive web interface (LI-10).
* Single-organisation deployment rather than multi-tenant service (LI-11).

TrustCabinet is a standalone capstone project. It carries no delivery dependency on any other project, and reuses no existing code base: every component is built by the team within the fifteen weeks planned here.

### 2. Assumptions & Constraints

| No | Category | Description | Note |
| :---: | :--- | :--- | :--- |
| **Assumptions** | | | |
| 1 | External Interfaces | Until a Product Owner is appointed, the detailed requirements of each iteration are elaborated by the Business Analyst from the scope baselined in Report 1 and confirmed by the team in the requirements phase of that iteration, together with their acceptance criteria. | Requirements |
| 2 | External Interfaces | Feedback from the supervisor on a submitted report version is received within seven days. If no comment is raised within that period, the version is treated as the current baseline for the following work. | Governance |
| 3 | Resources | All five members remain on the team for the whole fifteen weeks and are able to commit the hours stated in Section I.4 - Capacity Planning. | Team |
| 4 | Quality / Security | Meeting audio used to develop and test FE-08 and FE-09 is produced by the team's own recorded meetings in Vietnamese. No real organisational meeting material is used as test data. | Data Privacy |
| 5 | Resources | The free and educational licences the team relies on (Jira, GitHub, Google Workspace) remain available for the whole project at no cost. | Tools |
| **Constraints** | | | |
| 1 | Schedule | The project must be delivered within the fifteen-week academic schedule, 14/09/2026 to 27/12/2026. The closing milestone cannot be moved, so scope rather than schedule absorbs any overrun. | Fixed Schedule |
| 2 | Budget | The project has no budget. Only free, open-source or student-licensed tools, services and infrastructure may be used. | Zero Budget |
| 3 | Resources | The team consists of five part-time student members who carry other courses in parallel. There is no full-time developer and no dedicated designer or DevOps engineer. | Staffing |
| 4 | External Interfaces | No Product Owner has yet been appointed. Until the role is filled, requirements, priorities and acceptance criteria are derived by the team from the scope baselined in Report 1. The supervisor provides academic guidance and reviews the team's work, and does not take the Product Owner role. | Product Ownership |
| 5 | Technology | The system must be deployable inside the organisation's own infrastructure. No capability may depend on a service that cannot be run on-premises without redesigning that capability. | Self-hosted |
| 6 | Scope | Version 1 operates in Vietnamese only, with no translation and no subtitles (Report 1, LI-04). | Language |
| 7 | Scope / Security | The system performs no automatic recording. Meeting audio is recorded by a participant on their own device, uploaded afterwards, and processed only once the meeting has ended (Report 1, LI-01 and LI-02). | Audio Capture |
| 8 | Scope | Meeting minutes are signed with internal electronic signatures only. Certificate-authority signatures issued on a USB token are out of scope (Report 1, LI-06). | E-Signature |
| 9 | Technology | The technology stack, the database and the environment in which the speech-processing and summarisation model runs are decided early in the project. | Architecture |

### 3. Cost & Time Estimations

Effort is expressed in person-days (pds) of eight hours. The 450 person-days committed below are the effective working capacity derived in Section I.4; the 112.5 person-days of buffer are held back and not distributed into these work packages.

| # | Work Package | Effort (pds) | Due Date |
| :---: | :--- | :---: | :---: |
| **0** | **Initial Planning (week 1) 14/09/2026 - 20/09/2026** | **30** | |
| 0.1 | Report 1: Project Introduction v0.9 | 6 | 20/09/2026 |
| 0.2 | Technical feasibility study: virtual meeting room and speaker-attributed transcription (proof of concept) | 10 | 20/09/2026 |
| 0.3 | Overall architecture design, repository and development environment setup | 8 | 20/09/2026 |
| 0.4 | UI mock-up for the Iteration 1 scope | 6 | 20/09/2026 |
| **1** | **Iteration 1: Identity & Access Foundation - FE-01 to FE-04 (weeks 2-3) 21/09/2026 - 04/10/2026** | **60** | |
| 1.1 | Report 1: Project Introduction v1.0 | 3 | 27/09/2026 |
| 1.2 | Report 2: Project Management Plan v1.0 | 6 | 27/09/2026 |
| 1.3 | Report 3: SRS v0.9 (overall requirements) | 9 | 04/10/2026 |
| 1.4 | FE-01 Authentication & Account Security | 8 | 04/10/2026 |
| 1.5 | FE-02 User & Profile Management | 8 | 04/10/2026 |
| 1.6 | FE-03 Department & Team Management | 6 | 04/10/2026 |
| 1.7 | FE-04 Role-Based Access Control | 10 | 04/10/2026 |
| 1.8 | Code package & Report 5.1 Unit Test | 6 | 04/10/2026 |
| 1.9 | Software Package v1.0 (increment 1) | 4 | 04/10/2026 |
| **2** | **Iteration 2: Meeting & Document Core - FE-05, FE-06 (weeks 4-5) 05/10/2026 - 18/10/2026** | **60** | |
| 2.1 | Report 2: Project Management Plan v1.1 | 3 | 11/10/2026 |
| 2.2 | Report 3: SRS v1.0 (detailed requirements for Iterations 1-2) | 9 | 18/10/2026 |
| 2.3 | Report 4: SDS v1.0 (high level design specifications) | 9 | 18/10/2026 |
| 2.4 | Report 5.0: Test Documentation (test planning) | 6 | 18/10/2026 |
| 2.5 | FE-05 Meeting & Schedule Management | 12 | 18/10/2026 |
| 2.6 | FE-06 Meeting Document Management | 10 | 18/10/2026 |
| 2.7 | Code package & Report 5.1 Unit Test | 7 | 18/10/2026 |
| 2.8 | Software Package v2.0 (increment 2) | 4 | 18/10/2026 |
| **3** | **Iteration 3: Meeting Audio & AI Processing - FE-08, FE-09 (weeks 6-7) 19/10/2026 - 01/11/2026** | **60** | |
| 3.1 | Report 3: SRS v1.1 (detailed requirements for Iteration 3) | 6 | 25/10/2026 |
| 3.2 | Report 4: SDS v1.1 (detailed design of the audio processing pipeline) | 7 | 25/10/2026 |
| 3.3 | FE-08 Meeting Audio Upload & Media Library | 10 | 01/11/2026 |
| 3.4 | FE-09 Speaker-Attributed Transcription & Meeting Summary | 16 | 01/11/2026 |
| 3.5 | Report 5.2: Integration Test cases and Integration Test report | 6 | 01/11/2026 |
| 3.6 | Code package & Report 5.1 Unit Test | 7 | 01/11/2026 |
| 3.7 | Report 2: Project Management Plan v1.2 | 4 | 25/10/2026 |
| 3.8 | Software Package v3.0 (increment 3) | 4 | 01/11/2026 |
| **4** | **Iteration 4: Virtual Meeting Room - FE-07 (weeks 8-9) 02/11/2026 - 15/11/2026** | **60** | |
| 4.1 | Report 3: SRS v1.2 (detailed requirements for Iteration 4) | 5 | 08/11/2026 |
| 4.2 | Report 4: SDS v1.2 (detailed design of the meeting room and media flow) | 7 | 08/11/2026 |
| 4.3 | FE-07 Virtual Meeting Room | 24 | 15/11/2026 |
| 4.4 | Report 5.2: Integration Test cases and Integration Test report | 6 | 15/11/2026 |
| 4.5 | Code package & Report 5.1 Unit Test | 9 | 15/11/2026 |
| 4.6 | Report 2: Project Management Plan v1.3 | 4 | 08/11/2026 |
| 4.7 | Software Package v4.0 (increment 4) | 5 | 15/11/2026 |
| **5** | **Iteration 5: Meeting Governance & Task Loop - FE-10, FE-11, FE-12 (weeks 10-11) 16/11/2026 - 29/11/2026** | **60** | |
| 5.1 | Report 3: SRS v1.3 (detailed requirements for Iteration 5) | 5 | 22/11/2026 |
| 5.2 | Report 4: SDS v1.3 | 6 | 22/11/2026 |
| 5.3 | FE-10 Voting & Resolution Management | 10 | 29/11/2026 |
| 5.4 | FE-11 Meeting Minutes & Digital Signing | 12 | 29/11/2026 |
| 5.5 | FE-12 Task & Action Item Lifecycle Management | 11 | 29/11/2026 |
| 5.6 | Report 5.2 and 5.3: Integration Test cases and System Test cases | 6 | 29/11/2026 |
| 5.7 | Code package & Report 5.1 Unit Test | 7 | 29/11/2026 |
| 5.8 | Software Package v5.0 (increment 5) | 3 | 29/11/2026 |
| **6** | **Iteration 6: Notification, Workspace & Reporting - FE-13, FE-14, FE-15 (weeks 12-13) 30/11/2026 - 13/12/2026** | **60** | |
| 6.1 | FE-13 Email & In-App Notification | 9 | 13/12/2026 |
| 6.2 | FE-14 Personal Dashboard & Meeting Workspace | 10 | 13/12/2026 |
| 6.3 | FE-15 Reporting & Statistics | 10 | 13/12/2026 |
| 6.4 | Report 5.3 and 5.4: System Test cases and System Test report (FRs and NFRs) | 9 | 13/12/2026 |
| 6.5 | Report 6: Software User Guides (installation guide, user manual) | 8 | 13/12/2026 |
| 6.6 | Code package & Report 5.1 Unit Test | 7 | 13/12/2026 |
| 6.7 | Report 2: Project Management Plan v1.4 | 3 | 06/12/2026 |
| 6.8 | Software Package v6.0 (increment 6 - feature complete) | 4 | 13/12/2026 |
| **7** | **Deployment: Acceptance, Stabilisation & Project Closure (weeks 14-15) 14/12/2026 - 27/12/2026** | **60** | |
| 7.1 | Report 5.5: Acceptance Test scripts and acceptance test support | 10 | 20/12/2026 |
| 7.2 | Defect fixing and system stabilisation | 15 | 27/12/2026 |
| 7.3 | Report 7: Final Project Document | 12 | 27/12/2026 |
| 7.4 | Presentation file and demo rehearsal | 9 | 27/12/2026 |
| 7.5 | Final software package (source code, documents, deployment package) | 10 | 27/12/2026 |
| 7.6 | Report 2: Project Management Plan v1.5 (final update) | 4 | 27/12/2026 |
| | **TOTAL** | **450** | |

### 4. Capacity Planning

| Item | Value |
| :--- | :--- |
| **Hours per person per week** | 60 h (8 h/day x 5 weekdays + 10 h/day x 2 weekend days) |
| **Team total per week** | 5 people x 60 h = 300 h/week |
| **Total duration** | 15 weeks (14/09/2026 - 27/12/2026) |
| **Gross capacity** | 300 h/week x 15 weeks = 4,500 h = 562.5 person-days (8 h/person-day) |
| **Buffer (meetings, reviews, unplanned - 20%)** | 900 h = 112.5 person-days |
| **Effective working capacity** | 3,600 h = 450 person-days |

The buffer of 20% covers the time the team spends in meetings, reviews and retrospectives, rework, and unplanned absences. Because it is excluded before the work packages are planned, an overrun inside an iteration is absorbed by the buffer rather than pushing unfinished work into the next one.

### 5. Project Objectives

| # | Metric | Unit | Planned | Actual | Notes / References |
| :---: | :--- | :---: | :---: | :---: | :--- |
| 1 | Effort Usage | Person-day | 450 | - | Effective working capacity from Section I.4; distributed over Section I.3 work packages and tracked per member in Project Tracking workbook |
| 2 | Review Defects | No. of defects | 30 | - | Defects found in document and code reviews before test execution. Early detection indicator |
| 3 | Unit Test Defects | No. of defects | 40 | - | Written and executed by developer owning the module; recorded in Report 5.1 |
| 4 | Integration Test Defects | No. of defects | 30 | - | Found when modules and APIs are combined and exercised together; recorded in Report 5.2 |
| 5 | System Test Defects | No. of defects | 15 | - | Found when testing whole system against FRs and NFRs; recorded in Reports 5.3 & 5.4 |
| 6 | Acceptance Test Defects | No. of defects | 5 | - | Found during acceptance testing in deployment phase; recorded in Report 5.5 |
| 7 | Timeliness | % | 90% | - | Total on-time deliverables / Total project deliverables |
| 8 | Requirement Completeness | % | 90% | - | Implemented and accepted requirements / Baselined requirements in Report 3 (Failed if <= 75%) |
| 9 | Unit Test Code Coverage | % | 70% | - | Measured on core backend business logic, excluding generated code/config |

### 6. Risks and Mitigations

| # | Risk | Prob | Impact | Mitigation |
| :---: | :--- | :---: | :---: | :--- |
| **R01** | The virtual meeting room (FE-07) is the team's first piece of real-time media work. Connection establishment, NAT traversal and multi-party audio/video quality may take longer than estimated. | High | High | Run feasibility study in Initial Planning phase before estimate is committed; build on proven open-source media server rather than raw P2P. FE-07 is the only major feature in Iteration 4. Fallback: reduced room (audio, screen sharing, chat, capped participants). |
| **R02** | Speaker-attributed transcription of Vietnamese meeting audio (FE-09) produces output too inaccurate to summarise, making the closed-loop workflow unusable. | Medium | High | Evaluate candidate models in Initial Planning against team recordings; agree on acceptance threshold. Keep transcript editable so host can correct speaker labels before summary generation. Scheduled in Iteration 3 to allow time for adjustments. |
| **R03** | Technology stack and environment for speech-processing & summarisation model remain unfixed initially, delaying environment setup. | Medium | High | Set decision deadline at end of Initial Planning phase. Design architecture so transcription/summarisation sit behind an internal interface and can switch between self-hosted model and external service without modifying rest of system. |
| **R04** | 15 features in 15 weeks exceeds part-time student team capacity. | High | High | Prioritise feature list into must-have (FE-01 to FE-12) and should-have (FE-13 to FE-15). Re-plan remaining iterations based on actual effort consumed. The 20% buffer absorbs non-critical overruns. |
| **R05** | Workload is distributed unevenly or a member becomes unavailable at a critical point, leaving single-person knowledge silos. | Medium | Medium | Assign one accountable owner per work package and pair on the first task of every new module so at least two members understand it. Log effort in Jira and review weekly. |

---

## II. Management Approach

### 1. Project Process/Methodology

The team applies the **Iterative Incremental Model** to this project. The system is produced in a series of iterations, each running through planning, requirements, analysis and design, implementation, testing and evaluation, adding a working increment to the product delivered by the previous one.

```
+---------------------------------------------------------------------------------+
|                       Iterative Incremental Lifecycle                           |
+---------------------------------------------------------------------------------+
|  Initial Planning (W1) -> Iterations 1 to 6 (W2-13) -> Deployment/Closing (W14-15)|
|                                                                                 |
|  Each Iteration:                                                                |
|  [Planning] -> [Requirements] -> [Analysis & Design] ->                         |
|  [Implementation] -> [Testing] -> [Evaluation / Review]                         |
+---------------------------------------------------------------------------------+
```

#### Iteration Schedule Breakdown:
* **Initial Planning (14/09/2026 - 20/09/2026):** Environment, repo, Jira setup; architecture design; media & AI feasibility studies; UI mockups; Report 1 v0.9.
* **Iteration 1 (21/09/2026 - 04/10/2026) - FE-01 to FE-04:** Identity & Access Foundation (Authentication, User/Role, Department, RBAC). Software Package v1.0.
* **Iteration 2 (05/10/2026 - 18/10/2026) - FE-05, FE-06:** Meeting & Document Core (Meeting scheduling, calendar, agenda, document attachments & versioning). Software Package v2.0.
* **Iteration 3 (19/10/2026 - 01/11/2026) - FE-08, FE-09:** Meeting Audio & AI Processing (Audio upload, speaker-attributed transcription, AI summary & host approval). Software Package v3.0.
* **Iteration 4 (02/11/2026 - 15/11/2026) - FE-07:** Virtual Meeting Room (WebRTC / media server room with audio, video, screen share, chat). Software Package v4.0.
* **Iteration 5 (16/11/2026 - 29/11/2026) - FE-10, FE-11, FE-12:** Meeting Governance & Task Loop (Voting & resolutions, digitally signed minutes, action item task lifecycle). Software Package v5.0.
* **Iteration 6 (30/11/2026 - 13/12/2026) - FE-13, FE-14, FE-15:** Notification, Workspace & Reporting (Email/in-app notifications, user dashboard, reporting statistics). Software Package v6.0 (Feature Complete).
* **Deployment Phase (14/12/2026 - 27/12/2026):** Acceptance testing, defect fixing, final packaging, documentation finalisation, demo rehearsal, project closing.

---

### 2. Quality Management

Quality is managed on the principle that a defect is cheapest to remove in the phase that created it. Each iteration contains its own defect prevention, reviewing, and testing activities.

#### 2.1 Defect Prevention
* Agree on coding conventions, branching conventions, and Definition of Done during initial planning.
* Review architecture and module boundaries early to catch interface mismatches on paper.
* Perform early feasibility studies on high-risk components (WebRTC and Vietnamese ASR).
* Maintain a shared checklist of recurring defects (validation, RBAC checks on endpoints, file upload edge cases, timezone handling).

#### 2.2 Reviewing
* Requirements walkthrough with BA, developers, and QA Lead to establish shared acceptance criteria.
* Report drafts reviewed for consistency against Report 1 & Report 3 before submission.
* Mandatory GitHub Pull Request review: at least 1 approval required (Frontend Lead for FE, Backend Lead for BE/media).
* Check PRs against Definition of Done (functionality, passing unit tests, adherence to conventions, updated Jira tasks).

#### 2.3 Unit Testing
* Written by the developer owning the module; run on every push via CI pipeline.
* Test coverage for normal, abnormal (error), and boundary cases.
* Target coverage: **70%** of backend core business logic. Recorded in Report 5.1.

#### 2.4 Integration Testing
* Verifies frontend-backend API contracts, authentication/authorization enforcement, and audio-to-summary asynchronous workflows.
* Run as a regression suite across previously delivered increments from Iteration 4 onward.
* Grouped into shared collections and documented in Report 5.2.

#### 2.5 System Testing
* Led by Test Lead on the staging environment against full usage flows.
* Covers functional workflows and non-functional requirements (performance, file limits, concurrency, security).
* Documented in Reports 5.3 (FRs) and 5.4 (NFRs).

#### 2.6 Acceptance Testing
* Executed in the deployment phase against predefined scripts mapped to Report 3 requirements.
* Conducted by cross-testing members using realistic Vietnamese meeting datasets. Documented in Report 5.5.

---

### 3. Project Training Plan

| Training Area | Participants | Timeline & Duration |
| :--- | :--- | :--- |
| Iterative Incremental process, work breakdown, Jira board & defect workflow | All members | Week 1: 1 day |
| Git branching model and PR review workflow on GitHub | All members | Week 1: 1 day |
| Figma and UI mock-up conventions | All members (led by KhanhPG) | Week 1: 1 day |
| WebRTC & media server fundamentals (FE-07) | All members (led by LocHD) | Week 1: 2 days (PoC); Week 7: 1 day |
| Speech-to-text and speaker diarisation for Vietnamese audio (FE-09) | HaiTV, LocHD, HuyNQ | Week 1: 2 days (PoC); Week 5: 1 day |
| Test case design, test levels, and Report 5 templates | All members (led by HuyNT) | Week 3: 2 days |
| Digital signature concepts (hashing, signing, sealing, audit trail) | LocHD, HuyNT | Week 9: 1 day |
| Backend & Frontend frameworks, architecture, and coding conventions | All members | Week 1-2: 3 days |
| Database design & PostgreSQL / EF Core ORM | All members | Week 2: 2 days |

---

### 4. Responsibility Assignments

```
                           +------------------------+
                           |  Nguyen Quang Hung     |
                           |  (Academic Supervisor) |
                           +-----------+------------+
                                       |
                           +-----------v------------+
                           |  Tran Viet Hai         |
                           |  (Leader / BA / Dev)   |
                           +-----------+------------+
                                       |
         +-----------------------------+-----------------------------+
         |                             |                             |
+--------v---------+          +--------v---------+          +--------v---------+
|  Pham Gia Khanh  |          |  Hoang Duc Loc   |          | Nguyen Truong Huy|
|  (Frontend Lead) |          |  (Backend Lead)  |          | (Test / QA Lead) |
+--------+---------+          +--------+---------+          +--------+---------+
         |                             |                             |
         +-----------------------------+-----------------------------+
                                       |
                              +--------v---------+
                              | Nguyen Quang Huy |
                              | (Fullstack Dev)  |
                              +------------------+
```

#### RACI Matrix
* **R (Responsible):** Member(s) executing the task.
* **A (Accountable):** Member ensuring on-time and quality completion.
* **C (Consulted):** Member providing two-way feedback/input.
* **I (Informed):** Member kept updated.

| Responsibility / Deliverable | HaiTV | KhanhPG | LocHD | HuyNQ | HuyNT |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Documents and Reports** | | | | | |
| Report 1: Project Introduction | A/R | C | C | C | C |
| Report 2: Project Management Plan | A/R | C | C | C | C |
| Report 3: Software Requirement Specification | A/R | R | R | R | C |
| Report 4: Software Design Specification | A/R | R | R | R | I |
| Report 5: Test Documentation & Reports | C | R | R | R | A/R |
| Report 6: Software User Guides | A/R | R | R | R | R |
| Report 7: Final Project Document | A/R | R | R | R | R |
| Presentation slides and demo | A/R | R | R | R | R |
| **Analysis and Design** | | | | | |
| System architecture design | C | C | A/R | R | I |
| Use case diagram and main workflow | A/R | R | R | R | C |
| Database design | C | I | A/R | R | I |
| Class, sequence and package diagrams | R | R | A/R | R | I |
| UI design and mock-up | C | A/R | I | R | C |
| Audio processing & AI summary design (FE-08, FE-09) | C | I | A/R | R | C |
| Virtual meeting room design (FE-07) | C | R | A/R | R | C |
| **Implementation** | | | | | |
| Technology and tools selection | A | C | R | C | C |
| Coding conventions & Definition of Done | C | R | A/R | R | R |
| Frontend implementation | R | A/R | R | R | R |
| Backend implementation | R | R | A/R | R | R |
| Source code control & PR review | I | R | A/R | R | I |
| CI pipeline & deployment to staging | I | R | A/R | R | C |
| Jira progress & task tracking | A/R | R | R | R | R |
| **Testing** | | | | | |
| Test plan & strategy | C | I | I | I | A/R |
| Unit test cases & execution | R | R | R | R | A |
| Integration & regression testing | I | R | R | R | A/R |
| System test cases & execution | C | R | R | R | A/R |
| Acceptance test scripts & support | R | R | R | R | A/R |
| Defect logging, triage and tracking | R | R | R | R | A/R |
| **Project Management** | | | | | |
| Iteration planning & work allocation | A/R | R | R | R | R |
| Iteration evaluation & re-planning | A/R | R | R | R | R |
| Supervisor communication | A/R | I | C | I | I |
| Risk monitoring & change requests | A/R | C | C | C | C |
| Document & configuration management | A/R | R | R | R | R |

---

### 5. Project Communications

| Communication Item | Target Audience | Purpose | Frequency / Timing | Channels / Tools |
| :--- | :--- | :--- | :--- | :--- |
| **Team Progress Meeting** | All dev team members | Daily sync, task progress, blockers, Jira board update | 3 times/week (Mon, Wed, Fri), 21:00 - 21:30 | Google Meet, Jira |
| **Iteration Planning Meeting** | All team members | Define iteration goal, select features, task breakdown & estimation | First day of each iteration (2-3 hours) | Google Meet / On Campus, Jira |
| **Requirement Confirmation** | BA (HaiTV), Test Lead (HuyNT), Devs | Walkthrough detailed SRS requirements, agree on acceptance criteria | First 3 days of iteration (1-2 hours) | Google Meet, Google Docs |
| **Design Review Meeting** | LocHD, KhanhPG, HaiTV, HuyNT | Review data model, API contract, and UI screens for current iteration | After requirements phase (1-2 hours) | Google Meet, draw.io |
| **Iteration Evaluation Meeting** | All team members | Demo increment, assess vs goal, review consumed effort and defects | Last day of each iteration (1-2 hours) | Google Meet / On Campus, Jira |
| **Supervisor Meeting** | Supervisor (HungNQ), team | Report progress, demo increment, raise risks, receive academic feedback | Weekly (agreed schedule) | Google Meet / Campus, Google Docs |
| **Technical Discussion** | Concerned members | Resolve deep technical/architecture blockers as they arise | As needed | Google Meet, Jira |
| **Document Review** | BA (HaiTV) & section authors | Review report quality, consistency, and template conformance | Prior to submission | Google Docs Comments |
| **Quick Communication** | All team members | Fast updates, questions, links, ad-hoc sync | Continuous | Messenger, Zalo |

---

### 6. Configuration Management

#### 6.1 Document Management
* All documents are maintained in a shared Google Drive folder structured by report series (Report 1 to Report 7, Project Tracking, Meeting Minutes).
* Version naming follows `Report-N_Name_vX.Y.docx`. Working drafts are numbered `v0.9`, initial baselines `v1.0`, and revisions `v1.1`, `v1.2`, etc.
* Every document maintains an updated Change Log table tracking Date, Action (A/M/D), Author, and Description.
* Scope or timeline changes require a formal change request evaluated in the iteration evaluation meeting before amending documentation.

#### 6.2 Source Code Management
* Source code is hosted on a private GitHub repository holding frontend, backend, audio pipelines, migrations, and CI scripts.
* **Three-level branching strategy:**
  * `main`: Protected, production-ready releases tagged with increment versions (`v1.0`, `v2.0`...).
  * `dev`: Integration branch for CI builds, unit tests, and deployment to staging.
  * `feature/*`: Dedicated task branches named by Jira key (e.g., `FE-09-speaker-diarisation`).
* Merge to `dev` requires an approved PR meeting the Definition of Done (passing tests, code review, convention compliance).
* Credentials, secrets, and connection strings are strictly forbidden in git commits and must use environment variables / local user secrets.

#### 6.3 Tools & Infrastructures

| Category | Tools / Infrastructure |
| :--- | :--- |
| **Technology Stack** | ASP.NET Core 10 (C# Minimal API), React 19 + TypeScript + Vite |
| **Database** | PostgreSQL 16+ with EF Core Code-First Migrations |
| **AI / Speech Processing** | Vietnamese ASR / Whisper pipeline + LLM Summarization service |
| **Real-time Media** | WebRTC / Media Server, SignalR Hubs |
| **IDEs / Editors** | Visual Studio 2026, Visual Studio Code, JetBrains Rider |
| **Diagramming & Design** | draw.io (Architecture, ERD, Sequence diagrams), Figma (UI Mockups) |
| **Documentation** | Google Docs & Sheets (drafting), Markdown (repo docs), MS Office (final submission) |
| **Version Control & CI** | GitHub, GitHub Actions (CI build & test automation) |
| **Project Management** | Jira Software (Scrum / Sprint board, backlog, bug tracking) |
| **Communication** | Google Meet, Zalo, Messenger |
