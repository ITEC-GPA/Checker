namespace GPC.Checkers.Geotechnics.Seismic
{
    /// <summary>Subsoil category of NTC 2018 Tab. 3.2.II.</summary>
    public enum NtcSoilCategory { A, B, C, D, E }

    /// <summary>Topographic shape of NTC 2018 Tab. 3.2.III: flat surface, slope, ridge with narrow crest.</summary>
    public enum NtcTopography { Flat, Slope, Ridge }

    /// <summary>
    /// Maximum acceleration at the site amax/g = Ss St ag/g (NTC 2018 §3.2.3.2.1): stratigraphic amplification Ss of Tab. 3.2.IV (or assigned by a
    /// local response analysis) and topographic amplification St of Tab. 3.2.V-VI (or assigned). Transferred from ANTHEA
    /// (RetainingWall.DeriveSeismic, commit fe4652c).
    /// </summary>
    public sealed class NtcSiteAmplification
    {
        public double AgG { get; }
        public double Ss { get; }
        public double St { get; }
        public double AmaxG => Ss * St * AgG;
        /// <summary>Formula used for Ss, in Italian for the reports.</summary>
        public string SoilFormula { get; }
        /// <summary>Formula used for St, in Italian for the reports.</summary>
        public string TopographyFormula { get; }
        private NtcSiteAmplification(double ag, double ss, double st, string soil, string topography) { AgG = ag; Ss = ss; St = st; SoilFormula = soil; TopographyFormula = topography; }

        /// <summary>
        /// Ss of Tab. 3.2.IV from the category and F0·ag/g (A: 1; B: 1 ≤ 1.40 − 0.40 F0 ag/g ≤ 1.20; C: 1 ≤ 1.70 − 0.60 F0 ag/g ≤ 1.50; D: 0.90 ≤
        /// 2.40 − 1.50 F0 ag/g ≤ 1.80; E: 1 ≤ 2.00 − 1.10 F0 ag/g ≤ 1.60). F0 between 2.2 and 10 (not used for A).
        /// </summary>
        public static (double Ss, string Formula) Stratigraphic(NtcSoilCategory category, double agG, double f0)
        {
            if (!(agG >= 0 && agG <= 1)) throw new ArgumentException("Sisma: ag/g allo SLV (es. 0,20, non 20), inserire un valore fra 0 e 1.");
            if (category != NtcSoilCategory.A && !(f0 >= 2.2 && f0 <= 10)) throw new ArgumentException("Sisma: F₀ allo SLV, inserire un valore fra 2.2 e 10.");
            double x = (category == NtcSoilCategory.A ? 0 : f0) * agG;
            switch (category)
            {
                case NtcSoilCategory.A: return (1, "Categoria A: Ss=1.");
                case NtcSoilCategory.B: return (Clamp(1.4 - .4 * x, 1, 1.2), "Categoria B: Ss=max(1; min(1,2; 1,4−0,4F₀·ag/g)).");
                case NtcSoilCategory.C: return (Clamp(1.7 - .6 * x, 1, 1.5), "Categoria C: Ss=max(1; min(1,5; 1,7−0,6F₀·ag/g)).");
                case NtcSoilCategory.D: return (Clamp(2.4 - 1.5 * x, .9, 1.8), "Categoria D: Ss=max(0,9; min(1,8; 2,4−1,5F₀·ag/g)).");
                case NtcSoilCategory.E: return (Clamp(2 - 1.1 * x, 1, 1.6), "Categoria E: Ss=max(1; min(1,6; 2−1,1F₀·ag/g)).");
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        /// <summary>
        /// St of Tab. 3.2.V-VI: 1 for a flat surface or a mean inclination up to 15°; otherwise T2 (slope), T3 (ridge up to 30°) or T4 (ridge),
        /// St = 1 + (Stmax − 1) z/H with Stmax 1.2 (T2, T3) or 1.4 (T4) when the relief is higher than 30 m (H and the height z of the site above
        /// its base in mm), 1 for lower reliefs (simplified amplification not required, §3.2.2). Slope in rad, below 89°.
        /// </summary>
        public static (double St, string Formula) Topographic(NtcTopography topography, double slope = 0, double reliefHeight = 0, double siteHeight = 0)
        {
            if (topography == NtcTopography.Flat) return (1, "Superficie pianeggiante, T1: St=1.");
            if (topography != NtcTopography.Slope && topography != NtcTopography.Ridge) throw new ArgumentOutOfRangeException(nameof(topography));
            double degrees = slope * 180 / Math.PI;
            if (!(degrees >= 0 && degrees <= 89)) throw new ArgumentException("Sisma: inclinazione media del pendio/rilievo [°], inserire un valore fra 0 e 89.");
            if (degrees <= 15) return (1, "Inclinazione media ≤15°, T1: St=1.");
            if (!(reliefHeight >= 10 && reliefHeight <= 9e6)) throw new ArgumentException("Sisma: altezza del pendio/rilievo [m], inserire un valore fra 0.01 e 9000.");
            if (!(siteHeight >= 0 && siteHeight <= reliefHeight)) throw new ArgumentException("Sisma: quota del muro sopra la base del pendio/rilievo [m], inserire un valore fra 0 e l'altezza del rilievo.");
            string category = topography == NtcTopography.Slope ? "T2" : degrees <= 30 ? "T3" : "T4";
            double peak = category == "T4" ? 1.4 : 1.2;
            return reliefHeight > 30000
                ? (1 + (peak - 1) * siteHeight / reliefHeight, $"{category}: St=1+({(category == "T4" ? "1,4" : "1,2")}−1)·z/H; z=quota dalla base, H=altezza del rilievo (non del muro).")
                : (1, "Pendio/rilievo di altezza ≤30 m: amplificazione topografica semplificata non richiesta, St=1 (§3.2.2).");
        }

        /// <summary>The site amplification from the category or an assigned Ss (0.1-5) and from the topography or an assigned St (1-5).</summary>
        public static NtcSiteAmplification Create(double agG, (NtcSoilCategory Category, double F0)? soil, double? assignedSs, (NtcTopography Topography, double Slope, double ReliefHeight, double SiteHeight)? topography,
            double? assignedSt)
        {
            if (!(agG >= 0 && agG <= 1)) throw new ArgumentException("Sisma: ag/g allo SLV (es. 0,20, non 20), inserire un valore fra 0 e 1.");
            double ss; string soilFormula;
            if (assignedSs.HasValue)
            {
                if (!(assignedSs.Value >= .1 && assignedSs.Value <= 5)) throw new ArgumentException("Sisma: Ss assegnato, inserire un valore fra 0.1 e 5.");
                ss = assignedSs.Value; soilFormula = "Ss assegnato dal progettista / risposta sismica locale.";
            }
            else if (soil.HasValue) (ss, soilFormula) = Stratigraphic(soil.Value.Category, agG, soil.Value.F0);
            else throw new ArgumentException("Sisma: scegliere la categoria di sottosuolo A–E dalla relazione geotecnica.");
            double st; string topographyFormula;
            if (assignedSt.HasValue)
            {
                if (!(assignedSt.Value >= 1 && assignedSt.Value <= 5)) throw new ArgumentException("Sisma: St assegnato, inserire un valore fra 1 e 5.");
                st = assignedSt.Value; topographyFormula = "St assegnato dal progettista / risposta sismica locale.";
            }
            else if (topography.HasValue) (st, topographyFormula) = Topographic(topography.Value.Topography, topography.Value.Slope, topography.Value.ReliefHeight, topography.Value.SiteHeight);
            else throw new ArgumentException("Sisma: scegliere pianeggiante, pendio o rilievo a cresta stretta.");
            return new NtcSiteAmplification(agG, ss, st, soilFormula, topographyFormula);
        }

        private static double Clamp(double value, double low, double high) => value < low ? low : value > high ? high : value;
    }
}
