"""Independent NTC 2018 axial-member examples, in N/mm/MPa.

Uses only Python's standard library and closed-form circle/annulus properties.
No GPC output or compiled assembly is read. Creates a NEW destination; never
overwrites an expected file. Formulae: NTC 4.2.6, 4.2.7, 4.2.10, 4.2.42-46.
"""
import json
import math
import sys


def example(name, diameter, thickness, fy, fu, length1, length2, alpha, net=None):
    inner = diameter - 2 * thickness if thickness else 0
    area = math.pi / 4 * (diameter**2 - inner**2)
    inertia = math.pi / 64 * (diameter**4 - inner**4)
    answer = dict(name=name, area=area, inertia=inertia, yield_capacity=area * fy / 1.05)
    if net is not None:
        answer["fracture_capacity"] = 0.9 * net * fu / 1.25
    if length1 is not None:
        answer["axes"] = []
        for length in (length1, length2):
            critical = math.pi**2 * 210000 * inertia / length**2
            slenderness = math.sqrt(area * fy / critical)
            phi = (1 + alpha * (slenderness - 0.2) + slenderness**2) / 2
            chi = 1 if slenderness <= 0.2 else min(1, 1 / (phi + math.sqrt(phi**2 - slenderness**2)))
            answer["axes"].append(dict(ncr=critical, normalized_slenderness=slenderness,
                                       chi=chi, capacity=chi * area * fy / 1.05))
    return answer


result = dict(source="Independent standard-library Python; NTC2018 chapter 4, ordinary static axial steel members",
              units="N, mm, MPa", examples=[
                  example("solid50_tension_net1000", 50, 0, 355, 510, None, None, 0.49, 1000),
                  example("chs355_6x12_5_hot_s355", 355.6, 12.5, 355, 510, 6000, 8000, 0.21),
                  example("chs193_7x5_hot_s460_reduced440", 193.7, 5, 440, 550, 4000, 6000, 0.13),
                  example("chs193_7x5_cold_s460_reduced440", 193.7, 5, 440, 550, 4000, 6000, 0.49),
                  example("solid60_compression_s235", 60, 0, 235, 360, 3000, 4000, 0.49),
              ])
with open(sys.argv[1], "x", encoding="utf-8", newline="\n") as destination:
    json.dump(result, destination, indent=2, allow_nan=False)
    destination.write("\n")
