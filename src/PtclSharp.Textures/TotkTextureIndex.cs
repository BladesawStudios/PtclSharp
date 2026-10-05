namespace PtclSharp.Textures;

/// <summary>
/// Which textures TotK's effect files know: the GUID and name of every <c>Textures</c> entry, and where the <c>.txtg</c> lives
/// (<c>TexToGo/&lt;Name&gt;.txtg</c>). A GUID names the same texture in both games (every one of the 164 shared-pool textures
/// BotW and TotK have in common agrees by name), so a BotW emitter's GUID can be kept whenever TotK lists it.
/// </summary>
public sealed class TotkTextureIndex
{
    private readonly Dictionary<uint, string> _byGuid = [];
    private readonly Dictionary<string, uint> _byName = new(StringComparer.OrdinalIgnoreCase);

    public TotkTextureIndex(string? texToGoDirectory = null) => TexToGoDirectory = texToGoDirectory;

    /// <summary>The <c>TexToGo</c> directory, or null when the files are not available (then only GUIDs are checked).</summary>
    public string? TexToGoDirectory { get; }

    public int Count => _byGuid.Count;

    public void Add(string name, uint guid)
    {
        _byGuid[guid] = name;
        _byName[name] = guid;
    }

    public bool TryGetName(uint guid, out string name) => _byGuid.TryGetValue(guid, out name!);

    public bool TryGetGuid(string name, out uint guid) => _byName.TryGetValue(name, out guid);

    /// <summary>The path of a texture's <c>.txtg</c> (which may not exist), or null without a <c>TexToGo</c> directory.</summary>
    public string? TxtgPath(string name) => TexToGoDirectory is null ? null : Path.Combine(TexToGoDirectory, name + ".txtg");

    public bool HasFile(string name) => TxtgPath(name) is { } p && File.Exists(p);

    /// <summary>Collects the <c>Textures</c> lists of every TotK effect file in <paramref name="effectDirectory"/>.</summary>
    public static TotkTextureIndex Build(string effectDirectory, byte[] dictionary, string? texToGoDirectory)
    {
        var index = new TotkTextureIndex(texToGoDirectory);
        foreach (string path in Directory.EnumerateFiles(effectDirectory, "*.esetb.byml.zs"))
            foreach (PtclTextureReference t in PtclFile.ReadEsetb(File.ReadAllBytes(path), dictionary).Textures)
                index.Add(t.Name, t.Guid);
        return index;
    }

    /// <summary>Reads an index saved with <see cref="Save"/> (tab separated guid and name), which is much quicker than <see cref="Build"/>.</summary>
    public static TotkTextureIndex Load(string cachePath, string? texToGoDirectory)
    {
        var index = new TotkTextureIndex(texToGoDirectory);
        foreach (string line in File.ReadLines(cachePath))
        {
            int tab = line.IndexOf('\t');
            if (tab > 0) index.Add(line[(tab + 1)..], Convert.ToUInt32(line[..tab], 16));
        }
        return index;
    }

    public void Save(string cachePath) =>
        File.WriteAllLines(cachePath, _byGuid.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key:X8}\t{kv.Value}"));
}
