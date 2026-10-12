using System.Numerics;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

public sealed partial class Emitter
{
    /// <summary>Particle life used when a particle never dies (SystemParameters_InfiniteLifeValue).</summary>
    public const float InfiniteLife = 1e8f;

    /// <summary>EmitterCalculator::Emit / EmitBySearchOrder (without the compaction search: BotW fills a ring).</summary>
    private int Emit(ref bool emitDone, int emitCount)
    {
        float random = Random.GetFloat();
        int writeLast = -1;

        if (Res.PrimEmit == PrimEmitType.Unison)
        {
            if (Res.Volume == VolumeType.CircleEquallyDivided)
                emitCount *= Res.NumDivideCircle - (int)(random * Res.NumDivideCircleRandom * 0.01f * Res.NumDivideCircle);
            else if (Res.Volume == VolumeType.LineEquallyDivided)
                emitCount *= Res.NumDivideLine - (int)(random * Res.NumDivideLineRandom * 0.01f * Res.NumDivideLine);
        }

        for (int i = 0; i < emitCount; i++)
        {
            int writeIndex = ParticleAttrFillIndex;
            emitDone = true;
            if (InitializeParticle(writeIndex, i, emitCount, random))
            {
                writeLast = writeIndex;
                ParticleStore p = Particles;
                p.LocalDiff[writeIndex] = new Vector4(p.LocalVec[writeIndex].X, p.LocalVec[writeIndex].Y, p.LocalVec[writeIndex].Z, 0);
                if (ParentEmitter is not null) InheritParentParticleInfo(writeIndex);
                Plugin?.OnParticleEmit(writeIndex);

                ParticleAttrFillIndex = writeIndex + 1;
                if (ParticleAttrFillIndex == MaxParticleAttrFill) ParticleAttrFillIndex = 0;
                if (ParticleCount < MaxParticleAttrFill) ParticleCount++;
            }
        }
        return writeLast;
    }

    private static void StoreV3(Vector4[] a, int i, Vector3 v, float w) => a[i] = new Vector4(v, w);

    /// <summary>The emission volume functions (g_EmitFunctions): local position and velocity of one new particle.</summary>
    private bool EmitFromVolume(out Vector3 pos, out Vector3 vec, int emitIndex, float randomValue)
    {
        float allDir = AnimVec[(int)EmitterAnimType.AllDirectionalVelocity].X;
        Vector3 vs = AnimVec[(int)EmitterAnimType.EmitterVolumeScale];
        pos = Vector3.Zero; vec = Vector3.Zero;
        EmitterResource r = Res;

        void SweepParams(float sweep, out float width, out float start)
        {
            width = sweep;
            start = r.SweepStartRandom ? randomValue * VfxMath.TwoPi : r.SweepStart;
        }
        static float RotateForSweep(float random, float width, float start) => random * width + start - (width / 2f);

        switch (r.Volume)
        {
            case VolumeType.Point:
                vec = Random.GetNormalizedVec3() * allDir;
                return true;

            case VolumeType.Circle:
            case VolumeType.Cylinder:
            {
                SweepParams(r.SweepLongitude, out float w, out float s);
                float rot = RotateForSweep(Random.GetFloat(), w, s);
                float sinV = MathF.Sin(rot), cosV = MathF.Cos(rot);
                float sx = r.VolumeRadius.X * vs.X, sz = r.VolumeRadius.Z * vs.Z;
                pos = new Vector3(sinV * sx, 0, cosV * sz);
                vec = new Vector3(sinV * allDir, 0, cosV * allDir);
                if (r.Volume == VolumeType.Cylinder) pos.Y = (Random.GetFloat() * 2f - 1f) * r.VolumeRadius.Y * vs.Y;
                return true;
            }

            case VolumeType.CircleEquallyDivided:
            {
                float sweepStart = r.SweepStartRandom ? randomValue * VfxMath.TwoPi : r.SweepStart;
                float rot = sweepStart;
                int div = r.NumDivideCircle;
                if (r.PrimEmit == PrimEmitType.Unison) div -= (int)(randomValue * r.NumDivideCircleRandom * 0.01f * r.NumDivideCircle);
                if (r.SweepLongitude != VfxMath.TwoPi && div > 1) div--;
                div = Math.Max(div, 1);
                int fixedIndex = PickPrimIndex(emitIndex, div);
                float longitude = r.SweepLongitude;
                float plus = longitude / div;
                rot += (fixedIndex * plus) - (longitude / 2f) + ((Random.GetFloat() - 0.5f) * 2f * r.SurfacePosRand);
                float sinV = MathF.Sin(rot), cosV = MathF.Cos(rot);
                pos = new Vector3(sinV * r.VolumeRadius.X * vs.X, 0, cosV * r.VolumeRadius.Z * vs.Z);
                vec = new Vector3(sinV * allDir, 0, cosV * allDir);
                return true;
            }

            case VolumeType.CircleFill:
            case VolumeType.CylinderFill:
            {
                SweepParams(r.SweepLongitude, out float w, out float s);
                float rot = RotateForSweep(Random.GetFloat(), w, s);
                float sinV = MathF.Sin(rot), cosV = MathF.Cos(rot);
                float rand = Random.GetFloat();
                float inner = 1f - r.CaliberRatio;
                rand = VfxMath.SafeSqrt(rand + (inner * inner * (1f - rand)));
                float dirx = rand * sinV, dirz = rand * cosV;
                float sx = r.VolumeRadius.X * vs.X, sz = r.VolumeRadius.Z * vs.Z;
                pos = new Vector3(dirx * sx, 0, dirz * sz);
                Vector3 v = new(dirx * rand, 0, dirz * rand);
                if (sx > sz) v.Z *= sz / sx; else if (sz > 0) v.X *= sx / sz;
                vec = v.LengthSquared() > 0 ? Vector3.Normalize(v) : new Vector3(0, 0, 1);
                vec *= allDir;
                if (r.Volume == VolumeType.CylinderFill) pos.Y = (Random.GetFloat() * 2f - 1f) * r.VolumeRadius.Y * vs.Y;
                return true;
            }

            case VolumeType.Sphere:
            case VolumeType.SphereFill:
            {
                Vector3 scale = new(r.VolumeRadius.X * vs.X, r.VolumeRadius.Y * vs.Y, r.VolumeRadius.Z * vs.Z);
                float rotWidth, rotStart;
                if (r.ArcType == 1) SweepParams(r.SweepLatitude, out rotWidth, out rotStart);
                else SweepParams(r.SweepLongitude, out rotWidth, out rotStart);

                float sinV, cosV;
                if (r.ArcType == 1)
                {
                    float rot = Random.GetFloat() * VfxMath.TwoPi;
                    sinV = MathF.Sin(rot); cosV = MathF.Cos(rot);
                }
                else
                {
                    float rot = RotateForSweep(Random.GetFloat(), rotWidth, rotStart);
                    sinV = MathF.Sin(rot); cosV = MathF.Cos(rot);
                }
                float y = r.ArcType == 1 ? 1f - ((1f - MathF.Cos(r.SweepLatitude)) * Random.GetFloat()) : (Random.GetFloat() * 2f - 1f);
                float rr = VfxMath.SafeSqrt(1f - (y * y));
                float radius = 1f;
                if (r.Volume == VolumeType.SphereFill)
                {
                    radius = VfxMath.SafeSqrt(Random.GetFloat());
                    radius = (radius * r.CaliberRatio) + 1f - r.CaliberRatio;
                }
                Vector3 dir = new(rr * sinV, y, rr * cosV);
                if (r.ArcType == 1) dir = RotateLatitude(dir);
                pos = dir * scale * radius;
                vec = dir * allDir;
                return true;
            }

            case VolumeType.SphereEqually32Divided:
            case VolumeType.SphereEqually64Divided:
            {
                Vector3 scale = new(r.VolumeRadius.X * vs.X, r.VolumeRadius.Y * vs.Y, r.VolumeRadius.Z * vs.Z);
                float[] table;
                int divCount;
                if (r.Volume == VolumeType.SphereEqually32Divided)
                {
                    int[] pattern = [2, 3, 4, 6, 8, 12, 20, 32];
                    int vIndex = Math.Clamp(r.VolumeTblIndex, 0, 7);
                    table = SphereDivisionTables.Divided32[vIndex];
                    divCount = pattern[vIndex];
                }
                else
                {
                    divCount = Math.Clamp(r.VolumeTblIndex64, 2, 64);
                    table = SphereDivisionTables.Divided64[divCount - 2];
                }
                int fixedIndex = PickPrimIndex(emitIndex, divCount);
                Vector3 dir = new(table[fixedIndex * 3], table[(fixedIndex * 3) + 1], table[(fixedIndex * 3) + 2]);
                if (!IsLatitudeInside(dir)) return false;
                dir = RotateLatitude(dir);
                pos = dir * scale;
                vec = dir * allDir;
                return true;
            }

            case VolumeType.Box:
            {
                uint coord = Random.GetUnsignedIntegerDirect(), sign = Random.GetUnsignedIntegerDirect();
                Vector3 rnd = new(Random.GetFloat() * 2f - 1f, Random.GetFloat() * 2f - 1f, Random.GetFloat() * 2f - 1f);
                Vector3 s = r.VolumeRadius * vs;
                bool neg = sign >= 0xffffffffu / 2;
                if (coord < 0xffffffffu / 3) pos = new Vector3(s.X * rnd.X, s.Y * rnd.Y, neg ? -s.Z : s.Z);
                else if (coord < 0xffffffffu / 3 * 2) pos = new Vector3(s.X * rnd.X, neg ? -s.Y : s.Y, s.Z * rnd.Z);
                else pos = new Vector3(neg ? -s.X : s.X, s.Y * rnd.Y, s.Z * rnd.Z);
                vec = pos.LengthSquared() > 0 ? Vector3.Normalize(pos) * allDir : Vector3.Zero;
                return true;
            }

            case VolumeType.BoxFill:
            {
                Vector3 s = r.VolumeRadius * vs;
                float rr = 1f - r.CaliberRatio;
                if (rr == 1f) rr = 0.999f;
                float v = Random.GetFloat();
                float r2 = rr * rr, r3 = r2 * rr, volA = 1f - rr, volB = volA * rr;
                v *= 1f - r3;
                float rx = Random.GetFloat(), ry = Random.GetFloat(), rz = Random.GetFloat();
                if (v < volA) pos = new Vector3(rx, (ry * (1f - rr)) + rr, rz);
                else if (v < volA + volB) pos = new Vector3((rx * (1f - rr)) + rr, ry * rr, rz);
                else pos = new Vector3(rx * rr, ry * rr, (rz * (1f - rr)) + rr);
                if (Random.GetFloat() < 0.5f) pos.X = -pos.X;
                if (Random.GetFloat() < 0.5f) pos.Y = -pos.Y;
                if (Random.GetFloat() < 0.5f) pos.Z = -pos.Z;
                pos *= s;
                Vector3 dir = pos;
                if (dir.LengthSquared() == 0) dir = new Vector3(Random.GetFloat(), Random.GetFloat(), Random.GetFloat());
                vec = Vector3.Normalize(dir) * allDir;
                return true;
            }

            case VolumeType.Line:
            {
                float sz = r.LineLength * vs.Z;
                pos = new Vector3(0, 0, (Random.GetFloat() * sz) - ((sz + (r.LineCenter * sz)) / 2f));
                vec = new Vector3(0, 0, allDir);
                return true;
            }

            case VolumeType.LineEquallyDivided:
            {
                float sz = r.LineLength * vs.Z;
                int div = r.NumDivideLine;
                if (r.PrimEmit == PrimEmitType.Unison) div -= (int)(randomValue * r.NumDivideLineRandom * 0.01f * r.NumDivideLine);
                div = Math.Max(div, 1);
                int fixedIndex = PickPrimIndex(emitIndex, div);
                float t, plus;
                if (div == 1) { t = 0.5f; plus = 0f; } else { t = 0f; plus = 1f / (div - 1); }
                t = (plus * fixedIndex) + t;
                pos = new Vector3(0, 0, (t * sz) - ((sz + (r.LineCenter * sz)) / 2f));
                vec = new Vector3(0, 0, allDir);
                return true;
            }

            case VolumeType.Rectangle:
            {
                float sx = r.VolumeRadius.X * vs.X, sz = r.VolumeRadius.Z * vs.Z;
                uint coord = Random.GetUnsignedIntegerDirect(), sign = Random.GetUnsignedIntegerDirect();
                float rx = Random.GetFloat() * 2f - 1f, rz = Random.GetFloat() * 2f - 1f;
                bool neg = sign >= 0xffffffffu / 2;
                if (coord < 0xffffffffu / 2) pos = new Vector3(sx * rx, 0, neg ? -sz : sz);
                else pos = new Vector3(neg ? -sx : sx, 0, sz * rz);
                vec = pos.LengthSquared() > 0 ? Vector3.Normalize(pos) * allDir : Vector3.Zero;
                return true;
            }

            case VolumeType.Primitive:
            default:
            {
                IReadOnlyList<(Vector3 Position, Vector3 Normal)>? prim = Set.System.Primitives?.Invoke(r.VolumePrimitiveId);
                if (prim is null || prim.Count == 0)
                {
                    vec = Random.GetNormalizedVec3() * allDir;
                    return true;
                }
                int idx = emitIndex;
                if (r.PrimEmit == PrimEmitType.Random) idx = (int)(Random.GetFloat() * prim.Count);
                else if (r.PrimEmit == PrimEmitType.IndexOrder) { idx = EmitByPrimitiveCounter % prim.Count; EmitByPrimitiveCounter++; }
                idx = Math.Clamp(idx, 0, prim.Count - 1);
                pos = prim[idx].Position * vs;
                vec = prim[idx].Normal * allDir;
                return true;
            }
        }
    }

    private int PickPrimIndex(int emitIndex, int divCount)
    {
        switch (Res.PrimEmit)
        {
            case PrimEmitType.Random: return Math.Min((int)(Random.GetFloat() * divCount), divCount - 1);
            case PrimEmitType.IndexOrder:
                int i = EmitByPrimitiveCounter % divCount;
                EmitByPrimitiveCounter++;
                if (EmitByPrimitiveCounter >= divCount) EmitByPrimitiveCounter = 0;
                return i;
            default: return Math.Min(emitIndex, divCount - 1);
        }
    }

    private bool IsLatitudeInside(Vector3 dir)
    {
        if (MathF.PI - 0.0001f < Res.SweepLatitude) return true;
        return MathF.Cos(Res.SweepLatitude) < dir.Y;
    }

    private Vector3 RotateLatitude(Vector3 dir)
    {
        Vector3 axis = Res.LatitudeDir switch
        {
            0 => Vector3.UnitX, 1 => -Vector3.UnitX, 2 => Vector3.UnitY, 3 => -Vector3.UnitY, 4 => Vector3.UnitZ, _ => -Vector3.UnitZ
        };
        if (axis == Vector3.UnitY) return dir;
        return Vector3.Transform(dir, VfxMath.RotationBetween(Vector3.UnitY, axis));
    }

    /// <summary>EmitterCalculator::InitializeParticle, in the order of BotW's 0x7100ada3cc.</summary>
    private bool InitializeParticle(int idx, int emitIndex, int maxEmitCount, float randomValue)
    {
        ParticleStore p = Particles;
        if (!EmitFromVolume(out Vector3 localPos, out Vector3 localVec, emitIndex, randomValue)) return false;

        ParticleCreateId++;
        p.CreateId[idx] = ParticleCreateId;

        // tangential ("XZ diffusion") velocity
        if (Res.XzDiffusion != 0f)
        {
            Vector3 y = new(localPos.X, 0, localPos.Z);
            if (y.LengthSquared() <= float.Epsilon)
            {
                y.X = (Random.GetFloat() * 2f) - 1f;
                y.Z = (Random.GetFloat() * 2f) - 1f;
            }
            y = y.LengthSquared() > 0 ? Vector3.Normalize(y) : Vector3.Zero;
            localVec += y * Res.XzDiffusion;
        }

        float velRand = 1f - (Random.GetFloat() * (Res.VelRandomPercent / 100f) * Set.RandomVel);
        float dirVel = AnimVec[(int)EmitterAnimType.DesignatedDirectionalVelocity].X * Set.DirectionalVel;
        float angle = Res.DiffusionDirAngle;

        if (Res.PosRand != 0f)
        {
            Vector3 t = Random.GetNormalizedVec3();
            localPos.X += t.X * Res.PosRand;
            localPos.Y += t.Y * Res.PosRand;
        }

        // birth emitter matrix (the shader's sysEmtMat0..2Attr)
        if (p.Mat0 is not null)
        {
            Matrix4x4 m = MatrixSrt;
            p.Mat0[idx] = new Vector4(m.M11, m.M21, m.M31, m.M41);
            p.Mat1[idx] = new Vector4(m.M12, m.M22, m.M32, m.M42);
            p.Mat2[idx] = new Vector4(m.M13, m.M23, m.M33, m.M43);
        }
        p.LocalPos[idx] = new Vector4(localPos, 0);

        Vector3 designated = Res.DesignatedDir;
        if (Res.IsWorldOrientedVelocity) designated = TransformToLocalVec(designated, idx);

        if (angle == 0f)
            localVec = (localVec + (designated * dirVel)) * velRand;
        else
        {
            float a = 1f - (angle / 90f);
            float rot = Random.GetFloat() * VfxMath.TwoPi;
            float sinV = MathF.Sin(rot), cosV = MathF.Cos(rot);
            float yy = (Random.GetFloat() * (1f - a)) + a;
            float rr = VfxMath.SafeSqrt(1f - (yy * yy));
            Vector3 cone = new(rr * cosV, yy, rr * sinV);
            if (designated.LengthSquared() > 0) cone = Vector3.Transform(cone, VfxMath.RotationBetween(Vector3.UnitY, designated));
            localVec = (localVec + (cone * dirVel)) * velRand;
        }

        Vector3 diffusion = Random.GetVector3();
        localVec += new Vector3(diffusion.X * Res.Diffusion.X, diffusion.Y * Res.Diffusion.Y, diffusion.Z * Res.Diffusion.Z);
        localVec += EmitterLocalVec * Res.EmVelInherit;
        localVec += Vector3.TransformNormal(Set.AdditionalVel, VfxMath.InvertAffine(MatrixRt));

        // scale
        Vector3 ps = AnimVec[(int)EmitterAnimType.ParticleScale];
        Vector3 es = Set.ParticleEmissionScale;
        Vector3 scale;
        if (Res.PtclScaleRandom.X != Res.PtclScaleRandom.Y)
            scale = new Vector3(
                ps.X * (1f - (Res.PtclScaleRandom.X / 100f * Random.GetFloat())) * es.X,
                ps.Y * (1f - (Res.PtclScaleRandom.Y / 100f * Random.GetFloat())) * es.Y,
                ps.Z * (1f - (Res.PtclScaleRandom.Z / 100f * Random.GetFloat())) * es.Z);
        else
        {
            float rnd = 1f - (Res.PtclScaleRandom.X / 100f * Random.GetFloat());
            scale = new Vector3(ps.X * rnd * es.X, ps.Y * rnd * es.Y, ps.Z * rnd * es.Z);
        }
        p.CollisionCount[idx] = 0;
        float dyn = 1f + Res.DynamicsRand - (Res.DynamicsRand * Random.GetFloat() * 2f);
        p.Scale[idx] = new Vector4(scale, dyn);

        // time and life
        p.CreateTime[idx] = Time;
        float life;
        if (!Res.IsLifeInfinity)
        {
            float pl = AnimVec[(int)EmitterAnimType.ParticleLife].X;
            life = pl - (pl * (Random.GetInteger(Res.LifeRandomPercent) * 0.01f));
            life *= ParticleLifeScale;
            life *= Set.ParticleLifeScale;
        }
        else life = InfiniteLife;
        p.Life[idx] = life;
        p.LocalPos[idx] = new Vector4(localPos, life);
        p.LocalVec[idx] = new Vector4(localVec, Time);

        if (p.ChildEmitter is not null)
        {
            for (int k = 0; k < p.LightChildEmitCount![idx].Length; k++)
            {
                p.LightChildEmitCount[idx][k] = 0; p.LightChildEmitSaving[idx][k] = 0;
                p.LightChildEmitInterval[idx][k] = 0; p.LightChildEmitDone![idx][k] = false;
            }
            p.LightChildTime![idx] = 0;
        }

        p.Random[idx] = new Vector4(Random.GetFloat(), Random.GetFloat(), Random.GetFloat(), Random.GetFloat());
        p.InitRotate[idx] = new Vector4(Res.RotInit, 0);
        if (p.InitColor0 is not null) { p.InitColor0[idx] = Vector4.One; p.InitColor1![idx] = Vector4.One; }

        // child emitters
        if (p.ChildEmitter is not null)
        {
            for (int k = 0; k < Res.ChildCount; k++)
            {
                EmitterResource childRes = Res.Children[k];
                if (childRes.EnableEmitterParticle)
                {
                    Emitter? child = Set.CreateEmitter(childRes, 0, this, k);
                    if (child is null) continue;
                    p.ChildEmitter[idx][k] = child;
                    child.ParentEmitterCreateId = EmitterCreateId;
                    child.ParentParticleLife = life;
                    child.ParentParticleBirthTime = Time;
                    child.ParentParticleIndex = idx;
                    child.ParentParticleScale = p.Scale[idx];
                    child.ParentParticleRotate = p.InitRotate[idx];
                    child.ParentParticleRandom = p.Random[idx];
                    child.MatrixSrt = Matrix4x4.Identity;
                    child.MatrixRt = Matrix4x4.Identity;
                }
                else p.ChildEmitter[idx][k] = SharedChildHead[k];
            }
        }
        return true;
    }

    /// <summary>EmitterCalculator::InheritParentParticleInfo.</summary>
    private void InheritParentParticleInfo(int writeIndex)
    {
        ParticleStore p = Particles;
        Emitter parent = ParentEmitter!;
        Vector3 parentWorldVec;
        Vector4 parentScale, parentRotate, parentRandom;
        float parentTime, parentLife;
        int parentIndex = TempParentIndex;

        if (!Res.EnableEmitterParticle)
        {
            if (parentIndex < 0 || TempWorldPos is null) return;
            ParticleStore pp = parent.Particles;
            parentWorldVec = TempWorldVec ?? Vector3.Zero;
            parentScale = pp.Scale[parentIndex];
            parentRotate = pp.InitRotate[parentIndex];
            parentRandom = pp.Random[parentIndex];
            parentTime = parent.Time - pp.CreateTime[parentIndex] - parent.FrameRate;
            parentLife = pp.Life[parentIndex];
            Vector4 lp = p.LocalPos[writeIndex];
            p.LocalPos[writeIndex] = new Vector4(new Vector3(lp.X, lp.Y, lp.Z) + TempWorldPos.Value, lp.W);
        }
        else
        {
            parentWorldVec = ParentParticleWorldVec;
            parentScale = ParentParticleScale;
            parentRotate = ParentParticleRotate;
            parentRandom = ParentParticleRandom;
            parentTime = ParentParticleTime;
            parentLife = ParentParticleLife;
        }

        if (Res.InheritVelocity)
        {
            Vector4 v = p.LocalVec[writeIndex];
            Vector3 t = (parentWorldVec * Res.InheritVelocityRate) + new Vector3(v.X, v.Y, v.Z);
            p.LocalVec[writeIndex] = new Vector4(t, v.W);
        }
        if (Res.InheritScale)
        {
            Vector3 s = ParticleEval.Scale(parent.Res, new Vector3(parentScale.X, parentScale.Y, parentScale.Z), parentRandom, parentLife, parentTime) * Res.InheritScaleRate;
            p.Scale[writeIndex] = new Vector4(s, p.Scale[writeIndex].W);
        }
        if (Res.InheritRotate)
        {
            Vector3 rot = ParticleEval.Rotation(parent.Res, new Vector3(parentRotate.X, parentRotate.Y, parentRotate.Z), parentRandom, parentTime);
            p.InitRotate[writeIndex] = new Vector4(rot, 0);
        }
        if (p.InitColor0 is not null && (Res.InheritColor0 || Res.InheritAlpha0))
        {
            Vector4 c = ParticleEval.Color0(parent.Res, parentRandom, parent.Color0, parent.AnimVec[(int)EmitterAnimType.Color0],
                parent.AnimVec[(int)EmitterAnimType.Alpha0].X, parentLife, parentTime);
            Vector4 cur = p.InitColor0[writeIndex];
            if (Res.InheritColor0) { cur.X = c.X; cur.Y = c.Y; cur.Z = c.Z; }
            if (Res.InheritAlpha0) cur.W = c.W;
            p.InitColor0[writeIndex] = cur;
        }
        if (p.InitColor1 is not null && (Res.InheritColor1 || Res.InheritAlpha1))
        {
            Vector4 c = ParticleEval.Color1(parent.Res, parentRandom, parent.Color1, parent.AnimVec[(int)EmitterAnimType.Color1],
                parent.AnimVec[(int)EmitterAnimType.Alpha1].X, parentLife, parentTime);
            Vector4 cur = p.InitColor1[writeIndex];
            if (Res.InheritColor1) { cur.X = c.X; cur.Y = c.Y; cur.Z = c.Z; }
            if (Res.InheritAlpha1) cur.W = c.W;
            p.InitColor1[writeIndex] = cur;
        }
    }
}
