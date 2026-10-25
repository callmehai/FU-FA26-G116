---
name: sloplint
description: Find and fix the patterns that make a web UI look AI-generated ("AI slop") — decorative gradients, glows, glass, tracked-caps eyebrows, coloured side stripes, KPI tiles, icons in tinted tiles, endless motion, buzzword copy and 40-odd more — in product/app screens and landing pages. Combines a measured DOM checker (run on the rendered page at desktop and phone widths), a source scanner (CSS, Tailwind, JSX/TSX, Vue, Svelte), a weighted score, and professional fixes grounded in Cloudscape (AWS), Fluent 2 (Microsoft), Material 3 (Google), Carbon (IBM), Atlassian, Primer (GitHub) and Apple HIG. Use when building or reviewing any front-end screen, before showing screenshots, in UI code review, and whenever someone says "AI slop", "looks AI-generated", "generic", "template-y", "cheap", "vibe-coded", "trông như AI làm", "rẻ tiền", "làm FE cho tử tế".
---

# Anti AI slop

**What it is.** Models sample the safe centre of their training data (Anthropic calls it
*distributional convergence*), so an unguided UI lands on the same defaults every time. NN/g:
generated prototypes "share a similar, generic look". People recognise the combination in a second
and stop trusting the product.

**How this skill judges.** Not by taste and not by resemblance to one product. Every tell in
[`tells.json`](tells.json) is backed by several independent sources and, where possible, measured
on the rendered page. **One tell proves little; several together are the signal.** Adrian Krebs
found four or more on 22% of the 1,590 sites he measured.

Files:

| File | Use |
|---|---|
| [`tells.json`](tells.json) | The registry: id, scope (app / landing / both), weight, threshold, one-line fix |
| [`references/tells.md`](references/tells.md) | Each tell: what it looks like, why it reads as generated, how to detect, the fix with before/after code, when it is *not* a tell, what to put instead |
| [`references/design-systems.md`](references/design-systems.md) | The professional baseline the fixes aim at, with numbers and links |
| [`references/copy.md`](references/copy.md) | Copy tells and rewrites, English and Vietnamese |
| [`checker/detect.js`](checker/detect.js) | In-page checker: one function, runs in Playwright or chrome-devtools |
| [`checker/cli.mjs`](checker/cli.mjs) | `scan` (rendered pages → report.html + annotated screenshots) and `static` (source) |

## Order of authority

1. **The brief and the project's own decisions win.** Tokens, a brand colour, an ADR or design
   doc that mandates something on the list: do not quietly "fix" it. Record it in `.slop.json`
   under `allow` with the reason. It is then reported as *allowed* and not scored. If it should go,
   say so and let the owner decide.
2. **Design-system consensus** (`references/design-systems.md`) settles sizes, hierarchy,
   elevation, motion, tables and empty states.
3. **The tells** settle the rest. If you are unsure whether something reads as generated, ask
   what it communicates. If the answer is "nothing, it looks nice", it goes.

## Workflow

### 1. Set the scope and the exceptions

Decide whether the screen is `app` (product, admin, dashboard) or `landing` (marketing); a site
can be `both`. Read the project's tokens and design decisions. Write `.slop.json` at the project root:

```json
{
  "scope": "app",
  "allow": {
    "C1": "Sidebar gradient is part of the brand (ADR-12)",
    "T3": { "selector": "th", "reason": "Uppercase column headings are our table style" }
  },
  "ignore": [".third-party-widget"]
}
```

### 2. Scan the source

```bash
node <skill>/checker/cli.mjs static <src-dir>
```

It catches what the rendered page hides: Tailwind gradients and glows, `transition: all`,
`outline: none`, hover lifts, scroll-reveal libraries, emoji in markup, `·` joins, and missing
`prefers-reduced-motion`.

### 3. Scan the rendered pages, desktop and phone

```bash
node <skill>/checker/cli.mjs scan <url...> --viewports 1440,390 --hover --out slop-report
# a tab, panel or dialog: export ready({ page, url }) from the same module; it runs after load
# signed-in screens: --setup login.mjs, a module run in every fresh browser context before the page
# loads: export default async ({ context, page, url }) => { /* sign in, accept cookies */ }
# (--storage-state auth.json also works when the session survives being reused)
```

Without the CLI, evaluate the checker directly and read `findings`:

```ts
const detect = readFileSync('<skill>/checker/detect.js', 'utf8')
const { findings } = await page.evaluate(`(${detect})(${JSON.stringify({ allow, ignore })})`)
```

Before scanning, hide toasts and move the pointer to a corner so hover states do not leak in.
Scan every screen the change touches, plus its empty, error and long-content states.

### 4. Check what no machine can

These tells are `manual` in the registry. Answer each one in a sentence:

- **L14:** Does a working screen open with a hero, a greeting banner or an illustration instead of the user's work?
- **L15:** Is there a chart, sparkline or ring that does not change a decision?
- **W3:** Does any copy explain the screen rather than carry data? Is developer text leaking?
- **W6:** Is every number, logo and testimonial real and sourced?
- **A3:** Are the empty, filtered-empty, loading, error and long-content states designed?
- **A4:** Does a toast announce something already visible?

### 5. Triage, then fix

Sort every finding into one of three:

- **Tell:** fix it.
- **Allowed:** it is in `.slop.json`, so leave it.
- **False positive:** say why. For example, the shadow is on an overlay, or the gradient is a chart.

Fix in weight order (3, then 2, then 1) from `references/tells.md`. A fix **removes and
replaces**. It never swaps one trend for the next one: the cream + serif + terracotta "tasteful"
look (C7) is the current wave, not a cure. Do not strip the page sterile either. The first screen
must still show the product, the user's main job one click away, and real content at a
comfortable density.

### 6. Measure again and report

Rescan with the same checker and options. Report per screen and width:

```
Screen      Width  Score before → after  Removed                     Allowed        False positives
Meetings    1440   9 (slop) → 2 (clean)  L1 KPI tiles, S6 glow, M1   C1 (ADR-12)    S5 on the account menu (overlay)
```

Then box every change, so none is missed in review:

```bash
node <skill>/checker/cli.mjs diff <before-report> <after-report> --out slop-diff
```

Scan the old build into one report and the new one into another, with the same URLs (a different
URL is paired by order and flagged). Read `slop-diff/diff.json`: every item should be a fix you
meant. An item you did not intend is a regression; a tell you meant to fix that is not in
`fixed` is still there. Link `slop-diff/diff.html` from your report: it shows each change with
a sentence and a before/after crop per change.

## Score

Score = the sum of the weights of the distinct tells present on a page at one width, after
exceptions and thresholds. Weights are measured, not guessed: how much more often a tell appears on
AI-built pages than on professional sites ([`docs/calibration.md`](../../docs/calibration.md)).
Weight 0 means professional sites use it just as often: it is reported as *advice* with its fix,
and still worth fixing on review, but it does not mark a page as generated. A tell counts once it reaches its `min` occurrences (`tells.json`), or
when a single finding is marked strong (a gradient covering a large surface; buzzwords dense
enough to matter).

| Score | Reading |
|---|---|
| 0–2 | Clean: no pattern, or one weak one |
| 3–6 | Some slop: fix before shipping |
| 7+ | Slop: reads as generated |

Use the score to compare before and after, and one page against another. It is not a grade of
design quality. A page can score 0 and still be bad design.

## Building new UI without slop

Before writing markup:

1. **Token plan first.** Name 4–6 colours (neutral ramp, one brand, semantic states), the type
   scale (about five sizes, far apart), the radius per level, and one shadow step for overlays.
   Everything goes through `:root` variables.
2. **Name the user's main job on this screen** and make it the one primary action. Everything else
   is secondary, subtle or a link.
3. **Write the real copy**: verbs and nouns from the domain, no buzzwords, no explanations of the
   UI, errors that name the problem and the recovery.
4. **Plan the states**: empty, filtered-empty, loading, error, long names, zero, many.
5. **Spend boldness in one place, or nowhere.** One deliberate moment beats decoration everywhere.

Then build, scan at 1440 and 390, and fix before anyone sees it.

## Sources

Anthropic's *frontend-design* skill and *Improving frontend design through skills*; Adrian
Krebs, *Design slop* (1,590 sites, computed-style checks); NN/g on AI prototyping and the sparkles
icon; community skills (taste-skill, Impeccable, Hallmark, Uncodixfy, antislop, DeSlop and
others); Cloudscape, Fluent 2, Material 3, Carbon, Atlassian, Primer, Apple HIG. Links and what was
taken from each: [`references/sources.md`](references/sources.md).
