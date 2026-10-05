# Building the "Master Sword fires the Dark Beast beam" mod

`tools/TotkModKit` builds the mod from **copies** of the vanilla files; it refuses an output folder inside the vanilla romfs and never
writes there. Plan and evidence: `totk-line-beam-and-master-sword.md`. RSTB is left to TKMM.

```sh
# 1. convert the BotW effect (textures, models, donor shaders) into a work folder
PtclConvert <BotW>/Effect/GanonBeastBeam.sesetlist auto <romfs>/Pack/ZsDic.pack.zs work/GanonBeastBeam.Nin_NX_NVN.esetb.byml.zs \
    --botw-rom <BotW> --totk-romfs <romfs> --shader-db shaderdb.json.gz --texture-out work/TexToGo
# 2. build the mod tree
TotkModKit build-ganon-beam --romfs <romfs> --out converted/GanonBeastMod/romfs --xlink <xlink.exe> \
    --effect work/GanonBeastBeam.Nin_NX_NVN.esetb.byml.zs --textures work/TexToGo
```

What it writes under `--out` (all new files; load the folder with TKMM):

| File | Change |
|---|---|
| `Pack/Actor/GanonBeastBeam.pack.zs` | Copy of `Drake_Beam_Small` (a `Toggle` beam that binds to its shooter). Files carrying the donor name are renamed and every reference in the pack follows (`ActorParam`, `ELinkParam` -> user `GanonBeastBeam`, blackboard info/table, `GameParameterTable`, `AttackParam`, `ChemicalParam`, `ShootableParam`, `ModelBindParam`). The `SLinkParam` keeps the donor's sound user (the sound database has none for the new actor). Edits: blackboard `BeamosBeamRange`/`Default` = 300 (BotW `BeamRange`), `BeamRadiusScale` = 0.5, `BaseAttackPower` = 30, no initial burn. |
| `Pack/Actor/Weapon_Sword_070.pack.zs`, `..._077.pack.zs` | The `ShooterParam` is renamed to `Weapon_Sword_070_GanonBeastBeam`, its ref in the sword's `ActorParam` follows, and its single setting now shoots `Work/Actor/GanonBeastBeam.engine__actor__ActorParam.gyml` with `MaxActors = 1`. |
| `RSDB/ActorInfo.Product.121.rstbl.byml.zs`, `RSDB/GameActorInfo.Product.121.rstbl.byml.zs` | One new row `GanonBeastBeam`, copied from `Drake_Beam_Small`'s row (`ELinkUserName` -> `GanonBeastBeam`), inserted in sorted position. The other RSDB tables have no row for the donor; `Tag` has no entry for it. |
| `ELink2/elink2.Product.110.belnk.zs` | New user `GanonBeastBeam` copied from `Drake_Beam_Small_Fire` (edited with xlink2): `Tail` -> set `GanonBeast_Beam`, `Beam_Top` -> `GanonBeast_BeamHit`; the donor's muzzle loop (`Shoot` table and action slot) and `Chemical_Fire` are dropped. xlink2 round trips the shipped database to identical text. |
| `Effect/EffectFileInfo.Product.110.Nin_NX_NVN.byml.zs` | `GanonBeastBeam` added to `BinaryDict` and `EsetbList`. |
| `Effect/GanonBeastBeam.Nin_NX_NVN.esetb.byml.zs`, `TexToGo/*.txtg` | The converted effect and its four new textures. |

Verified: SARC reader/writer and BYML writer reproduce the shipped packs byte for byte, so only the listed edits differ; the vanilla
files were not modified. Not verified: anything in game. Tuning knobs are constants at the top of `GanonBeamMod.cs`.
