"""Independent closed finite-Winkler-beam solution. No FEM/GPC or external package.

EI*u'''' + k*u = 0; head force H, both ends moment-free, toe shear-free.
Decimal analytic functions and a 4x4 boundary solve at 60 and 80 digits;
independent Simpson integration checks the reactions. Never overwrite a fixture.
"""
from decimal import Decimal as D, localcontext
from pathlib import Path
from math import comb, cos, sin, cosh, sinh
import json
import sys


def trig(x):
    s, c, ts, tc = x, D(1), x, D(1)
    for i in range(1, 300):
        ts *= -x*x / D((2*i)*(2*i+1))
        tc *= -x*x / D((2*i-1)*(2*i))
        new_s, new_c = s+ts, c+tc
        if new_s==s and new_c==c:
            return s,c
        s,c=new_s,new_c
    raise ArithmeticError('Trigonometric series did not converge')


def basis(beta, x, order):
    t=beta*x
    s,c=trig(t); e=t.exp(); ch=(e+1/e)/2; sh=(e-1/e)/2
    dc=(c,-s,-c,s); ds=(s,c,-s,-c)
    return [beta**order*sum(D(comb(order,j))*hyper[j%2]*trigonometric[(order-j)%4]
                           for j in range(order+1))
            for hyper,trigonometric in [((ch,sh),dc),((ch,sh),ds),((sh,ch),dc),((sh,ch),ds)]]


def solve(matrix, rhs):
    a=[list(row)+[value] for row,value in zip(matrix,rhs)]
    for i in range(len(a)):
        pivot=max(range(i,len(a)),key=lambda j: abs(a[j][i]))
        a[i],a[pivot]=a[pivot],a[i]
        factor=a[i][i]; assert factor
        a[i]=[v/factor for v in a[i]]
        for j in range(len(a)):
            if j!=i:
                factor=a[j][i]; a[j]=[v-factor*w for v,w in zip(a[j],a[i])]
    return [row[-1] for row in a]


def calculate(digits):
    with localcontext() as ctx:
        ctx.prec=digits
        rigidity, foundation, force = D('2e14'),D(2),D(10000)
        beta=(foundation/(4*rigidity)).sqrt().sqrt(); cases=[]
        for length in range(3500,10001,500):
            matrix=[basis(beta,D(x),order) for x,order in [(0,2),(0,3),(length,2),(length,3)]]
            a=solve(matrix,[D(0),force/rigidity,D(0),D(0)])
            def derivative(x,order): return sum(v*w for v,w in zip(a,basis(beta,x,order)))
            points=[]
            for j in range(13):
                x=D(length)*j/12
                points.append(dict(depth=float(x),u=float(derivative(x,0)),rotation=float(derivative(x,1)),
                                   moment=0.0 if j in (0,12) else float(rigidity*derivative(x,2)),
                                   shear=float(force) if j==0 else 0.0 if j==12 else float(rigidity*derivative(x,3))))
            # Separate reaction integration with double standard-library functions
            # and composite Simpson, not by reusing the boundary equations.
            floats=[float(v) for v in a]; b=float(beta); panels=8192; h=length/panels
            def u(x):
                t=b*x; c,s,ch,sh=cos(t),sin(t),cosh(t),sinh(t)
                return sum(v*w for v,w in zip(floats,[ch*c,ch*s,sh*c,sh*s]))
            force_sum=moment_sum=0.0
            for j in range(panels+1):
                x=j*h; w=1 if j in (0,panels) else 4 if j%2 else 2
                q=-float(foundation)*u(x); force_sum+=w*q; moment_sum+=w*q*x
            assert abs(force_sum*h/3+float(force))<1e-6
            assert abs(moment_sum*h/3)<.001
            cases.append(dict(length=length,head_displacement=points[0]['u'],points=points))
        return dict(method="Closed solution EI*u'''' + k*u = 0; H at head, M=0 at both ends, V=0 at toe",
                    units='N, mm, Nmm',EI=float(rigidity),k=float(foundation),H=float(force),cases=cases)


if __name__=='__main__':
    target=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'tests/Design.Checks/Fixtures/embedment-winkler-expected.json'
    if target.exists(): raise SystemExit('Refusing to replace an independent expectation: '+str(target))
    first,refined=calculate(60),calculate(80)
    assert first==refined,'Precision sensitivity changes rounded expectations'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(refined,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\r\n')
    print(target)
    print([(c['length'],c['head_displacement']) for c in refined['cases']])
