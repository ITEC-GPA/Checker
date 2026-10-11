"""Independent m/kN/kPa worked examples. No import or output from the C# engine.
EN1997-1:2004 Annex D; closed-form undrained cases. This file only checks frozen expected numbers.
"""
import math
nq=3*math.exp(math.pi/math.sqrt(3))
ng=2*(nq-1)/math.sqrt(3)
q=18*nq*(1+(2/3)*.5)+.5*18*2*ng*(1-.3*2/3)
assert abs(nq-18.401122218708668)<1e-12
assert abs(ng-20.093085194346052)<1e-12
assert abs(q-730.9673600475911)<1e-10
assert abs(q*6/2.3-1906.8713740371945)<1e-9
print('PASS EC7: Nq, Ng, qlim, Rd; Rd=',q*6/2.3,'kN; N/R=',600/(q*6/2.3))
for name,value in [('EC7',75*(math.pi+2)*(1+.2*2/3)+18),
                   ('Hansen additive',75*(math.pi+2)*(1+.2*2/3+.4*.5)+18),
                   ('Vesic multiplicative',75*(math.pi+2)*(1+.2*2/3)*(1+.4*.5)+18)]:
    print(name,value,'kPa')
