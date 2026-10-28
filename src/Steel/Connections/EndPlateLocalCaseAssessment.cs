using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Coherent local evidence, not the complete joint resistance or a moment-rotation law.
    /// Actual child results remain accessible even if another component is unsupported.</summary>
    public sealed class EndPlateLocalCaseAssessmentResult
    {
        public EndPlateMomentWithBeamSectionResult BeamAndMoment { get; }
        public EndPlateConnectionAssessmentResult Connection { get; }
        public EndPlateInitialStiffnessFromMomentResult InitialStiffnessAssessment { get; }
        public SteelActions Actions => BeamAndMoment.BeamSection.Input.Actions;
        public bool HasCoherentCase { get; }
        public bool HasFailedComponent { get; }
        public bool HasUnsupportedComponent { get; }
        public ConnectionStatus CoveredComponentsStatus { get; }
        public bool CanQualifyWholeJoint => false;
        public bool CanQualifyWholeMember => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingWholeJointChecks { get; }
        public ConnectionCheck GoverningCoveredCheck => Checks.Count == 0 ? null : Checks.OrderByDescending(c => c.Utilization).First();
        public double? CoveredComponentsUtilization => GoverningCoveredCheck?.Utilization;
        public double? InitialStiffness => HasCoherentCase ? InitialStiffnessAssessment.InitialStiffness : null;
        public string Scope => "Same-case beam section, moment components, prescribed bolt/weld checks and initial elastic stiffness. No whole-joint resistance, secant law, rotation, stability or net/block qualification.";
        internal EndPlateLocalCaseAssessmentResult(EndPlateMomentWithBeamSectionResult beamAndMoment,
            EndPlateConnectionAssessmentResult connection, EndPlateInitialStiffnessFromMomentResult initial)
        {
            BeamAndMoment = beamAndMoment; Connection = connection; InitialStiffnessAssessment = initial;
            HasCoherentCase = beamAndMoment.MomentComponents != null && connection.IsSameMomentAssessment
                && ReferenceEquals(beamAndMoment.MomentComponents, connection.MomentComponents)
                && ReferenceEquals(beamAndMoment, initial.Parent)
                && ReferenceEquals(beamAndMoment.MomentComponents, initial.MomentComponents);
            HasFailedComponent = beamAndMoment.Status == ConnectionStatus.Failed || connection.HasFailedComponent
                || initial.Status == ConnectionStatus.Failed;
            HasUnsupportedComponent = beamAndMoment.Status == ConnectionStatus.NotSupported || !beamAndMoment.IsComplete
                || connection.CoveredComponentsStatus == ConnectionStatus.NotSupported || !initial.IsSupported;
            CoveredComponentsStatus = !HasCoherentCase || HasUnsupportedComponent ? ConnectionStatus.NotSupported
                : HasFailedComponent ? ConnectionStatus.Failed : ConnectionStatus.Passed;
            var issues = new List<string>();
            if (!HasCoherentCase) issues.Add("SAME_BEAM_MOMENT_CONNECTION_AND_STIFFNESS_PARENT_REQUIRED");
            issues.AddRange(beamAndMoment.Diagnostics.Select(d => "BeamAndMoment:" + d));
            issues.AddRange(connection.Diagnostics.Select(d => "Connection:" + d));
            issues.AddRange(initial.Diagnostics.Select(d => "InitialStiffness:" + d));
            Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray());
            // The full child objects retain failures when another model is unsupported;
            // a single ratio cannot represent an incomplete or mismatched assessment.
            Checks = Array.AsReadOnly(CoveredComponentsStatus == ConnectionStatus.NotSupported
                ? Array.Empty<ConnectionCheck>()
                : beamAndMoment.BeamSection.Checks.Select(c => c.WithId("BeamSection." + c.Id)).Concat(connection.Checks).ToArray());
            PendingWholeJointChecks = Array.AsReadOnly(connection.PendingWholeJointChecks.Concat(initial.PendingChecks)
                .Concat(new[] { "The Model beam check covers the actual cut, not member stability or the supporting column member",
                    "Initial stiffness does not qualify complete-joint Mj,Rd, secant stiffness, rotation or ductility" })
                .Distinct().ToArray());
        }
    }

    internal static class EndPlateLocalCaseAssessment
    {
        public static EndPlateLocalCaseAssessmentResult Assess(EndPlateMomentWithBeamSectionResult beamAndMoment,
            EndPlateConnectionAssessmentResult connection, EndPlateInitialStiffnessFromMomentResult initialStiffness)
        {
            if (beamAndMoment == null) throw new ArgumentNullException(nameof(beamAndMoment));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (initialStiffness == null) throw new ArgumentNullException(nameof(initialStiffness));
            return new EndPlateLocalCaseAssessmentResult(beamAndMoment, connection, initialStiffness);
        }
    }
}
