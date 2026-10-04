// Corpus extractor for the TotK PTCL research (docs/research/totk-emtr-offsets-ghidra.md).
// usage: PtclCorpus <command> <romfsRoot> <outPath>
//   emtr   <romfs> <out.bin>   every EMTR data block (0x10C8 bytes): [i32 len][64B file name][u32 node size][data]
//   attrs  <romfs> <out.bin>   every non-ESTA/ESET/EMTR node: [4B kind][4B parent kind][i32 len][node bytes]
//   bnsh   <romfs> <outDir>    every distinct BNSH inside GRSN/GRSR, named <sha1>.bnsh (+ .src with the source file)
// The shared Zstd dictionary (ID 1) is read from <romfs>/Pack/ZsDic.pack.zs.
using System.Security.Cryptography;
using System.Text;
using PtclSharp;
using ZstdSharp;

if (args.Length < 3) { Console.Error.WriteLine("usage: PtclCorpus <emtr|attrs|bnsh> <romfs> <out>"); return 1; }
string cmd = args[0], romfs = args[1], outPath = args[2];

byte[] sarc;
using (var d = new Decompressor()) sarc = d.Unwrap(File.ReadAllBytes(Path.Combine(romfs, "Pack", "ZsDic.pack.zs"))).ToArray();
int dataOff = BitConverter.ToInt32(sarc, 0xC), sfat = 0x14, cnt = BitConverter.ToUInt16(sarc, sfat + 6);
byte[]? dict = null;
for (int i = 0; i < cnt; i++)
{
    int e = sfat + 12 + i * 16, s = BitConverter.ToInt32(sarc, e + 8), en = BitConverter.ToInt32(sarc, e + 12);
    var cand = sarc[(dataOff + s)..(dataOff + en)];
    if (BitConverter.ToUInt32(cand, 4) == 1) dict = cand; // dictionary ID 1 is what the .esetb files use
}

IEnumerable<(string name, VfxbFile vfxb)> Load()
{
    foreach (var f in Directory.GetFiles(Path.Combine(romfs, "Effect"), "*.esetb.byml.zs"))
    {
        VfxbFile? v = null;
        try { v = PtclFile.ReadEsetb(File.ReadAllBytes(f), dict).Vfxb; }
        catch { /* 62 files are rejected by the current tree reader; see the doc's container section */ }
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
                    int len = Math.Min(0x10C8, v.Data.Length - o);
                    bw.Write(len); bw.Write(Encoding.ASCII.GetBytes(name.PadRight(64)[..64])); bw.Write(node.Size); bw.Write(v.Data, o, len); n++;
                }
                foreach (var c in node.Children) W(c);
            }
            foreach (var r in v.Roots) W(r);
        }
        Console.WriteLine($"emtr {n}");
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
