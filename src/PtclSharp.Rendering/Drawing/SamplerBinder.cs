using PtclSharp.Rendering.Assets;
using PtclSharp.Rendering.Gl;
using PtclSharp.Simulation.Resources;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// Binds a program's samplers: the emitter's own textures to <c>sysTextureSampler0-2</c>, the scene depth and colour to the samplers the game fills from
/// its buffers, and stand-ins for the lighting lookups the game supplies at runtime.
/// </summary>
internal sealed class SamplerBinder : IDisposable
{
    readonly GL _gl;
    readonly ProceduralTextures _fallbacks;
    readonly uint[] _samplers = new uint[32];

    public SamplerBinder(GL gl, ProceduralTextures fallbacks)
    {
        _gl = gl;
        _fallbacks = fallbacks;
        for (int i = 0; i < _samplers.Length; i++)
            _samplers[i] = gl.GenSampler();
    }

    /// <param name="sceneDepth">The texture <c>sysDepthBufferTexture</c> reads.</param>
    /// <param name="sceneColor">Copies the scene colour on first use and returns it, for <c>sysFrameBufferTexture</c>.</param>
    public void Bind(GlProgram program, EmitterResource resource, EffectAssets assets, uint sceneDepth, Func<uint> sceneColor)
    {
        // The decompiler's fp_t_*/vp_t_* names alias a named sampler on the same unit; the named one decides what is bound.
        foreach (var (name, unit) in program.Samplers.OrderBy(sampler => sampler.Key.StartsWith("sys", StringComparison.Ordinal) ? 1 : 0))
        {
            if (!name.StartsWith("sys", StringComparison.Ordinal) && program.Samplers.Any(other => other.Value == unit && other.Key.StartsWith("sys", StringComparison.Ordinal)))
                continue;

            // The texture is resolved before the unit is chosen: creating a fallback leaves a different unit active.
            string kind = program.SamplerTypes.GetValueOrDefault(name, "sampler2D");
            TextureSlot? slot = null;
            (TextureTarget target, uint texture) = kind switch
            {
                _ when kind.StartsWith("samplerCube", StringComparison.Ordinal) => (TextureTarget.TextureCubeMap, _fallbacks.Sky),
                _ when kind.StartsWith("sampler3D", StringComparison.Ordinal) => (TextureTarget.Texture3D, _fallbacks.White3D(TextureTarget.Texture3D)),
                _ when kind.Contains("Array", StringComparison.Ordinal) => (TextureTarget.Texture2DArray, _fallbacks.White3D(TextureTarget.Texture2DArray)),
                _ => (TextureTarget.Texture2D, Texture2D(name, resource, assets, sceneDepth, sceneColor, out slot))
            };
            _gl.ActiveTexture(TextureUnit.Texture0 + unit);
            _gl.BindTexture(target, texture);

            uint sampler = _samplers[unit % _samplers.Length];
            Configure(sampler, slot);
            _gl.BindSampler((uint)unit, sampler);
        }
    }

    uint Texture2D(string name, EmitterResource resource, EffectAssets assets, uint sceneDepth, Func<uint> sceneColor, out TextureSlot? slot)
    {
        slot = null;
        if (name.StartsWith("sysTextureSampler", StringComparison.Ordinal) && int.TryParse(name.AsSpan("sysTextureSampler".Length), out int index) && index < resource.Textures.Length)
        {
            slot = resource.Textures[index];
            return assets.Textures.Get(slot.Guid);
        }
        if (name.Contains("Depth", StringComparison.Ordinal))
            return sceneDepth;
        if (name == "sysFrameBufferTexture")
            return sceneColor();
        // The game's lighting ramps are not in the files; mid grey stands in for the ones that are plain 2D lookups.
        if (name.StartsWith("sysCustomShader", StringComparison.Ordinal))
            return _fallbacks.Gray;
        return _fallbacks.White;
    }

    void Configure(uint sampler, TextureSlot? slot)
    {
        static GLEnum Wrap(byte mode) => mode switch { 0 => GLEnum.MirroredRepeat, 1 => GLEnum.Repeat, 2 => GLEnum.ClampToEdge, _ => GLEnum.MirrorClampToEdge };
        bool linear = slot is null || slot.Filter == 0;
        // Textures that are not an emitter's own (scene images, ramps) have no mip chain.
        GLEnum minFilter = slot is null ? GLEnum.Linear : linear ? GLEnum.LinearMipmapLinear : GLEnum.NearestMipmapNearest;
        _gl.SamplerParameter(sampler, SamplerParameterI.MinFilter, (int)minFilter);
        _gl.SamplerParameter(sampler, SamplerParameterI.MagFilter, (int)(linear ? GLEnum.Linear : GLEnum.Nearest));
        _gl.SamplerParameter(sampler, SamplerParameterI.WrapS, (int)(slot is null ? GLEnum.ClampToEdge : Wrap(slot.WrapU)));
        _gl.SamplerParameter(sampler, SamplerParameterI.WrapT, (int)(slot is null ? GLEnum.ClampToEdge : Wrap(slot.WrapV)));
    }

    public void Dispose()
    {
        foreach (uint sampler in _samplers)
            _gl.DeleteSampler(sampler);
    }
}
