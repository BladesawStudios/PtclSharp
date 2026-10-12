using System.Numerics;
using System.Reflection;

namespace PtclSharp.Simulation.Runtime;

/// <summary>The 32x32x32 signed-byte curl-noise vector field the engine bakes in (vfx_CurlNoiseDataTable.ipp), sampled with wrap-around.</summary>
public static class CurlNoiseTable
{
    private const int N = 32;
    private static readonly sbyte[] Data = Load();

    private static sbyte[] Load()
    {
        using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("CurlNoise.bin")!;
        var bytes = new byte[N * N * N * 3];
        s.ReadExactly(bytes);
        var d = new sbyte[bytes.Length];
        for (int i = 0; i < d.Length; i++) d[i] = unchecked((sbyte)bytes[i]);
        return d;
    }

    private static Vector3 At(int x, int y, int z)
    {
        int i = ((((z & 31) * N) + (y & 31)) * N + (x & 31)) * 3;
        return new Vector3(Data[i], Data[i + 1], Data[i + 2]) / 127f;
    }

    public static Vector3 Nearest(Vector3 p) => At((int)p.X, (int)p.Y, (int)p.Z);

    public static Vector3 Interpolate(Vector3 p)
    {
        int x0 = (int)MathF.Floor(p.X), y0 = (int)MathF.Floor(p.Y), z0 = (int)MathF.Floor(p.Z);
        float fx = p.X - x0, fy = p.Y - y0, fz = p.Z - z0;
        Vector3 c00 = Vector3.Lerp(At(x0, y0, z0), At(x0 + 1, y0, z0), fx), c10 = Vector3.Lerp(At(x0, y0 + 1, z0), At(x0 + 1, y0 + 1, z0), fx);
        Vector3 c01 = Vector3.Lerp(At(x0, y0, z0 + 1), At(x0 + 1, y0, z0 + 1), fx), c11 = Vector3.Lerp(At(x0, y0 + 1, z0 + 1), At(x0 + 1, y0 + 1, z0 + 1), fx);
        return Vector3.Lerp(Vector3.Lerp(c00, c10, fy), Vector3.Lerp(c01, c11, fy), fz);
    }
}
