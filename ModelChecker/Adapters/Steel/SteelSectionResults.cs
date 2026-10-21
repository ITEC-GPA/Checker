using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>Boundary between scalar Steel outcomes and Model report contracts.</summary>
    internal static class SteelSectionResults
    {
        internal static CheckResult Failure(string axis, SectionResistanceResult result)
        {
            if (result.IsEvaluated) return null;
            var failure = new CheckResult {
                Outcome = EngineeringOutcome.NotEvaluated,
                Diagnostics = new List<ModelDiagnostic> {
                    ModelDiagnostic.Error("SteelSection." + axis + "." + result.DiagnosticCode, message: result.Message)
                }
            };
            switch (result.Status)
            {
                case SectionResistanceStatus.NotSupported:
                    failure.Data = DataStatus.NotSupported;
                    break;
                case SectionResistanceStatus.MissingInput:
                case SectionResistanceStatus.InvalidInput:
                    failure.Data = DataStatus.Insufficient;
                    break;
                case SectionResistanceStatus.CalculationError:
                    failure.Data = DataStatus.Ready;
                    failure.Execution = ExecutionStatus.Error;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(result));
            }
            return failure;
        }

        internal static CheckMetric Metric(string axis, double demand, SectionResistanceResult result)
        {
            if (!result.IsEvaluated) throw new ArgumentException("An evaluated section result is required.", nameof(result));
            return new CheckMetric(axis, demand, result.Resistance, result.ResistanceUnit, result.Utilization,
                result.Status == SectionResistanceStatus.Passed, result.Reference);
        }

        internal static void AddTrace(List<CheckCalculationValue> trace, string axis, SectionResistanceResult result)
        {
            foreach (var value in result.Trace)
                trace.Add(new CheckCalculationValue(axis + "." + value.Name, value.Value, value.Unit, result.Reference));
        }
    }
}
