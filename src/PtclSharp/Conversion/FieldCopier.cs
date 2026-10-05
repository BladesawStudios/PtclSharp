using System.Buffers.Binary;
using PtclSharp.Layout;

namespace PtclSharp.Conversion;

/// <summary>Copies fields between two layouts by name, adapting integer and float widths.</summary>
internal static class FieldCopier
{
    /// <summary>Result of copying one field.</summary>
    internal enum Outcome { Copied, Clamped, Incompatible }

    internal static Outcome Copy(FieldDef from, ReadOnlySpan<byte> source, FieldDef to, Span<byte> target)
    {
        if (from.Count != to.Count && !(IsRaw(from) && IsRaw(to)))
            return Outcome.Incompatible;

        if (IsRaw(from) || IsRaw(to))
        {
            int length = Math.Min(from.ByteLength, to.ByteLength);
            source.Slice(from.Offset, length).CopyTo(target.Slice(to.Offset, length));
            return Outcome.Copied;
        }

        if (from.Type == to.Type)
        {
            source.Slice(from.Offset, from.ByteLength).CopyTo(target.Slice(to.Offset, to.ByteLength));
            return Outcome.Copied;
        }

        bool clamped = false;
        for (int i = 0; i < from.Count; i++)
        {
            ReadOnlySpan<byte> src = source[(from.Offset + (i * from.ElementSize))..];
            Span<byte> dst = target[(to.Offset + (i * to.ElementSize))..];
            if (from.Type == FieldType.F32 || to.Type == FieldType.F32)
            {
                if (from.Type != FieldType.F32 || to.Type != FieldType.F32)
                {
                    // Integer <-> float: convert the value.
                    if (from.Type == FieldType.F32) clamped |= WriteInt(to.Type, dst, (long)BinaryPrimitives.ReadSingleLittleEndian(src), true);
                    else BinaryPrimitives.WriteSingleLittleEndian(dst, ReadInt(from.Type, src));
                }
            }
            else
            {
                clamped |= WriteInt(to.Type, dst, ReadInt(from.Type, src), false);
            }
        }
        return clamped ? Outcome.Clamped : Outcome.Copied;
    }

    private static bool IsRaw(FieldDef f) => f.Type is FieldType.Bytes or FieldType.String;

    private static long ReadInt(FieldType type, ReadOnlySpan<byte> s) => type switch
    {
        FieldType.U8 => s[0],
        FieldType.I8 => (sbyte)s[0],
        FieldType.U16 => BinaryPrimitives.ReadUInt16LittleEndian(s),
        FieldType.I16 => BinaryPrimitives.ReadInt16LittleEndian(s),
        FieldType.U32 => BinaryPrimitives.ReadUInt32LittleEndian(s),
        FieldType.I32 => BinaryPrimitives.ReadInt32LittleEndian(s),
        FieldType.I64 => BinaryPrimitives.ReadInt64LittleEndian(s),
        FieldType.U64 => unchecked((long)BinaryPrimitives.ReadUInt64LittleEndian(s)),
        _ => throw new InvalidOperationException($"Not an integer type: {type}.")
    };

    /// <summary>Writes with saturation; returns true when the value did not fit.</summary>
    private static bool WriteInt(FieldType type, Span<byte> d, long value, bool _)
    {
        (long min, long max) = type switch
        {
            FieldType.U8 => (0L, (long)byte.MaxValue),
            FieldType.I8 => (-128L, 127L),
            FieldType.U16 => (0L, (long)ushort.MaxValue),
            FieldType.I16 => (-32768L, 32767L),
            FieldType.U32 => (0L, (long)uint.MaxValue),
            FieldType.I32 => ((long)int.MinValue, (long)int.MaxValue),
            _ => (long.MinValue, long.MaxValue)
        };
        long clamped = Math.Clamp(value, min, max);
        switch (type)
        {
            case FieldType.U8: case FieldType.I8: d[0] = (byte)clamped; break;
            case FieldType.U16: BinaryPrimitives.WriteUInt16LittleEndian(d, (ushort)clamped); break;
            case FieldType.I16: BinaryPrimitives.WriteInt16LittleEndian(d, (short)clamped); break;
            case FieldType.U32: BinaryPrimitives.WriteUInt32LittleEndian(d, (uint)clamped); break;
            case FieldType.I32: BinaryPrimitives.WriteInt32LittleEndian(d, (int)clamped); break;
            case FieldType.I64: BinaryPrimitives.WriteInt64LittleEndian(d, clamped); break;
            case FieldType.U64: BinaryPrimitives.WriteUInt64LittleEndian(d, unchecked((ulong)clamped)); break;
            default: throw new InvalidOperationException($"Not an integer type: {type}.");
        }
        return clamped != value;
    }
}
