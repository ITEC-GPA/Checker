using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class BraceAnglePairTerminalPlateResult
    {
        public BraceAnglePairBoltedTransferResult Transfer { get; }
        public ConnectionVerification<BraceAnglePairBoltedTransferInput> GussetNetSection { get; }
        public ConnectionVerification<BraceAnglePairBoltedTransferInput> CollinearBlocks { get; }
        public bool CanQualifyWholeJoint => false;
        public ConnectionStatus Status => Transfer.InPlaneComponents.Status == ConnectionStatus.Failed
            || GussetNetSection.Status == ConnectionStatus.Failed || CollinearBlocks.Status == ConnectionStatus.Failed
            ? ConnectionStatus.Failed : ConnectionStatus.NotSupported;
        public IReadOnlyList<string> MissingMechanisms { get; }
        internal BraceAnglePairTerminalPlateResult(BraceAnglePairBoltedTransferResult t,
            ConnectionVerification<BraceAnglePairBoltedTransferInput> n, ConnectionVerification<BraceAnglePairBoltedTransferInput> b)
        {
            Transfer=t; GussetNetSection=n; CollinearBlocks=b;
            var missing=new List<string> { "LOCAL_3D_CONTACT_PRYING_AND_BOLT_NORMAL_FORCES", "GUSSET_SUPPORT_ATTACHMENT_AND_CONCURRENT_SUPPORT_ACTIONS", "MEMBER_PACKING_AND_LOCAL_STABILITY" };
            if(!n.IsComplete)missing.Add("GUSSET_NET_SECTION_OUTSIDE_SCOPE");
            if(!b.IsComplete)missing.Add("NONCOLLINEAR_BLOCK_TEARING");
            MissingMechanisms=Array.AsReadOnly(missing.ToArray());
        }
    }
    /// <summary>Completes the in-plane net/gross gusset section and, for strictly
    /// collinear bolt forces, all one-line edge-block prefixes of the2L terminal.
    /// Local out-of-plane couples are retained in Transfer, never cancelled.</summary>
    public static class BraceAnglePairTerminalPlates
    {
        public static BraceAnglePairTerminalPlateResult Verify(BraceAnglePairBoltedTransferInput p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            var t=BraceAnglePairBoltedTransfer.Verify(p);
            var net=new List<ConnectionCheck>(); var block=new List<ConnectionCheck>(); var trace=new List<SectionResistanceValue>();
            var ni=new List<string>(); var bi=new List<string>();
            if(!t.InPlaneComponents.IsComplete)
            { ni.AddRange(t.InPlaneComponents.Diagnostics); bi.AddRange(t.InPlaneComponents.Diagnostics); return Finish(); }
            var first=p.Angles.Components[0].Input; var g=p.Gusset;
            double d0=first.Bolt.HoleDiameter, area=g.Width*g.Thickness, an=(g.Width-d0)*g.Thickness;
            // y=0 is the physical bolt line. Remove exactly one hole per transverse
            // section, including its own inertia and its actual offset from the centroid.
            double yg=g.Width/2-p.GussetNegativeYEdge, yn=area*yg/an;
            double inertia=g.Thickness*Math.Pow(g.Width,3)/12+area*Math.Pow(yg-yn,2)
                -g.Thickness*(Math.Pow(d0,3)/12+d0*yn*yn);
            double n=t.ActionsOnGussetAtBoltGroup.Fx, m=t.ActionsOnGussetAtBoltGroup.Mz+yn*n;
            double y0=-p.GussetNegativeYEdge-yn,y1=g.Width-p.GussetNegativeYEdge-yn;
            double s0=n/an-m*y0/inertia,s1=n/an-m*y1/inertia;
            double stress=Math.Max(Math.Abs(s0),Math.Abs(s1));
            if(!(an>0&&inertia>0)||double.IsNaN(stress)||double.IsInfinity(stress))ni.Add("GUSSET_NET_SECTION_NUMERIC_RANGE");
            else
            {
                Value("Gusset.NetArea",an,"mm2"); Value("Gusset.NetCentroidFromBoltLine",yn,"mm"); Value("Gusset.NetInertia",inertia,"mm4");
                Value("Gusset.MomentAtNetCentroid",m,"Nmm"); Value("Gusset.EdgeStressNegative",s0,"MPa");Value("Gusset.EdgeStressPositive",s1,"MPa");
                net.Add(new ConnectionCheck("Gusset.GrossAxialYield",n,area*g.Fy/p.Code.Plates.GammaM0,"EN1993-1-1 6.2.3 / NTC2018 4.2.4.1.2.1"));
                net.Add(new ConnectionCheck("Gusset.ElasticNetStress",stress,Math.Min(g.Fy/p.Code.Plates.GammaM0,.9*g.Fu/p.Code.Plates.GammaM2),
                    "Conservative elastic net-section criterion with actual one-hole centroid and N+Mz; no plastic redistribution", "MPa"));
            }
            if(t.InPlaneBoltGroup.Forces.Any(f=>f.Fy!=0))bi.Add("COLLINEAR_LONGITUDINAL_BOLT_FORCES_REQUIRED");
            else
            {
                double heel=t.HeelTangentEdge.Value,toe=t.ToeTangentEdge.Value;
                foreach(int side in new[]{0,1})
                    Blocks("Angle"+side,first.Section.Thickness,first.Section.Fy,first.Section.Fu,
                        p.AngleEndDistance,heel,toe,false,.5);
                Blocks("Gusset",g.Thickness,g.Fy,g.Fu,p.GussetEndDistance,p.GussetNegativeYEdge,g.Width-p.GussetNegativeYEdge,true,1);
            }
            return Finish();

            void Blocks(string label,double thickness,double fy,double fu,double end,double left,double right,bool reverse,double factor)
            {
                var forces=(reverse?t.InPlaneBoltGroup.Forces.Reverse():t.InPlaneBoltGroup.Forces).ToArray();
                double demand=0;
                for(int k=1;k<=forces.Length;k++)
                {
                    demand+=factor*Math.Abs(forces[k-1].Fx);
                    double anv=(end+(k-1)*first.Pitch-(k-.5)*d0)*thickness;
                    foreach(var edge in new[]{new{Id="NegativeEdge",Distance=left},new{Id="PositiveEdge",Distance=right}})
                    {
                        double ant=(edge.Distance-d0/2)*thickness;
                        double capacity=.5*ant*fu/p.Code.Plates.GammaM2+PlasticShear.Resistance(anv, fy, p.Code.Plates.GammaM0);
                        if(!(capacity>0)||double.IsInfinity(capacity)){bi.Add("BLOCK_RESISTANCE_NUMERIC_RANGE");continue;}
                        block.Add(new ConnectionCheck(label+"."+edge.Id+".Prefix"+k,demand,capacity,
                            "EN1993-1-8:2005 3.10.2 eccentric edge block; actual collinear bolt forces; both conservative straight-strip edges"));
                        Value(label+"."+edge.Id+".Prefix"+k+".Ant",ant,"mm2"); Value(label+"."+edge.Id+".Prefix"+k+".Anv",anv,"mm2");
                    }
                }
            }
            void Value(string key,double value,string unit)=>trace.Add(new SectionResistanceValue(key,value,unit));
            BraceAnglePairTerminalPlateResult Finish()=>new BraceAnglePairTerminalPlateResult(t,
                new ConnectionVerification<BraceAnglePairBoltedTransferInput>(p,p.Code,"Steel.Connection.BraceAnglePair.GussetNetSection",
                    "Elastic in-plane net/gross gusset section only, constant-width continuation; no stability or support qualification.",net,trace,ni),
                new ConnectionVerification<BraceAnglePairBoltedTransferInput>(p,p.Code,"Steel.Connection.BraceAnglePair.CollinearBlocks",
                    "One-line longitudinal collinear edge blocks of both connected L legs and gusset; no mixed-direction block model.",block,trace,bi,
                    new[]{"Each free-edge prefix is checked against the sum of absolute actual bolt forces. Opposing forces are never cancelled.",
                    "Angle strips stop at the real root/toe tangent: no capacity is credited to the return leg or root in these block paths.",
                    "The eccentric0.5 factor is always retained. With collinear longitudinal forces a two-sided U path has at least the resistance of the smaller edge path; both edges are explicitly checked.",
                    "Neither these plane checks nor symmetric aggregate actions eliminate the out-of-plane couples of each physical angle."}));
        }
    }
}
