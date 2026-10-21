"""Independent hand checks of the embedded-wall section verifications (N, mm, MPa)."""
import math
import numpy as np

FCK, FYK, ES, GC, GS, ACC = 30.0, 450.0, 200000.0, 1.5, 1.15, 0.85
FCD, FYD = ACC * FCK / GC, FYK / GS


def sigma_c(eps):  # compression positive strain/stress, parabola-rectangle
    e = np.clip(eps, 0, None)
    t = np.clip(e / 0.002, 0, 1)
    return FCD * (2 * t - t * t)


def sigma_s(eps):
    return np.clip(ES * eps, -FYD, FYD)


# ---------------------------------------------------------------- 1. RC strip
print("=== 1. Diaphragm strip 1000x400, c=40, back 6phi20, front 5phi16, C30/B450 ===")
B, H, C = 1000.0, 400.0, 40.0
As_back, d_back = 6 * math.pi * 20 ** 2 / 4, H - C - 10  # tension face (back)
As_front, y_front = 5 * math.pi * 16 ** 2 / 4, C + 8      # compression face (front)
N_ed = 100000.0  # compression positive here
ny = 40000
y = (np.arange(ny) + .5) * H / ny  # distance from front (compressed) face
dA = B * H / ny


def strip_nm(x, subtract=True):
    # epsilon(y) compression positive, eps_cu at front face y=0, neutral axis at x
    eps = 0.0035 * (x - y) / x
    # steel limit check
    eps_back = 0.0035 * (x - d_back) / x
    fc = sigma_c(eps)
    n = np.sum(fc) * dA
    m = np.sum(fc * (H / 2 - y)) * dA  # moment about mid-depth, positive = compression at front
    for area, yy in ((As_back, d_back), (As_front, y_front)):
        e = 0.0035 * (x - yy) / x
        s = sigma_s(e) - (sigma_c(e) if subtract else 0)
        n += area * s
        m += area * s * (H / 2 - yy)
    return n, m, eps_back


for subtract in (True, False):
    lo, hi = 1.0, H
    for _ in range(80):
        mid = (lo + hi) / 2
        if strip_nm(mid, subtract)[0] < N_ed:
            lo = mid
        else:
            hi = mid
    n, m, eb = strip_nm((lo + hi) / 2, subtract)
    print(f"  subtract displaced concrete={subtract}: x={(lo+hi)/2:.2f} mm, N={n:.1f}, MRd={m/1e6:.3f} kNm, eps_s back={-eb:.4f} (<0.075 ok)")
# simplified stress block check
x_sb = (As_back * FYD - As_front * FYD + N_ed) / (0.8 * B * FCD)  # assume both yield (check)
print(f"  stress block x={x_sb:.1f} mm (front bars eps={0.0035*(x_sb-y_front)/x_sb:.5f})")

# NTC shear without links
d = d_back
k = min(2, 1 + math.sqrt(200 / d))
rho = min(0.02, As_back / (B * d))
scp = min(N_ed / (B * H), 0.2 * FCD)
v1 = (0.18 / GC * k * (100 * rho * FCK) ** (1 / 3) + 0.15 * scp) * B * d
v2 = (0.035 * k ** 1.5 * math.sqrt(FCK) + 0.15 * scp) * B * d
print(f"  shear: d={d}, k={k:.4f}, rho={rho:.5f}, scp={scp:.3f}, VRd1={v1/1e3:.2f} kN, Vmin={v2/1e3:.2f} kN, VRd={max(v1,v2)/1e3:.2f} kN")

# ---------------------------------------------------------------- 2. Circular RC pile
print("\n=== 2. Circular pile D=800, 12phi20, c=50, link phi10, C30/B450 ===")
D, R = 800.0, 400.0
rs = R - 50 - 10 - 10
ang = np.arange(12) * 2 * math.pi / 12
bar_y = rs * np.sin(ang)
abar = math.pi * 20 ** 2 / 4
nfib = 200000
yy = -R + (np.arange(nfib) + .5) * D / nfib


def widths(poly_n):
    if poly_n is None:
        return 2 * np.sqrt(np.clip(R * R - yy * yy, 0, None))
    v = [(R * math.cos(i * 2 * math.pi / poly_n), R * math.sin(i * 2 * math.pi / poly_n)) for i in range(poly_n)]
    left, right = np.full(nfib, np.inf), np.full(nfib, -np.inf)
    for (xa, ya), (xb, yb) in zip(v, v[1:] + v[:1]):
        if abs(yb - ya) < 1e-12:
            continue
        sel = (yy >= min(ya, yb)) & (yy < max(ya, yb))
        x = xa + (yy[sel] - ya) * (xb - xa) / (yb - ya)
        left[sel] = np.minimum(left[sel], x)
        right[sel] = np.maximum(right[sel], x)
    return right - left


def pile_mrd(nc, poly_n):  # nc compression positive; compression at top y=+R
    w = widths(poly_n) * D / nfib

    def nm(x):
        eps = 0.0035 * (yy - (R - x)) / x
        e_s = 0.0035 * (bar_y - (R - x)) / x
        fc = sigma_c(eps)
        fs = sigma_s(e_s) - sigma_c(e_s)
        return np.sum(fc * w) + abar * np.sum(fs), np.sum(fc * w * yy) + abar * np.sum(fs * bar_y)
    lo, hi = 1.0, D
    for _ in range(80):
        mid = (lo + hi) / 2
        if nm(mid)[0] < nc:
            lo = mid
        else:
            hi = mid
    return nm((lo + hi) / 2)[1], (lo + hi) / 2


for nc, fixture in ((-250000.0, 392784422.512768), (0.0, 461314867.0936418), (500000.0, 578997029.9906068)):
    m64, x64 = pile_mrd(nc, 64)
    mc, xc = pile_mrd(nc, None)
    print(f"  N={nc/1e3:7.0f} kN (comp +): 64-gon MRd={m64/1e6:.3f} kNm (fixture {fixture/1e6:.3f}, diff {m64/fixture-1:+.2e}); exact circle {mc/1e6:.3f} kNm (+{mc/m64-1:.2%})")

# d used by PileReinforcement: D/2 + min(centroid of bars with y<0, y>0) computed with C#-like double arithmetic
teta = 2.0 * math.pi / 12
ys = [rs * math.sin(teta * i) for i in range(12)]
print("  bar y (Polygon2d convention):", [f"{v:.3g}" for v in ys])
upper = [v for v in ys if v > 0]; lower = [v for v in ys if v < 0]
d_up = R + sum(upper) / len(upper); d_lo = R - sum(lower) / len(lower)
print(f"  bars y>0: {len(upper)}, y<0: {len(lower)}, centroid-based d: upper half {d_up:.1f}, lower half {d_lo:.1f} -> code d=min={min(d_up,d_lo):.1f}")
print(f"  continuous ring d=R+2rs/pi={R+2*rs/math.pi:.1f}; 0.45D+0.64(D/2-c_bar)={0.45*D+0.64*rs:.1f}")
# NTC shear with links (code: bw=D, Asw=2 legs, z=0.9d, theta auto in [1,2.5])
for dd, label in ((min(d_up, d_lo), 'code d'), (max(d_up, d_lo), 'd if 180deg bar on other side')):
    asw, s = 2 * math.pi * 10 ** 2 / 4, 150.0
    for bw, bl in ((D, 'bw=D'), (0.9 * D, 'bw=0.9D')):
        cot = min(max(math.sqrt(max(0, 0.5 * FCD * bw / (asw / s * FYD) - 1)), 1), 2.5)
        z = 0.9 * dd
        vrs = z * asw / s * FYD * cot
        vrc = z * bw * 0.5 * FCD * cot / (1 + cot * cot)
        print(f"  {label} {dd:.1f}, {bl}: cot={cot:.3f}, VRsd={vrs/1e3:.1f} kN, VRcd={vrc/1e3:.1f} kN, VRd={min(vrs,vrc)/1e3:.1f} kN")
Dp = D - 2 * 50 - 10
print(f"  Ang/Priestley hoops Vs=(pi/2)Ah fy D'/s cot (cot=2.5): {(math.pi/2)*78.54*FYD*Dp/150*2.5/1e3:.1f} kN")

# ---------------------------------------------------------------- 3. CHS 200x8 S355
print("\n=== 3. CHS 200x8 S355 (micropile / tube) ===")
Do, t, fy = 200.0, 8.0, 355.0
Ro, Ri = Do / 2, Do / 2 - t
A = math.pi * (Ro ** 2 - Ri ** 2)
f = fy / 1.05
Wpl = (Do ** 3 - (Do - 2 * t) ** 3) / 6
print(f"  A={A:.3f}, Wpl={Wpl:.1f}, Mpl={Wpl*f/1e6:.4f} kNm, Npl={A*f/1e3:.2f} kN, Vpl(2A/pi)={2*A/math.pi*f/math.sqrt(3)/1e3:.3f} kN")
print(f"  class: D/t={Do/t:.1f}, 50eps2={50*235/fy:.1f} -> class {'1' if Do/t <= 50*235/fy else '2+'}")
# independent fibre integration of N-M for the annulus
nr, nth = 400, 4000
r = Ri + (np.arange(nr) + .5) * t / nr
th = (np.arange(nth) + .5) * 2 * math.pi / nth
RR, TT = np.meshgrid(r, th)
Yf = (RR * np.sin(TT)).ravel(); dAf = (RR * (t / nr) * (2 * math.pi / nth)).ravel()


def chs_nm(h, fred):
    s = np.where(Yf > h, -fred, fred)  # +: one sign below h
    return abs(np.sum(s * dAf)), abs(np.sum(s * dAf * Yf))


for h, Nfix, Mfix, red in ((80.0, 1024209.7026631703, 55100447.2570435, 0.0), (80.0, 768157.2769973776, 41325335.442782626, 0.25), (20.0, 217980.50335000487, 97578188.33502328, 0.0)):
    n, m = chs_nm(h, f * (1 - red))
    print(f"  axis h={h}: fibre N={n:.0f} (fixture {Nfix:.0f}), M={m:.0f} (fixture {Mfix:.0f}, diff {m/Mfix-1:+.1e})")
# EC3 6.2.6/6.2.9 approximate interaction for CHS: M_N = Mpl (1 - n^1.7)
n_ = 1024209.7026631703 / (A * f)
print(f"  EC3 approx M_N=Mpl(1-n^1.7) for n={n_:.3f}: {Wpl*f*(1-n_**1.7)/1e6:.3f} kNm vs exact plastic 55.100 kNm")

# ---------------------------------------------------------------- 4. strut CHS 355.6x12.5 S355 hot L=6000
print("\n=== 4. Strut CHS 355.6x12.5 S355 hot-finished, L0=6000/8000 ===")
Do, t = 355.6, 12.5
A = math.pi / 4 * (Do ** 2 - (Do - 2 * t) ** 2); I = math.pi / 64 * (Do ** 4 - (Do - 2 * t) ** 4)
for L in (6000.0, 8000.0):
    Ncr = math.pi ** 2 * 210000 * I / L ** 2
    lam = math.sqrt(A * 355 / Ncr)
    phi = 0.5 * (1 + 0.21 * (lam - 0.2) + lam ** 2)
    chi = min(1, 1 / (phi + math.sqrt(phi ** 2 - lam ** 2)))
    print(f"  L0={L}: Ncr={Ncr/1e3:.1f} kN, lambda={lam:.4f}, chi={chi:.4f}, Nb,Rd={chi*A*355/1.05/1e3:.2f} kN")
