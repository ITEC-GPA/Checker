"""Independent rational section integration and point-group statics; no GPC import.

The three parent plate forces are integrals of one linear stress field. Cover
forces follow two equilibrium equations, and each bolt group a 3x3 equilibrium
system. Output is an analytical oracle, not a published experimental benchmark.
Run without arguments to print; --output writes a NEW reference JSON explicitly.
"""
from fractions import Fraction as F
import json
import sys
from pathlib import Path

# Units N, mm. Unsymmetric welded I; different flange and face thicknesses.
rectangles = {"Bottom": (F(500), F(0), F(30)),
              "Web": (F(16), F(30), F(830)),
              "Top": (F(400), F(830), F(854))}
area = sum(b*(hi-lo) for b,lo,hi in rectangles.values())
first = sum(b*(hi**2-lo**2)/2 for b,lo,hi in rectangles.values())
yc = first/area
inertia = sum(b*((hi-yc)**3-(lo-yc)**3)/3 for b,lo,hi in rectangles.values())
N,V,M = F(240000), F(-360000), F(480000000)
c0,c1 = N/area,-M/inertia
components = {}
for name,(b,lo,hi) in rectangles.items():
    lo-=yc; hi-=yc; y=(lo+hi)/2
    n=b*(c0*(hi-lo)+c1*(hi**2-lo**2)/2)
    moment=-b*(c0*((hi**2-lo**2)/2-y*(hi-lo))
               +c1*((hi**3-lo**3)/3-y*(hi**2-lo**2)/2))
    components[name]={"n":n,"m":moment,"y":y}
assert sum(x["n"] for x in components.values())==N
assert sum(x["m"]-x["y"]*x["n"] for x in components.values())==M
trace={"Section.Area":area,"Section.CentroidYFromBottom":yc,"Section.StrongAxisSecondMoment":inertia,
       "Web.AxialForce":components["Web"]["n"],"Web.LocalMoment":components["Web"]["m"],"Web.OffsetY":components["Web"]["y"]}
groups=[]
def group(side,name,points,fx,fy,m,origin_y,origin_z,is_web):
    # Solve sum Fx, sum Fy, sum(r x F) with F=(u-q*y,v+q*x).
    count=len(points); cx=sum(x for x,y in points)/count; cy=sum(y for x,y in points)/count
    j=sum((x-cx)**2+(y-cy)**2 for x,y in points)
    q=(m-cx*fy+cy*fx)/j
    forces=[(x,y,fx/count-q*(y-cy),fy/count+q*(x-cx)) for x,y in points]
    assert sum(row[2] for row in forces)==fx
    assert sum(row[3] for row in forces)==fy
    assert sum(x*v-y*u for x,y,u,v in forces)==m
    groups.append(dict(side=side,cover=name,forces=[[float(v) for v in row] for row in forces]))
    return [(x,origin_y+(y if is_web else 0),origin_z+(0 if is_web else y),u,v if is_web else 0,0 if is_web else v) for x,y,u,v in forces]
for side,sign,xsign in [("Left",F(1),F(-1)),("Right",F(-1),F(1))]:
    spatial=[]
    for name,tf,to,ti,gap,bi,rows,pitch,gauge in [
        ("Top",24,16,18,50,160,3,90,180),("Bottom",30,20,16,50,200,4,95,200)]:
        outward=F(1 if name=="Top" else -1)
        yo=outward*F(tf+to,2); yi=-outward*F(tf+ti,2)
        n,m,y=(components[name][k] for k in ["n","m","y"])
        fo=(-m-yi*n)/(yo-yi); fi=n-fo
        assert fo+fi==n and -yo*fo-yi*fi==m
        for key,value in [("ParentAxial",n),("ParentLocalMoment",m),("OuterForce",fo),("BothInnerForce",fi)]:
            trace[f"{side}.{name}.{key}"]=sign*value
        points=[(xsign*(80+i*pitch),F(z*gauge,2)) for i in range(rows) for z in [-1,1]]
        spatial+=group(side,name+"Outer",points,sign*fo,F(0),F(0),y+yo,F(0),False)
        for z,suffix in [(-1,"Negative"),(1,"Positive")]:
            centre=F(z*(gap+bi),2)
            pts=[p for p in points if p[1]*z>0]
            spatial+=group(side,name+"Inner"+suffix,pts,sign*fi/2,F(0),-centre*sign*fi/2,y+yi,F(0),False)
    pts=[(xsign*(80+i*100),F(j-2)*140) for i in range(3) for j in range(5)]
    for z,suffix in [(-1,"Negative"),(1,"Positive")]:
        spatial+=group(side,"Web"+suffix,pts,sign*components["Web"]["n"]/2,sign*V/2,
                       sign*components["Web"]["m"]/2,components["Web"]["y"],F(z*28,2),True)
    result=[sum(v[i] for v in spatial) for i in range(3,6)]
    result += [sum(y*fz-z*fy for x,y,z,fx,fy,fz in spatial),
               sum(z*fx-x*fz for x,y,z,fx,fy,fz in spatial),
               sum(x*fy-y*fx for x,y,z,fx,fy,fz in spatial)]
    assert result==[sign*N,sign*V,0,0,0,sign*M]
data=dict(description="Exact rational integrals and independent equilibrium; N/mm, no GPC DLLs.",
          trace={k:float(v) for k,v in trace.items()},groups=groups)
out=json.dumps(data,indent=2)+"\n"
if len(sys.argv)==3 and sys.argv[1]=="--output":
    path=Path(sys.argv[2]); path.parent.mkdir(parents=True,exist_ok=True); path.write_text(out,encoding="utf-8")
else: print(out)
