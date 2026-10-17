# PtclSharp

A .NET library for NintendoWare VFX resources used by
Breath of the Wild and Tears of the Kingdom.

See [docs/simulation-viewer.md](docs/simulation-viewer.md) for the BotW emitter simulation and the effect viewer.

Pokemon Legends: Z-A effects (VFXB binary version 22) read as a node tree with `VfxbReader.ReadTree`: header, nodes,
data blocks and emitter set / emitter names, but no typed emitter layout yet. See
[docs/format-differences.md](docs/format-differences.md#pokemon-legends-z-a-binary-version-22).

Corpus tests run against game files when these are set: `PTCL_BOTW_ROM`, `PTCL_TOTK_ROMFS`, `PTCL_BOTW_WIIU_ROM`, and
`PTCL_ZA_EFFECTS` (a folder of extracted Z-A `.ptcl` files).
