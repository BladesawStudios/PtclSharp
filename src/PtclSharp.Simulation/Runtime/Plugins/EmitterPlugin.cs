using System.Numerics;
using System.Runtime.InteropServices;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime.Plugins;

/// <summary>One vertex of plugin-built geometry: the three vec4 attributes (sysEmitterPluginAttr0..2) the stripe shaders read.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct PluginVertex
{
    public Vector4 Pos;   // xyz position, w signed half width
    public Vector4 Dir;   // xyz direction, w texture coordinate
    public Vector4 Emat;  // xyz up vector, w vertex index
}

/// <summary>One draw of plugin geometry: a triangle strip and the uniform block (sysEmitterPluginUniformBlock) that goes with it.</summary>
public sealed class PluginDraw
{
    public PluginVertex[] Vertices = [];
    /// <summary>Raw vertex bytes for plugins with a wider vertex (super stripe: five vec4); used instead of <see cref="Vertices"/> when set.</summary>
    public byte[]? Bytes;
    public int Stride = 48;
    public int Offset;
    public int Count;
    public byte[] Block = new byte[96];
}

/// <summary>
/// An emitter plugin (EP01 to EP04 chunks): geometry the CPU builds each frame from the emitter's particles. The simulation owns the
/// state and the vertex data; a renderer draws what <see cref="BuildDraws"/> returns.
/// </summary>
public abstract class EmitterPlugin
{
    protected Emitter Emitter { get; }
    protected EmitterPlugin(Emitter emitter) => Emitter = emitter;

    public virtual void OnParticleEmit(int index) { }
    public virtual void OnParticleCalculate(int index, float time, float life) { }
    public virtual void OnParticleRemove(int index, float time, float life) { }
    public virtual void PostCalculate() { }
    public virtual float ExtraOneTimeEndTime => 0f;
    public abstract IReadOnlyList<PluginDraw> BuildDraws();

    public static EmitterPlugin? Create(Emitter emitter)
    {
        PluginResource? p = emitter.Res.Plugin;
        if (p is null || emitter.Res.Calc != CalcType.Cpu) return null;
        return p.Type switch
        {
            1 => new ConnectionStripePlugin(emitter),
            2 => new StripePlugin(emitter),
            3 => new SuperStripePlugin(emitter),
            _ => null
        };
    }

    protected static float F(byte[] d, int at) => BitConverter.ToSingle(d, at);
    protected static int I(byte[] d, int at) => BitConverter.ToInt32(d, at);

    /// <summary>Cubic Hermite interpolation of a position between p1 and p2 with tangents t1 and t2.</summary>
    public static Vector3 Hermite(Vector3 p1, Vector3 t1, Vector3 p2, Vector3 t2, float s)
    {
        float s2 = s * s, s3 = s2 * s;
        return (((2 * s3) - (3 * s2) + 1) * p1) + ((s3 - (2 * s2) + s) * t1) + (((-2 * s3) + (3 * s2)) * p2) + ((s3 - s2) * t2);
    }

    public static Vector3 SubAndHalf(Vector3 a, Vector3 b) => (a - b) * 0.5f;
}
