using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Serviceability;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>Serviceability stress result in the existing report contracts: ServiceabilityCheckDetails with one
    /// metric per criterion (NormalStress, ShearStress, EquivalentStress; stress/limit with the same definition, so
    /// the row utilization is their maximum), the governing points and the full trace with sources and locations.
    /// An unavailable result keeps status, code and trace; the reason is an error diagnostic, except NotRequired (an
    /// unloaded cut), which is a not applicable row with the diagnostic as its ApplicabilityReason.</summary>
    internal static class SteelServiceStressResults
    {
        /// <summary>Points are in the section geometry axes X/Y through the gross centroid.</summary>
        internal const string Frame = "SectionGeometryCentroidal";

        internal static CheckResult Map(SteelServiceStressResult native, CombinationCategory category)
        {
            var c = CultureInfo.InvariantCulture;
            var trace = new List<CheckCalculationValue>
            {
                new CheckCalculationValue("ServiceStress.Status", (int)native.Status, "1", native.Status.ToString()),
                new CheckCalculationValue("ServiceStress.Reference", null, null, native.Reference),
                new CheckCalculationValue("ServiceStress.Scope", null, null, native.Scope)
            };
            if (native.DiagnosticCode != null)
                trace.Add(new CheckCalculationValue("ServiceStress.Diagnostic", null, null, native.DiagnosticCode + ": " + native.Message));
            if (native.Basis != null)
            {
                trace.Add(new CheckCalculationValue("ServiceStress.GammaMSerSource", null, null, native.Basis.GammaMSerSource));
                trace.Add(new CheckCalculationValue("ServiceStress.ApplicabilitySource", null, null, native.Basis.ApplicabilitySource));
            }
            if (native.Family != null) trace.Add(new CheckCalculationValue("ServiceStress.Family", null, null, native.Family));
            if (native.ShearModel != null) trace.Add(new CheckCalculationValue("ServiceStress.ShearModel", null, null, native.ShearModel));
            if (native.StressModel != null) trace.Add(new CheckCalculationValue("ServiceStress.StressModel", null, null, native.StressModel));
            if (native.ClassificationSource != null)
                trace.Add(new CheckCalculationValue("ServiceStress.Classification", native.SectionClass, "1", native.ClassificationSource));
            foreach (var v in native.Trace)
                trace.Add(new CheckCalculationValue("ServiceStress." + v.Name, v.Value, v.Unit, null));
            var metrics = new List<CheckMetric>(); var points = new List<CheckPointValue>();
            foreach (var v in native.Values)
            {
                string key = v.Criterion + "Stress";
                metrics.Add(new CheckMetric(key, v.Stress, v.Limit, "MPa", v.Utilization, v.Passed, v.Reference));
                points.Add(new CheckPointValue(key, v.X, v.Y, 0, v.Stress, "MPa", Frame));
                trace.Add(new CheckCalculationValue("ServiceStress." + v.Criterion + ".Location", null, null,
                    v.Location + " (" + v.X.ToString("0.###", c) + ", " + v.Y.ToString("0.###", c) + ") mm"));
            }
            var details = new ServiceabilityCheckDetails(SteelServiceStressCheck.MethodId, "stress/limit", category.ToString(), metrics, trace, points);
            var result = new CheckResult { Details = details, Execution = ExecutionStatus.Completed, Data = DataStatus.Ready,
                Diagnostics = new List<ModelDiagnostic>() };
            if (native.IsEvaluated)
            {
                result.Utilization = native.Utilization;
                result.Outcome = native.Status == SteelExecutionStatus.Passed ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
                return result;
            }
            result.Outcome = EngineeringOutcome.NotEvaluated;
            // An unloaded cut (every effective action zero, ServiceStress.NoActions) has no stress to limit: the row is not
            // applicable, which Model's report counts as resolved and never as satisfied. Any non-zero action is evaluated.
            if (native.Status == SteelExecutionStatus.NotRequired)
            {
                result.Applicability = CheckApplicability.NotApplicable; result.Execution = ExecutionStatus.NotExecuted;
                var neglected = native.Input.NeglectedActions;
                result.ApplicabilityReason = native.DiagnosticCode + ": " + native.Message + (neglected.Count == 0 ? ""
                    : " Negligible components counted as zero: " + string.Join(", ", neglected.Select(n => n.Component + "="
                        + n.Value.ToString("R", c) + " " + n.Unit + " <= " + n.Limit.ToString("R", c))) + ".");
                return result;
            }
            result.Diagnostics.Add(ModelDiagnostic.Error("SteelPipeline." + native.DiagnosticCode, message: native.Message));
            if (native.Status == SteelExecutionStatus.CalculationError) result.Execution = ExecutionStatus.Error;
            else if (native.Status == SteelExecutionStatus.MissingInput || native.Status == SteelExecutionStatus.InvalidInput) result.Data = DataStatus.Insufficient;
            else if (native.Status == SteelExecutionStatus.NotSupported) result.Data = DataStatus.NotSupported;
            return result;
        }
    }
}
