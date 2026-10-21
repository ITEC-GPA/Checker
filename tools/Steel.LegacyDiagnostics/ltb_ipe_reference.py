"""Independent Decimal IPE300 LTB reference, N/mm/MPa, no GPC dependencies.

EN1993-1-1:2005 6.3.2.3; NCCI SN003b eq.(2), table3.2.
It/Iw and polygonal Wpl are declared inputs; geometry integrated separately.
See LEGACY_CLOSURE_04/05 documents for scope and physical-model limitations.
"""
from decimal import Decimal as D, localcontext
import json
from ipe300_annex_a_reference import calculate as geometry, reduction


def calculate(case):
    assert case in (1, 2)
    with localcontext() as ctx:
        ctx.prec = 40
        g = geometry()
        inputs = g['inputs']
        pi = D('3.1415926535897932384626433832795028841971693993751')
        E, fy, length = D(210000), D(355), D(5000 if case == 1 else 4000)
        c1, c2, kc = map(D, ('1.127','.454','.94') if case == 1 else ('1.348','.630','.86'))
        ncr = pi*pi*E*inputs['Iz']/length**2
        stiffness = E/D('2.6')*inputs['It']+pi*pi*E*inputs['Iw']/length**2
        a = ncr*c2*150
        mcr = c1*ncr*stiffness/((a*a+ncr*stiffness).sqrt()+a)
        mc = inputs['Wpl1']*fy
        lam = (mc/mcr).sqrt()
        phi = (1+D('.34')*(lam-D('.4'))+D('.75')*lam*lam)/2
        f = min(D(1),1-D('.5')*(1-kc)*(1-2*(lam-D('.8'))**2))
        chi = min(D(1),1/lam**2,reduction(lam,D('.34'),D('.75'),D('.4'))/f)
        eps = (D(235)/fy).sqrt()
        # Flange outstand uniformly compressed; web in pure bending.
        assert ((D(150)-D('7.1'))/2-15)/D('10.7') <= 9*eps
        assert (300-2*D('10.7')-30)/D('7.1') <= 72*eps
        return dict(case=case,inputs=inputs,exact_plastic_moduli=g['exact_plastic_moduli'],
                    parameters=dict(E=E,Fy=fy,Lcr=length,C1=c1,C2=c2,kc=kc,zg=D(150),f=f,
                                    gammaM0=1,gammaM1=1,method='6.3.2.3',class_bending=1),
                    values=dict(BendingMoment1Capacity=mc,MCr=mcr,LambdaSignedLTBuckling=lam,
                                AlphaLTBuckling=D('.34'),PhiLTBuckling=phi,ChiLTBuckling=chi,
                                LateralTosionalBucklingCapacity=chi*mc))


if __name__ == '__main__':
    import sys
    print(json.dumps(calculate(int(sys.argv[1])),default=str,indent=2))
