"""Parses the byte-map tables of the research docs into field rows (shared by the C# layout generator)."""
import re,sys,io

def _int(t):
    t=t.strip().strip('`').replace(' ','')
    return int(t,0)

def parse(path,game):
    L=open(path,encoding='utf-8').read().split('\n')
    start=next(i for i,l in enumerate(L) if re.match(r'## (2|3)\. (Byte-by-Byte|Byte-by-byte)',l,re.I) or 'Byte-by-Byte' in l and l.startswith('## '))
    end=next((i for i,l in enumerate(L) if i>start and l.startswith('## ')),len(L))
    rows=[]
    for l in L[start:end]:
        if not l.startswith('| `0x'): continue
        c=[x.strip() for x in l.strip().strip('|').split('|')]
        first=c[0]
        offs=re.findall(r'0x([0-9A-Fa-f]+)',first)
        a=int(offs[0],16)
        if game=='totk':
            size_s,typ,name,ev=c[3],c[4],c[5],c[6]
        else:
            size_s,typ,name,ev=c[1],c[2],c[3],c[4]
        name=name.strip('`').strip(); typ=typ.strip('`').strip()
        try: size=_int(size_s)
        except Exception: size=None
        if len(offs)>1 and size is None: size=int(offs[1],16)+1-a
        rows.append(dict(offset=a,size=size,type=typ,name=name,evidence=ev))
    return rows

def elem(typ):
    """returns (FieldType, count) or None"""
    m=re.fullmatch(r'(uint8|int8|uint16|int16|uint32|int32|uint64|int64|float)(?:\[(\d+)\])?(?:\[(\d+)\])?',typ)
    if m:
        t={'uint8':'U8','int8':'I8','uint16':'U16','int16':'I16','uint32':'U32','int32':'I32','uint64':'U64','int64':'I64','float':'F32'}[m.group(1)]
        n=1
        if m.group(2): n*=int(m.group(2))
        if m.group(3): n*=int(m.group(3))
        return t,n
    m=re.fullmatch(r'bytes(?:\[(0x[0-9A-Fa-f]+|\d+)\])?',typ)
    if m: return 'Bytes',int(m.group(1),0) if m.group(1) else None
    m=re.fullmatch(r'char\[(\d+)\]',typ)
    if m: return 'String',int(m.group(1))
    return None

if __name__=='__main__':
    base=sys.argv[1]
    for g,f in (('totk','totk-emtr-offsets-ghidra.md'),('botw','botw-emtr-offsets-ghidra.md')):
        rows=parse(base+'/'+f,g)
        print(g,len(rows))
        bad=[r for r in rows if elem(r['type']) is None or r['size'] is None]
        print(' unparsed',[(hex(r['offset']),r['type'],r['size']) for r in bad][:10])
        # overlaps
        rs=sorted(rows,key=lambda r:(r['offset'],r['size'] or 0))
        ov=[]
        for a,b in zip(rs,rs[1:]):
            if a['offset']+(a['size'] or 0)>b['offset']: ov.append((hex(a['offset']),a['name'],hex(a['size'] or 0),hex(b['offset']),b['name']))
        print(' overlaps',len(ov)); 
        for o in ov[:40]: print('  ',o)
