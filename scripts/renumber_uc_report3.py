"""Renumber the use cases of Report 3 (ADR-0079, UC batch 1) and write the old -> new mapping table.

Usage:
    python scripts/renumber_uc_report3.py <in.docx> <out.docx>

One-off script, kept so the change can be reviewed and rerun. It runs on the DOCX from before the renumbering
and refuses one that no longer has the retired UC-51 heading. Decisions (24/11/2026):

* numbers stay two digits (UC-01 …) and follow the order of section II of Report 3, feature by feature;
* a parent keeps its sub-cases with letter suffixes, lettered again without gaps (UC-32a, UC-32c -> a, b);
* the retired UC-51 is removed and gets no new number; Feature numbers are not changed (FE-14 stays empty).

What it changes:
* the DOCX, outside the Change Log and the table of contents (Word rebuilds that field): every UC id, in one
  pass, so ranges such as UC-28a–UC-30 stay right because the order is kept; UC-86, which sat as an unnumbered
  sub-heading inside UC-81, becomes its own parent 10.4 and the FE-10 sections after it move down; the UC-51
  section and its row in 4.2 are removed; 4.2 is sorted by the new ids; one Change Log row;
* `docs/01-requirements/05-use-case-specifications.md`: ids mapped, UC-51 removed and the blocks put in report
  order, so the hand-kept parts (sequence diagram links, grouping notes) follow their use case. Run
  `sync_reports.py --report 3` afterwards;
* writes `docs/01-requirements/10-bang-anh-xa-uc-dot-1.md`.
"""

import copy
import random
import re
import sys
from pathlib import Path

import docx
from docx.text.paragraph import Paragraph
from lxml import etree

ROOT = Path(__file__).resolve().parent.parent
EXTRACT = ROOT / "docs/01-requirements/05-use-case-specifications.md"
MAPPING = ROOT / "docs/01-requirements/10-bang-anh-xa-uc-dot-1.md"

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
W14 = "http://schemas.microsoft.com/office/word/2010/wordml"
q = lambda t: f"{{{W}}}{t}"
PARAID = f"{{{W14}}}paraId"
XMLSPACE = "{http://www.w3.org/XML/1998/namespace}space"
UC = re.compile(r"\bUC-(\d+)([a-z]?)\b")
RETIRED = "UC-51"

SRC, OUT = sys.argv[1], sys.argv[2]
d = docx.Document(SRC)
body = d.element.body
random.seed(79)
used_ids = {el.get(PARAID) for el in body.iter() if el.get(PARAID)}


def text(el):
    return "".join(t.text or "" for t in el.iter(q("t")))


def style(el):
    s = Paragraph(el, d).style if el.tag == q("p") else None
    return s.name.lower() if s is not None else ""


def is_heading(el):
    return style(el).startswith("heading")


def set_para(p, value):
    """Keep the first run's formatting and the paragraph's bookmarks; replace the text."""
    runs = p.findall(q("r"))
    for extra in runs[1:]:
        p.remove(extra)
    r = runs[0]
    for c in list(r):
        if c.tag != q("rPr"):
            r.remove(c)
    t = etree.SubElement(r, q("t"))
    t.text = value
    t.set(XMLSPACE, "preserve")


def new_paraid():
    while True:
        v = f"{random.randint(0x10000000, 0x7FFFFFFE):08X}"
        if v not in used_ids:
            used_ids.add(v)
            return v


# ---------------------------------------------------------------- the order of section II and the mapping
children = list(body.iterchildren())
part = ""
order = []  # (old id, heading element)
for el in children:
    if not is_heading(el):
        continue
    t = text(el).strip()
    m = re.match(r"^(I|II|III|IV|V)\.\s", t)
    if m:
        part = m.group(1)
    if part == "II" and (m := re.search(r"\b(UC-\d+[a-z]?) – ", t)):
        order.append((m.group(1), el))
if not any(u == RETIRED and "Publish Meeting Summary (retired)" in text(el) for u, el in order):
    sys.exit("No retired UC-51 heading: this DOCX is already renumbered. Run the script on the DOCX from before.")

mapping, number, letter, parent = {}, 0, 0, None
for old, _el in order:
    if old == RETIRED:
        continue
    if old[-1].isalpha():
        assert parent == old[:-1], f"{old} does not follow its parent heading {old[:-1]}"
        letter += 1
        mapping[old] = f"UC-{number:02d}{chr(96 + letter)}"
    else:
        number, letter, parent = number + 1, 0, old
        mapping[old] = f"UC-{number:02d}"
unknown = set()


def remap(s):
    def one(m):
        old = f"UC-{m.group(1)}{m.group(2)}"
        if old in mapping:
            return mapping[old]
        unknown.add(old)
        return m.group(0)
    return UC.sub(one, s)


# ---------------------------------------------------------------- structural fixes (before the id pass)
headings = {old: el for old, el in order}
# UC-51: remove its section (heading up to the next heading) and every sentence that only points at it.
el = headings[RETIRED]
while True:
    nxt = el.getnext()
    body.remove(el)
    el = nxt
    if el is None or is_heading(el):
        break
SENTENCES = {
    "(UC-47, UC-48, UC-49, UC-50; UC-51 is not implemented)": "(UC-47, UC-48, UC-49, UC-50)",
    "publishing the summary is not implemented (UC-51).": "publishing the summary is not implemented.",
    # Notes about the numbering before this batch, which the new numbers make wrong.
    " UC-32b remains retired.": "",
    "UC-33b is reassigned to linking an existing Meeting document to an Agenda Item;":
        "UC-33b links an existing Meeting document to an Agenda Item;",
}
# UC-86 becomes a parent of its own: same level and style as UC-81, section 10.4; FE-10 below moves down.
uc81, uc86 = headings["UC-81"], headings["UC-86"]
old_ppr = uc86.find(q("pPr"))
if old_ppr is not None:
    uc86.remove(old_ppr)
uc86.insert(0, copy.deepcopy(uc81.find(q("pPr"))))
SECTION = {"UC-86": "10.4", "UC-86a": "10.4.1", "UC-86b": "10.4.2", "UC-86c": "10.4.3", "UC-86d": "10.4.4",
           "UC-47": "10.5", "UC-48": "10.6", "UC-49": "10.7", "UC-50": "10.8"}
for old, number_text in SECTION.items():
    h = headings[old]
    title = re.sub(r"^[\d.]*\s*", "", text(h).strip())
    set_para(h, f"{number_text} {title}")

def heading_el(prefix):
    found = [el for el in body.iterchildren() if is_heading(el) and text(el).strip().startswith(prefix)]
    assert len(found) == 1, prefix
    return found[0]


def next_table(el):
    while el is not None and el.tag != q("tbl"):
        el = el.getnext()
    return el


def key(uc_id):
    m = UC.fullmatch(uc_id)
    return (int(m.group(1)), m.group(2))


# 4.2: remove the UC-51 row now, before the id pass gives another use case the number UC-51.
for tr in next_table(heading_el("4.2 Use Cases")).findall(q("tr"))[1:]:
    if text(tr.findall(q("tc"))[0]).strip() == RETIRED:
        tr.getparent().remove(tr)

# ---------------------------------------------------------------- one pass over the ids
heading_now = ""
for el in list(body.iterchildren()):
    if is_heading(el):
        heading_now = text(el).strip()
    if heading_now.startswith("Change Log"):
        continue
    for p in el.iter(q("p")):
        if style(p).startswith("toc"):
            continue
        for t in p.iter(q("t")):
            if not t.text:
                continue
            new = t.text
            for a, b in SENTENCES.items():
                new = new.replace(a, b)
            new = remap(new)
            if new != t.text:
                t.text = new
                t.set(XMLSPACE, "preserve")


# 4.2: sort the rows by the new id (the UC-51 row went before the id pass).
uc_table = next_table(heading_el("4.2 Use Cases"))
data = [(key(text(tr.findall(q("tc"))[0]).strip()), tr) for tr in uc_table.findall(q("tr"))[1:]]
for _k, tr in data:
    uc_table.remove(tr)
for _k, tr in sorted(data, key=lambda x: x[0]):
    uc_table.append(tr)

# Change Log
changelog = next_table(heading_el("Change Log"))
last = changelog.findall(q("tr"))[-1]
row = copy.deepcopy(last)
for x in row.iter():
    if x.get(PARAID):
        x.set(PARAID, new_paraid())
for mark in list(row.iter(q("bookmarkStart"), q("bookmarkEnd"))):
    mark.getparent().remove(mark)
VALUES = ("24/11/2026", "M", "HuyNQ",
          f"[ADR-0079, UC batch 1] Use cases renumbered UC-01–UC-{number:02d} in the order of section II, sub-cases "
          "lettered again without gaps; UC-86 made a parent of its own; retired UC-51 removed. Earlier rows keep the "
          "old ids (see 10-bang-anh-xa-uc-dot-1).")
for tc, value in zip(row.findall(q("tc")), VALUES):
    p = tc.findall(q("p"))[0]
    for extra in tc.findall(q("p"))[1:]:
        tc.remove(extra)
    set_para(p, value)
last.addnext(row)

unknown -= {RETIRED}
if unknown:
    sys.exit(f"Ids with no new number outside the TOC and Change Log: {sorted(unknown)}")
d.save(OUT)

# ---------------------------------------------------------------- the use case extract
lines = EXTRACT.read_text(encoding="utf-8").splitlines()
HEAD = re.compile(r"^(#{3,4}) (UC-\d+[a-z]?) – ")
FE = re.compile(r"^## (FE-\d+) — ")
blocks, current, preamble, fe_of = {}, None, [], {}
sections = []  # [(fe line, [uc ids in extract order])]
tail_start = len(lines)
for i, line in enumerate(lines):
    if FE.match(line):
        sections.append((line, []))
        current = None
        continue
    if not sections:
        preamble.append(line)
        continue
    if m := HEAD.match(line):
        current = m.group(2)
        sections[-1][1].append(current)
        blocks[current] = [line]
        continue
    if current is None:
        blocks.setdefault(("fe", sections[-1][0]), []).append(line)
    else:
        blocks[current].append(line)
report_position = {old: i for i, (old, _el) in enumerate(order)}
out = list(preamble)
for fe_line, ucs in sections:
    out.append(fe_line)
    out += blocks.get(("fe", fe_line), [])
    for uc in sorted((u for u in ucs if u != RETIRED), key=lambda u: report_position.get(u, 10**6)):
        out += blocks[uc]
text_out = remap("\n".join(out))
EXTRACT.write_text(text_out.rstrip() + "\n", encoding="utf-8", newline="\n")

# ---------------------------------------------------------------- mapping table
rows_md = [f"| {old} | {new} |" for old, new in sorted(mapping.items(), key=lambda x: key(x[0]))]
rows_md.insert(next(i for i, r in enumerate(rows_md) if key(r.split(" | ")[0][2:]) > key(RETIRED)),
               f"| {RETIRED} | Đã bỏ (retired 19/11/2026, PO); không cấp mã mới |")
MAPPING.write_text("\n".join([
    "# Bảng ánh xạ UC đợt 1: mã cũ → mã mới",
    "",
    "> Theo [ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md), bảng này chỉ được thêm dòng, "
    "không sửa dòng đã có, và được giữ mãi. Áp dụng từ 24/11/2026 cho Report 3 (DOCX). ADR, note, test report và "
    "bảng ánh xạ BR [09](09-bang-anh-xa-br-dot-1.md) giữ mã UC cũ; tra bảng này.",
    "",
    f"Quy tắc: số 2 chữ số theo thứ tự mục II của Report 3, từng Feature (UC-01 → UC-{number:02d}); UC con giữ hậu tố "
    "chữ theo UC cha, đánh lại chữ liền nhau; UC-51 đã bỏ, không cấp số mới; số Feature giữ nguyên. Mã cũ và mã mới "
    "cùng dạng, nên một mã trích ở chỗ khác chỉ hiểu được khi biết nó trước hay sau ngày này.",
    "",
    "Các mã đã bỏ từ trước (UC-09, UC-32b, UC-34a, UC-62, UC-63, UC-69) không có trong Report 3 và không có dòng ở đây.",
    "",
    "| Mã cũ | Mã mới |",
    "|---|---|",
    *rows_md,
]) + "\n", encoding="utf-8", newline="\n")
print(f"{len(mapping)} use cases renumbered, UC-01 to UC-{number:02d}; {RETIRED} removed")
