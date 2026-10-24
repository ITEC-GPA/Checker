"""Independent SBE4.3 perturbations for the new 2c.4a tests. No Checker imports.

Published geometry is reproduced separately by derive_tying_source_benchmarks.py.
These perturbations are analytical test cases, not additional published examples.
"""
from math import sqrt
import json


def calculate(rows=6, cleat_fu=430, web_fu=430, tw=9, gamma0=1, contact=37):
    g, t, radius, d0, d, mu, fub, ass = tw+100, 10, 11, 22, 20, 1.1, 800, 245
    height=80+(rows-1)*70
    effective=2*min(40,(g-tw-2*radius+d0)/2)+(rows-1)*min(70,g-tw-2*radius+d0)
    m=(g-tw-2*t-1.6*radius)/2
    n=min(40,1.25*m)
    moment=effective*t*t*cleat_fu/(4*mu)
    ew=contact/4
    ft=.9*fub*ass/mu
    ant1,anv1=2*t*(rows-1)*(70-d0),4*t*(40-d0/2)
    ant2,anv2=2*t*(40+(rows-1)*70-(rows-.5)*d0),2*t*(40-d0/2)
    antw,anvw=tw*(rows-1)*(70-d0),2*tw*(40-d0/2)
    def block(ant,anv,fu): return ant*fu/mu+anv*275/(sqrt(3)*gamma0)
    return dict(rows=rows,height=height,
        mode1=(8*n-2*ew)*moment/(2*m*n-ew*(m+n)),
        mode2=(2*moment+n*2*rows*ft)/(m+n),mode3=2*rows*ft,
        angle_bearing=2*rows*2.5*(40/66)*cleat_fu*d*t/mu,
        web_bearing=rows*2.5*(40/66)*web_fu*d*tw/mu,
        angle_block1=block(ant1,anv1,cleat_fu),angle_block2=block(ant2,anv2,cleat_fu),
        web_net=.9*tw*(height-rows*d0)*web_fu/mu,web_block=block(antw,anvw,web_fu))


if __name__=='__main__':
    print(json.dumps(dict(source=calculate(),gamma_m0_1_05=calculate(gamma0=1.05),
        contact_33=calculate(contact=33),cleat_410=calculate(cleat_fu=410),
        web_410=calculate(web_fu=410),rows=[calculate(rows=n) for n in range(2,7)]),indent=2))
