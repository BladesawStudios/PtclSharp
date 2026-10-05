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
    public const double BeamRadiusScale = 0.5; // TotK's Drake beam uses 2.5; BotW's capsule radius was 0.1
    public const double BaseAttackPower = 30;   // the Drake beam's value (BotW's was 72 on a different scale)

    private readonly string _vanilla;
    private readonly string _out;
    private readonly ZsDictionaries _dictionaries;
    private readonly string _xlink;

    public GanonBeamMod(string vanillaRomfs, string outRomfs, string xlinkExe)
    {
        _vanilla = vanillaRomfs;
        _out = outRomfs;
        _xlink = xlinkExe;
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
        pack.Edit(pack.Find("Component/ELink/"), root => BymlEdit.SetString(root, "UserName", Actor));

        pack.Edit(pack.Find("Component/Blackboard/BlackboardParamTable/" + Actor), root =>
        {
            foreach (Byml entry in root.AsMap["BlackboardParamF32Array"].AsArray)
            {
                string key = entry.AsMap["BBKey"].AsString();
                if (key is "BeamosBeamRange" or "BeamosBeamRangeDefault") BymlEdit.SetNumber(entry, "InitVal", BeamRange);
                else if (key == "BeamRadiusScale") BymlEdit.SetNumber(entry, "InitVal", BeamRadiusScale);
            }
        });

        pack.Edit(pack.Find("GameBalance/AttackParam/" + Actor), root => BymlEdit.SetNumber(root, "BaseAttackPower", BaseAttackPower));

        // The donor is a fire beam; this one is not.
        pack.Edit(pack.Find("Component/ChemicalParam/" + Actor), root =>
        {
            Byml chemical = root.AsMap["Object"].AsArray[0];
            BymlEdit.SetBool(chemical, "InitialBurn", false);
            BymlEdit.SetBool(chemical, "IsSingleBurnEffect", false);
        });

        WriteOut(Path.Combine("Pack", "Actor", Actor + ".pack.zs"), _dictionaries.Compress(pack.ToSarc(), dict));
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
        if (fields.ContainsKey("ELinkUserName")) fields["ELinkUserName"] = Byml.From(Actor);
        if (fields.ContainsKey("ActorName")) fields["ActorName"] = Byml.From(Actor);

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
