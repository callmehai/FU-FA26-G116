"""Unit test report 5.1: case JSON -> checks, Markdown previews, Excel.

Content lives in docs/08-reports/test-reports/cases/, next to the 5.2 / 5.2.2 sources:
  functions.json             which sheets exist, in which order, and which methods each one covers (the plan)
  FE-xx/unit/<sheet>.json    UTC rows of one sheet -> Report5.1_Unit_Test.xlsx, one sheet per production function
  preview/FE-xx/unit-<sheet>.md  generated Markdown preview of each sheet, for reviewing in a PR

Usage (from the repository root):
  python scripts/test_reports/unit_cases.py scaffold   # create the missing FE-xx/unit/<sheet>.json with methods and no UTCs
  python scripts/test_reports/unit_cases.py check      # validate; exit 1 on an error
  python scripts/test_reports/unit_cases.py md         # write cases/preview/FE-xx/unit-*.md
  python scripts/test_reports/unit_cases.py build      # check + md + workbook from templates/unit-template.xlsx

The workbook is output: never edit it in Excel, edit the JSON and build again.
"""
import argparse
import json
import math
import re
import sys
from copy import copy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'docs/08-reports/test-reports'
UNIT = REPORTS / 'cases'
MANIFEST = UNIT / 'functions.json'
TEMPLATE = Path(__file__).resolve().parent / 'templates/unit-template.xlsx'
CATALOGUE = ROOT / 'docs/03-api/00-danh-muc-ma-thong-bao.md'
STATUSES = ('N/A', 'Passed', 'Pending', 'Failed')
TYPES = ('Normal', 'Abnormal', 'Boundary')
FORBIDDEN = re.compile(r'\bAssert\.|TestServer|dotnet test|\bfixture\b|WebApplicationFactory', re.I)


def load(path):
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def manifest():
    return load(MANIFEST)


def case_file(entry):
    return UNIT / entry['feature'] / 'unit' / f"{entry['sheet']}.json"


# ---------------------------------------------------------------- scaffold

def cmd_scaffold(_):
    m = manifest()
    for entry in m['sheets']:
        path = case_file(entry)
        if path.exists():
            continue
        save_json(path, {
            'feature': entry['feature'], 'module': entry['module'], 'sheet': entry['sheet'],
            'description': entry.get('description', ''), 'precondition': entry.get('precondition', ''),
            'methods': [{'method': mo['method'], 'file': mo.get('file', ''),
                         'line': mo.get('line', 0), 'cases': []} for mo in entry['methods']],
        })
        print('created', path.relative_to(ROOT))


# ---------------------------------------------------------------- check

def test_methods():
    names = set()
    for f in (ROOT / 'tests').rglob('*.cs'):
        try:
            text = f.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            continue
        names.update(re.findall(r'public\s+(?:async\s+)?[\w<>\[\],\s?]+\s(\w+)\s*\(', text))
    return names


def run_checks():
    m = manifest()
    errors, todo = [], []
    try:
        codes = set(re.findall(r'\bMSG\d{2,3}\b', CATALOGUE.read_text(encoding='utf-8')))
    except FileNotFoundError:
        codes = set()
        errors.append(f'catalogue not found: {CATALOGUE}')
    methods = test_methods()

    names = [s['sheet'] for s in m['sheets']]
    for n in names:
        if len(n) > 31 or re.search(r'[\\/?*\[\]:]', n) or names.count(n) > 1:
            errors.append(f'functions.json: bad or duplicate sheet name {n!r}')

    for entry in m['sheets']:
        sid = f"{entry['feature']}/{entry['sheet']}"
        for mo in entry.get('methods', []):
            fpath = ROOT / mo.get('file', '')
            line = mo.get('line', 0)
            if not mo.get('file') or not fpath.is_file():
                errors.append(f'functions.json {sid}: function {mo.get("method")} file not found: {mo.get("file")}')
                continue
            try:
                lines = fpath.read_text(encoding='utf-8').splitlines()
            except UnicodeDecodeError:
                errors.append(f'functions.json {sid}: cannot read {mo.get("file")}')
                continue
            if not (1 <= line <= len(lines)):
                errors.append(f'functions.json {sid}: {mo.get("method")} line {line} out of range in {mo.get("file")}')
            else:
                short = mo.get('method', '').split('.')[-1]
                if short not in lines[line - 1]:
                    errors.append(f'functions.json {sid}: {mo.get("method")} not at {mo.get("file")}:{line} (found: {lines[line-1].strip()[:80]!r})')
        for t in entry.get('methods', [])[0].get('directTests', []) if entry.get('methods') else []:
            pass
        # cited direct tests in the plan must exist
        for mo in entry.get('methods', []):
            for t in mo.get('directTests', []):
                if t not in methods:
                    errors.append(f'functions.json {sid}: test method not found: {t}')

        path = case_file(entry)
        where = path.relative_to(REPORTS).as_posix()
        if not path.exists():
            errors.append(f'{where}: missing (run scaffold)')
            continue
        doc = load(path)
        for key in ('feature', 'module', 'sheet'):
            if doc.get(key) != entry[key]:
                errors.append(f'{where}: {key} is {doc.get(key)!r}, functions.json says {entry[key]!r}')
        planned = [mo['method'] for mo in entry['methods']]
        actual = [mo['method'] for mo in doc.get('methods', [])]
        if actual != planned:
            errors.append(f'{where}: methods differ from functions.json\n      planned {planned}\n      actual  {actual}')
        n = 0
        for mo in doc.get('methods', []):
            if not mo.get('cases'):
                todo.append(f"{where}: {mo['method']}")
            for c in mo.get('cases', []):
                n += 1
                cid = c.get('id', '?')
                if cid != f'UTC{n:03d}':
                    errors.append(f'{where}: {cid} should be UTC{n:03d} (sequential per sheet)')
                for key in ('preconditions', 'inputs', 'return', 'exception', 'log', 'note'):
                    if key not in c or c.get(key) is None or (isinstance(c.get(key), str) and not c.get(key).strip() and key in ('inputs', 'return', 'exception', 'log')):
                        pass
                if not c.get('preconditions'):
                    errors.append(f'{where} {cid}: empty preconditions')
                for key in ('inputs', 'return', 'exception', 'log'):
                    if key not in c or c[key] is None or (isinstance(c[key], str) and not c[key].strip()):
                        errors.append(f'{where} {cid}: empty {key}')
                if c.get('type') not in TYPES:
                    errors.append(f'{where} {cid}: type must be one of {TYPES}')
                text = json.dumps({k: c.get(k, '') for k in ('preconditions', 'inputs', 'return', 'exception', 'log')}, ensure_ascii=False)
                if FORBIDDEN.search(text):
                    errors.append(f'{where} {cid}: test-code wording outside Note ({FORBIDDEN.search(text).group(0)})')
                if c.get('round1', 'Pending') not in STATUSES:
                    errors.append(f"{where} {cid}: round1 must be one of {STATUSES}")
                for found in re.findall(r'Automated: [^(]+\(([^)]*)\)', c.get('note', '')):
                    for name in re.split(r';\s*', found):
                        if name and name not in methods:
                            errors.append(f'{where} {cid}: test method not found: {name}')
                for code in sorted(set(re.findall(r'\bMSG\d{2,3}\b', json.dumps(c, ensure_ascii=False))) - codes):
                    errors.append(f'{where} {cid}: {code} is not in the message-code catalogue')
    return errors, todo


def cmd_check(_):
    errors, todo = run_checks()
    for e in errors:
        print('ERROR', e)
    for t in todo:
        print('TODO', t)
    print(f'{len(errors)} error(s); {len(todo)} method(s) still without UTCs')
    return 1 if errors else 0


# ---------------------------------------------------------------- markdown preview

def md_cell(value):
    items = [value] if isinstance(value, str) else value
    text = items[0] if len(items) == 1 else '<br>'.join(f'{i}. {s}' for i, s in enumerate(items, 1))
    return text.replace('|', '\\|').replace('\n', '<br>')


def expected_text(c):
    return f"Return: {c.get('return', '')}\nException: {c.get('exception', '')}\nLog Message: {c.get('log', '')}"


def cmd_md(_):
    m = manifest()
    for entry in m['sheets']:
        path = case_file(entry)
        if not path.exists():
            continue
        doc = load(path)
        out = [f"# {doc['sheet']} — Unit ({doc['feature']}, {doc['module']})", '',
               f"Description: {doc.get('description') or '—'}", '', f"Precondition: {doc.get('precondition') or '—'}", '']
        for mo in doc['methods']:
            out += [f"## {mo['method']}", '']
            if not mo.get('cases'):
                out += ['*No UTC yet.*', '']
                continue
            out += ['| ID | Pre-conditions | Inputs | Expected | Type | Note |', '|---|---|---|---|---|---|']
            for c in mo['cases']:
                out.append('| ' + ' | '.join([
                    md_cell(c['id']), md_cell(c.get('preconditions', [])),
                    md_cell(c.get('inputs', '')), md_cell(expected_text(c)),
                    md_cell(c.get('type', '')), md_cell(c.get('note', ''))]) + ' |')
            out.append('')
        target = UNIT / 'preview' / entry['feature'] / f"unit-{entry['sheet']}.md"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text('\n'.join(out), encoding='utf-8', newline='\n')
    print('previews ->', (UNIT / 'preview').relative_to(ROOT))


# ---------------------------------------------------------------- Excel

def numbered(items):
    items = list(items)
    return '\n'.join(f'{i}. {s}' for i, s in enumerate(items, 1)) if len(items) > 1 else (items[0] if items else '')


def bulleted(items):
    if isinstance(items, str):
        return items
    return '\n'.join(f'- {s}' for s in items)


def height_for(values_and_widths, line_height=13.5):
    lines = 1
    for value, width in values_and_widths:
        if not value:
            continue
        per_line = max(int(width * 1.15), 8)
        n = sum(max(1, math.ceil(len(part) / per_line)) for part in str(value).split('\n'))
        lines = max(lines, n)
    return min(409, lines * line_height + 4)


def styles_of(ws, row, cols):
    return [copy(ws.cell(row, c)._style) for c in range(1, cols + 1)]


def apply(ws, row, styles):
    for c, s in enumerate(styles, 1):
        ws.cell(row, c)._style = copy(s)


def clear_from(ws, first_row):
    for rng in list(ws.merged_cells.ranges):
        if rng.max_row >= first_row:
            ws.unmerge_cells(str(rng))
    if ws.max_row >= first_row:
        ws.delete_rows(first_row, ws.max_row - first_row + 1)


def build_book(m):
    import openpyxl
    from openpyxl.worksheet.datavalidation import DataValidation
    from openpyxl.worksheet.hyperlink import Hyperlink

    wb = openpyxl.load_workbook(TEMPLATE)
    cover, tc, ts, proto = wb['Cover'], wb['Test Cases'], wb['Test Statistics'], wb['Prototype']

    # Cover
    cover['B2'] = 'UNIT TEST DOCUMENT'
    cover['B4'] = m['project']
    cover['B5'] = m['projectCode']
    cover['F4'] = m['cover']['creator']
    cover['F5'] = m['cover']['issueDate']
    cover['F6'] = m['cover']['version']
    change_style = styles_of(cover, 11, 6)
    clear_from(cover, 11)
    for i, ch in enumerate(m['cover']['changes']):
        r = 11 + i
        apply(cover, r, change_style)
        for col, key in zip('ABCDEF', ('date', 'version', 'item', 'type', 'description', 'reference')):
            cover[f'{col}{r}'] = ch[key]

    # Module sheets, cloned from the prototype
    group_style = styles_of(proto, 12, 16)
    case_style = styles_of(proto, 13, 16)
    widths = {k: v.width for k, v in proto.column_dimensions.items()}
    built = []
    for entry in m['sheets']:
        doc = load(case_file(entry))
        ws = wb.copy_worksheet(proto)
        ws.title = entry['sheet']
        clear_from(ws, 12)
        for k, w in widths.items():
            ws.column_dimensions[k].width = w
        for hidden_col in ('J', 'K', 'M', 'N', 'T'):
            ws.column_dimensions[hidden_col].hidden = True
        ws['B2'] = entry['module']
        ws['B3'] = '=COUNTIF(A:A,"UTC*")'
        ws['B4'], ws['C4'], ws['D4'] = 'Passed', 'Failed', 'Pending'
        for r, col in ((5, 'F'), (6, 'I'), (7, 'L')):
            ws[f'B{r}'] = f'=COUNTIF(${col}12:${col}1003,B4)'
            ws[f'C{r}'] = f'=COUNTIF(${col}12:${col}1003,C4)'
            ws[f'D{r}'] = f'=COUNTIF(${col}12:${col}1003,D4)'
        ws['B8'], ws['C8'], ws['D8'] = 'Normal', 'Abnormal', 'Boundary'
        ws['B9'] = '=COUNTIF($E12:$E1003,B8)'
        ws['C9'] = '=COUNTIF($E12:$E1003,C8)'
        ws['D9'] = '=COUNTIF($E12:$E1003,D8)'
        r, case_rows = 12, []
        for mo in doc['methods']:
            apply(ws, r, group_style)
            ws.cell(r, 1, mo['method'])
            r += 1
            for c in mo.get('cases', []):
                apply(ws, r, case_style)
                values = [c['id'], numbered(c.get('preconditions', [])), c.get('inputs', ''),
                          expected_text(c), c.get('type', ''),
                          c.get('round1', 'Pending'), c.get('date1'), c.get('tester1'),
                          c.get('round2', 'Pending'), c.get('date2'), c.get('tester2'),
                          c.get('round3', 'Pending'), c.get('date3'), c.get('tester3'),
                          c.get('defects', ''), c.get('note', '')]
                for col, v in enumerate(values, 1):
                    ws.cell(r, col, v)
                ws.row_dimensions[r].height = height_for(
                    [(values[i], widths.get(col, 10)) for i, col in enumerate('ABCD')] + [(values[3], 34), (values[15], 24)])
                case_rows.append(r)
                r += 1
        last = max(r - 1, 13)
        # An added-but-empty validation serialises as <dataValidations count="0"/>, which is
        # schema-invalid and makes Excel offer a repair. Only attach validations to real cells.
        if case_rows:
            dv_status = DataValidation(type='list', formula1='"' + ','.join(STATUSES) + '"', allow_blank=True)
            dv_type = DataValidation(type='list', formula1='"' + ','.join(TYPES) + '"', allow_blank=True)
            ws.add_data_validation(dv_status)
            ws.add_data_validation(dv_type)
            for row, col in ((5, 'F'), (6, 'I'), (7, 'L')):
                for cr in case_rows:
                    dv_status.add(f'{col}{cr}')
            for cr in case_rows:
                dv_type.add(f'E{cr}')
        ws.freeze_panes = 'B12'
        built.append((entry, doc, len(case_rows)))
    wb.remove(proto)

    # Test Cases
    tc['D3'] = m['project']
    tc['D4'] = m['projectCode']
    tc['D5'] = m['book']['environment']
    tc['B6'] = m['book']['note']
    row_style = styles_of(tc, 9, 6)
    clear_from(tc, 9)
    for i, (entry, doc, n) in enumerate(built, 1):
        r = 8 + i
        apply(tc, r, row_style)
        tc[f'B{r}'] = i
        tc[f'C{r}'] = f"{entry['module']} {entry['feature']}"
        tc[f'D{r}'] = entry['sheet']
        tc[f'E{r}'] = f"{entry.get('description', '')} ({n} UTCs)"
        tc[f'F{r}'] = entry.get('precondition', '') or doc.get('precondition', '')
        tc.row_dimensions[r].height = height_for([(tc[f'E{r}'].value, 28), (tc[f'F{r}'].value, 30)])
        # Internal link: location on the element, not an external relationship
        tc[f'D{r}'].hyperlink = Hyperlink(ref=f'D{r}', location=f"'{entry['sheet']}'!A1", display=entry['sheet'])

    # Test Statistics
    data_style = styles_of(ts, 12, 9)
    total_style = styles_of(ts, 16, 9)
    cover_style = styles_of(ts, 18, 9)
    ts['B4'] = m['project']
    ts['B5'] = m['projectCode']
    ts['B6'] = f"{m['projectCode']}_Unit_Test_v{m['cover']['version']}"
    ts['F6'] = m['cover']['issueDate']
    ts['B7'] = m['book']['note'] + ' Statistics count the final round (Round 3).'
    clear_from(ts, 12)
    for i, (entry, _, _) in enumerate(built, 1):
        r = 11 + i
        apply(ts, r, data_style)
        ref = "'" + entry['sheet'].replace("'", "''") + "'"
        ts[f'A{r}'] = i
        ts[f'B{r}'] = entry['sheet']
        for col, cell in zip('CDEFGHI', ('B7', 'C7', 'D7', 'B9', 'C9', 'D9', 'B3')):
            ts[f'{col}{r}'] = f'={ref}!{cell}'
        ts[f'B{r}'].hyperlink = Hyperlink(ref=f'B{r}', location=f"'{entry['sheet']}'!A1", display=entry['sheet'])
    last = 11 + len(built)
    t = last + 1
    apply(ts, t, total_style)
    ts[f'B{t}'] = 'Sub total'
    for col in 'CDEFGHI':
        ts[f'{col}{t}'] = f'=SUM({col}12:{col}{last})'
    for label, formula in (
            ('Test coverage', f'=IFERROR((C{t}+D{t})*100/I{t},0)'),
            ('Test successful coverage', f'=IFERROR(C{t}*100/I{t},0)'),
            ('Normal case', f'=IFERROR(F{t}*100/I{t},0)'),
            ('Abnormal case', f'=IFERROR(G{t}*100/I{t},0)'),
            ('Boundary case', f'=IFERROR(H{t}*100/I{t},0)')):
        rr = t + 2 + ['Test coverage', 'Test successful coverage', 'Normal case', 'Abnormal case', 'Boundary case'].index(label)
        apply(ts, rr, cover_style)
        ts[f'B{rr}'] = label
        ts[f'D{rr}'] = formula
        ts[f'E{rr}'] = '%'

    target = REPORTS / m['book']['file']
    try:
        wb.save(target)
    except PermissionError:
        sys.exit(f'Cannot write {target.name}: close it in Excel and build again.')
    total = sum(n for _, _, n in built)
    print(f'{target.relative_to(ROOT)}: {len(built)} sheets, {total} UTCs')


def cmd_build(args):
    errors, todo = run_checks()
    if errors:
        for e in errors:
            print('ERROR', e)
        sys.exit(f'{len(errors)} error(s): fix them before building.')
    cmd_md(args)
    build_book(manifest())
    print(f'{len(todo)} method(s) still without UTCs')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=('scaffold', 'check', 'md', 'build'))
    args = parser.parse_args()
    return {'scaffold': cmd_scaffold, 'check': cmd_check,
            'md': cmd_md, 'build': cmd_build}[args.command](args) or 0


if __name__ == '__main__':
    sys.exit(main())
