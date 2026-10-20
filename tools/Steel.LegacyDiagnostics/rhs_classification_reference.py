"""Independent reference for the eleven loads in the two legacy RHS tests.

N, mm, MPa, Nmm; compression is negative. Sharp corners, fy=355 MPa.
EN 1993-1-1:2005 Table 5.2; no optional 5.5.2(9) treatment.
No GPC import, DLL, test-source parsing or regenerated regression fixture.
"""
import json
from math import sqrt


def plate_class(ratio, limits):
    return next((i for i, limit in enumerate(limits, 1) if ratio <= limit), 4)


def reference(name, h, b, t, compression):
    epsilon = sqrt(235 / 355)
    uniform = [factor * epsilon for factor in (33, 38, 42)]
    bending = [factor * epsilon for factor in (72, 83, 124)]
    web, flange = (h - 2 * t) / t, (b - 2 * t) / t
    axial = max(plate_class(web, uniform), plate_class(flange, uniform))
    strong = max(plate_class(web, bending), plate_class(flange, uniform))
    weak = max(plate_class(web, uniform), plate_class(flange, bending))
    loads = [(n, 0, 0, axial) for n in compression]
    loads.extend([(0, 1e6, 0, strong), (0, 0, 1e6, weak)])
    return {
        "test": name, "h": h, "b": b, "t": t, "fy": 355,
        "area": h * b - (h - 2 * t) * (b - 2 * t),
        "epsilon": epsilon, "compression_limits": uniform, "bending_limits": bending,
        "web_c_t": web, "flange_c_t": flange,
        "cases": [{"N": n, "M1": m1, "M2": m2, "class": cls}
                  for n, m1, m2, cls in loads],
    }


if __name__ == "__main__":
    print(json.dumps([
        reference("SectionRHSClassification", 500, 300, 12.5,
                  [-1500e3, -2859e3 * .95, -6029e3 * .95, -6029e3]),
        reference("SectionRHSClasstification2", 350, 250, 10,
                  [-1300e3, -1700e3, -2160e3]),
    ], indent=2, allow_nan=False))
