using System.Numerics;
using System.Runtime.InteropServices;

namespace PtclSharp.Simulation.Runtime.Plugins;

/// <summary>One vertex of a super stripe: pos, dir, wing, tex (uvw) and emat, the five vec4 the shaders read as sysEmitterPluginAttr0..4.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SuperStripeVertex
{
    public Vector4 Pos, Dir, Wing, Tex, Emat;
}

/// <summary>
/// The super stripe (EP03, nn::vfx SuperStripeSystem): a history ribbon whose points keep their own velocity (acceleration, air resistance
/// and a wobbling initial speed), with distance based or uniform texture mapping.
/// </summary>
public sealed class SuperStripePlugin : EmitterPlugin
{
    private sealed class History
    {
        public Vector3 Pos, Vec, Dir, Wing, Emat;
        public float Scale;
    }

    private sealed class Stripe
    {
        public readonly List<History> History = []; // newest first
        public int HistoryCount, DelayedHistoryCount;
        public float Time, Life, PrevFrame, Journey;
        public Vector4 Random, Color0 = Vector4.One, Color1 = Vector4.One;
        public Vector3 HeadPos;
        public SuperStripeVertex[] Vertices = [];
        public int RenderVertexCount;
    }

    private readonly int _calcType, _option, _tex0, _tex1, _tex2, _numHistory, _numDivide, _uvMapType;
    private readonly bool _emitterFollow;
    private readonly float _headAlpha, _tailAlpha, _airResist, _vecRegulation, _vecInitSpeed;
    private readonly Vector3 _acceleration, _initVecCycle;
    private readonly Stripe?[] _byParticle;
    private readonly List<Stripe> _delayed = [];

    public SuperStripePlugin(Emitter emitter) : base(emitter)
    {
        byte[] d = emitter.Res.Plugin!.Data;
        _calcType = I(d, 0);
        _emitterFollow = I(d, 4) != 0;
        _option = I(d, 8);
        _tex0 = I(d, 0xC); _tex1 = I(d, 0x10); _tex2 = I(d, 0x14);
        _numHistory = Math.Max(2, (int)F(d, 0x18));
        _headAlpha = F(d, 0x20); _tailAlpha = F(d, 0x24);
        _numDivide = I(d, 0x28);
        _airResist = F(d, 0x34);
        _acceleration = new Vector3(F(d, 0x38), F(d, 0x3C), F(d, 0x40));
        _vecRegulation = F(d, 0x44);
        _vecInitSpeed = F(d, 0x48);
        _initVecCycle = new Vector3(F(d, 0x4C), F(d, 0x50), F(d, 0x54));
        _uvMapType = I(d, 0x58);
        _byParticle = new Stripe?[emitter.Particles.Capacity];
    }

    private bool Ribbon => _calcType == 3;
    public override float ExtraOneTimeEndTime => Emitter.FrameRate > 1f ? _numHistory * Emitter.FrameRate : _numHistory;

    public override void OnParticleEmit(int index) => _byParticle[index] = new Stripe { Random = Emitter.Particles.Random[index] };

    private Vector4 RawColor(int index, float time, float life, bool second)
    {
        Vector4 random = Emitter.Particles.Random[index];
        Vector4 c = second ? ParticleEval.Color1(Emitter.Res, random, Vector4.One, Vector3.One, 1f, life, time)
            : ParticleEval.Color0(Emitter.Res, random, Vector4.One, Vector3.One, 1f, life, time);
        if (second) { float s = BitConverter.ToSingle(Emitter.Res.FullData, 0x3b0); c.X *= s; c.Y *= s; c.Z *= s; }
        return c;
    }

    private float WorldScaleX(int index, float time, float life)
    {
        ParticleStore p = Emitter.Particles;
        Vector4 s = p.Scale[index];
        return ParticleEval.Scale(Emitter.Res, new Vector3(s.X, s.Y, s.Z), p.Random[index], life, time).X * Emitter.Set.ParticleScaleForCalculation.X;
    }

    private Vector3 Pos(int index)
    {
        Vector4 p = Emitter.Particles.LocalPos[index];
        Vector3 local = new(p.X, p.Y, p.Z);
        return _emitterFollow ? local : Emitter.TransformToWorldPos(local, index);
    }

    private Vector3 Vec(int index)
    {
        Vector4 v = Emitter.Particles.LocalVec[index];
        Vector3 local = new(v.X, v.Y, v.Z);
        return _emitterFollow ? local : Emitter.TransformToWorldVec(local, index);
    }

    private static Vector3 Norm(Vector3 v) => v.LengthSquared() > 0 ? Vector3.Normalize(v) : v;

    public override void OnParticleCalculate(int index, float time, float life)
    {
        Stripe? s = _byParticle[index];
        if (s is null) return;
        s.Time = time;
        s.Life = life;
        s.RenderVertexCount = 0;
        s.Color0 = RawColor(index, time, life, false);
        s.Color1 = RawColor(index, time, life, true);

        bool push = MathF.Floor(time) - MathF.Floor(s.PrevFrame) > 0 || time == 0f;
        s.PrevFrame = time;
        UpdateHistory(s, index, push);

        if (_numDivide == 0) MakePolygon(s, 0f);
        else MakePolygonWithDivision(s, 0f);
    }

    private (float air, Vector3 accel) Physics()
    {
        float frameRate = Emitter.FrameRate;
        float air = _airResist + ((1f - _airResist) * (1f - frameRate));
        return (air, _acceleration * (frameRate * air));
    }

    private void MoveOlderHistories(Stripe s, int firstOlder)
    {
        float frameRate = Emitter.FrameRate;
        var (air, accel) = Physics();
        for (int i = firstOlder; i < s.HistoryCount && i < s.History.Count; i++)
        {
            History h = s.History[i];
            h.Vec = (h.Vec * air) + accel;
            h.Pos += h.Vec * frameRate;
        }
    }

    private Vector3 InitialVec(Stripe s)
    {
        Vector4 r = s.Random;
        float t = s.Time;
        Vector3 cyc = _initVecCycle * _vecRegulation;
        static float Sin(float deg) => MathF.Sin(deg * (MathF.PI / 180f));
        return new Vector3(
            Sin((t * cyc.X) + (360f * r.X)) + Sin((t * 0.5f) + (360f * r.Y)),
            Sin((t * cyc.Y) + (360f * r.Y)) + Sin((t * 0.5f) + (360f * r.Z)),
            Sin((t * cyc.Z) + (360f * r.Z)) + Sin((t * 0.5f) + (360f * r.W))) * _vecInitSpeed;
    }

    private Vector3 EmitterUp => new(Emitter.MatrixSrt.M21, Emitter.MatrixSrt.M22, Emitter.MatrixSrt.M23);

    private void UpdateHistory(Stripe s, int index, bool push)
    {
        float frameRate = Emitter.FrameRate;
        if (!push)
        {
            if (s.History.Count == 0) return;
            History now = s.History[0];
            Vector3 prevPos = now.Pos;
            now.Pos = Pos(index);
            s.HeadPos = now.Pos;
            s.Journey += (now.Pos - prevPos).Length();
            now.Scale = WorldScaleX(index, s.Time, s.Life);
            now.Emat = EmitterUp;
            MoveOlderHistories(s, 1);
            return;
        }

        if (!Ribbon)
        {
            var now = new History { Pos = Pos(index), Scale = WorldScaleX(index, s.Time, s.Life) };
            History? prev = s.History.Count > 0 ? s.History[0] : null;
            s.History.Insert(0, now);
            s.HeadPos = now.Pos;
            s.Journey += s.HistoryCount == 0 ? now.Pos.Length() : (now.Pos - prev!.Pos).Length();
            now.Emat = _emitterFollow ? Vector3.UnitY : EmitterUp;
            now.Vec = InitialVec(s);
            if (s.HistoryCount < _numHistory - 1) s.HistoryCount++;

            if (s.HistoryCount <= 1)
            {
                now.Dir = Vec(index);
                if (now.Dir.LengthSquared() > 0) { now.Dir = Vector3.Normalize(now.Dir); now.Wing = now.Dir; }
                else
                {
                    Vector3 worldDiff = Emitter.MatrixSrt.Translation - Emitter.EmitterPrevPos;
                    if (worldDiff.LengthSquared() > 0) { now.Dir = Vector3.Normalize(worldDiff); now.Wing = now.Dir; }
                    else { now.Dir = new Vector3(0, 0.001f, 0); now.Wing = now.Dir; }
                }
            }
            else
            {
                now.Dir = now.Pos - prev!.Pos;
                if (s.HistoryCount == 2) prev.Dir = now.Dir;
                Vector3 average = Vector3.Zero;
                for (int i = 0; i < s.HistoryCount && i < s.History.Count; i++) average += s.History[i].Dir * (s.HistoryCount - i);
                average = Norm(average);
                now.Wing = average;
                if (s.HistoryCount == 2) prev.Wing = now.Wing;
            }
            now.Wing = now.Wing.LengthSquared() > 0 ? Vector3.Normalize(now.Wing) : Vector3.UnitY;
            MoveOlderHistories(s, 1);
        }
        else
        {
            const float eps = 0.00001f;
            var now = new History();
            History? prev = s.History.Count > 0 ? s.History[0] : null;
            s.History.Insert(0, now);
            if (s.HistoryCount < _numHistory - 1) s.HistoryCount++;
            now.Pos = Pos(index);
            s.HeadPos = now.Pos;
            MoveOlderHistories(s, 1);
            now.Vec = InitialVec(s);
            now.Emat = _emitterFollow ? Vector3.UnitY : EmitterUp;
            now.Scale = WorldScaleX(index, s.Time, s.Life);

            if (s.HistoryCount == 1)
            {
                now.Dir = Vec(index) + (_acceleration * s.Life);
                if (now.Dir.LengthSquared() < eps) { now.Dir = Vector3.UnitY; now.Wing = Vector3.UnitX; }
                else
                {
                    Vector3 dir = Vector3.Normalize(now.Dir);
                    float dd = Vector3.Dot(dir, Vector3.UnitY);
                    now.Wing = MathF.Abs(MathF.Abs(dd) - 1f) < eps ? Norm(Vector3.UnitX * dd) : Norm(Vector3.Cross(dir, Vector3.UnitY));
                }
            }
            else
            {
                now.Dir = now.Pos - prev!.Pos;
                if (now.Dir.LengthSquared() < eps)
                {
                    Vector3 prevVec = now.Pos - prev.Pos;
                    if (prevVec.LengthSquared() < eps) { now.Wing = prev.Wing; now.Dir = prev.Dir; }
                    else
                    {
                        Vector3 dir = Vector3.Normalize(prevVec);
                        float dd = Vector3.Dot(dir, Vector3.UnitY);
                        now.Wing = MathF.Abs(MathF.Abs(dd) - 1f) < eps ? Norm(Vector3.UnitX * dd) : Norm(Vector3.Cross(dir, Vector3.UnitY));
                    }
                }
                else
                {
                    Vector3 nowDir = Norm(now.Dir), prevDir = Norm(prev.Dir), prevWing = Norm(prev.Wing);
                    Vector3 prevNormal = Norm(Vector3.Cross(prevWing, prevDir));
                    now.Wing = Norm(Vector3.Cross(nowDir, prevNormal));
                }
                if (s.HistoryCount == 2)
                {
                    prev.Wing = now.Wing;
                    prev.Dir = now.Dir;
                    prev.Wing = Norm(prev.Wing);
                    prev.Dir = Norm(prev.Dir);
                }
            }
            now.Dir = Norm(now.Dir);
            now.Wing = Norm(now.Wing);
            s.Journey += s.HistoryCount == 1 ? now.Pos.Length() : (now.Pos - prev!.Pos).Length();
        }
        if (s.History.Count > _numHistory) s.History.RemoveRange(_numHistory, s.History.Count - _numHistory);
        _ = frameRate;
    }

    public override void OnParticleRemove(int index, float time, float life)
    {
        Stripe? s = _byParticle[index];
        if (s is null) return;
        s.DelayedHistoryCount = s.HistoryCount;
        s.Color0 = RawColor(index, time, life, false);
        s.Color1 = RawColor(index, time, life, true);
        _delayed.Add(s);
        _byParticle[index] = null;
    }

    public override void PostCalculate()
    {
        float frameRate = Emitter.FrameRate;
        var (air, _) = Physics();
        for (int i = _delayed.Count - 1; i >= 0; i--)
        {
            Stripe s = _delayed[i];
            foreach (History h in s.History.Take(s.HistoryCount))
            {
                h.Vec = (h.Vec + (_acceleration * frameRate)) * air;
            }
            foreach (History h in s.History.Take(s.HistoryCount)) h.Pos += h.Vec * frameRate;

            s.Time += frameRate;
            bool remove = MathF.Floor(s.Time) - MathF.Floor(s.PrevFrame) > 0;
            s.PrevFrame = s.Time;
            if (s.HistoryCount > 1)
            {
                if (remove) s.HistoryCount--;
                float uvIndexOffset = s.DelayedHistoryCount - s.HistoryCount;
                if (_numDivide == 0) MakePolygon(s, uvIndexOffset);
                else MakePolygonWithDivision(s, uvIndexOffset * (_numDivide + 1));
            }
            else _delayed.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- polygon building

    private float TextureOffsetUniform(int option, int index, int vertexCount, int maxVertexCount, float indexOffset)
    {
        if (option == 0) return index / (float)Math.Max(vertexCount - 1, 1);
        float offset = (index + indexOffset) / Math.Max(maxVertexCount - 1, 1);
        return MathF.Min(offset, 1f);
    }

    private float TextureOffsetDistance(int option, int index, float prevOffset, float delta, float journey, float invLength)
    {
        if (option == 0) return index == 0 ? 0f : prevOffset + (delta * invLength);
        const float unit = 0.1f;
        return index == 0 ? journey * unit : prevOffset + (-delta * unit);
    }

    private Vector3 TextureUv(Stripe s, int index, int vertexCount, int maxVertexCount, float indexOffset, float delta, Vector3 prevTex, float invLength)
    {
        if (_uvMapType == 0)
            return new Vector3(TextureOffsetUniform(_tex0, index, vertexCount, maxVertexCount, indexOffset),
                TextureOffsetUniform(_tex1, index, vertexCount, maxVertexCount, indexOffset),
                TextureOffsetUniform(_tex2, index, vertexCount, maxVertexCount, indexOffset));
        return new Vector3(TextureOffsetDistance(_tex0, index, prevTex.X, delta, s.Journey, invLength),
            TextureOffsetDistance(_tex1, index, prevTex.Y, delta, s.Journey, invLength),
            TextureOffsetDistance(_tex2, index, prevTex.Z, delta, s.Journey, invLength));
    }

    private static float Fract(float x) => x - MathF.Floor(x);

    private static void Put(SuperStripeVertex[] v, int index, Vector3 pos, Vector3 dir, Vector3 wing, Vector3 tex, float scale, Vector3 emat)
    {
        int l = index * 2;
        v[l] = new SuperStripeVertex { Pos = new Vector4(pos, scale), Dir = new Vector4(dir, index), Wing = new Vector4(wing, 0), Tex = new Vector4(tex, 0), Emat = new Vector4(emat, 0) };
        v[l + 1] = new SuperStripeVertex { Pos = new Vector4(pos, -scale), Dir = new Vector4(dir, index), Wing = new Vector4(wing, 0), Tex = new Vector4(tex, 0), Emat = new Vector4(emat, 0) };
    }

    private float StripeLengthInv(Stripe s)
    {
        float len = 0;
        for (int i = 1; i < s.HistoryCount && i < s.History.Count; i++) len += (s.History[i - 1].Pos - s.History[i].Pos).Length();
        return len > 0 ? 1f / len : 0f;
    }

    private void MakePolygon(Stripe s, float indexOffset)
    {
        int count = Math.Min(s.HistoryCount, s.History.Count);
        if (count <= 1) { s.RenderVertexCount = 0; return; }
        if (s.Vertices.Length < count * 2) s.Vertices = new SuperStripeVertex[count * 2];
        float invLen = _uvMapType == 1 ? StripeLengthInv(s) : 0f;
        float scaleFade = Emitter.CurrentScaleFadeValue();
        float texOffset = Fract(s.Time) + indexOffset;
        for (int index = 0; index < count; index++)
        {
            History h = s.History[index];
            float delta = index > 0 ? (s.History[index - 1].Pos - h.Pos).Length() : 0f;
            Vector3 prevTex = index > 0 ? new Vector3(s.Vertices[(index * 2) - 2].Tex.X, s.Vertices[(index * 2) - 2].Tex.Y, s.Vertices[(index * 2) - 2].Tex.Z) : Vector3.Zero;
            Vector3 uv = TextureUv(s, index, count, _numHistory, texOffset, delta, prevTex, invLen);
            Put(s.Vertices, index, h.Pos, h.Dir, h.Wing, uv, h.Scale * scaleFade, h.Emat);
        }
        s.RenderVertexCount = count * 2;
    }

    private void MakePolygonWithDivision(Stripe s, float indexOffset)
    {
        int count = Math.Min(s.HistoryCount, s.History.Count);
        if (count <= 2) { s.RenderVertexCount = 0; return; }
        int div = _numDivide;
        int drawCount = count + ((count - 1) * div);
        if (s.Vertices.Length < drawCount * 2) s.Vertices = new SuperStripeVertex[drawCount * 2];
        float invLen = _uvMapType == 1 ? StripeLengthInv(s) : 0f;
        float scaleFade = Emitter.CurrentScaleFadeValue();
        float texOffset = Fract(s.Time) + indexOffset;
        float indexDiv = 1f / (1 + div);
        int maxVertex = _numHistory + ((_numHistory - 1) * div);
        Vector3 prevVertexPos = Vector3.Zero;
        for (int index = 0; index < drawCount; index++)
        {
            float fIndex = index * indexDiv;
            int hp = Math.Min((int)MathF.Floor(fIndex), count - 1);
            float inner = fIndex - hp;
            History h = s.History[hp];
            History? next = hp + 1 < count ? s.History[hp + 1] : null;
            Vector3 pos = h.Pos, dir = h.Dir, emat = h.Emat;
            if (next is not null && inner > 0f)
            {
                History? prev = hp > 0 ? s.History[hp - 1] : null;
                History? next2 = hp + 2 < count ? s.History[hp + 2] : null;
                Vector3 t1 = (next.Pos - (prev?.Pos ?? h.Pos)) * 0.5f;
                Vector3 t2 = ((next2?.Pos ?? next.Pos) - h.Pos) * 0.5f;
                pos = Hermite(h.Pos, t1, next.Pos, t2, inner);
                dir = Vector3.Lerp(h.Dir, next.Dir, inner);
                emat = Norm(Vector3.Lerp(h.Emat, next.Emat, inner));
            }
            float delta = index > 0 ? (pos - prevVertexPos).Length() : 0f;
            Vector3 prevTex = index > 0 ? new Vector3(s.Vertices[(index * 2) - 2].Tex.X, s.Vertices[(index * 2) - 2].Tex.Y, s.Vertices[(index * 2) - 2].Tex.Z) : Vector3.Zero;
            Vector3 uv = TextureUv(s, index, drawCount, maxVertex, texOffset, delta, prevTex, invLen);
            prevVertexPos = pos;
            Put(s.Vertices, index, pos, dir, h.Wing, uv, h.Scale * scaleFade, emat);
        }
        s.RenderVertexCount = drawCount * 2;
    }

    private byte[] Block(Stripe s, int meshType)
    {
        var v = new Vector4[6];
        v[0] = s.Random;
        v[1] = new Vector4(_headAlpha, _tailAlpha, 0, 0);
        v[2] = s.Color0;
        v[3] = s.Color1;
        v[4] = new Vector4(s.Time, s.HistoryCount + (_numDivide * (s.HistoryCount - 1)), meshType, s.Life);
        v[5] = new Vector4(s.HeadPos, 0);
        return MemoryMarshal.AsBytes(v.AsSpan()).ToArray();
    }

    public override IReadOnlyList<PluginDraw> BuildDraws()
    {
        var draws = new List<PluginDraw>();
        foreach (Stripe s in _byParticle.Where(x => x is not null).Select(x => x!).Concat(_delayed))
        {
            if (s.RenderVertexCount == 0) continue;
            byte[] bytes = MemoryMarshal.AsBytes(s.Vertices.AsSpan(0, s.RenderVertexCount)).ToArray();
            draws.Add(new PluginDraw { Bytes = bytes, Stride = 80, Count = s.RenderVertexCount, Block = Block(s, 0) });
            if (_option == 1) draws.Add(new PluginDraw { Bytes = bytes, Stride = 80, Count = s.RenderVertexCount, Block = Block(s, 1) });
        }
        return draws;
    }
}
