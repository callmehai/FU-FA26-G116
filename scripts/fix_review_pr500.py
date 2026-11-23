"""Fix the PR #500 review in Report 3: merged EX list every message, restore business EX, drop infra EX.

Usage: python scripts/fix_review_pr500.py [--write]
"""
import re
import sys
from pathlib import Path

import docx
from docx.table import Table
from docx.text.paragraph import Paragraph

DOCX = "docs/08-reports/source/Report_3_Software_Requirement_Specification.docx"
OLD_MD = sys.argv[sys.argv.index("--old") + 1]
NL = chr(10)
LQ, RQ = chr(0x201c), chr(0x201d)

old_md = open(OLD_MD, encoding="utf-8").read()


def old_ex(uc):
    secs = re.split(r"(?m)^#{3,4} (UC-\d+[a-z]?)\b", old_md)
    for i in range(1, len(secs), 2):
        if secs[i] == "UC-" + uc:
            m = re.search(r"\*\*Exception Flows\*\* \|(.*?)\|\n", secs[i + 1], re.S)
            return m.group(1).strip().split("<br>")
    raise KeyError(uc)


def old_block(uc, n):
    bl = old_ex(uc)
    k = next(j for j, x in enumerate(bl) if re.match(r"EX-%d\b" % n, x))
    out = [bl[k]]
    for x in bl[k + 1:]:
        if re.match(r"EX-\d+", x):
            break
        out.append(x)
    return NL.join(out)


def q(text):
    return LQ + text + RQ


EDITS = {}  # uc -> list of (old, new)


def e(uc, old, new):
    EDITS.setdefault(uc, []).append((old, new))


# ---- Item 1: merged EX name every message ----
e("55", "attempts to submit without selecting exactly one option. (BR-271)",
  "attempts to submit without selecting exactly one option, or submits an option that does not belong to this Vote. (BR-271)")
e("55", 'Select one option before submitting your ballot."',
  'Select one option before submitting your ballot." When the option does not belong to this Vote, the system displays MSG223 – '
  + q("The selected option is not available for this vote."))
e("55", "a person added after opening is not added to the list. (BR-268, BR-269)",
  "a person added after opening is not added to the list. (BR-268, BR-269) Or, at Step 4, the actor has not confirmed that they have read every vote document.")
e("55", 'MSG224 – "You are not eligible to cast a ballot in this vote."',
  'MSG224 – "You are not eligible to cast a ballot in this vote." When the documents are not all confirmed, the system displays MSG252 – '
  + q("Confirm that you have read all vote documents before casting your ballot.") + " and lists the documents still to read.")
e("55", "even if the Vote still appears Open. (BR-165)",
  "even if the Vote still appears Open. (BR-165) Or, at Step 4, the actor has already voted, including two submissions sent at the same time.")
e("55", 'MSG225 – "This vote is no longer accepting ballots."',
  'MSG225 – "This vote is no longer accepting ballots." When the actor has already voted, the system displays MSG226 – '
  + q("Your ballot has already been recorded. It cannot be changed or withdrawn.") + " and the recorded ballot stays unchanged.")

e("74", "of any Open Vote linked to this document.",
  "of any Open Vote linked to this document. Or no Vote linked to this document is Open any more.")
e("74", 'MSG224 – "You are not eligible to cast a ballot in this vote."',
  'MSG224 – "You are not eligible to cast a ballot in this vote." When no Open Vote remains, the system displays MSG225 – '
  + q("This vote is no longer accepting ballots."))

e("33a", "EX-2 – Selection is empty, unavailable or already attached",
  "EX-2 – Selection is empty, unavailable or already attached, or a file is invalid")
e("33a", "or does not belong to the actor.",
  "or does not belong to the actor. Or a selected document is already in the Meeting. Or, in AF-1, a file is missing or invalid.")
e("33a", "from your personal library." + RQ + ".",
  "from your personal library." + RQ + " When a selected document is already in the Meeting, the system displays MSG165 – "
  + q("One or more selected documents are already attached to the Meeting.")
  + " In AF-1, when a file is not a valid PDF or DOCX or is larger than 10 MB, the system displays MSG164 – "
  + q("Select valid PDF or DOCX files, each no larger than 10 MB.") + ".")

e("33b", "a selected document is no longer attached to this Meeting.",
  "a selected document is no longer attached to this Meeting. Or the Agenda Item is no longer in the Meeting, or a selected document is already linked to it.")
e("33b", "Refresh the document list." + RQ,
  "Refresh the document list." + RQ + " When the Agenda Item is no longer in the Meeting, the system displays MSG242 – "
  + q("Select an available Agenda Item in this Meeting.") + "; when a document is already linked, MSG243 – "
  + q("One or more selected documents are already attached to the Agenda Item."))

e("49", "At Step 2, no draft has finished being written.",
  "At Step 2, no draft has finished being written. Or the AI is still writing the draft.")
e("49", "There is no draft to correct or review." + RQ,
  "There is no draft to correct or review." + RQ + " When the AI is still writing, the system displays MSG387 – "
  + q("The AI is writing the draft, please wait."))

e("81c", "or has more than one speaker. (BR-363)",
  "or has more than one speaker. (BR-363) Or the recording is not of a supported audio type.")
e("81c", "Please read the full sample text clearly." + RQ,
  "Please read the full sample text clearly." + RQ + " An unsupported audio type shows MSG292 – "
  + q("Unsupported audio format. Use MP3, M4A, WAV, WebM, or OGG."))

e("86b", "the recording is not of a supported audio type.",
  "the recording is not of a supported audio type. Or the recording is too short, too long, too quiet, distorted or has more than one speaker.")
e("86b", "Use MP3, M4A, WAV, WebM, or OGG." + RQ,
  "Use MP3, M4A, WAV, WebM, or OGG." + RQ + " A recording that fails the quality rules shows MSG309 – "
  + q("Voice sample did not meet quality requirements. Please read the full sample text clearly."))

# ---- Item 3: restore business EX ----
e("06b", "ZZ_APPEND", old_block("06b", 2).replace("EX-2: Email Already in Use", "EX-2: Email already in use"))

# UC-07 is rebuilt as a whole below; UC-37 edits:
e("37", "EX-1 – Meeting has ended", "EX-1 – Meeting has ended or was cancelled")
e("37", "including while the actor was in the lobby.",
  "including while the actor was in the lobby, or the Meeting was cancelled. (BR-299)")
e("37", "No connection is made. The system displays MSG331", "No connection is made. When the Meeting has ended, the system displays MSG331")
e("37", " (BR-143) A Guest does not see MSG331",
  " (BR-143) When the Meeting was cancelled, no Join room button is offered and the system displays MSG350 – "
  + q("The meeting was cancelled, so the meeting room does not open.") + " A Guest does not see MSG331")
e("37", "ZZ_REPLACE_EX3", old_block("37", 10).replace("EX-10 –", "EX-3 –"))

WHOLE = {
    "07": NL.join([
        "EX-1: User account is not available for password reset",
        "1. At Step 4 of the Main Flow, the system cannot find the selected account, or determines that it is deactivated or has no password because it only signs in with Google (BR-22). The User Accounts list already makes Reset Password unavailable for such accounts, so this happens when the list is out of date.",
        "2. No password reset link is created and no email is sent.",
        "3. When the account no longer exists, the system informs the System Admin that the account no longer exists and refreshes the User Accounts list; this situation has no message code. Otherwise the system displays MSG14 – " + q("Password reset is not available for this account.") + " When the system can tell which reason applies, it updates the account in the list and states the reason instead (the account is deactivated, or it signs in with Google only).",
        "4. The use case ends.",
        old_block("07", 4).replace("EX-4:", "EX-2:"),
        old_block("07", 5).replace("EX-5:", "EX-3:"),
    ]),
    "32a": None, "45a": "N/A", "66": "N/A", "81a": "N/A", "81e": "N/A", "81f": "N/A", "84f": "N/A",
}

d = docx.Document(DOCX)
prev = None
changed = []
for el in d.element.body.iterchildren():
    if el.tag.endswith("}p"):
        p = Paragraph(el, d)
        if p.text.strip():
            prev = p
        continue
    if not el.tag.endswith("}tbl") or prev is None:
        continue
    hm = re.search(r"UC-(\d+[a-z]?)\b", prev.text)
    if not hm:
        continue
    uc = hm.group(1)
    if uc not in EDITS and uc not in WHOLE and uc != "06c":
        continue
    t = Table(el, d)
    if uc == "06c":
        n = 0
        for tt in el.iter("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t"):
            if tt.text and "(BR-424, UC-84c EX-2)" in tt.text:
                tt.text = tt.text.replace("(BR-424, UC-84c EX-2)", "(BR-424, UC-84c)")
                n += 1
        changed.append(f"UC-06c ref x{n}")
        continue
    row = next((r for r in t.rows if r.cells[0].text.strip() == "Exception Flows"), None)
    if row is None:
        continue
    cell = row.cells[-1]
    text = NL.join(pp.text for pp in cell.paragraphs)
    if uc in WHOLE:
        if WHOLE[uc] is None:  # 32a: drop EX-2 (infrastructure)
            i = text.index("EX-2 – Document list cannot be loaded")
            new = text[:i].rstrip(NL)
        else:
            new = WHOLE[uc]
    else:
        new = text
        for old, rep in EDITS[uc]:
            if old == "ZZ_APPEND":
                new = new.rstrip() + NL + rep
            elif old == "ZZ_REPLACE_EX3":
                i = new.index("EX-3 – Room service is not configured")
                j = new.find(NL + "EX-4", i)
                new = new[:i] + rep + (new[j:] if j >= 0 else "")
            else:
                assert new.count(old) == 1, (uc, old[:60], new.count(old))
                new = new.replace(old, rep)
    cell.paragraphs[0].runs[0].text = new
    for r in cell.paragraphs[0].runs[1:]:
        r.text = ""
    for extra in cell.paragraphs[1:]:
        extra._p.getparent().remove(extra._p)
    changed.append("UC-" + uc)

print(changed)
missing = (set(EDITS) | set(WHOLE) | {"06c"}) - {c[3:].split(" ")[0] for c in changed}
print("missing:", sorted(missing))
if "--write" in sys.argv:
    d.save(DOCX)
    print("saved")
