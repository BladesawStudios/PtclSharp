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

Not done yet: TotK to BotW, carrying `PRMA`/`G3PR` primitives across, texture archive handling, and any in-game verification of the
converted files.
