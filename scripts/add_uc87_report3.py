"""Add UC-87 Configure AI Service and close the other Report 3 gaps found by the UC-vs-code review (PR #509).

Usage:
    python scripts/add_uc87_report3.py <in.docx> <out.docx>

One-off script, kept so the change can be reviewed and rerun. It runs on the DOCX from before this change and
refuses one that already has UC-87. Every addition is taken from the code:

* UC-87 Configure AI Service (FE-03, after UC-13) with UC-87a View, UC-87b Update, UC-87c Test — the
  "Cấu hình AI" screen (`AiSettingsEndpoints`, `AiSettingsService`, ADR-0054, ADR-0056); its row in 4.2,
  two rows in the 4.4 permission matrix, a Screen List row and one sentence in 4.3.1;
* UC-16 AF-2: the meeting's status history (`GET /api/meetings/{id}/history`, `MeetingReadPolicy`);
* V.2: MSG408–MSG413 (UC-87) and MSG457–MSG462 (UC-79), which the code returns but V.2 lacked;
* 5.3 and III.16: the six background workers the inventory missed (JOB-03…JOB-08);
* III.17.6 API-06: the AI drafting service as it runs (in-house model or an admin-chosen API), not "planned";
* one Change Log row.
"""

import copy
import random
import re
import sys

import docx
from docx.text.paragraph import Paragraph
from lxml import etree

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
W14 = "http://schemas.microsoft.com/office/word/2010/wordml"
q = lambda t: f"{{{W}}}{t}"
PARAID = f"{{{W14}}}paraId"
XMLSPACE = "{http://www.w3.org/XML/1998/namespace}space"

SRC, OUT = sys.argv[1], sys.argv[2]
d = docx.Document(SRC)
body = d.element.body
random.seed(87)
used_ids = {el.get(PARAID) for el in body.iter() if el.get(PARAID)}
done = []


def new_paraid():
    while True:
        v = f"{random.randint(0x10000000, 0x7FFFFFFE):08X}"
        if v not in used_ids:
            used_ids.add(v)
            return v


def renew_ids(el):
    """For a copy: fresh paraIds, and no bookmarks, which would repeat the original's ids and names."""
    for x in el.iter():
        if x.get(PARAID):
            x.set(PARAID, new_paraid())
    for mark in list(el.iter(q("bookmarkStart"), q("bookmarkEnd"))):
        mark.getparent().remove(mark)


def text(el):
    return "".join(t.text or "" for t in el.iter(q("t")))


def is_heading(el):
    if el.tag != q("p"):
        return False
    style = Paragraph(el, d).style
    return style is not None and style.name.lower().startswith("heading")


def heading(prefix):
    """The heading paragraph whose text starts with prefix (exactly one)."""
    found = [el for el in body.iterchildren() if is_heading(el) and text(el).strip().startswith(prefix)]
    assert len(found) == 1, (prefix, len(found))
    return found[0]


def next_table(el):
    while el is not None and el.tag != q("tbl"):
        el = el.getnext()
    return el


def set_para(p, value, bold=False):
    """Keep the paragraph's first run (its formatting) with the new text; drop the other runs."""
    runs = p.findall(q("r"))
    if not runs:  # the text sits inside a hyperlink or similar: keep the first run's formatting only
        nested = next(p.iter(q("r")), None)
        r = etree.SubElement(p, q("r"))
        if nested is not None and nested.find(q("rPr")) is not None:
            r.append(copy.deepcopy(nested.find(q("rPr"))))
        runs = [r]
    for extra in runs[1:]:
        p.remove(extra)
    for child in list(p):  # bookmarks stay: a heading's _Toc bookmark is what the table of contents points at
        if child.tag not in (q("pPr"), q("r"), q("bookmarkStart"), q("bookmarkEnd")):
            p.remove(child)
    r = runs[0]
    for c in list(r):
        if c.tag != q("rPr"):
            r.remove(c)
    rpr = r.find(q("rPr"))
    if rpr is None:
        rpr = etree.SubElement(r, q("rPr"))
        r.insert(0, rpr)
    for b in rpr.findall(q("b")) + rpr.findall(q("bCs")):
        rpr.remove(b)
    if bold:
        rpr.insert(0, etree.Element(q("b")))
    t = etree.SubElement(r, q("t"))
    t.text = value
    t.set(XMLSPACE, "preserve")


TITLE = re.compile(r"^(AF|EX)-\d+ – ")


def set_cell(tc, lines):
    """One paragraph per line, cloned from the cell's first paragraph; AF/EX titles bold (UC-23a style)."""
    if isinstance(lines, str):
        lines = [lines]
    ps = tc.findall(q("p"))
    template = copy.deepcopy(ps[0])
    for p in ps:
        tc.remove(p)
    for line in lines:
        p = copy.deepcopy(template)
        renew_ids(p)
        set_para(p, line, bold=bool(TITLE.match(line)))
        tc.append(p)


def clone_after(anchor, source):
    el = copy.deepcopy(source)
    renew_ids(el)
    anchor.addnext(el)
    return el


def add_row(tbl, after_tr, values):
    tr = copy.deepcopy(after_tr)
    renew_ids(tr)
    for tc, value in zip(tr.findall(q("tc")), values):
        set_cell(tc, value)
    after_tr.addnext(tr)
    return tr


def row_starting(tbl, *cells):
    for tr in tbl.findall(q("tr")):
        values = [text(tc).strip() for tc in tr.findall(q("tc"))]
        if values[: len(cells)] == list(cells):
            return tr
    raise AssertionError(f"row {cells} not found")


def append_to(p, sentence):
    ts = [t for t in p.iter(q("t")) if t.text]
    ts[-1].text = ts[-1].text.rstrip() + " " + sentence
    ts[-1].set(XMLSPACE, "preserve")


if any(is_heading(el) and re.search(r"\bUC-87\b", text(el)) for el in body.iterchildren()):
    sys.exit("This DOCX already has UC-87: run the script on the DOCX from before the change.")

# ---------------------------------------------------------------- UC-87 specification
UC87 = {
    "UC-87a – View AI Settings": {
        "actors": ("System Admin", "None"),
        "Description": "Allows the System Admin to see which AI service writes the AI drafts of Normal meetings — the team's own model or an outside API — with the API's address and model, whether an access key is stored, and who changed the settings last and when. (BR-129)",
        "Preconditions": [
            "● PRE-1: The System Admin is signed in.",
            "● PRE-2: The System Admin's role holds the User administration permission.",
        ],
        "Postconditions": [
            "● POST-1: The current AI settings are displayed; of a stored access key only its last four characters are shown, never the key itself. (BR-129)",
            "● POST-2: Nothing is changed.",
        ],
        "Normal Flow": [
            "View AI Settings",
            "1. The System Admin opens Cấu hình AI [AI Settings] from the Administration menu.",
            "2. The system checks that the System Admin's role holds the User administration permission.",
            "3. The system displays the chosen service — Máy local [In-house model] or API (chuẩn OpenAI) [OpenAI-compatible API] — the name of the in-house model and whether the server has it configured, the API address and model, whether an access key is stored with its last four characters, and who last changed the settings and when. (BR-129)",
            "4. The System Admin reviews the settings and may change them (UC-87b) or test the service (UC-87c).",
        ],
        "Alternative Flows": [
            "AF-1 – Nothing saved yet",
            "1. At Step 3, no administrator has saved AI settings yet.",
            "2. The system shows the in-house model as the chosen service, with empty API fields and no stored key.",
            "3. The flow resumes at Step 4 of the Main Flow.",
        ],
        "Exception Flows": [
            "EX-1 – Settings cannot be loaded",
            "1. At Step 3, the system cannot read the settings.",
            "2. The system displays MSG410 – “Unable to load the AI settings.” and shows no settings.",
            "3. The System Admin may reload the page; the use case ends.",
        ],
    },
    "UC-87b – Update AI Settings": {
        "actors": ("System Admin", "None"),
        "Description": "Allows the System Admin to choose which AI service writes the AI drafts of Normal meetings and, for an outside API, to enter its address, model and access key. A meeting that is not Normal is always written by the in-house model, whatever is chosen. (BR-129)",
        "Preconditions": [
            "● PRE-1: The System Admin is signed in and the role holds the User administration permission.",
            "● PRE-2: The System Admin has opened the AI settings (UC-87a).",
        ],
        "Postconditions": [
            "● POST-1: The chosen service and the API address and model are saved; a new access key is stored encrypted and is never shown again, only its last four characters. (BR-129)",
            "● POST-2: Drafts asked for from then on use the chosen service for Normal meetings and the in-house model for every other meeting. (BR-129)",
            "● POST-3: The change is recorded in the audit log, without the key.",
        ],
        "Normal Flow": [
            "Update AI Settings",
            "1. The System Admin chooses Máy local [In-house model] or API (chuẩn OpenAI) [OpenAI-compatible API].",
            "2. For the API, the System Admin enters the API address and the model name and, when no key is stored yet or to replace it, pastes the access key.",
            "3. The System Admin selects Save.",
            "4. The system checks the values: the API address starts with https:// and has at most 500 characters, the model name is one word of at most 100 characters, and a new key has 8 to 500 characters without spaces; when the API is chosen, the address and model are required and a key must be stored already or entered. API values kept while the in-house model is chosen are checked the same way and kept for later.",
            "5. The system saves the settings, stores a new key encrypted and records the change in the audit log without the key. (BR-129)",
            "6. The system displays MSG408 – “AI settings saved.” and shows the saved settings, with only the last four characters of the key.",
        ],
        "Alternative Flows": [
            "AF-1 – Keep the stored key",
            "1. At Step 2, the System Admin leaves the access key empty while a key is stored.",
            "2. The system keeps the stored key; the flow resumes at Step 4 of the Main Flow.",
            "AF-2 – Remove the stored key",
            "1. At Step 2, with the in-house model chosen, the System Admin removes the stored key.",
            "2. At Step 5, the system deletes the key; the flow resumes at Step 6 of the Main Flow.",
        ],
        "Exception Flows": [
            "EX-1 – Settings are invalid",
            "1. At Step 4, a value breaks the rules of Step 4, the API is chosen without a stored or entered key, or the key would be removed while the API is chosen.",
            "2. The system displays MSG409 – “Invalid AI settings.” and highlights the affected fields. Nothing is saved.",
            "3. The System Admin may correct the values; the flow resumes at Step 2 of the Main Flow.",
            "EX-2 – Settings cannot be saved",
            "1. At Step 5, the system cannot save the settings.",
            "2. The system displays MSG411 – “Unable to save the AI settings. Please try again.” Nothing is changed.",
            "3. The System Admin may try again from Step 3 of the Main Flow.",
        ],
    },
    "UC-87c – Test AI Connection": {
        "actors": ("System Admin", "AI drafting service"),
        "Description": "Allows the System Admin to check, before a meeting, that an AI service answers, by making one short call with the values on the form. Nothing is saved.",
        "Preconditions": [
            "● PRE-1: The System Admin is signed in and the role holds the User administration permission.",
            "● PRE-2: The System Admin has opened the AI settings (UC-87a).",
        ],
        "Postconditions": [
            "● POST-1: The result is shown: the model that answered and how long the call took, or why it failed.",
            "● POST-2: No setting is saved or changed.",
        ],
        "Normal Flow": [
            "Test AI Connection",
            "1. The System Admin selects Thử kết nối [Test connection] for the chosen service.",
            "2. For the API, the system checks the address, model and key on the form as in Step 4 of UC-87b, using the stored key when the form gives none; for the in-house model it ignores the API fields.",
            "3. The system makes one short call to that service: the API on the form, or the in-house model the server is configured with, and its backup machine when there is one.",
            "4. The system displays MSG412 – “Connected to {model}.” with the time the call took.",
        ],
        "Alternative Flows": ["N/A"],
        "Exception Flows": [
            "EX-1 – Values are invalid",
            "1. At Step 2, the address, model or key breaks the rules of UC-87b Step 4.",
            "2. The system displays MSG409 – “Invalid AI settings.” and highlights the affected fields. No call is made.",
            "3. The System Admin may correct the values; the flow resumes at Step 1 of the Main Flow.",
            "EX-2 – The service does not answer",
            "1. At Step 3, the service refuses the key, cannot be reached, is not configured on the server, or returns an error.",
            "2. The system displays MSG413 with the reason. Nothing is saved.",
            "3. The use case ends; the System Admin may correct the settings (UC-87b) and test again.",
        ],
    },
}

h_parent_src = heading("3.3 UC-12 – Manage Roles")
h_sub_src = heading("3.3.1 UC-12a – Create Role")
tbl_src = next_table(h_sub_src)
anchor = next_table(heading("3.4 UC-13 – View Audit Log"))

anchor = clone_after(anchor, h_parent_src)
set_para(anchor.find(q("p")) if anchor.tag != q("p") else anchor, "3.5 UC-87 – Configure AI Service")
for number, (title, spec) in enumerate(UC87.items(), 1):
    anchor = clone_after(anchor, h_sub_src)
    set_para(anchor, f"3.5.{number} {title}")
    anchor = clone_after(anchor, tbl_src)
    for tr in anchor.findall(q("tr")):
        tcs = tr.findall(q("tc"))
        label = text(tcs[0]).strip()
        if label == "Primary Actors":
            set_cell(tcs[1], spec["actors"][0])
            set_cell(tcs[3], spec["actors"][1])
        else:
            set_cell(tcs[1], spec[label])
done.append("UC-87 specification (87a, 87b, 87c)")

# ---------------------------------------------------------------- 4.2, 4.3.1, 4.4, Screen List
uc_table = next_table(heading("4.2 Use Cases"))
add_row(uc_table, row_starting(uc_table, "UC-13"), [
    "UC-87", "Configure AI Service", "Role-Based Access Control",
    "Choose the AI service that writes meeting drafts, enter its access details and test it; see the detailed UC-87a–UC-87c specifications.",
])
admin = heading("4.3.1 UCs for System Admin").getnext()
append_to(admin, "The Admin also chooses and tests the AI service that writes the AI drafts of meetings (UC-87).")
matrix = next_table(heading("4.4 Permission Matrix"))
last = row_starting(matrix, "Role & permission", "Delete role")
last = add_row(matrix, last, ["AI settings", "View", "Full", "No", "No", "No", "No", "No", "No"])
add_row(matrix, last, ["AI settings", "Update / test connection", "Full", "No", "No", "No", "No", "No", "No"])
screens = next_table(heading("b. Screen List"))
add_row(screens, row_starting(screens, "3.3"), [
    "3.4", "AI Settings", "FE-03 Role-Based Access Control",
    "Allows the System Admin to choose the in-house model or an OpenAI-compatible API for the AI drafts of Normal meetings, enter the API's address, model and access key (never shown again), and test the connection (UC-87a–UC-87c).",
])
done.append("4.2 row, 4.3.1 sentence, 4.4 rows, Screen List row")

# ---------------------------------------------------------------- UC-16 AF-2: status history
uc16 = next_table(heading("4.3 UC-16 – View Meeting Details"))
af = row_starting(uc16, "Alternative Flows").findall(q("tc"))[1]
# Append paragraphs; the existing AF-1 keeps its own paragraphs and line breaks untouched.
template = af.findall(q("p"))[-1]
for line in [
    "AF-2 – View the status history",
    "1. At Step 5 of the Main Flow, the user looks at Lịch sử trạng thái [Status history] beside the Meeting details.",
    "2. The system lists the Meeting's status changes a page at a time, each with its action, the resulting status, who acted and when; a rejection reason is shown only to the Organizer, the assigned Approver and the current Chair and Secretary. (BR-022, BR-023)",
    "3. The use case resumes at Step 5 of the Main Flow.",
]:
    p = copy.deepcopy(template)
    renew_ids(p)
    set_para(p, line, bold=bool(TITLE.match(line)))
    af.append(p)
done.append("UC-16 AF-2")

# ---------------------------------------------------------------- V.2 messages
messages = next_table(heading("2. System Messages"))
rows = messages.findall(q("tr"))
number = max(int(text(tr.findall(q("tc"))[0]).strip()) for tr in rows[1:] if text(tr.findall(q("tc"))[0]).strip().isdigit())
NEW_MESSAGES = [
    ("MSG408", "Toast", "UC-87b – Update AI Settings / Main Flow", "AI settings saved."),
    ("MSG409", "Field error / Form error", "UC-87b / EX-1, UC-87c / EX-1", "Invalid AI settings."),
    ("MSG410", "Form error", "UC-87a / EX-1", "Unable to load the AI settings."),
    ("MSG411", "Form error", "UC-87b / EX-2", "Unable to save the AI settings. Please try again."),
    ("MSG412", "Inline result", "UC-87c – Test AI Connection / Main Flow", "Connected to {model}."),
    ("MSG413", "Inline result", "UC-87c / EX-2", "The reason the AI service gave or why it could not be reached."),
    ("MSG457", "Form error", "UC-79 / EX-3", "Only the current Chair may manage speaking turns."),
    ("MSG458", "Form error", "UC-79 – Register to Speak / Step 2", "You need to be in the meeting room to ask to speak."),
    ("MSG459", "Form error", "UC-79 – Register to Speak / invite", "Someone is already invited or speaking. End that turn first."),
    ("MSG460", "Form error", "UC-79 – Register to Speak", "This speaking request has changed. Reload the list."),
    ("MSG461", "Form error", "UC-79 – Register to Speak", "Unable to load the speaking list. Please try again."),
    ("MSG462", "Form error", "UC-79 – Register to Speak", "Unable to save the speaking turn. Please try again."),
]
last = rows[-1]
for code, kind, context, content in NEW_MESSAGES:
    number += 1
    last = add_row(messages, last, [str(number), code, kind, context, content])
done.append(f"V.2: {len(NEW_MESSAGES)} messages")

# ---------------------------------------------------------------- background jobs (5.3 and III.16)
JOBS = [
    ("Transcribe recordings", "Queue worker", "Transcribes waiting recordings one at a time on the in-house speech worker, with speaker attribution (UC-45c).",
     "16.3 Transcribe Recordings (JOB-03)", "Runs inside the backend and takes the waiting transcripts one at a time, because the in-house speech worker is CPU bound. It sends each recording's audio to the speech worker inside the system, never to an outside service, stores the passages with their speakers and suggested names, and records the progress so the Recordings tab can show it (UC-45c, BR-121)."),
    ("Write AI drafts", "Queue worker", "Writes requested AI drafts one at a time with the AI service chosen in UC-87.",
     "16.4 Write AI Drafts (JOB-04)", "Runs inside the backend and writes the queued AI drafts one at a time (UC-47): the in-house model for every meeting that is not Normal, the service chosen in UC-87 for a Normal meeting. A failed draft keeps its reason and can be asked for again (BR-129)."),
    ("Build combined recordings", "Scheduled worker", "Every 30 seconds, builds the combined recording of an ended Hybrid or Online meeting, and builds it again when a source changes.",
     "16.5 Build Combined Recordings (JOB-05)", "Runs inside the backend every 30 seconds, one meeting at a time, until no meeting is waiting. It builds the combined recording of a Hybrid or Online meeting once its sources have settled after the end, and again when a source changes, unless the Secretary erased it (UC-43a, BR-122)."),
    ("Erase expired recordings", "Scheduled worker", "Erases recordings and per-microphone sentences whose retention period has run out.",
     "16.6 Erase Expired Recordings (JOB-06)", "Runs inside the backend and erases the recordings, transcripts and per-microphone sentences 180 days after the first take, so the date shown to participants is kept; the erasure is recorded in the audit log (UC-44e, BR-110)."),
    ("Purge expired data", "Scheduled worker", "Removes orphaned files, draft votes left in cancelled meetings, old notifications, expired Guest codes and old email bodies.",
     "16.7 Purge Expired Data (JOB-07)", "Runs inside the backend at start-up and then at a configured interval. It removes audio files no recording uses, draft votes left in cancelled meetings, old notifications, expired Guest verification codes and the bodies of old emails (BR-077)."),
    ("Close overdue meetings", "Scheduled worker", "Every 15 minutes, cancels meetings not started a day after their planned start and ends meetings not ended a day after their planned end.",
     "16.8 Close Overdue Meetings (JOB-08)", "Runs inside the backend every 15 minutes. It cancels a meeting nobody started 24 hours after its planned start and ends a meeting nobody ended 24 hours after its planned end, recording each change in the meeting's history (BR-027, BR-029)."),
]
job_table = next_table(heading("5.3 Background Job Inventory"))
last = job_table.findall(q("tr"))[-1]
job_no = int(text(last.findall(q("tc"))[0]).strip())
for name, group, short, _h, _long in JOBS:
    job_no += 1
    last = add_row(job_table, last, [str(job_no), name, group, short])
h_job = heading("16.2 Deliver Meeting Invitation Emails (JOB-02)")
p_job = h_job.getnext()
anchor = p_job
for _name, _group, _short, title, long in JOBS:
    anchor = clone_after(anchor, h_job)
    set_para(anchor, title)
    anchor = clone_after(anchor, p_job)
    set_para(anchor, long)
done.append(f"jobs: {len(JOBS)} in 5.3 and III.16")

# ---------------------------------------------------------------- API-06
api = heading("17.6 Google AI Provider")
set_para(api, "17.6 AI drafting service – meeting drafts (API-06)")
API = {
    "URL": "The team's own language model, reached over the team's private network (ADR-0054), or an OpenAI-compatible API over HTTPS that the System Admin chooses for Normal meetings (UC-87b, ADR-0056).",
    "Purpose": "Writes the three sections of a meeting's AI draft from the transcript, the agenda and the vote results (UC-47). Transcription and speaker identification are not part of this interface: they run inside the system.",
    "Caller": "TrustCabinet backend background processing (JOB-04) → the in-house model, or the chosen API for a Normal meeting.",
    "Auth": "The in-house model needs no key. The API key is entered by the System Admin, stored encrypted and never shown again (UC-87b, BR-129).",
    "Request / Response": "Input: the transcript text, the agenda and the vote results of one meeting, never audio or voice data. Output: the three sections of the draft; the model and the time it read the transcript are stored with the draft.",
    "Failure handling": "A failed draft keeps its reason and can be asked for again; the System Admin can test the service at any time (UC-87c). The AI never writes formal minutes, and the draft stays unreviewed until the Chair or the Secretary reviews it.",
    "Status": "In use (FE-10, UC-47, UC-87). A meeting that is not Normal is always written by the in-house model.",
}
api_table = next_table(api)
for tr in api_table.findall(q("tr")):
    tcs = tr.findall(q("tc"))
    label = text(tcs[0]).strip()
    assert label in API, label
    set_cell(tcs[1], API[label])
done.append("API-06")

# ---------------------------------------------------------------- Change Log
changelog = next_table(heading("Change Log"))
add_row(changelog, changelog.findall(q("tr"))[-1], [
    "24/11/2026", "A, M", "HuyNQ",
    "[UC-vs-code review, PR #509] UC-87 Configure AI Service added (UC-87a–87c, MSG408–413, ADR-0054/0056); UC-16 status history, UC-79 messages MSG457–462, background jobs JOB-03–08 and API-06 brought in line with the code.",
])
done.append("Change Log row")

d.save(OUT)
print("\n".join(done))
