# Tells: what makes a front-end look generated, and how to fix it

A *tell* is a design move that a model produces by default, everywhere, without a reason. Most
tells began as good ideas on good sites; they became tells through repetition. Anthropic names the
mechanism "distributional convergence": without direction, the model "samples from this
high-probability center" of its training data
([Anthropic](https://claude.com/blog/improving-frontend-design-through-skills)). NN/g describes
the result as "good from afar, but far from good"
([NN/g](https://www.nngroup.com/articles/ai-prototyping/)).

This file is the human-readable side of [`tells.json`](../tells.json). The registry is the
contract: ids, slugs, titles, weights and detection modes live there, and the checker, the static
scanner and the report all key on `id`. The rules each fix leans on are collected, with links, in
[design-systems.md](design-systems.md).

## How to read an entry

Each entry opens with one line of metadata: category, **scope** (`app` for product, admin and
dashboard screens; `landing` for marketing pages; `both`), **weight** and **detect**.

Weights are measured on real pages: how much more often the tell appears on AI-built pages than
on professional sites ([calibration](../../../docs/calibration.md)).

| Weight | Meaning |
|---|---|
| 3 | At least 4× more common on AI-built pages. Rarely shipped on purpose by a team with a design system. |
| 2 | 2–4× more common. A clear tell, but has legitimate uses. |
| 1 | Somewhat more common. Weak alone; counts in combination. |
| 0 | Advice. Professional sites use it as often, so it says nothing about generation. The fix still stands as design-quality advice. |

| Detect | Mechanism |
|---|---|
| `dom` | The rendered-page checker reads computed styles and the DOM. |
| `static` | The source scanner greps CSS, class names and JSX. |
| `manual` | Nobody can measure it; a person or agent answers the checklist question in the entry. |

The sections of an entry:

- **Looks like**: the pattern, with the CSS or Tailwind values it usually arrives in.
- **Why it reads as generated**: the evidence, linked. Percentages marked *Krebs* are per-pattern
  rates from Adrian Krebs' first 500-site run over Show HN landing pages, as reported by
  [GIGAZINE](https://gigazine.net/gsc_news/en/20260423-design-slop). They describe landing pages;
  nobody has measured dashboards the same way.
- **Detect**: what the checker looks at, or the question to ask.
- **Fix**: the correction, then a Before / After pair.
- **Not a tell when**: the legitimate uses. If your project hits one of these on purpose, declare
  it in `.slop.json` instead of bending the design.
- **Put instead**: what fills the space, for tells whose removal leaves a hole.

**Tokens used in the fixes.** Every After block uses CSS custom properties instead of new colours:
`--canvas` (page ground), `--surface` and `--surface-muted` (panels), `--ink` and `--muted-ink`
(text), `--border` and `--border-strong`, `--brand`, `--brand-hover`, `--brand-ink` (text on
brand), `--danger`, `--warning`, `--success`, `--focus-ring`, `--radius-sm` / `--radius-md` /
`--radius-lg`, `--shadow-overlay`, `--font-body` / `--font-display` / `--font-mono`, and the type
steps `--text-xs` to `--text-xl`; a theme block may also point tokens at a raw ramp
(`--neutral-950`). Rename them to your project's tokens. If your project has none,
creating them is the first fix (see [C6](#stock-palette)).

## One tell proves little

Krebs is the only person who has measured these patterns at scale. He ran deterministic CSS and
DOM checks on computed styles, not a model judging screenshots, and estimates 5–10% false
positives ([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Over 1,590 sites, 22% matched
four or more patterns and 32% matched two or three. He and GIGAZINE both stress that any single
pattern proves nothing, and that unedited template sites looked like this before models existed.

So treat a single weight-1 hit as noise and a single weight-3 hit as a question. The signal is
several tells together, from different categories: a gradient hero, an eyebrow, three icon cards,
a soft shadow under each, and a fade-up on scroll. A score is a prompt to look, not a verdict.

---

## Contents

**Colour**
[C1 · Decorative gradient](#decorative-gradient) ·
[C2 · Gradient text](#gradient-text) ·
[C3 · Default indigo/violet primary](#ai-purple) ·
[C4 · Accent sprawl](#accent-sprawl) ·
[C5 · Neon on near-black](#neon-on-dark) ·
[C6 · Untouched framework palette](#stock-palette) ·
[C7 · Cream + serif + terracotta](#editorial-wave)

**Typography**
[T1 · Default face as the whole identity](#default-font-only) ·
[T2 · Trend display face](#trend-display-font) ·
[T3 · Eyebrows and tracked caps](#eyebrow-caps) ·
[T4 · One accented word in a headline](#accent-word) ·
[T5 · Monospace as costume](#mono-costume) ·
[T6 · Many near-equal sizes](#flat-type-scale) ·
[T7 · Text under 12px](#tiny-text) ·
[T8 · Stranded last word](#stranded-word)

**Shape, depth and effects**
[S1 · One big radius on everything](#one-radius) ·
[S2 · Mixed chip shapes and heights](#shape-mix) ·
[S3 · Coloured side stripe](#side-stripe) ·
[S4 · Soft shadow under every card](#shadow-everything) ·
[S5 · Border + shadow on one resting box](#elevation-soup) ·
[S6 · Glow and dramatic shadow](#glow) ·
[S7 · Glassmorphism](#glass) ·
[S8 · Cards inside cards](#nested-cards) ·
[S9 · Aurora/blob background](#blob-background)

**Layout and components**
[L1 · KPI tile row](#kpi-tiles) ·
[L2 · Icon in a tinted tile](#icon-tile) ·
[L3 · Icon soup chip](#icon-soup) ·
[L4 · Centred hero, then three icon cards](#hero-three-cards) ·
[L5 · Pill badge above the hero](#hero-pill) ·
[L6 · 01 / 02 / 03 markers](#index-numbers) ·
[L7 · Fake window chrome](#fake-chrome) ·
[L8 · Emoji and sparkles as icons](#emoji-icons) ·
[L9 · Decorative status dots](#status-dot) ·
[L10 · Same badge on every row](#badge-every-row) ·
[L11 · Middle-dot meta strings and glued arrows](#meta-strings) ·
[L12 · Dash for an empty value](#dash-empty) ·
[L13 · Competing primary buttons](#competing-primaries) ·
[L14 · Hero or welcome banner inside the app](#hero-in-app) ·
[L15 · Charts standing in for content](#fake-charts)

**Motion**
[M1 · Endless motion](#endless-motion) ·
[M2 · Hover lift and scale](#hover-lift) ·
[M3 · transition: all](#transition-all) ·
[M4 · Fade-up on every section](#fade-up-reveal) ·
[M5 · Motion without a reduced-motion path](#no-reduced-motion)

**Copy and content**
[W1 · Buzzword copy](#buzzwords) ·
[W2 · Generic call to action](#generic-cta) ·
[W3 · Copy that explains the screen](#explainer-copy) ·
[W4 · Exclamation marks and cheer](#exclamations) ·
[W5 · Template leakage](#template-leak) ·
[W6 · Invented social proof](#invented-proof)

**Accessibility and states**
[A1 · Low-contrast text](#low-contrast) ·
[A2 · Focus ring removed](#focus-removed) ·
[A3 · Only the happy path](#happy-path-only) ·
[A4 · A toast for every action](#toast-everything)

Closing: [Fixing without creating the next wave](#fixing-without-creating-the-next-wave)

---

## Colour

<a id="decorative-gradient"></a>
### C1 · Decorative gradient

`color` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** Linear or radial gradients on surfaces that carry no information: the sidebar,
the page header, the primary button, a badge, a card's top edge. Usually indigo to violet or
violet to pink: `bg-gradient-to-r from-indigo-500 to-purple-600`, `#667eea → #764ba2`.

**Why it reads as generated.** It is the most common pattern Krebs measured
([Krebs](https://www.adriankrebs.ch/blog/design-slop/), 28% of sites). Commentators call the
blue-to-purple gradient "the single loudest AI tell in 2026"
([925studios](https://www.925studios.co/blog/ai-slop-design-tells)) and "the Times New Roman of
AI-generated design" ([Fountain](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)).
Anthropic's own skill lists "gradient washes as decoration" in its SaaS-card cluster
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).

**Detect.** `dom`: elements whose computed `background-image` contains `linear-gradient` or
`radial-gradient` on chrome (nav, header, buttons, badges, cards), excluding charts and progress
bars. `static`: `bg-gradient-to-`, `from-purple-`, `from-indigo-`, `from-violet-`.

**Fix.** Flat token colour. Product design systems separate surfaces with neutrals and spend
colour on action and state ([Cloudscape](https://cloudscape.design/foundation/visual-foundation/colors/));
Fluent says to avoid brand colour on large surfaces ([Fluent 2](https://fluent2.microsoft.design/color)).
Keep a gradient only where the change of colour *is* the information.

```css
/* Before */
.sidebar     { background: linear-gradient(180deg, #4f46e5, #7c3aed); }
.btn-primary { background: linear-gradient(90deg, #6366f1, #ec4899); }

/* After */
.sidebar     { background: var(--surface); border-right: 1px solid var(--border); }
.btn-primary { background: var(--brand); color: var(--brand-ink); }
```

Tailwind: `bg-gradient-to-r from-indigo-500 to-purple-600` → `bg-brand text-brand-ink` (theme
colours mapped to the tokens).

**Not a tell when.** The gradient encodes data: a heat scale, a progress fill, a sequential
legend. A scrim under text laid over a photo. A gradient that is part of the registered brand
mark, used where the mark is used.

**Put instead.** The colour budget moves to the one primary action and to real content: a product
screenshot, a photograph, the data itself.

<a id="gradient-text"></a>
### C2 · Gradient text

`color` · scope `both` · weight 2 · detect `dom` `static`

**Looks like.** The H1, or one phrase of it, filled with a gradient through
`background-clip: text` and a transparent colour: `bg-gradient-to-r from-purple-500 to-pink-500
bg-clip-text text-transparent`.

**Why it reads as generated.** It is on every ban list that names surfaces:
"gradient bg-clip-text headlines" ([Anti-AI-UI](https://github.com/Vanszs/Anti-AI-UI)), first-wave
and banned in taste-skill ([taste-skill](https://github.com/Leonxlnx/taste-skill)), and in
Impeccable's refuse list with the reason "Emphasis comes from weight or size"
([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `dom`: computed `background-clip` / `-webkit-background-clip` is `text` and `color`
or `-webkit-text-fill-color` is transparent. `static`: `bg-clip-text` with `text-transparent`.

**Fix.** Solid ink. A headline earns emphasis from size, weight and where it sits, and a solid
colour keeps contrast measurable and text selection readable.

```css
/* Before */
h1 {
  background: linear-gradient(90deg, #6366f1, #ec4899);
  -webkit-background-clip: text; background-clip: text;
  color: transparent;
}

/* After */
h1 { color: var(--ink); font: 700 var(--text-xl)/1.15 var(--font-display); }
```

**Not a tell when.** A logotype drawn as a gradient by the brand, shipped as the brand's SVG. That
is an image of the mark, not a text treatment.

<a id="ai-purple"></a>
### C3 · Default indigo/violet primary

`color` · scope `both` · weight 2 · detect `dom` `static`

**Looks like.** Primary buttons, links and focus accents in the framework default:
`indigo-500 #6366F1`, `violet-500 #8B5CF6`, `purple-600`, or a shadcn `--primary` with hue 255–280.

**Why it reads as generated.** Krebs found a purple CTA on 10.7% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Anthropic's prompt warns against
"purple gradients on white backgrounds"
([Anthropic cookbook](https://platform.claude.com/cookbook/coding-prompting-for-frontend-aesthetics)).
The usual origin story is Tailwind UI's `bg-indigo-500` default, which Adam Wathan joked about in
2025; we only found that remark paraphrased, not the original post
([prg.sh](https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website)).

**Detect.** `dom`: the computed `background-color` of `button`, `[role=button]` and `a[class*=btn]`
converted to HSL, flagged at hue 240–290 with saturation above 50%. Mind the edge: `#6366F1`
itself computes to about 239°, so an implementation should start the band near 235°. `static`: the stock hex values
and `indigo-`/`violet-`/`purple-` utility classes on actions.

**Fix.** Derive the primary from the brand: the logo, the product's existing CSS, the brand guide.
Set it once as a token and reference it everywhere. Do not jump to another famous product's
colour instead; copying Linear's `#5E6AD2` or Atlassian's `#0052CC` is the same default with a
different name ([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)).

```css
/* Before */
.btn-primary { background: #6366f1; }   /* indigo-500 */
.link        { color: #8b5cf6; }        /* violet-500 */

/* After: the value is set once in tokens.css, from the brand guide */
.btn-primary { background: var(--brand); color: var(--brand-ink); }
.link        { color: var(--brand); }
```

**Not a tell when.** The brand really is violet. Declare it in `.slop.json` and the check stands
down. A violet used as one categorical series in a chart is not a primary.

<a id="accent-sprawl"></a>
### C4 · Accent sprawl

`color` · scope `both` · weight 0 · detect `dom`

**Looks like.** Five or more saturated hues used as solid fills. Each card, icon tile or nav
section gets its own colour; category tags in pink, amber and teal; the brand colour also on
headings, borders and icons.

**Why it reads as generated.** "AI loves using colours that mean nothing"
([godobject](https://godobject.dev/blog/10-tells-of-slop/)), often "five or six competing for
attention" ([Fountain](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)). Measured
product screens put "Colour on data, not chrome", at a median of about 3% coloured pixels
([taste-skill](https://github.com/Leonxlnx/taste-skill)). Fluent: "Don't use semantic colors for
decoration" ([Fluent 2](https://fluent2.microsoft.design/color)); HIG: "Avoid using the same color
to mean different things" ([HIG](https://developer.apple.com/design/human-interface-guidelines/color)).

**Detect.** `dom`: count distinct saturated hues used as `background-color` on non-data elements;
flag above four. Also flag the brand hue covering more than about 5% of the viewport, the gate
Hallmark uses ([Hallmark](https://github.com/Nutlope/hallmark)).

**Fix.** A neutral interface, one brand colour for action and selection, and semantic colours only
for state (danger, warning, success, info). Category is metadata: it gets a neutral tag, not a hue.

```jsx
// Before
<span className="bg-pink-100 text-pink-700">Finance</span>
<span className="bg-amber-100 text-amber-700">Legal</span>
<span className="bg-teal-100 text-teal-700">HR</span>

// After
<span className="tag">Finance</span>
<span className="tag">Legal</span>
<span className="tag">HR</span>
/* .tag { background: var(--surface-muted); color: var(--ink); } */
```

**Not a tell when.** Colours encode data: chart series, a calendar where users pick each
calendar's colour, labels the *user* colours. Atlassian reserves its accent palette for exactly
that case ([Atlassian](https://atlassian.design/components/lozenge/usage)).

**Put instead.** Contrast from type weight, size and spacing. One colour that means "act here"
reads louder than six that mean nothing.

<a id="neon-on-dark"></a>
### C5 · Neon on near-black

`color` · scope `landing` · weight 0 · detect `dom`

**Looks like.** A page ground of `#0B0B0B` / `#111` / `slate-950`, grey body text, one acid
accent (lime, cyan, vermilion), and that accent glowing: `shadow-[0_0_40px_theme(colors.lime.400)]`.
No light theme.

**Why it reads as generated.** Always-dark appeared on 20.3% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Anthropic lists "a near-black background
with a single bright acid-green or vermilion accent" as one of five generated clusters
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
The unslop-ui playbook adds the important nuance: "Dark mode itself is not the tell", the
unprompted glow is ([unslop-ui](https://claudecodehq.com/playbooks/unslop-ui)).

**Detect.** `dom`: body background luminance below 0.1, no `prefers-color-scheme` or theme switch
that produces a light ground, plus coloured zero-offset `box-shadow` or `text-shadow`.

**Fix.** Choose the theme from the audience and where they use the product, not the category;
Impeccable: pick it "from the use scene" ([Impeccable](https://github.com/pbakaus/impeccable)).
If dark is right, design its surface steps and contrast, and drop the glow.

```css
/* Before */
body { background: #0a0a0a; color: #a3a3a3; }
.cta { background: #a3e635; box-shadow: 0 0 40px rgb(163 230 53 / .5); }

/* After */
body { background: var(--canvas); color: var(--ink); }
.cta { background: var(--brand); color: var(--brand-ink); }
:root[data-theme="dark"] { --canvas: var(--neutral-950); --ink: var(--neutral-50); }
```

**Not a tell when.** Tools people use in dark rooms or beside media (video editors, terminals,
trading screens), where dark is the expected default and the palette was designed for it.

<a id="stock-palette"></a>
### C6 · Untouched framework palette

`color` · scope `both` · weight 1 · detect `static`

**Looks like.** Stock Tailwind swatches used directly as the brand (`indigo-500`, `violet-600`,
`slate-900`), shadcn's `:root` variables left at the values the CLI wrote, no custom colours in the
theme, hex values typed inline.

**Why it reads as generated.** Default shadcn appeared on 23.5% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). NN/g notes that AI prototyping tools
"default to component libraries like Shadcn and front-end frameworks like Tailwind CSS"
([NN/g](https://www.nngroup.com/articles/ai-prototyping/)). Hallmark gates stock swatches by name
([Hallmark](https://github.com/Nutlope/hallmark)); DeSlop calls it "Stock framework palette as
brand" ([DeSlop](https://github.com/nate-zz/DeSlop)); Kata flags "A hex typed inline because the
model did not know you had a variable for it" ([Kata](https://kata.leafphp.dev/checker)).

**Detect.** `static`: stock colour utilities on components, a theme file without custom colours,
shadcn variables matching the generated file, inline hex in `style=` or CSS outside the token file.

**Fix.** Define project tokens on `:root` once and make every component read them. In Tailwind,
map theme colours to the variables so utilities stay available.

```html
<!-- Before -->
<button class="bg-indigo-600 hover:bg-indigo-700 text-white">Save</button>

<!-- After. tokens.css:
  :root { --brand: /* from the brand guide */; --brand-hover: /* … */; --brand-ink: /* … */; }
  Tailwind theme: colors: { brand: 'var(--brand)', 'brand-hover': 'var(--brand-hover)', 'brand-ink': 'var(--brand-ink)' } -->
<button class="bg-brand hover:bg-brand-hover text-brand-ink">Save</button>
```

**Not a tell when.** A team has deliberately adopted the framework ramp *behind* its own tokens
(`--muted-ink: theme(colors.slate.600)`). The tell is the absence of a decision, not the hex.

<a id="editorial-wave"></a>
### C7 · Cream + serif + terracotta

`color` · scope `landing` · weight 2 · detect `dom`

**Looks like.** A warm cream ground (`#F4F1EA`, `#FAF8F5`, `bg-stone-50`), a high-contrast or soft
serif display face (Instrument Serif, Fraunces), and a terracotta or clay accent near `#D97757`;
sometimes sage or forest green instead of clay. Often with hairline rules and zero radius.

**Why it reads as generated.** It is the published *fix* for purple, adopted wholesale. Anthropic
now lists it first among its clusters
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md));
taste-skill dates it as the third wave, "the 'fix' that became the look"
([taste-skill](https://github.com/Leonxlnx/taste-skill)); unslop-ui says "Any two of the three is a
strong signal" ([unslop-ui](https://claudecodehq.com/playbooks/unslop-ui)).

**Detect.** `dom`: page ground with high lightness, low chroma and a warm hue; a serif display
family on H1/H2; an accent hue in the clay/terracotta or sage band. Two of three raises the flag.

**Fix.** Do not swap one default for another. Write down who the product is for and what it must
feel like, derive palette and type from that, and record the reasoning next to the tokens. If the
derivation still lands on cream and clay, say why in `.slop.json`.

```css
/* Before */
body { background: #f4f1ea; }
h1   { font-family: "Instrument Serif", serif; font-style: italic; }
.btn { background: #d97757; }

/* After: same structure; the values behind the tokens come from the brief */
body { background: var(--canvas); }
h1   { font-family: var(--font-display); }
.btn { background: var(--brand); color: var(--brand-ink); }
```

**Not a tell when.** The brand already owns these colours (a bakery, a publisher, an existing
identity with years of use). Declare it.

---

## Typography

<a id="default-font-only"></a>
### T1 · Default face as the whole identity

`type` · scope `both` · weight 3 · detect `dom`

**Looks like.** Inter, Roboto, Arial, Open Sans or `system-ui` as the only family on the page,
for the H1 and the table cell alike, at two or three weights.

**Why it reads as generated.** Anthropic's 2025 prompt named "Overused font families (Inter,
Roboto, Arial, system fonts)" and predicted output that "will almost always conform to Inter
fonts" ([Anthropic](https://claude.com/blog/improving-frontend-design-through-skills)). Its current
skill no longer bans faces, only asks that the choice be made deliberately
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
DeSlop puts the line well: Inter is fine as a text face; "Inter-as-the-entire-identity is a
default" ([DeSlop](https://github.com/nate-zz/DeSlop)).

**Detect.** `dom`: the set of computed `font-family` values over visible text has one member, and
it starts with a default face or a system stack.

**Fix.** Keep a neutral body face if it serves reading; give headings a face chosen for the
brand. In a product UI a single face is often right, so show the decision through a clear scale
and weight contrast instead. Uncodixfy and Hallmark both accept a default face "unless the
product already uses them" ([Uncodixfy](https://github.com/cyxzdev/Uncodixfy)).

```css
/* Before */
body, h1, h2, h3 { font-family: Inter, system-ui, sans-serif; }

/* After */
:root {
  --font-body: Inter, system-ui, sans-serif;
  --font-display: /* chosen for this brand */, var(--font-body);
}
body   { font-family: var(--font-body); }
h1, h2 { font-family: var(--font-display); }
```

**Not a tell when.** The organisation mandates the face, or the product deliberately follows the
platform (an internal tool that matches the OS). Declare the intended default in `.slop.json`.

<a id="trend-display-font"></a>
### T2 · Trend display face

`type` · scope `landing` · weight 2 · detect `dom`

**Looks like.** The second-wave safe picks as the display face: Space Grotesk, Instrument Serif,
Fraunces, Syne, Geist, Playfair Display. Often paired with an Inter body.

**Why it reads as generated.** These faces appeared on 15.8% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Anthropic itself notes the model "still
tend[s] to converge on common choices (Space Grotesk, for example)"
([Anthropic cookbook](https://platform.claude.com/cookbook/coding-prompting-for-frontend-aesthetics)).
taste-skill refuses to name replacements at all, because "every published 'use this instead' list
has become the next wave of tells within months" ([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `dom`: computed `font-family` of H1/H2 starts with one of the listed faces.

**Fix.** Choose the face from the brand and the reader: what the product sounds like, what the
type has to survive (long names, numbers, small sizes). State the choice and the reason before
writing CSS, as Anthropic's prompt asks. Do not pick from a "safe alternatives" list, this one
included.

```css
/* Before */
h1 { font-family: "Space Grotesk", sans-serif; }

/* After: the choice and its reason live next to the token */
:root { --font-display: /* chosen face */, var(--font-body); } /* why: … */
h1 { font-family: var(--font-display); }
```

**Not a tell when.** The face is part of an established identity, or the product's name and voice
genuinely call for it. Weight 1 exists because many good sites use these faces.

<a id="eyebrow-caps"></a>
### T3 · Eyebrows and tracked caps

`type` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** A tiny uppercase, letter-spaced label above each heading ("FEATURES",
"HOW IT WORKS"), and the same treatment on nav links, section titles and table heads:
`text-xs font-semibold uppercase tracking-widest text-indigo-600`.

**Why it reads as generated.** Krebs measured it on 10.5% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). It is the most uniformly condemned tell:
Impeccable calls it "a ban, not a default" ([Impeccable](https://github.com/pbakaus/impeccable));
Hallmark says labels, captions, table heads and nav links are "the body face… sentence or lower
case, untracked" ([Hallmark](https://github.com/Nutlope/hallmark)); Fluent says avoid all caps
([Fluent 2](https://fluent2.microsoft.design/typography)). taste-skill reports tiny tracked
capitals on 16% of top-rated sites against 57% of weak ones
([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `dom`: `text-transform: uppercase` with `letter-spacing` above about 0.05em on short
labels, especially directly before a heading. `static`: `uppercase` with `tracking-wide*`.

**Fix.** Delete the eyebrow; the heading already names the section. If the label carried a fact
the heading lacks, fold it into the heading. Table heads and nav become sentence case, untracked,
in the body face.

```jsx
// Before
<p className="text-xs font-semibold uppercase tracking-widest text-indigo-600">Features</p>
<h2 className="text-3xl font-bold">Everything you need to run the meeting</h2>

// After
<h2 className="section-title">Agenda, papers and votes in one meeting room</h2>
```

```css
th { text-transform: none; letter-spacing: 0; font: 600 var(--text-sm)/1.4 var(--font-body); color: var(--muted-ink); }
```

**Not a tell when.** Real acronyms and codes (`PDF`, `ISO 27001`), keyboard keys, a wordmark set
in caps by the brand.

<a id="accent-word"></a>
### T4 · One accented word in a headline

`type` · scope `landing` · weight 2 · detect `dom`

**Looks like.** One word of the H1 set in italic serif, a brand colour or a gradient:
`Meetings that <em class="font-serif italic text-indigo-500">actually</em> end`.

**Why it reads as generated.** Anthropic lists "Accenting just a single word or phrase in a
headline" among typography tells
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md));
Hallmark: "Never one italic word inside a roman heading" ([Hallmark](https://github.com/Nutlope/hallmark)).
Krebs tracked the serif italic accent inside an Inter hero as its own pattern
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)).

**Detect.** `dom`: a heading with a child whose computed `font-family`, `font-style` or `color`
differs from the heading's.

**Fix.** One style per heading. Rewrite the sentence so the important word carries weight by
meaning and position. If the headline needs styling to work, the headline is the problem.

```html
<!-- Before -->
<h1>Meetings that <em class="accent">actually</em> end</h1>

<!-- After -->
<h1>Every meeting ends with signed minutes</h1>
```

**Not a tell when.** Typographic convention requires it: a book or film title in italics, inline
code in a documentation heading.

<a id="mono-costume"></a>
### T5 · Monospace as costume

`type` · scope `both` · weight 0 · detect `dom`

**Looks like.** JetBrains Mono, IBM Plex Mono or `font-mono` on captions, meta lines, stat labels
and nav, often tiny and uppercase, sometimes with `//` comment-style prefixes.

**Why it reads as generated.** Impeccable: "Monospace as a costume for 'technical' rather than for
code, data, or measurement" ([Impeccable](https://github.com/pbakaus/impeccable)). Anthropic lists
"a monospace face for small data labels" in its template-chrome cluster
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md));
godobject points at mono `//` labels on anything "techy"
([godobject](https://godobject.dev/blog/10-tells-of-slop/)).

**Detect.** `dom`: a monospace computed family on elements that are not `code`, `pre`, `kbd`,
`samp`, and whose text is not an identifier, hash or aligned figure.

**Fix.** The body face for labels and meta. Align numbers with `tabular-nums`, which DeSlop
flags as the missed step ("Data set in a proportional font", [DeSlop](https://github.com/nate-zz/DeSlop)).
Keep mono for code and identifiers.

```css
/* Before */
.meta, .caption, .stat-label { font-family: "JetBrains Mono", monospace; font-size: 11px; text-transform: uppercase; }

/* After */
.meta, .caption { font: var(--text-sm)/1.4 var(--font-body); color: var(--muted-ink); }
.num            { font-variant-numeric: tabular-nums; }
code, .id       { font-family: var(--font-mono); }
```

**Not a tell when.** Code, commit hashes, IDs, keys, file paths, terminal output. Cloudscape
recommends monospace for IDs and numbers
([Cloudscape](https://cloudscape.design/foundation/visual-foundation/typography/)). taste-skill also
observed mono labels *more* often on award-winning sites, which is why the weight is 1.

<a id="flat-type-scale"></a>
### T6 · Many near-equal sizes

`type` · scope `both` · weight 0 · detect `dom`

**Looks like.** Nine or more computed font sizes on one page, a pixel or two apart: 13, 14, 15,
16, 17, 18, 20. Hierarchy is attempted with size alone and nothing reads as clearly bigger.

**Why it reads as generated.** taste-skill names "Nine nearly equal font sizes" and prescribes
"Five sizes, far apart" ([taste-skill](https://github.com/Leonxlnx/taste-skill)); DeSlop flags
heading sizes "that don't form a ratio" ([DeSlop](https://github.com/nate-zz/DeSlop)). Mature
systems run a short scale: Cloudscape uses 12/16 small, 14/20 body, 24/30 h1
([Cloudscape](https://cloudscape.design/foundation/visual-foundation/typography/)).

**Detect.** `dom`: count distinct computed `font-size` values on visible text; flag above about
eight, or several pairs within 2px of each other.

**Fix.** A scale of about five steps, far apart, as tokens. Use weight and colour (ink vs muted
ink) for the in-between distinctions.

```css
/* Before: 11, 12, 13, 14, 15, 16, 17, 18, 20px scattered across components */

/* After */
:root { --text-xs: 12px; --text-sm: 14px; --text-md: 16px; --text-lg: 20px; --text-xl: 28px; }
.caption { font-size: var(--text-xs); color: var(--muted-ink); }
body     { font-size: var(--text-sm); }
h1       { font-size: var(--text-xl); }
```

**Not a tell when.** Long-form editorial pages with a fluid scale (`clamp()`) can produce many
computed values from a few deliberate steps. Check the source, not only the render.

<a id="tiny-text"></a>
### T7 · Text under 12px

`type` · scope `both` · weight 0 · detect `dom`

**Looks like.** 10–11px captions, badge text, table meta and footnotes (`text-[10px]`,
`text-[11px]`), usually to squeeze a long string into a small box.

**Why it reads as generated.** Cloudscape: "Don't use font size smaller than 12px"
([Cloudscape](https://cloudscape.design/foundation/visual-foundation/typography/)); Carbon's
smallest label is 12/16 ([Carbon](https://carbondesignsystem.com/elements/typography/type-sets/));
Hallmark gates text under 12px ([Hallmark](https://github.com/Nutlope/hallmark)).

**Detect.** `dom`: visible text nodes with computed `font-size` below 12px.

**Fix.** Raise to the 12px floor and shorten the text instead of shrinking it. Move secondary
facts (full timestamp, author, ID) into a tooltip or a detail view.

```css
/* Before */
.badge      { font-size: 10px; }
.row-meta   { font-size: 11px; }

/* After */
.badge, .row-meta { font-size: var(--text-xs); } /* 12px */
```

**Not a tell when.** Superscript footnote markers, or labels inside a chart image that scales with
its container.

<a id="stranded-word"></a>
### T8 · Stranded last word

`type` · scope `landing` · weight 0 · detect `dom`

**Looks like.** A two- or three-line heading whose last line holds one word.

**Why it reads as generated.** Browsers fill lines greedily, and generated pages rarely override
it: "Lines filled greedily until one word is stranded" ([Kata](https://kata.leafphp.dev/checker)).
Hallmark flags display heads without `text-wrap: balance` ([Hallmark](https://github.com/Nutlope/hallmark)).

**Detect.** `dom`: headings that wrap, without `text-wrap: balance`, whose last line box holds a
single word at common widths (375, 1280).

**Fix.** Balance headings and prettify paragraphs. If a word still strands at some width, rewrite
the heading shorter; do not insert `<br>` or `&nbsp;` chains that break at other widths.

```css
h1, h2, h3 { text-wrap: balance; }
p          { text-wrap: pretty; }
```

**Not a tell when.** A single-word heading, or a deliberate typographic layout with set breaks
that hold at every breakpoint.

---

## Shape, depth and effects

<a id="one-radius"></a>
### S1 · One big radius on everything

`surface` · scope `both` · weight 3 · detect `dom`

**Looks like.** `rounded-2xl` or `rounded-3xl` (16–24px) on cards, inputs, buttons, badges, images
and dialogs alike; an inner box with the same radius as the box it sits in.

**Why it reads as generated.** "Cards, buttons, inputs, badges, and images all rounded to the same
exaggerated radius" ([noqta](https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026));
Anthropic: "one border-radius on everything regardless of hierarchy"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
Product systems keep control radii small: 4px for Atlassian labels and Fluent buttons, 6px for
Primer buttons ([Atlassian](https://atlassian.design/foundations/radius),
[Primer](https://primer.style/product/components/button/)).

**Detect.** `dom`: histogram of `border-radius` over boxed elements; flag when one value of 12px
or more covers most of them, or when nested boxes share the outer radius.

**Fix.** Radius per nesting level: controls and chips share one small radius, containers a larger
one, overlays the largest. A box inside a padded box takes the outer radius minus the padding, so
the curves stay concentric. Hallmark checks for exactly this, "one radius per nesting level"
([Hallmark](https://github.com/Nutlope/hallmark)).

```css
/* Before */
.card, .input, .btn, .badge, .dialog, img { border-radius: 16px; }

/* After */
.btn, .input, .chip { border-radius: var(--radius-sm); }   /* e.g. 4–6px  */
.card               { border-radius: var(--radius-md); }   /* e.g. 8px    */
.dialog             { border-radius: var(--radius-lg); }   /* e.g. 12px   */
.card > .inset      { border-radius: calc(var(--radius-md) - 4px); }
```

Tailwind: `rounded-2xl` on every component → `rounded-[var(--radius-sm)]` on controls, `-md` on cards.

**Not a tell when.** A brand built on generous curves that still steps by level, or avatars and
pill controls, which are round by definition.

<a id="shape-mix"></a>
### S2 · Mixed chip shapes and heights

`surface` · scope `both` · weight 0 · detect `dom`

**Looks like.** A pill "Draft" next to a 4px-square "Approved"; status chips at 20, 24 and 28px
in one table; pill buttons sitting in a dense admin toolbar.

**Why it reads as generated.** Each component was generated in isolation, so each brought its own
recipe. Fluent: "Avoid mixing badge sizes in the same context"
([Fluent 2](https://fluent2.microsoft.design/components/web/react/core/badge/usage)). no-slop-ui
asks for "no pills everywhere" and buttons at a 6–10px radius
([no-slop-ui](https://github.com/LeoStehlik/no-slop-ui)).

**Detect.** `dom`: elements with the same role (chips in one column, buttons in one toolbar) whose
computed `height` or `border-radius` differ.

**Fix.** One recipe per kind of label, as a component. Pill or rounded matters less than
consistency: Carbon tags and Primer labels are pills
([Carbon](https://carbondesignsystem.com/components/tag/style/),
[Primer](https://github.com/primer/react/blob/main/packages/react/src/Label/Label.module.css)),
Atlassian's are 4px.

```jsx
// Before
<span className="rounded-full px-3 py-1 text-xs">Draft</span>
<span className="rounded-md px-2 py-0.5 text-[11px]">Approved</span>

// After
<Chip>Draft</Chip>
<Chip>Approved</Chip>
/* .chip { height: 24px; padding: 0 8px; border-radius: var(--radius-sm); font-size: var(--text-xs); } */
```

**Not a tell when.** Two kinds of label are meant to look different (a removable filter token vs
a read-only status), and each kind is consistent across the product.

<a id="side-stripe"></a>
### S3 · Coloured side stripe

`surface` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** A 3–4px coloured border on one edge of a rounded card, alert, callout or list
item, the other sides hairline or none: `border-l-4 border-indigo-500 rounded-xl`.

**Why it reads as generated.** A designer quoted by Krebs called it "almost as reliable a sign…
as em-dashes for text" ([Krebs](https://www.adriankrebs.ch/blog/design-slop/), 13% of sites).
Shukla's skill calls it "the single most reliable AI tell"
([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)); Impeccable refuses any coloured
`border-left` or `border-right` above 1px on cards, list items, callouts or alerts
([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `dom`: one side's `border-*-width` of 3px or more in a non-neutral colour while the
other sides are 0–1px, often with a radius. `static`: `border-l-[2-8]`, `border-left: [3-8]px solid`.

**Fix.** Put the colour on the element that carries the meaning: an icon, a status label, the
title. Keep the box's border uniform and neutral.

```css
/* Before */
.alert-warning { border-left: 4px solid #f59e0b; border-radius: 12px; }

/* After */
.alert            { display: flex; gap: 8px; border: 1px solid var(--border); border-radius: var(--radius-md); }
.alert-warning .alert-icon { color: var(--warning); }
```

**Not a tell when.** The edge marks a current position that changes: the active item in a
navigation rail, the selected row, the changed line in a diff, a blockquote rule. antislop's test
is the right one: the stripe "must carry information or go"
([antislop](https://github.com/miqdadbadjuber/anti-slop)).

<a id="shadow-everything"></a>
### S4 · Soft shadow under every card

`surface` · scope `both` · weight 2 · detect `dom`

**Looks like.** Every resting card and panel lifted by the same soft grey shadow, typically
Tailwind's `shadow-sm`/`shadow-md` or `0 4px 6px rgb(0 0 0 / .1)`, doing the job a border or
whitespace should do.

**Why it reads as generated.** Anthropic's SaaS-card cluster: "the same soft grey shadow
(rgba(0,0,0,.1)) under each"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md));
"Subtle shadows (exactly 0.1 opacity)" ([prg.sh](https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website)).
Atlassian reserves raised elevation for "cards that can be moved" and says not to use it "to group
content when a border or white space would suffice" ([Atlassian](https://atlassian.design/foundations/elevation)).
In taste-skill's measured product screens, "17 of 23 give their cards no shadow"
([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `dom`: share of resting block elements (not overlays, not `position: fixed`) with a
non-`none` `box-shadow`; flag when most cards carry one.

**Fix.** Group with a border, a surface tone step or whitespace. Shadows belong to things above
the page: menus, popovers, dialogs, toasts, and an item being dragged. Carbon separates layers by
alternating surfaces, not shadows ([Carbon](https://carbondesignsystem.com/elements/color/overview/)).

```css
/* Before */
.card { box-shadow: 0 4px 6px -1px rgb(0 0 0 / .1), 0 2px 4px -2px rgb(0 0 0 / .1); }

/* After */
.card                    { border: 1px solid var(--border); box-shadow: none; }
.menu, .popover, .dialog { box-shadow: var(--shadow-overlay); }
```

Tailwind: `rounded-xl shadow-md` on every card → `border border-[var(--border)]`.

**Not a tell when.** Overlays, draggable cards on a board, a sticky header that gains a shadow
only once content scrolls under it.

<a id="elevation-soup"></a>
### S5 · Border + shadow on one resting box

`surface` · scope `both` · weight 2 · detect `dom`

**Looks like.** A resting card with a hairline border, a wide diffuse shadow and sometimes a ring:
`border shadow-lg ring-1 ring-black/5`.

**Why it reads as generated.** It stacks every separation device instead of choosing one. DeSlop:
"Elevation soup… border + shadow + ring on the same element" ([DeSlop](https://github.com/nate-zz/DeSlop));
Shukla: "hairline 1px border and a wide diffuse shadow on the same card"
([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)).

**Detect.** `dom`: a resting, in-flow element with a visible border and a non-`none` box-shadow
(or outline) at once.

**Fix.** Pick one per level: border for in-page grouping, elevation for overlays. A system needs
two or three elevation steps, not one per component.

```css
/* Before */
.card { border: 1px solid #e5e7eb; box-shadow: 0 10px 30px rgb(0 0 0 / .08); outline: 1px solid rgb(0 0 0 / .05); }

/* After */
.card { border: 1px solid var(--border); }
```

**Not a tell when.** Overlays. A popover or menu often needs a 1px stroke as well as its shadow to
separate from a surface of the same colour, and that is the overlay recipe, not soup.

<a id="glow"></a>
### S6 · Glow and dramatic shadow

`surface` · scope `both` · weight 1 · detect `dom` `static`

**Looks like.** Coloured, blurred halos on buttons and cards (`shadow-lg shadow-purple-500/50`,
`0 0 24px rgb(139 92 246 / .6)`), glowing headline `text-shadow`, drop shadows of 40px and more
on resting content.

**Why it reads as generated.** Krebs measured vibrant shadows on 4.3% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)); a glowing "Get started" button is
first-wave and banned in taste-skill ([taste-skill](https://github.com/Leonxlnx/taste-skill)).
Impeccable states the rule: shadows carry an offset and a soft blur, and "A zero-offset colored
halo is decoration" ([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `dom`: `box-shadow` or `text-shadow` whose colour is saturated rather than black or
grey, or whose blur exceeds about 32px on in-flow content. `static`: `shadow-{color}-`,
`drop-shadow-` with colour, `0 0 \d+px` with a non-grey colour.

**Fix.** No coloured shadows. Buttons rest flat (Material's filled button sits at elevation level
0); hover changes the fill; overlays use the system's shadow step.

```css
/* Before */
.btn-primary { box-shadow: 0 0 24px rgb(139 92 246 / .6); }
h1           { text-shadow: 0 0 20px #a78bfa; }

/* After */
.btn-primary       { box-shadow: none; }
.btn-primary:hover { background: var(--brand-hover); }
h1                 { text-shadow: none; }
```

**Not a tell when.** A focus ring drawn with a spread-only shadow (`0 0 0 2px var(--focus-ring)`,
no blur); a subtle dark `text-shadow` that keeps text legible over a photograph.

<a id="glass"></a>
### S7 · Glassmorphism

`surface` · scope `both` · weight 1 · detect `dom` `static`

**Looks like.** Translucent panels with backdrop blur as the default card, nav or modal style:
`backdrop-blur-xl bg-white/10 border border-white/20`.

**Why it reads as generated.** Glass panels appeared on 17.1% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)); "It just loves glassmorphism"
([godobject](https://godobject.dev/blog/10-tells-of-slop/)). Apple, whose platforms made the
material famous, limits it to the controls layer: "Don't use Liquid Glass in the content layer"
([HIG](https://developer.apple.com/design/human-interface-guidelines/materials)). The unslop-ui
data does not support glass as a reliable tell ([unslop-ui](https://claudecodehq.com/playbooks/unslop-ui)),
hence weight 2, not 3.

**Detect.** `dom`: computed `backdrop-filter` containing `blur` on cards and panels in the content
flow. `static`: `backdrop-blur`, `backdrop-filter: blur`.

**Fix.** Opaque surfaces one tone step from the ground. Keep blur only for a control that floats
over moving content, where it keeps the content's context visible.

```css
/* Before */
.card { background: rgb(255 255 255 / .1); backdrop-filter: blur(16px); border: 1px solid rgb(255 255 255 / .2); }

/* After */
.card { background: var(--surface); border: 1px solid var(--border); }
/* Legitimate: a sticky header over scrolling content */
.app-header.is-scrolled { background: color-mix(in srgb, var(--surface) 85%, transparent); backdrop-filter: blur(8px); }
```

**Not a tell when.** A toolbar floating over a map, video or canvas; a sticky header over a
scrolling list; the platform's own material on Apple targets.

<a id="nested-cards"></a>
### S8 · Cards inside cards

`surface` · scope `both` · weight 0 · detect `dom`

**Looks like.** A bordered or shadowed box inside another, sometimes three deep: a Settings card
holding section cards holding field cards.

**Why it reads as generated.** Generators wrap every group in the same `Card` component. Fountain
calls it "a filing cabinet inside a filing cabinet"
([Fountain](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)); Impeccable: "nested
cards are always wrong" ([Impeccable](https://github.com/pbakaus/impeccable)); Hallmark bans "a
card inside a card" ([Hallmark](https://github.com/Nutlope/hallmark)).

**Detect.** `dom`: an element with a visible border or shadow whose ancestor (within the same
section) also has one; flag at two levels, weight up at three.

**Fix.** One container level. Inside it, structure comes from headings, spacing and dividers.

```jsx
// Before
<Card><CardHeader>Settings</CardHeader><CardContent>
  <Card><CardContent><Card>Email notifications</Card></CardContent></Card>
</CardContent></Card>

// After
<section className="panel">
  <h2>Settings</h2>
  <div className="field-group">Email notifications …</div>
  <hr className="divider" />
  <div className="field-group">Security …</div>
</section>
```

**Not a tell when.** A board column holding draggable cards, a code block or media item inside a
panel, a selected option tile inside a form. The inner box there is an object, not a grouping.

<a id="blob-background"></a>
### S9 · Aurora/blob background

`surface` · scope `landing` · weight 1 · detect `dom` `static`

**Looks like.** Two to four blurred colour circles parked behind the hero
(`absolute h-96 w-96 rounded-full bg-purple-500 opacity-30 blur-3xl`), mesh or aurora gradients, a
dotted or lined grid with a glow at its centre.

**Why it reads as generated.** "radial light bloom sitting behind the hero section"
([Fountain](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)); DeSlop's
"Multi-hue pastel blob backgrounds" ([DeSlop](https://github.com/nate-zz/DeSlop)); Hallmark gates
aurora and mesh blobs ([Hallmark](https://github.com/Nutlope/hallmark)). unslop-ui reports that its
data does not support blobs as a *reliable* tell on their own
([unslop-ui](https://claudecodehq.com/playbooks/unslop-ui)); read this one in combination.

**Detect.** `dom`: absolutely positioned decorative elements with `filter: blur()` of 40px or more,
or stacked radial gradients behind the first section. `static`: `blur-3xl` on `rounded-full`
divs, `radial-gradient` stacks in the hero.

**Fix.** A plain ground. If the hero needs something to look at, show the product.

```html
<!-- Before -->
<div class="absolute -top-40 -left-40 h-96 w-96 rounded-full bg-purple-500 opacity-30 blur-3xl"></div>
<div class="absolute top-20 right-0 h-96 w-96 rounded-full bg-cyan-400 opacity-30 blur-3xl"></div>

<!-- After -->
<section class="hero"> <!-- .hero { background: var(--canvas); } -->
  <h1>…</h1>
  <img src="/screens/meeting-detail.png" alt="A meeting with its agenda, papers and vote results" />
</section>
```

**Not a tell when.** The imagery is the brand's own art direction, or a visualisation of real data
(a map, a signal, a heat field).

**Put instead.** A real screenshot of the main screen, a short product recording, a photograph
from the customer's world, or confident type on a plain ground.

---

## Layout and components

<a id="kpi-tiles"></a>
### L1 · KPI tile row

`layout` · scope `app` · weight 2 · detect `dom`

**Looks like.** Three to six identical cards across the top of a dashboard, each with an icon in a
tinted circle, a big (often brand-coloured) number, a caption that repeats the label, and a green
"+12% ↑" chip; sometimes a sparkline.

**Why it reads as generated.** taste-skill's description of the generated dashboard is exact:
"four KPI cards each with an icon in a tinted circle, a green '+12.5%' and a sparkline"; its
measured product screens follow the rule "KPIs are type, not tiles"
([taste-skill](https://github.com/Leonxlnx/taste-skill)). no-slop-ui bans "Fake metric grids that
fill space but do not support the task" ([no-slop-ui](https://github.com/LeoStehlik/no-slop-ui));
Uncodixfy: "No metric-card grid as the first instinct" ([Uncodixfy](https://github.com/cyxzdev/Uncodixfy)).

**Detect.** `dom`: 3–6 sibling boxes above the fold with the same DOM shape (icon wrapper, large
number, short caption, optional percentage chip).

**Fix.** Show numbers as type, where they are used: as counts on the tabs or filters that open the
list, or in a compact row in the table header. Drop the icon tiles, the captions that repeat the
label, and any delta without a stated period and baseline.

```jsx
// Before
<div className="grid grid-cols-4 gap-6">
  <Card><div className="rounded-full bg-indigo-100 p-3"><Users /></div>
    <p className="text-3xl font-bold text-indigo-600">1,284</p><p>Total users</p>
    <span className="text-green-600">+12%</span></Card>
  {/* …three more */}
</div>

// After: the counts filter the list below them
<nav className="tabs" aria-label="Meetings by status">
  <a aria-current="page">Needs my review <span className="count">4</span></a>
  <a>Scheduled <span className="count">12</span></a>
  <a>Closed <span className="count">96</span></a>
</nav>
<MeetingTable filter="needs-review" />
```

**Not a tell when.** The screen *is* a monitoring view and each number drives a decision, with its
period and threshold visible. Even then, numbers rarely need tiles.

**Put instead.** The user's queue: what needs their action, sorted by urgency. A count belongs next
to the thing it counts.

<a id="icon-tile"></a>
### L2 · Icon in a tinted tile

`layout` · scope `both` · weight 1 · detect `dom`

**Looks like.** A glyph centred in a coloured square or circle (`grid h-10 w-10 place-items-center
rounded-xl bg-indigo-100 text-indigo-600`) beside a heading, a card title, a list row or a feature.

**Why it reads as generated.** "Three identical feature cards, each with an icon in a rounded
square" is first-wave and banned in taste-skill ([taste-skill](https://github.com/Leonxlnx/taste-skill));
DeSlop: "Icon-in-tinted-box… Instant template" ([DeSlop](https://github.com/nate-zz/DeSlop)).
Cloudscape: "Don't use icons with page, section, or sub section headlines"
([Cloudscape](https://cloudscape.design/foundation/visual-foundation/iconography/)); Carbon colours an
icon like its adjacent text ([Carbon](https://carbondesignsystem.com/elements/icons/usage/)).

**Detect.** `dom`: an `svg` (or icon font glyph) whose parent has a tinted background, a radius and
roughly square dimensions, repeated across siblings.

**Fix.** The glyph alone at 16px, coloured like its text, where it helps recognition (row type,
action). No icon on section headings. Remove it entirely when it only illustrates the word next to it.

```jsx
// Before
<div className="flex items-center gap-3">
  <div className="grid h-10 w-10 place-items-center rounded-xl bg-indigo-100 text-indigo-600"><Calendar /></div>
  <h3>Schedule</h3>
</div>

// After
<h3>Schedule</h3>
<li className="row"><Icon name="calendar" className="row-icon" /> Board meeting, 24 Oct</li>
/* .row-icon { width: 16px; height: 16px; color: currentColor; } */
```

**Not a tell when.** App or file-type icons, avatars, integration logos where the logo is the
content.

**Put instead.** Nothing, usually. Where a feature needs a visual, show the feature: a cropped
screenshot of the actual control beats a generic glyph.

<a id="icon-soup"></a>
### L3 · Icon soup chip

`layout` · scope `both` · weight 1 · detect `dom`

**Looks like.** One small label carrying an icon, words, a tinted fill and a coloured border at
once: `inline-flex gap-1 rounded-full border border-emerald-200 bg-emerald-50 text-emerald-700`
with a `CheckCircle` inside.

**Why it reads as generated.** It applies every signal to say one thing. Cloudscape: "Don't include
icons or imagery on badges" ([Cloudscape](https://cloudscape.design/components/badge/)) and
"Don't combine multiple icons" ([Cloudscape](https://cloudscape.design/foundation/visual-foundation/iconography/)).
antislop flags the pill-border-dot-uppercase capsule as a self-referential badge
([antislop](https://github.com/miqdadbadjuber/anti-slop)).

**Detect.** `dom`: a short inline element (under ~28px tall) with an `svg` child, text, a tinted
background and a visible border.

**Fix.** A label is words. A status is icon plus words, without a box
([Cloudscape](https://cloudscape.design/components/status-indicator/)). An action that needs to be
small is an icon-only button with a tooltip and an accessible name.

```jsx
// Before
<span className="chip chip--success"><CheckCircle size={12} /> Approved</span>

// After
<span className="chip">Approved</span>                              {/* label */}
<span className="status"><Icon name="check" /> Approved</span>     {/* status: no box */}
<Tooltip label="Export"><button aria-label="Export"><Icon name="download" /></button></Tooltip>
```

**Not a tell when.** A design system defines it as one component and uses it consistently, like
Primer's StateLabel for issue state, which is solid, pill-shaped and always icon plus text
([Primer](https://primer.style/product/components/state-label/)).

<a id="hero-three-cards"></a>
### L4 · Centred hero, then three icon cards

`layout` · scope `landing` · weight 0 · detect `dom`

**Looks like.** A centred headline, a subtitle and two CTAs, followed by a grid of three or six
same-shaped cards, each starting with an icon, then a heading and two lines of text.

**Why it reads as generated.** "Hero section with centered text and a CTA button… Three features
in boxes below, each with an icon" ([prg.sh](https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website)).
Krebs measured a centred title on 23.5% of sites and templated 3- or 6-card grids on 20.1%
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Impeccable refuses "Same-size cards of
icon plus heading plus text as the page structure" ([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `dom`: an H1 with `text-align: center`, followed by a grid of 3 or 6 siblings sharing a
class signature, each starting with an icon wrapper.

**Fix.** Lead with the product itself: a real screenshot, a short demo, or the main job stated as
the headline. Let each section take the shape its argument needs; not every point deserves a card,
and three equal ones say the points are interchangeable.

```jsx
// Before
<section className="text-center"><h1>…</h1><p>…</p><Button>Get started</Button><Button>Learn more</Button></section>
<div className="grid grid-cols-3">{features.map(f => <Card icon={f.icon} title={f.title} body={f.body} />)}</div>

// After
<section className="hero">            {/* left-aligned text, product image beside it */}
  <h1>Run the board meeting without paper</h1>
  <Button variant="primary">Book a 20-minute demo</Button>
  <img src="/screens/agenda.png" alt="Agenda with attached papers and a live vote" />
</section>
<section className="walkthrough">…one feature, one real screen, one paragraph…</section>
```

**Not a tell when.** A centred hero with one strong, specific claim and a real product image, or a
features section where each item genuinely has the same shape (a pricing comparison).

**Put instead.** The product doing its main job, then the two or three proof points that matter,
each shown rather than described.

<a id="hero-pill"></a>
### L5 · Pill badge above the hero

`layout` · scope `landing` · weight 3 · detect `dom`

**Looks like.** A rounded chip directly above the H1, often with a sparkle, a dot or an arrow:
"✨ New: AI summaries →", "Introducing v2", "Backed by …".

**Why it reads as generated.** Krebs found it on 4.7% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)); taste-skill bans it as second-wave
([taste-skill](https://github.com/Leonxlnx/taste-skill)). antislop notes it usually "duplicates the
headline" ([antislop](https://github.com/miqdadbadjuber/anti-slop)). Cloudscape: "Don't use badges
for labeling features as new" ([Cloudscape](https://cloudscape.design/components/badge/)).

**Detect.** `dom`: the element immediately before the H1 has `border-radius` of at least half its
height and short text.

**Fix.** Delete it. Real news goes in the headline or the supporting sentence; ongoing news goes in
a changelog link in the navigation.

```html
<!-- Before -->
<a class="pill">✨ New: AI summaries →</a>
<h1>The meeting platform for modern teams</h1>

<!-- After -->
<h1>Minutes drafted from the recording, ready to sign</h1>
<nav>… <a href="/changelog">Changelog</a></nav>
```

**Not a tell when.** A time-bound notice the visitor must see (an outage, a deprecation date),
styled as a notice, not a pill.

<a id="index-numbers"></a>
### L6 · 01 / 02 / 03 markers

`layout` · scope `landing` · weight 0 · detect `dom`

**Looks like.** Zero-padded numbers on sections, cards or features that are not a sequence:
"01 Secure", "02 Fast", "03 Collaborative", often in mono.

**Why it reads as generated.** Anthropic: numbered markers are "only appropriate if the content
actually is a sequence"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
Krebs counted numbered steps on 9.4% of sites ([Krebs](https://www.adriankrebs.ch/blog/design-slop/)).
taste-skill bans index numbering while noting it appears *more* often on award-winning sites
([taste-skill](https://github.com/Leonxlnx/taste-skill)), which is why the weight is 1.

**Detect.** `dom`: siblings whose first text node matches `^0?\d{1,2}[./ ]` with zero padding,
where the container is not an `<ol>`.

**Fix.** Number only real steps, and mark them up as a list so the order is semantic. Otherwise
drop the markers; order comes from position.

```html
<!-- Before -->
<div class="feature"><span class="index">01</span><h3>Secure</h3></div>

<!-- After: a real sequence -->
<ol class="steps"><li>Upload the papers</li><li>Invite members</li><li>Open the vote</li></ol>
```

**Not a tell when.** Steps a user follows in order, chapters, legal clauses, numbered requirements
someone will refer to by number.

<a id="fake-chrome"></a>
### L7 · Fake window chrome

`layout` · scope `landing` · weight 1 · detect `dom`

**Looks like.** Three red, amber and green dots drawn above a screenshot or a code block that is
not in a macOS window.

**Why it reads as generated.** "Three traffic-light dots around a demo that is not in a browser"
([Kata](https://kata.leafphp.dev/checker)). It is decoration borrowed from one operating system to
signal "this is software". Evidence is thin beyond that source; weight 1.

**Detect.** `dom`: three small sibling circles in red/amber/green hues at the top of a box that
contains an image, `pre` or `code`.

**Fix.** Show the screenshot as it is, with a hairline border and a caption. If context matters
(mobile vs desktop), use an accurate device frame.

```html
<!-- Before -->
<div class="window"><div class="dots"><i></i><i></i><i></i></div><img src="/screens/app.png" alt="" /></div>

<!-- After -->
<figure class="shot">  <!-- .shot img { border: 1px solid var(--border); border-radius: var(--radius-md); } -->
  <img src="/screens/app.png" alt="Meeting list filtered to this week" />
  <figcaption>Meetings this week, filtered by committee</figcaption>
</figure>
```

**Not a tell when.** The product is a desktop app on macOS and the screenshot is real.

<a id="emoji-icons"></a>
### L8 · Emoji and sparkles as icons

`layout` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** 🚀 ⚡ ✨ 🎯 in headings, nav items, bullets, buttons and empty states; the
four-point sparkle (Lucide `sparkles`) on anything "AI".

**Why it reads as generated.** Emoji in the sidebar or nav appeared on 3.8% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)); they show up "as navigation icons, section
headers, bullet point replacements" ([Fountain](https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui)),
"Typed, not drawn" ([Kata](https://kata.leafphp.dev/checker)). NN/g finds that the sparkle's meaning
"differs from interface to interface" ([NN/g](https://www.nngroup.com/articles/ai-sparkles-icon-problem/));
see also [Big Medium](https://bigmedium.com/ideas/your-sparkles-are-fizzling.html).

**Detect.** `dom`: `\p{Extended_Pictographic}` in headings, `nav`, `button`, list markers.
`static`: emoji literals in JSX text, `lucide-sparkles` / `<Sparkles` imports.

**Fix.** One icon set, one size and stroke, used to supplement text, not replace it
([Primer](https://primer.style/octicons/usage-guidelines/)). Name AI features by what they do:
"Draft minutes", not "✨ AI magic".

```jsx
// Before
<h2>🚀 Features</h2>
<button>✨ Summarize with AI</button>

// After
<h2>Features</h2>
<button><Icon name="file-text" /> Draft minutes</button>
```

**Not a tell when.** Emoji as user content (reactions, chat, a user's status), or a product whose
voice is deliberately casual and uses emoji consistently as copy, not as an icon system.

<a id="status-dot"></a>
### L9 · Decorative status dots

`layout` · scope `both` · weight 2 · detect `dom`

**Looks like.** A small coloured dot before every chip, nav item or row, often pulsing
(`h-2 w-2 rounded-full bg-green-500 animate-pulse`), repeating what the label already says:
"● Active", "● Live", "● Operational".

**Why it reads as generated.** "The little pulsing dot with a 'Live' or 'New' pill"
([noqta](https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026)); "IT IS JUST NEVER
INACTIVE" ([godobject](https://godobject.dev/blog/10-tells-of-slop/)); DeSlop calls it "cockpit
cosplay" ([DeSlop](https://github.com/nate-zz/DeSlop)). Carbon asks for at least two of colour,
shape and symbol, and to avoid indicators when no action is needed
([Carbon](https://carbondesignsystem.com/patterns/status-indicator-pattern/)).

**Detect.** `dom`: small round elements (≤10px) or `::before` dots preceding short labels, repeated
across rows; heavier when animated.

**Fix.** Status is an icon or shape plus one or two words
([Cloudscape](https://cloudscape.design/components/status-indicator/)). Show a marker only where
the state changes a decision, and design its non-green states.

```jsx
// Before
<span className="flex items-center gap-2"><span className="h-2 w-2 animate-pulse rounded-full bg-green-500" />Active</span>

// After
<span>Active</span>
<span className="status status--danger"><Icon name="alert-triangle" /> Sync failed</span>
```

**Not a tell when.** Real presence (online, away), an unread marker, a recording or on-air
indicator that reflects actual state and can be off.

<a id="badge-every-row"></a>
### L10 · Same badge on every row

`layout` · scope `app` · weight 1 · detect `dom`

**Looks like.** A table column where every visible row carries the same coloured badge: "Active",
"Active", "Active".

**Why it reads as generated.** Uncodixfy lists "Tables with tag badges for every status"
([Uncodixfy](https://github.com/cyxzdev/Uncodixfy)). Cloudscape: "Avoid using badges, including
severity badges, to indicate status" ([Cloudscape](https://cloudscape.design/components/badge/)).
Colour that never varies carries no information and dulls the row that does differ.

**Detect.** `dom`: a column in which more than about 80% of visible cells contain a badge with the
same text and colour.

**Fix.** Plain text for the normal case; highlight only the exception, with a status indicator.

```jsx
// Before
<td><Badge color="green">Active</Badge></td>

// After
<td>{user.active ? "Active" : <span className="status status--warning"><Icon name="ban" /> Deactivated</span>}</td>
```

**Not a tell when.** The column genuinely varies across the data set and the screen happens to show
a uniform page (a filter for one status). The checker cannot tell; check with unfiltered data.

<a id="meta-strings"></a>
### L11 · Middle-dot meta strings and glued arrows

`layout` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** Facts joined by middle dots ("Board · 14:00 · Room 3 · 12 attendees"), labels
built as "WORD — fragment", and "→" or "↗" appended to links and buttons ("View all →").

**Why it reads as generated.** Anthropic lists "meta strings joined with middle dots", "labels
built as 'WORD — fragment'" and "a '→' appended to link and button text"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
Hallmark calls them "the punctuation of generated pages" ([Hallmark](https://github.com/Nutlope/hallmark));
antislop: when every CTA has an arrow, none points anywhere specific
([antislop](https://github.com/miqdadbadjuber/anti-slop)).

**Detect.** `dom`: text nodes with two or more ` · ` separators; link and button text ending in
`→`, `↗` or `»`. `static`: the same characters in JSX text and string literals.

**Fix.** Separate facts into cells, a second line or a comma list. A button says what it does
without an arrow.

```jsx
// Before
<p className="meta">Board · 14:00 · Room 3 · 12 attendees</p>
<a href="/meetings">View all →</a>

// After
<td>Board</td><td>14:00</td><td>Room 3</td><td className="num">12</td>
<a href="/meetings">All meetings</a>
```

**Not a tell when.** One dot in a byline ("Name · 5 min read") or a dot that marks a live state,
both of which Hallmark allows; breadcrumb separators; an `↗` used consistently to mean "opens
another site".

<a id="dash-empty"></a>
### L12 · Dash for an empty value

`layout` · scope `app` · weight 1 · detect `dom`

**Looks like.** "—", "-" or "N/A" standing for a missing value, especially in detail views and
forms: `Description: —`, `Due date: N/A`.

**Why it reads as generated.** The evidence is mixed, hence weight 1. The dash is a placeholder a
generator inserts without deciding what "missing" means: not set yet, not applicable, hidden, or
failed to load. "N/A" conflates two of those. But a muted dash in a dense table cell is standard:
Cloudscape says to use "-" for empty cells ([Cloudscape](https://cloudscape.design/components/table/)),
and taste-skill's measured product screens print missing values as a grey dash
([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `dom`: elements whose entire text is `—`, `–`, `-` or `N/A`; reported with their
context (table cell vs detail value).

**Fix.** Pick one rule and apply it everywhere. In detail views and forms, say what is missing, in
muted words: "No description", "Not set". In dense tables, a muted dash is fine if it is the only
empty marker the product uses.

```jsx
// Before (detail view)
<dt>Description</dt><dd>—</dd>
<dt>Due date</dt><dd>N/A</dd>

// After
<dt>Description</dt><dd className="muted">No description</dd>
<dt>Due date</dt><dd className="muted">Not set</dd>
/* .muted { color: var(--muted-ink); } */
```

**Not a tell when.** Table cells under a documented empty-cell rule, or a dash that is real data
(a score of "–" in sport, a negative sign).

<a id="competing-primaries"></a>
### L13 · Competing primary buttons

`layout` · scope `both` · weight 0 · detect `dom`

**Looks like.** Several solid, brand-filled buttons visible at once: "Import", "Export" and
"New meeting" all primary in one toolbar; a filled button on every card.

**Why it reads as generated.** Every major system says the same thing. Cloudscape: "there can be
only one" ([Cloudscape](https://cloudscape.design/components/button/)); Carbon: primary "should
only appear once per screen" ([Carbon](https://carbondesignsystem.com/components/button/usage/));
Atlassian: one primary "in a page or area" ([Atlassian](https://atlassian.design/components/button/usage));
Fluent: "Don't give secondary actions the same visual weight"
([Fluent 2](https://fluent2.microsoft.design/components/web/react/core/button/usage)); Primer
likewise ([Primer](https://primer.style/product/components/button/)).

**Detect.** `dom`: more than one visible button with a brand-coloured solid fill in the same page
or region (dialogs and side panels counted separately).

**Fix.** One primary per page or area: the action most users came to take. Everything else is
secondary, subtle or a link; more than three actions go into a menu.

```jsx
// Before
<Button variant="primary">Import</Button>
<Button variant="primary">Export</Button>
<Button variant="primary">New meeting</Button>

// After
<Menu label="More"><MenuItem>Import</MenuItem><MenuItem>Export</MenuItem></Menu>
<Button variant="primary">New meeting</Button>
```

**Not a tell when.** A dialog or side panel has its own primary on top of the page's; Carbon
counts modals and side panels separately.

<a id="hero-in-app"></a>
### L14 · Hero or welcome banner inside the app

`layout` · scope `app` · weight 2 · detect `manual`

**Looks like.** A large greeting block at the top of a working screen: "Welcome back, Alex 👋",
"Here's what's happening today", an illustration, a gradient, sometimes a CTA that repeats the nav.

**Why it reads as generated.** no-slop-ui bans "Hero sections inside internal dashboards"
([no-slop-ui](https://github.com/LeoStehlik/no-slop-ui)); Uncodixfy: "No 'hero section' inside an
internal UI unless there is a real product reason" ([Uncodixfy](https://github.com/cyxzdev/Uncodixfy));
taste-skill's measured product screens: "There is no hero header"
([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `manual`. Ask: *if the top block of this screen were deleted, would the user lose
anything they need to act? Does the first thing below the header belong to the user's work?*

**Fix.** Start with the work: page title, the primary action, the list or queue. A greeting, if
the product wants one, fits in the header line.

```jsx
// Before
<section className="hero">
  <h1>Welcome back, Alex 👋</h1><p>Here's what's happening today.</p><img src="/illustration.svg" alt="" />
</section>

// After
<header className="page-header"><h1>Meetings</h1><Button variant="primary">New meeting</Button></header>
<MeetingTable filter="needs-my-action" />
```

**Not a tell when.** A genuine first-run screen with nothing in it yet, where the block explains
the first step and disappears once there is data.

**Put instead.** What needs the user now: items awaiting their action, the next meeting, recent
documents. taste-skill's blind tests also warn that screens which were too quiet lost: keep the
product's name and mark visible and the main job one click away
([taste-skill](https://github.com/Leonxlnx/taste-skill)).

<a id="fake-charts"></a>
### L15 · Charts standing in for content

`layout` · scope `app` · weight 2 · detect `manual`

**Looks like.** Sparklines under KPI numbers, a donut of "engagement", progress rings at 72%, an
area chart with no axis labels, filling a slot on the dashboard.

**Why it reads as generated.** Impeccable refuses "Sparklines, progress rings, and soft-shadowed
rounded rectangles standing in for content" ([Impeccable](https://github.com/pbakaus/impeccable));
Uncodixfy: "No fake charts" and calls out a "Donut chart paired with hand-wavy percentages"
([Uncodixfy](https://github.com/cyxzdev/Uncodixfy)). Shukla's generated dashboard ends in "a donut
chart" before the activity table ([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)).

**Detect.** `manual`. Ask: *what decision would change if this chart's shape changed? Can the user
read an actual value, period and unit from it?* If neither, it is filler.

**Fix.** A chart only when the shape of the data answers a question (trend, distribution,
comparison). Otherwise a number with its context, or a short table with a link to the rows.

```jsx
// Before
<Card><DonutChart value={72} /><p>Engagement</p></Card>

// After
<p>3 of 12 members have not opened the agenda. <a href="/meetings/42/attendance">See who</a></p>
```

**Not a tell when.** An analytics product, or a chart with labelled axes, a period and a unit that
someone uses to decide something.

**Put instead.** The underlying list. A sentence that names the exception and links to it is
usually more useful than a ring that summarises it.

---

## Motion

<a id="endless-motion"></a>
### M1 · Endless motion

`motion` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** Infinite animations with nothing behind them: pulsing dots, a rippling CTA, an
arrow that nudges forever, a shimmer sweeping across a button that is not loading, a border beam
circling a card (`animate-pulse`, `animate-ping`, `animation: … infinite`).

**Why it reads as generated.** Pulsing "Live" dots that are "JUST NEVER INACTIVE"
([godobject](https://godobject.dev/blog/10-tells-of-slop/)); taste-skill lists "infinite loops that
mean nothing" among motion tells ([taste-skill](https://github.com/Leonxlnx/taste-skill)). Product
systems treat looping motion as a cost: Carbon says to minimise motion that is "frequently
noticed by average users" ([Carbon](https://carbondesignsystem.com/elements/motion/overview/));
Atlassian rules out motion that "flashes, rapidly oscillates"
([Atlassian](https://atlassian.design/foundations/motion)). Shimmer buttons and border beams come
from copy-paste component libraries ([pkgpulse](https://www.pkgpulse.com/guides/aceternity-ui-vs-magic-ui-vs-shadcn-animated-react-2026)),
though no essay we found names them as a tell yet.

**Detect.** `dom`: computed `animation-iteration-count: infinite` on elements that are not loading
indicators. `static`: `animate-pulse`, `animate-ping`, `animate-bounce`, `infinite` in keyframe
usage.

**Fix.** Nothing loops. Motion answers an action (open, close, add, remove) and stops. Attention
comes from position and contrast, not from movement in the corner of the eye.

```css
/* Before */
.badge-live::before { animation: pulse 2s infinite; }
.cta                { animation: ripple 1.5s infinite; }

/* After */
.badge-live::before, .cta { animation: none; }
```

Tailwind: `animate-pulse` on a status dot → remove the class.

**Not a tell when.** Loading indicators while something is actually loading; a recording or
on-air indicator while recording; a live call's speaking indicator. Each must stop when the state
ends.

<a id="hover-lift"></a>
### M2 · Hover lift and scale

`motion` · scope `both` · weight 0 · detect `dom` `static`

**Looks like.** Cards and buttons that rise or grow on hover, usually with a bigger shadow:
`hover:-translate-y-1 hover:scale-105 hover:shadow-xl transition-all duration-300`.

**Why it reads as generated.** Anthropic: "hover transitions on every card are the generic
default and read as AI-generated"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
no-slop-ui bans "Transform, bounce, scale, or spring hover effects"
([no-slop-ui](https://github.com/LeoStehlik/no-slop-ui)); Uncodixfy asks for "no transform
effects" ([Uncodixfy](https://github.com/cyxzdev/Uncodixfy)); DeSlop flags `hover:scale-105` on
cards ([DeSlop](https://github.com/nate-zz/DeSlop)).

**Detect.** `dom`: compare the computed `transform` of a card or button in idle and forced
`:hover` state. `static`: `hover:scale-`, `hover:-translate-y-`, `whileHover={{ scale`.

**Fix.** Change colour, background or border on hover and keep the layout still. Moving targets
under the pointer are harder to click, and lifting implies the item can be dragged.

```css
/* Before */
.card       { transition: all .3s; }
.card:hover { transform: translateY(-4px) scale(1.02); box-shadow: 0 20px 40px rgb(0 0 0 / .15); }

/* After */
.card       { transition: border-color 100ms ease-out; }
.card:hover { border-color: var(--border-strong); }
```

Tailwind: `hover:-translate-y-1 hover:shadow-xl transition-all duration-300` →
`hover:border-[var(--border-strong)] transition-colors duration-100`.

**Not a tell when.** A draggable item lifting as it is picked up; Atlassian keeps raised elevation
for exactly that ([Atlassian](https://atlassian.design/foundations/elevation)).

<a id="transition-all"></a>
### M3 · transition: all

`motion` · scope `both` · weight 1 · detect `dom` `static`

**Looks like.** `transition: all 300ms ease` or Tailwind `transition-all` on buttons, cards and
inputs, one duration shared by everything.

**Why it reads as generated.** It is the shortest snippet that "works", so it is everywhere.
Hallmark: "Never transition: all" ([Hallmark](https://github.com/Nutlope/hallmark)); Shukla flags
"transition: all .15s" with a hover translate
([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)). It also animates properties nobody
meant to animate (layout, colour on theme switch) and costs paint.

**Detect.** `dom`: computed `transition-property: all` with a non-zero duration. `static`:
`transition-all`, `transition: all`.

**Fix.** Transition only the properties that change, 50–150ms for hover and press. Atlassian:
"If someone will trigger this motion dozens of times a day, keep it under 150ms"
([Atlassian](https://atlassian.design/foundations/motion)); Carbon's short durations are 70 and
110ms ([Carbon](https://carbondesignsystem.com/elements/motion/overview/)).

```css
/* Before */
.btn { transition: all 300ms ease; }

/* After */
.btn { transition: background-color 100ms ease-out, border-color 100ms ease-out; }
```

**Not a tell when.** Rarely legitimate. A throwaway prototype, perhaps.

<a id="fade-up-reveal"></a>
### M4 · Fade-up on every section

`motion` · scope `landing` · weight 2 · detect `dom` `static`

**Looks like.** Every section fades in and rises 20–40px as it scrolls into view, with the same
duration and easing (`data-aos="fade-up"`, `whileInView={{ opacity: 1, y: 0 }}`).

**Why it reads as generated.** "Every element fades in from below. All at the same time. On a
300ms `ease` curve", because it is "the safest, most-quoted snippet on the internet"
([dev.to](https://dev.to/kenimo49/framer-motion-view-transitions-api-4-ui-motion-patterns-that-stop-looking-ai-generated-25gg)).
Anthropic names "fade-and-slide-up entrances on each section" as the generic default
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
Reveals that depend on script also fail and leave a blank band, which taste-skill lists as a tell
of its own ([taste-skill](https://github.com/Leonxlnx/taste-skill)).

**Detect.** `dom`: three or more sections that start at `opacity: 0` with a transform and share one
transition. `static`: `data-aos`, `whileInView`, `IntersectionObserver` adding a `reveal` class
across sections.

**Fix.** Content is simply there. Spend motion on at most one deliberate moment; Anthropic's
cookbook argues that "one well-orchestrated page load… creates more delight than scattered
micro-interactions" ([Anthropic cookbook](https://platform.claude.com/cookbook/coding-prompting-for-frontend-aesthetics)).
Any reveal must start from visible content when scripting is off.

```jsx
// Before
{sections.map(s => (
  <motion.section initial={{ opacity: 0, y: 40 }} whileInView={{ opacity: 1, y: 0 }} transition={{ duration: 0.6 }}>…</motion.section>
))}

// After
{sections.map(s => <section>…</section>)}
```

**Not a tell when.** One entrance that explains something (a diagram assembling in order, a
before/after reveal) and respects reduced motion.

**Put instead.** Rhythm from the layout: sections that differ in width, density and imagery give
the scroll its variety without animation.

<a id="no-reduced-motion"></a>
### M5 · Motion without a reduced-motion path

`motion` · scope `both` · weight 1 · detect `dom` `static`

**Looks like.** Keyframe animations, parallax, smooth scrolling or transform transitions with no
`prefers-reduced-motion` rule anywhere in the CSS.

**Why it reads as generated.** Generators add motion and skip the accessibility half; Anti-AI-UI
lists the missing rule as a tell ([Anti-AI-UI](https://github.com/Vanszs/Anti-AI-UI)). Cloudscape
respects `prefers-reduced-motion` as a foundation rule
([Cloudscape](https://cloudscape.design/foundation/visual-foundation/motion/)); Hallmark fails any
keyframe or transform transition without a fallback ([Hallmark](https://github.com/Nutlope/hallmark)).

**Detect.** `static`: animations or transform transitions present, no
`@media (prefers-reduced-motion` in the stylesheets. `dom`: emulate `reduce` and check that
animated elements stop.

**Fix.** Make motion opt-in: write it inside `no-preference`, so the reduced path is the default.
If that is too late for the codebase, add a global reduce rule.

```css
/* Preferred: motion only when the user has not asked for less */
@media (prefers-reduced-motion: no-preference) {
  .drawer { transition: transform 200ms ease-out; }
}

/* Retrofit */
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: .01ms !important; animation-iteration-count: 1 !important;
    transition-duration: .01ms !important; scroll-behavior: auto !important;
  }
}
```

**Not a tell when.** The page has no motion at all.

---

## Copy and content

<a id="buzzwords"></a>
### W1 · Buzzword copy

`copy` · scope `both` · weight 2 · detect `dom`

**Looks like.** "Unlock", "supercharge", "seamless", "elevate", "unleash", "revolutionise your
workflow"; the correction cadence "not just X, it's Y"; "Your all-in-one platform"; tricolon
headlines like "Build faster. Ship smarter."

**Why it reads as generated.** These verbs recur across every copy list
([godobject](https://godobject.dev/blog/10-tells-of-slop/),
[microcopyexamples](https://microcopyexamples.substack.com/p/avoid-landing-page-words),
[taste-skill](https://github.com/Leonxlnx/taste-skill)). "Build faster. Ship smarter."
([925studios](https://www.925studios.co/blog/ai-slop-design-tells)) and "Scale without limits."
([kostac](https://world.hey.com/kostac/spot-the-slop-a-ui-designer-s-guide-to-fixing-ai-defaults-4c448c9c))
are headlines that fit any product, which is the problem.

**Detect.** `dom`: wordlist match on headings, buttons and the first paragraph; regex for
`not (just|only) .+, (it's|it is)`.

**Fix.** Say what the product does, with a concrete noun and verb from the customer's work. A
good test: the sentence should be false for a competitor.

```html
<!-- Before -->
<h1>Supercharge your meetings with our all-in-one platform</h1>
<p>Not just a tool. It's a revolution in how teams collaborate.</p>

<!-- After -->
<h1>Send the agenda, vote on resolutions and sign the minutes without printing a page</h1>
```

**Not a tell when.** The word is literal: "unlock" for a smart lock, "seamless" for knitwear.

<a id="generic-cta"></a>
### W2 · Generic call to action

`copy` · scope `both` · weight 0 · detect `dom`

**Looks like.** "Get started", "Learn more", "Submit", "Click here", "Continue" as the label of the
button that does the most important thing.

**Why it reads as generated.** Anthropic: "A CTA says exactly what happens… 'Save changes,' not
'Submit.'"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md));
taste-skill lists "Get started" and "Learn more" as copy tells
([taste-skill](https://github.com/Leonxlnx/taste-skill)). Cloudscape asks for 1–2-word labels
without articles or "please" ([Cloudscape](https://cloudscape.design/components/button/)).

**Detect.** `dom`: button and link text matching the generic list, weighted up when it is the
primary button.

**Fix.** Verb plus object, naming the result. The confirmation then echoes it: a "Publish" button
produces "Published".

```html
<!-- Before -->
<button>Submit</button>
<a href="/security">Learn more</a>

<!-- After -->
<button>Create meeting</button>
<a href="/security">Read the security model</a>
```

**Not a tell when.** "Cancel", "Close", "Back", "Next" in a stepper, where the generic word is the
precise one.

<a id="explainer-copy"></a>
### W3 · Copy that explains the screen

`copy` · scope `both` · weight 1 · detect `dom` `manual`

**Looks like.** A subtitle that restates the title ("Users. Manage your users here."), notes
describing what the UI does ("This section shows your recent activity"), stack trivia leaking into
the page ("Built with Next.js and Tailwind").

**Why it reads as generated.** The model narrates its own output. Uncodixfy bans "Section notes and
mini-notes everywhere explaining what the UI does" ([Uncodixfy](https://github.com/cyxzdev/Uncodixfy));
godobject quotes self-descriptions like "Built with Hugo. Written from Neovim."
([godobject](https://godobject.dev/blog/10-tells-of-slop/)). Cloudscape: "Don't repeat heading or
button text in the description" ([Cloudscape](https://cloudscape.design/patterns/general/empty-states/));
Fluent: "Prune every excess word" ([Fluent 2](https://fluent2.microsoft.design/content-design)).

**Detect.** `dom`: a subtitle sharing most content words with its heading; sentences starting
"This page/section shows". `manual`: *Would a first-time user miss this sentence if it were gone?
Does it tell them something the heading and controls do not?*

**Fix.** Delete it. A header line may carry data (a count, a date range, a filter summary), not an
explanation of the interface.

```jsx
// Before
<h1>Users</h1>
<p>This page shows a list of users. You can search, filter and edit users here.</p>

// After
<h1>Users <span className="count">48</span></h1>
```

**Not a tell when.** A sentence states a consequence the user cannot see: "Deleting a meeting also
deletes its recording."

<a id="exclamations"></a>
### W4 · Exclamation marks and cheer

`copy` · scope `both` · weight 0 · detect `dom`

**Looks like.** "Oops! Something went wrong!", "Awesome! Your meeting is ready!", "You're all
set! 🎉", jokes in error states.

**Why it reads as generated.** Systems agree, unusually explicitly. Atlassian: "Avoid exclamation
marks in UI copy" ([Atlassian](https://atlassian.design/foundations/content/language-and-grammar));
Fluent: "Avoid exclamation points" ([Fluent 2](https://fluent2.microsoft.design/content-design));
Carbon's empty states: "Don't joke" ([Carbon](https://carbondesignsystem.com/patterns/empty-states-pattern/));
Primer: error states should not be playful ([Primer](https://primer.style/product/ui-patterns/empty-states/)).

**Detect.** `dom`: `!` in UI text outside user content; a wordlist of "Oops", "Awesome", "Yay",
"Woohoo".

**Fix.** Calm statements. Errors name the problem and the recovery, and "Errors don't apologize"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).

```text
Before: Oops! Something went wrong! Please try again!
After:  Couldn't save the agenda. Check your connection and try again.

Before: Awesome! Meeting created!
After:  (no message: the new meeting appears in the list)
```

**Not a tell when.** User-generated content, or a brand voice that is deliberately exuberant and
documented as such.

<a id="template-leak"></a>
### W5 · Template leakage

`copy` · scope `both` · weight 2 · detect `dom`

**Looks like.** Lorem ipsum, `[Name]`, `{{user.firstName}}`, "John Doe", "Jane Smith", "Acme Inc",
`example.com` addresses, "Your Company" in the shipped UI.

**Why it reads as generated.** Unresolved placeholders are direct evidence that nobody looked at
the rendered page: godobject's example is "Welcome to your Dashboard, [Name] ✨"
([godobject](https://godobject.dev/blog/10-tells-of-slop/)); Hallmark lists placeholder names
and "startup clichés (Acme, Nexus)" ([Hallmark](https://github.com/Nutlope/hallmark)); antislop
separates honest placeholders from fake-looking data like "John Doe"
([antislop](https://github.com/miqdadbadjuber/anti-slop)).

**Detect.** `dom`: visible text matching `lorem ipsum`, `\[[A-Z][a-z]+\]`, `\{\{.+\}\}`, a list of
stock names and companies, `@example\.(com|org)`.

**Fix.** Real data, or an honest empty state. Seed data for demos should look like the customer's
world and be clearly marked as sample data.

```jsx
// Before
<h1>Welcome back, [Name]</h1>
<td>John Doe</td><td>john@example.com</td>

// After
<h1>{user.displayName}</h1>                 {/* or no greeting at all */}
<EmptyState title="No members yet" action={<Button variant="secondary">Invite members</Button>} />
```

**Not a tell when.** An input's `placeholder` showing a format (`name@company.com`), or
documentation where `example.com` is the reserved domain for examples.

<a id="invented-proof"></a>
### W6 · Invented social proof

`copy` · scope `landing` · weight 0 · detect `dom` `manual`

**Looks like.** A stat banner of round numbers ("10k+ teams · 99.9% uptime · 4.9★"), a "Trusted by"
strip of famous logos, testimonials with a first name, a stock avatar and no organisation.

**Why it reads as generated.** Stat banners appeared on 12.2% of sites
([Krebs](https://www.adriankrebs.ch/blog/design-slop/)). Hallmark: "Never an invented metric,
testimonial, logo wall" ([Hallmark](https://github.com/Nutlope/hallmark)); Anti-AI-UI counts the
"Trusted by" strip among its signature tells ([Anti-AI-UI](https://github.com/Vanszs/Anti-AI-UI)).
Anthropic notes that "a big number with a small label, supporting stats, and a gradient accent is
the default treatment"
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).

**Detect.** `dom`: rows of large numbers with `+`, `%` or `★` and short labels; a heading matching
"trusted by" followed by logo images. `manual`: *Can each number, logo and quote be traced to a
source you could show a customer?*

**Fix.** Only claims you can source, with the source, date and permission. Otherwise leave them
out; an unsupported claim costs more trust than an absent one.

```html
<!-- Before -->
<div class="stats"><b>10k+</b> teams <b>99.9%</b> uptime <b>4.9★</b> rating</div>

<!-- After: one real customer, named with permission -->
<figure class="quote">
  <blockquote>We stopped printing 300 pages for every board meeting.</blockquote>
  <figcaption>Name Surname, Company Secretary, Organisation (quoted with permission)</figcaption>
</figure>
```

**Not a tell when.** The numbers are real, current and sourced (a public status page for uptime, a
linked review platform for ratings), and the logos are customers who agreed to appear.

**Put instead.** One specific customer story, a public changelog that shows the product moving, a
live demo or a free trial, or a precise claim the reader can check ("Exports minutes as PDF/A").

---

## Accessibility and states

<a id="low-contrast"></a>
### A1 · Low-contrast text

`a11y` · scope `both` · weight 0 · detect `dom`

**Looks like.** Grey-on-grey secondary text (`text-gray-400` on white is about 2.5:1), grey text on
a coloured surface, light grey placeholders, `slate-400` on `slate-800` cards in a dark theme.

**Why it reads as generated.** Kata lists "Text under 4.5:1" with removed focus rings and meaning
carried by colour alone ([Kata](https://kata.leafphp.dev/checker)); NN/g names "Poor color
contrast" among the flaws of AI prototypes ([NN/g](https://www.nngroup.com/articles/ai-prototyping/)).
Impeccable adds that on coloured surfaces secondary text should be tinted from the surface, "never
gray" ([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `dom`: computed contrast between text colour and its effective background, below 4.5:1
for body text and 3:1 for large text (24px, or 18.66px bold) and UI marks.

**Fix.** Meet WCAG 2.2 AA: 4.5:1 for body text, 3:1 for large text and for icons, field edges and
focus rings. Fix the token, not the instance, so every muted text passes at once.

```css
/* Before */
.muted { color: #9ca3af; }                     /* gray-400 on white ≈ 2.5:1 */

/* After */
.muted { color: var(--muted-ink); }            /* token chosen at ≥ 4.5:1 on --surface and --canvas */
```

**Not a tell when.** Disabled controls and decorative text are exempt from the contrast minimum,
though a disabled control should still say why it is disabled.

<a id="focus-removed"></a>
### A2 · Focus ring removed

`a11y` · scope `both` · weight 2 · detect `static`

**Looks like.** `outline: none` / `outline: 0` on `:focus`, or Tailwind `focus:outline-none`, with
no visible replacement; keyboard users cannot see where they are.

**Why it reads as generated.** Generators remove the browser ring because it looks unpolished in a
screenshot, then forget the replacement. Kata lists "focus rings removed"
([Kata](https://kata.leafphp.dev/checker)); Hallmark fails an outline "removed with nothing in its
place" and requires a ring at 3:1 or better ([Hallmark](https://github.com/Nutlope/hallmark));
Impeccable counts focus rings among the browser surfaces models "skip most reliably"
([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `static`: `outline: none|0` or `outline-none` on a focus selector or interactive
component, with no `:focus-visible` rule (or `focus-visible:ring`) for the same element.

**Fix.** A visible `:focus-visible` ring in a token colour, drawn with `outline` so it does not
shift layout, shown instantly.

```css
/* Before */
button:focus { outline: none; }

/* After */
:focus-visible { outline: 2px solid var(--focus-ring); outline-offset: 2px; }
```

Tailwind: `focus:outline-none` → `focus-visible:outline-2 focus-visible:outline-offset-2
focus-visible:outline-[var(--focus-ring)]`.

**Not a tell when.** `outline: none` paired with an equally visible replacement (a border or
`box-shadow` ring at 3:1) on the same focus state.

<a id="happy-path-only"></a>
### A3 · Only the happy path

`a11y` · scope `both` · weight 2 · detect `manual`

**Looks like.** The screen looks finished with the six tidy rows in the mock data and nothing else:
no loading state, no empty or filtered-empty state, no error, and a layout that breaks on a long
name or a 30-word title.

**Why it reads as generated.** "AI nails the happy path every time"
([kostac](https://world.hey.com/kostac/spot-the-slop-a-ui-designer-s-guide-to-fixing-ai-defaults-4c448c9c));
generated UI "usually looks convincing in the one state shown in the prompt"
([21st.dev](https://21st.dev/blog/ui-components-for-vibe-coding)). Shukla's rule is "Design for
real, hostile content" ([Shukla](https://github.com/Vinayak-Shukla-03/anti-ai-slop)); Impeccable's
verify list names hover, disabled, loading, error and empty ([Impeccable](https://github.com/pbakaus/impeccable)).

**Detect.** `manual`. For each list and form, check:
- Zero items: is there a heading, a one-line reason and one secondary action?
- Filters that match nothing: does it say so and offer to clear them, distinct from "no data"?
- Loading: does the layout hold its size, without a full-screen spinner for a 100ms request?
- Error: does it name the problem and the recovery, separately from the empty state?
- Hostile data: a 60-character name, a 30-word title, 1,000 rows, a missing optional field.

**Fix.** Design each state as part of the screen. Cloudscape: "Don't use empty states for errors"
([Cloudscape](https://cloudscape.design/patterns/general/empty-states/)).

```jsx
if (isLoading) return <TableSkeleton rows={5} />;
if (error)     return <ErrorState message="Couldn't load meetings." onRetry={refetch} />;
if (!rows.length)
  return filters.active
    ? <EmptyState title="No meetings match these filters" action={<Button onClick={clearFilters}>Clear filters</Button>} />
    : <EmptyState title="No meetings yet" action={<Button variant="secondary">New meeting</Button>} />;
```

**Not a tell when.** Static content with no data source.

<a id="toast-everything"></a>
### A4 · A toast for every action

`a11y` · scope `app` · weight 1 · detect `manual`

**Looks like.** "Saved successfully!", "Deleted successfully!", "Filter applied" toasts after
actions whose result is already visible: the row disappeared, the field shows the new value.

**Why it reads as generated.** One builder's docs say it outright: "The AI adds toasts automatically
when building forms, delete actions, and API calls" ([vybe](https://docs.vybe.build/tips/building-uis)).
Hallmark's rule: "If the effect is visible, no toast. Toasts are for failures and invisible
effects" ([Hallmark](https://github.com/Nutlope/hallmark)). Redundant toasts also interrupt screen
reader users with announcements that add nothing.

**Detect.** `manual`. For each toast call, ask: *Is the result already visible where the user is
looking? If yes, delete the toast. Is it off-screen, delayed, or a failure? Keep it.*

**Fix.** Toast only when the result is off-screen (a background export finished), delayed, or
failed. Let a visible change be its own confirmation, and keep the wording of the action and the
confirmation aligned.

```js
// Before
await deleteMeeting(id);
toast.success("Deleted successfully!");

// After
await deleteMeeting(id);              // the row disappears; that is the confirmation
// keep: toast.error("Couldn't delete the meeting. Try again.") in the catch
// keep: toast("Minutes exported", { action: { label: "Download", onClick: download } })
```

**Not a tell when.** Undo affordances ("Meeting deleted. Undo"), where the toast carries an action
the user may need.

---

## Fixing without creating the next wave

Every tell in this file was once a reasonable idea, and some were deliberate fixes. The warm
cream ground, the serif display face and the terracotta or sage accent were the published remedy
for purple gradients; models adopted the remedy wholesale and it became the next look. taste-skill keeps its registry dated for this reason and
refuses to name replacement fonts or colours, because "every published 'use this instead' list has
become the next wave of tells within months" ([taste-skill](https://github.com/Leonxlnx/taste-skill)).
unslop-ui watches the same drift in real time: cream, serif and a sage or forest primary, where
"Swapping one for the other resets the clock" ([unslop-ui](https://claudecodehq.com/playbooks/unslop-ui)).
Anthropic's own skill now lists the editorial cream look *first* among the generated clusters
([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).

So the fixes here are procedures, not swaps:

- **Derive, do not pick.** Colour from the brand and the product's existing CSS; type from the
  reader and the content; theme from where and when the product is used. Write the reason next to
  each token. A value with no reason is a default, however tasteful it looks.
- **Check the plan against the defaults before building.** Anthropic's process is to write the
  token plan first and revise any part that "reads like the generic default", saying what changed
  and why ([Anthropic skill](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)).
- **Spend boldness once.** "Spend your boldness in one place" (same source): one committed move,
  such as a distinctive display face, a photograph or a strong colour field, is worth more than ten
  safe ones.
- **The brief and the existing system win.** If the project already has tokens, they override
  every rule in this file. Record the difference in `.slop.json` rather than bending the product.

The opposite failure is just as recognisable. Strip every tell and put nothing in its place and
you get flat white, thin grey borders, a generic face and no identity. antislop calls that the
"Sterile Default" and says "The fix is never more bans" ([antislop](https://github.com/miqdadbadjuber/anti-slop));
taste-skill: "A page with every tell removed and nothing put in their place is an empty page with
good manners" ([taste-skill](https://github.com/Leonxlnx/taste-skill)). In its blind tests of
product screens, the screens that were too quiet lost to richer ones.

What fills the gap is content, not decoration: the product's real screens, the customer's own
words and data, the user's actual queue, a type scale with real contrast, one colour that means
"act here". Use the **Put instead** notes in each entry. Then take a screenshot, look at it as a
stranger would, and ask the only question that matters: could this page belong to any other
product? If it could, the work is not finished, however few tells the checker reports.
