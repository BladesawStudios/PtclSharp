using System.Numerics;
using PtclSharp.Simulation.Random;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

public sealed partial class Emitter
{
    /// <summary>EmitterCalculator::CalculateParticle: advances every live particle and runs lightweight-child emission.</summary>
    private void CalculateParticles()
    {
        ParticleStore p = Particles;
        int processCount = 0;
        for (int i = 0; i < ParticleCount; i++)
        {
            if (p.CreateId[i] == 0) continue;

            float time = p.GetTime(i, Time);
            float life = p.Life[i];
            if (time >= life) { Plugin?.OnParticleRemove(i, time, life); p.CreateId[i] = 0; continue; }

            Vector3 oldPos = XYZ(p.LocalPos[i]);
            float birthTime = p.CreateTime[i];
            float particleLife = p.Life[i];
            Vector3 localPos = XYZ(p.LocalPos[i]);
            Vector3 localVec = XYZ(p.LocalVec[i]);

            CalculateParticleBehavior(ref localPos, ref localVec, ref birthTime, ref particleLife, i, time);

            p.LocalPos[i] = new Vector4(localPos, particleLife);
            p.LocalVec[i] = new Vector4(localVec, birthTime);
            p.CreateTime[i] = birthTime;
            p.Life[i] = particleLife;

            Vector3 diff = localPos - oldPos;
            const float minVel = 0.0001f;
            if (MathF.Abs(diff.X) > minVel || MathF.Abs(diff.Y) > minVel || MathF.Abs(diff.Z) > minVel)
                p.LocalDiff[i] = new Vector4(diff, p.LocalDiff[i].W);

            Plugin?.OnParticleCalculate(i, time, life);
            EmitLightweightChildren(i);
            processCount++;
        }

        // positions and velocities for the normal child emitters
        if (p.ChildEmitter is not null)
            for (int i = 0; i < ParticleCount; i++)
            {
                if (p.CreateId[i] == 0) continue;
                for (int j = 0; j < Res.ChildCount; j++)
                {
                    Emitter? child = p.ChildEmitter[i][j];
                    if (child is null || !Res.Children[j].EnableEmitterParticle) continue;
                    if (!child.IsAlive || child.ParentEmitterCreateId != EmitterCreateId) continue;

                    child.ParentParticleTime = p.GetTime(i, Time);
                    child.ParentParticleLife = p.Life[i];
                    Vector3 localPos = XYZ(p.LocalPos[i]);
                    if (Res.Follow == FollowType.EmitterFull)
                        child.ParentParticleWorldPos = localPos;
                    else if (Res.Follow == FollowType.None)
                        child.ParentParticleWorldPos = Vector3.Transform(localPos, p.GetBirthMatrix(i));
                    else
                        child.ParentParticleWorldPos = localPos;
                    child.ParentParticleWorldVec = Vector3.TransformNormal(XYZ(p.LocalVec[i]), MatrixRt);
                }
            }

        if (processCount == 0) { ParticleCount = 0; ParticleAttrFillIndex = 0; }
        ParticleProperty_ProcessingCount = processCount;
        Plugin?.PostCalculate();
    }

    internal bool IsAlive { get; set; } = true;

    private static Vector3 XYZ(Vector4 v) => new(v.X, v.Y, v.Z);

    private void EmitLightweightChildren(int i)
    {
        ParticleStore p = Particles;
        for (int k = 0; k < Res.ChildCount; k++)
        {
            Emitter? child = p.ChildEmitter![i][k];
            if (child is null) break;
            if (!child.IsAlive) continue;
            EmitterResource cr = child.Res;
            if (cr.EnableEmitterParticle) continue;
            if (p.CreateId[i] == 0) continue;

            float childTime = p.LightChildTime![i];
            if (child.Time == 0 && !cr.EnableEmitterParticle)
            {
                if (cr.IsEmitterSrtAnim) { child.MatrixSrt = child.EmitterAnimMatrix(false); child.MatrixRt = child.EmitterAnimMatrix(true); }
                else { child.MatrixSrt = child.ResMatrixSrt; child.MatrixRt = child.ResMatrixRt; }
            }

            float parentLife = p.Life[i];
            float emitStart = parentLife * (cr.EmitTiming / 100f);
            if (emitStart > childTime) continue;

            bool done = p.LightChildEmitDone![i][k];
            float emitEnd = cr.IsOneTime ? emitStart + cr.EmitDuration : parentLife;
            if ((cr.IsOneTime || child.IsChild) && emitEnd <= childTime && done) continue;

            Vector3 parentPos = XYZ(p.LocalPos[i]);
            Vector3 parentVec = XYZ(p.LocalVec[i]);
            if (Res.Follow == FollowType.None)
            {
                parentPos = TransformToWorldPos(parentPos, i);
                parentVec = TransformToWorldVec(parentVec, i);
            }
            child.TempWorldPos = parentPos;
            child.TempWorldVec = parentVec;
            child.TempParentIndex = i;

            if (cr.IsEmitDistEnabled && childTime != 0f) child.EmitterLocalVec = XYZ(p.LocalDiff[i]);

            if (child.Res.Follow == FollowType.None && Res.Follow != FollowType.None)
            {
                child.MatrixSrt = child.ResMatrixSrt * MatrixSrt;
                child.MatrixRt = child.ResMatrixRt * MatrixRt;
            }

            float count = p.LightChildEmitCount![i][k], saving = p.LightChildEmitSaving![i][k], interval = p.LightChildEmitInterval![i][k];
            bool emitDone = done;
            child.TryEmitParticle(ref count, ref saving, ref interval, ref emitDone, FrameRate);
            p.LightChildEmitCount[i][k] = count; p.LightChildEmitSaving[i][k] = saving;
            p.LightChildEmitInterval[i][k] = interval; p.LightChildEmitDone[i][k] = emitDone;

            child.TempParentIndex = -1;
            child.TempWorldPos = null;
            child.TempWorldVec = null;
        }
    }

    // ------------------------------------------------------------------ behaviour

    private Vector3 RtForParticle(int index, bool perParticle) => Vector3.Zero;

    /// <summary>EmitterCalculator::CalculateParticleBehavior (BotW 0x7100ae0e34).</summary>
    private void CalculateParticleBehavior(ref Vector3 pos, ref Vector3 vec, ref float birthTime, ref float life, int index, float time)
    {
        ParticleStore p = Particles;
        float frameRate = FrameRate;
        Vector3 posBack = pos, vecBack = vec;

        float rndRatio = p.Scale[index].W * frameRate;
        pos = posBack + (vecBack * rndRatio);

        // air resistance (>1 accelerates, as in the original)
        if (Res.AirRegist < 1f) vec = vecBack * MathF.Pow(Res.AirRegist, frameRate);
        else vec = vecBack;

        // gravity
        float gravScale = AnimVec[(int)EmitterAnimType.GravityScale].X;
        if (gravScale > 0f)
        {
            Vector3 g = Res.GravityDir * gravScale;
            if (Res.IsWorldGravity)
            {
                Matrix4x4 rt = Res.Follow == FollowType.None ? VfxMath.MakeRt(p.GetBirthMatrix(index)) : MatrixRt;
                g = Vector3.TransformNormal(g, Matrix4x4.Transpose(rt)) * frameRate;
                vec += g;
            }
            else vec += g * frameRate;
        }

        if (!Res.IsUseField) return;

        if (Res.FieldCollision is { } col) FieldCollision(col, ref pos, ref vec, ref life, index, time);
        if (Res.FieldGpuNoise is { } noise) FieldGpuNoise(noise, ref pos, index, time);
        if (Res.FieldRandomSimple is { } rs) FieldRandomSimple(rs, ref vec, index, time);
        if (Res.FieldMagnet is { } mag) FieldMagnet(mag, ref pos, ref vec, index, time);
        if (Res.FieldSpin is { } spin) FieldSpin(spin, ref pos, index, time);
        if (Res.FieldConvergence is { } conv) FieldConvergence(conv, ref pos, index, time);
        if (Res.FieldCurlNoise is { } curl) FieldCurlNoise(curl, ref pos, ref vec, index);
        if (Res.FieldPosAdd is { } pa) FieldPosAdd(pa, ref pos, index, time);
    }

    private Vector3 Anim8(Anim8Key anim, Vector3 constant, int index, float time)
    {
        if (!anim.Enable) return constant;
        return anim.Evaluate(time, Particles.Random[index].X, Res.Life);
    }

    private void FieldRandomSimple(FieldRandomSimple f, ref Vector3 vec, int index, float time)
    {
        Vector3 rv = Anim8(f.Anim, f.RandomVel, index, time);
        if (((uint)time % f.Blank) == 0)
        {
            Vector3 r = Random.GetVector3();
            vec += new Vector3(r.X * rv.X, r.Y * rv.Y, r.Z * rv.Z);
        }
    }

    private void FieldMagnet(FieldMagnet f, ref Vector3 pos, ref Vector3 vec, int index, float time)
    {
        float power = Anim8(f.Anim, new Vector3(f.Power, 0, 0), index, time).X;
        Vector3 target = f.Position;
        if (f.FollowEmitter)
        {
            Matrix4x4 m = Res.Follow == FollowType.None ? Particles.GetBirthMatrix(index) : MatrixSrt;
            Vector3 local = Vector3.Transform(MatrixSrt.Translation, VfxMath.InvertAffine(m));
            target += local;
        }
        if (f.AxisX) vec.X += (target.X - pos.X - vec.X) * power;
        if (f.AxisY) vec.Y += (target.Y - pos.Y - vec.Y) * power;
        if (f.AxisZ) vec.Z += (target.Z - pos.Z - vec.Z) * power;
    }

    private void FieldSpin(FieldSpin f, ref Vector3 pos, int index, float time)
    {
        float frameRate = FrameRate;
        float dyn = Particles.Scale[index].W;
        float rotate = f.Rotate, outer = f.Outer;
        if (f.RotateAnim.Enable) rotate = Anim8(f.RotateAnim, Vector3.Zero, index, time).X * (MathF.PI / 180f);
        if (f.OuterAnim.Enable) outer = Anim8(f.OuterAnim, Vector3.Zero, index, time).X * (MathF.PI / 180f);
        float angle = rotate * dyn * frameRate;
        float sinV = MathF.Sin(angle), cosV = MathF.Cos(angle);

        // axis 0: YZ plane, 1: ZX plane, 2: XY plane
        ref float a = ref pos.Y; ref float b = ref pos.Z;
        switch (f.Axis)
        {
            case 0: a = ref pos.Y; b = ref pos.Z; break;
            case 1: a = ref pos.Z; b = ref pos.X; break;
            case 2: a = ref pos.X; b = ref pos.Y; break;
            default: return;
        }
        float v0 = (a * cosV) + (b * sinV);
        float v1 = (a * -sinV) + (b * cosV);
        a = v0; b = v1;
        if (outer == 0f) return;
        float len2 = (v0 * v0) + (v1 * v1);
        if (len2 <= 0f) return;
        float r = 1f / MathF.Sqrt(len2) * outer * dyn * frameRate;
        a += v0 * r; b += v1 * r;
    }

    private void FieldCollision(FieldCollision f, ref Vector3 pos, ref Vector3 vec, ref float life, int index, float time)
    {
        const float eps = 0.0001f;
        ParticleStore p = Particles;
        if (f.MaxCollisions != -1 && f.MaxCollisions <= p.CollisionCount[index]) return;
        float y = f.PlaneY;
        if (f.IsWorld)
        {
            Matrix4x4 m = Res.Follow == FollowType.EmitterFull ? MatrixSrt : p.GetBirthMatrix(index);
            Vector3 world = Vector3.Transform(pos, m);
            if (f.ReactionType == 1)
            {
                if (world.Y < y) { world.Y = y; life = time; pos = Vector3.Transform(world, VfxMath.InvertAffine(m)); }
            }
            else if (f.ReactionType == 0 && world.Y < y)
            {
                world.Y = y + eps;
                Vector3 worldVel = Vector3.TransformNormal(vec, m);
                worldVel.Y *= -f.Restitution;
                Matrix4x4 inv = VfxMath.InvertAffine(m);
                pos = Vector3.Transform(world, inv);
                vec = Vector3.TransformNormal(worldVel, inv) * f.Drag;
                p.CollisionCount[index]++;
            }
        }
        else
        {
            if (f.ReactionType == 1) { if (pos.Y < y) { pos.Y = y; life = time; } }
            else if (f.ReactionType == 0 && pos.Y < y)
            {
                pos.Y = y;
                vec.Y *= -f.Restitution;
                vec *= f.Drag;
                p.CollisionCount[index]++;
            }
        }
    }

    private void FieldConvergence(FieldConvergence f, ref Vector3 pos, int index, float time)
    {
        float dyn = Particles.Scale[index].W;
        float ratio = Anim8(f.Anim, new Vector3(f.Ratio, 0, 0), index, time).X;
        Vector3 target = f.Position;
        if (f.Type != 0)
        {
            Matrix4x4 m = Res.Follow == FollowType.None ? Particles.GetBirthMatrix(index) : MatrixSrt;
            target += Vector3.Transform(MatrixRt.Translation, VfxMath.InvertAffine(m));
        }
        pos += (target - pos) * (ratio * dyn * FrameRate);
    }

    private void FieldPosAdd(FieldPosAdd f, ref Vector3 pos, int index, float time)
    {
        float dyn = Particles.Scale[index].W;
        Vector3 add = Anim8(f.Anim, f.PositionAdd, index, time) * (dyn * FrameRate);
        if (f.IsGlobal)
        {
            Matrix4x4 rt = Res.Follow == FollowType.None ? VfxMath.MakeRt(Particles.GetBirthMatrix(index)) : MatrixRt;
            add = Vector3.TransformNormal(add, Matrix4x4.Transpose(rt));
        }
        pos += add;
    }

    private void FieldCurlNoise(FieldCurlNoise f, ref Vector3 pos, ref Vector3 vec, int index)
    {
        float offsetBase = f.Base;
        if ((f.BaseRandom & 1) != 0) offsetBase *= Particles.Random[index].X;
        Vector3 particlePos = pos;
        bool world = f.WorldCoordinate > 0;
        if (world) particlePos = TransformToWorldPos(particlePos, index);
        Vector3 coord = (particlePos * f.Scale) + (f.Speed * Time) + new Vector3(offsetBase);
        Vector3 curl = (f.Interpolation & 1) == 0 ? CurlNoiseTable.Nearest(coord) : CurlNoiseTable.Interpolate(coord);
        if (world) curl = TransformToLocalVec(curl, index);
        vec += curl * f.Influence * FrameRate;
    }

    private void FieldGpuNoise(FieldGpuNoise f, ref Vector3 pos, int index, float time)
    {
        ParticleStore p = Particles;
        float frameRate = FrameRate;
        Vector3 rv = Anim8(f.Anim, f.RandomVel, index, time);
        float blank = Math.Max(1f, f.Blank);
        float velTime = time;
        if (f.EnableAirResist)
        {
            float ub = Res.AirRegist;
            float air = ub + ((1f - ub) * (1f - frameRate));
            if (air != 1f) velTime = (1f - MathF.Pow(air, time)) / (1f - air);
        }
        float interval = VfxMath.TwoPi / blank;
        Vector4 r = p.Random[index];
        Vector3 noise;
        if (!f.EnableUnifiedPhase)
        {
            float N(float rnd)
            {
                float a = velTime + (blank * rnd);
                return f.EnableDetailedOption
                    ? NoiseWave(f, a + frameRate, interval) - NoiseWave(f, a, interval)
                    : NoiseDefault(a * interval, (a * interval) + (frameRate * interval));
            }
            noise = new Vector3(N(r.X), N(r.Y), N(r.Z));
        }
        else
        {
            float speed = f.UnifiedPhaseSpeed / 100f * blank;
            float width = f.UnifiedPhaseDistribution / 100f * blank;
            float baseT = (speed * Time) - width;
            float[] seeds = [0f, 0.31415f, 0.92653f];
            float[] rnds = [r.X, r.Y, r.Z];
            float[] outv = new float[3];
            for (int i = 0; i < 3; i++)
            {
                float s = (seeds[i] * blank) + baseT + (width * 2f * rnds[i]);
                float a = velTime + s;
                outv[i] = f.EnableDetailedOption
                    ? NoiseWave(f, a + (frameRate * speed), interval) - NoiseWave(f, a, interval)
                    : NoiseDefault(a * interval, (a + (frameRate * speed)) * interval);
            }
            noise = new Vector3(outv[0], outv[1], outv[2]);
        }
        pos += new Vector3(noise.X * rv.X, noise.Y * rv.Y, noise.Z * rv.Z);
    }

    /// <summary>RandFunc (0x7100aed4ac): sum of four sines with per-wave amplitude and divisor.</summary>
    private static float NoiseWave(FieldGpuNoise f, float t, float interval)
    {
        float x = t * interval, sum = 0;
        for (int i = 0; i < 4; i++)
        {
            float div = f.WaveParamHzRate[i];
            sum += f.WaveParam[i] * MathF.Sin(div != 0f ? x / div : 0f);
        }
        return sum;
    }

    /// <summary>The default noise integral (0x7100aed9dc).</summary>
    private static float NoiseDefault(float a, float b)
    {
        static float S(float v, float c) => MathF.Sin(v * c);
        const float c1 = 1.6666666f, c2 = 2.3809524f, c3 = 4.3478260f, c4 = 6.6666665f;
        return (4f * (S(b, c1) - S(a, c1))) + (3f * (S(b, c2) - S(a, c2))) + (2f * (S(b, c3) - S(a, c3))) + (1.5f * (S(b, c4) - S(a, c4)));
    }
}
