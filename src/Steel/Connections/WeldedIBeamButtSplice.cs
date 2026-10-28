using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum SpliceWeldPenetration { Full, Partial }

    /// <summary>Declared execution detail for one transverse parent-plate seam.
    /// Matching numerical dimensions and provenance are prerequisites, not a weld inspection.
    /// EffectiveLength excludes missing/defective ends; no reinforcement is credited.</summary>
    public sealed class SpliceButtWeldDetail
    {
        public SpliceWeldPenetration Penetration { get; }
        public double FusionDepth { get; }
        public double EffectiveLength { get; }
        public double AllWeldMinimumFy { get; }
        public double AllWeldMinimumFu { get; }
        public string ProcedureQualificationSource { get; }
        public string PreparationAndInspectionSource { get; }
        public SpliceButtWeldDetail(SpliceWeldPenetration penetration, double fusionDepth, double effectiveLength,
            double allWeldMinimumFy, double allWeldMinimumFu, string procedureQualificationSource, string preparationAndInspectionSource)
        {
            if (!Enum.IsDefined(typeof(SpliceWeldPenetration), penetration)) throw new ArgumentOutOfRangeException(nameof(penetration));
            foreach (var value in new[] { fusionDepth, effectiveLength, allWeldMinimumFy, allWeldMinimumFu })
                ConnectionValidation.Positive(value, nameof(value));
            if (allWeldMinimumFu < allWeldMinimumFy) throw new ArgumentException("All-weld fu must not be below fy.");
            Penetration = penetration; FusionDepth = fusionDepth; EffectiveLength = effectiveLength;
            AllWeldMinimumFy = allWeldMinimumFy; AllWeldMinimumFu = allWeldMinimumFu;
            ProcedureQualificationSource = procedureQualificationSource; PreparationAndInspectionSource = preparationAndInspectionSource;
        }
    }

    /// <summary>Two evaluated, immutable section snapshots at the same aligned butt plane.
    /// Actions on both snapshots use the SAME positive section-face convention and centroidal
    /// principal frame; they are not a pair of opposite nodal end-force vectors.</summary>
    public sealed class WeldedIBeamButtSpliceInput
    {
        public ConnectionDesignCode Code { get; }
        public SteelVerificationResult LeftSection { get; }
        public SteelVerificationResult RightSection { get; }
        public SpliceButtWeldDetail TopFlange { get; }
        public SpliceButtWeldDetail Web { get; }
        public SpliceButtWeldDetail BottomFlange { get; }
        public string ActionCombinationSource { get; }
        public string AlignmentAndSeamContinuitySource { get; }
        public WeldedIBeamButtSpliceInput(ConnectionDesignCode code, SteelVerificationResult leftSection,
            SteelVerificationResult rightSection, SpliceButtWeldDetail topFlange, SpliceButtWeldDetail web,
            SpliceButtWeldDetail bottomFlange, string actionCombinationSource, string alignmentAndSeamContinuitySource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            LeftSection = leftSection ?? throw new ArgumentNullException(nameof(leftSection));
            RightSection = rightSection ?? throw new ArgumentNullException(nameof(rightSection));
            TopFlange = topFlange ?? throw new ArgumentNullException(nameof(topFlange));
            Web = web ?? throw new ArgumentNullException(nameof(web)); BottomFlange = bottomFlange ?? throw new ArgumentNullException(nameof(bottomFlange));
            ActionCombinationSource = actionCombinationSource; AlignmentAndSeamContinuitySource = alignmentAndSeamContinuitySource;
        }
    }

    public sealed class WeldedIBeamButtSpliceResult
    {
        public WeldedIBeamButtSpliceInput Input { get; }
        public ConnectionVerification<WeldedIBeamButtSpliceInput> SeamPrerequisites { get; }
        public SteelVerificationResult LeftSection => Input.LeftSection;
        public SteelVerificationResult RightSection => Input.RightSection;
        public bool IsStaticResistanceComplete { get; }
        public ConnectionStatus Status { get; }
        public bool HasFailedSection => new[] { LeftSection, RightSection }.Any(r => r.Checks.Any(c => c.Status == SteelExecutionStatus.Failed));
        public bool CertifiesWeldExecution => false;
        public bool CoversMemberStability => false;
        public bool CoversFatigue => false;
        public string Scope => "STATIC ALIGNED CJP BUTT SPLICE of identical, unperforated welded I geometry under one N-V2-M1 state. "
            + "Equivalence to both connected section assessments, conditional on the stated qualified full-penetration execution. "
            + "No new section resistance formula, stiffness curve, member stability, execution certification or fatigue assessment.";
        internal WeldedIBeamButtSpliceResult(WeldedIBeamButtSpliceInput input, ConnectionVerification<WeldedIBeamButtSpliceInput> prerequisites)
        {
            Input = input; SeamPrerequisites = prerequisites;
            bool bound = prerequisites.Diagnostics.Count == 0;
            IsStaticResistanceComplete = bound && prerequisites.IsComplete && input.LeftSection.IsComplete && input.RightSection.IsComplete;
            Status = !bound ? ConnectionStatus.NotSupported
                : prerequisites.Status == ConnectionStatus.Failed || HasFailedSection ? ConnectionStatus.Failed
                : IsStaticResistanceComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        }
    }

    internal static class WeldedIBeamButtSplice
    {
        public static WeldedIBeamButtSpliceResult Verify(WeldedIBeamButtSpliceInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var left = p.LeftSection.Plan.Input; var right = p.RightSection.Plan.Input;
            if (string.IsNullOrWhiteSpace(p.ActionCombinationSource)) issues.Add("ACTION_COMBINATION_SOURCE_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.AlignmentAndSeamContinuitySource)) issues.Add("ALIGNMENT_AND_SEAM_INTERSECTION_EXECUTION_SOURCE_REQUIRED");
            foreach (var r in new[] { p.LeftSection, p.RightSection })
            {
                if (r.Plan.Scope != SteelCheckScope.Section) issues.Add("SECTION_ONLY_ASSESSMENT_REQUIRED");
                if (r.Plan.Input.Section.Unperforated != true || r.Plan.Input.Section.NetArea.HasValue) issues.Add("UNPERFORATED_UNNOTCHED_SECTION_REQUIRED");
                if (!SameCode(p.Code.Plates, r.Plan.Input.Code.SectionCode)) issues.Add("SECTION_DESIGN_CODE_OR_FACTORS_MISMATCH");
                if (Math.Abs(r.Plan.Input.Section.PrincipalAxisAngle) > 1e-12) issues.Add("PHYSICAL_HORIZONTAL_MAJOR_AXIS_REQUIRED");
                var a = r.Plan.Input.Actions;
                if (a.V1 != 0 || a.M2 != 0 || a.T != 0) issues.Add("N_MAJOR_MOMENT_AND_WEB_SHEAR_ONLY");
            }
            if (!SameActions(left.Actions, right.Actions)) issues.Add("CONCOMITANT_ACTIONS_MISMATCH");
            var lm = left.Section.ReadOnlySection; var rm = right.Section.ReadOnlySection;
            var lh = lm.SectionShape as SectionH; var rh = rm.SectionShape as SectionH;
            if (lm.SectionShape.GetType() != typeof(SectionH) || rm.SectionShape.GetType() != typeof(SectionH)
                || lm.SectionType != Section.SectionTypes.Welded || rm.SectionType != Section.SectionTypes.Welded)
                issues.Add("EXACT_WELDED_I_SECTIONS_REQUIRED");
            if (lh != null && rh != null)
            {
                if (lh.R != 0 || rh.R != 0) issues.Add("THREE_PLATE_GEOMETRY_WITHOUT_CREDITED_ROOT_REINFORCEMENT_REQUIRED");
                if (lh.Height != rh.Height || lh.ThicknessWeb != rh.ThicknessWeb || lh.LenghtTopFlange != rh.LenghtTopFlange
                    || lh.LenghtBottomFlange != rh.LenghtBottomFlange || lh.ThicknessTopFlange != rh.ThicknessTopFlange
                    || lh.ThicknessBottomFlange != rh.ThicknessBottomFlange) issues.Add("IDENTICAL_ALIGNED_GEOMETRY_REQUIRED");
                if (issues.Count == 0)
                {
                    // Matching both materials is a deliberate conservative scope when grades differ.
                    double fy = Math.Max(left.Section.YieldStrength, right.Section.YieldStrength);
                    double fu = Math.Max(left.Section.UltimateStrength, right.Section.UltimateStrength);
                    Check("TopFlange", p.TopFlange, lh.ThicknessTopFlange, lh.LenghtTopFlange, fy, fu);
                    Check("Web", p.Web, lh.ThicknessWeb, lh.HeightWeb, fy, fu);
                    Check("BottomFlange", p.BottomFlange, lh.ThicknessBottomFlange, lh.LenghtBottomFlange, fy, fu);
                }
            }
            var assessment = new ConnectionVerification<WeldedIBeamButtSpliceInput>(p, p.Code, "Steel.Connection.WeldedIBeamButtSplice.Prerequisites",
                "Full-penetration seam and matching-consumable prerequisites. Read the linked left/right section assessments for static resistance.",
                checks, trace, issues.Distinct(), new[] {
                    "EN1993-1-8:2005 4.7.1 and NTC2018 4.2.8.2.1: qualified full-penetration butt weld resistance equals the weaker connected part. No fillet-weld throat formula or additional resistance is credited.",
                    "Effective seam lengths/depths must cover every parent plate; declared preparation includes the three seam intersections and complete fusion. Provenance strings do not inspect or certify the executed weld.",
                    "Both section results retain their actual geometry, code, six actions, checks, missing preparations and failures. No passed normal-resistance check substitutes for a missing shear or interaction assessment.",
                    "The same internal section-face action convention is required on both sides; opposite nodal end forces must be converted before calling this API.",
                    "Different steel grades are admitted only with consumable minima matching BOTH materials, a conservative restriction. No material averaging, section transition, misalignment or partial penetration is admitted." });
            return new WeldedIBeamButtSpliceResult(p, assessment);

            void Check(string name, SpliceButtWeldDetail weld, double thickness, double length, double fy, double fu)
            {
                if (weld.Penetration != SpliceWeldPenetration.Full) issues.Add(name + ".FULL_PENETRATION_REQUIRED");
                if (weld.FusionDepth != thickness || weld.EffectiveLength < length) issues.Add(name + ".INCOMPLETE_OR_INCONSISTENT_PARENT_COVERAGE");
                if (string.IsNullOrWhiteSpace(weld.ProcedureQualificationSource) || string.IsNullOrWhiteSpace(weld.PreparationAndInspectionSource))
                    issues.Add(name + ".QUALIFIED_EXECUTION_SOURCES_REQUIRED");
                string reference = p.Code.IsNtc ? "NTC2018 4.2.8.2.1 and 11.3.4.5; matching consumable prerequisite EN1993-1-8:2005 4.7.1"
                    : "EN1993-1-8:2005 4.3.4,4.7.1";
                checks.Add(new ConnectionCheck(name + ".AllWeldYield", fy, weld.AllWeldMinimumFy, reference, "MPa"));
                checks.Add(new ConnectionCheck(name + ".AllWeldUltimate", fu, weld.AllWeldMinimumFu, reference, "MPa"));
                trace.Add(new SectionResistanceValue(name + ".ParentThickness", thickness, "mm"));
                trace.Add(new SectionResistanceValue(name + ".ParentLength", length, "mm"));
            }
        }
        private static bool SameActions(SteelActions a, SteelActions b) => a.N == b.N && a.V1 == b.V1 && a.V2 == b.V2 && a.T == b.T && a.M1 == b.M1 && a.M2 == b.M2;
        private static bool SameCode(SectionResistanceCode a, SectionResistanceCode b) => a.Standard == b.Standard && a.GammaM0 == b.GammaM0 && a.GammaM2 == b.GammaM2;
    }
}
