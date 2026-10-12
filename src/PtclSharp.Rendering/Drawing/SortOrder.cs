using System.Numerics;
using PtclSharp.Simulation.Runtime;

namespace PtclSharp.Rendering.Drawing;

/// <summary>The draw order of an emitter's particles for its sort type: by creation time either way, or far to near.</summary>
internal static class SortOrder
{
    /// <summary>The particle indices in draw order, or null when the emitter draws in slot order.</summary>
    public static int[]? For(Emitter emitter, in Matrix4x4 view)
    {
        byte sort = emitter.Res.SortType;
        if (sort == 0)
            return null;
        ParticleStore particles = emitter.Particles;
        var keyed = new List<(float Key, int Index)>(emitter.ParticleCount);
        for (int i = 0; i < emitter.ParticleCount; i++)
        {
            if (particles.CreateId[i] == 0)
                continue;
            float key = sort switch
            {
                1 => -particles.LocalVec[i].W,
                2 => particles.LocalVec[i].W,
                _ => ViewDepth(emitter, i, view)
            };
            keyed.Add((key, i));
        }
        keyed.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Index.CompareTo(b.Index));
        return keyed.Select(x => x.Index).ToArray();
    }

    // View z is negative in front of the camera, so a more negative z is farther, and the farthest draws first.
    static float ViewDepth(Emitter emitter, int index, in Matrix4x4 view)
    {
        Vector4 local = emitter.Particles.LocalPos[index];
        Vector3 world = emitter.TransformToWorldPos(new Vector3(local.X, local.Y, local.Z), index);
        return Vector3.Transform(world, view).Z;
    }
}
