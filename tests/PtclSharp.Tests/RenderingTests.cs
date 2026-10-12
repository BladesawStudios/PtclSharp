using System.Numerics;
using PtclSharp.Rendering.Drawing;
using PtclSharp.Rendering.Lighting;

namespace PtclSharp.Tests;

public class RenderingTests
{
    static ParticleView ViewAt(float near, float far, uint sceneDepth = 0) => new()
    {
        View = Matrix4x4.CreateLookAt(new Vector3(3, 2, 8), new Vector3(0, 1, 0), Vector3.UnitY),
        Projection = ParticleView.Perspective(float.DegreesToRadians(45f), 16f / 9f, near, far),
        Near = near, Far = far, Width = 1280, Height = 720, SceneLinearDepth = sceneDepth
    };

    // What the shaders do with the view block: depth01 = (clip z / clip w) * 0.5 + 0.5 from the view-projection rows, then 1 / (depth01 * w - y) is the distance.
    static float ShaderDistance(Vector4[] block, Vector3 world)
    {
        Vector4 p = new(world, 1f);
        float clipZ = Vector4.Dot(block[10], p), clipW = Vector4.Dot(block[11], p);
        float depth01 = (clipZ / clipW * 0.5f) + 0.5f;
        return 1f / ((depth01 * block[18].W) - block[18].Y);
    }

    [Theory]
    [InlineData(0.1f, 2000f)]
    [InlineData(0.5f, 500f)]
    public void TheViewBlockDepthConstantsRecoverTheViewDistance(float near, float far)
    {
        ParticleView view = ViewAt(near, far);
        Vector4[] block = ViewBlock.Build(view, out Vector3 eye, out _);
        foreach (var point in new[] { new Vector3(0, 1, 0), new Vector3(-4, 0.5f, -20), new Vector3(10, 3, 2) })
        {
            float expected = -Vector3.Transform(point, view.View).Z;
            Assert.Equal(expected, ShaderDistance(block, point), expected * 1e-3f);
        }
        Assert.True(Vector3.Distance(new Vector3(3, 2, 8), eye) < 1e-3f);
    }

    [Fact]
    public void TheViewBlockOffsetsTheSceneDistanceOnlyWithoutASceneDepth()
    {
        Assert.Equal(1000f, ViewBlock.Build(ViewAt(0.1f, 100f), out _, out _)[18].X);
        Assert.Equal(0f, ViewBlock.Build(ViewAt(0.1f, 100f, sceneDepth: 7), out _, out _)[18].X);
    }

    [Fact]
    public void ThePerspectiveMapsTheNearAndFarPlanesToTheGlClipRange()
    {
        Matrix4x4 projection = ParticleView.Perspective(float.DegreesToRadians(60f), 1f, 0.5f, 50f);
        Vector4 near = Vector4.Transform(new Vector4(0, 0, -0.5f, 1), projection), far = Vector4.Transform(new Vector4(0, 0, -50f, 1), projection);
        Assert.Equal(-1f, near.Z / near.W, 1e-4f);
        Assert.Equal(1f, far.Z / far.W, 1e-4f);
    }

    [Fact]
    public void TheEnvironmentBlocksHaveTheGamesSizes()
    {
        var environment = new GameEnvironment { FogAmount = 0.25f };
        foreach (int index in new[] { 0, 1, 2, 3 })
            Assert.Equal(4 * 64 * 4, environment.BuildBlock(index).Length);
        // fog strength lands in the rows the fog lerps read (25 and 26, w)
        float[] rows = new float[256];
        Buffer.BlockCopy(environment.BuildBlock(1), 0, rows, 0, 1024);
        Assert.Equal(0.25f, rows[(25 * 4) + 3]);
        Assert.Equal(0.25f, rows[(26 * 4) + 3]);
    }
}
