"""Independent three-girder benchmark derived from beam flexibility, not the FE code.

For a centre unit force, no girder torsion, equal springs k and spacing a:
  (1 - 3 r)/k = r a³/(3 D), hence each outer reaction r=1/(3+k a³/(3D)).
Each sine harmonic then gives M_i = 2/L * sin(λx)sin(λa_load) / λ² * r_i.
Refuses to overwrite the independently calculated expected values.
"""
from pathlib import Path
import math,json

target=Path(__file__).resolve().parents[1]/'Fixtures/transverse-independent.json'
if target.exists():raise SystemExit('Independent fixture already exists; not overwritten.')
rows=[]
for count in (32,64,128,256,512):
    outer=centre=0.
    for n in range(1,count+1):
        lam=n*math.pi/25;k=12000000*lam**4
        r=1/(3+k*4**3/(3*39062.5));moment=2/25*math.sin(lam*12.5)**2/lam**2
        outer+=r*moment;centre+=(1-2*r)*moment
    rows.append(dict(modes=count,outer=outer,centre=centre,total=2*outer+centre))
target.write_text(json.dumps(dict(source='Three equal girders: independent compatibility equation; EI=12e6 kNm², GJ=0, spacing=4m, D=39062.5kNm, L=25m',rows=rows),indent=2)+'\n',encoding='utf-8')
