"""Independent analytical benchmarks for the limited top-flush CJP component.
Decimal precision40; no Checker imports; no complete-joint or experimental validation.
The C1-shaped example has S355 plate and a top-flush CJP detail, unlike published C1.
"""
from decimal import Decimal as D, getcontext
getcontext().prec = 40
for width, thickness in [('200','12'),('209.3','15.6')]:
    area = D(width)*D(thickness)
    print('bf,tf,normal area,EC equivalent,NTC equivalent:',
          width,thickness,area,area*D(275),area*D(275)/D('1.05'))
leg, web, clear, shear = map(D,('8','10.1','476.5','300000'))
throat = leg/D(2).sqrt()
limit = D(410)/(D('.85')*D('1.25'))
for gamma in [D(1),D('1.05')]:
    normal = D(275)/gamma*web/(2*leg)
    parallel = shear/(2*throat*clear)
    equivalent = (4*normal**2+3*parallel**2).sqrt()
    shear_boundary = 2*throat*clear*((limit**2-4*normal**2)/3).sqrt()
    print('gammaM0,web sigma=tauPerp,tauParallel,eq,limit,u,Vboundary:',
          gamma,normal,parallel,equivalent,limit,equivalent/limit,shear_boundary)
    if gamma == 1:
        shortened = clear-2*throat
        normal_short = normal*clear/shortened
        parallel_short = shear/(2*throat*shortened)
        print('EC nominal web length,sigma,tauParallel,eq:',shortened,normal_short,parallel_short,
              (4*normal_short**2+3*parallel_short**2).sqrt())
for lower_leg in [D(8),D(12)]:
    compression = -D(275)*D('15.6')/(2*lower_leg)
    print('Lower leg,no bearing,sigma,tauPerp,eq:',lower_leg,compression,compression,-2*compression)
print('Upper actual inner toe12: throat=',D(12)/D(2).sqrt(),
      'clear straight outstand=',(D('209.3')-D('10.1'))/2-D('12.7'))
