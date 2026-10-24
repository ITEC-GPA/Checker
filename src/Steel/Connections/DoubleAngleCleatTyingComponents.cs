using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Explicit CTICM/SCI SBE-MS5 accidental-tying policy. gammaMu=1.1
    /// belongs to this complementary guidance, not to an automatically selected
    /// EC/NTC national code. GammaM0 applies only to the yielding term of block
    /// tearing and must be supplied with its own design-context source.</summary>
    public sealed class SbeAccidentalTyingPolicy
    {
        public double GammaMu => 1.1;
        public double GammaM0 { get; }
        public string DesignSituationSource { get; }
        public string GammaM0Source { get; }
        public string MethodId => "CTICM.SCI.SBE_MS5.AccidentalTying";
        public string Reference => "CTICM/SCI SBE-MS5 section4.3 pp63-67; example4.4 pp78-82";
        private SbeAccidentalTyingPolicy(string situation, double gammaM0, string gammaM0Source)
        {
            ConnectionValidation.Positive(gammaM0, nameof(gammaM0));
            GammaM0 = gammaM0; DesignSituationSource = situation; GammaM0Source = gammaM0Source;
        }
        public static SbeAccidentalTyingPolicy SbeMs5(string accidentalDesignSituationSource,
            double gammaM0, string gammaM0Source)
            => new SbeAccidentalTyingPolicy(accidentalDesignSituationSource, gammaM0, gammaM0Source);
    }

    /// <summary>Two identical mirrored L90x90x10 cleats in the initial SBE family.
    /// X points along the beam away from the support, Y upward, Z normal to the
    /// web. Origin is on the support face at CLEAT mid-height and web midplane.
    /// Thus the beam centroid is at Y=Height/2+50-Beam.Height/2, not generally0.
    /// The one beam bolt column is at X=50; the two support columns are at
    /// Z=+/-SupportGauge/2. Dimensions mm, strengths MPa. Geometry sources identify
    /// the actual identical products; this type contains no support member.
    /// Hardware widths are actual head/nut/washer dimensions, not bolt diameter.</summary>
    public sealed class DoubleAngleCleatTyingGeometry
    {
        public ConnectionPlate CleatLeg { get; }
        public double CleatRootRadius { get; }
        public ConnectionRolledISection Beam { get; }
        public ConnectionBolt Bolt { get; }
        public int Rows { get; }
        public double Pitch => 70;
        public double EndDistance => 40;
        public double FreeEdge => 40;
        public double Height => 2 * EndDistance + (Rows - 1) * Pitch;
        public double TopOffset => 50;
        public double Gap => 10;
        public double BeamBoltX => CleatLeg.Width - FreeEdge;
        public double BeamEndEdge => BeamBoltX - Gap;
        public double SupportGauge => Beam.WebThickness + 2 * BeamBoltX;
        public double BeamTopBoltEdge => TopOffset + EndDistance;
        public double BeamBottomBoltEdge => Beam.Height - BeamTopBoltEdge - (Rows - 1) * Pitch;
        public double BeamCentroidY => Height / 2 + TopOffset - Beam.Height / 2;
        public double SupportContactWidth { get; }
        public double BeamHardwareEnvelopeDiameter { get; }
        public bool BeamNotched { get; }
        public string GeometrySource { get; }
        public string MaterialSource { get; }
        public string HardwareSource { get; }
        public DoubleAngleCleatTyingGeometry(ConnectionPlate cleatLeg, double cleatRootRadius,
            ConnectionRolledISection beam, ConnectionBolt bolt, int rows, double supportContactWidth,
            double beamHardwareEnvelopeDiameter, string geometrySource, string materialSource,
            string hardwareSource, bool beamNotched = false)
        {
            CleatLeg = cleatLeg ?? throw new ArgumentNullException(nameof(cleatLeg));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            ConnectionValidation.Nonnegative(cleatRootRadius, nameof(cleatRootRadius));
            ConnectionValidation.Positive(supportContactWidth, nameof(supportContactWidth));
            ConnectionValidation.Positive(beamHardwareEnvelopeDiameter, nameof(beamHardwareEnvelopeDiameter));
            if (rows < 1 || rows > 100) throw new ArgumentOutOfRangeException(nameof(rows));
            if (cleatLeg.Width <= cleatLeg.Thickness + cleatRootRadius)
                throw new ArgumentException("The angle needs a straight leg beyond its root.", nameof(cleatLeg));
            CleatRootRadius = cleatRootRadius; Rows = rows; SupportContactWidth = supportContactWidth;
            BeamHardwareEnvelopeDiameter = beamHardwareEnvelopeDiameter; GeometrySource = geometrySource;
            MaterialSource = materialSource; HardwareSource = hardwareSource; BeamNotched = beamNotched;
        }
    }

    /// <summary>Independent accidental action state. The complete six-component
    /// wrench is transported from its actual application point to the cleat-centre
    /// origin. Only centred positive Fx, or the null case, is in this first scope.
    /// No ordinary load, Passed certificate or national-code inference is accepted.</summary>
    public sealed class DoubleAngleCleatTyingInput
    {
        public SbeAccidentalTyingPolicy Policy { get; }
        public DoubleAngleCleatTyingGeometry Geometry { get; }
        public PlanarConnectionActions Actions { get; }
        public DoubleAngleCleatTyingInput(SbeAccidentalTyingPolicy policy,
            DoubleAngleCleatTyingGeometry geometry, PlanarConnectionActions actions)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }
    }

    public sealed class DoubleAngleCleatTyingResult
    {
        public DoubleAngleCleatTyingInput Input { get; }
        public PlanarConnectionActions ActionsAtCleatCentre { get; }
        public ConnectionStatus CoveredComponentsStatus { get; }
        public bool IsComponentAssessmentComplete => CoveredComponentsStatus != ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ConnectionCheck GoverningCheck => IsComponentAssessmentComplete
            ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;
        public double? ComponentTyingResistance => IsComponentAssessmentComplete
            ? (double?)Checks.Min(c => c.Resistance) : null;
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "SUPPORT_FLANGE_OR_WEB_LOCAL_TYING_RESISTANCE_AND_CONTACT_COMPATIBILITY",
            "LOCAL_PUNCHING_BENEATH_HEADS_NUTS_AND_WASHERS",
            "ACTUAL_SUPPORT_BOLT_FORCE_DISTRIBUTION_INCLUDING_PRYING",
            "SUPPORT_MEMBER_ACTIONS_AND_FORCE_TRANSFER",
            "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS" });
        public IReadOnlyList<string> Assumptions { get; } = Array.AsReadOnly(new[] {
            "SBE accidental tying is separate from ordinary shear/N+V. The external design situation establishes the tying demand; the action is not derived from a member axial force.",
            "gammaMu=1.1 is selected complementary CTICM/SCI guidance. gammaM0 and its provenance are supplied separately for the fy term of block tearing. No NTC equivalence is inferred.",
            "One shared beam bolt column transfers total F/n per bolt through two equal shear planes; each cleat receives F/2. No packing, unequal cleats, staggered/oversized/slotted/countersunk holes or blind bolts.",
            "The T-stub modes are alternative resistances of the PAIR of cleats, including possible prying. No actual support-bolt tensile force or prying force is inferred as F/(2n).",
            "The beam-side long-joint length is zero in the longitudinal tying direction because all bolt centres share X=50. The vertical row span belongs to the distinct ordinary-shear direction.",
            "Both longitudinal block paths of the cleats are checked. For the unnotched beam only SBE web Case1 applies; cleat Case2 does not imply a notched beam.",
            "Hardware fit is a geometric condition, not a punching or execution qualification. No rigid-support certificate, full-joint resistance, ordinary Passed, rotation capacity or global robustness assessment is produced." });
        public string Scope => "CTICM/SCI SBE-MS5 accidental centred tying of two identical standard L90x90x10 cleats: "
            + "cleat/support-bolt T-stub modes, beam bolts in double shear, beam-leg/web bearing, cleat block paths, web net tension and web block Case1. "
            + "COMPONENTS ONLY: no supporting-member resistance, local punching, actual support-bolt prying forces, ordinary N+V or whole-joint qualification.";
        internal DoubleAngleCleatTyingResult(DoubleAngleCleatTyingInput input, PlanarConnectionActions centred,
            IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            Input = input; ActionsAtCleatCentre = centred;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            var evaluated = Diagnostics.Count == 0 ? checks.ToArray() : Array.Empty<ConnectionCheck>();
            Checks = Array.AsReadOnly(evaluated); Trace = Array.AsReadOnly(trace.ToArray());
            CoveredComponentsStatus = Diagnostics.Count != 0 || evaluated.Length == 0 ? ConnectionStatus.NotSupported
                : evaluated.All(c => c.Passed) ? ConnectionStatus.Passed : ConnectionStatus.Failed;
        }
    }

    public static class DoubleAngleCleatTyingComponents
    {
        public const string MethodId = "Steel.Connection.DoubleAngleCleat.AccidentalTyingComponents";
        public static DoubleAngleCleatTyingResult Verify(DoubleAngleCleatTyingInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var g = p.Geometry; var a = p.Actions; var beam = g.Beam; var leg = g.CleatLeg; var bolt = g.Bolt;
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); PlanarConnectionActions centred = null;
            Required(p.Policy.DesignSituationSource, "ACCIDENTAL_TYING_DESIGN_SITUATION_REQUIRED");
            Required(p.Policy.GammaM0Source, "BLOCK_YIELD_GAMMA_M0_SOURCE_REQUIRED");
            Required(g.GeometrySource, "ACTUAL_DOUBLE_ANGLE_GEOMETRY_SOURCE_REQUIRED");
            Required(g.MaterialSource, "ACTUAL_PRODUCT_MATERIAL_SOURCE_REQUIRED");
            Required(g.HardwareSource, "ACTUAL_CONTACT_HARDWARE_SOURCE_REQUIRED");
            double mx = a.Mx + a.ApplicationY * a.Fz - a.ApplicationZ * a.Fy;
            double my = a.My + a.ApplicationZ * a.Fx - a.ApplicationX * a.Fz;
            double mz = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            var original = new[] { a.Fx, a.Fy, a.Fz, a.Mx, a.My, a.Mz, a.ApplicationX, a.ApplicationY, a.ApplicationZ };
            var names = new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz", "ApplicationX", "ApplicationY", "ApplicationZ" };
            for (int j = 0; j < original.Length; j++) Value("Actions." + names[j], original[j], j < 3 ? "N" : j < 6 ? "Nmm" : "mm");
            Value("Actions.CentredMx", mx, "Nmm"); Value("Actions.CentredMy", my, "Nmm"); Value("Actions.CentredMz", mz, "Nmm");
            if (Finite(mx) && Finite(my) && Finite(mz)) centred = new PlanarConnectionActions(a.Fx, a.Fy, a.Fz, mx, my, mz);
            Gate(a.Fx >= 0 && a.Fy == 0 && a.Fz == 0 && mx == 0 && my == 0 && mz == 0,
                "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
            Gate(g.Rows >= 2 && g.Rows <= 6 && leg.Width == 90 && leg.Thickness == 10 && g.CleatRootRadius == 11,
                "STANDARD_IDENTICAL_L90X90X10_DETAIL_ONLY");
            Gate(leg.Fy == 275 && (leg.Fu == 410 || leg.Fu == 430) && beam.Fy == 275 && (beam.Fu == 410 || beam.Fu == 430)
                && beam.WebThickness >= 4 && beam.WebThickness <= 16, "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
            Gate(bolt.Diameter == 20 && bolt.HoleDiameter == 22 && bolt.TensileArea == 245
                && bolt.Grade == ConnectionBoltGrade.Class8_8 && bolt.ThreadedShearPlanes, "STANDARD_M20_8_8_ONLY");
            Gate(!g.BeamNotched, "NOTCHED_BEAM_NOT_QUALIFIED");
            Gate(beam.Height <= 610 && g.TopOffset >= beam.FlangeThickness + beam.RootRadius
                && g.TopOffset + g.Height <= beam.Height - beam.FlangeThickness - beam.RootRadius,
                "CLEAT_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
            Gate(g.Height >= .6 * beam.Height, "CLEAT_HEIGHT_OUTSIDE_STANDARD_SBE_DETAIL");
            double minimumWebEdge = Math.Min(g.BeamTopBoltEdge, g.BeamBottomBoltEdge);
            double rootClearance = g.BeamBoltX - leg.Thickness - g.CleatRootRadius;
            Gate(g.BeamEndEdge >= 1.2 * bolt.HoleDiameter && minimumWebEdge >= 1.2 * bolt.HoleDiameter
                && g.FreeEdge >= 1.2 * bolt.HoleDiameter && g.EndDistance >= 1.2 * bolt.HoleDiameter
                && g.Pitch >= 2.4 * bolt.HoleDiameter
                && g.Pitch <= Math.Min(14 * Math.Min(leg.Thickness, beam.WebThickness), 200),
                "BOLT_END_EDGE_OR_PITCH_OUTSIDE_DETAIL_SCOPE");
            Gate(g.SupportContactWidth >= bolt.HoleDiameter && g.BeamHardwareEnvelopeDiameter >= bolt.HoleDiameter
                && g.SupportContactWidth / 2 <= Math.Min(rootClearance, Math.Min(g.FreeEdge, g.EndDistance))
                && g.SupportContactWidth <= Math.Min(g.Pitch, g.SupportGauge)
                && g.BeamHardwareEnvelopeDiameter / 2 <= Math.Min(rootClearance,
                    Math.Min(g.FreeEdge, Math.Min(g.EndDistance, Math.Min(g.BeamEndEdge, minimumWebEdge))))
                && g.BeamHardwareEnvelopeDiameter <= g.Pitch,
                "HARDWARE_INTERSECTS_ROOT_EDGE_OR_OTHER_HARDWARE");
            Value("Geometry.Height", g.Height, "mm"); Value("Geometry.BeamCentroidY", g.BeamCentroidY, "mm");
            Value("Geometry.SupportGauge", g.SupportGauge, "mm"); Value("Geometry.BeamBoltX", g.BeamBoltX, "mm");
            Value("Geometry.BeamEndEdge", g.BeamEndEdge, "mm"); Value("Geometry.BeamTopBoltEdge", g.BeamTopBoltEdge, "mm");
            Value("Geometry.BeamBottomBoltEdge", g.BeamBottomBoltEdge, "mm"); Value("Geometry.BeamMinimumVerticalEdge", minimumWebEdge, "mm");
            Value("Geometry.SupportContactWidth", g.SupportContactWidth, "mm");
            Value("Geometry.BeamHardwareEnvelopeDiameter", g.BeamHardwareEnvelopeDiameter, "mm");
            Value("Policy.GammaMu", p.Policy.GammaMu, "1"); Value("Policy.GammaM0", p.Policy.GammaM0, "1");
            Value("Material.CleatFy", leg.Fy, "MPa"); Value("Material.CleatFu", leg.Fu, "MPa");
            Value("Material.BeamFy", beam.Fy, "MPa"); Value("Material.BeamFu", beam.Fu, "MPa");
            if (issues.Count != 0) return Result();

            double d0 = bolt.HoleDiameter, gm = p.Policy.GammaMu;
            double end = Math.Min(g.EndDistance, (g.SupportGauge - beam.WebThickness - 2 * g.CleatRootRadius + d0) / 2);
            double pitch = Math.Min(g.Pitch, g.SupportGauge - beam.WebThickness - 2 * g.CleatRootRadius + d0);
            double length = 2 * end + (g.Rows - 1) * pitch;
            double m = (g.SupportGauge - beam.WebThickness - 2 * leg.Thickness - 1.6 * g.CleatRootRadius) / 2;
            double n = Math.Min(g.FreeEdge, 1.25 * m), ew = g.SupportContactWidth / 4;
            double denominator = 2 * m * n - ew * (m + n);
            Gate(end > 0 && pitch > 0 && m > d0 / 2 && n > 0 && denominator > 0
                && g.SupportContactWidth < 2 * Math.Min(m, g.FreeEdge), "EQUIVALENT_TSTUB_GEOMETRY_OUTSIDE_SCOPE");
            double moment = .25 * length * leg.Thickness * leg.Thickness * leg.Fu / gm;
            double ft = .9 * bolt.Fu * bolt.TensileArea / gm, sumFt = 2 * g.Rows * ft;
            double fv = .6 * bolt.Fu * bolt.TensileArea / gm;
            Value("Geometry.EffectiveEnd", end, "mm"); Value("Geometry.EffectivePitch", pitch, "mm");
            Value("Geometry.EffectiveLength", length, "mm"); Value("Geometry.M", m, "mm");
            Value("Geometry.N", n, "mm"); Value("Geometry.ContactEw", ew, "mm");
            Value("Cleat.UltimatePlasticMoment", moment, "Nmm"); Value("Bolt.SingleTensileResistance", ft, "N");
            Value("SupportBolts.TotalTensileResistance", sumFt, "N"); Value("BeamBolt.SingleShearPlaneResistance", fv, "N");
            Value("BeamBolt.LongJointLengthInTyingDirection", 0, "mm"); Value("BeamBolt.BetaLongJoint", 1, "1");
            Value("BeamBolt.TotalForce", a.Fx / g.Rows, "N"); Value("BeamBolt.EachShearPlaneForce", a.Fx / (2 * g.Rows), "N");
            Value("EachCleat.TyingForce", a.Fx / 2, "N");
            if (issues.Count != 0) return Result();
            var modes = TStubMechanismArithmetic.Calculate(moment, moment, m, n, ew, sumFt,
                true, TStubMode1Method.DistributedContact);
            if (!modes.IsFinitePositive) { issues.Add("TSTUB_RESISTANCES_OUTSIDE_NUMERIC_RANGE"); return Result(); }
            Check("Tying.Cleats.Mode1", modes.PlateMode1, "4.3.1.1; EN1993-1-8 Table6.2 arithmetic, explicit tying policy");
            Check("Tying.Cleats.Mode2", modes.CombinedMode2.Value, "4.3.1.1; EN1993-1-8 Table6.2 arithmetic, explicit tying policy");
            Check("Tying.Cleats.Mode3", modes.BoltsMode3, "4.3.1.1");
            Check("Tying.BeamBolts.DoubleShear", 2 * g.Rows * fv, "4.3.1.2; Lj measured along tying direction, EN1993-1-8 3.8");

            double kAngle = Math.Min(2.5, Math.Min(2.8 * g.EndDistance / d0 - 1.7, 1.4 * g.Pitch / d0 - 1.7));
            double kWeb = Math.Min(2.5, Math.Min(2.8 * minimumWebEdge / d0 - 1.7, 1.4 * g.Pitch / d0 - 1.7));
            double alphaAngle = Math.Min(1, Math.Min(g.FreeEdge / (3 * d0), bolt.Fu / leg.Fu));
            double alphaWeb = Math.Min(1, Math.Min(g.BeamEndEdge / (3 * d0), bolt.Fu / beam.Fu));
            Value("Bearing.Cleat.K1", kAngle, "1"); Value("Bearing.Cleat.Alpha", alphaAngle, "1");
            Value("Bearing.Web.K1", kWeb, "1"); Value("Bearing.Web.Alpha", alphaWeb, "1");
            Check("Tying.Cleats.Bearing", 2 * g.Rows * kAngle * alphaAngle * leg.Fu * bolt.Diameter * leg.Thickness / gm, "4.3.1.3");
            Check("Tying.BeamWeb.Bearing", g.Rows * kWeb * alphaWeb * beam.Fu * bolt.Diameter * beam.WebThickness / gm, "4.3.2.1");
            Block("Tying.Cleats.BlockCase1", 2 * leg.Thickness * (g.Rows - 1) * (g.Pitch - d0),
                4 * leg.Thickness * (g.FreeEdge - d0 / 2), leg.Fy, leg.Fu, "4.3.1.4 Case1, both cleats");
            Block("Tying.Cleats.BlockCase2", 2 * leg.Thickness * (g.EndDistance + (g.Rows - 1) * g.Pitch - (g.Rows - .5) * d0),
                2 * leg.Thickness * (g.FreeEdge - d0 / 2), leg.Fy, leg.Fu, "4.3.1.4 Case2, both cleats; either symmetric free end");
            double beamNet = beam.WebThickness * (g.Height - g.Rows * d0);
            Value("BeamWeb.NetArea", beamNet, "mm2");
            Check("Tying.BeamWeb.NetTension", .9 * beamNet * beam.Fu / gm, "4.3.2.2");
            Block("Tying.BeamWeb.BlockCase1", beam.WebThickness * (g.Rows - 1) * (g.Pitch - d0),
                2 * beam.WebThickness * (g.BeamEndEdge - d0 / 2), beam.Fy, beam.Fu, "4.3.2.3 Case1, unnotched beam only");
            return Result();

            void Block(string id, double ant, double anv, double yield, double ultimate, string reference)
            {
                Value(id + ".Ant", ant, "mm2"); Value(id + ".Anv", anv, "mm2");
                double tension = ant * ultimate / gm, shear = anv * yield / (Math.Sqrt(3) * p.Policy.GammaM0);
                Value(id + ".TensionTerm", tension, "N"); Value(id + ".ShearYieldTerm", shear, "N");
                Gate(ant > 0 && anv > 0, id + ".NONPOSITIVE_BLOCK_AREA");
                Check(id, tension + shear, reference);
            }
            void Required(string source, string reason) { if (string.IsNullOrWhiteSpace(source)) issues.Add(reason); }
            void Gate(bool condition, string reason) { if (!condition) issues.Add(reason); }
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + ".OUTSIDE_NUMERIC_RANGE"); }
            void Check(string id, double resistance, string reference)
            {
                if (resistance <= 0 || !Finite(resistance)) issues.Add(id + ".RESISTANCE_OUTSIDE_NUMERIC_RANGE");
                else checks.Add(new ConnectionCheck(id, a.Fx, resistance, "CTICM/SCI SBE-MS5 " + reference));
            }
            DoubleAngleCleatTyingResult Result() => new DoubleAngleCleatTyingResult(p, centred, checks, trace, issues);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
