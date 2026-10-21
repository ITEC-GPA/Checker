"""Independent SectionHInteraction2 arithmetic (N, mm, MPa), Python 3 only.

EC3:2005 + AC:2006/2009, 6.3.1, 6.3.2.3, 6.3.3(4), A.1/A.2;
NCCI SN003b eq.(3), table 3.1. See LEGACY_CLOSURE_02_IPE300.txt.
It, Iw and the polygonal plastic moduli are DECLARED Model inputs, not
independently qualified torsion/warping solutions. No GPC result is read.
"""
from decimal import Decimal as D, localcontext
import json


def reduction(lam, alpha, beta=D(1), plateau=D('.2')):
    phi = (1 + alpha * (lam - plateau) + beta * lam**2) / 2
    lo, hi = D(0), min(D(1), 1 / lam**2, phi / (beta * lam**2))
    assert lam > plateau and beta * lam**2 * hi**2 - 2 * phi * hi + 1 < 0
    for _ in range(140):
        mid = (lo + hi) / 2
        if beta * lam**2 * mid**2 - 2 * phi * mid + 1 > 0:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def calculate():
    with localcontext() as ctx:
        ctx.prec = 40
        pi = D('3.1415926535897932384626433832795028841971693993751')
        h, b, tf, tw, radius = map(D, ('300', '150', '10.7', '7.1', '15'))
        hw = h - 2 * tf
        af = (1 - pi / 4) * radius**2
        qf = (D(5)/6 - pi/4) * radius**3
        jf = (1 - 5*pi/16) * radius**4
        area = 2*b*tf + tw*hw + 4*af
        iy = 2*(b*tf**3/12 + b*tf*((h-tf)/2)**2) + tw*hw**3/12
        iy += 4*((hw/2)**2*af - hw*qf + jf)
        iz = 2*tf*b**3/12 + hw*tw**3/12 + 4*((tw/2)**2*af + tw*qf + jf)
        wel1, wel2 = 2*iy/h, 2*iz/b
        # Exact first moments of rectangles and root fillets (independent geometry).
        exact_wpl1 = b*tf*(h-tf) + tw*hw**2/4 + 4*(hw/2*af-qf)
        exact_wpl2 = tf*b**2/2 + hw*tw**2/4 + 4*(tw/2*af+qf)
        declared = dict(It=D('199186.91786049944'), Iw=D('125760427779.96912'),
                        Wpl1=D('628365.3126399475'), Wpl2=D('125219.46804033165'))
        inputs = dict(A=area, Iy=iy, Iz=iz, Wel1=wel1, Wel2=wel2, **declared)
        fy, elastic, n, m, length = map(D, ('235','210000','190000','78000000','5000'))
        flange_ct = ((b-tw)/2-radius)/tf
        web_ct = (hw-2*radius)/tw
        # Plastic neutral axis lies in the straight web. Equal flange forces cancel.
        plastic_axis_shift = n/(2*tw*fy)
        assert plastic_axis_shift < (hw-2*radius)/2
        alpha_web = D('.5')+plastic_axis_shift/(hw-2*radius)
        class1_web_limit = 396/(13*alpha_web-1)  # epsilon=1, alpha>0.5
        assert flange_ct <= 9 and web_ct <= class1_web_limit
        g0 = g1 = D('1.05')
        it, iw, wpl1, wpl2 = (declared[k] for k in ('It','Iw','Wpl1','Wpl2'))
        nrk, myrk = area*fy, wpl1*fy
        ny, nz = (pi*pi*elastic*i/length**2 for i in (iy, iz))
        torsion = elastic/D('2.6')*it + pi*pi*elastic*iw/length**2
        nt = torsion*area/(iy+iz)
        ly, lz = (nrk/x for x in (ny,nz))
        ly, lz = ly.sqrt(), lz.sqrt()
        chi1, chi2 = reduction(ly,D('.21')), reduction(lz,D('.34'))
        mu1, mu2 = (ny-n)/(ny-chi1*n), (nz-n)/(nz-chi2*n)
        mcr_uniform = (nz*torsion).sqrt()
        mcr = D('1.77')*mcr_uniform
        l0, lt = (myrk/mcr_uniform).sqrt(), (myrk/mcr).sqrt()
        kc = 1/D('1.33')
        f = min(D(1), 1-D('.5')*(1-kc)*(1-2*(lt-D('.8'))**2))
        chi_lt = min(D(1), 1/lt**2, reduction(lt,D('.34'),D('.75'),D('.4'))/f)
        cm10, cm20 = D('.79')-D('.1188')*n/ny, D('.79')-D('.1188')*n/nz
        threshold = D('.2')/kc*((1-n/nz)*(1-n/nt)).sqrt().sqrt()
        assert l0 > threshold
        a = 1-it/iy
        eccentricity = m/n*area/wel1
        q = eccentricity.sqrt()*a
        cm1, cm2 = (cm10+q)/(1+q), cm20
        cm_lt = max(D(1), cm1**2*a/((1-n/nz)*(1-n/nt)).sqrt())
        w1, w2 = min(D('1.5'),wpl1/wel1), min(D('1.5'),wpl2/wel2)
        rm = m*g0/(myrk*chi_lt)
        # Mz=0, therefore bLT=dLT=0. Remaining auxiliary factors are still checked.
        clt = 10*a*l0**2*rm/((5+lz**4)*cm1)
        elt = D('1.7')*a*l0*rm/((D('.1')+lz**4)*cm1)
        npl, lm = n*g0/nrk, max(ly,lz)
        c11 = max(1+(w1-1)*(2-D('1.6')/w1*cm1**2*lm*(1+lm))*npl, wel1/wpl1)
        c12 = max(1+(w2-1)*((2-14*cm2**2*lm**2/w2**5)*npl-clt), D('.6')*(w2/w1).sqrt()*wel2/wpl2)
        c21 = max(1+(w1-1)*(2-14*cm1**2*lm**2/w1**5)*npl, D('.6')*(w1/w2).sqrt()*wel1/wpl1)
        c22 = max(1+(w2-1)*(2-D('1.6')/w2*cm2**2*lm*(1+lm)-elt)*npl, wel2/wpl2)
        k11 = cm1*cm_lt*mu1*ny/((ny-n)*c11)
        k12 = cm2*mu1*nz/((nz-n)*c12)*D('.6')*(w2/w1).sqrt()
        k21 = cm1*cm_lt*mu2*ny/((ny-n)*c21)*D('.6')*(w1/w2).sqrt()
        k22 = cm2*mu2*nz/((nz-n)*c22)
        nb1, nb2, mb = chi1*nrk/g1, chi2*nrk/g1, chi_lt*myrk/g1
        axial1, bending1, axial2, bending2 = n/nb1, k11*m/mb, n/nb2, k21*m/mb
        # EC3 6.3.1.2(4): the separate axial check may use cross-section
        # resistance for N/Ncr<=.04. The Annex A chain retains full reductions.
        reported_axial1 = nrk/g0 if n/ny <= D('.04') else nb1
        reported_axial2 = nrk/g0 if n/nz <= D('.04') else nb2
        values = dict(NCr1=ny, NCr2=nz, NcrTorsional=nt, NcrFlexuralTorsional=nt,
            LambdaSignedAxialBuckling1=ly, LambdaSignedAxialBuckling2=lz,
            ChiAxialBuckling1=chi1, ChiAxialBuckling2=chi2, MCr=mcr,
            Lambda0LTBuckling=l0, LambdaSignedLTBuckling=lt, ChiLTBuckling=chi_lt,
            LateralTosionalBucklingCapacity=mb, CmX0=cm10, CmY0=cm20,
            Cmx=cm1, Cmy=cm2, CmLT=cm_lt, Mux=mu1, Muy=mu2, Wx=w1, Wy=w2,
            EpsilonX=eccentricity, BLT=D(0), CLT=clt, DLT=D(0), ELT=elt,
            Cxx=c11, Cxy=c12, Cyx=c21, Cyy=c22, Kxx=k11, Kxy=k12, Kyx=k21, Kyy=k22,
            BucklingInteraction1Axis=axial1+bending1, BucklingInteraction2Axis=axial2+bending2,
            AxialBuckling1Capacity=reported_axial1, AxialBuckling2Capacity=reported_axial2)
        return dict(inputs=inputs, exact_plastic_moduli=dict(Wpl1=exact_wpl1,Wpl2=exact_wpl2),
            classification=dict(flange_ct=flange_ct,web_ct=web_ct,plastic_web_alpha=alpha_web,
                class1_web_limit=class1_web_limit,combined_class=1),
            parameters=dict(E=elastic, Fy=fy, N_compression=n, My=m, Mz=D(0), L=length,
                            GammaM0=g0, GammaM1=g1, C1=D('1.77'), kc=kc, susceptible_limit=threshold),
            terms=dict(axial1=axial1,bending1=bending1,axial2=axial2,bending2=bending2),
            axial_check=dict(N_over_Ncr1=n/ny,N_over_Ncr2=n/nz,
                interaction_denominator1=nb1,interaction_denominator2=nb2),
            values=values)


if __name__ == '__main__':
    print(json.dumps(calculate(), default=str, indent=2))
