using System.Globalization;
using PtclViewer;

// PtclViewer [--rom <BotW ROM root>] [--effect <name>] [--set <name|index>]
//            [--shot <out.png> [--frames N | --at 10,30,60] [--size WxH] [--cam yaw,pitch,dist[,tx,ty,tz]] [--exposure X]]
//
// With no --shot it opens the interactive viewer. With --shot it simulates the chosen emitter set for N effect frames, renders
// one image (or one per --at frame, named <out>_<frame>.png) and exits.

var options = new ViewerOptions();
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--rom": options.RomRoot = Next(); break;
        case "--effect": options.Effect = Next(); break;
        case "--set": options.Set = Next(); break;
        case "--shot": options.ShotPath = Next(); break;
        case "--frames": options.ShotFrames = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--at": options.ShotAtFrames = Next().Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray(); break;
        case "--size":
            string[] wh = Next().Split('x');
            options.Width = int.Parse(wh[0]); options.Height = int.Parse(wh[1]);
            break;
        case "--cam": options.Camera = Next().Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray(); break;
        case "--exposure": options.Exposure = float.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--survey": options.Survey = Next().Split(','); break;
        case "--max-sets": options.SurveyMaxSets = int.Parse(Next()); break;
        case "--no-frame": options.AutoFrame = false; break;
        case "--probe": options.Probe = Next().Split(',').Select(int.Parse).ToArray(); break;
        case "--color": { var c = Next().Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray(); options.SetColor = new System.Numerics.Vector4(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f); break; }
        case "--no-bloom": options.Bloom = false; break;
        case "--bloom": { var b = Next().Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray(); options.Bloom = true; options.BloomIntensity = b[0]; if (b.Length > 1) options.BloomThreshold = b[1]; break; }
        case "--orbit": { var o = Next().Split(','); options.OrbitRadius = float.Parse(o[0], CultureInfo.InvariantCulture); if (o.Length > 1) options.OrbitSpeed = float.Parse(o[1], CultureInfo.InvariantCulture); break; }
        case "--only": options.Only = Next().Split(','); break;
        case "--fps": options.Fps = float.Parse(Next(), CultureInfo.InvariantCulture); break;
        default:
            Console.Error.WriteLine($"unknown argument {args[i]}");
            return 1;
    }
}
return new ViewerApp(options).Run();
