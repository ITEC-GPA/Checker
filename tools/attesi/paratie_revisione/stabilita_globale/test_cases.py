"""Expected values for the proposed unit tests (global stability, anchors, embedment).
Run: py -I test_cases.py   (sibling modules are loaded explicitly from this trusted scratch folder)
- circle_check.py : independent fine-slice Bishop (100k-400k midpoint slices), not a port.
- port_check.py   : line-by-line port of SlopeStability.Slices/BishopSolver.Solve, to predict what the C# returns today.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import circle_check as ind
import port_check as port

R = 10000.0
CIRCLE = (0.0, 2000.0, R, -math.sqrt(1e8 - 1.6e7), math.sqrt(1e8 - 4e6))
L1 = {k: 1.0 for k in ("RightSurcharge", "LeftSurcharge", "StructureDistributed", "PointLoad-0")}
F0 = dict(soil=1.0, body=1.0, tphi=1.25, c=1.25, cu=1.4, kh=0, kv=0, undrained=False)

print("== TC-G1..G4: one circle, EmbeddedWallGlobalTests.Model(), direction +1 ==")
print("circle X=0 Y=2000 R=10000 Left=%.9f Right=%.9f" % (CIRCLE[3], CIRCLE[4]))
cases = [("G1 dry, M2, loads x1.0", {}, None, dict(F0, loads=L1)),
         ("G2 dry, M2, loads x1.3", {"load_factor": 1.3}, None, dict(F0, loads={k: 1.3 for k in L1})),
         ("G3 water -2500, M2, x1.0", {"water": -2500.0}, -2500.0, dict(F0, loads=L1)),
         ("G4 undrained cu/1.4, x1.0", {"undrained": True}, None, dict(F0, loads=L1, undrained=True))]
for label, kw, water, f in cases:
    Fa, Da, fell = ind.solve(n=400000, **kw)
    s = port.prepare(water)
    row = [f"{label}: independent F={Fa:.6f} D={Da:.6f} N/mm (Fellenius {fell:.4f})"]
    for n in (20, 40, 200):
        Fp, Dp = port.bishop(port.slices(s, CIRCLE, f, n))
        row.append(f"port n={n}: F={Fp:.6f} ({(Fp/Fa-1)*100:+.3f}%) D={Dp:.6f}")
    print("\n   ".join(row))

print("\n== TC-G5 (finding): prescribed PorePressures, WaterElevation null -> code treats section as dry ==")
Fwet, _, _ = ind.solve(n=400000, water=-2500.0)
Fdry_code, _ = port.bishop(port.slices(port.prepare(None), CIRCLE, dict(F0, loads=L1), 40))
print(f"expected (u from the prescribed hydrostatic profile, level -2500 rel.): F={Fwet:.6f}, eta=1.1/F={1.1/Fwet:.4f}")
print(f"current code (Water empty): F={Fdry_code:.6f}, eta={1.1/Fdry_code:.4f}; overestimate x{Fdry_code/Fwet:.3f}")

print("\n== TC-G6: distributed interval loads (global-distributed-expected.json), independent integrals ==")
for name, top, bot, qt, qb, w in [("ramp", 0, -6000, 0, 2, 1500), ("balanced", 0, -6000, -2, 2, 1500), ("partial", -1000, -4500, 1, 4, 2000)]:
    Lq = top - bot
    H = (qt + qb) * Lq / (2 * w)
    # first moment int y q(y)/w dy, q linear from qb at bot to qt at top (Simpson exact for quadratic)
    f = lambda y: y * (qb + (qt - qb) * (y - bot) / Lq) / w
    fm = Lq / 6 * (f(bot) + 4 * f((top + bot) / 2) + f(top))
    couple = (qb - qt) * Lq * Lq / (12 * w)
    ymid = (top + bot) / 2
    drive = (H * 2000 - fm) / R
    print(f"{name}: H={H} first_moment={fm} code couple={couple} H*Ymid-couple={H*ymid-couple} driving(yc=2000,R=1e4)={drive}")

print("\n== TC-A: anchors ==")
seg1 = math.pi * 200 * 1.5 * 2000 * 0.25; seg2 = math.pi * 200 * 1.8 * 4000 * 0.18
mean = (seg1 + seg2 + 1e6) / 2; rak = min(mean / 1.75, 1e6 / 1.70)
print(f"A1 segments {seg1!r} {seg2!r}; A2 Rak(n=2)={rak!r} Rad perm={rak/1.2!r} util(400kN)={400000/(rak/1.2)!r}")
m = (9e5 + 1.2e6 + 1.35e6) / 3
print(f"A3 measured n=3: Rak={min(m/1.3, 9e5/1.2)!r} Rad temp={min(m/1.3, 9e5/1.2)/1.1!r}")
print(f"A4 AL1 interpolation pl=1.25 MPa: qs={0.18 + (0.22-0.18)*(1.25-1)/(1.5-1)!r} MPa; SG2 pl=2.5: {0.20 + (0.30-0.20)*0.5!r}")
print(f"A5 single bulb 4 m SG1 pl=2, D=200, alpha=1.5: R={math.pi*200*1.5*4000*0.25!r}; Rd n=1 permanent={math.pi*200*1.5*4000*0.25/1.8/1.2!r}")

print("\n== TC-E: Hetenyi finite free-free beam, head load (embedment fixture) ==")
EI, k, H = 2e14, 2.0, 1e4
b = (k / (4 * EI)) ** 0.25
for Lw in (6000, 6500, 7000, 7500):
    x = b * Lw; s, c, sh, ch = math.sin(x), math.cos(x), math.sinh(x), math.cosh(x)
    y = 2 * H * b / k * (sh * ch - s * c) / (sh * sh - s * s)
    print(f"L={Lw}: u_head(H=10kN)={y:.10f} mm, u_head(H=15kN)={1.5*y:.10f} mm")
