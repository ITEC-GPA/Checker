using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Two actual complete external fillet loops on the opposite faces of a
    /// solid plate filling the gap between identical C/L backs. Width is transverse,
    /// Length longitudinal, Centre is measured in the original chord connected leg.
    /// Weld footprints are explicitly unperforated. Ordinary longitudinal transfer
    /// only; the inherent eccentricities to each chord centroid are calculated.</summary>
    public sealed class PackedPairWeldedLapDetail
    {
        public ConnectionDesignCode Code { get; }
        public double Width { get; } public double Length { get; } public double Thickness { get; }
        public double Centre { get; } public double Throat { get; }
        public double Fy { get; } public double Fu { get; }
        public FilletWeldSteelGrade PlateGrade { get; } public FilletWeldSteelGrade ChordGrade { get; }
        public bool UnperforatedFootprints { get; }
        public string ExecutionSource { get; }
        public PackedPairWeldedLapDetail(ConnectionDesignCode code,double width,double length,double thickness,double centre,double throat,
            double fy,double fu,FilletWeldSteelGrade plateGrade,FilletWeldSteelGrade chordGrade,bool unperforatedFootprints,string executionSource)
        {
            Code=code??throw new ArgumentNullException(nameof(code));
            foreach(double v in new[]{width,length,thickness,throat,fy,fu})ConnectionValidation.Positive(v,"lap detail");
            ConnectionValidation.Nonnegative(centre,nameof(centre));if(fu<fy)throw new ArgumentException("fu below fy");
            if(!Enum.IsDefined(typeof(FilletWeldSteelGrade),plateGrade)||!Enum.IsDefined(typeof(FilletWeldSteelGrade),chordGrade))throw new ArgumentOutOfRangeException(nameof(plateGrade));
            Width=width;Length=length;Thickness=thickness;Centre=centre;Throat=throat;Fy=fy;Fu=fu;PlateGrade=plateGrade;ChordGrade=chordGrade;
            UnperforatedFootprints=unperforatedFootprints;ExecutionSource=executionSource;
        }
    }

    internal sealed class PackedPairWeldedLapResult
    {
        internal readonly List<ConnectionCheck> Checks=new List<ConnectionCheck>();
        internal readonly List<SectionResistanceValue> Trace=new List<SectionResistanceValue>();
        internal readonly List<string> Issues=new List<string>();
        internal double ForceResistance=double.MaxValue;
        internal double CentroidSpacing,TransverseOffset,NormalOffset,ChordThickness;
        internal void Require(bool condition,string message){if(!condition)Issues.Add(message);}
        internal void Value(string id,double v,string unit){if(double.IsNaN(v)||double.IsInfinity(v))Issues.Add("NumericRange:"+id);else Trace.Add(new SectionResistanceValue(id,v,unit));}
        internal void Check(string id,double d,double r,string reference,string unit="N")
        {if(!(r>0)||double.IsNaN(d)||double.IsInfinity(r)||double.IsInfinity(d/r))Issues.Add("NumericRange:"+id);else Checks.Add(new ConnectionCheck(id,d,r,reference,unit));}
    }

    internal static class PackedPairWeldedLap
    {
        internal static PackedPairWeldedLapResult Verify(PackedPairImperfectionModel demand,PackedPairWeldedLapDetail d,double force)
        {
            var r=new PackedPairWeldedLapResult();var input=demand.Layout.Input;var property=input.Section.CreateSectionCopy();
            var pair=(SectionBuiltUp)property.SectionShape;var chord=pair.Parts[0].Section;var geometry=ModelBuiltUpGeometry.Create(pair);
            var c=chord as SectionC;var l=chord as SectionL;bool vertical=pair.Parts[0].MirrorX!=pair.Parts[1].MirrorX;
            double t=!(c is null)?c.ThicknessWeb:vertical?l.VerticalLegThickness:l.HorizontalLegThickness;
            double gap=Math.Abs(vertical?pair.Parts[0].X-pair.Parts[1].X:pair.Parts[0].Y-pair.Parts[1].Y);
            double minT=!(c is null)?Math.Min(t,Math.Min(c.ThicknessTop,c.ThicknessBottom)):Math.Min(l.VerticalLegThickness,l.HorizontalLegThickness);
            double leg=Math.Sqrt(2)*d.Throat;
            r.CentroidSpacing=geometry.CentroidSpacing;r.ChordThickness=t;
            r.NormalOffset=Math.Abs((vertical?chord.Centroid.X:chord.Centroid.Y)-t/2)+t/2;
            r.TransverseOffset=Math.Abs((vertical?chord.Centroid.Y:chord.Centroid.X)-d.Centre);
            r.Require(property.FormedType==Section.FormedTypes.HotFinished,"HotFinishedChordsRequired");
            r.Require(d.Code.Plates.Standard==input.Code.SectionCode.Standard&&d.Code.Plates.GammaM0==input.Code.SectionCode.GammaM0
                &&d.Code.Plates.GammaM2==input.Code.SectionCode.GammaM2&&d.Code.Plates.ParameterSet==input.Code.SectionCode.ParameterSet,"DesignContextMismatch");
            r.Require(c is null||vertical&&c.LengthTop==c.LengthBottom&&c.ThicknessTop==c.ThicknessBottom,"EqualFlangeBackToBackChannelsRequired");
            r.Require(Math.Abs(gap-d.Thickness)<=1e-8*Math.Max(1,gap)&&gap<=3*minT,"PlateMustFillCloseGap");
            r.Require(geometry.CentroidSpacing>gap,"BacksMustFaceCentralPlate");
            r.Require(d.Fy<=(double)d.PlateGrade&&input.Section.YieldStrength<=(double)d.ChordGrade,"ActualProductStrengthMustFitGrade");
            r.Require(d.UnperforatedFootprints,"UnperforatedWeldFootprintsRequired");
            r.Require(!string.IsNullOrWhiteSpace(d.ExecutionSource),"FullSizeContinuityConsumablesAndRootExecutionRequired");
            r.Require(d.Throat>=3&&leg<=Math.Min(t,gap)&&2*leg<=gap,"ActualWeldsMustFitBothFacesWithoutOverlap");
            r.Require(Math.Min(d.Width,d.Length)>=Math.Max(30,6*d.Throat),"MinimumEffectiveWeldLengths");
            r.Require(PackingWeldedStationComponents.OnOneStraightBack(chord,vertical,d.Centre,d.Width/2+leg),"WeldLoopMustFitExactStraightBack");
            r.Require(d.Width<=14*Math.Sqrt(235/d.Fy)*gap,"CentralPlateStripMustBeStocky");
            r.Require(demand.Layout.MaximumPitch<=15*geometry.MinimumRadius,"Table6_9MaximumPitch");
            double w=!(c is null)?Math.Max(c.LengthTop,c.LengthBottom):l.HorizontalLegLength;
            double h=!(c is null)?c.Height:l.VerticalLegLength;
            double rx=Math.Max(chord.Centroid.X,w-chord.Centroid.X),ry=Math.Max(chord.Centroid.Y,h-chord.Centroid.Y);
            double area=input.Section.NetArea.HasValue?Math.Min(chord.Area,input.Section.NetArea.Value/2):chord.Area;
            r.Require(area>0,"PositiveChordNetAreaRequired");
            double sigma=demand.ChordCompression/area+demand.ChordAssemblyMoment*(vertical?rx:ry)/geometry.InPlaneInertia
                +demand.ChordOtherMoment*(vertical?ry:rx)/(vertical?chord.Jxx:chord.Jyy);
            r.Value("Lap.MemberNormalStressBound",sigma,"MPa");r.Value("Lap.ForcePerFace",force,"N");
            r.Value("Lap.NormalCentroidOffset",r.NormalOffset,"mm");r.Value("Lap.TransverseCentroidOffset",r.TransverseOffset,"mm");
            try
            {
                var gross=new SteelVerificationInput(new SteelSectionInput(property,true),input.Actions,input.Code,input.Member);
                var local=SteelPackedChordResistance.UniformFork(gross,demand.Layout.MaximumPitch,demand.ChordCompression,"Explicit short chord panel in welded packed-pair detail");
                double critical=local.Analysis.Values.Min(v=>v.Value);r.Value("Lap.LocalChordCriticalMinimum",critical,"N");
                r.Require(critical>=10*demand.ChordCompression,"LocalChordSecondOrderOutsideLinearDetailScope");
            }
            catch(NotSupportedException ex){r.Issues.Add("Chord:"+ex.Message);}
            if(r.Issues.Count!=0)return r;
            // Elastic lower-bound strip: no beneficial stress spread, plastic redistribution,
            // contact, or cancellation of the two opposite chord eccentric couples.
            double lever=gap+t+2*Math.Sqrt(r.NormalOffset*r.NormalOffset+r.TransverseOffset*r.TransverseOffset);
            Ply("Chord0",t,input.Section.YieldStrength,input.Section.UltimateStrength,sigma);
            Ply("Chord1",t,input.Section.YieldStrength,input.Section.UltimateStrength,sigma);
            Ply("CentralPlate",gap,d.Fy,d.Fu,0);
            var segments=PackingWeldedStationComponents.Loop(d.Length,d.Width,new PackingFaceWeldDetail(0,d.Throat,FilletWeldEndCondition.FullSize,d.ExecutionSource)).ToArray();
            // Independent signs envelope either face and either longitudinal transfer sign.
            // Each loop takes its FULL face force and eccentric couple, not half of them.
            int scenario=0;
            foreach(int sign in new[]{-1,1})foreach(int normal in new[]{-1,1})foreach(int transverse in new[]{-1,1})
            {
                var weld=FilletWeldGroup.Verify(new FilletWeldGroupInput(d.Code,segments,
                    new FilletWeldMaterial(d.PlateGrade,d.Fu,gap),new FilletWeldMaterial(d.ChordGrade,input.Section.UltimateStrength,t),
                    new PlanarConnectionActions(sign*force,0,0,0,normal*force*lever,transverse*force*r.TransverseOffset),
                    FilletWeldMethod.Directional,d.ExecutionSource,true,"Full eccentric moment bound at each physical face",d.Length));
                if(!weld.IsComplete)r.Issues.AddRange(weld.Diagnostics);
                foreach(var point in weld.Points)
                {
                    foreach(var check in point.Verification.Checks)
                        r.Check("Weld.Envelope"+scenario+".Segment"+point.SegmentIndex+"."+point.AtStart+"."+check.Id,check.Demand,check.Resistance,check.Reference,check.Unit);
                    double lineBound=Math.Abs(point.ForcePerLengthX)+Math.Abs(point.ForcePerLengthY)+Math.Sqrt(3)*Math.Abs(point.ForcePerLengthZ);
                    // Local membrane/shear flux entering each parent at the actual weld line,
                    // including the normal peel traction generated by the eccentric couple.
                    foreach(bool wall in new[]{true,false})
                    {
                        double thickness=wall?t:gap,stress0=wall?sigma:0,fy=wall?input.Section.YieldStrength:d.Fy;
                        string id=(wall?"WallLine":"PlateLine")+scenario+"."+point.SegmentIndex+"."+point.AtStart;
                        r.Check(id,stress0+lineBound/thickness,fy/d.Code.Plates.GammaM0,"Supplementary elastic parent-metal line-traction bound, including peel","MPa");
                        double available=fy/d.Code.Plates.GammaM0-stress0;
                        if(lineBound>0)r.ForceResistance=Math.Min(r.ForceResistance,force*(available>0?available:fy/d.Code.Plates.GammaM0)*thickness/lineBound);
                    }
                }
                if(force>0&&weld.Utilization>0)r.ForceResistance=Math.Min(r.ForceResistance,force/weld.Utilization.Value);
                scenario++;
            }
            return r;
            void Ply(string name,double thickness,double fy,double fu,double baseStress)
            {
                double a=d.Width*thickness,modulus=d.Width*thickness*thickness/6;
                double coefficient=(1+Math.Sqrt(3))/a+lever/modulus;
                r.Check(name+".ElasticWallAndLap",baseStress+force*coefficient,fy/d.Code.Plates.GammaM0,
                    "EN1993-1-1 6.2.1; nominal absolute elastic strip stress including inherent lap eccentricity","MPa");
                r.Check(name+".GrossRuptureBound",force,.9*a*fu/d.Code.Plates.GammaM2,"Supplementary gross strip rupture bound");
                double available=fy/d.Code.Plates.GammaM0-baseStress;
                r.ForceResistance=Math.Min(r.ForceResistance,(available>0?available:fy/d.Code.Plates.GammaM0)/coefficient);
                r.ForceResistance=Math.Min(r.ForceResistance,.9*a*fu/d.Code.Plates.GammaM2);
                r.Value(name+".Area",a,"mm2");r.Value(name+".ElasticModulus",modulus,"mm3");r.Value(name+".LapMoment",force*lever,"Nmm");
            }
        }
    }
}
