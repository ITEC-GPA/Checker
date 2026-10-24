using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Detailed assessment of the same ordinary, opposite-face pair as
    /// FlexibleEndPlate.VerifyPair. Keeps both independently assessed children even
    /// when applicability of either child prevents a common envelope. A known
    /// failed child remains failed; this does not make an unsupported pair complete.</summary>
    public sealed class FlexibleEndPlatePairAssessmentResult
    {
        public ConnectionVerification<FlexibleEndPlatePairInput> Assessment { get; }
        public FlexibleEndPlateResult First { get; }
        public FlexibleEndPlateResult Second { get; }
        public FlexibleEndPlatePairInput Input => Assessment.Input;
        public ConnectionStatus Status => Assessment.Status;
        public bool IsComplete => Assessment.IsComplete;
        public bool HasFailedComponent { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks => Assessment.Checks;
        public IReadOnlyList<SectionResistanceValue> Trace => Assessment.Trace;
        public IReadOnlyList<string> Diagnostics => Assessment.Diagnostics;
        public ConnectionCheck GoverningCheck => Assessment.GoverningCheck;
        public double? Utilization => Assessment.Utilization;
        public string Scope => Assessment.Scope;

        internal FlexibleEndPlatePairAssessmentResult(
            ConnectionVerification<FlexibleEndPlatePairInput> assessment,
            FlexibleEndPlateResult first, FlexibleEndPlateResult second)
        {
            Assessment = assessment ?? throw new ArgumentNullException(nameof(assessment));
            First = first ?? throw new ArgumentNullException(nameof(first));
            Second = second ?? throw new ArgumentNullException(nameof(second));
            // Includes a failed shared support with two individually passing sides.
            // An unsupported envelope must not erase a failure already found in a child.
            HasFailedComponent = first.Status == ConnectionStatus.Failed
                || second.Status == ConnectionStatus.Failed || assessment.Status == ConnectionStatus.Failed;
        }
    }
}
