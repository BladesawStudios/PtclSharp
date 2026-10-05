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

## 6. Open questions (each is a Ghidra or in-game test)

1. What binds a `Toggle` shootable to its shooter? `Kohga_Golem_Beam` uses a golem-specific AI node for position/orientation; the
   weapon path (`ShootArgEquipment`, `PrepareControllerToggle`) may position it by itself. Trace `PrepareControllerToggle`
   (`0x7101737c84`) and the equipment shoot path (`FUN_7101b74178` / `FUN_7101b741f0` in `ChemicalRodShootModule`).
2. Does the sword's module support a *held* beam (Toggle on while a button is held) or only fire-and-forget? Check
   `ChemicalRodShootModule::updateImpl_` (`0x7101b73e24`) against the Toggle lifecycle in `FUN_7101743ea8`.
3. Where does the range (`+0x98`) come from for a non-device beam: blackboard `BeamosBeamRange`/`Default` via `FUN_7101bb9a58`
   (`ExecuteBeamDevice`)? A weapon-fired beam needs a defined range.
4. What `FUN_710086d5d0(1.0, length, handle)` sets, and which emitter field of the converted effect reads it (BotW writes the same
   pair; the custom-shader emitters probably read it).
5. Whether the converted custom-shader emitters (donor shaders) render the beam acceptably; this is the shader/CSDP track
   (`docs/conversion.md`), independent of the actor.
