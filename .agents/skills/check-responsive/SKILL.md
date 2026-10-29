---
name: check-responsive
description: Check every TrustCabinet screen change on a phone, a narrow laptop and a desktop before calling it done — the page never scrolls sideways, nothing is clipped inside its card, the table columns people need stay visible, dialogs fit with their buttons reachable, touch targets are 40px, and no action explains itself only on hover (touch screens have no hover, so `data-tooltip` never shows). Use for any change under frontend/src, together with verify-ui-in-browser, and whenever the user asks "test mobile chưa", "responsive", "trên điện thoại thì sao", "màn hẹp".
---

# Checking a screen at every width

A screen that works at 1440px can still be unusable on a phone: a column pushed out of its card, a
dialog whose buttons sit under the keyboard, an icon nobody can identify because its tooltip needs a
mouse. Desktop screenshots never show these. Check the widths below for **every** change that
affects what the user sees — not only when the change "looks like layout work".

Why this skill exists: SCRUM-97 was checked at 1440px and 1200px only. On a phone the attendees
table hid the role column and the row actions behind a sideways scroll, and the new toolbar button
was an icon whose meaning lived in a hover tooltip.

## 1. The widths

| Name | Viewport | Playwright options | What it catches |
|---|---|---|---|
| Phone | 390×844 | `isMobile: true, hasTouch: true` | Sidebar is the 72px rail (always collapsed under 720px, ADR-0012); no hover |
| Small phone | 360×740 | same | Android; also the room's side panel width (ADR-0011) |
| Narrow laptop | 1200×800 | — | Toolbars must still be one line (frontend-ui-design §4) |
| Desktop | 1440×900 | — | The default the other specs use |

Phone (390) is mandatory for every visible change. Run 360 when the change touches a panel used in
the call room, a dialog, or a toolbar.

## 2. Running it

Put the phone run in the ticket's spec, next to the desktop scenarios
(verify-ui-in-browser §2a), so the re-run after review covers both:

```ts
test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })
  test('attendees tab', async ({ page }, info) => {
    await signIn(page, 'chutoa')
    // …open the screen, use tap() for buttons…
    await page.screenshot({ path: info.outputPath('phone-attendees.png') })
  })
})
```

With the chrome-devtools MCP, use `emulate` (viewport `390x844x3,mobile,touch`) or
`resize_page`, then the same checks through `evaluate_script`.

## 3. What to check — measure it

1. **The page never scrolls sideways**:
   `document.documentElement.scrollWidth <= window.innerWidth`.
2. **Nothing spills out of its card.** A card that scrolls sideways inside hides columns just as
   badly. List what sticks out:
   ```ts
   const clipped = await page.evaluate(() =>
     [...document.querySelectorAll('.list-panel *, .card *, .dialog *')]
       .filter((el) => {
         const box = el.closest('.list-panel, .card, .dialog')!.getBoundingClientRect()
         const r = el.getBoundingClientRect()
         return r.width > 0 && (r.right > box.right + 1 || r.left < box.left - 1)
       })
       .map((el) => `${el.tagName}.${(el as HTMLElement).className}`)
       .slice(0, 10),
   )
   expect(clipped).toEqual([])
   ```
3. **Tables keep the columns people act on** — the name, its status or role, and the row actions
   — visible at 390 without sideways scrolling. If they do not fit, re-flow the table under the
   breakpoint (secondary cells stack under the first one, actions stay at the row's end) instead
   of leaving it to an inner scroll.
4. **No hover.** `@media (hover: none)` applies: `data-tooltip` and `title` never appear. Every
   icon-only button must be recognisable without its tooltip — otherwise show its label on narrow
   or touch screens. Facts that minimize-text moved into tooltips must still be reachable by tap or
   focus.
5. **Touch targets** are at least 40px high and wide (ADR-0043: controls grow to 40px on a touch screen), 8px apart.
6. **Dialogs fit**: height ≤ `innerHeight - 32`, the body scrolls, the footer buttons are on
   screen. Focus a field and shrink the viewport height to 500px (the on-screen keyboard): the
   primary button must still be reachable by scrolling the dialog.
7. **Toolbars wrap into tidy rows** — the search box takes the first row, the buttons the next —
   never squeezed into one cramped line or cut off.
8. **Stable heights still hold** where lines wrap: a hint or a consequence line that is one line on
   desktop may be two on a phone; check that picking an option does not make the dialog jump there
   either (frontend-ui-design §3).
9. **Text** stays at 12px or more and is not cut off without a way to read the rest.

## 4. Evidence

- Before/after pairs at the phone width as well as the desktop one (verify-ui-in-browser §3), in
  the same report.
- In the PR's "Tested locally", one line per width with the measured result ("390×844: no sideways
  scroll, 0 clipped elements, dialog 610px of 844px").
- Fix what the phone run finds before the PR, or say in the PR which problem is left and why
  (for example, one that was already on `main`).
