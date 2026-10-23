"""Independent arithmetic, standard Python only; prints, never changes fixtures.
SCI P398 (2013), section4 pp55-59 and exampleD.1 pp129-140.
https://www.steelconstruction.info/images/5/5d/SCI_P398.pdf
N, mm, MPa. Uses exact given dimensions; published rounded numbers/typos are
not treated as exact expected values. UK EC factors apply to this example only.
"""
from math import pi, sqrt
import json

h,b,tw,tf,r,A,I = 453.4,189.9,8.5,12.7,10.2,8550,294000000
fy,fu,gm0,gm1,gm2,gm2bolt = 275,410,1,1,1.1,1.25
Ag,An,Ant,Anv = 180*12,(180-2*22)*12,(2*30-22)*12,2*12*(60+2*80-2.5*22)
ncr = pi**2*210000*(180*12**3/12)/72**2
lam=sqrt(Ag*fy/ncr)
phi=(1+.49*(lam-.2)+lam**2)/2
chi=min(1,1/(phi+sqrt(phi**2-lam**2)))
hp,t,d0,ny=340,10,22,3
Aw=(h-2*tf)*tw
Iw=tw*(h-2*tf)**3/12
N,V,M=-150000,-150000,-200e6
nw=N*Aw/A
mw=M*Iw/I
mgLeft=mw+.1125e3*V
mgRight=-mw+.1125e3*V
vg=hp*t*fy/(1.27*sqrt(3)*gm0)
vn=(hp-ny*d0)*t*fu/(sqrt(3)*gm2)
nc=hp*t*fy/gm0
nt=.9*(hp-ny*d0)*t*fu/gm2
mel=t*hp**2/6*fy/gm0
av=max(A-2*b*tf+(tw+2*r)*tf,(h-2*tf-2*r)*tw)
rho=max(0,(2*(abs(V)/2)/min(vg,vn)-1))**2
remaining=(1-abs(nw/2)/nc)*(1-rho)*mel
bolt_forces=[(nw/6-mgLeft*y/68437.5,V/6+mgLeft*x/68437.5)
             for x in [42.5,-42.5] for y in [-120,0,120]]
print(json.dumps({
    'flange':{'A':Ag,'An':An,'AntOutside':Ant,'Anv':Anv,'grossN':Ag*fy/gm0,
              'netN':.9*An*fu/gm2,'blockOutside':Ant*fu/gm2+Anv*fy/(sqrt(3)*gm0),
              'Ncr':ncr,'lambda':lam,'chi':chi,'bucklingN':chi*Ag*fy/gm1,
              'coverBoltBearing':(2.8*30/22-1.7)*(60/(3*22))*fu*20*12/gm2bolt},
    'webEachCover':{'grossShear':vg,'netShear':vn,'grossN':nc,'netTension':nt,'elasticM':mel,
                    'N':nw/2,'V':abs(V)/2,'leftM':mgLeft/2,'rightM':mgRight/2,
                    'rho':rho,'remainingM':remaining,
                    'leftNMEquivalentUtilization':abs(mgLeft/2)/remaining},
    'webMember':{'Av':av,'Avnet':av-ny*d0*tw,'grossShear':av*fy/(sqrt(3)*gm0),
                'netShear':(av-ny*d0*tw)*fu/(sqrt(3)*gm2),
                'linearBearingUtilization':max(abs(fx)+abs(fy_) for fx,fy_ in bolt_forces)/139400}
},indent=2))
