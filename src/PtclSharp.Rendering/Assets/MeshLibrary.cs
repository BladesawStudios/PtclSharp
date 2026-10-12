using System.Numerics;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Assets;

/// <summary>A draw mesh (an effect's G3D model) on the GPU.</summary>
public sealed class GpuMesh
{
    public uint Pos, Normal, Tangent, Color, Uv0, Uv1, Indices;
    public int IndexCount;
    public PrimitiveType Topology = PrimitiveType.Triangles;
}

/// <summary>
/// The draw meshes and volume primitives of one effect file, read from the cache <see cref="MeshDumpTool"/> writes and uploaded to GL on first use.
/// </summary>
public sealed unsafe class MeshLibrary
{
    readonly GL _gl;
    readonly Dictionary<ulong, (int Vertices, float[] Data, uint[] Indices, uint Topology)> _models = [];
    readonly Dictionary<ulong, (Vector3 Position, Vector3 Normal)[]> _primitives = [];
    readonly Dictionary<ulong, GpuMesh> _uploaded = [];

    public MeshLibrary(GL gl, string effectPath, string bootupPack)
    {
        _gl = gl;
        try { Read(MeshDumpTool.EnsureCache(effectPath, bootupPack)); }
        catch (Exception e) { Error = e.Message; }
    }

    /// <summary>Why the meshes could not be loaded, or null.</summary>
    public string? Error { get; }

    public IReadOnlyList<(Vector3 Position, Vector3 Normal)>? Primitive(ulong id) => _primitives.GetValueOrDefault(id);

    public GpuMesh? GetMesh(ulong id)
    {
        if (_uploaded.TryGetValue(id, out GpuMesh? known))
            return known;
        if (!_models.TryGetValue(id, out var model))
            return null;

        var mesh = new GpuMesh
        {
            Pos = Attribute(model, 0), Normal = Attribute(model, 1), Tangent = Attribute(model, 2), Color = Attribute(model, 3),
            Uv0 = Attribute(model, 4), Uv1 = Attribute(model, 5), IndexCount = model.Indices.Length,
            Topology = model.Topology switch { 1 => PrimitiveType.TriangleStrip, 2 => PrimitiveType.Lines, _ => PrimitiveType.Triangles }
        };
        mesh.Indices = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Indices);
        fixed (uint* p = model.Indices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(model.Indices.Length * 4), p, BufferUsageARB.StaticDraw);
        return _uploaded[id] = mesh;
    }

    // One vec4 buffer out of the interleaved six-attribute vertex data.
    uint Attribute((int Vertices, float[] Data, uint[] Indices, uint Topology) model, int slot)
    {
        float[] values = new float[model.Vertices * 4];
        for (int i = 0; i < model.Vertices; i++)
            Array.Copy(model.Data, (i * 24) + (slot * 4), values, i * 4, 4);
        uint buffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        fixed (float* p = values)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(values.Length * 4), p, BufferUsageARB.StaticDraw);
        return buffer;
    }

    void Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (new string(reader.ReadChars(4)) != "PMSH" || reader.ReadUInt32() != 2)
            throw new InvalidDataException("bad mesh cache");
        uint models = reader.ReadUInt32(), primitives = reader.ReadUInt32();
        for (uint m = 0; m < models; m++)
        {
            ulong id = reader.ReadUInt64();
            int vertices = (int)reader.ReadUInt32(), indices = (int)reader.ReadUInt32();
            uint topology = reader.ReadUInt32();
            var data = new float[vertices * 24];
            for (int i = 0; i < data.Length; i++)
                data[i] = reader.ReadSingle();
            var index = new uint[indices];
            for (int i = 0; i < indices; i++)
                index[i] = reader.ReadUInt32();
            _models[id] = (vertices, data, index, topology);
        }
        for (uint p = 0; p < primitives; p++)
        {
            ulong id = reader.ReadUInt64();
            var points = new (Vector3, Vector3)[(int)reader.ReadUInt32()];
            for (int i = 0; i < points.Length; i++)
                points[i] = (new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            _primitives[id] = points;
        }
    }
}
