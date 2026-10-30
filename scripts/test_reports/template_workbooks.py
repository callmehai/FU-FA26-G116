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
import io,posixpath
from PIL import Image,ImageDraw,ImageFont
from lxml import etree as E
from readable import test_date,table_fields,unit_fields,description,endpoint,authored
from values import case_inputs

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


AFTER_HYPERLINKS={'printOptions','pageMargins','pageSetup','headerFooter','rowBreaks','colBreaks','customProperties','cellWatches','ignoredErrors','smartTags','drawing','legacyDrawing','legacyDrawingHF','picture','oleObjects','controls','webPublishItems','tableParts','extLst'}


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
        # Printer settings are not carried over, so their relationship would dangle.
        for e in self.root.findall(tag('pageSetup')):e.attrib.pop('{'+R+'}id',None)
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

    def widths(self):
        found={}
        for c in self.root.iter(tag('col')):
            for n in range(int(c.get('min')),min(int(c.get('max')),40)+1):found[col(n)]=float(c.get('width','9'))
        return found

    def width(self,letter,width):
        # Single-column <col> entries are widened; a column no entry covers gets its own one.
        n=number(letter);cols=self.root.find(tag('cols'))
        for c in self.root.iter(tag('col')):
            if c.get('min')==c.get('max')==str(n):c.set('width',str(width));c.set('customWidth','1');return
            if int(c.get('min'))<=n<=int(c.get('max')):return
        if cols is not None:
            new=E.Element(tag('col'),min=str(n),max=str(n),width=str(width),customWidth='1')
            after=[c for c in cols if int(c.get('min'))<n]
            if after:after[-1].addnext(new)
            else:cols.insert(0,new)

    def height(self,row,height):
        r=self.cell('A'+str(row)).getparent();r.set('ht',str(min(409,height)));r.set('customHeight','1')

    def merge(self,ref):
        ms=self.root.find(tag('mergeCells'))
        if ms is None:
            ms=E.Element(tag('mergeCells'));self.root.insert(list(self.root).index(self.data)+1,ms)
        if ref not in [e.get('ref') for e in ms]:E.SubElement(ms,tag('mergeCell'),ref=ref)
        ms.set('count',str(len(ms)))

    def validate(self,sqref,formula):
        # dataValidations sits after mergeCells/conditionalFormatting and before hyperlinks.
        dvs=self.root.find(tag('dataValidations'))
        if dvs is None:
            dvs=E.Element(tag('dataValidations'));anchor=next((e for e in self.root if E.QName(e).localname in {'hyperlinks'}|AFTER_HYPERLINKS),None)
            if anchor is not None:anchor.addprevious(dvs)
            else:self.root.append(dvs)
        dv=E.SubElement(dvs,tag('dataValidation'),type='list',allowBlank='1',showErrorMessage='1',sqref=sqref);E.SubElement(dv,tag('formula1')).text=formula
        dvs.set('count',str(len(dvs)))

    def link(self,addr,sheet,display=None):
        links=self.root.find(tag('hyperlinks'))
        if links is None:
            # CT_Worksheet order: hyperlinks follows phoneticPr/conditionalFormatting/dataValidations.
            links=E.Element(tag('hyperlinks'));anchor=next((e for e in self.root if E.QName(e).localname in AFTER_HYPERLINKS),None)
            if anchor is not None:anchor.addprevious(links)
            else:self.root.append(links)
        # Google Sheets shows a converted internal link as "#gid=..." unless the link carries its text.
        E.SubElement(links,tag('hyperlink'),ref=addr,location="'"+sheet.replace("'","''")+"'!A1",display=display or sheet)

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
        self.original={};self.chart=None
        names={s.get('name') for s in self.workbook.find(tag('sheets'))}
        # Only these source patterns are reused. Parsing every formatted, empty row
        # in all 112 legacy unit sheets would waste several gigabytes of memory.
        if 'ModuleName1' in names:
            limits={'Cover':20,'Test Cases':12,'Test Statistics':40,'ModuleName1':24}
        elif 'Unit' in self.path.name:
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
                if s.name=='Cover' or s.name=='Statistics' and 'login' in self.original or s.name=='Customer Feedback Statistics' or s.name=='Test Statistics' and 'ModuleName1' in self.original:
                    op,root=self.original[s.name];drawing=root.find(tag('drawing'))
                    rp=op.rsplit('/',1)[0]+'/_rels/'+op.rsplit('/',1)[1]+'.rels'
                    if drawing is not None and rp in self.parts:
                        # <drawing> precedes <legacyDrawing>, which the notes above may have added.
                        final=E.fromstring(parts[f'xl/worksheets/sheet{i}.xml']);legacy=final.find(tag('legacyDrawing'))
                        if legacy is not None:legacy.addprevious(deepcopy(drawing))
                        else:final.append(deepcopy(drawing))
                        parts[f'xl/worksheets/sheet{i}.xml']=xml(final)
                        links=E.fromstring(self.parts[rp])
                        for link in list(links):
                            if not link.get('Type','').endswith('/drawing'):links.remove(link)
                        existing=parts.get(f'xl/worksheets/_rels/sheet{i}.xml.rels')
                        if existing:
                            merged=E.fromstring(existing)
                            for link in links:merged.append(deepcopy(link))
                            links=merged
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
        if 'ModuleName1' in self.original and self.chart:
            # Unit template: two pies under the rate rows. Keep each pie's first series and
            # point it at the Sub total row; the other series reference deleted rows (#REF!).
            chartns={'c':'http://schemas.openxmlformats.org/drawingml/2006/chart','a':'http://schemas.openxmlformats.org/drawingml/2006/main','xdr':'http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing'}
            total=self.chart['row'];drawing_path=next((k for k in parts if k.startswith('xl/drawings/drawing') and k.endswith('.xml') and b'/chart' in parts.get('xl/drawings/_rels/'+k.rsplit('/',1)[1]+'.rels',b'')),None)
            if drawing_path:
                drawing=E.fromstring(parts[drawing_path])
                for node in drawing.findall('.//xdr:from/xdr:row',chartns):node.text=str(total+7)
                for node in drawing.findall('.//xdr:to/xdr:row',chartns):node.text=str(total+22)
                parts[drawing_path]=xml(drawing)
            for chart,(cols,values,title) in {'xl/charts/chart1.xml':('$F$%d:$H$%d',self.chart['types'],'Test Type'),'xl/charts/chart2.xml':('$C$%d:$E$%d',self.chart['status'],'Test Result')}.items():
                if chart not in parts:continue
                tree=E.fromstring(parts[chart])
                for plot in tree.findall('.//c:plotArea/*',chartns):
                    for extra in plot.findall('c:ser',chartns)[1:]:plot.remove(extra)
                ser=tree.find('.//c:ser',chartns)
                ser.find('c:cat//c:f',chartns).text="'Test Statistics'!"+cols%(11,11)
                ref=ser.find('c:val/c:numRef',chartns);ref.find('c:f',chartns).text="'Test Statistics'!"+cols%(total,total)
                cache=ref.find('c:numCache',chartns)
                for pt in cache.findall('c:pt',chartns):cache.remove(pt)
                for k,v in enumerate(values):
                    pt=E.SubElement(cache,'{'+chartns['c']+'}pt',idx=str(k));E.SubElement(pt,'{'+chartns['c']+'}v').text=str(v)
                node=tree.find('.//c:title//a:t',chartns)
                if node is not None:node.text=title
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


def cover(t,title,date,history=None):
    s=t.add('Cover');s.keep(20);s.clear(11)
    history=history or [(date,'1.0','Test documentation','A','Generate English test documentation from TrustCabinet source using the supplied template.')]
    for a,v in {'B2':title,'B4':'TrustCabinet','B5':'SEP490-G116','F4':'TrustCabinet Team','F5':date,'F6':history[-1][1]}.items():s.put(a,v)
    for r,row in enumerate(history,11):
        for a,v in zip('ABCDEF',row):s.put(a+str(r),v)
    s.put('B6','=B5&"_"&B2&"_v"&F6',f'SEP490-G116_{title}_v{history[-1][1]}')
    return s


ABNORMAL=re.compile(r'(?i)(^|_)(reject|refuse|invalid|fail|throw|error|den(y|ies|ied)|forbid|unauthori[sz]ed|not_?found|missing|wrong|expired?|conflict|cannot|can_?not|blocked|duplicate|bad|disallow|no_longer|unknown|locked|inactive|deactivated|mismatch|blank|null|empty|outside|revoked|tamper|malformed|unsupported|answers_msg|stale|too_)')
BOUNDARY=re.compile(r'(?i)(^|_)(boundary|limit|limits|max|maximum|min|minimum|exactly|edge|threshold|length|longest|shortest|at_most|at_least|last|zero|immediately|cap|caps|capped|upper|lower|equal_to)(_|$)')
def unit_type(c,inputs):
    """Normal / Abnormal / Boundary, inferred from the test name and data; the tester reviews it."""
    name=c['method']
    if BOUNDARY.search(name):return 'Boundary'
    if ABNORMAL.search(name):return 'Abnormal'
    if c['kind']=='Theory' and re.search(r'=\s*(null|""|"\s+"|-\d|0\b|int\.MaxValue|int\.MinValue)',inputs):return 'Boundary'
    return 'Normal'


def wrapped(text,width):
    return sum(max(1,-(-len(line)//max(1,int(width*0.85)))) for line in str(text).splitlines() or [''])


LINE=14.5;BUDGET=26
def fit(text,width,lines=BUDGET):
    """Keep a cell within Excel's 409pt row limit, cutting whole lines and saying so."""
    text=str(text);kept=[]
    for line in text.splitlines():
        if wrapped('\n'.join(kept+[line]),width)>lines-1:
            kept.append('… (continued in the test source)');break
        kept.append(line)
    return '\n'.join(kept)


def height(cells,minimum=37.5):
    return max(minimum,min(409,8+LINE*max(wrapped(v,w) for v,w in cells)))


UNIT_RESULT={'Passed':'Passed','Failed':'Failed','Skipped':'N/A'}
def unit_book(path,cases,meta,history):
    t=Template(path);cv=cover(t,'UNIT TEST DOCUMENT',meta['date'],history)
    cv.put('B6','=B5&"_UT_v"&F6',f'SEP490-G116_UT_v{history[-1][1]}')
    modules=defaultdict(list)
    for c in cases:modules[c['module']].append(c)
    li=t.add('Test Cases');li.keep(12);li.clear(9)
    li.put('D3','=Cover!B4','TrustCabinet');li.put('D4','=Cover!B5','SEP490-G116')
    li.put('D5','1. Runtime: .NET SDK 10.0.401 (global.json), xUnit test project tests/TrustCabinet.Web.Tests\n2. Database: none; unit tests run in process with fakes and stubs\n3. Web browser: not required\n4. Command: dotnet test TrustCabinet.sln');li.height(5,72)
    st=t.add('Test Statistics')
    patterns={r:deepcopy(st.data.find(f"{tag('row')}[@r='{r}']")) for r in (16,18,19,20,21,22)}
    st.keep(12);st.clear(12)
    st.put('B4','=Cover!B4','TrustCabinet');st.put('B5','=Cover!B5','SEP490-G116');st.put('B6','=B5&"_"&"Test Report"&"_v"&Cover!F6',f"SEP490-G116_Test Report_v{history[-1][1]}")
    st.put('F4','TrustCabinet Team');st.put('F5','Pending review');st.put('F6',meta['date'])
    st.put('B7','')
    totals=dict(status=[0,0,0],types=[0,0,0],cases=0)
    for i,(module,items) in enumerate(sorted(modules.items()),1):
        name=re.sub(r'[:*?/\\\[\]]','-',module)[:31]
        s=t.add('ModuleName1',name);header=deepcopy(s.data.find(f"{tag('row')}[@r='12']"));case=deepcopy(s.data.find(f"{tag('row')}[@r='13']"))
        s.keep(11);s.clear(12);s.put('B2',module)
        for letter,size in (('B',30),('C',26),('D',46),('G',11)):s.width(letter,size)
        w=s.widths()
        for a,v in zip(('T2','T3','T4','T5'),('Passed','Failed','Pending','N/A')):s.put(a,v)
        row=12;n=0;counts=dict(Passed=0,Failed=0,Pending=0,Normal=0,Abnormal=0,Boundary=0)
        for c in sorted(items,key=lambda c:(c['className'],c['line'])):
            s.row(header,row);s.put(f'A{row}',c['method']);row+=1
            pre,_,expected=unit_fields(c);result=UNIT_RESULT.get(c['status'],'Pending');pre=fit(pre,w['B']);expected=fit(expected,w['D'])
            for inputs in case_inputs(c):
                n+=1;kind=unit_type(c,inputs);counts[kind]+=1;counts[result]=counts.get(result,0)+1;inputs=fit(inputs,w['C'])
                s.row(case,row,height([(pre,w['B']),(inputs,w['C']),(expected,w['D'])]))
                # Note stays empty: the method header row above already names the test.
                for a,v in zip('ABCDEFGHILOP',[f'UTC{n:03}',pre,inputs,expected,kind,result,test_date(c['executedAt']),c['tester'],'Pending','Pending',c['defect'],'']):s.put(a+str(row),v)
                row+=1
        last=row-1
        s.put('B3',f'=COUNTIF(A12:A{last},"UTC*")',n)
        for r,colname in ((5,'F'),(6,'I'),(7,'L')):
            for a,label in zip('BCD',('Passed','Failed','Pending')):s.put(f'{a}{r}',f'=COUNTIF(${colname}$12:${colname}${last},{a}$4)',counts[label] if r==5 else (n if label=='Pending' else 0))
        for a,label in zip('BCD',('Normal','Abnormal','Boundary')):s.put(f'{a}9',f'=COUNTIF($E$12:$E${last},{a}$8)',counts[label])
        s.validate(f'F12:F{last} I12:I{last} L12:L{last}','$T$2:$T$5');s.validate(f'E12:E{last}','"Normal,Abnormal,Boundary"')
        status=[counts['Passed'],counts['Failed'],n-counts['Passed']-counts['Failed']];types=[counts['Normal'],counts['Abnormal'],counts['Boundary']]
        r=i+8;li.row(9,r,20)
        for a,v in zip('BCDEF',[i,module,name,f"{len(items)} methods, {n} cases",'None (in-process fakes)']):li.put(a+str(r),v)
        li.link('D'+str(r),name)
        r=i+11;st.row(12,r,20);st.put('A'+str(r),i);st.put('B'+str(r),name);st.link('B'+str(r),name)
        q="'"+name.replace("'","''")+"'!"
        for a,formula,cache in zip('CDEFGHI',[q+'B5',q+'C5',f'{q}B3-{q}B5-{q}C5',q+'B9',q+'C9',q+'D9',q+'B3'],status+types+[n]):st.put(a+str(r),'='+formula,cache)
        totals['status']=[x+y for x,y in zip(totals['status'],status)];totals['types']=[x+y for x,y in zip(totals['types'],types)];totals['cases']+=n
    first=12;sub=len(modules)+first+2;st.row(patterns[16],sub,25);st.put('B'+str(sub),'Sub total')
    for a,v in zip('CDEFGHI',totals['status']+totals['types']+[totals['cases']]):st.put(a+str(sub),f'=SUM({a}{first}:{a}{sub-1})',v)
    whole=totals['cases'] or 1
    for offset,(label,formula,value) in enumerate([('Test coverage',f'(C{sub}+D{sub})*100/I{sub}',(totals['status'][0]+totals['status'][1])*100/whole),('Test successful coverage',f'C{sub}*100/I{sub}',totals['status'][0]*100/whole),('Normal case',f'F{sub}*100/I{sub}',totals['types'][0]*100/whole),('Abnormal case',f'G{sub}*100/I{sub}',totals['types'][1]*100/whole),('Boundary case',f'H{sub}*100/I{sub}',totals['types'][2]*100/whole)]):
        r=sub+2+offset;st.row(patterns[18+offset],r,20);st.put('B'+str(r),label);st.put('D'+str(r),f'=IFERROR({formula},0)',round(value,2));st.put('E'+str(r),'%')
    t.chart=dict(row=sub,status=totals['status'],types=totals['types']);t.unit_cases=totals['cases']
    return t


def table_book(path,cases,meta,title,system=False):
    t=Template(path);cover(t,title,meta['date'],meta.get('history'));li=t.add('Test Cases');li.keep(9);li.clear(9)
    li.put('D3','TrustCabinet');li.put('D4','SEP490-G116');li.put('D5','.NET 10 / xUnit; PostgreSQL / Redis; Chrome / Edge for manual UI workflows.');li.height(5,65)
    st=t.add('Test Statistics')
    subtotal_row=int(re.search(r'\d+',next(a for a,v in st.values.items() if v=='Sub total'))[0]);total_pattern=deepcopy(st.data.find(f"{tag('row')}[@r='{subtotal_row}']"))
    rate_rows={label:int(re.search(r'\d+',next(a for a,v in st.values.items() if v==label))[0]) for label in ['Test coverage','Test successful coverage']}
    rate_patterns={label:deepcopy(st.data.find(f"{tag('row')}[@r='{row}']")) for label,row in rate_rows.items()}
    labelcol=re.match('[A-Z]+',next(a for a,v in st.values.items() if v=='Test coverage'))[0]
    valuecol=re.match('[A-Z]+',next(a for a in st.formulas if int(re.search(r'\d+',a)[0])==rate_rows['Test coverage']))[0]
    percentcol=re.match('[A-Z]+',next(a for a,v in st.values.items() if v=='%' and int(re.search(r'\d+',a)[0])==rate_rows['Test coverage']))[0]
    st.keep(11);st.clear(11)
    st.put('C3','TrustCabinet');st.put('C4','SEP490-G116');st.put('C5','SEP490-G116_Test_Report_v1');st.put('C6','')
    for addr in ('G5','H5'):
        if addr in st.values:st.put(addr,meta['date'])
    for addr,value in list(st.values.items()):
        if value=='TienTM':st.put(addr,'TrustCabinet Team')
    groups=defaultdict(list)
    for c in cases:groups[c['module']].append(c)
    for i,(group,items) in enumerate(sorted(groups.items()),1):
        name=re.sub(r'[:*?/\\\[\]]','-',group)[:31]
        s=t.add('WorkFlow1' if system else 'Authentication',name);s.keep(12);s.clear(11)
        s.put('B2',items[0]['title'] if system else group);s.put('B3','System-level test flow: '+items[0]['title'] if system else f'Functional integration tests for {group.lower()} through the web interface' if items[0].get('authoredText') else f'API tests for the {group.lower()} endpoints');s.height(3,42)
        s.put('B4',len(items));s.put('P2','Passed');s.put('P3','Failed');s.put('P4','Pending');s.put('P5','Skipped')
        old=s.root.find(tag('mergeCells'))
        if old is not None:
            for m in list(old):
                if max(map(int,re.findall(r'\d+',m.get('ref'))))>=11:old.remove(m)
        w=s.widths()
        # API cases are grouped under the endpoint they call, as in the template; others by test class.
        # Hand-written cases (readable.authored) keep their own order, group and code; the rest are
        # grouped under the endpoint they call, as in the template, or by test class.
        hand=authored()
        heading=lambda c:hand[c['id']]['group'] if c['id'] in hand else c['group'] if c.get('group') else c['title'] if system else (endpoint(c) if c['level']=='API' else None) or c['className']
        order=lambda c:(0,hand[c['id']]['order']) if c['id'] in hand else (0,(c['id'],0)) if c.get('authoredText') else (1,(heading(c),c['line']))
        row=11;previous=None
        for c in items if system else sorted(items,key=order):
            if heading(c)!=previous:
                previous=heading(c);s.row(12,row,24);s.merge(f'A{row}:O{row}');s.put('A'+str(row),previous);row+=1
            steps,expected,pre=table_fields(c)
            status=c['status'] if c['status'] in {'Passed','Failed','Skipped'} else 'Pending'
            ident,text=(hand[c['id']]['code'],hand[c['id']]['description']) if c['id'] in hand else (c['id'],c['title'] if c.get('automation')=='Manual' else description(c))
            fields=[ident,text,fit(steps,w.get('C',30)),fit(expected,w.get('D',30)),fit(pre,w.get('E',30)),status,test_date(c['executedAt']),c['tester'],'','','','','','','']
            s.row(12,row,height([(v,w.get(col(k+1),9)) for k,v in enumerate(fields) if k in (1,2,3,4)],60))
            for k,v in enumerate(fields):s.put(col(k+1)+str(row),v)
            row+=1
        last=row-1
        for rr,cc in [(6,'F'),(7,'I'),(8,'L')]:
            for addr,formula in [(f'B{rr}',f'=COUNTIF({cc}12:{cc}{last},"Passed")'),(f'C{rr}',f'=COUNTIF({cc}12:{cc}{last},"Failed")'),(f'E{rr}',f'=COUNTIF({cc}12:{cc}{last},"Skipped")'),(f'D{rr}',f'=B4-B{rr}-C{rr}-E{rr}')]:
                status={'B':'Passed','C':'Failed','E':'Skipped'}.get(addr[0]);cache=sum(c['status']==status for c in items) if status and rr==6 else (len(items)-sum(c['status'] in {'Passed','Failed','Skipped'} for c in items) if rr==6 and addr[0]=='D' else len(items) if addr[0]=='D' else 0);s.put(addr,formula,cache)
        listed=table_fields(items[0])[2] if system else 'See the Pre-conditions of each case.'
        r=i+8;li.row(9,r,height([(listed,li.widths().get('F',30))],40))
        for a,v in zip('BCDEF',[i,items[0]['title'] if system else group,name,f'{len(items)} definitions / scenarios',listed]):li.put(a+str(r),v)
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
    t=Template(path);cover(t,'USER ACCEPTANCE TEST REPORT DOCUMENT',meta['date'],meta.get('history'));s=t.add('Statistics');s.clear(10);s.keep(22)
    for a,v in {'B3':'TrustCabinet','B4':'SEP490-G116','E3':'TrustCabinet Team','E4':meta['date'],'B5':'SEP490-G116_Test_Report5.4_v1.0','B6':''}.items():s.put(a,v)
    s.height(6,55)
    for i,c in enumerate(cases,10):
        s.put('A'+str(i),c['id']);s.put('B'+str(i),c['title']);s.put('D'+str(i),c['actual'] or 'No response collected.');s.put('E'+str(i),c['status'] if c['status'] in {'Passed','Failed'} else 'Pending');s.height(i,32)
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
