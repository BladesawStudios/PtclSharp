namespace PtclSharp.Simulation.Resources;

public enum CalcType : byte { Cpu = 0, Gpu = 1, GpuStreamOut = 2 }

/// <summary>Particle coordinate mode (EMTR 0x753): 1 uses per-particle basis arrays (BotW 0x7100ae0e34 normalizes them for gravity).</summary>
public enum FollowType : byte { EmitterFull = 0, None = 1, EmitterPosition = 2 }

public enum VolumeType : byte
{
    Point = 0, Circle, CircleEquallyDivided, CircleFill, Sphere, SphereEqually32Divided, SphereEqually64Divided, SphereFill,
    Cylinder, CylinderFill, Box, BoxFill, Line, LineEquallyDivided, Rectangle, Primitive
}

public enum PrimEmitType : byte { Unison = 0, Random = 1, IndexOrder = 2 }

/// <summary>Indices into the emitter-animation lane array (EA* chunks, in resolve order of nn::vfx).</summary>
public enum EmitterAnimType
{
    Scale, Rotate, Translate, Color0, Color1, ParticleScale, EmitterVolumeScale, EmissionRate, ParticleLife,
    Alpha0, Alpha1, AllDirectionalVelocity, DesignatedDirectionalVelocity, GravityScale,
    Count
}
