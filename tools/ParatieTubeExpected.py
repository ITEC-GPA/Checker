"""Independent CHS plastic N-M-V references, N/mm/MPa; no GPC imports.

Integrate disk caps numerically in angle at prescribed neutral-axis offsets.
The production solver will instead invert the normal force using cap formulae.
NTC2018 4.2.4.1.2, 4.2.23 and 4.2.40. For V>0.5Vrd the yield reduction is
deliberately applied to the whole annulus, conservatively, not only its shear area.
Creates a new file; never replaces an expected result.
"""
import json
import math
import sys


def simpson(function, low, high, intervals=16384):
    step = (high-low)/intervals
    return step/3 * (function(low)+function(high)+sum(
        (4 if i % 2 else 2)*function(low+i*step) for i in range(1, intervals)))


def cap(radius, offset):
    if offset >= radius:
        return 0.0, 0.0
    theta = math.asin(offset/radius)
    area = 2*radius**2*simpson(lambda a: math.cos(a)**2, theta, math.pi/2)
    first = 2*radius**3*simpson(lambda a: math.sin(a)*math.cos(a)**2, theta, math.pi/2)
    return area, first


examples = []
for name, diameter, thickness, fy, offset_fraction, shear_fraction in [
    ('pure_bending', 200., 8., 355., 0., 0.),
    ('combined_low_shear', 200., 8., 355., .2, .4),
    ('combined_half_shear', 200., 8., 355., .8, .5),
    ('combined_high_shear', 200., 8., 355., .8, .75),
    ('near_axial_limit', 200., 8., 355., .99, .9),
    ('class_two', 200., 3., 235., .4, .6),
]:
    outer, inner = diameter/2, diameter/2-thickness
    area = math.pi*(outer**2-inner**2)
    inertia = math.pi/4*(outer**4-inner**4)
    shear_capacity = 2*area/math.pi*fy/(math.sqrt(3)*1.05)
    reduction = max(0., 2*shear_fraction-1)**2
    yield_design = fy/1.05*(1-reduction)
    offset = offset_fraction*outer
    a1, q1 = cap(outer, offset)
    a2, q2 = cap(inner, offset)
    normal = yield_design*(area-2*(a1-a2))
    if offset == 0:
        normal = 0.0
    moment = 2*yield_design*(q1-q2)
    examples.append(dict(name=name, diameter=diameter, thickness=thickness, fy=fy,
        area=area, inertia=inertia, neutral_axis=offset, normal=normal,
        shear=shear_fraction*shear_capacity, shear_capacity=shear_capacity,
        reduction=reduction, normal_capacity=area*yield_design, moment_capacity=moment))
with open(sys.argv[1], 'x', encoding='utf8', newline='\n') as output:
    json.dump(dict(units='N, mm, MPa', method='Independent angular Simpson integration, 16384 intervals', examples=examples), output, indent=2)
    output.write('\n')
