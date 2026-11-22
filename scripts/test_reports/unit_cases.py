"""Unit test report 5.1: case JSON -> checks, Markdown previews, Excel.

Content lives in docs/08-reports/test-reports/cases/, next to the 5.2 / 5.2.2 sources:
  functions.json             the plan: which sheets exist (per feature), which methods each one covers, each method's
                             layer (Domain / Application / Infrastructure) and file:line, the code source (a git ref),
                             and the skip list (public methods deliberately without a sheet, each with a reason)
  FE-xx/unit/<sheet>.json    UTC rows of one sheet -> Report5.1_Unit_Test.xlsx, one sheet per production function
  preview/FE-xx/unit-<sheet>.md  generated Markdown preview of each sheet, for reviewing in a PR

The code under test is read from the git ref in functions.json "source.ref" (default origin/app: the four-project backend,
ADR-0065) with `git show`, never from the working tree, so every command works on a branch that does not contain it.
The resolved commit is written into the workbook cover. Fetch the ref first: git fetch origin app

Usage (from the repository root):
  python scripts/test_reports/unit_cases.py scaffold   # create the missing FE-xx/unit/<sheet>.json with methods and no UTCs
  python scripts/test_reports/unit_cases.py check      # validate; exit 1 on an error; prints coverage per feature and layer
  python scripts/test_reports/unit_cases.py relink     # re-pin file/line (and file, when a class moved) of every method
  python scripts/test_reports/unit_cases.py inventory  # list the public business methods of the code and their status
  python scripts/test_reports/unit_cases.py md         # write cases/preview/FE-xx/unit-*.md
  python scripts/test_reports/unit_cases.py sync-back  # merge executed Round/Test date/Tester cells from the old workbook back into the JSON (by UTC id)
  python scripts/test_reports/unit_cases.py build      # dry-run: check, then print the diff old workbook vs JSON (writes nothing)
  python scripts/test_reports/unit_cases.py build --overwrite  # check + md + workbook from templates/unit-template.xlsx (backs the old file up first)
  python scripts/test_reports/unit_cases.py apply-trx --trx <run.trx> [--round 1] [--dry-run] [--features FE-04 FE-12]

The workbook is output: never edit it in Excel, edit the JSON and build again.
JSON is the source of truth and the template is the frame: build always regenerates from the
template, never patches cells into an old workbook. `build` without flags writes nothing; only
`build --overwrite` writes, after copying the old file to `<name>.bak-<YYYYMMDD-HHMMSS>.xlsx`.
`sync-back` only fills Round/Test date/Tester cells that are non-empty and not Pending;
description/procedure/expected and every other standard field are never overwritten from Excel.
`apply-trx` imports one `dotnet test --logger trx` run into round N (`--round`, default 1):
each UTC is matched by the xUnit Trait `UTC` with value `<Class.Method>/<UTCnnn>`
(exact trait value first, test display name containing the same string as fallback),
and only `roundN`/`dateN`/`testerN` are written. A flaky UTC (Passed and Failed in
one run) or an environment failure (paths, permissions, sockets) is left Pending,
never Failed.
"""
import argparse
import datetime
import json
import math
import re
import shutil
import subprocess
import sys
from collections import OrderedDict
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
LAYERS = ('Domain', 'Application', 'Infrastructure')
FORBIDDEN = re.compile(r'\bAssert\.|TestServer|dotnet test|\bfixture\b|WebApplicationFactory', re.I)
NOTE_FORBIDDEN = re.compile(r'Automated:|tests/|\.cs\b|Web\.Tests', re.I)


def load(path):
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def manifest():
    return load(MANIFEST)


def case_file(entry):
    return UNIT / entry['feature'] / 'unit' / f"{entry['sheet']}.json"


# ---------------------------------------------------------------- code source (a git ref, not the working tree)

class Source:
    """Reads the code under test from a git ref with `git show`; ref '' means the working tree."""

    def __init__(self, ref):
        self.ref = ref
        self.sha = ''
        self._files = None
        self._text = {}
        if ref:
            run = subprocess.run(['git', 'rev-parse', '--verify', '--quiet', ref + '^{commit}'],
                                 cwd=ROOT, capture_output=True, text=True)
            if run.returncode != 0:
                sys.exit(f"source ref {ref!r} is not available in this clone. Fetch it first, e.g. "
                         f"'git fetch origin {ref.split('/')[-1]}', or change source.ref in functions.json.")
            self.sha = run.stdout.strip()

    def files(self):
        """Every backend .cs path (posix, repo-relative) of the code under test, without build output or migrations."""
        if self._files is None:
            if self.ref:
                out = subprocess.run(['git', 'ls-tree', '-r', '--name-only', self.sha, 'backend'],
                                     cwd=ROOT, capture_output=True, check=True).stdout.decode('utf-8')
                names = out.splitlines()
            else:
                names = [p.relative_to(ROOT).as_posix() for p in (ROOT / 'backend').rglob('*.cs')]
            self._files = sorted(n for n in names if n.endswith('.cs')
                                 and not re.search(r'/(obj|bin|Migrations)/', n))
        return self._files

    def read(self, path):
        if path not in self._text:
            if self.ref:
                run = subprocess.run(['git', 'show', f'{self.sha}:{path}'], cwd=ROOT, capture_output=True)
                self._text[path] = run.stdout.decode('utf-8-sig') if run.returncode == 0 else None
            else:
                p = ROOT / path
                self._text[path] = p.read_text(encoding='utf-8-sig') if p.is_file() else None
        return self._text[path]

    def label(self):
        return f"{self.ref} @ {self.sha[:12]}" if self.ref else 'working tree'


_SOURCE = {}


def source(m=None):
    m = m or manifest()
    ref = m.get('source', {}).get('ref', '')
    if ref not in _SOURCE:
        _SOURCE[ref] = Source(ref)
    return _SOURCE[ref]


# ---------------------------------------------------------------- C# scanning

TYPE_DECL = re.compile(r'^(\s*)(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file)\s+)*'
                       r'(?:record\s+struct|record\s+class|class|record|struct|interface)\s+(\w+)')
METHOD_DECL = re.compile(r'^\s*public\s+(?:(?:static|async|override|virtual|sealed|unsafe|new)\s+)*'
                         r'([^=;{]*?)\b(\w+)\s*(?:<[^>(]*>)?\s*\(')
# Files whose public members are plumbing, never business logic: out of the inventory by rule.
RULE_EXCLUDED = re.compile(r'(ServiceCollectionExtensions|DependencyInjection|Options|Unavailable\w+)\.cs$|/(Entities|Enums)/')


def scan_methods(text):
    """Public methods of a C# file: [(class, method, line)]. Class comes from the nearest enclosing type by indentation."""
    types = {}
    out = []
    for number, line in enumerate(text.splitlines(), 1):
        t = TYPE_DECL.match(line)
        if t:
            indent = len(t.group(1))
            for deeper in [i for i in types if i >= indent]:
                del types[deeper]
            types[indent] = t.group(2)
        d = METHOD_DECL.match(line)
        if t or not d or ' operator ' in line:
            continue
        indent = len(line) - len(line.lstrip())
        outer = [i for i in types if i < indent]
        owner = types[max(outer)] if outer else ''
        name = d.group(2)
        if name in ('static', 'async', 'public', 'new', 'override') or name == owner or d.group(1).strip().startswith('new'):
            continue
        out.append((owner, name, number))
    return out


def layer_of(path):
    m = re.match(r'backend/TrustCabinet\.(Domain|Application|Infrastructure)/', path)
    return m.group(1) if m else ''


def inventory(src, m):
    """Public business methods of the Domain and Application layers, plus the Infrastructure files the plan opts in."""
    wanted = set(m.get('inventory', {}).get('infrastructure', []))
    items = []
    for path in src.files():
        layer = layer_of(path)
        if layer == 'Web' or not layer or RULE_EXCLUDED.search(path):
            continue
        if layer == 'Infrastructure' and path not in wanted:
            continue
        text = src.read(path)
        if text is None:
            continue
        for owner, name, line in scan_methods(text):
            items.append({'layer': layer, 'file': path, 'class': owner, 'method': name, 'line': line})
    return items


def find_declaration(text, method):
    """Lines (1-based) where `Class.Method` or `Method` is declared in one C# file; declarations of the class win."""
    cls, _, name = method.rpartition('.')
    hits = [line for owner, n, line in scan_methods(text) if n == name and (not cls or owner == cls.split('.')[-1])]
    if hits:
        return hits
    # not public (a private helper under test) or a type the scanner did not see: fall back to any declaration line
    decl = re.compile(r'^\s*(?:(?:public|internal|private|protected|static|async|override|virtual|sealed|partial|unsafe)\s+)+'
                      r'[^=;]*?\b' + re.escape(name) + r'\s*(?:<[^>(]*>)?\s*\(')
    return [i + 1 for i, ln in enumerate(text.splitlines()) if decl.match(ln)]


def locate(mo, src):
    """Where a method is now: (file, line), or (None, reason). Tries its pinned file, then every backend/**/*.cs declaring its class."""
    method = mo.get('method', '')
    cls = method.rpartition('.')[0].split('.')[-1]
    pinned = mo.get('file', '')
    text = src.read(pinned) if pinned else None
    if text is not None:
        hits = find_declaration(text, method)
        if len(hits) == 1:
            return pinned, hits[0]
        if len(hits) > 1:
            if mo.get('line') in hits:
                return pinned, mo['line']
            return None, f'ambiguous in {pinned}: lines {hits}'
    if not cls:
        return None, 'not found'
    cls_re = re.compile(r'\b(?:class|record|struct)\s+' + re.escape(cls) + r'\b')
    found = []
    for path in src.files():
        text = src.read(path)
        if text and cls_re.search(text):
            found += [(path, h) for h in find_declaration(text, method)]
    if len(found) == 1:
        return found[0]
    return None, ('not found under backend/' if not found else f'ambiguous: {found}')


# ---------------------------------------------------------------- scaffold

def sync_sheet(entry, path):
    """Bring an existing sheet file in line with the plan: add planned methods (no UTCs), keep UTCs of the others in plan order."""
    doc = load(path)
    have = {mo['method']: mo for mo in doc.get('methods', [])}
    methods, dirty = [], False
    for mo in entry['methods']:
        cur = have.pop(mo['method'], None)
        if cur is None:
            cur, dirty = {'method': mo['method'], 'layer': mo.get('layer', ''), 'file': mo.get('file', ''),
                          'line': mo.get('line', 0), 'cases': []}, True
        methods.append(cur)
    leftover = [k for k, v in have.items() if v.get('cases')]
    if leftover:
        print('KEPT (has UTCs, no longer planned)', path.relative_to(ROOT), leftover)
        methods += [have[k] for k in leftover]
    if [mo['method'] for mo in methods] != [mo['method'] for mo in doc.get('methods', [])]:
        dirty = True
    if dirty:
        doc['methods'] = methods
        save_json(path, doc)
        print('updated', path.relative_to(ROOT))


def cmd_scaffold(_):
    m = manifest()
    for entry in m['sheets']:
        path = case_file(entry)
        if path.exists():
            sync_sheet(entry, path)
            continue
        save_json(path, {
            'feature': entry['feature'], 'module': entry['module'], 'sheet': entry['sheet'],
            'description': entry.get('description', ''), 'precondition': entry.get('precondition', ''),
            'methods': [{'method': mo['method'], 'layer': mo.get('layer', ''), 'file': mo.get('file', ''),
                         'line': mo.get('line', 0), 'cases': []} for mo in entry['methods']],
        })
        print('created', path.relative_to(ROOT))


# ---------------------------------------------------------------- check

def test_methods():
    # Only called when at least one method declares directTests (opt-in future gate).
    names = set()
    for f in (ROOT / 'tests').rglob('*.cs'):
        try:
            text = f.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            continue
        names.update(re.findall(r'public\s+(?:async\s+)?[\w<>\[\],\s?]+\s(\w+)\s*\(', text))
    return names


def skipped_by(item, skips):
    """The skip entry that covers an inventory item, or None. An entry names a file, a class, or Class.Method."""
    full = f"{item['class']}.{item['method']}"
    for s in skips:
        if s.get('method') and s['method'] == full and s.get('file', item['file']) == item['file']:
            return s
        if s.get('class') and s['class'] == item['class'] and s.get('file', item['file']) == item['file'] \
                and not s.get('method') and ('methods' not in s or item['method'] in s['methods']):
            return s
        if s.get('file') and not s.get('class') and not s.get('method') and s['file'] == item['file']:
            return s
    return None


def plan_index(m):
    """(file, 'Class.Method') -> (feature, sheet) for every planned method."""
    index = {}
    for entry in m['sheets']:
        for mo in entry['methods']:
            index[(mo.get('file', ''), mo['method'])] = (entry['feature'], entry['sheet'])
    return index


def classify(m, src):
    """Inventory items with status 'planned', 'skipped' or 'unplanned', plus stale skip entries."""
    items = inventory(src, m)
    plan = plan_index(m)
    skips = m.get('skipped', [])
    used = set()
    for it in items:
        key = (it['file'], f"{it['class']}.{it['method']}")
        if key in plan:
            it['status'], it['feature'], it['sheet'] = 'planned', *plan[key]
            continue
        s = skipped_by(it, skips)
        if s is not None:
            it['status'] = 'skipped'
            used.add(id(s))
        else:
            it['status'] = 'unplanned'
    stale = [s for s in skips if id(s) not in used]
    return items, stale


def run_checks():
    m = manifest()
    errors, todo = [], []
    src = source(m)
    try:
        codes = set(re.findall(r'\bMSG\d{2,3}\b', CATALOGUE.read_text(encoding='utf-8')))
    except FileNotFoundError:
        codes = set()
        errors.append(f'catalogue not found: {CATALOGUE}')
    need_tests = any(mo.get('directTests') for s in m['sheets'] for mo in s.get('methods', []))
    methods = test_methods() if need_tests else set()

    if not m.get('source', {}).get('ref'):
        errors.append('functions.json: source.ref is missing (the git ref of the code under test, e.g. origin/app)')

    names = [s['sheet'] for s in m['sheets']]
    for n in names:
        if len(n) > 31 or re.search(r'[\\/?*\[\]:]', n) or names.count(n) > 1:
            errors.append(f'functions.json: bad or duplicate sheet name {n!r}')

    for entry in m['sheets']:
        sid = f"{entry['feature']}/{entry['sheet']}"
        for mo in entry.get('methods', []):
            for t in mo.get('directTests', []):
                if t not in methods:
                    errors.append(f'functions.json {sid}: test method not found: {t}')
        for mo in entry.get('methods', []):
            name = mo.get('method')
            if mo.get('layer') not in LAYERS:
                errors.append(f'functions.json {sid}: {name} layer must be one of {LAYERS}')
            elif layer_of(mo.get('file', '')) != mo['layer']:
                errors.append(f'functions.json {sid}: {name} says layer {mo["layer"]} but {mo.get("file")} is in {layer_of(mo.get("file", "")) or "no layer"}')
            text = src.read(mo['file']) if mo.get('file') else None
            line = mo.get('line', 0)
            if text is None:
                errors.append(f'functions.json {sid}: function {name} file not found in {src.label()}: {mo.get("file")} - run relink')
                continue
            lines = text.splitlines()
            if not (1 <= line <= len(lines)):
                errors.append(f'functions.json {sid}: {name} line {line} out of range in {mo.get("file")}')
            elif name.split('.')[-1] not in lines[line - 1]:
                hits = find_declaration(text, name)
                hint = (f'; declared at line {hits[0]}' if len(hits) == 1 else
                        f'; candidates at lines {hits}' if hits else '; declaration not found in this file')
                errors.append(f'functions.json {sid}: {name} not at {mo.get("file")}:{line} (found: {lines[line - 1].strip()[:80]!r}){hint} - run relink')
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
                if not c.get('preconditions'):
                    errors.append(f'{where} {cid}: empty preconditions')
                for key in ('inputs', 'return', 'exception', 'log'):
                    if key not in c or c[key] is None or (isinstance(c[key], str) and not c[key].strip()):
                        errors.append(f'{where} {cid}: empty {key}')
                if c.get('type') not in TYPES:
                    errors.append(f'{where} {cid}: type must be one of {TYPES}')
                text = json.dumps({k: c.get(k, '') for k in ('preconditions', 'inputs', 'return', 'exception', 'log')}, ensure_ascii=False)
                if FORBIDDEN.search(text):
                    errors.append(f'{where} {cid}: test-code wording in case fields ({FORBIDDEN.search(text).group(0)})')
                if c.get('round1', 'Pending') not in STATUSES:
                    errors.append(f"{where} {cid}: round1 must be one of {STATUSES}")
                bad = NOTE_FORBIDDEN.search(c.get('note', ''))
                if bad:
                    errors.append(f'{where} {cid}: note must not reference tests ({bad.group(0)})')
                for code in sorted(set(re.findall(r'\bMSG\d{2,3}\b', json.dumps(c, ensure_ascii=False))) - codes):
                    errors.append(f'{where} {cid}: {code} is not in the message-code catalogue')

    # Coverage gate: every public business method of the code is planned or skipped with a reason.
    for s in m.get('skipped', []):
        if not str(s.get('reason', '')).strip():
            errors.append(f'functions.json skipped entry without a reason: {json.dumps(s, ensure_ascii=False)[:120]}')
        if not (s.get('file') or s.get('class') or s.get('method')):
            errors.append(f'functions.json skipped entry names no file, class or method: {json.dumps(s, ensure_ascii=False)[:120]}')
    items, stale = classify(m, src)
    for it in items:
        if it['status'] == 'unplanned':
            errors.append(f"coverage: {it['layer']} {it['class']}.{it['method']} ({it['file']}:{it['line']}) is neither planned nor in the skip list")
    for s in stale:
        errors.append(f'functions.json skipped entry matches no public method (stale): {json.dumps(s, ensure_ascii=False)[:140]}')
    return errors, todo


def coverage(m):
    """Per feature and layer: planned methods, methods with UTCs, UTCs, plus public methods skipped (by their sheet-less feature)."""
    rows = OrderedDict()
    for entry in m['sheets']:
        path = case_file(entry)
        doc = load(path) if path.exists() else {'methods': []}
        cases = {mo['method']: len(mo.get('cases', [])) for mo in doc['methods']}
        for mo in entry['methods']:
            r = rows.setdefault((entry['feature'], mo.get('layer', '')), {'planned': 0, 'written': 0, 'utcs': 0, 'sheets': set()})
            r['planned'] += 1
            r['sheets'].add(entry['sheet'])
            n = cases.get(mo['method'], 0)
            r['utcs'] += n
            r['written'] += 1 if n else 0
    return rows


def cmd_check(_):
    errors, todo = run_checks()
    for e in errors:
        print('ERROR', e)
    for t in todo:
        print('TODO', t)
    m = manifest()
    rows = coverage(m)
    print(f"\ncode under test: {source(m).label()}")
    print(f"{'feature':8} {'layer':15} {'methods':>8} {'with UTCs':>10} {'UTCs':>6}")
    for (fe, layer), r in sorted(rows.items()):
        print(f"{fe:8} {layer:15} {r['planned']:8} {r['written']:10} {r['utcs']:6}")
    items, _ = classify(m, source(m))
    skipped = sum(1 for i in items if i['status'] == 'skipped')
    print(f"public methods scanned: {len(items)}; planned {sum(1 for i in items if i['status'] == 'planned')}; skipped {skipped}")
    print(f'{len(errors)} error(s); {len(todo)} method(s) still without UTCs')
    return 1 if errors else 0


def cmd_inventory(_):
    m = manifest()
    items, stale = classify(m, source(m))
    for it in items:
        extra = f" -> {it['feature']}/{it['sheet']}" if it['status'] == 'planned' else ''
        print(f"{it['status']:9} {it['layer'][:3]} {it['class']}.{it['method']} {it['file']}:{it['line']}{extra}")
    for s in stale:
        print('STALE skip', json.dumps(s, ensure_ascii=False)[:140])


# ---------------------------------------------------------------- relink

def cmd_relink(_):
    """Re-pin file/line of every method in functions.json and in each sheet JSON after a refactor moved code."""
    m = manifest()
    src = source(m)
    moved = 0
    pins = {}
    for entry in m['sheets']:
        for mo in entry['methods']:
            file, line = locate(mo, src)
            sid = f"{entry['feature']}/{entry['sheet']} {mo['method']}"
            if file is None:
                print('UNCHANGED', sid, '-', line)
                continue
            pins[(entry['feature'], entry['sheet'], mo['method'])] = (file, line)
            if (file, line) != (mo.get('file'), mo.get('line')):
                print('MOVED', sid, f"{mo.get('file')}:{mo.get('line')} -> {file}:{line}")
                mo['file'], mo['line'] = file, line
                moved += 1
            if layer_of(file) and mo.get('layer') != layer_of(file):
                mo['layer'] = layer_of(file)
                moved += 1
    save_json(MANIFEST, m)
    for entry in m['sheets']:
        path = case_file(entry)
        if not path.exists():
            continue
        doc = load(path)
        dirty = False
        for mo in doc.get('methods', []):
            pin = pins.get((entry['feature'], entry['sheet'], mo['method']))
            if pin and (mo.get('file'), mo.get('line'), mo.get('layer')) != (*pin, layer_of(pin[0])):
                mo['file'], mo['line'], mo['layer'] = *pin, layer_of(pin[0])
                dirty = True
        if dirty:
            save_json(path, doc)
    print(f'{moved} change(s); code under test: {src.label()}')


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
            out += [f"## {mo['method']}", '', f"Layer: {mo.get('layer') or '—'} · `{mo.get('file', '')}:{mo.get('line', 0)}`", '']
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
    tc.title, ts.title = 'MethodList', 'Statistics'

    # Cover
    cover['B2'] = 'UNIT TEST DOCUMENT'
    cover['B4'] = m['project']
    cover['B5'] = m['projectCode']
    cover['F4'] = m['cover']['creator']
    cover['F5'] = m['cover']['issueDate']
    cover['F6'] = m['cover']['version']
    cover['A7'], cover['B7'] = 'Code Under Test', f"{source(m).label()} (four-project backend)"
    cover['A7']._style, cover['B7']._style = copy(cover['A6']._style), copy(cover['B6']._style)
    cover.merge_cells('B7:D7')
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
        layers = sorted({mo.get('layer', '') for mo in entry['methods']})
        header = [('F2', 'Method', ', '.join(mo['method'] for mo in entry['methods'])),
                  ('F3', 'Layer', ' / '.join(layers)),
                  ('F4', 'Code under test', ', '.join(f"{mo['file'].split('/', 2)[-1]}:{mo['line']}" for mo in entry['methods'])
                   + f" @ {source(m).sha[:12]}"),
                  ('F5', 'Created By', m['cover']['creator']), ('F6', 'Executed By', ''),
                  ('F7', 'Test requirement', entry.get('description', ''))]
        for ref, label, value in header:
            row = int(ref[1:])
            ws[ref], ws[f'H{row}'] = label, value
            ws[ref]._style, ws[f'H{row}']._style = copy(ws['A2']._style), copy(ws['B2']._style)
            ws.merge_cells(f'H{row}:O{row}')
            ws[f'H{row}'].alignment = copy(ws['B3'].alignment)
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
    head_style = styles_of(tc, 8, 6)
    for col, label in zip('BCDEFG', ('No', 'Module Name', 'Sheet Name', 'Method Name', 'Description', 'Pre-Condition')):
        tc[f'{col}8'] = label
    tc['G8']._style = copy(head_style[-1])
    tc.column_dimensions['D'].width, tc.column_dimensions['E'].width = 22, 40
    tc.column_dimensions['F'].width, tc.column_dimensions['G'].width = 34, 30.6
    row_style = styles_of(tc, 9, 6)
    clear_from(tc, 9)
    for i, (entry, doc, n) in enumerate(built, 1):
        r = 8 + i
        apply(tc, r, row_style)
        tc[f'B{r}'] = i
        tc[f'G{r}']._style = copy(row_style[-1])
        tc[f'C{r}'] = f"{entry['module']} {entry['feature']}"
        tc[f'D{r}'] = entry['sheet']
        tc[f'E{r}'] = chr(10).join(f"{mo['method']} ({mo.get('layer', '')})" for mo in entry['methods'])
        tc[f'F{r}'] = f"{entry.get('description', '')} ({n} UTCs)"
        tc[f'G{r}'] = entry.get('precondition', '') or doc.get('precondition', '')
        tc.row_dimensions[r].height = height_for([(tc[f'E{r}'].value, 38), (tc[f'F{r}'].value, 32), (tc[f'G{r}'].value, 30)])
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

    # Function coverage by feature and layer (planned methods, those with UTCs, public methods skipped on purpose)
    start = t + 9
    items, _ = classify(m, source(m))
    skipped = {}
    for it in items:
        if it['status'] == 'skipped':
            skipped[it['layer']] = skipped.get(it['layer'], 0) + 1
    ts[f'B{start}'] = 'Function coverage by feature and layer'
    ts[f'B{start}']._style = copy(ts[f'B{t}']._style)
    for col, label in zip('BCDEF', ('Feature', 'Layer', 'Methods', 'With UTCs', 'UTCs')):
        ts[f'{col}{start + 1}'] = label
        ts[f'{col}{start + 1}']._style = copy(ts['B11']._style)
    r = start + 2
    for (fe, layer), c in sorted(coverage(m).items()):
        apply(ts, r, data_style)
        ts[f'A{r}'] = None
        for col, v in zip('BCDEF', (fe, layer, c['planned'], c['written'], c['utcs'])):
            ts[f'{col}{r}'] = v
        r += 1
    for layer in LAYERS:
        if skipped.get(layer):
            apply(ts, r, data_style)
            ts[f'A{r}'] = None
            ts[f'B{r}'], ts[f'C{r}'], ts[f'D{r}'] = 'Skipped (skip list)', layer, skipped[layer]
            r += 1

    target = REPORTS / m['book']['file']
    try:
        wb.save(target)
    except PermissionError:
        sys.exit(f'Cannot write {target.name}: close it in Excel and build again.')
    total = sum(n for _, _, n in built)
    print(f'{target.relative_to(ROOT)}: {len(built)} sheets, {total} UTCs')


# ---------------------------------------------------------------- safe rebuild: sync-back, dry-run, --overwrite

#: Columns of the executed results in a module sheet (1-based): Round 1/2/3, Test date, Tester.
RESULT_COLS = {'round1': 6, 'date1': 7, 'tester1': 8,
               'round2': 9, 'date2': 10, 'tester2': 11,
               'round3': 12, 'date3': 13, 'tester3': 14}
UTC_ID = re.compile(r'^UTC\d+$')
#: Sheets that hold no UTC rows (template leftovers, both current and historical titles).
NON_CASE_SHEETS = {'Cover', 'Test Cases', 'Test Statistics', 'MethodList', 'Statistics', 'Prototype'}
FIRST_CASE_ROW = 12


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
    stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    bak = target.with_name(f'{target.stem}.bak-{stamp}.xlsx')
    shutil.copy2(target, bak)
    print(f'backup -> {bak.relative_to(ROOT)}')
    return bak


def _sheet_ids(ws):
    """Case IDs of a module sheet in row order (column A, `UTCnnn`)."""
    ids = []
    for row in range(FIRST_CASE_ROW, (ws.max_row or 0) + 1):
        v = _cell_text(ws.cell(row, 1).value)
        if UTC_ID.match(v):
            ids.append(v)
    return ids


def cmd_sync_back(_):
    """Merge executed results from the old workbook back into the JSON, by UTC id.

    Only Round 1/2/3, Test date and Tester cells that are non-empty and not
    Pending are merged. Description, procedure, expected and every other
    standard field are never overwritten from Excel.
    """
    m = manifest()
    target = REPORTS / m['book']['file']
    if not target.exists():
        print(f'no existing workbook at {target.relative_to(ROOT)}; nothing to sync-back.')
        return 0
    import openpyxl
    wb = openpyxl.load_workbook(target, data_only=True)
    by_sheet = {e['sheet']: e for e in m['sheets']}
    total_cases, total_cells = 0, 0
    for name in wb.sheetnames:
        if name in NON_CASE_SHEETS:
            continue
        entry = by_sheet.get(name)
        if entry is None:
            print(f'SKIP {name}: not in functions.json')
            continue
        path = case_file(entry)
        if not path.exists():
            print(f'SKIP {name}: {path.relative_to(ROOT)} missing')
            continue
        ws = wb[name]
        wanted = {}
        for row in range(FIRST_CASE_ROW, (ws.max_row or 0) + 1):
            cid = _cell_text(ws.cell(row, 1).value)
            if UTC_ID.match(cid):
                vals = {k: _cell_text(ws.cell(row, c).value) for k, c in RESULT_COLS.items()}
                vals = {k: v for k, v in vals.items() if v and v != 'Pending'}
                if vals:
                    wanted[cid] = vals
        if not wanted:
            print(f'{name}: 0 result(s) merged (no executed cell beyond Pending)')
            continue
        doc = load(path)
        index = {}
        for mo in doc.get('methods', []):
            for c in mo.get('cases', []):
                index[c.get('id')] = c
        n_cases, n_cells = 0, 0
        for cid, vals in wanted.items():
            c = index.get(cid)
            if c is None:
                print(f'  WARN {name} {cid}: in the workbook but not in the JSON (left as is)')
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
        print(f'{name}: {n_cases} case(s), {n_cells} cell(s) merged')
    print(f'sync-back total: {total_cases} case(s), {total_cells} cell(s)')
    return 0


# ---------------------------------------------------------------- apply-trx: import one TRX run into round N

#: Who the imported results are attributed to.
APPLY_TRX_TESTER = 'Phạm Gia Khánh'
#: `<Class.Method>/<UTCnnn>` as carried by the xUnit Trait `UTC` (and, when the
#: runner strips traits, by the test display name containing the same string).
UTC_KEY = re.compile(r'([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)/(UTC\d{3})')
#: Failure text that blames the machine, not the code: missing paths, denied
#: writes, busy files, dead sockets. Such a result never becomes Failed.
ENV_FAILURE = re.compile(
    r'UnauthorizedAccess|DirectoryNotFound|FileNotFound|EndOfStream|IOException|'
    r'Access to the path|Access is denied|denied|being used by another process|'
    r'No such file or directory|PathTooLong|Socket|HttpRequest|connection was refused|'
    r'Connection refused|timed out|Timeout|OutOfMemory', re.IGNORECASE)


def _local(tag):
    return tag.rsplit('}', 1)[-1]


def _read_json_raw(path):
    """(data, newline) of a case JSON, keeping the file's line endings: CRLF stays CRLF."""
    raw = path.read_bytes()
    return json.loads(raw.decode('utf-8-sig')), '\r\n' if b'\r\n' in raw else '\n'


def _write_json_raw(path, data, newline):
    text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    if newline == '\r\n':
        text = text.replace('\n', '\r\n')
    path.write_bytes(text.encode('utf-8'))


def parse_trx(path):
    """One TRX run -> (run date ISO, [result dicts]).

    A result carries its outcome, its failure text, the traits/categories of
    its test definition, and the qualified test name. The UTC key
    `<Class.Method>/<UTCnnn>` matches a trait value first; when the runner
    stripped the traits it matches a test name containing the same string.
    """
    import xml.etree.ElementTree as ET
    root = ET.parse(path).getroot()
    defs = {}
    for ut in root.iter():
        if _local(ut.tag) != 'UnitTest':
            continue
        info = {'traits': [], 'className': '', 'method': ''}
        for el in ut.iter():
            name = _local(el.tag)
            if name == 'TestMethod':
                info['className'] = (el.get('className') or '').split(',')[0]
                info['method'] = (el.get('name') or '').split('(')[0]
            elif name in ('TestCategoryItem', 'Trait', 'Property'):
                for attr in ('TestCategory', 'value', 'Value'):
                    v = el.get(attr)
                    if v:
                        info['traits'].append(v)
        uid = ut.get('id') or ut.get('testId')
        if uid:
            defs[uid] = info
    run_date = ''
    for el in root.iter():
        if _local(el.tag) == 'Times':
            for attr in ('start', 'creation', 'finish'):
                if re.match(r'\d{4}-\d{2}-\d{2}', el.get(attr) or ''):
                    run_date = el.get(attr)[:10]
                    break
            break
    results = []
    for res in root.iter():
        if _local(res.tag) != 'UnitTestResult':
            continue
        info = defs.get(res.get('testId') or '', {})
        message, stack = '', ''
        for el in res.iter():
            name = _local(el.tag)
            if name == 'Message' and not message:
                message = (el.text or '').strip()
            elif name == 'StackTrace' and not stack:
                stack = (el.text or '').strip()
        results.append({'testName': res.get('testName') or '', 'outcome': res.get('outcome') or 'Unknown',
                        'message': message, 'stack': stack, 'traits': info.get('traits', []),
                        'qualified': f"{info.get('className', '')}.{info.get('method', '')}".strip('.')})
    return run_date, results


def _suffix_key(candidate, index):
    """The longest dotted suffix of `<a.b.c>/UTCnnn` that is a UTC key.

    TRX test names are fully qualified (`Ns.Class.Method`), while the index
    holds `Class.Method/UTCnnn`, so `Ns.Class.Method/UTC001` must resolve to
    `Class.Method/UTC001`.
    """
    left, sep, utc = candidate.partition('/')
    if not sep:
        return None
    parts = left.split('.')
    for i in range(len(parts)):
        key = '.'.join(parts[i:]) + '/' + utc
        if key in index:
            return key
    return None


def _match_key(res, index):
    """(key, via) for one TRX result, or (None, ''). A trait value wins over the test name."""
    for trait in res['traits']:
        v = (trait or '').strip()
        if v in index:
            return v, 'trait'
        key = _suffix_key(v, index)
        if key is not None:
            return key, 'trait'
    hay = f"{res['qualified']} {res['testName']}"
    for m in UTC_KEY.finditer(hay):
        key = _suffix_key(m.group(0), index)
        if key is not None:
            return key, 'name'
    return None, ''


def _decide(results):
    """One UTC's TRX results -> (status or None, reason). None leaves the case Pending."""
    passed = [r for r in results if r['outcome'] == 'Passed']
    failed = [r for r in results if r['outcome'] in ('Failed', 'Error', 'Timeout', 'Aborted')]
    if failed and passed:
        return None, 'flaky: Passed and Failed in one run, left Pending'
    if failed:
        text = '\n'.join(((r['message'] or '') + '\n' + (r['stack'] or '')) for r in failed)
        if ENV_FAILURE.search(text):
            return None, 'environment failure (path/permission/socket), left Pending'
        return 'Failed', ''
    if passed and len(passed) == len(results):
        return 'Passed', ''
    kinds = sorted({r['outcome'] for r in results}) or ['none']
    return None, f"no decisive result ({', '.join(kinds)}), left Pending"


def cmd_apply_trx(args):
    """Merge one `dotnet test --logger trx` run into round N of the unit JSON.

    Only `roundN`/`dateN`/`testerN` are written; description, inputs, expected
    and every other field are never touched. A case whose TRX results are
    mixed (flaky across reruns) or fail with an environment signature
    (paths, permissions, sockets) is left Pending, never Failed.
    """
    if not args.trx or not Path(args.trx).is_file():
        sys.exit(f'trx not found: {args.trx or "(missing --trx)"}')
    if args.round not in (1, 2, 3):
        sys.exit(f'--round must be 1, 2 or 3, got {args.round!r}')
    m = manifest()
    known = sorted({e['feature'] for e in m['sheets']})
    wanted = set(args.features or [])
    unknown = sorted(wanted - set(known))
    if unknown:
        sys.exit(f'unknown feature(s): {unknown}; known: {known}')
    rk, dk, tk = f'round{args.round}', f'date{args.round}', f'tester{args.round}'
    docs, index, sheets = {}, {}, {}
    for entry in m['sheets']:
        if wanted and entry['feature'] not in wanted:
            continue
        path = case_file(entry)
        if not path.exists():
            print(f"SKIP {entry['feature']}/{entry['sheet']}: {path.relative_to(ROOT)} missing (run scaffold)")
            continue
        data, newline = _read_json_raw(path)
        docs[path] = [data, newline]
        sheets[path] = f"{entry['feature']}/{entry['sheet']}"
        for mo in data.get('methods', []):
            for c in mo.get('cases', []):
                index.setdefault(f"{mo['method']}/{c.get('id')}", []).append((path, c))
    run_date, results = parse_trx(args.trx)
    date = args.date or run_date or datetime.date.today().isoformat()
    if args.date:
        try:
            datetime.date.fromisoformat(date)
        except ValueError:
            sys.exit(f'--date must be YYYY-MM-DD, got {args.date!r}')
    by_key = {}
    unmatched = 0
    for res in results:
        key, via = _match_key(res, index)
        if key is None:
            unmatched += 1
            continue
        bundle = by_key.setdefault(key, {'via': via, 'results': []})
        bundle['results'].append(res)
        if via == 'trait':
            bundle['via'] = 'trait'
    changed = skipped = 0
    per_feature, touched = {}, set()
    dupes = sorted(k for k, v in index.items() if len(v) > 1)
    for key in sorted(by_key):
        bundle = by_key[key]
        locs = index[key]
        status, reason = _decide(bundle['results'])
        n = len(bundle['results'])
        where = ', '.join(sheets[p] for p in dict.fromkeys(p for p, _ in locs))
        if status is None:
            print(f'SKIP {key} [{where}; via {bundle["via"]}, {n} result(s)]: {reason}')
            skipped += 1
            continue
        if all(c.get(rk) == status and c.get(dk) == date and c.get(tk) == args.tester for _, c in locs):
            print(f'KEEP {key} [{where}]: already {rk}={status} {dk}={date}')
            continue
        if not args.dry_run:
            for _, c in locs:
                c[rk], c[dk], c[tk] = status, date, args.tester
        touched.update(p for p, _ in locs)
        changed += 1
        for path, _ in locs:
            fe = sheets[path].split('/')[0]
            per_feature[fe] = per_feature.get(fe, 0) + 1
        print(f'SET {key} [{where}; via {bundle["via"]}, {n} result(s)] -> {status}')
    missing = sum(1 for k in index if k not in by_key)
    if not args.dry_run:
        for path in sorted(touched):
            _write_json_raw(path, *docs[path])
    scope = sorted(wanted) or ['all']
    print(f'apply-trx round {args.round} (scope={scope}, {dk}={date}, tester={args.tester}): '
          f'{changed} case(s) in {len(touched)} file(s) set {dict(sorted(per_feature.items()))}; '
          f'{skipped} skipped (left Pending); {unmatched} TRX result(s) matched no UTC; '
          f'{missing} UTC(s) in scope have no TRX result'
          + ('; dry-run, nothing written' if args.dry_run else ''))
    for key in dupes:
        if key in by_key:
            print(f'WARN duplicate UTC key in {len(index[key])} sheet(s), all set: {key}')
    return 0


def _json_counts(m):
    """sheet -> (case count, {ids}) from the JSON."""
    out = {}
    for entry in m['sheets']:
        path = case_file(entry)
        ids = []
        if path.exists():
            for mo in load(path).get('methods', []):
                ids += [c.get('id') for c in mo.get('cases', [])]
        out[entry['sheet']] = (len(ids), set(ids))
    return out


def _workbook_counts(target):
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
        ids = _sheet_ids(wb[name])
        sheets[name] = (len(ids), set(ids))
    return cover, sheets


def cmd_dry_run(m=None):
    """Print the diff between the old workbook (if any) and the JSON; writes nothing."""
    m = m or manifest()
    new = _json_counts(m)
    target = REPORTS / m['book']['file']
    new_total = sum(n for n, _ in new.values())
    if not target.exists():
        print(f'no existing workbook at {target.relative_to(ROOT)}: build would create '
              f'{len(new)} sheet(s), {new_total} UTC(s).')
        return 0
    old_cover, old = _workbook_counts(target)
    print(f'old workbook: {target.relative_to(ROOT)} ({len(old)} sheets, '
          f'{sum(n for n, _ in old.values())} UTCs); cover version {old_cover or "?"} '
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
            print(f'  {name}: (new) {new[name][0]} UTCs  *')
    for name in removed:
        print(f'  {name}: ({old[name][0]} UTCs, removed)  *')
    print(f'total: {sum(n for n, _ in old.values())} -> {new_total} UTCs')
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
        print(f'{len(todo)} method(s) still without UTCs')
        return 0
    target = REPORTS / manifest()['book']['file']
    if target.exists():
        _backup(target)
    cmd_md(args)
    build_book(manifest())
    print(f'{len(todo)} method(s) still without UTCs')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', choices=('scaffold', 'check', 'relink', 'inventory', 'md', 'sync-back', 'build',
                                             'apply-trx'))
    parser.add_argument('--overwrite', action='store_true',
                        help='build only: back the old workbook up to <name>.bak-<YYYYMMDD-HHMMSS>.xlsx and write it (default is a dry-run diff)')
    parser.add_argument('--trx', default='',
                        help='apply-trx only: the .trx file of one `dotnet test --logger trx` run')
    parser.add_argument('--round', type=int, default=1,
                        help='apply-trx only: which round to write (1, 2 or 3; default 1)')
    parser.add_argument('--dry-run', action='store_true',
                        help='apply-trx only: print what would change, write nothing')
    parser.add_argument('--features', nargs='*', default=[],
                        help='apply-trx only: only these features, e.g. --features FE-04 FE-12 (default: all)')
    parser.add_argument('--tester', default=APPLY_TRX_TESTER,
                        help='apply-trx only: tester name written to testerN')
    parser.add_argument('--date', default='',
                        help='apply-trx only: run date as YYYY-MM-DD (default: the TRX run date, else today)')
    args = parser.parse_args()
    return {'scaffold': cmd_scaffold, 'check': cmd_check, 'relink': cmd_relink, 'inventory': cmd_inventory,
            'md': cmd_md, 'sync-back': cmd_sync_back, 'build': cmd_build, 'apply-trx': cmd_apply_trx}[args.command](args) or 0


if __name__ == '__main__':
    sys.exit(main())
