using System.Buffers.Binary;
using System.Text;

namespace PtclSharp.Writer;

/// <summary>
/// One node of the editable emitter-set tree (<c>ESTA</c>, <c>ESET</c>, <c>EMTR</c> and attribute chunks). The writer
/// recomputes every offset and size from the tree; the fields kept here are the ones that are plain data.
/// </summary>
public sealed class VfxbTreeNode
{
    public VfxbTreeNode(string kind) => Kind = kind;

    /// <summary>Four-character node kind (<c>EMTR</c>, <c>FCSF</c>, ...).</summary>
    public string Kind { get; }

    /// <summary>Header word at +0x18 (always 0 in the corpus, kept for fidelity).</summary>
    public uint Reserved18 { get; set; }

    /// <summary>Header halfword at +0x1C: the number of child nodes the game's tools declare (see <see cref="VfxbDocument.RecountChildren"/>).</summary>
    public ushort ChildCountField { get; set; }

    /// <summary>Header halfword at +0x1E (always 0 in the corpus).</summary>
    public ushort Reserved1E { get; set; }

    /// <summary>The node's data block, or null when the node has none (data_rel = -1).</summary>
    public byte[]? Data { get; set; }

    public List<VfxbTreeNode> Attributes { get; } = [];
    public List<VfxbTreeNode> Children { get; } = [];

    /// <summary>EMTR data blocks start on an absolute 0x100 boundary; every other node's data follows its header directly.</summary>
    internal int DataAlignment => Kind == "EMTR" ? 0x100 : 1;

    public VfxbTreeNode DeepClone()
    {
        var copy = new VfxbTreeNode(Kind)
        {
            Reserved18 = Reserved18,
            ChildCountField = ChildCountField,
            Reserved1E = Reserved1E,
            Data = Data?.ToArray()
        };
        foreach (VfxbTreeNode a in Attributes) copy.Attributes.Add(a.DeepClone());
        foreach (VfxbTreeNode c in Children) copy.Children.Add(c.DeepClone());
        return copy;
    }

    public override string ToString() => $"{Kind} data={Data?.Length.ToString() ?? "-"} attrs={Attributes.Count} children={Children.Count}";
}

/// <summary>
/// An editable VFXB file: the emitter-set tree (<c>ESTA</c> and everything below it) as mutable nodes, plus the rest of the
/// file (shader archives, primitives, G3D data) kept as an opaque tail that is copied verbatim. Writing an unmodified
/// document reproduces the original bytes exactly.
/// </summary>
public sealed class VfxbDocument
{
    private const int FileHeaderSize = 0x40;
    private const int NodeHeaderSize = 0x20;
    private const int TailAlignment = 0x1000;

    private VfxbDocument(VfxbLayout layout, byte[] fileHeader, VfxbTreeNode esta, byte[] tail, int originalTailOffset)
    {
        Layout = layout;
        FileHeader = fileHeader;
        Esta = esta;
        Tail = tail;
        OriginalTailOffset = originalTailOffset;
    }

    public VfxbLayout Layout { get; }

    /// <summary>The first 0x40 bytes of the file (the size field is rewritten on output).</summary>
    public byte[] FileHeader { get; }

    /// <summary>The <c>ESTA</c> root: its children are the <c>ESET</c> nodes.</summary>
    public VfxbTreeNode Esta { get; }

    /// <summary>Every byte after the emitter-set tree: <c>GRTF</c>, <c>PRMA</c>, <c>G3PR</c>, <c>GRSN</c> and their data.</summary>
    public byte[] Tail { get; set; }

    /// <summary>Where <see cref="Tail"/> started in the source file; the writer keeps new positions congruent modulo 0x1000.</summary>
    public int OriginalTailOffset { get; }

    /// <summary>The tail split into its root nodes, for replacing one of them (see <see cref="VfxbTail"/>); assign <c>ToBytes()</c> back to <see cref="Tail"/>.</summary>
    public VfxbTail ParseTail() => VfxbTail.Parse(Tail, OriginalTailOffset);

    /// <summary>A new document with <paramref name="sets"/> as the emitter sets, keeping this document's header and tail (a donor file).</summary>
    public VfxbDocument WithSets(IEnumerable<VfxbTreeNode> sets)
    {
        var esta = new VfxbTreeNode("ESTA") { Data = [] };
        esta.Children.AddRange(sets);
        return new VfxbDocument(Layout, (byte[])FileHeader.Clone(), esta, (byte[])Tail.Clone(), OriginalTailOffset);
    }

    public IEnumerable<VfxbTreeNode> Sets => Esta.Children.Where(c => c.Kind == "ESET");

    public static VfxbDocument From(VfxbFile file)
    {
        VfxbNode estaNode = file.Roots.FirstOrDefault(r => r.Kind == "ESTA")
            ?? throw new InvalidDataException("The file has no ESTA node.");
        if (file.Roots[0] != estaNode)
            throw new InvalidDataException("ESTA is not the first root node; the tail model does not apply.");
        int tailOffset = file.Roots.Count > 1 ? file.Roots[1].Offset : file.Data.Length;

        byte[] header = file.Data.AsSpan(0, FileHeaderSize).ToArray();
        VfxbTreeNode esta = Convert(file.Data, estaNode, tailOffset);
        return new VfxbDocument(file.Layout, header, esta, file.Data.AsSpan(tailOffset).ToArray(), tailOffset);
    }

    private static VfxbTreeNode Convert(byte[] data, VfxbNode n, int limit)
    {
        var node = new VfxbTreeNode(n.Kind)
        {
            Reserved18 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(n.Offset + 0x18)),
            ChildCountField = n.DeclaredChildCount,
            Reserved1E = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(n.Offset + 0x1E))
        };

        int end = n.Offset + checked((int)n.Size);
        int firstBlock = end;
        foreach (VfxbNode a in n.Attributes) firstBlock = Math.Min(firstBlock, a.Offset);
        foreach (VfxbNode c in n.Children) firstBlock = Math.Min(firstBlock, c.Offset);
        if (n.DataOffset is int dataOffset)
        {
            if (firstBlock < dataOffset) firstBlock = end; // the data comes after the children (not seen in the corpus)
            node.Data = data.AsSpan(dataOffset, firstBlock - dataOffset).ToArray();
        }
        foreach (VfxbNode a in n.Attributes) node.Attributes.Add(Convert(data, a, limit));
        foreach (VfxbNode c in n.Children) node.Children.Add(Convert(data, c, limit));
        return node;
    }

    /// <summary>Serializes the document.</summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        ms.Write(FileHeader);
        int estaStart = (int)ms.Position;
        EmitNode(ms, Esta);
        int end = (int)ms.Position;

        int minimum = (end + 0xF) & ~0xF;
        int tailStart = Tail.Length == 0
            ? minimum
            : OriginalTailOffset - (((OriginalTailOffset - minimum) / TailAlignment) * TailAlignment);
        if (tailStart < minimum) tailStart += TailAlignment;
        while (ms.Position < tailStart) ms.WriteByte(0);
        ms.Write(Tail);

        byte[] result = ms.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x1C), (uint)result.Length);
        if (Tail.Length > 0)
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(estaStart + 0xC), tailStart - estaStart);
        else
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(estaStart + 0xC), -1);
        return result;
    }

    private static void EmitNode(MemoryStream ms, VfxbTreeNode node)
    {
        int start = (int)ms.Position;
        Span<byte> header = stackalloc byte[NodeHeaderSize];
        header.Clear();
        Encoding.ASCII.GetBytes(node.Kind, header);
        ms.Write(header);

        int dataRel = -1;
        if (node.Data is { } data)
        {
            int dataStart = (start + NodeHeaderSize + node.DataAlignment - 1) / node.DataAlignment * node.DataAlignment;
            while (ms.Position < dataStart) ms.WriteByte(0);
            dataRel = dataStart - start;
            ms.Write(data);
        }

        int attrRel = node.Attributes.Count > 0 ? (int)ms.Position - start : -1;
        EmitChain(ms, node.Attributes);
        int ownEnd = (int)ms.Position;
        int childRel = node.Children.Count > 0 ? ownEnd - start : -1;
        EmitChain(ms, node.Children);

        int end = (int)ms.Position;
        // An EMTR's size covers only its own header, data and attributes (nested child emitters follow it and are
        // reached through child_rel); the set and root nodes count everything below them.
        int size = node.Kind == "EMTR" ? ownEnd - start : end - start;
        long save = ms.Position;
        ms.Position = start;
        ms.Position = start + 4;
        WriteInt(ms, size);
        WriteInt(ms, childRel);
        WriteInt(ms, -1); // sibling, patched by the chain emitter
        WriteInt(ms, attrRel);
        WriteInt(ms, dataRel);
        WriteInt(ms, (int)node.Reserved18);
        WriteInt(ms, node.ChildCountField | (node.Reserved1E << 16));
        ms.Position = save;
    }

    private static void EmitChain(MemoryStream ms, List<VfxbTreeNode> chain)
    {
        var starts = new List<int>(chain.Count);
        foreach (VfxbTreeNode child in chain)
        {
            starts.Add((int)ms.Position);
            EmitNode(ms, child);
        }
        long save = ms.Position;
        for (int i = 0; i < chain.Count - 1; i++)
        {
            ms.Position = starts[i] + 0xC;
            WriteInt(ms, starts[i + 1] - starts[i]);
        }
        ms.Position = save;
    }

    private static void WriteInt(MemoryStream ms, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        ms.Write(b);
    }

    /// <summary>
    /// Recomputes the child-count fields of <c>ESTA</c> (number of sets), every <c>ESET</c> (number of its direct emitter nodes)
    /// and every <c>EMTR</c> (number of nested emitters), and the emitter count stored in each set's data block.
    /// </summary>
    public void RecountChildren()
    {
        Esta.ChildCountField = (ushort)Esta.Children.Count;
        foreach (VfxbTreeNode set in Sets)
        {
            set.ChildCountField = (ushort)set.Children.Count;
            foreach (VfxbTreeNode emitter in set.Children) emitter.ChildCountField = (ushort)emitter.Children.Count;
            int total = CountEmitters(set);
            if (set.Data is { } d)
            {
                if (Layout.EmitterSetEmitterCountSize == sizeof(ushort))
                    BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(Layout.EmitterSetEmitterCountOffset), (ushort)total);
                else
                    BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(Layout.EmitterSetEmitterCountOffset), total);
            }
        }
    }

    /// <summary>Number of EMTR nodes below <paramref name="set"/>, including nested child emitters (what the set's count field holds).</summary>
    public static int CountEmitters(VfxbTreeNode set)
    {
        int n = 0;
        void Walk(VfxbTreeNode x)
        {
            foreach (VfxbTreeNode c in x.Children)
            {
                if (c.Kind == "EMTR") n++;
                Walk(c);
            }
        }
        Walk(set);
        return n;
    }
}
