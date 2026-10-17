using System.Text;

namespace PtclSharp.Tests;

/// <summary>
/// <see cref="VfxbReader.ReadTree"/>: the layout-free read. For BotW and TotK it must agree with
/// <see cref="VfxbReader.Read"/>; for Z-A (binary version 22, no layout yet) it is the only way in.
/// </summary>
public class TreeTests
{
    [BotwCorpusFact]
    public void ReadTreeMatchesReadForEveryBotwFile() =>
        AssertTreesMatch(Corpus.BotwFiles().Select(f => Corpus.LoadBotw(f).Vfxb));

    [TotkCorpusFact]
    public void ReadTreeMatchesReadForEveryTotkFile() =>
        AssertTreesMatch(Corpus.TotkFiles().Select(f => Corpus.LoadTotk(f).Vfxb));

    [ZaCorpusFact]
    public void EveryZaEffectReadsAsATree()
    {
        var failures = new List<string>();
        int count = 0, emitters = 0;
        foreach (string file in Corpus.ZaFiles())
        {
            count++;
            try
            {
                VfxbTree tree = VfxbReader.ReadTree(File.ReadAllBytes(file));
                Assert.Equal(VfxbReader.ZaBinaryVersion, tree.Header.BinaryVersion);
                VfxbNode esta = Assert.Single(tree.Roots, n => n.Kind == "ESTA");
                foreach (VfxbNode set in esta.Children.Where(n => n.Kind == "ESET"))
                {
                    Assert.False(string.IsNullOrEmpty(tree.NameOf(set)));
                    emitters += set.Children.Count(n => n.Kind == "EMTR");
                }
            }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
        Assert.True(count > 1000, $"Expected the Z-A effects, found {count} files.");
        Assert.True(emitters > count, $"Expected emitters under the emitter sets, found {emitters}.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [ZaCorpusFact]
    public void ReadOnAZaEffectPointsToReadTree()
    {
        byte[] bytes = File.ReadAllBytes(Corpus.ZaFiles().First());
        var ex = Assert.Throws<PtclVersionException>(() => VfxbReader.Read(bytes));
        Assert.Contains(nameof(VfxbReader.ReadTree), ex.Message);
    }

    private static void AssertTreesMatch(IEnumerable<VfxbFile> files)
    {
        var failures = new List<string>();
        int count = 0;
        foreach (VfxbFile file in files)
        {
            count++;
            VfxbTree tree = VfxbReader.ReadTree(file.Data);
            if (tree.Header != file.Header || Describe(tree.Roots) != Describe(file.Roots))
                failures.Add($"{file.Header.FileName}: tree differs");
            foreach (VfxbEmitterSet set in file.EmitterSets)
            {
                if (tree.NameOf(set.Node) != set.Name) failures.Add($"{set.Name}: emitter set name differs");
                foreach (VfxbEmitter emitter in set.Emitters)
                    if (tree.NameOf(emitter.Node) != emitter.Name) failures.Add($"{set.Name}/{emitter.Name}: emitter name differs");
            }
        }
        Assert.True(count > 100, $"Expected the full corpus, found {count} files.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    private static string Describe(IReadOnlyList<VfxbNode> nodes)
    {
        var sb = new StringBuilder();
        void Walk(VfxbNode n)
        {
            sb.Append('(').Append(n.Kind).Append(' ').Append(n.Offset).Append(' ').Append(n.Size).Append(' ')
              .Append(n.ChildRelativeOffset).Append(' ').Append(n.SiblingRelativeOffset).Append(' ')
              .Append(n.AttributeRelativeOffset).Append(' ').Append(n.DataRelativeOffset).Append(' ').Append(n.DeclaredChildCount);
            foreach (VfxbNode c in n.Children) Walk(c);
            sb.Append('|');
            foreach (VfxbNode a in n.Attributes) Walk(a);
            sb.Append(')');
        }
        foreach (VfxbNode n in nodes) Walk(n);
        return sb.ToString();
    }
}
