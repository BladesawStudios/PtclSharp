using System.Buffers.Binary;
using System.Numerics;
using PtclSharp.Layout;

namespace PtclSharp.Simulation.Resources;

/// <summary>
/// Reads BotW (nn::vfx 4.4.0) emitters. Offsets come from the named EMTR/chunk layouts in PtclSharp; the static-block fix-ups are a
/// port of EmitterResource::UpdateParams (0x7100adb8f4) and its shader-flag helper 0x7100adc8a8.
/// </summary>
public sealed class BotwEmitterResourceReader : IEmitterResourceReader
{
    public PtclVersion Version => PtclVersion.BotW_NintendoWareVfx_4_4_0;

    private const int StaticBlockSize = 0x750;

    public EmitterSetResource ReadSet(VfxbFile file, VfxbEmitterSet set)
    {
        var result = new EmitterSetResource { Name = set.Name };
        // The flat list is depth-first; Depth 0 entries are parents, Depth 1 entries belong to the nearest preceding parent.
        EmitterResource? parent = null;
        foreach (VfxbEmitter e in set.Emitters)
        {
            EmitterResource res = ReadEmitter(file, e);
            res.Set = result;
            if (e.Depth == 0) { result.Emitters.Add(res); parent = res; }
            else if (parent is not null) { res.Parent = parent; parent.Children.Add(res); }
        }
        return result;
    }

    private static bool B(in StructView v, string f) => v.GetByte(f) != 0;
    private static Vector3 V3(in StructView v, string f) => new(v.GetSingle(f, 0), v.GetSingle(f, 1), v.GetSingle(f, 2));

    public EmitterResource ReadEmitter(VfxbFile file, VfxbEmitter emitter)
    {
        PtclLayoutSet layouts = file.Layouts;
        int dataOffset = emitter.Node.DataOffset!.Value;
        byte[] copy = file.Data.AsSpan(dataOffset, file.Layout.EmitterFixedDataSize).ToArray();
        var v = new StructView(layouts.Emitter, copy);

        var r = new EmitterResource { Name = emitter.Name };

        // attribute chunks first: the shader-flag word depends on which fields exist
        var chunks = new Dictionary<string, VfxbNode>();
        foreach (VfxbNode a in emitter.Node.Attributes) chunks.TryAdd(a.Kind, a);
        ReadChunks(file, r, chunks);

        // ----- emitter -----
        r.Calc = (CalcType)v.GetByte("emitter_calc_type");
        r.Follow = (FollowType)v.GetByte("velocity_coord");
        r.SortType = v.GetByte("particle_sort_mode_index");
        r.DrawPath = v.GetUInt32("draw_path");
        r.CustomActionIndex = v.GetInt32("custom_action_index");
        r.CustomShaderIndex = v.GetInt32("custom_shader_index");
        r.IsFadeEmit = B(v, "fade_emit_stop");
        r.IsAlphaFadeOut = B(v, "fade_out_curve");
        r.IsScaleFadeOut = B(v, "fade_out_scale");
        r.IsAlphaFadeIn = B(v, "fade_in_curve");
        r.IsScaleFadeIn = B(v, "fade_in_scale");
        r.FadeOutTime = v.GetInt32("fade_out_time");
        r.FadeInTime = v.GetInt32("fade_in_time");
        r.RandomSeedType = v.GetByte("seed_source");
        r.FixedSeed = v.GetUInt32("fixed_seed");
        r.IsUpdateMatrixByEmit = B(v, "update_matrix_by_emit");
        r.Trans = V3(v, "emitter_trans_xyz"); r.TransRand = V3(v, "emitter_trans_rnd");
        r.Rotate = V3(v, "emitter_rot_xyz"); r.RotateRand = V3(v, "emitter_rot_rnd");
        r.Scale = V3(v, "emitter_scale_xyz");
        r.EmitterColor0 = new Vector4(V3(v, "emitter_color0_rgb"), v.GetSingle("emitter_color0_alpha"));
        r.EmitterColor1 = new Vector4(V3(v, "emitter_color1_rgb"), v.GetSingle("emitter_color1_alpha"));

        // ----- emission -----
        r.IsOneTime = B(v, "emit_loop_mode");
        r.IsWorldGravity = B(v, "gravity_coord");
        r.IsEmitDistEnabled = B(v, "emit_dist_enable");
        r.IsWorldOrientedVelocity = B(v, "designated_direction_transform_enable");
        r.Start = (int)v.GetUInt32("emit_start_delay");
        r.EmitTiming = (int)v.GetUInt32("child_emit_timing");
        r.EmitDuration = (int)v.GetUInt32("emit_duration");
        r.Rate = v.GetSingle("emit_rate");
        r.RateRandomPercent = v.GetInt32("emit_rate_random_percent");
        r.Interval = v.GetInt32("emit_interval");
        r.IntervalRandom = v.GetInt32("emit_interval_random");
        r.PosRand = v.GetSingle("emission_position_table_offset_scale");
        r.GravityScale = v.GetSingle("gravity_scale");
        r.GravityDir = V3(v, "gravity_xyz");
        r.EmitDistUnit = v.GetSingle("emit_dist_unit");
        r.EmitDistMin = v.GetSingle("emit_dist_min");
        r.EmitDistMax = v.GetSingle("emit_dist_max");
        r.EmitDistMargin = v.GetSingle("emit_dist_margin");
        r.EmitDistParticleMax = v.GetInt32("emit_dist_particle_max");

        // ----- volume -----
        r.Volume = (VolumeType)v.GetByte("shape_type");
        r.SweepStartRandom = B(v, "shape_angle_mode");
        r.ArcType = v.GetByte("shape_rot_mode");
        r.SweepLongitude = v.GetSingle("shape_angle_b");
        r.SweepLatitude = v.GetSingle("shape_angle_c");
        r.SweepStart = v.GetSingle("shape_angle_d");
        r.SurfacePosRand = v.GetSingle("shape_angle_random");
        r.CaliberRatio = v.GetSingle("shape_fill_ratio");
        r.LineCenter = v.GetSingle("line_center_bias");
        r.LineLength = v.GetSingle("line_length");
        r.VolumeRadius = V3(v, "shape_radius_xyz");
        r.VolumeFormScale = V3(v, "emitter_volume_scale_xyz");
        r.PrimEmit = (PrimEmitType)Math.Clamp(v.GetInt32("primitive_dist_mode"), 0, 2);
        r.NumDivideCircle = v.GetInt32("shape_divisions");
        r.NumDivideCircleRandom = (int)v.GetUInt32("circle_division_random_reduction_percent");
        r.NumDivideLine = (int)v.GetUInt32("line_division_count");
        r.NumDivideLineRandom = (int)v.GetUInt32("line_division_reduction_percent");
        r.VolumeTblIndex = v.GetByte("sphere_direction_table_index");
        r.VolumeTblIndex64 = v.GetByte("sphere64_division_count");
        r.LatitudeDir = v.GetByte("shape_rot_variant");
        r.VolumePrimitiveId = v.GetUInt64("mesh_primitive_idx");

        // ----- velocity -----
        r.AllDirection = v.GetSingle("all_directional_speed");
        r.DesignatedDirScale = v.GetSingle("designated_direction_speed");
        r.DesignatedDir = V3(v, "designated_direction_xyz");
        r.DiffusionDirAngle = v.GetSingle("emission_direction_spread_degrees");
        r.XzDiffusion = v.GetSingle("emission_tangent_amount");
        r.Diffusion = new Vector3(v.GetSingle("emission_direction_random_x"), v.GetSingle("emission_direction_random_y"), v.GetSingle("emission_direction_random_z"));
        r.VelRandomPercent = v.GetSingle("initial_speed_random_percent");
        r.EmVelInherit = v.GetSingle("emitter_motion_inherit_scale");

        // ----- particle -----
        r.Life = v.GetInt32("particle_lifespan");
        r.LifeRandomPercent = v.GetInt32("particle_lifespan_random_percent");
        r.IsLifeInfinity = B(v, "emit_infinite_flag");
        r.DynamicsRand = v.GetSingle("particle_attribute_w_random_amplitude");
        r.PtclScale = V3(v, "particle_scale_xyz");
        r.PtclScaleRandom = V3(v, "particle_scale_rnd");
        r.RotInit = V3(v, "rotation_initial_xyz");
        r.RotInitRand = V3(v, "rotation_initial_random_xyz");
        r.RotAdd = V3(v, "rotation_add_xyz");
        r.RotAddRand = V3(v, "rotation_add_random_xyz");
        r.RotRegist = v.GetSingle("rotation_add_attenuation");
        r.RotRevRandX = B(v, "rotation_random_sign_x_enable");
        r.RotRevRandY = B(v, "rotation_random_sign_y_enable");
        r.RotRevRandZ = B(v, "rotation_random_sign_z_enable");
        r.AirRegist = v.GetSingle("velocity_attenuation_per_frame");

        // ----- inherit -----
        r.InheritVelocity = B(v, "inherit_parent_velocity"); r.InheritScale = B(v, "inherit_parent_scale");
        r.InheritRotate = B(v, "inherit_parent_rotation");
        r.InheritColor0 = B(v, "inherit_parent_color0_rgb"); r.InheritColor1 = B(v, "inherit_parent_color1_rgb");
        r.InheritAlpha0 = B(v, "inherit_parent_alpha0"); r.InheritAlpha1 = B(v, "inherit_parent_alpha1");
        r.InheritAlpha0EachFrame = B(v, "inherit_parent_alpha0_each_frame"); r.InheritAlpha1EachFrame = B(v, "inherit_parent_alpha1_each_frame");
        r.EnableEmitterParticle = B(v, "inherit_enable_emitter_particle");
        r.ChildPreDraw = B(v, "child_pre_draw");
        r.InheritVelocityRate = v.GetSingle("inherit_parent_velocity_rate");
        r.InheritScaleRate = v.GetSingle("inherit_parent_scale_rate");

        // ----- shading / render -----
        r.ShaderIndex = v.GetInt32("shader_idx_normal");
        r.ShaderIndexPass1 = v.GetInt32("shader_idx_pass1");
        r.ShaderIndexPass2 = v.GetInt32("shader_idx_pass2");
        r.ComputeShader0 = v.GetInt32("compute_shader0");
        r.G3dPrimitiveId = v.GetUInt64("g3d_primitive_idx");
        r.Render = new RenderState
        {
            BlendEnable = B(v, "blend_target_enable"), DepthTest = B(v, "depth_test_enable"), DepthWrite = B(v, "depth_write_enable"),
            DepthFunc = v.GetByte("depth_compare_func"), BlendMode = v.GetByte("blend_mode_index"), CullMode = v.GetByte("cull_mode_index")
        };
        r.Textures = new TextureSlot[3];
        for (int i = 0; i < 3; i++)
        {
            Span<byte> sel = v.GetBytes($"tex_slot{i}_sampler_select");
            r.Textures[i] = new TextureSlot { Guid = v.GetUInt64($"tex_slot{i}_guid"), WrapU = sel[0], WrapV = sel[1], Filter = sel[2] };
        }

        // Emitter animation bookkeeping (Emitter::ResourceUpdate / EmitterResource::Setup).
        foreach (var track in r.EmitterAnims)
            if (track is { Enabled: true }) r.IsUseEmitterAnim = true;
        r.IsEmitterSrtAnim = (r.EmitterAnims[(int)EmitterAnimType.Scale]?.Enabled ?? false)
            || (r.EmitterAnims[(int)EmitterAnimType.Rotate]?.Enabled ?? false)
            || (r.EmitterAnims[(int)EmitterAnimType.Translate]?.Enabled ?? false);

        r.StaticBlock = BuildStaticBlock(copy, r, chunks, file);
        r.FullData = copy;
        r.NearFade = (BitConverter.ToSingle(copy, 0x5D0), BitConverter.ToSingle(copy, 0x5D4));
        // After UpdateParams the emit rate may have been overridden for equally divided shapes.
        r.Rate = v.GetSingle("emit_rate");
        return r;
    }

    // ------------------------------------------------------------------ chunks

    private static void ReadChunks(VfxbFile file, EmitterResource r, Dictionary<string, VfxbNode> chunks)
    {
        PtclLayoutSet layouts = file.Layouts;
        (string Fourcc, EmitterAnimType Type)[] ea =
        [
            ("EAES", EmitterAnimType.Scale), ("EAER", EmitterAnimType.Rotate), ("EAET", EmitterAnimType.Translate),
            ("EAC0", EmitterAnimType.Color0), ("EAC1", EmitterAnimType.Color1), ("EASL", EmitterAnimType.ParticleScale),
            ("EASS", EmitterAnimType.EmitterVolumeScale), ("EATR", EmitterAnimType.EmissionRate), ("EAPL", EmitterAnimType.ParticleLife),
            ("EAA0", EmitterAnimType.Alpha0), ("EAA1", EmitterAnimType.Alpha1), ("EAOV", EmitterAnimType.AllDirectionalVelocity),
            ("EADV", EmitterAnimType.DesignatedDirectionalVelocity), ("EAGV", EmitterAnimType.GravityScale)
        ];
        foreach (var (fourcc, type) in ea)
        {
            if (!chunks.TryGetValue(fourcc, out VfxbNode? node)) continue;
            int at = node.DataOffset!.Value;
            ReadOnlySpan<byte> d = file.Data;
            int keyCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[(at + 4)..]);
            var keys = new Vector4[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                int k = at + 0x0C + (i * 0x10);
                keys[i] = new Vector4(F(d, k), F(d, k + 4), F(d, k + 8), F(d, k + 12));
            }
            r.EmitterAnims[(int)type] = new EmitterAnimTrack { Enabled = d[at] != 0, Loop = d[at + 1] != 0, Keys = keys };
        }

        bool Chunk(string name, out StructView view)
        {
            if (!chunks.TryGetValue(name, out VfxbNode? n)) { view = default; return false; }
            ChunkLayout layout = layouts.Chunks[name];
            view = new StructView(layout, file.Data.AsSpan(n.DataOffset!.Value, layout.Size));
            return true;
        }

        if (Chunk("FRND", out StructView frnd))
        {
            var f = new FieldGpuNoise
            {
                EnableUnifiedPhase = B(frnd, "enable_unified_phase"), EnableDetailedOption = B(frnd, "enable_detailed_option"),
                EnableAirResist = B(frnd, "enable_air_resist"), RandomVel = V3(frnd, "random_vel"), Blank = frnd.GetInt32("blank"),
                UnifiedPhaseSpeed = frnd.GetSingle("unified_phase_speed"), UnifiedPhaseDistribution = frnd.GetSingle("unified_phase_distribution")
            };
            for (int i = 0; i < 4; i++) { f.WaveParam[i] = frnd.GetSingle("wave_param", i); f.WaveParamHzRate[i] = frnd.GetSingle("wave_param_hz_rate", i); }
            ReadAnim8(frnd, "random_vel_anim", f.Anim);
            r.FieldGpuNoise = f;
        }
        if (Chunk("FRN1", out StructView frn1))
        {
            var f = new FieldRandomSimple { RandomVel = V3(frn1, "random_vel"), Blank = Math.Max(1u, frn1.GetUInt32("blank")) };
            ReadAnim8(frn1, "random_vel_anim", f.Anim);
            r.FieldRandomSimple = f;
        }
        if (Chunk("FMAG", out StructView fmag))
        {
            var f = new FieldMagnet
            {
                FollowEmitter = B(fmag, "follow_emitter"), AxisX = B(fmag, "axis_x"), AxisY = B(fmag, "axis_y"), AxisZ = B(fmag, "axis_z"),
                Power = fmag.GetSingle("power"), Position = V3(fmag, "position")
            };
            ReadAnim8(fmag, "power_anim", f.Anim);
            r.FieldMagnet = f;
        }
        if (Chunk("FSPN", out StructView fspn))
        {
            var f = new FieldSpin { Rotate = fspn.GetSingle("spin_rotate"), Axis = fspn.GetInt32("spin_axis"), Outer = fspn.GetSingle("spin_outer") };
            ReadAnim8(fspn, "rotate_anim", f.RotateAnim);
            ReadAnim8(fspn, "outer_anim", f.OuterAnim);
            r.FieldSpin = f;
        }
        if (Chunk("FCOL", out StructView fcol))
            r.FieldCollision = new FieldCollision
            {
                ReactionType = fcol.GetByte("reaction_type"), IsWorld = B(fcol, "is_world"), PlaneY = fcol.GetSingle("plane_y"),
                Restitution = fcol.GetSingle("restitution"), MaxCollisions = fcol.GetInt32("max_collisions"), Drag = fcol.GetSingle("drag")
            };
        if (Chunk("FCOV", out StructView fcov))
        {
            var f = new FieldConvergence { Type = fcov.GetByte("type"), Position = V3(fcov, "position"), Ratio = fcov.GetSingle("ratio") };
            ReadAnim8(fcov, "ratio_anim", f.Anim);
            r.FieldConvergence = f;
        }
        if (Chunk("FPAD", out StructView fpad))
        {
            var f = new FieldPosAdd { IsGlobal = B(fpad, "is_global"), PositionAdd = V3(fpad, "position_add") };
            ReadAnim8(fpad, "position_add_anim", f.Anim);
            r.FieldPosAdd = f;
        }
        if (Chunk("FCLN", out StructView fcln))
            r.FieldCurlNoise = new FieldCurlNoise
            {
                Interpolation = fcln.GetByte("interpolation"), BaseRandom = fcln.GetByte("base_random"), WorldCoordinate = fcln.GetByte("world_coordinate"),
                Speed = V3(fcln, "speed"), Influence = V3(fcln, "influence"), Scale = fcln.GetSingle("scale"), Base = fcln.GetSingle("base")
            };
        r.HasCustomField = chunks.ContainsKey("FCSF");

        foreach (string plugin in new[] { "EP01", "EP02", "EP03", "EP04" })
            if (chunks.TryGetValue(plugin, out VfxbNode? p))
                r.Plugin = new PluginResource
                {
                    Type = plugin[3] - '0',
                    Data = file.Data.AsSpan(p.DataOffset!.Value, (int)p.Size - 0x20).ToArray()
                };
        if (chunks.TryGetValue("CSDP", out VfxbNode? csdp))
            r.CustomShaderBlock = file.Data.AsSpan(csdp.DataOffset!.Value, (int)csdp.Size - 0x20).ToArray();
    }

    private static float F(ReadOnlySpan<byte> d, int at) => BinaryPrimitives.ReadSingleLittleEndian(d[at..]);

    private static void ReadAnim8(in StructView v, string prefix, Anim8Key into)
    {
        into.Enable = v.GetUInt32(prefix + "_enable") != 0;
        into.Loop = v.GetUInt32(prefix + "_loop") != 0;
        into.StartRandom = v.GetUInt32(prefix + "_start_random") != 0;
        into.KeyCount = Math.Clamp(v.GetInt32(prefix + "_key_count"), 0, 8);
        into.LoopNum = v.GetInt32(prefix + "_loop_num");
        for (int i = 0; i < 8; i++)
            into.Keys[i] = new Vector4(v.GetSingle(prefix + "_keys", i * 4), v.GetSingle(prefix + "_keys", i * 4 + 1),
                v.GetSingle(prefix + "_keys", i * 4 + 2), v.GetSingle(prefix + "_keys", i * 4 + 3));
    }

    // ------------------------------------------------------------------ static block (UpdateParams)

    private static void SetF(byte[] d, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(d.AsSpan(at), value);
    private static float GetF(byte[] d, int at) => BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(at));
    private static void Copy4(byte[] d, int to, int from) => d.AsSpan(from, 4).CopyTo(d.AsSpan(to));

    private static byte[] BuildStaticBlock(byte[] d, EmitterResource r, Dictionary<string, VfxbNode> chunks, VfxbFile file)
    {
        // constant colours when the mode byte says "constant"
        if (d[0x9a4] == 0) { Copy4(d, 0x3c0, 0x9a8); Copy4(d, 0x3c4, 0x9ac); Copy4(d, 0x3c8, 0x9b0); }
        if (d[0x9a6] == 0) Copy4(d, 0x440, 0x9b4);
        if (d[0x9a5] == 0) { Copy4(d, 0x4c0, 0x9b8); Copy4(d, 0x4c4, 0x9bc); Copy4(d, 0x4c8, 0x9c0); }
        if (d[0x9a7] == 0) Copy4(d, 0x540, 0x9c4);

        // UV domain scale
        float[] domX = [1f, 2f, 1f, 2f], domY = [1f, 1f, 2f, 2f];
        foreach (var (modeAt, dest) in new[] { (0xa5c, 0x300), (0xa6c, 0x350), (0xa7c, 0x3a0) })
            if (d[modeAt] < 4) { SetF(d, dest, domX[d[modeAt]]); SetF(d, dest + 4, domY[d[modeAt]]); }

        // per-texture scroll / rotate / scale switches
        void Zero(params int[] at) { foreach (int a in at) SetF(d, a, 0f); }
        if (d[0xa59] == 0) Zero(0x2c8, 0x2cc, 0x2c0, 0x2c4, 0x2d0, 0x2d4);
        if (d[0xa69] == 0) Zero(0x318, 0x31c, 0x310, 0x314, 0x320, 0x324);
        if (d[0xa79] == 0) Zero(0x368, 0x36c, 0x360, 0x364, 0x370, 0x374);
        if (d[0xa5a] == 0) Zero(0x2f0, 0x2f4, 0x2f8);
        if (d[0xa6a] == 0) Zero(0x340, 0x344, 0x348);
        if (d[0xa7a] == 0) Zero(0x390, 0x394, 0x398);
        if (d[0xa5b] == 0) { SetF(d, 0x2e0, 1); SetF(d, 0x2e4, 1); Zero(0x2d8, 0x2dc, 0x2e8, 0x2ec); }
        if (d[0xa6b] == 0) { SetF(d, 0x330, 1); SetF(d, 0x334, 1); Zero(0x328, 0x32c, 0x338, 0x33c); }
        if (d[0xa7b] == 0) { SetF(d, 0x380, 1); SetF(d, 0x384, 1); Zero(0x378, 0x37c, 0x388, 0x38c); }

        // rotation lanes
        if (d[0x8b0] == 0) Zero(0x700, 0x710, 0x720, 0x730);
        if (d[0x8b1] == 0) Zero(0x704, 0x714, 0x724, 0x734);
        if (d[0x8b2] == 0) Zero(0x708, 0x718, 0x728, 0x738);

        // key tables: pad key[count..7] with the last key, time + index
        foreach (var (countAt, table) in new[] { (0x60, 0x3c0), (0x68, 0x4c0), (0x64, 0x440), (0x6c, 0x540), (0x70, 0x600), (0x74, 0x680) })
        {
            int n = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(countAt));
            if (n < 1 || n > 7) continue;
            for (int k = n; k < 8; k++)
            {
                d.AsSpan(table + ((n - 1) * 16), 16).CopyTo(d.AsSpan(table + (k * 16)));
                SetF(d, table + (k * 16) + 12, GetF(d, table + (k * 16) + 12) + k);
            }
        }

        // loop periods and random-phase switches
        (int rate, int enable, int period, int random)[] loops =
        [
            (0x80, 0x8d8, 0x8e4, 0x94), (0x84, 0x8d9, 0x8e8, 0x98), (0x88, 0x8da, 0x8ec, 0x9c), (0x8c, 0x8db, 0x8f0, 0xa0), (0x90, 0x8dc, 0x8f4, 0xa4)
        ];
        for (int i = 0; i < 5; i++)
        {
            SetF(d, loops[i].rate, d[loops[i].enable] != 0 ? BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(loops[i].period)) : 0f);
            SetF(d, loops[i].random, d[0x8dd + i] != 0 ? 1f : 0f);
        }

        // emission rate overrides for equally divided shapes
        byte shape = d[0x838];
        if (shape > 0xf || ((1 << shape) & 0xa064) == 0) BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(0x874), -1);
        int primDist = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(0x874));
        if (primDist == 0)
        {
            float[] sphere32 = [2, 3, 4, 6, 8, 12, 20, 32];
            switch (shape)
            {
                case 2: case 0xd: SetF(d, 0x800, 1f); break;
                case 5: SetF(d, 0x800, sphere32[Math.Min((int)d[0x83c], 7)]); break;
                case 6: SetF(d, 0x800, d[0x83d]); break;
            }
        }

        // shader feature flags
        uint w0 = 0, w1 = 0;
        if (d[0x7f1] != 0) w0 |= 8;
        if (d[0x9ef] < 0x30) w0 |= 1u << (d[0x9ef] >> 4);
        foreach (var (modeAt, blockAt, bit1) in new[] { (0xa58, 0x110, 0x10), (0xa68, 0x1a0, 0x100), (0xa78, 0x230, 0x1000) })
        {
            byte mode = d[modeAt];
            if (mode is >= 1 and <= 3) w0 |= (uint)bit1 << (mode - 1);
            if (mode == 4)
            {
                float count = GetF(d, blockAt + 8);
                for (int i = 0; i < count; i++) BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(blockAt + 0x10 + (i * 4)), i);
                SetF(d, blockAt, count);
                w0 |= (uint)bit1 << 3;
            }
        }
        (int at, uint bit)[] bits =
        [
            (0x8ad, 0x10000), (0x8ae, 0x20000), (0x8af, 0x40000), (0xa5d, 0x80000), (0xa5e, 0x100000), (0xa6d, 0x200000), (0xa6e, 0x400000),
            (0xa7d, 0x800000), (0xa7e, 0x1000000), (0xa5f, 0x2000000), (0xa6f, 0x4000000), (0xa7f, 0x8000000), (0x8b3, 0x10000000)
        ];
        foreach (var (at, bit) in bits) if (d[at] != 0) w0 |= bit;
        if (d[0x8ac] == 1) w0 |= 0x40000000;
        if (d[0x8ac] == 0) w0 |= 0x20000000;
        if (d[0x8b4] != 0) w1 |= 1;
        (string chunk, uint bit)[] fieldBits = [("FRND", 2), ("FRN1", 0x100), ("FPAD", 4), ("FMAG", 8), ("FCOV", 0x10), ("FSPN", 0x20), ("FCOL", 0x40), ("FCLN", 0x80)];
        foreach (var (chunk, bit) in fieldBits) if (chunks.ContainsKey(chunk)) w1 |= bit;
        if (d[0x753] < 3) w1 |= new uint[] { 0x200, 0x800, 0x400 }[d[0x753]];
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(0x50), w0);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(0x54), w1);
        if (chunks.TryGetValue("FCSF", out VfxbNode? fcsf))
            file.Data.AsSpan(fcsf.DataOffset!.Value, 4).CopyTo(d.AsSpan(0x5c));

        return d.AsSpan(0, StaticBlockSize).ToArray();
    }
}
