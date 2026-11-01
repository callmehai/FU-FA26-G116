"""Check that a report DOCX is one Word opens without the "unreadable content" prompt.

Run:
    python scripts/check_docx.py                      # every DOCX in docs/08-reports/source/
    python scripts/check_docx.py path/to/report.docx   # one file
    python scripts/check_docx.py --fix-fonts           # set Report 3 fonts to Times New Roman, then check

Report 3 uses Times New Roman throughout (docs/08-reports/README.md). Pasting from a web page or an
email, or Word styles, bring in other fonts; the check lists them and --fix-fonts rewrites them.
Other reports keep their own fonts and are never touched.

A merge or a script that re-serialises word/document.xml can drop the namespace declarations on
the root element while mc:Ignorable still names them; Word then refuses the file. The checks here
catch that and the other defects that make Word offer to recover a document. Exit code is 1 when
any file has an error.
"""

from __future__ import annotations

import os
import re
import sys
import zipfile
from collections import Counter
from pathlib import Path
from xml.dom import minidom

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "docs" / "08-reports" / "source"


TIMES = "Times New Roman"
# Equation and bullet glyph fonts are not text fonts; leave them alone.
FONT_OK = {TIMES, "Cambria Math", "Symbol", "Wingdings", "Wingdings 2", "Wingdings 3"}
FONT_PARTS = re.compile(r"word/(document|styles|numbering|footnotes|endnotes|header\d*|footer\d*)\.xml$")
FONT_ATTR = re.compile(r'\s(w:(?:ascii|hAnsi|cs|eastAsia)(?:Theme)?)="([^"]*)"')


def is_report3(path: Path) -> bool:
    return path.name.startswith("Report_3")


def font_issues(zf: zipfile.ZipFile) -> dict[str, set[str]]:
    """Parts that name a font other than Times New Roman, with the fonts found."""
    found: dict[str, set[str]] = {}
    for name in zf.namelist():
        if not FONT_PARTS.match(name):
            continue
        xml = zf.read(name).decode("utf8")
        bad = set()
        for rfonts in re.findall(r"<w:rFonts [^>]*>", xml):
            for attr, value in FONT_ATTR.findall(rfonts):
                if attr == "w:eastAsiaTheme":
                    continue  # only decides East Asian text, never Latin or Vietnamese
                if attr.endswith("Theme"):
                    bad.add(f"{attr}={value}")
                elif value not in FONT_OK:
                    bad.add(value)
        if bad:
            found[name] = bad
    return found


def fix_fonts(path: Path) -> int:
    """Rewrite every non-Times-New-Roman font on a run or style to Times New Roman. Returns parts changed."""
    zin = zipfile.ZipFile(path)
    infos = zin.infolist()
    data = {i.filename: zin.read(i.filename) for i in infos}
    changed = 0
    for name in font_issues(zin):
        xml = data[name].decode("utf8")

        def rewrite(m: re.Match[str]) -> str:
            tag = m.group(0)
            tag = re.sub(r'\sw:(?:ascii|hAnsi|cs)Theme="[^"]*"', "", tag)
            return re.sub(r'(w:(?:ascii|hAnsi|cs|eastAsia)=")([^"]*)(")',
                          lambda a: a.group(0) if a.group(2) in FONT_OK else f"{a.group(1)}{TIMES}{a.group(3)}", tag)

        new = re.sub(r"<w:rFonts [^>]*>", rewrite, xml)
        if new != xml:
            data[name] = new.encode("utf8")
            changed += 1
    zin.close()
    if changed:
        tmp = path.with_suffix(path.suffix + ".tmp")
        with zipfile.ZipFile(tmp, "w") as zo:
            for i in infos:
                zo.writestr(i, data[i.filename], compress_type=i.compress_type)
        os.replace(tmp, path)
    return changed


def check(path: Path) -> list[str]:
    errors: list[str] = []
    try:
        zf = zipfile.ZipFile(path)
    except zipfile.BadZipFile as e:
        return [f"not a valid zip: {e}"]
    bad = zf.testzip()
    if bad:
        errors.append(f"corrupt zip entry: {bad}")
    for name in zf.namelist():
        if name.endswith((".xml", ".rels")):
            try:
                minidom.parseString(zf.read(name))
            except Exception as e:  # noqa: BLE001 - report any parse failure
                errors.append(f"{name}: not well-formed XML ({e})")
    if "word/document.xml" not in zf.namelist():
        return errors + ["word/document.xml missing"]

    doc = zf.read("word/document.xml").decode("utf8")
    root = doc[: doc.index("<w:body>")]
    declared = set(re.findall(r"xmlns:(\w+)=", root))
    ignorable = re.search(r'mc:Ignorable="([^"]*)"', root)
    if ignorable:
        missing = [p for p in ignorable.group(1).split() if p not in declared]
        if missing:
            errors.append(f"mc:Ignorable names undeclared prefixes on the root: {', '.join(missing)}")
    used = set(re.findall(r"</?(\w+):", doc))
    undeclared = sorted(p for p in used - declared - {"xml"} if f"xmlns:{p}=" not in doc)
    if undeclared:
        errors.append(f"prefixes used but never declared: {', '.join(undeclared)}")

    starts = re.findall(r'<w:bookmarkStart [^>]*w:id="(\d+)"', doc)
    ends = re.findall(r'<w:bookmarkEnd [^>]*w:id="(\d+)"', doc)
    if any(n > 1 for n in Counter(starts).values()):
        errors.append("duplicate bookmark ids")
    if set(starts) ^ set(ends):
        errors.append("bookmarkStart/bookmarkEnd not paired")
    names = re.findall(r'<w:bookmarkStart [^>]*w:name="([^"]+)"', doc)
    if any(n > 1 for n in Counter(names).values()):
        errors.append("duplicate bookmark names")
    if any(n > 1 for n in Counter(re.findall(r'<wp:docPr [^>]*\bid="(\d+)"', doc)).values()):
        errors.append("duplicate drawing ids (wp:docPr)")
    if any(int(p, 16) >= 0x80000000 for p in re.findall(r'w14:paraId="(\w+)"', doc)):
        errors.append("w14:paraId >= 0x80000000")

    rels = zf.read("word/_rels/document.xml.rels").decode("utf8")
    rel_ids = set(re.findall(r'Id="(\w+)"', rels))
    refs = set(re.findall(r'r:(?:embed|id|link)="(\w+)"', doc))
    if refs - rel_ids:
        errors.append(f"relationships missing: {', '.join(sorted(refs - rel_ids))}")
    if is_report3(path):
        for part, fonts in sorted(font_issues(zf).items()):
            errors.append(f"{part}: font other than {TIMES}: {', '.join(sorted(fonts))} (run with --fix-fonts)")
    return errors


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--fix-fonts"]
    fixing = len(args) != len(sys.argv) - 1
    targets = [Path(a) for a in args] or sorted(p for p in SOURCE_DIR.glob("*.docx") if not p.name.startswith("~$"))
    failed = False
    for path in targets:
        if fixing and is_report3(path):
            n = fix_fonts(path)
            print(f"fonts: {path.name}: {n} part(s) set to {TIMES}")
        errors = check(path)
        print(f"{'FAIL' if errors else 'OK  '} {path.name}")
        for e in errors:
            print(f"     - {e}")
        failed |= bool(errors)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
