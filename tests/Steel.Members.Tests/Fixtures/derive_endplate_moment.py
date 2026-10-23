"""Independent SCI P398 C.1 assembly arithmetic, no Checker imports.
Units: N, mm, Nmm. Exact input dimensions versus rounded published 416kNm.
The declared 1000mm column continuations are a test boundary assumption.
"""
from math import pi, sqrt

def tstub(m, emin, leff1, leff2, t, fy, rows, gm0=1, gm2=1.25, dw=39.55):
    n = min(emin, 1.25*m)
    ew = dw/4
    m1 = .25*leff1*t*t*fy/gm0
    m2 = .25*leff2*t*t*fy/gm0
    b = rows*2*.9*800*353/gm2
    return min((8*n-2*ew)*m1/(2*m*n-ew*(m+n)), (2*m2+n*b)/(m+n), b)

def assemble(extended=True, gm0=1, gm1=1, beta=0, beam_m=649e6):
    # Each tuple is (first,count,capacity,id).
    c=[]; pos=[-40,60,150] if extended else [60,150]
    mc=(100-12.8)/2-.8*12.7; e=(258.8-100)/2
    av=13600-2*258.8*20.5+(12.8+2*12.7)*20.5
    def omega(width):
        x=width*12.8/av
        w1=1/sqrt(1+1.3*x*x); w2=1/sqrt(1+5.2*x*x)
        return 1 if beta<=.5 else w1+2*(1-beta)*(1-w1) if beta<1 else w1+(beta-1)*(w2-w1)
    for count in range(1,len(pos)+1):
        for first in range(len(pos)-count+1):
            # All C1 column intervals are governed by noncircular group lengths.
            cp=2*pi*mc if count==1 else 2*pi*mc+2*(pos[first+count-1]-pos[first])
            nc=4*mc+1.25*e if count==1 else 4*mc+1.25*e+(pos[first+count-1]-pos[first])
            c.append((first,count,tstub(mc,75,min(cp,nc),nc,20.5,265,count,gm0), 'column T-stub'))
            width=min(cp,nc)
            c.append((first,count,omega(width)*width*12.8*265/gm0,'column web'))
    offset=1 if extended else 0
    m=(100-10.1)/2-.8*8
    for first,count,cp,nc in [(0,1,2*pi*m,7.5*m),(1,1,2*pi*m,4*m+1.25*75),(0,2,2*pi*m+180,7.5*m+90)]:
        c.append((first+offset,count,tstub(m,75,min(cp,nc),nc,25,265,count,gm0),'internal T-stub'))
        c.append((first+offset,count,min(cp,nc)*10.1*275/gm0,'beam web'))
    if extended:
        c.append((0,1,tstub(30.4,50,125,125,25,265,1,gm0),'external T-stub'))
    beff=15.6+2*8+5*(20.5+12.7)+50
    lam=.932*sqrt(beff*200.3*265/(210000*12.8**2))
    rho=1 if lam<=.72 else min(1,(lam-.2)/lam**2)
    comp=min(omega(beff)*beff*12.8*265/gm0,rho*omega(beff)*beff*12.8*265/gm1)
    c.append((0,len(pos),comp,'column compression'))
    c.append((0,len(pos),beam_m/(533.1-15.6),'beam compression'))
    if beta:
        c.append((0,len(pos),.9*av*265/(sqrt(3)*gm0*beta),'column panel'))
    levers=[533.1-15.6/2-y for y in pos]; forces=[]; ref=None
    threshold=1.9*.9*800*353/1.25
    for row,h in enumerate(levers):
        available=[(res-sum(forces[a:a+n]),tag) for a,n,res,tag in c if a<=row<a+n]
        if ref is not None: available.append((forces[ref]*h/levers[ref],'1.9Ft elastic row limit'))
        force,tag=min(available); force=max(0,force); forces.append(force)
        if ref is None and force>threshold: ref=row
        print('row',row+1,'lever',h,'capacity',force,'limit',tag)
    moment=sum(h*f for h,f in zip(levers,forces))
    print('compression=',sum(forces),'moment=',moment,'kNm=',moment/1e6)
    return moment

for label,kw in [('C1 exact',{}),('flush geometry',{'extended':False}),
                 ('NTC factors',{'gm0':1.05,'gm1':1.05}),('single-sided beta1',{'beta':1}),
                 ('low beam compression',{'beam_m':100e6})]:
    print(label); assemble(**kw)
