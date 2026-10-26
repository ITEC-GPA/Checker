using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Conventional symmetric category-A2L tension terminal. A single bolt
    /// line on each connected leg, central gusset, matching coplanar collector and
    /// full-penetration butt weld outside the lap. WeldDistance is positive towards
    /// the collector (-X), beyond the free angle end. Positive collector N is tension.
    /// No beam/column T attachment, moment, compression or frame stability model.</summary>
    public sealed class BraceAnglePairStandardTensionInput
    {
        public BraceAnglePairBoltedTransferInput Transfer { get; }
        public ConnectionPlate Collector { get; }
        public SteelActions CollectorCutActions { get; }
        public SpliceButtWeldDetail Weld { get; }
        public double WeldDistanceFromBoltCentroid { get; }
        public string CoplanarAlignmentAndCutSource { get; }
        public BraceAnglePairStandardTensionInput(BraceAnglePairBoltedTransferInput transfer, ConnectionPlate collector,
            SteelActions collectorCutActions, SpliceButtWeldDetail weld, double weldDistanceFromBoltCentroid,
            string coplanarAlignmentAndCutSource)
        {
            Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
            Collector = collector ?? throw new ArgumentNullException(nameof(collector));
            CollectorCutActions = collectorCutActions ?? throw new ArgumentNullException(nameof(collectorCutActions));
            Weld = weld ?? throw new ArgumentNullException(nameof(weld));
            ConnectionValidation.Positive(weldDistanceFromBoltCentroid, nameof(weldDistanceFromBoltCentroid));
            WeldDistanceFromBoltCentroid = weldDistanceFromBoltCentroid;
            CoplanarAlignmentAndCutSource = coplanarAlignmentAndCutSource;
        }
    }
    public sealed class BraceAnglePairStandardTensionResult
    {
        public BraceAnglePairStandardTensionInput Input { get; }
        public BraceAnglePairTerminalPlateResult Plates { get; }
        public ConnectionVerification<BraceAnglePairStandardTensionInput> Continuation { get; }
        public ConnectionVerification<BraceAnglePairBoltedTransferInput> BlockHierarchy { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public bool IsStaticResistanceComplete => MissingRequirements.Count == 0;
        public bool HasFailedComponent => Plates.Transfer.InPlaneComponents.Status == ConnectionStatus.Failed
            || Plates.GussetNetSection.Status == ConnectionStatus.Failed || Plates.CollinearBlocks.Status == ConnectionStatus.Failed
            || Continuation.Status == ConnectionStatus.Failed;
        public ConnectionStatus Status => HasFailedComponent ? ConnectionStatus.Failed
            : IsStaticResistanceComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public double? Utilization => !IsStaticResistanceComplete ? null : (double?)new[] {
            Plates.Transfer.InPlaneComponents.Utilization.Value, Plates.GussetNetSection.Utilization.Value,
            Plates.CollinearBlocks.Utilization ?? 0, Continuation.Utilization.Value }.Max();
        public bool SolvesLocalContactOrPrying => false;
        public string Scope => "Conventional static symmetric2L tension terminal, EN1993-1-8 2.7(2)/3.10.3 simplified angle model, "
            + "actual in-plane eccentric bolt forces, direct collinear blocks or sufficient block hierarchy, and constant coplanar CJP collector. No explicit local contact/stiffness solution, compression, T support or whole-member qualification.";
        internal BraceAnglePairStandardTensionResult(BraceAnglePairStandardTensionInput p, BraceAnglePairTerminalPlateResult plates,
            ConnectionVerification<BraceAnglePairStandardTensionInput> continuation,
            ConnectionVerification<BraceAnglePairBoltedTransferInput> hierarchy, IEnumerable<string> missing)
        { Input=p; Plates=plates; Continuation=continuation; BlockHierarchy=hierarchy; MissingRequirements=Array.AsReadOnly(missing.Distinct().ToArray()); }
    }
    public static class BraceAnglePairStandardTension
    {
        public static BraceAnglePairStandardTensionResult Verify(BraceAnglePairStandardTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var r=BraceAnglePairTerminalPlates.Verify(p.Transfer); var g=p.Transfer.Gusset; var c=p.Collector;
            var issues=new List<string>(); var checks=new List<ConnectionCheck>(); var trace=new List<SectionResistanceValue>();
            var first=p.Transfer.Angles.Components[0].Input; var a=p.CollectorCutActions; var w=p.Weld; var code=p.Transfer.Code;
            if (!r.Transfer.InPlaneComponents.IsComplete) issues.Add("ANGLE_TRANSFER_OUTSIDE_STANDARD_FIELD");
            var hierarchy=BraceAnglePairBlockHierarchy.Verify(p.Transfer,r.Transfer);
            if (!r.GussetNetSection.IsComplete) issues.Add("PLANE_NET_SECTION_MODEL_INCOMPLETE");
            if (!r.CollinearBlocks.IsComplete && hierarchy.Status!=ConnectionStatus.Passed)
                issues.Add("NONCOLLINEAR_BLOCK_HIERARCHY_NOT_ESTABLISHED");
            // The gusset and collector centroid must lie on the MEMBER setting-out
            // line, not necessarily on the bolt line. The actual in-plane bolt moment
            // and every transverse bolt force are retained and checked.
            var lever=r.Transfer.InPlaneComponents.Trace.FirstOrDefault(t=>t.Name=="GussetAtGroup.InPlaneLever");
            if (lever==null || lever.Value+g.Width/2-p.Transfer.GussetNegativeYEdge!=0)
                issues.Add("GUSSET_CENTROID_MUST_ALIGN_WITH_MEMBER_SETTING_OUT_LINE");
            if (first.BoltCount<3) issues.Add("AT_LEAST_THREE_BOLTS_REQUIRED_FOR_THIS_STANDARD_DETAIL");
            if (g.Width!=c.Width || g.Thickness!=c.Thickness) issues.Add("MATCHING_CONSTANT_RECTANGULAR_COLLECTOR_REQUIRED");
            if (Math.Max(Math.Max(g.Fy,c.Fy),first.Section.Fy)>460) issues.Add("CARBON_STEEL_UP_TO_S460_REQUIRED");
            if (a.N!=p.Transfer.Angles.SourceActions.N || a.V1!=0 || a.V2!=0 || a.T!=0 || a.M1!=0 || a.M2!=0)
                issues.Add("ACTUAL_COLLECTOR_CUT_MUST_MATCH_PURE_TENSION_CASE");
            if (p.WeldDistanceFromBoltCentroid <= (first.BoltCount-1)*first.Pitch/2+p.Transfer.AngleEndDistance)
                issues.Add("CJP_MUST_CLEAR_THE_FREE_ANGLE_END");
            if (w.Penetration!=SpliceWeldPenetration.Full || w.FusionDepth<g.Thickness || w.EffectiveLength<g.Width)
                issues.Add("FULL_WIDTH_FULL_PENETRATION_REQUIRED");
            if (w.AllWeldMinimumFy<Math.Max(g.Fy,c.Fy) || w.AllWeldMinimumFu<Math.Max(g.Fu,c.Fu))
                issues.Add("MATCHING_WELD_STRENGTH_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.CoplanarAlignmentAndCutSource) || string.IsNullOrWhiteSpace(w.ProcedureQualificationSource)
                || string.IsNullOrWhiteSpace(w.PreparationAndInspectionSource)) issues.Add("ALIGNED_CUT_AND_EXECUTION_EVIDENCE_REQUIRED");
            if (issues.Count==0)
            {
                double area=g.Width*g.Thickness, yield=area*Math.Min(g.Fy,c.Fy)/code.Plates.GammaM0,
                    rupture=.9*area*Math.Min(g.Fu,c.Fu)/code.Plates.GammaM2;
                if (double.IsInfinity(yield) || double.IsInfinity(rupture)) issues.Add("CONTINUATION_NUMERIC_RANGE");
                else
                {
                    checks.Add(new ConnectionCheck("Collector.GrossYield",a.N,yield,"EN1993-1-1 6.2.3 / NTC2018 4.2.4.1.2.1"));
                    checks.Add(new ConnectionCheck("Collector.ConservativeRupture",a.N,rupture,"Conservative0.9fu bound on the unperforated continuation"));
                    checks.Add(new ConnectionCheck("CJP.FullStrength",a.N,Math.Min(yield,rupture),"EN1993-1-8:2005 4.7.1 / NTC2018 4.2.8.2.1"));
                    trace.Add(new SectionResistanceValue("Collector.Area",area,"mm2"));
                }
            }
            var continuation=new ConnectionVerification<BraceAnglePairStandardTensionInput>(p,code,
                "Steel.Connection.BraceAnglePair.StandardTension.Continuation", "Matching constant-section CJP continuation, bounded at the actual collector cut.",
                checks,trace,issues,new[]{
                    "Conventional one-leg angle tension idealisation: EN1993-1-8 2.7(2) permits the3.10.3 effective-net method. It is applied independently to each actual L, with its beta and short-leg equivalent area where required; it is not replaced by gross paired area.",
                    "Symmetric double-lap bolt shear and individual ply bearing are checked independently. All local transported six-component actions remain available in Plates.Transfer.FaceDemands. This result does not assert that these moments are zero or supply an explicit contact/prying response.",
                    "The qualified result is restricted to the conventional symmetric pure-tension typology. In-plane bolt eccentricity is explicitly retained; noncollinear blocks require the sufficient fastener hierarchy. Externally applied bolt-axis force, unequal chord forces, compression or a generic support cannot use this route.",
                    "Under NTC the angle simplification is explicitly the supplementary EC method with NTC material factors. C/2C are not covered by the angle beta method.",
                    "The collector is the same rectangular section and midsurface as the gusset; no spreading to a beam or column is assumed. Member/frame verification and execution certification remain separate."});
            return new BraceAnglePairStandardTensionResult(p,r,continuation,hierarchy,issues);
        }
    }
}
