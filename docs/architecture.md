# PtclSharp architecture

PtclSharp treats a game particle file as three layers instead of one monolithic
format:

```text
game file
  -> transport compression
  -> game container
  -> VFXB resource tree
  -> version layout
  -> confirmed semantic properties
```

## Versions observed

| Game | Runtime SDK | VFXB binary version | Outer file |
| --- | --- | ---: | --- |
| Breath of the Wild | `nn::vfx` 4.4.0 | 20 (`0x14`) | Yaz0-compressed raw VFXB (`.sesetlist`) |
| Tears of the Kingdom | `nn::vfx2` 15.3.1 | 51 (`0x33`) | dictionary-Zstandard BYML (`.esetb.byml.zs`) |

The runtime SDK version and VFXB binary version are different concepts and must
remain separate in the API.

## Confirmed shared VFXB structure

The six example files share the `VFXB    ` signature and a 32-byte binary-file
header. The first resource node is selected by the header's 16-bit offset at
`0x16`. Resource nodes have a 32-byte header:

| Offset | Size | Meaning |
| ---: | ---: | --- |
| `0x00` | 4 | FourCC |
| `0x04` | 4 | node size |
| `0x08` | 4 | child offset, relative to this node (`-1` = none) |
| `0x0C` | 4 | sibling offset, relative to this node (`-1` = none) |
| `0x10` | 4 | attribute offset, relative to this node (`-1` = none) |
| `0x14` | 4 | data offset, relative to this node (`-1` = none) |
| `0x1C` | 2 | declared child count |

The BotW runtime decompilation independently confirms the relative child,
sibling, attribute, and data pointers. It walks the top-level sibling chain,
finds `ESTA`, counts `ESET` children, then counts `EMTR` children within each
set. `EmitterResource::ResolveBinaryData` walks the attribute chain and selects
optional animation/field resources by FourCC.

Known hierarchy:

```text
VFXB
  ESTA
    ESET
      EMTR
        attribute chunks (CADP, CSDP, EAA0, EAA1, ...)
  GRTF (BotW texture/resource table)
  PRMA
  TRMA (TotK samples)
  G3PR
  GRSN
```

## Confirmed version-layout differences

| Field | BotW | TotK |
| --- | ---: | ---: |
| ESET fixed data size | `0x60` | `0xB4` |
| ESET emitter count | signed 32-bit at data + `0x50` | unsigned 16-bit at data + `0x70` |
| EMTR fixed data size | `0xA88` | `0x10C8` |
| EMTR `ResEmitter` body | data + `0x50` | data + `0x70` |
| ESET/EMTR name | data + `0x10`, max `0x40` bytes | same |

BotW offsets above are confirmed both by samples and the executable. TotK
offsets currently have agreement across all three samples and should be checked
against `nn::vfx2` in the TotK executable before they graduate from observed
layout facts to runtime-confirmed facts.

## TotK outer container

After dictionary-Zstandard decompression, the BYML root contains:

- `Esets`: emitter-set names;
- `Textures`: maps with `name` and `guid`;
- `PtclBin`: aligned VFXB binary data (alignment 4096 in all three samples).

`PtclBin` is a BYML binary value, not intrinsically base64. Text-oriented BYML
tools normally render that byte string as base64; decoding that representation
produces the raw `VFXB    ` bytes.

All three provided TotK files use Zstandard dictionary ID 1. A dictionary must
therefore be supplied explicitly; silently using dictionary-less Zstandard is
not correct for these assets.

## TotK executable confirmations

The `nn::vfx2` loader in the TotK executable confirms:

- ESET total emitter count is an unsigned 16-bit value at data + `0x70`;
- an EMTR's serialized `ResEmitter` begins at node data + `0x70`;
- parent and child EMTR nodes use the same `ResEmitter` representation;
- six texture-sampler GUIDs are stored at EMTR data offsets `0xF98`, `0xFB0`,
  `0xFC8`, `0xFE0`, `0xFF8`, and `0x1010`;
- `0xFFFFFFFFFFFFFFFF` denotes an unused texture slot.

Every non-null GUID observed at those six offsets resolves to a GUID in the
outer BYML `Textures` array across the three examples. The outer GUID is stored
as a BYML unsigned 32-bit value; the EMTR slot is 64-bit.

TotK's `EmitterResource::ResolveBinaryData` recognizes the following EMTR
attribute FourCCs. The semantic names are corroborated by the matching
NintendoWare VFX source implementation:

| FourCC | Meaning |
| --- | --- |
| `FRND` / `FRN1` | GPU-noise random field / legacy simple random field |
| `FMAG` | magnetic field |
| `FSPN` | spin field |
| `FCOL` | simple collision field |
| `FCOV` | convergence field |
| `FCLN` | curl-noise field |
| `FPAD` | position-add field |
| `FCSF` | custom field |
| `EP01`–`EP04` | connection stripe, stripe, super stripe, and area-loop plugins |
| `CUDP` | custom-data parameters |
| `CADP` | custom-action parameters |
| `CSDP` | custom-shader parameters |
| `EASL` | particle-scale animation |
| `EAES`, `EAER`, `EAET` | emitter scale, rotation, and translation animation |
| `EATR` | emission-rate animation |
| `EAOV`, `EADV`, `EAGV` | all-direction velocity, directed velocity, and gravity-scale animation |
| `EAPL` | particle-life animation |
| `EAC0`, `EAC1` | color 0 and color 1 animation |
| `EAA0`, `EAA1` | alpha 0 and alpha 1 animation |
| `EASS` | emitter-volume-scale animation |

`FGWD` is additionally recognized by TotK but is absent from the available older
VFX source. Its runtime treatment proves that it is a field block; “global wind”
is a plausible expansion of the tag, but remains a hypothesis and is therefore
not exposed under that semantic name.

## API policy

`PtclFile` owns the outer game container. `VfxbFile` owns the common resource
tree. `VfxbLayout` contains only offsets that differ by binary generation.

Unknown bytes and nodes remain present in the original decoded data and raw node
tree. Future writers should patch confirmed fields into a copy of those bytes.
They should not reconstruct EMTR blocks from a partial semantic model, because
that would erase unknown state.

Cross-version conversion is a separate operation from serialization. In
particular, `GRSN` shader resources are generation-specific, so a future
converter needs an explicit TotK donor/resource policy. It must not imply that a
BotW document can simply be saved as TotK.

## Evidence bar for new fields

A semantic field should enter the public object model only after at least one of:

1. executable code reads or writes the exact offset with identifiable behavior;
2. matching SDK source names the field and the binary offset is independently
   established;
3. controlled edits produce the predicted in-game result and agree across more
   than one sample.

Until then, preserve it as raw data and record the hypothesis in research notes,
not as a confidently named API property.
