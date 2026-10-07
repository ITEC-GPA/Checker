using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Shear;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>
    /// Crack check of a section for one serviceability state. The stress state (strain plane in the section plane, ordinary bar stresses in the
    /// order of <see cref="CrackSectionGeometry.Bars"/>, MPa, compression negative) comes from the section solver; the crack width requires a
    /// linear analysis without tensile concrete. Lengths mm, stresses MPa.
    /// </summary>
    public sealed class SectionCrackInput
    {
        public Standard Standard { get; }
        public ServiceabilityCombination Combination { get; }
        /// <summary>Exposure class (<see cref="CrackRequirements.Exposures"/>); null when not given.</summary>
        public string Exposure { get; }
        public bool SensitiveReinforcement { get; }
        /// <summary>Design wlim, mm (Eurocode family and Model Code 2010); null = limit of the standard.</summary>
        public double? DesignLimit { get; }
        public CrackSectionGeometry Geometry { get; }
        public StrainPlane StrainPlane { get; }
        public IReadOnlyList<double> BarStresses { get; }
        public bool LinearAnalysis { get; }
        public bool TensileConcrete { get; }
        /// <summary>Tendons in the section: the crack width of prestressed members is not implemented.</summary>
        public bool Prestressed { get; }
        public double Es { get; }
        public double Ecm { get; }
        public double Fctm { get; }
        public bool ShortTerm { get; }
        public bool RibbedBars { get; }
        /// <summary>Cover to the surface of the longitudinal bars (nominal cover plus link diameter), mm.</summary>
        public double NominalCover { get; }
        public double? CoverOverride { get; }
        public double? SpacingOverride { get; }
        /// <summary>Maximum concrete stress (tension positive) of the uncracked linear section with tensile concrete, for decompression and crack formation.</summary>
        public Func<double> UncrackedMaximumConcreteStress { get; }
        /// <summary>
        /// Legacy rule of ANTHEA before the D7-b deviation, for NTC 2018 and CNR-DT 200 only: with the neutral axis inside the section k2 comes from the
        /// ordinary bar stresses (0.5 with a compressed bar, 1.0 otherwise, <see cref="CrackWidthCalculator.K2"/>), so a singly reinforced bent section gets
        /// k2 = 1. Kept to reproduce the frozen fixtures and the comparisons; the other profiles always use k2 = 0.5 when the neutral axis crosses the section.
        /// Default false (deviation D7-b of ANTHEA, docs/refactoring/scostamenti.md): k2 = 0.5 whenever the neutral axis is inside the section. It has no
        /// effect on entirely compressed or entirely tensile sections, nor on the inner surfaces of hollow sections.
        /// What it reproduces: wk, ratio, verdict, outcome, status, regions and the k2 of the partially compressed section as before D7-b. What it does not:
        /// <see cref="SectionCrackResult.K2"/> of an entirely compressed section (null with both rules, k2 does not apply; before D7-b it carried the k2 of
        /// the bars) and the trace texts (the k2 entry reads "legacy rule ..." and appears once).
        /// </summary>
        public bool NtcK2FromCompressedBars { get; }

        public SectionCrackInput(Standard standard, ServiceabilityCombination combination, string exposure, bool sensitiveReinforcement, double? designLimit,
            CrackSectionGeometry geometry, StrainPlane strainPlane, IEnumerable<double> barStresses, bool linearAnalysis, bool tensileConcrete, bool prestressed,
            double es, double ecm, double fctm, bool shortTerm, bool ribbedBars, double nominalCover, double? coverOverride = null, double? spacingOverride = null,
            Func<double> uncrackedMaximumConcreteStress = null, bool ntcK2FromCompressedBars = false)
        {
            Standard = standard ?? throw new ArgumentNullException(nameof(standard));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            StrainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
            BarStresses = (barStresses ?? throw new ArgumentNullException(nameof(barStresses))).ToArray();
            if (BarStresses.Count != geometry.Bars.Count) throw new ArgumentException("Cracking: one stress per ordinary bar is required.");
            if (!CrackSectionGeometry.Positive(es) || !CrackSectionGeometry.Positive(ecm) || !CrackSectionGeometry.Positive(fctm)) throw new ArgumentException("Cracking: Es, Ecm and fctm must be positive.");
            if (double.IsNaN(nominalCover) || double.IsInfinity(nominalCover) || nominalCover < 0) throw new ArgumentOutOfRangeException(nameof(nominalCover));
            if (coverOverride.HasValue && (double.IsNaN(coverOverride.Value) || coverOverride < 0)) throw new ArgumentOutOfRangeException(nameof(coverOverride));
            if (spacingOverride.HasValue && !CrackSectionGeometry.Positive(spacingOverride.Value)) throw new ArgumentOutOfRangeException(nameof(spacingOverride));
            Combination = combination; Exposure = exposure; SensitiveReinforcement = sensitiveReinforcement; DesignLimit = designLimit;
            LinearAnalysis = linearAnalysis; TensileConcrete = tensileConcrete; Prestressed = prestressed; Es = es; Ecm = ecm; Fctm = fctm;
            ShortTerm = shortTerm; RibbedBars = ribbedBars; NominalCover = nominalCover; CoverOverride = coverOverride; SpacingOverride = spacingOverride;
            UncrackedMaximumConcreteStress = uncrackedMaximumConcreteStress; NtcK2FromCompressedBars = ntcK2FromCompressedBars;
        }

        /// <summary>Ordinary bar stresses of a native stress result in the order of <see cref="CrackSectionGeometry.From"/> (linear: with the creep coefficients).</summary>
        public static double[] OrdinaryBarStresses(StressAnalysisResult stress, ReinforcedConcreteSection section)
        {
            if (stress == null) throw new ArgumentNullException(nameof(stress));
            if (section == null) throw new ArgumentNullException(nameof(section));
            var all = stress.LinearElasticAnalysis ? stress.GetRebarsTension(stress.PsiRebar ?? 0, stress.PsiTendon ?? 0) : stress.GetRebarsTension();
            return all.Where(r => r.rebar.RebarMaterial.SteelType != SteelMaterial.SteelTypes.Tendon && r.rebar.EpsilonP == 0).Select(r => r.tension).ToArray();
        }
    }

    public enum CrackVerdict { Satisfied, NotSatisfied, NotEvaluated, NotRequired }

    /// <summary>Why a crack check gave no verdict, or which branch produced it.</summary>
    public enum CrackOutcome
    {
        Evaluated, NotRequired, MissingExposure, MissingDesignLimit, PrestressNotSupported, RequiresLinearCrackedAnalysis, NeutralAxisUndetermined,
        NoTensileReinforcement, NoEffectiveArea, SpacingUndetermined, InnerSurfaceUnreinforced, InnerSurfaceNotSupported
    }

    /// <summary>
    /// Result of the crack check: criterion and limit, governing width (mm) with ratio wk/wlim, or the stress of the uncracked section for decompression
    /// and crack formation; effective area and steel, spacing and the list of all the regions checked (faces and inner surfaces are independent).
    /// </summary>
    public sealed class SectionCrackResult
    {
        public CrackProfile Profile { get; internal set; }
        public CrackRequirement Requirement { get; internal set; }
        public double? Width { get; internal set; }
        public double? Limit { get; internal set; }
        public double? Ratio { get; internal set; }
        public bool? Passed { get; internal set; }
        public CrackVerdict Verdict => Requirement?.Criterion == CrackCriterion.NotRequired ? CrackVerdict.NotRequired
            : Passed == true ? CrackVerdict.Satisfied : Passed == false ? CrackVerdict.NotSatisfied : CrackVerdict.NotEvaluated;
        public CrackOutcome Outcome { get; internal set; }
        public string Status { get; internal set; }
        public double? EffectiveArea { get; internal set; }
        public double? EffectiveSteel { get; internal set; }
        public double? BarSpacing { get; internal set; }
        /// <summary>Automatic, Manual or InnerSurface.</summary>
        public string SpacingSource { get; internal set; }
        /// <summary>
        /// k2 of the governing region: 0.5 with the neutral axis inside the section (reported also when wk = 0 in the cover or comes from the upper bound
        /// without bonded bars, where k2 does not enter), (εmax + εmin)/(2 εmax) for entirely tensile sections; for the inner surfaces of hollow sections
        /// (walls, ring) the same formula on the strains of the tensile band, also when the neutral axis cuts the section, so a governing inner wall can
        /// report k2 &gt; 0.5 in a bent section; null when the section is entirely compressed (k2 does not apply) or no width is computed before the k2 step.
        /// </summary>
        public double? K2 { get; internal set; }
        /// <summary>Decompression / crack formation: maximum stress of the uncracked section and its limit, MPa.</summary>
        public double? UncrackedMaximumStress { get; internal set; }
        public double? StressLimit { get; internal set; }
        public string GoverningRegion { get; internal set; }
        public string Reference { get; internal set; }
        public IReadOnlyList<CrackRegion> Regions { get; internal set; } = new CrackRegion[0];
        public IReadOnlyList<ShearCalculationDetail> Details { get; internal set; } = new ShearCalculationDetail[0];

        internal SectionCrackResult Copy() => (SectionCrackResult)MemberwiseClone();
    }

    /// <summary>
    /// Section crack check transferred from ANTHEA (Ntc2018Checks.Cracking, ConcreteTensionCracking, ConcreteInnerCracking, commit fe4652c), without the
    /// global spacing calculator. Entirely compressed sections: wk = 0, no k2. Partially compressed sections (neutral axis inside the section, D7-b): effective
    /// area beyond hc,eff along the strain gradient, governing bar stress, k2 = 0.5 for every profile (NTC and CNR-DT 200 from the bar stresses only with
    /// <see cref="SectionCrackInput.NtcK2FromCompressedBars"/>); neutral axis within the cover of the reinforced tensile edge: wk = 0; tensile bars outside Ac,eff: upper bound
    /// of EC2 7.3.4(3), eq. (7.14) with sr,max from (h − x). Entirely tensile sections: independent faces (±x, ±y or radial for circles), never summed;
    /// DS adds the coarse system with the whole section (DK NA 7.3.4(1)). Hollow sections: inner walls or ring checked independently, each with the k2 of
    /// its own tensile band, (εmax + max(0, εmin))/(2 εmax), for every profile and also when the neutral axis cuts the section (local area, EN 7.3.4(3));
    /// their h − x is εmax/|∇ε| bounded by the height of the section along the gradient, and with a negligible gradient uniform tension (k2 = 1,
    /// h − x = h of the section normal to the face), see <see cref="UniformTensionTolerance"/> (0.0.17.0).
    /// Fixtures: GPCChecker.Test.Concrete/Fixtures/crack-legacy.csv.
    /// </summary>
    public static class SectionCrackCheck
    {
        public const string MethodId = "Concrete.SectionCracking";

        /// <summary>Relative strain variation |∇ε| h/εmax over the section below which the inner bands are in uniform tension (k2 = 1): the
        /// section solver returns gradients of 1e-15…1e-12 1/mm for axial tension, whose direction means nothing; 1e-4 is an eccentricity of about h/120000.</summary>
        public const double UniformTensionTolerance = 1e-4;

        public static SectionCrackResult Evaluate(SectionCrackInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var profile = CrackProfiles.Resolve(p.Standard);
            var details = new List<ShearCalculationDetail>();
            void Add(string symbol, double value, string unit, string expression) => details.Add(new ShearCalculationDetail(symbol, value, unit, expression));
            var req = CrackRequirements.For(profile, p.Combination, p.Exposure, p.SensitiveReinforcement, p.DesignLimit);
            var result = new SectionCrackResult { Profile = profile, Requirement = req, Limit = req.Limit, Reference = CrackProfiles.Reference(profile) };
            SectionCrackResult Stop(CrackOutcome outcome, string status) { result.Outcome = outcome; result.Status = status; result.Details = details.AsReadOnly(); return result; }
            switch (req.Criterion)
            {
                case CrackCriterion.NotRequired:
                    return Stop(CrackOutcome.NotRequired, req.RequiredCombination.HasValue ? "Not required: check the " + req.RequiredCombination + " combination" : "Not required for this combination");
                case CrackCriterion.ExposureRequired: return Stop(CrackOutcome.MissingExposure, "Select the exposure class");
                case CrackCriterion.DesignLimitRequired: return Stop(CrackOutcome.MissingDesignLimit, "Assign the design wlim for these data");
            }
            var g = p.Geometry; var plane = p.StrainPlane;
            if (req.Criterion != CrackCriterion.CrackWidth)
            {
                // NTC 4.1.2.2.4.5: decompression and crack formation use the uncracked homogenized section, not wk / 0.
                if (p.UncrackedMaximumConcreteStress == null) throw new ArgumentException("Cracking: the stress of the uncracked section is required for " + req.Criterion + ".");
                double maximum = p.UncrackedMaximumConcreteStress();
                double limit = req.Criterion == CrackCriterion.Decompression ? 0 : p.Fctm / 1.2;
                Add("σct,max", maximum, "MPa", "maximum stress of the uncracked linear section with tensile concrete");
                Add("σct,lim", limit, "MPa", req.Criterion == CrackCriterion.Decompression ? "0" : "fctm / 1.2");
                result.UncrackedMaximumStress = maximum; result.StressLimit = limit; result.Passed = maximum <= limit;
                return Stop(CrackOutcome.Evaluated, req.Criterion + (maximum <= limit ? ": satisfied" : ": not satisfied"));
            }
            if (p.Prestressed) return Stop(CrackOutcome.PrestressNotSupported, "Crack width of prestressed members: bond / decompression model not implemented");
            if (!p.LinearAnalysis || p.TensileConcrete) return Stop(CrackOutcome.RequiresLinearCrackedAnalysis, "wk requires a linear analysis without tensile concrete");
            var stresses = p.BarStresses;
            // Validates the bar stresses (missing or not finite: ArgumentException) before any branch, as before D7-b; the value serves only the legacy rule.
            double barK2 = CrackWidthCalculator.K2(stresses);
            var points = g.Outline.Concat(g.Holes.SelectMany(h => h)).ToArray();
            var strains = points.Select(plane.GetStrain).ToArray();
            Add("εc,min", strains.Min(), "−", "minimum strain at the vertices"); Add("εc,max", strains.Max(), "−", "maximum strain at the vertices");
            if (strains.Max() <= 1e-12)
            {
                // Pure compression (also a tiny positive εc,max ≤ 1e-12): no crack, wk = 0. k2 does not enter: K2 stays null and the trace has no k2 entry.
                result.Width = 0; result.Ratio = 0; result.Passed = true;
                return Stop(CrackOutcome.Evaluated, "Section entirely compressed");
            }
            if (strains.Min() >= 0) return Inner(FullyTensioned(p, profile, req.Limit.Value, details), p, profile);
            // Neutral axis inside the section: the section is bent, also under axial compression or tension with bending, so k2 = 0.5 for every profile
            // (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3), k2 of (7.11)). Legacy rule of ANTHEA for NTC 2018 / CNR-DT 200: k2 from the bar stresses.
            bool legacyK2 = p.NtcK2FromCompressedBars && CrackProfiles.IsNtc(profile);
            double k2 = legacyK2 ? barK2 : .5;
            result.K2 = k2;
            Add("k2", k2, "−", legacyK2 ? "legacy rule (before D7-b): 0.5 with a compressed bar, 1.0 otherwise"
                : "neutral axis inside the section: bending, k2 = 0.5 (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3))");
            double gradient = CrackSectionGeometry.Hypot(plane.ChiX, plane.ChiY);
            if (gradient <= 1e-15) return Stop(CrackOutcome.NeutralAxisUndetermined, "Neutral axis not determined");
            double qx = plane.ChiX / gradient, qy = plane.ChiY / gradient;
            double Q(double x, double y) => qx * x + qy * y;
            double top = points.Max(v => Q(v.X, v.Y)), bottom = points.Min(v => Q(v.X, v.Y)), height = top - bottom;
            double tensileDepth = strains.Max() / gradient;
            Add("h", height, "mm", "projected height along the strain gradient"); Add("h − x", tensileDepth, "mm", "εc,max / |∇ε|");
            var tensile = Enumerable.Range(0, g.Bars.Count).Where(i => plane.GetStrain(g.Bars[i].X, g.Bars[i].Y) > 0).ToArray();
            if (tensile.Length == 0)
            {
                // Neutral axis within the cover of the tensile edge: the reinforcement of that face (closer than h/2, upper bound of hc,eff) is compressed,
                // σs ≤ 0 and wk = 0 (limit of EC2 7.3.4 for σs → 0). Without bars on that side the tensile zone is unreinforced: no verdict.
                double nearest = g.Bars.Count == 0 ? double.PositiveInfinity : top - g.Bars.Max(b => Q(b.X, b.Y));
                if (!(nearest < height / 2)) return Stop(CrackOutcome.NoTensileReinforcement, "No tensile reinforcement");
                Add("h − d,min", nearest, "mm", "Qmax − Q of the bar nearest to the tensile edge > h − x");
                result.Width = 0; result.Ratio = 0; result.Passed = true;
                Stop(CrackOutcome.Evaluated, "Neutral axis within the cover: no tensile bar, wk = 0");
                return Inner(result, p, profile);
            }
            double centroid = tensile.Sum(i => Q(g.Bars[i].X, g.Bars[i].Y) * g.Bars[i].Area) / tensile.Sum(i => g.Bars[i].Area);
            double coverToCenter = top - centroid;
            double hc = g.EffectiveDepth(profile, qx, qy, top, height, coverToCenter, tensileDepth, false, p.NominalCover);
            Add("h − d", coverToCenter, "mm", "Qmax − Q of the tensile bars"); Add("hc,eff", hc, "mm", HcExpression(profile));
            if (hc <= 0) return Stop(CrackOutcome.NoEffectiveArea, "Zero effective area");
            double level = top - hc;
            var effective = tensile.Where(i => Q(g.Bars[i].X, g.Bars[i].Y) >= level - 1e-8).ToArray();
            var region = g.Region("TensileZone", qx, qy, level, effective);
            double aceff = region.Area;
            Add("Ac,eff", aceff, "mm2", "concrete beyond Q = Qmax − hc,eff"); Add("As,eff", region.SteelArea, "mm2", effective.Length + " tensile bars");
            double wlim = req.Limit.Value;
            if (effective.Length == 0)
            {
                // Tensile bars deeper than hc,eff (neutral axis close to the bars): no bonded bar in Ac,eff, upper bound of EC2 7.3.4(3), eq. (7.14) with the tensile bars.
                double sigmaT = tensile.Max(i => stresses[i]), phiT = tensile.Sum(i => g.Bars[i].Diameter * g.Bars[i].Diameter) / tensile.Sum(i => g.Bars[i].Diameter);
                Add("Øeq", phiT, "mm", "ΣØ²/ΣØ of the tensile bars"); Add("σs", sigmaT, "MPa", "maximum stress of the tensile bars, none in Ac,eff");
                double bound = CrackWidthCalculator.UnbondedUpperBound(profile, sigmaT, p.Es, p.Fctm, phiT, tensileDepth, p.ShortTerm, p.RibbedBars, details);
                result.Width = bound; result.Ratio = bound / wlim; result.Passed = bound <= wlim; result.EffectiveArea = aceff; result.EffectiveSteel = 0;
                result.GoverningRegion = region.Key; result.Regions = new[] { region.WithWidth(bound) };
                Stop(CrackOutcome.Evaluated, "No bar in Ac,eff: upper bound with sr,max from (h − x)" + (bound <= wlim ? ", crack width within the limit" : ", crack width beyond the limit"));
                return Inner(result, p, profile);
            }
            if (aceff <= 0) return Stop(CrackOutcome.NoEffectiveArea, "No effective reinforcement or area");
            double steel = effective.Sum(i => g.Bars[i].Area), phi = effective.Sum(i => g.Bars[i].Diameter * g.Bars[i].Diameter) / effective.Sum(i => g.Bars[i].Diameter);
            double sigma = effective.Max(i => stresses[i]);
            double c = p.CoverOverride ?? p.NominalCover;
            double? spacing = p.SpacingOverride ?? g.MaximumSpacing(effective);
            Add("Øeq", phi, "mm", "ΣØ²/ΣØ"); Add("σs", sigma, "MPa", "maximum stress of the effective bars"); Add("c", c, "mm", p.CoverOverride.HasValue ? "assigned" : "nominal cover + link");
            result.EffectiveArea = aceff; result.EffectiveSteel = steel;
            if (!(spacing > 0)) return Stop(CrackOutcome.SpacingUndetermined, "Automatic spacing not determined: assign the maximum spacing");
            Add("s", spacing.Value, "mm", p.SpacingOverride.HasValue ? "assigned maximum spacing" : "maximum spacing of the effective tensile bars");
            // The k2 of the width formula is the section-level k2 already in the trace with its reason: one k2 entry only.
            var formula = new List<ShearCalculationDetail>();
            double width = CrackWidthCalculator.Width(profile, new CrackWidthInput(sigma, p.Es, p.Ecm, p.Fctm, steel / aceff, phi, c, spacing.Value, tensileDepth, p.ShortTerm, p.RibbedBars, k2), formula);
            details.AddRange(formula.Where(d => d.Symbol != "k2"));
            result.Width = width; result.Ratio = width / wlim; result.Passed = width <= wlim; result.BarSpacing = spacing;
            result.SpacingSource = p.SpacingOverride.HasValue ? "Manual" : "Automatic"; result.GoverningRegion = region.Key;
            result.Regions = new[] { region.WithWidth(width) };
            Stop(CrackOutcome.Evaluated, width <= wlim ? "Crack width within the limit" : "Crack width beyond the limit");
            return Inner(result, p, profile);
        }

        private static string HcExpression(CrackProfile profile) => profile == CrackProfile.DsEN1992p11 ? "band with centroid at the tensile reinforcement (DK NA Fig. 7.100)"
            : profile == CrackProfile.DinEN1992p11 ? "(2 + 0.1 h/(h − d)) (h − d) in [2.5; 5] (h − d), DIN NCI 7.3.2(3)" : "min[2.5 (h − d); (h − x)/3; h/2]";

        private static SectionCrackResult FullyTensioned(SectionCrackInput p, CrackProfile profile, double limit, List<ShearCalculationDetail> details)
        {
            var g = p.Geometry; var plane = p.StrainPlane; var stresses = p.BarStresses;
            var result = new SectionCrackResult { Profile = profile, Limit = limit, Reference = CrackProfiles.Reference(profile) };
            var regions = new List<CrackRegion>(); var widths = new List<double>(); var spacings = new List<double>();
            var strains = g.Outline.Select(plane.GetStrain).ToArray();
            double maxStrain = strains.Max(), k2 = maxStrain > 0 ? Math.Min(1, Math.Max(.5, (strains.Min() + maxStrain) / (2 * maxStrain))) : 1;
            result.K2 = k2;
            details.Add(new ShearCalculationDetail("k2", k2, "−", "entirely tensile: (εmax + εmin)/(2 εmax); uniform tension: 1"));
            // Opposite faces are independent; effective areas of different directions are never added.
            var faces = new List<Tuple<string, double, double>> { Tuple.Create("Face+x", 1d, 0d), Tuple.Create("Face-x", -1d, 0d), Tuple.Create("Face+y", 0d, 1d), Tuple.Create("Face-y", 0d, -1d) };
            if (g.Circular)
            {
                faces.Clear();
                var angles = g.Bars.Select(b => Math.Atan2(b.Y, b.X)).Concat(new[] { Math.Atan2(plane.ChiY, plane.ChiX), Math.Atan2(plane.ChiY, plane.ChiX) + Math.PI }).ToArray();
                foreach (double angle in angles)
                {
                    double x = Math.Cos(angle), y = Math.Sin(angle);
                    if (!faces.Any(f => CrackSectionGeometry.Hypot(f.Item2 - x, f.Item3 - y) < 1e-8))
                        faces.Add(Tuple.Create("Radial(" + (angle * 180 / Math.PI).ToString("0.##", CultureInfo.InvariantCulture) + ")", x, y));
                }
            }
            SectionCrackResult Stop(CrackOutcome outcome, string status, CrackRegion region)
            {
                result.Outcome = outcome; result.Status = status; result.Regions = regions.Concat(new[] { region }).ToArray(); result.Details = details.AsReadOnly(); return result;
            }
            foreach (var face in faces)
            {
                string key = face.Item1; double qx = face.Item2, qy = face.Item3;
                double Q(double x, double y) => qx * x + qy * y;
                double top = g.Outline.Max(v => Q(v.X, v.Y)), bottom = g.Outline.Min(v => Q(v.X, v.Y)), height = top - bottom;
                double edge = g.Bars.Max(b => Q(b.X, b.Y)), largest = g.Bars.Max(b => b.Diameter);
                var face_ = g.Bars.Where(b => Q(b.X, b.Y) >= edge - largest).ToArray();
                double center = face_.Sum(b => Q(b.X, b.Y) * b.Area) / face_.Sum(b => b.Area);
                double hc = g.EffectiveDepth(profile, qx, qy, top, height, top - center, height, true, p.NominalCover), level = top - hc;
                var indices = Enumerable.Range(0, g.Bars.Count).Where(i => Q(g.Bars[i].X, g.Bars[i].Y) >= level - 1e-8 && stresses[i] > 0).ToArray();
                var region = g.Region(key, qx, qy, level, indices);
                details.Add(new ShearCalculationDetail(key + " · hc,eff", hc, "mm", "effective depth of the entirely tensile section"));
                details.Add(new ShearCalculationDetail(key + " · Ac,eff", region.Area, "mm2", "clipped outline minus holes"));
                details.Add(new ShearCalculationDetail(key + " · As,eff", region.SteelArea, "mm2", indices.Length + " bars"));
                if (region.Area <= 0 || region.SteelArea <= 0) return Stop(CrackOutcome.NoEffectiveArea, key + ": no effective area or reinforcement", region);
                double phi = indices.Sum(i => g.Bars[i].Diameter * g.Bars[i].Diameter) / indices.Sum(i => g.Bars[i].Diameter);
                double cover = p.CoverOverride ?? indices.Min(i => top - Q(g.Bars[i].X, g.Bars[i].Y) - g.Bars[i].Diameter / 2);
                double? spacing = p.SpacingOverride ?? g.MaximumSpacing(indices);
                if (!(spacing > 0)) return Stop(CrackOutcome.SpacingUndetermined, key + ": assign the maximum bar spacing", region);
                double sigma = indices.Max(i => stresses[i]);
                var calculation = new List<ShearCalculationDetail>();
                double width = CrackWidthCalculator.Width(profile, new CrackWidthInput(sigma, p.Es, p.Ecm, p.Fctm, region.SteelArea / region.Area, phi, cover, spacing.Value, height,
                    p.ShortTerm, p.RibbedBars, k2), calculation);
                details.AddRange(calculation.Select(d => new ShearCalculationDetail(key + " · " + d.Symbol, d.Value, d.Unit, d.Expression)));
                regions.Add(region.WithWidth(width)); widths.Add(width); spacings.Add(spacing.Value);
            }
            if (profile == CrackProfile.DsEN1992p11)
            {
                var all = Enumerable.Range(0, g.Bars.Count).ToArray();
                double area = CrackSectionGeometry.Area(g.Outline) - g.Holes.Sum(h => CrackSectionGeometry.Area(h)), steel = g.Bars.Sum(b => b.Area);
                double phi = g.Bars.Sum(b => b.Diameter * b.Diameter) / g.Bars.Sum(b => b.Diameter);
                double cover = p.CoverOverride ?? g.Bars.Min(b => g.BarCover(b));
                double? spacing = p.SpacingOverride ?? g.MaximumSpacing(all);
                var coarse = new CrackRegion("DsCoarseSystem", 0, 0, 0, area, all, steel, null, g.Outline, g.Holes);
                if (!(spacing > 0)) { result.Outcome = CrackOutcome.SpacingUndetermined; result.Status = "DS coarse system: assign the maximum spacing"; result.Regions = regions.ToArray(); result.Details = details.AsReadOnly(); return result; }
                var trace = new List<ShearCalculationDetail>();
                double width = .5 * CrackWidthCalculator.Width(CrackProfile.DsEN1992p11, new CrackWidthInput(stresses.Max(), p.Es, p.Ecm, p.Fctm, steel / area, phi, cover, spacing.Value,
                    Math.Max(g.Width, g.Height), p.ShortTerm, p.RibbedBars, k2), trace);
                details.AddRange(trace.Select(d => new ShearCalculationDetail("DsCoarseSystem · " + d.Symbol, d.Value, d.Unit, d.Expression)));
                details.Add(new ShearCalculationDetail("DsCoarseSystem · wk", width, "mm", "0.5 · (7.8) with the whole tensile section, DK NA 7.3.4(1)"));
                regions.Add(coarse.WithWidth(width)); widths.Add(width); spacings.Add(spacing.Value);
            }
            int governing = widths.IndexOf(widths.Max()); var gr = regions[governing]; double w = widths[governing];
            result.Width = w; result.Ratio = w / limit; result.Passed = w <= limit; result.EffectiveArea = gr.Area; result.EffectiveSteel = gr.SteelArea;
            result.BarSpacing = spacings[governing]; result.SpacingSource = p.SpacingOverride.HasValue ? "Manual" : "Automatic"; result.GoverningRegion = gr.Key;
            result.Regions = regions.ToArray(); result.Outcome = CrackOutcome.Evaluated;
            result.Status = "Entirely tensile · " + gr.Key + (w <= limit ? " · crack width within the limit" : " · crack width beyond the limit");
            details.Add(new ShearCalculationDetail("wk", w, "mm", "governing region " + gr.Key));
            result.Details = details.AsReadOnly();
            return result;
        }

        /// <summary>Additional checks on the cavity boundary: each wall or ring independently; an unreinforced tensile inner surface cannot pass.</summary>
        private static SectionCrackResult Inner(SectionCrackResult outer, SectionCrackInput p, CrackProfile profile)
        {
            var g = p.Geometry;
            if (g.Holes.Count == 0 || !outer.Limit.HasValue || !outer.Width.HasValue) return outer;
            double limit = outer.Limit.Value; var plane = p.StrainPlane; var stresses = p.BarStresses;
            double E(Point2d v) => plane.GetStrain(v);
            if (g.Holes.SelectMany(h => h).Max(E) <= 1e-12) { var compressed = outer.Copy(); compressed.Status += " · inner outline compressed"; return compressed; }
            var details = outer.Details.ToList(); var regions = outer.Regions.ToList();
            var results = new List<SectionCrackResult> { outer };
            // Tensile depth h − x of a band (EN 1992-1-1 7.3.4(3), eq. (7.14)): εmax/|∇ε| from the neutral axis, never beyond the height h of the
            // section along the gradient (x ≥ 0), as for the faces of an entirely tensile section. A gradient negligible against the strain
            // (solver noise, about 1e-15…1e-12 1/mm) has no direction: uniform tension, whole band, k2 = 1, h − x = h of the section normal to the face.
            // Before 0.0.17.0 εmax/|∇ε| was used unbounded (about 1e12 mm with a noise gradient).
            double gradient = CrackSectionGeometry.Hypot(plane.ChiX, plane.ChiY);
            double gx = gradient > 0 ? plane.ChiX / gradient : 0, gy = gradient > 0 ? plane.ChiY / gradient : 0;
            double sectionAlongGradient = g.Outline.Max(v => gx * v.X + gy * v.Y) - g.Outline.Min(v => gx * v.X + gy * v.Y);
            bool uniform = gradient < 1e-15 || gradient * sectionAlongGradient <= UniformTensionTolerance * g.Outline.Max(E);
            IReadOnlyList<Point2d> Tension(IReadOnlyList<Point2d> polygon)
                => uniform ? polygon : CrackSectionGeometry.Clip(polygon, gx, gy, -plane.GetStrain(0, 0) / gradient);
            void Check(string key, IReadOnlyList<Point2d> outline, IEnumerable<IReadOnlyList<Point2d>> holes, int[] indices, Func<CrackBar, double> cover, double effectiveDepth,
                double uniformDepth)
            {
                var polygon = Tension(outline); var clipped = holes.Select(Tension).Where(v => v.Count >= 3).ToArray();
                double area = CrackSectionGeometry.Area(polygon) - clipped.Sum(v => CrackSectionGeometry.Area(v)), steel = indices.Sum(i => g.Bars[i].Area);
                if (area <= 1e-8) return;
                var region = new CrackRegion(key, 0, 0, 0, area, indices, steel, null, polygon, clipped);
                regions.Add(region);
                if (steel <= 0)
                {
                    results.Add(new SectionCrackResult { Limit = limit, Outcome = CrackOutcome.InnerSurfaceUnreinforced, Status = key + ": inner surface in tension without effective reinforcement" });
                    return;
                }
                var bars = indices.Select(i => g.Bars[i]).ToArray();
                double sigma = indices.Max(i => stresses[i]);
                double phi = bars.Sum(b => b.Diameter * b.Diameter) / bars.Sum(b => b.Diameter);
                double c = p.CoverOverride ?? bars.Min(cover);
                double? spacing = p.SpacingOverride ?? g.MaximumSpacing(indices);
                if (!(spacing > 0))
                {
                    results.Add(new SectionCrackResult { Limit = limit, Outcome = CrackOutcome.SpacingUndetermined, Status = key + ": assign the maximum spacing for the inner surface" });
                    return;
                }
                var strains = polygon.Select(E).ToArray();
                double k2 = uniform ? 1 : strains.Max() > 0 ? Math.Min(1, Math.Max(.5, (Math.Max(0, strains.Min()) + strains.Max()) / (2 * strains.Max()))) : 1;
                double fromNeutralAxis = uniform ? double.PositiveInfinity : strains.Max() / gradient;
                double depth = uniform ? uniformDepth : Math.Min(fromNeutralAxis, sectionAlongGradient);
                var trace = new List<ShearCalculationDetail> { new ShearCalculationDetail("hc,eff", effectiveDepth, "mm", "band of the inner wall / ring, at most half the thickness") };
                // Written only where the bound acts, so that the trace of the other cases is unchanged.
                if (uniform) trace.Add(new ShearCalculationDetail("h − x", depth, "mm", "uniform tension (|∇ε| h ≤ " + UniformTensionTolerance.ToString("G1", CultureInfo.InvariantCulture)
                    + " εmax): h of the section normal to the face, whole band tensile, k2 = 1 (EN 1992-1-1 7.3.4(3), eq. (7.14))"));
                else if (fromNeutralAxis > sectionAlongGradient) trace.Add(new ShearCalculationDetail("h − x", depth, "mm", "min[εmax/|∇ε| = "
                    + fromNeutralAxis.ToString("G6", CultureInfo.InvariantCulture) + " mm; h along the gradient]: neutral axis outside the section, x = 0 (EN 1992-1-1 7.3.4(3), eq. (7.14))"));
                double width = CrackWidthCalculator.Width(profile, new CrackWidthInput(sigma, p.Es, p.Ecm, p.Fctm, steel / area, phi, c, spacing.Value, depth, p.ShortTerm, p.RibbedBars, k2), trace);
                details.Add(new ShearCalculationDetail(key + " · Ac,eff", area, "mm2", "tensile band of the inner surface, hole excluded"));
                details.Add(new ShearCalculationDetail(key + " · As,eff", steel, "mm2", indices.Length + " bars"));
                details.AddRange(trace.Select(d => new ShearCalculationDetail(key + " · " + d.Symbol, d.Value, d.Unit, d.Expression)));
                regions[regions.Count - 1] = region.WithWidth(width);
                results.Add(new SectionCrackResult { Width = width, Limit = limit, Ratio = width / limit, Passed = width <= limit, Status = key, EffectiveArea = area, EffectiveSteel = steel,
                    BarSpacing = spacing, SpacingSource = "InnerSurface", K2 = k2, GoverningRegion = key, Outcome = CrackOutcome.Evaluated, Details = trace.AsReadOnly() });
            }
            var tensile = Enumerable.Range(0, g.Bars.Count).Where(i => stresses[i] > 0).ToArray();
            double factor = profile == CrackProfile.DsEN1992p11 ? 2 : 2.5;
            if (g.Circular)
            {
                var hole = g.Holes[0];
                double ri = hole.Max(v => CrackSectionGeometry.Hypot(v.X, v.Y)), outerRadius = g.Outline.Max(v => CrackSectionGeometry.Hypot(v.X, v.Y)), wall = outerRadius - ri;
                double nearest = tensile.Select(i => CrackSectionGeometry.Hypot(g.Bars[i].X, g.Bars[i].Y) - ri).DefaultIfEmpty(wall).Min();
                double hc = Math.Min(factor * nearest, wall / 2), radius = ri + hc;
                var outline = hole.Select(v => new Point2d(v.X * radius / ri, v.Y * radius / ri)).Reverse().ToArray();
                var indices = tensile.Where(i => CrackSectionGeometry.Hypot(g.Bars[i].X, g.Bars[i].Y) <= radius + 1e-8).ToArray();
                Check("InnerRing", outline, g.Holes, indices, b => CrackSectionGeometry.Hypot(b.X, b.Y) - ri - b.Diameter / 2, hc, Math.Max(g.Width, g.Height));
            }
            else if (g.Holes.Count == 1 && AxisAlignedRectangle(g.Holes[0]))
            {
                foreach (var face in new[] { Tuple.Create("InnerWall+x", 1d, 0d), Tuple.Create("InnerWall-x", -1d, 0d), Tuple.Create("InnerWall+y", 0d, 1d), Tuple.Create("InnerWall-y", 0d, -1d) })
                {
                    double qx = face.Item2, qy = face.Item3;
                    double Q(double x, double y) => qx * x + qy * y;
                    double T(double x, double y) => -qy * x + qx * y;
                    var hole = g.Holes[0];
                    double inner = hole.Max(v => Q(v.X, v.Y)), edge = g.Outline.Max(v => Q(v.X, v.Y)), wall = edge - inner;
                    var side = hole.Where(v => Math.Abs(Q(v.X, v.Y) - inner) < 1e-8).ToArray();
                    if (side.Max(E) <= 1e-12) continue;
                    double tangentMin = side.Min(v => T(v.X, v.Y)), tangentMax = side.Max(v => T(v.X, v.Y));
                    var candidates = tensile.Where(i => Q(g.Bars[i].X, g.Bars[i].Y) > inner && T(g.Bars[i].X, g.Bars[i].Y) >= tangentMin - 1e-8
                        && T(g.Bars[i].X, g.Bars[i].Y) <= tangentMax + 1e-8).ToArray();
                    double nearest = candidates.Select(i => Q(g.Bars[i].X, g.Bars[i].Y) - inner).DefaultIfEmpty(wall).Min();
                    double hc = Math.Min(factor * nearest, wall / 2);
                    IReadOnlyList<Point2d> Band(IReadOnlyList<Point2d> v) => CrackSectionGeometry.Clip(CrackSectionGeometry.Clip(CrackSectionGeometry.Clip(
                        CrackSectionGeometry.Clip(v, qx, qy, inner), -qx, -qy, -inner - hc), -qy, qx, tangentMin), qy, -qx, -tangentMax);
                    var indices = candidates.Where(i => Q(g.Bars[i].X, g.Bars[i].Y) <= inner + hc + 1e-8).ToArray();
                    Check(face.Item1, Band(g.Outline), g.Holes.Select(Band).Where(v => v.Count >= 3), indices, b => Q(b.X, b.Y) - inner - b.Diameter / 2, hc,
                        edge - g.Outline.Min(v => Q(v.X, v.Y)));
                }
            }
            else results.Add(new SectionCrackResult { Limit = limit, Outcome = CrackOutcome.InnerSurfaceNotSupported,
                Status = "Inner surfaces: implemented for one axis-aligned rectangular hole or a circular ring" });
            var governing = results.Where(r => r.Width.HasValue).OrderByDescending(r => r.Width.Value).First();
            foreach (var r in results.Where(r => r.Width.HasValue)) if (r.Width.Value == governing.Width.Value) { governing = r; break; } // first maximum, as MaxBy
            bool incomplete = results.Any(r => !r.Width.HasValue);
            var final = governing.Copy();
            final.Profile = outer.Profile; final.Requirement = outer.Requirement; final.Reference = outer.Reference;
            if (!ReferenceEquals(governing, outer)) details.Add(new ShearCalculationDetail("wk", governing.Width.Value, "mm", "envelope of the outer and inner surfaces, governing " + governing.Status));
            string missing = string.Join("; ", results.Where(r => !r.Width.HasValue).Select(r => r.Status));
            final.Details = details.AsReadOnly(); final.Regions = regions.ToArray();
            final.Passed = governing.Width > limit ? false : incomplete ? (bool?)null : true;
            final.Ratio = incomplete && governing.Width <= limit ? null : governing.Ratio;
            final.Status = incomplete ? governing.Status + " · " + missing : governing.Status + " · outer and inner surfaces checked";
            if (incomplete && final.Passed == null) final.Outcome = results.First(r => !r.Width.HasValue).Outcome;
            return final;
        }

        private static bool AxisAlignedRectangle(IReadOnlyList<Point2d> polygon)
        {
            if (polygon.Count != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % 4];
                if (Math.Abs(a.X - b.X) > 1e-9 && Math.Abs(a.Y - b.Y) > 1e-9) return false;
            }
            return true;
        }
    }
}
