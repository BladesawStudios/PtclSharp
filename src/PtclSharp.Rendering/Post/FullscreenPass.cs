using Silk.NET.OpenGL;
using PtclSharp.Rendering.Gl;

namespace PtclSharp.Rendering.Post;

/// <summary>A fragment program drawn over the whole viewport as one triangle, for the post-processing passes.</summary>
internal sealed class FullscreenPass : IDisposable
{
    readonly GL _gl;
    readonly GlProgram _program;
    readonly uint _vao;

    public FullscreenPass(GL gl, string fragmentName)
    {
        _gl = gl;
        _program = new GlProgram(gl, GlslFiles.Load("Fullscreen.vert"), GlslFiles.Load(fragmentName), fragmentName);
        _vao = gl.GenVertexArray();
    }

    public void Use() => _gl.UseProgram(_program.Handle);

    public void SetFloat(string name, float value) => _gl.Uniform1(_gl.GetUniformLocation(_program.Handle, name), value);

    public void SetVector2(string name, float x, float y) => _gl.Uniform2(_gl.GetUniformLocation(_program.Handle, name), x, y);

    /// <summary>Binds <paramref name="texture"/> to unit <paramref name="unit"/> for the sampler uniform <paramref name="name"/>.</summary>
    public void SetTexture(string name, int unit, uint texture)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.BindSampler((uint)unit, 0);
        _gl.Uniform1(_gl.GetUniformLocation(_program.Handle, name), unit);
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    public void Dispose()
    {
        _gl.DeleteProgram(_program.Handle);
        _gl.DeleteVertexArray(_vao);
    }
}
