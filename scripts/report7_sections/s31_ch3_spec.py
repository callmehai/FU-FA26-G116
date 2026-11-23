"""Chapter 3 sections 2-5: Use Case Specifications, Functional Specifications, NFR, Requirement Appendix.

Content is condensed from Report 3 (read with python-docx), so the numbers and wording stay in step with it.
Chapter 3 is business language: vendor and technology names are scrubbed from everything copied.
"""

from __future__ import annotations

import re
import sys

import docx
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt
from docx.table import Table
from docx.text.paragraph import Paragraph

from report7_lib import DOCS, Report

R3 = DOCS / "08-reports" / "source" / "Report_3_Software_Requirement_Specification.docx"
SCREENS = DOCS / "wireframe" / "screens"

UCS = ["UC-01", "UC-14", "UC-19", "UC-20", "UC-37", "UC-55", "UC-59", "UC-64"]

# (Report 3 heading, wireframe image)
SCREEN_LIST = [
    ("1.1 Sign In", "screen-1_1.png"),
    ("4.3 Meeting List", "screen-4_3.png"),
    ("4.4 Create / Edit Meeting", "screen-4_4.png"),
    ("8.2 Online Meeting Room – Video Grid", "screen-8_2.png"),
    ("11.1 Meeting Detail – Voting Tab", "screen-11_1.png"),
    ("12.1 Minutes Editor", "screen-12_1.png"),
]

BRS = ["BR-01", "BR-02", "BR-03", "BR-05", "BR-09", "BR-18", "BR-29", "BR-34", "BR-36", "BR-37", "BR-38",
       "BR-85", "BR-123", "BR-153", "BR-155", "BR-165", "BR-183", "BR-318", "BR-351", "BR-361", "BR-386",
       "BR-406", "BR-408", "BR-409", "BR-445"]

MSGS = ["MSG01", "MSG02", "MSG07", "MSG29", "MSG31", "MSG78", "MSG43", "MSG44", "MSG45", "MSG137",
        "MSG110", "MSG117", "MSG164", "MSG222", "MSG225", "MSG226", "MSG229", "MSG349", "MSG395",
        "MSG398", "MSG424", "MSG427", "MSG428", "MSG429", "MSG401", "MSG404"]

# vendor / technology names that must not reach Chapter 3
SCRUB = [
    (r"Resend Email", "the email service"), (r"\bResend\b", "the email service"),
    (r"\bSMTP( server)?\b", "email server"), (r"LiveKit( SFU| server)?", "the media server"),
    (r"PostgreSQL( \d+)?", "the database"), (r"\bRedis\b", "the credential store"),
    (r"\bDocker( Compose)?\b", "container"), (r"\bWebRTC\b", "real-time media"),
    (r"Cloudflare R2", "file storage"), (r"\bJWT\b", "token"), (r"\bTLS 1\.2 or later\b", "encryption"),
    (r"\bTLS\b", "encryption"), (r"\bHTTPS\b", "encrypted connection"), (r"\bAPI\b", "service"),
    (r"\bWebSocket\b", "real-time channel"), (r"\bendpoints\b", "service addresses"),
    (r"\bendpoint\b", "service address"),
]
BANNED = re.compile(r"Resend|SMTP|LiveKit|PostgreSQL|Redis|Docker|WebRTC|Cloudflare|JWT|/api|endpoint|Spring|React", re.I)


def scrub(s: str) -> str:
    for pat, rep in SCRUB:
        s = re.sub(pat, rep, s)
    return s


def clip(s: str, limit: int) -> str:
    s = scrub(s).strip()
    if len(s) <= limit:
        return s
    cut = s[:limit]
    m = max(cut.rfind(". "), cut.rfind("; "))
    if m > limit * 0.5:
        return cut[:m + 1] + " …"
    return cut[:cut.rfind(" ")] + " …"


def _txt(el) -> str:
    return "".join(t.text or "" for t in el.iter(qn("w:t")))


class R3Doc:
    def __init__(self):
        self.d = docx.Document(str(R3))
        self.els = list(self.d.element.body.iterchildren())

    def style(self, el) -> str:
        ppr = el.find(qn("w:pPr"))
        if ppr is not None:
            st = ppr.find(qn("w:pStyle"))
            if st is not None:
                return (st.get(qn("w:val")) or "").lower()
        return ""

    def heading_idx(self, pred, start=0):
        for i in range(start, len(self.els)):
            e = self.els[i]
            if e.tag == qn("w:p") and self.style(e).startswith("heading") and pred(_txt(e).strip()):
                return i
        sys.exit("Report 3 heading not found")

    def next_table(self, i, limit=6):
        for j in range(i + 1, min(i + limit, len(self.els))):
            if self.els[j].tag == qn("w:tbl"):
                return Table(self.els[j], self.d)
        sys.exit("Report 3: no table after heading")

    def paras_after(self, i):
        out = []
        for j in range(i + 1, len(self.els)):
            e = self.els[j]
            if e.tag == qn("w:p"):
                if self.style(e).startswith("heading"):
                    break
                t = _txt(e).strip()
                if t:
                    out.append(t)
            elif e.tag == qn("w:tbl"):
                out.append(None)  # table marker
        return out

    def uc_title(self, code: str) -> str:
        i = self.heading_idx(lambda t: re.match(rf"^\d+(\.\d+)*\s+{re.escape(code)}\s+[–-]", t) is not None)
        return re.sub(r"^\d+(\.\d+)*\s+", "", _txt(self.els[i]).strip())

    def uc_names(self):
        names = {}
        for e in self.els:
            if e.tag == qn("w:p") and self.style(e).startswith("heading"):
                m = re.match(r"^(?:\d+(?:\.\d+)*\s+)?(UC-\d+[a-z]?)\s+[–-]\s+(.*)$", _txt(e).strip())
                if m:
                    names.setdefault(m.group(1), m.group(2))
        return names


def cell_lines(cell) -> list[str]:
    """One step per line: paragraphs are split further at manual line breaks,
    because Report 3 packs several flow steps into one paragraph of some cells."""
    lines = []
    for p in cell.paragraphs:
        cur = ""
        for node in p._p.iter():
            if node.tag == qn("w:t"):
                cur += node.text or ""
            elif node.tag == qn("w:br"):
                if cur.strip():
                    lines.append(cur.strip())
                cur = ""
        if cur.strip():
            lines.append(cur.strip())
    return lines


def unique_cells(row):
    seen, out = [], []
    for c in row.cells:
        if c._tc not in seen:
            seen.append(c._tc)
            out.append(c)
    return out


def blocks(lines, prefix):
    """Split flow lines into blocks that start with e.g. 'EX-1' / 'AF-1'."""
    out, cur = [], None
    for ln in lines:
        if ln.startswith(prefix):
            cur = [ln]
            out.append(cur)
        elif cur is not None:
            cur.append(ln)
    return out


def condense_uc(row_label, lines):
    if row_label == "Normal Flow":
        head, steps = lines[:1], lines[1:]
        return head + [clip(s, 500) for s in steps]  # every main-flow step is kept
    if row_label == "Alternative Flows":
        bl = blocks(lines, "AF-")
        keep = sum(bl, []) if bl else lines  # every alternative branch is kept
        return [clip(s, 450) for s in keep]
    if row_label == "Exception Flows":
        bl = blocks(lines, "EX-")
        keep = sum(bl[:3], []) if bl else lines[:12]  # the first three exception flows
        return [clip(s, 450) for s in keep]
    return [clip(s, 600) for s in lines]


def shade(cell, fill):
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:fill"), fill)
    cell._tc.get_or_add_tcPr().append(shd)


def put(cell, lines, size=8.5, bold=False):
    cell.text = ""
    first = True
    for ln in lines:
        p = cell.paragraphs[0] if first else cell.add_paragraph()
        first = False
        p.paragraph_format.space_after = Pt(1)
        run = p.add_run(ln)
        run.font.size = Pt(size)
        run.bold = bold


def uc_table(r: Report, src: Table):
    rows = [unique_cells(row) for row in src.rows]
    t = r.doc.add_table(rows=len(rows), cols=4)
    t.style = r.doc.styles["Table Grid"]
    for ri, cells in enumerate(rows):
        if ri == 0:  # actors row: label, value, label, value
            for ci, c in enumerate(cells[:4]):
                put(t.cell(0, ci), [scrub(x) for x in cell_lines(c)], bold=ci in (0, 2))
                if ci in (0, 2):
                    shade(t.cell(0, ci), "D9E2F3")
            continue
        label = cells[0].text.strip()
        body = cell_lines(cells[1])
        merged = t.cell(ri, 1).merge(t.cell(ri, 3))
        put(t.cell(ri, 0), [label], bold=True)
        shade(t.cell(ri, 0), "D9E2F3")
        put(merged, condense_uc(label, body))
    for row in t.rows:
        for ci, w in enumerate([1.1, 2.2, 1.1, 1.9]):
            row.cells[ci].width = Inches(w)
    el = t._tbl
    r.body.remove(el)
    return el


def simple_table(r: Report, header, rows, widths, size=8.5):
    return r.table(header, rows, widths_in=widths, size=size)


def sec_uc(r: Report, d3: R3Doc):
    h2 = r.find_heading("2. Use Case Specifications")
    h3 = r.find_heading("3. Functional Specifications")
    els = [r.text("The eight use cases below carry the four main business flows: BF-01 access (UC-01), BF-02 meeting "
                  "preparation and approval (UC-14, UC-19, UC-20), BF-03 meeting execution and decision making "
                  "(UC-37, UC-55) and BF-04 post-meeting records (UC-59, UC-64). Each table is condensed from Report 3 "
                  "Part II: purpose, actors, preconditions and postconditions are kept in full, the main flow keeps "
                  "every step, and all alternative branches plus the first three exception flows are kept. The full "
                  "text of every use case, including the remaining exception flows, is in Report 3, Part II.")]
    for n, code in enumerate(UCS, 1):
        title = d3.uc_title(code)
        i = d3.heading_idx(lambda t, c=code: re.match(rf"^\d+(\.\d+)*\s+{re.escape(c)}\s+[–-]", t) is not None)
        src = d3.next_table(i)
        els.append(r.heading(f"2.{n} {scrub(title)}", 3))
        els.append(uc_table(r, src))
        els.append(r.new_para("", size=4))
    r.replace_until(h2, h3, els)


def sec_screens(r: Report, d3: R3Doc):
    h3 = r.find_heading("3. Functional Specifications")
    h4 = r.find_heading("4. Non-Functional Requirements")
    names = d3.uc_names()
    part3 = d3.heading_idx(lambda t: t.startswith("III. Functional Requirements"))
    els = [r.text("Six key screens are specified here, one per step of the main business flows: signing in, the meeting "
                  "list, creating a meeting, the online meeting room, voting and the minutes editor. Each shows its "
                  "wireframe image taken from docs/wireframe/screens/, a function description, the use cases it serves "
                  "and its main components. The full wireframe set and the "
                  "field tables of all screens are in Report 3, Part III.")]
    for n, (title, img) in enumerate(SCREEN_LIST, 1):
        i = d3.heading_idx(lambda t, tt=title: t == tt, start=part3)
        paras = d3.paras_after(i)
        desc = next(p for p in paras if p)
        ucs = []
        for p in paras:
            if p:
                for u in re.findall(r"UC-\d+[a-z]?", p):
                    if u not in ucs:
                        ucs.append(u)
        name = re.sub(r"^\d+\.\d+\s+", "", title)
        els.append(r.heading(f"3.{n} {name}", 3))
        img_path = SCREENS / img
        if img_path.exists():
            els += r.figure(3, img_path, f"Wireframe of the {name} screen")
        else:
            els.append(r.text(f"TODO(s31): wireframe image {img} for the {name} screen is missing "
                              f"in docs/wireframe/screens/.", bold=True))
        els.append(r.text("Function description:", bold=True))
        els.append(r.text(clip(desc, 1000)))
        els.append(r.text("Use cases served:", bold=True))
        els += r.bullets([f"{u} – {scrub(names[u])}" for u in ucs[:6] if u in names])
        src = d3.next_table(i, limit=60)
        rows = []
        for row in src.rows[1:13]:
            c = unique_cells(row)
            rows.append([clip(c[0].text.replace("\n", " "), 60), clip(c[1].text.replace("\n", " "), 400)])
        els.append(r.text("Main components:", bold=True))
        els.append(r.table(["Component", "Description"], rows, widths_in=[1.7, 4.6], size=8.5))
        els.append(r.new_para("", size=4))
    r.replace_until(h3, h4, els)


def pick_rows(d3: R3Doc, heading_pred, names, nameidx=0):
    i = d3.heading_idx(heading_pred)
    t = d3.next_table(i)
    got = {}
    for row in t.rows[1:]:
        c = unique_cells(row)
        key = c[0].text.strip()
        got[key] = [x.text.strip() for x in c]
    out = []
    for n in names:
        if n not in got:
            sys.exit(f"Report 3: missing row {n}")
        out.append(got[n])
    return out


def nfr_rows(d3: R3Doc, heading, wanted):
    i = d3.heading_idx(lambda t: t.startswith(heading))
    t = d3.next_table(i)
    rows = {}
    for row in t.rows[1:]:
        c = unique_cells(row)
        rows[c[0].text.strip()] = [x.text.strip() for x in c]
    out = []
    for w in wanted:
        if w not in rows:
            sys.exit(f"Report 3 NFR row missing: {w}")
        out.append([scrub(rows[w][0])] + [clip(x, 520) for x in rows[w][1:]])
    return out


def sec_nfr(r: Report, d3: R3Doc):
    h4 = r.find_heading("4. Non-Functional Requirements")
    h5 = r.find_heading("5. Requirement Appendix")
    W2 = [1.7, 4.6]
    els = [r.heading("4.1 External Interfaces", 3)]
    els += [
        r.text("User interface: a single-page web application opened in a standard web browser; no installation, plug-in "
               "or extension is needed. Every screen, label, message, notification and email is in Vietnamese. Dates are "
               "shown as dd/MM/yyyy and times in 24-hour format, Vietnam time (UTC+7). The layout is desktop-first "
               "(1280 px or wider, usable at 1024 px); guest invitation and verification, invitation response, voting, "
               "notifications and document viewing also work on a phone browser of 360 px or wider. A native mobile app "
               "is out of scope."),
        r.text("Hardware interface: no special hardware. Clients are PCs, laptops, tablets or phones with a supported "
               "browser; joining the online room needs a microphone and speaker or headset and optionally a camera, "
               "granted by the user. The system never switches on a microphone or camera by itself. Server: one Linux "
               "host inside the organisation, baseline 8 vCPU, 16 GB RAM and 500 GB of storage for documents and audio."),
        r.text("Software interfaces: (1) a relational database for business data, the audit log and delivery queues; "
               "(2) a short-lived credential store for sessions and password-reset links; (3) an outbound email service "
               "for invitations (with a calendar attachment), reminders, approval and password-reset emails; (4) Google "
               "sign-in for internal users whose Google email is linked to an account, with password sign-in always "
               "available; (5) a real-time media service for audio, video and screen sharing in the online room; "
               "(6) speech recognition and draft-minutes generation that run on the organisation's own infrastructure and "
               "produce a speaker-attributed Vietnamese transcript and a draft of the minutes; (7) a file store for "
               "documents and recordings. No other third-party system (HR, ERP, calendar, chat, eOffice) is integrated."),
        r.text("Communication interfaces: all traffic between browser and server is encrypted and uses one origin; data "
               "is exchanged as JSON. Notifications, agenda and vote updates are pushed over a real-time channel that "
               "reconnects by itself when it drops. Meeting media is encrypted in transit."),
        r.heading("4.2 Quality Attributes", 3),
        r.text("Targets are for one organisation of about 200 internal users and are verified in Reports 5.3-5.5."),
        r.heading("4.2.1 Performance", 4),
        r.table(["Requirement", "Target", "Condition"],
                [[scrub(a), clip(b, 380), clip(c, 200)] for a, b, c in
                 nfr_rows(d3, "2.2 Performance", ["Server response time", "Search and list views",
                                                   "Document upload and preview",
                                                   "Meeting audio upload and AI processing",
                                                   "Notification and real-time update latency",
                                                   "Concurrent users", "Report generation and export"])],
                widths_in=[1.4, 3.1, 1.8], size=8.5),
        r.heading("4.2.2 Security", 4),
        r.table(["Requirement", "Description"],
                nfr_rows(d3, "2.3 Security", ["Authentication required", "Password policy and protection",
                                              "Sign-in rate limiting", "Session and token expiry",
                                              "Role-based access control and least privilege",
                                              "Document security levels", "External Guest access scope",
                                              "Encryption of data in transit", "Audit logging",
                                              "Integrity of issued minutes, vote conclusions and stored documents",
                                              "Upload safety"]),
                widths_in=W2, size=8.5),
        r.heading("4.2.3 Usability", 4),
        r.table(["Requirement", "Description"],
                nfr_rows(d3, "2.1 Usability", ["Vietnamese user interface", "Consistency",
                                               "Responsive, desktop-first layout", "Browser support",
                                               "Clear error messages with codes",
                                               "Confirmation of destructive and irreversible actions"]),
                widths_in=W2, size=8.5),
        r.heading("4.2.4 Reliability and Availability", 4),
        r.table(["Requirement", "Description"],
                nfr_rows(d3, "2.4 Reliability", ["No loss of committed data", "Reliable asynchronous delivery",
                                                 "Recoverable AI processing"])
                + nfr_rows(d3, "2.5 Availability", ["Service availability", "Recovery time and data loss",
                                                    "Graceful degradation"]),
                widths_in=W2, size=8.5),
        r.heading("4.2.5 Maintainability and Portability", 4),
        r.table(["Requirement", "Description"],
                nfr_rows(d3, "2.6 Maintainability", ["Layered, replaceable design", "Code quality and test coverage",
                                                     "Configurability without code change"])
                + nfr_rows(d3, "2.7 Portability", ["Self-hosted containerised deployment",
                                                   "Environment independence"]),
                widths_in=W2, size=8.5),
    ]
    r.replace_until(h4, h5, els)


def sec_appendix(r: Report, d3: R3Doc):
    h5 = r.find_heading("5. Requirement Appendix")
    ch4 = r.find_heading("Chapter 4 - System Design")
    # business rules
    i = d3.heading_idx(lambda t: t.startswith("1. Business Rules"), start=d3.heading_idx(
        lambda t: t.startswith("V. Requirement Appendix")))
    t = d3.next_table(i, limit=8)
    rules = {}
    for row in t.rows[1:]:
        c = [x.text.strip() for x in unique_cells(row)]
        rules[c[0]] = c
    br_rows = []
    for b in BRS:
        if b not in rules:
            sys.exit(f"Report 3: missing {b}")
        c = rules[b]
        br_rows.append([b, clip(c[1], 330), clip(c[2], 60), scrub(c[4]) if len(c) > 4 and c[4] else "—"])
    # messages
    i = d3.heading_idx(lambda t: t.startswith("2. System Messages"))
    t = d3.next_table(i, limit=8)
    msgs = {}
    for row in t.rows[1:]:
        c = [x.text.strip() for x in unique_cells(row)]
        if len(c) >= 5:
            msgs.setdefault(c[1], c)
    msg_rows = []
    for n, m in enumerate(MSGS, 1):
        if m not in msgs:
            sys.exit(f"Report 3: missing {m}")
        c = msgs[m]
        msg_rows.append([str(n), m, c[2], clip(c[3], 70), clip(c[4].replace("\n", " "), 160)])
    els = [
        r.text("This appendix keeps what applies to the whole system. The complete lists (more than four hundred "
               "business rules and the full message catalogue) are in Report 3, Part V."),
        r.heading("5.1 Business Rules", 3),
        r.text(f"{len(BRS)} cross-cutting rules are listed: access and authentication, the meeting and minutes "
               "lifecycles, guests, documents, recordings and audit. The other rules belong to one use case and are "
               "referenced from its specification in Report 3."),
        r.table(["BR-ID", "Rule Statement", "Applies To", "Enforced By"], br_rows,
                widths_in=[0.6, 3.9, 0.9, 0.9], size=8),
        r.heading("5.2 System Messages", 3),
        r.text(f"{len(MSGS)} representative messages are listed. Every message has one global code MSGnn that never "
               "changes meaning; the full catalogue is in Report 3, Part V, Section 2."),
        r.table(["#", "Message code", "Message Type", "Context", "Content"], msg_rows,
                widths_in=[0.3, 0.8, 1.0, 1.7, 2.5], size=8),
        r.heading("5.3 Other Requirements", 3),
        r.table(["Topic", "Requirement"], [
            ["Recording notice and retention",
             "Before joining, the person is told that the meeting is recorded for the minutes and that the recording is "
             "erased after 180 days (BR-353). Erasing a recording removes its audio and transcript and keeps an audit "
             "record that it existed (BR-348)."],
            ["Voice ID consent",
             "Voice recognition of a person needs that person's own separate consent, never combined with recording "
             "consent and never pre-selected; withdrawing it deletes all the person's voice samples (BR-361, BR-365). "
             "Personal data handling follows Law 91/2025/QH15, which superseded the Decree 13/2023 regime cited "
              "in Report 3."],
            ["Issued minutes", "Minutes are issued by the Chair's approval with a full audit trail. They carry no "
             "CA-issued digital signature; signing was dropped by the product owner on 2026-11-14 (BR-166)."],
            ["Scope exclusions", "No native mobile app, no task or action-item tracking, no organisation units "
             "(one organisation), no integration with other corporate systems in this version."],
            ["Language and locale", "Vietnamese only in this version; dd/MM/yyyy dates, 24-hour time, Vietnam time (UTC+7)."],
            ["Retired rules", "A retired business rule or message code stays in the catalogue marked as retired and "
             "its number is never reused."],
        ], widths_in=[1.6, 4.7], size=8.5),
    ]
    r.replace_until(h5, ch4, els)


def fill(r: Report):
    d3 = R3Doc()
    sec_uc(r, d3)
    sec_screens(r, d3)
    sec_nfr(r, d3)
    sec_appendix(r, d3)
    # business-language guard for the part we rebuilt
    start = r.find_heading("2. Use Case Specifications")
    end = r.find_heading("Chapter 4 - System Design")
    kids = r.children()
    for el in kids[kids.index(start): kids.index(end)]:
        hit = BANNED.search(_txt(el))
        if hit:
            print(f"WARNING s31: banned term {hit.group(0)!r} in: {_txt(el)[:100]}", file=sys.stderr)
