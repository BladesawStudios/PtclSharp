using System.Numerics;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// The camera and scene inputs one particle pass needs. Matrices use System.Numerics' row-vector layout; the projection is OpenGL's (clip z runs from
/// -w to w), which is what the game's particle shaders expect. Everything is drawn the way the game draws, with the window origin at the top left.
/// </summary>
public readonly struct ParticleView
{
    public ParticleView()
    {
    }

    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public float Near { get; init; }

    public float Far { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>
    /// A single-channel float texture holding the scene's view-space distance, with the same top-left origin, for soft particles; 0 when there is no scene
    /// to fade against. The distance in world units is <c>texel * SceneDepthScale + SceneDepthOffset</c>.
    /// </summary>
    public uint SceneLinearDepth { get; init; }

    public float SceneDepthScale { get; init; } = 1f;

    public float SceneDepthOffset { get; init; }

    /// <summary>An OpenGL perspective projection (Y up, looking down -Z).</summary>
    public static Matrix4x4 Perspective(float fovY, float aspect, float near, float far)
    {
        float y = 1f / MathF.Tan(fovY * 0.5f);
        return new Matrix4x4(
            y / aspect, 0, 0, 0,
            0, y, 0, 0,
            0, 0, -(far + near) / (far - near), -1,
            0, 0, -2f * far * near / (far - near), 0);
    }
}
