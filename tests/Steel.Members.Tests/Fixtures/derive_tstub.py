"""Independent Decimal reproduction of EN1993-1-8 Table 6.2 and SCI P398 C.1.

No production module is imported. Prints results only; never rewrites test fixtures.
N, mm, N/mm2. P398 uses UK factors gammaM0=1 and gammaM2Connection=1.25.
Its rounded intermediate numbers are not used as unrounded reference values.
"""
from decimal import Decimal as D, getcontext
import json

getcontext().prec = 40


def calculate(t, fy, m, emin, leff1, leff2, rows, area, bolt_fu=800,
              gamma0="1", gamma2="1.25", contact_width="0"):
    t, fy, m, emin, leff1, leff2, area, bolt_fu, gamma0, gamma2, contact_width = map(
        lambda value: D(str(value)),
        (t, fy, m, emin, leff1, leff2, area, bolt_fu, gamma0, gamma2, contact_width))
    n = min(emin, D("1.25") * m)
    moment1 = leff1 * t ** 2 * fy / (4 * gamma0)
    moment2 = leff2 * t ** 2 * fy / (4 * gamma0)
    bolts = 2 * rows * D("0.9") * bolt_fu * area / gamma2
    ew = contact_width / 4
    mode1 = ((8 * n - 2 * ew) * moment1 / (2 * m * n - ew * (m + n))
             if contact_width else 4 * moment1 / m)
    mode2 = (2 * moment2 + n * bolts) / (m + n)
    return {key: str(value) for key, value in dict(
        n=n, moment1=moment1, moment2=moment2, mode1=mode1, mode2=mode2,
        mode3=bolts, no_prying_plate=2 * moment1 / m,
        critical_elongation=D("8.8") * m ** 3 * area * rows / (leff1 * t ** 3)).items()}


cases = {}
for label, t, m, e, l1, l2, rows in [
    ("SCI_C1_column_row_p87", "20.5", "33.4", 75, 210, 233, 1),
    ("SCI_C1_external_end_plate_p89", 25, "30.4", 50, 125, 125, 1),
    ("SCI_C1_column_rows_1_2_p93", "20.5", "33.4", 75, 332, 332, 2),
    ("SCI_C1_column_rows_1_2_3_p97", "20.5", "33.4", 75, 422, 422, 3),
]:
    cases[label] = calculate(t, 265, m, e, l1, l2, rows, 353, contact_width="39.55")
for t in (6, 12, 20, 30):
    cases[f"analytical_M20_t{t}"] = calculate(t, 275, 40, 60, 200, 200, 1, 245)
cases["analytical_M20_NTC_t12"] = calculate(12, 275, 40, 60, 200, 200, 1, 245, gamma0="1.05")
print(json.dumps(cases, indent=2))
