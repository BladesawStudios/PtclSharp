using BymlSharp;
using Yaz0Sharp;
using ZstdSharp;

namespace PtclSharp;

public enum PtclContainerFormat
{
    Sesetlist,
    EsetbByml
}

/// <summary>
/// A decoded game container and its shared VFXB payload. The outer container is
/// deliberately separate from VFXB because the two games package it differently.
/// </summary>
public sealed class PtclFile
{
    private PtclFile(
        PtclVersion version,
        PtclContainerFormat containerFormat,
        VfxbFile vfxb,
        byte[] decodedContainer,
        BymlFile? byml,
        IReadOnlyList<string> declaredEmitterSets,
        IReadOnlyList<PtclTextureReference> textures)
    {
        Version = version;
        ContainerFormat = containerFormat;
        Vfxb = vfxb;
        DecodedContainer = decodedContainer;
        Byml = byml;
        DeclaredEmitterSets = declaredEmitterSets;
        Textures = textures;
    }

    public PtclVersion Version { get; }
    public PtclContainerFormat ContainerFormat { get; }
    public VfxbFile Vfxb { get; }

    /// <summary>The decompressed outer bytes, retained for lossless future editing.</summary>
    public byte[] DecodedContainer { get; }

    /// <summary>The TotK outer BYML tree, or null for BotW.</summary>
    public BymlFile? Byml { get; }

    public IReadOnlyList<string> DeclaredEmitterSets { get; }
    public IReadOnlyList<PtclTextureReference> Textures { get; }

    public static PtclFile ReadSesetlist(ReadOnlySpan<byte> bytes)
    {
        byte[] decoded = Yaz0.IsCompressed(bytes) ? Yaz0.Decompress(bytes) : bytes.ToArray();
        VfxbFile vfxb = VfxbReader.Read(decoded, VfxbLayouts.BotW);
        return new PtclFile(
            PtclVersion.BotW_NintendoWareVfx_4_4_0,
            PtclContainerFormat.Sesetlist,
            vfxb,
            decoded,
            null,
            vfxb.EmitterSets.Select(x => x.Name).ToArray(),
            []);
    }

    public static PtclFile ReadEsetb(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> zstdDictionary)
    {
        using var decompressor = new Decompressor();
        if (!zstdDictionary.IsEmpty)
            decompressor.LoadDictionary(zstdDictionary);

        byte[] decoded = decompressor.Unwrap(bytes).ToArray();
        BymlFile byml = BymlFile.FromBinary(decoded);
        Byml root = byml.Root;
        if (!root.IsMap)
            throw new InvalidDataException("TotK ESETB root is not a BYML map.");

        Byml ptclBin = root["PtclBin"]
            ?? throw new InvalidDataException("TotK ESETB has no PtclBin entry.");
        VfxbFile vfxb = VfxbReader.Read(ptclBin.Binary, VfxbLayouts.TotK);

        string[] declaredSets = root["Esets"] is { IsArray: true } sets
            ? sets.AsArray.Select(x => x.String).ToArray()
            : [];

        PtclTextureReference[] textures = root["Textures"] is { IsArray: true } textureArray
            ? textureArray.AsArray.Select(ReadTexture).ToArray()
            : [];

        return new PtclFile(
            PtclVersion.TotK_NintendoWareVfx2_15_3_1,
            PtclContainerFormat.EsetbByml,
            vfxb,
            decoded,
            byml,
            declaredSets,
            textures);
    }

    private static PtclTextureReference ReadTexture(Byml node)
    {
        if (!node.IsMap)
            throw new InvalidDataException("A TotK Textures entry is not a BYML map.");

        return new PtclTextureReference(
            node["name"]?.AsString() ?? string.Empty,
            node["guid"]?.UInt
                ?? throw new InvalidDataException("A TotK texture has no unsigned 32-bit guid."));
    }
}

public sealed record PtclTextureReference(string Name, uint Guid);
