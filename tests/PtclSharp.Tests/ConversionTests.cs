using PtclSharp.Conversion;
using PtclSharp.Layout;
using PtclSharp.Writer;

namespace PtclSharp.Tests;

/// <summary>Runs when both game corpora are available.</summary>
public sealed class BothCorporaFactAttribute : FactAttribute
{
    public BothCorporaFactAttribute()
    {
        if (Corpus.BotwRoot is null || Corpus.TotkRoot is null)
            Skip = $"Set {Corpus.BotwEnv} and {Corpus.TotkEnv} to run the conversion tests.";
    }
}

public class ConversionTests
{
    private static readonly PtclLayoutSet Botw = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);
    private static readonly PtclLayoutSet Totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

    private sealed record Donor(PtclFile File, VfxbDocument Document, ShaderBinding Binding);

    private static Donor LoadDonor()
    {
        // The first TotK file with an emitter that carries a custom-shader chunk.
        foreach (string path in Corpus.TotkFiles())
        {
            PtclFile ptcl = Corpus.LoadTotk(path);
            VfxbDocument doc = VfxbDocument.From(ptcl.Vfxb);
            VfxbTreeNode? emitter = doc.Sets.SelectMany(s => s.Children).FirstOrDefault(e => e.Attributes.Any(a => a.Kind == "CSDP"));
            if (emitter is null) continue;
            byte[] csdp = emitter.Attributes.First(a => a.Kind == "CSDP").Data!;
            return new Donor(ptcl, doc, ShaderBinding.FromDonorEmitter(emitter.Data!, csdp));
        }
        throw new InvalidOperationException("No donor emitter found.");
    }

    private static ConverterOptions OptionsFor(Donor donor) => new() { ShaderBinder = _ => donor.Binding };

    [BothCorporaFact]
    public void EveryBotwFileConvertsToAReadableTotkFile()
    {
        Donor donor = LoadDonor();
        int files = 0;
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 4 == 0))
        {
            PtclFile botw = Corpus.LoadBotw(path);
            VfxbDocument converted = BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donor.Document, OptionsFor(donor), out ConversionReport report);
            byte[] bytes = converted.ToBytes();

            VfxbFile read = VfxbReader.Read(bytes, VfxbLayouts.TotK); // validates declared emitter counts as well
            Assert.Equal(botw.Vfxb.EmitterSets.Select(s => s.Name), read.EmitterSets.Select(s => s.Name));
            Assert.Equal(botw.Vfxb.EmitterSets.Select(s => s.Emitters.Count), read.EmitterSets.Select(s => s.Emitters.Count));
            Assert.Equal(botw.Vfxb.EmitterSets.Sum(s => s.Emitters.Count), report.EmittersConverted);
            files++;
        }
        Assert.True(files > 200);
    }

    [BothCorporaFact]
    public void ProvenFieldsAreCarriedAcrossUnchanged()
    {
        Donor donor = LoadDonor();
        int compared = 0;
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 9 == 0))
        {
            PtclFile botw = Corpus.LoadBotw(path);
            VfxbFile converted = VfxbReader.Read(
                BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donor.Document, OptionsFor(donor), out _).ToBytes(), VfxbLayouts.TotK);

            var before = botw.Vfxb.EmitterSets.SelectMany(s => s.Emitters).ToArray();
            var after = converted.EmitterSets.SelectMany(s => s.Emitters).ToArray();
            for (int i = 0; i < before.Length; i++)
            {
                StructView b = botw.Vfxb.EmitterView(before[i]);
                StructView t = converted.EmitterView(after[i]);
                Assert.Equal(b.GetString("emitter_name"), t.GetString("emitter_name"));
                foreach (FieldDef f in Botw.Emitter.Fields.Where(f => f.Status == FieldStatus.Confirmed))
                {
                    if (f.Name is "emitter_name" or "shader_idx_normal" or "shader_idx_pass1" or "shader_idx_pass2" or "compute_shader0"
                        or "custom_shader_index" or "custom_action_index") continue;
                    if (!Totk.Emitter.TryGetField(f.Name, out FieldDef? to) || to.Type != f.Type || to.Count != f.Count || to.Status == FieldStatus.Unused) continue;
                    Assert.True(b.GetBytes(f.Name).SequenceEqual(t.GetBytes(f.Name)), $"{before[i].Name}.{f.Name} changed in conversion");
                    compared++;
                }

                // Renamed and narrowed fields.
                Assert.Equal(b.GetByte("depth_compare_func"), t.GetByte("depth_stencil_mode_index"));
                Assert.Equal((byte)Math.Min(255, b.GetInt32("emit_rate_random_percent")), t.GetByte("emit_rate_random_percent"));
                Assert.Equal((byte)Math.Min(255, b.GetInt32("particle_lifespan_random_percent")), t.GetByte("particle_lifespan_random_percent"));
                Assert.Equal((ushort)Math.Min(65535, b.GetInt32("loop_color0_period_i32")), t.GetUInt16("loop_color0_period_u16"));
            }
        }
        Assert.True(compared > 10000);
    }

    [BothCorporaFact]
    public void ConvertedChunksFitTheTotkLayouts()
    {
        Donor donor = LoadDonor();
        var kinds = new HashSet<string>();
        foreach (string path in Corpus.BotwFiles().Where((_, i) => i % 6 == 0))
        {
            PtclFile botw = Corpus.LoadBotw(path);
            VfxbDocument converted = BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donor.Document, OptionsFor(donor), out _);
            foreach (VfxbTreeNode emitter in converted.Sets.SelectMany(s => s.Children))
                foreach (VfxbTreeNode chunk in emitter.Attributes)
                {
                    if (!Totk.Chunks.TryGetValue(chunk.Kind, out ChunkLayout? layout)) { Assert.Equal("CSDP", chunk.Kind); continue; }
                    kinds.Add(chunk.Kind);
                    int expected = layout.Repeating is { } rep
                        ? rep.PayloadSize((int)new StructView(layout, chunk.Data!.AsSpan(0, layout.Size).ToArray()).GetUInt32(rep.CountField))
                        : layout.Size;
                    Assert.Equal(expected, chunk.Data!.Length);
                }
        }
        foreach (string kind in new[] { "EAC0", "FRND", "FSPN", "FCOV", "FCLN", "FCSF", "EP02", "EP03" })
            Assert.Contains(kind, kinds);
    }

    [BothCorporaFact]
    public void ConversionIsDeterministicAndReportsWhatItCannotCarry()
    {
        Donor donor = LoadDonor();
        string path = Corpus.BotwFiles().First(f => Path.GetFileName(f) == "AncientBall.sesetlist");
        VfxbDocument source = VfxbDocument.From(Corpus.LoadBotw(path).Vfxb);

        byte[] a = BotwToTotkConverter.ToTotk(source, donor.Document, OptionsFor(donor), out ConversionReport report).ToBytes();
        byte[] b = BotwToTotkConverter.ToTotk(source, donor.Document, OptionsFor(donor), out _).ToBytes();
        Assert.Equal(a, b);
        Assert.False(report.Of(ConversionSeverity.Warning).Count() == 0, "Expected at least one warning for a real effect.");

        // Without a shader binding the report must say the result is not usable yet.
        BotwToTotkConverter.ToTotk(source, donor.Document, new ConverterOptions(), out ConversionReport unbound);
        Assert.True(unbound.HasErrors);
        Assert.Contains(unbound.Notes, n => n.Message.StartsWith("No shader binding", StringComparison.Ordinal));
    }

    [BothCorporaFact]
    public void ConvertedFileCanBePackedAsAnEsetb()
    {
        Donor donor = LoadDonor();
        PtclFile botw = Corpus.LoadBotw(Corpus.BotwFiles().First(f => Path.GetFileName(f) == "AncientBall.sesetlist"));
        VfxbDocument converted = BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donor.Document, OptionsFor(donor), out _);

        string[] sets = botw.Vfxb.EmitterSets.Select(s => s.Name).ToArray();
        byte[] packed = PtclWriter.WriteEsetb(donor.File, converted.ToBytes(), Corpus.TotkDictionary, sets, donor.File.Textures);
        PtclFile again = PtclFile.ReadEsetb(packed, Corpus.TotkDictionary);
        Assert.Equal(sets, again.DeclaredEmitterSets);
        Assert.Equal(sets, again.Vfxb.EmitterSets.Select(s => s.Name));
    }
}
