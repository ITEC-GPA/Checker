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
        internal static CheckResult Map(SteelVerificationResult native, string inputFingerprint)
        {
            var metrics = new List<CheckMetric>(); var diagnostics = new List<ModelDiagnostic>();
            var trace = new List<CheckCalculationValue> {
                new CheckCalculationValue("Pipeline.IsComplete", native.IsComplete ? 1 : 0, "1", "Requested section inventory complete"),
                new CheckCalculationValue("Pipeline.Outcome", (int)native.Outcome, "1", native.Outcome.ToString()),
                new CheckCalculationValue("Section.PrincipalAxisAngle", native.Plan.Input.Section.PrincipalAxisAngle, "rad", "Angle from section geometry X to principal axis 1")
            };
            var a = native.Plan.Input.Actions;
            foreach (var value in new[] { ("N", a.N, "N"), ("V1", a.V1, "N"), ("V2", a.V2, "N"),
                ("T", a.T, "Nmm"), ("M1", a.M1, "Nmm"), ("M2", a.M2, "Nmm") })
                trace.Add(new CheckCalculationValue("Input." + value.Item1, value.Item2, value.Item3, "Concomitant principal-axis actions; N positive in tension"));

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
                    double? demand = check.Decision.Method == SteelMethod.SectionTension || check.Decision.Method == SteelMethod.SectionCompression ? a.N
                        : check.Decision.Method == SteelMethod.SectionBending1 ? a.M1
                        : check.Decision.Method == SteelMethod.SectionBending2 ? a.M2
                        : check.Decision.Method == SteelMethod.SectionShear1 ? a.V1
                        : check.Decision.Method == SteelMethod.SectionShear2 ? (double?)a.V2 : null;
                    metrics.Add(new CheckMetric(prefix, demand, check.Resistance, check.ResistanceUnit ?? "1",
                        check.Utilization, check.Status == SteelExecutionStatus.Passed, check.Reference));
                }
            }
            // Different interactions use different utilization definitions. Preserve only
            // a single evaluated method's ratio as the row ratio; never take a global max.
            var result = new CheckResult {
                Details = new NativeMethodDetails(SteelPipelineMaterialChecker.EngineId, SteelPipelineMaterialChecker.Limitations,
                    inputFingerprint, metrics, trace, new[] {
                        "Section scope only. Each metric retains its own method and utilization definition; EC3 plastic H scales moments at fixed N.",
                        "External classifications and shear assessments are caller declarations bound to the section fingerprint, not independent certification."
                    }, null, null, "Steel.Pipeline.Section:per-method-utilization"),
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
            return result;
        }
    }
}
