using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Static bearing-type double-cover plate splice. References and geometry in README.txt.
    /// Equal elastic bolt forces give a conservative individual-fastener check, with no assumed plastic redistribution.</summary>
    public static class DoubleCoverSplice
    {
        public static DoubleCoverSpliceResult Verify(DoubleCoverSpliceInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            void Value(string name, double value, string unit) { ConnectionValidation.Nonnegative(Math.Abs(value), name); trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Require(bool condition, string code) { if (!condition) issues.Add(code); }
            var a = p.Actions; var b = p.Bolt; var c = p.Code;
            Require(a.N >= 0 && a.V1 == 0 && a.V2 == 0 && a.T == 0 && a.M1 == 0 && a.M2 == 0, "ConcentricTensionOnly");
            Require(p.Main.Fy <= 460 && p.Cover.Fy <= 460, "CarbonSteelUpToS460Only");
            Require(p.Cover.Width <= p.Main.Width, "CoverWiderThanMainPlate");
            Require(b.Diameter >= 12 && b.Diameter <= 36, "BoltDiameterOutside12To36mm");
            double clearance = b.HoleDiameter - b.Diameter;
            // Declared normal round holes: upper bound from the nominal-clearance bands.
            // M12/M14 with 2 mm clearance need the special 3.6.1(5) reduction/group check;
            // that configuration is deliberately outside this initial method.
            double normalClearance = b.Diameter <= 14 ? 1 : b.Diameter <= 24 ? 2 : 3;
            Require(clearance <= normalClearance + 1e-10, "RoundHoleClearanceOutsideQualifiedRange");
            if (c.IsNtc && clearance > (b.Diameter <= 20 ? 1 : 1.5) + 1e-10)
                Require(!string.IsNullOrWhiteSpace(p.HoleClearanceServiceabilitySource), "NtcHoleClearanceRequiresServiceabilityEvidence");
            // Only tensile plates: buckling between fasteners is not applicable. Weathering steel is excluded.
            double eMain = (p.Main.Width - p.Gauge) / 2, eCover = (p.Cover.Width - p.Gauge) / 2;
            Require(p.MainEnd >= 1.2 * b.HoleDiameter && p.CoverEnd >= 1.2 * b.HoleDiameter
                && eMain >= 1.2 * b.HoleDiameter && eCover >= 1.2 * b.HoleDiameter, "MinimumEndOrEdgeDistance");
            Require(p.Gauge >= 2.4 * b.HoleDiameter && (p.Rows == 1 || p.Pitch >= 2.2 * b.HoleDiameter), "MinimumBoltPitch");
            double tMin = Math.Min(p.Main.Thickness, p.Cover.Thickness);
            double maxPitch = Math.Min(14 * tMin, 200);
            Require(p.Gauge <= maxPitch && (p.Rows == 1 || p.Pitch <= maxPitch), "MaximumBoltPitch");
            if (p.Exposed)
                Require(p.MainEnd <= 4 * tMin + 40 && p.CoverEnd <= 4 * tMin + 40
                    && eMain <= 4 * tMin + 40 && eCover <= 4 * tMin + 40, "MaximumExposedEdgeDistance");
            double lj = (p.Rows - 1.0) * p.Pitch;
            Value("BoltCountPerSide", 2 * p.Rows, "1"); Value("TotalBoltCount", 4 * p.Rows, "1");
            Value("MainAxialForce", a.N, "N"); Value("ForcePerCover", a.N / 2, "N");
            Value("ForcePerBolt", a.N / (2 * p.Rows), "N"); Value("Lj", lj, "mm");
            Value("CoverLength", 2 * (p.CoverEnd + lj + p.MainEnd) + p.ButtGap, "mm");
            Value("GammaM0", c.Plates.GammaM0, "1"); Value("GammaM2Plate", c.Plates.GammaM2, "1");
            Value("GammaM2Connection", c.GammaM2Connection, "1");
            if (issues.Count != 0) return new DoubleCoverSpliceResult(p, checks, trace, issues);

            var boltComponent = BoltStrength.Verify(new BoltStrengthInput(c, b, a.N / (2 * p.Rows), 0,
                shearPlanes: 2, jointLength: lj, holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var boltValues);
            if (!boltComponent.IsComplete)
                return new DoubleCoverSpliceResult(p, checks, trace, boltComponent.Diagnostics.ToList());
            Value("BoltShearArea", boltValues.ShearArea, "mm2");
            Value("AlphaV", boltValues.AlphaV, "1");
            Value("BetaLongJoint", boltValues.BetaLongJoint, "1");
            double boltRd = boltValues.ShearResistance;
            string boltRef = boltValues.Reference;
            for (int side = 1; side <= 2; side++)
                for (int row = 1; row <= p.Rows; row++)
                    for (int column = 1; column <= 2; column++)
                    {
                        string id = "Side" + side + ".Row" + row + ".Column" + column;
                        checks.Add(new ConnectionCheck(id + ".BoltShear", a.N / (2 * p.Rows), boltRd, boltRef));
                        Bearing(id + ".MainBearing", p.Main, p.MainEnd, eMain, row == 1, a.N / (2 * p.Rows));
                        // Covers are identical: this per-cover check applies independently to each of the two.
                        Bearing(id + ".EachCoverBearing", p.Cover, p.CoverEnd, eCover, row == p.Rows, a.N / (4 * p.Rows));
                    }
            if (issues.Count != 0) return new DoubleCoverSpliceResult(p, checks, trace, issues);
            Plate("Main", p.Main, p.MainEnd, eMain, a.N);
            Plate("Cover1", p.Cover, p.CoverEnd, eCover, a.N / 2);
            Plate("Cover2", p.Cover, p.CoverEnd, eCover, a.N / 2);
            return new DoubleCoverSpliceResult(p, checks, trace, issues);

            void Bearing(string id, ConnectionPlate plate, double e1, double e2, bool endBolt, double demand)
            {
                var component = BoltBearing.Verify(new BoltBearingInput(c, b, plate, demand, e1, e2,
                    p.Rows > 1 ? (double?)p.Pitch : null, p.Gauge, endBolt, edgeBolt: true,
                    holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var bearingValues);
                if (!component.IsComplete)
                {
                    issues.AddRange(component.Diagnostics.Select(d => id + ":" + d));
                    return;
                }
                Value(id + ".AlphaB", bearingValues.AlphaB, "1");
                Value(id + ".K1", bearingValues.K1, "1");
                var bearing = component.Checks.Single();
                checks.Add(new ConnectionCheck(id, demand, bearing.Resistance, bearing.Reference));
            }
            void Plate(string id, ConnectionPlate plate, double end, double edge, double demand)
            {
                double ag = plate.Width * plate.Thickness, an = (plate.Width - 2 * b.HoleDiameter) * plate.Thickness;
                double antInside = (p.Gauge - b.HoleDiameter) * plate.Thickness;
                double antOutside = (2 * edge - b.HoleDiameter) * plate.Thickness;
                double anv = 2 * plate.Thickness * (end + lj - (p.Rows - .5) * b.HoleDiameter);
                Value(id + ".Ag", ag, "mm2"); Value(id + ".Anet", an, "mm2");
                Value(id + ".AntInside", antInside, "mm2"); Value(id + ".AntOutside", antOutside, "mm2"); Value(id + ".Anv", anv, "mm2");
                string tensileRef = c.IsNtc ? "NTC2018 4.2.4.1.2.1" : "EN1993-1-1 6.2.3";
                checks.Add(new ConnectionCheck(id + ".GrossYielding", demand, SectionResistanceArithmetic.Normal(ag, plate.Fy, c.Plates.GammaM0), tensileRef));
                checks.Add(new ConnectionCheck(id + ".NetRupture", demand, SectionResistanceArithmetic.NetTension(an, plate.Fu, c.Plates.GammaM2), tensileRef));
                // Both symmetric paths use the assigned plate fracture factor (SCI P358 p179).
                checks.Add(new ConnectionCheck(id + ".BlockInside", demand, Block(antInside), "EN1993-1-8 3.10.2(2); SCI P358 p179"));
                checks.Add(new ConnectionCheck(id + ".BlockOutside", demand, Block(antOutside), "EN1993-1-8 3.10.2(2); SCI P358 p179"));
                double Block(double ant) => plate.Fu * ant / c.Plates.GammaM2 + PlasticShear.Resistance(plate.Fy, anv, c.Plates.GammaM0);
            }
        }
    }
}
