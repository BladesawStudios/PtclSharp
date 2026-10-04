"""Finds the values EmitterResource::UpdateParams copies from attribute chunks into the EMTR block (runtime overlays).

Tracks, linearly: `ldr xR, [x19, #slot]` (chunk pointer) -> `ldr/ldrb Y, [xR, #off]` (member) -> conversions -> `str Y, [xB, #dst]`.
Reports (chunk slot, member offset, width) -> destination offsets. x19 is the EmitterResource in this function.

usage: python overlays.py [start_hex end_hex]   (defaults: UpdateParams 0x710000bbe4..0x710000e388, as image offsets)
"""
import re
import sys

import armdis

START, END = 0xBBE4, 0xE388
SLOTS = {0x2F8: 'FRND', 0x300: 'FRN1', 0x308: 'FMAG', 0x310: 'FSPN', 0x318: 'FCOL', 0x320: 'FCOV', 0x328: 'FPAD',
         0x330: 'FCLN', 0x338: 'FGWD', 0x340: 'FCSF', 0x348: 'EAES', 0x350: 'EAER', 0x358: 'EAET', 0x360: 'EAC0',
         0x368: 'EAC1', 0x370: 'EATR', 0x378: 'EAPL', 0x380: 'EAA0', 0x388: 'EAA1', 0x390: 'EAOV', 0x398: 'EADV',
         0x3A0: 'EASL', 0x3A8: 'EASS', 0x3B0: 'EAGV', 0x3B8: 'CSDP', 0x3C0: 'CADP', 0x3C8: 'CUDP'}
MEM = re.compile(r'\[(x\d+|sp)(?:, #(-?0x[0-9a-f]+|-?\d+))?\]')


def run(start=START, end=END):
    ptr = {}   # reg -> slot name
    val = {}   # reg -> (slot, offset, mnemonic) for loaded values
    out = []
    for i in armdis.md.disasm(armdis.t[start:end], start):
        mn, op = i.mnemonic, i.op_str
        parts = [p.strip() for p in op.split(',')]
        m = MEM.search(op)
        if mn == 'ldr' and m and m.group(1) == 'x19' and parts[0].startswith('x'):
            off = int(m.group(2), 0) if m.group(2) else 0
            if off in SLOTS:
                ptr[parts[0]] = SLOTS[off]
                continue
        if mn in ('ldr', 'ldrb', 'ldrh', 'ldrsw', 'ldp') and m and m.group(1) in ptr:
            off = int(m.group(2), 0) if m.group(2) else 0
            dst = parts[0]
            val[dst] = (ptr[m.group(1)], off, mn)
            if mn == 'ldp':
                val[parts[1]] = (ptr[m.group(1)], off + 4, mn)
            continue
        if mn in ('ucvtf', 'scvtf', 'fcvtzs', 'fcvtzu', 'mov', 'fmov', 'fmul', 'fadd', 'fsub') and len(parts) >= 2 and parts[1] in val:
            val[parts[0]] = val[parts[1]] + (mn,) if len(val[parts[1]]) == 3 else val[parts[1]]
            continue
        if mn in ('str', 'strb', 'strh', 'stur', 'stp') and m and parts[0] in val:
            off = int(m.group(2), 0) if m.group(2) else 0
            out.append((m.group(1), off, val[parts[0]], i.address, mn))
            if mn == 'stp' and parts[1] in val:
                out.append((m.group(1), off + 4, val[parts[1]], i.address, mn))
            continue
        # a write to a tracked register by something else invalidates it
        d = parts[0]
        if d in val and not mn.startswith(('str', 'cmp', 'cbz', 'cbnz', 'tbz', 'tbnz', 'b')):
            del val[d]
        if d in ptr and not mn.startswith(('str', 'cmp', 'cbz', 'cbnz', 'tbz', 'tbnz', 'b')):
            del ptr[d]
    return out


if __name__ == '__main__':
    s = int(sys.argv[1], 16) if len(sys.argv) > 1 else START
    e = int(sys.argv[2], 16) if len(sys.argv) > 2 else END
    for base, dst, src, addr, mn in run(s, e):
        print(f'{src[0]}+0x{src[1]:X} ({src[2]}{"," + src[3] if len(src) > 3 else ""}) -> [{base}+0x{dst:X}] @{addr:x}')
