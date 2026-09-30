---
name: frontend-ui-design
description: How to design and build a TrustCabinet screen so it reads like one product — research before coding, the UX rules the team has settled on (forms, lists, states, dropdowns, layout stability, copy), and the checks to run. Use for any frontend work under frontend/src — a new screen, wiring a screen to an API, or changing how something looks or behaves. Pair with verify-ui-in-browser to see the result before calling it done.
---

# Designing and building a TrustCabinet screen

This is about **UI design**, not about making CSS pretty. A screen is good when a user knows
where they are, what they can do, what just happened, and what to do next — in every state, not
only the happy path. Repo conventions (tokens, `Select.tsx`, light only, 36px controls, same
origin) live in [AGENTS.md](../../../AGENTS.md); the Use Case workflow lives in
[docs/DEVELOPMENT_WORKFLOW.md](../../../docs/DEVELOPMENT_WORKFLOW.md). Read both first; this
skill does not repeat them.

## 1. Research before you draw anything

1. **The ticket.** Jira description *and* comments — BE records requirement changes there.
2. **The contract.** `docs/03-api/<area>.md`: fields, limits, defaults, every response code. Each
   code is a state the UI must handle. Read the backend validator too (the `*Rules.cs` file) for
   the exact field-error keys and messages.
3. **The spec and sequence diagram** in `docs/01-requirements/` and `docs/06-sequencediagram/`:
   which messages the user sees, and when.
4. **The reference product.** Look at how AI-eCabinet (`~/Developer/ai-ecabinet`) builds the same
   screen, **as a designer, not a copier** — never copy its code. Write down two lists: what to
   adopt and what to avoid. Its known weak spots: filters not in the URL, no empty-vs-no-match
   distinction, no cancel or unsaved-changes guard, heavy defaults, duplicated create/edit markup.
5. **What already exists here.** Reuse `PageHeader`, `Select`, `SegmentedControl`, `Dialog`,
   `ConfirmDialog`, `Pagination`, `Badge`, `StatusBadge`, toasts, `useAccessLost`, `errorMessage`.
   Match the surrounding code's comment density and naming.

If the design needs data the backend does not have (a field, a filter), that is a **requirement
change**, not a UI detail. Stop and ask the lead, check the product scope in `README.md`, and
note the decision in the Jira ticket. Example: "online meeting link" was rejected because online
meetings run in the in-app call room (README: *Gọi nhóm theo phòng họp*).

## 2. Every screen has more than one state — design all of them

| State | What the user sees |
|---|---|
| First load | Skeleton rows or a short "Đang tải…" line, sized like the real content |
| Reload / next page | Keep the previous content on screen, dimmed (`aria-busy`) — never flash empty |
| Empty, no data yet | Say so and offer the next step ("Tạo cuộc họp") if the user may take it |
| Empty, filtered | "Không có … khớp bộ lọc." plus a "Xoá bộ lọc" button — never the same text as above |
| Load error | The backend's `message` plus "Thử lại"; the rest of the page stays usable |
| Validation error | Under the field, first invalid field focused, input kept |
| Save error (5xx) | Alert near the submit button; everything the user typed is kept |
| Conflict (409) | Say why, reload the current state, withdraw the action that no longer applies |
| 404 on a record | The shared `NotFound` — same as an unknown URL, so existence is not revealed |
| 403 mid-session | `useAccessLost()` — leave the screen, reload the session |
| Success | Toast with the backend's message, then go where the user now needs to be |

## 3. Forms

- **One component for create and edit** (see `MeetingForm.tsx`); the page owns what differs
  (where data comes from, which call saves, 404/409 handling).
- **Mirror the backend rules on the client** with the backend's own messages and limits:
  `maxLength` equals the backend limit, a counter on long text, required fields marked with
  `field-required`. The backend still decides — map its `errors` object onto the fields.
- **Cross-field rules** (end after start) are checked live once both values exist, and the error
  sits on the field that must change.
- **Defaults are the backend's defaults**, and never a heavy one (not "Mật" for sensitivity).
- **Guard unsaved input**: Cancel with changes opens a `ConfirmDialog` whose buttons say what they
  do ("Bỏ thay đổi" / "Tiếp tục sửa" — never two buttons that both read "Huỷ"); `useBeforeUnload`
  covers closing the tab.
- **Submit**: disabled while pending, label changes ("Đang lưu…"), actions at the end of the form
  under a divider, primary on the right.
- **A field that depends on another replaces a cell of the same size.** Switching the meeting mode
  swaps the location input for a read-only box of equal height; every variant has a one-line hint.
  Measure it (verify-ui-in-browser) — the form must be the same height in every mode.
- **Hints are one line.** A wrapping hint makes one grid row taller than its neighbour.
- **Choose the control by the choice**: 2–4 short options → `SegmentedControl` (all visible, one
  click); more, or long labels → `Select`; never a native `<select>`.
- **Hide what the user can never do; explain what they cannot do right now.** An edit button for a
  non-editable status is hidden. When the reason is worth knowing (e.g. deleting a role that is
  still assigned), keep the control and let the dialog say why and disable its confirm button
  (`ConfirmDialog`'s `blocker`).

## 4. Lists

- **The server filters, sorts and pages** — the browser never filters rows it received.
- **All criteria live in the URL** (`useSearchParams`): reload, a shared link, and Back from a
  detail page return to the same view. Filter changes use `replace: true`. Read the URL
  defensively — drop unknown values instead of sending them, clamp a page past the end.
- **Search waits ~300 ms** after typing stops: one request per word, not per keystroke.
- **Sortable headers** are buttons with ↕/↑/↓ and `aria-sort` on the `<th>`.
- **Paging footer**: "21–40 trong 45 …", rows-per-page, page buttons with an ellipsis.
- **The toolbar fits on one line at a 1200px-wide window.** Let the search box shrink
  (`search-flex`); make secondary controls icon-only with a tooltip before letting it wrap.
- **Row = link**: the whole row opens the record; the title link and an icon button remain for
  keyboard and middle-click.
- **Dates**: a date-only range means whole local days — `from` 00:00, `to` 23:59:59.999, converted
  to UTC (`lib/dateInput.ts`).

## 5. Details that separate "works" from "looks right"

- **Align text on one vertical line.** A dropdown's option text starts exactly where the trigger's
  value does; the selected check sits at the end of the row. Never indent every row to make room for
  one marker.
- **A dropdown shows its whole fixed list** (menu max-height fits the longest one); only genuinely
  long lists scroll.
- **Page-level forms are one readable column** (`form-page`, 760px), left-aligned under the page
  header so the page keeps a single left edge. Alerts about the form share that column.
- **Flex children that must not shrink** get `flex: none` (`.table-card`, `.form-card`) — inside
  the column-flex `.content` they otherwise shrink to the viewport and spill past the padding.
- **Grids that mix short and tall cells** use `align-items: start`.
- **Unset values** read as absent — muted italic "Chưa có", "Chưa xác định" — not an em dash.
- **Free text** keeps its line breaks (`white-space: pre-wrap`).
- **Icons** come from `components/Icon.tsx` (add a path there), never a text glyph like ✓.

## 6. Features used during a meeting are panels

Agenda, attendance, speaking requests, live voting, documents, recording and the secretary's
notes are used **during** a meeting, and an online meeting happens in the full-screen call room,
where navigating the tab away drops the call. So each of them is written **once** as a panel that
both the meeting detail tab and the room's side panel render —
[ADR-0011](../../../docs/05-decisions/0011-tinh-nang-hop-la-panel-dung-chung.md).

- `src/features/<feature>/<Feature>Panel.tsx`, props `{ meetingId, variant: 'page' | 'room' }`.
- It loads its own data through `src/api/` and subscribes to `/hubs/meetings` itself; the parent
  passes nothing but the id.
- It works at **360px** wide and has no `PageHeader` or breadcrumb inside.
- Actions open a `Dialog`/`ConfirmDialog`, never navigate. In `variant='room'`, a link to another
  screen opens a **new tab** (`target="_blank" rel="noopener"`).
- What the user may do comes from the server, never from guessing on their role.
- Export the feature's API calls from `src/api/<feature>.ts` so the room can reuse them (the room's
  "raise hand" button calls the speaking-request API).
- Events everyone must notice (a vote opens, someone is invited to speak, recording starts) are
  published on the hub so the room can show them.

Verify both places: the detail tab, and the panel at 360px inside a narrow window.

## 7. Copy

Vietnamese, specific, and about consequences: "Lưu thay đổi không tự gửi duyệt lại — trạng thái vẫn
giữ nguyên." beats "Lưu ý khi sửa". Say what a button does ("Tạo cuộc họp", "Lưu thay đổi"). Show
the backend's `message` when there is one; branch on `code`, never on text.

## 8. Before you commit

```bash
cd frontend && npm run lint && npm run build
```

Format only the files you wrote. The repo has no Prettier config; this matches its style:

```bash
npx prettier --print-width 120 --single-quote --no-semi --write <your files>
```

Check `git diff` afterwards and revert any reformatting of lines you did not change.

Then run **verify-ui-in-browser**, and put what you tested in the PR's "Tested locally" list.
Screens that change user-facing behaviour also update `docs/03-api/` where the contract's
"Hợp đồng tích hợp FE" section describes it.
