"""Excavation-side lateral boundary: EmbeddedWallSeepageMesh always fixes h=RightHead on x=RightExtent
(EmbeddedWallSeepageMesh.cs:49). For a symmetric excavation of width B the axis x=B/2 is a no-flow line.
Compare toe excess head, Terzaghi-prism uplift and mean gradient for the two boundary types.
Units: mm, N, MPa. Saturated, isotropic (k cancels)."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from wallfe import mesh, solve, head_at, GW

LG, RG, WB, T, BOT = 0.0, -6000.0, -12000.0, 600.0, -30000.0
LH, RH = 0.0, -6000.0
LE = 40000.0
STEP = 500.0
half = T / 2
D = RG - WB          # embedment below excavation, 6000
b = D / 2            # Terzaghi prism width
GSAT = 20e-6


def run(RE, noflow):
    nodes, tris, heads, faces = mesh(LE, RE, BOT, LG, WB, T, LG, RG, LH, RH, STEP)
    if noflow:
        heads = {k: v for k, v in heads.items() if not (nodes[k][0] == RE and nodes[k][1] < RG)}
    h, r, K = solve(nodes, tris, heads)
    q = sum(r[k] for k in heads if r[k] > 0)
    toe = head_at(nodes, tris, h, half, WB) - RH
    xs = np.linspace(half, half + b, 61)
    ex = np.array([head_at(nodes, tris, h, x, WB) - RH for x in xs])
    avg = np.trapezoid(ex, xs) / b
    u = np.array([GW * (head_at(nodes, tris, h, x, WB) - WB) for x in xs])
    U = np.trapezoid(u, xs) * 1000.0   # N per 1000 mm of wall
    W = GSAT * D * b * 1000.0          # total prism weight, N
    im = (head_at(nodes, tris, h, half, WB) - head_at(nodes, tris, h, half, RG)) / D
    return q, toe, avg, U, W, im


ic = (GSAT - GW) / GW
print('i_c = %.4f; NTC HYD mean limit ic/3 = %.4f' % (ic, ic / 3))
for label, RE, nf in [('code BC, RightExtent=5 m (B=10 m)', 5000.0, False),
                      ('no-flow axis, RightExtent=5 m', 5000.0, True),
                      ('code BC, RightExtent=40 m', 40000.0, False),
                      ('code BC, RightExtent=4 m (B=8 m)', 4000.0, False),
                      ('no-flow axis, RightExtent=4 m', 4000.0, True)]:
    q, toe, avg, U, W, im = run(RE, nf)
    upl = 1.1 * U / (0.9 * W)
    fs_terz = (GSAT - GW) * D / (GW * avg)
    print('%-38s q/kH=%.4f toe excess=%.0f mm prism avg excess=%.0f mm  U=%.0f N W=%.0f N  UPL util=%.3f  FS_Terz=%.2f  i_mean=%.4f util_mean=%.3f'
          % (label, q / (LH - RH), toe, avg, U, W, upl, fs_terz, im, im / (ic / 3)))
