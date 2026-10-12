using System.Numerics;

namespace PtclSharp.Simulation.Random;

/// <summary>The two 512-entry tables nn::vfx::detail::Random::Initialize builds (0x7100acb1f8), seeded with 12345679.</summary>
public static class RandomTables
{
    public const int Size = 512;
    public static readonly Vector3[] Vec3 = new Vector3[Size];
    public static readonly Vector3[] Normalized = new Vector3[Size];

    static RandomTables()
    {
        var rnd = new SeadRandom(12345679);
        for (int i = 0; i < Size; i++)
        {
            Vec3[i] = new Vector3(rnd.GetFloat() * 2 - 1, rnd.GetFloat() * 2 - 1, rnd.GetFloat() * 2 - 1);
            var n = new Vector3(rnd.GetFloat() * 2 - 1, rnd.GetFloat() * 2 - 1, rnd.GetFloat() * 2 - 1);
            Normalized[i] = n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.Zero;
        }
    }
}

/// <summary>
/// nn::vfx::detail::Random: a 32-bit LCG (x * 0x41c64e6d + 0x3039) plus two table cursors (Random::SetSeed 0x7100acb434).
/// The float is the pre-advance state times 2^-32, as in CreateResMatrix 0x7100ad6560 and InitializeParticle 0x7100ada3cc.
/// </summary>
public struct VfxRandom
{
    private ushort _vecIndex, _normIndex;
    private uint _value;

    public void SetSeed(uint seed)
    {
        _vecIndex = (ushort)seed;
        _normIndex = (ushort)(seed >> 16);
        _value = seed;
    }

    public float GetFloat()
    {
        float r = (float)_value * (1.0f / 4294967296.0f);
        _value = unchecked(_value * 0x41c64e6du + 0x3039u);
        return r;
    }

    /// <summary>[0, max): (state * max) >> 32.</summary>
    public int GetInteger(int max)
    {
        int r = (int)(((ulong)_value * (uint)max) >> 32);
        _value = unchecked(_value * 0x41c64e6du + 0x3039u);
        return r;
    }

    public uint GetUnsignedIntegerDirect()
    {
        uint r = _value;
        _value = unchecked(_value * 0x41c64e6du + 0x3039u);
        return r;
    }

    public Vector3 GetVector3() => RandomTables.Vec3[_vecIndex++ & 0x1ff];
    public Vector3 GetNormalizedVec3() => RandomTables.Normalized[_normIndex++ & 0x1ff];
}
