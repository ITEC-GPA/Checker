using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Concrete.Shear
{
    /// <summary>
    /// Shear resistance of an ordinary reinforced concrete section in one direction, transferred from ANTHEA
    /// (ConcreteCodeChecks.Shear and Ntc2018Checks.Shear, commit fe4652c) with explicit N/Nmm units.
    /// No favourable reduction near supports, no prestress. Fixtures: GPCChecker.Test.Concrete/Fixtures/shear-legacy.csv.
    /// </summary>
    public static class SectionShearCalculator
    {
        public const string MethodId = "Concrete.SectionShear";

        public static SectionShearResult Calculate(SectionShearInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var profile = ShearProfiles.Resolve(p.Standard);
            if (new[] { p.Area, p.Bw, p.D, p.Fck, p.Fcd, p.Fyd, p.GammaC, p.Es, p.Spacing }.Any(v => !Finite(v) || v <= 0)
                || new[] { p.N, p.V, p.M, p.AxialEccentricity }.Any(v => !Finite(v))
                || new[] { p.Asl, p.Asw, p.Aggregate }.Any(v => !Finite(v) || v < 0)
                || !Finite(p.AlphaDegrees) || p.AlphaDegrees < 45 || p.AlphaDegrees > 90 || !Finite(p.LeverFactor) || p.LeverFactor <= 0 || p.LeverFactor > .9)
                throw new ArgumentException("Shear: invalid geometry, materials, actions or shear-reinforcement inclination.");
            if (p.Fck > 90) throw new ArgumentException("Shear: concrete above C90/105 is outside the implemented range.");
            double fck = p.Fck, fcd = p.Fcd;
            // NS: strength classes above C60/75 are used with the C60 value in the shear formulas.
            if (profile == ShearProfile.NsEN1992p11 && fck > 60) { fcd = fcd * 60 / fck; fck = 60; }
            SectionShearResult result;
            if (profile == ShearProfile.Ntc2018) result = Ntc(p);
            else if (profile == ShearProfile.CnrDT200) result = FrpStrengthened(p);
            else if (profile == ShearProfile.CnrDT204) result = FibreReinforced(p);
            else result = Eurocode(p, profile, fck, fcd);
            result.Profile = profile; result.Demand = Math.Abs(p.V); result.WithShearReinforcement = p.Asw > 0;
            result.Reference = ShearProfiles.Reference(profile); result.Model = ShearProfiles.Model(profile);
            if (result.Status == null) // a method that declines the case sets its own NotEvaluated status
            {
                if (result.Ratio.HasValue) result.Verdict = result.Ratio.Value <= 1 ? ShearVerdict.Satisfied : ShearVerdict.NotSatisfied;
                else result.Verdict = result.VRd <= 0 && result.Demand > 0 ? ShearVerdict.NotSatisfied : ShearVerdict.NotEvaluated;
                result.Status = result.VRd <= 0 ? "Zero resistance" : result.Verdict == ShearVerdict.Satisfied
                    ? "Resistance sufficient; detailing to be verified" : "Resistance insufficient";
            }
            return result;
        }

        /// <summary>NTC 2018 §4.1.2.3.5.1 (without) and §4.1.2.3.5.2 (with shear reinforcement).</summary>
        private static SectionShearResult Ntc(SectionShearInput p)
        {
            var details = new List<ShearCalculationDetail>();
            void Add(string symbol, double value, string unit, string expression) => details.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            double sigmaCp = -p.N / p.Area; // NTC: compression positive; the solver and the input use compression negative.
            if (p.Asw == 0)
            {
                if (p.N > 0) return new SectionShearResult { Verdict = ShearVerdict.NotEvaluated, Status = "Tension without shear reinforcement: not verified automatically",
                    Details = details.AsReadOnly() };
                sigmaCp = Math.Min(sigmaCp, .2 * p.Fcd);
                double k = Math.Min(2, 1 + Math.Sqrt(200 / p.D)), rho = Math.Min(.02, p.Asl / (p.Bw * p.D));
                double a = (.18 / p.GammaC * k * Cbrt(100 * rho * p.Fck) + .15 * sigmaCp) * p.Bw * p.D;
                double b = (.035 * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck) + .15 * sigmaCp) * p.Bw * p.D;
                double resistance = Math.Max(a, b);
                Add("σcp", sigmaCp, "MPa", "min(−N/Ac; 0.2 fcd), compression positive"); Add("k", k, "−", "min[2; 1 + √(200/d)]");
                Add("ρl", rho, "−", "min[0.02; Asl/(bw d)]"); Add("vmin", .035 * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck), "MPa", "0.035 k^1.5 √fck");
                return new SectionShearResult { VRsd = a, VRcd = b, VRd = resistance, Ratio = Math.Abs(p.V) / resistance, CotTheta = 0, Details = details.AsReadOnly() };
            }
            double sc = Math.Max(0, sigmaCp), fcd = p.Fcd;
            double ac = sc <= .25 * fcd ? 1 + sc / fcd : sc <= .5 * fcd ? 1.25 : Math.Max(0, 2.5 * (1 - sc / fcd));
            double alpha = p.AlphaDegrees * Math.PI / 180;
            double autoCot = Math.Sqrt(Math.Max(0, .5 * fcd * p.Bw * ac / (p.Asw / p.Spacing * p.Fyd * Math.Sin(alpha)) - 1));
            double cot = p.CotTheta ?? Math.Min(Math.Max(autoCot, 1), 2.5);
            if (!Finite(cot) || cot < 1 || cot > 2.5) throw new ArgumentException("NTC 2018: cot θ must be within [1; 2.5].");
            double common = 1 / Math.Tan(alpha) + cot;
            double rsd = p.LeverFactor * p.D * p.Asw / p.Spacing * p.Fyd * common * Math.Sin(alpha);
            double rcd = p.LeverFactor * p.D * p.Bw * ac * .5 * fcd * common / (1 + cot * cot);
            double rd = Math.Min(rsd, rcd);
            Add("z", p.LeverFactor * p.D, "mm", "z/d · d"); Add("σcp", sigmaCp, "MPa", "−N/Ac, compression positive");
            Add("αc", ac, "−", "1 + σcp/fcd; 1.25; 2.5(1 − σcp/fcd)"); Add("ν fcd", .5 * fcd, "MPa", "0.5 fcd");
            Add("cot θ", cot, "−", p.CotTheta.HasValue ? "Assigned" : "√(ν fcd bw αc / (Asw/s fyd sin α) − 1) within [1; 2.5]");
            return new SectionShearResult { VRsd = rsd, VRcd = rcd, VRd = rd, Ratio = rd > 0 ? Math.Abs(p.V) / rd : (double?)null, CotTheta = cot, Details = details.AsReadOnly() };
        }

        /// <summary>
        /// CNR-DT 200 R1/2013: VRd = min(VRd,s + VRd,f; VRd,c) with VRd,s and VRd,c of NTC 2018. Sections carry no FRP data, so
        /// VRd,f = 0 and the resistance is the one of the reinforced concrete member; the detail states it.
        /// </summary>
        private static SectionShearResult FrpStrengthened(SectionShearInput p)
        {
            var result = Ntc(p);
            var details = result.Details.ToList();
            details.Add(new ShearCalculationDetail("VRd,f", 0, "N", "No FRP shear strengthening in the section"));
            result.Details = details.AsReadOnly();
            return result;
        }

        /// <summary>
        /// CNR-DT 204/2006 (and fib MC2010 7.7-5) for fibre-reinforced members without shear reinforcement:
        /// VRd,F = [0.18/γc · k · (100 ρl (1 + 7.5 fFtuk/fctk) fck)^(1/3) + 0.15 σcp] bw d ≥ (vmin + 0.15 σcp) bw d,
        /// σcp = −N/Ac ≤ 0.2 fcd (compression positive). With fFtuk = 0 it coincides with EC2 6.2.2.
        /// Fibres combined with shear reinforcement are not implemented.
        /// </summary>
        private static SectionShearResult FibreReinforced(SectionShearInput p)
        {
            if (p.Asw > 0) throw new NotSupportedException("CNR-DT 204: fibres combined with shear reinforcement are not implemented.");
            if (!Finite(p.ResidualTensileStrength) || p.ResidualTensileStrength < 0 || !Finite(p.MatrixTensileStrength) || p.MatrixTensileStrength <= 0)
                throw new ArgumentException("CNR-DT 204: fFtuk ≥ 0 and the matrix fctk > 0 are required.");
            var details = new List<ShearCalculationDetail>();
            void Add(string symbol, double value, string unit, string expression) => details.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            double sigma = Math.Min(-p.N / p.Area, .2 * p.Fcd);
            double k = Math.Min(2, 1 + Math.Sqrt(200 / p.D)), rho = Math.Min(.02, p.Asl / (p.Bw * p.D));
            double fibres = 1 + 7.5 * p.ResidualTensileStrength / p.MatrixTensileStrength;
            double vmin = .035 * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck);
            double resistance = (.18 / p.GammaC * k * Cbrt(100 * rho * fibres * p.Fck) + .15 * sigma) * p.Bw * p.D;
            double floor = (vmin + .15 * sigma) * p.Bw * p.D;
            double rd = Math.Max(0, Math.Max(resistance, floor));
            Add("σcp", sigma, "MPa", "min(−N/Ac; 0.2 fcd), compression positive"); Add("k", k, "−", "min[2; 1 + √(200/d)]");
            Add("ρl", rho, "−", "min[0.02; Asl/(bw d)]"); Add("fFtuk", p.ResidualTensileStrength, "MPa", "ultimate residual strength of the FRC");
            Add("fctk", p.MatrixTensileStrength, "MPa", "matrix tensile strength (5%)"); Add("1 + 7.5 fFtuk/fctk", fibres, "−", "fibre term");
            Add("vmin", vmin, "MPa", "0.035 k^1.5 √fck"); Add("VRd,F", resistance, "N", "fibre-reinforced concrete"); Add("VRd,F,min", floor, "N", "(vmin + 0.15 σcp) bw d");
            return new SectionShearResult { VRsd = 0, VRcd = rd, VRd = rd, Ratio = rd > 0 ? Math.Abs(p.V) / rd : p.V == 0 ? 0 : (double?)null, CotTheta = 0,
                Details = details.AsReadOnly() };
        }

        /// <summary>Eurocode 2 first generation with the implemented national annexes, and Model Code 2010 level II.</summary>
        private static SectionShearResult Eurocode(SectionShearInput p, ShearProfile profile, double fck, double fcd)
        {
            var details = new List<ShearCalculationDetail>();
            void Add(string symbol, double value, string unit, string expression) => details.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            double z = p.LeverFactor * p.D, sigma = -p.N / p.Area;
            Add("z", z, "mm", "z/d · d"); Add("σcp", sigma, "MPa", "−N/Ac, compression positive");
            bool mc = profile == ShearProfile.ModelCode2010;
            bool ns = profile == ShearProfile.NsEN1992p11, din = profile == ShearProfile.DinEN1992p11, ds = profile == ShearProfile.DsEN1992p11;
            double epsilon = 0;
            if (mc)
            {
                if (p.Asl <= 0) throw new ArgumentException("Model Code 2010: an effective Asl is required, also with shear reinforcement.");
                epsilon = Math.Max(0, (Math.Abs(p.M) / z + Math.Abs(p.V) + p.N * (.5 + p.AxialEccentricity / z)) / (2 * p.Es * p.Asl));
                Add("εx", epsilon, "−", "max[0; (|M|/z + |V| + N(1/2 + Δe/z))/(2 Es Asl)]");
            }
            SectionShearResult Result(double rs, double rc, double rd, double adopted) => new SectionShearResult { VRsd = rs, VRcd = rc, VRd = rd,
                Ratio = rd > 0 ? Math.Abs(p.V) / rd : p.V == 0 ? 0 : (double?)null, CotTheta = adopted, LongitudinalStrain = epsilon, Details = details.AsReadOnly() };
            if (p.Asw == 0)
            {
                if (mc)
                {
                    double dg = fck > 70 ? 0 : p.Aggregate, kd = Math.Max(.75, 32 / (16 + dg));
                    double kv = .4 / (1 + 1500 * epsilon) * 1300 / (1000 + kd * z);
                    double resistance = kv * Math.Min(8, Math.Sqrt(fck)) * z * p.Bw / p.GammaC;
                    Add("dg efficace", dg, "mm", "0 for fck > 70 MPa"); Add("kdg", kd, "−", "max[0.75; 32/(16+dg)]");
                    Add("kv", kv, "−", "0.4/(1+1500 εx) · 1300/(1000+kdg z)");
                    return Result(0, resistance, resistance, 0);
                }
                double k = Math.Min(2, 1 + Math.Sqrt(200 / p.D)), rho = Math.Min(.02, p.Asl / (p.Bw * p.D));
                double cr = .18 / p.GammaC, k1 = .15, vmin = .035 * Math.Pow(k, 1.5) * Math.Sqrt(fck);
                if (ns) { cr = (p.Aggregate < 16 ? .15 : .18) / p.GammaC; k1 = sigma < 0 ? .3 : .15; }
                if (din) { cr = .15 / p.GammaC; k1 = .12; vmin = (p.D <= 600 ? .0525 : p.D >= 800 ? .0375 : .0525 - (p.D - 600) * .015 / 200) / p.GammaC * Math.Pow(k, 1.5) * Math.Sqrt(fck); }
                if (ds) vmin = .051 / p.GammaC * Math.Pow(k, 1.5) * Math.Sqrt(fck);
                double sc = Math.Min(sigma, .2 * fcd);
                double empirical = (cr * k * Cbrt(100 * rho * fck) + k1 * sc) * p.Bw * p.D;
                double floor = (vmin + k1 * sc) * p.Bw * p.D;
                Add("k", k, "−", "min[2;1+√(200/d)]"); Add("ρl", rho, "−", "min[0.02;Asl/(bw d)]");
                Add("CRd,c", cr, "−", "Coefficient of the standard"); Add("k1", k1, "−", "Coefficient of σcp"); Add("vmin", vmin, "MPa", "Minimum of the standard");
                return Result(empirical, floor, Math.Max(0, Math.Max(empirical, floor)), 0);
            }
            double alpha = p.AlphaDegrees * Math.PI / 180, ca = 1 / Math.Tan(alpha);
            double minCot = 1, maxCot = mc ? 1 / Math.Tan(20 * Math.PI / 180) : 2.5;
            if (ns && -sigma >= .7 * (fck <= 50 ? .3 * Math.Pow(fck, 2d / 3) : 2.12 * Math.Log(1 + (fck + 8) / 10))) maxCot = 1.25;
            double acw = 1; // EC2 6.2.3: recommended value for non-prestressed structures.
            double nu = StrutEfficiency(profile, fck);
            if (din)
            {
                nu = .75 * Math.Min(1, 1.1 - fck / 500);
                double vcc = .24 * Cbrt(fck) * Math.Max(0, 1 - 1.2 * sigma / fcd) * p.Bw * z;
                maxCot = Math.Abs(p.V) <= vcc ? 3 : Math.Min(3, (1.2 + 1.4 * sigma / fcd) / (1 - vcc / Math.Abs(p.V)));
                if (maxCot < 1) throw new ArgumentException("DIN: strut inclination out of range with this axial tension.");
                Add("VRd,cc", vcc, "N", "0.24 fck^(1/3) (1−1.2 σcp/fcd) bw z");
            }
            if (ds) { minCot = Math.Tan(alpha / 2); maxCot = 2; }
            Add("cot θ massimo", maxCot, "−", ds ? "Cautious limit 2, also with curtailed reinforcement; steel B/C" : "Limit of the standard");
            (double Steel, double Concrete) Resistance(double c)
            {
                double rs = z * p.Asw / p.Spacing * p.Fyd * (c + ca) * Math.Sin(alpha);
                double strength = acw * nu * fcd;
                if (mc)
                {
                    double e1 = epsilon + (epsilon + .002) * c * c;
                    strength = Math.Min(.65, 1 / (1.2 + 55 * e1)) * Math.Min(1, Cbrt(30 / fck)) * fck / p.GammaC;
                }
                return (rs, z * p.Bw * strength * (c + ca) / (1 + c * c));
            }
            double cot = p.CotTheta ?? minCot;
            if (!p.CotTheta.HasValue)
            {
                // Bounded scalar maximisation; it also covers inclined reinforcement and the strain-dependent MC strut.
                double lo = minCot, hi = maxCot;
                for (int i = 0; i < 80; i++)
                {
                    double a = lo + (hi - lo) / 3, b = hi - (hi - lo) / 3;
                    var ra = Resistance(a); var rb = Resistance(b);
                    if (Math.Min(ra.Steel, ra.Concrete) < Math.Min(rb.Steel, rb.Concrete)) lo = a; else hi = b;
                }
                cot = (lo + hi) / 2;
            }
            if (!Finite(cot) || cot < minCot - 1e-10 || cot > maxCot + 1e-10) throw new ArgumentException($"Shear: cot θ outside [{minCot:0.###}; {maxCot:0.###}].");
            var capacity = Resistance(cot);
            Add("cot θ", cot, "−", p.CotTheta.HasValue ? "Assigned" : "Maximises min(VRd,s; VRd,max) within the admissible range");
            if (!mc) { Add("ν1", nu, "−", "Strut reduction"); Add("αcw", acw, "−", "Non-prestressed section"); }
            return Result(capacity.Steel, capacity.Concrete, Math.Min(capacity.Steel, capacity.Concrete), cot);
        }

        /// <summary>
        /// Strength reduction ν of the cracked concrete strut (6.2.2(6) and 6.2.3(3) with ν1 = ν) of the Eurocode profiles:
        /// recommended 0.6 (1 − fck/250); DS (DK NA 5.6.1(3)P) 0.7 − fck/200 ≥ 0.45; UNI (DM 31/07/2012) 0.5 up to C70/85.
        /// DIN replaces it in the shear strut (0.75 ν2) and in torsion. fck already capped by NS.
        /// </summary>
        internal static double StrutEfficiency(ShearProfile profile, double fck)
        {
            if (profile == ShearProfile.DsEN1992p11) return Math.Max(.45, .7 - fck / 200);
            if (profile == ShearProfile.UniEN1992p11 && fck <= 70) return .5;
            return .6 * (1 - fck / 250);
        }

        // netstandard2.0 has no Math.Cbrt; the difference is at the level of the last bits.
        internal static double Cbrt(double x) => x == 0 ? 0 : Math.Pow(x, 1.0 / 3.0);
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
