# AGENTS.md — Report 5.2 / 5.2.2 Integration Test

Instructions for any agent that rewrites `Report5.2_Integration_Test.xlsx` (UI integration) or
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
| Sheet | one per FE: `FE-04 Meeting Management` (≤31 chars, no `/ \ ? * [ ]`) | same |
| Group row inside sheet | one per **function / sub-flow** the user recognises: `Google Sign-in`, `Token Refresh`, `Create Meeting` (each maps to one UC or a slice of one; UC id goes in Note) | one per **endpoint**: `POST /api/meetings` |
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
| FE-01 | AUTH | FE-01 Authentication | 03-api/01-xac-thuc, 16-truy-cap-khach-uc04 |
| FE-02 | USER | FE-02 User Management | 02-quan-ly-nguoi-dung |
| FE-03 | RBAC | FE-03 Access Control | 03-ma-tran-phan-quyen, 04-*, 21-nhat-ky-kiem-toan |
| FE-04 | MEET | FE-04 Meeting Management | 05-quan-ly-cuoc-hop |
| FE-05 | AGENDA | FE-05 Agenda | 08-chuong-trinh-nghi-su |
| FE-06 | PART | FE-06 Participants | 09-thanh-phan-tham-du, 10-diem-danh |
| FE-07 | DOC | FE-07 Documents | 11-tai-lieu-cuoc-hop, 14-kho-tai-lieu-ca-nhan |
| FE-08 | ROOM | FE-08 Online Room | 09-phong-hop-truc-tuyen, 19-ghi-chu, 20-dang-ky-phat-bieu |
| FE-09 | REC | FE-09 Recording | 12-ban-ghi-cuoc-hop |
| FE-10 | TRANS | FE-10 Transcription | 13-nhan-dien-giong-noi, 18-may-ai |
| FE-11 | VOTE | FE-11 Voting | 15-bieu-quyet |
| FE-12 | MIN | FE-12 Minutes | 17-du-thao-bien-ban |
| FE-13 | NOTI | FE-13 Notifications | 06-thong-bao-trong-ung-dung, 07-gui-email-thu-moi |
| FE-14 | DASH | FE-14 Dashboard | — |
| FE-15 | RPT | FE-15 Reporting | 22-bao-cao-thong-ke |
| FE-16 | CAL | FE-16 Calendar | 23-chia-se-lich |

The endpoint → feature mapping is decided by the **UC** the endpoint serves (inventory), not by the
file it lives in. `/api/health` and the SPA fallback are not a feature: put them nowhere, or in
FE-01's preconditions as "backend is running (`GET /api/health` = 200)".

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
- `Round 1` = `NotRun`. Nothing is `Passed` without a real execution (README "Kết quả và evidence").
- Case text is English; Vietnamese UI labels, messages and sample data stay Vietnamese in quotes.
- A behaviour the code does that contradicts the UC spec: write the case for the **code**, and add
  `Deviation: …` to Note so BA can review it. Do not edit the UC docs from this task.

## 4. Source of truth — do not hand-edit the workbooks

The `.xlsx` files are regenerated; edits made in Excel are lost. Case content lives in
`cases/` next to this file, one JSON file per feature per workbook:

```
cases/ui/FE-04.json      -> Report5.2_Integration_Test.xlsx, sheet "FE-04 Meeting Management"
cases/api/FE-04.json     -> Report5.2.2_Integration_Test_API_Test.xlsx, same sheet name
```

```json
{
  "feature": "FE-04", "code": "MEET", "sheet": "FE-04 Meeting Management",
  "precondition": "Shared precondition shown on the Test Cases sheet.",
  "groups": [
    { "title": "POST /api/meetings", "uc": ["UC-14"],
      "cases": [
        { "id": "MEET_API_IT01", "description": "...", "procedure": ["...", "..."],
          "expected": ["...", "..."], "preconditions": ["...", "..."],
          "note": "UC-14 · BR-xx · Automated: tests/... (Method)" } ] } ]
}
```

`scripts/generate_test_reports.py` renders these into the template for 5.2 and 5.2.2 (it no longer
builds 5.2.2 from the Roslyn inventory; 5.1 Unit Test still does). Then run
`scripts/test_reports/verify_reports.py` and open the result in Excel to check wrapping and row heights.

## 5. Splitting the work between agents

Run the phases in order; within phase 1 the agents run in parallel because each one owns only its
own two JSON files.

| Phase | Agents | Owns | Done when |
|---|---|---|---|
| 0. Pipeline | 1 | `cases/` schema, renderer change in `scripts/`, empty skeleton per FE, this file, README | FE-01 rendered end to end in both workbooks; user approves the look |
| 1. Authoring | 1 per FE (or per 2–3 small FEs) | `cases/ui/FE-xx.json`, `cases/api/FE-xx.json` | every implemented endpoint/UC of the FE covered per §3 |
| 2. Review | 1 | nothing but review notes | IDs unique and sequential, every route exists in code, every `MSGnn` exists in the catalogue with the right status, no `Assert`/fixture text, sheet totals add up |
| 3. Render | 1 (same as phase 0) | the two `.xlsx`, `inventory.json`, `manifest.json` | regenerated, verified, README table updated |

Push to Drive (`scripts/push_reports.py tests`) only when the user asks.

Prompt for a phase-1 agent (fill in the FE):

> Read `docs/08-reports/test-reports/AGENTS.md` and follow it. Your feature is **FE-04 Meeting
> Management** (code `MEET`). Write `cases/api/FE-04.json` and `cases/ui/FE-04.json` only — touch no
> other file. Find every endpoint and screen of FE-04 from the inventory, `docs/03-api/`, the
> endpoint code under `backend/TrustCabinet.Web/`, the screens under `frontend/src/features/` and
> the tests under `tests/TrustCabinet.Web.Tests/`. Do not run the renderer. Report: endpoints and
> UCs covered, case counts, any endpoint you could not map to a UC, and any `Deviation:` notes.
