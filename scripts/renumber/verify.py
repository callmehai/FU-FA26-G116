"""Check the result: contiguous numbering, ascending order, every code used in code exists in the catalogue.

Usage: python scripts/renumber/verify.py      (exit code 1 when a check fails)
"""
import re
import sys

from common import *

bad = []


def contiguous(label, ids):
    s = sorted(set(ids))
    gaps = sorted(set(range(1, s[-1] + 1)) - set(s))
    if gaps:
        bad.append(f"{label}: gaps {gaps[:20]}")
    return s


def first_seen(pattern, text):
    order = []
    for x in re.findall(pattern, text, re.M):
        if int(x) not in order:
            order.append(int(x))
    return order


br = [int(x) for x in re.findall(r"^\| `BR-(\d+)` \|", BUSINESS_RULES.read_text(encoding="utf-8"), re.M)]
contiguous("BR", br)
if br != sorted(br):
    bad.append("BR: table not ascending")

main = MSG_CATALOGUE.read_text(encoding="utf-8").split("## Danh mục", 1)[1]
msg_order = first_seen(r"^\| `MSG(\d+)` \|", main)
catalogue = contiguous("MSG", msg_order)
if msg_order != sorted(msg_order):
    bad.append("MSG: catalogue not ascending")

inv = INVENTORY.read_text(encoding="utf-8").split("# 4. Danh mục Use Case theo Feature", 1)[1].split("# 5.", 1)[0]
uc_order = first_seen(r"^\| UC-(\d+)[a-z]? \|", inv)
contiguous("UC", uc_order)
if uc_order != sorted(uc_order):
    bad.append("UC: inventory not ascending")

used = set()
for sub in ("backend", "frontend/src", "tests"):
    for p in (ROOT / sub).rglob("*"):
        if p.suffix in {".cs", ".ts", ".tsx"} and not ({"bin", "obj", "node_modules", "wwwroot"} & set(p.parts)):
            t = p.read_text(encoding="utf-8", errors="ignore")
            used |= {int(x) for x in MSG_RE.findall(t)} | {int(x) for x in MSGCONST_RE.findall(t)}
missing = sorted(used - set(catalogue))
if missing:
    bad.append(f"MSG used in code but not in catalogue: {missing}")

print("\n".join(bad) if bad else "OK: BR, MSG, UC contiguous and ascending; every code in code is in the catalogue")
sys.exit(1 if bad else 0)
