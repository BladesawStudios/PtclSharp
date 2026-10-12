using System.Numerics;
using System.Runtime.InteropServices;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

public sealed partial class Emitter
{
    /// <summary>Size of the per-frame dynamic block (sysEmitterDynamicUniformBlock): 12 vec4.</summary>
    public const int DynamicBlockSize = 12 * 16;

    /// <summary>The block as of the last Calculate (the engine builds it just before the emitter clock advances).</summary>
    public readonly byte[] DynamicBlock = new byte[DynamicBlockSize];

    /// <summary>EmitterCalculator::MakeDynamicConstantBuffer.</summary>
    public void WriteDynamicBlock(Span<byte> dst)
    {
        Span<Vector4> v = MemoryMarshal.Cast<byte, Vector4>(dst);
        Vector4 setColor = Set.Color;
        Vector3 c0 = AnimVec[(int)EmitterAnimType.Color0], c1 = AnimVec[(int)EmitterAnimType.Color1];
        Vector4 color0 = new(Color0.X * c0.X * setColor.X, Color0.Y * c0.Y * setColor.Y, Color0.Z * c0.Z * setColor.Z,
            Color0.W * AnimVec[(int)EmitterAnimType.Alpha0].X);
        Vector4 color1 = new(Color1.X * c1.X * setColor.X, Color1.Y * c1.Y * setColor.Y, Color1.Z * c1.Z * setColor.Z,
            Color1.W * AnimVec[(int)EmitterAnimType.Alpha1].X);

        if (IsChild && ParentEmitter is not null)
        {
            if (Res.InheritAlpha0 && Res.InheritAlpha0EachFrame) color0.W *= ParentEmitter.AnimVec[(int)EmitterAnimType.Alpha0].X;
            if (Res.InheritAlpha1 && Res.InheritAlpha1EachFrame) color1.W *= ParentEmitter.AnimVec[(int)EmitterAnimType.Alpha0].X;
        }
        v[0] = color0;
        v[1] = color1;

        float alpha = setColor.W * CurrentAlphaFadeValue();
        if (((Res.InheritAlpha0 && Res.InheritAlpha0EachFrame) || (Res.InheritAlpha1 && Res.InheritAlpha1EachFrame)) && ParentEmitter is not null)
            alpha *= ParentEmitter.CurrentAlphaFadeValue();
        float fScale = CurrentScaleFadeValue();

        float frameRate = FrameRate;
        float time = Time;
        // MakeDynamicConstantBuffer runs before the clock advances; the caller passes the same convention
        v[2] = new Vector4(time, ParticleCount, SavingFrameRate, frameRate);
        v[3] = new Vector4(alpha, Set.ParticleScaleForCalculation.X * fScale, Set.ParticleScaleForCalculation.Y * fScale, Set.ParticleScaleForCalculation.Z * fScale);

        WriteMatrix(v, 4, MatrixSrt);
        WriteMatrix(v, 8, MatrixRt);
    }

    /// <summary>Matrix4x3 transposed to a 3x4 plus (0,0,0,1): row i holds component i of every axis.</summary>
    private static void WriteMatrix(Span<Vector4> v, int at, in Matrix4x4 m)
    {
        v[at] = new Vector4(m.M11, m.M21, m.M31, m.M41);
        v[at + 1] = new Vector4(m.M12, m.M22, m.M32, m.M42);
        v[at + 2] = new Vector4(m.M13, m.M23, m.M33, m.M43);
        v[at + 3] = new Vector4(0, 0, 0, 1);
    }
}
