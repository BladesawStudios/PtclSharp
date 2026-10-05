using BfresLibrary;
using PtclSharp.Models;
using PtclSharp.Writer;

namespace PtclSharp.Tests;

public class ModelTests
{
    private static readonly Lazy<TotkModelIndex> Index = new(() =>
        TotkModelIndex.Build(Path.Combine(Corpus.TotkRoot!, "Effect"), Corpus.TotkDictionary));

    private static PtclFile LoadResident() => PtclFile.ReadSesetlist(
        Textures.ResidentFile.ReadFromPack(Path.Combine(Corpus.BotwRoot!, "Pack", "Bootup.pack")) ?? throw new InvalidOperationException("No resident file."));

    private static string Describe(Model m) =>
        string.Join(",", m.VertexBuffers[0].Attributes.Values.Select(a => $"{a.Name}:{a.Format}@{a.Offset}")) + $"/stride {m.VertexBuffers[0].Buffers[0].Stride}";

    private static bool PositionsClose(Model a, Model b)
    {
        System.Numerics.Vector3[]? pa = BotwModelConverter.ReadPositions(a.VertexBuffers[0]), pb = BotwModelConverter.ReadPositions(b.VertexBuffers[0]);
        if (pa is null || pb is null || pa.Length != pb.Length) return false;
        float extent = pa.Max(p => p.Length()) + 1e-6f;
        return pa.Zip(pb).All(t => (t.First - t.Second).Length() <= extent * 0.002f);
    }

    [BothCorporaFact]
    public void ConvertedBotwModelsMatchTotksCopyOfTheSameAsset()
    {
        TotkModelIndex totk = Index.Value;
        Assert.NotNull(totk.TemplateFile);
        G3dContent template = totk.OpenContent(totk.TemplateFile!);

        var sources = new List<G3dContent>();
        sources.Add(G3dContent.Read(LoadResident())!);
        sources.AddRange(Corpus.BotwFiles().Where((_, i) => i % 7 == 0).Select(p => G3dContent.Read(Corpus.LoadBotw(p))).OfType<G3dContent>());

        int compared = 0, geometryIdentical = 0, radiusClose = 0;
        var checkedIds = new HashSet<uint>();
        var different = new List<string>();
        foreach (G3dContent content in sources)
        {
            ResFile res = content.Open();
            foreach (G3dEntry entry in content.Entries)
            {
                if (!totk.TryFind(entry.Id, out TotkModelLocation? location) || !checkedIds.Add(entry.Id)) continue;
                Model botw = res.Models[entry.Index];
                var notes = new List<string>();
                BotwModelConverter.Convert(botw, template.Open().Models.Values.First(TotkModelIndex.IsDummyMaterial), notes);

                G3dContent totkContent = totk.OpenContent(location.File);
                Model reference = totkContent.Open().Models[location.Index];
                compared++;
                // Nintendo re-quantised positions for TotK (32-bit float x3 became 16-bit float x4) in some assets, so compare decoded positions.
                bool sameLayout = Describe(botw) == Describe(reference);
                bool sameVertices = sameLayout
                    ? botw.VertexBuffers[0].Buffers[0].Data[0].AsSpan().SequenceEqual(reference.VertexBuffers[0].Buffers[0].Data[0])
                    : PositionsClose(botw, reference);
                bool sameIndices = botw.Shapes[0].Meshes[0].IndexBuffer.Data[0].AsSpan().SequenceEqual(reference.Shapes[0].Meshes[0].IndexBuffer.Data[0]);
                if (sameVertices && sameIndices && botw.VertexBuffers[0].VertexCount == reference.VertexBuffers[0].VertexCount) geometryIdentical++;
                else if (different.Count < 6)
                    different.Add($"{botw.Name}: verts {botw.VertexBuffers[0].VertexCount}/{reference.VertexBuffers[0].VertexCount} vertices {sameVertices} indices {sameIndices} [{Describe(botw)}] vs [{Describe(reference)}]");
                if (Math.Abs(botw.Shapes[0].RadiusArray[0] - reference.Shapes[0].RadiusArray[0]) < 0.001f) radiusClose++;
            }
        }
        Assert.True(compared > 80, $"only {compared} shared models compared");
        Assert.True(geometryIdentical > compared * 0.95, $"{geometryIdentical} of {compared} have the same geometry; e.g. {string.Join(" | ", different)}");
        Assert.True(radiusClose > compared * 0.8, $"bounding radius close for {radiusClose} of {compared}");
    }

    [BothCorporaFact]
    public void ABuiltResourceReloadsAndMatchesItsEntries()
    {
        TotkModelIndex totk = Index.Value;
        PtclFile botw = Corpus.LoadBotw(Corpus.BotwFiles().First(f => Path.GetFileName(f) == "GanonBeastBeam.sesetlist"));
        ModelPlan plan = ModelPlanner.Plan(botw, new BotwModelCatalog(botw, LoadResident()), totk, new ModelPlanOptions { PreferTotkModels = false });
        Assert.NotEmpty(plan.Items);
        Assert.All(plan.Items, i => Assert.Equal(ModelDisposition.Convert, i.Disposition));

        G3dBuild build = G3dBuilder.Build(plan, totk);
        Assert.NotNull(build.Resource);
        var again = new ResFile(new MemoryStream(build.Resource!));
        Assert.Equal(10u, again.VersionMajor);
        Assert.Equal(plan.Items.Count, again.Models.Count);
        Assert.Equal(build.ModelNames, again.Models.Keys.ToList());
        Assert.Equal(plan.Items.Select(i => i.Id), build.Entries.Select(e => BitConverter.ToUInt32(e, 0)));
        Assert.All(again.Models.Values, m => Assert.True(TotkModelIndex.IsDummyMaterial(m)));
    }

    [BothCorporaFact]
    public void ModelsLandInTheConvertedFileAndItStillReads()
    {
        TotkModelIndex totk = Index.Value;
        PtclFile botw = Corpus.LoadBotw(Corpus.BotwFiles().First(f => Path.GetFileName(f) == "GanonBeastBeam.sesetlist"));
        ModelPlan plan = ModelPlanner.Plan(botw, new BotwModelCatalog(botw, LoadResident()), totk);
        G3dBuild build = G3dBuilder.Build(plan, totk);

        PtclFile donor = Corpus.LoadTotk(Corpus.TotkFiles().First(f => Path.GetFileName(f).StartsWith("Kohga_Golem_Beam", StringComparison.Ordinal)));
        VfxbDocument document = VfxbDocument.From(donor.Vfxb);
        build.ApplyTo(document);
        byte[] bytes = document.ToBytes();

        VfxbFile read = VfxbReader.Read(bytes, VfxbLayouts.TotK);
        VfxbNode g3pr = read.Roots.First(r => r.Kind == "G3PR");
        Assert.Equal(0, g3pr.DataOffset!.Value % 0x1000);
        Assert.Equal(plan.Items.Count, (int)g3pr.Children.First(c => c.Kind == "G3NT").Size / 0x18);
        VfxbNode grsn = read.Roots.First(r => r.Kind == "GRSN");
        Assert.Equal(0, grsn.Children.First(c => c.Kind == "GRSR").DataOffset!.Value % 0x1000);
        Assert.Equal(donor.Vfxb.Roots.First(r => r.Kind == "GRSN").Children.First(c => c.Kind == "GRSR").Size, grsn.Children.First(c => c.Kind == "GRSR").Size);
    }

    [BothCorporaFact]
    public void EveryEmitterOfAConvertedFileFindsItsModelAndPrimitive()
    {
        TotkModelIndex totk = Index.Value;
        PtclFile resident = LoadResident();
        PtclFile donor = Corpus.LoadTotk(Corpus.TotkFiles().First(f => Path.GetFileName(f).StartsWith("Kohga_Golem_Beam", StringComparison.Ordinal)));
        int files = 0, models = 0, primitives = 0, converted = 0;
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 11 == 0))
        {
            PtclFile botw = Corpus.LoadBotw(path);
            var catalog = new BotwModelCatalog(botw, resident);
            ModelPlan modelPlan = ModelPlanner.Plan(botw, catalog, totk);
            PrimitivePlan primitivePlan = PrimitivePlan.Plan(botw, catalog, totk);
            Assert.Empty(modelPlan.Unresolved);
            Assert.DoesNotContain(primitivePlan.Items, i => i.Node is null);
            converted += modelPlan.Items.Count(i => i.Disposition == ModelDisposition.Convert);

            VfxbDocument document = VfxbDocument.From(donor.Vfxb);
            G3dBuilder.Build(modelPlan, totk).ApplyTo(document);
            primitivePlan.ApplyTo(document);
            VfxbFile read = VfxbReader.Read(document.ToBytes(), VfxbLayouts.TotK);

            G3dContent? content = G3dContent.Read(read);
            var ids = content?.Entries.Select(e => (ulong)e.Id).ToHashSet() ?? [];
            Assert.Equal(modelPlan.CarriedIds, ids);
            if (content is not null) Assert.Equal(content.ModelCount, content.Open().Models.Count);
            models += ids.Count;
            var primIds = PrimitiveContent.Read(read).Primitives.Select(p => p.Id).ToHashSet();
            Assert.Equal(primitivePlan.CarriedIds, primIds);
            primitives += primIds.Count;
            files++;
        }
        Assert.True(files > 25);
        Assert.True(models > 30);
        Assert.True(primitives > 0);
        Assert.True(converted > 0, "the sample should exercise the BotW conversion path too");
    }
}
