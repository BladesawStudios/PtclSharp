using System.Numerics;

namespace PtclSharp.Simulation.Runtime;

/// <summary>
/// Struct-of-arrays particle state. The vec4 arrays are exactly the attribute buffers the engine hands to the particle vertex shader
/// (nn::vfx ParticleProperty): localPos.w = life, localVec.w = create time, scale.w = dynamics random, and so on.
/// </summary>
public sealed class ParticleStore
{
    public int Capacity { get; }

    public readonly Vector4[] LocalPos, LocalVec, LocalDiff, Scale, Random, InitRotate;
    public readonly Vector4[]? InitColor0, InitColor1;
    public readonly Vector4[]? Mat0, Mat1, Mat2;

    // ParticleData
    public readonly int[] CreateId;
    public readonly float[] CreateTime, Life;
    public readonly int[] CollisionCount;

    // ParentParticleData (only when the emitter has child emitter resources)
    public readonly Emitter?[][]? ChildEmitter;
    public readonly float[]? LightChildTime;
    public readonly float[][]? LightChildEmitCount, LightChildEmitSaving, LightChildEmitInterval;
    public readonly bool[][]? LightChildEmitDone;

    public ParticleStore(int capacity, bool hasInitColor, bool hasMatrices, int childSlots)
    {
        Capacity = Math.Max(capacity, 1);
        int n = Capacity;
        LocalPos = new Vector4[n]; LocalVec = new Vector4[n]; LocalDiff = new Vector4[n];
        Scale = new Vector4[n]; Random = new Vector4[n]; InitRotate = new Vector4[n];
        if (hasInitColor) { InitColor0 = new Vector4[n]; InitColor1 = new Vector4[n]; }
        if (hasMatrices) { Mat0 = new Vector4[n]; Mat1 = new Vector4[n]; Mat2 = new Vector4[n]; }
        CreateId = new int[n]; CreateTime = new float[n]; Life = new float[n]; CollisionCount = new int[n];
        if (childSlots > 0)
        {
            ChildEmitter = new Emitter?[n][];
            LightChildTime = new float[n];
            LightChildEmitCount = new float[n][]; LightChildEmitSaving = new float[n][]; LightChildEmitInterval = new float[n][];
            LightChildEmitDone = new bool[n][];
            for (int i = 0; i < n; i++)
            {
                ChildEmitter[i] = new Emitter?[childSlots];
                LightChildEmitCount[i] = new float[childSlots]; LightChildEmitSaving[i] = new float[childSlots];
                LightChildEmitInterval[i] = new float[childSlots]; LightChildEmitDone[i] = new bool[childSlots];
            }
        }
    }

    /// <summary>ParticleData::GetTime: age of particle i at emitter time <paramref name="emitterTime"/>.</summary>
    public float GetTime(int i, float emitterTime) => emitterTime - CreateTime[i];

    public Matrix4x4 GetBirthMatrix(int i)
    {
        Vector4 a = Mat0![i], b = Mat1![i], c = Mat2![i];
        return new Matrix4x4(a.X, b.X, c.X, 0, a.Y, b.Y, c.Y, 0, a.Z, b.Z, c.Z, 0, a.W, b.W, c.W, 1);
    }
}
