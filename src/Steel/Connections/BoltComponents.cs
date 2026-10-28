using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Force resultants for bolt strength; N/mm. Preload/slip and contact are separate.
    /// JointLength is the distance between the end fasteners in the force-transfer direction.
    /// PackingThickness is the thicker packing for a connection with two shear planes.</summary>
    public sealed class BoltStrengthInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionBolt Bolt { get; }
        public double ShearForce { get; }
        public double TensionForce { get; }
        public int ShearPlanes { get; }
        public double JointLength { get; }
        public double PackingThickness { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public BoltStrengthInput(ConnectionDesignCode code, ConnectionBolt bolt, double shearForce,
            double tensionForce, int shearPlanes = 1, double jointLength = 0, double packingThickness = 0,
            string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            ConnectionValidation.Nonnegative(shearForce, nameof(shearForce));
            ConnectionValidation.Nonnegative(tensionForce, nameof(tensionForce));
            ConnectionValidation.Nonnegative(jointLength, nameof(jointLength));
            ConnectionValidation.Nonnegative(packingThickness, nameof(packingThickness));
            if (shearPlanes < 1 || shearPlanes > 2) throw new ArgumentOutOfRangeException(nameof(shearPlanes));
            ShearForce = shearForce; TensionForce = tensionForce; ShearPlanes = shearPlanes;
            JointLength = jointLength; PackingThickness = packingThickness;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>Typed intermediate values of one bolt strength assessment. The trace and
    /// checks are derived from them; callers read these values, never trace labels.</summary>
    internal sealed class BoltStrengthValues
    {
        internal double ShearArea { get; }
        internal double AlphaV { get; }
        internal double BetaLongJoint { get; }
        internal double BetaPacking { get; }
        internal double ShearResistance { get; }
        internal double TensionResistance { get; }
        internal string Reference { get; }
        internal BoltStrengthValues(double shearArea, double alphaV, double betaLongJoint, double betaPacking,
            double shearResistance, double tensionResistance, string reference)
        {
            ShearArea = shearArea; AlphaV = alphaV; BetaLongJoint = betaLongJoint; BetaPacking = betaPacking;
            ShearResistance = shearResistance; TensionResistance = tensionResistance; Reference = reference;
        }
        /// <summary>These values, when the verification evaluated its checks; otherwise the same
        /// InvalidOperationException a lookup of the missing check raised before.</summary>
        internal BoltStrengthValues Evaluated(ConnectionVerification<BoltStrengthInput> verification)
            => verification.Checks.Count != 0 ? this : throw new InvalidOperationException("The bolt strength checks were not evaluated.");
    }

    public static class BoltStrength
    {
        public static ConnectionVerification<BoltStrengthInput> Verify(BoltStrengthInput p) => Verify(p, out _);

        internal static ConnectionVerification<BoltStrengthInput> Verify(BoltStrengthInput p, out BoltStrengthValues values)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); var b = p.Bolt;
            BoltComponentRules.RoundHole(b, issues);
            BoltComponentRules.NtcHole(p.Code, b, p.HoleClearanceServiceabilitySource, issues);
            double area = b.ThreadedShearPlanes ? b.TensileArea : Math.PI * b.Diameter * b.Diameter / 4;
            double alpha = b.ThreadedShearPlanes && b.Grade == ConnectionBoltGrade.Class10_9 ? .5 : .6;
            double betaLong = BoltLongJoint.Reduction(p.JointLength, b.Diameter);
            double betaPacking = p.PackingThickness <= b.Diameter / 3 ? 1
                : 9 / (8 + 3 * (p.PackingThickness / b.Diameter));
            double shear = BoltResistance.Shear(p.ShearPlanes, alpha, b.Fu, area, p.Code.GammaM2Connection, betaLong, betaPacking);
            double tension = BoltResistance.Tension(b.Fu, b.TensileArea, p.Code.GammaM2Connection);
            string reference = p.Code.IsNtc ? "NTC2018 4.2.8.1.1 (4.2.63-66,4.2.68); EN1993-1-8 Table3.4,3.6.1(12-13),3.8"
                : "EN1993-1-8:2005 Table3.4; 3.6.1(12-13); 3.8";
            values = new BoltStrengthValues(area, alpha, betaLong, betaPacking, shear, tension, reference);
            trace.Add(new SectionResistanceValue("ShearArea", area, "mm2"));
            trace.Add(new SectionResistanceValue("AlphaV", alpha, "1"));
            trace.Add(new SectionResistanceValue("BetaLongJoint", betaLong, "1"));
            trace.Add(new SectionResistanceValue("BetaPacking", betaPacking, "1"));
            trace.Add(new SectionResistanceValue("ShearPlanes", p.ShearPlanes, "1"));
            trace.Add(new SectionResistanceValue("JointLength", p.JointLength, "mm"));
            trace.Add(new SectionResistanceValue("PackingThickness", p.PackingThickness, "mm"));
            BoltComponentRules.Capacity(shear, "BoltShearOutsideNumericRange", issues);
            BoltComponentRules.Capacity(tension, "BoltTensionOutsideNumericRange", issues);
            double interaction = p.ShearForce / shear + p.TensionForce / (1.4 * tension);
            if (double.IsInfinity(interaction) || double.IsNaN(interaction)) issues.Add("InteractionOutsideNumericRange");
            if (issues.Count == 0)
            {
                checks.Add(new ConnectionCheck("Bolt.Shear", p.ShearForce, shear, reference));
                checks.Add(new ConnectionCheck("Bolt.Tension", p.TensionForce, tension, reference));
                checks.Add(new ConnectionCheck("Bolt.ShearTensionInteraction",
                    interaction, 1, "EN1993-1-8:2005 Table3.4 (bolt component only)", "1"));
            }
            trace.RemoveAll(v => double.IsNaN(v.Value) || double.IsInfinity(v.Value));
            return new ConnectionVerification<BoltStrengthInput>(p, p.Code, "Steel.Connection.Bolt.Strength",
                "One 8.8/10.9 bolt strength check, normal round hole, one/two identical shear planes. "
                + "Only bolt shear/tension/interaction; no bearing, punching, slip or parent-joint qualification. "
                + "NTC2018 equation 4.2.71 additionally requires the assembled plate/bearing/punching resistances.",
                checks, trace, issues, new[] { "Bolts conform to the referenced product/thread execution standards; no countersunk or fitted bolts.",
                    "JointLength is the end-fastener spacing in the load-transfer direction. The uniform-transfer exemption is not invoked.",
                    "For double shear PackingThickness is the thicker packing; both shear planes have equal thread conditions and equal assigned force.",
                    "Packing and long-joint reductions follow EN1993-1-8 also for the NTC policy; they are not stated in NTC equation 4.2.63." });
        }
    }

    /// <summary>One plate ply at one bolt. End/edge distances are measured in the direction
    /// and transverse to the considered force component. Null pitch means no neighbouring
    /// bolt in that direction; an interior bolt requires its corresponding pitch.</summary>
    public sealed class BoltBearingInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionBolt Bolt { get; }
        public ConnectionPlate Plate { get; }
        public double Force { get; }
        public double EndDistance { get; }
        public double EdgeDistance { get; }
        public double? LongitudinalPitch { get; }
        public double? TransversePitch { get; }
        public bool EndBolt { get; }
        public bool EdgeBolt { get; }
        public string HoleClearanceServiceabilitySource { get; }
        /// <summary>EN1993-1-8 Figure3.3 geometry: a single bolt position in the load-transfer direction.
        /// Do not infer this from a single transverse column of a multi-row fin plate.</summary>
        public bool SingleLapWithSingleBoltRow { get; }
        public bool HardenedWashersUnderHeadAndNut { get; }
        public BoltBearingInput(ConnectionDesignCode code, ConnectionBolt bolt, ConnectionPlate plate,
            double force, double endDistance, double edgeDistance, double? longitudinalPitch,
            double? transversePitch, bool endBolt, bool edgeBolt, string holeClearanceServiceabilitySource = null,
            bool singleLapWithSingleBoltRow = false, bool hardenedWashersUnderHeadAndNut = false)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            ConnectionValidation.Nonnegative(force, nameof(force));
            ConnectionValidation.Positive(endDistance, nameof(endDistance));
            ConnectionValidation.Positive(edgeDistance, nameof(edgeDistance));
            if (longitudinalPitch.HasValue) ConnectionValidation.Positive(longitudinalPitch.Value, nameof(longitudinalPitch));
            if (transversePitch.HasValue) ConnectionValidation.Positive(transversePitch.Value, nameof(transversePitch));
            if (!endBolt && !longitudinalPitch.HasValue || !edgeBolt && !transversePitch.HasValue)
                throw new ArgumentException("Interior bolts require the corresponding pitch.");
            Force = force; EndDistance = endDistance; EdgeDistance = edgeDistance;
            LongitudinalPitch = longitudinalPitch; TransversePitch = transversePitch;
            EndBolt = endBolt; EdgeBolt = edgeBolt;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
            SingleLapWithSingleBoltRow = singleLapWithSingleBoltRow;
            HardenedWashersUnderHeadAndNut = hardenedWashersUnderHeadAndNut;
        }
    }

    /// <summary>Typed intermediate values of one ply bearing assessment; the trace is derived from them.</summary>
    internal sealed class BoltBearingValues
    {
        internal double AlphaD { get; }
        internal double AlphaB { get; }
        internal double K1 { get; }
        internal double Resistance { get; }
        internal BoltBearingValues(double alphaD, double alphaB, double k1, double resistance)
        { AlphaD = alphaD; AlphaB = alphaB; K1 = k1; Resistance = resistance; }
    }

    public static class BoltBearing
    {
        public static ConnectionVerification<BoltBearingInput> Verify(BoltBearingInput p) => Verify(p, out _);

        internal static ConnectionVerification<BoltBearingInput> Verify(BoltBearingInput p, out BoltBearingValues values)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); var b = p.Bolt; double d0 = b.HoleDiameter;
            BoltComponentRules.RoundHole(b, issues);
            if (p.Plate.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            if (p.EndDistance < 1.2 * d0 || p.EdgeDistance < 1.2 * d0) issues.Add("MinimumEndOrEdgeDistance");
            if (p.LongitudinalPitch.HasValue && p.LongitudinalPitch.Value < 2.2 * d0
                || p.TransversePitch.HasValue && p.TransversePitch.Value < 2.4 * d0) issues.Add("MinimumBoltPitch");
            BoltComponentRules.NtcHole(p.Code, b, p.HoleClearanceServiceabilitySource, issues);
            if (p.SingleLapWithSingleBoltRow && !p.HardenedWashersUnderHeadAndNut)
                issues.Add("SingleLapSingleRowRequiresHardenedWashersUnderHeadAndNut");
            double ad = p.EndBolt ? p.EndDistance / (3 * d0) : p.LongitudinalPitch.Value / (3 * d0) - .25;
            double ab = Math.Min(1, Math.Min(ad, b.Fu / p.Plate.Fu));
            double k = p.EdgeBolt ? Math.Min(2.5, 2.8 * p.EdgeDistance / d0 - 1.7)
                : Math.Min(2.5, 1.4 * p.TransversePitch.Value / d0 - 1.7);
            if (!p.Code.IsNtc && p.EdgeBolt && p.TransversePitch.HasValue)
                k = Math.Min(k, 1.4 * p.TransversePitch.Value / d0 - 1.7);
            trace.Add(new SectionResistanceValue("AlphaD", ad, "1"));
            trace.Add(new SectionResistanceValue("AlphaB", ab, "1"));
            trace.Add(new SectionResistanceValue("K1", k, "1"));
            double bearing = k * ab * p.Plate.Fu * b.Diameter * p.Plate.Thickness / p.Code.GammaM2Connection;
            if (p.SingleLapWithSingleBoltRow)
            {
                double singleLapLimit = 1.5 * p.Plate.Fu * b.Diameter * p.Plate.Thickness / p.Code.GammaM2Connection;
                trace.Add(new SectionResistanceValue("SingleLapBearingLimit", singleLapLimit, "N"));
                bearing = Math.Min(bearing, singleLapLimit);
            }
            BoltComponentRules.Capacity(bearing, "PlateBearingOutsideNumericRange", issues);
            values = new BoltBearingValues(ad, ab, k, bearing);
            if (issues.Count == 0)
                checks.Add(new ConnectionCheck("Plate.Bearing", p.Force,
                    bearing,
                    (p.Code.IsNtc ? "NTC2018 4.2.8.1.1 (4.2.67)" : "EN1993-1-8:2005 Table3.4")
                    + (p.SingleLapWithSingleBoltRow ? "; EN1993-1-8 3.6.1(10-11)" : "")));
            trace.RemoveAll(v => double.IsNaN(v.Value) || double.IsInfinity(v.Value));
            return new ConnectionVerification<BoltBearingInput>(p, p.Code, "Steel.Connection.Bolt.PlateBearing",
                "One plate ply, one force component parallel to its edge, normal round hole. "
                + "No group redistribution or net/block/maximum-spacing checks; those belong to the joint.", checks, trace, issues,
                new[] { "Distances are aligned with the current signed force component, including the relevant end/edge after load reversal.",
                    "Single-lap/one-row geometry must be declared explicitly; its EN reduction also supplements the NTC policy.",
                    "NTC hole-clearance evidence is a caller-supplied serviceability reference, not an automatically verified SLS check." });
        }
    }

    public sealed class BoltPunchingInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate Plate { get; }
        public double TensionForce { get; }
        public double HoleDiameter { get; }
        public double MeanHeadNutDiameter { get; }
        public BoltPunchingInput(ConnectionDesignCode code, ConnectionPlate plate, double tensionForce,
            double holeDiameter, double meanHeadNutDiameter)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            ConnectionValidation.Nonnegative(tensionForce, nameof(tensionForce));
            ConnectionValidation.Positive(holeDiameter, nameof(holeDiameter));
            ConnectionValidation.Positive(meanHeadNutDiameter, nameof(meanHeadNutDiameter));
            if (meanHeadNutDiameter <= holeDiameter) throw new ArgumentException("Head/nut must cover the hole.");
            TensionForce = tensionForce; HoleDiameter = holeDiameter; MeanHeadNutDiameter = meanHeadNutDiameter;
        }
    }

    public static class BoltPunching
    {
        public static ConnectionVerification<BoltPunchingInput> Verify(BoltPunchingInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>();
            if (p.Plate.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            if (p.MeanHeadNutDiameter > p.Plate.Width) issues.Add("PunchingDiameterExceedsPlateWidth");
            double resistance = .6 * Math.PI * p.MeanHeadNutDiameter * p.Plate.Thickness * p.Plate.Fu / p.Code.GammaM2Connection;
            BoltComponentRules.Capacity(resistance, "PlatePunchingOutsideNumericRange", issues);
            var checks = new List<ConnectionCheck>();
            if (issues.Count == 0) checks.Add(new ConnectionCheck("Plate.BoltHeadPunching", p.TensionForce, resistance,
                p.Code.IsNtc ? "NTC2018 4.2.8.1.1 (4.2.70)" : "EN1993-1-8:2005 Table3.4"));
            return new ConnectionVerification<BoltPunchingInput>(p, p.Code, "Steel.Connection.Bolt.Punching",
                "Local punching under a non-countersunk head or nut. Diameter is the smaller "
                + "of the mean across-points/across-flats dimensions of head and nut; no weld/support-wall punching.",
                checks, new[] { new SectionResistanceValue("MeanHeadNutDiameter", p.MeanHeadNutDiameter, "mm") }, issues,
                new[] { "A complete punching perimeter lies within this plate; proximity to longitudinal ends is checked by the calling joint.",
                    "The mean diameter is provided from actual head/nut dimensions; a washer diameter is not substituted." });
        }
    }

    /// <summary>One bolt in single shear between two plate plies, with named head-side and nut-side plates.
    /// Component input objects share the exact code/bolt/plate snapshots and concomitant force demands.
    /// This contract does not assemble a bolt group or qualify the whole connection.</summary>
    public sealed class BoltSingleShearAssemblyInput
    {
        public BoltStrengthInput Bolt { get; }
        public BoltBearingInput HeadPlyBearing { get; }
        public BoltBearingInput NutPlyBearing { get; }
        public BoltPunchingInput HeadPlyPunching { get; }
        public BoltPunchingInput NutPlyPunching { get; }
        public BoltSingleShearAssemblyInput(BoltStrengthInput bolt, BoltBearingInput headPlyBearing,
            BoltBearingInput nutPlyBearing, BoltPunchingInput headPlyPunching, BoltPunchingInput nutPlyPunching)
        {
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            HeadPlyBearing = headPlyBearing ?? throw new ArgumentNullException(nameof(headPlyBearing));
            NutPlyBearing = nutPlyBearing ?? throw new ArgumentNullException(nameof(nutPlyBearing));
            HeadPlyPunching = headPlyPunching ?? throw new ArgumentNullException(nameof(headPlyPunching));
            NutPlyPunching = nutPlyPunching ?? throw new ArgumentNullException(nameof(nutPlyPunching));
            if (!ReferenceEquals(bolt.Code, headPlyBearing.Code) || !ReferenceEquals(bolt.Code, nutPlyBearing.Code)
                || !ReferenceEquals(bolt.Code, headPlyPunching.Code) || !ReferenceEquals(bolt.Code, nutPlyPunching.Code)
                || !ReferenceEquals(bolt.Bolt, headPlyBearing.Bolt) || !ReferenceEquals(bolt.Bolt, nutPlyBearing.Bolt))
                throw new ArgumentException("All components must share the same immutable code and bolt instances.");
            if (!ReferenceEquals(headPlyBearing.Plate, headPlyPunching.Plate) || !ReferenceEquals(nutPlyBearing.Plate, nutPlyPunching.Plate))
                throw new ArgumentException("Head-side and nut-side bearing/punching must identify their respective plate instances.");
            if (headPlyBearing.Force != bolt.ShearForce || nutPlyBearing.Force != bolt.ShearForce
                || headPlyPunching.TensionForce != bolt.TensionForce || nutPlyPunching.TensionForce != bolt.TensionForce)
                throw new ArgumentException("Single-shear plies and bolt must use the same concomitant shear and tension demands.");
            if (headPlyPunching.HoleDiameter != bolt.Bolt.HoleDiameter || nutPlyPunching.HoleDiameter != bolt.Bolt.HoleDiameter)
                throw new ArgumentException("Punching inputs must refer to the same bolt hole diameter.");
        }
    }

    public static class BoltSingleShearAssembly
    {
        public static ConnectionVerification<BoltSingleShearAssemblyInput> Verify(BoltSingleShearAssemblyInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var code = p.Bolt.Code; var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>();
            if (p.Bolt.ShearPlanes != 1) issues.Add("AssemblyRequiresSingleShearPlane");
            var bolt = BoltStrength.Verify(p.Bolt, out var boltValues);
            var headBearing = BoltBearing.Verify(p.HeadPlyBearing, out var headValues); var nutBearing = BoltBearing.Verify(p.NutPlyBearing, out var nutValues);
            var headPunching = BoltPunching.Verify(p.HeadPlyPunching); var nutPunching = BoltPunching.Verify(p.NutPlyPunching);
            void Include(string prefix, IEnumerable<ConnectionCheck> evaluated, IEnumerable<string> diagnostics)
            {
                foreach (var diagnostic in diagnostics) issues.Add(prefix + ":" + diagnostic);
                foreach (var check in evaluated)
                    checks.Add(new ConnectionCheck(prefix + "." + check.Id, check.Demand, check.Resistance, check.Reference, check.Unit));
            }
            Include("Bolt", bolt.Checks, bolt.Diagnostics);
            Include("HeadPly", headBearing.Checks, headBearing.Diagnostics); Include("NutPly", nutBearing.Checks, nutBearing.Diagnostics);
            Include("HeadPly", headPunching.Checks, headPunching.Diagnostics); Include("NutPly", nutPunching.Checks, nutPunching.Diagnostics);
            if (issues.Count == 0)
            {
                double shear = Math.Min(boltValues.ShearResistance, Math.Min(headValues.Resistance, nutValues.Resistance));
                double tension = Math.Min(boltValues.TensionResistance,
                    Math.Min(headPunching.Checks.Single().Resistance, nutPunching.Checks.Single().Resistance));
                trace.Add(new SectionResistanceValue("AssembledShearResistance", shear, "N"));
                trace.Add(new SectionResistanceValue("AssembledTensionResistance", tension, "N"));
                if (code.IsNtc)
                {
                    double interaction = p.Bolt.ShearForce / shear + p.Bolt.TensionForce / (1.4 * tension);
                    if (double.IsNaN(interaction) || double.IsInfinity(interaction)) issues.Add("AssemblyInteractionOutsideNumericRange");
                    else
                    {
                        checks.Add(new ConnectionCheck("Assembly.NtcShearTensionInteraction", interaction, 1,
                            "NTC2018 4.2.8.1.1 (4.2.71), using min(bolt shear, both ply bearing) and min(bolt tension, head/nut punching)", "1"));
                        checks.Add(new ConnectionCheck("Assembly.NtcTensionLimit", p.Bolt.TensionForce, tension,
                            "NTC2018 4.2.8.1.1 tension limitation following (4.2.71)"));
                    }
                }
            }
            return new ConnectionVerification<BoltSingleShearAssemblyInput>(p, code, "Steel.Connection.Bolt.SingleShearAssembly",
                "One bolt with two named plate plies in single shear: bolt shear/tension, bearing of both plies and head/nut punching. "
                + "No group distribution, plate net/block failure, prying, slip or complete-joint qualification.", checks, trace, issues,
                new[] { "All input forces belong to one concomitant action state after any required prying/contact calculation.",
                    "NTC interaction uses the assembled resistance minima; EC interaction remains the Table3.4 bolt check with separate plate checks.",
                    "Single-lap Figure3.3 applicability, full punching perimeter and end/edge distances must be assigned for each actual ply." });
        }
    }

    /// <summary>EN1993-1-8 3.8(1) long-joint factor betaLf for the joint length between end fasteners.</summary>
    internal static class BoltLongJoint
    {
        internal static double Reduction(double jointLength, double diameter)
            => Math.Max(.75, Math.Min(1, 1 - (jointLength - 15 * diameter) / (200 * diameter)));
    }

    /// <summary>The only implementation of the bolt shear/tension resistance formulas
    /// (EN1993-1-8 Table3.4, NTC2018 4.2.63-4.2.68). The operand order is part of the
    /// contract: every caller reproduces its previous results bit for bit.</summary>
    internal static class BoltResistance
    {
        /// <summary>Fv,Rd = n alphaV fub A / gamma * betaLf * betaP, evaluated left to right.</summary>
        internal static double Shear(int planes, double alphaV, double fub, double area, double gamma, double betaLong = 1, double betaPacking = 1)
            => planes * alphaV * fub * area / gamma * betaLong * betaPacking;
        /// <summary>Ft,Rd = 0.9 fub As / gamma.</summary>
        internal static double Tension(double fub, double tensileArea, double gamma) => .9 * fub * tensileArea / gamma;
    }

    /// <summary>The only implementation of the plastic shear resistance Vpl = A fy / (factor sqrt3 gamma)
    /// and of the net shear fracture bound A fu / (sqrt3 gamma). factor=1.27 is the SCI nominal-moment reduction.</summary>
    internal static class PlasticShear
    {
        internal static double Resistance(double area, double fy, double gamma) => area * fy / (Math.Sqrt(3) * gamma);
        internal static double Resistance(double area, double fy, double factor, double gamma) => area * fy / (factor * Math.Sqrt(3) * gamma);
    }

    internal static class BoltComponentRules
    {
        internal static void Capacity(double value, string diagnostic, IList<string> issues)
        { if (!(value > 0) || double.IsInfinity(value)) issues.Add(diagnostic); }
        internal static void NtcHole(ConnectionDesignCode code, ConnectionBolt bolt, string evidence, IList<string> issues)
        {
            if (code.IsNtc && bolt.HoleDiameter - bolt.Diameter > (bolt.Diameter <= 20 ? 1 : 1.5) + 1e-10
                && string.IsNullOrWhiteSpace(evidence)) issues.Add("NtcHoleClearanceRequiresServiceabilityEvidence");
        }
        internal static void RoundHole(ConnectionBolt b, IList<string> issues)
        {
            if (b.Diameter < 12 || b.Diameter > 36) issues.Add("BoltDiameterOutside12To36mm");
            double max = b.Diameter <= 14 ? 1 : b.Diameter <= 24 ? 2 : 3;
            if (b.HoleDiameter - b.Diameter > max + 1e-10) issues.Add("RoundHoleClearanceOutsideQualifiedRange");
        }
    }
}
