using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Two identical, mirrored equal-leg rolled angles. CleatLeg describes EACH
    /// leg's width, thickness and strengths. N, mm, Nmm and MPa. The action frame is
    /// (X along beam away from support, Y upward, Z normal to beam web). Actions are
    /// at the support face and mid-height, not at the beam bolt group centroid.
    /// This input deliberately contains no supporting-member model.</summary>
    public sealed class DoubleAngleCleatComponentInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate CleatLeg { get; }
        public double CleatRootRadius { get; }
        public ConnectionRolledISection Beam { get; }
        public ConnectionBolt Bolt { get; }
        public int Rows { get; }
        public double Pitch => 70;
        public double Height => 80 + (Rows - 1) * Pitch;
        public double TopOffset => 50;
        public double FreeEdge => 40;
        public double Gap => 10;
        public double Eccentricity => CleatLeg.Width - FreeEdge;
        public double BeamEndEdge => Eccentricity - Gap;
        public double SupportGauge => Beam.WebThickness + 2 * Eccentricity;
        public bool BeamNotched { get; }
        public bool BeamLaterallyRestrained { get; }
        public PlanarConnectionActions Actions { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public DoubleAngleCleatComponentInput(ConnectionDesignCode code, ConnectionPlate cleatLeg,
            double cleatRootRadius, ConnectionRolledISection beam, ConnectionBolt bolt, int rows,
            PlanarConnectionActions actions, bool beamLaterallyRestrained, bool beamNotched = false,
            string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            CleatLeg = cleatLeg ?? throw new ArgumentNullException(nameof(cleatLeg));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ConnectionValidation.Nonnegative(cleatRootRadius, nameof(cleatRootRadius));
            if (rows < 1 || rows > 100) throw new ArgumentOutOfRangeException(nameof(rows));
            if (cleatLeg.Width <= cleatLeg.Thickness + cleatRootRadius)
                throw new ArgumentException("The angle must have a straight leg outside its root.");
            CleatRootRadius = cleatRootRadius; Rows = rows; BeamNotched = beamNotched;
            BeamLaterallyRestrained = beamLaterallyRestrained; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>Only the named components are assessed. A Passed ComponentAssessment
    /// cannot qualify the supporting member or the whole joint.</summary>
    public sealed class DoubleAngleCleatComponentResult
    {
        public ConnectionVerification<DoubleAngleCleatComponentInput> ComponentAssessment { get; }
        public ElasticBoltGroupResult BeamBolts { get; }
        public ElasticBoltGroupResult LeftSupportBolts { get; }
        public ElasticBoltGroupResult RightSupportBolts { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> MissingWholeJointChecks { get; } = Array.AsReadOnly(new[] {
            "SUPPORT_MEMBER_BEARING", "SUPPORT_LOCAL_RESISTANCE_AND_CONCURRENT_ACTIONS", "FRAME_ROTATION_QUALIFICATION" });
        public double? LoadUtilization => ComponentAssessment.IsComplete
            ? (double?)ConnectionOutcome.LoadChecks(ComponentAssessment.Checks).Max(c => c.Utilization) : null;
        internal DoubleAngleCleatComponentResult(ConnectionVerification<DoubleAngleCleatComponentInput> assessment,
            ElasticBoltGroupResult beamBolts, ElasticBoltGroupResult left, ElasticBoltGroupResult right)
        { ComponentAssessment = assessment; BeamBolts = beamBolts; LeftSupportBolts = left; RightSupportBolts = right; }
    }

    /// <summary>CTICM/SCI SBE-MS5 chapter4, ordinary shear components. No accidental
    /// tying policy, supporting-member verification or whole-joint certification.</summary>
    public static class DoubleAngleCleatComponents
    {
        public static DoubleAngleCleatComponentResult Verify(DoubleAngleCleatComponentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            ElasticBoltGroupResult beamBolts = null, left = null, right = null;
            var leg = p.CleatLeg; var beam = p.Beam; var a = p.Actions;
            void Gate(bool condition, string message) { if (!condition) issues.Add(message); }
            void Value(string name, double value, string unit)
            { ConnectionNumbers.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Check(string id, double demand, double resistance, string source, string unit = "N")
                => checks.Add(new ConnectionCheck(id, demand, resistance, source, unit));
            void Detail(string id, double demand, double resistance, string source, string unit = "N")
                => checks.Add(new ConnectionCheck(id, demand, resistance, source, unit, ConnectionCheckRole.Detailing));
            DoubleAngleCleatComponentResult Result() => new DoubleAngleCleatComponentResult(
                new ConnectionVerification<DoubleAngleCleatComponentInput>(p, p.Code, "Steel.Connection.DoubleAngleCleat.Components",
                    "Ordinary downward shear components of two identical mirrored L90x90x10 S275 cleats and an unnotched restrained rolled I/H beam. "
                    + "Includes beam bolts in double shear, two angle legs per cleat, support-side bolts in single shear, and beam web. "
                    + "SUPPORTING MEMBER IS NOT ASSESSED: neither its bearing nor its local/global resistance. No whole-joint result, rotation classification, tying, ordinary axial force, fatigue, seismic or fire assessment.",
                    checks, trace, issues, new[] {
                        "CTICM/SCI SBE-MS5 chapter4 is complementary guidance for the ordinary static model, not an NTC-specific joint equation.",
                        "Beam bolt basis uses beam-longitudinal X/upward Y; support bolt basis uses transverse X/upward Y. Both are right-handed; normal axes differ.",
                        "The symmetric beam bolt transfers its total force through two equal shear planes. Each angle carries half; the beam web carries the total.",
                        "Nominal leg bending is represented by the guidance1.27 factor and support-bolt0.8 factor, not by an invented axial bolt force.",
                        "Long-joint reduction is retained. The source six-row example does not explicitly justify an exemption and is separately compared before that reduction.",
                        "No packing/preload, slots or unequal cleats. Standard M20 hardware fits within a37mm diameter envelope; installation/access and member/frame adequacy remain separate."
                    }), beamBolts, left, right);

            Gate(a.Fx == 0 && a.Fy <= 0 && a.Fz == 0 && a.Mx == 0 && a.My == 0 && a.Mz == 0
                && a.ApplicationX == 0 && a.ApplicationY == 0 && a.ApplicationZ == 0, "DOWNWARD_SHEAR_AT_SUPPORT_FACE_ONLY");
            Gate(p.Rows >= 2 && p.Rows <= 6 && leg.Width == 90 && leg.Thickness == 10 && p.CleatRootRadius == 11, "STANDARD_EQUAL_L90X90X10_DETAIL_ONLY");
            Gate(leg.Fy == 275 && (leg.Fu == 410 || leg.Fu == 430) && beam.Fy == 275 && (beam.Fu == 410 || beam.Fu == 430)
                && beam.WebThickness >= 4 && beam.WebThickness <= 16, "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
            Gate(p.Bolt.Diameter == 20 && p.Bolt.HoleDiameter == 22 && p.Bolt.TensileArea == 245
                && p.Bolt.Grade == ConnectionBoltGrade.Class8_8 && p.Bolt.ThreadedShearPlanes, "STANDARD_M20_8_8_ONLY");
            Gate(!p.BeamNotched, "NOTCHED_BEAM_NOT_QUALIFIED");
            Gate(p.BeamLaterallyRestrained, "LATERAL_RESTRAINT_REQUIRED");
            Gate(beam.Height <= 610 && p.TopOffset >= beam.FlangeThickness + beam.RootRadius
                && p.TopOffset + p.Height <= beam.Height - beam.FlangeThickness - beam.RootRadius, "CLEAT_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
            Gate(beam.ClearWeb / beam.WebThickness <= 60 * Math.Sqrt(235 / beam.Fy), "BEAM_SHEAR_BUCKLING_REQUIRES_SEPARATE_MODEL");
            Gate(p.Eccentricity - leg.Thickness - p.CleatRootRadius >= 37.0 / 2 && p.FreeEdge >= 37.0 / 2, "HARDWARE_INTERSECTS_ANGLE_ROOT_OR_EDGE");
            if (p.Code.IsNtc) Gate(!string.IsNullOrWhiteSpace(p.HoleClearanceServiceabilitySource), "NTC_HOLE_CLEARANCE_EVIDENCE_REQUIRED");
            if (issues.Count != 0) return Result();

            double v = -a.Fy, gm0 = p.Code.Plates.GammaM0, gm2 = p.Code.Plates.GammaM2;
            double beamTopEdge = p.TopOffset + 40, beamBottomEdge = beam.Height - beamTopEdge - (p.Rows - 1) * p.Pitch;
            Detail("Detail.MinimumHeight", .6 * beam.Height, p.Height, "SBE-MS5 chapter4 cleat detail", "mm");
            Detail("Detail.Pitch", p.Pitch, Math.Min(14 * Math.Min(leg.Thickness, beam.WebThickness), 200), "EN1993-1-8 Table3.3 / NTC Table4.2.XVIII", "mm");
            Value("Geometry.Eccentricity", p.Eccentricity, "mm"); Value("Geometry.SupportGauge", p.SupportGauge, "mm");
            Value("Geometry.BeamTopBoltEdge", beamTopEdge, "mm"); Value("Geometry.BeamBottomBoltEdge", beamBottomEdge, "mm");
            var positions = Enumerable.Range(0, p.Rows).Select(i => new ConnectionPoint2D(0, (p.Rows - 1) * p.Pitch / 2 - i * p.Pitch)).ToArray();
            beamBolts = ElasticBoltGroup.Calculate(positions, new PlanarConnectionActions(0, -v, 0, 0, 0, 0, -p.Eccentricity, 0, 0));
            left = ElasticBoltGroup.Calculate(positions, new PlanarConnectionActions(0, -v / 2, 0, 0, 0, 0));
            right = ElasticBoltGroup.Calculate(positions, new PlanarConnectionActions(0, -v / 2, 0, 0, 0, 0));
            foreach (var group in new[] { beamBolts, left, right }) if (!group.IsSupported) issues.AddRange(group.Diagnostics);
            if (issues.Count != 0) return Result();
            Value("BeamBolts.MzAtCentroid", beamBolts.ActionsAtCentroid.Mz, "Nmm");
            double beta = 6 * p.Eccentricity / (p.Rows * (p.Rows + 1) * p.Pitch);
            double forcePerUnit = ElasticBoltGroup.Hypot(1.0 / p.Rows, beta);
            Value("BeamBolts.BetaEccentricity", beta, "1");
            double Bearing(ConnectionPlate part, double ev, double eh, bool vertical, string label)
            {
                var end = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, part, 0,
                    vertical ? ev : eh, vertical ? eh : ev, vertical ? (double?)p.Pitch : null,
                    vertical ? null : (double?)p.Pitch, true, true, p.HoleClearanceServiceabilitySource));
                if (!end.IsComplete) { issues.AddRange(end.Diagnostics.Select(s => label + ":" + s)); return 0; }
                double capacity = end.Checks.Single().Resistance;
                if (vertical)
                {
                    var inner = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, part, 0, ev, eh, p.Pitch, null, false, true, p.HoleClearanceServiceabilitySource));
                    if (!inner.IsComplete) { issues.AddRange(inner.Diagnostics.Select(s => label + ":" + s)); return 0; }
                    capacity = Math.Min(capacity, inner.Checks.Single().Resistance);
                }
                Value(label, capacity, "N"); return capacity;
            }
            double legV = Bearing(leg, 40, p.FreeEdge, true, "CleatBearing.Vertical");
            double legH = Bearing(leg, 40, p.FreeEdge, false, "CleatBearing.Horizontal");
            var web = new ConnectionPlate(beam.Height, beam.WebThickness, beam.Fy, beam.Fu);
            double beamV = Bearing(web, Math.Min(beamTopEdge, beamBottomEdge), p.BeamEndEdge, true, "BeamBearing.Vertical");
            double beamH = Bearing(web, Math.Min(beamTopEdge, beamBottomEdge), p.BeamEndEdge, false, "BeamBearing.Horizontal");
            var strength = BoltStrength.Verify(new BoltStrengthInput(p.Code, p.Bolt, 0, 0,
                jointLength: (p.Rows - 1) * p.Pitch, holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var strengthValues);
            if (!strength.IsComplete) issues.AddRange(strength.Diagnostics);
            if (issues.Count != 0) return Result();
            double fv = strengthValues.ShearResistance;
            Value("Bolt.SingleShearResistance", fv, "N");
            Value("Bolt.BetaLongJoint", strengthValues.BetaLongJoint, "1");
            Value("BeamBolts.EquivalentGroupResistance", 2 * fv / forcePerUnit, "N");
            Value("CleatBearing.EquivalentGroupResistance", 2 / ElasticBoltGroup.Hypot(1 / (p.Rows * legV), beta / legH), "N");
            Value("BeamBearing.EquivalentGroupResistance", 1 / ElasticBoltGroup.Hypot(1 / (p.Rows * beamV), beta / beamH), "N");
            foreach (var force in beamBolts.Forces)
            {
                string prefix = "BeamBolt" + (force.Index + 1);
                Check(prefix + ".DoubleShear", force.Shear, 2 * fv, "EN1993-1-8 Table3.4,3.8; SBE4.2.1.1");
                foreach (string side in new[] { "Left", "Right" })
                {
                    Check(prefix + "." + side + "CleatBearing", ElasticBoltGroup.Hypot(force.Fx / (2 * legH), force.Fy / (2 * legV)),
                        1, "SBE4.2.1.1 elliptical directional bearing; each angle carries half", "1");
                    Value(prefix + "." + side + "HorizontalForce", force.Fx / 2, "N");
                    Value(prefix + "." + side + "VerticalForce", force.Fy / 2, "N");
                }
                Check(prefix + ".WebBearing", ElasticBoltGroup.Hypot(force.Fx / beamH, force.Fy / beamV), 1, "SBE4.2.1.1 elliptical directional bearing; beam web carries total", "1");
            }
            foreach (string side in new[] { "Left", "Right" })
            {
                var group = side == "Left" ? left : right;
                foreach (var force in group.Forces)
                {
                    Check(side + ".SupportBolt" + (force.Index + 1) + ".SingleShear", force.Shear, .8 * fv, "SBE4.2.1.2 guidance factor0.8, EN Table3.4 and3.8");
                    Check(side + ".SupportBolt" + (force.Index + 1) + ".CleatBearing", force.Shear, legV, "EN Table3.4 / NTC4.2.67, cleat ply only");
                }
            }
            Value("SupportBolts.EquivalentGroupResistance", 2 * p.Rows * .8 * fv, "N");
            double avNet = leg.Thickness * (p.Height - p.Rows * p.Bolt.HoleDiameter);
            double ant = leg.Thickness * (p.FreeEdge - p.Bolt.HoleDiameter / 2);
            double anv = leg.Thickness * (p.Height - 40 - (p.Rows - .5) * p.Bolt.HoleDiameter);
            Value("Cleat.AvNetEach", avNet, "mm2"); Value("Cleat.AntEach", ant, "mm2"); Value("Cleat.AnvEach", anv, "mm2");
            // Check both physical legs of both physical angles, each carrying V/2.
            foreach (string side in new[] { "Left", "Right" }) foreach (string part in new[] { "BeamLeg", "SupportLeg" })
            {
                string id = side + "." + part;
                Check(id + ".GrossShear", v / 2, PlasticShear.Resistance(p.Height * leg.Thickness, leg.Fy, 1.27, gm0), "SBE4.2.2, guidance1.27 nominal bending");
                Check(id + ".NetShear", v / 2, PlasticShear.Resistance(avNet, leg.Fu, gm2), "SBE4.2.2");
                Check(id + ".BlockTearing", v / 2, .5 * ant * leg.Fu / gm2 + PlasticShear.Resistance(anv, leg.Fy, gm0), "SBE4.2.2; EN1993-1-8 3.10.2 eccentric block");
            }
            double av = Math.Max(beam.Area - 2 * beam.Width * beam.FlangeThickness + (beam.WebThickness + 2 * beam.RootRadius) * beam.FlangeThickness,
                (beam.Height - 2 * beam.FlangeThickness) * beam.WebThickness);
            double net = av - p.Rows * p.Bolt.HoleDiameter * beam.WebThickness;
            double antBeam = beam.WebThickness * (p.BeamEndEdge - p.Bolt.HoleDiameter / 2);
            double anvBeam = beam.WebThickness * (beamTopEdge + (p.Rows - 1) * p.Pitch - (p.Rows - .5) * p.Bolt.HoleDiameter);
            Value("Beam.Av", av, "mm2"); Value("Beam.AvNet", net, "mm2"); Value("Beam.Ant", antBeam, "mm2"); Value("Beam.Anv", anvBeam, "mm2");
            Check("Beam.GrossShear", v, PlasticShear.Resistance(av, beam.Fy, gm0), "SBE4.2.3.1 rolled I/H web");
            Check("Beam.NetShear", v, PlasticShear.Resistance(net, beam.Fu, gm2), "SBE4.2.3.1");
            Check("Beam.BlockTearing", v, .5 * antBeam * beam.Fu / gm2 + PlasticShear.Resistance(anvBeam, beam.Fy, gm0), "SBE4.2.3.1 and example4.4 p77: unnotched beam top block path");
            return Result();
        }
    }
}
