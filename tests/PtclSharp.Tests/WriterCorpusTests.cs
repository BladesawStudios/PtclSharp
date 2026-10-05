using PtclSharp.Writer;

namespace PtclSharp.Tests;

/// <summary>The tree writer must reproduce every shipped VFXB payload byte for byte.</summary>
public class WriterCorpusTests
{
    private static string? FirstDifference(byte[] expected, byte[] actual)
    {
        if (expected.SequenceEqual(actual)) return null;
        int n = Math.Min(expected.Length, actual.Length);
        for (int i = 0; i < n; i++)
            if (expected[i] != actual[i]) return $"first difference at 0x{i:X} (expected {expected[i]:X2}, got {actual[i]:X2}); lengths {expected.Length:X} vs {actual.Length:X}";
        return $"lengths differ: {expected.Length:X} vs {actual.Length:X}";
    }

    [BotwCorpusFact]
    public void BotwFilesRoundTripByteForByte()
    {
        var failures = new List<string>();
        foreach (string file in Corpus.BotwFiles())
        {
            VfxbFile vfxb = Corpus.LoadBotw(file).Vfxb;
            string? diff = FirstDifference(vfxb.Data, VfxbDocument.From(vfxb).ToBytes());
            if (diff is not null) failures.Add($"{Path.GetFileName(file)}: {diff}");
        }
        Assert.True(failures.Count == 0, $"{failures.Count} failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(8))}");
    }

    [TotkCorpusFact]
    public void TotkFilesRoundTripByteForByte()
    {
        var failures = new List<string>();
        foreach (string file in Corpus.TotkFiles())
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            string? diff = FirstDifference(vfxb.Data, VfxbDocument.From(vfxb).ToBytes());
            if (diff is not null) failures.Add($"{Path.GetFileName(file)}: {diff}");
        }
        Assert.True(failures.Count == 0, $"{failures.Count} failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(8))}");
    }

    [BotwCorpusFact]
    public void RecountReproducesTheShippedCounts_Botw()
    {
        foreach (string file in Corpus.BotwFiles().Where((_, i) => i % 3 == 0))
        {
            VfxbFile vfxb = Corpus.LoadBotw(file).Vfxb;
            var doc = VfxbDocument.From(vfxb);
            doc.RecountChildren();
            Assert.Equal(vfxb.Data, doc.ToBytes());
        }
    }

    [TotkCorpusFact]
    public void RecountReproducesTheShippedCounts_Totk()
    {
        foreach (string file in Corpus.TotkFiles().Where((_, i) => i % 5 == 0))
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            var doc = VfxbDocument.From(vfxb);
            doc.RecountChildren();
            Assert.Equal(vfxb.Data, doc.ToBytes());
        }
    }

    [BotwCorpusFact]
    public void BotwContainerRecompressesToTheSameVfxb()
    {
        foreach (string file in Corpus.BotwFiles().Where((_, i) => i % 40 == 0))
        {
            PtclFile ptcl = Corpus.LoadBotw(file);
            byte[] packed = PtclWriter.WriteSesetlist(ptcl.Vfxb.Data, Yaz0Sharp.Yaz0.GetAlignment(File.ReadAllBytes(file)));
            Assert.Equal(ptcl.Vfxb.Data, PtclFile.ReadSesetlist(packed).Vfxb.Data);
        }
    }

    [TotkCorpusFact]
    public void TotkContainerRebuildsToTheSameBymlAndVfxb()
    {
        foreach (string file in Corpus.TotkFiles().Where((_, i) => i % 60 == 0))
        {
            PtclFile ptcl = Corpus.LoadTotk(file);
            byte[] byml = PtclWriter.BuildEsetbByml(ptcl, ptcl.Vfxb.Data);
            Assert.Equal(ptcl.DecodedContainer, byml);

            byte[] packed = PtclWriter.WriteEsetb(ptcl, ptcl.Vfxb.Data, Corpus.TotkDictionary);
            PtclFile again = PtclFile.ReadEsetb(packed, Corpus.TotkDictionary);
            Assert.Equal(ptcl.Vfxb.Data, again.Vfxb.Data);
            Assert.Equal(ptcl.DeclaredEmitterSets, again.DeclaredEmitterSets);
            Assert.Equal(ptcl.Textures, again.Textures);
        }
    }
}
