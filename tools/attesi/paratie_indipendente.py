"""Independent staged Winkler solver for embedded walls, written for the October 2026 review of EmbeddedWallAnalysis.

It shares only the documented model (docs/metodi/paratie.md) with the C# engine:
p = clamp(p0 + kh (v - vp), pa, pp) per side, Rankine limits, stage fields blended linearly over
20 sub-steps, supports F = clamp(P0 + k (e - ep), lo, cap). The implementation is separate:
- exact Hermite beam stiffness, soil springs lumped at nodes (trapezoidal tributary length);
- its own vertical stress and limit computation;
- Newton directions with an Armijo line search on the potential energy (convex, C1), whose
  differences are summed term by term; the step is a descent direction for every tangent used;
- unilateral supports keep their slack point (gap): only capacity yielding moves the offset.
Two meshes (50 and 25 mm) give a Richardson value 2 f25 - f50 for the first-order lumping error.
A limit-equilibrium check (free earth support) of case B is included as an existence argument.

Units N, mm, MPa, N/mm3. Run without arguments to print; --write refuses to replace the fixture.
"""
import json
import math
import sys
from pathlib import Path

import numpy as np

GW = 9.81e-6


class Layer:
    def __init__(s, top, bot, g, gsat, phi_deg, c, kh, k0, undrained=False, cu=0.0):
        s.top, s.bot, s.g, s.gsat, s.phi, s.c = top, bot, g, gsat, math.radians(phi_deg), c
        s.kh, s.k0, s.und, s.cu = kh, k0, undrained, cu


def limits(layers, ground, z):
    """(kh, p0, pa, pp, water) at elevation z; undrained layers use total stresses and water = 0."""
    zw = ground.get("water")
    u = GW * max(0.0, zw - z) if zw is not None else 0.0
    if z >= ground["elev"]:
        return 0.0, 0.0, 0.0, 0.0, u
    sv = ground["q"] + (GW * max(0.0, zw - ground["elev"]) if zw is not None else 0.0)
    for L in layers:
        top, bot = min(L.top, ground["elev"]), max(L.bot, z)
        if top <= bot:
            continue
        wet = max(0.0, min(top, zw) - bot) if zw is not None else 0.0
        sv += (top - bot - wet) * L.g + wet * L.gsat
    L = next(L for L in layers if L.bot <= z <= L.top)
    se = sv - u
    if L.und:
        return L.kh, L.k0 * se + u, max(0.0, sv - 2 * L.cu), sv + 2 * L.cu, 0.0
    ka = math.tan(math.pi / 4 - L.phi / 2) ** 2
    kp = 1 / ka
    return L.kh, L.k0 * se, max(0.0, ka * se - 2 * L.c * math.sqrt(ka)), kp * se + 2 * L.c * math.sqrt(kp), u


def hermite_k(ei, h):
    return ei / h ** 3 * np.array([[12, 6 * h, -12, 6 * h], [6 * h, 4 * h * h, -6 * h, 2 * h * h],
                                   [-12, -6 * h, 12, -6 * h], [6 * h, 2 * h * h, -6 * h, 4 * h * h]])


def G(s, p0, k, lo, hi):
    """Antiderivative of clamp(p0 + k s, lo, hi): the spring energy (convex, C1)."""
    sa = (lo - p0) / k
    sp = (hi - p0) / k if math.isfinite(hi) else math.inf
    sc = min(max(s, sa), sp)
    return p0 * sc + .5 * k * sc * sc + lo * min(s - sa, 0.0) + (hi * max(s - sp, 0.0) if math.isfinite(hi) else 0.0)


def solve(case, h=50.0, nsub=20, gap=True):
    top, bot = case["top"], case["bot"]
    n_el = int(round((top - bot) / h))
    x = np.linspace(0, top - bot, n_el + 1)
    z = top - x
    nn = len(x)
    nd = 2 * nn
    K = np.zeros((nd, nd))
    for e in range(n_el):
        idx = [2 * e, 2 * e + 1, 2 * e + 2, 2 * e + 3]
        K[np.ix_(idx, idx)] += hermite_k(case["ei"], x[e + 1] - x[e])
    trib = np.zeros(nn)
    trib[:-1] += np.diff(x) / 2
    trib[1:] += np.diff(x) / 2
    b = case.get("width", 1000.0)
    fixed = [i for i, f in zip([0, 1, nd - 2, nd - 1], case.get("fix", [False] * 4)) if f]
    free = np.array([i for i in range(nd) if i not in fixed])
    supports = case.get("supports", [])
    node = {s["id"]: int(np.argmin(abs(z - s["elev"]))) for s in supports}
    u = np.zeros(nd)
    vp = np.zeros((nn, 2))
    act = np.zeros((nn, 2), bool)
    sref = {s["id"]: 0.0 for s in supports}
    splast = {s["id"]: 0.0 for s in supports}
    prev, out = None, []
    for st in case["stages"]:
        start = prev if prev is not None else dict(st, supports={}, loads=[])
        old = [[limits(case[side], start[side], zz) for zz in z] for side in ("left", "right")]
        new = [[limits(case[side], st[side], zz) for zz in z] for side in ("left", "right")]
        for s in supports:
            if s["id"] not in start["supports"] and s["id"] in st["supports"]:
                sref[s["id"]] = u[2 * node[s["id"]]]
                splast[s["id"]] = 0.0
        for k in range(1, nsub + 1):
            f = k / nsub
            ucom = u.copy()
            F = [np.array([[a * (1 - f) + c * f for a, c in zip(old[side][i], new[side][i])] for i in range(nn)]) for side in (0, 1)]

            def support_state(s):
                a = 1.0 if s["id"] in start["supports"] else 0.0
                c = 1.0 if s["id"] in st["supports"] else 0.0
                actv = a * (1 - f) + c * f
                lock = start["supports"].get(s["id"], 0.0) * (1 - f) + st["supports"].get(s["id"], 0.0) * f
                lim = actv * s.get("cap", math.inf)
                return actv, lock, lim, (-lim if s["kind"] == "Bilateral" else 0.0)

            def evaluate(uu):
                R = K @ uu
                T = K.copy()
                nvp, nact = vp.copy(), act.copy()
                press = np.zeros((nn, 2))
                for side, sign in ((0, 1), (1, -1)):
                    kk, p0, pa, pp, w = F[side].T
                    v = -sign * uu[0::2]
                    off = np.where(act[:, side], vp[:, side], -sign * ucom[0::2])
                    on = kk > 0
                    trial = p0 + kk * (v - off)
                    p = np.where(on, np.clip(trial, pa, pp), 0.0)
                    pl = on & ((trial < pa) | (trial > pp))
                    nvp[:, side] = np.where(on, np.where(pl, v - (p - p0) / np.where(on, kk, 1), off), 0.0)
                    nact[:, side] = on
                    R[0::2] -= sign * (p + w) * b * trib
                    T[0::2, 0::2] += np.diag(np.where(on & ~pl, kk, 0.0) * b * trib)
                    press[:, side] = p + w
                for loads, factor in ((start["loads"], 1 - f), (st["loads"], f)):
                    for elev, force in loads:
                        R[2 * int(np.argmin(abs(z - elev)))] -= factor * force
                forces, nsp = {}, dict(splast)
                for s in supports:
                    actv, lock, lim, lo = support_state(s)
                    if actv == 0:
                        nsp[s["id"]] = 0.0
                        continue
                    i = 2 * node[s["id"]]
                    cs = math.cos(s["incl"])
                    kax = actv * s["k"]
                    ext = s["dir"] * cs * (uu[i] - sref[s["id"]])
                    trial = lock + kax * (ext - splast[s["id"]])
                    force = max(lo, min(lim, trial))
                    if force != trial and not (gap and force == 0.0 and s["kind"] != "Bilateral"):
                        nsp[s["id"]] = ext - (force - lock) / kax
                    R[i] += s["dir"] * cs * force * b / s["spacing"]
                    if force == trial:
                        T[i, i] += kax * cs * cs * b / s["spacing"]
                    forces[s["id"]] = force / actv
                return R, T, nvp, nact, press, forces, nsp

            def denergy(uu, d, lam):
                dd = lam * d
                dE = dd @ K @ uu + .5 * dd @ K @ dd
                for side, sign in ((0, 1), (1, -1)):
                    kk, p0, pa, pp, w = F[side].T
                    v = -sign * uu[0::2]
                    dv = -sign * dd[0::2]
                    off = np.where(act[:, side], vp[:, side], -sign * ucom[0::2])
                    dE -= np.sum(sign * w * b * trib * dd[0::2])
                    for i in np.nonzero(kk > 0)[0]:
                        dE += b * trib[i] * (G(v[i] + dv[i] - off[i], p0[i], kk[i], pa[i], pp[i]) - G(v[i] - off[i], p0[i], kk[i], pa[i], pp[i]))
                for loads, factor in ((start["loads"], 1 - f), (st["loads"], f)):
                    for elev, force in loads:
                        dE -= factor * force * dd[2 * int(np.argmin(abs(z - elev)))]
                for s in supports:
                    actv, lock, lim, lo = support_state(s)
                    if actv == 0:
                        continue
                    i = 2 * node[s["id"]]
                    cs = math.cos(s["incl"])
                    ext = s["dir"] * cs * (uu[i] - sref[s["id"]]) - splast[s["id"]]
                    dE += b / s["spacing"] * (G(ext + s["dir"] * cs * dd[i], lock, actv * s["k"], lo, lim) - G(ext, lock, actv * s["k"], lo, lim))
                return dE

            Ke = K.copy()
            for side in (0, 1):
                Ke[0::2, 0::2] += np.diag(F[side][:, 0] * b * trib)
            for s in supports:
                Ke[2 * node[s["id"]], 2 * node[s["id"]]] += s["k"] * math.cos(s["incl"]) ** 2 * b / s["spacing"]
            uu, ok = ucom.copy(), False
            for _ in range(3000):
                R, T, *_ = evaluate(uu)
                if np.abs(R[free]).max() < 5e-2:
                    ok = True
                    break
                d = np.zeros(nd)
                try:
                    d[free] = np.linalg.solve(T[np.ix_(free, free)] + 1e-12 * np.diag(np.diag(Ke)[free]), -R[free])
                    if R @ d >= 0:
                        raise np.linalg.LinAlgError
                except np.linalg.LinAlgError:
                    d[free] = np.linalg.solve(Ke[np.ix_(free, free)], -R[free])
                slope, lam = R @ d, 1.0
                while lam > 1e-12 and denergy(uu, d, lam) > 1e-4 * lam * slope:
                    lam /= 2
                uu = uu + lam * d
            if not ok:
                raise RuntimeError(f"{st['id']}: no equilibrium at fraction {f}")
            _, _, vp, act, press, forces, splast = evaluate(uu)
            u = uu
        M = np.zeros(nn)
        for e in range(n_el):
            M[e] = -(hermite_k(case["ei"], x[e + 1] - x[e]) @ u[2 * e:2 * e + 4])[1]
        M[-1] = (hermite_k(case["ei"], x[-1] - x[-2]) @ u[nd - 4:])[3]
        i = int(np.argmax(abs(M)))
        out.append(dict(id=st["id"], top_displacement=float(u[0]), max_moment=float(M[i]), max_moment_elevation=float(z[i]),
                        support_forces={k: float(v) for k, v in forces.items()}))
        prev = st
    return out


SAND = dict(g=18e-6, gsat=20e-6, phi_deg=30, c=0.0)
CASES = {
    "cantilever_sand": dict(
        top=0, bot=-12000, ei=2e14,
        left=[Layer(0, -12000, kh=.01, k0=.5, **SAND)], right=[Layer(0, -12000, kh=.01, k0=.5, **SAND)],
        stages=[dict(id="S0", left=dict(elev=0, q=0), right=dict(elev=0, q=0), supports={}, loads=[]),
                dict(id="S1", left=dict(elev=0, q=0), right=dict(elev=-2000, q=0), supports={}, loads=[]),
                dict(id="S2", left=dict(elev=0, q=0), right=dict(elev=-4000, q=0), supports={}, loads=[])]),
    "anchored_water": dict(
        top=0, bot=-14000, ei=3e14,
        left=[Layer(0, -14000, kh=.015, k0=.5, **SAND)], right=[Layer(0, -14000, kh=.015, k0=.5, **SAND)],
        supports=[dict(id="T1", elev=-1500, kind="Anchor", k=20000, incl=math.radians(20), dir=1, spacing=2500)],
        stages=[dict(id="S0", left=dict(elev=0, water=-3000, q=.02), right=dict(elev=0, water=-3000, q=0), supports={}, loads=[]),
                dict(id="S1", left=dict(elev=0, water=-3000, q=.02), right=dict(elev=-2000, water=-3000, q=0), supports={}, loads=[]),
                dict(id="S2", left=dict(elev=0, water=-3000, q=.02), right=dict(elev=-2000, water=-3000, q=0), supports={"T1": 150000.0}, loads=[]),
                dict(id="S3", left=dict(elev=0, water=-3000, q=.02), right=dict(elev=-7000, water=-7000, q=0), supports={"T1": 150000.0}, loads=[])]),
    "cantilever_undrained": dict(
        top=0, bot=-10000, ei=2e14,
        left=[Layer(0, -10000, 19e-6, 19e-6, 0, 0, kh=.008, k0=.6, undrained=True, cu=.04)],
        right=[Layer(0, -10000, 19e-6, 19e-6, 0, 0, kh=.008, k0=.6, undrained=True, cu=.04)],
        stages=[dict(id="S0", left=dict(elev=0, q=.01), right=dict(elev=0, q=0), supports={}, loads=[]),
                dict(id="S1", left=dict(elev=0, q=.01), right=dict(elev=-3000, q=0), supports={}, loads=[])]),
}


def free_earth_ratio():
    """Case anchored_water, final stage: passive / driving moment about the anchor (kN, m), Ka = 1/3, Kp = 3."""
    zz = np.linspace(0, 14, 140001)
    gw = 9.81
    svl = np.where(zz < 3, 20 + 18 * zz, 74 + (20 - gw) * (zz - 3))
    drive = svl / 3 + gw * np.clip(zz - 3, 0, None) - gw * np.clip(zz - 7, 0, None)
    resist = np.where(zz > 7, 3 * (20 - gw) * (zz - 7), 0)
    return float(np.trapezoid(resist * (zz - 1.5), zz) / np.trapezoid(drive * (zz - 1.5), zz))


def main():
    cases = {}
    for name, case in CASES.items():
        coarse, fine = solve(case, 50.0), solve(case, 25.0)
        stages = []
        for a, c in zip(coarse, fine):
            def rich(key):
                return 2 * c[key] - a[key]
            stages.append(dict(id=c["id"], top_displacement=rich("top_displacement"), max_moment=rich("max_moment"),
                               max_moment_elevation=c["max_moment_elevation"],
                               support_forces={k: 2 * c["support_forces"][k] - a["support_forces"][k] for k in c["support_forces"]},
                               mesh50=a, mesh25=c))
        cases[name] = stages
    k_beam, k_strut, h = 3 * 2e14 / 6000 ** 3, 10000.0, 10000.0
    payload = dict(
        units="N, mm, Nmm; values on a 1000 mm strip",
        method="independent lumped-spring solver, meshes 50/25 mm, Richardson 2*f25 - f50; energy line search",
        tolerance="0.5 % relative on displacement, moment and support force (engine mesh 250 mm)",
        cases=cases,
        free_earth_support_ratio_anchored_water=free_earth_ratio(),
        unilateral_reload=dict(note="Cantilever L=6 m, EI=2e14, top support EA/L=10000 N/mm: away -10 kN, back +10 kN",
                               away_displacement=-h / k_beam, back_displacement=h / (k_beam + k_strut),
                               back_force=h * k_strut / (k_beam + k_strut)))
    text = json.dumps(payload, indent=2) + "\n"
    if "--write" in sys.argv:
        path = Path(__file__).resolve().parents[2] / "tests/EmbeddedWalls.Tests/independent-expected.json"
        with path.open("x", encoding="utf-8", newline="\n") as file:
            file.write(text)
    else:
        print(text)


if __name__ == "__main__":
    main()
