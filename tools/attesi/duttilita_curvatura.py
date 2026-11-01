"""Independent reference of the curvature ductility of a rectangular RC section (NTC 2018 §4.1.2.3.4.2, Circolare 2019 Fig. C4.1.12).

Written for the review of the moment-curvature method (docs/metodi/ca.momento-curvatura.md, voce R14 of ANTHEA, 1/11/2026). It shares
only the documented model with GPCChecker.Concrete; the implementation is separate (closed-form integration of the exact
parabola-rectangle in the strain variable, bisection on the neutral axis depth, curvature control):

- design laws: fcd = αcc fck / γc on the parabola-rectangle (εc2, εcu), zero stress beyond εcu; concrete in tension neglected; steel
  elastic-perfectly plastic at fyd up to εud = 0.9 εuk; net section (σs − σc) As for the bars inside the concrete;
- curvature-controlled branch at constant N: for each curvature κ the neutral axis depth x from N(x) = N;
- limit curvature κu: the largest κ with εc,top ≤ εcu, steel ≤ εud and, for the section entirely compressed, the strain at
  (1 − εc2/εcu) h from the compressed edge ≤ εc2 (pivots B, A and C of the plastic domain);
- conventional first yield (NTC [4.1.18b]): the first of tension steel at εyd = fyd/Es and concrete at εc2 on the compressed edge,
  giving M'yd and κ'yd; φyd = (MRd/M'yd) κ'yd with MRd = M(κu), the resistance at the limit point;
- κu,0.85: first curvature after the peak with M = 0.85 Mmax; φu = min(κu,0.85; κu); μφ = φu/φyd.

Units N, mm, MPa; N negative in compression as in the library (the internal sums use compression positive). Standard library only:
    py -3 tools/attesi/duttilita_curvatura.py          prints the cases as JSON
"""
import json
import math
import sys

GAUSS5 = [(-0.9061798459386640, 0.2369268850561891), (-0.5384693101056831, 0.4786286704993665), (0.0, 0.5688888888888889),
          (0.5384693101056831, 0.4786286704993665), (0.9061798459386640, 0.2369268850561891)]


def gauss(f, a, b, n=8):
    h = (b - a) / n
    total = 0.0
    for i in range(n):
        lo = a + i * h
        total += sum(w * f(lo + h / 2 * (1 + t)) for t, w in GAUSS5) * h / 2
    return total


class Section:
    def __init__(self, b, h, bars, fck, fyk, es, eps_uk, alpha_cc=0.85, gamma_c=1.5, gamma_s=1.15, eps_c2=0.002, eps_cu=0.0035):
        """bars: (depth from the compressed edge [mm], diameter [mm])."""
        self.b, self.h = b, h
        self.bars = [(z, math.pi * d * d / 4) for z, d in bars]
        self.fcd = alpha_cc * fck / gamma_c
        self.fyd = fyk / gamma_s
        self.es = es
        self.eyd = self.fyd / es
        self.eud = 0.9 * eps_uk
        self.ec2, self.ecu = eps_c2, eps_cu
        self.table = None

    def nonlinear(self, fck, alpha_cc=0.85, gamma_c=1.5):
        """EN 1992-1-1 3.1.5 (3.14) with fck at the peak, as the NonLinear diagram of the material: 14 points (8 up to εc1, 4 to εcu1,
        εcu1 repeated), linear between them, zero beyond εcu1, scaled by αcc/γc. εc1 = 0.7 fcm^0.31 ‰ (≤ 2.8 ‰), εcu1 = 3.5 ‰ up to C50/60,
        k = 1.05 Ecm εc1/fcm, Ecm = 22000 (fcm/10)^0.3 (Table 3.1). The peak strain εc1 replaces εc2 (first yield and pivot C)."""
        fcm = fck + 8
        ec1, ecu1 = min(0.7 * fcm ** 0.31, 2.8) / 1000, 0.0035
        k = 1.05 * 22000 * (fcm / 10) ** 0.3 * ec1 / fcm
        strains = [ec1 / 8 * i for i in range(9)] + [ec1 + (ecu1 - ec1) / 4 * i for i in range(1, 5)] + [ecu1]
        stresses = [alpha_cc / gamma_c * fck * (k * e / ec1 - (e / ec1) ** 2) / (1 + (k - 2) * e / ec1) for e in strains]
        self.table = list(zip(strains, stresses))
        self.ec2, self.ecu = ec1, ecu1
        return self

    def sigma_c(self, e):
        """Compression positive."""
        if e <= 0 or e > self.ecu * (1 + 1e-12):
            return 0.0
        if self.table:
            for (e0, s0), (e1, s1) in zip(self.table, self.table[1:]):
                if e0 <= e <= e1 and e1 > e0:
                    return s0 + (s1 - s0) * (e - e0) / (e1 - e0)
            return 0.0
        if e <= self.ec2:
            return self.fcd * (1 - (1 - e / self.ec2) ** 2)
        return self.fcd

    def sigma_s(self, e):
        return math.copysign(min(self.es * abs(e), self.fyd), e)

    def strain(self, x, k, z):
        """Compression positive: ε(z) = κ (x − z)."""
        return k * (x - z)

    def forces(self, x, k):
        """N (compression positive) and M about mid-depth (positive when it compresses the top edge)."""
        h = self.h
        e_top, e_bot = self.strain(x, k, 0.0), self.strain(x, k, h)
        lo, hi = max(0.0, e_bot), e_top
        n = m = 0.0
        if hi > 0:
            corners = [e for e, _ in self.table] if self.table else [self.ec2, self.ecu]
            breaks = sorted({lo, hi} | {v for v in corners if lo < v < hi})
            for a, c in zip(breaks, breaks[1:]):
                # fibre of strain e at depth z = x − e/κ, dz = −de/κ
                n += self.b / k * gauss(lambda e: self.sigma_c(e), a, c)
                m += self.b / k * gauss(lambda e: self.sigma_c(e) * (h / 2 - (x - e / k)), a, c)
        for z, a_s in self.bars:
            e = self.strain(x, k, z)
            net = self.sigma_s(e) - self.sigma_c(e)   # σs with compression positive (odd law), minus the concrete in the hole
            n += net * a_s
            m += net * a_s * (h / 2 - z)
        return n, m

    def neutral_axis(self, k, n_target):
        """x with N(x) = n_target (compression positive); N grows with x."""
        f = lambda x: self.forces(x, k)[0] - n_target
        # admissible planes only (compressed edge at most at εcu): there N grows with x; beyond, the law drops to zero
        lo, hi = -max(10 * self.h, self.eud / k), self.ecu / k
        flo, fhi = f(lo), f(hi)
        if not flo < 0 < fhi:
            return None
        for _ in range(200):
            mid = (lo + hi) / 2
            fm = f(mid)
            if fm < 0:
                lo = mid
            else:
                hi = mid
            if hi - lo < 1e-11 * self.h:
                break
        return (lo + hi) / 2

    def state(self, k, n_target):
        x = self.neutral_axis(k, n_target)
        if x is None:
            return None
        n, m = self.forces(x, k)
        tension = max(-self.strain(x, k, z) for z, _ in self.bars)       # largest tensile bar strain
        steel = max(abs(self.strain(x, k, z)) for z, _ in self.bars)
        top = self.strain(x, k, 0.0)
        pivot_c = self.strain(x, k, (1 - self.ec2 / self.ecu) * self.h) if self.strain(x, k, self.h) > 0 else -1.0
        return dict(x=x, k=k, n=n, m=m, tension=tension, steel=steel, top=top, pivot_c=pivot_c)

    def limit_ratio(self, s):
        if s is None:
            return math.inf
        return max(s['top'] / self.ecu, s['steel'] / self.eud if s['tension'] > 0 else 0.0, s['pivot_c'] / self.ec2)

    def yield_ratio(self, s):
        return max(s['tension'] / self.eyd, s['top'] / self.ec2)


def bisect(f, lo, hi, iters=80):
    flo = f(lo)
    for _ in range(iters):
        mid = (lo + hi) / 2
        if (f(mid) < 0) == (flo < 0):
            lo = mid
        else:
            hi = mid
    return lo                                                       # on the side of f(lo): the last admissible plane for the limit


def ductility(sec, n_library):
    """n_library: N with compression negative (library convention)."""
    nt = -n_library
    k_hi = 1e-6
    while sec.limit_ratio(sec.state(k_hi, nt)) < 1:
        k_hi *= 2
    k_u = bisect(lambda k: sec.limit_ratio(sec.state(k, nt)) - 1, k_hi / 2, k_hi)
    lim = sec.state(k_u, nt)
    k_y = bisect(lambda k: sec.yield_ratio(sec.state(k, nt)) - 1, 0.0 + 1e-12, k_u) if sec.yield_ratio(lim) >= 1 else None
    y = sec.state(k_y, nt) if k_y else None
    # sweep for the peak and the post-peak reduction
    steps = 2000
    ks = [k_u * i / steps for i in range(1, steps + 1)]
    ms = [sec.state(k, nt)['m'] for k in ks]
    i_max = max(range(steps), key=lambda i: ms[i])
    lo, hi = ks[max(i_max - 1, 0)], ks[min(i_max + 1, steps - 1)]
    g = (math.sqrt(5) - 1) / 2
    for _ in range(60):                                             # golden section on the peak
        a, c = hi - g * (hi - lo), lo + g * (hi - lo)
        if sec.state(a, nt)['m'] > sec.state(c, nt)['m']:
            hi = c
        else:
            lo = a
    k_max = (lo + hi) / 2
    m_max = max(sec.state(k_max, nt)['m'], ms[i_max])
    k_85 = None
    for i in range(i_max, steps):
        if ms[i] < 0.85 * m_max:
            k_85 = bisect(lambda k: sec.state(k, nt)['m'] - 0.85 * m_max, ks[i - 1], ks[i])
            break
    m_rd = lim['m']
    out = dict(N_kN=n_library / 1e3, MRd_kNm=m_rd / 1e6, chi_u_limit=k_u, x_limit=lim['x'], Mmax_kNm=m_max / 1e6, chi_Mmax=k_max)
    if y:
        phi_yd = m_rd / y['m'] * k_y
        phi_u = min(k_85, k_u) if k_85 else k_u
        out.update(Myd1_kNm=y['m'] / 1e6, chi_yd1=k_y, first_yield='TensionSteel' if y['tension'] / sec.eyd >= y['top'] / sec.ec2 else 'ConcretePeakStrain',
                   phi_yd=phi_yd, chi_u_085=k_85, phi_u=phi_u, ultimate='PostPeakReduction' if k_85 and k_85 < k_u else 'LimitPoint',
                   mu_phi=phi_u / phi_yd)
    return out


def cases():
    out = []
    # ca.momento-curvatura §10: R300x500 C30/37, 3Ø20 at 452 mm, 2Ø16 at 46 mm from the compressed (top) edge, fyk 450 EPP, εuk 10 %
    page = Section(300, 500, [(452, 20)] * 3 + [(46, 16)] * 2, 30, 450, 200000, 0.10)
    out.append(dict(id='pagina-10', descrizione='R300x500 C30/37 3Ø20 + 2Ø16, N = 0, lembo superiore compresso (§10 della pagina)', **ductility(page, 0.0)))
    # UltimateSections.Symmetric of the tests: 300 × 500 C25/30, 4Ø16 at ±204 mm (46 and 454 mm from the edge), B450C EPP εuk 7.5 %
    sym = Section(300, 500, [(46, 16)] * 2 + [(454, 16)] * 2, 25, 450, 200000, 0.075)
    for n_kn in (0.0, -500.0, -1000.0, -1500.0):
        out.append(dict(id='simmetrica-%d' % int(-n_kn), descrizione='300 × 500 C25/30 4Ø16, N = %g kN' % n_kn, **ductility(sym, n_kn * 1e3)))
    # the same section with the nonlinear law of EN 3.1.5 (descending branch after εc1): the peak may come before the limit
    for n_kn in (0.0, -1000.0, -1500.0):
        nl = Section(300, 500, [(46, 16)] * 2 + [(454, 16)] * 2, 25, 450, 200000, 0.075).nonlinear(25)
        out.append(dict(id='non-lineare-%d' % int(-n_kn), descrizione='300 × 500 C25/30 4Ø16, legge non lineare EN 3.1.5, N = %g kN' % n_kn,
                        **ductility(nl, n_kn * 1e3)))
    return out


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    print(json.dumps(cases(), indent=1, ensure_ascii=False))
