"""Line-by-line port of SlopeStability.Slices + BishopSolver.Solve + EmbeddedWallGlobalStability.Prepare (test model),
to compare the code's discretisation with the independent fine-slice calculation in circle_check.py."""
import math
GX = [-.8611363115940526, -.3399810435848563, .3399810435848563, .8611363115940526]
GW = [.3478548451374538, .6521451548625461, .6521451548625461, .3478548451374538]

def height(line, x):
    for i in range(1, len(line)):
        if line[i][0] > line[i-1][0] and x <= line[i][0] + 1e-7:
            return line[i-1][1] + (line[i][1]-line[i-1][1])*(x-line[i-1][0])/(line[i][0]-line[i-1][0])
    return line[-1][1]

def vinterval(poly, x):
    hits = []
    for i in range(len(poly)):
        a = poly[i]; b = poly[(i+1) % len(poly)]
        if min(a[0], b[0]) <= x < max(a[0], b[0]):
            hits.append(a[1] + (b[1]-a[1])*(x-a[0])/(b[0]-a[0]))
    if not hits: return None
    assert len(hits) == 2
    return (min(hits), max(hits))

class Sec: pass

def prepare(water_level=None, wall_gamma=25e-6):
    s = Sec()
    half = 300.0
    s.surface = [(-20000.0, -2000.0), (-half, -2000.0), (-half, 0.0), (half, 0.0), (20000.0, 0.0)]
    soil = dict(g=18e-6, gs=20e-6, phi=math.pi/6, c=.005, cu=.04)
    s.layers = [(soil, -21000.0)]       # uphill (main) column
    s.vlayers = [(soil, -21000.0)]      # valley column (x < 0)
    s.split = 0.0
    s.water = [] if water_level is None else [(-20000.0, water_level), (20000.0, water_level)]
    s.bodies = [([(-half, -6000.0), (half, -6000.0), (half, 0.0), (-half, 0.0)], wall_gamma)]
    # (id, left, right, y, V, H, M, distributed)
    s.loads = [("RightSurcharge", -20000.0, -half, -2000.0, .005, 0, 0, True),
               ("LeftSurcharge", half, 20000.0, 0.0, .01, 0, 0, True),
               ("StructureDistributed", 0, 0, -3000.0, 0, 6.0, 0, False),
               ("PointLoad-0", 0, 0, -1000.0, 10.0, 5.0, 3000.0, False)]
    s.gw = 9.81e-6
    return s

def layers_at(s, x):
    return s.vlayers if (len(s.vlayers) > 0 and x < s.split) else s.layers

def crossings(c, y):
    X, Y, R, L, Rt = c
    if y > Y or y < Y - R: return []
    dx = math.sqrt(max(0, R*R - (Y-y)**2))
    return [x for x in (X-dx, X+dx) if L + 1e-5 < x < Rt - 1e-5]

def base(c, x):
    X, Y, R, L, Rt = c
    return Y - math.sqrt(max(0, R*R - (x-X)**2))

def divisions(s, c, count):
    X, Y, R, L, Rt = c
    xs = [L + (Rt-L)*i/count for i in range(1, count)] + [L, Rt]
    xs += [p[0] for p in s.surface] + [p[0] for p in s.water]
    xs += [p[0] for b in s.bodies for p in b[0]]
    for col in ([s.vlayers, s.layers] if s.vlayers else [s.layers]):
        for soil, bot in col: xs += crossings(c, bot)
    if s.vlayers: xs.append(s.split)
    for ld in s.loads:
        if ld[7]: xs += [ld[1], ld[2]]
    xs = sorted(v for v in xs if L <= v <= Rt)
    merged = []
    for v in xs:
        if not merged or v - merged[-1] > 1e-4: merged.append(v)
    return merged

def slices(s, c, f, count):
    X, Y, R, L, Rt = c
    cuts = divisions(s, c, count); out = []
    for i in range(1, len(cuts)):
        l, r = cuts[i-1], cuts[i]; x = (l+r)/2; b = r-l; y = base(c, x); top = height(s.surface, x)
        layer = next(sl for sl in layers_at(s, x) if y >= sl[1] - 1e-6)
        soilW = bodyW = wx = wy = 0.0
        for j in range(4):
            gx = x + b*GX[j]/2; scale = b*GW[j]/2; bottom = base(c, gx); surf = height(s.surface, gx)
            water = -math.inf if not s.water else height(s.water, gx)
            intervals = [(bd, vinterval(bd[0], gx)) for bd in s.bodies]
            intervals = [t for t in intervals if t[1] is not None]
            upper = surf
            def mass(low, high, gamma, rigid):
                nonlocal soilW, bodyW, wx, wy
                if high <= low: return
                w = (high-low)*gamma*scale
                if rigid: bodyW += w
                else: soilW += w
                wx += w*gx; wy += w*(high+low)/2
            for soil, bot in layers_at(s, gx):
                low = max(bottom, bot); high = upper
                for wet in (False, True):
                    lo = low if wet else max(low, water); hi = min(high, water) if wet else high
                    gamma = (soil['gs'] if wet else soil['g'])*f['soil']
                    mass(lo, hi, gamma, False)
                    for bd, sp in intervals: mass(max(lo, sp[0]), min(hi, sp[1]), -gamma, False)
                upper = min(upper, bot)
            for bd, sp in intervals: mass(sp[0], sp[1], bd[1]*f['body'], True)
        wT = soilW + bodyW; xg = wx/wT if wT > 0 else x; yg = wy/wT if wT > 0 else (top+y)/2
        v = (1-f['kv'])*wT; h = f['kh']*wT
        driving = (v*(xg-X) + h*(Y-yg))/R; vl = hl = 0.0
        for (lid, lL, lR, lY, lV, lH, lM, dist) in s.loads:
            fac = f['loads'].get(lid, 0)
            length = max(0, min(r, lR) - max(l, lL)) if dist else (1 if (lL >= l and (lL < r or (i == len(cuts)-1 and lL <= r))) else 0)
            if fac == 0 or length == 0: continue
            atX = (max(l, lL) + min(r, lR))/2 if dist else lL
            vl += lV*fac*length; hl += lH*fac*length
            driving += fac*length*(lV*(atX-X) + lH*(Y-lY) + lM)/R
        u = 0 if f['undrained'] else (0 if not s.water else s.gw*max(0, height(s.water, x) - y))
        soil = layer[0]
        phi = 0 if f['undrained'] else math.atan(math.tan(soil['phi'])/f['tphi'])
        coh = soil['cu']/f['cu'] if f['undrained'] else soil['c']/f['c']
        out.append(dict(b=b, alpha=math.asin((x-X)/R), V=v+vl, u=u, phi=phi, c=coh, D=driving))
    return out

def bishop(sl):
    D = sum(s['D'] for s in sl)
    def res(s, F):
        t = math.tan(s['phi']); m = math.cos(s['alpha']) + math.sin(s['alpha'])*t/F
        return (s['c']*s['b'] + (s['V'] - s['u']*s['b'])*t)/m
    def resid(F): return sum(res(s, F) for s in sl) - F*D
    lower = max(1e-7, max(-math.tan(s['alpha'])*math.tan(s['phi']) for s in sl) + 1e-7)
    upper = max(2, lower*2)
    assert resid(lower) >= 0
    while resid(upper) > 0 and upper < 1e5: upper *= 2
    for it in range(100):
        F = (lower+upper)/2; r = resid(F)
        if abs(r) <= 1e-9*max(1, F*D): break
        if r > 0: lower = F
        else: upper = F
    return F, D

if __name__ == "__main__":
    R = 10000.0; circle = (0.0, 2000.0, R, -math.sqrt(1e8-1.6e7), math.sqrt(1e8-4e6))
    loads1 = {k: 1.0 for k in ("RightSurcharge", "LeftSurcharge", "StructureDistributed", "PointLoad-0")}
    loads13 = {k: 1.3 for k in loads1}
    base_f = dict(soil=1.0, body=1.0, tphi=1.25, c=1.25, cu=1.4, kh=0, kv=0, undrained=False)
    for label, s, f in [("dry, loads x1", prepare(), dict(base_f, loads=loads1)),
                        ("dry, loads x1.3", prepare(), dict(base_f, loads=loads13)),
                        ("water -2500, loads x1", prepare(-2500.0), dict(base_f, loads=loads1)),
                        ("undrained", prepare(), dict(base_f, loads=loads1, undrained=True))]:
        for n in (20, 40, 80):
            F, D = bishop(slices(s, circle, f, n))
            print(f"{label:24s} n={n:3d}  F={F:.6f}  D={D:.6f}")
