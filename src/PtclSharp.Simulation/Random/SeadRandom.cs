namespace PtclSharp.Simulation.Random;

/// <summary>sead::Random: xorshift128, as the BotW executable's 0x7100b20838..0x7100b20920 (init(seed), getU32).</summary>
public sealed class SeadRandom
{
    private uint _x, _y, _z, _w;

    public SeadRandom(uint seed) => Init(seed);

    public void Init(uint seed)
    {
        _x = 0x6c078965u * (seed ^ (seed >> 30)) + 1;
        _y = 0x6c078965u * (_x ^ (_x >> 30)) + 2;
        _z = 0x6c078965u * (_y ^ (_y >> 30)) + 3;
        _w = 0x6c078965u * (_z ^ (_z >> 30)) + 4;
    }

    public uint GetU32()
    {
        uint t = _x ^ (_x << 11);
        _x = _y; _y = _z; _z = _w;
        _w = t ^ (t >> 8) ^ _w ^ (_w >> 19);
        return _w;
    }

    /// <summary>[0, 1): the mantissa trick the table builder uses ((u32 >> 9 | 0x3f800000) - 1).</summary>
    public float GetFloat() => BitConverter.UInt32BitsToSingle((GetU32() >> 9) | 0x3f800000u) - 1.0f;
}
