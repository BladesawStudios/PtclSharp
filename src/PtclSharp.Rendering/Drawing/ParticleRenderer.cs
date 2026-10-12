using System.Numerics;
using System.Runtime.InteropServices;
using PtclSharp.Rendering.Assets;
using PtclSharp.Rendering.Gl;
using PtclSharp.Rendering.Lighting;
using PtclSharp.Simulation.Resources;
using PtclSharp.Simulation.Runtime;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// Draws simulated emitters with the game's own shaders. The view, static and dynamic uniform blocks and the per-particle attribute streams are what
/// nn::vfx hands to the GPU (EmitterCalculator::Draw), one instanced strip per emitter.
/// </summary>
/// <remarks>
/// Call <see cref="Begin"/> with the host's HDR target bound, draw sets, then <see cref="End"/>. The pass draws the way the game does (window origin at the
/// top left, OpenGL clip depth), so the target ends up upside down compared with an ordinary GL image and a host flips it when it presents.
/// </remarks>
public sealed unsafe class ParticleRenderer : IDisposable
{
    readonly GL _gl;
    readonly UniformBuffers _uniforms;
    readonly SamplerBinder _samplers;
    readonly SceneInputs _scene;
    readonly uint _viewBuffer, _supportBuffer, _fallbackBuffer;
    readonly Dictionary<Emitter, EmitterGpu> _gpu = [];
    readonly HashSet<string> _warned = [];
    ParticleView _view;
    Matrix4x4 _billboard = Matrix4x4.Identity;
    Vector3 _eye;
    uint _sceneDepth;

    public ParticleRenderer(GL gl, GameEnvironment? environment = null)
    {
        _gl = gl;
        Environment = environment ?? new GameEnvironment();
        Textures = new ProceduralTextures(gl, Environment);
        _uniforms = new UniformBuffers(gl);
        _samplers = new SamplerBinder(gl, Textures);
        _scene = new SceneInputs(gl);
        _viewBuffer = _uniforms.Create();
        _supportBuffer = _uniforms.Create();
        _fallbackBuffer = _uniforms.Create();
        FillConstantBuffers();
    }

    public GameEnvironment Environment { get; }

    public ProceduralTextures Textures { get; }

    public int DrawCalls { get; private set; }

    public int ParticlesDrawn { get; private set; }

    public List<string> Warnings { get; } = [];

    /// <summary>Loads an effect file's GPU assets, built on this renderer's environment and fallback textures.</summary>
    public EffectAssets LoadEffect(string name, PtclFile file, string? bootupPack, string effectPath) =>
        new(_gl, Textures, Environment, name, file, bootupPack, effectPath);

    /// <summary>Starts a pass: uploads the view, prepares the scene inputs and switches to the game's clip conventions.</summary>
    public void Begin(in ParticleView view)
    {
        _view = view;
        DrawCalls = ParticlesDrawn = 0;
        _sceneDepth = _scene.PrepareDepth(view) ?? Textures.White;
        _uniforms.Upload(_viewBuffer, MemoryMarshal.AsBytes(ViewBlock.Build(view, out _eye, out _billboard).AsSpan()));
        _gl.ClipControl(ClipControlOrigin.UpperLeft, ClipControlDepth.NegativeOneToOne);
    }

    /// <summary>Ends the pass and restores the ordinary clip conventions.</summary>
    public void End()
    {
        _gl.ClipControl(ClipControlOrigin.LowerLeft, ClipControlDepth.NegativeOneToOne);
        _gl.BindVertexArray(0);
    }

    public void DrawSet(EmitterSetInstance set, EffectAssets assets, Func<Emitter, bool>? visible = null)
    {
        foreach (Emitter root in set.Roots)
        {
            // Children flagged "pre-draw" go under their parent.
            foreach (Emitter child in root.ChildLists.SelectMany(list => list).Where(child => child.Res.ChildPreDraw))
                DrawEmitter(child, assets, visible);
            DrawEmitter(root, assets, visible);
            foreach (Emitter child in root.ChildLists.SelectMany(list => list).Where(child => !child.Res.ChildPreDraw))
                DrawEmitter(child, assets, visible);
        }
    }

    void DrawEmitter(Emitter emitter, EffectAssets assets, Func<Emitter, bool>? visible)
    {
        if ((emitter.ParticleCount <= 0 && emitter.Plugin is null) || !emitter.IsVisible || (visible is not null && !visible(emitter)))
            return;
        EmitterResource resource = emitter.Res;
        // An emitter that hands its particles to a game callback (custom action) and has no texture is a light or logic emitter: the game draws nothing for it.
        if (resource.CustomActionIndex > 0 && !resource.Textures.Any(texture => texture.IsValid))
            return;
        GlProgram? program = assets.GetProgram(resource.ShaderIndex);
        if (program is null)
            return;

        EmitterGpu gpu = GpuFor(emitter, resource);
        UploadBlocks(emitter, gpu);
        RenderStates.Apply(_gl, resource.Render);
        _gl.UseProgram(program.Handle);
        BindBlocks(program, gpu);
        _samplers.Bind(program, resource, assets, _sceneDepth, () => _scene.Color(_view.Width, _view.Height));

        if (emitter.Plugin is not null)
        {
            ParticlesDrawn += gpu.DrawPlugin(emitter.Plugin, program, _uniforms);
            DrawCalls++;
            CheckError(program, $"stripe {resource.Name}");
            return;
        }
        DrawParticles(emitter, resource, assets, program, gpu);
    }

    void DrawParticles(Emitter emitter, EmitterResource resource, EffectAssets assets, GlProgram program, EmitterGpu gpu)
    {
        int count = emitter.ParticleCount;
        int[]? order = SortOrder.For(emitter, _view.View);
        int instances = order?.Length ?? count;
        GpuMesh? mesh = resource.G3dPrimitiveId != ulong.MaxValue ? assets.Meshes.GetMesh(resource.G3dPrimitiveId) : null;
        if (resource.G3dPrimitiveId != ulong.MaxValue && mesh is null)
            Warn($"mesh:{assets.Name}:{resource.G3dPrimitiveId:X}", $"{resource.Name}: model {resource.G3dPrimitiveId:X} not found");

        uint array = gpu.BindAttributes(emitter, program, order, mesh, message => Warn($"{program.Label}:{message}", message));
        _gl.BindVertexArray(array);
        AreaLoop? area = resource.Plugin is { Type: 4 } ? (gpu.Area ??= new AreaLoop(resource.Plugin.Data)) : null;
        for (int repeat = 0; repeat < (area?.RepeatCount ?? 1); repeat++)
        {
            if (area is not null)
                _uniforms.Upload(gpu.Plugin, area.Block(emitter.MatrixSrt, _eye, _billboard, repeat));
            if (mesh is not null)
                _gl.DrawElementsInstanced(mesh.Topology, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null, (uint)instances);
            else
                _gl.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, (uint)instances);
        }
        DrawCalls++;
        ParticlesDrawn += instances;
        CheckError(program, resource.Name);
    }

    EmitterGpu GpuFor(Emitter emitter, EmitterResource resource)
    {
        if (_gpu.TryGetValue(emitter, out EmitterGpu? known))
            return known;
        var gpu = new EmitterGpu(_gl, _uniforms);
        _uniforms.Upload(gpu.Static, resource.StaticBlock);
        if (resource.CustomShaderBlock.Length > 0)
            _uniforms.Upload(gpu.Reserved, resource.CustomShaderBlock);
        return _gpu[emitter] = gpu;
    }

    void UploadBlocks(Emitter emitter, EmitterGpu gpu)
    {
        _uniforms.Upload(gpu.Dynamic, emitter.DynamicBlock);
        for (int i = 0; i < gpu.Game.Length; i++)
            _uniforms.Upload(gpu.Game[i], Environment.BuildBlock(i));
    }

    void BindBlocks(GlProgram program, EmitterGpu gpu)
    {
        foreach (var (name, binding) in program.UniformBlocks)
        {
            uint buffer = name switch
            {
                "support_buffer" => _supportBuffer,
                "sysViewUniformBlock" => _viewBuffer,
                "sysEmitterStaticUniformBlock" => gpu.Static,
                "sysEmitterDynamicUniformBlock" => gpu.Dynamic,
                "sysCustomShaderReservedUniformBlockParam" => gpu.Reserved,
                "sysEmitterPluginUniformBlock" => gpu.Plugin,
                _ when name.StartsWith("sysCustomShaderUniformBlock", StringComparison.Ordinal) => gpu.Game[Math.Clamp(name[^1] - '0', 0, 3)],
                _ => _fallbackBuffer
            };
            _gl.BindBufferBase(BufferTargetARB.UniformBuffer, binding, buffer);
        }
    }

    // The shaders read ones for any block we do not model, and the decompiler's own block has its alpha test and render scale at 1.
    void FillConstantBuffers()
    {
        var ones = new float[1024];
        Array.Fill(ones, 1f);
        _uniforms.Upload(_fallbackBuffer, MemoryMarshal.AsBytes(ones.AsSpan()));

        byte[] support = new byte[512];
        BitConverter.GetBytes(1f).CopyTo(support, 36);
        for (int i = 0; i < 73; i++)
            BitConverter.GetBytes(1f).CopyTo(support, 56 + (i * 4));
        _uniforms.Upload(_supportBuffer, support);
    }

    void CheckError(GlProgram program, string what)
    {
        GLEnum error = _gl.GetError();
        if (error != GLEnum.NoError)
            Warn($"gl:{program.Label}:{error}", $"GL {error} drawing {what} ({program.Label})");
    }

    void Warn(string key, string message)
    {
        if (_warned.Add(key))
            Warnings.Add(message);
    }

    /// <summary>Drops the GPU state of emitters that no longer exist.</summary>
    public void Prune(IEnumerable<Emitter> live)
    {
        var keep = new HashSet<Emitter>(live);
        foreach (Emitter emitter in _gpu.Keys.Where(candidate => !keep.Contains(candidate)).ToArray())
        {
            _gpu[emitter].Dispose();
            _gpu.Remove(emitter);
        }
    }

    public void Dispose()
    {
        foreach (EmitterGpu gpu in _gpu.Values)
            gpu.Dispose();
        _gpu.Clear();
        foreach (uint buffer in new[] { _viewBuffer, _supportBuffer, _fallbackBuffer })
            _gl.DeleteBuffer(buffer);
        _samplers.Dispose();
        _scene.Dispose();
        Textures.Dispose();
    }
}
