using BymlSharp;

namespace TotkModKit;

/// <summary>
/// An actor pack held in memory for editing. Files can be renamed (every reference to them inside the pack follows) and BYML files
/// edited; <see cref="ToSarc"/> serializes it. BymlSharp rewrites every <c>.bgyml</c> in the shipped packs byte for byte, so only
/// what is edited changes.
/// </summary>
public sealed class PackEdit
{
    private readonly List<(string Name, byte[] Data)> _files;

    public PackEdit(byte[] sarc) => _files = Sarc.Read(sarc);

    public IEnumerable<string> Names => _files.Select(f => f.Name);

    public bool Contains(string name) => _files.Any(f => f.Name == name);

    public byte[] Get(string name) => _files.First(f => f.Name == name).Data;

    /// <summary>Finds the single file whose name starts with <paramref name="prefix"/> (for example a folder and the start of a file name).</summary>
    public string Find(string prefix) => _files.Select(f => f.Name).Single(n => n.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Renames a file and rewrites every string in the pack that refers to it. References come in two spellings: <c>?path.bgyml</c>
    /// (a file inside the pack) and <c>Work/path.gyml</c> (the source-tree spelling the engine resolves to the same file).
    /// </summary>
    public void Rename(string oldName, string newName)
    {
        int index = _files.FindIndex(f => f.Name == oldName);
        if (index < 0) throw new KeyNotFoundException($"{oldName} is not in the pack.");
        if (Contains(newName)) throw new InvalidOperationException($"{newName} already exists in the pack.");
        _files[index] = (newName, _files[index].Data);

        string SourceSpelling(string name) => "Work/" + (name.EndsWith(".bgyml", StringComparison.Ordinal) ? name[..^".bgyml".Length] + ".gyml" : name);
        var map = new Dictionary<string, string>
        {
            ["?" + oldName] = "?" + newName,
            [SourceSpelling(oldName)] = SourceSpelling(newName)
        };
        RewriteStrings(s => map.GetValueOrDefault(s));
    }

    /// <summary>Replaces every string value of every BYML in the pack for which <paramref name="map"/> returns a replacement.</summary>
    public void RewriteStrings(Func<string, string?> map)
    {
        for (int i = 0; i < _files.Count; i++)
        {
            if (!_files[i].Name.EndsWith(".bgyml", StringComparison.Ordinal)) continue;
            BymlFile file = BymlFile.FromBinary(_files[i].Data);
            if (!Walk(file.Root, map)) continue;
            _files[i] = (_files[i].Name, file.Write());
        }
    }

    private static bool Walk(Byml node, Func<string, string?> map)
    {
        bool changed = false;
        if (node.IsMap)
        {
            foreach (string key in node.AsMap.Keys.ToList())
            {
                Byml child = node.AsMap[key];
                if (child.Type == BymlType.String && map(child.AsString()) is { } replaced) { node.AsMap[key] = Byml.From(replaced); changed = true; }
                else if (child.IsContainer) changed |= Walk(child, map);
            }
        }
        else if (node.IsArray)
        {
            IList<Byml> list = node.AsArray;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type == BymlType.String && map(list[i].AsString()) is { } replaced) { list[i] = Byml.From(replaced); changed = true; }
                else if (list[i].IsContainer) changed |= Walk(list[i], map);
            }
        }
        else if (node.Type == BymlType.HashMap64)
        {
            foreach (ulong key in node.AsHashMap.Keys.ToList())
            {
                Byml child = node.AsHashMap[key];
                if (child.Type == BymlType.String && map(child.AsString()) is { } replaced) { node.AsHashMap[key] = Byml.From(replaced); changed = true; }
                else if (child.IsContainer) changed |= Walk(child, map);
            }
        }
        return changed;
    }

    /// <summary>Parses a BYML file of the pack, lets <paramref name="edit"/> change it, and stores it back (same BYML version).</summary>
    public void Edit(string name, Action<Byml> edit)
    {
        int index = _files.FindIndex(f => f.Name == name);
        if (index < 0) throw new KeyNotFoundException($"{name} is not in the pack.");
        BymlFile file = BymlFile.FromBinary(_files[index].Data);
        edit(file.Root);
        _files[index] = (name, file.Write());
    }

    /// <summary>Adds a file that is not in the pack yet.</summary>
    public void Add(string name, byte[] data)
    {
        if (Contains(name)) throw new InvalidOperationException($"{name} already exists in the pack.");
        _files.Add((name, data));
    }

    public byte[] ToSarc() => Sarc.Write(_files);
}

/// <summary>Helpers for editing parsed BYML values without changing their number types.</summary>
public static class BymlEdit
{
    public static void SetNumber(Byml map, string key, double value)
    {
        Byml existing = map.AsMap[key];
        map.AsMap[key] = existing.Type switch
        {
            BymlType.Float => Byml.From((float)value),
            BymlType.Double => Byml.From(value),
            BymlType.UInt => Byml.From((uint)value),
            BymlType.Int64 => Byml.From((long)value),
            BymlType.UInt64 => Byml.From((ulong)value),
            _ => Byml.From((int)value)
        };
    }

    public static void SetBool(Byml map, string key, bool value) => map.AsMap[key] = Byml.From(value);

    public static void SetString(Byml map, string key, string value) => map.AsMap[key] = Byml.From(value);
}
