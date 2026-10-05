namespace PtclSharp.Layout;

/// <summary>Small helpers that keep the hand-written layout tables short and uniform.</summary>
internal static class LayoutBuilders
{
    internal static FieldDef Def(string name, int offset, FieldType type, int count, FieldStatus status, string evidence)
        => new(name, offset, type, count, status, evidence);

    internal static FieldDef U8(string n, int o, FieldStatus s, string e, int count = 1) => new(n, o, FieldType.U8, count, s, e);
    internal static FieldDef U16(string n, int o, FieldStatus s, string e) => new(n, o, FieldType.U16, 1, s, e);
    internal static FieldDef U32(string n, int o, FieldStatus s, string e) => new(n, o, FieldType.U32, 1, s, e);
    internal static FieldDef I32(string n, int o, FieldStatus s, string e) => new(n, o, FieldType.I32, 1, s, e);
    internal static FieldDef U64(string n, int o, FieldStatus s, string e) => new(n, o, FieldType.U64, 1, s, e);
    internal static FieldDef F32(string n, int o, FieldStatus s, string e, int count = 1) => new(n, o, FieldType.F32, count, s, e);
    internal static FieldDef Raw(string n, int o, int length, FieldStatus s, string e) => new(n, o, FieldType.Bytes, length, s, e);
    internal static FieldDef Str(string n, int o, int length, FieldStatus s, string e) => new(n, o, FieldType.String, length, s, e);

    internal const FieldStatus C = FieldStatus.Confirmed;
    internal const FieldStatus P = FieldStatus.Paired;
    internal const FieldStatus V = FieldStatus.Unverified;
    internal const FieldStatus U = FieldStatus.Unused;

    /// <summary>
    /// The shared 8-key animation sub-block used by the field chunks (0x98 bytes at <paramref name="at"/>):
    /// enable, loop, start_random, key_count, loop_num, interpolation, then eight (x, y, z, time) keys.
    /// </summary>
    internal static IEnumerable<FieldDef> Anim8Key(string prefix, int at, string evidence)
    {
        yield return U32(prefix + "_enable", at + 0x00, C, evidence);
        yield return U32(prefix + "_loop", at + 0x04, C, evidence);
        yield return U32(prefix + "_start_random", at + 0x08, C, evidence);
        yield return I32(prefix + "_key_count", at + 0x0C, C, evidence);
        yield return I32(prefix + "_loop_num", at + 0x10, C, evidence);
        yield return U32(prefix + "_interpolation", at + 0x14, C, evidence);
        yield return F32(prefix + "_keys", at + 0x18, C, evidence, count: 32);
    }

    /// <summary>
    /// The BotW (nn::vfx) 8-key animation sub-block of the field chunks (0x94 bytes at <paramref name="at"/>): enable, loop,
    /// start_random, key_count, loop_num, then eight (x, y, z, time) keys. It has no interpolation word.
    /// </summary>
    internal static IEnumerable<FieldDef> Anim8KeyV20(string prefix, int at, string evidence)
    {
        yield return U32(prefix + "_enable", at + 0x00, C, evidence);
        yield return U32(prefix + "_loop", at + 0x04, C, evidence);
        yield return U32(prefix + "_start_random", at + 0x08, C, evidence);
        yield return I32(prefix + "_key_count", at + 0x0C, C, evidence);
        yield return I32(prefix + "_loop_num", at + 0x10, C, evidence);
        yield return F32(prefix + "_keys", at + 0x14, C, evidence, count: 32);
    }

    /// <summary>The generic TotK/BotW node header (0x20 bytes that precede every node).</summary>
    internal static StructLayout NodeHeader(FieldStatus confirmedStatus) => new("NodeHeader", 0x20,
    [
        Str("fourcc", 0x00, 4, confirmedStatus, "Resource::Trace compares this word against each node kind."),
        U32("size", 0x04, confirmedStatus, "Read by the loaders (shader file size, attribute payload size = size - 0x20); meaning depends on node kind."),
        I32("child_rel", 0x08, confirmedStatus, "Offset to the first child node; 0xFFFFFFFF means none."),
        I32("sibling_rel", 0x0C, confirmedStatus, "Offset to the next sibling node; 0xFFFFFFFF ends the chain."),
        I32("attribute_rel", 0x10, confirmedStatus, "Offset to the first attribute chunk (ResolveBinaryData walks this chain)."),
        I32("data_rel", 0x14, confirmedStatus, "Offset to the node's data block."),
        U32("unverified_18", 0x18, V, "Always 0 in the shipped corpus; not read by the traced loaders."),
        U16("child_count", 0x1C, V, "Corpus: number of child nodes (nested EMTR count for EMTR; 1 for G3PR even when it has no child); not read by the traced loaders."),
        U16("unverified_1E", 0x1E, V, "Always 0 in the shipped corpus.")
    ]);
}
