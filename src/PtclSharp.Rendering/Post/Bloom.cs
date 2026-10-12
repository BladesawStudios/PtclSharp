using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Post;

/// <summary>
/// Bloom for an HDR image: the bright parts are filtered into a pyramid of half-size levels, then each level is blurred back up into the one above.
/// <see cref="Render"/> returns the glow as a texture to add to the scene when it is tonemapped.
/// </summary>
public sealed class Bloom : IDisposable
{
    readonly GL _gl;
    readonly FullscreenPass _prefilter, _down, _up;
    readonly uint _framebuffer;
    readonly List<(uint Texture, int Width, int Height)> _levels = [];
    int _sourceWidth, _sourceHeight;

    public Bloom(GL gl)
    {
        _gl = gl;
        _prefilter = new FullscreenPass(gl, "BloomPrefilter.frag");
        _down = new FullscreenPass(gl, "BloomDown.frag");
        _up = new FullscreenPass(gl, "BloomUp.frag");
        _framebuffer = gl.GenFramebuffer();
    }

    /// <summary>Blurs the bright parts of <paramref name="scene"/> and returns the result, which is half the size of the scene.</summary>
    public uint Render(uint scene, int width, int height, BloomSettings settings)
    {
        EnsureLevels(width, height, Math.Clamp(settings.Levels, 2, 8));
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.ScissorTest);

        BindLevel(0);
        _prefilter.Use();
        _prefilter.SetTexture("source", 0, scene);
        _prefilter.SetVector2("texel", 1f / width, 1f / height);
        _prefilter.SetFloat("threshold", settings.Threshold);
        _prefilter.SetFloat("knee", Math.Max(settings.Knee, 1e-4f));
        _prefilter.Draw();

        _down.Use();
        for (int level = 1; level < _levels.Count; level++)
        {
            BindLevel(level);
            _down.SetTexture("source", 0, _levels[level - 1].Texture);
            _down.SetVector2("texel", 1f / _levels[level - 1].Width, 1f / _levels[level - 1].Height);
            _down.Draw();
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _up.Use();
        for (int level = _levels.Count - 1; level >= 1; level--)
        {
            BindLevel(level - 1);
            _up.SetTexture("source", 0, _levels[level].Texture);
            _up.SetVector2("texel", 1f / _levels[level].Width, 1f / _levels[level].Height);
            _up.SetFloat("radius", settings.Radius);
            _up.Draw();
        }
        _gl.Disable(EnableCap.Blend);
        return _levels[0].Texture;
    }

    void BindLevel(int level)
    {
        var (texture, width, height) = _levels[level];
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        _gl.Viewport(0, 0, (uint)width, (uint)height);
    }

    unsafe void EnsureLevels(int width, int height, int count)
    {
        if (width == _sourceWidth && height == _sourceHeight && _levels.Count == count)
            return;
        ReleaseLevels();
        (_sourceWidth, _sourceHeight) = (width, height);
        for (int level = 0; level < count; level++)
        {
            int w = Math.Max(1, width >> (level + 1)), h = Math.Max(1, height >> (level + 1));
            uint texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, texture);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.HalfFloat, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _levels.Add((texture, w, h));
        }
    }

    void ReleaseLevels()
    {
        foreach (var level in _levels)
            _gl.DeleteTexture(level.Texture);
        _levels.Clear();
    }

    public void Dispose()
    {
        ReleaseLevels();
        _gl.DeleteFramebuffer(_framebuffer);
        _prefilter.Dispose();
        _down.Dispose();
        _up.Dispose();
    }
}
