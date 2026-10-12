using PtclSharp.Simulation.Resources;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>Maps an emitter's render state (blend mode, depth, culling) to GL state.</summary>
internal static class RenderStates
{
    public static void Apply(GL gl, RenderState state)
    {
        if (state.BlendEnable)
        {
            gl.Enable(EnableCap.Blend);
            (BlendingFactor source, BlendingFactor destination, BlendEquationModeEXT equation) = state.BlendMode switch
            {
                0 => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendEquationModeEXT.FuncAdd),
                1 => (BlendingFactor.SrcAlpha, BlendingFactor.One, BlendEquationModeEXT.FuncAdd),
                2 => (BlendingFactor.SrcAlpha, BlendingFactor.One, BlendEquationModeEXT.FuncReverseSubtract),
                3 => (BlendingFactor.Zero, BlendingFactor.SrcColor, BlendEquationModeEXT.FuncAdd),
                4 => (BlendingFactor.OneMinusDstColor, BlendingFactor.One, BlendEquationModeEXT.FuncAdd),
                _ => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendEquationModeEXT.FuncAdd)
            };
            gl.BlendFuncSeparate(source, destination, BlendingFactor.Zero, BlendingFactor.One);
            gl.BlendEquationSeparate(equation, BlendEquationModeEXT.FuncAdd);
        }
        else
            gl.Disable(EnableCap.Blend);

        if (state.DepthTest)
            gl.Enable(EnableCap.DepthTest);
        else
            gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(state.DepthWrite);
        gl.DepthFunc(state.DepthFunc switch
        {
            0 => DepthFunction.Never, 1 => DepthFunction.Less, 2 => DepthFunction.Equal, 3 => DepthFunction.Lequal,
            4 => DepthFunction.Greater, 5 => DepthFunction.Notequal, 6 => DepthFunction.Gequal, _ => DepthFunction.Always
        });

        if (state.CullMode == 0)
            gl.Disable(EnableCap.CullFace);
        else
        {
            gl.Enable(EnableCap.CullFace);
            // The window origin is at the top left, which reverses the winding the rasterizer sees: the game's back is GL's front.
            gl.CullFace(state.CullMode == 1 ? TriangleFace.Front : TriangleFace.Back);
        }
    }
}
