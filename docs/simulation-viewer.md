# Simulation, rendering and the effect viewer

PtclSharp plays BotW effects the way the game does: the emitter runtime is re-implemented on the CPU, and the **game's own particle shaders** draw the
result. Nothing about an effect's look is approximated in C#; what is approximated is the *game state* the shaders read (lighting, fog, scene depth).

```
 .sesetlist ──► PtclSharp (decode) ──► PtclSharp.Simulation ──► PtclSharp.Rendering ──► HDR target ──► bloom ──► tonemap
   (VFXB)        EmitterResource          Emitter / particles       game shaders on GL       (game orientation)
                                                                         ▲
                                         lighting + scene inputs ────────┘   (GameEnvironment, depth, frame copy)
```

| Project | What it is |
| --- | --- |
| `PtclSharp.Simulation` | Game-neutral emitter resources and the runtime: emission, particle update, fields, children, plugins (stripes), emitter animation. A BotW reader builds the resources, including the `UpdateParams` fix-ups the game applies before upload. |
| `PtclSharp.Rendering` | GL: decompiles an effect's BNSH shaders, uploads the same uniform blocks and attribute streams the game does, draws, and post-processes (bloom, tonemap). No windowing; the host owns the context. |
| `tools/PtclViewer` | Silk.NET + ImGui viewer and headless screenshot/survey tool. |
| `tools/PtclMeshDump` | Out-of-process G3D/PRIM extraction (the BfresLibrary copies cannot share a process with the shader decompiler's). |

## Simulation

`EffectSystem` owns the global random generator and the live `EmitterSetInstance`s; each set owns `Emitter`s. One `Calculate(frames)` advances everything
(1.0 is one game frame, 30 per second). The pieces mirror nn::vfx: emission shapes, per-particle initialisation (the BotW random tables and LCG), behaviours
(gravity, air resistance, fields, curl noise, spin), child emitters, emitter animation lanes, and the CPU path of the three calculation types. Emitters whose
calculation type is `Gpu` are moved analytically by their vertex shader; `GpuStreamOut` ones are simulated on the CPU. Plugins build stripe geometry
(connection, history and super stripes); the area loop is a draw-time repetition.

Fields are only labelled in `PtclSharp.Layout` when a Ghidra trace proves them; the shipped BotW binary wins over the SDK sources in `research/vfx`.

## Rendering

Each draw is one instanced four-vertex strip per particle slot (or a G3D mesh instanced per particle). The shader is selected by the emitter's shader index
into the file's BNSH archive, decompiled with ShaderLibrary, and compiled with small fixes (`GlslClean`). Its inputs:

| Block / stream | Content |
| --- | --- |
| `sysViewUniformBlock` | view, projection, view-projection and billboard matrices, eye, **depth constants** (row 18), viewport (`ViewBlock`) |
| `sysEmitterStaticUniformBlock` | the first 0x750 bytes of the EMTR after `UpdateParams`: key tables, flags, fade distances |
| `sysEmitterDynamicUniformBlock` | per frame: colours, time, count, alpha, scale (`Emitter.Dynamic`) |
| `sysCustomShaderReservedUniformBlockParam` | the effect's CSDP chunk |
| `sysCustomShaderUniformBlock0-3` | **supplied by the game's lighting code**, not by the file: see `GameEnvironment` |
| `sysEmitterPluginUniformBlock` | stripe parameters, or the area loop's repeat |
| attributes (`sysLocalPosAttr`, `sysScaleAttr`, ...) | the simulation's per-particle arrays, streamed each frame |

### Conventions

The library draws the way the game does: **window origin at the top left, OpenGL clip depth, OpenGL projection**. The target is therefore upside down
relative to an ordinary GL image until it is flipped on presentation (`Tonemap.Draw(..., flipVertical: true)`). The shaders compute screen coordinates for
the depth and frame-buffer samplers assuming this, and WildRenderingSharp's own game-origin passes use the same convention, so scene textures line up.

The view block's depth row is what makes distance fades work: a fragment's distance is `1 / (depth01 * w - y)` with `w = -(1/near - 1/far)` and `y = -1/near`
(`RenderingTests` pins this). Many emitters fade in between two camera distances (`EmitterResource.NearFade`), so effects are meant to be seen from a distance;
the viewer's auto-framing keeps the camera beyond the fade.

### Game state the shaders read

* **Lighting and fog** - `GameEnvironment` fills the custom shader blocks with the layouts the game's callbacks write (traced in Ghidra), from a settable sun,
  ambient and fog. Fog strength is 0 by default; leaving it at 1 turns every lit effect into a flat fog colour.
* **Ramps and environment cube** - the game's time-of-day ramps and reflection cubes are runtime textures that are not in the files; `ProceduralTextures`
  stands in (a daylight sky cube, mid grey for 2D ramps).
* **Deferred shaders** - about 13% of the corpus writes a G-buffer (albedo and view-space normal) instead of a colour. `GlslClean.ForwardizeGBuffer` rewrites them
  to shade the albedo with the sun and applies the fixed-function alpha test, using the emitter's discard threshold.
* **Scene depth and colour** (`SceneInputs`) - soft particles read the scene's distance and refraction effects read the scene colour. A host passes a linear
  depth texture in the view (`SceneLinearDepth`, with its scale and offset); the renderer converts it to the form the shaders expect and copies the bound target
  once per pass for `sysFrameBufferTexture`.

## Post-processing

`Bloom` (bright-pass with a soft knee, 13-tap downsample chain, tent upsample) returns a glow texture that `Tonemap` adds before exposure and an ACES curve.
It works on any HDR texture; `BloomSettings` has the threshold, knee, intensity, radius and level count.

## The viewer

```
dotnet run --project tools/PtclViewer -- --rom "<BotW ROM>" --effect FireWood --set Chemical_Fire_FireWood
```

Interactive: pick an effect and set, play/pause/step, loop (restart when the effect ends, or after N seconds; optionally advance to the next set), speed,
exposure, bloom, orbit, per-emitter visibility. Headless: `--shot out.png --at 20,60`, `--survey a,b|all --shot dir`, `--cam yaw,pitch,dist[,tx,ty,tz]`,
`--no-bloom`, `--bloom intensity[,threshold]`, `--color r,g,b` (the runtime colour the game sets on some effects), `--probe x,y`.

Debug environment variables: `PTCL_DUMP_GLSL`, `PTCL_DUMP_TEX` (dump decompiled shaders / decoded textures), `PTCL_NODISCARD=1`, `PTCL_DEBUG_EXPR` (replace the fragment
output), `PTCL_DEBUG_VTX` (write vertex temporaries to the second output and force the strip on screen).

Tests: `PTCL_BOTW_ROM=<ROM> dotnet test tests/PtclSharp.Tests --filter "SimulationTests|RenderingTests"` (the corpus test simulates every fourth BotW file).

## Known approximations

* The game's lighting environment, time-of-day ramps and reflection cubes are stand-ins; effects that depend heavily on them (smoke, malice) are plausible,
  not exact.
* GPU noise on non-detailed GPU emitters is approximate.
* Sort order follows the SDK semantics and has not been compared frame by frame with the game.
* BotW only: the TotK emitter format needs its own `IEmitterResourceReader`.
