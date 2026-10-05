#!/bin/sh
# Builds the Ganon beam effect from TotK's Drake fire beam effect (read only).
# usage: make_ganon_beam_effect.sh <TotK romfs> <out.esetb.byml.zs>
R="$1"; OUT="$2"
HERE="$(dirname "$0")"
# Body: BotW's palette is a magenta-pink (core 2,0.006,0.72; glow 1,0.11,0.4) at enormous HDR gain (500x, 6000x) which is the bloom.
# Hit: BotW's flames use color0_mode 2 (the key frames, not the orange constant): (100,20,20) -> (100,4,20) -> (100,1,20), a red-pink, and its
# Core is magenta (0.92,0.2,0.88). Drake's flames are orange, so they are painted and made larger.
dotnet run --project "$HERE" -- "$R/Effect/Drake_Beam_Small_Fire.Nin_NX_NVN.esetb.byml.zs" "$R/Pack/ZsDic.pack.zs" "$OUT" \
  --only-set Enm_Drake_Beam_Small_Fire --only-set Enm_Drake_Beam_Small_Top_Fire \
  --rename-set Enm_Drake_Beam_Small_Fire=GanonBeast_Beam --rename-set Enm_Drake_Beam_Small_Top_Fire=GanonBeast_BeamHit \
  --drop Impact_Fire_01 \
  --paint BeamLine_Fire_02=1,0.02,0.4 --gain BeamLine_Fire_02=6 --scale BeamLine_Fire_02=2,1,2 \
  --paint GlowLine_Fire_01=1,0.11,0.4 --gain GlowLine_Fire_01=8 --scale GlowLine_Fire_01=3,1,3 \
  --paint AroundLine_Fire_01=1,0.05,0.4 --gain AroundLine_Fire_01=6 --scale AroundLine_Fire_01=2,1,2 \
  --paint LineLight_Fire_00=1,0.11,0.4 --gain LineLight_Fire_00=8 --scale LineLight_Fire_00=3,1,3 \
  --paint ShockWave_00=1,0.1,0.3 --paint FireRoot_00=1,0.05,0.2 --paint Fire_Spread_00=1,0.05,0.2   --paint PointGlow_Point_00=0.92,0.2,0.88 --paint PointLight_Fire_00=1,0.1,0.4 --paint Spark_00=1,0.3,0.4   --scale ShockWave_00=8,8,8 --scale FireRoot_00=8,8,8 --scale PointGlow_Point_00=8,8,8 --scale Fire_Spread_00=8,8,8 --scale Spark_00=8,8,8 --scale PointLight_Fire_00=3,3,3
