// Ports a BotW effect (.sesetlist) to TotK (.esetb.byml.zs).
//
//   PtclConvert <botw.sesetlist> <donor.esetb.byml.zs> <ZsDic.pack.zs> <output.esetb.byml.zs> [--donor-emitter <name>] [--all-notes]
//               [--botw-rom <ROM root> --totk-romfs <romfs root> [--texture-out <dir>] [--texture-cache <file>]]
//               [--model-cache <file>] [--rename-set <from>=<to> ...]
//               [--shader-db <shaderdb.json.gz> [--donor-file <TotK effect name>]]
//
// The donor is any TotK effect: its shader archive, primitives and the shader selection of one of its emitters (the first one
// with a custom-shader chunk, or the one named by --donor-emitter) are used for every converted emitter, because BotW shaders
// cannot be translated. Meshes are not carried over; the report lists everything that needs attention.
//
// With --shader-db (built by PtclShaderDb) and --totk-romfs, pass "auto" as the donor: the TotK effect whose shader archive best
// serves the whole BotW effect is chosen from the database, and every emitter gets the shader selection of its closest donor
// emitter in that archive (--donor-file picks the TotK effect yourself).
//
// --rename-set renames an emitter set (repeatable). The game spawns sets by name from the file it loads, so a converted effect that
// replaces a TotK file must carry the names that file had (for example Obj_MasterBeam in PlayerBeam).
//
// With --botw-rom and --totk-romfs the G3D models the emitters use are carried too: a model TotK also has (same id) is copied from the
// TotK file that has it, any other is converted from the BotW BFRES (see PtclSharp.Models) and the file's G3PR is rebuilt.
//
// With --botw-rom and --totk-romfs the textures are carried too: a texture TotK already has (same GUID) is reused, and one it
// lacks is converted from the BotW BNTX to a TexToGo .txtg written to --texture-out (default: a TexToGo folder beside the
// output); the file's Textures list is rebuilt to match.
using PtclSharp;
using PtclSharp.Conversion;
using PtclSharp.Models;
using PtclSharp.Shaders;
using PtclSharp.Textures;
using PtclSharp.Writer;

if (args.Length < 4)
{
    Console.Error.WriteLine("usage: PtclConvert <botw.sesetlist> <donor.esetb.byml.zs> <ZsDic.pack.zs> <output.esetb.byml.zs> [--donor-emitter <name>] [--all-notes] [--botw-rom <dir> --totk-romfs <dir> [--texture-out <dir>] [--texture-cache <file>]] [--shader-db <file> [--donor-file <name>]]");
    return 1;
}

string botwPath = args[0], donorPath = args[1], dictPath = args[2], outPath = args[3];
string? donorEmitterName = null;
bool allNotes = false;
var renames = new Dictionary<string, string>(StringComparer.Ordinal);
var donorOverrides = new Dictionary<string, (string Name, int Normal)>(StringComparer.Ordinal);
string? botwRom = null, totkRomfs = null, textureOut = null, textureCache = null, modelCache = null, shaderDb = null, donorFile = null;
for (int i = 4; i < args.Length; i++)
{
    if (args[i] == "--donor-emitter" && i + 1 < args.Length) donorEmitterName = args[++i];
    else if (args[i] == "--all-notes") allNotes = true;
    else if (args[i] == "--botw-rom" && i + 1 < args.Length) botwRom = args[++i];
    else if (args[i] == "--totk-romfs" && i + 1 < args.Length) totkRomfs = args[++i];
    else if (args[i] == "--texture-out" && i + 1 < args.Length) textureOut = args[++i];
    else if (args[i] == "--texture-cache" && i + 1 < args.Length) textureCache = args[++i];
    else if (args[i] == "--model-cache" && i + 1 < args.Length) modelCache = args[++i];
    else if (args[i] == "--rename-set" && i + 1 < args.Length && args[++i].Split('=', 2) is { Length: 2 } pair) renames[pair[0]] = pair[1];
    else if (args[i] == "--shader-db" && i + 1 < args.Length) shaderDb = args[++i];
    else if (args[i] == "--donor-file" && i + 1 < args.Length) donorFile = args[++i];
    else if (args[i] == "--donor-for" && i + 1 < args.Length && args[++i].Split('=', 2) is { Length: 2 } map && map[1].Split('@') is { Length: 2 } target && int.TryParse(target[1], out int normal))
        donorOverrides[map[0]] = (target[0], normal);
}
if ((botwRom is null) != (totkRomfs is null))
{
    Console.Error.WriteLine("--botw-rom and --totk-romfs go together.");
    return 1;
}

byte[] dictionary = PtclDictionary.FromZsDicPackFile(dictPath);
PtclFile botw = PtclFile.ReadSesetlist(File.ReadAllBytes(botwPath));
Func<byte[], ShaderBinding?> binder;
PtclFile donor;
VfxbDocument donorDoc;
if (shaderDb is not null)
{
    if (totkRomfs is null) { Console.Error.WriteLine("--shader-db needs --totk-romfs to load the chosen donor file."); return 1; }
    var finder = new DonorFinder(ShaderDatabase.Load(shaderDb));
    IReadOnlyList<BotwEmitterShaders>? sources = finder.SourceEmitters(Path.GetFileNameWithoutExtension(botwPath), botw);
    if (sources is null) { Console.Error.WriteLine("This BotW file is not in the shader database (rebuild it with PtclShaderDb build)."); return 1; }

    IReadOnlyList<DonorPlan> plans = finder.Plan(sources, top: donorFile is null ? 3 : int.MaxValue);
    DonorPlan? plan = donorFile is null ? plans.FirstOrDefault() : plans.FirstOrDefault(p => string.Equals(p.File.Name, donorFile, StringComparison.OrdinalIgnoreCase));
    if (plan is null) { Console.Error.WriteLine(donorFile is null ? "No TotK shader archive can serve this effect." : $"'{donorFile}' cannot serve this effect (or is not a TotK effect in the database)."); return 1; }
    foreach (DonorPlan p in plans.Take(3))
        Console.WriteLine($"donor candidate {p.File.Name}: {p.Score:F3} (best possible with free choice per emitter {p.Ceiling:F3})");
    if (donorOverrides.Count > 0)
    {
        // The scoring compares shader signatures, not what a shader does; --donor-for lets a human pick the donor emitter (inside the chosen
        // file's archive) for a source emitter, as <source emitter>=<donor emitter>@<its shader_idx_normal>.
        var chosen = new List<EmitterDonor>();
        foreach (EmitterDonor e in plan.Emitters)
        {
            if (donorOverrides.TryGetValue(e.Source.Name, out var wanted))
            {
                int at = Array.FindIndex(plan.File.Emitters, u => u.Name == wanted.Name && u.Normal == wanted.Normal);
                if (at < 0) { Console.Error.WriteLine($"--donor-for: {plan.File.Name} has no emitter {wanted.Name}@{wanted.Normal}."); return 1; }
                chosen.Add(new EmitterDonor(e.Source, new DonorCandidate(plan.File, at, plan.File.Emitters[at], 1.0, ["chosen by --donor-for"])));
            }
            else chosen.Add(e);
        }
        plan = new DonorPlan(plan.File, plan.Score, plan.Ceiling, chosen);
    }
    Console.WriteLine($"donor file: {plan.File.Name}");
    donor = PtclFile.ReadEsetb(File.ReadAllBytes(Path.Combine(totkRomfs, "Effect", plan.File.Name + ".Nin_NX_NVN.esetb.byml.zs")), dictionary);
    donorDoc = VfxbDocument.From(donor.Vfxb);
    binder = DonorBinding.CreateBinder(plan, donorDoc);
    foreach (EmitterDonor e in plan.Emitters)
        Console.WriteLine($"  {e.Source.Name} -> {(e.Donor is null ? "(no donor)" : $"{e.Donor.Emitter.Name} [{e.Donor.Score:F2}]")}{(allNotes && e.Donor is not null ? ": " + string.Join("; ", e.Donor.Notes) : "")}");
}
else
{
    donor = PtclFile.ReadEsetb(File.ReadAllBytes(donorPath), dictionary);
    donorDoc = VfxbDocument.From(donor.Vfxb);

    VfxbTreeNode? donorEmitter = donorDoc.Sets.SelectMany(s => s.Children).FirstOrDefault(e =>
        e.Attributes.Any(a => a.Kind == "CSDP") &&
        (donorEmitterName is null || EmitterName(e) == donorEmitterName));
    if (donorEmitter is null)
    {
        Console.Error.WriteLine("The donor has no emitter with a custom-shader chunk" + (donorEmitterName is null ? "." : $" named '{donorEmitterName}'."));
        return 1;
    }
    Console.WriteLine($"donor emitter: {EmitterName(donorEmitter)}");
    var binding = ShaderBinding.FromDonorEmitter(donorEmitter.Data!, donorEmitter.Attributes.First(a => a.Kind == "CSDP").Data);
    binder = _ => binding;
}
TexturePlan? texturePlan = null;
ModelPlan? modelPlan = null;
PrimitivePlan? primitivePlan = null;
TotkModelIndex? modelIndex = null;
if (botwRom is not null && totkRomfs is not null)
{
    string texToGo = Path.Combine(totkRomfs, "TexToGo");
    TotkTextureIndex index = textureCache is not null && File.Exists(textureCache)
        ? TotkTextureIndex.Load(textureCache, texToGo)
        : TotkTextureIndex.Build(Path.Combine(totkRomfs, "Effect"), dictionary, texToGo);
    if (textureCache is not null && !File.Exists(textureCache)) index.Save(textureCache);
    PtclFile residentFile = ResidentFile.LoadFromPack(Path.Combine(botwRom, "Pack", "Bootup.pack"));
    BotwTextureCatalog resident = BotwTextureCatalog.ForResident(residentFile);
    texturePlan = TexturePlanner.Plan(botw, BotwTextureCatalog.For(botw, resident), index);

    string totkEffects = Path.Combine(totkRomfs, "Effect");
    modelIndex = modelCache is not null && File.Exists(modelCache)
        ? TotkModelIndex.Load(modelCache, totkEffects, dictionary)
        : TotkModelIndex.Build(totkEffects, dictionary);
    if (modelCache is not null && !File.Exists(modelCache)) modelIndex.Save(modelCache);
    var modelCatalog = new BotwModelCatalog(botw, residentFile);
    modelPlan = ModelPlanner.Plan(botw, modelCatalog, modelIndex);
    primitivePlan = PrimitivePlan.Plan(botw, modelCatalog, modelIndex);
}

var options = new ConverterOptions { ShaderBinder = binder, TextureMap = texturePlan is null ? null : texturePlan.Map, CarriedModelIds = modelPlan?.CarriedIds, CarriedMeshIds = primitivePlan?.CarriedIds };
VfxbDocument converted = BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donorDoc, options, out ConversionReport report);

G3dBuild? models = null;
if (modelPlan is not null && modelIndex is not null)
{
    models = G3dBuilder.Build(modelPlan, modelIndex);
    models.ApplyTo(converted);
    primitivePlan?.ApplyTo(converted);
}

foreach (VfxbTreeNode set in converted.Sets)
{
    string current = System.Text.Encoding.UTF8.GetString(set.Data!, 0x10, 0x40).TrimEnd(' ');
    if (!renames.TryGetValue(current, out string? renamed)) continue;
    Array.Clear(set.Data!, 0x10, 0x40);
    System.Text.Encoding.UTF8.GetBytes(renamed, set.Data.AsSpan(0x10, 0x3F));
    Console.WriteLine($"renamed set {current} -> {renamed}");
}
string[] sets = botw.Vfxb.EmitterSets.Select(s => renames.GetValueOrDefault(s.Name, s.Name)).ToArray();
IReadOnlyList<PtclTextureReference> textures = texturePlan is null ? donor.Textures : texturePlan.TextureReferences.ToList();
File.WriteAllBytes(outPath, PtclWriter.WriteEsetb(donor, converted.ToBytes(), dictionary, sets, textures));

if (modelPlan is not null && models is not null)
{
    Console.WriteLine($"models: {modelPlan.Items.Count(i => i.Disposition == ModelDisposition.ReuseTotk)} copied from TotK, " +
        $"{modelPlan.Items.Count(i => i.Disposition == ModelDisposition.Convert)} converted, {modelPlan.Unresolved.Count()} unresolved");
    foreach (string name in models.ModelNames) Console.WriteLine($"  model {name}");
    foreach (string note in models.Notes) Console.WriteLine($"    note: {note}");
    if (primitivePlan is not null && primitivePlan.Items.Count > 0)
        Console.WriteLine($"mesh primitives: {primitivePlan.CarriedIds.Count} carried, {primitivePlan.Items.Count(i => i.Node is null)} unresolved");
    foreach (ModelPlanItem item in modelPlan.Unresolved) Console.WriteLine($"  UNRESOLVED model {item.Id:X8}: {item.Problem}");
}

if (texturePlan is not null)
{
    string dir = textureOut ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!, "TexToGo");
    Console.WriteLine($"textures: {texturePlan.Items.Count(i => i.Disposition == TextureDisposition.ReuseTotk)} reused, " +
        $"{texturePlan.Items.Count(i => i.Disposition == TextureDisposition.ReuseTotkByName)} reused by name, " +
        $"{texturePlan.Imports.Count()} imported, {texturePlan.Unresolved.Count()} unresolved");
    foreach (TexturePlanItem item in texturePlan.Imports)
    {
        Directory.CreateDirectory(dir);
        TxtgConversion converted_ = BntxToTxtg.Convert(item.Source!.Pixels!);
        File.WriteAllBytes(Path.Combine(dir, item.Name + ".txtg"), converted_.Bytes);
        Console.WriteLine($"  imported {item.Name}.txtg");
        foreach (string note in converted_.Notes) Console.WriteLine($"    note: {note}");
    }
    foreach (TexturePlanItem item in texturePlan.Unresolved)
        Console.WriteLine($"  UNRESOLVED {item.BotwGuid:X8} {item.Name}: {item.Problem}");
}

Console.WriteLine($"converted {report.SetsConverted} set(s), {report.EmittersConverted} emitter(s), {report.ChunksConverted} chunk(s) ({report.ChunksDropped} dropped), {report.FieldsCopied} field copies");
foreach (var (severity, message, count) in report.Summary())
    if (allNotes || severity != ConversionSeverity.Info)
        Console.WriteLine($"  {severity,-7} x{count,-3} {message}");
Console.WriteLine($"wrote {outPath}");
return report.HasErrors ? 2 : 0;

static string EmitterName(VfxbTreeNode e)
{
    int end = Array.IndexOf(e.Data!, (byte)0, 0x10);
    return System.Text.Encoding.UTF8.GetString(e.Data!, 0x10, Math.Min(end < 0 ? 0x40 : end - 0x10, 0x40));
}
