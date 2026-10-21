"""Mesh sensitivity of TC-S2 (step 250 mm)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wallfe import mesh, solve, head_at
LG, RG, WB, TW, BOT, LH, RH, LE, RE = 0.0, -6000.0, -12000.0, 600.0, -30000.0, 0.0, -6000.0, 40000.0, 5000.0
for step in (250.0,):
    for noflow in (False, True):
        nodes, tris, heads, faces = mesh(LE, RE, BOT, LG, WB, TW, LG, RG, LH, RH, step)
        if noflow:
            heads = {i: v for i, v in heads.items() if not (nodes[i][0] == RE and nodes[i][1] < RG)}
        h, r, _ = solve(nodes, tris, heads)
        im = (head_at(nodes, tris, h, 300, WB) - head_at(nodes, tris, h, 300, RG)) / 6000
        print(step, 'no-flow' if noflow else 'code', len(nodes), solve.iterations, round(im, 5))
