using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Intermediate solid packing with two complete external weld loops under
    /// computed longitudinal transfer only. The two physical faces each transfer Q;
    /// their opposite signs do not halve Q or cancel the local wall/lap stresses.</summary>
    public sealed class SteelWeldedPackingResult
    {
        public PackedPairImperfectionModel Demand { get; }
        public PackedPairWeldedLapDetail Detail { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete=>Diagnostics.Count==0;
        public bool CanQualifyWholeStation=>IsComplete;
        public bool CanQualifyWholeMember=>false;
        public ConnectionStatus Status=>Checks.Any(x=>!x.Passed)?ConnectionStatus.Failed:IsComplete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
        public double? Utilization=>IsComplete?Checks.Max(x=>x.Utilization):(double?)null;
        public double MaximumConnectionDemand { get; }
        public double? ConditionalForceResistance { get; }
        internal SteelWeldedPackingResult(PackedPairImperfectionModel demand,PackedPairWeldedLapDetail detail,PackedPairWeldedLapResult r,double q)
        {Demand=demand;Detail=detail;Checks=Array.AsReadOnly(r.Checks.ToArray());Trace=Array.AsReadOnly(r.Trace.ToArray());Diagnostics=Array.AsReadOnly(r.Issues.ToArray());
            MaximumConnectionDemand=q;ConditionalForceResistance=IsComplete&&r.ForceResistance>0&&r.ForceResistance<double.MaxValue?r.ForceResistance:(double?)null;}
    }
    public static class SteelWeldedPacking
    {
        public static SteelWeldedPackingResult Verify(PackedPairImperfectionModel demand,PackedPairWeldedLapDetail detail)
        {
            if(demand==null)throw new ArgumentNullException(nameof(demand));if(detail==null)throw new ArgumentNullException(nameof(detail));
            var stations=demand.Stations.Skip(1).Take(Math.Max(0,demand.Stations.Count-2)).ToArray();
            double q=stations.Select(x=>x.LongitudinalShear).DefaultIfEmpty(0).Max();
            var r=PackedPairWeldedLap.Verify(demand,detail,q);double footprint=detail.Length+2*Math.Sqrt(2)*detail.Throat;
            r.Require(demand.Layout.Input.Section.Unperforated==true,"UnperforatedMemberRequiredForAllWeldedRoute");
            r.Require(stations.Length>0,"AtLeastOneIntermediateWeldedPackingRequired");
            r.Require(demand.Layout.PanelLengths.All(x=>x>=footprint),"IntermediateWeldFootprintsMustNotOverlap");
            foreach(var s in stations)
            {
                r.Require(s.Station>=footprint/2&&s.Station+footprint/2<=demand.Layout.Input.Member.Length,"WeldFootprintOutsideMember");
                r.Value("Packing.Station"+s.Index+".ForcePerFace",s.LongitudinalShear,"N");
            }
            r.Value("Packing.MaximumForcePerFace",q,"N");
            return new SteelWeldedPackingResult(demand,detail,r,q);
        }
    }
}
