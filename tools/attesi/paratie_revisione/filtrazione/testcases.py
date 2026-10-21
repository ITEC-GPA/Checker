"""Independent expected values for the seepage / hydraulic / basal-heave review (library units N, mm, MPa, N/mm3).
Each block prints the inputs as the C# API takes them and the expected outputs.
Run: py -I testcases.py"""
import math, os, sys
from decimal import Decimal as Dm, getcontext
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from wallfe import mesh, solve, head_at, line_integral_u, subdivide, GW

getcontext().prec = 50


def K_ell(m):
    a, b = 1.0, math.sqrt(1 - m * m)
    for _ in range(60):
        a, b = (a + b) / 2, math.sqrt(a * b)
    return math.pi / (2 * a)


print('=== TC-S1 Harr sheet pile in finite layer (EmbeddedWallSeepageMesh + SeepageAnalysis) ===')
T, s, H, k = 10000.0, 5000.0, 4000.0, 0.01
lam = math.sin(math.pi * s / (2 * T))
qh = K_ell(math.cos(math.pi * s / (2 * T))) / (2 * K_ell(lam))
print('Harr Q/kH = %.6f -> Q = %.6f mm2/s for k=%.2f mm/s, H=%.0f mm' % (qh, qh * k * H, k, H))
nodes, tris, heads, faces = mesh(80000, 80000, -T, 0, -s, 20, 0, 0, H, 0, 500)
h, r, _ = solve(nodes, tris, heads)
q = sum(r[i] for i in heads if r[i] > 0)
print('port mesh 500: nodes=%d CG its=%d  Q/kH=%.6f (%.2f%% from Harr)  h(0,-5000)=%.3f  h(+10,-5000)=%.3f'
      % (len(nodes), solve.iterations, q / H, 100 * (q / H / qh - 1), head_at(nodes, tris, h, 0, -s), head_at(nodes, tris, h, 10, -s)))

print('\n=== TC-S2 symmetric excavation: excavation-side lateral boundary type ===')
LG, RG, WB, TW, BOT, LH, RH, LE = 0.0, -6000.0, -12000.0, 600.0, -30000.0, 0.0, -6000.0, 40000.0
half = TW / 2; Demb = RG - WB; b = Demb / 2
for RE in (5000.0, 4000.0):
    for noflow in (False, True):
        nodes, tris, heads, faces = mesh(LE, RE, BOT, LG, WB, TW, LG, RG, LH, RH, 500)
        if noflow:
            heads = {i: v for i, v in heads.items() if not (nodes[i][0] == RE and nodes[i][1] < RG)}
        h, r, _ = solve(nodes, tris, heads)
        xs = subdivide([-LE, -half, 0, half, RE], 500)
        q = sum(r[i] for i in heads if r[i] > 0)
        im = (head_at(nodes, tris, h, half, WB) - head_at(nodes, tris, h, half, RG)) / Demb
        Iu = line_integral_u(nodes, tris, h, half, half + b, WB, xs)
        U = Iu * 1000.0
        u_toe_R = GW * (head_at(nodes, tris, h, half, WB) - WB)
        u_toe_L = GW * (head_at(nodes, tris, h, -half, WB) - WB)
        u_mid_R = GW * (head_at(nodes, tris, h, half, -9000) - (-9000))
        print('RE=%5.0f %-9s nodes=%d its=%d  q/kH=%.5f  MeanUpwardGradient(x=300,-6000,-12000)=%.5f  NetUpwardForce(300..3300,z=-12000,w=1000,top=0)=%.1f N  u_R(toe)=%.6f u_L(toe)=%.6f u_R(-9000)=%.6f MPa'
              % (RE, 'no-flow' if noflow else 'code', len(nodes), solve.iterations, q / (LH - RH), im, U, u_toe_R, u_toe_L, u_mid_R))
gsat = 20e-6; ic = (gsat - GW) / GW
print('ic=%.6f  ic/3=%.6f  W_prism=%.1f N  sigma_v(toe,R)=%.6f MPa' % (ic, ic / 3, gsat * Demb * b * 1000, gsat * Demb))

print('\n=== TC-S3 Profile zero crossing + SaturationFromPorePressures (Rectangle(2,4), all heads 600 mm) ===')
z = [1000, 750, 600, 500, 250, 0]
print('Profile(x=500, top=1000, bottom=0, ignoreSuction=true) expected (z, p MPa):', [(zz, round(max(0.0, GW * (600 - zz)), 12)) for zz in z])
g, gs = 18e-6, 20e-6
sv = g * 400 + gs * 600; u0 = GW * 600
print('WallSoilPressure.At(layer gamma=18e-6, gsat=20e-6, ground 1000, z=0): sigma_v=%.6f u=%.6f sigma_v_eff=%.6f MPa' % (sv, u0, sv - u0))
print('  (without the inserted zero at z=600 the wet thickness would be 750 mm -> sigma_v=%.6f)' % (g * 250 + gs * 750))

print('\n=== TC-S4 Van Genuchten-Mualem (UnsaturatedConductivity.At) ===')
def vg(alpha, n, psi):
    a, nn, p = Dm(alpha), Dm(n), Dm(psi)
    m = 1 - 1 / nn
    x = (a * (-p)) ** nn
    se = (1 + x) ** (-m)
    inv = 1 / (1 + x)
    kr = se.sqrt() * (1 - (1 - inv) ** m) ** 2
    return se, kr
for alpha, n, psi in [(0.002, 2, -500), (0.001, 1.5, -3000), (0.01, 3, -1e5), (0.0005, 1.2, -20000)]:
    se, kr = vg(alpha, n, psi)
    print('alpha=%g n=%g psi=%g mm: Se=%.15e kr=%.15e' % (alpha, n, psi, se, kr))

print('\n=== TC-H1 FreeOutflow NTC 2018 ===')
print('gsat=20e-6: ic=%.12f  Mean i=0.30 -> cap=%.12f util=%.12f ; Exit i=0.60 -> cap=%.12f util=%.12f'
      % (ic, ic / 3, 0.30 / (ic / 3), ic / 2, 0.60 / (ic / 2)))

print('\n=== TC-H2 Uplift Terzaghi prism (NTC 2018 Tab. 6.2.III) ===')
U = GW * (6000 + 2500) * 3000 * 1000; W = 20e-6 * 6000 * 3000 * 1000
print('Water G1 up %.3f N, weight G1 down %.3f N -> demand %.3f capacity %.3f util %.12f' % (U, W, 1.1 * U, 0.9 * W, 1.1 * U / (0.9 * W)))
hw = 2000.0
Ub = GW * (6000 + 2500 + hw) * 3000 * 1000; top = GW * hw * 3000 * 1000
print('Ponded hw=2 m: net single action %.1f N -> util %.6f ; separate actions (1.1*Ubottom vs 0.9*(W+Wwater)) util %.6f'
      % (Ub - top, 1.1 * (Ub - top) / (0.9 * W), 1.1 * Ub / (0.9 * (W + top))))

print('\n=== TC-B1 Undrained basal heave, Terzaghi Nc=5.7 (UndrainedBasalHeave.Characteristic/Check) ===')
Hh, B, gam, cu, Nc, q = Dm(6000), Dm(12000), Dm('18e-6'), Dm('0.030'), Dm('5.7'), Dm('0.010')
Bp = B / Dm(2).sqrt()
for name, g1, gq, mcu in [('characteristic', 1, 1, 1), ('A1+M1+R1', Dm('1.3'), Dm('1.5'), 1), ('A2+M2+R1', 1, Dm('1.3'), Dm('1.4'))]:
    c = cu / mcu; gross = g1 * gam * Hh + gq * q; relief = c * Hh / Bp; net = gross - relief; cap = Nc * c
    print('%-15s B\'=%.6f gross=%.12f relief=%.12f net=%.12f cap=%.12f util=%.12f FS=%.6f' % (name, Bp, gross, relief, net, cap, net / cap, cap / net))
