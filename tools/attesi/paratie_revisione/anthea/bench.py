from fractions import Fraction as Fr
# guide benchmark: cantilever L=6 m fixed at toe, EI=200000 kNm2, q 1->4 kN/m between depth 1 and 4.5 m
L=Fr(6); EI=Fr(200000)
n=20000
import math
def q(d): return 1+3*(d-1)/3.5
V=M=u=0.0
N=200000; a0,a1=1.0,4.5; h=(a1-a0)/N
for i in range(N):
    d=a0+(i+0.5)*h; P=q(d)*h; a=6-d
    V+=P; M+=P*a; u+=P*a*a*(3*6-a)/(6*200000)
print("V kN",V,"M kNm",M,"u_top mm",u*1000)
# envelope benchmark: H=-6 kN and couple +12 kNm conj. to du/d(depth) at head
H=-6; C=12; Lf=6; EIf=200000
u_H=H*Lf**3/(3*EIf); u_C=-C*Lf**2/(2*EIf)
print("u_top mm", (u_H+u_C)*1000, "M_foot", H*Lf - C)
