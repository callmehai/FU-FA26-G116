"""Protect evidence accounting; these checks require no database or application run."""
import importlib.util,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('reports',ROOT/'scripts/generate_test_reports.py');reports=importlib.util.module_from_spec(spec);spec.loader.exec_module(reports)

def case(expected=1):return dict(fullName='Example.Tests.Check',expectedCount=expected,status='NotRun',resultCount=0,evidence='',actual='')
def result(display='Check',outcome='Passed'):return dict(fullName='Example.Tests.Check',displayName=display,outcome=outcome,executedAt='2026-10-23T10:00:00Z')
class EvidenceTests(unittest.TestCase):
    def test_json_snapshot_can_be_replaced_without_partial_content(self):
        import tempfile,json
        with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as directory:
            self.assertTrue(Path(directory).resolve().is_relative_to((ROOT/'artifacts').resolve()))
            path=Path(directory)/'inventory.json'
            reports.write_json(path,{'status':'NotRun'})
            reports.write_json(path,{'status':'Passed','evidence':'verified.trx'})
            self.assertEqual(json.loads(path.read_text())['evidence'],'verified.trx')
            self.assertFalse(path.with_name('inventory.json.writing').exists())
    def test_empty_word_cell_keeps_a_valid_paragraph_container(self):
        from template_document import text_set,W
        from lxml import etree as E
        cell=E.fromstring(f'<w:tc xmlns:w="{W}"><w:p><w:r/></w:p></w:tc>')
        text_set(cell,'')
        self.assertEqual(len(cell.findall('{'+W+'}r')),0)
        self.assertIsNotNone(cell.find('{'+W+'}p/{'+W+'}r/{'+W+'}t'))
    def test_source_code_is_never_interpreted_as_a_formula(self):
        from template_workbooks import Sheet,S
        from lxml import etree as E
        sheet=Sheet('Case',E.fromstring(f'<worksheet xmlns="{S}"><sheetData/></worksheet>'),[])
        sheet.put('D12','== AgendaItemStatus.InProgress))')
        self.assertIn('D12',sheet.values);self.assertNotIn('D12',sheet.formulas)
        sheet.put('B6','=COUNTIF(F12:F20,"Passed")',0)
        self.assertIn('B6',sheet.formulas)
    def test_no_evidence_does_not_pass(self):
        c=case();reports.attach_results([c],[],True,'run.trx');self.assertEqual(c['status'],'NotRun')
    def test_unverified_evidence_does_not_pass(self):
        c=case();reports.attach_results([c],[result()],False,'run.trx');self.assertEqual(c['status'],'NotRun')
    def test_verified_fact_passes(self):
        c=case();reports.attach_results([c],[result()],True,'run.trx');self.assertEqual(c['status'],'Passed')
    def test_theory_requires_every_variant(self):
        c=case(2);reports.attach_results([c],[result()],True,'run.trx');self.assertEqual(c['status'],'Partial')
        reports.attach_results([c],[result('Check(1)'),result('Check(2)')],True,'run.trx');self.assertEqual(c['status'],'Passed')
    def test_duplicate_variants_are_not_completion(self):
        c=case(2);reports.attach_results([c],[result(),result()],True,'run.trx');self.assertEqual(c['status'],'Partial')
    def test_dynamic_data_is_not_assumed_complete(self):
        c=case(None);reports.attach_results([c],[result()],True,'run.trx');self.assertEqual(c['status'],'Partial')
    def test_failure_is_not_hidden_by_passing_variant(self):
        c=case(2);reports.attach_results([c],[result(),result('other','Failed')],True,'run.trx');self.assertEqual(c['status'],'Failed')
    def test_skip_does_not_pass(self):
        c=case();reports.attach_results([c],[result(outcome='NotExecuted')],True,'run.trx');self.assertEqual(c['status'],'Skipped')
    def test_english_manual_designs_are_not_execution_results(self):
        rows=reports.scenarios();self.assertEqual(sum(r['level']=='SYS' for r in rows),9);self.assertEqual(sum(r['level']=='UAT' for r in rows),8);self.assertGreater(sum(r['level']=='UI' for r in rows),0);self.assertEqual(sum(r['status']=='Blocked' for r in rows),1);self.assertFalse(any(r['status']=='Passed' for r in rows));self.assertEqual(len({r['id'] for r in rows}),len(rows))

if __name__=='__main__':unittest.main()
