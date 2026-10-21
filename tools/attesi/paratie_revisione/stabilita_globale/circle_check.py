"""Independent Bishop-simplified check of one slip circle for the EmbeddedWallGlobalTests model.
Units: N, mm, MPa (N/mm2), N/mm3. Valley on the left in slope coordinates (engine frame).
Model (direction +1, valley = physical right): wall x in [-300, 300], top y=0, toe y=-6000, gamma_c = 25e-6.
Uphill ground y=0 (x>300) with q=0.01 MPa; valley ground y=-2000 (x<-300) with q=0.005 MPa.
Point load at y=-1000 (x=0): V=10 N/mm, H=5 N/mm toward valley, couple 3000 Nmm/mm (driving).
Uniform horizontal load on the wall: 6 N/mm resultant at y=-3000 toward valley.
Soil: gamma 18e-6, gamma_sat 20e-6, phi 30 deg, c' 0.005 MPa. M2: tan/1.25, c/1.25.
"""
import math

def circle_ends(xc, yc, R, yl, yr):
    xl = xc - math.sqrt(R*R - (yc-yl)**2)
    xr = xc + math.sqrt(R*R - (yc-yr)**2)
    return xl, xr

def solve(n=200000, water=None, gw=9.81e-6, load_factor=1.0, gphi=1.25, gc=1.25, undrained=False, gcu=1.4, cu=0.04):
    xc, yc, R = 0.0, 2000.0, 10000.0
    xl, xr = circle_ends(xc, yc, R, -2000.0, 0.0)
    g, gs, gcon = 18e-6, 20e-6, 25e-6
    phi = 0.0 if undrained else math.atan(math.tan(math.radians(30))/gphi)
    c = cu/gcu if undrained else 0.005/gc
    tanp = math.tan(phi)
    dx = (xr - xl)/n
    slices = []
    for i in range(n):
        x = xl + (i+0.5)*dx
        yb = yc - math.sqrt(R*R - (x-xc)**2)
        top = -2000.0 if x < -300 else 0.0
        # weight: soil from base to top except wall body [-6000,0] for |x|<300
        def soilw(lo, hi):
            if hi <= lo: return 0.0
            if water is None: return g*(hi-lo)
            wet = max(0.0, min(hi, water) - lo)
            dry = max(0.0, hi - max(lo, water))
            return gs*wet + g*dry
        if abs(x) < 300:
            W = (soilw(yb, -6000.0) + gcon*6000.0)*dx
        else:
            W = soilw(yb, top)*dx
        q = 0.005 if x < -300 else (0.01 if x > 300 else 0.0)
        V = W + load_factor*q*dx
        u = 0.0 if (water is None or undrained) else gw*max(0.0, water - yb)
        alpha = math.asin((x-xc)/R)
        slices.append((x, dx, V, u, alpha))
    # point load at x = 0 (vertical part enters the slice containing x=0)
    Vp = 10.0*load_factor
    # driving moment / R
    D = sum(V*(x-xc) for x, dx, V, u, a in slices)
    D += load_factor*(Vp*(0-xc) + 5.0*(yc-(-1000.0)) + 3000.0)
    D += load_factor*(6.0*(yc-(-3000.0)))
    D /= R
    # add Vp to the slice that contains x=0
    k = min(range(n), key=lambda j: abs(slices[j][0]))
    x, dx_, V, u, a = slices[k]; slices[k] = (x, dx_, V+Vp, u, a)
    F = 1.5
    for it in range(500):
        num = sum((c*dx + (V-u*dx)*tanp)/(math.cos(a)+math.sin(a)*tanp/F) for x, dx, V, u, a in slices)
        Fn = num/D
        if abs(Fn-F) < 1e-12: F = Fn; break
        F = Fn
    fell = sum(c*dx/math.cos(a) + (V*math.cos(a) - u*dx/math.cos(a))*tanp for x, dx, V, u, a in slices)/D
    return F, D, fell

if __name__ == "__main__":
    for label, kw in [("dry, loads x1", {}), ("dry, loads x1.3", {"load_factor": 1.3}),
                      ("water -2500, loads x1", {"water": -2500.0}),
                      ("undrained cu=40kPa/1.4", {"undrained": True})]:
        F, D, fell = solve(n=100000, **kw)
        print(f"{label:28s} F_Bishop={F:.6f}  D={D:.6f} N/mm  F_Fellenius={fell:.6f}  eta=1.1/F={1.1/F:.4f}")
