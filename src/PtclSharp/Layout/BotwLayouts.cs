using static PtclSharp.Layout.LayoutBuilders;

namespace PtclSharp.Layout;

/// <summary>
/// Breath of the Wild (<c>nn::vfx</c>, VFXB version 20). The EMTR table is generated from
/// docs/research/botw-emtr-offsets-ghidra.md, which was re-verified against the BotW 1.6.0 Switch executable
/// (every row carries its own status: Confirmed, Paired, Unverified or Unused). The chunk layouts below are
/// hand-written from the same document (section 4).
/// </summary>
internal static class BotwLayouts
{
    internal static PtclLayoutSet Create() => new(
        PtclVersion.BotW_NintendoWareVfx_4_4_0,
        VfxbLayouts.BotW,
        FileHeader(),
        NodeHeader(C),
        EmitterSet(),
        new EmitterLayout("EMTR", 0xA88, BotwEmitterFields.All, textureSlotCount: 3, keyframeTrackCount: 6),
        Chunks());

    private static StructLayout FileHeader() => new("VfxbFileHeader", 0x40,
    [
        Str("signature", 0x00, 8, C, "Resource constructor 0x7100ae2a5c: BinaryFileHeader::IsSignatureValid."),
        U8("version_micro", 0x08, V, "Standard header field."),
        U8("graphics_api", 0x09, C, "Resource constructor 0x7100ae2a5c requires 4."),
        U16("binary_version", 0x0A, C, "Resource constructor 0x7100ae2a5c requires 0x14 (20)."),
        U16("byte_order_mark", 0x0C, V, "Checked by the reader."),
        U8("alignment_shift", 0x0E, C, "Resource constructor 0x7100ae2a5c: BinaryFileHeader::IsAlignmentValid / GetAlignment."),
        U8("target_address_size", 0x0F, V, "Standard header field."),
        U32("file_name_offset", 0x10, V, "Standard header field."),
        U16("flag", 0x14, V, "Standard header field."),
        U16("first_block_offset", 0x16, C, "Resource constructor 0x7100ae2a5c: the node chain starts at header + this value."),
        U32("relocation_table_offset", 0x18, V, "Standard header field."),
        U32("file_size", 0x1C, V, "Checked by the reader."),
        Str("file_name", 0x20, 0x20, V, "Zero-padded file name string.")
    ]);

    private static StructLayout EmitterSet() => new("ESET", 0x60,
    [
        Str("name", 0x10, 0x40, C, "Resource::SearchEmitterSetId 0x7100AE4A10."),
        I32("emitter_count", 0x50, C, "InitializeEmitterSetResource 0x7100AE44EC (signed 32-bit)."),
        I32("unused_58", 0x58, U, "No reader found in the effect library (a taint trace of every ESET data load reaches only 0x50); values such as 500, 30 and 255 in the corpus suggest editor or game-side metadata."),
        U32("unused_5C", 0x5C, U, "No reader found in the effect library; 0xFF in 6 corpus sets.")
    ]);

    private static IEnumerable<ChunkLayout> Chunks()
    {
        const string Ea = "CalculateEmitterKeyFrameAnimation 0x7100adf920 (reads only the loop byte, key count and keys).";
        foreach (string fourcc in new[] { "EAES", "EAER", "EAET", "EAC0", "EAC1", "EATR", "EAPL", "EAA0", "EAA1", "EAOV", "EADV", "EASL", "EASS", "EAGV" })
        {
            yield return new ChunkLayout(fourcc, 0x0C,
            [
                U8("enabled", 0x00, C, "The emitter lane loop (0x7100ad73d0) skips lanes whose byte is zero."),
                U8("loop", 0x01, C, Ea + " fmodf by the last key time when nonzero."),
                U8("unused_02", 0x02, U, "Not read by BotW (TotK uses this byte as the interpolation mode)."),
                U8("unused_03", 0x03, U, "Not read."),
                U32("key_count", 0x04, C, Ea),
                U32("unused_08", 0x08, U, "Not read; always 0 in the corpus.")
            ],
            new RepeatingGroup("keys", 0x0C, 0x10, "key_count",
            [
                F32("value", 0x00, C, "x, y, z value of the key.", count: 3),
                F32("time", 0x0C, C, "Key time; linear interpolation between keys.")
            ]));
        }

        const string Fr = "FieldRandom 0x7100aece80 / RandFunc 0x7100aed4ac / UpdateParams 0x7100adb8f4.";
        yield return new ChunkLayout("FRND", 0xD0,
            new[]
            {
                U8("enable_unified_phase", 0x00, C, Fr + " 0x7100aece80: zero uses per-axis noise, non-zero the unified phase (speed 0x14, distribution 0x18)."),
                U8("enable_detailed_option", 0x01, C, Fr + " Zero uses the simple noise table, non-zero the wave function RandFunc (wave_param at +0x1C..+0x28 amplitudes, wave_param_hz_rate at +0x2C..+0x38 divisors)."),
                U8("enable_air_resist", 0x02, C, Fr + " Non-zero applies the velocity attenuation (EMTR 0xC0) to the time step."),
                U8("unused_03", 0x03, U, "Not copied or read; always 0 in the corpus."),
                F32("random_vel", 0x04, C, Fr, count: 3),
                I32("blank", 0x10, C, Fr + " Converted to float."),
                F32("unified_phase_speed", 0x14, C, Fr),
                F32("unified_phase_distribution", 0x18, C, Fr),
                F32("wave_param", 0x1C, C, Fr, count: 4),
                F32("wave_param_hz_rate", 0x2C, C, Fr, count: 4)
            }.Concat(Anim8KeyV20("random_vel_anim", 0x3C, "CalculateField8KeyAnim 0x7100adfa5c; enable word at +0x3C tested in 0x7100ae0e34.")));

        yield return new ChunkLayout("FRN1", 0xA4,
            new[]
            {
                F32("random_vel", 0x00, C, "0x7100ae0e34: constant vector used when the animation is disabled, scaled by the 512-entry random table.", count: 3),
                U32("blank", 0x0C, C, "0x7100ae0e34: impulse applied when (int)time % blank == 0.")
            }.Concat(Anim8KeyV20("random_vel_anim", 0x10, "0x7100ae0e34: animation enable word at +0x10.")));

        const string Mag = "FieldMagnet 0x7100adfd38.";
        yield return new ChunkLayout("FMAG", 0xA8,
            new[]
            {
                U8("follow_emitter", 0x00, C, Mag + " Zero evaluates in world space, non-zero in emitter space."),
                U8("axis_x", 0x01, C, Mag + " Nonzero enables the X component."),
                U8("axis_y", 0x02, C, Mag + " Nonzero enables the Y component."),
                U8("axis_z", 0x03, C, Mag + " Nonzero enables the Z component."),
                F32("power", 0x04, C, Mag + " Constant used when the animation is disabled."),
                F32("position", 0x08, C, Mag, count: 3)
            }.Concat(Anim8KeyV20("power_anim", 0x14, Mag + " Enable word at +0x14.")));

        const string Spn = "CalculateParticleBehaviorFieldSpin 0x7100ae0244 (slot +0x260): rotates the XY/XZ/YZ plane of the velocity (axis 0..2) by rotate * dt and adds outward push outer.";
        yield return new ChunkLayout("FSPN", 0x134,
            new[]
            {
                F32("spin_rotate", 0x00, C, Spn),
                I32("spin_axis", 0x04, C, Spn + " Converted to float."),
                F32("spin_outer", 0x08, C, Spn)
            }.Concat(Anim8KeyV20("rotate_anim", 0x0C, Spn)).Concat(Anim8KeyV20("outer_anim", 0xA0, Spn)));

        const string Col = "CalculateParticleBehaviorFieldCollision 0x7100ae065c (slot +0x268); no shipped file has this chunk.";
        yield return new ChunkLayout("FCOL", 0x14,
        [
            U8("reaction_type", 0x00, C, Col + " Combined into one float with is_world."),
            U8("is_world", 0x01, C, Col),
            F32("plane_y", 0x04, C, Col),
            F32("restitution", 0x08, C, Col),
            I32("max_collisions", 0x0C, C, Col + " -1 means unlimited; otherwise the particle stops colliding once its collision counter reaches the value."),
            F32("drag", 0x10, C, Col)
        ]);

        const string Cov = "FieldConvergence 0x7100ae0994 (slot +0x270): pulls the particle toward position by ratio * dt (type selects local or emitter space).";
        yield return new ChunkLayout("FCOV", 0xA8,
            new[]
            {
                U8("type", 0x00, C, Cov),
                F32("position", 0x04, C, Cov, count: 3),
                F32("ratio", 0x10, C, Cov)
            }.Concat(Anim8KeyV20("ratio_anim", 0x14, Cov)));

        yield return new ChunkLayout("FCSF", 0x44,
        [
            U32("custom_field_type", 0x00, C, "UpdateParams 0x7100adb8f4 copies it to EMTR 0x5C (runtime_attribute_word); consumed outside the effect library."),
            F32("value", 0x04, C, "UpdateParams 0x7100adb8f4 copies the first eight floats into the field buffer (custom field parameters 0..7).", count: 16)
        ]);

        const string Pad = "0x7100ae0e34 (slot +0x278) and UpdateParams.";
        yield return new ChunkLayout("FPAD", 0xA4,
            new[]
            {
                U8("is_global", 0x00, C, Pad + " Zero adds the vector in emitter space (0x7100ae0e34 selects the emitter basis for a zero byte)."),
                F32("position_add", 0x04, C, Pad, count: 3)
            }.Concat(Anim8KeyV20("position_add_anim", 0x10, Pad)));

        const string Cln = "CurlNoise 0x7100ad4c70 (slot +0x280).";
        yield return new ChunkLayout("FCLN", 0x24,
        [
            U8("interpolation", 0x00, C, Cln + " Bit 0 set samples with interpolation, clear uses the nearest table entry."),
            U8("base_random", 0x01, C, Cln + " Bit 0 multiplies base by the per-particle random."),
            U8("world_coordinate", 0x02, C, Cln + " Non-zero transforms the particle position by the emitter basis before sampling and rotates the result back."),
            F32("speed", 0x04, C, Cln, count: 3),
            F32("influence", 0x10, C, Cln, count: 3),
            F32("scale", 0x1C, C, Cln),
            F32("base", 0x20, C, Cln)
        ]);

        const string Ep1 = "ConnectionStripeSystem 0x7100ae75bc / 0x7100ae7260 / Draw 0x7100ae9560.";
        yield return new ChunkLayout("EP01", 0x28,
        [
            U32("calc_type", 0x00, C, Ep1 + " Values 1 and 2 are tested as (v - 1) < 2."),
            U32("unused_04", 0x04, U, "No reader found in the connection stripe update, init or draw; always 0 in the corpus."),
            U32("option", 0x08, C, "Draw 0x7100ae9560: 1 draws a second (cross) mesh."),
            U32("unused_0C", 0x0C, U, "No reader found in the connection stripe code; 1 in 17 of 31 corpus chunks."),
            I32("num_divide", 0x10, C, Ep1),
            U32("connection_type", 0x14, C, Ep1 + " Tested against 1 and 2."),
            F32("head_alpha", 0x18, C, "0x7100ae7260: copied into the emitter user data."),
            F32("tail_alpha", 0x1C, C, "0x7100ae7260: copied into the emitter user data."),
            U32("unused_20", 0x20, U, "No reader found; always 0 in the corpus."),
            U32("unused_24", 0x24, U, "No reader found in the connection stripe code; 1 in 28 of 31 corpus chunks.")
        ]);

        const string Ep2 = "StripeSystem 0x7100ae54d0 / 0x7100ae6388 / 0x7100ae5e8c.";
        yield return new ChunkLayout("EP02", 0x2C,
        [
            U32("calc_type", 0x00, C, "0x7100ae6388 / 0x7100ae5e8c: non-zero selects the upright/matrix orientation."),
            U32("emitter_follow", 0x04, C, "0x7100ae6388: zero takes the particle-position branch, non-zero follows the emitter."),
            U32("option", 0x08, C, "DrawParticleStripe 0x7100ae6de4: 1 draws a second (cross) mesh."),
            U32("texturing", 0x0C, C, "0x7100ae6388: 1 maps the texture over num_history, 0 over the live history count."),
            F32("num_divide", 0x10, C, Ep2 + " Converted with (int)."),
            F32("num_history", 0x14, C, Ep2 + " Converted with (int)."),
            U32("unused_18", 0x18, U, "No reader found in the stripe update, emit or draw code; 1 in 2 of 106 corpus chunks."),
            F32("head_alpha", 0x1C, C, "0x7100ae54d0: copied into the emitter user data."),
            F32("tail_alpha", 0x20, C, "0x7100ae54d0: copied into the emitter user data."),
            U32("unused_24", 0x24, U, "No reader found in the stripe code; 1 in 17 of 106 corpus chunks."),
            F32("dir_interpolate", 0x28, C, "0x7100ae6388: positive values blend the stripe direction toward the previous direction.")
        ]);

        const string Ep3 = "SuperStripeSystem 0x7100ae9854 / 0x7100aea3dc.";
        yield return new ChunkLayout("EP03", 0x84,
        [
            U32("calc_type", 0x00, C, "0x7100aea3dc: compared with 3."),
            U32("emitter_follow", 0x04, C, "0x7100aea3dc: zero takes the world-space history branch."),
            U32("option", 0x08, C, "DrawParticleStripe 0x7100aecc38: 1 draws a second (cross) mesh."),
            U32("texturing0", 0x0C, C, "0x7100aebda0: selects which of the UV candidates feeds the first texture coordinate (0 or 1)."),
            U32("texturing1", 0x10, C, "0x7100aebda0: selector for the second texture coordinate."),
            U32("texturing2", 0x14, C, "0x7100aebda0: selector for the third texture coordinate."),
            F32("num_history", 0x18, C, Ep3 + " Converted with (int)."),
            U32("unused_1C", 0x1C, U, "No reader found; always 0 in the corpus."),
            F32("head_alpha", 0x20, C, "0x7100ae9854: copied into the stripe instance."),
            F32("tail_alpha", 0x24, C, "0x7100ae9854: copied into the stripe instance."),
            I32("num_divide", 0x28, C, Ep3),
            Raw("unused_2C", 0x2C, 8, U, "No reader found; always 0 in the corpus."),
            F32("history_air_resist", 0x34, C, "0x7100aea3dc: attenuation of the history velocity."),
            F32("history_acceleration", 0x38, C, "0x7100aea3dc: three floats.", count: 3),
            F32("history_vec_regulation", 0x44, C, "0x7100aea3dc."),
            F32("history_vec_init_speed", 0x48, C, "0x7100aea3dc: multiplies the sine sum of the initial history vector; same structure as SDK SuperStripe historyVecInitSpeed (regulation x rotate cycle feed the phase)."),
            F32("history_init_vec_rotate_cycle", 0x4C, C, "0x7100aea3dc: three floats.", count: 3),
            U32("texture_uv_map_type", 0x58, C, "0x7100aebda0: value 1 maps the texture by accumulated stripe length (total length is summed first); other values map by history index."),
            F32("head_scale", 0x5C, C, "0x7100ae9854: copied into the stripe instance."),
            F32("tail_scale", 0x60, C, "0x7100ae9854: copied into the stripe instance."),
            Raw("unused_64", 0x64, 0x20, U, "No reader found; always 0 in the corpus (TotK has static parameters in this area).")
        ]);

        const string Ep4 = "AreaLoopSystem::Draw 0x7100ad3a2c.";
        yield return new ChunkLayout("EP04", 0x50,
        [
            F32("repeat_offset", 0x00, C, Ep4, count: 3),
            F32("repeat_num", 0x0C, C, Ep4 + " (int)(value + 1) repetitions are drawn."),
            F32("area_size", 0x10, C, Ep4, count: 3),
            F32("unverified_1C", 0x1C, V, Ep4 + " Copied into the draw constants; always 0 in the corpus."),
            F32("area_pos", 0x20, C, Ep4, count: 3),
            Raw("clipping_type", 0x2C, 4, C, Ep4 + " Converted (float)(int)value; always 0 in the corpus."),
            F32("alpha_ratio", 0x30, C, Ep4, count: 3),
            F32("is_camera_loop", 0x3C, C, Ep4 + " Zero takes the fixed-area branch."),
            F32("area_rotate", 0x40, C, Ep4, count: 3),
            F32("unused_4C", 0x4C, U, "Not read by Draw or any other area-loop code; always 0 in the corpus (SDK clippingWorldHeight position).")
        ]);

        const string Prim = "PRMA handler 0x7100ae36e4 / primitive init 0x7100aedf9c / size 0x7100aedef8.";
        yield return new ChunkLayout("PRIM", 0x54,
        [
            U64("unique_id", 0x00, C, Prim),
            I32("vertex_count", 0x08, C, Prim),
            Raw("unused_array_descriptors", 0x0C, 0x2C, U, "Not read by 0x7100aedf9c or 0x7100aedef8; the corpus shows (count, component count) pairs that repeat the vertex count."),
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
            U32("next_offset", 0x08, C, "0x7100ae3994: the next entry is at this byte offset (0 ends the chain)."),
            U8("attribute_index_a", 0x14, C, "0x7100aee3a8: index (0xFF = none) of the g3d attribute-name fallback for the first UV stream."),
            U8("attribute_index_b", 0x15, C, "0x7100aee3a8: index (0xFF = none) of the fallback for the _u1 stream.")
        ]);
    }
}
