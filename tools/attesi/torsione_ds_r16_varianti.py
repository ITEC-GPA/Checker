"""R16, DS torsion with shear: strut interaction of the three readings on a grid (review of 15/11/2026, M1).

r = V/VRd,max with VRd,max from nu_v (5.103 NA), t = T/TRd,max with TRd,max from nu_t (5.104 NA, walls reinforced on the
outer face only). With the common factor a = fcd cot(theta)/(1 + cot^2(theta)) the wall shear stresses are
tau_V = r nu_v a and tau_T = t nu_t a, so every reading depends on r, t, nu_v and nu_t only:
- weighted mean of Concrete 0.28-0.37, nu = (nu_v tau_V + nu_t tau_T)/(tau_V + tau_T), interaction (t nu_t + r nu_v)/nu
  = (t nu_t + r nu_v)^2/(nu_v^2 r + nu_t^2 t);
- option (b), commit e6bbb7f8: nu = nu_t for both terms, interaction t + r nu_v/nu_t;
- variant (b2): each term with its own factor, interaction t + r. It equals (t nu_t + r nu_v)/nu with nu weighted on the
  two ratios, nu = (nu_v r + nu_t t)/(r + t).
Always mean <= (b2) <= (b): (b2) - mean = t r (nu_v - nu_t)^2/(nu_v^2 r + nu_t^2 t), (b) - (b2) = r (nu_v/nu_t - 1).
Run: python -I tools/attesi/torsione_ds_r16_varianti.py
"""


def factors(fck):
    return max(0.45, 0.7 - fck / 200), 0.7 * (0.7 - fck / 200)


def readings(r, t, nu_v, nu_t):
    mean = (t * nu_t + r * nu_v) ** 2 / (nu_v ** 2 * r + nu_t ** 2 * t)
    return mean, t + r * nu_v / nu_t, t + r


print("| fck | nu_v | nu_t | V/VRd,max | T/TRd,max | weighted mean (0.28-0.37) | (b) nu_t | (b2) separate terms | (b)/mean | (b2)/mean |")
print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
for fck in (30, 50, 90):
    nu_v, nu_t = factors(fck)
    for r in (0.25, 0.5, 0.75):
        for t in (0.01, 0.1, 0.5):
            mean, b, b2 = readings(r, t, nu_v, nu_t)
            assert mean <= b2 + 1e-15 <= b + 2e-15
            print(f"| {fck} | {nu_v:.3f} | {nu_t:.3f} | {r:.2f} | {t:.2f} | {mean:.3f} | {b:.3f} | {b2:.3f} | {b / mean:.3f} | {b2 / mean:.3f} |")

# Example of the review: fck 30, Ak 82 416 mm2, tef 96 mm, fcd 20 MPa, cot 1.5, V/VRd,max 0.75, T = 0.5 kNm (TRd,max = 56.2356 kNm with nu_t).
nu_v, nu_t = factors(30)
trd = 0.385 * 2 * 82416 * 96 * 20 * 1.5 / 3.25
t = 0.5e6 / trd
mean, b, b2 = readings(0.75, t, nu_v, nu_t)
print(f"review example: T/TRd,max = {t:.6f}; mean {mean:.4f}, (b) {b:.4f}, (b2) {b2:.4f}; shear alone 0.75")
for T in (1e-3, 1e3, 1e6):  # Nmm
    t = T / trd
    mean, b, b2 = readings(0.75, t, nu_v, nu_t)
    print(f"T = {T:g} Nmm: mean {mean:.6f}, (b) {b:.6f}, (b2) {b2:.6f}")
