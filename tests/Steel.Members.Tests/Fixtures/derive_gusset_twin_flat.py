"""Independent scalar benchmark; no production assembly or fixture generation.

References: EN1993-1-8:2005 Table3.4, 3.10.2; EN1993-1-1:2005 6.2.3;
SCI P358 (2014), section8.2 (flat bracing), p179 (symmetric block paths).
This is a constructed analytical example, not a published whole-joint test.
Dimensions mm, strengths MPa, force N. The support attachment is not assessed.
"""
import json
import math

def benchmark(gamma0):
    n, count, d, d0, area = 100000.0, 4, 20.0, 21.0, 245.0
    gamma2, gauge, pitch, rows = 1.25, 80.0, 60.0, 2
    result = {"bolt_count": count, "overlap": 150.0,
              "bolt_demand": n / count,
              "bolt_shear_resistance": 2 * .6 * 800 * area / gamma2}
    for name, width, thickness, end, demand in [
        ("gusset", 200.0, 16.0, 40.0, n),
        ("each_brace", 160.0, 10.0, 50.0, n / 2)]:
        fy, fu = 275.0, 430.0
        edge = (width - gauge) / 2
        ag, an = width * thickness, (width - 2 * d0) * thickness
        ant_i = (gauge - d0) * thickness
        ant_o = (2 * edge - d0) * thickness
        anv = 2 * thickness * (end + (rows - 1) * pitch - (rows - .5) * d0)
        k1 = min(2.5, 2.8 * edge / d0 - 1.7, 1.4 * gauge / d0 - 1.7)
        result[name] = {"demand": demand, "ag": ag, "anet": an, "ant_inside": ant_i,
            "ant_outside": ant_o, "anv": anv,
            "gross": ag * fy / gamma0, "net": .9 * an * fu / gamma2,
            "block_inside": fu * ant_i / gamma2 + fy * anv / (math.sqrt(3) * gamma0),
            "block_outside": fu * ant_o / gamma2 + fy * anv / (math.sqrt(3) * gamma0),
            "bearing_end": k1 * min(end / (3 * d0), 800 / fu, 1) * fu * d * thickness / gamma2,
            "bearing_inner": k1 * min(pitch / (3 * d0) - .25, 800 / fu, 1) * fu * d * thickness / gamma2}
    return result

if __name__ == "__main__":
    print(json.dumps({"ec_explicit": benchmark(1), "ntc2018": benchmark(1.05)}, indent=2))
