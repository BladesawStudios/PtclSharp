// Re-skins a TotK effect: renames its emitter sets and shifts its colours. The input file is only read.
//
//   PtclTint <in.esetb.byml.zs> <ZsDic.pack.zs> <out.esetb.byml.zs> [--rename-set from=to ...] [--recolor <gMul>,<bMul>,<swap 0|1>]
//                            [--width <factor>] [--only-set <name> ...]
//
// --recolor gMul,bMul,swap: for every colour (the colour key frames, the constant colours and the emitter colours) the new green is
//   gMul * old green and the new blue is bMul * old blue; with swap=1 the old green and blue trade places first. Fire orange
//   (1, 0.25, 0.04) becomes pink with `1,1.4,1`... see docs/porting/totk-line-beam-and-master-sword.md section 8.4.
// --width f: multiplies particle_scale x and z of every emitter by f (the beam's thickness; y is the length axis).
// --only-set: drop every set that is not named (names are the ones in the input file, before renaming).
using PtclSharp;
using PtclSharp.Layout;
using PtclSharp.Writer;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: PtclTint <in.esetb.byml.zs> <ZsDic.pack.zs> <out.esetb.byml.zs> [--rename-set a=b] [--recolor g,b,swap] [--width f] [--only-set name]");
    return 1;
}

var renames = new Dictionary<string, string>(StringComparer.Ordinal);
var only = new HashSet<string>(StringComparer.Ordinal);
float gMul = 1, bMul = 1, width = 1;
bool swap = false, recolor = false;
for (int i = 3; i < args.Length; i++)
{
    if (args[i] == "--rename-set" && args[++i].Split('=', 2) is { Length: 2 } pair) renames[pair[0]] = pair[1];
    else if (args[i] == "--only-set") only.Add(args[++i]);
    else if (args[i] == "--width") width = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
    else if (args[i] == "--recolor")
    {
        string[] p = args[++i].Split(',');
        gMul = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
        bMul = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
        swap = p[2] == "1";
        recolor = true;
    }
}

byte[] dictionary = PtclDictionary.FromZsDicPackFile(args[1]);
PtclFile file = PtclFile.ReadEsetb(File.ReadAllBytes(args[0]), dictionary);
VfxbDocument document = VfxbDocument.From(file.Vfxb);
PtclLayoutSet layouts = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

void Recolor(Span<float> rgbPairs, int stride)
{
    for (int i = 0; i + 2 < rgbPairs.Length; i += stride)
    {
        float g = rgbPairs[i + 1], b = rgbPairs[i + 2];
        if (swap) (g, b) = (b, g);
        rgbPairs[i + 1] = g * gMul;
        rgbPairs[i + 2] = b * bMul;
    }
}

void Emitters(VfxbTreeNode node, Action<VfxbTreeNode> each)
{
    foreach (VfxbTreeNode child in node.Children)
    {
        if (child.Kind == "EMTR") each(child);
        Emitters(child, each);
    }
}

var kept = new List<VfxbTreeNode>();
var names = new List<string>();
foreach (VfxbTreeNode set in document.Sets)
{
    string name = System.Text.Encoding.UTF8.GetString(set.Data!, 0x10, 0x40).TrimEnd('\0', ' ');
    if (only.Count > 0 && !only.Contains(name)) continue;
    string final = renames.GetValueOrDefault(name, name);
    Array.Clear(set.Data!, 0x10, 0x40);
    System.Text.Encoding.UTF8.GetBytes(final, set.Data.AsSpan(0x10, 0x3F));
    kept.Add(set);
    names.Add(final);
    Console.WriteLine($"set {name} -> {final}");

    Emitters(set, e =>
    {
        var view = new StructView(layouts.Emitter, e.Data!);
        string emitter = view.GetString("emitter_name");
        if (recolor)
        {
            // kf_color0 / kf_color1 are 8 keys of (r, g, b, time); the constant colours and emitter colours are plain rgb triples.
            foreach (string field in new[] { "kf_color0", "kf_color1" })
            {
                var keys = new float[32];
                for (int k = 0; k < 32; k++) keys[k] = view.GetSingle(field, k);
                Recolor(keys, 4);
                for (int k = 0; k < 32; k++) view.SetSingle(field, keys[k], k);
            }
            foreach (string field in new[] { "color0_const_rgb", "color1_const_rgb", "emitter_color0_rgb", "emitter_color1_rgb" })
            {
                var rgb = new float[3];
                for (int k = 0; k < 3; k++) rgb[k] = view.GetSingle(field, k);
                Recolor(rgb, 3);
                for (int k = 0; k < 3; k++) view.SetSingle(field, rgb[k], k);
            }
        }
        if (width != 1)
        {
            view.SetSingle("particle_scale_xyz", view.GetSingle("particle_scale_xyz", 0) * width, 0);
            view.SetSingle("particle_scale_xyz", view.GetSingle("particle_scale_xyz", 2) * width, 2);
        }
        Console.WriteLine($"  {emitter}: particle_scale ({view.GetSingle("particle_scale_xyz", 0):G4}, {view.GetSingle("particle_scale_xyz", 1):G4}, {view.GetSingle("particle_scale_xyz", 2):G4})");
    });
}

VfxbDocument result = document.WithSets(kept);
result.RecountChildren();
File.WriteAllBytes(args[2], PtclWriter.WriteEsetb(file, result.ToBytes(), dictionary, names, null));
Console.WriteLine($"wrote {args[2]}");
return 0;
