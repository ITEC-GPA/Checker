using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One rectangular gusset between two identical flat braces, one through-bolt group.
    /// Local X runs from the gusset free end towards the brace free ends, Y spans plate width,
    /// and Z is normal to the gusset. Positive Fx is the total force exerted by the bolts
    /// on the two braces; the gusset receives the opposite force. Both bodies are in tension.
    /// The bolt-group centroid is the origin. Units N, mm and MPa. Gusset and brace free
    /// ends are on opposite ends of the overlap; Rows count longitudinal positions,
    /// with two bolts per position at Y=+/-Gauge/2. There is no butt and no second group.</summary>
    public sealed class GussetTwinFlatTensionInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate Gusset { get; }
        public ConnectionPlate EachBrace { get; }
        public ConnectionBolt Bolt { get; }
        public PlanarConnectionActions Actions { get; }
        public int Rows { get; }
        public double Pitch { get; }
        public double Gauge { get; }
        public double GussetEnd { get; }
        public double BraceEnd { get; }
        public bool Exposed { get; }
        public string HoleClearanceServiceabilitySource { get; }

        public GussetTwinFlatTensionInput(ConnectionDesignCode code, ConnectionPlate gusset,
            ConnectionPlate eachBrace, ConnectionBolt bolt, PlanarConnectionActions actions,
            int rows, double pitch, double gauge, double gussetEnd, double braceEnd,
            bool exposed = false, string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Gusset = gusset ?? throw new ArgumentNullException(nameof(gusset));
            EachBrace = eachBrace ?? throw new ArgumentNullException(nameof(eachBrace));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (rows < 1 || rows > 1000) throw new ArgumentOutOfRangeException(nameof(rows));
            ConnectionValidation.Positive(pitch, nameof(pitch));
            ConnectionValidation.Positive(gauge, nameof(gauge));
            ConnectionValidation.Positive(gussetEnd, nameof(gussetEnd));
            ConnectionValidation.Positive(braceEnd, nameof(braceEnd));
            Rows = rows; Pitch = pitch; Gauge = gauge; GussetEnd = gussetEnd;
            BraceEnd = braceEnd; Exposed = exposed;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>ULS load transfer in a single symmetric bolted gusset/flat-brace overlap.
    /// The unperforated continuations of the plates are the assessment boundaries;
    /// the attachment of the gusset to a beam/column is deliberately not qualified.</summary>
    public static class GussetTwinFlatTension
    {
        public static ConnectionVerification<GussetTwinFlatTensionInput> Verify(GussetTwinFlatTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>();
            var issues = new List<string>();
            var a = p.Actions; var c = p.Code; var b = p.Bolt;
            double mx = a.Mx + a.ApplicationY * a.Fz - a.ApplicationZ * a.Fy;
            double my = a.My + a.ApplicationZ * a.Fx - a.ApplicationX * a.Fz;
            double mz = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            Require(Finite(mx) && Finite(my) && Finite(mz), "ActionTransportOutsideNumericRange");
            Require(a.Fx >= 0 && a.Fy == 0 && a.Fz == 0 && mx == 0 && my == 0 && mz == 0,
                "ConcentricTensionAtGroupCentroidRequired");
            Require(p.Gusset.Fy <= 460 && p.EachBrace.Fy <= 460, "CarbonSteelUpToS460Only");
            double lj = (p.Rows - 1.0) * p.Pitch;
            double overlap = p.GussetEnd + lj + p.BraceEnd;
            double eg = (p.Gusset.Width - p.Gauge) / 2;
            double eb = (p.EachBrace.Width - p.Gauge) / 2;
            double tmin = Math.Min(p.Gusset.Thickness, p.EachBrace.Thickness);
            Require(Finite(lj) && Finite(overlap), "GeometryOutsideNumericRange");
            Require(p.GussetEnd >= 1.2 * b.HoleDiameter && p.BraceEnd >= 1.2 * b.HoleDiameter
                && eg >= 1.2 * b.HoleDiameter && eb >= 1.2 * b.HoleDiameter, "MinimumEndOrEdgeDistance");
            Require(p.Gauge >= 2.4 * b.HoleDiameter && (p.Rows == 1 || p.Pitch >= 2.2 * b.HoleDiameter),
                "MinimumBoltPitch");
            double maximumPitch = Math.Min(14 * tmin, 200);
            Require(p.Gauge <= maximumPitch && (p.Rows == 1 || p.Pitch <= maximumPitch), "MaximumBoltPitch");
            if (p.Exposed)
                Require(p.GussetEnd <= 4 * tmin + 40 && p.BraceEnd <= 4 * tmin + 40
                    && eg <= 4 * tmin + 40 && eb <= 4 * tmin + 40, "MaximumExposedEdgeDistance");
            Value("BoltCount", 2 * p.Rows, "1"); Value("ShearPlanes", 2, "1");
            Value("GussetForce", a.Fx, "N"); Value("EachBraceForce", a.Fx / 2, "N");
            Value("JointLength", lj, "mm"); Value("OverlapLength", overlap, "mm");
            Value("MomentAtGroupX", mx, "Nmm"); Value("MomentAtGroupY", my, "Nmm");
            Value("MomentAtGroupZ", mz, "Nmm");
            Value("GammaM0", c.Plates.GammaM0, "1"); Value("GammaM2Plate", c.Plates.GammaM2, "1");
            Value("GammaM2Connection", c.GammaM2Connection, "1");
            if (issues.Count != 0) return Result();

            var positions = new List<ConnectionPoint2D>();
            for (int row = 0; row < p.Rows; row++)
            {
                double x = (row - (p.Rows - 1) / 2.0) * p.Pitch;
                positions.Add(new ConnectionPoint2D(x, -p.Gauge / 2));
                positions.Add(new ConnectionPoint2D(x, p.Gauge / 2));
            }
            var distribution = ElasticBoltGroup.Calculate(positions, a);
            if (!distribution.IsSupported)
            {
                issues.AddRange(distribution.Diagnostics.Select(d => "BoltGroup:" + d));
                return Result();
            }
            foreach (var f in distribution.Forces)
            {
                string id = "Bolt" + (f.Index + 1);
                Value(id + ".X", f.Position.X, "mm"); Value(id + ".Y", f.Position.Y, "mm");
                Value(id + ".Fx", f.Fx, "N"); Value(id + ".Fy", f.Fy, "N");
                var strength = BoltStrength.Verify(new BoltStrengthInput(c, b, f.Shear, 0,
                    shearPlanes: 2, jointLength: lj,
                    holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource));
                if (!strength.IsComplete)
                {
                    issues.AddRange(strength.Diagnostics.Select(d => id + ":" + d));
                    continue;
                }
                var shear = strength.Checks.Single(k => k.Id == "Bolt.Shear");
                Check(id + ".Shear", f.Shear, shear.Resistance, shear.Reference);
                int row = f.Index / 2;
                Bearing(id + ".GussetBearing", p.Gusset, p.GussetEnd, eg, row == 0, f.Shear);
                Bearing(id + ".Brace1Bearing", p.EachBrace, p.BraceEnd, eb, row == p.Rows - 1, f.Shear / 2);
                Bearing(id + ".Brace2Bearing", p.EachBrace, p.BraceEnd, eb, row == p.Rows - 1, f.Shear / 2);
            }
            if (issues.Count != 0) return Result();
            Plate("Gusset", p.Gusset, p.GussetEnd, eg, a.Fx);
            Plate("Brace1", p.EachBrace, p.BraceEnd, eb, a.Fx / 2);
            Plate("Brace2", p.EachBrace, p.BraceEnd, eb, a.Fx / 2);
            return Result();

            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            {
                if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit));
                else issues.Add(name + ":OutsideNumericRange");
            }
            void Check(string id, double demand, double resistance, string reference)
            {
                if (!Finite(resistance) || !(resistance > 0) || !Finite(demand))
                    issues.Add(id + ":OutsideNumericRange");
                else checks.Add(new ConnectionCheck(id, demand, resistance, reference));
            }
            void Bearing(string id, ConnectionPlate plate, double end, double edge, bool endBolt, double force)
            {
                var bearing = BoltBearing.Verify(new BoltBearingInput(c, b, plate, force, end, edge,
                    p.Rows > 1 ? (double?)p.Pitch : null, p.Gauge, endBolt, edgeBolt: true,
                    holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource));
                if (!bearing.IsComplete) issues.AddRange(bearing.Diagnostics.Select(d => id + ":" + d));
                else
                {
                    var k = bearing.Checks.Single();
                    Check(id, force, k.Resistance, k.Reference);
                    foreach (var v in bearing.Trace) Value(id + "." + v.Name, v.Value, v.Unit);
                }
            }
            void Plate(string id, ConnectionPlate plate, double end, double edge, double force)
            {
                double ag = plate.Width * plate.Thickness;
                double an = (plate.Width - 2 * b.HoleDiameter) * plate.Thickness;
                double antInside = (p.Gauge - b.HoleDiameter) * plate.Thickness;
                double antOutside = (2 * edge - b.HoleDiameter) * plate.Thickness;
                double anv = 2 * plate.Thickness * (end + lj - (p.Rows - .5) * b.HoleDiameter);
                Value(id + ".Ag", ag, "mm2"); Value(id + ".Anet", an, "mm2");
                Value(id + ".AntInside", antInside, "mm2"); Value(id + ".AntOutside", antOutside, "mm2");
                Value(id + ".Anv", anv, "mm2");
                string normalRef = c.IsNtc ? "NTC2018 4.2.4.1.2.1" : "EN1993-1-1:2005 6.2.3";
                Check(id + ".GrossYielding", force, SectionResistanceArithmetic.Normal(ag, plate.Fy, c.Plates.GammaM0), normalRef);
                Check(id + ".NetRupture", force, SectionResistanceArithmetic.NetTension(an, plate.Fu, c.Plates.GammaM2), normalRef);
                string blockRef = "EN1993-1-8:2005 3.10.2(2); SCI P358 p179 (symmetric paths)";
                Check(id + ".BlockInside", force, Block(antInside), blockRef);
                Check(id + ".BlockOutside", force, Block(antOutside), blockRef);
                double Block(double ant) => plate.Fu * ant / c.Plates.GammaM2
                    + plate.Fy * anv / (Math.Sqrt(3) * c.Plates.GammaM0);
            }
            ConnectionVerification<GussetTwinFlatTensionInput> Result()
                => new ConnectionVerification<GussetTwinFlatTensionInput>(p, c,
                    "Steel.Connection.Gusset.TwinFlatTension",
                    "One symmetric double-shear bolted overlap: central rectangular gusset and two identical flat braces, "
                    + "concentric tension, ULS plate/bolt strength only. Support attachment and supporting members, "
                    + "slip/deformation, compression, stability, fatigue and seismic ductility remain outside this component.",
                    checks, trace, issues, new[]
                    {
                        "The two braces are identical, in direct symmetric contact on opposite gusset faces; no packing, gap, bolt preload or contact-bearing resistance is credited.",
                        "Gusset free end is the negative-X end of the group; brace free ends are the positive-X end. Bolt forces in the trace act on the two braces; the gusset receives opposite forces. Each brace transfers N/2 and the central gusset transfers N.",
                        "Actions are transported to the bolt-group centroid before checking concentricity. A nonzero residual eccentric moment is not discarded or absorbed by an effective net section.",
                        "The plate widths describe constant rectangular unnotched strips, continuing beyond the group. No Whitmore spreading width or support resistance is inferred.",
                        "Block tearing uses the EN1993-1-8 supplementary model also under NTC2018; it is not identified as an explicit NTC block-tearing equation.",
                        "Non-weathering carbon steel and ordinary static loading; plate geometries and strengths refer to the actual product and thickness.",
                        "Support and brace-body interfaces must independently transfer the reported resultants before this component can contribute to a qualified complete joint."
                    });
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
