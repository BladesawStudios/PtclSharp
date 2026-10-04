import os
EXE=os.environ.get("TOTK_MAIN_NSO",r"C:/Users/dylan/shenanigans/Personal/Nintendo/Switch/Zelda TotK/RE/Exefs121/main")
import sys,nso,capstone
t=nso.load(EXE)[0][1]
md=capstone.Cs(capstone.CS_ARCH_ARM64,capstone.CS_MODE_ARM)
def d(a,before=14,after=8):
    s=a-before*4
    for i in md.disasm(t[s:a+after*4],s):
        print(("=> " if i.address==a else "   ")+f"{i.address:x}: {i.mnemonic} {i.op_str}")
    print('--')
if __name__=='__main__':
    for x in sys.argv[1:]: d(int(x,16))
