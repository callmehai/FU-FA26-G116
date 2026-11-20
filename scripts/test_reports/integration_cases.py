"""Integration test reports 5.2 (UI) and 5.2.2 (API): case JSON -> checks, Markdown previews, Excel.

The content lives in docs/08-reports/test-reports/cases/:
  sheets.json          which sheets exist, in which order, and which groups each one must cover
                       (UI: one group per function; API: one group per endpoint)
  routes.json          every /api route the backend maps, dumped from the running route table
  FE-xx/ui.json        UI cases of one feature   -> Report5.2_Integration_Test.xlsx
  FE-xx/api.json       API cases of one feature  -> Report5.2.2_Integration_Test_API_Test.xlsx
  (FE-xx/unit/ and functions.json belong to Report 5.1: scripts/test_reports/unit_cases.py)

Usage (from the repository root):
  python scripts/test_reports/integration_cases.py routes     # refresh routes.json (needs .NET SDK)
  python scripts/test_reports/integration_cases.py scaffold   # create the missing FE-xx/{ui,api}.json, groups only
  python scripts/test_reports/integration_cases.py check      # validate; exit 1 on an error
  python scripts/test_reports/integration_cases.py md         # write cases/preview/FE-xx/{ui,api}.md
  python scripts/test_reports/integration_cases.py sync-back  # merge executed Round/Test date/Tester cells from the old workbooks back into the JSON (by case id)
  python scripts/test_reports/integration_cases.py build      # dry-run: check, then print the diff old workbooks vs JSON (writes nothing)
  python scripts/test_reports/integration_cases.py build --overwrite  # check + md + Report 5.2.2 (backs the old file up first)
  python scripts/test_reports/integration_cases.py build --overwrite --with-ui  # ... and Report 5.2 as well (replaces the v1.2 workbook)

Report 5.2 is not rebuilt by default: the workbook in use is version 1.1 kept cell for cell plus 22 sheets
(see the test-reports README), and building it from FE-xx/ui.json would replace it.

The workbooks are output: never edit them in Excel, edit the JSON and build again.
JSON is the source of truth and the template is the frame: build always regenerates from the
template, never patches cells into an old workbook. `build` without flags writes nothing; only
`build --overwrite` writes, after copying each old file to `<name>.bak-<YYYYMMDD-HHMM>.xlsx`.
`sync-back` only fills Round/Test date/Tester cells that are non-empty and not Pending, matched
by case id (`<CODE>_FTnn` in the UI book, `<CODE>_API_ITnn` in the API book); description,
procedure, expected and every other standard field are never overwritten from Excel.
"""
import argparse
import datetime
import json
import math
import re
import shutil
import subprocess
import sys
from copy import copy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'docs/08-reports/test-reports'
CASES = REPORTS / 'cases'
TEMPLATE = Path(__file__).resolve().parent / 'templates/integration-template.xlsx'
CATALOGUE = ROOT / 'docs/03-api/00-danh-muc-ma-thong-bao.md'
BOOKS = ('ui', 'api')
STATUSES = ('N/A', 'Passed', 'Pending', 'Failed')
FORBIDDEN = re.compile(r'\bAssert\.|TestServer|dotnet test|\bfixture\b|WebApplicationFactory', re.I)


def load(path):
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def manifest():
    return load(CASES / 'sheets.json')


def case_file(book, sheet):
    return CASES / sheet['feature'] / f'{book}.json'


def id_prefix(book, code):
    return f'{code}_API_IT' if book == 'api' else f'{code}_FT'


# ---------------------------------------------------------------- routes

DUMP_TEST = '''using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace TrustCabinet.Web.Tests;

public class ZzIntegrationReportRouteDump
{
    [Fact]
    public void Dump()
    {
        using var factory = new TrustCabinetApiFactory();
        _ = factory.CreateClient();
        var rows = factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new List<string>(),
                route = "/" + e.RoutePattern.RawText?.TrimStart('/'),
            });
        File.WriteAllText(Environment.GetEnvironmentVariable("ROUTE_DUMP")!, JsonSerializer.Serialize(rows));
    }
}
'''


def normalise_route(route):
    route = re.sub(r'\{(\w+)(?::[^}]+)?\??\}', r'{\1}', route)
    return route.rstrip('/') or '/'


def cmd_routes(_):
    """Boot the app in memory through the test factory and read its route table."""
    probe = ROOT / 'tests/TrustCabinet.Web.Tests/ZzIntegrationReportRouteDump.cs'
    raw = CASES / '.routes-raw.json'
    probe.write_text(DUMP_TEST, encoding='utf-8')
    try:
        env = {**__import__('os').environ, 'ROUTE_DUMP': str(raw)}
        subprocess.run(['dotnet', 'test', 'tests/TrustCabinet.Web.Tests', '--filter',
                        'FullyQualifiedName~ZzIntegrationReportRouteDump', '--nologo', '-v', 'q'],
                       cwd=ROOT, env=env, check=True)
    finally:
        probe.unlink(missing_ok=True)
    rows = set()
    for r in load(raw):
        route = normalise_route(r['route'])
        if route.startswith('/api/'):
            for m in r['methods']:
                rows.add(f'{m} {route}')
    raw.unlink()
    ordered = sorted(rows, key=lambda s: (s.split(' ', 1)[1], s))
    save_json(CASES / 'routes.json', ordered)
    print(f'{len(ordered)} /api routes -> {CASES / "routes.json"}')


# ---------------------------------------------------------------- scaffold

def cmd_scaffold(_):
    m = manifest()
    for sheet in m['sheets']:
        for book in BOOKS:
            path = case_file(book, sheet)
            if path.exists():
                continue
            save_json(path, {
                'feature': sheet['feature'], 'code': sheet['code'], 'sheet': sheet['sheet'],
                'precondition': '',
                'groups': [{**g, 'cases': []} for g in sheet[book]['groups']]})
            print('created', path.relative_to(ROOT))


# ---------------------------------------------------------------- check

def test_methods():
    names = set()
    for f in (ROOT / 'tests').rglob('*.cs'):
        names.update(re.findall(r'public (?:async Task|void) (\w+)\(', f.read_text(encoding='utf-8')))
    return names


def run_checks():
    m = manifest()
    errors, todo = [], []
    routes = set(load(CASES / 'routes.json'))
    excluded = set(m.get('excludedRoutes', {}))
    methods = test_methods()
    codes = set(re.findall(r'\bMSG\d{2,3}\b', CATALOGUE.read_text(encoding='utf-8')))

    # Every route belongs to exactly one sheet, and every API group is a real route.
    owner = {}
    for sheet in m['sheets']:
        for g in sheet['api']['groups']:
            if g['title'] not in routes:
                errors.append(f"sheets.json {sheet['sheet']}: '{g['title']}' is not a route the backend maps")
            if g['title'] in owner:
                errors.append(f"sheets.json: '{g['title']}' is in both {owner[g['title']]} and {sheet['sheet']}")
            owner[g['title']] = sheet['sheet']
    for r in sorted(routes - set(owner) - excluded):
        errors.append(f'sheets.json: route {r} belongs to no sheet (add it to a sheet or to excludedRoutes)')
    names = [s['sheet'] for s in m['sheets']]
    for n in names:
        if len(n) > 31 or re.search(r'[\\/?*\[\]:]', n) or names.count(n) > 1:
            errors.append(f'sheets.json: bad or duplicate sheet name {n!r}')

    for sheet in m['sheets']:
        for book in BOOKS:
            path = case_file(book, sheet)
            where = path.relative_to(CASES).as_posix()
            if not path.exists():
                errors.append(f'{where}: missing (run scaffold)')
                continue
            doc = load(path)
            for key in ('feature', 'code', 'sheet'):
                if doc.get(key) != sheet[key]:
                    errors.append(f'{where}: {key} is {doc.get(key)!r}, sheets.json says {sheet[key]!r}')
            planned = [g['title'] for g in sheet[book]['groups']]
            actual = [g['title'] for g in doc['groups']]
            if actual != planned:
                errors.append(f'{where}: groups differ from sheets.json\n      planned {planned}\n      actual  {actual}')
            prefix = id_prefix(book, sheet['code'])
            n = 0
            for g in doc['groups']:
                if not g['cases']:
                    todo.append(f"{where}: {g['title']}")
                for c in g['cases']:
                    n += 1
                    cid = c.get('id', '?')
                    if cid != f'{prefix}{n:02d}':
                        errors.append(f'{where}: {cid} should be {prefix}{n:02d}')
                    for key in ('description', 'procedure', 'expected', 'preconditions', 'note'):
                        if not c.get(key):
                            errors.append(f'{where} {cid}: empty {key}')
                    for key in ('description', 'procedure', 'expected', 'preconditions'):
                        text = json.dumps(c.get(key, ''), ensure_ascii=False)
                        if FORBIDDEN.search(text):
                            errors.append(f'{where} {cid}: test-code wording in {key} ({FORBIDDEN.search(text).group(0)})')
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
    print(f'{len(errors)} error(s); {len(todo)} group(s) still without cases')
    return 1 if errors else 0


# ---------------------------------------------------------------- markdown preview

def md_cell(value):
    items = [value] if isinstance(value, str) else value
    text = items[0] if len(items) == 1 else '<br>'.join(f'{i}. {s}' for i, s in enumerate(items, 1))
    return text.replace('|', '\\|').replace('\n', '<br>')


def cmd_md(_):
    m = manifest()
    for sheet in m['sheets']:
        for book in BOOKS:
            path = case_file(book, sheet)
            if not path.exists():
                continue
            doc = load(path)
            out = [f"# {doc['sheet']} — {'API' if book == 'api' else 'UI'} ({doc['feature']}, {doc['code']})", '',
                   f"Requirement: {sheet[book]['requirement']}", '', f"Precondition: {doc['precondition'] or '—'}", '']
            for g in doc['groups']:
                out += [f"## {g['title']} ({', '.join(g['uc'])})", '']
                if not g['cases']:
                    out += ['*No case yet.*', '']
                    continue
                out += ['| ID | Description | Procedure | Expected | Pre-conditions | Note |', '|---|---|---|---|---|---|']
                for c in g['cases']:
                    out.append('| ' + ' | '.join(md_cell(c[k]) for k in
                               ('id', 'description', 'procedure', 'expected', 'preconditions', 'note')) + ' |')
                out.append('')
            target = CASES / 'preview' / sheet['feature'] / f'{book}.md'
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('\n'.join(out), encoding='utf-8', newline='\n')
    print('previews ->', (CASES / 'preview').relative_to(ROOT))


# ---------------------------------------------------------------- Excel

def numbered(items):
    return '\n'.join(f'{i}. {s}' for i, s in enumerate(items, 1)) if len(items) > 1 else items[0]


def bulleted(items):
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


def build_book(book, m):
    import openpyxl
    from openpyxl.worksheet.datavalidation import DataValidation

    meta = m['books'][book]
    wb = openpyxl.load_workbook(TEMPLATE)
    cover, tc, ts, proto = wb['Cover'], wb['Test Cases'], wb['Test Statistics'], wb['Feature']

    # Cover
    cover['B2'] = meta['title']
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

    # Feature sheets, cloned from the prototype
    group_style = styles_of(proto, 11, 15)
    case_style = styles_of(proto, 12, 15)
    widths = {k: v.width for k, v in proto.column_dimensions.items()}
    widths['O'] = 32
    built = []
    for sheet in m['sheets']:
        doc = load(case_file(book, sheet))
        ws = wb.copy_worksheet(proto)
        ws.title = sheet['sheet']
        clear_from(ws, 11)
        for k, w in widths.items():
            ws.column_dimensions[k].width = w
        ws['B2'] = sheet['sheet']
        ws['B3'] = sheet[book]['requirement']
        r, case_rows = 11, []
        for g in doc['groups']:
            apply(ws, r, group_style)
            ws.cell(r, 1, g['title'])
            ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=15)
            r += 1
            for c in g['cases']:
                apply(ws, r, case_style)
                values = [c['id'], c['description'], numbered(c['procedure']), numbered(c['expected']),
                          bulleted(c['preconditions']), c.get('round1', 'Pending'), c.get('date1'), c.get('tester1'),
                          c.get('round2'), c.get('date2'), c.get('tester2'),
                          c.get('round3'), c.get('date3'), c.get('tester3'), c['note']]
                for col, v in enumerate(values, 1):
                    ws.cell(r, col, v)
                ws.row_dimensions[r].height = height_for(
                    [(values[i], widths.get(col, 10)) for i, col in enumerate('ABCDE')] + [(values[14], widths['O'])])
                case_rows.append(r)
                r += 1
        last = max(r - 1, 12)
        ws['B4'] = len(case_rows)
        # An added-but-empty validation serialises as <dataValidations count="0"/>, which is
        # schema-invalid and makes Excel offer a repair. Only attach it to real cells.
        if case_rows:
            dv = DataValidation(type='list', formula1='"' + ','.join(STATUSES) + '"', allow_blank=True)
            ws.add_data_validation(dv)
        for row, col in ((6, 'F'), (7, 'I'), (8, 'L')):
            rng = f'{col}12:{col}{last}'
            ws[f'B{row}'] = f'=COUNTIF({rng},"Passed")'
            ws[f'C{row}'] = f'=COUNTIF({rng},"Failed")'
            ws[f'E{row}'] = f'=COUNTIF({rng},"N/A")'
            ws[f'D{row}'] = f'=B4-B{row}-C{row}-E{row}'
            for cr in case_rows:
                dv.add(f'{col}{cr}')
        ws.freeze_panes = 'B11'
        built.append((sheet, doc, len(case_rows)))
    wb.remove(proto)

    # Test Cases
    tc['D3'] = m['project']
    tc['D4'] = m['projectCode']
    tc['D5'] = meta['environment']
    tc['B6'] = meta['note']
    row_style = styles_of(tc, 9, 6)
    clear_from(tc, 9)
    for i, (sheet, doc, n) in enumerate(built, 1):
        r = 8 + i
        apply(tc, r, row_style)
        tc[f'B{r}'] = i
        tc[f'C{r}'] = f"{sheet['feature']} {sheet['function']}"
        tc[f'D{r}'] = sheet['sheet']
        tc[f'E{r}'] = f"{sheet[book]['requirement']} ({n} cases)"
        tc[f'F{r}'] = doc['precondition']
        tc.row_dimensions[r].height = height_for([(tc[f'E{r}'].value, 28), (doc['precondition'], 30)])

    # Test Statistics
    data_style = styles_of(ts, 11, 8)
    total_style = styles_of(ts, 12, 8)
    cover_style = styles_of(ts, 14, 8)
    ts['C3'] = m['project']
    ts['C4'] = m['projectCode']
    ts['C5'] = f"{m['projectCode']}_{'API_Test' if book == 'api' else 'Integration_Test'}_v{m['cover']['version']}"
    ts['G5'] = m['cover']['issueDate']
    ts['C6'] = meta['note']
    clear_from(ts, 11)
    for i, (sheet, _, _) in enumerate(built, 1):
        r = 10 + i
        apply(ts, r, data_style)
        ref = "'" + sheet['sheet'].replace("'", "''") + "'"
        ts[f'B{r}'] = i
        ts[f'C{r}'] = sheet['sheet']
        for col, cell in zip('DEFGH', ('B6', 'C6', 'D6', 'E6', 'B4')):
            ts[f'{col}{r}'] = f'={ref}!{cell}'
    last = 10 + len(built)
    t = last + 1
    apply(ts, t, total_style)
    ts[f'C{t}'] = 'Sub total'
    for col in 'DEFGH':
        ts[f'{col}{t}'] = f'=SUM({col}11:{col}{last})'
    for k, (label, formula) in enumerate((
            ('Test coverage', f'=IFERROR((D{t}+E{t})*100/(H{t}-G{t}),0)'),
            ('Test successful coverage', f'=IFERROR(D{t}*100/(H{t}-G{t}),0)'))):
        r = t + 2 + k
        apply(ts, r, cover_style)
        ts[f'C{r}'] = label
        ts[f'E{r}'] = formula
        ts[f'F{r}'] = '%'

    target = REPORTS / meta['file']
    try:
        wb.save(target)
    except PermissionError:
        sys.exit(f'Cannot write {target.name}: close it in Excel and build again.')
    total = sum(n for _, _, n in built)
    print(f'{target.relative_to(ROOT)}: {len(built)} sheets, {total} cases')


# ---------------------------------------------------------------- safe rebuild: sync-back, dry-run, --overwrite

#: Columns of the executed results in a feature sheet (1-based): Round 1/2/3, Test date, Tester.
#: Same column positions as the 5.1 unit sheets (only the case-id shape differs).
RESULT_COLS = {'round1': 6, 'date1': 7, 'tester1': 8,
               'round2': 9, 'date2': 10, 'tester2': 11,
               'round3': 12, 'date3': 13, 'tester3': 14}
#: Case-id shapes per book: `<CODE>_FTnn` (UI) vs `<CODE>_API_ITnn` (API).
ID_PATTERNS = {'ui': re.compile(r'^.+_FT\d+$'), 'api': re.compile(r'^.+_API_IT\d+$')}
#: Sheets that hold no case rows.
NON_CASE_SHEETS = {'Cover', 'Test Cases', 'Test Statistics', 'Feature'}
FIRST_CASE_ROW = 11


def _cell_text(value):
    """Excel cell -> comparable text. Dates become ISO strings; everything else is stripped."""
    if value is None:
        return ''
    if isinstance(value, datetime.datetime):
        return value.date().isoformat() if value.time() == datetime.time() else value.isoformat(sep=' ')
    if isinstance(value, datetime.date):
        return value.isoformat()
    return str(value).strip()


def _backup(target):
    stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M')
    bak = target.with_name(f'{target.stem}.bak-{stamp}.xlsx')
    shutil.copy2(target, bak)
    print(f'backup -> {bak.relative_to(ROOT)}')
    return bak


def _sheet_ids(ws, book):
    """Case IDs of a feature sheet in row order (column A, `<CODE>_FTnn` or `<CODE>_API_ITnn`)."""
    pat = ID_PATTERNS[book]
    ids = []
    for row in range(FIRST_CASE_ROW, (ws.max_row or 0) + 1):
        v = _cell_text(ws.cell(row, 1).value)
        if pat.match(v):
            ids.append(v)
    return ids


def _book_target(m, book):
    return REPORTS / m['books'][book]['file']


def cmd_sync_back(_):
    """Merge executed results from the old workbooks back into the JSON, by case id.

    Only Round 1/2/3, Test date and Tester cells that are non-empty and not
    Pending are merged. Description, procedure, expected and every other
    standard field are never overwritten from Excel.
    """
    import openpyxl
    m = manifest()
    grand_cases, grand_cells = 0, 0
    for book in BOOKS:
        target = _book_target(m, book)
        if not target.exists():
            print(f'[{book}] no existing workbook at {target.relative_to(ROOT)}; nothing to sync-back.')
            continue
        wb = openpyxl.load_workbook(target, data_only=True)
        by_sheet = {s['sheet']: s for s in m['sheets']}
        total_cases, total_cells = 0, 0
        for name in wb.sheetnames:
            if name in NON_CASE_SHEETS:
                continue
            sheet = by_sheet.get(name)
            if sheet is None:
                print(f'[{book}] SKIP {name}: not in sheets.json')
                continue
            path = case_file(book, sheet)
            if not path.exists():
                print(f'[{book}] SKIP {name}: {path.relative_to(ROOT)} missing')
                continue
            ws = wb[name]
            wanted = {}
            for row in range(FIRST_CASE_ROW, (ws.max_row or 0) + 1):
                cid = _cell_text(ws.cell(row, 1).value)
                if ID_PATTERNS[book].match(cid):
                    vals = {k: _cell_text(ws.cell(row, c).value) for k, c in RESULT_COLS.items()}
                    vals = {k: v for k, v in vals.items() if v and v != 'Pending'}
                    if vals:
                        wanted[cid] = vals
            if not wanted:
                print(f'[{book}] {name}: 0 result(s) merged (no executed cell beyond Pending)')
                continue
            doc = load(path)
            index = {}
            for g in doc.get('groups', []):
                for c in g.get('cases', []):
                    index[c.get('id')] = c
            n_cases, n_cells = 0, 0
            for cid, vals in wanted.items():
                c = index.get(cid)
                if c is None:
                    print(f'  WARN [{book}] {name} {cid}: in the workbook but not in the JSON (left as is)')
                    continue
                for k, v in vals.items():
                    if c.get(k) != v:
                        c[k] = v
                        n_cells += 1
                n_cases += 1
            if n_cells:
                save_json(path, doc)
            total_cases += n_cases
            total_cells += n_cells
            print(f'[{book}] {name}: {n_cases} case(s), {n_cells} cell(s) merged')
        print(f'[{book}] sync-back total: {total_cases} case(s), {total_cells} cell(s)')
        grand_cases += total_cases
        grand_cells += total_cells
    print(f'sync-back grand total: {grand_cases} case(s), {grand_cells} cell(s)')
    return 0


def _json_counts(m, book):
    """sheet -> (case count, {ids}) from the JSON of one book."""
    out = {}
    for sheet in m['sheets']:
        path = case_file(book, sheet)
        ids = []
        if path.exists():
            for g in load(path).get('groups', []):
                ids += [c.get('id') for c in g.get('cases', [])]
        out[sheet['sheet']] = (len(ids), set(ids))
    return out


def _workbook_counts(target, book):
    """(cover version, {sheet -> (count, {ids})}) from an existing workbook."""
    import openpyxl
    wb = openpyxl.load_workbook(target, data_only=True)
    cover = ''
    if 'Cover' in wb.sheetnames:
        cover = _cell_text(wb['Cover']['F6'].value)
    sheets = {}
    for name in wb.sheetnames:
        if name in NON_CASE_SHEETS:
            continue
        ids = _sheet_ids(wb[name], book)
        sheets[name] = (len(ids), set(ids))
    return cover, sheets


def cmd_dry_run(m=None):
    """Print the diff between the old workbooks (if any) and the JSON; writes nothing."""
    m = m or manifest()
    for book in BOOKS:
        new = _json_counts(m, book)
        target = _book_target(m, book)
        new_total = sum(n for n, _ in new.values())
        print(f'--- [{book}] {target.name} ---')
        if not target.exists():
            print(f'no existing workbook at {target.relative_to(ROOT)}: build would create '
                  f'{len(new)} sheet(s), {new_total} case(s).')
            continue
        old_cover, old = _workbook_counts(target, book)
        print(f'old workbook: {target.relative_to(ROOT)} ({len(old)} sheets, '
              f'{sum(n for n, _ in old.values())} cases); cover version {old_cover or "?"} '
              f'-> {m["cover"]["version"]}')
        added = [s for s in new if s not in old]
        removed = [s for s in old if s not in new]
        print(f'sheets: {len(old)} -> {len(new)}'
              + (f'; added: {added}' if added else '; no added sheet')
              + (f'; removed: {removed}' if removed else '; no removed sheet'))
        for name in new:
            if name in old:
                on, oids = old[name]
                nn, nids = new[name]
                flag = '' if (on == nn and oids == nids) else '  *'
                extra = ''
                if oids != nids:
                    extra = (f' (+{sorted(nids - oids)[:5]}{"..." if len(nids - oids) > 5 else ""}'
                             f' / -{sorted(oids - nids)[:5]}{"..." if len(oids - nids) > 5 else ""})')
                print(f'  {name}: {on} -> {nn}{extra}{flag}')
            else:
                print(f'  {name}: (new) {new[name][0]} cases  *')
        for name in removed:
            print(f'  {name}: ({old[name][0]} cases, removed)  *')
        print(f'total: {sum(n for n, _ in old.values())} -> {new_total} cases')
    return 0


def cmd_build(args):
    errors, todo = run_checks()
    if errors:
        for e in errors:
            print('ERROR', e)
        sys.exit(f'{len(errors)} error(s): fix them before building.')
    if not args.overwrite:
        print('dry-run (no file written; re-run with --overwrite to write). check passed.')
        cmd_dry_run()
        print(f'{len(todo)} group(s) still without cases')
        return 0
    m = manifest()
    books = BOOKS if args.with_ui else tuple(b for b in BOOKS if b != 'ui')
    if 'ui' not in books:
        print('Report 5.2 skipped: it is the v1.2 workbook, not built from cases/ (pass --with-ui to replace it)')
    for book in books:
        target = _book_target(m, book)
        if target.exists():
            _backup(target)
    cmd_md(args)
    for book in books:
        build_book(book, m)
    print(f'{len(todo)} group(s) still without cases')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=('routes', 'scaffold', 'check', 'md', 'sync-back', 'build'))
    parser.add_argument('--overwrite', action='store_true',
                        help='build only: back the old workbooks up to <name>.bak-<YYYYMMDD-HHMM>.xlsx and write them (default is a dry-run diff)')
    parser.add_argument('--with-ui', action='store_true',
                        help='build: also rebuild Report 5.2 from FE-xx/ui.json, replacing the v1.2 workbook')
    args = parser.parse_args()
    return {'routes': cmd_routes, 'scaffold': cmd_scaffold, 'check': cmd_check,
            'md': cmd_md, 'sync-back': cmd_sync_back, 'build': cmd_build}[args.command](args) or 0


if __name__ == '__main__':
    sys.exit(main())
