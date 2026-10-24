"""Independent same-case statics for a proposed I-beam splice ULS envelope.

Standard Python only; does not import Checker or generate/change test fixtures.
SCI P398 (2013), section4 and exampleD.1: supplied exact section/grid data,
and the existing qualified directional bearing resistance139400 N per axis.
The oracle concerns individual bearing checks, not every remaining splice mode.
"""
import json

H, TW, TF, AREA, IY = 453.4, 8.5, 12.7, 8550.0, 294000000.0
HW = H - 2 * TF
AW = HW * TW
IW = TW * HW ** 3 / 12
R_BEARING = 139400.0


def bearing_case(n, v, m):
    nw, mw = n * AW / AREA, m * IW / IY
    sides = {}
    for name, sign in (("Left", 1), ("Right", -1)):
        xs = [-sign * 70, -sign * 155]
        points = [(x, y) for x in xs for y in (-120, 0, 120)]
        xc = sum(x for x, _ in points) / len(points)
        polar = sum((x - xc) ** 2 + y ** 2 for x, y in points)
        ng, vg = sign * nw, sign * v
        mg = sign * mw - xc * vg
        forces = []
        for i, (x, y) in enumerate(points, 1):
            fx = ng / len(points) - mg * y / polar
            fy = vg / len(points) + mg * (x - xc) / polar
            forces.append({"bolt": i, "fx": fx, "fy": fy,
                           "bearing_interaction": (abs(fx) + abs(fy)) / R_BEARING})
        sides[name] = {"group_moment": mg, "bolts": forces,
                       "maximum_bearing_interaction": max(f["bearing_interaction"] for f in forces)}
    return {"actions": {"N": n, "V": v, "M": m}, "sides": sides,
            "maximum_bearing_interaction": max(s["maximum_bearing_interaction"] for s in sides.values())}


cases = {
    "U-low": bearing_case(-75000, -75000, -100000000),
    "U-P398": bearing_case(-150000, -150000, -200000000),
    "U-axial": bearing_case(-300000, 0, 0),
    "U-shear-moment": bearing_case(0, -150000, -200000000),
}
synthetic = bearing_case(-300000, -150000, -200000000)
report = {
    "units": "N, mm, Nmm; dimensionless utilization",
    "source": "SCI P398(2013) D.1; independently balanced point forces and qualified bearing capacity",
    "cases": cases,
    "envelope_low_and_P398": max(cases[k]["maximum_bearing_interaction"] for k in ("U-low", "U-P398")),
    "envelope_axial_and_shear_moment": max(cases[k]["maximum_bearing_interaction"] for k in ("U-axial", "U-shear-moment")),
    "forbidden_synthetic_coordinatewise_maximum": synthetic,
}
print(json.dumps(report, indent=2))
