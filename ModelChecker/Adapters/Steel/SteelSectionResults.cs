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

        internal static CheckResult ClassificationFailure(UniformCompressionClassification result)
        {
            if (result.IsEvaluated) return null;
            var failure = NativeResults.Missing("SteelClassification." + result.DiagnosticCode);
            if (result.Status == SectionClassificationStatus.CalculationError)
            {
                failure.Data = DataStatus.Ready;
                failure.Execution = ExecutionStatus.Error;
            }
            return failure;
        }

        internal static void AddClassificationTrace(List<CheckCalculationValue> trace, UniformCompressionClassification result)
        {
            trace.Add(new CheckCalculationValue("CompressionClass", result.NominalClass, "1", "Conservative uniform-compression stockiness gate"));
            foreach (var part in result.Parts)
            {
                var c = part.Classification; string key = "Classification." + part.Id + ".";
                trace.Add(new CheckCalculationValue(key + "ClearWidth", c.WidthOrDiameter, "mm", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Thickness", c.Thickness, "mm", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Ratio", c.WidthThicknessRatio, "1", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Class", c.NominalClass, "1", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Class1Limit", c.Class1Limit, "1", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Class2Limit", c.Class2Limit, "1", c.Reference));
                trace.Add(new CheckCalculationValue(key + "Class3Limit", c.Class3Limit, "1", c.Reference));
            }
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
