using System.Collections.Frozen;

namespace PtclSharp.Layout;

/// <summary>The layout of an EMTR data block plus the game-level capabilities that belong to it.</summary>
public sealed class EmitterLayout : StructLayout
{
    public EmitterLayout(string name, int size, IEnumerable<FieldDef> fields, int textureSlotCount, int? keyframeTrackCount)
        : base(name, size, fields)
    {
        TextureSlotCount = textureSlotCount;
        KeyframeTrackCount = keyframeTrackCount;
    }

    /// <summary>Number of texture sampler slots (BotW 3, TotK 6).</summary>
    public int TextureSlotCount { get; }

    /// <summary>Number of 8-key tracks, or null while not settled (BotW documents 5 in some places and 6 in others).</summary>
    public int? KeyframeTrackCount { get; }
}

/// <summary>
/// Everything layout-related for one game/runtime version: the container structures, the EMTR block and the
/// attribute/node payload layouts that have been mapped. A field or chunk that is absent here is not
/// representable (or not yet mapped) in that game, which is what conversion reports build on.
/// </summary>
public sealed class PtclLayoutSet
{
    public PtclLayoutSet(
        PtclVersion version,
        VfxbLayout vfxb,
        StructLayout fileHeader,
        StructLayout nodeHeader,
        StructLayout emitterSet,
        EmitterLayout emitter,
        IEnumerable<ChunkLayout> chunks)
    {
        Version = version;
        Vfxb = vfxb;
        FileHeader = fileHeader;
        NodeHeader = nodeHeader;
        EmitterSet = emitterSet;
        Emitter = emitter;
        Chunks = chunks.ToFrozenDictionary(c => c.Name, StringComparer.Ordinal);
    }

    public PtclVersion Version { get; }

    /// <summary>The structural constants the VFXB reader needs (counts, sizes, texture GUID slots).</summary>
    public VfxbLayout Vfxb { get; }

    /// <summary><c>nn::util::BinaryFileHeader</c> plus the file-name area (0x40 bytes).</summary>
    public StructLayout FileHeader { get; }

    /// <summary>The 0x20-byte header that precedes every node.</summary>
    public StructLayout NodeHeader { get; }

    /// <summary>ESET node data.</summary>
    public StructLayout EmitterSet { get; }

    /// <summary>EMTR node data.</summary>
    public EmitterLayout Emitter { get; }

    /// <summary>Payload layouts by node FourCC (attribute chunks and other data-carrying nodes that have been mapped).</summary>
    public IReadOnlyDictionary<string, ChunkLayout> Chunks { get; }
}
