---
name: anti-ai-slop
description: Phòng chống AI slop — keep TrustCabinet screens from looking machine-generated. Every visible element must trace back to the same AI-eCabinet screen, the ticket, or an existing TrustCabinet component; anything else is invented decoration and goes. Catalogues the tells the lead has rejected (pill chips, decorative counter chips, a button per option where eCab has one picker, every CTA in one colour, chips trailing text out of line, solid-next-to-tint chips, glow and icon soup) and gives a slop pass with measurements to run before any FE change is shown. Use for every change under frontend/src, before showing screenshots, and whenever the user says "AI slop", "slop", "trông như AI làm", "lắm chữ", "rối", "không giống eCab", "làm FE cho tử tế".
---

# Anti AI slop (phòng chống AI slop)

"AI slop" here means UI that looks generated: widgets nobody asked for, the generic SaaS look
(pills, glows, stat chips, icons on everything), and layouts that are almost aligned. The lead
reads a screen in a second and rejects it on sight. The cure is not taste in the abstract; it is
**copying the reference's decisions and inventing nothing**.

Pairs with [frontend-ui-design](../frontend-ui-design/SKILL.md) (what a screen must do),
[minimize-text](../minimize-text/SKILL.md) (words) and
[verify-ui-in-browser](../verify-ui-in-browser/SKILL.md) (screenshots, measuring).

## Rule 0 — nothing without a source

Before building, write down every element the screen will show, and next to each its source:

| Source | Example |
|---|---|
| The **same screen in AI-eCabinet** (`~/Developer/ai-ecabinet/clients/web/src`, `git pull` first) — read its code, not its docs | eCab's `AttendanceCell.tsx`: one 156×32 `Select`, dashed "Điểm danh" placeholder, dot + word |
| An **acceptance criterion** of the Jira ticket | "Chưa điểm danh khác Absent" |
| An **existing TrustCabinet component or token** | `Select`, `.role-chip`, `--live` |

An element with no source is deleted, however useful it seems. If it really is needed, it is a
requirement change: ask the lead first, do not ship it to find out.

From eCab take the **measurements**, not only the idea: height, radius, font size and weight,
padding, colour of each state, what is a control and what is text, what has no border. Port them
onto our tokens; never paste eCab files.

## The tells

Each was rejected on a real screen. Check every one.

1. **Pills.** A chip whose corners are half its height (`border-radius: 12px` on a 24px chip). eCab
   chips are square-cornered, 6px (`--radius`). Pills read as a template. *(2026-10-23: role chips,
   every `.badge`.)*
2. **Decorative counters and stat chips.** "Có mặt 1/5" with an icon on a tinted pill in the
   toolbar — eCab has no counter there. A number on screen needs a source like any other element.
   *(2026-10-23.)*
3. **A button per option where the reference has one picker.** "✓ Có mặt | ✗ Vắng" on every row
   doubled the words and filled the table. eCab: one compact select that shows only the current
   value. Show the value; reveal the options on demand. *(2026-10-23.)*
4. **Every call to action in one colour.** Three blue primaries on one screen ("Vào phòng họp",
   "Tham dự", add person). A colour must mean something: brand blue manages, `--live` green joins
   what is happening now, the answer chip's tint answers. At most **one** rippling button per
   screen. *(2026-10-23.)*
5. **Items that trail variable-length text.** A role chip placed right after an email starts at a
   different x on every row. Everything in a list lines up on one vertical edge: under the name
   (avatar + gap), or in its own column with a fixed width first in the line. *(2026-10-23,
   narrow roster.)*
6. **One solid among tints.** A solid Chair chip beside a tinted Secretary chip reads as one
   colour twice. A set of chips is all tints of their own hues, as eCab's roles. *(2026-10-23.)*
7. **Icon soup.** Icon + label + tinted background + border on one small element. An icon earns
   its place by replacing words (an icon-only button with a tooltip), not by decorating them.
8. **Effects eCab does not have.** Glow, gradient, coloured shadow, ring, emoji, a second
   animation. Our only motion is the one CTA ripple and the arrow nudge.
9. **Words that explain the screen.** Hints repeating the label, sentences under headers, a toast
   for every successful click on a control whose new value is already visible. See minimize-text.
10. **Dense rows.** Controls at full 40px inside table rows, two lines of buttons per row. In a row,
    a control is 32px (40px only for touch, `pointer: coarse`), one line, eCab's whitespace.
11. **Fake polish states.** Skeleton shimmer on things that load in 50 ms, empty states with
    illustrations, "N/A" or "—" for unset values (use the muted "Chưa có" pattern).

## The slop pass (before showing anything)

Run it on the screenshots of every viewport you test (1440, 1200, 390 touch):

1. **Trace.** Point at each element in the screenshot and name its source from Rule 0. Delete
   orphans.
2. **Side by side with eCab.** Put the eCab screen (its code, or the running demo when the user
   provides it) next to ours. Count elements per row and words per row; ours must not have more.
3. **Measure, don't eyeball.** In Playwright:

   ```ts
   // No pills: every chip's radius is below half its height (.status is the one deliberate pill).
   const pills = await page.locator('.badge, .role-chip, [class*="chip"]:not(.status)').evaluateAll((els) =>
     els.filter((e) => e.getBoundingClientRect().height > 8 // skip .visually-hidden and hidden copies
       && parseFloat(getComputedStyle(e).borderTopLeftRadius) >= e.getBoundingClientRect().height / 2 - 1)
       .map((e) => e.className))
   // Every list lines up: one left edge per kind of item.
   const lefts = (sel: string) => page.locator(sel).evaluateAll((els) =>
     new Set(els.map((e) => Math.round(e.getBoundingClientRect().left))).size)
   expect(await lefts('.participants-panel .col-role .role-chip')).toBe(1)
   // CTA colours: no more than one solid primary per colour, one ripple.
   const ctas = await page.locator('.btn-cta:visible').evaluateAll((els) =>
     els.map((e) => [getComputedStyle(e).backgroundColor, getComputedStyle(e).animationName]))
   ```

   `.status` (meeting state) is a deliberate pill — it must differ from the sensitivity `.level`
   by shape (AGENTS.md); it is excluded above, do not "fix" it.
4. **Name what you removed.** When reporting, list what the slop pass deleted or changed, so the
   lead sees the pass happened.

If after all this you are unsure whether something reads as generated, it does. Remove it.
