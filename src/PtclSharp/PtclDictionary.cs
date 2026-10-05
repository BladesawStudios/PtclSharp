using ZstdSharp;

namespace PtclSharp;

/// <summary>Loads the Zstandard dictionary that TotK's <c>.esetb.byml.zs</c> files are compressed with.</summary>
public static class PtclDictionary
{
    /// <summary>Finds the dictionary with ID 1 inside <c>Pack/ZsDic.pack.zs</c> (a Zstandard-compressed SARC).</summary>
    public static byte[] FromZsDicPack(ReadOnlySpan<byte> zsDicPack)
    {
        byte[] sarc;
        using (var decompressor = new Decompressor())
            sarc = decompressor.Unwrap(zsDicPack).ToArray();

        int dataOffset = BitConverter.ToInt32(sarc, 0xC);
        const int sfat = 0x14;
        int count = BitConverter.ToUInt16(sarc, sfat + 6);
        for (int i = 0; i < count; i++)
        {
            int entry = sfat + 12 + (i * 16);
            int start = BitConverter.ToInt32(sarc, entry + 8);
            int end = BitConverter.ToInt32(sarc, entry + 12);
            byte[] candidate = sarc[(dataOffset + start)..(dataOffset + end)];
            if (BitConverter.ToUInt32(candidate, 4) == 1) return candidate;
        }
        throw new InvalidDataException("No Zstandard dictionary with ID 1 in ZsDic.pack.zs.");
    }

    public static byte[] FromZsDicPackFile(string path) => FromZsDicPack(File.ReadAllBytes(path));
}
