"""Interprocedural register-taint tracker for ARM64 code.

Starting from a load that fetches a pointer (for example `ldr xR, [xS, #0x128]` = EmitterResource +0x128), follows the
tainted value through moves, stack spills/reloads and calls (tainted x0..x7 arguments are followed into the callee) and
reports every load that dereferences it, grouped by field offset.

It is deliberately path-insensitive (a linear scan per function): it finds the set of offsets a function can read, it
does not prove which branch reads them. Always read the instruction context before naming a field.

usage: python taint.py <hex addr of the pointer load> [depth]
"""
import re
import sys
import collections

import armdis

IMM = r'(?:#(-?0x[0-9a-f]+|-?\d+))'
LOADS = ('ldr', 'ldrb', 'ldrh', 'ldrsw', 'ldrsb', 'ldrsh', 'ldur', 'ldurb', 'ldp', 'ldr')
ARGS = [f'x{i}' for i in range(8)]


def parse_mem(op):
    m = re.search(r'\[(x\d+|sp)(?:, ' + IMM + r')?\]', op)
    if not m:
        return None
    return m.group(1), int(m.group(2), 0) if m.group(2) else 0


def func_end(start, limit=0x2000):
    """Heuristic end: first `ret` after start (functions here are straight-line enough), capped."""
    code = armdis.t[start:start + limit]
    for i in armdis.md.disasm(code, start):
        if i.mnemonic == 'ret':
            return i.address + 4
    return start + limit


class Tracker:
    def __init__(self):
        self.hits = collections.defaultdict(set)  # offset -> {(mnemonic, address)}
        self.seen = set()
        self.external = set()  # (call site, target) where a tainted argument leaves the traced code

    def run_function(self, start, tainted_args, depth, end=None):
        key = (start, tuple(sorted(tainted_args)))
        if key in self.seen or depth < 0:
            return
        self.seen.add(key)
        end = end or func_end(start)
        regs = set(tainted_args)
        spill = set()  # stack offsets holding the tainted value
        for i in armdis.md.disasm(armdis.t[start:end], start):
            self.step(i, regs, spill, depth)

    def step(self, i, regs, spill, depth):
        mn, op = i.mnemonic, i.op_str
        parts = [p.strip() for p in op.split(',')]
        dest = parts[0] if parts else ''
        mem = parse_mem(op)
        # record dereferences of a tainted base
        if mn in LOADS and mem and mem[0] in regs and mem[0] not in ('sp', 'x29'):
            self.hits[mem[1]].add((mn, i.address))
        # taint propagation
        if mn == 'mov' and len(parts) == 2 and parts[1] in regs:
            regs.add(dest)
        elif mn == 'add' and len(parts) >= 2 and parts[1] in regs and parts[0].startswith('x') and not re.search(r'x\d+$', parts[-1]):
            # pointer plus constant: a derived pointer is tracked as the same object with an offset; record the delta
            regs.add(dest)
        elif mn in ('str', 'stur') and mem and mem[0] in ('sp', 'x29') and dest in regs:
            spill.add((mem[0], mem[1]))
        elif mn in ('ldr', 'ldur') and mem and mem[0] in ('sp', 'x29') and (mem[0], mem[1]) in spill:
            regs.add(dest)
        elif mn.startswith(('ldr', 'mov', 'add', 'sub', 'and', 'orr', 'lsl', 'lsr', 'adrp', 'madd', 'mul', 'cset', 'csel')) and dest in regs:
            # overwritten by something that is not a tracked copy (a self-load such as ldr x8,[x8,#imm] is a deref, handled above)
            if not (mn.startswith('add') and parts[1] in regs):
                regs.discard(dest)
        # follow calls whose arguments are tainted
        if mn == 'bl':
            m = re.match(r'#(0x[0-9a-f]+)', op)
            if m:
                target = int(m.group(1), 16)
                tainted = [a for a in ARGS if a in regs]
                if tainted and (target < 0x850 or target >= 0x2f300):
                    self.external.add((i.address, target))
                if tainted and depth > 0:
                    self.run_function(target, tainted, depth - 1)
            # caller-saved registers are clobbered by the call
            for a in ARGS + ['x8', 'x9', 'x10', 'x11', 'x12', 'x13', 'x14', 'x15']:
                regs.discard(a)


def from_load(addr, depth=3):
    """Taint the destination of the `ldr xR, [..., #imm]` at addr and track it to the end of that function."""
    ins = list(armdis.md.disasm(armdis.t[addr:addr + 4], addr))[0]
    reg = ins.op_str.split(',')[0].strip()
    tr = Tracker()
    end = func_end(addr)
    regs = {reg}
    spill = set()
    for i in list(armdis.md.disasm(armdis.t[addr + 4:end], addr + 4)):
        tr.step(i, regs, spill, depth)
    return tr


if __name__ == '__main__':
    a = int(sys.argv[1], 16)
    d = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    tr = from_load(a, d)
    for off in sorted(tr.hits):
        print(f'+0x{off:X}', sorted({f'{m}@{ad:x}' for m, ad in tr.hits[off]})[:4])
