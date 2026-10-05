using PtclSharp.Conversion;
using PtclSharp.Layout;
using PtclSharp.Shaders;
using PtclSharp.Writer;

namespace PtclSharp.Tests;

/// <summary>Runs when both corpora and a shader database (<c>PTCL_SHADER_DB</c>, built with PtclShaderDb) are available.</summary>
public sealed class ShaderDbFactAttribute : FactAttribute
{
    public ShaderDbFactAttribute()
    {
        if (Corpus.BotwRoot is null || Corpus.TotkRoot is null || ShaderTests.DatabasePath is null)
            Skip = $"Set {Corpus.BotwEnv}, {Corpus.TotkEnv} and {ShaderTests.DatabaseEnv} (a database built with PtclShaderDb) to run the shader tests.";
    }
}

public class ShaderTests
{
    internal const string DatabaseEnv = "PTCL_SHADER_DB";

    internal static string? DatabasePath
    {
        get
        {
            string? path = Environment.GetEnvironmentVariable(DatabaseEnv);
            return path is not null && File.Exists(path) ? path : null;
        }
    }

    private static readonly PtclLayoutSet Botw = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);
    private static readonly PtclLayoutSet Totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

    private static ProgramSignature Program(string id, string[] samplers, int[] reads, string[]? inputs = null) =>
        new(id, "VF", inputs ?? ["sysPosAttr"], samplers, [], [], [], [], reads, []);

    private static ShaderFileEntry TotkFile(string name, params (string Id, string Emitter, string Slots)[] programs) =>
        new(ShaderGame.TotK, name, programs.Select(p => p.Id).ToArray(), [],
            programs.Select((p, i) => new ShaderEmitterUse(p.Emitter, i, -1, -1, -1, -1, -1, -1, p.Slots)).ToArray());

    /// <summary>A small database in which the right answer is obvious, so the search logic can be checked without game files.</summary>
    private static (ShaderDatabase Db, BotwEmitterShaders Source) Fixture()
    {
        int scroll = Totk.Emitter["tex0_mode_index"].Offset & ~3;
        int botwScroll = Botw.Emitter["tex0_mode_index"].Offset & ~3;

        var db = new ShaderDatabase();
        db.Programs["scrolls"] = Program("scrolls", ["sysTextureSampler0"], [scroll]);
        db.Programs["plain"] = Program("plain", ["sysTextureSampler0"], []);
        db.Programs["twoTextures"] = Program("twoTextures", ["sysTextureSampler0", "sysTextureSampler1"], [scroll]);
        db.Files.Add(TotkFile("A", ("plain", "PlainOne", "100"), ("twoTextures", "TwoTex", "110")));
        db.Files.Add(TotkFile("B", ("scrolls", "ScrollOne", "100")));

        ProgramSignature botwProgram = Program("botw", ["sysTextureSampler0"], [botwScroll]);
        byte[] data = new byte[Botw.Emitter.Size];
        data[Botw.Emitter["tex0_mode_index"].Offset] = 1; // the emitter really uses the field its program reads
        return (db, new BotwEmitterShaders("Source", data, botwProgram, null, null, null));
    }

    [Fact]
    public void ProgramsThatSampleDifferentTextureSlotsAreNeverDonors()
    {
        (ShaderDatabase db, BotwEmitterShaders source) = Fixture();
        IReadOnlyList<DonorCandidate> ranked = new DonorFinder(db).Rank(source);
        Assert.DoesNotContain(ranked, c => c.Emitter.Name == "TwoTex");
        Assert.Equal(2, ranked.Count);
    }

    [Fact]
    public void ProgramsThatReadTheFieldsTheSourceUsesRankFirst()
    {
        (ShaderDatabase db, BotwEmitterShaders source) = Fixture();
        IReadOnlyList<DonorCandidate> ranked = new DonorFinder(db).Rank(source);
        Assert.Equal("ScrollOne", ranked[0].Emitter.Name);
        Assert.True(ranked[0].Score > ranked[1].Score);
        Assert.Contains(ranked[1].Notes, n => n.Contains("tex0_mode_index"));
    }

    [Fact]
    public void ThePlanPicksTheArchiveThatServesTheWholeEffect()
    {
        (ShaderDatabase db, BotwEmitterShaders source) = Fixture();
        BotwEmitterShaders twoTextures = source with
        {
            Name = "Second",
            Normal = Program("botw2", ["sysTextureSampler0", "sysTextureSampler1"], [Botw.Emitter["tex0_mode_index"].Offset & ~3])
        };
        IReadOnlyList<DonorPlan> plans = new DonorFinder(db).Plan([source, twoTextures]);

        // Archive A has both a one-texture and a two-texture program; B only has the one-texture program.
        Assert.Equal("A", plans[0].File.Name);
        Assert.All(plans[0].Emitters, e => Assert.NotNull(e.Donor));
        Assert.Contains(plans[1].Emitters, e => e.Donor is null);
    }

    [Fact]
    public void TheDatabaseSurvivesASaveAndLoad()
    {
        (ShaderDatabase db, _) = Fixture();
        string path = Path.Combine(Path.GetTempPath(), $"shaderdb-{Guid.NewGuid():N}.json.gz");
        try
        {
            db.Save(path);
            ShaderDatabase back = ShaderDatabase.Load(path);
            Assert.Equal(db.Programs.Keys.Order(), back.Programs.Keys.Order());
            Assert.Equal(db.Files.Select(f => f.Name), back.Files.Select(f => f.Name));
            Assert.Equal(db.Programs["scrolls"].FragmentReads, back.Programs["scrolls"].FragmentReads);
            Assert.Equal(1, back.Programs["plain"].TextureSlotMask);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BotwFieldsMapToTheirTotkCounterparts()
    {
        Assert.Equal("tex0_mode_index", BotwToTotkConverter.TotkFieldFor("tex0_mode_index"));
        Assert.Equal("loop_color0_period_u16", BotwToTotkConverter.TotkFieldFor("loop_color0_period_i32"));
        Assert.Equal("unverified_890", BotwToTotkConverter.TotkFieldFor("unverified_5C0"));
        Assert.Null(BotwToTotkConverter.TotkFieldFor("unverified_5C0", includeUnverifiedAnalogs: false));
        Assert.Null(BotwToTotkConverter.TotkFieldFor("gpu_accel_dir_xyz"));
    }

    [ShaderDbFact]
    public void ADonorChosenFromTheDatabaseGivesEveryEmitterAShader()
    {
        var finder = new DonorFinder(ShaderDatabase.Load(DatabasePath!));
        string path = Corpus.BotwFiles().First(f => Path.GetFileName(f) == "AncientBall.sesetlist");
        PtclFile botw = Corpus.LoadBotw(path);
        IReadOnlyList<BotwEmitterShaders> sources = finder.SourceEmitters("AncientBall", botw)
            ?? throw new InvalidOperationException("AncientBall is not in the database.");
        Assert.Equal(botw.Vfxb.EmitterSets.Sum(s => s.Emitters.Count), sources.Count);

        DonorPlan plan = finder.Plan(sources, top: 1)[0];
        Assert.True(plan.Score > 0.5, $"best archive scored only {plan.Score:F2}");

        PtclFile donor = PtclFile.ReadEsetb(File.ReadAllBytes(Path.Combine(Corpus.TotkRoot!, "Effect", plan.File.Name + ".Nin_NX_NVN.esetb.byml.zs")), Corpus.TotkDictionary);
        VfxbDocument donorDoc = VfxbDocument.From(donor.Vfxb);
        var options = new ConverterOptions { ShaderBinder = DonorBinding.CreateBinder(plan, donorDoc) };
        BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donorDoc, options, out ConversionReport report);

        Assert.DoesNotContain(report.Notes, n => n.Message.StartsWith("No shader binding", StringComparison.Ordinal));
    }

    [ShaderDbFact]
    public void TotkOwnVersionOfASharedEffectScoresCloseToTheBestDonor()
    {
        ShaderDatabase db = ShaderDatabase.Load(DatabasePath!);
        var finder = new DonorFinder(db);
        var totk = db.FilesOf(ShaderGame.TotK).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int compared = 0, close = 0;
        foreach (ShaderFileEntry entry in db.FilesOf(ShaderGame.BotW).Where(f => totk.ContainsKey(f.Name)).Take(60))
        {
            PtclFile botw = Corpus.LoadBotw(Path.Combine(Corpus.BotwRoot!, "Effect", entry.Name + ".sesetlist"));
            ShaderFileEntry counterpart = totk[entry.Name];
            foreach (BotwEmitterShaders source in finder.SourceEmitters(entry.Name, botw)!.Take(6))
            {
                int truth = Array.FindIndex(counterpart.Emitters, e => e.Name == source.Name && db.Resolve(counterpart, e.Normal) is not null);
                if (truth < 0) continue;
                IReadOnlyList<DonorCandidate> ranked = finder.Rank(source, top: 4000);
                int rank = ranked.ToList().FindIndex(c => c.File == counterpart && c.EmitterIndex == truth);
                if (rank < 0) continue; // TotK re-authored it with different textures
                compared++;
                if (ranked[0].Score - ranked[rank].Score <= 0.05) close++;
            }
        }
        Assert.True(compared > 100, $"only {compared} comparable emitters");
        Assert.True(close > compared * 0.6, $"{close} of {compared} within 0.05 of the best donor");
    }
}
