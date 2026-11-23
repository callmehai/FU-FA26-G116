"""Validate the Report 7 DOCX: opens in Word, TOC, page budget, template leftovers.

    python scripts/check_report7.py                 # report only
    python scripts/check_report7.py --update-toc    # also refresh the TOC / page numbers and save

Exit code 1 when any ERROR is printed:
  - check_docx.py reports a problem the template did not already have;
  - the document is over 150 pages (the school's limit; Word is the judge of pagination);
  - the table of contents lacks a heading, or lists one that is not in the body;
  - template leftovers remain (HOW TO WRITE boxes, EXAMPLE - boxes, [placeholders], sample products).
Needs Word (COM) on Windows; without it only the text checks run.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from pathlib import Path

import docx
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))
from report7_lib import PAGE_BUDGET, SOURCE, _text, is_heading, norm  # noqa: E402

LEFTOVER = [
    (re.compile(r"HOW TO WRITE THIS SECTION"), "HOW TO WRITE box"),
    (re.compile(r"^EXAMPLE\s*[—-]"), "EXAMPLE box"),
    (re.compile(r"<Group Name>|<Member name>|\[Project name\]|\[Fill |\[Provide |\[Show |\[Explain |\[List |\[Content #"),
     "template placeholder"),
    (re.compile(r"InventoryFlow|MedRecord|TalentHub|ShopEasy|SAP Business One|Zoho|Spring Boot|Candidate Self-Service"),
     "sample product / stack of the template"),
    (re.compile(r"\{\{|\}\}"), "{{placeholder}}"),
]

PS = r"""
param([string]$Path,[string]$Update)
$w = New-Object -ComObject Word.Application
$w.Visible = $false
$w.DisplayAlerts = 0
try {
  $d = $w.Documents.Open($Path)
  $d.Repaginate()
  if ($Update -eq '1') {
    foreach ($t in $d.TablesOfContents) { $t.Update() }
    $d.Repaginate()
    $d.Save()
  }
  "PAGES=" + $d.ComputeStatistics(2)
  foreach ($t in $d.TablesOfContents) { foreach ($p in $t.Range.Paragraphs) { "TOC=" + $p.Range.Text.Trim() } }
  $d.Close(0)
} finally { $w.Quit() }
"""


def word(path: Path, update: bool):
    with tempfile.NamedTemporaryFile("w", suffix=".ps1", delete=False, encoding="utf-8-sig") as f:
        f.write(PS)
    try:
        out = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", f.name,
             "-Path", str(path), "-Update", "1" if update else "0"],
            capture_output=True, text=True, timeout=300)
    finally:
        Path(f.name).unlink(missing_ok=True)
    pages, toc = None, []
    for line in out.stdout.splitlines():
        if line.startswith("PAGES="):
            pages = int(line[6:])
        elif line.startswith("TOC="):
            toc.append(line[4:])
    return pages, toc, out.stderr.strip()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("path", nargs="?", type=Path, default=SOURCE)
    ap.add_argument("--update-toc", action="store_true")
    args = ap.parse_args()
    errors: list[str] = []

    d = docx.Document(str(args.path))
    body = d.element.body

    # 1. leftovers
    for el in body.iter(qn("w:p")):
        t = _text(el).strip()
        for rx, why in LEFTOVER:
            if rx.search(t):
                errors.append(f"leftover ({why}): {t[:70]!r}")
                break

    # 2. headings vs TOC
    headings = [(_style(el), _text(el).strip()) for el in body.iterchildren() if is_heading(el) and _text(el).strip()]
    # (empty headings are template noise and listed below)
    empty = [1 for el in body.iterchildren() if is_heading(el) and not _text(el).strip()]
    if empty:
        errors.append(f"{len(empty)} empty heading paragraph(s)")

    # 3. Word
    pages, toc, err = word(args.path, args.update_toc)
    if pages is None:
        print(f"WARN  Word not available, skipped pagination/TOC ({err[:120]})")
    else:
        print(f"pages: {pages} / {PAGE_BUDGET}")
        if pages > PAGE_BUDGET:
            errors.append(f"{pages} pages, over the {PAGE_BUDGET}-page limit")
        toc_norm = [norm(re.sub(r"\s*\d+\s*$", "", re.sub(r"\t.*$", "", t))) for t in toc]
        body_norm = {norm(t) for s, t in headings if s.lower() in ("heading1", "heading2")}
        for t in toc_norm:
            if t and t not in body_norm and not t.startswith("table of contents"):
                errors.append(f"TOC lists a heading not in the body: {t[:60]!r}")
        for s, t in headings:
            if s.lower() in ("heading1", "heading2") and norm(t) not in toc_norm and not norm(t).startswith("table of contents"):
                errors.append(f"TOC misses heading: {t[:60]!r}" + ("" if args.update_toc else " (run --update-toc)"))
        print(f"TOC entries: {len(toc)}")
        for t in toc[:12]:
            print("   ", t)

    # 4. generic house checks (template has known failures: duplicate TableNormal, grid mismatches)
    r = subprocess.run([sys.executable, str(ROOT / "scripts" / "check_docx.py"), str(args.path)],
                       capture_output=True, text=True)
    known = ("duplicate styleId", "has 3 columns, the grid has", "has 1 columns, the grid has", "empty Heading3")
    for line in r.stdout.splitlines():
        if line.strip().startswith("-") and not any(k in line for k in known):
            errors.append("check_docx: " + line.strip())

    for e in errors[:60]:
        print("ERROR", e)
    if len(errors) > 60:
        print(f"... and {len(errors) - 60} more")
    print(f"{len(errors)} error(s)")
    sys.exit(1 if errors else 0)


def _style(el):
    from report7_lib import _style as s
    return s(el)


if __name__ == "__main__":
    main()
