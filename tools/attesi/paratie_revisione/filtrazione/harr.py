"""Compare ported wall mesh/FE with Harr closed form for a sheet pile in a finite layer."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from wallfe import mesh, solve, head_at


def K(m):
    a, b = 1.0, math.sqrt(1 - m * m)
    for _ in range(60):
        a, b = (a + b) / 2, math.sqrt(a * b)
    return math.pi / (2 * a)


T = 10000.0; s = 5000.0; H = 4000.0
lam = math.sin(math.pi * s / (2 * T)); lamp = math.cos(math.pi * s / (2 * T))
q_harr = K(lamp) / (2 * K(lam))
ie_harr = math.pi / (4 * T * K(lam) * lam) * H
print('Harr s/T=%.2f  Q/kH=%.5f  exit gradient=%.5f  tip head=H/2' % (s / T, q_harr, ie_harr))
for step in (1000.0, 500.0, 250.0):
    t = 20.0
    nodes, tris, heads, faces = mesh(8 * T, 8 * T, -T, 0, -s, t, 0, 0, H, 0, step)
    h, r, Kmat = solve(nodes, tris, heads)
    fixed = list(heads)
    inflow = sum(r[k] for k in fixed if r[k] > 0)
    tip = head_at(nodes, tris, h, t / 2, -s)
    # exit gradient at right face just below the surface, from the first element
    hz = head_at(nodes, tris, h, t / 2, -step)
    print('step %5.0f  nodes %5d  Q/kH=%.5f  tip head/H=%.4f  mean i over first cell=%.5f' % (step, len(nodes), inflow / H, tip / H, hz / step))
