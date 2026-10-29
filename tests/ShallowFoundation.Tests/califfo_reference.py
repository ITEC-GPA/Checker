"""Independent expected values from the Califfo theoretical manual (26.01.2013), written from its formulas.
No import of, and no output from, the C# engine. Units here: kN, m, kPa; the frozen file uses the engine units
(mm, N, MPa, N/mm3, rad).

  python califfo_reference.py          recompute and compare with califfo-reference.json (PASS/FAIL)
  python califfo_reference.py --write  regenerate califfo-reference.json

Each case holds the value read literally from the manual ("manual"). Where the engine follows a documented
convention that the manual does not prescribe, "convention" holds the value of that convention, computed here
from its own rule, and "relation" says on which side of the manual value it lies ("le" = conservative).
Conventions:
- depth factors use the geometric side aligned with B' (the manual: the geometric B) when eccentricity swaps B' and L';
- EC7 square or circular factors from B'/L' (the manual: from the real geometry);
- Meyerhof 0 < phi < 10: USACE EM 1110-1-1905 Table 4-3 interpolation (the manual: undefined; here factors 1);
- Meyerhof theta > phi: i_gamma = 0 (USACE Table 4-3; the manual writes only (1 - theta/phi)^2);
- Meyerhof undrained with an inclined load: i_q = i_c = (1 - theta/90)^2 also on the surcharge term, q i_q
  (Meyerhof 1963 and USACE EM 1110-1-1905 Table 4-3: i_c = i_q for any phi; with phi = 0 only N_q = s_q = d_q = 1).
  The manual writes (3.16) q_ult = (2 + pi) cu s_c d_c i_c + q, without i_q, and gives (3.14) i_c = i_q only for
  phi > 0. Decision of the user (15/10/2026, bug GEO-1): Meyerhof (1963)/USACE, not the manual. The "manual" value
  keeps + q; the "convention" value is + q i_q, conservative ("le"). Without H the two coincide;
- Brinch Hansen on a circle with an inclined load: H aligned with B' (H_B = |H|, H_L = 0), since a circle has no
  preferred axis (the manual gives no rule for the circle; the literal reading here splits H along x and y).
Brinch Hansen with an inclined load, read literally from the manual: drained (3.23)-(3.25) and undrained (3.27),
(3.27'), (3.27''); with H > 0 the bearing capacity is computed for a failure in the direction of B' and in the
direction of L' (also when the component along L' is zero) and the smaller value is adopted (section 3.4.1); a strip
has only the direction of B'. B'L' in (3.23) and (3.27) is the effective area (pi B^2 / 4 for the circle). With H = 0
the normal-load expressions (3.19), (3.20), (3.28) apply: neither direction tends to them as H -> 0 in general.
Actions beyond the domain (section 3.4.1: no inclination factor may be negative) give a null bearing capacity.
"""
import json, math, os, sys

PI = math.pi
GW = 9.81
HERE = os.path.dirname(os.path.abspath(__file__))
FROZEN = os.path.join(HERE, 'califfo-reference.json')


def prandtl(phi):
    nq = math.exp(PI * math.tan(phi)) * math.tan(PI / 4 + phi / 2) ** 2  # (3.4)
    return (nq - 1) / math.tan(phi), nq  # (3.3)


def depth_k(D, B):
    return D / B if D <= B else math.atan(D / B)  # (3.20b)


def geometry(cs, conv):
    shape, B, N = cs['shape'], cs['B'], cs['N']
    ex, ey = cs.get('My', 0) / N, cs.get('Mx', 0) / N
    Hx, Hy = abs(cs.get('Hx', 0)), abs(cs.get('Hy', 0))
    g = dict(H=math.hypot(Hx, Hy), notes=[])
    if shape == 'strip':
        g.update(Bp=B - 2 * abs(ex), Lp=1.0, r=0.0, HB=Hx, HL=0.0, Bdepth=B, square=False)
        g['A'] = g['Bp']
    elif shape == 'circle':
        g.update(Bp=B, Lp=B, r=1.0, HB=Hx, HL=Hy, Bdepth=B, square=True, A=PI * B * B / 4)
    else:
        L = cs['L']
        bx, ly = B - 2 * abs(ex), L - 2 * abs(ey)
        swap = bx > ly
        bp, lp, hb, hl = (ly, bx, Hy, Hx) if swap else (bx, ly, Hx, Hy)
        g.update(Bp=bp, Lp=lp, r=bp / lp if bp > 0 and lp > 0 else 0, HB=hb, HL=hl, square=(B == L), A=bx * ly)
        g['outside'] = bx <= 0 or ly <= 0
        g['Bdepth'] = min(B, L)
        has_depth = cs['method'] in ('Hansen', 'Vesic', 'Meyerhof') and cs.get('depth', True)
        if conv and has_depth and (L if swap else B) != g['Bdepth']:
            g['Bdepth'] = L if swap else B
            g['notes'].append('fattori di profondità con il lato geometrico allineato a B′ (manuale: B geometrica)')
        if conv and cs['method'] == 'Eurocode7' and g['square'] and (ex or ey):
            g['square'] = False
            g['notes'].append('EC7 quadrata eccentrica: fattori da B′/L′ (manuale: formula della quadrata)')
    g.setdefault('outside', g['Bp'] <= 0)
    return g


def overburden(cs, drained):
    D, gc, q = cs['D'], cs.get('gc', 18.0), cs.get('surcharge', 0.0)
    zw = cs.get('zw')
    if zw is None:
        return q + gc * D
    dry = min(D, zw)
    return q + gc * dry + (D - dry) * (cs.get('gcsat', 20.0) - (GW if drained else 0))


def gamma_below(cs, g):
    # Not in the manual: Das (2009) rule, water table within B' below the base.
    gm, zw = cs.get('g', 18.0), cs.get('zw')
    if zw is None:
        return gm
    sub, d = cs.get('gsat', 20.0) - GW, zw - cs['D']
    return sub if d <= 0 else gm if d >= g['Bp'] else sub + d / g['Bp'] * (gm - sub)


def m_exponent(g):  # (3.32)
    if g['r'] == 0:
        return 2.0
    r = g['r']
    mb, ml = (2 + r) / (1 + r), (2 + 1 / r) / (1 + 1 / r)
    return mb if g['H'] == 0 else ml * (g['HL'] / g['H']) ** 2 + mb * (g['HB'] / g['H']) ** 2


def term(N=1.0, S=1.0, D=1.0, I=1.0, additive=None):
    return dict(N=N, S=S, D=D, I=I, add=additive)


def hansen_components(cs, g, conv, out):
    """H_B and H_L of section 3.4.1 (absolute values, already exchanged when B' lies along y)."""
    hb, hl = g['HB'], g['HL']
    if conv and cs['shape'] == 'circle' and hl > 0:
        hb, hl = g['H'], 0.0
        out['notes'].append('Hansen, cerchio: H allineata a B′ (manuale: nessuna regola per il cerchio, qui componenti su x e y)')
    return hb, hl


def hansen_directions(cs, g, direction):
    """Section 3.4.1: failure in the direction of B' and of L' (not for a strip); the smaller value is adopted."""
    rows = [direction('B', g['r'], g['Bdepth'])]
    if cs['shape'] != 'strip':
        rows.append(direction('L', g['Lp'] / g['Bp'], g['Lp']))  # (3.24'), (3.25'): L'/B' and D/L'
    return min(rows, key=lambda row: row[0])


def hansen_drained_inclined(cs, g, conv, phi, c, q, gm, out):  # (3.23)-(3.25), eta_B = eta_L = 0
    nc, nq = prandtl(phi)
    t, s = math.tan(phi), math.sin(phi)
    ng = 1.5 * (nq - 1) * t  # (3.18)
    hb, hl = hansen_components(cs, g, conv, out)
    den = cs['N'] + g['A'] * c / t  # N + B'L' c cot(phi)
    iq = {'B': (1 - 0.5 * hb / den) ** 5, 'L': (1 - 0.5 * hl / den) ** 5}
    ig = {'B': (1 - 0.7 * hb / den) ** 5, 'L': (1 - 0.7 * hl / den) ** 5}
    ic = {k: iq[k] - (1 - iq[k]) / (nq - 1) for k in iq}
    if min(list(iq.values()) + list(ig.values()) + list(ic.values())) < 0:  # section 3.4.1: null capacity
        out.update(exhausted=True, qult=0.0)
        return out

    def direction(d, ratio, side):
        other = 'L' if d == 'B' else 'B'
        sq = 1 + iq[d] * ratio * s  # (3.24)
        sc = sq - (1 - sq) / (nq - 1)
        sg = max(0.6, 1 - 0.4 * ratio * ig[d] / ig[other]) if ig[other] > 0 else 0.6  # "comunque >= 0,6"
        dq = 1 + 2 * t * (1 - s) ** 2 * depth_k(cs['D'], side) if cs.get('depth', True) else 1.0  # (3.25)
        dc = dq - (1 - dq) / (nc * t)
        qult = c * nc * sc * dc * ic[d] + q * nq * sq * dq * iq[d] + 0.5 * gm * g['Bp'] * ng * sg * ig[d]
        return qult, term(nc, sc, dc, ic[d]), term(nq, sq, dq, iq[d]), term(ng, sg, 1.0, ig[d])

    qult, fc, fq, fg = hansen_directions(cs, g, direction)
    out.update(qult=qult, c=fc, q_=fq, g_=fg)
    return out


def hansen_undrained_inclined(cs, g, conv, q, out):  # (3.26), (3.27), (3.27'), (3.27''); b_c = g_c = 0
    cu, nc = cs['cu'], PI + 2
    hb, hl = hansen_components(cs, g, conv, out)
    if max(hb, hl) > g['A'] * cu:  # square root of a negative number: beyond the domain
        out.update(exhausted=True, qult=0.0)
        return out
    i = {'B': 0.5 - 0.5 * math.sqrt(1 - hb / (g['A'] * cu)), 'L': 0.5 - 0.5 * math.sqrt(1 - hl / (g['A'] * cu))}

    def direction(d, ratio, side):
        sc = 0.2 * i[d] * ratio  # literally 0.2 i_c B'/L': zero for H -> 0, not 0.2 B'/L' of (3.28)
        dc = 0.4 * depth_k(cs['D'], side) if cs.get('depth', True) else 0.0
        return nc * cu * (1 + sc + dc - i[d]) + q, term(nc, additive=[sc, dc, i[d]])

    qult, fc = hansen_directions(cs, g, direction)
    out.update(qult=qult, c=fc, q_=term(1.0), g_=term(0.0))
    return out


def reference(cs, conv):
    method, drained = cs['method'], cs['cond'] == 'D'
    g = geometry(cs, conv)
    q, N, D = overburden(cs, drained), cs['N'], cs['D']
    out = dict(A=0.0 if g['outside'] else g['A'], notes=list(g['notes']), exhausted=False)
    if g['outside']:
        out.update(exhausted=True, qult=0.0)
        return out
    if not drained:
        cu, nc = cs['cu'], PI + 2
        if method == 'Terzaghi':  # (3.8), rough base; s_gamma from the shape table, with N_gamma = 0
            sc = 1.3 if g['square'] else 1.0
            sg = 1.0 if cs['shape'] == 'strip' else 0.6 if cs['shape'] == 'circle' else 0.8
            out.update(qult=(1 + 1.5 * PI) * sc * cu + q, c=term(1 + 1.5 * PI, sc), q_=term(1.0), g_=term(0.0, sg))
            return out
        elif method == 'Eurocode7':  # (3.42), (3.43)
            sc = 1.2 if g['square'] else 1 + 0.2 * g['r']
            if g['H'] > g['A'] * cu:
                out.update(exhausted=True, qult=0.0)
                return out
            ic = 0.5 * (1 + math.sqrt(1 - g['H'] / (g['A'] * cu)))
            out.update(qult=nc * cu * sc * ic + q, c=term(nc, sc, 1, ic))
        elif method == 'Meyerhof':  # (3.16), (3.17)
            sc, dc = 1 + 0.2 * g['r'], 1 + 0.2 * D / g['Bdepth']
            ic = (1 - math.atan2(g['H'], N) / (PI / 2)) ** 2
            iq = 1.0
            if conv and g['H'] > 0:  # Meyerhof (1963), USACE Table 4-3: i_q = i_c for any phi (manual (3.16): + q)
                iq = ic
                out['notes'].append('Meyerhof non drenato: iq = ic anche sul termine q (Meyerhof 1963, USACE tab. 4-3; manuale (3.16): + q)')
            out.update(qult=nc * cu * sc * dc * ic + q * iq, c=term(nc, sc, dc, ic), q_=term(1.0, I=iq))
        elif method == 'Hansen' and g['H'] > 0:  # (3.26), (3.27): inclined load
            return hansen_undrained_inclined(cs, g, conv, q, out)
        elif method == 'Hansen':  # (3.26), (3.28): normal load, additive corrections
            sa, da = 0.2 * g['r'], 0.4 * depth_k(D, g['Bdepth'])
            out.update(qult=nc * cu * (1 + sa + da) + q, c=term(nc, additive=[sa, da, 0.0]))
        elif method == 'Vesic':  # (3.36), (3.38)
            sc, dc = 1 + 0.2 * g['r'], 1 + 0.4 * depth_k(D, g['Bdepth'])
            ic = 1 - m_exponent(g) * g['H'] / (g['A'] * cu * nc)
            if ic < 0:
                out.update(exhausted=True, qult=0.0)
                return out
            out.update(qult=nc * cu * sc * dc * ic + q, c=term(nc, sc, dc, ic))
        out.setdefault('q_', term(1.0))
        out.update(g_=term(0.0))
        return out
    phi, c = cs['phi'] * PI / 180, cs.get('c', 0.0)
    if cs.get('local') == 'Terzaghi':  # (4.1)
        c, phi = 2 / 3 * c, math.atan(2 / 3 * math.tan(phi))
    if cs.get('local') == 'Vesic':  # (4.2), sands only
        dr = cs['Dr']
        phi = math.atan((2 / 3 + dr - 0.75 * dr * dr) * math.tan(phi))
    gm = gamma_below(cs, g)
    t, s, r, H = math.tan(phi), math.sin(phi), g['r'], g['H']
    fc, fq, fg = term(), term(), term()
    if method == 'Terzaghi':  # (3.5)-(3.7); N_gamma known from the manual only at 34° and 48°
        nq = math.exp(2 * (3 * PI / 4 - phi / 2) * t) / (2 * math.cos(PI / 4 + phi / 2) ** 2)
        fq['N'], fc['N'], fg['N'] = nq, (nq - 1) / t, {34: 36.0, 48: 780.0}[cs['phi']]
        fc['S'] = 1.3 if g['square'] else 1.0
        fg['S'] = 1.0 if cs['shape'] == 'strip' else 0.6 if cs['shape'] == 'circle' else 0.8
    elif method == 'Hansen' and H > 0:  # (3.23)-(3.25): inclined load
        return hansen_drained_inclined(cs, g, conv, phi, c, q, gm, out)
    else:
        nc, nq = prandtl(phi)
        fc['N'], fq['N'] = nc, nq
        if method == 'Eurocode7':  # (3.40), (3.41)
            fg['N'] = 2 * (nq - 1) * t
            fq['S'], fg['S'] = (1 + s, 0.7) if g['square'] else (1 + r * s, 1 - 0.3 * r)
            fc['S'] = (fq['S'] * nq - 1) / (nq - 1)
        elif method in ('Hansen', 'Vesic'):
            if method == 'Hansen':  # (3.18), (3.19): normal load only
                fg['N'], fq['S'] = 1.5 * (nq - 1) * t, 1 + r * s
                fc['S'] = fq['S'] - (1 - fq['S']) / (nq - 1)
            else:  # (3.30), (3.19')
                fg['N'], fq['S'], fc['S'] = 2 * (nq + 1) * t, 1 + r * t, 1 + r * nq / nc
            fg['S'] = 1 - 0.4 * r
            if cs.get('depth', True):
                fq['D'] = 1 + 2 * t * (1 - s) ** 2 * depth_k(D, g['Bdepth'])  # (3.20b)
                fc['D'] = fq['D'] - (1 - fq['D']) / (nc * t)  # (3.20a)
        elif method == 'Meyerhof':  # (3.11)-(3.14)
            kp = math.tan(PI / 4 + phi / 2) ** 2
            fg['N'] = (nq - 1) * math.tan(1.4 * phi)
            dB = D / g['Bdepth'] if cs.get('depth', True) else 0.0
            fc['S'], fc['D'] = 1 + 0.2 * kp * r, 1 + 0.2 * math.sqrt(kp) * dB
            if cs['phi'] >= 10:
                fq['S'] = fg['S'] = 1 + 0.1 * kp * r
                fq['D'] = fg['D'] = 1 + 0.1 * math.sqrt(kp) * dB
            elif conv:  # USACE Table 4-3: linear between phi = 0 (factor 1) and phi = 10°
                k10, w = math.tan(PI / 4 + PI / 36) ** 2, cs['phi'] / 10
                fq['S'] = fg['S'] = 1 + w * 0.1 * k10 * r
                fq['D'] = fg['D'] = 1 + w * 0.1 * math.sqrt(k10) * dB
                out['notes'].append('Meyerhof φ′ < 10°: interpolazione USACE tab. 4-3 (manuale: indefiniti, qui 1)')
            th = math.atan2(H, N)
            fc['I'] = fq['I'] = (1 - th / (PI / 2)) ** 2
            fg['I'] = (1 - th / phi) ** 2
            if conv and th > phi:
                fg['I'] = 0.0
                out['notes'].append('Meyerhof θ > φ′: iγ = 0 (USACE tab. 4-3; manuale: solo (1 − θ/φ′)²)')
        if method in ('Eurocode7', 'Vesic') and H > 0:  # (3.31)
            f = H / (N + g['A'] * c / t)
            if f >= 1:
                out.update(exhausted=True, qult=0.0)
                return out
            m = m_exponent(g)
            fq['I'], fg['I'] = (1 - f) ** m, (1 - f) ** (m + 1)
            fc['I'] = fq['I'] - (1 - fq['I']) / (nc * t)
            if fc['I'] < 0:
                out.update(exhausted=True, qult=0.0)
                return out
    pc = c * fc['N'] * fc['S'] * fc['D'] * fc['I']
    pq = q * fq['N'] * fq['S'] * fq['D'] * fq['I']
    pg = 0.5 * gm * g['Bp'] * fg['N'] * fg['S'] * fg['D'] * fg['I']
    out.update(qult=pc + pq + pg, c=fc, q_=fq, g_=fg)
    return out


def engine_input(cs):
    shape = {'rect': 'Rectangle', 'square': 'Rectangle', 'strip': 'Strip', 'circle': 'Circle'}[cs['shape']]
    e = dict(Method=cs['method'], Shape=shape, Condition='Drained' if cs['cond'] == 'D' else 'Undrained',
             Width=cs['B'] * 1000, Length=cs.get('L', cs['B']) * 1000, Depth=cs['D'] * 1000,
             FrictionAngle=cs.get('phi', 30) * PI / 180, Cohesion=cs.get('c', 0) / 1000,
             UndrainedStrength=cs.get('cu', 75) / 1000, Normal=cs['N'] * 1000)
    for key, name, k in (('Hx', 'HorizontalX', 1e3), ('Hy', 'HorizontalY', 1e3), ('Mx', 'MomentX', 1e6), ('My', 'MomentY', 1e6)):
        if key in cs:
            e[name] = cs[key] * k
    if 'zw' in cs:
        e.update(WaterEnabled=True, WaterDepth=cs['zw'] * 1000)
    if cs.get('local') == 'Terzaghi':
        e['Compression'] = 'TerzaghiLocalShear'
    if cs.get('local') == 'Vesic':
        e.update(Compression='VesicDensity', RelativeDensity=cs['Dr'])
    if 'profile' in cs:
        e['Profile'] = cs['profile']
    if cs.get('depth') is False:
        e['IncludeDepthFactors'] = False
    return e


BASE = dict(Profile='Characteristic', Compression='GeneralShear', UnitWeight=18e-6, SaturatedUnitWeight=20e-6,
            CoverUnitWeight=18e-6, CoverSaturatedUnitWeight=20e-6, WaterUnitWeight=GW * 1e-6, WaterEnabled=False,
            Surcharge=0.0, HorizontalX=0.0, HorizontalY=0.0, MomentX=0.0, MomentY=0.0, IncludeDepthFactors=True, RoughBase=True)


def cases():
    out = []

    def add(**k):
        out.append(k)

    shapes = [dict(shape='rect', B=2, L=3, N=600), dict(shape='square', B=2, L=2, N=600),
              dict(shape='circle', B=2, N=600), dict(shape='strip', B=2, N=300)]
    for method in ('Eurocode7', 'Hansen', 'Vesic', 'Meyerhof'):
        for phi in (5, 20, 30, 38):
            for c in (0, 10):
                for sh in shapes:
                    for D in (1, 3):
                        add(method=method, cond='D', phi=phi, c=c, D=D, **sh)
        for mx, my in ((60, 120), (360, 0), (360, 120)):
            for D in (1, 1.5):
                add(method=method, cond='D', phi=30, c=10, D=D, shape='rect', B=2, L=3, N=600, Mx=mx, My=my)
        add(method=method, cond='D', phi=30, c=10, D=1, shape='square', B=2, L=2, N=600, My=120)
        for sh in shapes:
            for D in (1, 3):
                add(method=method, cond='U', cu=75, D=D, **sh)
        add(method=method, cond='U', cu=75, D=1, shape='square', B=2, L=2, N=600, My=120)
        add(method=method, cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, Mx=360)
        add(method=method, cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, My=600)  # resultant on the edge
    for method in ('Eurocode7', 'Vesic'):
        for c in (0, 10):
            for hx, hy in ((60, 0), (0, 60), (40, 30)):
                add(method=method, cond='D', phi=30, c=c, D=1, shape='rect', B=2, L=3, N=600, Hx=hx, Hy=hy)
                add(method=method, cond='D', phi=30, c=c, D=1, shape='rect', B=2, L=3, N=600, Hx=hx, Hy=hy, My=120, Mx=360)
            add(method=method, cond='D', phi=30, c=c, D=1, shape='circle', B=2, N=600, Hx=60)
            add(method=method, cond='D', phi=30, c=c, D=1, shape='strip', B=2, N=300, Hx=40)
            for hx in (480, 540, 600):  # near and beyond the limit of the inclination formula
                add(method=method, cond='D', phi=30, c=c, D=1, shape='rect', B=2, L=3, N=600, Hx=hx)
        for hx, hy in ((60, 0), (0, 60), (40, 30)):
            add(method=method, cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, Hx=hx, Hy=hy)
        add(method=method, cond='U', cu=75, D=1, shape='strip', B=2, N=300, Hx=40)
        for hx in (440, 460, 1500):  # A′cu = 450 kN; Vesić ic < 0 above m H = A′ cu Nc
            add(method=method, cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, Hx=hx)
    for hx in (40, 150, 200):
        add(method='Meyerhof', cond='D', phi=30, c=10, D=1, shape='strip', B=2, N=300, Hx=hx)
        add(method='Meyerhof', cond='U', cu=75, D=1, shape='strip', B=2, N=300, Hx=hx)
    for phi in (34, 48):
        for c in (0, 10):
            for sh in (dict(shape='strip', B=2, N=300), dict(shape='square', B=2, L=2, N=600), dict(shape='circle', B=2, N=600)):
                add(method='Terzaghi', cond='D', phi=phi, c=c, D=1, **sh)
    for sh in (dict(shape='strip', B=2, N=300), dict(shape='square', B=2, L=2, N=600), dict(shape='circle', B=2, N=600)):
        add(method='Terzaghi', cond='U', cu=75, D=1, **sh)
    for zw in (0.5, 1.0, 2.0, 4.0):
        add(method='Eurocode7', cond='D', phi=30, c=0, D=1, shape='rect', B=2, L=3, N=600, zw=zw)
        add(method='Vesic', cond='D', phi=30, c=5, D=1, shape='rect', B=2, L=3, N=600, zw=zw)
        add(method='Eurocode7', cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, zw=zw)
    add(method='Eurocode7', cond='D', phi=30, c=10, D=1, shape='rect', B=2, L=3, N=600, local='Terzaghi')
    add(method='Vesic', cond='D', phi=34, c=0, D=1, shape='rect', B=2, L=3, N=600, local='Vesic', Dr=0.3)
    add(method='Eurocode7', cond='D', phi=30, c=10, D=1, shape='rect', B=2, L=3, N=600, profile='Ntc2018')
    add(method='Eurocode7', cond='U', cu=75, D=1, shape='rect', B=2, L=3, N=600, profile='Ntc2018')
    add(method='Vesic', cond='D', phi=30, c=0, D=1, shape='rect', B=2, L=3, N=600, depth=False)
    add(method='Meyerhof', cond='D', phi=30, c=0, D=1, shape='rect', B=2, L=3, N=600, depth=False)
    add(method='Eurocode7', cond='D', phi=30, c=10, D=1, shape='rect', B=2, L=3, N=600, My=600)
    # Brinch Hansen with an inclined load (1.16.0): drained (3.23)-(3.25) and undrained (3.27), both directions.
    rect = dict(shape='rect', B=2, L=3, N=600)
    for c in (0, 10):
        for hx, hy in ((60, 0), (0, 60), (40, 30), (6, 0), (0, 6)):
            add(method='Hansen', cond='D', phi=30, c=c, D=1, Hx=hx, Hy=hy, **rect)
        add(method='Hansen', cond='D', phi=30, c=c, D=1, Hx=40, Hy=30, My=120, Mx=360, **rect)
        add(method='Hansen', cond='D', phi=30, c=c, D=1, shape='square', B=2, L=2, N=600, Hx=60)
        add(method='Hansen', cond='D', phi=30, c=c, D=1, shape='circle', B=2, N=600, Hx=60)
        add(method='Hansen', cond='D', phi=30, c=c, D=1, shape='circle', B=2, N=600, Hx=40, Hy=30)
        add(method='Hansen', cond='D', phi=30, c=c, D=1, shape='strip', B=2, N=300, Hx=40)
        for hx in (480, 540, 600, 700):  # i_c < 0 beyond about 530 kN (c = 0) and 620 kN (c = 10)
            add(method='Hansen', cond='D', phi=30, c=c, D=1, Hx=hx, **rect)
    for phi in (20, 38):
        add(method='Hansen', cond='D', phi=phi, c=10, D=3, Hx=40, Hy=30, **rect)
    for hx in (6, 60):  # surface footing on sand: the direction of L' governs also with H_L = 0
        add(method='Hansen', cond='D', phi=30, c=0, D=0, Hx=hx, **rect)
    add(method='Hansen', cond='D', phi=30, c=0, D=1, Hy=60, depth=False, **rect)
    for hx, hy in ((60, 0), (0, 60), (40, 30), (6, 0)):
        add(method='Hansen', cond='U', cu=75, D=1, Hx=hx, Hy=hy, **rect)
    add(method='Hansen', cond='U', cu=75, D=1, Hx=40, Hy=30, Mx=360, **rect)
    add(method='Hansen', cond='U', cu=75, D=3, Hx=60, **rect)
    add(method='Hansen', cond='U', cu=75, D=1, shape='square', B=2, L=2, N=600, Hx=60)
    add(method='Hansen', cond='U', cu=75, D=1, shape='circle', B=2, N=600, Hx=60)
    add(method='Hansen', cond='U', cu=75, D=1, shape='circle', B=2, N=600, Hx=40, Hy=30)
    add(method='Hansen', cond='U', cu=75, D=1, shape='strip', B=2, N=300, Hx=40)
    for hx in (440, 460):  # A′cu = 450 kN
        add(method='Hansen', cond='U', cu=75, D=1, Hx=hx, **rect)
    return out


def expected(ref, cs):
    gr = 2.3 if cs.get('profile') == 'Ntc2018' else 1.0
    e = dict(exhausted=ref['exhausted'], qult=ref['qult'] / 1000, rd=ref['qult'] * ref['A'] / gr * 1000)  # MPa, N
    if not ref['exhausted']:
        for key, name in (('c', 'Cohesion'), ('q_', 'Surcharge'), ('g_', 'Weight')):
            t = ref[key]
            e[name] = [t['N'], t['S'], t['D'], t['I']] + ([] if t['add'] is None else t['add'])
    return e


def build():
    rows = []
    for i, cs in enumerate(cases()):
        manual, conv = reference(cs, False), reference(cs, True)
        inp = {k: v for k, v in engine_input(cs).items() if BASE.get(k, object()) != v}
        row = dict(id=f'{i:03d}', input=inp, manual=expected(manual, cs))
        if cs['method'] == 'Terzaghi' and cs['cond'] == 'D':
            row['tolerance'] = 1e-3  # the manual quotes N_gamma = 780 at 48°, the published table 780.1
        if conv['notes']:
            row['convention'] = expected(conv, cs)
            row['note'] = '; '.join(conv['notes'])
            row['relation'] = 'le' if conv['qult'] <= manual['qult'] else 'ge'
        rows.append(row)
    return dict(source='Califfo, Manuale teorico 26.01.2013; generated by califfo_reference.py', base=BASE, cases=rows)


def dump(data):
    lines = ['{"source": ' + json.dumps(data['source'], ensure_ascii=False) + ',',
             ' "base": ' + json.dumps(data['base']) + ',', ' "cases": [']
    lines += ['  ' + json.dumps(r, ensure_ascii=False) + (',' if i < len(data['cases']) - 1 else '') for i, r in enumerate(data['cases'])]
    return '\n'.join(lines + [' ]}']) + '\n'


if __name__ == '__main__':
    text = dump(build())
    if '--write' in sys.argv:
        with open(FROZEN, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
        print('scritto', FROZEN)
    else:
        with open(FROZEN, encoding='utf-8') as f:
            frozen = f.read()
        ok = frozen == text
        print('PASS' if ok else 'FAIL', 'califfo-reference.json', len(build()['cases']), 'casi')
        sys.exit(0 if ok else 1)
