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
| `EmitterResource::ResolveBinaryData` | `0x710000ec80` |
| `Emitter::Calculate` | `0x710000febc` |
| `EmitterCalculator::CalculateParticleBehavior` | `0x7100018280` |
| `EmitterCalculator::CalculateRotationMatrix` | `0x7100019fac` |
| `FUN_71000127f0` (initial direction/velocity) | `0x71000127f0` |
| `EmitterSet::Draw` / `DrawEmitter` | `0x710000a3a8` / `0x710000a018` |

Still not fully resolved: `0xE14`, `0xE78..0xE94`, `0x100..0x10F`, `0x130..0x13F`, the render bytes not read by `Rendercontext::Initialize`, and several transform/metadata gaps. `CalculateEmitPrimitive` and the individual shape functions have been decompiled far enough to establish the shape table and main geometry fields.

**Verified/corrected this session:**

- Follow-up TotK audit: the VFXB node header is separate from EMTR data. `InitializeEmitterSetResource` proves FourCC/size/child/sibling/attribute/data-relative fields at node `+0x00/+0x04/+0x08/+0x0C/+0x10/+0x14`; therefore EMTR-data rows `0x000..0x00C` are not magic/size/version/flags and are now unverified.
- Corrected pointer-domain mistakes in the old map: EMTR `0x050..0x05C`, `0x0A8..0x0AF`, `0x0B5..0x0BE`, and `0x0EC` had been named from similarly numbered offsets in live `Emitter`, `EmitterSet`, or `EmitterResource` objects. Those unsupported serialized labels were removed. `0xB0..0xD4` is a runtime overlay populated from the five loop-animation controls.
- `0xE08` is confirmed lifetime; `0xE0C` is downward random lifetime percent. `0xE10` is not fade-in: it generates the W component of the initial-scale particle vector in `[1-value,1+value]`. `0xE14` remains unverified.
- `0xF10` is the angular emission spread in degrees. `0xF14` is not a cone angle: it adds a normalized XZ tangent times the field value. `0xF18..0xF20` are per-axis direction randomization, `0xF24` is one-sided random speed percent, and `0xF28/0xF2C` are emitter-motion inheritance scale/cap.
- `0x0E0..0x0EC` is the stationary/degenerate position-delta fallback, and `0x0F0` is per-frame velocity attenuation (`pow(value, frame_time)`).
- `0xCA1` is the actual particle-sort mode. `0xDEB` selects descending versus ascending camera-depth order for mode 2. `0xDFC` only selects shader bits and is not proven to be sorting.
- `0xC10..0xC98`, `0xE00..0xE02`, `0xE46..0xE4B`, and `0xDFD..0xDFF` form the three-lane rotation-modulation/waveform system. Initial rotation, random range, increment, attenuation, increment randomization, waveform amplitude/period/time/random phase, and random output signs are now separated.
- `0xCF0..0xCF8` is emitter base scale, not angular velocity. `ResolveBinaryData`, `ResourceUpdate`, and the 14-lane animation loop establish `0xCFC..0xD18` as emitter Color0/Color1 RGBA, `0xD6C` as gravity scale, `0xDC0..0xDC8` as emitter-volume scale, `0xEFC` as all-directional speed, `0xF00` as designated-direction speed, and `0xF04..0xF0C` as the designated direction vector.
- Child inheritance is mapped at `0xD30..0xD44`; `0xD39` selects pre/post-parent draw and `0xD3C` selects normal versus lightweight child-emitter behavior. `0xD33` still has no proven reader.
- `0x110..0x12C` plus `0xF8C..0xF8F` form the alpha/scale waveform system. `0xE28..0xE44` are now tied to Color0, Alpha0, Color1, Alpha1, and Scale loop periods/random phase/interpolation modes.
- Color modes `2` and `3` are interpolated-key and discrete-key selection respectively; mode 3 was incorrectly called random. The six texture mode bytes and UV-domain scale bytes were also neutralized and documented from their exact flag/table behavior.
- Six flipbook runtime blocks are now mapped at `0x140..0x49F` (stride `0x90`). `0xE51..0xE56` select external versus internal resolution for the three graphics and three compute shader passes.

- `0xE28..0xE3C`: five enable flags, five loop-style flags, and rates (`uint16` x4 at `0xE34..0xE3A`, `int32` at `0xE3C`). Confirmed by `UpdateParams`. The `loop_trackN` names mean only "index N". Which animation each drives is not proven.
- `0xE78..0xE94`: **no reader found** in any decompiled TotK function. The speculative two-five-flag interpretation was removed; `0xE78..0xE83` is now one unverified byte region until element widths are proven.
- Fade fields `0xCA9..0xCAC`, `0xD28`, `0xD2C`: confirmed by `CalculateAlpha*` and `GetScaleRate`.
- Render state, base `0xDE8` (`Rendercontext::Initialize`): `0xDE8` = blend-target enable, `0xDEE` = blend-mode index (0 to 5), `0xDEF` maps `0/1/2` to none/back/front culling, and `0xDEA` is a value below 8 written into depth-stencil state. `0xDE9`, `0xDEC`, `0xDED`, and `0xDF0` are not read by this TotK initialization path and remain unverified.
- Emission block `0xD4A..0xD8C`: rewritten. `emit_duration` (`0xD54`, `uint32`), `emit_interval` (`0xD60`), `emit_interval_random` (`0xD64`), `emit_dist_enable` (`0xD4A`), `emit_dist_unit/min/max/margin` (`0xD7C/0xD80/0xD84/0xD88`), `emit_dist_particle_max` (`0xD8C`). Names come from the SDK; the **binary's arithmetic matches the SDK's**, which is the evidence. For example `UpdateByEmit` computes `(D60+1) + random(D64)`, and `TryEmitParticle` clamps the travelled length between `D80` and `D84` after zeroing values below `D88`.
- `0xD5C` is confirmed as a one-byte emission-rate random percentage in TotK. `0xD5D..0xD5F` remain unverified/padding; the BotW width must be checked separately.

Sample-inspection warning: when using PowerShell against `VfxbNode.DataOffset`, cast the property directly (`[int]$o = $node.DataOffset`). It is already unboxed; using `$node.DataOffset.Value` silently yields null and reads from file offset zero. Earlier exploratory sample values obtained with `.Value` were discarded and rerun correctly.

**Coverage status:** the old numeric audit is obsolete because the map has been split into more precise rows. Major remaining unknown regions are `0x000..0x00C`, `0x050..0x06F`, `0x0A8..0x0AF`, shader-only/static-uniform gaps (`0x100..0x10F`, `0x130..0x13F`), `0xC4C..0xC5F`, `0xD1C..0xD27`, `0xD33`, inactive render bytes, `0xE14`, and `0xE78..0xE94`. A raw offset appearing in a decompile is not sufficient; each row still needs its use checked by hand.

### BotW

Doc: `docs/research/botw-emtr-offsets-ghidra.md`. Not touched this session. Earlier work decompiled (Switch 1.6.0 ARM64): `InitializeEmitterSetResource @ 0x7100ae44ec`, `InitializeEmitterGraphicsResource @ 0x7100ae3e54`, constant buffer setup `0x7100adb8f4`, feature bitmask `0x7100adc8a8`, `CalculateParticle @ 0x7100ad73d0`, `CreateResMatrix @ 0x7100ad6560`, circle emit `0x7100add248` and `0x7100add704`, color/alpha/scale animation `0x7100ae19ac`, `0x7100ae1e08`, `0x7100ae1514`, `TryEmitParticle @ 0x7100ad7084`, `UpdateByEmit @ 0x7100ad69b0`, `Emitter::Initialize @ 0x7100ad583c`. **The doc's `file:Lnnn` line citations were inherited and some refer to a different (Wii U) binary. Re-verify each against the live Switch binary.**

Known BotW issues:

- Track count: docs say 5 tracks in some places and 6 (count `0x74`, array `0x680`) in others.
- Semantics of `0x8D8..0x8F4`: needs a decompile to settle what these five flag/rate sets drive.
- Every `Paired` field needs its BotW counterpart decompiled.

## 3. Other open items

1. The fourteen emitter-animation attribute identities, order, live-lane offsets and base-value sources are mapped in TotK: `EAES`, `EAER`, `EAET`, `EAC0`, `EAC1`, `EATR`, `EAPL`, `EAA0`, `EAA1`, `EAOV`, `EADV`, `EASL`, `EASS`, `EAGV`. Their **chunk payload schemas** are still unmapped. The remaining particle/custom attribute chunks (`EP01-04`, `FRN1`, `FRND`, `FPAD`, `FCSF`, `FMAG`, `FCOL`, `FCLN`, `FSPN`, `CADP`, `CSDP`, `CUDP`, and TotK-only `FCOV`, `FGWD`) have only their FourCC lookup/resolution behavior confirmed (BotW `ResolveBinaryData @ 0x7100adcca8`, TotK `EmitterResource::ResolveBinaryData @ 0x710000ec80`).
2. Container tree: node types actually seen in the SDK source are `GRTF`(+`GTNT`), `PRMA`(`PRIM`), `G3NT`, `GRSN`, `ESTA`, `ESET`, `EMTR`. `G3DA` and `SHDA` are unverified. Confirm in the binaries before the writer relies on them.
3. The uniform staging areas are only partly mapped. In TotK, the six repeated texture blocks at `0x4A0 + slot * 0x50` have confirmed UV translation, scale, rotation and domain-scale members, but several bytes remain unknown. The static/shader-only regions still have a proof ceiling without shader reflection or a direct CPU consumer.
4. Texture slots stay neutral (`TextureSlotId_0..5`). Do not reintroduce "emissive/specular/custom light" names.

## 4. Historical coverage audit script

This was useful in the earlier session, but its saved decompiler-output paths and step IDs are session-local. Do not treat the output as evidence. Re-decompile the relevant functions in the live Ghidra project and verify each use by hand.

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

## 6. Handoff to Claude

### Working context

- Repository: `C:\Users\dylan\repos\Nintending\Libraries\PtclSharp`
- Current Ghidra program: `/main.analyzed`, the TotK binary.
- Primary live map: `docs/research/totk-emtr-offsets-ghidra.md`.
- BotW map: `docs/research/botw-emtr-offsets-ghidra.md`; it is not the current Ghidra program and contains older claims that must be rechecked when the user switches projects.
- The untracked `lib/BymlSharp/` and `lib/ZstdSharp/` directories, and any unrelated working-tree changes, belong to the user. Preserve them.

### Non-negotiable evidence rule

Only give a field a semantic name when a TotK decompile proves the interpretation through an actual read, write, call argument, enum dispatch, arithmetic role or runtime copy chain. An offset merely appearing in a decompile, a suggestive value in a sample, an SDK name without matching binary behavior, or a BotW analogy is not enough. Leave anything else `Unverified`, state its exact width only when that is proven, and record the function/address and reasoning in the map immediately.

Do **not** begin the class-library/model implementation yet. The user explicitly wants the questionable TotK fields verified first. Do not invent friendly names for unknown render bytes, texture slots, shader-only constants or attribute payload members.

### Confirmed anchor functions in the open TotK program

- `EmitterResource::UpdateParams @ 0x710000bbe4`: parameter normalization/defaulting and six texture-uniform blocks.
- `EmitterResource::ResolveBinaryData @ 0x710000ec80`: FourCC resolution and the exact fourteen-lane emitter-animation ordering.
- `Emitter::ResourceUpdate @ 0x7100002000`: copies serialized base values into live animation lanes.
- `Emitter::Calculate @ 0x710000febc`: evaluates fourteen live lanes at `Emitter + 0x3D8`, stride `0x0C`.
- `Emitter::CalculateParticleBehavior @ 0x7100018280`: runtime particle behavior.
- Initial-direction helper `FUN_71000127f0`.
- Color calculations `CalculateParticleColor0VecFromTime @ 0x7100018d4c` and Color1 wrapper `0x710001967c`.
- `Rendercontext::Initialize @ 0x710001c148`: the render-state bytes that are genuinely consumed by this TotK path.
- Shape paths: circle `0x7100013908`, sphere-32 `0x7100014378`, box `0x7100014f2c`, line `0x7100015300`, equally divided line `0x7100015388`, primitive `0x71000155fc`.
- Animation evaluator `0x7100016250`.

Keep pointer domains distinct. Some functions receive the serialized EMTR data base, others the owning resource object, a secondary body, a live `Emitter`, or a live `Particle`. Convert an observed member access to an EMTR file offset only after proving that base. Several earlier wrong labels came from mixing these domains.

### Highest-value remaining TotK work

1. `0xE14`: decode the packed particle/GPU feature flags by tracing each bit into concrete runtime branches or shader/resource selection. Do not name a bit from sample correlation alone.
2. `0xE78..0xE94`: no confirmed reader has been found. `0xE78..0xE83` must remain one unverified 12-byte region until element widths are proven; `0xE84..0xE94` are five unverified `int32` values.
3. Shader/static-uniform gaps, especially `0x100..0x10F` and `0x130..0x13F`. The CPU binary may not contain enough evidence. If no direct reader, shader reflection, embedded shader metadata or binding copy establishes semantics, explicitly retain `Unverified` rather than guessing.
4. Rotation tail `0xC4C..0xC5F`, color tail `0xD1C..0xD27`, and child byte `0xD33`.
5. Inactive render bytes `0xDE9`, `0xDEC`, `0xDED`, `0xDF0`. They are not read by `Rendercontext::Initialize`; search for other consumers before assigning names.
6. Determine the higher-level roles of the generic keyframe arrays `kf_track5` through `kf_track9` (`0x950..0xBCF`) only if their consumers prove them. These are distinct from the fourteen mapped emitter-animation lanes; do not infer their semantics from numbering or ordering.
7. Attribute chunk payload schemas are a separate optional trace. Their pointers and the fourteen emitter-animation identities are known, but their internal serialized layouts are not.

There may be a genuine binary-only proof ceiling for values consumed exclusively by shaders or opaque middleware. A complete map can honestly contain such `Unverified` regions; “full” means every claim is defensible, not that every byte has been given a speculative name.

### Suggested continuation workflow

1. Start from an unknown row in `totk-emtr-offsets-ghidra.md`, find all direct and indirect readers, and establish the pointer domain before interpreting offsets.
2. Follow calls through initialization, resource normalization and runtime consumption. A default value alone is not semantic proof.
3. Update the research map immediately with the address, access width and exact evidence. If the trace disproves a current label, correct it and mention the replacement in this handoff.
4. For sample inspection in PowerShell, cast `VfxbNode.DataOffset` directly: `[int]$o = $node.DataOffset`. Do not use `$node.DataOffset.Value`; it silently produced null and caused reads from file offset zero in an earlier exploratory pass.
5. Run `git diff --check`, inspect the focused docs diff, and do not touch the code model until the TotK verification phase is explicitly closed by the user.

After TotK is exhausted, ask the user to switch Ghidra to BotW. Then re-verify every BotW row against the live Switch binary, resolve the 5-versus-6 animation-track discrepancy and `0x8D8..0x8F4`, and establish counterparts for all `Paired` rows. Only after both maps are sound should implementation proceed to `FieldDef` / `EmitterLayout`, version-neutral views/models, conversion diagnostics and the tree-based writer.


## 7. Update: TotK verification pass 3 (2026-10-04)

This section supersedes the "Highest-value remaining TotK work" list in section 6 where they disagree. The detailed results are in `totk-emtr-offsets-ghidra.md`; this is the summary, the methods that worked, and what is still open.

### New facts that change how the work is done

1. **The GPU static uniform block is a verbatim copy of EMTR data bytes `[0, 0xCA0)`.** `EmitterResource::UpdateParams` ends with `memcpy(mapped, *param_1, 0xCA0)` (`0x710000BBE4`); `InitializeConstantBuffer` (`0x710000AB94`) sizes the buffer the same way. So a shader read of `sysEmitterStaticUniformBlock.data[i].c` reads EMTR data offset `i*16 + c*4`, after the `UpdateParams` runtime overlays (`0x70..0x7F`, `0xB0..0xD7`, keyframe padding).
2. **Shader archives are inside each `.esetb`**: top-level `GRSN` -> child `GRSR` (a BNSH `ResShaderFile`). There is no separate effect `.bfsha` in `Shader/`. Marrow's `ShaderLibrary` decompiles those BNSH directly (`docs/research/tools/ShaderUse`). 1,088 distinct archives / 14,423 programs were analysed; every static-block index is a compile-time constant, so the set of GPU-read bytes is exact.
3. **The NintendoWare SDK source in `research/vfx` is an accurate naming source where the executable arithmetic agrees.** Confirmed this way: `CA5 update_matrix_by_emit`, `CA8 fade_emit_stop`, `CB4 draw_path`, `D3A/D3B inherit_alpha0/1_each_frame`, `E74 custom_shader_index`, `EF8 custom_action_index`, `D94 sphere64_division_count`, the whole field-chunk family, and the `ESET`/`PRIM`/`G3NT` layouts. Names were not taken where the executable shows only a different arithmetic role (e.g. `FRND +0x1C/+0x2C`).
4. **Exhaustive "no reader" claims are now possible** for offsets at or beyond `0xCA0`: scan every ARM64 load/store/pair in the `nn::vfx2` code (`0x7100000850..0x71002F300`) for an immediate covering the range, drop GOT loads, and inspect what remains. This also found real readers the old map said did not exist: `D68`, `D94`, `DDC` (emit helper / `UpdateParams` jump table / `Emit`).
5. **A corpus of 25,261 shipped EMTR blocks** (`tools/PtclCorpus emtr`) gives value statistics. Regions that are zero in every emitter and unread by both CPU and GPU are recorded as such (`0x000..0x00F`, `0x050..0x06F`, `0x0A8..0x0AF`, `0x0D8..0x0DF`, `0xD33`, etc.). This is supporting evidence only, never a label.

### What is now verified (summary)

- Container: file header, node header, per-kind `size` meaning and alignment, nested child emitters, `GRSN` children, `G3PR`/`G3NT`, `PRMA`/`PRIM`, `ESET` data (see section 1D of the map).
- Attribute chunks: `EAxx` schema (enabled/loop/interpolation/key count/keys), field chunks `FRND FRN1 FMAG FSPN FCOL FCOV FPAD FCLN` with the shared `Anim8Key` block, stripe/area-loop plugin chunks (partially, see below), `CSDP` (raw custom-shader uniform block), `FCSF` (type word plus 16 floats copied to the GPU field buffer), `CADP`/`CUDP` (opaque, passed to callbacks), `FGWD` (3 words copied to the field buffer; purpose unproven).
- EMTR: new confirmed rows `CA5, CA8, CB4, D3A, D3B, D68, D94, DDC, E74, EF8`; every previously "unverified" row and every previously unlisted byte range now carries an explicit **Audit** (CPU scan, GPU read set, corpus statistics); GPU-only fields have exact arithmetic roles but stay `unverified_*` unless the role is unambiguous (`0x100..0x108` template-vertex bias, `0x8B8` discard threshold).
- `kf_track5` has the same shape as the SDK `shaderAnim` track (identical padding arithmetic); tracks 5 to 8 are read by the vertex shader, track 9 by nothing.

### Still open for TotK (honest proof ceiling)

- Names for GPU-only fields: `0xBD0..0xC0F` is a polymorphic shader parameter block; `0x890..0x8AC` ramp-like pairs; `0xC50/0xC54`; `0x10C`, `0x130/0x134`, `0xF4`, `0x8B0`, `0x8C0`, `0x8C4`. Resolving these needs understanding each shader feature (variant selection by `ShaderFlag::Initialize` words `0x70..0x78`), not another binary scan.
- Stripe chunks: only the offsets whose arithmetic was traced are named (`EP01 +0x0C/+0x10`; `EP02 +4/+8/+0x10/+0x14/+0x20/+0x24`; `EP03 +0xC/+0x14/+0x18/+0x1C/+0x50/+0x54`; `EP04` fully). The remaining bytes need the stripe-calculation functions (`CalculateDelayedStripe`, `UpdateHistory`, `UpdateStripePolygon`) traced field by field.
- `CADP`/`CSDP`/`CUDP` member layouts are defined by game code and shader reflection, not by `nn::vfx2`; `FCSF`'s `custom_field_type` and `FGWD`'s three words are consumed outside what was traced.
- `E14` (`0xE14`, packed flags), `E78..E94`, `D1C..D27`, `DE9`, `DEC`, `DED`, `DF0`, and the texture-slot record tails (`FA0..FAF` etc.) have **no reader in `nn::vfx2`**; the texture record tails are passed with the GUID to the texture-resolver interface in game code.
- `ESFT` and `GRTF` nodes never occur in shipped files (their payloads are unmapped); `FPAD` and `FGWD` chunks never occur either, so `FPAD`'s layout comes from the executable only and `FGWD` is opaque.
- Rows verified in earlier sessions were not re-verified here. The 5-versus-6 track discrepancy, `0x8D8..0x8F4`, and all `Paired` rows are BotW tasks.

### Library issues found while building the corpus (fixed 2026-10-04 at the user's request)

1. `VfxbReader.ReadEmitterSets` compared the ESET emitter count with direct `EMTR` children only; the count includes nested child emitters. Now every `EMTR` descendant is collected depth-first and `VfxbEmitter.Depth` records nesting.
2. `VfxbReader.ReadSiblingChain` followed the `G3PR` child chain into G3D data in 4 files. Child chains are now bounded by the header child count.
3. The `GRSN` child chain (`GRSR`, `GRRE`, `GRCE`, sometimes `GRSC`/`GRRI`/`GRCI`) is now read.
All 1,592 shipped files now load. There is still no automated test project; the check was a scratch loader over the whole corpus.
4. A tree-based writer must reproduce: per-kind `size` rules, `0x100`-aligned `EMTR` data, `0x1000`-aligned `G3PR` and `GRSR` data, parent-sibling spans that include nested children, and the `child_count` header word (all documented in section 1D).

### Method notes for the next session

- `ToolSearch` is needed to load the Ghidra MCP tools. Very large decompiles are saved to a file instead of returned; grep that file. Avoid decompiling matrix-heavy functions (`AreaLoopSystem::Draw`) just to read a few offsets: a register-access summary (`tools/exe/accs.py`) plus the SDK source is faster.
- The open Ghidra program matched both `Exefs121/main` and `Exefs111/main111` at the `nn::vfx2` addresses; analyses here used `Exefs121/main`.
- When switching to BotW, redo the same three steps first: the uniform-block copy size/source (`FUN_7100adb8f4` copies `0x750` bytes, i.e. data `[0, 0x750)`), a corpus of EMTR blocks, and a shader-use union. BotW sets are Yaz0-compressed (see `src/PtclSharp/Compression.cs` and `Container.ReadSesetlist`), so the corpus extractor needs a BotW loader before it can be reused.

### Update (later on 2026-10-04)

- New tools: `tools/exe/taint.py` (interprocedural pointer-taint trace; used to prove which bytes of the stripe and plugin payloads are read at all) and `tools/exe/overlays.py` (finds what `UpdateParams` copies from chunks into the **GPU field buffer**, a separate `0x160`-byte uniform buffer, not the EMTR block).
- The field-buffer slot order is an independent check on chunk member names. It exposed one error in this pass: `FCLN +0x04` is the curl-noise **speed** and `+0x10` the **influence** (an earlier revision had them swapped; both the CPU dataflow and the GPU copy order agree on the corrected assignment).
- `EP01` is fully traced (`calc_type`, `option`, `num_divide`, `connection_type`, alphas); `EP02`/`EP03` gained `calc_type`, `emitter_follow`, `dir_interpolate`, `history_air_resist`, `history_acceleration`, `history_vec_regulation`, `history_init_vec_rotate_cycle`, `static_param_x/y`. Bytes outside the traced read sets of the stripe payloads have **no reader in the engine**.
- Layout tables and tests (`src/PtclSharp/Layout`, `tests/PtclSharp.Tests`) were added and are generated from the doc tables (`tools/gen/gen_layouts.py`).

### TotK status definitions (agreed with the user, 2026-10-04)

`Confirmed` = purpose proven in the executable. `Unused` = no consumer exists in the engine (CPU scan, GPU read set, corpus), so it can be copied or dropped; 62 EMTR ranges (about 440 bytes) are `Unused`. `Unverified` = something consumes it but its purpose is not proven; 24 EMTR ranges remain: the shader-only fields (with a separate **Guess:** note, stored as `FieldDef.Hypothesis`) and the texture-slot record tails passed to the game-side texture resolver. TotK is considered complete at this level; the next phase is BotW (the user must open the BotW program in Ghidra first).

### BotW status (2026-10-04, second pass)

- BotW `EMTR` (0xA88 bytes) is fully mapped in `botw-emtr-offsets-ghidra.md` section 3 (286 rows, no gaps): 223 Confirmed from BotW 1.6.0 code or shader proof, 41 Unused, 18 Unverified (GPU-only, with Guess), 4 Paired. The first-pass BotW table and the BotW offset column of the TotK table were wrong in places (render-state bytes, loop timers, the `0x970..0x998` direction block, "light uniforms" = rotation parameters, pulse flags) and were replaced.
- BotW chunk payloads, `ESET`, `PRIM`, `G3NT` are in section 4 and `BotwLayouts.cs`; `FCOV` exists in BotW (slot `+0x270`), `FGWD` does not.
- Remaining Unverified chunk members are listed in `BotwLayouts.cs` (`unverified_*`): stripe members with no traced reader, `ESET` `+0x58/+0x5C`, PRIM array descriptors. Open question for porting: BotW and TotK share some slots with different meanings (`depth_write_enable` vs `depth_sort_ascending`, `emission_position_table_offset_scale` vs `emission_direction_table_offset_scale`); a converter needs an explicit alias table.
- Tests: `tests/PtclSharp.Tests/BotwCorpusTests.cs` (set `PTCL_BOTW_ROM`).

### BotW status update (third pass)

Stripe, field and primitive chunks were verified against BotW code (`EP01..EP04`, `FRND`, `FRN1`, `FMAG`, `FSPN`, `FCOL`, `FCOV`, `FCLN`, `FCSF`, `FPAD`, `PRIM`, `G3NT`), plus the file header checks in the `Resource` constructor. Only these remain below Confirmed: `EP04 +0x1C`, `PRIM`/`G3NT` are Confirmed, and the `EMTR` GPU-only fields (18 Unverified with Guess, `template_vertex_bias_z`, `emitter_name`). `ESET +0x58/+0x5C` and several stripe words are Unused (no reader in the effect library). BotW `0xB0..0xBC` is a GPU acceleration term (`0.5 * vec * t^2 * scale`), not the TotK CPU fallback.
