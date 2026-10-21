"""Expected values for proposed unit tests of the embedded-wall section checks.
Independent of GPC code: closed-form NTC 2018 formulas and own fibre integration. Units N, mm, MPa, Nmm.
Run: py -I test_cases.py
"""
import math
import numpy as np

FCK, FYK, ES, GC, GS, ACC = 30.0, 450.0, 200000.0, 1.5, 1.15, 0.85
FCD, FYD = ACC * FCK / GC, FYK / GS
ECM = 22000 * ((FCK + 8) / 10) ** 0.3


def sigma_c(eps):
    t = np.clip(np.clip(eps, 0, None) / 0.002, 0, 1)
    return FCD * (2 * t - t * t)


def sigma_s(eps):
    return np.clip(ES * eps, -FYD, FYD)


# ---------------------------------------------------------------------------
# Strip of EmbeddedWallConcreteTests.Section(): b=1000, h=400, clear cover 40,
# back (Concrete Top, y=350) 6phi20, front (Concrete Bottom, y=48) 5phi16, C30, B450, Es=200000.
B, H, C = 1000.0, 400.0, 40.0
BARS = {"back": (6 * math.pi * 400 / 4, H - C - 10.0), "front": (5 * math.pi * 256 / 4, C + 8.0)}
NY = 40000
Y = (np.arange(NY) + .5) * H / NY  # from the Concrete bottom (front) face
DA = B * H / NY


def strip_mrd(n_comp, tension_face, subtract=True):
    """MRd (positive) at assigned axial force n_comp (compression positive)."""
    # compressed edge: front (y=0) when back is in tension; back (y=H) when front is in tension
    def nm(x):
        depth = Y if tension_face == "back" else H - Y
        eps = 0.0035 * (x - depth) / x
        n = np.sum(sigma_c(eps)) * DA
        m = np.sum(sigma_c(eps) * (H / 2 - depth)) * DA
        for area, yb in BARS.values():
            db = yb if tension_face == "back" else H - yb
            e = 0.0035 * (x - db) / x
            s = sigma_s(e) - (sigma_c(e) if subtract else 0)
            n += area * s
            m += area * s * (H / 2 - db)
        return n, m
    lo, hi = 0.5, H
    for _ in range(90):
        mid = (lo + hi) / 2
        if nm(mid)[0] < n_comp:
            lo = mid
        else:
            hi = mid
    return nm((lo + hi) / 2)[1], (lo + hi) / 2


def ntc_vrd_nolinks(n_comp, d, asl, bw=B, ac=B * H):
    k = min(2, 1 + math.sqrt(200 / d)); rho = min(.02, asl / (bw * d)); scp = min(n_comp / ac, .2 * FCD)
    a = (.18 / GC * k * (100 * rho * FCK) ** (1 / 3) + .15 * scp) * bw * d
    b = (.035 * k ** 1.5 * math.sqrt(FCK) + .15 * scp) * bw * d
    return max(a, b), a, b


print("TC-RC1/RC2  strip MRd at N = -100 kN (Concrete sign), toe of the cantilever test")
for face in ("back", "front"):
    for sub in (True, False):
        m, x = strip_mrd(100000.0, face, sub)
        print(f"   {face:5s} in tension, subtract displaced concrete={sub}: MRd = {m:.6e} Nmm  (x = {x:.2f} mm)")
print("TC-RC3  strip shear without links, N = -100 kN")
for face, (asl, yb) in BARS.items():
    d = yb if face == "back" else H - yb
    vrd, a, b = ntc_vrd_nolinks(100000.0, d, asl)
    print(f"   tension {face}: d={d}, Asl={asl:.3f}: VRd={vrd:.6e} N (formula {a:.6e}, vmin {b:.6e})")
vrd0, a0, b0 = ntc_vrd_nolinks(0.0, 350.0, BARS['back'][0])
print(f"   N=0, tension back: VRd={vrd0:.6e} N")
vcap, _, _ = ntc_vrd_nolinks(1e9, 350.0, BARS['back'][0])
print(f"   sigma_cp cap 0.2 fcd = {0.2*FCD} MPa -> VRd(N huge)={vcap:.6e} N")

# ---------------------------------------------------------------------------
print("TC-RC4  strip SLS, linear cracked section, N=0, M_concrete = -100e6 (back in tension), creep phi")
def cracked(m, phi, comp_n_minus_1):
    n = ES / (ECM / (1 + phi))
    a_t, d = BARS["back"]; a_c, yc = BARS["front"]  # compression bars at depth 48 from compressed face
    dc = yc
    nc = (n - 1) if comp_n_minus_1 else n
    # x from first moment: b x^2/2 + nc a_c (x - dc) - n a_t (d - x) = 0
    aa, bb, cc = B / 2, nc * a_c + n * a_t, -(nc * a_c * dc + n * a_t * d)
    x = (-bb + math.sqrt(bb * bb - 4 * aa * cc)) / (2 * aa)
    i = B * x ** 3 / 3 + nc * a_c * (x - dc) ** 2 + n * a_t * (d - x) ** 2
    return x, m * x / i, n * m * (d - x) / i, n
for phi in (0.0, 2.0):
    for nm1 in (True, False):
        x, sc, ss, n = cracked(100e6, phi, nm1)
        print(f"   phi={phi}, n={n:.3f}, comp bars {'n-1' if nm1 else 'n'}: x={x:.2f} mm, sigma_c={sc:.4f} MPa, sigma_s={ss:.3f} MPa")
print(f"   limits: 0.60fck={0.6*FCK}, 0.45fck={0.45*FCK}, 0.80fyk={0.8*FYK} MPa")

# ---------------------------------------------------------------------------
print("TC-P1  circular pile D=800, 12phi20, cover 50, link 10/150: exact-N MRd (64-gon and true circle)")
D, R = 800.0, 400.0
RS = R - 50 - 10 - 10
TETA = 2.0 * math.pi / 12
BAR_Y = np.array([RS * math.sin(TETA * i) for i in range(12)])
ABAR = math.pi * 100
NF = 200000
YY = -R + (np.arange(NF) + .5) * D / NF


def widths(poly_n):
    if poly_n is None:
        return 2 * np.sqrt(np.clip(R * R - YY * YY, 0, None))
    v = [(R * math.cos(i * 2 * math.pi / poly_n), R * math.sin(i * 2 * math.pi / poly_n)) for i in range(poly_n)]
    left, right = np.full(NF, np.inf), np.full(NF, -np.inf)
    for (xa, ya), (xb, yb) in zip(v, v[1:] + v[:1]):
        if abs(yb - ya) < 1e-12:
            continue
        sel = (YY >= min(ya, yb)) & (YY < max(ya, yb))
        xx = xa + (YY[sel] - ya) * (xb - xa) / (yb - ya)
        left[sel] = np.minimum(left[sel], xx); right[sel] = np.maximum(right[sel], xx)
    return right - left


def pile_mrd(n_comp, poly_n):
    w = widths(poly_n) * D / NF
    def nm(x):
        eps = 0.0035 * (YY - (R - x)) / x; e_s = 0.0035 * (BAR_Y - (R - x)) / x
        fs = sigma_s(e_s) - sigma_c(e_s)
        return np.sum(sigma_c(eps) * w) + ABAR * np.sum(fs), np.sum(sigma_c(eps) * w * YY) + ABAR * np.sum(fs * BAR_Y)
    lo, hi = 1.0, D
    for _ in range(90):
        mid = (lo + hi) / 2
        if nm(mid)[0] < n_comp:
            lo = mid
        else:
            hi = mid
    return nm((lo + hi) / 2)[1]
for n in (0.0, 500000.0, -250000.0):
    print(f"   N(comp+)={n:9.0f}: 64-gon {pile_mrd(n, 64):.6e}  circle {pile_mrd(n, None):.6e} Nmm")

print("TC-P2  pile shear (NTC 4.1.28-29, links 2 legs phi10/150, z = 0.9 d, bw = D as coded)")
upper = BAR_Y[BAR_Y > 0]; lower = BAR_Y[BAR_Y < 0]
d_code = R + min(upper.mean(), -lower.mean()); d_strict = R + max(upper.mean(), -lower.mean())
print(f"   bar y at 180 deg = {BAR_Y[6]:.3e} (counted in y>0 half): n(y>0)={upper.size}, n(y<0)={lower.size}")
def pile_v(n_comp, d, bw, asw=2 * math.pi * 25, s=150.0, lever=.9):
    scp = max(0, n_comp / (math.pi * R * R))
    ac = 1 + scp / FCD if scp <= .25 * FCD else 1.25 if scp <= .5 * FCD else 2.5 * (1 - scp / FCD)
    cot = min(max(math.sqrt(max(0, .5 * FCD * bw * ac / (asw / s * FYD) - 1)), 1), 2.5)
    z = lever * d
    return min(z * asw / s * FYD * cot, z * bw * ac * .5 * FCD * cot / (1 + cot * cot)), cot
for d, lab in ((d_code, "code d"), (d_strict, "strict tension-half d")):
    for bw in (D, .9 * D):
        v, cot = pile_v(0.0, d, bw)
        print(f"   {lab}={d:.4f}, bw={bw:.0f}: VRd={v:.6e} N (cot={cot})")
v2, _ = pile_v(0.0, d_code, D, asw=2 * math.pi * 36, s=100.0)
v3, _ = pile_v(0.0, d_code, .9 * D, asw=2 * math.pi * 36, s=100.0)
print(f"   dense links phi12/100, code d: bw=D {v2:.6e} N, bw=0.9D {v3:.6e} N")

# ---------------------------------------------------------------------------
print("TC-T1  tube CHS 200x8 S355, end-to-end base of stage A in EmbeddedWallSteelTubeTests (b=1500, s=2000)")
Ro, Ri, fy = 100.0, 92.0, 355.0
A = math.pi * (Ro * Ro - Ri * Ri); f = fy / 1.05
def cap(a, h):
    return 0.0 if h >= a else a * a * (math.acos(h / a) - (h / a) * math.sqrt(1 - (h / a) ** 2))
def mrd(nabs, red=0.0):
    ff = f * (1 - red)
    if nabs >= A * ff:
        return 0.0
    lo, hi = 0.0, Ro
    for _ in range(200):
        mid = (lo + hi) / 2
        if ff * (A - 2 * (cap(Ro, mid) - cap(Ri, mid))) < nabs:
            lo = mid
        else:
            hi = mid
    h = (lo + hi) / 2
    return 4 / 3 * ff * ((Ro * Ro - h * h) ** 1.5 - max(0, Ri * Ri - h * h) ** 1.5)
fac = 2000 / 1500
N = 5000 * fac; V = 200 * fac; M = 200 * 6000 * fac
print(f"   actions per tube: N=-{N:.6f} (compression), V={V:.6f}, |M|={M:.6f}; MRd={mrd(N):.6e} Nmm, Vpl={2*A/math.pi*f/math.sqrt(3):.6e} N, Npl={A*f:.6e} N")
print(f"   Mpl (N=0) = {mrd(0):.6e}; check vs Wpl f = {(200**3-184**3)/6*f:.6e}")
