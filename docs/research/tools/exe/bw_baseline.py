"""Per-row evidence baseline for a game's EMTR map: corpus zero/distinct, GPU reads, CPU immediate hits in the library.
usage (BotW): TOTK_MAIN_NSO=<main> PTCL_LIB=ac8000,af2000 python bw_baseline.py <rows.json> <emtr.bin> <used.json> <size>"""
import sys,json,struct,collections,re,os
import numpy as np,audit,scan
sys.path.insert(0,os.path.join(os.path.dirname(__file__),'..','gen'))
from parse_maps import parse,elem
rows=parse(sys.argv[1],sys.argv[5] if len(sys.argv)>5 else 'botw')
size=int(sys.argv[4],0)
d=open(sys.argv[2],'rb').read();i=0;blobs=[]
while i<len(d):
    ln=struct.unpack_from('<i',d,i)[0]; i+=4+64+4; blobs.append(d[i:i+ln]); i+=ln
A=np.frombuffer(b''.join(b[:size] for b in blobs if len(b)>=size),dtype=np.uint8).reshape(-1,size); n=A.shape[0]
u=json.load(open(sys.argv[3]))['used']; gpu={}
for k,v in u.items():
    b,o=k.split(':')
    if b=='sysEmitterStaticUniformBlock': gpu[int(o,16)]=v
print('emitters',n)
for r in rows:
    a=r['offset'];sz=r['size'] or 4;b=a+sz-1
    seg=A[:,a:b+1]; z=not seg.any(); dist=len(set(map(bytes,seg[::max(1,n//2000)])))
    g=sorted(o for o in gpu if a<=o<=b)
    c=[h for h in scan.hits(a,b) if audit.LIB[0]<=h[0]<audit.LIB[1] and not audit.isgot(h[0])]
    print(f"{a:04X} {sz:4d} {r['name'][:34]:34s} zero={'Y' if z else 'n'} dist={dist:4d} gpu={''.join(sorted(set(''.join(gpu[o] for o in g)))) or '-':2s} cpu={len(c)}")
