"""Triage for BotW<->TotK field pairs: compares the code around the readers of a field in both executables.

For every TotK map row that names a BotW offset, finds immediate-offset load/store sites (library code only, GOT loads
dropped) for the field in each binary, and scores how similar the surrounding instruction shapes are (mnemonic sequence
with registers and immediates erased). A high score means the same source expression is compiled around both reads - strong
supporting evidence, NOT proof; read the contexts of low scores and of anything you intend to mark Confirmed.

env: BOTW_NSO, TOTK_NSO (paths to the `main` NSOs); BOTW_LIB / TOTK_LIB (hex lo,hi image-offset ranges of the vfx code).
usage: python pairmatch.py <totk-emtr-offsets-ghidra.md> [min_score]
"""
import difflib
import os
import re
import sys

import capstone
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'gen'))
import nso
from parse_maps import parse

BOTW = os.environ.get('BOTW_NSO', r'C:/Users/dylan/shenanigans/Personal/Nintendo/Switch/Zelda BotW/RE/main')
TOTK = os.environ.get('TOTK_NSO', r'C:/Users/dylan/shenanigans/Personal/Nintendo/Switch/Zelda TotK/RE/Exefs121/main')
RANGES = {'botw': tuple(int(x, 16) for x in os.environ.get('BOTW_LIB', 'ac8000,af2000').split(',')),
          'totk': tuple(int(x, 16) for x in os.environ.get('TOTK_LIB', '850,2f300').split(','))}
md = capstone.Cs(capstone.CS_ARCH_ARM64, capstone.CS_MODE_ARM)


class Image:
    def __init__(self, path, game):
        self.text = nso.load(path)[0][1]
        self.w = np.frombuffer(self.text[:len(self.text) // 4 * 4], dtype='<u4')
        self.lib = RANGES[game]

    def isgot(self, a):
        i = a // 4
        base = (int(self.w[i]) >> 5) & 31
        for k in (1, 2, 3, 4):
            p = int(self.w[i - k])
            if (p & 0x9F000000) == 0x90000000 and (p & 31) == base:
                return True
        return False

    def hits(self, lo, hi):
        w = self.w
        imm12 = (w >> 10) & 0xFFF
        size = (w >> 30) & 3
        v = (w >> 26) & 1
        opc = (w >> 22) & 3
        base = (w & 0x3B000000) == 0x39000000
        q = (v == 1) & (size == 0) & (opc >= 2)
        width = np.where(q, 16, 1 << size)
        shift = np.where(q, 4, size)
        off = imm12 << shift.astype(np.uint32)
        m = base & (off <= hi) & (off + width > lo)
        out = [int(i) * 4 for i in np.nonzero(m)[0]]
        # pairs
        pair = ((w & 0x3A000000) == 0x28000000) & (((w >> 23) & 7) == 2)
        psz = (w >> 30) & 3
        pv = (w >> 26) & 1
        pw = np.where(pv == 1, np.array([4, 8, 16, 0])[psz], np.where(psz == 2, 8, np.where(psz == 0, 4, 0))).astype(np.int64)
        imm7 = ((w >> 15) & 0x7F).astype(np.int64)
        imm7 = np.where(imm7 >= 64, imm7 - 128, imm7)
        poff = imm7 * pw
        m2 = pair & (pw > 0) & (poff >= 0) & (poff <= hi) & (poff + 2 * pw > lo)
        out += [int(i) * 4 for i in np.nonzero(m2)[0]]
        return sorted(a for a in set(out) if self.lib[0] <= a < self.lib[1] and not self.isgot(a))

    def shape(self, a, n=7):
        out = []
        for i in md.disasm(self.text[a - 4 * n:a + 4 * (n + 1)], a - 4 * n):
            out.append(i.mnemonic)
        return out


def score(a, b):
    return difflib.SequenceMatcher(None, a, b).ratio()


def main():
    rows = parse(sys.argv[1], 'totk')
    min_score = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
    # the TotK table has the BotW offset in column 2; re-read it
    pairs = []
    for l in open(sys.argv[1], encoding='utf-8'):
        if not l.startswith('| `0x'):
            continue
        c = [x.strip() for x in l.strip().strip('|').split('|')]
        mt = re.match(r'`0x([0-9A-Fa-f]+)`', c[0])
        mb = re.fullmatch(r'`0x([0-9A-Fa-f]+)`', c[1])
        if not (mt and mb):
            continue
        name = c[5].strip('`')
        if name.startswith(('unverified', 'unused', 'runtime', 'reserved')):
            continue
        try:
            size = int(c[3], 0)
        except ValueError:
            continue
        pairs.append((int(mt.group(1), 16), int(mb.group(1), 16), size, name))
    botw, totk = Image(BOTW, 'botw'), Image(TOTK, 'totk')
    for t, b, size, name in pairs:
        # width-limited probes (an 8-byte read of a 4-byte field would also match neighbours)
        ps = min(size, 4)
        bh, th = botw.hits(b, b + ps - 1), totk.hits(t, t + ps - 1)
        best = (0.0, None, None)
        for ba in bh:
            sb = botw.shape(ba)
            for ta in th:
                sc = score(sb, totk.shape(ta))
                if sc > best[0]:
                    best = (sc, ba, ta)
        if best[0] >= min_score:
            print(f'{name:38s} botw+0x{b:X} totk+0x{t:X} sites {len(bh):3d}/{len(th):3d} best {best[0]:.2f}'
                  + (f' botw@{best[1]:x} totk@{best[2]:x}' if best[1] else ''))


if __name__ == '__main__':
    main()
