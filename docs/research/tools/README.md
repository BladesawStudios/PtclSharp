# PTCL research tooling

Scripts used to build `docs/research/totk-emtr-offsets-ghidra.md`. They are research aids, not part of the library, and
contain absolute paths for the author's machine (override with the noted environment variables).

| Tool | Purpose |
|---|---|
| `PtclCorpus` (C#, uses `PtclSharp`) | `emtr`, `attrs`, `bnsh` corpus extraction from every shipped `.esetb.byml.zs` (see the header of `Program.cs`). Output feeds the per-offset value statistics ("Corpus:" sentences in the doc). |
| `ShaderUse` (C#, needs Marrow's `ShaderLibrary.CompileTool`) | Decompiles every distinct BNSH to GLSL and reports which bytes of `sysEmitterStaticUniformBlock` (binding 6 = EMTR data bytes `[0, 0xCA0)`) the vertex/fragment/compute programs read (`use`), the normalized GLSL lines that read each byte (`patterns`), or writes sample GLSL files for chosen offsets (`show`). |
| `exe/nso.py` | Loads an NSO (`main`), LZ4-decompresses its segments. `TOTK_MAIN_NSO` overrides the path. |
| `exe/scan.py`, `exe/audit.py` | Finds every ARM64 load/store/pair whose immediate covers a byte range of an object (`audit.py` limits it to the `nn::vfx2` code, `0x7100000850..0x71002F300` in the 1.2.1 image, and drops `adrp`+`ldr` GOT loads). This is how "no reader" claims were established. |
| `exe/armdis.py`, `exe/accs.py` | Capstone disassembly of an address and per-register access summary of a function range. |

Typical use:

```bash
dotnet run --project PtclCorpus -- emtr  totk "<romfs>" emtr_all.bin    # also: attrs | eset | bnsh ; game = totk | botw
dotnet run --project PtclCorpus -- emtr  botw "<ROM root>" emtr_botw.bin
dotnet run --project PtclCorpus -- bnsh  totk "<romfs>" bnsh_all
dotnet run --project ShaderUse  -- use      bnsh_all used_all.json
dotnet run --project ShaderUse  -- patterns bnsh_all patterns.json
python exe/audit.py "E14 E14 E17"    # scan the vfx2 code for readers of EMTR data offsets 0xE14..0xE17
```

Evidence rules (from `verification-handoff.md`) still apply: an offset appearing in a scan is a lead, not proof; read the
instruction context and establish the pointer base before naming a field.

## Layout generation and tests

`gen/gen_layouts.py` turns the byte-map tables of the two research docs into `src/PtclSharp/Layout/{Totk,Botw}EmitterFields.g.cs`
(run `python docs/research/tools/gen/gen_layouts.py docs/research src/PtclSharp/Layout`). Until the C# tables become the primary
source, edit the doc row and regenerate; hand-written layouts (file/node header, ESET, chunks) live in
`src/PtclSharp/Layout/TotkLayouts.cs` and `BotwLayouts.cs`.

`tests/PtclSharp.Tests` validates the tables (no files needed) and, when `PTCL_TOTK_ROMFS` points at a TotK romfs root,
checks them against all shipped effect files: every file loads, declared emitter counts, node order, alignment, chunk
payload sizes, and a lossless read/rewrite of every emitter field.

## BotW additions

| Tool | Purpose |
|---|---|
| `exe/pairmatch.py`, `exe/cards.py` | Compare the code around the readers of a field in the BotW and TotK executables (shape score / side-by-side context). Supporting evidence only. |
| `exe/bw_baseline.py` | Per-row corpus/GPU/CPU baseline for an EMTR map (`TOTK_MAIN_NSO` points at the BotW `main`, `PTCL_LIB=ac8000,af2000`). |
| `gen/botw_rows.py`, `gen/botw_overrides.py` | Source of the BotW byte map: regenerate section 3 of the BotW doc with `BOTW_EMTR_BIN=<emtr_botw.bin> python botw_rows.py <docs/research>`, then run `gen_layouts.py`. |

BotW corpus tests need `PTCL_BOTW_ROM` (the ROM root that contains `Effect/*.sesetlist`).
