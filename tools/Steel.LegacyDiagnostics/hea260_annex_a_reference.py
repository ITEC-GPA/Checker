"""SectionHInteraction3: independent Decimal arithmetic, N/mm/MPa.

EN 1993-1-1:2005 + AC2006/2009, 6.3.1, 6.3.2.3, 6.3.3(4), A.1/A.2;
NCCI SN003b eq.(3), table 3.2 (fork supports, UDL at shear centre).
Model's Wpl/It/Iw are DECLARED inputs; this does not qualify those solutions.
Only the scalar bisection helper is shared with the step-2 reference.
Run beside ipe300_annex_a_reference.py; no third-party or GPC dependencies.
"""
from decimal import Decimal as D, localcontext
import json
from ipe300_annex_a_reference import reduction


def calculate():
    with localcontext() as ctx:
        ctx.prec = 40
        pi = D('3.1415926535897932384626433832795028841971693993751')
        h, b, tf, tw, r = map(D, ('250','260','12.5','7.5','24'))
        hw = h-2*tf
        af, qf, jf = (1-pi/4)*r**2, (D(5)/6-pi/4)*r**3, (1-5*pi/16)*r**4
        area = 2*b*tf+tw*hw+4*af
        iy = 2*(b*tf**3/12+b*tf*((h-tf)/2)**2)+tw*hw**3/12
        iy += 4*((hw/2)**2*af-hw*qf+jf)
        iz = 2*tf*b**3/12+hw*tw**3/12+4*((tw/2)**2*af+tw*qf+jf)
        wel1, wel2 = 2*iy/h, 2*iz/b
        exact_wpl1 = b*tf*(h-tf)+tw*hw**2/4+4*(hw/2*af-qf)
        exact_wpl2 = tf*b**2/2+hw*tw**2/4+4*(tw/2*af+qf)
        declared = dict(Wpl1=D('919789.9015433467'), Wpl2=D('430171.11123419553'),
                        It=D('541937.2876357703'), Iw=D('516240692070.4727'))
        inputs = dict(A=area,Iy=iy,Iz=iz,Wel1=wel1,Wel2=wel2,**declared)
        wpl1, wpl2, it, iw = (declared[k] for k in ('Wpl1','Wpl2','It','Iw'))
        elastic, fy, length = map(D, ('210000','235','4000'))
        n, my, mz = map(D, ('400000','71000000','30000000'))
        g0 = g1 = D(1)
        flange_ct, web_ct = ((b-tw)/2-r)/tf, (hw-2*r)/tw
        # Full compression is Class 1 already: a conservative classification
        # bound for every plate under the combined N/My/Mz stress gradient.
        assert flange_ct <= 9 and web_ct <= 33  # epsilon=1
        nrk, myrk, mzrk = area*fy, wpl1*fy, wpl2*fy
        ny, nz = (pi*pi*elastic*i/length**2 for i in (iy,iz))
        torsion = elastic/D('2.6')*it+pi*pi*elastic*iw/length**2
        nt = torsion*area/(iy+iz)
        ly, lz = (nrk/ny).sqrt(), (nrk/nz).sqrt()
        chi1, chi2 = reduction(ly,D('.34')), reduction(lz,D('.49'))
        mu1, mu2 = (ny-n)/(ny-chi1*n), (nz-n)/(nz-chi2*n)
        mcr_uniform = (nz*torsion).sqrt()
        mcr = D('1.127')*mcr_uniform
        l0, lt = (myrk/mcr_uniform).sqrt(), (myrk/mcr).sqrt()
        kc = D('.94')
        f = min(D(1),1-D('.5')*(1-kc)*(1-2*(lt-D('.8'))**2))
        chi_lt = min(D(1),1/lt**2,reduction(lt,D('.34'),D('.75'),D('.4'))/f)
        # Constant transverse loads -> parabolic moment diagrams in both planes.
        cm10, cm20 = 1+D('.03')*n/ny, 1+D('.03')*n/nz
        threshold = D('.2')/kc*((1-n/nz)*(1-n/nt)).sqrt().sqrt()
        assert l0 > threshold
        a, eccentricity = 1-it/iy, my/n*area/wel1
        q = eccentricity.sqrt()*a
        cm1, cm2 = (cm10+q)/(1+q), cm20
        cm_lt = max(D(1),cm1**2*a/((1-n/nz)*(1-n/nt)).sqrt())
        w1, w2 = min(D('1.5'),wpl1/wel1), min(D('1.5'),wpl2/wel2)
        ry, rz = my*g0/(myrk*chi_lt), mz*g0/mzrk
        blt = D('.5')*a*l0**2*ry*rz
        clt = 10*a*l0**2*ry/((5+lz**4)*cm1)
        dlt = 2*a*l0*ry*rz/((D('.1')+lz**4)*cm1*cm2)
        elt = D('1.7')*a*l0*ry/((D('.1')+lz**4)*cm1)
        npl, lm = n*g0/nrk, max(ly,lz)
        c11 = max(1+(w1-1)*((2-D('1.6')/w1*cm1**2*lm*(1+lm))*npl-blt),wel1/wpl1)
        c12 = max(1+(w2-1)*((2-14*cm2**2*lm**2/w2**5)*npl-clt),D('.6')*(w2/w1).sqrt()*wel2/wpl2)
        c21 = max(1+(w1-1)*((2-14*cm1**2*lm**2/w1**5)*npl-dlt),D('.6')*(w1/w2).sqrt()*wel1/wpl1)
        c22 = max(1+(w2-1)*(2-D('1.6')/w2*cm2**2*lm*(1+lm)-elt)*npl,wel2/wpl2)
        k11 = cm1*cm_lt*mu1*ny/((ny-n)*c11)
        k12 = cm2*mu1*nz/((nz-n)*c12)*D('.6')*(w2/w1).sqrt()
        k21 = cm1*cm_lt*mu2*ny/((ny-n)*c21)*D('.6')*(w1/w2).sqrt()
        k22 = cm2*mu2*nz/((nz-n)*c22)
        nb1, nb2, mb, mzrd = chi1*nrk/g1, chi2*nrk/g1, chi_lt*myrk/g1, mzrk/g1
        axial1, major1, minor1 = n/nb1, k11*my/mb, k12*mz/mzrd
        axial2, major2, minor2 = n/nb2, k21*my/mb, k22*mz/mzrd
        # Separate axial checks may use 6.3.1.2(4); interactions retain full chi.
        reported1 = nrk/g0 if n/ny <= D('.04') else nb1
        reported2 = nrk/g0 if n/nz <= D('.04') else nb2
        values = dict(NCr1=ny,NCr2=nz,NcrTorsional=nt,NcrFlexuralTorsional=nt,
            LambdaSignedAxialBuckling1=ly,LambdaSignedAxialBuckling2=lz,
            ChiAxialBuckling1=chi1,ChiAxialBuckling2=chi2,MCr=mcr,Lambda0LTBuckling=l0,
            LambdaSignedLTBuckling=lt,ChiLTBuckling=chi_lt,LateralTosionalBucklingCapacity=mb,
            CmX0=cm10,CmY0=cm20,Cmx=cm1,Cmy=cm2,CmLT=cm_lt,Mux=mu1,Muy=mu2,
            Wx=w1,Wy=w2,EpsilonX=eccentricity,BLT=blt,CLT=clt,DLT=dlt,ELT=elt,
            Cxx=c11,Cxy=c12,Cyx=c21,Cyy=c22,Kxx=k11,Kxy=k12,Kyx=k21,Kyy=k22,
            BucklingInteraction1Axis=axial1+major1+minor1,BucklingInteraction2Axis=axial2+major2+minor2,
            AxialBuckling1Capacity=reported1,AxialBuckling2Capacity=reported2)
        return dict(inputs=inputs,exact_plastic_moduli=dict(Wpl1=exact_wpl1,Wpl2=exact_wpl2),
            classification=dict(flange_ct=flange_ct,web_ct=web_ct,uniform_compression_class=1,combined_class=1),
            parameters=dict(E=elastic,Fy=fy,N_compression=n,My=my,Mz=mz,L=length,
                            GammaM0=g0,GammaM1=g1,C1=D('1.127'),kc=kc,susceptible_limit=threshold),
            terms=dict(axial1=axial1,major1=major1,minor1=minor1,axial2=axial2,major2=major2,minor2=minor2),
            axial_check=dict(N_over_Ncr1=n/ny,N_over_Ncr2=n/nz,
                            interaction_denominator1=nb1,interaction_denominator2=nb2),values=values)


if __name__ == '__main__':
    print(json.dumps(calculate(),default=str,indent=2))
