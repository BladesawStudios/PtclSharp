using System.Buffers.Binary;

namespace PtclSharp.Writer;

/// <summary>
/// The root nodes that follow the emitter-set tree (<c>PRMA</c>, <c>TRMA</c>, <c>G3PR</c>, <c>GRSN</c>, or <c>GRTF</c> in BotW) as separate
/// pieces, so one of them can be replaced. Every piece holds its node header and everything up to the next root; the relative
/// offsets inside a piece do not change when it moves, but three things depend on where it lands and are recomputed on output:
/// the <c>sibling_rel</c> of each root, the <c>0x1000</c> alignment of the G3D resource inside <c>G3PR</c>, and the padding in
/// front of the first child of <c>GRSN</c> (the shader binary must start on a <c>0x1000</c> boundary).
/// </summary>
public sealed class VfxbTail
{
    private const int NodeHeaderSize = 0x20;
    private const int DataAlignment = 0x1000;

    /// <summary>One root node. <see cref="Build"/>, when set, makes the bytes from the node's absolute position (it may depend on it).</summary>
    public sealed class Root
    {
        public Root(string kind, byte[] bytes)
        {
            Kind = kind;
            Bytes = bytes;
        }

        public Root(string kind, Func<int, byte[]> build)
        {
            Kind = kind;
            Bytes = [];
            Build = build;
        }

        public string Kind { get; }
        public byte[] Bytes { get; }
        public Func<int, byte[]>? Build { get; }
    }

    private readonly List<Root> _roots;

    private VfxbTail(List<Root> roots, int originalOffset)
    {
        _roots = roots;
        OriginalOffset = originalOffset;
    }

    public IReadOnlyList<Root> Roots => _roots;

    /// <summary>Where the tail started in the source file; absolute positions are only needed modulo <c>0x1000</c>.</summary>
    public int OriginalOffset { get; }

    public Root? Find(string kind) => _roots.FirstOrDefault(r => r.Kind == kind);

    public static VfxbTail Parse(byte[] tail, int originalOffset)
    {
        var roots = new List<Root>();
        int position = 0;
        while (position + NodeHeaderSize <= tail.Length)
        {
            string kind = System.Text.Encoding.ASCII.GetString(tail, position, 4);
            int sibling = BinaryPrimitives.ReadInt32LittleEndian(tail.AsSpan(position + 0xC));
            int end = sibling == -1 ? tail.Length : position + sibling;
            if (end <= position || end > tail.Length) throw new InvalidDataException($"Tail node {kind} at 0x{position:X} has a bad sibling offset.");
            roots.Add(new Root(kind, tail.AsSpan(position, end - position).ToArray()));
            if (sibling == -1) break;
            position = end;
        }
        return new VfxbTail(roots, originalOffset);
    }

    /// <summary>A tail with <paramref name="replacement"/> in place of the root of the same kind, or appended before <c>GRSN</c> when there is none.</summary>
    public VfxbTail Replace(Root replacement)
    {
        var roots = new List<Root>(_roots);
        int index = roots.FindIndex(r => r.Kind == replacement.Kind);
        if (index >= 0)
        {
            roots[index] = replacement;
        }
        else
        {
            int shaders = roots.FindIndex(r => r.Kind == "GRSN");
            roots.Insert(shaders >= 0 ? shaders : roots.Count, replacement);
        }
        return new VfxbTail(roots, OriginalOffset);
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        var starts = new List<int>();
        for (int i = 0; i < _roots.Count; i++)
        {
            Root root = _roots[i];
            int start = (int)ms.Position;
            starts.Add(start);
            byte[] bytes = root.Build is { } build ? build(OriginalOffset + start) : root.Bytes;
            if (root.Kind == "GRSN") bytes = RepadShaderNode(bytes, OriginalOffset + start);
            ms.Write(bytes);
            while ((ms.Position & 0xF) != 0 && i + 1 < _roots.Count) ms.WriteByte(0);
        }

        byte[] result = ms.ToArray();
        for (int i = 0; i < _roots.Count; i++)
        {
            int sibling = i + 1 < _roots.Count ? starts[i + 1] - starts[i] : -1;
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(starts[i] + 0xC), sibling);
        }
        return result;
    }

    /// <summary>
    /// <c>GRSN</c> keeps its children at a distance that puts the first child's data on a <c>0x1000</c> boundary of the file; moving
    /// the node needs different padding between its header and that child. The first child starts at the smallest position after the
    /// header whose data (0x20 further) is aligned.
    /// </summary>
    private static byte[] RepadShaderNode(byte[] node, int absoluteStart)
    {
        int childRel = BinaryPrimitives.ReadInt32LittleEndian(node.AsSpan(8));
        // Only a first child that carries a shader binary needs the aligned data; an empty one (files without graphics shaders) does not.
        // (BotW's GRSN has its own data far from the header and is never moved by this class, so it is left alone.)
        if (BinaryPrimitives.ReadInt32LittleEndian(node.AsSpan(0x14)) != NodeHeaderSize) return node;
        if (childRel == -1 || childRel + 8 > node.Length || BinaryPrimitives.ReadUInt32LittleEndian(node.AsSpan(childRel + 4)) == 0) return node;

        int firstChild = absoluteStart + NodeHeaderSize;
        int dataAt = firstChild + NodeHeaderSize;
        dataAt = (dataAt + DataAlignment - 1) & ~(DataAlignment - 1);
        int newChildRel = dataAt - NodeHeaderSize - absoluteStart;
        if (newChildRel == childRel) return node;

        byte[] result = new byte[newChildRel + (node.Length - childRel)];
        node.AsSpan(0, NodeHeaderSize).CopyTo(result);
        node.AsSpan(childRel).CopyTo(result.AsSpan(newChildRel));
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), newChildRel);
        // data_rel of the node itself (0x20) and its own size field are untouched; its children keep their relative links.
        return result;
    }

    /// <summary>
    /// Builds a <c>PRMA</c> root from complete <c>PRIM</c> nodes (header and data, as found in either game: the node layout is the same).
    /// Each node's sibling link is rewritten; the root's size is the extent of its children, as in the shipped files.
    /// </summary>
    public static Root BuildPrimitives(IReadOnlyList<byte[]> primitives)
    {
        var node = new byte[NodeHeaderSize];
        "PRMA"u8.CopyTo(node);
        BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(8), -1);
        BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(0x10), -1);
        BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(0x14), -1);
        if (primitives.Count == 0) return new Root("PRMA", node);

        using var ms = new MemoryStream();
        ms.Write(node);
        for (int i = 0; i < primitives.Count; i++)
        {
            byte[] prim = (byte[])primitives[i].Clone();
            int extent = (prim.Length + 3) & ~3;
            BinaryPrimitives.WriteInt32LittleEndian(prim.AsSpan(0xC), i + 1 < primitives.Count ? extent : -1);
            ms.Write(prim);
            for (int pad = prim.Length; pad < extent; pad++) ms.WriteByte(0);
        }
        byte[] result = ms.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - NodeHeaderSize));
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), NodeHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0x14), NodeHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x1C), (ushort)primitives.Count);
        return new Root("PRMA", result);
    }

    /// <summary>
    /// Builds a <c>G3PR</c> root holding <paramref name="g3dResource"/> (a complete FRES file) and one <c>G3NT</c> entry of 0x18 bytes
    /// per model. With no resource it builds the empty form shipped TotK files use when no model is needed.
    /// </summary>
    public static Root BuildG3d(byte[]? g3dResource, IReadOnlyList<byte[]> entries)
    {
        return new Root("G3PR", absoluteStart =>
        {
            var node = new byte[NodeHeaderSize];
            "G3PR"u8.CopyTo(node);
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(8), -1);
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(0x10), -1);
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(0x14), -1);
            BinaryPrimitives.WriteUInt16LittleEndian(node.AsSpan(0x1C), 1); // the corpus has 1 here even for an empty G3PR

            if (g3dResource is null || g3dResource.Length == 0)
                return node;

            int tableSize = entries.Count * 0x18;
            int tableExtent = (NodeHeaderSize + tableSize + 0xF) & ~0xF;
            int dataAt = absoluteStart + NodeHeaderSize + tableExtent;
            dataAt = (dataAt + DataAlignment - 1) & ~(DataAlignment - 1);
            int dataRel = dataAt - absoluteStart;

            BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), (uint)g3dResource.Length);
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(8), NodeHeaderSize);
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(0x14), dataRel);

            var result = new byte[dataRel + g3dResource.Length];
            node.CopyTo(result, 0);
            var g3nt = result.AsSpan(NodeHeaderSize, tableExtent);
            "G3NT"u8.CopyTo(g3nt);
            BinaryPrimitives.WriteUInt32LittleEndian(g3nt[4..], (uint)tableSize);
            BinaryPrimitives.WriteInt32LittleEndian(g3nt[8..], -1);
            BinaryPrimitives.WriteInt32LittleEndian(g3nt[0xC..], tableExtent);
            BinaryPrimitives.WriteInt32LittleEndian(g3nt[0x10..], -1);
            BinaryPrimitives.WriteInt32LittleEndian(g3nt[0x14..], NodeHeaderSize);
            for (int i = 0; i < entries.Count; i++)
            {
                Span<byte> entry = g3nt.Slice(NodeHeaderSize + (i * 0x18), 0x18);
                entries[i].AsSpan(0, 0x18).CopyTo(entry);
                // The chain word at +8 is the distance to the next entry; the last one ends the chain.
                BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], i + 1 < entries.Count ? 0x18u : 0u);
            }
            g3dResource.CopyTo(result, dataRel);
            return result;
        });
    }
}
