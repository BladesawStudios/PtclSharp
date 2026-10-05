# TotK: how line beams and the Master Sword beam work, and how to port the Dark Beast beam

Executable: TotK 1.2.1 Switch image (`0x71...` addresses). Companion to `finding-what-drives-an-effect.md` (the BotW side).

## 1. The key result

TotK has the BotW line beam **as data-driven engine code**, not as a per-actor class. A *Shootable* actor whose
`ShootableParam.ShootableType` is `Toggle` is a line beam: the engine ray casts, resizes the damage capsule, emits the ELink keys
`Tail` / `Beam_Top` and feeds the effect its transform, width and length. The ELink call-table names are the same as BotW's
(`Tail`, `Beam_Top`; `Kohga_Golem_Beam` and `BeamosBeam` use them). Therefore the Dark Beast beam can exist in TotK **as an actor
pack plus an ELink user plus the converted effect**, with no new beam code, and the Master Sword can fire it by pointing its
`ShooterParam` at that actor. What is unverified is whether a `Toggle` shootable can be fired by the sword's shoot module (section 5).

## 2. The Shooter / Shootable framework (data)

| Piece | File (inside the actor pack, `Pack/Actor/<Actor>.pack.zs`, Zstandard with `pack.zsdic`, SARC; `.bgyml` is BYML) | What it says |
|---|---|---|
| Who shoots what | `Component/ShooterParam/<X>.game__component__ShooterParam.bgyml` | `ShootableActorSettings[]`: `Actor` (an `ActorParam` path), `CreateMethod` (`OnInitialization`), `CreateNumOnInitialization`, `MaxActors`, `CreatePriority`, `KeyHash`, `ParamNum`, `Is*` flags |
| Kind of shootable | `Component/ShootableParam/<Y>.game__component__ShootableParam.bgyml` | `ShootableType`: `UseAI` (the shootable runs its own AI: a ball), `Toggle` (continuous ray: a beam), also arrow and shockwave controllers exist in code |
| Beam options (`Toggle`) | same file | `ToggleRayVector` (`ShootableToggleRayY` = local Y axis), `ToggleRayCastAndScaleSensor` (ray cast and resize the sensor), `ToggleGameActorActive`, `DamageInterval` (30), `BeamTerrorLevel` / `BeamTerrorRadius` (perception) |
| Beam range / width | `Component/Blackboard/BlackboardParamTable/<Y>.*.bgyml` | f32 blackboard keys `BeamosBeamRange`, `BeamosBeamRangeDefault`, `BeamRadiusScale`, `BeamRadiusScaleDisplay` |
| Effect | `Component/ELink/<Y>.engine__component__ELinkParam.bgyml` -> `UserName`; ELink2 database user of that name | `Tail`, `Beam_Top` call tables (section 4) |
| Damage | `GameBalance/AttackParam/<Y>.*`, `Phive/RigidBody*` (`<Y>_Atk`, `BeamCutting` capsule) | attack power, the sensor the beam resizes |

Examples read from the shipped packs:
- `Weapon_Sword_070` (the Master Sword; `Weapon_Sword_077` shares its ShooterParam): one setting, `Actor =
  Work/Actor/PlayerBeam.engine__actor__ActorParam.gyml`, `CreateMethod = OnInitialization`, `MaxActors = 4`, `ParamNum = 12`.
  Its AI is `MasterSwordRoot.root.ainb` with `MasterSwordRoot.Shoot.module.ainb`; the code behind it is
  `ai_unknown_node::MasterSwordRootShootModule` (`0x7101e35a58` enter, `0x7101e35ad8` update; parameter `IsAlwaysEnergyMax`), a thin
  subclass of `ChemicalRodShootModule` (`0x7101b73e24` update; the spawn is `FUN_7101b72d88`, which asks the `Shooter` component).
- `PlayerBeam` (what the sword shoots now): `ShootableType = UseAI`, AI `ChemicalBall.root.ainb` + `ChemicalBall.Shoot.module.ainb`,
  `GameParameter/ChemicalBall/PlayerBeam.*`, ELink user `PlayerBeam` (call tables `Body` -> `Obj_MasterBeam` etc.). It is a
  projectile, which matches the in-game test (the converted effect flashed when the projectile ended).
- `Kohga_Golem_Beam` (a `BeamosBeam`-derived actor): `ShootableType = Toggle`, `ToggleRayVector = ShootableToggleRayY`,
  `ToggleRayCastAndScaleSensor = true`, `DamageInterval = 30`; AI root `ExecuteKohgaGolemBeamMtrixBind` (golem specific: it copies
  the owner's position/orientation onto the beam each frame, `0x7101cbde60`), `ExecuteKohgaGolemBeamWitdh` (width control,
  `0x7101cbe0ec`); ELink user `Kohga_Golem_Beam` (`Tail` -> `Enm_Kohga_Golem_Beam`, `Beam_Top` -> `Enm_Kohga_Golem_Beam_Top`,
  `Shoot` call table started by an action slot `ShootableState`/`Shoot`).

## 3. The beam engine (code): `FUN_710174694c` (`ShootControllerToggle` update)

Per frame, for a `Toggle` shootable (`game::shoot::ShootControllerToggle`, `0x710131266c` ctor, `getAtSensor` `0x7101746650`):
1. If not active (`FUN_7101743ea8`): kill the `Tail` handle (`+0x6c`) and fade the hit handle (`+0x7c`), set the bullet layer.
2. Counts the damage interval (`ShootableParam::getDamageInterval`) and increments the attack id each interval.
3. Builds a ray from the shooter along `ToggleRayVector` scaled by the **range** at `+0x98`, ray casts
   (`phive::RaycastQueryBase::query`, collision filter from `IsThroughFriendly`).
4. On a hit: emits the ELink key **`"Beam_Top"`** (`XLinkComponent::searchAndEmit`, once; handle set `+0x7c`), places it with
   `HandleELink::setMatrix` at the hit point oriented by the hit normal, moves the matching sound (`HandleSLink::setPosition`), and
   sets the XLink property `0xd76c96f4` (an s32; the hit material/actor kind, `0xb` when nothing is hit).
   On a miss: fades the hit handle and sets the property to `0xb`.
5. Resizes the sensor capsules with `ShapeCapsule::setCenterAB(start, end)` and `setRadius(width * scale)`: the attack sensor and the
   `BeamCutting` rigid body (radius +1.0), and updates the nav-mesh obstacle when the beam moved.
6. Feeds the **`Tail`** handle (`+0x6c`) every frame: `HandleELink::setMtxAndScale(handle, matrix, scale)` with the matrix built
   along start->end and scale `(width, 1.0, width)` (`+0xa8`), then `FUN_710086d5d0(1.0, length, handle)`, the same
   `(1.0, length)` pair BotW writes at event offsets `+0x10c/+0x110`. (Meaning of that call: **not yet traced**; its callee should be
   an ELink instance parameter setter; a short beam (<= `DAT_71038126f0`) uses a fixed matrix.)
7. Emits a perception "BeamTerror" for nearby AI.

BotW does the same in `LineBeam::m163` (`0x71002c7184`) + `BeamBase::reflectMaybe` (`0x7100003140`); see the BotW doc. Differences
that matter: TotK has no `Barrier` key (BotW swaps `Beam_Top` for `Barrier` on a `HolyWall` hit); TotK's range is a blackboard
value, BotW's grows over time from 0 to `BeamRange`.

## 4. ELink2 data for the beam

TotK `Kohga_Golem_Beam` user (xlink2 text, `ELink2/elink2.Product.110.belnk.zs`): `AssetCallTables` `Tail[0x72f9fb41]` -> asset
`Enm_Kohga_Golem_Beam`, `Beam_Top[0xe17bf0c6]` -> `Enm_Kohga_Golem_Beam_Top`, `Shoot[0x312ba840]` (the start trigger), `Chemical_*`
and `Death_*` tables; `ActionSlots.ShootableState.Shoot` is a frame window that triggers `Shoot`. BotW's `GanonBeastBeam` user:
`Tail` -> `GanonBeast_Beam`, `Barrier` -> `GanonBeast_Beam_BarrierHit`, `Beam_Top` -> `GanonBeast_BeamHit` (`valDampDist = 1000`,
`valDrawPriority = 129`).

## 5. The plan (data first, code only if a test demands it)

1. **Effect** (done): the converted `GanonBeastBeam` effect, sets renamed to anything the ELink user references (the names can be
   anything because the user table maps key -> set name; rename sets with `PtclConvert --rename-set`).
2. **ELink2 user** `GanonBeastBeam`: new user in the TotK database (edit the xlink2 text and reconvert with `xlink`):
   tables `Tail` -> set for the beam body, `Beam_Top` -> hit flash, plus the `Chemical_*`/`Death_*` tables copied from
   `Kohga_Golem_Beam`, and the `Shoot` call table with its action slot, copied from the Kohga user.
3. **Actor pack** `GanonBeastBeam` (a copy of `Kohga_Golem_Beam.pack.zs` re-pointed): `ELinkParam.UserName`, `ActorParam` name, a
   blackboard table with the beam's range (`BeamosBeamRange*`) and width (`BeamRadiusScale*`), attack param (BotW power 72 is a
   starting value), AI root = a generic one (not the golem-specific `ExecuteKohgaGolemBeamMtrixBind`; it needs a node that binds the
   beam to the shooter, see open question 1).
4. **Master Sword**: change `Weapon_Sword_070`'s `ShooterParam` `Actor` to the new actor (and check `MaxActors`, `ParamNum`).
5. **Test** in game. Failure modes and what they mean are in section 6.

## 6. The open questions, traced (confidence marked)

**Q1. What binds a Toggle shootable to its shooter and aims it?** *(mostly established; the last link is inferred)*
- Shooting is generic. `Shooter::shootImpl_` (`0x71016490d0`) and `SharedShootableActor::shoot` (`0x7101734ab0`) do not care about the
  `ShootableType`: they reset the `Shootable`, store the shoot arguments (origin, velocity, target position, damage, attack id), call
  `changeState(2)` (active) and copy the data to the shot actor. The type only selects the *controller* that runs afterwards
  (`ShootControllerToggle`, `...UseAI`, `...Arrow`, `...Shockwave`, `...Thrust`; `PrepareController*` mirror them). So a sword can
  shoot a Toggle actor through the same code that shoots `PlayerBeam`.
- A queued shoot request (`Shooter::updateShootables`, `0x710082ed1c`) carries a **bone name** (`ShootBoneName`) and an origin
  offset; the code transforms them by the shooter's bone matrix (`vfunc +0x78`), gets the shooter's target position and calls
  `Shooter::prepare` (if no actor is ready) and `Shooter::shoot`. This is the path enemy AI uses (`ExecuteShooterPrepare`,
  `OneShotShooterShoot`/`...FromTarget`, params `ShootBoneName`, `ShootPos`, `ShootableIndex`).
- The Toggle controller does **not** move the actor to the muzzle. It reads the actor's own transform each frame. Actors that follow
  a moving owner do it by data: `Drake_Beam_Small` has **`ModelBindParam.BindType = Trans`** (bound to its creator's model; the
  `BeamosBeam` base has no bind, which is why beam *devices* stay put) and no AI at all; `Kohga_Golem_Beam` uses a custom AI node
  (`ExecuteKohgaGolemBeamMtrixBind`) to copy the owner's transform instead.
- **Aiming:** when the shoot request supplied a target position (`Shooter::updateTargetPos`, `0x71016484fc`, sets `Shootable+0xac`
  and flag `+0xd9 |= 0x40`), the Toggle controller rotates the actor so its forward axis points at it (`FUN_7101747950`,
  `makeVectorRotation`). Unverified: that the sword's request (the `RodParam` fields `IsTargetPos`, `IsShootCamera`) fills the
  target position; the in-game test shows it.
- The controller itself emits the `Tail` effect (`FUN_7101746830` calls `XLinkComponent::searchAndEmit` with the key at
  `0x71036f5027`) when it (re)activates, so the actor needs no AI to start the effect.

**Q2. Held beam or fire-and-forget?** *(established for the engine, open for the sword's input)*
A Toggle beam lives as long as the shootable is in the shoot state: nothing in the controller ends it. The shooter ends it with
`Shooter::sleep` / `sleepAll` (`OneShotShooterRequestSleep(All)` in the Drake AI after the beam animation). The Master Sword's AI
graph (`MasterSwordRoot.root.ainb`) has `OneShotPrapreShooterActorSleep`, `QueryShooterHasPreparedActor` and the
`ChemicalRod.IsShootCondition` module; `MasterSwordRootShootModule` (`0x7101e35ad8`) only manages energy and prepares actors
(`FUN_7101b72d88` -> `Shooter::requestCreateShootable`). What actually triggers the *shot* is the player's attack AI via the
weapon's `RodParam` (`ShootNum`, `ShootAngle`, ...), not the sword's own AI, and **nothing in the vanilla game shoots a Toggle actor from
a weapon or the player** (only owners such as `Enemy_Drake*`, `Enemy_DungeonBoss_Gerudo*`, `DgnObj_BeamDevice*`, `AssassinIronBall*`
do). So expect a one-shot trigger to leave the beam running until something sleeps it (the sword's own sleep path, a lifetime on the
actor, or damage/death). The DeathParam `ShootableCommon` and `ShootableParam.ResetSystemGroupIDTime` are the data knobs to try.

**Q3. Range and width.** *(established)*
Both come from the beam actor's **blackboard defaults**, not from code: `BeamosBeamRange` / `BeamosBeamRangeDefault` (Drake beam:
2000.0), `BeamRadiusScale` (2.5) and `BeamRadiusScaleDisplay` (1.0). `FUN_710174694c` multiplies the ray vector by the range at
`+0x98` and sets the capsule radius to `radius * scale`. Devices override the range through `ExecuteBeamDevice`
(`FUN_7101bb9a58`) and the Zora boss through `ExecuteDungeonBossZoraBeam`; an ordinary beam actor just keeps its defaults. For the
Dark Beast beam pick range = BotW `BeamRange` of `Enemy_GanonBeast` (read it from its AIProgram) and width from the BotW
`BeamBase +0xc80`.

**Q4. What is the `(1.0, length)` pair?** *(established as to mechanism; the SDK name is inferred)*
`FUN_710086d5d0` writes the two floats into the ELink event (`+0x130/+0x134`) and sets event flag `0x1000`.
`EventELink::fixDelayParam_` (`0x71009db3b0`) hands the flags to `AssetExecutorELink::setDelayParam` (`0x71009db598`), where bit
`0xc` copies them into **`nn::vfx2::EmitterSet +0x1c0/+0x1c4`** (x, y; z is kept) and recomputes `+0x1e0/+0x1e8` as the product with
the set's own scale (`+0x1a0/+0x1a8`): an **EmitterSet-wide scale of the particles** (bit `0xd` is the same with all three
components; bit `0x12` is a volume scale and sets a flag). BotW writes the same pair through event flag `0x400`. Nothing in the
effect file needs to read it: the beam's particles are unit-length (BotW's models are 1 unit long: `gurdianbeam`, `ring32uloop`)
and the engine stretches every particle by `(1, length)`. Consequence for the port: it works for any converted emitter set as long as
the emitters scale by the set scale, which is the default behaviour of both engines.

**Q5. Do the custom-shader emitters render acceptably?** Independent of the actor; see the shader track in `docs/conversion.md`.

## 7. Mod construction checklist

1. **Effect**: `PtclConvert ... --rename-set` (any set names; the ELink user maps keys to them).
2. **ELink2**: copy the `Drake_Beam_Small_Fire` (or `Kohga_Golem_Beam`) user, rename to `GanonBeastBeam`, point `Tail` at the beam body
   set and `Beam_Top` at the hit set; keep the `Chemical_*`/`Death_*` tables. (TotK emits only `Tail` and `Beam_Top`; BotW's
   `Barrier` variant has no TotK trigger, so merge it into `Beam_Top` or drop it.)
3. **Actor pack** `GanonBeastBeam`: start from `Drake_Beam_Small` (no AI, `ModelBind Trans`, `BeamosBeam` parent, ShootableType
   `Toggle`), change `ELinkParam.UserName`, the blackboard defaults (range, radius scale), and `AttackParam` (BotW power 72).
4. **Master Sword**: in `Weapon_Sword_070` and `Weapon_Sword_077` set the `ShooterParam` `Actor` to the new actor; keep
   `CreateMethod = OnInitialization`, `ParamNum = 12`; use `MaxActors = 1` (a held beam).
5. **Test order**: (a) beam appears and follows; (b) aims at the target; (c) how it ends; fix with sleep/lifetime data; (d) range/width
   values; (e) visuals (shader track).

## 8. In-game findings and fixes

First in-game test (actor + sword + effect, no AI): the sword fires, the effect barely shows, the beam never ends, and it does not
behave like BotW's. Traced in TotK for the second symptom:

- **Why it never ends.** `ShootControllerToggle`'s update (`0x710174694c`) never ends anything; it only keeps the ray, capsules and
  effect handles current. (`FUN_7101743ea8`, which looks like a lifetime gate, is an owner-to-origin occlusion ray: if the owner
  cannot see the beam origin the effect handles are killed and re-emitted when it can.) A Toggle shootable lives until its
  *shooter* puts it to sleep: `Shooter::sleepAll` (`0x7101648594`) is reached only through `trySleepAll` from AI nodes
  (`OneShotShooterRequestSleepAll`, Kohga/Goron-sage nodes), and `Shootable::requestSleep` (`0x710164580c`) from
  `OneShotShootableRequestSleep`. The sword side (`ChemicalRodShootModule`/`MasterSwordRootShootModule`, `0x7101b73e24`) only
  creates, prepares and shoots; the vanilla Master Sword beam is a *projectile*, which ends itself through
  `ProjectileShootStateTimeOut`, `ProjectileDeleteRange` or a hit (`FUN_71017385ac`, the projectile controller's update). Those
  fields are not read by the Toggle controller.
- **`ShootRange` / `ShootSpeed` / `ShootMotionProperty` in the sword's `RodParam` do not apply to a Toggle.**
- **Fix (data only): give the beam actor an AI.** `Drake_Beam_Small` has none. `scripts/make_beam_ainb.py` trims a copy of the
  Drake fire burst beam's AINB to: S32 selector on `ShootableState` (enum order from the name table at `0x710431f700`: Sleep 0,
  Prepare 1, Shoot 2, Stationary 3, Reflected 4, UsedExternally 5) -> in Shoot, `ExecuteGenericVFRCounter` feeds a BoolSelector
  (expression: counter >= N frames) whose True branch runs `OneShotShootableRequestSleep`. `GanonBeamMod.AddLifetimeAi` adds
  `AI/<Actor>.root.ainb`, `AI/AIInfo/<Actor>...bgyml` (`RootAIRef: Work/AI/Root/<Actor>.root.ain`) and `Components.AIInfoRef` to the
  actor. `GanonBeamMod.BeamFrames` is the duration (30 fps frames).
- **Where range and width come from** (`FUN_7101746364`, the controller's start): `BeamosBeamRange` (fallback `...Default`) ->
  ray length (`+0x98`), `BeamRadiusScale` -> capsule radius multiplier (`+0xa4`), `BeamRadiusScaleDisplay` -> effect scale (`+0xa8`,
  fed as `(w, 1, w)`). Every vanilla TotK beam has Display = 1; their visible width comes from the effect itself.
- **Still open**: BotW's effect width (`BeamBase+0xc80`, set by `setProperties` from the owner; not in the actor's param lists), the
  0 -> `BeamRange` growth over time (TotK reads the blackboard range once at start; growth would need an AI node writing
  `BeamosBeamRange` each frame, or a code patch), the `Barrier` hit variant, and why the effect is nearly invisible.

### 8.1 Second test: still never ends, nearly invisible

Findings from the second round (no new in-game result yet for the fixes below):

- **RSDB rows carry per-actor memory and culling data.** `InstanceHeapSize` is 47,776 for the AI-less `Drake_Beam_Small` but about
  56,500 for the AI-carrying `Drake_Burst_Beam_Small_Fire`; the cloned row now uses 56,528 so the AI can be allocated. The Drake rows
  also have `CalcRadius`/`DisplayRadius` 80 and `LoadRadius` 180 (distance culling radii of the actor itself).
- **BotW's width is 1.0, by code.** `LineBeam`'s creation function (`0x6f331c` in the BotW text; stores range `+0xd14`, width `+0xc80`,
  growth frames `+0xd18`) receives the width as a literal `1.0` from all three callers; the range is the action's `BeamRange` (300)
  and the growth frames come from a virtual call on the action (or -1). The BotW effect is therefore authored for scale 1: its
  body emitters have `particle_scale_xyz` of only `0.01..0.03` on unit models (a ~1 cm hot core, colour x500 HDR) plus one 1 m wide
  glow (`Light_Long`); TotK's Drake body emitter is `0.7`. Seen in game: nearly invisible at range, visible when something is close.
  The hit sets are large (`4..40`), which is why the hit effect shows.
- **Tools** (scratch, not committed): an emitter field dumper (`PtclSharp` layouts) and a G3D model bounds dumper; both models and
  emitter data confirm the unit-size geometry.
- **Changes**: the lifetime timer now runs in every state but Sleep; `Beam_Top` no longer carries the donor's `Scale = 1.875` (BotW
  has none; it has `valDampDist = 1000`, `valDrawPriority = 129`); `BeamRadiusScaleDisplay` is `GanonBeamMod.BeamWidth` (10) to
  make the thin core visible. This departs from BotW's 1.0 on purpose: TotK has no equivalent of whatever made the BotW core read
  as thick (bloom strength, shader), so it is a visual compromise to tune.

### 8.2 Third test: despawns, flickers in, thin and not pink

The lifetime AI works. The look was wrong because the automatic donor choice paired the main body emitter (`Emitter1_Copy2`, a plain
textured model with no custom shader in BotW) with the `static` archive's `Distortion_00`, a screen-distortion shader: it does not
draw the emitter colour, and refracts the background where it covers. The scorer compares program *signatures* (samplers, vertex
inputs, parameter reads), not what a shader does, so any shader with the right inputs can win. `PtclConvert --donor-for
<source emitter>=<donor emitter>@<shader_idx_normal>` now overrides the donor of one source emitter inside the chosen archive.
`DbList`-style ranking (top donors per emitter within one file) is how to find alternatives; prefer non-custom donors with the same
texture-slot pattern for plain emitters. Body set overrides used: `Emitter1_Copy2=Ring_In_00@908`, `Emitter1_Copy2_Copy2=Ring_Out_00@909`,
`Light_Long=light_Yellow@79`, `Dot=ZonauAura_A_00@419`. The hit sets still use automatic donors (several are "custom shader the
BotW side cannot carry", e.g. `Ripple`, `YBill`, `Smoke`).

### 8.3 Fourth test: the beam is almost never drawn (an executable patch)

The body effect showed for ~2 frames, rarely. Cause, from the Toggle update (`0x710174694c`): its first call, `0x7101743ea8`, returns
true when a ray from the owner (the shooter's position, resolved through the Attachment parent for attached actors) to the beam
actor's position hits anything. The ray sets no group exclusion, so a sword in Link's hand makes it hit Link. On true the update kills
the `Tail` handle (`+0x6c`), fades the hit handle (`+0x7c`), sets `+0xcf` and returns; when it is false again `+0xcf` makes it
re-emit `Tail` (`0x7101746830`). A Drake has the ray clear of its own body, so vanilla beams never trip it.

There is no data switch for it (the only other condition is an invalid shooter link, which the sword always has). Patch
(`tools/TotkModKit/patches/GanonBeastBeam_NoOcclusionKill.pchtxt`, TotK 1.2.1 build id `9B4E4365...F850`): the `bl 0x7101743ea8` at
`0x7101746984` (text offset `0x1746984`) becomes `mov w0, #0`. Only the Toggle update's call is changed; the other caller
(`FUN_7101744fe8`) is untouched. Built output is in `converted/GanonBeastMod/exefs/` (also an IPS32 for Atmosphere-style loaders).

### 8.4 Bisect result: the pipeline works, the converted effect is what is invisible

With `TotkModKit --elink-user Drake_Beam_Small_Fire` (the same actor, sword packs, lifetime AI and exefs patch, emitting through the *vanilla*
Drake fire effect) the sword fires a long beam with the Drake's base flame, so spawning, matrix, `(1, length)` scaling, lifetime and
the occlusion patch are all fine. (It fired straight up: see below.) The faults are in the converted BotW emitters:

- BotW's body emitters are ~1 cm wide (`particle_scale` 0.01..0.03 on unit models, colour x500 HDR, drawn with BotW's bloom); TotK's
  Drake body emitters are 0.6..2 m (and the glow 6x20x10). The donor shaders do not reproduce BotW's HDR/bloom look.
- Field differences against the vanilla donor emitter that matter: `blend_mode_index` (donor 1, converted 0), `draw_path` (8 / 7),
  `particle_color_rgb_scale` (0.2 / 500), the donor's `unverified_shader_param_BD0_C0F` block, and `emitter_trans`.

Working route for now: `tools/PtclTint` re-skins a vanilla TotK effect (reads it, renames the sets, shifts colours with
`--recolor g,b,swap`, scales the width with `--width`). The body is TotK's own beam emitters tinted from fire-orange to BotW's magenta:
```
PtclTint Drake_Beam_Small_Fire.Nin_NX_NVN.esetb.byml.zs ZsDic.pack.zs out.esetb.byml.zs --only-set Enm_Drake_Beam_Small_Fire
  --only-set Enm_Drake_Beam_Small_Top_Fire --rename-set Enm_Drake_Beam_Small_Fire=GanonBeast_Beam
  --rename-set Enm_Drake_Beam_Small_Top_Fire=GanonBeast_BeamHit --recolor 1,1.4,1
```

Aim: the Toggle controller casts along `ToggleRayVector` (`ShootableToggleRayX/Y/Z`, local axes of the actor); the donor's Y fires
straight up, a shot actor faces local Z (and BotW's `BeamDir` is `(0, 0, 1)`), so the actor's ShootableParam now sets
`ToggleRayVector = ShootableToggleRayZ`.

### 8.5 Colour and chemistry

- **BotW's absolute HDR must not be copied onto the Drake emitters.** `--keys` (copy BotW's colour/alpha/scale animation) and `--peak` made the
  body a flat, hard-edged white spindle: BotW's `Light_Long` has `alpha0_const = 1` and 6000x colour, while the Drake `LineLight` emitter relies
  on a tiny alpha, so it filled its mesh solid, and anything far above ~30 clips to white and then blooms white. TotK's bloom is fine (the vanilla
  Drake beam has a halo); the body now keeps the Drake emitters' own intensity/alpha/animation and only gets a hue (`--paint`) and a width
  (`--scale`). `--keys` is kept for the hit flames, which look right.
- **The orange fire explosion on impact is chemistry, not the effect.** The donor's chemical material is `IsBurn: True` with the `FireLv3`
  (StrongFire) element; the attack param itself has no element. Kohga/Gerudo/PlayerBeam use the empty `NoChemicalShootable` chemical param, so the
  build now copies that file from `Kohga_Golem_Beam` into the actor, repoints `ChemicalRef`, and removes the donor's chemical files.

### 8.6 Range growth (executable patch with a code cave)

BotW's `LineBeam` grows its range from 0 to `BeamRange` over a number of frames. TotK's Toggle controller reads `BeamosBeamRange` once, in its start
function (`0x7101746364`; the only code that uses the key's hash), caches it at `this+0x98` and uses that every frame, so no data (and no AI node)
can make it grow: it needs code. `tools/TotkModKit/scripts/make_exefs_patch.py` builds the patch (`.pchtxt` + IPS32, assembled with keystone and
re-disassembled to check):

- The call `bl 0x7101743ea8` (occlusion test) at `0x7101746984` in the update becomes `bl <cave>`. For a controller without the marker the cave ends in `b 0x7101743ea8` (a tail call: x0, x1 and lr are untouched), so every vanilla
  beam runs the original occlusion test unchanged; for ours it returns 0 (never occluded), which replaces the earlier "never occluded" patch.
- The cave sits in the zero padding at the end of `.text` (`0x2b19a50`; the segment is padded to `0x2b1a000`), 88 bytes.
- It only acts when the controller's `BeamRadiusScale` (`this+0xa4`) is the marker `0.500123` (our actor's blackboard; vanilla beams use whole
  numbers). Then each frame `this+0x98 = min(300, max(this+0x9c, 0) + 10)`. `this+0x9c` is the last frame's measured length (the start function
  sets it to -100 each shot), so the beam grows from zero on every shot and, after a hit, grows from the hit distance. Do not use `this+0x5c`
  as a counter: the base `ShootController::calc_` (`0x7101743bd4`) counts it down and resets physics group ids when it reaches zero.
- BotW's growth time comes from a virtual call on the action (some callers pass -1); the rate (10 m per frame, ~30 frames) is a guess.
