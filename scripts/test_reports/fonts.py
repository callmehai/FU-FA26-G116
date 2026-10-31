"""Set every font in the generated Report 5 files to one typeface.

The six templates came from different teams and mix Tahoma, Calibri, MS PGothic, Cambria, Arial,
Georgia, Cardo and Gungsuh. The team asked for a single plain font throughout, so after a file is
written every font reference in it is rewritten: cell fonts and rich-text runs in a workbook, run
fonts in a document, and the latin / east-Asian / complex-script faces of every theme, chart and
drawing. Layout, sizes, colours and the rest of the template stay as they are.

verify_reports.py applies the same `part` transform to the template before comparing, so a template
part that changed in any other way still fails the check.
"""
import re
from zipfile import ZipFile,ZIP_DEFLATED

FONT='Times New Roman'
# Glyph fonts draw bullets and symbols, not text; replacing them would turn a bullet into a letter.
GLYPHS={'Symbol','Wingdings','Wingdings 2','Wingdings 3','Webdings','Marlett'}
WORKBOOK=re.compile(r'^xl/(?:styles|sharedStrings)\.xml$|^xl/worksheets/sheet\d+\.xml$')
DRAWINGML=re.compile(r'^(?:xl|word)/(?:theme/theme\d+|charts/chart\d+|drawings/drawing\d+)\.xml$')
DOCUMENT=re.compile(r'^word/(?:document|styles|numbering|header\d+|footer\d+|footnotes|endnotes)\.xml$')


def part(name,data):
    """The bytes of one package part with its fonts set to FONT; other parts come back unchanged."""
    if not name.endswith('.xml') or not (WORKBOOK.match(name) or DRAWINGML.match(name) or DOCUMENT.match(name)):return data
    text=data.decode('utf-8')
    if WORKBOOK.match(name):
        # <name> holds a cell font's face, <rFont> a rich-text run's. A theme <scheme> would make Excel
        # draw the theme font instead, and a Japanese <charset> would pick a Japanese fallback.
        text=re.sub(r'<(name|rFont) val="[^"]*"\s*/>',lambda m:f'<{m[1]} val="{FONT}"/>',text)
        text=re.sub(r'<(?:scheme|charset) val="[^"]*"\s*/>','',text)
        text=re.sub(r'<family val="[^"]*"\s*/>','<family val="1"/>',text)
    if DRAWINGML.match(name) or WORKBOOK.match(name):
        text=re.sub(r'<a:(latin|ea|cs)\b[^>]*?/>',lambda m:f'<a:{m[1]} typeface="{FONT}"/>',text)
        text=re.sub(r'(<a:font\b[^>]*?\btypeface=")[^"]*(")',rf'\g<1>{FONT}\2',text)
    if DOCUMENT.match(name):
        def run(m):
            faces=set(re.findall(r'w:(?:ascii|hAnsi|eastAsia|cs)="([^"]*)"',m[0]))
            if faces&GLYPHS:return m[0]
            return f'<w:rFonts w:ascii="{FONT}" w:hAnsi="{FONT}" w:eastAsia="{FONT}" w:cs="{FONT}"/>'
        text=re.sub(r'<w:rFonts\b[^>]*?/>',run,text)
    return text.encode('utf-8')


def unify(path):
    """Rewrite the package at path in place with every font set to FONT."""
    with ZipFile(path) as z:
        items=[(info,part(info.filename,z.read(info.filename))) for info in z.infolist()]
    with ZipFile(path,'w',ZIP_DEFLATED) as z:
        for info,data in items:z.writestr(info,data)
