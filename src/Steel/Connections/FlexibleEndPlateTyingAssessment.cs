using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One accidental tying state, assessed by the plate/bolt/beam, weld and
    /// ideal interior-support-web components. All input adapters must contain the SAME
    /// immutable FlexibleEndPlateTyingInput instance; equal numeric forces from separate
    /// states are not evidence of concomitance. No complete-joint resistance is returned.</summary>
    internal sealed class FlexibleEndPlateTyingAssessmentResult
    {
        public FlexibleEndPlateTyingWeldResult Welds { get; }
        public FlexibleEndPlateTyingSupportWebResult SupportWeb { get; }
        public FlexibleEndPlateTyingResult PlateAndBeam => Welds.PlateAndBeamAssessment;
        public bool IsSameTyingState { get; }
        public ConnectionStatus CoveredComponentsStatus { get; }
        public bool HasFailedComponent { get; }
        public bool CanQualifyWholeJoint => false;
        public bool IsSupportBoundaryCompatibilityVerified => SupportWeb.IsSupportBoundaryCompatibilityVerified;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingWholeJointChecks { get; }
        public ConnectionCheck GoverningCheck => Checks.Count == 0 ? null : Checks.OrderByDescending(c => c.Utilization).First();
        public double? CoveredComponentsUtilization => GoverningCheck?.Utilization;
        public string Scope => "Shared accidental state: plate/bolts/beam, SCI weld hierarchy and actual weld demand, ideal interior support-web mechanism. No finite-boundary, contact, supporting-member or whole-joint qualification.";

        internal FlexibleEndPlateTyingAssessmentResult(FlexibleEndPlateTyingWeldResult welds,
            FlexibleEndPlateTyingSupportWebResult support, bool sameState)
        {
            Welds = welds; SupportWeb = support; IsSameTyingState = sameState;
            var issues = new List<string>();
            if (!sameState) issues.Add("SAME_IMMUTABLE_TYING_INPUT_REQUIRED");
            issues.AddRange(PlateAndBeam.Diagnostics.Select(d => "PlateAndBeam:" + d));
            issues.AddRange(welds.Diagnostics.Select(d => "Welds:" + d));
            issues.AddRange(support.Diagnostics.Select(d => "SupportWeb:" + d));
            Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray());
            var states = new[] { PlateAndBeam.Status, welds.Status, support.LocalModelStatus,
                support.PlateAndBeamAssessment.Status };
            HasFailedComponent = states.Contains(ConnectionStatus.Failed);
            CoveredComponentsStatus = !sameState || states.Contains(ConnectionStatus.NotSupported)
                ? ConnectionStatus.NotSupported : HasFailedComponent ? ConnectionStatus.Failed : ConnectionStatus.Passed;
            // Incompatible or incomplete components must not produce a favourable aggregate.
            // Individual results remain available, including any known failed component.
            Checks = Array.AsReadOnly(CoveredComponentsStatus == ConnectionStatus.NotSupported
                ? Array.Empty<ConnectionCheck>()
                : PlateAndBeam.Checks.Concat(welds.Checks).Concat(support.Checks).ToArray());
            var pending = PlateAndBeam.PendingChecks.Concat(welds.PendingChecks).Concat(support.PendingChecks);
            if (sameState && welds.IsComponentAssessmentComplete)
                pending = pending.Where(x => x != "WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY");
            PendingWholeJointChecks = Array.AsReadOnly(pending.Distinct().ToArray());
        }
    }

    internal static class FlexibleEndPlateTyingAssessment
    {
        /// <summary>Both adapters must reference the same Tying object. Their source
        /// strings are provenance descriptions, not interchangeable load-case identifiers.
        /// A passing component cannot hide failure of another component or certify the
        /// continuous-web model's compatibility with actual finite support boundaries.</summary>
        public static FlexibleEndPlateTyingAssessmentResult Verify(FlexibleEndPlateTyingWeldInput weld,
            FlexibleEndPlateTyingSupportWebInput support)
        {
            if (weld == null) throw new ArgumentNullException(nameof(weld));
            if (support == null) throw new ArgumentNullException(nameof(support));
            return new FlexibleEndPlateTyingAssessmentResult(FlexibleEndPlateTyingWeld.Verify(weld),
                FlexibleEndPlateTyingSupportWeb.Verify(support), ReferenceEquals(weld.Tying, support.Tying));
        }
    }
}
