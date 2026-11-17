"""R1 of the review G-207 (16/11/2026), branch claude/scheda-r16-nu-min: lower bound of the effectiveness factor of DK NA 5.6.1(3)P.

DS/EN 1992-1-1 DK NA:2024 (PDF of bygningsreglementet.dk, header "DK NA:2021 rev. 2024-01-09"), page 30: the factors of the clause assume at
least the minimum reinforcement; without it nu = 2/sqrt(fck) (5.100 NA), and "the value of (5.100 NA) is always a lower bound of nu". Page 31:
nu_v = 0.7 - fck/200, not less than 0.45 (5.103 NA); nu_t = 0.7 (0.7 - fck/200) (5.104 NA); weighted mean of nu_v and nu_t for shear and
torsion together; nu_t = nu_v for walls with closed links and longitudinal bars on both faces. Text extracted from the official PDF on 16/11/2026
(and read by the review G-207, section "Fonti").
With the bound: nu_t = max[0.7 (0.7 - fck/200); 2/sqrt(fck)], governed by the bound for fck < 24.46 MPa and fck > 73.22 MPa.
nu_v >= 2/sqrt(fck) already for fck >= 9.38 MPa (every class from C12/15).
Strut resistance of the torsion (SectionTorsionCalculator): TRcd = 2 Ak tef nu fcd cot(theta)/(1 + cot^2(theta)).
Geometry of TorsionMigrationTests (rectangle 300 x 500, Ak = 82 416 mm2, tef = 96 mm, cot(theta) = 1.5), walls reinforced on the outer face only.
Run: python -I tools/attesi/torsione_ds_r1_nu_min.py
"""
import math

AK, TEF, COT = 82416.0, 96.0, 1.5
GEOMETRY = 2 * AK * TEF * COT / (1 + COT * COT)  # mm3: TRcd = nu fcd GEOMETRY (Nmm)


def factors(fck):
    nu_v = max(0.45, 0.7 - fck / 200)
    nu_t_bare = 0.7 * (0.7 - fck / 200)
    return nu_v, nu_t_bare, max(nu_t_bare, 2 / math.sqrt(fck)), 2 / math.sqrt(fck)


print(f"2 Ak tef cot/(1 + cot^2) = {GEOMETRY:.6f} mm3")
print("| fck | 2/sqrt(fck) | nu_v | nu_t (5.104) | nu_t with 5.100 | governs |")
for fck in (12, 16, 20, 24, 25, 30, 50, 70, 80, 90):
    nu_v, bare, bounded, minimum = factors(fck)
    print(f"| {fck} | {minimum:.6f} | {nu_v:.6f} | {bare:.6f} | {bounded:.6f} | {'5.100' if bounded > bare else '5.104'} |")
    assert nu_v >= minimum

# Cases of the report: fcd = fck/1.5 (input of the example, not a partial factor of the DK NA), T = 0.9 TRcd before, V/VRd,max = 0.25 (b2).
print("cases (fcd = fck/1.5, T = 0.9 TRcd before, V/VRd,max(nu_v) = 0.25, variant b2: t + r):")
for fck in (20, 80, 90, 30):
    fcd = fck / 1.5
    nu_v, bare, bounded, minimum = factors(fck)
    before, after = bare * fcd * GEOMETRY, bounded * fcd * GEOMETRY
    t = 0.9 * before
    print(f"  fck {fck}: fcd {fcd:.6f} MPa, nu_t {bare:.6f} -> {bounded:.6f}, TRcd {before / 1e6:.6f} -> {after / 1e6:.6f} kNm"
          f" ({(after / before - 1) * 100:+.2f} %); T = {t / 1e6:.6f} kNm: T/TRcd {t / before:.6f} -> {t / after:.6f},"
          f" interaction {t / before + 0.25:.6f} -> {t / after + 0.25:.6f}")

# Test values (fcd 20 MPa as in the existing test, to isolate the factor): TRcd = nu_t 20 GEOMETRY.
print("test values with fcd = 20 MPa:")
for fck in (20, 30, 80, 90):
    nu_v, bare, bounded, minimum = factors(fck)
    print(f"  fck {fck}: nu_t {bounded:.15f}, TRcd {bounded * 20 * GEOMETRY:.6f} Nmm (before {bare * 20 * GEOMETRY:.6f})")
# Review G-207, R1: fck 90, fcd 60: 76.684918 -> 92.380573 kNm.
print(f"review check fck 90, fcd 60: {0.175 * 60 * GEOMETRY / 1e6:.6f} -> {2 / math.sqrt(90) * 60 * GEOMETRY / 1e6:.6f} kNm")
