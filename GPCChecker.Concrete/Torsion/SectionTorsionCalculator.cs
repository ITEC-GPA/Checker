using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Shear;

namespace GPC.Checkers.Concrete.Torsion
{
    /// <summary>
    /// Torsion of a section with closed links and its interaction with the shear of both directions on the same cot θ.
    /// NTC 2018 transferred from ANTHEA ConcreteTorsionCalculator (commit fe4652c, fixtures GPCChecker.Test.Concrete/Fixtures/torsion-legacy.csv)
    /// with N/Nmm units. The other profiles follow the same thin-walled truss with the strut strength and interaction of their standard.
    /// No prestress, no inclined links, no favourable contribution of the concrete without links.
    /// </summary>
    public static class SectionTorsionCalculator
    {
        public const string MethodId = "Concrete.SectionTorsion";

        /// <summary>
        /// Computes the shear of each direction with <see cref="SectionShearCalculator"/> and the common cot θ, then the torsion check.
        /// A null direction is allowed only when it carries no shear. Its cot θ is replaced by the torsion one; for DIN the admissible
        /// range of each direction is checked with VEd,T+V (shear flow of the torsion added to the shear, wall height taken as z).
        /// </summary>
        public static SectionTorsionResult Calculate(SectionTorsionInput p, SectionShearInput axis1, SectionShearInput axis2)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var profile = TorsionProfiles.Resolve(p.Standard);
            Validate(p, profile);
            if (p.LinkLegArea == 0) return Evaluate(p, null, null);
            var range = new List<ShearCalculationDetail>();
            TorsionShearComponent Component(SectionShearInput axis, int index)
            {
                if (axis == null) return null;
                if (axis.Standard.GetType() != p.Standard.GetType()) throw new ArgumentException("Torsion: the shear of axis " + index + " uses another standard.");
                if (axis.Asw <= 0) throw new ArgumentException("Torsion: the shear of axis " + index + " has no shear reinforcement (closed links required).");
                double demand = axis.V;
                if (profile == TorsionProfile.DinEN1992p11)
                {
                    double z = axis.LeverFactor * axis.D;
                    demand = Math.Abs(axis.V) + Math.Abs(p.T) * z * axis.Bw / (2 * p.Geometry.EnclosedArea * p.Geometry.Thickness);
                    range.Add(new ShearCalculationDetail("VEd,T+V,bw (axis " + index + ")", demand, "N", "|V| + |T| z bw/(2 Ak tef): range of cot θ (DIN NA 6.3.2(2))"));
                }
                var shear = SectionShearCalculator.Calculate(axis.With(demand, p.CotTheta));
                if (profile == TorsionProfile.DinEN1992p11) shear = SectionShearCalculator.Calculate(axis.With(axis.V, p.CotTheta));
                return new TorsionShearComponent(axis.V, shear);
            }
            var c1 = Component(axis1, 1); var c2 = Component(axis2, 2);
            var result = Evaluate(p, c1, c2);
            result.Axis1Shear = c1?.Shear; result.Axis2Shear = c2?.Shear;
            if (range.Count > 0) result.Details = range.Concat(result.Details).ToList().AsReadOnly();
            return result;
        }

        /// <summary>Torsion check with given shear components (null = no shear in that direction), all on the cot θ of the input.</summary>
        public static SectionTorsionResult Evaluate(SectionTorsionInput p, TorsionShearComponent axis1, TorsionShearComponent axis2)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var profile = TorsionProfiles.Resolve(p.Standard);
            Validate(p, profile);
            var details = new List<ShearCalculationDetail>();
            void Add(string symbol, double value, string unit, string expression) => details.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            var limitations = new List<string>();
            double cot = p.CotTheta, ak = p.Geometry.EnclosedArea, uk = p.Geometry.Perimeter, tef = p.Geometry.Thickness;
            double fck = p.Fck, fcd = p.Fcd;
            // NS: strength classes above C60/75 are used with the C60 value, as in the shear formulas.
            if (profile == TorsionProfile.NsEN1992p11 && fck > 60) { fcd = fcd * 60 / fck; fck = 60; }
            bool box = p.HollowWithReinforcementOnBothFaces;
            bool quadratic = !box && (profile == TorsionProfile.DinEN1992p11 || profile == TorsionProfile.ModelCode2010);
            if (profile == TorsionProfile.DinEN1992p11 && axis1 == null && axis2 == null && cot > 1 + 1e-12)
                throw new ArgumentException("DIN: without the shear data of the directions the range of cot θ (VEd,T+V) is not known; use cot θ = 1.");

            double strut;
            switch (profile)
            {
                case TorsionProfile.Ntc2018:
                case TorsionProfile.CnrDT200:
                    strut = .5 * fcd; Add("f'cd", strut, "MPa", "0.5 fcd"); break;
                case TorsionProfile.DinEN1992p11:
                    {
                        double nu2 = Math.Min(1, 1.1 - fck / 500), nu = (box ? .75 : .525) * nu2;
                        strut = nu * fcd; Add("ν", nu, "−", (box ? "0.75" : "0.525") + " ν2, ν2 = 1.1 − fck/500 ≤ 1"); break;
                    }
                case TorsionProfile.ModelCode2010:
                    {
                        double ex = Math.Max(axis1?.LongitudinalStrain ?? 0, axis2?.LongitudinalStrain ?? 0), e1 = ex + (ex + .002) * cot * cot;
                        double kc = Math.Min(.65, 1 / (1.2 + 55 * e1)) * Math.Min(1, SectionShearCalculator.Cbrt(30 / fck));
                        strut = kc * fck / p.GammaC;
                        Add("εx", ex, "−", "largest εx of the two shear directions"); Add("ε1", e1, "−", "εx + (εx + 0.002) cot²θ");
                        Add("kc", kc, "−", "min[0.65; 1/(1.2 + 55 ε1)] · min[1; (30/fck)^(1/3)]"); break;
                    }
                default:
                    {
                        double nu = SectionShearCalculator.StrutEfficiency(TorsionProfiles.Shear(profile), fck);
                        strut = nu * fcd; Add("ν", nu, "−", "6.2.2(6) of the standard; αcw = 1"); break;
                    }
            }
            double rc = 2 * ak * tef * strut * cot / (1 + cot * cot);
            double rs = 2 * ak * p.LinkLegArea / p.Spacing * p.LinkFyd * cot;
            double rl = 2 * ak * p.LongitudinalArea / uk * p.LongitudinalFyd / cot;
            double rd = Math.Min(rc, Math.Min(rs, rl));
            double t = Math.Abs(p.T);

            // Units of the legacy tolerance (1e-12 kNm and 1e-12 kN).
            double Ratio(double action, double capacity, double tolerance) => Math.Abs(action) < tolerance ? 0 : capacity > 0 ? Math.Abs(action) / capacity : double.PositiveInfinity;
            double TorsionRatio(double capacity) => Ratio(t, capacity, 1e-6);
            double ShearRatio(TorsionShearComponent c, bool concrete) => c == null ? 0 : Ratio(c.Demand, concrete ? c.VRcd : c.VRsd, 1e-9);
            foreach (var c in new[] { axis1, axis2 })
                if (c != null && Math.Abs(c.Demand) > 0 && Math.Abs(c.CotTheta - cot) > 1e-8)
                    throw new ArgumentException("Shear and torsion must use the same cot θ.");

            double eta = TorsionRatio(rd);
            double shearConcrete = ShearRatio(axis1, true) + ShearRatio(axis2, true), torsionConcrete = TorsionRatio(rc);
            double concreteInteraction = p.LinkLegArea == 0 && t > 0 ? double.PositiveInfinity
                : quadratic ? Math.Sqrt(torsionConcrete * torsionConcrete + shearConcrete * shearConcrete) : torsionConcrete + shearConcrete;
            double linkInteraction = TorsionRatio(rs) + Math.Max(ShearRatio(axis1, false), ShearRatio(axis2, false));
            double requiredAl = t * uk * cot / (2 * ak * p.LongitudinalFyd);
            double requiredLinks = t / (2 * ak * p.LinkFyd * cot);

            Add("Ak", ak, "mm2", "area enclosed by the centre line of the walls"); Add("uk", uk, "mm", "perimeter of Ak"); Add("tef", tef, "mm", "wall thickness");
            Add("cot θ", cot, "−", "common to torsion and shear");
            Add("TRcd", rc, "Nmm", "2 Ak tef fc,strut cot θ/(1 + cot²θ)"); Add("TRsd", rs, "Nmm", "2 Ak (Ast/s) fyd cot θ"); Add("TRld", rl, "Nmm", "2 Ak (ΣAsl/uk) fyd / cot θ");
            Add("ΣAsl,req", requiredAl, "mm2", "|T| uk cot θ/(2 Ak fyd)"); Add("Ast/s,req", requiredLinks, "mm2/mm", "|T|/(2 Ak fyd cot θ), one leg");
            if (profile == TorsionProfile.CnrDT200) Add("TRd,f", 0, "Nmm", "No FRP torsional strengthening in the section");
            if (profile == TorsionProfile.DsEN1992p11)
                limitations.Add("DK NA 6.3.2(6): the linear rule Σ SEd/SRd ≤ 1 for combined V, T, N and M (and the alternative of Annex F) is not applied; "
                    + "only the strut interaction 6.29 and the truss reinforcement are checked.");
            if (profile == TorsionProfile.ModelCode2010 || profile == TorsionProfile.DinEN1992p11)
                Add("interaction", quadratic ? 2 : 1, "−", quadratic ? "√[(T/TRcd)² + (ΣV/VRcd)²] (solid section)" : "T/TRcd + ΣV/VRcd (box section)");

            double? Finite(double v) => double.IsInfinity(v) ? (double?)null : v;
            bool passed = eta <= 1 && concreteInteraction <= 1 && linkInteraction <= 1;
            var result = new SectionTorsionResult
            {
                Profile = profile, Demand = t, TRcd = rc, TRsd = rs, TRld = rl, TRd = rd, TorsionRatio = Finite(eta), ConcreteInteraction = Finite(concreteInteraction),
                LinkInteraction = Finite(linkInteraction), RequiredLongitudinalArea = requiredAl, RequiredLinkAreaPerLength = requiredLinks, CotTheta = cot,
                StrutStrength = strut, QuadraticInteraction = quadratic, Verdict = passed ? TorsionVerdict.Satisfied : TorsionVerdict.NotSatisfied,
                Status = passed ? "Torsion and interaction satisfied in the assigned model" : p.LinkLegArea == 0 && t > 0 ? "No closed links: zero torsional resistance"
                    : double.IsInfinity(eta) ? "Torsion not satisfied: zero resistance, ratio not defined" : "Torsion / interaction not satisfied",
                Model = TorsionProfiles.Model(profile), Reference = TorsionProfiles.Reference(profile), Details = details.AsReadOnly(), Limitations = limitations.AsReadOnly()
            };
            if (result.TorsionRatio.HasValue && result.ConcreteInteraction.HasValue && result.LinkInteraction.HasValue)
                result.Ratio = Math.Max(result.TorsionRatio.Value, Math.Max(result.ConcreteInteraction.Value, result.LinkInteraction.Value));
            return result;
        }

        private static void Validate(SectionTorsionInput p, TorsionProfile profile)
        {
            if (new[] { p.Fck, p.Fcd, p.GammaC, p.LinkFyd, p.LongitudinalFyd, p.Spacing }.Any(v => !TorsionGeometry.Positive(v))
                || double.IsNaN(p.T) || double.IsInfinity(p.T) || !(p.LinkLegArea >= 0) || double.IsInfinity(p.LinkLegArea)
                || !(p.LongitudinalArea >= 0) || double.IsInfinity(p.LongitudinalArea))
                throw new ArgumentException("Torsion: check geometry, reinforcement and materials.");
            if (p.Fck > 90) throw new ArgumentException("Torsion: concrete above C90/105 is outside the implemented range.");
            double max = TorsionProfiles.MaximumCotTheta(profile);
            if (double.IsNaN(p.CotTheta) || p.CotTheta < 1 || p.CotTheta > max)
                throw new ArgumentException($"Torsion: cot θ must be within [1; {max:0.###}].");
            if (p.LinkAngleDegrees != 90) throw new NotSupportedException("Torsion: only links at 90° are implemented.");
        }
    }
}
