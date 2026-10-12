using System.Text.RegularExpressions;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Gl;

/// <summary>A linked vertex+fragment program plus what the decompiled source says about its bindings.</summary>
public sealed partial class GlProgram
{
    public uint Handle { get; }
    public string Label { get; }

    /// <summary>Uniform block name (without the leading underscore) to binding point.</summary>
    public Dictionary<string, uint> UniformBlocks { get; } = [];

    /// <summary>Sampler uniform name to texture unit.</summary>
    public Dictionary<string, int> Samplers { get; } = [];

    /// <summary>Sampler uniform name to its GLSL type (sampler2D, samplerCube, sampler2DArray, ...).</summary>
    public Dictionary<string, string> SamplerTypes { get; } = [];

    /// <summary>Vertex input name to attribute location.</summary>
    public Dictionary<string, uint> Inputs { get; } = [];

    [GeneratedRegex(@"binding\s*=\s*(\d+),\s*std140\)\s*uniform\s+_?(\w+)")]
    private static partial Regex BlockRx();

    [GeneratedRegex(@"binding\s*=\s*(\d+)\)\s*uniform\s+(sampler\w*)\s+(\w+)\s*;")]
    private static partial Regex SamplerRx();

    [GeneratedRegex(@"location\s*=\s*(\d+)\)\s*in\s+\w+\s+(\w+)\s*;")]
    private static partial Regex InputRx();

    public GlProgram(GL gl, string vertexSource, string fragmentSource, string label)
    {
        Label = label;
        uint vs = Compile(gl, ShaderType.VertexShader, vertexSource, label + ".vert");
        uint fs = Compile(gl, ShaderType.FragmentShader, fragmentSource, label + ".frag");
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vs);
        gl.AttachShader(Handle, fs);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        if (linked == 0)
            throw new InvalidOperationException($"link failed for {label}:\n{gl.GetProgramInfoLog(Handle)}");

        foreach (Match m in BlockRx().Matches(vertexSource + "\n" + fragmentSource))
            UniformBlocks[m.Groups[2].Value] = uint.Parse(m.Groups[1].Value);
        foreach (Match m in SamplerRx().Matches(fragmentSource + "\n" + vertexSource))
        {
            Samplers[m.Groups[3].Value] = int.Parse(m.Groups[1].Value);
            SamplerTypes[m.Groups[3].Value] = m.Groups[2].Value;
        }
        foreach (Match m in InputRx().Matches(vertexSource))
            Inputs[m.Groups[2].Value] = uint.Parse(m.Groups[1].Value);
    }

    private static uint Compile(GL gl, ShaderType type, string source, string label)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, source);
        gl.CompileShader(s);
        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = gl.GetShaderInfoLog(s);
            gl.DeleteShader(s);
            throw new InvalidOperationException($"compile failed for {label}:\n{log}");
        }
        return s;
    }
}
