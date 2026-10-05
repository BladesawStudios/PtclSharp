namespace PtclSharp.Textures;

public enum TextureDisposition
{
    /// <summary>TotK lists this GUID: its own copy is used.</summary>
    ReuseTotk,

    /// <summary>TotK has a texture of this name under another GUID: the emitter is pointed at that one.</summary>
    ReuseTotkByName,

    /// <summary>TotK does not have it: the BNTX texture is converted to a new <c>.txtg</c> and listed under its BotW GUID.</summary>
    Import,

    /// <summary>The GUID is in no table, or the pixels cannot be carried (damaged archive, unsupported format).</summary>
    Unresolved
}

/// <param name="BotwGuid">The GUID the BotW emitter uses.</param>
/// <param name="Name">The texture name, or null when no table names the GUID.</param>
/// <param name="TotkGuid">The GUID the converted emitter must use (null when unresolved).</param>
public sealed record TexturePlanItem(ulong BotwGuid, string? Name, TextureDisposition Disposition, uint? TotkGuid, BotwTexture? Source, string? Problem = null);

/// <summary>Everything the textures of one BotW effect file need in TotK.</summary>
public sealed class TexturePlan
{
    public required IReadOnlyList<TexturePlanItem> Items { get; init; }

    /// <summary>Use as <c>ConverterOptions.TextureMap</c>.</summary>
    public ulong? Map(ulong botwGuid) => Items.FirstOrDefault(i => i.BotwGuid == botwGuid)?.TotkGuid;

    /// <summary>The <c>Textures</c> entries a TotK file needs for the planned textures (reused and imported).</summary>
    public IEnumerable<PtclTextureReference> TextureReferences =>
        Items.Where(i => i is { TotkGuid: not null, Name: not null }).DistinctBy(i => i.TotkGuid)
            .Select(i => new PtclTextureReference(i.Name!, i.TotkGuid!.Value));

    public IEnumerable<TexturePlanItem> Imports => Items.Where(i => i.Disposition == TextureDisposition.Import);
    public IEnumerable<TexturePlanItem> Unresolved => Items.Where(i => i.Disposition == TextureDisposition.Unresolved);
}

public static class TexturePlanner
{
    /// <summary>Plans every texture GUID the emitters of <paramref name="botw"/> use.</summary>
    public static TexturePlan Plan(PtclFile botw, BotwTextureCatalog catalog, TotkTextureIndex totk)
    {
        var items = new List<TexturePlanItem>();
        foreach (ulong guid in botw.Vfxb.EmitterSets.SelectMany(s => s.Emitters).SelectMany(e => e.TextureSamplerGuids)
                     .OfType<ulong>().Where(g => g != ulong.MaxValue).Distinct().Order())
            items.Add(PlanOne(guid, catalog, totk));
        return new TexturePlan { Items = items };
    }

    public static TexturePlanItem PlanOne(ulong guid, BotwTextureCatalog catalog, TotkTextureIndex totk)
    {
        BotwTexture? source = catalog.Find(guid);
        if (guid <= uint.MaxValue && totk.TryGetName((uint)guid, out string totkName))
            return new TexturePlanItem(guid, totkName, TextureDisposition.ReuseTotk, (uint)guid, source);
        if (source is null)
            return new TexturePlanItem(guid, null, TextureDisposition.Unresolved, null, null, "No GTNT table (own or resident) names this GUID.");
        if (totk.TryGetGuid(source.Name, out uint other))
            return new TexturePlanItem(guid, source.Name, TextureDisposition.ReuseTotkByName, other, source);
        if (source.Pixels is null)
            return new TexturePlanItem(guid, source.Name, TextureDisposition.Unresolved, null, source, "Named by a GTNT table but absent from the BNTX archive.");
        if (guid > uint.MaxValue)
            return new TexturePlanItem(guid, source.Name, TextureDisposition.Unresolved, null, source, "GUID does not fit TotK's 32-bit texture GUID.");
        if (!BntxToTxtg.TryMapFormat(source.Pixels.Format, out _, out _))
            return new TexturePlanItem(guid, source.Name, TextureDisposition.Unresolved, null, source, $"BNTX format {source.Pixels.Format} has no TexToGo counterpart.");
        return new TexturePlanItem(guid, source.Name, TextureDisposition.Import, (uint)guid, source);
    }
}
