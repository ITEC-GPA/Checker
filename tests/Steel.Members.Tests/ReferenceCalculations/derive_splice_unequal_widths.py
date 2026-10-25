"""Independent rational membrane/free-body oracle. No GPC/.NET imports.
Parent: 400x30 + 320x16 + 400x30. Local M is carried by two axial face forces.
The oracle solves the two force equations first, then interpolates face strains.
It does NOT invert the stiffness matrix used in the production audit.
Run: python derive_splice_unequal_widths.py <new-output.json>. Refuses overwrite.
"""
import json, sys
from fractions import Fraction as Q
A = Q(29120)
I = 2 * (Q(400*30**3,12) + 400*30*175**2) + Q(16*320**3,12)
rows = []
for width in [260, 360]:
    for sign in [1,-1]:
        for moment in [0, 50_000_000]:
            n, m = Q(sign*200_000), Q(sign*moment)
            for top in [True,False]:
                y=Q(175 if top else -175); yo=Q(25 if top else -25); yi=-yo
                nf=n*12000/A-m*12000*y/I
                mf=m*Q(400*30**3,12)/I
                outer=(-mf-yi*nf)/(yo-yi); inner=nf-outer
                ao, ai = Q(width*20), Q(2*140*20)
                eo,ei=outer/(210000*ao),inner/(210000*ai)
                k=(eo-ei)/(yo-yi); e0=eo-k*yo
                rows.append(dict(width=width,sign=sign,moment=moment,top=top,values={
                    "AreaCentroidOffset":float((ao*yo+ai*yi)/(ao+ai)),
                    "Determinant":float(ao*ai*(yo-yi)**2),
                    "OuterForce":float(outer),"BothInnerForce":float(inner),
                    "MembraneStrainAtParentCentroid":float(e0),"FaceStrainGradient":float(k)}))
with open(sys.argv[1],"x",encoding="utf-8") as output:
    json.dump({"basis":"Exact rational free bodies and independently interpolated face strains; synthetic, not a published full joint","rows":rows},output,indent=2)
    output.write("\n")

