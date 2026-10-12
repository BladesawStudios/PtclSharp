using System.Numerics;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

public sealed partial class Emitter
{
    public enum CalcResult { Alive = 0, Kill = 1 }

    private Matrix4x4 EmitterAnimMatrix(bool rt)
    {
        Vector3 scale = AnimVec[(int)EmitterAnimType.Scale];
        Vector3 rot = AnimVec[(int)EmitterAnimType.Rotate];
        Vector3 trans = AnimVec[(int)EmitterAnimType.Translate];
        if (Res.EmitterAnims[(int)EmitterAnimType.Rotate] is not null)
            rot = new Vector3(rot.X * (MathF.PI / 180f), rot.Y * (MathF.PI / 180f), rot.Z * (MathF.PI / 180f));
        return VfxMath.CreateSrtXyz(rt ? Vector3.One : scale, rot, trans);
    }

    /// <summary>EmitterCalculator::UpdateEmitterMatrix.</summary>
    private void UpdateEmitterMatrix()
    {
        if (!IsChild)
        {
            if (Res.IsEmitterSrtAnim)
            {
                MatrixSrt = EmitterAnimMatrix(false) * Set.MatrixSrt;
                MatrixRt = EmitterAnimMatrix(true) * Set.MatrixRt;
            }
            else if (Set.IsSrtDirty)
            {
                MatrixSrt = ResMatrixSrt * Set.MatrixSrt;
                MatrixRt = ResMatrixRt * Set.MatrixRt;
            }
            return;
        }

        if (Res.EnableEmitterParticle)
        {
            if (Res.IsEmitterSrtAnim) { MatrixSrt = EmitterAnimMatrix(false); MatrixRt = EmitterAnimMatrix(true); }
            else { MatrixSrt = ResMatrixSrt; MatrixRt = ResMatrixRt; }
            Vector3 pos = MatrixSrt.Translation + ParentParticleWorldPos;
            MatrixSrt.Translation = pos;
            MatrixRt.Translation = pos;
            if (ParentEmitter!.Res.Follow != FollowType.None)
            {
                MatrixSrt = MatrixSrt * ParentEmitter.MatrixSrt;
                MatrixRt = MatrixRt * ParentEmitter.MatrixRt;
            }
        }
        else
        {
            if (Res.IsEmitterSrtAnim) { MatrixSrt = EmitterAnimMatrix(false); MatrixRt = EmitterAnimMatrix(true); }
            else { MatrixSrt = ResMatrixSrt; MatrixRt = ResMatrixRt; }
            if (ParentEmitter!.Res.Follow != FollowType.None)
            {
                MatrixSrt = MatrixSrt * ParentEmitter.MatrixSrt;
                MatrixRt = MatrixRt * ParentEmitter.MatrixRt;
            }
        }
    }

    private void UpdateEmitterLocalVec()
    {
        Matrix4x4 inv = VfxMath.InvertAffine(MatrixSrt);
        Vector3 localPos = Vector3.Transform(MatrixSrt.Translation, inv);
        Vector3 prevLocal = Vector3.Transform(EmitterPrevPos, inv);
        EmitterLocalVec = localPos - prevLocal;
    }

    /// <summary>
    /// EmitterCalculator::Calculate. Returns false when the emitter has ended and should be killed.
    /// </summary>
    internal bool Calculate(float frameRate, bool isFade, bool isEmission)
    {
        bool isEmit = isEmission;
        bool ret = true;
        float emitStart = Res.Start;
        float emitterTime = Time;
        float emitEnd = Res.Start + Res.EmitDuration;
        float parentLife;

        SavingFrameRate = frameRate;
        if (IsKillReservation) return false;
        FrameRate = frameRate;

        if (IsChild && Res.EnableEmitterParticle)
        {
            parentLife = ParentParticleLife;
            emitStart = parentLife * (Res.EmitTiming / 100f);
            emitEnd = Res.IsOneTime ? emitStart + Res.EmitDuration : ParentParticleLife;
        }

        // emitter-time animation
        _calculatedAnimCount = 0;
        if (Res.IsUseEmitterAnim && !(IsChild && !Res.EnableEmitterParticle))
        {
            for (int i = 0; i < (int)EmitterAnimType.Count; i++)
            {
                EmitterAnimTrack? t = Res.EmitterAnims[i];
                if (t is { Enabled: true } && (!_animEnd[i] || t.Loop))
                {
                    Vector3 v = AnimVec[i];
                    if (t.Evaluate(Time, ref v, ref _animEnd[i])) AnimVec[i] = v;
                    _calculatedAnimCount++;
                }
            }
            if (Res.EmitterAnims[(int)EmitterAnimType.AllDirectionalVelocity] is { Enabled: true } a1
                && (!_animEnd[(int)EmitterAnimType.AllDirectionalVelocity] || a1.Loop))
                AnimVec[(int)EmitterAnimType.AllDirectionalVelocity].X *= Set.AllDirectionalVel;
            if (Res.EmitterAnims[(int)EmitterAnimType.EmitterVolumeScale] is { Enabled: true } a2
                && (!_animEnd[(int)EmitterAnimType.EmitterVolumeScale] || a2.Loop))
                AnimVec[(int)EmitterAnimType.EmitterVolumeScale] *= Set.EmitterVolumeScale;
        }
        else
        {
            AnimVec[(int)EmitterAnimType.AllDirectionalVelocity].X = Res.AllDirection * Set.AllDirectionalVel;
            AnimVec[(int)EmitterAnimType.EmitterVolumeScale] = Res.VolumeFormScale * Set.EmitterVolumeScale;
        }
        if (_calculatedAnimCount > 0 && AnimVec[(int)EmitterAnimType.ParticleLife].X > Res.Life)
            AnimVec[(int)EmitterAnimType.ParticleLife].X = Res.Life;

        UpdateEmitterMatrix();
        if (emitterTime == 0f) EmitterPrevPos = MatrixSrt.Translation;
        if (Time != 0f) UpdateEmitterLocalVec();

        // fade in
        if ((Res.IsAlphaFadeIn || Res.IsScaleFadeIn) && FadeInValue < 1f)
        {
            if (Res.FadeInTime > 0) FadeInValue = MathF.Min(1f, FadeInValue + frameRate / Res.FadeInTime);
            else FadeInValue = 1f;
        }

        // fade out
        if (isFade || IsSoloFade)
        {
            if (Res.IsFadeEmit) isEmit = false;
            if (Res.IsAlphaFadeOut || Res.IsScaleFadeOut)
            {
                if (Res.FadeOutTime <= 0) { FadeOutValue = 0; return false; }
                FadeOutValue -= frameRate / Res.FadeOutTime;
                if (FadeOutValue < 0f) { FadeOutValue = 0; return false; }
            }
        }

        // emission
        if (isEmit)
        {
            if (!(IsChild && !Res.EnableEmitterParticle) && emitStart <= emitterTime)
            {
                bool finished = (Res.IsOneTime || IsChild) && emitEnd <= emitterTime && IsEmitDone;
                if (!finished) TryEmitParticle(ref EmitCount, ref EmitSaving, ref EmitInterval, ref IsEmitDone, frameRate);
            }
        }

        // lightweight children accumulate their own clock on every live parent particle
        if (Res.ChildCount > 0 && Particles.LightChildTime is not null)
            for (int i = 0; i < ParticleCount; i++)
                if (Particles.CreateId[i] != 0) Particles.LightChildTime[i] += frameRate;

        if (Res.Calc == CalcType.Cpu || Res.Calc == CalcType.GpuStreamOut)
            CalculateParticles();
        else
            GpuSideAge();

        WriteDynamicBlock(DynamicBlock);
        EmitterPrevPos = MatrixSrt.Translation;
        Time += frameRate;

        // death
        if (IsChild && !Res.EnableEmitterParticle)
        {
            if (ChildLastResult == (int)CalcResult.Kill && emitterTime > emitStart + EmitLastFrame + Res.Life) ret = false;
        }
        else if (Res.IsOneTime)
        {
            if (!Res.IsLifeInfinity)
            {
                float endTime = emitEnd + Res.Life;
                if (Res.IsAlphaFadeOut || Res.IsScaleFadeOut) endTime += Res.FadeOutTime;
                endTime += Plugin?.ExtraOneTimeEndTime ?? 0f;
                if (emitterTime > endTime) ret = false;
            }
        }
        else
        {
            if ((isFade || IsSoloFade || IsChild) && emitterTime > emitStart + EmitLastFrame + Res.Life) ret = false;
        }
        return ret;
    }

    /// <summary>
    /// GPU-calculated emitters (calc type 1) move particles in the vertex shader. The CPU only has to retire dead slots so the
    /// emission search and the live count stay right.
    /// </summary>
    private void GpuSideAge()
    {
        ParticleProperty_ProcessingCount = ParticleCount;
        for (int i = 0; i < ParticleCount; i++)
        {
            if (Particles.CreateId[i] == 0) continue;
            float time = Time - Particles.CreateTime[i];
            if (time >= Particles.Life[i]) Particles.CreateId[i] = 0;
        }
    }

    internal int ParticleProperty_ProcessingCount;

    /// <summary>EmitterCalculator::TryEmitParticle.</summary>
    internal void TryEmitParticle(ref float emitCount, ref float emitSaving, ref float emitInterval, ref bool emitDone, float frameRate)
    {
        if (!Res.IsEmitDistEnabled)
        {
            if (emitCount >= emitInterval)
            {
                float emitCountSub = emitCount - emitInterval;
                float emitRateRandom = Res.RateRandomPercent / 100f * Res.Rate;
                float emitReduction = emitRateRandom * Random.GetFloat();

                float emitRatio = Res.Rate;
                if (Res.Volume != VolumeType.SphereEqually32Divided && Res.Volume != VolumeType.SphereEqually64Divided)
                    emitRatio = AnimVec[(int)EmitterAnimType.EmissionRate].X;

                emitSaving += (emitRatio - emitReduction) * EmitRatio * Set.EmissionRatioScale;
                if (emitSaving < 0) emitSaving = 0;
                int count = (int)emitSaving;
                if (count != 0)
                {
                    Emit(ref emitDone, count);
                    UpdateByEmit(ref emitInterval);
                    Set.IsSrtDirty = true;
                    emitSaving -= count;
                    emitCount = frameRate + emitCountSub;
                    EmitLastFrame = Time;
                }
                else emitCount = 0;
            }
            else emitCount += frameRate;
        }
        else if (Set.EnableDistanceBasedEmission)
        {
            float length = EmitterLocalVec.Length();
            float vessel = emitSaving;
            float virtualLength = length;
            if (virtualLength < Res.EmitDistMargin) virtualLength = 0;
            if (virtualLength == 0f) virtualLength = Res.EmitDistMin;
            else if (virtualLength < Res.EmitDistMin) virtualLength = Res.EmitDistMin;
            else if (Res.EmitDistMax < virtualLength) virtualLength = Res.EmitDistMax;
            vessel += virtualLength;
            int count = Res.EmitDistUnit != 0f ? (int)(vessel / Res.EmitDistUnit) : 0;
            if (count > 0)
            {
                Vector3 prev = EmitterPrevPos, curr = MatrixSrt.Translation;
                for (int i = 0; i < count; i++)
                {
                    vessel -= Res.EmitDistUnit;
                    float ratio = virtualLength != 0f ? vessel / virtualLength : 0f;
                    Vector3 pos = Vector3.Lerp(curr, prev, ratio);
                    MatrixRt.Translation = pos; MatrixSrt.Translation = pos;
                    Emit(ref emitDone, 1);
                    MatrixRt.Translation = curr; MatrixSrt.Translation = curr;
                }
                EmitLastFrame = Time;
            }
            emitSaving = vessel;
        }
    }
}
