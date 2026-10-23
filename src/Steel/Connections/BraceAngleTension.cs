using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Names the physical leg in the supplied section, independently of its length or local orientation.</summary>
    public enum BraceAngleConnectedLeg { First, Second }

    /// <summary>One angle section, mm and MPa. GrossArea is the actual catalogue/geometric area,
    /// including fillets; it is not replaced by the area of a sharp-corner approximation.</summary>
    public sealed class BraceAngleSection
    {
        public double FirstLeg { get; }
        public double SecondLeg { get; }
        public double Thickness { get; }
        public double GrossArea { get; }
        public double Fy { get; }
        public double Fu { get; }
        public BraceAngleSection(double firstLeg, double secondLeg, double thickness,
            double grossArea, double fy, double fu)
        {
            ConnectionValidation.Positive(firstLeg, nameof(firstLeg));
            ConnectionValidation.Positive(secondLeg, nameof(secondLeg));
            ConnectionValidation.Positive(thickness, nameof(thickness));
            ConnectionValidation.Positive(grossArea, nameof(grossArea));
            ConnectionValidation.Positive(fy, nameof(fy)); ConnectionValidation.Positive(fu, nameof(fu));
            if (thickness >= Math.Min(firstLeg, secondLeg)) throw new ArgumentException("Angle thickness must be smaller than both legs.");
            if (fu < fy) throw new ArgumentException("fu must not be below fy.");
            if (grossArea > firstLeg * secondLeg) throw new ArgumentException("Angle area exceeds its rectangular envelope.");
            FirstLeg = firstLeg; SecondLeg = secondLeg; Thickness = thickness;
            GrossArea = grossArea; Fy = fy; Fu = fu;
        }
    }

    /// <summary>One angle in axial tension, connected through a single longitudinal bolt line on one leg.
    /// Tension is the force in THIS angle, never the total force in a double-angle member.
    /// ToeEdgeDistance is e2 from the connected-leg toe to the bolt centre. Pitch is p1.
    /// For a short-leg connection with 2+ bolts, EquivalentShortLegGrossArea is the actual
    /// area of the equal-leg angle of the shorter size and same thickness required by 3.10.3;
    /// this is a resistance-model area, not a second physical member.</summary>
    public sealed class BraceAngleTensionInput
    {
        public ConnectionDesignCode Code { get; }
        public BraceAngleSection Section { get; }
        public BraceAngleConnectedLeg ConnectedLeg { get; }
        public ConnectionBolt Bolt { get; }
        public double Tension { get; }
        public int BoltCount { get; }
        public double Pitch { get; }
        public double ToeEdgeDistance { get; }
        public double? EquivalentShortLegGrossArea { get; }
        public string EquivalentSectionSource { get; }
        public BraceAngleTensionInput(ConnectionDesignCode code, BraceAngleSection section,
            BraceAngleConnectedLeg connectedLeg, ConnectionBolt bolt, double tension,
            int boltCount, double pitch, double toeEdgeDistance,
            double? equivalentShortLegGrossArea = null, string equivalentSectionSource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            if (!Enum.IsDefined(typeof(BraceAngleConnectedLeg), connectedLeg)) throw new ArgumentOutOfRangeException(nameof(connectedLeg));
            ConnectionValidation.Nonnegative(tension, nameof(tension));
            if (boltCount < 1 || boltCount > 1000) throw new ArgumentOutOfRangeException(nameof(boltCount));
            ConnectionValidation.Positive(pitch, nameof(pitch));
            ConnectionValidation.Positive(toeEdgeDistance, nameof(toeEdgeDistance));
            if (equivalentShortLegGrossArea.HasValue)
                ConnectionValidation.Positive(equivalentShortLegGrossArea.Value, nameof(equivalentShortLegGrossArea));
            ConnectedLeg = connectedLeg; Tension = tension; BoltCount = boltCount;
            Pitch = pitch; ToeEdgeDistance = toeEdgeDistance;
            EquivalentShortLegGrossArea = equivalentShortLegGrossArea;
            EquivalentSectionSource = equivalentSectionSource;
        }
    }

    /// <summary>Gross yielding and effective net-section rupture of one angle according to
    /// EN1993-1-8:2005 3.10.3. The eccentricity allowance in that clause applies to this
    /// section resistance only; it does not certify gusset bending, fasteners or supports.</summary>
    public static class BraceAngleTension
    {
        public static ConnectionVerification<BraceAngleTensionInput> Verify(BraceAngleTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var s = p.Section; var b = p.Bolt;
            double connected = p.ConnectedLeg == BraceAngleConnectedLeg.First ? s.FirstLeg : s.SecondLeg;
            double other = p.ConnectedLeg == BraceAngleConnectedLeg.First ? s.SecondLeg : s.FirstLeg;
            bool shortLeg = connected < other;
            Require(s.Fy <= 460, "CarbonSteelUpToS460Only");
            BoltComponentRules.RoundHole(b, issues);
            Require(p.ToeEdgeDistance >= 1.2 * b.HoleDiameter, "MinimumToeEdgeDistance");
            Require(connected - p.ToeEdgeDistance - b.HoleDiameter / 2 >= s.Thickness, "BoltHoleIntersectsHeel");
            Require(p.BoltCount == 1 || p.Pitch >= 2.2 * b.HoleDiameter, "MinimumBoltPitch");
            double actualNetArea = s.GrossArea - b.HoleDiameter * s.Thickness;
            Require(actualNetArea > 0 && Finite(actualNetArea), "NonpositiveNetArea");
            double modelGrossArea = s.GrossArea;
            if (shortLeg && p.BoltCount > 1)
            {
                Require(p.EquivalentShortLegGrossArea.HasValue
                    && !string.IsNullOrWhiteSpace(p.EquivalentSectionSource), "ShortLegEquivalentSectionRequired");
                if (p.EquivalentShortLegGrossArea.HasValue)
                {
                    modelGrossArea = p.EquivalentShortLegGrossArea.Value;
                    Require(modelGrossArea <= s.GrossArea && modelGrossArea <= connected * connected,
                        "EquivalentSectionAreaExceedsPhysicalBounds");
                }
            }
            double modelNetArea = modelGrossArea - b.HoleDiameter * s.Thickness;
            Require(modelNetArea > 0 && Finite(modelNetArea), "NonpositiveEquivalentNetArea");
            double beta = p.BoltCount == 1 ? 1 : (p.BoltCount == 2 ? .4 : .5)
                + Math.Max(0, Math.Min(1, (p.Pitch / b.HoleDiameter - 2.5) / 2.5))
                * (p.BoltCount == 2 ? .3 : .2);
            double effectiveNet = p.BoltCount == 1
                ? 2 * (p.ToeEdgeDistance - .5 * b.HoleDiameter) * s.Thickness
                : beta * modelNetArea;
            double reducedResistance = effectiveNet * s.Fu / p.Code.Plates.GammaM2;
            double grossResistance = s.GrossArea * s.Fy / p.Code.Plates.GammaM0;
            Value("ConnectedLeg", connected, "mm"); Value("UnconnectedLeg", other, "mm");
            Value("ConnectedToShortLeg", shortLeg ? 1 : 0, "1");
            Value("GrossArea", s.GrossArea, "mm2"); Value("ActualNetArea", actualNetArea, "mm2");
            Value("ModelGrossArea", modelGrossArea, "mm2"); Value("ModelNetArea", modelNetArea, "mm2");
            Value("Beta", beta, "1"); Value("EffectiveNetArea", effectiveNet, "mm2");
            Value("GammaM0", p.Code.Plates.GammaM0, "1"); Value("GammaM2Plate", p.Code.Plates.GammaM2, "1");
            Require(Finite(reducedResistance) && reducedResistance > 0
                && Finite(grossResistance) && grossResistance > 0, "ResistanceOutsideNumericRange");
            if (issues.Count == 0)
            {
                string sectionRef = p.Code.IsNtc ? "NTC2018 4.2.4.1.2.1" : "EN1993-1-1:2005 6.2.3";
                checks.Add(new ConnectionCheck("Angle.GrossYielding", p.Tension, grossResistance, sectionRef));
                checks.Add(new ConnectionCheck("Angle.EffectiveNetRupture", p.Tension, reducedResistance,
                    "EN1993-1-8:2005 3.10.3 " + (p.BoltCount == 1 ? "(3.11)" : p.BoltCount == 2 ? "(3.12), Table3.8" : "(3.13), Table3.8")));
            }
            return new ConnectionVerification<BraceAngleTensionInput>(p, p.Code,
                "Steel.Connection.Brace.AngleOneLegTension",
                "One angle gross/effective-net tensile section resistance with one longitudinal bolt line on one leg. "
                + "No bolt/bearing/block-tearing, gusset/support, compression, out-of-plane joint equilibrium or whole-member qualification.",
                checks, trace, issues, new[]
                {
                    "Section dimensions and gross areas are supplied from the actual section definition/catalogue; strengths apply to its product thickness.",
                    "EN1993-1-8 3.10.3 accounts for eccentricity and shear lag in the angle's tensile section resistance only; it does not remove eccentric actions from other connected parts.",
                    "For an unequal angle connected on its short leg with at least two bolts, the equal-short-leg section area and its source must be supplied explicitly.",
                    "Under NTC2018 the effective net-section method is explicitly supplementary EN1993-1-8, using the NTC material factors; it is not represented as an NTC equation.",
                    "Bolt end distances, root/washer clearance, hole-clearance serviceability, bearing, block tearing and support attachment require their own connection checks."
                });

            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            {
                if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit));
                else issues.Add(name + ":OutsideNumericRange");
            }
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
