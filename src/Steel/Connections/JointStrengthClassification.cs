using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum JointColumnPosition { TopOfColumn, WithinContinuousEquivalentColumn }
    public enum JointStrengthRange { PinnedRange, PartialStrengthRange, FullStrengthRange }
    public enum JointStrengthClass { NominallyPinned, PartialStrength, FullStrength }

    /// <summary>The typed plastic-rotation criterion must belong to the SAME immutable
    /// resistance evidence. GlobalAnalysisCompatibilitySource identifies the separate
    /// assessment that the simple model transfers the forces without adverse moments and
    /// can accept the rotations imposed by the actual load case. A thin plate alone or a
    /// rotation M/S is not this evidence. Neither statement is independently certified here.</summary>
    public sealed class EndPlatePinnedRotationEvidence
    {
        public EndPlateRotationCapacityResult RotationCriterion { get; }
        public string GlobalAnalysisCompatibilitySource { get; }
        public EndPlatePinnedRotationEvidence(EndPlateRotationCapacityResult rotationCriterion,
            string globalAnalysisCompatibilitySource)
        {
            RotationCriterion = rotationCriterion ?? throw new ArgumentNullException(nameof(rotationCriterion));
            GlobalAnalysisCompatibilitySource = globalAnalysisCompatibilitySource;
        }
    }

    /// <summary>Figure5.5 beam-to-column configuration, same bending axis/direction.
    /// WithinContinuousEquivalentColumn requires the same relevant column resistance
    /// above and below, not an unqualified factor2 for unlike columns or multiple beams.</summary>
    public sealed class JointStrengthClassificationInput
    {
        public EndPlateJointResistanceEvidence Resistance { get; }
        public JointColumnPosition Position { get; }
        public string AdjacentMembersConfigurationSource { get; }
        public EndPlatePinnedRotationEvidence PinnedRotation { get; }
        public JointStrengthClassificationInput(EndPlateJointResistanceEvidence resistance,
            JointColumnPosition position, string adjacentMembersConfigurationSource,
            EndPlatePinnedRotationEvidence pinnedRotation = null)
        {
            Resistance = resistance ?? throw new ArgumentNullException(nameof(resistance));
            if (!Enum.IsDefined(typeof(JointColumnPosition), position)) throw new ArgumentOutOfRangeException(nameof(position));
            Position = position; AdjacentMembersConfigurationSource = adjacentMembersConfigurationSource;
            PinnedRotation = pinnedRotation;
        }
    }

    public sealed class JointStrengthClassificationResult
    {
        public JointStrengthClassificationInput Input { get; }
        public JointStrengthRange? NumericalRange { get; }
        public JointStrengthClass? AppliedClass { get; }
        public bool IsClassificationComplete => AppliedClass.HasValue && Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public bool PinnedRotationEvidenceRequired => NumericalRange == JointStrengthRange.PinnedRange;
        public double? FullStrengthThreshold { get; }
        public double? PinnedStrengthLimit { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Reference => "EN1993-1-8:2005 5.2.3, Figure5.5 and 5.2.3.2(3)";
        public string Scope => "Strength class in the Figure5.5 beam-column field using externally qualified complete-joint and adjacent-member resistances. No stiffness class, member verification or automatic analysis model. Pinned needs separate rotation/model compatibility evidence.";
        internal JointStrengthClassificationResult(JointStrengthClassificationInput input,
            JointStrengthRange? range, JointStrengthClass? applied, double? full, double? pinned,
            List<SectionResistanceValue> trace, List<string> diagnostics)
        {
            Input = input; NumericalRange = range; AppliedClass = diagnostics.Count == 0 ? applied : null;
            FullStrengthThreshold = full; PinnedStrengthLimit = pinned;
            Trace = Array.AsReadOnly(trace.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
        }
    }

    public static class JointStrengthClassification
    {
        public static JointStrengthClassificationResult Classify(JointStrengthClassificationInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            JointStrengthRange? range = null; JointStrengthClass? applied = null;
            double? full = null, pinned = null;
            var e = p.Resistance; e.ValidateResistanceEvidence(issues);
            if (string.IsNullOrWhiteSpace(p.AdjacentMembersConfigurationSource)) issues.Add("AdjacentMemberConfigurationEvidenceRequired");
            if (issues.Count != 0) return Result();
            // Figure5.5 says OR: the smaller adjacent-member threshold controls.
            // Avoid overflow of 2*Mc when the beam already controls.
            double columnThreshold = e.ColumnPlasticMomentResistance;
            if (p.Position == JointColumnPosition.WithinContinuousEquivalentColumn)
                columnThreshold = e.ColumnPlasticMomentResistance >= e.BeamPlasticMomentResistance / 2
                    ? e.BeamPlasticMomentResistance : 2 * e.ColumnPlasticMomentResistance;
            full = Math.Min(e.BeamPlasticMomentResistance, columnThreshold); pinned = .25 * full.Value;
            if (!(pinned.Value > 0)) { issues.Add("StrengthLimitsOutsideNumericRange"); return Result(); }
            trace.Add(new SectionResistanceValue("FullStrengthThreshold", full.Value, "Nmm"));
            trace.Add(new SectionResistanceValue("PinnedStrengthLimit", pinned.Value, "Nmm"));
            trace.Add(new SectionResistanceValue("JointMomentResistance", e.JointMomentResistance, "Nmm"));
            if (e.JointMomentResistance >= full.Value)
            { range = JointStrengthRange.FullStrengthRange; applied = JointStrengthClass.FullStrength; }
            else if (e.JointMomentResistance > pinned.Value)
            { range = JointStrengthRange.PartialStrengthRange; applied = JointStrengthClass.PartialStrength; }
            else
            {
                range = JointStrengthRange.PinnedRange;
                if (p.PinnedRotation == null) issues.Add("PinnedRotationAndGlobalModelEvidenceRequired");
                else
                {
                    var criterion = p.PinnedRotation.RotationCriterion;
                    if (!ReferenceEquals(criterion.Input.Resistance, e)) issues.Add("PinnedRotationEvidenceBelongsToAnotherResistanceCase");
                    if (criterion.Status != EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis)
                        issues.Add("PinnedRotationSufficiencyNotDemonstrated");
                    if (string.IsNullOrWhiteSpace(p.PinnedRotation.GlobalAnalysisCompatibilitySource))
                        issues.Add("PinnedGlobalAnalysisCompatibilityEvidenceRequired");
                }
                if (issues.Count == 0) applied = JointStrengthClass.NominallyPinned;
            }
            return Result();

            JointStrengthClassificationResult Result() => new JointStrengthClassificationResult(p,
                range, applied, full, pinned, trace, issues);
        }
    }
}
