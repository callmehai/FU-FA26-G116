# Sources

Everything this kit flags traces back to one of the sources below. Star counts, licences and last
push dates for repositories were read from the GitHub API on **2026-10-23**; they will drift.
Quotes are short and attributed; the rules in this kit are written in our own words.

---

## Research and measurements

**Adrian Krebs, "Design slop" study.** https://www.adriankrebs.ch/blog/design-slop/
The only measurement we found. Krebs loaded Show HN landing pages with Playwright and tested
computed styles and the DOM with deterministic checks, not a model judging screenshots, and puts
his false-positive rate at 5–10%. The blog reports 1,590 sites, with 22% matching four or more
patterns and 32% matching two or three. Per-pattern rates from an earlier 500-site run were
reported by GIGAZINE (https://gigazine.net/gsc_news/en/20260423-design-slop): gradients 28%,
default shadcn 23.5%, centred hero 23.5%, templated card grids 20.1%, permanent dark mode 20.3%,
glass panels 17.1%, trend fonts 15.8%, side stripes 13%, stat banners 12.2%, purple CTAs 10.7%,
tracked-caps eyebrows 10.5%.
*What we took:* the method (computed styles in a real browser), the per-pattern frequencies that
informed our weights, and the point that one pattern proves nothing and only combinations do.
Krebs also notes that unedited templates looked like this before AI, which is why the kit talks
about "generic", not "machine-made".

**Nielsen Norman Group, "AI prototyping".** https://www.nngroup.com/articles/ai-prototyping/
Without visual specs, AI prototyping tools produce designs with "a similar, generic look" because
they default to libraries such as shadcn and Tailwind; the results are "good from afar, but far
from good". Also lists weak hierarchy, inconsistent spacing and poor contrast.
*What we took:* the link between framework defaults and sameness (C3, C6), and contrast (A1).

**Nielsen Norman Group, "The AI sparkles icon problem".**
https://www.nngroup.com/articles/ai-sparkles-icon-problem/
The sparkle means something different in each interface and is easily confused with a favourite
star. See also Big Medium, "Your sparkles are fizzling":
https://bigmedium.com/ideas/your-sparkles-are-fizzling.html
*What we took:* the sparkle half of L8.

**Anthropic, "Improving frontend design through skills"** (Rajasekaran, Wei and Bricken,
2025-11-12). https://claude.com/blog/improving-frontend-design-through-skills (also served at
https://claude.com/resources/articles/improving-frontend-design-through-skills)
Names the cause: **distributional convergence**. Safe choices dominate the training data, so an
unguided model samples the high-probability centre: Inter, purple gradients on white, minimal
motion. Recommends naming the convergent defaults, giving concrete alternatives, and shipping the
guidance as a skill.
*What we took:* the framing in the README, T1 and C3, and the decision to ship as a skill.

**Anthropic cookbook, "Prompting for frontend aesthetics".**
https://platform.claude.com/cookbook/coding-prompting-for-frontend-aesthetics
(notebook: https://github.com/anthropics/claude-cookbooks, `coding/prompting_for_frontend_aesthetics.ipynb`)
The prompt behind the blog post: an avoid list (overused fonts, purple gradients on white,
predictable layouts) and the observation that output still converges on choices such as Space
Grotesk.
*What we took:* T2, the idea that a replacement default becomes the next tell.

### Commentary we read

These are essays and posts, not measurements. We used them to name patterns and cross-check the
skill sources, not to set weights.

| Source | Used for |
|---|---|
| 925 Studios, AI slop design tells: https://www.925studios.co/blog/ai-slop-design-tells | C1, two-beat slogans (W1) |
| The Fountain Institute, signs of a vibe-coded UI: https://www.thefountaininstitute.com/blog/signs-vibe-coded-ui | C1, C4, S6, S8, L8, L9 |
| godobject, 10 tells of slop: https://godobject.dev/blog/10-tells-of-slop/ | C4, S7, L9, W1, W3 |
| noqta, overused AI design patterns: https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026 | T3, S1, S3, L9 |
| Kosta C., "Spot the slop": https://world.hey.com/kostac/spot-the-slop-a-ui-designer-s-guide-to-fixing-ai-defaults-4c448c9c | S1, M4, A3, W1 |
| Kata checker: https://kata.leafphp.dev/checker | T8, S4, L7, L8, A1, A2 |
| kenimo49, motion patterns that stop looking AI-generated: https://dev.to/kenimo49/framer-motion-view-transitions-api-4-ui-motion-patterns-that-stop-looking-ai-generated-25gg | M4 |
| prg.sh, why AI builds the same purple site: https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website | C3, T1, S4, L4 |
| unslop-ui playbook: https://claudecodehq.com/playbooks/unslop-ui | C3, C5, C7, M2; its doubts about glass and blobs as tells |
| Microcopy Examples, words to avoid on landing pages: https://microcopyexamples.substack.com/p/avoid-landing-page-words | W1 |
| Robots on Payroll, avoiding the vibe-coded look: https://robotsonpayroll.substack.com/p/how-to-avoid-that-vibe-coded-look | prose tells (W1) |
| vybe docs, building UIs: https://docs.vybe.build/tips/building-uis | A4 |
| 21st.dev, UI components for vibe coding: https://21st.dev/blog/ui-components-for-vibe-coding | A3 |

The claim that a Tailwind UI default (`bg-indigo-500`) seeded the purple look comes from a joking
post by Adam Wathan (August 2025) that we only found paraphrased by others; treat it as folklore.

---

## Community skills

Stars, licence and last push as reported by the GitHub API on 2026-10-23.

| Skill | URL | Stars | Last push | Licence | What we took |
|---|---|---|---|---|---|
| Anthropic `frontend-design` | https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md | 183,188 (whole `anthropics/skills` repo) | 2026-10-09 (skill changed 2026-09-03) | Apache-2.0 (skill's own `LICENSE.txt`) | Its five convergence clusters, which became C5, C7, S1, S4, T3, T5 and L11; L6 (numbers only for sequences); M4 (fade-up on every section); "Save changes, not Submit" (W2); errors that don't apologise. |
| taste-skill (Leonxlnx) | https://github.com/Leonxlnx/taste-skill | 101,418 | 2026-10-20 | MIT | The "waves" idea: the fix for one default becomes the next tell, so the kit names no replacement fonts (T2, C7). From its app-screen notes measured on 23 products: colour on data not chrome (C4), KPIs as type (L1), no card shadows (S4), no hero (L14), five sizes far apart (T6). Its warning that a page with every tell removed and nothing added is still not good. |
| Impeccable (Paul Bakaus) | https://github.com/pbakaus/impeccable | 85,261 | 2026-10-23 | Apache-2.0 | Side stripes over 1px (S3), nested cards (S8), charts standing in for content (L15), monospace as costume (T5), its verify list: contrast, all states, errors naming the recovery (A1, A3). |
| Hallmark (Nutlope / Together AI) | https://github.com/Nutlope/hallmark | 31,070 | 2026-10-22 | MIT | Non-waivable floor items: no `transition: all` (M3), no single accented word (T4), no invented proof (W6), no tracked caps on labels (T3); stock Tailwind swatches (C6); 12px floor (T7); one radius per nesting level (S1). It ships a linter, which encouraged us to ship one too. |
| antislop (miqdadbadjuber) | https://github.com/miqdadbadjuber/anti-slop | 6,930 | 2026-10-23 | MIT | The Tell / Why / Fix entry shape; arrows on every button (L11), capsule badges and decorative dots (L9), sparkle icons (L8). We did **not** adopt its view that a uniform icon set such as Lucide is itself a tell. |
| Uncodixfy (cyxzdev) | https://github.com/cyxzdev/Uncodixfy | 2,667 | 2026-03-18 | MIT | The "keep it normal" baseline for internal tools: no hero in an app (L14), no metric-card grid by reflex (L1), no section notes explaining the UI (W3), no hover transforms (M2), no `::before` status dots (L9), no badge on every row (L10). Its rule that the project's existing colours and fonts win shaped `.slop.json`. |
| Anti-AI-UI (Vanszs) | https://github.com/Vanszs/Anti-AI-UI | 16 | 2026-06-19 | MIT | Gradient text (C2), glass nav, missing reduced-motion path (M5). |
| no-slop-ui (LeoStehlik) | https://github.com/LeoStehlik/no-slop-ui | 10 | 2026-08-31 | MIT | Fake metric grids and heroes inside dashboards (L1, L14), no transform or scale hover (M2), 6–10px button radius rather than pills. |
| anti-ai-slop (Vinayak-Shukla-03) | https://github.com/Vinayak-Shukla-03/anti-ai-slop | 9 | 2026-07-02 | MIT | The default-dashboard description, the side stripe as a reliable tell (S3), hairline plus diffuse shadow (S5), `transition: all` with a lift (M2, M3), designing for hostile content (A3). Same name as this kit, separate project. |
| DeSlop (nate-zz) | https://github.com/nate-zz/DeSlop | 0 | 2026-07-14 | MIT | Stock framework palette (C6), elevation soup (S5), icon in a tinted box (L2), tabular numerals for data (T5 fix), and a grep sweep that inspired the static scanner. |

---

## Design systems

Official pages for the rules in [design-systems.md](design-systems.md).

| System | Entry point |
|---|---|
| AWS Cloudscape | https://cloudscape.design/ |
| IBM Carbon | https://carbondesignsystem.com/ |
| Atlassian Design System | https://atlassian.design/ |
| GitHub Primer | https://primer.style/ (component source: https://github.com/primer/react, sizes: https://github.com/primer/primitives) |
| Microsoft Fluent 2 | https://fluent2.microsoft.design/ |
| Google Material 3 | https://m3.material.io/ (token values: https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192) |
| Apple Human Interface Guidelines | https://developer.apple.com/design/human-interface-guidelines/ |
| WCAG 2.2 (contrast, A1) | https://www.w3.org/TR/WCAG22/ |

---

## How this kit differs

The skills above are mostly prose for an agent to follow while it writes code. This kit keeps
that (SKILL.md and these references) and adds four things:

1. **A measured DOM checker.** `checker/detect.js` runs inside the rendered page and reads
   computed styles, the way Krebs did, so it sees what Tailwind classes, CSS-in-JS and component
   libraries actually produce. A static scanner covers what source text shows reliably.
2. **A registry with weights.** `tells.json` is the single list. The checker, the scanner, the
   report and the docs all key on its IDs, and each tell carries a weight, so a page gets a score
   instead of a pass/fail on any single pattern.
3. **A project exceptions file.** `.slop.json` lets a team record that a pattern is their
   deliberate choice, with a reason, per tell or per selector. A brand that really is violet is
   not slop.
4. **Both product UIs and landing pages.** Every tell is scoped `app`, `landing` or `both`. Most
   existing lists target marketing pages; dashboards, tables and forms have their own tells (KPI
   tiles, a badge on every row, a toast for every action).
