# Finding what drives an effect in BotW (playbook)

Goal: for an effect you ported with `PtclConvert`, find out which parts of its behaviour come from **data** (the effect file,
the ELink2 database, actor parameter files) and which come from **game code**, so only the latter needs an executable mod.
Worked example: the Dark Beast Ganon beam (`GanonBeastBeam`). Addresses are for the BotW 1.6.0 Switch executable
(`0x71...` image addresses); TotK 1.2.1 has its own.

## 0. Vocabulary

| Layer | Where it lives | Can be ported as data? |
|---|---|---|
| Effect file (`Effect/<Name>.sesetlist`, TotK `.esetb.byml.zs`) | emitter sets, emitters, textures, models, shaders | yes (`PtclConvert`) |
| Custom shader parameters (`CSDP` chunk) | per emitter, raw uniform block (52 or 100 bytes in BotW) | only if the donor shader reads the same layout |
| Custom action data (`CADP` chunk) | per emitter, opaque to the effect library; read by game code | no, needs the game-side consumer |
| ELink2 database (`Pack/Bootup.pack` > `ELink2/ELink2DB.sbelnk`, magic `XLNK`) | effect *user* (actor effect name) > assets > which emitter set, bone, parameters | yes, it is data |
| Actor files (`Actor/Pack/<Actor>.sbactorpack`: ActorLink, AIProgram, GeneralParamList...) | which AI/actions an actor runs | yes, but the actions are game code |
| Game code (`uking::action::*`, `uking::ai::*`, `Effect::*`, `gsys::*`) | gameplay, effect spawning, per-frame data feeding | only by exe patch |

## 1. Method

1. **Name the pieces.** The actor name, the effect file name, the emitter set names and the ELink user name are usually the same
   string family. Search the effect file's set names (`PtclCorpus` or the converter log) in the executable (`list_strings`) and
   in `ELink2DB.sbelnk` (strings are stored plainly: `GanonBeastBeam`, `GanonBeast_Beam`, `GanonBeast_BeamHit`,
   `GanonBeast_Beam_BarrierHit`).
2. **Unpack the actor.** `Actor/Pack/<Actor>.sbactorpack` is Yaz0 + SARC. For the beam: `ActorLink` (names the AI and the
   `LineBeam` tag), `AIProgram` (lists the actions: `SimpleLineBeam`, `GanonBeastBeamMove`, `AscendingCurrent_GanonBeastBeam`),
   `GeneralParamList`, `Physics`. There is **no** effect link in the actor itself.
3. **Read the actions.** Class names in the AIProgram map to `uking::action::<Name>` / `uking::ai::<Name>` in Ghidra
   (`search_functions_by_name`). Decompile `calc_` (per frame), `enter_`/`leave_` and `loadParams` (the static parameters it reads
   by name tell you what the designers can tune).
4. **Find the effect side.** Effects are spawned through ELink2 (`Effect::*`, `eft::Effect::*`); the custom-shader hooks are the
   callers of `gsys::CSUtil::GetUboTempBuffer` (`0x7100c50d5c`). Classify each caller: engine-wide data (lights, fog, shadows) or
   effect-specific data.
5. **Decide per piece:** data (port it), generic engine data (nothing to do), effect-specific code (patch).

### Reading ELink2 databases with xlink2

[dt-12345/xlink2](https://github.com/dt-12345/xlink2) (C++26, build with gcc 16: `cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release
-DCMAKE_CXX_COMPILER=g++`, after cloning `fmtlib/fmt` into `lib/fmt`) turns a database into readable text:

```sh
xlink -i ELink2DB.bin -o botw_elink.txt -g UKing      # BotW: Pack/Bootup.pack > ELink2/ELink2DB.sbelnk, Yaz0-decompressed
xlink -i elink2.bin   -o totk_elink.txt               # TotK: ELink2/elink2.Product.110.belnk.zs, Zstandard with the ZsDic dictionary
```

A **user** (named like the actor effect, for example `GanonBeastBeam`) has *asset call tables* keyed by the name game code or an
`AlwaysTriggers` entry requests; each ends in an `Asset` whose `RuntimeAssetName` is the **emitter set name** inside the effect file,
plus per-call overrides (`Scale`, `valDampDist`, `valDrawPriority`...). This is where set names come from.

## 2. Findings for the Dark Beast Ganon beam (BotW)

- The beam is an **actor** (`GanonBeastBeam`) whose AI runs `uking::action::GanonBeastBeamMove` (`0x7100175d1c`), which derives from
  `uking::action::SimpleLineBeam` (`0x7100255234`). Both are **gameplay**: they follow a line between two positions, query the
  terrain (`Terrain__getSomething`), place "rest" actors along the beam (`RestActor` = `AscendingCurrent_GanonBeastBeam`, parameters
  `RestDistTime`, `RestDistTimeAdd`, `RestNumMax`, `RestDistLimit`, `RestDistMinLimit`, `RestDistInterval`), and send the line as an
  actor message under a spinlock (`FUN_710070dcc0`; the receiving AI is `uking::ai::SimpleLineBeam::handleMessage_`,
  `0x710056f35c`, message ids `0x8000038` and `0x8000039`). Nothing in them touches shaders or emitters directly.
- The beam's **effect** is data. BotW user `GanonBeastBeam` (ELink2 text line ~103927) has three call tables, none with parameters
  except the last: `Tail` -> set `GanonBeast_Beam`, `Barrier` -> `GanonBeast_Beam_BarrierHit`, `Beam_Top` -> `GanonBeast_BeamHit`
  (`valDampDist = 1000.0`, `valDrawPriority = 129`, all `BitFlag = 0b10`). No scale, no length, no per-frame property: the beam
  has no effect parameters at all beyond the actor's own transform. The names ("Tail", "Beam_Top") suggest a moving head that
  leaves a stripe trail, which the actor's movement (`GanonBeastBeamMove`) produces.
- TotK user `PlayerBeam` (the Master Sword beam) is the analogue: an `AlwaysTriggers` entry starts call table `Body` -> set
  `Obj_MasterBeam` (`Scale = 3.0`, `valAfterFadeSpeed = 0.9`), plus `MasterBeamDisappear` -> `Obj_MasterBeam_Disappear` (`Scale =
  4.0`, `valCombo = 2`, `valPower = 0.05`, `valDampDist = 100`) and `MasterBeamHit` -> `MasterBeam_Disappear`. Role mapping used
  for the test file: Tail -> Body, Barrier -> MasterBeamDisappear, Beam_Top -> MasterBeamHit.
- So replacing `PlayerBeam` with the converted effect gives the Ganon beast beam visuals riding on the Master Sword beam
  projectile. What is **not** ported is the BotW actor behaviour: the beam as a line from the Guardian-style source to the hit
  point with rest actors, terrain following and damage along it.
- The three engine callbacks that fill the custom-shader uniform buffer (`FUN_71011b9484`, `FUN_71012676bc`, `FUN_71011b37e4`) are
  the generic `RenderStateSet_Basic` / `Basic2` callbacks: they copy lights, shadows, fog and view data into a 0x240 / 0x60 / 0x270
  byte buffer for **every** effect, selected by flag bits of the emitter's shader (`EmitterResource + 0x930`). They are not
  beam specific, so porting them is not needed.
- The beam emitters that use custom shaders (`custom_shader_index` 3 and 4) get their beam-specific values from the emitter's own
  `CSDP` block, which is **static data in the effect file**. If that holds for all of them, the beam needs no executable change for
  the visuals, only a donor shader that reads the same block (see `docs/conversion.md`, "Shader donors").

### 2.1 How the beam works in BotW code (traced)

The earlier guess (a moving projectile with a stripe trail) was wrong. The Dark Beast beam is a **line beam**: a damage capsule
between a muzzle and a hit point, with an effect instance that is re-oriented and stretched every frame.

**Owner side: `uking::action::ForkGanonBeastBeamShoot`** (`0x71001540e8` calc, `0x7100154094` enter, `0x7100154170` params).
A parallel ("Fork") action of `Enemy_GanonBeast`. Static params: `BeamRange`, `BeamBoneName`, `BeamActorKey`, `BeamActorName`,
`MuzzleOffset` (vec3), `BeamDir` (vec3). The beam actor is one of the owner's *actor parts* (`FUN_71006f3934` returns the parts
actor for `BeamActorKey`). `enter_` writes `BeamDir` into the beam actor (`FUN_71002c7d1c`: under its critical section, to
`beam + 0xcc8..0xcd0`) and copies `MuzzleOffset`. `calc_` watches an animation-sequence event (`ASList::x`, list 3): on the rising
edge it sets the beam actor's properties from the owner's matrix (`setProperties`) and sends message **`0x8000038`** (start); on
the falling edge it sends **`0x8000039`** (stop). The messages go through `FUN_710070de10`.

**Beam actor `GanonBeastBeam`** (profile `LineBeam`, class chain `LineBeam` > `BeamBase` > `DynamicActor`):
- `Physics`: one keyframed capsule rigid body `BeamCutting` (radius 0.1, shape `(0,0,0)-(0,1,0)`) with an `AttackCommon` contact
  point; `GeneralParamList`: `Attack.Power = 72`, `Beam.BeamLevel = 255`.
- `AIProgram`: root AI `SimpleLineBeam` (children: wait = `DummyAction`, attack = `GanonBeastBeamMove` with `RestActor =
  AscendingCurrent_GanonBeastBeam`, `RestDistLimit = RestDistMinLimit = 125`, `RestDistInterval = 1`, `RestDistTime = 600`,
  `RestDistTimeAdd = 15`, `RestNumMax = 1`, `IsGuardPierces = true`). `uking::ai::SimpleLineBeam::handleMessage_` (`0x710056f35c`)
  turns the start/stop messages into flag bits (2 = beam on, 4 = beam off); `calc_` (`0x710056f290`) switches the child
  action when the flag changes. `GanonBeastBeamMove::calc_` (`0x7100175d1c`) spawns and tracks the "rest" actors: it walks
  the beam line in 5 m steps for ground following and places an updraft actor every `RestDistInterval`.
- `BeamBase::m164` (`0x7100003014`, start): fades the old `Tail` effect handle, **emits the ELink key `"Tail"`** on the beam
  actor (handle at `+0xc48`), stores the muzzle position at `+0xc68..0xc70`.
- `BeamBase::m163` (`0x7100002ef8`, per frame): builds the beam's world matrix from its parent (owner) actor: a bone matrix
  (`+0xc10/+0xc12` = bone indices) or the parent's matrix, plus a local offset (`+0xc18..0xc20`), via `FUN_7100003494`; then calls
  `BeamBase::reflectMaybe` (`0x7100003140`).
- `reflectMaybe` **drives the `Tail` effect every frame**: it takes beam direction = end - start, builds an orientation matrix
  and writes it into the effect handle (matrix at handle `+0xc0..+0xe8`, scale at `+0xb4..+0xbc` = (`beam+0xc80`, 1.0, `beam+0xc80`)
  where `+0xc80` is the beam width, matrix-dirty flag `|= 8`), and writes two floats at handle `+0x10c` / `+0x110` with flag `|= 0x400`:
  `(1.0, length)` normally, `(1.0, 0.01)`-ish when the beam is shorter than a threshold. **The beam length reaches the effect
  as a per-instance parameter.** (What `+0x10c/+0x110` are in the XLink2 event, and which emitter field reads them, is not yet
  traced; the custom-shader emitters of `GanonBeastBeam` are the likely readers.) It also moves a sound shape (`aal::Shape`,
  `+0xc78`) along the line.
- `LineBeam::m163` (`0x71002c7184`, per frame): refreshes the transform, then `FUN_71002c73d4` computes the range: `+0xd14`
  target range (from `BeamRange`), `+0xd18` growth time in frames, `+0xd10` current range, which **grows from 0 to the target**
  each frame (`target / frames * deltaTime`); end point = muzzle + `BeamDir` (`+0xcc8`) * range, transformed by the beam matrix.
  Then a **ray cast** (`FUN_71002c795c`) from the start (`+0xc68`) to that end: on a hit the end becomes the hit position
  (`+0xd1c..0xd24`), with hit normal (`+0xd34`), material mask (`+0xcf8`) and hit actor (`+0xd48`). Finally the capsule's two
  vertices are set to start and end (`CapsuleRigidBody::setVertices`), so **the damage volume is exactly the visible line**
  (extended slightly when the attacker has the player profile).
- `FUN_71002c75f0` (post): at the hit point it keeps a second effect handle (`+0xcd8`) and a sound handle (`+0xce8`) positioned
  and oriented by the hit normal. `FUN_71002c7b40` emits the ELink key from the table at `0x71023cec48`: index 0 = **`"Beam_Top"`**
  (normal hit), index 1 = **`"Barrier"`** (the hit material's sub-material is `HolyWall`), fading the previous one when the kind
  changes. These are the three keys of the `GanonBeastBeam` ELink user: `Tail` (the beam body), `Beam_Top` (hit flash),
  `Barrier` (hit on the holy wall).
- Stop: `LineBeam::m166` resets the current range to 0.

**What the effect needs from the game** (this is the whole interface): the `Tail` instance must be placed at the muzzle, oriented
along the beam, scaled to the beam width, and given the length parameter every frame; `Beam_Top` / `Barrier` must be emitted at the
hit point, oriented by the hit normal. Everything else (range growth, ray cast, capsule, rest actors, messages) is gameplay code.

## 3. What is still open (next steps, in order)

1. ~~Parse the ELink2 entries~~ (done, section 2) and ~~trace the BotW beam~~ (done, 2.1). The beam length is passed to the `Tail`
   effect as an instance parameter every frame; find which XLink2 event field `+0x10c/+0x110` is and who reads it.
2. Find the TotK counterpart of `LineBeam`/`BeamBase` (TotK Ghidra): TotK has line beams too (Beamos, Zonai devices); if the class
   exists, the port may be an actor definition plus effect, and the exe mod only has to feed what the Ganon effect reads.
3. Recover the custom shader block: extend `PtclShaderDb` to record which bytes of `sysCustomShaderUniformBlock1` each custom
   program reads and how (scale, colour, scroll), then map the BotW beam's `CSDP` values onto a TotK program.
4. Only for behaviour that is still missing: write the executable patch (a TotK hook that supplies the dynamic value, found the
   same way in the TotK image).

## 4. Reusing this for another effect

Replace the names in section 1.1, repeat 1.2 to 1.5, and fill a new "Findings" section. Keep addresses with the executable
version they belong to.
