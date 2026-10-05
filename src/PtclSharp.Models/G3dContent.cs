using BfresLibrary;

namespace PtclSharp.Models;

/// <summary>One <c>G3NT</c> entry: the id emitters use (<c>g3d_primitive_idx</c>) and the entry's 0x18 raw bytes, which also say which vertex attribute is which.</summary>
/// <param name="Index">Position in the table, which is also the position of the model in the FRES file.</param>
public sealed record G3dEntry(uint Id, int Index, byte[] Raw);

/// <summary>
/// The G3D side of an effect file: the <c>G3PR</c> node holds a complete BFRES (<c>FRES</c>) file, and its <c>G3NT</c> child has one 0x18 byte
/// entry per model, in model order. An emitter picks a model by the id at the start of an entry (the engine's lookup is
/// <c>nn::vfx::Resource::GetG3dPrimitive</c>, 0x7100ae49c8 in BotW, which walks the entries and falls back to the resident resource).
/// </summary>
public sealed class G3dContent
{
    private readonly byte[] _resource;

    private G3dContent(byte[] resource, IReadOnlyList<G3dEntry> entries)
    {
        _resource = resource;
        Entries = entries;
    }

    public IReadOnlyList<G3dEntry> Entries { get; }

    public int ModelCount => Entries.Count;

    /// <summary>The raw FRES bytes.</summary>
    public ReadOnlySpan<byte> Resource => _resource;

    public G3dEntry? Find(uint id) => Entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Parses the FRES file anew; the caller owns the result and may modify it.</summary>
    public ResFile Open() => new(new MemoryStream(_resource));

    /// <summary>Reads the G3D data of a file; null when it has none (many effects need no model).</summary>
    public static G3dContent? Read(PtclFile file) => Read(file.Vfxb);

    public static G3dContent? Read(VfxbFile vfxb)
    {
        VfxbNode? g3pr = vfxb.Roots.FirstOrDefault(r => r.Kind == "G3PR");
        if (g3pr?.DataOffset is not int data || g3pr.Size == 0) return null;
        VfxbNode? g3nt = g3pr.Children.FirstOrDefault(c => c.Kind == "G3NT");
        if (g3nt?.DataOffset is not int table) return null;

        byte[] bytes = vfxb.Data;
        var entries = new List<G3dEntry>();
        for (int i = 0; i < (int)g3nt.Size / 0x18; i++)
        {
            byte[] raw = bytes.AsSpan(table + (i * 0x18), 0x18).ToArray();
            entries.Add(new G3dEntry(BitConverter.ToUInt32(raw, 0), i, raw));
        }
        return new G3dContent(bytes.AsSpan(data, (int)g3pr.Size).ToArray(), entries);
    }
}
