"""Side-by-side evidence cards for BotW<->TotK field pairs (see pairmatch.py for the environment variables).

usage: python cards.py <totk-emtr-offsets-ghidra.md> <field name> [<field name> ...]
Prints, for each field, the best-matching reader site in each binary with +-6 instructions of context.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import re
import pairmatch as pm


def snippet(img, a, n=6):
    out = []
    for i in pm.md.disasm(img.text[a - 4 * n:a + 4 * (n + 1)], a - 4 * n):
        mark = '=>' if i.address == a else '  '
        out.append(f'{mark} {i.address:x}: {i.mnemonic} {i.op_str}')
    return out


def main():
    path = sys.argv[1]
    want = set(sys.argv[2:])
    botw, totk = pm.Image(pm.BOTW, 'botw'), pm.Image(pm.TOTK, 'totk')
    for l in open(path, encoding='utf-8'):
        if not l.startswith('| `0x'):
            continue
        c = [x.strip() for x in l.strip().strip('|').split('|')]
        if len(c) < 6:
            continue
        name = c[5].strip('`')
        if name not in want:
            continue
        mt = re.match(r'`0x([0-9A-Fa-f]+)`', c[0])
        mb = re.fullmatch(r'`0x([0-9A-Fa-f]+)`', c[1])
        if not (mt and mb):
            print(f'## {name}: no BotW offset in the TotK table')
            continue
        t, b = int(mt.group(1), 16), int(mb.group(1), 16)
        size = min(int(c[3], 0), 4)
        bh, th = botw.hits(b, b + size - 1), totk.hits(t, t + size - 1)
        best = (0.0, None, None)
        for ba in bh:
            sb = botw.shape(ba)
            for ta in th:
                sc = pm.score(sb, totk.shape(ta))
                if sc > best[0]:
                    best = (sc, ba, ta)
        print(f'## {name}  botw+0x{b:X} totk+0x{t:X}  botw sites {len(bh)} totk sites {len(th)} best {best[0]:.2f}')
        if best[1]:
            bs, ts = snippet(botw, best[1]), snippet(totk, best[2])
            for x, y in zip(bs, ts):
                print(f'  {x:60s} | {y}')
        elif bh:
            print('  BotW sites:', ' '.join(f'{a:x}' for a in bh[:8]))
            for x in snippet(botw, bh[0]):
                print('  ' + x)
        print()


if __name__ == '__main__':
    main()
