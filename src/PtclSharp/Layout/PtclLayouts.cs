namespace PtclSharp.Layout;

/// <summary>Entry point for version-specific layouts. Add a new game/runtime by adding a case here.</summary>
public static class PtclLayouts
{
    private static readonly Lazy<PtclLayoutSet> BotW = new(BotwLayouts.Create);
    private static readonly Lazy<PtclLayoutSet> TotK = new(TotkLayouts.Create);

    public static PtclLayoutSet For(PtclVersion version) => version switch
    {
        PtclVersion.BotW_NintendoWareVfx_4_4_0 => BotW.Value,
        PtclVersion.TotK_NintendoWareVfx2_15_3_1 => TotK.Value,
        _ => throw new PtclVersionException($"No layout is registered for {version}.")
    };

    /// <summary>Every registered layout set.</summary>
    public static IReadOnlyList<PtclLayoutSet> All => [BotW.Value, TotK.Value];
}

/// <summary>How a field differs between two layouts of the same structure.</summary>
public sealed record FieldChange(string Name, FieldDef From, FieldDef To)
{
    public bool OffsetChanged => From.Offset != To.Offset;
    public bool TypeChanged => From.Type != To.Type || From.Count != To.Count;
    public bool StatusChanged => From.Status != To.Status;
}

/// <summary>
/// Name-based comparison of two layouts of one structure (for example BotW and TotK EMTR). Fields are joined by
/// <see cref="FieldDef.Name"/>; a field present on only one side cannot be represented on the other side, and
/// a conversion has to drop it or invent a default (and say so in its report).
/// </summary>
public sealed class LayoutDiff
{
    private LayoutDiff(IReadOnlyList<FieldDef> onlyInSource, IReadOnlyList<FieldDef> onlyInTarget, IReadOnlyList<FieldChange> shared)
    {
        OnlyInSource = onlyInSource;
        OnlyInTarget = onlyInTarget;
        Shared = shared;
    }

    public IReadOnlyList<FieldDef> OnlyInSource { get; }
    public IReadOnlyList<FieldDef> OnlyInTarget { get; }

    /// <summary>Fields with the same name on both sides (each entry says what differs).</summary>
    public IReadOnlyList<FieldChange> Shared { get; }

    public static LayoutDiff Compare(StructLayout source, StructLayout target)
    {
        var shared = new List<FieldChange>();
        var onlySource = new List<FieldDef>();
        foreach (FieldDef f in source.Fields)
        {
            if (target.TryGetField(f.Name, out FieldDef other)) shared.Add(new FieldChange(f.Name, f, other));
            else onlySource.Add(f);
        }
        List<FieldDef> onlyTarget = target.Fields.Where(f => !source.Contains(f.Name)).ToList();
        return new LayoutDiff(onlySource, onlyTarget, shared);
    }
}
