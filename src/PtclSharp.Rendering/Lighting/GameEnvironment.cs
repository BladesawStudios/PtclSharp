using System.Numerics;
using System.Runtime.InteropServices;

namespace PtclSharp.Rendering.Lighting;

/// <summary>
/// The lighting the game feeds its custom particle shaders through <c>sysCustomShaderUniformBlock0-3</c>. The game's callbacks fill those blocks from its
/// live environment (time of day, lights, fog, shadows), which an effect file does not carry, so this builds the same layouts from a settable
/// daylight instead.
/// </summary>
/// <remarks>
/// The layouts come from the game's custom shader callbacks: "Basic" is 0x240 bytes, "Basic2" 0x60 bytes and the environment block 0x270 bytes, which the
/// shaders read as block 1. A host with a real environment (WildRenderingSharp) can set the same properties from it.
/// </remarks>
public sealed class GameEnvironment
{
    /// <summary>Direction from the surface to the sun, in world space (Y up).</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(0.35f, 0.8f, 0.45f));

    public Vector3 SunColor { get; set; } = new(1.0f, 0.95f, 0.85f);

    public Vector3 Ambient { get; set; } = new(0.30f, 0.32f, 0.38f);

    public Vector3 FogColor { get; set; } = new(0.60f, 0.66f, 0.76f);

    /// <summary>Largest blend towards <see cref="FogColor"/> the distance and height fog can reach; 0 is no fog.</summary>
    public float FogAmount { get; set; }

    /// <summary>The bytes of custom shader block <paramref name="index"/> (0 to 3).</summary>
    public byte[] BuildBlock(int index)
    {
        float[] rows = new float[4 * 64];
        switch (index)
        {
            case 1: FillEnvironment(rows); break;
            case 2: FillBasic2(rows); break;
            default: FillBasic(rows); break;
        }
        return MemoryMarshal.AsBytes(rows.AsSpan()).ToArray();
    }

    static void Row(float[] rows, int row, float x, float y, float z, float w)
    {
        rows[row * 4] = x;
        rows[(row * 4) + 1] = y;
        rows[(row * 4) + 2] = z;
        rows[(row * 4) + 3] = w;
    }

    // The 39-vector environment block. Rows 3 to 21 are light and camera matrices; leaving them zero keeps the optional point lights off.
    void FillEnvironment(float[] rows)
    {
        Row(rows, 1, 0f, 0f, 0f, 1f);                                         // fog/occlusion parameter, scene value, time-of-day curve, 1 - shadow
        Row(rows, 2, SunDirection.X, SunDirection.Y, SunDirection.Z, 1f);
        Row(rows, 22, 0.002f, 0f, 1f, 1f);                                    // distance fog: x is 1 / range, y is the start
        Row(rows, 23, 1f, 1f, 0f, 1f);                                        // x is the falloff exponent, z the largest amount
        Row(rows, 24, 0.002f, 1f, 1f, 1f);                                    // x is 1 / fog distance, w the falloff exponent
        Row(rows, 25, FogColor.X, FogColor.Y, FogColor.Z, FogAmount);         // fog colour, and the most the distance fog can blend
        Row(rows, 26, FogColor.X, FogColor.Y, FogColor.Z, FogAmount);         // the same for the height fog
        Row(rows, 27, 1f, 1f, 1f, 1f);
        Row(rows, 28, SunDirection.X, SunDirection.Y, SunDirection.Z, 0f);
        Row(rows, 29, 1f, 1f, 1f, 1f);
        Row(rows, 30, SunColor.X * 1.5f, SunColor.Y * 1.5f, SunColor.Z * 1.5f, 1f);
        Row(rows, 31, SunColor.X * 1.5f, SunColor.Y * 1.5f, SunColor.Z * 1.5f, 1f);
        Row(rows, 32, Ambient.X, Ambient.Y, Ambient.Z, 1f);
        Row(rows, 34, 1f, 1f, 0f, 0f);
        Row(rows, 37, Ambient.X, Ambient.Y, Ambient.Z, 1f);
        Row(rows, 38, Ambient.X, Ambient.Y, Ambient.Z, 1f);
    }

    // The 6-vector block with the directional light the vertex shaders use.
    void FillBasic2(float[] rows)
    {
        Row(rows, 0, 0f, 0f, 1f, 1f);
        Row(rows, 1, SunDirection.X, 0f, SunDirection.Z, 1f);
        Row(rows, 2, 1f, 1f, 1f, 0f);
        Row(rows, 3, SunColor.X, SunColor.Y, SunColor.Z, 0f);
        Row(rows, 4, Ambient.X, Ambient.Y, Ambient.Z, 0f);
        Row(rows, 5, 1f, 1f, 1f, 1f);
    }

    // The 36-vector "Basic" block.
    void FillBasic(float[] rows)
    {
        Row(rows, 0, 0f, 0f, 1f, 1f);
        Row(rows, 1, SunDirection.X, 0f, SunDirection.Z, 0f);
        for (int row = 22; row < 30; row++)
            Row(rows, row, Ambient.X, Ambient.Y, Ambient.Z, 1f);
        Row(rows, 30, SunColor.X * 4f, 1f, 1f, 1f);
        Row(rows, 31, SunColor.X * 4f, 1f, 1f, 1f);
        Row(rows, 32, 0f, 0f, 0f, 1f);
    }
}
