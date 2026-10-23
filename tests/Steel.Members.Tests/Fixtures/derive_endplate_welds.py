"""Independent local paired-fillet envelope; no Checker import.
SCI P398 C.1 p105: legs12/8, not throats. Straight lengths and300kN are explicit test choices.
The source's web-thickness rounding10.2 is shown separately from the10.1 section snapshot.
"""
from decimal import Decimal as D, getcontext
getcontext().prec=40
root2=D(2).sqrt()
fy,fu,gamma0,gamma2,beta=map(D,('275','410','1','1.25','.85'))
flange_t,web_t,flange_leg,web_leg=map(D,('15.6','10.1','12','8'))
flange_length=(D('209.3')-web_t)/2-D('12.7')
web_length=D('533.1')-2*(flange_t+D('12.7'))
strength=fu/(beta*gamma2)
for label,t,leg in [('flange',flange_t,flange_leg),('web',web_t,web_leg)]:
    print(label,'a=',leg/root2,'C1 half thickness=',t/2)
print('SCI p105 rounded web half thickness=',D('10.2')/2)
print('straight lengths: flange outstand, web=',flange_length,web_length)
for ntc in [False,True]:
    gm0=D('1.05') if ntc else gamma0
    for full in [True,False]:
        l_flange=flange_length if ntc or full else flange_length-2*flange_leg/root2
        l_web=web_length if ntc or full else web_length-2*web_leg/root2
        sf=fy/gm0*flange_t/(2*flange_leg)*flange_length/l_flange
        sw=fy/gm0*web_t/(2*web_leg)*web_length/l_web
        tv=D(300000)/(2*(web_leg/root2)*l_web)
        eq=(4*sw*sw+3*tv*tv).sqrt()
        vmax=2*(web_leg/root2)*l_web*((strength*strength-4*sw*sw)/3).sqrt()
        print('NTC',ntc,'fullsize',full,'Leff=',l_flange,l_web,'sigmaFlange=',sf,
            'sigmaWeb=',sw,'tauWeb=',tv,'eqWeb=',eq,'limit=',strength,'Vmax=',vmax)
print('compression weld8: sigma=',-fy*flange_t/16,'equiv=',fy*flange_t/8)
