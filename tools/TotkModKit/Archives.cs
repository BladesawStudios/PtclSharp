using System.Buffers.Binary;
using System.Text;
using ZstdSharp;

namespace TotkModKit;

/// <summary>The three Zstandard dictionaries in <c>Pack/ZsDic.pack.zs</c>: 1 for general files, 2 for BCETT BYML, 3 for packs.</summary>
public sealed class ZsDictionaries
{
    private readonly Dictionary<uint, byte[]> _byId = [];

    public static ZsDictionaries Load(string romfsRoot)
    {
        var result = new ZsDictionaries();
        byte[] sarc = DecompressPlain(File.ReadAllBytes(Path.Combine(romfsRoot, "Pack", "ZsDic.pack.zs")));
        foreach ((string _, byte[] data) in Sarc.Read(sarc))
            result._byId[BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4))] = data;
        return result;
    }

    private static byte[] DecompressPlain(byte[] data)
    {
        using var d = new Decompressor();
        return d.Unwrap(data).ToArray();
    }

    public byte[] Get(uint id) => _byId[id];

    /// <summary>Decompresses a <c>.zs</c> file, finding the dictionary it was made with; returns that dictionary's id.</summary>
    public byte[] Decompress(byte[] zs, out uint dictionaryId)
    {
        foreach ((uint id, byte[] dict) in _byId.OrderBy(kv => kv.Key))
        {
            try
            {
                using var d = new Decompressor();
                d.LoadDictionary(dict);
                byte[] result = d.Unwrap(zs).ToArray();
                dictionaryId = id;
                return result;
            }
            catch (ZstdException) { }
        }
        throw new InvalidDataException("No dictionary in ZsDic.pack decompresses this file.");
    }

    public byte[] Compress(byte[] data, uint dictionaryId, int level = 16)
    {
        using var c = new Compressor(level);
        c.LoadDictionary(_byId[dictionaryId]);
        return c.Wrap(data).ToArray();
    }
}

/// <summary>Minimal SARC reader and writer (little endian), enough for actor packs.</summary>
public static class Sarc
{
    public static List<(string Name, byte[] Data)> Read(byte[] d)
    {
        if (!d.AsSpan(0, 4).SequenceEqual("SARC"u8)) throw new InvalidDataException("Not a SARC archive.");
        int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(4));
        int dataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(0xC));
        int sfat = headerSize;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(sfat + 6));
        int nameBase = sfat + 12 + (count * 16) + 8;
        var files = new List<(string, byte[])>();
        for (int i = 0; i < count; i++)
        {
            int node = sfat + 12 + (i * 16);
            uint nameWord = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(node + 4));
            int nameOffset = nameBase + ((int)(nameWord & 0xFFFFFF) * 4);
            string name = Encoding.UTF8.GetString(d, nameOffset, Array.IndexOf(d, (byte)0, nameOffset) - nameOffset);
            int start = dataOffset + (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(node + 8));
            int end = dataOffset + (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(node + 12));
            files.Add((name, d.AsSpan(start, end - start).ToArray()));
        }
        return files;
    }

    public static uint Hash(string name)
    {
        uint h = 0;
        foreach (byte b in Encoding.UTF8.GetBytes(name)) h = unchecked((h * 0x65) + (uint)(sbyte)b);
        return h;
    }

    public static byte[] Write(IEnumerable<(string Name, byte[] Data)> files, int alignment = 8)
    {
        var sorted = files.OrderBy(f => Hash(f.Name)).ToList();
        int count = sorted.Count;

        var names = new MemoryStream();
        var nameOffsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            nameOffsets[i] = (int)names.Position / 4;
            names.Write(Encoding.UTF8.GetBytes(sorted[i].Name));
            names.WriteByte(0);
            while (names.Position % 4 != 0) names.WriteByte(0);
        }

        int sfatSize = 12 + (count * 16);
        int sfntSize = 8 + (int)names.Length;
        int dataOffset = Align(0x14 + sfatSize + sfntSize, alignment);

        var data = new MemoryStream();
        var ranges = new (int Start, int End)[count];
        for (int i = 0; i < count; i++)
        {
            while (data.Position % alignment != 0) data.WriteByte(0);
            ranges[i].Start = (int)data.Position;
            data.Write(sorted[i].Data);
            ranges[i].End = (int)data.Position;
        }

        using var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("SARC"u8);
        w.Write((ushort)0x14);
        w.Write((ushort)0xFEFF);
        w.Write((uint)(dataOffset + data.Length));
        w.Write((uint)dataOffset);
        w.Write((uint)0x100);
        w.Write("SFAT"u8);
        w.Write((ushort)0xC);
        w.Write((ushort)count);
        w.Write((uint)0x65);
        for (int i = 0; i < count; i++)
        {
            w.Write(Hash(sorted[i].Name));
            w.Write((uint)(0x01000000 | (uint)nameOffsets[i]));
            w.Write((uint)ranges[i].Start);
            w.Write((uint)ranges[i].End);
        }
        w.Write("SFNT"u8);
        w.Write((ushort)8);
        w.Write((ushort)0);
        w.Write(names.ToArray());
        while (ms.Position < dataOffset) ms.WriteByte(0);
        w.Write(data.ToArray());
        return ms.ToArray();
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
