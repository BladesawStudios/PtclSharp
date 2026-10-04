using PtclSharp;
using ZstdSharp;

namespace PtclSharp.Tests;

/// <summary>
/// Access to the shipped game files. Corpus tests run only when the <c>PTCL_TOTK_ROMFS</c> environment variable points at
/// a TotK romfs root (the folder that contains <c>Effect/</c> and <c>Pack/ZsDic.pack.zs</c>); otherwise they are skipped.
/// </summary>
internal static class Corpus
{
    internal const string TotkEnv = "PTCL_TOTK_ROMFS";

    internal static string? TotkRoot
    {
        get
        {
            string? root = Environment.GetEnvironmentVariable(TotkEnv);
            return root is not null && Directory.Exists(Path.Combine(root, "Effect")) ? root : null;
        }
    }

    private static readonly Lazy<byte[]> Dictionary = new(() => LoadDictionary(TotkRoot!));

    internal static IEnumerable<string> TotkFiles() =>
        Directory.GetFiles(Path.Combine(TotkRoot!, "Effect"), "*.esetb.byml.zs").Order(StringComparer.Ordinal);

    internal static PtclFile LoadTotk(string path) => PtclFile.ReadEsetb(File.ReadAllBytes(path), Dictionary.Value);

    /// <summary>Finds the Zstandard dictionary with ID 1 in <c>Pack/ZsDic.pack.zs</c> (a SARC) - the one the .esetb files use.</summary>
    private static byte[] LoadDictionary(string root)
    {
        byte[] sarc;
        using (var decompressor = new Decompressor())
            sarc = decompressor.Unwrap(File.ReadAllBytes(Path.Combine(root, "Pack", "ZsDic.pack.zs"))).ToArray();

        int dataOffset = BitConverter.ToInt32(sarc, 0xC);
        const int sfat = 0x14;
        int count = BitConverter.ToUInt16(sarc, sfat + 6);
        for (int i = 0; i < count; i++)
        {
            int entry = sfat + 12 + (i * 16);
            int start = BitConverter.ToInt32(sarc, entry + 8);
            int end = BitConverter.ToInt32(sarc, entry + 12);
            byte[] candidate = sarc[(dataOffset + start)..(dataOffset + end)];
            if (BitConverter.ToUInt32(candidate, 4) == 1) return candidate;
        }
        throw new InvalidDataException("No Zstandard dictionary with ID 1 in ZsDic.pack.zs.");
    }
}

/// <summary>A <see cref="FactAttribute"/> that is skipped when the TotK corpus is not available.</summary>
public sealed class TotkCorpusFactAttribute : FactAttribute
{
    public TotkCorpusFactAttribute()
    {
        if (Corpus.TotkRoot is null)
            Skip = $"Set {Corpus.TotkEnv} to a TotK romfs root to run the corpus tests.";
    }
}
