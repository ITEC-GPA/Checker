using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Concentric twin-flat tension transfer, continued through a full-width
    /// full-penetration butt weld into an identical rectangular collector cross section.
    /// CollectorCutActions are INTERNAL forces, N positive in tension, at its reference
    /// cut. They must equal the same concomitant transfer; no separate envelope input.</summary>
    public sealed class GussetTwinFlatCollectorInput
    {
        public GussetTwinFlatTensionInput Transfer { get; }
        public ConnectionPlate Collector { get; }
        public SteelActions CollectorCutActions { get; }
        public double WeldDistanceFromBoltCentroid { get; }
        public double FusionDepth { get; }
        public double WeldWidth { get; }
        public double WeldFy { get; }
        public double WeldFu { get; }
        public bool FullPenetration { get; }
        public bool MidSurfacesAligned { get; }
        public string ExecutionSource { get; }
        public string ConcomitantCutSource { get; }
        public GussetTwinFlatCollectorInput(GussetTwinFlatTensionInput transfer, ConnectionPlate collector,
            SteelActions collectorCutActions, double weldDistanceFromBoltCentroid, double fusionDepth,
            double weldWidth, double weldFy, double weldFu, bool fullPenetration, bool midSurfacesAligned,
            string executionSource, string concomitantCutSource)
        {
            Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
            Collector = collector ?? throw new ArgumentNullException(nameof(collector));
            CollectorCutActions = collectorCutActions ?? throw new ArgumentNullException(nameof(collectorCutActions));
            foreach (double x in new[] { weldDistanceFromBoltCentroid, fusionDepth, weldWidth, weldFy, weldFu })
                ConnectionValidation.Positive(x, nameof(x));
            if (weldFu < weldFy) throw new ArgumentException("Weld fu must not be below fy.");
            WeldDistanceFromBoltCentroid = weldDistanceFromBoltCentroid; FusionDepth = fusionDepth;
            WeldWidth = weldWidth; WeldFy = weldFy; WeldFu = weldFu; FullPenetration = fullPenetration;
            MidSurfacesAligned = midSurfacesAligned; ExecutionSource = executionSource; ConcomitantCutSource = concomitantCutSource;
        }
    }
    public sealed class GussetTwinFlatCollectorResult
    {
        public GussetTwinFlatCollectorInput Input { get; }
        public ConnectionVerification<GussetTwinFlatTensionInput> Transfer { get; }
        public ConnectionVerification<GussetTwinFlatCollectorInput> CollectorAndWeld { get; }
        public bool IsStaticResistanceComplete => Transfer.IsComplete && CollectorAndWeld.IsComplete;
        public ConnectionStatus Status => Transfer.Status == ConnectionStatus.Failed || CollectorAndWeld.Status == ConnectionStatus.Failed
            ? ConnectionStatus.Failed : IsStaticResistanceComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public double? Utilization => IsStaticResistanceComplete ? (double?)Math.Max(Transfer.Utilization.Value, CollectorAndWeld.Utilization.Value) : null;
        public string Scope => "Static concentric twin-flat tension terminal through central gusset and matching coplanar CJP collector, up to the supplied collector cut. No beam/column T attachment, member instability, fatigue or frame qualification.";
        internal GussetTwinFlatCollectorResult(GussetTwinFlatCollectorInput p, ConnectionVerification<GussetTwinFlatTensionInput> t,
            ConnectionVerification<GussetTwinFlatCollectorInput> c) { Input=p; Transfer=t; CollectorAndWeld=c; }
    }
    public static class GussetTwinFlatCollector
    {
        public static GussetTwinFlatCollectorResult Verify(GussetTwinFlatCollectorInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var t = GussetTwinFlatTension.Verify(p.Transfer); var g = p.Transfer.Gusset; var c = p.Collector;
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            var a = p.CollectorCutActions; var code = p.Transfer.Code;
            if (!t.IsComplete) issues.Add("TRANSFER_OUTSIDE_CONCENTRIC_TENSION_FIELD");
            // No diffusion angle, effective-width guess or discontinuous thickness
            // transition. The continuation is exactly the same gross cross section.
            if (g.Width != c.Width || g.Thickness != c.Thickness || !p.MidSurfacesAligned)
                issues.Add("MATCHING_RECTANGULAR_COPLANAR_SECTIONS_REQUIRED");
            if (!p.FullPenetration || p.FusionDepth < g.Thickness || p.WeldWidth < g.Width)
                issues.Add("FULL_WIDTH_FULL_PENETRATION_REQUIRED");
            if (p.WeldFy < Math.Max(g.Fy,c.Fy) || p.WeldFu < Math.Max(g.Fu,c.Fu)) issues.Add("COMPATIBLE_WELD_METAL_REQUIRED");
            if (c.Fy > 460) issues.Add("COLLECTOR_CARBON_STEEL_UP_TO_S460_ONLY");
            if (p.WeldDistanceFromBoltCentroid <= (p.Transfer.Rows-1)*p.Transfer.Pitch/2 + p.Transfer.BraceEnd)
                issues.Add("BUTT_WELD_MUST_CLEAR_ENTIRE_BOLTED_OVERLAP");
            if (a.N != p.Transfer.Actions.Fx || a.V1 != 0 || a.V2 != 0 || a.T != 0 || a.M1 != 0 || a.M2 != 0)
                issues.Add("COLLECTOR_CUT_MUST_MATCH_CONCOMITANT_AXIAL_TRANSFER");
            if (string.IsNullOrWhiteSpace(p.ExecutionSource) || string.IsNullOrWhiteSpace(p.ConcomitantCutSource))
                issues.Add("EXECUTION_AND_ACTUAL_CUT_SOURCES_REQUIRED");
            if (issues.Count == 0)
            {
                double area = g.Width*g.Thickness;
                double yield = area*Math.Min(g.Fy,c.Fy)/code.Plates.GammaM0;
                double rupture = .9*area*Math.Min(g.Fu,c.Fu)/code.Plates.GammaM2;
                if (double.IsInfinity(yield) || double.IsInfinity(rupture)) issues.Add("COLLECTOR_RESISTANCE_OUTSIDE_NUMERIC_RANGE");
                else
                {
                    checks.Add(new ConnectionCheck("Continuation.GrossYield",a.N,yield,"EN1993-1-1 6.2.3 / NTC2018 4.2.4.1.2.1"));
                    checks.Add(new ConnectionCheck("Continuation.ConservativeRupture",a.N,rupture,"Conservative0.9fu gross-area bound without holes; no weld reinforcement credit"));
                    checks.Add(new ConnectionCheck("ButtWeld.FullStrength",a.N,Math.Min(yield,rupture),
                        "EN1993-1-8:2005 4.7.1 / NTC2018 4.2.8.2.1; matching full-penetration weld, weaker connected part"));
                    trace.Add(new SectionResistanceValue("Continuation.Area",area,"mm2"));
                    trace.Add(new SectionResistanceValue("Continuation.ConcomitantN",a.N,"N"));
                }
            }
            var r = new ConnectionVerification<GussetTwinFlatCollectorInput>(p,code,"Steel.Connection.Gusset.TwinFlat.CoplanarCollector",
                "Unperforated constant rectangular continuation and matching CJP butt weld, bounded at the collector cut.", checks,trace,issues,
                new[] { "No wider-plate spreading or T-support bending is credited. Collector has the same width, thickness and mid-surface as the gusset.",
                    "Execution/material evidence does not replace numerical checks. Weld metal must match both plates and the entire section must be fused.",
                    "Collector forces are the actual concomitant internal actions at a cut; positive N means tension. No moments or shear are discarded by tolerance.",
                    "The transfer result remains available, including failure, even if the collector is outside scope." });
            return new GussetTwinFlatCollectorResult(p,t,r);
        }
    }
}
