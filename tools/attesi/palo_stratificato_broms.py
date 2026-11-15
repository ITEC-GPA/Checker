"""Expected values of the stratified/Broms comparison (option C of the ANTHEA decision sheet of 16/11/2026, library part).

Independent closed forms for PileEdgeCaseTests.StratifiedResultExposesBromsForShortAndIntermediateGranularPiles (GPCChecker.Test.Geotechnics).
Homogeneous dry sand, phi' = 30 deg (Kp = 3), gamma = 18 kN/m3, D = 0.6 m, L = 4 m: limit reaction p = 3 Kp D gamma' z = k z.
Broms (Viggiani 13.2.4): free head short H = 0.5 gamma D L^3 Kp/(L + e); restrained head short H = 1.5 gamma D L^2 Kp;
restrained head intermediate H = (My + 0.5 gamma D L^3 Kp)/L.
Stratified closure (article ANTHEA supporto/docs/articolo-palo-orizzontale-stratificato.md): free head short, with zeta = zr/L and eps = e/L,
2 zeta^3 + 3 eps zeta^2 - 1.5 eps - 1 = 0 and H = k L^2 (zeta^2 - 1/2); restrained head short as Broms; restrained head intermediate
zr^3 = (3 My/k + L^3)/2 and H = k (zr^2 - L^2/2).
Units N, mm. Run: python -I tools/attesi/palo_stratificato_broms.py
"""

KP, GAMMA, D, L = 3.0, 18e-6, 600.0, 4000.0
k = 3 * KP * D * GAMMA  # N/mm2


def bisect(f, lo, hi):
    for _ in range(200):
        mid = (lo + hi) / 2
        if (f(lo) < 0) == (f(mid) < 0):
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def free_short(e):
    eps = e / L
    zeta = bisect(lambda z: 2 * z**3 + 3 * eps * z**2 - 1.5 * eps - 1, 2**-0.5, 1.0)
    stratified = k * L**2 * (zeta**2 - 0.5)
    broms = 0.5 * GAMMA * D * L**3 * KP / (L + e)
    return zeta, stratified, broms


for e in (0.0, 1000.0):
    zeta, s, b = free_short(e)
    print(f"free head e = {e:.0f} mm: zeta = {zeta:.12f}, H stratified = {s:.6f} N, H Broms = {b:.6f} N, ratio = {s / b:.12f}")

broms_fixed = 1.5 * GAMMA * D * L**2 * KP
print(f"restrained head short: H = {broms_fixed:.6f} N (both closures)")

MY = 1e9
zr = ((3 * MY / k + L**3) / 2) ** (1 / 3)
s = k * (zr**2 - L**2 / 2)
b = (MY + 0.5 * GAMMA * D * L**3 * KP) / L
print(f"restrained head intermediate My = {MY:.0f} N mm: zr = {zr:.6f} mm, H stratified = {s:.6f} N, H Broms = {b:.6f} N, ratio = {s / b:.12f}")
# Long candidate of the restrained head (2 My at the head and at zero shear): Peak(h) = S(z(h)) with z = sqrt(2 h/k), S = k z^3/3.
h_long = k * (6 * MY / k) ** (2 / 3) / 2
print(f"long candidate (restrained head): H = {h_long:.6f} N > intermediate")
