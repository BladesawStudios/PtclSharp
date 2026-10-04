import os
EXE=os.environ.get("TOTK_MAIN_NSO",r"C:/Users/dylan/shenanigans/Personal/Nintendo/Switch/Zelda TotK/RE/Exefs121/main")
import numpy as np,sys,nso
B=EXE
t=nso.load(B)[0][1]
w=np.frombuffer(t[:len(t)//4*4],dtype='<u4')
def hits(lo,hi):
    """accesses (unsigned-imm ldr/str and ldp/stp, any width) whose byte range intersects [lo,hi]"""
    res=[]
    imm12=(w>>10)&0xFFF
    size=(w>>30)&3
    v=(w>>26)&1
    opc=(w>>22)&3
    base=(w&0x3B000000)==0x39000000
    # scalar & vector: width bytes
    width=np.where(v==1,np.where((size==0)&(opc>=2),16,1<<size),1<<size)
    shift=np.where(v==1,np.where((size==0)&(opc>=2),4,size),size)
    off=imm12<<shift.astype(np.uint32)
    m=base&(off<=hi)&(off+width>lo)
    for i in np.nonzero(m)[0]: res.append((int(i)*4,int(off[i]),int(width[i]),'ldst'))
    # pairs: 0x28000000 class (stp/ldp): bits 29:27=101, 25:23 = 010(offset),  
    pair=((w&0x3A000000)==0x28000000)&(((w>>23)&7)==2)
    psz=(w>>30)&3
    pv=(w>>26)&1
    pw=np.where(pv==1,np.array([4,8,16,0])[psz],np.where(psz==2,8,np.where(psz==0,4,0)))
    pw=pw.astype(np.int64)
    imm7=((w>>15)&0x7F).astype(np.int64); imm7=np.where(imm7>=64,imm7-128,imm7)
    poff=imm7*pw
    m=pair&(pw>0)&(poff<=hi)&(poff+2*pw>lo)&(poff>=0)
    for i in np.nonzero(m)[0]: res.append((int(i)*4,int(poff[i]),int(2*pw[i]),'pair'))
    # add/sub imm
    return sorted(res)
if __name__=='__main__':
    lo=int(sys.argv[1],16);hi=int(sys.argv[2],16) if len(sys.argv)>2 else lo
    r=hits(lo,hi)
    print(len(r))
    for a,o,wd,k in r: print(f"0x71{a:08x} off=0x{o:X} w={wd} {k}")
