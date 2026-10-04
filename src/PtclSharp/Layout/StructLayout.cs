using System.Collections.Frozen;

namespace PtclSharp.Layout;

/// <summary>
/// A validated, immutable description of a fixed-size binary structure: its fields in offset order, with
/// no overlaps, nothing outside <see cref="Size"/>, and unique names. Bytes not covered by a field are
/// reported by <see cref="Gaps"/> and are always preserved verbatim by views and writers.
/// </summary>
public class StructLayout
{
    private readonly FrozenDictionary<string, FieldDef> _byName;

    public StructLayout(string name, int size, IEnumerable<FieldDef> fields)
    {
        Name = name;
        Size = size;
        Fields = fields.OrderBy(f => f.Offset).ThenBy(f => f.Name, StringComparer.Ordinal).ToArray();
        Validate();
        _byName = Fields.ToFrozenDictionary(f => f.Name, StringComparer.Ordinal);
    }

    public string Name { get; }

    /// <summary>Fixed byte size of the structure (the fixed part, for structures with a repeating tail).</summary>
    public int Size { get; }

    /// <summary>Fields ordered by offset.</summary>
    public IReadOnlyList<FieldDef> Fields { get; }

    public bool Contains(string field) => _byName.ContainsKey(field);

    public bool TryGetField(string field, out FieldDef def)
    {
        if (_byName.TryGetValue(field, out FieldDef? found))
        {
            def = found;
            return true;
        }
        def = null!;
        return false;
    }

    public FieldDef this[string field] => _byName.TryGetValue(field, out FieldDef? def)
        ? def
        : throw new KeyNotFoundException($"{Name} has no field '{field}'.");

    /// <summary>The field covering <paramref name="offset"/>, or null if the byte is in a gap.</summary>
    public FieldDef? FieldAt(int offset)
    {
        int lo = 0, hi = Fields.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            FieldDef f = Fields[mid];
            if (offset < f.Offset) hi = mid - 1;
            else if (offset >= f.End) lo = mid + 1;
            else return f;
        }
        return null;
    }

    /// <summary>Byte ranges no field describes. They are legal; they are simply carried as raw bytes.</summary>
    public IReadOnlyList<(int Offset, int Length)> Gaps
    {
        get
        {
            var gaps = new List<(int, int)>();
            int cursor = 0;
            foreach (FieldDef f in Fields)
            {
                if (f.Offset > cursor) gaps.Add((cursor, f.Offset - cursor));
                cursor = Math.Max(cursor, f.End);
            }
            if (cursor < Size) gaps.Add((cursor, Size - cursor));
            return gaps;
        }
    }

    /// <summary>Number of bytes covered by fields of each status (gaps count as nothing).</summary>
    public int BytesWithStatus(FieldStatus status) => Fields.Where(f => f.Status == status).Sum(f => f.ByteLength);

    private void Validate()
    {
        if (Size < 0) throw new ArgumentException($"{Name}: negative size.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        int cursor = 0;
        foreach (FieldDef f in Fields)
        {
            if (f.Count < 1) throw new ArgumentException($"{Name}.{f.Name}: count must be at least 1.");
            if (f.Offset < 0 || f.End > Size)
                throw new ArgumentException($"{Name}.{f.Name}: 0x{f.Offset:X}..0x{f.End:X} is outside the 0x{Size:X}-byte structure.");
            if (f.Offset < cursor)
                throw new ArgumentException($"{Name}.{f.Name}: overlaps the previous field (ends at 0x{cursor:X}, starts at 0x{f.Offset:X}).");
            if (!names.Add(f.Name)) throw new ArgumentException($"{Name}: duplicate field name '{f.Name}'.");
            cursor = f.End;
        }
    }
}

/// <summary>
/// A chunk whose payload is a fixed header followed by a repeated element (for example a keyframe list).
/// </summary>
/// <param name="FirstOffset">Offset of element 0.</param>
/// <param name="Stride">Bytes per element.</param>
/// <param name="CountField">Name of the header field that holds the element count.</param>
/// <param name="Fields">Element fields; offsets are relative to the element start.</param>
public sealed record RepeatingGroup(string Name, int FirstOffset, int Stride, string CountField, IReadOnlyList<FieldDef> Fields)
{
    public int PayloadSize(int count) => FirstOffset + (Stride * count);
}

/// <summary>A <see cref="StructLayout"/> for a chunk payload that may end with a repeating group.</summary>
public sealed class ChunkLayout : StructLayout
{
    public ChunkLayout(string name, int size, IEnumerable<FieldDef> fields, RepeatingGroup? repeating = null)
        : base(name, size, fields)
    {
        Repeating = repeating;
        if (repeating is not null)
        {
            if (!Contains(repeating.CountField))
                throw new ArgumentException($"{name}: repeating group count field '{repeating.CountField}' does not exist.");
            if (repeating.FirstOffset != size)
                throw new ArgumentException($"{name}: repeating group must start at the end of the fixed part (0x{size:X}).");
            foreach (FieldDef f in repeating.Fields)
                if (f.Offset < 0 || f.End > repeating.Stride)
                    throw new ArgumentException($"{name}.{f.Name}: outside the repeating element.");
        }
    }

    public RepeatingGroup? Repeating { get; }
}
