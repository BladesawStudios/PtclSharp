using PtclSharp.Layout;

namespace PtclSharp.Conversion;

/// <summary>
/// The TotK shader a converted emitter will use. A BotW emitter's shader indices point into the BotW file's own shader
/// archive, which is not carried over, so the caller chooses a TotK shader (usually by copying a donor TotK emitter).
/// </summary>
/// <param name="Fields">TotK emitter fields to set (name to integer value), for example <c>shader_idx_normal</c>.</param>
/// <param name="CustomShaderParams">The <c>CSDP</c> payload that matches the chosen shader, or null for none.</param>
public sealed record ShaderBinding(IReadOnlyDictionary<string, long> Fields, byte[]? CustomShaderParams = null)
{
    private static readonly string[] ShaderFields =
    [
        "shader_idx_normal", "shader_idx_pass1", "shader_idx_pass2", "compute_shader0", "compute_shader1", "compute_shader2",
        "custom_shader_index", "custom_action_index",
        "graphics_shader0_internal", "graphics_shader1_internal", "graphics_shader2_internal",
        "compute_shader0_internal", "compute_shader1_internal", "compute_shader2_internal"
    ];

    /// <summary>Takes every shader-selection field of <paramref name="donor"/> (a TotK emitter block) and its <c>CSDP</c> payload.</summary>
    public static ShaderBinding FromDonorEmitter(ReadOnlySpan<byte> donorEmitterData, byte[]? donorCustomShaderParams)
    {
        PtclLayoutSet totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);
        var view = new StructView(totk.Emitter, donorEmitterData.ToArray());
        var fields = new Dictionary<string, long>();
        foreach (string name in ShaderFields)
        {
            FieldDef f = totk.Emitter[name];
            fields[name] = f.Type switch
            {
                FieldType.U8 => view.GetByte(name),
                FieldType.I32 => view.GetInt32(name),
                FieldType.U32 => view.GetUInt32(name),
                _ => throw new InvalidOperationException($"Unexpected shader field type {f.Type}.")
            };
        }
        return new ShaderBinding(fields, donorCustomShaderParams);
    }
}

/// <summary>Everything a BotW to TotK conversion needs from the caller beyond the BotW data itself.</summary>
public sealed class ConverterOptions
{
    /// <summary>Chooses the TotK shader for a converted emitter from its BotW EMTR data; null keeps the BotW indices and reports an error.</summary>
    public Func<byte[], ShaderBinding?>? ShaderBinder { get; init; }

    /// <summary>Maps a BotW texture GUID to the TotK GUID of the equivalent texture; null keeps the GUID and reports a warning.</summary>
    public Func<ulong, ulong?>? TextureMap { get; init; }

    /// <summary>
    /// Also copies the GPU-only fields whose purpose is unproven but whose position, shader read pattern and value
    /// distribution match a TotK field (see <see cref="BotwToTotkConverter.UnverifiedAnalogs"/>). Default true.
    /// </summary>
    public bool CopyUnverifiedAnalogs { get; init; } = true;
}
