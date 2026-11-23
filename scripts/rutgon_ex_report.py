#!/usr/bin/env python3
"""Report which Exception Flows of each UC are candidates to cut, and how many
sequence-diagram lines that would save. Read-only: it changes no document.

Rules (agreed with BA, 2026-11-23):
  - at most CAP (5) EX per UC;
  - EX that share a MSG with an earlier EX of the same UC are merged;
  - system EX ("unable to save/load ... try again") are dropped in the spec and the diagram;
  - business EX (permission, state, invalid data) are cut last, only for BA to choose.

Usage: python scripts/rutgon_ex_report.py [--cap 5] [--out report.md]
"""
import argparse, glob, os, re, sys

SPEC = "docs/01-requirements/05-use-case-specifications.md"
SEQ = "docs/06-sequencediagram/*/*.txt"
SYS = re.compile(r"unable to (save|load|display|send|upload|delete)|cannot be (saved|loaded|read)|try again|temporarily|unavailable", re.I)


def spec_ucs():
    t = open(SPEC, encoding="utf8").read()
    out = {}
    for p in re.split(r"\n(?=#{3,4} UC-\d)", t)[1:]:
        uc = re.match(r"#{3,4} (UC-\d+[a-z]?)", p).group(1)
        cell = re.search(r"\*\*Exception Flows\*\* \|(.*?)\|\s*$", p, re.M | re.S)
        if not cell:
            out[uc] = []
            continue
        exs = []
        for m in re.finditer(r"(EX-\d+) – ([^<]+)<br>((?:(?!<br>EX-\d+ –).)*)", cell.group(1)):
            body = m.group(3)
            msg = re.search(r"(MSG\d+) – “([^”]*)”", body)
            exs.append({"id": m.group(1), "title": m.group(2).strip(),
                        "msg": msg.group(1) if msg else None,
                        "system": bool(msg and SYS.search(msg.group(2)))})
        out[uc] = exs
    return out


def seq_branch_lines():
    """UC -> {EX id -> lines of its branch in the sequence diagram}."""
    out = {}
    for f in glob.glob(SEQ):
        m = re.match(r"(UC)(\d+[a-z]?)_", os.path.basename(f))
        if not m:
            continue
        uc = "UC-" + m.group(2)
        L = open(f, encoding="utf8").read().split("\n")
        d = out.setdefault(uc, {"lines": 0, "ex": {}})
        d["lines"] += len(L)
        for i, ln in enumerate(L):
            h = re.match(r"^(\s*)(alt|else|opt)\b.*?(EX-\d+)", ln)
            if not h:
                continue
            ind, j = len(h.group(1)), i + 1
            while j < len(L):
                mm = re.match(r"^(\s*)(alt|else|opt|end)\b", L[j])
                if mm and len(mm.group(1)) <= ind:
                    break
                j += 1
            d["ex"][h.group(3)] = d["ex"].get(h.group(3), 0) + (j - i)
    return out


def plan(exs, cap):
    keep, merge, system, review = [], [], [], []
    seen, sys_kept = set(), False
    for e in exs:
        if e["msg"] and e["msg"] in seen:
            merge.append(e)
            continue
        if e["msg"]:
            seen.add(e["msg"])
        if e["system"]:
            system.append(e)  # infrastructure failure: drawn nowhere, dropped from the spec too
            continue
        keep.append(e)
    if len(keep) > cap:
        # business EX beyond the cap: BA picks which to drop
        review = keep[cap:]
        keep = keep[:cap]
    return keep, merge, system, review


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cap", type=int, default=3)
    ap.add_argument("--out")
    a = ap.parse_args()
    spec, seq = spec_ucs(), seq_branch_lines()
    rows, saved, total = [], 0, 0
    for uc, exs in spec.items():
        keep, merge, system, review = plan(exs, a.cap)
        cut = merge + system + review
        lines = seq.get(uc, {}).get("ex", {})
        s = sum(lines.get(e["id"], 0) for e in cut)
        saved += s
        total += seq.get(uc, {}).get("lines", 0)
        if cut:
            rows.append((uc, len(exs), len(keep), merge, system, review, s))
    w = lambda es: ", ".join(f'{e["id"]}{"/" + e["msg"] if e["msg"] else ""}' for e in es) or "-"
    out = ["| UC | EX | Keep | Merge (same MSG) | Extra system EX | Over cap (BA picks) | Seq lines saved |",
           "|---|--:|--:|---|---|---|--:|"]
    for uc, n, k, mg, sy, rv, s in sorted(rows, key=lambda r: -r[6]):
        out.append(f"| {uc} | {n} | {k} | {w(mg)} | {w(sy)} | {w(rv)} | {s} |")
    out.append(f"\nUC to change: {len(rows)} of {len(spec)}. Sequence lines saved: {saved} of {total} "
               f"({100 * saved / max(total, 1):.1f}%).")
    text = "\n".join(out)
    if a.out:
        open(a.out, "w", encoding="utf8").write(text + "\n")
    sys.stdout.reconfigure(encoding="utf8")
    print(text)


if __name__ == "__main__":
    main()
