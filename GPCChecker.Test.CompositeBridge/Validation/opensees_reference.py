"""Independent OpenSees reference, N / mm / MPa. No Checker imports or outputs.
Run with OpenSeesPy 3.8.0.0: python opensees_reference.py reference.json
The section equilibrium and material history are solved by OpenSees zeroLengthSection.
https://openseespydoc.readthedocs.io/en/latest/src/Hardening.html
https://openseespydoc.readthedocs.io/en/latest/src/ElasticMultiLinear.html
"""
import json, math, sys
import openseespy.opensees as ops

E, FY, ET = 200000., 250., 2000.
HISO = E * ET / (E - ET)
STEPS = int(sys.argv[2]) if len(sys.argv) > 2 else 20000

def rectangle(component, bottom, width, height, layers):
    rows = []
    for i in range(layers):
        mid = bottom + (i + .5) * height / layers
        for sign in (-1, 1):
            rows.append(dict(component=component, y=mid + sign * height / layers / math.sqrt(12), area=width * height / layers / 2))
    return rows

steel = rectangle('steel', -300, 200, 20, 4) + rectangle('steel', -280, 10, 280, 28) + rectangle('steel', 0, 150, 15, 4)
slab = rectangle('concrete', 15, 600, 100, 32)

def build(fibers, plastic, concrete=False, eigen=0, birth=(0, 0), steps=STEPS):
    ops.wipe(); ops.model('basic', '-ndm', 2, '-ndf', 3)
    ops.node(1, 0, 0); ops.node(2, 0, 0); ops.fix(1, 1, 1, 1); ops.fix(2, 0, 1, 0)
    if plastic: ops.uniaxialMaterial('Hardening', 1, E, FY, HISO, 0.)
    else: ops.uniaxialMaterial('Elastic', 1, E)
    # Identical tabulated envelope is passed as input to two independent implementations.
    if concrete:
        ops.uniaxialMaterial('ElasticMultiLinear', 2, '-strain', -.0035, -.002, -.001, 0., .01, '-stress', -30., -30., -22.5, 0., 0.)
    else: ops.uniaxialMaterial('Elastic', 2, 30000.)
    ops.section('Fiber', 1)
    for i, f in enumerate(fibers):
        material = 1 if f['component'] == 'steel' else 2
        if material == 2 and (eigen or birth != (0, 0)):
            tag = 1000 + i
            # OpenSees adds initStrain: subtract birth and imposed contraction.
            ops.uniaxialMaterial('InitStrainMaterial', tag, material, -birth[0] + birth[1] * f['y'] - eigen)
            material = tag
        # OpenSees FiberSection shifts its reference to the area centroid by default.
        ops.fiber(f['y'], 0., f['area'], material)
    ops.element('zeroLengthSection', 1, 1, 2, 1)
    ops.constraints('Plain'); ops.numberer('Plain'); ops.system('BandGeneral')
    ops.test('NormUnbalance', 1.e-3, 4000); ops.algorithm('ModifiedNewton', '-initial'); ops.integrator('LoadControl', 1. / steps); ops.analysis('Static')
    if concrete: ops.algorithm('NewtonLineSearch')

def run_case(name, fibers, loads, plastic=True, concrete=False, eigen=0, birth=(0, 0)):
    steps = STEPS * 10 if name == 'plastic_N_M_cycle' else STEPS
    build(fibers, plastic, concrete, eigen, birth, steps)
    yg = sum(f['area'] * f['y'] for f in fibers) / sum(f['area'] for f in fibers)
    rows = []; old_n = old_m = 0.
    for index, (n, m) in enumerate(loads):
        print(name, index, n, m, flush=True)
        ops.timeSeries('Linear', index + 1); ops.pattern('Plain', index + 1, index + 1)
        # Section local x is axial; curvature sign matches epsilon = e0 - k*y.
        ops.load(2, n - old_n, 0., m - old_m + (n - old_n) * yg)
        if ops.analyze(steps) != 0: raise RuntimeError(name + ' did not converge')
        e_centroid, k = ops.eleResponse(1, 'section', 'deformation')
        fiber_values = ops.eleResponse(1, 'section', 'fiberData')
        stresses = [fiber_values[i + 3] for i in range(0, len(fiber_values), 5)]
        strains = [fiber_values[i + 4] for i in range(0, len(fiber_values), 5)]
        assert len(stresses) == len(fibers)
        rows.append(dict(n=n, m=m, axial=e_centroid + k * yg, curvature=k, stresses=stresses, strains=strains))
        old_n, old_m = n, m
        ops.loadConst('-time', 0.)
    return dict(name=name, substeps=steps, plastic=plastic, concrete=concrete, eigen=eigen, birth=list(birth), fibers=fibers, stages=rows)

steel_centroid = sum(f['area'] * f['y'] for f in steel) / sum(f['area'] for f in steel)
cases = [
    run_case('elastic_N_M', steel, [(0, 1.e8), (-5.e5, 1.5e8), (2.e5, -1.e8), (0, 0)], False),
    run_case('plastic_axial_cycle', steel, [(n, -n * steel_centroid) for n in [1.e6, 2.4e6, 0, -3.e6, 0, 3.5e6]]),
    run_case('plastic_bending_cycle', steel, [(0, 1.e8), (0, 2.e8), (0, 3.e8), (0, 0), (0, -3.5e8), (0, 0)]),
    run_case('plastic_N_M_cycle', steel, [(-2.e5, 1.e8), (-4.e5, 2.5e8), (3.e5, 3.e8), (0, 0), (2.e5, -3.e8)]),
    run_case('composite_nonlinear', steel + slab, [(0, 1.e8), (-5.e5, 2.e8), (-1.e6, 3.e8), (0, 2.e8), (0, 0)], True, True),
    run_case('free_composite_shrinkage', steel + slab, [(0, 0)], False, False, -.0002),
]
# Independent casting plane obtained from OpenSees steel-only model, not from Checker.
cast = run_case('before_cast', steel, [(0, 8.e7)], False)
birth = (cast['stages'][0]['axial'], cast['stages'][0]['curvature'])
cases.append(run_case('cast_then_shrink', steel + slab, [(0, 8.e7), (-1.e5, 1.2e8)], False, False, -.0002, birth))
output = dict(solver='OpenSees', version=ops.version(), substeps=STEPS, package='openseespy 3.8.0.0 / openseespywin 3.8.0.0',
              units='N mm MPa', steel=dict(E=E, fy=FY, tangent=ET, Hiso=HISO), cases=cases)
with open(sys.argv[1], 'w', encoding='utf-8') as stream: json.dump(output, stream, indent=2, allow_nan=False)
print(f"OpenSees {ops.version()}: {len(cases)} cases / {sum(len(c['stages']) for c in cases)} converged states")
