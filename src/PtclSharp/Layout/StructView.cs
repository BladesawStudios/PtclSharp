using System.Buffers.Binary;
using System.Text;

namespace PtclSharp.Layout;

/// <summary>
/// Zero-copy typed access to a structure's bytes through a <see cref="StructLayout"/>. Reads and writes touch
/// only the named field; every other byte, including gaps and <see cref="FieldStatus.Unverified"/> fields, is
/// left exactly as it is, so editing through a view is lossless.
/// </summary>
public readonly ref struct StructView
{
    public StructView(StructLayout layout, Span<byte> data)
    {
        if (data.Length < layout.Size)
            throw new ArgumentException($"{layout.Name} needs {layout.Size} bytes but the span has {data.Length}.");
        Layout = layout;
        Data = data;
    }

    public StructLayout Layout { get; }
    public Span<byte> Data { get; }

    public byte GetByte(string field, int index = 0) => Data[Locate(field, FieldType.U8, index)];
    public sbyte GetSByte(string field, int index = 0) => (sbyte)Data[Locate(field, FieldType.I8, index)];
    public ushort GetUInt16(string field, int index = 0) => BinaryPrimitives.ReadUInt16LittleEndian(Data[Locate(field, FieldType.U16, index)..]);
    public short GetInt16(string field, int index = 0) => BinaryPrimitives.ReadInt16LittleEndian(Data[Locate(field, FieldType.I16, index)..]);
    public uint GetUInt32(string field, int index = 0) => BinaryPrimitives.ReadUInt32LittleEndian(Data[Locate(field, FieldType.U32, index)..]);
    public int GetInt32(string field, int index = 0) => BinaryPrimitives.ReadInt32LittleEndian(Data[Locate(field, FieldType.I32, index)..]);
    public ulong GetUInt64(string field, int index = 0) => BinaryPrimitives.ReadUInt64LittleEndian(Data[Locate(field, FieldType.U64, index)..]);
    public long GetInt64(string field, int index = 0) => BinaryPrimitives.ReadInt64LittleEndian(Data[Locate(field, FieldType.I64, index)..]);
    public float GetSingle(string field, int index = 0) => BinaryPrimitives.ReadSingleLittleEndian(Data[Locate(field, FieldType.F32, index)..]);

    public void SetByte(string field, byte value, int index = 0) => Data[Locate(field, FieldType.U8, index)] = value;
    public void SetSByte(string field, sbyte value, int index = 0) => Data[Locate(field, FieldType.I8, index)] = (byte)value;
    public void SetUInt16(string field, ushort value, int index = 0) => BinaryPrimitives.WriteUInt16LittleEndian(Data[Locate(field, FieldType.U16, index)..], value);
    public void SetInt16(string field, short value, int index = 0) => BinaryPrimitives.WriteInt16LittleEndian(Data[Locate(field, FieldType.I16, index)..], value);
    public void SetUInt32(string field, uint value, int index = 0) => BinaryPrimitives.WriteUInt32LittleEndian(Data[Locate(field, FieldType.U32, index)..], value);
    public void SetInt32(string field, int value, int index = 0) => BinaryPrimitives.WriteInt32LittleEndian(Data[Locate(field, FieldType.I32, index)..], value);
    public void SetUInt64(string field, ulong value, int index = 0) => BinaryPrimitives.WriteUInt64LittleEndian(Data[Locate(field, FieldType.U64, index)..], value);
    public void SetInt64(string field, long value, int index = 0) => BinaryPrimitives.WriteInt64LittleEndian(Data[Locate(field, FieldType.I64, index)..], value);
    public void SetSingle(string field, float value, int index = 0) => BinaryPrimitives.WriteSingleLittleEndian(Data[Locate(field, FieldType.F32, index)..], value);

    /// <summary>The raw bytes of any field (works for every type).</summary>
    public Span<byte> GetBytes(string field)
    {
        FieldDef def = Layout[field];
        return Data.Slice(def.Offset, def.ByteLength);
    }

    /// <summary>A NUL-terminated UTF-8 string field.</summary>
    public string GetString(string field)
    {
        FieldDef def = Layout[field];
        if (def.Type != FieldType.String) throw new InvalidOperationException($"{Layout.Name}.{field} is {def.Type}, not a string.");
        ReadOnlySpan<byte> span = Data.Slice(def.Offset, def.ByteLength);
        int end = span.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? span : span[..end]);
    }

    /// <summary>Writes a string, NUL-padding the rest of the field. Throws when it does not fit.</summary>
    public void SetString(string field, string value)
    {
        FieldDef def = Layout[field];
        if (def.Type != FieldType.String) throw new InvalidOperationException($"{Layout.Name}.{field} is {def.Type}, not a string.");
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > def.ByteLength)
            throw new ArgumentException($"'{value}' needs {bytes.Length} bytes; {Layout.Name}.{field} holds {def.ByteLength}.");
        Span<byte> span = Data.Slice(def.Offset, def.ByteLength);
        span.Clear();
        bytes.CopyTo(span);
    }

    /// <summary>Reads any numeric element as a boxed value (u8..u64, i8..i64, f32); arrays return the element at <paramref name="index"/>.</summary>
    public object Read(FieldDef def, int index = 0) => def.Type switch
    {
        FieldType.U8 => (object)Data[ElementOffset(def, index)],
        FieldType.I8 => (object)(sbyte)Data[ElementOffset(def, index)],
        FieldType.U16 => (object)BinaryPrimitives.ReadUInt16LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.I16 => (object)BinaryPrimitives.ReadInt16LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.U32 => (object)BinaryPrimitives.ReadUInt32LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.I32 => (object)BinaryPrimitives.ReadInt32LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.U64 => (object)BinaryPrimitives.ReadUInt64LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.I64 => (object)BinaryPrimitives.ReadInt64LittleEndian(Data[ElementOffset(def, index)..]),
        FieldType.F32 => (object)BinaryPrimitives.ReadSingleLittleEndian(Data[ElementOffset(def, index)..]),
        _ => throw new InvalidOperationException($"{def.Name} is {def.Type}; use GetBytes/GetString.")
    };

    private int Locate(string field, FieldType expected, int index)
    {
        FieldDef def = Layout[field];
        if (def.Type != expected)
            throw new InvalidOperationException($"{Layout.Name}.{field} is {def.Type}, not {expected}.");
        return ElementOffset(def, index);
    }

    private static int ElementOffset(FieldDef def, int index)
    {
        if ((uint)index >= (uint)def.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"{def.Name} has {def.Count} element(s).");
        return def.Offset + (index * def.ElementSize);
    }
}
