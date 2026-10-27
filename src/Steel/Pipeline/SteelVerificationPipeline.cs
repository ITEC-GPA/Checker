using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Pipeline
{
    /// <summary>Executes the immutable selection plan over existing resistance methods.
    /// Unavailable checks stay in the output and do not suppress independent ready checks.
    /// No member diagram, restraints, classification or national coefficient is inferred.
    /// Cancellation propagates; it is never turned into an engineering result.</summary>
    public static class SteelVerificationPipeline
    {
        public static SteelVerificationResult Execute(SteelSelectionPlan plan,
            IEnumerable<SteelShearBucklingAssessment> shearAssessments = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            cancellationToken.ThrowIfCancellationRequested();
            var assessments = new Dictionary<int, SteelShearBucklingAssessment>();
            foreach (var assessment in shearAssessments ?? Enumerable.Empty<SteelShearBucklingAssessment>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (assessment == null || !ReferenceEquals(assessment.Input, plan.Input))
                    throw new ArgumentException("Every assessment must belong to this exact input snapshot/code/action state.", nameof(shearAssessments));
                if (assessments.ContainsKey(assessment.Axis))
                    throw new ArgumentException("Only one assessment per shear axis is allowed.", nameof(shearAssessments));
                assessments.Add(assessment.Axis, assessment);
            }
            var checks = new List<SteelCheckExecution>();
            foreach (var decision in plan.Decisions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                checks.Add(ExecuteOne(plan.Input, decision, assessments, plan.MemberAnalysis, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            foreach(var assessment in assessments.Values.Where(v=>v.PanelDetails!=null))
            {
                var d=assessment.PanelDetails;
                var decision=new SteelMethodDecision(SteelCheckTarget.WebPanelDetails,SteelSelectionStatus.Ready,
                    SteelMethod.WebPanelDetails,"WebPanel.ComputedDetails","Computed same-case boundary and attachment checks.");
                var trace=d.Trace.Concat(d.Checks.Select(c=>new SectionResistanceValue("Detail."+c.Name,c.Utilization,"1")));
                checks.Add(new SteelCheckExecution(decision,d.Passed?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                    d.Passed?null:"WebPanel.DetailFailure",d.Stable?null:"Stiffener second-order equilibrium is unstable.",
                    "EN1993-1-5:2006 section9; EN1993-1-8:2005 4.5.3.3",
                    "Actual bilateral flat stiffeners, web/flange welds, direct bearing and optional paired rigid end post; local panel scope.",
                    utilization:d.Utilization,trace:trace,shearAssessments:new[]{assessment}));
            }
            return new SteelVerificationResult(plan, checks);
        }

        private static SteelCheckExecution ExecuteOne(SteelVerificationInput input, SteelMethodDecision decision,
            IDictionary<int, SteelShearBucklingAssessment> assessments, SteelMemberAnalysis memberAnalysis, CancellationToken cancellationToken)
        {
            if (decision.Status != SteelSelectionStatus.Ready && decision.Status != SteelSelectionStatus.RequiresPreparation)
                return new SteelCheckExecution(decision, SelectionStatus(decision.Status), decision.DiagnosticCode, decision.Message);

            var used = new List<SteelShearBucklingAssessment>();
            if (decision.Status == SteelSelectionStatus.RequiresPreparation)
            {
                if (decision.RequiredPreparation.Count == 0)
                    throw new InvalidOperationException("A blocked method must declare its prerequisites.");
                foreach (var requirement in decision.RequiredPreparation)
                    if (requirement != SteelPreparationRequirement.ShearBucklingAssessment)
                        return new SteelCheckExecution(decision, SteelExecutionStatus.NotSupported,
                            "Preparation.NotIntegrated", "This prerequisite has no pipeline handler.");
                int[] axes = decision.Method == SteelMethod.LinearShearInteraction ? new[] { 1, 2 }
                    : decision.Target == SteelCheckTarget.Shear1 ? new[] { 1 }
                    : decision.Target == SteelCheckTarget.Shear2 ? new[] { 2 }
                    : new[] { 1, 2 }.Where(axis => (axis == 1 ? input.Actions.V1 : input.Actions.V2) != 0).ToArray();
                foreach (int axis in axes)
                    if (assessments.TryGetValue(axis, out var assessment)) used.Add(assessment);
                if(used.Any(v=>v.PanelDetails!=null&&!v.PanelDetails.Passed))
                    return new SteelCheckExecution(decision,SteelExecutionStatus.Failed,"WebPanel.DetailFailure",
                        "The actual boundary/stiffener/weld/bearing checks failed; panel resistance cannot qualify this state.",
                        shearAssessments:used);
                bool acceptsWebResistance = decision.Method == SteelMethod.EffectiveNormalResistance || decision.Method == SteelMethod.SectionShear2;
                if (used.Any(a => !a.IsBucklingExcluded && (!acceptsWebResistance || !a.BucklingResistance.HasValue)))
                    return new SteelCheckExecution(decision, SteelExecutionStatus.NotSupported,
                        "ShearBuckling.ResistanceMethodRequired",
                        "Shear buckling is not excluded. A qualified resistance/interaction method is required; plastic resistance cannot be used.",
                        shearAssessments: used);
                if (used.Count != axes.Length)
                    return new SteelCheckExecution(decision, SteelExecutionStatus.RequiresPreparation,
                        "ShearBuckling.AssessmentRequired", "Supply a shear-buckling assessment for every active shear direction of this check.",
                        shearAssessments: used);
            }

            try
            {
                if (!decision.Method.HasValue) throw new InvalidOperationException("A ready decision must name a method.");
                if (decision.Target == SteelCheckTarget.MemberStability)
                    return SteelMemberAnalysisExecution.Execute(memberAnalysis, decision);
                var section = input.Section.ReadOnlySection;
                var a = input.Actions; var s = input.Section; var code = input.Code.SectionCode;
                SectionResistanceResult result;
                switch (decision.Method.Value)
                {
                    case SteelMethod.EffectiveNormalResistance:
                        return SteelEffectiveSectionExecution.Execute(input, decision, cancellationToken, used);
                    case SteelMethod.LinearResistanceInteraction:
                    case SteelMethod.LinearShearInteraction:
                        return LinearInteraction(input, decision, section, used);
                    case SteelMethod.SectionTension:
                        result = SectionResistanceVerifier.Tension(code, a.N, s.Area, s.YieldStrength, s.UltimateStrength, s.Unperforated, s.NetArea); break;
                    case SteelMethod.SectionCompression:
                        result = SectionResistanceVerifier.Compression(code, a.N, s.Area, s.YieldStrength, decision.SectionClass); break;
                    case SteelMethod.SectionBending1:
                        result = SectionResistanceVerifier.Bending(code, a.M1, section.Wpl1, section.Wel1, s.YieldStrength, decision.SectionClass); break;
                    case SteelMethod.SectionBending2:
                        result = SectionResistanceVerifier.Bending(code, a.M2, section.Wpl2, section.Wel2, s.YieldStrength, decision.SectionClass); break;
                    case SteelMethod.SectionShear1:
                        result = Shear(input, section, 1); break;
                    case SteelMethod.SectionShear2:
                        if (used.Any(v => v.BucklingResistance.HasValue)) {
                            var web = used.Single(v => v.BucklingResistance.HasValue);
                            double utilization = Math.Abs(a.V2) / web.BucklingResistance.Value;
                            SteelInputValidation.Finite(utilization, "web utilization");
                            return new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                                reference: web.Reference, scope: web.PanelDetails==null?"H web-only shear resistance; boundary detailing assessed externally; no flange contribution or normal interaction.":"H web-only shear resistance with computed actual boundary detailing; no flange contribution or normal interaction.",
                                resistance: web.BucklingResistance, resistanceUnit: "N", utilization: utilization, trace: web.Trace, shearAssessments: used);
                        }
                        result = Shear(input, section, 2); break;
                    case SteelMethod.Ec3PlasticHInteraction:
                        return PlasticH(input, decision, section, used);
                    case SteelMethod.HMajorBendingShear:
                        return HShear(input, decision, section, used);
                    case SteelMethod.NtcElasticNormalInteraction:
                    case SteelMethod.ElasticNormalInteraction:
                        return ElasticNormal(input, decision, section, used);
                    default: throw new InvalidOperationException("Selected method has no pipeline executor.");
                }
                return new SteelCheckExecution(decision, ResistanceStatus(result.Status), result.DiagnosticCode,
                    result.Message, result.Reference, result.Scope, result.Resistance, result.ResistanceUnit,
                    result.Utilization, result.Trace, used);
            }
            catch (EffectiveSectionConvergenceException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.CalculationError, "Class4.NonConvergence", ex.Message, shearAssessments: used); }
            catch (NotSupportedException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.NotSupported, "Execution.NotSupported", ex.Message, shearAssessments: used); }
            catch (ArgumentException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.InvalidInput, "Execution.InvalidInput", ex.Message, shearAssessments: used); }
            catch (ArithmeticException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.CalculationError, "Execution.NumericalRange", ex.Message, shearAssessments: used); }
        }

        private static SteelCheckExecution LinearInteraction(SteelVerificationInput input, SteelMethodDecision decision,
            ISteelSection section, IEnumerable<SteelShearBucklingAssessment> used)
        {
            bool shearOnly = decision.Method == SteelMethod.LinearShearInteraction;
            var a = input.Actions;
            double fd = input.Section.YieldStrength / input.Code.SectionCode.GammaM0;
            double n = shearOnly ? 0 : Math.Abs(a.N) / (input.Section.Area * fd);
            double m1 = shearOnly || a.M1 == 0 ? 0 : Math.Abs(a.M1) / (section.Wel1 * fd);
            double m2 = shearOnly || a.M2 == 0 ? 0 : Math.Abs(a.M2) / (section.Wel2 * fd);
            double v1 = ShearRatio(input, section, 1), v2 = ShearRatio(input, section, 2);
            double utilization = n + m1 + m2 + v1 + v2;
            var trace = new List<SectionResistanceValue>();
            Add(trace, "NormalRatio", n, "1"); Add(trace, "ElasticMomentRatio1", m1, "1");
            Add(trace, "ElasticMomentRatio2", m2, "1"); Add(trace, "ShearRatio1", v1, "1");
            Add(trace, "ShearRatio2", v2, "1"); Add(trace, "Fy", input.Section.YieldStrength, "MPa");
            Add(trace, "GammaM0", input.Code.SectionCode.GammaM0, "1"); Add(trace, "Utilization", utilization, "1");
            string reference = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                ? "NTC 2018 4.2.4.1.2, 4.2.4.1.2.8-9; conservative resistance superposition per EN 1993-1-1:2005 6.2.1(7)"
                : "EN 1993-1-1:2005 6.2.1(7), 6.2.6, 6.2.10; conservative resistance superposition";
            return new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: reference, scope: shearOnly
                    ? "Conservative sum |V1|/V1Rd+|V2|/V2Rd; both shear-buckling exclusions required. Normal interaction is a separate check."
                    : "Conservative linear N-M1-M2-V1-V2 resistance bound, gross class 1-3, elastic bending moduli; no local stress field, torsion or member stability.",
                utilization: utilization, trace: trace, shearAssessments: used);
        }

        private static SectionResistanceResult Shear(SteelVerificationInput input, ISteelSection section, int axis)
            => SectionResistanceVerifier.Shear(input.Code.SectionCode, axis == 1 ? input.Actions.V1 : input.Actions.V2,
                axis == 1 ? ModelShearAreas.GetArea1(section) : ModelShearAreas.GetArea2(section, input.Code.SectionCode.Standard),
                input.Section.YieldStrength);

        private static double ShearRatio(SteelVerificationInput input, ISteelSection section, int axis)
        {
            if ((axis == 1 ? input.Actions.V1 : input.Actions.V2) == 0) return 0;
            var result = Shear(input, section, axis);
            if (!result.IsEvaluated || !result.Utilization.HasValue)
                throw new ArithmeticException("Cannot establish the shear ratio required by plastic H interaction.");
            return result.Utilization.Value;
        }

        private static SteelCheckExecution PlasticH(SteelVerificationInput input, SteelMethodDecision decision,
            ISteelSection section, IEnumerable<SteelShearBucklingAssessment> used)
        {
            double v1 = ShearRatio(input, section, 1), v2 = ShearRatio(input, section, 2);
            var a = input.Actions;
            var result = Ec3PlasticHSection.Evaluate((SectionH)section.SectionShape, decision.SectionClass.Value,
                input.Section.YieldStrength, input.Code.SectionCode.GammaM0, a.N, a.M1, a.M2, v1, v2, a.T);
            var trace = NormalTrace(input, decision);
            Add(trace, "ShearRatio1", v1, "1"); Add(trace, "ShearRatio2", v2, "1");
            Add(trace, "NplRd", result.NplRd, "N");
            Add(trace, "Moment1RdAtFixedN", result.Moment1Rd, "Nmm");
            Add(trace, "Moment2RdAtFixedN", result.Moment2Rd, "Nmm");
            const string scope = "Plastic H section interaction, class 1/2, low shear, no torsion; utilization scales moments at fixed N. Member stability excluded.";
            if (double.IsPositiveInfinity(result.Utilization) && Math.Abs(a.N) >= result.NplRd
                && (a.M1 != 0 || a.M2 != 0) && result.Moment1Rd == 0 && result.Moment2Rd == 0)
                return new SteelCheckExecution(decision, SteelExecutionStatus.Failed, "PlasticH.NoRemainingMomentCapacity",
                    "The axial force exhausts plastic capacity and nonzero moments remain. No finite moment utilization exists.",
                    result.Reference, scope, trace: trace, shearAssessments: used);
            Add(trace, "Utilization", result.Utilization, "1");
            return new SteelCheckExecution(decision, result.IsSatisfied ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: result.Reference, scope: scope, utilization: result.Utilization, trace: trace, shearAssessments: used);
        }

        private static SteelCheckExecution HShear(SteelVerificationInput input, SteelMethodDecision decision,
            ISteelSection section, IEnumerable<SteelShearBucklingAssessment> used)
        {
            var h = (SectionH)section.SectionShape;
            double fy = input.Section.YieldStrength, gamma = input.Code.SectionCode.GammaM0;
            double av = ModelShearAreas.GetArea2(section, input.Code.SectionCode.Standard);
            double vpl = av * fy / (Math.Sqrt(3) * gamma), ratio = Math.Abs(input.Actions.V2) / vpl;
            var trace = NormalTrace(input, decision);
            Add(trace, "V2", input.Actions.V2, "N"); Add(trace, "Av2", av, "mm2");
            Add(trace, "VplRd", vpl, "N"); Add(trace, "ShearRatio2", ratio, "1");
            string reference = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                ? "NTC 2018 4.2.4.1.2.6 equations4.2.30-32" : "EN 1993-1-1:2005 6.2.8 equations6.29-30";
            const string scope = "Symmetric H, class1/2, major-axis M1 and web V2, N=M2=V1=T=0; shear buckling excluded; no member-stability conclusion.";
            if (ratio > 1)
                return new SteelCheckExecution(decision, SteelExecutionStatus.Failed, "HighShear.PlasticShearExceeded",
                    "Shear exceeds VplRd; no admissible M-V resistance is asserted.", reference, scope, trace: trace, shearAssessments: used);
            double rho = SectionInteractionArithmetic.ShearReductionRho(input.Actions.V2, vpl);
            double aw = h.HeightWeb * h.ThicknessWeb;
            double mpl = section.Wpl1 * fy / gamma;
            double mvr = Math.Min(mpl, SectionInteractionArithmetic.MajorIMomentWithShear(section.Wpl1, rho, aw, h.ThicknessWeb, fy, gamma));
            if (!(mvr > 0) || double.IsInfinity(mvr)) throw new ArithmeticException("Nonpositive or nonfinite M-V resistance.");
            double utilization = Math.Abs(input.Actions.M1) / mvr;
            Add(trace, "Rho", rho, "1"); Add(trace, "WebArea", aw, "mm2"); Add(trace, "WebThickness", h.ThicknessWeb, "mm");
            Add(trace, "Wpl1", section.Wpl1, "mm3"); Add(trace, "MplRd", mpl, "Nmm"); Add(trace, "MvRd", mvr, "Nmm"); Add(trace, "Utilization", utilization, "1");
            return new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: reference, scope: scope, resistance: mvr, resistanceUnit: "Nmm", utilization: utilization, trace: trace, shearAssessments: used);
        }

        private static SteelCheckExecution ElasticNormal(SteelVerificationInput input, SteelMethodDecision decision,
            ISteelSection section, IEnumerable<SteelShearBucklingAssessment> used)
        {
            var a = input.Actions; var s = input.Section;
            double utilization = SectionNormalInteraction.Elastic(input.Code.SectionCode, a.N, a.M1, a.M2,
                s.Area, section.Wel1, section.Wel2, s.YieldStrength, decision.SectionClass.Value);
            var trace = NormalTrace(input, decision);
            Add(trace, "Wel1", section.Wel1, "mm3"); Add(trace, "Wel2", section.Wel2, "mm3");
            Add(trace, "Utilization", utilization, "1");
            return new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                    ? "NTC 2018 4.2.3.2; 4.2.4.1.2.8-9; conservative elastic normal bound"
                    : "EN 1993-1-1:2005 6.2.1(4), 6.2.9.2; conservative elastic normal bound",
                scope: "Conservative elastic normal-stress bound, classes 1-3; any admitted low shear needs separate assessment; torsion and member stability excluded.",
                utilization: utilization, trace: trace, shearAssessments: used);
        }

        private static List<SectionResistanceValue> NormalTrace(SteelVerificationInput input, SteelMethodDecision decision)
        {
            var trace = new List<SectionResistanceValue>(); var a = input.Actions;
            Add(trace, "N", a.N, "N"); Add(trace, "M1", a.M1, "Nmm"); Add(trace, "M2", a.M2, "Nmm");
            Add(trace, "Area", input.Section.Area, "mm2"); Add(trace, "Fy", input.Section.YieldStrength, "MPa");
            Add(trace, "GammaM0", input.Code.SectionCode.GammaM0, "1"); Add(trace, "SectionClass", decision.SectionClass.Value, "1");
            return trace;
        }

        private static void Add(ICollection<SectionResistanceValue> trace, string name, double value, string unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArithmeticException("Nonfinite result: " + name);
            trace.Add(new SectionResistanceValue(name, value, unit));
        }

        private static SteelExecutionStatus SelectionStatus(SteelSelectionStatus status)
        {
            switch (status)
            {
                case SteelSelectionStatus.NotRequired: return SteelExecutionStatus.NotRequired;
                case SteelSelectionStatus.MissingInput: return SteelExecutionStatus.MissingInput;
                case SteelSelectionStatus.NotSupported: return SteelExecutionStatus.NotSupported;
                case SteelSelectionStatus.InvalidInput: return SteelExecutionStatus.InvalidInput;
                default: throw new InvalidOperationException("Selection status requires execution or preparation.");
            }
        }

        private static SteelExecutionStatus ResistanceStatus(SectionResistanceStatus status)
        {
            switch (status)
            {
                case SectionResistanceStatus.Passed: return SteelExecutionStatus.Passed;
                case SectionResistanceStatus.Failed: return SteelExecutionStatus.Failed;
                case SectionResistanceStatus.NotSupported: return SteelExecutionStatus.NotSupported;
                case SectionResistanceStatus.MissingInput: return SteelExecutionStatus.MissingInput;
                case SectionResistanceStatus.InvalidInput: return SteelExecutionStatus.InvalidInput;
                case SectionResistanceStatus.CalculationError: return SteelExecutionStatus.CalculationError;
                default: throw new InvalidOperationException("Unknown resistance status.");
            }
        }
    }
}
