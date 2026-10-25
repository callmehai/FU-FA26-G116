"""Retain the supplied XLSX package design while replacing project-specific slots.

Artifact Tool authors all cell payloads. This adapter restores source OOXML styles,
Mac/VML extensions, drawing relationships and row patterns that a generic export
cannot preserve byte-for-byte. Original templates are never modified.
"""
from copy import deepcopy
from collections import defaultdict
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import re
import textwrap
import io,posixpath
from PIL import Image,ImageDraw,ImageFont
from lxml import etree as E

S='http://schemas.openxmlformats.org/spreadsheetml/2006/main'
R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
P='http://schemas.openxmlformats.org/package/2006/relationships'
N={'s':S,'r':R}
def tag(x): return '{'+S+'}'+x
def xml(x): return E.tostring(x,xml_declaration=True,encoding='UTF-8',standalone=True)
def col(n):
    text=''
    while n:
        n,k=divmod(n-1,26);text=chr(65+k)+text
    return text
def number(text):
    n=0
    for c in text:n=n*26+ord(c)-64
    return n


class Sheet:
    def __init__(self, name, root, strings):
        self.name=name;self.root=deepcopy(root);self.values={};self.formulas={};self.caches={};self.notes={}
        self.data=self.root.find(tag('sheetData'))
        for c in self.data.iter(tag('c')):
            v=c.find(tag('v'));f=c.find(tag('f'))
            if f is not None and f.text: self.formulas[c.get('r')]='='+f.text
            elif v is not None:
                self.values[c.get('r')]=strings[int(v.text)] if c.get('t')=='s' else (float(v.text) if c.get('t') not in {'str','e'} else v.text)
            elif c.get('t')=='inlineStr':self.values[c.get('r')]=''.join(c.find(tag('is')).itertext())
            for child in list(c):c.remove(child)
            c.attrib.pop('t',None)
        for kind in ('hyperlinks','tableParts','drawing','legacyDrawing','extLst','customSheetViews'):
            # References to another project's comments, images or hyperlinks are data.
            # Cover drawings are reattached explicitly, retaining the source logo.
            for e in self.root.findall(tag(kind)):self.root.remove(e)
        self.root.attrib.pop('{http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac}dyDescent',None)

    def cell(self, addr):
        rownum=int(re.search(r'\d+',addr)[0]);r=self.data.find(f"{tag('row')}[@r='{rownum}']")
        if r is None:
            r=E.Element(tag('row'),r=str(rownum));self.data.append(r)
        c=r.find(f"{tag('c')}[@r='{addr}']")
        if c is None:c=E.SubElement(r,tag('c'),r=addr)
        return c

    def put(self,addr,value,cache=None):
        self.values.pop(addr,None);self.formulas.pop(addr,None)
        # Source assertions can begin with == or => after wrapping. Only explicit
        # calculation writes supply a cached value; source text is always literal.
        if isinstance(value,str) and value.startswith('=') and cache is not None:
            self.formulas[addr]=value
            if cache is not None:self.caches[addr]=cache
        else:self.values[addr]=value
        self.cell(addr)

    def clear(self,first,last=1000000):
        for mapping in (self.values,self.formulas,self.caches):
            for a in list(mapping):
                if first<=int(re.search(r'\d+',a)[0])<=last:del mapping[a]

    def keep(self,last):
        for row in list(self.data):
            if int(row.get('r'))>last:self.data.remove(row)
        merges=self.root.find(tag('mergeCells'))
        if merges is not None:
            for m in list(merges):
                if max(map(int,re.findall(r'\d+',m.get('ref'))))>last:merges.remove(m)
            merges.set('count',str(len(merges)))
        self.clear(last+1)
        for e in self.root.findall(tag('conditionalFormatting')):
            self.root.remove(e)
        for e in self.root.findall(tag('dataValidations')):self.root.remove(e)

    def row(self,source,target,height=None):
        old=self.data.find(f"{tag('row')}[@r='{source}']") if isinstance(source,int) else source
        new=deepcopy(old) if old is not None else E.Element(tag('row'))
        existing=self.data.find(f"{tag('row')}[@r='{target}']")
        if existing is not None:self.data.remove(existing)
        new.set('r',str(target))
        if height is not None:new.set('ht',str(min(409,height)));new.set('customHeight','1')
        for c in new:
            c.set('r',re.sub(r'\d+',str(target),c.get('r')))
            for child in list(c):c.remove(child)
        self.data.append(new)

    def height(self,row,height):
        r=self.cell('A'+str(row)).getparent();r.set('ht',str(min(409,height)));r.set('customHeight','1')

    def merge(self,ref):
        ms=self.root.find(tag('mergeCells'))
        if ms is None:
            ms=E.Element(tag('mergeCells'));self.root.insert(list(self.root).index(self.data)+1,ms)
        if ref not in [e.get('ref') for e in ms]:E.SubElement(ms,tag('mergeCell'),ref=ref)
        ms.set('count',str(len(ms)))

    def link(self,addr,sheet):
        links=self.root.find(tag('hyperlinks'))
        if links is None:
            links=E.Element(tag('hyperlinks'));anchor=self.root.find(tag('mergeCells'))
            self.root.insert(list(self.root).index(anchor if anchor is not None else self.data)+1,links)
        E.SubElement(links,tag('hyperlink'),ref=addr,location="'"+sheet.replace("'","''")+"'!A1")

    def finish(self,authored):
        for c in self.data.iter(tag('c')):
            for child in list(c):c.remove(child)
            c.attrib.pop('t',None)
        payload=E.fromstring(authored)
        for ac in payload.findall('.//s:sheetData/s:row/s:c',N):
            addr=ac.get('r');c=self.cell(addr)
            if ac.get('t'):c.set('t',ac.get('t'))
            for child in ac:c.append(deepcopy(child))
        for addr,value in self.caches.items():
            c=self.cell(addr)
            for v in c.findall(tag('v')):c.remove(v)
            if isinstance(value,str):c.set('t','str')
            E.SubElement(c,tag('v')).text=str(value)
        for row in self.data:
            row[:]=sorted(row,key=lambda c:number(re.match('[A-Z]+',c.get('r'))[0]))
        self.data[:]=sorted(self.data,key=lambda r:int(r.get('r')))
        view=self.root.find('s:sheetViews/s:sheetView',N)
        if view is not None:
            view.attrib.pop('tabSelected',None)
            view.set('topLeftCell','A1')
            for selection in view.findall(tag('selection')):
                selection.set('activeCell','A1');selection.set('sqref','A1')
        return xml(self.root)


class Template:
    def __init__(self,path):
        self.path=Path(path)
        with ZipFile(path) as z:self.parts={n:z.read(n) for n in z.namelist()}
        ss=E.fromstring(self.parts['xl/sharedStrings.xml']) if 'xl/sharedStrings.xml' in self.parts else []
        self.strings=[''.join(s.itertext()) for s in ss]
        self.workbook=E.fromstring(self.parts['xl/workbook.xml'])
        rels=E.fromstring(self.parts['xl/_rels/workbook.xml.rels'])
        targets={r.get('Id'):r.get('Target').lstrip('/') for r in rels}
        self.original={}
        # Only these source patterns are reused. Parsing every formatted, empty row
        # in all 112 legacy unit sheets would waste several gigabytes of memory.
        if 'Unit' in self.path.name:
            limits={'Guideline':60,'Cover':20,'MethodList':9,'Statistics':130,'login':23}
        elif 'Acceptance' in self.path.name:
            limits={'Cover':20,'Statistics':22,'Customer Feedback Statistics':28,'Customers Feedbacks':6}
        else:
            limits={'Cover':20,'Test Cases':9,'Test Statistics':40,'WorkFlow1' if 'System' in self.path.name else 'Authentication':12}
        for s in self.workbook.find(tag('sheets')):
            if s.get('name') not in limits:continue
            path=targets[s.get('{'+R+'}id')]
            if not path.startswith('xl/'):path='xl/'+path
            root=E.fromstring(self.parts[path]);data=root.find(tag('sheetData'))
            for row in list(data):
                if int(row.get('r'))>limits[s.get('name')]:data.remove(row)
            self.original[s.get('name')]=(path,root)
        self.sheets=[]

    def add(self,source,name=None):
        s=Sheet(name or source,self.original[source][1],self.strings);self.sheets.append(s);return s

    def spec(self,filename):
        return dict(filename=filename,sheets=[dict(name=s.name,values=s.values,formulas=s.formulas) for s in self.sheets])

    def save(self,path,authored_path,images=None):
        parts={k:v for k,v in self.parts.items() if not k.startswith(('xl/worksheets/','xl/comments','xl/threadedComments','xl/persons','xl/drawings/','xl/media/','xl/externalLinks/')) and k not in {'xl/sharedStrings.xml','xl/calcChain.xml'}}
        wb=deepcopy(self.workbook);sh=wb.find(tag('sheets'));sh.clear()
        for e in wb.findall(tag('definedNames')):wb.remove(e)
        for e in wb.findall(tag('customWorkbookViews')):wb.remove(e)
        # Google Sheets round-trip extensions reference private source package IDs.
        # They are not part of the report design and cannot survive a new inventory.
        for e in wb.findall(tag('extLst')):wb.remove(e)
        calc=wb.find(tag('calcPr'))
        if calc is not None:calc.set('fullCalcOnLoad','1');calc.set('forceFullCalc','1')
        views=wb.find(tag('bookViews'))
        if views is not None:
            for v in views:v.set('activeTab','0');v.set('firstSheet','0')
        relroot=E.Element('{'+P+'}Relationships',nsmap={None:P})
        E.SubElement(relroot,'{'+P+'}Relationship',Id='styles',Type=R+'/styles',Target='styles.xml')
        E.SubElement(relroot,'{'+P+'}Relationship',Id='theme',Type=R+'/theme',Target='theme/theme1.xml')
        with ZipFile(authored_path) as z:
            rawstrings=[]
            if 'xl/sharedStrings.xml' in z.namelist():rawstrings=[''.join(x.itertext()) for x in E.fromstring(z.read('xl/sharedStrings.xml'))]
            for i,s in enumerate(self.sheets,1):
                E.SubElement(sh,tag('sheet'),name=s.name,sheetId=str(i),attrib={'{'+R+'}id':f'sheet{i}'})
                E.SubElement(relroot,'{'+P+'}Relationship',Id=f'sheet{i}',Type=R+'/worksheet',Target=f'worksheets/sheet{i}.xml')
                raw=E.fromstring(z.read(f'xl/worksheets/sheet{i}.xml'))
                for c in raw.findall('.//s:c',N):
                    if c.get('t')=='s':
                        v=c.find(tag('v'));value=rawstrings[int(v.text)];c.remove(v);c.set('t','inlineStr');E.SubElement(E.SubElement(c,tag('is')),tag('t')).text=value
                parts[f'xl/worksheets/sheet{i}.xml']=s.finish(xml(raw))
                if s.notes:
                    comment_path=f'xl/comments{i}.xml';vml_path=f'xl/drawings/notes{i}.vml'
                    comments=E.Element(tag('comments'),nsmap={None:S});authors=E.SubElement(comments,tag('authors'));E.SubElement(authors,tag('author')).text='TrustCabinet Team';comment_list=E.SubElement(comments,tag('commentList'))
                    vml=E.Element('xml',nsmap={'v':'urn:schemas-microsoft-com:vml','o':'urn:schemas-microsoft-com:office:office','x':'urn:schemas-microsoft-com:office:excel'})
                    shape_type=E.SubElement(vml,'{urn:schemas-microsoft-com:vml}shapetype',id='_x0000_t202',coordsize='21600,21600',attrib={'{urn:schemas-microsoft-com:office:office}spt':'202','path':'m,l,21600r21600,l21600,xe'})
                    E.SubElement(shape_type,'{urn:schemas-microsoft-com:vml}stroke',joinstyle='miter');E.SubElement(shape_type,'{urn:schemas-microsoft-com:vml}path',attrib={'{urn:schemas-microsoft-com:office:office}connecttype':'rect'},gradientshapeok='t')
                    for j,(addr,note) in enumerate(s.notes.items(),1025):
                        c=E.SubElement(comment_list,tag('comment'),ref=addr,authorId='0');E.SubElement(E.SubElement(c,tag('text')),tag('t')).text=note
                        shape=E.SubElement(vml,'{urn:schemas-microsoft-com:vml}shape',id=f'_x0000_s{j}',type='#_x0000_t202',style='position:absolute;width:420pt;height:260pt;visibility:hidden',fillcolor='#ffffe1',attrib={'{urn:schemas-microsoft-com:office:office}insetmode':'auto'})
                        E.SubElement(shape,'{urn:schemas-microsoft-com:vml}fill',color2='#ffffe1');E.SubElement(shape,'{urn:schemas-microsoft-com:vml}shadow',on='t',color='black',obscured='t');E.SubElement(E.SubElement(shape,'{urn:schemas-microsoft-com:vml}textbox',style='mso-direction-alt:auto'),'div',style='text-align:left')
                        client=E.SubElement(shape,'{urn:schemas-microsoft-com:office:excel}ClientData',ObjectType='Note');E.SubElement(client,'{urn:schemas-microsoft-com:office:excel}MoveWithCells');E.SubElement(client,'{urn:schemas-microsoft-com:office:excel}SizeWithCells');E.SubElement(client,'{urn:schemas-microsoft-com:office:excel}AutoFill').text='False';E.SubElement(client,'{urn:schemas-microsoft-com:office:excel}Row').text=str(int(re.search(r'\d+',addr)[0])-1);E.SubElement(client,'{urn:schemas-microsoft-com:office:excel}Column').text=str(number(re.match('[A-Z]+',addr)[0])-1)
                    parts[comment_path]=xml(comments);parts[vml_path]=xml(vml)
                    rp=E.Element('{'+P+'}Relationships',nsmap={None:P});E.SubElement(rp,'{'+P+'}Relationship',Id='notes',Type=R+'/comments',Target=f'../comments{i}.xml');E.SubElement(rp,'{'+P+'}Relationship',Id='noteShapes',Type=R+'/vmlDrawing',Target=f'../drawings/notes{i}.vml');parts[f'xl/worksheets/_rels/sheet{i}.xml.rels']=xml(rp)
                    sheet_root=E.fromstring(parts[f'xl/worksheets/sheet{i}.xml']);E.SubElement(sheet_root,tag('legacyDrawing'),attrib={'{'+R+'}id':'noteShapes'});parts[f'xl/worksheets/sheet{i}.xml']=xml(sheet_root)
                if s.name=='Cover' or s.name=='Statistics' and 'login' in self.original or s.name=='Customer Feedback Statistics':
                    op,root=self.original[s.name];drawing=root.find(tag('drawing'))
                    rp=op.rsplit('/',1)[0]+'/_rels/'+op.rsplit('/',1)[1]+'.rels'
                    if drawing is not None and rp in self.parts:
                        final=E.fromstring(parts[f'xl/worksheets/sheet{i}.xml']);final.append(deepcopy(drawing));parts[f'xl/worksheets/sheet{i}.xml']=xml(final)
                        links=E.fromstring(self.parts[rp])
                        for link in list(links):
                            if not link.get('Type','').endswith('/drawing'):links.remove(link)
                        parts[f'xl/worksheets/_rels/sheet{i}.xml.rels']=xml(links)
                        for key,val in self.parts.items():
                            if key.startswith(('xl/drawings/','xl/media/')):parts[key]=val
        if 'login' in self.original:
            total=len(self.sheets)-4+12
            chartns={'c':'http://schemas.openxmlformats.org/drawingml/2006/chart','a':'http://schemas.openxmlformats.org/drawingml/2006/main','xdr':'http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing'}
            drawing=E.fromstring(parts['xl/drawings/drawing4.xml'])
            for node in drawing.findall('.//xdr:row',chartns):node.text=str(total+7)
            for node in drawing.findall('.//xdr:cNvPr',chartns):node.set('title','Chart')
            parts['xl/drawings/drawing4.xml']=xml(drawing)
            for chart in ('xl/charts/chart1.xml','xl/charts/chart2.xml'):
                tree=E.fromstring(parts[chart])
                for node in tree.findall('.//c:f',chartns):node.text=node.text.replace('$123','$'+str(total))
                if chart.endswith('chart2.xml'):
                    tree.find('.//c:cat/c:strRef/c:f',chartns).text='Statistics!$C$11:$E$11'
                    tree.find('.//c:title//a:t',chartns).text='Test Result'
                else:
                    tree.find('.//c:title//a:t',chartns).text='Test Type (pending review)'
                    for node in tree.findall('.//c:showVal',chartns):node.set('val','0')
                parts[chart]=xml(tree)
        if 'Customers Feedbacks' in self.original:
            font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',42)
            bold=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',52)
            panels=[['Business role','Meeting preparation','Approval workflow'],['Publication and schedule changes','Meeting confidentiality','Password recovery'],['Ease of use','Suggested improvements','Overall satisfaction']]
            for index,questions in enumerate(panels,2):
                image=Image.new('RGB',(1932,2500),'white');draw=ImageDraw.Draw(image)
                draw.rectangle((0,0,1932,180),fill=(102,61,147));draw.text((80,60),'TrustCabinet Customer Feedback',font=bold,fill='white')
                draw.text((80,230),'Responses: 0',font=bold,fill=(45,45,45))
                for q,question in enumerate(questions):
                    y=420+q*650;draw.text((80,y),question,font=bold,fill=(40,40,40));draw.line((80,y+100,1850,y+100),fill=(215,215,215),width=3)
                    draw.text((80,y+180),'No responses collected',font=font,fill=(100,100,100))
                    draw.text((80,y+260),'Results will appear after a recorded UAT session.',font=font,fill=(100,100,100))
                buffer=io.BytesIO();image.save(buffer,format='JPEG',quality=90);parts[f'xl/media/image{index}.jpg']=buffer.getvalue()
        # UAT feedback screenshots are replaced in-place; they never carry ACCE results.
        if images:
            for key,value in images.items():parts[key]=value
        parts['xl/workbook.xml']=xml(wb);parts['xl/_rels/workbook.xml.rels']=xml(relroot)
        ct=E.fromstring(self.parts['[Content_Types].xml'])
        for e in list(ct):
            if e.tag.endswith('Override') and (e.get('PartName','').startswith(('/xl/worksheets/','/xl/comments','/xl/threadedComments','/xl/persons','/xl/externalLinks/')) or e.get('PartName') in {'/xl/sharedStrings.xml','/xl/calcChain.xml'}):ct.remove(e)
        for i in range(1,len(self.sheets)+1):E.SubElement(ct,'{http://schemas.openxmlformats.org/package/2006/content-types}Override',PartName=f'/xl/worksheets/sheet{i}.xml',ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml')
        for i,s in enumerate(self.sheets,1):
            if s.notes:E.SubElement(ct,'{http://schemas.openxmlformats.org/package/2006/content-types}Override',PartName=f'/xl/comments{i}.xml',ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml')
        # Keep only drawing parts referenced by retained cover or feedback sheet.
        for e in list(ct):
            if e.tag.endswith('Override') and e.get('PartName','').lstrip('/') not in parts:ct.remove(e)
        parts['[Content_Types].xml']=xml(ct)
        for key in ('docProps/core.xml','docProps/app.xml'):
            if key in parts:
                root=E.fromstring(parts[key])
                for e in root.iter():
                    if E.QName(e).localname in {'creator','lastModifiedBy','Company','Manager'}:e.text='TrustCabinet SEP490-G116'
                if key=='docProps/app.xml':
                    for e in root.iter():
                        if E.QName(e).localname=='TitlesOfParts':
                            e.clear();vector=E.SubElement(e,'{http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes}vector',size=str(len(self.sheets)),baseType='lpstr')
                            for s in self.sheets:E.SubElement(vector,'{http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes}lpstr').text=s.name
                parts[key]=xml(root)
        # Prune unreachable package content, including the other team's feedback images.
        reachable={'[Content_Types].xml'};queue=['']
        while queue:
            part=queue.pop();folder=posixpath.dirname(part);base=posixpath.basename(part)
            relname=(folder+'/_rels/'+base+'.rels').lstrip('/') if part else '_rels/.rels'
            if relname not in parts:continue
            reachable.add(relname)
            for rel in E.fromstring(parts[relname]):
                if rel.get('TargetMode')=='External':continue
                target=rel.get('Target','');target=target.lstrip('/') if target.startswith('/') else posixpath.normpath(posixpath.join(folder,target))
                if target in parts and target not in reachable:reachable.add(target);queue.append(target)
        parts={k:v for k,v in parts.items() if k in reachable}
        ct=E.fromstring(parts['[Content_Types].xml'])
        for e in list(ct):
            if e.tag.endswith('Override') and e.get('PartName','').lstrip('/') not in parts:ct.remove(e)
        parts['[Content_Types].xml']=xml(ct)
        with ZipFile(path,'w',ZIP_DEFLATED) as z:
            for k,v in parts.items():z.writestr(k,v)


def cover(t,title,date):
    s=t.add('Cover');s.keep(20);s.clear(11)
    for a,v in {'B2':title,'B4':'TrustCabinet','B5':'SEP490-G116','F4':'TrustCabinet Team','F5':date,'F6':'1.0','A11':date,'B11':'1.0','D11':'A','E11':'Generate English test documentation from TrustCabinet source using the supplied template.'}.items():s.put(a,v)
    s.put('B6','=B5&"_"&B2&"_"&F6',f'SEP490-G116_{title}_1')
    return s


def pieces(value,width=100,size=5):
    lines=[x for line in str(value).splitlines() for x in (textwrap.wrap(line,width,replace_whitespace=False) or [''])]
    return ['\n'.join(lines[i:i+size]) for i in range(0,len(lines),size)] or ['']


def unit_book(path,cases,meta):
    t=Template(path);g=t.add('Guideline');g.keep(57)
    g.put('A4',' - Each method sheet documents one extracted xUnit test definition. All InlineData variants belong to that definition; execution count is tracked separately in inventory.json.')
    g.put('A6',' - MethodList maps every definition to its source and matrix. No KLOC or code-coverage claim is inferred from the method count.')
    g.put('A8',' - Statistics counts test definitions. Test coverage means execution rate here, not measured code coverage. N/A/B classifications remain blank until reviewed by the tester.')
    g.put('A39',' - P = Passed, F = Failed. NotRun means no verified execution evidence. Partial, Skipped and Blocked remain untested for the summary. A Theory passes only with every expected variant verified.')
    cover(t,'UNIT TEST DOCUMENT',meta['date'])
    li=t.add('MethodList');li.keep(9);li.clear(9);li.put('C6','.NET 10; xUnit; source fixtures; dedicated PostgreSQL/Redis when required.');li.height(6,42)
    st=t.add('Statistics');total_pattern=deepcopy(st.data.find(f"{tag('row')}[@r='123']"));rate_patterns={r:deepcopy(st.data.find(f"{tag('row')}[@r='{r}']")) for r in range(125,130)}
    st.keep(12);st.clear(12);st.put('F4','TrustCabinet Team');st.put('F5','Pending review');st.put('F6',meta['date']);st.put('B7','One row per test definition; P/F require verified TRX. N/A/B pending review; code coverage has not been measured.');st.height(7,45)
    names=[]
    for i,c in enumerate(cases,1):
        name=f'U{i:03}-'+c['method'][:26];names.append(name)
        s=t.add('login',name);s.keep(23);s.clear(7)
        s.put('C1',c['module']);s.put('K1',c['method']);s.put('C2','TrustCabinet Team');s.put('K2',c['tester']);s.put('C3',c['id']+' | '+c['title']+'\n'+c['source']+':'+str(c['line']));s.height(3,60);s.height(1,48)
        s.put('E7','UTCID01');s.put('A8','Condition');s.put('B8','Precondition')
        s.put('B9',c['precondition']);s.put('E9','O');s.height(9,110)
        s.put('B10','Fixture and helpers: see the referenced source.');s.put('E10','O');s.height(10,42)
        s.put('B11','Input data');s.put('B12',c['parameters'] or 'No method parameters');s.put('E12','O');s.height(12,45)
        inputs='\n'.join(c['data']) or 'Fact: setup data is declared in the test body.'
        s.put('B13',inputs);s.put('E13','O');s.height(13,min(390,30+17*len(textwrap.wrap(inputs,34))))
        s.put('B14','Procedure: execute this xUnit definition and all its data variants.');s.put('E14','O');s.height(14,65)
        s.put('A15','Confirm');s.put('B15','Return / assertions')
        checks=[]
        for a in c['assertions']:checks.extend(pieces(a,34,8))
        if not checks:checks=['Assertions are in source helper methods; inspect the referenced test and fixture.']
        end=16+len(checks)
        # Clone the template confirmation row and result block without introducing a new style.
        rows={r:deepcopy(s.data.find(f"{tag('row')}[@r='{r}']")) for r in (16,20,21,22,23)}
        for row in list(s.data):
            if int(row.get('r'))>=16:s.data.remove(row)
        ms=s.root.find(tag('mergeCells'))
        if ms is not None:
            for m in list(ms):
                if min(map(int,re.findall(r'\d+',m.get('ref'))))>=16:ms.remove(m)
        for j,text in enumerate(checks,16):
            r=deepcopy(rows[16]);r.set('r',str(j))
            for cell in r:cell.set('r',re.sub(r'\d+',str(j),cell.get('r')))
            s.data.append(r);s.merge(f'B{j}:D{j}');s.put(f'B{j}',text);s.put(f'E{j}','O');s.height(j,20+17*max(1,len(text.splitlines())))
        labels=['Type(N : Normal, A : Abnormal, B : Boundary)','Passed/Failed','Executed Date','Defect ID']
        for j,(source,label) in enumerate(zip((20,21,22,23),labels),end):
            r=deepcopy(rows[source]);r.set('r',str(j))
            for cell in r:cell.set('r',re.sub(r'\d+',str(j),cell.get('r')))
            s.data.append(r);s.merge(f'B{j}:D{j}');s.put(f'B{j}',label)
        s.put(f'A{end}','Result');s.put(f'E{end+1}',{'Passed':'P','Failed':'F'}.get(c['status'],c['status']));s.put(f'E{end+2}',c['executedAt']);s.put(f'E{end+3}',c['defect']);s.height(end,55)
        for addr,formula,cache in [('A5',f'=COUNTIF(E{end+1}:T{end+1},"P")',int(c['status']=='Passed')),('C5',f'=COUNTIF(E{end+1}:T{end+1},"F")',int(c['status']=='Failed')),('E5','=N5-A5-C5',int(c['status'] not in {'Passed','Failed'})),('K5',f'=COUNTIF(E{end}:T{end},"N")',0),('L5',f'=COUNTIF(E{end}:T{end},"A")',0),('M5',f'=COUNTIF(E{end}:T{end},"B")',0),('N5','=COUNTA(E7:T7)',1)]:s.put(addr,formula,cache)
        r=i+8;li.row(9,r,75)
        for a,v in zip('ABCDEF',[i,c['module'],c['method'],name,c['id']+' | '+c['title'],c['precondition']]):li.put(a+str(r),v)
        li.link('D'+str(r),name)
        r=i+11;st.row(12,r,30);st.put('A'+str(r),i);st.put('B'+str(r),name)
        st.link('B'+str(r),name)
        for a,src,cache in zip('CDEFGHI',['A5','C5','E5','K5','L5','M5','N5'],[int(c['status']=='Passed'),int(c['status']=='Failed'),int(c['status'] not in {'Passed','Failed'}),0,0,0,1]):st.put(a+str(r),f"='{name}'!{src}",cache)
    r=len(cases)+12;st.row(total_pattern,r,25);st.put('B'+str(r),'Sub total')
    for a in 'CDEFGHI':st.put(a+str(r),f'=SUM({a}12:{a}{r-1})',sum(st.caches.get(a+str(j),0) for j in range(12,r)))
    executed=sum(c['status'] in {'Passed','Failed'} for c in cases);passed=sum(c['status']=='Passed' for c in cases)
    st.row(rate_patterns[125],r+2,25);st.put('B'+str(r+2),'Test coverage');st.put('D'+str(r+2),f'=IFERROR((C{r}+D{r})*100/I{r},0)',executed*100/len(cases) if cases else 0);st.put('E'+str(r+2),'%')
    st.row(rate_patterns[126],r+3,25);st.put('B'+str(r+3),'Test successful coverage');st.put('D'+str(r+3),f'=IFERROR(C{r}*100/I{r},0)',passed*100/len(cases) if cases else 0);st.put('E'+str(r+3),'%')
    for offset,label,sourcecol,pattern in [(4,'Normal case','F',127),(5,'Abnormal case','G',128),(6,'Boundary case','H',129)]:
        st.row(rate_patterns[pattern],r+offset,25);st.put('B'+str(r+offset),label);st.put('D'+str(r+offset),f'=IFERROR({sourcecol}{r}*100/I{r},0)',0);st.put('E'+str(r+offset),'%')
    return t


def table_book(path,cases,meta,title,system=False):
    t=Template(path);cover(t,title,meta['date']);li=t.add('Test Cases');li.keep(9);li.clear(9)
    li.put('D3','TrustCabinet');li.put('D4','SEP490-G116');li.put('D5','.NET 10 / xUnit; PostgreSQL / Redis; Chrome / Edge for manual UI workflows.');li.height(5,65)
    st=t.add('Test Statistics')
    subtotal_row=int(re.search(r'\d+',next(a for a,v in st.values.items() if v=='Sub total'))[0]);total_pattern=deepcopy(st.data.find(f"{tag('row')}[@r='{subtotal_row}']"))
    rate_rows={label:int(re.search(r'\d+',next(a for a,v in st.values.items() if v==label))[0]) for label in ['Test coverage','Test successful coverage']}
    rate_patterns={label:deepcopy(st.data.find(f"{tag('row')}[@r='{row}']")) for label,row in rate_rows.items()}
    labelcol=re.match('[A-Z]+',next(a for a,v in st.values.items() if v=='Test coverage'))[0]
    valuecol=re.match('[A-Z]+',next(a for a in st.formulas if int(re.search(r'\d+',a)[0])==rate_rows['Test coverage']))[0]
    percentcol=re.match('[A-Z]+',next(a for a,v in st.values.items() if v=='%' and int(re.search(r'\d+',a)[0])==rate_rows['Test coverage']))[0]
    st.keep(11);st.clear(11)
    st.put('C3','TrustCabinet');st.put('C4','SEP490-G116');st.put('C5','SEP490-G116_Test_Report_v1');st.put('C6','Round 1. Pending: NotRun/Blocked/Partial. N/A: Skipped.');st.height(6,60)
    for addr in ('G5','H5'):
        if addr in st.values:st.put(addr,meta['date'])
    for addr,value in list(st.values.items()):
        if value=='TienTM':st.put(addr,'TrustCabinet Team')
    groups=defaultdict(list)
    for c in cases:groups[c['module'] if system or c.get('automation')!='Manual' else 'UI '+c['module']].append(c)
    for i,(group,items) in enumerate(sorted(groups.items()),1):
        name=re.sub(r'[:*?/\\\[\]]','-',group)[:31]
        s=t.add('WorkFlow1' if system else 'Authentication',name);s.keep(12);s.clear(11)
        s.put('B2',items[0]['title'] if system else group);s.put('B3','Source-reviewed '+('end-to-end workflow' if system else 'integration test definitions')+'; record results only after execution.');s.height(3,42)
        s.put('B4',len(items));s.put('P2','Passed');s.put('P3','Failed');s.put('P4','NotRun');s.put('P5','Skipped')
        old=s.root.find(tag('mergeCells'))
        if old is not None:
            for m in list(old):
                if max(map(int,re.findall(r'\d+',m.get('ref'))))>=11:old.remove(m)
        row=11
        for c in items:
            s.row(12,row,24);s.merge(f'A{row}:O{row}');s.put('A'+str(row),c['title'] if system else c['className']);row+=1
            fields=[c['id'],c['title'],c['steps'],c['expected'],c['precondition'],c['status'],c['executedAt'],c['tester'],'','','','','','',c['source']+':'+str(c['line'])+ ('\n'+c['actual'] if c['actual'] else '')+ ('\nEvidence: '+c['evidence'] if c['evidence'] else '')]
            parts=[pieces(v,40 if k in [1,2,3,4] else 27,10 if k in [1,2,3,4] else 5) for k,v in enumerate(fields)]
            for n in range(max(map(len,parts))):
                s.row(12,row,180)
                for k,values in enumerate(parts):
                    value=values[n] if n<len(values) else ''
                    if n and k in [0,5,6,7,8,9,10,11,12,13]:value=''
                    s.put(col(k+1)+str(row),value)
                maxlines=max(len(v[n].splitlines()) for v in parts if n<len(v));s.height(row,max(80,20+18*maxlines));row+=1
        last=row-1
        for rr,cc in [(6,'F'),(7,'I'),(8,'L')]:
            for addr,formula in [(f'B{rr}',f'=COUNTIF({cc}12:{cc}{last},"Passed")'),(f'C{rr}',f'=COUNTIF({cc}12:{cc}{last},"Failed")'),(f'E{rr}',f'=COUNTIF({cc}12:{cc}{last},"Skipped")'),(f'D{rr}',f'=B4-B{rr}-C{rr}-E{rr}')]:
                status={'B':'Passed','C':'Failed','E':'Skipped'}.get(addr[0]);cache=sum(c['status']==status for c in items) if status and rr==6 else (len(items)-sum(c['status'] in {'Passed','Failed','Skipped'} for c in items) if rr==6 and addr[0]=='D' else len(items) if addr[0]=='D' else 0);s.put(addr,formula,cache)
        r=i+8;li.row(9,r,85)
        for a,v in zip('BCDEF',[i,items[0]['title'] if system else group,name,f'{len(items)} definitions / scenarios',items[0]['precondition'] if system else 'See each case and its source fixture.']):li.put(a+str(r),v)
        li.link('D'+str(r),name)
        r=i+10;st.row(11,r,45);st.put('B'+str(r),i);st.put('C'+str(r),name)
        st.link('C'+str(r),name)
        for a,target in zip('DEFGH',['B6','C6','D6','E6','B4']):st.put(a+str(r),f"='{name}'!{target}",s.values.get(target,s.caches.get(target,0)))
    total=len(groups)+11;st.row(total_pattern,total,25);st.put('C'+str(total),'Sub total')
    for a in 'DEFGH':st.put(a+str(total),f'=SUM({a}11:{a}{total-1})',sum(st.caches.get(a+str(j),0) for j in range(11,total)))
    denominator=sum(c['status']!='Skipped' for c in cases)
    for offset,label,formula,value in [(2,'Test coverage',f'=IFERROR((D{total}+E{total})*100/(H{total}-G{total}),0)',sum(c['status'] in {'Passed','Failed'} for c in cases)),(3,'Test successful coverage',f'=IFERROR(D{total}*100/(H{total}-G{total}),0)',sum(c['status']=='Passed' for c in cases))]:
        st.row(rate_patterns[label],total+offset,25);st.put(labelcol+str(total+offset),label);st.put(valuecol+str(total+offset),formula,value*100/denominator if denominator else 0);st.put(percentcol+str(total+offset),'%')
    return t


def uat_book(path,cases,meta):
    t=Template(path);cover(t,'USER ACCEPTANCE TEST REPORT DOCUMENT',meta['date']);s=t.add('Statistics');s.clear(10);s.keep(22)
    for a,v in {'B3':'TrustCabinet','B4':'SEP490-G116','E3':'TrustCabinet Team','E4':meta['date'],'B5':'SEP490-G116_Test_Report5.4_v1.0','B6':'No customer feedback or acceptance decision has been collected. Criteria below require representative-user execution.'}.items():s.put(a,v)
    s.height(6,55)
    for i,c in enumerate(cases,10):
        s.put('A'+str(i),c['id']);s.put('B'+str(i),c['title']);s.put('D'+str(i),c['actual'] or 'No response collected.');s.put('E'+str(i),c['status']);s.height(i,32)
        s.notes['B'+str(i)]='Preconditions: '+c['precondition']+'\n\nProcedure:\n'+c['steps']+'\n\nExpected result: '+c['expected']+'\n\nSource: '+c['source']+ ('\nTester: '+c['tester']+'\nDate: '+c['executedAt']+'\nEvidence: '+c['evidence'] if c['tester'] else '')
    extras=[('Business needs fit','Awaiting representative organizer, approver, participant and administrator feedback.'),('Ease of use','Awaiting customer feedback.'),('Improvements and new features','Awaiting customer feedback; no requests inferred.'),('Overall satisfaction','No ratings collected; an average score cannot be calculated.'),('Acceptance decision','Pending stakeholder review and sign-off.')]
    for i,(title,text) in enumerate(extras,18):s.put('A'+str(i),i-9);s.put('B'+str(i),title);s.put('D'+str(i),text);s.put('E'+str(i),'Pending');s.height(i,40)
    f=t.add('Customer Feedback Statistics');f.keep(28);f.clear(1)
    # The source sheet consists of three positioned survey screenshots. The package
    # writer retains those anchors and replaces their images with empty English panels.
    raw=t.add('Customers Feedbacks');raw.keep(6);raw.clear(2)
    headers=['ID','Start time','Completion time','Email','Name','Last modified time','Name and business role','Do the demonstrated features meet your meeting-management needs?','How easy is the system to understand and use?','Which demonstrated features do you value most?','Which areas need improvement?','What features should be added next?','Which business rules or workflow steps are unclear?','UI/UX suggestions','Overall satisfaction (1-5)','General comments for the development team']
    for i,h in enumerate(headers,1):raw.put(col(i)+'1',h)
    raw.height(1,110)
    return t
