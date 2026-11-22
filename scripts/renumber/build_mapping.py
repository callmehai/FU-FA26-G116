"""Build mapping/{uc,br,msg}.csv (old -> new, contiguous) from the current sources.

Usage:  python scripts/renumber/build_mapping.py [--drop-uc 9,62,63,69]

Rules
  UC   order of the first appearance in the inventory's Feature tables; letters (06a) follow the parent.
  BR   order in 06-business-rules.md; rows marked "Retired" are dropped.
  MSG  order of the first appearance in the catalogue's "Danh mục" section; codes whose every row says
       "Ngừng dùng" are dropped; codes only in the contract sections are appended by number.
Human reviews mapping/*.csv and mapping/review.txt BEFORE apply_mapping.py runs.
"""
import argparse
import re

from common import *


def build_uc(drop):
    text = INVENTORY.read_text(encoding="utf-8")
    body = text.split("# 4. Danh mục Use Case theo Feature", 1)[1]
    # Stop before the old-numbering table at the end, if present.
    body = body.split("# 5.", 1)[0]
    order, seen = [], set()
    for line in body.splitlines():
        m = re.match(r"\|\s*UC-(\d{2,3})([a-z]?)\s*\|", line)
        if m and int(m.group(1)) not in seen:
            seen.add(int(m.group(1)))
            order.append(int(m.group(1)))
    rows, new = [], 0
    for old in order:
        if old in drop:
            rows.append((old, "", "dropped (PO)"))
        else:
            new += 1
            rows.append((old, new, ""))
    return rows


def build_br():
    rows, new = [], 0
    for line in BUSINESS_RULES.read_text(encoding="utf-8").splitlines():
        m = re.match(r"\| `BR-(\d+)` \|(.*)", line)
        if not m:
            continue
        old = int(m.group(1))
        if "Retired" in line:
            rows.append((old, "", "retired"))
        else:
            new += 1
            rows.append((old, new, ""))
    return rows


def build_msg():
    text = MSG_CATALOGUE.read_text(encoding="utf-8")
    head, main = text.split("## Danh mục", 1)
    status, order = {}, []
    for line in main.splitlines():
        m = re.match(r"\|\s*`MSG(\d+)`\s*\|", line)
        if not m:
            continue
        n = int(m.group(1))
        if n not in status:
            order.append(n)
            status[n] = []
        status[n].append("Ngừng dùng" in line)
    extra = sorted({int(x) for x in re.findall(r"`MSG(\d+)`", head)} - set(order))
    order += extra
    rows, new = [], 0
    for old in order:
        retired = old in status and all(status[old])
        if retired:
            rows.append((old, "", "retired"))
        else:
            new += 1
            rows.append((old, new, "only in contract section" if old in extra else ""))
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--drop-uc", default="9,62,63,69")
    a = ap.parse_args()
    drop = {int(x) for x in a.drop_uc.split(",") if x}
    hdr = ("old", "new", "note")
    review = []
    for name, rows in (("uc", build_uc(drop)), ("br", build_br()), ("msg", build_msg())):
        write_csv(MAP_DIR / f"{name}.csv", rows, hdr)
        kept = sum(1 for r in rows if r[1] != "")
        moved = sum(1 for r in rows if r[1] != "" and r[0] != r[1])
        review.append(f"{name.upper()}: {len(rows)} old ids, {kept} kept, {len(rows) - kept} dropped, {moved} change number")
        review += [f"  {name} {r[0]}: {r[2]}" for r in rows if r[2]]
    (MAP_DIR / "review.txt").write_text("\n".join(review) + "\n", encoding="utf-8")
    print("\n".join(l for l in review if not l.startswith("  ")))


if __name__ == "__main__":
    main()
