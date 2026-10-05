#!/bin/sh
# Builds the Ganon beam effect from TotK's Drake fire beam effect (read only), with BotW's colour/alpha/scale animation copied on.
# usage: make_ganon_beam_effect.sh <TotK romfs> <BotW ROM> <out.esetb.byml.zs>
R="$1"; B="$2"; OUT="$3"
HERE="$(dirname "$0")"
# Emitter pairing (TotK Drake emitter <- BotW GanonBeastBeam emitter), by role:
#   BeamLine_Fire_02 <- Emitter1_Copy2 (thin hot core)   GlowLine_Fire_01 <- Emitter1_Copy2_Copy2 (soft core)   LineLight_Fire_00 <- Light_Long (glow)
#   FireRoot_00 <- YBill   Fire_Spread_00 <- Smoke   ShockWave_00 <- Smoke_Center   PointGlow_Point_00 <- Core   (the hit flames)
# The body keeps the Drake emitters' own intensity, alpha and animation (BotW's absolute values, copied with --keys, clip to flat white and
# fill the mesh solid: its shaders read alpha and HDR differently); only the hue (--paint, brightness-preserving) and width change.
# The keyed flame emitters (FireRoot, Fire_Spread, ShockWave) are repainted pink with a capped peak: BotW's keys (100,20,20), (160,24,2) clip to orange-yellow on
# TotK's flame sprites.
# AroundLine_Fire_01, PointLight_Fire_00 and Spark_00 have no BotW counterpart and are painted. Sizes are TotK's, scaled (--scale).
dotnet run --project "$HERE" -- "$R/Effect/Drake_Beam_Small_Fire.Nin_NX_NVN.esetb.byml.zs" "$R/Pack/ZsDic.pack.zs" "$OUT" \
  --botw "$B/Effect/GanonBeastBeam.sesetlist" \
  --only-set Enm_Drake_Beam_Small_Fire --only-set Enm_Drake_Beam_Small_Top_Fire \
  --rename-set Enm_Drake_Beam_Small_Fire=GanonBeast_Beam --rename-set Enm_Drake_Beam_Small_Top_Fire=GanonBeast_BeamHit \
  --drop Impact_Fire_01 \
  --paint BeamLine_Fire_02=1,0.85,0.95 --gain BeamLine_Fire_02=8 --scale BeamLine_Fire_02=0.8,1,0.8   --paint GlowLine_Fire_01=1,0.04,0.35 --gain GlowLine_Fire_01=3 --scale GlowLine_Fire_01=2,1,2   --paint AroundLine_Fire_01=1,0.03,0.35 --gain AroundLine_Fire_01=3 --scale AroundLine_Fire_01=1.5,1,1.5   --paint LineLight_Fire_00=1,0.05,0.35 --gain LineLight_Fire_00=3 --scale LineLight_Fire_00=1.5,1,1.5   --keys FireRoot_00=YBill --paint FireRoot_00=1,0.08,0.35 --peak FireRoot_00=25 --scale FireRoot_00=16,16,16 --round FireRoot_00 --still FireRoot_00 \
  --keys Fire_Spread_00=Smoke --paint Fire_Spread_00=1,0.08,0.35 --peak Fire_Spread_00=25 --scale Fire_Spread_00=16,13.4,16 --round Fire_Spread_00 \
  --keys ShockWave_00=Smoke_Center --paint ShockWave_00=1,0.08,0.35 --peak ShockWave_00=25 --scale ShockWave_00=12,12,12 \
  --keys PointGlow_Point_00=Core --scale PointGlow_Point_00=4,4,4 \
  --paint PointLight_Fire_00=1,0.1,0.4 --scale PointLight_Fire_00=1.5,1.5,1.5 \
  --paint Spark_00=1,0.3,0.4 --scale Spark_00=8,8,8
