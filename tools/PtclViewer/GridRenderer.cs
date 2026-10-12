using System.Numerics;
using PtclSharp.Rendering.Gl;
using Silk.NET.OpenGL;

namespace PtclViewer;

/// <summary>A ground grid with coloured axes, drawn into the scene so particles can pass through it.</summary>
public sealed unsafe class GridRenderer : IDisposable
{
    const string VertexShader = """
        #version 450 core
        layout(location = 0) in vec3 position;
        layout(location = 1) in vec3 color;
        uniform mat4 viewProjection;
        out vec3 vertexColor;
        void main() { vertexColor = color; gl_Position = viewProjection * vec4(position, 1.0); }
        """;

    const string FragmentShader = """
        #version 450 core
        in vec3 vertexColor;
        layout(location = 0) out vec4 color;
        void main() { color = vec4(vertexColor, 1.0); }
        """;

    readonly GL _gl;
    readonly GlProgram _program;
    readonly uint _vao, _vbo;
    readonly int _vertexCount;

    public GridRenderer(GL gl)
    {
        _gl = gl;
        _program = new GlProgram(gl, VertexShader, FragmentShader, "grid");

        var vertices = new List<float>();
        void Line(Vector3 a, Vector3 b, Vector3 color) => vertices.AddRange([a.X, a.Y, a.Z, color.X, color.Y, color.Z, b.X, b.Y, b.Z, color.X, color.Y, color.Z]);
        const int extent = 20;
        for (int i = -extent; i <= extent; i++)
        {
            Vector3 color = i == 0 ? new Vector3(0.35f) : new Vector3(0.12f);
            Line(new Vector3(i, 0, -extent), new Vector3(i, 0, extent), color);
            Line(new Vector3(-extent, 0, i), new Vector3(extent, 0, i), color);
        }
        Line(Vector3.Zero, Vector3.UnitX * 1.5f, new Vector3(1, 0.2f, 0.2f));
        Line(Vector3.Zero, Vector3.UnitY * 1.5f, new Vector3(0.2f, 1, 0.2f));
        Line(Vector3.Zero, Vector3.UnitZ * 1.5f, new Vector3(0.3f, 0.4f, 1));
        _vertexCount = vertices.Count / 6;

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        float[] data = vertices.ToArray();
        fixed (float* p = data)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 24, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 24, (void*)12);
    }

    public void Draw(Matrix4x4 viewProjection)
    {
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.CullFace);
        _gl.UseProgram(_program.Handle);
        // System.Numerics is row-vector (v * M), which is what an upload without transposing expresses to GLSL's column-vector convention.
        float* m = stackalloc float[16]
        {
            viewProjection.M11, viewProjection.M12, viewProjection.M13, viewProjection.M14,
            viewProjection.M21, viewProjection.M22, viewProjection.M23, viewProjection.M24,
            viewProjection.M31, viewProjection.M32, viewProjection.M33, viewProjection.M34,
            viewProjection.M41, viewProjection.M42, viewProjection.M43, viewProjection.M44
        };
        _gl.UniformMatrix4(_gl.GetUniformLocation(_program.Handle, "viewProjection"), 1, false, m);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
    }

    public void Dispose()
    {
        _gl.DeleteProgram(_program.Handle);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
    }
}
