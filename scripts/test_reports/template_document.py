"""Fill the retained Word template through targeted OOXML slot replacements."""
from copy import deepcopy
from collections import Counter
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
import hashlib,json,textwrap
from lxml import etree as E
from PIL import Image,ImageDraw,ImageFont,ImageChops

W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
N={'w':W,'r':R}

def serialize(root):return E.tostring(root,xml_declaration=True,encoding='UTF-8',standalone=True)
def text_set(element,text):
    nodes=element.findall('.//w:t',N)
    if not nodes:
        paragraph=element if element.tag=='{'+W+'}p' else element.find('.//w:p',N)
        if paragraph is None:paragraph=E.SubElement(element,'{'+W+'}p')
        run=E.SubElement(paragraph,'{'+W+'}r');nodes=[E.SubElement(run,'{'+W+'}t')]
    nodes[0].text=text;nodes[0].set('{http://www.w3.org/XML/1998/namespace}space','preserve')
    for node in nodes[1:]:node.text=''
    if element.tag=='{'+W+'}tc':
        for paragraph in element.findall('.//w:p',N):
            if not any(n.text for n in paragraph.findall('.//w:t',N)):
                num=paragraph.find('w:pPr/w:numPr',N)
                if num is not None:num.getparent().remove(num)


def linked_text(paragraph,prefix,label,suffix=''):
    links=paragraph.findall('.//w:hyperlink',N)
    text_set(paragraph,prefix)
    if not links:return
    text_set(links[0],label)
    if suffix:
        run=E.SubElement(paragraph,'{'+W+'}r');node=E.SubElement(run,'{'+W+'}t');node.set('{http://www.w3.org/XML/1998/namespace}space','preserve');node.text=suffix


def refresh_cached_toc(path,toc_json):
    """Refresh only cached TOC page text using Word's read-only rendering evidence."""
    entries=json.loads(Path(toc_json).read_text(encoding='utf-8-sig'));lookup={}
    for entry in entries:
        title,_,page=entry.rpartition('\t')
        if page.strip().isdigit():lookup[' '.join(title.split())]=page.strip()
    with ZipFile(path) as z:parts={n:z.read(n) for n in z.namelist()}
    root=E.fromstring(parts['word/document.xml']);updated=0
    for paragraph in root.findall('.//w:sdt//w:p',N):
        if paragraph.find('.//w:hyperlink',N) is None:continue
        nodes=[n for n in paragraph.findall('.//w:t',N) if n.text]
        if len(nodes)<2 or not nodes[-1].text.strip().isdigit():continue
        title=' '.join(''.join(n.text for n in nodes[:-1]).split())
        if title in lookup:nodes[-1].text=lookup[title];updated+=1
    if lookup and updated!=len(lookup):raise ValueError(f'TOC refresh mismatch: {updated} cached entries / {len(lookup)} Word entries')
    parts['word/document.xml']=serialize(root)
    with ZipFile(path,'w',ZIP_DEFLATED) as z:
        for key,value in parts.items():z.writestr(key,value)
    return updated

def make_document(reference,destination,tests,manual,meta,build):
    reference=Path(reference)
    with ZipFile(reference) as z:parts={n:z.read(n) for n in z.namelist()}
    root=E.fromstring(parts['word/document.xml']);body=root.find('w:body',N);pp=body.findall('w:p',N);tables=root.findall('.//w:tbl',N)
    # Keep the reference package and an exact page/style inventory as the template contract.
    contract=build/'artifact.md'
    sections=root.findall('.//w:sectPr',N)
    inventory={k:dict(size=len(v),sha256=hashlib.sha256(v).hexdigest(),role='editable' if k in {'word/document.xml','word/_rels/document.xml.rels','word/settings.xml','word/footer1.xml','docProps/core.xml','docProps/app.xml'} or k.startswith('word/media/') and k!='word/media/image7.png' else 'preserve-only') for k,v in parts.items()}
    (build/'reference-package.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
    contract.write_text('# TrustCabinet Report 5 template contract\n\nReference: '+str(reference.resolve())+'\nSHA-256: '+hashlib.sha256(reference.read_bytes()).hexdigest()+'\n\nThe reference is retained unchanged. Output: '+str(Path(destination).resolve())+'\n\n## Page system and typography\nPreserve every sectPr, styles.xml, numbering.xml, fontTable.xml, theme, header/footer and drawing anchor byte-for-byte. Source section geometry:\n'+ '\n'.join('```xml\n'+E.tostring(s).decode()+'\n```' for s in sections)+'\n\n## Slots and flow\nKeep cover/logo, Table of Contents, I Record of Changes, II Testing Documentation, scope, feature subsections, nonfunctional criteria, four test levels, constraints, strategy tables, tool/environment/milestone sections, case figures and result figures in their original order. Replace body paragraphs by their zero-based body w:p indices, preserving paragraph/run properties. Replace change-log cells and strategy-table text in place. Clone the source two-column strategy table only to fill the three originally empty tool/environment/milestone sections. Replace nine ACCE screenshots with TrustCabinet figures, keeping image relationships and drawing geometry. Preserve image7.png (university logo). Update external links to the six verified files in the user-selected folder. No prior project identities, feedback or results may remain. Set updateFields for Word refresh.\n\n## Preservation gates\nCompare against reference-package.json; only listed editable parts and new cloned body content may differ. All source styles, table grids, sections, header/footer, logo and relationships except hyperlink targets remain unchanged. Verify all pages through Word PDF rendering.\n',encoding='utf-8')
    contract.write_text(contract.read_text(encoding='utf-8').replace('Clone the source two-column strategy table only to fill the three originally empty tool/environment/milestone sections.','Fill the existing tool/environment/milestone tables inside their content controls; preserve all nine original tables.').replace('header/footer and drawing anchor byte-for-byte','header, footer geometry and drawing anchors; only the project name in footer text is edited').replace('new cloned body content','the explicitly listed content slots'),encoding='utf-8')
    replacements={
        11:'Report 5 – Software Test Documentation',20:'– Hanoi, '+meta['date']+' –',
        29:'This plan documents the current TrustCabinet implementation (SEP490-G116), its test designs and evidence requirements. The generated inventory contains '+str(len(tests))+' xUnit test definitions. No verified execution results have been supplied for this export.',
        33:'Verify Performance Standards: measure response and page-load timings against thresholds approved by the team; no measurements are claimed in this report.',
        38:'Implemented functional features and the workflows listed in the accompanying workbooks',
        43:'Real-time notifications and agenda communication through SignalR',44:'Meeting validation, approval, publication and published-meeting rescheduling',
        52:'Third-party infrastructure operated by Google and the SMTP provider',
        58:'Backend: ASP.NET Core 10 Minimal API with Entity Framework Core and PostgreSQL',
        59:'Frontend: React 19, TypeScript and Vite; relative API paths through the same origin',
        60:'Real-time: ASP.NET Core SignalR; Redis supports refresh-token/session infrastructure',
        61:'External services: configured Google Sign-In and SMTP delivery; use test adapters or a sandbox as specified by each fixture',
        65:'Google Sign-In for pre-provisioned active internal accounts',66:'JWT access tokens and HttpOnly refresh cookies',
        71:'Create, list and update users; activate or deactivate accounts',72:'Update account details through administration APIs',73:'Administrator-initiated password-reset email and single-use reset completion',
        76:'1.2.3 Role and Capability Management',77:'List roles and capability catalogue',78:'Create, rename and delete eligible custom roles',79:'Update role capabilities and persist assignments',80:'Protect system roles and the last active administrator',
        81:'1.2.4 Draft Meeting Management',82:'Create and edit Draft meetings',83:'Validate meeting fields, times and sensitivity',84:'Filter, search and page the caller-visible meeting list',85:'Load details and status history under server-side access checks',86:'Maintain permitted organizer and approver relationships',87:'Reject invalid or unauthorized updates without partial changes',
        88:'1.2.5 Meeting Approval Workflow',89:'Submit a ready Draft for approval',90:'Approve or reject as the assigned approver',91:'Record rejection reasons and append-only status history',92:'Revise and resubmit a Rejected meeting',
        93:'1.2.6 Publication and Rescheduling',94:'Publish an Approved meeting with required active meeting roles',95:'Validate Chair and Secretary readiness before publication',96:'Update permitted schedule and location fields after publication',97:'Notify intended recipients and queue calendar email updates',
        98:'1.2.7 Meeting Access and Recovery',99:'Filter meeting reads by caller relationship and state',100:'Return 404 for an inaccessible individual meeting',101:'Return 403 for a fixed API endpoint when capability is missing',102:'Re-authorize every state transition on the server',103:'Verify organizer-transfer and recovery behavior where backend endpoints exist',104:'Preserve evidence of earlier actions in meeting history',
        105:'1.2.8 Agenda Backend',106:'Create and update agenda items through the implemented API',107:'Order and validate agenda items',108:'Verify concurrency and permitted agenda actions',109:'Check real-time agenda messages according to source tests',110:'Complete agenda UI workflow is Blocked until the missing UI and meeting-start integration exist',
        111:'1.2.9 Persistence and Data Integrity',112:'Validate the EF Core model and provider configuration',113:'Verify repository queries and transaction behavior',114:'Verify migration and seeded-role invariants',115:'Check concurrency and administrator-invariant protection',
        117:'Real-time notification delivery through SignalR',
        121:'1.2.11 Email Delivery',122:'Durable email queue, recipient selection and ICS invitation generation',123:'SMTP sandbox delivery, retry, deduplication and outage recovery',
        124:'1.2.12 Calendar and Navigation',125:'Week, month and list calendar views',126:'Meeting preview and detail navigation',127:'Capability-filtered administration navigation',128:'Protected-route authentication and restricted-route 404 behavior',129:'Notification navigation when access to the target has changed',
        131:'Unimplemented document, voting, recording, transcript, minutes and call workflows; do not infer completed features from entity tables',
        135:'Out-of-scope task tracking, voiceprints, multi-tenancy, organization units and eOffice integration',
        138:'Measure standard API response time; the team must approve the target before a pass/fail decision',139:'AI timing and quality tests are deferred until an implemented AI integration is in scope',140:'Measure page-load time and record browser, network and build conditions',143:'Email-worker retry behavior and queue recovery under controlled SMTP failure',
        151:'File handling is deferred where the corresponding business endpoint is not implemented',
        170:'Input/Time: source-defined method and fixture; record actual duration from the test runner',175:'Token, password, validator and message-code behavior according to existing source tests',176:'No frontend component or hook test suite is inferred from backend xUnit tests',
        179:'Code coverage: measurement pending',183:'Backend: Visual Studio Code (integrated terminal); Report5.1_Unit_Test.xlsx',
        186:'Input/Time: API, repository, database, Redis and source-contract definitions; duration comes from actual execution',
        195:'API integration between frontend and backend, verified by the manual UI cases',
        205:'Critical-path coverage: BA review pending',
        213:'Backend: Visual Studio Code (integrated terminal)',215:'API source cases are in Report5.2.2_Integration_Test_API_Test.xlsx; non-HTTP and UI cases are in Report5.2_Integration_Test.xlsx',
        218:'Input/Time: nine source-reviewed workflows; actual execution duration is recorded with evidence',
        228:'Approved performance targets are measured and met for the delivered scope',
        236:'Input/Time: eight proposed acceptance criteria and representative-user tasks; session timing is recorded when conducted',
        246:'Delivered user stories are reviewed against business requirements; incomplete workflows remain explicitly deferred',
        253:'Demonstrate the delivered workflow to representative administrators, organizers, approvers and participants',
        260:'Unimplemented workflows remain deferred or Blocked until prerequisites exist',
        265:'Google and SMTP integrations use the adapters declared by tests; real-service verification requires a configured sandbox',
        273:'Record differences between staging and production instead of assuming exact equivalence',
        284:'BA reconciles requirement documents during BA Final Review after frontend integration',
        285:'Jira SCRUM is the requirements source; testcase-to-UC mapping remains subject to BA review',
        288:'2.1.1 Component Test',289:'Table 2.1.1 Component Test',290:'2.1.2 API Test',291:'Table 2.1.2 API Test',292:'2.1.3 GUI Test',293:'Table 2.1.3 GUI Test',294:'2.1.4 Regression Test',295:'Table 2.1.4 Regression Test',
        314:'View Unit Test Case at: Report5.1_Unit_Test.xlsx',317:'View Integration Test Case at: Report5.2_Integration_Test.xlsx',
        318:'View API Test Case at: Report5.2.2_Integration_Test_API_Test.xlsx. Execution evidence: no verified TRX imported for this export.',
        321:'View System Test Case at: Report5.3_System_Test.xlsx',322:'System execution evidence: pending manual execution; SYS-09 is Blocked by missing UI/workflow prerequisites.',
        325:'View Acceptance Test Case at: Report5.4_Acceptance_Test.xlsx',326:'Representative-user demo recording: not collected.',327:'Customer feedback: not collected. Record actual responses in Customers Feedbacks after a UAT session.',
        331:'Refer: Report5.1_Unit_Test.xlsx. '+str(meta['classification'].get('UNIT',0))+' definitions; results are NotRun without verified evidence. Code coverage has not been measured.',
        334:'Refer: Report5.2_Integration_Test.xlsx and Report5.2.2_Integration_Test_API_Test.xlsx. '+str(meta['classification'].get('INTEGRATION',0))+' non-HTTP definitions, '+str(meta['classification'].get('API',0))+' API definitions and 20 manual UI scenarios.',
        337:'Refer: Report5.3_System_Test.xlsx. Nine designed workflows: eight NotRun and one Blocked; no completed execution is claimed.',
        341:'Refer: Report5.4_Acceptance_Test.xlsx. Eight proposed acceptance criteria; zero customer responses; stakeholder sign-off is pending.',
    }
    for index,text in replacements.items():text_set(pp[index],text)
    text_set(pp[29],f'This plan documents the current TrustCabinet implementation (SEP490-G116), test designs and evidence requirements. The inventory contains {len(tests)} xUnit definitions. Execution status: '+', '.join(f'{key}={value}' for key,value in meta['status'].items())+'. Verified evidence: '+('available.' if meta['evidenceVerified'] else 'not supplied.'))
    for idx,level,filename in [(331,'UNIT','Report5.1_Unit_Test.xlsx'),(337,'SYS','Report5.3_System_Test.xlsx'),(341,'UAT','Report5.4_Acceptance_Test.xlsx')]:
        cases=[t for t in tests+manual if t['level']==level];counts=Counter(t['status'] for t in cases)
        text_set(pp[idx],'Refer: '+filename+'. '+str(len(cases))+' definitions / scenarios: '+', '.join(f'{k}={v}' for k,v in counts.items())+'. '+('Customer feedback and stakeholder sign-off are pending.' if level=='UAT' else 'Code coverage is not measured by these counts.'))
    # Empty hyperlink labels are replaced by raw URLs when Word refreshes fields.
    # Retain the original linked-filename pattern instead of leaving empty labels.
    for index,prefix,label in [(314,'View Unit Test Case at: ','Report5.1_Unit_Test.xlsx'),(317,'View Integration Test Case at: ','Report5.2_Integration_Test.xlsx'),(318,'View API Test Case at: ','Report5.2.2_Integration_Test_API_Test.xlsx'),(321,'View System Test Case at: ','Report5.3_System_Test.xlsx'),(325,'View Acceptance Test Case at: ','Report5.4_Acceptance_Test.xlsx')]:linked_text(pp[index],prefix,label)
    linked_text(pp[322],'System test files: ','Drive folder','; manual execution evidence is pending. SYS-09 is Blocked by missing workflow prerequisites.')
    for index in (326,327):
        for link in pp[index].findall('.//w:hyperlink',N):link.getparent().remove(link)
    for index,level,filename in [(331,'UNIT','Report5.1_Unit_Test.xlsx'),(337,'SYS','Report5.3_System_Test.xlsx'),(341,'UAT','Report5.4_Acceptance_Test.xlsx')]:
        cases=[c for c in tests+manual if c['level']==level];counts=Counter(c['status'] for c in cases)
        linked_text(pp[index],'Refer: ',filename,'. '+str(len(cases))+' definitions / scenarios: '+', '.join(f'{k}={v}' for k,v in counts.items())+'. '+('Customer feedback and stakeholder sign-off are pending.' if level=='UAT' else ''))
    linked_text(pp[334],'Refer: ','Report5.2_Integration_Test.xlsx',f" and Report5.2.2_Integration_Test_API_Test.xlsx. {meta['classification'].get('INTEGRATION',0)} non-HTTP definitions, {meta['classification'].get('API',0)} API definitions and 20 manual UI scenarios.")
    # Remove the other team's change history; retain all row and cell formatting.
    for row in tables[0].findall('w:tr',N)[1:]:
        for cell in row.findall('w:tc',N):text_set(cell,'')
    for cell,text in zip(tables[0].findall('w:tr',N)[1].findall('w:tc',N),[meta['date'],'A','TC Team','Create the English TrustCabinet test plan, test designs and source inventory using the supplied Report 5 templates. Execution and customer feedback remain pending unless evidence is imported.']):text_set(cell,text)
    for ti,values in {
        1:{1:'Execute source-defined xUnit methods with their fixtures and every data variant. Compare each assertion and record TRX results. This report separates isolated logic from database, Redis, source-contract and HTTP integrations.',2:'All selected definitions execute against the documented build; failures are resolved or explicitly accepted; valid execution evidence is retained.',3:'Method count is not code coverage. Theory variants require complete evidence before a method is marked Passed.'},
        2:{1:'Use existing xUnit TestServer endpoint tests for valid, invalid and unauthorized requests. Verify response status, message code, payload and persisted state according to source assertions. Inspect fake adapters and run real-service checks separately where needed.',3:'Fixed capability-protected API: 403. Inaccessible individual resource: 404. Anonymous protected request: 401.'},
        3:{3:'Execute the 20 source-reviewed UI scenarios in the integration workbook. Backend test success does not establish browser success.'},
        4:{3:'Re-run affected tests after each change and retain separate evidence for each run; do not merge repeated attempts into the case count.'},
    }.items():
        for row,text in values.items():text_set(tables[ti].findall('w:tr',N)[row].findall('w:tc',N)[1],text)
    # These three tables are inside content controls, not direct body children.
    # Fill their existing cells, preserving the controls, row count and column grids.
    tools=[['Test API and integration','Visual Studio Code','Microsoft','Installed version'],['Test unit logic','Visual Studio Code','Microsoft','Installed version'],['Test system and UAT','Chrome / Edge','Google / Microsoft','Record at execution'],['Write test documentation','Template export script','TrustCabinet','Current source'],['Manage testcases and reports','Excel / Word','Microsoft','Installed version'],['Manage bugs','Jira SCRUM','Atlassian','Hosted service']]
    environment=[['Test graphical interface','Chrome / Edge','Google / Microsoft','Record at execution'],['Test backend APIs','Visual Studio Code','Microsoft','Installed version'],['Create and execute unit tests','Visual Studio Code','Microsoft','Installed version'],['Bug management','Jira SCRUM','Atlassian','Hosted service'],['Manage testcases and reports','Excel / Word','Microsoft','Installed version'],['Write test documentation','Template export script','TrustCabinet','Current source']]
    for index,entries in [(6,tools),(7,environment)]:
        for row,values in zip(tables[index].findall('w:tr',N)[1:],entries):
            for cell,value in zip(row.findall('w:tc',N),values):text_set(cell,value)
    for row in tables[8].findall('w:tr',N)[1:]:
        cells=row.findall('w:tc',N)
        if len(cells)==1:
            original=''.join(cells[0].itertext());match=__import__('re').search(r'Sprint (\d+)',original)
            text_set(cells[0],'Sprint '+(match[1] if match else '')+' — schedule pending team agreement')
        else:
            for cell in cells[1:]:text_set(cell,'TBD')
    # Replace source hyperlinks without altering relationship IDs or anchors.
    rel=E.fromstring(parts['word/_rels/document.xml.rels'])
    mapping={'rId10':'unit','rId22':'unit','rId12':'integration','rId24':'integration','rId15':'system','rId26':'system','rId18':'uat','rId28':'uat','rId13':'api'}
    for item in rel:
        if item.get('Type','').endswith('/hyperlink'):item.set('Target',LINKS[mapping[item.get('Id')]] if item.get('Id') in mapping else DRIVE)
    settings=E.fromstring(parts['word/settings.xml']);uf=settings.find('w:updateFields',N)
    if uf is None:uf=E.SubElement(settings,'{'+W+'}updateFields')
    uf.set('{'+W+'}val','true')
    # Preserve all image slots and source bounds. Use output workbook captures, not ACCE evidence.
    # Crop the whitespace of native PDF captures before fitting them to the original
    # drawing slots. This is rendering, not a redesign of the source image geometry.
    for f in (build/'previews').glob('*.png'):
        im=Image.open(f).convert('RGB');difference=ImageChops.difference(im,Image.new('RGB',im.size,'white'));bbox=difference.getbbox()
        if bbox:im.crop(bbox).save(f)
    image_sources={'image1.png':'unit-statistics.png','image2.png':'uat-statistics.png','image3.png':'unit-case.png','image4.png':'integration-case.png','image5.png':'system-statistics.png','image6.png':'uat-lastrow.png','image8.png':'system-case.png','image10.png':'integration-statistics.png'}
    for name,preview in image_sources.items():
        f=build/'previews'/preview
        if not f.is_file():raise FileNotFoundError('Missing report figure; run with --preview: '+str(f))
        parts['word/media/'+name]=f.read_bytes()
    sample=next(t for t in tests if t['level']=='UNIT' and len(t['body'].splitlines())<18)
    code='// '+sample['source']+':'+str(sample['line'])+'\n['+sample['kind']+']\n'+sample.get('declaration','public void '+sample['method']+sample['parameters'])+'\n'+sample['body']
    font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',18)
    lines=[x for line in code.splitlines() for x in textwrap.wrap(line,100,replace_whitespace=False) or ['']]
    image=Image.new('RGB',(1150,max(180,26*len(lines)+30)),(20,20,20));draw=ImageDraw.Draw(image)
    draw.multiline_text((15,15),'\n'.join(lines),font=font,fill=(230,230,230),spacing=5)
    import io
    buf=io.BytesIO();image.save(buf,format='PNG');parts['word/media/image9.png']=buf.getvalue()
    parts['word/document.xml']=serialize(root);parts['word/_rels/document.xml.rels']=serialize(rel);parts['word/settings.xml']=serialize(settings)
    footer=E.fromstring(parts['word/footer1.xml'])
    for item in footer.findall('.//w:t',N):
        if item.text:item.text=item.text.replace('ACCE','TrustCabinet')
    parts['word/footer1.xml']=serialize(footer)
    for key in ('docProps/core.xml','docProps/app.xml'):
        if key in parts:
            x=E.fromstring(parts[key])
            for item in x.iter():
                if E.QName(item).localname in {'creator','lastModifiedBy','Company','Manager'}:item.text='TrustCabinet Team'
            parts[key]=serialize(x)
    with ZipFile(destination,'w',ZIP_DEFLATED) as z:
        for k,v in parts.items():z.writestr(k,v)
    with ZipFile(reference) as z:
        for k,v in parts.items():
            if k in inventory and inventory[k]['role']=='preserve-only' and z.read(k)!=v:raise ValueError('Preserved package part changed: '+k)
