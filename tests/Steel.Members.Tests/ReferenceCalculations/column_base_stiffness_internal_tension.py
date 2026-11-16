"""Independent hand calculation for ColumnBaseStiffnessAndInternalTensionTests (15/11/2026).

EN 1993-1-8:2005 + AC:2009: 6.2.5 (fjd, c), 6.2.6.3 (column web in tension, Table 6.3 omega1), 6.2.6.5/6.2.6.11
(T-stub, Table 6.2 modes), Table 6.6 (outer row, first row below the flange with alpha), 6.2.8.3 / Table 6.7
(two-side equilibrium), 6.3.1(6) and Table 6.8 (mu, psi = 2.7), Table 6.11 (k13 with c = 1.25 tp, k15, k16),
6.3.4 and Table 6.12 (Sj); Table 3.4 combined shear and tension; EN 1992-1-1 6.7 (FRdu).
SCI P398 Appendix G closed form of Figure 6.11, inverted numerically (an explicit, documented alternative).
The rotational stiffness is cross-checked with an independent two-spring model (rigid plate, M/phi at fixed e).
No Checker import. Units N, mm, MPa. Run: python -I column_base_stiffness_internal_tension.py
"""
import json
import math

E = 210000.0


def hole_area_and_moment_inside(y_low, y_high, yc, r):
    """Disk of radius r centred at (x inside, yc) clipped by y_low <= y <= y_high (x fully inside): numeric strips."""
    n = 200000
    a = max(y_low, yc - r); b = min(y_high, yc + r)
    if a >= b:
        return 0.0, 0.0
    if a == yc - r and b == yc + r:
        return math.pi * r * r, math.pi * r * r * yc        # whole disk inside the strip
    h = (b - a) / n; area = 0.0; mom = 0.0
    for i in range(n):
        y = a + (i + 0.5) * h
        w = 2 * math.sqrt(max(0.0, r * r - (y - yc) ** 2))
        area += w * h; mom += w * h * y
    return area, mom


def compression_side(bp, hp, tp, fyp, gm0, fck, acc, gc, ac1_factor, hc, bc, tf, holes_y, d0, n_holes_per_row=2):
    ac0 = bp * hp
    fcd = acc * fck / gc
    ratio = min(math.sqrt(ac1_factor), 3.0)
    fjd = 2.0 / 3.0 * ac0 * fcd * ratio / ac0
    c = tp * math.sqrt(fyp / (3 * fjd * gm0))
    out = min(c, (hp - hc) / 2); inw = min(c, (hc - 2 * tf) / 2); half_x = min(bc / 2 + c, bp / 2)
    y_low, y_high = hc / 2 - tf - inw, hc / 2 + out
    ag = 2 * half_x * (y_high - y_low); sg = ag * (y_low + y_high) / 2
    area, mom = ag, sg
    for yh in holes_y:
        ah, sh = hole_area_and_moment_inside(y_low, y_high, yh, d0 / 2)
        area -= n_holes_per_row * ah; mom -= n_holes_per_row * sh
    return {"fjd": fjd, "c": c, "Aeff": area, "zC": mom / area, "FcPl": fjd * area, "yLow": y_low, "yHigh": y_high, "halfX": half_x}


def sci_alpha(m, e, m2):
    l1, l2 = m / (m + e), m2 / (m + e)

    def curve(alpha):
        one = 1.25 / (alpha - 2.75); two = alpha * one / 2
        return one if l2 >= two else one + (1 - one) * ((two - l2) / two) ** (0.185 * alpha ** 1.785)
    lo, hi = 4.45, 8.0
    for _ in range(200):
        mid = (lo + hi) / 2
        if curve(mid) > l1: lo = mid
        else: hi = mid
    return (lo + hi) / 2, l1, l2


def tstub(leff1, leff2, m, n, tp, fyp, gm0, sum_ft, prying):
    mpl1 = 0.25 * leff1 * tp * tp * fyp / gm0; mpl2 = 0.25 * leff2 * tp * tp * fyp / gm0
    plate = 2 * mpl1 / m                                  # 6.2.6.11(2): no prying relied upon for the plate
    mode2 = (2 * mpl2 + n * sum_ft) / (m + n) if prying else None
    mode3 = sum_ft
    return {"Mpl1": mpl1, "Plate": plate, "Mode2": mode2, "Mode3": mode3, "FT": min(plate, mode3, mode2 if mode2 is not None else math.inf)}


def equilibrium(n_ed, m_ed, zt, zc, ft, fc):
    best = None
    for name, at, bt in (("A tension B compression", True, False), ("both tension", True, True),
                         ("A compression B tension", False, True), ("both compression", False, False)):
        za = zt if at else zc; zb = zt if bt else zc
        fa = (n_ed * zb + m_ed) / (za + zb); fb = (n_ed * za - m_ed) / (za + zb)
        cons = min(fa if at else -fa, fb if bt else -fb)
        if best is None or cons > best[0]:
            best = (cons, name, at, bt, fa, fb, za + zb)
    _, name, at, bt, fa, fb, z = best
    u = max(abs(fa) / (ft if at else fc), abs(fb) / (ft if bt else fc))
    return {"state": name, "A_tension": at, "B_tension": bt, "FA": fa, "FB": fb, "z": z, "u": u, "MjRd": m_ed / u}


def stiffness(eq, zt, zc, k13, k15, k16, n_ed, m_ed):
    kt = 1 / (1 / k15 + 1 / k16)
    ka = kt if eq["A_tension"] else k13; kb = kt if eq["B_tension"] else k13
    za = zt if eq["A_tension"] else zc; zb = zt if eq["B_tension"] else zc; z = za + zb
    e = m_ed / n_ed if n_ed != 0 else math.inf
    ek = (zb * kb - za * ka) / (ka + kb)
    ratio = 1.0 if ek == 0 or math.isinf(e) else e / (e + ek)
    sj_ini = ratio * E * z * z / (1 / ka + 1 / kb)
    # Two-spring cross-check: deltaA = d0 + phi zA, deltaB = d0 - phi zB; FA = E kA deltaA, FB = E kB deltaB;
    # N = FA + FB, M = FA zA - FB zB. Solve the 2x2 system for phi with M = m_ed, N = n_ed.
    a11, a12 = E * ka + E * kb, E * ka * za - E * kb * zb
    a21, a22 = E * ka * za - E * kb * zb, E * ka * za * za + E * kb * zb * zb
    det = a11 * a22 - a12 * a21
    phi = (a11 * m_ed - a21 * n_ed) / det
    u = eq["u"] if m_ed != 0 else 0.0
    mu = 1.0 if u <= 2.0 / 3.0 else (1.5 * u) ** 2.7
    return {"k13": k13, "k15": k15, "k16": k16, "kT": kt, "z": z, "e": e, "ek": ek, "ratio": ratio,
            "SjIni": sj_ini, "SjIniTwoSpring": m_ed / phi if m_ed != 0 else None, "mu": mu, "Sj": sj_ini / mu}


def k13_of(ec, bp, hp, tp, hc, bc, tf):
    cs = 1.25 * tp
    beff = tf + min(cs, (hp - hc) / 2) + min(cs, (hc - 2 * tf) / 2)
    leff = 2 * min(bc / 2 + cs, bp / 2)
    return ec * math.sqrt(beff * leff) / (1.275 * E), beff, leff


def example1():
    # HEB240 S275, plate 400x440x30 S275, 4 M24 5.6 outside (gauge 160, row 170), EC factors, alpha_cc 1, gamma_c 1.5.
    comp = compression_side(400, 440, 30, 275, 1.0, 25, 1.0, 1.5, 4.0, 240, 240, 17, [], 26)
    # Partial holes at y = 170 (r 13) cut by the upper boundary 120 + c: analytic cap of the bearing audit.
    c = comp["c"]; r = 13.0; a = 120 + c - 170
    acap = r * r * math.acos(a / r) - a * math.sqrt(r * r - a * a)
    ah = math.pi * r * r - acap; sh = 170 * ah - 2.0 / 3.0 * (r * r - a * a) ** 1.5
    ag = 2 * (120 + c) * (17 + 2 * c); area = ag - 2 * ah; zc = (111.5 * ag - 2 * sh) / area
    comp.update({"Aeff": area, "zC": zc, "FcPl": comp["fjd"] * area})
    mx = 50 - 0.8 * 12; e = 120; ex = 220 - 170
    leff_nc = min(4 * mx + 1.25 * ex, e + 2 * mx + 0.625 * ex, 200, 80 + 2 * mx + 0.625 * ex)
    leff_cp = min(2 * math.pi * mx, math.pi * mx + 160, math.pi * mx + 2 * e)
    leff1 = min(leff_cp, leff_nc); n = min(ex, 1.25 * mx)
    lb = 8 * 24 + 30 + 30 + 4 + 21.5 / 2; lbs = 8.8 * mx ** 3 * 353 / (leff1 * 30 ** 3); prying = lb <= lbs
    anchor = min(0.9 * 500 * 353 / 1.25, 110000.0)
    ts = tstub(leff1, leff_nc, mx, n, 30, 275, 1.0, 2 * anchor, prying)
    fc = min(comp["FcPl"], 1053e3 * 275 / 1.0 / (240 - 17))
    eq = equilibrium(-600e3, 120e6, 170, zc, ts["FT"], fc)
    k13, beff, leff = k13_of(31000, 400, 440, 30, 240, 240, 17)
    k15 = (0.85 if prying else 0.425) * leff1 * 30 ** 3 / mx ** 3; k16 = (1.6 if prying else 2.0) * 353 / lb
    st = stiffness(eq, 170, zc, k13, k15, k16, -600e3, 120e6)
    # Shear with the Table 3.4 combined rule on the tensioned side (no prying): Ft,Ed = FA/2.
    f1 = 0.5 * 500 * 353 / 1.25; f2 = (0.44 - 0.0003 * 300) * 500 * 353 / 1.25; fv_steel = min(f1, f2)
    fvb = min(fv_steel, 60000.0)  # plate bearing is larger here (as in Example 1), concrete 60 kN declared
    ft_ed = eq["FA"] / 2; steel = 0.9 * 500 * 353 / 1.25
    per = min(fvb, max(0.0, fv_steel * (1 - ft_ed / (1.4 * steel))))
    shear = {"Ff": 0.2 * 600e3, "fvb": fvb, "FvWithoutInteraction": 0.2 * 600e3 + 2 * fvb,
             "AnchorTension": ft_ed, "TensionedAnchorShear": per, "FvWithInteraction": 0.2 * 600e3 + 2 * fvb + 2 * per}
    return {"compression": comp, "Lb": lb, "LbStar": lbs, "prying": prying, "leff1": leff1, "mx": mx, "tstub": ts,
            "equilibrium": eq, "k13_beff": beff, "k13_leff": leff, "stiffness": st, "shear": shear}


def example2():
    # NTC: gammaM0 1.05, gammaM2 1.25; plate 15 mm, 4 M20 8.8 outside (row 180), alpha_cc 0.85; N = +50 kN, M1 = 25 kNm.
    comp = compression_side(400, 440, 15, 275, 1.05, 25, 0.85, 1.5, 4.0, 240, 240, 17, [180.0], 22)
    mx = 60 - 0.8 * 12; e = 120; ex = 220 - 180
    leff_nc = min(4 * mx + 1.25 * ex, e + 2 * mx + 0.625 * ex, 200, 80 + 2 * mx + 0.625 * ex)
    leff_cp = min(2 * math.pi * mx, math.pi * mx + 160, math.pi * mx + 2 * e)
    leff1 = min(leff_cp, leff_nc); n = min(ex, 1.25 * mx)
    lb = 8 * 20 + 30 + 15 + 3 + 18 / 2; lbs = 8.8 * mx ** 3 * 245 / (leff1 * 15 ** 3); prying = lb <= lbs
    anchor = min(0.9 * 800 * 245 / 1.25, 90000.0)
    ts = tstub(leff1, leff_nc, mx, n, 15, 275, 1.05, 2 * anchor, prying)
    fc = min(comp["FcPl"], 1053e3 * 275 / 1.05 / (240 - 17))
    eq = equilibrium(50e3, 25e6, 180, comp["zC"], ts["FT"], fc)
    k13, beff, leff = k13_of(31000, 400, 440, 15, 240, 240, 17)
    k15 = (0.85 if prying else 0.425) * leff1 * 15 ** 3 / mx ** 3; k16 = (1.6 if prying else 2.0) * 245 / lb
    st = stiffness(eq, 180, comp["zC"], k13, k15, k16, 50e3, 25e6)
    return {"compression": comp, "Lb": lb, "LbStar": lbs, "prying": prying, "leff1": leff1, "tstub": ts, "equilibrium": eq, "stiffness": st}


def internal(alpha_declared=None, tp=30.0, d=24.0, as_=353.0, d0=26.0, fub=500.0, washer=4.0, nut=21.5, concrete=110000.0):
    # HEB500 S275 (h500 b300 tw14.5 tf28 r27), plate 300x600x30 S275, 4 M24 5.6 between the flanges: gauge 140,
    # rows at +/-180; welds flange 12, web 8; EC factors; concrete C25/30 Ac1 = 4 Ac0; N = +50 kN, M1 = 60 kNm.
    hc, bc, tw, tf, r = 500.0, 300.0, 14.5, 28.0, 27.0
    area_col = 2 * bc * tf + (hc - 2 * tf) * tw + 4 * (1 - math.pi / 4) * r * r
    comp = compression_side(300, 600, tp, 275, 1.0, 25, 1.0, 1.5, 4.0, hc, bc, tf, [180.0], d0)
    m = (140 - tw) / 2 - 0.8 * 8; e = (300 - 140) / 2; first_row = hc / 2 - tf - 180; m2 = first_row - 0.8 * 12
    if alpha_declared is None:
        alpha, l1, l2 = sci_alpha(m, e, m2)
    else:
        alpha, l1, l2 = alpha_declared, m / (m + e), m2 / (m + e)
    leff_cp = 2 * math.pi * m; leff_nc = alpha * m; leff1 = min(leff_cp, leff_nc); n = min(e, 1.25 * m)
    lb = 8 * d + 30 + tp + washer + nut / 2; lbs = 8.8 * m ** 3 * as_ / (leff1 * tp ** 3); prying = lb <= lbs
    steel = 0.9 * fub * as_ / 1.25
    anchor = min(steel, concrete)                         # plate punching under the nut is larger in these cases
    ts = tstub(leff1, leff_nc, m, n, tp, 275, 1.0, 2 * anchor, prying)
    avc =area_col - 2 * bc * tf + (tw + 2 * r) * tf
    x = leff1 * tw / avc; omega1 = 1 / math.sqrt(1 + 1.3 * x * x); ftwc = omega1 * leff1 * tw * 275 / 1.0
    ft = min(ts["FT"], ftwc)
    fc = min(comp["FcPl"], 4815e3 * 275 / 1.0 / (hc - tf))
    eq = equilibrium(50e3, 60e6, 180, comp["zC"], ft, fc)
    k13, beff, leff = k13_of(31000, 300, 600, tp, hc, bc, tf)
    k15 = (0.85 if prying else 0.425) * leff1 * tp ** 3 / m ** 3; k16 = (1.6 if prying else 2.0) * as_ / lb
    st = stiffness(eq, 180, comp["zC"], k13, k15, k16, 50e3, 60e6)
    return {"m": m, "e": e, "m2": m2, "lambda1": l1, "lambda2": l2, "alpha": alpha, "asymptote": 4 + 1.25 * e / m,
            "endPattern": 2 * m + 0.625 * e, "leff1": leff1, "leff2": leff_nc, "Lb": lb, "LbStar": lbs, "prying": prying,
            "AnchorSteel": steel, "compression": comp, "tstub": ts, "Avc": avc, "omega1": omega1, "FtWcRd": ftwc, "FT": ft, "equilibrium": eq, "stiffness": st}


if __name__ == "__main__":
    print(json.dumps({"Example1": example1(), "Example2": example2(), "InternalDeclaredAlpha6": internal(6.0),
                      "InternalSciAlpha": internal(),
                      # Review M1 (16/11): M36 10.9 (As 817, hole 39, washer 5, nut 29), plate 40, concrete 600 kN per anchor.
                      "InternalWebGoverns": internal(6.0, tp=40.0, d=36.0, as_=817.0, d0=39.0, fub=1000.0, washer=5.0, nut=29.0,
                                                     concrete=600000.0)}, indent=2))
