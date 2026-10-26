"""Independent manufactured-solution oracle; no GPC DLL or production output.

Four-row symmetric flush detail, N/mm/MPa. SCI P398 Tables2.2/2.3,
AppendixG and EN1993-1-8 Table6.11. The governing per-row lengths are
identified by hand for pitches90,233.1,90; this is deliberately NOT a
translation of the production group enumerator or inverse solver.
Integration uses two-point Gauss quadrature on each active interval.
Run: python end_plate_reference.py OUTPUT.json
"""
import json, math, sys

E=210000.
m=27.2; e=75.; m2=34.8
l1=m/(m+e); l2=m2/(m+e)
def chart(a):
    x=1.25/(a-2.75); y=a*x/2
    return x if l2>=y else x+(1-x)*((y-l2)/y)**(.185*a**1.785)
left,right=4.45,8.
for _ in range(80):
    mid=(left+right)/2
    if chart(mid)>l1:left=mid
    else:right=mid
alpha=(left+right)/2
# The90mm pairs govern the per-row stiffness lengths in this detail.
lc=2*33.44+.625*79.4+90/2
lp_end=min(2*math.pi*m,alpha*m-(2*m+.625*e)+90/2)
lp_inner=2*m+.625*e+90/2
lengths=[lp_end,lp_inner,lp_inner,lp_end]
k=[]
for lp in lengths:
    compliance=200.3/(.7*lc*12.8)+33.44**3/(.9*lc*20.5**3)+m**3/(.9*lp*25**3)+70.5/(1.6*353)
    k.append(E/compliance)
z=[206.55,116.55,-116.55,-206.55]
kc=.7*E*206.6*12.8/200.3
bands=[(250.95,266.55),(-266.55,-250.95)]
def integral(f,lo,hi,u,t):
    point=-u/t if t else float('inf')
    knots=[lo,point,hi] if lo<point<hi else [lo,hi]
    total=0.
    for a,b in zip(knots,knots[1:]):
        center=(a+b)/2; half=(b-a)/2; offset=half/math.sqrt(3)
        total+=half*(f(center-offset)+f(center+offset))
    return total
def evaluate(name,u,t):
    forces=[s*max(u+t*p,0) for s,p in zip(k,z)]
    contacts=[]; km=[0.,0.,0.]; energy=sum(f*max(u+t*p,0)/2 for f,p in zip(forces,z))
    for p,s in zip(z,k):
        if u+t*p>0:
            for j in range(3):km[j]+=s*p**j
    for lo,hi in bands:
        density=kc/(hi-lo)
        f=lambda p:density*min(u+t*p,0)
        c=-integral(f,lo,hi,u,t); moment=integral(lambda p:p*f(p),lo,hi,u,t)
        closed=integral(lambda p:float(u+t*p<0),lo,hi,u,t)
        for j in range(3):km[j]+=integral(lambda p:density*p**j if u+t*p<0 else 0,lo,hi,u,t)
        energy+=integral(lambda p:density*min(u+t*p,0)**2/2,lo,hi,u,t)
        contacts.append(dict(compression=c,moment=moment,closed=closed,peak=max(-f(lo),-f(hi))))
    n=sum(forces)-sum(c['compression'] for c in contacts)
    moment=sum(f*p for f,p in zip(forces,z))+sum(c['moment'] for c in contacts)
    # Net column rectangles are integrated directly about their own centroid.
    rectangles=[(258.8-52,20.5,123.1),(12.8,225.7,0),(258.8,20.5,-123.1)]
    area=sum(b*h for b,h,c in rectangles); centroid=sum(b*h*c for b,h,c in rectangles)/area
    inertia=sum(b*h**3/12+b*h*(c-centroid)**2 for b,h,c in rectangles)
    modulus=inertia/max(abs(133.35-centroid),abs(-133.35-centroid))
    # Sweep external and internal force events, independent of production envelope.
    upper_shear=n/2
    knots=sorted(set(z+[v for band in bands for v in band]),reverse=True)
    panels=[abs(upper_shear),abs(upper_shear-n)]
    for pos in knots:
        above=sum(f for f,p in zip(forces,z) if p>pos)
        for lo,hi in bands:
            if pos<hi:
                above+=integral(lambda p:kc/(hi-lo)*min(u+t*p,0),max(lo,pos),hi,u,t)
        panels.extend([abs(upper_shear-above),abs(upper_shear-above-sum(f for f,p in zip(forces,z) if p==pos))])
    panel=max(panels)
    return dict(name=name,u=u,theta=t,n=n,moment=moment,rowForces=forces,contacts=contacts,tangent=km,
                energy=energy,panel=panel,columnNetArea=area,columnCentroid=centroid,columnInertia=inertia,columnModulus=modulus)

states=[('tension',.04,0),('compression',-.04,0),('positive_moment',0,.0002),('negative_moment',0,-.0002),
        ('tension_bending',.025,.00015),('compression_bending',-.025,-.00015),
        ('partial_upper_contact',-.025875,.0001),('partial_lower_contact',-.025875,-.0001),
        ('near_limit',.08,.0004),('beyond_limit',.32,.0016),('high_utilization',.05,.00025)]
out=dict(source='Independent manufactured solution, not an experimental whole-joint validation',alpha=alpha,
         rowStiffness=k,contactStiffness=kc,cases=[evaluate(*s) for s in states])
with open(sys.argv[1],'w',encoding='utf-8') as f:json.dump(out,f,indent=2,allow_nan=False)
print('Generated',len(states),'independent cases; row stiffness',k)
