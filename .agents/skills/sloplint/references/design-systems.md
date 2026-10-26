# The professional baseline: what mature design systems agree on

A generated UI usually looks generic for a simple reason: it ignores rules that teams shipping
large products settled years ago. This file collects those rules from seven public design
systems, so that every fix in this kit can point at a source instead of at taste.

Systems covered: AWS **Cloudscape**, IBM **Carbon**, **Atlassian**, GitHub **Primer**, Microsoft
**Fluent 2**, Google **Material 3** and **Apple HIG**.

How to read the tables:

- Quoted words are short quotes from the linked page. Everything else is our summary.
- **M3 (tokens)** rows come from Google's token source (`material-components/material-web`,
  `tokens/versions/v0_192`) and the Material Android docs, because m3.material.io only renders in
  a browser. They are token values, not written guidance.
- Primer and Fluent sizes were read from their component source on GitHub where the docs page
  does not print them.
- A rule is listed only if we read it on the official page or in official source. Forms are left
  out because we did not collect enough form guidance to state a consensus.

When a project already has a design system, its tokens win over everything here. Record the
difference in `.slop.json` (see the README) rather than bending the project to match.

---

## Buttons and hierarchy

| System | Rule | Link |
|---|---|---|
| Cloudscape | One primary button per page ("there can be only one"). In table rows use an inline link button so the row height does not change. Icons are not decoration. Labels are 1–2 words, no articles, no "please". | https://cloudscape.design/components/button/ |
| Carbon | A primary button appears once per screen (header, modals and side panels are counted separately). Groups hold 2–3 buttons; more go in a menu. Heights 24 / 32 / **40 (md)** / 48px; do not mix sizes in a group. Icon-only buttons are used sparingly and always get a tooltip. | https://carbondesignsystem.com/components/button/usage/ · https://carbondesignsystem.com/components/button/style/ |
| Atlassian | One primary button or CTA "in a page or area". Most buttons need no icon. Avoid disabled buttons, and never put a tooltip on one. | https://atlassian.design/components/button/usage |
| Primer | Use primary "sparingly": never two in one group, rarely two per page. Control heights 24 / 28 / **32 (medium)** / 40 / 48px. Medium radius 6px. | https://primer.style/product/components/button/ |
| Fluent 2 | One primary button per layout. When there are many minor actions, give all of them the outline, subtle or transparent style. Secondary actions never get the primary's weight. Button radius 4px. | https://fluent2.microsoft.design/components/web/react/core/button/usage |
| M3 (tokens) | Filled button 40px high, full (pill) radius, resting at elevation level 0. | https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192 |

**Consensus:** one filled primary per page or area; everything else outline, subtle or link;
2–3 buttons per group with the rest in a menu; 32–40px high in dense UI; labels are a verb and a
noun in sentence case, without "please" or trailing punctuation.

## Status, badges and chips

| System | Rule | Link |
|---|---|---|
| Cloudscape (badge) | Badge colours limited to grey, blue, green and five severity levels. No icons or imagery on badges. Badges do not show status and do not label features as new. | https://cloudscape.design/components/badge/ |
| Cloudscape (status) | Status is icon + text + colour, never colour alone. Red error, green success, blue info, **grey for stopped, pending, in progress, loading**. One or two words. Status sits in the second table column. | https://cloudscape.design/components/status-indicator/ |
| Carbon (tag) | Heights 18 / 24 / 32px, **16px radius (pill)**, 12px regular text, 8px padding, 16px icon. Under 20 characters. Tags are not links. | https://carbondesignsystem.com/components/tag/style/ · https://carbondesignsystem.com/components/tag/usage/ |
| Carbon (status) | Use at least two of colour, shape and symbol, at 3:1 contrast. More than five or six indicators overwhelm. Shapes go before labels, left-aligned in lists and tables. Show no indicator when no action is needed. | https://carbondesignsystem.com/patterns/status-indicator-pattern/ |
| Atlassian | A lozenge is for labels that change action or priority; plain metadata is a tag. Status uses semantic colours; accent colours are for user-made labels only. Sentence case, max 200px wide. | https://atlassian.design/components/lozenge/usage |
| Atlassian (radius) | **4px** for lozenges, tags and labels; 2px for badges; full radius for avatars and pills. | https://atlassian.design/foundations/radius |
| Primer (Label) | Pill, 20 / 24px high, 12px medium (500), transparent fill with a 1px coloured border. | https://github.com/primer/react/blob/main/packages/react/src/Label/Label.module.css |
| Primer (StateLabel) | Issue and PR state: solid, pill, semibold, always icon + text. | https://primer.style/product/components/state-label/ |
| Fluent 2 | Badge heights 16 / 20 / 24 / 32px; circular, rounded (4px) or square. One or two words. Do not mix badge sizes in one context. | https://fluent2.microsoft.design/components/web/react/core/badge/usage · https://fluent2.microsoft.design/shapes |
| M3 (tokens) | Chip 32px high, **8px corners**, 14px/500 label, 18px icon. Flat by default; the elevated variant is only for chips over images. | https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192 |

**Consensus:** status is a shape or icon plus one or two words, never colour alone; five or six
status colours at most, each with a fixed meaning, grey for pending; badges are neither status
nor links and carry no icon; 18–24px high with 12px text; one shape and one height per kind of
label on a screen.

## Colour

| System | Rule | Link |
|---|---|---|
| Cloudscape | The UI is mostly white and grey; colour is reserved for calls to action and status. Blue marks the primary action, links and interactive accents. | https://cloudscape.design/foundation/visual-foundation/colors/ |
| Carbon | Additional colours are used "sparingly and purposefully". Blue is the primary action colour. | https://carbondesignsystem.com/elements/color/overview/ |
| Atlassian | Every colour has a role (neutral, brand, information, success, warning, danger, discovery). Accent colours are interchangeable: swapping them should change nothing. | https://atlassian.design/foundations/color |
| Primer | Semantic roles (accent, success, attention, danger, done) in muted and emphasis variants. Colour is not the main means of emphasis in type. | https://primer.style/product/getting-started/foundations/color-usage/ |
| Fluent 2 | Do not overuse brand colour or put it on large surfaces. Semantic colours are never decoration. | https://fluent2.microsoft.design/color |
| Apple HIG | One colour, one meaning. Use background colour to communicate, not as a flourish. Do not colour the backgrounds of several controls. | https://developer.apple.com/design/human-interface-guidelines/color |

**Consensus:** a neutral UI; the brand colour marks the primary action, links, selection and
focus; semantic colours appear only where they mean something; no brand colour on large
surfaces.

## Elevation and effects

| System | Rule | Link |
|---|---|---|
| Atlassian | The raised level is for cards that can be moved; overlay is for modals, dialogs and menus. Do not raise content to group it when a border or whitespace would do. | https://atlassian.design/foundations/elevation |
| Carbon | The light theme separates layers by alternating White and Gray 10 surfaces, not by shadow. | https://carbondesignsystem.com/elements/color/overview/ |
| Fluent 2 | Shadow ramp 2 / 4 / 8 / 16 / 28 / 64; dialogs use 64. One light direction everywhere. Windows uses strokes instead of key shadows. | https://fluent2.microsoft.design/elevation |
| M3 (tokens) | Levels 0 / 1 / 3 / 6 / 8 / 12dp. Filled buttons and flat chips rest at **level 0**. | https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192 |
| Apple HIG | Liquid Glass (blur) belongs to the controls layer only, never the content layer, and is used sparingly. | https://developer.apple.com/design/human-interface-guidelines/materials |

**Consensus:** resting content is flat and grouped by borders, surface tone or whitespace;
shadows belong to overlays and draggable items; buttons and chips sit at level 0. None of these
systems recommends decorative gradients, coloured glows or glass panels in product UI.

## Motion

| System | Rule | Link |
|---|---|---|
| Cloudscape | Durations 115 / 165 / 250ms. Motion is used "purposefully and judiciously". Honour `prefers-reduced-motion`. Nothing flashes more than three times a second. | https://cloudscape.design/foundation/visual-foundation/motion/ |
| Carbon | Durations 70 / 110 / 150 / 240 / 400 / 700ms. Productive motion stays out of the way; expressive motion is for occasional, important moments. If average users notice it often, reduce it. | https://carbondesignsystem.com/elements/motion/overview/ |
| Atlassian | Hover and press 50–150ms; transitions 150–400ms. Anything triggered dozens of times a day stays under 150ms. One animation at a time. Exits faster than entrances. No flashing or oscillation. | https://atlassian.design/foundations/motion |
| Fluent 2 | No motion outside the element in focus. Fade large elements rather than slide them. | https://fluent2.microsoft.design/motion |
| Apple HIG | Do not add motion for its own sake; avoid it on frequent interactions. | https://developer.apple.com/design/human-interface-guidelines/motion |
| M3 (tokens) | Short durations 50–200ms, medium 250–400ms. | https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192 |

**Consensus:** 50–150ms for hover and press, 150–400ms for transitions; frequent interactions
stay under 150ms; one animation at a time; nothing loops, flashes or oscillates; reduced motion is
honoured.

## Type and spacing

| System | Rule | Link |
|---|---|---|
| Cloudscape | Body 14/20, small 12/16, h1 24/30. Nothing under 12px. Monospace for IDs and numbers. 4px grid: 2 / 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40. Comfortable density by default; compact for data-heavy views. | https://cloudscape.design/foundation/visual-foundation/typography/ · https://cloudscape.design/foundation/visual-foundation/spacing/ · https://cloudscape.design/foundation/visual-foundation/content-density/ |
| Carbon | Productive type set on a 14px base: body-compact 14/18, label 12/16. | https://carbondesignsystem.com/elements/typography/type-sets/ |
| Atlassian | 8px base; scale 2 / 4 / 6 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 / 64 / 80. 0–8px is for badges and inputs. | https://atlassian.design/foundations/spacing |
| Fluent 2 | Body 14/20, caption 12/16. Avoid all caps. | https://fluent2.microsoft.design/typography |
| M3 (tokens) | Body-medium 14/20; label-large 14/20 at weight 500. | https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192 |

**Consensus:** 14px body with an 18–20px line height, 12px floor; sentence case, not all caps; a
4px or 8px spacing grid (4 / 8 / 12 / 16 / 24 / 32); comfortable density by default.

## Data tables

| System | Rule | Link |
|---|---|---|
| Carbon | Row heights 24 / 32 / **40 (default)** / 48 / 64px, 64px only for two-line rows. Header 14px semibold, cells 14px regular, at least 16px between columns. Toolbar, header row and pagination match the row height. Up to two row actions inline, more in an overflow menu; at most five toolbar actions. | https://carbondesignsystem.com/components/data-table/style/ · https://carbondesignsystem.com/components/data-table/usage/ |
| Cloudscape | First column is the identifier or link, second is status. Text and categorical numbers (dates, phone numbers) left-aligned; quantities right-aligned with equal decimals. "-" for an empty cell. Filtering, sorting and paging only above five items. | https://cloudscape.design/components/table/ |
| Primer | Numbers right-aligned. Row actions in the last column; with several, use a menu and pull out at most one. A sortable table has a default sort. | https://primer.style/product/components/data-table/ |

**Consensus:** 40px rows by default (32 compact, 48 spacious); toolbar, header and paging match
the row height; text left, quantities right; identifier first, status second; row actions last,
one or two inline and the rest in a menu.

## Empty states and microcopy

| System | Rule | Link |
|---|---|---|
| Cloudscape | A heading such as "No distributions"; a description only when needed; the action is a secondary button. Empty states are not for errors. Do not repeat the heading or button in the description. No exclamation marks, "please", "thank you" or "etc.". | https://cloudscape.design/patterns/general/empty-states/ |
| Carbon | Keep words to a minimum. Illustrations are optional and dropped when space is short. No jokes. A tertiary button when several empty states share a screen. | https://carbondesignsystem.com/patterns/empty-states-pattern/ |
| Atlassian | Only the heading is required; limit the words. No exclamation marks in UI copy; no "e.g.", "i.e.", "etc." or "&". | https://atlassian.design/components/empty-state/usage · https://atlassian.design/foundations/content/language-and-grammar |
| Primer | One primary action; secondary text brief and not redundant; error states are not playful. | https://primer.style/product/ui-patterns/empty-states/ |
| Fluent 2 | Cut every word that is not needed; avoid exclamation points. | https://fluent2.microsoft.design/content-design |

**Consensus:** a short heading, an optional one-line reason and one action; no jokes, no
exclamation marks, no filler; illustrations are optional; errors get their own treatment. None of
the systems we read has an explicit emoji rule; the ban follows from "no jokes, not playful".

## Icons

| System | Rule | Link |
|---|---|---|
| Cloudscape | 16px icons. No icons on page, section or sub-section headings. Do not combine several icons. Status icons come with a label. | https://cloudscape.design/foundation/visual-foundation/iconography/ |
| Carbon | One icon size across the product (16px), coloured like the adjacent text, centred on the text line. | https://carbondesignsystem.com/elements/icons/usage/ |
| Primer | Icons supplement text rather than replace it; avoid them for abstract ideas; do not resize them. | https://primer.style/octicons/usage-guidelines/ |
| Fluent 2 | Regular icons by default, filled only for the selected state. One colour. | https://fluent2.microsoft.design/iconography |
| Apple HIG | Consistent size, stroke and weight, matched to the adjacent text. | https://developer.apple.com/design/human-interface-guidelines/icons |

**Consensus:** icons support text and never decorate; one set, one size (16px in dense UI),
coloured like the text beside them; no icons on headings; an icon-only button has a tooltip and an
accessible name.

---

## Where systems disagree

These are real differences between respected systems. The kit does not treat either side as
slop; it flags the *mix*, and the project records its choice in `.slop.json`.

**Pill chips or 4–8px chips.** Carbon tags (16px radius) and Primer labels are pills. Atlassian
uses 4px for lozenges and tags, Material chips use 8px, and Fluent offers circular, rounded and
square badges. The shape does not decide whether a screen looks professional. Consistency does:
one shape and one height per kind of label (Fluent says not to mix badge sizes in one context).
That is why tell S2 flags mixed shapes, not pills.

**"-" or words for an empty cell.** Cloudscape puts "-" in empty table cells, and dense tables in
many products do the same. Writing the absence in words ("Not set", "No description") reads
better in forms, detail panels and sparse tables, and survives screen readers better than a lone
dash. Tell L12 defaults to words because generated UIs scatter "—", "-" and "N/A" without a rule.
A product that chooses "-" for dense tables should allow L12 for `td` in `.slop.json` and use it
consistently.

**Pill buttons.** Material 3's filled button has a full (pill) radius. Primer uses 6px, Fluent
4px, and community "keep it normal" guides for admin UIs ask for 6–10px and no pills. In a dense
product UI, pill buttons next to square inputs and chips read as a template; in a Material-based
product they are simply the system. If you build on M3, allow S2 for buttons and say so.
