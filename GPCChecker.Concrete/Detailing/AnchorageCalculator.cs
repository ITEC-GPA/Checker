using System;
using System.Linq;

namespace GPC.Checkers.Concrete.Detailing
{
    /// <summary>
    /// Straight tension anchorage or lap of one bar. Units mm, MPa. Fctk05 is the 5% tensile strength used for bond, capped by the caller at the
    /// C60/75 value (EC2 8.4.2(2)); α1…α5 = 1 (no favourable shape or confinement reduction).
    /// </summary>
    public sealed class AnchorageInput
    {
        public double Diameter { get; }
        /// <summary>Design stress σsd of the bar at the start of the anchorage, MPa (≤ fyd).</summary>
        public double Stress { get; }
        public double Fctk05 { get; }
        public double GammaC { get; }
        /// <summary>Good bond conditions (η1 = 1; otherwise 0.7).</summary>
        public bool GoodBond { get; }
        public double AvailableLength { get; }
        public bool Lap { get; }
        /// <summary>Percentage of lapped bars in the lap section, (0; 100].</summary>
        public double LapPercent { get; }
        /// <summary>Clear distance between the lapped bars, mm.</summary>
        public double LapClearDistance { get; }
        public bool RibbedBars { get; }
        /// <summary>αct of the bond strength (EC2 3.1.6(2)); 1 = recommended.</summary>
        public double AlphaCt { get; }
        public AnchorageInput(double diameter, double stress, double fctk05, double gammaC, bool goodBond, double availableLength, bool lap = false,
            double lapPercent = 100, double lapClearDistance = 0, bool ribbedBars = true, double alphaCt = 1)
        {
            Diameter = diameter; Stress = stress; Fctk05 = fctk05; GammaC = gammaC; GoodBond = goodBond; AvailableLength = availableLength; Lap = lap;
            LapPercent = lapPercent; LapClearDistance = lapClearDistance; RibbedBars = ribbedBars; AlphaCt = alphaCt;
        }
    }

    public sealed class AnchorageResult
    {
        public DetailingProfile Profile { get; internal set; }
        /// <summary>Design bond strength fbd = 2.25 η1 η2 αct fctk,0.05/γc, MPa.</summary>
        public double Fbd { get; internal set; }
        public double Eta1 { get; internal set; }
        public double Eta2 { get; internal set; }
        /// <summary>lb,rqd = Ø σsd/(4 fbd), mm.</summary>
        public double BasicLength { get; internal set; }
        public double Alpha6 { get; internal set; }
        /// <summary>Minimum length of the standard, mm.</summary>
        public double MinimumLength { get; internal set; }
        /// <summary>Required anchorage or lap length, mm.</summary>
        public double RequiredLength { get; internal set; }
        public double AvailableLength { get; internal set; }
        public bool LengthPassed { get; internal set; }
        /// <summary>Limit of the clear distance between lapped bars, mm (NTC: 4Ø must hold; Eurocode: beyond min(4Ø; 50 mm) the lap is lengthened).</summary>
        public double MaximumLapClearDistance { get; internal set; }
        public bool LapClearDistancePassed { get; internal set; }
        public bool Passed => LengthPassed && LapClearDistancePassed;
        public string Expression { get; internal set; }
        public string Reference { get; internal set; }
    }

    /// <summary>
    /// Anchorage and lap lengths of straight ribbed bars, transferred from ANTHEA (ConcreteAnchorageCalculator and ConcreteBond.Strength, commit fe4652c):
    /// NTC 2018: lbd = max(lb,rqd; 20Ø; 150 mm), l0 = max(α6 lb,rqd; 0.3 α6 lb,rqd; 20Ø; 200 mm), clear distance of lapped bars ≤ 4Ø.
    /// Eurocode family (EN, UNI, DS; ribbed bars only, DK NA 8.4.2(2)): lbd = max(lb,rqd; lb,min), lb,min = max(0.3 lb,rqd; 10Ø; 100 mm) (8.6);
    /// l0 = max(α6 lb,rqd; l0,min), l0,min = max(0.3 α6 lb,rqd; 15Ø; 200 mm) (8.11), lengthened by the clear distance beyond min(4Ø; 50 mm) (8.7.2(3)).
    /// α6 = (ρ1/25)^0.5 in [1; 1.5]. Fixtures: GPCChecker.Test.Concrete/Fixtures/anchorage-legacy.csv.
    /// </summary>
    public static class AnchorageCalculator
    {
        public static double BondStrength(double fctk05, double diameter, double eta1, double alphaCt, double gammaC)
        {
            if (new[] { fctk05, diameter, eta1, alphaCt, gammaC }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0) || diameter >= 132)
                throw new ArgumentException("Bond: check diameter, strength and coefficients.");
            double eta2 = diameter <= 32 ? 1 : (132 - diameter) / 100;
            return 2.25 * eta1 * eta2 * alphaCt * fctk05 / gammaC;
        }

        public static AnchorageResult Calculate(DetailingProfile profile, AnchorageInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (new[] { p.Diameter, p.Fctk05, p.GammaC, p.AlphaCt }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                || new[] { p.Stress, p.AvailableLength }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v < 0)
                || (p.Lap && (double.IsNaN(p.LapClearDistance) || double.IsInfinity(p.LapClearDistance) || p.LapClearDistance < 0
                    || double.IsNaN(p.LapPercent) || p.LapPercent <= 0 || p.LapPercent > 100)))
                throw new ArgumentException("Anchorage: check diameter, stress, materials and lengths.");
            double eta2 = p.Diameter <= 32 ? 1 : (132 - p.Diameter) / 100;
            if (eta2 <= 0) throw new ArgumentException("Bar diameter out of range for bond.");
            bool ntc = DetailingProfiles.IsNtc(profile);
            if (!p.RibbedBars) throw new NotSupportedException("Anchorage: the bond rules are implemented for ribbed bars only.");
            double eta1 = p.GoodBond ? 1 : .7;
            double fbd = BondStrength(p.Fctk05, p.Diameter, eta1, ntc ? 1 : p.AlphaCt, p.GammaC);
            double basic = p.Diameter * p.Stress / (4 * fbd);
            double alpha6 = p.Lap ? Math.Min(1.5, Math.Max(1, Math.Sqrt(p.LapPercent / 25))) : 1;
            double minimum, required, clearLimit = 4 * p.Diameter; bool clearPassed;
            string expression;
            if (ntc)
            {
                minimum = p.Lap ? Math.Max(.3 * alpha6 * basic, Math.Max(20 * p.Diameter, 200)) : Math.Max(20 * p.Diameter, 150);
                required = p.Lap ? Math.Max(alpha6 * basic, minimum) : Math.Max(basic, minimum);
                clearPassed = !p.Lap || p.LapClearDistance <= clearLimit;
                expression = p.Lap ? "l0 = max[α6 lb,rqd; 0.3 α6 lb,rqd; 20Ø; 200 mm]; clear distance ≤ 4Ø" : "lbd = max(lb,rqd; 20Ø; 150 mm)";
            }
            else
            {
                minimum = p.Lap ? Math.Max(.3 * alpha6 * basic, Math.Max(15 * p.Diameter, 200)) : Math.Max(.3 * basic, Math.Max(10 * p.Diameter, 100));
                required = p.Lap ? Math.Max(alpha6 * basic, minimum) : Math.Max(basic, minimum);
                clearLimit = Math.Min(4 * p.Diameter, 50); clearPassed = true;
                if (p.Lap && p.LapClearDistance > clearLimit) required += p.LapClearDistance - clearLimit; // 8.7.2(3)
                expression = p.Lap ? "l0 = max[α6 lb,rqd; max(0.3 α6 lb,rqd; 15Ø; 200 mm)] + clear distance beyond min(4Ø; 50 mm)"
                    : "lbd = max[lb,rqd; max(0.3 lb,rqd; 10Ø; 100 mm)]";
            }
            return new AnchorageResult
            {
                Profile = profile, Fbd = fbd, Eta1 = eta1, Eta2 = eta2, BasicLength = basic, Alpha6 = alpha6, MinimumLength = minimum, RequiredLength = required,
                AvailableLength = p.AvailableLength, LengthPassed = p.AvailableLength >= required, MaximumLapClearDistance = clearLimit, LapClearDistancePassed = clearPassed,
                Expression = expression + "; straight bars, α1…α5 = 1", Reference = DetailingProfiles.Reference(profile)
            };
        }
    }
}
