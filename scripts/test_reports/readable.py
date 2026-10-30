"""Turn the C# of an xUnit test into the plain, numbered text a tester reads in Report 5.

Every function here is a heuristic over the source the extractor captured: it names the
request, the arranged state and each assertion in words. The source reference kept in
each row's Note stays the authority when a sentence is too coarse.
"""
import json,re
from functools import lru_cache
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
TESTER='HuyNT'
HTTP={'OK':200,'Created':201,'Accepted':202,'NoContent':204,'MovedPermanently':301,'Found':302,'Redirect':302,'NotModified':304,
    'BadRequest':400,'Unauthorized':401,'Forbidden':403,'NotFound':404,'MethodNotAllowed':405,'Conflict':409,'Gone':410,
    'PreconditionFailed':412,'RequestEntityTooLarge':413,'UnsupportedMediaType':415,'UnprocessableEntity':422,
    'UnprocessableContent':422,'PreconditionRequired':428,'TooManyRequests':429,'InternalServerError':500,'ServiceUnavailable':503}
SETUP=re.compile(r'\b(Seed\w*|Given\w*|With\w*|Add\w*|CreateScope|CreateAsyncScope|GetRequiredService|CreateInstance|Db|Factory|Fixture|Setup\w*|Arrange\w*)\s*\(|^\s*(?:using\s+)?(?:var\s+)?\w+(?:\.\w+)+\s*=[^=>]|^\s*(?:await\s+)?(?:using\s+)?var\s+\w+\s*=\s*new\b|^\s*(?:await\s+)?(?:using\s+)?var\s+\w+\s*=\s*(?:Guid\.\w+|New\w*|Create\w*)\(\)$|^\s*(?:await\s+)?(?:using\s+)?var\s+\w+\s*=\s*(?:await\s+)?(?:New|Make|Build|Valid|Sample)\w*\((?!.*\)\s*\.\s*\w+\s*\()')
CLIENT=re.compile(r'\b(ClientFor|CreateClient|CreateDefaultClient|NewClient)\s*\(')
REQUEST=re.compile(r'\.(Get|Post|Put|Patch|Delete)(?:AsJson|FromJson|String|Stream|ByteArray)?Async(?:<[^>(]*>)?\(')


@lru_cache(maxsize=None)
def authored():
    """Hand-written cases from scripts/test_reports/cases/*.json, keyed by inventory id.

    A tester writes these the way the template's sample does: a request a person can send, the
    data to send and the behaviour to see. They replace the text inferred from source for that test.
    """
    found={}
    for path in sorted((ROOT/'scripts/test_reports/cases').glob('api-*.json')):
        data=json.loads(path.read_text(encoding='utf-8'))
        for n,case in enumerate(data['cases'],1):found[case['id']]=dict(expand(case),code=f"{data['prefix']}{n:02}",order=(path.name,n))
    return found


# Who sends a request, in the short form the case files use ("as": "admin").
SENDERS={'none':'None (no authentication)',
    'admin':'Authorization: Bearer <access token of quantri@trustcabinet.local> (Admin, AdminUsers)',
    'secretary':'Authorization: Bearer <access token of thuky@trustcabinet.local> (Secretary, MeetingCreator)',
    'chair':'Authorization: Bearer <access token of chutoa@trustcabinet.local> (Chair)',
    'member':'Authorization: Bearer <access token of thanhvien@trustcabinet.local> (Member, no capability)'}


def expand(case):
    """Short case form -> full form. "req" writes the four request steps of the template:
    the request, its headers, its query parameters and its body; "steps" are added after them."""
    case=dict(case)
    case.setdefault('description',case.pop('d',''))
    case.setdefault('expected',case.pop('exp',[]))
    pre=case.pop('pre',case.get('precondition',[]))
    case['precondition']=pre if pre and pre[0].startswith('Backend') else ['Backend is running.']+pre
    if 'req' in case:
        r=case.pop('req');method,url=r['m'],r['url'];path,_,query=url.partition('?')
        sender=SENDERS.get(r.get('as','none'),r.get('as'))
        headers=(['Content-Type: application/json'] if r.get('body') else [])+[sender]
        steps=[f'Send {method} request to {url}.','Request headers: '+'; '.join(headers)+'.',
            'Query parameters: '+('; '.join(query.split('&')) if query else 'None')+'.']
        body=r.get('body')
        steps.append(f'Request JSON body: {body}.' if body else f'Request body: None ({method} request).' if method in {'GET','DELETE'} else 'Request body: None.')
        case['procedure']=steps+case.pop('steps',[])
    elif 'steps' in case:case['procedure']=case.pop('steps')
    return case


def test_date(value):
    """dd/mm/yyyy from a TRX or ISO timestamp; the time of day is not reported."""
    m=re.match(r'(\d{4})-(\d{2})-(\d{2})',value or '')
    return f'{m[3]}/{m[2]}/{m[1]}' if m else ''


def csharp_split(text,sep=','):
    """Split C# on top-level separators, respecting literals, brackets, generics and lambdas."""
    parts=[];depth=0;angle=0;start=0;i=0
    while i<len(text):
        ch=text[i]
        if ch=='"' or ch=="'":
            i=literal_end(text,i);continue
        if ch in '([{':depth+=1
        elif ch in ')]}':depth-=1
        elif ch=='<' and i and (text[i-1].isalnum() or text[i-1]=='_') and text[i+1:i+2].isalpha():angle+=1
        elif ch=='>' and angle and text[i-1]!='=' and text[i+1:i+2]!='=':angle-=1
        elif ch==sep and depth==0 and angle==0:parts.append(text[start:i].strip());start=i+1
        i+=1
    tail=text[start:].strip()
    return parts+[tail] if tail or parts else parts


def literal_end(text,i):
    """Index just past the string or char literal starting at text[i]."""
    if text[i]=="'":
        j=i+1
        while j<len(text) and text[j]!="'":j+=2 if text[j]=='\\' else 1
        return j+1
    quotes=len(text[i:])-len(text[i:].lstrip('"'))
    if quotes>=3:
        end=text.find('"'*quotes,i+quotes);return len(text) if end<0 else end+quotes
    verbatim='@' in text[max(0,i-2):i];j=i+1
    while j<len(text):
        if text[j]=='"':
            if verbatim and text[j+1:j+2]=='"':j+=2;continue
            return j+1
        j+=2 if text[j]=='\\' and not verbatim else 1
    return j


def statements(body):
    """Top-level statements of a method body, comments removed and whitespace collapsed."""
    text=body.strip()
    if text.startswith('=>'):text=text[2:]
    elif text.startswith('{') and text.endswith('}'):text=text[1:-1]
    out=[];buf=[];depth=0;i=0
    def flush():
        s=' '.join(''.join(buf).split()).strip()
        if s and s!=';':out.append(s.rstrip(';').strip())
        buf.clear()
    while i<len(text):
        ch=text[i]
        if text.startswith('//',i):
            j=text.find('\n',i);i=len(text) if j<0 else j;continue
        if text.startswith('/*',i):
            j=text.find('*/',i);i=len(text) if j<0 else j+2;continue
        if ch in '"\'':
            j=literal_end(text,i);buf.append(text[i:j]);i=j;continue
        buf.append(ch)
        if ch in '([{':depth+=1
        elif ch in ')]}':
            depth-=1
            # A block ends its statement; an initializer or lambda is followed by ; ) , or .
            after=text[i+1:].lstrip()
            if ch=='}' and depth==0 and not re.match(r'[;),.]|else\b|catch\b|finally\b',after):flush()
        elif ch==';' and depth==0:flush()
        i+=1
    flush()
    return out


def clip(text,limit):
    text=' '.join(str(text).split())
    return text if len(text)<=limit else text[:limit-1].rstrip()+'…'


DOCUMENTS={'root','body','json','doc','document','error','problem','payload','rejected','content','response'}
def simplify(x):
    """Readable form of a C# expression: JSON lookups become paths, noise is dropped."""
    x=' '.join(x.split())
    x=re.sub(r'\bawait\s+|\.ConfigureAwait\(false\)|\.ToString\(\)|!(?=[.)\],;]|$)','',x)
    x=re.sub(r'\b(?:Npgsql|System\.Text\.Json|System\.Net|Microsoft\.AspNetCore\.Authorization|TrustCabinet\.Infrastructure|TrustCabinet\.Domain(?:\.\w+)?)\.(?=[A-Z])','',x)
    def path(m):
        props='.'.join(re.findall(r'GetProperty\("([^"]+)"\)',m[3]));tail=m[4] or ''
        target=props if m[2] or m[1] in DOCUMENTS else m[1]+'.'+props
        if 'GetArrayLength' in tail:return f'length of {target}'
        if 'ValueKind' in tail:return f'{target} JSON kind'
        return target
    x=re.sub(r'(\w+)(\.RootElement)?((?:\.GetProperty\("[^"]+"\))+)(\.(?:Get\w+|Enumerate\w+)\(\)|\.ValueKind)?',path,x)
    x=re.sub(r'(\w+)\.RootElement\.(Get\w+|Enumerate\w+)\(\)',r'\1 body',x)
    x=re.sub(r'\bHttpStatusCode\.(\w+)',lambda m:http(m[1]),x)
    x=re.sub(r'JsonValueKind\.(\w+)',r'\1',x)
    return x


def http(name):
    words=re.sub(r'(?<=[a-z])(?=[A-Z])',' ',name)
    return f'HTTP {HTTP[name]} {words}' if name in HTTP else f'HTTP {words}'


def call_args(text,start):
    """Arguments of the call whose '(' is at text[start], plus the index past ')'."""
    depth=0;i=start
    while i<len(text):
        ch=text[i]
        if ch in '"\'':i=literal_end(text,i);continue
        if ch in '([{':depth+=1
        elif ch in ')]}':
            depth-=1
            if depth==0:return csharp_split(text[start+1:i]),i+1
        i+=1
    return csharp_split(text[start+1:]),len(text)


ASSERT=re.compile(r'Assert\.(\w+)\s*(?:<([^()]*)>)?\s*\(')
def assertion(a):
    """(kind, sentence) for one assertion; kind is status, exception, log, json or check."""
    a=' '.join(a.split());m=ASSERT.search(a)
    if not m:return 'check',clip(simplify(a),170)
    name,generic=m[1],m[2];args,_=call_args(a,m.end()-1);args=[simplify(x) for x in args];first=args[0] if args else ''
    second=args[1] if len(args)>1 else ''
    if name.startswith('Throws'):return 'exception',(generic or 'an exception').split('.')[-1]
    pair=re.fullmatch(r'\((\d{3}),\s*"(MSG\d+)"\)',first)
    if name=='Equal' and pair:
        code=next((k for k,v in HTTP.items() if v==int(pair[1])),'');call=REQUEST.search(second)
        via=''
        if call:
            url=call_args(second,call.end()-1)[0]
            via=f' for {call[1].upper()} `{url_of(url[0],second)}`' if url else ''
        return 'status',f'{http(code) if code else "HTTP "+pair[1]} response with `code` = "{pair[2]}"{via}'
    if name=='Equal' and second.endswith('StatusCode') and first.startswith('HTTP '):
        who=second[:-len('.StatusCode')]
        return 'status',f'{first} response'+('' if who in {'response',''} else f' for `{who}`')
    sentence={
        'Equal':lambda:f'`{second}` = {first}','NotEqual':lambda:f'`{second}` is not {first}','Same':lambda:f'`{second}` is the same object as `{first}`',
        'NotSame':lambda:f'`{second}` is a different object from `{first}`','True':lambda:f'`{first}` is true','False':lambda:f'`{first}` is false',
        'Null':lambda:f'`{first}` is null','NotNull':lambda:f'`{first}` is not null','Empty':lambda:f'`{first}` is empty','NotEmpty':lambda:f'`{first}` is not empty',
        'Single':lambda:f'`{first}` has exactly one item'+(' matching the condition' if second else ''),'Contains':lambda:f'`{second}` contains {first}',
        'DoesNotContain':lambda:f'`{second}` does not contain {first}','StartsWith':lambda:f'`{second}` starts with {first}','EndsWith':lambda:f'`{second}` ends with {first}',
        'Matches':lambda:f'`{second}` matches {first}','DoesNotMatch':lambda:f'`{second}` does not match {first}','InRange':lambda:f'`{first}` is between {second} and {args[2] if len(args)>2 else "?"}',
        'All':lambda:f'Every item of `{first}` meets: '+re.sub(r'^\w+\s*=>\s*','',second),'Collection':lambda:f'`{first}` has {len(args)-1} items, checked in order',
        'IsType':lambda:f'`{first}` is a {generic}','IsAssignableFrom':lambda:f'`{first}` is a {generic}','Equivalent':lambda:f'`{second}` is equivalent to {first}',
        'Subset':lambda:f'`{first}` includes every item of `{second}`','Superset':lambda:f'`{second}` includes every item of `{first}`','Fail':lambda:'The test fails explicitly: '+first,
    }.get(name)
    blank=re.fullmatch(r'string\.IsNullOrWhiteSpace\((.*)\)|string\.IsNullOrEmpty\((.*)\)',first)
    if blank and name in {'True','False'}:
        text=f'`{blank[1] or blank[2]}` is '+('blank' if name=='True' else 'not blank')
    else:text=sentence() if sentence else simplify(a)
    target=second if name in {'Equal','NotEqual','Contains','DoesNotContain','StartsWith','EndsWith','Matches','Equivalent'} else first
    kind='log' if re.search(r'(?i)\blog|logger|entries',target) else 'json' if re.search(r'GetProperty\(|\.RootElement',a) else 'check'
    return kind,clip(text,170)


@lru_cache(maxsize=None)
def source_text(path):
    try:return (ROOT/path).read_text(encoding='utf-8')
    except OSError:return ''


DECLARATION=re.compile(r'(?m)^\s*(?:(?:private|internal|public|protected|static|async|override)\s+)+[\w<>\[\](),.? ]+?\s+(\w+)\s*\(')
@lru_cache(maxsize=None)
def declarations():
    """Method name -> test files declaring it, from one pass over tests/."""
    index={}
    for p in sorted((ROOT/'tests').rglob('*.cs')):
        if {'bin','obj'}.intersection(p.parts):continue
        rel=p.relative_to(ROOT).as_posix()
        for name in set(DECLARATION.findall(source_text(rel))):index.setdefault(name,[]).append(rel)
    return index


@lru_cache(maxsize=None)
def helper(path,name):
    """Body text of a helper method with this name, looked up from the test's file outwards."""
    files=declarations().get(name,[]);folder=path.rsplit('/',1)[0]
    for candidate in sorted(files,key=lambda f:(f!=path,f.rsplit('/',1)[0]!=folder,f)):
        found=declared(candidate,name)
        if found:return found
    return ''


def declared(path,name):
    text=source_text(path);m=re.search(rf'(?m)^\s*(?:(?:private|internal|public|protected|static|async|override)\s+)+[\w<>\[\](),.? ]+?\s+{re.escape(name)}\s*\(',text)
    if not m:return ''
    start=text.find('{',m.end());arrow=text.find('=>',m.end())
    if arrow!=-1 and (start==-1 or arrow<start):end=text.find(';',arrow);return text[arrow:end+1]
    depth=0
    for i in range(start,len(text)):
        if text[i]=='{':depth+=1
        elif text[i]=='}':
            depth-=1
            if depth==0:return text[start:i+1]
    return text[start:]


def url_of(arg,body):
    arg=arg.strip()
    m=re.match(r'^\$?@?"(.*)"$',arg) or re.match(r'^@?\$"(.*)"$',arg)
    if m:return m[1]
    m=re.search(rf'\b{re.escape(arg)}\s*=\s*\$?@?"([^"]+)"',body)
    return m[1] if m else '<'+arg+'>'


def payload(arg):
    arg=arg.strip()
    if arg in {'','null','default'}:return ''
    m=re.match(r'^(?:JsonContent\.Create|new\s+StringContent)\((.*)\)$',arg)
    if m:arg=csharp_split(m[1])[0]
    if arg.startswith('new {') or arg.startswith('new{'):
        inner=arg[arg.index('{')+1:arg.rindex('}')];fields=[]
        for f in csharp_split(inner):
            k,_,v=f.partition('=');fields.append(f'{k.strip()}: {simplify(v.strip())}' if v else simplify(k))
        return clip('{ '+', '.join(fields)+' }',260)
    return clip(simplify(arg),200)


def requests(c,text=None,seen=None):
    """HTTP calls a test makes, directly or through a helper declared in its source file."""
    text=c['body'] if text is None else text;seen=seen or set();found=[]
    flat=' '.join(text.split())
    for m in REQUEST.finditer(flat):
        args,_=call_args(flat,m.end()-1)
        if not args:continue
        found.append(dict(method=m[1].upper(),url=url_of(args[0],flat),body=payload(args[1]) if len(args)>1 and m[1]!='Get' and m[1]!='Delete' else ''))
    for m in re.finditer(r'new\s+HttpRequestMessage\s*\(\s*HttpMethod\.(\w+)\s*,\s*(\$?@?"[^"]*"|\w+)',flat):
        found.append(dict(method=m[1].upper(),url=url_of(m[2],flat),body='JSON content set on the request' if 'Content =' in flat else ''))
    if not found and not seen:
        for name in dict.fromkeys(re.findall(r'\b([A-Z]\w+)\s*\(',flat)):
            if name in seen or name in {'Assert','Guid','Task'}:continue
            seen.add(name);inner=helper(c['source'],name)
            if inner and REQUEST.search(inner) or 'HttpRequestMessage' in inner:
                for r in requests(c,inner,seen):found.append(dict(r,via=name))
    return found


def role_of(c,expr,body):
    """Role named by a Given* helper, a BuiltInRoles argument or the variable that holds one."""
    expr=expr.strip();m=re.match(r'^(\w+)$',expr)
    if m:
        d=re.search(rf'\bvar\s+{re.escape(expr)}\s*=\s*([^;]+);',body)
        if d:expr=d[1]
    r=re.search(r'BuiltInRoles\.(\w+)',expr)
    if r:return r[1]
    g=re.match(r'^(?:_?\w+\.)?(Given\w+)\s*\(',expr)
    if g:
        r=re.search(r'BuiltInRoles\.(\w+)',helper(c['source'],g[1]))
        if r:return r[1]
    return None


def auth(c):
    """Header lines and precondition bullets describing who sends the request."""
    body=' '.join(c['body'].split());pre=[];headers=[]
    calls=[(m[1],call_args(body,m.end()-1)[0]) for m in re.finditer(r'\b(ClientFor)\s*\(',body)]
    if calls:
        headers.append('`Authorization: Bearer <valid_token>`')
        for _,args in calls[:2]:
            role=role_of(c,args[0],body) if args else None
            caps=[re.sub(r'^Capability\.','',a) for a in args[1:] if a.startswith('Capability.')]
            pre.append('- Authenticated user'+(f' has role `{role}`' if role else '')+(' and' if role and caps else '')+(f' has capability {", ".join(f"`{x}`" for x in caps)}' if caps else ' has no extra capability' if len(args)>1 or not role else '')+'.')
    elif re.search(r'Bearer|DefaultRequestHeaders\.Authorization|Headers\.Authorization',body):
        headers.append('`Authorization: Bearer <token issued in the test>`');pre.append('- A signed-in user with a valid access token.')
    elif (m:=re.search(r'\b(?:[\w.]+\.)?(\w*Client)\s*\(',body)) and m[1] not in {'CreateClient','CreateDefaultClient','NewClient'}:
        args,_=call_args(body,m.end()-1);inner=' '.join(helper(c['source'],m[1]).split())
        if re.search(r'ClientFor\s*\(|Bearer|Authorization',inner):
            headers.append('`Authorization: Bearer <valid_token>`')
            role=role_of(c,inner,'');caps=re.findall(r'Capability\.(\w+)',inner)
            who=f' ({args[0]})' if args and args[0] and not args[0].startswith(('null','default')) else ''
            pre.append(f'- Authenticated user{who}'+(f' has role `{role}`' if role else '')+(' and' if role and caps else '')+(f' has capability {", ".join(f"`{x}`" for x in dict.fromkeys(caps))}' if caps else '')+'.')
        else:pre.append('- No access token is sent (anonymous request).')
    elif re.search(r'\bNewClient\s*\(',body):
        args,_=call_args(body,re.search(r'\bNewClient\s*\(',body).end()-1)
        pre.append('- A test account exists'+(' ('+', '.join(simplify(a) for a in args if a)+')' if any(args) else '')+'.')
    elif re.search(r'\bCreate(?:Default)?Client\s*\(',body):pre.append('- No access token is sent (anonymous request).')
    for m in re.finditer(r'Headers\.(?:Add|TryAddWithoutValidation)\(\s*"([^"]+)"\s*,\s*([^)]+)\)',body):headers.append(f'`{m[1]}: {clip(simplify(m[2]),60)}`')
    if 'Cookie' in body and not any('Cookie' in h for h in headers):headers.append('`Cookie` header as set in the test')
    return headers,pre


def setup_line(c,s):
    """One bullet for an arranging statement, or None for client and scope plumbing."""
    if CLIENT.search(s) or re.search(r'CreateScope|CreateAsyncScope|GetRequiredService<|\bvar\s+\w+\s*=\s*\w+\.Db\(\)|^using\s+var\s+\w+\s*=\s*await\s+JsonOf',s):return None
    s=re.sub(r'^(?:await\s+)?(?:using\s+)?','',s)
    if re.match(r'^var\s+\w+\s*=\s*(?:Guid\.(?:CreateVersion7|NewGuid)|New\w*|Create\w*)\(\)$',s):return None
    g=re.match(r'^var\s+(\w+)\s*=\s*(?:await\s+)?(?:_?\w+\.)?(Given\w+)\s*\((.*)\)$',s)
    if g:
        role=role_of(c,g[2]+'('+g[3]+')','');extra=f' ({clip(simplify(g[3]),70)})' if g[3].strip() else ''
        if role:return f'- {g[1]}: a user with role `{role}` exists{extra}.'
        return f'- {g[1]}: prepared by `{g[2]}`{extra}.'
    seed=re.match(r'^var\s+(\w+)\s*=\s*(?:await\s+)?\w+\.Seed\w*\((.*)\)$',s)
    if seed:return '- Test data is seeded'+(f' ({clip(simplify(seed[2]),80)})' if seed[2].strip() else ' (meeting, users and agenda from the fixture)')+'.'
    if re.match(r'^var\s+\w+\s*=\s*new\s+\w+',s) and len(s)>120:return '- '+clip(simplify(re.sub(r'^var\s+(\w+)\s*=\s*new\s+(\w+).*$',r'\1: a new \2 is prepared',s)),120)+'.'
    a=re.match(r'^(?:var\s+)?([\w.\[\]"]+)\s*=(?![=>])\s*(.+)$',s)
    if a:return f'- `{clip(simplify(a[1]),60)}` = `{clip(simplify(a[2]),90)}`.'
    return f'- `{clip(simplify(s),140)}`.'


def split(c):
    """(setup, actions, has_asserts) from the statements before the first assertion."""
    st=statements(c['body']);first=next((i for i,s in enumerate(st) if 'Assert.' in s),len(st));before=st[:first]
    actions=[s for s in before if not SETUP.search(s) and not CLIENT.search(s) and re.search(r'\w\s*\(',s)]
    if not actions and before:actions=[before[-1]]
    setup=[s for s in before if s not in actions]
    return setup,actions,first<len(st)


def bullets(c,lines,limit=5):
    out=[];seen=set()
    for s in lines:
        b=setup_line(c,s)
        if b and b not in seen:seen.add(b);out.append(b)
    return out[:limit]+(['- Further arrangement is in the test source.'] if len(out)>limit else [])


def environment(c):
    r=c.get('reason','')
    if c['level']=='API':
        out=['- Backend is running (ASP.NET Core TestServer).']
        out.append('- PostgreSQL test database is available.' if 'PostgreSQL' in r else '- Data services are replaced by in-memory fakes.')
        return out
    if c['level']=='INTEGRATION':
        out=[]
        if 'PostgreSQL' in r:out.append('- PostgreSQL test database is available.')
        if 'Redis' in r:out.append('- Redis server is available.')
        if 'InMemory' in r:out.append('- EF Core InMemory database is used (not PostgreSQL).')
        if 'migration' in r:out.append('- The EF Core model and migrations are built.')
        if 'filesystem' in r:out.append('- The repository source files are readable.')
        return out or ['- '+r]
    return [f'- `{re.sub(r"Tests?$","",c["className"])}` runs in process; its dependencies are fakes or stubs.']


def description(c):
    words=c['method'].replace('_',' ').strip()
    head=words.split(' ',1)[0]
    words=words if len(head)>1 and head.isupper() else words[0].lower()+words[1:]
    return f'Verify {words}.'


def expected_lines(c,kinds=None):
    lines=[assertion(a) for a in c['assertions']]
    if kinds:lines=[x for x in lines if x[0] in kinds]
    return lines


def numbered(items,limit):
    out=items[:limit]
    if len(items)>limit:out.append(f'… and {len(items)-limit} more checks (see the test source).')
    return '\n'.join(f'{i}. {x}' for i,x in enumerate(out,1))


def endpoint(c):
    r=requests(c)
    if not r:return None
    return r[0]['method']+' '+re.sub(r'\{[^}]*\}','{id}',r[0]['url'].split('?')[0])


def table_fields(c):
    """(procedure, expected, precondition) in the numbered style of the API template."""
    if c.get('authoredText'):return plain(c['steps']),plain(c['expected']),plain(c['precondition'])
    if c.get('automation')=='Manual':return manual_fields(c)
    if c.get('id') in authored():
        a=authored()[c['id']]
        return plain(listed(a['procedure'])),plain(listed(a['expected'])),plain(listed(a['precondition']))
    setup,actions,_=split(c);reqs=requests(c);headers,who=auth(c);steps=[]
    pre=environment(c)+who+bullets(c,setup)
    if c.get('skip'):pre.append('- Skipped in source: '+c['skip'])
    if reqs:
        if len(reqs)==1:
            r=reqs[0];path,_,query=r['url'].partition('?')
            steps.append(f"Send {r['method']} request to {r['url']}.")
            steps.append('Request headers: '+'; '.join(headers) if headers else 'Request headers: None.')
            steps.append('Query parameters: '+'; '.join(query.split('&')) if query else 'Query parameters: None.')
            steps.append(f"Request body: {r['body']}" if r['body'] else f"Request body: None ({r['method']} request)." if r['method'] in {'GET','DELETE'} else 'Request body: None.')
        else:
            for r in reqs[:6]:steps.append(f"Send {r['method']} request to {r['url']}"+(f" with body {r['body']}" if r['body'] else '')+'.')
            if len(reqs)>6:steps.append(f'Send the remaining {len(reqs)-6} requests listed in the test source.')
            steps.append('Request headers: '+'; '.join(headers) if headers else 'Request headers: None.')
    else:
        if setup:steps.append('Prepare the data listed in Pre-conditions.')
        for s in actions[:3]:steps.append('Execute `'+clip(simplify(re.sub(VAR,'',s)),150)+'`.')
        if len(actions)>3:steps.append(f'Execute the remaining {len(actions)-3} steps in the test source.')
        steps.append('Check the results listed in Expected Results.')
    found=expected_lines(c);status=[t for k,t in found if k=='status'];rest=[]
    for k,t in found:
        if k=='status':continue
        rest.append(('Response JSON body: ' if k=='json' and reqs else 'Exception thrown: ' if k=='exception' else '')+t+('.' if not t.endswith('.') else ''))
    items=[s+'.' for s in status]+rest or ['The test completes without a failed assertion.']
    return plain(numbered(steps,9)),plain(numbered(items,9)),plain(listed(pre))


def plain(text):
    """Report cells carry no Markdown: code is written as is, without backticks."""
    return text.replace('`','')


def listed(lines):
    """Number every item of a list, as the templates do: '1. …', '2. …'."""
    items=[re.sub(r'^\s*[-•]\s*','',x) for x in lines if x.strip()]
    return '\n'.join(f'{i}. {x}' for i,x in enumerate(items,1))


def manual_fields(c):
    pre=[p.strip()[0].upper()+p.strip()[1:].rstrip('.')+'.' for p in re.split(r';\s*',c['precondition']) if p.strip()]
    expected=[p.strip().rstrip('.')+'.' for p in re.split(r';\s*',c['expected']) if p.strip()]
    return plain(c['steps']),plain('\n'.join(f'{i}. {x[0].upper()+x[1:]}' for i,x in enumerate(expected,1))),plain(listed(pre))


VAR=r'^(?:await\s+)?(?:using\s+)?(?:var\s+(?:\w+|\([^)]*\))\s*=\s*)?(?:await\s+)?'
LINQ={'Select','Where','Single','SingleOrDefault','First','FirstOrDefault','Last','Any','All','Count','OrderBy','OrderByDescending','ThenBy',
    'ToArray','ToList','ToDictionary','ToHashSet','Distinct','Take','Skip','SelectMany','GroupBy','Concat','Contains','Sum','Max','Min','Order',
    'AsEnumerable','ToString','Equals'}
def last_call(statement):
    """(name, args) of the last top-level, non-LINQ call in a statement's expression."""
    text=re.sub(VAR,'',statement);depth=0;i=0;best=None
    while i<len(text):
        ch=text[i]
        if ch in '"\'':i=literal_end(text,i);continue
        if ch=='(' and depth==0:
            m=re.search(r'(\w+)\s*(?:<[^<>()]*>)?\s*$',text[:i])
            if m and m[1] not in LINQ and m[1]!='new' and not re.search(r'\bnew\s+[\w.]*$',text[:m.start(1)]):best=(m[1],call_args(text,i)[0])
        if ch in '([{':depth+=1
        elif ch in ')]}':depth-=1
        i+=1
    return best


def unit_fields(c):
    """(precondition, call, expected) in the Return / Exception / Log Message style of the unit template."""
    setup,actions,_=split(c)
    results={n for s in actions for m in [re.match(r'^(?:using\s+)?var\s+(\w+|\([^)]*\))\s*=',s)] if m for n in re.findall(r'\w+',m[1])}
    pre=environment(c)+bullets(c,setup,4)
    if c.get('skip'):pre.append('- Skipped in source: '+c['skip'])
    call=''
    # The call under test: the first awaited action with a non-LINQ call, else the last such action.
    found=[(s,last_call(s)) for s in actions if not SETUP.search(s)];found=[x for x in found if x[1]]
    subject=re.sub(r'Tests?$','',c['className'])
    if not found:
        # Assert-only tests call the subject inside the assertion: Assert.Empty(Policy.Select(x)).
        for a in c['assertions']:
            flat=' '.join(a.split());m=re.search(rf'\b{re.escape(subject)}\.(\w+)\s*\(',flat) or re.search(r'\.(\w+Async)\s*\(',flat)
            if m:found=[(flat,(m[1],call_args(flat,m.end()-1)[0]))];results=set();break
    # Input parameters hold only the data handed to the call, as in the template (a=0; b=0).
    if found:
        _,(name,args)=next((x for x in found if 'await ' in x[0]),found[-1])
        call='; '.join(clip(simplify(x),70) for x in args if x) or 'N/A'
    else:call='N/A'
    returns=[];exceptions=[];logs=[];other=[]
    for a in c['assertions']:
        kind,target,value=result_of(a)
        if kind=='exception':exceptions.append(value)
        elif kind=='log':logs.append(value)
        elif not results or any(re.match(rf'{re.escape(r)}\b',target) for r in results):returns.append((target,value))
        else:other.append((target,value))
    # Return names only the result; side-effect checks stand in when nothing is returned.
    returns=returns or other
    def block(label,items,limit):
        if not items:return f'{label}: N/A'
        if len(items)==1:return f'{label}: {items[0]}'
        shown=items[:limit]+([f'… and {len(items)-limit} more'] if len(items)>limit else [])
        return f'{label}:\n'+'\n'.join(f'{i}. {x}' for i,x in enumerate(shown,1))
    values=[value if len(returns)==1 else f'{member(target,results)}: {value}' for target,value in returns]
    expected=[block('Return',values or ([] if exceptions else ['completes without error']),6),block('Exception',exceptions,2),block('Log Message',logs,2)]
    return plain(listed(pre)),plain(call),plain('\n'.join(expected))


def member(target,results):
    """result.Outcome -> Outcome; other targets keep their own name."""
    head,_,rest=target.partition('.')
    return rest if len(results)==1 and head in results and rest else target


def result_of(a):
    """(kind, target, expected value) of one assertion, for the Return line of a unit case."""
    a=' '.join(a.split());m=ASSERT.search(a)
    if not m:return 'check',a,clip(simplify(a),120)
    name,generic=m[1],m[2];args=[simplify(x) for x in call_args(a,m.end()-1)[0]]
    first=args[0] if args else '';second=args[1] if len(args)>1 else ''
    if name.startswith('Throws'):return 'exception','',(generic or 'an exception').split('.')[-1]
    blank=re.fullmatch(r'string\.IsNullOr(?:WhiteSpace|Empty)\((.*)\)',first)
    if blank and name in {'True','False'}:return 'check',blank[1],'blank' if name=='True' else 'not blank'
    value={'Equal':(second,first),'NotEqual':(second,'not '+first),'Same':(second,first),'NotSame':(second,'not '+first),
        'True':(first,'true'),'False':(first,'false'),'Null':(first,'null'),'NotNull':(first,'not null'),'Empty':(first,'empty'),
        'NotEmpty':(first,'not empty'),'Single':(first,'exactly one item'),'Contains':(second,'contains '+first),
        'DoesNotContain':(second,'does not contain '+first),'StartsWith':(second,'starts with '+first),'EndsWith':(second,'ends with '+first),
        'Matches':(second,'matches '+first),'InRange':(first,f'between {second} and {args[2] if len(args)>2 else "?"}'),
        'All':(first,'every item passes the check'),'IsType':(first,f'a {generic}'),'IsAssignableFrom':(first,f'a {generic}'),
        'Equivalent':(second,first),'Collection':(first,f'{len(args)-1} items in order')}.get(name,(first,clip(simplify(a),120)))
    target,result=value
    kind='log' if re.search(r'(?i)\blog|logger|entries',target) else 'check'
    return kind,target,clip(result,120)
