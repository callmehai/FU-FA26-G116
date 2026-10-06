---
name: verify-ui-in-browser
description: Run TrustCabinet locally and check a frontend change in a real browser through the chrome-devtools MCP — start the servers, sign in as the seeded roles, drive forms and lists, take screenshots and critique them, measure layout, simulate errors and conflicts, confirm what reached the database, then clean up. Use before opening or updating any PR that changes what the user sees, and whenever asked to "chạy local lên xem" or test a screen.
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

## 2. Drive the browser (chrome-devtools MCP)

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
  must still fit.

### Inputs that need care

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

Use `getBoundingClientRect()` in `evaluate_script` to prove layout claims:

- a toolbar is one line → every child has the same `top`;
- a form does not jump when a mode changes → `.form-card` height is equal in every mode;
- dropdown text is aligned → trigger value `x` equals the first option label `x`;
- an alert lines up with the form → equal widths;
- a menu shows its whole list → `scrollHeight <= clientHeight`.

## 4. Test the states, not just the happy path

For each screen, walk the state table in **frontend-ui-design** §2:

- **Validation**: submit empty; enter a cross-field conflict (end before start).
- **Conflict (409)**: open the form, change the record's state underneath it, then save:
  ```bash
  docker compose exec -T postgres psql -U trustcabinet -d trustcabinet \
    -c "UPDATE meetings SET status='WaitingApproval' WHERE id='<id>';"
  ```
- **Server error**: patch `fetch` inside the page for the one call, check the error state, restore
  it and use "Thử lại":
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
- Do not commit screenshots or test data.
- When the user has finished looking: stop both servers, delete the `[Test FE]` rows, remove any
  temporary worktree or branch.

## Browser setup notes

The MCP server is configured per machine (`~/.claude.json` → `mcpServers.chrome-devtools`).
With `--isolated` it starts a throw-away profile every session — nothing persists, including the
app's sign-in. Without it, it keeps a dedicated profile in
`~/.cache/chrome-devtools-mcp/chrome-profile`. `--autoConnect` (Chrome 144+, after ticking
`chrome://inspect/#remote-debugging`) attaches to the user's own Chrome instead — convenient, but
the agent then drives every signed-in site in that profile; prefer the dedicated profile for
testing localhost.
