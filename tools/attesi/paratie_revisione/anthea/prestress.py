# Elastic cantilever (guide benchmark): L=6000 mm, EI=2e14 Nmm2, fixed at toe; anchor at head EA/L=10000 N/mm (=10000 kN/m), b=s=1 m.
L=6000.; EI=2e14; kc=3*EI/L**3; ka=10000.; H=10000.
u1=H/kc
def stage(P, uref):
    # kc*u = H - F ; F = P + ka*(u-uref), clipped >=0 (anchor)
    u=(H-P+ka*uref)/(kc+ka); F=P+ka*(u-uref)
    if F<0: u=H/kc; F=0.
    return u,F
u2,F2=stage(50000.,u1)
u3k,F3k=stage(50000.,u1)
u3r,F3r=stage(0.,u1)
print("kc N/mm",kc,"u1 mm",u1)
print("F2 lock-off 50 kN: u=%.4f mm F=%.3f kN"%(u2,F2/1e3))
print("F3 prestress repeated: u=%.4f mm F=%.3f kN"%(u3k,F3k/1e3))
print("F3 default prestress 0: u=%.4f mm F=%.3f kN"%(u3r,F3r/1e3))
# unit checks
print("1 kN/m3 in N/mm3", 1e3/1e9, " 1 kNm2 in Nmm2", 1e3*1e6, " 1 kN/m in N/mm", 1e3/1e3)
