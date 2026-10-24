"""Independent analytical 3d.2 benchmark, Decimal precision40, no Checker imports.
This is an implementation check of an idealised nominal model, not an experimental
or independently validated complete packing connection.
"""
from decimal import Decimal as D, getcontext
getcontext().prec = 40
L, B, g, a, force = map(D, ('120', '60', '12', '3', '100000'))
root2 = D(2).sqrt()
couple = force*g/2
A, Iy, Wy = L*B, B*L**3/12, B*L**2/6
# Face0(x,y,z): (F,0,0; 0,-Fg/2,0); face1: (-F,0,0;0,+Fg/2,0).
# Transform face1 with R=diag(1,-1,-1), retaining the gap moment.
# My(z)=-Fg/2+(g/2-z)*F=-z*F. Centre is zero, ends govern.
sigma, tau = couple/Wy, D('1.5')*force/A
eq = (sigma*sigma+3*tau*tau).sqrt()
print('Block A,Iy,Wy:', A,Iy,Wy)
print('Block end moments:', -couple,couple)
print('Block sigmaBound,tauBound,eqBound:', sigma,tau,eq)
print('Block EC/NTC utilizations:',eq/D(275),eq/(D(275)/D('1.05')))
Aw = 2*a*(L+B)
Ixw = a*(2*L*(B/2)**2+2*B**3/12)
Iyw = a*(2*B*(L/2)**2+2*L**3/12)
sx, sz = force/Aw, couple*(L/2)/Iyw
# CCW root tangent; p is inward. Block is at z<0, bead is at -p,-z.
# Normal from block to chord across the physical throat: n=(-p+z)/sqrt2.
# Horizontal roots have sx as parallel shear. Right vertical root p=-x:
sigma_right, tau_right = (sx+sz)/root2, (sx-sz)/root2
eq_horizontal = (2*sz*sz+3*sx*sx).sqrt()
eq_vertical = (sigma_right**2+3*tau_right**2).sqrt()
limit = D(410)/(D('.85')*D('1.25'))
print('Weld a,leg,g-s0-s1:',a,a*root2,g-2*a*root2)
print('Each real loop Aw,Ix,Iy:',Aw,Ixw,Iyw)
print('Weld sx,maximum sz:',sx,sz)
print('Right-root physical sigma,tauPerp:',sigma_right,tau_right)
print('Horizontal/vertical equivalent, limit:',eq_horizontal,eq_vertical,limit)
print('Weld envelope utilization:',max(eq_horizontal,eq_vertical)/limit)
print('EC nominal-end area, NTC nondefective area:',Aw-4*2*a*a,Aw)
print('Pure normal +/-72000: block stress=',D(72000)/A,
      'weld sigma=',D(72000)/(Aw*root2))
print('Pure My=1440000: block stress=',D(1440000)/Wy)
print('Absent face couples -> residual global My=',-force*g)
