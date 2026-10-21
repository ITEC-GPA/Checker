"""Independent rectangle integration for five SAP legacy setups (N,mm,MPa).

No reproduction of SAP's complete member check. Unsupported contracts remain
unsupported. Numerical outputs qualify only the named available components.
"""
from decimal import Decimal as D, localcontext
import json


def calculate(case):
    with localcontext() as ctx:
        ctx.prec=40
        assert 7<=case<=11
        if case in (7,8):
            h,tw,bt,tt,bb,tb=map(D,(400,12,200,10,300,25) if case==7 else (300,25,300,25,150,25))
            rects=[(bt,tt,D(0),h-tt/2),(tw,h-tt-tb,D(0),(h+tb-tt)/2),(bb,tb,D(0),tb/2)]
            length=D(12000)
        elif case in (9,10):
            h,b,tw,tf=map(D,(400,200,40 if case==9 else 45,50))
            rects=[(b,tf,D(0),h-tf/2),(tw,h-tf,D(0),(h-tf)/2)]
            length=D(1000 if case==9 else 12000)
        else:
            # Bottom horizontal leg plus non-overlapping vertical remainder.
            rects=[tuple(map(D,(350,80,175,40))),tuple(map(D,(40,270,20,215)))]
            length=D(1000)
        area=sum(w*d for w,d,x,y in rects)
        xc=sum(w*d*x for w,d,x,y in rects)/area
        yc=sum(w*d*y for w,d,x,y in rects)/area
        ix=sum(w*d**3/12+w*d*(y-yc)**2 for w,d,x,y in rects)
        iy=sum(d*w**3/12+w*d*(x-xc)**2 for w,d,x,y in rects)
        ixy=sum(w*d*(x-xc)*(y-yc) for w,d,x,y in rects)
        root=((ix-iy)**2+4*ixy**2).sqrt()
        major,minor=(ix+iy+root)/2,(ix+iy-root)/2
        props=dict(Area=area,J11=major,J22=minor)
        values={}
        if case in (7,9,10):
            assert ix>iy and ixy==0
            wel1=ix/max(abs(y+d/2-yc) for w,d,x,y in rects)
            wel1=min(wel1,ix/max(abs(y-d/2-yc) for w,d,x,y in rects))
            wel2=iy/max(w/2 for w,d,x,y in rects)
            props.update(Wel1=wel1,Wel2=wel2)
            if case==10:
                # Equal-area plastic neutral axis is in the web: A/2/tw.
                yp=area/(2*tw)
                assert yp<h-tf
                wpl1=tw*(yp**2+(h-tf-yp)**2)/2+b*tf*(h-tf/2-yp)
                wpl2=sum(d*w*w/4 for w,d,x,y in rects)
                props.update(Wpl1=wpl1,Wpl2=wpl2)
                w1,w2=wpl1,wpl2
            else:w1,w2=wel1,wel2
            pi=D('3.1415926535897932384626433832795028841971693993751')
            # Declared legacy net-area factor=1. Preserve the checker's conservative
            # minimum of yielding and net-section rupture; this is not a hole model.
            nt=min(area*355,D('.9')*area*490/D('1.25'))
            values=dict(AxialTensionCapacity=nt,BendingMoment1Capacity=w1*355,
                        BendingMoment2Capacity=w2*355,NCr1=pi*pi*210000*major/length**2,
                        NCr2=pi*pi*210000*minor/length**2)
        return dict(case=case,rectangles=[dict(width=w,depth=d,x=x,y=y) for w,d,x,y in rects],
                    centroid=dict(x=xc,y=yc),geometric_inertia=dict(Ixx=ix,Iyy=iy,Ixy=ixy),
                    properties=props,values=values,
                    parameters=dict(E=210000,Fy=355,Fu=490,L=length,gammaM0=1,gammaM1=1,gammaM2=D('1.25'),An_over_A=1),
                    classification={7:3,8:1,9:3,10:2,11:3}[case],
                    disposition='Explicit unsupported complete member check; only named components qualified')

if __name__=='__main__':
    import sys
    print(json.dumps(calculate(int(sys.argv[1])),default=str,indent=2))
