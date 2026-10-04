// Decompiles every BNSH produced by `PtclCorpus bnsh` and reports how the shaders use the emitter static uniform block
// (sysEmitterStaticUniformBlock, binding 6 = a verbatim copy of EMTR data bytes [0, 0xCA0)).
//   use      <bnshDir> <out.json>   union of block byte offsets read, per stage: { "<block>:<HEXOFFSET>": "VF..." } for every uniform block
//   patterns <bnshDir> <out.json>   for each static-block byte, the 12 most common normalized GLSL lines that read it
//   show     <bnshDir> <outDir> <hexOffset[,hexOffset]> [maxFilesPerOffset]   write the first GLSL files that read those bytes
using System.Text.Json;
using System.Text.RegularExpressions;
using EffectLibraryTest;
using ShaderLibrary;
using ShaderLibrary.CompileTool;

string cmd = args[0], dir = args[1], outArg = args[2];
var blockRe = new Regex(@"binding = (\d+), std140\) uniform _(\w+)\s*\{[^}]*\}\s*(\w+);");

IEnumerable<(string file, int variant, string stage, string glsl)> Programs()
{
    foreach (var f in Directory.GetFiles(dir, "*.bnsh"))
    {
        BnshFile bnsh;
        try { bnsh = new BnshFile(f); } catch { continue; }
        int vi = 0;
        foreach (var v in bnsh.Variations)
        {
            vi++;
            var p = v.BinaryProgram;
            foreach (var (nm, c, r) in new[] { ("V", p.VertexShader, p.VertexShaderReflection), ("F", p.FragmentShader, p.FragmentShaderReflection), ("C", p.ComputeShader, p.ComputeShaderReflection) })
            {
                if (c?.ByteCode == null) continue;
                string t;
                try { t = ShaderExtract.GetCode(c, r); } catch { continue; }
                yield return (f, vi, nm, t);
            }
        }
    }
}

if (cmd == "use")
{
    var used = new Dictionary<string, HashSet<string>>();
    foreach (var (_, _, stage, t) in Programs())
        foreach (Match m in blockRe.Matches(t))
        {
            string blk = m.Groups[2].Value, name = m.Groups[3].Value;
            foreach (Match u in Regex.Matches(t, Regex.Escape(name) + @"\.data\[(\d+)\](?:\.([xyzw]+))?"))
            {
                int i = int.Parse(u.Groups[1].Value);
                foreach (char ch in u.Groups[2].Success ? u.Groups[2].Value : "xyzw")
                {
                    string k = $"{blk}:{i * 16 + "xyzw".IndexOf(ch) * 4:X}";
                    if (!used.TryGetValue(k, out var s)) used[k] = s = new();
                    s.Add(stage);
                }
            }
        }
    File.WriteAllText(outArg, JsonSerializer.Serialize(new { used = used.ToDictionary(k => k.Key, k => string.Concat(k.Value.OrderBy(x => x))) }, new JsonSerializerOptions { WriteIndented = true }));
}
else if (cmd == "patterns")
{
    var pat = new Dictionary<int, Dictionary<string, int>>();
    var tempRe = new Regex(@"temp_\d+");
    var otherU = new Regex(@"\b(?:fp|vp)_c(?:\d+)\.data\[\w+\](?:\.[xyzw]+)?");
    foreach (var (_, _, stage, t) in Programs())
    {
        var m = Regex.Match(t, @"binding = 6, std140\) uniform _sysEmitterStaticUniformBlock\s*\{[^}]*\}\s*(\w+);");
        if (!m.Success) continue;
        string name = m.Groups[1].Value;
        var own = new Regex(Regex.Escape(name) + @"\.data\[(\d+)\]\.([xyzw])");
        foreach (var line0 in t.Split('\n'))
        {
            var line = line0.Trim();
            if (line.StartsWith("//") || !line.Contains(name + ".data[")) continue;
            foreach (Match mm in own.Matches(line))
            {
                int off = int.Parse(mm.Groups[1].Value) * 16 + "xyzw".IndexOf(mm.Groups[2].Value[0]) * 4;
                string norm = stage + ": " + tempRe.Replace(otherU.Replace(own.Replace(line.Replace(mm.Value, "F"), "S"), "U"), "t");
                if (!pat.TryGetValue(off, out var d)) pat[off] = d = new();
                d[norm] = d.GetValueOrDefault(norm) + 1;
            }
        }
    }
    var res = pat.ToDictionary(k => k.Key.ToString("X"), k => k.Value.OrderByDescending(x => x.Value).Take(12).ToDictionary(x => x.Key, x => x.Value));
    File.WriteAllText(outArg, JsonSerializer.Serialize(res, new JsonSerializerOptions { WriteIndented = true }));
}
else if (cmd == "show")
{
    Directory.CreateDirectory(outArg);
    var want = args[3].Split(',').Select(x => Convert.ToInt32(x, 16)).ToArray();
    int maxPer = args.Length > 4 ? int.Parse(args[4]) : 2;
    var got = want.ToDictionary(x => x, x => 0);
    foreach (var (file, vi, stage, t) in Programs())
    {
        if (want.All(w => got[w] >= maxPer)) break;
        var m = Regex.Match(t, @"binding = 6, std140\) uniform _sysEmitterStaticUniformBlock\s*\{[^}]*\}\s*(\w+);");
        if (!m.Success) continue;
        var hit = new List<int>();
        foreach (Match u in Regex.Matches(t, Regex.Escape(m.Groups[1].Value) + @"\.data\[(\d+)\](?:\.([xyzw]+))?"))
        {
            int i = int.Parse(u.Groups[1].Value);
            foreach (char ch in u.Groups[2].Success ? u.Groups[2].Value : "xyzw")
            {
                int o = i * 16 + "xyzw".IndexOf(ch) * 4;
                if (want.Contains(o) && got[o] < maxPer) hit.Add(o);
            }
        }
        if (hit.Count == 0) continue;
        File.WriteAllText(Path.Combine(outArg, $"{Path.GetFileNameWithoutExtension(file)[..8]}_v{vi}.{stage}"), t);
        foreach (var o in hit.Distinct()) got[o]++;
    }
}
