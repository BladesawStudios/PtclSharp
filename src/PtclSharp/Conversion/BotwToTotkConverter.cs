using PtclSharp.Layout;
using PtclSharp.Writer;

namespace PtclSharp.Conversion;

/// <summary>
/// Converts the emitter-set tree of a BotW effect to TotK's layouts. Only fields whose meaning is proven (or paired at the
/// same position) move across; everything else is reported. The result needs a TotK tail (shaders, primitives) from a donor
/// file, because those are not translatable.
/// </summary>
public static class BotwToTotkConverter
{
    private static readonly PtclLayoutSet Botw = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);
    private static readonly PtclLayoutSet Totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

    /// <summary>BotW field name to TotK field name for fields that are the same thing under a different name or width.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["loop_color0_period_i32"] = "loop_color0_period_u16",
        ["loop_alpha0_period_i32"] = "loop_alpha0_period_u16",
        ["loop_color1_period_i32"] = "loop_color1_period_u16",
        ["loop_alpha1_period_i32"] = "loop_alpha1_period_u16",
        // Both games write this byte straight into the first byte of the depth-stencil state (BotW names it by what it is).
        ["depth_compare_func"] = "depth_stencil_mode_index",
        ["history_vec_init_speed"] = "unverified_3C"
    };

    /// <summary>
    /// GPU-only floats of unproven purpose that sit at the same place in the shader-visible block, are read with the same
    /// arithmetic and have the same value distribution in both games.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> UnverifiedAnalogs = new Dictionary<string, string>
    {
        ["unverified_5C0"] = "unverified_890",
        ["unverified_5C4"] = "unverified_894",
        ["unverified_5C8"] = "unverified_898",
        ["unverified_5CC"] = "unverified_89C",
        ["unverified_5D0"] = "unverified_8A0",
        ["unverified_5D4"] = "unverified_8A4",
        ["unverified_5D8"] = "unverified_8A8",
        ["unverified_5DC"] = "unverified_8AC",
        ["unverified_5E0"] = "unverified_8B0",
        ["unverified_5F0"] = "unverified_8C0",
        ["unverified_5F4"] = "unverified_8C4",
        ["unverified_100"] = "unverified_130",
        ["unverified_104"] = "unverified_134",
        ["unverified_740"] = "unverified_C50",
        ["unverified_744"] = "unverified_C54"
    };

    /// <summary>
    /// The TotK emitter field that receives a BotW emitter field in conversion (same name, a known alias, or one of the
    /// <see cref="UnverifiedAnalogs"/> when <paramref name="includeUnverifiedAnalogs"/> is set), or null when TotK has no counterpart.
    /// </summary>
    public static string? TotkFieldFor(string botwField, bool includeUnverifiedAnalogs = true)
    {
        if (Aliases.TryGetValue(botwField, out string? alias)) return alias;
        if (includeUnverifiedAnalogs && UnverifiedAnalogs.TryGetValue(botwField, out string? analog)) return analog;
        return Totk.Emitter.TryGetField(botwField, out FieldDef? to) && to.Status != FieldStatus.Unused ? to.Name : null;
    }

    /// <summary>Fields the converter sets itself (shader, callbacks) and must not copy from BotW.</summary>
    private static readonly HashSet<string> Handled =
    [
        "shader_idx_normal", "shader_idx_pass1", "shader_idx_pass2", "compute_shader0", "custom_shader_index", "custom_action_index",
        "tex_slot0_guid", "tex_slot1_guid", "tex_slot2_guid", "emitter_name"
    ];

    private static readonly string[] ChunkOrder =
    [
        "CSDP", "FCLN", "FSPN", "FRN1", "FRND", "FMAG", "FCOV", "FCOL", "FPAD", "FCSF",
        "EASL", "EADV", "EAA0", "EAA1", "EAC0", "EAC1", "EAES", "EAER", "EAET", "EATR", "EAPL", "EAOV", "EASS", "EAGV",
        "EP01", "EP02", "EP03", "EP04"
    ];

    /// <summary>Converts every set under the BotW document's <c>ESTA</c> into TotK-form tree nodes.</summary>
    public static List<VfxbTreeNode> ConvertSets(VfxbDocument botw, ConverterOptions options, ConversionReport report)
    {
        var sets = new List<VfxbTreeNode>();
        foreach (VfxbTreeNode set in botw.Sets)
            sets.Add(ConvertSet(set, options, report));
        return sets;
    }

    /// <summary>Builds a TotK document: the converted sets in front of the donor's header and tail.</summary>
    public static VfxbDocument ToTotk(VfxbDocument botw, VfxbDocument totkDonor, ConverterOptions options, out ConversionReport report)
    {
        report = new ConversionReport();
        List<VfxbTreeNode> sets = ConvertSets(botw, options, report);
        VfxbDocument result = totkDonor.WithSets(sets);
        result.RecountChildren();
        bool carriesGeometry = options.CarriedModelIds is not null || options.CarriedMeshIds is not null;
        report.Add(ConversionSeverity.Warning, "file", carriesGeometry
            ? "The shader archive comes from the donor file, not from the BotW file; emitters that use a BotW custom shader need a donor for it separately. Models and mesh primitives are the caller's (see ConverterOptions)."
            : "Primitives (PRMA), G3D models and shader archives come from the donor file, not from the BotW file; emitters that use a BotW mesh or custom shader need those supplied separately.");
        return result;
    }

    public static VfxbTreeNode ConvertSet(VfxbTreeNode botwSet, ConverterOptions options, ConversionReport report)
    {
        byte[] botwData = botwSet.Data ?? throw new InvalidDataException("An ESET node has no data block.");
        string name = new StructView(Botw.EmitterSet, (byte[])botwData.Clone()).GetString("name");

        var set = new VfxbTreeNode("ESET") { Data = new byte[Totk.EmitterSet.Size] };
        botwData.AsSpan(0x10, 0x40).CopyTo(set.Data.AsSpan(0x10, 0x40));
        report.SetsConverted++;

        foreach (VfxbTreeNode emitter in botwSet.Children.Where(c => c.Kind == "EMTR"))
            set.Children.Add(ConvertEmitter(emitter, options, report, $"{name}"));
        return set;
    }

    private static VfxbTreeNode ConvertEmitter(VfxbTreeNode botwEmitter, ConverterOptions options, ConversionReport report, string setName)
    {
        byte[] src = botwEmitter.Data ?? throw new InvalidDataException("An EMTR node has no data block.");
        var srcView = new StructView(Botw.Emitter, (byte[])src.Clone());
        string scope = $"{setName}/{srcView.GetString("emitter_name")}";

        byte[] dst = TotkDefaults.EmitterData();
        src.AsSpan(0x10, 0x40).CopyTo(dst.AsSpan(0x10, 0x40));
        var dstView = new StructView(Totk.Emitter, dst);

        // 1. Same-name fields that are proven (or paired) in BotW and exist in TotK.
        foreach (FieldDef f in Botw.Emitter.Fields)
        {
            if (f.Status is FieldStatus.Unused or FieldStatus.Unverified) continue;
            if (Handled.Contains(f.Name)) continue;
            string target = Aliases.TryGetValue(f.Name, out string? alias) ? alias : f.Name;
            if (!Totk.Emitter.TryGetField(target, out FieldDef? to) || to.Status == FieldStatus.Unused)
            {
                if (HasNonZero(src, f))
                    report.Add(ConversionSeverity.Warning, scope, $"BotW field '{f.Name}' has no TotK counterpart and was dropped.");
                continue;
            }
            Apply(f, src, to, dst, scope, report);
        }

        // 2. Unproven GPU-only fields that line up with a TotK field.
        if (options.CopyUnverifiedAnalogs)
        {
            foreach ((string from, string to) in UnverifiedAnalogs)
                Apply(Botw.Emitter[from], src, Totk.Emitter[to], dst, scope, report, quiet: true);
        }

        // 3. Things the converter decides.
        ApplyTextures(srcView, dstView, options, scope, report);
        ApplyShader(src, dstView, options, scope, report, out byte[]? customShaderParams);
        CheckMeshes(srcView, options, scope, report);
        if (srcView.GetByte("depth_test_enable") != 1 || srcView.GetByte("depth_write_enable") != 0)
            report.Add(ConversionSeverity.Info, scope, "BotW depth test/write flags (depth_test_enable, depth_write_enable) have no TotK per-emitter field; TotK defaults apply.");
        if (srcView.GetByte("emitter_calc_type") == 2)
            report.Add(ConversionSeverity.Warning, scope, "GPU-compute emitter: needs TotK compute shaders (compute_shader0..2) from the shader binding.");

        var node = new VfxbTreeNode("EMTR") { Data = dst };
        node.Attributes.AddRange(ConvertChunks(botwEmitter, scope, customShaderParams, report));
        foreach (VfxbTreeNode child in botwEmitter.Children.Where(c => c.Kind == "EMTR"))
            node.Children.Add(ConvertEmitter(child, options, report, scope));
        report.EmittersConverted++;
        return node;
    }

    private static void Apply(FieldDef from, byte[] src, FieldDef to, byte[] dst, string scope, ConversionReport report, bool quiet = false)
    {
        FieldCopier.Outcome outcome = FieldCopier.Copy(from, src, to, dst);
        switch (outcome)
        {
            case FieldCopier.Outcome.Copied:
                report.FieldsCopied++;
                break;
            case FieldCopier.Outcome.Clamped:
                report.FieldsCopied++;
                report.Add(ConversionSeverity.Warning, scope, $"'{from.Name}' does not fit TotK '{to.Name}' ({to.Type}) and was clamped.");
                break;
            default:
                report.Add(ConversionSeverity.Warning, scope, $"'{from.Name}' ({from.Type}[{from.Count}]) is not shaped like TotK '{to.Name}' ({to.Type}[{to.Count}]); skipped.");
                break;
        }
        if (!quiet && from.Status == FieldStatus.Paired && !Totk.Emitter[to.Name].IsConfirmed)
            report.Add(ConversionSeverity.Info, scope, $"'{from.Name}' was carried on position only (not proven in both games).");
    }

    private static bool HasNonZero(byte[] data, FieldDef f) => data.AsSpan(f.Offset, f.ByteLength).ContainsAnyExcept((byte)0);

    private static void ApplyTextures(StructView src, StructView dst, ConverterOptions options, string scope, ConversionReport report)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            string name = $"tex_slot{slot}_guid";
            ulong guid = src.GetUInt64(name);
            if (guid != ulong.MaxValue)
            {
                ulong? mapped = options.TextureMap?.Invoke(guid);
                if (mapped is null)
                    report.Add(ConversionSeverity.Warning, scope, $"Texture GUID of slot {slot} was kept as is; TotK needs the GUID of an equivalent texture (TextureMap).");
                guid = mapped ?? guid;
            }
            dst.SetUInt64(name, guid);
        }
        report.Add(ConversionSeverity.Info, scope, "BotW sampler selection bytes are not mapped (TotK keeps its default sampler record).");
    }

    private static void ApplyShader(byte[] botwData, StructView dst, ConverterOptions options, string scope, ConversionReport report, out byte[]? customShaderParams)
    {
        customShaderParams = null;
        ShaderBinding? binding = options.ShaderBinder?.Invoke(botwData);
        if (binding is null)
        {
            report.Add(ConversionSeverity.Error, scope, "No shader binding: the BotW shader indices refer to the BotW archive and are meaningless in TotK.");
            foreach (string name in new[] { "shader_idx_normal", "shader_idx_pass1", "shader_idx_pass2", "compute_shader0" })
            {
                FieldDef from = Botw.Emitter[name];
                FieldCopier.Copy(from, botwData, Totk.Emitter[name], dst.Data);
            }
            return;
        }
        foreach ((string name, long value) in binding.Fields)
        {
            FieldDef f = Totk.Emitter[name];
            Span<byte> slot = dst.Data.Slice(f.Offset, f.ByteLength);
            switch (f.Type)
            {
                case FieldType.U8: slot[0] = (byte)value; break;
                case FieldType.I32: System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(slot, (int)value); break;
                case FieldType.U32: System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(slot, (uint)value); break;
                default: throw new InvalidOperationException($"Unsupported shader field type {f.Type} for '{name}'.");
            }
        }
        customShaderParams = binding.CustomShaderParams;
    }

    private static void CheckMeshes(StructView src, ConverterOptions options, string scope, ConversionReport report)
    {
        ulong mesh = src.GetUInt64("mesh_primitive_idx");
        if (mesh != ulong.MaxValue && options.CarriedMeshIds?.Contains(mesh) != true)
            report.Add(ConversionSeverity.Error, scope, "Uses a BotW mesh primitive (mesh_primitive_idx); the primitive is not carried over.");
        ulong model = src.GetUInt64("g3d_primitive_idx");
        if (model != ulong.MaxValue && options.CarriedModelIds?.Contains(model) != true)
            report.Add(ConversionSeverity.Error, scope, "Uses a BotW G3D model primitive (g3d_primitive_idx); the model is not carried over.");
        if (src.GetByte("is_trimming_prim") != 0 && src.GetUInt64("trim_primitive_idx") != ulong.MaxValue)
            report.Add(ConversionSeverity.Error, scope, "Uses a BotW trimming primitive; it is not carried over.");
    }

    private static IEnumerable<VfxbTreeNode> ConvertChunks(VfxbTreeNode botwEmitter, string scope, byte[]? customShaderParams, ConversionReport report)
    {
        var converted = new List<VfxbTreeNode>();
        foreach (VfxbTreeNode chunk in botwEmitter.Attributes)
        {
            VfxbTreeNode? result = ConvertChunk(chunk, scope, report);
            if (result is not null) converted.Add(result);
        }
        if (customShaderParams is not null)
            converted.Add(new VfxbTreeNode("CSDP") { Data = customShaderParams });
        return converted.OrderBy(c => Array.IndexOf(ChunkOrder, c.Kind) is var i && i < 0 ? int.MaxValue : i);
    }

    private static VfxbTreeNode? ConvertChunk(VfxbTreeNode chunk, string scope, ConversionReport report)
    {
        string kind = chunk.Kind;
        if (kind is "CSDP" or "CADP" or "CUDP")
        {
            report.ChunksDropped++;
            report.Add(ConversionSeverity.Warning, scope, $"{kind} (BotW custom shader/action data) is shader specific and was dropped.");
            return null;
        }
        if (!Botw.Chunks.TryGetValue(kind, out ChunkLayout? from) || !Totk.Chunks.TryGetValue(kind, out ChunkLayout? to))
        {
            report.ChunksDropped++;
            report.Add(ConversionSeverity.Warning, scope, $"Chunk {kind} is not mapped in both games and was dropped.");
            return null;
        }

        byte[] source = chunk.Data ?? [];
        int size = to.Repeating is { } rep ? rep.PayloadSize(KeyCount(from, source)) : to.Size;
        byte[] payload = new byte[size];

        if (from.Repeating is not null)
        {
            // Emitter animation lanes: same header and keys; BotW has no interpolation byte, so keep TotK's default (0, linear).
            source.AsSpan().CopyTo(payload);
            payload[2] = 0;
        }
        else
        {
            foreach (FieldDef f in from.Fields)
            {
                if (f.IsUnused) continue;
                string target = Aliases.TryGetValue(f.Name, out string? alias) ? alias : f.Name;
                if (!to.TryGetField(target, out FieldDef? t)) continue;
                if (FieldCopier.Copy(f, source, t, payload) == FieldCopier.Outcome.Incompatible)
                    report.Add(ConversionSeverity.Warning, scope, $"{kind}.{f.Name} is not shaped like TotK {kind}.{target}; skipped.");
            }
            foreach (FieldDef f in from.Fields.Where(f => !f.IsUnused && !to.Contains(Aliases.GetValueOrDefault(f.Name, f.Name))))
                if (source.AsSpan(f.Offset, f.ByteLength).ContainsAnyExcept((byte)0))
                    report.Add(ConversionSeverity.Warning, scope, $"{kind}.{f.Name} has no TotK counterpart and was dropped.");
        }
        report.ChunksConverted++;
        return new VfxbTreeNode(kind) { Data = payload };
    }

    private static int KeyCount(ChunkLayout layout, byte[] payload)
        => (int)new StructView(layout, payload.AsSpan(0, layout.Size).ToArray()).GetUInt32(layout.Repeating!.CountField);
}
