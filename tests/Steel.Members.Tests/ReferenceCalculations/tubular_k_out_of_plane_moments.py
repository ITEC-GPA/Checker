"""Independent hand calculation of out-of-plane brace moments on CHS K joints (TubularJointKMomentTests).

EN 1993-1-8:2005 + AC:2009: Table 7.2 (axial: K gap/overlap chord face, punching), Table 7.5 (chord face
failure for Mop on K, N, T, X and Y joints; punching shear on K and N gap joints), eq. (7.3) with Mip = 0,
1.5(6) beta. GPC cautelative reading for K/N: beta = min(di/d0, (d1+d2)/(2 d0)) in the Mop formula.
No Checker import; units N, mm, MPa. Run: python -I tubular_k_out_of_plane_moments.py
"""
import json
import math

FY0, T0, D0, GM5 = 355.0, 8.0, 168.3, 1.0
A0 = math.pi / 4 * (D0 ** 2 - (D0 - 2 * T0) ** 2)
GAMMA = D0 / (2 * T0)


def kp_from_cut(n_less_compressed):
    sigma_p = -n_less_compressed / A0          # no chord moments in these cases
    np_ = sigma_p / FY0 / GM5
    return np_, (max(0.0, min(1.0, 1 - 0.3 * np_ * (1 + np_))) if np_ > 0 else 1.0)


def kg(g):
    return GAMMA ** 0.2 * (1 + 0.024 * GAMMA ** 1.2 / (1 + math.exp(0.5 * g / T0 - 1.33)))


def n1_rd(kgv, kp, d1, sin1):
    return kgv * kp * FY0 * T0 ** 2 / sin1 * (1.8 + 10.2 * d1 / D0) / GM5


def punching_axial(di, sin):
    return FY0 / math.sqrt(3) * T0 * math.pi * di * (1 + sin) / (2 * sin * sin) / GM5


def mop_face(di, sin, beta, kp):
    return FY0 * T0 ** 2 * di / sin * 2.7 / (1 - 0.81 * beta) * kp / GM5


def mop_punching(di, sin):
    return FY0 * T0 * di ** 2 / math.sqrt(3) * (3 + sin) / (4 * sin * sin) / GM5


def case(name, da, db, na, nb, mopa, mopb, g, angle=45.0):
    s = math.sin(math.radians(angle))
    np_, kp = kp_from_cut(-400000.0 if g >= 0 else -350000.0)
    kgv = kg(g)
    first_d = da if na <= 0 else db                     # i = 1 is the compression brace (1.5(4))
    n1 = n1_rd(kgv, kp, first_d, s)
    n2 = s / s * n1
    mean_beta = (da + db) / (2 * D0)
    out = {"np": np_, "kp": kp, "kg": kgv, "N1Rd": n1, "N2Rd": n2, "MeanBeta": mean_beta}
    for key, d, n, mop in (("BraceA", da, na, mopa), ("BraceB", db, nb, mopb)):
        gap = g >= 0
        punch_n = punching_axial(d, s) if gap and d <= D0 - 2 * T0 else None
        nrd = min(n1 if n <= 0 else n2, punch_n if punch_n is not None else math.inf)
        if mop == 0:
            continue
        beta = min(d / D0, mean_beta)
        face = mop_face(d, s, beta, kp)
        punch_m = mop_punching(d, s) if gap and d <= D0 - 2 * T0 else None
        mrd = min(face, punch_m if punch_m is not None else math.inf)
        out[key] = {"beta": beta, "MopFace": face, "MopPunching": punch_m, "MopRd": mrd, "NRd": nrd,
                    "PunchingAxial": punch_n, "Interaction": abs(n) / nrd + abs(mop) / mrd}
    return name, out


def general(name, d0, t0, da, db, ang_a, ang_b, na, nb, mopa, mopb, g, n_less_compressed):
    """Any CHS K/N joint: i = 1 is the compression brace; N2,Rd = sin(theta1)/sin(theta2) N1,Rd (Table 7.2)."""
    a0 = math.pi / 4 * (d0 ** 2 - (d0 - 2 * t0) ** 2); gamma = d0 / (2 * t0)
    np_ = -n_less_compressed / a0 / FY0 / GM5
    kp = max(0.0, min(1.0, 1 - 0.3 * np_ * (1 + np_))) if np_ > 0 else 1.0
    kgv = gamma ** 0.2 * (1 + 0.024 * gamma ** 1.2 / (1 + math.exp(0.5 * g / t0 - 1.33)))
    sa, sb = math.sin(math.radians(ang_a)), math.sin(math.radians(ang_b))
    first_a = na <= 0
    d1, s1 = (da, sa) if first_a else (db, sb)
    n1 = kgv * kp * FY0 * t0 ** 2 / s1 * (1.8 + 10.2 * d1 / d0) / GM5
    mean_beta = (da + db) / (2 * d0)
    out = {"np": np_, "kp": kp, "kg": kgv, "N1Rd": n1, "MeanBeta": mean_beta}
    for key, d, s, n, mop in (("BraceA", da, sa, na, mopa), ("BraceB", db, sb, nb, mopb)):
        is_first = (key == "BraceA") == first_a
        nrd_face = n1 if is_first else s1 / s * n1
        punch_n = FY0 / math.sqrt(3) * t0 * math.pi * d * (1 + s) / (2 * s * s) / GM5 if g >= 0 and d <= d0 - 2 * t0 else None
        nrd = min(nrd_face, punch_n if punch_n is not None else math.inf)
        beta = min(d / d0, mean_beta)
        face = FY0 * t0 ** 2 * d / s * 2.7 / (1 - 0.81 * beta) * kp / GM5
        punch_m = FY0 * t0 * d ** 2 / math.sqrt(3) * (3 + s) / (4 * s * s) / GM5 if g >= 0 and d <= d0 - 2 * t0 else None
        mrd = min(face, punch_m if punch_m is not None else math.inf)
        out[key] = {"NRdFace": nrd_face, "PunchingAxial": punch_n, "NRd": nrd, "beta": beta, "MopFace": face,
                    "MopPunching": punch_m, "MopRd": mrd, "Interaction": abs(n) / nrd + abs(mop) / mrd if mop else None}
    return name, out


if __name__ == "__main__":
    extra = dict([
        # Review B8 (16/11): N joint, chord 168.3x8, braces 88.9x5, gap 15; A at 90 deg -150 kN, B at 45 deg +212.13 kN,
        # cuts -400 / -550 kN, Mop = 1 kNm on both (reviewer: eq.(7.3) A 0.635330, B 0.600905).
        general("NGap", 168.3, 8.0, 88.9, 88.9, 90.0, 45.0, -150000.0, 150000.0 / math.sin(math.pi / 4), 1e6, 1e6, 15.0, -400000.0),
        # Punching governs Mop: chord 168.3x16 (d0/t0 = 10.52), braces 42.4x4 at 45/45, -50/+50 kN, gap 20, Mop,A = 0.5 kNm,
        # chord cut A -100 kN.
        general("KPunchingGoverns", 168.3, 16.0, 42.4, 42.4, 45.0, 45.0, -50000.0, 50000.0, 0.5e6, 0.0, 20.0, -100000.0),
    ])
    res = dict([
        case("KGapEqualBraces", 88.9, 88.9, -200000.0, 200000.0, 1.5e6, 1.0e6, 20.0),
        case("KGapUnequalBraces", 88.9, 76.1, -200000.0, 200000.0, 1.2e6, 0.8e6, 20.0),
        case("KOverlap50", 88.9, 88.9, -180000.0, 180000.0, 1.0e6, 0.0, -0.5 * 88.9 / math.sin(math.pi / 4)),
    ])
    res.update(extra)
    print(json.dumps(res, indent=2))
