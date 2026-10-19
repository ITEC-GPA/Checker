"""Immutable independent cantilever expectations: exact rational integration of point-load Green functions.
Run before production changes. N, mm; x measured down from the free head, fixed toe x=L.
No FEM shape functions, stiffness matrix or production solver are used.
"""
from fractions import Fraction as F
from pathlib import Path
import json

L, EI = F(6000), F(200000000000000)


def mul(a, b):
    result = [F(0)] * (len(a) + len(b) - 1)
    for i, ai in enumerate(a):
        for j, bj in enumerate(b):
            result[i + j] += ai * bj
    return result


def integral(poly, a, b):
    return sum(c * (b ** (i + 1) - a ** (i + 1)) / (i + 1) for i, c in enumerate(poly)) if b > a else F(0)


def response(loads, x):
    v = m = u = rotation = F(0)
    y = L - x
    for a, b, qa, qb in loads:
        slope = (qb - qa) / (b - a)
        q = [qa - slope * a, slope]
        v += integral(q, a, min(x, b))
        m += integral(mul(q, [x, -1]), a, min(x, b))
        # Point-load Green function integrated over the loaded interval, split at the observation point.
        u += integral(mul(q, [y*y*(3*L-y), -3*y*y]), a, min(x, b)) / (6*EI)
        u += integral(mul(q, mul([L*L, -2*L, 1], [3*y-L, 1])), max(a, x), b) / (6*EI)
        rotation -= integral(mul(q, [y*(2*L-y), -2*y]), a, min(x, b)) / (2*EI)
        rotation -= integral(mul(q, [L*L, -2*L, 1]), max(a, x), b) / (2*EI)
    return {"depth": float(x), "shear": float(v), "moment": float(m), "displacement": float(u), "rotation": float(rotation)}


cases = {
    "uniform": [(0, 6000, 2, 2)],
    "ramp_down": [(0, 6000, 0, 2)],
    "ramp_up": [(0, 6000, 3, 0)],
    "partial": [(1000, 4500, 1, 4)],
    "overlap": [(500, 2500, 2, -1), (1500, 5500, F(-1, 2), 3)],
    "zero_resultant": [(0, 6000, -2, 2)],
}
output = {"method": "Exact rational integration of cantilever Green functions", "length": float(L), "EI": float(EI), "cases": []}
for name, raw in cases.items():
    loads = [tuple(F(n) for n in load) for load in raw]
    points = [response(loads, F(x)) for x in range(0, 6001, 500)]
    output["cases"].append({"name": name, "loads": [[float(v) for v in load] for load in loads], "points": points,
                            "reaction_force": -points[-1]["shear"], "reaction_moment": points[-1]["moment"]})
target = Path(__file__).resolve().parents[1] / "tests/EmbeddedWalls.Tests/distributed-expected.json"
with target.open("x", encoding="utf-8", newline="\n") as file:
    json.dump(output, file, indent=2, ensure_ascii=False)
    file.write("\n")
print(target)
