# TotK `EMTR` Serialized-Field Evidence & Field Map

This document records the exact byte offsets, types, executable behaviors, and Ghidra decompilation citations for Tears of the Kingdom (`nn::vfx2`, VFXB v51).

Offsets are relative to the serialized `EMTR` node's data start (`EMTR.node + dataRelativeOffset`).

---

## 1. Resource-Tree & Allocation Architecture

### A. EmitterSet Node (`ESET`)
* **Fixed Data Size**: `0xB4` bytes.
* **Name**: `char[64]` at `ESET.data + 0x10` (null-terminated C-string).
* **Declared Emitter Count**: Unsigned 16-bit integer (`uint16`) at `ESET.data + 0x70` (read by `nn::vfx2::Resource::InitializeEmitterSetResource`).

### B. Emitter Node (`EMTR`)
* **Fixed Data Size**: `0x10C8` bytes (4,296 bytes).
* **Header / Body Split**: Fixed header occupies `0x00`â€“`0x70` (includes 16 bytes alignment padding at `0x60`â€“`0x6F`); the serialized `ResEmitter` body begins at `EMTR.data + 0x70`.
* **Track Count**: Supports 10 keyframe tracks (`0x080`â€“`0x0A8`), expanding beyond BotW's 5 tracks.
* **Texture Slots**: Supports 6 texture slots (`0xF98`â€“`0x1028`, stride `0x18`), expanding beyond BotW's 3 slots (`0x9F8`â€“`0xA58`, stride `0x20`).
* **Sampler Config**: Stride `0x10` starting at `0x1028` (6 slots total: `0x1028`â€“`0x1088`).

---

## 2. Byte-by-Byte Verified Field Map

| TotK Offset | BotW Offset | Delta | Size (B) | Type | Field Name | Executable Behavior & Verification Evidence |
|:---:|:---:|:---:|:---:|:---:|:---|:---|
| `0x000` | `0x000` | `+0x000` | 4 | `char[4]` | `magic` | FourCC node magic: `'EMTR'`. |
| `0x004` | `0x004` | `+0x000` | 4 | `uint32` | `node_size` | Serialized node data length: `0x10C8` (4,296 bytes). |
| `0x008` | `0x008` | `+0x000` | 4 | `uint32` | `version` | Binary format version (`0x00041400`). |
| `0x00C` | `0x00C` | `+0x000` | 4 | `uint32` | `flags` | Node behavior flags. |
| `0x010` | `0x010` | `+0x000` | 64 | `char[64]` | `emitter_name` | Emitter identifier null-terminated C-string. |
| `0x050` | `0x050` | `+0x000` | 4 | `uint32` | `runtime_link_id` | Internal runtime link identifier. |
| `0x054` | `0x054` | `+0x000` | 4 | `uint32` | `emitter_flags` | Emitter behavior bitflags. |
| `0x058` | `0x058` | `+0x000` | 4 | `uint32` | `random_seed` | Base random seed for emitter instance. |
| `0x05C` | `0x05C` | `+0x000` | 4 | `float` | `global_scale` | Master emitter global scale factor. |
| `0x060` | â€” | â€” | 16 | `bytes` | `header_pad` | 64-bit alignment header padding (`0x60`â€“`0x70`). |
| **`+0x070`**| **`+0x050`**| **`+0x020`**| â€” | â€” | **ResEmitter Body Start** | Stored into `EmitterResource + 0x18`. |
| `0x070` | `0x050` | `+0x020` | 4 | `uint32` | `eset_emitter_index` | Emitter ordinal index within parent ESET (`0x7100008eac`). |
| `0x074` | `0x054` | `+0x020` | 4 | `uint32` | `shader_flags_init` | Initial shader flag mask passed to `ShaderFlag::Initialize`. |
| `0x078` | `0x058` | `+0x020` | 4 | `uint32` | `custom_param_flags`| Custom attribute binding flags. |
| `0x07C` | `0x05c` | `+0x020` | 4 | `uint32` | `lod_flags` | LOD calculation flags. |
| `0x080` | `0x060` | `+0x020` | 4 | `uint32` | `color0_key_count` | Key count (0..8) for Color0 RGB animation track (`UpdateParams:L489`). |
| `0x084` | `0x064` | `+0x020` | 4 | `uint32` | `alpha0_key_count` | Key count (0..8) for Alpha0 animation track (`UpdateParams:L669`). |
| `0x088` | `0x068` | `+0x020` | 4 | `uint32` | `color1_key_count` | Key count (0..8) for Color1 RGB animation track (`UpdateParams:L579`). |
| `0x08C` | `0x06c` | `+0x020` | 4 | `uint32` | `alpha1_key_count` | Key count (0..8) for Alpha1 animation track (`UpdateParams:L759`). |
| `0x090` | `0x070` | `+0x020` | 4 | `uint32` | `scale_key_count` | Key count (0..8) for Scale XYZ animation track (`UpdateParams:L849`). |
| `0x094` | `0x074` | `+0x020` | 4 | `uint32` | `rot_key_count` | Key count (0..8) for Rotation track (`UpdateParams:L939`). |
| `0x098` | â€” | TotK only | 4 | `uint32` | `track5_key_count` | Key count (0..8) for Track 5 (`UpdateParams:L1029`). |
| `0x09C` | â€” | TotK only | 4 | `uint32` | `track6_key_count` | Key count (0..8) for Track 6 (`UpdateParams:L1119`). |
| `0x0A0` | â€” | TotK only | 4 | `uint32` | `track7_key_count` | Key count (0..8) for Track 7 (`UpdateParams:L1197`). |
| `0x0A4` | â€” | TotK only | 4 | `uint32` | `track8_key_count` | Key count (0..8) for Track 8 (`UpdateParams:L1275`). |
| `0x0A8` | `0x074` | â€” | 13 | `uint8[13]` | `vertex_attr_slots`| Shader vertex attribute input slot indices (`CreateVertexState:L136`). |
| `0x0B5` | â€” | TotK only | 1 | `uint8` | `has_custom_attributes`| Flag enabling custom attribute processing (`InitializeParticle:L194`). |
| `0x0B6` | â€” | TotK only | 1 | `uint8` | `vertex_stride_mode`| Particle vertex layout mode (1..4). |
| `0x0B7` | â€” | TotK only | 2 | `uint16` | `shader_features` | Feature flags for custom lighting / fog shader passes. |
| `0x0B8` | â€” | TotK only | 1 | `uint8` | `has_extended_blocks`| Set when extended FourCC attribute blocks are bound (`ResolveBinaryData`). |
| `0x0BE` | â€” | TotK only | 1 | `uint8` | `has_field_modifiers`| Set when field physics modifiers are bound (`CalculateParticleBehavior:L224`). |
| `0x0C0` | â€” | TotK only | 4 | `float` | `light_intensity` | Dynamic point light intensity scalar. |
| `0x0C4` | â€” | TotK only | 4 | `float` | `light_radius` | Dynamic point light radius. |
| `0x0C8` | â€” | TotK only | 12 | `float[3]` | `light_color_rgb` | Dynamic point light color RGB. |
| `0x0EC` | `0x744` | â€” | 4 | `float` | `init_velocity_factor`| Velocity multiplier evaluated in `CalculateParticle:L281`. |
| `0x100`â€“`0x49F` | â€” | â€” | 928 | `bytes` | `uniform_staging` | GPU constant buffer staging block (uploaded verbatim to UBO `memcpy(pvVar2, 0xca0)` at `0x710000bbe4:L1694`). |
| `0x4A0`â€“`0x4EF` | `0x2C0` | â€” | `0x50`| `bytes` | `tex0_uniform_block`| Slot 0 Albedo UV transform matrix & scroll rates (`UpdateParams:L198-203`). |
| `0x4F0`â€“`0x53F` | `0x310` | â€” | `0x50`| `bytes` | `tex1_uniform_block`| Slot 1 Alpha Mask UV transform matrix & scroll rates (`UpdateParams:L211-216`). |
| `0x540`â€“`0x58F` | `0x360` | â€” | `0x50`| `bytes` | `tex2_uniform_block`| Slot 2 Flow Map UV transform matrix & scroll rates (`UpdateParams:L224-229`). |
| `0x590`â€“`0x5DF` | â€” | TotK only | `0x50`| `bytes` | `tex3_uniform_block`| Slot 3 Emissive UV transform matrix (`UpdateParams:L237-242`). |
| `0x5E0`â€“`0x62F` | â€” | TotK only | `0x50`| `bytes` | `tex4_uniform_block`| Slot 4 Specular UV transform matrix (`UpdateParams:L250-255`). |
| `0x630`â€“`0x67F` | â€” | TotK only | `0x50`| `bytes` | `tex5_uniform_block`| Slot 5 Custom Light Map UV transform matrix (`UpdateParams:L263-268`). |
| `0x680` | `0x3B0` | `+0x2D0` | 4 | `float` | `alpha_scale` | Master alpha scale multiplier (`UpdateParams:L92`). |
| `0x690` | `0x3C0` | `+0x2D0` | 128 | `float[8][4]` | `kf_color0` | Color0 RGB keyframes `(val.xyz, time.w)` (`UpdateParams:L97, 494`). |
| `0x710` | `0x440` | `+0x2D0` | 128 | `float[8][4]` | `kf_alpha0` | Alpha0 keyframes `(val.x, time.w)` (`UpdateParams:L105, 674`). |
| `0x790` | `0x4C0` | `+0x2D0` | 128 | `float[8][4]` | `kf_color1` | Color1 RGB keyframes `(val.xyz, time.w)` (`UpdateParams:L114, 584`). |
| `0x810` | `0x540` | `+0x2D0` | 128 | `float[8][4]` | `kf_alpha1` | Alpha1 keyframes `(val.x, time.w)` (`UpdateParams:L122, 764`). |
| `0x8D0` | `0x600` | `+0x2D0` | 128 | `float[8][4]` | `kf_scale` | Scale XYZ keyframes `(val.xyz, time.w)` (`UpdateParams:L854`). |
| `0x950` | `0x680` | `+0x2D0` | 128 | `float[8][4]` | `totk_kf_rot` | Rotation XYZ keyframe array (`UpdateParams:L944`). |
| `0x9D0` | â€” | TotK only | 128 | `float[8][4]` | `totk_kf_track5` | Track 5 keyframe array (`UpdateParams:L1034`). |
| `0xA50` | â€” | TotK only | 128 | `float[8][4]` | `totk_kf_track6` | Track 6 keyframe array (`UpdateParams:L1124`). |
| `0xAD0` | â€” | TotK only | 128 | `float[8][4]` | `totk_kf_track7` | Track 7 keyframe array (`UpdateParams:L1202`). |
| `0xB50` | â€” | TotK only | 128 | `float[8][4]` | `totk_kf_track8` | Track 8 keyframe array (`UpdateParams:L1280`). |
| `0xC10`â€“`0xC98` | â€” | â€” | 144 | `bytes` | `runtime_matrix_state`| Transform and normal matrix staging buffer (`UpdateParams:L414-452`). |
| `0xCA0` | `0x748` | `+0x558` | 1 | `uint8` | `sim_flags` | Simulation flag; copied into runtime flag bit 13 in `Emitter::Initialize` (`0x7100001900`). |
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
| `0xD39` | `0x7E1` | `+0x558` | 1 | `uint8` | `billboard_mode` | Billboard mode: 0=Screen, 1=Y-Axis, 2=Directional, 3=LookAt, 4=Mesh (`DrawEmitter:L12`). |
| `0xD3C` | â€” | TotK only | 1 | `uint8` | `child_alloc_flag` | Copied into runtime flag bit 17; tested when creating child emitters (`0x7100001900`). |
| `0xD48` | `0x7F0` | `+0x558` | 1 | `uint8` | `emit_loop_mode` | Loop mode: 0 = Infinite / Loop, 1 = One-Shot (`UpdateParams:L1604`). |
| `0xD49` | `0x7F1` | `+0x558` | 1 | `uint8` | `gravity_coord` | Gravity vector coordinate space: 0 = World, 1 = Local (`CalculateParticleBehavior:L95`). |
| `0xD4A` | `0x7F2` | `+0x558` | 1 | `uint8` | `emit_dist_enable` | `emission.isEmitDistEnabled`. `Emitter::Initialize` sets runtime flag bit 21 from it; `CalculateRequiredParticleAsignmentCount` then uses `0xD8C` as the particle count (SDK `vfx_Emitter.cpp:223-228`). |
| `0xD4C` | `0x7F4` | `+0x558` | 4 | `uint32` | `emit_start_delay` | Delay before emission starts in frames. |
| `0xD50` | `0x7F8` | `+0x558` | 4 | `uint32` | `child_emit_timing` | Converted with `ucvtf` and used as a percentage (`/100`) of the parent particle lifetime (`CalculateParticle`). |
| `0xD54` | `0x7FC` | `+0x558` | 4 | `uint32` | `emit_duration` | Active emission duration in frames. `CalculateRequiredParticleAsignmentCount` uses it as `duration/(interval+1)`; `UpdateParams` clamps `0xD60` to it for one-shot, non-distance emitters; `CalculateParticle` adds it to the start time (SDK `emission.emitDuration`). |
| `0xD58` | `0x800` | `+0x558` | 4 | `float` | `emit_rate` | Particles per frame (`TryEmitParticle` reads it as float). |
| `0xD5C` | `0x804` | `+0x558` | 1+ | `uint8` | `emit_rate_random` | `TryEmitParticle` reads one byte here (SDK `rateRandom/100`). BotW doc has a 4-byte slot; bytes beyond the first are not read in TotK, so treat the slot as unverified past byte 0. |
| `0xD60` | `0x808` | `+0x558` | 4 | `int32` | `emit_interval` | `UpdateByEmit` computes `(D60 + 1) + random(D64)`, identical to SDK `vfx_Emitter.cpp:761` (`interval + 1.0f + GetInteger(intervalRandom)`). |
| `0xD64` | `0x80C` | `+0x558` | 4 | `int32` | `emit_interval_random` | Upper bound of the random range in `UpdateByEmit` (`emission.intervalRandom`). |
| `0xD70` | `0x818` | `+0x558` | 12 | `float[3]` | `gravity_xyz` | Constant acceleration / gravity vector XYZ (`CalculateParticleBehavior:L92-94`). |
| `0xD7C` | - | TotK only | 4 | `float` | `emit_dist_unit` | Distance-emission step: `TryEmitParticle` divides the clamped length by it (SDK `emitDistUnit`, `vfx_EmitterCalc.cpp:202-216`). |
| `0xD80` | - | TotK only | 4 | `float` | `emit_dist_min` | Lower clamp of the (scaled) travelled length (SDK `emitDistMin`). |
| `0xD84` | - | TotK only | 4 | `float` | `emit_dist_max` | Upper clamp of the (scaled) travelled length (SDK `emitDistMax`). |
| `0xD88` | - | TotK only | 4 | `float` | `emit_dist_margin` | Lengths below `margin * scale` are zeroed (SDK `emitDistMargin`). |
| `0xD8C` | - | TotK only | 4 | `int32` | `emit_dist_particle_max` | Particle count used when `emit_dist_enable` is set (`0x71000035F4`; SDK `emitDistParticleMax`). |
| `0xD90` | `0x838` | `+0x558` | 1 | `uint8` | `shape_type` | Shape enum: 0=Point, 1=Circle, 4=Sphere, 7=Cylinder, 9=Box, 11=Line (`InitializeParticle:L36`). |
| `0xD91` | `0x839` | `+0x558` | 1 | `uint8` | `shape_angle_mode` | Shape angle calculation mode: 0=Static, 1=Time-varying (`CalculateEmitCircle:L24`). |
| `0xD92` | `0x83A` | `+0x558` | 1 | `uint8` | `shape_rot_mode` | Shape orientation mode (`CalculateEmitSphere:L102`). |
| `0xD95` | `0x83E` | `+0x557` | 1 | `uint8` | `shape_rot_variant` | **1-byte packing delta**. Basis frame variant (`CalculateEmitSphereFill:L183`). |
| `0xD98` | `0x840` | `+0x558` | 4 | `float` | `shape_angle_b` | Emission arc spread in radians (`CalculateEmitCircle:L20`). |
| `0xD9C` | `0x844` | `+0x558` | 4 | `float` | `shape_angle_c` | Elevation / latitude cone angle in radians (`CalculateEmitSphere:L101`). |
| `0xDA0` | `0x848` | `+0x558` | 4 | `float` | `shape_angle_d` | Initial phase angle offset in radians (`CalculateEmitCircle:L21`). |
| `0xDA8` | `0x850` | `+0x558` | 4 | `float` | `shape_hollow_ratio`| Shape fill ratio: 0.0 = Solid Volume, 1.0 = Surface Shell (`CalculateEmitSphereFill`). |
| `0xDAC` | `0x854` | `+0x558` | 4 | `float` | `shape_line_a` | Line shape start parameter. |
| `0xDB0` | `0x858` | `+0x558` | 4 | `float` | `shape_line_b` | Line shape end parameter. |
| `0xDB4` | `0x85C` | `+0x558` | 12 | `float[3]` | `shape_radius_xyz` | Shape semi-axis radii along X, Y, Z (`CalculateEmitCircle:L49`). |
| `0xDCC` | â€” | TotK only | 4 | `int32` | `primitive_dist_mode`| 0 = Divided checks, 1/2 = Primitive indexing paths (`CalculateEmitPrimitive` `0x71000155FC`). |
| `0xDD0` | `0x878` | `+0x558` | 8 | `uint64` | `mesh_primitive_idx`| G3D primitive index used by `Resource::InitializeEmitterGraphicsResource` (`0x710001E650`). |
| `0xDD8` | `0x880` | `+0x558` | 4 | `int32` | `shape_divisions` | Slice division count for equally divided circle shapes. |
| `0xDE8` | `0x898` | `+0x550` | 1 | `uint8` | `render_color_write`| Blend / color write enable: 1 = Enabled (`Rendercontext::Initialize:L35`). |
| `0xDE9` | `0x899` | `+0x550` | 1 | `uint8` | `render_depth_write`| Depth buffer write mask enable. |
| `0xDEA` | `0x89A` | `+0x550` | 1 | `uint8` | `render_depth_func` | Depth comparison function (0..7) (`Rendercontext::Initialize:L55`). |
| `0xDEB` | `0x89B` | `+0x550` | 1 | `uint8` | `render_depth_test` | Depth testing enable flag. |
| `0xDEC` | `0x89C` | `+0x550` | 1 | `uint8` | `render_alpha_test` | Alpha testing enable flag. |
| `0xDED` | `0x89D` | `+0x550` | 1 | `uint8` | `render_alpha_func` | Alpha test comparison function. |
| `0xDEE` | `0x89E` | `+0x550` | 1 | `uint8` | `render_blend_mode` | Blend mode enum: 0=AlphaBlend, 1=Add, 2=Sub, 3=Mul, 4=Screen (`Rendercontext:L34`). |
| `0xDEF` | `0x89F` | `+0x550` | 1 | `uint8` | `render_cull_mode` | Rasterizer culling: 0=None/Double, 1=Front, 2=Back (`Rendercontext:L63`). |
| `0xDF0` | `0x8A0` | `+0x550` | 4 | `float` | `render_alpha_ref` | Alpha test reference cutoff threshold. |
| `0xDF8` | `0x8A8` | `+0x550` | 1 | `uint8` | `emit_infinite_flag`| Infinite emitter lifetime flag: 1 = Infinite (`UpdateParams:L1608`). |
| `0xDF9` | `0x8A9` | `+0x550` | 1 | `uint8` | `is_trimming_prim` | Trim primitive enable flag (`Resource::InitializeEmitterGraphicsResource` `0x710001E650`). |
| `0xDFC` | `0x8AC` | `+0x550` | 1 | `uint8` | `sort_mode` | Particle sort mode (`ShaderFlag::Initialize:L341-347`). |
| `0xDFD` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag0` | Shader feature flag bit 28 (`ShaderFlag::Initialize:L270-277`). |
| `0xDFE` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag1` | Shader feature flag bit 29 (`ShaderFlag::Initialize:L278-280`). |
| `0xDFF` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag2` | Shader feature flag bit 30 (`ShaderFlag::Initialize:L281-283`). |
| `0xE00` | â€” | TotK only | 1 | `uint8` | `normal_mat_slot0_enable`| Normal matrix slot 0 enable; clears `0xC10..0xC90` identity transform when 0 (`UpdateParams:L408-422`). |
| `0xE01` | â€” | TotK only | 1 | `uint8` | `normal_mat_slot1_enable`| Normal matrix slot 1 enable; clears `0xC14..0xC94` identity transform when 0 (`UpdateParams:L423-437`). |
| `0xE02` | â€” | TotK only | 1 | `uint8` | `normal_mat_slot2_enable`| Normal matrix slot 2 enable; clears `0xC18..0xC98` identity transform when 0 (`UpdateParams:L438-452`). |
| `0xE03` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag3` | Shader flag bit 18 in `param_1[1]` (`ShaderFlag::Initialize:L338`). |
| `0xE04` | â€” | TotK only | 1 | `uint8` | `shader_opt_flag4` | Shader flag bit 0 in `param_1[2]` (`ShaderFlag::Initialize:L348-355`). |
| `0xE08` | `0x8B8` | `+0x550` | 4 | `uint32` | `particle_lifespan` | Base particle lifetime in frames (`UpdateParams:L1612`, `InitializeParticle:L96`). |
| `0xE0C` | `0x8BC` | `+0x550` | 1 | `uint8` | `particle_lifespan_rnd`| Random lifespan variance percentage (`InitializeParticle:L105`). |
| `0xE10` | `0x8C0` | `+0x550` | 4 | `float` | `particle_fade_in` | Alpha fade-in duration in frames (`InitializeParticle:L96`). |
| `0xE14` | `0x8C4` | `+0x550` | 4 | `float` | `particle_fade_out` | Alpha fade-out duration in frames. |
| `0xE18` | `0x8C8` | `+0x550` | 8 | `uint64` | `g3d_primitive_idx` | Primary G3D primitive index used by `EmitterResource::Setup` (`0x710000B694`). |
| `0xE20` | `0x8D0` | `+0x550` | 8 | `uint64` | `trim_primitive_idx`| Optional trim primitive index, read when `0xDF9` is set (`0x710001E650`). |
| `0xE28` | `0x8D8` | `+0x550` | 1 | `uint8` | `loop_track0_enable`| Track 0 loop mode enable (`UpdateParams:L1355`). |
| `0xE29` | `0x8D9` | `+0x550` | 1 | `uint8` | `loop_track1_enable`| Track 1 loop mode enable (`UpdateParams:L1365`). |
| `0xE2A` | `0x8DA` | `+0x550` | 1 | `uint8` | `loop_track2_enable`| Track 2 loop mode enable (`UpdateParams:L1377`). |
| `0xE2B` | `0x8DB` | `+0x550` | 1 | `uint8` | `loop_track3_enable`| Track 3 loop mode enable (`UpdateParams:L1387`). |
| `0xE2C` | `0x8DC` | `+0x550` | 1 | `uint8` | `loop_track4_enable`| Track 4 loop mode enable (`UpdateParams:L1398`). |
| `0xE2D` | `0x8DD` | `+0x550` | 1 | `uint8` | `loop_track0_rnd` | Track 0 loop random initial phase sub-flag (`UpdateParams:L1360`). |
| `0xE2E` | `0x8DE` | `+0x550` | 1 | `uint8` | `loop_track1_rnd` | Track 1 loop random initial phase sub-flag (`UpdateParams:L1371`). |
| `0xE2F` | `0x8DF` | `+0x550` | 1 | `uint8` | `loop_track2_rnd` | Track 2 loop random initial phase sub-flag (`UpdateParams:L1382`). |
| `0xE30` | `0x8E0` | `+0x550` | 1 | `uint8` | `loop_track3_rnd` | Track 3 loop random initial phase sub-flag (`UpdateParams:L1392`). |
| `0xE31` | `0x8E1` | `+0x550` | 1 | `uint8` | `loop_track4_rnd` | Track 4 loop random initial phase sub-flag (`UpdateParams:L1403`). |
| `0xE34` | â€” | TotK only | 2 | `uint16` | `loop_track0_rate_u16`| Track 0 loop cycle interval in frames as 16-bit int (`UpdateParams:L1356`). |
| `0xE36` | â€” | TotK only | 2 | `uint16` | `loop_track1_rate_u16`| Track 1 loop cycle interval in frames as 16-bit int (`UpdateParams:L1366`). |
| `0xE38` | â€” | TotK only | 2 | `uint16` | `loop_track2_rate_u16`| Track 2 loop cycle interval in frames as 16-bit int (`UpdateParams:L1378`). |
| `0xE3A` | â€” | TotK only | 2 | `uint16` | `loop_track3_rate_u16`| Track 3 loop cycle interval in frames as 16-bit int (`UpdateParams:L1388`). |
| `0xE3C` | `0x8F4` | `+0x548` | 4 | `int32` | `loop_track4_rate_i32`| Track 4 loop cycle interval in frames as 32-bit int (`UpdateParams:L1399`). |
| `0xE49` | â€” | TotK only | 1 | `uint8` | `waveform_ctrl0` | Waveform control nibble 0 (`ShaderFlag::Initialize:L33-46`). |
| `0xE4A` | â€” | TotK only | 1 | `uint8` | `waveform_ctrl1` | Waveform control nibble 1 (`ShaderFlag::Initialize:L47-60`). |
| `0xE4B` | â€” | TotK only | 1 | `uint8` | `waveform_ctrl2` | Waveform control nibble 2 (`ShaderFlag::Initialize:L61-74`). |
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
| `0xF14` | `0x974` | `+0x5A0` | 4 | `float` | `spread_cone_angle` | Initial velocity directional dispersion cone angle in radians (`FUN_71000127f0:L312`). |
| `0xF44` | `0x9A4` | `+0x5A0` | 1 | `uint8` | `color0_mode` | Color0 mode: 0=Constant, 2=8-Key anim, 3=Random (`UpdateParams:L95`). |
| `0xF45` | `0x9A5` | `+0x5A0` | 1 | `uint8` | `color1_mode` | Color1 mode: 0=Constant, 2=8-Key anim, 3=Random (`UpdateParams:L111`). |
| `0xF46` | `0x9A6` | `+0x5A0` | 1 | `uint8` | `alpha0_mode` | Alpha0 mode: 0=Constant, 2=8-Key anim, 3=Random (`UpdateParams:L102`). |
| `0xF47` | `0x9A7` | `+0x5A0` | 1 | `uint8` | `alpha1_mode` | Alpha1 mode: 0=Constant, 2=8-Key anim, 3=Random (`UpdateParams:L119`). |
| `0xF48` | `0x9A8` | `+0x5A0` | 12 | `float[3]` | `color0_const_rgb` | Color0 constant RGB values (`UpdateParams:L97`). |
| `0xF54` | `0x9B4` | `+0x5A0` | 4 | `float` | `alpha0_const` | Alpha0 constant Alpha value (`UpdateParams:L105`). |
| `0xF58` | `0x9B8` | `+0x5A0` | 12 | `float[3]` | `color1_const_rgb` | Color1 constant RGB values (`UpdateParams:L114`). |
| `0xF64` | `0x9C4` | `+0x5A0` | 4 | `float` | `alpha1_const` | Alpha1 constant Alpha value (`UpdateParams:L122`). |
| `0xF68` | `0x9C8` | `+0x5A0` | 12 | `float[3]` | `particle_scale_xyz`| Initial particle base scale XYZ (`InitializeParticle:L59-64`). |
| `0xF74` | `0x9D4` | `+0x5A0` | 12 | `float[3]` | `particle_scale_rnd`| Initial particle scale random range XYZ (`InitializeParticle:L51-86`). |
| `0xF8F` | `0x9EF` | `+0x5A0` | 1 | `uint8` | `color_pulse_mode` | Waveform control byte (high nibble: 1=2, 2=4; `ShaderFlag::Initialize:L19-32`). |
| `0xF98` | `0x9F8` | Indexed | 8 | `uint64` | `tex_slot0_guid` | Texture Slot 0 GUID (`TextureSlotId_0`: Primary/Albedo). Stride `0x18`. (`UpdateParams:L65`). |
| `0xFB0` | `0xA18` | Indexed | 8 | `uint64` | `tex_slot1_guid` | Texture Slot 1 GUID (`TextureSlotId_1`: Secondary/Mask). (`UpdateParams:L70`). |
| `0xFC8` | `0xA38` | Indexed | 8 | `uint64` | `tex_slot2_guid` | Texture Slot 2 GUID (`TextureSlotId_2`: Tertiary/Flow). (`UpdateParams:L75`). |
| `0xFE0` | â€” | TotK only | 8 | `uint64` | `tex_slot3_guid` | Texture Slot 3 GUID (`TextureSlotId_3`). (`UpdateParams:L80`). |
| `0xFF8` | â€” | TotK only | 8 | `uint64` | `tex_slot4_guid` | Texture Slot 4 GUID (`TextureSlotId_4`). (`UpdateParams:L85`). |
| `0x1010`| â€” | TotK only | 8 | `uint64` | `tex_slot5_guid` | Texture Slot 5 GUID (`TextureSlotId_5`). (`UpdateParams:L90`). |
| `0x1028`| `0xA58` | Indexed | 1 | `uint8` | `tex0_flipbook_type`| Slot 0 Flipbook / sprite-sheet animation (0=Standard, 4=Flipbook, 6=Extended; `UpdateParams:L458`). |
| `0x1029`| `0xA59` | Indexed | 1 | `uint8` | `tex0_uv_scroll` | Slot 0 UV translation scroll enable (`UpdateParams:L197`). |
| `0x102A`| `0xA5A` | Indexed | 1 | `uint8` | `tex0_uv_rotate` | Slot 0 UV rotation animation enable (`UpdateParams:L270`). |
| `0x102B`| `0xA5B` | Indexed | 1 | `uint8` | `tex0_uv_scale` | Slot 0 UV scale animation enable (`UpdateParams:L330`). |
| `0x102C`| `0xA5C` | Indexed | 1 | `uint8` | `tex0_wrap_mode` | Slot 0 texture wrap mode (0=Clamp, 1=Repeat, 2=Mirror; `UpdateParams:L125`). |
| `0x102D`| `0xA5D` | Indexed | 1 | `uint8` | `tex0_sampler_flag0`| Slot 0 sampler filtering sub-flag (`ShaderFlag::Initialize:L284`). |
| `0x102E`| `0xA5E` | Indexed | 1 | `uint8` | `tex0_sampler_flag1`| Slot 0 sampler filtering sub-flag (`ShaderFlag::Initialize:L287`). |
| `0x102F`| `0xA5F` | Indexed | 1 | `uint8` | `tex0_sampler_flag2`| Slot 0 sampler filtering sub-flag (`ShaderFlag::Initialize:L320`). |
| `0x1038`| `0xA68` | Indexed | 1 | `uint8` | `tex1_flipbook_type`| Slot 1 Flipbook / sprite-sheet animation (`UpdateParams:L463`). |
| `0x1039`| `0xA69` | Indexed | 1 | `uint8` | `tex1_uv_scroll` | Slot 1 UV translation scroll enable (`UpdateParams:L208`). |
| `0x103A`| `0xA6A` | Indexed | 1 | `uint8` | `tex1_uv_rotate` | Slot 1 UV rotation animation enable (`UpdateParams:L280`). |
| `0x103B`| `0xA6B` | Indexed | 1 | `uint8` | `tex1_uv_scale` | Slot 1 UV scale animation enable (`UpdateParams:L343`). |
| `0x103C`| `0xA6C` | Indexed | 1 | `uint8` | `tex1_wrap_mode` | Slot 1 texture wrap mode (`UpdateParams:L131`). |
| `0x103D`| `0xA6D` | Indexed | 1 | `uint8` | `tex1_sampler_flag0`| Slot 1 sampler filtering sub-flag (`ShaderFlag::Initialize:L290`). |
| `0x103E`| `0xA6E` | Indexed | 1 | `uint8` | `tex1_sampler_flag1`| Slot 1 sampler filtering sub-flag (`ShaderFlag::Initialize:L293`). |
| `0x103F`| `0xA6F` | Indexed | 1 | `uint8` | `tex1_sampler_flag2`| Slot 1 sampler filtering sub-flag (`ShaderFlag::Initialize:L323`). |
| `0x1048`| `0xA78` | Indexed | 1 | `uint8` | `tex2_flipbook_type`| Slot 2 Flipbook / sprite-sheet animation (`UpdateParams:L468`). |
| `0x1049`| `0xA79` | Indexed | 1 | `uint8` | `tex2_uv_scroll` | Slot 2 UV translation scroll enable (`UpdateParams:L218`). |
| `0x104A`| `0xA7A` | Indexed | 1 | `uint8` | `tex2_uv_rotate` | Slot 2 UV rotation animation enable (`UpdateParams:L290`). |
| `0x104B`| `0xA7B` | Indexed | 1 | `uint8` | `tex2_uv_scale` | Slot 2 UV scale animation enable (`UpdateParams:L356`). |
| `0x104C`| `0xA7C` | Indexed | 1 | `uint8` | `tex2_wrap_mode` | Slot 2 texture wrap mode (`UpdateParams:L143`). |
| `0x104D`| `0xA7D` | Indexed | 1 | `uint8` | `tex2_sampler_flag0`| Slot 2 sampler filtering sub-flag (`ShaderFlag::Initialize:L296`). |
| `0x104E`| `0xA7E` | Indexed | 1 | `uint8` | `tex2_sampler_flag1`| Slot 2 sampler filtering sub-flag (`ShaderFlag::Initialize:L299`). |
| `0x104F`| `0xA7F` | Indexed | 1 | `uint8` | `tex2_sampler_flag2`| Slot 2 sampler filtering sub-flag (`ShaderFlag::Initialize:L326`). |
| `0x1058`| â€” | TotK only | 1 | `uint8` | `tex3_flipbook_type`| Slot 3 Flipbook / sprite-sheet animation (`UpdateParams:L473`). |
| `0x1059`| â€” | TotK only | 1 | `uint8` | `tex3_uv_scroll` | Slot 3 UV translation scroll enable (`UpdateParams:L231`). |
| `0x105A`| â€” | TotK only | 1 | `uint8` | `tex3_uv_rotate` | Slot 3 UV rotation animation enable (`UpdateParams:L300`). |
| `0x105B`| â€” | TotK only | 1 | `uint8` | `tex3_uv_scale` | Slot 3 UV scale animation enable (`UpdateParams:L369`). |
| `0x105C`| â€” | TotK only | 1 | `uint8` | `tex3_wrap_mode` | Slot 3 texture wrap mode (`UpdateParams:L155`). |
| `0x105D`| â€” | TotK only | 1 | `uint8` | `tex3_sampler_flag0`| Slot 3 sampler filtering sub-flag (`ShaderFlag::Initialize:L302`). |
| `0x105E`| â€” | TotK only | 1 | `uint8` | `tex3_sampler_flag1`| Slot 3 sampler filtering sub-flag (`ShaderFlag::Initialize:L305`). |
| `0x105F`| â€” | TotK only | 1 | `uint8` | `tex3_sampler_flag2`| Slot 3 sampler filtering sub-flag (`ShaderFlag::Initialize:L329`). |
| `0x1068`| â€” | TotK only | 1 | `uint8` | `tex4_flipbook_type`| Slot 4 Flipbook / sprite-sheet animation (`UpdateParams:L478`). |
| `0x1069`| â€” | TotK only | 1 | `uint8` | `tex4_uv_scroll` | Slot 4 UV translation scroll enable (`UpdateParams:L245`). |
| `0x106A`| â€” | TotK only | 1 | `uint8` | `tex4_uv_rotate` | Slot 4 UV rotation animation enable (`UpdateParams:L310`). |
| `0x106B`| â€” | TotK only | 1 | `uint8` | `tex4_uv_scale` | Slot 4 UV scale animation enable (`UpdateParams:L382`). |
| `0x106C`| â€” | TotK only | 1 | `uint8` | `tex4_wrap_mode` | Slot 4 texture wrap mode (`UpdateParams:L167`). |
| `0x106D`| â€” | TotK only | 1 | `uint8` | `tex4_sampler_flag0`| Slot 4 sampler filtering sub-flag (`ShaderFlag::Initialize:L308`). |
| `0x106E`| â€” | TotK only | 1 | `uint8` | `tex4_sampler_flag1`| Slot 4 sampler filtering sub-flag (`ShaderFlag::Initialize:L311`). |
| `0x106F`| â€” | TotK only | 1 | `uint8` | `tex4_sampler_flag2`| Slot 4 sampler filtering sub-flag (`ShaderFlag::Initialize:L332`). |
| `0x1078`| â€” | TotK only | 1 | `uint8` | `tex5_flipbook_type`| Slot 5 Flipbook / sprite-sheet animation (`UpdateParams:L483`). |
| `0x1079`| â€” | TotK only | 1 | `uint8` | `tex5_uv_scroll` | Slot 5 UV translation scroll enable (`UpdateParams:L257`). |
| `0x107A`| â€” | TotK only | 1 | `uint8` | `tex5_uv_rotate` | Slot 5 UV rotation animation enable (`UpdateParams:L320`). |
| `0x107B`| â€” | TotK only | 1 | `uint8` | `tex5_uv_scale` | Slot 5 UV scale animation enable (`UpdateParams:L395`). |
| `0x107C`| â€” | TotK only | 1 | `uint8` | `tex5_wrap_mode` | Slot 5 texture wrap mode (`UpdateParams:L179`). |
| `0x107D`| â€” | TotK only | 1 | `uint8` | `tex5_sampler_flag0`| Slot 5 sampler filtering sub-flag (`ShaderFlag::Initialize:L314`). |
| `0x107E`| â€” | TotK only | 1 | `uint8` | `tex5_sampler_flag1`| Slot 5 sampler filtering sub-flag (`ShaderFlag::Initialize:L317`). |
| `0x107F`| â€” | TotK only | 1 | `uint8` | `tex5_sampler_flag2`| Slot 5 sampler filtering sub-flag (`ShaderFlag::Initialize:L335`). |
| **`0x10C8`**| â€” | â€” | â€” | â€” | **Struct End** | End of fixed data struct (`0x10C8` bytes total). |
