"""Fill Report 4 part II (Detailed Code Design) with the diagrams in the repository.

Source of truth: docs/08-reports/README.md -- the DOCX in docs/08-reports/source/ is edited, never a
Markdown snapshot. This script works on the structure the report has now (UC ids of Report 3, one
H2 per feature "N. FE-NN <name>", H3 "N.1 Class Diagram" and "N.2 Sequence Diagram", one H4 per UC).

What it does, idempotently:
  - adds a class diagram section "N.1 Class Diagram" for every docs/07-classdiagram PNG the report
    lacks (the sequence section moves from N.1 to N.2 and its numbers follow);
  - adds an H4 + image + caption for every docs/06-sequencediagram UC PNG the report lacks, next to
    the UC with the nearest smaller id of the same folder, then renumbers the section;
  - captions: every image is followed by an italic centred "Figure <number>: <Class|Sequence> Diagram of
    <what>" built from its heading; a missing caption is written, a wrong number is corrected;
  - alt text: every picture without a description gets its caption as alternative text;
  - --refresh UC-01,UC-36a,FE-16 (or --refresh all) replaces the picture bytes of those figures with the
    current PNG (width kept, height follows the new aspect); without it existing pictures are untouched,
    because some were laid out by hand (e.g. UC-36b).

Not done (Word finishing pass): the table of contents (Update Field), landscape pages for wide
diagrams, the Change Log row. Section I (architecture, ERD, entity tables) is still filled by
scripts/fill_report4.py.

Usage:
    python scripts/fill_report4_diagrams.py                          # dry run: print the plan
    python scripts/fill_report4_diagrams.py --refresh UC-01,UC-63    # plan including refreshes
    python scripts/fill_report4_diagrams.py --write [--refresh ...]  # modify the DOCX in place
    python scripts/fill_report4_diagrams.py --output /tmp/R4.docx    # write a copy instead

Close the DOCX in Word first, or the write fails with "Device or resource busy".
"""

from __future__ import annotations

import argparse
import html
import re
import struct
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCX = ROOT / "docs/08-reports/source/Report-4_Software Design Specification.docx"
SEQ = ROOT / "docs/06-sequencediagram"
CLS = ROOT / "docs/07-classdiagram"
P_RE = re.compile(r"<w:p[ >].*?</w:p>", re.S)
T_RE = re.compile(r"<w:t[^>]*>([^<]*)</w:t>")
PAGE_EMU = 5731510  # content width of a portrait page
MAX_HEIGHT_EMU = 8000000
BODY_FROM = 500  # paragraphs before this index are the table of contents


def text(p: str) -> str:
    return html.unescape("".join(T_RE.findall(p)))


def style(p: str) -> str | None:
    m = re.search(r'<w:pStyle w:val="([^"]+)"', p)
    return m.group(1) if m else None


def png_size(b: bytes) -> tuple[int, int]:
    return struct.unpack(">II", b[16:24])


def uc_key(uc: str) -> tuple[int, str]:
    m = re.fullmatch(r"UC-(\d+)([a-z]?)", uc)
    assert m, uc
    return int(m.group(1)), m.group(2)


def seq_sources() -> list[dict]:
    """Every sequence PNG of a UC: id, caption title and PNG path, from its .txt `title` line."""
    out = []
    for txt in sorted(SEQ.rglob("UC*.txt")):
        first = next((x for x in txt.read_text(encoding="utf-8-sig").splitlines() if x.startswith("title ")), "")
        m = re.match(r"title (UC-\d+[a-z]?) [-–—] (.+)$", first)
        png = txt.with_suffix(".png")
        if m and png.exists():
            out.append({"uc": m.group(1), "title": m.group(2).strip(), "png": png, "folder": txt.parent.name})
    return out


class Report:
    def __init__(self, path: Path):
        with zipfile.ZipFile(path) as zf:  # read fully and close: Windows cannot replace an open file
            self.infos = zf.infolist()
            self.blobs = {i.filename: zf.read(i.filename) for i in self.infos}
        self.xml = self.blobs["word/document.xml"].decode("utf8")
        self.rels = self.blobs["word/_rels/document.xml.rels"].decode("utf8")
        self.spans = [(m.start(), m.end()) for m in P_RE.finditer(self.xml)]
        self.P = [self.xml[a:b] for a, b in self.spans]
        self.media: dict[str, bytes] = {}
        self.new_rels: list[str] = []
        self.docpr = max(int(i) for i in re.findall(r'<wp:docPr id="(\d+)"', self.xml)) + 1
        self.log: list[str] = []
        self.tpl()

    # ---- structure -------------------------------------------------------------------------------
    def find(self, pred, start=0):
        for i in range(start, len(self.P)):
            if pred(i):
                return i
        raise SystemExit("template paragraph not found: the report no longer has the expected structure")

    def sections(self) -> dict[int, tuple[int, int, str]]:
        """{section number: (first paragraph, end, feature id)} of the FE sections of part II."""
        heads = [i for i in range(BODY_FROM, len(self.P)) if style(self.P[i]) == "Heading2"]
        out = {}
        for k, s in enumerate(heads):
            m = re.match(r"(\d+)\. (FE-\d+) ", text(self.P[s]))
            if m:
                e = heads[k + 1] if k + 1 < len(heads) else len(self.P)
                out[int(m.group(1))] = (s, e, m.group(2))
        return out

    def seq_prefix(self, s: int, e: int) -> tuple[int, str] | None:
        for i in range(s, e):
            m = re.match(r"(\d+)\.(\d) Sequence Diagram", text(self.P[i]))
            if m and style(self.P[i]) == "Heading3":
                return i, f"{m.group(1)}.{m.group(2)}"
        return None

    def figure_of(self, i: int) -> tuple[int, int] | None:
        """(image paragraph, caption paragraph) of the figure whose H4 is paragraph i."""
        if i + 2 < len(self.P) and "<w:drawing" in self.P[i + 1] and text(self.P[i + 2]).startswith("Figure"):
            return i + 1, i + 2
        return None

    # ---- templates -------------------------------------------------------------------------------
    @staticmethod
    def clean(p: str) -> str:
        p = re.sub(r"<w:bookmarkStart[^>]*/>|<w:bookmarkEnd[^>]*/>", "", p)
        p = re.sub(r' w14:(paraId|textId)="[^"]*"', "", p)
        p = re.sub(r"<w:sectPr.*?</w:sectPr>", "", p, flags=re.S)
        return p.replace("<w:lastRenderedPageBreak/>", "")

    def tpl(self) -> None:
        p = self.P
        self.h3 = self.clean(p[self.find(lambda i: style(p[i]) == "Heading3" and "Class Diagram" in text(p[i]), BODY_FROM)])
        h4 = self.find(lambda i: style(p[i]) == "Heading4" and self.figure_of(i) is not None, BODY_FROM)
        self.h4 = self.clean(p[h4])
        self.img = self.clean(p[h4 + 1])
        self.cap = self.clean(p[h4 + 2])

    # ---- building blocks -------------------------------------------------------------------------
    @staticmethod
    def settext(p: str, t: str) -> str:
        return re.sub(r"(<w:t[^>]*>)[^<]*(</w:t>)",
                      lambda m: '<w:t xml:space="preserve">' + html.escape(t, quote=False) + "</w:t>", p, count=1)

    def picture(self, png: Path, alt: str) -> str:
        data = png.read_bytes()
        rid, name = f"rIdFill{len(self.media) + 1}", f"fill{len(self.media) + 1}.png"
        self.media["word/media/" + name] = data
        self.new_rels.append(f'<Relationship Id="{rid}" Type="http://schemas.openxmlformats.org/officeDocument/2006/'
                             f'relationships/image" Target="media/{name}"/>')
        return self.fit(self.img, data, alt, rid, name)

    def fit(self, para: str, data: bytes, alt: str, rid: str | None = None, name: str | None = None) -> str:
        w, h = png_size(data)
        cx = int(min(PAGE_EMU, PAGE_EMU * w / 1920))
        cy = int(cx * h / w)
        if cy > MAX_HEIGHT_EMU:
            cx, cy = int(cx * MAX_HEIGHT_EMU / cy), MAX_HEIGHT_EMU
        para = re.sub(r"<wp:extent [^>]*/>", f'<wp:extent cx="{cx}" cy="{cy}"/>', para)
        para = re.sub(r'<a:ext cx="\d+" cy="\d+"/>', f'<a:ext cx="{cx}" cy="{cy}"/>', para)
        if rid:
            para = re.sub(r'<wp:docPr [^>]*/>',
                          f'<wp:docPr id="{self.docpr}" name="Picture {self.docpr}" descr="{html.escape(alt)}"/>', para)
            para = re.sub(r'<pic:cNvPr id="\d+" name="[^"]*"', f'<pic:cNvPr id="{self.docpr}" name="{name}"', para)
            para = re.sub(r'r:embed="[^"]+"', f'r:embed="{rid}"', para)
            self.docpr += 1
        return para

    @staticmethod
    def keep_width(para: str, data: bytes) -> str:
        """Same displayed width as the picture it replaces (a landscape page stays landscape-wide), so a
        higher-resolution export of the same diagram is sharper, not bigger; the height follows its aspect."""
        w, h = png_size(data)
        cx = int(re.search(r'<wp:extent cx="(\d+)"', para).group(1))
        cy = int(cx * h / w)
        para = re.sub(r"<wp:extent [^>]*/>", f'<wp:extent cx="{cx}" cy="{cy}"/>', para)
        return re.sub(r'<a:ext cx="\d+" cy="\d+"/>', f'<a:ext cx="{cx}" cy="{cy}"/>', para)

    def uc_block(self, num: str, uc: str, title: str, png: Path) -> list[str]:
        what = f"{uc} – {title}"
        return [self.settext(self.h4, f"{num} {what}"), self.picture(png, f"Sequence Diagram of {what}"),
                self.settext(self.cap, f"Figure {num}: Sequence Diagram of {what}")]

    def class_block(self, num: str, fe: str, fe_title: str, png: Path) -> list[str]:
        what = f"{fe} {fe_title}"
        return [self.settext(self.h3, f"{num} Class Diagram"), self.picture(png, f"Class Diagram of {what}"),
                self.settext(self.cap, f"Figure {num}: Class Diagram of {what}")]

    def reparse(self) -> None:
        """Join the edited paragraphs back into the XML and split it again, so each one is its own entry."""
        out, pos = [], 0
        for k, (a, b) in enumerate(self.spans):
            out.append(self.xml[pos:a])
            out.append(self.P[k])
            pos = b
        out.append(self.xml[pos:])
        self.xml = "".join(out)
        self.spans = [(m.start(), m.end()) for m in P_RE.finditer(self.xml)]
        self.P = [self.xml[a:b] for a, b in self.spans]

    # ---- operations ------------------------------------------------------------------------------
    def renumber(self, n: int) -> None:
        """Make the H4 numbers and Figure numbers of section n of the sequence part consecutive."""
        s, e, _ = self.sections()[n]
        sp = self.seq_prefix(s, e)
        if not sp:
            return
        k = 0
        for i in range(sp[0] + 1, e):
            t = text(self.P[i])
            if style(self.P[i]) == "Heading4" and re.match(r"\d+\.\d+\.\d+ ", t):
                k += 1
                self.P[i] = self.settext(self.P[i], re.sub(r"^\d+\.\d+\.\d+", f"{sp[1]}.{k}", t))
            elif t.startswith("Figure ") and re.match(r"Figure \d+\.\d+\.\d+:", t) and k:
                self.P[i] = self.settext(self.P[i], re.sub(r"^Figure \d+\.\d+\.\d+:", f"Figure {sp[1]}.{k}:", t))

    def add_class(self, fe: str, png: Path) -> None:
        n, (s, e, _) = next((n, v) for n, v in self.sections().items() if v[2] == fe)
        if any(f"Class Diagram of {fe} " in text(x) for x in self.P[s:e]):
            return
        sp = self.seq_prefix(s, e)
        fe_title = re.sub(r"^\d+\. FE-\d+ ", "", text(self.P[s]))
        block = self.class_block(f"{n}.1", fe, fe_title, png)
        if sp is None:
            self.log.append(f"{fe}: section has no sequence part, class diagram not added")
            return
        # the sequence part and everything in it moves from N.1 to N.2
        if sp[1] == f"{n}.1":
            for i in range(sp[0], e):
                self.P[i] = re.sub(r"(<w:t[^>]*>)(Figure )?%d\.1([. ])" % n,
                                   lambda m: m.group(1) + (m.group(2) or "") + f"{n}.2" + m.group(3), self.P[i])
        # a note saying "no class diagram" is replaced by the diagram itself
        at = sp[0]
        if at > s and "no class diagram" in text(self.P[at - 1]):
            self.P[at - 1] = ""
        self.P[at] = "".join(block) + self.P[at]
        self.reparse()
        self.log.append(f"add class diagram {n}.1 {fe}")

    def add_uc(self, src: dict) -> None:
        uc, title, folder = src["uc"], src["title"], src["folder"]
        what = f"{uc} – {title}"
        if any(t.startswith("Figure") and what in t for t in map(text, self.P[BODY_FROM:])):
            return
        # the section is the one that already holds other UCs of the same folder
        mates = {s["uc"] for s in seq_sources() if s["folder"] == folder and s["uc"] != uc}
        best: tuple[int, int, tuple[int, str]] | None = None
        first: tuple[int, int, tuple[int, str]] | None = None
        for n, (s, e, _) in self.sections().items():
            for i in range(s, e):
                m = re.match(r"\d+\.\d+\.\d+ (UC-\d+[a-z]?) – ", text(self.P[i])) if style(self.P[i]) == "Heading4" else None
                if m and m.group(1) in mates:
                    key = uc_key(m.group(1))
                    if key < uc_key(uc) and (best is None or key > best[2]):
                        best = (n, i, key)
                    if key > uc_key(uc) and (first is None or key < first[2]):
                        first = (n, i, key)
        if best:
            n, i, _ = best
            at = self.figure_of(i)[1] + 1 if self.figure_of(i) else i + 1
        elif first:
            n, i, _ = first
            at = i
        else:
            self.log.append(f"{uc}: no other UC of {folder} in the report, not added")
            return
        sp = self.seq_prefix(*self.sections()[n][:2])
        assert sp
        self.P[at] = "".join(self.uc_block(f"{sp[1]}.0", uc, title, src["png"])) + self.P[at]
        self.reparse()
        self.renumber(n)
        self.reparse()
        self.log.append(f"add {uc} – {title} in section {n}")

    def refresh(self, wanted: set[str], by_uc: dict[str, list[dict]], by_fe: dict[str, Path]) -> None:
        rel = {m.group(1): m.group(2) for m in re.finditer(r'<Relationship [^>]*?Id="(rId\w+)"[^>]*?Target="([^"]+)"', self.rels)}
        rel.update({m.group(2): m.group(1) for m in re.finditer(r'<Relationship [^>]*?Target="([^"]+)"[^>]*?Id="(rId\w+)"', self.rels)})
        for i, p in enumerate(self.P):
            if i < BODY_FROM or not text(p).startswith("Figure") or "<w:drawing" not in self.P[i - 1]:
                continue
            t = text(p)
            m = re.search(r"Sequence Diagram of (UC-\d+[a-z]?) – (.+)$", t)
            c = re.search(r"Class Diagram of (FE-\d+)", t)
            png = None
            if m and ("all" in wanted or m.group(1) in wanted):
                png = next((s["png"] for s in by_uc.get(m.group(1), []) if s["title"] == m.group(2)), None)
            elif c and ("all" in wanted or c.group(1) in wanted):
                png = by_fe.get(c.group(1))
            if png is None:
                continue
            em = re.search(r'r:embed="([^"]+)"', self.P[i - 1])
            target = "word/" + rel[em.group(1)] if em else None
            data = png.read_bytes()
            if target and self.blobs[target] != data and self.media.get(target) != data:
                self.media[target] = data
                self.P[i - 1] = self.keep_width(self.P[i - 1], data)
                self.log.append(f"refresh {t[:70]}")

    def fix_captions_and_alt(self) -> None:
        for i, p in enumerate(self.P):
            if i < BODY_FROM or not text(p).startswith("Figure ") or "<w:drawing" not in self.P[i - 1]:
                continue
            img = self.P[i - 1]
            d = re.search(r'<wp:docPr [^>]*?descr="([^"]*)"', img)
            if not d or not d.group(1).strip():
                alt = html.escape(text(p))
                if d:
                    img = re.sub(r'(<wp:docPr [^>]*?)descr="[^"]*"', lambda m: m.group(1) + f'descr="{alt}"', img, count=1)
                else:
                    img = re.sub(r"(<wp:docPr [^>]*?)(/>)", lambda m: m.group(1) + f' descr="{alt}"' + m.group(2), img, count=1)
                self.P[i - 1] = img

    def save(self, target: Path) -> None:
        self.reparse()
        xml = self.xml
        rels = self.rels.replace("</Relationships>", "".join(self.new_rels) + "</Relationships>")
        tmp = target.with_suffix(".tmp")
        with zipfile.ZipFile(tmp, "w") as zo:
            for it in self.infos:
                data = self.blobs[it.filename]
                if it.filename == "word/document.xml":
                    data = xml.encode("utf8")
                elif it.filename == "word/_rels/document.xml.rels":
                    data = rels.encode("utf8")
                elif it.filename in self.media:
                    data = self.media.pop(it.filename)
                zo.writestr(it, data, compress_type=it.compress_type)
            for name, data in self.media.items():
                zo.writestr(name, data)
        try:
            tmp.replace(target)
        except PermissionError:
            tmp.unlink(missing_ok=True)
            raise SystemExit(f"cannot write {target.name}: it is open in Word (or locked by sync). Close it and run again.")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write", action="store_true", help="modify the DOCX in place")
    ap.add_argument("--output", type=Path, help="write a copy here instead")
    ap.add_argument("--refresh", default="", help="comma list of UC ids / FE ids, or 'all', whose pictures are replaced")
    ap.add_argument("--source", type=Path, default=DOCX)
    a = ap.parse_args()
    rep = Report(a.source)
    srcs = seq_sources()
    by_uc: dict[str, list[dict]] = {}
    for s in srcs:
        by_uc.setdefault(s["uc"], []).append(s)
    by_fe = {f"FE-{m.group(1)}": p for p in sorted(CLS.glob("feature-*.drawio.png"))
             if (m := re.match(r"feature-(\d+)-", p.name))}
    for fe, png in by_fe.items():
        rep.add_class(fe, png)
    for s in sorted(srcs, key=lambda s: (s["folder"], uc_key(s["uc"]), s["title"])):
        rep.add_uc(s)
    if a.refresh:
        rep.refresh({x.strip() for x in a.refresh.split(",")}, by_uc, by_fe)
    rep.fix_captions_and_alt()
    print("\n".join(rep.log) or "nothing to add: every diagram is already in the report")
    if a.write or a.output:
        target = a.output or a.source
        rep.save(target)
        print(f"written {target}")
    else:
        print("dry run: nothing written (use --write or --output)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
