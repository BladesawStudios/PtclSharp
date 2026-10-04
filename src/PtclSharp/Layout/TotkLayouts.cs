using static PtclSharp.Layout.LayoutBuilders;

namespace PtclSharp.Layout;

/// <summary>
/// Tears of the Kingdom (<c>nn::vfx2</c>, VFXB version 51). The EMTR table is generated from
/// docs/research/totk-emtr-offsets-ghidra.md; everything else is hand-written from the same document.
/// </summary>
internal static class TotkLayouts
{
    internal static PtclLayoutSet Create() => new(
        PtclVersion.TotK_NintendoWareVfx2_15_3_1,
        VfxbLayouts.TotK,
        FileHeader(),
        NodeHeader(C),
        EmitterSet(),
        new EmitterLayout("EMTR", 0x10C8, TotkEmitterFields.All, textureSlotCount: 6, keyframeTrackCount: 10),
        Chunks());

    private static StructLayout FileHeader() => new("VfxbFileHeader", 0x40,
    [
        Str("signature", 0x00, 8, C, "Resource::Verify: BinaryFileHeader::IsSignatureValid ('VFXB    ')."),
        U8("version_micro", 0x08, V, "Standard header field; 0 in every shipped file."),
        U8("graphics_api", 0x09, C, "Resource::Verify requires 4."),
        U16("binary_version", 0x0A, C, "Resource::Verify requires 0x33 (51)."),
        U16("byte_order_mark", 0x0C, V, "0xFEFF (bytes FF FE) in every shipped file; checked by the reader."),
        U8("alignment_shift", 0x0E, C, "Consumed by BinaryFileHeader::IsAlignmentValid/GetAlignment; 12 in shipped files."),
        U8("target_address_size", 0x0F, V, "64 in every shipped file."),
        U32("file_name_offset", 0x10, V, "0x20 in every shipped file."),
        U16("flag", 0x14, V, "0 in every shipped file."),
        U16("first_block_offset", 0x16, V, "0x40 in every shipped file; the reader starts the node chain here."),
        U32("relocation_table_offset", 0x18, V, "0 in every shipped file."),
        U32("file_size", 0x1C, V, "Equals the decoded payload length; checked by the reader."),
        Str("file_name", 0x20, 0x20, V, "Zero-padded file name string.")
    ]);

    private static StructLayout EmitterSet() => new("ESET", 0xB4,
    [
        Str("name", 0x10, 0x40, P, "Emitter-set name (BotW SearchEmitterSetId); TotK sample files agree."),
        U16("emitter_count", 0x70, C, "InitializeEmitterSetResource reads a 16-bit count; it includes nested child emitters."),
        U32("unverified_78", 0x78, V, "No reader found; 0 except 0xFF in 11 corpus sets."),
        F32("unverified_94_to_A4", 0x94, V, "No reader found; corpus values look like floats.", count: 5),
        U32("unverified_A8", 0xA8, V, "No reader found; 0 or 1 in the corpus.")
    ]);

    private static IEnumerable<ChunkLayout> Chunks()
    {
        const string Ea = "CalculateEmitterKeyFrameAnimation 0x7100016250 / Emitter::Calculate 0x710000FEBC.";
        foreach (string fourcc in new[] { "EAES", "EAER", "EAET", "EAC0", "EAC1", "EATR", "EAPL", "EAA0", "EAA1", "EAOV", "EADV", "EASL", "EASS", "EAGV" })
        {
            yield return new ChunkLayout(fourcc, 0x0C,
            [
                U8("enabled", 0x00, C, "Emitter::Calculate skips a lane unless this byte is nonzero."),
                U8("loop", 0x01, C, "Time is reduced with fmodf by the last key time when nonzero."),
                U8("interpolation", 0x02, C, "0 linear, 1 hold the lower key."),
                U8("unverified_03", 0x03, V, "Not read; always 0 in the corpus."),
                U32("key_count", 0x04, C, Ea),
                U32("unverified_08", 0x08, V, "Not read; always 0 in the corpus.")
            ],
            new RepeatingGroup("keys", 0x0C, 0x10, "key_count",
            [
                F32("value", 0x00, C, "x, y, z value of the key.", count: 3),
                F32("time", 0x0C, C, "Key time; non-decreasing in the corpus.")
            ]));
        }

        const string Fr = "CalculateParticleBehaviorFieldRandom 0x71000168D0 / CalculateGpuNoise 0x710001AA10.";
        yield return new ChunkLayout("FRND", 0xD4,
            new[]
            {
                U8("enable_unified_phase", 0x00, C, Fr),
                U8("enable_detailed_option", 0x01, C, Fr),
                U8("enable_air_resist", 0x02, C, Fr),
                U8("unverified_03", 0x03, V, "Not read; always 0 in the corpus."),
                F32("random_vel", 0x04, C, Fr, count: 3),
                I32("blank", 0x10, C, Fr),
                F32("unified_phase_speed", 0x14, C, Fr),
                F32("unified_phase_distribution", 0x18, C, Fr),
                F32("wave_coefficient", 0x1C, C, "RandFunc 0x710001AFF0: multiplier of each sine term.", count: 4),
                F32("wave_divisor", 0x2C, C, "RandFunc 0x710001AFF0: divisor of the phase in each sine term.", count: 4)
            }.Concat(Anim8Key("random_vel_anim", 0x3C, "Calculate8KeyAnim 0x7100016120.")));

        yield return new ChunkLayout("FRN1", 0xA8,
            new[]
            {
                F32("random_vel", 0x00, C, "Inline block of CalculateParticleBehavior 0x7100018280.", count: 3),
                U32("blank", 0x0C, C, "Impulse applied when (int)time % blank == 0.")
            }.Concat(Anim8Key("random_vel_anim", 0x10, "Calculate8KeyAnim 0x7100016120.")));

        const string Mag = "CalculateParticleBehaviorFieldMagnet 0x7100016A10.";
        yield return new ChunkLayout("FMAG", 0xB4,
            new[]
            {
                U8("follow_emitter", 0x00, C, Mag),
                U8("axis_x", 0x01, C, Mag),
                U8("axis_y", 0x02, C, Mag),
                U8("axis_z", 0x03, C, Mag),
                F32("power", 0x04, C, Mag),
                F32("position", 0x08, C, Mag, count: 3),
                U8("unverified_AC", 0xAC, V, "Entry gate byte; meaning unproven, always 0 in the corpus.")
            }.Concat(Anim8Key("power_anim", 0x14, Mag)));

        const string Spn = "CalculateParticleBehaviorFieldSpin 0x71000170CC.";
        yield return new ChunkLayout("FSPN", 0x13C,
            new[]
            {
                F32("spin_rotate", 0x00, C, Spn),
                I32("spin_axis", 0x04, C, Spn),
                F32("spin_outer", 0x08, C, Spn)
            }.Concat(Anim8Key("rotate_anim", 0x0C, Spn)).Concat(Anim8Key("outer_anim", 0xA4, Spn)));

        const string Col = "CalculateParticleBehaviorFieldCollision 0x71000175CC.";
        yield return new ChunkLayout("FCOL", 0x14,
        [
            U8("reaction_type", 0x00, C, Col),
            U8("is_world", 0x01, C, Col),
            F32("plane_y", 0x04, C, Col),
            F32("restitution", 0x08, C, Col),
            I32("max_collisions", 0x0C, C, Col),
            F32("drag", 0x10, C, Col)
        ]);

        const string Cov = "CalculateParticleBehaviorFieldConvergence 0x710001795C.";
        yield return new ChunkLayout("FCOV", 0xAC,
            new[]
            {
                U8("type", 0x00, C, Cov),
                U8("unverified_01", 0x01, V, "Entry gate byte; meaning unproven, always 0 in the corpus."),
                F32("position", 0x04, C, Cov, count: 3),
                F32("ratio", 0x10, C, Cov)
            }.Concat(Anim8Key("ratio_anim", 0x14, Cov)));

        const string Pad = "CalculateParticleBehaviorFPAD 0x7100017D34 (executable only; no shipped file has this chunk).";
        yield return new ChunkLayout("FPAD", 0xA8,
            new[]
            {
                U8("is_global", 0x00, C, Pad),
                F32("position_add", 0x04, C, Pad, count: 3)
            }.Concat(Anim8Key("position_add_anim", 0x10, Pad)));

        const string Cln = "CalculateParticleBehavior_FieldCurlNoise 0x7100000C80.";
        yield return new ChunkLayout("FCLN", 0x24,
        [
            U8("interpolation", 0x00, C, Cln),
            U8("base_random", 0x01, C, Cln),
            U8("world_coordinate", 0x02, C, Cln),
            F32("influence", 0x04, C, Cln, count: 3),
            F32("speed", 0x10, C, Cln, count: 3),
            F32("scale", 0x1C, C, Cln),
            F32("base", 0x20, C, Cln)
        ]);

        yield return new ChunkLayout("EP01", 0x1C,
        [
            F32("head_alpha", 0x0C, C, "ConnectionStripeSystem::InitializeStripeEmitter 0x710002AD44."),
            F32("tail_alpha", 0x10, C, "ConnectionStripeSystem::InitializeStripeEmitter 0x710002AD4C.")
        ]);

        const string Ep2 = "StripeSystem 0x7100021328..0x7100023308.";
        yield return new ChunkLayout("EP02", 0x28,
        [
            U8("calc_type", 0x00, C, Ep2),
            U8("emitter_follow", 0x01, C, Ep2),
            U8("unverified_02", 0x02, V, "Value 2 selects an externally allocated vertex buffer (0x71000215F0); never occurs in the corpus."),
            F32("num_divide", 0x04, C, Ep2),
            F32("num_history", 0x08, C, Ep2),
            F32("head_alpha", 0x10, C, Ep2),
            F32("tail_alpha", 0x14, C, Ep2),
            F32("dir_interpolate", 0x1C, C, Ep2),
            I32("static_param_z", 0x20, C, "Converted with scvtf into the emitter user data; TotK meaning unproven."),
            F32("static_param_w", 0x24, C, "Copied into the emitter user data; TotK meaning unproven.")
        ]);

        const string Ep3 = "SuperStripeSystem::InitializeStripeEmitter 0x7100023458.";
        yield return new ChunkLayout("EP03", 0x60,
        [
            U8("unverified_02", 0x02, V, "Same external-buffer mode test as EP02 +0x02."),
            F32("num_history", 0x0C, C, Ep3),
            F32("head_alpha", 0x14, C, Ep3),
            F32("tail_alpha", 0x18, C, Ep3),
            I32("num_divide", 0x1C, C, Ep3),
            F32("head_scale", 0x50, C, Ep3),
            F32("tail_scale", 0x54, C, Ep3)
        ]);

        const string Ep4 = "AreaLoopSystem::Draw 0x710002E27C.";
        yield return new ChunkLayout("EP04", 0x4C,
        [
            F32("repeat_offset", 0x00, C, Ep4, count: 3),
            F32("repeat_num", 0x0C, C, Ep4),
            F32("area_size", 0x10, C, Ep4, count: 3),
            F32("area_pos", 0x20, C, Ep4, count: 3),
            Raw("clipping_type", 0x2C, 4, C, Ep4 + " Converted (float)(int)value; storage type not proven."),
            F32("alpha_ratio", 0x30, C, Ep4, count: 3),
            F32("is_camera_loop", 0x3C, C, Ep4),
            F32("area_rotate", 0x40, C, Ep4, count: 3)
        ]);

        const string Prim = "Primitive::Initialize 0x710001FEAC / 0x710001FF3C.";
        yield return new ChunkLayout("PRIM", 0x54,
        [
            U64("unique_id", 0x00, C, Prim),
            I32("vertex_count", 0x08, C, Prim),
            Raw("unverified_array_descriptors", 0x0C, 0x2C, V, "Not read by the traced initializer; corpus shows (count, component count) pairs."),
            I32("index_count", 0x38, C, Prim),
            U32("position_array_offset", 0x3C, C, Prim),
            U32("normal_array_offset", 0x40, C, Prim),
            U32("tangent_array_offset", 0x44, C, Prim),
            U32("color_array_offset", 0x48, C, Prim),
            U32("texcoord_array_offset", 0x4C, C, Prim),
            U32("index_array_offset", 0x50, C, Prim)
        ]);

        yield return new ChunkLayout("G3NT.Entry", 0x18,
        [
            U32("next_offset", 0x08, C, "BindExternalG3dResFile 0x710001E8E4 chains entries through this word; 0 ends the chain."),
            U8("attribute_index_a", 0x14, C, "G3dPrimitive::Initialize 0x7100020444 (0xFF = none)."),
            U8("attribute_index_b", 0x15, C, "G3dPrimitive::Initialize 0x7100020444 (0xFF = none).")
        ]);
    }
}
