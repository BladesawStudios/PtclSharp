using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Post;

/// <summary>Draws an HDR image (plus optional bloom) into the bound framebuffer: exposure, an ACES curve and gamma.</summary>
public sealed class Tonemap : IDisposable
{
    readonly GL _gl;
    readonly FullscreenPass _pass;
    readonly uint _blackTexture;

    public Tonemap(GL gl)
    {
        _gl = gl;
        _pass = new FullscreenPass(gl, "Tonemap.frag");
        _blackTexture = CreateBlack();
    }

    /// <summary>
    /// Draws <paramref name="scene"/> to the current framebuffer; <paramref name="bloom"/> may be 0 for none. <paramref name="flipVertical"/> is for a scene drawn with
    /// the game's window origin (top left).
    /// </summary>
    public void Draw(uint scene, uint bloom, float exposure, float bloomIntensity, bool flipVertical = false)
    {
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.ScissorTest);
        _pass.Use();
        _pass.SetTexture("scene", 0, scene);
        _pass.SetTexture("bloom", 1, bloom != 0 ? bloom : _blackTexture);
        _pass.SetFloat("exposure", exposure);
        _pass.SetFloat("bloomIntensity", bloom != 0 ? bloomIntensity : 0f);
        _pass.SetFloat("flipVertical", flipVertical ? 1f : 0f);
        _pass.Draw();
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    unsafe uint CreateBlack()
    {
        uint texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        byte* pixel = stackalloc byte[4] { 0, 0, 0, 255 };
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        return texture;
    }

    public void Dispose()
    {
        _pass.Dispose();
        _gl.DeleteTexture(_blackTexture);
    }
}
