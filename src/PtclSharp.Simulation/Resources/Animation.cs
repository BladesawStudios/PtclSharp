using System.Numerics;

namespace PtclSharp.Simulation.Resources;

/// <summary>An EA* chunk: emitter-time keyframes (x, y, z, time) evaluated in frames (CalculateEmitterKeyFrameAnimation 0x7100adf920).</summary>
public sealed class EmitterAnimTrack
{
    public bool Enabled;
    public bool Loop;
    public Vector4[] Keys = [];

    /// <summary>Evaluates at <paramref name="time"/>. Returns false when the track has no keys (the caller keeps its default).</summary>
    public bool Evaluate(float time, ref Vector3 value, ref bool animationOver)
    {
        int n = Keys.Length;
        if (n == 0) return false;
        if (n == 1) { value = new Vector3(Keys[0].X, Keys[0].Y, Keys[0].Z); return true; }
        float t = time;
        if (Loop)
        {
            float max = Keys[n - 1].W;
            if (max > 0) t %= max;
        }
        if (t < Keys[0].W) { value = new Vector3(Keys[0].X, Keys[0].Y, Keys[0].Z); return true; }
        if (Keys[n - 1].W <= t)
        {
            value = new Vector3(Keys[n - 1].X, Keys[n - 1].Y, Keys[n - 1].Z);
            animationOver = true;
            return true;
        }
        for (int i = 0; i < n - 1; i++)
        {
            float t1 = Keys[i].W, t2 = Keys[i + 1].W;
            if (t1 <= t && t < t2)
            {
                Vector3 a = new(Keys[i].X, Keys[i].Y, Keys[i].Z), b = new(Keys[i + 1].X, Keys[i + 1].Y, Keys[i + 1].Z);
                value = a + (b - a) * ((t - t1) / (t2 - t1));
                return true;
            }
        }
        return true;
    }
}

/// <summary>The 8-key parameter set used by fields (ResAnim8KeyParamSet): enable, loop, start-random, key count, loop frames, keys.</summary>
public sealed class Anim8Key
{
    public bool Enable;
    public bool Loop;
    public bool StartRandom;
    public int KeyCount;
    public int LoopNum;
    public Vector4[] Keys = new Vector4[8];

    /// <summary>CalculateField8KeyAnim (0x7100adfa5c / vfx_ParticleBehavior.cpp).</summary>
    public Vector3 Evaluate(float time, float randomX, float emitterParticleLife)
    {
        if (KeyCount == 0) return Vector3.Zero;
        if (KeyCount == 1) return new Vector3(Keys[0].X, Keys[0].Y, Keys[0].Z);
        float local;
        if (Loop && LoopNum > 0)
        {
            local = time + (StartRandom ? 1f : 0f) * randomX * LoopNum;
            local = (local % LoopNum) / LoopNum;
        }
        else local = time / emitterParticleLife;
        int last = KeyCount - 1;
        if (Keys[last].W <= local) return new Vector3(Keys[last].X, Keys[last].Y, Keys[last].Z);
        if (local < Keys[0].W) return new Vector3(Keys[0].X, Keys[0].Y, Keys[0].Z);
        for (int i = 0; i < KeyCount - 1; i++)
        {
            float t1 = Keys[i].W, t2 = Keys[i + 1].W;
            if (t1 <= local && local < t2)
            {
                Vector3 a = new(Keys[i].X, Keys[i].Y, Keys[i].Z), b = new(Keys[i + 1].X, Keys[i + 1].Y, Keys[i + 1].Z);
                return a + (b - a) * ((local - t1) / (t2 - t1));
            }
        }
        return Vector3.Zero;
    }
}
