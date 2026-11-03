"""Check that a report DOCX is one Word opens without the "unreadable content" prompt, and that the
report still follows the house style the team writes it in.

Run:
    python scripts/check_docx.py                      # every DOCX in docs/08-reports/source/
    python scripts/check_docx.py path/to/report.docx   # one file
    python scripts/check_docx.py --fix-fonts           # set Report 3 fonts to Times New Roman, then check

Exit code is 1 when a file has an error. Warnings are printed but do not fail the run.

What is checked, and why
------------------------
Word opens a DOCX without a repair prompt only when the package and the WordprocessingML inside it are
sound. A merge or a script that re-serialises `word/document.xml` can drop the namespace declarations
on the root element while `mc:Ignorable` still names them; Word then refuses the file. The package,
namespace, bookmark, drawing, relationship and font checks catch that class of defect.

The rest keeps the reports readable and consistent after an edit made by hand or by a script:

- every paragraph style resolves in `styles.xml`, and headings never skip a level;
- a heading is not empty, and text never sits outside a run (where Word shows nothing);
- a table keeps the same number of columns on every row (merged cells counted), and its header row
  is filled in;
- Report 3 keeps its use-case shape: unique `UC-xx` headings, parent use cases with sub-cases, the
  standard row labels, `PRE-n`/`POST-n`/`AF-n`/`EX-n` and Normal Flow steps numbered 1..n, a business
  rule per `BR-n` row in ascending order, and every `BR-n`/`MSG-n`/`UC-n` a use case cites resolves;
- the small things: no `TODO`/`TBD` left behind, no stray spaces around a paragraph, and every picture
  has alternative text (warning only, the reports predate that habit).

Report 3 uses Times New Roman throughout (docs/08-reports/README.md). Other reports keep their own
fonts and are never rewritten; their fonts are only reported.
"""

from __future__ import annotations

import html
import os
import re
import sys
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from xml.dom import minidom

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "docs" / "08-reports" / "source"


TIMES = "Times New Roman"
# Equation and bullet glyph fonts are not text fonts; leave them alone.
FONT_OK = {TIMES, "Cambria Math", "Symbol", "Wingdings", "Wingdings 2", "Wingdings 3"}
FONT_PARTS = re.compile(r"word/(document|styles|numbering|footnotes|endnotes|header\d*|footer\d*)\.xml$")
FONT_ATTR = re.compile(r'\s(w:(?:ascii|hAnsi|cs|eastAsia)(?:Theme)?)="([^"]*)"')

P_RE = re.compile(r"<w:p\b[^>]*>.*?</w:p>", re.S)
T_RE = re.compile(r"<w:t(?:\s[^>]*)?>(.*?)</w:t>", re.S)
RUN_RE = re.compile(r"<w:r\b[^>]*>.*?</w:r>", re.S)
BREAK_RE = re.compile(r"<w:br\s*/>|<w:cr\s*/>")
STYLE_RE = re.compile(r'<w:pStyle w:val="([^"]+)"')
TBL_RE = re.compile(r"<w:tbl>.*?</w:tbl>", re.S)
TR_RE = re.compile(r"<w:tr\b[^>]*>.*?</w:tr>", re.S)
TC_RE = re.compile(r"<w:tc\b[^>]*>.*?</w:tc>", re.S)
GRIDSPAN_RE = re.compile(r'<w:gridSpan w:val="(\d+)"')
GRIDCOL_RE = re.compile(r"<w:gridCol\b")
DRAWING_RE = re.compile(r"<w:drawing>.*?</w:drawing>", re.S)
DOCPR_RE = re.compile(r"<wp:docPr\b[^>]*>")
ALT_RE = re.compile(r'\b(?:descr|title)="[^"]+"')

UC_HEADING_RE = re.compile(r"^\s*(?:\d+(?:\.\d+)*\s+)?(UC-\d+[a-z]?)\s+[–-]\s+\S")
UC_ID_RE = re.compile(r"^UC-\d{2}[a-z]?$")
UC_LABELS = (
    "Primary Actors",
    "Secondary Actors",
    "Description",
    "Preconditions",
    "Postconditions",
    "Normal Flow",
    "Alternative Flows",
    "Exception Flows",
)
BR_ID_RE = re.compile(r"^BR-\d{2,3}$")
MSG_ID_RE = re.compile(r"^MSG\d{2,4}$")
PLACEHOLDER_RE = re.compile(r"placeholder only", re.I)
SUSPICIOUS = ("TODO", "TBD", "FIXME", "XXX", "??", "Lorem ipsum")
# MSG01 is documented as shared by POST /login and POST /refresh (one situation: the request cannot
# be authenticated) in docs/03-api/00-danh-muc-ma-thong-bao.md, so two rows with different text are
# intended there; every other reuse of one code for two texts is reported.
SHARED_MSG_CODES = {"MSG01"}


def is_report3(path: Path) -> bool:
    return path.name.startswith("Report_3")


def font_issues(zf: zipfile.ZipFile) -> dict[str, set[str]]:
    """Parts that name a font other than Times New Roman, with the fonts found."""
    found: dict[str, set[str]] = {}
    for name in zf.namelist():
        if not FONT_PARTS.match(name):
            continue
        xml = zf.read(name).decode("utf8")
        bad = set()
        for rfonts in re.findall(r"<w:rFonts [^>]*>", xml):
            for attr, value in FONT_ATTR.findall(rfonts):
                if attr == "w:eastAsiaTheme":
                    continue  # only decides East Asian text, never Latin or Vietnamese
                if attr.endswith("Theme"):
                    bad.add(f"{attr}={value}")
                elif value not in FONT_OK:
                    bad.add(value)
        if bad:
            found[name] = bad
    return found


def fix_fonts(path: Path) -> int:
    """Rewrite every non-Times-New-Roman font on a run or style to Times New Roman. Returns parts changed."""
    zin = zipfile.ZipFile(path)
    infos = zin.infolist()
    data = {i.filename: zin.read(i.filename) for i in infos}
    changed = 0
    for name in font_issues(zin):
        xml = data[name].decode("utf8")

        def rewrite(m: re.Match[str]) -> str:
            tag = m.group(0)
            tag = re.sub(r'\sw:(?:ascii|hAnsi|cs)Theme="[^"]*"', "", tag)
            return re.sub(r'(w:(?:ascii|hAnsi|cs|eastAsia)=")([^"]*)(")',
                          lambda a: a.group(0) if a.group(2) in FONT_OK else f"{a.group(1)}{TIMES}{a.group(3)}", tag)

        new = re.sub(r"<w:rFonts [^>]*>", rewrite, xml)
        if new != xml:
            data[name] = new.encode("utf8")
            changed += 1
    zin.close()
    if changed:
        tmp = path.with_suffix(path.suffix + ".tmp")
        with zipfile.ZipFile(tmp, "w") as zo:
            for i in infos:
                zo.writestr(i, data[i.filename], compress_type=i.compress_type)
        os.replace(tmp, path)
    return changed


def text_of(xml: str) -> str:
    return "".join(html.unescape(t) for t in T_RE.findall(xml))


def paragraphs(doc: str) -> list[dict[str, object]]:
    """Every paragraph with its style, its full text and its text split at line breaks."""
    out = []
    for m in P_RE.finditer(doc):
        para = m.group(0)
        style = STYLE_RE.search(para)
        out.append({
            "style": style.group(1) if style else None,
            "text": text_of(para),
            "lines": [text_of(line) for line in BREAK_RE.split(para)],
        })
    return out


def heading_level(style: str | None) -> int | None:
    if not style:
        return None
    match = re.fullmatch(r"Heading (\d)|Heading(\d)", style)
    if match:
        return int(match.group(1) or match.group(2))
    return None


def table_rows(xml: str) -> list[list[str]]:
    rows = []
    for tr in TR_RE.finditer(xml):
        rows.append([text_of(tc.group(0)).strip() for tc in TC_RE.finditer(tr.group(0))])
    return rows


def check_styles(zf: zipfile.ZipFile, paras: list[dict[str, object]]) -> list[str]:
    errors: list[str] = []
    styles_xml = zf.read("word/styles.xml").decode("utf8") if "word/styles.xml" in zf.namelist() else ""
    defined = re.findall(r'<w:style [^>]*w:styleId="([^"]+)"', styles_xml)
    duplicated = [s for s, n in Counter(defined).items() if n > 1]
    if duplicated:
        errors.append(f"duplicate styleId in styles.xml: {', '.join(sorted(duplicated))}")
    used = sorted({p["style"] for p in paras if p["style"]})
    unknown = [s for s in used if s not in defined]
    if unknown:
        errors.append(f"paragraph style not defined in styles.xml: {', '.join(unknown)}")
    return errors


def check_headings(paras: list[dict[str, object]]) -> list[str]:
    errors: list[str] = []
    last = 0
    for para in paras:
        level = heading_level(para["style"])
        if level is None:
            continue
        text = str(para["text"]).strip()
        if not text:
            errors.append(f"empty Heading{level}")
        if last and level > last + 1:
            errors.append(f"Heading{level} after Heading{last} skips a level: {text[:60]!r}")
        last = level
    return errors


def check_tables(doc: str) -> list[str]:
    errors: list[str] = []
    for number, tbl in enumerate(TBL_RE.finditer(doc), start=1):
        xml = tbl.group(0)
        width = len(GRIDCOL_RE.findall(xml))
        rows = list(TR_RE.finditer(xml))
        if not rows:
            errors.append(f"table {number} has no rows")
            continue
        printed = 0
        for row_number, tr in enumerate(rows, start=1):
            cells = list(TC_RE.finditer(tr.group(0)))
            span = sum(int(GRIDSPAN_RE.search(c.group(0)).group(1)) if GRIDSPAN_RE.search(c.group(0)) else 1
                       for c in cells)
            if span != width and printed < 3:
                errors.append(f"table {number} row {row_number} has {span} columns, the grid has {width}")
                printed += 1
        header = [text_of(c.group(0)).strip() for c in TC_RE.finditer(rows[0].group(0))]
        if any(not cell for cell in header):
            errors.append(f"table {number} has an empty header cell")
    return errors


def check_runs(doc: str) -> list[str]:
    """Text must live inside a run; text left outside one is silently lost in Word."""
    stripped = RUN_RE.sub("", doc)
    if re.search(r"<w:t(?:\s|>)", stripped):
        return ["text sits outside a run (w:t not inside w:r)"]
    return []


def check_hygiene(paras: list[dict[str, object]], doc: str) -> list[str]:
    warnings: list[str] = []
    for token in SUSPICIOUS:
        hits = [str(p["text"])[:60] for p in paras if token in str(p["text"])]
        if hits:
            warnings.append(f"placeholder text {token!r} in {len(hits)} paragraph(s), first: {hits[0]!r}")
    spaced = sum(1 for p in paras if str(p["text"]).strip() and str(p["text"]) != str(p["text"]).strip())
    if spaced:
        warnings.append(f"{spaced} paragraph(s) begin or end with a space")
    drawings = DRAWING_RE.findall(doc)
    without_alt = 0
    for drawing in drawings:
        docpr = DOCPR_RE.search(drawing)
        if not docpr or not ALT_RE.search(docpr.group(0)):
            without_alt += 1
    if without_alt:
        warnings.append(f"{without_alt} of {len(drawings)} picture(s) have no alternative text")
    return warnings


def uc_blocks(paras: list[dict[str, object]]) -> list[dict[str, object]]:
    """Use-case sections: the heading paragraph, its id, and the paragraphs up to the next UC heading."""
    heads = []
    for index, para in enumerate(paras):
        if heading_level(para["style"]) not in (3, 4):
            continue
        match = UC_HEADING_RE.match(str(para["text"]))
        if match:
            heads.append({"index": index, "id": match.group(1), "title": str(para["text"]).strip()})
    for position, head in enumerate(heads):
        head["end"] = heads[position + 1]["index"] if position + 1 < len(heads) else len(paras)
        head["block"] = paras[head["index"]:head["end"]]
        head["text"] = "\n".join(str(p["text"]) for p in head["block"])
    return heads


def numbering_error(label: str, numbers: list[int]) -> str | None:
    expected = list(range(1, len(numbers) + 1))
    if numbers and numbers != expected:
        return f"{label} numbered {numbers}, expected {expected}"
    return None


def flow_steps(block: list[dict[str, object]]) -> list[int]:
    """Step numbers of the Normal Flow, stopping at the next row label or the first AF/EX flow."""
    numbers: list[int] = []
    start = next((i for i, p in enumerate(block) if str(p["text"]).strip() == "Normal Flow"), None)
    if start is None:
        return numbers
    for para in block[start + 1:]:
        if str(para["text"]).strip() in UC_LABELS:
            break
        for line in para["lines"]:
            line = line.strip()
            if re.match(r"^(?:AF|EX)-\d+\s*[:–-]", line):
                return numbers
            match = re.match(r"^(\d+)\.\s", line)
            if match:
                numbers.append(int(match.group(1)))
    return numbers


def message_key(text: str) -> str:
    """The message without the per-field detail some rows append after "Field errors:"."""
    return text.split("Field errors:")[0].strip()


def check_report3(paras: list[dict[str, object]], doc: str) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []

    blocks = uc_blocks(paras)
    if not blocks:
        return ["no use-case headings (UC-xx) found"], warnings

    ids = [b["id"] for b in blocks]
    for code, count in Counter(ids).items():
        if count > 1:
            errors.append(f"use-case heading {code} appears {count} times")
    for code in ids:
        if not UC_ID_RE.match(code):
            errors.append(f"use-case id {code} is not UC-nn or UC-nna")
    defined = set(ids)
    for code in sorted({code[:-1] for code in ids if re.fullmatch(r"UC-\d+[a-z]", code)}):
        if code not in defined:
            errors.append(f"sub-case headings {code}a… exist but {code} has no heading")

    for position, block in enumerate(blocks):
        code = block["id"]
        body = block["block"]
        next_id = blocks[position + 1]["id"] if position + 1 < len(blocks) else None
        is_parent = next_id is not None and re.fullmatch(re.escape(code) + r"[a-z]", next_id) is not None
        if is_parent:
            continue
        text = block["text"]
        if PLACEHOLDER_RE.search(text):
            continue
        present = {str(p["text"]).strip() for p in body}
        missing = [label for label in UC_LABELS if label not in present]
        if missing:
            errors.append(f"{code}: missing row(s) {', '.join(missing)}")
        step_error = numbering_error(f"{code} Normal Flow steps", flow_steps(body))
        if step_error:
            errors.append(step_error)
        for tag in ("PRE", "POST"):
            numbers = [int(n) for n in re.findall(rf"{tag}-(\d+):", text)]
            error = numbering_error(f"{code} {tag}", numbers)
            if error:
                errors.append(error)
        definitions: dict[str, list[int]] = defaultdict(list)
        for para in body:
            for line in para["lines"]:
                match = re.match(r"^\s*(AF|EX)-(\d+)\s*[:–-]", line)
                if match:
                    definitions[match.group(1)].append(int(match.group(2)))
        for tag, numbers in sorted(definitions.items()):
            error = numbering_error(f"{code} {tag} flows", numbers)
            if error:
                errors.append(error)

    # Business rules: one row per code, in ascending order, each with a rule and the use case it serves.
    br_codes: list[int] = []
    br_seen: set[int] = set()
    rule_count = 0
    for tbl in TBL_RE.finditer(doc):
        rows = table_rows(tbl.group(0))
        if not rows or not any(cell in ("BR-ID", "Rule Statement") for cell in rows[0]):
            continue
        rule_count += 1
        for row_number, row in enumerate(rows[1:], start=2):
            if not row or not BR_ID_RE.match(row[0]):
                continue
            if len(row) != 5:
                errors.append(f"BR row {row[0]} has {len(row)} cells, expected 5")
            number = int(row[0].split("-")[1])
            if number in br_seen:
                errors.append(f"business rule {row[0]} appears twice")
            br_seen.add(number)
            br_codes.append(number)
            if len(row) > 1 and not row[1]:
                errors.append(f"business rule {row[0]} has no text")
            if len(row) > 2 and not row[2]:
                errors.append(f"business rule {row[0]} names no use case")
    if rule_count == 0:
        errors.append("no Business Rules table found")
    elif br_codes != sorted(br_codes):
        for position in range(1, len(br_codes)):
            if br_codes[position] < br_codes[position - 1]:
                errors.append(f"business rules are not in ascending order: BR-{br_codes[position - 1]:02d} "
                              f"then BR-{br_codes[position]:02d}")
                break

    # System messages: one row per code and context, and one text per code.
    msg_contents: dict[str, set[str]] = defaultdict(set)
    message_count = 0
    for tbl in TBL_RE.finditer(doc):
        rows = table_rows(tbl.group(0))
        if not rows or not any("Message code" in cell for cell in rows[0]):
            continue
        message_count += 1
        seen: set[tuple[str, str]] = set()
        for row in rows[1:]:
            if len(row) < 2 or not MSG_ID_RE.match(row[1]):
                continue
            if len(row) != 5:
                errors.append(f"message row {row[1]} has {len(row)} cells, expected 5")
            key = (row[1], row[3] if len(row) > 3 else "")
            if key in seen:
                errors.append(f"message {row[1]} appears twice for the same context")
            seen.add(key)
            if len(row) > 4:
                msg_contents[row[1]].add(message_key(row[4]))
    if message_count == 0:
        errors.append("no System Messages table found")
    for code, contents in sorted(msg_contents.items()):
        if len(contents) > 1 and code not in SHARED_MSG_CODES:
            errors.append(f"message code {code} is used for {len(contents)} different texts: "
                          f"{' / '.join(sorted(c[:40] for c in contents))}")

    # Every business rule, message and use case a use case cites must exist. Paragraphs that describe
    # codes as retired are history, not references, so they are left out.
    body = "\n".join(str(p["text"]) for block in blocks for p in block["block"]
                     if "retired" not in str(p["text"]).lower())
    unknown_br = sorted(set(re.findall(r"BR-\d+", body)) - {f"BR-{n:02d}" for n in br_seen} - {f"BR-{n:03d}" for n in br_seen})
    if unknown_br:
        warnings.append(f"cited business rules with no row: {', '.join(unknown_br[:8])}")
    unknown_msg = sorted(set(re.findall(r"MSG\d+", body)) - set(msg_contents))
    if unknown_msg:
        errors.append(f"cited messages with no row: {', '.join(unknown_msg[:8])}")
    unknown_uc = sorted(set(re.findall(r"UC-\d+[a-z]?", body)) - defined)
    if unknown_uc:
        warnings.append(f"cited use cases with no heading: {', '.join(unknown_uc[:8])}")

    return errors, warnings


def check(path: Path) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    try:
        zf = zipfile.ZipFile(path)
    except zipfile.BadZipFile as e:
        return [f"not a valid zip: {e}"], warnings
    bad = zf.testzip()
    if bad:
        errors.append(f"corrupt zip entry: {bad}")
    for name in zf.namelist():
        if name.endswith((".xml", ".rels")):
            try:
                minidom.parseString(zf.read(name))
            except Exception as e:  # noqa: BLE001 - report any parse failure
                errors.append(f"{name}: not well-formed XML ({e})")
    if "word/document.xml" not in zf.namelist():
        return errors + ["word/document.xml missing"], warnings

    doc = zf.read("word/document.xml").decode("utf8")
    root = doc[: doc.index("<w:body>")]
    declared = set(re.findall(r"xmlns:(\w+)=", root))
    ignorable = re.search(r'mc:Ignorable="([^"]*)"', root)
    if ignorable:
        missing = [p for p in ignorable.group(1).split() if p not in declared]
        if missing:
            errors.append(f"mc:Ignorable names undeclared prefixes on the root: {', '.join(missing)}")
    used = set(re.findall(r"</?(\w+):", doc))
    undeclared = sorted(p for p in used - declared - {"xml"} if f"xmlns:{p}=" not in doc)
    if undeclared:
        errors.append(f"prefixes used but never declared: {', '.join(undeclared)}")

    starts = re.findall(r'<w:bookmarkStart [^>]*w:id="(\d+)"', doc)
    ends = re.findall(r'<w:bookmarkEnd [^>]*w:id="(\d+)"', doc)
    if any(n > 1 for n in Counter(starts).values()):
        errors.append("duplicate bookmark ids")
    if set(starts) ^ set(ends):
        errors.append("bookmarkStart/bookmarkEnd not paired")
    names = re.findall(r'<w:bookmarkStart [^>]*w:name="([^"]+)"', doc)
    if any(n > 1 for n in Counter(names).values()):
        errors.append("duplicate bookmark names")
    if any(n > 1 for n in Counter(re.findall(r'<wp:docPr [^>]*\bid="(\d+)"', doc)).values()):
        errors.append("duplicate drawing ids (wp:docPr)")
    if any(int(p, 16) >= 0x80000000 for p in re.findall(r'w14:paraId="(\w+)"', doc)):
        errors.append("w14:paraId >= 0x80000000")

    rels = zf.read("word/_rels/document.xml.rels").decode("utf8")
    rel_ids = set(re.findall(r'Id="(\w+)"', rels))
    refs = set(re.findall(r'r:(?:embed|id|link)="(\w+)"', doc))
    if refs - rel_ids:
        errors.append(f"relationships missing: {', '.join(sorted(refs - rel_ids))}")

    paras = paragraphs(doc)
    errors.extend(check_styles(zf, paras))
    errors.extend(check_headings(paras))
    errors.extend(check_tables(doc))
    errors.extend(check_runs(doc))
    warnings.extend(check_hygiene(paras, doc))

    if is_report3(path):
        report_errors, report_warnings = check_report3(paras, doc)
        errors.extend(report_errors)
        warnings.extend(report_warnings)
        for part, fonts in sorted(font_issues(zf).items()):
            errors.append(f"{part}: font other than {TIMES}: {', '.join(sorted(fonts))} (run with --fix-fonts)")
    return errors, warnings


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--fix-fonts"]
    fixing = len(args) != len(sys.argv) - 1
    targets = [Path(a) for a in args] or sorted(p for p in SOURCE_DIR.glob("*.docx") if not p.name.startswith("~$"))
    failed = False
    for path in targets:
        if fixing and is_report3(path):
            n = fix_fonts(path)
            print(f"fonts: {path.name}: {n} part(s) set to {TIMES}")
        errors, warnings = check(path)
        print(f"{'FAIL' if errors else 'OK  '} {path.name}")
        for e in errors:
            print(f"     - {e}")
        for w in warnings:
            print(f"     ! {w}")
        failed |= bool(errors)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
