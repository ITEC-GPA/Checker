using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Central welded gusset continuing into a COPLANAR collector plate via
    /// a full-penetration butt weld over the full gusset width, aligned mid-surfaces.
    /// Both end nodes have this same detail. Root is physically restrained against
    /// out-of-plane rotation; its evidence is required. FreeLength is from the root
    /// to the nearest lap edge. ChordOverhang provides the real weld footprint beyond
    /// the member's terminal reference station. No T attachment to a column flange.</summary>
    public sealed class SteelPackedPairTerminalDetail
    {
        public PackedPairWeldedLapDetail Lap { get; }
        public ConnectionPlate Collector { get; }
        public double FreeLength { get; } public double ChordOverhang { get; }
        public double WeldFusionDepth { get; } public double WeldSeamWidth { get; }
        public double WeldFy { get; } public double WeldFu { get; }
        public bool FullPenetration { get; } public bool RootRotationRestrained { get; }
        public string SupportAndWeldSource { get; }
        public SteelPackedPairTerminalDetail(PackedPairWeldedLapDetail lap,ConnectionPlate collector,double freeLength,double chordOverhang,
            double weldFusionDepth,double weldSeamWidth,double weldFy,double weldFu,bool fullPenetration,bool rootRotationRestrained,string supportAndWeldSource)
        {
            Lap=lap??throw new ArgumentNullException(nameof(lap));Collector=collector??throw new ArgumentNullException(nameof(collector));
            foreach(double v in new[]{freeLength,chordOverhang,weldFusionDepth,weldSeamWidth,weldFy,weldFu})ConnectionValidation.Positive(v,"terminal detail");
            if(weldFu<weldFy)throw new ArgumentException("Weld fu below fy");
            FreeLength=freeLength;ChordOverhang=chordOverhang;WeldFusionDepth=weldFusionDepth;WeldSeamWidth=weldSeamWidth;
            WeldFy=weldFy;WeldFu=weldFu;FullPenetration=fullPenetration;RootRotationRestrained=rootRotationRestrained;SupportAndWeldSource=supportAndWeldSource;
        }
    }

    public sealed class SteelPackedPairTerminalResult
    {
        public PackedPairImperfectionModel Demand { get; }
        public SteelPackedPairTerminalDetail Detail { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete=>Diagnostics.Count==0;
        public ConnectionStatus Status=>Checks.Any(x=>!x.Passed)?ConnectionStatus.Failed:IsComplete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
        public bool CanQualifyWholeTerminal=>IsComplete;
        public bool CanQualifyWholeMember=>false;
        public double ForcePerChord { get; }
        /// <summary>Conditional local force resistance for this fixed end-force pattern
        /// and concomitant member stress; not a global critical load of the member.</summary>
        public double? ForceResistance { get; }
        public double? Utilization=>IsComplete?Checks.Max(x=>x.Utilization):(double?)null;
        internal SteelPackedPairTerminalResult(PackedPairImperfectionModel demand,SteelPackedPairTerminalDetail detail,PackedPairWeldedLapResult r,double force)
        {Demand=demand;Detail=detail;Checks=Array.AsReadOnly(r.Checks.ToArray());Trace=Array.AsReadOnly(r.Trace.ToArray());Diagnostics=Array.AsReadOnly(r.Issues.ToArray());
            ForcePerChord=force;ForceResistance=IsComplete&&r.ForceResistance>0&&r.ForceResistance<double.MaxValue?r.ForceResistance:(double?)null;}
    }

    public static class SteelPackedPairTerminal
    {
        public static SteelPackedPairTerminalResult Verify(PackedPairImperfectionModel demand,SteelPackedPairTerminalDetail d)
        {
            if(demand==null)throw new ArgumentNullException(nameof(demand));if(d==null)throw new ArgumentNullException(nameof(d));
            double transfer=Math.Max(demand.Stations.First().LongitudinalShear,demand.Stations.Last().LongitudinalShear);
            double force=-demand.Layout.Input.Actions.N/2+transfer;
            var lap=d.Lap;var r=PackedPairWeldedLap.Verify(demand,lap,force);double leg=Math.Sqrt(2)*lap.Throat;
            r.Require(r.TransverseOffset<=1e-8*Math.Max(1,lap.Width),"TerminalGussetMustBeCentredOnTransverseChordCentroid");
            r.Require(d.ChordOverhang>=lap.Length/2+leg,"ActualChordOverhangMustContainTerminalWelds");
            r.Require(demand.Layout.PanelLengths.First()>=lap.Length+2*leg&&demand.Layout.PanelLengths.Last()>=lap.Length+2*leg,"TerminalWeldFootprintMustNotOverlapNextStation");
            r.Require(d.Collector.Width>=lap.Width&&d.Collector.Thickness>=lap.Thickness,"CoplanarCollectorMustContainFullGussetSection");
            r.Require(d.FullPenetration&&d.WeldFusionDepth>=lap.Thickness&&d.WeldSeamWidth>=lap.Width,"FullPenetrationAcrossEntireGussetRequired");
            r.Require(d.WeldFy>=Math.Max(lap.Fy,d.Collector.Fy)&&d.WeldFu>=Math.Max(lap.Fu,d.Collector.Fu),"CompatibleFullStrengthButtWeldRequired");
            r.Require(d.RootRotationRestrained&&!string.IsNullOrWhiteSpace(d.SupportAndWeldSource),"ActualRootRestraintMidSurfaceAlignmentAndInspectionRequired");
            r.Value("Terminal.EndTransferEnvelope",transfer,"N");r.Value("Terminal.ForcePerChord",force,"N");
            if(r.Issues.Count==0)
            {
                // Conservative isolated cantilever strip: no Whitmore spread or assistance
                // from collector width beyond the gusset, and effective length 2L.
                double length=2*(d.FreeLength+lap.Length/2),a=lap.Width*lap.Thickness;
                double inertia=Math.Min(lap.Width*Math.Pow(lap.Thickness,3),lap.Thickness*Math.Pow(lap.Width,3))/12;
                double modulus=Math.Min(lap.Width*lap.Thickness*lap.Thickness,lap.Thickness*lap.Width*lap.Width)/6;
                double ncr=Math.PI*Math.PI*demand.Layout.Input.Section.ElasticModulus*inertia/(length*length),total=2*force;
                // Differential chord transfer may not be cancelled at the collector.
                // Equal centroidal N parts cancel their opposed eccentric couples; the
                // end transfer is enveloped in either direction with both centroid arms.
                // Free body as in SteelPackedPairBoltedTerminal (EN 1993-1-1:2005 6.4.4(1)-(2)): chord forces
                // N/2+Q and N/2-Q on the centroid lines z=+-s/2, s=CentroidSpacing=gap+2 NormalOffset, give
                // exactly Q s at the gusset root. This envelope, 2Q(s+2e_n)=(2+4e_n/s)Q s, is at least twice
                // that statics: kept unchanged as a conservative bound (30/10/2026 review).
                double moment=2*transfer*(r.CentroidSpacing+2*r.NormalOffset);
                double fy=Math.Min(lap.Fy,d.Collector.Fy),fu=Math.Min(lap.Fu,d.Collector.Fu);
                double limit=Math.Min(fy/lap.Code.Plates.GammaM0,.9*fu/lap.Code.Plates.GammaM2);
                r.Value("Terminal.GussetEffectiveLength",length,"mm");r.Value("Terminal.GussetNcr",ncr,"N");r.Value("Terminal.CollectorMomentEnvelope",moment,"Nmm");
                r.Value("Terminal.GussetArea",a,"mm2");r.Value("Terminal.GussetModulus",modulus,"mm3");
                double Stress(double scale)=>total*scale/a+(moment+total*length/150)*scale/(modulus*(1-total*scale/ncr));
                if(total>=ncr)r.Check("Terminal.GussetEuler",total,ncr,"Explicit fixed-root/free-tip isolated strip Euler limit");
                else r.Check("Terminal.GussetAndCollectorElastic",Stress(1),limit,"Conservative elastic beam-column with Lk/150 bow, net-free solid strip and CJP continuity","MPa");
                // Solve the conditional local resistance monotonically, below Euler.
                double lo=0,hi=ncr/total*(1-1e-12);
                for(int i=0;i<80;i++){double mid=(lo+hi)/2;if(Stress(mid)<=limit)lo=mid;else hi=mid;}
                r.ForceResistance=Math.Min(r.ForceResistance,force*lo);
                r.Value("Terminal.ConditionalForceResistance",r.ForceResistance,"N");
            }
            return new SteelPackedPairTerminalResult(demand,d,r,force);
        }
    }
}
