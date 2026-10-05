// Re-skins a TotK effect: renames its emitter sets and shifts its colours. The input file is only read.
//
//   PtclTint <in.esetb.byml.zs> <ZsDic.pack.zs> <out.esetb.byml.zs> [--rename-set from=to ...] [--recolor <gMul>,<bMul>,<swap 0|1>]
//                            [--width <factor>] [--only-set <name> ...]
//                            [--botw <file.sesetlist> --keys <emitter>=<BotW emitter> ...]
//                            [--peak <emitter>=P] [--paint <emitter>=r,g,b] [--gain <emitter>=f] [--scale <emitter>=x,y,z] [--drop <emitter>]
//
// --recolor gMul,bMul,swap: for every colour (the colour key frames, the constant colours and the emitter colours) the new green is
//   gMul * old green and the new blue is bMul * old blue; with swap=1 the old green and blue trade places first. Fire orange
//   (1, 0.25, 0.04) becomes pink with `1,1.4,1`... see docs/porting/totk-line-beam-and-master-sword.md section 8.4.
// --width f: multiplies particle_scale x and z of every emitter by f (the beam's thickness; y is the length axis).
// --keys: copies the colour, alpha and scale animation of a BotW emitter (found by name in --botw's file) onto the emitter: the key counts, key
//   tables, colour modes, constants, the HDR scale (particle_color_rgb_scale) and the particle lifetime, since the keys are timed by it.
//   Applied before --paint / --gain / --scale, which can then adjust the result.
// --peak: rescales particle_color_rgb_scale so the emitter's brightest colour channel (over its colour keys, or its constant colour when
//   color0_mode is not 2) times the scale equals P. Colours far above ~30 clip to flat white, and the bloom then glows white, not in the hue.
// --paint: replaces the hue of one emitter's colours with r,g,b, keeping each colour's brightness (its largest channel).
// --gain: multiplies the emitter's particle_color_rgb_scale (HDR intensity: this is what feeds the bloom).
// --scale: multiplies the emitter's particle_scale_xyz by x,y,z. --drop: removes the emitter. (Emitter names are the input file's.)
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
var paints = new Dictionary<string, float[]>(StringComparer.Ordinal);
var gains = new Dictionary<string, float>(StringComparer.Ordinal);
var scales = new Dictionary<string, float[]>(StringComparer.Ordinal);
string? botwPath = null;
var keys = new Dictionary<string, string>(StringComparer.Ordinal);
var peaks = new Dictionary<string, float>(StringComparer.Ordinal);
var drops = new HashSet<string>(StringComparer.Ordinal);
float[] Floats(string text) => text.Split(',').Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
float gMul = 1, bMul = 1, width = 1;
bool swap = false, recolor = false;
for (int i = 3; i < args.Length; i++)
{
    if (args[i] == "--rename-set" && args[++i].Split('=', 2) is { Length: 2 } pair) renames[pair[0]] = pair[1];
    else if (args[i] == "--only-set") only.Add(args[++i]);
    else if (args[i] == "--paint" && args[++i].Split('=', 2) is { Length: 2 } a) paints[a[0]] = Floats(a[1]);
    else if (args[i] == "--gain" && args[++i].Split('=', 2) is { Length: 2 } b) gains[b[0]] = Floats(b[1])[0];
    else if (args[i] == "--scale" && args[++i].Split('=', 2) is { Length: 2 } c) scales[c[0]] = Floats(c[1]);
    else if (args[i] == "--botw") botwPath = args[++i];
    else if (args[i] == "--keys" && args[++i].Split('=', 2) is { Length: 2 } d) keys[d[0]] = d[1];
    else if (args[i] == "--peak" && args[++i].Split('=', 2) is { Length: 2 } pk) peaks[pk[0]] = Floats(pk[1])[0];
    else if (args[i] == "--drop") drops.Add(args[++i]);
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

static void Paint(StructView view, float[] hue, string field, int count, int stride)
{
    var v = new float[count];
    for (int k = 0; k < count; k++) v[k] = view.GetSingle(field, k);
    for (int k = 0; k + 2 < count; k += stride)
    {
        float peak = Math.Max(v[k], Math.Max(v[k + 1], v[k + 2]));
        for (int j = 0; j < 3; j++) v[k + j] = hue[j] * peak;
    }
    for (int k = 0; k < count; k++) view.SetSingle(field, v[k], k);
}

void Emitters(VfxbTreeNode node, Action<VfxbTreeNode> each)
{
    foreach (VfxbTreeNode child in node.Children)
    {
        if (child.Kind == "EMTR") each(child);
        Emitters(child, each);
    }
}

// BotW emitter data blocks by name, for --keys.
var botwEmitters = new Dictionary<string, byte[]>(StringComparer.Ordinal);
PtclLayoutSet botwLayouts = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);
if (botwPath is not null)
{
    PtclFile botw = PtclFile.ReadSesetlist(File.ReadAllBytes(botwPath));
    foreach (VfxbTreeNode set in VfxbDocument.From(botw.Vfxb).Sets)
        Emitters(set, e => botwEmitters[new StructView(botwLayouts.Emitter, e.Data!).GetString("emitter_name")] = (byte[])e.Data!.Clone());
}

static void CopyField(StructView from, StructView to, StructLayout fromLayout, StructLayout toLayout, string name)
{
    if (!fromLayout.TryGetField(name, out FieldDef a) || !toLayout.TryGetField(name, out FieldDef b) || a.Count != b.Count)
        throw new InvalidOperationException($"field {name} is not the same shape in both layouts");
    bool IsInt(FieldType t) => t is FieldType.U8 or FieldType.U32 or FieldType.I32;
    if (a.Type != b.Type && !(IsInt(a.Type) && IsInt(b.Type))) throw new InvalidOperationException($"field {name}: {a.Type} cannot become {b.Type}");
    for (int k = 0; k < a.Count; k++)
    {
        if (a.Type == FieldType.F32) { to.SetSingle(name, from.GetSingle(name, k), k); continue; }
        long value = a.Type switch { FieldType.U8 => from.GetByte(name, k), FieldType.U32 => from.GetUInt32(name, k), _ => from.GetInt32(name, k) };
        switch (b.Type)
        {
            case FieldType.U8: to.SetByte(name, (byte)Math.Clamp(value, 0, 255), k); break;
            case FieldType.U32: to.SetUInt32(name, (uint)Math.Max(0, value), k); break;
            default: to.SetInt32(name, (int)value, k); break;
        }
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

    foreach (VfxbTreeNode gone in set.Children.Where(c => c.Kind == "EMTR" && drops.Contains(new StructView(layouts.Emitter, c.Data!).GetString("emitter_name"))).ToList())
    {
        set.Children.Remove(gone);
        Console.WriteLine($"  dropped {new StructView(layouts.Emitter, gone.Data!).GetString("emitter_name")}");
    }

    Emitters(set, e =>
    {
        var view = new StructView(layouts.Emitter, e.Data!);
        string emitter = view.GetString("emitter_name");
        if (keys.TryGetValue(emitter, out string? botwName))
        {
            if (!botwEmitters.TryGetValue(botwName, out byte[]? botwData)) throw new InvalidOperationException($"--keys: BotW emitter {botwName} not found");
            var from = new StructView(botwLayouts.Emitter, botwData);
            foreach (string field in new[]
                     {
                         "color0_mode", "color0_key_count", "kf_color0", "color1_key_count", "kf_color1", "alpha0_key_count", "kf_alpha0",
                         "alpha1_key_count", "kf_alpha1", "scale_key_count", "kf_scale", "color0_const_rgb", "color1_const_rgb", "alpha0_const",
                         "alpha1_const", "particle_color_rgb_scale", "particle_lifespan", "particle_lifespan_random_percent"
                     })
                CopyField(from, view, botwLayouts.Emitter, layouts.Emitter, field);
            Console.WriteLine($"  {emitter}: animation copied from BotW {botwName}");
        }
        if (paints.TryGetValue(emitter, out float[]? hue))
        {
            Paint(view, hue, "kf_color0", 32, 4); Paint(view, hue, "kf_color1", 32, 4);
            foreach (string field in new[] { "color0_const_rgb", "color1_const_rgb", "emitter_color0_rgb", "emitter_color1_rgb" }) Paint(view, hue, field, 3, 3);
        }
        if (peaks.TryGetValue(emitter, out float peak))
        {
            float best = 0;
            if (view.GetByte("color0_mode") == 2)
            {
                int count = (int)view.GetUInt32("color0_key_count");
                for (int k = 0; k < count && k < 8; k++)
                    for (int j = 0; j < 3; j++) best = Math.Max(best, view.GetSingle("kf_color0", (k * 4) + j));
            }
            else for (int j = 0; j < 3; j++) best = Math.Max(best, view.GetSingle("color0_const_rgb", j));
            float current = best * view.GetSingle("particle_color_rgb_scale");
            if (current > 0) view.SetSingle("particle_color_rgb_scale", view.GetSingle("particle_color_rgb_scale") * peak / current);
            Console.WriteLine($"  {emitter}: peak {current:G5} -> {peak}");
        }
        if (gains.TryGetValue(emitter, out float gain)) view.SetSingle("particle_color_rgb_scale", view.GetSingle("particle_color_rgb_scale") * gain);
        if (scales.TryGetValue(emitter, out float[]? by))
            for (int k = 0; k < 3; k++) view.SetSingle("particle_scale_xyz", view.GetSingle("particle_scale_xyz", k) * by[k], k);
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
