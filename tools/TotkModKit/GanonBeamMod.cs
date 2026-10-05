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
    public const double BeamRadiusScale = 0.5;  // the damage capsule radius multiplier (the donor uses 2.5; BotW's capsule radius was 0.1)
    public const double BaseAttackPower = 40;   // per hit; with DamageInterval 5 (6 hits a second at 30 fps) that is ~240 a second. Vanilla per hit: sword beam 10, Gerudo beam 16, Drake 30 (hit every 30), Kohga 32

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

    // The fire field: a standalone, destructive fire/explosion actor, cloned from the Drake beam's own ExpandFireField_Drake (a chemical
    // field that ignites and damages what is inside it) with its effect file cloned and tinted pink. Nothing spawns it yet; it is a
    // separate actor so it can be shot, spawned by AI or placed like any vanilla one.
    public const string FireDonor = "ExpandFireField_Drake";
    public const string FireActor = "GanonBeastFireField";
    public const string FireEffectFile = "GanonBeastFireField";
    public const string FireSet = "GanonBeast_FireField";

    public void Build(string effectFile, string? texturesDir, string? fireEffectFile = null)
    {
        BuildActorPack();
        BuildSwordPack("Weapon_Sword_070");
        BuildSwordPack("Weapon_Sword_077");
        if (fireEffectFile is not null) BuildFireFieldPack();
        foreach (string table in new[] { "ActorInfo", "GameActorInfo" })
        {
            var rows = new List<(string Actor, string From, string ELinkUser, int? Heap)> { (Actor, Donor, _elinkUser, InstanceHeapSize) };
            if (fireEffectFile is not null) rows.Add((FireActor, FireDonor, FireActor, null));
            BuildRsdb(table, rows);
        }
        BuildEffectFileInfo(fireEffectFile is null ? [EffectFile] : [EffectFile, FireEffectFile]);
        BuildElink(fireEffectFile is not null);
        CopyEffect(EffectFile, effectFile, texturesDir);
        if (fireEffectFile is not null) CopyEffect(FireEffectFile, fireEffectFile, null);
    }

    private void BuildFireFieldPack()
    {
        Console.WriteLine($"actor pack {FireActor} (copy of {FireDonor})");
        var pack = new PackEdit(ReadVanillaPack(FireDonor, out uint dict));
        foreach (string name in pack.Names.Where(n => n.Contains(FireDonor, StringComparison.Ordinal)).ToList())
        {
            if (name.StartsWith("Component/SLink/", StringComparison.Ordinal)) continue; // the sound database has no user for the new actor
            pack.Rename(name, name.Replace(FireDonor, FireActor));
        }
        pack.Edit(pack.Find("Component/ELink/"), root => BymlEdit.SetString(root, "UserName", FireActor));
        WriteOut(Path.Combine("Pack", "Actor", FireActor + ".pack.zs"), _dictionaries.Compress(pack.ToSarc(), dict));
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

    private void BuildRsdb(string table, IReadOnlyList<(string Actor, string From, string ELinkUser, int? Heap)> news)
    {
        string file = $"{table}.Product.121.rstbl.byml.zs";
        byte[] data = _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "RSDB", file)), out uint dict);
        BymlFile bymlFile = BymlFile.FromBinary(data);
        IList<Byml> rows = bymlFile.Root.AsArray;

        foreach ((string actor, string cloneFrom, string elinkUser, int? heap) in news)
        {
            Console.WriteLine($"RSDB {table}: row {actor} copied from {cloneFrom}");
            if (rows.Any(r => r.AsMap["__RowId"].AsString() == actor)) throw new InvalidOperationException($"{table} already has a row named {actor}.");
            Byml source = BymlFile.FromBinary(data).Root.AsArray.First(r => r.AsMap["__RowId"].AsString() == cloneFrom); // an independent copy
            IDictionary<string, Byml> fields = source.AsMap;
            fields["__RowId"] = Byml.From(actor);
            if (fields.ContainsKey("ELinkUserName")) fields["ELinkUserName"] = Byml.From(elinkUser);
            if (fields.ContainsKey("ActorName")) fields["ActorName"] = Byml.From(actor);
            if (heap is int size && fields.ContainsKey("InstanceHeapSize")) BymlEdit.SetNumber(source, "InstanceHeapSize", size);

            // Keep the table sorted the way it ships.
            int at = 0;
            while (at < rows.Count && string.CompareOrdinal(rows[at].AsMap["__RowId"].AsString(), actor) < 0) at++;
            rows.Insert(at, source);
        }
        WriteOut(Path.Combine("RSDB", file), _dictionaries.Compress(bymlFile.Write(), dict));
    }

    // --- Effect registry and effect files -----------------------------------------------------------------------------------------

    private void BuildEffectFileInfo(IReadOnlyList<string> effectFiles)
    {
        Console.WriteLine("EffectFileInfo: register the effect files");
        string file = "EffectFileInfo.Product.110.Nin_NX_NVN.byml.zs";
        byte[] data = _dictionaries.Decompress(File.ReadAllBytes(Path.Combine(_vanilla, "Effect", file)), out uint dict);
        BymlFile bymlFile = BymlFile.FromBinary(data);

        foreach (string name in effectFiles)
        {
            bymlFile.Root.AsMap["BinaryDict"].AsMap[name] = Byml.From(name);
            IList<Byml> list = bymlFile.Root.AsMap["EsetbList"].AsArray;
            if (list.Any(e => e.AsString() == name)) throw new InvalidOperationException($"EsetbList already lists {name}.");
            int at = 0;
            while (at < list.Count && string.CompareOrdinal(list[at].AsString(), name) < 0) at++;
            list.Insert(at, Byml.From(name));
        }
        WriteOut(Path.Combine("Effect", file), _dictionaries.Compress(bymlFile.Write(), dict));
    }

    private void CopyEffect(string name, string effectFile, string? texturesDir)
    {
        WriteOut(Path.Combine("Effect", name + ".Nin_NX_NVN.esetb.byml.zs"), File.ReadAllBytes(effectFile));
        if (texturesDir is null) return;
        foreach (string txtg in Directory.EnumerateFiles(texturesDir, "*.txtg"))
            WriteOut(Path.Combine("TexToGo", Path.GetFileName(txtg)), File.ReadAllBytes(txtg));
    }

    // --- ELink2 ---------------------------------------------------------------------------------------------------------------------

    private void BuildElink(bool withFireField)
    {
        Console.WriteLine("ELink2: add the GanonBeastBeam user" + (withFireField ? " and the GanonBeastFireField user" : ""));
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

            text = text.Insert(end, block);

            if (withFireField)
            {
                // The fire field's user: a copy of the donor's with its fire effect pointed at the cloned (pink) set.
                int fs = text.IndexOf($"\n  {FireDonor} {{\n", StringComparison.Ordinal);
                if (fs < 0) throw new InvalidOperationException($"ELink2 user {FireDonor} not found.");
                fs++;
                int fe = text.IndexOf("\n  }\n", fs, StringComparison.Ordinal) + "\n  }\n".Length;
                string fire = text[fs..fe].Replace($"  {FireDonor} {{", $"  {FireActor} {{", StringComparison.Ordinal);
                fire = SetAsset(fire, "Chemical_Fire", FireSet);
                text = text.Insert(fe, fire);
            }

            File.WriteAllText(textPath, text);
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
