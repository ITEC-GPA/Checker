"""Independent arithmetic of EN1993-1-8 Table3.4; no production imports.

Synthetic supporting-flange example. This is not a whole-joint qualification.
Units: N, mm, MPa. Output is informational, never rewrites fixtures.
"""
from fractions import Fraction as F


def bearing(thickness, upper_end, lower_end):
    d, d0, pitch, fu, fub, gm2 = F(20), F(22), F(70), F(430), F(800), F(5, 4)
    gauge, width = F(109), F(250)
    edge = (width - gauge) / 2
    k = min(F(5, 2), F(14, 5) * edge / d0 - F(17, 10), F(7, 5) * gauge / d0 - F(17, 10))
    a_end = min(F(1), min(upper_end, lower_end) / (3 * d0), fub / fu)
    a_inner = min(F(1), pitch / (3 * d0) - F(1, 4), fub / fu)
    factor = k * fu * d * thickness / gm2
    return factor * a_end, factor * a_inner


if __name__ == "__main__":
    demand = F(450000, 12)
    for label, thickness, top, bottom in [("base", F(20), F(100), F(100)), ("thin_near_end", F(5), F(100), F(132, 5))]:
        end, interior = bearing(thickness, top, bottom)
        resistance = min(end, interior)
        print(label, {"end_N": float(end), "interior_N": float(interior), "demand_N": float(demand),
                      "governing_N": float(resistance), "utilization": float(demand / resistance)})
    forces = [(-F(109, 2), -demand)] * 6 + [(F(109, 2), -demand)] * 6
    print("equilibrium", {"vertical_N": float(sum(v for _, v in forces)),
                          "normal_moment_Nmm": float(sum(x * v for x, v in forces))})
