#!/usr/bin/env python3
"""Lint activation bars in sequence-diagram .txt files (sequencediagram.org syntax).

Flags, per diagram:
  - BAR-CUT-IN-BRANCH: a bar opened outside alt/opt/loop is closed inside one branch, so the bar
    ends early while later arrows still point at the participant (seen in calendar-sharing);
  - UNBALANCED: activate / deactivate counts differ, or a deactivate has no open bar;
  - UNCLOSED: a bar is still open at the end of the diagram.
A bar opened and closed inside the same branch (a repository call) is fine.

Usage: python scripts/check_sequence_lifelines.py [files or dirs ...]  (default: all)
Exit code 1 when anything is flagged.
"""
import glob, re, sys

MSG = re.compile(r"^\s*(\w+)\s*-+>>?\s*(\w+)\s*:")
ACT = re.compile(r"^\s*(activate|deactivate)\s+(\w+)")
BR = re.compile(r"^\s*(alt|opt|loop|else|end)\b")


def check(path):
    """A bar opened outside an alt/opt/loop must be closed outside it too. Closing it inside one
    branch (typically the early-return error branch) ends the bar there while arrows keep
    pointing at the participant in the other branches: the lifeline looks cut short."""
    L = open(path, encoding="utf8").read().splitlines()
    bars, problems, level = {}, [], 0   # bars[who] = stack of nesting levels where each bar was opened
    for n, ln in enumerate(L, 1):
        b = BR.match(ln)
        if b:
            k = b.group(1)
            if k in ("alt", "opt", "loop"):
                level += 1
            elif k == "end":
                level = max(level - 1, 0)
            continue
        a = ACT.match(ln)
        if not a:
            continue
        who = a.group(2)
        if a.group(1) == "activate":
            bars.setdefault(who, []).append(level)
        elif not bars.get(who):
            problems.append((n, "UNBALANCED", f"deactivate {who} with no open bar"))
        else:
            opened = bars[who].pop()
            if opened < level:
                problems.append((n, "BAR-CUT-IN-BRANCH",
                                 f"{who}: bar opened outside the alt/opt, closed inside a branch (line {n}); "
                                 "move the deactivate to the end of the diagram"))
    for who, st in bars.items():
        if st:
            problems.append((len(L), "UNCLOSED", f"{who} has {len(st)} open bar(s) at the end"))
    return problems


def main():
    args = sys.argv[1:] or ["docs/06-sequencediagram"]
    files = []
    for a in args:
        files += glob.glob(a + "/**/*.txt", recursive=True) if "." not in a.split("/")[-1] else [a]
    bad = 0
    sys.stdout.reconfigure(encoding="utf8")
    for f in sorted(files):
        p = check(f)
        if p:
            bad += 1
            print(f.replace("\\", "/").split("06-sequencediagram/")[-1])
            for n, k, m in p[:6]:
                print(f"   {n:4} {k}: {m}")
    print(f"\n{bad} of {len(files)} diagrams flagged")
    sys.exit(1 if bad else 0)


main()
