using Vector3 = System.Numerics.Vector3;
using BfresLibrary;
using Syroot.Maths;

namespace PtclSharp.Models;

/// <summary>
/// Turns a BotW effect model (BFRES 5.0.0.3) into a TotK one (10.0.0.0). The geometry is carried over byte for byte: the two models the
/// games share by name have identical vertex data, index data and attribute layouts, and TotK ships hundreds of models with the same
/// vertex layouts BotW uses. What Nintendo's own conversion changed in those two (found by comparing them) is what this does:
/// the source path is cleared, the matrix lists become null, the bounding sphere is recomputed as the smallest sphere around the vertices (BotW stored only a radius around the origin; TotK also stores the centre), and the material
/// becomes the empty "dummy" material TotK gives effect models, since the particle system only reads the model's vertex buffer and
/// first shape and never looks at the material. A BotW material cannot simply be re-saved as version 10: the two material formats differ
/// (version 10 stores hashed names and separate parameter tables) and BfresLibrary does not convert them.
/// </summary>
public static class BotwModelConverter
{
    private const float RadiusMargin = 0.0001f;

    /// <summary>Converts <paramref name="source"/> in place. <paramref name="dummyMaterialModel"/> supplies the TotK dummy material.</summary>
    public static void Convert(Model source, Model dummyMaterialModel, ICollection<string> notes)
    {
        Material original = source.Materials[0];
        Material dummy = dummyMaterialModel.Materials[0];
        dummy.Name = original.Name;
        if (source.Materials.Count > 1) notes.Add($"'{source.Name}' has {source.Materials.Count} materials; all its shapes now use the first.");
        if (source.Shapes.Count > 1) notes.Add($"'{source.Name}' has {source.Shapes.Count} shapes; the particle system reads only the first.");

        var materials = new ResDict<Material>();
        materials.Add(dummy.Name, dummy);
        source.Materials = materials;
        foreach (Shape shape in source.Shapes.Values) shape.MaterialIndex = 0;

        source.Path = "";
        source.Skeleton.MatrixToBoneList = null;
        source.Skeleton.InverseModelMatrices = null;

        foreach (Shape shape in source.Shapes.Values)
            RecomputeBounds(source, shape, notes);
    }

    private static void RecomputeBounds(Model model, Shape shape, ICollection<string> notes)
    {
        VertexBuffer buffer = model.VertexBuffers[shape.VertexBufferIndex];
        Vector3[]? positions = ReadPositions(buffer);
        if (positions is null || positions.Length == 0)
        {
            notes.Add($"'{model.Name}': vertex positions are in a format the bounds code does not read; the bounding sphere is unchanged.");
            return;
        }

        (Vector3 center, float sphere) = MinimalSphere.Of(positions);
        float radius = sphere + RadiusMargin;

        shape.RadiusArray = [radius];
        shape.BoundingRadiusList = [new Vector4F(center.X, center.Y, center.Z, radius)];
    }

    /// <summary>Decodes the <c>_p0</c> attribute (16-bit float x4 or 32-bit float x3), or null for another format.</summary>
    public static Vector3[]? ReadPositions(VertexBuffer buffer)
    {
        if (!buffer.Attributes.TryGetValue("_p0", out VertexAttrib? attribute)) return null;
        string name = attribute.Format.ToString();
        bool half = name == "Format_16_16_16_16_Single", single = name == "Format_32_32_32_Single";
        if (!half && !single) return null;

        BfresLibrary.Buffer data = buffer.Buffers[attribute.BufferIndex];
        byte[] bytes = data.Data[0];
        int stride = (int)data.Stride;
        int count = (int)buffer.VertexCount;
        var result = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            int at = (i * stride) + attribute.Offset;
            result[i] = half
                ? new Vector3((float)BitConverter.ToHalf(bytes, at), (float)BitConverter.ToHalf(bytes, at + 2), (float)BitConverter.ToHalf(bytes, at + 4))
                : new Vector3(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8));
        }
        return result;
    }
}
