using static PtclSharp.Layout.LayoutBuilders;

namespace PtclSharp.Layout;

/// <summary>
/// Breath of the Wild (<c>nn::vfx</c>, VFXB version 20). The EMTR table is generated from
/// docs/research/botw-emtr-offsets-ghidra.md and has not been re-verified against the live Switch binary:
/// rows whose name also exists in the TotK map are <see cref="FieldStatus.Paired"/>, the rest are
/// <see cref="FieldStatus.Unverified"/>. No attribute-chunk payload layouts are mapped for BotW yet.
/// </summary>
internal static class BotwLayouts
{
    internal static PtclLayoutSet Create() => new(
        PtclVersion.BotW_NintendoWareVfx_4_4_0,
        VfxbLayouts.BotW,
        FileHeader(),
        NodeHeader(P),
        EmitterSet(),
        new EmitterLayout("EMTR", 0xA88, BotwEmitterFields.All, textureSlotCount: 3, keyframeTrackCount: null),
        []);

    private static StructLayout FileHeader() => new("VfxbFileHeader", 0x40,
    [
        Str("signature", 0x00, 8, P, "Same standard header as TotK; BotW sample files agree."),
        U8("version_micro", 0x08, V, "Standard header field."),
        U8("graphics_api", 0x09, V, "BotW graphics API value not verified."),
        U16("binary_version", 0x0A, P, "VFXB version 20 for BotW (read by the PtclSharp reader)."),
        U16("byte_order_mark", 0x0C, V, "Checked by the reader."),
        U8("alignment_shift", 0x0E, V, "Standard header field."),
        U8("target_address_size", 0x0F, V, "Standard header field."),
        U32("file_name_offset", 0x10, V, "Standard header field."),
        U16("flag", 0x14, V, "Standard header field."),
        U16("first_block_offset", 0x16, V, "The reader starts the node chain here."),
        U32("relocation_table_offset", 0x18, V, "Standard header field."),
        U32("file_size", 0x1C, V, "Checked by the reader."),
        Str("file_name", 0x20, 0x20, V, "Zero-padded file name string.")
    ]);

    private static StructLayout EmitterSet() => new("ESET", 0x60,
    [
        Str("name", 0x10, 0x40, C, "Resource::SearchEmitterSetId 0x7100AE4A10."),
        I32("emitter_count", 0x50, C, "InitializeEmitterSetResource 0x7100AE44EC (signed 32-bit).")
    ]);
}
