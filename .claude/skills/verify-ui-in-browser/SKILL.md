---
name: verify-ui-in-browser
description: Run TrustCabinet locally and check a frontend change in a real browser — through Playwright when the machine has it set up, otherwise the chrome-devtools MCP — start the servers, sign in as the seeded roles, drive forms and lists, take screenshots and critique them, measure layout, simulate errors and conflicts, confirm what reached the database, then clean up. Use before opening or updating any PR that changes what the user sees, and whenever asked to "chạy local lên xem" or test a screen.
---

# Verifying a UI change in the browser

Build and lint passing says the code compiles, not that the screen is right. Look at it, use it
as each role would, break it on purpose, and fix what you see — then write down what you tested.
Design rules are in **frontend-ui-design**; this skill is how to check them.

## 1. Start the app

```bash
docker compose ps            # postgres + redis must be running; `docker compose up -d` if not
dotnet run --project backend/TrustCabinet.Web --launch-profile http   # :5225, run in background
cd frontend && npx vite --port 5173 --strictPort                      # :5173, run in background
curl -s http://localhost:5225/api/health                              # wait until it answers
```

Run both servers as background processes and poll `/api/health` instead of sleeping. The
connection string comes from `dotnet user-secrets`; if the backend refuses to start, that is why.
Always open **http://localhost:5173** — Vite proxies `/api`.

Development seed accounts, password `TrustCabinet@2026`: `quantri@` (admin), `chutoa@` (chair,
approver), `thuky@` (secretary, meeting creator), `thanhvien@` (member) — all
`@trustcabinet.local`.

## 2. Drive the browser

Pick the tool first:

```bash
test -f ~/.playwright-e2e/node_modules/@playwright/test/package.json && echo playwright || echo cdp
```

- **Playwright** (preferred when it is set up — see *Browser setup notes*): the checks are a spec
  file, so a whole scenario runs in one command, can be re-run after every fix, and the reviewer
  can run the same file. Follow §2a.
- **chrome-devtools MCP** otherwise: drive the page one tool call at a time. Follow §2b.

The rest of this skill (what to measure, which states to test, test data, the report) is the same
for both.

### 2a. Playwright

The workspace lives outside the repo, in `~/.playwright-e2e` (`playwright.config.ts`,
`fixtures.ts`, `tests/`). It never starts the app: start it as in §1 first. Write one spec per
ticket, `tests/<ticket>.spec.ts` (e.g. `scrum-47.spec.ts`), and keep it there for the re-run after
review fixes. Never copy it into the repo.

```bash
cd ~/.playwright-e2e
npx playwright test tests/smoke.spec.ts                     # first: proves browser + app + sign-in
PW_OUT=<scratchpad>/pw npx playwright test tests/scrum-47.spec.ts   # artifacts into the scratchpad
npx playwright test tests/scrum-47.spec.ts -g "filter" --headed    # one test, watch it run
```

```ts
import { execSync } from 'node:child_process'
import { expect, login, signIn, test } from '../fixtures'

test('…', async ({ page, browser }, info) => {
  await signIn(page, 'thuky')                 // API login: refresh cookie + "signed in" marker
  await page.goto('/calendar?span=month')
  await page.screenshot({ path: info.outputPath('month.png') })   // then Read the PNG and look
})
```

- **Signing in**: `signIn(page, seed)` logs in through the API, so each test starts signed in
  without the form. `login(page.request, seed)` returns a bearer token for creating `[Test FE]`
  data through the API. A second user at the same time: `const other = await (await
  browser.newContext()).newPage(); await signIn(other, 'thanhvien')`.
- **Locators**: prefer roles and labels, which also checks the accessible names:
  `getByRole('button', { name: 'Thử lại' })`, `getByRole('alert')`, `getByLabel('Email')`.
- **Inputs**: `fill` works on text **and** on `date` / `datetime-local` (`'2026-10-05T09:00'`) —
  React hears it.
- **`Select`**: `getByRole('button', { name: /^Kiểu lịch/ }).click()`, then
  `getByRole('option', { name: 'Tháng' }).click()`. The trigger's name is `"<label>: <value>"`.
- **`SegmentedControl`**: `getByRole('radio', { name: '…' }).check({ force: true })` — the input is
  visually hidden.
- **Look**: save screenshots with `info.outputPath()` and open them with the Read tool, then
  critique as in §2b. `main.content` is the scroll container: screenshot it with
  `page.locator('main.content').screenshot()` or set its `scrollTop` first.
- **Measure**: `locator.boundingBox()`, or `evaluateAll` for many elements at once:
  ```ts
  const tops = await page.locator('.calendar-toolbar > *').evaluateAll((els) =>
    els.map((e) => Math.round(e.getBoundingClientRect().top)))
  expect(new Set(tops).size).toBe(1)          // the toolbar is one line
  ```
- **Window size**: `page.setViewportSize({ width: 1200, height: 700 })`.
- **Server error**: `page.route` instead of patching `fetch`; flip it off and use "Thử lại":
  ```ts
  let fail = true
  await page.route('**/api/meetings?*', (r) =>
    fail ? r.fulfill({ status: 500, json: { code: 'MSG38', message: '…' } }) : r.continue())
  ```
- **Requests**: `page.waitForRequest(/\/api\/meetings\?/)`, or collect with
  `page.on('request', …)` and assert the count and the query string.
- **Database**: call psql from the spec, so the check sits next to the action it verifies:
  ```ts
  const DB = 'docker compose exec -T postgres psql -U trustcabinet -d trustcabinet -tAc'
  const psql = (sql: string) => execSync(`${DB} "${sql}"`, { cwd: '<repo root>' }).toString().trim()
  ```
- A failing test keeps a screenshot and a trace in the output folder:
  `npx playwright show-trace <out>/…/trace.zip` replays it step by step.

### 2b. chrome-devtools MCP

- `new_page` / `navigate_page` to open screens. For a second user at the same time, use
  `new_page` with `isolatedContext: "<name>"` — it gets its own cookies and storage.
- `take_snapshot` gives the accessibility tree with `uid`s for `click` / `fill`. It is large on a
  table page — for assertions prefer `evaluate_script` returning exactly what you need (row
  titles, URL, a summary text, element rectangles).
- `take_screenshot` to **look**. Then critique it like a designer: alignment, spacing, rows that
  wrap, text that is cut off, states that look broken. Fix, reload, look again.
- The app's scroll container is `main.content`, so `fullPage` screenshots do not scroll. Scroll it
  in a script first: `document.querySelector('main.content').scrollTop = N`.
- Check at a **1200px-wide** window as well as a large one — the toolbar and two-column form
  must still fit — and on a **phone (390px, touch)**: see **check-responsive**, which applies to
  Playwright runs too.

#### Inputs that need care

- **Text inputs**: the `fill` tool works.
- **`date` / `datetime-local`**: `fill` changes the DOM but React never hears it. Use the native
  setter and dispatch `input`:
  ```js
  const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
  set.call(el, '2026-10-05T09:00'); el.dispatchEvent(new Event('input', { bubbles: true }))
  ```
  If an error message stays after filling, check the React props (`__reactProps…`) — the value
  may not have reached state.
- **`Select`** (our dropdown): click `.select-trigger`, then click the `.select-option`, or press
  `ArrowDown` / `Enter`. The menu is a popover in the top layer, so it may not show in snapshots.
- **`SegmentedControl`**: click the hidden radio: `option.querySelector('input').click()`.

## 3. Measure, don't eyeball

Use `getBoundingClientRect()` (in `evaluate_script`, or `evaluateAll` / `boundingBox()` in
Playwright) to prove layout claims:

- a toolbar is one line → every child has the same `top`;
- a form does not jump when a mode changes → `.form-card` height is equal in every mode;
- dropdown text is aligned → trigger value `x` equals the first option label `x`;
- an alert lines up with the form → equal widths;
- a menu shows its whole list → `scrollHeight <= clientHeight`.

### Before and after, side by side

Every change to what the user sees comes with evidence of what it changed: the same screen, in the
same state and at the same viewport, once on the old code and once on the new. Numbers prove a
layout claim; the pair shows the reader what actually moved.

- Capture **before** first, before editing. If the change is already made, run the old code in a
  second worktree on another Vite port, against the same backend:
  ```bash
  git worktree add ../tc-before origin/main          # or the commit before the change
  ln -s "$PWD/frontend/node_modules" ../tc-before/frontend/node_modules
  (cd ../tc-before/frontend && npx vite --port 5174 --strictPort)   # in the background
  ```
  Then run the **same spec** twice, changing only the base URL: have it read
  `test.use({ baseURL: process.env.BASE ?? 'http://localhost:5173' })`.
- A screenshot the user sent with the bug report is a valid "before".
- One image per scenario, the two shots next to each other under "Trước" / "Sau":
  `python3 ~/.playwright-e2e/pair.py before.png after.png pair.png` (see *Browser setup notes*).
  Keep the ticket's evidence together in `~/.playwright-e2e/reports/<ticket>/` (`pairs/`, `shots/`),
  outside the repo; §6 turns it into the HTML report.
- **Box every change, so none is missed.** A pair side by side hides small fixes. Scan both
  builds with [sloplint](../anti-ai-slop/SKILL.md) on the same paths and diff them:
  ```bash
  S="npx -y github:callmehai/sloplint#v0.2.0"; R=~/.playwright-e2e/reports/<ticket>
  $S scan http://localhost:5174/<path>… --setup .claude/skills/anti-ai-slop/login.mjs --out $R/truoc
  $S scan http://localhost:5173/<path>… --setup .claude/skills/anti-ai-slop/login.mjs --out $R/sau
  $S diff $R/truoc $R/sau --by order --out $R/diff
  ```
  `diff.html` has one card per change: what changed, in a sentence, and the same region cropped
  before and after. Check `diff.json` lists only changes you meant, and link `diff.html` from the
  report. Keep the pairs for scenarios a scan cannot reach (a dialog, a toast, a hover).
- A text or label change needs a pair too; a pure refactor with nothing visible does not.
- Keep the `../tc-before` worktree until the PR is merged (review fixes need new pairs), then
  `git worktree remove ../tc-before`.

## 4. Test the states, not just the happy path

For each screen, walk the state table in **frontend-ui-design** §2:

- **Validation**: submit empty; enter a cross-field conflict (end before start).
- **Conflict (409)**: open the form, change the record's state underneath it, then save:
  ```bash
  docker compose exec -T postgres psql -U trustcabinet -d trustcabinet \
    -c "UPDATE meetings SET status='WaitingApproval' WHERE id='<id>';"
  ```
- **Server error**: fail the one call, check the error state, then let it through and use
  "Thử lại". Playwright: `page.route` (§2a). CDP: patch `fetch` inside the page:
  ```js
  const real = window.fetch
  window.fetch = (u, i) => String(u).startsWith('/api/meetings?')
    ? Promise.resolve(new Response(JSON.stringify({ code: 'MSG38', message: '…' }),
        { status: 500, headers: { 'Content-Type': 'application/json' } }))
    : real(u, i)
  ```
- **Permissions**: repeat as another role in an isolated context — what they may not see must not
  appear (lists are filtered server-side, counts included), actions they may not take are hidden,
  and a direct URL gives `NotFound`.
- **URLs**: reload, Back from a detail page, a hand-edited query string with garbage values.
- **Persistence**: confirm what reached the database, not just what the screen says — for example
  that 09:00 in UTC+7 was stored as 02:00 UTC, or that a hidden field was saved empty:
  ```bash
  docker compose exec -T postgres psql -U trustcabinet -d trustcabinet -tAc "SELECT …"
  ```
- **Requests**: `performance.getEntriesByType('resource')` shows what was sent — e.g. that typing a
  search produced one request, or that an invalid range produced none. (Development builds run
  effects twice under StrictMode, so the first load shows two identical requests.)

## 5. Test data

Create what you need through the **API** (log in with `POST /api/auth/login`, then call the
endpoint) rather than inserting rows, so the data passes the same rules as real data. Prefix every
title with `[Test FE]` so it is easy to find and remove. Ask before deleting anything you did not
create.

## 6. Report and clean up

- In the PR, a **Tested locally** checklist: one line per scenario with the concrete result
  ("Rows per page 10 → 4 pages; page 4 shows 31–31 trong 31, next disabled").
- **One local HTML report per ticket**, so the user and every teammate can open it in a browser
  whatever coding agent they use. It is self-contained (images embedded) and lives outside the repo:
  write `~/.playwright-e2e/reports/<ticket>/manifest.json` (what changed, the scenarios and their
  results, each before/after pair and scenario shot with one line on what it shows, the decisions
  still open), then `python3 ~/.playwright-e2e/report.py reports/<ticket>/manifest.json` writes
  `index.html` beside it. Give the user that path and open it (`open …` on macOS). Do not publish it
  to an agent-specific host (a Claude Artifact, a Codex share link): not everyone on the team can
  open those. Rebuild it after each round of review fixes.
- In the PR, say in words what each pair shows. Do not commit screenshots, the report or test data.
- When the user has finished looking, stop both servers. **After the PR is merged**, clean up the
  rest: delete the `[Test FE]` rows, remove `~/.playwright-e2e/reports/<ticket>/`, the
  `../tc-before` worktree and any temporary branch. Until then keep them — review fixes re-run the
  same scenarios and rebuild the report.

## Browser setup notes

**Playwright** is set up per machine, outside the repo, so it adds nothing to `frontend/package.json`
or CI. To set it up on a new machine, create `~/.playwright-e2e` with:

- `package.json` with `@playwright/test` pinned (`"1.64.1"`), then `npm install` and
  `npx playwright install chromium`;
- `playwright.config.ts`: `testDir: './tests'`, `outputDir: process.env.PW_OUT ?? './test-results'`,
  `workers: 1` (one shared database), `baseURL: 'http://localhost:5173'`, viewport 1536×864,
  `locale: 'vi-VN'`, `timezoneId: 'Asia/Ho_Chi_Minh'`, `screenshot: 'only-on-failure'`,
  `trace: 'retain-on-failure'`;
- `fixtures.ts` exporting `test`, `expect`, `login(request, seed)` (POST `/api/auth/login`, returns
  the access token) and `signIn(page, seed)` (`login(page.request, …)` plus
  `page.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))`);
- `tests/smoke.spec.ts`: sign in as `thuky`, open `/calendar`, expect it not to land on `/login`.
- `pair.py` for the before/after pairs: open both PNGs with Pillow (`pip install pillow`), paste
  them side by side on a white sheet with a 44px band on top, write "Trước" and "Sau" there in a
  font with Vietnamese diacritics (PIL's built-in font has none), save.
- `report.py` for the §6 report: read the manifest (title, heading, lead, facts, changes, checks,
  pairs, shots, asks; image paths relative to it), embed each image as a JPEG data URI (pairs at
  1800px wide, shots at 1200px), write one plain `index.html` with inline CSS and no external
  requests, so it opens offline.

The setup is done when the smoke test passes against the running app.

**chrome-devtools**: the MCP server is configured per machine (`~/.claude.json` → `mcpServers.chrome-devtools`).
With `--isolated` it starts a throw-away profile every session — nothing persists, including the
app's sign-in. Without it, it keeps a dedicated profile in
`~/.cache/chrome-devtools-mcp/chrome-profile`. `--autoConnect` (Chrome 144+, after ticking
`chrome://inspect/#remote-debugging`) attaches to the user's own Chrome instead — convenient, but
the agent then drives every signed-in site in that profile; prefer the dedicated profile for
testing localhost.
