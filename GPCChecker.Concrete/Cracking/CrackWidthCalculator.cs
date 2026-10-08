using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Shear;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>
    /// Data of the crack-width formula for one effective region. Units: MPa, mm. σs ≥ 0 is the governing bar stress, ρ = As,eff/Ac,eff,
    /// Øeq = ΣØ²/ΣØ, c the cover to the bar surface, s the maximum spacing of the effective bars, (h − x) the tensile depth; k2 in [0.5; 1].
    /// </summary>
    public sealed class CrackWidthInput
    {
        public double SteelStress { get; }
        public double Es { get; }
        public double Ecm { get; }
        public double Fct { get; }
        public double Rho { get; }
        public double Diameter { get; }
        public double Cover { get; }
        public double Spacing { get; }
        public double TensileDepth { get; }
        public bool ShortTerm { get; }
        public bool Ribbed { get; }
        public double K2 { get; }
        public CrackWidthInput(double steelStress, double es, double ecm, double fct, double rho, double diameter, double cover, double spacing, double tensileDepth,
            bool shortTerm, bool ribbed, double k2)
        {
            SteelStress = steelStress; Es = es; Ecm = ecm; Fct = fct; Rho = rho; Diameter = diameter; Cover = cover; Spacing = spacing; TensileDepth = tensileDepth;
            ShortTerm = shortTerm; Ribbed = ribbed; K2 = k2;
        }
    }

    /// <summary>
    /// Crack width wk of one region, transferred from ANTHEA (Ntc2018Checks.CrackWidth and ConcreteCodeChecks.CrackWidth, commit fe4652c), mm.
    /// NTC 2018 (Circolare C4.1.2.2.4.5): wk = 1.7 Δsm (εsm − εcm), Δsm = (3.4 c + k1 k2 0.425 Ø/ρ)/1.7, sparse bars (s &gt; 5(c + Ø/2)) with
    /// max(Δsm; 0.75 (h − x)). Eurocode family (7.8-7.14): wk = sr,max (εsm − εcm), sr,max = k3 c + k1 k2 k4 Ø/ρ (1.3 (h − x) for sparse bars);
    /// DS: k3 = 3.4 (25/c)^(2/3); DIN: kt = 0.4, sr,max = min(Ø/(3.6 ρ) or 1.3 (h − x); σs Ø/(3.6 fct)); Model Code 2010: sr = 2 [c + Ø/(4 τbm/fctm ρ)]
    /// with τbm/fctm = 1.8 (short) or 1.35 (long), βmin = 1 − kt. MC2010 and DIN only with ribbed bars.
    /// </summary>
    public static class CrackWidthCalculator
    {
        public static double Width(CrackProfile profile, CrackWidthInput p, List<ShearCalculationDetail> details = null)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (new[] { p.Es, p.Ecm, p.Fct, p.Rho, p.Diameter, p.Spacing, p.TensileDepth }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                || double.IsNaN(p.SteelStress + p.Cover + p.K2) || double.IsInfinity(p.SteelStress + p.Cover + p.K2) || p.SteelStress < 0 || p.Cover < 0 || p.K2 < .5 || p.K2 > 1)
                throw CrackRejection.Create(CrackRejection.WidthParameters, "Cracking: invalid crack-width parameters.");
            void Add(string symbol, double value, string unit, string expression) => details?.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            double sigma = p.SteelStress, es = p.Es, rho = p.Rho, phi = p.Diameter, cover = p.Cover;
            if (CrackProfiles.IsNtc(profile))
            {
                double kt = p.ShortTerm ? .6 : .4, k1 = p.Ribbed ? .8 : 1.6;
                double alphaE = es / p.Ecm;
                double stiffening = kt * p.Fct / rho * (1 + alphaE * rho);
                double computedStrain = (sigma - stiffening) / es, minimumStrain = .6 * sigma / es;
                double strain = Math.Max(computedStrain, minimumStrain);
                double near = (3.4 * cover + k1 * p.K2 * .425 * phi / rho) / 1.7;
                double spacingLimit = 5 * (cover + phi / 2), far = .75 * p.TensileDepth;
                bool closeBars = p.Spacing <= spacingLimit;
                double distance = closeBars ? near : Math.Max(near, far);
                double ntcWidth = Math.Max(0, 1.7 * distance * strain);
                Add("ρp,eff", rho, "−", "As,eff / Ac,eff"); Add("αe", alphaE, "−", "Es / Ecm"); Add("kt", kt, "−", p.ShortTerm ? "short term" : "long term");
                Add("k1", k1, "−", p.Ribbed ? "ribbed bars" : "plain bars"); Add("k2", p.K2, "−", "strain distribution");
                Add("εsm − εcm", strain, "−", "max[(σs − kt fct/ρ (1 + αe ρ))/Es; 0.6 σs/Es]");
                Add("Δsm,near", near, "mm", "(3.4 c + k1 k2 0.425 Ø/ρ)/1.7"); Add("s,lim", spacingLimit, "mm", "5 (c + Ø/2)");
                Add("Δsm,far", far, "mm", "0.75 (h − x)"); Add("Δsm", distance, "mm", closeBars ? "near bars" : "max(near; far)");
                Add("wk", ntcWidth, "mm", "max[0; 1.7 Δsm (εsm − εcm)]");
                return ntcWidth;
            }
            bool mc = profile == CrackProfile.ModelCode2010, din = profile == CrackProfile.DinEN1992p11, ds = profile == CrackProfile.DsEN1992p11;
            if ((mc || din) && !p.Ribbed) throw CrackRejection.Create(CrackRejection.RibbedBarsRequired, "Cracking: the Model Code 2010 / DIN crack model is implemented for ribbed bars.");
            double ktEc = din ? .4 : p.ShortTerm ? .6 : .4;
            double lower = mc ? 1 - ktEc : .6;
            double strainEc = Math.Max(lower * sigma / es, (sigma - ktEc * p.Fct / rho * (1 + es / p.Ecm * rho)) / es);
            double k3 = ds && cover > 0 ? 3.4 * Math.Pow(25 / cover, 2d / 3) : 3.4;
            double sr = k3 * cover + (p.Ribbed ? .8 : 1.6) * p.K2 * .425 * phi / rho;
            string formula = "k3 c + k1 k2 k4 Ø/ρ";
            if (mc) { sr = 2 * (cover + phi / (4 * (p.ShortTerm ? 1.8 : 1.35) * rho)); formula = "2 [c + Ø/(4 τbm/fctm ρ)]"; }
            else if (din)
            {
                double freeSpacing = p.Spacing > 5 * (cover + phi / 2) ? 1.3 * p.TensileDepth : phi / (3.6 * rho);
                sr = Math.Min(freeSpacing, sigma * phi / (3.6 * p.Fct));
                formula = "min[Ø/(3.6 ρ) or 1.3 (h − x); σs Ø/(3.6 fct)]";
            }
            else if (p.Spacing > 5 * (cover + phi / 2)) { sr = 1.3 * p.TensileDepth; formula = "1.3 (h − x), sparse bars (7.14)"; }
            Add("ρp,eff", rho, "−", "As,eff / Ac,eff"); Add("αe", es / p.Ecm, "−", "Es / Ecm"); Add("kt", ktEc, "−", "duration / standard");
            Add("k2", p.K2, "−", "strain distribution"); Add("k3", k3, "−", ds ? "3.4 (25/c)^(2/3)" : "3.4");
            Add("sr,max", sr, "mm", formula); Add("εsm − εcm", strainEc, "−", "max[(σs − kt fct/ρ (1 + αe ρ))/Es; βmin σs/Es]");
            Add("βmin", lower, "−", mc ? "1 − kt" : "0.6"); Add("wk", sr * strainEc, "mm", "sr,max (εsm − εcm)");
            return sr * strainEc;
        }

        /// <summary>
        /// Upper bound of wk without bonded bars in Ac,eff (EC2 7.3.4(3), eq. (7.14); for NTC by analogy with Circolare C4.1.2.2.4.5 [C4.1.10], which does not treat this case), mm: the limit ρ → 0 of <see cref="Width"/>.
        /// εsm − εcm = βmin σs/Es; sr,max = 1.3 (h − x), NTC 1.7 · 0.75 (h − x) as the sparse-bar branch, DIN also ≤ σs Ø/(3.6 fct).
        /// σs and Ø of the tensile bars outside Ac,eff. MC2010 and DIN only with ribbed bars.
        /// </summary>
        public static double UnbondedUpperBound(CrackProfile profile, double steelStress, double es, double fct, double diameter, double tensileDepth, bool shortTerm, bool ribbed,
            List<ShearCalculationDetail> details = null)
        {
            if (new[] { es, fct, diameter, tensileDepth }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0) || double.IsNaN(steelStress) || double.IsInfinity(steelStress) || steelStress < 0)
                throw CrackRejection.Create(CrackRejection.UpperBoundParameters, "Cracking: invalid crack-width parameters.");
            void Add(string symbol, double value, string unit, string expression) => details?.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            bool mc = profile == CrackProfile.ModelCode2010, din = profile == CrackProfile.DinEN1992p11;
            if ((mc || din) && !ribbed) throw CrackRejection.Create(CrackRejection.RibbedBarsRequired, "Cracking: the Model Code 2010 / DIN crack model is implemented for ribbed bars.");
            double lower = mc ? 1 - (shortTerm ? .6 : .4) : .6, strain = lower * steelStress / es;
            double sr = CrackProfiles.IsNtc(profile) ? 1.7 * .75 * tensileDepth : 1.3 * tensileDepth;
            string formula = CrackProfiles.IsNtc(profile) ? "1.7 · 0.75 (h − x), no bonded bar in Ac,eff" : "1.3 (h − x), no bonded bar in Ac,eff (7.3.4(3), eq. (7.14))";
            if (din) { sr = Math.Min(sr, steelStress * diameter / (3.6 * fct)); formula = "min[1.3 (h − x); σs Ø/(3.6 fct)], no bonded bar in Ac,eff"; }
            Add("εsm − εcm", strain, "−", "βmin σs/Es (ρp,eff → 0)"); Add("βmin", lower, "−", mc ? "1 − kt" : "0.6");
            Add("sr,max", sr, "mm", formula); Add("wk", sr * strain, "mm", "sr,max (εsm − εcm), upper bound");
            return sr * strain;
        }

        /// <summary>
        /// Legacy NTC 2018 / CNR-DT 200 rule before D7-b (<see cref="SectionCrackInput.NtcK2FromCompressedBars"/>): k2 from the bar stresses (compression
        /// negative), 0.5 with a compressed bar, 1.0 otherwise; zero stresses are not compressed. Also validates the bar stresses (missing or not finite:
        /// ArgumentException). The current rule of <see cref="SectionCrackCheck"/> uses k2 = 0.5 whenever the neutral axis cuts the section.
        /// </summary>
        public static double K2(IReadOnlyList<double> barStresses)
        {
            if (barStresses == null || barStresses.Count == 0 || barStresses.Any(s => double.IsNaN(s) || double.IsInfinity(s)))
                throw CrackRejection.Create(CrackRejection.BarStresses, "k2: bar stresses missing or not finite.");
            return barStresses.Any(s => s < 0) ? .5 : 1;
        }
    }
}
