using System.Buffers.Binary;
using System.Text;

namespace PtclSharp;

/// <summary>One GTNT entry: the GUID an emitter uses and the name of the texture inside the BNTX archive.</summary>
public sealed record BotwTextureName(ulong Guid, string Name);

/// <summary>
/// The texture side of a BotW effect file: the <c>GRTF</c> node holds a BNTX archive and its <c>GTNT</c> child is the table
/// that says which GUID names which texture. The engine looks an emitter's GUID up in the file's own table first and then in
/// the resident resource (<c>Effect/GameResident.sesetlist</c> inside <c>Bootup.pack</c>), which holds the shared textures
/// (the lookup is at 0x7100ae42a8, called from <c>nn::vfx::Resource::InitializeEmitterGraphicsResource</c>).
/// </summary>
public sealed class BotwTextureTable
{
    private BotwTextureTable(IReadOnlyList<BotwTextureName> names, ReadOnlyMemory<byte> archive)
    {
        Names = names;
        Archive = archive;
    }

    public IReadOnlyList<BotwTextureName> Names { get; }

    /// <summary>The embedded BNTX archive (it runs to the end of the payload; the BNTX header knows its real size), or empty when the file has none.</summary>
    public ReadOnlyMemory<byte> Archive { get; }

    /// <summary>Reads the table of a BotW file; an empty table when the file has no <c>GRTF</c> node.</summary>
    public static BotwTextureTable Read(VfxbFile vfxb)
    {
        VfxbNode? grtf = vfxb.Roots.FirstOrDefault(r => r.Kind == "GRTF");
        if (grtf is null) return new BotwTextureTable([], ReadOnlyMemory<byte>.Empty);

        byte[] data = vfxb.Data;
        var names = new List<BotwTextureName>();
        foreach (VfxbNode gtnt in grtf.Children.Concat(grtf.Attributes).Where(n => n.Kind == "GTNT"))
        {
            if (gtnt.DataOffset is not int start) continue;
            // The node's size counts only its data (the entries), not the 0x20 byte header.
            int end = Math.Min(data.Length, checked(start + (int)gtnt.Size));
            // Entry: u64 guid, u32 entry size, u32 name length (counting the terminator), then the name padded to 8 bytes.
            for (int pos = start; pos + 16 <= end;)
            {
                ulong guid = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 8));
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 12));
                if (length == 0 || pos + 16 + length > end) break;
                names.Add(new BotwTextureName(guid, Encoding.ASCII.GetString(data, pos + 16, (int)length).TrimEnd('\0')));
                pos += size != 0 ? (int)size : 16 + (int)((length + 8) & ~7u);
            }
        }

        ReadOnlyMemory<byte> archive = ReadOnlyMemory<byte>.Empty;
        if (grtf.DataOffset is int bntx && bntx + 4 <= data.Length && data.AsSpan(bntx, 4).SequenceEqual("BNTX"u8))
            archive = data.AsMemory(bntx);
        return new BotwTextureTable(names, archive);
    }
}
