"""S3 of the embedded-wall review (scheda decisioni ANTHEA 16/11/2026): independent expected values of the crack width for the tests
WallSectionSessionTests.CrackSpacingOfTheTensionFaceIsOptIn (part B) and WallSectionSessionTests.CrackWidthBranchOfS3MatchesTheIndependentScalarVector
(part A), tests/Concrete.Checks compiled in GPCChecker.Test.Concrete; rilievo R2 of the review G-207 (16/11/2026). Standard library only, no
Checker code: the tests compare the library with these numbers, not with the intermediate results of the same calculation.

A. Scalar contract of CrackWidthCalculator (NTC 2018, Circolare C4.1.2.2.4.5), vector of the review G-207 (section S3, table W1-W4,
   chatgpt-compiti/revisione-checker-scheda-G207.md): sigma_s 200 MPa, Es 200 000, Ecm 30 000, fct 3 MPa, rho 0.02, k1 0.8 (ribbed),
   k2 0.5, kt 0.4 (long term), bar 16 mm, c 40 mm, h - x 300 mm. Recomputed here, same values as the review:
   d_eps = max[(sigma_s - kt fct/rho (1 + Es/Ecm rho))/Es; 0.6 sigma_s/Es] = 0.000660; near = (3.4 c + k1 k2 0.425 phi/rho)/1.7 = 160 mm;
   far = 0.75 (h - x) = 225 mm; s_lim = 5 (c + phi/2) = 240 mm; wk = 1.7 d_sm d_eps with d_sm = near if s <= s_lim, else max(near; far).

B. Cracked section of the wall strip of the session test, by hand (closed form, not the section solver): strip 1000 x 400 mm, clear cover
   40 mm, top 4 bars 20 mm at y = 350 mm, bottom 7 bars 16 mm at y = 48 mm; C30 of EN 1992-1-1 (Ecm = 22 000 (fcm/10)^0.3, fcm = 38 MPa,
   fctm = 0.3 fck^(2/3)), B450 Es = 200 000 MPa, creep phi = 2, quasi-permanent combination (long term, kt = 0.4), ribbed bars.
   Linear cracked analysis without tensile concrete, as the contract of WallSectionSession.CheckServiceability: concrete E = Ecm in
   compression, bars n = Es (1 + phi)/Ecm, a compressed bar displaces its concrete (n - 1). N = 0, M = +-50 kNm.
   Neutral axis from the first moment of the homogenized section, x from the compressed face:
   b x^2/2 + (n - 1) As' (x - d') - n As (d - x) = 0; I = b x^3/3 + (n - 1) As' (x - d')^2 + n As (d - x)^2; sigma_s = n M (d - x)/I.
   Crack check of the section (SectionCrackCheck, NTC): h - x = 400 - x; hc,eff = min[2.5 (h - d); (h - x)/3; h/2];
   Ac,eff = 1000 hc,eff; rho = As/Ac,eff (all the bars of the tensile face are within hc,eff); c = 40 mm (assigned cover);
   fct = fctm; then the formula of A with the spacing of the rule: sparser face 300 mm, tension face 150.67 mm (bottom) or 300 mm (top).
Run: python -I tools/attesi/fessurazione_s3_faccia_tesa.py
"""
import math


def ntc_width(sigma, es, ecm, fct, rho, phi, c, s, tensile_depth, kt=0.4, k1=0.8, k2=0.5):
    d_eps = max((sigma - kt * fct / rho * (1 + es / ecm * rho)) / es, 0.6 * sigma / es)
    near = (3.4 * c + k1 * k2 * 0.425 * phi / rho) / 1.7
    far = 0.75 * tensile_depth
    s_lim = 5 * (c + phi / 2)
    d_sm = near if s <= s_lim else max(near, far)
    return dict(d_eps=d_eps, near=near, far=far, s_lim=s_lim, d_sm=d_sm, wk=1.7 * d_sm * d_eps)


print("A. Scalar vector of the review G-207 (W1-W4)")
for case, s in (("W1", (1000 - 80 - 16) / 6), ("W2", 300.0), ("W3", 240.0), ("W4", 240.000001)):
    r = ntc_width(200, 200000, 30000, 3, 0.02, 16, 40, s, 300)
    print(f"  {case}: s = {s:.9f} mm  d_eps = {r['d_eps']:.9f}  near = {r['near']:.9f}  far = {r['far']:.9f}  s_lim = {r['s_lim']:.9f}"
          f"  d_sm = {r['d_sm']:.9f}  wk = {r['wk']:.9f} mm")

print("B. Cracked wall strip 1000 x 400, NTC quasi-permanent, phi = 2")
b, h, cover = 1000.0, 400.0, 40.0
fck, es, phi_creep = 30.0, 200000.0, 2.0
ecm = 22000 * ((fck + 8) / 10) ** 0.3
fctm = 0.3 * fck ** (2 / 3)
n = es * (1 + phi_creep) / ecm
area = lambda d: math.pi * d * d / 4
top_bars, top_d, top_y = 4, 20.0, 400 - 40 - 10.0
bottom_bars, bottom_d, bottom_y = 7, 16.0, 40 + 8.0
print(f"  Ecm = {ecm:.9f} MPa, fctm = {fctm:.9f} MPa, n = {n:.9f}")
for name, m, s_sparser, s_tension in (("bottom in tension, M = +50 kNm", 5e7, 300.0, (1000 - 80 - 16) / 6),
                                      ("top in tension, M = -50 kNm", -5e7, 300.0, 300.0)):
    if m > 0:  # x from the top face: tensile bars at the bottom
        a_t, phi_t, d = bottom_bars * area(bottom_d), bottom_d, h - bottom_y
        a_c, d_c = top_bars * area(top_d), h - top_y
    else:
        a_t, phi_t, d = top_bars * area(top_d), top_d, top_y
        a_c, d_c = bottom_bars * area(bottom_d), bottom_y
    # b x^2/2 + (n - 1) a_c (x - d_c) - n a_t (d - x) = 0
    qa, qb, qc = b / 2, (n - 1) * a_c + n * a_t, -(n - 1) * a_c * d_c - n * a_t * d
    x = (-qb + math.sqrt(qb * qb - 4 * qa * qc)) / (2 * qa)
    assert x > d_c, "compressed bars inside the compressed zone"
    inertia = b * x ** 3 / 3 + (n - 1) * a_c * (x - d_c) ** 2 + n * a_t * (d - x) ** 2
    sigma_s = n * abs(m) * (d - x) / inertia
    tensile_depth = h - x
    hc = min(2.5 * (h - d), tensile_depth / 3, h / 2)
    assert h - d <= hc, "the bars of the tensile face are within hc,eff"
    aceff = b * hc
    rho = a_t / aceff
    print(f"  {name}: x = {x:.9f} mm, h - x = {tensile_depth:.9f} mm, sigma_s = {sigma_s:.9f} MPa, hc,eff = {hc:.9f} mm,"
          f" Ac,eff = {aceff:.6f} mm2, As,eff = {a_t:.9f} mm2, rho = {rho:.12f}")
    for rule, s in (("sparser face", s_sparser), ("tension face", s_tension)):
        r = ntc_width(sigma_s, es, ecm, fctm, rho, phi_t, cover, s, tensile_depth)
        print(f"    {rule}: s = {s:.9f} mm  d_eps = {r['d_eps']:.12f}  near = {r['near']:.9f}  far = {r['far']:.9f}"
              f"  s_lim = {r['s_lim']:.3f}  d_sm = {r['d_sm']:.9f}  wk = {r['wk']:.9f} mm")
