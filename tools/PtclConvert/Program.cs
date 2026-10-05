// Ports a BotW effect (.sesetlist) to TotK (.esetb.byml.zs).
//
//   PtclConvert <botw.sesetlist> <donor.esetb.byml.zs> <ZsDic.pack.zs> <output.esetb.byml.zs> [--donor-emitter <name>] [--all-notes]
//
// The donor is any TotK effect: its shader archive, primitives and the shader selection of one of its emitters (the first one
// with a custom-shader chunk, or the one named by --donor-emitter) are used for every converted emitter, because BotW shaders
// cannot be translated. Textures and meshes are not carried over; the report lists everything that needs attention.
using PtclSharp;
using PtclSharp.Conversion;
using PtclSharp.Writer;

if (args.Length < 4)
{
    Console.Error.WriteLine("usage: PtclConvert <botw.sesetlist> <donor.esetb.byml.zs> <ZsDic.pack.zs> <output.esetb.byml.zs> [--donor-emitter <name>] [--all-notes]");
    return 1;
}

string botwPath = args[0], donorPath = args[1], dictPath = args[2], outPath = args[3];
string? donorEmitterName = null;
bool allNotes = false;
for (int i = 4; i < args.Length; i++)
{
    if (args[i] == "--donor-emitter" && i + 1 < args.Length) donorEmitterName = args[++i];
    else if (args[i] == "--all-notes") allNotes = true;
}

byte[] dictionary = PtclDictionary.FromZsDicPackFile(dictPath);
PtclFile botw = PtclFile.ReadSesetlist(File.ReadAllBytes(botwPath));
PtclFile donor = PtclFile.ReadEsetb(File.ReadAllBytes(donorPath), dictionary);
VfxbDocument donorDoc = VfxbDocument.From(donor.Vfxb);

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
VfxbDocument converted = BotwToTotkConverter.ToTotk(VfxbDocument.From(botw.Vfxb), donorDoc, new ConverterOptions { ShaderBinder = _ => binding }, out ConversionReport report);

string[] sets = botw.Vfxb.EmitterSets.Select(s => s.Name).ToArray();
File.WriteAllBytes(outPath, PtclWriter.WriteEsetb(donor, converted.ToBytes(), dictionary, sets, donor.Textures));

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
