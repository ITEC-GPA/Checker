"""Independent arithmetic for the parallel-flange C legacy (N,mm,MPa).

Mcr model coefficients C1/C2 and It/Iw/Wpl are declared inputs, not independently
certified choices for a web-loaded channel. EN1993-1-1:2005 6.3.2.2 reduction
is qualified conditionally on this Mcr. See LEGACY_CLOSURE_06.txt.
"""
from decimal import Decimal as D, localcontext
import json
from ipe300_annex_a_reference import reduction


def calculate():
    with localcontext() as ctx:
        ctx.prec=40
        pi=D('3.1415926535897932384626433832795028841971693993751')
        h,b,tf,tw,r=map(D,('300','100','16','10','16'))
        hw=h-2*tf
        af=(1-pi/4)*r*r
        qf=(D(5)/6-pi/4)*r**3
        jf=(1-5*pi/16)*r**4
        area=2*b*tf+tw*hw+2*af
        sx=2*b*tf*b/2+tw*hw*tw/2+2*(tw*af+qf)
        xc=sx/area
        iy=2*(b*tf**3/12+b*tf*((h-tf)/2)**2)+tw*hw**3/12
        iy+=2*((hw/2)**2*af-hw*qf+jf)
        iz=2*tf*b**3/3+hw*tw**3/3+2*(tw*tw*af+2*tw*qf+jf)-area*xc*xc
        exact_wpl=b*tf*(h-tf)+tw*hw*hw/4+2*(hw/2*af-qf)
        inputs=dict(A=area,Iy=iy,Iz=iz,Wel1=2*iy/h,Wel2=iz/(b-xc),
                    It=D(354080),Iw=D('78943251406.91194'),Wpl1=D('648295.8139467458'))
        E,fy,L,c1,c2,zg=map(D,('210000','355','5000','1.127','.45','150'))
        nz=pi*pi*E*iz/L**2
        stiffness=E/D('2.6')*inputs['It']+pi*pi*E*inputs['Iw']/L**2
        a=nz*c2*zg
        mcr=c1*nz*stiffness/((a*a+nz*stiffness).sqrt()+a)
        mc=inputs['Wpl1']*fy
        lam=(mc/mcr).sqrt()
        phi=(1+D('.76')*(lam-D('.2'))+lam**2)/2
        chi=reduction(lam,D('.76'))
        eps=(D(235)/fy).sqrt()
        assert (b-tw-r)/tf <= 9*eps
        assert (hw-2*r)/tw <= 72*eps
        return dict(inputs=inputs,exact_Wpl1=exact_wpl,
            parameters=dict(E=E,Fy=fy,Lcr=L,C1=c1,C2=c2,zg=zg,zj=0,k=1,kw=1,
                            alpha=D('.76'),gammaM0=1,gammaM1=1,method='6.3.2.2',class_bending=1),
            scope='Arithmetic with declared Mcr model; not full eccentric channel resistance',
            values=dict(BendingMoment1Capacity=mc,MCr=mcr,LambdaSignedLTBuckling=lam,
                        AlphaLTBuckling=D('.76'),PhiLTBuckling=phi,ChiLTBuckling=chi,
                        LateralTosionalBucklingCapacity=chi*mc))

if __name__=='__main__': print(json.dumps(calculate(),default=str,indent=2))
