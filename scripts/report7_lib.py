"""Helpers shared by scripts/fill_report7.py and the section fillers in scripts/report7_sections/.

A section filler is a module exposing `fill(r: Report)`; it rebuilds ONE section of the Report 7
DOCX from the repo (find the heading, replace_section, add paragraphs / tables / figures), so a
re-run gives the same document.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import docx
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
SOURCE = DOCS / "08-reports" / "source" / "Report-7_Final Project Document.docx"

CONTENT_W_IN = 6.3  # A4 portrait, 1in margins
CONTENT_H_IN = 8.6  # leaves room for the caption on the same page
PAGE_BUDGET = 150   # the school's limit for the whole document

BODY_FONT = "Times New Roman"  # house body font; sizes stay as they are


def tnr(run):
    """Point one run at the body font, keeping its size/bold/italic/color."""
    run.font.name = BODY_FONT
    return run


def unify_fonts(doc):
    """Point the base styles at Times New Roman (idempotent).

    Only the font name is set; sizes, colors and style-level bold are untouched.
    Filler runs additionally carry the font directly (see new_para/table), so
    content stays TNR even where a style is overridden later.
    """
    for name in ("Normal", "Title", "Caption", "Footer",
                 "Heading 1", "Heading 2", "Heading 3",
                 "Heading 4", "Heading 5", "Heading 6",
                 "Heading 7", "Heading 8", "Heading 9"):
        try:
            st = doc.styles[name]
        except KeyError:
            continue
        st.font.name = BODY_FONT


def _text(el) -> str:
    return "".join(t.text or "" for t in el.iter(qn("w:t")))


def _style(el) -> str:
    ppr = el.find(qn("w:pPr"))
    if ppr is not None:
        st = ppr.find(qn("w:pStyle"))
        if st is not None:
            return st.get(qn("w:val")) or ""
    return ""


def is_heading(el) -> bool:
    return el.tag == qn("w:p") and _style(el).lower().startswith("heading")


def norm(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip().lower()


class Report:
    def __init__(self, path: Path):
        self.doc = docx.Document(str(path))
        self.body = self.doc.element.body
        self.fig = [0, 0]  # chapter, running number

    def children(self):
        return [c for c in self.body.iterchildren() if c.tag != qn("w:sectPr")]

    def find_heading(self, text: str):
        """The unique heading whose normalised text equals `text`."""
        hits = [c for c in self.children() if is_heading(c) and norm(_text(c)) == norm(text)]
        if len(hits) != 1:
            sys.exit(f"heading {text!r}: expected 1 match, found {len(hits)}")
        return hits[0]

    def section(self, heading):
        """Elements after `heading` up to (not including) the next heading."""
        kids = self.children()
        i = kids.index(heading) + 1
        out = []
        while i < len(kids) and not is_heading(kids[i]):
            out.append(kids[i])
            i += 1
        return out

    def until(self, heading, next_heading):
        """Elements between two headings, sub-headings included."""
        kids = self.children()
        return kids[kids.index(heading) + 1: kids.index(next_heading)]

    # ---- builders: return detached elements
    def new_para(self, text="", style=None, align=None, italic=False, bold=False, size=None):
        p = self.doc.add_paragraph()
        if style:
            p.style = self.doc.styles[style]
        if text:
            r = p.add_run(text)
            # never write False: it would override the style (e.g. Heading bold)
            if italic:
                r.italic = True
            if bold:
                r.bold = True
            tnr(r)
            if size:
                r.font.size = Pt(size)
        if align is not None:
            p.alignment = align
        el = p._p
        self.body.remove(el)
        return el

    def heading(self, text: str, level: int):
        return self.new_para(text, style=f"Heading {level}")

    def text(self, text: str, bold=False, italic=False):
        return self.new_para(text, bold=bold, italic=italic)

    def bullets(self, items):
        return [self.new_para("• " + it) for it in items]

    def table(self, header, rows, widths_in=None, size=9):
        """Table Grid table with a shaded bold header row; returns the <w:tbl> element."""
        t = self.doc.add_table(rows=1 + len(rows), cols=len(header))
        t.style = self.doc.styles["Table Grid"]
        for ri, row in enumerate([header] + [list(x) for x in rows]):
            for ci, val in enumerate(row):
                cell = t.cell(ri, ci)
                cell.text = ""
                run = cell.paragraphs[0].add_run(str(val))
                tnr(run)
                run.font.size = Pt(size)
                if ri == 0:
                    run.bold = True
                    shd = OxmlElement("w:shd")
                    shd.set(qn("w:val"), "clear")
                    shd.set(qn("w:fill"), "D9E2F3")
                    cell._tc.get_or_add_tcPr().append(shd)
                if widths_in:
                    cell.width = Inches(widths_in[ci])
        t.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:tblHeader"))
        el = t._tbl
        self.body.remove(el)
        return el

    def picture_para(self, path: Path):
        w, h = Image.open(path).size
        width_in = min(CONTENT_W_IN, CONTENT_H_IN * w / h)
        p = self.doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.keep_with_next = True
        p.add_run().add_picture(str(path), width=Inches(width_in))
        el = p._p
        self.body.remove(el)
        return el

    def caption(self, chapter: int, text: str):
        if self.fig[0] != chapter:
            self.fig = [chapter, 0]
        self.fig[1] += 1
        return self.new_para(f"Figure {chapter}.{self.fig[1]}: {text}",
                             align=WD_ALIGN_PARAGRAPH.CENTER, italic=True, size=10)

    def figure(self, chapter: int, path: Path, text: str):
        if not path.exists():
            sys.exit(f"missing diagram: {path}")
        return [self.picture_para(path), self.caption(chapter, text)]

    # ---- placement
    def replace_section(self, heading, new_elements):
        """Drop what sits under `heading` (to the next heading) and put `new_elements` there."""
        for el in self.section(heading):
            self.body.remove(el)
        self.insert_after(heading, new_elements)

    def replace_until(self, heading, next_heading, new_elements):
        """Same, but also drops sub-headings up to `next_heading`."""
        for el in self.until(heading, next_heading):
            self.body.remove(el)
        self.insert_after(heading, new_elements)

    def insert_after(self, anchor, elements):
        for el in elements:
            anchor.addnext(el)
            anchor = el

    def replace_first_image(self, heading, path: Path, chapter: int, text: str):
        """Swap the template's sample picture for ours, keep the rest of the section."""
        slot = next((e for e in self.section(heading)
                     if e.tag == qn("w:p") and list(e.iter(qn("w:drawing")))), None)
        if slot is None:
            sys.exit(f"no image slot under {_text(heading)!r}")
        pic, cap = self.figure(chapter, path, text)
        slot.addnext(pic)
        pic.addnext(cap)
        self.body.remove(slot)
        nxt = cap.getnext()
        if nxt is not None and nxt.tag == qn("w:p") and not _text(nxt).strip() \
                and not list(nxt.iter(qn("w:drawing"))):
            self.body.remove(nxt)
