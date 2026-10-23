"""Independent Decimal calculations: EN1993-1-8 6.2.6.1-3 and SCI P398 C.1.
No production import, fixture rewrite or intermediate rounding. N/mm/MPa.
"""
from decimal import Decimal as D, getcontext
import json

getcontext().prec = 40


def web(width, depth, thickness, fy, av, beta="0", sigma="0", gamma0="1", gamma1="1"):
    width, depth, thickness, fy, av, beta, sigma, gamma0, gamma1 = map(
        lambda value: D(str(value)), (width, depth, thickness, fy, av, beta, sigma, gamma0, gamma1))
    x = width * thickness / av
    w1 = 1 / (1 + D("1.3") * x*x).sqrt()
    w2 = 1 / (1 + D("5.2") * x*x).sqrt()
    omega = 1 if beta <= D(".5") else (w1 + 2 * (1-beta) * (1-w1) if beta < 1 else w1 + (beta-1) * (w2-w1))
    lam = D(".932") * (width*depth*fy / (210000 * thickness*thickness)).sqrt()
    rho = D(1) if lam <= D(".72") else min(D(1), (lam-D(".2")) / (lam*lam))
    kw = 1 if sigma <= D(".7")*fy else D("1.7")-sigma/fy
    base = omega*width*thickness*fy
    return {key: str(value) for key, value in dict(
        omega1=w1, omega2=w2, omega=omega, slenderness=lam, rho=rho, kw=kw,
        tension=base/gamma0, compression_yield=kw*base/gamma0,
        compression_buckling=kw*rho*base/gamma1,
        panel=D(".9")*fy*av/(D(3).sqrt()*gamma0)).items()}


cases = {}
for beta in (0, ".5", ".75", 1, "1.5", 2):
    cases[f"Analytical_I_web_beta{beta}"] = web(280, 260, 10, 275, 2800, beta)
cases["SCI_C1_printed_beff248"] = web(248, "200.3", "12.8", 265, "3772.3")
cases["SCI_C1_geometric_beff247point6"] = web("247.6", "200.3", "12.8", 265, "3772.3")
cases["SCI_C1_tension233"] = web(233, "200.3", "12.8", 265, "3772.3")
print(json.dumps(cases, indent=2))
