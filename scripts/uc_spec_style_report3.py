"""Check, and optionally fix, the formatting of every UC Specification table in Report 3.

Usage:
    python scripts/uc_spec_style_report3.py <report3.docx>                 # report only, changes nothing
    python scripts/uc_spec_style_report3.py <report3.docx> --apply <out.docx>

The rules are the ones in docs/08-reports/README.md "Style UC Specification" and note 61
(docs/notes/ban-giao-ba-final-review/61-dong-bo-style-uc-spec-report3.md). The model is the UC-23a table as it
was when the rule was written (commit 1e3db559), not the UC-23a in the DOCX today.

Formatting only. --apply never rewrites a sentence; the only characters it may add or change are:
* "AF-n: Title" / "EX-n: Title" becomes "AF-n – Title" / "EX-n – Title";
* a PRE-n / POST-n line without the leading "● " gets it.

What --apply does, inside the tables whose first cell is "Primary Actors":
* the label cells (left column) are bold;
* in a content cell, several paragraphs are merged into one, separated by line breaks;
* no italic in a content cell;
* Normal Flow, Preconditions, Postconditions, Description and actors: no bold;
* Alternative Flows and Exception Flows: a line that starts with "AF-n" or "EX-n" is bold, every other line is not.

What it only reports (wording, fixed by hand with note 60): an exception flow with no closing sentence
("The use case ends." / "The flow resumes at Step N …"), "Main Flow" where the label says "Normal Flow",
and AF/EX titles written in Title Case.

After --apply: run scripts/check_docx.py on the output, open it in Word, then sync_reports.py --report 3; the
Markdown diff may differ only by ":" -> "–" and "● ".
"""

import copy
import re
import sys
from collections import defaultdict

import docx
from docx.oxml.ns import qn

LABELS = ["Primary Actors", "Secondary Actors", "Description", "Preconditions", "Postconditions",
          "Normal Flow", "Alternative Flows", "Exception Flows"]
FLOW_HEAD = re.compile(r"^\s*(AF|EX)-\d+")
COLON_HEAD = re.compile(r"^(\s*(?:AF|EX)-\d+)\s*:\s*")
PRE_POST = re.compile(r"^\s*(PRE|POST)-\d+\s*:")
CLOSING = re.compile(r"(use case ends|resumes at Step \d+|returns to Step \d+)")


def unique_cells(row):
    seen, cells = set(), []
    for cell in row.cells:
        if id(cell._tc) not in seen:
            seen.add(id(cell._tc))
            cells.append(cell)
    return cells


def uc_tables(document):
    """Yield (uc id, table) for every UC Specification table, with the id of the heading above it."""
    current = None
    body = document.element.body
    for element in body.iterchildren():
        if element.tag == qn("w:p"):
            paragraph = docx.text.paragraph.Paragraph(element, document)
            match = re.search(r"(UC-\d+[a-z]?)\s*[–-]", paragraph.text)
            if match and paragraph.style.name.startswith("Heading"):
                current = match.group(1)
        elif element.tag == qn("w:tbl"):
            table = docx.table.Table(element, document)
            if "Primary Actor" in table.rows[0].cells[0].text:
                yield current, table


def pairs(table):
    for row in table.rows:
        cells = unique_cells(row)
        for i in range(0, len(cells) - 1, 2):
            yield cells[i].text.strip(), cells[i], cells[i + 1]


# ---------------------------------------------------------------- run helpers

def set_flag(run_el, tag, on):
    rpr = run_el.find(qn("w:rPr"))
    if rpr is None:
        if not on:
            return
        rpr = run_el.makeelement(qn("w:rPr"), {})
        run_el.insert(0, rpr)
    for name in (tag, tag + "Cs"):
        for old in rpr.findall(qn("w:" + name)):
            rpr.remove(old)
        if on:
            rpr.append(rpr.makeelement(qn("w:" + name), {}))


def split_runs_at_breaks(paragraph_el):
    """Split every run that holds a w:br so each run carries text of one line only."""
    for run in list(paragraph_el.iter(qn("w:r"))):
        content = [c for c in run if c.tag != qn("w:rPr")]
        if not any(c.tag == qn("w:br") for c in content) or len(content) == 1:
            continue
        rpr = run.find(qn("w:rPr"))
        groups, group = [], []
        for child in content:
            if child.tag == qn("w:br"):
                if group:
                    groups.append(group)
                groups.append([child])
                group = []
            else:
                group.append(child)
        if group:
            groups.append(group)
        anchor = run
        for g in groups:
            new = run.makeelement(qn("w:r"), {})
            if rpr is not None:
                new.append(copy.deepcopy(rpr))
            for child in g:
                new.append(child)
            anchor.addnext(new)
            anchor = new
        run.getparent().remove(run)


def lines_of(paragraph_el):
    """Group the runs of a paragraph into lines, split at w:br. Returns [(text, [run elements])]."""
    lines, text, runs = [], "", []
    for run in paragraph_el.iter(qn("w:r")):
        if run.find(qn("w:br")) is not None:
            lines.append((text, runs))
            text, runs = "", []
            continue
        text += "".join(t.text or "" for t in run.iter(qn("w:t")))
        runs.append(run)
    lines.append((text, runs))
    return lines


def first_text(runs):
    for run in runs:
        for t in run.iter(qn("w:t")):
            if t.text and t.text.strip():
                return t
    return None


# ---------------------------------------------------------------- checks and fixes

def merge_paragraphs(cell):
    paragraphs = cell._tc.findall(qn("w:p"))
    if len(paragraphs) < 2:
        return False
    first = paragraphs[0]
    for p in paragraphs[1:]:
        runs = [c for c in p if c.tag in (qn("w:r"), qn("w:hyperlink"))]
        if not runs:
            cell._tc.remove(p)
            continue
        br_run = first.makeelement(qn("w:r"), {})
        sample = runs[0].find(qn("w:rPr")) if runs[0].tag == qn("w:r") else None
        if sample is not None:
            br_run.append(copy.deepcopy(sample))
        br_run.append(br_run.makeelement(qn("w:br"), {}))
        first.append(br_run)
        for r in runs:
            first.append(r)
        cell._tc.remove(p)
    return True


def handle(uc, label, label_cell, cell, apply, found):
    def note(kind):
        found[kind].append(uc)

    label_runs = [r for p in label_cell.paragraphs for r in p.runs if r.text.strip()]
    if label_runs and not all(r.bold for r in label_runs):
        note("label not bold")
        if apply:
            for r in label_runs:
                r.bold = True

    if len(cell.paragraphs) > 1:
        note("cell split into several paragraphs")
        if apply:
            merge_paragraphs(cell)

    for p in cell._tc.findall(qn("w:p")):
        split_runs_at_breaks(p)  # check mode too: the document is not saved then
        flow = label in ("Alternative Flows", "Exception Flows")
        for text, runs in lines_of(p):
            if not text.strip():
                continue
            visible = [r for r in runs if "".join(t.text or "" for t in r.iter(qn("w:t"))).strip()]
            italic = any(r.find(qn("w:rPr")) is not None and r.find(qn("w:rPr")).find(qn("w:i")) is not None
                         and r.find(qn("w:rPr")).find(qn("w:i")).get(qn("w:val")) not in ("0", "false")
                         for r in visible)
            bold = [r.find(qn("w:rPr")) is not None and r.find(qn("w:rPr")).find(qn("w:b")) is not None
                    and r.find(qn("w:rPr")).find(qn("w:b")).get(qn("w:val")) not in ("0", "false")
                    for r in visible]
            head = flow and FLOW_HEAD.match(text)
            want_bold = bool(head)
            if italic:
                note("italic in " + label)
            if any(b != want_bold for b in bold):
                note(("AF/EX title not bold" if head else "flow step bold") if flow else "bold in " + label)
            if apply:
                for r in runs:
                    set_flag(r, "i", False)
                    set_flag(r, "b", want_bold)
            if head and COLON_HEAD.match(text):
                note("AF/EX title uses ':'")
                if apply:
                    t = first_text(runs)
                    if t is not None and COLON_HEAD.match(t.text):
                        t.text = COLON_HEAD.sub(lambda m: m.group(1) + " – ", t.text, count=1)
                        t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
            if head:
                name = re.sub(r"^\s*(?:AF|EX)-\d+\s*[–:-]\s*", "", text)
                words = [w for w in name.split() if w.isalpha() and len(w) > 3][1:]
                if words and all(w[0].isupper() for w in words):
                    note("AF/EX title in Title Case (fix by hand)")
            if label in ("Preconditions", "Postconditions") and PRE_POST.match(text):
                note("PRE/POST without '● '")
                if apply:
                    t = first_text(runs)
                    if t is not None:
                        t.text = "● " + t.text.lstrip()
                        t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")

    if label == "Exception Flows":
        blocks = re.split(r"\n(?=\s*EX-\d)", "\n" + cell.text)
        for block in blocks:
            if block.strip().startswith("EX") and not CLOSING.search(block.strip().split("\n")[-1]):
                note("EX without closing sentence (fix by hand)")
    if "Main Flow" in cell.text:
        note("'Main Flow' instead of 'Normal Flow' (fix by hand)")


def main(argv):
    if len(argv) not in (2, 4) or (len(argv) == 4 and argv[2] != "--apply"):
        print(__doc__)
        return 2
    apply = len(argv) == 4
    document = docx.Document(argv[1])
    found = defaultdict(list)
    count = 0
    for uc, table in uc_tables(document):
        count += 1
        labels = [label for label, _, _ in pairs(table)]
        if labels != LABELS:
            found["labels differ from the model"].append(uc)
        for label, label_cell, cell in pairs(table):
            handle(uc, label, label_cell, cell, apply, found)
    print(f"{count} UC tables")
    for kind in sorted(found):
        ucs = list(dict.fromkeys(found[kind]))
        print(f"- {kind} [{len(ucs)} UC]: {' '.join(str(u) for u in ucs)}")
    if apply:
        document.save(argv[3])
        print(f"written {argv[3]}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
