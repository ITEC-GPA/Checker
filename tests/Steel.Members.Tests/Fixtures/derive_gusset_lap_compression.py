"""Independent rational arithmetic oracle; no production imports.

SCI P358:2014 8.5 pp261-263. Solve equal rotation of the two branches
under an eccentric unit axial load, then solve N/Npl + M/Mpartial = 1.
Khoo et al. (2010), DOI10.18057/IJASC.2010.6.2.1, figs2/3 and tables1/2:
the experimental comparison is restricted to compact Lc=170mm specimens
that failed in the connection. The authors' Australian design formula is NOT used.
"""
from fractions import Fraction as Q
import json


def calculate(wt, tt, fyt, wg, tg, fyg, lt, lg, pitch, gm0=Q(1)):
    wt, tt, fyt, wg, tg, fyg, lt, lg, pitch = map(Q, (wt, tt, fyt, wg, tg, fyg, lt, lg, pitch))
    wt, wg = min(wt, 20 * tt), min(wg, 20 * tg)
    it, ig = wt * tt**3 / 12, wg * tg**3 / 12
    ilap = (wt + wg) * (tt + tg)**3 / 24
    kt = 1 / (lt / it + pitch / (2 * ilap))
    kg = 1 / (lg / ig + pitch / (2 * ilap))
    e0 = (tt + tg) / 2
    e = Q(6, 5) * e0
    rotation_per_unit_force = e / (kt + kg)
    mt, mg = kt * rotation_per_unit_force, kg * rotation_per_unit_force
    npt, npg = wt * tt * fyt / gm0, wg * tg * fyg / gm0
    mpt, mpg = wt * tt**2 * fyt / (5 * gm0), wg * tg**2 * fyg / (5 * gm0)
    nt = 1 / (1 / npt + mt / mpt)
    ng = 1 / (1 / npg + mg / mpg)
    assert mt + mg == e
    assert nt / npt + nt * mt / mpt == 1
    assert ng / npg + ng * mg / mpg == 1
    return {k: float(v) for k, v in dict(It=it, Ig=ig, Ilap=ilap, muTab=kt/(kt+kg),
        muGusset=kg/(kt+kg), eccentricity=e0, amplifiedEccentricity=e,
        tabMomentPerForce=mt, gussetMomentPerForce=mg,
        tabCapacity=nt, gussetCapacity=ng).items()}


if __name__ == '__main__':
    results = {
        'constructed_ec3': calculate(160, 10, 275, 200, 12, 275, 40, 50, 60),
        'constructed_ntc2018': calculate(160, 10, 275, 200, 12, 275, 40, 50, 60, Q(21, 20)),
        'khoo_compact_measured_fy': calculate(180, 10, 320, 180, 10, 320, 50, 50, 70),
    }
    measured = {'A1': 158500, 'A2': 186100, 'A3': 159800, 'B1': 175100, 'C1': 165300}
    predicted = results['khoo_compact_measured_fy']['tabCapacity']
    assert predicted == 144000
    results['experimental_comparison'] = {
        name: {'measuredN': n, 'sciNominalToExperiment': predicted / n}
        for name, n in measured.items()
    }
    print(json.dumps(results, indent=2))
