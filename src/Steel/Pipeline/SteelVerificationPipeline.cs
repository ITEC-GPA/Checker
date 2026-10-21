using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Inputs;
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
                checks.Add(ExecuteOne(plan.Input, decision, assessments, plan.MemberAnalysis));
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new SteelVerificationResult(plan, checks);
        }

        private static SteelCheckExecution ExecuteOne(SteelVerificationInput input, SteelMethodDecision decision,
            IDictionary<int, SteelShearBucklingAssessment> assessments, SteelMemberAnalysis memberAnalysis)
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
                int[] axes = decision.Target == SteelCheckTarget.Shear1 ? new[] { 1 }
                    : decision.Target == SteelCheckTarget.Shear2 ? new[] { 2 }
                    : new[] { 1, 2 }.Where(axis => (axis == 1 ? input.Actions.V1 : input.Actions.V2) != 0).ToArray();
                foreach (int axis in axes)
                    if (assessments.TryGetValue(axis, out var assessment)) used.Add(assessment);
                if (used.Any(a => !a.IsBucklingExcluded))
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
                var section = input.Section.CreateSectionCopy();
                var a = input.Actions; var s = input.Section; var code = input.Code.SectionCode;
                SectionResistanceResult result;
                switch (decision.Method.Value)
                {
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
                        result = Shear(input, section, 2); break;
                    case SteelMethod.Ec3PlasticHInteraction:
                        return PlasticH(input, decision, section, used);
                    case SteelMethod.NtcElasticNormalInteraction:
                    case SteelMethod.ElasticNormalInteraction:
                        return ElasticNormal(input, decision, section, used);
                    default: throw new InvalidOperationException("Selected method has no pipeline executor.");
                }
                return new SteelCheckExecution(decision, ResistanceStatus(result.Status), result.DiagnosticCode,
                    result.Message, result.Reference, result.Scope, result.Resistance, result.ResistanceUnit,
                    result.Utilization, result.Trace, used);
            }
            catch (NotSupportedException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.NotSupported, "Execution.NotSupported", ex.Message, shearAssessments: used); }
            catch (ArgumentException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.InvalidInput, "Execution.InvalidInput", ex.Message, shearAssessments: used); }
            catch (ArithmeticException ex)
            { return new SteelCheckExecution(decision, SteelExecutionStatus.CalculationError, "Execution.NumericalRange", ex.Message, shearAssessments: used); }
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
