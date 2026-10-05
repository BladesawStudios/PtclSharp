using PtclSharp.Textures;
using TxtgSharp;
using BntxSharp;

namespace PtclSharp.Tests;

public class TextureTests
{
    private static BotwTextureCatalog ResidentCatalog() =>
        BotwTextureCatalog.ForResident(ResidentFile.LoadFromPack(Path.Combine(Corpus.BotwRoot!, "Pack", "Bootup.pack")));

    private static TotkTextureIndex TotkIndex() =>
        TotkTextureIndex.Build(Path.Combine(Corpus.TotkRoot!, "Effect"), Corpus.TotkDictionary, Path.Combine(Corpus.TotkRoot!, "TexToGo"));

    [BotwCorpusFact]
    public void GtntNamesTheTexturesOfAFile()
    {
        PtclFile file = Corpus.LoadBotw(Path.Combine(Corpus.BotwRoot!, "Effect", "GanonBeastBeam.sesetlist"));
        BotwTextureTable table = BotwTextureTable.Read(file.Vfxb);
        BotwTextureName entry = Assert.Single(table.Names);
        Assert.Equal(0xBB8237F5UL, entry.Guid);
        Assert.Equal("mask_lastbossarealine", entry.Name);

        BotwTextureCatalog catalog = BotwTextureCatalog.For(file, null);
        BotwTexture texture = Assert.IsType<BotwTexture>(catalog.Find(0xBB8237F5UL));
        Assert.Equal(64, texture.Pixels!.Width);
        Assert.Equal(SurfaceFormat.BC4_UNORM, texture.Pixels.Format);
    }

    [BotwCorpusFact]
    public void TheResidentFileHoldsTheSharedPool()
    {
        BotwTextureCatalog resident = ResidentCatalog();
        Assert.True(resident.Count > 200);
        Assert.NotNull(resident.Find(0x1FF60D1CUL));
        Assert.NotNull(resident.Find(0x99C7465AUL));
    }

    [BothCorporaFact]
    public void SharedGuidsNameTheSameTextureInBothGames()
    {
        BotwTextureCatalog resident = ResidentCatalog();
        TotkTextureIndex totk = TotkIndex();
        int shared = 0;
        foreach (BotwTexture t in resident.Textures)
        {
            if (!totk.TryGetName((uint)t.Guid, out string name)) continue;
            Assert.Equal(t.Name, name, ignoreCase: true);
            shared++;
        }
        Assert.True(shared > 150);
    }

    [BothCorporaFact]
    public void MostBotwTexturesAreFoundAndEveryImportConverts()
    {
        BotwTextureCatalog resident = ResidentCatalog();
        TotkTextureIndex totk = TotkIndex();
        int refs = 0, reused = 0, unresolved = 0, imported = 0;
        var converted = new HashSet<string>();
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 5 == 0))
        {
            PtclFile botw = Corpus.LoadBotw(path);
            TexturePlan plan = TexturePlanner.Plan(botw, BotwTextureCatalog.For(botw, resident), totk);
            foreach (TexturePlanItem item in plan.Items)
            {
                refs++;
                switch (item.Disposition)
                {
                    case TextureDisposition.Unresolved: unresolved++; break;
                    case TextureDisposition.Import:
                        imported++;
                        if (!converted.Add(item.Name!)) break;
                        BntxTexture source = item.Source!.Pixels!;
                        TxtgConversion result = BntxToTxtg.Convert(source, compressionLevel: 3);
                        TxtgFile back = TxtgFile.FromBytes(result.Bytes);
                        Assert.Equal(source.Width, back.Width);
                        Assert.Equal(source.Height, back.Height);
                        Assert.Equal(source.MipCount, back.MipCount);
                        if (source.Format is not (SurfaceFormat.BC4_SNORM or SurfaceFormat.BC5_SNORM))
                            Assert.Equal(source.ToRgba8(), back.ToRgba8());
                        break;
                    default: reused++; break;
                }
            }
        }
        Assert.True(refs > 100);
        Assert.True(reused > refs / 2, $"reused {reused} of {refs}");
        Assert.True(imported > 0);
        Assert.True(unresolved < refs / 10, $"{unresolved} of {refs} references stayed unresolved");
    }
}
