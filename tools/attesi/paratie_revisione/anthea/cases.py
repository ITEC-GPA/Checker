import math
L=6000.; EI=2e14; kc=3*EI/L**3; H=10000.; ka=10000.
print("kc",kc)
# (a) anchor dir+1 installed with load, P=0, s=b
u=H/(kc+ka); print("a: u=%.10f mm F=%.10f kN R=%.10f kN"%(u,ka*u/1e3,-ka*u/1e3))
# (d) spacing 2.5 m, width 1 m
m=1000/2500.; u=H/(kc+ka*m); F=ka*u; print("d: u=%.10f F=%.10f kN R=%.10f kN"%(u,F/1e3,-F*m/1e3))
# (e) 30 deg
c=math.cos(math.radians(30)); s=math.sin(math.radians(30)); u=H/(kc+ka*c*c); F=ka*c*u
print("e: u=%.10f F=%.10f kN R=%.10f kN dN=%.10f kN"%(u,F/1e3,-c*F/1e3,F*s/1e3))
# prestress
u1=H/kc; P=50000.
u2=(H-P+ka*u1)/(kc+ka); F2=P+ka*(u2-u1); u3=(H+ka*u1)/(kc+ka); F3=ka*(u3-u1)
print("C1: u1=%.10f u2=%.10f F2=%.10f kN u3=%.10f F3=%.10f"%(u1,u2,F2/1e3,u3,F3/1e3))
# soil pressure tests (MPa)
g=18e-6; gs=20e-6; q=0.02; phi=math.radians(30); Ka=math.tan(math.pi/4-phi/2)**2; Kp=1/Ka; K0=.5; gw=9.81e-6
z=-5000.
sv=q+g*5000; print("B1 dry: sv=%.12f p0=%.12f pa=%.12f pp=%.12f"%(sv,K0*sv,Ka*sv,Kp*sv))
for cc in (0.01,):
    print("B1 dry c=10kPa: pa=%.12f pp=%.12f"%(max(0,Ka*sv-2*cc*math.sqrt(Ka)),Kp*sv+2*cc*math.sqrt(Kp)))
u=gw*3000; sv=q+g*2000+gs*3000; se=sv-u
print("B1 wet: u=%.12f sv=%.12f se=%.12f p0=%.12f pa=%.12f pp=%.12f"%(u,sv,se,K0*se,Ka*se,Kp*se))
print("rad 20deg",math.radians(20)," 30deg",math.radians(30))
