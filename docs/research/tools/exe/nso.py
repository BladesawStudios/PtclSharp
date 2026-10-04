import struct,lz4.block,sys
def load(p):
    d=open(p,'rb').read()
    assert d[:4]==b'NSO0'
    fl=struct.unpack_from('<I',d,0xC)[0]
    segs=[]
    for i in range(3):
        fo,mo,sz=struct.unpack_from('<III',d,0x10+i*0x10)
        cs=struct.unpack_from('<I',d,0x60+i*4)[0]
        raw=d[fo:fo+cs]
        segs.append((mo,lz4.block.decompress(raw,uncompressed_size=sz) if fl&(1<<i) else raw))
    return segs
if __name__=='__main__':
    for p in sys.argv[1:]:
        s=load(p); t=s[0][1]
        print(p,[(hex(m),hex(len(b))) for m,b in s], t[0xbbe4:0xbbe4+32].hex())
