#!/bin/sh
# Builds the Ganon fire field's effect: TotK's ExpandFireField_Drake effect (read only), its fire set renamed and painted pink.
# usage: make_ganon_fire_effect.sh <TotK romfs> <out.esetb.byml.zs>
R="$1"; OUT="$2"
HERE="$(dirname "$0")"
# --paint keeps each colour's brightness and replaces its hue, so the flames keep their (HDR) intensity and animation.
dotnet run --project "$HERE" -- "$R/Effect/ExpandFireField_Drake.Nin_NX_NVN.esetb.byml.zs" "$R/Pack/ZsDic.pack.zs" "$OUT" \
  --only-set Chm_ExpandFireField_Drake_Fire --rename-set Chm_ExpandFireField_Drake_Fire=GanonBeast_FireField \
  --paint Fire_Outside=1,0.08,0.35 --paint Fire_Spark=1,0.2,0.45 --paint Fire_PointLight=1,0.1,0.4
