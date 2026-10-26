"""Check source traceability, template preservation and execution accounting."""
import argparse,hashlib,json,posixpath,re
from pathlib import Path
from zipfile import ZipFile
from lxml import etree as E
S='http://schemas.openxmlformats.org/spreadsheetml/2006/main'
R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
N={'s':S,'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
ROOT=Path(__file__).resolve().parents[2]

def cells(z,path):
    ss=[''.join(x.itertext()) for x in E.fromstring(z.read('xl/sharedStrings.xml'))] if 'xl/sharedStrings.xml' in z.namelist() else []
    values={};root=E.fromstring(z.read(path))
    for c in root.findall('.//s:sheetData/s:row/s:c',N):
        v=c.find('s:v',N)
        values[c.get('r')]=''.join(c.find('s:is',N).itertext()) if c.get('t')=='inlineStr' else ss[int(v.text)] if c.get('t')=='s' else v.text if v is not None else ''
    return root,values

def verify(directory,templates):
    manifest=json.loads((directory/'manifest.json').read_text(encoding='utf-8'));inventory=json.loads((directory/'inventory.json').read_text(encoding='utf-8'))
    tests=[c for c in inventory if c.get('automation')=='xUnit'];assert len(tests)==manifest['testMethods'];assert len({t['id'] for t in inventory})==len(inventory)
    assert all((ROOT/t['source']).is_file() for t in inventory)
    for name,expected in manifest['templates'].items():assert hashlib.sha256((templates/name).read_bytes()).hexdigest()==expected,'Reference template was modified'
    covered=set();reports={}
    for name,expected in manifest['files'].items():
        path=directory/name;assert hashlib.sha256(path.read_bytes()).hexdigest()==expected,'Manifest hash mismatch: '+name
        with ZipFile(path) as z:
            assert z.testzip() is None
            for part in z.namelist():
                if part.endswith(('.xml','.rels')):E.fromstring(z.read(part))
                if part.endswith('.rels'):
                    base='' if part=='_rels/.rels' else part.rsplit('/_rels/',1)[0]
                    for link in E.fromstring(z.read(part)):
                        if link.get('TargetMode')=='External':continue
                        target=link.get('Target','');target=target.lstrip('/') if target.startswith('/') else posixpath.normpath(posixpath.join(base,target))
                        assert target in z.namelist(),f'Dangling relationship: {name}:{part}->{target}'
            if name.endswith('.xlsx'):
                source='FA25_SEP490-G158_Report5.2.2_Integration Test_API Test.xlsx' if '.2.2_' in name else name
                with ZipFile(templates/source) as sourcezip:
                    for part in ['xl/styles.xml','xl/theme/theme1.xml']:assert z.read(part)==sourcezip.read(part),'Template styling changed: '+name
                wb=E.fromstring(z.read('xl/workbook.xml'));sheets=wb.find('{'+S+'}sheets');seen=set()
                for i,sh in enumerate(sheets,1):
                    assert sh.get('name') not in seen;seen.add(sh.get('name'))
                    root,vals=cells(z,f'xl/worksheets/sheet{i}.xml');cellmap={c.get('r'):c for c in root.findall('.//s:sheetData/s:row/s:c',N)};addresses=list(root.xpath('//s:sheetData/s:row/s:c/@r',namespaces=N));assert len(addresses)==len(set(addresses))
                    covered.update(re.findall(r'TC-[A-F0-9]{12}|UI-[A-Z]+-\d+|SYS-\d+|UAT-\d+','\n'.join(vals.values())))
                    for addr,value in vals.items():
                        assert not re.search(r'\bACCE\b|AI-Powered Coding Competency|TienTM|TrangCTQ',value),f'Prior project content remains: {name}/{sh.get("name")}/{addr}: {value[:100]}'
                        c=cellmap[addr]
                        assert c.get('t')!='e',f'Formula error: {name}/{sh.get("name")}/{addr}={value}'
                    if sh.get('name') in {'Statistics','Test Statistics'}:
                        subtotal=next((int(re.search(r'\d+',a)[0]) for a,v in vals.items() if v=='Sub total'),None)
                        if subtotal:
                            col='I' if 'Unit' in name else 'H';reports[name]=int(float(vals[col+str(subtotal)]))
                if 'Acceptance' in name:assert [x.get('name') for x in sheets]==['Cover','Statistics','Customer Feedback Statistics','Customers Feedbacks']
            else:
                reference=templates/'FA25_SEP490-G158_Report5.0_Test Documentation.docx'
                with ZipFile(reference) as sourcezip:
                    for part in ['word/styles.xml','word/numbering.xml','word/fontTable.xml','word/theme/theme1.xml','word/header1.xml','word/media/image7.png']:assert z.read(part)==sourcezip.read(part),'Word preserve-only part changed: '+part
                    sr=E.fromstring(sourcezip.read('word/document.xml'));fr=E.fromstring(z.read('word/document.xml'))
                    assert len(sr.findall('.//w:tbl',N))==len(fr.findall('.//w:tbl',N))==9
                    assert len(sr.findall('.//w:sdt',N))==len(fr.findall('.//w:sdt',N))
                    assert [E.tostring(x) for x in sr.findall('.//w:sectPr',N)]==[E.tostring(x) for x in fr.findall('.//w:sectPr',N)]
                    assert sr.xpath('count(//w:drawing)',namespaces=N)==fr.xpath('count(//w:drawing)',namespaces=N)
                text=' '.join(fr.xpath('//w:t/text()',namespaces=N))+' '.join(E.fromstring(z.read('word/footer1.xml')).xpath('//w:t/text()',namespaces=N))
                assert not re.search(r'\bACCE\b|TienTM|TrangCTQ|pytest|Django|FA25_SEP490|Code coverage ≥ 70|coverage ≥ 80',text),'Prior project content remains in Word'
    assert {t['id'] for t in inventory}<=covered,'Missing test IDs: '+str({t['id'] for t in inventory}-covered)
    assert reports['Report5.1_Unit_Test.xlsx']==manifest['classification']['UNIT']
    assert reports['Report5.2_Integration_Test.xlsx']==manifest['classification']['INTEGRATION']+manifest['manualCases']['UI']
    assert reports['Report5.2.2_Integration_Test_API_Test.xlsx']==manifest['classification']['API']
    assert reports['Report5.3_System_Test.xlsx']==manifest['manualCases']['SYS']
    print(json.dumps(dict(verifiedFiles=len(manifest['files']),testMethods=len(tests),tracedIDs=len(covered),summaryCounts=reports,referenceTemplatesUnchanged=True,sourceStylesPreserved=True),indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--directory',type=Path,default=ROOT/'docs/08-reports/test-reports');p.add_argument('--templates',type=Path,default=Path.home()/'Downloads');a=p.parse_args();verify(a.directory,a.templates)
