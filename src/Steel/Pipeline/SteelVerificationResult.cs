using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Checking.Contracts;

namespace GPC.Checkers.Steel.Pipeline
{
    public enum SteelExecutionStatus
    {
        Passed, Failed, NotRequired, RequiresPreparation, MissingInput,
        NotSupported, InvalidInput, CalculationError
    }

    /// <summary>One selected check, including unavailable checks. Resistance/Utilization
    /// are finite and present only when evaluated; Trace can retain finite inputs on error.
    /// Interaction checks have no scalar Resistance.
    /// EC3 plastic H utilization scales moments at FIXED N, not the whole action vector.
    /// Exhausted axial capacity with nonzero moments is Failed with null Utilization;
    /// its diagnostic and finite capacity trace replace the native positive infinity.</summary>
    public sealed class SteelCheckExecution
    {
        public SteelMethodDecision Decision { get; }
        public SteelExecutionStatus Status { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }
        public string Reference { get; }
        public string Scope { get; }
        public double? Resistance { get; }
        public string ResistanceUnit { get; }
        public double? Utilization { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<SteelShearBucklingAssessment> ShearAssessments { get; }
        public bool IsEvaluated => Status == SteelExecutionStatus.Passed || Status == SteelExecutionStatus.Failed;

        internal SteelCheckExecution(SteelMethodDecision decision, SteelExecutionStatus status,
            string diagnostic = null, string message = null, string reference = null, string scope = null,
            double? resistance = null, string resistanceUnit = null, double? utilization = null,
            IEnumerable<SectionResistanceValue> trace = null,
            IEnumerable<SteelShearBucklingAssessment> shearAssessments = null)
        {
            Decision = decision; Status = status; DiagnosticCode = diagnostic; Message = message;
            Reference = reference; Scope = scope; Resistance = resistance; ResistanceUnit = resistanceUnit;
            Utilization = utilization;
            Trace = Array.AsReadOnly((trace ?? Enumerable.Empty<SectionResistanceValue>()).ToArray());
            ShearAssessments = Array.AsReadOnly((shearAssessments ?? Enumerable.Empty<SteelShearBucklingAssessment>()).ToArray());
        }

        internal SteelCheckExecution WithTrace(IEnumerable<SectionResistanceValue> extra)
            => new SteelCheckExecution(Decision, Status, DiagnosticCode, Message, Reference, Scope,
                Resistance, ResistanceUnit, Utilization, Trace.Concat(extra), ShearAssessments);
    }

    /// <summary>Outcome for the requested plan scope only. A failure remains a failure
    /// even if another check is unavailable; IsComplete distinguishes that partial result.
    /// Satisfied requires at least one evaluated check and no unresolved checks. An empty
    /// load case is NotEvaluated. No maximum utilization combines different load paths.</summary>
    public sealed class SteelVerificationResult
    {
        public SteelSelectionPlan Plan { get; }
        public IReadOnlyList<SteelCheckExecution> Checks { get; }
        public bool IsComplete { get; }
        public EngineeringOutcome Outcome { get; }

        internal SteelVerificationResult(SteelSelectionPlan plan, IEnumerable<SteelCheckExecution> checks)
        {
            Plan = plan; Checks = Array.AsReadOnly(checks.ToArray());
            IsComplete = Checks.Any(c => c.IsEvaluated)
                && Checks.All(c => c.IsEvaluated || c.Status == SteelExecutionStatus.NotRequired);
            Outcome = Checks.Any(c => c.Status == SteelExecutionStatus.Failed) ? EngineeringOutcome.NotSatisfied
                : IsComplete ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotEvaluated;
        }
    }
}
