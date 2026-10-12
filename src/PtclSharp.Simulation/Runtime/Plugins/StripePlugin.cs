using System.Numerics;
using System.Runtime.InteropServices;
using PtclSharp.Simulation.Resources;

namespace PtclSharp.Simulation.Runtime.Plugins;

/// <summary>
/// The history stripe (EP02, nn::vfx StripeSystem): every particle drags a ribbon behind it, built from its recent positions. A particle that
/// dies leaves its ribbon on a delay list, which shrinks one point per frame until it is gone.
/// </summary>
public sealed class StripePlugin : EmitterPlugin
{
    private sealed class History
    {
        public Vector3 Pos;
        public float Scale;
        public Vector3 Dir;
        public Vector3 Emat;
    }

    private sealed class Stripe
    {
        public readonly List<History> History = []; // newest first
        public float Time, Life, PrevFrame;
        public Vector4 Random, Color0 = Vector4.One, Color1 = Vector4.One;
        public Vector3 HeadPos, VInterpolate;
        public PluginVertex[] Vertices = [];
        public int RenderVertexCount;
    }

    private readonly int _calcType, _option, _texturing, _numDivide, _numHistory;
    private readonly bool _emitterFollow;
    private readonly float _headAlpha, _tailAlpha, _dirInterpolate;
    private readonly Stripe?[] _byParticle;
    private readonly List<Stripe> _delayed = [];

    public StripePlugin(Emitter emitter) : base(emitter)
    {
        byte[] d = emitter.Res.Plugin!.Data;
        _calcType = I(d, 0);
        _emitterFollow = I(d, 4) != 0;
        _option = I(d, 8);
        _texturing = I(d, 0xC);
        _numDivide = (int)F(d, 0x10);
        _numHistory = Math.Max(2, (int)F(d, 0x14));
        _headAlpha = F(d, 0x1C);
        _tailAlpha = F(d, 0x20);
        _dirInterpolate = F(d, 0x28);
        _byParticle = new Stripe?[emitter.Particles.Capacity];
    }

    public override float ExtraOneTimeEndTime => Emitter.FrameRate > 1f ? _numHistory * Emitter.FrameRate : _numHistory;

    public override void OnParticleEmit(int index)
    {
        Vector4 r = Emitter.Particles.Random[index];
        _byParticle[index] = new Stripe { Random = r };
    }

    private Vector4 RawColor(Stripe s, int index, float time, float life, bool second)
    {
        Vector4 random = Emitter.Particles.Random[index];
        Vector4 c = second
            ? ParticleEval.Color1(Emitter.Res, random, Vector4.One, Vector3.One, 1f, life, time)
            : ParticleEval.Color0(Emitter.Res, random, Vector4.One, Vector3.One, 1f, life, time);
        if (second)
        {
            float scale = BitConverter.ToSingle(Emitter.Res.FullData, 0x3b0);
            c.X *= scale; c.Y *= scale; c.Z *= scale;
        }
        return c;
    }

    private float WorldScaleX(int index, float time, float life)
    {
        ParticleStore p = Emitter.Particles;
        Vector4 s = p.Scale[index];
        Vector3 v = ParticleEval.Scale(Emitter.Res, new Vector3(s.X, s.Y, s.Z), p.Random[index], life, time);
        return v.X * Emitter.Set.ParticleScaleForCalculation.X;
    }

    public override void OnParticleCalculate(int index, float time, float life)
    {
        Stripe? stripe = _byParticle[index];
        if (stripe is null) return;
        ParticleStore p = Emitter.Particles;
        Vector3 localPos = new(p.LocalPos[index].X, p.LocalPos[index].Y, p.LocalPos[index].Z);
        Vector3 localVec = new(p.LocalVec[index].X, p.LocalVec[index].Y, p.LocalVec[index].Z);

        stripe.Time = time;
        stripe.Life = life;
        stripe.RenderVertexCount = 0;
        stripe.Color0 = RawColor(stripe, index, time, life, false);
        stripe.Color1 = RawColor(stripe, index, time, life, true);

        bool push = MathF.Floor(time) - MathF.Floor(stripe.PrevFrame) > 0 || time == 0f;
        stripe.PrevFrame = time;
        float scale = WorldScaleX(index, time, life);
        Vector3 emitterUp = new(Emitter.MatrixSrt.M21, Emitter.MatrixSrt.M22, Emitter.MatrixSrt.M23);

        if (!push)
        {
            if (stripe.History.Count > 0)
            {
                History h = stripe.History[0];
                h.Pos = _emitterFollow ? localPos : Emitter.TransformToWorldPos(localPos, index);
                h.Scale = scale;
                h.Emat = emitterUp;
            }
        }
        else
        {
            Vector3 pos = _emitterFollow ? localPos : Emitter.TransformToWorldPos(localPos, index);
            Vector3 vec = _emitterFollow ? localVec : Emitter.TransformToWorldVec(localVec, index);
            stripe.HeadPos = pos;
            stripe.History.Insert(0, new History { Pos = pos, Scale = scale, Dir = vec, Emat = _emitterFollow ? Vector3.UnitY : emitterUp });
            if (stripe.History.Count > _numHistory) stripe.History.RemoveAt(stripe.History.Count - 1);
        }

        if (stripe.History.Count >= 2)
        {
            History now = stripe.History[0], prev = stripe.History[1];
            Vector3 direction = now.Pos - prev.Pos;
            if (_dirInterpolate > 0)
            {
                if (stripe.Time < 2)
                {
                    stripe.VInterpolate = direction.LengthSquared() > 0 ? Vector3.Normalize(direction) : direction;
                    now.Dir = stripe.VInterpolate;
                }
                else
                {
                    if (direction.LengthSquared() > 0) direction = Vector3.Normalize(direction);
                    stripe.VInterpolate += (direction - stripe.VInterpolate) * _dirInterpolate;
                    if (stripe.VInterpolate.LengthSquared() > 0) stripe.VInterpolate = Vector3.Normalize(stripe.VInterpolate);
                    now.Dir = stripe.VInterpolate;
                }
            }
            else now.Dir = direction;
            if (stripe.History.Count == 2) prev.Dir = now.Dir;

            float texBase = _texturing == 0 ? stripe.History.Count : _texturing == 1 ? _numHistory : 1;
            MakePolygon(stripe, texBase);
        }
    }

    public override void OnParticleRemove(int index, float time, float life)
    {
        Stripe? stripe = _byParticle[index];
        if (stripe is null) return;
        stripe.Color0 = RawColor(stripe, index, time, life, false);
        stripe.Color1 = RawColor(stripe, index, time, life, true);
        _delayed.Add(stripe);
        _byParticle[index] = null;
    }

    public override void PostCalculate()
    {
        float frameRate = Emitter.FrameRate;
        for (int i = _delayed.Count - 1; i >= 0; i--)
        {
            Stripe s = _delayed[i];
            s.Time += frameRate;
            bool remove = MathF.Floor(s.Time) - MathF.Floor(s.PrevFrame) > 0;
            s.PrevFrame = s.Time;
            if (s.History.Count > 1)
            {
                if (remove) s.History.RemoveAt(s.History.Count - 1);
                MakePolygon(s, s.History.Count);
            }
            else _delayed.RemoveAt(i);
        }
    }

    private void MakePolygon(Stripe s, float textureBase)
    {
        int count = s.History.Count;
        if (count <= 2) { s.RenderVertexCount = 0; return; }
        float scaleFade = Emitter.CurrentScaleFadeValue();
        int div = _numDivide;
        int drawCount = count + ((count - 1) * div);
        if (s.Vertices.Length < drawCount * 2) s.Vertices = new PluginVertex[drawCount * 2];
        float texBase = div == 0 ? textureBase - 1 : (textureBase - 1) + ((count - 1) * div);
        float invTex = 1f / Math.Max(texBase, 1e-6f);
        float indexDiv = 1f / (1 + div);

        for (int index = 0; index < drawCount; index++)
        {
            float fIndex = index * indexDiv;
            int hp = Math.Min((int)MathF.Floor(fIndex), count - 1);
            float inner = fIndex - hp;
            History h = s.History[hp];
            History? next = hp + 1 < count ? s.History[hp + 1] : null;
            Vector3 pos = h.Pos, dir = h.Dir, emat = h.Emat;
            if (div > 0 && next is not null && inner > 0f)
            {
                History? prev = hp > 0 ? s.History[hp - 1] : null;
                History? next2 = hp + 2 < count ? s.History[hp + 2] : null;
                Vector3 t1 = (next.Pos - (prev?.Pos ?? h.Pos)) * 0.5f;
                Vector3 t2 = ((next2?.Pos ?? next.Pos) - h.Pos) * 0.5f;
                pos = Hermite(h.Pos, t1, next.Pos, t2, inner);
                dir = Vector3.Lerp(h.Dir, next.Dir, inner);
                emat = Vector3.Lerp(h.Emat, next.Emat, inner);
                if (emat.LengthSquared() > 0) emat = Vector3.Normalize(emat);
            }
            float w = h.Scale * scaleFade;
            float v = index * invTex;
            s.Vertices[index * 2] = new PluginVertex { Pos = new Vector4(pos, w), Dir = new Vector4(dir, v), Emat = new Vector4(emat, index) };
            s.Vertices[(index * 2) + 1] = new PluginVertex { Pos = new Vector4(pos, -w), Dir = new Vector4(dir, v), Emat = new Vector4(emat, index) };
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
        v[4] = new Vector4(s.Time, s.History.Count + (_numDivide * (s.History.Count - 1)), meshType, s.Life);
        v[5] = new Vector4(s.HeadPos, 0);
        return MemoryMarshal.AsBytes(v.AsSpan()).ToArray();
    }

    public override IReadOnlyList<PluginDraw> BuildDraws()
    {
        var draws = new List<PluginDraw>();
        IEnumerable<Stripe> all = _byParticle.Where(s => s is not null).Select(s => s!).Concat(_delayed);
        foreach (Stripe s in all)
        {
            if (s.RenderVertexCount == 0) continue;
            draws.Add(new PluginDraw { Vertices = s.Vertices, Count = s.RenderVertexCount, Block = Block(s, 0) });
            if (_option == 1) draws.Add(new PluginDraw { Vertices = s.Vertices, Count = s.RenderVertexCount, Block = Block(s, 1) });
        }
        return draws;
    }
}
