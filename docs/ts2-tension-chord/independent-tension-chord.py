"""Independent 1D equilibrium of the tension chord (BUG-TS-2 option 1, beta = 0.4): 300x500 C25/30, 2 diameter 16 at y = -204 and 2 at y = +204.
N in N, M in Nmm, strain dimensionless; tension positive (compression negative); bending with chi < 0, the bottom edge (y = -250) in tension.
Concrete in compression: design parabola-rectangle (fcd = 0.85 fck / 1.5). Two states (EN 1992-1-1 7.4.3(3)):
- uncracked (state I), while no concrete fibre exceeds eps_cr = fctm / Ecm: concrete Ecm eps in tension, bars at the strain of the plane;
- cracked (state II): concrete zero in tension; a tensile bar inside the effective tension area hc,eff = min[2.5 (h - d); (h - x)/3 (h when
  entirely tensile); h/2] from the tensile edge (EN 1992-1-1 7.3.2(3)), rho_eff = As,eff / (b hc,eff), is read at the equivalent strain
  eps* = eps_m / (1 - beta) up to (1 - beta) eps_sr and eps_m + beta eps_sr beyond, eps_sr = fctm (1 + Es/Ecm rho_eff) / rho_eff / Es.
Bars elastic perfectly plastic at fyd = 450 / 1.15; they subtract the concrete they displace (at the strain of the plane).
The uncracked state is solved first; if a fibre exceeds eps_cr, the cracked state with the effective areas of the previous pass until they do not
change. No GPC library or solver output is used for the expected values.
"""
import json
import math

fc = .85 * 25 / 1.5
ec = 22000 * (33 / 10) ** .3
fct = .30 * 25 ** (2 / 3)
cr = fct / ec
es, fyd, beta = 200000., 450 / 1.15, .4
bar_area = math.pi * 16 ** 2 / 4
levels = [-204., 204.]


def sigma(e, cracked):
    if e < -.0035: return 0.
    if e < -.002: return -fc
    if e <= 0: return fc * (1000 * e + 250000 * e * e)
    return 0. if cracked else ec * e


def F(e, cracked):
    if e < -.002: return F(-.002, cracked) - fc * (max(e, -.0035) + .002)
    if e <= 0: return fc * (500 * e * e + 250000 * e ** 3 / 3)
    return 0. if cracked else ec * e * e / 2


def G(e, cracked):
    if e < -.002: return G(-.002, cracked) - fc * (max(e, -.0035) ** 2 - .002 ** 2) / 2
    if e <= 0: return fc * (1000 * e ** 3 / 3 + 250000 * e ** 4 / 4)
    return 0. if cracked else ec * e ** 3 / 3


def equivalent(e, rho):
    if rho is None or e <= 0: return e
    sr = fct * (1 + es / ec * rho) / rho / es
    return e / (1 - beta) if e <= (1 - beta) * sr else e + beta * sr


def ratios(e, k):
    """rho_eff of each bar level (None: bare), from the tensile edge of the plane."""
    tensile = [y for y in levels if e + k * y > 0]
    if not tensile or k == 0: return {y: None for y in levels}
    q = -1 if k < 0 else 1                        # Q = q y grows towards the tension
    top = 250.
    edge = e + k * (q * 250)                      # strain at the tensile edge
    other = e - k * (q * 250)
    entirely = other > 0
    centroid = sum(q * y for y in tensile) / len(tensile)
    hc = min(2.5 * (top - centroid), 500. if entirely else edge / abs(k) / 3, 250.)
    effective = [y for y in tensile if q * y >= top - hc - 1e-8]
    rho = len(effective) * 2 * bar_area / (300 * hc) if effective else None
    out = {y: (rho if y in effective else None) for y in levels}
    if entirely:                                  # the opposite face, h/2 (EN 7.3.2(3), section in tension)
        centroid2 = sum(-q * y for y in tensile) / len(tensile)
        hc2 = min(2.5 * (top - centroid2), 500., 250.)
        effective2 = [y for y in tensile if -q * y >= top - hc2 - 1e-8 and out[y] is None]
        if effective2:
            rho2 = len(effective2) * 2 * bar_area / (300 * hc2)
            for y in effective2: out[y] = rho2
    return out


def integrate(e, k, rho, cracked):
    if k == 0:
        n, m = 300 * 500 * sigma(e, cracked), 0.
    else:
        lo, hi = e - 250 * k, e + 250 * k
        df, dg = F(hi, cracked) - F(lo, cracked), G(hi, cracked) - G(lo, cracked)
        n, m = 300 * df / k, -300 * (dg - e * df) / k ** 2
    for y in levels:
        strain = e + k * y
        steel = max(-fyd, min(fyd, es * (equivalent(strain, rho[y]) if cracked else strain)))
        force = 2 * bar_area * (steel - sigma(strain, cracked))
        n += force; m -= force * y
    return n, m


def newton(n, m, e, k, rho, cracked):
    """Newton with a backtracking line search."""
    def norm(rn, rm): return abs(rn) + abs(rm) / 250
    for _ in range(200):
        vn, vm = integrate(e, k, rho, cracked)
        rn, rm = n * 1000 - vn, m * 1e6 - vm
        if abs(rn) < 1e-6 and abs(rm) < 1e-3: break
        dn, dm = integrate(e + 1e-9, k, rho, cracked); kn, km = integrate(e, k + 1e-11, rho, cracked)
        a, b, c, d = (dn - vn) / 1e-9, (kn - vn) / 1e-11, (dm - vm) / 1e-9, (km - vm) / 1e-11
        det = a * d - b * c
        if det == 0: e += 1e-8; continue
        de, dk = (d * rn - b * rm) / det, (-c * rn + a * rm) / det
        alpha, current = 1., norm(rn, rm)
        while alpha > 1e-6:
            tn, tm = integrate(e + alpha * de, k + alpha * dk, rho, cracked)
            if norm(n * 1000 - tn, m * 1e6 - tm) < current: break
            alpha /= 2
        e += alpha * de; k += alpha * dk
    return e, k


def solve(n, m, e, k):
    bare = {y: None for y in levels}
    e, k = newton(n, m, e, k, bare, False)
    if max(e - 250 * k, e + 250 * k) <= cr: return e, k, bare, False
    rho = ratios(e, k)
    for _ in range(12):
        e, k = newton(n, m, e, k, rho, True)
        new = ratios(e, k)
        if new == rho: break
        rho = new
    return e, k, rho, True


rows = []
for n, m, e, k in [(0, 20, -1.5e-5, -3e-7), (0, 45, -.7e-5, -9.5e-7), (-300, 60, -1.55e-4, -1.2e-6), (0, 55, -1e-4, -3e-6)]:
    e, k, rho, cracked = solve(n, m, e, k)
    eb, et, bar = e - 250 * k, e + 250 * k, e - 204 * k
    vn, vm = integrate(e, k, rho, cracked)
    rows.append(dict(N=n, M=m, Cracked=cracked, Chi=abs(k), EpsB=eb, EpsT=et, EpsBar=bar, RhoBar=rho[-204.],
                     SigmaBar=max(-fyd, min(fyd, es * (equivalent(bar, rho[-204.]) if cracked else bar))), SigmaB=sigma(eb, cracked),
                     SigmaT=sigma(et, cracked), ResidualN=vn - n * 1000, ResidualM=vm - m * 1e6))
# First yield at N = 0 (fib Model Code 2010 7.6.4.4): with the tension chord the bar at the crack reaches fyd / Es (eps* = eps_yd) while its mean
# strain, the strain of the plane, is lower; with the bars bare the strain of the plane reaches it. Bisection on M between 45 and 68 kNm.
def first_yield(chord):
    bare = {y: None for y in levels}
    def bar_strain(m):
        if chord:
            e, k, rho, cracked = solve(0, m, -1e-4, -3e-6)
        else:
            (e, k), rho, cracked = newton(0, m, -1e-4, -3e-6, bare, True), bare, True
        mean = e - 204 * k
        return (equivalent(mean, rho[-204.]) if cracked else mean), mean, abs(k)
    lo, hi = 45., 68.
    for _ in range(60):
        mid = (lo + hi) / 2
        if bar_strain(mid)[0] >= fyd / es: hi = mid
        else: lo = mid
    at_crack, mean, chi = bar_strain((lo + hi) / 2)
    return dict(FirstYield='tension chord' if chord else 'bare bars', N=0, My=(lo + hi) / 2, Chi=chi, EpsBarAtCrack=at_crack, EpsBarMean=mean)


rows += [first_yield(True), first_yield(False)]
# Uniform tension N = +200 kN, below the cracking load fct (Ac + (alpha_e - 1) As) of the tie: uncracked, eps = N / (Ec (Ac - As) + Es As).
steel = 4 * bar_area
rows.append(dict(N=200, M=0, CrackingLoad=fct * (150000 + (es / ec - 1) * steel), YieldLoad=fyd * steel,
                 Strain=200000 / (ec * (150000 - steel) + es * steel)))
if __name__ == '__main__': print(json.dumps(rows, indent=2))
