using System.Buffers.Binary;
using System.Numerics;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

/// <summary>
/// CPU evaluation of the per-particle scale, colour and rotation curves (EmitterCalculator::CalculateParticleScaleVecFromTime and
/// friends). The GPU does the same in the vertex shader; the CPU copy exists for child-emitter inheritance and for tools.
/// Reads the emitter's static block (BotW layout offsets live in the BotW reader; this class uses the block, which is shared in shape).
/// </summary>
public static class ParticleEval
{
    private static float F(byte[] b, int at) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at));
    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));

    private static Vector3 Key8(byte[] b, int table, int keyCount, float random, float time, float loopRate, float isLoopRandom, float life)
    {
        if (keyCount <= 0) return Vector3.One;
        keyCount = Math.Min(keyCount, 8);
        Vector4 K(int i) => new(F(b, table + (i * 16)), F(b, table + (i * 16) + 4), F(b, table + (i * 16) + 8), F(b, table + (i * 16) + 12));
        if (keyCount == 1) { var k = K(0); return new Vector3(k.X, k.Y, k.Z); }
        float local;
        if (loopRate > 0) { local = time + (isLoopRandom * random * loopRate); local = (local % loopRate) / loopRate; }
        else local = time / life;
        Vector4 last = K(keyCount - 1), first = K(0);
        if (last.W <= local) return new Vector3(last.X, last.Y, last.Z);
        if (local < first.W) return new Vector3(first.X, first.Y, first.Z);
        for (int i = 0; i < keyCount - 1; i++)
        {
            Vector4 a = K(i), c = K(i + 1);
            if (a.W <= local && local < c.W)
            {
                Vector3 va = new(a.X, a.Y, a.Z), vc = new(c.X, c.Y, c.Z);
                return va + ((vc - va) * ((local - a.W) / (c.W - a.W)));
            }
        }
        return Vector3.Zero;
    }

    private static float Wave(byte[] b, float time, int axis, Vector4 random)
    {
        int type = b[0x9ef] >> 4;
        float amp = F(b, 0xe0 + (axis * 4)), cycle = F(b, 0xe8 + (axis * 4)), phRnd = F(b, 0xf0 + (axis * 4)), phInit = F(b, 0xf8 + (axis * 4));
        float t = ((time + phInit) / cycle) + (phRnd * random.X);
        float Fract(float x) => x - MathF.Floor(x);
        return type switch
        {
            0 => 1f - (((MathF.Cos(t * MathF.PI * 2f) + 1f) * 0.5f) * amp),
            1 => MathF.Abs(1f - (Fract(t) * amp)),
            2 => MathF.Abs(1f - ((1f - (0.5f <= Fract(t) ? 1f : 0f)) * amp)),
            _ => 1f
        };
    }

    public static Vector3 Scale(EmitterResource r, Vector3 initial, Vector4 random, float life, float time)
    {
        byte[] b = r.FullData;
        if (b.Length < 0xA88) return initial;
        int keys = Math.Clamp(I(b, 0x70), 0, 8);
        Vector3 s = initial;
        if (keys > 1)
        {
            float loopRate = F(b, 0x90);
            s *= Key8(b, 0x600, keys, random.X, time, loopRate, F(b, 0xa4), life);
        }
        else
        {
            var k = new Vector3(F(b, 0x600), F(b, 0x604), F(b, 0x608));
            s *= k;
        }
        if (b[0x9ed] != 0)
        {
            float fx = Wave(b, time, 0, random);
            float fy = b[0x9ee] != 0 ? Wave(b, time, 1, random) : fx;
            s.X *= fx; s.Y *= fy;
        }
        return s;
    }

    private static Vector4 Color(EmitterResource r, int colorTable, int alphaTable, int colorCountAt, int alphaCountAt, int colorModeAt, int alphaModeAt,
        int colorConstAt, int alphaConstAt, int colorRateAt, int alphaRateAt, int colorRandAt, int alphaRandAt,
        Vector4 random, Vector4 emitterColor, Vector3 emitterAnimColor, float emitterAnimAlpha, float life, float time, bool useColorScale)
    {
        byte[] b = r.FullData;
        Vector4 c = new(F(b, colorConstAt), F(b, colorConstAt + 4), F(b, colorConstAt + 8), F(b, alphaConstAt));
        int cKeys = Math.Clamp(I(b, colorCountAt), 0, 8), aKeys = Math.Clamp(I(b, alphaCountAt), 0, 8);
        if (b[colorModeAt] == 2 && cKeys > 0)
        {
            int idx = Math.Min((int)(random.X * cKeys), cKeys - 1);
            c.X = F(b, colorTable + (idx * 16)); c.Y = F(b, colorTable + (idx * 16) + 4); c.Z = F(b, colorTable + (idx * 16) + 8);
        }
        else if (b[colorModeAt] == 1 && cKeys > 0)
        {
            Vector3 v = Key8(b, colorTable, cKeys, random.X, time, F(b, colorRateAt), F(b, colorRandAt), life);
            c.X = v.X; c.Y = v.Y; c.Z = v.Z;
        }
        if (b[alphaModeAt] == 1 && aKeys > 0)
            c.W = Key8(b, alphaTable, aKeys, random.X, time, F(b, alphaRateAt), F(b, alphaRandAt), life).X;
        float colorScale = useColorScale ? F(b, 0x3b0) : 1f;
        c.X *= emitterColor.X * emitterAnimColor.X * colorScale;
        c.Y *= emitterColor.Y * emitterAnimColor.Y * colorScale;
        c.Z *= emitterColor.Z * emitterAnimColor.Z * colorScale;
        c.W *= emitterColor.W * emitterAnimAlpha;
        if (b[0x9ec] != 0) c.W = Math.Clamp(c.W * Wave(b, time, 0, random), 0, 1);
        return c;
    }

    public static Vector4 Color0(EmitterResource r, Vector4 random, Vector4 emitterColor, Vector3 emitterAnimColor, float emitterAnimAlpha, float life, float time)
        => Color(r, 0x3c0, 0x440, 0x60, 0x64, 0x9a4, 0x9a6, 0x9a8, 0x9b4, 0x80, 0x84, 0x94, 0x98, random, emitterColor, emitterAnimColor, emitterAnimAlpha, life, time, true);

    public static Vector4 Color1(EmitterResource r, Vector4 random, Vector4 emitterColor, Vector3 emitterAnimColor, float emitterAnimAlpha, float life, float time)
        => Color(r, 0x4c0, 0x540, 0x68, 0x6c, 0x9a5, 0x9a7, 0x9b8, 0x9c4, 0x88, 0x8c, 0x9c, 0xa0, random, emitterColor, emitterAnimColor, emitterAnimAlpha, life, time, false);

    /// <summary>CalculateRotationMatrix (the Euler angles; the matrix itself is built where it is used).</summary>
    public static Vector3 Rotation(EmitterResource r, Vector3 initial, Vector4 random, float time)
    {
        Vector3 rAdd = r.RotAdd, rAddRand = r.RotAddRand;
        float pr = random.X, pg = random.Y, pb = random.Z;
        float tx = (pr + pg) / 2, ty = (pg + pb) / 2, tz = (pb + pr) / 2;
        float vx = rAdd.X + (tx * rAddRand.X), vy = rAdd.Y + (ty * rAddRand.Y), vz = rAdd.Z + (tz * rAddRand.Z);
        float ix = initial.X, iy = initial.Y, iz = initial.Z;
        if (r.RotRevRandX && pg >= 0.5f) { vx = -vx; ix = -ix; }
        if (r.RotRevRandY && pb >= 0.5f) { vy = -vy; iy = -iy; }
        if (r.RotRevRandZ && pr >= 0.5f) { vz = -vz; iz = -iz; }
        float rotX = ix + (pr * r.RotInitRand.X), rotY = iy + (pg * r.RotInitRand.Y), rotZ = iz + (pb * r.RotInitRand.Z);
        float regist = r.RotRegist;
        float rotTime = regist == 1f ? time : (1f - MathF.Pow(regist, time)) / (1f - regist);
        return new Vector3((vx * rotTime) + rotX, (vy * rotTime) + rotY, (vz * rotTime) + rotZ);
    }
}
