using PtclSharp.Layout;

namespace PtclSharp.Tests;

/// <summary>
/// The Wii U (EFTB) files carry the same node tree and BotW layouts as the Switch files, big-endian. Runs only when
/// <c>PTCL_BOTW_WIIU_ROM</c> points at the Wii U content root; the pairing test also needs <c>PTCL_BOTW_ROM</c>.
/// </summary>
public class WiiUCorpusTests
{
    [BotwWiiUCorpusFact]
    public void EveryShippedWiiUFileLoadsAsBigEndian()
    {
        var failures = new List<string>();
        int count = 0;
        foreach (string file in Corpus.BotwWiiUFiles())
        {
            count++;
            try
            {
                PtclFile ptcl = Corpus.LoadBotw(file);
                if (!ptcl.Vfxb.BigEndian) failures.Add($"{Path.GetFileName(file)}: not read as EFTB");
            }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
        Assert.True(count > 100, $"Expected Effect files, found {count}.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [BotwWiiUCorpusFact]
    public void WiiUEmittersMatchTheirSwitchCounterparts()
    {
        if (Corpus.BotwRoot is null) return;
        var failures = new List<string>();
        int emitters = 0;
        foreach (string file in Corpus.BotwWiiUFiles())
        {
            string switchFile = Path.Combine(Corpus.BotwRoot, "Effect", Path.GetFileName(file));
            if (!File.Exists(switchFile)) continue;
            VfxbFile wiiu = Corpus.LoadBotw(file).Vfxb;
            VfxbFile nx = Corpus.LoadBotw(switchFile).Vfxb;
            var a = wiiu.EmitterSets.SelectMany(s => s.Emitters.Select(e => (s.Name, e))).ToList();
            var b = nx.EmitterSets.SelectMany(s => s.Emitters.Select(e => (s.Name, e))).ToList();
            if (!a.Select(x => (x.Name, x.e.Name, x.e.Depth)).SequenceEqual(b.Select(x => (x.Name, x.e.Name, x.e.Depth))))
            {
                failures.Add($"{Path.GetFileName(file)}: emitter sets or emitters differ");
                continue;
            }
            for (int i = 0; i < a.Count; i++)
            {
                emitters++;
                StructView wv = wiiu.EmitterView(a[i].e), sv = nx.EmitterView(b[i].e);
                foreach (string field in new[] { "color0_key_count", "scale_key_count" })
                    if (wv.GetUInt32(field) != sv.GetUInt32(field))
                        failures.Add($"{Path.GetFileName(file)} {a[i].e.Name}: {field} {wv.GetUInt32(field)} vs {sv.GetUInt32(field)}");
            }
        }
        Assert.True(emitters > 1000, $"Expected paired emitters, found {emitters}.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }
}
