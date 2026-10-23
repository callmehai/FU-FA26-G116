---
name: anti-ai-slop
description: Phòng chống AI slop — detect and remove the visual and copy patterns that make a screen read as machine-generated, measured with sloplint (github.com/callmehai/sloplint): a calibrated catalogue of tells from published research (Anthropic, Adrian Krebs' study of 1,590 sites, NN/g) and the shared rules of Cloudscape, Fluent 2, Material 3, Carbon, Atlassian and Primer, a checker that scores each rendered screen at 1440 and 390, and a before/after diff with a card and crop per change. Project exceptions live in .slop.json. Use for every change under frontend/src, before showing screenshots, when reviewing a FE PR, and whenever the user says "AI slop", "slop", "trông như AI làm", "rẻ tiền", "template", "lắm chữ", "rối", "làm FE cho tử tế".
---

# Anti AI slop (phòng chống AI slop)

**What it is.** Language models sample the safe centre of their training data — Anthropic calls it
*distributional convergence* — so an unguided UI lands on the same defaults every time: gradients,
glows, KPI tiles with an icon in a tinted square, a pulsing dot, an eyebrow above every heading,
uniform rounded cards with a soft shadow. NN/g: generated prototypes "share a similar, generic
look… good from afar, but far from good." People recognise the combination at a glance.

**How to judge.** Not by taste and not by resemblance to one reference product. A screen is judged
against (1) the tells below, each backed by several independent sources and most of them
measurable, and (2) the rules the major product design systems agree on. AI-eCabinet stays a
domain reference (what a screen must do), not the visual gate — it has slop of its own.

**One tell proves little; several together are the signal.** Krebs found 4+ co-occurring patterns
on 22% of sites. sloplint adds the calibrated weights of the distinct tells on a screen: 0–2 clean,
3–6 some slop, 7+ slop. Its catalogue (IDs such as `S6`, `L9`) is in the sloplint repo:
[`tells.json`](https://github.com/callmehai/sloplint/blob/main/skills/sloplint/tells.json) and
[`references/tells.md`](https://github.com/callmehai/sloplint/blob/main/skills/sloplint/references/tells.md),
each tell with its evidence and the fix.

Pairs with [frontend-ui-design](../frontend-ui-design/SKILL.md) (what a screen must do),
[minimize-text](../minimize-text/SKILL.md) (words), [check-responsive](../check-responsive/SKILL.md)
and [verify-ui-in-browser](../verify-ui-in-browser/SKILL.md) (screenshots, measuring).

## Order of authority

1. **An accepted ADR or AGENTS.md rule wins.** If it mandates something on the list (ADR-0012's
   sidebar gradient, say), do not quietly "fix" it: report it as *sanctioned*, and if it should go,
   propose a new ADR. Never edit an accepted one.
2. **Design-system consensus** (below) settles sizes, button hierarchy, elevation, motion.
3. **The tells** settle everything else. If you are unsure whether something reads as generated,
   ask what it *communicates*; if the answer is "nothing, it looks nice", remove it.

**Not tells here**, despite appearing on generic lists: Lucide icons (mandated via `Icon.tsx`),
Be Vietnam Pro (ADR-0012), a blue brand (`--brand`). The "editorial fix" — cream background, serif
display, terracotta accent — is the *next* wave of slop (Anthropic, taste, unslop-ui), not a cure.

## Design-system consensus (Cloudscape, Fluent 2, Material 3, Carbon, Atlassian, Primer, HIG)

- **One primary button per page or area**; others outline/subtle/link. Groups of 2–3; more go in a
  menu. No icon purely for decoration. Labels are verb + noun, no "please", no punctuation.
- **Status = shape or icon + text, never colour alone**, 1–2 words, at most 5–6 status colours with
  fixed meanings; grey for pending.
- **Chips/tags 18–24px high, 12px text, weight 400–500**; one shape and height per screen.
- **Colour is mostly neutral.** Brand only for primary action, links, selection, focus; never on
  large decorative surfaces.
- **Shadows only on overlays** (menus, popovers, dialogs) and draggable cards. Group with a border
  or whitespace. No decorative gradient; blur only for floating controls.
- **Motion:** 50–150ms for hover/press, 150–400ms for transitions, exits faster than entrances,
  never more than one animation at once, nothing flashing or oscillating, honour
  `prefers-reduced-motion`.
- **Type and spacing:** 14px body on 18–20px lines, 12px minimum, 4/8px spacing grid.
- **Tables:** 40px rows by default (32 compact), header and toolbar match; left-align text,
  right-align quantities, tabular numerals; ≤2 inline row actions, more in a menu.
- **Empty states:** a short heading, an optional one-line reason, one secondary action; no jokes,
  no exclamation marks; illustration optional.
- **Icons support text**, one size (16px) across the product, coloured like the adjacent text; never
  on section headings; an icon-only button has a tooltip and an accessible name.

## Sanctioned in this repo (report, do not silently change)

Declared in [`.slop.json`](../../../.slop.json) at the repository root, so sloplint marks them
*allowed* and does not score them. Changing one means a new ADR, then removing its line there.

| sloplint | Where | Mandated by |
|---|---|---|
| C1 decorative gradient | sidebar `#3f63b3 → #304f97`; room header and login band follow it | ADR-0012 |
| T3 tracked caps | table column headings "viết hoa 12px" | ADR-0012 |
| L3 icon soup chip | sensitivity chips: 4px, one icon per level; "Thường" is plain text | ADR-0012, ADR-0028 |
| S2 mixed chip shapes | call room pre-join and controls follow Google Meet (pills) | room design (SCRUM-39..44) |

List them under "Sanctioned" in the report so the lead can decide whether a new ADR should retire
any of them.

## The pass

Run it before showing any screenshot, and on review. The app must be running (see
[verify-ui-in-browser](../verify-ui-in-browser/SKILL.md)); `npx` fetches sloplint from GitHub the
first time, and `npx playwright install chromium` downloads the browser once.

1. **Scan the source** the change touches:

   ```bash
   npx -y github:callmehai/sloplint#v0.2.1 static frontend/src
   ```

2. **Scan the rendered screens** the change touches, at 1440 and at 390 with touch.
   [`login.mjs`](login.mjs) signs each page in as a seeded user (the admin for `/mgmt/admin/*`,
   the secretary elsewhere; `SLOP_SEED=chutoa` to pick one):

   ```bash
   npx -y github:callmehai/sloplint#v0.2.1 scan \
     http://localhost:5173/mgmt/meetings http://localhost:5173/calendar \
     --scope app --setup .claude/skills/anti-ai-slop/login.mjs --wait 1500 --out slop-report
   ```

   `slop-report/report.html` lists each tell with its fix and an annotated screenshot. A list
   that opens filtered to one status repeats that status on every row: pass `?status=all`.

3. **Triage each hit** as *tell* (fix it), *sanctioned* (table above) or *false positive* (say
   why, e.g. a dialog's shadow is an overlay).

4. **Fix, then prove it with a diff.** Scan the old code (the `../tc-before` worktree, see
   verify-ui-in-browser) and the new one with the same paths, then:

   ```bash
   npx -y github:callmehai/sloplint#v0.2.1 diff before after --by order --out diff
   ```

   `diff/diff.html` has one card per change, with a sentence on what changed and the same region
   cropped before and after. Read `diff/diff.json`: every item should be a change you meant; a
   tell you meant to fix that is not under `fixes` is still there. Keep the reports under
   `~/.playwright-e2e/reports/<ticket>/` and link `diff.html` from the ticket's local report.
   A fix removes; it does not swap one trend for another.

5. **Report** per screen and width: score before → after, tells removed, tells sanctioned, false
   positives.

Removing tells is half the job: "A page with every tell removed and nothing put in their place is
an empty page with good manners" (taste). The first screen must still show the product's name, the
user's main job one click away, and real data at a comfortable density.
