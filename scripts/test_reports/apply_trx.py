#!/usr/bin/env python3
"""Write xUnit results from a .trx file into the integration cases (Report 5.2.2).

A case whose `note` cites `Automated: <file> (<TestA>; <TestB>)` gets `round<N>`, `date<N>` and `tester<N>`:

  * every cited test passed          -> Passed
  * any cited test failed            -> Failed, and `| R<N> FAIL: <test>: <first error line>` is appended to `note`
  * a cited test is missing from the run, skipped or not executed -> the case is left untouched

Only api.json is read. Cases marked N/A, and cases with no `Automated:` note, are never touched.
Passed means "the cited tests passed"; whether the tests cover every line of Expected is the Phase 0 review
in round1/plan-api.md, so run this after that review, or filter by --features.

  dotnet test TrustCabinet.sln --logger "trx;LogFileName=round1.trx" --results-directory TestResults
  python scripts/test_reports/apply_trx.py TestResults/round1.trx --dry-run
  python scripts/test_reports/apply_trx.py TestResults/round1.trx --features FE-04 FE-12

Then run `integration_cases.py check` and `build`. The JSON keeps its line endings and formatting.
"""
import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CASES = ROOT / 'docs/08-reports/test-reports/cases'
AUTOMATED = re.compile(r'Automated: [^(]+\(([^)]*)\)')
MARK = '| R{n} FAIL:'


def read_trx(path):
    """method name -> list of (outcome, first error line)."""
    tree = ET.parse(path)
    ns = {'t': tree.getroot().tag.split('}')[0].strip('{')}
    by_id = {}
    for u in tree.iterfind('.//t:UnitTest', ns):
        method = u.find('t:TestMethod', ns)
        if method is None:
            continue
        by_id[u.get('id')] = method.get('name').split('.')[-1].split('(')[0]
    results = {}
    for r in tree.iterfind('.//t:UnitTestResult', ns):
        name = by_id.get(r.get('testId')) or r.get('testName', '').split('(')[0].split('.')[-1]
        msg = r.find('t:Output/t:ErrorInfo/t:Message', ns)
        line = ''
        if msg is not None and msg.text:
            line = next((ln.strip() for ln in msg.text.splitlines() if ln.strip()), '')[:200]
        results.setdefault(name, []).append((r.get('outcome'), line))
    return results


def verdict(names, results):
    """Return ('Passed' | 'Failed' | None, note_suffix)."""
    failed, ok = [], 0
    for name in names:
        runs = results.get(name)
        if not runs:
            return None, ''
        for outcome, line in runs:
            if outcome == 'Passed':
                continue
            if outcome == 'Failed':
                failed.append(f'{name}: {line}' if line else name)
            else:
                return None, ''
        ok += 1
    if failed:
        return 'Failed', '; '.join(failed)
    return ('Passed', '') if ok else (None, '')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('trx')
    ap.add_argument('--round', type=int, default=1, choices=(1, 2, 3))
    ap.add_argument('--tester', default='Phạm Gia Khánh')
    ap.add_argument('--date', default=date.today().isoformat())
    ap.add_argument('--features', nargs='*', help='only these Features, for example FE-04 FE-12')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--cases-dir', type=Path, default=CASES, help='defaults to docs/08-reports/test-reports/cases')
    args = ap.parse_args()

    results = read_trx(args.trx)
    n = args.round
    mark = MARK.format(n=n)
    total = {'Passed': 0, 'Failed': 0, 'untouched': 0}
    for path in sorted(args.cases_dir.glob('FE-*/api.json')):
        if args.features and path.parent.name not in args.features:
            continue
        raw = path.read_bytes().decode('utf-8')
        eol = '\r\n' if '\r\n' in raw else '\n'
        data = json.loads(raw)
        changed = 0
        for group in data['groups']:
            for c in group['cases']:
                note = c.get('note', '')
                found = AUTOMATED.findall(note)
                if not found or c.get(f'round{n}') == 'N/A':
                    continue
                names = [x for chunk in found for x in re.split(r';\s*', chunk) if x]
                status, detail = verdict(names, results)
                if status is None:
                    total['untouched'] += 1
                    continue
                note = re.sub(r'\s*' + re.escape(mark) + r'.*$', '', note, flags=re.S)
                if status == 'Failed':
                    note = f'{note} {mark} {detail}'
                c['note'] = note
                c[f'round{n}'] = status
                c[f'date{n}'] = args.date
                c[f'tester{n}'] = args.tester
                total[status] += 1
                changed += 1
        if changed and not args.dry_run:
            out = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
            path.write_bytes(out.replace('\n', eol).encode('utf-8'))
        if changed:
            print(f'{path.parent.name}: {changed} case(s)' + (' (dry run)' if args.dry_run else ''))
    print(f"Passed {total['Passed']}, Failed {total['Failed']}, left untouched {total['untouched']}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
