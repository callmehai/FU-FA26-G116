"""Fill Report 7 (Final Project Document) from the repo.

Source of truth: docs/08-reports/README.md -- the DOCX in docs/08-reports/source/ is edited,
never a Markdown snapshot. Report 7 is the template of the school and only collects what Reports
1-6 already hold. Each section is rebuilt by one filler in scripts/report7_sections/ (files run in
name order, each exposing `fill(r)`), so a run is idempotent and sections can be written in
parallel. Class and sequence diagrams are NOT part of Report 7 (they stay in Report 4).

Usage:
    python scripts/fill_report7.py                        # dry run
    python scripts/fill_report7.py --output /tmp/R7.docx  # write a copy
    python scripts/fill_report7.py --write                # modify the source in place
    python scripts/fill_report7.py --only s00_diagrams    # run a subset of fillers
Then:
    python scripts/check_report7.py --update-toc          # TOC, page budget, leftovers
"""

from __future__ import annotations

import argparse
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from docx.oxml.ns import qn  # noqa: E402

from report7_lib import SOURCE, Report, _text  # noqa: E402

WP = "{http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing}docPr"


def load_fillers(only):
    out = []
    for f in sorted((HERE / "report7_sections").glob("s*.py")):
        if only and f.stem not in only:
            continue
        spec = importlib.util.spec_from_file_location(f.stem, f)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        out.append((f.stem, mod))
    return out


def finish_pictures(r: Report):
    """Unique drawing ids (python-docx restarts them) and alt text taken from the caption below."""
    n = 0
    for para in r.body.iter(qn("w:p")):
        for dp in para.iter(WP):
            n += 1
            dp.set("id", str(n))
            nxt = para.getnext()
            cap = _text(nxt).strip() if nxt is not None and nxt.tag == qn("w:p") else ""
            if cap.startswith("Figure ") and not dp.get("descr"):
                dp.set("descr", cap)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", type=Path, default=SOURCE)
    ap.add_argument("--output", type=Path, help="write a copy here")
    ap.add_argument("--write", action="store_true", help="modify the source in place")
    ap.add_argument("--only", nargs="*", help="filler names to run, e.g. s00_diagrams")
    args = ap.parse_args()

    r = Report(args.source)
    for name, mod in load_fillers(args.only):
        print(f"filler {name}")
        mod.fill(r)
    finish_pictures(r)
    print(f"figures in document: {len(list(r.body.iter(qn('w:drawing'))))}")
    target = args.source if args.write else args.output
    if target is None:
        print("dry run: nothing written (use --output or --write)")
        return
    r.doc.save(str(target))
    print(f"wrote {target}")


if __name__ == "__main__":
    main()
