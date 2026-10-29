"""Independent scalar oracle. No GPC imports or executable invocation.
NTC 2018 3.3.2, 3.3.6, 3.3.7; direct exposure formula (rather than vm/Iv).
Creates a NEW file only. Existing expected results must never be overwritten.
"""
import json, math, pathlib, sys
target = pathlib.Path(sys.argv[1])
if target.exists():
    raise SystemExit('Refusing to overwrite an independent expected result')
rows=[]
for category, (kr,z0,zmin) in enumerate(((.17,.01,2),(.19,.05,4),(.20,.1,5),(.22,.3,8),(.23,.7,12)), 1):
    for years in (10,50,100,1000):
        for z,ct in ((0,1),(10,1),(30,1.2),(200,1)):
            probability=1 if years==50 else .75*math.sqrt(1-.2*math.log(-math.log(1-1/years)))
            logarithm=math.log(max(z,zmin)/z0)
            exposure=kr**2*ct*logarithm*(7+ct*logarithm)
            rows.append(dict(category=category, years=years,z=z,ct=ct,qp=.625*(27*probability)**2*exposure/1000))
target.write_text(json.dumps(dict(source='Independent Python: NTC2018 3.3.2/3.3.6/3.3.7, vb=27 m/s, rho=1.25 kg/m3',wind=rows),indent=2)+'\n',encoding='utf-8')
