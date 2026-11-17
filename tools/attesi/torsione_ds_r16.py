"""Expected values of R16 option (b) (ANTHEA decision sheet of 16/11/2026): DS torsion with nu = nu_t whenever there is torsion.

Independent hand arithmetic for TorsionMigrationTests.DsTorsionUsesTheTorsionEfficiencyOfDkNa (GPCChecker.Test.Concrete).
Rectangle 300x500 of the tests: Ak = 82416 mm2, tef = 96 mm, fck = 30 MPa, fcd = 20 MPa, cot(theta) = 1.5, T = 30 kNm.
DK NA: nu_v = max(0.45; 0.7 - fck/200) (5.103 NA), nu_t = 0.7 (0.7 - fck/200) (5.104 NA), nu_t = nu_v with walls reinforced on both faces.
Shear of one direction given as V = 100 kN with VRcd = 400 kN (VRcd computed with nu_v).
Run: python -I tools/attesi/torsione_ds_r16.py
"""

AK, TEF, FCK, FCD, COT, T = 82416.0, 96.0, 30.0, 20.0, 1.5, 30e6
V, VRCD = 100e3, 400e3

nu_v = max(0.45, 0.7 - FCK / 200)
nu_t = 0.7 * (0.7 - FCK / 200)
truss = 2 * AK * TEF * FCD * COT / (1 + COT * COT)  # TRcd per unit nu, N mm

# Concrete 0.28-0.37: mean weighted on the wall shear stresses tau_V and tau_T.
tau_t = T / (2 * AK * TEF)
tau_v = V / VRCD * nu_v * FCD * COT / (1 + COT * COT)
nu_old = (nu_v * tau_v + nu_t * tau_t) / (tau_v + tau_t)
trcd_old = nu_old * truss
inter_old = T / trcd_old + V / VRCD * nu_v / nu_old

# Concrete 0.38 (R16 option b): nu = nu_t with torsion; the shear ratio is referred to the same nu (scale nu_v/nu_t).
trcd_new = nu_t * truss
inter_new = T / trcd_new + V / VRCD * nu_v / nu_t

# Legacy option DsTorsionStrutEfficiency.ShearFactor: nu_v in the strut, shear ratio unscaled (unchanged).
inter_legacy = T / (nu_v * truss) + V / VRCD

print(f"nu_v = {nu_v:.6f}  nu_t = {nu_t:.6f}  truss = {truss:.6f} N mm")
print(f"pure torsion: TRcd = {trcd_new / 1e6:.6f} kNm (nu_t), box {nu_v * truss / 1e6:.6f} kNm (nu_v)")
print(f"before: tau_T = {tau_t:.6f} MPa, tau_V = {tau_v:.6f} MPa, nu = {nu_old:.6f}, TRcd = {trcd_old / 1e6:.6f} kNm, interaction = {inter_old:.6f}")
print(f"now:    nu = {nu_t:.6f}, TRcd = {trcd_new / 1e6:.6f} kNm, interaction = {inter_new:.6f}")
print(f"legacy ShearFactor: interaction = {inter_legacy:.6f}")
print(f"change of the strut interaction: {inter_new / inter_old - 1:+.2%}; of TRcd: {trcd_new / trcd_old - 1:+.2%}")
