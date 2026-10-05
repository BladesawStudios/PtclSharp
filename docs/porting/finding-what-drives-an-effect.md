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

## 2. Findings for the Dark Beast Ganon beam (BotW)

- The beam is an **actor** (`GanonBeastBeam`) whose AI runs `uking::action::GanonBeastBeamMove` (`0x7100175d1c`), which derives from
  `uking::action::SimpleLineBeam` (`0x7100255234`). Both are **gameplay**: they follow a line between two positions, query the
  terrain (`Terrain__getSomething`), place "rest" actors along the beam (`RestActor` = `AscendingCurrent_GanonBeastBeam`, parameters
  `RestDistTime`, `RestDistTimeAdd`, `RestNumMax`, `RestDistLimit`, `RestDistMinLimit`, `RestDistInterval`), and send the line as an
  actor message under a spinlock (`FUN_710070dcc0`; the receiving AI is `uking::ai::SimpleLineBeam::handleMessage_`,
  `0x710056f35c`, message ids `0x8000038` and `0x8000039`). Nothing in them touches shaders or emitters directly.
- The beam's **effect** is data: `ELink2DB.sbelnk` has a `GanonBeastBeam` user that points at the sets `GanonBeast_Beam`,
  `GanonBeast_BeamHit` and `GanonBeast_Beam_BarrierHit` of `Effect/GanonBeastBeam.sesetlist`. How the beam's length and
  orientation reach the effect (ELink2 properties versus an actor-attached transform) is **not yet established**; the ELink2 entry
  has to be parsed to answer it.
- The three engine callbacks that fill the custom-shader uniform buffer (`FUN_71011b9484`, `FUN_71012676bc`, `FUN_71011b37e4`) are
  the generic `RenderStateSet_Basic` / `Basic2` callbacks: they copy lights, shadows, fog and view data into a 0x240 / 0x60 / 0x270
  byte buffer for **every** effect, selected by flag bits of the emitter's shader (`EmitterResource + 0x930`). They are not
  beam specific, so porting them is not needed.
- The beam emitters that use custom shaders (`custom_shader_index` 3 and 4) get their beam-specific values from the emitter's own
  `CSDP` block, which is **static data in the effect file**. If that holds for all of them, the beam needs no executable change for
  the visuals, only a donor shader that reads the same block (see `docs/conversion.md`, "Shader donors").

## 3. What is still open (next steps, in order)

1. Parse the `GanonBeastBeam` entry of `ELink2DB.sbelnk` (BotW) and the `PlayerBeam` / `Obj_MasterBeam` entry of the TotK database;
   compare the properties they pass. This decides whether the mod is data only.
2. Recover the custom shader block: extend `PtclShaderDb` to record which bytes of `sysCustomShaderUniformBlock1` each custom
   program reads and how (scale, colour, scroll), then map the BotW beam's `CSDP` values onto a TotK program.
3. Only for behaviour that is still missing: write the executable patch (a TotK hook that supplies the dynamic value, found the
   same way in the TotK image).

## 4. Reusing this for another effect

Replace the names in section 1.1, repeat 1.2 to 1.5, and fill a new "Findings" section. Keep addresses with the executable
version they belong to.
