using System.Numerics;
using PtclSharp.Simulation.Random;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime;

/// <summary>A running effect (nn::vfx::EmitterSet): the shared transform and scales plus its live emitters.</summary>
public sealed class EmitterSetInstance
{
    public EffectSystem System { get; }
    public EmitterSetResource Resource { get; }
    public uint RandomSeed { get; }

    public Matrix4x4 MatrixSrt = Matrix4x4.Identity, MatrixRt = Matrix4x4.Identity;
    public bool IsSrtDirty = true;
    public bool IsFade;
    public bool EnableDistanceBasedEmission = true;

    // user-facing scales (defaults from EmitterSet::ResetCommon)
    public float RandomVel = 1f, DirectionalVel = 1f, AllDirectionalVel = 1f;
    public float EmissionRatioScale = 1f, EmissionIntervalScale = 1f, ParticleLifeScale = 1f;
    public Vector3 EmitterVolumeScale = Vector3.One, ParticleEmissionScale = Vector3.One, ParticleScaleForCalculation = Vector3.One;
    public Vector3 AdditionalVel;
    public Vector4 Color = Vector4.One;

    private readonly List<Emitter> _roots = [];
    private int _emitterCreateId;

    public IReadOnlyList<Emitter> Roots => _roots;
    public bool IsAlive => _roots.Count > 0;

    /// <summary>Every live emitter, parents first then their children.</summary>
    public IEnumerable<Emitter> AllEmitters
    {
        get
        {
            foreach (Emitter root in _roots)
            {
                yield return root;
                foreach (Emitter c in root.ChildLists.SelectMany(l => l)) yield return c;
            }
        }
    }

    internal EmitterSetInstance(EffectSystem system, EmitterSetResource resource)
    {
        System = system;
        Resource = resource;
        RandomSeed = system.GlobalRandom.GetU32();
        foreach (EmitterResource res in resource.Emitters)
        {
            Emitter? e = CreateEmitter(res, 0, null, -1);
            if (e is null) continue;
            foreach (var (childRes, k) in res.Children.Select((c, k) => (c, k)))
                if (!childRes.EnableEmitterParticle)
                {
                    Emitter? shared = CreateEmitter(childRes, 0, e, k);
                    if (shared is null) continue;
                    e.SharedChildHead[k] = shared;
                    shared.ParentEmitterCreateId = e.EmitterCreateId;
                    shared.ParentParticleLife = e.Res.Life;
                }
        }
    }

    public void SetMatrix(Matrix4x4 srt)
    {
        MatrixSrt = srt;
        MatrixRt = VfxMath.MakeRt(srt);
        IsSrtDirty = true;
    }

    public void SetPos(Vector3 pos)
    {
        MatrixSrt.Translation = pos;
        MatrixRt.Translation = pos;
        IsSrtDirty = true;
    }

    public void Fade() => IsFade = true;

    internal Emitter? CreateEmitter(EmitterResource res, int maxParticles, Emitter? parent, int childIndex)
    {
        Emitter? e = Emitter.Create(this, res, maxParticles, parent);
        if (e is null) return null;
        e.ChildResIndex = childIndex;
        e.EmitterCreateId = _emitterCreateId++;
        if (parent is null) _roots.Add(e);
        else parent.ChildLists[childIndex].Add(e);
        return e;
    }

    /// <summary>EmitterSet::Calculate.</summary>
    public void Calculate(float frameRate)
    {
        if (frameRate <= 0f)
        {
            IsSrtDirty = false;
            return;
        }
        foreach (Emitter root in _roots.ToArray())
        {
            bool alive = root.Calculate(frameRate, IsFade, true);
            int result = alive ? (int)Emitter.CalcResult.Alive : (int)Emitter.CalcResult.Kill;
            root.ChildLastResult = result;
            root.IsAliveChildren = false;

            foreach (List<Emitter> list in root.ChildLists)
                foreach (Emitter child in list.ToArray())
                {
                    bool childAlive = child.Calculate(frameRate, IsFade, true);
                    child.ChildLastResult = result;
                    if (!childAlive) { child.IsAlive = false; list.Remove(child); }
                    else root.IsAliveChildren = true;
                }

            if (!alive && !root.IsAliveChildren)
            {
                root.IsAlive = false;
                _roots.Remove(root);
            }
        }
        IsSrtDirty = false;
    }
}
