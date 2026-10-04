using System.Buffers.Binary;
using System.Text;
using PtclSharp.Layout;

namespace PtclSharp.Tests;

/// <summary>
/// Checks the layout tables and the documented container rules against every shipped TotK effect file.
/// These turn claims in docs/research into regressions: if a rule stops holding, a test fails.
/// </summary>
public class TotkCorpusTests
{
    [TotkCorpusFact]
    public void EveryShippedFileLoads()
    {
        var failures = new List<string>();
        int count = 0;
        foreach (string file in Corpus.TotkFiles())
        {
            count++;
            try { Corpus.LoadTotk(file); }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
        Assert.True(count > 1000, $"Expected the full corpus, found {count} files.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [TotkCorpusFact]
    public void DeclaredEmitterCountEqualsAllEmitterNodes()
    {
        foreach (string file in Corpus.TotkFiles())
        {
            PtclFile ptcl = Corpus.LoadTotk(file);
            foreach (VfxbEmitterSet set in ptcl.Vfxb.EmitterSets)
                Assert.Equal(set.DeclaredEmitterCount, set.Emitters.Count);
        }
    }

    [TotkCorpusFact]
    public void TopLevelNodeOrderAndHeaderChildCountsHold()
    {
        string[] expected = ["ESTA", "PRMA", "TRMA", "G3PR", "GRSN"];
        foreach (string file in Corpus.TotkFiles())
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            Assert.Equal(expected, vfxb.Roots.Select(r => r.Kind));

            // The header child count is bounded by what the reader actually found for the counted kinds.
            // G3PR is the exception: its header says 1 even in the files that carry no G3D data (no child node).
            foreach (VfxbNode node in vfxb.Roots)
                if (node.Kind is "ESTA" or "PRMA" || (node.Kind == "G3PR" && node.ChildRelativeOffset > 0))
                    Assert.Equal(node.DeclaredChildCount, node.Children.Count);

            VfxbNode grsn = vfxb.Roots.Single(r => r.Kind == "GRSN");
            Assert.NotEmpty(grsn.Children); // GRSR in nearly every file; a few ship only other shader-resource children
        }
    }

    [TotkCorpusFact]
    public void EmitterDataIsAlignedAndAttributePayloadsMatchTheirSize()
    {
        foreach (string file in Corpus.TotkFiles())
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
                foreach (VfxbEmitter emitter in set.Emitters)
                {
                    Assert.Equal(0, emitter.Node.DataOffset!.Value % 0x100);
                    foreach (VfxbNode attr in emitter.Node.Attributes)
                    {
                        Assert.Equal(0x20, attr.DataRelativeOffset);
                        if (attr.SiblingRelativeOffset > 0)
                            Assert.Equal((int)attr.Size, attr.SiblingRelativeOffset); // size includes the 0x20 header
                    }
                }
        }
    }

    [TotkCorpusFact]
    public void ChunkLayoutsFitEveryShippedChunk()
    {
        PtclLayoutSet layouts = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);
        var seen = new HashSet<string>();
        foreach (string file in Corpus.TotkFiles())
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
                foreach (VfxbEmitter emitter in set.Emitters)
                    foreach (VfxbNode attr in emitter.Node.Attributes)
                    {
                        if (!layouts.Chunks.TryGetValue(attr.Kind, out ChunkLayout? layout)) continue;
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
        // Every mapped chunk kind that exists in shipped data was exercised (FPAD/FGWD never ship).
        foreach (string kind in new[] { "EAC0", "EASL", "FRND", "FRN1", "FMAG", "FSPN", "FCOL", "FCOV", "FCLN", "EP01", "EP02", "EP03", "EP04" })
            Assert.Contains(kind, seen);
    }

    [TotkCorpusFact]
    public void EmitterFieldsReadBackAndRewriteToIdenticalBytes()
    {
        PtclLayoutSet layouts = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);
        int checkedEmitters = 0;
        foreach (string file in Corpus.TotkFiles().Where((_, i) => i % 7 == 0)) // a spread of files keeps the run fast
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
                foreach (VfxbEmitter emitter in set.Emitters)
                {
                    int offset = emitter.Node.DataOffset!.Value;
                    byte[] original = vfxb.Data.AsSpan(offset, layouts.Emitter.Size).ToArray();
                    byte[] copy = (byte[])original.Clone();
                    var view = new StructView(layouts.Emitter, copy);

                    foreach (FieldDef f in layouts.Emitter.Fields)
                    {
                        if (f.Type is FieldType.Bytes or FieldType.String) continue;
                        for (int i = 0; i < f.Count; i++) Rewrite(view, f, i);
                    }
                    Assert.Equal(original, copy);

                    // The names the reader exposes agree with the layout-driven view.
                    Assert.Equal(emitter.Name, new StructView(layouts.Emitter, original).GetString("emitter_name"));
                    checkedEmitters++;
                }
        }
        Assert.True(checkedEmitters > 500);
    }

    [TotkCorpusFact]
    public void TextureGuidFieldsMatchTheReaderAndUnusedSlotsAreAllOnes()
    {
        PtclLayoutSet layouts = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);
        foreach (string file in Corpus.TotkFiles().Where((_, i) => i % 5 == 0))
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
                foreach (VfxbEmitter emitter in set.Emitters)
                {
                    var view = vfxb.EmitterView(emitter);
                    for (int slot = 0; slot < layouts.Emitter.TextureSlotCount; slot++)
                    {
                        ulong guid = view.GetUInt64($"tex_slot{slot}_guid");
                        ulong? expected = emitter.TextureSamplerGuids[slot];
                        Assert.Equal(expected ?? ulong.MaxValue, guid);
                    }
                }
        }
    }

    [TotkCorpusFact]
    public void EmitterSetViewReadsNameAndCount()
    {
        foreach (string file in Corpus.TotkFiles().Where((_, i) => i % 11 == 0))
        {
            VfxbFile vfxb = Corpus.LoadTotk(file).Vfxb;
            foreach (VfxbEmitterSet set in vfxb.EmitterSets)
            {
                var view = vfxb.EmitterSetView(set);
                Assert.Equal(set.Name, view.GetString("name"));
                Assert.Equal(set.DeclaredEmitterCount, view.GetUInt16("emitter_count"));
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
                // Rewrite through the raw bits so NaN payloads cannot hide a mismatch.
                int bits = BinaryPrimitives.ReadInt32LittleEndian(view.Data[(f.Offset + (index * 4))..]);
                BinaryPrimitives.WriteInt32LittleEndian(view.Data[(f.Offset + (index * 4))..], bits);
                Assert.Equal(bits, BitConverter.SingleToInt32Bits(view.GetSingle(f.Name, index)));
                break;
            default: throw new InvalidOperationException(f.ToString());
        }
    }
}
