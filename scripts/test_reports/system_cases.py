"""System test report 5.3: workflow case JSON -> checks, Markdown previews, Excel.

The content lives in docs/08-reports/test-reports/cases/system/:
  plan.json      the workflows (one sheet each), their order and the groups (steps) each must cover.
                 Only the coordinator edits it.
  WF-xx.json     the cases of one workflow, written by whoever owns it
  preview/       generated Markdown, for reading in a pull request

The visible Note column is left empty. Everything a note would say goes in `aiNote` (BF · UC · BR · MSG · 5.2 trace,
who ran it, run id, environment, why a case changed); it goes to a hidden "AI context" column right of Note, for
agents and reviewers who read the file, not for the printed report.

Usage (from the repository root):
  python scripts/test_reports/system_cases.py scaffold           # create missing WF-xx.json, groups only
  python scripts/test_reports/system_cases.py check              # validate; exit 1 on an error
  python scripts/test_reports/system_cases.py md                 # write cases/system/preview/WF-xx.md
  python scripts/test_reports/system_cases.py build              # check + md, then print what the workbook would hold
  python scripts/test_reports/system_cases.py build --overwrite  # ... back up the workbook to .bak-YYYYMMDD-HHMMSS and rebuild it

The workbook is output: never edit it in Excel. Results (round1/date1/tester1 ...) go into the JSON.
"""
import argparse
import math
import re
import shutil
import sys
from copy import copy
from datetime import date, datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from integration_cases import (  # noqa: E402  shared Excel helpers, same template layout
    ROOT, REPORTS, TEMPLATE, apply, bulleted, clear_from, load, numbered, save_json, styles_of)
from openpyxl.utils import get_column_letter  # noqa: E402

SYSTEM = REPORTS / 'cases/system'
PLAN = SYSTEM / 'plan.json'
PREVIEW = SYSTEM / 'preview'
STATUSES = ('Passed', 'Failed', 'Pending', 'N/A')
REQUIRED = ('id', 'description', 'procedure', 'expected', 'preconditions', 'aiNote')
AI_COL = 16  # column P, right of Note (O), hidden
# A system case describes what a person does on the running product, never how the code is tested.
FORBIDDEN = re.compile(r'Automated:|tests/|\.cs\b|\bAssert\.|dotnet test|\bfixture\b|frontend/src|backend/', re.I)
DATE = re.compile(r'^\d{4}-\d{2}-\d{2}$')


def height_for(values_and_widths, line_height=15):
    # Same conservative rule as the fixed integration script: Calibri 11 averages
    # ~1 char per width unit, +2 lines cover rounding (Excel never breaks mid-word).
    lines = 1
    for value, width in values_and_widths:
        if not value:
            continue
        per_line = max(int(width * 1.0), 8)
        n = sum(max(1, math.ceil(len(part) / per_line)) for part in str(value).split('\n'))
        lines = max(lines, n)
    return min(409, lines * line_height + 6)


HEADER_BLUE = ('FF000080', 'FF333399')     # house-template header fills of the first three sheets
HEADER_GREEN = 'FF76923C'                  # header fill of the feature sheets
TEXT_COLOURS = ('FF993300', 'FF008000', 'FF0000FF')   # brown labels, green notes, blue descriptions


def harmonise(wb):
    """Give Cover, Test Cases and Test Statistics the look of the feature sheets: Times New Roman 11,
    the green header fill, thin borders, black text (links stay blue), instead of the house template's
    Tahoma, navy headers, hair borders and brown/green/blue text."""
    from openpyxl.styles import Border, Font, PatternFill, Side
    thin = Side(style='thin')
    for name in ('Cover', 'Test Cases', 'Test Statistics'):
        ws = wb[name]
        for row in ws.iter_rows():
            for cell in row:
                font = copy(cell.font)
                size = 20 if (font.sz or 0) >= 20 else 11
                rgb = font.color.rgb if font.color is not None and font.color.type == 'rgb' else None
                if cell.hyperlink:
                    colour = font.color
                elif rgb in TEXT_COLOURS:
                    colour = None
                else:
                    colour = font.color
                cell.font = Font(name='Times New Roman', sz=size, b=font.b, i=False if size == 11 else font.i,
                                 u=font.u if cell.hyperlink else None, color=colour)
                fill = cell.fill
                if fill.fill_type == 'solid' and fill.fgColor.type == 'rgb' and fill.fgColor.rgb in HEADER_BLUE:
                    cell.fill = PatternFill('solid', fgColor=HEADER_GREEN)
                border = cell.border
                if any(side is not None and side.style == 'hair' for side in (border.left, border.right, border.top, border.bottom)):
                    cell.border = Border(*(thin if side is not None and side.style else side
                                           for side in (border.left, border.right, border.top, border.bottom)))
                # Every field wraps; headers stay centred, data stays readable without manual resize.
                al = copy(cell.alignment)
                al.wrap_text = True
                cell.alignment = al

    # Unify all workflow sheet cells and template font entries to Times New Roman 11.
    # NOTE: never copy() a StyleProxy — build a fresh Font and clear template scheme entries.
    from openpyxl.styles import Font as _IFont
    for ws in wb.worksheets:
        if ws.title in ('Cover', 'Test Cases', 'Test Statistics'):
            continue
        for row in ws.iter_rows():
            for cell in row:
                font = cell.font
                cell.font = _IFont(name='Times New Roman', size=11, bold=font.bold,
                                   italic=font.italic, underline=font.underline,
                                   strike=font.strike, color=font.color,
                                   vertAlign=font.vertAlign)
                al = copy(cell.alignment)
                al.wrap_text = True
                cell.alignment = al
    normal = wb._named_styles['Normal']
    normal.font = Font(name='Times New Roman', sz=11)
    for idx in range(len(wb._fonts)):
        wf = wb._fonts[idx]
        wb._fonts[idx] = _IFont(name='Times New Roman', sz=wf.sz or 11, bold=wf.bold,
                                italic=wf.italic, underline=wf.underline,
                                strike=wf.strike, color=wf.color, vertAlign=wf.vertAlign)


def plan():
    return load(PLAN)


def wf_file(wf):
    return SYSTEM / f"{wf['id']}.json"


def wf_number(wf):
    return int(wf['id'].split('-')[1])


def cmd_scaffold(_):
    made = 0
    for wf in plan()['workflows']:
        path = wf_file(wf)
        if path.exists():
            continue
        save_json(path, {'workflow': wf['id'], 'groups': [{'title': g, 'cases': []} for g in wf['groups']]})
        print('created', path.relative_to(ROOT))
        made += 1
    print(f'{made} file(s) created')


def run_checks():
    errors, todo, seen = [], [], set()
    p = plan()
    r52_ids = set()
    for ui_path in (REPORTS / 'cases').glob('FE-*/ui.json'):
        ui_doc = load(ui_path)
        for ug in ui_doc.get('groups', []):
            for uc in ug.get('cases', []):
                r52_ids.add(uc['id'])
    sheets = [wf['sheet'] for wf in p['workflows']]
    if len(set(sheets)) != len(sheets):
        errors.append('plan.json: duplicate sheet name')
    for wf in p['workflows']:
        path = wf_file(wf)
        where = path.name
        if not path.exists():
            todo.append(f"{wf['id']}: no file yet (run scaffold)")
            continue
        doc = load(path)
        if doc.get('workflow') != wf['id']:
            errors.append(f"{where}: 'workflow' must be {wf['id']}")
        titles = [g['title'] for g in doc.get('groups', [])]
        if titles != wf['groups']:
            errors.append(f'{where}: groups must be exactly, in order, {wf["groups"]} (got {titles})')
        prefix = f'SYS_WF{wf_number(wf):02d}_TC'
        expected_n = 1
        for g in doc.get('groups', []):
            if not g.get('cases'):
                todo.append(f"{wf['id']} / {g['title']}: no case yet")
            for c in g.get('cases', []):
                cid = c.get('id', '?')
                at = f'{where} {cid}'
                for key in REQUIRED:
                    if not c.get(key):
                        errors.append(f'{at}: missing {key}')
                if cid != f'{prefix}{expected_n:02d}':
                    errors.append(f'{at}: id must be {prefix}{expected_n:02d} (numbered in order through the workflow)')
                expected_n += 1
                if cid in seen:
                    errors.append(f'{at}: duplicate id')
                seen.add(cid)
                for key in ('procedure', 'expected', 'preconditions'):
                    if key in c and not isinstance(c[key], list):
                        errors.append(f'{at}: {key} must be a list of strings')
                if not str(c.get('aiNote', '')).startswith(wf['bf']):
                    errors.append(f"{at}: aiNote must start with {wf['bf']} (trace: BF · UC · BR · MSG · 5.2 ids)")
                if c.get('note'):
                    errors.append(f'{at}: the Note column stays empty; put it in aiNote')
                text = ' '.join(str(v) for v in c.values())
                if FORBIDDEN.search(text):
                    errors.append(f'{at}: mentions test code or a source path ({FORBIDDEN.search(text).group(0)!r})')
                for n in (1, 2, 3):
                    status = c.get(f'round{n}')
                    if status is None:
                        continue
                    if status not in STATUSES:
                        errors.append(f'{at}: round{n} must be one of {STATUSES}')
                    if status in ('Passed', 'Failed'):
                        d = str(c.get(f'date{n}', ''))
                        if not DATE.match(d):
                            errors.append(f'{at}: round{n} {status} needs date{n} as YYYY-MM-DD')
                        if not c.get(f'tester{n}'):
                            errors.append(f'{at}: round{n} {status} needs tester{n}')
                    if status == 'Failed' and 'Bug:' not in str(c.get('aiNote', '')):
                        errors.append(f'{at}: a Failed round needs "Bug: <Jira key or short reason>" in aiNote')
                for m52 in re.findall(r'5\.2:\s*([A-Z]+_FT\d+(?:\s*,\s*[A-Z]+_FT\d+)*)', str(c.get('aiNote', ''))):
                    for ref_id in re.split(r'\s*,\s*', m52):
                        if ref_id not in r52_ids:
                            errors.append(f'{at}: broken 5.2 reference {ref_id}')
    return errors, todo


def cmd_check(_):
    errors, todo = run_checks()
    for e in errors:
        print('ERROR', e)
    for t in todo:
        print('TODO ', t)
    print(f'{len(errors)} error(s), {len(todo)} open item(s)')
    return 1 if errors else 0


def md_cell(value):
    if isinstance(value, list):
        value = '<br>'.join(f'{i}. {v}' for i, v in enumerate(value, 1))
    return str(value or '').replace('|', '\\|').replace('\n', '<br>')


def cmd_md(_):
    PREVIEW.mkdir(parents=True, exist_ok=True)
    for wf in plan()['workflows']:
        path = wf_file(wf)
        if not path.exists():
            continue
        doc = load(path)
        lines = [f"# {wf['id']} {wf['name']} ({wf['sheet']})", '',
                 '> Generated by `scripts/test_reports/system_cases.py md` — do not edit.', '',
                 f"**Flow:** {wf['bf']} · **Description:** {wf['description']}", '',
                 f"**Pre-condition:** {wf['precondition']}", '']
        for g in doc['groups']:
            lines += [f"## {g['title']}", '']
            if not g['cases']:
                lines += ['_No case yet._', '']
                continue
            lines += ['| ID | Description | Procedure | Expected | Pre-conditions | R1 | AI context (hidden column) |',
                      '|---|---|---|---|---|---|---|']
            for c in g['cases']:
                lines.append('| ' + ' | '.join(md_cell(x) for x in (
                    c['id'], c['description'], c['procedure'], c['expected'], c['preconditions'],
                    c.get('round1', 'Pending'), c['aiNote'])) + ' |')
            lines.append('')
        (PREVIEW / f"{wf['id']}.md").write_text('\n'.join(lines), encoding='utf-8', newline='\n')
    print(f'previews in {PREVIEW.relative_to(ROOT)}')


def visible_note(c):
    status = c.get('round1', 'Pending')
    ai = str(c.get('aiNote', ''))
    parts = [pt.strip() for pt in ai.split('·') if pt.strip()]
    kept = []
    for pt in parts:
        if re.fullmatch(r'(?:BF-\d+|UC-\d+[a-z]?|BR-\d{3}|MSG\d{2,3}|EX-\d+|AF-\d+)(?:\s*,\s*(?:BF-\d+|UC-\d+[a-z]?|BR-\d{3}|MSG\d{2,3}|EX-\d+|AF-\d+))*', pt):
            continue
        kept.append(pt)
    if status == 'Pending':
        return ' | '.join(kept) if kept else 'Pending — not executed yet'
    return f"{status}: {' | '.join(kept)}" if kept else status


def build_workbook(p, target):
    import openpyxl
    from openpyxl.worksheet.datavalidation import DataValidation

    wb = openpyxl.load_workbook(TEMPLATE)
    cover, tc, ts, proto = wb['Cover'], wb['Test Cases'], wb['Test Statistics'], wb['Feature']

    cover['B2'] = p['title']
    cover['B4'] = p['project']
    cover['B5'] = p['projectCode']
    cover['B6'] = '=B5&"_System_Test_v"&F6'
    cover['F4'] = p['cover']['creator']
    cover['F5'] = p['cover']['issueDate']
    cover['F6'] = p['cover']['version']
    change_style = styles_of(cover, 11, 6)
    clear_from(cover, 11)
    for i, ch in enumerate(p['cover']['changes']):
        r = 11 + i
        apply(cover, r, change_style)
        for col, key in zip('ABCDEF', ('date', 'version', 'item', 'type', 'description', 'reference')):
            cover[f'{col}{r}'] = ch[key]
        cover.row_dimensions[r].height = height_for(
            [(ch['item'], 14.4), (ch['description'], 75.9), (ch['reference'], 48.1)], line_height=15)

    group_style = styles_of(proto, 11, 16)
    case_style = styles_of(proto, 12, 16)
    widths = {k: v.width for k, v in proto.column_dimensions.items()}
    widths['O'] = 42
    built = []
    for wf in p['workflows']:
        path = wf_file(wf)
        doc = load(path) if path.exists() else {'groups': [{'title': g, 'cases': []} for g in wf['groups']]}
        ws = wb.copy_worksheet(proto)
        ws.title = wf['sheet']
        clear_from(ws, 11)
        for k, w in widths.items():
            ws.column_dimensions[k].width = w
        ws['O10'] = 'Note'  # the course template's heading; the integration template says "Actual result ..."
        ai_head = ws.cell(10, AI_COL, 'AI context')
        ai_head._style = copy(ws['O10']._style)
        ws.column_dimensions[get_column_letter(AI_COL)].width = 60
        ws.column_dimensions[get_column_letter(AI_COL)].hidden = True
        ws['B2'] = wf['name']
        ws['B3'] = f"System-level test flow ({wf['bf']}): {wf['description']}"
        ws.row_dimensions[10].height = 18
        r, case_rows = 11, []
        for g in doc['groups']:
            apply(ws, r, group_style)
            ws.cell(r, 1, g['title'])
            ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=15)
            ws.row_dimensions[r].height = 24.0  # single-line group header; template rows vary
            r += 1
            for c in g['cases']:
                apply(ws, r, case_style)
                values = [c['id'], c['description'], numbered(c['procedure']), numbered(c['expected']),
                          bulleted(c['preconditions']), c.get('round1', 'Pending'), c.get('date1'), c.get('tester1'),
                          c.get('round2'), c.get('date2'), c.get('tester2'),
                          c.get('round3'), c.get('date3'), c.get('tester3'), visible_note(c)]
                for col, v in enumerate(values, 1):
                    if v and col in (7, 10, 13):
                        v = date.fromisoformat(v)
                    ws.cell(r, col, v)
                ai = ws.cell(r, AI_COL, c['aiNote'])
                ai._style = copy(ws.cell(r, 15)._style)
                ws.row_dimensions[r].height = height_for(
                    # Hidden P (long aiNote) must not drive the height.
                    [(values[i], widths.get(col, 10)) for i, col in enumerate('ABCDE')] + [(values[14], widths['O'])])
                case_rows.append(r)
                r += 1
        last = max(r - 1, 12)
        ws['B4'] = f'=COUNTIF(A12:A{last},"SYS_*")'
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
        built.append((wf, len(case_rows)))
    wb.remove(proto)

    tc['D3'] = p['project']
    tc['D4'] = p['projectCode']
    # 5.3 prints the real Round 1 environment (plan.json) and a 5.3-specific
    # execution note, not the integration template's leftover text (old role
    # names, "the attendee UI is not implemented", "remain in Report 5.2.2").
    tc['D5'] = p['environment']
    tc['B6'] = ('Execution: run each workflow in order; a later case runs on data left by an earlier one — '
                'check each precondition first. Round 1: Passed = ran on the app with evidence in Note and hidden AI context; '
                'Failed = ran with Bug in Note; Pending = not run; N/A = blocked by a failed predecessor. '
                'The Note column summarizes execution outcome, build, 5.2 references and evidence.')
    from openpyxl.styles import Alignment
    tc['D5'].alignment = Alignment(wrap_text=True, vertical='center', horizontal='left')
    tc['B6'].alignment = Alignment(wrap_text=True, vertical='center', horizontal='left')
    tc.row_dimensions[5].height = height_for([(tc['D5'].value, 90)])
    tc.row_dimensions[6].height = height_for([(tc['B6'].value, 90)])
    row_style = styles_of(tc, 9, 6)
    clear_from(tc, 9)
    for i, (wf, n) in enumerate(built, 1):
        r = 8 + i
        apply(tc, r, row_style)
        tc[f'B{r}'] = i
        tc[f'C{r}'] = wf['name']
        tc[f'D{r}'] = wf['sheet']
        tc[f'E{r}'] = f"{wf['description']} ({n} cases)"
        tc[f'F{r}'] = wf['precondition']
        for col in 'BCDEF':
            al = copy(tc[f'{col}{r}'].alignment)
            al.vertical = 'top'
            al.wrap_text = True
            tc[f'{col}{r}'].alignment = al
        tc.row_dimensions[r].height = height_for(
            [(tc[f'C{r}'].value, 32), (tc[f'E{r}'].value, 23), (wf['precondition'], 26)], line_height=15)

    data_style = styles_of(ts, 11, 8)
    total_style = styles_of(ts, 12, 8)
    cover_style = styles_of(ts, 14, 8)
    ts['C3'] = p['project']
    ts['C4'] = p['projectCode']
    ts['C5'] = f"{p['projectCode']}_System_Test_v{p['cover']['version']}"
    ts['G5'] = p['cover']['issueDate']
    ts['H10'] = 'Number of test cases'
    ts.column_dimensions['C'].width = max(ts.column_dimensions['C'].width or 24, 34)
    # Keep the template's Notes (C6) as-is for the same reason as above.
    clear_from(ts, 11)
    for i, (wf, _) in enumerate(built, 1):
        r = 10 + i
        apply(ts, r, data_style)
        ts[f'B{r}'] = i
        ts[f'C{r}'] = wf['name']
        for col, cell in zip('DEFGH', ('B6', 'C6', 'D6', 'E6', 'B4')):
            ts[f'{col}{r}'] = f"='{wf['sheet']}'!{cell}"
    last = 10 + len(built)
    t = last + 1
    apply(ts, t, total_style)
    ts[f'C{t}'] = 'Sub total'
    for col in 'DEFGH':
        ts[f'{col}{t}'] = f'=SUM({col}11:{col}{last})'
    from openpyxl.styles import Border as _Border, Side as _Side
    _thin = _Side(style='thin')
    for k, (label, formula) in enumerate((
            ('Test coverage', f'=IFERROR((D{t}+E{t})*100/(H{t}-G{t}),0)'),
            ('Test successful coverage', f'=IFERROR(D{t}*100/(H{t}-G{t}),0)'))):
        r = t + 2 + k
        apply(ts, r, cover_style)
        ts[f'C{r}'] = label
        ts[f'E{r}'] = formula
        ts[f'F{r}'] = '%'
        for col_idx in range(2, 9):
            ts.cell(r, col_idx).border = _Border(
                left=_thin if col_idx == 2 else None,
                right=_thin if col_idx == 8 else None,
                top=_thin if k == 0 else None,
                bottom=_thin if k == 1 else None,
            )

    for r in range(11, t):
        ts.row_dimensions[r].height = max(21, height_for([(ts[f'C{r}'].value, 32)], line_height=15))
    ts.row_dimensions[t].height = 21
    ts.row_dimensions[6].height = max(36, height_for([(ts['C6'].value, 55)], line_height=15))
    notes = copy(ts['C6'].alignment)
    notes.wrap_text = True
    notes.vertical = 'top'
    ts['C6'].alignment = notes
    harmonise(wb)

    try:
        wb.save(target)
    except PermissionError:
        sys.exit(f'Cannot write {target.name}: close it in Excel and build again.')
    return built


def cmd_build(args):
    errors, todo = run_checks()
    if errors:
        for e in errors:
            print('ERROR', e)
        sys.exit(f'{len(errors)} error(s): fix them before building.')
    cmd_md(args)
    p = plan()
    target = REPORTS / p['file']
    if not args.overwrite:
        for wf in p['workflows']:
            path = wf_file(wf)
            n = sum(len(g['cases']) for g in load(path)['groups']) if path.exists() else 0
            print(f"{wf['sheet']:<11} {wf['id']} {n:>3} case(s)  {wf['name']}")
        print(f'Dry run: {target.relative_to(ROOT)} not written. Pass --overwrite to rebuild it.')
        return
    if target.exists():
        backup = target.with_name(f"{target.stem}.bak-{datetime.now():%Y%m%d-%H%M%S}{target.suffix}")
        shutil.copy2(target, backup)
        print('backup', backup.relative_to(ROOT))
    built = build_workbook(p, target)
    print(f'{target.relative_to(ROOT)}: {len(built)} sheets, {sum(n for _, n in built)} cases; '
          f'{len(todo)} open item(s)')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=('scaffold', 'check', 'md', 'build'))
    parser.add_argument('--overwrite', action='store_true', help='build: write the workbook (after a .bak copy)')
    args = parser.parse_args()
    return {'scaffold': cmd_scaffold, 'check': cmd_check, 'md': cmd_md, 'build': cmd_build}[args.command](args) or 0


if __name__ == '__main__':
    sys.exit(main())
