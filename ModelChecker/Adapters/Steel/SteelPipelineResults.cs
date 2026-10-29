using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>Uses existing persistent report contracts. Unavailable components retain
    /// status/method/reason in trace and diagnostics, evaluated components retain metrics.
    /// Model's report contract requires an incomplete row to be NotEvaluated even if
    /// one component failed; that failure remains explicit in its metric.</summary>
    internal static class SteelPipelineResults
    {
        internal static CheckResult Map(SteelVerificationResult native, string inputFingerprint, string methodId = SteelPipelineMaterialChecker.EngineId, string scope = SteelPipelineMaterialChecker.Limitations)
        {
            var metrics = new List<CheckMetric>(); var diagnostics = new List<ModelDiagnostic>();
            var trace = new List<CheckCalculationValue> {
                new CheckCalculationValue("Pipeline.IsComplete", native.IsComplete ? 1 : 0, "1", "Requested section inventory complete"),
                new CheckCalculationValue("Pipeline.Outcome", (int)native.Outcome, "1", native.Outcome.ToString()),
                new CheckCalculationValue("Section.PrincipalAxisAngle", native.Plan.Input.Section.PrincipalAxisAngle, "rad", "Angle from section geometry X to principal axis 1")
            };
            if (native.Plan.MemberAnalysis != null)
            {
                var analysis = native.Plan.MemberAnalysis;
                trace.Add(new CheckCalculationValue("Member.AnalysisKind", null, null, analysis.Kind.ToString()));
                trace.Add(new CheckCalculationValue("Member.LoadPattern", null, null, analysis.LoadPatternReference));
                trace.Add(new CheckCalculationValue("Member.ParameterSet", null, null, analysis.ParameterSet));
                trace.Add(new CheckCalculationValue("Member.ClassificationSource", null, null, analysis.ClassificationSource));
                if(analysis.EffectiveSectionAnalysisSource!=null)
                    trace.Add(new CheckCalculationValue("Member.EffectiveSectionAnalysisSource",null,null,analysis.EffectiveSectionAnalysisSource));
                if(analysis.PackedPair!=null)
                {
                    trace.Add(new CheckCalculationValue("Member.PackedPair.DetailingSource",null,null,analysis.PackedPair.DetailingSource));
                    trace.Add(new CheckCalculationValue("Member.PackedPair.LocalAnalysisSource",null,null,analysis.PackedPair.LocalAnalysisSource));
                    trace.Add(new CheckCalculationValue("Member.PackedPair.ResistanceSource",null,null,analysis.PackedPair.ResistanceSource));
                    var chord=analysis.PackedPair.ChordCalculation;
                    if(chord!=null) {
                        trace.Add(new CheckCalculationValue("Member.PackedPair.ChordModelSource",null,null,chord.ModelSource));
                        trace.Add(new CheckCalculationValue("Member.PackedPair.ChordCurves",null,null,chord.Analysis.ParameterSet));
                        foreach(var v in chord.Analysis.Values)
                            trace.Add(new CheckCalculationValue("Member.PackedPair.ChordCritical."+v.Quantity,v.Value,v.Unit,v.Source));
                    }
                }
                if (analysis.BeamColumnParameters != null)
                {
                    var p=analysis.BeamColumnParameters;
                    trace.Add(new CheckCalculationValue("Member.CompressionCurve1",null,null,p.Curve1.ToString()));
                    trace.Add(new CheckCalculationValue("Member.CompressionCurve2",null,null,p.Curve2.ToString()));
                    trace.Add(new CheckCalculationValue("Member.TorsionSusceptible",p.TorsionSusceptible?1:0,"1",p.LateralTorsionalExclusionSource));
                    trace.Add(new CheckCalculationValue("Member.MomentFactorSource",null,null,p.MomentFactorSource));
                    trace.Add(new CheckCalculationValue("Member.StabilityModelSource",null,null,p.StabilityModelSource));
                    trace.Add(new CheckCalculationValue("Member.TorsionalExclusionSource",null,null,p.TorsionalExclusionSource));
                    trace.Add(new CheckCalculationValue("Member.FlexuralTorsionalExclusionSource",null,null,p.FlexuralTorsionalExclusionSource));
                    trace.Add(new CheckCalculationValue("Member.ShearBucklingExclusionSource",null,null,p.ShearBucklingExclusionSource));
                    trace.Add(new CheckCalculationValue("Member.McrBasis",null,null,p.CriticalMomentBasis.ToString()));
                }
                else if (analysis.Prismatic != null)
                {
                    if (analysis.Kind == GPC.Checkers.Steel.Inputs.SteelMemberAnalysisKind.PrismaticCompression)
                    {
                        trace.Add(new CheckCalculationValue("Member.CompressionCurve1", null, null, analysis.Prismatic.Curve1.ToString()));
                        trace.Add(new CheckCalculationValue("Member.CompressionCurve2", null, null, analysis.Prismatic.Curve2.ToString()));
                        trace.Add(new CheckCalculationValue("Member.TorsionalCurve", null, null, analysis.Prismatic.TorsionalCurve.ToString()));
                        trace.Add(new CheckCalculationValue("Member.TorsionalExclusionSource", null, null, analysis.Prismatic.TorsionalExclusionSource));
                        trace.Add(new CheckCalculationValue("Member.FlexuralTorsionalExclusionSource", null, null, analysis.Prismatic.FlexuralTorsionalExclusionSource));
                    }
                    else trace.Add(new CheckCalculationValue("Member.McrBasis", null, null, analysis.Prismatic.CriticalMomentBasis.ToString()));
                }
                else
                {
                    trace.Add(new CheckCalculationValue("Member.LateralCurve", null, null, analysis.LateralCurve.ToString()));
                    trace.Add(new CheckCalculationValue("Member.LateralTorsionalCurve", null, null, analysis.LateralTorsionalCurve.ToString()));
                    if (analysis.Kind == GPC.Checkers.Steel.Inputs.SteelMemberAnalysisKind.UniformForkHOrT)
                        trace.Add(new CheckCalculationValue("Member.InPlaneCurve", null, null, analysis.InPlaneCurve.ToString()));
                }
                var ltb = analysis.LateralTorsionalMethod; // rows only for an explicitly selected non-general LTB method
                if (ltb.Kind != GPC.Checkers.Steel.Inputs.SteelLateralTorsionalMethodKind.General)
                {
                    trace.Add(new CheckCalculationValue("Member.LtbMethod", null, null, ltb.Kind + "; " + ltb.ParameterSource));
                    trace.Add(new CheckCalculationValue("Member.LtbKc", ltb.MomentFactor.Kc, "1", ltb.MomentFactor.Distribution + "; " + ltb.MomentFactor.Source));
                }
                // EN 1993-1-1 options, rows only when declared.
                if (ltb.DeclaredCurve.HasValue)
                    trace.Add(new CheckCalculationValue("Member.LtbCurve", null, null, ltb.DeclaredCurve.Value + "; " + ltb.DeclaredCurveSource));
                if (ltb.LowMomentExemption)
                    trace.Add(new CheckCalculationValue("Member.LtbLowMomentExemption", ltb.LambdaLT0 * ltb.LambdaLT0, "1",
                        "EN 1993-1-1:2005 6.3.2.2(4), limit lambdaLT,0^2 of MEd/Mcr; " + ltb.LowMomentExemptionSource));
                foreach (var v in analysis.Values)
                {
                    trace.Add(new CheckCalculationValue("Member.External." + v.Quantity, v.Value, v.Unit, v.Source));
                    trace.Add(new CheckCalculationValue("Member.External." + v.Quantity + ".Original", v.OriginalValue, v.OriginalUnit.ToString(), v.Source));
                }
            }
            var a = native.Plan.Input.Actions;
            foreach (var value in new[] { ("N", a.N, "N"), ("V1", a.V1, "N"), ("V2", a.V2, "N"),
                ("T", a.T, "Nmm"), ("M1", a.M1, "Nmm"), ("M2", a.M2, "Nmm") })
                trace.Add(new CheckCalculationValue("Input." + value.Item1, value.Item2, value.Item3, native.Plan.MemberAnalysis?.BeamColumnParameters != null ? "Whole-member maxima from ONE design state; section concomitance is preserved separately; N positive in tension" : "Concomitant principal-axis actions; N positive in tension"));

            foreach (var check in native.Checks)
            {
                string prefix = check.Decision.Target.ToString();
                trace.Add(new CheckCalculationValue(prefix + ".Status", (int)check.Status, "1", check.Status.ToString()));
                trace.Add(new CheckCalculationValue(prefix + ".Method", null, null, check.Decision.Method?.ToString() ?? "Not selected"));
                if (check.Decision.SectionClass.HasValue)
                    trace.Add(new CheckCalculationValue(prefix + ".Classification", check.Decision.SectionClass, "1", check.Decision.ClassificationSource));
                if (check.Scope != null) trace.Add(new CheckCalculationValue(prefix + ".Scope", null, null, check.Scope));
                if (check.Decision.MissingInputs.Count != 0)
                    trace.Add(new CheckCalculationValue(prefix + ".MissingInputs", null, null, string.Join(", ", check.Decision.MissingInputs)));
                foreach (var value in check.Trace)
                    trace.Add(new CheckCalculationValue(prefix + "." + value.Name, value.Value, value.Unit, check.Reference));
                foreach (var assessment in check.ShearAssessments)
                {
                    string evidence = prefix + ".ShearAssessment" + assessment.Axis;
                    trace.Add(new CheckCalculationValue(evidence + ".Excluded", assessment.IsBucklingExcluded ? 1 : 0, "1", assessment.Source));
                    foreach (var value in assessment.Trace)
                        trace.Add(new CheckCalculationValue(evidence + "." + value.Name, value.Value, value.Unit, assessment.Reference));
                }
                if (check.DiagnosticCode != null && check.Status != SteelExecutionStatus.NotRequired)
                    diagnostics.Add(ModelDiagnostic.Error("SteelPipeline." + prefix + "." + check.DiagnosticCode, message: check.Message));
                if (check.IsEvaluated)
                {
                    double? demand = check.Decision.Method == SteelMethod.SectionTension || check.Decision.Method == SteelMethod.SectionCompression || check.Decision.Method == SteelMethod.PrismaticCompression ? a.N
                        : check.Decision.Method == SteelMethod.SectionBending1 || check.Decision.Method == SteelMethod.PrismaticLateralTorsionalBuckling || check.Decision.Method == SteelMethod.HMajorBendingShear ? a.M1
                        : check.Decision.Method == SteelMethod.SectionBending2 ? a.M2
                        : check.Decision.Method == SteelMethod.SectionShear1 ? a.V1
                        : check.Decision.Method == SteelMethod.SectionShear2 ? (double?)a.V2 : null;
                    metrics.Add(new CheckMetric(prefix, demand, check.Resistance, check.ResistanceUnit ?? "1",
                        check.Utilization, check.Status == SteelExecutionStatus.Passed, check.Reference));
                }
            }
            // Model keeps an incomplete row NotEvaluated (CheckResultRules.ValidDecision);
            // a failure already evaluated inside it must still reach the report and filters.
            if (!native.IsComplete && native.Outcome == EngineeringOutcome.NotSatisfied)
                diagnostics.Add(ModelDiagnostic.Error("SteelPipeline.KnownFailureInIncompleteAssessment", message: "Failed before completion: "
                    + string.Join(", ", native.Checks.Where(c => c.Status == SteelExecutionStatus.Failed).Select(c => c.Decision.Target.ToString()))));
            // Different interactions use different utilization definitions. Preserve only
            // a single evaluated method's ratio as the row ratio; never take a global max.
            var result = new CheckResult {
                Details = new NativeMethodDetails(methodId, scope,
                    inputFingerprint, metrics, trace, new[] {
                        "Requested scope: " + native.Plan.Scope + ". Each metric retains its own utilization definition; EC3 plastic H scales moments at fixed N.",
                        "External classifications and shear assessments are caller declarations bound to the section fingerprint, not independent certification."
                    }, null, null, methodId + ":per-method-utilization"),
                Diagnostics = diagnostics,
                Execution = ExecutionStatus.Completed,
                Data = DataStatus.Ready,
                Outcome = native.IsComplete ? native.Outcome : EngineeringOutcome.NotEvaluated,
                Utilization = native.IsComplete && metrics.Count == 1 ? metrics[0].Utilization : null
            };
            if (native.Checks.Any(c => c.Status == SteelExecutionStatus.CalculationError))
                result.Execution = ExecutionStatus.Error;
            else if (native.Checks.Any(c => c.Status == SteelExecutionStatus.MissingInput
                || c.Status == SteelExecutionStatus.InvalidInput || c.Status == SteelExecutionStatus.RequiresPreparation))
                result.Data = DataStatus.Insufficient;
            else if (native.Checks.Any(c => c.Status == SteelExecutionStatus.NotSupported))
                result.Data = DataStatus.NotSupported;
            // A member mechanism the code does not require for this pattern (e.g. minor-axis bending alone,
            // no compression) is not applicable, not unevaluated: Model counts it as resolved, never as satisfied.
            // Section cuts keep NotEvaluated: a zero cut implies no member-wide conclusion.
            if (native.Plan.MemberAnalysis != null && native.Checks.Count != 0 && native.Checks.All(c => c.Status == SteelExecutionStatus.NotRequired))
            {
                result.Applicability = CheckApplicability.NotApplicable; result.Execution = ExecutionStatus.NotExecuted;
                result.ApplicabilityReason = string.Join("; ", native.Checks.Select(c => c.Decision.Target + " " + c.DiagnosticCode + ": " + c.Message));
            }
            return result;
        }
    }
}
