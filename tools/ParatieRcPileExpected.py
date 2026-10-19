"""Independent horizontal-fibre integration of a 64-sided RC pile.

No GPC imports. Compression positive; N, mm, MPa, Nmm. NTC2018 ordinary
C30 parabola-rectangle (epsc2=.002, epscu=.0035), B450 elastic-perfect plastic.
Concrete displaced by bars is subtracted. File is created exclusively.
"""
import argparse
import json
import math
from pathlib import Path
import numpy as np

D, COUNT, PHI, COVER, LINK = 800., 12, 20., 50., 10.
FCD, FYD, ES = .85 * 30 / 1.5, 450 / 1.15, 200000.
R = D / 2
BAR_R = R - COVER - LINK - PHI / 2
BAR_Y = BAR_R * np.sin(np.arange(COUNT) * 2 * math.pi / COUNT)
BAR_A = math.pi * PHI ** 2 / 4


def expected(divisions, samples):
    # Independent midpoint integration across polygon edges; refine separately.
    y = -R + (np.arange(samples) + .5) * D / samples
    vertices = [(R*math.cos(i*2*math.pi/divisions), R*math.sin(i*2*math.pi/divisions)) for i in range(divisions)]
    left, right = np.full(samples, np.inf), np.full(samples, -np.inf)
    for (xa, ya), (xb, yb) in zip(vertices, vertices[1:] + vertices[:1]):
        if abs(yb-ya) < 1e-12:
            continue
        selected = (y >= min(ya,yb)) & (y < max(ya,yb))
        x = xa + (y[selected]-ya)*(xb-xa)/(yb-ya)
        left[selected] = np.minimum(left[selected], x)
        right[selected] = np.maximum(right[selected], x)
    area = (right-left)*D/samples

    def stress(e):
        t = np.clip(e/.002, 0, 1)
        return FCD*(2*t-t*t)

    def nm(depth):
        ec = .0035*(y-R+depth)/depth
        es = .0035*(BAR_Y-R+depth)/depth
        fc = stress(ec)
        fs = np.clip(ES*es, -FYD, FYD)-stress(es)
        return float(np.sum(fc*area)+BAR_A*np.sum(fs)), float(np.sum(fc*area*y)+BAR_A*np.sum(fs*BAR_Y))

    rows = []
    for target in [-250000., 0., 500000.]:
        lo, hi = 1., D
        assert nm(lo)[0] < target < nm(hi)[0]
        for _ in range(64):
            mid = (lo+hi)/2
            if nm(mid)[0] < target:
                lo = mid
            else:
                hi = mid
        depth = (lo+hi)/2
        n, m = nm(depth)
        assert max(abs(.0035*(BAR_Y-R+depth)/depth)) < .075
        rows.append(dict(N=target, M=m, neutral_axis_depth=depth, residual=n-target))
    return rows


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    fine = expected(64, 131072)
    coarse = expected(64, 65536)
    for a, b in zip(fine, coarse):
        assert abs(a['M']/b['M']-1) < 1e-7
    ecm = 22000*(38/10)**.3
    data = dict(method=__doc__, diameter=D, count=COUNT, phi=PHI, cover=COVER, link=LINK,
                Ecm=ecm, I=math.pi*D**4/64, bar_radius=BAR_R, samples=131072,
                cases=fine, discretization128=expected(128, 131072))
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
    print(json.dumps(data, ensure_ascii=False, indent=2))
