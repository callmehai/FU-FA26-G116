---
name: minimize-text
description: Cut the words on a TrustCabinet screen down to what people scan for, the way AI-eCabinet does — one line per row, chips and icons instead of sentences, and the secondary facts (who, exact time, IDs, explanations) moved into hover/focus tooltips. Use when building or reviewing any screen, and whenever the user says a screen is "nhiều chữ", "rối", "text tràn lan", "gọn lại", "minimize text", or shows an eCab screenshot next to ours. Pair with frontend-ui-design (what a screen must do) and verify-ui-in-browser (prove it).
---

# Minimize text

A screen is read in a glance, not word by word. Every line that is not what the reader came for
pushes what they came for further away. AI-eCabinet looks calm because each row says one thing and
everything else waits behind a hover. Do the same.

Words are moved or removed, never information: what leaves the screen must still be one hover,
focus or click away, and must still reach a screen reader.

## 1. Look at eCab first

Before writing or changing a screen, open the same screen in `~/Developer/ai-ecabinet/clients/web`
(`git pull` first) and count its words per row. If eCab says it in a chip and a time, we do not say
it in two lines of text. Port its pattern rewritten for our stack (`data-tooltip`, `Icon`, our CSS
tokens), never by pasting its files.

## 2. Sort every piece of text on the screen

Write the screen's text down and give each piece one of four places:

| Kind | Example | Where it goes |
|---|---|---|
| **Must read** — the reader acts on it or would be hurt by missing it | a rejection reason, a validation error, a consent or deletion notice, "Bạn có tham dự cuộc họp này?" | Stays in view, as short as it can be |
| **Scan** — what the reader looks for in a list | the event ("Duyệt"), the status, the time, the title | Stays in view as a chip, a badge, a number or one short line |
| **On demand** — true and sometimes wanted | who did it, who it went to, the exact timestamp of a relative time, the full name behind initials, what an icon button does | A tooltip (`data-tooltip`) on the thing it describes |
| **Redundant** — says again what the screen already shows | a hint that repeats the label, "Danh sách các cuộc họp" under a header that says so, a summary of errors already shown on their rows, a legend for colours that carry their own label | Delete |

When unsure between "scan" and "on demand", ask: would the reader of *this* screen look for it
in more than one row in ten? If not, it goes behind a hover.

## 3. Patterns

- **One line per row.** A list or timeline row is `[marker] [chip or title] ··· [time]`. A second
  line is only for must-read content (a rejection reason), never for metadata.
  *History timeline:* "Tạo cuộc họp / Nguyễn Thư Ký 2 · 00:07 22/10/2026 / Người duyệt: Trần Chủ
  Toạ" became `● Gửi duyệt ······ 00:07 22/10/2026`, with "Người thực hiện: … / Người duyệt: …" in
  the time's tooltip.
- **A chip beats a sentence.** States, roles, answers ("Tham dự", "Chủ tọa") are chips coloured by
  meaning; the colour says it before the word is read.
- **Icon buttons with a tooltip** for secondary actions whose icon is well known (edit, delete,
  invite by email, add person, collapse). Keep a text label on the **one** primary action of the
  screen and on destructive confirms. *Attendees toolbar:* two labelled buttons became ✉️ and 👤+
  with "Mời khách ngoài hệ thống (qua email)" / "Thêm người dự họp" on hover.
- **Errors sit where they happened, once.** A field or row error is one line on that field or
  row; no summary line repeating it (MSG277: a red bar on the row and one sentence, nothing below
  the form).
- **A question is a title, not a paragraph.** "Bạn có tham dự cuộc họp này?" plus the two buttons;
  the rules about answering once go in the docs, not on the bar.
- **Empty and loading states are one sentence plus the next step**, never an explanation of why.
- **Labels are nouns, buttons are verbs, both short:** "Mời", "Lưu", "Giao Chủ tọa", not "Gửi lời
  mời tham dự ngay".

## 4. How to put text behind a hover here

- Add `data-tooltip="…"` to the element. `components/TooltipLayer.tsx` (mounted once in the app)
  shows it after 400 ms on hover and at once on keyboard focus, in the top layer so tables and
  dialogs never clip it. `\n` makes a new line. Never use the native `title` attribute.
- The element must be reachable without a mouse: a button or link already is; anything else
  (`<time>`, a chip, an avatar) gets `tabIndex={0}`.
- The tooltip is visual only. Keep the accessible name complete: `aria-label` on an icon button,
  or a `.visually-hidden` span inside the element carrying the same words.
- **Never** hide must-read content behind a hover: phones have none, and a reader who does not
  know to hover never finds it. If people need it to act, it stays in view.

## 5. Check

- Before/after pair of the screen (verify-ui-in-browser §3); the "after" should be visibly
  emptier, with nothing the reader needs missing.
- Measure "one line per row": every row's height equals the chip's height.
- Hover **and** keyboard-focus each tooltip target and assert the tooltip text in Playwright
  (`page.locator('.tooltip')`).
- Read the "after" as the least experienced role would see it: if they cannot tell what to do
  next, a must-read line was cut — put it back.
