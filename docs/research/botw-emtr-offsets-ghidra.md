# BotW `EMTR` Executable Evidence & Field Map

This document records the exact byte offsets, types, executable behaviors, and Ghidra Switch ARM64 decompilation citations for Breath of the Wild (`nn::vfx`, VFXB v20).

Offsets are relative to the serialized `EMTR` node's data start (`EMTR.node + dataRelativeOffset`).

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

## 2. Confirmed Attribute Handlers (`ResolveBinaryData`)

`nn::vfx::EmitterResource::ResolveBinaryData` at `0x7100adcca8` traverses child attribute nodes and resolves the following FourCC tags:

```text
EAA0 EAA1 EAC0 EAC1 EAPL EASL EAER EADV EAGV EAOV
EATR EAES EAET EASS EP01 EP02 EP03 EP04
FRN1 FRND FPAD FCSF FMAG FCOL FCLN FSPN
CADP CSDP CUDP
```

* `CSDP`: Payload size stored as `nodeSize - 0x20`.
* `EP01`–`EP04`: Binds emitter plugin modes 1 through 4.
* `FCOV` and `FGWD`: Exclusive to TotK (`nn::vfx2`), not handled in BotW `ResolveBinaryData`.

---

## 3. Byte-by-Byte Verified Field Map

Every field below is proven by decompiled instructions in the BotW Switch ARM64 1.6.0 binary (`main.analyzed`).

| BotW Offset | Size (B) | Type | Field Name | Executable Behavior & Ghidra Verification Evidence |
|:---:|:---:|:---:|:---|:---|
| `0x000` | 4 | `char[4]` | `magic` | FourCC node magic: `'EMTR'`. |
| `0x004` | 4 | `uint32` | `node_size` | Serialized node data length: `0x0A88` (2,696 bytes). |
| `0x008` | 4 | `uint32` | `version` | Binary format version (`0x00041400` / `0x14` = 20). |
| `0x00C` | 4 | `uint32` | `flags` | Node behavior flags. |
| `0x010` | 64 | `char[64]` | `emitter_name` | Emitter identifier null-terminated C-string. |
| **`+0x050`**| — | — | **ResEmitter Body Start** | Stored into `EmitterResource + 0x18`; base for internal resource offsets. |
| `0x050` | 4 | `uint32` | `eset_emitter_index` | Emitter index within parent ESET list. |
| `0x054` | 4 | `uint32` | `shader_flags_init` | Initial shader flag bitmask; written to runtime mask by `FUN_7100adb8f4:L450`. |
| `0x058` | 4 | `uint32` | `custom_param_flags`| Custom attribute binding flags. |
| `0x05C` | 4 | `uint32` | `lod_flags` | LOD calculation flags. |
| `0x060` | 4 | `uint32` | `color0_key_count` | Keyframe count (0..8) for Color0 RGB track. Checked in `FUN_7100adb8f4:L207`. |
| `0x064` | 4 | `uint32` | `alpha0_key_count` | Keyframe count (0..8) for Alpha0 track. Checked in `FUN_7100adb8f4:L257`. |
| `0x068` | 4 | `uint32` | `color1_key_count` | Keyframe count (0..8) for Color1 RGB track. Checked in `FUN_7100adb8f4:L232`. |
| `0x06C` | 4 | `uint32` | `alpha1_key_count` | Keyframe count (0..8) for Alpha1 track. Checked in `FUN_7100adb8f4:L282`. |
| `0x070` | 4 | `uint32` | `scale_key_count` | Keyframe count (0..8) for Scale XYZ track. Checked in `FUN_7100adb8f4:L307`. |
| `0x074` | 4 | `uint32` | `rot_key_count` | Keyframe count (0..8) for Track 5 / Rotation. Checked in `FUN_7100adb8f4:L332`. |
| `0x080` | 4 | `float` | `color0_loop_timer` | Staged loop cycle duration for Color0. `FUN_7100adb8f4:L363`. |
| `0x084` | 4 | `float` | `color1_loop_timer` | Staged loop cycle duration for Color1. `FUN_7100adb8f4:L375`. |
| `0x088` | 4 | `float` | `alpha0_loop_timer` | Staged loop cycle duration for Alpha0. `FUN_7100adb8f4:L385`. |
| `0x08C` | 4 | `float` | `alpha1_loop_timer` | Staged loop cycle duration for Alpha1. `FUN_7100adb8f4:L396`. |
| `0x090` | 4 | `float` | `scale_loop_timer` | Staged loop cycle duration for Scale. `FUN_7100adb8f4:L406`. |
| `0x094` | 4 | `float` | `color0_loop_flag` | Staged loop enable flag float for Color0. `FUN_7100adb8f4:L369`. |
| `0x098` | 4 | `float` | `color1_loop_flag` | Staged loop enable flag float for Color1. `FUN_7100adb8f4:L380`. |
| `0x09C` | 4 | `float` | `alpha0_loop_flag` | Staged loop enable flag float for Alpha0. `FUN_7100adb8f4:L390`. |
| `0x0A0` | 4 | `float` | `alpha1_loop_flag` | Staged loop enable flag float for Alpha1. `FUN_7100adb8f4:L401`. |
| `0x0A4` | 4 | `float` | `scale_loop_flag` | Staged loop enable flag float for Scale. `FUN_7100adb8f4:L411`. |
| `0x0A8`–`0x2BF` | 536 | `bytes` | `uniform_staging` | GPU constant buffer parameters (`memcpy(pvVar2, dataStart, 0x750)` at `0x7100adb8f4:L635`). |
| `0x2C0`–`0x30F` | `0x50`| `bytes` | `tex0_uniform_block`| Slot 0 UV transform matrix & scroll rates. Zeroed when `0xA59` is 0 (`0x7100adb8f4:L114`). |
| `0x310`–`0x35F` | `0x50`| `bytes` | `tex1_uniform_block`| Slot 1 UV transform matrix & scroll rates. Zeroed when `0xA69` is 0 (`0x7100adb8f4:L123`). |
| `0x360`–`0x3AF` | `0x50`| `bytes` | `tex2_uniform_block`| Slot 2 UV transform matrix & scroll rates. Zeroed when `0xA79` is 0 (`0x7100adb8f4:L132`). |
| `0x3B0` | 4 | `float` | `alpha_scale` | Master alpha scale multiplier. |
| `0x3C0`–`0x43F` | 128 | `float[8][4]` | `kf_color0` | Color0 RGB keyframes `(val.xyz, time.w)`. Fallback to constant `0x9A8` when mode 0 (`0x7100adb8f4:L74`). |
| `0x440`–`0x4BF` | 128 | `float[8][4]` | `kf_alpha0` | Alpha0 keyframes `(val.x, time.w)`. Fallback to constant `0x9B4` when mode 0 (`0x7100adb8f4:L80`). |
| `0x4C0`–`0x53F` | 128 | `float[8][4]` | `kf_color1` | Color1 RGB keyframes `(val.xyz, time.w)`. Fallback to constant `0x9B8` when mode 0 (`0x7100adb8f4:L84`). |
| `0x540`–`0x5BF` | 128 | `float[8][4]` | `kf_alpha1` | Alpha1 keyframes `(val.x, time.w)`. Fallback to constant `0x9C4` when mode 0 (`0x7100adb8f4:L90`). |
| `0x600`–`0x67F` | 128 | `float[8][4]` | `kf_scale` | Scale XYZ keyframes `(val.xyz, time.w)`. `FUN_7100adb8f4:L312`. |
| `0x680`–`0x6FF` | 128 | `float[8][4]` | `kf_rot` | Track 5 / Rotation keyframes `(val.xyz, time.w)`. `FUN_7100adb8f4:L337`. |
| `0x700`–`0x73C` | 64 | `bytes` | `light_uniforms` | Dynamic point light uniform block. Zeroed if channels `0x8B0..0x8B2` disabled (`0x7100adb8f4:L186-205`). |
| `0x752` | 1 | `uint8` | `emitter_calc_type` | Mode: 0 = CPU particle simulation, 2 = GPU compute simulation (`0x7100ad5a74:L75`, `0x7100ad73d0:L953`). |
| `0x753` | 1 | `uint8` | `feature_mask_variant` | Bitfield variant selector for `param_1[1]` in `FUN_7100adc8a8:L170`. |
| `0x754` | 1 | `uint8` | `is_fade_alpha_fade` | Fade-out enabled flag; tested in `CalculateParticle` (`0x7100ad73d0:L885`). |
| `0x755` | 1 | `uint8` | `fade_out_curve` | Fade-out alpha curve selector (0=Off, 1..3=Curve); tested in `0x7100ad73d0:L885, 991`. |
| `0x756` | 1 | `uint8` | `fade_out_scale` | Fade-out scale flag; tested in `0x7100ad73d0:L886, 991`. |
| `0x757` | 1 | `uint8` | `seed_source` | Random seed source: 0 = Global PRNG, 1 = Parent ESET seed, 2 = Fixed seed (`0x7100ad583c:L97`). |
| `0x758` | 1 | `uint8` | `is_matrix_by_emit` | 1 = Recalculate transform matrix via `CreateResMatrix` on each emit (`0x7100ad69b0:L12`). |
| `0x75B` | 1 | `uint8` | `fade_in_curve` | Fade-in alpha curve selector; tested in `0x7100ad73d0:L877`. |
| `0x75C` | 1 | `uint8` | `fade_in_scale` | Fade-in scale flag; tested in `0x7100ad73d0:L877`. |
| `0x760` | 4 | `uint32` | `fixed_seed` | Fixed random seed value applied when `seed_source == 2` (`0x7100ad583c:L99`). |
| `0x768` | 4 | `int32` | `fade_out_time` | Fade-out duration in frames (divisor in `0x7100ad73d0:L888, 992`). |
| `0x76C` | 4 | `int32` | `fade_in_time` | Fade-in duration in frames (divisor in `0x7100ad73d0:L880`). |
| `0x770` | 12 | `float[3]` | `emitter_trans_xyz` | Emitter base translation coordinates XYZ (`CreateResMatrix` `0x7100ad6560:L147`). |
| `0x77C` | 12 | `float[3]` | `emitter_trans_rnd` | Emitter translation random range XYZ (`0x7100ad6560:L150`). |
| `0x788` | 12 | `float[3]` | `emitter_rot_xyz` | Emitter base Euler rotation XYZ in radians (`0x7100ad6560:L55`). |
| `0x794` | 12 | `float[3]` | `emitter_rot_rnd` | Emitter rotation random range XYZ in radians (`0x7100ad6560:L58`). |
| `0x7A0` | 12 | `float[3]` | `emitter_scale_xyz` | Emitter base scaling factor XYZ (`0x7100ad6560:L207`). |
| `0x7E1` | 1 | `uint8` | `child_pre_draw` | 1 = Draw child emitter before parent, 0 = Draw after parent (`EmitterSet::Draw` `0x7100aca65c:L20`). |
| `0x7E4` | 1 | `uint8` | `child_timing_mode` | 1 = Child emitter trigger timing relative to parent particle lifetime % (`0x7100ad73d0:L191`). |
| `0x7F0` | 1 | `uint8` | `emit_loop_mode` | Emission loop mode: 0 = Loop / Infinite, 1 = One-Time (`0x7100ae44ec:L107`, `0x7100ad73d0:L924`). |
| `0x7F1` | 1 | `uint8` | `gravity_coord` | Gravity vector coordinate system: 0 = World, 1 = Emitter Local (`FUN_7100adc8a8:L11`). |
| `0x7F2` | 1 | `uint8` | `emit_dist_enable` | Emission trigger mode: 0 = Time-based, 1 = Distance-based (`0x7100ad7084:L35`). |
| `0x7F4` | 4 | `uint32` | `emit_start_delay` | Delay before emission starts in frames (`0x7100ad73d0:L164`). |
| `0x7F8` | 4 | `uint32` | `child_emit_timing` | Parent particle life % threshold triggering child emitter (`0x7100ad73d0:L192`). |
| `0x7FC` | 4 | `uint32` | `emit_duration` | Active emission period duration in frames (`0x7100ad73d0:L163`). |
| `0x800` | 4 | `float` | `emit_rate` | Particles emitted per frame (`TryEmitParticle` `0x7100ad7084:L40`). |
| `0x804` | 4 | `int32` | `emit_rate_random` | Emission rate random variance % (`0x7100ad7084:L39`). |
| `0x808` | 4 | `int32` | `emit_interval` | Emission interval period in frames (`UpdateByEmit` `0x7100ad69b0:L7`). |
| `0x80C` | 4 | `int32` | `emit_interval_rnd` | Emission interval random variance in frames (`0x7100ad69b0:L8`). |
| `0x818` | 12 | `float[3]` | `gravity_xyz` | Constant acceleration / gravity vector XYZ. |
| `0x838` | 1 | `uint8` | `shape_type` | Emission volume shape enum: 0=Point, 1=Circle, 2=CircleDiv, 3=CircleFill, 4=Sphere, 5=SphereFill, etc. (`0x7100ad7084:L44`, `0x7100adb8f4:L413`). |
| `0x839` | 1 | `uint8` | `shape_angle_mode` | Angle generation mode: 0 = Static phase, 1 = Time-varying phase (`CalculateEmitCircle` `0x7100add248:L14`). |
| `0x83A` | 1 | `uint8` | `shape_rot_mode` | Shape orientation mode. |
| `0x83E` | 1 | `uint8` | `shape_rot_variant` | Coordinate basis frame variant. |
| `0x840` | 4 | `float` | `shape_angle_b` | Emission arc spread in radians (`0x7100add248:L20`). |
| `0x844` | 4 | `float` | `shape_angle_c` | Elevation / latitude cone angle in radians. |
| `0x848` | 4 | `float` | `shape_angle_d` | Initial phase angle offset in radians (`0x7100add248:L15`). |
| `0x850` | 4 | `float` | `shape_hollow_ratio`| Shape volume fill ratio: 0.0 = Solid Volume, 1.0 = Surface Shell (`CalculateEmitCircleFill` `0x7100add704:L79`). |
| `0x85C` | 12 | `float[3]` | `shape_radius_xyz` | Shape semi-axis radii (X: `0x85C`, Y: `0x860`, Z: `0x864`) (`0x7100add248:L42`, `0x7100add704:L91`). |
| `0x878` | 8 | `uint64` | `mesh_primitive_idx`| Mesh primitive index looked up by `Resource::InitializeEmitterGraphicsResource` (`0x7100ae3e54:L19`). |
| `0x880` | 4 | `int32` | `shape_divisions` | Slice division count for equally divided shapes. |
| `0x898` | 1 | `uint8` | `render_color_write`| Color buffer write mask: 1 = Enabled. |
| `0x899` | 1 | `uint8` | `render_depth_write`| Depth buffer write mask: 1 = Enabled. |
| `0x89A` | 1 | `uint8` | `render_depth_func` | Depth comparison function (GX2 / NVN comparison enum). |
| `0x89B` | 1 | `uint8` | `render_depth_test` | Depth test enable: 1 = Enabled. |
| `0x89C` | 1 | `uint8` | `render_alpha_test` | Alpha test enable: 1 = Enabled. |
| `0x89D` | 1 | `uint8` | `render_alpha_func` | Alpha test comparison function enum. |
| `0x89E` | 1 | `uint8` | `render_blend_mode` | Blend mode enum: 0=AlphaBlend, 1=Additive, 2=Subtractive, 3=Multiplicative, 4=Screen, 5=Replace. |
| `0x89F` | 1 | `uint8` | `render_cull_mode` | Rasterizer cull mode: 0=None/Double-sided, 1=Front, 2=Back. |
| `0x8A0` | 4 | `float` | `render_alpha_ref` | Alpha test reference cutoff threshold. |
| `0x8A8` | 1 | `uint8` | `emit_infinite_flag`| 1 = Infinite emitter lifetime (emitter never expires) (`0x7100ae44ec:L102`, `0x7100ad73d0:L989`). |
| `0x8A9` | 1 | `uint8` | `is_trimming_prim` | 1 = Trimming primitive enabled (`0x7100ae3e54:L26`). |
| `0x8AC` | 1 | `uint8` | `sort_mode` | Particle sort mode (encoded into bits 29/30 of feature mask in `FUN_7100adc8a8:L135`). |
| `0x8AD` | 1 | `uint8` | `rot_rev_rand_x` | 1 = 50% random inversion of rotation X axis (`CalculateRotationMatrix` `0x7100ae2264:L46`). |
| `0x8AE` | 1 | `uint8` | `rot_rev_rand_y` | 1 = 50% random inversion of rotation Y axis (`0x7100ae2264:L56`). |
| `0x8AF` | 1 | `uint8` | `rot_rev_rand_z` | 1 = 50% random inversion of rotation Z axis (`0x7100ae2264:L59`). |
| `0x8B0` | 1 | `uint8` | `light_channel0_mode`| Dynamic point light channel 0 enable (`FUN_7100adb8f4:L186`). |
| `0x8B1` | 1 | `uint8` | `light_channel1_mode`| Dynamic point light channel 1 enable (`FUN_7100adb8f4:L193`). |
| `0x8B2` | 1 | `uint8` | `light_channel2_mode`| Dynamic point light channel 2 enable (`FUN_7100adb8f4:L200`). |
| `0x8B8` | 4 | `uint32` | `particle_lifespan` | Particle lifespan duration in frames (`0x7100ad73d0:L241, 982, 990`). |
| `0x8BC` | 4 | `uint32` | `particle_lifespan_rnd`| Random lifespan variance percentage. |
| `0x8C0` | 4 | `float` | `particle_fade_rate`| Particle alpha fade-in duration in frames. |
| `0x8C4` | 4 | `uint32` | `particle_fade_rnd` | Particle alpha fade-out duration / variance in frames. |
| `0x8C8` | 8 | `uint64` | `g3d_primitive_idx` | G3D primitive index looked up by `EmitterResource::Setup` (`0x7100adb7c0:L18`). |
| `0x8D0` | 8 | `uint64` | `trim_primitive_idx`| Trimming primitive index looked up by `0x7100ae3e54:L26`. |
| `0x8D8` | 1 | `uint8` | `color0_loop_enable`| Color0 animation looping enabled (`0x7100ae19ac:L87`, `0x7100adb8f4:L360`). |
| `0x8D9` | 1 | `uint8` | `alpha0_loop_enable`| Alpha0 animation looping enabled (`0x7100ae19ac:L108`, `0x7100adb8f4:L372`). |
| `0x8DA` | 1 | `uint8` | `color1_loop_enable`| Color1 animation looping enabled (`0x7100ae1e08:L87`, `0x7100adb8f4:L382`). |
| `0x8DB` | 1 | `uint8` | `alpha1_loop_enable`| Alpha1 animation looping enabled (`0x7100ae1e08:L108`, `0x7100adb8f4:L393`). |
| `0x8DC` | 1 | `uint8` | `scale_loop_enable` | Scale animation looping enabled (`0x7100ae1514:L52`, `0x7100adb8f4:L403`). |
| `0x8DD` | 1 | `uint8` | `color0_loop_rnd` | Color0 loop random initial phase (`0x7100ae19ac:L93`, `0x7100adb8f4:L366`). |
| `0x8DE` | 1 | `uint8` | `alpha0_loop_rnd` | Alpha0 loop random initial phase (`0x7100ae19ac:L114`, `0x7100adb8f4:L377`). |
| `0x8DF` | 1 | `uint8` | `color1_loop_rnd` | Color1 loop random initial phase (`0x7100ae1e08:L93`, `0x7100adb8f4:L387`). |
| `0x8E0` | 1 | `uint8` | `alpha1_loop_rnd` | Alpha1 loop random initial phase (`0x7100ae1e08:L114`, `0x7100adb8f4:L398`). |
| `0x8E1` | 1 | `uint8` | `scale_loop_rnd` | Scale loop random initial phase (`0x7100ae1514:L55`, `0x7100adb8f4:L408`). |
| `0x8E4` | 4 | `int32` | `color0_loop_rate` | Color0 loop period in frames (`0x7100ae19ac:L91`, `0x7100adb8f4:L361`). |
| `0x8E8` | 4 | `int32` | `alpha0_loop_rate` | Alpha0 loop period in frames (`0x7100ae19ac:L112`, `0x7100adb8f4:L373`). |
| `0x8EC` | 4 | `int32` | `color1_loop_rate` | Color1 loop period in frames (`0x7100ae1e08:L91`, `0x7100adb8f4:L383`). |
| `0x8F0` | 4 | `int32` | `alpha1_loop_rate` | Alpha1 loop period in frames (`0x7100ae1e08:L112`, `0x7100adb8f4:L394`). |
| `0x8F4` | 4 | `int32` | `scale_loop_rate` | Scale loop period in frames (`0x7100ae1514:L53`, `0x7100adb8f4:L404`). |
| `0x914` | 4 | `int32` | `shader_normal_idx` | Normal vertex/pixel shader index in `SHDA`/`GRSN` archive (`0x7100adb7c0:L25`). |
| `0x918` | 4 | `int32` | `compute_shader_idx`| Compute shader index in shader archive (`0x7100adb7c0:L35`). |
| `0x91C` | 4 | `int32` | `shader_pass1_idx` | Pass 1 shader index in shader archive (`0x7100adb7c0:L28`). |
| `0x924` | 4 | `int32` | `shader_pass2_idx` | Pass 2 shader index in shader archive (`0x7100adb7c0:L30`). |
| `0x974` | 4 | `float` | `spread_cone_angle` | Initial directional dispersion cone angle in radians. |
| `0x9A4` | 1 | `uint8` | `color0_mode` | Color0 mode: 0 = Constant, 2 = 8-Key Anim, 3 = Random (`0x7100ae19ac:L64`, `0x7100adb8f4:L73`). |
| `0x9A5` | 1 | `uint8` | `color1_mode` | Color1 mode: 0 = Constant, 2 = 8-Key Anim, 3 = Random (`0x7100ae1e08:L64`, `0x7100adb8f4:L83`). |
| `0x9A6` | 1 | `uint8` | `alpha0_mode` | Alpha0 mode: 0 = Constant, 2 = 8-Key Anim, 3 = Random (`0x7100ae19ac:L107`, `0x7100adb8f4:L79`). |
| `0x9A7` | 1 | `uint8` | `alpha1_mode` | Alpha1 mode: 0 = Constant, 2 = 8-Key Anim, 3 = Random (`0x7100ae1e08:L107`, `0x7100adb8f4:L89`). |
| `0x9A8` | 12 | `float[3]` | `color0_const_rgb` | Color0 constant RGB values (`0x7100ae19ac:L39`, `0x7100adb8f4:L74`). |
| `0x9B4` | 4 | `float` | `alpha0_const` | Alpha0 constant alpha scalar (`0x7100ae19ac:L54`, `0x7100adb8f4:L80`). |
| `0x9B8` | 12 | `float[3]` | `color1_const_rgb` | Color1 constant RGB values (`0x7100ae1e08:L39`, `0x7100adb8f4:L84`). |
| `0x9C4` | 4 | `float` | `alpha1_const` | Alpha1 constant alpha scalar (`0x7100ae1e08:L54`, `0x7100adb8f4:L90`). |
| `0x9C8` | 12 | `float[3]` | `particle_scale_xyz`| Base particle dimensions XYZ. |
| `0x9D4` | 12 | `float[3]` | `particle_scale_rnd`| Base particle scale random variance range XYZ. |
| `0x9EC` | 1 | `uint8` | `color_pulse_enable`| 1 = Color pulsing waveform enabled (`0x7100ae1e08:L144`). |
| `0x9ED` | 1 | `uint8` | `scale_pulse_enable`| 1 = Scale pulsing waveform enabled (`0x7100ae1514:L65`). |
| `0x9EF` | 1 | `uint8` | `pulse_mode` | Pulsing waveform mode (high nibble: 0=Cos, 1=Sawtooth, 2=Square; `0x7100ae1e08:L151`, `0x7100adc8a8:L14`). |
| `0x9F8` | 8 | `uint64` | `tex_slot0_guid` | Texture Slot 0 GUID: Albedo / Base Color (`0x7100ae3e54:L14`, `0x7100adb8f4:L58`). |
| `0xA18` | 8 | `uint64` | `tex_slot1_guid` | Texture Slot 1 GUID: Alpha / Dissolve Mask (`0x7100ae3e54:L16`, `0x7100adb8f4:L63`). |
| `0xA38` | 8 | `uint64` | `tex_slot2_guid` | Texture Slot 2 GUID: Distortion / Normal / Flow (`0x7100ae3e54:L18`, `0x7100adb8f4:L68`). |
| `0xA58` | 1 | `uint8` | `tex0_flipbook_type`| Slot 0 flipbook animation type (0=Standard, 4=Flipbook; `0x7100adc8a8:L18`). |
| `0xA59` | 1 | `uint8` | `tex0_uv_scroll` | Slot 0 UV translation scroll enable (`0x7100adb8f4:L114`). |
| `0xA5A` | 1 | `uint8` | `tex0_uv_rotate` | Slot 0 UV rotation animation enable (`0x7100adb8f4:L141`). |
| `0xA5B` | 1 | `uint8` | `tex0_uv_scale` | Slot 0 UV scale animation enable (`0x7100adb8f4:L159`). |
| `0xA5C` | 1 | `uint8` | `tex0_wrap_mode` | Slot 0 texture wrap mode (0=Clamp, 1=Repeat, 2=Mirror; `0x7100adb8f4:L93`). |
| `0xA68`–`0xA77` | 16 | `bytes` | `tex1_sampler_cfg` | Slot 1 sampler config & UV animation modes (`0x7100adb8f4:L101`). |
| `0xA78`–`0xA87` | 16 | `bytes` | `tex2_sampler_cfg` | Slot 2 sampler config & UV animation modes (`0x7100adb8f4:L107`). |
| **`0xA88`** | — | — | **Struct End** | End of fixed data struct (`0xA88` bytes total). |
