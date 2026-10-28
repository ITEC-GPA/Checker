using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Column cut actions extrapolated to the SAME joint reference, in one
    /// continuous column local system: N compression negative; M1 is in the beam plane.
    /// Lower.N-Upper.N=-beam.V2, Lower.M1-Upper.M1=beam.M1, Lower.V2=Upper.V2.
    /// Positive column V2 opposes panel shear. No independently enveloped extrema.
    /// Contact is a real centred rectangular, closed footprint under the compression
    /// flange, on both interfaces; widths/heights are measured, not inferred from labels.</summary>
    public sealed class EndPlateColumnCaseInput
    {
        public EndPlateMomentWithBeamSectionResult Parent { get; }
        public SteelActions Upper { get; }
        public SteelActions Lower { get; }
        public double ContactWidth { get; }
        public double ContactHeight { get; }
        public double ContactGap { get; }
        public double ColumnFlangeFu { get; }
        public string CombinationSource { get; }
        public string ContactExecutionSource { get; }
        public EndPlateColumnCaseInput(EndPlateMomentWithBeamSectionResult parent,SteelActions upper,SteelActions lower,
            double contactWidth,double contactHeight,double contactGap,double columnFlangeFu,string combinationSource,string contactExecutionSource)
        {
            Parent=parent??throw new ArgumentNullException(nameof(parent));Upper=upper??throw new ArgumentNullException(nameof(upper));
            Lower=lower??throw new ArgumentNullException(nameof(lower));
            ConnectionValidation.Positive(contactWidth,nameof(contactWidth));ConnectionValidation.Positive(contactHeight,nameof(contactHeight));
            ConnectionValidation.Nonnegative(contactGap,nameof(contactGap));
            ConnectionValidation.Positive(columnFlangeFu,nameof(columnFlangeFu));ColumnFlangeFu=columnFlangeFu;
            ContactWidth=contactWidth;ContactHeight=contactHeight;ContactGap=contactGap;CombinationSource=combinationSource;ContactExecutionSource=contactExecutionSource;
        }
    }

    public sealed class EndPlateColumnCaseResult
    {
        public EndPlateColumnCaseInput Input { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete=>Diagnostics.Count==0;
        public bool CanQualifyWholeMember=>false;
        public ConnectionStatus Status=>Checks.Any(x=>!x.Passed)||Input.Parent.Status==ConnectionStatus.Failed?ConnectionStatus.Failed:
            IsComplete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
        public double? ContactLimitedMomentResistance { get; }
        internal EndPlateColumnCaseResult(EndPlateColumnCaseInput input,List<ConnectionCheck> checks,List<SectionResistanceValue> trace,
            List<string> issues,double? resistance)
        {Input=input;Checks=Array.AsReadOnly(checks.ToArray());Trace=Array.AsReadOnly(trace.ToArray());Diagnostics=Array.AsReadOnly(issues.ToArray());ContactLimitedMomentResistance=resistance;}
    }

    internal static class EndPlateColumnCase
    {
        public static EndPlateColumnCaseResult Verify(EndPlateColumnCaseInput p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            var parent=p.Parent;var m=parent.Input;var col=m.Column;var code=m.Policy.Code;
            var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();var issues=new List<string>();double? mr=null;
            void Require(bool ok,string issue){if(!ok)issues.Add(issue);}
            void Value(string id,double v,string unit){if(double.IsNaN(v)||double.IsInfinity(v))issues.Add("NumericRange:"+id);else trace.Add(new SectionResistanceValue(id,v,unit));}
            void Check(string id,double d,double r,string reference,string unit="MPa")
            {if(!(r>0)||double.IsNaN(d)||double.IsInfinity(d/r))issues.Add("NumericRange:"+id);else checks.Add(new ConnectionCheck(id,d,r,reference,unit));}
            Require(parent.IsComplete&&parent.MomentComponents?.MomentResistance!=null,"CompleteSameCaseMomentRequired");
            Require(m.PanelConfiguration==ColumnWebPanelConfiguration.SingleSidedJoint&&m.Beta==1,"SingleSidedBetaOneRequired");
            Require(!string.IsNullOrWhiteSpace(p.CombinationSource),"SameCombinationAndReferenceSourceRequired");
            Require(!string.IsNullOrWhiteSpace(p.ContactExecutionSource),"ActualClosedContactEvidenceRequired");
            Require(p.ContactGap==0,"OpenContactOutsideStandardModel");
            Require(p.ColumnFlangeFu>=m.ColumnFlangeFy,"ActualColumnFlangeStrengthRequired");
            Require(p.ContactWidth<=Math.Min(m.Beam.Width,Math.Min(m.EndPlate.Width,col.Width))&&p.ContactHeight<=m.Beam.FlangeThickness,
                "ContactMustFitCentredCompressionFlangeFootprint");
            foreach(var a in new[]{p.Upper,p.Lower})Require(a.V1==0&&a.M2==0&&a.T==0,"ColumnInPlaneActionsOnly");
            var beam=parent.BeamSection.Input.Actions;
            Require(Equal(p.Lower.N-p.Upper.N,-beam.V2),"ColumnAxialJumpDoesNotBalanceBeamShear");
            Require(Equal(p.Lower.M1-p.Upper.M1,beam.M1),"ColumnMomentJumpDoesNotBalanceBeamMoment");
            Require(Equal(p.Lower.V2,p.Upper.V2),"ColumnShearJumpRequiresNonzeroBeamAxial");
            double fy=Math.Min(col.Fy,m.ColumnFlangeFy),eps=Math.Sqrt(235/fy);
            Require((col.Width-col.WebThickness-2*col.RootRadius)/(2*col.FlangeThickness)<=14*eps
                &&col.ClearWeb/col.WebThickness<=42*eps,"ColumnRectangularPartsMustBeClassThreeOrBetterInUniformCompression");
            if(issues.Count==0)
            {
                // Conservative rectangles: omit beneficial root fillets, deduct both holes
                // in the loaded flange at every cut, even compression. Recompute centroid
                // and inertia, never reuse the gross section modulus with a reduced area.
                double h=col.Height,b=col.Width,t=col.FlangeThickness,tw=col.WebThickness,hw=h-2*t,z=(h-t)/2;
                double ag=2*b*t+hw*tw,ig=2*(b*t*t*t/12+b*t*z*z)+tw*hw*hw*hw/12;
                double removed=2*m.Bolt.HoleDiameter*t,an=ag-removed,cy=-removed*z/an;
                double inertia=ig-removed*(z*z+t*t/12)-an*cy*cy,c=h/2+Math.Abs(cy);
                Value("Column.NetArea",an,"mm2");Value("Column.NetCentroidShift",cy,"mm");Value("Column.NetInertia",inertia,"mm4");
                Require(an>0&&inertia>0,"PositiveColumnNetGeometryRequired");
                if(issues.Count==0)
                {
                    double sigmaCompression=0;
                    Cut("Upper",p.Upper);Cut("Lower",p.Lower);
                    Value("Column.RequiredLongitudinalCompressionStress",sigmaCompression,"MPa");
                    Require(m.ColumnLongitudinalCompressionStress+1e-9>=sigmaCompression,"ComponentColumnStressBelowActualNetSectionBound");
                    double moment=parent.MomentComponents.MomentResistance.Value;
                    double total=parent.MomentComponents.EffectiveRows.Sum(r=>r.Resistance);
                    double force=total*m.Moment/moment,vc=(p.Upper.V2+p.Lower.V2)/2;
                    double panel=Math.Abs(force-vc);
                    Value("Panel.RowForceAtDemand",force,"N");Value("Panel.ColumnShear",vc,"N");Value("Panel.EquilibriumShear",panel,"N");
                    Require(panel<=force+1e-8*Math.Max(1,force),"ActualPanelShearExceedsBetaOneEnvelope");
                    double capacity=p.ContactWidth*p.ContactHeight*Math.Min(m.Beam.Fy,Math.Min(m.EndPlate.Fy,m.ColumnFlangeFy))/code.Plates.GammaM0;
                    Check("CompressionContact",force,capacity,"Centred closed contact on both interfaces; conservative uniform yield-pressure bound","N");
                    double limit=moment*Math.Min(1,capacity/total);
                    Value("ContactLimitedMomentResistance",limit,"Nmm");
                    if(issues.Count==0)mr=limit;
                    void Cut(string name,SteelActions a)
                    {
                        // Cut actions refer to the gross column centroid. The asymmetric
                        // holes move the net centroid: transfer N with its eccentricity,
                        // retaining the adverse sign as a conservative moment envelope.
                        double netMomentBound=Math.Abs(a.M1)+Math.Abs(a.N*cy);
                        double stress=Math.Abs(a.N)/an+netMomentBound*c/inertia;
                        double compression=Math.Max(0,-a.N/an+netMomentBound*c/inertia);
                        Value(name+".NetMomentBound",netMomentBound,"Nmm");
                        double shear=1.5*Math.Abs(a.V2)/(tw*col.ClearWeb);
                        sigmaCompression=Math.Max(sigmaCompression,compression);
                        Value(name+".NormalStressBound",stress,"MPa");Value(name+".ShearStressBound",shear,"MPa");
                        Check(name+".ElasticNetYield",stress+Math.Sqrt(3)*shear,fy/code.Plates.GammaM0,
                            "EN1993-1-1 6.2.1 elastic check; |sigma|+sqrt(3)|tau| bounds von Mises; net rectangles");
                        Check(name+".NetFractureBound",stress,.9*Math.Min(col.Fu,p.ColumnFlangeFu)/code.Plates.GammaM2,
                            "Supplementary elastic normal-stress bound on net rectangles; all signs conservatively retained");
                    }
                }
            }
            return new EndPlateColumnCaseResult(p,checks,trace,issues,mr);
        }
        private static bool Equal(double a,double b)=>Math.Abs(a-b)<=1e-8*Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)));
    }
}
