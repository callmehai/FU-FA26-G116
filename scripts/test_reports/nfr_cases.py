"""System test report 5.4 (non-functional requirements): case JSON -> checks, Markdown previews, Excel.

The content lives in docs/08-reports/test-reports/cases/nfr/:
  plan.json      the sheets (one per quality attribute of Report 3 §IV.2), their layout, ID prefix and the
                 requirements each must cover. Only the coordinator edits it.
  <Sheet>.json   the cases of one sheet, written by whoever owns it
  preview/       generated Markdown, for reading in a pull request

Two layouts, copied from the course sample:
  gwt     Test Case ID | Scenario Name | Role / Session | Given | When | Then (Expected) | Priority | Status | Defect ID | Notes
  metric  Test Case ID | Scenario | Tool | Target | Actual | Status | Defect IDs | Notes

Usage (from the repository root):
  python scripts/test_reports/nfr_cases.py scaffold           # create missing <Sheet>.json, no cases
  python scripts/test_reports/nfr_cases.py check              # validate; exit 1 on an error
  python scripts/test_reports/nfr_cases.py md                 # write cases/nfr/preview/<Sheet>.md
  python scripts/test_reports/nfr_cases.py build              # check + md, then print what the workbook would hold
  python scripts/test_reports/nfr_cases.py build --overwrite  # ... back up the workbook to .bak-YYYYMMDD-HHMMSS and rebuild it

The visible Notes column holds only `reason`: one short sentence on why a case is Fail, Blocked or Skip, so the
printed report never shows those statuses without a reason. A Pass leaves Notes empty. `aiNote` holds the detail (who
ran it, measurements, run ids, environment); with the Report 3 requirement, the condition, the date and the tester it
goes to a hidden "AI context" column right of Notes, for agents and reviewers, not for the printed report.

The workbook is output: never edit it in Excel. Results (status, actual, date, tester, defect) go into the JSON.
"""
import argparse
import json
import re
import shutil
import sys
from copy import copy
from datetime import date, datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from integration_cases import (  # noqa: E402
    TEMPLATE as INT_TEMPLATE, apply, clear_from, harmonise, height_for, styles_of)

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'docs/08-reports/test-reports'
NFR = REPORTS / 'cases/nfr'
PLAN = NFR / 'plan.json'
PREVIEW = NFR / 'preview'
TEMPLATE = Path(__file__).resolve().parent / 'templates/nfr-template.xlsx'

STATUSES = ('Not Run', 'Pass', 'Fail', 'Blocked', 'Skip')
PRIORITIES = ('Critical', 'High', 'Medium', 'Low')
FIELDS = {
    'gwt': ('id', 'scenario', 'role', 'given', 'when', 'then', 'priority', 'status', 'defect', 'date', 'tester', 'notes'),
    'metric': ('id', 'scenario', 'tool', 'target', 'actual', 'status', 'defect', 'date', 'tester', 'notes'),
}
REQUIRED = {
    'gwt': ('id', 'requirement', 'scenario', 'role', 'given', 'when', 'then', 'priority'),
    'metric': ('id', 'requirement', 'scenario', 'tool', 'target', 'condition'),
}
FORBIDDEN = re.compile(r'Automated:|tests/|\.cs\b|\bAssert\.|dotnet test|\bfixture\b|frontend/src|backend/', re.I)
DATE = re.compile(r'^\d{4}-\d{2}-\d{2}$')


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def save_json(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def plan():
    return load(PLAN)


def sheet_file(sheet):
    return NFR / f"{sheet['sheet']}.json"


def text(v):
    return '\n'.join(v) if isinstance(v, list) else (v or '')


def ai_of(c):
    """Hidden AI context cell: the Report 3 requirement, the condition (metric), the detail, then who ran it and when."""
    parts = [f"Report 3 §IV: {c.get('requirement', '')}"]
    if c.get('condition'):
        parts.append(f"Condition: {c['condition']}")
    if c.get('aiNote'):
        parts.append(c['aiNote'])
    if c.get('date') or c.get('tester'):
        parts.append(' · '.join(x for x in (c.get('date'), c.get('tester')) if x))
    return '\n'.join(parts)


def values_of(layout, c):
    row = dict(c, status=c.get('status', 'Not Run'), defect=c.get('defect', ''), notes=c.get('reason', ''))
    return [text(row.get(k)) for k in FIELDS[layout]]


def cmd_scaffold(_):
    for s in plan()['sheets']:
        f = sheet_file(s)
        if f.exists():
            print(f'kept    {f.relative_to(ROOT)}')
            continue
        save_json(f, {'sheet': s['sheet'], 'cases': []})
        print(f'created {f.relative_to(ROOT)}')


def run_checks():
    errors, todo = [], []
    for s in plan()['sheets']:
        f = sheet_file(s)
        where = s['sheet']
        if not f.exists():
            errors.append(f'{where}: missing {f.name} (run scaffold)')
            continue
        doc = load(f)
        layout, wanted = s['layout'], s['requirements']
        covered = set()
        for n, c in enumerate(doc.get('cases', []), 1):
            cid = c.get('id', '?')
            expect = f"TC-{s['prefix']}-{n:03d}"
            if cid != expect:
                errors.append(f'{where}: {cid} should be {expect}')
            for key in REQUIRED[layout]:
                if not c.get(key):
                    errors.append(f'{where} {cid}: empty {key}')
            if c.get('requirement') and c['requirement'] not in wanted:
                errors.append(f"{where} {cid}: requirement \"{c['requirement']}\" is not in plan.json for this sheet")
            covered.add(c.get('requirement'))
            if layout == 'gwt' and c.get('priority') and c['priority'] not in PRIORITIES:
                errors.append(f'{where} {cid}: priority must be one of {PRIORITIES}')
            status = c.get('status', 'Not Run')
            if status not in STATUSES:
                errors.append(f'{where} {cid}: status must be one of {STATUSES}')
            if status != 'Not Run':
                if not DATE.match(str(c.get('date', ''))):
                    errors.append(f'{where} {cid}: status {status} needs date YYYY-MM-DD')
                if not c.get('tester'):
                    errors.append(f'{where} {cid}: status {status} needs tester')
            if status == 'Fail' and not c.get('defect'):
                errors.append(f'{where} {cid}: Fail needs defect (SCRUM-xxx or a short reason)')
            if status in ('Fail', 'Blocked', 'Skip') and not c.get('reason'):
                errors.append(f'{where} {cid}: {status} needs a short reason (shown in Notes)')
            if status in ('Pass', 'Not Run') and c.get('reason'):
                errors.append(f'{where} {cid}: reason is only for Fail, Blocked and Skip')
            if c.get('note'):
                errors.append(f'{where} {cid}: use reason (visible) or aiNote (hidden), not note')
            if layout == 'metric' and status in ('Pass', 'Fail') and not c.get('actual'):
                errors.append(f'{where} {cid}: {status} needs the measured actual')
            hit = FORBIDDEN.search(json.dumps(c, ensure_ascii=False))
            if hit:
                errors.append(f'{where} {cid}: test-code wording ({hit.group(0)})')
        for r in wanted:
            if r not in covered:
                todo.append(f'{where}: no case yet for "{r}"')
    return errors, todo


def cmd_check(_):
    errors, todo = run_checks()
    for e in errors:
        print('ERROR', e)
    for t in todo:
        print('TODO ', t)
    print(f'{len(errors)} error(s), {len(todo)} requirement(s) without a case')
    return 1 if errors else 0


HEAD = {
    'gwt': ('Test Case ID', 'Scenario Name', 'Role / Session', 'Given', 'When', 'Then (Expected)', 'Priority',
            'Status', 'Defect ID', 'Test Date', 'Tester', 'Notes'),
    'metric': ('Test Case ID', 'Scenario', 'Tool', 'Target', 'Actual', 'Status', 'Defect ID', 'Test Date', 'Tester', 'Notes'),
}


def cell_md(v):
    return v.replace('|', '\\|').replace('\n', '<br>')


def cmd_md(_):
    PREVIEW.mkdir(exist_ok=True)
    for s in plan()['sheets']:
        if not sheet_file(s).exists():
            continue
        doc, layout = load(sheet_file(s)), s['layout']
        lines = [f"# {s['sheet']} — Report 3 §IV.{s['attribute']}", '',
                 '_Generated by `scripts/test_reports/nfr_cases.py md`; do not edit._', '',
                 '| ' + ' | '.join(HEAD[layout]) + ' | AI context (hidden column) |',
                 '|' + '---|' * (len(HEAD[layout]) + 1)]
        for c in doc.get('cases', []):
            lines.append('| ' + ' | '.join(cell_md(v) for v in values_of(layout, c) + [ai_of(c)]) + ' |')
        (PREVIEW / f"{s['sheet']}.md").write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'previews written to {PREVIEW.relative_to(ROOT)}')
    return 0


def build_book(p):
    import openpyxl
    from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
    from openpyxl.utils import get_column_letter

    # Start from integration-template to inherit Cover & Test Statistics styling, then copy NFR sheet prototypes.
    wb = openpyxl.load_workbook(INT_TEMPLATE)
    nfr_wb = openpyxl.load_workbook(TEMPLATE)
    cover, ts = wb['Cover'], wb['Test Statistics']
    wb.remove(wb['Test Cases'])
    wb.remove(wb['Feature'])

    cover_info = p.get('cover', {})
    cover['B2'] = cover_info.get('title', 'SYSTEM TEST NFRs REPORT DOCUMENT')
    cover['B4'] = p.get('project', 'TrustCabinet')
    cover['B5'] = p.get('projectCode', 'SEP490-G116')
    cover['B6'] = '=B5&"_System_Test_NFRs_v"&F6'
    cover['F4'] = cover_info.get('creator', 'Nguyễn Trường Huy')
    cover['F5'] = cover_info.get('issueDate', '2026-11-27')
    cover['F6'] = cover_info.get('version', '1.1')
    change_style = styles_of(cover, 11, 6)
    clear_from(cover, 11)
    for i, ch in enumerate(cover_info.get('changes', [])):
        r = 11 + i
        apply(cover, r, change_style)
        for col, key in zip('ABCDEF', ('date', 'version', 'item', 'type', 'description', 'reference')):
            cover[f'{col}{r}'] = ch[key]
        cover.row_dimensions[r].height = height_for(
            [(ch['item'], 14.4), (ch['description'], 75.9), (ch['reference'], 48.1)], line_height=15)

    built = []
    for s in p['sheets']:
        proto = nfr_wb['GWT' if s['layout'] == 'gwt' else 'Metric']
        ws = wb.create_sheet(title=s['sheet'])
        for k, d in proto.column_dimensions.items():
            ws.column_dimensions[k].width = d.width
        ws.freeze_panes = proto.freeze_panes
        for dv in proto.data_validations.dataValidation:
            ws.add_data_validation(copy(dv))
        doc = load(sheet_file(s))
        headers = HEAD[s['layout']]
        ai_col = len(headers) + 1
        head_style = copy(proto.cell(1, 1)._style)
        for col_idx, h_text in enumerate(headers, 1):
            hc = ws.cell(1, col_idx, h_text)
            hc._style = copy(head_style)
            hc.alignment = Alignment(wrap_text=True, horizontal='center', vertical='center')
        for col_idx, w in ((len(headers) - 2, 13), (len(headers) - 1, 20), (len(headers), 32)):
            ws.column_dimensions[get_column_letter(col_idx)].width = w
        ws.row_dimensions[1].height = 24
        ah = ws.cell(1, ai_col, 'AI context')
        ah._style = copy(head_style)
        letter = get_column_letter(ai_col)
        ws.column_dimensions[letter].width = 60
        ws.column_dimensions[letter].hidden = True
        border = copy(proto.cell(1, 1).border)
        cases = doc.get('cases', [])
        for r, c in enumerate(cases, 2):
            vals = values_of(s['layout'], c) + [ai_of(c)]
            for col, v in enumerate(vals, 1):
                cell = ws.cell(r, col, v or None)
                cell.border = copy(border)
                cell.font = Font(name='Times New Roman', size=11)
                cell.alignment = Alignment(wrap_text=True, vertical='top')
            longest = max(len(line) for v in values_of(s['layout'], c) for line in (v.split('\n') or ['']))
            ws.row_dimensions[r].height = min(15 * max(2, longest // 28 + sum(v.count('\n') for v in values_of(s['layout'], c)) // 3 + 1), 300)
        built.append((s, len(cases)))

    # Populate Test Statistics sheet
    ts['B1'] = 'SYSTEM TEST NFRs STATISTICS'
    ts['C3'] = p.get('project', 'TrustCabinet')
    ts['C4'] = p.get('projectCode', 'SEP490-G116')
    ts['C5'] = f"{p.get('projectCode', 'SEP490-G116')}_System_Test_NFRs_v{cover_info.get('version', '1.1')}"
    ts['G5'] = cover_info.get('issueDate', '2026-11-27')
    ts['C6'] = 'Round 1 NFRs: Pass / Fail / Blocked / Skip / Not Run across Report 3 §IV.2 quality attributes.'
    ts.column_dimensions['C'].width = 28
    ts.column_dimensions['I'].width = 18
    for col, label in zip('BCDEFGHI', ('No', 'Quality Attribute', 'Pass', 'Fail', 'Blocked', 'Skip', 'Not Run', 'Total Test Cases')):
        ts[f'{col}10'] = label
        ts[f'{col}10']._style = copy(ts['H10']._style) if col == 'I' else copy(ts[f'{col}10']._style)

    data_style = styles_of(ts, 11, 8) + [copy(ts.cell(11, 8)._style)]
    total_style = styles_of(ts, 12, 8) + [copy(ts.cell(12, 8)._style)]
    cover_style = styles_of(ts, 14, 8) + [copy(ts.cell(14, 8)._style)]
    clear_from(ts, 11)
    for i, (s, n) in enumerate(built, 1):
        r = 10 + i
        apply(ts, r, data_style)
        ts[f'B{r}'] = i
        ts[f'C{r}'] = f"{s['sheet']} ({s['attribute']})"
        status_col = 'H' if s['layout'] == 'gwt' else 'F'
        last_row = max(n + 1, 2)
        rng = f"'{s['sheet']}'!{status_col}2:{status_col}{last_row}"
        for col, st in zip('DEFGH', ('Pass', 'Fail', 'Blocked', 'Skip', 'Not Run')):
            ts[f'{col}{r}'] = f'=COUNTIF({rng},"{st}")'
        ts[f'I{r}'] = f'=SUM(D{r}:H{r})'
        ts.row_dimensions[r].height = 24
    last = 10 + len(built)
    t = last + 1
    apply(ts, t, total_style)
    ts[f'C{t}'] = 'Sub total'
    for col in 'DEFGHI':
        ts[f'{col}{t}'] = f'=SUM({col}11:{col}{last})'
    ts.row_dimensions[t].height = 21

    thin = Side(style='thin')
    for k, (label, formula) in enumerate((
            ('Test coverage', f'=IFERROR((D{t}+E{t})*100/(I{t}-G{t}),0)'),
            ('Test successful coverage', f'=IFERROR(D{t}*100/(I{t}-G{t}),0)'))):
        r = t + 2 + k
        apply(ts, r, cover_style)
        ts[f'C{r}'] = label
        ts[f'E{r}'] = formula
        ts[f'F{r}'] = '%'
        for col_idx in range(2, 10):
            ts.cell(r, col_idx).border = Border(
                left=thin if col_idx == 2 else None,
                right=thin if col_idx == 9 else None,
                top=thin if k == 0 else None,
                bottom=thin if k == 1 else None,
            )
    ts.row_dimensions[3].height = 18
    ts.row_dimensions[4].height = 18
    ts.row_dimensions[5].height = 18
    ts.row_dimensions[6].height = 36

    # Harmonise Cover, Test Statistics and all NFR sheets to Times New Roman (no Tahoma / MS PGothic leftovers)
    for name in ('Cover', 'Test Statistics'):
        ws = wb[name]
        for row in ws.iter_rows():
            for cell in row:
                f = copy(cell.font)
                sz = 20 if (f.sz or 0) >= 20 else 11
                rgb = f.color.rgb if f.color is not None and f.color.type == 'rgb' else None
                colour = f.color if (cell.hyperlink or rgb not in ('FF993300', 'FF008000', 'FF0000FF')) else None
                cell.font = Font(name='Times New Roman', sz=sz, b=f.b, i=False if sz == 11 else f.i,
                                 u=f.u if cell.hyperlink else None, color=colour)
                fl = cell.fill
                if fl.fill_type == 'solid' and fl.fgColor.type == 'rgb' and fl.fgColor.rgb in ('FF000080', 'FF333399'):
                    cell.fill = PatternFill('solid', fgColor='FF76923C')
    for ws in wb.worksheets:
        if ws.title in ('Cover', 'Test Statistics'):
            continue
        for row in ws.iter_rows():
            for cell in row:
                f = cell.font
                cell.font = Font(name='Times New Roman', size=11, bold=f.bold,
                                 italic=f.italic, underline=f.underline,
                                 strike=f.strike, color=f.color, vertAlign=f.vertAlign)
    wb._named_styles['Normal'].font = Font(name='Times New Roman', sz=11)
    for idx in range(len(wb._fonts)):
        wf = wb._fonts[idx]
        wb._fonts[idx] = Font(name='Times New Roman', sz=wf.sz or 11, bold=wf.bold,
                              italic=wf.italic, underline=wf.underline,
                              strike=wf.strike, color=wf.color, vertAlign=wf.vertAlign)
    return wb


def cmd_build(a):
    errors, _ = run_checks()
    cmd_check(a)
    if errors:
        print('fix the errors first')
        return 1
    cmd_md(a)
    p = plan()
    for s in p['sheets']:
        print(f"{s['sheet']:<13} {len(load(sheet_file(s)).get('cases', []))} case(s)")
    target = REPORTS / p['file']
    if not a.overwrite:
        print(f'dry run; add --overwrite to write {target.relative_to(ROOT)}')
        return 0
    if target.exists():
        bak = target.with_name(f"{target.stem}.bak-{datetime.now():%Y%m%d-%H%M%S}{target.suffix}")
        shutil.copy2(target, bak)
        print(f'backup  {bak.relative_to(ROOT)}')
    build_book(p).save(target)
    print(f'wrote   {target.relative_to(ROOT)} ({date.today()})')
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    for name in ('scaffold', 'check', 'md'):
        sub.add_parser(name)
    b = sub.add_parser('build')
    b.add_argument('--overwrite', action='store_true')
    a = ap.parse_args()
    sys.exit({'scaffold': cmd_scaffold, 'check': cmd_check, 'md': cmd_md, 'build': cmd_build}[a.cmd](a) or 0)


if __name__ == '__main__':
    main()
