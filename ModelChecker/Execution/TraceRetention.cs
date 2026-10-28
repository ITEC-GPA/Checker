using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    /// <summary>Calculation traces kept in the rows of a beam-material run (MultiMaterialCheckRequest and
    /// the beam jobs of a VerificationConfiguration).</summary>
    public enum CheckTraceRetention
    {
        /// <summary>Every row keeps its complete trace (default).</summary>
        Full = 0,
        /// <summary>A native-method row keeps its complete trace when it is not Satisfied, or when it governs
        /// a metric of its target (beam element or physical member) over all the jobs of the run. The other
        /// rows keep outcome, utilization, metrics, warnings and evidence, and one Trace.Omitted entry instead
        /// of the trace. The trace of such a row is obtained on request by running the same request again,
        /// restricted to that target if useful, with Full: the engines are deterministic.</summary>
        GoverningAndFailed = 1
    }

    public sealed partial class ModelChecker
    {
        /// <summary>Opt-in trace compaction for the beam-material runs; Full by default. Not persisted:
        /// a run setting, it changes neither outcomes nor utilizations, metrics or scope fingerprints.</summary>
        public CheckTraceRetention TraceRetention { get; set; }
    }

    internal static class TraceCompaction
    {
        internal const string OmittedKey = "Trace.Omitted";

        /// <summary>Governing: the largest utilization of the row, or of one of its metrics (by metric key),
        /// among the Satisfied native rows of the same target and comparison identity (that of
        /// CheckReportViews.Governing); the first row in report order wins a tie.</summary>
        internal static void Apply(IEnumerable<CheckResult> rows)
        {
            var candidates = rows.Where(r => r != null && r.Outcome == EngineeringOutcome.Satisfied && r.Details is NativeMethodDetails).ToArray();
            var keep = new HashSet<CheckResult>(ReferenceComparer.Instance);
            foreach (var group in candidates.GroupBy(Identity))
            {
                var rated = group.Where(r => r.Utilization.HasValue).ToArray();
                if (rated.Length > 0) keep.Add(rated.OrderByDescending(r => r.Utilization.Value).First());
                foreach (var metric in group.SelectMany(r => r.Details.Metrics.Where(m => m.Utilization.HasValue).Select(m => new { Row = r, m.Key, Value = m.Utilization.Value }))
                    .GroupBy(m => m.Key))
                    keep.Add(metric.OrderByDescending(m => m.Value).First().Row);
            }
            foreach (var row in candidates.Where(r => !keep.Contains(r)))
            {
                var d = (NativeMethodDetails)row.Details;
                row.Details = new NativeMethodDetails(d.MethodId, d.ScopeDescription, d.NativeInputFingerprint, d.Metrics,
                    new[] { new CheckCalculationValue(OmittedKey, d.Trace.Count, "1",
                        "Trace kept only for governing and not satisfied rows (CheckTraceRetention.GoverningAndFailed); run again with Full to obtain it.") },
                    d.Warnings, d.Points, d.Convergence, d.UtilizationDefinition);
                row.SealEvidence();
            }
        }

        private static string Identity(CheckResult r) => string.Join("\n", new[] {
            r.Target.Kind.ToString(), r.Target.BeamId?.ToString(CultureInfo.InvariantCulture) ?? r.Target.MemberId,
            r.Mechanism.ToString(), CheckReportViews.StandardKey(r), r.Details.MethodId, r.Details.UtilizationDefinition,
            r.EngineVersion, r.EngineConfiguration, r.Settings, r.Scope.ToString(), r.MethodId, r.Check?.Key });

        private sealed class ReferenceComparer : IEqualityComparer<CheckResult>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(CheckResult x, CheckResult y) => ReferenceEquals(x, y);
            public int GetHashCode(CheckResult obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
