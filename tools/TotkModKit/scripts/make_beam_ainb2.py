"""Builds the Ganon beam actor's AI from copies of two vanilla AINBs (never edits them): the lifetime AI plus a fire-field spawner.

Graph (while the shootable's state is not Sleep):
    Simultaneous
      1. timer: ExecuteGenericVFRCounter >= <frames>  ->  OneShotShootableRequestSleep   (otherwise success)
      2. spawner: QueryPhysicsCastRayEntity from Actor.Pos along Actor.Forward for <distance> metres
            IsHit ?  ->  QueryShooterHasAnySleepingPrepareableActors ?
                              ->  ExecuteShooterPrepareAndShootInShapeFormation (ActorName OnHitGroundExplosion, Center = the ray's HitPos)
In every other state the S32 selector's default branch only reports success.

Sources: Drake_Burst_Beam_Small_Fire.root.ainb (the selector, timer, sleep and the shoot node; the vanilla burst beam shoots its fire field with
exactly this node) and Dungeonboss_Goron.QueryPhysicsRayCastCheckGroundPos.module.ainb (the three QueryPhysicsOutputLayerEntity layer queries and the
cast node, with their layer masks).

usage: make_beam_ainb2.py <Drake_Burst_Beam_Small_Fire.root.ainb> <Goron ray cast module ainb> <out.ainb> <frames> <root name> [distance]
"""
import copy
import sys
import uuid

import ainb

drake_path, goron_path, out, frames, name = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]), sys.argv[5]
DISTANCE = float(sys.argv[6]) if len(sys.argv) > 6 else 300.0

drake = ainb.AINB.from_file(drake_path).as_dict()
goron = ainb.AINB.from_file(goron_path).as_dict()
dn = {n["Node Index"]: n for n in drake["Nodes"]}
gn = {n["Node Index"]: n for n in goron["Nodes"]}

# ---- the nodes, in their new order; each is (key, node dict) ----------------------------------------------------------------------
nodes = []


def add(key, node, new_guid=False):
    node = copy.deepcopy(node)
    if new_guid:
        node["GUID"] = str(uuid.uuid4())
    node["Flags"] = [f for f in node["Flags"] if f != "Is Root Node"]
    nodes.append((key, node))


add("state", dn[0])                 # QueryShootableGetState
add("selector", dn[1])              # S32 selector on the state (root)
nodes[-1][1]["Flags"].append("Is Root Node")
add("ok_default", dn[2])            # success
add("sim", dn[4])                   # Simultaneous
add("timer_sel", dn[5])             # BoolSelector on the counter
add("timer_ok", dn[6])              # success (timer not elapsed)
add("counter", dn[8])               # ExecuteGenericVFRCounter
add("sleep", dn[11])                # OneShotShootableRequestSleep
add("hit_sel", dn[15], new_guid=True)   # BoolSelector: IsHit (a copy of the HasSleeping selector's shape)
add("hit_no", dn[16], new_guid=True)    # success (no hit)
add("free_sel", dn[15])             # BoolSelector: a shooter slot is free
add("free_q", dn[14])               # QueryShooterHasAnySleepingPrepareableActors
add("free_no", dn[16])              # success (no free slot)
add("shoot", dn[9])                 # ExecuteShooterPrepareAndShootInShapeFormation
add("layer0", gn[0])                # QueryPhysicsOutputLayerEntity (8)
add("layer1", gn[1])                # (9)
add("layer2", gn[2])                # (10)
add("cast", gn[3])                  # QueryPhysicsCastRayEntity
add("endpos", gn[4])                # Element_Expression: CheckPos + UpVec * CheckDistance

index = {key: i for i, (key, _) in enumerate(nodes)}
node = {key: n for key, n in nodes}
for key, n in nodes:
    n["Node Index"] = index[key]

# The file-level expressions: 0 is the timer comparison (from the Drake file, threshold = frames); 1 and 2 are Actor.Pos and Actor.Forward.
EXPR_TIMER, EXPR_POS, EXPR_FWD = 0, 1, 2
exprs = []
timer = copy.deepcopy(drake["Expressions"]["Expressions"][0])
timer["Main"] = [l.replace("10.0", repr(frames)) for l in timer["Main"]]
assert any(repr(frames) in l for l in timer["Main"]), "timer threshold not found in the expression"
exprs.append(timer)
for i, func in ((EXPR_POS, "Actor.Pos"), (EXPR_FWD, "Actor.Forward")):
    exprs.append({"Expression Index": i, "Input Type": "IMM", "Output Type": "VECTOR3F",
                  "Main": [f"0x0000    CFN {func}( Vector3F ), GMem[0x0]", "0x0008    STR vec3f Out[0x0], vec3f GMem[0x0]", "0x0010    END"]})


# 3: the vanilla "start + direction * distance" expression of the Goron ray module, which its Element_Expression node refers to by index
EXPR_RAY = 3
ray = copy.deepcopy(goron["Expressions"]["Expressions"][0])
ray["Expression Index"] = EXPR_RAY
exprs.append(ray)
next(p for p in node["endpos"]["Properties"]["Int"] if p["Name"] == "ExpressionBase")["Expression Index"] = EXPR_RAY


def child(key, name_, **extra):
    c = {"Node Index": index[key], "Name": name_}
    c.update(extra)
    return c


def link(param, source_key, output_index=0):
    param["Node Index"] = index[source_key]
    param["Output Index"] = output_index
    param.pop("Expression Index", None)
    param["Flags"] = [f for f in param.get("Flags", []) if f not in ("Uses Default", "Pulse TLS")]


def expr(param, expression):
    param["Node Index"] = -1
    param["Output Index"] = 0
    param["Expression Index"] = expression
    param["Flags"] = [f for f in param.get("Flags", []) if f not in ("Uses Default", "Pulse TLS")]


def param(n, kind, pname, group="Inputs"):
    return next(p for p in n["Parameters"][group][kind] if p["Name"] == pname)


# ---- wiring ----------------------------------------------------------------------------------------------------------------------------
# S32 selector: any state but Sleep (Prepare 1, Shoot 2, Stationary 3) runs the Simultaneous node.
sel = node["selector"]
sel["Queries"] = [index["state"]]
sel["Plugs"]["Child"] = [child("sim", "", Condition=s) for s in (1, 2, 3)] + [child("ok_default", "", **{"Is Default": True})]
link(param(sel, "Int", "Input"), "state")

node["sim"]["Plugs"]["Child"] = [child("timer_sel", ""), child("hit_sel", "")]

# timer branch (as in make_beam_ainb.py)
node["timer_sel"]["Queries"] = [index["counter"]]
node["timer_sel"]["Plugs"] = {"Generic": [child("counter", "Input", **{"Unknown 1": 0, "Unknown 2": 0})],
                              "Child": [child("sleep", "True"), child("timer_ok", "False")]}
param(node["timer_sel"], "Bool", "Input")["Expression Index"] = EXPR_TIMER
param(node["timer_sel"], "Bool", "Input")["Node Index"] = index["counter"]

# the ray: start = Actor.Pos, end = start + Actor.Forward * distance
cast = node["cast"]
cast["Queries"] = [index["layer0"], index["layer1"], index["layer2"], index["endpos"]]
for k, key in enumerate(("layer0", "layer1", "layer2")):
    cast["Parameters"]["Inputs"]["Int"][0]["Sources"][k]["Node Index"] = index[key]
    node[key]["Plugs"] = {"Generic": [child("cast", "LayerEntity")]}
expr(param(cast, "Vector3F", "StartPos"), EXPR_POS)
link(param(cast, "Vector3F", "EndPos"), "endpos")
cast["Plugs"] = {"Generic": [child("hit_sel", "IsHit")]}

end = node["endpos"]
end["Queries"] = []
expr(param(end, "Vector3F", "CheckPos"), EXPR_POS)
expr(param(end, "Vector3F", "UpVec"), EXPR_FWD)
p = param(end, "Float", "CheckDistance")
p["Node Index"] = -1
p["Output Index"] = 0
p["Default Value"] = DISTANCE
p["Flags"] = ["Uses Default"]
end["Plugs"] = {}

# spawner: IsHit ? (slot free ? shoot : ok) : ok
hit = node["hit_sel"]
hit["Queries"] = [index["cast"]]
hit["Plugs"] = {"Generic": [child("cast", "Input", **{"Unknown 1": 0, "Unknown 2": 0})],
                "Child": [child("free_sel", "True"), child("hit_no", "False")]}
hp = param(hit, "Bool", "Input")
hp["Node Index"] = index["cast"]
hp["Output Index"] = 0
hp.pop("Expression Index", None)
hp["Flags"] = []

free = node["free_sel"]
free["Queries"] = [index["free_q"]]
free["Plugs"] = {"Generic": [child("free_q", "Input", **{"Unknown 1": 0, "Unknown 2": 0})],
                 "Child": [child("shoot", "True"), child("free_no", "False")]}
param(free, "Bool", "Input")["Node Index"] = index["free_q"]

shoot = node["shoot"]
shoot["Queries"] = [index["cast"]]
link(param(shoot, "Vector3F", "Center"), "cast", 1)      # HitPos is the second Vector3F output (HitNormal, HitPos)

# ---- assemble ---------------------------------------------------------------------------------------------------------------------------
drake["Nodes"] = [n for _, n in nodes]
drake["Commands"][0]["Root Node Index"] = index["selector"]
drake["Filename"] = name
drake["Expressions"]["Expressions"] = exprs

# ---- structural checks: every index used must exist ---------------------------------------------------------------------------------
count = len(drake["Nodes"])
known_exprs = {e["Expression Index"] for e in exprs}
assert len(known_exprs) == len(exprs)
for n in drake["Nodes"]:
    for q in n.get("Queries", []):
        assert 0 <= q < count, (n["Node Index"], "query", q)
    for plugs in n.get("Plugs", {}).values():
        for pl in plugs:
            assert 0 <= pl["Node Index"] < count, (n["Node Index"], "plug", pl)
    for group in n["Parameters"]["Inputs"].values():
        for pr in group:
            for src in pr.get("Sources", []):
                assert 0 <= src["Node Index"] < count, (n["Node Index"], "source", src)
            if "Node Index" in pr:
                assert -1 <= pr["Node Index"] < count, (n["Node Index"], "param", pr["Name"], pr["Node Index"])
            if "Expression Index" in pr:
                assert pr["Expression Index"] in known_exprs, (n["Node Index"], "expression", pr)
    for group in n.get("Properties", {}).values():
        for pr in group:
            if "Expression Index" in pr:
                assert pr["Expression Index"] in known_exprs, (n["Node Index"], "expression", pr)
assert sum("Is Root Node" in n["Flags"] for n in drake["Nodes"]) == 1
guids = [n["GUID"] for n in drake["Nodes"]]
assert len(set(guids)) == len(guids), "duplicate node GUIDs"

a = ainb.AINB.from_dict(drake)
data = a.to_binary() if hasattr(a, "to_binary") else a.write()
open(out, "wb").write(bytes(data))
b = ainb.AINB.from_binary(open(out, "rb").read())
print("ok", len(b.as_dict()["Nodes"]), "nodes;", [n["Name"] or n["Node Type"] for n in b.as_dict()["Nodes"]])
