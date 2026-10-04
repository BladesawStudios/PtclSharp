import scan,numpy as np,sys
w=scan.w
def isgot(a):
    i=a//4; cur=int(w[i]); base=(cur>>5)&31
    for k in (1,2,3):
        p=int(w[i-k])
        if (p&0x9F000000)==0x90000000 and (p&31)==base: return True
    return False
LIB=(0x850,0x2f300)
def run(name,lo,hi):
    out=[]
    for base,lab in [(0,'data'),(0x70,'body')]:
        if lo-base<0: continue
        for a,o,wd,k in scan.hits(lo-base,hi-base):
            if LIB[0]<=a<LIB[1] and not isgot(a): out.append((a,o,wd,lab))
    print(f"{name} [{lo:X}-{hi:X}]: {len(out)}")
    for a,o,wd,lab in out: print(f"   0x71{a:08x} imm=0x{o:X} w={wd} base={lab}")
rows=[l.split() for l in sys.argv[1:]]
for r in rows: run(r[0],int(r[1],16),int(r[2],16))
