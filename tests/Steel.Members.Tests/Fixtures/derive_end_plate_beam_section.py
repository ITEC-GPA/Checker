"""Independent geometric integration, N/mm/MPa; no Model/Checker imports.

The roots are square-minus-quarter-circle regions. The stress integral at
high shear reduces the rectangular web stress and leaves flanges/roots at fy.
Normative comparison: EN1993-1-1:2005 6.2.5,6.2.6,6.2.8 / NTC2018 4.2.4.1.2.
SCI P398 C.1 p103 prints649kNm (catalogue); it is kept separate from exact geometry.
This is an analytical oracle, not experimental validation of a complete joint.
"""
import json
import math
from pathlib import Path


def integrate(h, b, tw, tf, r, fy, gamma, ratio):
    hw = h - 2 * tf
    q = hw / 2
    ar = (1 - math.pi / 4) * r**2
    qr = (5 / 6 - math.pi / 4) * r**3
    jr = (1 - 5 * math.pi / 16) * r**4
    area = 2 * b * tf + tw * hw + 4 * ar
    first_flange = b * tf * (h / 2 - tf / 2)
    first_web = tw * q**2 / 2
    first_roots = 2 * (q * ar - qr)
    wpl = 2 * (first_flange + first_web + first_roots)
    inertia = 2 * (b * tf**3 / 12 + b * tf * (h / 2 - tf / 2)**2)
    inertia += tw * hw**3 / 12 + 4 * (q**2 * ar - 2 * q * qr + jr)
    av = max(area - 2 * b * tf + (tw + 2 * r) * tf, hw * tw)
    # Reduced block fy in the rectangular web, integrated over its positive/negative halves.
    reduction = 0 if ratio <= .5 else (2 * ratio - 1)**2
    m = 2 * fy / gamma * (first_flange + first_roots + (1 - reduction) * first_web)
    return dict(area=area, wpl=wpl, inertia=inertia, wel=inertia/(h/2), av=av,
                vpl=av*fy/(math.sqrt(3)*gamma), rho=reduction, mc=m)


cases = {}
for ntc, gamma in ((False, 1), (True, 1.05)):
    for ratio in (0, .5, .500001, .75, 1):
        cases[f'C1_{"NTC" if ntc else "EC"}_{ratio}'] = integrate(533.1, 209.3, 10.1, 15.6, 12.7, 275, gamma, ratio)
Path(__file__).with_name('end_plate_beam_section_cases.json').write_text(json.dumps(cases, indent=2) + '\n', encoding='utf-8')
print(json.dumps(cases, indent=2))
