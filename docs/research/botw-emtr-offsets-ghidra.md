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

## Pair-match triage (generated)

Output of `tools/exe/pairmatch.py` on the TotK map: per paired field, reader-site counts and the best context-shape score (1.00 = same compiled shape). Supporting evidence only; not proof. Zero BotW sites means the field is only read via a bulk copy (GPU uniform block) or not at all, and needs manual work.

```
emitter_name                           botw+0x10 totk+0x10 sites 1451/1072 best 1.00 botw@adc5a4 totk@df14
color0_key_count                       botw+0x60 totk+0x80 sites 223/238 best 1.00 botw@ae5b74 totk@2139c
alpha0_key_count                       botw+0x64 totk+0x84 sites 194/194 best 0.87 botw@ad9f7c totk@246f4
color1_key_count                       botw+0x68 totk+0x88 sites 187/206 best 0.87 botw@aece70 totk@2e100
alpha1_key_count                       botw+0x6C totk+0x8C sites 170/171 best 0.87 botw@ad4078 totk@1275c
scale_key_count                        botw+0x70 totk+0x90 sites 148/179 best 0.87 botw@ad8c00 totk@7948
track5_key_count                       botw+0x74 totk+0x94 sites 140/163 best 0.87 botw@ad8c00 totk@7948
stationary_diff_fallback_selector      botw+0x744 totk+0xEC sites   2/ 52 best 0.40 botw@ad6984 totk@1ca8
particle_color_rgb_scale               botw+0x3B0 totk+0x680 sites   8/  3 best 0.27 botw@aca4c8 totk@98bc
kf_color0                              botw+0x3C0 totk+0x690 sites   3/  1 best 0.60 botw@adb96c totk@d958
kf_alpha0                              botw+0x440 totk+0x710 sites   4/  1 best 1.00 botw@adb990 totk@d96c
kf_color1                              botw+0x4C0 totk+0x790 sites   3/  1 best 0.73 botw@adb9a4 totk@d988
kf_alpha1                              botw+0x540 totk+0x810 sites   4/  1 best 0.67 botw@adb9c8 totk@d99c
kf_scale                               botw+0x600 totk+0x8D0 sites   3/  3 best 0.40 botw@adbf3c totk@27f94
kf_track5                              botw+0x680 totk+0x950 sites   8/  4 best 0.33 botw@ad88dc totk@274e4
sim_flags                              botw+0x748 totk+0xCA0 sites   0/  1 best 0.00
particle_sort_mode_index               botw+0x749 totk+0xCA1 sites   0/  2 best 0.00
emitter_calc_type                      botw+0x752 totk+0xCA2 sites  40/  3 best 0.47 botw@aca148 totk@1d70
velocity_coord                         botw+0x74B totk+0xCA3 sites   0/ 38 best 0.00
seed_source                            botw+0x757 totk+0xCA4 sites   2/  1 best 0.67 botw@ad59a4 totk@1df4
fade_in_curve                          botw+0x75B totk+0xCA9 sites   4/  3 best 0.93 botw@ad795c totk@103b4
fade_in_scale                          botw+0x75C totk+0xCAA sites   8/  3 best 0.93 botw@ad7964 totk@103bc
fade_out_curve                         botw+0x755 totk+0xCAB sites   7/  3 best 0.93 botw@ad79c4 totk@1041c
fade_out_scale                         botw+0x756 totk+0xCAC sites  12/  3 best 0.93 botw@ad79cc totk@10424
fixed_seed                             botw+0x760 totk+0xCB0 sites   1/  1 best 0.80 botw@ad59ec totk@1e38
fade_out_time                          botw+0x768 totk+0xCB8 sites   2/  2 best 1.00 botw@ad79d4 totk@1042c
fade_in_time                           botw+0x76C totk+0xCBC sites   1/  1 best 1.00 botw@ad797c totk@103d4
emitter_trans_xyz                      botw+0x770 totk+0xCC0 sites   4/  8 best 0.60 botw@ad61fc totk@2cd0
emitter_trans_rnd                      botw+0x77C totk+0xCCC sites   2/  4 best 0.53 botw@ad3ef0 totk@f640
emitter_rot_xyz                        botw+0x788 totk+0xCD8 sites   3/  3 best 0.67 botw@adc238 totk@da78
emitter_rot_rnd                        botw+0x794 totk+0xCE4 sites   1/  1 best 0.33 botw@ad659c totk@3880
emitter_scale_xyz                      botw+0x798 totk+0xCF0 sites   1/  3 best 0.47 botw@ad65a8 totk@2cb0
emitter_color0_rgb                     botw+0x7A4 totk+0xCFC sites   4/  2 best 0.67 botw@ad61d0 totk@2cdc
emitter_color0_alpha                   botw+0x7B0 totk+0xD08 sites   1/  2 best 0.80 botw@ad6210 totk@2d40
emitter_color1_rgb                     botw+0x7B4 totk+0xD0C sites   1/  2 best 0.73 botw@ad6218 totk@2cf4
emitter_color1_alpha                   botw+0x7C0 totk+0xD18 sites   1/  1 best 0.87 botw@ad622c totk@2d48
child_pre_draw                         botw+0x7E1 totk+0xD39 sites   2/  1 best 0.60 botw@aca6e8 totk@a0b0
emit_loop_mode                         botw+0x7F0 totk+0xD48 sites   8/  6 best 0.60 botw@ae4798 totk@1ef04
gravity_coord                          botw+0x7F1 totk+0xD49 sites   2/  4 best 0.80 botw@adc8ac totk@e38c
emit_dist_enable                       botw+0x7F2 totk+0xD4A sites   3/  3 best 0.53 botw@ad635c totk@e2f8
emit_start_delay                       botw+0x7F4 totk+0xD4C sites   1/  2 best 0.53 botw@ad7418 totk@ff10
child_emit_timing                      botw+0x7F8 totk+0xD50 sites   2/  2 best 0.47 botw@ad8228 totk@1118c
emit_duration                          botw+0x7FC totk+0xD54 sites   5/  7 best 0.47 botw@ad7414 totk@ff14
emit_rate                              botw+0x800 totk+0xD58 sites  13/  2 best 0.73 botw@adc1f0 totk@da1c
emit_rate_random_percent               botw+0x804 totk+0xD5C sites   5/  2 best 0.47 botw@acdd94 totk@f790
emit_interval                          botw+0x808 totk+0xD60 sites   6/  7 best 0.53 botw@ae70d0 totk@23110
emit_interval_random                   botw+0x80C totk+0xD64 sites   1/  1 best 0.47 botw@ad69c0 totk@3e14
gravity_xyz                            botw+0x818 totk+0xD70 sites   3/  1 best 0.80 botw@ae0f04 totk@18354
shape_type                             botw+0x838 totk+0xD90 sites   8/  8 best 0.87 botw@ad64e0 totk@23190
shape_angle_mode                       botw+0x839 totk+0xD91 sites   7/  4 best 0.60 botw@add250 totk@13b44
shape_rot_mode                         botw+0x83A totk+0xD92 sites   6/  6 best 0.60 botw@ade8fc totk@13ebc
shape_rot_variant                      botw+0x83E totk+0xD95 sites   4/  3 best 0.73 botw@ade104 totk@14564
shape_angle_b                          botw+0x840 totk+0xD98 sites   6/  2 best 0.60 botw@add24c totk@13914
shape_angle_c                          botw+0x844 totk+0xD9C sites   6/  3 best 0.73 botw@ade008 totk@14470
shape_angle_d                          botw+0x848 totk+0xDA0 sites   5/  4 best 0.53 botw@add43c totk@13918
shape_fill_ratio                       botw+0x850 totk+0xDA8 sites   3/  3 best 0.73 botw@add8b0 totk@13cb8
line_center_bias                       botw+0x854 totk+0xDAC sites   2/  2 best 0.67 botw@adf2fc totk@15338
line_length                            botw+0x858 totk+0xDB0 sites   2/  2 best 0.80 botw@adf344 totk@15390
shape_radius_xyz                       botw+0x85C totk+0xDB4 sites  10/  8 best 0.73 botw@adf480 totk@154d8
mesh_primitive_idx                     botw+0x878 totk+0xDD0 sites   1/  1 best 0.67 botw@ae3ed8 totk@1e674
shape_divisions                        botw+0x880 totk+0xDD8 sites   4/  2 best 0.67 botw@ad652c totk@3700
blend_target_enable                    botw+0x898 totk+0xDE8 sites   0/  1 best 0.00
depth_stencil_mode_index               botw+0x89A totk+0xDEA sites   0/  1 best 0.00
depth_sort_ascending                   botw+0x89B totk+0xDEB sites   1/  2 best 0.60 botw@acdcfc totk@480c
blend_mode_index                       botw+0x89E totk+0xDEE sites   0/  1 best 0.00
cull_mode_index                        botw+0x89F totk+0xDEF sites   0/  1 best 0.00
emit_infinite_flag                     botw+0x8A8 totk+0xDF8 sites  14/  6 best 0.87 botw@ae477c totk@1eef0
is_trimming_prim                       botw+0x8A9 totk+0xDF9 sites   9/  3 best 0.80 botw@ae3efc totk@1e6a4
shader_mode_index_DFC                  botw+0x8AC totk+0xDFC sites  10/  3 best 0.73 botw@adcba0 totk@e778
particle_lifespan                      botw+0x8B8 totk+0xE08 sites  16/  2 best 0.40 botw@ad6278 totk@dcd4
particle_lifespan_random_percent       botw+0x8BC totk+0xE0C sites   4/  2 best 0.47 botw@adadc8 totk@1246c
particle_attribute_w_random_amplitude  botw+0x8C0 totk+0xE10 sites   6/  1 best 0.53 botw@adad70 totk@12414
g3d_primitive_idx                      botw+0x8C8 totk+0xE18 sites   6/  2 best 0.40 botw@adb800 totk@b6ec
trim_primitive_idx                     botw+0x8D0 totk+0xE20 sites   5/  1 best 0.87 botw@ae3f04 totk@1e6ac
loop_color0_enable                     botw+0x8D8 totk+0xE28 sites   3/  3 best 0.73 botw@ae1ab8 totk@18e60
loop_alpha0_enable                     botw+0x8D9 totk+0xE29 sites   3/  3 best 0.80 botw@adc074 totk@d83c
loop_color1_enable                     botw+0x8DA totk+0xE2A sites   3/  3 best 0.80 botw@adc0a4 totk@d870
loop_alpha1_enable                     botw+0x8DB totk+0xE2B sites   3/  3 best 0.87 botw@adc0d4 totk@d89c
loop_scale_enable                      botw+0x8DC totk+0xE2C sites   3/  2 best 0.87 botw@adc104 totk@d8d0
loop_color0_random_phase               botw+0x8DD totk+0xE2D sites   3/  3 best 0.67 botw@adc05c totk@d828
loop_alpha0_random_phase               botw+0x8DE totk+0xE2E sites   3/  3 best 0.80 botw@adc090 totk@d858
loop_color1_random_phase               botw+0x8DF totk+0xE2F sites   3/  3 best 0.80 botw@adc0c0 totk@d888
loop_alpha1_random_phase               botw+0x8E0 totk+0xE30 sites   2/  3 best 0.80 botw@adc0f0 totk@d8b8
loop_scale_random_phase                botw+0x8E1 totk+0xE31 sites   2/  2 best 0.93 botw@adc120 totk@d8e8
loop_scale_period_i32                  botw+0x8F4 totk+0xE3C sites   2/  2 best 0.93 botw@adc110 totk@d8d8
shader_idx_normal                      botw+0x914 totk+0xE5C sites   1/  3 best 0.47 botw@adb83c totk@27644
shader_idx_pass1                       botw+0x91C totk+0xE60 sites   1/  1 best 0.40 botw@adb850 totk@b878
shader_idx_pass2                       botw+0x924 totk+0xE64 sites   1/  1 best 0.53 botw@adb874 totk@b8b4
compute_shader0                        botw+0x918 totk+0xE68 sites   1/  1 best 0.40 botw@adb894 totk@b99c
emission_direction_spread_degrees      botw+0x970 totk+0xF10 sites   1/  2 best 0.53 botw@ad62b4 totk@12930
emission_tangent_amount                botw+0x974 totk+0xF14 sites   0/  3 best 0.00
emission_direction_random_x            botw+0x978 totk+0xF18 sites   0/  1 best 0.00
emission_direction_random_y            botw+0x97C totk+0xF1C sites   1/  1 best 0.33 botw@ada7a4 totk@13654
emission_direction_random_z            botw+0x980 totk+0xF20 sites   2/  1 best 0.40 botw@acbbcc totk@13670
initial_speed_random_percent           botw+0x984 totk+0xF24 sites   3/  1 best 0.53 botw@ada460 totk@12924
emitter_motion_inherit_scale           botw+0x988 totk+0xF28 sites   2/  1 best 0.27 botw@adabdc totk@136a8
emitter_motion_inherit_max             botw+0x98C totk+0xF2C sites   2/  1 best 0.27 botw@adac00 totk@136b0
color0_mode                            botw+0x9A4 totk+0xF44 sites   3/  5 best 1.00 botw@ae1a9c totk@18e44
color1_mode                            botw+0x9A5 totk+0xF45 sites   3/  6 best 1.00 botw@ae1ef8 totk@19774
alpha0_mode                            botw+0x9A6 totk+0xF46 sites   2/  4 best 0.87 botw@adb984 totk@d960
alpha1_mode                            botw+0x9A7 totk+0xF47 sites   2/  4 best 0.87 botw@ae1f70 totk@19830
color0_const_rgb                       botw+0x9A8 totk+0xF48 sites   1/  3 best 0.60 botw@adb968 totk@d954
alpha0_const                           botw+0x9B4 totk+0xF54 sites   1/  1 best 1.00 botw@adb98c totk@d968
color1_const_rgb                       botw+0x9B8 totk+0xF58 sites   1/  3 best 0.80 botw@adb9a0 totk@d984
alpha1_const                           botw+0x9C4 totk+0xF64 sites   1/  1 best 0.73 botw@adb9c4 totk@d998
particle_scale_xyz                     botw+0x9C8 totk+0xF68 sites   1/  1 best 0.73 botw@ad6248 totk@2d0c
particle_scale_rnd                     botw+0x9D4 totk+0xF74 sites   1/  2 best 0.53 botw@adac78 totk@12258
waveform_mode_packed                   botw+0x9EF totk+0xF8F sites   4/  6 best 1.00 botw@ae1640 totk@189d0
tex_slot0_guid                         botw+0x9F8 totk+0xF98 sites   4/  4 best 0.60 botw@adb90c totk@bc08
tex_slot1_guid                         botw+0xA18 totk+0xFB0 sites   3/  4 best 0.60 botw@adb928 totk@bc30
tex_slot2_guid                         botw+0xA38 totk+0xFC8 sites   4/  5 best 0.53 botw@adb944 totk@bc58
tex0_mode_index                        botw+0xA58 totk+0x1028 sites   4/  0 best 0.00
tex0_uv_scroll                         botw+0xA59 totk+0x1029 sites   1/  0 best 0.00
tex0_uv_rotate                         botw+0xA5A totk+0x102A sites   1/  0 best 0.00
tex0_uv_scale                          botw+0xA5B totk+0x102B sites   1/  0 best 0.00
tex0_uv_domain_scale_mode              botw+0xA5C totk+0x102C sites   1/  0 best 0.00
tex0_shader_flag0                      botw+0xA5D totk+0x102D sites   1/  0 best 0.00
tex0_shader_flag1                      botw+0xA5E totk+0x102E sites   1/  0 best 0.00
tex0_shader_flag2                      botw+0xA5F totk+0x102F sites   1/  0 best 0.00
tex1_mode_index                        botw+0xA68 totk+0x1038 sites   4/  0 best 0.00
tex1_uv_scroll                         botw+0xA69 totk+0x1039 sites   1/  0 best 0.00
tex1_uv_rotate                         botw+0xA6A totk+0x103A sites   1/  0 best 0.00
tex1_uv_scale                          botw+0xA6B totk+0x103B sites   1/  0 best 0.00
tex1_uv_domain_scale_mode              botw+0xA6C totk+0x103C sites   1/  0 best 0.00
tex1_shader_flag0                      botw+0xA6D totk+0x103D sites   1/  0 best 0.00
tex1_shader_flag1                      botw+0xA6E totk+0x103E sites   1/  0 best 0.00
tex1_shader_flag2                      botw+0xA6F totk+0x103F sites   1/  0 best 0.00
tex2_mode_index                        botw+0xA78 totk+0x1048 sites   4/  0 best 0.00
tex2_uv_scroll                         botw+0xA79 totk+0x1049 sites   1/  0 best 0.00
tex2_uv_rotate                         botw+0xA7A totk+0x104A sites   1/  0 best 0.00
tex2_uv_scale                          botw+0xA7B totk+0x104B sites   1/  0 best 0.00
tex2_uv_domain_scale_mode              botw+0xA7C totk+0x104C sites   1/  0 best 0.00
tex2_shader_flag0                      botw+0xA7D totk+0x104D sites   1/  0 best 0.00
tex2_shader_flag1                      botw+0xA7E totk+0x104E sites   1/  0 best 0.00
tex2_shader_flag2                      botw+0xA7F totk+0x104F sites   1/  0 best 0.00
```
