using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Standard one-bolt longitudinal lap through one solid rectangular packing.
    /// Identical hot-finished C/L backs; ordinary round holes, filled by a through bolt,
    /// hardened washers both sides. No externally applied opening force, transverse
    /// interface force or couple. The inherent lap eccentricity is NOT zeroed: both
    /// connected walls and the packing carry the full Q*e elastic moment bound,
    /// with e including gap, wall thickness and chord-centroid transfer eccentricity.
    /// Actual dimensions/materials are required, not inferred from catalogue names.</summary>
    public sealed class SteelBoltedPackingDetail
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionBolt Bolt { get; }
        public double Width { get; }
        public double Length { get; }
        public double Thickness { get; }
        public double TransverseCoordinate { get; }
        public double Fy { get; }
        public double Fu { get; }
        public double HardwareDiameter { get; }
        public bool HardenedWashers { get; }
        public string ExecutionSource { get; }
        public string HoleClearanceSource { get; }
        public SteelBoltedPackingDetail(ConnectionDesignCode code,ConnectionBolt bolt,double width,double length,
            double thickness,double transverseCoordinate,double fy,double fu,double hardwareDiameter,bool hardenedWashers,
            string executionSource,string holeClearanceSource)
        {
            Code=code??throw new ArgumentNullException(nameof(code));Bolt=bolt??throw new ArgumentNullException(nameof(bolt));
            foreach(double v in new[]{width,length,thickness,fy,fu,hardwareDiameter})ConnectionValidation.Positive(v,"packing detail");
            ConnectionValidation.Nonnegative(transverseCoordinate,nameof(transverseCoordinate));
            if(fu<fy||hardwareDiameter<=bolt.HoleDiameter)throw new ArgumentException("Actual material and covering head/washer dimensions required.");
            Width=width;Length=length;Thickness=thickness;TransverseCoordinate=transverseCoordinate;Fy=fy;Fu=fu;
            HardwareDiameter=hardwareDiameter;HardenedWashers=hardenedWashers;ExecutionSource=executionSource;HoleClearanceSource=holeClearanceSource;
        }
    }

    public sealed class SteelBoltedPackingResult
    {
        public PackedPairImperfectionModel Demand { get; }
        public SteelBoltedPackingDetail Detail { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete=>Diagnostics.Count==0;
        public ConnectionStatus Status=>!IsComplete?ConnectionStatus.NotSupported:Checks.All(c=>c.Passed)?ConnectionStatus.Passed:ConnectionStatus.Failed;
        public double? Utilization=>IsComplete?Checks.Max(c=>c.Utilization):(double?)null;
        public bool CanQualifyWholeMember=>false;
        public double MaximumConnectionDemand { get; }
        public double ConnectionResistance { get; }
        internal SteelBoltedPackingResult(PackedPairImperfectionModel demand,SteelBoltedPackingDetail detail,
            List<ConnectionCheck> checks,List<SectionResistanceValue> trace,List<string> issues,double force,double resistance)
        {Demand=demand;Detail=detail;Checks=Array.AsReadOnly(checks.ToArray());Trace=Array.AsReadOnly(trace.ToArray());
            Diagnostics=Array.AsReadOnly(issues.ToArray());MaximumConnectionDemand=force;ConnectionResistance=resistance;}
    }

    public static class SteelBoltedPacking
    {
        public static SteelBoltedPackingResult Verify(PackedPairImperfectionModel demand,SteelBoltedPackingDetail detail)
        {
            if(demand==null)throw new ArgumentNullException(nameof(demand));if(detail==null)throw new ArgumentNullException(nameof(detail));
            var layout=demand.Layout;var input=layout.Input;var shape=(SectionBuiltUp)input.Section.CreateSectionCopy().SectionShape;
            var property=input.Section.CreateSectionCopy();var chord=shape.Parts[0].Section;var g=ModelBuiltUpGeometry.Create(shape);
            var code=detail.Code;var bolt=detail.Bolt;double d0=bolt.HoleDiameter;
            var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>(demand.Trace);var issues=new List<string>();
            void Require(bool condition,string message){if(!condition)issues.Add(message);}
            void Value(string name,double value,string unit){if(double.IsNaN(value)||double.IsInfinity(value))issues.Add("NumericRange:"+name);else trace.Add(new SectionResistanceValue(name,value,unit));}
            void Check(string id,double force,double capacity,string reference,string unit="N")
            {if(!(capacity>0)||double.IsInfinity(capacity)||double.IsNaN(force)||double.IsInfinity(force)||double.IsInfinity(force/capacity))issues.Add("NumericRange:"+id);else checks.Add(new ConnectionCheck(id,force,capacity,reference,unit));}
            Require(code.Plates.Standard==input.Code.SectionCode.Standard&&code.Plates.GammaM0==input.Code.SectionCode.GammaM0
                &&code.Plates.GammaM2==input.Code.SectionCode.GammaM2&&code.Plates.ParameterSet==input.Code.SectionCode.ParameterSet,"DesignContextMismatch");
            Require(input.Section.Unperforated==false,"DeclareOrdinaryFilledBoltHoles");
            Require(property.FormedType==Section.FormedTypes.HotFinished,"HotFinishedChordsRequired");
            Require(input.Section.YieldStrength<=460&&detail.Fy<=460,"CarbonSteelUpToS460");
            Require(detail.HardenedWashers,"HardenedWashersBothSidesRequired");
            Require(!string.IsNullOrWhiteSpace(detail.ExecutionSource),"ExecutionSourceRequired");
            Require(layout.Stations.Count>=3,"AtLeastOneIntermediatePackingRequired");
            bool vertical=shape.Parts[0].MirrorX!=shape.Parts[1].MirrorX;
            var c=chord as SectionC;var l=chord as SectionL;
            double t=!(c is null)?c.ThicknessWeb:vertical?l.VerticalLegThickness:l.HorizontalLegThickness;
            Require(input.Section.NetArea.HasValue&&Math.Abs(input.Section.NetArea.Value-2*(chord.Area-d0*t))<=1e-8*chord.Area,
                "NetAreaMustMatchOnlyTheDeclaredSingleLineOfFilledHoles");
            double low=!(c is null)?c.ThicknessBottom+c.R1:vertical?l.HorizontalLegThickness+l.R:l.VerticalLegThickness+l.R;
            double high=!(c is null)?c.Height-c.ThicknessTop-c.R1:vertical?l.VerticalLegLength-l.R2:l.HorizontalLegLength-l.R2;
            double centre=detail.TransverseCoordinate;
            double gap=Math.Abs(vertical?shape.Parts[0].X-shape.Parts[1].X:shape.Parts[0].Y-shape.Parts[1].Y);
            double minimumThickness=!(c is null)?Math.Min(t,Math.Min(c.ThicknessTop,c.ThicknessBottom)):Math.Min(l.HorizontalLegThickness,l.VerticalLegThickness);
            Require(g.CentroidSpacing>gap,"BacksMustFacePacking");
            Require(c is null||vertical&&c.LengthTop==c.LengthBottom&&c.ThicknessTop==c.ThicknessBottom,"EqualFlangeBackToBackChannelsRequired");
            Require(Math.Abs(gap-detail.Thickness)<=1e-8*Math.Max(1,gap)&&gap<=3*minimumThickness,"PackingMustFillCloseGap");
            Require(layout.MaximumPitch<=15*g.MinimumRadius,"Table6_9MaximumPitch");
            Require(centre-detail.Width/2>=low&&centre+detail.Width/2<=high,"PackingMustFitStraightInnerBack");
            Require(detail.HardwareDiameter<=Math.Min(detail.Width,detail.Length),"HardwareMustFitPacking");
            Require(detail.Width>=2.4*d0&&detail.Length>=2.4*d0,"PackingMinimumBoltDistances");
            Require(detail.Width/2<=4*Math.Min(t,gap)+40&&detail.Length/2<=4*Math.Min(t,gap)+40,"PackingMaximumEdgeDistances");
            Require(layout.PanelLengths.All(v=>v>=detail.Length),"PackingFootprintsMustNotOverlap");
            Require(detail.Width<=14*Math.Sqrt(235/detail.Fy)*gap,"PackingStripMustBeStocky");
            foreach(var station in demand.Stations.Skip(1).Take(demand.Stations.Count-2))
                Require(station.Station>=detail.Length/2&&station.Station+detail.Length/2<=input.Member.Length,"PackingFootprintOutsideMember");
            double q=demand.Stations.Skip(1).Take(Math.Max(0,demand.Stations.Count-2)).Select(s=>s.LongitudinalShear).DefaultIfEmpty(0).Max();
            Value("Packing.MaxIntermediateShear",q,"N");Value("Packing.ClearGap",gap,"mm");Value("Packing.ConnectedWallThickness",t,"mm");
            // Conservative concurrent upper bound: both independent bow directions and
            // all extreme coordinates, even though Table5.1 permits one direction at a time.
            double width=!(c is null)?Math.Max(c.LengthTop,c.LengthBottom):l.HorizontalLegLength;
            double height=!(c is null)?c.Height:l.VerticalLegLength;
            double rx=Math.Max(chord.Centroid.X,width-chord.Centroid.X),ry=Math.Max(chord.Centroid.Y,height-chord.Centroid.Y);
            double sigma=demand.ChordCompression/chord.Area
                +demand.ChordAssemblyMoment*(vertical?rx:ry)/g.InPlaneInertia
                +demand.ChordOtherMoment*(vertical?ry:rx)/(vertical?chord.Jxx:chord.Jyy);
            Value("Packing.MemberNormalStressBound",sigma,"MPa");
            try
            {
                var gross=new SteelVerificationInput(new SteelSectionInput(property,true),input.Actions,input.Code,input.Member);
                var local=SteelPackedChordResistance.UniformFork(gross,layout.MaximumPitch,demand.ChordCompression,
                    "Axial local critical values used only for the EN1993-1-1 5.2.1 alpha_cr>=10 gate; physical packed layout under6.4.4");
                double ncr=local.Analysis.Values.Min(v=>v.Value);
                Value("Packing.LocalChordCriticalMinimum",ncr,"N");
                Value("Packing.LocalChordCriticalRatio",ncr/demand.ChordCompression,"1");
                Require(ncr>=10*demand.ChordCompression,"LocalChordSecondOrderOutsideLinearDetailScope");
            }
            catch(NotSupportedException ex){issues.Add("LocalChord:"+ex.Message);}
            double resistance=double.MaxValue;
            if(issues.Count==0)
            {
                var strength=BoltStrength.Verify(new BoltStrengthInput(code,bolt,q,0,1,0,gap,detail.HoleClearanceSource));
                if(!strength.IsComplete)issues.AddRange(strength.Diagnostics);
                else {checks.AddRange(strength.Checks);resistance=Math.Min(resistance,strength.Checks.Single(v=>v.Id=="Bolt.Shear").Resistance);}
                foreach(var v in strength.Trace)trace.Add(new SectionResistanceValue("Packing."+v.Name,v.Value,v.Unit));
                // A deliberately isolated central strip no wider than the actual packing:
                // edges at the strip boundary and both force signs. Material beyond it is
                // omitted in the connection resistance, not counted as an unproved spread.
                void Ply(string name,double thickness,double fy,double fu,double longitudinalStress)
                {
                    var plate=new ConnectionPlate(detail.Width,thickness,fy,fu);
                    var bearing=BoltBearing.Verify(new BoltBearingInput(code,bolt,plate,q,detail.Length/2,detail.Width/2,null,null,true,true,
                        detail.HoleClearanceSource,true,detail.HardenedWashers));
                    if(!bearing.IsComplete)issues.AddRange(bearing.Diagnostics.Select(v=>name+":"+v));
                    else {var r=bearing.Checks.Single();Check(name+".Bearing",q,r.Resistance,r.Reference);resistance=Math.Min(resistance,r.Resistance);}
                    double ag=detail.Width*thickness,an=(detail.Width-d0)*thickness;
                    double ant=(detail.Width/2-d0/2)*thickness,anv=(detail.Length/2-d0/2)*thickness;
                    double net=SectionResistanceArithmetic.NetTension(an,fu,code.Plates.GammaM2);
                    // One-sided paths retain half of the load and use the eccentric block rule.
                    double block=.5*fu*ant/code.Plates.GammaM2+fy*anv/(Math.Sqrt(3)*code.Plates.GammaM0);
                    Check(name+".NetRupture",q,net,"EN1993-1-1 6.2.3");
                    Check(name+".BlockEachSide",q/2,block,"EN1993-1-8 3.10.2(3), both symmetric isolated-strip paths");
                    resistance=Math.Min(resistance,Math.Min(net,2*block));
                    double hx=vertical?t/2:centre,hy=vertical?centre:t/2;
                    double transferEccentricity=Math.Sqrt(Math.Pow(hx-chord.Centroid.X,2)+Math.Pow(hy-chord.Centroid.Y,2));
                    double lever=gap+t+2*transferEccentricity;
                    double modulus=(detail.Width-d0)*thickness*thickness/6;
                    // Full eccentric couple in EACH ply, plus membrane and shear absolute
                    // bounds. This supplements the single-lap bearing/packing bolt rules.
                    double netLongitudinalStress=longitudinalStress*ag/an;
                    double stress=netLongitudinalStress+q/an+q*lever/modulus+Math.Sqrt(3)*q/an;
                    Check(name+".ElasticWallAndLap",stress,fy/code.Plates.GammaM0,"EN1993-1-1 6.2.1(5); absolute elastic stress bound, inherent lap moment retained","MPa");
                    Value(name+".NetArea",an,"mm2");Value(name+".LapMoment",q*lever,"Nmm");
                    Value(name+".NetElasticModulus",modulus,"mm3");
                    double localLimit=Math.Max(0,fy/code.Plates.GammaM0-netLongitudinalStress);
                    if(localLimit>0)resistance=Math.Min(resistance,localLimit/(1/an+lever/modulus+Math.Sqrt(3)/an));
                    else resistance=Math.Min(resistance,fy/code.Plates.GammaM0/(1/an+lever/modulus+Math.Sqrt(3)/an));
                    Check(name+".GrossYield",q,ag*fy/code.Plates.GammaM0,"EN1993-1-1 6.2.1");
                }
                Ply("Chord0.Wall",t,input.Section.YieldStrength,input.Section.UltimateStrength,sigma);
                Ply("Chord1.Wall",t,input.Section.YieldStrength,input.Section.UltimateStrength,sigma);
                Ply("Packing",gap,detail.Fy,detail.Fu,0);
            }
            if(resistance==double.MaxValue)resistance=0;
            Value("Packing.CompleteLongitudinalResistance",resistance,"N");
            foreach(var s in demand.Stations)Value("Packing.Station"+s.Index+".LongitudinalShear",s.LongitudinalShear,"N");
            return new SteelBoltedPackingResult(demand,detail,checks,trace,issues,q,resistance);
        }
    }
}
