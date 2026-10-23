"""Independent EN 1993-1-8:2005 Table 6.6 / SCI P398 C.1 arithmetic.

Run directly with Python 3; no Checker imports and no fixture updates.
Lengths mm, stresses N/mm2, forces N. Published C.1 uses rounded intermediates.
Appendix G is a separate approximation, not a fit to C.1 alpha=7.5.
"""
from decimal import Decimal as D, getcontext
from math import pow

getcontext().prec = 40
pi = D('3.141592653589793238462643383279502884197')
m = (D(100) - D('10.1')) / 2 - D('.8') * 8
e, m2, pitch = D(75), D('44.4') - D('.8') * 12, D(90)
l1, l2 = m / (m + e), m2 / (m + e)

def appendix_g_alpha(lambda1, lambda2):
    # SCI P398 Appendix G p163: equation is inverted independently by bisection.
    # This is not an implemented Checker alpha selector.
    def predicted_lambda1(alpha):
        u = 1.25 / (alpha - 2.75)
        v = alpha * u / 2
        return u if lambda2 >= v else u + (1-u) * pow((v-lambda2)/v, .185 * pow(alpha, 1.785))
    low, high = 4.45, 8.0
    for _ in range(100):
        mid = (low + high) / 2
        if predicted_lambda1(mid) > lambda1:
            low = mid
        else:
            high = mid
    return (low + high) / 2

def capacities(cp, nc, bolt_count):
    # Table 6.2 alternative method2, prying possible; no backing plates.
    leff1, leff2 = min(cp, nc), nc
    moment1 = D('.25') * leff1 * 25**2 * 265
    moment2 = D('.25') * leff2 * 25**2 * 265
    n, ew = min(e, D('1.25')*m), D('39.55') / 4
    bolts = D('.9') * 800 * 353 / D('1.25') * bolt_count
    f1 = (8*n-2*ew)*moment1/(2*m*n-ew*(m+n))
    f2 = (2*moment2+n*bolts)/(m+n)
    return [f1, f2, bolts]

print('m,e,m2,lambda1,lambda2:', m, e, m2, l1, l2)
print('AppendixG with rounded C1 m38.6:', appendix_g_alpha(38.6/113.6, 34.8/113.6))
approximation = appendix_g_alpha(float(l1), float(l2))
print('AppendixG with actual m38.55:', approximation)
for alpha in (D('7.5'), D(str(approximation))):
    first_cp, first_nc = 2*pi*m, alpha*m
    other_nc = 4*m + D('1.25')*e
    # Top group row .5p+alpha*m-(2m+.625e); lower row 2m+.625e+.5p.
    group_cp, group_nc = 2*pi*m+2*pitch, alpha*m+pitch
    print('alpha:', alpha)
    print('first leff1,leff2:', min(first_cp, first_nc), first_nc)
    print('other leff1,leff2:', min(first_cp, other_nc), other_nc)
    print('group leff1,leff2:', min(group_cp, group_nc), group_nc)
    print('first F1,F2,F3:', capacities(first_cp, first_nc, 2))
    print('other F1,F2,F3:', capacities(first_cp, other_nc, 2))
    print('group F1,F2,F3:', capacities(group_cp, group_nc, 4))
