namespace PtclSharp.Layout;

/// <summary>How much evidence backs a field's name and meaning.</summary>
public enum FieldStatus
{
    /// <summary>The executable of this layout's own game proves the field (a read/write/arithmetic role).</summary>
    Confirmed,

    /// <summary>
    /// Name/offset carried over from the other game's proven field. Not yet decompiled in this game.
    /// </summary>
    Paired,

    /// <summary>
    /// Location and width are known (or reserved); purpose is not proven. The bytes are preserved and
    /// carried through conversions, never interpreted.
    /// </summary>
    Unverified
}

/// <summary>Element type of a serialized field. <see cref="Bytes"/> and <see cref="String"/> use <see cref="FieldDef.Count"/> as a byte length.</summary>
public enum FieldType
{
    Bytes,
    U8,
    I8,
    U16,
    I16,
    U32,
    I32,
    U64,
    I64,
    F32,
    String
}

/// <summary>One serialized field of a binary structure. Offsets are relative to the structure's data start.</summary>
/// <param name="Name">Stable identifier shared between game layouts (the join key for conversion).</param>
/// <param name="Offset">Byte offset from the start of the structure.</param>
/// <param name="Type">Element type.</param>
/// <param name="Count">Element count (byte length for <see cref="FieldType.Bytes"/> / <see cref="FieldType.String"/>).</param>
/// <param name="Status">Evidence level.</param>
/// <param name="Evidence">One-line justification; the full evidence lives in docs/research.</param>
public sealed record FieldDef(string Name, int Offset, FieldType Type, int Count, FieldStatus Status, string Evidence)
{
    public int ElementSize => Type switch
    {
        FieldType.U8 or FieldType.I8 or FieldType.Bytes or FieldType.String => 1,
        FieldType.U16 or FieldType.I16 => 2,
        FieldType.U32 or FieldType.I32 or FieldType.F32 => 4,
        FieldType.U64 or FieldType.I64 => 8,
        _ => throw new InvalidOperationException($"Unknown field type {Type}.")
    };

    public int ByteLength => ElementSize * Count;

    /// <summary>First byte after the field.</summary>
    public int End => Offset + ByteLength;

    /// <summary>A single numeric element (not an array, byte blob or string).</summary>
    public bool IsScalar => Count == 1 && Type is not (FieldType.Bytes or FieldType.String);

    public bool IsConfirmed => Status == FieldStatus.Confirmed;

    public override string ToString() => $"{Name} @0x{Offset:X} {Type}[{Count}] ({Status})";
}
