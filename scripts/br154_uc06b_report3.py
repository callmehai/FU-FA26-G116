"""Apply the decisions after PR #509 (24/11/2026) to the Report 3 DOCX.

Usage:
    python scripts/br154_uc06b_report3.py <in.docx> <out.docx>

One-off script, kept so the change can be reviewed and rerun; it refuses a DOCX that already has BR-154.
* BR-125 (old BR-467) is split: BR-125 keeps what is carried over, the new BR-154 says what cannot be matched
  leaves only a trace in the audit log. BR-001…BR-153 keep their numbers (ADR-0079).
* BR-153 (old BR-468) keeps the visibility rule; "combined recordings add no hours" moves to BR-151, the formulas.
* UC-54c (old UC-45c) states the four matching thresholds of old BR-467 (`TranscriptCarry`).
* UC-06b is written as the code saves it: Full Name, Email and System Role in one save (`UserService.UpdateAsync`).
Every replacement must apply exactly once, or the script stops without writing.
"""

import copy
import random
import re
import sys

import docx
from lxml import etree

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
W14 = "http://schemas.microsoft.com/office/word/2010/wordml"
q = lambda t: f"{{{W}}}{t}"
PARAID = f"{{{W14}}}paraId"
XMLSPACE = "{http://www.w3.org/XML/1998/namespace}space"

SRC, OUT = sys.argv[1], sys.argv[2]
d = docx.Document(SRC)
body = d.element.body
random.seed(154)
used = {el.get(PARAID) for el in body.iter() if el.get(PARAID)}


def text(el):
    return "".join(t.text or "" for t in el.iter(q("t")))


def cell_rows(tbl):
    for tr in tbl.iter(q("tr")):
        yield tr, [text(tc).strip() for tc in tr.findall(q("tc"))]


br_table = next(t for t in body.iter(q("tbl"))
                if [text(tc).strip() for tc in t.find(q("tr")).findall(q("tc"))][:2] == ["ID", "Rule"])
rows = {cells[0]: tr for tr, cells in cell_rows(br_table) if re.fullmatch(r"BR-\d{3}", cells[0])}
if "BR-154" in rows:
    sys.exit("This DOCX already has BR-154: run the script on the DOCX from before the change.")


def set_cell(tc, value):
    ps = tc.findall(q("p"))
    for extra in ps[1:]:
        tc.remove(extra)
    p = ps[0]
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


RULES = {
    "BR-125": "When transcription runs again, hand-assigned names, corrected text and moved sentences carry over to the matching sentence or voice.",
    "BR-151": "Attendance rate, time to minutes, meeting duration and recorded hours follow their formulas; combined recordings add no hours; Meetings missing the data are left out of the averages.",
    "BR-153": "The report query counts only records the viewer can read: draft Votes and unissued minutes for the Chair and Secretary, recordings for participants.",
}
for rule_id, rule in RULES.items():
    set_cell(rows[rule_id].findall(q("tc"))[1], rule)
new = copy.deepcopy(rows["BR-153"])
for x in new.iter():
    if x.get(PARAID):
        while True:
            v = f"{random.randint(0x10000000, 0x7FFFFFFE):08X}"
            if v not in used:
                used.add(v)
                x.set(PARAID, v)
                break
for mark in list(new.iter(q("bookmarkStart"), q("bookmarkEnd"))):
    mark.getparent().remove(mark)
set_cell(new.findall(q("tc"))[0], "BR-154")
set_cell(new.findall(q("tc"))[1], "When transcription runs again, what cannot be matched leaves only a trace in the audit log, without the transcript text.")
rows["BR-153"].addnext(new)

# Text replacements, each inside one run; old -> new, applied exactly once.
REPLACE = [
    # UC-54c Normal Flow step 5: the matching thresholds of old BR-467 (TranscriptCarry).
    ("accepted notice version 1.2 or later. (BR-120, BR-123, BR-121)",
     "accepted notice version 1.2 or later. (BR-120, BR-123, BR-121) When the transcript is made again, a hand-assigned name "
     "goes with a new voice that shares, within what the old voice spoke, at least 60% of the new voice's words and at least "
     "2 seconds, leaving out sentences moved by hand, and wins over a name the system filled in; a corrected text or a moved "
     "sentence goes with the new sentence of the same take when both ends differ by no more than 500 ms or the overlap reaches "
     "at least 80% of the longer sentence; a sentence once corrected or moved is always carried, however short. (BR-125)"),
    # UC-54c POST-2: the log-only part is now BR-154.
    ("without the transcript text. (BR-125)", "without the transcript text. (BR-125, BR-154)"),
    # UC-79a step 5: recorded hours without combined recordings is a formula (BR-151).
    ("keeps the chosen top rows with a Total row. (BR-153)", "keeps the chosen top rows with a Total row. (BR-151, BR-153)"),
    # UC-06b, as UserService.UpdateAsync saves it.
    ("Allows the System Admin to update editable information of an existing user account within their permitted management scope.",
     "Allows the System Admin to update the Full Name, Email and System Role of an existing user account in one save. Changing "
     "the System Role here gives the same result, under the same rules, as UC-08."),
    ("3. The System Admin modifies the editable information, Full Name.",
     "3. The System Admin modifies the editable information: Full Name, Email and System Role."),
    ("5. The system validates the submitted information, including required fields and data format.",
     "5. The system validates the submitted information, including required fields, data format and Email uniqueness, and, "
     "when the System Role changes, the rules of UC-08, so that at least one active account keeps the User administration "
     "permission. (BR-005)"),
    ("6. The system updates the user account information.",
     "6. The system saves the Full Name, Email and System Role together; a new role takes effect from the user's next action. (BR-008)"),
]
counts = [0] * len(REPLACE)
for t in body.iter(q("t")):
    if not t.text:
        continue
    value = t.text
    for i, (a, b) in enumerate(REPLACE):
        if a in value:
            value = value.replace(a, b)
            counts[i] += 1
    if value != t.text:
        t.text = value
        t.set(XMLSPACE, "preserve")
bad = [REPLACE[i][0][:60] for i, c in enumerate(counts) if c != 1]
if bad:
    sys.exit("Not applied exactly once (the text has changed or sits across runs):\n  " + "\n  ".join(bad))

# Change Log
changelog = None
for el in body.iterchildren():
    if el.tag == q("p") and text(el).strip() == "Change Log":
        changelog = el
    elif changelog is not None and el.tag == q("tbl"):
        changelog = el
        break
last = changelog.findall(q("tr"))[-1]
row = copy.deepcopy(last)
for mark in list(row.iter(q("bookmarkStart"), q("bookmarkEnd"))):
    mark.getparent().remove(mark)
for x in row.iter():
    if x.get(PARAID):
        x.set(PARAID, f"{random.randint(0x10000000, 0x7FFFFFFE):08X}")
for tc, value in zip(row.findall(q("tc")), (
        "24/11/2026", "M, A", "HuyNQ",
        "[decisions after PR #509] BR-125 split (new BR-154: unmatched carry only in the audit log); combined recordings add no hours moved from BR-153 to BR-151; UC-54c states the carry thresholds; UC-06b edits Full Name, Email and System Role in one save, as built.")):
    set_cell(tc, value)
last.addnext(row)

# Only word/document.xml changes: every other part is copied from SRC byte for byte. The DOCX on `app` was last
# saved by Word, and a python-docx save would rewrite styles, headers, footers and settings and recompress images.
import io
import zipfile

buffer = io.BytesIO()
d.save(buffer)
document_xml = zipfile.ZipFile(buffer).read("word/document.xml")
with zipfile.ZipFile(SRC) as src, zipfile.ZipFile(OUT, "w") as dst:
    for info in src.infolist():
        data = document_xml if info.filename == "word/document.xml" else src.read(info.filename)
        dst.writestr(info, data, compress_type=info.compress_type)
print("BR-125/151/153 reworded, BR-154 added, UC-54c, UC-79a, UC-06b updated, Change Log row added")
