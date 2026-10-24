"""Independent affine-strain envelope and exact polygon integration; no .NET.
The material tables are reconstructed from their declared input formulas, not
read from a Checker result. Limits include actual bar radius and pure-compression
pivot. This is a numerical reference, not experimental evidence or a global proof.
"""
import math,json
from pathlib import Path
P=Path(__file__).resolve().parent
FCK=25.; FYD=450/1.15; E=200000.; AS=math.pi*20**2/4
RECT=[(-150.,-250.),(150.,-250.),(150.,250.),(-150.,250.)]
BARS=[(-100.,-200.,10.),(0.,-200.,10.),(100.,-200.,10.)]

def table(name):
    fcm=FCK+8
    ey={'Bilinear':.00175,'ParabolaRectangle':.002}.get(name,.7*fcm**.31/1000)
    eu=.0035; fcd=.85*FCK/1.5
    if name=='Bilinear': return [(0.,0.),(ey,fcd),(eu,fcd)],ey,eu
    q=[ey*i/8 for i in range(9)]
    if name=='ParabolaRectangle': q+=[eu]; stress=lambda x:fcd*(1-(1-min(1,x/ey))**2)
    else:
        q += [ey+(eu-ey)*i/4 for i in range(1,5)]
        k=1.05*22000*(fcm/10)**.3*ey/fcm
        stress=lambda x:fcd*(k*(x/ey)-(x/ey)**2)/(1+(k-2)*(x/ey))
    return [(x,stress(x)) for x in q],ey,eu

def clip(poly,a,b,c): # a+b*x+c*y >=0
    out=[]
    for p,q in zip(poly,poly[1:]+poly[:1]):
        fp=a+b*p[0]+c*p[1]; fq=a+b*q[0]+c*q[1]
        if fp>=0: out.append(p)
        if (fp>=0)!=(fq>=0):
            t=fp/(fp-fq);out.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
    return out

def moments(poly):
    a=sx=sy=xx=yy=xy=0.
    for (x,y),(u,v) in zip(poly,poly[1:]+poly[:1]):
        z=x*v-u*y;a+=z;sx+=(x+u)*z;sy+=(y+v)*z
        xx+=(x*x+x*u+u*u)*z;yy+=(y*y+y*v+v*v)*z
        xy+=(2*x*y+x*v+u*y+2*u*v)*z
    return a/2,sx/6,sy/6,xx/12,yy/12,xy/24

def stress(e,t):
    q=-e
    if q<=0:return 0.
    for (a,sa),(b,sb) in zip(t,t[1:]):
        if q<=b+1e-14:return -(sa+(sb-sa)*(q-a)/(b-a))
    return 0.

def resultant(e0,kx,ky,t):
    n=mx=my=0.
    for (a,sa),(b,sb) in zip(t,t[1:]):
        # positive compression q=-e0-kx*x-ky*y, stress=-linear(q)
        poly=clip(clip(RECT,-e0-a,-kx,-ky),b+e0,kx,ky)
        if len(poly)<3:continue
        area,sx,sy,xx,yy,xy=moments(poly)
        slope=(sb-sa)/(b-a); constant=-sa+slope*(a+e0)
        bx=slope*kx;by=slope*ky
        n+=constant*area+bx*sx+by*sy
        mx-=constant*sy+bx*xy+by*yy
        my+=constant*sx+bx*xx+by*xy
    for x,y,r in BARS:
        strain=e0+kx*x+ky*y
        force=(max(-FYD,min(FYD,E*strain))-stress(strain,t))*math.pi*r*r
        n+=force;mx-=force*y;my+=force*x
    return n,mx,my

def roots_on_ray(theta,name,state,target):
    t,ey,eu=table(name); sy=math.cos(theta);sx=-math.sin(theta)
    dc=[sx*x+sy*y for x,y in RECT];dmin,dmax=min(dc),max(dc)
    dbmin=min(sx*x+sy*y-r for x,y,r in BARS);dbmax=max(sx*x+sy*y+r for x,y,r in BARS)
    steel=FYD/E if state=='Elastic' else .075*.9
    ec=ey if state=='Elastic' else eu
    lines=[(-ec,dmax),(-steel,dbmax)]
    if state=='Plastic':lines.append((-ey,dmax-(1-ey/eu)*(dmax-dmin)))
    kmax=min((steel-a)/(b-dbmin) for a,b in lines if b>dbmin)
    knots=[0.,kmax]
    for i,(a,b) in enumerate(lines):
        for c,d in lines[i+1:]:
            if b!=d and 0<(c-a)/(b-d)<kmax:knots.append((c-a)/(b-d))
    knots=sorted(set(knots));grid=[]
    for a,b in zip(knots,knots[1:]):grid += [a+(b-a)*i/16 for i in range(16)]
    grid+=[kmax];result=[]
    for upper in [False,True]:
        def at(k):
            e0=steel+k*dbmin if upper else max(a+b*k for a,b in lines)
            n,mx,my=resultant(e0,-k*sx,-k*sy,t)
            return n-target,mx,my,e0,k
        prev=at(grid[0]);lo=grid[0]
        for hi in grid[1:]:
            curr=at(hi)
            if prev[0]*curr[0]<0:
                a,b=lo,hi;fa=prev[0]
                for _ in range(55):
                    mid=(a+b)/2;v=at(mid)
                    if fa*v[0]<=0:b=mid
                    else:a=mid;fa=v[0]
                v=at((a+b)/2);result.append(dict(theta=theta,N=target+v[0],Mx=v[1],My=v[2],epsilon=v[3],curvature=v[4]))
            prev=curr;lo=hi
    return result

data=[]
for name,state in [('Bilinear','Elastic'),('NonLinear','Plastic')]:
    rows=[]
    for deg in range(181):rows+=roots_on_ray(deg*math.pi/180,name,state,-2e6)
    assert rows
    best=max(rows,key=lambda r:r['Mx'])
    fine=[]
    centre=best['theta']*180/math.pi
    for i in range(-100,101):
        deg=centre+i/100
        if 0<=deg<=180:fine+=roots_on_ray(deg*math.pi/180,name,state,-2e6)
    refined=max(fine,key=lambda r:r['Mx'])
    data.append(dict(diagram=name,state=state,target=-2e6,angular_step_degrees=1,
                     max_Mx_sampled=best,root_count=len(rows),minimum_Mx=min(r['Mx'] for r in rows),
                     local_refined_max_Mx=refined,local_refinement_step_degrees=.01,
                     limitation='Exhaustive 1-degree angular sample with exact constitutive-strip integration; not a certified continuous angular bound.'))
shapes=[('R1',300,500,30,[(-100,-200,10),(0,-200,10),(100,-200,10),(-100,205,8),(100,205,8)]),
        ('R2',400,600,35,[(x,y,10) for x in [-150,-50,50,150] for y in [-250,250]]),
        ('R3',300,500,25,[(-100,-200,10),(0,-200,10),(100,-200,10)]),
        ('C1',600,72,30,[(240*math.cos((22.5+i*45)*math.pi/180),240*math.sin((22.5+i*45)*math.pi/180),10) for i in range(8)]),
        ('C2',800,144,40,[(330*math.cos((15+i*30)*math.pi/180),330*math.sin((15+i*30)*math.pi/180),8) for i in range(12)])]
oracle=[]
for label,w,h,FCK,BARS in shapes:
    RECT=[(-w/2,-h/2),(w/2,-h/2),(w/2,h/2),(-w/2,h/2)] if label[0]=='R' else [(w/2*math.cos(2*math.pi*i/h),w/2*math.sin(2*math.pi*i/h)) for i in range(h)]
    for name in ['ParabolaRectangle','Bilinear','NonLinear']:
        for state in ['Plastic','Elastic']:
            roots=roots_on_ray(0,name,state,0)
            valid=[r for r in roots if r['Mx']>0]
            assert valid,(label,name,state)
            point=max(valid,key=lambda r:r['Mx'])
            oracle.append(dict(section=label,diagram=name,state=state,**point))
report=dict(description=__doc__,R3_compression=data,zero_axial_moment_oracles=oracle,
            R3_tension_lower_Mx=200*180000-50*(3*AS*FYD-180000))
(P/'DomainPointIndependentData.cs').write_text('''// Generated exclusively by derive_domain_migration_oracles.py, no Checker outputs.
namespace ConcreteTests
{
    internal static class DomainPointIndependentData
    {
        internal static readonly (string Section, string Diagram, string State, double Moment)[] ZeroN =
        {
'''+''.join('            ("{section}", "{diagram}", "{state}", {Mx:.17g}),\n'.format(**v) for v in oracle)+'''        };
    }
}
''',encoding='utf-8',newline='\r\n')
(P/'domain_migration_oracles.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
