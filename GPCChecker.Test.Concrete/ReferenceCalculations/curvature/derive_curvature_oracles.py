"""Independent exact clipped-polynomial moment/curvature oracle; Python stdlib only.

Reads original XML geometry/material and CSV input columns only. No DLL and no
historical result columns. Polygons are clipped at the 8 linear material segments;
area/first moments are then integrated exactly by a degree-two triangle rule.
Steel is elastic-perfectly-plastic, bars displace concrete at their centres.

The Newton routine below belongs only to this independent verification script.
It solves exact polygon equilibrium and an active concrete/yield constraint,
not the fixed-Gauss-point equations used by Checker. Report its own residuals.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import xml.etree.ElementTree as ET



def area(poly):
    return sum(a[0]*b[1]-a[1]*b[0] for a, b in zip(poly, poly[1:]+poly[:1]))/2


def clip(poly, p, level, above):
    if not poly:
        return []
    out = []
    prev = poly[-1]
    fp = p[0]+p[1]*prev[0]+p[2]*prev[1]-level
    for curr in poly:
        fc = p[0]+p[1]*curr[0]+p[2]*curr[1]-level
        ip, ic = (fp >= 0, fc >= 0) if above else (fp <= 0, fc <= 0)
        if ip != ic:
            t = fp/(fp-fc)
            out.append((prev[0]+t*(curr[0]-prev[0]), prev[1]+t*(curr[1]-prev[1])))
        if ic:
            out.append(curr)
        prev, fp = curr, fc
    return out


def sigma(e, fcd):
    if e >= 0 or e < -.0035:
        return 0.
    if e <= -.002:
        return -fcd
    t = -e/.002*8
    j = int(t)
    a, b, t = j/8, (j+1)/8, t-j
    return -fcd*((1-t)*(2*a-a*a)+t*(2*b-b*b))


def concrete(poly, p, fcd):
    result = [0., 0., 0.]
    if p[1] == 0 and p[2] == 0:
        ar = area(poly)
        sx = sum((a[0]+b[0])*(a[0]*b[1]-a[1]*b[0])/6 for a, b in zip(poly, poly[1:]+poly[:1]))
        sy = sum((a[1]+b[1])*(a[0]*b[1]-a[1]*b[0])/6 for a, b in zip(poly, poly[1:]+poly[:1]))
        f = sigma(p[0], fcd)*math.copysign(1, ar)
        return [f*ar, -f*sy, f*sx]
    for band in range(9):
        cut = clip(poly, p, -band*.002/8, False)
        cut = clip(cut, p, -(band+1)*.002/8 if band < 8 else -.0035, True)
        if len(cut) < 3:
            continue
        if area(cut) < 0:
            cut.reverse()
        for i in range(1, len(cut)-1):
            tri = [cut[0], cut[i], cut[i+1]]
            ar = area(tri)
            for vertex in tri:
                x = sum(v[0] for v in tri)/6+vertex[0]/2
                y = sum(v[1] for v in tri)/6+vertex[1]/2
                force = ar/3*sigma(p[0]+p[1]*x+p[2]*y, fcd)
                result[0] += force
                result[1] -= force*y
                result[2] += force*x
    return result


def resultants(case, p):
    ans = [0., 0., 0.]
    for poly in case['polygons']:
        for j, v in enumerate(concrete(poly, p, case['fcd'])):
            ans[j] += v
    for x, y, diameter in case['bars']:
        e = p[0]+p[1]*x+p[2]*y
        stress = max(-case['fyd'], min(case['fyd'], 200000*e))
        force = (stress-sigma(e, case['fcd']))*math.pi*diameter**2/4
        ans[0] += force
        ans[1] -= force*y
        ans[2] += force*x
    # Convert from section-origin solver moments to the actual archived force pole.
    ans[1] += ans[0]*case['origin'][1]
    ans[2] -= ans[0]*case['origin'][0]
    return ans


def solve3(a, b):
    a = [list(row)+[rhs] for row, rhs in zip(a, b)]
    for i in range(3):
        k = max(range(i, 3), key=lambda j: abs(a[j][i]))
        a[i], a[k] = a[k], a[i]
        if abs(a[i][i]) < 1e-20:
            raise ArithmeticError('singular independent Jacobian')
        f = a[i][i]
        a[i] = [v/f for v in a[i]]
        for j in range(3):
            if j != i:
                f = a[j][i]
                a[j] = [u-f*v for u, v in zip(a[j], a[i])]
    return [row[-1] for row in a]


def solve(case, mode):
    points = case['outline']
    dimension = max(max(v[k] for v in points)-min(v[k] for v in points) for k in (0, 1))
    scale = sum(abs(area(p)) for p in case['polygons'])*case['fcd']
    angle = case['theta']*math.pi/180
    c, s = math.cos(angle), math.sin(angle)
    def plane(q):
        return [q[0]*.001, q[1]*.001/dimension, q[2]*.001/dimension]
    def residual(q):
        p = plane(q)
        n, mx, my = resultants(case, p)
        if mode == 'ultimate':
            active = min(p[0]+p[1]*x+p[2]*y for x, y in points)/.0035+1
        else:
            active = max(abs(p[0]+p[1]*x+p[2]*y) for x, y, _ in case['bars'])/(case['fyd']/200000)-1
        return [(n-case['N'])/scale, (my*c-mx*s)/(scale*dimension), active]
    norm = lambda r: math.sqrt(sum(x*x for x in r))
    for gradient in ([14, 8, 25, 5] if mode == 'ultimate' else [6, 3, 10, 15]):
        q = [0., gradient*s, -gradient*c]
        p = plane(q)
        if mode == 'ultimate':
            q[0] = (-.0035-min(p[1]*x+p[2]*y for x, y in points))/.001
        else:
            q[0] = case['N']/(scale*1000/1.875)  # geometric initial guess only
        for iteration in range(100):
            f = residual(q)
            if norm(f) < 2e-11:
                p = plane(q); n, mx, my = resultants(case, p)
                if mx*c+my*s <= 0:
                    break
                return dict(mode=mode, strain_at_section_origin=p[0], gradient_x=p[1], gradient_y=p[2],
                    curvature=math.hypot(p[1], p[2]), N=n, M1=mx, M2=my, moment=mx*c+my*s,
                    axial_residual=n-case['N'], moment_cross_residual=my*c-mx*s,
                    active_normalized_residual=f[2], iterations=iteration)
            step = 1e-5
            columns = []
            for j in range(3):
                hi, lo = q[:], q[:]
                hi[j] += step; lo[j] -= step
                columns.append([(a-b)/(2*step) for a, b in zip(residual(hi), residual(lo))])
            try:
                delta = solve3([[columns[j][i] for j in range(3)] for i in range(3)], [-v for v in f])
            except ArithmeticError:
                break
            improved = False
            for k in range(24):
                test = [v+d/2**k for v, d in zip(q, delta)]
                if norm(residual(test)) < norm(f):
                    q, improved = test, True
                    break
            if not improved:
                break
    raise ArithmeticError('Independent exact oracle did not converge: '+str(case['id'])+' '+mode)


def inputs(fixtures):
    root = ET.parse(fixtures/'detailing-sections.xml').getroot()
    z = '{http://schemas.microsoft.com/2003/10/Serialization/}'
    ids = {v.get(z+'Id'): v for v in root.iter() if v.get(z+'Id')}
    name = lambda v: v.tag.split('}')[-1]
    def child(v, n):
        a = next(x for x in v if name(x) == n)
        return ids[a.get(z+'Ref')] if a.get(z+'Ref') else a
    sections = {}
    for parent in root.iter():
        keys = [v for v in parent if name(v) == 'Key']
        if keys and keys[0].text in ['R300x500', 'R400x400', 'C400', 'T1200x800']:
            sections[keys[0].text] = child(parent, 'Value')
    for line in (fixtures/'curvature-legacy.csv').read_text(encoding='utf-8-sig').splitlines():
        if not line or not line.split(';')[0].isdigit():
            continue
        row = line.split(';')[:15]  # explicitly NEVER load captured output columns
        section = sections[row[1]]
        fill = child(child(child(section, 'SectionShape'), 'Shape2d'), 'Fill')
        outline = [(float(child(child(fill, 'Point'+str(i)), 'X').text), float(child(child(fill, 'Point'+str(i)), 'Y').text))
                   for i in range(int(child(fill, 'Count').text))]
        material = child(section, 'ConcreteMaterial')
        assert child(material, 'CompressionStressStrainDiagrams').text == 'ParabolaRectangle'
        bars = []
        for item in child(section, 'RebarCollection'):
            bar = child(item, 'Value'); position = child(bar, 'Position'); shape = child(bar, 'RebarSection')
            bars.append((float(child(position, 'X').text), float(child(position, 'Y').text), float(child(shape, 'Diameter').text)))
            steel = child(shape, 'SteelMaterial')
            assert float(child(steel, 'Fyk').text) == 450
            assert float(child(steel, 'Fu').text) == 450
            assert float(child(steel, 'ElasticModulusTension').text) == 200000
            assert float(child(steel, 'StrainUTension').text) == .1
            assert float(child(bar, 'EpsilonP').text) == 0
        assert float(child(material, 'StrainYCompression').text) == -.002
        assert float(child(material, 'StrainUCompression').text) == -.0035
        assert [float(v) for v in row[5].split(',')] == [-1, 0, 0]
        assert [float(v) for v in row[6].split(',')] == [0, -1, 0]
        parameters = dict(v.split('=') for v in row[3].split(','))
        polygons = [outline]
        if row[1] == 'T1200x800':
            rectangle = lambda x0, x1, y0, y1: [(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
            polygons = [rectangle(-600,600,44.230769230769226,294.2307692307692),
                        rectangle(-200,200,-505.7692307692308,44.230769230769226)]
        yield dict(id=int(row[0]), section=row[1], N=float(row[8])*1000, theta=float(row[9]),
            origin=[float(x) for x in row[4].split(',')], outline=outline, polygons=polygons, bars=bars,
            fcd=abs(float(child(material, 'Fck').text))*float(parameters['AlphaCC'])/float(parameters['GammaC']),
            fyd=450/float(parameters['GammaS']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixtures', type=Path,
        default=Path(__file__).resolve().parents[2] / 'Fixtures',
        help='Historical input folder; default is ../../Fixtures relative to ReferenceCalculations/curvature/.')
    parser.add_argument('--output', type=Path,
        default=Path(__file__).resolve().parent / 'curvature-independent-oracles.json')
    args = parser.parse_args()
    fixtures = args.fixtures.resolve()
    cases = []
    for case in inputs(fixtures):
        result = dict(id=case['id'], section=case['section'], area=sum(abs(area(p)) for p in case['polygons']),
            fcd=case['fcd'], fyd=case['fyd'], N=case['N'], theta=case['theta'],
            ultimate=solve(case, 'ultimate'), first_yield=solve(case, 'yield'))
        cases.append(result)
        print(case['id'], case['section'], 'ultimate', result['ultimate']['moment'], result['ultimate']['curvature'],
              'yield', result['first_yield']['moment'], result['first_yield']['curvature'])
    result = dict(method='Independent exact clipped polygon, explicit 8-segment concrete and elastic-perfectly-plastic bars; not captured production output',
        units='N, mm, Nmm, 1/mm', cases=cases,
        unchanged_fixture_sha256={name:hashlib.sha256((fixtures/name).read_bytes()).hexdigest()
            for name in ['detailing-sections.xml','curvature-legacy.csv','curvature-contract-0.0.17.csv']})
    args.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
