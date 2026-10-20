"""Independent geometry oracle: exact rectangle integrals, polygon clipping for plastic PNA.
No GPC assembly/output is read. New fixture only; never overwrites an existing file.
Units mm; tuple entries are x0,y0,x1,y1 of nonoverlapping rectangles.
"""
import math, json, sys
from pathlib import Path
out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).with_name("section-oracles.json")
rects = {
    "H": [(-100,130,100,150),(-100,-150,100,-130),(-5,-130,5,130)],
    "C": [(0,135,100,150),(0,-150,100,-135),(0,-135,10,135)],
    "T": [(-100,85,100,100),(-5,-100,5,85)],
    "L": [(0,0,100,12),(0,12,10,150)],
    "RHS": [(-100,140,100,150),(-100,-150,100,-140),(-100,-140,-90,140),(90,-140,100,140)],
    "Rectangular": [(-50,-100,50,100)],
}
def integral(poly):
    cross = [poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly))]
    return (sum(cross)/2, sum((poly[i][0]+poly[(i+1)%len(poly)][0])*cross[i] for i in range(len(poly)))/6,
            sum((poly[i][1]+poly[(i+1)%len(poly)][1])*cross[i] for i in range(len(poly)))/6)
def clip(poly, axis, cut):
    out = []
    for p,q in zip(poly,poly[1:]+poly[:1]):
        a,b = p[axis] <= cut,q[axis] <= cut
        if a: out.append(p)
        if a != b:
            t=(cut-p[axis])/(q[axis]-p[axis])
            out.append(tuple(p[i]+t*(q[i]-p[i]) for i in (0,1)))
    return out
data=[]
for name,rs in rects.items():
    areas=[(x1-x0)*(y1-y0) for x0,y0,x1,y1 in rs]
    A=sum(areas)
    cx=sum(a*(r[0]+r[2])/2 for a,r in zip(areas,rs))/A
    cy=sum(a*(r[1]+r[3])/2 for a,r in zip(areas,rs))/A
    Ix=sum(a*((r[3]-r[1])**2/12+((r[1]+r[3])/2-cy)**2) for a,r in zip(areas,rs))
    Iy=sum(a*((r[2]-r[0])**2/12+((r[0]+r[2])/2-cx)**2) for a,r in zip(areas,rs))
    Ixy=sum(a*((r[0]+r[2])/2-cx)*((r[1]+r[3])/2-cy) for a,r in zip(areas,rs))
    angle=.5*math.atan2(-2*Ixy,Ix-Iy)
    c,s=math.cos(angle),math.sin(angle)
    polys=[]
    for x0,y0,x1,y1 in rs:
        polys.append([(c*(x-cx)+s*(y-cy),-s*(x-cx)+c*(y-cy)) for x,y in [(x0,y0),(x1,y0),(x1,y1),(x0,y1)]])
    I1=(Ix+Iy)/2+math.hypot((Ix-Iy)/2,Ixy)
    I2=(Ix+Iy)/2-math.hypot((Ix-Iy)/2,Ixy)
    w=[]
    for axis in (1,0):
        lo=min(p[axis] for poly in polys for p in poly); hi=max(p[axis] for poly in polys for p in poly)
        for _ in range(100):
            cut=(lo+hi)/2
            if sum(integral(clip(poly,axis,cut))[0] for poly in polys) < A/2: lo=cut
            else: hi=cut
        below=sum(integral(clip(poly,axis,cut))[axis+1] for poly in polys)
        total=sum(integral(poly)[axis+1] for poly in polys)
        w.append(total-2*below)
    data.append(dict(Name=name,Area=A,I1=I1,I2=I2,Wpl1=w[0],Wpl2=w[1],
                     Wel1=I1/max(abs(p[1]) for poly in polys for p in poly),
                     Wel2=I2/max(abs(p[0]) for poly in polys for p in poly)))
for name,R,r in [("CHS",100,90),("Circular",50,0)]:
    A=math.pi*(R*R-r*r); I=math.pi/4*(R**4-r**4); W=4/3*(R**3-r**3)
    data.append(dict(Name=name,Area=A,I1=I,I2=I,Wpl1=W,Wpl2=W,Wel1=I/R,Wel2=I/R))
out.parent.mkdir(parents=True,exist_ok=True)
with out.open("x",encoding="utf8") as f: json.dump(data,f,indent=2)
