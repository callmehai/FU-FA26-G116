"""Rename the sequence diagrams to the new UC ids (ADR-0079, table 10) and repoint every live link to them.

Usage:
    python scripts/doi_ten_sequence.py            # dry run: prints the renames
    python scripts/doi_ten_sequence.py --write

For each docs/06-sequencediagram/**/UCnn_Name.txt whose UC id changed: the .txt, its sequencediagram.org .png, its
drawio-png/<page id>.drawio.png, and its tab in TrustCabinet_Sequence_Diagrams.drawio (tab id, the links of the Index
tab, the UC ids in the tab names). Then every live text file that names the old file (links, Report 4's fill script,
examples in the export scripts) is updated. History is left as written: ADRs, docs/notes, test reports, meeting
minutes, the Change Logs, and the mapping tables. Renames go through temporary names, so UC14 -> UC13 never meets the
old UC13. Run after doi_ma_br_uc.py has rewritten the ids inside the .txt files; the PNGs are exported afterwards.
"""

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SEQ = ROOT / "docs/06-sequencediagram"
COMBINED = SEQ / "TrustCabinet_Sequence_Diagrams.drawio"
PNG_DIR = SEQ / "drawio-png"
LIVE_EXTS = {".md", ".txt", ".py", ".js", ".mjs", ".cs", ".ts", ".tsx", ".json", ".html"}
HISTORY = ("docs/05-decisions/", "docs/notes/", "docs/08-reports/test-reports/", "docs/04-meetings/",
           "docs/01-requirements/09-bang-anh-xa", "docs/01-requirements/10-bang-anh-xa", "docs/01-requirements/08-ke-hoach",
           "docs/08-reports/Report-", "node_modules/", "tests/qa/results/", ".claude/worktrees/")


def load_uc():
    rows = {}
    for line in (ROOT / "docs/01-requirements/10-bang-anh-xa-uc-dot-1.md").read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\| (UC-\d+[a-z]?) \| (UC-\d+[a-z]?) \|$", line)
        if m:
            rows[m.group(1)] = m.group(2)
    return rows


def page_id(stem):
    return stem.lower().replace("-", "").replace(" ", "")


UC = load_uc()
renames = []  # (old stem, new stem, folder)
for txt in sorted(SEQ.rglob("*.txt")):
    m = re.fullmatch(r"UC(\d+)([a-z]?)_(.+)", txt.stem)
    if not m:
        continue
    old_id = f"UC-{int(m.group(1)):02d}{m.group(2)}"
    if old_id not in UC:
        sys.exit(f"{txt.name} is not an old id of table 10: the files are already renamed. Stop.")
    new = UC[old_id].replace("UC-", "UC") + "_" + m.group(3)
    if new != txt.stem:
        renames.append((txt.stem, new, txt.parent))


def git_mv(a, b):
    subprocess.run(["git", "mv", str(a.relative_to(ROOT)), str(b.relative_to(ROOT))], cwd=ROOT, check=True)


def main():
    write = "--write" in sys.argv
    print(f"{len(renames)} sequence diagrams to rename")
    for old, new, folder in renames[:5]:
        print(f"  {folder.name}/{old} -> {new}")
    if not write:
        return
    # 1. files, through temporary names
    moves = []
    for old, new, folder in renames:
        moves += [(folder / f"{old}.txt", folder / f"{new}.txt"), (folder / f"{old}.png", folder / f"{new}.png"),
                  (PNG_DIR / f"{page_id(old)}.drawio.png", PNG_DIR / f"{page_id(new)}.drawio.png")]
    moves = [(a, b) for a, b in moves if a.exists()]
    for a, b in moves:
        git_mv(a, a.with_name("__tmp__" + a.name))
    for a, b in moves:
        git_mv(a.with_name("__tmp__" + a.name), b)
    # 2. the combined draw.io file: tab ids and the Index links, then the UC ids in tab names
    text = COMBINED.read_text(encoding="utf-8")
    for old, new, _ in renames:
        text = re.sub(rf"(?<![\w]){re.escape(page_id(old))}(?![\w])", "\x02" + page_id(new) + "\x02", text)
    text = text.replace("\x02", "")

    def tab_name(m):
        return re.sub(r"\bUC-(\d+)([a-z]?)\b",
                      lambda u: UC.get(f"UC-{int(u.group(1)):02d}{u.group(2)}", u.group(0)), m.group(0))

    text = re.sub(r'<diagram id="[^"]*" name="[^"]*"', tab_name, text)
    COMBINED.write_text(text, encoding="utf-8", newline="\n")
    # 3. live links to the old names
    tracked = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.split("\n")
    changed = 0
    for rel in tracked:
        if not rel or Path(rel).suffix not in LIVE_EXTS or rel.startswith(HISTORY) or "/" not in rel and not rel.endswith(".md"):
            continue
        path = ROOT / rel
        if not path.is_file():
            continue
        try:
            s = path.read_bytes().decode("utf-8")
        except UnicodeDecodeError:
            continue
        t = s
        for old, new, _ in renames:
            t = re.sub(rf"(?<![\w]){re.escape(old)}(?![\w])", "\x02" + new + "\x02", t)
            t = re.sub(rf"(?<![\w]){re.escape(page_id(old))}(?=\.drawio\.png|\b)", "\x02" + page_id(new) + "\x02", t)
        t = t.replace("\x02", "")
        if t != s:
            path.write_bytes(t.encode("utf-8"))
            changed += 1
    print(f"renamed {len(moves)} files, updated links in {changed} files")


if __name__ == "__main__":
    main()
