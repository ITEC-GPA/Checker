using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Supplementary IN-PLANE checks of the same end-plate case. Out-of-plane
    /// bending/prying remain the EN6.2.4/6.2.7 component model, not a copied splice formula.
    /// Every path and both load directions are retained; the column is bounded by its
    /// actual ends, not by the height of the end plate. N, mm, MPa.</summary>
    public sealed class EndPlateInPlaneResult
    {
        public EndPlateBoltComponentResult Bolts { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete => Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public ConnectionStatus Status => Checks.Any(c => !c.Passed) ? ConnectionStatus.Failed
            : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        internal EndPlateInPlaneResult(EndPlateBoltComponentResult bolts,List<ConnectionCheck> checks,
            List<SectionResistanceValue> trace,List<string> issues)
        { Bolts=bolts;Checks=Array.AsReadOnly(checks.ToArray());Trace=Array.AsReadOnly(trace.ToArray());Diagnostics=Array.AsReadOnly(issues.ToArray()); }
    }

    internal static class EndPlateInPlaneChecks
    {
        public static EndPlateInPlaneResult Verify(EndPlateBoltComponentResult bolts)
        {
            if(bolts==null)throw new ArgumentNullException(nameof(bolts));
            var b=bolts.BoltAssessment.Input;var m=b.MomentComponents.ComponentAssessment.Input;
            var rows=b.Rows;var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();var issues=new List<string>();
            if(!bolts.BoltAssessment.IsComplete)issues.Add("CompleteBoltAssessmentRequired");
            if(rows.Any(r=>r.LeftShear!=r.RightShear))issues.Add("SymmetricIndividualRowsRequired");
            if(m.PanelConfiguration!=ColumnWebPanelConfiguration.SingleSidedJoint||m.Beta!=1)issues.Add("SingleSidedStandardJointRequired");
            if(m.EndPlate.Width<m.Beam.Width)issues.Add("PlateMustCoverWholeBeamFlange");
            if(m.EndPlate.Thickness<4||m.Column.FlangeThickness<4)issues.Add("PlateThicknessAtLeastFourMillimetres");
            if(issues.Count==0)
            {
                double top=m.ExtensionGeometry==null?0:-m.ExtensionGeometry.FlangeToRow-m.ExtensionGeometry.EndDistance;
                double bottom=m.Beam.Height+m.CompressionEndPlateProjection;
                double firstTension=rows.First(r=>r.MomentRowIndex.HasValue).Y;
                double lastTension=rows.Last(r=>r.MomentRowIndex.HasValue).Y;
                Ply("Plate",m.EndPlate,top,bottom);
                Ply("ColumnFlange",new ConnectionPlate(m.Column.Width,m.Column.FlangeThickness,m.ColumnFlangeFy,b.ColumnFlangeFu),
                    firstTension-m.ColumnFirstEndContinuation,lastTension+m.ColumnLastEndContinuation);
            }
            return new EndPlateInPlaneResult(bolts,checks,trace,issues);

            void Check(string id,double demand,double resistance,string reference)
            {
                if(!(resistance>0)||double.IsInfinity(resistance)||double.IsNaN(demand)||double.IsInfinity(demand/resistance))issues.Add("NumericRange:"+id);
                else checks.Add(new ConnectionCheck(id,demand,resistance,reference));
            }
            void Value(string id,double value,string unit){if(double.IsNaN(value)||double.IsInfinity(value))issues.Add("NumericRange:"+id);else trace.Add(new SectionResistanceValue(id,value,unit));}
            void Ply(string name,ConnectionPlate p,double top,double bottom)
            {
                double d0=m.Bolt.HoleDiameter,g=m.InternalGeometry.BoltGauge,t=p.Thickness;
                double e=(p.Width-g)/2, gm0=m.Policy.Code.Plates.GammaM0,gm2=m.Policy.Code.Plates.GammaM2;
                // Extra conservative net-shear section. It is not called a net-tension
                // resistance of an end plate carrying transverse bolt tension.
                double ag=p.Width*t,an=(p.Width-2*d0)*t;
                Value(name+".GrossArea",ag,"mm2");Value(name+".NetArea",an,"mm2");
                Check(name+".GrossShear",m.Shear,PlasticShear.Resistance(ag, p.Fy, gm0),"EN1993-1-1 6.2.6; full joint shear envelope");
                Check(name+".NetShearBound",m.Shear,an*Math.Min(p.Fy/gm0,p.Fu/gm2)/Math.Sqrt(3),"Supplementary elastic net-area shear bound; no out-of-plane fracture formula implied");
                var baseBolt=BoltStrength.Verify(new BoltStrengthInput(m.Policy.Code,m.Bolt,0,0,jointLength:rows.Last().Y-rows.First().Y,
                    holeClearanceServiceabilitySource:m.HoleClearanceServiceabilitySource),out var baseValues);
                if(!baseBolt.IsComplete){issues.AddRange(baseBolt.Diagnostics);return;}
                double fv=baseValues.ShearResistance;
                var resistances=new List<double>();
                for(int i=0;i<rows.Count;i++)
                {
                    double end=Math.Min(rows[i].Y-top,bottom-rows[i].Y);
                    double? pitch=rows.Count==1?(double?)null:i==0?rows[1].Y-rows[0].Y:i==rows.Count-1?rows[i].Y-rows[i-1].Y:
                        Math.Min(rows[i].Y-rows[i-1].Y,rows[i+1].Y-rows[i].Y);
                    double capacity=double.MaxValue;
                    foreach(bool endBolt in pitch.HasValue?new[]{true,false}:new[]{true})
                    {
                        var bearing=BoltBearing.Verify(new BoltBearingInput(m.Policy.Code,m.Bolt,p,0,end,e,pitch,g,endBolt,true,m.HoleClearanceServiceabilitySource));
                        if(!bearing.IsComplete){issues.AddRange(bearing.Diagnostics.Select(x=>name+":"+x));continue;}
                        capacity=Math.Min(capacity,bearing.Checks.Single().Resistance);
                    }
                    resistances.Add(capacity);
                }
                // EN3.7: n times the least resistance is the conservative branch even
                // where the sum of the individual bearing resistances would be permitted.
                Check(name+".BoltGroup",m.Shear,2*rows.Count*Math.Min(fv,resistances.Min()),"EN1993-1-8:2005 3.7; conservative minimum-per-fastener group");
                // Both exits, including every hole actually crossed. A single outer strip
                // is eccentrically loaded: use the 0.5 coefficient even for two-column paths.
                for(int direction=0;direction<2;direction++)
                for(int cut=0;cut<rows.Count;cut++)
                {
                    int count=direction==0?cut+1:rows.Count-cut;
                    double length=direction==0?rows[cut].Y-top:bottom-rows[cut].Y;
                    double av=(length-(count-.5)*d0)*t;
                    double atOuter=(e-.5*d0)*t,atInner=(g-d0)*t;
                    var selected=direction==0?rows.Take(cut+1):rows.Skip(cut);
                    double q=selected.Sum(r=>r.LeftShear+r.RightShear);
                    string id=name+"."+(direction==0?"Top":"Bottom")+".Row"+(cut+1);
                    Value(id+".AnvOneSide",av,"mm2");Value(id+".AntOuter",atOuter,"mm2");Value(id+".AntInner",atInner,"mm2");
                    Check(id+".OuterBlock",q/2,.5*p.Fu*atOuter/gm2+PlasticShear.Resistance(p.Fy, av, gm0),"EN1993-1-8:2005 3.10.2(3); single outer strip, in-plane action only");
                    Check(id+".InnerBlock",q,.5*p.Fu*atInner/gm2+PlasticShear.Resistance(2*p.Fy, av, gm0),"EN1993-1-8:2005 3.10.2(3); two-column block, in-plane action only");
                }
            }
        }
    }
}
