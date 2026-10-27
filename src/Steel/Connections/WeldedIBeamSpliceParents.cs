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
    /// <summary>Two identical continuous longitudinal fillets at each web/flange
    /// junction, on BOTH member ends. Effective length per side excludes the butt gap.
    /// No transverse concentrated load, intermittent weld or partial-penetration credit.</summary>
    public sealed class SpliceLongitudinalFillets
    {
        public double TopThroat { get; }
        public double BottomThroat { get; }
        public double EffectiveLengthEachSide { get; }
        public FilletWeldSteelGrade TopGrade { get; }
        public FilletWeldSteelGrade WebGrade { get; }
        public FilletWeldSteelGrade BottomGrade { get; }
        public string QualifiedContinuousExecutionSource { get; }
        public SpliceLongitudinalFillets(double topThroat, double bottomThroat, double effectiveLengthEachSide,
            FilletWeldSteelGrade topGrade, FilletWeldSteelGrade webGrade, FilletWeldSteelGrade bottomGrade, string qualifiedContinuousExecutionSource)
        {
            ConnectionValidation.Positive(topThroat, nameof(topThroat)); ConnectionValidation.Positive(bottomThroat, nameof(bottomThroat));
            ConnectionValidation.Positive(effectiveLengthEachSide, nameof(effectiveLengthEachSide));
            foreach (var grade in new[] { topGrade, webGrade, bottomGrade })
                if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            TopThroat = topThroat; BottomThroat = bottomThroat; EffectiveLengthEachSide = effectiveLengthEachSide;
            TopGrade = topGrade; WebGrade = webGrade; BottomGrade = bottomGrade;
            QualifiedContinuousExecutionSource = qualifiedContinuousExecutionSource;
        }
    }
    public sealed class WeldedIBeamSpliceParentInput
    {
        public WeldedIBeamSpliceDetailInput Detail { get; }
        public SteelVerificationResult LeftSection { get; }
        public SteelVerificationResult RightSection { get; }
        public SteelVerificationResult LeftMember { get; }
        public SteelVerificationResult RightMember { get; }
        public SpliceLongitudinalFillets Welds { get; }
        public string ContinuityAndSecondOrderAnalysisSource { get; }
        public WeldedIBeamSpliceParentInput(WeldedIBeamSpliceDetailInput detail, SteelVerificationResult leftSection,
            SteelVerificationResult rightSection, SpliceLongitudinalFillets welds, string continuityAndSecondOrderAnalysisSource,
            SteelVerificationResult leftMember = null, SteelVerificationResult rightMember = null)
        {
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
            LeftSection = leftSection ?? throw new ArgumentNullException(nameof(leftSection));
            RightSection = rightSection ?? throw new ArgumentNullException(nameof(rightSection));
            Welds = welds ?? throw new ArgumentNullException(nameof(welds));
            ContinuityAndSecondOrderAnalysisSource = continuityAndSecondOrderAnalysisSource;
            LeftMember = leftMember; RightMember = rightMember;
        }
    }
    public sealed class WeldedIBeamSpliceParentResult
    {
        public WeldedIBeamSpliceParentInput Input { get; }
        public IReadOnlyList<string> BindingDiagnostics { get; }
        public ConnectionVerification<WeldedIBeamSpliceParentInput> LongitudinalWelds { get; }
        public bool IsBound => BindingDiagnostics.Count == 0;
        public bool IsSectionAndWeldComplete => IsBound && Input.LeftSection.IsComplete && Input.RightSection.IsComplete && LongitudinalWelds.IsComplete;
        public bool HasFailedSectionOrWeld => Input.LeftSection.Checks.Concat(Input.RightSection.Checks).Any(c => c.Status == SteelExecutionStatus.Failed)
            || LongitudinalWelds.Status == ConnectionStatus.Failed;
        public bool HasBothMemberAssessments => IsBound && Input.LeftMember?.IsComplete == true && Input.RightMember?.IsComplete == true;
        public bool HasFailedMember => new[] { Input.LeftMember, Input.RightMember }.Where(r => r != null).Any(r => r.Checks.Any(c => c.Status == SteelExecutionStatus.Failed));
        internal WeldedIBeamSpliceParentResult(WeldedIBeamSpliceParentInput input, IEnumerable<string> issues,
            ConnectionVerification<WeldedIBeamSpliceParentInput> welds)
        { Input = input; BindingDiagnostics = Array.AsReadOnly(issues.Distinct().ToArray()); LongitudinalWelds = welds; }
    }

    public static class WeldedIBeamSpliceParents
    {
        /// <summary>Splice X=member longitudinal, Y=section up. Steel M1=int(sigma*y),
        /// while splice Mz=-int(sigma*y): M1=-Mz, V2=Fy. Transport the pole FIRST.
        /// Both connected sections use the SAME positive section-face convention.</summary>
        public static SteelActions ToSteelActions(WeldedIBeamSpliceForceInput input)
        {
            return ToSteelActions(WeldedIBeamSpliceForces.Calculate(input));
        }
        private static SteelActions ToSteelActions(WeldedIBeamSpliceForceResult f)
        {
            if (!f.IsBalanced) throw new ArgumentException("Supported balanced splice force model required.", nameof(f));
            var a = f.ActionsAtJoint;
            return new SteelActions(a.Fx, 0, a.Fy, 0, -a.Mz, 0);
        }
        public static WeldedIBeamSpliceParentResult Verify(WeldedIBeamSpliceParentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, WeldedIBeamSpliceForces.Calculate(p.Detail.Plates.Bolts.Detail));
        }
        internal static WeldedIBeamSpliceParentResult Verify(WeldedIBeamSpliceParentInput p, WeldedIBeamSpliceForceResult f)
        {
            var d = p.Detail.Plates.Bolts.Detail; var code = p.Detail.Plates.Bolts.Code;
            if (!ReferenceEquals(d, f.Input)) throw new ArgumentException("Prepared forces belong to another input.");
            var issues = new List<string>();
            issues.AddRange(f.Diagnostics);
            if (string.IsNullOrWhiteSpace(p.ContinuityAndSecondOrderAnalysisSource)) issues.Add("CONTINUITY_RESTRAINTS_AND_SECOND_ORDER_ANALYSIS_SOURCE_REQUIRED");
            if (f.IsBalanced)
            {
                var a = ToSteelActions(f);
                Bind(p.LeftSection, "Left.Section", false); Bind(p.RightSection, "Right.Section", false);
                if (p.LeftMember != null) Bind(p.LeftMember, "Left.Member", true);
                if (p.RightMember != null) Bind(p.RightMember, "Right.Member", true);
                void Bind(SteelVerificationResult result, string name, bool member)
                {
                    var i = result.Plan.Input; var c = i.Section.ReadOnlySection; var h = c.SectionShape as SectionH; var s = d.Section;
                    if (c.SectionShape.GetType() != typeof(SectionH) || c.SectionType != Section.SectionTypes.Welded || h == null || h.R != 0)
                        issues.Add(name + ".EXACT_WELDED_I_REQUIRED");
                    else if (h.HeightWeb != s.Web.Width || h.ThicknessWeb != s.Web.Thickness || h.LenghtTopFlange != s.TopFlange.Width
                        || h.ThicknessTopFlange != s.TopFlange.Thickness || h.LenghtBottomFlange != s.BottomFlange.Width || h.ThicknessBottomFlange != s.BottomFlange.Thickness)
                        issues.Add(name + ".GEOMETRY_MISMATCH");
                    if (i.Section.YieldStrength != new[] { s.TopFlange, s.Web, s.BottomFlange }.Min(x => x.Fy)
                        || i.Section.UltimateStrength != new[] { s.TopFlange, s.Web, s.BottomFlange }.Min(x => x.Fu))
                        issues.Add(name + ".WEAKEST_PARENT_MATERIAL_MISMATCH");
                    if (i.Section.ElasticModulus != p.Detail.Plates.Policy.ElasticModulus || Math.Abs(i.Section.PrincipalAxisAngle) > 1e-12)
                        issues.Add(name + ".MODULUS_OR_PHYSICAL_AXES_MISMATCH");
                    if (i.Code.SectionCode.Standard != code.Plates.Standard || i.Code.SectionCode.GammaM0 != code.Plates.GammaM0
                        || i.Code.SectionCode.GammaM2 != code.Plates.GammaM2) issues.Add(name + ".CODE_MISMATCH");
                    if (member)
                    {
                        if (result.Plan.Scope == SteelCheckScope.Section || i.Code.GammaM1 != p.Detail.Plates.Policy.GammaM1)
                            issues.Add(name + ".MEMBER_SCOPE_OR_GAMMA_M1_REQUIRED");
                        // Whole-member governing actions are not generally equal to this cut.
                    }
                    else
                    {
                        if (result.Plan.Scope == SteelCheckScope.Member) issues.Add(name + ".SECTION_SCOPE_REQUIRED");
                        if (result.Plan.Decisions.Any(decision => decision.SectionClass == 4))
                            issues.Add(name + ".CLASS4_PERFORATED_PARENT_OUTSIDE_STANDARD_SPLICE_SCOPE");
                        if (i.Section.Unperforated != true || i.Section.NetArea.HasValue) issues.Add(name + ".GROSS_PARENT_BASELINE_REQUIRED_NET_HOLES_CHECKED_BY_SPLICE");
                        if (i.Actions.N != a.N || i.Actions.V1 != 0 || i.Actions.V2 != a.V2 || i.Actions.T != 0
                            || i.Actions.M1 != a.M1 || i.Actions.M2 != 0) issues.Add(name + ".ACTION_OR_AXIS_SIGN_MISMATCH");
                    }
                }
            }
            var weldIssues = new List<string>(f.Diagnostics); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var w = p.Welds;
            if (string.IsNullOrWhiteSpace(w.QualifiedContinuousExecutionSource)) weldIssues.Add("QUALIFIED_CONTINUOUS_DOUBLE_FILLET_EXECUTION_REQUIRED");
            double half = Math.Max(d.WebBolts.FirstBoltDistance + d.WebBolts.LongitudinalSpan + d.WebCoverEnd,
                Math.Max(d.Top.Bolts.FirstBoltDistance + d.Top.Bolts.LongitudinalSpan + d.Top.EndDistance,
                d.Bottom.Bolts.FirstBoltDistance + d.Bottom.Bolts.LongitudinalSpan + d.Bottom.EndDistance));
            if (w.EffectiveLengthEachSide < half - d.ButtGap / 2) weldIssues.Add("CONTINUOUS_WELD_MUST_COVER_ENTIRE_LAP");
            Weld(true); Weld(false);
            var assessment = new ConnectionVerification<WeldedIBeamSpliceParentInput>(p, code, "Steel.Connection.WeldedIBeamSplice.LongitudinalWelds",
                "Continuous double web/flange fillets under longitudinal elastic shear flow VQ/I. No transverse concentrated force, intermittent seam or execution certification.",
                checks, trace, weldIssues.Distinct(), new[] {
                    "q=V Af yf/I, shared equally by the two identical longitudinal fillets. The concentrated flange transfer is supplied by flange bolts; it is not added again to this web/flange shear flow.",
                    "Actual parent materials, seam throats and complete effective lap length are bound to the splice geometry. FilletWeldResistance is reused with the directional method.",
                    "Gross parent section results are retained unchanged. Perforated parent section is checked separately by StandardPlates. Member stability is reported only when actual member assessments are supplied; no length, Mcr or restraint is invented." });
            return new WeldedIBeamSpliceParentResult(p, issues, assessment);
            void Weld(bool top)
            {
                var s = d.Section; var plate = top ? s.TopFlange : s.BottomFlange;
                double throat = top ? w.TopThroat : w.BottomThroat, toe = top ? s.TopWeldToe : s.BottomWeldToe;
                var grade = top ? w.TopGrade : w.BottomGrade; string name = top ? "Top" : "Bottom";
                if (throat < 3 || w.EffectiveLengthEachSide < Math.Max(30, 6 * throat)) weldIssues.Add(name + ".FILLET_MINIMUM_GEOMETRY");
                if (Math.Sqrt(2) * throat > toe) weldIssues.Add(name + ".FILLET_LEG_EXCEEDS_DECLARED_SEAM_ENVELOPE");
                if (plate.Fy > (int)grade || s.Web.Fy > (int)w.WebGrade) weldIssues.Add(name + ".PARENT_GRADE_INCONSISTENT_WITH_YIELD_STRENGTH");
                double flow = Math.Abs(d.Actions.Fy * plate.Width * plate.Thickness * (top ? s.TopY : s.BottomY) / s.StrongAxisSecondMoment);
                double tau = flow / (2 * throat);
                if (!WeldedIBeamSplicePlates.Finite(tau)) { weldIssues.Add(name + ".NUMERIC_RANGE"); return; }
                var r = FilletWeldResistance.Verify(code, new FilletWeldMaterial(grade, plate.Fu, plate.Thickness),
                    new FilletWeldMaterial(w.WebGrade, s.Web.Fu, s.Web.Thickness), 0, 0, tau, FilletWeldMethod.Directional);
                weldIssues.AddRange(r.Diagnostics);
                foreach (var check in r.Checks) checks.Add(new ConnectionCheck(name + "." + check.Id, check.Demand, check.Resistance, check.Reference, check.Unit));
                trace.Add(new SectionResistanceValue(name + ".ShearFlow", flow, "N/mm"));
                trace.Add(new SectionResistanceValue(name + ".TauParallel", tau, "MPa"));
            }
        }
    }
}
