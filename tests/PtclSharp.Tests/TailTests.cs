using PtclSharp.Writer;

namespace PtclSharp.Tests;

public class TailTests
{
    private static (byte[] Fres, List<byte[]> Entries)? ReadG3d(PtclFile file)
    {
        VfxbNode? g3pr = file.Vfxb.Roots.FirstOrDefault(r => r.Kind == "G3PR");
        if (g3pr?.DataOffset is not int data) return null;
        VfxbNode g3nt = g3pr.Children.First(c => c.Kind == "G3NT");
        var entries = new List<byte[]>();
        for (int i = 0; i < (int)g3nt.Size / 0x18; i++)
            entries.Add(file.Vfxb.Data.AsSpan(g3nt.DataOffset!.Value + (i * 0x18), 0x18).ToArray());
        return (file.Vfxb.Data.AsSpan(data, (int)g3pr.Size).ToArray(), entries);
    }

    [TotkCorpusFact]
    public void SplittingAndJoiningTheTailChangesNothing()
    {
        foreach (string path in Corpus.TotkFiles().Where((_, i) => i % 3 == 0))
        {
            VfxbDocument doc = VfxbDocument.From(Corpus.LoadTotk(path).Vfxb);
            Assert.Equal(doc.Tail, doc.ParseTail().ToBytes());
        }
    }

    [BotwCorpusFact]
    public void TheBotwTailSurvivesSplittingToo()
    {
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 4 == 0))
        {
            VfxbDocument doc = VfxbDocument.From(Corpus.LoadBotw(path).Vfxb);
            Assert.Equal(doc.Tail, doc.ParseTail().ToBytes());
        }
    }

    [TotkCorpusFact]
    public void ARebuiltG3dRootIsIdenticalToTheShippedOne()
    {
        int rebuilt = 0, empty = 0;
        foreach (string path in Corpus.TotkFiles())
        {
            PtclFile file = Corpus.LoadTotk(path);
            VfxbDocument doc = VfxbDocument.From(file.Vfxb);
            (byte[] fres, List<byte[]> entries)? g3d = ReadG3d(file);
            VfxbTail tail = doc.ParseTail();
            VfxbTail.Root original = tail.Find("G3PR")!;
            VfxbTail.Root replacement = g3d is null ? VfxbTail.BuildG3d(null, []) : VfxbTail.BuildG3d(g3d.Value.fres, g3d.Value.entries);
            if (g3d is null) empty++; else rebuilt++;
            Assert.True(doc.Tail.AsSpan().SequenceEqual(tail.Replace(replacement).ToBytes()), $"{Path.GetFileName(path)}: G3PR rebuild differs");
        }
        Assert.True(rebuilt > 1000);
        Assert.True(empty > 50);
    }

    [TotkCorpusFact]
    public void ARebuiltPrimitiveRootIsIdenticalToTheShippedOne()
    {
        int withPrimitives = 0;
        foreach (string path in Corpus.TotkFiles())
        {
            PtclFile file = Corpus.LoadTotk(path);
            VfxbDocument doc = VfxbDocument.From(file.Vfxb);
            VfxbNode prma = file.Vfxb.Roots.First(r => r.Kind == "PRMA");
            var prims = prma.Children.Select(c => file.Vfxb.Data.AsSpan(c.Offset, (int)c.Size + 0x20).ToArray()).ToList();
            if (prims.Count > 0) withPrimitives++;
            VfxbTail tail = doc.ParseTail();
            Assert.True(doc.Tail.AsSpan().SequenceEqual(tail.Replace(VfxbTail.BuildPrimitives(prims)).ToBytes()), $"{Path.GetFileName(path)}: PRMA rebuild differs");
        }
        Assert.True(withPrimitives > 100);
    }
}
