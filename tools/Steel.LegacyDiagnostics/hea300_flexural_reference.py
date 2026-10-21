"""Independent reference for SectionHBuckling1; no GPC or .NET dependencies.

N, mm, MPa. EN 1993-1-1:2005 + AC:2006/2009, 6.3.1.1-3,
tables 5.2, 6.1 and 6.2. See LEGACY_CLOSURE_01_HEA300.txt.
Run with Python 3; decimal arithmetic and bisection avoid the production
double-precision radical expression for the buckling reduction factor.
"""
from decimal import Decimal as D, localcontext
import json


def calculate():
    with localcontext() as ctx:
        ctx.prec = 50
        pi = D("3.1415926535897932384626433832795028841971693993751")
        h, b, tw, tf, r = map(D, ("290", "300", "8.5", "14", "27"))
        hw = h - 2 * tf
        # One root fillet: square minus quarter disk, axes at sharp web/flange corner.
        af = (1 - pi / 4) * r**2
        qf = (D(5) / 6 - pi / 4) * r**3
        jf = (1 - 5 * pi / 16) * r**4
        area = 2 * b * tf + hw * tw + 4 * af
        iy = 2 * (b * tf**3 / 12 + b * tf * ((h - tf) / 2)**2)
        iy += tw * hw**3 / 12 + 4 * ((hw / 2)**2 * af - hw * qf + jf)
        iz = 2 * tf * b**3 / 12 + hw * tw**3 / 12
        iz += 4 * ((tw / 2)**2 * af + tw * qf + jf)
        rows = []
        for fy in (D(355), D(275)):
            eps = (235 / fy).sqrt()
            cf_t = ((b - tw) / 2 - r) / tf
            cw_t = (hw - 2 * r) / tw
            flange_class = next(cl for cl, limit in enumerate((9, 10, 14), 1) if cf_t <= limit * eps)
            web_class = next(cl for cl, limit in enumerate((33, 38, 42), 1) if cw_t <= limit * eps)
            for axis, inertia, length, alpha in ((1, iy, D(13000), D(".34")), (2, iz, D(6500), D(".49"))):
                ncr = pi**2 * 210000 * inertia / length**2
                lam2 = area * fy / ncr
                lam = lam2.sqrt()
                phi = (1 + alpha * (lam - D(".2")) + lam2) / 2
                # Smaller root of lambda^2 chi^2 - 2 Phi chi + 1 = 0.
                # [0, min(1, vertex)] is monotone; these cases have lambda > .2.
                lo, hi = D(0), min(D(1), phi / lam2)
                assert lam > D(".2") and lam2 * hi**2 - 2 * phi * hi + 1 < 0
                for _ in range(160):
                    mid = (lo + hi) / 2
                    if lam2 * mid**2 - 2 * phi * mid + 1 > 0:
                        lo = mid
                    else:
                        hi = mid
                chi = (lo + hi) / 2
                nb = chi * area * fy  # gammaM1 = 1, same as the legacy configuration.
                old = D("1522500") if axis == 1 else D("1667400")
                rows.append(dict(fy=fy, axis=axis, section_class=max(flange_class, web_class),
                    length=length, alpha=alpha, ncr=ncr, slenderness=lam, phi=phi, chi=chi,
                    nb=nb, utilization=D(1320000) / nb,
                    historical_expected=old, historical_relative_error=abs(nb-old)/nb))
        return dict(area=area, iy=iy, iz=iz, flange_width_thickness=cf_t,
                    web_width_thickness=cw_t, cases=rows)


if __name__ == "__main__":
    print(json.dumps(calculate(), default=str, indent=2))
