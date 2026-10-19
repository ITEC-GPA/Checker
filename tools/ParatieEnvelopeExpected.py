"""Independent exact statics and twice-integrated curvature for phase/case envelopes.
Cantilever fixed at x=L, head x=0; N/mm/Nmm. No FEM or production code.
Point H is positive rightwards, C conjugate to du/dx, P positive downwards.
Writes a new fixture exclusively, never replaces an existing expectation.
"""
from fractions import Fraction as F
from pathlib import Path
import json

L, EI = F(6000), F(200000000000000)
histories = [
    dict(case="A", weight=3, stages=[
        dict(stage="Loaded", actions=[(0, 10000, 0, 20000)]),
        dict(stage="Reversed", actions=[(0, -6000, 12000000, -30000)])]),
    dict(case="B", weight=F(1, 2), stages=[
        dict(stage="Loaded", actions=[(2000, 5000, 0, 30000), (4000, 0, -8000000, 0)])])]

def point(history, stage, x, side):
    n, v, m, u, r = F(history['weight']) * x, F(0), F(0), F(0), F(0)
    for a, h, c, p in (tuple(F(t) for t in action) for action in stage['actions']):
        if x > a or x == a and side == 'Below':
            n += p; v += h; m += h * (x-a) - c
        b = max(x, a)
        # Integral (s-x) [H(s-a)-C] ds / EI, and minus integral M/EI.
        u += (h*((L**3-b**3)/3 - (x+a)*(L**2-b**2)/2 + x*a*(L-b))
              - c*((L**2-b**2)/2-x*(L-b))) / EI
        r -= (h*((L**2-b**2)/2-a*(L-b))-c*(L-b))/EI
    return dict(case=history['case'], stage=stage['stage'], normal=float(n), shear=float(v), moment=float(m), displacement=float(u), rotation=float(r))

rows=[]
for x in map(F, range(0, 6001, 125)):
    for side in (['Below'] if x == 0 else ['Above'] if x == L else ['Above','Below']):
        observations=[point(h,s,x,side) for h in histories for s in h['stages']]
        rows.append(dict(depth=float(x),side=side,observations=observations,
            envelopes={q:dict(minimum=min(observations,key=lambda p:p[q]),maximum=max(observations,key=lambda p:p[q]))
                       for q in ['normal','shear','moment','displacement','rotation']}))
data=dict(method='Exact equilibrium and rational integration of curvature, independent of FEM', length=float(L), EI=float(EI),
          histories=[dict(h,weight=float(h['weight'])) for h in histories], stations=rows)
target=Path(__file__).resolve().parents[1]/'tests/EmbeddedWalls.Tests/envelope-expected.json'
with target.open('x',encoding='utf-8',newline='\r\n') as out:
    json.dump(data,out,indent=2);out.write('\n')
print(target)
