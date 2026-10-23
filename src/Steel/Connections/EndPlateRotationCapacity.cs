using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum EndPlateRotationCapacityStatus
    {
        NotRequiredByOverstrength, SufficientForPlasticAnalysis,
        CannotAssumeSufficientForPlasticAnalysis, NotDemonstratedBySimplifiedRules,
        OutOfScope, MissingEvidence
    }

    /// <summary>Simplified sufficient-rotation criteria for static bolted end-plate
    /// beam-to-column-flange joints. Nominal grades are explicit: thickness-dependent
    /// fy does not identify the grade. No numeric rotation capacity is inferred.</summary>
    public sealed class EndPlateRotationCapacityInput
    {
        public EndPlateJointResistanceEvidence Resistance { get; }
        public JointNominalSteelGrade BeamGrade { get; }
        public JointNominalSteelGrade ColumnGrade { get; }
        public JointNominalSteelGrade EndPlateGrade { get; }
        public string NominalGradesSource { get; }
        public EndPlateRotationCapacityInput(EndPlateJointResistanceEvidence resistance,
            JointNominalSteelGrade beamGrade, JointNominalSteelGrade columnGrade,
            JointNominalSteelGrade endPlateGrade, string nominalGradesSource)
        {
            Resistance = resistance ?? throw new ArgumentNullException(nameof(resistance));
            foreach (var grade in new[] { beamGrade, columnGrade, endPlateGrade })
                if (!Enum.IsDefined(typeof(JointNominalSteelGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            BeamGrade = beamGrade; ColumnGrade = columnGrade; EndPlateGrade = endPlateGrade;
            NominalGradesSource = nominalGradesSource;
        }
    }

    /// <summary>Qualitative sufficient condition or exemption, never a measured rotation
    /// capacity. Failure of a sufficient condition does not prove inadequate ductility.</summary>
    public sealed class EndPlateRotationCapacityResult
    {
        public EndPlateRotationCapacityInput Input { get; }
        public EndPlateRotationCapacityStatus Status { get; }
        public double? RotationCapacityRad => null;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> AppliedRules { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public string Reference => "EN1993-1-8:2005 6.4.1 and 6.4.2, bolted connections";
        public string Scope => "Static bolted end-plate beam-column joint, same qualified resistance case and bending direction. Sufficiency for plastic analysis or overstrength exemption only. No phi_Cd, no welded-joint rotation formula, no seismic qualification, no automatic global joint approval. Governing mechanisms are explicitly supplied external evidence.";
        internal EndPlateRotationCapacityResult(EndPlateRotationCapacityInput input,
            EndPlateRotationCapacityStatus status, List<string> rules, List<string> diagnostics,
            List<SectionResistanceValue> trace)
        {
            Input = input; Status = status;
            AppliedRules = Array.AsReadOnly(rules.Distinct().ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            Trace = Array.AsReadOnly(trace.ToArray());
        }
    }

    public static class EndPlateRotationCapacity
    {
        public static EndPlateRotationCapacityResult Assess(EndPlateRotationCapacityInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var e = p.Resistance;
            var issues = new List<string>(); var rules = new List<string>();
            var trace = new List<SectionResistanceValue>();
            e.ValidateResistanceEvidence(issues);
            if (issues.Count != 0) return Result(EndPlateRotationCapacityStatus.MissingEvidence);
            double overstrengthRatio = e.JointMomentResistance / e.BeamPlasticMomentResistance;
            Value("JointToConnectedBeamMomentResistanceRatio", overstrengthRatio, "1");
            Value("JointMomentResistance", e.JointMomentResistance, "Nmm");
            Value("ConnectedBeamPlasticMomentResistance", e.BeamPlasticMomentResistance, "Nmm");
            if (issues.Count != 0) return Result(EndPlateRotationCapacityStatus.OutOfScope);
            // 6.4.1(3) is an exemption, not the methods6.4.2/6.4.3 restricted by6.4.1(2).
            // Reference is the connected BEAM, not min(Mbeam,Mcolumn) of classification.
            if (overstrengthRatio >= 1.2)
            { rules.Add("EN1993-1-8:2005 6.4.1(3)"); return Result(EndPlateRotationCapacityStatus.NotRequiredByOverstrength); }
            if (string.IsNullOrWhiteSpace(p.NominalGradesSource)) issues.Add("NominalSteelGradesEvidenceRequired");
            if (e.GoverningMechanisms.Count == 0 || string.IsNullOrWhiteSpace(e.GoverningMechanismsSource))
                issues.Add("AllGoverningJointResistanceMechanismsEvidenceRequired");
            if (issues.Count != 0) return Result(EndPlateRotationCapacityStatus.MissingEvidence);
            double axialRatio = Math.Abs(e.AxialForce) / e.BeamPlasticAxialResistance;
            Value("AbsoluteAxialToBeamPlasticResistanceRatio", axialRatio, "1");
            if (axialRatio > .05) issues.Add("SimplifiedRotationCriteriaRequireAxialForceAtMostFivePercent");
            if (!Permitted(p.BeamGrade) || !Permitted(p.ColumnGrade) || !Permitted(p.EndPlateGrade))
                issues.Add("SimplifiedRotationCriteriaRequireS235S275OrS355");
            if (issues.Count != 0) return Result(EndPlateRotationCapacityStatus.OutOfScope);
            double webSlenderness = e.Column.ClearWeb / e.Column.WebThickness;
            double webLimit = 69 * Math.Sqrt(235 / e.Column.Fy);
            double flangeLimit = .36 * e.Bolt.Diameter * Math.Sqrt(e.Bolt.Fu / e.ColumnFlangeFy);
            double plateLimit = .36 * e.Bolt.Diameter * Math.Sqrt(e.Bolt.Fu / e.EndPlate.Fy);
            Value("ColumnWebClearDepthOverThickness", webSlenderness, "1");
            Value("ColumnWebSlendernessLimit", webLimit, "1");
            Value("ColumnFlangeThicknessLimit", flangeLimit, "mm");
            Value("EndPlateThicknessLimit", plateLimit, "mm");
            Value("ColumnFlangeThickness", e.Column.FlangeThickness, "mm");
            Value("EndPlateThickness", e.EndPlate.Thickness, "mm");
            if (issues.Count != 0) return Result(EndPlateRotationCapacityStatus.OutOfScope);
            // Explicit negative rule must not disappear when a bending mechanism is also critical.
            if (e.GoverningMechanisms.Contains(EndPlateGoverningMechanism.BoltShear))
            {
                rules.Add("EN1993-1-8:2005 6.4.2(3)");
                issues.Add("BoltsInShearGovernJointResistance");
                return Result(EndPlateRotationCapacityStatus.CannotAssumeSufficientForPlasticAnalysis);
            }
            bool thin = e.Column.FlangeThickness <= flangeLimit || e.EndPlate.Thickness <= plateLimit;
            bool allCovered = true;
            foreach (var mode in e.GoverningMechanisms)
            {
                if (mode == EndPlateGoverningMechanism.ColumnWebPanelShear && webSlenderness <= webLimit)
                    rules.Add("EN1993-1-8:2005 6.4.2(1)");
                else if ((mode == EndPlateGoverningMechanism.ColumnFlangeBending || mode == EndPlateGoverningMechanism.EndPlateBending) && thin)
                    rules.Add("EN1993-1-8:2005 6.4.2(2)");
                else
                {
                    allCovered = false;
                    issues.Add("GoverningMechanismNotCoveredBySufficientCriterion:" + mode);
                }
            }
            // With co-governing modes, qualify every declared mode. Picking the first would
            // hide a concurrent brittle/unknown limit. No invented interaction is calculated.
            return Result(allCovered ? EndPlateRotationCapacityStatus.SufficientForPlasticAnalysis
                : EndPlateRotationCapacityStatus.NotDemonstratedBySimplifiedRules);

            void Value(string name, double value, string unit)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) issues.Add(name + "OutsideNumericRange");
                else trace.Add(new SectionResistanceValue(name, value, unit));
            }
            EndPlateRotationCapacityResult Result(EndPlateRotationCapacityStatus status)
                => new EndPlateRotationCapacityResult(p, status, rules, issues, trace);
        }
        private static bool Permitted(JointNominalSteelGrade grade)
            => grade == JointNominalSteelGrade.S235 || grade == JointNominalSteelGrade.S275 || grade == JointNominalSteelGrade.S355;
    }
}
