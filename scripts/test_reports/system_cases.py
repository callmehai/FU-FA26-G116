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
import re
import shutil
import sys
from copy import copy
from datetime import date, datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from integration_cases import (  # noqa: E402  shared Excel helpers, same template layout
    ROOT, REPORTS, TEMPLATE, apply, bulleted, clear_from, height_for, load, numbered, save_json, styles_of)
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


def build_workbook(p, target):
    import openpyxl
    from openpyxl.worksheet.datavalidation import DataValidation

    wb = openpyxl.load_workbook(TEMPLATE)
    cover, tc, ts, proto = wb['Cover'], wb['Test Cases'], wb['Test Statistics'], wb['Feature']

    cover['B2'] = p['title']
    cover['B4'] = p['project']
    cover['B5'] = p['projectCode']
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

    group_style = styles_of(proto, 11, 15)
    case_style = styles_of(proto, 12, 15)
    widths = {k: v.width for k, v in proto.column_dimensions.items()}
    widths['O'] = 32
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
                          c.get('round3'), c.get('date3'), c.get('tester3'), None]
                for col, v in enumerate(values, 1):
                    if v and col in (7, 10, 13):
                        v = date.fromisoformat(v)
                    ws.cell(r, col, v)
                ai = ws.cell(r, AI_COL, c['aiNote'])
                ai._style = copy(ws.cell(r, 15)._style)
                ws.row_dimensions[r].height = height_for(
                    [(values[i], widths.get(col, 10)) for i, col in enumerate('ABCDE')] + [(values[14], widths['O'])])
                case_rows.append(r)
                r += 1
        last = max(r - 1, 12)
        ws['B4'] = len(case_rows)
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
    tc['D5'] = p['environment']
    tc['B6'] = p.get('note') or None
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
        tc.row_dimensions[r].height = height_for([(tc[f'E{r}'].value, 28), (wf['precondition'], 30)])

    data_style = styles_of(ts, 11, 8)
    total_style = styles_of(ts, 12, 8)
    cover_style = styles_of(ts, 14, 8)
    ts['C3'] = p['project']
    ts['C4'] = p['projectCode']
    ts['C5'] = f"{p['projectCode']}_System_Test_v{p['cover']['version']}"
    ts['G5'] = p['cover']['issueDate']
    ts['C6'] = p.get('note') or None
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
    for k, (label, formula) in enumerate((
            ('Test coverage', f'=IFERROR((D{t}+E{t})*100/(H{t}-G{t}),0)'),
            ('Test successful coverage', f'=IFERROR(D{t}*100/(H{t}-G{t}),0)'))):
        r = t + 2 + k
        apply(ts, r, cover_style)
        ts[f'C{r}'] = label
        ts[f'E{r}'] = formula
        ts[f'F{r}'] = '%'

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
