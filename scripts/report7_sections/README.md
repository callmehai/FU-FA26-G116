# Report 7 section fillers

Report 7 (`docs/08-reports/source/Report-7_Final Project Document.docx`) is the school's template. It only
collects what Reports 1–6 and the repo already hold. Each **filler** here rebuilds one part of it; `scripts/fill_report7.py`
runs them in file-name order. One agent / person owns one filler file and touches nothing else.

## Rules for every filler

- A filler is `sNN_name.py` with `def fill(r: Report)`; helpers are in `scripts/report7_lib.py`
  (`find_heading`, `replace_section`, `replace_until`, `insert_after`, `text`, `bullets`, `table`, `heading`, `figure`,
  `replace_first_image`). Read `s00_diagrams.py` for a worked example.
- **Idempotent.** Locate the section by its heading text, then replace everything under it
  (`replace_section` / `replace_until`). Never append blindly; a second run must give the same document.
- **Remove the template boxes** of your section: every `HOW TO WRITE THIS SECTION` table, every `EXAMPLE — …` table and
  every `[bracketed instruction]`, and the sample content of the template (InventoryFlow, MedRecord, TalentHub, Spring Boot…).
  `python scripts/check_report7.py` lists what is left.
- **English** text (the template and Reports 3/4 are English). Keep Vietnamese UI labels and terms as they are.
- **Facts come from the repo**, not from memory: Reports 1–6 in `docs/08-reports/source/*.docx` (read with python-docx),
  `docs/**`, the code, Jira export notes. Do not invent numbers, dates, names or results. If a fact is not in the repo, leave a
  one-line `TODO(<owner>): <what is missing>` paragraph in the DOCX and say so in your report.
- **Language of Chapter 3 (SRS) is business language:** no vendor or technology names (no Resend, SMTP, endpoint paths).
  Chapter 4 may name the technology.
- **Page budget** for the whole document is 150 (Word counts). Per part, see the table below. Prefer a compact table over prose;
  do not paste a whole Report section when a summary table plus a pointer ("full text in Report 3 §x") is enough.
- Class and sequence diagrams: include only 2–4 representative ones (owner decision 23/11/2026, following the school template depth); the full set stays in Report 4. State the source of each diagram.
- Chapter 3 §3 needs one wireframe/mockup image per key screen (the school template's Content #1); use `docs/wireframe/screens/`.
- Do not touch `s00_diagrams.py`, the lib, or other fillers; do not edit the source DOCX directly; do not `--write`; do not commit.

## How to test your filler

```powershell
python scripts/fill_report7.py --only s00_diagrams sNN_yours --output $env:TEMP\r7-sNN.docx
python scripts/check_report7.py $env:TEMP\r7-sNN.docx      # leftovers of YOUR section must be gone; pages within budget
```

Leftovers in other sections are expected until their filler exists. Look at the rendered pages (Word → PDF) for your section.

## Ownership and budget

| Filler | Template part | Main sources | Pages |
|---|---|---|---|
| `s10_front_ch1.py` | cover members, Acknowledgements, Definitions and Acronyms, Chapter 1 (1–6) | Report 1, Report 2 cover, `docs/00-overview`, ADRs | ≤ 14 |
| `s20_ch2_plan.py` | Chapter 2 (objectives, scope, methodology, assumptions, schedule, risks, RACI) | Report 2, weekly report xlsm, meeting minutes | ≤ 10 |
| `s30_ch3_catalogue.py` | Ch.3 §1.3 entity table, §1.4.1 actors, §1.4.3 UC inventory, §1.5.1b screens, §1.5.2 external API, §1.5.3 jobs | Report 3, `docs/01-requirements/00-master-use-case-inventory.md`, code | ≤ 22 |
| `s31_ch3_spec.py` | Ch.3 §2 UC specifications (6–8 key UC), §3 functional specs (key screens), §4 NFR, §5 business rules / messages / other | Report 3, `docs/01-requirements/`, `docs/wireframe/`, message catalogue | ≤ 30 |
| `s40_ch4_design.py` | Ch.4 §1 text, §2 table descriptions, §4 NFR design, §5 other | Report 4, `docs/02-design`, ADRs, code | ≤ 22 |
| `s50_ch5_ch6.py` | Chapter 5 delivery package, Chapter 6 conclusion | Report 5.x workbooks / `docs/08-reports/test-reports`, git, Report 2 | ≤ 14 |

Front matter, TOC and figures (`s00_diagrams.py`) take about 40 pages today.
