"""Builds the BotW EMTR byte map (docs/research/botw-emtr-offsets-ghidra.md section 3) from per-region definitions.

Evidence rules (same vocabulary as the TotK map):
  Confirmed  the BotW executable's own code proves the role (function addresses are 0x71 image addresses).
  Paired     same role as a proven TotK field at an aligned position; BotW reader/GPU read-set agrees but the BotW code was not read for this exact field.
  Unverified consumed (GPU or CPU) but purpose unproven. May carry a Guess.
  Unused     zero/never consumed: no GPU read in the 0x750-byte uniform copy and no BotW reader found.
usage: python botw_rows.py <docs/research dir>   (rewrites the section 3 table of the BotW doc)
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
from parse_maps import parse

C, P, U, X = 'Confirmed', 'Paired', 'Unverified', 'Unused'
rows = []


def add(off, size, typ, name, status, ev, tk=None):
    rows.append(dict(offset=off, size=size, type=typ, name=name, status=status, evidence=ev, tk=tk))


def unused(off, size, why='No GPU read (outside the shader read set) and no BotW reader found; bytes preserved as authored.'):
    add(off, size, 'bytes[%d]' % size, 'unused_%03X_%03X' % (off, off + size - 1) if size > 1 else 'unused_%03X' % off, X, why)


# ------------------------------------------------------------------ 0x000-0x07F prefix, flags, key counts
unused(0x000, 0x10, 'Zero in all 8,244 corpus emitters; this is the runtime prefix of the data block (TotK has the same unused prefix). The fourcc/size/version words the previous map placed here are the node header, which sits before the data block.')
add(0x010, 0x40, 'char[64]', 'emitter_name', P, 'NUL-terminated ASCII name (corpus: 586 distinct names in 8,244 emitters). The name is matched by the emitter search; not re-read in BotW code for this pass.', 'emitter_name')
add(0x050, 4, 'uint32', 'runtime_shader_flags_word0', C, '`EmitterResource::UpdateParams` (0x7100adb8f4) overwrites it with the first word built by 0x7100adc8a8 (shader feature bits); the GPU reads it only through bit tests (`& 0x80000`, `& 0x10000` ...). Zero in every file.', 'runtime_shader_flags_word0')
add(0x054, 4, 'uint32', 'runtime_shader_flags_word1', C, 'Second word built by 0x7100adc8a8 and stored by `UpdateParams`; GPU tests bit `& 1`. Zero in every file.', 'runtime_shader_flags_word1')
unused(0x058, 4, 'Zero in every file; no GPU read and no BotW writer or reader found (TotK word2 has no BotW counterpart).')
add(0x05C, 4, 'uint32', 'runtime_attribute_word', C, '`UpdateParams` stores the first word of attribute chunk slot `+0x288` here (`D[0x5c] = chunk[0]`). Zero in every file.', 'runtime_attribute_word')
for i, (nm, label) in enumerate([('color0', 'Color0'), ('alpha0', 'Alpha0'), ('color1', 'Color1'), ('alpha1', 'Alpha1'), ('scale', 'Scale'), ('track5', 'track 5')]):
    tbl = [0x3C0, 0x440, 0x4C0, 0x540, 0x600, 0x680][i]
    add(0x060 + 4 * i, 4, 'uint32', nm + '_key_count', C, 'Key count (0..8) of the table at 0x%X. `UpdateParams` (0x7100adb8f4) pads slots `count..7` by repeating the last key (keys 1..7 only) and shifts their time lane; the animation evaluators (0x7100ae19ac, 0x7100ae1514) skip the key path when the count is below 1/2.' % tbl, nm + '_key_count')
unused(0x078, 8, 'Zero in every file; no GPU read and no BotW reader found (TotK 0x0A8..0x0AF is the same hole after its larger key-count table).')
# ------------------------------------------------------------------ 0x080-0x0AF runtime loop overlays
for i, nm in enumerate(['color0', 'alpha0', 'color1', 'alpha1', 'scale']):
    en = [0x8D8, 0x8D9, 0x8DA, 0x8DB, 0x8DC][i]
    rate = [0x8E4, 0x8E8, 0x8EC, 0x8F0, 0x8F4][i]
    add(0x080 + 4 * i, 4, 'float', 'runtime_loop_track%d_rate' % i, C, '`UpdateParams` writes `float(D[0x%X])` when `D[0x%X] != 0`, otherwise 0.0 (%s loop period). The GPU reads the reciprocal pattern `0.0 < F` / `1.0 / F`. Zero in every file.' % (rate, en, nm), 'runtime_loop_track%d_rate' % i)
for i, nm in enumerate(['color0', 'alpha0', 'color1', 'alpha1', 'scale']):
    rnd = [0x8DD, 0x8DE, 0x8DF, 0x8E0, 0x8E1][i]
    add(0x094 + 4 * i, 4, 'float', 'runtime_loop_track%d_random_enable' % i, C, '`UpdateParams` writes 1.0 when `D[0x%X] != 0` (%s random start phase), otherwise 0.0; the GPU multiplies it (`t = t * F`). Zero in every file.' % (rnd, nm), 'runtime_loop_track%d_random_enable' % i)
unused(0x0A8, 8, 'Zero in every file; no GPU read and no BotW reader found.')
# ------------------------------------------------------------------ 0x0B0-0x10F  (TotK 0x0E0-0x13F, +0x30)
add(0x0B0, 12, 'float[3]', 'gpu_accel_dir_xyz', C, 'Vertex shaders (for example 0118E544_v1.V line 1803): `position += 0.5 * (t * t * data[11].w) * data[11].xyz` with `t = age + offset`, i.e. a constant-acceleration displacement along this vector. Default (0, -1, 0) in 97% of emitters. TotK holds the same bytes at 0xE0 and reads them on the CPU as the stationary-delta fallback; the BotW CPU code does not read them.')
add(0x0BC, 4, 'float', 'gpu_accel_scale', C, 'Vertex shaders: `t * t * data[11].w` multiplies the acceleration vector (see `gpu_accel_dir_xyz`). 0.0109 is the most common value (about 9.8 / 30^2, gravity per frame squared).')
add(0x0C0, 4, 'float', 'velocity_attenuation_per_frame', C, '`CalculateParticleBehavior` (0x7100ae0e34): `if (D[0xc0] >= 1.0) velocity unchanged, else velocity *= powf(D[0xc0], frame_time)` (read as `body+0x70`). GPU reads `log2(F)` and `F == 1.0` for the compute path.', 'velocity_attenuation_per_frame')
unused(0x0C4, 12, 'Zero in every file; no GPU read and no BotW reader found.')
for i, ax in enumerate('xyz'):
    add(0x0D0 + 4 * i, 4, 'float', 'template_vertex_bias_' + ax, P, 'GPU vertex stage `fma(0.5, F, t)` on the template vertex position (0xD0/0xD4 in 6,852 programs, 0xD8 through varied arithmetic); same position, read pattern and distribution as TotK 0x%X.' % (0x100 + 4 * i), 'template_vertex_bias_' + ax)
unused(0x0DC, 4, 'Zero in every file; no GPU read (TotK 0x10C, the analogous float, is nonzero there).')
wf = [('waveform0_amplitude', 0xE0, 'amplitude of waveform 0 (multiplies the alpha/scale-X waveform)'), ('waveform1_amplitude', 0xE4, 'amplitude of waveform 1 (scale Y)'),
      ('waveform0_period', 0xE8, 'period divisor of waveform 0'), ('waveform1_period', 0xEC, 'period divisor of waveform 1'),
      ('waveform0_random_phase_scale', 0xF0, 'multiplier of the per-particle random phase of waveform 0'), ('waveform1_random_phase_scale', 0xF4, 'multiplier of the per-particle random phase of waveform 1'),
      ('waveform0_time_offset', 0xF8, 'time offset of waveform 0'), ('waveform1_time_offset', 0xFC, 'time offset of waveform 1')]
for nm, off, what in wf:
    add(off, 4, 'float', nm, C, '%s. Read as `body+0x%X` by the pulse evaluators (0x7100ae19ac for alpha, 0x7100ae1514 for scale): phase = `(time_offset + t) / period + random_phase_scale * particleRandom`, shape from the high nibble of 0x9EF, amplitude `1 - amp * wave`. GPU reads the same dwords (`0.0 - F`, `1.0 / F`, `t * F`, `t + F`).' % (what, off - 0x50), nm)
add(0x100, 4, 'float', 'unverified_100', U, 'Fragment stage only (535 programs): `t = t * F`. Role not established. **Guess:** Likely an intensity/brightness-style multiplier on a fragment quantity (TotK analogue 0x130); low confidence.', 'unverified_130')
add(0x104, 4, 'float', 'unverified_104', U, 'Fragment stage only (534 programs): `t = t * F`. Role not established. **Guess:** Likely an intensity/brightness-style multiplier on a fragment quantity (TotK analogue 0x134); low confidence.', 'unverified_134')
unused(0x108, 8, 'Zero in every file; no GPU read and no BotW reader found.')
# ------------------------------------------------------------------ 0x110-0x2BF texture flipbook runtime blocks
for i in range(3):
    base = 0x110 + 0x90 * i
    add(base, 0x90, 'bytes[0x90]', 'tex%d_flipbook_runtime_block' % i, C, 'Runtime pattern table of texture slot %d. `0x7100adc8a8` (mode 4 only, `D[0x%X] == 4`) writes `D[0x%X] = D[0x%X]` (frame count float) and fills the int table at `D[0x%X + 4*i] = i` for `i < count`; the GPU truncates the first two dwords (`trunc(F)`). Mostly zero in files; slot 2 is almost always empty.' % (i, 0xA58 + 0x10 * i, base, base + 8, base + 0x10), 'tex%d_flipbook_runtime_block' % i)
# ------------------------------------------------------------------ 0x2C0-0x3AF texture uniform blocks
for i in range(3):
    base = 0x2C0 + 0x50 * i
    cfg = 0xA58 + 0x10 * i
    add(base, 0x50, 'bytes[0x50]', 'tex%d_uniform_block' % i, C, 'UV transform block of texture slot %d, copied verbatim to the GPU. `UpdateParams` proves its structure: when `D[0x%X]` (scroll) is 0 it zeroes +0x00..+0x14; when `D[0x%X]` (rotate) is 0 it zeroes +0x30..+0x38; when `D[0x%X]` (scale) is 0 it writes 1.0 to +0x20/+0x24 and 0 to +0x18/+0x1C/+0x28/+0x2C; it writes the two floats selected by `D[0x%X]` (domain mode, value < 4, table at 0x7101e787a0/b0) to +0x40/+0x44.' % (i, cfg + 1, cfg + 2, cfg + 3, cfg + 4), 'tex%d_uniform_block' % i)
# ------------------------------------------------------------------ 0x3B0-0x5FF color/alpha key tables (+0x2D0 from TotK)
add(0x3B0, 4, 'float', 'particle_color_rgb_scale', C, 'Read as `body+0x360` by the color evaluator (0x7100ae19ac): `rgb = key_rgb * this * emitterColor * ...`; applies to the three color channels only (alpha uses a separate factor).', 'particle_color_rgb_scale')
unused(0x3B4, 12, 'Zero in every file; no GPU read and no BotW reader found.')
for nm, off in [('kf_color0', 0x3C0), ('kf_alpha0', 0x440), ('kf_color1', 0x4C0), ('kf_alpha1', 0x540)]:
    cnt = {'kf_color0': 'color0', 'kf_alpha0': 'alpha0', 'kf_color1': 'color1', 'kf_alpha1': 'alpha1'}[nm]
    mode = {'kf_color0': 0x9A4, 'kf_alpha0': 0x9A6, 'kf_color1': 0x9A5, 'kf_alpha1': 0x9A7}[nm]
    add(off, 128, 'float[8][4]', nm, C, 'Eight keys (value xyz or x, time w). `UpdateParams` copies the constant (0x%X) into key 0 when mode `D[0x%X] == 0` and pads unused keys after `%s_key_count`; `Calculate8KeyAnim` evaluates them (0x7100ae19ac) when the mode is 2; mode 3 of color picks a discrete key by `floor(life * count)` from the same table (0x%X + 0x10*idx).' % ({0x3C0: 0x9A8, 0x440: 0x9B4, 0x4C0: 0x9B8, 0x540: 0x9C4}[off], mode, cnt, off), nm)
add(0x5C0, 4, 'float', 'unverified_5C0', U, 'Not read by any shader (GPU read set starts at 0x5C4) and no BotW reader found; nonzero (10.0) in 8 files. TotK analogue 0x890 is read there.', 'unverified_890')
tk_un = ['unverified_894', 'unverified_898', 'unverified_89C', 'unverified_8A0', 'unverified_8A4', 'unverified_8A8', 'unverified_8AC', 'unverified_8B0']
for i, t in enumerate(tk_un):
    off = 0x5C4 + 4 * i
    add(off, 4, 'float', 'unverified_%03X' % off, U, 'GPU-read dword; same position in the read set, shader pattern and value distribution as TotK 0x%03X. Role not established (see the TotK row for the shader patterns).' % (0x894 + 4 * i), t)
unused(0x5E4, 4, 'Zero in every file; no GPU read.')
add(0x5E8, 4, 'float', 'fragment_discard_threshold', P, 'GPU fragment-stage read (`t <= F` guarding a discard in TotK); same position in the BotW read set as TotK 0x8B8. Not decompiled in BotW fragment code for this pass.', 'fragment_discard_threshold')
unused(0x5EC, 4, 'Zero in every file; no GPU read.')
add(0x5F0, 4, 'float', 'unverified_5F0', U, 'GPU-read dword (vertex stage in TotK); same position as TotK 0x8C0. Role not established.', 'unverified_8C0')
add(0x5F4, 4, 'float', 'unverified_5F4', U, 'GPU-read dword (fragment stage in TotK); same position as TotK 0x8C4. Role not established.', 'unverified_8C4')
unused(0x5F8, 8, 'Zero in every file; no GPU read.')
add(0x600, 128, 'float[8][4]', 'kf_scale', C, 'Scale keys xyz + time. `FUN_7100ae1514` evaluates them as `body+0x5b0` (`Calculate8KeyAnim`) when `scale_key_count >= 2`; `UpdateParams` pads unused keys.', 'kf_scale')
add(0x680, 128, 'float[8][4]', 'kf_track5', C, 'Sixth key table (xyz + time), padded by `UpdateParams` with `track5_key_count` (0x74); GPU-read at `0x680..0x6FC`. No BotW CPU reader was found for it.', 'kf_track5')
# ------------------------------------------------------------------ 0x700-0x74F rotation block
add(0x700, 12, 'float[3]', 'rotation_initial_xyz', C, '`UpdateParams` ends by copying D[0x700..0x70B] to `EmitterResource+0x320`; `0x7100ada3cc` stores that triple into the per-particle rotation array (`param_6[5]`) at emission, so it is the initial rotation of each particle. Only lanes enabled by 0x8B0..0x8B2 are kept (`UpdateParams` zeroes lane i of the four rotation vec4s when the lane flag is 0). Not read by any shader.', 'rotation_initial_xyz')
unused(0x70C, 4, 'Zero in every file; padding of the vec4.')
add(0x710, 12, 'float[3]', 'rotation_initial_random_xyz', C, '`CalculateRotationMatrix` (0x7100ae2264): `initial + body[0x6c0..0x6c8] * random` per axis.', 'rotation_initial_random_xyz')
unused(0x71C, 4, 'Zero in every file; padding of the vec4.')
add(0x720, 12, 'float[3]', 'rotation_add_xyz', C, '`CalculateRotationMatrix`: per-frame rotation added, `body[0x6d0..0x6d8]` plus the random term, scaled by time (with attenuation).', 'rotation_add_xyz')
add(0x72C, 4, 'float', 'rotation_add_attenuation', C, '`CalculateRotationMatrix`: `powf(body[0x6dc], t)` and `(1 - a) / (1 - base)` give the attenuated accumulated rotation.', 'rotation_add_attenuation')
add(0x730, 12, 'float[3]', 'rotation_add_random_xyz', C, '`CalculateRotationMatrix`: `body[0x6e0..0x6e8] * (r1 + r2) * 0.5` added to the per-frame rotation.', 'rotation_add_random_xyz')
unused(0x73C, 4, 'Zero in every file; padding of the vec4.')
add(0x740, 4, 'float', 'unverified_740', U, 'GPU vertex read (`740`). Same position relative to the rotation block as TotK 0xC50. Role not established. **Guess:** Likely the maximum length used to clamp and normalize a velocity/ribbon stretch in the vertex stage (TotK analogue).', 'unverified_C50')
add(0x744, 4, 'float', 'unverified_744', U, 'GPU vertex read (`744`). Same position relative to the rotation block as TotK 0xC54. Role not established. **Guess:** Likely the minimum length (lower clamp) used when normalizing a velocity/ribbon stretch in the vertex stage (TotK analogue).', 'unverified_C54')
unused(0x748, 8, 'Zero in every file; beyond the GPU copy (0x750 bytes) and no BotW reader found. The previous map placed `sim_flags` and sort/velocity bytes here; the BotW code reads them at 0x750..0x753.')
# ------------------------------------------------------------------ 0x750-0x76F behavior flags
add(0x750, 1, 'uint8', 'sim_flags', P, 'First byte beyond the GPU copy. Read by the emitter update gate at 0x7100aca538 (together with emitter flag `+2` and the fade value `+0x64 > 0`); TotK copies the same byte into a runtime flag bit. Exact behavior not decoded.', 'sim_flags')
add(0x751, 1, 'uint8', 'particle_sort_mode_index', P, 'Non-zero selects the sorted-particle path: 0x7100ad9900 branches to 0x7100ad99dc; 0 uses the plain list. Per-mode semantics follow TotK (0 none, 1/3 scalar sort, 2 camera depth, 4 callback) but were not decoded in BotW.', 'particle_sort_mode_index')
add(0x752, 1, 'uint8', 'emitter_calc_type', C, '`0x7100ad73d0`: `0` runs the CPU particle calculation (`0x7100ad7ea0`), non-zero copies the particle count (GPU path), `2` with the caller flag also runs the CPU path. `0x7100ad5a74` allocates CPU/GPU particle buffers by this value (2 aligns to 32 and drops the CPU arrays).', 'emitter_calc_type')
add(0x753, 1, 'uint8', 'velocity_coord', C, 'Particle coordinate mode 0..2: `0x7100ad7ea0` / `0x7100ada3cc` / `0x7100ae0e34` use the emitter matrix for 0, per-particle basis arrays for 1 (gravity normalizes the basis) and 2 (matrix rows plus translation); `0x7100ad5a74` allocates the extra basis arrays when non-zero; `0x7100adc8a8` maps it through a three-entry table into shader flag word 1.', 'velocity_coord')
add(0x754, 1, 'uint8', 'fade_emit_stop', P, '`0x7100ad73d0`: `param_6 = (D[0x754] == 0) & param_6` before the fade-out test, i.e. when set the emitter keeps its emission flag instead of being stopped by the fade-out. TotK name; behavior decoded only to the gate.', 'fade_emit_stop')
add(0x755, 1, 'uint8', 'fade_out_curve', C, 'Either 0x755 or 0x756 non-zero enables fade-out in `0x7100ad73d0`; `0x7100ad8854` multiplies the alpha factor (`Emitter+0x64`) when 0x755 is set.', 'fade_out_curve')
add(0x756, 1, 'uint8', 'fade_out_scale', C, 'See 0x755; `0x7100ad8854` multiplies the scale factor (`Emitter+0x64`) when 0x756 is set.', 'fade_out_scale')
add(0x757, 1, 'uint8', 'seed_source', C, '`Emitter::Initialize` (0x7100ad583c): 0 = draw from the global generator, 1 = ESET `+0x34`, 2 = `D[0x760] * -0x2023e3cb`.', 'seed_source')
add(0x758, 1, 'uint8', 'update_matrix_by_emit', C, '`Emitter::UpdateByEmit` (0x7100ad69b0) calls `CreateResMatrix` (0x7100ad6560) after each emit when non-zero.', 'update_matrix_by_emit')
unused(0x759, 2, 'Nonzero in the corpus but no reader found (TotK 0xCA6..0xCA7 is also unused).')
add(0x75B, 1, 'uint8', 'fade_in_curve', C, '`Initialize` sets the initial fade value to 0 when this or 0x75C is set; `0x7100ad73d0` ramps `Emitter+0x68` by `dt / D[0x76c]`; `0x7100ad8854` applies it to the alpha factor.', 'fade_in_curve')
add(0x75C, 1, 'uint8', 'fade_in_scale', C, 'See 0x75B; `0x7100ad8854` applies the ramp to the scale factor when set.', 'fade_in_scale')
unused(0x75D, 3, 'Zero in every file; no reader found.')
add(0x760, 4, 'uint32', 'fixed_seed', C, '`Initialize`: seed = `D[0x760] * -0x2023e3cb` when `seed_source == 2`.', 'fixed_seed')
add(0x764, 4, 'uint32', 'draw_path', P, '`CreateEmitter` (0x7100ac9b08) loads it and passes it to the setter at 0x7100ad6b54, which stores it at `Emitter+0x3b0`. Matches TotK `draw_path`; the mask test was not re-read in BotW.', 'draw_path')
add(0x768, 4, 'int32', 'fade_out_time', C, '`0x7100ad73d0`: `Emitter+0x64 -= dt / (float)D[0x768]`; a value below 1 ends the emitter immediately.', 'fade_out_time')
add(0x76C, 4, 'int32', 'fade_in_time', C, '`0x7100ad73d0`: `Emitter+0x68 += dt / (float)D[0x76c]`; a value below 1 jumps to 1.0.', 'fade_in_time')
add(0x770, 12, 'float[3]', 'emitter_trans_xyz', C, '`CreateResMatrix` (0x7100ad6560): translation base.', 'emitter_trans_xyz')
add(0x77C, 12, 'float[3]', 'emitter_trans_rnd', C, '`CreateResMatrix`: translation random range (`rand * 2 - 1` times this).', 'emitter_trans_rnd')
add(0x788, 12, 'float[3]', 'emitter_rot_xyz', C, '`CreateResMatrix`: Euler rotation base, wrapped into [-pi, pi] by the shared range-reduction constants and evaluated with the sine/cosine polynomials.', 'emitter_rot_xyz')
add(0x794, 12, 'float[3]', 'emitter_rot_rnd', C, '`CreateResMatrix`: rotation random range.', 'emitter_rot_rnd')
add(0x7A0, 12, 'float[3]', 'emitter_scale_xyz', C, '`CreateResMatrix`: scale applied to the rotation basis.', 'emitter_scale_xyz')
add(0x7AC, 12, 'float[3]', 'emitter_color0_rgb', C, '`0x7100ad5a74` copies it to `Emitter+0x5e4`; `0x7100ad8854` multiplies the animated emitter color0 by it when filling the dynamic uniform block.', 'emitter_color0_rgb')
add(0x7B8, 4, 'float', 'emitter_color0_alpha', C, 'Copied to `Emitter+0x614` (0x7100ad5a74) and multiplied into the dynamic block alpha (0x7100ad8854).', 'emitter_color0_alpha')
add(0x7BC, 12, 'float[3]', 'emitter_color1_rgb', C, 'Copied to `Emitter+0x5f0` and multiplied into the dynamic block color1 (0x7100ad8854).', 'emitter_color1_rgb')
add(0x7C8, 4, 'float', 'emitter_color1_alpha', C, 'Copied to `Emitter+0x620` and multiplied into the dynamic block alpha1 (0x7100ad8854).', 'emitter_color1_alpha')
unused(0x7CC, 12, 'No reader found. The bytes hold floats in the corpus (0x7D0 is -1.0 in 94% of emitters); TotK 0xD1C..0xD27 is the same unused hole.')
# ------------------------------------------------------------------ child inheritance
inh = [(0x7D8, 'inherit_parent_velocity', 'adds the parent particle velocity times `D[0x7e8]` to the child particle velocity'),
       (0x7D9, 'inherit_parent_scale', 'evaluates the parent scale (`FUN_7100ae1514`) times `D[0x7ec]` into the child scale'),
       (0x7DA, 'inherit_parent_rotation', 'writes the parent rotation matrix result (`CalculateRotationMatrix`)'),
       ]
for off, nm, what in inh:
    add(off, 1, 'uint8', nm, C, '`0x7100ad9bc0` (child particle setup): %s.' % what, nm)
unused(0x7DB, 1, 'Zero in every file; no reader found (TotK 0xD33).')
add(0x7DC, 1, 'uint8', 'inherit_parent_color0_rgb', C, '`0x7100ad9bc0`: runs the parent color0 evaluator (`0x7100ae19ac`) into the child color0 rgb.', 'inherit_parent_color0_rgb')
add(0x7DD, 1, 'uint8', 'inherit_parent_color1_rgb', C, '`0x7100ad9bc0`: runs the parent color1 evaluator (`0x7100ae1e08`) into the child color1 rgb.', 'inherit_parent_color1_rgb')
add(0x7DE, 1, 'uint8', 'inherit_parent_alpha0', C, '`0x7100ad9bc0`: copies the parent alpha0 into the child; `0x7100ad8854` additionally multiplies it each frame when 0x7E2 is set.', 'inherit_parent_alpha0')
add(0x7DF, 1, 'uint8', 'inherit_parent_alpha1', C, '`0x7100ad9bc0`: copies the parent alpha1 into the child; `0x7100ad8854` multiplies it each frame when 0x7E3 is set.', 'inherit_parent_alpha1')
unused(0x7E0, 1, 'Nonzero in the corpus but no reader found (TotK 0xD38 is also unused).')
add(0x7E1, 1, 'uint8', 'child_pre_draw', C, '`EmitterSet::Draw` reads it at 0x7100aca6e8 and 0x7100aca798 to draw the child before or after the parent.', 'child_pre_draw')
add(0x7E2, 1, 'uint8', 'inherit_parent_alpha0_each_frame', C, '`0x7100ad8854`: with 0x7DE also set, multiplies the emitter alpha factor by the parent alpha every frame.', 'inherit_parent_alpha0_each_frame')
add(0x7E3, 1, 'uint8', 'inherit_parent_alpha1_each_frame', C, '`0x7100ad8854`: with 0x7DF also set, multiplies the alpha1 factor by the parent alpha every frame.', 'inherit_parent_alpha1_each_frame')
add(0x7E4, 1, 'uint8', 'inherit_enable_emitter_particle', C, 'Marks an emitter that follows a parent particle: `0x7100ad73d0` and `0x7100ad7ea0` use the parent-particle timing branch (`D[0x7f8] / 100 * parentLife`) and `0x7100ad9bc0` takes the inheritance branch only when it is non-zero.', 'inherit_enable_emitter_particle')
unused(0x7E5, 3, 'Zero in every file; no reader found.')
add(0x7E8, 4, 'float', 'inherit_parent_velocity_rate', C, '`0x7100ad9bc0`: factor on the inherited parent velocity.', 'inherit_parent_velocity_rate')
add(0x7EC, 4, 'float', 'inherit_parent_scale_rate', C, '`0x7100ad9bc0`: factor on the inherited parent scale.', 'inherit_parent_scale_rate')
# ------------------------------------------------------------------ emission timing
add(0x7F0, 1, 'uint8', 'emit_loop_mode', C, '0 = the emitter never ends, non-zero = it ends at `start + duration` (+ lifespan): `0x7100ad73d0` (end test), `0x7100ae44ec` (ESET aggregation sets the "has endless emitter" flag when 0).', 'emit_loop_mode')
add(0x7F1, 1, 'uint8', 'gravity_coord', C, '`0x7100ae0e34`: 0 applies gravity in world space, non-zero transforms it by the emitter/particle basis; `0x7100adc8a8` sets shader flag bit 8.', 'gravity_coord')
add(0x7F2, 1, 'uint8', 'emit_dist_enable', C, '`0x7100ad7084` takes the distance-based emission branch when non-zero; `0x7100ad6358` then uses `D[0x834]` as the particle capacity.', 'emit_dist_enable')
add(0x7F3, 1, 'uint8', 'designated_direction_transform_enable', C, '`0x7100ada3cc`: when non-zero the designated direction at 0x974 is transformed by the emitter basis (`0x7100ad529c`) before it contributes to velocity.', 'designated_direction_transform_enable')
add(0x7F4, 4, 'uint32', 'emit_start_delay', C, '`0x7100ad73d0`: emission window start frame.', 'emit_start_delay')
add(0x7F8, 4, 'uint32', 'child_emit_timing', C, '`0x7100ad73d0`: for child emitters `start = parentLife * D[0x7f8] / 100`.', 'child_emit_timing')
add(0x7FC, 4, 'uint32', 'emit_duration', C, '`0x7100ad73d0`: window end = start + duration.', 'emit_duration')
add(0x800, 4, 'float', 'emit_rate', C, '`TryEmitParticle` (0x7100ad7084): particles per frame; `UpdateParams` replaces it for shape types 2/13 (1.0), 5 (table by 0x83C), 6 (count 0x83D) and 15 (callback) while 0x874 == 0.', 'emit_rate')
add(0x804, 4, 'int32', 'emit_rate_random_percent', C, '`TryEmitParticle`: `rate * (percent / 100) * random` subtracted from the rate (i32 here, byte in TotK).', 'emit_rate_random_percent')
add(0x808, 4, 'int32', 'emit_interval', C, '`UpdateByEmit` (0x7100ad69b0): interval = `D[0x808] + 1 + (random * D[0x80c] >> 32)`.', 'emit_interval')
add(0x80C, 4, 'int32', 'emit_interval_random', C, '`UpdateByEmit`: random extension of the interval (see 0x808).', 'emit_interval_random')
add(0x810, 4, 'float', 'emission_position_table_offset_scale', C, '`0x7100ada3cc`: when non-zero, adds `table[counter++ & 0x1ff].xy * value` to the emitted particle position X/Y (counter at `Emitter+0xa2`). TotK 0xD68 applies the same table step to the direction instead.', None)
add(0x814, 4, 'float', 'gravity_scale', C, '`0x7100ad5a74` copies it to `Emitter+0x65c`; `0x7100ae0e34` multiplies the gravity vector by it (skipped when <= 0).', 'gravity_scale')
add(0x818, 12, 'float[3]', 'gravity_xyz', C, '`0x7100ae0e34`: acceleration `scale * g * dt`.', 'gravity_xyz')
add(0x824, 4, 'float', 'emit_dist_unit', C, '`0x7100ad7084`: spacing of distance-based emission, `n = (int)(travel / unit)`.', 'emit_dist_unit')
add(0x828, 4, 'float', 'emit_dist_min', C, '`0x7100ad7084`: lower clamp of the travel length.', 'emit_dist_min')
add(0x82C, 4, 'float', 'emit_dist_max', C, '`0x7100ad7084`: upper clamp of the travel length.', 'emit_dist_max')
add(0x830, 4, 'float', 'emit_dist_margin', C, '`0x7100ad7084`: travel shorter than this counts as no movement.', 'emit_dist_margin')
add(0x834, 4, 'int32', 'emit_dist_particle_max', C, '`0x7100ad6358`: capacity used for distance-based emitters.', 'emit_dist_particle_max')
# ------------------------------------------------------------------ shape
add(0x838, 1, 'uint8', 'shape_type', C, '`0x7100ada3cc` dispatches through a 16-entry function table (0x71024bd018) indexed by this byte: 1 circle (0x7100add248), 2 circle equally divided (0x7100add418), 3 circle fill (0x7100add704), 5 sphere equally 64-divided (0x7100addf24), 6 sphere equally 32-divided (0x7100ade3b8), 11 box fill (0x7100adf0a8), 15 primitive (0x7100adf59c); entries 0, 4, 7..10, 12..14 not named here. `UpdateParams` special-cases types 2, 5, 6, 13 and 15.', 'shape_type')
add(0x839, 1, 'uint8', 'shape_angle_mode', C, '`CalculateEmitCircle` (0x7100add24c): 0 uses the fixed phase 0x848, otherwise a time-varying phase.', 'shape_angle_mode')
add(0x83A, 1, 'uint8', 'shape_rot_mode', P, 'Read by the sphere/circle emit functions (0x7100adda2c, 0x7100addaa4, 0x7100ade890 ...); same position and use as TotK 0xD92.', 'shape_rot_mode')
unused(0x83B, 1, 'Zero in every file; no reader found.')
add(0x83C, 1, 'uint8', 'sphere_direction_table_index', C, '`UpdateParams` (0x7100adc1a0): for shape type 5 with 0x874 == 0, `emit_rate = table[D[0x83c]]`.', 'sphere_direction_table_index')
add(0x83D, 1, 'uint8', 'sphere64_division_count', C, '`UpdateParams` (0x7100adc1c0): for shape type 6 with 0x874 == 0, `emit_rate = (float)D[0x83d]`.', 'sphere64_division_count')
add(0x83E, 1, 'uint8', 'shape_rot_variant', P, 'Read by the sphere emit functions (0x7100addc70, 0x7100ade104 ...); same position as TotK 0xD95.', 'shape_rot_variant')
unused(0x83F, 1, 'Nonzero in the corpus but no reader found.')
add(0x840, 4, 'float', 'shape_angle_b', C, '`CalculateEmitCircle`: arc spread (`spread * (rand - 0.5)`).', 'shape_angle_b')
add(0x844, 4, 'float', 'shape_angle_c', P, 'Read by the sphere emit functions (0x7100adda44, 0x7100ade008 ...); same position as TotK 0xD9C.', 'shape_angle_c')
add(0x848, 4, 'float', 'shape_angle_d', C, '`CalculateEmitCircle`: fixed phase when 0x839 is 0.', 'shape_angle_d')
add(0x84C, 4, 'float', 'shape_angle_random', C, '`CalculateEmitCircleEquallyDivided` (0x7100add4fc): `angle += D[0x84c] * (2 * rand - 1)`. BotW only; TotK 0xDA4 has no reader.', None)
add(0x850, 4, 'float', 'shape_fill_ratio', P, 'Read by the fill shape functions (0x7100add8b0, 0x7100adeb00, 0x7100adf0bc); same position as TotK 0xDA8.', 'shape_fill_ratio')
add(0x854, 4, 'float', 'line_center_bias', P, 'Read by the line shape functions (0x7100adf2fc, 0x7100adf3fc); same position as TotK 0xDAC.', 'line_center_bias')
add(0x858, 4, 'float', 'line_length', P, 'Read by the line shape functions (0x7100adf2d0, 0x7100adf344); same position as TotK 0xDB0.', 'line_length')
add(0x85C, 12, 'float[3]', 'shape_radius_xyz', C, '`CalculateEmitCircle`: X radius at 0x85C and Z radius at 0x864; Y read by the sphere/box functions.', 'shape_radius_xyz')
add(0x868, 12, 'float[3]', 'emitter_volume_scale_xyz', C, '`0x7100ad5a74` copies it to `Emitter+0x650`; `0x7100ad73d0` multiplies it by the ESET animation lanes `+0xe0..0xe8` each frame.', 'emitter_volume_scale_xyz')
add(0x874, 4, 'int32', 'primitive_dist_mode', C, '`CalculateEmitCircleEquallyDivided`: 0 = divided, 1 = random division index, 2 = sequential index (`Emitter+0x34`); `UpdateParams` forces -1 for shape types outside {2,5,6,13,15}.', 'primitive_dist_mode')
add(0x878, 8, 'uint64', 'mesh_primitive_idx', C, '`Resource::InitializeEmitterGraphicsResource` (0x7100ae3e54): `-1` = none, otherwise looked up (0x7100ae495c) into `EmitterResource+0x80`.', 'mesh_primitive_idx')
add(0x880, 4, 'int32', 'shape_divisions', C, '`CalculateEmitCircleEquallyDivided`: division count.', 'shape_divisions')
add(0x884, 4, 'uint32', 'circle_division_random_reduction_percent', C, '`CalculateEmitCircleEquallyDivided`: random reduction of the division count (`count - count * pct * rand * k`).', 'circle_division_random_reduction_percent')
add(0x888, 4, 'uint32', 'line_division_count', P, 'Read at 0x7100ad64fc, 0x7100ad6534, 0x7100adf354 (line division functions); same position as TotK 0xDE0.', 'line_division_count')
add(0x88C, 4, 'uint32', 'line_division_reduction_percent', P, 'Read at 0x7100adf36c; same position as TotK 0xDE4.', 'line_division_reduction_percent')
unused(0x890, 8, 'Byte 0x890 is nonzero in the corpus but no reader found; TotK has no counterpart.')
# ------------------------------------------------------------------ render state
add(0x898, 1, 'uint8', 'blend_target_enable', C, '`0x7100ae2834` (render-state setup): bit 0 of the blend target state.', 'blend_target_enable')
add(0x899, 1, 'uint8', 'depth_test_enable', C, '`0x7100ae2834`: bit 0 of the depth-stencil flags. BotW only (TotK 0xDE9 unused).', None)
add(0x89A, 1, 'uint8', 'depth_compare_func', C, '`0x7100ae2834`: depth comparison function (accepted when < 8). TotK 0xDEA `depth_stencil_mode_index` occupies the same slot.', 'depth_stencil_mode_index')
add(0x89B, 1, 'uint8', 'depth_write_enable', C, '`0x7100ae2834`: bit 1 of the depth-stencil flags; the draw dispatcher (0x7100acdcfc) also selects between two draw routines by this byte. TotK 0xDEB `depth_sort_ascending` occupies the same slot.', 'depth_sort_ascending')
unused(0x89C, 2, 'Bytes 4 and 5 of the render-state block are never read by `0x7100ae2834`.')
add(0x89E, 1, 'uint8', 'blend_mode_index', C, '`0x7100ae2834`: values below 5 index a packed table of blend factors/operations.', 'blend_mode_index')
add(0x89F, 1, 'uint8', 'cull_mode_index', C, '`0x7100ae2834`: values below 3 map through a packed table (0 none, 1/2 front/back).', 'cull_mode_index')
unused(0x8A0, 8, 'Not read by `0x7100ae2834`; 0x8A0 is a float in the corpus but has no reader.')
add(0x8A8, 1, 'uint8', 'emit_infinite_flag', C, '`0x7100ad73d0`: when 1, the emitter is never retired (end test skipped); particle lifetime is also the "infinite" constant in `0x7100ada3cc`; `0x7100ae44ec` sets the ESET `+0x2a` flag.', 'emit_infinite_flag')
add(0x8A9, 1, 'uint8', 'is_trimming_prim', C, '`0x7100ae3e54`: trimming primitive is resolved only when set and 0x8D0 != -1.', 'is_trimming_prim')
unused(0x8AA, 2, 'No reader found.')
add(0x8AC, 1, 'uint8', 'shader_mode_index_DFC', C, '`0x7100adc8a8`: 0 sets shader flag bit `0x20000000`, 1 sets `0x40000000`. TotK 0xDFC uses the same slot.', 'shader_mode_index_DFC')
for i, ax in enumerate('xyz'):
    add(0x8AD + i, 1, 'uint8', 'rotation_random_sign_%s_enable' % ax, C, '`CalculateRotationMatrix` (0x7100ae2264): flips the %s rotation sign when the random value is >= 0.5; `0x7100adc8a8` sets feature bit 0x%X.' % (ax.upper(), 0x10000 << i), 'rotation_random_sign_%s_enable' % ax)
for i in range(3):
    add(0x8B0 + i, 1, 'uint8', 'rotation_param_lane%d_enable' % i, C, '`UpdateParams`: when 0, lane %d of the four rotation vec4s (0x700, 0x710, 0x720, 0x730) is zeroed. (The previous map called this block "light uniforms".)' % i, 'rotation_param_lane%d_enable' % i)
add(0x8B3, 1, 'uint8', 'shader_opt_flag3', C, '`0x7100adc8a8`: sets shader flag bit `0x10000000`.', 'shader_opt_flag3')
add(0x8B4, 1, 'uint8', 'shader_opt_flag4', C, '`0x7100adc8a8`: sets shader flag word 1 bit 1.', 'shader_opt_flag4')
unused(0x8B5, 3, 'Bytes 0x8B5..0x8B6 nonzero in the corpus; no reader found (TotK 0xE05..0xE07 also unused).')
add(0x8B8, 4, 'int32', 'particle_lifespan', C, '`0x7100ad5a74` copies it as float to `Emitter+0x608`; `0x7100ada3cc` computes lifetime = `D+0x608 * (1 + random pct)`; `0x7100ad73d0` end test uses it.', 'particle_lifespan')
add(0x8BC, 4, 'int32', 'particle_lifespan_random_percent', C, '`0x7100ada3cc`: `floor(random * D[0x8bc] >> 32)` times a 0.01 constant added to the lifespan factor (i32 here, byte in TotK).', 'particle_lifespan_random_percent')
add(0x8C0, 4, 'float', 'particle_attribute_w_random_amplitude', C, '`0x7100ada3cc`: stores `value + 1 + 2 * value * random` into the W lane of the particle scale vector.', 'particle_attribute_w_random_amplitude')
unused(0x8C4, 4, 'No reader found (TotK 0xE14 also unused).')
add(0x8C8, 8, 'uint64', 'g3d_primitive_idx', C, '`EmitterResource::Setup` (0x7100adb7c0): looked up through `GetG3dPrimitive` when not -1.', 'g3d_primitive_idx')
add(0x8D0, 8, 'uint64', 'trim_primitive_idx', C, '`0x7100ae3e54`: looked up when `is_trimming_prim` is set and the index is not -1.', 'trim_primitive_idx')
for i, nm in enumerate(['color0', 'alpha0', 'color1', 'alpha1', 'scale']):
    add(0x8D8 + i, 1, 'uint8', 'loop_%s_enable' % nm, C, '`UpdateParams` writes `runtime_loop_track%d_rate = float(period)` only when set; `0x7100ae19ac` / `0x7100ae1514` pass the period to `Calculate8KeyAnim` only when set.' % i, 'loop_%s_enable' % nm)
for i, nm in enumerate(['color0', 'alpha0', 'color1', 'alpha1', 'scale']):
    add(0x8DD + i, 1, 'uint8', 'loop_%s_random_phase' % nm, C, '`UpdateParams` writes 1.0/0.0 to `runtime_loop_track%d_random_enable`; the evaluators pass it as the random-phase flag.' % i, 'loop_%s_random_phase' % nm)
unused(0x8E2, 2, 'No reader found.')
for i, nm in enumerate(['color0', 'alpha0', 'color1', 'alpha1', 'scale']):
    add(0x8E4 + 4 * i, 4, 'int32', 'loop_%s_period_i32' % nm, C, 'Loop period in frames: `UpdateParams` converts it to float into `runtime_loop_track%d_rate`; read directly by the evaluators.' % i, None)
unused(0x8F8, 8, 'Bytes nonzero in the corpus but no reader found; TotK uses 0xE40..0xE44 for key interpolation modes, which BotW does not read.')
unused(0x900, 20, 'No reader found (0x900 appears only as the low bits of a constant-pool address).')
# ------------------------------------------------------------------ shaders / custom
add(0x914, 4, 'int32', 'shader_idx_normal', C, '`EmitterResource::Setup` (0x7100adb7c0): `ShaderManager::GetShader(D[0x914])` into `EmitterResource+0x340`.', 'shader_idx_normal')
add(0x918, 4, 'int32', 'compute_shader0', C, '`Setup`: `shaderTable + D[0x918] * 0x40` into `EmitterResource+0x358` when not -1.', 'compute_shader0')
add(0x91C, 4, 'int32', 'shader_idx_pass1', C, '`Setup`: second graphics shader, skipped when -1 (`EmitterResource+0x348`).', 'shader_idx_pass1')
unused(0x920, 4, 'Zero in every file; no reader found.')
add(0x924, 4, 'int32', 'shader_idx_pass2', C, '`Setup`: third graphics shader, skipped when -1 (`EmitterResource+0x350`).', 'shader_idx_pass2')
unused(0x928, 4, 'No reader found.')
add(0x92C, 4, 'int32', 'custom_shader_index', P, '`0x7100ad5a74`: a non-zero value together with chunk slot `+0x398` (custom shader) marks the emitter as having a custom-shader callback. Matches TotK 0xE74; the callback-slot arithmetic was not re-read.', 'custom_shader_index')
unused(0x930, 0x38, 'No reader found (TotK 0xE78..0xEF7 is the same unused block).')
add(0x968, 4, 'int32', 'custom_action_index', P, '`0x7100ad5a74`: positive value with chunk slot `+0x390` (custom action) enables the action callback. Matches TotK 0xEF8.', 'custom_action_index')
add(0x96C, 4, 'float', 'all_directional_speed', P, '`0x7100ad5a74` copies it to `Emitter+0x62c`; `0x7100ad73d0` multiplies it by the ESET animation lane `+0x1d8` each frame. Same slot as TotK 0xEFC; the downstream consumer was not decoded.', 'all_directional_speed')
add(0x970, 4, 'float', 'designated_direction_speed', C, '`0x7100ad5a74` copies it to `Emitter+0x638`; `0x7100ada3cc` scales the designated direction by it (times the ESET lane `+0x200`).', 'designated_direction_speed')
add(0x974, 12, 'float[3]', 'designated_direction_xyz', C, '`0x7100ada3cc`: base direction vector added to the emitted direction (`dir + vec * speed`), optionally transformed when 0x7F3 is set.', 'designated_direction_xyz')
add(0x980, 4, 'float', 'emission_direction_spread_degrees', C, '`0x7100ada3cc`: `value / 90 + 1` is the lower bound of the cone sample; non-zero rotates the emitted direction into a random cone around it.', 'emission_direction_spread_degrees')
add(0x984, 4, 'float', 'emission_tangent_amount', C, '`0x7100ada3cc`: adds `normalize(XZ tangent of the emitted position) * value` to the direction (random XZ direction at the origin).', 'emission_tangent_amount')
add(0x988, 4, 'float', 'emission_direction_random_x', C, '`0x7100ada3cc`: `direction.x += randomTable.x * value`.', 'emission_direction_random_x')
add(0x98C, 4, 'float', 'emission_direction_random_y', C, '`0x7100ada3cc`: `direction.y += randomTable.y * value`.', 'emission_direction_random_y')
add(0x990, 4, 'float', 'emission_direction_random_z', C, '`0x7100ada3cc`: `direction.z += randomTable.z * value`.', 'emission_direction_random_z')
add(0x994, 4, 'float', 'initial_speed_random_percent', C, '`0x7100ada3cc`: speed factor `1 + eset(+0x1dc) * random * k * (value / 100)`.', 'initial_speed_random_percent')
add(0x998, 4, 'float', 'emitter_motion_inherit_scale', C, '`0x7100ada3cc`: adds `emitterVelocity(Emitter+0x380) * value` to the direction.', 'emitter_motion_inherit_scale')
unused(0x99C, 8, 'Nonzero in the corpus but no reader found; TotK 0xF2C `emitter_motion_inherit_max` and its neighbours have no BotW reader.')
# ------------------------------------------------------------------ color / scale
for off, nm in [(0x9A4, 'color0_mode'), (0x9A5, 'color1_mode'), (0x9A6, 'alpha0_mode'), (0x9A7, 'alpha1_mode')]:
    tgt = {'color0_mode': (0x3C0, 0x9A8), 'color1_mode': (0x4C0, 0x9B8), 'alpha0_mode': (0x440, 0x9B4), 'alpha1_mode': (0x540, 0x9C4)}[nm]
    add(off, 1, 'uint8', nm, C, '`UpdateParams`: 0 copies the constant (0x%X) into key 0 of the table at 0x%X; `0x7100ae19ac`/`0x7100ae1e08`: 2 evaluates the keys, 3 (color only) selects a discrete key.' % (tgt[1], tgt[0]), nm)
add(0x9A8, 12, 'float[3]', 'color0_const_rgb', C, '`UpdateParams` and `0x7100ae19ac`: constant color0.', 'color0_const_rgb')
add(0x9B4, 4, 'float', 'alpha0_const', C, 'Constant alpha0 (`UpdateParams` writes it to 0x440).', 'alpha0_const')
add(0x9B8, 12, 'float[3]', 'color1_const_rgb', C, 'Constant color1 (`UpdateParams` writes it to 0x4C0).', 'color1_const_rgb')
add(0x9C4, 4, 'float', 'alpha1_const', C, 'Constant alpha1 (`UpdateParams` writes it to 0x540).', 'alpha1_const')
add(0x9C8, 12, 'float[3]', 'particle_scale_xyz', C, '`0x7100ad5a74` copies it to `Emitter+0x644`; `0x7100ada3cc` multiplies it by the ESET scale lanes into the particle scale.', 'particle_scale_xyz')
add(0x9D4, 12, 'float[3]', 'particle_scale_rnd', C, '`0x7100ada3cc`: uniform random factor `1 + (v / 100) * random` (x), or per-axis when 0x9D4 != 0x9D8.', 'particle_scale_rnd')
unused(0x9E0, 12, 'No reader found (TotK 0xF80..0xF8B also unused).')
add(0x9EC, 1, 'uint8', 'waveform_alpha_enable', C, '`0x7100ae19ac`: multiplies alpha by waveform 0 when set.', 'waveform_alpha_enable')
add(0x9ED, 1, 'uint8', 'waveform_scale_x_enable', C, '`0x7100ae1514`: applies waveform 0 to scale X (and Z) when set.', 'waveform_scale_x_enable')
add(0x9EE, 1, 'uint8', 'waveform_scale_y_enable', C, '`0x7100ae1514`: additionally applies waveform 1 to scale Y when set.', 'waveform_scale_y_enable')
add(0x9EF, 1, 'uint8', 'waveform_mode_packed', C, 'High nibble: 0 cosine, 1 sawtooth, 2 square (`0x7100ae19ac`, `0x7100ae1514`); `0x7100adc8a8` maps values below 0x30 through a table into shader flag word 0.', 'waveform_mode_packed')
unused(0x9F0, 8, 'No reader found.')
# ------------------------------------------------------------------ textures
for i in range(3):
    g = 0x9F8 + 0x20 * i
    add(g, 8, 'uint64', 'tex_slot%d_guid' % i, C, '`InitializeEmitterGraphicsResource` (0x7100ae3e54): passed to the texture lookup `0x7100ae42a8` (-1 = none) into `EmitterResource+0x%X`.' % (0x48 + 8 * i), 'tex_slot%d_guid' % i)
    add(g + 8, 3, 'bytes[3]', 'tex_slot%d_sampler_select' % i, C, '`0x7100ad3578` (called from `UpdateParams` with `D+0x%X`): sampler index = `b[1] + b[0] * 4 + b[2] * 16` into the sampler table (stride 0xA8). Which byte is filter vs wrap is not established.' % g, None)
    add(g + 11, 21, 'bytes[21]', 'unverified_%03X_%03X' % (g + 11, g + 0x1F), U, 'No reader found; part of the 0x20-byte slot record. Bytes 0x%X..0x%X are nonzero in the corpus.' % (g + 0x14, g + 0x18))
for i in range(3):
    b = 0xA58 + 0x10 * i
    add(b, 1, 'uint8', 'tex%d_mode_index' % i, C, '`0x7100adc8a8`: 1..3 set one shader feature bit each; 4 builds the frame table (see `tex%d_flipbook_runtime_block`).' % i, 'tex%d_mode_index' % i)
    add(b + 1, 1, 'uint8', 'tex%d_uv_scroll' % i, C, '`UpdateParams`: zeroes the scroll fields of `tex%d_uniform_block` when 0.' % i, 'tex%d_uv_scroll' % i)
    add(b + 2, 1, 'uint8', 'tex%d_uv_rotate' % i, C, '`UpdateParams`: zeroes the rotate fields when 0.', 'tex%d_uv_rotate' % i)
    add(b + 3, 1, 'uint8', 'tex%d_uv_scale' % i, C, '`UpdateParams`: writes the default scale (1.0) when 0.', 'tex%d_uv_scale' % i)
    add(b + 4, 1, 'uint8', 'tex%d_uv_domain_scale_mode' % i, C, '`UpdateParams`: values below 4 select two floats from a table into the uniform block.', 'tex%d_uv_domain_scale_mode' % i)
    for k, bit in enumerate([[0x80000, 0x100000, 0x2000000], [0x200000, 0x400000, 0x4000000], [0x800000, 0x1000000, 0x8000000]][i]):
        add(b + 5 + k, 1, 'uint8', 'tex%d_shader_flag%d' % (i, k), C, '`0x7100adc8a8`: sets shader feature bit 0x%X when non-zero.' % bit, 'tex%d_shader_flag%d' % (i, k))
    unused(b + 8, 8, 'No reader found; byte 0x%X nonzero in the corpus.' % (b + 8) if i == 0 else 'No reader found.')


def section(game_rows):
    out = ['| BotW Offset | Size (B) | Type | Field | Status | Evidence |', '|:---:|:---:|:---:|:---|:---:|:---|']
    for r in game_rows:
        rng = '`0x%03X`' % r['offset']
        if r['size'] > 8 or (r['size'] > 1 and r['type'].startswith('bytes')) or r['type'].startswith('float['):
            rng = '`0x%03X`–`0x%03X`' % (r['offset'], r['offset'] + r['size'] - 1)
        out.append('| %s | %d | `%s` | `%s` | %s | %s |' % (rng, r['size'], r['type'], r['name'], r['status'], r['evidence']))
    return out


def main():
    docs = sys.argv[1]
    totk = {r['name']: r for r in parse(os.path.join(docs, 'totk-emtr-offsets-ghidra.md'), 'totk')}
    rows.sort(key=lambda r: r['offset'])
    # coverage check
    pos = 0
    errs = []
    for r in rows:
        if r['offset'] != pos:
            errs.append('gap/overlap at 0x%X (row 0x%X %s)' % (pos, r['offset'], r['name']))
        pos = r['offset'] + r['size']
    if pos != 0xA88:
        errs.append('ends at 0x%X' % pos)
    names = [r['name'] for r in rows]
    if len(set(names)) != len(names):
        errs.append('duplicate names: ' + str([n for n in set(names) if names.count(n) > 1]))
    from botw_overrides import OVERRIDES
    for r in rows:
        if r['name'] in OVERRIDES:
            r['status'], r['evidence'] = OVERRIDES[r['name']]
    # append guesses from the TotK analogue
    for r in rows:
        if r['status'] == U and r['tk'] and r['tk'] in totk:
            m = re.search(r'\*\*Guess:\*\*\s*(.*?)(?=\s*\*\*Audit:\*\*|$)', totk[r['tk']]['evidence'])
            if m and '**Guess:**' not in r['evidence']:
                r['evidence'] += ' **Guess:** ' + m.group(1).strip()
    binp = os.environ.get('BOTW_EMTR_BIN')
    if binp:
        import struct
        import numpy as np
        d = open(binp, 'rb').read()
        i = 0
        blobs = []
        while i < len(d):
            ln = struct.unpack_from('<i', d, i)[0]
            i += 4 + 64 + 4
            blobs.append(d[i:i + ln])
            i += ln
        A = np.frombuffer(b''.join(b[:0xA88] for b in blobs if len(b) >= 0xA88), dtype=np.uint8).reshape(-1, 0xA88)
        n = A.shape[0]
        for r in rows:
            if r['status'] == X:
                k = int((A[:, r['offset']:r['offset'] + r['size']] != 0).any(axis=1).sum())
                r['evidence'] += ' Corpus: zero in all %d emitters.' % n if k == 0 else ' Corpus: nonzero in %d of %d emitters.' % (k, n)
    for e in errs:
        print('ERROR', e)
    path = os.path.join(docs, 'botw-emtr-offsets-ghidra.md')
    L = open(path, encoding='utf-8').read().split('\n')
    start = next(i for i, l in enumerate(L) if l.startswith('## 3.'))
    end = next(i for i, l in enumerate(L) if i > start and l.startswith('## '))
    head = ['## 3. Byte-by-Byte Field Map (re-verified against the BotW 1.6.0 Switch executable)', '',
            'Offsets are relative to the start of the EMTR data block (0xA88 bytes). Status: **Confirmed** = proven in BotW code, **Paired** = same role as a proven TotK field at an aligned position, **Unverified** = consumed but purpose unproven, **Unused** = no consumer found.',
            'Function addresses are Switch image addresses `0x71xxxxxxxx`. "body+N" means `EmitterResource+0x18` (= data + 0x50) plus N. The previous version of this table contained errors that this pass corrected (loop timers order, render-state bytes, the 0x974 block, "light uniforms", pulse flags).', '']
    new = L[:start] + head + section(rows) + [''] + L[end:]
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(new))
    from collections import Counter
    print(Counter(r['status'] for r in rows), len(rows))


if __name__ == '__main__':
    main()
