# BotW and TotK format boundaries

The two games share the VFXB resource-tree concept, but their serialized
layouts and outer containers are different generations.

| Property | BotW | TotK |
| --- | --- | --- |
| Runtime | `nn::vfx` | `nn::vfx2` |
| SDK version observed | 4.4.0 | 15.3.1 |
| VFXB binary version | 20 (`0x14`) | 51 (`0x33`) |
| Outer container | Yaz0-compressed raw VFXB `.sesetlist` | dictionary-Zstandard BYML `.esetb` |
| Embedded binary field | raw VFXB payload | BYML `PtclBin` binary value, often displayed as base64 |
| ESET fixed data | `0x60` | `0xB4` |
| ESET emitter count | signed 32-bit at data `+0x50` | unsigned 16-bit at data `+0x70` |
| EMTR fixed data | `0xA88` | `0x10C8` |
| EMTR resource body | data `+0x50` | data `+0x70` |

## Shared node structure

Both loaders use 32-byte resource-node headers with relative child, sibling,
attribute, and data offsets. The common hierarchy is:

```text
VFXB
  ESTA
    ESET
      EMTR
        attribute chunks
```

The executable traces confirm that both generations walk ESET and EMTR sibling
chains, resolve attribute chunks by FourCC, and use the name at serialized data
offset `+0x10` for emitter-set lookup.

## Why conversion needs a target writer

Even when an attribute FourCC exists in both games, the surrounding EMTR body,
runtime resource layout, texture references, graphics resources, and container
transport are version-specific. Conversion therefore needs:

```text
source reader -> normalized model -> target writer
```

and not a raw block copy.

Unknown bytes should be preserved for same-version editing. During cross-version
conversion they remain attached to the source document and are reported if the
target writer cannot represent them.
