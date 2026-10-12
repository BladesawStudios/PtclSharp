namespace PtclSharp.Rendering.Assets;

/// <summary>
/// Debugging switches read once from the environment. <c>PTCL_DUMP_GLSL</c> and <c>PTCL_DUMP_TEX</c> write the decompiled shaders and decoded textures to a
/// folder, <c>PTCL_NODISCARD=1</c> turns <c>discard</c> off, <c>PTCL_DEBUG_EXPR</c> replaces the fragment output with a GLSL expression and
/// <c>PTCL_DEBUG_VTX</c> writes vertex temporaries to the second output.
/// </summary>
internal static class EffectDebug
{
    public static string? DumpGlslDirectory { get; } = Read("PTCL_DUMP_GLSL");

    public static string? DumpTextureDirectory { get; } = Read("PTCL_DUMP_TEX");

    public static bool KeepDiscard { get; } = Read("PTCL_NODISCARD") != "1";

    public static string? FragmentExpression { get; } = Read("PTCL_DEBUG_EXPR");

    public static string? VertexTemporaries { get; } = Read("PTCL_DEBUG_VTX");

    static string? Read(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    /// <summary>Applies <c>PTCL_DEBUG_VTX</c>: <c>"temp_588,temp_575"</c> writes up to four vertex temporaries into <c>out_attr1.xyzw</c> just before the shader ends.</summary>
    public static string DebugVertex(string source)
    {
        if (VertexTemporaries is null)
            return source;
        const string lanes = "xyzw";
        // The strip is forced onto the screen so the temporaries can be read back from its pixels even where the shader culls it.
        const string force = "    gl_Position = vec4((gl_VertexID == 0 || gl_VertexID == 2) ? -0.5 : 0.5, gl_VertexID < 2 ? -0.5 : 0.5, 0.0, 1.0);\n";
        string[] names = VertexTemporaries.Split(',');
        var writes = new System.Text.StringBuilder();
        for (int i = 0; i < Math.Min(4, names.Length); i++)
            writes.Append($"    out_attr1.{lanes[i]} = float({names[i]});\n");
        writes.Append(force);
        string withReturns = source.Replace("return;", writes + "    return;");
        return withReturns[..withReturns.LastIndexOf('}')] + writes + "}\n";
    }

    /// <summary>Applies <c>PTCL_DEBUG_EXPR</c> and <c>PTCL_NODISCARD</c> to a fragment shader.</summary>
    public static string DebugFragment(string source)
    {
        if (!KeepDiscard)
            source = source.Replace("discard;", ";");
        if (FragmentExpression is null)
            return source;
        source = source.Replace("return;", ";");
        return source[..source.LastIndexOf('}')] + "out_color0 = " + FragmentExpression + ";\n}\n";
    }
}
