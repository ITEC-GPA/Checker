"""Independent reference of the model column (colonna modello) of GPCChecker.Concrete; Python standard library only.

No GPC code, DLL or result of the library is read. The section is a rectangle b x h bent about the axis parallel to b
(strain gradient along h), with point bars in layers; compression is positive in this script.

Declared model, the same as the method page ca.colonna-modello (it is not the model of the external sources):
- concrete: design parabola-rectangle, sigma = fcd [1 - (1 - e/ec2')^2] up to ec2', fcd up to ecu', 0 beyond and in tension;
  creep stretches the strains of the law, ec2' = ec2 (1 + phi_ef), ecu' = ecu (1 + phi_ef) (EN 1992-1-1 5.8.6(4));
- steel: elastic-perfectly-plastic, fyd = fyk / gamma_s, Es; the bars are not stretched;
- net section (gross with --gross): a bar inside the concrete subtracts the concrete stress at its strain;
- ultimate strains as the plastic failure domain: ecu' at the compressed edge, ec2' at the pivot C (depth (1 - ec2/ecu) h from
  the compressed edge), eud = 0.9 euk at the far edge of the tensile bars, euc at the near edge of the compressed bars;
- model column: e2 = l0^2 / c chi, M2 = |N| e2, M1Rd,max = max over chi in [0, chi_u] of M(chi) - |N| l0^2 / c chi.

Integration: the concrete stress is split at the strains 0, ec2', ecu' and each piece is integrated with 3-point Gauss-Legendre,
exact for sigma (degree 2 in y) and sigma y (degree 3). The axial equilibrium is solved by bisection on the reference strain,
the ultimate curvature by bisection on chi, the maximum by dense sampling and golden-section search; these algorithms are
not those of the library (Illinois, sampling with the yield points, golden section on three samples).

Usage: python -I derive_model_column_oracles.py [--output model-column-independent-oracles.json]
"""
import argparse
import json
import math

GL3 = ((-math.sqrt(0.6), 5.0 / 9.0), (0.0, 8.0 / 9.0), (math.sqrt(0.6), 5.0 / 9.0))


class Rectangle:
    """Rectangular reinforced concrete section, compression positive, y towards the compressed edge, origin at the centroid."""

    def __init__(self, b, h, fcd, layers, fyd, es, euk, ec2=2e-3, ecu=3.5e-3, phi=0.0, net=True):
        self.b, self.h, self.fcd, self.fyd, self.es = b, h, fcd, fyd, es
        self.layers = [(y, n * math.pi * d * d / 4.0, d) for (y, n, d) in layers]
        self.ec2, self.ecu, self.phi, self.net = ec2 * (1 + phi), ecu * (1 + phi), phi, net
        self.ratio = ec2 / ecu
        self.eud, self.euc = 0.9 * euk, euk

    def sigma_c(self, e):
        if e <= 0 or e > self.ecu * (1 + 1e-12):
            return 0.0
        if e < self.ec2:
            r = 1.0 - e / self.ec2
            return self.fcd * (1.0 - r * r)
        return self.fcd

    def sigma_s(self, e):
        return max(-self.fyd, min(self.fyd, self.es * e))

    def forces(self, e0, chi):
        """N and M of the plane e(y) = e0 + chi y: N in N (compression positive), M in Nmm about the centroid (compressed top)."""
        lo, hi = -self.h / 2.0, self.h / 2.0
        cuts = [lo, hi]
        if chi != 0:
            for level in (0.0, self.ec2, self.ecu):
                y = (level - e0) / chi
                if lo < y < hi:
                    cuts.append(y)
        cuts.sort()
        n = m = 0.0
        for a, c in zip(cuts, cuts[1:]):
            half, mid = (c - a) / 2.0, (c + a) / 2.0
            for x, w in GL3:
                y = mid + half * x
                s = self.sigma_c(e0 + chi * y) * self.b * w * half
                n += s
                m += s * y
        for y, area, _ in self.layers:
            e = e0 + chi * y
            s = self.sigma_s(e) - (self.sigma_c(e) if self.net else 0.0)
            n += s * area
            m += s * area * y
        return n, m

    def bracket(self, chi):
        """Range of the reference strain of the admissible planes at the curvature chi (None when empty)."""
        hi = min(self.ecu - chi * self.h / 2.0, self.ec2 - chi * (self.h / 2.0 - (1.0 - self.ratio) * self.h))
        lo = -1e9
        for y, _, d in self.layers:
            hi = min(hi, self.euc - chi * (y + d / 2.0))
            lo = max(lo, -self.eud - chi * (y - d / 2.0))
        return (lo, hi) if lo <= hi else None

    def plane(self, n_ed, chi):
        """Reference strain and moment at the curvature chi with N = n_ed; None when no admissible plane carries n_ed."""
        r = self.bracket(chi)
        if r is None:
            return None
        lo, hi = r
        if self.forces(hi, chi)[0] < n_ed or self.forces(lo, chi)[0] > n_ed:
            return None
        for _ in range(90):
            mid = 0.5 * (lo + hi)
            if self.forces(mid, chi)[0] < n_ed:
                lo = mid
            else:
                hi = mid
        e0 = 0.5 * (lo + hi)
        return e0, self.forces(e0, chi)[1]

    def ultimate_curvature(self, n_ed):
        a, b = 0.0, 1e-6
        while self.plane(n_ed, b) is not None:
            a, b = b, 2 * b
        for _ in range(80):
            m = 0.5 * (a + b)
            if self.plane(n_ed, m) is None:
                b = m
            else:
                a = m
        return a


def golden(f, a, b, iterations=120):
    g = (math.sqrt(5) - 1) / 2
    c, d = b - g * (b - a), a + g * (b - a)
    fc, fd = f(c), f(d)
    for _ in range(iterations):
        if fc >= fd:
            b, d, fd = d, c, fc
            c = b - g * (b - a)
            fc = f(c)
        else:
            a, c, fc = c, d, fd
            d = a + g * (b - a)
            fd = f(d)
    return (a + b) / 2


def model_column(section, n_ed, l0, c=10.0, samples=3000, first_order=None):
    k = n_ed * l0 * l0 / c
    chi_u = section.ultimate_curvature(n_ed)
    moment = lambda chi: section.plane(n_ed, chi)[1]
    g = lambda chi: moment(chi) - k * chi
    grid = [chi_u * i / samples for i in range(samples + 1)]
    values = [g(x) for x in grid]
    i = max(range(len(values)), key=lambda j: values[j])
    a, b = grid[max(0, i - 1)], grid[min(samples, i + 1)]
    chi_star = golden(g, a, b)
    best = max((g(chi_star), chi_star), (values[i], grid[i]), (g(chi_u), chi_u))
    out = {
        "k_Nmm2": k, "chi_u_per_mm": chi_u, "M_at_chi_u_Nmm": moment(chi_u),
        "M1Rd_max_Nmm": best[0], "chi_star_per_mm": best[1], "e2_star_mm": l0 * l0 / c * best[1],
        "mode": "SectionFailure" if chi_u - best[1] <= 1e-9 * chi_u else "Instability",
    }
    if first_order is not None:
        if first_order > best[0]:
            out["equilibrium"] = None
        else:
            j = next(j for j in range(len(grid)) if values[j] >= first_order)
            lo, hi = (grid[j - 1], grid[j]) if j > 0 else (0.0, 0.0)
            for _ in range(100):
                mid = 0.5 * (lo + hi)
                if g(mid) < first_order:
                    lo = mid
                else:
                    hi = mid
            chi = 0.5 * (lo + hi)
            out["equilibrium"] = {"M0Ed_Nmm": first_order, "chi_eq_per_mm": chi, "e2_mm": l0 * l0 / c * chi,
                                  "M2_Nmm": k * chi, "MEd_Nmm": first_order + k * chi}
    return out


def vcaslu(phi=0.0, net=True):
    return Rectangle(400.0, 400.0, 0.85 * 25 / 1.5, [(160.0, 3, 24.0), (-160.0, 3, 24.0)], 450 / 1.15, 200000.0, 0.075,
                     phi=phi, net=net)


def units(net=True):
    return Rectangle(500.0, 500.0, 0.85 * 35 / 1.5, [(210.0, 6, 24.0), (-210.0, 6, 24.0)], 430 / 1.15, 206000.0, 0.075, net=net)


def closed_forms():
    """Slenderness, imperfections and nominal curvature of the 400 x 400 example (EN 1992-1-1 and NTC 2018 formulas)."""
    b = h = 400.0
    ac, fcd, fyd, es, n_ed, l0, l = b * h, 0.85 * 25 / 1.5, 450 / 1.15, 200000.0, 500e3, 12000.0, 6000.0
    a_s = 6 * math.pi * 24 * 24 / 4
    i = h / math.sqrt(12)
    lam = l0 / i
    nu = n_ed / (ac * fcd)
    omega = a_s * fyd / (ac * fcd)
    alpha_h = min(1.0, max(2.0 / 3.0, 2 / math.sqrt(l / 1000)))
    theta_i = alpha_h / 200
    kr = min(1.0, (1 + omega - nu) / (1 + omega - 0.4))
    d = h / 2 + 160.0
    inv_r0 = fyd / es / (0.45 * d)
    out = {"i_mm": i, "lambda": lam, "nu": nu, "omega": omega, "lambda_lim_ntc": 25 / math.sqrt(nu),
           "lambda_lim_en_phi0_rm_unknown": 20 * 1.0 * math.sqrt(1 + 2 * omega) * 0.7 / math.sqrt(nu),
           "alpha_h_l6m": alpha_h, "theta_i_l6m": theta_i, "ei_en_mm": theta_i * l0 / 2, "ei_l0_400_mm": l0 / 400,
           "ei_ntc_l300_mm": l / 300, "e0_ntc_mm": max(l0 / 200, 20.0), "e0_en_mm": max(h / 30, 20.0),
           "Kr": kr, "d_mm": d, "inv_r0_per_mm": inv_r0}
    for phi in (0.0, 1.0):
        beta = 0.35 + 25 / 200 - lam / 150
        k_phi = max(1.0, 1 + beta * phi)
        e2 = kr * k_phi * inv_r0 * l0 * l0 / 10
        out["nominal_phi%g" % phi] = {"beta": beta, "Kphi": k_phi, "e2_mm": e2, "M2_Nmm": n_ed * e2}
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", default="model-column-independent-oracles.json")
    args = parser.parse_args()
    result = {"source": "derive_model_column_oracles.py, Python standard library only; units N, mm, Nmm, 1/mm; "
                        "compression positive in this file, |N| in the moments", "cases": {}}
    cases = result["cases"]
    chi_h = [0.612, 1.224, 1.836, 2.448, 3.060, 4.246, 4.284, 4.895, 6.119, 7.955, 11.015]
    for net in (False, True):
        s = vcaslu(net=net)
        name = "R400-3+3D24-l0-12" + ("" if net else "-gross")
        case = model_column(s, 500e3, 12000.0, first_order=137.5e6)
        case["M_at_table_chi_Nmm"] = [s.plane(500e3, x / 1000 / 400)[1] for x in chi_h]
        case["table_chi_h_permille"] = chi_h
        cases[name] = case
    s = vcaslu()
    cases["R400-3+3D24-l0-12-c8"] = model_column(s, 500e3, 12000.0, c=8.0)
    for phi in (1.0, 2.0):
        cases["R400-3+3D24-l0-12-phi%g" % phi] = model_column(vcaslu(phi=phi), 500e3, 12000.0)
    cases["R400-3+3D24-l0-1"] = model_column(s, 500e3, 1000.0)
    cases["R400-3+3D24-l0-0.5"] = model_column(s, 500e3, 500.0)
    cases["R400-3+3D24-N0"] = model_column(s, 0.0, 12000.0)
    cases["R400-3+3D24-N2000-l0-6"] = model_column(s, 2000e3, 6000.0)
    cases["R400-3+3D24-N3200-l0-3"] = model_column(s, 3200e3, 3000.0)
    r = s.bracket(0.0)
    cases["R400-3+3D24-NRdc"] = {"NRd_c_N": s.forces(r[1], 0.0)[0]}
    for net in (False, True):
        u = units(net=net)
        for l0 in (8000.0, 13000.0, 18000.0):
            cases["R500-6+6D24-N1200-l0-%g" % (l0 / 1000) + ("" if net else "-gross")] = model_column(u, 1200e3, l0)
    result["closed_forms"] = closed_forms()
    with open(args.output, "w", encoding="utf-8", newline="\r\n") as f:
        json.dump(result, f, indent=2, sort_keys=True)
        f.write("\n")
    for key in sorted(cases):
        c = cases[key]
        if "M1Rd_max_Nmm" in c:
            print("%-30s M1Rd,max = %10.3f kNm  chi* = %.6e  e2* = %7.2f mm  chi_u = %.6e  M(chi_u) = %8.3f  %s" % (
                key, c["M1Rd_max_Nmm"] / 1e6, c["chi_star_per_mm"], c["e2_star_mm"], c["chi_u_per_mm"], c["M_at_chi_u_Nmm"] / 1e6, c["mode"]))
            if c.get("equilibrium"):
                e = c["equilibrium"]
                print("%-30s equilibrium chi = %.6e  e2 = %.3f mm  MEd = %.4f kNm" % ("", e["chi_eq_per_mm"], e["e2_mm"], e["MEd_Nmm"] / 1e6))
        else:
            print(key, c)
    print(json.dumps(result["closed_forms"], indent=1))
    for key in ("R400-3+3D24-l0-12", "R400-3+3D24-l0-12-gross"):
        print(key, ["%.3f" % (m / 1e6) for m in cases[key]["M_at_table_chi_Nmm"]])


if __name__ == "__main__":
    main()
