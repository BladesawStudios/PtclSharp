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
    internal const string BotwEnv = "PTCL_BOTW_ROM";

    internal static string? TotkRoot
    {
        get
        {
            string? root = Environment.GetEnvironmentVariable(TotkEnv);
            return root is not null && Directory.Exists(Path.Combine(root, "Effect")) ? root : null;
        }
    }

    /// <summary>BotW ROM root (the folder that contains <c>Effect/*.sesetlist</c>), or null.</summary>
    internal static string? BotwRoot
    {
        get
        {
            string? root = Environment.GetEnvironmentVariable(BotwEnv);
            return root is not null && Directory.Exists(Path.Combine(root, "Effect")) ? root : null;
        }
    }

    internal static IEnumerable<string> BotwFiles() =>
        Directory.GetFiles(Path.Combine(BotwRoot!, "Effect"), "*.sesetlist").Order(StringComparer.Ordinal);

    internal static PtclFile LoadBotw(string path) => PtclFile.ReadSesetlist(File.ReadAllBytes(path));

    private static readonly Lazy<byte[]> Dictionary = new(() => LoadDictionary(TotkRoot!));

    internal static IEnumerable<string> TotkFiles() =>
        Directory.GetFiles(Path.Combine(TotkRoot!, "Effect"), "*.esetb.byml.zs").Order(StringComparer.Ordinal);

    internal static byte[] TotkDictionary => Dictionary.Value;

    internal static PtclFile LoadTotk(string path) => PtclFile.ReadEsetb(File.ReadAllBytes(path), Dictionary.Value);

    private static byte[] LoadDictionary(string root) => PtclDictionary.FromZsDicPackFile(Path.Combine(root, "Pack", "ZsDic.pack.zs"));
}

/// <summary>A <see cref="FactAttribute"/> that is skipped when the BotW corpus is not available.</summary>
public sealed class BotwCorpusFactAttribute : FactAttribute
{
    public BotwCorpusFactAttribute()
    {
        if (Corpus.BotwRoot is null)
            Skip = $"Set {Corpus.BotwEnv} to a BotW ROM root (the folder with Effect/*.sesetlist) to run the corpus tests.";
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
