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
dotnet run --project PtclCorpus -- emtr  "<romfs>" emtr_all.bin
dotnet run --project PtclCorpus -- bnsh  "<romfs>" bnsh_all
dotnet run --project ShaderUse  -- use      bnsh_all used_all.json
dotnet run --project ShaderUse  -- patterns bnsh_all patterns.json
python exe/audit.py "E14 E14 E17"    # scan the vfx2 code for readers of EMTR data offsets 0xE14..0xE17
```

Evidence rules (from `verification-handoff.md`) still apply: an offset appearing in a scan is a lead, not proof; read the
instruction context and establish the pointer base before naming a field.
