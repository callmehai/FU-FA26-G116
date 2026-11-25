---
name: doc-audit
description: Audit one slice of the TrustCabinet submission documents (a Report, a Feature's UCs, a diagram family, the slides) against the code that runs, the way a reviewer for the acceptance and the thesis defence would — extract every claim (text, table, number, diagram, screenshot), verify it against the code, Jira, the running app and the other documents, judge every image, and write a findings ledger a human then confirms or rejects before anything is fixed. Use when asked to "audit tài liệu", "doc khớp code chưa", "rà report", "kiểm tra diagram/ảnh", "sẵn sàng nghiệm thu / bảo vệ", and again after the documents are corrected.
---

# Audit the documents against the code

The auditor is **independent** and **read-only**. It checks what the documents *claim* against what the code and the
running app *do*. It edits no document, no diagram and no code. It produces a findings ledger; a human decides; only
then does a separate step fix what the human confirmed.

Two layers run, in this order, on one **slice** at a time:

- **Layer A, text and numbers:** every statement a council member could test (a UC step, a rule, a message, a count,
  a role, a status name, a table row).
- **Layer B, images:** every diagram and screenshot (does it show what the document says it shows, and is it the
  current version of the thing it depicts).

Mechanical checks that a script can do alone (dead links, `MSGnn` in the catalogue, `ArchitectureTests`) are not this
skill's job; run them first and start the audit from a green state: `python scripts/trace_br.py --check`,
`python scripts/test_reports/verify_reports.py`, `python scripts/test_reports/integration_cases.py check`.

## 0. Who runs it

Run in a **fresh subagent** or session given only the slice and this skill. An agent that wrote the code or edited the
document carries its own reading of it. The auditor may read code to find the truth, but never "fixes" the document
to match what it assumes the author meant.

## 1. Pick the slice

One slice per run, small enough to judge by eye. Never "audit everything" in one pass.

| Slice | Documents (canonical source) | Truth to check against |
|---|---|---|
| `R1` Report 1 | `docs/08-reports/source/Report_1_*.docx` | scope in AGENTS.md "Status", `scopeData.ts`, Jira epics |
| `R2` Report 2 | `source/Report_2_*.docx`, weekly report | git history, Jira sprints, meeting minutes |
| `R3:FE-xx` one Feature of Report 3 | `source/Report_3_*.docx` (read the snapshot `Report-3_*.md` and `uc-map.html`) | code, `docs/03-api/`, MSG catalogue, running app |
| `R3:NFR` non-functional | Report 3 chapter IV | measurable facts in code, deploy, config |
| `R4:<area>` Design spec | `source/Report-4_*.docx`, `docs/06`–`16` | the four projects, `ArchitectureTests`, ERD, migrations |
| `R5:<n>` Test documentation | `test-reports/*.xlsx`, `cases/FE-xx/*.json` | real tests, `.trx`, `tests/qa/`, the code under test |
| `DG:<family>` Diagram family | `docs/06-sequencediagram` … `16-architecture-diagram`, `wireframe/` | code and the screens |
| `SL` Slides | `source/TrustCabinet_Report.pptx` | every other slice, and the demo |

Generated Markdown (`Report-*.md`, `05-use-case-specifications.md`, `06-business-rules.md`) may be **read** to find a
claim fast, but the finding cites the DOCX location, because the DOCX is the only thing that gets fixed.
Features and UCs the PO dropped (AGENTS.md "Status") must not appear as built anywhere; one that does is a Blocker.

## 2. Extract claims (before reading any code)

Read the slice and write a numbered list of **claims**, each one checkable and each with its location
(`file › section/UC/table/figure number › page or line`). Kinds:

`flow` (a UC step or branch) · `rule` (a BR) · `message` (an MSG and its text) · `actor/role/permission` ·
`state` (a status name or a transition) · `data` (an entity, field, enum, key type) · `number` (count, size, limit,
date) · `structure` (a layer, a dependency direction, a project) · `scope` (built / dropped / out of scope) ·
`image` (figure N shows X) · `trace` (this requirement is covered by that test).

Write claims from the document alone, so the code cannot colour what is asked. Skip prose that asserts nothing.

## 3. Layer A: verify each claim

For every claim find the truth, in this order of authority: **running app and API** > **code and tests** > Jira
(description and comments) > other documents. Record the evidence as `path:line`, an API call and its answer, or a
screenshot. Then give one verdict:

| Verdict | Meaning |
|---|---|
| `MATCH` | the truth says the same, with evidence |
| `DRIFT` | the document says something the truth contradicts (document is wrong or stale) |
| `CODE-GAP` | the document says it is built, the code does not do it |
| `UNDOCUMENTED` | the code does something a reader of this slice would need to know, and the slice is silent |
| `CONTRADICTION` | two documents disagree with each other (name both locations) |
| `UNVERIFIABLE` | cannot be checked here; say the exact step a human must do. Never a way to skip a hard claim |

How to check by kind is in [criteria.md](criteria.md). Rules that apply everywhere:

- A flow is verified by **tracing the call chain** (endpoint → service → port → repository) and by exercising it on
  the running app when it is user-visible, not by reading the UC and nodding.
- A number is recounted, never copied. A coverage or pass count is recomputed from the `.trx` or the workbook.
- A claim that holds in the code but not on the screen the user sees is `DRIFT` against the screen.
- AGENTS.md says BA settles documents last. That is for PR review. In an acceptance audit a stale document **is** a
  finding; the ledger says which side to change (see §6).
- Do not invent a rule: a threshold or behaviour in no ticket, UC or BR is a question for a human.

## 4. Layer B: verify each image

Open **every** image in the slice with the Read tool and look at it; a file that exists is not a verdict. For each:

1. **What does the document say it shows?** The caption, the paragraph that cites it, the UC it belongs to.
2. **Is it the current artifact?** A diagram PNG must be an export of the current `.drawio` or `.txt` source
   (compare the source's last commit with the image's; re-export with `scripts/export-diagrams.js`,
   `scripts/export_sequence.py`, `scripts/sync_sequence_drawio_png.py` into the scratchpad and compare). A screenshot
   must match the running app: take a fresh one with `tests/qa` or `verify-ui-in-browser` and compare labels,
   buttons, columns, statuses and the role shown.
3. **Does its content match the truth?** Participants and messages against the call chain; classes and attributes
   against the entities; states and transitions against the enum and the policy; screen elements against the UI;
   actors and associations against the UC list and the permission table.
4. **Is it fit to submit?** Legible when printed at report width, not cropped or blurred, no overlapping labels or
   dangling arrows, UML notation correct (arrow kind, multiplicity, stereotype), consistent names with the text,
   no secrets or real personal data (emails, tokens, internal URLs), no browser error or debug UI in a screenshot.
5. **Is it numbered and cited?** Figure number in the caption, referenced from the text, and in the right section.

Each image gets one row in the ledger: `figure › claim › source › current? › content matches? › fit?` and a verdict
from §3, plus `STALE` (matches an older version of the source) where it applies.

## 5. The ledger

Write `tests/doc-audit/out/<slice>/<date>-<commit>/findings.md` (create the folder; it is working output, do not
commit it unless the user asks). A summary table first, then one block per finding:

```
## F-07  DRIFT  Major   R3:FE-09 › UC-48 › step 5 (DOCX p.212)
Claim:    "Chủ toạ gửi biên bản cho Thư ký duyệt"
Truth:    Secretary submits, Chair approves. MinutesService.cs:142, POST /api/minutes/{id}/submit 200 as secretary
Evidence: shots/07-secretary-submit.png ; api/07.json
Side:     document   (alt: code)
Fix:      change step 5 actor to Thư ký; BR-61 already says so
Human:    [ ] confirm   [ ] reject   [ ] change severity ___   note: ______
```

Severity: **Blocker** (a dropped feature shown as built, a claim the council can falsify live, a diagram that depicts
a different system, a test report whose numbers are wrong) · **Major** (a wrong flow, rule, role, message or state;
a stale screenshot or diagram a council member will notice) · **Minor** (wording, numbering, a cosmetic image defect).
The summary table also lists: claims checked, `MATCH` count, and every `UNVERIFIABLE` with the human step.
Keep evidence beside the ledger: `shots/`, `api/`, `reexports/`.

## 6. Human gate (the audit stops here)

Hand the user the ledger path and a short list of what to look at first (Blockers, then Majors, then
`UNVERIFIABLE`). **Do not fix anything yet.** The user fills each `Human:` line. Then:

- `confirm` goes to the fix list; `reject` stays in the ledger with the user's reason and is never re-raised for the
  same text; a changed severity wins.
- For each confirmed finding the user (or the ledger's `Side:`) says whether the **document** or the **code** changes.
  Code changes follow the normal workflow (ticket, PR). The auditor never changes code to fit a document.

## 7. Fix path (separate step, only when asked)

Documents are fixed in their **source**, never in generated output: edit the DOCX in `docs/08-reports/source/`
(or the `.drawio` / `.txt` for diagrams), then `check_docx.py`, `sync_reports.py`, re-export the images, review
`git diff`, per [docs/08-reports/README.md](../../../docs/08-reports/README.md) and `DRAWIO_EDITING_RULES.md`.
Edit only the confirmed spots. Push to Drive only when the user asks.

## 8. Re-audit

After a fix, re-run the **same slice** with the previous ledger as input: re-verify each confirmed finding first
(it closes only on `MATCH` with new evidence on the new commit), then re-run the whole slice, since an edit can
break a neighbour (a renamed state changes four diagrams). Record `closed in <commit>` in the ledger.

## 9. Cross-slice checks (run once all slices are clean)

Traceability end to end: requirement (Jira) → UC → BR/MSG → endpoint → test case (5.2, 5.2.2) → result → slide.
A row with a missing cell is a gap. Terms and counts the council will compare across documents must be identical in
Reports 1–5, the slides and the demo: UC count, role names, status names, the list of what was dropped.

## 10. Never

- Never edit a document, diagram, snapshot or code during the audit; never "fix" the generated Markdown.
- Never mark `MATCH` from reading the document alone, or from an image's existence; open it.
- Never weaken a claim to make it match, and never merge findings to shorten the ledger.
- Never put a password, token or real personal data in the ledger or its evidence.
- Never make a paid or outside call (AI, email) to verify a claim; check up to the call and mark the rest
  `UNVERIFIABLE`.
