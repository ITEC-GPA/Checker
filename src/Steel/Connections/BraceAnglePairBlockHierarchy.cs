using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Sufficient rectangular block/fastener hierarchy for the actual straight
    /// connected strips and central gusset. All orthogonal U/L prefixes and contiguous
    /// transverse subsets, both directions. The demand is unreduced bolt resistance,
    /// NOT applied load. Failure excludes this sufficient method, not proof of rupture.</summary>
    public static class BraceAnglePairBlockHierarchy
    {
        public static ConnectionVerification<BraceAnglePairBoltedTransferInput> Verify(BraceAnglePairBoltedTransferInput p)
            => Verify(p,BraceAnglePairBoltedTransfer.Verify(p));
        internal static ConnectionVerification<BraceAnglePairBoltedTransferInput> Verify(BraceAnglePairBoltedTransferInput p, BraceAnglePairBoltedTransferResult r)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();var issues=new List<string>();
            if(!r.InPlaneComponents.IsComplete)issues.AddRange(r.InPlaneComponents.Diagnostics);
            else
            {
                var first=p.Angles.Components[0].Input;var bolt=first.Bolt;
                var raw=BoltStrength.Verify(new BoltStrengthInput(p.Code,bolt,0,0,1,0,0,p.HoleClearanceServiceabilitySource));
                if(!raw.IsComplete)issues.AddRange(raw.Diagnostics);
                else
                {
                    double rb=raw.Checks.Single(c=>c.Id=="Bolt.Shear").Resistance;
                    double heel=r.InPlaneComponents.Trace.Single(t=>t.Name=="AngleHeelTangentEdge").Value;
                    double toe=r.InPlaneComponents.Trace.Single(t=>t.Name=="AngleToeTangentEdge").Value;
                    Plate("Angle0",first.Section.Thickness,first.Section.Fy,first.Section.Fu,heel,toe,rb);
                    Plate("Angle1",first.Section.Thickness,first.Section.Fy,first.Section.Fu,heel,toe,rb);
                    Plate("Gusset",p.Gusset.Thickness,p.Gusset.Fy,p.Gusset.Fu,p.GussetNegativeYEdge,p.Gusset.Width-p.GussetNegativeYEdge,2*rb);
                    void Plate(string name,double thickness,double fy,double fu,double low,double high,double resistancePerBolt)
                    {
                        var points=r.InPlaneBoltGroup.Points;
                        double xmin=points.Min(q=>q.X)-p.AngleEndDistance,xmax=points.Max(q=>q.X)+p.GussetEndDistance;
                        // Both actual and artificial longitudinal free ends are included;
                        // the artificial end removes continuation material conservatively.
                        foreach(int axis in new[]{0,1})foreach(int sign in new[]{-1,1})
                        {
                            double[] us=points.Select(q=>sign*(axis==0?q.X:q.Y)).Distinct().OrderBy(v=>v).ToArray();
                            double[] vs=points.Select(q=>axis==0?q.Y:q.X).Distinct().OrderBy(v=>v).ToArray();
                            double start=sign==1?(axis==0?xmin:-low):-(axis==0?xmax:high);
                            double vmin=axis==0?-low:xmin,vmax=axis==0?high:xmax;int id=0;
                            for(int k=1;k<=us.Length;k++)for(int begin=0;begin<vs.Length;begin++)for(int end=begin;end<vs.Length;end++)
                            {
                                int count=end-begin+1;double lv=us[k-1]-start-(k-.5)*bolt.HoleDiameter;
                                Path(vs[end]-vs[begin]-(count-1)*bolt.HoleDiameter,2*lv,"U");
                                if(begin==0)Path(vs[end]-vmin-(count-.5)*bolt.HoleDiameter,lv,"Lmin");
                                if(end==vs.Length-1)Path(vmax-vs[begin]-(count-.5)*bolt.HoleDiameter,lv,"Lmax");
                                void Path(double lt,double ls,string mode)
                                {
                                    double capacity=thickness*(.5*lt*fu/p.Code.Plates.GammaM2+ls*fy/(Math.Sqrt(3)*p.Code.Plates.GammaM0));
                                    if(lt<0||ls<=0||double.IsInfinity(capacity)){issues.Add(name+".INVALID_BLOCK_PATH");return;}
                                    checks.Add(new ConnectionCheck(name+".Axis"+axis+".Direction"+sign+".Path"+(++id)+"."+mode,
                                        k*count*resistancePerBolt,capacity,"EN1993-1-8:2005 3.10.2(3); sufficient rectangular block/fastener hierarchy, not applied demand"));
                                }
                            }
                        }
                    }
                }
            }
            return new ConnectionVerification<BraceAnglePairBoltedTransferInput>(p,p.Code,"Steel.Connection.BraceAnglePair.BlockHierarchy",
                "Sufficient exclusion of rectangular block failure, including noncollinear bolt groups; no general rupture interaction or whole-joint qualification.",checks,trace,issues,
                new[]{"All bolt capacities are unreduced and individual; two planes for the gusset, one for each angle. No opposing-force cancellation.",
                    "Both orthogonal axes, every free-edge prefix and contiguous transverse subset are checked; eccentric0.5 factor retained. Angle material stops at actual root/toe tangents.",
                    "A failed hierarchy is an applicability failure and must not be presented as actual block rupture under the supplied load."});
        }
    }
}
