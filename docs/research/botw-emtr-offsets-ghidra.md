# BotW `EMTR` executable evidence

This note records facts directly confirmed in the open BotW executable with
Ghidra. Offsets are relative to the serialized EMTR node's data start.

## Resource-tree and allocation facts

`nn::vfx::Resource::TraceEmitterSetArray` at `0x7100ae3c98` counts direct
`ESET` nodes on the `ESTA` sibling chain and allocates one runtime set record
of `0x30` bytes per set.

The per-set helper at `0x7100ae44ec` confirms:

- ESET data is `ESET.node + dataRelativeOffset`;
- the serialized ESET emitter count is a signed 32-bit value at `ESET.data + 0x50`;
- direct EMTR children are counted from the ESET child chain;
- the runtime emitter allocation uses `0x3E0` bytes per emitter plus a `0x20`
  byte header;
- each EMTR's serialized pointer is the node data start and its resource body
  begins at `EMTR.data + 0x50`;
- child EMTR nodes use the same representation as parent EMTR nodes;
- `EmitterResource::ResolveBinaryData` is called for every parent and child.

`nn::vfx::Resource::SearchEmitterSetId` at `0x7100ae4a10` compares the search
string with `ESET.data + 0x10`.

## Confirmed attribute handlers

`nn::vfx::EmitterResource::ResolveBinaryData` at `0x7100adcca8` walks the
attribute chain and recognizes these FourCCs:

```text
EAA0 EAA1 EAC0 EAC1 EAPL EASL EAER EADV EAGV EAOV
EATR EAES EAET EASS EP01 EP02 EP03 EP04
FRN1 FRND FPAD FCSF FMAG FCOL FCLN FSPN
CADP CSDP CUDP
```

The handler stores resolved payload pointers in the runtime
`EmitterResource`. `CSDP` additionally stores its payload size as
`nodeSize - 0x20`. `EP01`–`EP04` select plugin modes 1 through 4.

No BotW handler for TotK's confirmed `FCOV` or `FGWD` tags appears in this
function. That is runtime evidence for a capability warning, not proof that a
different BotW code path could never consume such data.

## Graphics-resource facts

`nn::vfx::Resource::InitializeEmitterGraphicsResource` at `0x7100ae3e54`
confirms that BotW resolves three graphics-resource references from the
serialized emitter body at offsets `+0x9F8`, `+0xA18`, and `+0xA38`.

It also checks serialized values at `+0x878`, `+0x8A9`, and `+0x8D0` while
initializing optional graphics resources. Their authoring-facing names are not
established by this trace and should remain unnamed in the normalized model.

## Deliberately unresolved

The following need additional BotW traces before they become semantic model
properties:

- particle-life, emission timing, and volume fields;
- static color, scale, rotation, and velocity fields;
- animation payload layouts inside the `EA*` chunks;
- exact texture-slot serialization;
- the meaning of the graphics-resource values around `+0x878`–`+0xA38`.

Until then, PtclSharp should preserve those bytes and report conversion
limitations instead of guessing their meanings.
