using System.Buffers.Binary;
using PtclSharp.Layout;

namespace PtclSharp.Tests;

/// <summary>
/// Checks the BotW layout tables and the claims in docs/research/botw-emtr-offsets-ghidra.md against every shipped BotW
/// effect file. Runs only when <c>PTCL_BOTW_ROM</c> points at the ROM root.
/// </summary>
public class BotwCorpusTests
{
    private static readonly PtclLayoutSet Layouts = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);

    private static IEnumerable<(string File, VfxbFile Vfxb, VfxbEmitter Emitter)> Emitters(int step = 1)
    {
        foreach (string file in Corpus.BotwFiles().Where((_, i) => i % step == 0))
        {
            VfxbFile vfxb = Corpus.LoadBotw(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
                foreach (VfxbEmitter emitter in set.Emitters)
                    yield return (file, vfxb, emitter);
        }
    }

    [BotwCorpusFact]
    public void EveryShippedFileLoads()
    {
        var failures = new List<string>();
        int count = 0;
        foreach (string file in Corpus.BotwFiles())
        {
            count++;
            try { Corpus.LoadBotw(file); }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
        Assert.True(count > 800, $"Expected the full corpus, found {count} files.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [BotwCorpusFact]
    public void DeclaredEmitterCountEqualsAllEmitterNodes()
    {
        foreach (string file in Corpus.BotwFiles())
            foreach (VfxbEmitterSet set in Corpus.LoadBotw(file).Vfxb.EmitterSets)
                Assert.Equal(set.DeclaredEmitterCount, set.Emitters.Count);
    }

    [BotwCorpusFact]
    public void EmitterDataIsAlignedAndFitsTheFile()
    {
        int emitters = 0;
        foreach (var (_, vfxb, emitter) in Emitters())
        {
            Assert.Equal(0, emitter.Node.DataOffset!.Value % 0x100);
            Assert.True(emitter.Node.DataOffset.Value + Layouts.Emitter.Size <= vfxb.Data.Length);
            emitters++;
        }
        Assert.True(emitters > 8000);
    }

    [BotwCorpusFact]
    public void EmitterFieldsReadBackAndRewriteToIdenticalBytes()
    {
        int checkedEmitters = 0;
        foreach (var (_, vfxb, emitter) in Emitters(step: 7))
        {
            int offset = emitter.Node.DataOffset!.Value;
            byte[] original = vfxb.Data.AsSpan(offset, Layouts.Emitter.Size).ToArray();
            byte[] copy = (byte[])original.Clone();
            var view = new StructView(Layouts.Emitter, copy);
            foreach (FieldDef f in Layouts.Emitter.Fields)
            {
                if (f.Type is FieldType.Bytes or FieldType.String) continue;
                for (int i = 0; i < f.Count; i++) Rewrite(view, f, i);
            }
            Assert.Equal(original, copy);
            Assert.Equal(emitter.Name, new StructView(Layouts.Emitter, original).GetString("emitter_name"));
            checkedEmitters++;
        }
        Assert.True(checkedEmitters > 500);
    }

    [BotwCorpusFact]
    public void TextureGuidFieldsMatchTheReader()
    {
        foreach (var (_, vfxb, emitter) in Emitters(step: 5))
        {
            var view = vfxb.EmitterView(emitter);
            for (int slot = 0; slot < Layouts.Emitter.TextureSlotCount; slot++)
            {
                ulong guid = view.GetUInt64($"tex_slot{slot}_guid");
                Assert.Equal(emitter.TextureSamplerGuids[slot] ?? ulong.MaxValue, guid);
            }
        }
    }

    /// <summary>Fields the engine overwrites at run time, and bytes the map documents as always zero.</summary>
    [BotwCorpusFact]
    public void DocumentedRuntimeAndZeroFieldsAreZeroInEveryFile()
    {
        string[] zero =
        [
            "unused_000_00F", "runtime_shader_flags_word0", "runtime_shader_flags_word1", "unused_058_05B", "runtime_attribute_word",
            "unused_078_07F", "runtime_loop_track0_rate", "runtime_loop_track1_rate", "runtime_loop_track2_rate", "runtime_loop_track3_rate",
            "runtime_loop_track4_rate", "runtime_loop_track0_random_enable", "runtime_loop_track4_random_enable", "unused_0A8_0AF",
            "unused_0C4_0CF", "unused_0DC_0DF", "unused_108_10F", "unused_3B4_3BF", "unused_5E4_5E7", "unused_5EC_5EF", "unused_5F8_5FF",
            "unused_70C_70F", "unused_71C_71F", "unused_73C_73F", "unused_748_74F", "unused_75D_75F", "unused_7DB",
            "unused_7E5_7E7", "unused_83B", "unused_920_923"
        ];
        foreach (var (file, vfxb, emitter) in Emitters(step: 3))
        {
            var data = vfxb.Data.AsSpan(emitter.Node.DataOffset!.Value, Layouts.Emitter.Size);
            foreach (string name in zero)
            {
                FieldDef f = Layouts.Emitter[name];
                Assert.True(!data.Slice(f.Offset, f.ByteLength).ContainsAnyExcept((byte)0), $"{Path.GetFileName(file)} {emitter.Name}: {name} is not zero");
            }
        }
    }

    [BotwCorpusFact]
    public void ValueRangesFollowTheDecodedEngineBehavior()
    {
        foreach (var (_, vfxb, emitter) in Emitters(step: 3))
        {
            var v = vfxb.EmitterView(emitter);
            foreach (string key in new[] { "color0_key_count", "alpha0_key_count", "color1_key_count", "alpha1_key_count", "scale_key_count", "track5_key_count" })
                Assert.InRange(v.GetUInt32(key), 0u, 8u);
            Assert.InRange(v.GetByte("seed_source"), (byte)0, (byte)2);          // Emitter::Initialize switch (0x7100ad583c)
            Assert.InRange(v.GetByte("velocity_coord"), (byte)0, (byte)2);       // three-entry feature table (0x7100adc8a8)
            Assert.InRange(v.GetByte("depth_compare_func"), (byte)0, (byte)7);   // accepted when < 8 (0x7100ae2834)
            Assert.InRange(v.GetByte("blend_mode_index"), (byte)0, (byte)4);     // packed table of 5 entries
            Assert.InRange(v.GetByte("cull_mode_index"), (byte)0, (byte)2);      // packed table of 3 entries
            foreach (string slot in new[] { "tex0", "tex1", "tex2" })
                Assert.InRange(v.GetByte($"{slot}_uv_domain_scale_mode"), (byte)0, (byte)3); // table index < 4 (0x7100adb8f4)
        }
    }

    [BotwCorpusFact]
    public void ChunkLayoutsFitEveryShippedChunk()
    {
        var seen = new HashSet<string>();
        foreach (var (_, vfxb, emitter) in Emitters())
        {
            foreach (VfxbNode attr in emitter.Node.Attributes)
            {
                if (!Layouts.Chunks.TryGetValue(attr.Kind, out ChunkLayout? layout)) continue;
                seen.Add(attr.Kind);
                int payload = (int)attr.Size - 0x20;
                int dataOffset = attr.DataOffset!.Value;
                if (layout.Repeating is { } rep)
                {
                    var view = new StructView(layout, vfxb.Data.AsSpan(dataOffset, layout.Size));
                    int keys = (int)view.GetUInt32(rep.CountField);
                    Assert.Equal(rep.PayloadSize(keys), payload);
                    Assert.InRange(keys, 1, 12);
                }
                else
                {
                    Assert.Equal(layout.Size, payload);
                }
            }
        }
        foreach (string kind in new[] { "EAC0", "EASL", "FRND", "FRN1", "FMAG", "FSPN", "FCOV", "FCLN", "FCSF", "FPAD", "EP01", "EP02", "EP03", "EP04" })
            Assert.Contains(kind, seen);
    }

    [BotwCorpusFact]
    public void ChunkBytesDocumentedAsZeroAreZero()
    {
        foreach (var (file, vfxb, emitter) in Emitters(step: 2))
            foreach (VfxbNode attr in emitter.Node.Attributes)
            {
                if (!Layouts.Chunks.TryGetValue(attr.Kind, out ChunkLayout? layout) || layout.Repeating is not null) continue;
                var data = vfxb.Data.AsSpan(attr.DataOffset!.Value, layout.Size);
                foreach (FieldDef f in layout.Fields.Where(f => f.IsUnused && f.Evidence.Contains("always 0")))
                    Assert.True(!data.Slice(f.Offset, f.ByteLength).ContainsAnyExcept((byte)0), $"{Path.GetFileName(file)} {attr.Kind}.{f.Name} is not zero");
            }
    }

    [BotwCorpusFact]
    public void EmitterSetViewReadsNameAndCount()
    {
        foreach (string file in Corpus.BotwFiles().Where((_, i) => i % 11 == 0))
        {
            VfxbFile vfxb = Corpus.LoadBotw(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
            {
                var view = vfxb.EmitterSetView(set);
                Assert.Equal(set.Name, view.GetString("name"));
                Assert.Equal(set.DeclaredEmitterCount, view.GetInt32("emitter_count"));
            }
        }
    }

    private static void Rewrite(StructView view, FieldDef f, int index)
    {
        switch (f.Type)
        {
            case FieldType.U8: view.SetByte(f.Name, view.GetByte(f.Name, index), index); break;
            case FieldType.I8: view.SetSByte(f.Name, view.GetSByte(f.Name, index), index); break;
            case FieldType.U16: view.SetUInt16(f.Name, view.GetUInt16(f.Name, index), index); break;
            case FieldType.I16: view.SetInt16(f.Name, view.GetInt16(f.Name, index), index); break;
            case FieldType.U32: view.SetUInt32(f.Name, view.GetUInt32(f.Name, index), index); break;
            case FieldType.I32: view.SetInt32(f.Name, view.GetInt32(f.Name, index), index); break;
            case FieldType.U64: view.SetUInt64(f.Name, view.GetUInt64(f.Name, index), index); break;
            case FieldType.I64: view.SetInt64(f.Name, view.GetInt64(f.Name, index), index); break;
            case FieldType.F32:
                int bits = BinaryPrimitives.ReadInt32LittleEndian(view.Data[(f.Offset + (index * 4))..]);
                BinaryPrimitives.WriteInt32LittleEndian(view.Data[(f.Offset + (index * 4))..], bits);
                Assert.Equal(bits, BitConverter.SingleToInt32Bits(view.GetSingle(f.Name, index)));
                break;
            default: throw new InvalidOperationException(f.ToString());
        }
    }
}
