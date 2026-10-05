# Cross-version conversion design

PtclSharp should treat conversion as a translation between two runtime
capability profiles, not as a cast between two VFXB structs.

```text
source container
    -> version-specific reader
    -> exact raw VFXB document
    -> normalized emitter model
    -> capability validation and diagnostics
    -> target-version writer
    -> target container
```

## Three representations

### Raw document

The raw document is the source of truth for lossless same-version editing. It
keeps the original container metadata, VFXB header, node graph, unknown bytes,
attribute chunks, and original offsets. The tree writer rebuilds structure from
these nodes while copying opaque payloads and unverified byte ranges unchanged.

This is the role currently started by `VfxbFile`, `VfxbNode`, and the
container readers.

### Normalized model

The normalized model represents concepts shared by the two engines:

```text
EmitterSetModel
  Name
  Emitters
  SharedOverrides

EmitterModel
  Name
  TextureBindings
  Fade
  Emission
  Shape
  Render
  Animations
  ExtensionChunks
```

It must contain only fields whose serialized behavior is understood well
enough to translate. Version-specific offsets do not belong in this layer.
Unknown or partially understood data remains attached as raw chunks.

### Conversion result

Conversion should return both the target document and a report:

```csharp
ConversionResult result = PtclConverter.Convert(
    source,
    PtclTarget.BotW,
    new PtclConversionOptions());
```

The report should identify the exact source location, feature, target, and
action taken:

```text
Warning: ESET[0]/EMTR[2]/FGWD
TotK field chunk has no confirmed BotW runtime handler; omitted from output.
```

## Capability profiles

Each target needs a capability profile generated from executable evidence:

```text
PtclCapabilities.BotW
PtclCapabilities.TotK
```

A feature should not be represented by a single boolean. Track at least:

```text
Readable              runtime loader recognizes it
Writable              PtclSharp can serialize it correctly
LosslessRoundTrip     read/write preserves its bytes and semantics
ApproximationAllowed  a documented downgrade exists
```

The Ghidra handler tables are runtime evidence. They establish `Readable`,
but `Writable` only becomes true after the corresponding payload layout and
writer behavior are understood.

## Diagnostics policy

- `Error`: conversion cannot produce a valid target document, or a required
  feature has no target representation.
- `Warning`: an optional feature will be dropped, approximated, or preserved
  only outside the target runtime.
- `Info`: a target-specific feature was intentionally omitted or a raw chunk was
  preserved without semantic conversion.

Recommended options:

```text
FailOnError
FailOnWarning
PreserveUnknownChunks
AllowApproximation
```

Unknown and unsupported are different. An unrecognized chunk must not silently
be classified as unsupported; it may simply be a feature that has not been
reverse-engineered yet.

## Initial confirmed capability boundary

The BotW executable's `nn::vfx::EmitterResource::ResolveBinaryData` at
`0x7100adcca8` recognizes:

```text
EAA0 EAA1 EAC0 EAC1 EAPL EASL EAER EADV EAGV EAOV
EATR EAES EAET EASS EP01 EP02 EP03 EP04
FRN1 FRND FPAD FCSF FMAG FCOL FCLN FSPN
CADP CSDP CUDP
```

The TotK executable's `nn::vfx2::EmitterResource::ResolveBinaryData` at
`0x710000ec80` additionally has confirmed handlers for:

```text
FCOV FGWD FRN1 FRND FMAG FSPN FCOL FCLN FPAD FCSF
EAA0 EAA1 EAC0 EAC1 EAPL EASL EAER EAET EATR
EAOV EADV EAGV EAES EASS EP01 EP02 EP03 EP04
CADP CSDP CUDP
```

The exact handler list and payload layout are maintained in the per-game
research notes. Runtime recognition alone does not make a chunk safely
convertible.

## Conversion behavior

### Same-version editing

Use the raw document, update confirmed fields through typed views, preserve
unknown chunks, and rebuild the tree in the original version/container. This is
the lossless path.

### Cross-version conversion

Read the source into the normalized model, validate every source chunk against
the target capability profile, then construct the target layout. Do not copy
BotW EMTR bytes into TotK offsets or vice versa: the fixed-data sizes, body
offsets, texture references, runtime structures, and outer containers differ.

### Downgrades

If a source feature has no target handler, conversion must either:

1. emit a warning and omit it;
2. apply an explicit approximation; or
3. fail when `FailOnWarning` is enabled.

The report must state which choice occurred. No feature should disappear
silently.

## Suggested implementation order

1. Split the existing raw VFXB types from the future normalized model.
2. Add `PtclCapabilityProfile`, `PtclFeatureId`, and diagnostic types.
3. Register executable-confirmed attribute-handler capabilities for BotW/TotK.
4. Normalize emitter-set names, emitter names, texture bindings, and the fields
   already mapped in the Ghidra ledgers.
5. Add a validator that reports unsupported and unknown chunks before writing.
6. Add the tree writer and same-version rebuild first, then cross-version writers.
7. Expand the normalized model only as new offsets and payloads are confirmed.


## Implemented (BotW to TotK)

`PtclSharp.Writer` and `PtclSharp.Conversion` implement the raw-document and conversion layers for the direction BotW to TotK.

- `VfxbDocument` is the editable raw document: the `ESTA` tree (`ESET`, `EMTR`, attribute chunks) as mutable nodes plus an opaque
  tail (`GRTF`, `PRMA`, `G3PR`, `GRSN`). `ToBytes()` recomputes every size and offset; writing an unmodified document reproduces all
  866 BotW and 1,592 TotK payloads byte for byte (`WriterCorpusTests`). `RecountChildren()` rebuilds the child-count words and the set
  emitter counts. `PtclWriter` packs the result into a `.sesetlist` (Yaz0) or `.esetb.byml.zs` (BYML + Zstandard with the shared
  dictionary), keeping unknown BYML keys.
- `BotwToTotkConverter` maps emitter data field by field through the version layouts: same-name Confirmed/Paired fields are copied,
  a small alias table handles renamed or narrowed fields (`depth_compare_func` -> `depth_stencil_mode_index`, the loop periods,
  percent fields), GPU-only fields that line up with a TotK field are copied as "analogs", chunks are rebuilt in TotK layouts
  (animation blocks gain the interpolation word, stripe chunks are re-laid out by name), and TotK-only fields start from the most
  common value in the shipped TotK files (`TotkDefaults`).
- What cannot be translated is reported, not guessed: BotW shaders (the caller picks a TotK shader through `ShaderBinding`, usually
  copied from a donor TotK emitter, together with its `CSDP`), textures (`TextureMap`), meshes and G3D models, custom shader/action
  chunks and fields TotK has no counterpart for. `ConversionReport.Summary()` aggregates the notes.
- `tools/PtclConvert` runs the whole pipeline: `PtclConvert botw.sesetlist donor.esetb.byml.zs ZsDic.pack.zs out.esetb.byml.zs`.

### Textures (`PtclSharp.Textures`)

- A texture GUID names the same texture in both games (every shared-pool texture the two have in common agrees by name; the hash
  that produces it is unknown, so GUIDs come from tables, never from names). BotW resolves an emitter GUID in the file's own
  `GRTF` > `GTNT` table, then in the resident resource `Effect/GameResident.sesetlist` inside `Pack/Bootup.pack` (228 shared textures;
  lookup at `0x7100ae42a8`). The `GTNT` entries are `u64 guid, u32 entry size, u32 name length (with terminator), name padded to 8`
  and the node's `size` counts only the entries. TotK lists `(name, guid)` in the esetb BYML `Textures` array and keeps the pixels in
  `TexToGo/<Name>.txtg`.
- `TexturePlanner.Plan` classifies every GUID a BotW file uses: 79% are listed by TotK under the same GUID (reuse), a few by name only,
  and the rest (plants, tests, a few effects) are BotW-only. `BntxToTxtg` converts those BNTX textures to `.txtg` (format mapping taken
  from the 289 textures both games share: BC1 -> 0x202, BC3 -> 0x505, BC4 -> 0x606, BC5 -> 0x707, R8/RG8/RGBA8 -> 0xC0C/0xB0B/0xA0A;
  channel selectors carried over). BC4/BC5 *snorm* data is remapped to unsigned; that choice is **not verified in game**.
- `PtclConvert ... --botw-rom <ROM> --totk-romfs <romfs>` plans, writes the new `.txtg` files and rebuilds the `Textures` list.

### Shader donors (`PtclSharp.Shaders`, `tools/PtclShaderDb`)

- An emitter's `shader_idx_normal`/`pass1`/`pass2` index the variations of its file's shader archive (BotW: the `GRSN` node's own
  data is the graphics BNSH and its `GRSC` child the compute BNSH; TotK: `GRSN` > `GRSR` / `GRSC`). The texture slots an emitter fills
  equal the `sysTextureSampler*` slots its program samples in 99% of BotW and 88% of TotK emitters.
- `PtclShaderDb build` decompiles every program with Marrow's ShaderLibrary and records, per program (identified by a hash of its byte
  code), its samplers, vertex attributes, uniform blocks and which bytes of the emitter static block (a copy of the emitter data) it
  reads. 15,711 distinct programs from 866 BotW and 1,592 TotK files fit in a 1.6 MB gzip JSON (about 2.5 minutes to build).
- `DonorFinder` compares a BotW program with TotK programs: texture slots must match exactly; then a weighted comparison of the
  *fields* each reads (BotW offsets are mapped to TotK fields with `BotwToTotkConverter.TotkFieldFor`, fields almost every program
  reads weigh little, fields the emitter leaves at zero weigh less), vertex attributes, soft-particle depth sampling and custom-shader
  use. `Plan` picks the one TotK archive that serves the whole effect best, since a converted file carries a single shader archive.
- Check against ground truth: for 600 emitters that exist in both games under the same names, TotK's own version scored within 0.05
  of the best donor for 320 of them (and was ranked first for 115, top ten for 280); 65 had been re-authored with other textures.
- `PtclConvert botw.sesetlist auto ZsDic.pack.zs out.esetb.byml.zs --totk-romfs <romfs> --shader-db shaderdb.json.gz` chooses the
  donor file and per-emitter donors automatically (`--donor-file` overrides the file, `--all-notes` prints what each donor loses).
  The BotW effect has to be in the database; the donors' `CSDP` custom-shader parameters travel with them.

### Models and mesh primitives (`PtclSharp.Models`, `VfxbTail`)

- An emitter picks a G3D model by `g3d_primitive_idx`, a 32-bit id that is the first dword of an entry of the `G3PR` > `G3NT` table
  (entries are `0x18` bytes, one per model in the embedded BFRES, in model order; bytes `+0x10..+0x15` index the vertex attributes
  `_p0,_n0,_t0,_c0,_u0,_u1`). The lookup is `nn::vfx::Resource::GetG3dPrimitive` (`0x7100ae49c8`): own table first, then the resident
  resource, exactly like textures. 2,183 of BotW's 3,211 model references resolve in `GameResident.sesetlist`; TotK embeds every model
  an effect uses in the file itself (no resident lookups at all). Mesh primitives (`mesh_primitive_idx`) work the same way through
  `PRMA` > `PRIM`, whose unique id is the first qword of the payload.
- Ids are shared between the games: 2,654 of the BotW references are ids that TotK files also carry. The two models the games share by
  name even have byte-identical vertex and index data, so the default is to embed TotK's own copy (already BFRES 10); only models TotK
  lacks are converted. The vfx engine reads a model through its first vertex buffer and shape (vertex attributes found by name) and
  never touches the material.
- BotW BFRES is 5.0.0.3, TotK's is 10.0.0.0. `BotwModelConverter` keeps the geometry byte for byte and does what Nintendo's conversion
  did (checked on the two shared-name models and 94 shared-id models): the source path is cleared, the skeleton's matrix lists become
  null, the bounding sphere is recomputed as the smallest sphere around the vertices (TotK stores a centre as well), and the material
  becomes the empty "dummy" material TotK gives effect models. Re-saving a BotW material as version 10 is not possible: BfresLibrary
  does not convert the two material layouts and its writer produces a file it cannot read back. Nintendo re-quantised some assets'
  positions (float x3 to half x4); both layouts exist in shipped TotK files, so models converted from BotW keep BotW's.
- `VfxbTail` splits the part of a file after `ESTA` into root nodes so `G3PR` and `PRMA` can be replaced: it recomputes sibling links, the
  `0x1000` alignment of the BFRES inside `G3PR` and the padding before the first `GRSN` child (the shader binary must start on a
  `0x1000` boundary). Rebuilding every shipped TotK file's `G3PR` and `PRMA` from its own parts reproduces the original bytes exactly.
- BfresLibrary needed three small fixes for version 10 (in the submodule, not yet upstream): relocation entries for empty render-info and
  parameter tables, materials without shader info (the dummy material is stored with a null shader-info pointer), and the model-level
  shader-assign list skipping such materials. All 243 sampled TotK G3D resources now load, save and reload, and every model of every BotW
  effect can be built (with TotK copies and with forced conversion). The saved layout is not byte-identical to Nintendo's (different
  block order, the file-name string), and **no converted file has been loaded in game**.

Not done yet: TotK to BotW, BotW effects missing from the database (a
signature needs the Marrow decompiler), compute-shader emitters beyond simple matching, and any in-game verification of the converted
files (donor choice and the snorm remap are hypotheses until tested in game).
