# AGENTS.md — Report 5.1 Unit Test, Report 5.2 / 5.2.2 Integration Test

Instructions for any agent that rewrites `Report5.1_Unit_Test.xlsx` (unit, row layout),
`Report5.2_Integration_Test.xlsx` (UI integration) or
`Report5.2.2_Integration_Test_API_Test.xlsx` (API integration). English, like the other agent
instructions in this repo. Read [README.md](README.md) and the root [AGENTS.md](../../../AGENTS.md) first.

## 1. What is wrong today

- **Not split by Feature.** 5.2.2 has sheets `Access, Agenda, Authentication, Core, Meetings, …` —
  the xUnit folder names. 5.2 has its own ad-hoc list (`Create Meeting`, `Approval`, …) and stops at
  notifications. The system is split into **FE-01 … FE-16**
  ([00-master-use-case-inventory.md](../../01-requirements/00-master-use-case-inventory.md)).
- **5.2.2 is a dump of the C# tests.** IDs are hashes (`TC-ABB2CD5300B6`), the procedure is
  "Run: dotnet test --filter …", the expected result is `Assert.Equal(...)` lines, the precondition is
  "TestServer; factory may use fakes". A tester or the council cannot read or execute it.

## 2. Target shape

Both workbooks keep the template exactly (Cover, Test Cases, Test Statistics, then one sheet per
feature; header block rows 1–8; columns `Test Case ID | Description | Procedure | Expected Results |
Pre-conditions | Round 1 | Test date | Tester | Round 2 | … | Note`). Only the content changes.

| | 5.2 Integration Test (UI) | 5.2.2 Integration Test — API |
|---|---|---|
| Who runs it | Tester in Chrome/Edge on the test build | Tester with Postman / curl (see `docs/03-api/postman/`) |
| Sheet | one per FE, short name: `Meeting Management` (≤31 chars, no `/ \ ? * [ ]`) | same |
| Group row inside sheet | one per **function / sub-flow** the user recognises: `Google Sign-in`, `Token Refresh`, `Create Meeting` (each maps to one UC or a slice of one; UC id goes in Note) | one per **endpoint**: `POST /api/meetings` |
| Who decides sheets and groups | [`cases/sheets.json`](cases/sheets.json) — an authoring agent never adds, renames or reorders a sheet or a group | same; `check` fails if a route the backend maps belongs to no sheet |
| Case ID | `<CODE>_FT01`, `<CODE>_FT02`… | `<CODE>_API_IT01`, `<CODE>_API_IT02`… |
| Description | one line: behaviour + condition, e.g. "Login blocked for inactive account." | "Verify `<METHOD> <route>` … when …" |
| Procedure | numbered screen actions from the start page (`1. Go to /login. 2. Click "Đăng nhập bằng Google"…`), Vietnamese UI labels quoted exactly; how to force a state (expire token, deactivate user) is a step | numbered: method + URL, headers, body |
| Expected | **both sides of the integration**: what the backend does (status, `MSGnn`, cookies/token, row saved) **and** what the UI shows (redirect, banner text, what stays hidden), plus persistence after reload | status, response body (`code: "MSGnn"`, key fields), side effects |

5.2 is the frontend + backend + database exercised together through the browser — not a pure UI
check and not an API call. Reference case (house template, ID style `AUTH_FT0n`):

```
Group:     Google Sign-in
ID:        AUTH_FT03
Desc:      Login blocked for inactive account.
Procedure: 1. Go to /login.
           2. Click "Đăng nhập bằng Google" and complete Google consent with a deactivated account.
Expected:  1. Backend rejects the sign-in with <status>/<MSGnn>; no session is created.
           2. Login page shows "<exact Vietnamese message>" and stays on /login.
           3. Opening a protected URL (/meetings) still asks to sign in.
Pre:       - Account exists and is deactivated (UC-06c).
           - Backend & frontend running on the test build.
           - User not signed in.
Note:      UC-02 · BR-xx · Automated: tests/... (Method) or "Manual only"
```

Numbering restarts per feature and never per endpoint. Skip a feature (no sheet) only if nothing
of it is implemented; say so in the Test Cases sheet description instead of inventing cases.

Feature codes (use exactly these):

| FE | Code | Sheet name | Start from (verify against the inventory and the code) |
|---|---|---|---|
| FE-01 | AUTH | Authentication | 03-api/01-xac-thuc, 16-truy-cap-khach-uc04 |
| FE-02 | USER | User Management | 02-quan-ly-nguoi-dung |
| FE-03 | RBAC | Access Control | 03-ma-tran-phan-quyen, 04-*, 21-nhat-ky-kiem-toan |
| FE-04 | MEET | Meeting Management | 05-quan-ly-cuoc-hop |
| FE-05 | AGENDA | Agenda | 08-chuong-trinh-nghi-su |
| FE-06 | PART | Participants | 09-thanh-phan-tham-du, 10-diem-danh |
| FE-07 | DOC | Documents | 11-tai-lieu-cuoc-hop, 14-kho-tai-lieu-ca-nhan |
| FE-08 | ROOM | Online Room | 09-phong-hop-truc-tuyen, 19-ghi-chu, 20-dang-ky-phat-bieu |
| FE-09 | REC | Recording | 12-ban-ghi-cuoc-hop |
| FE-10 | TRANS | Transcription & Summary | 13-nhan-dien-giong-noi, 18-may-ai |
| FE-11 | VOTE | Voting | 15-bieu-quyet |
| FE-12 | MIN | Minutes | 17-du-thao-bien-ban |
| FE-13 | NOTI | Notifications | 06-thong-bao-trong-ung-dung, 07-gui-email-thu-moi |
| FE-14 | DASH | — (no sheet: UC-69 is a placeholder) | — |
| FE-15 | RPT | Reporting | 22-bao-cao-thong-ke |
| FE-16 | CAL | Calendar | 23-chia-se-lich |

The endpoint → feature mapping is decided by the **UC** the endpoint serves (inventory), not by the
file it lives in, and is already written down in `sheets.json`. `/api/health` is in `excludedRoutes`:
it appears only in preconditions as "backend is running (`GET /api/health` = 200)". SignalR hubs
(`/hubs/*`) are not API groups; they are covered by the real-time UI groups.

## 3. How to write one case

Good (API):

```
ID:        MEET_API_IT03
Desc:      Verify POST /api/meetings rejects an end time before the start time.
Procedure: 1. Sign in as the Organizer (POST /api/auth/login) and keep the access token.
           2. Send POST /api/meetings
              Headers: Authorization: Bearer <access_token>; Content-Type: application/json
              Body: { "title": "Họp giao ban", "startAt": "2026-12-01T09:00:00+07:00",
                      "endAt": "2026-12-01T08:00:00+07:00", ... }
Expected:  1. HTTP 400 Bad Request.
           2. Body: { "code": "MSGnn", "errors": { "endAt": [...] } }
           3. No meeting is created: GET /api/meetings does not list "Họp giao ban".
Pre:       - Backend running on the test build.
           - Organizer account with MeetingCreator, supplied by QA lead.
Note:      UC-14 · BR-xx · Automated: tests/TrustCabinet.Web.Tests/Meetings/MeetingEndpointsTests.cs (Method)
```

Good (UI): see the `AUTH_FT03` reference in §2.

Rules:

- Business language. Never `Assert.*`, fixture names, `TestServer`, `dotnet test`, fakes or class
  names in Description / Procedure / Expected / Pre-conditions. The C# test that automates the case
  goes in **Note** only, as a cross-reference.
- Every value comes from the code and `docs/03-api/` — real routes, real field names, real status
  codes, real `MSGnn` from [00-danh-muc-ma-thong-bao.md](../../03-api/00-danh-muc-ma-thong-bao.md).
  Read the endpoint and its test before writing; do not guess a body shape.
- Respect the 401 / 403 / 404 rule in the root AGENTS.md: a resource the caller may not read is
  **404**, a fixed endpoint without capability is **403**.
- Coverage per endpoint (5.2.2), at minimum: happy path; each validation `MSGnn` it can return;
  401 without token; 403 or 404 for a caller without rights; each state/conflict rule (wrong status,
  409, last admin, …); each side effect worth checking (notification, status history, audit row,
  email in the sandbox). One case = one behaviour.
- Coverage per function (5.2): happy path, each AF and EX of the UC spec that the screen implements,
  what happens when the backend refuses (inactive, no right, wrong state, expired session/token),
  the user cancelling midway, the capability-hidden route (404), and reload/persistence. Cases that
  need two users (organizer + approver, sender + recipient) say so and check the second user's view.
- Pre-conditions name the role and data, never a real password or secret ("account supplied by QA
  lead"). Seeded test accounts may be named by role.
- Statuses are `Passed`, `Failed`, `Pending`, `N/A`. `Round 1` starts as `Pending` (leave `round1` out
  of the JSON). Nothing is `Passed` without a real execution (README "Kết quả và evidence").
- Case text is English; Vietnamese UI labels, messages and sample data stay Vietnamese in quotes.
- A behaviour the code does that contradicts the UC spec: write the case for the **code**, and add
  `Deviation: …` to Note so BA can review it. Do not edit the UC docs from this task.

## 4. Source of truth — do not hand-edit the workbooks

The `.xlsx` files are regenerated; edits made in Excel are lost. Case content lives in
`cases/` next to this file, one JSON file per feature per workbook:

```
cases/sheets.json        which sheets, in which order, and the groups each one must have (the plan)
cases/routes.json        every /api route the backend maps, dumped from the running route table
cases/FE-04/ui.json      -> Report5.2_Integration_Test.xlsx, sheet "Meeting Management"
cases/FE-04/api.json     -> Report5.2.2_Integration_Test_API_Test.xlsx, same sheet name
cases/preview/**.md      generated Markdown preview of each JSON, for reviewing in a PR
```

```json
{
  "feature": "FE-04", "code": "MEET", "sheet": "Meeting Management",
  "precondition": "Shared precondition shown on the Test Cases sheet.",
  "groups": [
    { "title": "POST /api/meetings", "uc": ["UC-14"],
      "cases": [
        { "id": "MEET_API_IT01", "description": "...", "procedure": ["...", "..."],
          "expected": ["...", "..."], "preconditions": ["...", "..."],
          "note": "UC-14 · BR-xx · Automated: tests/... (Method)" } ] } ]
}
```

Optional per case: `round1`/`date1`/`tester1` (and 2, 3) once a round is really executed.

[`scripts/test_reports/integration_cases.py`](../../../scripts/test_reports/integration_cases.py)
does everything mechanical; nobody opens the workbooks to edit them:

| Command | What it does |
|---|---|
| `routes` | boots the app in memory through the test factory and rewrites `routes.json` (run after adding an endpoint) |
| `scaffold` | creates the missing `FE-xx/ui.json` and `FE-xx/api.json` with the groups from `sheets.json` and no cases |
| `check` | every route in exactly one sheet; JSON groups = `sheets.json`; IDs `<CODE>_FT01…` / `<CODE>_API_IT01…` in sequence; no empty field; no test-code wording; every `MSGnn` in the catalogue; every test method in a Note exists; lists groups still without cases |
| `md` | writes `cases/preview/FE-xx/{ui,api}.md` |
| `build` | `check`, then `md`, then both workbooks from `scripts/test_reports/templates/integration-template.xlsx` (Cover, Test Cases, Test Statistics with formulas, one sheet per FE). Close the workbooks in Excel first |

Never copy the ACCE sample workbooks (`docs/08-reports/sample/`, gitignored) into the repo; the
template is derived from our own Report 5.2.

## 5. Splitting the work between agents

Run the phases in order; within phase 1 the agents run in parallel because each one owns only its
own two JSON files.

| Phase | Agents | Owns | Done when |
|---|---|---|---|
| 0. Pipeline | 1 | `sheets.json`, `routes.json`, `integration_cases.py`, the template, skeletons, this file, README | done: FE-01 written, every route mapped, both workbooks build |
| 1. Authoring | 1 per FE (or per 2–3 small FEs) | `cases/FE-xx/ui.json`, `cases/FE-xx/api.json` | every group of the FE has cases per §3; `check` shows no error for the FE |
| 2. Review | 1 | nothing but review notes | every `MSGnn` has the right status for its situation, the body shapes match the code, preconditions are reachable — what `check` cannot see |
| 3. Render | 1 (same as phase 0) | the two `.xlsx`, `cases/preview/` | `build` passes; README table updated |

Push to Drive (`scripts/push_reports.py tests`) only when the user asks.

## 6. Report 5.1 Unit Test (same pipeline, row layout of the house `.xls`)

5.1 keeps the house template exactly (Cover, Test Cases, Test Statistics, then one sheet per
production function; header block rows 1–9; columns `Test Case ID | Pre-conditions |
Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | … | Note`).
Only the content changes. The layout and style come only from
`Report5.1_Unit Test.xls` (read through Excel COM — openpyxl cannot open `.xls`), saved cleaned as
`scripts/test_reports/templates/unit-template.xlsx`: sheets Cover, Test Cases, Test Statistics, plus
one prototype module sheet with one `methodName` group row and one UTC row, placeholders removed.
The ACCE sample (`docs/08-reports/sample/Report5.1_Unit_Test.xlsx`, gitignored) decides only how
sheets are split — one sheet per production function under test, short names (`create_user`,
`login`), grouped by module in the list sheet. Never copy its text, values or styles. Its O-matrix
layout is not ours; ours is the row layout of the `.xls`. The teammate first attempt
(`Report5.1_Unit_Test_Old_Concept.xlsx`: one sheet per xUnit method, `TC-<hash>` IDs, test-code
text) is retired: reuse only which tests exist and how modules map; its generator (`unit_book` in
`scripts/test_reports/template_workbooks.py`, called from `scripts/generate_test_reports.py`) is
retired with a pointer to the new script.

| | 5.1 Unit Test |
|---|---|
| Who runs it | Developer / tester calling the production function directly (in-process, xUnit or a scratch harness) |
| Sheet | one per **production function under test** (`Class.Method`, shortened to ≤31 chars, unique, e.g. `Meeting.Approve`) |
| Group row inside sheet | the full `Class.Method` (`methodName`); a sheet may carry a second group only for a helper tightly bound to the same rule |
| Who decides sheets and groups | [`cases/functions.json`](cases/functions.json) — an authoring agent never adds, renames or reorders a sheet or a method |
| Case ID | `UTC001`, `UTC002`… sequential per sheet (never per method) |
| Description | one line: behaviour + condition on the function, e.g. "Rejects an end time before the start time." |
| Pre-conditions | the state the function needs (seeded rows, role, config) — never a password or secret |
| Inputs | the exact arguments passed to the function |
| Expected | **always three lines**: `Return: …`, `Exception: …`, `Log Message: …`, with `N/A` where nothing applies |
| Type | `Normal` / `Abnormal` / `Boundary` (data validation on the sheet) |
| Module | feature code from `cases/sheets.json` (`AUTH`, `USER`, `MEET`, `VOTE`…) |
| Test Cases sheet | `No | Module Name | Sheet Name (hyperlinked) | Description | Pre-Condition` |

A unit is a production function, not a test method or an endpoint. Skip endpoint glue and EF
configuration. Rank by business weight: workflow transitions, authorization rules, the vote tally,
scheduling and conflicts, attendance, the minutes template, token and password rules, notification
fan-out, transcript and speaker logic. A function the tests never call directly (HTTP-only coverage)
is a gap: list it, do not write the missing C# test unless asked.

Rules (same spirit as §3):

- Business language. Never `Assert.*`, fixture names, `TestServer`, `dotnet test`, fakes or class
  names in Pre-conditions / Inputs / Expected / Type. The C# test that automates the case goes in
  **Note** only, as `BR-xx · MSGnn · Automated: tests/... (Method)` or `"Manual only"`.
- `Expected Results` is always the three lines `Return: …` / `Exception: …` / `Log Message: …`.
- Every value comes from the code — real signatures, real outcomes, real `MSGnn` from
  [00-danh-muc-ma-thong-bao.md](../../03-api/00-danh-muc-ma-thong-bao.md). Read the function and
  its test before writing; do not guess a shape. Vietnamese messages are quoted exactly.
- Coverage per function, at minimum: happy path; each validation `MSGnn` it can return; each
  state/conflict rule (wrong status, last admin, …); each side effect worth checking (notification,
  status history, audit row, email in the sandbox); one Boundary case per numeric/string limit.
  One case = one behaviour.
- Statuses are `Passed`, `Failed`, `Pending`, `N/A`. Every round starts as `Pending` (leave the
  round out of the JSON). Nothing is `Passed` without a real execution (README "Kết quả và evidence").
- Case text is English; Vietnamese messages and sample data stay Vietnamese in quotes.
- A behaviour the code does that contradicts the UC spec: write the case for the **code**, and add
  `Deviation: …` to Note so BA can review it.

Source of truth — do not hand-edit the workbook:

```
cases/functions.json             which sheets exist, in which order, and which methods each one covers (the plan)
cases/FE-04/unit/<sheet>.json    UTC rows of one sheet -> Report5.1_Unit_Test.xlsx
cases/preview/FE-04/unit-*.md    generated Markdown preview of each sheet, for reviewing in a PR
```

```json
{
  "feature": "FE-04", "module": "MEET", "sheet": "Meeting.Approve",
  "description": "…", "precondition": "…",
  "methods": [
    { "method": "MeetingService.ApproveAsync", "file": "backend/.../MeetingService.cs", "line": 411,
      "cases": [
        { "id": "UTC001", "preconditions": ["…"], "inputs": "…",
          "return": "…", "exception": "N/A", "log": "…",
          "type": "Normal", "note": "BR-xx · MSGnn · Automated: tests/... (Method)" } ] } ]
}
```

[`scripts/test_reports/unit_cases.py`](../../../../scripts/test_reports/unit_cases.py)
does everything mechanical; nobody opens the workbook to edit it:

| Command | What it does |
|---|---|
| `scaffold` | creates the missing `cases/FE-xx/unit/<sheet>.json` with the methods from `functions.json` and no UTCs |
| `check` | every function exists at its `file:line`; every cited test method exists; UTC IDs `UTC001…` sequential per sheet; `type` is Normal/Abnormal/Boundary; every `MSGnn` in the catalogue; no `Assert.*`/fixture wording outside Note; sheet names valid (≤31 chars, no `/ \ ? * [ ]`, unique); lists methods still without UTCs |
| `md` | writes `cases/preview/FE-xx/unit-<sheet>.md` |
| `build` | `check`, then `md`, then `Report5.1_Unit_Test.xlsx` from `scripts/test_reports/templates/unit-template.xlsx` (Cover, Test Cases, Test Statistics with formulas, one sheet per `functions.json` entry). Close the workbook in Excel first |

Template formula fixes (kept in both the template and the generated sheets): Test Cases D3/D4
`#REF!` now point at `Cover!B4`/`Cover!B5`; the Type counts use `$E12` consistently (the
`E$10` outlier is fixed); the Statistics percentages are wrapped in `IFERROR(…,0)`; each module
sheet counts `=COUNTIF(A:A,"UTC*")` and the Statistics rows reference each sheet directly with
Sub total, Test coverage, Test successful coverage and the Normal / Abnormal / Boundary percentages.

Prompt for a phase-1 agent (fill in the FE):

> Read `docs/08-reports/test-reports/AGENTS.md` and follow it. Your feature is **FE-04 Meeting
> Management** (code `MEET`). Write `cases/FE-04/api.json` and `cases/FE-04/ui.json` only — touch no
> other file, and keep their groups exactly as `cases/sheets.json` lists them. Fill each group from
> the inventory, `docs/03-api/`, the endpoint code under `backend/TrustCabinet.Web/`, the screens
> under `frontend/src/features/` and the tests under `tests/TrustCabinet.Web.Tests/`. Use
> `cases/FE-01/` and `cases/FE-11/` as the model. Run
> `python scripts/test_reports/integration_cases.py check` until it shows no error for FE-04; do not
> run `build`. Report: case counts per group, any group you think is wrong in `sheets.json`, and any
> `Deviation:` notes.

## 7. House lessons (learned from the FE-01 / FE-11 reference)

1. **N/A/B comes from the code, not from feeling.** Normal = the documented happy path; Abnormal =
   a refused input or state, each mapping to one `MSGnn`; Boundary = a numeric or string limit from
   the code or the tests (1000-character reason, 64 KiB body, 200-character passphrase, 0 ballots,
   15-minute expiry). No `MSGnn` and no limit usually means Normal.
2. **Preconditions name roles and states, never secrets** ("active Secretary account supplied by QA
   lead"). Passwords appear only as placeholders.
3. **Unit Notes never reference HTTP tests.** `Automated:` is only for a test that calls the
   function directly — many such tests live in `tests/TrustCabinet.Web.Tests` but use no HTTP
   (e.g. `PasswordHasherTests` calls `PasswordHasher.Verify` in-process; the project holds both
   kinds). An endpoint test never automates a UTC, not even as a cross-reference. No direct test →
   Note is `Manual only` (keeping UC/MSG context); the missing test itself is tracked as a gap in
   `functions.json` and the report until someone writes it.
4. **Quote Vietnamese exactly** from `docs/03-api/`, the endpoint, or the `.tsx`. One wrong word
   fails review.
5. **Expected checks both sides.** API: status, `code`, body fields, and what must *not* happen (no
   cookie, no draft, no second session). UI: the screen text, where the app stays, and the second
   user's view. One case = one behaviour.
6. **Unit Expected is always the three lines** `Return:` / `Exception:` / `Log Message:`. Pure
   functions get `Exception: N/A`; logger calls go in `Log Message:` verbatim; side effects (audit
   row, notification) go in `Return:`. A pure function never claims it "saves nothing" — it saves
   nothing by definition.
7. **IDs are mechanical** (`<CODE>_FTnn`, `<CODE>_API_ITnn`, `UTCnnn` sequential). Let `check`
   number them; never hand-number.
8. **Rounds stay Pending.** Leave `round1` out of the JSON; nothing is `Passed` without a real run.
