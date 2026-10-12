using System.Numerics;
using PtclSharp.Rendering.Lighting;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Assets;

/// <summary>
/// The textures the game supplies at runtime that an effect file cannot: flat fallbacks for missing images and lookup tables, and a sky cube map for the
/// environment sampler the lit particle shaders read.
/// </summary>
public sealed unsafe class ProceduralTextures : IDisposable
{
    const int CubeSize = 32;

    readonly GL _gl;
    readonly GameEnvironment _environment;
    readonly Dictionary<uint, uint> _solids = [];
    readonly Dictionary<TextureTarget, uint> _arrays = [];
    uint _sky;

    public ProceduralTextures(GL gl, GameEnvironment environment)
    {
        _gl = gl;
        _environment = environment;
    }

    public uint White => Solid(255, 255, 255, 255);

    public uint Black => Solid(0, 0, 0, 255);

    /// <summary>Mid grey, standing in for the game's environment ramps.</summary>
    public uint Gray => Solid(153, 153, 153, 255);

    /// <summary>A 1x1 texture, created once per colour.</summary>
    public uint Solid(byte r, byte g, byte b, byte a)
    {
        uint key = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
        if (_solids.TryGetValue(key, out uint known))
            return known;
        uint texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture15); // uploads must not disturb the units a draw has set up
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        byte* pixel = stackalloc byte[4] { r, g, b, a };
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        return _solids[key] = texture;
    }

    /// <summary>A white 1x1x1 texture for 3D and array samplers.</summary>
    public uint White3D(TextureTarget target)
    {
        if (_arrays.TryGetValue(target, out uint known))
            return known;
        uint texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture15);
        _gl.BindTexture(target, texture);
        byte* pixel = stackalloc byte[4] { 255, 255, 255, 255 };
        _gl.TexImage3D(target, 0, InternalFormat.Rgba8, 1, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        return _arrays[target] = texture;
    }

    /// <summary>A daylight sky: blue overhead, dull ground below and a sun spot, from the environment's sun.</summary>
    public uint Sky
    {
        get
        {
            if (_sky != 0)
                return _sky;
            _sky = _gl.GenTexture();
            _gl.ActiveTexture(TextureUnit.Texture15);
            _gl.BindTexture(TextureTarget.TextureCubeMap, _sky);
            byte[] pixels = new byte[CubeSize * CubeSize * 4];
            for (int face = 0; face < 6; face++)
            {
                FillFace(pixels, face);
                fixed (byte* p = pixels)
                    _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, 0, InternalFormat.Rgba8, CubeSize, CubeSize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
            _gl.GenerateMipmap(TextureTarget.TextureCubeMap);
            return _sky;
        }
    }

    void FillFace(byte[] pixels, int face)
    {
        for (int y = 0; y < CubeSize; y++)
            for (int x = 0; x < CubeSize; x++)
            {
                float u = (((x + 0.5f) / CubeSize) * 2f) - 1f, v = (((y + 0.5f) / CubeSize) * 2f) - 1f;
                Vector3 direction = Vector3.Normalize(face switch
                {
                    0 => new Vector3(1, -v, -u), 1 => new Vector3(-1, -v, u), 2 => new Vector3(u, 1, v),
                    3 => new Vector3(u, -1, -v), 4 => new Vector3(u, -v, 1), _ => new Vector3(-u, -v, -1)
                });
                float t = Math.Clamp((direction.Y * 0.5f) + 0.5f, 0f, 1f);
                Vector3 sky = Vector3.Lerp(new Vector3(0.30f, 0.30f, 0.28f), new Vector3(0.55f, 0.68f, 0.95f), MathF.Pow(t, 0.7f));
                sky += _environment.SunColor * (MathF.Pow(Math.Max(Vector3.Dot(direction, _environment.SunDirection), 0f), 48f) * 0.8f);
                int i = ((y * CubeSize) + x) * 4;
                pixels[i] = (byte)(Math.Clamp(sky.X, 0f, 1f) * 255);
                pixels[i + 1] = (byte)(Math.Clamp(sky.Y, 0f, 1f) * 255);
                pixels[i + 2] = (byte)(Math.Clamp(sky.Z, 0f, 1f) * 255);
                pixels[i + 3] = 255;
            }
    }

    public void Dispose()
    {
        foreach (uint texture in _solids.Values.Concat(_arrays.Values))
            _gl.DeleteTexture(texture);
        if (_sky != 0)
            _gl.DeleteTexture(_sky);
    }
}
