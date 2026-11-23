"""Chapter 2 - Project Management Plan: objectives, scope, methodology, assumptions, schedule, risks, RACI.

Sources: Report 2 (plan), Project_Weekly_Report_G116.xlsm (W1-W6), Meeting_Minutes/*.docx (W1-W11),
docs/DEVELOPMENT_WORKFLOW.md, git history of `app`. "Actual" columns come from those, never from memory.
Week numbers are course weeks: W1 = 07/09/2026 (the weekly report and the minutes count this way);
Report 2 counts its "week 1" from 14/09, so its week n is course week n+1.
"""

from __future__ import annotations

from report7_lib import Report, _text, is_heading


def fill(r: Report):
    objectives(r)
    scope(r)
    methodology(r)
    assumptions(r)
    summary(r)
    schedule(r)
    risks(r)
    raci(r)


def objectives(r: Report):
    h = r.find_heading("1. Project Objectives")
    els = [
        r.text("General: build a paperless meeting system for TrustSoft that carries one meeting from scheduling to "
               "issued minutes, with voice/video calling built in and AI help for the post-meeting work, on a "
               "stack the team can host itself."),
        r.text("Specific objectives (each one is judged achieved / partially achieved / not achieved in Chapter 6):"),
        r.table(
            ["#", "Objective", "Measurable target"],
            [
                ["1", "Deliver the baselined meeting loop: sign-in and roles, scheduling with approval, agenda, "
                      "participants and attendance, documents, online room, recording, transcription, voting, "
                      "minutes, notifications.",
                 "Requirement completeness >= 90% of the Report 3 baseline (failed at <= 75%)."],
                ["2", "Produce Vietnamese transcripts with speaker attribution and an AI draft of the minutes, "
                      "running on a model the team hosts itself.",
                 "Transcription quality measured on the team's own recordings against the threshold agreed in "
                      "Initial Planning; the draft is editable and approved by a person."],
                ["3", "Enforce access on the server: every endpoint re-authorises, and a record a user may not "
                      "see never reaches the browser.",
                 "No critical defect open at closing; role x action tests pass."],
                ["4", "Deliver on time and with managed quality.",
                 "Timeliness >= 90% of deliverables; unit test coverage >= 70% of core backend logic."],
                ["5", "Catch defects early: review, unit, integration, system and acceptance defects.",
                 "Planned counts 30 / 40 / 30 / 15 / 5 (Report 2 Section I.5); actuals in Chapter 5."],
                ["6", "Stay inside the committed effort.", "300 person-days of effective capacity (Report 2 Section I.4)."],
            ],
            widths_in=[0.3, 3.7, 2.3],
        ),
        r.text("TODO(HuyNT): fill the Actual figures of objectives 4-6 (timeliness, coverage, effort, defect counts) "
               "at closing; they are not in the repository yet."),
    ]
    r.replace_section(h, els)


def scope(r: Report):
    h = r.find_heading("2. Project Scope")
    els = [
        r.text("Target users: one organisation (TrustSoft) with seven roles - System Admin, Meeting Organizer, "
               "Meeting Approver, Chair, Secretary, Participant and Guest. There is no multi-tenancy and no "
               "organisational unit (ADR-0004)."),
        r.text("TODO(KhanhPG): confirm the seven-role list above against the Report 3 baseline; the role names are "
               "not stated in Report 2 or docs/00-overview."),
        r.text("In scope (Report 1 baseline, feature numbers as in Report 2):", bold=True),
        r.table(
            ["Group", "Features"],
            [
                ["Identity and access", "FE-01 Authentication & Account Security; FE-02 User & Profile Management; "
                                        "FE-03 Role-Based Access Control"],
                ["Meeting operations", "FE-04 Meeting Management; FE-05 Agenda; FE-06 Participants and attendance; "
                                       "FE-07 Personal Document Library & Meeting Documents; FE-08 Online Meeting Room "
                                       "(including Register to Speak)"],
                ["Meeting intelligence", "FE-09 Audio Upload & Media Library; FE-10 Speaker-Attributed Transcription "
                                         "& Meeting Summary, with Voice ID under its own consent (see scope changes)"],
                ["Meeting governance", "FE-11 Voting; FE-12 Meeting Minutes: AI draft, draft from the fixed "
                                       "template, send for approval, approve and issue, return and correct"],
                ["Communication and visibility", "FE-13 Email & In-App Notification; FE-15 Reporting & Statistics"],
            ],
            widths_in=[1.5, 4.8],
        ),
        r.text("Scope changes made during the project (all recorded in the Report 2 Change Log and in Jira):",
               bold=True),
        *r.bullets([
            "27/09/2026: task and action-item lifecycle management removed from scope (Report 1 and Report 2 Change "
            "Logs; FE-13 to FE-15 renumbered FE-12 to FE-14 at the time).",
            "05-09/10/2026: baseline consolidation recorded in the Report 1 and Report 2 Change Logs (FE-04 to "
            "FE-07 split, then FE-04 removed; active features renumbered FE-01 to FE-15; one minutes workflow per "
            "meeting; in-meeting voting with Chair conclusions).",
            "23/10/2026 (PO): Voice ID added under its own consent (ADR-0026, ADR-0027; review council 17/10, "
            "supervisor 21/10, MOM-08).",
            "TODO(PO): confirm the 14/11/2026 decision dropping signing of the minutes, even a simulated signature "
            "(UC-62, UC-63), and the Secretary's list of document comments (SCRUM-106); no minute of it is in the "
            "repository.",
            "14/11/2026 (PO): signing of the minutes, even a simulated signature (UC-62, UC-63), and the "
            "Secretary's list of document comments (SCRUM-106) dropped; minutes end at approval and issue.",
            "15/11/2026 (PO): personal dashboard and meeting workspace (UC-69), locking an account after wrong "
            "passwords (UC-09) and FE-14 (account lifecycle beyond deactivation) dropped; the per-minute sign-in "
            "limit stays (SCRUM-207).",
        ]),
        r.text("Out of scope (Report 1, LI-01 to LI-12): automatic server-side recording; real-time transcription "
               "or in-meeting AI; video processing; translation and other languages; bridges to external "
               "conferencing platforms; certificate-authority or HSM signatures; HRM, ERP, calendar or chat "
               "integrations and a public API; native mobile client; multi-tenancy; task assignment and action-item "
               "tracking; air-gapped deployment."),
        r.text("Target scale: a single organisation; the online room is sized for a team meeting, not a webinar."),
    ]
    r.replace_section(h, els)


def methodology(r: Report):
    h = r.find_heading("3. Process / Methodology")
    els = [
        r.text("Model. The team planned the Iterative Incremental model: an Initial Planning week, six two-week "
               "iterations and a two-week deployment phase, each iteration running planning, requirements, "
               "analysis and design, implementation, testing and evaluation. It was chosen because the two most "
               "uncertain features - the online room and speaker-attributed Vietnamese transcription - could not be "
               "estimated from a document and had to be built early to be priced."),
        r.text("How it ran in practice. The team worked in short Jira sprints and moved "
               "each Use Case through one pipeline instead of writing all analysis first: Requirement, Business "
               "Analyst, Backend, Frontend, BA Final Review, then Tester (docs/DEVELOPMENT_WORKFLOW.md). The "
               "backend writes the basic UC specification and sequence diagram and builds, the frontend integrates "
               "as soon as the API is usable, and the BA settles the requirement documents last, noting each "
               "deviation in the ticket. Different Use Cases sit at different stages in the same week."),
        r.text("Control and quality. Weekly meeting with the supervisor (minutes for W1-W11 are in the repository); meetings with "
               "the Product Owner at TrustSoft for requirement decisions (18/09, 13/10, 16/10, 19/10); every change "
               "enters through a reviewed pull request, CI builds with warnings as errors and runs the tests; the "
               "test server is deployed automatically after CI."),
        r.text("Deviations from Report 2: (1) the integration branch is `app`, which became the main branch, and "
               "`main` keeps the frozen three-project demo - Report 2 described main/dev; (2) work was tracked and reported weekly (weekly report W1-W6, supervisor minutes MOM-01 to MOM-15), finer "
               "than the two-week iteration of Report 2; TODO(HaiTV): confirm the Jira sprint length from Jira; (3) features overlapped "
               "across iterations (Table 2.1), which the pipeline allows."),
        r.text("Team organisation.", bold=True),
        r.table(
            ["Area", "Owner(s)", "Integration point"],
            [
                ["Planning, frontend lead, online room UI, Jira", "Tran Viet Hai", "Pull-request review of front-end changes"],
                ["Requirements, UI/UX, BA final review, documents", "Pham Gia Khanh", "Reconciles UC specification with the code at BA Final Review"],
                ["Backend lead, architecture, database, CI and deployment", "Hoang Duc Loc", "Reviews back-end changes; owns repository integrity"],
                ["Participants, agenda, meeting lifecycle, attendance (back end and front end)", "Nguyen Quang Huy", "Pairs with the room and notification owners"],
                ["Testing, QA, notifications (front end)", "Nguyen Truong Huy", "Independent tester on every ticket before Done"],
            ],
            widths_in=[2.7, 1.5, 2.1],
        ),
        r.text("Source: Report 2 Section II.4 and the weekly report of W6 (owners per work item). The Supervisor is "
               "Nguyen Quang Hung; the Product Owner is Dinh Van Toi (TrustSoft)."),
    ]
    r.replace_section(h, els)


def assumptions(r: Report):
    h = r.find_heading("4. Assumptions & Constraints")
    els = [
        r.text("Assumptions", bold=True),
        r.table(
            ["#", "Assumption", "How it held"],
            [
                ["A1", "The Product Owner confirms detailed requirements in each iteration.",
                 "Held: four recorded sessions with TrustSoft (18/09, 13/10, 16/10, 19/10) settled the baseline, "
                 "ending a meeting, voting and the recording method."],
                ["A2", "Supervisor feedback arrives within seven days.", "Partly held: weekly supervisor meetings held (MOM-01 to MOM-15, W1-W11); report turnaround within seven days "
                  "is not tracked in the repository."],
                ["A3", "All five members stay for the fifteen weeks at 40 h/week.", "Held on membership; hours are not tracked in the repository."],
                ["A4", "Audio for development and tests is the team's own Vietnamese recordings; no real organisational data.", "Held."],
                ["A5", "Free or educational licences stay available.", "Held."],
            ],
            widths_in=[0.4, 3.5, 2.4],
        ),
        r.text("Constraints", bold=True),
        r.table(
            ["#", "Constraint"],
            [
                ["C1", "Fixed academic schedule: 14/09/2026 to 27/12/2026; the closing date cannot move."],
                ["C2", "No budget: free, open-source or student-licensed tools only."],
                ["C3", "Five students; other courses compete for their hours."],
                ["C4", "Requirements and acceptance are decided by the Product Owner."],
                ["C5", "Speech and language models run in-house (ADR-0026, ADR-0054); a CPU-only server limits model size."],
                ["C6", "Vietnamese only; no automatic recording; recordings and voice data are personal data under "
                       "Law 91/2025/QH15 and need consent and deletion rules."],
                ["C7", "Fixed stack: ASP.NET Core 10, React 19 + TypeScript and Vite, PostgreSQL 17 via EF Core 10, Redis for "
                  "refresh tokens, LiveKit (Report 2 Section II.6.3)."],
                ["C8", "Council reviews gate the project: every comment of the first review (17/10) must be fixed before "
                       "the second review, or the team may not defend (supervisor MOM-08, 21/10/2026; second review in week 13 there, week 12 in MOM-12 "
                        "and MOM-13). Report 2 constraint 8 (internal electronic signatures) fell away with the "
                        "signing drop; TODO(PO): see scope changes."],
            ],
            widths_in=[0.4, 5.9],
        ),
    ]
    r.replace_section(h, els)


def summary(r: Report):
    h = r.find_heading("5. Project Plan Summary")
    els = [
        r.text("Effort was planned as 300 person-days of effective capacity (5 members x 40 h x 15 weeks, minus a "
               "20% buffer): 20 for Initial Planning and 40 for each of six iterations and the deployment phase. "
               "Sections 5.1-5.3 give the milestones planned against actual, the risks and who is responsible."),
    ]
    r.replace_section(h, els)


def schedule(r: Report):
    h = r.find_heading("5.1 Project Schedule")
    rows = [
        ["Initial Planning: topic, requirement baseline, repository, architecture, feasibility",
         "W2 (14-20/09)", "W2-W3",
         "Topic finalised 16/09 (MOM-02); TrustSoft requirements session 18/09 (MOM-03); TODO(LocHD): confirm repository "
         "creation and Jira/GitHub configuration dates from the app git history and Jira.", "Team; KhanhPG (requirements); LocHD (repository)"],
        ["Iteration 1: sign-in, users, roles (FE-01 to FE-03)",
         "W3-W4 (to 04/10)", "W3-W4",
         "TODO(LocHD): confirm merge dates of role/permission work (PR #13, #19) from the app git history; planned W3-W4.", "LocHD; HaiTV"],
        ["Mock defense (first trial)", "W5-W6", "W6 (14/10)",
         "Trial defense delivered 14/10; every council comment kept as an item to fix.", "Team"],
        ["Iteration 2: meetings, agenda, participants, documents, notifications (FE-04 to FE-07, FE-13)",
         "W5-W6 (to 18/10)", "W5-W8",
         "SLIP. Still In Progress in the W6 report (15/10); TODO(HuyNQ): confirm merge dates of document viewing "
         "(PR #134) and meeting documents/voting (PR #202) from the app git history. See slip 1 below.", "HuyNQ (meeting back end); HaiTV; HuyNT (notifications)"],
        ["Iteration 3: audio and AI processing (FE-09, FE-10)",
         "W7-W8 (to 01/11)", "W7-W11",
         "SLIP. Recording method decided 19/10 (MOM-11); local model hosting decided 21/10 (MOM-08) with ADR-0026/ADR-0027 "
         "dated 23/10; AI draft demoed 04/11 (MOM-13); TODO(LocHD): confirm load test (03/11), recording fixes (13/11) "
          "and transcription refactor (16/11) from the app git history. "
         "See slip 2.", "LocHD; HuyNQ; HaiTV"],
        ["Iteration 4: online meeting room (FE-08)", "W9-W10 (to 15/11)", "W9-W11",
         "Room over LiveKit shown on 12/11 (W10); Raise Hand and the Chair's invitation to speak shown on 20/11 (W11).",
         "HaiTV; LocHD"],
        ["Iteration 5: voting and minutes (FE-11, FE-12)", "W11-W12 (to 29/11)", "W9-W11",
         "AHEAD. AI draft shown 04/11 (MOM-13); voting, submit for approval, approve and issue shown 12/11 (MOM-14); "
         "TODO(PO): confirm the 14/11 signing drop, which removed the largest part of FE-12.", "KhanhPG (UC); LocHD; HuyNQ"],
        ["Iteration 6: reporting (FE-15)", "W13-W14 (to 13/12)", "W10-W12",
         "TODO(LocHD): confirm UC-70 specification and the meeting-report back end with Excel export (SCRUM-38, 09/11) "
          "from Jira and the app git history; screens exist. "
         "(SCRUM-38); screens exist. Acceptance still to come.", "LocHD; HaiTV"],
        ["Second council review", "W12", "Pending",
         "Supervisor on 04/11: last gate before the defense; app must be complete, tested and shown live. "
         "The 21/10 minutes said W13.", "Team"],
        ["Deployment: acceptance, stabilisation, Reports 5.5, 6, 7, presentation (14-27/12)",
         "W15-W16", "Pending", "Full live demo rehearsed on seeded data (20/11). TODO(HuyNT): acceptance result.",
         "HuyNT (acceptance); KhanhPG (reports); team"],
    ]
    els = [
        r.text("Table 2.1: milestones, planned against actual. Weeks are course weeks (W1 = 07/09/2026); Report 2 "
               "numbers the same weeks one lower. Actuals are taken from the weekly report, the minutes and the "
               "merge history of `app`, as of 23/11/2026 (W12).", italic=True),
        r.table(["Milestone", "Planned", "Actual", "Evidence / note", "Owner(s)"], rows,
                widths_in=[1.6, 0.8, 0.6, 2.6, 0.9], size=8),
        r.text("Slip 1 - Iteration 2 ran about two weeks late. The Meeting Session plan of W5 was abandoned (the "
               "meeting became one independent entity); twelve design diagrams (SCRUM-71-82) stayed in review; the "
               "personal document library had no owner in W6; and Guest access depended on a real OTP and session "
               "(SCRUM-20). Absorbed by: dropping the Meeting Session work, assigning an owner to the library, and "
               "moving participant, agenda and lifecycle work into W7 with HuyNQ accountable.", ),
        r.text("Slip 2 - Iteration 3 ran about three weeks late. The environment for the models was open until the "
               "supervisor advised running them locally (21/10); Voice ID was added on 23/10; the first 7B local "
               "model left agenda items blank on long transcripts (W9, W10). Absorbed by moving the room and the "
               "minutes work in parallel and by dropping signing (14/11), which paid back the lost time. "
               "Iterations 5 and 6 finished earlier than planned for that reason."),
    ]
    r.replace_section(h, els)


def risks(r: Report):
    h = r.find_heading("5.2 Risk and Mitigations")
    rows = [
        ["R01", "Online room is the team's first real-time media work", "High / High",
         "Built on an open-source media server (LiveKit) with a feasibility study first.",
         "Mitigated: room runs end to end, shown 12/11; load test of calls and recording 03/11 (HaiTV, LocHD)."],
        ["R02", "Vietnamese transcription too inaccurate to summarise", "Medium / High",
         "Own-recording evaluation, editable transcript, early iteration.",
         "Materialised in part: a 7B local model leaves blanks on long transcripts; summary is per agenda item "
         "and editable, a person approves (LocHD)."],
        ["R03", "Where the speech and language models run", "Low / High",
         "Decided in-house (ADR-0026); model behind one internal interface.",
         "Mitigated: model on a team machine behind an OpenAI-compatible API; the app works with the worker off."],
        ["R04", "Too many features for five members in fifteen weeks", "High / High",
         "Must-have / should-have split, re-plan on actual effort.",
         "Materialised: Iterations 2 and 3 slipped; the PO dropped signing, UC-69, UC-09, FE-14 (14-15/11). "
         "Accountable: HaiTV."],
        ["R05", "Uneven workload, single-owner modules", "Medium / Medium",
         "Pairing; effort logged in Jira; reassign each planning.",
         "Materialised once: library without an owner in W6; assigned in W7 (HaiTV, KhanhPG)."],
        ["R06 (new)", "Council comments not fixed before the second review", "Medium / High",
         "Keep each comment as a ticket; supervisor demo each week.",
         "Open: first-review functions shown 28/10 and judged to basically meet the council (supervisor)."],
        ["R07 (new)", "Voice data and recordings are personal data", "Medium / High",
         "Consent before recording and before Voice ID, encrypted voice samples, deletion on withdrawal (ADR-0027).",
         "Mitigated in design; verified in Chapter 5."],
    ]
    els = [
        r.text("R01-R05 are the risks of Report 2 with what actually happened; R06-R07 appeared during the project. "
               "Probability / Impact as assessed in Report 2.", italic=True),
        r.table(["#", "Risk", "Prob / Impact", "Planned mitigation", "Outcome (owner)"], rows,
                widths_in=[0.5, 1.5, 0.7, 1.8, 1.8], size=8),
    ]
    r.replace_section(h, els)


def raci(r: Report):
    h = r.find_heading("5.3 Responsibility Assignments")
    M = ["HaiTV", "KhanhPG", "LocHD", "HuyNQ", "HuyNT"]
    rows = [
        ["Documents", "Report 1: Project Introduction", "C", "A/R", "C", "C", "C"],
        ["", "Report 2: Project Management Plan", "A/R", "C", "C", "C", "C"],
        ["", "Report 3: SRS", "R", "A/R", "R", "R", "C"],
        ["", "Report 4: Software Design Specification", "A/R", "R", "R", "R", "I"],
        ["", "Report 5: Test documents and reports", "C", "R", "R", "R", "A/R"],
        ["", "Reports 6, 7, presentation and demo", "A/R", "R", "R", "R", "R"],
        ["Analysis and design", "System architecture", "C", "C", "A/R", "R", "I"],
        ["", "Use case diagram and main workflow", "R", "A/R", "R", "R", "C"],
        ["", "Database design", "C", "I", "A/R", "R", "I"],
        ["", "Class, sequence and package diagrams", "R", "R", "A/R", "R", "I"],
        ["", "UI design and mock-up", "C", "A/R", "I", "R", "C"],
        ["", "Audio / AI design (FE-09, FE-10)", "C", "I", "A/R", "R", "C"],
        ["", "Online room design (FE-08)", "C", "R", "A/R", "R", "C"],
        ["Implementation", "Technology and tools", "A", "C", "R", "C", "C"],
        ["", "Coding convention, definition of done", "C", "R", "A/R", "R", "R"],
        ["", "Frontend implementation", "A/R", "R", "R", "R", "R"],
        ["", "Backend implementation", "R", "R", "A/R", "R", "R"],
        ["", "Pull-request review, source control", "R", "I", "A/R", "R", "I"],
        ["", "CI and deployment to the test server", "R", "I", "A/R", "R", "C"],
        ["", "Tracking work in Jira", "A/R", "R", "R", "R", "R"],
        ["Test", "Test plan and strategy", "C", "I", "I", "I", "A/R"],
        ["", "Unit tests", "R", "R", "R", "R", "A"],
        ["", "Integration and regression tests", "I", "R", "R", "R", "A/R"],
        ["", "System tests", "C", "R", "R", "R", "A/R"],
        ["", "Acceptance scripts and support", "R", "R", "R", "R", "A/R"],
        ["", "Defect logging and tracking", "R", "R", "R", "R", "A/R"],
        ["Project management", "Iteration planning and evaluation", "A/R", "R", "R", "R", "R"],
        ["", "Communication with the supervisor", "A/R", "I", "C", "I", "I"],
        ["", "Risks and change requests", "A/R", "C", "C", "C", "C"],
        ["", "Document and configuration management", "R", "A/R", "R", "R", "R"],
    ]
    els = [
        r.text("R = Responsible, A = Accountable, C = Consulted, I = Informed. Members: HaiTV = Tran Viet Hai "
               "(Team Leader, Frontend Lead); KhanhPG = Pham Gia Khanh (Business Analyst, UI/UX); LocHD = Hoang "
               "Duc Loc (Backend Lead); HuyNQ = Nguyen Quang Huy (architecture, backend and frontend); HuyNT = "
               "Nguyen Truong Huy (Test Lead, QA). The assignment is the one baselined in Report 2; the "
               "Supervisor (Nguyen Quang Hung) and the Product Owner (Dinh Van Toi) sit outside the chart.",
               italic=True),
        r.table(["Area", "Work package"] + M, rows, widths_in=[1.0, 2.5, 0.55, 0.65, 0.55, 0.55, 0.5], size=8),
        r.text("In practice the weekly report of W6 shows the same split by work item: HuyNQ on participants, "
               "agenda and meeting lifecycle; LocHD on voting design and the calendar; HaiTV on the online room and "
               "front-end stability; HuyNT on notifications and email; KhanhPG on use case specifications and "
               "diagrams."),
    ]
    r.replace_section(h, els)
    # The template leaves an empty Heading 3 paragraph between §5.3 and the
    # "Chapter 3" heading. replace_section stops at the next heading, so that
    # empty paragraph survives each run — drop it (idempotent: gone on re-run).
    ch3 = r.find_heading("Chapter 3 - Requirements Analysis")
    kids = r.children()
    for el in kids[kids.index(h) + 1:kids.index(ch3)]:
        if is_heading(el) and not _text(el).strip():
            r.body.remove(el)
