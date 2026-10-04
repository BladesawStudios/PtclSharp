import os
EXE=os.environ.get("TOTK_MAIN_NSO",r"C:/Users/dylan/shenanigans/Personal/Nintendo/Switch/Zelda TotK/RE/Exefs121/main")
import sys,nso,capstone,re,collections
t=nso.load(EXE)[0][1]
md=capstone.Cs(capstone.CS_ARCH_ARM64,capstone.CS_MODE_ARM); md.detail=False
def run(a,b,maxoff=0x200):
    d=collections.defaultdict(lambda: collections.defaultdict(list))
    for i in md.disasm(t[a:b],a):
        m=re.match(r'(ldr|ldrb|ldrh|ldrsw|ldur|ldp|str|strb|strh|stp)\s+([^,]+),(?:\s*([^,\[]+),)?\s*\[(\w+)(?:,\s*#(-?0x[0-9a-f]+|-?\d+))?\]',i.mnemonic+' '+i.op_str)
        if m:
            reg=m.group(4); off=int(m.group(5),0) if m.group(5) else 0
            if 0<=off<maxoff: d[reg][off].append((hex(i.address),i.mnemonic))
    return d
if __name__=='__main__':
    a=int(sys.argv[1],16); b=int(sys.argv[2],16)
    d=run(a,b)
    for reg,offs in sorted(d.items()):
        print(reg,' '.join(f"{o:X}({offs[o][0][1]})" for o in sorted(offs)))
