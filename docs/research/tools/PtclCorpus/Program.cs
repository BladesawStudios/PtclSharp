// Corpus extractor for the TotK PTCL research (docs/research/totk-emtr-offsets-ghidra.md).
// usage: PtclCorpus <command> <game> <root> <outPath>   (game: totk | botw)
//   totk root = romfs root (contains Effect/*.esetb.byml.zs and Pack/ZsDic.pack.zs); botw root = ROM root (contains Effect/*.sesetlist)
//   emtr   every EMTR data block (0x10C8 bytes for TotK, 0xA88 for BotW): [i32 len][64B file name][u32 node size][data]
//   eset   every ESET data block (0x60 bytes for BotW, 0xB4 for TotK), same record format as emtr
//   attrs  <romfs> <out.bin>   every non-ESTA/ESET/EMTR node: [4B kind][4B parent kind][i32 len][node bytes]
//   bnsh   <romfs> <outDir>    every distinct BNSH inside GRSN/GRSR, named <sha1>.bnsh (+ .src with the source file)
// The shared Zstd dictionary (ID 1) is read from <romfs>/Pack/ZsDic.pack.zs.
using System.Security.Cryptography;
using System.Text;
using PtclSharp;
using ZstdSharp;

if (args.Length < 4) { Console.Error.WriteLine("usage: PtclCorpus <emtr|attrs|bnsh> <totk|botw> <root> <out>"); return 1; }
string cmd = args[0], game = args[1], romfs = args[2], outPath = args[3];
bool botw = game == "botw";
int emitterSize = botw ? 0xA88 : 0x10C8;

byte[]? dict = null;
if (!botw)
{
    byte[] sarc;
    using (var d = new Decompressor()) sarc = d.Unwrap(File.ReadAllBytes(Path.Combine(romfs, "Pack", "ZsDic.pack.zs"))).ToArray();
    int dataOff = BitConverter.ToInt32(sarc, 0xC), sfat = 0x14, cnt = BitConverter.ToUInt16(sarc, sfat + 6);
    for (int i = 0; i < cnt; i++)
    {
        int e = sfat + 12 + i * 16, s = BitConverter.ToInt32(sarc, e + 8), en = BitConverter.ToInt32(sarc, e + 12);
        var cand = sarc[(dataOff + s)..(dataOff + en)];
        if (BitConverter.ToUInt32(cand, 4) == 1) dict = cand; // dictionary ID 1 is what the .esetb files use
    }
}

IEnumerable<(string name, VfxbFile vfxb)> Load()
{
    string pattern = botw ? "*.sesetlist" : "*.esetb.byml.zs";
    foreach (var f in Directory.GetFiles(Path.Combine(romfs, "Effect"), pattern).Order(StringComparer.Ordinal))
    {
        VfxbFile? v = null;
        try { v = botw ? PtclFile.ReadSesetlist(File.ReadAllBytes(f)).Vfxb : PtclFile.ReadEsetb(File.ReadAllBytes(f), dict).Vfxb; }
        catch (Exception ex) { Console.Error.WriteLine($"{Path.GetFileName(f)}: {ex.Message}"); }
        if (v != null) yield return (Path.GetFileName(f), v);
    }
}

switch (cmd)
{
    case "emtr":
    {
        using var bw = new BinaryWriter(File.Create(outPath));
        int n = 0;
        foreach (var (name, v) in Load())
        {
            void W(VfxbNode node)
            {
                if (node.Kind == "EMTR" && node.DataOffset is int o)
                {
                    int len = Math.Min(emitterSize, v.Data.Length - o);
                    bw.Write(len); bw.Write(Encoding.ASCII.GetBytes(name.PadRight(64)[..64])); bw.Write(node.Size); bw.Write(v.Data, o, len); n++;
                }
                foreach (var c in node.Children) W(c);
            }
            foreach (var r in v.Roots) W(r);
        }
        Console.WriteLine($"emtr {n}");
        break;
    }
    case "tree":
    {
        // usage: PtclCorpus tree <game> <root> <fileName>  -> prints the node tree with offsets, sizes and data ranges
        foreach (var (name, v) in Load())
        {
            if (name != outPath) continue;
            Console.WriteLine($"{name} file={v.Data.Length:X} firstBlock={v.Header.FirstBlockOffset:X}");
            void P(VfxbNode n, int ind, string tag)
            {
                Console.WriteLine($"{new string(' ', ind)}{tag}{n.Kind} @{n.Offset:X} size={n.Size:X} child={n.ChildRelativeOffset:X} sib={n.SiblingRelativeOffset:X} attr={n.AttributeRelativeOffset:X} data={(n.DataOffset is int d ? d.ToString("X") : "-")} cnt={n.DeclaredChildCount}");
                foreach (var a in n.Attributes) P(a, ind + 2, "A:");
                foreach (var c in n.Children) P(c, ind + 2, "C:");
            }
            foreach (var r in v.Roots) P(r, 0, "");
        }
        break;
    }
    case "tex":
    {
        // usage: PtclCorpus tex <game> <root> <fileName> : texture list (TotK) and per-emitter texture GUIDs
        foreach (var f in Directory.GetFiles(Path.Combine(romfs, "Effect"), botw ? "*.sesetlist" : "*.esetb.byml.zs"))
        {
            if (Path.GetFileName(f) != outPath) continue;
            var ptcl = botw ? PtclFile.ReadSesetlist(File.ReadAllBytes(f)) : PtclFile.ReadEsetb(File.ReadAllBytes(f), dict);
            foreach (var t in ptcl.Textures) Console.WriteLine($"texture {t.Name} guid={t.Guid:X8}");
            foreach (var set in ptcl.Vfxb.EmitterSets)
                foreach (var e in set.Emitters)
                    Console.WriteLine($"{set.Name}/{e.Name}: " + string.Join(" ", e.TextureSamplerGuids.Select(g => g is ulong u ? u.ToString("X16") : "-")));
        }
        break;
    }
    case "eset":
    {
        int esetSize = botw ? 0x60 : 0xB4;
        using var bw = new BinaryWriter(File.Create(outPath));
        int n = 0;
        foreach (var (name, v) in Load())
        {
            void W(VfxbNode node)
            {
                if (node.Kind == "ESET" && node.DataOffset is int o)
                {
                    int len = Math.Min(esetSize, v.Data.Length - o);
                    bw.Write(len); bw.Write(Encoding.ASCII.GetBytes(name.PadRight(64)[..64])); bw.Write(node.Size); bw.Write(v.Data, o, len); n++;
                }
                foreach (var c in node.Children) W(c);
            }
            foreach (var r in v.Roots) W(r);
        }
        Console.WriteLine($"eset {n}");
        break;
    }
    case "attrs":
    {
        using var bw = new BinaryWriter(File.Create(outPath));
        foreach (var (_, v) in Load())
        {
            var d = v.Data;
            void W(VfxbNode n, string parent)
            {
                if (n.Kind is not ("ESTA" or "ESET" or "EMTR"))
                {
                    int end = n.Offset + (int)n.Size;
                    if (end > d.Length || n.Size == 0) end = Math.Min(d.Length, n.Offset + 0x20);
                    bw.Write(Encoding.ASCII.GetBytes((n.Kind + "    ")[..4])); bw.Write(Encoding.ASCII.GetBytes((parent + "    ")[..4]));
                    bw.Write(end - n.Offset); bw.Write(d, n.Offset, end - n.Offset);
                }
                foreach (var a in n.Attributes) W(a, n.Kind);
                foreach (var c in n.Children) W(c, n.Kind);
            }
            foreach (var r in v.Roots) W(r, "ROOT");
        }
        break;
    }
    case "bnsh":
    {
        Directory.CreateDirectory(outPath);
        var seen = new HashSet<string>();
        foreach (var (name, v) in Load())
        {
            var d = v.Data;
            var grsn = v.Roots.FirstOrDefault(r => r.Kind == "GRSN");
            if (grsn == null) continue;
            // the library does not follow GRSN children, so search for the BNSH magic after the GRSN node
            for (int i = grsn.Offset; i + 4 <= d.Length; i++)
            {
                if (d[i] == 'B' && d[i + 1] == 'N' && d[i + 2] == 'S' && d[i + 3] == 'H')
                {
                    var blob = d[i..];
                    var h = Convert.ToHexString(SHA1.HashData(blob));
                    if (seen.Add(h)) { File.WriteAllBytes(Path.Combine(outPath, h + ".bnsh"), blob); File.WriteAllText(Path.Combine(outPath, h + ".src"), name); }
                    break;
                }
            }
        }
        Console.WriteLine($"unique bnsh {seen.Count}");
        break;
    }
    default: Console.Error.WriteLine("unknown command"); return 1;
}
return 0;
