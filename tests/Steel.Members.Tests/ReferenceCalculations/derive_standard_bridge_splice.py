"""Independent static oracle, no GPC/.NET calls. N, mm, MPa.

Run once to CREATE a NEW fixture; never overwrite a committed regression fixture.
Rectangle integrals, equal-stiffness bolt statics and free-body cuts are implemented
directly. The example geometry is chosen for the balanced standard domain; it is
NOT presented as the full SCI P357 bridge. Published P357 checks have separate tests.
"""
import json
import math
import sys
from fractions import Fraction as F
from pathlib import Path


def example(unequal):
    top = (F(400), F(30))
    web = (F(320), F(16))
    bottom = (F(420 if unequal else 400), F(35 if unequal else 30))
    height = top[1] + web[0] + bottom[1]
    areas = [b*t for b,t in (top,web,bottom)]
    ys = [height-top[1]/2, bottom[1]+web[0]/2, bottom[1]/2]
    area = sum(areas)
    yc = sum(a*y for a,y in zip(areas,ys))/area
    ys = [y-yc for y in ys]
    own = [top[0]*top[1]**3/12,web[1]*web[0]**3/12,bottom[0]*bottom[1]**3/12]
    inertia = sum(j+a*y*y for j,a,y in zip(own,areas,ys))
    n,v,m = F(200000),F(-100000),F(50000000)
    ns = [n*a/area-m*a*y/inertia for a,y in zip(areas,ys)]
    ms = [m*j/inertia for j in own]
    covers = {}
    for name,index,outward in [('Top',0,1),('Bottom',2,-1)]:
        parent_t = (top if index==0 else bottom)[1]
        offset = outward*(parent_t+20)/2
        outer = (ns[index]-ms[index]/offset)/2
        inner = (ns[index]-outer)/2
        covers[name+'Outer'] = (F(280),F(20),[F(-90),F(90)],F(0),outer,F(0),F(0))
        covers[name+'InnerNegative'] = (F(140),F(20),[F(-90)],F(-90),inner,F(0),F(0))
        covers[name+'InnerPositive'] = (F(140),F(20),[F(90)],F(90),inner,F(0),F(0))
    for name in ('WebNegative','WebPositive'):
        covers[name] = (F(280),F(25),[F(-90),F(0),F(90)],F(0),ns[1]/2,v/2,ms[1]/2)
    results = {}
    for name,(b,t,rows,centre,nc,vc,mc) in covers.items():
        bolts = []
        for side in (-1,1):
            # Sign of force TO left/right parent: left positive, right negative.
            sign = -side
            xx = [side*F(a) for a in (70,140,210)]
            cx,cy = sum(xx)/3,sum(rows)/len(rows)
            count = 3*len(rows)
            polar = sum((x-cx)**2+(y-cy)**2 for x in xx for y in rows)
            moment = sign*(mc-cx*vc+(cy-centre)*nc)
            for x in xx:
                for y in rows:
                    fx = sign*nc/count-moment*(y-cy)/polar
                    fy = sign*vc/count+moment*(x-cx)/polar
                    bolts.append((x,y,fx,fy))
        an = (b-len(rows)*26)*t
        inn = t*b**3/12-sum(t*(F(26)**3/12+26*(y-centre)**2) for y in rows)
        wn = inn/(b/2)
        cuts=[]
        # At a column test both limiting sides, and at the physical splice plane.
        for x in sorted(set([q[0] for q in bolts]+[F(0)])):
            for after in (False,True):
                selected=[q for q in bolts if q[0]<x or (after and q[0]==x)]
                nn=sum(q[2] for q in selected)
                vv=sum(q[3] for q in selected)
                mm=sum((q[0]-x)*q[3]-(q[1]-centre)*q[2] for q in selected)
                sigma=abs(nn)/an+abs(mm)/wn
                tau=F(3,2)*abs(vv)/an
                cuts.append(math.hypot(float(sigma),math.sqrt(3)*float(tau)))
        results[name] = dict(Anet=float(an),Inet=float(inn),NetEquivalent=max(cuts),
            MaxBoltShear=max(math.hypot(float(q[2]),float(q[3])) for q in bolts))
    at=(top[0]-52)*top[1]; ab=(bottom[0]-52)*bottom[1]; aw=(web[0]-78)*web[1]
    an=at+ab+aw; ycn=(at*ys[0]+ab*ys[2]+aw*ys[1])/an
    iw=own[1]-sum(web[1]*(F(26)**3/12+26*F(y)**2) for y in (-90,0,90))
    inn=at*(top[1]**2/12+(ys[0]-ycn)**2)+ab*(bottom[1]**2/12+(ys[2]-ycn)**2)+iw+aw*(ys[1]-ycn)**2
    mb=abs(m+ycn*n)+270*abs(v)
    sigma=abs(n)/an+mb*max(height-yc-ycn,yc+ycn)/inn
    tau=F(3,2)*abs(v)/aw
    return dict(unequal=unequal,area=float(area),inertia=float(inertia),centroid=float(yc),covers=results,
        parentNetArea=float(an),parentNetInertia=float(inn),parentNetCentroid=float(ycn),
        parentNetEquivalent=math.hypot(float(sigma),math.sqrt(3)*float(tau)),
        topShearFlow=float(abs(v*areas[0]*ys[0]/inertia)),bottomShearFlow=float(abs(v*areas[2]*ys[2]/inertia)))


if __name__=='__main__':
    output=Path(sys.argv[1])
    with output.open('x',encoding='utf-8',newline='') as stream:
        stream.write(json.dumps({'units':'N, mm, MPa','oracle':'Python Fraction rectangle integrals and independent free-body equilibrium',
            'examples':[example(False),example(True)]},indent=2).replace('\n','\r\n')+'\r\n')
    print(output)
