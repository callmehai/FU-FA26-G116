---
name: anti-ai-slop
description: Phòng chống AI slop — detect and remove the visual and copy patterns that make a screen read as machine-generated, using a catalogue of tells sourced from published research (Anthropic's frontend-design guidance, Adrian Krebs' measured study of 1,590 sites, NN/g) and from the shared rules of Cloudscape (AWS), Fluent 2 (Microsoft), Material 3 (Google), Carbon (IBM), Atlassian and Primer (GitHub). Ships a deterministic DOM checker (slop-check.js) to run on every screen at 1440 and 390, a grep pass for the source, and a rule for tells our own ADRs mandate. Use for every change under frontend/src, before showing screenshots, when reviewing a FE PR, and whenever the user says "AI slop", "slop", "trông như AI làm", "rẻ tiền", "template", "lắm chữ", "rối", "làm FE cho tử tế".
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
on 22% of sites. Count them per screen.

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

## The tells

`Key` is the field in `slop-check.js` output. `Src` counts independent sources naming it (out of
the nine anti-slop guides surveyed, plus Krebs' measured rate where he has one).

| # | Tell | Key | Evidence | Instead |
|---|---|---|---|---|
| T1 | **Decorative gradients** — washes on surfaces, sidebars, headers, buttons | `gradients` | 9/9; top pattern in Krebs (28%) | Flat token colour. No design system recommends decorative gradients |
| T2 | **Gradient text** | `gradientText` | 6/9 | Ink |
| T3 | **Glow** — coloured, blurred shadow; halo on a CTA; big dramatic drop shadow | `glow` | 7/9 | Shadows only on overlays (menus, dialogs). Buttons rest flat (M3 level 0, Atlassian, Carbon) |
| T4 | **Glassmorphism** — backdrop blur as decoration | `glass` | 7/9; Krebs 17%; HIG: never in the content layer | Opaque surface |
| T5 | **Eyebrows and shouting labels** — tracked UPPERCASE above headings, in labels, form sections | `caps` | 7/9; Krebs 10.5%; Atlassian, Fluent: no all caps | Sentence case, untracked, the body face |
| T6 | **Coloured side stripe** — 3–4px left/top border on a card, alert, banner | `stripes` | 5/9; Krebs 13%, "almost as reliable as em-dashes for text" | Put the colour on the one element that means it (icon, status chip) |
| T7 | **Mixed or arbitrary chip shapes** — pills next to squares, several heights in one view | `pills`, `pillButtons` | Fluent: "avoid mixing badge sizes"; Carbon/Primer use pills, Atlassian/M3 4–8px | One shape and one height per kind. Shape is not the tell; inconsistency is. Pill *buttons* in a dense admin UI are |
| T8 | **Endless motion** — pulsing dots, rippling CTAs, nudging arrows, shimmer | `endless` | 5/9 ("it is just never inactive"); Atlassian: no simultaneous or oscillating motion | Nothing loops. Motion only answers an action, 50–150ms for hover/press, 150–400ms for transitions |
| T9 | **Icon in a tinted tile** — glyph in a coloured square/circle beside a heading, card, row | `iconTiles` | 8/9 (part of the "SaaS card kit") | The glyph alone, coloured like its text, or nothing (Cloudscape: no icons on headings) |
| T10 | **Icon soup** — icon + words + tint + border on one small chip | `iconSoup` | Cloudscape: "Don't include icons on badges" | Words alone, or an icon-only control with a tooltip |
| T11 | **KPI tile row** — 3–6 identical cards, icon tile, big coloured number, caption | `kpiRows` | 6/9; taste's 23 measured product screens: "KPIs are type, not tiles" | Label and number as type; no caption repeating the label; no colour per card |
| T12 | **Emoji or sparkles** standing in for icons | `emoji` | 6/9; NN/g on the sparkle icon | `components/Icon.tsx` glyphs |
| T13 | **Template meta strings** — "A · B · C", "WORD — fragment", `→` glued to labels | `metaStrings` | 4/9 (Anthropic current skill, Hallmark, taste, Impeccable) | Separate cells, a second line, or a comma. Buttons say what they do without an arrow |
| T14 | **Dash for an empty value** | `dashCells` | Repo rule (frontend-ui-design §5) | Muted italic words: `<em className="value-empty">Chưa có mô tả</em>` |
| T15 | **Text under 12px** | `tiny` | Cloudscape, Carbon, ADR-0012 | 12px floor |
| T16 | **One radius on everything** | `oneRadius` | 8/9 ("one border-radius regardless of hierarchy") | Radius per nesting level; flag a single value ≥12px covering >70% of boxes |
| T17 | **Elevation soup** — border + shadow (+ ring) on the same resting box | `elevationSoup` | DeSlop, anti-ai-slop; Atlassian: raised only for movable cards | Border *or* whitespace. Overlays (dialog, menu, popover) are exempt |
| T18 | **Accent sprawl** — many saturated hues, colour that means nothing | `accentHues` | 4/9; Fluent: "Don't use semantic colors for decoration"; HIG | Neutral UI, brand blue for action/selection, semantic colours only for state |
| T19 | **Competing primaries** — several solid filled buttons on one screen | `solidButtons` | Cloudscape, Carbon, Atlassian, Fluent, Primer: one primary per page | One primary; the rest outline, subtle or link |
| T20 | **Words that explain the screen** — subtitles restating the title, section notes, dev text ("chờ backend…") | `words` + read | Uncodixfy, taste; Cloudscape: "don't repeat heading text in the description" | Delete. A header line carries data (a count), not an explanation. See minimize-text |
| T21 | **Toast for every action** whose result is already visible | read the code | vybe docs, NN/g | Toast only when the result is off-screen or failed |
| T22 | **Only the happy path designed** | read the code | 4/9; NN/g | Empty, filtered-empty, loading, error, long names — frontend-ui-design §2 |
| T23 | **Fake data** — invented metrics, sparklines, testimonials, sample rows | read the code | 4/9; ADR-0012 ("không bịa dữ liệu mẫu") | Real data or an honest empty state |

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

| Tell | Where | Mandated by |
|---|---|---|
| T1 gradient | sidebar `#3f63b3 → #304f97`; room header and login band follow it | ADR-0012 |
| T5 caps | table column headings "viết hoa 12px" | ADR-0012 |
| T10 icon soup | sensitivity chips: 4px, one icon per level; "Thường" is plain text (ADR-0028) | ADR-0012 |
| T20 dev text | placeholder screens say which endpoint they wait for | ADR-0012 |
| T7 pills | call room pre-join and controls follow Google Meet | room design (SCRUM-39..44) |

List them under "Sanctioned" in the report so the lead can decide whether a new ADR should retire
any of them.

## The pass

Run it before showing any screenshot, and on review.

1. **Grep the diff** (or the whole `frontend/src` on a sweep):

   ```bash
   cd frontend/src && grep -rnE \
     'gradient|backdrop-filter|text-transform: *uppercase|letter-spacing: *\.[0-9]|border-(left|top): *[3-9]px|inset [3-9]px 0|animation:.*infinite|transition: *all|:hover[^{]*\{[^}]*(translate|scale)|box-shadow:[^;]*#[0-9a-f]{6}[0-9a-f]{2}' \
     --include='*.css' --include='*.tsx' .
   grep -rnE "' · '|\\$\\{[^}]*\\} · |→|'—'" --include='*.tsx' .
   ```

2. **Measure the rendered page** on every screen the change touches, at 1440 and at 390 with touch:

   ```ts
   const check = readFileSync('.claude/skills/anti-ai-slop/slop-check.js', 'utf8')
   const report = await page.evaluate(`(${check})()`)
   ```

   With the chrome-devtools MCP, pass the file's text to `evaluate_script` as the function. Hide
   toasts first and move the mouse away so hover states do not leak in.

3. **Triage each hit** as *tell* (fix it), *sanctioned* (table above) or *false positive* (say
   why — e.g. a dialog's shadow is an overlay). Count real tells per screen.

4. **Fix, then measure again** with the same checker, and capture before/after pairs (repo rule:
   local HTML report, 1440 + 390). A fix removes; it does not swap one trend for another.

5. **Report** per screen: tells removed, tells sanctioned, false positives. Name what you deleted so
   the lead sees the pass happened.

Removing tells is half the job: "A page with every tell removed and nothing put in their place is
an empty page with good manners" (taste). The first screen must still show the product's name, the
user's main job one click away, and real data at a comfortable density.

## Sources

- Anthropic, *frontend-design* skill (github.com/anthropics/skills, Sep 2026) and *Improving
  frontend design through skills* (claude.com/blog, Nov 2025).
- Adrian Krebs, *Design slop* — Playwright computed-style checks over 1,590 Show HN sites
  (adriankrebs.ch/blog/design-slop); per-pattern rates via GIGAZINE (Apr 2026).
- NN/g, *AI prototyping* and *The AI sparkles icon problem* (nngroup.com).
- Community skills: taste-skill (`references/tells.md`, `app-screens.md`), Impeccable
  (`craft-floor.md`), Hallmark (`gates.md`), Uncodixfy, antislop, anti-ai-slop, DeSlop, unslop-ui.
- Design systems: Cloudscape (badge, status indicator, button, table, empty states, motion,
  typography), Fluent 2 (badge, button, color, elevation, motion, shapes), Material 3 tokens
  (material-web v0_192), Carbon (tag, button, status pattern, data table, motion, color), Atlassian
  (lozenge, button, radius, elevation, motion, color), Primer (label, button, data table, color),
  Apple HIG (color, materials, motion, icons).
