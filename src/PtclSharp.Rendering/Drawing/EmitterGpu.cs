using System.Numerics;
using PtclSharp.Rendering.Assets;
using PtclSharp.Rendering.Gl;
using PtclSharp.Simulation.Runtime;
using PtclSharp.Simulation.Runtime.Plugins;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// The GPU state of one running emitter: its uniform blocks, and the vertex buffers its particles' attribute streams are streamed into each frame. A
/// draw is one instanced four-vertex strip per particle slot, or the mesh's indices instanced the same way.
/// </summary>
internal sealed unsafe class EmitterGpu : IDisposable
{
    readonly GL _gl;
    readonly Dictionary<string, uint> _buffers = [];
    readonly Dictionary<GlProgram, uint> _arrays = [];
    uint _pluginArray, _pluginBuffer;
    int _capacity;

    public EmitterGpu(GL gl, UniformBuffers uniforms)
    {
        _gl = gl;
        Static = uniforms.Create();
        Dynamic = uniforms.Create();
        Reserved = uniforms.Create();
        Plugin = uniforms.Create();
        Game = [uniforms.Create(), uniforms.Create(), uniforms.Create(), uniforms.Create()];
    }

    public uint Static { get; }

    public uint Dynamic { get; }

    /// <summary>The custom shader parameter block (CSDP).</summary>
    public uint Reserved { get; }

    /// <summary>The plugin block: a stripe's per-draw parameters, or the area loop's.</summary>
    public uint Plugin { get; }

    /// <summary>The game-supplied custom shader blocks 0 to 3.</summary>
    public uint[] Game { get; }

    public AreaLoop? Area { get; set; }

    /// <summary>Streams the particle attributes the program reads and returns the vertex array to draw with.</summary>
    public uint BindAttributes(Emitter emitter, GlProgram program, int[]? order, GpuMesh? mesh, Action<string> warn)
    {
        ParticleStore particles = emitter.Particles;
        if (!_arrays.TryGetValue(program, out uint array))
            _arrays[program] = array = _gl.GenVertexArray();
        _gl.BindVertexArray(array);
        if (mesh is not null)
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Indices);

        int count = order?.Length ?? emitter.ParticleCount;
        foreach (var (name, location) in program.Inputs)
        {
            if (name.StartsWith("gl_", StringComparison.Ordinal))
                continue;
            if (MeshBuffer(mesh, name) is uint meshBuffer and not 0)
            {
                BindMeshAttribute(meshBuffer, location);
                continue;
            }
            Vector4[]? source = ParticleAttributes.For(particles, name);
            if (source is null)
            {
                warn($"{program.Label}: no data for attribute {name}");
                _gl.DisableVertexAttribArray(location);
                _gl.VertexAttrib4(location, 1f, 1f, 1f, 1f);
                continue;
            }
            StreamAttribute(name, location, source, order, count, particles.Capacity);
        }
        _capacity = Math.Max(_capacity, particles.Capacity);
        return array;
    }

    /// <summary>Draws the geometry an emitter plugin (a stripe) built: one triangle strip per draw, each with its own uniform block.</summary>
    public int DrawPlugin(EmitterPlugin plugin, GlProgram program, UniformBuffers uniforms)
    {
        var draws = plugin.BuildDraws();
        if (draws.Count == 0)
            return 0;
        if (_pluginArray == 0)
        {
            _pluginArray = _gl.GenVertexArray();
            _pluginBuffer = _gl.GenBuffer();
        }
        _gl.BindVertexArray(_pluginArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _pluginBuffer);
        int stride = draws[0].Stride;
        foreach (var (name, location) in program.Inputs)
        {
            if (!name.StartsWith("sysEmitterPluginAttr", StringComparison.Ordinal) || !int.TryParse(name.AsSpan("sysEmitterPluginAttr".Length), out int slot))
                continue;
            _gl.EnableVertexAttribArray(location);
            _gl.VertexAttribPointer(location, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(slot * 16));
            _gl.VertexAttribDivisor(location, 0);
        }

        int strips = 0;
        foreach (PluginDraw draw in draws)
        {
            if (draw.Count < 3)
                continue;
            uniforms.Upload(Plugin, draw.Block);
            UploadVertices(draw);
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, (uint)draw.Count);
            strips += draw.Count / 2;
        }
        return strips;
    }

    void UploadVertices(PluginDraw draw)
    {
        if (draw.Bytes is not null)
            fixed (byte* bytes = draw.Bytes)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(draw.Count * draw.Stride), bytes, BufferUsageARB.StreamDraw);
        else
            fixed (PluginVertex* vertices = draw.Vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(draw.Count * sizeof(PluginVertex)), vertices + draw.Offset, BufferUsageARB.StreamDraw);
    }

    static uint? MeshBuffer(GpuMesh? mesh, string attribute) => mesh is null ? null : attribute switch
    {
        "sysPosAttr" => mesh.Pos,
        "sysNormalAttr" => mesh.Normal,
        "sysTangentAttr" => mesh.Tangent,
        "sysColorAttr" or "sysVertexColor0Attr" or "sysVertexColor1Attr" => mesh.Color,
        "sysTexCoordAttr" => mesh.Uv0,
        "sysTexCoord1Attr" => mesh.Uv1,
        _ => null
    };

    void BindMeshAttribute(uint buffer, uint location)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        _gl.EnableVertexAttribArray(location);
        _gl.VertexAttribPointer(location, 4, VertexAttribPointerType.Float, false, 16, null);
        _gl.VertexAttribDivisor(location, 0);
    }

    void StreamAttribute(string name, uint location, Vector4[] source, int[]? order, int count, int capacity)
    {
        if (!_buffers.TryGetValue(name, out uint buffer) || _capacity < capacity)
        {
            if (buffer == 0)
                buffer = _gl.GenBuffer();
            _buffers[name] = buffer;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(capacity * 16), null, BufferUsageARB.DynamicDraw);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        if (order is null)
            fixed (Vector4* p = source)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(count * 16), p);
        else
        {
            var gathered = new Vector4[count];
            for (int i = 0; i < count; i++)
                gathered[i] = source[order[i]];
            fixed (Vector4* p = gathered)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(count * 16), p);
        }
        _gl.EnableVertexAttribArray(location);
        _gl.VertexAttribPointer(location, 4, VertexAttribPointerType.Float, false, 16, null);
        _gl.VertexAttribDivisor(location, 1);
    }

    public void Dispose()
    {
        foreach (uint buffer in new[] { Static, Dynamic, Reserved, Plugin }.Concat(Game).Concat(_buffers.Values))
            _gl.DeleteBuffer(buffer);
        foreach (uint array in _arrays.Values)
            _gl.DeleteVertexArray(array);
        if (_pluginArray != 0)
        {
            _gl.DeleteVertexArray(_pluginArray);
            _gl.DeleteBuffer(_pluginBuffer);
        }
    }
}
