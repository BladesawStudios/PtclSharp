using System.Diagnostics;
using BymlSharp;

namespace TotkModKit;

/// <summary>
/// Builds the "Dark Beast Ganon beam fired by the Master Sword" mod from copies of the vanilla files. The recipe is the one in
/// docs/porting/totk-line-beam-and-master-sword.md section 7. Nothing is written to the vanilla romfs; everything goes to the out folder.
/// </summary>
public sealed class GanonBeamMod
{
    // Names. The new actor is a copy of Drake_Beam_Small: a Toggle (continuous line) shootable that binds to its shooter.
    public const string Donor = "Drake_Beam_Small";
    public const string Actor = "GanonBeastBeam";
    public const string EffectFile = "GanonBeastBeam";
    public const string BodySet = "GanonBeast_Beam";
    public const string HitSet = "GanonBeast_BeamHit";

    // Gameplay values, from BotW unless noted. Range: ForkGanonBeastBeamShoot BeamRange of Enemy_GanonBeast. The rest are starting points.
    public const double BeamRange = 300.0;
    public const double BeamWidth = 2.5;        // BeamRadiusScaleDisplay: multiplies the width of every body emitter (the effect is fed (w, 1, w)); BotW fed 1.0 to emitters authored for it, our Drake-based body wants more
    public const int InstanceHeapSize = 56528;  // the donor has no AI; the Drake fire burst beam, which has one, needs this much
    public const double BeamRadiusScale = 0.500123; // 0.5 plus a marker: the exefs patch (scripts/make_exefs_patch.py) grows the range only for a controller with exactly this value // TotK's Drake beam uses 2.5; BotW's capsule radius was 0.1
    public const double BaseAttackPower = 20;   // per hit; with DamageInterval 5 (6 hits a second at 30 fps) that is ~120 a second. Vanilla per hit: sword beam 10, Gerudo beam 16, Drake 30 (hit every 30), Kohga 32

    private readonly string _vanilla;
    private readonly string _out;
    private readonly ZsDictionaries _dictionaries;
    private readonly string _xlink;
    private readonly string _python;

    /// <summary>How long a shot lasts, in 30 fps frames (the unit of the AI's VFR counter).</summary>
    public const int BeamFrames = 90;
    public const int DamageInterval = 5;        // frames between hits (the donor's is 30); the beam re-hits whatever it overlaps this often

    private readonly string _elinkUser;

    /// <param name="elinkUser">The ELink user the actor emits effects through. Defaults to the actor's own; naming a vanilla user (for example
    /// Drake_Beam_Small_Fire) is a diagnostic: the same actor then drives a known-good vanilla effect.</param>
    public GanonBeamMod(string vanillaRomfs, string outRomfs, string xlinkExe, string python = "python", string? elinkUser = null)
    {
        _elinkUser = elinkUser ?? Actor;
        _vanilla = vanillaRomfs;
        _out = outRomfs;
        _xlink = xlinkExe;
        _python = python;
        _dictionaries = ZsDictionaries.Load(vanillaRomfs);
        if (Path.GetFullPath(outRomfs).StartsWith(Path.GetFullPath(vanillaRomfs), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The output folder must not be inside the vanilla romfs.");
    }

    public void Build(string effectFile, string? texturesDir)
    {
        BuildActorPack();
        BuildSwordPack("Weapon_Sword_070");
        BuildSwordPack("Weapon_Sword_077");
        BuildRsdb("ActorInfo", row => row, cloneFrom: Donor);
        BuildRsdb("GameActorInfo", row => row, cloneFrom: Donor);
        BuildEffectFileInfo();
        BuildElink();
        CopyEffect(effectFile, texturesDir);
    }

    // --- actor pack ---------------------------------------------------------------------------------------------------------------

    private byte[] ReadVanillaPack(string actor, out uint dictionary) =>
        _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "Pack", "Actor", actor + ".pack.zs")), out dictionary);

    private void WriteOut(string relative, byte[] data)
    {
        string path = Path.Combine(_out, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
        Console.WriteLine($"  wrote {relative} ({data.Length:N0} bytes)");
    }

    private void BuildActorPack()
    {
        Console.WriteLine($"actor pack {Actor} (copy of {Donor})");
        var pack = new PackEdit(ReadVanillaPack(Donor, out uint dict));

        // Rename the files that carry the donor's name; references inside the pack follow.
        string R(string name) => name.Replace(Donor + "_Fire", Actor).Replace(Donor, Actor);
        foreach (string name in pack.Names.Where(n => n.Contains(Donor, StringComparison.Ordinal)).ToList())
        {
            // The SLink user file only names the sound user; the sound database has no user for the new actor, so it keeps the donor's.
            if (name.StartsWith("Component/SLink/", StringComparison.Ordinal)) continue;
            // The model bind file is data the donor owns, but renaming it buys nothing; it stays.
            pack.Rename(name, R(name));
        }

        string P(string folder, string file) => $"{folder}/{file}";
        pack.Edit(pack.Find("Component/ELink/"), root => BymlEdit.SetString(root, "UserName", _elinkUser));

        pack.Edit(pack.Find("Component/Blackboard/BlackboardParamTable/" + Actor), root =>
        {
            foreach (Byml entry in root.AsMap["BlackboardParamF32Array"].AsArray)
            {
                string key = entry.AsMap["BBKey"].AsString();
                if (key is "BeamosBeamRange" or "BeamosBeamRangeDefault") BymlEdit.SetNumber(entry, "InitVal", BeamRange);
                else if (key == "BeamRadiusScale") BymlEdit.SetNumber(entry, "InitVal", BeamRadiusScale);
                else if (key == "BeamRadiusScaleDisplay") BymlEdit.SetNumber(entry, "InitVal", BeamWidth);
            }
        });

        // The Toggle controller casts its ray along one local axis of the actor. The donor uses Y (a Drake's mouth bone); a shot actor
        // faces along its local Z, which is also BotW's BeamDir (0, 0, 1). With Y the beam fires straight up.
        pack.Edit(pack.Find("Component/ShootableParam/" + Actor), root =>
        {
            BymlEdit.SetString(root, "ToggleRayVector", "ShootableToggleRayZ");
            root.AsMap["DamageInterval"] = Byml.From(DamageInterval);
        });

        pack.Edit(pack.Find("GameBalance/AttackParam/" + Actor), root => BymlEdit.SetNumber(root, "BaseAttackPower", BaseAttackPower));

        // The donor is a fire beam (a burning, FireLv3 chemical material that ignites what it hits and spawns fire explosions); this one is
        // not. The other TotK beams (Kohga, Gerudo, the sword's PlayerBeam) use the empty NoChemicalShootable chemical param: copy that.
        const string NoChemical = "Component/ChemicalParam/NoChemicalShootable.game__component__ChemicalParam.bgyml";
        var kohga = new PackEdit(ReadVanillaPack("Kohga_Golem_Beam", out _));
        pack.Add(NoChemical, kohga.Get(NoChemical));
        pack.Edit($"Actor/{Actor}.engine__actor__ActorParam.bgyml", root => root.AsMap["Components"].AsMap["ChemicalRef"] = Byml.From("?" + NoChemical));
        foreach (string unused in pack.Names.Where(n => n.StartsWith("Chemical/") || n == pack.Find("Component/ChemicalParam/" + Actor)).ToList())
            pack.Remove(unused);

        AddLifetimeAi(pack);

        WriteOut(Path.Combine("Pack", "Actor", Actor + ".pack.zs"), _dictionaries.Compress(pack.ToSarc(), dict));
    }

    /// <summary>
    /// A Toggle shootable lives until its shooter puts it to sleep, and nothing on the sword's side ever does (the vanilla Master Sword
    /// beam is a projectile that ends itself). So the actor gets an AI of its own: a frame counter that, past <see cref="BeamFrames"/>,
    /// runs OneShotShootableRequestSleep. The AINB is a trimmed copy of the Drake fire burst beam's (scripts/make_beam_ainb.py).
    /// </summary>
    private void AddLifetimeAi(PackEdit pack)
    {
        const string AiDonor = "Drake_Burst_Beam_Small_Fire";
        var donor = new PackEdit(ReadVanillaPack(AiDonor, out _));
        string work = Path.Combine(Path.GetTempPath(), "totkmodkit_ainb");
        Directory.CreateDirectory(work);
        string source = Path.Combine(work, AiDonor + ".root.ainb");
        string built = Path.Combine(work, Actor + ".root.ainb");
        File.WriteAllBytes(source, donor.Get($"AI/{AiDonor}.root.ainb"));

        string script = Path.Combine(AppContext.BaseDirectory, "scripts", "make_beam_ainb.py");
        var start = new ProcessStartInfo(_python, $"\"{script}\" \"{source}\" \"{built}\" {BeamFrames} {Actor}.root")
        { RedirectStandardOutput = true, RedirectStandardError = true };
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("make_beam_ainb.py failed:\n" + output);

        pack.Add($"AI/{Actor}.root.ainb", File.ReadAllBytes(built));
        string info = $"AI/AIInfo/{Actor}.engine__actor__AIInfo.bgyml";
        pack.Add(info, donor.Get($"AI/AIInfo/{AiDonor}.engine__actor__AIInfo.bgyml"));
        pack.Edit(info, root => BymlEdit.SetString(root, "RootAIRef", $"Work/AI/Root/{Actor}.root.ain"));
        pack.Edit($"Actor/{Actor}.engine__actor__ActorParam.bgyml", root =>
            root.AsMap["Components"].AsMap["AIInfoRef"] = Byml.From("?" + info));
    }

    // --- Master Sword -------------------------------------------------------------------------------------------------------------

    private void BuildSwordPack(string sword)
    {
        Console.WriteLine($"sword pack {sword}");
        var pack = new PackEdit(ReadVanillaPack(sword, out uint dict));

        string shooter = pack.Find("Component/ShooterParam/");
        string renamed = shooter.Replace("Weapon_Sword_070", "Weapon_Sword_070_" + Actor);
        pack.Rename(shooter, renamed);
        pack.Edit(renamed, root =>
        {
            Byml setting = root.AsMap["ShootableActorSettings"].AsArray[0];
            BymlEdit.SetString(setting, "Actor", $"Work/Actor/{Actor}.engine__actor__ActorParam.gyml");
            BymlEdit.SetNumber(setting, "MaxActors", 1);
        });

        // The rod code finds the shootable it fires through the RodParam's ShootableName, which must name the same actor as the
        // ShooterParam creates. Left at PlayerBeam, nothing matches and every shot fails (the "shoot failed" sound).
        string rod = "GameParameter/RodParam/Weapon_Sword_070.game__Object__RodParam.bgyml";
        string renamedRod = rod.Replace("Weapon_Sword_070", "Weapon_Sword_070_" + Actor);
        pack.Rename(rod, renamedRod);
        pack.Edit(renamedRod, root =>
            BymlEdit.SetString(root.AsMap["RodLevelParamLv1"], "ShootableName", $"Work/Actor/{Actor}.engine__actor__ActorParam.gyml"));

        WriteOut(Path.Combine("Pack", "Actor", sword + ".pack.zs"), _dictionaries.Compress(pack.ToSarc(), dict));
    }

    // --- RSDB ---------------------------------------------------------------------------------------------------------------------

    private void BuildRsdb(string table, Func<Byml, Byml> unused, string cloneFrom)
    {
        Console.WriteLine($"RSDB {table}: row {Actor} copied from {cloneFrom}");
        string file = $"{table}.Product.121.rstbl.byml.zs";
        byte[] data = _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "RSDB", file)), out uint dict);
        BymlFile bymlFile = BymlFile.FromBinary(data);
        IList<Byml> rows = bymlFile.Root.AsArray;

        if (rows.Any(r => r.AsMap["__RowId"].AsString() == Actor)) throw new InvalidOperationException($"{table} already has a row named {Actor}.");
        Byml source = BymlFile.FromBinary(data).Root.AsArray.First(r => r.AsMap["__RowId"].AsString() == cloneFrom); // an independent copy
        IDictionary<string, Byml> fields = source.AsMap;
        fields["__RowId"] = Byml.From(Actor);
        if (fields.ContainsKey("ELinkUserName")) fields["ELinkUserName"] = Byml.From(_elinkUser);
        if (fields.ContainsKey("ActorName")) fields["ActorName"] = Byml.From(Actor);
        if (fields.ContainsKey("InstanceHeapSize")) BymlEdit.SetNumber(source, "InstanceHeapSize", InstanceHeapSize);

        // Keep the table sorted the way it ships.
        int at = 0;
        while (at < rows.Count && string.CompareOrdinal(rows[at].AsMap["__RowId"].AsString(), Actor) < 0) at++;
        rows.Insert(at, source);
        WriteOut(Path.Combine("RSDB", file), _dictionaries.Compress(bymlFile.Write(), dict));
    }

    // --- Effect registry and effect files -----------------------------------------------------------------------------------------

    private void BuildEffectFileInfo()
    {
        Console.WriteLine("EffectFileInfo: register the effect file");
        string file = "EffectFileInfo.Product.110.Nin_NX_NVN.byml.zs";
        byte[] data = _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "Effect", file)), out uint dict);
        BymlFile bymlFile = BymlFile.FromBinary(data);

        bymlFile.Root.AsMap["BinaryDict"].AsMap[EffectFile] = Byml.From(EffectFile);
        IList<Byml> list = bymlFile.Root.AsMap["EsetbList"].AsArray;
        if (list.Any(e => e.AsString() == EffectFile)) throw new InvalidOperationException("EsetbList already lists the effect file.");
        int at = 0;
        while (at < list.Count && string.CompareOrdinal(list[at].AsString(), EffectFile) < 0) at++;
        list.Insert(at, Byml.From(EffectFile));
        WriteOut(Path.Combine("Effect", file), _dictionaries.Compress(bymlFile.Write(), dict));
    }

    private void CopyEffect(string effectFile, string? texturesDir)
    {
        WriteOut(Path.Combine("Effect", EffectFile + ".Nin_NX_NVN.esetb.byml.zs"), File.ReadAllBytes(effectFile));
        if (texturesDir is null) return;
        foreach (string txtg in Directory.EnumerateFiles(texturesDir, "*.txtg"))
            WriteOut(Path.Combine("TexToGo", Path.GetFileName(txtg)), File.ReadAllBytes(txtg));
    }

    // --- ELink2 ---------------------------------------------------------------------------------------------------------------------

    private void BuildElink()
    {
        Console.WriteLine("ELink2: add the GanonBeastBeam user");
        string file = "elink2.Product.110.belnk.zs";
        byte[] bin = _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "ELink2", file)), out uint dict);

        string work = Path.Combine(Path.GetTempPath(), "totkmodkit-elink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string binPath = Path.Combine(work, "elink.bin"), textPath = Path.Combine(work, "elink.txt"), newBin = Path.Combine(work, "new.bin");
            File.WriteAllBytes(binPath, bin);
            RunXlink(binPath, textPath);

            string text = File.ReadAllText(textPath);
            string donorUser = Donor + "_Fire";
            int start = text.IndexOf($"\n  {donorUser} {{\n", StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException($"ELink2 user {donorUser} not found.");
            start++;
            int end = text.IndexOf("\n  }\n", start, StringComparison.Ordinal) + "\n  }\n".Length;
            string block = text[start..end];

            block = block.Replace($"  {donorUser} {{", $"  {Actor} {{", StringComparison.Ordinal);
            block = RemoveBlock(block, "    ActionSlots {");
            block = RemoveBlock(block, "      Shoot[");
            block = RemoveBlock(block, "      Chemical_Fire[");
            block = SetAsset(block, "Tail", BodySet);
            block = SetAsset(block, "Beam_Top", HitSet);
            block = block.Replace("          Scale = 1.875\n", "", StringComparison.Ordinal); // the donor's hit scale; BotW's Beam_Top has none

            File.WriteAllText(textPath, text.Insert(end, block));
            RunXlink(textPath, newBin);
            WriteOut(Path.Combine("ELink2", file), _dictionaries.Compress(File.ReadAllBytes(newBin), dict));
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    /// <summary>Removes the brace block that starts at the first line beginning with <paramref name="opening"/> (its closing line has the same indent).</summary>
    private static string RemoveBlock(string text, string opening)
    {
        int start = text.IndexOf("\n" + opening, StringComparison.Ordinal);
        if (start < 0) return text;
        start++;
        string indent = new(' ', opening.Length - opening.TrimStart().Length);
        int end = text.IndexOf("\n" + indent + "}\n", start, StringComparison.Ordinal) + ("\n" + indent + "}\n").Length;
        return text.Remove(start, end - start);
    }

    private static string SetAsset(string block, string table, string runtimeAsset)
    {
        int at = block.IndexOf($"      {table}[", StringComparison.Ordinal);
        int line = block.IndexOf("RuntimeAssetName = ", at, StringComparison.Ordinal);
        int lineEnd = block.IndexOf('\n', line);
        return block[..line] + $"RuntimeAssetName = \"{runtimeAsset}\"" + block[lineEnd..];
    }

    private void RunXlink(string input, string output)
    {
        var info = new ProcessStartInfo(_xlink, ["-i", input, "-o", output]) { RedirectStandardOutput = true, RedirectStandardError = true };
        using Process p = Process.Start(info)!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"xlink failed: {p.StandardError.ReadToEnd()}");
    }
}
