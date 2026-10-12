using System.Numerics;
using System.Runtime.InteropServices;

namespace PtclSharp.Simulation.Runtime.Plugins;

/// <summary>
/// The connection stripe (EP01, nn::vfx ConnectionStripeSystem): one ribbon through all of the emitter's live particles, oldest first.
/// </summary>
public sealed class ConnectionStripePlugin : EmitterPlugin
{
    private readonly int _calcType, _option, _numDivide, _connectionType;
    private readonly float _headAlpha, _tailAlpha;
    private readonly Vector4 _random;
    private PluginVertex[] _vertices = [];
    private int _renderVertexCount;

    public ConnectionStripePlugin(Emitter emitter) : base(emitter)
    {
        byte[] d = emitter.Res.Plugin!.Data;
        _calcType = I(d, 0);
        _option = I(d, 8);
        _numDivide = I(d, 0x10);
        _connectionType = I(d, 0x14);
        _headAlpha = F(d, 0x18);
        _tailAlpha = F(d, 0x1C);
        _random = new Vector4(emitter.Random.GetFloat(), emitter.Random.GetFloat(), emitter.Random.GetFloat(), emitter.Random.GetFloat());
    }

    private Vector3 WorldPos(int i)
    {
        Vector4 p = Emitter.Particles.LocalPos[i];
        return Emitter.TransformToWorldPos(new Vector3(p.X, p.Y, p.Z), i);
    }

    private float ScaleX(int i)
    {
        ParticleStore p = Emitter.Particles;
        float time = p.GetTime(i, Emitter.Time), life = p.Life[i];
        Vector4 s = p.Scale[i];
        return ParticleEval.Scale(Emitter.Res, new Vector3(s.X, s.Y, s.Z), p.Random[i], life, time).X * Emitter.Set.ParticleScaleForCalculation.X;
    }

    private Vector3 EmatY(int i)
    {
        if (_calcType == 0) return Vector3.Zero;
        if (Emitter.Res.Follow == Resources.FollowType.EmitterFull) return new Vector3(Emitter.MatrixSrt.M21, Emitter.MatrixSrt.M22, Emitter.MatrixSrt.M23);
        Matrix4x4 m = Emitter.Particles.GetBirthMatrix(i);
        return new Vector3(m.M21, m.M22, m.M23);
    }

    public override void PostCalculate()
    {
        _renderVertexCount = 0;
        ParticleStore p = Emitter.Particles;
        var order = new List<int>();
        for (int i = 0; i < Emitter.ParticleCount; i++) if (p.CreateId[i] != 0) order.Add(i);
        order.Sort((a, b) => p.LocalVec[a].W != p.LocalVec[b].W ? p.LocalVec[a].W.CompareTo(p.LocalVec[b].W) : a.CompareTo(b));
        int n = order.Count;
        if (n == 0) return;

        int baseCount = n + (_connectionType is 1 or 2 ? 1 : 0);
        int wholeCount = baseCount + ((baseCount - 1) * _numDivide);
        float scaleFade = Emitter.CurrentScaleFadeValue();
        int loop = n - 1;
        bool ok = (_numDivide == 0 && baseCount >= 2 && !(_connectionType == 2 && n <= 1)) || (_numDivide > 0 && n >= 3);
        if (!ok) return;
        if (_vertices.Length < Math.Max(wholeCount, 2) * 2 + 4) _vertices = new PluginVertex[(Math.Max(wholeCount, 2) * 2) + 4];
        int attr = 0;

        void Add(Vector3 pos, Vector3 dir, Vector3 emat, float scale)
        {
            int l = attr * 2;
            float v = wholeCount > 1 ? attr / (float)(wholeCount - 1) : 0f;
            // the connection stripe packs vertices in the opposite order to the history stripe, so the sides swap
            _vertices[l] = new PluginVertex { Pos = new Vector4(pos, -scale), Dir = new Vector4(dir, v), Emat = new Vector4(emat, attr) };
            _vertices[l + 1] = new PluginVertex { Pos = new Vector4(pos, scale), Dir = new Vector4(dir, v), Emat = new Vector4(emat, attr) };
            attr++;
            _renderVertexCount += 2;
        }

        float inv = 1f / (_numDivide + 1);
        void Subdivide(Vector3 nowPos, Vector3 nowDir, Vector3 nextPos, Vector3 nextDir, Vector3 emat, float scale)
        {
            for (int k = 1; k <= _numDivide; k++)
            {
                float s = inv * k;
                Add(Hermite(nowPos, nowDir, nextPos, nextDir, s), Vector3.Lerp(nowDir, nextDir, s), emat, scale);
            }
        }

        if (_connectionType is 1 or 2)
        {
            Vector3 nowPos, nowDir, nextPos = WorldPos(order[0]);
            int idxNext = order[0];
            Vector3 emat;
            float scale;
            if (_connectionType == 2)
            {
                int idxNow = order[loop];
                nowPos = WorldPos(idxNow);
                emat = EmatY(idxNow);
                scale = ScaleX(idxNow) * scaleFade;
                nowDir = nextPos - nowPos;
                Vector3 nextDir = n > 1 ? WorldPos(order[1]) - nextPos : nowDir;
                if (_numDivide > 0) nextDir = n > 1 ? SubAndHalf(WorldPos(order[1]), nowPos) : nowDir;
                Add(nowPos, nowDir, emat, scale);
                if (_numDivide > 0) Subdivide(nowPos, nowDir, nextPos, nextDir, emat, scale);
            }
            else
            {
                emat = EmatY(idxNext);
                scale = ScaleX(idxNext) * scaleFade;
                nowPos = Emitter.MatrixSrt.Translation;
                nowDir = nextPos - nowPos;
                Vector3 nextDir = n > 1 ? SubAndHalf(WorldPos(order[1]), nowPos) : nowDir;
                Add(nowPos, nowDir, emat, scale);
                if (_numDivide > 0) Subdivide(nowPos, nowDir, nextPos, nextDir, emat, scale);
            }
        }

        for (int idx = 0; idx < loop; idx++)
        {
            int now = order[idx], next = order[idx + 1];
            Vector3 nowPos = WorldPos(now), nextPos = WorldPos(next), nowDir, nextDir = Vector3.Zero;
            Vector3 emat = EmatY(now);
            float scale = ScaleX(now) * scaleFade;
            if (_numDivide > 0)
            {
                if (idx == 0)
                {
                    Vector3 next2 = WorldPos(order[idx + 2]);
                    nowDir = nextPos - nowPos;
                    nextDir = SubAndHalf(next2, nowPos);
                }
                else if (idx == loop - 1)
                {
                    Vector3 prev = WorldPos(order[idx - 1]);
                    nowDir = SubAndHalf(nextPos, prev);
                    nextDir = (nextPos - nowPos) * 0.5f + ((nowPos - prev) * -0.25f);
                }
                else
                {
                    Vector3 prev = WorldPos(order[idx - 1]), next2 = WorldPos(order[idx + 2]);
                    nowDir = SubAndHalf(nextPos, prev);
                    nextDir = SubAndHalf(next2, nowPos);
                }
            }
            else nowDir = nextPos - nowPos;
            Add(nowPos, nowDir, emat, scale);
            if (_numDivide > 0) Subdivide(nowPos, nowDir, nextPos, nextDir, emat, scale);
        }

        if (n >= 2)
        {
            int idxNow = order[loop];
            Vector3 nowPos = WorldPos(idxNow), nowDir;
            if (_connectionType == 2) nowDir = WorldPos(order[0]) - nowPos;
            else nowDir = nowPos - WorldPos(order[loop - 1]);
            Add(nowPos, nowDir, EmatY(idxNow), ScaleX(idxNow) * scaleFade);
        }

    }

    private byte[] Block(int meshType)
    {
        var v = new Vector4[6];
        v[0] = _random;
        v[1] = new Vector4(_headAlpha, _tailAlpha, 0, 0);
        v[2] = Vector4.One;
        v[3] = Vector4.One;
        v[4] = new Vector4(Emitter.Time, _vertices.Length > 0 ? _renderVertexCount / 2 : 0, meshType, Emitter.Res.Life);
        return MemoryMarshal.AsBytes(v.AsSpan()).ToArray();
    }

    public override IReadOnlyList<PluginDraw> BuildDraws()
    {
        if (_renderVertexCount == 0) return [];
        var draws = new List<PluginDraw> { new() { Vertices = _vertices, Count = _renderVertexCount, Block = Block(0) } };
        if (_option == 1) draws.Add(new PluginDraw { Vertices = _vertices, Count = _renderVertexCount, Block = Block(1) });
        return draws;
    }
}
