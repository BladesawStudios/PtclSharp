using System.Numerics;

namespace PtclSharp.Simulation.Resources;

public sealed class FieldGpuNoise
{
    public bool EnableUnifiedPhase, EnableDetailedOption, EnableAirResist;
    public Vector3 RandomVel;
    public float Blank, UnifiedPhaseSpeed, UnifiedPhaseDistribution;
    public float[] WaveParam = new float[4], WaveParamHzRate = new float[4];
    public Anim8Key Anim = new();
}

public sealed class FieldRandomSimple { public Vector3 RandomVel; public uint Blank; public Anim8Key Anim = new(); }
public sealed class FieldMagnet { public bool FollowEmitter, AxisX, AxisY, AxisZ; public float Power; public Vector3 Position; public Anim8Key Anim = new(); }
public sealed class FieldSpin { public float Rotate, Outer; public int Axis; public Anim8Key RotateAnim = new(), OuterAnim = new(); }
public sealed class FieldCollision { public byte ReactionType; public bool IsWorld; public float PlaneY, Restitution, Drag; public int MaxCollisions; }
public sealed class FieldConvergence { public byte Type; public Vector3 Position; public float Ratio; public Anim8Key Anim = new(); }
public sealed class FieldPosAdd { public bool IsGlobal; public Vector3 PositionAdd; public Anim8Key Anim = new(); }
public sealed class FieldCurlNoise { public byte Interpolation, BaseRandom, WorldCoordinate; public Vector3 Speed, Influence; public float Scale, Base; }

public sealed class TextureSlot
{
    public ulong Guid = ulong.MaxValue;
    public byte WrapU, WrapV, Filter;
    public bool IsValid => Guid != ulong.MaxValue;
}

/// <summary>The plugin chunk (EP01..EP04) of an emitter, raw payload kept for the stripe/area-loop systems.</summary>
public sealed class PluginResource { public int Type; public byte[] Data = []; }

/// <summary>
/// A game-neutral view of one EMTR: everything the CPU simulation needs, plus the static uniform block that the particle shaders
/// read. Built per game by an <see cref="IEmitterResourceReader"/>; the simulation never touches game offsets.
/// </summary>
public sealed class EmitterResource
{
    public string Name = "";
    public EmitterSetResource? Set;
    public EmitterResource? Parent;
    public List<EmitterResource> Children = [];
    public bool IsChild => Parent is not null;

    // --- emitter ---
    public CalcType Calc;
    public FollowType Follow;
    public byte SortType;
    public uint DrawPath;
    public int CustomActionIndex, CustomShaderIndex;
    public bool IsFadeEmit, IsAlphaFadeOut, IsScaleFadeOut, IsAlphaFadeIn, IsScaleFadeIn;
    public int FadeOutTime, FadeInTime;
    public byte RandomSeedType; // 0 global, 1 emitter set, 2 fixed
    public uint FixedSeed;
    public bool IsUpdateMatrixByEmit;
    public Vector3 Trans, TransRand, Rotate, RotateRand, Scale = Vector3.One;
    public Vector4 EmitterColor0 = Vector4.One, EmitterColor1 = Vector4.One;

    // --- emission ---
    public bool IsOneTime;
    public bool IsWorldGravity;
    public bool IsEmitDistEnabled;
    public bool IsWorldOrientedVelocity;
    public int Start, EmitTiming, EmitDuration;
    public float Rate;
    public int RateRandomPercent, Interval, IntervalRandom;
    public float PosRand;
    public float GravityScale;
    public Vector3 GravityDir;
    public float EmitDistUnit, EmitDistMin, EmitDistMax, EmitDistMargin;
    public int EmitDistParticleMax;

    // --- volume ---
    public VolumeType Volume;
    public bool SweepStartRandom;
    public byte ArcType;
    public float SweepLongitude, SweepLatitude, SweepStart, SurfacePosRand, CaliberRatio, LineCenter, LineLength;
    public Vector3 VolumeRadius, VolumeFormScale = Vector3.One;
    public PrimEmitType PrimEmit;
    public int NumDivideCircle, NumDivideLine, NumDivideCircleRandom, NumDivideLineRandom;
    public int VolumeTblIndex, VolumeTblIndex64, LatitudeDir;
    public ulong VolumePrimitiveId = ulong.MaxValue;

    // --- velocity ---
    public float AllDirection, DesignatedDirScale, DiffusionDirAngle, XzDiffusion, VelRandomPercent, EmVelInherit;
    public Vector3 DesignatedDir, Diffusion;

    // --- particle ---
    public int Life, LifeRandomPercent;
    public bool IsLifeInfinity;
    public float DynamicsRand;
    public Vector3 PtclScale = Vector3.One, PtclScaleRandom;

    // --- rotation (particle) ---
    public Vector3 RotInit, RotInitRand, RotAdd, RotAddRand;
    public float RotRegist = 1;
    public bool RotRevRandX, RotRevRandY, RotRevRandZ;

    // --- inherit (child emitters) ---
    public bool InheritVelocity, InheritScale, InheritRotate, InheritColor0, InheritColor1, InheritAlpha0, InheritAlpha1;
    public bool InheritAlpha0EachFrame, InheritAlpha1EachFrame, EnableEmitterParticle, ChildPreDraw;
    public float InheritVelocityRate, InheritScaleRate;

    // --- animation ---
    public EmitterAnimTrack?[] EmitterAnims = new EmitterAnimTrack?[(int)EmitterAnimType.Count];
    public bool IsEmitterSrtAnim, IsUseEmitterAnim;

    // --- fields / plugins ---
    public FieldGpuNoise? FieldGpuNoise;
    public FieldRandomSimple? FieldRandomSimple;
    public FieldMagnet? FieldMagnet;
    public FieldSpin? FieldSpin;
    public FieldCollision? FieldCollision;
    public FieldConvergence? FieldConvergence;
    public FieldPosAdd? FieldPosAdd;
    public FieldCurlNoise? FieldCurlNoise;
    public bool HasCustomField;
    public bool IsUseField => FieldGpuNoise is not null || FieldRandomSimple is not null || FieldMagnet is not null || FieldSpin is not null
        || FieldCollision is not null || FieldConvergence is not null || FieldPosAdd is not null || FieldCurlNoise is not null || HasCustomField;
    public PluginResource? Plugin;

    // --- shading ---
    public float AirRegist = 1;
    public int ShaderIndex = -1, ShaderIndexPass1 = -1, ShaderIndexPass2 = -1;
    public int ComputeShader0 = -1, ComputeShader1 = -1, ComputeShader2 = -1;
    public ulong G3dPrimitiveId = ulong.MaxValue;
    public TextureSlot[] Textures = [];
    public RenderState Render = new();

    /// <summary>The per-emitter static uniform block (sysEmitterStaticUniformBlock), after the UpdateParams fix-ups.</summary>
    public byte[] StaticBlock = [];

    /// <summary>The whole fixed EMTR block after the UpdateParams fix-ups (the static block is its first 0x750 bytes).</summary>
    public byte[] FullData = [];

    /// <summary>
    /// The camera distances (EMTR 0x5D0 and 0x5D4) between which the emitter's vertex shader fades it in: invisible nearer than the first, fully visible
    /// beyond the second. Read from the shader's fade expression (<c>(distance - 0x5D0) / (0x5D4 - 0x5D0)</c>), which the field table still lists as unverified.
    /// </summary>
    public (float Start, float End) NearFade;

    /// <summary>Raw custom-shader uniform block (CSDP), copied verbatim to the GPU.</summary>
    public byte[] CustomShaderBlock = [];

    // --- derived at load ---
    public Matrix4x4 ResMatrixSrt = Matrix4x4.Identity, ResMatrixRt = Matrix4x4.Identity;

    public int ChildCount => Children.Count;
}

public sealed class RenderState
{
    public bool BlendEnable, DepthTest, DepthWrite;
    public byte DepthFunc, BlendMode, CullMode;
}

public sealed class EmitterSetResource
{
    public string Name = "";
    public List<EmitterResource> Emitters = [];
    public IEnumerable<EmitterResource> AllEmitters => Emitters.SelectMany(Flatten);
    private static IEnumerable<EmitterResource> Flatten(EmitterResource e) => new[] { e }.Concat(e.Children.SelectMany(Flatten));
}
