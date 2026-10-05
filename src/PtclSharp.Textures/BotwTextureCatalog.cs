using BntxSharp;

namespace PtclSharp.Textures;

/// <summary>A BotW texture: the GUID emitters use, its name and the decoded BNTX texture holding the pixels.</summary>
public sealed record BotwTexture(ulong Guid, string Name, BntxTexture? Pixels);

/// <summary>
/// Resolves a BotW emitter's texture GUID the way the engine does: the file's own <c>GTNT</c> table first, then the resident
/// resource (<c>Effect/GameResident.sesetlist</c> in <c>Bootup.pack</c>).
/// </summary>
public sealed class BotwTextureCatalog
{
    private readonly Dictionary<ulong, BotwTexture> _own = [];
    private readonly BotwTextureCatalog? _resident;

    private BotwTextureCatalog(BotwTextureTable table, BotwTextureCatalog? resident)
    {
        _resident = resident;
        var pixels = new Dictionary<string, BntxTexture>(StringComparer.OrdinalIgnoreCase);
        if (!table.Archive.IsEmpty)
        {
            try
            {
                foreach (BntxTexture t in BntxFile.Load(table.Archive.Span).Textures)
                    pixels[t.Name] = t;
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException)
            {
                // A damaged archive leaves the names without pixels; the planner reports them as unavailable.
            }
        }
        foreach (BotwTextureName n in table.Names)
            _own[n.Guid] = new BotwTexture(n.Guid, n.Name, pixels.GetValueOrDefault(n.Name));
    }

    public int Count => _own.Count;

    public IEnumerable<BotwTexture> Textures => _own.Values;

    /// <summary>Builds the catalog of a resident file (<c>GameResident.sesetlist</c>), which has no resident of its own.</summary>
    public static BotwTextureCatalog ForResident(PtclFile resident) => new(BotwTextureTable.Read(resident.Vfxb), null);

    /// <summary>Builds the catalog of an effect file, falling back to <paramref name="resident"/> for GUIDs it does not define.</summary>
    public static BotwTextureCatalog For(PtclFile file, BotwTextureCatalog? resident) => new(BotwTextureTable.Read(file.Vfxb), resident);

    public BotwTexture? Find(ulong guid) =>
        _own.TryGetValue(guid, out BotwTexture? t) ? t : _resident?.Find(guid);
}
