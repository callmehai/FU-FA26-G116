"""Diagrams: cover, Ch.3 context / BF / ERD / UC / screen flow, Ch.4 architecture / package / ERD.

Class and sequence diagrams are not in Report 7 (they stay in Report 4).
"""

from __future__ import annotations

import re
import sys
from copy import deepcopy

from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt
from docx.text.paragraph import Paragraph
from PIL import Image as PILImage

from report7_lib import DOCS, Report, tnr, unify_fonts

CONTEXT = DOCS / "11-contextdiagram" / "trustcabinet-context.drawio.png"
SCREEN_FLOW = DOCS / "12-screenflow" / "TrustCabinet-screen-flow.png"
ERD = DOCS / "13-erd" / "muc-tieu" / "trustcabinet-erd-muc-tieu.drawio.png"
ARCH = DOCS / "16-architecture-diagram" / "kien-truc-phan-mem.png"

BFS = [
    ("BF-01", "System Access & Administration",
     "Trigger: access is granted or changed. End condition: the user signs in and uses exactly the functions of the role.",
     "BF-01_System_Access_Administration.drawio.png"),
    ("BF-02", "Meeting Preparation, Approval & Participant Coordination",
     "Trigger: an Organizer needs to hold a meeting. End condition: the meeting is Published and the invitations are sent.",
     "BF-02_Meeting_Preparation_Approval.drawio.png"),
    ("BF-03", "Meeting Execution & Decision Making",
     "Trigger: the Chair starts a Published meeting. End condition: the meeting is Ended, the agenda results and votes are recorded.",
     "BF-03_Meeting_Execution_Decision_Making.drawio.png"),
    ("BF-04", "Post-Meeting Processing & Formal Records",
     "Trigger: the meeting is Ended. End condition: the minutes are approved by the Chair, issued, locked and searchable by permission.",
     "BF-04_Post_Meeting_Processing_Formal_Records.drawio.png"),
]

ACTORS = [  # (title, file)
    ("System Admin", "uc-system-admin.png"),
    ("Meeting Organizer", "uc-meeting-organizer.png"),
    ("Meeting Approver", "uc-meeting-approver.png"),
    ("Chair", "uc-chair.png"),
    ("Secretary", "uc-secretary.png"),
    ("Participant", "uc-participant.png"),
    ("Guest", "uc-guest.png"),
]

PACKAGES = [
    ("Package overview", "package-tong-quan.png"),
    ("Back-end packages", "package-backend.png"),
    ("Application package", "package-application.png"),
    ("Domain package", "package-domain.png"),
    ("Front-end packages", "package-frontend.png"),
]



def fill_cover(r: Report):
    doc = r.doc
    date_p = None
    for p in doc.paragraphs[:16]:
        if re.search(r"Hanoi.*20\d\d", p.text):
            for run in p.runs[1:]:
                run.text = ""
            tnr(p.runs[0])
            p.runs[0].text = "– Hanoi, Nov 2026 –"
            date_p = p._p
            break
    for t in doc.tables[:3]:
        for row in t.rows:
            for cell in row.cells:
                for p in cell.paragraphs:
                    if "[Project name]" in p.text:
                        _set(p, "TrustCabinet – AI-assisted Paperless Meeting System")
                    elif p.text.strip() == "<Group Name>":
                        _set(p, "SEP490-G116")
    if date_p is not None:
        # Pull the date back onto page 1: the template stacks ~44pt empty
        # spacer paragraphs above it (pushed to page 2 once the member table
        # grew). Shrink those spacers; leave everything after the date alone
        # so the TOC stays on page 2.
        kids = [c for c in r.body.iterchildren() if c.tag != qn("w:sectPr")]
        for el in kids[:kids.index(date_p)]:
            if el.tag != qn("w:p") or list(el.iter(qn("w:drawing"))):
                continue
            if "".join(t.text or "" for t in el.iter(qn("w:t"))).strip():
                continue
            for rPr in el.iter(qn("w:rPr")):
                for sz in list(rPr.iter(qn("w:sz"))) + list(rPr.iter(qn("w:szCs"))):
                    if int(sz.get(qn("w:val") or "0") or 0) > 22:
                        sz.set(qn("w:val"), "22")


def _set(p: Paragraph, text: str):
    if not p.runs:
        tnr(p.add_run(text))
        return
    tnr(p.runs[0])
    p.runs[0].text = text
    for run in p.runs[1:]:
        run.text = ""


def _page_setup(r: Report):
    """(content_width_in, content_height_in) of a landscape page, from the doc defaults."""
    sec = r.doc.sections[0]
    cw = (sec.page_width - sec.left_margin - sec.right_margin) / 914400
    ch = (sec.page_height - sec.top_margin - sec.bottom_margin) / 914400
    return ch, cw  # swapped: landscape


def _new_sectPr(r: Report, landscape: bool, break_type: str | None):
    """Section properties for an ERD page: own orientation, template margins,
    and the template's header/footer references (so every section keeps the
    footer instead of falling back to no footer)."""
    sec = r.doc.sections[0]
    sectPr = OxmlElement("w:sectPr")
    pgSz = OxmlElement("w:pgSz")
    if landscape:
        w_emu, h_emu = sec.page_height.emu, sec.page_width.emu
    else:
        w_emu, h_emu = sec.page_width.emu, sec.page_height.emu
    pgSz.set(qn("w:w"), str(int(w_emu / 635)))
    pgSz.set(qn("w:h"), str(int(h_emu / 635)))
    pgSz.set(qn("w:orient"), "landscape" if landscape else "portrait")
    sectPr.append(pgSz)
    body_sp = r.body.find(qn("w:sectPr"))
    if body_sp is not None:
        pgMar = body_sp.find(qn("w:pgMar"))
        if pgMar is not None:
            sectPr.append(deepcopy(pgMar))
        for tag in ("w:headerReference", "w:footerReference"):
            for ref in body_sp.findall(qn(tag)):
                sectPr.append(deepcopy(ref))
    if break_type is not None:
        brk = OxmlElement("w:type")
        brk.set(qn("w:val"), break_type)
        sectPr.append(brk)
    return sectPr


def _pPr(el):
    pPr = el.find(qn("w:pPr"))
    if pPr is None:
        pPr = OxmlElement("w:pPr")
        el.insert(0, pPr)
    return pPr


def _is_own_break(el) -> bool:
    """Legacy standalone break paragraph from the previous s00 revision."""
    if el.tag != qn("w:p"):
        return False
    pPr = el.find(qn("w:pPr"))
    if pPr is None or pPr.find(qn("w:sectPr")) is None:
        return False
    # our break paragraphs carry no text and no drawing
    if any(el.iter(qn("w:drawing"))):
        return False
    return not "".join(t.text or "" for t in el.iter(qn("w:t"))).strip()


def _wide_picture(r: Report, path, width_in: float):
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    p = r.doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.keep_with_next = True
    p.add_run().add_picture(str(path), width=Inches(width_in))
    el = p._p
    r.body.remove(el)
    return el


def _replace_first_image_landscape(r: Report, heading, path, chapter: int, text: str):
    """Like Report.replace_first_image, but the figure sits on its own landscape page.

    The section break lives inside the heading's and the caption's own pPr
    (not in standalone paragraphs), so later fillers that drop empty
    paragraphs (s30 drop_instructions, s40 _keep_figures) cannot remove it,
    and the footer references travel with every section. Opening break is
    continuous (heading stays with its page; Word moves the landscape part to
    a fresh page), closing break is nextPage (portrait resumes on a new page).
    Idempotent: a previous run's sectPr on the heading is replaced, and an
    orphan caption with the same figure text is removed, so a re-run on one
    document never stacks breaks or captions.
    """
    from report7_lib import _text, is_heading
    kids = r.children()
    rest = kids[kids.index(heading) + 1:]
    nxt_h = next((c for c in rest if is_heading(c)), None)
    lo = kids.index(heading) + 1
    hi = kids.index(nxt_h) if nxt_h is not None else len(kids)
    for el in kids[lo:hi]:
        if _is_own_break(el):
            r.body.remove(el)
    hpPr = _pPr(heading)
    for old in hpPr.findall(qn("w:sectPr")):
        hpPr.remove(old)
    slot = next((e for e in r.section(heading)
                 if e.tag == qn("w:p") and list(e.iter(qn("w:drawing")))), None)
    if slot is None:
        sys.exit(f"no image slot under {_text(heading)!r}")
    # drop previous runs' leftovers: legacy standalone breaks and orphan
    # captions (renumber-proof match; loop: a --write-polluted source can
    # stack more than one)
    nxt = slot.getnext()
    while nxt is not None:
        if _is_own_break(nxt):
            kill, nxt = nxt, nxt.getnext()
            r.body.remove(kill)
            continue
        if nxt.tag == qn("w:p") and not list(nxt.iter(qn("w:drawing"))):
            t = "".join(x.text or "" for x in nxt.iter(qn("w:t"))).strip()
            if t.startswith(f"Figure {chapter}.") and t.endswith(text):
                kill, nxt = nxt, nxt.getnext()
                r.body.remove(kill)
                continue
        break
    lw, lh = _page_setup(r)
    iw, ih = PILImage.open(path).size
    width_in = min(lw - 0.2, max(0.5, (lh - 0.6) * iw / ih))
    pic = _wide_picture(r, path, width_in)
    cap = r.caption(chapter, text)
    slot.addnext(pic)
    pic.addnext(cap)
    r.body.remove(slot)
    hpPr.append(_new_sectPr(r, landscape=False, break_type="continuous"))
    cpPr = _pPr(cap)
    for old in cpPr.findall(qn("w:sectPr")):
        cpPr.remove(old)
    cpPr.append(_new_sectPr(r, landscape=True, break_type="nextPage"))
    nxt = cap.getnext()
    if nxt is not None and nxt.tag == qn("w:p") and not "".join(
            t.text or "" for t in nxt.iter(qn("w:t"))).strip() \
            and not list(nxt.iter(qn("w:drawing"))) and nxt.find(qn("w:pPr")) is None:
        r.body.remove(nxt)


def fill_chapter3(r: Report):
    h = r.find_heading("1.1. Context Diagram")
    r.replace_section(h, r.figure(3, CONTEXT, "Context Diagram of TrustCabinet"))

    h = r.find_heading("1.2. Main Business Flows")
    nxt = r.find_heading("1.3. Entity Relationship Diagram")
    # everything between 1.2 and 1.3, headings included
    kids = r.children()
    for el in kids[kids.index(h) + 1: kids.index(nxt)]:
        r.body.remove(el)
    anchor = h
    for i, (code, name, desc, file) in enumerate(BFS, 1):
        els = [r.new_para(f"1.2.{i} {name} ({code})", style="Heading 4"),
               r.new_para(desc)]
        els += r.figure(3, DOCS / "09-bussinessflow" / file, f"Swimlane of {code} {name}")
        for el in els:
            anchor.addnext(el)
            anchor = el

    h = r.find_heading("1.3. Entity Relationship Diagram")
    _replace_first_image_landscape(r, h, ERD, 3, "Entity Relationship Diagram of TrustCabinet (target model)")

    h = r.find_heading("1.4.2 Use Case Diagrams")
    nxt = r.find_heading("1.4.3 Use Case Inventory")
    kids = r.children()
    for el in kids[kids.index(h) + 1: kids.index(nxt)]:
        r.body.remove(el)
    anchor = h
    for i, (title, file) in enumerate(ACTORS):
        els = [r.new_para(f"{chr(97 + i)}. UCs for {title}", style="Heading 5")]
        els += r.figure(3, DOCS / "14-uc-diagram" / file, f"Use Case Diagram of the {title}")
        for el in els:
            anchor.addnext(el)
            anchor = el

    h = r.find_heading("a. Screen Flow")
    r.replace_first_image(h, SCREEN_FLOW, 3, "Screen Flow of TrustCabinet")


def fill_chapter4(r: Report):
    h = r.find_heading("1.1 System Architecture")
    r.replace_section(h, r.figure(4, ARCH, "Software Architecture of TrustCabinet"))

    h = r.find_heading("1.2 Package Diagram")
    els = []
    for title, file in PACKAGES:
        els += r.figure(4, DOCS / "15-package-diagram" / file, title)
    r.replace_section(h, els)

    h = r.find_heading("2. Database Design")
    sec = r.section(h)
    if any(list(e.iter(qn("w:drawing"))) for e in sec):
        _replace_first_image_landscape(r, h, ERD, 4, "Database Entity Relationship Diagram (target model)")

    # Class and sequence diagrams stay in Report 4; Report 7 only points to them.
    h31 = r.find_heading("3.1 Class Diagram")
    h32 = r.find_heading("3.2 Sequence Diagram(s)")
    h4 = r.find_heading("4. NFR Design Specifications")
    r.replace_until(h31, h32, [r.text("The class diagram of every feature is in Report 4 (Software Design Specification), Part II.")])
    r.replace_until(h32, h4, [r.text("The sequence diagram of every use case is in Report 4 (Software Design Specification), Part II.")])


def fill(r: Report):
    unify_fonts(r.doc)  # Times New Roman for the whole document (styles level)
    fill_cover(r)
    fill_chapter3(r)
    fill_chapter4(r)
