using System.Text.RegularExpressions;

namespace PtclSharp.Rendering.Gl;

/// <summary>Makes the Tegra decompiler's GLSL compile on a desktop driver (the same fixes WildRenderingSharp applies).</summary>
public static partial class GlslClean
{
    private static readonly string[] DroppedPrefixes = ["#extension", "#pragma optionNV"];

    private static readonly string[] SafeMath =
    [
        "#define safe_log2(x) log2(max(float(x), 1e-7))",
        "#define log2(x) safe_log2(x)",
        "#define safe_inversesqrt(x) inversesqrt(max(float(x), 1e-12))",
        "#define inversesqrt(x) safe_inversesqrt(x)",
    ];

    [GeneratedRegex(@"binding\s*=\s*-\d+")]
    private static partial Regex NegativeBinding();

    [GeneratedRegex(@"^[ \t]*fp_s\d+\.data\[[^\]\n]*\][ \t]*=(?!=)[^;\n]*;", RegexOptions.Multiline)]
    private static partial Regex FragmentStorageWrite();

    [GeneratedRegex(@"out_attr1\.([xyz]) = (\w+);")]
    private static partial Regex GBufferAlbedo();

    [GeneratedRegex(@"out_attr3\.([xyz]) = (\w+);")]
    private static partial Regex GBufferNormal();

    [GeneratedRegex(@"^[ \t]*out_color0\.[xyz] = [^;]*;[ \t]*\r?\n", RegexOptions.Multiline)]
    private static partial Regex ColorXyzWrite();

    [GeneratedRegex(@"uniform _sysEmitterStaticUniformBlock\s*\{[^}]*\}\s*(\w+);")]
    private static partial Regex StaticBlockVar();

    private const string NL = "\n";

    /// <summary>GLSL for the sun's direction in view space, from the view block's rotation rows (<c>fp_c8</c> is the view uniform block in the decompiled names).</summary>
    public static string SunInViewSpace(System.Numerics.Vector3 sun)
    {
        string v = $"vec3({sun.X:0.####}, {sun.Y:0.####}, {sun.Z:0.####})";
        return $"normalize(vec3(dot(fp_c8.data[0].xyz, {v}), dot(fp_c8.data[1].xyz, {v}), dot(fp_c8.data[2].xyz, {v})))";
    }

    /// <summary>
    /// Deferred particle shaders write a G-buffer (albedo in attribute 1, view-space normal packed to 0..1 in attribute 3) and a constant in the
    /// colour output. The viewer has no lighting pass, so shade the albedo with the sun here and write that as the colour.
    /// </summary>
    public static string ForwardizeGBuffer(string fragment, string sunView)
    {
        if (!fragment.Contains("out vec4 out_attr1;", StringComparison.Ordinal) || !fragment.Contains("out_attr3.", StringComparison.Ordinal)) return fragment;
        var albedo = new Dictionary<string, string>();
        var normal = new Dictionary<string, string>();
        foreach (Match m in GBufferAlbedo().Matches(fragment)) albedo[m.Groups[1].Value] = m.Groups[2].Value;
        foreach (Match m in GBufferNormal().Matches(fragment)) normal[m.Groups[1].Value] = m.Groups[2].Value;
        if (albedo.Count < 3 || normal.Count < 3) return fragment;
        Match first = ColorXyzWrite().Match(fragment);
        if (!first.Success) return fragment;
        string shade =
            $"    vec3 gb_n = normalize(vec3({normal["x"]}, {normal["y"]}, {normal["z"]}) * 2.0 - 1.0);" + NL +
            $"    float gb_l = 0.45 + 0.65 * max(dot(gb_n, {sunView}), 0.0);" + NL +
            $"    out_color0.x = {albedo["x"]} * gb_l;" + NL + $"    out_color0.y = {albedo["y"]} * gb_l;" + NL + $"    out_color0.z = {albedo["z"]} * gb_l;" + NL;
        string text = ColorXyzWrite().Replace(fragment, "");
        int at = text.IndexOf("    out_color0.w", StringComparison.Ordinal);
        if (at < 0) return fragment;
        text = text.Insert(at, shade);
        // opaque (blend-off) draws are alpha tested: shaders that do not discard themselves rely on the fixed-function test, whose reference
        // is the emitter's fragment discard threshold (EMTR 0x5E8, static block row 94 z)
        Match block = StaticBlockVar().Match(text);
        if (block.Success && !text.Contains("discard;", StringComparison.Ordinal))
        {
            int ret = text.LastIndexOf("    return;", StringComparison.Ordinal);
            if (ret > 0) text = text.Insert(ret, $"    if (out_color0.w <= {block.Groups[1].Value}.data[94].z) {{ discard; }}{NL}");
        }
        return text;
    }

    public static string Apply(string source)
    {
        var output = new List<string>();
        bool versionSeen = false;
        foreach (string raw in source.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string t = line.TrimStart();
            if (DroppedPrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) continue;
            line = line.Replace("gl_FragData[0]", "out_color0");
            line = line.Replace("output_color[0]", "out_color0");
            output.Add(line);
            if (!versionSeen && t.StartsWith("#version", StringComparison.Ordinal))
            {
                versionSeen = true;
                output.AddRange(SafeMath);
            }
        }
        string text = string.Join('\n', output);
        text = NegativeBinding().Replace(text, "binding = 15");
        text = FragmentStorageWrite().Replace(text, "");
        return text;
    }
}
