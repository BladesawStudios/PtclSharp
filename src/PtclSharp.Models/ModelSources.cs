using BfresLibrary;

namespace PtclSharp.Models;

/// <summary>Where TotK keeps a model: the effect file and the position in its G3NT table.</summary>
public sealed record TotkModelLocation(string File, int Index);

/// <summary>
/// Which models TotK's effect files carry, by id. Every TotK file embeds all the models its emitters use (none resolve through a resident
/// resource), and the same id in two games is the same model (a BotW model whose id TotK also lists is the very asset, byte for byte:
/// both models the games share by name have identical vertex and index data).
/// </summary>
public sealed class TotkModelIndex
{
    private readonly Dictionary<uint, TotkModelLocation> _byId = [];
    private readonly Dictionary<ulong, TotkModelLocation> _primitives = [];

    public TotkModelIndex(string effectDirectory, byte[] dictionary)
    {
        EffectDirectory = effectDirectory;
        Dictionary = dictionary;
    }

    public string EffectDirectory { get; }
    public byte[] Dictionary { get; }
    public int Count => _byId.Count;

    /// <summary>A TotK file with a model whose material is the empty "dummy" material TotK gives effect models (see <see cref="BotwModelConverter"/>).</summary>
    public string? TemplateFile { get; private set; }

    public bool TryFind(uint id, out TotkModelLocation location) => _byId.TryGetValue(id, out location!);

    public void Add(uint id, string file, int index) => _byId.TryAdd(id, new TotkModelLocation(file, index));

    public bool TryFindPrimitive(ulong id, out TotkModelLocation location) => _primitives.TryGetValue(id, out location!);

    public void AddPrimitive(ulong id, string file, int index) => _primitives.TryAdd(id, new TotkModelLocation(file, index));

    public static TotkModelIndex Build(string effectDirectory, byte[] dictionary)
    {
        var index = new TotkModelIndex(effectDirectory, dictionary);
        foreach (string path in Directory.EnumerateFiles(effectDirectory, "*.esetb.byml.zs").Order(StringComparer.Ordinal))
        {
            string name = FileName(path);
            PtclFile file = PtclFile.ReadEsetb(File.ReadAllBytes(path), dictionary);
            PrimitiveContent primitives = PrimitiveContent.Read(file);
            for (int i = 0; i < primitives.Primitives.Count; i++) index.AddPrimitive(primitives.Primitives[i].Id, name, i);
            G3dContent? content = G3dContent.Read(file);
            if (content is null) continue;
            foreach (G3dEntry entry in content.Entries) index.Add(entry.Id, name, entry.Index);
            index.TemplateFile ??= HasDummyMaterial(content) ? name : null;
        }
        return index;
    }

    /// <summary>Reads an index saved with <see cref="Save"/>.</summary>
    public static TotkModelIndex Load(string cachePath, string effectDirectory, byte[] dictionary)
    {
        var index = new TotkModelIndex(effectDirectory, dictionary);
        foreach (string line in File.ReadLines(cachePath))
        {
            string[] parts = line.Split('\t');
            if (parts[0] == "#template") index.TemplateFile = parts[1];
            else if (parts.Length == 4 && parts[0] == "P") index.AddPrimitive(Convert.ToUInt64(parts[1], 16), parts[2], int.Parse(parts[3]));
            else if (parts.Length == 3) index.Add(Convert.ToUInt32(parts[0], 16), parts[1], int.Parse(parts[2]));
        }
        return index;
    }

    public void Save(string cachePath)
    {
        var lines = new List<string>();
        if (TemplateFile is not null) lines.Add($"#template\t{TemplateFile}");
        lines.AddRange(_byId.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key:X8}\t{kv.Value.File}\t{kv.Value.Index}"));
        lines.AddRange(_primitives.OrderBy(kv => kv.Key).Select(kv => $"P\t{kv.Key:X16}\t{kv.Value.File}\t{kv.Value.Index}"));
        File.WriteAllLines(cachePath, lines);
    }

    /// <summary>Loads a TotK effect file's mesh primitives.</summary>
    public PrimitiveContent OpenPrimitives(string file) =>
        PrimitiveContent.Read(PtclFile.ReadEsetb(File.ReadAllBytes(Path.Combine(EffectDirectory, file + ".Nin_NX_NVN.esetb.byml.zs")), Dictionary));

    /// <summary>Loads a TotK effect file's G3D content.</summary>
    public G3dContent OpenContent(string file) =>
        G3dContent.Read(PtclFile.ReadEsetb(File.ReadAllBytes(Path.Combine(EffectDirectory, file + ".Nin_NX_NVN.esetb.byml.zs")), Dictionary))
        ?? throw new InvalidDataException($"{file} has no G3D data.");

    private static string FileName(string path)
    {
        string name = Path.GetFileName(path);
        return name[..^".Nin_NX_NVN.esetb.byml.zs".Length];
    }

    internal static bool HasDummyMaterial(G3dContent content)
    {
        ResFile resource = content.Open();
        return resource.Models.Values.Any(IsDummyMaterial);
    }

    public static bool IsDummyMaterial(Model model) =>
        model.Materials.Count == 1 && model.Materials[0] is { ShaderAssign: { ShaderArchiveName: "" or null }, ShaderInfoV10: null, ShaderParams.Count: 0 };
}

/// <summary>A BotW model: where it was found and whether it came from the resident resource.</summary>
public sealed record BotwModelLocation(G3dContent Content, G3dEntry Entry, bool Resident);

/// <summary>
/// Resolves a BotW emitter's model id the way the engine does: the file's own <c>G3NT</c> table first, then the resident resource
/// (<c>Effect/GameResident.sesetlist</c> inside <c>Pack/Bootup.pack</c>).
/// </summary>
public sealed class BotwModelCatalog
{
    private readonly G3dContent? _own;
    private readonly G3dContent? _resident;
    private readonly PrimitiveContent _ownPrimitives;
    private readonly PrimitiveContent? _residentPrimitives;

    public BotwModelCatalog(PtclFile file, PtclFile? resident)
    {
        _own = G3dContent.Read(file);
        _resident = resident is null ? null : G3dContent.Read(resident);
        _ownPrimitives = PrimitiveContent.Read(file);
        _residentPrimitives = resident is null ? null : PrimitiveContent.Read(resident);
    }

    /// <summary>The mesh primitive with this unique id: the file's own <c>PRMA</c> first, then the resident one.</summary>
    public (ulong Id, byte[] Node)? FindPrimitive(ulong id) => _ownPrimitives.Find(id) ?? _residentPrimitives?.Find(id);

    public BotwModelLocation? Find(uint id)
    {
        if (_own?.Find(id) is { } own) return new BotwModelLocation(_own, own, false);
        if (_resident?.Find(id) is { } shared) return new BotwModelLocation(_resident, shared, true);
        return null;
    }
}

/// <summary>The <c>PRMA</c> side of an effect file: mesh primitives (<c>PRIM</c> nodes) an emitter picks by <c>mesh_primitive_idx</c>, which is the node's unique id.</summary>
public sealed class PrimitiveContent
{
    private PrimitiveContent(IReadOnlyList<(ulong Id, byte[] Node)> primitives) => Primitives = primitives;

    /// <summary>Complete <c>PRIM</c> nodes (header and data); the layout is the same in both games.</summary>
    public IReadOnlyList<(ulong Id, byte[] Node)> Primitives { get; }

    public static PrimitiveContent Read(PtclFile file) => Read(file.Vfxb);

    public static PrimitiveContent Read(VfxbFile vfxb)
    {
        VfxbNode? prma = vfxb.Roots.FirstOrDefault(r => r.Kind == "PRMA");
        byte[] data = vfxb.Data;
        var list = new List<(ulong, byte[])>();
        if (prma is not null)
            foreach (VfxbNode prim in prma.Children.Where(c => c.Kind == "PRIM" && c.DataOffset is not null))
                list.Add((BitConverter.ToUInt64(data, prim.DataOffset!.Value), data.AsSpan(prim.Offset, (int)prim.Size + 0x20).ToArray()));
        return new PrimitiveContent(list);
    }

    public (ulong Id, byte[] Node)? Find(ulong id) => Primitives.Any(p => p.Id == id) ? Primitives.First(p => p.Id == id) : null;
}
