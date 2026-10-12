using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Post;

/// <summary>A half-float colour texture with a depth/stencil renderbuffer, sized to the viewport; the target a scene is drawn into before it is tonemapped.</summary>
public sealed class HdrTarget : IDisposable
{
    readonly GL _gl;
    uint _framebuffer, _colorTexture, _depthBuffer;

    public HdrTarget(GL gl) => _gl = gl;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public uint Framebuffer => _framebuffer;

    public uint ColorTexture => _colorTexture;

    /// <summary>Makes the target <paramref name="width"/> by <paramref name="height"/>, reallocating only when the size changed.</summary>
    public unsafe void Resize(int width, int height)
    {
        if (_framebuffer != 0 && width == Width && height == Height)
            return;
        Release();
        (Width, Height) = (width, height);

        _colorTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.HalfFloat, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _depthBuffer = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);

        _framebuffer = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthBuffer);
    }

    /// <summary>Binds the target for drawing and sets the viewport to cover it.</summary>
    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    void Release()
    {
        if (_framebuffer == 0)
            return;
        _gl.DeleteFramebuffer(_framebuffer);
        _gl.DeleteTexture(_colorTexture);
        _gl.DeleteRenderbuffer(_depthBuffer);
        _framebuffer = _colorTexture = _depthBuffer = 0;
    }

    public void Dispose() => Release();
}
