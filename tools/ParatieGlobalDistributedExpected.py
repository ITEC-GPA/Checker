"""Reproduce the independent global-action fixture, created before the transfer implementation.
Exact rational integrals of q(y) and y*q(y), per unit out-of-plane width. Refuses overwrite.
"""
from fractions import Fraction as F
import json
from pathlib import Path

cases = []
for name, top, bottom, qt, qb, width in [
    ('ramp', 0, -6000, 0, 2, 1500),
    ('balanced', 0, -6000, -2, 2, 1500),
    ('partial', -1000, -4500, 1, 4, 2000),
]:
    top, bottom, qt, qb, width = map(F, (top, bottom, qt, qb, width))
    slope = (qt-qb)/(top-bottom)
    intercept = qb-slope*bottom
    h = (intercept*(top-bottom)+slope*(top**2-bottom**2)/2)/width
    first = (intercept*(top**2-bottom**2)/2+slope*(top**3-bottom**3)/3)/width
    cases.append(dict(name=name, top=float(top), bottom=float(bottom), q_top=float(qt), q_bottom=float(qb),
                      width=float(width), horizontal=float(h), first_moment_y=float(first),
                      driving_at_centre_y_2000_radius_10000=float((2000*h-first)/10000)))
path = Path(__file__).resolve().parents[1] / 'tests/EmbeddedWalls.Tests/global-distributed-expected.json'
with path.open('x', encoding='utf-8') as file:
    json.dump(dict(method='Independent exact rational integrals of q(y) and y*q(y), per unit out-of-plane width', cases=cases), file, indent=2)
