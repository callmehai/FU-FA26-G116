---
name: qa-ticket
description: Independently QA a TrustCabinet Jira ticket (or a feature FE-xx, or a set of UCs) on the running app, the way a tester would — turn the ticket, its UC specification and business rules into acceptance criteria a user can check plus a role × action permission table, write a plan for the tests/qa runner, run it on desktop and phone, judge every screenshot, and report PASS / FAIL / INCONCLUSIVE with evidence as a [qa-bot] Jira comment and a local HTML report. Use when asked to "QA", "kiểm thử ticket", "test lại", "auto QA", before a ticket is called Done, and after a QA defect is fixed.
---

# QA a ticket

The tester is **independent**: it checks what was asked against what runs, not what the implementer meant. It
writes no application code. A defect it finds is reported, never fixed in the same pass.

The runner lives in [tests/qa/](../../../tests/qa/README.md): `run.mjs` runs a plan, one fresh browser context per
criterion and device, with an outside-request guard, and writes `results.json`, `report.html` and `summary.md`
into `tests/qa/out/<ticket>/<time>-<commit>/`.

## 0. Who runs it

- Run the QA in a **fresh subagent** (or a fresh session) given only the ticket key and this skill. An agent that
  just wrote the code carries its own reading of the ticket and will test that reading. Never QA your own change in
  the same context you built it.
- The QA agent may read code to find selectors and endpoints, but the criteria come from the requirement, not from
  the code.

## 1. Is the ticket ready, and which build is tested

- Every PR of the ticket is on `main`: `git merge-base --is-ancestor <merge-commit> origin/main`. A stacked PR
  merged into its base is **not** on `main` (#238).
- Local `main` is pulled and the backend and frontend were restarted after the pull, so the running app is that
  commit. The report names the commit; a run with uncommitted changes says so.
- Do not QA the same ticket twice on the same commit: look for `tests/qa/out/<TICKET>/*-<commit>/` first. A new run
  needs a new merge (or the user asking).

## 2. Acceptance criteria and the permission table

Read, in this order: the Jira ticket (description **and comments**: deviations live there), the UC specification
(`docs/01-requirements/05-use-case-specifications.md`), the business rules it cites
(`docs/01-requirements/06-business-rules.md`), the API doc in `docs/03-api/`, the message codes in
`docs/03-api/00-danh-muc-ma-thong-bao.md`, and the PR body's "how it was checked".

Write numbered criteria, `AC1`, `AC2`…, each one a user can check on the screen or a client can check on the API:
actor, steps, expected (the exact message text or code when there is one). Cover:

- **the flow** end to end, as the UC's Normal Flow runs it, not each step alone;
- **what goes wrong:** each Alternative / Exception flow that shows something to a user, invalid input, a record
  that is gone, an action in the wrong state (approving minutes already issued), a duplicate;
- **every role that can reach it, the denied ones included.** One row per role and action before any plan:

  | Role | Action | Expected |
  |---|---|---|
  | secretary | submit minutes for approval | 200 MSG424, status WaitingApproval |
  | member | `POST /api/minutes/{id}/submit` | 403 or 404, per the table below |
  | member | open the minutes tab | no Submit button |

  The API's answer follows AGENTS.md: 401 signed out; 403 for a fixed endpoint the role lacks; **404** for one
  resource the caller may not read; a restricted UI route renders the same "Không tìm thấy trang" as an unknown URL.
- **the phone** for every UI criterion (`devices: ['desktop', 'phone']`): no sideways page scroll, controls reachable,
  nothing explained only on hover (check-responsive).
- **steady layout** for every control whose value the user changes (a filter, a tab, a toggle): measure its box for
  each value and fail if its width changes or the controls after it move. A screenshot per state does not show this;
  the numbers do (FE-04 AC23b: the meeting list's filters grew and shrank with the picked label, found by BA on 15/11).
  When one screen has this defect, measure the same kind of control on every other screen too, not only the one
  reported: the audit log's filters had the same fault and were found by a teammate a day later (FE-03 AC20b).

Drop criteria the PO dropped (see AGENTS.md "Status") and do not invent rules: a threshold or behaviour that is not
in the ticket, the UC or a BR is a question for a human, not a criterion.

## 3. The plan

One file per ticket, `tests/qa/plans/<ticket-lowercase>.mjs`, kept in the repo: it is the regression test for the
ticket and the next QA run re-uses it. Shape (see `plans/smoke.mjs`):

```js
export default {
  ticket: 'SCRUM-33',
  title: 'Gửi duyệt, duyệt và trả lại biên bản',
  devices: ['desktop'],                 // default per criterion; a criterion may override
  allowHosts: [],                       // extra hosts the page may call (LiveKit in a room test)
  criteria: [
    {
      id: 'AC1', role: 'secretary', devices: ['desktop', 'phone'],
      title: 'Thư ký gửi duyệt biên bản đã soạn',
      steps: ['Mở tab Biên bản của cuộc họp đã kết thúc', 'Bấm Gửi duyệt'],
      expected: 'Toast MSG424, biên bản chuyển Chờ duyệt, Chủ toạ nhận thông báo',
      async run(qa) { /* … */ },
    },
  ],
}
```

`qa` gives: `page`, `expect`, `goto(path)`, `signIn(role)` (`admin`, `chair`, `secretary`, `member`, an email, or
`anon`), `api(role)` → `{ get, post, put, patch, delete }` returning `{ status, body }`, `expectStatus(res, code)`,
`capabilities(role)` (`{ roleName, capabilities }` from `/api/auth/me`), `shot(slug)` (evidence named
`NN-<role>-<AC>-<device>-<slug>.png`, taken with transitions finished), `name(text)` (`[QA] text`), `note(text)`,
`inconclusive(reason)`.

- **Prepare state through the API, test the thing under test through the screen.** Creating a meeting by clicking
  through five dialogs to test the minutes tab tests the dialogs, slowly.
- Name every record the plan creates with `qa.name(...)`, so it can be found and deleted.
- Use role and label locators (`getByRole`, `getByLabel`), never CSS chains copied from the DOM.
- Take a shot at every state a reader must see: before the action, the result, the denied view.
- Wait for conditions (`expect(...).toBeVisible()`, `toHaveURL`), never fixed sleeps.
- Scope generic roles to the part of the page under test: the toast stack is a `status` region on every page, so
  `page.getByRole('status')` alone matches it; use `page.locator('form').getByRole('status')` or the like.
- **Check the account before judging a denial.** A local database whose roles were edited by hand can give a seed
  account more than its name says (the secretary seed carrying `AdminUsers`). Call `qa.capabilities(role)` first; if
  the account holds the capability the criterion denies, the criterion is INCONCLUSIVE and says which account to
  restore, never a PASS or a defect.
- **Paid or outside calls** (the AI API, email delivery): never make a real paid call unless the user asks, and then
  once. Check what can be checked before the call (validation answers 400 before anything leaves), and exercise the
  failure path with a throwaway key against an unreachable `.invalid` host. Re-read the stored settings afterwards to
  prove the test saved nothing.

## 4. Run

```bash
# the app must be running on the commit under test (verify-ui-in-browser §1)
cd tests/qa && npm ci        # first time only
node run.mjs plans/smoke.mjs                   # proves runner + app + seed accounts
node run.mjs plans/scrum-33.mjs                # the ticket
node run.mjs plans/scrum-33.mjs --only AC3     # one criterion while investigating
```

If another dev server holds 5173, start Vite on another port and pass `--base http://localhost:5174`; the runner
refuses a base whose `/api/health` is not TrustCabinet's. Against the shared test server
(`--base https://trustcabinet.workspace-hub.space`) run **read-only plans only**: it is the team's demo data.

## 5. Judge

The runner's PASS is not the verdict. Open **every** screenshot and check it shows what the criterion claims, and
nothing wrong besides: clipped or overlapping text, English or raw codes shown to a user, a wrong count, an empty
state where data was expected. A wrong screenshot turns the criterion into FAIL; a side defect becomes a `note`.

INCONCLUSIVE is for what cannot be judged here — a real microphone or a second person in a LiveKit room, an email
actually delivered, the AI machine off — and names the exact step a human must do. It is never a way to skip a
criterion that is merely hard to automate.

## 6. Report

- Edit `summary.md` after judging (verdicts you changed, notes), then post it as **one Jira comment** on the ticket,
  starting `[qa-bot]`, with the screenshots a reader needs attached to the ticket (`uploadAttachmentToJiraIssue`). A
  comment never cites a local path as something to read: nobody else can open it.
- Each defect: steps, expected, actual, evidence, and the ticket's assignee tagged. One defect per bullet.
- Do **not** move the ticket's status, reassign it or create tickets: say what you recommend (Done, or back to the
  assignee) and let the user decide.
- Post a short note in Discord 🔍・review-pr tagging the assignee when there is a defect (no paths, no codes).
- Give the user `tests/qa/out/<ticket>/<run>/report.html` to open: the table with every screenshot, which zooms on
  click.

## 7. After a fix

Re-run the same plan on the new commit: the failed criteria first (`--only`), then the whole plan, since a fix can
break a neighbour. A defect is closed only by a PASS on a commit that contains the fix.

## 8. Never

- Never QA on someone's real account, never write to the test server, never put a password or token in a plan, a
  screenshot, a note or a comment (the dev seed password is the documented one; `QA_PASSWORD` overrides it).
- Never weaken a criterion to make it pass, and never mark PASS from the runner's output alone.
- Never leave `[QA]` records behind without saying so in the report.
