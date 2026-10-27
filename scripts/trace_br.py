"""Trace every use case, business rule (BR) and system message (MSG) in Report 3 to the code and
tests that cite it, and render the result as one self-contained HTML page for reading.

Source of truth for rules, messages and use cases:
docs/08-reports/Report-3_Software_Requirement_Specification.md. Code and tests cite a rule or a
message by writing its id (BR-158, MSG138) in a comment, a constant or a test name; this script greps
for those ids.

Usage:
    python scripts/trace_br.py --html              # writes docs/uc-map.html, the main output
    python scripts/trace_br.py --html out.html     # same, somewhere else
    python scripts/trace_br.py                     # drift summary to stdout
    python scripts/trace_br.py --uc UC-15          # one use case card in the terminal
    python scripts/trace_br.py --check             # exit 1 when code cites a BR/MSG Report 3 lacks (CI)
"""

from __future__ import annotations

import argparse
import datetime
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPORT = ROOT / "docs/08-reports/Report-3_Software_Requirement_Specification.md"
DEFAULT_HTML = ROOT / "docs/uc-map.html"
CODE_DIRS = {
    "code": [ROOT / "backend", ROOT / "frontend/src"],
    "test": [ROOT / "tests"],
}
CODE_SUFFIXES = {".cs", ".ts", ".tsx"}
SKIP_PARTS = {"bin", "obj", "node_modules", "wwwroot", "Migrations"}

BR_RE = re.compile(r"\bBR-(\d{1,3})\b")
MSG_RE = re.compile(r"\bMSG(\d{2,3})\b")
UC_RE = re.compile(r"\bUC-(\d{2,3}[a-z]?)\b")
FE_HEAD = re.compile(r"^### \d+\. (FE-\d+) – (.+)$")
UC_HEAD = re.compile(r"^(#{4,5}) [\d.]+ (UC-\d{2,3}[a-z]?) – (.+)$")
STEP = re.compile(r"^(\d+)\.\s+(.*)$")
BLOCK = re.compile(r"^((?:AF|EX|BF)-\d+)\s*[–-]\s*(.*)$")


def norm_br(n: str) -> str:
    return f"BR-{int(n):02d}"


def split_row(line: str) -> list[str]:
    inner = line.strip()[1:-1]
    return [c.strip() for c in re.split(r"(?<!\\)\|", inner)]


def parse_flow(text: str) -> list[dict]:
    """Split a flow cell into blocks of {id, title, notes, steps}."""
    if not text or text.strip().upper() in {"N/A", "NONE"}:
        return []
    blocks: list[dict] = []
    for raw in text.split("<br>"):
        line = raw.strip()
        if not line:
            continue
        if m := BLOCK.match(line):
            blocks.append({"id": m[1], "title": m[2], "notes": [], "steps": []})
        elif m := STEP.match(line):
            if not blocks:
                blocks.append({"id": "", "title": "", "notes": [], "steps": []})
            blocks[-1]["steps"].append({"n": int(m[1]), "text": m[2]})
        elif not blocks:
            blocks.append({"id": "", "title": line, "notes": [], "steps": []})
        elif blocks[-1]["steps"]:
            blocks[-1]["steps"][-1]["text"] += " " + line
        else:
            blocks[-1]["notes"].append(line)
    return blocks


def parse_bullets(text: str) -> list[str]:
    return [re.sub(r"^●\s*", "", p.strip()) for p in text.split("<br>") if p.strip()]


def parse_report() -> dict:
    lines = REPORT.read_text(encoding="utf-8").splitlines()
    features: dict[str, dict] = {}
    ucs: dict[str, dict] = {}
    rules: dict[str, dict] = {}
    msgs: dict[str, dict] = {}
    section, feature, current, parent = "", "", None, None
    for no, line in enumerate(lines, 1):
        if line.startswith("## "):
            section = line
            current = None
        if line.startswith("## II. Use Case Specifications"):
            section = "II"
        if section == "II":
            if m := FE_HEAD.match(line):
                feature, current = m[1], None
                features[feature] = {"id": m[1], "name": m[2], "ucs": []}
            elif m := UC_HEAD.match(line):
                current = m[2]
                if len(m[1]) == 4:
                    parent = current
                ucs[current] = {"id": current, "name": m[3], "feature": feature, "line": no, "extra": {},
                                "parent": parent if len(m[1]) == 5 else None}
                features[feature]["ucs"].append(current)
            elif current and line.startswith("| ") and not line.startswith("| ---"):
                c = split_row(line)
                if len(c) >= 2:
                    ucs[current].setdefault("rows", []).append(c)
        elif section.startswith("## V. "):
            if re.match(r"^\| BR-\d+ \|", line):
                c = split_row(line)
                rules[norm_br(c[0][3:])] = {
                    "text": c[1],
                    "applies": sorted({f"UC-{u}" for u in UC_RE.findall(c[2])}),
                    "rationale": c[3] if len(c) > 3 else "",
                    "enforced": c[4] if len(c) > 4 else "",
                }
            elif re.match(r"^\| \d+ \| MSG\d+ \|", line):
                c = split_row(line)
                msgs[c[1]] = {"type": c[2], "context": c[3], "content": c[4] if len(c) > 4 else ""}

    for uc in ucs.values():
        for row in uc.pop("rows", []):
            key, val = row[0], row[1]
            if key == "Primary Actors":
                uc["actors"] = val
                if len(row) >= 4 and row[2] == "Secondary Actors":
                    uc["secondary"] = row[3]
            elif key == "Description":
                uc["desc"] = val
            elif key == "Preconditions":
                uc["pre"] = parse_bullets(val)
            elif key == "Postconditions":
                uc["post"] = parse_bullets(val)
            elif key == "Normal Flow":
                uc["normal"] = parse_flow(val)
            elif key == "Alternative Flows":
                uc["alts"] = parse_flow(val)
            elif key == "Exception Flows":
                uc["exs"] = parse_flow(val)
            elif val:
                uc["extra"][key] = val
        blob = json.dumps(
            {k: uc.get(k) for k in ("desc", "pre", "post", "normal", "alts", "exs", "extra")},
            ensure_ascii=False,
        )
        uc["br"] = sorted({norm_br(b) for b in BR_RE.findall(blob)}, key=lambda x: int(x[3:]))
        uc["msg"] = sorted({f"MSG{s}" for s in MSG_RE.findall(blob)}, key=lambda x: int(x[3:]))
    return {"features": list(features.values()), "ucs": ucs, "rules": rules, "msgs": msgs}


def scan_code() -> tuple[dict, dict]:
    """(BR id -> {code, test}, MSG id -> {code, test}), each a list of 'path:line'."""
    br: dict[str, dict[str, list[str]]] = defaultdict(lambda: {"code": [], "test": []})
    msg: dict[str, dict[str, list[str]]] = defaultdict(lambda: {"code": [], "test": []})
    for kind, dirs in CODE_DIRS.items():
        for base in dirs:
            for path in base.rglob("*"):
                if path.suffix not in CODE_SUFFIXES or SKIP_PARTS & set(path.parts):
                    continue
                rel = path.relative_to(ROOT).as_posix()
                try:
                    text = path.read_text(encoding="utf-8").splitlines()
                except UnicodeDecodeError:
                    continue
                for no, line in enumerate(text, 1):
                    for b in BR_RE.findall(line):
                        br[norm_br(b)][kind].append(f"{rel}:{no}")
                    for m in MSG_RE.findall(line):
                        msg[f"MSG{m}"][kind].append(f"{rel}:{no}")
    return br, msg


def build(model: dict, br_hits: dict, msg_hits: dict) -> dict:
    cited_in: dict[str, set] = defaultdict(set)
    msg_in: dict[str, set] = defaultdict(set)
    for uc in model["ucs"].values():
        for b in uc["br"]:
            cited_in[b].add(uc["id"])
        for m in uc["msg"]:
            msg_in[m].add(uc["id"])
    for b, r in model["rules"].items():
        r["code"], r["test"] = br_hits[b]["code"], br_hits[b]["test"]
        r["ucs"] = sorted(cited_in[b] | set(r["applies"]), key=lambda u: (int(re.sub(r"\D", "", u)), u))
    for m, r in model["msgs"].items():
        r["code"], r["test"] = msg_hits[m]["code"], msg_hits[m]["test"]
        r["ucs"] = sorted(
            msg_in[m] | {f"UC-{u}" for u in UC_RE.findall(r["context"])},
            key=lambda u: (int(re.sub(r"\D", "", u)), u),
        )
    for uc in model["ucs"].values():
        refs = [(b, model["rules"].get(b)) for b in uc["br"]]
        uc["cov"] = {
            "br": len(refs),
            "code": sum(1 for _, r in refs if r and r["code"]),
            "test": sum(1 for _, r in refs if r and r["test"]),
        }
    model["root"] = ROOT.as_posix()
    return model


def drift(model: dict, br_hits: dict, msg_hits: dict) -> dict:
    return {
        "untraced": sorted((b for b, r in model["rules"].items() if not r["code"] and not r["test"]),
                           key=lambda x: int(x[3:])),
        "unknown_br": sorted((b for b in br_hits if b not in model["rules"]), key=lambda x: int(x[3:])),
        "unknown_msg": sorted((m for m in msg_hits if m not in model["msgs"]), key=lambda x: int(x[3:])),
        "br_missing_in_uc": sorted(
            (b for b, r in model["rules"].items() if not r["ucs"]), key=lambda x: int(x[3:])
        ),
    }


def short(refs: list[str], limit: int = 3) -> str:
    files = sorted({r.rsplit(":", 1)[0].rsplit("/", 1)[-1] for r in refs})
    return ", ".join(files[:limit]) + (f" +{len(files) - limit}" if len(files) > limit else "") if files else "—"


def card(model: dict, uc_id: str) -> str:
    uc = model["ucs"][uc_id]
    out = [f"{uc_id} — {uc['name']}  [{uc['feature']}]", f"  actors: {uc.get('actors', '—')}", ""]
    for b in uc["br"]:
        r = model["rules"].get(b)
        if not r:
            out.append(f"  {b}  (missing from Appendix V.1)")
            continue
        out.append(f"  {b}  {r['text'][:100]}")
        out.append(f"        code: {short(r['code'])}   test: {short(r['test'])}")
    out.append("")
    for m in uc["msg"]:
        r = model["msgs"].get(m)
        out.append(f"  {m}  {r['content'][:90] if r else '(missing from Appendix V.2)'}")
    return "\n".join(out)


def write_html(model: dict, path: Path) -> None:
    tpl = Path(__file__).with_name("trace_br_template.html").read_text(encoding="utf-8")
    model = {**model, "generated": datetime.date.today().isoformat()}
    data = json.dumps(model, ensure_ascii=False).replace("</", "<\\/")
    path.write_text(tpl.replace("__DATA__", data), encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--html", nargs="?", const=str(DEFAULT_HTML), help="write the HTML viewer")
    ap.add_argument("--uc", help="print one use case card, e.g. UC-15")
    ap.add_argument("--check", action="store_true", help="exit 1 on ids in code that Report 3 lacks")
    args = ap.parse_args()

    model = parse_report()
    br_hits, msg_hits = scan_code()
    build(model, br_hits, msg_hits)
    d = drift(model, br_hits, msg_hits)

    if args.uc:
        key = args.uc.upper().replace("UC-", "UC-")
        key = key if key in model["ucs"] else key.lower().replace("uc-", "UC-")
        if key not in model["ucs"]:
            print(f"{args.uc} not found in Report 3", file=sys.stderr)
            return 2
        print(card(model, key))
        return 0

    rules = model["rules"]
    with_test = sum(1 for r in rules.values() if r["test"])
    print(f"Use cases {len(model['ucs'])} · BR {len(rules)} · MSG {len(model['msgs'])}")
    print(f"BR traced to code or test: {len(rules) - len(d['untraced'])}   with a test: {with_test}")
    print(f"BR with no code and no test: {len(d['untraced'])}")
    print(f"BR cited in no use case and not listed in Applies To: {len(d['br_missing_in_uc'])}")
    print(f"Ids in code that Report 3 lacks — BR: {d['unknown_br']}  MSG: {d['unknown_msg']}")

    if args.html:
        out = Path(args.html)
        write_html(model, out)
        print(f"Wrote {out}")
    return 1 if args.check and (d["unknown_br"] or d["unknown_msg"]) else 0


if __name__ == "__main__":
    sys.exit(main())
