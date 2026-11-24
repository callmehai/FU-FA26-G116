"""Rewrite old BR and UC ids to the new ones in the live docs, code comments, tests and QA plans (ADR-0079, batch 1).

Usage:
    python scripts/doi_ma_br_uc.py            # dry run: writes the review list, changes nothing
    python scripts/doi_ma_br_uc.py --write    # rewrite the files

Mapping: docs/01-requirements/09-bang-anh-xa-br-dot-1.md (BR, a later row for an id wins) and
docs/01-requirements/10-bang-anh-xa-uc-dot-1.md (UC). Old and new ids share their form, so this runs once: it refuses to
run when a file in scope already holds a new-style id BR-0nn (old ids were never written with three digits below 100).

Rules (task for PR 2):
* an old BR becomes its new BR(s); the audit rule split three ways (BR-009/010/011) keeps the one that fits the file;
* a BR moved into a use case becomes that use case's new id — the one the surrounding text already names when the
  target is a group, otherwise all of them; a BR moved to NFR becomes "NFR"; a UI-convention or retired BR is dropped;
* an old UC becomes its new id; a use case dropped before the renumbering (UC-09, UC-32b, UC-34a, UC-51, UC-62, UC-63,
  UC-69) is marked as an old, dropped id, because its number now belongs to another use case;
* only ids change: no behaviour, no file names (sequence diagram links such as UC13_CreateMeeting stay for PR 3).
"""

import collections
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCOPE = ["AGENTS.md", "docs/01-requirements/00-master-use-case-inventory.md", "docs/02-design", "docs/03-api",
         "backend", "frontend/src", "frontend/tests", "tests"]
# PR 3 runs it on the diagrams' text sources: python scripts/doi_ma_br_uc.py --scope docs/06-sequencediagram --write
if "--scope" in sys.argv:
    SCOPE = sys.argv[sys.argv.index("--scope") + 1].split(",")
SKIP = {"bin", "obj", "node_modules", "wwwroot", "Migrations", "results"}
EXTS = {".cs", ".ts", ".tsx", ".md", ".mjs", ".js", ".json", ".txt"}
DROPPED = {"UC-09", "UC-32b", "UC-34a", "UC-51", "UC-62", "UC-63", "UC-69"}
AUDIT = ["BR-009", "BR-010", "BR-011"]
REVIEW = Path(sys.argv[sys.argv.index("--review") + 1]) if "--review" in sys.argv else None


def load_br():
    rows = {}
    for line in (ROOT / "docs/01-requirements/09-bang-anh-xa-br-dot-1.md").read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\| BR-(\d+) \| (.+?) \|$", line)
        if m:
            rows[int(m.group(1))] = m.group(2).split(" — ")[0].strip()  # a later (correction) row wins
    return rows


def load_uc():
    rows = {}
    for line in (ROOT / "docs/01-requirements/10-bang-anh-xa-uc-dot-1.md").read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\| (UC-\d+[a-z]?) \| (UC-\d+[a-z]?) \|$", line)
        if m:
            rows[m.group(1)] = m.group(2)
    return rows


def load_applies():
    """Old BR -> the old UCs of its "Applies To" column, from Report 3 as it stood before the cut (PR #509)."""
    import subprocess
    old = subprocess.run(["git", "show", f"{BEFORE_CUT}:docs/08-reports/Report-3_Software_Requirement_Specification.md"],
                         capture_output=True, text=True, encoding="utf-8", cwd=ROOT, check=True).stdout
    applies = {}
    for line in old.splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) >= 3 and re.fullmatch(r"BR-\d+", cells[0]):
            applies[int(cells[0][3:])] = expand_uc_targets(cells[2])
    return applies


BEFORE_CUT = "613a6e2f1c604011298293e832e417747a7d9839"  # app just before PR #509 merged
BR = load_br()
UC = load_uc()


def uc_key(uc):
    m = re.fullmatch(r"UC-(\d+)([a-z]?)", uc)
    return (int(m.group(1)), m.group(2))


OLD_UCS = sorted(set(UC) | DROPPED, key=uc_key)


def norm_uc(s):
    m = re.fullmatch(r"UC-(\d+)([a-z]?)", s)
    return f"UC-{int(m.group(1)):02d}{m.group(2)}"


def expand_uc_targets(text):
    """'UC-20, UC-28, UC-21, UC-61/64/65' or 'UC-23a–23d' (old ids) -> list of old ids."""
    out = []
    for item in re.split(r",\s*", text):
        item = item.strip()
        m = re.fullmatch(r"UC-(\d+)([a-z]?)\s*[–-]\s*(?:UC-)?(\d+)?([a-z]?)", item)
        if m:
            a = f"UC-{int(m.group(1)):02d}{m.group(2)}"
            b = f"UC-{int(m.group(3) or m.group(1)):02d}{m.group(4)}"
            out += [u for u in OLD_UCS if uc_key(a) <= uc_key(u) <= uc_key(b)]
            continue
        m = re.fullmatch(r"UC-(\d+[a-z]?)((?:/\d+[a-z]?)+)", item)
        if m:
            out += [norm_uc("UC-" + x) for x in [m.group(1)] + m.group(2).strip("/").split("/")]
            continue
        if re.fullmatch(r"UC-\d+[a-z]?", item):
            out.append(norm_uc(item))
    return out


APPLIES = load_applies()


def br_kind(n):
    t = BR.get(n)
    if t is None:
        return "unknown", []
    if t.startswith("BR-"):
        return "br", re.findall(r"BR-\d{3}", t)
    if t.startswith("Trùng"):
        target = int(re.search(r"BR-(\d+)", t).group(1))
        return br_kind(target) if target > 154 else ("br", [f"BR-{target:03d}"])
    if t.startswith("Đã ngừng"):
        return "drop", []
    if "Quy ước chung" in t:
        return "drop", []
    target = t.split("chuyển tới", 1)[1]
    ucs = expand_uc_targets(re.sub(r",?\s*NFR[^,]*", "", target))
    if not ucs:
        return "nfr", []
    return ("uc+nfr" if "NFR" in target else "uc"), ucs  # old ids; "UC-33a, UC-36, NFR" keeps its NFR part


def audit_pick(path):
    p = path.as_posix().lower()
    # QA plans are named by feature: FE-01–03 accounts and roles, FE-12/15/16 minutes, reports and calendars,
    # FE-07/10 documents and voice samples.
    if re.search(r"/(users?|auth|access|roles?|rolepermission|signin|01-xac-thuc|02-nguoi-dung|03-vai-tro)|/fe-0[123]\.", p):
        return ["BR-009"]
    if re.search(r"minutes|bien-ban|report|bao-cao|calendar|chia-se-lich|/fe-1[256]\.", p):
        return ["BR-010"]
    if re.search(r"voice|giong|document|tai-lieu|library|kho|/fe-(?:07|10)\.", p):
        return ["BR-011"]
    return list(AUDIT)


# A slash list may hold a range too: BR-159/160/252–274.
ITEM = (r"(?:BR|UC)-\d+[a-z]?(?:\s*(?:–|\.\.|→)\s*(?:(?:BR|UC)-)?\d+[a-z]?)?"
        r"(?:/(?:(?:BR|UC)-)?\d+[a-z]?(?:–(?:(?:BR|UC)-)?\d+[a-z]?)?)*")
GROUP = re.compile(rf"(?<![\w-])({ITEM}(?:(?:\s*,\s*|\s+and\s+|\s+và\s+|\s+or\s+|\s+hoặc\s+){ITEM})*)(?![\w-])")


def expand_item(item):
    """One citation item -> [(kind, old id)]."""
    kind = item[:2]
    m = re.fullmatch(r"(BR|UC)-(\d+)([a-z]?)\s*(?:–|\.\.|→)\s*(?:(?:BR|UC)-)?(\d+)([a-z]?)", item)
    if m:
        if kind == "BR":
            found = [("BR", n) for n in range(int(m.group(2)), int(m.group(4)) + 1) if n in BR]
            return found or [("BR", int(m.group(2))), ("BR", int(m.group(4)))]
        a = f"UC-{int(m.group(2)):02d}{m.group(3)}"
        b = f"UC-{int(m.group(4)):02d}{m.group(5)}"
        found = [("UC", u) for u in OLD_UCS if uc_key(a) <= uc_key(u) <= uc_key(b)]
        return found or [("UC", a), ("UC", b)]  # backwards: an arrow of a flow, not a range
    parts = re.split(r"/", item)
    out = []
    for part in parts:
        part = part if re.match(r"(BR|UC)-", part) else f"{kind}-{part}"
        if re.search(r"\d\s*[–-]\s*\d", part[3:]):  # a range inside a slash list: BR-159/160/252–274
            out += expand_item(re.sub(r"^(BR|UC)-(\d+[a-z]?)\s*[–-]\s*", r"\1-\2–", part))
        elif part.startswith("BR-"):
            out.append(("BR", int(part[3:])))
        else:
            out.append(("UC", norm_uc(part)))
    return out


stats = collections.Counter()
review = []
gaps = []  # UC ranges whose new ends do not cover exactly the same use cases: checked by hand


def map_group(group, context, path, lang):
    """Return the new text for one group of ids, or '' when nothing is left."""
    items = re.split(r"(\s*,\s*|\s+and\s+|\s+và\s+|\s+or\s+|\s+hoặc\s+)", group)
    olds, news = [], []
    for item in items[0::2]:
        # A UC range keeps its form: the order is kept, so only its two ends change (UC-01 → UC-81).
        m = re.fullmatch(r"(UC-\d+[a-z]?)(\s*(?:–|\.\.|→)\s*)((?:UC-)?\d+[a-z]?)", item)
        if m:
            inside = [u for _k, u in expand_item(item) if u in UC]
            if len(inside) > 2:
                if olds:
                    news += map_items(olds, context, path, lang)
                    olds = []
                first, last = UC[inside[0]], UC[inside[-1]]
                news.append(f"{first}{m.group(2)}{last}")
                stats["UC:range"] += 1
                covered = {u for u in UC.values() if uc_key(first) <= uc_key(u) <= uc_key(last)}
                if covered != {UC[u] for u in inside}:
                    gaps.append((path.as_posix(), item, f"{first}{m.group(2)}{last}"))
                continue
        olds += expand_item(item)
    # Six or more old BRs at once is a summary of "the rules this cites": it keeps the rules (new BRs) only, not the
    # use cases and NFR the moved ones went to, which made such lists long and mixed (review of PR #522).
    summary = sum(1 for k, _ in olds if k == "BR") >= 6
    news += map_items(olds, context, path, lang, rules_only=summary)
    seen, out = set(), []
    for x in news:
        if x not in seen:
            seen.add(x)
            out.append(x)
    return ", ".join(compress(out))


def map_items(olds, context, path, lang, rules_only=False):
    news = []
    for kind, old in olds:
        if kind == "BR":
            k, ids = br_kind(old)
            stats[f"BR:{k}"] += 1
            if rules_only and k != "br":
                continue
            if k == "br":
                news += audit_pick(path) if ids == AUDIT else ids
            elif k in ("uc", "uc+nfr"):
                # Narrow the target group to the use cases the rule applied to (old "Applies To"); a sub-case counts for
                # its parent (BR-191: UC-72 -> UC-72a, UC-72c). Not narrowed by the surrounding text, which named the
                # wrong use case where it mentioned one for another reason (review of PR #522).
                applies = [u for u in APPLIES.get(old, []) if u in UC]
                pool = [u for u in applies if any(u == t or re.fullmatch(re.escape(t) + r"[a-z]", u) for t in ids)]
                news += [UC[u] for u in (pool or applies or ids) if u in UC]
                if k == "uc+nfr":
                    news.append("NFR")
            elif k == "nfr":
                news.append("NFR")
            elif k == "unknown":
                news.append(f"BR-{old}")
                stats["BR:UNKNOWN"] += 1
        else:
            if old in DROPPED:
                news.append(f"{old} cũ" if lang == "vi" else f"former {old}")
                stats["UC:dropped"] += 1
            elif old in UC:
                news.append(UC[old])
                stats["UC:mapped"] += 1
            else:
                news.append(old)
                stats["UC:UNKNOWN"] += 1
    return news


def compress(ids):
    """BR-104, BR-105, BR-106 -> BR-104–BR-106 (three or more in a row)."""
    out, run = [], []

    def flush():
        if len(run) >= 3:
            out.append(f"{run[0]}–{run[-1]}")
        else:
            out.extend(run)
        run.clear()

    for x in ids:
        m = re.fullmatch(r"BR-(\d{3})", x)
        if m and run and int(m.group(1)) == int(run[-1][3:]) + 1:
            run.append(x)
            continue
        flush()
        if m:
            run.append(x)
        else:
            out.append(x)
    flush()
    return out


FLOWS = ["UC-04 → UC-26"]
OLD_LABEL = re.compile(r"\bUC-\d+[a-z]?(?:\s*/\s*UC-\d+[a-z]?)*\s+cũ\b|\bformer UC-\d+[a-z]?")
VIETNAMESE = re.compile(r"[ăâđêôơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]", re.I)


def rewrite_text(text, path):
    lines = text.split("\n")
    out_lines = []
    in_history = False
    for i, line in enumerate(lines):
        # The inventory's §5 is the table of the 10/10/2026 regrouping: history, kept as written.
        if path.name == "00-master-use-case-inventory.md" and line.startswith("# "):
            in_history = line.startswith("# 5.")
        lang = "vi" if VIETNAMESE.search(line) else "en"
        if in_history or not re.search(r"\b(?:BR|UC)-\d", line):
            out_lines.append(line)
            continue
        context = "\n".join(lines[max(0, i - 3): i + 4])
        file_context = text

        def repl(m):
            new = map_group(m.group(1), context, path, lang)
            return new if new else GONE

        work = line
        for pattern, replacement in LINE_REPLACE:
            work = re.sub(pattern, replacement, work)
        for flow in FLOWS:  # an arrow between two steps, not a range: map each end
            work = work.replace(flow, flow.replace(" → ", " ⟶ "))
        # An id already labelled old ("UC-26 cũ", "UC-26 / UC-27 cũ", "former UC-09") belongs to an earlier numbering
        # (the Meeting Session use cases of ADR-0013): kept exactly as written.
        kept = []

        def keep(m):
            kept.append(m.group(0))
            return f"\x01{len(kept) - 1}\x01"

        work = OLD_LABEL.sub(keep, work)
        new_line = GROUP.sub(repl, work).replace(" ⟶ ", " → ")
        new_line = re.sub("\x01(\\d+)\x01", lambda m: kept[int(m.group(1))], new_line)
        new_line = cleanup(new_line, line)
        if new_line != line:
            review.append((path.as_posix(), i + 1, line.strip(), new_line.strip()))
        out_lines.append(new_line)
    return "\n".join(out_lines)


GONE = "\x00"
# A sentence about the numbering before ADR-0079, which the new ids make wrong.
LINE_REPLACE = [
    (r"các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng \(\[danh mục BR\]\([^)]*\)\)\.",
     "mã BR và UC trong file là mã mới theo [bảng 09](../01-requirements/09-bang-anh-xa-br-dot-1.md) và "
     "[bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md) (ADR-0079)."),
]


def dedupe_brackets(s):
    """Inside one bracket, drop an id that already appeared earlier: (UC-42 AF-1, UC-42) -> (UC-42 AF-1)."""
    def one(m):
        seen, parts = set(), []
        for piece in re.split(r"(\s*,\s*|\s*/\s*)", m.group(1)):
            ids = re.findall(r"\b(?:BR|UC)-\d+[a-z]?\b", piece)
            if re.fullmatch(r"(?:BR|UC)-\d+[a-z]?", piece.strip()) and piece.strip() in seen:
                if parts and re.fullmatch(r"\s*[,/]\s*", parts[-1]):
                    parts.pop()
                continue
            seen.update(ids)
            parts.append(piece)
        return "(" + "".join(parts) + ")"
    s = re.sub(r"\(([^()]*)\)", one, s)
    return re.sub(r"\b((?:BR|UC)-\d+[a-z]?) / \1\b", r"\1", s)  # UC-06b / UC-06b


def cleanup(new, old):
    """Remove what a dropped citation (GONE) leaves behind."""
    s = new
    s = re.sub(r"\s*\(\s*" + GONE + r"\s*\)", "", s)                        # (BR-12)
    s = re.sub(r"(?<=\()" + GONE + r"\s*[,;]\s*|\s*[,;]\s*" + GONE + r"(?=\))", "", s)
    s = re.sub(r"\b(?:theo|với|with|under|per|according to|follows?)\s+" + GONE, "", s)  # theo BR-54
    s = re.sub(r"(?<=//)\s*" + GONE + r":\s*", " ", s)                         # // BR-30: text
    s = re.sub(r"\*\*" + GONE + r"\.?\*\*\s*", "", s)
    s = re.sub(GONE + r":\s*", "", s)
    s = re.sub(r"\s*[,;/]\s*" + GONE + r"|" + GONE + r"\s*[,;/]\s*", "", s)
    s = s.replace(GONE, "")
    s = dedupe_brackets(s)
    s = re.sub(r"\((?:\s*[,;/])+\s*\)|\(\s+\)", "", s)  # ( ) or (, ), never a call's ()
    s = re.sub(r"\(\s*[,;/]\s*", "(", s)                     # (, BR-013 -> (BR-013
    s = re.sub(r"\s*[,;/]\s*\)", ")", s)                     # BR-013, ) -> BR-013)
    s = re.sub(r"(?<=[\w)])\s+\.", ".", s) if s != old else s
    s = re.sub(r"  +", " ", s) if s != old and "  " not in old else s
    s = re.sub(r"\s+\)", ")", s) if s != old else s
    s = re.sub(r"(?<=\S) +(?=[;,:])", "", s) if s != old else s
    return s


# Sentences about the numbering of their time that the new ids make wrong, or a dropped citation leaves cut:
# rewritten by hand, applied after the id pass. Each must apply exactly once.
POST_FIXES = {
    "docs/01-requirements/00-master-use-case-inventory.md": [
        ("**Baseline hiện tại: 16 Feature, UC-01 → UC-56, UC-75 → UC-57** (",
         "**Baseline hiện tại: 16 Feature, UC-01 → UC-81** — mã đánh số lại ngày 24/11/2026 theo ADR-0079, tra mã cũ ở "
         "[bảng 10](10-bang-anh-xa-uc-dot-1.md) ("),
        ("Các UC này nối tiếp dải số cũ, không\nđánh lại UC-01 → UC-41.",
         "Các UC này nối tiếp dải số lúc đó, không\nđánh lại các số đã có (trước đợt đánh số lại 24/11/2026)."),
        ("> **Quy ước số:** từ baseline này **UC-01 → UC-79 đóng băng số**. Thêm UC mới thì lấy `UC-40` trở đi;",
         "> **Quy ước số:** từ 24/11/2026 mã UC theo đợt đánh số lại của ADR-0079 (**UC-01 → UC-81**, "
         "[bảng 10](10-bang-anh-xa-uc-dot-1.md)). Thêm UC mới thì lấy `UC-82` trở đi;"),
        ("UC-51–UC-55 đổi nội dung", "UC-51, UC-52, UC-54, UC-55 đổi nội dung"),
    ],
    "docs/02-design/04-luong-nghiep-vu-chinh.md": [
        ("Số UC theo baseline 10/10/2026 (UC-01 → UC-79).",
         "Số UC theo đợt đánh số lại 24/11/2026 (UC-01 → UC-81, [bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md))."),
    ],
    "docs/03-api/05-quan-ly-cuoc-hop.md": [
        ("Sequence UC-16 hiện ghi BR-016, BR-017 ở bước repository; BA cần bổ sung và nhánh query",
         "Sequence UC-16 hiện ghi luật xem cuộc họp (BR-016, BR-017; diagram còn mã cũ tới PR 3) ở bước repository; BA cần "
         "bổ sung cách lọc theo tham số và nhánh query"),
    ],
    "docs/02-design/02-erd.md": [("(SCRUM-37 retired)", "(SCRUM-37)")],
    # The only id list wrapped across two lines: the second line holds bare old numbers (285 → BR-086/087, 288/289 →
    # BR-094/095, 293 → BR-096/097; 287, 290–292, 304, 305 moved into use cases).
    "docs/03-api/15-bieu-quyet.md": [("BR-096–BR-099/\n285/287–293/304/305; ADR", "BR-094–BR-099;\nADR")],
    "docs/03-api/21-nhat-ky-kiem-toan.md": [("màn hình tự hiện câu theo\n.", "màn hình tự hiện câu.")],
    # A test named after an old BR (BR-422, now a step of UC-81a).
    "tests/TrustCabinet.Web.Tests/CalendarSharing/MeetingSlotFinderTests.cs": [
        ("public void The_BR_422_example_gives_10_00()", "public void The_gap_and_step_example_gives_10_00()"),
    ],
}


def files():
    for entry in SCOPE:
        p = ROOT / entry
        for f in ([p] if p.is_file() else p.rglob("*")):
            if f.is_file() and f.suffix in EXTS and not (SKIP & set(f.relative_to(ROOT).parts)):
                yield f


def main():
    write = "--write" in sys.argv
    for f in files():
        t = f.read_text(encoding="utf-8")
        if re.search(r"\bBR-0\d\d\b", t):
            sys.exit(f"{f.relative_to(ROOT)} already holds a new BR id (BR-0nn): this rewrite has run. Stop.")
    changed = 0
    for f in files():
        raw = f.read_bytes()
        t = raw.decode("utf-8")
        crlf = "\r\n" in t
        new = rewrite_text(t.replace("\r\n", "\n"), f.relative_to(ROOT))
        for a, b in POST_FIXES.get(f.relative_to(ROOT).as_posix(), []):
            if new.count(a) != 1:
                sys.exit(f"post fix not applied exactly once in {f.relative_to(ROOT)}: {a[:60]}")
            new = new.replace(a, b)
        if crlf:
            new = new.replace("\n", "\r\n")
        if new != t:
            changed += 1
            if write:
                f.write_bytes(new.encode("utf-8"))
    print(f"{'rewrote' if write else 'would rewrite'} {changed} files", dict(stats))
    for g in gaps:
        print("range to check:", *g)
    if REVIEW:
        REVIEW.write_text("\n".join(f"{p}:{n}\n  - {a}\n  + {b}" for p, n, a, b in review), encoding="utf-8")


if __name__ == "__main__":
    main()
