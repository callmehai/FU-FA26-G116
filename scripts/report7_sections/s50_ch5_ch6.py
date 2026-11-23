"""Chapter 5 (Delivery Package) and Chapter 6 (Conclusion and Future Work).

Facts come from docs/08-reports/test-reports (Report 5.x), Report 2 (objective metrics), AGENTS.md and the ADRs.
Where the repo holds no measured number, a TODO line is left instead of an estimate.
"""

from __future__ import annotations

from report7_lib import Report

REPO = "https://github.com/callmehai/FU-FA26-G116.git"


def _fill(r: Report, heading: str, elements):
    r.replace_section(r.find_heading(heading), elements)


def fill(r: Report):
    fill_ch5(r)
    fill_ch6(r)


# ------------------------------------------------------------------ Chapter 5
def fill_ch5(r: Report):
    # bracketed instructions sitting directly under the chapter and the "1." heading
    for title in ("Chapter 5 – Delivery Package", "1. Test & Quality Summary"):
        h = r.find_heading(title)
        for el in r.section(h):
            r.body.remove(el)
    # Test strategy / environment / milestones (school-template depth, repo truth only)
    h1 = r.find_heading("1. Test & Quality Summary")
    r.insert_after(h1, [
        r.text("Test strategy follows Report5.0_Test_Documentation.docx (§§2–3) and the Report 2 test process "
               "(§§2.3–2.6). Each level has its own workbook in docs/08-reports/test-reports/; only a round that was "
               "really executed and recorded counts, every other case stays Pending.", italic=False),
        r.table(
            ["Level", "Objective and technique (repo source)", "Workbook"],
            [
                ["UT", "Developer-owned xUnit per production function, in the same iteration as the code, run on every "
                 "push through CI (Report 2 §2.3). Each function is covered for Normal / Abnormal / Boundary; expected "
                 "results always state Return / Exception / Log Message (test-reports AGENTS.md §6). 604 UTC "
                 "(149 functions), Round #1 run 21-22/11/2026.",
                 "Report5.1_Unit_Test.xlsx"],
                ["IT (UI)", "Tester in Chrome/Edge on the test build; frontend + backend + database exercised together "
                 "through the browser, checking both sides of the integration (AGENTS.md §2). Workbook v1.2 holds 411 "
                 "cases on 35 sheets (all Pending); the feature plan in cases/ defines 875 UI cases (pending "
                 "harmonisation).",
                 "Report5.2_Integration_Test.xlsx"],
                ["IT (API)", "Tester with Postman / curl (docs/03-api/postman/); one group per endpoint, minimum: happy "
                 "path, each validation MSGnn, 401 without token, 403/404 without rights, state/conflict rules and side "
                 "effects (AGENTS.md §3). 1,537 cases across 15 sheets (FE-01..FE-16 except FE-14); Round #1 rows blank, "
                 "not executed.",
                 "Report5.2.2_Integration_Test_API_Test.xlsx"],
                ["ST", "Led by the Test Lead on the staging package, full end-to-end flow from scheduling to minutes "
                 "(Report 2 §2.5). 9 workflows; every Round #1 row is NotRun / Pending, not executed as a recorded round.",
                 "Report5.3_System_Test.xlsx"],
                ["AT", "Manual scripts on staging with a prepared demo data set including real Vietnamese meeting audio; "
                 "each script run by a member who did not build the feature, then formal acceptance by the PO "
                 "(Report 2 §2.6). 8 scripts UAT-01..UAT-08, all Not run; Statistics sheet states no customer response; "
                 "criteria 9-13 and the acceptance decision are Pending.",
                 "Report5.4_Acceptance_Test.xlsx"],
                ["Regression", "From Iteration 4 the integration suite is re-run as a regression pass over earlier "
                 "increments (Report 2 §2.4); Report 5.0 §2.1.4 requires re-running affected tests after each change "
                 "with separate evidence per run.",
                 "Recorded in 5.1/5.2 workbooks per round"],
            ],
            widths_in=[0.7, 4.0, 1.6], size=8),
        r.text("Test environment: the staging test server and the developer/CI loop described in Report 2 "
               "(Table 11, Tools/Infrastructure), deploy/ and the Report 5.0 test plan. No separate performance rig "
               "exists outside the staging server and tests/load/."),
        r.table(
            ["Item", "Environment used for testing (repo source)"],
            [
                ["Staging server", "AWS Lightsail, Docker Compose (app, PostgreSQL, Redis, LiveKit, Caddy); app runs as "
                 "Staging, migrates as a deploy step after a pg_dump backup (deploy/README.md, ADR-0047). Branches: app "
                 "-> app.trustcabinet.workspace-hub.space (main branch, ADR-0068)."],
                ["Software stack", "Backend ASP.NET Core 10 (.NET SDK pinned by global.json 10.0.401), frontend React 19 + "
                 "Vite + TypeScript, PostgreSQL 17 + Redis 8 via EF Core code-first (62 migrations), SeaweedFS/R2 file "
                 "store, self-hosted CPU speech worker, team-machine AI model over Tailscale (deploy/README.md §§9-11)."],
                ["Test clients", "Chrome/Edge on the test build (UI); Postman / curl against docs/03-api (API); xUnit "
                 "+ Microsoft.AspNetCore.Mvc.Testing in CI with real PostgreSQL/Redis services (Report 2 §§2.3-2.4)."],
                ["Test data", "Seeded demo accounts and meetings (--seed-demo); seeded roles/capabilities. Only test "
                 "data on the server; no real personal data (deploy/README.md)."],
            ],
            widths_in=[1.2, 5.1], size=8),
        r.text("TODO(Test Lead): record the exact build/commit and database seed used for each executed round; the "
               "workbooks name the test build but no commit hash is recorded in them.", italic=True),
        r.text("Test milestones come from the Report 2 schedule (Section I.3) and the weekly report "
               "(Project_Weekly_Report_G116.xlsm, weeks W1-W6 to 18/10/2026). Actual execution exists only for the "
               "Round #1 unit run; the weekly report predates all test rounds, so no execution date can be taken "
               "from it."),
        r.table(
            ["Milestone (Report 2 plan)", "Planned date", "Actual"],
            [
                ["Report 5.0 test planning (Iter. 2)", "18/10/2026", "Workbook created 23/10/2026 (manifest date)."],
                ["Report 5.1 unit tests, increments 1-5", "04/10 - 29/11/2026", "Round #1 run 21-22/11/2026: 604/604 passed."],
                ["Report 5.2 integration (Iters. 3-5)", "01/11, 15/11, 29/11/2026", "Not executed; all 411 UI + 1,537 API cases Pending."],
                ["Report 5.3/5.4 system tests (Iter. 6)", "13/12/2026", "Not executed; 9 workflows Pending."],
                ["Acceptance session (deployment)", "20/12/2026", "Not run; 8 UAT scripts Not run, no PO decision."],
            ],
            widths_in=[2.6, 1.4, 2.3], size=8),
        r.text("TODO(Test Lead / PM): enter actual start/end dates of each test round once executed (source: Jira and "
               "the weekly report weeks after W6); Rounds #2 and #3 rows in every workbook are empty.", italic=True),
    ])
    # 1.1 Test results ------------------------------------------------------
    _fill(r, "1.1 Test Results", [
        r.text("Results recorded in the Report 5 workbooks (docs/08-reports/test-reports). Only a run that was really executed "
               "and recorded is counted; every other case stays Pending."),
        r.table(
            ["Document", "Level", "Defined", "Passed", "Failed", "Pending", "Notes"],
            [
                ["Report 5.1 Unit Test", "UT", "604 UTC (149 functions)", "604", "0", "0",
                 "Round #1 run 21-22/11/2026 on branch app by Pham Gia Khanh; each UTC is bound to an automated test."],
                ["Report 5.2 Integration Test (UI)", "IT", "411 cases (35 sheets, workbook v1.2)", "0", "0", "411",
                 "Workbook in repo holds 411 cases, all Pending; the feature plan in cases/ defines 875 UI cases (pending harmonisation)."],
                ["Report 5.2.2 Integration Test (API)", "IT", "1,537 cases", "0", "0", "1,537",
                 "Verified 1,537 cases in workbook (15 sheets); Round #1 rows blank, not executed."],
                ["Report 5.3 System Test", "ST", "9 workflows", "0", "0", "9",
                 "Each workflow row is NotRun / Pending in Round #1; not executed as a recorded round."],
                ["Report 5.4 Acceptance Test", "AT", "8 UAT scripts", "0", "0", "8",
                 "UAT-01..UAT-08 are NotRun; Statistics sheet states 'No customer feedback or acceptance'; Customers Feedbacks sheet is empty and criteria 9-13 (fit, ease, improvements, satisfaction, acceptance decision) are Pending."],
            ],
            widths_in=[1.35, 0.4, 1.2, 0.55, 0.5, 0.6, 1.7], size=8),
        r.text("Rounds #2 and #3 are empty: no later round has been run."),
        r.text("TODO(Test Lead): enter Round #1 results of 5.2, 5.2.2, 5.3 and 5.4 once the tester has executed them, "
               "and the measured unit test code coverage (Report 2 target: 70%); coverage is not measured in the repo today.",
               italic=True),
        r.text("Note: round1-report/UNIT.md still states the pre-reduction figure 818 UTC; commit 3683a2e1 reduced the set to 604 UTC "
               "covering all 149 functions (verified by counting cases/FE-*/unit/*.json). 604 is the current figure; UNIT.md is stale and left untouched as a test-report source.", italic=True),
    ])

    # 1.2 / 1.3 defects -----------------------------------------------------
    _fill(r, "1.2 Test Levels’ Defects", [
        r.table(
            ["Metric", "Review", "UT", "IT", "ST", "AT", "Total", "Note"],
            [
                ["Total Defects", "TODO", "0", "TODO", "TODO", "TODO", "15 (all Done)", "Verified on Jira 23/11/2026: 15 bug tickets, all Done; High 2 (SCRUM-133, SCRUM-147), Medium 10, Low 3. Per-phase split is not recorded on the tickets."],
                ["Open Defects (Critical)", "TODO", "0", "TODO", "TODO", "TODO", "0", "0 open: all 15 verified Done on Jira 23/11/2026."],
                ["Open Defects (Major)", "TODO", "0", "TODO", "TODO", "TODO", "0", "0 open: all 15 verified Done on Jira 23/11/2026."],
            ],
            widths_in=[1.5, 0.55, 0.4, 0.4, 0.4, 0.4, 0.5, 2.1], size=8),
        r.text("TODO(Test Lead): per-phase defect counts (review / IT / ST / AT) cannot be determined from Jira: the 15 bug tickets carry no [qa-bot] phase comment. "
               "Ask the finders which phase each defect belongs to and fill the TODO cells. Report 2 planned 30 / 40 / 30 / 15 / 5 defects for review / UT / IT / ST / AT.",
               italic=True),
    ])
    _fill(r, "1.3 Work Product’s Defects", [
        r.text("TODO(BA): defects per work product (software package, Reports 1-6) at review, UT, IT, ST and AT come from the "
               "review log and Jira; no such count exists in the repo, so none is stated here.", italic=True),
    ])

    # 1.4 UAT ---------------------------------------------------------------
    _fill(r, "1.4 User Acceptance Tests (UAT)", [
        r.table(
            ["Script ID", "Title", "Status"],
            [
                ["UAT-01", "Administer user access", "Not run"],
                ["UAT-02", "Prepare a meeting", "Not run"],
                ["UAT-03", "Review meeting requests", "Not run"],
                ["UAT-04", "Receive correct invitation", "Not run"],
                ["UAT-05", "Understand schedule change", "Not run"],
                ["UAT-06", "Protect meeting information", "Not run"],
                ["UAT-07", "Reset forgotten password", "Not run"],
                ["UAT-08", "Use supported browsers", "Not run"],
            ],
            widths_in=[0.9, 4.2, 1.2], size=9),
        r.text("Full UAT scripts are in Report5.4_Acceptance_Test.xlsx. The workbook records no customer response and "
               "no acceptance decision."),
        r.text("TODO(Test Lead / PO): run the UAT session with the Product Owner and fill Status and the Business Flow column "
               "(BF-01..BF-04) per script.", italic=True),
    ])

    # 2 Source & build ------------------------------------------------------
    _fill(r, "2. Source & Build Artefacts", [
        r.table(
            ["Item", "Location / Format", "Notes"],
            [
                ["Application source code", f"Git repository {REPO}",
                 "Main branch is app (ADR-0068); main is the frozen three-project demo. backend/ (.NET 10, four projects: Domain, "
                 "Application, Infrastructure, Web), frontend/ (React 19 + Vite + TypeScript), workers/speech/ (speech worker)."],
                ["Release tag", "TODO(BE Lead): final release tag", "Only the tag demo-2026-11-18 exists (verified via git tag on 23/11/2026)."],
                ["Build", "Dockerfile (repository root)", "One image: frontend build + backend. SDK pinned by global.json (10.0.401)."],
                ["Continuous integration", ".github/workflows/ci.yml",
                 "Backend build with -warnaserror, backend tests on PostgreSQL, frontend lint/build/unit tests, speech worker tests."],
                ["Deployment", "deploy/ (docker-compose.yml, Caddyfile, livekit.yaml) and .github/workflows/deploy.yml",
                 "Shared test server on AWS Lightsail, runs as Staging; migration is a deploy step after a pg_dump backup (ADR-0047)."],
                ["Test automation", "tests/TrustCabinet.Application.Tests, tests/TrustCabinet.Web.Tests, tests/qa, tests/load, tests/ai",
                 "xUnit use case and integration tests; browser QA runner; load and AI benchmark scripts."],
            ],
            widths_in=[1.4, 2.3, 2.6], size=8),
    ])

    # 3 Database ------------------------------------------------------------
    _fill(r, "3. Database Artefacts", [
        r.table(
            ["Item", "File", "Description"],
            [
                ["Schema (code-first)", "backend/TrustCabinet.Infrastructure/Migrations/",
                 "62 EF Core migrations from 20260919150718_InitialCreate to 20261122163710_TranscriptSegmentSpeakerAssigned, "
                 "with TrustCabinetDbContextModelSnapshot.cs. PostgreSQL; applied by dotnet TrustCabinet.Web.dll --migrate."],
                ["Entity configuration", "backend/TrustCabinet.Infrastructure/Configurations/",
                 "One IEntityTypeConfiguration per entity; model described in docs/02-design/02-erd.md."],
                ["Seed data", "backend/TrustCabinet.Infrastructure/Seed/",
                 "ReferenceDataSeeder (roles, capabilities) and the development data seeders (users, meetings, agenda, participants); "
                 "development seeds run only in Development."],
            ],
            widths_in=[1.3, 2.3, 2.7], size=8),
    ])

    # 4 Documentation -------------------------------------------------------
    _fill(r, "4.1 SDLC Documentation", [
        r.table(
            ["Document", "File", "Description"],
            [
                ["Project Introduction", "Report_1_Project_Introduction.docx", "Context, problem, solution, feature list FE-01..FE-16."],
                ["Project Planning", "Report_2_Project_Management_Plan.docx", "Iterative incremental plan, schedule, risks, RACI, objective metrics."],
                ["Software Requirement Specification", "Report_3_Software_Requirement_Specification.docx", "Use cases, business rules, messages."],
                ["Software Design Specification", "Report-4_Software Design Specification.docx", "Architecture, database, class and sequence diagrams."],
                ["Test documentation", "Report5.x (see 4.2)", "Test plan and workbooks."],
                ["Software User Guides", "Report-6_Software User Guides.docx", "Installation and use per role."],
                ["Final Project Document", "Report-7_Final Project Document.docx", "This document."],
                ["Decision records", "docs/05-decisions/ (79 ADRs)", "Technical decisions, never edited once accepted."],
            ],
            widths_in=[1.7, 2.6, 2.0], size=8),
        r.text("All sources are in docs/08-reports/source/; the Markdown copies in docs/08-reports/ are generated and not edited by hand."),
    ])
    _fill(r, "4.2 Test Artefacts", [
        r.table(
            ["Item", "File", "Description"],
            [
                ["Test documentation", "Report5.0_Test_Documentation.docx", "Scope, strategy, plan, test cases and report."],
                ["Unit Test", "Report5.1_Unit_Test.xlsx", "149 function sheets, 604 UTC."],
                ["Integration Test (UI)", "Report5.2_Integration_Test.xlsx", "411 cases on 35 sheets (workbook v1.2); the feature plan in cases/FE-xx/ui.json defines 875 UI cases."],
                ["Integration Test (API)", "Report5.2.2_Integration_Test_API_Test.xlsx", "1,537 cases per endpoint, Feature FE-01..FE-16."],
                ["System Test", "Report5.3_System_Test.xlsx", "9 workflows, three-round table."],
                ["Acceptance Test", "Report5.4_Acceptance_Test.xlsx", "8 UAT criteria, feedback sheets (empty)."],
                ["Generation and evidence", "test-reports/manifest.json, inventory.json, execution-evidence.json, round1-report/",
                 "Source fingerprint, inventory and Round #1 unit test report."],
            ],
            widths_in=[1.5, 2.6, 2.2], size=8),
        r.text("Location: docs/08-reports/test-reports/."),
    ])

    # 5 Known issues --------------------------------------------------------
    _fill(r, "5.1 Open Defects Accepted for Release", [
        r.text("None. The Round #1 unit test run found no defect, and all 15 product defects recorded on Jira (SCRUM-65, 66, 67, 105, 133, 134, 147, 148, 149, 150, 151, 210, 211, 212, 213) are Done; verified 23/11/2026."),
        r.text("Per-phase allocation of the 15 defects is not recorded on the Jira tickets (no [qa-bot] phase comment); see section 1.2.", italic=True),
    ])
    _fill(r, "5.2 Known Limitations", [
        r.table(
            ["Limitation", "Detail", "Mitigation"],
            [
                ["Signing minutes dropped", "Signing the minutes, even simulated (UC-62, UC-63), was dropped by the PO on 2026-11-14; "
                 "the Chair approval issues the minutes.", "Approval and issue are recorded in the history of the minutes."],
                ["Dashboard, workspace, account lock dropped", "Personal dashboard and meeting workspace (UC-69) and locking an account "
                 "after wrong passwords (UC-09) were dropped on 2026-11-15.", "The per-minute sign-in limit (MSG414) remains."],
                ["No task tracking", "Assignees, due dates and completion of action items are out of scope; TrustCabinet stops at the approved minutes.",
                 "Follow-up is done outside the system."],
                ["No real HSM / eOffice / air-gapped", "Real HSM signing, eOffice integration and air-gapped deployment are out of scope.", "-"],
                ["One organisation, no org units", "No multi-tenancy and no department hierarchy (ADR-0004).", "Roles and capabilities scope access."],
                ["Speech quality", "Vietnamese transcription, speaker separation and Voice ID run on in-house models "
                 "(ADR-0026, ADR-0074); accuracy is below a commercial service.", "Transcript is editable; manual edits are kept on re-run (ADR-0077)."],
                ["Local AI for minutes", "The minutes draft runs on a local language model (ADR-0054) with a fallback machine (ADR-0063).",
                 "The Secretary reviews and edits every draft."],
                ["Test server only", "Deployed on one shared Lightsail test server behind a Content-Security-Policy; no production-grade scaling.",
                 "Sized for the demonstration."],
                ["Test evidence incomplete", "IT, ST and AT rounds are not executed or recorded (section 1.1).", "See TODO in section 1.1."],
            ],
            widths_in=[1.5, 3.0, 1.8], size=8),
    ])


# ------------------------------------------------------------------ Chapter 6
def fill_ch6(r: Report):
    _fill(r, "1. Business/Organizational Evaluation", [
        r.text("No pilot was run in a real organisation, so there is no measured as-is versus to-be comparison, no stakeholder "
               "survey and no adoption feedback. The system has been exercised only by the team and in the demonstration on the "
               "shared test server; the acceptance feedback sheet of Report 5.4 is empty. No business benefit is claimed here."),
        r.table(
            ["Metric", "As-Is", "With TrustCabinet"],
            [
                ["Time to prepare a meeting and distribute documents", "Not measured", "Not measured"],
                ["Time to produce approved minutes", "Not measured", "Not measured"],
                ["Stakeholder satisfaction (1-5)", "Not collected", "Not collected"],
            ],
            widths_in=[3.3, 1.5, 1.5], size=9),
        r.text("TODO(PO / BA): run a short pilot or acceptance session with representative users, measure the metrics above "
               "against the as-is of Chapter 1, and enter the figures and feedback here.", italic=True),
    ])

    _fill(r, "2.1 Achievements", [
        r.text("Objectives are the project metrics committed in Report 2 (Section I.5) and the fifteen features baselined in Report 1. "
               "'Actual' is stated only where the repo holds a measurement."),
        r.table(
            ["Objective (Report 2)", "Planned", "Status", "Evidence"],
            [
                ["Deliver the 15 baselined features (FE-01..FE-13, FE-15, FE-16)", "All", "Achieved with changes",
                 "The meeting loop runs end to end up to issued minutes (AGENTS.md); signing and some items were dropped by the PO."],
                ["Requirement completeness", "90%", "Not measured",
                 "TODO(BA): implemented and accepted requirements / requirements baselined in Report 3."],
                ["Unit test defects", "40", "0 found in Round #1", "Report 5.1: 604 UTC, all Passed."],
                ["Unit test code coverage", "70%", "Not measured", "No coverage figure in the repo."],
                ["Integration / system / acceptance defects", "30 / 15 / 5", "0 found (rounds not executed)", "IT/ST/AT Round #1 not executed; see Chapter 5, 1.1-1.2."],
                ["Review defects", "30", "Not recorded", "Review log is in Jira."],
                ["Effort usage", "300 person-days", "Not recorded", "TODO(PM): the workbook in the repo covers only weeks W1-W6 (to 18/10/2026) with no person-day totals."],
                ["Timeliness", "90%", "Not recorded", "TODO(PM): on-time deliverables / total deliverables."],
                ["Transcription quality threshold (agreed in initial planning)", "Threshold in Report 3", "See Report 3",
                 "Benchmarks in tests/ai; thresholds tuned after the 21/11 trial (ADR-0074)."],
            ],
            widths_in=[2.3, 0.9, 1.1, 2.0], size=8),
        r.text("The repository holds about 1,700 commits since 2026-09-18, 62 EF migrations and 79 ADRs; the test server deploys "
               "automatically from branch app."),
    ])

    _fill(r, "2.2 Limitations", [
        r.text("Technical: the limitations of Chapter 5, section 5.2 apply (dropped signing, no task tracking, in-house speech models "
               "of limited accuracy, single test server). Integration, system and acceptance testing were not executed and recorded."),
        r.text("Organisational: no pilot with a real organisation was held, so adoption, training needs and the as-is comparison are "
               "unknown; the PO acceptance session is still open; Jira defect and effort figures have not been transferred into the reports."),
    ])

    _fill(r, "3. Future Work", [
        *r.bullets([
            "Run the pending test rounds (IT, ST) and the UAT session with the Product Owner, then close the TODO metrics of Chapter 5.",
            "Pilot in one real organisation and measure meeting preparation time, time to approved minutes and user satisfaction.",
            "Real digital signing of the minutes with an HSM or a certificate authority (dropped in scope, SCRUM-37).",
            "Action-item tracking after the minutes (assignees, due dates, completion), kept outside scope so far.",
            "Integration with eOffice and an air-gapped deployment option; improve Vietnamese transcription and speaker separation accuracy.",
        ]),
    ])
