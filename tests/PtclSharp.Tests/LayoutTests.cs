using PtclSharp.Layout;

namespace PtclSharp.Tests;

/// <summary>Self-consistency of the layout tables. These need no game files.</summary>
public class LayoutTests
{
    public static TheoryData<PtclVersion> Versions => new()
    {
        PtclVersion.BotW_NintendoWareVfx_4_4_0,
        PtclVersion.TotK_NintendoWareVfx2_15_3_1
    };

    [Theory, MemberData(nameof(Versions))]
    public void LayoutSetBuildsAndMatchesVfxbConstants(PtclVersion version)
    {
        PtclLayoutSet set = PtclLayouts.For(version);
        Assert.Equal(version, set.Version);
        Assert.Equal(set.Vfxb.EmitterFixedDataSize, set.Emitter.Size);
        Assert.Equal(set.Vfxb.EmitterSetFixedDataSize, set.EmitterSet.Size);
        Assert.Equal(set.Vfxb.TextureSamplerGuidOffsets.Count, set.Emitter.TextureSlotCount);
        Assert.Equal(0x20, set.NodeHeader.Size);
        Assert.Equal(0x40, set.FileHeader.Size);
    }

    [Theory, MemberData(nameof(Versions))]
    public void EmitterSetCountFieldAgreesWithTheReaderConstants(PtclVersion version)
    {
        PtclLayoutSet set = PtclLayouts.For(version);
        FieldDef count = set.EmitterSet["emitter_count"];
        Assert.Equal(set.Vfxb.EmitterSetEmitterCountOffset, count.Offset);
        Assert.Equal(set.Vfxb.EmitterSetEmitterCountSize, count.ByteLength);
    }

    [Theory, MemberData(nameof(Versions))]
    public void TextureGuidFieldsSitAtTheReaderOffsets(PtclVersion version)
    {
        PtclLayoutSet set = PtclLayouts.For(version);
        for (int slot = 0; slot < set.Vfxb.TextureSamplerGuidOffsets.Count; slot++)
        {
            FieldDef guid = set.Emitter[$"tex_slot{slot}_guid"];
            Assert.Equal(set.Vfxb.TextureSamplerGuidOffsets[slot], guid.Offset);
            Assert.Equal(FieldType.U64, guid.Type);
        }
    }

    [Fact]
    public void TotkEmitterFieldsAreFullyAccountedFor()
    {
        EmitterLayout emitter = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1).Emitter;

        // Every byte of the 0x10C8-byte block is either a named field or an explicit unverified range.
        Assert.Empty(emitter.Gaps);
        Assert.Equal(6, emitter.TextureSlotCount);
        Assert.Equal(10, emitter.KeyframeTrackCount);
        Assert.DoesNotContain(emitter.Fields, f => f.Status == FieldStatus.Paired);
        Assert.All(emitter.Fields, f =>
        {
            Assert.Equal(f.Name.StartsWith("unverified", StringComparison.Ordinal), f.Status == FieldStatus.Unverified);
            Assert.Equal(f.Name.StartsWith("unused", StringComparison.Ordinal), f.Status == FieldStatus.Unused);
        });
        // Unused bytes are never described as having a consumer, and guesses never sit on proven-unused fields.
        Assert.All(emitter.Fields.Where(f => f.IsUnused), f => Assert.Null(f.Hypothesis));
        Assert.Contains(emitter.Fields, f => f.Hypothesis is not null);
    }

    [Fact]
    public void BotwEmitterIsNotClaimedAsConfirmedBeforeItIsReverified()
    {
        EmitterLayout emitter = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0).Emitter;
        Assert.DoesNotContain(emitter.Fields, f => f.Status == FieldStatus.Confirmed);
        Assert.Contains(emitter.Fields, f => f.Status == FieldStatus.Paired);
        Assert.Null(emitter.KeyframeTrackCount);
    }

    [Fact]
    public void ConfirmedByteCountsAreReportedPerStatus()
    {
        EmitterLayout emitter = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1).Emitter;
        int total = Enum.GetValues<FieldStatus>().Sum(emitter.BytesWithStatus);
        Assert.Equal(emitter.Size, total); // no gaps, no overlaps: every byte has exactly one status
        Assert.True(emitter.BytesWithStatus(FieldStatus.Confirmed) > emitter.BytesWithStatus(FieldStatus.Unverified));
    }

    [Fact]
    public void LayoutDiffJoinsFieldsByName()
    {
        EmitterLayout botw = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0).Emitter;
        EmitterLayout totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1).Emitter;
        LayoutDiff diff = LayoutDiff.Compare(botw, totk);

        // A shared name with a different offset is exactly what a converter has to translate.
        FieldChange emitRate = Assert.Single(diff.Shared, c => c.Name == "emit_rate");
        Assert.True(emitRate.OffsetChanged);
        Assert.Equal(0x800, emitRate.From.Offset);
        Assert.Equal(0xD58, emitRate.To.Offset);

        Assert.NotEmpty(diff.OnlyInTarget);
        Assert.Contains(diff.OnlyInTarget, f => f.Name == "tex_slot5_guid");
        Assert.DoesNotContain(diff.OnlyInSource, f => f.Name == "tex_slot0_guid");
    }

    [Fact]
    public void KeyframeChunkRepeatingGroupIsConsistent()
    {
        ChunkLayout chunk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1).Chunks["EAC0"];
        Assert.NotNull(chunk.Repeating);
        Assert.Equal(0x2C, chunk.Repeating.PayloadSize(2)); // matches the shipped 2-key chunk (node size 0x4C = 0x20 + 0x2C)
    }

    [Fact]
    public void ViewReadsAndWritesOnlyTheNamedField()
    {
        StructLayout layout = new("Test", 0x20,
        [
            new FieldDef("name", 0x00, FieldType.String, 8, FieldStatus.Confirmed, ""),
            new FieldDef("count", 0x08, FieldType.U16, 1, FieldStatus.Confirmed, ""),
            new FieldDef("vec", 0x0C, FieldType.F32, 3, FieldStatus.Confirmed, ""),
            new FieldDef("id", 0x18, FieldType.U64, 1, FieldStatus.Unverified, "")
        ]);
        byte[] data = new byte[0x20];
        Array.Fill(data, (byte)0xAB);
        byte[] before = (byte[])data.Clone();

        var view = new StructView(layout, data);
        view.SetString("name", "hi");
        view.SetUInt16("count", 0x1234);
        view.SetSingle("vec", 2.5f, 1);
        view.SetUInt64("id", 0x0123456789ABCDEF);

        Assert.Equal("hi", view.GetString("name"));
        Assert.Equal(0x1234, view.GetUInt16("count"));
        Assert.Equal(2.5f, view.GetSingle("vec", 1));
        Assert.Equal(0xABABABABu, BitConverter.ToUInt32(data, 0x0C));      // vec[0] untouched
        Assert.Equal(0xABu, data[0x0A]);                                    // gap between count and vec untouched
        Assert.Equal(0xABu, data[0x16]);                                    // gap before id untouched
        Assert.Equal(2.5f, BitConverter.ToSingle(data, 0x10));              // vec[1] was written
        Assert.Equal(before[0x14..0x18], data[0x14..0x18]);                 // vec[2] untouched
        Assert.Throws<InvalidOperationException>(() => new StructView(layout, data).GetUInt32("count")); // wrong type is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new StructView(layout, data).GetSingle("vec", 3));
        Assert.Throws<ArgumentException>(() => new StructView(layout, data).SetString("name", "far too long"));
    }

    [Fact]
    public void StructLayoutRejectsOverlapsAndOutOfRangeFields()
    {
        Assert.Throws<ArgumentException>(() => new StructLayout("Bad", 8,
        [
            new FieldDef("a", 0, FieldType.U32, 1, FieldStatus.Confirmed, ""),
            new FieldDef("b", 2, FieldType.U32, 1, FieldStatus.Confirmed, "")
        ]));
        Assert.Throws<ArgumentException>(() => new StructLayout("Bad", 4,
            [new FieldDef("a", 2, FieldType.U32, 1, FieldStatus.Confirmed, "")]));
        Assert.Throws<ArgumentException>(() => new StructLayout("Bad", 8,
        [
            new FieldDef("a", 0, FieldType.U8, 1, FieldStatus.Confirmed, ""),
            new FieldDef("a", 4, FieldType.U8, 1, FieldStatus.Confirmed, "")
        ]));
    }
}
