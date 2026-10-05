# BotW EMTR verification notes (working log)

> Superseded by `botw-emtr-offsets-ghidra.md` (generated from `tools/gen/botw_rows.py`); kept as the first-pass log.

Binary: BotW 1.6.0 Switch `main` (Ghidra image base `0x7100000000`). `D` = EMTR data base (`EmitterResource+0x10` / `Emitter+0x98`).
Status vocabulary matches the TotK doc. Only rows listed under **Confirmed** have been read in decompiled BotW code in this pass.

## Function index (BotW)

| Address | Role |
|---|---|
| `7100ad583c` | `Emitter::Initialize` |
| `7100ad73d0` | emitter per-frame calculate (`CalculateParticle` wrapper: timing, fade, emit, end-of-life) |
| `7100ad7084` | `EmitterCalculator::TryEmitParticle` |
| `7100ad69b0` | `Emitter::UpdateByEmit` |
| `7100ad6560` | `CreateResMatrix` (builds the emitter SRT matrices from the TRS fields) |
| `7100add24c` | `CalculateEmitCircle` |
| `7100adc8a8` | shader feature-flag builder |
| `7100ae1514` | scale animation value (key-anim + pulse) |
| `7100ae2264` | `CalculateRotationMatrix` |

## Confirmed in this pass

| Offset | Field | Evidence |
|---|---|---|
| `0x757` | seed mode (0 = global PRNG, 1 = ESET `+0x34`, 2 = fixed) | `ad583c`: switch on byte; mode 2 uses `D+0x760 * -0x2023e3cb` as seed. |
| `0x760` | fixed seed | same site. |
| `0x75B`,`0x75C` | fade-in flags (curve/scale) | `ad583c` sets initial fade value 0 when either is set; `ad73d0` ramps `Emitter+0x68` up to 1.0. |
| `0x76C` | fade-in time (frames, i32) | `ad73d0`: `0x68 += dt / (float)D[0x76c]`; <1 jumps straight to 1.0. |
| `0x754` | fade-out gate (when 0 together with caller flag, fade-out applies) | `ad73d0`: `param_6 = (D[0x754]==0) & param_6` before the fade-out test. |
| `0x755`,`0x756` | fade-out flags | `ad73d0`: fade-out runs if either is non-zero. |
| `0x768` | fade-out time (frames, i32) | `ad73d0`: `0x64 -= dt / (float)D[0x768]`; <1 ends the emitter. |
| `0x7F0` | finite-lifetime flag (0 = never ends) | `ad73d0` end test only evaluates `start+duration(+lifespan)` when non-zero. |
| `0x7F4` | emit start (frames) | `ad73d0`: `start = D[0x7f4]`. |
| `0x7FC` | emit duration (frames) | `ad73d0`: `end = start + D[0x7fc]`. |
| `0x7F8` | child emit timing (percent) | `ad73d0`: `D[0x7f8]/100 * parentLife` for child emitters. |
| `0x7E4` | child timing mode | `ad73d0`: child emitters use the timing branch only when non-zero. |
| `0x800`,`0x804` | emit rate (f32) / random percent (i32) | `ad7084`: `rate + rate * (pct/100) * rand`. |
| `0x808`,`0x80C` | emit interval / random range (i32) | `ad69b0`: `interval + 1 + (rand * range >> 32)`. |
| `0x758` | rebuild matrix per emit | `ad69b0` calls `CreateResMatrix` when non-zero. |
| `0x7F2` | distance-based emission | `ad7084`: non-zero selects the distance branch. |
| `0x824` | distance emit step (f32) | `ad7084`: `if (D[0x824] != 0) n = (int)(travel / D[0x824])`. |
| `0x828`,`0x82C`,`0x830` | distance clamp: min-travel threshold `0x830`, floor `0x828`, ceiling `0x82C` | `ad7084`: travel below `0x830` is zeroed; result clamped into `[0x828,0x82C]`. |
| `0x770..0x77B` / `0x77C..0x787` | emitter translation / random range (3 f32 each) | `ad6560`. |
| `0x788..0x793` / `0x794..0x79F` | emitter rotation / random range (3 f32 each) | `ad6560`. |
| `0x7A0..0x7AB` | emitter scale (3 f32) | `ad6560`. |
| `0x8A8` | endless lifetime flag (1 = never ends) | `ad73d0`: end test skipped when `== 1`. |
| `0x8B8` | particle lifespan (i32 frames) | `ad73d0`, `ae1514`. |
| `0x839` | angle mode (0 = fixed `0x848`, else time-varying) | `add24c`. |
| `0x840`,`0x848` | arc spread, base angle | `add24c`. |
| `0x85C`,`0x864` | circle radii X and Z | `add24c`. |
| `0x8AD`,`0x8AE`,`0x8AF` | random rotation reversal X/Y/Z | `ae2264`, `adc8a8` (feature flags `0x10000..0x40000`). |
| `0x8DC`,`0x8E1`,`0x8F4` | scale loop enable / random / rate | `ae1514`. |
| `0x9ED`,`0x9EE`,`0x9EF` | scale pulse enable / separate-Y pulse / waveform in high nibble (0 cosine, 1 saw, 2 square) | `ae1514`. |
| `0x7F1` | gravity coordinate flag | `adc8a8`: sets shader feature bit `8`. |
| `0x753` | feature variant (0..2) | `adc8a8`: selects a feature word from a 3-entry table. |
| `0x8AC` | sort mode (0 -> bit `0x20000000`, 1 -> `0x40000000`) | `adc8a8`. |
| `0xA58`,`0xA68`,`0xA78` | texture pattern mode per slot (1..3 feature bits, 4 = frame table) | `adc8a8`: bit pairs `0x10/0x20/0x40/0x80`, `0x100..0x800`, `0x1000..0x8000`; mode 4 also builds an index table. |
| `0xA5D,0xA5E,0xA5F, 0xA6D,0xA6E,0xA6F, 0xA7D,0xA7E,0xA7F`, `0x8B3`, `0x8B4` | flag bytes that each set a shader feature bit | `adc8a8`. (Previously uncovered.) |
| `0x752` | emitter calc type | `ad73d0`: `0` -> CPU particle calc (`ad7ea0`); non-zero copies the particle count (GPU path); `2` plus caller flag also runs the CPU path. |
| `0x9F8`,`0xA18`,`0xA38` | texture GUIDs slot 0/1/2 (u64) | `ae3e54`: each passed to the texture lookup `ae42a8`, results stored at `EmitterResource+0x48/0x50/0x58` (init `-1`). |
| `0x878` | mesh primitive index (u64, `-1` = none) | `ae3e54`: `-1` -> null, else primitive lookup `ae495c` stored at `+0x80`. |
| `0x8A9`,`0x8D0` | trimming-primitive flag / index | `ae3e54`: lookup runs only when flag non-zero and index != `-1`; stored at `+0x78`. |
| `0x8C8` | G3D primitive index (u64) | `adb7c0` (`EmitterResource::Setup`): `GetG3dPrimitive` when index != `-1` and the emitter-type word `+0x330` is 0 or > 3. |
| `0x914`,`0x91C`,`0x924` | shader indices normal / pass 1 / pass 2 (`-1` = none) | `adb7c0`: `ShaderManager::GetShader(idx)` into `+0x340/+0x348/+0x350`. |
| `0x918` | compute shader index (`-1` = none) | `adb7c0`: `shaderTable + idx * 0x40` into `+0x358`. |
| `0x8A8`,`0x7F0` (ESET aggregation) | per-set flags: any endless emitter, any non-finite emitter | `ae44ec`: `0x8A8 != 0` sets ESET-record `+0x2A`; `0x7F0 == 0` sets `+0x29`. |
