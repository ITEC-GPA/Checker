"""Independent EN6.3 initial-stiffness oracle for SCI C1 GEOMETRY.

No DLL/import of production formulas. This is an analytical recalculation,
not a published SCI stiffness. C1 alpha7.5 is the published graphical value.
Lb70.5 includes assumed actual washers4+4, head15/nut19; those hardware
dimensions and the long column-end continuations are explicit fixture data.
"""
import json
from math import pi


def calculate(extended=True, beta=0):
    col_m = (100 - 12.8) / 2 - .8 * 12.7
    col_e = (258.8 - 100) / 2
    plate_m = (100 - 10.1) / 2 - .8 * 8
    plate_e = (250 - 100) / 2
    plate_end = 2 * plate_m + .625 * plate_e
    column_single = min(2 * pi * col_m, 4 * col_m + 1.25 * col_e)
    plate_single = min(2 * pi * plate_m, 7.5 * plate_m)
    plate_second = min(2 * pi * plate_m, 4 * plate_m + 1.25 * plate_e)
    lc = [column_single, column_single, column_single] if extended else [column_single, column_single]
    coordinates = [-40, 60, 150] if extended else [60, 150]
    # All contiguous group contributions of each row, circular/non-circular separately.
    col_candidates = [[column_single] for _ in coordinates]
    for count in range(2, len(coordinates) + 1):
        for start in range(len(coordinates) - count + 1):
            for i in range(start, start + count):
                end = i in (start, start + count - 1)
                pitch = (coordinates[i + 1] - coordinates[i]) if i == start else (
                    coordinates[i] - coordinates[i - 1] if i == start + count - 1
                    else (coordinates[i + 1] - coordinates[i - 1]) / 2)
                cp = pi * col_m + pitch if end else 2 * pitch
                nc = 2 * col_m + .625 * col_e + pitch / 2 if end else pitch
                col_candidates[i].append(min(cp, nc))
    lc = [min(a) for a in col_candidates]
    lp_inside = [min(plate_single, pi * plate_m + 90, 45 + 7.5 * plate_m - plate_end),
                 min(plate_second, pi * plate_m + 90, 45 + plate_end)]
    lp = [125] + lp_inside if extended else lp_inside
    ms = [40 - .8 * 12, plate_m, plate_m] if extended else [plate_m, plate_m]
    arms = [533.1 - 15.6 / 2 - y for y in coordinates]
    av = 13600 - 2 * 258.8 * 20.5 + (12.8 + 2 * 12.7) * 20.5
    width = 15.6 + 2 * 8 + 5 * (20.5 + 12.7) + 25 + min(25, 46.9 - 8)
    dc = 266.7 - 2 * (20.5 + 12.7)
    k2 = .7 * width * 12.8 / dc
    rows = []
    for h, c, p, m in zip(arms, lc, lp, ms):
        k3 = .7 * c * 12.8 / dc
        k4 = .9 * c * (20.5 / col_m) ** 3
        k5 = .9 * p * (25 / m) ** 3
        k10 = 1.6 * 353 / 70.5
        ke = 1 / (1 / k3 + 1 / k4 + 1 / k5 + 1 / k10)
        rows.append(dict(h=h, lc=c, lp=p, m=m, k3=k3, k4=k4, k5=k5, k10=k10, keff=ke))
    skh = sum(r['keff'] * r['h'] for r in rows)
    skh2 = sum(r['keff'] * r['h'] ** 2 for r in rows)
    z = skh2 / skh
    keq = skh / z
    panel = beta * z / (.38 * av)
    initial = 210000 * z * z / (panel + 1 / k2 + 1 / keq)
    return dict(extended=extended, beta=beta, rows=rows, av=av, beff=width, column_m=col_m,
                column_candidates=col_candidates, k2=k2, zeq=z, keq=keq, initial=initial)


if __name__ == '__main__':
    print(json.dumps([calculate(e, b) for e in (True, False) for b in (0, 1)], indent=2))
