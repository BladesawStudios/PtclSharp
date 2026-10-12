using System.Buffers.Binary;
using System.Text;

namespace PtclSharp.Rendering.Assets;

/// <summary>Reads one file out of a SARC archive (the game's resident resource pack).</summary>
internal static class SarcReader
{
    /// <summary>The bytes of <paramref name="path"/> inside <paramref name="sarc"/>, or null when it is not there.</summary>
    public static byte[]? Extract(byte[] sarc, string path)
    {
        if (sarc.Length < 0x14 || !sarc.AsSpan(0, 4).SequenceEqual("SARC"u8))
            throw new InvalidDataException("Not a SARC archive.");
        bool little = BinaryPrimitives.ReadUInt16LittleEndian(sarc.AsSpan(6)) == 0xFEFF;
        ushort U16(int offset) => little ? BinaryPrimitives.ReadUInt16LittleEndian(sarc.AsSpan(offset)) : BinaryPrimitives.ReadUInt16BigEndian(sarc.AsSpan(offset));
        uint U32(int offset) => little ? BinaryPrimitives.ReadUInt32LittleEndian(sarc.AsSpan(offset)) : BinaryPrimitives.ReadUInt32BigEndian(sarc.AsSpan(offset));

        int headerSize = U16(4), dataStart = (int)U32(0x0C), count = U16(headerSize + 6), nodes = headerSize + 12;
        int names = nodes + (count * 16) + 8;
        for (int i = 0; i < count; i++)
        {
            int node = nodes + (i * 16);
            int nameOffset = names + ((int)(U32(node + 4) & 0xFFFFFF) * 4);
            int nameEnd = Array.IndexOf(sarc, (byte)0, nameOffset);
            if (Encoding.UTF8.GetString(sarc, nameOffset, nameEnd - nameOffset) != path)
                continue;
            int start = dataStart + (int)U32(node + 8);
            return sarc.AsSpan(start, (int)U32(node + 12) - (int)U32(node + 8)).ToArray();
        }
        return null;
    }
}
