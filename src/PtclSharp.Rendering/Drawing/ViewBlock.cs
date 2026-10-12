using System.Numerics;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// The view uniform block (<c>sysViewUniformBlock</c>) the particle shaders read: view, projection, view-projection and billboard matrices stored as
/// column-vector rows, the eye, the depth constants and the viewport.
/// </summary>
internal static class ViewBlock
{
    /// <summary>Distance a scene with no depth is treated as being at, so the soft fade never touches a particle.</summary>
    const float NoSceneDepth = 1000f;

    public const int Rows = 32;

    /// <summary>Fills the block for <paramref name="view"/>; <paramref name="eye"/> and <paramref name="billboard"/> are what the area loop plugin needs too.</summary>
    public static Vector4[] Build(in ParticleView view, out Vector3 eye, out Matrix4x4 billboard)
    {
        Matrix4x4 viewProjection = view.View * view.Projection;
        Matrix4x4.Invert(view.View, out Matrix4x4 world);
        eye = world.Translation;
        billboard = world;
        billboard.Translation = Vector3.Zero;

        var block = new Vector4[Rows];
        StoreRows(block, 0, view.View);
        StoreRows(block, 4, view.Projection);
        StoreRows(block, 8, viewProjection);
        StoreRows(block, 12, billboard);
        block[16] = new Vector4(-new Vector3(world.M31, world.M32, world.M33), 0f);
        block[17] = new Vector4(eye, 0f);

        // Row 18: w converts a depth value to inverse view distance (1 / (depth * w - y) is a fragment's distance, which is how the shaders read it), z turns
        // the scene minus fragment distance into a difference, and x offsets the scene distance, which is the texel times w when there is a scene.
        float inverseNear = 1f / view.Near, inverseFar = 1f / view.Far;
        block[18] = new Vector4(view.SceneLinearDepth != 0 ? 0f : NoSceneDepth, -inverseNear, -1f, -(inverseNear - inverseFar));
        block[19] = new Vector4(view.Width, view.Height, 1f / view.Width, 1f / view.Height);
        return block;
    }

    /// <summary>The scale a scene-distance texel is multiplied by in the shaders (row 18, w).</summary>
    public static float DepthScale(in ParticleView view) => -((1f / view.Near) - (1f / view.Far));

    // Row j of a column-vector 4x4 is column j of the engine's row-vector matrix.
    static void StoreRows(Span<Vector4> rows, int at, in Matrix4x4 m)
    {
        rows[at] = new Vector4(m.M11, m.M21, m.M31, m.M41);
        rows[at + 1] = new Vector4(m.M12, m.M22, m.M32, m.M42);
        rows[at + 2] = new Vector4(m.M13, m.M23, m.M33, m.M43);
        rows[at + 3] = new Vector4(m.M14, m.M24, m.M34, m.M44);
    }
}
