# BotW `EMTR` Executable Evidence & Field Map

This document records the exact byte offsets, types, executable behaviors, and Ghidra Switch ARM64 decompilation citations for Breath of the Wild (`nn::vfx`, VFXB v20).

Offsets are relative to the serialized `EMTR` node's data start (`EMTR.node + dataRelativeOffset`).

**Status (2026-10): 286 rows cover all 0xA88 bytes: Confirmed (BotW code or shader proof) 225, Unused 41, Unverified 18 (GPU-only fields with a Guess, as in the TotK map), Paired 2 (`emitter_name`, `template_vertex_bias_z`).** Section 3 is generated from `tools/gen/botw_rows.py` + `tools/gen/botw_overrides.py`; the chunk layouts (section 4) are `src/PtclSharp/Layout/BotwLayouts.cs`. The "BotW offset" column of the TotK document is a per-segment guess and must not be used to find BotW fields.

---

## 1. Resource-Tree & Allocation Architecture

### A. EmitterSet Node (`ESET`)
* **Fixed Data Size**: `0x60` bytes.
* **Name**: `char[64]` at `ESET.data + 0x10` (null-terminated C-string; matched in `nn::vfx::Resource::SearchEmitterSetId` at `0x7100ae4a10`).
* **Declared Emitter Count**: Signed 32-bit integer (`int32`) at `ESET.data + 0x50` (read by `nn::vfx::Resource::InitializeEmitterSetResource` at `0x7100ae44ec:L24`).
* **Runtime Emitter Record**: `0x3E0` bytes per emitter plus a `0x20`-byte header allocated by `0x7100ae44ec:L72`.

### B. Emitter Node (`EMTR`)
* **Fixed Data Size**: `0x0A88` bytes (2,696 bytes).
* **Header / Body Split**: Fixed header occupies `0x00`–`0x50`; the serialized `ResEmitter` body begins at `EMTR.data + 0x50`. The pointer to `EMTR.data + 0x50` is stored directly into `EmitterResource + 0x18`.
* **Child Nodes**: Child `EMTR` nodes share the exact same `0x0A88`-byte layout as parent `EMTR` nodes.
* **Constant Buffer Staging**: In `FUN_7100adb8f4` (lines 631–641), the uniform constant buffer staging block is uploaded verbatim to the GPU via `memcpy(pvVar2, dataStart, 0x750)` (1,872 bytes).

---

## 2. Attribute chunk handlers (`ResolveBinaryData`, 0x7100adcca8)

`nn::vfx::EmitterResource::ResolveBinaryData` walks the attribute chain of each EMTR (`attribute_rel`, then each chunk's `sibling_rel`) and stores the payload pointer of each recognised fourcc in a fixed `EmitterResource` slot:

| Slot | Fourcc | Slot | Fourcc | Slot | Fourcc |
|:---:|:---|:---:|:---|:---:|:---|
| `+0x248` | `FRND` | `+0x290` | `EAES` | `+0x2d8` | `EAOV` |
| `+0x250` | `FRN1` | `+0x298` | `EAER` | `+0x2e0` | `EADV` |
| `+0x258` | `FMAG` | `+0x2a0` | `EAET` | `+0x2e8` | `EASL` |
| `+0x260` | `FSPN` | `+0x2a8` | `EAC0` | `+0x2f0` | `EASS` |
| `+0x268` | `FCOL` | `+0x2b0` | `EAC1` | `+0x2f8` | `EAGV` |
| `+0x270` | `FCOV` | `+0x2b8` | `EATR` | `+0x300` | `CSDP` (size `nodeSize - 0x20` at `+0x308`) |
| `+0x278` | `FPAD` | `+0x2c0` | `EAPL` | `+0x310` | `CADP` |
| `+0x280` | `FCLN` | `+0x2c8` | `EAA0` | `+0x318` | `CUDP` |
| `+0x288` | `FCSF` | `+0x2d0` | `EAA1` | `+0x338` | `EP01`..`EP04` (type 1..4 stored at `+0x330`) |

Corrections to the previous version of this section: `FCOV` **is** handled in BotW (slot `+0x270`) and ships in the corpus (196 chunks); `FGWD` is the only TotK chunk with no BotW counterpart.

## 3. Byte-by-Byte Field Map (re-verified against the BotW 1.6.0 Switch executable)

Offsets are relative to the start of the EMTR data block (0xA88 bytes). Status: **Confirmed** = proven in BotW code, **Paired** = same role as a proven TotK field at an aligned position, **Unverified** = consumed but purpose unproven, **Unused** = no consumer found.
Function addresses are Switch image addresses `0x71xxxxxxxx`. "body+N" means `EmitterResource+0x18` (= data + 0x50) plus N. The previous version of this table contained errors that this pass corrected (loop timers order, render-state bytes, the 0x974 block, "light uniforms", pulse flags).

| BotW Offset | Size (B) | Type | Field | Status | Evidence |
|:---:|:---:|:---:|:---|:---:|:---|
| `0x000`–`0x00F` | 16 | `bytes[16]` | `unused_000_00F` | Unused | Zero in all 8,244 corpus emitters; this is the runtime prefix of the data block (TotK has the same unused prefix). The fourcc/size/version words the previous map placed here are the node header, which sits before the data block. Corpus: zero in all 8244 emitters. |
| `0x010`–`0x04F` | 64 | `char[64]` | `emitter_name` | Paired | NUL-terminated ASCII name (corpus: 586 distinct names in 8,244 emitters). The engine library has no emitter-name search (only `SearchEmitterSetId` for sets), so the field is editor/tool metadata; kept Paired with TotK, which stores it in the same place. |
| `0x050` | 4 | `uint32` | `runtime_shader_flags_word0` | Confirmed | `EmitterResource::UpdateParams` (0x7100adb8f4) overwrites it with the first word built by 0x7100adc8a8 (shader feature bits); the GPU reads it only through bit tests (`& 0x80000`, `& 0x10000` ...). Zero in every file. |
| `0x054` | 4 | `uint32` | `runtime_shader_flags_word1` | Confirmed | Second word built by 0x7100adc8a8 and stored by `UpdateParams`; GPU tests bit `& 1`. Zero in every file. |
| `0x058`–`0x05B` | 4 | `bytes[4]` | `unused_058_05B` | Unused | Zero in every file; no GPU read and no BotW writer or reader found (TotK word2 has no BotW counterpart). Corpus: zero in all 8244 emitters. |
| `0x05C` | 4 | `uint32` | `runtime_attribute_word` | Confirmed | `UpdateParams` stores the first word of attribute chunk slot `+0x288` here (`D[0x5c] = chunk[0]`). Zero in every file. |
| `0x060` | 4 | `uint32` | `color0_key_count` | Confirmed | Key count (0..8) of the table at 0x3C0. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x064` | 4 | `uint32` | `alpha0_key_count` | Confirmed | Key count (0..8) of the table at 0x440. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x068` | 4 | `uint32` | `color1_key_count` | Confirmed | Key count (0..8) of the table at 0x4C0. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x06C` | 4 | `uint32` | `alpha1_key_count` | Confirmed | Key count (0..8) of the table at 0x540. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x070` | 4 | `uint32` | `scale_key_count` | Confirmed | Key count (0..8) of the table at 0x600. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x074` | 4 | `uint32` | `track5_key_count` | Confirmed | Key count (0..8) of the table at 0x680. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2. |
| `0x078`–`0x07F` | 8 | `bytes[8]` | `unused_078_07F` | Unused | Zero in every file; no GPU read and no BotW reader found (TotK 0x0A8..0x0AF is the same hole after its larger key-count table). Corpus: zero in all 8244 emitters. |
| `0x080` | 4 | `float` | `runtime_loop_track0_rate` | Confirmed | `UpdateParams` writes `float(D[0x8E4])` when `D[0x8D8] != 0`, otherwise 0.0 (color0 loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file. |
| `0x084` | 4 | `float` | `runtime_loop_track1_rate` | Confirmed | `UpdateParams` writes `float(D[0x8E8])` when `D[0x8D9] != 0`, otherwise 0.0 (alpha0 loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file. |
| `0x088` | 4 | `float` | `runtime_loop_track2_rate` | Confirmed | `UpdateParams` writes `float(D[0x8EC])` when `D[0x8DA] != 0`, otherwise 0.0 (color1 loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file. |
| `0x08C` | 4 | `float` | `runtime_loop_track3_rate` | Confirmed | `UpdateParams` writes `float(D[0x8F0])` when `D[0x8DB] != 0`, otherwise 0.0 (alpha1 loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file. |
| `0x090` | 4 | `float` | `runtime_loop_track4_rate` | Confirmed | `UpdateParams` writes `float(D[0x8F4])` when `D[0x8DC] != 0`, otherwise 0.0 (scale loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file. |
| `0x094` | 4 | `float` | `runtime_loop_track0_random_enable` | Confirmed | `UpdateParams` writes 1.0 when `D[0x8DD] != 0` (color0 random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file. |
| `0x098` | 4 | `float` | `runtime_loop_track1_random_enable` | Confirmed | `UpdateParams` writes 1.0 when `D[0x8DE] != 0` (alpha0 random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file. |
| `0x09C` | 4 | `float` | `runtime_loop_track2_random_enable` | Confirmed | `UpdateParams` writes 1.0 when `D[0x8DF] != 0` (color1 random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file. |
| `0x0A0` | 4 | `float` | `runtime_loop_track3_random_enable` | Confirmed | `UpdateParams` writes 1.0 when `D[0x8E0] != 0` (alpha1 random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file. |
| `0x0A4` | 4 | `float` | `runtime_loop_track4_random_enable` | Confirmed | `UpdateParams` writes 1.0 when `D[0x8E1] != 0` (scale random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file. |
| `0x0A8`–`0x0AF` | 8 | `bytes[8]` | `unused_0A8_0AF` | Unused | Zero in every file; no GPU read and no BotW reader found. Corpus: zero in all 8244 emitters. |
| `0x0B0`–`0x0BB` | 12 | `float[3]` | `gpu_accel_dir_xyz` | Confirmed | Vertex shaders (for example 0118E544_v1.V line 1803): `position += 0.5 * (t * t * data[11].w) * data[11].xyz` with `t = age + offset`, i.e. a constant-acceleration displacement along this vector. Default (0, -1, 0) in 97% of emitters. TotK holds the same bytes at 0xE0 and reads them on the CPU as the stationary-delta fallback; the BotW CPU code does not read them. |
| `0x0BC` | 4 | `float` | `gpu_accel_scale` | Confirmed | Vertex shaders: `t * t * data[11].w` multiplies the acceleration vector (see `gpu_accel_dir_xyz`). 0.0109 is the most common value (about 9.8 / 30^2, gravity per frame squared). |
| `0x0C0` | 4 | `float` | `velocity_attenuation_per_frame` | Confirmed | `CalculateParticleBehavior` (0x7100ae0e34): `if (D[0xc0] >= 1.0) velocity unchanged, else velocity *= powf(D[0xc0], frame_time)` (read as `body+0x70`). GPU reads `log2(F)` and `F == 1.0` for the compute path. |
| `0x0C4`–`0x0CF` | 12 | `bytes[12]` | `unused_0C4_0CF` | Unused | Zero in every file; no GPU read and no BotW reader found. Corpus: zero in all 8244 emitters. |
| `0x0D0` | 4 | `float` | `template_vertex_bias_x` | Confirmed | GPU vertex shaders (00C5365F_v1.V): `fma(0.5, data[13].x, templateVertex.x)` where `data[13].x` is dword 0xD0. |
| `0x0D4` | 4 | `float` | `template_vertex_bias_y` | Confirmed | GPU vertex shaders: `fma(0.5, data[13].y, templateVertex.y)` (dword 0xD4). |
| `0x0D8` | 4 | `float` | `template_vertex_bias_z` | Paired | GPU vertex stage `fma(0.5, F, t)` on the template vertex position (0xD0/0xD4 in 6,852 programs, 0xD8 through varied arithmetic); same position, read pattern and distribution as TotK 0x108. |
| `0x0DC`–`0x0DF` | 4 | `bytes[4]` | `unused_0DC_0DF` | Unused | Zero in every file; no GPU read (TotK 0x10C, the analogous float, is nonzero there). Corpus: zero in all 8244 emitters. |
| `0x0E0` | 4 | `float` | `waveform0_amplitude` | Confirmed | amplitude of waveform 0 (multiplies the alpha/scale-X waveform). Read as `body+0x90` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0E4` | 4 | `float` | `waveform1_amplitude` | Confirmed | amplitude of waveform 1 (scale Y). Read as `body+0x94` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0E8` | 4 | `float` | `waveform0_period` | Confirmed | period divisor of waveform 0. Read as `body+0x98` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0EC` | 4 | `float` | `waveform1_period` | Confirmed | period divisor of waveform 1. Read as `body+0x9C` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0F0` | 4 | `float` | `waveform0_random_phase_scale` | Confirmed | multiplier of the per-particle random phase of waveform 0. Read as `body+0xA0` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0F4` | 4 | `float` | `waveform1_random_phase_scale` | Confirmed | multiplier of the per-particle random phase of waveform 1. Read as `body+0xA4` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0F8` | 4 | `float` | `waveform0_time_offset` | Confirmed | time offset of waveform 0. Read as `body+0xA8` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x0FC` | 4 | `float` | `waveform1_time_offset` | Confirmed | time offset of waveform 1. Read as `body+0xAC` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`). |
| `0x100` | 4 | `float` | `unverified_100` | Unverified | Fragment stage only (535 programs): `t = t * F`. Role not established. **Guess:** Likely an intensity/brightness-style multiplier on a fragment quantity (TotK analogue 0x130); low confidence. |
| `0x104` | 4 | `float` | `unverified_104` | Unverified | Fragment stage only (534 programs): `t = t * F`. Role not established. **Guess:** Likely an intensity/brightness-style multiplier on a fragment quantity (TotK analogue 0x134); low confidence. |
| `0x108`–`0x10F` | 8 | `bytes[8]` | `unused_108_10F` | Unused | Zero in every file; no GPU read and no BotW reader found. Corpus: zero in all 8244 emitters. |
| `0x110`–`0x19F` | 144 | `bytes[0x90]` | `tex0_flipbook_runtime_block` | Confirmed | Runtime pattern table of texture slot 0. `0x7100adc8a8` (mode 4 only, `D[0xA58] == 4`) writes `D[0x110] = D[0x118]` (frame count float) and fills the int table at `D[0x120 + 4*i] = i` for `i < count`; the GPU truncates the first two dwords (`trunc(F)`). Mostly zero in files; slot 2 is almost always empty. |
| `0x1A0`–`0x22F` | 144 | `bytes[0x90]` | `tex1_flipbook_runtime_block` | Confirmed | Runtime pattern table of texture slot 1. `0x7100adc8a8` (mode 4 only, `D[0xA68] == 4`) writes `D[0x1A0] = D[0x1A8]` (frame count float) and fills the int table at `D[0x1B0 + 4*i] = i` for `i < count`; the GPU truncates the first two dwords (`trunc(F)`). Mostly zero in files; slot 2 is almost always empty. |
| `0x230`–`0x2BF` | 144 | `bytes[0x90]` | `tex2_flipbook_runtime_block` | Confirmed | Runtime pattern table of texture slot 2. `0x7100adc8a8` (mode 4 only, `D[0xA78] == 4`) writes `D[0x230] = D[0x238]` (frame count float) and fills the int table at `D[0x240 + 4*i] = i` for `i < count`; the GPU truncates the first two dwords (`trunc(F)`). Mostly zero in files; slot 2 is almost always empty. |
| `0x2C0`–`0x30F` | 80 | `bytes[0x50]` | `tex0_uniform_block` | Confirmed | UV transform block of texture slot 0, copied verbatim to the GPU. `UpdateParams` proves its structure: when `D[0xA59]` (scroll) is 0 it zeroes +0x00..+0x14; when `D[0xA5A]` (rotate) is 0 it zeroes +0x30..+0x38; when `D[0xA5B]` (scale) is 0 it writes 1.0 to +0x20/+0x24 and 0 to +0x18/+0x1C/+0x28/+0x2C; it writes the two floats selected by `D[0xA5C]` (domain mode, value < 4, table at 0x7101e787a0/b0) to +0x40/+0x44. |
| `0x310`–`0x35F` | 80 | `bytes[0x50]` | `tex1_uniform_block` | Confirmed | UV transform block of texture slot 1, copied verbatim to the GPU. `UpdateParams` proves its structure: when `D[0xA69]` (scroll) is 0 it zeroes +0x00..+0x14; when `D[0xA6A]` (rotate) is 0 it zeroes +0x30..+0x38; when `D[0xA6B]` (scale) is 0 it writes 1.0 to +0x20/+0x24 and 0 to +0x18/+0x1C/+0x28/+0x2C; it writes the two floats selected by `D[0xA6C]` (domain mode, value < 4, table at 0x7101e787a0/b0) to +0x40/+0x44. |
| `0x360`–`0x3AF` | 80 | `bytes[0x50]` | `tex2_uniform_block` | Confirmed | UV transform block of texture slot 2, copied verbatim to the GPU. `UpdateParams` proves its structure: when `D[0xA79]` (scroll) is 0 it zeroes +0x00..+0x14; when `D[0xA7A]` (rotate) is 0 it zeroes +0x30..+0x38; when `D[0xA7B]` (scale) is 0 it writes 1.0 to +0x20/+0x24 and 0 to +0x18/+0x1C/+0x28/+0x2C; it writes the two floats selected by `D[0xA7C]` (domain mode, value < 4, table at 0x7101e787a0/b0) to +0x40/+0x44. |
| `0x3B0` | 4 | `float` | `particle_color_rgb_scale` | Confirmed | Read as `body+0x360` by the color evaluator (0x7100ae19ac): `rgb = key_rgb * this * emitterColor * ...`; applies to the three color channels only (alpha uses a separate factor). |
| `0x3B4`–`0x3BF` | 12 | `bytes[12]` | `unused_3B4_3BF` | Unused | Zero in every file; no GPU read and no BotW reader found. Corpus: zero in all 8244 emitters. |
| `0x3C0`–`0x43F` | 128 | `float[8][4]` | `kf_color0` | Confirmed | Eight keys (value xyz or x, time w). `UpdateParams` copies the constant (0x9A8) into key 0 when mode `D[0x9A4] == 0` and pads unused keys after `color0_key_count`; `Calculate8KeyAnim` evaluates them (0x7100ae19ac) when the mode is 2; mode 3 of color picks a discrete key by `floor(life * count)` from the same table (0x3C0 + 0x10*idx). |
| `0x440`–`0x4BF` | 128 | `float[8][4]` | `kf_alpha0` | Confirmed | Eight keys (value xyz or x, time w). `UpdateParams` copies the constant (0x9B4) into key 0 when mode `D[0x9A6] == 0` and pads unused keys after `alpha0_key_count`; `Calculate8KeyAnim` evaluates them (0x7100ae19ac) when the mode is 2; mode 3 of color picks a discrete key by `floor(life * count)` from the same table (0x440 + 0x10*idx). |
| `0x4C0`–`0x53F` | 128 | `float[8][4]` | `kf_color1` | Confirmed | Eight keys (value xyz or x, time w). `UpdateParams` copies the constant (0x9B8) into key 0 when mode `D[0x9A5] == 0` and pads unused keys after `color1_key_count`; `Calculate8KeyAnim` evaluates them (0x7100ae19ac) when the mode is 2; mode 3 of color picks a discrete key by `floor(life * count)` from the same table (0x4C0 + 0x10*idx). |
| `0x540`–`0x5BF` | 128 | `float[8][4]` | `kf_alpha1` | Confirmed | Eight keys (value xyz or x, time w). `UpdateParams` copies the constant (0x9C4) into key 0 when mode `D[0x9A7] == 0` and pads unused keys after `alpha1_key_count`; `Calculate8KeyAnim` evaluates them (0x7100ae19ac) when the mode is 2; mode 3 of color picks a discrete key by `floor(life * count)` from the same table (0x540 + 0x10*idx). |
| `0x5C0` | 4 | `float` | `unverified_5C0` | Unverified | Not read by any shader (GPU read set starts at 0x5C4) and no BotW reader found; nonzero (10.0) in 8 files. TotK analogue 0x890 is read there. **Guess:** Possibly a scale paired with another static-block value for a vertex offset; very low confidence. |
| `0x5C4` | 4 | `float` | `unverified_5C4` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x894. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the radius/scale of a fixed multi-tap sample or offset pattern (the constants 0.24, 0.48 and 0.1 are tap offsets), such as a distortion or blur kernel. |
| `0x5C8` | 4 | `float` | `unverified_5C8` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x898. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the start of a depth or distance fade ramp (soft-particle style); the paired value at 0x89C is its end. |
| `0x5CC` | 4 | `float` | `unverified_5CC` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x89C. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the end of a depth or distance fade ramp (soft-particle style); the paired value at 0x898 is its start. |
| `0x5D0` | 4 | `float` | `unverified_5D0` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x8A0. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the near bound of a vertex-computed distance (camera distance) fade; paired with 0x8A4. |
| `0x5D4` | 4 | `float` | `unverified_5D4` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x8A4. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the far bound of a vertex-computed distance fade; paired with 0x8A0. |
| `0x5D8` | 4 | `float` | `unverified_5D8` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x8A8. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the near bound of a second vertex-computed fade or scale ramp; paired with 0x8AC. |
| `0x5DC` | 4 | `float` | `unverified_5DC` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x8AC. Role not established (see the TotK row for the shader patterns). **Guess:** Likely the far bound of a second vertex-computed fade or scale ramp; paired with 0x8A8. |
| `0x5E0` | 4 | `float` | `unverified_5E0` | Unverified | GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x8B0. Role not established (see the TotK row for the shader patterns). **Guess:** Likely an alpha threshold used in a greater-or-equal test (a second alpha clip or an upper cut-off). |
| `0x5E4`–`0x5E7` | 4 | `bytes[4]` | `unused_5E4_5E7` | Unused | Zero in every file; no GPU read. Corpus: zero in all 8244 emitters. |
| `0x5E8` | 4 | `float` | `fragment_discard_threshold` | Confirmed | GPU fragment shaders (for example 00C5365F_v1.F line 444): `if (alpha <= data[94].z) discard;` where `data[94].z` is dword 0x5E8. |
| `0x5EC`–`0x5EF` | 4 | `bytes[4]` | `unused_5EC_5EF` | Unused | Zero in every file; no GPU read. Corpus: zero in all 8244 emitters. |
| `0x5F0` | 4 | `float` | `unverified_5F0` | Unverified | GPU-read dword (vertex stage in TotK); same position as TotK 0x8C0. Role not established. **Guess:** Likely a strength/scale of a vertex displacement (for example distortion, wind or normal offset); low confidence. |
| `0x5F4` | 4 | `float` | `unverified_5F4` | Unverified | GPU-read dword (fragment stage in TotK); same position as TotK 0x8C4. Role not established. **Guess:** Likely the reciprocal fade distance of a soft-particle depth fade (the programs that use it sample `sysDepthBufferTexture`). |
| `0x5F8`–`0x5FF` | 8 | `bytes[8]` | `unused_5F8_5FF` | Unused | Zero in every file; no GPU read. Corpus: zero in all 8244 emitters. |
| `0x600`–`0x67F` | 128 | `float[8][4]` | `kf_scale` | Confirmed | Scale keys xyz + time. `FUN_7100ae1514` evaluates them as `body+0x5b0` (`Calculate8KeyAnim`) when `scale_key_count >= 2`; `UpdateParams` pads unused keys. |
| `0x680`–`0x6FF` | 128 | `float[8][4]` | `kf_track5` | Confirmed | Sixth key table (xyz + time), padded by `UpdateParams` with `track5_key_count` (0x74); GPU-read at `0x680..0x6FC`. No BotW CPU reader was found for it. |
| `0x700`–`0x70B` | 12 | `float[3]` | `rotation_initial_xyz` | Confirmed | `UpdateParams` ends by copying D[0x700..0x70B] to `EmitterResource+0x320`; `0x7100ada3cc` stores that triple into the per-particle rotation array (`param_6[5]`) at emission, so it is the initial rotation of each particle. Only lanes enabled by 0x8B0..0x8B2 are kept (`UpdateParams` zeroes lane i of the four rotation vec4s when the lane flag is 0). Not read by any shader. |
| `0x70C`–`0x70F` | 4 | `bytes[4]` | `unused_70C_70F` | Unused | Zero in every file; padding of the vec4. Corpus: zero in all 8244 emitters. |
| `0x710`–`0x71B` | 12 | `float[3]` | `rotation_initial_random_xyz` | Confirmed | `CalculateRotationMatrix` (0x7100ae2264): `initial + body[0x6c0..0x6c8] * random` per axis. |
| `0x71C`–`0x71F` | 4 | `bytes[4]` | `unused_71C_71F` | Unused | Zero in every file; padding of the vec4. Corpus: zero in all 8244 emitters. |
| `0x720`–`0x72B` | 12 | `float[3]` | `rotation_add_xyz` | Confirmed | `CalculateRotationMatrix`: per-frame rotation added, `body[0x6d0..0x6d8]` plus the random term, scaled by time (with attenuation). |
| `0x72C` | 4 | `float` | `rotation_add_attenuation` | Confirmed | `CalculateRotationMatrix`: `powf(body[0x6dc], t)` and `(1 - a) / (1 - base)` give the attenuated accumulated rotation. |
| `0x730`–`0x73B` | 12 | `float[3]` | `rotation_add_random_xyz` | Confirmed | `CalculateRotationMatrix`: `body[0x6e0..0x6e8] * (r1 + r2) * 0.5` added to the per-frame rotation. |
| `0x73C`–`0x73F` | 4 | `bytes[4]` | `unused_73C_73F` | Unused | Zero in every file; padding of the vec4. Corpus: zero in all 8244 emitters. |
| `0x740` | 4 | `float` | `unverified_740` | Unverified | GPU vertex read (`740`). Same position relative to the rotation block as TotK 0xC50. Role not established. **Guess:** Likely the maximum length used to clamp and normalize a velocity/ribbon stretch in the vertex stage (TotK analogue). |
| `0x744` | 4 | `float` | `unverified_744` | Unverified | GPU vertex read (`744`). Same position relative to the rotation block as TotK 0xC54. Role not established. **Guess:** Likely the minimum length (lower clamp) used when normalizing a velocity/ribbon stretch in the vertex stage (TotK analogue). |
| `0x748`–`0x74F` | 8 | `bytes[8]` | `unused_748_74F` | Unused | Zero in every file; beyond the GPU copy (0x750 bytes) and no BotW reader found. The previous map placed `sim_flags` and sort/velocity bytes here; the BotW code reads them at 0x750..0x753. Corpus: zero in all 8244 emitters. |
| `0x750` | 1 | `uint8` | `sim_flags` | Confirmed | Emitter draw gate (`FUN_7100aca518`, 0x7100aca538): the emitter is drawn only when this byte is non-zero together with emitter flag `+2`, fade value `+0x64 > 0` and live particles (or a stripe/child type). TotK copies the same byte into a runtime flag bit. |
| `0x751` | 1 | `uint8` | `particle_sort_mode_index` | Confirmed | `FUN_7100ad99dc`: passed as the `ParticleSortType` argument of `System::GetSortedParticleList`; non-zero selects the sorted draw path (0x7100ad9900). |
| `0x752` | 1 | `uint8` | `emitter_calc_type` | Confirmed | `0x7100ad73d0`: `0` runs the CPU particle calculation (`0x7100ad7ea0`), non-zero copies the particle count (GPU path), `2` with the caller flag also runs the CPU path. `0x7100ad5a74` allocates CPU/GPU particle buffers by this value (2 aligns to 32 and drops the CPU arrays). |
| `0x753` | 1 | `uint8` | `velocity_coord` | Confirmed | Particle coordinate mode 0..2: `0x7100ad7ea0` / `0x7100ada3cc` / `0x7100ae0e34` use the emitter matrix for 0, per-particle basis arrays for 1 (gravity normalizes the basis) and 2 (matrix rows plus translation); `0x7100ad5a74` allocates the extra basis arrays when non-zero; `0x7100adc8a8` maps it through a three-entry table into shader flag word 1. |
| `0x754` | 1 | `uint8` | `fade_emit_stop` | Confirmed | `0x7100ad73d0`: during fade-out `param_6 = (D[0x754] == 0) & param_6`, and `TryEmitParticle` only runs while `param_6` is set, so a non-zero value stops emission while the emitter fades out. |
| `0x755` | 1 | `uint8` | `fade_out_curve` | Confirmed | Either 0x755 or 0x756 non-zero enables fade-out in `0x7100ad73d0`; `0x7100ad8854` multiplies the alpha factor (`Emitter+0x64`) when 0x755 is set. |
| `0x756` | 1 | `uint8` | `fade_out_scale` | Confirmed | See 0x755; `0x7100ad8854` multiplies the scale factor (`Emitter+0x64`) when 0x756 is set. |
| `0x757` | 1 | `uint8` | `seed_source` | Confirmed | `Emitter::Initialize` (0x7100ad583c): 0 = draw from the global generator, 1 = ESET `+0x34`, 2 = `D[0x760] * -0x2023e3cb`. |
| `0x758` | 1 | `uint8` | `update_matrix_by_emit` | Confirmed | `Emitter::UpdateByEmit` (0x7100ad69b0) calls `CreateResMatrix` (0x7100ad6560) after each emit when non-zero. |
| `0x759`–`0x75A` | 2 | `bytes[2]` | `unused_759_75A` | Unused | Nonzero in the corpus but no reader found (TotK 0xCA6..0xCA7 is also unused). Corpus: nonzero in 360 of 8244 emitters. |
| `0x75B` | 1 | `uint8` | `fade_in_curve` | Confirmed | `Initialize` sets the initial fade value to 0 when this or 0x75C is set; `0x7100ad73d0` ramps `Emitter+0x68` by `dt / D[0x76c]`; `0x7100ad8854` applies it to the alpha factor. |
| `0x75C` | 1 | `uint8` | `fade_in_scale` | Confirmed | See 0x75B; `0x7100ad8854` applies the ramp to the scale factor when set. |
| `0x75D`–`0x75F` | 3 | `bytes[3]` | `unused_75D_75F` | Unused | Zero in every file; no reader found. Corpus: zero in all 8244 emitters. |
| `0x760` | 4 | `uint32` | `fixed_seed` | Confirmed | `Initialize`: seed = `D[0x760] * -0x2023e3cb` when `seed_source == 2`. |
| `0x764` | 4 | `uint32` | `draw_path` | Confirmed | `FUN_7100ac99d0` (CreateEmitter, 0x7100ac9b08) stores it to `Emitter+0x3b0`; `FUN_7100aca518` draws the emitter only when `(1 << (Emitter+0x3b0 & 0x1f)) & drawPathMask` is non-zero. |
| `0x768` | 4 | `int32` | `fade_out_time` | Confirmed | `0x7100ad73d0`: `Emitter+0x64 -= dt / (float)D[0x768]`; a value below 1 ends the emitter immediately. |
| `0x76C` | 4 | `int32` | `fade_in_time` | Confirmed | `0x7100ad73d0`: `Emitter+0x68 += dt / (float)D[0x76c]`; a value below 1 jumps to 1.0. |
| `0x770`–`0x77B` | 12 | `float[3]` | `emitter_trans_xyz` | Confirmed | `CreateResMatrix` (0x7100ad6560): translation base. |
| `0x77C`–`0x787` | 12 | `float[3]` | `emitter_trans_rnd` | Confirmed | `CreateResMatrix`: translation random range (`rand * 2 - 1` times this). |
| `0x788`–`0x793` | 12 | `float[3]` | `emitter_rot_xyz` | Confirmed | `CreateResMatrix`: Euler rotation base, wrapped into [-pi, pi] by the shared range-reduction constants and evaluated with the sine/cosine polynomials. |
| `0x794`–`0x79F` | 12 | `float[3]` | `emitter_rot_rnd` | Confirmed | `CreateResMatrix`: rotation random range. |
| `0x7A0`–`0x7AB` | 12 | `float[3]` | `emitter_scale_xyz` | Confirmed | `CreateResMatrix`: scale applied to the rotation basis. |
| `0x7AC`–`0x7B7` | 12 | `float[3]` | `emitter_color0_rgb` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x5e4`; `0x7100ad8854` multiplies the animated emitter color0 by it when filling the dynamic uniform block. |
| `0x7B8` | 4 | `float` | `emitter_color0_alpha` | Confirmed | Copied to `Emitter+0x614` (0x7100ad5a74) and multiplied into the dynamic block alpha (0x7100ad8854). |
| `0x7BC`–`0x7C7` | 12 | `float[3]` | `emitter_color1_rgb` | Confirmed | Copied to `Emitter+0x5f0` and multiplied into the dynamic block color1 (0x7100ad8854). |
| `0x7C8` | 4 | `float` | `emitter_color1_alpha` | Confirmed | Copied to `Emitter+0x620` and multiplied into the dynamic block alpha1 (0x7100ad8854). |
| `0x7CC`–`0x7D7` | 12 | `bytes[12]` | `unused_7CC_7D7` | Unused | No reader found. The bytes hold floats in the corpus (0x7D0 is -1.0 in 94% of emitters); TotK 0xD1C..0xD27 is the same unused hole. Corpus: nonzero in 8244 of 8244 emitters. |
| `0x7D8` | 1 | `uint8` | `inherit_parent_velocity` | Confirmed | `0x7100ad9bc0` (child particle setup): adds the parent particle velocity times `D[0x7e8]` to the child particle velocity. |
| `0x7D9` | 1 | `uint8` | `inherit_parent_scale` | Confirmed | `0x7100ad9bc0` (child particle setup): evaluates the parent scale (`FUN_7100ae1514`) times `D[0x7ec]` into the child scale. |
| `0x7DA` | 1 | `uint8` | `inherit_parent_rotation` | Confirmed | `0x7100ad9bc0` (child particle setup): writes the parent rotation matrix result (`CalculateRotationMatrix`). |
| `0x7DB` | 1 | `bytes[1]` | `unused_7DB` | Unused | Zero in every file; no reader found (TotK 0xD33). Corpus: zero in all 8244 emitters. |
| `0x7DC` | 1 | `uint8` | `inherit_parent_color0_rgb` | Confirmed | `0x7100ad9bc0`: runs the parent color0 evaluator (`0x7100ae19ac`) into the child color0 rgb. |
| `0x7DD` | 1 | `uint8` | `inherit_parent_color1_rgb` | Confirmed | `0x7100ad9bc0`: runs the parent color1 evaluator (`0x7100ae1e08`) into the child color1 rgb. |
| `0x7DE` | 1 | `uint8` | `inherit_parent_alpha0` | Confirmed | `0x7100ad9bc0`: copies the parent alpha0 into the child; `0x7100ad8854` additionally multiplies it each frame when 0x7E2 is set. |
| `0x7DF` | 1 | `uint8` | `inherit_parent_alpha1` | Confirmed | `0x7100ad9bc0`: copies the parent alpha1 into the child; `0x7100ad8854` multiplies it each frame when 0x7E3 is set. |
| `0x7E0` | 1 | `bytes[1]` | `unused_7E0` | Unused | Nonzero in the corpus but no reader found (TotK 0xD38 is also unused). Corpus: nonzero in 116 of 8244 emitters. |
| `0x7E1` | 1 | `uint8` | `child_pre_draw` | Confirmed | `EmitterSet::Draw` reads it at 0x7100aca6e8 and 0x7100aca798 to draw the child before or after the parent. |
| `0x7E2` | 1 | `uint8` | `inherit_parent_alpha0_each_frame` | Confirmed | `0x7100ad8854`: with 0x7DE also set, multiplies the emitter alpha factor by the parent alpha every frame. |
| `0x7E3` | 1 | `uint8` | `inherit_parent_alpha1_each_frame` | Confirmed | `0x7100ad8854`: with 0x7DF also set, multiplies the alpha1 factor by the parent alpha every frame. |
| `0x7E4` | 1 | `uint8` | `inherit_enable_emitter_particle` | Confirmed | Marks an emitter that follows a parent particle: `0x7100ad73d0` and `0x7100ad7ea0` use the parent-particle timing branch (`D[0x7f8] / 100 * parentLife`) and `0x7100ad9bc0` takes the inheritance branch only when it is non-zero. |
| `0x7E5`–`0x7E7` | 3 | `bytes[3]` | `unused_7E5_7E7` | Unused | Zero in every file; no reader found. Corpus: zero in all 8244 emitters. |
| `0x7E8` | 4 | `float` | `inherit_parent_velocity_rate` | Confirmed | `0x7100ad9bc0`: factor on the inherited parent velocity. |
| `0x7EC` | 4 | `float` | `inherit_parent_scale_rate` | Confirmed | `0x7100ad9bc0`: factor on the inherited parent scale. |
| `0x7F0` | 1 | `uint8` | `emit_loop_mode` | Confirmed | 0 = the emitter never ends, non-zero = it ends at `start + duration` (+ lifespan): `0x7100ad73d0` (end test), `0x7100ae44ec` (ESET aggregation sets the "has endless emitter" flag when 0). |
| `0x7F1` | 1 | `uint8` | `gravity_coord` | Confirmed | `0x7100ae0e34`: 0 applies gravity in world space, non-zero transforms it by the emitter/particle basis; `0x7100adc8a8` sets shader flag bit 8. |
| `0x7F2` | 1 | `uint8` | `emit_dist_enable` | Confirmed | `0x7100ad7084` takes the distance-based emission branch when non-zero; `0x7100ad6358` then uses `D[0x834]` as the particle capacity. |
| `0x7F3` | 1 | `uint8` | `designated_direction_transform_enable` | Confirmed | `0x7100ada3cc`: when non-zero the designated direction at 0x974 is transformed by the emitter basis (`0x7100ad529c`) before it contributes to velocity. |
| `0x7F4` | 4 | `uint32` | `emit_start_delay` | Confirmed | `0x7100ad73d0`: emission window start frame. |
| `0x7F8` | 4 | `uint32` | `child_emit_timing` | Confirmed | `0x7100ad73d0`: for child emitters `start = parentLife * D[0x7f8] / 100`. |
| `0x7FC` | 4 | `uint32` | `emit_duration` | Confirmed | `0x7100ad73d0`: window end = start + duration. |
| `0x800` | 4 | `float` | `emit_rate` | Confirmed | `TryEmitParticle` (0x7100ad7084): particles per frame; `UpdateParams` replaces it for shape types 2/13 (1.0), 5 (table by 0x83C), 6 (count 0x83D) and 15 (callback) while 0x874 == 0. |
| `0x804` | 4 | `int32` | `emit_rate_random_percent` | Confirmed | `TryEmitParticle`: `rate * (percent / 100) * random` subtracted from the rate (i32 here, byte in TotK). |
| `0x808` | 4 | `int32` | `emit_interval` | Confirmed | `UpdateByEmit` (0x7100ad69b0): interval = `D[0x808] + 1 + (random * D[0x80c] >> 32)`. |
| `0x80C` | 4 | `int32` | `emit_interval_random` | Confirmed | `UpdateByEmit`: random extension of the interval (see 0x808). |
| `0x810` | 4 | `float` | `emission_position_table_offset_scale` | Confirmed | `0x7100ada3cc`: when non-zero, adds `table[counter++ & 0x1ff].xy * value` to the emitted particle position X/Y (counter at `Emitter+0xa2`). TotK 0xD68 applies the same table step to the direction instead. |
| `0x814` | 4 | `float` | `gravity_scale` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x65c`; `0x7100ae0e34` multiplies the gravity vector by it (skipped when <= 0). |
| `0x818`–`0x823` | 12 | `float[3]` | `gravity_xyz` | Confirmed | `0x7100ae0e34`: acceleration `scale * g * dt`. |
| `0x824` | 4 | `float` | `emit_dist_unit` | Confirmed | `0x7100ad7084`: spacing of distance-based emission, `n = (int)(travel / unit)`. |
| `0x828` | 4 | `float` | `emit_dist_min` | Confirmed | `0x7100ad7084`: lower clamp of the travel length. |
| `0x82C` | 4 | `float` | `emit_dist_max` | Confirmed | `0x7100ad7084`: upper clamp of the travel length. |
| `0x830` | 4 | `float` | `emit_dist_margin` | Confirmed | `0x7100ad7084`: travel shorter than this counts as no movement. |
| `0x834` | 4 | `int32` | `emit_dist_particle_max` | Confirmed | `0x7100ad6358`: capacity used for distance-based emitters. |
| `0x838` | 1 | `uint8` | `shape_type` | Confirmed | `0x7100ada3cc` dispatches through a 16-entry function table (0x71024bd018) indexed by this byte: 1 circle (0x7100add248), 2 circle equally divided (0x7100add418), 3 circle fill (0x7100add704), 5 sphere equally 64-divided (0x7100addf24), 6 sphere equally 32-divided (0x7100ade3b8), 11 box fill (0x7100adf0a8), 15 primitive (0x7100adf59c); entries 0, 4, 7..10, 12..14 not named here. `UpdateParams` special-cases types 2, 5, 6, 13 and 15. |
| `0x839` | 1 | `uint8` | `shape_angle_mode` | Confirmed | `CalculateEmitCircle` (0x7100add24c): 0 uses the fixed phase 0x848, otherwise a time-varying phase. |
| `0x83A` | 1 | `uint8` | `shape_rot_mode` | Confirmed | `0x7100add9d8` (sphere): value 1 uses 0x844 as the polar spread (instead of 0x840) and applies the rotation variant 0x83E; other values sample the polar angle at random. |
| `0x83B` | 1 | `bytes[1]` | `unused_83B` | Unused | Zero in every file; no reader found. Corpus: zero in all 8244 emitters. |
| `0x83C` | 1 | `uint8` | `sphere_direction_table_index` | Confirmed | `UpdateParams` (0x7100adc1a0): for shape type 5 with 0x874 == 0, `emit_rate = table[D[0x83c]]`. |
| `0x83D` | 1 | `uint8` | `sphere64_division_count` | Confirmed | `UpdateParams` (0x7100adc1c0): for shape type 6 with 0x874 == 0, `emit_rate = (float)D[0x83d]`. |
| `0x83E` | 1 | `uint8` | `shape_rot_variant` | Confirmed | `0x7100add9d8` (sphere, rot mode 1): switch 0..5 selects one of six constant axis vectors that the emitted point and direction are rotated onto. |
| `0x83F` | 1 | `bytes[1]` | `unused_83F` | Unused | Nonzero in the corpus but no reader found. Corpus: nonzero in 6799 of 8244 emitters. |
| `0x840` | 4 | `float` | `shape_angle_b` | Confirmed | `CalculateEmitCircle`: arc spread (`spread * (rand - 0.5)`). |
| `0x844` | 4 | `float` | `shape_angle_c` | Confirmed | `0x7100add9d8` (sphere): polar spread used when 0x83A is 1. |
| `0x848` | 4 | `float` | `shape_angle_d` | Confirmed | `CalculateEmitCircle`: fixed phase when 0x839 is 0. |
| `0x84C` | 4 | `float` | `shape_angle_random` | Confirmed | `CalculateEmitCircleEquallyDivided` (0x7100add4fc): `angle += D[0x84c] * (2 * rand - 1)`. BotW only; TotK 0xDA4 has no reader. |
| `0x850` | 4 | `float` | `shape_fill_ratio` | Confirmed | `CalculateEmitCircleFill` (0x7100add704): radial factor `sqrt(u + (1 - u) * (1 - fill)^2)`; 0 gives the outer shell, 1 a solid disc. |
| `0x854` | 4 | `float` | `line_center_bias` | Confirmed | `0x7100adf2c0` / `0x7100adf338` (line shapes): offset `-(len + len * bias) / 2` shifts the sampled interval. |
| `0x858` | 4 | `float` | `line_length` | Confirmed | `0x7100adf2c0` / `0x7100adf338` (line shapes): segment length times the Z emitter scale. |
| `0x85C`–`0x867` | 12 | `float[3]` | `shape_radius_xyz` | Confirmed | `CalculateEmitCircle`: X radius at 0x85C and Z radius at 0x864; Y read by the sphere/box functions. |
| `0x868`–`0x873` | 12 | `float[3]` | `emitter_volume_scale_xyz` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x650`; `0x7100ad73d0` multiplies it by the ESET animation lanes `+0xe0..0xe8` each frame. |
| `0x874` | 4 | `int32` | `primitive_dist_mode` | Confirmed | `CalculateEmitCircleEquallyDivided`: 0 = divided, 1 = random division index, 2 = sequential index (`Emitter+0x34`); `UpdateParams` forces -1 for shape types outside {2,5,6,13,15}. |
| `0x878` | 8 | `uint64` | `mesh_primitive_idx` | Confirmed | `Resource::InitializeEmitterGraphicsResource` (0x7100ae3e54): `-1` = none, otherwise looked up (0x7100ae495c) into `EmitterResource+0x80`. |
| `0x880` | 4 | `int32` | `shape_divisions` | Confirmed | `CalculateEmitCircleEquallyDivided`: division count. |
| `0x884` | 4 | `uint32` | `circle_division_random_reduction_percent` | Confirmed | `CalculateEmitCircleEquallyDivided`: random reduction of the division count (`count - count * pct * rand * k`). |
| `0x888` | 4 | `uint32` | `line_division_count` | Confirmed | `0x7100adf338` (line equally divided, type 13): number of points on the line; mode 2 steps `Emitter+0x34` through them. |
| `0x88C` | 4 | `uint32` | `line_division_reduction_percent` | Confirmed | `0x7100adf338`: with `primitive_dist_mode == 0` the count is reduced by `count * pct * rand * k`. |
| `0x890`–`0x897` | 8 | `bytes[8]` | `unused_890_897` | Unused | Byte 0x890 is nonzero in the corpus but no reader found; TotK has no counterpart. Corpus: nonzero in 14 of 8244 emitters. |
| `0x898` | 1 | `uint8` | `blend_target_enable` | Confirmed | `0x7100ae2834` (render-state setup): bit 0 of the blend target state. |
| `0x899` | 1 | `uint8` | `depth_test_enable` | Confirmed | `0x7100ae2834`: bit 0 of the depth-stencil flags. BotW only (TotK 0xDE9 unused). |
| `0x89A` | 1 | `uint8` | `depth_compare_func` | Confirmed | `0x7100ae2834`: depth comparison function (accepted when < 8). TotK 0xDEA `depth_stencil_mode_index` occupies the same slot. |
| `0x89B` | 1 | `uint8` | `depth_write_enable` | Confirmed | `0x7100ae2834`: bit 1 of the depth-stencil flags; the draw dispatcher (0x7100acdcfc) also selects between two draw routines by this byte. TotK 0xDEB `depth_sort_ascending` occupies the same slot. |
| `0x89C`–`0x89D` | 2 | `bytes[2]` | `unused_89C_89D` | Unused | Bytes 4 and 5 of the render-state block are never read by `0x7100ae2834`. Corpus: nonzero in 8244 of 8244 emitters. |
| `0x89E` | 1 | `uint8` | `blend_mode_index` | Confirmed | `0x7100ae2834`: values below 5 index a packed table of blend factors/operations. |
| `0x89F` | 1 | `uint8` | `cull_mode_index` | Confirmed | `0x7100ae2834`: values below 3 map through a packed table (0 none, 1/2 front/back). |
| `0x8A0`–`0x8A7` | 8 | `bytes[8]` | `unused_8A0_8A7` | Unused | Not read by `0x7100ae2834`; 0x8A0 is a float in the corpus but has no reader. Corpus: nonzero in 1623 of 8244 emitters. |
| `0x8A8` | 1 | `uint8` | `emit_infinite_flag` | Confirmed | `0x7100ad73d0`: when 1, the emitter is never retired (end test skipped); particle lifetime is also the "infinite" constant in `0x7100ada3cc`; `0x7100ae44ec` sets the ESET `+0x2a` flag. |
| `0x8A9` | 1 | `uint8` | `is_trimming_prim` | Confirmed | `0x7100ae3e54`: trimming primitive is resolved only when set and 0x8D0 != -1. |
| `0x8AA`–`0x8AB` | 2 | `bytes[2]` | `unused_8AA_8AB` | Unused | No reader found. Corpus: nonzero in 8244 of 8244 emitters. |
| `0x8AC` | 1 | `uint8` | `shader_mode_index_DFC` | Confirmed | `0x7100adc8a8`: 0 sets shader flag bit `0x20000000`, 1 sets `0x40000000`. TotK 0xDFC uses the same slot. |
| `0x8AD` | 1 | `uint8` | `rotation_random_sign_x_enable` | Confirmed | `CalculateRotationMatrix` (0x7100ae2264): flips the X rotation sign when the random value is >= 0.5; `0x7100adc8a8` sets feature bit 0x10000. |
| `0x8AE` | 1 | `uint8` | `rotation_random_sign_y_enable` | Confirmed | `CalculateRotationMatrix` (0x7100ae2264): flips the Y rotation sign when the random value is >= 0.5; `0x7100adc8a8` sets feature bit 0x20000. |
| `0x8AF` | 1 | `uint8` | `rotation_random_sign_z_enable` | Confirmed | `CalculateRotationMatrix` (0x7100ae2264): flips the Z rotation sign when the random value is >= 0.5; `0x7100adc8a8` sets feature bit 0x40000. |
| `0x8B0` | 1 | `uint8` | `rotation_param_lane0_enable` | Confirmed | `UpdateParams`: when 0, lane 0 of the four rotation vec4s (0x700, 0x710, 0x720, 0x730) is zeroed. (The previous map called this block "light uniforms".) |
| `0x8B1` | 1 | `uint8` | `rotation_param_lane1_enable` | Confirmed | `UpdateParams`: when 0, lane 1 of the four rotation vec4s (0x700, 0x710, 0x720, 0x730) is zeroed. (The previous map called this block "light uniforms".) |
| `0x8B2` | 1 | `uint8` | `rotation_param_lane2_enable` | Confirmed | `UpdateParams`: when 0, lane 2 of the four rotation vec4s (0x700, 0x710, 0x720, 0x730) is zeroed. (The previous map called this block "light uniforms".) |
| `0x8B3` | 1 | `uint8` | `shader_opt_flag3` | Confirmed | `0x7100adc8a8`: sets shader flag bit `0x10000000`. |
| `0x8B4` | 1 | `uint8` | `shader_opt_flag4` | Confirmed | `0x7100adc8a8`: sets shader flag word 1 bit 1. |
| `0x8B5`–`0x8B7` | 3 | `bytes[3]` | `unused_8B5_8B7` | Unused | Bytes 0x8B5..0x8B6 nonzero in the corpus; no reader found (TotK 0xE05..0xE07 also unused). Corpus: nonzero in 1995 of 8244 emitters. |
| `0x8B8` | 4 | `int32` | `particle_lifespan` | Confirmed | `0x7100ad5a74` copies it as float to `Emitter+0x608`; `0x7100ada3cc` computes lifetime = `D+0x608 * (1 + random pct)`; `0x7100ad73d0` end test uses it. |
| `0x8BC` | 4 | `int32` | `particle_lifespan_random_percent` | Confirmed | `0x7100ada3cc`: `floor(random * D[0x8bc] >> 32)` times a 0.01 constant added to the lifespan factor (i32 here, byte in TotK). |
| `0x8C0` | 4 | `float` | `particle_attribute_w_random_amplitude` | Confirmed | `0x7100ada3cc`: stores `value + 1 + 2 * value * random` into the W lane of the particle scale vector. |
| `0x8C4`–`0x8C7` | 4 | `bytes[4]` | `unused_8C4_8C7` | Unused | No reader found (TotK 0xE14 also unused). Corpus: nonzero in 3211 of 8244 emitters. |
| `0x8C8` | 8 | `uint64` | `g3d_primitive_idx` | Confirmed | `EmitterResource::Setup` (0x7100adb7c0): looked up through `GetG3dPrimitive` when not -1. |
| `0x8D0` | 8 | `uint64` | `trim_primitive_idx` | Confirmed | `0x7100ae3e54`: looked up when `is_trimming_prim` is set and the index is not -1. |
| `0x8D8` | 1 | `uint8` | `loop_color0_enable` | Confirmed | `UpdateParams` writes `runtime_loop_track0_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set. |
| `0x8D9` | 1 | `uint8` | `loop_alpha0_enable` | Confirmed | `UpdateParams` writes `runtime_loop_track1_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set. |
| `0x8DA` | 1 | `uint8` | `loop_color1_enable` | Confirmed | `UpdateParams` writes `runtime_loop_track2_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set. |
| `0x8DB` | 1 | `uint8` | `loop_alpha1_enable` | Confirmed | `UpdateParams` writes `runtime_loop_track3_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set. |
| `0x8DC` | 1 | `uint8` | `loop_scale_enable` | Confirmed | `UpdateParams` writes `runtime_loop_track4_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set. |
| `0x8DD` | 1 | `uint8` | `loop_color0_random_phase` | Confirmed | `UpdateParams` writes 1.0/0.0 to `runtime_loop_track0_random_enable`; the evaluators pass it as the random-phase flag. |
| `0x8DE` | 1 | `uint8` | `loop_alpha0_random_phase` | Confirmed | `UpdateParams` writes 1.0/0.0 to `runtime_loop_track1_random_enable`; the evaluators pass it as the random-phase flag. |
| `0x8DF` | 1 | `uint8` | `loop_color1_random_phase` | Confirmed | `UpdateParams` writes 1.0/0.0 to `runtime_loop_track2_random_enable`; the evaluators pass it as the random-phase flag. |
| `0x8E0` | 1 | `uint8` | `loop_alpha1_random_phase` | Confirmed | `UpdateParams` writes 1.0/0.0 to `runtime_loop_track3_random_enable`; the evaluators pass it as the random-phase flag. |
| `0x8E1` | 1 | `uint8` | `loop_scale_random_phase` | Confirmed | `UpdateParams` writes 1.0/0.0 to `runtime_loop_track4_random_enable`; the evaluators pass it as the random-phase flag. |
| `0x8E2`–`0x8E3` | 2 | `bytes[2]` | `unused_8E2_8E3` | Unused | No reader found. Corpus: nonzero in 2183 of 8244 emitters. |
| `0x8E4` | 4 | `int32` | `loop_color0_period_i32` | Confirmed | Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track0_rate`; read directly by the evaluators. |
| `0x8E8` | 4 | `int32` | `loop_alpha0_period_i32` | Confirmed | Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track1_rate`; read directly by the evaluators. |
| `0x8EC` | 4 | `int32` | `loop_color1_period_i32` | Confirmed | Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track2_rate`; read directly by the evaluators. |
| `0x8F0` | 4 | `int32` | `loop_alpha1_period_i32` | Confirmed | Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track3_rate`; read directly by the evaluators. |
| `0x8F4` | 4 | `int32` | `loop_scale_period_i32` | Confirmed | Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track4_rate`; read directly by the evaluators. |
| `0x8F8`–`0x8FF` | 8 | `bytes[8]` | `unused_8F8_8FF` | Unused | Bytes nonzero in the corpus but no reader found; TotK uses 0xE40..0xE44 for key interpolation modes, which BotW does not read. Corpus: nonzero in 7953 of 8244 emitters. |
| `0x900`–`0x913` | 20 | `bytes[20]` | `unused_900_913` | Unused | No reader found (0x900 appears only as the low bits of a constant-pool address). Corpus: nonzero in 8130 of 8244 emitters. |
| `0x914` | 4 | `int32` | `shader_idx_normal` | Confirmed | `EmitterResource::Setup` (0x7100adb7c0): `ShaderManager::GetShader(D[0x914])` into `EmitterResource+0x340`. |
| `0x918` | 4 | `int32` | `compute_shader0` | Confirmed | `Setup`: `shaderTable + D[0x918] * 0x40` into `EmitterResource+0x358` when not -1. |
| `0x91C` | 4 | `int32` | `shader_idx_pass1` | Confirmed | `Setup`: second graphics shader, skipped when -1 (`EmitterResource+0x348`). |
| `0x920`–`0x923` | 4 | `bytes[4]` | `unused_920_923` | Unused | Zero in every file; no reader found. Corpus: zero in all 8244 emitters. |
| `0x924` | 4 | `int32` | `shader_idx_pass2` | Confirmed | `Setup`: third graphics shader, skipped when -1 (`EmitterResource+0x350`). |
| `0x928`–`0x92B` | 4 | `bytes[4]` | `unused_928_92B` | Unused | No reader found. Corpus: zero in all 8244 emitters. |
| `0x92C` | 4 | `int32` | `custom_shader_index` | Confirmed | `FUN_7100ac99d0` (CreateEmitter): 0 selects the default callback slot, otherwise callback `(id + 8) * 0x58 + 0x8d8` of the system; a missing callback logs "CustomShader Callback not Set" and is skipped. |
| `0x930`–`0x967` | 56 | `bytes[56]` | `unused_930_967` | Unused | No reader found (TotK 0xE78..0xEF7 is the same unused block). Corpus: nonzero in 8035 of 8244 emitters. |
| `0x968` | 4 | `int32` | `custom_action_index` | Confirmed | `FUN_7100ac99d0`: values > 0 select action callback `(id - 1) * 0x58 + 0x8d8` (requires chunk slot `+0x310`). |
| `0x96C` | 4 | `float` | `all_directional_speed` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x62c`; `0x7100ad73d0` scales it by the ESET lane `+0x1d8`; every shape emit function multiplies the emitted direction by it (`param_7+0x6c` = `Emitter+0x62c`). |
| `0x970` | 4 | `float` | `designated_direction_speed` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x638`; `0x7100ada3cc` scales the designated direction by it (times the ESET lane `+0x200`). |
| `0x974`–`0x97F` | 12 | `float[3]` | `designated_direction_xyz` | Confirmed | `0x7100ada3cc`: base direction vector added to the emitted direction (`dir + vec * speed`), optionally transformed when 0x7F3 is set. |
| `0x980` | 4 | `float` | `emission_direction_spread_degrees` | Confirmed | `0x7100ada3cc`: `value / 90 + 1` is the lower bound of the cone sample; non-zero rotates the emitted direction into a random cone around it. |
| `0x984` | 4 | `float` | `emission_tangent_amount` | Confirmed | `0x7100ada3cc`: adds `normalize(XZ tangent of the emitted position) * value` to the direction (random XZ direction at the origin). |
| `0x988` | 4 | `float` | `emission_direction_random_x` | Confirmed | `0x7100ada3cc`: `direction.x += randomTable.x * value`. |
| `0x98C` | 4 | `float` | `emission_direction_random_y` | Confirmed | `0x7100ada3cc`: `direction.y += randomTable.y * value`. |
| `0x990` | 4 | `float` | `emission_direction_random_z` | Confirmed | `0x7100ada3cc`: `direction.z += randomTable.z * value`. |
| `0x994` | 4 | `float` | `initial_speed_random_percent` | Confirmed | `0x7100ada3cc`: speed factor `1 + eset(+0x1dc) * random * k * (value / 100)`. |
| `0x998` | 4 | `float` | `emitter_motion_inherit_scale` | Confirmed | `0x7100ada3cc`: adds `emitterVelocity(Emitter+0x380) * value` to the direction. |
| `0x99C`–`0x9A3` | 8 | `bytes[8]` | `unused_99C_9A3` | Unused | Nonzero in the corpus but no reader found; TotK 0xF2C `emitter_motion_inherit_max` and its neighbours have no BotW reader. Corpus: nonzero in 4074 of 8244 emitters. |
| `0x9A4` | 1 | `uint8` | `color0_mode` | Confirmed | `UpdateParams`: 0 copies the constant (0x9A8) into key 0 of the table at 0x3C0; `0x7100ae19ac`/`0x7100ae1e08`: 2 evaluates the keys, 3 (color only) selects a discrete key. |
| `0x9A5` | 1 | `uint8` | `color1_mode` | Confirmed | `UpdateParams`: 0 copies the constant (0x9B8) into key 0 of the table at 0x4C0; `0x7100ae19ac`/`0x7100ae1e08`: 2 evaluates the keys, 3 (color only) selects a discrete key. |
| `0x9A6` | 1 | `uint8` | `alpha0_mode` | Confirmed | `UpdateParams`: 0 copies the constant (0x9B4) into key 0 of the table at 0x440; `0x7100ae19ac`/`0x7100ae1e08`: 2 evaluates the keys, 3 (color only) selects a discrete key. |
| `0x9A7` | 1 | `uint8` | `alpha1_mode` | Confirmed | `UpdateParams`: 0 copies the constant (0x9C4) into key 0 of the table at 0x540; `0x7100ae19ac`/`0x7100ae1e08`: 2 evaluates the keys, 3 (color only) selects a discrete key. |
| `0x9A8`–`0x9B3` | 12 | `float[3]` | `color0_const_rgb` | Confirmed | `UpdateParams` and `0x7100ae19ac`: constant color0. |
| `0x9B4` | 4 | `float` | `alpha0_const` | Confirmed | Constant alpha0 (`UpdateParams` writes it to 0x440). |
| `0x9B8`–`0x9C3` | 12 | `float[3]` | `color1_const_rgb` | Confirmed | Constant color1 (`UpdateParams` writes it to 0x4C0). |
| `0x9C4` | 4 | `float` | `alpha1_const` | Confirmed | Constant alpha1 (`UpdateParams` writes it to 0x540). |
| `0x9C8`–`0x9D3` | 12 | `float[3]` | `particle_scale_xyz` | Confirmed | `0x7100ad5a74` copies it to `Emitter+0x644`; `0x7100ada3cc` multiplies it by the ESET scale lanes into the particle scale. |
| `0x9D4`–`0x9DF` | 12 | `float[3]` | `particle_scale_rnd` | Confirmed | `0x7100ada3cc`: uniform random factor `1 + (v / 100) * random` (x), or per-axis when 0x9D4 != 0x9D8. |
| `0x9E0`–`0x9EB` | 12 | `bytes[12]` | `unused_9E0_9EB` | Unused | No reader found (TotK 0xF80..0xF8B also unused). Corpus: nonzero in 6684 of 8244 emitters. |
| `0x9EC` | 1 | `uint8` | `waveform_alpha_enable` | Confirmed | `0x7100ae19ac`: multiplies alpha by waveform 0 when set. |
| `0x9ED` | 1 | `uint8` | `waveform_scale_x_enable` | Confirmed | `0x7100ae1514`: applies waveform 0 to scale X (and Z) when set. |
| `0x9EE` | 1 | `uint8` | `waveform_scale_y_enable` | Confirmed | `0x7100ae1514`: additionally applies waveform 1 to scale Y when set. |
| `0x9EF` | 1 | `uint8` | `waveform_mode_packed` | Confirmed | High nibble: 0 cosine, 1 sawtooth, 2 square (`0x7100ae19ac`, `0x7100ae1514`); `0x7100adc8a8` maps values below 0x30 through a table into shader flag word 0. |
| `0x9F0`–`0x9F7` | 8 | `bytes[8]` | `unused_9F0_9F7` | Unused | No reader found. Corpus: zero in all 8244 emitters. |
| `0x9F8` | 8 | `uint64` | `tex_slot0_guid` | Confirmed | `InitializeEmitterGraphicsResource` (0x7100ae3e54): passed to the texture lookup `0x7100ae42a8` (-1 = none) into `EmitterResource+0x48`. |
| `0xA00`–`0xA02` | 3 | `bytes[3]` | `tex_slot0_sampler_select` | Confirmed | `0x7100ad3578` (called from `UpdateParams` with `D+0x9F8`): sampler index = `b[1] + b[0] * 4 + b[2] * 16` into the sampler table (stride 0xA8). Which byte is filter vs wrap is not established. |
| `0xA03`–`0xA17` | 21 | `bytes[21]` | `unverified_A03_A17` | Unverified | No reader found; part of the 0x20-byte slot record. Bytes 0xA0C..0xA10 are nonzero in the corpus. |
| `0xA18` | 8 | `uint64` | `tex_slot1_guid` | Confirmed | `InitializeEmitterGraphicsResource` (0x7100ae3e54): passed to the texture lookup `0x7100ae42a8` (-1 = none) into `EmitterResource+0x50`. |
| `0xA20`–`0xA22` | 3 | `bytes[3]` | `tex_slot1_sampler_select` | Confirmed | `0x7100ad3578` (called from `UpdateParams` with `D+0xA18`): sampler index = `b[1] + b[0] * 4 + b[2] * 16` into the sampler table (stride 0xA8). Which byte is filter vs wrap is not established. |
| `0xA23`–`0xA37` | 21 | `bytes[21]` | `unverified_A23_A37` | Unverified | No reader found; part of the 0x20-byte slot record. Bytes 0xA2C..0xA30 are nonzero in the corpus. |
| `0xA38` | 8 | `uint64` | `tex_slot2_guid` | Confirmed | `InitializeEmitterGraphicsResource` (0x7100ae3e54): passed to the texture lookup `0x7100ae42a8` (-1 = none) into `EmitterResource+0x58`. |
| `0xA40`–`0xA42` | 3 | `bytes[3]` | `tex_slot2_sampler_select` | Confirmed | `0x7100ad3578` (called from `UpdateParams` with `D+0xA38`): sampler index = `b[1] + b[0] * 4 + b[2] * 16` into the sampler table (stride 0xA8). Which byte is filter vs wrap is not established. |
| `0xA43`–`0xA57` | 21 | `bytes[21]` | `unverified_A43_A57` | Unverified | No reader found; part of the 0x20-byte slot record. Bytes 0xA4C..0xA50 are nonzero in the corpus. |
| `0xA58` | 1 | `uint8` | `tex0_mode_index` | Confirmed | `0x7100adc8a8`: 1..3 set one shader feature bit each; 4 builds the frame table (see `tex0_flipbook_runtime_block`). |
| `0xA59` | 1 | `uint8` | `tex0_uv_scroll` | Confirmed | `UpdateParams`: zeroes the scroll fields of `tex0_uniform_block` when 0. |
| `0xA5A` | 1 | `uint8` | `tex0_uv_rotate` | Confirmed | `UpdateParams`: zeroes the rotate fields when 0. |
| `0xA5B` | 1 | `uint8` | `tex0_uv_scale` | Confirmed | `UpdateParams`: writes the default scale (1.0) when 0. |
| `0xA5C` | 1 | `uint8` | `tex0_uv_domain_scale_mode` | Confirmed | `UpdateParams`: values below 4 select two floats from a table into the uniform block. |
| `0xA5D` | 1 | `uint8` | `tex0_shader_flag0` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x80000 when non-zero. |
| `0xA5E` | 1 | `uint8` | `tex0_shader_flag1` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x100000 when non-zero. |
| `0xA5F` | 1 | `uint8` | `tex0_shader_flag2` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x2000000 when non-zero. |
| `0xA60`–`0xA67` | 8 | `bytes[8]` | `unused_A60_A67` | Unused | No reader found; byte 0xA60 nonzero in the corpus. Corpus: nonzero in 152 of 8244 emitters. |
| `0xA68` | 1 | `uint8` | `tex1_mode_index` | Confirmed | `0x7100adc8a8`: 1..3 set one shader feature bit each; 4 builds the frame table (see `tex1_flipbook_runtime_block`). |
| `0xA69` | 1 | `uint8` | `tex1_uv_scroll` | Confirmed | `UpdateParams`: zeroes the scroll fields of `tex1_uniform_block` when 0. |
| `0xA6A` | 1 | `uint8` | `tex1_uv_rotate` | Confirmed | `UpdateParams`: zeroes the rotate fields when 0. |
| `0xA6B` | 1 | `uint8` | `tex1_uv_scale` | Confirmed | `UpdateParams`: writes the default scale (1.0) when 0. |
| `0xA6C` | 1 | `uint8` | `tex1_uv_domain_scale_mode` | Confirmed | `UpdateParams`: values below 4 select two floats from a table into the uniform block. |
| `0xA6D` | 1 | `uint8` | `tex1_shader_flag0` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x200000 when non-zero. |
| `0xA6E` | 1 | `uint8` | `tex1_shader_flag1` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x400000 when non-zero. |
| `0xA6F` | 1 | `uint8` | `tex1_shader_flag2` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x4000000 when non-zero. |
| `0xA70`–`0xA77` | 8 | `bytes[8]` | `unused_A70_A77` | Unused | No reader found. Corpus: nonzero in 119 of 8244 emitters. |
| `0xA78` | 1 | `uint8` | `tex2_mode_index` | Confirmed | `0x7100adc8a8`: 1..3 set one shader feature bit each; 4 builds the frame table (see `tex2_flipbook_runtime_block`). |
| `0xA79` | 1 | `uint8` | `tex2_uv_scroll` | Confirmed | `UpdateParams`: zeroes the scroll fields of `tex2_uniform_block` when 0. |
| `0xA7A` | 1 | `uint8` | `tex2_uv_rotate` | Confirmed | `UpdateParams`: zeroes the rotate fields when 0. |
| `0xA7B` | 1 | `uint8` | `tex2_uv_scale` | Confirmed | `UpdateParams`: writes the default scale (1.0) when 0. |
| `0xA7C` | 1 | `uint8` | `tex2_uv_domain_scale_mode` | Confirmed | `UpdateParams`: values below 4 select two floats from a table into the uniform block. |
| `0xA7D` | 1 | `uint8` | `tex2_shader_flag0` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x800000 when non-zero. |
| `0xA7E` | 1 | `uint8` | `tex2_shader_flag1` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x1000000 when non-zero. |
| `0xA7F` | 1 | `uint8` | `tex2_shader_flag2` | Confirmed | `0x7100adc8a8`: sets shader feature bit 0x8000000 when non-zero. |
| `0xA80`–`0xA87` | 8 | `bytes[8]` | `unused_A80_A87` | Unused | No reader found. Corpus: nonzero in 49 of 8244 emitters. |

## 4. Chunk payload layouts

The tables are the C# definitions in `src/PtclSharp/Layout/BotwLayouts.cs` (the single source of truth, checked against all 8,244 shipped emitters by `tests/PtclSharp.Tests/BotwCorpusTests.cs`). Sizes are payload sizes (node size minus the 0x20-byte header).

| Chunk | Payload | Corpus | Notes |
|:---|:---:|:---:|:---|
| `EAES EAER EAET EAC0 EAC1 EATR EAPL EAA0 EAA1 EAOV EADV EASL EASS EAGV` | `0x0C + 0x10 * key_count` | 2,416 (EAGV: none) | Emitter lane: `enabled` byte `+0`, `loop` byte `+1`, `key_count` `+4`, keys `(x, y, z, time)` from `+0x0C`. BotW has no interpolation byte (always linear); `+2`, `+3`, `+8` unused. Evaluator `CalculateEmitterKeyFrameAnimation` (0x7100adf920). |
| `FRND` | `0xD0` | 105 | Field random; Confirmed in `0x7100aece80` / `RandFunc` (0x7100aed4ac). Members `+0..+0x3B` as TotK; 8-key animation (`+0x3C`, 0x94 bytes). |
| `FRN1` | `0xA4` | 3 | Constant `+0..+0xB`, `blank` `+0xC`, animation `+0x10`. Confirmed in 0x7100ae0e34. |
| `FMAG` | `0xA8` | 18 | Magnet. Follow byte `+0`, axis bytes `+1..+3`, power `+4`, position `+8`, animation `+0x14`. Confirmed in 0x7100adfd38. |
| `FSPN` | `0x134` | 117 | Spin (Confirmed, 0x7100ae0244): rotate `+0`, axis `+4`, outer `+8`, animations at `+0x0C` and `+0xA0`. |
| `FCOL` | `0x14` | 0 | Collision (Confirmed in 0x7100ae065c); no shipped chunk. |
| `FCOV` | `0xA8` | 196 | Convergence (Confirmed, 0x7100ae0994): type `+0`, position `+4`, ratio `+0x10`, animation `+0x14`. |
| `FPAD` | `0xA4` | 10 | Position add: global byte `+0`, vector `+4`, animation `+0x10`. |
| `FCLN` | `0x24` | 666 | Curl noise (Confirmed, 0x7100ad4c70); same members as TotK. |
| `FCSF` | `0x44` | 3,159 | Custom field: type `+0`, 16 floats (the GPU buffer receives the first 8). |
| `EP01` | `0x28` | 31 | Connection stripe: calc type `+0`, option `+8` (cross mesh), num divide `+0x10`, connection type `+0x14`, head/tail alpha `+0x18/+0x1C`; `+4`, `+0xC`, `+0x20`, `+0x24` unused. |
| `EP02` | `0x2C` | 106 | Stripe: calc type, emitter follow, option `+8`, texturing `+0x0C`, num divide `+0x10`, num history `+0x14`, head/tail alpha `+0x1C/+0x20`, dir interpolate `+0x28`; `+0x18`, `+0x24` unused. |
| `EP03` | `0x84` | 102 | Super stripe: option `+8`, texturing0..2 `+0x0C..+0x14`, num history `+0x18`, alphas `+0x20/+0x24`, num divide `+0x28`, history parameters TotK's shifted by `+0xC`, UV map type `+0x58`, scales `+0x5C/+0x60`; `+0x64..+0x83` unused. |
| `EP04` | `0x50` | 13 | Area loop: same members as TotK; `+0x1C` is passed to the draw constants (always 0), `+0x4C` unused. |
| `CSDP` | node size - 0x20 | 3,974 | Raw custom-shader uniform block (52 or 100 bytes in the corpus); copied verbatim to the GPU. |
| `CADP` | varies | 1,063 | Custom-action data, opaque to the effect library. |
| `CUDP` | varies | 0 | Custom user data; no shipped chunk. |
| `PRIM` | `0x54` header + arrays | 14 | Same header as TotK. |
| `G3NT` entry | `0x18` | 369 | Same chain word and attribute indices as TotK. |

Container facts (BotW): top-level node order `ESTA, GRTF, PRMA, G3PR, GRSN`; `ESET` is `0x60` bytes (name `+0x10`, `emitter_count` `+0x50`; `+0x58` and `+0x5C` unverified); `EMTR` data is aligned to `0x100`, with nested child EMTRs counted by the ESET.

