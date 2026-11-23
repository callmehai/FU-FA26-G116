"""Last filler: footer, figure numbering in document order.

- footer: the template says "ProjectCode" and a fixed " / 28" pages; use the real name and a NUMPAGES field;
- captions "Figure N.M: ..." are renumbered per chapter in the order they appear, so fillers that
  add figures to the same chapter never produce two "Figure 3.1".
"""

from __future__ import annotations

import re
from copy import deepcopy

from docx.oxml import OxmlElement
from docx.oxml.ns import qn

from report7_lib import Report, _text

CAP = re.compile(r"^Figure (\d+)\.\d+:")


def _fix_footer(r: Report):
    doc = r.doc
    # Never touch sec.footer (python-docx creates a new empty footer part for
    # sections without one); work on the raw sectPr references instead.
    RID = qn("r:id")
    done_parts: set[str] = set()

    def fix_part(part):
        for t in part.element.iter(qn("w:t")):
            if "ProjectCode" in (t.text or ""):
                t.text = "TrustCabinet – Final Project Document"
            elif (t.text or "").strip() == "/ 28":
                run = t.getparent()
                t.text = " / "
                for kind, txt in (("begin", None), ("instr", " NUMPAGES "), ("separate", None), ("text", "1"), ("end", None)):
                    nr = deepcopy(run)
                    for c in list(nr):
                        if c.tag != qn("w:rPr"):
                            nr.remove(c)
                    if kind == "instr":
                        e = OxmlElement("w:instrText")
                        e.set(qn("xml:space"), "preserve")
                        e.text = txt
                    elif kind == "text":
                        e = OxmlElement("w:t")
                        e.text = txt
                    else:
                        e = OxmlElement("w:fldChar")
                        e.set(qn("w:fldCharType"), kind)
                    nr.append(e)
                    run.addnext(nr)
                    run = nr

    def part_text(rId):
        try:
            part = doc.part.rels[rId].target_part
        except KeyError:
            return None, None
        txt = "".join(t.text or "" for t in part.element.iter(qn("w:t"))).strip()
        return part, txt

    # 1. fix every footer part that carries content (normally just footer1.xml)
    for sp in [r.body.find(qn("w:sectPr"))] + [
            p.find(qn("w:pPr")).find(qn("w:sectPr"))
            for p in r.body.iter(qn("w:p"))
            if p.find(qn("w:pPr")) is not None and p.find(qn("w:pPr")).find(qn("w:sectPr")) is not None]:
        if sp is None:
            continue
        for ref in sp.findall(qn("w:footerReference")):
            rId = ref.get(RID)
            if rId in done_parts:
                continue
            done_parts.add(rId)
            part, _ = part_text(rId)
            if part is not None:
                fix_part(part)

    # 2. drop references to EMPTY footer parts on every section but the first,
    # so those sections inherit the real footer instead of showing nothing
    sectprs = [p.find(qn("w:pPr")).find(qn("w:sectPr"))
               for p in r.body.iter(qn("w:p"))
               if p.find(qn("w:pPr")) is not None and p.find(qn("w:pPr")).find(qn("w:sectPr")) is not None]
    body_sp = r.body.find(qn("w:sectPr"))
    if body_sp is not None:
        sectprs.append(body_sp)
    for sp in sectprs[1:]:
        for ref in list(sp.findall(qn("w:footerReference"))):
            _, txt = part_text(ref.get(RID))
            if txt == "":
                sp.remove(ref)


def _renumber_figures(r: Report):
    count: dict[str, int] = {}
    for p in r.body.iter(qn("w:p")):
        m = CAP.match(_text(p).strip())
        if not m:
            continue
        ch = m.group(1)
        count[ch] = count.get(ch, 0) + 1
        first = next(iter(p.iter(qn("w:t"))))
        first.text = CAP.sub(f"Figure {ch}.{count[ch]}:", first.text, count=1)


def fill(r: Report):
    _fix_footer(r)
    _renumber_figures(r)
