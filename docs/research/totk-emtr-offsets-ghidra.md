# TotK `EMTR` serialized-field evidence

This note records only behavior directly observed in the TotK executable with
Ghidra.  Offsets are relative to the serialized `EMTR` node's data start (the
same pointer that `nn::vfx2::Resource::InitializeEmitterSetResource` stores as
the emitter resource pointer).  The fixed `ResEmitter` body begins at
`EMTR.data + 0x70`, but these fields are referenced from the node-data base.

The addresses below are TotK `main` symbols in the Ghidra project.  They are
evidence anchors, not a claim that the executable ABI is stable across builds.

## Confirmed mappings

| Offset | Type observed | Executable behavior | Evidence |
| ---: | --- | --- | --- |
| `0xCA4` | byte | Selects the random-seed source in `Emitter::Initialize`: cases `0`, `1`, and `2` take the global, emitter-set, and fixed-seed paths respectively. | `nn::vfx2::Emitter::Initialize` `0x7100001900` |
| `0xCB0` | 32-bit integer | Read on the fixed-seed path selected by `0xCA4 == 2`. | `Emitter::Initialize` `0x7100001900` |
| `0xCA0` | byte | Copied into runtime emitter flags at bit 13 during initialization. | `Emitter::Initialize` `0x7100001900` |
| `0xCA2` | byte | Three-way mode value (`0`, `1`, or `2`) copied into distinct runtime flag bits 14–16. | `Emitter::Initialize` `0x7100001900` |
| `0xCA9` | byte | Fade-in alpha curve selector. `0` disables the curve; `1`, `2`, and `3` apply linear, squared, and fourth-power progress respectively. | `Emitter::CalculateAlpha1` `0x7100004048`; `Emitter::Initialize` `0x7100001900` |
| `0xCAA` | byte | Scale fade-in flag: when nonzero, `GetScaleRate` interpolates from `0xD28` toward `1.0` using runtime fade-in progress. | `Emitter::GetScaleRate` `0x7100004158` |
| `0xCAB` | byte | Fade-out alpha curve selector. `0` disables the curve; `1`, `2`, and `3` apply linear, squared, and fourth-power progress respectively. It is also tested with `0xCAC` for fade-out/death handling. | `Emitter::CalculateAlpha` `0x71000040A8`; `Emitter::Calculate` `0x710000FEBc` |
| `0xCAC` | byte | Scale fade-out flag: when nonzero, `GetScaleRate` interpolates from `0xD2C` toward `1.0` using runtime fade-out progress. | `Emitter::GetScaleRate` `0x7100004158` |
| `0xCB8` | 32-bit integer | Fade-out timing divisor in `Emitter::Calculate`; non-positive values disable that incremental fade step. | `Emitter::Calculate` `0x710000FEBc` |
| `0xCBC` | 32-bit integer | Fade-in timing divisor in `Emitter::Calculate`; non-positive values disable that incremental fade step. | `Emitter::Calculate` `0x710000FEBc` |
| `0xD28` | float | Scale fade-in starting value used by `GetScaleRate`. | `Emitter::GetScaleRate` `0x7100004158` |
| `0xD2C` | float | Scale fade-out starting value used by `GetScaleRate`. | `Emitter::GetScaleRate` `0x7100004158` |
| `0xD90` | byte | Emission-dispatch selector. `Emitter::InitializeParticle` indexes the global emit-function table with it; shape helpers and divided-emission checks also branch on it. | `Emitter::InitializeParticle` `0x7100012188`; `Emitter::IsEmitDividedParticle` `0x710000420C`; `EmitterCalculator::CalculateEmit*` |
| `0xD91` | byte | Selects a time-varying versus static angle path in circle/sphere emission. | `EmitterCalculator::CalculateEmitCircle` `0x7100013908`; `CalculateEmitSphere` `0x7100013E18` |
| `0xD92` | byte | Selects the alternate circle/sphere distribution path; the code compares it against `1` and changes angle/vector generation. | `CalculateEmitCircle` `0x7100013908`; `CalculateEmitSphere` `0x7100013E18` |
| `0xD95` | byte | Chooses one of six basis vectors in the sphere path when the alternate distribution is active. | `CalculateEmitSphere` `0x7100013E18` |
| `0xD98` | float | Angle/range input used by circle and sphere emission. | `CalculateEmitCircle` `0x7100013908`; `CalculateEmitSphere` `0x7100013E18` |
| `0xD9C` | float | Alternate angle/range input used by the sphere path. | `CalculateEmitSphere` `0x7100013E18` |
| `0xDA0` | float | Angle input; combined with `0xD98` and optionally advanced as a function of time. | `CalculateEmitCircle` `0x7100013908`; `CalculateEmitSphere` `0x7100013E18` |
| `0xDB4`/`0xDB8`/`0xDBC` | float ×3 | Emission vector scale components. Box, circle, and sphere helpers multiply their generated coordinates by these three values. | `CalculateEmitBox` `0x7100014F2C`; `CalculateEmitCircle` `0x7100013908`; `CalculateEmitSphere` `0x7100013E18` |
| `0xDCC` | 32-bit integer | Emission distribution/control mode. `0` enables divided-emission checks; `1` and `2` select distinct primitive-indexing paths in `CalculateEmitPrimitive`. | `Emitter::IsEmitDividedParticle` `0x710000420C`; `CalculateEmitPrimitive` `0x71000155FC`; `EmitterCalculator::Emit` `0x71000127EC` |
| `0xDD8`/`0xDDC` and `0xDE0`/`0xDE4` | 32-bit pairs | Count/randomization inputs used by `EmitterCalculator::Emit` for two `0xD90` selector values. The executable proves their arithmetic role, but not Nintendo-facing field names. | `EmitterCalculator::Emit` `0x71000127EC` |
| `0xD54` | 32-bit integer | Added to the emission timer/base value in `TryEmitParticle`. | `EmitterCalculator::TryEmitParticle` `0x710000F69C` |
| `0xD58` | float | Used as the emission amount/rate input when divided emission is active. | `TryEmitParticle` `0x710000F69C`; `EmitterCalculator::Emit` `0x71000127EC` |
| `0xD5C` | byte | Converted to a percentage-like random factor in emission-count calculations. | `TryEmitParticle` `0x710000F69C` |
| `0xD60` | 32-bit integer | Used as a divisor after adding `1.0` when calculating the required particle-assignment count. | `Emitter::CalculateRequiredParticleAsignmentCount` `0x71000035F4`; `Emitter::Initialize` `0x7100001900` |
| `0xD7C` | float | Manual-emission spacing value: used as the divisor for repeated manual emits and subtracted between emitted particles. | `EmitterCalculator::TryEmitParticle` `0x710000F69C` |
| `0xD80`/`0xD84`/`0xD88` | float ×3 | Manual-emission coefficients multiplied by frame time and compared against the current movement/distance magnitude to determine manual emission amount. | `EmitterCalculator::TryEmitParticle` `0x710000F69C` |
| `0xD8C` | 32-bit integer | Direct required-assignment count used by the manual/special emission branch. | `Emitter::CalculateRequiredParticleAsignmentCount` `0x71000035F4` |
| `0xD3C` | byte | Copied into runtime flag bit 17 and tested when creating/initializing child emitters. | `Emitter::Initialize` `0x7100001900`; `Emitter::InitializeParticle` `0x7100012188` |
| `0xD48`/`0xD4A` | byte ×2 | Copied into runtime flag bits 20 and 21 during initialization. Their higher-level meanings are not established by the traced paths. | `Emitter::Initialize` `0x7100001900` |
| `0xDD0` | 32-bit value | Primitive index used by `Resource::InitializeEmitterGraphicsResource` to resolve the emitter's G3D primitive. | `Resource::InitializeEmitterGraphicsResource` `0x710001E650` |
| `0xDF9` | byte | Trim-primitive enable flag tested before the optional trim primitive lookup. | `Resource::InitializeEmitterGraphicsResource` `0x710001E650` |
| `0xE18` | 32-bit value | Primary primitive index used by `EmitterResource::Setup` to resolve the emitter's G3D primitive and update shader/vertex state. | `EmitterResource::Setup` `0x710000B694` |
| `0xE20` | 32-bit value | Optional trim primitive index, read when `0xDF9` is set. | `Resource::InitializeEmitterGraphicsResource` `0x710001E650` |
| `0xCA3` | byte | Selects particle-update behavior. `CalculateParticle` tests both zero and one and takes different data sources for the particle vector calculation. | `Emitter::CalculateParticle` `0x7100010968`; `Emitter::ResourceUpdate` `0x7100002000` |
| `0xD49` | byte | Gates the alternate particle-vector path in `CalculateParticle`; when set, the code may source vector values from per-particle/parent data instead of the emitter's current vector. | `Emitter::CalculateParticle` `0x7100010968` |
| `0xD50` | 32-bit integer | Converted to a percentage (`value / 100`) and compared with the current particle time/age value during particle updates. | `Emitter::CalculateParticle` `0x7100010968` |
| `0xD54` | 32-bit integer | Added to the `0xD50`-derived threshold when the corresponding runtime flag is active; the same field is also used by emission timing. | `Emitter::CalculateParticle` `0x7100010968`; `EmitterCalculator::TryEmitParticle` `0x710000F69C` |

`Emitter::CalculateRequiredParticleAsignmentCount` also proves that the value
derived from `0xD54` and `0xD60` participates in the allocation/count estimate,
and that the `0xDD8` or `0xDE0` integer is used as a multiplier for selector
values `0x02` and `0x0D` when `0xDCC == 0`.

## Serialized values mirrored into runtime state

`nn::vfx2::Emitter::ResourceUpdate` copies these serialized values into the
runtime `Emitter` object before particle calculation.  The copy itself is
confirmed even where the final authoring meaning is not:

| Serialized source | Runtime destination | Shape observed | Evidence |
| ---: | ---: | --- | --- |
| `0xCC0..0xCCB` | `Emitter + 0x3F0..0x3FB` | 12-byte value | `Emitter::ResourceUpdate` `0x7100002000` |
| `0xCD8..0xCE3` | `Emitter + 0x3E4..0x3EF` | 12-byte value | same |
| `0xCF0..0xCFB` | `Emitter + 0x3D8..0x3E3` | 12-byte value | same |
| `0xCC8` | `Emitter + 0x3F8` | 32-bit tail of the first 12-byte value | same |
| `0xCFC`, `0xD00`, `0xD04` | `Emitter + 0x3FC`, `0x400`, `0x404` | three 32-bit values | same |
| `0xD08`, `0xD0C`, `0xD10`, `0xD14` | `Emitter + 0x42C`, `0x408`, `0x40C`, `0x410` | four 32-bit values | same |
| `0xD18` | `Emitter + 0x438` | 32-bit value | same |
| `0xDC0..0xDCB` | `Emitter + 0x468..0x473` | 12-byte value | same |
| `0xEF8..0xF00` | `Emitter + 0x444`, `0x450` | two 32-bit values | same |
| `0xD6C` | `Emitter + 0x474` | 32-bit value | same |

`Emitter::CalculateParticle` then reads the `0x3D8`, `0x3E4`, and `0x3F0`
runtime values while constructing transformed particle vectors.  That proves
these serialized ranges are part of the static particle-property input, but it
does not by itself prove the original authoring names.  They remain raw fields
in PtclSharp for now.

The table intentionally uses behavioral names such as “selector”, “timing
divisor”, and “vector scale” where the executable does not expose a definitive
serialized member name.  Those are safe descriptions of what the code does,
not guesses about authoring terminology.

## Confirmed non-field layout facts used by this map

- `Resource::InitializeEmitterSetResource` reads the ESET emitter count from
  `ESET.data + 0x70` as an unsigned 16-bit value.
- It stores each EMTR's serialized pointer at `EMTR.node + dataOffset`, and
  the static UBO/body pointer at `serialized + 0x70`.
- `EmitterResource::UpdateParams` resolves six texture-sampler records at
  `0xF98`, `0xFB0`, `0xFC8`, `0xFE0`, `0xFF8`, and `0x1010`.

## Deliberately not mapped yet

The following still need a direct offset-to-operation trace before they should
become named library properties:

- particle-life, emission interval, and emission-rate fields;
- remaining render/material fields around the now-confirmed primitive indices
  at `0xDD0`, `0xE18`, and optional trim pair `0xDF9`/`0xE20`;
- the remaining volume/shape fields around `0xD60`, `0xD7C`–`0xD88`, and
  `0xDC0`–`0xDC8`;
- static color, scale, rotation, and velocity values;
- animation payload layouts inside `EA*` chunks;
- the exact authoring names and enum labels for the `0xD90`/`0xDCC` modes.

Until those are traced, PtclSharp should preserve the bytes and expose no
strongly named semantic property for them.
