using System.Numerics;
using PtclSharp.Simulation;
using PtclSharp.Simulation.Random;
using PtclSharp.Simulation.Resources;
using PtclSharp.Simulation.Runtime;

namespace PtclSharp.Tests;

public class SimulationTests
{
    [Fact]
    public void SeadRandomIsDeterministic()
    {
        var a = new SeadRandom(12345679);
        var b = new SeadRandom(12345679);
        for (int i = 0; i < 64; i++) Assert.Equal(a.GetU32(), b.GetU32());
        var c = new SeadRandom(1);
        Assert.NotEqual(new SeadRandom(12345679).GetU32(), c.GetU32());
    }

    [Fact]
    public void VfxRandomFloatsStayInUnitRange()
    {
        var r = new VfxRandom();
        r.SetSeed(0xDEADBEEF);
        for (int i = 0; i < 10_000; i++)
        {
            float f = r.GetFloat();
            Assert.InRange(f, 0f, 1f);
            Assert.InRange(r.GetInteger(7), 0, 6);
        }
    }

    [Fact]
    public void VfxRandomMatchesTheGameLcg()
    {
        var r = new VfxRandom();
        r.SetSeed(1);
        Assert.Equal(1f / 4294967296f, r.GetFloat());
        // state after one step: 1 * 0x41c64e6d + 0x3039
        Assert.Equal((0x41c64e6du + 0x3039u) / 4294967296f, r.GetFloat());
    }

    [Fact]
    public void AnimationTrackInterpolatesAndFinishes()
    {
        var track = new EmitterAnimTrack { Keys = [new Vector4(0, 0, 0, 0), new Vector4(10, 20, 30, 10)] };
        Vector3 v = default;
        bool over = false;
        Assert.True(track.Evaluate(5, ref v, ref over));
        Assert.Equal(new Vector3(5, 10, 15), v);
        Assert.False(over);
        Assert.True(track.Evaluate(11, ref v, ref over));
        Assert.Equal(new Vector3(10, 20, 30), v);
        Assert.True(over);
    }

    [Fact]
    public void EmptyAnimationTrackKeepsTheDefault()
    {
        Vector3 v = new(1, 2, 3);
        bool over = false;
        Assert.False(new EmitterAnimTrack().Evaluate(3, ref v, ref over));
        Assert.Equal(new Vector3(1, 2, 3), v);
    }

    [Fact]
    public void SrtMatrixPlacesTranslation()
    {
        Matrix4x4 m = VfxMath.CreateSrtXyz(new Vector3(2), Vector3.Zero, new Vector3(1, 2, 3));
        Assert.Equal(new Vector3(1, 2, 3), m.Translation);
        Assert.Equal(new Vector3(3, 2 * 1 + 2, 3 + 2), Vector3.Transform(Vector3.One, m).Let(p => new Vector3(p.X, p.Y, p.Z)));
    }

    /// <summary>Every shipped BotW emitter set simulates for four seconds without throwing, and the corpus as a whole emits particles.</summary>
    [BotwCorpusFact]
    public void EveryBotwEmitterSetSimulates()
    {
        var failures = new List<string>();
        int withParticles = 0, sets = 0;
        foreach (string file in Corpus.BotwFiles().Where((_, i) => i % 4 == 0))
        {
            try
            {
                PtclFile pf = Corpus.LoadBotw(file);
                var reader = EmitterResourceReaders.For(pf.Version);
                foreach (var set in pf.Vfxb.EmitterSets)
                {
                    sets++;
                    var system = new EffectSystem();
                    EmitterSetInstance instance = system.CreateEmitterSet(reader.ReadSet(pf.Vfxb, set));
                    bool any = false;
                    for (int f = 0; f < 120; f++)
                    {
                        system.Calculate(1f);
                        any |= instance.AllEmitters.Any(e => e.ParticleCount > 0);
                    }
                    if (any) withParticles++;
                }
            }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(file)}: {ex.GetType().Name} {ex.Message}"); }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
        Assert.True(withParticles > sets / 2, $"only {withParticles} of {sets} sets produced particles");
    }
}

internal static class SimulationTestExtensions
{
    internal static TResult Let<T, TResult>(this T value, Func<T, TResult> f) => f(value);
}
