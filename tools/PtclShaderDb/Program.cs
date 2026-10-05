// Builds the shader database that PtclSharp.Shaders searches for donor shaders.
//
//   PtclShaderDb build --botw-rom <ROM root> --totk-romfs <romfs root> --out <shaderdb.json.gz> [--game botw|totk] [--limit N]
//   PtclShaderDb stats <shaderdb.json.gz>
//   PtclShaderDb validate <shaderdb.json.gz> --botw-rom <ROM root> [--max N]   effects that exist in both games: does the search find TotK's own version?
//   PtclShaderDb match <shaderdb.json.gz> <botw.sesetlist> [--emitters]   which TotK files could host the effect, and how well
//
// For every effect file it opens the shader archive (TotK: GRSN > GRSR and GRSC; BotW: the GRSN node's own data and its GRSC child),
// identifies each program by the hash of its byte code, and records what the program looks like from outside (samplers, vertex
// attributes, uniform blocks, and the bytes of sysEmitterStaticUniformBlock its stages read, found by decompiling the byte code
// to GLSL with Marrow's ShaderLibrary). Every emitter's shader selection is recorded next to it.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using EffectLibraryTest;
using PtclSharp;
using PtclSharp.Layout;
using PtclSharp.Shaders;
using ShaderLibrary;

if (args.Length < 2) return Usage();

if (args[0] == "stats")
{
    ShaderDatabase loaded = ShaderDatabase.Load(args[1]);
    Console.WriteLine($"{loaded.Programs.Count} programs, {loaded.Files.Count} files ({loaded.FilesOf(ShaderGame.BotW).Count()} BotW, {loaded.FilesOf(ShaderGame.TotK).Count()} TotK)");
    foreach (ShaderGame g in Enum.GetValues<ShaderGame>())
    {
        var ids = loaded.FilesOf(g).SelectMany(f => f.Programs.Concat(f.ComputePrograms)).Distinct().ToList();
        Console.WriteLine($"  {g}: {ids.Count} distinct programs, {loaded.FilesOf(g).Sum(f => f.Emitters.Length)} emitters");
    }
    return 0;
}
if (args[0] == "match") return Match();
if (args[0] == "validate") return Validate();
if (args[0] != "build") return Usage();

string? botwRom = Option("--botw-rom"), totkRomfs = Option("--totk-romfs"), outPath = Option("--out"), only = Option("--game");
int limit = int.TryParse(Option("--limit"), out int l) ? l : int.MaxValue;
if (outPath is null || (botwRom is null && totkRomfs is null)) return Usage();

var db = new ShaderDatabase();
var sw = Stopwatch.StartNew();
if (botwRom is not null && only is null or "botw") Scan(ShaderGame.BotW, Path.Combine(botwRom, "Effect"), "*.sesetlist", null);
if (totkRomfs is not null && only is null or "totk")
{
    byte[] dictionary = PtclDictionary.FromZsDicPackFile(Path.Combine(totkRomfs, "Pack", "ZsDic.pack.zs"));
    Scan(ShaderGame.TotK, Path.Combine(totkRomfs, "Effect"), "*.esetb.byml.zs", dictionary);
}
db.Save(outPath);
Console.WriteLine($"{db.Programs.Count} programs from {db.Files.Count} files in {sw.Elapsed.TotalSeconds:F0}s -> {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
return 0;

string? Option(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static int Usage()
{
    Console.Error.WriteLine("usage: PtclShaderDb build --botw-rom <dir> --totk-romfs <dir> --out <file> [--game botw|totk] [--limit N]\n       PtclShaderDb stats <file>");
    return 1;
}

void Scan(ShaderGame game, string effectDir, string pattern, byte[]? dictionary)
{
    var archives = new Dictionary<string, string[]>(); // BNSH hash -> program ids, so files sharing an archive are decompiled once
    int done = 0;
    foreach (string path in Directory.EnumerateFiles(effectDir, pattern).Order(StringComparer.Ordinal).Take(limit))
    {
        // "Foo.Product.110" and "Foo" are different effects; only the platform and container suffixes go.
        string name = Path.GetFileName(path);
        foreach (string suffix in new[] { ".Nin_NX_NVN.esetb.byml.zs", ".sesetlist" })
            if (name.EndsWith(suffix, StringComparison.Ordinal)) name = name[..^suffix.Length];
        PtclFile file;
        try { file = game == ShaderGame.BotW ? PtclFile.ReadSesetlist(File.ReadAllBytes(path)) : PtclFile.ReadEsetb(File.ReadAllBytes(path), dictionary); }
        catch (Exception e) { Console.Error.WriteLine($"{name}: {e.Message}"); continue; }

        VfxbFile v = file.Vfxb;
        VfxbNode? grsn = v.Roots.FirstOrDefault(r => r.Kind == "GRSN");
        string[] graphics = [], compute = [];
        if (grsn is not null)
        {
            int? graphicsAt = game == ShaderGame.BotW ? grsn.DataOffset : grsn.Children.FirstOrDefault(c => c.Kind == "GRSR")?.DataOffset;
            int? computeAt = grsn.Children.FirstOrDefault(c => c.Kind == "GRSC")?.DataOffset;
            graphics = Archive(v.Data, graphicsAt);
            compute = Archive(v.Data, computeAt);
        }

        var emitters = new List<ShaderEmitterUse>();
        foreach (VfxbEmitter e in v.EmitterSets.SelectMany(s => s.Emitters))
        {
            StructView view = v.EmitterView(e);
            string slots = string.Concat(e.TextureSamplerGuids.Select(g => g is ulong u && u != ulong.MaxValue ? '1' : '0'));
            emitters.Add(new ShaderEmitterUse(e.Name, Int(view, "shader_idx_normal"), Int(view, "shader_idx_pass1"), Int(view, "shader_idx_pass2"),
                Int(view, "compute_shader0"), Int(view, "compute_shader1"), Int(view, "compute_shader2"), Int(view, "custom_shader_index"), slots));
        }
        db.Files.Add(new ShaderFileEntry(game, name, graphics, compute, emitters.ToArray()));
        if (++done % 100 == 0) Console.WriteLine($"{game}: {done} files, {db.Programs.Count} programs, {sw.Elapsed.TotalSeconds:F0}s");
    }

    string[] Archive(byte[] data, int? at)
    {
        if (at is not int start || start + 4 > data.Length || !data.AsSpan(start, 4).SequenceEqual("BNSH"u8)) return [];
        string archiveHash = Convert.ToHexString(SHA1.HashData(data.AsSpan(start)));
        if (archives.TryGetValue(archiveHash, out string[]? known)) return known;

        BnshFile bnsh;
        try { bnsh = new BnshFile(new MemoryStream(data, start, data.Length - start)); }
        catch (Exception e) { Console.Error.WriteLine($"BNSH at 0x{start:X}: {e.Message}"); return archives[archiveHash] = []; }

        var ids = new List<string>();
        foreach (BnshFile.ShaderVariation variation in bnsh.Variations)
        {
            ProgramSignature? signature = null;
            try { signature = Describe(variation.BinaryProgram); }
            catch (Exception e) { Console.Error.WriteLine($"variation: {e.Message}"); }
            ids.Add(signature?.Id ?? "");
        }
        return archives[archiveHash] = ids.ToArray();
    }
}

static int Int(in StructView view, string field) =>
    view.Layout.TryGetField(field, out FieldDef? def)
        ? view.Read(def) switch { int i => i, uint u => unchecked((int)u), byte b => b, sbyte sb => sb, short sh => sh, ushort us => us, long lg => unchecked((int)lg), _ => -1 }
        : -1;

ProgramSignature Describe(BnshFile.BnshShaderProgram p)
{
    var hash = SHA1.Create();
    foreach (BnshFile.ShaderCode? code in new[] { p.VertexShader, p.FragmentShader, p.ComputeShader })
    {
        hash.TransformBlock([(byte)(code?.ByteCode is null ? 0 : 1)], 0, 1, null, 0);
        if (code?.ByteCode is null) continue;
        hash.TransformBlock(code.ByteCode, 0, code.ByteCode.Length, null, 0);
        if (code.ControlCode is not null) hash.TransformBlock(code.ControlCode, 0, code.ControlCode.Length, null, 0);
    }
    hash.TransformFinalBlock([], 0, 0);
    string id = Convert.ToHexString(hash.Hash!);
    if (db.Programs.TryGetValue(id, out ProgramSignature? existing)) return existing;

    static string[] Names<T>(ResDict<T>? dict) where T : IResData, new() => dict is null ? [] : dict.Keys.Order(StringComparer.Ordinal).ToArray();
    int[] Reads(BnshFile.ShaderCode? code, BnshFile.ShaderReflectionData? reflection)
    {
        if (code?.ByteCode is null) return [];
        string glsl;
        try { glsl = ShaderExtract.GetCode(code, reflection); }
        catch { return []; }
        return StaticBlockReads(glsl);
    }

    string stages = (p.VertexShader?.ByteCode is null ? "" : "V") + (p.FragmentShader?.ByteCode is null ? "" : "F") + (p.ComputeShader?.ByteCode is null ? "" : "C");
    var signature = new ProgramSignature(id, stages,
        Names(p.VertexShaderReflection?.Inputs),
        Names(p.FragmentShaderReflection?.Samplers),
        Names(p.VertexShaderReflection?.Samplers),
        Names(p.FragmentShaderReflection?.UniformBuffers),
        Names(p.VertexShaderReflection?.UniformBuffers),
        Reads(p.VertexShader, p.VertexShaderReflection),
        Reads(p.FragmentShader, p.FragmentShaderReflection),
        Reads(p.ComputeShader, p.ComputeShaderReflection));
    db.Programs[id] = signature;
    return signature;
}

// Byte offsets of sysEmitterStaticUniformBlock that the GLSL reads: the block is declared as an array of vec4 called data, so
// data[i].y is byte i * 16 + 4. A use without a swizzle reads all four components.
static int[] StaticBlockReads(string glsl)
{
    Match decl = Regex.Match(glsl, @"binding = \d+, std140\) uniform _sysEmitterStaticUniformBlock\s*\{[^}]*\}\s*(\w+);");
    if (!decl.Success) return [];
    var offsets = new SortedSet<int>();
    foreach (Match use in Regex.Matches(glsl, Regex.Escape(decl.Groups[1].Value) + @"\.data\[(\d+)\](?:\.([xyzw]+))?"))
    {
        int index = int.Parse(use.Groups[1].Value);
        foreach (char c in use.Groups[2].Success ? use.Groups[2].Value : "xyzw")
            offsets.Add((index * 16) + ("xyzw".IndexOf(c) * 4));
    }
    return offsets.ToArray();
}

int Match()
{
    if (args.Length < 3) return Usage();
    var finder = new DonorFinder(ShaderDatabase.Load(args[1]));
    string path = args[2];
    PtclFile botw = PtclFile.ReadSesetlist(File.ReadAllBytes(path));
    IReadOnlyList<BotwEmitterShaders>? sources = finder.SourceEmitters(Path.GetFileNameWithoutExtension(path), botw);
    if (sources is null) { Console.Error.WriteLine("This BotW file is not in the database."); return 1; }

    var sw = Stopwatch.StartNew();
    IReadOnlyList<DonorPlan> plans = finder.Plan(sources, top: 5);
    Console.WriteLine($"{sources.Count} emitters, searched in {sw.ElapsedMilliseconds} ms");
    foreach (DonorPlan plan in plans)
    {
        Console.WriteLine($"{plan.File.Name,-40} score {plan.Score:F3} (ceiling {plan.Ceiling:F3}), {plan.Emitters.Count(e => e.Donor is null)} emitters without a donor");
        if (!args.Contains("--emitters") || plan != plans[0]) continue;
        foreach (EmitterDonor e in plan.Emitters)
            Console.WriteLine($"    {e.Source.Name,-32} -> {(e.Donor is null ? "(none)" : $"{e.Donor.Emitter.Name} [{e.Donor.Score:F3}] {string.Join("; ", e.Donor.Notes.Take(2))}")}");
    }
    return 0;
}

// Effects that BotW and TotK both ship (same file name, same emitter name) give a ground truth: TotK's emitter of that name is what
// the artists chose for the same visual. The search should score it close to the best donor it can find, or rank it near the top.
int Validate()
{
    string? rom = Option("--botw-rom");
    if (args.Length < 2 || rom is null) return Usage();
    int max = int.TryParse(Option("--max"), out int m) ? m : 100;
    var db = ShaderDatabase.Load(args[1]);
    var finder = new DonorFinder(db);
    var totkByName = db.FilesOf(ShaderGame.TotK).ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

    int compared = 0, close = 0, top1 = 0, top10 = 0, sameProgram = 0, slotMismatch = 0;
    var gaps = new List<double>();
    foreach (ShaderFileEntry botwEntry in db.FilesOf(ShaderGame.BotW))
    {
        if (!totkByName.TryGetValue(botwEntry.Name, out ShaderFileEntry? totkEntry) || compared >= max * 20) continue;
        string path = Path.Combine(rom, "Effect", botwEntry.Name + ".sesetlist");
        if (!File.Exists(path)) continue;
        PtclFile botw = PtclFile.ReadSesetlist(File.ReadAllBytes(path));
        IReadOnlyList<BotwEmitterShaders>? sources = finder.SourceEmitters(botwEntry.Name, botw);
        if (sources is null) continue;
        foreach (BotwEmitterShaders source in sources)
        {
            int truth = Array.FindIndex(totkEntry.Emitters, e => e.Name == source.Name && db.Resolve(totkEntry, e.Normal) is not null);
            if (truth < 0 || compared >= max * 20) continue;
            IReadOnlyList<DonorCandidate> ranked = finder.Rank(source, top: 4000);
            if (ranked.Count == 0) { slotMismatch++; compared++; continue; }
            int rank = ranked.ToList().FindIndex(c => c.File == totkEntry && c.EmitterIndex == truth);
            compared++;
            if (rank < 0) { slotMismatch++; continue; }
            double gap = ranked[0].Score - ranked[rank].Score;
            gaps.Add(gap);
            if (gap <= 0.05) close++;
            if (rank == 0) top1++;
            if (rank < 10) top10++;
            ProgramSignature? bestProgram = db.Resolve(ranked[0].File, ranked[0].Emitter.Normal);
            if (bestProgram is not null && bestProgram.Id == db.Resolve(totkEntry, totkEntry.Emitters[truth].Normal)?.Id) sameProgram++;
        }
    }
    Console.WriteLine($"compared {compared} emitters present in both games");
    Console.WriteLine($"  TotK's own program cannot even be a candidate (texture slots differ): {slotMismatch}");
    Console.WriteLine($"  rank 1: {top1}, top 10: {top10}, best score within 0.05 of TotK's own: {close}, best program is TotK's own program: {sameProgram}");
    if (gaps.Count > 0) Console.WriteLine($"  score gap to best: median {gaps.Order().ElementAt(gaps.Count / 2):F3}, mean {gaps.Average():F3}");
    return 0;
}
