# TotK `EMTR` Serialized-Field Evidence & Field Map

This document records the exact byte offsets, types, executable behaviors, and Ghidra decompilation citations for Tears of the Kingdom (`nn::vfx2`, VFXB v51).

Offsets are relative to the serialized `EMTR` node's data start (`EMTR.node + dataRelativeOffset`).

The VFXB resource-node header is a separate `0x20`-byte structure. `Resource::InitializeEmitterSetResource` proves the fields used while walking the tree: FourCC at node `+0x00`, node size at `+0x04`, child-relative offset at `+0x08`, sibling-relative offset at `+0x0C`, attribute-relative offset at `+0x10`, and data-relative offset at `+0x14`. It computes the EMTR data pointer as `node + *(uint32_t *)(node + 0x14)`. Those node-header offsets must not be confused with the offsets below.

---

## 1. Resource-Tree & Allocation Architecture

### A. EmitterSet Node (`ESET`)
* **Fixed Data Size**: `0xB4` bytes.
* **Name**: `char[64]` at `ESET.data + 0x10` (null-terminated C-string).
* **Declared Emitter Count**: Unsigned 16-bit integer (`uint16`) at `ESET.data + 0x70` (read by `nn::vfx2::Resource::InitializeEmitterSetResource`).

### B. Emitter Node (`EMTR`)
* **Fixed Data Size**: `0x10C8` bytes (4,296 bytes).
* **Header / Body Split**: `InitializeEmitterSetResource` stores both the EMTR data pointer and a secondary pointer to `EMTR.data + 0x70`. The `0x60..0x6F` region is therefore pre-body/reserved alignment space, but its serialized contents must still be preserved until independently decoded.
* **Track Count**: Supports 10 keyframe tracks (`0x080`â€“`0x0A8`), expanding beyond BotW's 5 tracks.
* **Texture Slots**: Supports 6 texture slots (`0xF98`â€“`0x1028`, stride `0x18`), expanding beyond BotW's 3 slots (`0x9F8`â€“`0xA58`, stride `0x20`).
* **Sampler Config**: Stride `0x10` starting at `0x1028` (6 slots total: `0x1028`â€“`0x1088`).

---

## 2. Byte-by-Byte Verified Field Map

| TotK Offset | BotW Offset | Delta | Size (B) | Type | Field Name | Executable Behavior & Verification Evidence |
|:---:|:---:|:---:|:---:|:---:|:---|:---|
| `0x000` | `0x000` | `+0x000` | 4 | `bytes[4]` | `unverified_000` | EMTR data, not the node FourCC. The FourCC is in the separate node header. No unambiguous data-field reader has yet established this value's meaning. |
| `0x004` | `0x004` | `+0x000` | 4 | `bytes[4]` | `unverified_004` | EMTR data, not the node size. No unambiguous data-field reader has yet established this value's meaning. |
| `0x008` | `0x008` | `+0x000` | 4 | `bytes[4]` | `unverified_008` | EMTR data, not the VFXB version. The format version belongs to the file header. No unambiguous data-field reader has yet established this value's meaning. |
| `0x00C` | `0x00C` | `+0x000` | 4 | `bytes[4]` | `unverified_00C` | EMTR data, not node-header flags. No unambiguous data-field reader has yet established this value's meaning. |
| `0x010` | `0x010` | `+0x000` | 64 | `char[64]` | `emitter_name` | Null-terminated emitter name. `Resource::Trace` passes `EmitterResource->data + 0x10` as the emitter name in initialization and descriptor-slot diagnostics. |
| `0x050` | `0x050` | `+0x000` | 4 | `bytes[4]` | `unverified_050` | No unambiguous TotK executable use has established the serialized meaning. |
| `0x054` | `0x054` | `+0x000` | 4 | `bytes[4]` | `unverified_054` | No unambiguous TotK executable use has established the serialized meaning. |
| `0x058` | `0x058` | `+0x000` | 4 | `bytes[4]` | `unverified_058` | Not the fixed emitter seed used by `Emitter::Initialize`; that confirmed value is at `0xCB0`. No unambiguous use has established this slot's meaning. |
| `0x05C` | `0x05C` | `+0x000` | 4 | `bytes[4]` | `unverified_05C` | The `+0x5C` scale read in `Emitter::InitializeParticle` is through the emitter-set/runtime pointer, not the EMTR serialized-data pointer. It therefore does not prove an EMTR `global_scale` field here. |
| `0x060` | â€” | â€” | 16 | `bytes[16]` | `reserved_060_06F` | Pre-body/alignment region. Preserve verbatim; no reader has established that every byte is semantically inert. |
| **`+0x070`**| **`+0x050`**| **`+0x020`**| â€” | â€” | **Secondary body pointer** | `InitializeEmitterSetResource` stores `EMTR.data + 0x70` at `EmitterResource + 0x260` (`plVar16[0x4C]`). This is not the node-data pointer itself; `EmitterResource + 0x00` continues to hold `EMTR.data`. |
| `0x070` | `0x050` | `+0x020` | 4 | `uint32` | `runtime_shader_flags_word0` | Runtime staging, not an emitter ordinal. `EmitterResource::UpdateParams` overwrites this word with word 0 produced by `ShaderFlag::Initialize`. |
| `0x074` | `0x054` | `+0x020` | 4 | `uint32` | `runtime_shader_flags_word1` | Runtime staging overwritten with word 1 produced by `ShaderFlag::Initialize`. |
| `0x078` | `0x058` | `+0x020` | 4 | `uint32` | `runtime_shader_flags_word2`| Runtime staging overwritten with word 2 produced by `ShaderFlag::Initialize`. |
| `0x07C` | `0x05c` | `+0x020` | 4 | `uint32` | `runtime_attribute_word` | Runtime staging overwritten from an optional resolved attribute pointer (`EmitterResource + 0x340`). Its higher-level meaning is not established. |
| `0x080` | `0x060` | `+0x020` | 4 | `uint32` | `color0_key_count` | Key count (0..8) for Color0 RGB animation track (`UpdateParams:L489`). |
| `0x084` | `0x064` | `+0x020` | 4 | `uint32` | `alpha0_key_count` | Key count (0..8) for Alpha0 animation track (`UpdateParams:L669`). |
| `0x088` | `0x068` | `+0x020` | 4 | `uint32` | `color1_key_count` | Key count (0..8) for Color1 RGB animation track (`UpdateParams:L579`). |
| `0x08C` | `0x06c` | `+0x020` | 4 | `uint32` | `alpha1_key_count` | Key count (0..8) for Alpha1 animation track (`UpdateParams:L759`). |
| `0x090` | `0x070` | `+0x020` | 4 | `uint32` | `scale_key_count` | Key count (0..8) for Scale XYZ animation track (`UpdateParams:L849`). |
| `0x094` | `0x074` | `+0x020` | 4 | `uint32` | `track5_key_count` | Key count (0..8) for generic track 5. The previous Rotation label was not backed by a traced consumer. |
| `0x098` | â€” | TotK only | 4 | `uint32` | `track6_key_count` | Key count (0..8) for generic track 6. |
| `0x09C` | â€” | TotK only | 4 | `uint32` | `track7_key_count` | Key count (0..8) for generic track 7. |
| `0x0A0` | â€” | TotK only | 4 | `uint32` | `track8_key_count` | Key count (0..8) for generic track 8. |
| `0x0A4` | â€” | TotK only | 4 | `uint32` | `track9_key_count` | Key count (0..8) for generic track 9. |
| `0x0A8` | `0x074` | â€” | 8 | `bytes[8]` | `unverified_0A8_0AF`| No EMTR-data reader has established these serialized bytes. The previous vertex-attribute-slot claim confused virtual primitive-buffer queries in `CreateVertexState` with reads from the serialized data. |
| `0x0B0` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track0_rate` | Written by `UpdateParams` as `float(E34)` when `E28 != 0`, otherwise `0.0`. This overlaps serialized vertex-attribute bytes and is not an independent serialized input. |
| `0x0B4` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track1_rate` | Written by `UpdateParams` as `float(E36)` when `E29 != 0`, otherwise `0.0`. The first byte overlaps the final serialized vertex-attribute slot. |
| `0x0B8` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track2_rate` | Written by `UpdateParams` as `float(E38)` when `E2A != 0`, otherwise `0.0`. |
| `0x0BC` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track3_rate` | Written by `UpdateParams` as `float(E3A)` when `E2B != 0`, otherwise `0.0`. |
| `0x0C0` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track4_rate` | Written by `UpdateParams` as `float(E3C)` when `E2C != 0`, otherwise `0.0`. The previous `light_intensity` label was unsupported. |
| `0x0C4` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track0_random_enable` | Written by `UpdateParams` as `1.0` when `E2D != 0`, otherwise `0.0`. The previous `light_radius` label was unsupported. |
| `0x0C8` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track1_random_enable` | Written by `UpdateParams` as `1.0` when `E2E != 0`, otherwise `0.0`. The previous light-color label was unsupported. |
| `0x0CC` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track2_random_enable` | Written by `UpdateParams` as `1.0` when `E2F != 0`, otherwise `0.0`. |
| `0x0D0` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track3_random_enable` | Written by `UpdateParams` as `1.0` when `E30 != 0`, otherwise `0.0`. |
| `0x0D4` | â€” | Runtime overlay | 4 | `float` | `runtime_loop_track4_random_enable` | Written by `UpdateParams` as `1.0` when `E31 != 0`, otherwise `0.0`. |
| `0x0EC` | `0x744` | â€” | 4 | `bytes[4]` | `unverified_0EC`| The apparent `+0xEC` multiplier in particle code is an offset in the live `Emitter` object, not in the EMTR data pointer. It does not prove a serialized `init_velocity_factor` here. This word is included in the `0xCA0`-byte constant-buffer upload, but its shader meaning remains unverified. |
| `0x100`â€“`0x10F` | â€” | â€” | 16 | `bytes[16]` | `unverified_uniform_100_10F` | Uploaded in the `0xCA0`-byte constant-buffer copy; fields remain unmapped. |
| `0x110` | â€” | TotK only | 4 | `float` | `waveform0_amplitude` | Amplitude in `abs(1 - amplitude * waveform)` for alpha and Scale X modulation. |
| `0x114` | â€” | TotK only | 4 | `float` | `waveform1_amplitude` | Second-axis amplitude used for Scale Y modulation. |
| `0x118` | â€” | TotK only | 4 | `float` | `waveform0_period` | Divisor in `(time_offset + particle_time) / period` for alpha and Scale X. |
| `0x11C` | â€” | TotK only | 4 | `float` | `waveform1_period` | Second-axis period used for Scale Y. |
| `0x120` | â€” | TotK only | 4 | `float` | `waveform0_random_phase_scale` | Multiplies the per-particle random scalar added to waveform-0 phase. |
| `0x124` | â€” | TotK only | 4 | `float` | `waveform1_random_phase_scale` | Second-axis random phase scale. |
| `0x128` | â€” | TotK only | 4 | `float` | `waveform0_time_offset` | Added to particle time before division by waveform-0 period. |
| `0x12C` | â€” | TotK only | 4 | `float` | `waveform1_time_offset` | Second-axis time offset. |
| `0x130`â€“`0x13F` | â€” | â€” | 16 | `bytes[16]` | `unverified_uniform_130_13F` | Uploaded in the constant-buffer copy; fields remain unmapped. |
| `0x140`â€“`0x1CF` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex0_flipbook_runtime_block` | When `0x1028 == 4`, `ShaderFlag::Initialize` reads float count at `+0x08` (`0x148`), copies it to `+0x00` (`0x140`), initializes sequential `int32` indices at `+0x10` (`0x150`), and sets shader word 0 bit `0x80`. Other bytes remain unmapped. |
| `0x1D0`â€“`0x25F` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex1_flipbook_runtime_block` | Slot 1 equivalent: count `0x1D8`, runtime copy `0x1D0`, sequential indices from `0x1E0`, shader word 0 bit `0x800`. |
| `0x260`â€“`0x2EF` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex2_flipbook_runtime_block` | Slot 2 equivalent: count `0x268`, runtime copy `0x260`, sequential indices from `0x270`, shader word 0 bit `0x8000`. |
| `0x2F0`â€“`0x37F` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex3_flipbook_runtime_block` | Slot 3 equivalent: count `0x2F8`, runtime copy `0x2F0`, sequential indices from `0x300`, shader word 0 bit `0x80000`. |
| `0x380`â€“`0x40F` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex4_flipbook_runtime_block` | Slot 4 equivalent: count `0x388`, runtime copy `0x380`, sequential indices from `0x390`, shader word 0 bit `0x800000`. |
| `0x410`â€“`0x49F` | â€” | TotK only | `0x90` | `bytes[0x90]` | `tex5_flipbook_runtime_block` | Slot 5 equivalent: count `0x418`, runtime copy `0x410`, sequential indices from `0x420`, shader word 0 bit `0x8000000`. |
| `0x4A0`â€“`0x4EF` | `0x2C0` | â€” | `0x50`| `bytes` | `tex0_uniform_block`| Texture slot 0 UV-transform/animation staging block (`UpdateParams:L198-203`). |
| `0x4F0`â€“`0x53F` | `0x310` | â€” | `0x50`| `bytes` | `tex1_uniform_block`| Texture slot 1 UV-transform/animation staging block (`UpdateParams:L211-216`). |
| `0x540`â€“`0x58F` | `0x360` | â€” | `0x50`| `bytes` | `tex2_uniform_block`| Texture slot 2 UV-transform/animation staging block (`UpdateParams:L224-229`). |
| `0x590`â€“`0x5DF` | â€” | TotK only | `0x50`| `bytes` | `tex3_uniform_block`| Texture slot 3 UV-transform/animation staging block (`UpdateParams:L237-242`). |
| `0x5E0`â€“`0x62F` | â€” | TotK only | `0x50`| `bytes` | `tex4_uniform_block`| Texture slot 4 UV-transform/animation staging block (`UpdateParams:L250-255`). |
| `0x630`â€“`0x67F` | â€” | TotK only | `0x50`| `bytes` | `tex5_uniform_block`| Texture slot 5 UV-transform/animation staging block (`UpdateParams:L263-268`). |
| `0x680` | `0x3B0` | `+0x2D0` | 4 | `float` | `unverified_680` | Precedes the confirmed Color0 key array at `0x690`. `UpdateParams` uses `0x680 + key_count*0x10` only as address arithmetic that lands on the last Color0 key; that does not establish an `alpha_scale` semantic for the word at `0x680` itself. |
| `0x690` | `0x3C0` | `+0x2D0` | 128 | `float[8][4]` | `kf_color0` | Color0 RGB keyframes `(val.xyz, time.w)` (`UpdateParams:L97, 494`). |
| `0x710` | `0x440` | `+0x2D0` | 128 | `float[8][4]` | `kf_alpha0` | Alpha0 keyframes `(val.x, time.w)` (`UpdateParams:L105, 674`). |
| `0x790` | `0x4C0` | `+0x2D0` | 128 | `float[8][4]` | `kf_color1` | Color1 RGB keyframes `(val.xyz, time.w)` (`UpdateParams:L114, 584`). |
| `0x810` | `0x540` | `+0x2D0` | 128 | `float[8][4]` | `kf_alpha1` | Alpha1 keyframes `(val.x, time.w)` (`UpdateParams:L122, 764`). |
| `0x8D0` | `0x600` | `+0x2D0` | 128 | `float[8][4]` | `kf_scale` | Scale XYZ keyframes `(val.xyz, time.w)` (`UpdateParams:L854`). |
| `0x950` | `0x680` | `+0x2D0` | 128 | `float[8][4]` | `kf_track5` | Generic track-5 keyframe array. `UpdateParams` pads its final key to eight entries; higher-level consumer semantics are unverified. |
| `0x9D0` | â€” | TotK only | 128 | `float[8][4]` | `kf_track6` | Generic track-6 keyframe array with the same padding behavior. |
| `0xA50` | â€” | TotK only | 128 | `float[8][4]` | `kf_track7` | Generic track-7 keyframe array with the same padding behavior. |
| `0xAD0` | â€” | TotK only | 128 | `float[8][4]` | `kf_track8` | Generic track-8 keyframe array with the same padding behavior. |
| `0xB50` | â€” | TotK only | 128 | `float[8][4]` | `kf_track9` | Generic track-9 keyframe array with the same padding behavior. |
| `0xC10`â€“`0xC5F` | â€” | TotK only | `0x50` | `bytes[0x50]` | `rotation_modulation_core` | Three-lane rotation-modulation coefficients consumed by `CalculateRotationMatrix`; the exact individual coefficient roles are still being separated. `UpdateParams` clears lane components when the corresponding `E00..E02` byte is zero. This is not a normal-matrix buffer. |
| `0xC60` | â€” | TotK only | 12 | `float[3]` | `rotation_wave_amplitude_xyz` | Per-axis amplitude used by the rotation waveform branches. |
| `0xC70` | â€” | TotK only | 12 | `float[3]` | `rotation_wave_period_xyz` | Per-axis divisor in `(time_offset + particle_time) / period`. Defaults to `1.0` per lane when `E00..E02` is zero. |
| `0xC80` | â€” | TotK only | 12 | `float[3]` | `rotation_wave_time_offset_xyz` | Per-axis time offsets. |
| `0xC90` | â€” | TotK only | 12 | `float[3]` | `rotation_wave_random_phase_xyz` | Per-axis multipliers for the particle-random phase term. |
| `0xCA0` | `0x748` | `+0x558` | 1 | `uint8` | `sim_flags` | Simulation flag; copied into runtime flag bit 13 in `Emitter::Initialize` (`0x7100001900`). |
| `0xCA1` | `0x749` | `+0x558` | 1 | `uint8` | `particle_sort_mode_index` | Passed by `EmitterCalculator::EntrySortedParticle` to `System::GetSortedParticleList`. `0` takes the no-sort/default path, `1` and `3` sort the per-particle scalar stored at vector offset `+0xC` in opposite orders, `2` computes a camera-space depth key, and `4` invokes the custom sort callback. Friendly Nintendo enum names remain unproven. |
| `0xCA2` | `0x752` | `+0x550` | 1 | `uint8` | `emitter_calc_type` | Mode: 0 = CPU, 2 = GPU compute; copied into runtime flag bits 14â€“16 (`0x7100001900`). |
| `0xCA3` | `0x74B` | `+0x558` | 1 | `uint8` | `velocity_coord` | Velocity coordinate space: 0 = Local, 1 = World (`CalculateParticle` `0x7100010968`). |
| `0xCA4` | `0x757` | `+0x54D` | 1 | `uint8` | `seed_source` | Random seed source: 0 = Global, 1 = ESET, 2 = Fixed (`0x7100001900`). |
| `0xCA9` | `0x75B` | `+0x54E` | 1 | `uint8` | `fade_in_curve` | Fade-in alpha curve selector: 0=Off, 1=Lin, 2=Quad, 3=Quart (`0x7100004048`). |
| `0xCAA` | `0x75C` | `+0x54E` | 1 | `uint8` | `fade_in_scale` | Fade-in scale enable flag (`Emitter::GetScaleRate` `0x7100004158`). |
| `0xCAB` | `0x755` | `+0x556` | 1 | `uint8` | `fade_out_curve` | Fade-out alpha curve selector: 0=Off, 1=Lin, 2=Quad, 3=Quart (`0x71000040A8`). |
| `0xCAC` | `0x756` | `+0x556` | 1 | `uint8` | `fade_out_scale` | Fade-out scale enable flag (`Emitter::GetScaleRate` `0x7100004158`). |
| `0xCB0` | `0x760` | `+0x550` | 4 | `uint32` | `fixed_seed` | Fixed seed value when `seed_source == 2` (`0x7100001900`). |
| `0xCB8` | `0x768` | `+0x550` | 4 | `int32` | `fade_out_time` | Fade-out timing divisor in frames (`Emitter::Calculate` `0x710000FEBC`). |
| `0xCBC` | `0x76C` | `+0x550` | 4 | `int32` | `fade_in_time` | Fade-in timing divisor in frames (`Emitter::Calculate` `0x710000FEBC`). |
| `0xCC0` | `0x770` | `+0x550` | 12 | `float[3]` | `emitter_trans_xyz` | Emitter base translation coordinates XYZ (`Emitter::ResourceUpdate` `0x7100002000`). |
| `0xCCC` | `0x77C` | `+0x550` | 12 | `float[3]` | `emitter_trans_rnd` | Emitter translation random range XYZ (`0x7100002000`). |
| `0xCD8` | `0x788` | `+0x550` | 12 | `float[3]` | `emitter_rot_xyz` | Emitter base Euler rotation XYZ in radians (`CreateResMatrix:L50-58`). |
| `0xCE4` | `0x794` | `+0x550` | 12 | `float[3]` | `emitter_rot_rnd` | Emitter rotation random range XYZ in radians (`CreateResMatrix:L52-67`). |
| `0xCF0` | `0x798` | `+0x558` | 12 | `float[3]` | `rot_velocity_xyz` | Per-particle angular velocity in rad/sec (`UpdateParams:L1563`). |
| `0xCFC` | `0x7A4` | `+0x558` | 12 | `float[3]` | `rot_vel_random_xyz` | Angular velocity random variance range. |
| `0xD28` | â€” | TotK only | 4 | `float` | `scale_fade_in_init`| Scale fade-in starting value used by `GetScaleRate` (`0x7100004158`). |
| `0xD2C` | â€” | TotK only | 4 | `float` | `scale_fade_out_init`| Scale fade-out starting value used by `GetScaleRate` (`0x7100004158`). |
| `0xD39` | `0x7E1` | `+0x558` | 1 | `uint8` | `unverified_D39` | The previous billboard-mode enum was not supported by a matching reader in the traced TotK draw, shader, emitter, or resource functions. |
| `0xD3C` | â€” | TotK only | 1 | `uint8` | `child_alloc_flag` | Copied into runtime flag bit 17; tested when creating child emitters (`0x7100001900`). |
| `0xD48` | `0x7F0` | `+0x558` | 1 | `uint8` | `emit_loop_mode` | Loop mode: 0 = Infinite / Loop, 1 = One-Shot (`UpdateParams:L1604`). |
| `0xD49` | `0x7F1` | `+0x558` | 1 | `uint8` | `gravity_coord` | Gravity vector coordinate space: 0 = World, 1 = Local (`CalculateParticleBehavior:L95`). |
| `0xD4A` | `0x7F2` | `+0x558` | 1 | `uint8` | `emit_dist_enable` | `emission.isEmitDistEnabled`. `Emitter::Initialize` sets runtime flag bit 21 from it; `CalculateRequiredParticleAsignmentCount` then uses `0xD8C` as the particle count (SDK `vfx_Emitter.cpp:223-228`). |
| `0xD4C` | `0x7F4` | `+0x558` | 4 | `uint32` | `emit_start_delay` | Delay before emission starts in frames. |
| `0xD50` | `0x7F8` | `+0x558` | 4 | `uint32` | `child_emit_timing` | Converted with `ucvtf` and used as a percentage (`/100`) of the parent particle lifetime (`CalculateParticle`). |
| `0xD54` | `0x7FC` | `+0x558` | 4 | `uint32` | `emit_duration` | Active emission duration in frames. `CalculateRequiredParticleAsignmentCount` uses it as `duration/(interval+1)`; `UpdateParams` clamps `0xD60` to it for one-shot, non-distance emitters; `CalculateParticle` adds it to the start time (SDK `emission.emitDuration`). |
| `0xD58` | `0x800` | `+0x558` | 4 | `float` | `emit_rate` | Particles per frame (`TryEmitParticle` reads it as float). |
| `0xD5C` | `0x804` | `+0x558` | 1 | `uint8` | `emit_rate_random_percent` | `TryEmitParticle` reads exactly one byte and applies it as a percentage variation to the emission rate. `0xD5D..0xD5F` remain padding/unverified; no wider TotK read has been established. |
| `0xD60` | `0x808` | `+0x558` | 4 | `int32` | `emit_interval` | `UpdateByEmit` computes `(D60 + 1) + random(D64)`, identical to SDK `vfx_Emitter.cpp:761` (`interval + 1.0f + GetInteger(intervalRandom)`). |
| `0xD64` | `0x80C` | `+0x558` | 4 | `int32` | `emit_interval_random` | Upper bound of the random range in `UpdateByEmit` (`emission.intervalRandom`). |
| `0xD70` | `0x818` | `+0x558` | 12 | `float[3]` | `gravity_xyz` | Constant acceleration / gravity vector XYZ (`CalculateParticleBehavior:L92-94`). |
| `0xD7C` | - | TotK only | 4 | `float` | `emit_dist_unit` | Distance-emission step: `TryEmitParticle` divides the clamped length by it (SDK `emitDistUnit`, `vfx_EmitterCalc.cpp:202-216`). |
| `0xD80` | - | TotK only | 4 | `float` | `emit_dist_min` | Lower clamp of the (scaled) travelled length (SDK `emitDistMin`). |
| `0xD84` | - | TotK only | 4 | `float` | `emit_dist_max` | Upper clamp of the (scaled) travelled length (SDK `emitDistMax`). |
| `0xD88` | - | TotK only | 4 | `float` | `emit_dist_margin` | Lengths below `margin * scale` are zeroed (SDK `emitDistMargin`). |
| `0xD8C` | - | TotK only | 4 | `int32` | `emit_dist_particle_max` | Particle count used when `emit_dist_enable` is set (`0x71000035F4`; SDK `emitDistParticleMax`). |
| `0xD90` | `0x838` | `+0x558` | 1 | `uint8` | `shape_type` | Direct index into `g_EmitFunctions`: 0=Point, 1=Circle, 2=CircleEquallyDivided, 3=CircleFill, 4=Sphere, 5=SphereEqually32Divided, 6=SphereEqually64Divided, 7=SphereFill, 8=Cylinder, 9=CylinderFill, 10=Box, 11=BoxFill, 12=Line, 13=LineEquallyDivided, 14=Rectangle, 15=Primitive (`Emitter::InitializeParticle` `0x7100012188`; table at `0x71041AA110`). |
| `0xD91` | `0x839` | `+0x558` | 1 | `uint8` | `shape_angle_mode` | Shape angle calculation mode: 0=Static, 1=Time-varying (`CalculateEmitCircle:L24`). |
| `0xD92` | `0x83A` | `+0x558` | 1 | `uint8` | `shape_rot_mode` | Shape orientation mode (`CalculateEmitSphere:L102`). |
| `0xD95` | `0x83E` | `+0x557` | 1 | `uint8` | `shape_rot_variant` | **1-byte packing delta**. Basis frame variant (`CalculateEmitSphereFill:L183`). |
| `0xD98` | `0x840` | `+0x558` | 4 | `float` | `shape_angle_b` | Emission arc spread in radians (`CalculateEmitCircle:L20`). |
| `0xD9C` | `0x844` | `+0x558` | 4 | `float` | `shape_angle_c` | Elevation / latitude cone angle in radians (`CalculateEmitSphere:L101`). |
| `0xDA0` | `0x848` | `+0x558` | 4 | `float` | `shape_angle_d` | Initial phase angle offset in radians (`CalculateEmitCircle:L21`). |
| `0xDA8` | `0x850` | `+0x558` | 4 | `float` | `shape_fill_ratio` | Interior-fill amount used by circle, sphere, and box fill emitters. The generated radial factor proves `0.0` produces the outer shell/perimeter and `1.0` permits the full interior volume (`CalculateEmitCircleFill` `0x7100013B1C`; `CalculateEmitSphereFill` `0x710001486C`; `CalculateEmitBoxFill` `0x71000150CC`). |
| `0xDAC` | `0x854` | `+0x558` | 4 | `float` | `line_center_bias` | Appears in the line-position expression `length * (random - (1 + value) / 2)`, shifting the sampled line interval without defining its length (`CalculateEmitLine` `0x7100015300`; `CalculateEmitLineEquallyDivided` `0x7100015388`). |
| `0xDB0` | `0x858` | `+0x558` | 4 | `float` | `line_length` | Multiplied by the Z component of the emitter scale and used as the line segment length (`CalculateEmitLine` `0x7100015300`; `CalculateEmitLineEquallyDivided` `0x7100015388`). |
| `0xDB4` | `0x85C` | `+0x558` | 12 | `float[3]` | `shape_radius_xyz` | Shape semi-axis radii along X, Y, Z (`CalculateEmitCircle:L49`). |
| `0xDCC` | â€” | TotK only | 4 | `int32` | `primitive_dist_mode`| 0 = Divided checks, 1/2 = Primitive indexing paths (`CalculateEmitPrimitive` `0x71000155FC`). |
| `0xDD0` | `0x878` | `+0x558` | 8 | `uint64` | `mesh_primitive_idx`| G3D primitive index used by `Resource::InitializeEmitterGraphicsResource` (`0x710001E650`). |
| `0xDD8` | `0x880` | `+0x558` | 4 | `int32` | `shape_divisions` | Slice division count for equally divided circle shapes. |
| `0xDE8` | `0x898` | `+0x550` | 1 | `uint8` | `blend_target_enable` | Passed as the blend target's enable bit by `Rendercontext::Initialize`. This trace does not establish a general color-write-mask meaning. |
| `0xDE9` | `0x899` | `+0x550` | 1 | `uint8` | `unverified_DE9` | No reader found in the traced TotK render-state path. |
| `0xDEA` | `0x89A` | `+0x550` | 1 | `uint8` | `depth_stencil_mode_index` | Required to be below 8 and written directly into the first byte of `DepthStencilStateInfo`. The exact Nintendo-facing enum name is not established (`Rendercontext::Initialize` `0x710001C148`). |
| `0xDEB` | `0x89B` | `+0x550` | 1 | `uint8` | `depth_sort_ascending` | Used only by the `0xCA1 == 2` camera-depth sorting path in `System::GetSortedParticleList`. Zero selects descending float-key order; nonzero selects ascending float-key order. It is not consumed by `Rendercontext::Initialize`. |
| `0xDEC` | `0x89C` | `+0x550` | 1 | `uint8` | `unverified_DEC` | No reader found in the traced TotK render-state path. |
| `0xDED` | `0x89D` | `+0x550` | 1 | `uint8` | `unverified_DED` | No reader found in the traced TotK render-state path. |
| `0xDEE` | `0x89E` | `+0x550` | 1 | `uint8` | `blend_mode_index` | Values below 6 select one of six packed blend-state configurations. Friendly mode names are not assigned until those configurations are decoded (`Rendercontext::Initialize` `0x710001C148`). |
| `0xDEF` | `0x89F` | `+0x550` | 1 | `uint8` | `cull_mode_index` | Input values map to rasterizer values as 0→0, 1→2, and 2→1 (`Rendercontext::Initialize` `0x710001C148`). Friendly front/back labels are not yet proven. |
| `0xDF0` | `0x8A0` | `+0x550` | 4 | `float` | `unverified_DF0` | No reader found in the traced TotK render-state path. |
| `0xDF8` | `0x8A8` | `+0x550` | 1 | `uint8` | `emit_infinite_flag`| Infinite emitter lifetime flag: 1 = Infinite (`UpdateParams:L1608`). |
| `0xDF9` | `0x8A9` | `+0x550` | 1 | `uint8` | `is_trimming_prim` | Trim primitive enable flag (`Resource::InitializeEmitterGraphicsResource` `0x710001E650`). |
| `0xDFC` | `0x8AC` | `+0x550` | 1 | `uint8` | `shader_mode_index_DFC` | `ShaderFlag::Initialize` maps value `0` to shader-flag word 1 bit `0x80000` and value `1` to bit `0x100000`. That use alone does not prove the previous `sort_mode` label. |
| `0xDFD` | â€” | TotK only | 1 | `uint8` | `rotation_random_sign_x_enable` | Enables a per-particle random sign inversion in the X rotation-output path; also sets shader-flag word 0 bit 28. |
| `0xDFE` | â€” | TotK only | 1 | `uint8` | `rotation_random_sign_y_enable` | Y rotation-output equivalent; also sets shader-flag word 0 bit 29. |
| `0xDFF` | â€” | TotK only | 1 | `uint8` | `rotation_random_sign_z_enable` | Z rotation-output equivalent; also sets shader-flag word 0 bit 30. |
| `0xE00` | â€” | TotK only | 1 | `uint8` | `rotation_param_lane0_enable`| When zero, `UpdateParams` resets lane-0 values across `0xC10/20/30/40/60/70/80/90`, including period `1.0`; nonzero preserves the serialized lane. |
| `0xE01` | â€” | TotK only | 1 | `uint8` | `rotation_param_lane1_enable`| Lane-1 equivalent across offsets ending in `...14/24/34/44/64/74/84/94`. |
| `0xE02` | â€” | TotK only | 1 | `uint8` | `rotation_param_lane2_enable`| Lane-2 equivalent across offsets ending in `...18/28/38/48/68/78/88/98`. |
| `0xE03` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag3` | Shader flag bit 18 in `param_1[1]` (`ShaderFlag::Initialize:L338`). |
| `0xE04` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag4` | Shader flag bit 0 in `param_1[2]` (`ShaderFlag::Initialize:L348-355`). |
| `0xE08` | `0x8B8` | `+0x550` | 4 | `uint32` | `particle_lifespan` | Base particle lifetime in frames. `EmitterResource::UpdateParams` copies it into the runtime lifetime slot when no lifetime animation is present; `Emitter::ResourceUpdate` converts that slot to float; `Emitter::InitializeParticle` stores the resulting value in the particle record; and `Emitter::CalculateParticle` kills the particle when `current_time - birth_time >= lifetime`. |
| `0xE0C` | `0x8BC` | `+0x550` | 1 | `uint8` | `particle_lifespan_random_percent`| Unsigned downward lifetime variation. `InitializeParticle` computes `base_lifetime * (1 - floor(random_u32 * value / 2^32) * 0.01)` and stores the result as the particle's lifetime. |
| `0xE10` | `0x8C0` | `+0x550` | 4 | `float` | `particle_attribute_w_random_amplitude` | Not a fade duration. `InitializeParticle` writes `1 + value - 2 * value * U`, for `U` derived from a uniform 32-bit RNG, to the W component of the per-particle vector whose XYZ components hold initial scale. This proves a symmetric per-particle scalar range `[1-value, 1+value]`; its higher-level shader meaning is not yet established. |
| `0xE14` | `0x8C4` | `+0x550` | 4 | `bytes[4]` | `unverified_E14` | No reader has been found in the traced particle initialization, update, lifetime, fade, resource-setup, or graphics-resource paths. The previous `particle_fade_out` label had no executable support. |
| `0xE18` | `0x8C8` | `+0x550` | 8 | `uint64` | `g3d_primitive_idx` | Primary G3D primitive index used by `EmitterResource::Setup` (`0x710000B694`). |
| `0xE20` | `0x8D0` | `+0x550` | 8 | `uint64` | `trim_primitive_idx`| Optional trim primitive index, read when `0xDF9` is set (`0x710001E650`). |
| `0xE28` | `0x8D8` | `+0x550` | 1 | `uint8` | `loop_color0_enable`| Enables period wrapping for the Color0 key animation. |
| `0xE29` | `0x8D9` | `+0x550` | 1 | `uint8` | `loop_alpha0_enable`| Enables period wrapping for the Alpha0 key animation. |
| `0xE2A` | `0x8DA` | `+0x550` | 1 | `uint8` | `loop_color1_enable`| Enables period wrapping for the Color1 key animation. |
| `0xE2B` | `0x8DB` | `+0x550` | 1 | `uint8` | `loop_alpha1_enable`| Enables period wrapping for the Alpha1 key animation. |
| `0xE2C` | `0x8DC` | `+0x550` | 1 | `uint8` | `loop_scale_enable`| Enables period wrapping for the Scale key animation. |
| `0xE2D` | `0x8DD` | `+0x550` | 1 | `uint8` | `loop_color0_random_phase` | Multiplies the per-particle phase term used before Color0 period wrapping. |
| `0xE2E` | `0x8DE` | `+0x550` | 1 | `uint8` | `loop_alpha0_random_phase` | Alpha0 equivalent of `0xE2D`. |
| `0xE2F` | `0x8DF` | `+0x550` | 1 | `uint8` | `loop_color1_random_phase` | Color1 equivalent of `0xE2D`. |
| `0xE30` | `0x8E0` | `+0x550` | 1 | `uint8` | `loop_alpha1_random_phase` | Alpha1 equivalent of `0xE2D`. |
| `0xE31` | `0x8E1` | `+0x550` | 1 | `uint8` | `loop_scale_random_phase` | Scale equivalent of `0xE2D`. |
| `0xE34` | â€” | TotK only | 2 | `uint16` | `loop_color0_period_u16`| Color0 loop period in frames. |
| `0xE36` | â€” | TotK only | 2 | `uint16` | `loop_alpha0_period_u16`| Alpha0 loop period in frames. |
| `0xE38` | â€” | TotK only | 2 | `uint16` | `loop_color1_period_u16`| Color1 loop period in frames. |
| `0xE3A` | â€” | TotK only | 2 | `uint16` | `loop_alpha1_period_u16`| Alpha1 loop period in frames. |
| `0xE3C` | `0x8F4` | `+0x548` | 4 | `int32` | `loop_scale_period_i32`| Scale loop period in frames. |
| `0xE40` | â€” | TotK only | 1 | `uint8` | `color0_key_interpolation_mode` | Passed directly to `Calculate8KeyAnim` for Color0. Friendly interpolation enum values remain unproven. |
| `0xE41` | â€” | TotK only | 1 | `uint8` | `alpha0_key_interpolation_mode` | Passed directly to `Calculate8KeyAnim` for Alpha0. |
| `0xE42` | â€” | TotK only | 1 | `uint8` | `color1_key_interpolation_mode` | Passed directly to `Calculate8KeyAnim` for Color1. |
| `0xE43` | â€” | TotK only | 1 | `uint8` | `alpha1_key_interpolation_mode` | Passed directly to `Calculate8KeyAnim` for Alpha1. |
| `0xE44` | â€” | TotK only | 1 | `uint8` | `scale_key_interpolation_mode` | Passed directly to `Calculate8KeyAnim` for Scale. |
| `0xE46` | â€” | TotK only | 1 | `uint8` | `rotation_wave_x_enable` | Nonzero applies the X-axis rotation waveform in `CalculateRotationMatrix`. |
| `0xE47` | â€” | TotK only | 1 | `uint8` | `rotation_wave_y_enable` | Nonzero applies the Y-axis rotation waveform. |
| `0xE48` | â€” | TotK only | 1 | `uint8` | `rotation_wave_z_enable` | Nonzero applies the Z-axis rotation waveform. |
| `0xE49` | â€” | TotK only | 1 | `uint8` | `rotation_wave_x_mode_packed` | High nibble selects X-axis rotation waveform arithmetic: `0` smooth periodic; `2` and `4` use the half-period signed pulse branch. Shader flags distinguish `0/2/4` with word-2 bits `0x1000/0x2000/0x4000`. Low-nibble meaning is unproven. |
| `0xE4A` | â€” | TotK only | 1 | `uint8` | `rotation_wave_y_mode_packed` | Y-axis equivalent, mapped to word-2 bits `0x8000/0x10000/0x20000`. |
| `0xE4B` | â€” | TotK only | 1 | `uint8` | `rotation_wave_z_mode_packed` | Z-axis equivalent, mapped to word-2 bits `0x40000/0x80000/0x100000`. |
| `0xE51` | â€” | TotK only | 1 | `uint8` | `graphics_shader0_internal` | Shader-source selector for the normal graphics pass. Zero asks the caller-provided resolver for index `E5C`; nonzero resolves the index through the internal graphics `ShaderManager` (`UpdateShaderResource`). |
| `0xE52` | â€” | TotK only | 1 | `uint8` | `graphics_shader1_internal` | Same source selection for graphics pass 1 / index `E60`. |
| `0xE53` | â€” | TotK only | 1 | `uint8` | `graphics_shader2_internal` | Same source selection for graphics pass 2 / index `E64`. |
| `0xE54` | â€” | TotK only | 1 | `uint8` | `compute_shader0_internal` | Compute-shader source selector for the normal pass. Zero asks the caller-provided compute resolver for index `E68`; nonzero uses the internal `ComputeShaderManager`. |
| `0xE55` | â€” | TotK only | 1 | `uint8` | `compute_shader1_internal` | Same source selection for compute pass 1 / index `E6C`. |
| `0xE56` | â€” | TotK only | 1 | `uint8` | `compute_shader2_internal` | Same source selection for compute pass 2 / index `E70`. |
| `0xE5C` | `0x914` | `+0x548` | 4 | `int32` | `shader_idx_normal` | Normal shader index in shader archive (`UpdateShaderResource:L25`). |
| `0xE60` | `0x91C` | `+0x548` | 4 | `int32` | `shader_idx_pass1` | Pass 1 shader index in shader archive (`UpdateShaderResource:L52`). |
| `0xE64` | `0x924` | `+0x548` | 4 | `int32` | `shader_idx_pass2` | Pass 2 shader index in shader archive (`UpdateShaderResource:L88`). |
| `0xE68` | `0x918` | â€” | 4 | `int32` | `compute_shader0` | Normal compute shader index (`UpdateShaderResource:L122`). |
| `0xE6C` | â€” | TotK only | 4 | `int32` | `compute_shader1` | Pass 1 compute shader index (`UpdateShaderResource:L145`). |
| `0xE70` | â€” | TotK only | 4 | `int32` | `compute_shader2` | Pass 2 compute shader index (`UpdateShaderResource:L172`). |
| `0xE78` | - | UNVERIFIED | 1 | `uint8` | `unverified_E78` | Five-flag group; no TotK reader found. Purpose unknown. |
| `0xE79` | - | UNVERIFIED | 1 | `uint8` | `unverified_E79` | Same group, purpose unknown. |
| `0xE7A` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7A` | Same group, purpose unknown. |
| `0xE7B` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7B` | Same group, purpose unknown. |
| `0xE7C` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7C` | Same group, purpose unknown. |
| `0xE7D` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7D` | Second five-flag group, purpose unknown. |
| `0xE7E` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7E` | Same group, purpose unknown. |
| `0xE7F` | - | UNVERIFIED | 1 | `uint8` | `unverified_E7F` | Same group, purpose unknown. |
| `0xE80` | - | UNVERIFIED | 1 | `uint8` | `unverified_E80` | Same group, purpose unknown. |
| `0xE81` | - | UNVERIFIED | 1 | `uint8` | `unverified_E81` | Same group, purpose unknown. |
| `0xE84` | - | UNVERIFIED | 4 | `int32` | `unverified_E84` | Five-slot int32 run (0xE84..0xE94); purpose unknown. |
| `0xE88` | - | UNVERIFIED | 4 | `int32` | `unverified_E88` | Same run, purpose unknown. |
| `0xE8C` | - | UNVERIFIED | 4 | `int32` | `unverified_E8C` | Same run, purpose unknown. |
| `0xE90` | - | UNVERIFIED | 4 | `int32` | `unverified_E90` | Same run, purpose unknown. |
| `0xE94` | - | UNVERIFIED | 4 | `int32` | `unverified_E94` | Same run, purpose unknown. |
| `0xF10` | `0x970` | `+0x5A0` | 4 | `float` | `emission_direction_spread_degrees` | Angular spread applied around the shape-produced direction. `FUN_71000127F0` converts the value to the lower bound `1 - value/90`, randomly samples between that bound and `1`, derives the complementary radial component, rotates the resulting local direction into the source-direction frame, and then applies the initial speed. A zero value takes the direct no-spread branch. |
| `0xF14` | `0x974` | `+0x5A0` | 4 | `float` | `emission_tangent_amount` | Linear tangential contribution to the shape-produced emission direction, not an angle. `FUN_71000127F0` derives a normalized XZ tangent from the emitted position (or a random normalized XZ direction when the position is at the origin) and performs `direction += tangent * value`. |
| `0xF18` | `0x978` | `+0x5A0` | 4 | `float` | `emission_direction_random_x` | Multiplies the X component of a sampled random vector and adds it to the initialized particle direction (`direction.x += random.x * value`). |
| `0xF1C` | `0x97C` | `+0x5A0` | 4 | `float` | `emission_direction_random_y` | Multiplies the Y component of the same sampled random vector and adds it to the initialized particle direction. |
| `0xF20` | `0x980` | `+0x5A0` | 4 | `float` | `emission_direction_random_z` | Multiplies the Z component of the same sampled random vector and adds it to the initialized particle direction. |
| `0xF24` | `0x984` | `+0x5A0` | 4 | `float` | `initial_speed_random_percent` | One-sided downward randomization of initial speed. `FUN_71000127F0` multiplies the speed by `1 - U * (value / 100)`, where `U` is uniform in `[0,1)`. |
| `0xF28` | `0x988` | `+0x5A0` | 4 | `float` | `emitter_motion_inherit_scale` | Scales the emitter displacement vector divided by elapsed emitter time before adding it to particle direction/velocity. The contribution is computed only when the elapsed-time value is positive. |
| `0xF2C` | `0x98C` | `+0x5A0` | 4 | `float` | `emitter_motion_inherit_max` | Maximum magnitude of the motion-inheritance vector produced with `0xF28`. `FUN_71000127F0` normalizes and rescales that vector when its length exceeds this value, then adds it to the particle direction/velocity. |
| `0xF44` | `0x9A4` | `+0x5A0` | 1 | `uint8` | `color0_mode` | Color0 mode: `0` copies the constant into key slot 0, `2` evaluates the timed key array through `Calculate8KeyAnim`, and `3` selects a discrete key with `floor(normalized_life * key_count)`. The previous `3=Random` label was incorrect. |
| `0xF45` | `0x9A5` | `+0x5A0` | 1 | `uint8` | `color1_mode` | Color1 mode with the same `0` constant / `2` interpolated-key / `3` discrete-key behavior. |
| `0xF46` | `0x9A6` | `+0x5A0` | 1 | `uint8` | `alpha0_mode` | Alpha0 mode with the same `0` constant / `2` interpolated-key behavior; no separate mode-3 branch is present in the traced Alpha0 evaluator. |
| `0xF47` | `0x9A7` | `+0x5A0` | 1 | `uint8` | `alpha1_mode` | Alpha1 mode with the same `0` constant / `2` interpolated-key behavior; no separate mode-3 branch is present in the traced Alpha1 evaluator. |
| `0xF48` | `0x9A8` | `+0x5A0` | 12 | `float[3]` | `color0_const_rgb` | Color0 constant RGB values (`UpdateParams:L97`). |
| `0xF54` | `0x9B4` | `+0x5A0` | 4 | `float` | `alpha0_const` | Alpha0 constant Alpha value (`UpdateParams:L105`). |
| `0xF58` | `0x9B8` | `+0x5A0` | 12 | `float[3]` | `color1_const_rgb` | Color1 constant RGB values (`UpdateParams:L114`). |
| `0xF64` | `0x9C4` | `+0x5A0` | 4 | `float` | `alpha1_const` | Alpha1 constant Alpha value (`UpdateParams:L122`). |
| `0xF68` | `0x9C8` | `+0x5A0` | 12 | `float[3]` | `particle_scale_xyz`| Initial particle base scale XYZ (`InitializeParticle:L59-64`). |
| `0xF74` | `0x9D4` | `+0x5A0` | 12 | `float[3]` | `particle_scale_rnd`| Initial particle scale random range XYZ (`InitializeParticle:L51-86`). |
| `0xF8C` | â€” | TotK only | 1 | `uint8` | `waveform_alpha_enable` | Nonzero multiplies Alpha0 and Alpha1 by waveform 0 in the color evaluators. |
| `0xF8D` | â€” | TotK only | 1 | `uint8` | `waveform_scale_x_enable` | Nonzero applies waveform 0 to Scale X. |
| `0xF8E` | â€” | TotK only | 1 | `uint8` | `waveform_scale_y_enable` | Nonzero additionally applies waveform 1 to Scale Y. |
| `0xF8F` | `0x9EF` | `+0x5A0` | 1 | `uint8` | `waveform_mode_packed` | The high nibble selects waveform arithmetic: `0` smooth periodic curve, `1` fractional ramp/saw, `2` half-period pulse/square. `ShaderFlag::Initialize` maps those values to shader-word-0 bits `0x1`, `0x2`, and `0x4`. The low nibble's independent meaning is not established. |
| `0xF98` | `0x9F8` | Indexed | 8 | `uint64` | `tex_slot0_guid` | Neutral texture slot 0 GUID. Stride `0x18`; no material-role name is assigned by the executable trace. (`UpdateParams:L65`). |
| `0xFB0` | `0xA18` | Indexed | 8 | `uint64` | `tex_slot1_guid` | Neutral texture slot 1 GUID; no material-role name is assigned by the executable trace. (`UpdateParams:L70`). |
| `0xFC8` | `0xA38` | Indexed | 8 | `uint64` | `tex_slot2_guid` | Neutral texture slot 2 GUID; no material-role name is assigned by the executable trace. (`UpdateParams:L75`). |
| `0xFE0` | â€” | TotK only | 8 | `uint64` | `tex_slot3_guid` | Texture Slot 3 GUID (`TextureSlotId_3`). (`UpdateParams:L80`). |
| `0xFF8` | â€” | TotK only | 8 | `uint64` | `tex_slot4_guid` | Texture Slot 4 GUID (`TextureSlotId_4`). (`UpdateParams:L85`). |
| `0x1010`| â€” | TotK only | 8 | `uint64` | `tex_slot5_guid` | Texture Slot 5 GUID (`TextureSlotId_5`). (`UpdateParams:L90`). |
| `0x1028`| `0xA58` | Indexed | 1 | `uint8` | `tex0_mode_index`| Slot 0 texture/shader mode. Values `1`, `2`, and `3` set distinct shader-word-0 bits; `4` initializes the slot-0 flipbook runtime block and sets bit `0x80`; `6` sets shader-word-1 bit `0x200000`. Friendly enum names are not proven. |
| `0x1029`| `0xA59` | Indexed | 1 | `uint8` | `tex0_uv_scroll` | Slot 0 UV translation scroll enable (`UpdateParams:L197`). |
| `0x102A`| `0xA5A` | Indexed | 1 | `uint8` | `tex0_uv_rotate` | Slot 0 UV rotation animation enable (`UpdateParams:L270`). |
| `0x102B`| `0xA5B` | Indexed | 1 | `uint8` | `tex0_uv_scale` | Slot 0 UV scale animation enable (`UpdateParams:L330`). |
| `0x102C`| `0xA5C` | Indexed | 1 | `uint8` | `tex0_uv_domain_scale_mode` | Slot 0 selector mapped to two UV uniform floats: `0=(1,1)`, `1=(1,2)`, `2=(2,1)`, `3=(2,2)`. The binary does not establish Clamp/Repeat/Mirror sampler names (`UpdateParams`). |
| `0x102D`| `0xA5D` | Indexed | 1 | `uint8` | `tex0_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x1`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x102E`| `0xA5E` | Indexed | 1 | `uint8` | `tex0_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x2`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x102F`| `0xA5F` | Indexed | 1 | `uint8` | `tex0_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x1000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x1038`| `0xA68` | Indexed | 1 | `uint8` | `tex1_mode_index`| Slot 1 equivalent of `0x1028`; value `4` initializes the slot-1 flipbook runtime block. |
| `0x1039`| `0xA69` | Indexed | 1 | `uint8` | `tex1_uv_scroll` | Slot 1 UV translation scroll enable (`UpdateParams:L208`). |
| `0x103A`| `0xA6A` | Indexed | 1 | `uint8` | `tex1_uv_rotate` | Slot 1 UV rotation animation enable (`UpdateParams:L280`). |
| `0x103B`| `0xA6B` | Indexed | 1 | `uint8` | `tex1_uv_scale` | Slot 1 UV scale animation enable (`UpdateParams:L343`). |
| `0x103C`| `0xA6C` | Indexed | 1 | `uint8` | `tex1_uv_domain_scale_mode` | Slot 1 selector with the same four `(U,V)` factor mappings as `0x102C`. |
| `0x103D`| `0xA6D` | Indexed | 1 | `uint8` | `tex1_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x4`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x103E`| `0xA6E` | Indexed | 1 | `uint8` | `tex1_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x8`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x103F`| `0xA6F` | Indexed | 1 | `uint8` | `tex1_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x2000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x1048`| `0xA78` | Indexed | 1 | `uint8` | `tex2_mode_index`| Slot 2 equivalent of `0x1028`; value `4` initializes the slot-2 flipbook runtime block. |
| `0x1049`| `0xA79` | Indexed | 1 | `uint8` | `tex2_uv_scroll` | Slot 2 UV translation scroll enable (`UpdateParams:L218`). |
| `0x104A`| `0xA7A` | Indexed | 1 | `uint8` | `tex2_uv_rotate` | Slot 2 UV rotation animation enable (`UpdateParams:L290`). |
| `0x104B`| `0xA7B` | Indexed | 1 | `uint8` | `tex2_uv_scale` | Slot 2 UV scale animation enable (`UpdateParams:L356`). |
| `0x104C`| `0xA7C` | Indexed | 1 | `uint8` | `tex2_uv_domain_scale_mode` | Slot 2 selector with the same four `(U,V)` factor mappings as `0x102C`. |
| `0x104D`| `0xA7D` | Indexed | 1 | `uint8` | `tex2_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x10`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x104E`| `0xA7E` | Indexed | 1 | `uint8` | `tex2_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x20`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x104F`| `0xA7F` | Indexed | 1 | `uint8` | `tex2_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x4000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x1058`| â€” | TotK only | 1 | `uint8` | `tex3_mode_index`| Slot 3 equivalent of `0x1028`; value `4` initializes the slot-3 flipbook runtime block. |
| `0x1059`| â€” | TotK only | 1 | `uint8` | `tex3_uv_scroll` | Slot 3 UV translation scroll enable (`UpdateParams:L231`). |
| `0x105A`| â€” | TotK only | 1 | `uint8` | `tex3_uv_rotate` | Slot 3 UV rotation animation enable (`UpdateParams:L300`). |
| `0x105B`| â€” | TotK only | 1 | `uint8` | `tex3_uv_scale` | Slot 3 UV scale animation enable (`UpdateParams:L369`). |
| `0x105C`| â€” | TotK only | 1 | `uint8` | `tex3_uv_domain_scale_mode` | Slot 3 selector with the same four `(U,V)` factor mappings as `0x102C`. |
| `0x105D`| â€” | TotK only | 1 | `uint8` | `tex3_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x40`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x105E`| â€” | TotK only | 1 | `uint8` | `tex3_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x80`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x105F`| â€” | TotK only | 1 | `uint8` | `tex3_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x8000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x1068`| â€” | TotK only | 1 | `uint8` | `tex4_mode_index`| Slot 4 equivalent of `0x1028`; value `4` initializes the slot-4 flipbook runtime block. |
| `0x1069`| â€” | TotK only | 1 | `uint8` | `tex4_uv_scroll` | Slot 4 UV translation scroll enable (`UpdateParams:L245`). |
| `0x106A`| â€” | TotK only | 1 | `uint8` | `tex4_uv_rotate` | Slot 4 UV rotation animation enable (`UpdateParams:L310`). |
| `0x106B`| â€” | TotK only | 1 | `uint8` | `tex4_uv_scale` | Slot 4 UV scale animation enable (`UpdateParams:L382`). |
| `0x106C`| â€” | TotK only | 1 | `uint8` | `tex4_uv_domain_scale_mode` | Slot 4 selector with the same four `(U,V)` factor mappings as `0x102C`. |
| `0x106D`| â€” | TotK only | 1 | `uint8` | `tex4_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x100`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x106E`| â€” | TotK only | 1 | `uint8` | `tex4_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x200`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x106F`| â€” | TotK only | 1 | `uint8` | `tex4_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x10000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x1078`| â€” | TotK only | 1 | `uint8` | `tex5_mode_index`| Slot 5 equivalent of `0x1028`; value `4` initializes the slot-5 flipbook runtime block. |
| `0x1079`| â€” | TotK only | 1 | `uint8` | `tex5_uv_scroll` | Slot 5 UV translation scroll enable (`UpdateParams:L257`). |
| `0x107A`| â€” | TotK only | 1 | `uint8` | `tex5_uv_rotate` | Slot 5 UV rotation animation enable (`UpdateParams:L320`). |
| `0x107B`| â€” | TotK only | 1 | `uint8` | `tex5_uv_scale` | Slot 5 UV scale animation enable (`UpdateParams:L395`). |
| `0x107C`| â€” | TotK only | 1 | `uint8` | `tex5_uv_domain_scale_mode` | Slot 5 selector with the same four `(U,V)` factor mappings as `0x102C`. |
| `0x107D`| â€” | TotK only | 1 | `uint8` | `tex5_shader_flag0` | Nonzero sets shader-flag word 1 bit `0x400`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x107E`| â€” | TotK only | 1 | `uint8` | `tex5_shader_flag1` | Nonzero sets shader-flag word 1 bit `0x800`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| `0x107F`| â€” | TotK only | 1 | `uint8` | `tex5_shader_flag2` | Nonzero sets shader-flag word 1 bit `0x20000`; higher-level meaning unproven (`ShaderFlag::Initialize` `0x710000E388`). |
| **`0x10C8`**| â€” | â€” | â€” | â€” | **Struct End** | End of fixed data struct (`0x10C8` bytes total). |
