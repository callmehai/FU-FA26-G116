"""The concrete value behind each argument a unit test hands to the code under test.

Report 5.1 lists the input of every case. Writing the C# argument (user.Email, _actorId) tells a
reader nothing, so each argument is followed back through the test source until it is a value:
locals and fields to their initialisers, helper calls (NewUser(), ValidInput(...)) through their
parameter defaults to the object they build, constants (BuiltInRoles.Member, Password) to their
declaration in backend/ or tests/. An id the test generates at run time (Guid.CreateVersion7())
has no fixed value; it is written as an example id marked "(generated)".

Like readable.py this is a heuristic over source text: whatever it cannot follow stays as the C#.
"""
import hashlib,re
from datetime import datetime,timedelta,timezone
from functools import lru_cache
from readable import ROOT,csharp_split,call_args,literal_end,source_text,statements,squash,clip,simplify

DEPTH=30
LITERAL=re.compile(r'''^(?:[@$]*"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)'|-?\d[\d_]*(?:\.\d+)?[mMdDfFlLuU]*|true|false|null|default)$''')
GENERATED=re.compile(r'^Guid\.(?:CreateVersion7|NewGuid)\(\s*\)$')
GUID=re.compile(r'^(?:new(?:\s+Guid)?|Guid\.Parse)\(\s*"([0-9a-fA-F-]{36})"\s*\)$')
DECLARED=r'(?:(?:private|internal|public|protected|static|readonly|const|required|override|new)\s+)+'


def example_id(seed):
    """A stable, version-7-shaped example for an id the test generates at run time."""
    h=hashlib.sha256(seed.encode()).hexdigest()
    return f"019{h[:5]}-{h[5:9]}-7{h[9:12]}-{'89ab'[int(h[12],16)%4]}{h[13:16]}-{h[16:28]} (generated)"


@lru_cache(maxsize=None)
def sources():
    """Every C# file a test can reach: the backend first, then the tests."""
    out=[]
    for top in ('backend','tests'):
        for p in sorted((ROOT/top).rglob('*.cs')):
            if not {'bin','obj','Migrations'}.intersection(p.parts):out.append(p.relative_to(ROOT).as_posix())
    return out


def owner(text,pos):
    found=list(re.finditer(r'\b(?:class|record|struct|interface)\s+(\w+)',text[:pos]))
    return found[-1][1] if found else ''


@lru_cache(maxsize=None)
def members():
    """(Type, Member) -> (initialiser, file) for consts, static fields and expression properties."""
    index={}
    pattern=re.compile(rf'(?m)^\s*{DECLARED}[\w<>\[\]?,. ]+?\s+(\w+)\s*(=>|=)(?!=)\s*')
    for path in sources():
        text=source_text(path)
        for m in pattern.finditer(text):
            init=initialiser(text,m.end())
            if init:index.setdefault((owner(text,m.start()),m[1]),(init,path))
    return index


def initialiser(text,start):
    """The expression starting at text[start], up to its top-level ';'."""
    depth=0;i=start
    while i<len(text):
        ch=text[i]
        if ch in '"\'':i=literal_end(text,i);continue
        if text.startswith('//',i):
            j=text.find('\n',i);i=len(text) if j<0 else j;continue
        if ch in '([{':depth+=1
        elif ch in ')]}':
            depth-=1
            if depth<0:return ''
        elif ch==';' and depth==0:return squash(text[start:i])
        i+=1
    return ''


@lru_cache(maxsize=None)
def field(path,name):
    """Initialiser of a field, const or expression property declared in a test file."""
    text=source_text(path)
    m=re.search(rf'(?m)^\s*{DECLARED}([\w<>\[\]?,. ]+?)\s+{re.escape(name)}\s*(=>|=)(?!=)\s*',text)
    return (initialiser(text,m.end()),m[1].split()[-1]) if m else ('','')




@lru_cache(maxsize=None)
def method(name,arity,prefer='',cls=''):
    """The declaration of a method or record that accepts `arity` arguments, as a dict:
    params [(type, name, default)], body (the returned expression when the method is
    expression-bodied or a single return, else None), ret (declared type) and path.
    The class under test's own file wins, then `prefer`'s file and folder, then the backend."""
    own={cls+'.cs','I'+cls+'.cs'} if cls else set()
    files=sorted(sources(),key=lambda f:(f.rsplit('/',1)[-1] not in own,f!=prefer,f.rsplit('/',1)[0]!=prefer.rsplit('/',1)[0],not f.startswith('backend'),f))
    for path in files:
        text=source_text(path)
        if name not in text:continue
        for m in re.finditer(rf'(?m)^[ \t]*(?:(?:{MODIFIERS})\s+)*[\w<>\[\]?,.() ]+?\s+{re.escape(name)}\s*(?:<[^<>()]*>)?\s*\(',text):
            head=text[m.start():m.end()];prefix=head[:head.rindex(name)]
            if re.search(r'\b(?:new|return|await|var|if|while|switch|throw|yield|using|else|case)\b|[=(]|\.\s*$',prefix):continue
            args,end=call_args(text,m.end()-1)
            params=[]
            for p in args:
                if not p:continue
                p=re.sub(r'^\[[^\]]*\]\s*','',p);decl,_,default=p.partition('=');words=decl.split()
                if len(words)<2:break
                params.append((' '.join(w for w in words[:-1] if w not in {'this','params','ref','in','out'}),words[-1],default.strip() or None))
            else:
                need=sum(1 for p in params if p[2] is None)
                if not need<=arity<=len(params):continue
                rest=text[end:].lstrip();rest=re.sub(r'^where\b[^{=]*','',rest)
                if rest.startswith('=>'):body=initialiser(text,end+text[end:].index('=>')+2)
                elif rest.startswith('{'):
                    block=statements(rest[:block_end(rest)])
                    body=block[0][7:] if len(block)==1 and block[0].startswith('return ') else None
                else:body=None
                ret=' '.join(w for w in prefix.split() if not re.fullmatch(MODIFIERS,w))
                return dict(params=params,body=body,ret=ret,path=path)
    return None


MODIFIERS='private|internal|public|protected|static|async|override|virtual|sealed|abstract|partial|readonly'


def block_end(text):
    depth=0;i=0
    while i<len(text):
        ch=text[i]
        if ch in '"\'':i=literal_end(text,i);continue
        if ch=='{':depth+=1
        elif ch=='}':
            depth-=1
            if depth==0:return i+1
        i+=1
    return len(text)


class Env:
    """Where a name is looked up: bound parameters, then the locals before the call, then the file's fields."""
    def __init__(self,path,stmts=(),bound=None,seed=''):
        self.path=path;self.stmts=list(stmts);self.bound=bound or {};self.seed=seed

    def lookup(self,name):
        """(initialiser, env it is read in, declared type) of a name, or (None, None, '')."""
        if name in self.bound:
            expr,env=self.bound[name][:2];kind=self.bound[name][2] if len(self.bound[name])>2 else ''
            return expr,env or Env(self.path,seed=self.seed),kind
        for s in reversed(self.stmts):
            m=re.match(rf'^(?:await\s+)?(?:using\s+)?(var|[\w<>\[\]?.]+)\s+{re.escape(name)}\s*=(?![=>])\s*(.+)$',s)
            if m:return m[2],self,'' if m[1]=='var' else m[1]
        init,kind=field(self.path,name)
        if init:return init,Env(self.path,seed=self.seed),kind
        return None,None,''


def bind(params,args,env,where):
    """Parameter name -> (argument expression, env): positional, named, then defaults read in `where`."""
    bound={};position=0
    for a in args:
        if not a:continue
        named=re.match(r'^(\w+)\s*:(?!:)\s*(.+)$',a)
        if named and any(p[1]==named[1] for p in params):bound[named[1]]=(named[2],env,next(p[0] for p in params if p[1]==named[1]))
        elif position<len(params):bound[params[position][1]]=(a,env,params[position][0]);position+=1
    for kind,name,default in params:
        if name not in bound and default is not None:bound[name]=(default,Env(where,seed=env.seed),kind)
    return bound


# A date is carried as ISO text (2026-11-06T02:00:00Z) so a later AddHours or ToString can use it.
ISO=re.compile(r'^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(Z|[+-]\d\d:\d\d)$')
NOW=datetime.now(timezone.utc).replace(second=0,microsecond=0)
RELATIVE=[False]


def to_dt(s):
    m=ISO.match(s or '')
    return datetime.fromisoformat(m[1]+('+00:00' if m[2]=='Z' else m[2])) if m else None


def from_dt(d):
    off=d.utcoffset() or timedelta(0)
    if not off:return d.strftime('%Y-%m-%dT%H:%M:%SZ')
    minutes=int(off.total_seconds()//60);sign='+' if minutes>=0 else '-'
    return d.strftime('%Y-%m-%dT%H:%M:%S')+f'{sign}{abs(minutes)//60:02}:{abs(minutes)%60:02}'


def number(s):
    try:return float(re.sub(r'[mMdDfFlL]$','',s.replace('_','')))
    except ValueError:return None


def offset(s):
    if s in {'TimeSpan.Zero','default'}:return timezone.utc
    m=re.fullmatch(r'TimeSpan\.FromHours\((-?\d+)\)',s or '')
    return timezone(timedelta(hours=int(m[1]))) if m else None


def top_level(e,token):
    """Index of the last top-level occurrence of token in e, or -1."""
    depth=0;i=0;found=-1
    while i<len(e):
        ch=e[i]
        if ch in '"\'':i=literal_end(e,i);continue
        if ch in '([{':depth+=1
        elif ch in ')]}':depth-=1
        elif depth==0 and e.startswith(token,i):found=i;i+=len(token);continue
        i+=1
    return found


def ternary(e):
    """(condition, when true, when false) of a top-level conditional expression, or None."""
    depth=0;i=0;q=-1;nested=0
    while i<len(e):
        ch=e[i]
        if ch in '"\'':i=literal_end(e,i);continue
        if ch in '([{':depth+=1
        elif ch in ')]}':depth-=1
        elif depth==0 and ch=='?' and e[i+1:i+2] not in {'?','.','['} and e[i-1:i]!='?':
            if q<0:q=i
            else:nested+=1
        elif depth==0 and ch==':' and q>=0 and e[i+1:i+2]!=':':
            if nested:nested-=1
            else:return e[:q].strip(),e[q+1:i].strip(),e[i+1:].strip()
        i+=1
    return None


def tail(e):
    """('call', receiver, name, args) or ('member', receiver, name) for the last top-level access of e."""
    depth=0;i=0;dot=-1
    while i<len(e):
        ch=e[i]
        if ch in '"\'':i=literal_end(e,i);continue
        if ch in '([{':depth+=1
        elif ch in ')]}':depth-=1
        elif ch=='<' and re.match(r'[A-Z]',e[i+1:i+2] or ' ') and i and (e[i-1].isalnum() or e[i-1]=='_'):depth+=1
        elif ch=='>' and depth and e[i-1]!='=':depth-=1
        elif ch=='.' and depth==0 and re.match(r'[A-Za-z_]',e[i+1:i+2] or ' ') and not e[i-1:i].isdigit():dot=i
        i+=1
    receiver,rest=(e[:dot].rstrip('?'),e[dot+1:]) if dot>0 else (None,e)
    if receiver is not None and re.fullmatch(r'\w+',rest):return 'member',receiver,rest
    m=re.match(r'^([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(',rest)
    if m:
        args,end=call_args(rest,m.end()-1)
        if end==len(rest):return 'call',receiver,m[1],[a for a in args if a]
    return None


def resolve(expr,env,depth=0,name='',hint=''):
    """The value of a C# expression, written for a reader."""
    e=squash(expr)
    if depth>DEPTH or not e:return e
    e=re.sub(r'^await\s+','',e).rstrip('!')
    while e.startswith('(') and call_args(e,0)[1]==len(e) and len(call_args(e,0)[0])==1:e=e[1:-1].strip()
    if LITERAL.match(e) or e.startswith('"""') or e.startswith('$"') and '{' not in e:return e
    if GENERATED.match(e):return example_id(env.seed+'|'+(name or e))
    g=GUID.match(e)
    if g:return g[1]
    sw=re.fullmatch(r'(.+?)\s+switch\s*\{(.*)\}',e)
    if sw:
        subject=resolve(sw[1],env,depth+1)
        for arm in csharp_split(sw[2]):
            pattern,_,value=arm.partition('=>')
            if pattern.strip() in {'_',subject}:return resolve(value,env,depth+1,name,hint)
    t=ternary(e)
    if t:
        cond=resolve(t[0],env,depth+1)
        eq=re.fullmatch(r'(.+?)\s*(==|!=)\s*(.+)',t[0])
        if eq:
            left,right=resolve(eq[1],env,depth+1),resolve(eq[3],env,depth+1)
            if LITERAL.match(left) and LITERAL.match(right):cond='true' if (left==right)==(eq[2]=='==') else 'false'
        if cond in {'true','false'}:return resolve(t[1] if cond=='true' else t[2],env,depth+1,name,hint)
    q=top_level(e,'??')
    if q>0:
        left=resolve(e[:q],env,depth+1,name,hint)
        return resolve(e[q+2:],env,depth+1,name,hint) if left=='null' else left
    s=re.fullmatch(r"new\s+string\(\s*('(?:[^'\\]|\\.)')\s*,\s*(.+)\)",e)
    if s:return f'{s[1]} × {resolve(s[2],env,depth+1)}'
    parts=csharp_split(e,'+')
    if len(parts)>1 and all(parts):return ' + '.join(resolve(p,env,depth+1) for p in parts)
    if e.startswith('[') and e.endswith(']'):
        return '['+', '.join(resolve(x,env,depth+1) for x in csharp_split(e[1:-1]) if x)+']'
    if re.fullmatch(r'new\s*[\w.]*\[\]\s*\{.*\}',e):
        return '['+', '.join(resolve(x,env,depth+1) for x in csharp_split(e[e.index('{')+1:e.rindex('}')]) if x)+']'
    if re.fullmatch(r'Array\.Empty<.*>\(\)',e):return '[]'
    nm=re.fullmatch(r'nameof\((?:\w+\.)*(\w+)\)',e)
    if nm:return f'"{nm[1]}"'
    if re.fullmatch(r'\w+',e):
        init,where,kind=env.lookup(e)
        return resolve(init,where,depth+1,e,kind or hint) if init is not None else e
    w=re.fullmatch(r'(.+?)\s+with\s*\{(.*)\}',e)
    if w:
        fields=[(f.partition('=')[0].strip(),f.partition('=')[2].strip()) for f in csharp_split(w[2]) if f.strip()]
        return render(resolve(w[1],env,depth+1,name,hint)+' with',fields,env,depth)
    if re.match(r'^new\s*[\w.<>?]*\s*[({]',e):return construct(e,env,depth,hint)
    t=tail(e)
    if not t:return e
    if t[0]=='member':
        _,receiver,prop=t
        if re.fullmatch(r'(?:DateTime(?:Offset)?|TimeProvider\.System)',receiver) and prop in {'UtcNow','Now'}:
            RELATIVE[0]=True;return from_dt(NOW)
        if re.fullmatch(r'\w+',receiver):
            init,where,kind=env.lookup(receiver)
            if init is not None:
                value=property_of(init,where,prop,depth+1,receiver,kind)
                if value is not None:return value
                if prop=='Id':return example_id(env.seed+'|'+receiver+'.Id')
        hit=members().get((receiver.split('.')[-1],prop))
        if hit:return resolve(hit[0],Env(hit[1],seed=env.seed),depth+1,prop)
        value=resolve(receiver,env,depth+1);d=to_dt(value)
        if d and prop=='Date':return from_dt(d.replace(hour=0,minute=0,second=0))
        if d and prop=='UtcDateTime':return from_dt(d.astimezone(timezone.utc))
        return f'{value}.{prop}' if value!=receiver else e
    _,receiver,called,args=t
    if receiver in {None,'this'}:
        found=method(called,len(args),env.path)
        if found and found['body']:
            scope=Env(found['path'],bound=bind(found['params'],args,env,found['path']),seed=env.seed)
            return resolve(found['body'],scope,depth+1,name,found['ret'])
        return f"{called}({', '.join(resolve(a,env,depth+1) for a in args)})"
    value=resolve(receiver,env,depth+1);d=to_dt(value)
    if d:
        add=re.fullmatch(r'Add(Days|Hours|Minutes|Seconds|Milliseconds)',called)
        n=number(resolve(args[0],env,depth+1)) if args else None
        if add and n is not None:return from_dt(d+timedelta(**{add[1].lower():n}))
        if called=='ToString':return f'"{from_dt(d)}"'
        if called=='ToUniversalTime':return from_dt(d.astimezone(timezone.utc))
        if called=='ToOffset' and args and offset(args[0]):return from_dt(d.astimezone(offset(args[0])))
    shown=', '.join(resolve(a,env,depth+1) for a in args)
    if ' { ' in value and len(value)>40:return f'{called}({shown})'
    return f"{value}.{called}({shown})"


def clean(kind):
    kind=re.sub(r'\?$','',kind.strip())
    m=re.fullmatch(r'(?:Task|ValueTask)<(.+)>',kind)
    return clean(m[1]) if m else kind


def property_of(init,env,prop,depth,head,hint=''):
    """Value of `head.prop` when head was initialised with `init`, or None when it cannot be read."""
    e=re.sub(r'^await\s+','',squash(init))
    if depth>DEPTH:return None
    if re.fullmatch(r'\w+',e):
        nxt,where,kind=env.lookup(e)
        return property_of(nxt,where,prop,depth+1,head,kind) if nxt is not None else None
    w=re.fullmatch(r'(.+?)\s+with\s*\{(.*)\}',e)
    if w:
        for f in csharp_split(w[2]):
            k,_,v=f.partition('=')
            if k.strip()==prop:return resolve(v,env,depth+1)
        return property_of(w[1],env,prop,depth+1,head,hint)
    if re.match(r'^new\s*[\w.<>?]*\s*[({]',e):
        fields,_=object_fields(e,env,hint)
        for k,v in fields:
            if k==prop:return resolve(v,env,depth+1,head+'.'+prop)
        return None
    t=tail(e)
    if t and t[0]=='call' and t[1] in {None,'this'}:
        found=method(t[2],len(t[3]),env.path)
        if found and found['body']:
            scope=Env(found['path'],bound=bind(found['params'],t[3],env,found['path']),seed=env.seed)
            return property_of(found['body'],scope,prop,depth+1,head,found['ret'])
    return None


def object_fields(e,env,hint=''):
    """([(name, expression)], type) set by a `new` expression: record arguments by parameter name, then initialiser."""
    n=re.match(r'^new\s*([\w.<>?]*)\s*',e);kind=n[1] or clean(hint);rest=e[n.end():];fields=[]
    if rest.startswith('('):
        args,end=call_args(rest,0);args=[a for a in args if a];rest=rest[end:].strip()
        short=re.sub(r'<.*$','',kind.split('.')[-1])
        found=method(short,len(args),env.path) if short[:1].isupper() else None
        names=[p[1] for p in found['params']] if found else []
        position=0
        for a in args:
            named=re.match(r'^(\w+)\s*:(?!:)\s*(.+)$',a)
            if named:fields.append((named[1],named[2]))
            else:fields.append((names[position] if position<len(names) else '',a));position+=1
        if found:
            given={k for k,_ in fields}
            fields+=[(p[1],p[2]) for p in found['params'] if p[1] not in given and p[2] is not None]
    if rest.startswith('{'):
        for f in csharp_split(rest[1:rest.rfind('}')]):
            k,eq,v=f.partition('=')
            if eq and re.fullmatch(r'\s*\w+\s*',k) and not v.startswith(('=','>')):fields.append((k.strip(),v.strip()))
            elif f.strip():fields.append(('',f.strip()))
    return fields,kind


def construct(e,env,depth,hint=''):
    fields,kind=object_fields(e,env,hint)
    if re.fullmatch(r'(?:System\.)?DateTime(?:Offset)?',kind):
        values=[resolve(v,env,depth+1) for _,v in fields]
        if values and to_dt(values[0]):
            zone=offset(values[1]) if len(values)>1 else None
            d=to_dt(values[0]);return from_dt(d.replace(tzinfo=zone) if zone else d)
        nums=[number(v) for v in values[:6]]
        if len(nums)>=3 and all(x is not None for x in nums):
            parts=[int(x) for x in nums]+[0]*(6-len(nums));zone=offset(values[6]) if len(values)>6 else timezone.utc
            return from_dt(datetime(*parts[:6],tzinfo=zone or timezone.utc))
    return render(kind,fields,env,depth)


def render(kind,fields,env,depth):
    shown=[(f'{k} = ' if k else '')+resolve(v,env,depth+1,k) for k,v in fields]
    body='{ '+', '.join(shown)+' }' if shown else '{ }'
    return f'{kind} {body}' if kind else body


def labelled(name,args,env,cls='',limit=200):
    """'param = value' lines for the arguments of the call under test."""
    args=[a for a in args if a and not re.fullmatch(r'out\s+(?:var\s+)?\w+',a)]
    found=method(name,len(args),env.path,cls) if name else None
    params=found['params'] if found else []
    lines=[];position=0
    for a in args:
        named=re.match(r'^(\w+)\s*:(?!:)\s*(.+)$',a);kind=''
        if named:label,a=named[1],named[2];kind=next((p[0] for p in params if p[1]==label),'')
        else:
            p=params[position] if position<len(params) else None;position+=1
            if p and 'CancellationToken' in p[0]:continue
            label,kind=(p[1],p[0]) if p else ('','')
        if a in {'CancellationToken.None','default(CancellationToken)'} or re.fullmatch(r'(?:\w+\.)?(?:ct|cancellationToken)|\w+\.Token',a):continue
        value=resolve(a,env,0,label or a,kind)
        # A multi-line raw string (a JSON body) is written on one line, like the rest of the cell.
        value=clip(simplify(' '.join(value.split()) if '\n' in value else value),limit)
        if value==label:continue  # a fixture handed straight through (f = f) says nothing
        lines.append(f'{label} = {value}' if label else value)
    return lines


def case_inputs(c):
    """One Input parameters cell per test case (each InlineData row of a Theory, or the Fact), in
    'parameter = value' lines with every argument followed back to its value."""
    from readable import unit_call
    names=[re.sub(r'\s*=.*$','',p,flags=re.S).split()[-1] for p in csharp_split(c['parameters'].strip()[1:-1]) if p.split()]
    found=unit_call(c);body=statements(c['body']);cls=re.sub(r'Tests?$','',c['className'])
    stmts=body[:next((i for i,s in enumerate(body) if found[0] in s),len(body))] if found else []
    rows=[]
    for d in c['data'] or [None]:
        bound={};raw=''
        if d:
            kind,_,inner=d.partition('(');inner=inner[:-1] if inner.endswith(')') else inner
            if kind!='InlineData':rows.append(f'Every row of {kind}({inner})');continue
            values=csharp_split(inner);raw=inner
            if len(values)==len(names):bound={n:(v,None) for n,v in zip(names,values)}
        RELATIVE[0]=False
        env=Env(c['source'],stmts,bound,c['id']+('|'+raw if raw else ''))
        lines=labelled(found[1],found[2],env,cls) if found else []
        # A Theory value that reaches the code under test some other way than the call's
        # arguments (a fake, a clock) is still an input; one used only by the asserts is not.
        before=' '.join(stmts)+' '+(found[0] if found else '')
        for n,(v,_) in bound.items():
            if re.search(rf'\b{re.escape(n)}\b',before) and not any(v in l for l in lines):lines.append(f'{n} = {v}')
        if not lines and bound:lines=[f'{n} = {v}' for n,(v,_) in bound.items()]
        if not lines and raw:lines=[raw]
        if RELATIVE[0]:lines.append(f'(times counted from the run date, here {NOW:%d/%m/%Y %H:%M} UTC)')
        rows.append('\n'.join(lines) or 'N/A')
    return rows
