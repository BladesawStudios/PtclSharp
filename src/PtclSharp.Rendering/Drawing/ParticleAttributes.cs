using System.Numerics;
using PtclSharp.Simulation.Runtime;

namespace PtclSharp.Rendering.Drawing;

/// <summary>Which per-particle stream a shader attribute reads (<c>sysLocalPosAttr</c> and its siblings).</summary>
internal static class ParticleAttributes
{
    public static Vector4[]? For(ParticleStore particles, string attribute)
    {
        if (attribute.Contains("LocalPos", StringComparison.Ordinal))
            return particles.LocalPos;
        if (attribute.Contains("LocalVec", StringComparison.Ordinal))
            return particles.LocalVec;
        if (attribute.Contains("LocalDiff", StringComparison.Ordinal))
            return particles.LocalDiff;
        if (attribute.Contains("Scale", StringComparison.Ordinal))
            return particles.Scale;
        if (attribute.Contains("Random", StringComparison.Ordinal))
            return particles.Random;
        if (attribute.Contains("Rot", StringComparison.Ordinal))
            return particles.InitRotate;
        if (attribute.Contains("Color0", StringComparison.Ordinal))
            return particles.InitColor0;
        if (attribute.Contains("Color1", StringComparison.Ordinal))
            return particles.InitColor1;
        if (attribute.Contains("EmtMat0", StringComparison.Ordinal))
            return particles.Mat0;
        if (attribute.Contains("EmtMat1", StringComparison.Ordinal))
            return particles.Mat1;
        if (attribute.Contains("EmtMat2", StringComparison.Ordinal))
            return particles.Mat2;
        return null;
    }
}
