// TotK mod kit: builds actor packs, RSDB rows and related files from copies of the vanilla files. It never writes to the vanilla
// romfs; every output goes to the --out folder.
//   TotkModKit sarc-test   --romfs <vanilla romfs> <Actor name>        round-trip a pack through the SARC reader/writer
//   TotkModKit build-ganon-beam --romfs <vanilla> --out <mod romfs> --xlink <xlink.exe> --effect <file> [--textures <dir>]
//   TotkModKit rsdb-scan   --romfs <vanilla romfs> <Actor name>        which RSDB tables have a row for the actor
using BymlSharp;
using TotkModKit;

string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
string romfs = Option("--romfs") ?? throw new ArgumentException("--romfs <vanilla romfs root> is required");
string command = args[0];
var dictionaries = ZsDictionaries.Load(romfs);

switch (command)
{
    case "sarc-test":
    {
        string actor = args[^1];
        byte[] pack = dictionaries.Decompress(File.ReadAllBytes(Path.Combine(romfs, "Pack", "Actor", actor + ".pack.zs")), out uint id);
        var files = Sarc.Read(pack);
        byte[] again = Sarc.Write(files);
        var back = Sarc.Read(again);
        Console.WriteLine($"{actor}: dict {id}, {files.Count} files, rewritten {again.Length} bytes (original {pack.Length})");
        Console.WriteLine(files.Select(f => f.Name).Order().SequenceEqual(back.Select(f => f.Name).Order()) && files.All(f => back.First(b => b.Name == f.Name).Data.SequenceEqual(f.Data)) ? "round trip identical" : "ROUND TRIP DIFFERS");
        break;
    }
    case "rsdb-scan":
    {
        string actor = args[^1];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(romfs, "RSDB"), "*.byml.zs").Order())
        {
            byte[] data = dictionaries.Decompress(File.ReadAllBytes(path), out uint id);
            Byml root = Byml.FromBinary(data);
            int hits = 0;
            if (root.IsArray)
                foreach (Byml row in root.AsArray)
                    if (row.IsMap && row.AsMap.Values.Any(v => v.Type == BymlType.String && v.AsString() == actor)) hits++;
            Console.WriteLine($"{Path.GetFileName(path),-60} dict {id} rows {(root.IsArray ? root.Count : -1),6} rows mentioning {actor}: {hits}");
        }
        break;
    }
    case "byml-test":
    {
        string actor = args[^1];
        byte[] pack = dictionaries.Decompress(File.ReadAllBytes(Path.Combine(romfs, "Pack", "Actor", actor + ".pack.zs")), out _);
        int same = 0, different = 0, failed = 0;
        foreach ((string name, byte[] data) in Sarc.Read(pack))
        {
            if (!name.EndsWith(".bgyml", StringComparison.Ordinal)) continue;
            try
            {
                Byml b = Byml.FromBinary(data);
                byte[] again = b.ToBinary(BymlFile.FromBinary(data).Version);
                if (again.AsSpan().SequenceEqual(data)) same++;
                else { different++; if (different < 4) Console.WriteLine($"differs: {name} ({data.Length} -> {again.Length})"); }
            }
            catch (Exception e) { failed++; Console.WriteLine($"failed: {name}: {e.Message}"); }
        }
        Console.WriteLine($"identical {same}, different {different}, failed {failed}");
        break;
    }
    case "build-ganon-beam":
    {
        // TotkModKit build-ganon-beam --romfs <vanilla> --out <mod romfs> --xlink <xlink.exe> --effect <GanonBeastBeam.esetb.byml.zs> [--textures <TexToGo dir>]
        var mod = new GanonBeamMod(romfs, Option("--out") ?? throw new ArgumentException("--out"), Option("--xlink") ?? throw new ArgumentException("--xlink"), Option("--python") ?? "python", Option("--elink-user"));
        mod.Build(Option("--effect") ?? throw new ArgumentException("--effect"), Option("--textures"));
        break;
    }
    default:
        Console.Error.WriteLine("unknown command");
        return 1;
}
return 0;
