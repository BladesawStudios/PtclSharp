using System.Numerics;
using PtclSharp.Simulation.Random;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

/// <summary>
/// One running emitter (nn::vfx::Emitter plus EmitterCalculator): emission, the per-frame particle update and the values the
/// shaders read each frame. Game-specific numbers live in <see cref="EmitterResource"/>; this class is game-neutral.
/// </summary>
public sealed partial class Emitter
{
    public const int MaxParticlesPerEmitter = 60000;

    public EmitterResource Res { get; }
    public EmitterSetInstance Set { get; }
    public Emitter? ParentEmitter { get; }
    public bool IsChild { get; }
    public int ChildResIndex { get; internal set; } = -1;
    public ParticleStore Particles { get; private set; } = null!;

    /// <summary>The emitter plugin (stripes), when the emitter has one and runs on the CPU.</summary>
    public Plugins.EmitterPlugin? Plugin { get; private set; }

    /// <summary>Child emitter instances per child resource (normal children: one per parent particle; lightweight: the single shared one).</summary>
    public List<Emitter>[] ChildLists { get; }
    public Emitter?[] SharedChildHead { get; }

    public float Time;
    public float FrameRate;
    public float SavingFrameRate;
    public int ParticleCount;
    public int ParticleAttrFillIndex;
    public int MaxParticleAttrFill;
    public int ParticleCreateId;
    public int EmitterCreateId;
    public bool IsAliveChildren;

    // emission state
    public float EmitCount, EmitSaving, EmitInterval;
    public bool IsEmitDone;
    public float EmitLastFrame;
    public int EmitByPrimitiveCounter;
    public VfxRandom Random;

    // matrices
    public Matrix4x4 MatrixSrt = Matrix4x4.Identity, MatrixRt = Matrix4x4.Identity;
    public Matrix4x4 ResMatrixSrt = Matrix4x4.Identity, ResMatrixRt = Matrix4x4.Identity;
    public Vector3 EmitterPrevPos, EmitterLocalVec;

    // fade
    public float FadeInValue, FadeOutValue = 1f;
    public bool IsSoloFade;
    public bool IsKillReservation;

    // user-adjustable
    public float EmitRatio = 1f, EmitIntervalRatio = 1f, ParticleLifeScale = 1f;
    public Vector4 Color0 = Vector4.One, Color1 = Vector4.One;
    public bool IsVisible = true;

    // emitter animation: lane values (EmitterAnimType) and "finished" flags
    public readonly Vector3[] AnimVec = new Vector3[(int)EmitterAnimType.Count];
    private readonly bool[] _animEnd = new bool[(int)EmitterAnimType.Count];
    private int _calculatedAnimCount;

    // child-of-particle data
    public int ChildLastResult; // EmitterCalcResult of the previous frame, used by lightweight children
    public int ParentEmitterCreateId = -1;
    public float ParentParticleLife, ParentParticleTime;
    public Vector3 ParentParticleWorldPos, ParentParticleWorldVec;
    public Vector4 ParentParticleScale = Vector4.One, ParentParticleRotate, ParentParticleRandom;
    public int ParentParticleIndex = -1;
    public float ParentParticleBirthTime;

    // lightweight child scratch (set by the parent while it emits)
    internal Vector3? TempWorldPos, TempWorldVec;
    internal int TempParentIndex = -1;

    private Emitter(EmitterSetInstance set, EmitterResource res, Emitter? parent)
    {
        Set = set;
        Res = res;
        ParentEmitter = parent;
        IsChild = res.IsChild;
        ChildLists = new List<Emitter>[res.ChildCount];
        for (int i = 0; i < ChildLists.Length; i++) ChildLists[i] = [];
        SharedChildHead = new Emitter?[res.ChildCount];
    }

    internal static Emitter? Create(EmitterSetInstance set, EmitterResource res, int maxParticles, Emitter? parent)
    {
        var e = new Emitter(set, res, parent);
        return e.Initialize(maxParticles) ? e : null;
    }

    private bool Initialize(int maxParticles)
    {
        FadeInValue = (Res.IsAlphaFadeIn || Res.IsScaleFadeIn) ? 0f : 1f;

        // random seed
        switch (Res.RandomSeedType)
        {
            case 0: Random.SetSeed(Set.System.GlobalRandom.GetU32()); break;
            case 1: Random.SetSeed(Set.RandomSeed); break;
            default: Random.SetSeed(unchecked(Res.FixedSeed * 0xDFDC1C35u)); break;
        }

        int count = maxParticles != 0 ? maxParticles : CalculateRequiredParticleCount();
        if (count <= 0) return false;
        count = Math.Min(count, MaxParticlesPerEmitter);
        MaxParticleAttrFill = count;
        Particles = new ParticleStore(count, IsChild, Res.Follow != FollowType.EmitterFull, Res.ChildCount);

        CreateResMatrix();
        Plugin = Plugins.EmitterPlugin.Create(this);

        AnimVec[(int)EmitterAnimType.Scale] = Res.Scale;
        AnimVec[(int)EmitterAnimType.Rotate] = Res.Rotate;
        AnimVec[(int)EmitterAnimType.Translate] = Res.Trans;
        AnimVec[(int)EmitterAnimType.Color0] = new Vector3(Res.EmitterColor0.X, Res.EmitterColor0.Y, Res.EmitterColor0.Z);
        AnimVec[(int)EmitterAnimType.Color1] = new Vector3(Res.EmitterColor1.X, Res.EmitterColor1.Y, Res.EmitterColor1.Z);
        AnimVec[(int)EmitterAnimType.ParticleScale] = Res.PtclScale;
        AnimVec[(int)EmitterAnimType.EmitterVolumeScale] = Res.VolumeFormScale;
        AnimVec[(int)EmitterAnimType.EmissionRate] = new Vector3(Res.Rate, 0, 0);
        AnimVec[(int)EmitterAnimType.ParticleLife] = new Vector3(Res.Life, 0, 0);
        AnimVec[(int)EmitterAnimType.Alpha0] = new Vector3(Res.EmitterColor0.W, 0, 0);
        AnimVec[(int)EmitterAnimType.Alpha1] = new Vector3(Res.EmitterColor1.W, 0, 0);
        AnimVec[(int)EmitterAnimType.AllDirectionalVelocity] = new Vector3(Res.AllDirection, 0, 0);
        AnimVec[(int)EmitterAnimType.DesignatedDirectionalVelocity] = new Vector3(Res.DesignatedDirScale, 0, 0);
        AnimVec[(int)EmitterAnimType.GravityScale] = new Vector3(Res.GravityScale, 0, 0);

        EmitInterval = 0f; // the first emission is immediate
        EmitterPrevPos = Set.MatrixSrt.Translation;
        return true;
    }

    private int CalculateRequiredParticleCount()
    {
        EmitterResource d = Res;
        int required;
        if (d.IsEmitDistEnabled) required = d.EmitDistParticleMax;
        else
        {
            float maxEmitRatio = d.Rate;
            if (d.EmitterAnims[(int)EmitterAnimType.EmissionRate] is { Keys.Length: > 0 } rateTrack)
                maxEmitRatio = Math.Max(1f, rateTrack.Keys.Max(k => k.X));
            float maxLife = d.Life;
            if (d.EmitterAnims[(int)EmitterAnimType.ParticleLife] is { Keys.Length: > 0 } lifeTrack)
                maxLife = Math.Max(d.Life, lifeTrack.Keys.Max(k => k.X));

            bool oneTime = d.IsOneTime, infinite = d.IsLifeInfinity;
            bool streamOut = d.Calc == CalcType.GpuStreamOut;
            float emitTime = d.EmitDuration == 0 ? 1f : d.EmitDuration;
            int untilEmitEnd = (int)MathF.Ceiling(emitTime / (d.Interval + 1));
            int untilLifeEnd = (int)MathF.Ceiling(maxLife / (d.Interval + 1));
            int peak = infinite && oneTime ? untilEmitEnd : (oneTime && untilEmitEnd < untilLifeEnd ? untilEmitEnd : untilLifeEnd);
            required = (int)(MathF.Ceiling(peak * maxEmitRatio) + MathF.Ceiling(maxEmitRatio));

            int unison = 1;
            if (d.PrimEmit == PrimEmitType.Unison)
            {
                if (d.Volume == VolumeType.CircleEquallyDivided) unison = Math.Max(1, d.NumDivideCircle);
                else if (d.Volume == VolumeType.LineEquallyDivided) unison = Math.Max(1, d.NumDivideLine);
            }
            required *= unison;
            if (!oneTime && streamOut) required += (int)MathF.Ceiling(maxEmitRatio) * unison;
        }

        if (IsChild && !Res.EnableEmitterParticle && ParentEmitter is not null)
            required *= ParentEmitter.MaxParticleAttrFill;
        return required;
    }

    /// <summary>Emitter::CreateResMatrix: the resource SRT with its random ranges applied.</summary>
    public void CreateResMatrix()
    {
        Vector3 rotate = new(
            Res.Rotate.X + (Random.GetFloat() * 2f - 1f) * Res.RotateRand.X,
            Res.Rotate.Y + (Random.GetFloat() * 2f - 1f) * Res.RotateRand.Y,
            Res.Rotate.Z + (Random.GetFloat() * 2f - 1f) * Res.RotateRand.Z);
        Vector3 translate = new(
            Res.Trans.X + (Random.GetFloat() * 2f - 1f) * Res.TransRand.X,
            Res.Trans.Y + (Random.GetFloat() * 2f - 1f) * Res.TransRand.Y,
            Res.Trans.Z + (Random.GetFloat() * 2f - 1f) * Res.TransRand.Z);
        ResMatrixSrt = VfxMath.CreateSrtXyz(Res.Scale, rotate, translate);
        ResMatrixRt = VfxMath.CreateSrtXyz(Vector3.One, rotate, translate);
    }

    /// <summary>Emitter::UpdateByEmit.</summary>
    private void UpdateByEmit(ref float emitInterval)
    {
        emitInterval = (Res.Interval + 1f) + Random.GetInteger(Res.IntervalRandom);
        emitInterval *= EmitIntervalRatio;
        emitInterval *= Set.EmissionIntervalScale;
        if (Res.IsUpdateMatrixByEmit) CreateResMatrix();
    }

    // ---- fades (CalculateCurrentAlphaFadeValue / ScaleFadeValue)

    public float CurrentAlphaFadeValue()
    {
        float v = 1f;
        if (Res.IsAlphaFadeIn) v *= FadeInValue;
        if (Res.IsAlphaFadeOut) v *= FadeOutValue;
        return v;
    }

    public float CurrentScaleFadeValue()
    {
        float v = 1f;
        if (Res.IsScaleFadeIn) v *= FadeInValue;
        if (Res.IsScaleFadeOut) v *= FadeOutValue;
        return v;
    }

    public void Fade() => IsSoloFade = true;

    public Matrix4x4 GetParticleEmitterMatrix(int i) => Particles.GetBirthMatrix(i);

    /// <summary>Emitter-local position to world, according to the follow type.</summary>
    public Vector3 TransformToWorldPos(Vector3 p, int i) => Res.Follow switch
    {
        FollowType.EmitterFull => Vector3.Transform(p, MatrixSrt),
        FollowType.EmitterPosition => Vector3.TransformNormal(p, Particles.GetBirthMatrix(i)) + MatrixSrt.Translation,
        _ => Vector3.Transform(p, Particles.GetBirthMatrix(i))
    };

    public Vector3 TransformToWorldVec(Vector3 v, int i) => Res.Follow switch
    {
        FollowType.EmitterFull => Vector3.TransformNormal(v, MatrixRt),
        _ => Vector3.TransformNormal(v, VfxMath.MakeRt(Particles.GetBirthMatrix(i)))
    };

    /// <summary>World-space vector to emitter-local (inverse rotation), for world-oriented velocities.</summary>
    public Vector3 TransformToLocalVec(Vector3 v, int i)
    {
        Matrix4x4 rt = Res.Follow == FollowType.EmitterFull ? MatrixRt : VfxMath.MakeRt(Particles.GetBirthMatrix(i));
        return Vector3.TransformNormal(v, VfxMath.InvertAffine(rt));
    }
}
