"""Apply the Business Rule cut (406 -> 153, plan 08) and the actor rename to the Report 3 DOCX.

Usage:
    python scripts/apply_br_cut_report3.py <in.docx> <out.docx>

One-off script for the first BR renumbering (ADR-0079), kept so the change can be reviewed and rerun.
It maps OLD ids to new ones, so it must run on the DOCX from before the cut: it refuses a DOCX whose
Business Rules table already has the two columns `ID | Rule`.

What it does, outside the Change Log table:
* rewrites every (BR-...) citation with docs/01-requirements/09-bang-anh-xa-br-dot-1.md: an old id becomes
  its new id(s); an id moved to a use case, to NFR or to the shared UI conventions, or retired, is dropped;
  the audit group (BR-009/010/011) is narrowed by use case. A later row of table 09 for the same old id is a
  correction and wins over the earlier one (BR-158, BR-264, BR-312);
* rebuilds section V.1 as `ID | Rule`, BR-001...BR-153, in English from the plan's "Noi dung" column;
* writes a moved rule into an existing step of its use case (MOVED, BARE, FIX) wherever the citing step only
  pointed at the rule instead of stating it, or no use case described it — every one of the moved rules was
  checked against the step that cited it (review of PR #509) — and the NFR ones into existing rows of
  section IV.2 (plus one Performance row for speaker recognition). The UI-convention rules stay dropped, as
  the plan says. Wording follows the code where an old rule had drifted from it (BR-163);
* renames the actors Meeting Organizer -> Member and Meeting Approver -> Approver; adds one Change Log row.
"""

import collections
import copy
import random
import re
import sys
from pathlib import Path

import docx
from docx.table import Table
from docx.text.paragraph import Paragraph
from lxml import etree

ROOT = Path(__file__).resolve().parent.parent
MAP = ROOT / "docs/01-requirements/09-bang-anh-xa-br-dot-1.md"

DASH = "\u2013"
AUDIT = ("BR-009", "BR-010", "BR-011")


def load_map():
    m = {}
    for line in MAP.read_text(encoding="utf-8").splitlines():
        mm = re.match(r"^\| BR-(\d+) \| (.+?) \|$", line)
        if mm:
            m[int(mm.group(1))] = mm.group(2).strip()
    return m


MP = load_map()


def kind(n):
    t = MP[n].split(" — ", 1)[0]  # a correction row explains itself after " — "
    if t.startswith("BR-"):
        return "br", re.findall(r"BR-\d{3}", t)
    if t.startswith("Trùng"):
        target = int(re.search(r"BR-(\d+)", t).group(1))
        if target > 153:  # points at an old ID (BR-264/312 -> old BR-384): follow it
            return kind(target)
        return "br", [f"BR-{target:03d}"]
    if t.startswith("Đã ngừng"):
        return "retired", []
    if "Quy ước chung" in t:
        return "ui", []
    tgt = t.split("chuyển tới", 1)[1].strip()
    return ("nfr" if ("NFR" in tgt and "UC-" not in tgt) else "uc"), [tgt]


def audit_pick(ctx):
    """Pick the audit BR that matches the subject of the UC or section."""
    m = re.match(r"UC-(\d+)", ctx or "")
    if m:
        n = int(m.group(1))
        if n <= 13:
            return ["BR-009"]
        if n in (32, 64, 70, 84, 61, 65, 83):
            return ["BR-010"]
        if n in (33, 36, 86, 34, 71, 72):
            return ["BR-011"]
    return list(AUDIT)


def new_ids(n, ctx):
    k, ids = kind(n)
    if k != "br":
        return k, []
    if tuple(ids) == AUDIT:
        ids = audit_pick(ctx)
    return "br", ids


def expand(item):
    """Return old BR numbers named by one citation item, or None when the item is not a BR item."""
    item = item.strip()
    m = re.fullmatch(r"BR-(\d+)\s*[" + DASH + r"-]\s*(?:BR-)?(\d+)", item)
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        return [x for x in range(a, b + 1) if x in MP]
    m = re.fullmatch(r"BR-(\d+)((?:/\d+)+)", item)
    if m:
        return [int(m.group(1))] + [int(x) for x in m.group(2).strip("/").split("/")]
    m = re.fullmatch(r"BR-(\d+)", item)
    if m:
        return [int(m.group(1))]
    return None


GROUP = re.compile(r"(\s?)\(([^()]*\bBR-\d+[^()]*)\)")


def fmt(ids):
    return ", ".join(ids)


def rewrite_group(inner, ctx, log):
    """Rewrite the inside of one parenthesised citation. Return new inner text or '' to drop it."""
    parts = re.split(r"(\s*[,;]\s*)", inner)
    out = []  # list of (kind, text) where kind 'br' merges
    seps = []
    items = parts[0::2]
    for it in items:
        olds = expand(it)
        if olds is None:
            if re.search(r"\bBR-\d+", it):
                log.append(("FLAG", it))
                # keep non-standard item untouched
            out.append(("txt", it.strip()))
            continue
        news = []
        for o in olds:
            k, ids = new_ids(o, ctx)
            log.append((k, f"BR-{o}", ",".join(ids)))
            news += ids
        out.append(("br", news))
    # merge consecutive BR lists, dedupe, drop empties
    final = []
    seen = set()
    for k, v in out:
        if k == "br":
            v = [x for x in v if not (x in seen or seen.add(x))]
            if not v:
                continue
            if final and final[-1][0] == "br":
                final[-1] = ("br", final[-1][1] + v)
            else:
                final.append(("br", v))
        else:
            if v:
                final.append(("txt", v))
    if not final:
        return ""
    sep = "; " if ";" in inner and "," not in inner else ", "
    return sep.join(fmt(v) if k == "br" else v for k, v in final)


def rewrite(text, ctx, log, bare_overrides=None):
    for phrase, rep in (bare_overrides or {}).items():
        if phrase in text:
            text = text.replace(phrase, rep)
    outside = GROUP.sub("", text)
    if re.search(r"\bBR-\d+", outside):
        log.append(("FLAG-BARE", outside))

    def repl(m):
        new = rewrite_group(m.group(2), ctx, log)
        if new == "":
            return ""
        return f"{m.group(1)}({new})"

    return GROUP.sub(repl, text)


# English wording of the 153 rules, one sentence each (plan 08, section 4).
BR = [
    # 1. Accounts, sign-in, audit
    "A deactivated account cannot sign in or use any function; all its active sessions are ended and its historical data is kept.",
    "An active account can sign in even when its role has no capability.",
    "Google sign-in succeeds only when the email matches an existing internal account; no account is created automatically.",
    "A sign-in error message does not reveal whether the email exists.",
    "An email address belongs to at most one account.",
    "A newly created account is Active.",
    "Only capabilities defined by the system can be granted to a role.",
    "A change to a role's capabilities or to a user's role takes effect from the user's next action, without ending the session.",
    "Account and role administration changes and every sign-in are recorded in the audit log.",
    "Issuing minutes, viewing a document of a Secret Meeting, exporting a report and sharing a calendar are recorded in the audit log.",
    "Recording or playing a voice sample, adding or removing a meeting document, and reading a deactivated account's library are recorded in the audit log.",
    "Only a role with the user-administration capability views the audit log; the audit log is read-only and nobody can edit or delete it.",
    # 2. Meeting lifecycle
    "A new Meeting starts in Draft; its planned start is later than now and its planned end is later than its planned start.",
    "Meeting information can be edited only in Draft or Rejected; editing does not change the status.",
    "For a Published Meeting, only the Organizer changes the schedule or the location; the new start must be later than now.",
    "Only the Organizer, the Approver, the Chair, the Secretary or a participant of a Meeting can access that Meeting.",
    "The participant list and the invitation responses are shown only to people who can view the Meeting.",
    "Submitting for approval moves a Draft or Rejected Meeting to WaitingApproval; while it waits, its general information cannot be edited.",
    "Each approval round has one valid Approver, and only that Approver approves or rejects; without an Approver the Meeting cannot be submitted.",
    "Approval moves a WaitingApproval Meeting to Approved; rejection moves it to Rejected and requires a reason.",
    "Only the Organizer publishes a Meeting, only from Approved, and only when it has exactly one Chair and at least one active Secretary.",
    "Every status change is recorded in the history with the actor, time, action, new status and reason.",
    "The status history is append-only; no entry is edited or deleted.",
    "Only an active Chair or Secretary starts a Meeting, only from Published and only when both a Chair and a Secretary are assigned.",
    "Only the Chair ends a Meeting, only while it is InProgress; ending the Meeting closes its online room.",
    "Ending a Meeting does not close its Votes; ballots already cast are kept.",
    "A Meeting still InProgress 24 hours after its planned end is ended automatically.",
    "Only the Organizer cancels a Meeting, only before it starts; Cancelled is final and read-only.",
    "A Meeting not started 24 hours after its planned start is cancelled automatically by the system.",
    "Read access by status: a Member reads only Published, InProgress, Ended, or Cancelled after Published; the Chair, Secretary and Organizer read every status; the Approver reads from submission, never a Draft.",
    # 3. Notifications
    "Once a Meeting is published, people are notified when invited, when their role changes or they are removed, and when the Meeting is rescheduled or cancelled.",
    "A failure to send a notification does not undo the action that triggered it.",
    "Each person reads and changes the status of their own notifications only.",
    "Opening a link in a notification checks access again.",
    "Nobody receives an in-app notification about their own action.",
    # 4. Participants, roles, invitations
    "Only the current Secretary and the Chair manage the participant list; the Organizer has no separate right to it.",
    "The participant list can be edited in Draft, Rejected, Approved, Published and InProgress; in WaitingApproval it is read-only.",
    "An internal participant is an active account and appears at most once in a Meeting.",
    "Each participant has exactly one meeting role, Chair, Secretary or Member, independent of their system role.",
    "A Meeting has at most one Chair; a Published or InProgress Meeting always has a Chair and at least one Secretary.",
    "Before publication, the Secretary assigns or changes the Chair.",
    "From publication, only the current Chair hands the Chair role to an active internal participant who is not a Secretary, and the former Chair becomes a Member; if the Chair is deactivated, the Secretary may replace them.",
    "A role change does not change the invitation response or the attendance; response and attendance are independent of each other.",
    "Removing a participant or revoking a Guest invitation ends the access gained through that relationship; other independent access remains.",
    "The responses and attendance of a removed person are kept as history and do not count towards the current list, access or role counts.",
    "Only the invitee answers an invitation, only Accepted or Declined, once, while the Meeting is Published or InProgress, and cannot change it; the Chair does not answer.",
    "An invitation not yet answered counts as attending.",
    "Declining only hides the Meeting from that person's calendar; it does not change their role or access, including reading documents.",
    # 5. Guests
    "A Guest is a meeting-specific invitation, not an internal participant.",
    "A Guest has no system role and cannot be the Chair or a Secretary.",
    "Within one Meeting an email has at most one Guest invitation, and no Guest invitation is made for the email of an internal participant.",
    "A Guest enters through their own invitation, only into that Meeting, without an internal account.",
    "A Guest must prove control of the invited email address.",
    "Guest access lasts 12 hours from verification without extension, and ends earlier on sign-out, invitation revocation or the end of the Meeting.",
    "While the Meeting is Published or InProgress, a Guest has the rights of a participant, except moderating the room, a personal library and sharing documents.",
    # 6. Attendance
    "Only an active Secretary records or corrects official attendance, while the Meeting is InProgress.",
    "Attendance covers internal participants and Guests as Present or Absent; a person not yet recorded stays blank and is not counted as absent.",
    # 7. Agenda
    "An agenda item belongs to one Meeting.",
    "The Secretary or the Chair adds, edits, deletes and reorders agenda items from Draft to InProgress; from Ended the agenda is locked.",
    "Only the Chair starts or completes an agenda item.",
    "Any Pending item can be started, but at most one item is InProgress at a time.",
    "An item must be started before it is completed; completing it neither starts another item nor ends the Meeting.",
    "Only an item that has not started (Pending) can be deleted.",
    "A Presenter is a current participant or a Guest of the Meeting.",
    "Removing a person from the Meeting leaves their agenda items without a Presenter.",
    "Each agenda item has at most one conclusion.",
    "The Chair or the Secretary records the conclusion of an InProgress or Done item until the minutes draft is built.",
    # 8. Documents, personal library, comments
    "An uploaded file never changes and has no versions; to change it, a new file is uploaded to replace the meeting document.",
    "Only PDF and DOCX files are accepted.",
    "Only the Chair or the Secretary adds or removes meeting documents and links them to agenda items.",
    "Documents are added or removed from the time the Meeting can be viewed until it ends.",
    "A meeting document comes from the adder's personal library or their computer; a file uploaded straight into a Meeting also goes into the personal library.",
    "A file has at most one active attachment in a Meeting.",
    "Removing a document from a Meeting does not delete it from the personal library or other Meetings, and unlinks it from every agenda item.",
    "Comments on a removed document are kept as history; adding the document again starts afresh, without the old comments.",
    "Deleting a document from the personal library does not remove it from a Meeting.",
    "A file no longer used anywhere is purged periodically; its history and comments are kept.",
    "A document links only to agenda items of the same Meeting.",
    "Anyone who can view a Meeting reads its documents, except a document attached only to a draft Vote, which only the Chair and the Secretary see.",
    "After the Meeting ends, its documents and comments are read-only.",
    "A document of a Secret Meeting can be read only online and never downloaded.",
    "Anyone who can view a document can comment on it until the Meeting ends.",
    "A personal library and its folders belong to one person, and only that person accesses them.",
    "A folder cannot be moved into itself or one of its subfolders; only an empty folder can be deleted.",
    "The Admin only reads the library of a deactivated account, and never sees files of a Secret Meeting.",
    # 9. Voting
    "Only the Chair or the Secretary creates, configures, views and deletes draft Votes, while the Meeting is Approved, Published or InProgress; an opened or closed Vote cannot be deleted.",
    "A Vote belongs to the Meeting, not to an agenda item; draft Votes never opened are deleted when the Meeting ends.",
    "The content, options, type and voting roles of an opened or closed Vote cannot be changed.",
    "A Vote has at least two distinct options and is either Public or Secret.",
    "Only the Chair opens a draft Vote, only while the Meeting is InProgress; at most one Vote is Open at a time and it must have eligible voters.",
    "The voter list is fixed at opening from the chosen roles and current access, not from attendance; later role changes do not alter it.",
    "A ballot is cast only while the Meeting is InProgress, the Vote is Open and the voter still has access.",
    "Each voter casts one ballot for one option of the Vote and cannot change or withdraw it; losing access only blocks new ballots.",
    "Vote documents are attached only while the Vote is Draft and are fixed when it opens.",
    "A voter must confirm reading every Vote document before casting a ballot.",
    "A Secret Vote never links a voter to an option, read confirmations included; only totals are published.",
    "After a Public Vote closes, each voter's choice is shown.",
    "Only the Chair closes and tallies a Vote, while the Meeting is InProgress or Ended; percentages are based on the ballots cast.",
    "The Chair records Passed, NotPassed or Deferred for a closed Vote and cannot change it afterwards; the system never derives it from the result.",
    # 10. Online meeting room
    "Only assigned participants and valid Guests join the meeting room; being only the Organizer or the Approver is not enough.",
    "Leaving the room or losing the connection does not change the Meeting, list, roles, attendance or Votes; a room connection is not attendance.",
    "Only the Chair mutes another person's microphone; that person may unmute again.",
    "Chat messages in the room are not stored.",
    "Each person has at most one speaking request.",
    "Only the Chair orders, invites, declines and closes speaking turns.",
    "The speaking history stays with the Meeting and is not a task.",
    "The recording method follows the meeting mode and is not chosen by the user; the room is recorded only while InProgress, and audio files only for in-person Meetings.",
    "Only the Secretary records the room, uploads audio files, deletes takes or deletes recordings.",
    "Before recording, participants must be informed; the Secretary confirms this and the system stores the time and purpose.",
    "Recordings, transcripts and recorded sentences are deleted automatically 180 days after the first take; later takes do not extend this, and deleting a recording deletes its audio and transcript.",
    "The Chair, the Secretary and the participants can listen to the recordings.",
    "Only the Chair or the Secretary downloads a recording or exports a transcript, never for a Secret Meeting; a Guest only reviews their own microphone sentences.",
    "An online Meeting is recorded only while a microphone is on; each person withdraws their own sentences, and the Secretary deletes any sentence.",
    "The Chair or the Secretary excludes a sentence from the record with a reason, without deleting it; only they read excluded sentences.",
    "Only the Secretary edits the text of the transcript.",
    # 11. Recording, transcript, Voice ID (continued)
    "Voice ID needs its own consent, separate from recording consent; a changed notice requires consent again.",
    "Withdrawing consent or deactivating the account deletes all voice samples, suggestions and names filled in by the system; names the Secretary assigned stay.",
    "Each person has two voice sample sets under one consent, up to 5 self-recorded and up to 5 collected on their behalf; only the owner plays the self-recorded samples.",
    "A holder of the collection right records, plays and deletes collected samples, only of people who consented, and asks the person at each collection.",
    "Speaker suggestions compare only consenting people on the current list, are shown only to the Secretary, never suggest a Guest, and are assigned only to people on the list.",
    "Audio and voice samples are processed inside the system and never sent to an outside service.",
    "The combined recording is only for listening and downloading, and is never transcribed.",
    "The system fills in a name when a voice clearly matches a consenting person on the current list.",
    "Only people who consented to notice version 1.2 or later have names filled in automatically, others are only suggested; collection on someone's behalf needs version 1.4.",
    "When transcription runs again, hand-assigned names, corrected text and moved sentences carry over to the matching sentence or voice; unmatched ones leave only a trace in the log, without text.",
    # 12. AI draft, minutes, notes
    "Only the Chair or the Secretary requests, edits and reviews the AI draft, only when the Meeting is Ended and has at least one recorded sentence.",
    "Other people read the AI draft only after it has been reviewed.",
    "The AI writes only from the meeting record; it does not write attendance, guess names or write vote results.",
    "By default the AI runs on the team's own server; the Admin may choose an outside API for Normal Meetings; access keys are stored encrypted.",
    "Minutes follow one fixed template (Form 1.4, Decree 30/2020/ND-CP); templates are not managed.",
    "Only the Chair or the Secretary reads and edits the minutes draft, only after the Meeting is Ended; other people see only issued minutes.",
    "Minutes of a Secret Meeting cannot be downloaded as Word or PDF.",
    "The system does not track assignees, due dates or task progress; the Tasks section is plain text.",
    "Only the Secretary writes meeting notes, from InProgress until the minutes draft is built; the Chair can read them.",
    "Only the Secretary submits the minutes for approval, only after the Meeting is Ended.",
    "Only the Chair approves or returns the minutes; returning them requires a reason.",
    "Approval issues the minutes immediately; the Meeting stays Ended.",
    "Issued minutes never change; a correction creates a revision from the issued version, with at most one revision not yet issued.",
    "Issued and superseded minutes are read and downloaded by everyone who can read the Meeting except Guests; drafts only by the Chair and the Secretary.",
    # 13. Calendar sharing, finding a time
    "Calendar sharing has two levels: level 1 covers Normal Meetings, level 2 Normal and Internal; Secret Meetings are never shared.",
    "The viewer sees only busy, free, unknown or not shared, never the title, location, attendees or sensitivity; only Organizers use calendar sharing.",
    "Each pair of calendar owner and viewer has at most one Pending or Effective share.",
    "A share stops when the viewer is no longer an Organizer or is deactivated, and expires after its end date.",
    "Days outside the shared range are unknown; a person who has not shared counts as neither free nor busy.",
    "A Meeting above the shared level counts as free; the searcher's own Meetings always count.",
    "Only a Chair, Secretary or Member assignment in a WaitingApproval, Approved, Published, InProgress or Ended Meeting counts as busy.",
    "A person who declined, and Draft, Rejected or Cancelled Meetings, do not count as busy.",
    "A slot that touches a Secret Meeting can still create a Draft, without a special warning.",
    # 14. Reports
    "Internal users view and export reports; Guests do not.",
    "A report counts only Meetings the user can read, by month of planned start, current status and current sensitivity.",
    "Attendance rate, time to minutes and meeting duration follow their formulas; Meetings missing the data are left out of the averages.",
    "Header figures show numbers only; query tables show a person's name or a Meeting's code and title, only for Meetings the viewer reads; a Guest shows only a name.",
    "The report query counts only records the viewer can read: draft Votes and unissued minutes for the Chair and Secretary, recordings for participants; combined recordings add no hours.",
]
assert len(BR) == 153, len(BR)



W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
W14 = "http://schemas.microsoft.com/office/word/2010/wordml"
q = lambda t: f"{{{W}}}{t}"
PARAID = f"{{{W14}}}paraId"
TEXTID = f"{{{W14}}}textId"
XMLSPACE = "{http://www.w3.org/XML/1998/namespace}space"

SRC, OUT = sys.argv[1], sys.argv[2]
d = docx.Document(SRC)
body = d.element.body
if any(
    [Paragraph(tc.find(q('p')), d).text.strip() for tc in tr.findall(q('tc'))][:2] == ['ID', 'Rule']
    for tr in body.iter(q('tr'))
):
    sys.exit('The Business Rules table already has two columns: run this on the DOCX from before the cut.')
report = collections.defaultdict(list)

random.seed(79)  # same paraIds on every run, so a rerun can be compared byte for byte
used_ids = {el.get(PARAID) for el in body.iter() if el.get(PARAID)}


def new_paraid():
    while True:
        v = f"{random.randint(0x10000000, 0x7FFFFFFE):08X}"
        if v not in used_ids:
            used_ids.add(v)
            return v


# ---------- run helpers ----------
def simple_run(r):
    return all(c.tag in (q("rPr"), q("t")) for c in r)


def rpr_key(r):
    x = r.find(q("rPr"))
    return etree.tostring(x) if x is not None else b""


def merge_runs(p):
    """Merge adjacent plain-text runs that share formatting, so a citation is never split."""
    prev = None
    for child in list(p):
        if child.tag == q("r") and simple_run(child) and child.find(q("t")) is not None:
            if prev is not None and rpr_key(prev) == rpr_key(child) and prev.getnext() is child:
                pt = prev.find(q("t"))
                pt.text = (pt.text or "") + "".join(t.text or "" for t in child.findall(q("t")))
                pt.set(XMLSPACE, "preserve")
                p.remove(child)
                continue
            prev = child
        else:
            prev = None


def ptext(p):
    return "".join(t.text or "" for t in p.iter(q("t")))


def map_ts(p, fn):
    for t in p.iter(q("t")):
        if t.text:
            new = fn(t.text)
            if new != t.text:
                t.text = new
                t.set(XMLSPACE, "preserve")


def lines(p):
    """Split a paragraph into lines at w:br; each line is its list of w:t elements."""
    out = [[]]
    for el in p.iter(q("t"), q("br"), q("cr")):
        if el.tag == q("t"):
            if el.text:
                out[-1].append(el)
        else:
            out.append([])
    return [ln for ln in out if ln]


def append_to_line(p, prefix, sentence):
    for ln in lines(p):
        if "".join(t.text for t in ln).startswith(prefix):
            append_sentence(p, sentence, ln)
            return True
    return False


def append_sentence(p, sentence, ts=None):
    """Add a sentence at the end of a paragraph (or line), before a trailing (BR-…) citation."""
    ts = ts or [t for t in p.iter(q("t")) if t.text]
    last = ts[-1]
    def joined(head):
        head = head.rstrip()
        return head + ("" if head.endswith((".", ";", ":", "!", "?", ")")) else ".") + " " + sentence

    m = re.search(r"(\s*\([^()]*\bBR-\d{3}[^()]*\)\s*)$", last.text)
    if m:
        last.text = joined(last.text[: m.start()]) + m.group(1)
    else:
        last.text = joined(last.text)
    last.set(XMLSPACE, "preserve")


def all_lines(p):
    out = [[]]
    for el in p.iter(q("t"), q("br"), q("cr")):
        if el.tag == q("t"):
            out[-1].append(el)
        else:
            out.append([])
    return out


def line_texts(p):
    return ["".join(t.text or "" for t in ln) for ln in all_lines(p)]


def strip_line_ends(p, before):
    """After a citation was dropped, remove the space it leaves at the end of a line."""
    for old, ln in zip(before, all_lines(p)):
        ts = [t for t in ln if t.text]
        if not ts or old.endswith(" "):
            continue
        # strip trailing spaces across the last runs of the line
        for t in reversed(ts):
            stripped = t.text.rstrip()
            if stripped != t.text:
                t.text = stripped
            if t.text:
                break


# ---------- walk the body ----------
def blocks():
    """Yield (part, ctx, heading, element) for each body paragraph or table."""
    heading, part, ctx = "", "", ""
    for el in body.iterchildren():
        if el.tag == q("p"):
            P = Paragraph(el, d)
            t = P.text.strip()
            if P.style is not None and P.style.name.lower().startswith("heading") and t:
                heading = t
                m = re.match(r"^(I|II|III|IV|V)\.\s", t)
                if m:
                    part = m.group(1)
                m = re.search(r"\bUC-\d+[a-z]?\b", t)
                ctx = m.group(0) if (m and part == "II") else t
        yield part, ctx, heading, el


BARE = {
    "under BR-160 and BR-165": "under BR-090 and BR-092",
    "under UC-15 and BR-158": "under UC-15 and BR-015",
    "read access under BR-176": "read access under BR-017",
    " according to BR-12": "",
    "before publication under BR-118": "before publication under BR-030",
    "may view under BR-29:": "may view under BR-016:",
    "required by BR-121": "required by BR-017",
    "that BR-334 allows": "that BR-085 allows",
    "hidden by BR-334": "hidden by BR-085",
    # BR-421/422 (MeetingSlotRules): the step only pointed at them.
    "; defaults follow BR-421/BR-422": (
        ". The duration is 15 minutes to 8 hours (2 hours by default); the date range starts today, no more than "
        "365 days ahead, and covers at most 31 days; working hours are 08:00–17:00 with a 12:00–13:00 lunch break by "
        "default; each slot keeps a buffer of 0–4 hours (30 minutes by default) before and after every busy period, and "
        "start times are rounded up to a step of 5 minutes to 2 hours (30 minutes by default); a slot never starts in "
        "the past"),
    "the parameters break BR-421/BR-422 (": "the parameters are outside the limits of Step 1 (",
    "under BR-65, BR-68 and BR-74": "under BR-059",
    "(BR-90, BR-91: invitations are still sent when the Meeting is published)": "(invitations are still sent when the Meeting is published, BR-90)",
    "UC-31 / EX-4, BR-172 / ": "UC-31 / EX-4 / ",
    "Retired — document-name constraint removed with BR-243": "Retired — document-name constraint removed",
    "UC-33d, BR-275, BR-277).": "UC-33d, BR-079, BR-080).",
}

ACTOR = [
    (re.compile(r"\bMeeting Organizers\b"), "Members"),
    (re.compile(r"\bMeeting Organizer\b"), "Member"),
    (re.compile(r"\bMeeting Approvers\b"), "Approvers"),
    (re.compile(r"\bMeeting Approver\b"), "Approver"),
]
SHORT_ORG = re.compile(r"\bOrganizer(s?)\b")

# UC -> [(paragraph prefix, sentence)] moved from old BRs that no UC described yet
MOVED = {
    "UC-20": [("6. The system notifies current internal participants",
               "Each invitation links directly to the Meeting details and its documents, and the system logs the queuing and delivery status of every message. "
               "In the 15 minutes before the planned start, each invited active internal participant receives one in-app reminder and one reminder email, and each invited Guest one reminder email; after a reschedule the reminder follows the new start time.")],
    "UC-66": [("3. The system displays the user's notifications",
               "Notifications are kept for later viewing; while the user is connected, a new notification and the unread count appear in real time without reloading the page.")],
    "UC-28a": [("5. The system saves all selected participants together",
                "A failed invitation delivery does not undo the saved roster.")],
    "UC-28c": [("4. The system arranges the applicable notification",
                "The notification carries no link that opens the Meeting.")],
    # BR-163 said the link cannot be entered; the code (MeetingRules.MaxOnlineLinkLength) and screen 4.4 take one.
    "UC-14": [("3. The Organizer enters Meeting Name",
               "For an Online or Hybrid Meeting an external Online Link of at most 1,000 characters may be entered; left blank, the TrustCabinet online meeting room is used."),
              ("5. The system checks the required values",
               "The list holds 1 to 100 participants; Guests cannot be added at creation, and the creator need not be a participant.")],
    "UC-82": [("3. The actor types.",
               "When the minutes draft is later built without an AI draft (UC-59), each item's note goes under that item in section II, lines marked Kết luận go to section III and lines marked Nhiệm vụ go to “Nhiệm vụ, sản phẩm và thời hạn”.")],
    # Review of PR #509: steps that only pointed at the moved rule.
    "UC-23a": [("3. The system verifies the active Secretary assignment",
                "The Title is required; Description and Presenter are optional.")],
    "UC-47": [("5. The system cuts the transcript along the agenda",
               "Speech outside every timed item goes into one extra part titled “Nội dung khác”; a leftover of 300 characters or fewer is ignored as greetings between items.")],
    "UC-49": [("● PRE-2: A finished draft exists.", "The AI is not writing the draft again.")],
    "UC-50": [("● PRE-2: A finished draft exists.", "The AI is not writing the draft again.")],
    "UC-43a": [("1. At Step 4, the Meeting is Hybrid or Online, has Ended",
                "The system builds it after the Meeting ends, from the room recording and every online sentence for a Hybrid Meeting or from every online sentence for an Online Meeting, on the real clock: excluded sentences are left out, silences over 2 seconds are shortened to 2 seconds, a track over 12 hours is not built, and a Secret Meeting has none. "
                "When a source changes later the system builds it again, unless the Secretary has erased it.")],
    "UC-84a": [("2. The system checks that the actor is still",
                "A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends.")],
    "UC-84b": [("2. The system checks the recipient",
                "A range is valid when it starts today or later, does not end before it starts and covers at most 365 days counting both ends.")],
    "UC-84d": [("1. The viewer opens their share",
                "A longer range may keep the share's original start date and still covers at most 365 days counting both ends.")],
}
# ctx -> [(text after the citation rewrite, replacement)]: wording that a citation or one word gets wrong.
FIX = {
    # Review of PR #509: the reschedule step's delivery sentence is BR-032, not the reschedule rule.
    # The reschedule lines also cited old BR-27 (-> BR-014, "edit only in Draft or Rejected"), which contradicts them.
    "UC-15": [("Delivery failure does not roll back the saved change. (BR-022, BR-023, BR-015)",
               "Delivery failure does not roll back the saved change. (BR-022, BR-023, BR-032)"),
              ("The Meeting must not have started. (BR-014, BR-015)", "The Meeting must not have started. (BR-015)"),
              ("All other fields are read-only. (BR-014, BR-015)", "All other fields are read-only. (BR-015)"),
              ("outside the allowed Reschedule set was modified. (BR-014, BR-015)",
               "outside the allowed Reschedule set was modified. (BR-015)")],
    "4.8 Publish Meeting": [("địa điểm và link.] (BR-014, BR-015)", "địa điểm và link.] (BR-015)")],
    "4.9 Reschedule Meeting": [("and has not started (BR-014, BR-015)", "and has not started (BR-015)")],
    # BR-312: the minutes list each item with its Presenter (MinutesTemplate).
    "UC-59": [("the agenda and each item's recorded conclusion", "the agenda with each item's Presenter and recorded conclusion")],
    # BR-431: a range the owner shortens must lie within the requested one. Step 2 cited BR-142/143 twice.
    "UC-84c": [("or a shorter range (BR-140, BR-141)", "or a shorter range within the requested one (BR-140, BR-141)"),
               ("can only be declined (BR-142, BR-143). (BR-142, BR-143)", "can only be declined (BR-142, BR-143).")],
    "UC-84e": [("(BR-142, BR-143). (BR-142, BR-143)", "(BR-142, BR-143).")],
    # Step 1 states the search limits; BR-148 (Secret overlap) is cited where the Secret meeting is handled.
    "UC-85a": [("a slot never starts in the past. (BR-148)", "a slot never starts in the past.")],
}
APPROVER_TOO = "An Approver can also do this, because the Approver role also holds the create-meeting permission."
NFR_ADD = {
    "Session and token expiry": "Sign-out revokes only the refresh token, so an access token already issued stays usable until it expires.",
    "External Guest access scope": "For one invitation a new verification code can be requested at most once per minute, and at most 20 codes are sent in any 24 hours.",
    "Upload safety": "All stored documents together take at most 10 GB, counting files not yet purged; an upload that would exceed it is rejected and none of its files is stored. "
                     "Minutes content is HTML filtered by the server before saving, so nothing in it can run in a browser; it holds at most 300,000 characters and 64 levels of nesting, and the Word and PDF files keep only the first 63 columns of a table.",
    "Online meeting room capacity": "The 50-connection limit is enforced by the LiveKit room service, which refuses the next connection while the room is full.",
}
SPEAKER_ROW = (
    "Speaker recognition and transcript clean-up",
    "By default a voice is suggested and its name filled in at a similarity of 0.6 or more, a second voice joins a person at 0.7 and an unnamed voice inherits a person's name at 0.8. "
    "A room-mic passage under 10 characters, or under 3 s and under 20 characters, with no matched voice or only filler words is left out; a per-microphone sentence of filler words only is not stored; "
    "words recognised below 40% confidence are flagged; numbers are written in digits under the Vietnamese ITN rules.",
    "In-house speech worker with its default configuration; the thresholds are configurable",
)


def set_cell_text(tc, text):
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
    t.text = text
    t.set(XMLSPACE, "preserve")


def renew_ids(el):
    for x in el.iter():
        if x.get(PARAID):
            x.set(PARAID, new_paraid())


def rebuild_br_table(tbl):
    trs = tbl.findall(q("tr"))
    head, tmpl = trs[0], copy.deepcopy(trs[1])
    for tr in trs[1:]:
        tbl.remove(tr)
    grid = tbl.find(q("tblGrid"))
    cols = grid.findall(q("gridCol"))
    first = int(cols[0].get(q("w")))
    total = sum(int(c.get(q("w"))) for c in cols)
    for c in cols[2:]:
        grid.remove(c)
    cols[1].set(q("w"), str(total - first))
    for tr in (head, tmpl):
        tcs = tr.findall(q("tc"))
        for tc in tcs[2:]:
            tr.remove(tc)
        tcs[1].find(q("tcPr")).find(q("tcW")).set(q("w"), str(total - first))
    set_cell_text(head.findall(q("tc"))[0], "ID")
    set_cell_text(head.findall(q("tc"))[1], "Rule")
    for i, rule in enumerate(BR, 1):
        tr = copy.deepcopy(tmpl)
        renew_ids(tr)
        tcs = tr.findall(q("tc"))
        set_cell_text(tcs[0], f"BR-{i:03d}")
        set_cell_text(tcs[1], rule)
        tbl.append(tr)
    report["br_table"].append(f"{len(BR)} rows, widths {first}/{total - first}")


CHANGELOG = ("24/11/2026", "M", "HuyNQ",
             "[plan 08, ADR-0079] Business Rules cut from 406 to 153 and renumbered BR-001–153; UI, validation and technical limits moved into the use cases and section IV. "
             "Actors Meeting Organizer and Meeting Approver renamed Member and Approver. Earlier rows keep the old BR IDs (see 09-bang-anh-xa-br-dot-1).")


def add_changelog(tbl):
    last = tbl.findall(q("tr"))[-1]
    tr = copy.deepcopy(last)
    renew_ids(tr)
    for tc, text in zip(tr.findall(q("tc")), CHANGELOG):
        set_cell_text(tc, text)
    last.addnext(tr)


def row_label(tr):
    tc = tr.find(q("tc"))
    return ptext(tc).strip() if tc is not None else ""


# ---------- main pass ----------
moved_done = collections.Counter()
fix_done = collections.Counter()
for part, ctx, heading, el in list(blocks()):
    if el.tag == q("tbl"):
        if heading.startswith("Change Log"):
            add_changelog(el)
            continue
        if heading.startswith("1. Business Rules"):
            rebuild_br_table(el)
            continue
        paras = list(el.iter(q("p")))
    elif el.tag == q("p"):
        paras = [el]
    else:
        continue
    if heading.startswith("Change Log"):
        continue
    for p in paras:
        text = ptext(p)
        if not text:
            continue
        merge_runs(p)
        # 1+2: bare references and citations
        if re.search(r"\bBR-\d", text):
            log = []
            expected = rewrite(text, ctx, log, BARE)
            before = line_texts(p)
            map_ts(p, lambda s: rewrite(s, ctx, [], BARE))
            strip_line_ends(p, before)
            got = ptext(p)
            if got != expected:
                report["split_citation"].append((ctx, text[:200]))
            stale = re.search(r"\bBR-(?:\d{1,2}|\d{4,}|15[4-9]|1[6-9]\d|[2-9]\d\d)\b", got)
            if stale or any(x[0].startswith("FLAG") for x in log):
                report["old_id_left"].append((ctx, got[:300], [x for x in log if x[0].startswith("FLAG")]))
        for old_text, new_text in FIX.get(ctx, []):
            if old_text in ptext(p):
                map_ts(p, lambda s: s.replace(old_text, new_text))
                fix_done[(ctx, old_text)] += 1
        # 3: moved content (match before the actor rename)
        for prefix, sentence in MOVED.get(ctx, []):
            if append_to_line(p, prefix, sentence):
                moved_done[(ctx, prefix)] += 1
        # 4: actor rename
        def actor(s):
            for rx, rep in ACTOR:
                s = rx.sub(rep, s)
            if part == "II" or heading.startswith("4.3"):
                s = s.replace("Being the Organizer or the Approver alone", "Being the Member who created the Meeting or its Approver alone")
                s = SHORT_ORG.sub(lambda m: "Member" + m.group(1), s)
            return s
        before = ptext(p)
        map_ts(p, actor)
        if part == "II" and SHORT_ORG.search(before):
            report["short_organizer"].append((ctx, before[:220]))

# UC-14/15/18 description, NFR rows, actor table
for part, ctx, heading, el in list(blocks()):
    if el.tag != q("tbl"):
        continue
    for tr in el.findall(q("tr")):
        label = row_label(tr)
        tcs = tr.findall(q("tc"))
        if part == "II" and ctx in ("UC-14", "UC-15", "UC-18") and label == "Description":
            ps = [p for p in tcs[1].findall(q("p")) if ptext(p).strip()]
            append_sentence(ps[-1], APPROVER_TOO)
            report["approver_too"].append(ctx)
        if part == "IV" and label in NFR_ADD:
            ps = [p for p in tcs[1].findall(q("p")) if ptext(p).strip()]
            append_sentence(ps[-1], NFR_ADD[label])
            report["nfr"].append(label)
        if heading.startswith("4.1 Actors") and len(tcs) == 3:
            name = ptext(tcs[1]).strip()
            if name == "Member":
                set_cell_text(tcs[1], "Member (Thành viên)")
                set_cell_text(tcs[2], "An internal user whose system role may create meetings. A Member creates, updates, submits for approval and publishes the meetings they organise. An Approver can do the same, because that role also holds the create-meeting permission.")
                report["actor_table"].append(name)
            if name == "Approver":
                set_cell_text(tcs[1], "Approver (Phê duyệt viên)")
                report["actor_table"].append(name)
    if part == "IV" and heading.startswith("2.2 Performance") and not report["speaker_row"]:
        rows = el.findall(q("tr"))
        tr = copy.deepcopy(rows[-1])
        renew_ids(tr)
        for tc, text in zip(tr.findall(q("tc")), SPEAKER_ROW):
            set_cell_text(tc, text)
        rows[-1].addnext(tr)
        report["speaker_row"].append("added")

report["moved"] = sorted(moved_done.items())
missing = [f"MOVED {c}: {pfx[:40]}" for c, items in MOVED.items() for pfx, _ in items if not moved_done[(c, pfx)]]
missing += [f"FIX {c}: {old[:40]}" for c, items in FIX.items() for old, _ in items if not fix_done[(c, old)]]
if missing:
    sys.exit("Not applied, the DOCX text has changed:\n  " + "\n  ".join(missing))
d.save(OUT)
for k, v in report.items():
    print(f"== {k}: {len(v)}")
    if k in ("old_id_left", "split_citation"):
        for x in v:
            print("  ", x)
    elif k not in ("short_organizer",):
        print("  ", v)
