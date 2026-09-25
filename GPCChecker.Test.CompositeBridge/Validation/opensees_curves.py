"""Independent deformation-controlled reference. No Checker imports or outputs.
OpenSees Fiber + zeroLengthSection + DisplacementControl. Units N / mm / MPa.
The two rectangular material envelopes are explicitly supplied to both solvers.
Usage: python opensees_curves.py output.json [substeps=200]
"""
import json, math, sys
import openseespy.opensees as ops

E, FY, ET = 200000., 250., 2000.
STEPS = int(sys.argv[2]) if len(sys.argv) > 2 else 200
def rectangle(component, bottom, width, height, layers):
    return [dict(component=component, y=bottom+(i+.5)*height/layers+sign*height/layers/math.sqrt(12), area=width*height/layers/2)
            for i in range(layers) for sign in (-1, 1)]
steel = rectangle('steel', -300, 200, 20, 4)+rectangle('steel', -280, 10, 280, 28)+rectangle('steel', 0, 150, 15, 4)
slab = rectangle('concrete', 15, 600, 100, 32)

def run(name, mode, plastic, concrete, n, targets, reference=0):
    steps=STEPS*10 if name=='plastic_steel_MC_cycle' else STEPS
    fibers=steel+slab if concrete else steel
    yg=sum(f['area']*f['y'] for f in fibers)/sum(f['area'] for f in fibers)
    ops.wipe(); ops.model('basic','-ndm',2,'-ndf',3)
    ops.node(1,0,0);ops.node(2,0,0);ops.fix(1,1,1,1);ops.fix(2,0,1,1)
    if plastic: ops.uniaxialMaterial('Hardening',1,E,FY,E*ET/(E-ET),0.)
    else: ops.uniaxialMaterial('Elastic',1,E)
    ops.uniaxialMaterial('ElasticMultiLinear',2,'-strain',-.0035,-.002,-.001,0.,.01,'-stress',-30.,-30.,-22.5,0.,0.)
    ops.section('Fiber',1)
    for f in fibers: ops.fiber(f['y'],0.,f['area'],1 if f['component']=='steel' else 2)
    ops.element('zeroLengthSection',1,1,2,1)
    ops.constraints('Plain');ops.numberer('Plain');ops.system('BandGeneral')
    # Mixed N/Nmm residual. 1e-3 Nmm is below the measured roundoff floor of large moments.
    ops.test('NormUnbalance',1e-3,200);ops.algorithm('Newton');ops.integrator('LoadControl',1/steps);ops.analysis('Static')
    ops.timeSeries('Linear',1);ops.pattern('Plain',1,1);ops.load(2,n,0.,0.)
    if ops.analyze(steps)!=0: raise RuntimeError(name+' preload failed')
    ops.loadConst('-time',0.)
    def state(target):
        data=ops.eleResponse(1,'section','fiberData')
        stresses=data[3::5];strains=data[4::5]
        ec,k=ops.eleResponse(1,'section','deformation')
        force=sum(s*f['area'] for s,f in zip(stresses,fibers))
        moment=-sum(s*f['area']*f['y'] for s,f in zip(stresses,fibers))
        return dict(target=target, axial=ec+k*yg, curvature=k,n=force,m=moment,mref=moment+force*reference,stresses=stresses,strains=strains)
    initial=state(0.)
    if mode=='MC':
        ops.remove('sp',2,3)
        # Release the rotation constraint while replacing its reaction by the same external moment.
        ops.timeSeries('Constant',2);ops.pattern('Plain',2,2);ops.load(2,0.,0.,initial['m']+n*yg)
    ops.timeSeries('Linear',3);ops.pattern('Plain',3,3)
    ops.load(2,0.,0.,1.) if mode=='MC' else ops.load(2,1.,0.,0.)
    old=0.; rows=[]
    for target in targets:
        if target!=old:
            ops.integrator('DisplacementControl',2,3 if mode=='MC' else 1,(target-old)/steps)
            if ops.analyze(steps)!=0: raise RuntimeError(name+' failed at '+str(target))
        rows.append(state(target));old=target
    return dict(name=name,mode=mode,plastic=plastic,concrete=concrete,n=n,reference=reference,substeps=steps,fibers=fibers,initial=initial,points=rows)

cases=[
run('elastic_eccentric_MC','MC',False,False,-1e5,[1e-6,5e-6,0.,-5e-6],-100),
run('plastic_steel_MC_cycle','MC',True,False,0.,[2e-6,1e-5,4e-5,0.,-4e-5,0.]),
run('plastic_steel_MC_compression','MC',True,False,-5e5,[2e-6,1e-5,3e-5,0.,-3e-5]),
run('composite_MC','MC',True,True,-1e5,[1e-6,3e-6,8e-6,1e-5,3e-6,0.]),
run('steel_NE_cycle','NE',True,False,0.,[.0005,.002,.006,.004,0.,-.004,0.]),
run('composite_NE_compression','NE',True,True,0.,[-.00025,-.0008,-.0015,-.0025,-.0033,-.001,0.])]
with open(sys.argv[1],'w',encoding='utf-8') as f: json.dump(dict(solver='OpenSees',version=ops.version(),units='N mm MPa',cases=cases),f,indent=2,allow_nan=False)
print('OpenSees',ops.version(),len(cases),'curves',sum(len(c['points']) for c in cases),'states',flush=True)
