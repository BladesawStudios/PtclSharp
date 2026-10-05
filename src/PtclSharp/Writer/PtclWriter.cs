using BymlSharp;
using Yaz0Sharp;
using ZstdSharp;

namespace PtclSharp.Writer;

/// <summary>Packs VFXB bytes back into the two games' outer containers.</summary>
public static class PtclWriter
{
    /// <summary>BotW <c>.sesetlist</c>: the VFXB payload, Yaz0-compressed.</summary>
    public static byte[] WriteSesetlist(ReadOnlySpan<byte> vfxb, uint yaz0Alignment = 0x1000, int level = Yaz0.DefaultLevel)
        => Yaz0.Compress(vfxb, yaz0Alignment, level);

    /// <summary>
    /// TotK <c>.esetb.byml.zs</c>. The BYML of <paramref name="template"/> is kept (so unknown keys survive) with the
    /// <c>PtclBin</c> payload replaced; <paramref name="emitterSets"/> and <paramref name="textures"/> replace the declared
    /// <c>Esets</c> and <c>Textures</c> lists when given.
    /// </summary>
    public static byte[] WriteEsetb(
        PtclFile template,
        ReadOnlySpan<byte> vfxb,
        ReadOnlySpan<byte> zstdDictionary,
        IReadOnlyList<string>? emitterSets = null,
        IReadOnlyList<PtclTextureReference>? textures = null,
        int zstdLevel = 3)
    {
        byte[] container = BuildEsetbByml(template, vfxb, emitterSets, textures);
        using var compressor = new Compressor(zstdLevel);
        if (!zstdDictionary.IsEmpty)
            compressor.LoadDictionary(zstdDictionary);
        return compressor.Wrap(container).ToArray();
    }

    /// <summary>The decompressed BYML bytes of a rebuilt TotK container.</summary>
    public static byte[] BuildEsetbByml(
        PtclFile template,
        ReadOnlySpan<byte> vfxb,
        IReadOnlyList<string>? emitterSets = null,
        IReadOnlyList<PtclTextureReference>? textures = null)
    {
        if (template.Byml is null)
            throw new InvalidOperationException("The template is not a TotK container (it has no BYML).");
        // Re-parse so the template stays untouched.
        BymlFile source = BymlFile.FromBinary(template.DecodedContainer);
        Byml root = source.Root;
        Byml oldBin = root["PtclBin"] ?? throw new InvalidDataException("The template has no PtclBin entry.");
        root["PtclBin"] = oldBin.BinaryAlignment > 0 ? Byml.From(vfxb.ToArray(), oldBin.BinaryAlignment) : Byml.From(vfxb.ToArray());

        if (emitterSets is not null)
            root["Esets"] = Byml.Array(emitterSets.Select(n => (Byml)n));
        if (textures is not null)
            root["Textures"] = Byml.Array(textures.Select(t => Byml.Map(new Dictionary<string, Byml>
            {
                ["name"] = t.Name,
                ["guid"] = t.Guid
            })));
        return new BymlFile(root, source.Version, source.BigEndian) { ShareIdenticalContainers = source.ShareIdenticalContainers }.Write();
    }
}
