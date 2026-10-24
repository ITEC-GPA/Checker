"""Independent arithmetic for phase3c.3. No Checker/Model import or test run."""
from math import hypot, sqrt, sin, cos, radians
import json


def forces(points, force, moment):
    polar = sum(x*x + y*y for x, y in points)
    values = [(force/len(points) - moment*y/polar, moment*x/polar)
              for x, y in points]
    return {"J_mm2": polar, "forces_N": values,
            "resultants_N": [hypot(x, y) for x, y in values],
            "reconstructed": [sum(x for x, y in values),
                              sum(y for x, y in values),
                              sum(p[0]*f[1]-p[1]*f[0] for p, f in zip(points, values))]}


jrc = {"source": "JRC SX034a-EN-EU sheets5/6; exact, not rounded alpha/beta",
       "bolt_shear_N": .6*800*245/1.25,
       "angle_end_bearing_N": 2.5*(50/(3*22))*430*20*12/1.25,
       "angle_inner_bearing_N": 2.5*(80/(3*22)-.25)*430*20*12/1.25,
       "angle_gross_N": 2270*275,
       "angle_net_N": (.5+.2*(80/22-2.5)/2.5)*(2270-22*12)*430/1.25}
sbe_points = [(-16.25,-30), (48.75,-30), (-48.75,30), (16.25,30)]
sbe = forces(sbe_points, 406900, 406900*44.5)
sbe["source"] = "SBE_SS5 AppendixB TableB.13; staggered2lines; kernel-only benchmark"
sbe["moment_Nmm"] = 406900*44.5
angle = radians(90-42)  # Figure angle is from vertical: TableB.14 uses 48deg from horizontal.
sbe["rotated48deg_forces_N"] = [(x*cos(angle)+y*sin(angle), -x*sin(angle)+y*cos(angle))
                                for x, y in sbe["forces_N"]]
sbe["bearing_ellipse_bolt1_using_published_kN"] = (84.37/154.22)**2+(143.22/264.38)**2
sbe["bearing_ellipse_bolt2_using_published_kN"] = (182.86/264.38)**2+(54.54/257.60)**2
sbe["block_gusset_N"] = .5*510*633.6/1.25+355*3533.1/sqrt(3)
sbe["block_angle_corrected_N"] = .5*510*933.6/1.25+355*1402.5/sqrt(3)
sbe["block_angle_printed_inconsistent_N"] = 407910

# Sharp actual L: two non-overlapping rectangles, no catalog approximation.
area = 150*10 + (100-10)*10
cx = (150*10*5 + (100-10)*10*55)/area
cy = (150*10*75 + (100-10)*10*5)/area
line = forces([(-90,0),(-30,0),(30,0),(90,0)], 200000, (75-cy)*200000)
line.update({"source": "Independent one-line analytical detail; not a published joint",
             "angle_A_mm2": area, "angle_centroid_mm": [cx,cy],
             "centroid_normal_positions_mm": [-10-cx,10+cx],
             "boltline_normal_positions_mm": [-15,15],
             "local_moments_at_leg_midplane_Nmm": [-(cx-5)*100000,(cx-5)*100000],
             "local_moments_at_gusset_faces_Nmm": [-cx*100000,cx*100000],
             "total_Mz_Nmm": (75-cy)*200000,
             "bolt_double_shear_N": 2*.6*800*157/1.25,
             "angle_gross_N": area*275,
             "angle_effective_net_N": (.5+.2*(60/18-2.5)/2.5)*(area-18*10)*430/1.25})
result = {"units": "N, Nmm, mm, MPa", "JRC": jrc, "SBE": sbe, "one_line": line}
print(json.dumps(result, indent=2))
