"""Rewrite UC / BR / MSG references everywhere, from mapping/*.csv, in ONE pass (no double renaming).

Usage
  python scripts/renumber/apply_mapping.py            # dry run: counts per file + mapping/orphans.log
  python scripts/renumber/apply_mapping.py --write    # rewrite files
  python scripts/renumber/apply_mapping.py --docx     # also handle DOCX/PPTX in docs/08-reports/source (with --write to save)
  python scripts/renumber/apply_mapping.py --adrs     # also rewrite accepted ADRs (off by default)

Not touched by default (see PLAN.md): accepted ADRs, generated Report snapshots and extracts
(regenerate them from the DOCX), migrations, build output, binary images, this folder.
"""
import argparse
import re
import zipfile
from collections import Counter
from pathlib import Path

from common import *

TEXT_EXT = {".md", ".cs", ".ts", ".tsx", ".js", ".mjs", ".json", ".py", ".yml", ".yaml", ".html",
            ".drawio", ".dio", ".txt", ".csv", ".css", ".xml", ".ps1", ".cmd", ".sh"}
SKIP_DIRS = {".git", "node_modules", "bin", "obj", "wwwroot", "Migrations", "renumber", ".vs", "dist"}
SKIP_FILES = {"docs/08-reports/Report-1_Project_Introduction.md",
              "docs/08-reports/Report-2_Project_Management_Plan.md",
              "docs/08-reports/Report-3_Software_Requirement_Specification.md",
              "docs/01-requirements/05-use-case-specifications.md"}
SKIP_PREFIX = ("docs/05-decisions/",)


def make_rewriter(uc, br, msg, orphans):
    def r_uc(m):
        old = int(m.group(1))
        if old not in uc:
            orphans[f"UC-{m.group(1)}{m.group(2)}"] += 1
            return m.group(0)
        return fmt("UC-", uc[old]) + m.group(2)

    def r_br(m):
        old = int(m.group(1))
        if old not in br:
            orphans[f"BR-{m.group(1)}"] += 1
            return m.group(0)
        return fmt("BR-", br[old])

    def r_msg(m):
        old = int(m.group(1))
        if old not in msg:
            orphans[f"MSG{m.group(1)}"] += 1
            return m.group(0)
        return fmt("MSG", msg[old])

    def r_const(m):
        old = int(m.group(1))
        return fmt("Msg", msg[old]) if old in msg else m.group(0)

    def rewrite(text):
        text = UC_RE.sub(r_uc, text)
        text = BR_RE.sub(r_br, text)
        text = MSG_RE.sub(r_msg, text)
        return MSGCONST_RE.sub(r_const, text)

    return rewrite


def files(adrs):
    for p in ROOT.rglob("*"):
        if not p.is_file() or p.suffix.lower() not in TEXT_EXT:
            continue
        parts = p.relative_to(ROOT).parts
        rel = "/".join(parts)
        if SKIP_DIRS & set(parts):
            continue
        if rel in SKIP_FILES or (not adrs and rel.startswith(SKIP_PREFIX)):
            continue
        yield p, rel


def rewrite_office(path, rewrite, write, ranges):
    """Replace tokens that sit inside a single text run; ranges are reported for a human."""
    run = re.compile(r"(<(?:w|a):t(?: [^>]*)?>)([^<]*)(</(?:w|a):t>)")
    total, parts = 0, []
    with zipfile.ZipFile(path) as z:
        for info in z.infolist():
            data = z.read(info.filename)
            if info.filename.endswith(".xml") and (info.filename.startswith("word/") or info.filename.startswith("ppt/")):
                xml = data.decode("utf-8")
                new = run.sub(lambda m: m.group(1) + rewrite(m.group(2)) + m.group(3), xml)
                total += sum(1 for a, b in zip(run.findall(xml), run.findall(new)) if a[1] != b[1])
                plain = "".join(x[1] for x in run.findall(xml))
                ranges.extend(f"{path.name}: {m.group(0)}" for m in RANGE_RE.finditer(plain))
                data = new.encode("utf-8")
            parts.append((info, data))
    if write and total:
        tmp = path.with_suffix(path.suffix + ".tmp")
        with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as z:
            for info, data in parts:
                z.writestr(info, data)
        tmp.replace(path)
    return total


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--docx", action="store_true")
    ap.add_argument("--adrs", action="store_true")
    a = ap.parse_args()
    uc, br, msg = read_map("uc"), read_map("br"), read_map("msg")
    orphans, ranges, changed = Counter(), [], Counter()
    rewrite = make_rewriter(uc, br, msg, orphans)

    for p, rel in files(a.adrs):
        try:
            old = p.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        ranges.extend(f"{rel}: {m.group(0)}" for m in RANGE_RE.finditer(old))
        new = rewrite(old)
        if new != old:
            changed[rel] = sum(1 for x, y in zip(old.split(), new.split()) if x != y)
            if a.write:
                p.write_text(new, encoding="utf-8", newline="")

    if a.docx:
        src = ROOT / "docs/08-reports/source"
        for p in list(src.glob("*.docx")) + list(src.glob("*.pptx")):
            n = rewrite_office(p, rewrite, a.write, ranges)
            if n:
                changed[p.relative_to(ROOT).as_posix()] = n

    log = Path(__file__).parent / "mapping" / "orphans.log"
    log.write_text(
        "Tokens with no new number (dropped or unknown) - decide by hand:\n"
        + "\n".join(f"{k}\t{v}" for k, v in sorted(orphans.items()))
        + "\n\nRanges / bare numbers to check by hand:\n" + "\n".join(ranges) + "\n", encoding="utf-8")
    print("WROTE" if a.write else "DRY RUN", len(changed), "files;", sum(changed.values()), "tokens;",
          len(orphans), "orphan ids;", len(ranges), "ranges to check")


if __name__ == "__main__":
    main()
