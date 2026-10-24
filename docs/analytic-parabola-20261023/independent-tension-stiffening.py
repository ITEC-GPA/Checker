"""Independent 1D equilibrium: exact stress primitives, 300x500 C25/30, 4 diameter 16.
N in N, M in Nmm, strain dimensionless; compression negative.
No GPC library or solver output is used for the expected values.
"""
import math
import json
fc = .85*25/1.5
ec = 22000*(33/10)**.3
fct = .30*25**(2/3)
cr = fct/ec

def sigma(e):
    if e < -.0035: return 0.
    if e < -.002: return -fc
    if e <= 0: return fc*(1000*e+250000*e*e)
    if e <= cr: return ec*e
    return fct/(1+math.sqrt(500*e))

def F(e):
    if e < -.002: return F(-.002)-fc*(max(e,-.0035)+.002)
    if e <= 0: return fc*(500*e*e+250000*e**3/3)
    if e <= cr: return ec*e*e/2
    u,v=math.sqrt(500*e),math.sqrt(500*cr)
    return F(cr)+2*fct/500*(u-math.log1p(u)-v+math.log1p(v))

def G(e):
    if e < -.002: return G(-.002)-fc*(max(e,-.0035)**2-.002**2)/2
    if e <= 0: return fc*(1000*e**3/3+250000*e**4/4)
    if e <= cr: return ec*e**3/3
    def H(u): return u**3/3-u*u/2+u-math.log1p(u)
    return G(cr)+2*fct/500**2*(H(math.sqrt(500*e))-H(math.sqrt(500*cr)))

def integrate(e,k):
    lo,hi=e-250*k,e+250*k
    df,dg=F(hi)-F(lo),G(hi)-G(lo)
    n,m=300*df/k,-300*(dg-e*df)/k**2
    for y in [-204.,204.]:
        strain=e+k*y
        steel=max(-450/1.15,min(450/1.15,200000*strain))
        force=2*math.pi*16**2/4*(steel-sigma(strain))
        n+=force; m-=force*y
    return n,m

rows=[]
for n,m,e,k in [(0,20,-1.5e-5,-3e-7),(0,45,-.7e-5,-9.5e-7),(-300,60,-1.55e-4,-1.2e-6)]:
    for _ in range(20):
        vn,vm=integrate(e,k); dn,dm=integrate(e+1e-9,k); kn,km=integrate(e,k+1e-11)
        a,b,c,d=(dn-vn)/1e-9,(kn-vn)/1e-11,(dm-vm)/1e-9,(km-vm)/1e-11
        rn,rm=n*1000-vn,m*1e6-vm
        e+=(d*rn-b*rm)/(a*d-b*c); k+=(-c*rn+a*rm)/(a*d-b*c)
    eb,et,bar=e-250*k,e+250*k,e-204*k
    vn,vm=integrate(e,k)
    rows.append(dict(N=n,M=m,Chi=abs(k),EpsB=eb,EpsT=et,EpsBar=bar,SigmaBar=200000*bar,SigmaB=sigma(eb),SigmaT=sigma(et),ResidualN=vn-n*1000,ResidualM=vm-m*1e6))
if __name__=='__main__': print(json.dumps(rows,indent=2))
