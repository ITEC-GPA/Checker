using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual head/nut dimensions and bearing footprints, mm. Head is on the
    /// end-plate side, nut on the column side. A washer may enlarge the footprint used
    /// only for physical clearance; it never replaces the head/nut punching diameter.</summary>
    public sealed class EndPlateBoltHardware
    {
        public double HeadAcrossFlats { get; }
        public double HeadAcrossPoints { get; }
        public double NutAcrossFlats { get; }
        public double NutAcrossPoints { get; }
        public double EndPlateContactFootprint { get; }
        public double ColumnContactFootprint { get; }
        public double PunchingMeanDiameter => Math.Min((HeadAcrossFlats + HeadAcrossPoints) / 2, (NutAcrossFlats + NutAcrossPoints) / 2);
        public string Source { get; }
        public EndPlateBoltHardware(double headAcrossFlats, double headAcrossPoints, double nutAcrossFlats,
            double nutAcrossPoints, double endPlateContactFootprint, double columnContactFootprint, string source)
        {
            foreach (double v in new[] { headAcrossFlats, headAcrossPoints, nutAcrossFlats, nutAcrossPoints,
                endPlateContactFootprint, columnContactFootprint }) ConnectionValidation.Positive(v, nameof(v));
            if (headAcrossPoints < headAcrossFlats || nutAcrossPoints < nutAcrossFlats
                || endPlateContactFootprint < headAcrossPoints || columnContactFootprint < nutAcrossPoints)
                throw new ArgumentException("Actual head/nut across-points and contact footprints must enclose their across-flats dimensions.");
            HeadAcrossFlats = headAcrossFlats; HeadAcrossPoints = headAcrossPoints;
            NutAcrossFlats = nutAcrossFlats; NutAcrossPoints = nutAcrossPoints;
            EndPlateContactFootprint = endPlateContactFootprint; ColumnContactFootprint = columnContactFootprint; Source = source;
        }
    }

    /// <summary>One physical pair of bolts. Y is measured from the beam tension-flange
    /// exterior face toward the compression flange. Shears are nonnegative magnitudes
    /// in the single declared vertical transfer direction. MomentRowIndex is zero based;
    /// null marks a separately qualified shear-only row nearer the compression zone.</summary>
    public sealed class EndPlateBoltShearRow
    {
        public int? MomentRowIndex { get; }
        public double Y { get; }
        public double LeftShear { get; }
        public double RightShear { get; }
        public string NoTensionSource { get; }
        public EndPlateBoltShearRow(int? momentRowIndex, double y, double leftShear, double rightShear, string noTensionSource = null)
        {
            if (momentRowIndex.HasValue && (momentRowIndex < 0 || momentRowIndex > 19)) throw new ArgumentOutOfRangeException(nameof(momentRowIndex));
            ConnectionNumbers.Finite(y, nameof(y)); ConnectionValidation.Nonnegative(leftShear, nameof(leftShear));
            ConnectionValidation.Nonnegative(rightShear, nameof(rightShear));
            MomentRowIndex = momentRowIndex; Y = y; LeftShear = leftShear; RightShear = rightShear; NoTensionSource = noTensionSource;
        }
    }

    public sealed class EndPlateBoltComponentInput
    {
        public EndPlateMomentComponentResult MomentComponents { get; }
        public EndPlateBoltHardware Hardware { get; }
        public IReadOnlyList<EndPlateBoltShearRow> Rows { get; }
        public double ColumnFlangeFu { get; }
        public string ColumnFlangeStrengthSource { get; }
        public string ShearDistributionSource { get; }
        public EndPlateBoltComponentInput(EndPlateMomentComponentResult momentComponents, EndPlateBoltHardware hardware,
            IEnumerable<EndPlateBoltShearRow> rows, double columnFlangeFu, string columnFlangeStrengthSource, string shearDistributionSource)
        {
            MomentComponents = momentComponents ?? throw new ArgumentNullException(nameof(momentComponents));
            Hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var copy = rows.ToArray();
            if (copy.Length < 1 || copy.Length > 20 || copy.Any(r => r == null)) throw new ArgumentException("One to twenty physical non-null rows required.", nameof(rows));
            Rows = Array.AsReadOnly(copy); ConnectionValidation.Positive(columnFlangeFu, nameof(columnFlangeFu));
            ColumnFlangeFu = columnFlangeFu; ColumnFlangeStrengthSource = columnFlangeStrengthSource;
            ShearDistributionSource = shearDistributionSource;
        }
    }

    public sealed class EndPlateBoltComponentResult
    {
        public ConnectionVerification<EndPlateBoltComponentInput> BoltAssessment { get; }
        public EndPlateMomentComponentResult MomentComponents => BoltAssessment.Input.MomentComponents;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> PendingChecks { get; }
        internal EndPlateBoltComponentResult(ConnectionVerification<EndPlateBoltComponentInput> assessment)
        {
            BoltAssessment = assessment;
            PendingChecks = Array.AsReadOnly(new[] {
                "Beam/end-plate welds and shear introduction through the beam-web welds, EN6.2.2(1)",
                "End-plate and support net/block failure, local stress/load introduction, and member verification",
                "Rotational stiffness, rotation capacity and global-model compatibility",
                "Coupled resistance re-evaluation if the conservative full-Ft punching envelope fails; no T-stub capacity is increased or silently reduced" });
        }
    }

    public static class EndPlateBoltComponents
    {
        public static EndPlateBoltComponentResult Verify(EndPlateBoltComponentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var parent = p.MomentComponents.ComponentAssessment; var m = parent.Input; var code = m.Policy.Code;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            Required(p.Hardware.Source, "ActualHardwareSourceRequired");
            Required(p.ColumnFlangeStrengthSource, "ColumnFlangeStrengthSourceRequired");
            Required(p.ShearDistributionSource, "ConcomitantShearDistributionSourceRequired");
            if (parent.Status != ConnectionStatus.Passed) issues.Add("PassedMomentComponentsRequiredForFullFtEnvelope");
            if (p.ColumnFlangeFu < m.ColumnFlangeFy) issues.Add("ColumnFlangeUltimateStrengthBelowYield");
            double dm = p.Hardware.PunchingMeanDiameter;
            if (!Finite(dm) || dm <= m.Bolt.HoleDiameter) issues.Add("PunchingHeadNutMustCoverHole");
            if (m.TStubMethod == TStubMode1Method.DistributedContact
                && m.ContactWidth > Math.Min(p.Hardware.EndPlateContactFootprint, p.Hardware.ColumnContactFootprint))
                issues.Add("TStubContactWidthExceedsActualHardwareOnOnePly");
            var expectedY = new List<double>();
            if (m.ExtensionGeometry != null) expectedY.Add(-m.ExtensionGeometry.FlangeToRow);
            expectedY.AddRange(m.InternalGeometry.OrderedInternalRows.Select(y => y + m.Beam.FlangeThickness));
            for (int i = 0; i < expectedY.Count; i++)
            {
                var matches = p.Rows.Where(r => r.MomentRowIndex == i).ToArray();
                if (matches.Length != 1 || !Equal(matches[0].Y, expectedY[i])) issues.Add("EveryMomentRowMustAppearExactlyOnceAtItsPhysicalCoordinate");
            }
            foreach (var row in p.Rows)
            {
                if (row.MomentRowIndex >= expectedY.Count) issues.Add("MomentRowIndexOutsideParentGeometry");
                if (!row.MomentRowIndex.HasValue)
                {
                    Required(row.NoTensionSource, "ShearOnlyRowNoTensionQualificationRequired");
                    if (row.Y <= expectedY.Last()) issues.Add("ShearOnlyRowsMustFollowEveryActiveTensionRow");
                }
            }
            for (int i = 1; i < p.Rows.Count; i++)
                if (ConnectionValidation.BelowMinimumWithRoundoff(p.Rows[i].Y - p.Rows[i - 1].Y, 2.4 * m.Bolt.HoleDiameter))
                    issues.Add("PhysicalRowsMustIncreaseWithQualifiedPitch");
            double total = p.Rows.Sum(r => r.LeftShear + r.RightShear);
            double imbalance = p.Rows.Sum(r => r.RightShear - r.LeftShear);
            double torsion = imbalance * m.InternalGeometry.BoltGauge / 2;
            if (!Finite(total) || Math.Abs(total - m.Shear) > 1e-10 * Math.Max(total, m.Shear)) issues.Add("AssignedBoltShearsDoNotEquilibrateRetainedShear");
            if (!Finite(torsion) || Math.Abs(imbalance) > 1e-10 * total) issues.Add("AssignedBoltShearsCreateUnmodelledTorsion");
            Value("AssignedShear", total, "N"); Value("RequiredShear", m.Shear, "N"); Value("AssignedTorsion", torsion, "Nmm");
            double plateTop = m.ExtensionGeometry == null ? 0 : -m.ExtensionGeometry.FlangeToRow - m.ExtensionGeometry.EndDistance;
            double plateBottom = m.Beam.Height + m.CompressionEndPlateProjection;
            double columnTop = expectedY.First() - m.ColumnFirstEndContinuation;
            double columnBottom = expectedY.Last() + m.ColumnLastEndContinuation;
            double jointLength = p.Rows.Last().Y - p.Rows.First().Y;
            Value("PhysicalJointLength", jointLength, "mm"); Value("PunchingMeanHeadNutDiameter", dm, "mm");
            if (issues.Count != 0) return Result();
            var columnPly = new ConnectionPlate(m.Column.Width, m.Column.FlangeThickness, m.ColumnFlangeFy, p.ColumnFlangeFu);
            var baseBolt = BoltStrength.Verify(new BoltStrengthInput(code, m.Bolt, 0, 0, jointLength: jointLength,
                holeClearanceServiceabilitySource: m.HoleClearanceServiceabilitySource), out var baseValues);
            Include("BaseBolt", baseBolt.Checks, baseBolt.Trace, baseBolt.Diagnostics, false);
            if (issues.Count != 0) return Result();
            double ft = baseValues.TensionResistance;
            double fv = baseValues.ShearResistance;
            Value("FullTensionEnvelopePerBolt", ft, "N");
            Value("TensionBoltResidualShearFactor", 2d / 7, "1");
            Value("BoltsOnlyShearEnvelope", p.Rows.Sum(r => 2 * fv * (r.MomentRowIndex.HasValue ? 2d / 7 : 1)), "N");
            for (int rowIndex = 0; rowIndex < p.Rows.Count; rowIndex++)
            {
                var row = p.Rows[rowIndex];
                double? pitch = p.Rows.Count == 1 ? (double?)null : rowIndex == 0 ? p.Rows[1].Y - row.Y
                    : rowIndex == p.Rows.Count - 1 ? row.Y - p.Rows[rowIndex - 1].Y
                    : Math.Min(row.Y - p.Rows[rowIndex - 1].Y, p.Rows[rowIndex + 1].Y - row.Y);
                double plateEnd = Math.Min(row.Y - plateTop, plateBottom - row.Y);
                double columnEnd = Math.Min(row.Y - columnTop, columnBottom - row.Y);
                double plateEdge = (m.EndPlate.Width - m.InternalGeometry.BoltGauge) / 2;
                double columnEdge = (m.Column.Width - m.InternalGeometry.BoltGauge) / 2;
                string id = "PhysicalRow" + (rowIndex + 1);
                double plateClear = PlateWeldClearance(row.Y);
                double columnClear = (m.InternalGeometry.BoltGauge - m.Column.WebThickness) / 2 - m.Column.RootRadius;
                // Actual contact footprints govern fit; dm governs punching, never a washer diameter.
                if (plateEnd < p.Hardware.EndPlateContactFootprint / 2 || plateEdge < p.Hardware.EndPlateContactFootprint / 2
                    || plateClear <= p.Hardware.EndPlateContactFootprint / 2
                    || columnEnd < p.Hardware.ColumnContactFootprint / 2 || columnEdge < p.Hardware.ColumnContactFootprint / 2
                    || columnClear <= p.Hardware.ColumnContactFootprint / 2
                    || m.InternalGeometry.BoltGauge < Math.Max(p.Hardware.EndPlateContactFootprint, p.Hardware.ColumnContactFootprint)
                    || pitch < Math.Max(p.Hardware.EndPlateContactFootprint, p.Hardware.ColumnContactFootprint))
                    issues.Add(id + ":HardwareOrFullPunchingPerimeterDoesNotFit");
                if (plateEnd < 1.2 * m.Bolt.HoleDiameter || columnEnd < 1.2 * m.Bolt.HoleDiameter)
                    issues.Add(id + ":MinimumPhysicalEndDistance");
                Value(id + ".Y", row.Y, "mm"); Value(id + ".PlateEndDistance", plateEnd, "mm");
                Value(id + ".ColumnEndDistance", columnEnd, "mm");
                Value(id + ".PlateWeldClearance", plateClear, "mm");
                double tension = row.MomentRowIndex.HasValue ? ft : 0;
                Value(id + ".TensionEnvelopePerBolt", tension, "N");
                if (issues.Count != 0) continue;
                EvaluateBolt(id + ".Left", row.LeftShear);
                EvaluateBolt(id + ".Right", row.RightShear);

                void EvaluateBolt(string name, double shear)
                {
                    var strength = new BoltStrengthInput(code, m.Bolt, shear, tension, jointLength: jointLength,
                        holeClearanceServiceabilitySource: m.HoleClearanceServiceabilitySource);
                    // Load reversal swaps the end/pitch mechanism of an extreme row. Taking only
                    // its end distance would omit the adjacent hole in the opposite direction.
                    // Evaluate both kernel inputs on each ply and retain the smaller capacity;
                    // the same conservative envelope also covers the intermediate rows.
                    BoltBearingInput Bearing(ConnectionPlate ply, double end, double edge, string plyName)
                    {
                        var endInput = new BoltBearingInput(code, m.Bolt, ply, shear, end, edge, pitch,
                            m.InternalGeometry.BoltGauge, true, true, m.HoleClearanceServiceabilitySource);
                        var endResult = BoltBearing.Verify(endInput);
                        Include(name + "." + plyName + ".EndEnvelope", endResult.Checks, endResult.Trace, endResult.Diagnostics, false);
                        if (!endResult.IsComplete) return null;
                        double endResistance = endResult.Checks.Single().Resistance;
                        Value(name + "." + plyName + ".BearingEndResistance", endResistance, "N");
                        if (!pitch.HasValue) return endInput;
                        var pitchInput = new BoltBearingInput(code, m.Bolt, ply, shear, end, edge, pitch,
                            m.InternalGeometry.BoltGauge, false, true, m.HoleClearanceServiceabilitySource);
                        var pitchResult = BoltBearing.Verify(pitchInput);
                        Include(name + "." + plyName + ".PitchEnvelope", pitchResult.Checks, pitchResult.Trace, pitchResult.Diagnostics, false);
                        if (!pitchResult.IsComplete) return null;
                        double pitchResistance = pitchResult.Checks.Single().Resistance;
                        Value(name + "." + plyName + ".BearingPitchResistance", pitchResistance, "N");
                        Value(name + "." + plyName + ".BearingUsesEndMechanism", endResistance <= pitchResistance ? 1 : 0, "1");
                        return endResistance <= pitchResistance ? endInput : pitchInput;
                    }
                    var headBearing = Bearing(m.EndPlate, plateEnd, plateEdge, "HeadPly");
                    var nutBearing = Bearing(columnPly, columnEnd, columnEdge, "NutPly");
                    if (headBearing == null || nutBearing == null) return;
                    var assembled = BoltSingleShearAssembly.Verify(new BoltSingleShearAssemblyInput(strength,
                        headBearing, nutBearing,
                        new BoltPunchingInput(code, m.EndPlate, tension, m.Bolt.HoleDiameter, dm),
                        new BoltPunchingInput(code, columnPly, tension, m.Bolt.HoleDiameter, dm)));
                    Include(name, assembled.Checks, assembled.Trace, assembled.Diagnostics, true);
                }
            }
            return Result();

            double PlateWeldClearance(double y)
            {
                if (y < 0) return -y - m.InternalGeometry.TensionFlangeWeldLeg;
                if (y > m.Beam.Height) return y - m.Beam.Height - m.InternalGeometry.CompressionFlangeWeldLeg;
                if (y <= m.Beam.FlangeThickness || y >= m.Beam.Height - m.Beam.FlangeThickness) return 0;
                return Math.Min((m.InternalGeometry.BoltGauge - m.Beam.WebThickness) / 2 - m.InternalGeometry.WebWeldLeg,
                    Math.Min(y - m.Beam.FlangeThickness - m.InternalGeometry.TensionFlangeWeldLeg,
                        m.Beam.Height - m.Beam.FlangeThickness - y - m.InternalGeometry.CompressionFlangeWeldLeg));
            }
            void Required(string source, string issue) { if (string.IsNullOrWhiteSpace(source)) issues.Add(issue); }
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Include(string name, IEnumerable<ConnectionCheck> childChecks, IEnumerable<SectionResistanceValue> childTrace,
                IEnumerable<string> childIssues, bool includeChecks)
            {
                issues.AddRange(childIssues.Select(d => name + ":" + d));
                trace.AddRange(childTrace.Select(t => new SectionResistanceValue(name + "." + t.Name, t.Value, t.Unit)));
                if (includeChecks) checks.AddRange(childChecks.Select(c => new ConnectionCheck(name + "." + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit)));
            }
            EndPlateBoltComponentResult Result() => new EndPlateBoltComponentResult(new ConnectionVerification<EndPlateBoltComponentInput>(
                p, code, "Steel.Connection.EndPlate.BoltComponents",
                "Prescribed equilibrated vertical bolt shears with full-Ft tension/prying envelope, both-ply bearing and head/nut punching. No whole joint, weld or plate net/block qualification.",
                checks, trace, issues, new[] {
                    "EN1993-1-8:2005 6.2.2(2) Note permits full tensile resistance with 0.4/1.4 of bolt shear resistance; no row-force/2 approximation omits prying.",
                    "The input shear distribution is independently qualified for this same parent load case: " + p.ShearDistributionSource,
                    "All active tension rows use full bolt Ft,Rd even at low moment; optional shear-only rows require explicit no-tension qualification.",
                    "Long-joint reduction uses the actual end-row spacing; no uniform-transfer exemption is invoked.",
                    "Each plate uses conservative minimum end distances/adjacent pitches; signed biaxial shear, packing and slip are outside this first field.",
                    "The smaller actual head/nut mean across-flats/points diameter is used for both punching checks, never the washer diameter.",
                    "If punching is below the full-Ft envelope this assessment fails; the parent T-stub is neither enlarged nor silently recalibrated.",
                    "NTC uses BoltSingleShearAssembly including the bearing/punching minima in equation4.2.71; EC checks interaction and each ply separately.",
                    "Washer/head/nut bearing pressure and execution suitability remain hardware qualification: " + p.Hardware.Source
                }));
        }
        private static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
