using PtclSharp.Rendering.Post;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// The scene images the particle shaders sample: the scene distance for soft particles and the colour drawn so far for refraction. Both are prepared
/// from what the host drew before the particles.
/// </summary>
internal sealed unsafe class SceneInputs : IDisposable
{
    readonly GL _gl;
    readonly FullscreenPass _depthPass;
    readonly uint _depthFramebuffer, _colorFramebuffer;
    uint _depthTexture, _colorTexture;
    int _depthWidth, _depthHeight, _colorWidth, _colorHeight;
    int _hostFramebuffer;
    bool _colorCaptured;

    public SceneInputs(GL gl)
    {
        _gl = gl;
        _depthPass = new FullscreenPass(gl, "ParticleDepth.frag");
        _depthFramebuffer = gl.GenFramebuffer();
        _colorFramebuffer = gl.GenFramebuffer();
    }

    /// <summary>The texture the depth sampler reads for this view: the scene distance divided by the shaders' scale, or null when there is no scene depth.</summary>
    public uint? PrepareDepth(in ParticleView view)
    {
        _gl.GetInteger(GetPName.DrawFramebufferBinding, out _hostFramebuffer);
        _colorCaptured = false;
        if (view.SceneLinearDepth == 0)
            return null;

        Allocate(ref _depthTexture, ref _depthWidth, ref _depthHeight, view.Width, view.Height, InternalFormat.R32f);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFramebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _depthTexture, 0);
        _gl.Viewport(0, 0, (uint)view.Width, (uint)view.Height);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _depthPass.Use();
        _depthPass.SetTexture("linearDepth", 0, view.SceneLinearDepth);
        _depthPass.SetFloat("distanceScale", view.SceneDepthScale);
        _depthPass.SetFloat("distanceOffset", view.SceneDepthOffset);
        _depthPass.SetFloat("scale", ViewBlock.DepthScale(view));
        _depthPass.Draw();

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)_hostFramebuffer);
        _gl.Viewport(0, 0, (uint)view.Width, (uint)view.Height);
        return _depthTexture;
    }

    /// <summary>The scene colour as the host had drawn it when the pass began, copied once per pass on first use.</summary>
    public uint Color(int width, int height)
    {
        if (_colorCaptured)
            return _colorTexture;
        Allocate(ref _colorTexture, ref _colorWidth, ref _colorHeight, width, height, InternalFormat.Rgba16f);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)_hostFramebuffer);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _colorFramebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);
        _gl.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)_hostFramebuffer);
        _colorCaptured = true;
        return _colorTexture;
    }

    void Allocate(ref uint texture, ref int currentWidth, ref int currentHeight, int width, int height, InternalFormat format)
    {
        if (texture != 0 && currentWidth == width && currentHeight == height)
            return;
        if (texture != 0)
            _gl.DeleteTexture(texture);
        texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, format, (uint)width, (uint)height, 0,
            format == InternalFormat.R32f ? PixelFormat.Red : PixelFormat.Rgba, format == InternalFormat.R32f ? PixelType.Float : PixelType.HalfFloat, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        (currentWidth, currentHeight) = (width, height);
    }

    public void Dispose()
    {
        _depthPass.Dispose();
        _gl.DeleteFramebuffer(_depthFramebuffer);
        _gl.DeleteFramebuffer(_colorFramebuffer);
        if (_depthTexture != 0)
            _gl.DeleteTexture(_depthTexture);
        if (_colorTexture != 0)
            _gl.DeleteTexture(_colorTexture);
    }
}
