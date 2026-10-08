using System.Buffers.Binary;
using System.Text;
using PtclSharp.Layout;

namespace PtclSharp;

public sealed record VfxbLayout(
    PtclVersion RuntimeVersion,
    ushort BinaryVersion,
    int EmitterSetEmitterCountOffset,
    int EmitterSetEmitterCountSize,
    int EmitterSetFixedDataSize,
    int EmitterFixedDataSize,
    int EmitterResourceOffset,
    IReadOnlyList<int> TextureSamplerGuidOffsets);

public static class VfxbLayouts
{
    public static VfxbLayout BotW { get; } = new(
        PtclVersion.BotW_NintendoWareVfx_4_4_0,
        20,
        0x50,
        sizeof(int),
        0x60,
        0xA88,
        0x50,
        [0x9F8, 0xA18, 0xA38]);

    public static VfxbLayout TotK { get; } = new(
        PtclVersion.TotK_NintendoWareVfx2_15_3_1,
        51,
        0x70,
        sizeof(ushort),
        0xB4,
        0x10C8,
        0x70,
        [0xF98, 0xFB0, 0xFC8, 0xFE0, 0xFF8, 0x1010]);
}

public sealed record VfxbHeader(
    string Signature,
    byte GraphicsApi,
    ushort BinaryVersion,
    ushort ByteOrderMark,
    byte AlignmentShift,
    byte TargetAddressSize,
    ushort FirstBlockOffset,
    uint FileSize,
    string FileName = "",
    bool BigEndian = false);

/// <summary>A raw VFXB resource node. Unknown data is intentionally not interpreted.</summary>
public sealed class VfxbNode
{
    internal VfxbNode(
        string kind,
        int offset,
        uint size,
        int childRelativeOffset,
        int siblingRelativeOffset,
        int attributeRelativeOffset,
        int dataRelativeOffset,
        ushort declaredChildCount)
    {
        Kind = kind;
        Offset = offset;
        Size = size;
        ChildRelativeOffset = childRelativeOffset;
        SiblingRelativeOffset = siblingRelativeOffset;
        AttributeRelativeOffset = attributeRelativeOffset;
        DataRelativeOffset = dataRelativeOffset;
        DeclaredChildCount = declaredChildCount;
    }

    public string Kind { get; }
    public int Offset { get; }
    public uint Size { get; }
    public int ChildRelativeOffset { get; }
    public int SiblingRelativeOffset { get; }
    public int AttributeRelativeOffset { get; }
    public int DataRelativeOffset { get; }
    public ushort DeclaredChildCount { get; }
    public int? DataOffset => DataRelativeOffset == -1 ? null : checked(Offset + DataRelativeOffset);
    public List<VfxbNode> Children { get; } = [];
    public List<VfxbNode> Attributes { get; } = [];
}

/// <param name="Depth">0 for a top-level emitter, 1 for a child emitter nested inside another EMTR node.</param>
public sealed record VfxbEmitter(
    string Name,
    IReadOnlyList<ulong?> TextureSamplerGuids,
    VfxbNode Node,
    int Depth = 0);

public sealed record VfxbEmitterSet(
    string Name,
    int DeclaredEmitterCount,
    IReadOnlyList<VfxbEmitter> Emitters,
    VfxbNode Node);

public sealed class VfxbFile
{
    internal VfxbFile(
        byte[] data,
        VfxbLayout layout,
        VfxbHeader header,
        IReadOnlyList<VfxbNode> roots,
        IReadOnlyList<VfxbEmitterSet> emitterSets)
    {
        Data = data;
        Layout = layout;
        Header = header;
        Roots = roots;
        EmitterSets = emitterSets;
    }

    /// <summary>The original payload. Keep this as the source of truth for unknown fields.</summary>
    public byte[] Data { get; }
    public VfxbLayout Layout { get; }
    public VfxbHeader Header { get; }
    public IReadOnlyList<VfxbNode> Roots { get; }
    public IReadOnlyList<VfxbEmitterSet> EmitterSets { get; }

    /// <summary>True for a Wii U EFTB file: the same node tree and layouts, stored big-endian.</summary>
    public bool BigEndian => Header.BigEndian;

    /// <summary>
    /// The bytes of a node's data block: up to its first child or attribute node, or the whole declared size for
    /// nodes whose size covers only their binary (textures, primitives, shaders and their name tables).
    /// </summary>
    public ReadOnlySpan<byte> NodeData(VfxbNode node)
    {
        if (node.DataRelativeOffset < 0 || node.DataRelativeOffset >= 0x10000000)
            return [];
        int start = node.Offset + node.DataRelativeOffset;
        long length;
        if (BinaryNodes.Contains(node.Kind))
            length = node.Size;
        else
        {
            long end = node.Size;
            foreach (int rel in new[] { node.ChildRelativeOffset, node.AttributeRelativeOffset })
                if (rel != -1 && rel >= node.DataRelativeOffset && rel < end)
                    end = rel;
            length = Math.Max(0, end - node.DataRelativeOffset);
        }
        if (start < 0 || start + length > Data.Length)
            throw new InvalidDataException($"{node.Kind} data at 0x{start:X}+0x{length:X} is outside the file.");
        return Data.AsSpan(start, (int)length);
    }

    private static readonly HashSet<string> BinaryNodes = ["TEXR", "GX2B", "PRIM", "SHDB", "GRTF", "GTNT", "G3PR", "G3NT", "GRSN", "GRSC"];

    /// <summary>The version-specific field layouts for this file.</summary>
    public PtclLayoutSet Layouts => PtclLayouts.For(Layout.RuntimeVersion);

    /// <summary>A typed, zero-copy view of an emitter's data block. Writes go straight into <see cref="Data"/>.</summary>
    public StructView EmitterView(VfxbEmitter emitter)
    {
        int offset = emitter.Node.DataOffset
            ?? throw new InvalidDataException($"EMTR node at 0x{emitter.Node.Offset:X} has no data block.");
        return new StructView(Layouts.Emitter, Data.AsSpan(offset, Layout.EmitterFixedDataSize), BigEndian);
    }

    /// <summary>A typed, zero-copy view of an emitter set's data block.</summary>
    public StructView EmitterSetView(VfxbEmitterSet set)
    {
        int offset = set.Node.DataOffset
            ?? throw new InvalidDataException($"ESET node at 0x{set.Node.Offset:X} has no data block.");
        return new StructView(Layouts.EmitterSet, Data.AsSpan(offset, Layout.EmitterSetFixedDataSize), BigEndian);
    }
}

public static class VfxbReader
{
    private const int HeaderSize = 0x20;
    private const int EftbHeaderSize = 0x30;
    private const int NodeHeaderSize = 0x20;

    /// <summary>True for a VFXB (Switch, TotK) or EFTB (Wii U BotW) payload.</summary>
    public static bool IsVfxb(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= HeaderSize && (bytes[..8].SequenceEqual("VFXB    "u8) || bytes[..4].SequenceEqual("EFTB"u8));

    public static VfxbFile Read(ReadOnlySpan<byte> bytes, VfxbLayout? expectedLayout = null)
    {
        if (bytes.Length < HeaderSize)
            throw new InvalidDataException("VFXB is shorter than its binary header.");

        VfxbHeader header;
        if (bytes[..4].SequenceEqual("EFTB"u8))
        {
            //Wii U: a 0x30-byte header (magic, version word, file name) and the VFXB node tree in big-endian.
            if (bytes.Length < EftbHeaderSize)
                throw new InvalidDataException("EFTB is shorter than its binary header.");
            header = new VfxbHeader(
                "EFTB",
                0,
                checked((ushort)BinaryPrimitives.ReadUInt32BigEndian(bytes[4..])),
                0xFEFF,
                0,
                0,
                EftbHeaderSize,
                (uint)bytes.Length,
                ReadFixedString(bytes, 8, 0x20),
                true);
        }
        else
        {
            string signature = Encoding.ASCII.GetString(bytes[..8]);
            if (signature != "VFXB    ")
                throw new InvalidDataException($"Invalid VFXB signature '{signature}'.");

            header = new VfxbHeader(
                signature,
                bytes[9],
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]),
                bytes[14],
                bytes[15],
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[22..]),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]),
                bytes.Length >= 0x40 ? ReadFixedString(bytes, 0x20, 0x20) : "");

            if (header.ByteOrderMark != 0xFEFF)
                throw new InvalidDataException($"Unsupported VFXB byte-order mark 0x{header.ByteOrderMark:X4}.");
            if (header.FileSize != bytes.Length)
                throw new InvalidDataException($"VFXB header size {header.FileSize} does not match {bytes.Length} bytes.");
        }

        VfxbLayout layout = expectedLayout ?? header.BinaryVersion switch
        {
            20 => VfxbLayouts.BotW,
            51 => VfxbLayouts.TotK,
            _ => throw new PtclVersionException($"Unsupported VFXB binary version {header.BinaryVersion}.")
        };

        if (header.BinaryVersion != layout.BinaryVersion)
            throw new PtclVersionException(
                $"Expected VFXB binary version {layout.BinaryVersion}, got {header.BinaryVersion}.");

        byte[] data = bytes.ToArray();
        bool big = header.BigEndian;
        List<VfxbNode> roots = ReadSiblingChain(data, big, header.FirstBlockOffset, new HashSet<int>(), true);
        VfxbNode? esta = roots.FirstOrDefault(x => x.Kind == "ESTA");
        List<VfxbEmitterSet> sets = esta is null ? [] : ReadEmitterSets(data, big, esta, layout);
        return new VfxbFile(data, layout, header, roots, sets);
    }

    private static List<VfxbEmitterSet> ReadEmitterSets(byte[] data, bool big, VfxbNode esta, VfxbLayout layout)
    {
        var result = new List<VfxbEmitterSet>();
        foreach (VfxbNode setNode in esta.Children.Where(x => x.Kind == "ESET"))
        {
            int dataOffset = RequireDataOffset(setNode);
            EnsureRange(data, dataOffset, layout.EmitterSetFixedDataSize, "ESET fixed data");
            string name = ReadFixedString(data, dataOffset + 0x10, 0x40);
            ReadOnlySpan<byte> countData = data.AsSpan(
                dataOffset + layout.EmitterSetEmitterCountOffset,
                layout.EmitterSetEmitterCountSize);
            int declaredCount = layout.EmitterSetEmitterCountSize switch
            {
                sizeof(ushort) => big ? BinaryPrimitives.ReadUInt16BigEndian(countData) : BinaryPrimitives.ReadUInt16LittleEndian(countData),
                sizeof(int) => big ? BinaryPrimitives.ReadInt32BigEndian(countData) : BinaryPrimitives.ReadInt32LittleEndian(countData),
                _ => throw new InvalidOperationException(
                    $"Unsupported emitter-count size {layout.EmitterSetEmitterCountSize}.")
            };
            // The declared count includes child emitters nested inside EMTR nodes (child_rel > 0 on the
            // EMTR), so collect every EMTR descendant depth-first rather than only direct children.
            var collected = new List<VfxbEmitter>();
            void Collect(IEnumerable<VfxbNode> nodes, int depth)
            {
                foreach (VfxbNode node in nodes.Where(x => x.Kind == "EMTR"))
                {
                    collected.Add(ReadEmitter(data, big, node, layout, depth));
                    Collect(node.Children, depth + 1);
                }
            }
            Collect(setNode.Children, 0);
            VfxbEmitter[] emitters = collected.ToArray();

            if (declaredCount != emitters.Length)
                throw new InvalidDataException(
                    $"ESET '{name}' declares {declaredCount} emitters but has {emitters.Length} EMTR nodes (including nested child emitters).");

            result.Add(new VfxbEmitterSet(name, declaredCount, emitters, setNode));
        }
        return result;
    }

    private static VfxbEmitter ReadEmitter(byte[] data, bool big, VfxbNode node, VfxbLayout layout, int depth)
    {
        int dataOffset = RequireDataOffset(node);
        EnsureRange(data, dataOffset, layout.EmitterFixedDataSize, "EMTR fixed data");
        ulong?[] textureGuids = layout.TextureSamplerGuidOffsets
            .Select(offset =>
            {
                ReadOnlySpan<byte> slot = data.AsSpan(dataOffset + offset, 8);
                ulong guid = big ? BinaryPrimitives.ReadUInt64BigEndian(slot) : BinaryPrimitives.ReadUInt64LittleEndian(slot);
                return guid == ulong.MaxValue ? (ulong?)null : guid;
            })
            .ToArray();
        return new VfxbEmitter(
            ReadFixedString(data, dataOffset + 0x10, 0x40),
            textureGuids,
            node,
            depth);
    }

    private static List<VfxbNode> ReadSiblingChain(
        byte[] data,
        bool big,
        int startOffset,
        HashSet<int> ancestry,
        bool recurseIntoChildren,
        int maxNodes = int.MaxValue)
    {
        var nodes = new List<VfxbNode>();
        var siblings = new HashSet<int>();
        int offset = startOffset;
        while (offset != -1 && nodes.Count < maxNodes)
        {
            if (!siblings.Add(offset))
                throw new InvalidDataException($"VFXB sibling cycle at 0x{offset:X}.");
            VfxbNode node = ReadNode(data, big, offset);
            nodes.Add(node);

            // A positive header child count bounds the child chain: the chain is not always terminated
            // (for G3PR the G3NT sibling offset can point into the G3D data). GRSN has child_rel > 0 with a
            // zero count; its chain (GRSR, GRRE, GRCE) is terminated normally.
            if (recurseIntoChildren && node.ChildRelativeOffset > 0
                && (node.DeclaredChildCount > 0 || node.Kind == "GRSN"))
            {
                if (!ancestry.Add(offset))
                    throw new InvalidDataException($"VFXB child cycle at 0x{offset:X}.");
                node.Children.AddRange(ReadSiblingChain(
                    data, big, checked(offset + node.ChildRelativeOffset), ancestry, true,
                    node.DeclaredChildCount > 0 ? node.DeclaredChildCount : int.MaxValue));
                ancestry.Remove(offset);
            }
            if (node.AttributeRelativeOffset > 0)
                node.Attributes.AddRange(ReadSiblingChain(
                    data, big, checked(offset + node.AttributeRelativeOffset), new HashSet<int>(), false));

            offset = node.SiblingRelativeOffset <= 0
                ? -1
                : checked(offset + node.SiblingRelativeOffset);
        }
        return nodes;
    }

    private static VfxbNode ReadNode(byte[] data, bool big, int offset)
    {
        EnsureRange(data, offset, NodeHeaderSize, "node header");
        ReadOnlySpan<byte> span = data.AsSpan(offset, NodeHeaderSize);
        int I32(ReadOnlySpan<byte> s) => big ? BinaryPrimitives.ReadInt32BigEndian(s) : BinaryPrimitives.ReadInt32LittleEndian(s);
        return new VfxbNode(
            Encoding.ASCII.GetString(span[..4]),
            offset,
            (uint)I32(span[4..]),
            I32(span[8..]),
            I32(span[12..]),
            I32(span[16..]),
            I32(span[20..]),
            big ? BinaryPrimitives.ReadUInt16BigEndian(span[28..]) : BinaryPrimitives.ReadUInt16LittleEndian(span[28..]));
    }

    private static int RequireDataOffset(VfxbNode node) => node.DataOffset
        ?? throw new InvalidDataException($"{node.Kind} node at 0x{node.Offset:X} has no data block.");

    private static string ReadFixedString(ReadOnlySpan<byte> data, int offset, int maximumLength)
    {
        if (offset < 0 || maximumLength < 0 || offset > data.Length - maximumLength)
            throw new InvalidDataException($"VFXB fixed string range 0x{offset:X}+0x{maximumLength:X} is outside the file.");
        ReadOnlySpan<byte> span = data.Slice(offset, maximumLength);
        int terminator = span.IndexOf((byte)0);
        return Encoding.UTF8.GetString(terminator < 0 ? span : span[..terminator]);
    }

    private static void EnsureRange(byte[] data, int offset, int length, string description)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(
                $"VFXB {description} range 0x{offset:X}+0x{length:X} is outside the file.");
    }
}
