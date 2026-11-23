"""Chapter 3 catalogue: entity purposes, actors, UC inventory, screen descriptions, external services, background jobs.

Entity purposes, the UC inventory and the screen list are read from Report 3 (the SRS), shortened to one line each.
Chapter 3 is business language, so no vendor or technology names appear here.
"""

from __future__ import annotations

import re

import docx
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

from report7_lib import DOCS, Report, _text, is_heading

REPORT3 = DOCS / "08-reports" / "source" / "Report_3_Software_Requirement_Specification.docx"

FEATURES = {
    "FE-01": "Authentication & Account Security",
    "FE-02": "User & Profile Management",
    "FE-03": "Role-Based Access Control",
    "FE-04": "Meeting Management",
    "FE-05": "Agenda Management",
    "FE-06": "Meeting Participant Management",
    "FE-07": "Personal Document Library & Meeting Document Management",
    "FE-08": "Online Meeting Room Management",
    "FE-09": "Meeting Recording",
    "FE-10": "Transcription, Speaker Identification & Meeting Summary",
    "FE-11": "Voting Management",
    "FE-12": "Meeting Minutes & Approval",
    "FE-13": "Email & In-App Notification",
    "FE-15": "Reporting & Statistics",
    "FE-16": "Working Calendar & Meeting Time Search",
}

ACTORS = [
    ("Guest", "An external person without an internal account, invited to one specific meeting by name and email. "
              "The Guest verifies the invited email and then uses only the permitted functions of that meeting."),
    ("Meeting Organizer", "A user who creates, updates, submits for approval, publishes, reschedules and cancels the "
                          "meetings they organise, and assigns the Secretary when one is missing."),
    ("Meeting Approver", "A user assigned to one meeting approval request who approves it or rejects it with a reason. "
                         "The approval permission alone does not give access to every meeting."),
    ("System Admin", "Manages user accounts, roles, permissions and system-wide configuration, and reads the audit log."),
    ("Chair", "The user chairing one meeting: starts and ends it, runs the agenda, opens and closes votes and records "
              "their conclusions, moderates the online room, approves or returns the minutes."),
    ("Secretary", "The user supporting one meeting: prepares the agenda, manages the roster, attendance and documents, "
                  "takes notes, uploads audio, reviews transcripts and summaries, and prepares and submits the minutes."),
    ("Participant", "An internal user invited to one meeting: responds to the invitation, joins the online room, reads "
                    "and comments on documents, votes, and reads the issued minutes."),
    ("Voice sample collector", "A user holding the permission to collect voice samples on behalf of attendees who "
                               "consented to Voice ID (UC-86). By default this is the System Admin."),
    ("Identity provider (Google)", "Secondary actor. Confirms the identity of a user who signs in with a Google account (UC-02)."),
    ("Email delivery service", "Secondary actor. Delivers password-reset messages, meeting invitations, guest verification "
                               "codes and notification emails."),
    ("File storage service", "Secondary actor. Keeps the uploaded documents and returns them to authorised users "
                             "(UC-32 to UC-36)."),
    ("AI drafting service", "Secondary actor. Writes the draft of the meeting content from the transcript, the agenda and "
                            "the vote results (UC-47). It receives text only, never audio; it runs on the team's own server "
                            "unless the administrator chooses an outside service for a Normal meeting (BR-381)."),
]

EXTRA_SCREENS = [  # screens that exist in the application but not in Report 3's list
    ("5.4", "Meeting Detail – Notes Tab", "FE-05",
     "Allows the Secretary to take notes live, by agenda item or for the whole meeting; the Chair reads them (UC-82)."),
    ("8.8", "In-Room Speaking Panel", "FE-08",
     "Allows a person in the room to raise a hand; the Chair sees the hands in order and invites one person at a time (UC-79)."),
    ("10.3", "My Voice ID", "FE-10",
     "Allows a user to give or withdraw Voice ID consent, record and play their voice samples, and see who collected them (UC-81)."),
    ("10.4", "Voice Sample Collection", "FE-10",
     "Allows a holder of the collection permission to record, play and delete voice samples for another user (UC-86)."),
    ("10.5", "AI Settings", "FE-10",
     "Allows the System Admin to choose which AI service writes the meeting draft for a Normal meeting."),
    ("16.1", "Calendar Sharing", "FE-16",
     "Allows a user to request, share, answer, change and withdraw calendar sharing and to see the shares they take part in (UC-84)."),
    ("16.2", "Find Meeting Time", "FE-16",
     "Allows the Meeting Organizer to find free slots common to up to ten people, view shared calendars and create a Draft meeting from a slot (UC-85)."),
]

SCREEN_FIXES = {  # stale descriptions in Report 3, rewritten to match the application
    "8.5": "Allows eligible voters in the online room to follow the open Vote and cast a ballot without leaving the room.",
}

EXTERNAL = [
    ("Google sign-in", "Browser → Google → TrustCabinet",
     "The user authenticates with their Google account in a Google pop-up; TrustCabinet then checks the returned identity "
     "proof (issuer, audience, expiry, verified email) and signs the user in, linking the account by verified email on first use."),
    ("Email delivery", "TrustCabinet → email delivery service",
     "Sends meeting invitations (with a calendar file), password-reset links, guest verification codes and notification emails. "
     "Invitations wait in a queue and are retried when delivery fails."),
    ("Media server (online room)", "TrustCabinet ↔ media server ↔ Browser",
     "Carries the live audio, video and screen sharing of the online room. TrustCabinet issues each authorised person a "
     "short-lived room access, reads the room events (joined, left, screen shared) and closes the room or removes a person "
     "whose access has ended."),
    ("Speech recognition", "TrustCabinet → speech service (own server)",
     "Turns recorded meeting audio into a Vietnamese transcript with separated speakers and, for people who consented to "
     "Voice ID, suggests the speaker name. It runs on the team's own server; audio does not leave the system."),
    ("Language model (AI draft)", "TrustCabinet → language model (own server or chosen outside service)",
     "Writes the three-section draft of the meeting content from the transcript, the agenda and the vote results. "
     "Text only is sent; an outside service is used only for a Normal meeting and only when the administrator chooses it."),
    ("File storage", "TrustCabinet ↔ object storage",
     "Keeps the bytes of personal and meeting documents and recordings in a private store. TrustCabinet reads a file back "
     "only after checking the permission of the caller."),
]

JOBS = [
    ("Process notifications", "Scheduled", "Every 5 seconds: creates the meeting reminders that are due and pushes new in-app notifications to connected users."),
    ("Deliver invitation emails", "Queue", "Every 5 seconds, when email is enabled: sends the queued meeting invitation and notification emails and retries failed ones under the retry policy."),
    ("Reconcile meeting rooms", "Scheduled", "Every 30 seconds: closes rooms of ended meetings and removes people who lost access (deactivated, removed from the meeting), because room events are not guaranteed to arrive."),
    ("Build combined recording", "Queue", "Every 30 seconds: once the sources of an ended online or hybrid meeting settle, builds the combined recording, one meeting at a time."),
    ("Transcribe recordings", "Queue", "Every 3 seconds: runs waiting transcriptions one at a time, in order."),
    ("Write AI summary drafts", "Queue", "Every 3 seconds: writes queued meeting-content drafts one at a time, in order."),
    ("Close overdue meetings", "Scheduled", "Every 15 minutes: cancels meetings nobody started a day after their planned start and ends meetings nobody ended 24 hours after their planned end (BR-301)."),
    ("Erase expired recordings", "Scheduled", "Every 15 minutes: erases recordings and per-microphone sentences whose retention period has run out, and saves a room-microphone take that was left open for hours."),
    ("Apply data retention rules", "Scheduled", "At start-up and then hourly: removes orphaned audio files, vote drafts of cancelled meetings, old notifications, expired guest codes and old email bodies (ADR-0067)."),
    ("Purge orphaned files", "Scheduled", "At start-up and then hourly, while file storage is configured: deletes stored files no document points at once their 24-hour grace period is over, and writes one audit entry with the count."),
]

SCRUB = [  # (regex, replacement) applied to text read from Report 3
    (r"\bResend Email\b", "the email delivery service"),
    (r"\bSMTP\b", "email"),
    (r"\bLiveKit\b", "the media server"),
    (r"\bCloudflare R2\b", "file storage"),
]


def scrub(s: str) -> str:
    for pat, rep in SCRUB:
        s = re.sub(pat, rep, s)
    return s


def short(s: str, limit: int) -> str:
    s = re.sub(r"\s+", " ", s).strip()
    m = re.match(r"(.+?[.;])(\s|$)", s)
    first = m.group(1) if m and len(m.group(1)) >= 40 else s
    if len(first) > limit:
        first = first[:limit].rsplit(" ", 1)[0].rstrip(",;:( ") + " …"
    return scrub(first)


def read_report3():
    d = docx.Document(str(REPORT3))
    body = list(d.element.body)

    def tx(e):
        return "".join(t.text or "" for t in e.iter(qn("w:t")))

    entities = []
    for i, e in enumerate(body):
        m = re.fullmatch(r"Entity \d+: (\w+)", tx(e).strip())
        if m:
            entities.append((m.group(1), short(tx(body[i + 1]), 170)))

    def table_by_header(first, second):
        for t in d.tables:
            h = [c.text.strip() for c in t.rows[0].cells]
            if h[:2] == [first, second]:
                return [[c.text.strip() for c in row.cells] for row in t.rows[1:]]
        raise SystemExit(f"Report 3 table {first!r}/{second!r} not found")

    ucs = table_by_header("ID", "Use Case")
    screens = table_by_header("#", "Screen Name")
    return entities, ucs, screens


def _num_key(s: str):
    parts = re.match(r"(\d+)\.(\d+)(\w*)", s)
    return (int(parts.group(1)), int(parts.group(2)), parts.group(3))


def tbl(r: Report, header, rows, widths, size=9):
    """r.table with the column widths also written to the grid and a fixed layout, so Word and LibreOffice honour them."""
    el = r.table(header, rows, widths, size)
    grid = el.find(qn("w:tblGrid"))
    for col, w in zip(grid.findall(qn("w:gridCol")), widths):
        col.set(qn("w:w"), str(int(w * 1440)))
    pr = el.find(qn("w:tblPr"))
    for old in pr.findall(qn("w:tblLayout")):
        pr.remove(old)
    lay = OxmlElement("w:tblLayout")
    lay.set(qn("w:type"), "fixed")
    pr.append(lay)
    return el


def drop_instructions(r: Report, h):
    """Remove the template's [bracketed instruction] paragraphs and sample tables under `h`; drop a repeated caption."""
    prev = None
    for el in r.section(h):
        t = _text(el).strip()
        if el.tag == qn("w:tbl") or (el.tag == qn("w:p") and t.startswith("[")):
            r.body.remove(el)
        elif el.tag == qn("w:p") and t.startswith("Figure ") and t == prev:
            r.body.remove(el)
        prev = t if el.tag == qn("w:p") else None


def fill(r: Report):
    entities, ucs, screens = read_report3()

    # 1.3 entity purposes: keep the ERD figure, replace the sample table and the instruction.
    h = r.find_heading("1.3. Entity Relationship Diagram")
    drop_instructions(r, h)
    sec = r.section(h)
    anchor = next((e for e in sec if _text(e).strip() == "Entity Purposes"), None)
    els = [tbl(r, ["Entity", "Purpose"], entities, [1.6, 4.7], size=8)]
    if anchor is None:
        anchor = sec[-1]
        els.insert(0, r.text("Entity Purposes", bold=True))
    r.insert_after(anchor, els)

    # 1.4.1 actors
    h = r.find_heading("1.4.1 System Actors")
    r.replace_section(h, [tbl(r, ["#", "Actor", "Description"],
                                  [(i, a, d) for i, (a, d) in enumerate(ACTORS, 1)], [0.3, 1.5, 4.5], size=9)])

    # 1.4.3 use case inventory
    h = r.find_heading("1.4.3 Use Case Inventory")
    rows = []
    for uc_id, name, feat, desc in ucs:
        if re.fullmatch(r"UC-\d+[a-z]", uc_id):
            continue  # sub-use cases are listed inside their parent's specification
        rows.append((uc_id, scrub(name), scrub(feat), short(desc, 170)))
    rows.sort(key=lambda x: int(x[0][3:].rstrip("abcdef")))
    r.replace_section(h, [
        r.text(f"The system has {len(rows)} use cases in 15 features. Sub-use cases (UC-06a, UC-44b…) are specified "
               "under their parent in Chapter 3, Part 2. UC-51 is retired and keeps its number."),
        tbl(r, ["ID", "Use Case", "Feature", "Description"], rows, [0.55, 1.65, 1.35, 2.75], size=8),
    ])

    # 1.5 overview
    h = r.find_heading("1.5. System Functionalities")
    for el in r.section(h):
        if el.tag == qn("w:p") and _text(el).strip().startswith("["):
            r.body.remove(el)
    r.insert_after(h, [r.text("The system offers the functions below through screens for the internal roles, a guest "
                              "view for invited outside people, notification and email channels, and background jobs "
                              "that keep data in line with the rules.")])

    # 1.5.1a: only the template instruction goes; the figure stays
    drop_instructions(r, r.find_heading("a. Screen Flow"))

    # 1.5.1b screen descriptions
    rows = []
    for num, name, feat, desc in screens:
        code = feat.split()[0]
        rows.append((num, scrub(name), f"{code} {FEATURES.get(code, feat[len(code):].strip())}",
                     SCREEN_FIXES.get(num) or short(desc, 190)))
    for num, name, code, desc in EXTRA_SCREENS:
        rows.append((num, name, f"{code} {FEATURES[code]}", desc))
    rows.sort(key=lambda x: _num_key(x[0]))
    h = r.find_heading("b. Descriptions")
    r.replace_section(h, [tbl(r, ["#", "Screen Name", "Feature", "Description"], rows, [0.4, 1.5, 1.5, 2.9], size=8)])

    # 1.5.2 external services
    h = r.find_heading("1.5.2 External API Inventory")
    r.replace_section(h, [tbl(r, ["#", "API Name", "Calling Path", "Description"],
                                  [(i, *x) for i, x in enumerate(EXTERNAL, 1)], [0.3, 1.3, 1.5, 3.2], size=9)])

    # 1.5.3 background jobs
    h = r.find_heading("1.5.3 Background Job Inventory")
    r.replace_section(h, [tbl(r, ["#", "Job Name", "Group", "Description"],
                                  [(i, *x) for i, x in enumerate(JOBS, 1)], [0.3, 1.5, 0.9, 3.6], size=9)])
