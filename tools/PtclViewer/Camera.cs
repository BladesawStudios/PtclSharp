using System.Numerics;

namespace PtclViewer;

/// <summary>An orbit camera in the games' right-handed, Y-up world.</summary>
public sealed class Camera
{
    public Vector3 Target = new(0, 1, 0);
    public float Yaw = 0.6f;           // radians around Y
    public float Pitch = 0.25f;        // radians above the horizon
    public float Distance = 8f;
    public float FovY = 45f * MathF.PI / 180f;
    public float Near = 0.1f, Far = 2000f;

    public Vector3 Position
    {
        get
        {
            float cp = MathF.Cos(Pitch);
            return Target + new Vector3(MathF.Sin(Yaw) * cp, MathF.Sin(Pitch), MathF.Cos(Yaw) * cp) * Distance;
        }
    }

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);

    /// <summary>An OpenGL perspective projection, the convention the game's particle shaders expect.</summary>
    public Matrix4x4 Projection(float aspect) => PtclSharp.Rendering.Drawing.ParticleView.Perspective(FovY, aspect, Near, Far);

    public void Orbit(float dx, float dy)
    {
        Yaw -= dx * 0.01f;
        Pitch = Math.Clamp(Pitch + (dy * 0.01f), -1.5f, 1.5f);
    }

    public void Zoom(float amount) => Distance = Math.Clamp(Distance * MathF.Pow(0.9f, amount), 0.2f, 500f);

    public void Pan(float dx, float dy)
    {
        Matrix4x4.Invert(View, out Matrix4x4 inv);
        Vector3 right = new(inv.M11, inv.M12, inv.M13), up = new(inv.M21, inv.M22, inv.M23);
        Target += ((-right * dx) + (up * dy)) * (Distance * 0.0015f);
    }
}
