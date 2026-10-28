using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Explicit complementary SCI accidental-tying model, not the ordinary
    /// EC3/NTC material policy. gammaMu=1.1 is a SCI recommendation, not an EN/NTC
    /// partial factor inferred from a selected national code. The external design
    /// situation must establish that the action is a robustness tying force.</summary>
    public sealed class SciAccidentalTyingPolicy
    {
        public double GammaMu => 1.1;
        public string MethodId => "SCI.P358.2014.AccidentalTying";
        public string DesignSituationSource { get; }
        public string Reference => "SCI P358(2014) AppendixA/B and Checks11/12; SCI AD396(2016)";
        private SciAccidentalTyingPolicy(string designSituationSource)
        { DesignSituationSource = designSituationSource; }
        public static SciAccidentalTyingPolicy P3582014(string accidentalDesignSituationSource)
            => new SciAccidentalTyingPolicy(accidentalDesignSituationSource);
    }

    /// <summary>Partial-depth end plate welded only to the two faces of an unnotched
    /// beam web. Dimensions mm, strengths MPa. No supporting-member model is contained.
    /// Frame: X transverse, Y upward, Z along the beam away from the support. Origin
    /// is at the beam section centroid depth on the support-contact plane. Plate
    /// centre is (0, Beam.Height/2-50-Height/2, Plate.Thickness/2), not automatically
    /// the beam centroid. The initial detail fixes p70, e1=40, e2=30, top offset50.
    /// ContactWidth is actual washer diameter or head/nut width across points on
    /// the end-plate side, not nominal bolt diameter or hole diameter.</summary>
    public sealed class FlexibleEndPlateTyingGeometry
    {
        public ConnectionPlate Plate { get; }
        public ConnectionBolt Bolt { get; }
        public ConnectionRolledISection Beam { get; }
        public int Rows { get; }
        public double Gauge { get; }
        public double Height => 80 + (Rows - 1) * 70;
        public double Pitch => 70;
        public double EndDistance => 40;
        public double FreeEdge => (Plate.Width - Gauge) / 2;
        public double PlateTopOffset => 50;
        public double PlateCentreX => 0;
        public double PlateCentreY => Beam.Height / 2 - PlateTopOffset - Height / 2;
        public double PlateCentreZ => Plate.Thickness / 2;
        public double WeldThroat { get; }
        public double ContactWidth { get; }
        public bool BeamNotched { get; }
        public string GeometrySource { get; }
        public string ContactHardwareSource { get; }
        public FlexibleEndPlateTyingGeometry(ConnectionPlate plate, ConnectionBolt bolt,
            ConnectionRolledISection beam, int rows, double gauge, double weldThroat,
            double contactWidth, string geometrySource, string contactHardwareSource, bool beamNotched = false)
        {
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            if (rows < 1 || rows > 100) throw new ArgumentOutOfRangeException(nameof(rows));
            ConnectionValidation.Positive(gauge, nameof(gauge));
            ConnectionValidation.Positive(weldThroat, nameof(weldThroat));
            ConnectionValidation.Positive(contactWidth, nameof(contactWidth));
            if (plate.Width <= gauge || gauge <= beam.WebThickness)
                throw new ArgumentException("Bolt columns must lie outside the beam web and inside the plate.");
            Rows = rows; Gauge = gauge; WeldThroat = weldThroat; ContactWidth = contactWidth;
            GeometrySource = geometrySource; ContactHardwareSource = contactHardwareSource; BeamNotched = beamNotched;
        }
    }

    /// <summary>Independent accidental action state in the Geometry frame. Actions
    /// are transported from their stated application point to the physical plate
    /// centre by Mcentre=Mpoint+(rpoint-rcentre)xF. Only centred positive Fz, or
    /// the null case, is in this first field. Ordinary actions/results are not inputs.</summary>
    public sealed class FlexibleEndPlateTyingInput
    {
        public SciAccidentalTyingPolicy Policy { get; }
        public FlexibleEndPlateTyingGeometry Geometry { get; }
        public PlanarConnectionActions Actions { get; }
        public FlexibleEndPlateTyingInput(SciAccidentalTyingPolicy policy,
            FlexibleEndPlateTyingGeometry geometry, PlanarConnectionActions actions)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }
    }

    /// <summary>Only SCI Checks11/12. Incomplete supporting/weld mechanisms stay
    /// explicit even when all assessed component checks pass.</summary>
    public sealed class FlexibleEndPlateTyingResult
    {
        public FlexibleEndPlateTyingInput Input { get; }
        public ConnectionStatus Status { get; }
        public bool IsComponentAssessmentComplete { get; }
        public bool CanQualifyWholeJoint => false;
        public PlanarConnectionActions ActionsAtPlateCentre { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY",
            "SUPPORT_LOCAL_TYING_RESISTANCE_AND_CONTACT_COMPATIBILITY",
            "SUPPORT_MEMBER_ACTIONS_AND_FORCE_TRANSFER",
            "ACTUAL_HEAD_NUT_WASHER_BEARING_AND_PUNCHING",
            "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS" });
        public IReadOnlyList<string> Assumptions { get; } = Array.AsReadOnly(new[] {
            "SCI P358 accidental tying is a distinct situation from ordinary shear or N+V; the caller establishes the tying action and its case.",
            "gammaMu=1.1 is explicitly selected SCI guidance. No national-code selection supplies this factor automatically.",
            "The three T-stub modes assess the whole end-plate group. Prying is implicit in resistance; no actual bolt tension or prying force is returned.",
            "AD396 supports the P358 end-plate model even for a deformable support; it does not verify that support or permit omission of Check14.",
            "Weld throat locates yield lines only. It is not a weld strength, consumable or failure-hierarchy verification.",
            "No supporting-member rigidity is assumed as a certified property; the support is outside this component result.",
            "No ordinary Passed certificate, frame pinned model, rotation capacity, or numerical stiffness is inferred." });
        public ConnectionCheck GoverningCheck => IsComponentAssessmentComplete
            ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;
        public double? ComponentTyingResistance => IsComponentAssessmentComplete ? (double?)Checks.Min(c => c.Resistance) : null;
        public string Scope => "SCI P358(2014) partial-depth plate/bolt group in accidental tying (Check11) and supported unnotched beam web (Check12), two columns M20 8.8, standard S275 detail. No weld or support verification, ordinary N+V, full-depth end plate or whole-joint qualification.";
        /// <summary>Typed resistances of the checks Tying.EndPlate.Mode1 and Tying.BeamWeb.Tension; null when not evaluated.</summary>
        internal double? EndPlateMode1Resistance { get; }
        internal double? BeamWebTensionResistance { get; }
        internal FlexibleEndPlateTyingResult(FlexibleEndPlateTyingInput input,
            PlanarConnectionActions centred, List<ConnectionCheck> checks,
            List<SectionResistanceValue> trace, List<string> diagnostics, double? endPlateMode1 = null, double? beamWebTension = null)
        {
            EndPlateMode1Resistance = endPlateMode1; BeamWebTensionResistance = beamWebTension;
            Input = input; ActionsAtPlateCentre = centred;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            var evaluated = ConnectionOutcome.Retained(checks, Diagnostics.Count);
            Checks = Array.AsReadOnly(evaluated); Trace = Array.AsReadOnly(trace.ToArray());
            IsComponentAssessmentComplete = ConnectionOutcome.IsComplete(evaluated, Diagnostics.Count);
            Status = ConnectionOutcome.Status(evaluated, Diagnostics.Count);
        }
    }

    public static class FlexibleEndPlateTyingComponents
    {
        public static FlexibleEndPlateTyingResult Verify(FlexibleEndPlateTyingInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var g = p.Geometry; var a = p.Actions; var beam = g.Beam; var plate = g.Plate;
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); PlanarConnectionActions centred = null; double? mode1 = null, web = null;
            Required(p.Policy.DesignSituationSource, "ACCIDENTAL_TYING_DESIGN_SITUATION_REQUIRED");
            Required(g.GeometrySource, "ACTUAL_PARTIAL_DEPTH_GEOMETRY_SOURCE_REQUIRED");
            Required(g.ContactHardwareSource, "ACTUAL_END_PLATE_CONTACT_HARDWARE_SOURCE_REQUIRED");
            double dx = a.ApplicationX - g.PlateCentreX, dy = a.ApplicationY - g.PlateCentreY,
                dz = a.ApplicationZ - g.PlateCentreZ;
            double mx = a.Mx + dy * a.Fz - dz * a.Fy;
            double my = a.My + dz * a.Fx - dx * a.Fz;
            double mz = a.Mz + dx * a.Fy - dy * a.Fx;
            Value("Geometry.PlateCentreX", g.PlateCentreX, "mm");
            Value("Geometry.PlateCentreY", g.PlateCentreY, "mm");
            Value("Geometry.PlateCentreZ", g.PlateCentreZ, "mm");
            Value("Actions.CentredMx", mx, "Nmm"); Value("Actions.CentredMy", my, "Nmm"); Value("Actions.CentredMz", mz, "Nmm");
            Value("Actions.Fx", a.Fx, "N"); Value("Actions.Fy", a.Fy, "N"); Value("Actions.Fz", a.Fz, "N");
            if (Finite(mx) && Finite(my) && Finite(mz))
                centred = new PlanarConnectionActions(a.Fx, a.Fy, a.Fz, mx, my, mz,
                    g.PlateCentreX, g.PlateCentreY, g.PlateCentreZ);
            Gate(a.Fx == 0 && a.Fy == 0 && a.Fz >= 0 && mx == 0 && my == 0 && mz == 0,
                "CENTRED_ACCIDENTAL_TYING_ONLY_NO_SHEAR_OR_MOMENT");
            Gate(!g.BeamNotched, "NOTCHED_BEAM_NOT_QUALIFIED");
            Gate(g.Rows >= 2 && g.Rows <= 6 && (g.Gauge == 90 || g.Gauge == 140)
                && plate.Width == g.Gauge + 60 && (plate.Thickness == 10 || plate.Thickness == 12),
                "STANDARD_PARTIAL_DEPTH_DETAIL_REQUIRED");
            Gate(plate.Fy == 275 && plate.Fu == 410 && beam.Fy == 275 && beam.Fu == 410
                && beam.WebThickness >= 4 && beam.WebThickness <= 16, "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
            Gate(g.Bolt.Diameter == 20 && g.Bolt.HoleDiameter == 22 && g.Bolt.TensileArea == 245
                && g.Bolt.Grade == ConnectionBoltGrade.Class8_8 && g.Bolt.ThreadedShearPlanes, "STANDARD_M20_8_8_ONLY");
            Gate(beam.Height <= 610 && g.PlateTopOffset >= beam.FlangeThickness + beam.RootRadius
                && g.PlateTopOffset + g.Height <= beam.Height - beam.FlangeThickness - beam.RootRadius,
                "PLATE_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
            Gate(g.Height >= .6 * beam.Height, "PLATE_HEIGHT_OUTSIDE_STANDARD_SCI_DETAIL");
            Gate(g.WeldThroat >= 3 && g.WeldThroat <= Math.Min(plate.Thickness, beam.WebThickness) / Math.Sqrt(2),
                "WELD_GEOMETRY_OUTSIDE_FILLET_SCOPE");
            double toeDistance = (g.Gauge - beam.WebThickness) / 2 - g.WeldThroat * Math.Sqrt(2);
            Gate(g.ContactWidth >= g.Bolt.HoleDiameter && g.ContactWidth / 2 <= Math.Min(toeDistance,
                Math.Min(g.FreeEdge, g.EndDistance)), "CONTACT_HARDWARE_INTERSECTS_WELD_OR_PLATE_EDGE");
            if (issues.Count != 0) return Result();

            double pitchLimit = g.Gauge - beam.WebThickness - 2 * g.WeldThroat * Math.Sqrt(2) + g.Bolt.HoleDiameter;
            double endLimit = pitchLimit / 2;
            double effectiveEnd = Math.Min(g.EndDistance, endLimit), effectivePitch = Math.Min(g.Pitch, pitchLimit);
            double length = 2 * effectiveEnd + (g.Rows - 1) * effectivePitch;
            double m = (g.Gauge - beam.WebThickness - 2 * .8 * g.WeldThroat * Math.Sqrt(2)) / 2;
            double n = Math.Min(g.FreeEdge, 1.25 * m), ew = g.ContactWidth / 4;
            Gate(m > g.Bolt.HoleDiameter / 2 && g.FreeEdge >= 1.2 * g.Bolt.HoleDiameter
                && effectiveEnd > 0 && effectivePitch > 0 && g.ContactWidth < 2 * Math.Min(m, g.FreeEdge),
                "EQUIVALENT_TSTUB_GEOMETRY_OUTSIDE_SCOPE");
            double moment = .25 * length * plate.Thickness * plate.Thickness * plate.Fu / p.Policy.GammaMu;
            double boltResistance = BoltResistance.Tension(g.Bolt.Fu, g.Bolt.TensileArea, p.Policy.GammaMu);
            double totalBolt = 2 * g.Rows * boltResistance;
            Value("Geometry.EffectiveEnd", effectiveEnd, "mm"); Value("Geometry.EffectivePitch", effectivePitch, "mm");
            Value("Geometry.EffectiveEndLimit", endLimit, "mm"); Value("Geometry.EffectivePitchLimit", pitchLimit, "mm");
            Value("Geometry.EffectiveLength", length, "mm"); Value("Geometry.M", m, "mm");
            Value("Geometry.N", n, "mm"); Value("Geometry.ContactEw", ew, "mm");
            Value("Geometry.Height", g.Height, "mm"); Value("Geometry.BoltRows", g.Rows, "1");
            Value("Geometry.BoltCount", 2 * g.Rows, "1");
            Value("Policy.GammaMu", p.Policy.GammaMu, "1"); Value("Plate.UltimateStrength", plate.Fu, "MPa");
            Value("Plate.UltimatePlasticMoment", moment, "Nmm");
            Value("Bolt.SingleTensileResistance", boltResistance, "N");
            Value("Bolt.TotalTensileResistance", totalBolt, "N");
            if (issues.Count != 0) return Result();
            var modes = TStubMechanismArithmetic.Calculate(moment, moment, m, n, ew, totalBolt,
                true, TStubMode1Method.DistributedContact);
            if (!modes.IsFinitePositive) { issues.Add("TSTUB_RESISTANCES_OUTSIDE_NUMERIC_RANGE"); return Result(); }
            if (Check("Tying.EndPlate.Mode1", modes.PlateMode1, "SCI P358(2014) Check11, AppendixA/B; AD396")) mode1 = modes.PlateMode1;
            Check("Tying.EndPlate.Mode2", modes.CombinedMode2.Value, "SCI P358(2014) Check11, AppendixA/B; AD396");
            Check("Tying.EndPlate.Mode3", modes.BoltsMode3, "SCI P358(2014) Check11, AppendixA/B");
            double webTension = beam.WebThickness * g.Height * beam.Fu / p.Policy.GammaMu;
            if (Check("Tying.BeamWeb.Tension", webTension, "SCI P358(2014) Check12, unnotched unperforated supported beam web")) web = webTension;
            return Result();

            void Required(string source, string diagnostic) { if (string.IsNullOrWhiteSpace(source)) issues.Add(diagnostic); }
            void Gate(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + "OutsideNumericRange"); }
            bool Check(string id, double resistance, string reference)
            {
                if (!(resistance > 0) || !Finite(resistance)) { issues.Add(id + "OutsideNumericRange"); return false; }
                checks.Add(new ConnectionCheck(id, a.Fz, resistance, reference)); return true;
            }
            FlexibleEndPlateTyingResult Result() => new FlexibleEndPlateTyingResult(p, centred, checks, trace, issues, mode1, web);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
