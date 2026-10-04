# Verification handoff: BotW / TotK EMTR field maps

Status date: 2026-10-04. Goal: a **100% verified** field map of the `EMTR` (and `ESET`) serialized structures for BotW (VFXB v20, `nn::vfx`, 0xA88 bytes) and TotK (VFXB v51, `nn::vfx2`, 0x10C8 bytes), so a normalized API can be built over both games.

Hard rule from the user: **do not label a field unless you are certain of its purpose.** Unproven fields stay `Unverified` and are carried through the API with that status.

## 1. Method (what counts as evidence)

A field label is **Confirmed** only if at least one of these is true:

1. A decompiled function in the **target game's own binary** reads the field and its use unambiguously defines the meaning (for example a value is used as a blend-mode index, or as `interval + 1 + rand(range)`).
2. Two independent in-binary uses agree (for example the same offset gates the same behavior in two functions).

Weaker sources, which are hints and never proof:

- **NintendoWare SDK source** in `research/vfx/vfx_*.cpp`. It is not guaranteed to match the exact BotW/TotK build. Use it to propose names only. A name is promoted to Confirmed when the binary's arithmetic matches the SDK's arithmetic, and the doc must say so.
- **EffectPorter docs/CSV** (`WiiXLaunch\totk-abilities-botw\Tooling\EffectPorter\docs\*.md`, `emitter_fields.csv`, `effectporter\emitter.py`). These contain speculative labels and wrong line refs. Do not trust them.
- **Offset-delta pairing** (BotW offset + a constant = TotK offset). The deltas vary by zone (`+0x20`, `+0x2D0`, `+0x548`, `+0x550`, `+0x558`, `+0x5A0`). A paired field is `Paired`, not Confirmed, until its counterpart is decompiled in the other game.

### Workflow that works

1. Decompile a function with `decompile_function_by_address` (arg: `address`). Large outputs get saved to a file under `...\brain\<conv>\.system_generated\steps\<N>\output.txt`.
2. Grep that output for the hex offsets (`0xd5c` style, lowercase) with `Select-String ... -Context`.
3. Read the usage, compare with the doc row, fix the row (label, type, size, evidence).
4. Re-run the coverage audit (section 4) to see what is still unseen.

Ghidra MCP server: `ghidra`. Tools: `search_functions_by_name` (arg `query`), `decompile_function_by_address`, `get_function_by_address`, `get_xrefs_to`, `list_open_programs`. `open_program` failed on `/main111*`, so the user must switch the open program in Ghidra manually. **Ask the user which game is open before decompiling.** Project files: `/main`, `/main.analyzed`, `/main111`, `/main111.analyzed`, `/sdk`, `/sdk.analyzed`.

## 2. Where things stand

### TotK (open in Ghidra during this session)

Doc: `docs/research/totk-emtr-offsets-ghidra.md`. Functions decompiled so far:

| Function | Address |
|---|---|
| `EmitterResource::UpdateParams` | `0x710000bbe4` |
| `EmitterResource::UpdateShaderResource` | `0x710000b79c` |
| `EmitterResource::Setup` | `0x710000b694` |
| `EmitterResource::InitializeVertexState` | `0x710000b39c` |
| `Rendercontext::Initialize` | `0x710001c148` |
| `Emitter::Initialize` | `0x7100001900` |
| `Emitter::UpdateByEmit` | `0x7100003df8` |
| `Emitter::CalculateRequiredParticleAsignmentCount` | `0x71000035f4` |
| `Emitter::CalculateParticle` | `0x7100010968` |
| `Emitter::CalculateAlpha` / `CalculateAlpha1` / `GetScaleRate` | `0x71000040a8` / `0x7100004048` / `0x7100004158` |
| `Emitter::InitializeParticle` | `0x7100012188` |
| `Emitter::CreateResMatrix` / `UpdateResMatrix` | `0x7100003844` / `0x710000fb4c` |
| `Emitter::UpdateParticleProperties` | `0x710000fa40` |
| `EmitterSet::Initialize` | `0x7100008eac` |
| `Emitter::InheritParentParticleInfo` | `0x7100011ba4` |
| `TryEmitParticle` | `0x710000f69c` |

Not yet decompiled or fully read: `ResolveBinaryData @ 0x710000ec80`, `Emitter::Calculate @ 0x710000febc` (decompiled, nothing found for `0xE78+`), `FUN_71000127f0` (spread cone), `CalculateEmitPrimitive @ 0x71000155fc`, the emit-shape functions, and `InitializeConstantBuffer @ 0x710000ab94`.

**Verified/corrected this session:**

- `0xE28..0xE3C`: five enable flags, five loop-style flags, and rates (`uint16` x4 at `0xE34..0xE3A`, `int32` at `0xE3C`). Confirmed by `UpdateParams`. The `loop_trackN` names mean only "index N". Which animation each drives is not proven.
- `0xE78..0xE94`: **no reader found** in any decompiled TotK function. Relabeled `unverified_E78..E94`, BotW pairing removed.
- Fade fields `0xCA9..0xCAC`, `0xD28`, `0xD2C`: confirmed by `CalculateAlpha*` and `GetScaleRate`.
- Render state, base `0xDE8` (`Rendercontext::Initialize`): `0xDEE` = blend-mode index (0 to 5), `0xDEF` = cull mode (a 0/1/2 style enum mapped through a small switch), `0xDEA` = a value below 8 fed to the depth-stencil state. **`0xDE8` is used as the blend target's enable bit, not "color write"; the doc label still says `render_color_write` and is wrong.** `0xDEA` being "depth func" is not proven.
- Emission block `0xD4A..0xD8C`: rewritten. `emit_duration` (`0xD54`, `uint32`), `emit_interval` (`0xD60`), `emit_interval_random` (`0xD64`), `emit_dist_enable` (`0xD4A`), `emit_dist_unit/min/max/margin` (`0xD7C/0xD80/0xD84/0xD88`), `emit_dist_particle_max` (`0xD8C`). Names come from the SDK; the **binary's arithmetic matches the SDK's**, which is the evidence. For example `UpdateByEmit` computes `(D60+1) + random(D64)`, and `TryEmitParticle` clamps the travelled length between `D80` and `D84` after zeroing values below `D88`.
- Open doubt: `0xD5C` `emit_rate_random`. TotK reads **one byte**; BotW doc has `int32` at `0x804`. Unresolved.

**Coverage audit** (section 4): 232 documented TotK rows. After this session about 73 had no matching offset in any decompiled function. Still unseen: header (`0x000..0x00C`), `0x0C8`, `0xD39`, `0xD70`, shape block `0xD91..0xDB4`, `0xDD0`, render `0xDE9..0xDF0`, `0xDF9`, `0xDFC..0xE04`, `0xE14..0xE20`, waveform bytes `0xE49..0xE4B`, `0xF14`, `0xF8F`, sampler flags `0x102D..0x107F`. "Seen" only means the offset appears in a decompile; each row still needs its use checked by hand (the `0xD54` row was "seen" yet wrong).

### BotW

Doc: `docs/research/botw-emtr-offsets-ghidra.md`. Not touched this session. Earlier work decompiled (Switch 1.6.0 ARM64): `InitializeEmitterSetResource @ 0x7100ae44ec`, `InitializeEmitterGraphicsResource @ 0x7100ae3e54`, constant buffer setup `0x7100adb8f4`, feature bitmask `0x7100adc8a8`, `CalculateParticle @ 0x7100ad73d0`, `CreateResMatrix @ 0x7100ad6560`, circle emit `0x7100add248` and `0x7100add704`, color/alpha/scale animation `0x7100ae19ac`, `0x7100ae1e08`, `0x7100ae1514`, `TryEmitParticle @ 0x7100ad7084`, `UpdateByEmit @ 0x7100ad69b0`, `Emitter::Initialize @ 0x7100ad583c`. **The doc's `file:Lnnn` line citations were inherited and some refer to a different (Wii U) binary. Re-verify each against the live Switch binary.**

Known BotW issues:

- Track count: docs say 5 tracks in some places and 6 (count `0x74`, array `0x680`) in others.
- Semantics of `0x8D8..0x8F4`: needs a decompile to settle what these five flag/rate sets drive.
- Every `Paired` field needs its BotW counterpart decompiled.

## 3. Other open items

1. Attribute chunk payloads are unmapped: `EAA0 EAA1 EAC0 EAC1 EAPL EASL EAER EADV EAGV EAOV EATR EAES EAET EASS EP01-04 FRN1 FRND FPAD FCSF FMAG FCOL FCLN FSPN CADP CSDP CUDP` and TotK-only `FCOV`, `FGWD`. Only the FourCCs are known (BotW `ResolveBinaryData @ 0x7100adcca8`, TotK `0x710000ec80`).
2. Container tree: node types actually seen in the SDK source are `GRTF`(+`GTNT`), `PRMA`(`PRIM`), `G3NT`, `GRSN`, `ESTA`, `ESET`, `EMTR`. `G3DA` and `SHDA` are unverified. Confirm in the binaries before the writer relies on them.
3. Uniform staging regions (BotW `0x0A8..0x2C0`, TotK `0x0EC..0x4A0`) are opaque.
4. Texture slots stay neutral (`TextureSlotId_0..5`). Do not reintroduce "emissive/specular/custom light" names.

## 4. Coverage audit script (re-run after each decompile)

```powershell
$base = "C:\Users\dylan\.gemini\antigravity\brain\c24ad853-4589-4b7e-8c47-b11f5145aff9\.system_generated\steps"
$ids  = 616,633,638,640,654,655,657,669,671,673,675,676,690   # step ids of decompile outputs
$code = ($ids | % { Get-Content "$base\$_\output.txt" -Raw }) -join "`n"
$seen = @{}
[regex]::Matches($code,'0x([0-9a-f]{2,4})\b') | % { $seen[[Convert]::ToInt32($_.Groups[1].Value,16)] = 1 }
Select-String -Path docs\research\totk-emtr-offsets-ghidra.md -Pattern '^\| `0x([0-9A-F]+)`' |
  % { $o=[Convert]::ToInt32($_.Matches[0].Groups[1].Value,16); if(-not $seen.ContainsKey($o)){ $_.Line.Split('|')[1].Trim() } }
```

Step ids belong to the earlier session; if they are gone, re-decompile and re-save.

## 5. Planned API (do not code the model until the maps are verified)

See `docs/api-design.md`. Decisions so far: **tree-based writer** (rebuild the node tree rather than patch bytes in place) per the user. Still open: attribute chunks (opaque `RawChunk` vs trace), and whether `Unverified`/`Paired` fields are hidden or exposed with status. The `FieldStatus {Confirmed, Paired, Unverified}` enum in the layout tables is meant to carry exactly what this document tracks.

## 6. Next steps, in order

1. TotK: shape block (`InitializeParticle`, emit-shape functions), render/sort/shader-option bytes, `0xE14..0xE20`, waveform bytes, `0xF14`, `0xF8F`, sampler flags; then fix the `render_color_write` label and resolve `0xD5C`.
2. Ask the user to switch Ghidra to BotW, then re-verify every BotW row against the live binary, settle the 5-vs-6 track count and `0x8D8..0x8F4`, and decompile counterparts for all `Paired` rows.
3. Trace attribute chunk payloads (optional, user decision).
4. Only then implement `FieldDef` / `EmitterLayout` tables from the verified docs, then the view, model, converter and tree writer. Run `dotnet build` after each code change.
