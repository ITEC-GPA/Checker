"""Independent pressure balance, calculated in m/kPa before the GPC implementation.

FHWA-IF-99-015 §5.8.2, eq.34-37: bearing pressure Nc*cu versus
gamma*H - cu*H/B'. Uniform surcharges are added by vertical equilibrium.
Nc is assigned and sourced, not inferred from a proprietary model or graph.
NTC 2018 §6.5.3.1.2: A1/M1/R1 and A2/M2/R1, R1=1.
Run without output arguments to inspect; --write refuses to replace a fixture.
"""
from decimal import Decimal as D, getcontext
from pathlib import Path
import json
import sys

getcontext().prec = 50
cases = [
    dict(id='wide', h='4', b='20', d=None, gamma='18', cu='35', nc='6', g2='5', q='10', psi='0.7', shear=False),
    dict(id='deep', h='5', b='10', d=None, gamma='18', cu='25', nc='6', g2='0', q='10', psi='1', shear=True),
    dict(id='stiff-layer', h='6', b='20', d='4', gamma='20', cu='30', nc='6', g2='5', q='20', psi='1', shear=True),
    dict(id='weak', h='8', b='30', d=None, gamma='19', cu='15', nc='6', g2='0', q='20', psi='1', shear=False),
]
rows=[]
for c in cases:
    h,b,gamma,cu,nc,g2,q,psi = [D(c[k]) for k in ('h','b','gamma','cu','nc','g2','q','psi')]
    width = b/D(2).sqrt()
    if c['d'] is not None:
        width=min(width,D(c['d']))
    expected=[]
    for name,g1f,g2f,qf,m in [('characteristic','1','1','1','1'),('A1+M1+R1','1.3','1.5','1.5','1'),('A2+M2+R1','1','1.3','1.3','1.4')]:
        strength=cu/D(m)
        gross=D(g1f)*gamma*h + D(g2f)*g2 + D(qf)*q*psi
        relief=strength*h/width if c['shear'] else D(0)
        net=gross-relief
        cap=nc*strength
        expected.append(dict(combination=name, gross_kpa=float(gross), relief_kpa=float(relief),
            net_kpa=float(net), capacity_kpa=float(cap), utilization=float(max(D(0),net)/cap)))
    rows.append(dict(input=c, width_m=float(width), expected=expected))
payload=dict(units='m, kN/m3, kPa; convert at the test boundary', precision='50 decimal digits',
             tolerance='1e-12 MPa absolute and 1e-11 utilization; no discretization error', cases=rows)
text=json.dumps(payload,indent=2)+'\n'
if '--write' in sys.argv:
    path=Path(__file__).resolve().parents[2]/'tests/EmbeddedWalls.Tests/basal-heave-expected.json'
    with path.open('x',encoding='utf-8',newline='\n') as file:
        file.write(text)
else:
    print(text)
