"""Independent EN3.4/6.2.2 bolt envelope and SCI P398 C.1 comparison.
No Checker imports. Fourth-row y470 is an explicitly chosen test dimension.
SCI p105 supplies counts/capacities, not this coordinate or a long-joint waiver.
"""
from decimal import Decimal as D, getcontext
getcontext().prec = 40
pi = D('3.141592653589793238462643383279502884197')
d, d0, fu, area, gamma = map(D, ('24','26','800','353','1.25'))
positions = list(map(D, ('-40','60','150','470')))
length = positions[-1] - positions[0]
beta = min(D(1), max(D('.75'), 1-(length-15*d)/(200*d)))
fv = D('.6') * fu * area / gamma
ft = D('.9') * fu * area / gamma
factor = D(2)/7
dm = (D(36) + D('39.55')) / 2
print('Lj,beta,Fv0,Fv,Ft:', length,beta,fv,fv*beta,ft)
print('residual per tension bolt:', factor*fv*beta)
print('EN622 counts six tension,two shear-only, unrounded and unreduced:', (2+6*factor)*fv)
print('SCI displayed count approximation using136kN and0.28:', (2+6*D('.28'))*136000)
print('Bolts-only envelope with actual Lj:', (2+6*factor)*fv*beta)
for thickness in [D(25),D('20.5'),D(8),D(9)]:
    print('punching t=',thickness,'dm=',dm,':', D('.6')*pi*dm*thickness*410/gamma)
print('baseline tension-bolt interaction:', D(30000)/(fv*beta) + 1/D('1.4'))
for i,y in enumerate(positions):
    end = min(y+90, 580-y)
    pitch = positions[1]-y if i==0 else y-positions[i-1] if i==3 else min(y-positions[i-1],positions[i+1]-y)
    alpha = min(D(1), end/(3*d0), pitch/(3*d0)-D('.25'))
    print('row',i+1,'end',end,'pitch',pitch,'plate bearing',D('2.5')*alpha*410*d*25/gamma)
print('Extreme-row regressions: independent end/pitch mechanism envelope on each ply')
for name, y, end in [('lower',D('212.4'),D('302.4')),('upper',D(60),D(60))]:
    pitch=D('62.4')
    alpha_inner=pitch/(3*d0)-D('.25')
    for t in [D(9),D('20.5')]:
        # Column free-end continuation is1000mm for either tested extreme.
        end_for_ply=D(1000) if t==D('20.5') else end
        base=D('2.5')*410*d*t/gamma
        end_resistance=min(D(1),end_for_ply/(3*d0))*base
        pitch_resistance=min(D(1),alpha_inner)*base
        print(name,'t=',t,'alpha_inner=',alpha_inner,'end=',end_resistance,
            'pitch=',pitch_resistance,'selected=',min(end_resistance,pitch_resistance))
