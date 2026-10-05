"""Builds the Ganon beam actor's AI from a copy of a vanilla AINB (never edits the vanilla file).

The graph is: an S32 selector on the shootable's state (Sleep 0, Prepare 1, Shoot 2, Stationary 3). While the state is Shoot a
frame counter feeds a BoolSelector; once the counter passes `frames` its True branch runs OneShotShootableRequestSleep (the
shootable returns to its shooter's pool, which also fades its effects), otherwise the node reports success. In every other state
the selector's default branch just reports success. Every node is taken unchanged from Drake_Burst_Beam_Small_Fire.root.ainb.

usage: make_beam_ainb.py <vanilla Drake_Burst_Beam_Small_Fire.root.ainb> <out.ainb> <frames> <root name>
"""
import sys
import ainb

src, out, frames, name = sys.argv[1], sys.argv[2], float(sys.argv[3]), sys.argv[4]
d = ainb.AINB.from_file(src).as_dict()
nodes = {n["Node Index"]: n for n in d["Nodes"]}

# Done on the old indices, before they are remapped: the timer's True branch sleeps instead of killing, and the state selector
# runs the timer in state Shoot (2) rather than Stationary (3).
for c in nodes[5]["Plugs"]["Child"]:
    if c["Name"] == "True":
        c["Node Index"] = 11
for c in nodes[1]["Plugs"]["Child"]:
    if not c.get("Is Default"):
        c["Node Index"] = 5
        c["Condition"] = 2

# old index -> new index
keep = [0, 1, 2, 5, 8, 6, 11]  # state query, state selector(root), success, BoolSelector, VFRCounter, success, RequestSleep
remap = {old: new for new, old in enumerate(keep)}
new_nodes = []
for old in keep:
    n = nodes[old]
    n["Node Index"] = remap[old]
    n["Queries"] = [remap[q] for q in n.get("Queries", [])]
    for plugs in n.get("Plugs", {}).values():
        for p in plugs:
            if "Node Index" in p and p["Node Index"] >= 0:
                p["Node Index"] = remap[p["Node Index"]]
    for group in n["Parameters"]["Inputs"].values():
        for p in group:
            if p.get("Node Index", -1) >= 0:
                p["Node Index"] = remap[p["Node Index"]]
    new_nodes.append(n)



d["Nodes"] = new_nodes
d["Commands"][0]["Root Node Index"] = remap[1]
d["Filename"] = name
for e in d["Expressions"]["Expressions"]:
    if e["Expression Index"] == 0:
        e["Main"] = [l.replace("10.0", repr(frames)) for l in e["Main"]]
# the second expression (actor position) is no longer referenced
d["Expressions"]["Expressions"] = [e for e in d["Expressions"]["Expressions"] if e["Expression Index"] == 0]

a = ainb.AINB.from_dict(d)
data = a.to_binary() if hasattr(a, "to_binary") else a.write()
open(out, "wb").write(bytes(data))
b = ainb.AINB.from_binary(open(out, "rb").read())
print("ok", [n["Name"] or n["Node Type"] for n in b.as_dict()["Nodes"]])
