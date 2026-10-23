"""Independent SCI D.1 geometry/statics rerun, N and mm.

Standard Python only. No C# calls, fixture writes, or expectation regeneration.
The inter-fastener result is the stated longitudinal component model, not a
published whole-joint resistance or a complete plate-stability benchmark.
"""
from math import fsum, pi, sqrt

HEIGHT, THICKNESS, FY, E = 340.0, 10.0, 275.0, 210000.0
AREA = HEIGHT * THICKNESS
MODULUS = THICKNESS * HEIGHT ** 2 / 6
NW = -150000 * (428 * 8.5) / 8550
MW = -200e6 * (8.5 * 428 ** 3 / 12) / 294e6
V = -150000
J = 4 * 120 ** 2 + 6 * 42.5 ** 2

# Use explicit independent closed-form group statics, not production results.
bolts = []
for sign, centroid, columns in ((1, -112.5, (-155, -70)), (-1, 112.5, (70, 155))):
    group_moment = sign * (MW - centroid * V)
    for x in columns:
        for y in (-120, 0, 120):
            fx = (sign * NW / 6 - group_moment * y / J) / 2
            fy = (sign * V / 6 + group_moment * (x - centroid) / J) / 2
            bolts.append((x, y, fx, fy))

cuts = []
for x in (-205, -155, -70, 0, 70, 155, 205):
    for limit in ((0, 1) if x in (-155, -70, 70, 155) else (-1,)):
        included = [b for b in bolts if b[0] < x or (limit == 1 and b[0] == x)]
        n = fsum(b[2] for b in included)
        v = fsum(b[3] for b in included)
        m = fsum((b[0] - x) * b[3] - b[1] * b[2] for b in included)
        sigma = max(0.0, -n / AREA + abs(m) / MODULUS)
        cuts.append((x, limit, n, v, m, sigma))

critical = max((c for c in cuts if -205 < c[0] < 205), key=lambda c: c[5])
length = .6 * max(85, 2 * 70)
slenderness = length / (pi * THICKNESS / sqrt(12)) * sqrt(FY / E)
phi = .5 * (1 + .49 * (slenderness - .2) + slenderness ** 2)
chi = min(1.0, 1 / (phi + sqrt(phi ** 2 - slenderness ** 2)))

print("Each-cover cuts: x, columnLimit(-1/0/1), N, V, M, sigmaCompression")
for cut in cuts:
    print(*(f"{value:.15g}" for value in cut), sep=", ")
print("Critical:", critical)
print("A,W,Lcr,lambda,chi:", AREA, MODULUS, length, slenderness, chi)
print("chi*fy, utilization (gammaM1=1):", chi * FY, critical[5] / (chi * FY))
