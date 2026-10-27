using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Pipeline;
using GPC.Model.Checking.Contracts;

namespace GPC.Checkers.Steel.Reporting
{
    /// <summary>Common presentation of an existing assessment, within its declared scope.
    /// No recalculation, new qualification, utilization envelope or interpretation of
    /// hidden partial component checks. Failure and completeness are independent.
    /// A failed linked member is reported separately from a local connection verdict.</summary>
    public sealed class SteelAssessmentSummary
    {
        public string Scope { get; }
        public EngineeringOutcome Outcome { get; }
        public bool IsComplete { get; }
        public bool HasKnownFailure { get; }
        public bool HasFailedLinkedMember { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public IReadOnlyList<string> ExternalConditions { get; }

        private SteelAssessmentSummary(string scope, bool complete, bool failed, IEnumerable<string> missing,
            IEnumerable<string> conditions = null, bool failedLinkedMember = false)
        {
            Scope = scope;
            var issues = missing.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (!complete && issues.Count == 0) issues.Add("ASSESSMENT_INCOMPLETE_WITHIN_DECLARED_SCOPE");
            MissingRequirements = Array.AsReadOnly(issues.ToArray());
            IsComplete = complete && issues.Count == 0;
            HasKnownFailure = failed; HasFailedLinkedMember = failedLinkedMember;
            Outcome = failed ? EngineeringOutcome.NotSatisfied : IsComplete ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotEvaluated;
            ExternalConditions = Array.AsReadOnly((conditions ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray());
        }

        public static SteelAssessmentSummary From(SteelVerificationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new SteelAssessmentSummary(result.Plan.Scope.ToString(), result.IsComplete,
                result.Checks.Any(c => c.Status == SteelExecutionStatus.Failed),
                result.Checks.Where(c => !c.IsEvaluated && c.Status != SteelExecutionStatus.NotRequired)
                    .Select(c => c.Decision.Target + ":" + c.Status + ":" + c.DiagnosticCode));
        }

        public static SteelAssessmentSummary From<T>(ConnectionVerification<T> result) where T : class
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            // Favourable partial checks stay suppressed; failed evaluated checks are retained.
            // Only published, evaluated resistance checks are evidence of a known failure.
            return new SteelAssessmentSummary(result.Scope, result.IsComplete,
                result.Checks.Any(c => !c.Passed), result.Diagnostics, result.Assumptions);
        }

        public static SteelAssessmentSummary From(WeldedIBeamSpliceStandardResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new SteelAssessmentSummary(result.Scope, result.IsStaticResistanceComplete,
                result.HasFailedComponent, result.MissingRequirements,
                new[] { "Declared execution and structural continuity/second-order analysis remain conditions of this local static result. No fatigue or whole-member qualification." },
                result.HasFailedLinkedMember);
        }

        public static SteelAssessmentSummary From(BridgeSpliceReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return new SteelAssessmentSummary(report.Scope, report.IsComplete, report.HasKnownFailure,
                report.MissingRequirements, new[] { "Local static scope, conditional on the declared execution and global model. Fatigue, rotational stiffness and whole-member qualification remain separate." }, report.HasFailedLinkedMember);
        }

        public static SteelAssessmentSummary From(StandardConnectionCaseReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return new SteelAssessmentSummary(report.Scope, report.IsComplete,
                report.Status == ConnectionStatus.Failed || report.Checks.Any(c => !c.Passed),
                report.Diagnostics, report.ExternalConditions);
        }
    }
}
