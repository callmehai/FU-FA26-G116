"""Rewrite the Exception Flows row of Report 3 UC tables from the final sequence diagrams.

Usage: python scripts/apply_ex_to_report3.py [--write] [--base <commit>]   (dry run without --write)

For each UC whose diagram has EX: new EX-k takes the old spec block that mentions the diagram's MSG
(or shares at least two words with the branch label), renumbered; a multi-line block gets the
diagram's branch label as its title. A UC with an EX no old block covers is skipped and listed.
"""
import re
import subprocess
import sys
from pathlib import Path

import docx
from docx.table import Table
from docx.text.paragraph import Paragraph

sys.path.insert(0, str(Path(__file__).parent))
from rutgon_ex_final import parse  # noqa: E402

DOCX = "docs/08-reports/source/Report_3_Software_Requirement_Specification.docx"
NL = chr(10)
BASE = sys.argv[sys.argv.index("--base") + 1] if "--base" in sys.argv else "fa648659"
HEAD_RE = re.compile(r"(?m)^(?=EX-\d+(?: – |: ))")

files = subprocess.run(["git", "diff", "--name-only", BASE, "HEAD", "--", "*.txt"],
                       capture_output=True, text=True).stdout.split()
diagrams = {}
for f in files:
    m = re.match(r"UC(\d+[a-z]?)_", Path(f).name)
    if not m:
        continue
    ex = parse(f)
    if ex:
        diagrams.setdefault(m.group(1), []).append(ex)

d = docx.Document(DOCX)
prev = None
done = {}
skipped = set()
report = []
for el in d.element.body.iterchildren():
    if el.tag.endswith("}p"):
        p = Paragraph(el, d)
        if p.text.strip():
            prev = p
        continue
    if not el.tag.endswith("}tbl") or prev is None:
        continue
    hm = re.search(r"UC-(\d+[a-z]?)\b", prev.text)
    if not hm or hm.group(1) not in diagrams or hm.group(1) in done or hm.group(1) in skipped:
        continue
    uc = hm.group(1)
    t = Table(el, d)
    row = next((r for r in t.rows if r.cells[0].text.strip() == "Exception Flows"), None)
    if row is None:
        continue
    cell = row.cells[-1]
    old = NL.join(pp.text for pp in cell.paragraphs)
    blocks = [b.strip(NL) for b in HEAD_RE.split(old) if b.strip()]
    if uc == "04" and len(blocks) == 9:
        q = chr(0x201c); r_ = chr(0x201d)
        done[uc] = (cell, NL.join([
            blocks[0] + " If the Guest session has ended after Step 7 (the 12-hour session expired, the invitation was revoked or the Meeting is no longer open), the system removes the Meeting data from the screen, displays MSG378 " + chr(0x2013) + " " + q + "The guest session has ended." + r_ + " and returns to the verification screen. (BR-155)",
            "EX-2: The code cannot be accepted or cannot be requested: the code is incorrect, expired or already used (MSG126), is locked after 5 incorrect entries (MSG372), is requested again within 1 minute (MSG373), or more than 20 codes were sent for one invitation within 24 hours (MSG379); the system denies the step, displays the message of the case, does not establish a session, and tells the Guest how long to wait when the limit is time-based. The Guest may enter the code again or request a new code once allowed. (BR-153, BR-371)",
            blocks[6].replace("EX-7: The display information is invalid:", "EX-3: The display information is invalid or was already declared:", 1) + " If the invitation already has a display name and the Guest submits a different display name or job title, nothing is changed, the system displays MSG381 " + chr(0x2013) + " " + q + "The display name was already declared." + r_ + " and the Guest continues with the information declared earlier. (BR-180)",
        ]), old)
        report.append("UC-04: 9 EX cu -> 3 moi (viet tay trong script)")
        continue
    first = {}
    for ex_ in diagrams[uc]:
        for k_, v_ in ex_.items():
            first.setdefault(k_, v_)
    new = []
    used = set()
    missing = []
    for k in sorted(first, key=lambda s: int(s.split("-")[1])):
        msg, sit = first[k]
        cands = [msg] + [ex[k][0] for ex in diagrams[uc][1:] if k in ex]
        hit = None
        for c in cands:
            for i, b in enumerate(blocks):
                if c and i not in used and re.search(c + r"\b", b):
                    hit = i
                    break
            if hit is not None:
                break
        if hit is None:
            words = set(re.findall(r"[a-z]{4,}", sit.lower()))
            best = (0, None)
            for i, b in enumerate(blocks):
                if i in used or b.startswith("N/A"):
                    continue
                sc = len(words & set(re.findall(r"[a-z]{4,}", b.lower())))
                if sc > best[0]:
                    best = (sc, i)
            if best[0] >= 2:
                hit = best[1]
        n = int(k.split("-")[1])
        if hit is None:
            missing.append(f"{k} ({msg or 'no MSG'}) {sit}")
            continue
        used.add(hit)
        b = blocks[hit]
        if NL in b:
            sep = " – " if re.match(r"EX-\d+ – ", b) else ": "
            new.append(f"EX-{n}{sep}{sit}" + NL + b.split(NL, 1)[1])
        else:
            new.append(re.sub(r"^EX-\d+", f"EX-{n}", b))
    if missing:
        skipped.add(uc)
        report.append(f"UC-{uc}: BO QUA - spec khong co buoc cho: " + "; ".join(missing))
        continue
    done[uc] = (cell, NL.join(new), old)
    gone = [b.split(NL)[0][:60] for i, b in enumerate(blocks) if i not in used]
    report.append(f"UC-{uc}: {len(blocks)} EX cu -> {len(new)} moi; bo: {gone}")

print(NL.join(report))
print("UC khong thay trong DOCX:", sorted(set(diagrams) - set(done) - skipped))
if "--write" in sys.argv:
    for uc, (cell, text, old) in done.items():
        para = cell.paragraphs[0]
        runs = para.runs
        runs[0].text = text
        for r in runs[1:]:
            r.text = ""
        for extra in cell.paragraphs[1:]:
            extra._p.getparent().remove(extra._p)
    d.save(DOCX)
    print("saved", len(done), "UC")
