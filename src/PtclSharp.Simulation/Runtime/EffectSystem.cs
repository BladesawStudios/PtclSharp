using System.Numerics;
using PtclSharp.Simulation.Random;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

/// <summary>Owner of the running effects and the global random generator (nn::vfx::System, reduced to the simulation).</summary>
public sealed class EffectSystem
{
    public SeadRandom GlobalRandom { get; private set; }
    private readonly List<EmitterSetInstance> _sets = [];

    /// <summary>Resolves a primitive id to mesh points and normals for primitive-volume emitters. Optional.</summary>
    public Func<ulong, IReadOnlyList<(Vector3 Position, Vector3 Normal)>?>? Primitives { get; set; }

    public IReadOnlyList<EmitterSetInstance> Sets => _sets;

    public EffectSystem(uint seed = 256) => GlobalRandom = new SeadRandom(seed);

    public void Reseed(uint seed) => GlobalRandom = new SeadRandom(seed);

    public EmitterSetInstance CreateEmitterSet(EmitterSetResource resource)
    {
        var set = new EmitterSetInstance(this, resource);
        _sets.Add(set);
        return set;
    }

    public void Kill(EmitterSetInstance set) => _sets.Remove(set);
    public void KillAll() => _sets.Clear();

    /// <summary>Advances every effect by <paramref name="frames"/> (1.0 = one game frame). Finished effects are removed.</summary>
    public void Calculate(float frames)
    {
        foreach (EmitterSetInstance s in _sets.ToArray())
        {
            s.Calculate(frames);
            if (!s.IsAlive) _sets.Remove(s);
        }
    }
}
