from decimal import Decimal as D, getcontext
getcontext().prec = 35
aw = D(428) * D('8.5')
iw = D('8.5') * D(428)**3 / 12
ra, ri = aw / 8550, iw / D(294000000)
j = D(4)*120**2 + D(6)*(D(85)/2)**2
print('aw, iw, ra, ri, J:', aw, iw, ra, ri, j)
for n, v, m in [(150000,150000,200000000),(100000,100000,133000000)]:
    n, v, m = -D(n), -D(v), -D(m)
    nw, mw = ra*n, ri*m
    ft = (1-ra)*n/2 - (1-ri)*m/D('440.7')
    fb = (1-ra)*n/2 + (1-ri)*m/D('440.7')
    ml, mr = mw+D('112.5')*v, -mw+D('112.5')*v
    mx, my = abs(nw)/6+abs(ml)*120/j, abs(v)/6+abs(ml)*D('42.5')/j
    print('top, bottom, nw, mw, ml, mr, max:', ft,fb,nw,mw,ml,mr,(mx*mx+my*my).sqrt())
