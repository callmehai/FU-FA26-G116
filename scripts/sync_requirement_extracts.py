"""Regenerate the Report 3 extracts under docs/01-requirements/ from the snapshot.

Report 3 (`docs/08-reports/Report-3_Software_Requirement_Specification.md`) is the
source. Two files are verbatim extracts of it:

* `05-use-case-specifications.md` — section II, Use Case Specifications,
* `06-business-rules.md` — section V.1, Business Rules (two columns, `ID | Rule`,
  BR-001…BR-153 since ADR-0079). This file is generated whole; old ids and retired
  rules live in `09-bang-anh-xa-br-dot-1.md`, not here.

The use case extract keeps things the report does not have, and this script never
touches them:

* the Vietnamese grouping note under a parent UC (`_UC gom — đặc tả nằm ở …_`),
* the `Sequence Diagram:` link above or below each UC table,
* the `Placeholder only` line for a UC the report does not specify yet.

Run:
    python scripts/sync_requirement_extracts.py --check   # list what is out of sync
    python scripts/sync_requirement_extracts.py           # rewrite the two extracts

A UC that the report no longer contains (e.g. UC-62/UC-63 dropped by the PO)
is reported as `not in report: <UC>` and is NEVER deleted by a plain sync —
the snapshot README forbids hand edits, and silent deletion would hide a
report/extract mismatch. To remove such UCs after confirming the report is
the source of truth, first dry-run with `--check`, then confirm explicitly:

    python scripts/sync_requirement_extracts.py --prune-missing
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPORT = ROOT / "docs" / "08-reports" / "Report-3_Software_Requirement_Specification.md"
SPEC = ROOT / "docs" / "01-requirements" / "05-use-case-specifications.md"
RULES = ROOT / "docs" / "01-requirements" / "06-business-rules.md"

FIELDS = (
    "Primary Actors",
    "Secondary Actors",
    "Description",
    "Preconditions",
    "Postconditions",
    "Normal Flow",
    "Alternative Flows",
    "Exception Flows",
)

UC_HEADING = re.compile(r"^(#{3,6}) (UC-\d+[a-z]?) – (.+)$")
REPORT_HEADING = re.compile(r"^(#{4,5}) .*?\b(UC-\d+[a-z]?) – (.+)$")
FE_HEADING = re.compile(r"^## (FE-\d+) — (.+)$")
REPORT_FE_HEADING = re.compile(r"^### \d+\. (FE-\d+) – (.+)$")
RULE_ROW = re.compile(r"^\| `?(BR-\d{3})`? \| (.*) \|$")
RULE_HEADER = "| ID | Rule |"


def is_sub(uc: str) -> bool:
    return len(uc) > 5


def cells(line: str) -> list[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def parse_report(lines: list[str]) -> tuple[list[tuple[str, str, str]], dict[str, dict[str, str]]]:
    """Return [(uc_id, title, fe_id)] in report order, plus {uc_id: {field: text}}."""
    start = next(i for i, line in enumerate(lines) if line.startswith("## II. Use Case Specifications"))
    end = next(i for i, line in enumerate(lines) if line.startswith("## III."))
    order: list[tuple[str, str, str]] = []
    fields: dict[str, dict[str, str]] = {}
    fe = ""
    current: str | None = None
    for line in lines[start:end]:
        fe_match = REPORT_FE_HEADING.match(line)
        if fe_match:
            fe = fe_match.group(1)
            current = None
            continue
        uc_match = REPORT_HEADING.match(line)
        if uc_match:
            current = uc_match.group(2)
            order.append((current, uc_match.group(3).strip(), fe))
            fields[current] = {}
            continue
        if current is None or not line.startswith("|"):
            continue
        row = cells(line)
        for index in range(0, len(row) - 1, 2):
            label, value = row[index], row[index + 1]
            if label in FIELDS:
                fields[current][label] = value
    return order, fields


def render_table(values: dict[str, str]) -> list[str]:
    out = ["| Mục | Nội dung |", "|---|---|"]
    for field in FIELDS:
        if field in values:
            out.append(f"| **{field}** | {values[field]} |")
    return out


def parse_spec(lines: list[str]) -> list[tuple[int, int, str, str]]:
    """Return [(start, end, uc_id, fe_id)] for every UC block, 0-based, end exclusive."""
    blocks: list[tuple[int, int, str, str]] = []
    fe = ""
    headings = [(i, line) for i, line in enumerate(lines) if FE_HEADING.match(line) or UC_HEADING.match(line)]
    for position, (index, line) in enumerate(headings):
        fe_match = FE_HEADING.match(line)
        if fe_match:
            fe = fe_match.group(1)
            continue
        uc_match = UC_HEADING.match(line)
        assert uc_match
        end = len(lines)
        for later_index, later_line in headings[position + 1:]:
            if FE_HEADING.match(later_line) or UC_HEADING.match(later_line):
                end = later_index
                break
        blocks.append((index, end, uc_match.group(2), fe))
    return blocks


def block_table_range(lines: list[str], start: int, end: int) -> tuple[int, int] | None:
    """Locate the contiguous run of table lines inside a UC block."""
    for index in range(start, end):
        if lines[index].startswith("|"):
            last = index
            while last + 1 < end and lines[last + 1].startswith("|"):
                last += 1
            return index, last + 1
    return None


def placeholder_range(lines: list[str], start: int, end: int) -> tuple[int, int] | None:
    for index in range(start, end):
        if lines[index].strip().startswith("Placeholder only"):
            return index, index + 1
    return None


def slug(title: str) -> str:
    text = title.lower()
    text = re.sub(r"[^a-z0-9 \-]", "", text)
    return text.replace(" ", "-")


def rebuild_toc(lines: list[str], blocks: list[tuple[int, int, str, str]]) -> list[str]:
    first_fe = next(i for i, line in enumerate(lines) if FE_HEADING.match(line))
    toc_start = next(i for i, line in enumerate(lines) if line.startswith("## Mục lục"))
    fe_titles = {FE_HEADING.match(line).group(1): FE_HEADING.match(line).group(2) for line in lines if FE_HEADING.match(line)}
    grouped: dict[str, list[tuple[str, str]]] = {}
    for start, _end, uc, fe in blocks:
        title = UC_HEADING.match(lines[start]).group(3).strip()
        grouped.setdefault(fe, []).append((uc, title))
    toc = ["## Mục lục", ""]
    for fe, title in fe_titles.items():
        toc.append(f"### {fe} — {title}")
        toc.append("")
        for uc, uc_title in grouped.get(fe, []):
            indent = "  " if is_sub(uc) else ""
            toc.append(f"{indent}- [{uc} — {uc_title}](#{slug(f'{uc} – {uc_title}')})")
        toc.append("")
    return lines[:toc_start] + toc + ["---", ""] + lines[first_fe:]


def sync_use_cases(check: bool, prune_missing: bool = False) -> int:
    report_lines = REPORT.read_text(encoding="utf-8").splitlines()
    order, report_fields = parse_report(report_lines)
    lines = SPEC.read_text(encoding="utf-8").splitlines()
    blocks = parse_spec(lines)
    present = {uc for _s, _e, uc, _fe in blocks}
    report_ids = [uc for uc, _t, _fe in order]

    drift: list[str] = []
    for uc, _title, _fe in order:
        if uc not in present:
            drift.append(f"missing in extract: {uc}")
            continue
        if not report_fields.get(uc):
            continue
        start, end, _uc, _fe = next(block for block in blocks if block[2] == uc)
        span = block_table_range(lines, start, end)
        if span is None:
            drift.append(f"no table yet: {uc}")
            continue
        if lines[span[0]:span[1]] != render_table(report_fields[uc]):
            drift.append(f"table out of date: {uc}")
    for uc in sorted(present - set(report_ids)):
        drift.append(f"not in report: {uc}")

    for item in drift:
        print(item)
    if check:
        return 1 if drift else 0

    # 0. Prune UCs the report no longer contains — ONLY the ones listed above
    # as "not in report", and only with the explicit --prune-missing flag
    # (the --check run is the dry-run). A plain sync never deletes.
    if prune_missing:
        for uc in sorted(present - set(report_ids)):
            block = next(block for block in blocks if block[2] == uc)
            print(f"prune: {uc}")
            del lines[block[0]:block[1]]
            blocks = parse_spec(lines)
        # A deleted block took its trailing blank lines with it; keep one
        # blank line before any heading that now directly follows text.
        for index, line in enumerate(lines):
            if UC_HEADING.match(line) and index > 0 and lines[index - 1].strip():
                lines[index:index] = [""]
                blocks = parse_spec(lines)

    # 1. Replace the table of every UC the report specifies.
    for uc, _title, _fe in order:
        if uc not in present or not report_fields.get(uc):
            continue
        start, end, _uc, _fe = next(block for block in blocks if block[2] == uc)
        table = render_table(report_fields[uc])
        span = block_table_range(lines, start, end)
        if span:
            lines[span[0]:span[1]] = table
        else:
            span = placeholder_range(lines, start, end)
            if span:
                lines[span[0]:span[1]] = table
        blocks = parse_spec(lines)

    # 2. Update the heading of every UC to the report's title.
    for uc, title, _fe in order:
        if uc not in {block[2] for block in blocks}:
            continue
        start, _end, _uc, _fe = next(block for block in blocks if block[2] == uc)
        match = UC_HEADING.match(lines[start])
        assert match
        lines[start] = f"{match.group(1)} {uc} – {title}"
        blocks = parse_spec(lines)

    # 2b. Update every FE section heading to the report's title. The report is
    # the source; the old code rebuilt the ToC from the extract's own
    # headings, so FE-12 stayed "Meeting Minutes & Digital Signing" after the
    # report renamed it "Meeting Minutes & Approval".
    report_fe_titles: dict[str, str] = {}
    for line in report_lines:
        fe_match = REPORT_FE_HEADING.match(line)
        if fe_match:
            report_fe_titles.setdefault(fe_match.group(1), fe_match.group(2).strip())
    for index, line in enumerate(lines):
        fe_match = FE_HEADING.match(line)
        if fe_match and report_fe_titles.get(fe_match.group(1)):
            lines[index] = f"## {fe_match.group(1)} — {report_fe_titles[fe_match.group(1)]}"
    blocks = parse_spec(lines)

    # 3. Add the UCs the report has and the extract lacks, at the end of their Feature.
    fe_titles = report_fe_titles
    for uc, title, fe in order:
        if uc in {block[2] for block in blocks}:
            continue
        blocks = parse_spec(lines)
        body: list[str] = [f"{'####' if is_sub(uc) else '###'} {uc} – {title}", ""]
        if report_fields.get(uc):
            body += render_table(report_fields[uc])
        else:
            body += [f"_UC gom — đặc tả nằm ở các UC con {uc}a, {uc}b… bên dưới._"]
        body += [""]
        same_fe = [block for block in blocks if block[3] == fe]
        if same_fe:
            end_of_block = same_fe[-1][1]
            if is_sub(uc):
                # A sub-UC belongs with its parent, not at the end of the Feature: insert it after
                # the last sub-UC already there (UC-39d after UC-39c, not after UC-42).
                parent = uc[:5]
                siblings = [b for b in blocks if b[3] == fe and b[2].startswith(parent) and b[2] != parent]
                if siblings:
                    end_of_block = siblings[-1][1]
                else:
                    anchor = next((b for b in blocks if b[3] == fe and b[2] == parent), None)
                    if anchor:
                        end_of_block = anchor[1]
            insert_at = end_of_block
            separator: list[str] = []
            while insert_at > 0:
                previous = lines[insert_at - 1].strip()
                if previous == "---":
                    separator = ["---", ""]
                    insert_at -= 1
                elif not previous:
                    insert_at -= 1
                else:
                    break
            del lines[insert_at:end_of_block]
            lines[insert_at:insert_at] = [""] + body + separator
        else:
            fe_line = next((i for i, line in enumerate(lines) if line.startswith(f"## {fe} —")), None)
            if fe_line is not None:
                lines[fe_line + 1:fe_line + 1] = [""] + body
            else:
                # A brand-new Feature has no section in the extract yet (first
                # sync that introduces it): append the section at the end.
                fe_title = fe_titles.get(fe, fe)
                end = len(lines)
                while end > 0 and not lines[end - 1].strip():
                    end -= 1
                lines[end:end] = ["", f"## {fe} — {fe_title}", ""] + body
        blocks = parse_spec(lines)

    # 4. Rebuild the table of contents from the headings now in the file.
    lines = rebuild_toc(lines, parse_spec(lines))
    SPEC.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8", newline="\n")
    print(f"Synced {SPEC.relative_to(ROOT)}")
    return 0


def parse_report_rules(lines: list[str]) -> list[tuple[str, str]]:
    """Return the BR rows of section V.1 in report order, as (id, rule)."""
    start = next(i for i, line in enumerate(lines) if line.startswith("### 1. Business Rules"))
    end = next(i for i, line in enumerate(lines) if line.startswith("### 2. System Messages"))
    rows: list[tuple[str, str]] = []
    for line in lines[start:end]:
        match = RULE_ROW.match(line)
        if match:
            rows.append((match.group(1), match.group(2)))
    return rows


# The whole extract is generated: since ADR-0079 there is no hand-written part left. Old ids, the
# rules retired before the renumbering and where each old rule went live in the mapping table.
RULES_HEAD = """# Danh mục Quy tắc Nghiệp vụ (Business Rules) — TrustCabinet

> **GENERATED — DO NOT EDIT.** Bản trích nguyên văn Report 3 §V.1. Sửa DOCX trong
> [`docs/08-reports/source/`](../08-reports/source/README.md) rồi chạy
> `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
> (hoặc `python scripts/sync_requirement_extracts.py`). Chi tiết: [08-reports/README.md](../08-reports/README.md).

Có **{count} BR**, đánh số `BR-001` → `BR-{last}` theo [ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md)
và [kế hoạch gộp BR](08-ke-hoach-gop-business-rules.md). Mỗi BR một câu, một quyết định nghiệp vụ; giao diện, kiểm tra
dữ liệu nhập và giới hạn kỹ thuật nằm trong UC Specification hoặc mục IV (NFR) của Report 3. Mã `BR` độc lập với mã
thông báo `MSG`.

## Quy ước

- UC Specification và Sequence Diagram dẫn chiếu BR ID tại bước áp dụng, không chép lại nội dung.
- Mã cũ (`BR-01` → `BR-468`) đã ngừng dùng; tra mã mới hoặc nơi chuyển đến ở
  [09-bang-anh-xa-br-dot-1.md](09-bang-anh-xa-br-dot-1.md). Bảng đó gồm cả các BR đã ngừng trước đợt đánh số lại.
  ADR, note và test report cũ giữ mã cũ.

## Danh mục BR

{header}
|---|---|
"""


def render_rules(rows: list[tuple[str, str]]) -> str:
    head = RULES_HEAD.format(count=len(rows), last=rows[-1][0][3:] if rows else "000", header=RULE_HEADER)
    return head + "".join(f"| `{rule_id}` | {rule} |\n" for rule_id, rule in rows)


def sync_rules(check: bool) -> int:
    rows = parse_report_rules(REPORT.read_text(encoding="utf-8").splitlines())
    expected = render_rules(rows)
    current = RULES.read_text(encoding="utf-8") if RULES.is_file() else ""

    drift: list[str] = []
    if current != expected:
        have = {m.group(1): line for line in current.splitlines() if (m := RULE_ROW.match(line))}
        wanted = dict(rows)
        for rule_id in sorted(set(wanted) - set(have), key=lambda value: int(value[3:])):
            drift.append(f"missing BR: {rule_id}")
        for rule_id in sorted(set(have) - set(wanted), key=lambda value: int(value[3:])):
            drift.append(f"BR not in report: {rule_id}")
        for rule_id in sorted(set(wanted) & set(have), key=lambda value: int(value[3:])):
            if have[rule_id] != f"| `{rule_id}` | {wanted[rule_id]} |":
                drift.append(f"BR text out of date: {rule_id}")
        if not drift:
            drift.append("BR extract header or order out of date")

    for item in drift:
        print(item)
    if check:
        return 1 if drift else 0
    RULES.write_text(expected, encoding="utf-8", newline="\n")
    print(f"Synced {RULES.relative_to(ROOT)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="report drift without writing")
    parser.add_argument(
        "--prune-missing",
        action="store_true",
        help="delete extract UC blocks the report no longer has (dry-run first with --check)",
    )
    args = parser.parse_args()
    if args.prune_missing and args.check:
        parser.error("--prune-missing writes files and cannot be combined with --check")
    status = sync_use_cases(args.check, args.prune_missing) | sync_rules(args.check)
    if args.check and not status:
        print("UP TO DATE: both extracts match Report 3.")
    return status


if __name__ == "__main__":
    sys.exit(main())
