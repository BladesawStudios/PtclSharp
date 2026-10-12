using System.Numerics;

namespace PtclSharp.Simulation;

/// <summary>
/// Matrix helpers in the engine's convention: row vectors (v * M), basis axes in the rows, translation in the fourth row. This is
/// also System.Numerics' convention, so Matrix4x4 stands in for nn::util::Matrix4x3f.
/// </summary>
public static class VfxMath
{
    public const float TwoPi = MathF.PI * 2f;

    /// <summary>MatrixCreateSrtXyz: scale, then X, Y, Z rotation (radians), then translation.</summary>
    public static Matrix4x4 CreateSrtXyz(Vector3 scale, Vector3 rotate, Vector3 translate)
    {
        Matrix4x4 m = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationX(rotate.X) * Matrix4x4.CreateRotationY(rotate.Y)
            * Matrix4x4.CreateRotationZ(rotate.Z);
        m.Translation = translate;
        return m;
    }

    public static Vector3 TransformNormal(Vector3 v, in Matrix4x4 m) => Vector3.TransformNormal(v, m);
    public static Vector3 Transform(Vector3 v, in Matrix4x4 m) => Vector3.Transform(v, m);

    public static Matrix4x4 InvertAffine(in Matrix4x4 m) => Matrix4x4.Invert(m, out Matrix4x4 inv) ? inv : Matrix4x4.Identity;

    /// <summary>MakrRtMatrix: the same matrix with every axis normalized (scale removed).</summary>
    public static Matrix4x4 MakeRt(in Matrix4x4 srt)
    {
        Matrix4x4 rt = Matrix4x4.Identity;
        Vector3 x = new(srt.M11, srt.M12, srt.M13), y = new(srt.M21, srt.M22, srt.M23), z = new(srt.M31, srt.M32, srt.M33);
        x = x.Length() > 0 ? Vector3.Normalize(x) : Vector3.Zero;
        y = y.Length() > 0 ? Vector3.Normalize(y) : Vector3.Zero;
        z = z.Length() > 0 ? Vector3.Normalize(z) : Vector3.Zero;
        rt.M11 = x.X; rt.M12 = x.Y; rt.M13 = x.Z;
        rt.M21 = y.X; rt.M22 = y.Y; rt.M23 = y.Z;
        rt.M31 = z.X; rt.M32 = z.Y; rt.M33 = z.Z;
        rt.Translation = srt.Translation;
        return rt;
    }

    public static float SafeSqrt(float v) => v > 0 ? MathF.Sqrt(v) : 0f;

    /// <summary>Rotation taking <paramref name="from"/> to <paramref name="to"/> (both nonzero), as a matrix.</summary>
    public static Matrix4x4 RotationBetween(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        if (to.LengthSquared() < 1e-12f) return Matrix4x4.Identity;
        to = Vector3.Normalize(to);
        float d = Vector3.Dot(from, to);
        if (d > 0.999999f) return Matrix4x4.Identity;
        if (d < -0.999999f)
        {
            Vector3 axis = MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
            axis = Vector3.Normalize(Vector3.Cross(from, axis));
            return Matrix4x4.CreateFromAxisAngle(axis, MathF.PI);
        }
        Vector3 a = Vector3.Normalize(Vector3.Cross(from, to));
        return Matrix4x4.CreateFromAxisAngle(a, MathF.Acos(Math.Clamp(d, -1, 1)));
    }
}
