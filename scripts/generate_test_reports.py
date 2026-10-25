"""Export TrustCabinet tests into the six supplied English Report 5 templates."""
from __future__ import annotations
import argparse
from collections import Counter
from datetime import datetime,timezone
import csv,hashlib,json,re,shutil,subprocess,sys
from pathlib import Path
from zoneinfo import ZoneInfo
from lxml import etree as E

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts/test_reports'))
from manual_cases import scenarios
from template_workbooks import unit_book,table_book,uat_book
from template_document import make_document,refresh_cached_toc
FILES={'UNIT':'Report5.1_Unit_Test.xlsx','INTEGRATION':'Report5.2_Integration_Test.xlsx','API':'Report5.2.2_Integration_Test_API_Test.xlsx','SYSTEM':'Report5.3_System_Test.xlsx','UAT':'Report5.4_Acceptance_Test.xlsx'}
TEMPLATES={**FILES,'API':'FA25_SEP490-G158_Report5.2.2_Integration Test_API Test.xlsx','DOCX':'FA25_SEP490-G158_Report5.0_Test Documentation.docx'}


def write_json(path,data):
    # Atomic replacement avoids partial inventories and Windows indexer locks on
    # files that a viewer may still be reading during regeneration.
    path=Path(path);temporary=path.with_name(path.name+'.writing')
    temporary.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    temporary.replace(path)
def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def source_digest():
    h=hashlib.sha256()
    for area in ('tests','backend','frontend/src'):
        for p in sorted((ROOT/area).rglob('*')):
            if p.is_file() and p.suffix in {'.cs','.csproj','.ts','.tsx','.json','.css'} and not {'bin','obj','node_modules','wwwroot'}.intersection(p.parts):h.update(p.relative_to(ROOT).as_posix().encode()+b'\0'+p.read_bytes()+b'\0')
    return h.hexdigest()


def classify(t):
    context=t['context']+'\n'+t['body']+'\n'+t['classDeclaration'];name=t['className']
    pg=bool(re.search('PostgreSqlTestDatabase|PostgresTestDatabase|AgendaFixture',context));redis='ConnectionMultiplexer' in context or name.startswith('Redis')
    http=bool(re.search('Endpoint|CapabilityGate|SpaFallback',name) or 'AgendaFixture' in context or re.search(r'Factory\(|CreateClient\(|CreateDefaultClient\(',t['body']))
    if name=='AgendaMigrationTests':http=False
    if http:return 'API','ASP.NET Core TestServer; '+('PostgreSQL and fixture adapters.' if pg else 'factory services may use fakes.')
    if pg or redis:return 'INTEGRATION','Real infrastructure: '+', '.join(x for x,v in [('PostgreSQL',pg),('Redis',redis)] if v)+'.'
    if 'UseInMemoryDatabase' in context:return 'INTEGRATION','EF Core InMemory; does not establish PostgreSQL behavior.'
    if 'DbContext' in context or 'Migration' in name:return 'INTEGRATION','Database model/provider/migration; inspect source fixture for connection requirements.'
    if 'File.Read' in context or 'Directory.' in context:return 'INTEGRATION','Source/filesystem contract checks.'
    return 'UNIT','In-process logic; fakes/stubs as declared by the source fixture.'


def enrich(t):
    level,reason=classify(t);inline=[x for x in t['data'] if x.startswith('InlineData')];dynamic=any(x.startswith(('MemberData','ClassData')) for x in t['data'])
    c={k:v for k,v in t.items() if k not in {'context','classDeclaration'}}
    c.update(level=level,title=t['method'].replace('_',' '),reason=reason,automation='xUnit',status='NotRun',expectedCount=None if dynamic else len(inline) if t['kind']=='Theory' else 1,
        tester='',executedAt='',actual='',evidence='',defect='',resultCount=0,
        precondition=reason+' Fixture: '+t['className']+'. '+('Source Skip: '+t['skip']+'. ' if t['skip'] else '')+('Dynamic data is resolved during execution.' if dynamic else ''),
        steps='1. Initialize the fixture and data defined in source.\n2. Run: dotnet test --filter FullyQualifiedName='+t['fullName']+'\n3. Compare every assertion, including source helpers.',
        expected='\n'.join(t['assertions']) or 'Inspect assertions in the source helper methods.')
    return c


def read_trx(path):
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};root=E.parse(str(path));definitions={}
    for x in root.findall('.//t:UnitTest',ns):
        m=x.find('t:TestMethod',ns)
        if m is not None:definitions[x.get('id')]=m.get('className','').split(',')[0]+'.'+m.get('name','').split('(')[0]
    results=[]
    for x in root.findall('.//t:UnitTestResult',ns):
        results.append(dict(fullName=definitions.get(x.get('testId'),''),displayName=x.get('testName',''),outcome=x.get('outcome','Unknown'),executedAt=x.get('endTime','')))
    return results


def attach_results(tests,results,verified,name):
    for c in tests:
        rr=[r for r in results if r['fullName']==c['fullName']]
        if not rr:continue
        c['resultCount']=len(rr);c['evidence']=name
        if not verified:c['actual']='TRX source fingerprint was not verified; result is reference-only.';continue
        outcomes={r['outcome'] for r in rr}
        c['status']='Failed' if outcomes.intersection({'Failed','Error','Timeout'}) else 'Skipped' if outcomes<={'NotExecuted','Skipped'} else 'Passed' if outcomes=={'Passed'} and c['expectedCount']==len(rr) and len({r['displayName'] for r in rr})==len(rr) else 'Partial'
        c['actual']='; '.join(f'{k}: {v}' for k,v in Counter(r['outcome'] for r in rr).items());c['tester']='xUnit / TRX';c['executedAt']=max(r['executedAt'] for r in rr)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--template-dir',type=Path,default=Path.home()/'Downloads');p.add_argument('--output-dir',type=Path,default=ROOT/'docs/08-reports/test-reports');p.add_argument('--overwrite',action='store_true')
    p.add_argument('--trx',type=Path);p.add_argument('--evidence-manifest',type=Path);p.add_argument('--manual-results',type=Path)
    p.add_argument('--run-tests',action='store_true');p.add_argument('--node',default=shutil.which('node'));p.add_argument('--artifact-module',type=Path,default=Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs')
    p.add_argument('--preview',action='store_true',help='Render selected workbook areas for layout review')
    a=p.parse_args();out=a.output_dir.resolve();build=ROOT/'artifacts/test-report-build/template-edition';build.mkdir(parents=True,exist_ok=True)
    if any((out/f).exists() for f in [*FILES.values(),'Report5.0_Test_Documentation.docx']) and not a.overwrite:p.error('Reports exist; use --overwrite or another --output-dir.')
    if a.trx and a.run_tests:p.error('Choose --trx or --run-tests.')
    for name in TEMPLATES.values():
        if not (a.template_dir/name).is_file():p.error('Missing original template: '+str(a.template_dir/name))
    if not a.node or not a.artifact_module.is_file():p.error('Node and @oai/artifact-tool are required; supply --node and --artifact-module.')
    subprocess.run(['dotnet','run','--project',str(ROOT/'scripts/test_reports/extractor'),'--',str(ROOT),str(build/'raw-inventory.json')],cwd=ROOT,check=True)
    tests=[enrich(t) for t in json.loads((build/'raw-inventory.json').read_text(encoding='utf-8'))];fingerprint=source_digest();exitcode=0
    if a.run_tests:
        run=build/('run-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));run.mkdir()
        exitcode=subprocess.run(['dotnet','test',str(ROOT/'TrustCabinet.sln'),'--logger','trx','--results-directory',str(run)],cwd=ROOT).returncode
        trxs=list(run.glob('*.trx'))
        if len(trxs)!=1 or source_digest()!=fingerprint:raise RuntimeError('Expected one TRX and unchanged source. No report execution results were imported.')
        a.trx=trxs[0];a.evidence_manifest=run/'evidence-manifest.json';write_json(a.evidence_manifest,dict(sourceDigest=fingerprint,trxSha256=digest(a.trx)))
    evidence=json.loads(a.evidence_manifest.read_text(encoding='utf-8')) if a.evidence_manifest else {};verified=bool(a.trx and evidence.get('sourceDigest')==fingerprint and evidence.get('trxSha256')==digest(a.trx));results=read_trx(a.trx) if a.trx else []
    attach_results(tests,results,verified,a.trx.name if a.trx else '')
    manual=scenarios()
    if a.manual_results:
        lookup={c['id']:c for c in manual};seen=set()
        with a.manual_results.open(encoding='utf-8-sig',newline='') as f:
            for r in csv.DictReader(f):
                if r['id'] not in lookup or r['id'] in seen:raise ValueError('Unknown or repeated case: '+r['id'])
                seen.add(r['id'])
                if r['status'] not in {'NotRun','Passed','Failed','Skipped','Partial','Blocked'}:raise ValueError('Invalid status')
                if r['status'] in {'Passed','Failed'} and not all(r.get(k) for k in ['tester','executedAt','actual','evidence']):raise ValueError('Passed/Failed requires tester, date, actual and evidence')
                lookup[r['id']].update({k:r.get(k,'') for k in ['status','tester','executedAt','actual','evidence','defect']})
    for c in tests+manual:
        if not (ROOT/c['source']).is_file():raise ValueError('Missing source: '+c['source'])
    commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    meta=dict(project='TrustCabinet',projectCode='SEP490-G116',date=datetime.now(ZoneInfo('Asia/Ho_Chi_Minh')).strftime('%Y-%m-%d'),generatedAt=datetime.now(timezone.utc).isoformat(),commit=commit,sourceDigest=fingerprint,evidenceVerified=verified,testMethods=len(tests),classification=dict(Counter(c['level'] for c in tests)),status=dict(Counter(c['status'] for c in tests)),manualCases=dict(Counter(c['level'] for c in manual)),executions=len(results),templates={name:digest(a.template_dir/name) for name in TEMPLATES.values()})
    books={}
    books['UNIT']=unit_book(a.template_dir/TEMPLATES['UNIT'],[c for c in tests if c['level']=='UNIT'],meta)
    books['INTEGRATION']=table_book(a.template_dir/TEMPLATES['INTEGRATION'],[c for c in tests if c['level']=='INTEGRATION']+[c for c in manual if c['level']=='UI'],meta,'INTEGRATION TEST DOCUMENT')
    books['API']=table_book(a.template_dir/TEMPLATES['API'],[c for c in tests if c['level']=='API'],meta,'API TEST')
    books['SYSTEM']=table_book(a.template_dir/TEMPLATES['SYSTEM'],[c for c in manual if c['level']=='SYS'],meta,'SYSTEM TEST REPORT DOCUMENT',True)
    books['UAT']=uat_book(a.template_dir/TEMPLATES['UAT'],[c for c in manual if c['level']=='UAT'],meta)
    specs=build/'authoring.json';write_json(specs,[book.spec(FILES[k]) for k,book in books.items()]);authored=build/'authored'
    subprocess.run([a.node,str(ROOT/'scripts/test_reports/author_workbooks.mjs'),str(specs),str(authored),str(a.artifact_module)],cwd=ROOT,check=True)
    out.mkdir(parents=True,exist_ok=True)
    for k,book in books.items():book.save(out/FILES[k],authored/FILES[k])
    subprocess.run([a.node,str(ROOT/'scripts/test_reports/render_workbooks.mjs'),str(out),str(build/'previews'),str(a.artifact_module)],cwd=ROOT,check=True)
    make_document(a.template_dir/TEMPLATES['DOCX'],out/'Report5.0_Test_Documentation.docx',tests,manual,meta,build)
    pdf=build/'final-word.pdf'
    subprocess.run(['C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(ROOT/'scripts/test_reports/render_word.ps1'),'-InputPath',str(out/'Report5.0_Test_Documentation.docx'),'-PdfPath',str(pdf)],cwd=ROOT,check=True)
    meta['tocEntriesRefreshed']=refresh_cached_toc(out/'Report5.0_Test_Documentation.docx',str(pdf)+'.toc.json')
    write_json(out/'inventory.json',tests+manual);write_json(out/'execution-evidence.json',dict(verified=verified,results=results))
    meta['files']={f.name:digest(f) for f in out.iterdir() if f.suffix in {'.xlsx','.docx'}};write_json(out/'manifest.json',meta)
    print(json.dumps(meta,ensure_ascii=False,indent=2));return exitcode

if __name__=='__main__':raise SystemExit(main())
