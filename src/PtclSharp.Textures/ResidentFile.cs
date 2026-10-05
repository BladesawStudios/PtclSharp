using System.Buffers.Binary;
using System.Text;

namespace PtclSharp.Textures;

/// <summary>Finds BotW's resident effect resource, <c>Effect/GameResident.sesetlist</c>, inside <c>Pack/Bootup.pack</c> (a SARC).</summary>
public static class ResidentFile
{
    public const string PathInPack = "Effect/GameResident.sesetlist";

    /// <summary>Reads the file out of the pack, or null when the pack does not have it.</summary>
    public static byte[]? ReadFromPack(string bootupPackPath) => Extract(File.ReadAllBytes(bootupPackPath), PathInPack);

    public static PtclFile LoadFromPack(string bootupPackPath) =>
        PtclFile.ReadSesetlist(ReadFromPack(bootupPackPath) ?? throw new FileNotFoundException($"{PathInPack} is not in {bootupPackPath}."));

    /// <summary>Extracts one file from a SARC archive by its full path.</summary>
    public static byte[]? Extract(byte[] sarc, string path)
    {
        if (sarc.Length < 0x14 || !sarc.AsSpan(0, 4).SequenceEqual("SARC"u8)) throw new InvalidDataException("Not a SARC archive.");
        bool little = BinaryPrimitives.ReadUInt16LittleEndian(sarc.AsSpan(6)) == 0xFEFF;
        ushort U16(int o) => little ? BinaryPrimitives.ReadUInt16LittleEndian(sarc.AsSpan(o)) : BinaryPrimitives.ReadUInt16BigEndian(sarc.AsSpan(o));
        uint U32(int o) => little ? BinaryPrimitives.ReadUInt32LittleEndian(sarc.AsSpan(o)) : BinaryPrimitives.ReadUInt32BigEndian(sarc.AsSpan(o));

        int headerSize = U16(4);
        int dataStart = (int)U32(0x0C);
        int sfat = headerSize;
        int count = U16(sfat + 6);
        int nodes = sfat + 12;
        int names = nodes + (count * 16) + 8; // after the SFNT header
        for (int i = 0; i < count; i++)
        {
            int node = nodes + (i * 16);
            int nameOffset = names + ((int)(U32(node + 4) & 0xFFFFFF) * 4);
            int nameEnd = Array.IndexOf(sarc, (byte)0, nameOffset);
            if (Encoding.UTF8.GetString(sarc, nameOffset, nameEnd - nameOffset) != path) continue;
            int start = dataStart + (int)U32(node + 8);
            return sarc.AsSpan(start, (int)U32(node + 12) - (int)U32(node + 8)).ToArray();
        }
        return null;
    }
}
