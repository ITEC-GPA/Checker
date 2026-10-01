using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Cracking;

namespace GPC.Checkers.Concrete.Detailing
{
    public enum MemberDetailingKind { Beam, Column }

    /// <summary>
    /// Data of the 1D detailing of a beam or column section. Units mm, mm², MPa, N (compression positive). Widths are explicit: bt of the
    /// tension zone when the top or the bottom face is tensioned, bw of the web for the links. The bars and the outline are those of the section.
    /// </summary>
    public sealed class MemberDetailingInput
    {
        public MemberDetailingKind Kind { get; }
        public CrackSectionGeometry Geometry { get; }
        public double ConcreteArea { get; }
        public double Fck { get; }
        public double Fctm { get; }
        public double Fyk { get; }
        public double Fyd { get; }
        public double TopWidth { get; }
        public double BottomWidth { get; }
        public double WebWidth { get; }
        /// <summary>Largest design compression NEd of the member among the verified states, N (columns).</summary>
        public double Compression { get; }
        public bool HasLinks { get; }
        public double LinkDiameter { get; }
        public double LinkSpacing { get; }
        public int LinkLegs { get; }
        public double Aggregate { get; }
        /// <summary>Nominal cover to the links (to the bars without links), mm.</summary>
        public double NominalCover { get; }
        /// <summary>cmin,dur of the durability design, mm; null = not given (the cover checks stay pending).</summary>
        public double? MinimumDurabilityCover { get; }
        public double CoverDeviation { get; }
        public bool LapZone { get; }
        /// <summary>Confirmation that the compression bars are restrained by the links (corner bars, bars within 150 mm of a restrained bar).</summary>
        public bool CompressionBarsRestrained { get; }
        /// <summary>
        /// End zones confirmed: beams, anchorage of the bottom bars at the end supports and shift rule; columns (Eurocode), link spacing reduced within
        /// max(b; h) of beams and slabs.
        /// </summary>
        public bool EndZonesConfirmed { get; }

        public MemberDetailingInput(MemberDetailingKind kind, CrackSectionGeometry geometry, double concreteArea, double fck, double fctm, double fyk, double fyd,
            double topWidth, double bottomWidth, double webWidth, double compression, bool hasLinks, double linkDiameter, double linkSpacing, int linkLegs, double aggregate,
            double nominalCover, double? minimumDurabilityCover, double coverDeviation, bool lapZone, bool compressionBarsRestrained, bool endZonesConfirmed)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            if (geometry.Bars.Count == 0) throw new ArgumentException("Detailing: the section has no bars.");
            Kind = kind; ConcreteArea = concreteArea; Fck = fck; Fctm = fctm; Fyk = fyk; Fyd = fyd; TopWidth = topWidth; BottomWidth = bottomWidth; WebWidth = webWidth;
            Compression = compression; HasLinks = hasLinks; LinkDiameter = linkDiameter; LinkSpacing = linkSpacing; LinkLegs = linkLegs; Aggregate = aggregate;
            NominalCover = nominalCover; MinimumDurabilityCover = minimumDurabilityCover; CoverDeviation = coverDeviation; LapZone = lapZone;
            CompressionBarsRestrained = compressionBarsRestrained; EndZonesConfirmed = endZonesConfirmed;
        }
    }

    /// <summary>One detailing rule: actual value, limit (unit), verdict; null verdict = pending (data or confirmation missing, or rule not implemented).</summary>
    public sealed class DetailingCheck
    {
        /// <summary>Stable key, for example ClearSpacing, MinimumTension:Bottom, LinkSpacing.</summary>
        public string Key { get; }
        public double? Actual { get; }
        public double? Limit { get; }
        public string Unit { get; }
        public bool? Passed { get; }
        public string Reference { get; }
        public string Explanation { get; }
        /// <summary>Pending because the rule (a national value) is not implemented, not because data or a confirmation are missing.</summary>
        public bool NotImplemented { get; }
        internal DetailingCheck(string key, double? actual, double? limit, string unit, bool? passed, string reference, string explanation, bool notImplemented = false)
        { Key = key; Actual = actual; Limit = limit; Unit = unit; Passed = passed; Reference = reference; Explanation = explanation; NotImplemented = notImplemented; }
    }

    public sealed class MemberDetailingResult
    {
        public DetailingProfile Profile { get; internal set; }
        public MemberDetailingKind Kind { get; internal set; }
        public IReadOnlyList<DetailingCheck> Checks { get; internal set; }
        /// <summary>True when every rule is satisfied, false when one fails, null when some rule is pending and none fails.</summary>
        public bool? Passed => Checks.Any(c => c.Passed == false) ? false : Checks.Any(c => c.Passed == null) ? (bool?)null : true;
        public string Reference { get; internal set; }
    }

    /// <summary>
    /// Minimum and maximum reinforcement, links, spacing and cover of beam and column sections. NTC 2018 §4.1.6.1 transferred from ANTHEA
    /// (ConcreteDetailingCalculator, commit fe4652c; fixtures detailing-legacy.csv). EN 1992-1-1 §§8.2, 4.4.1, 9.2, 9.5 with the recommended values,
    /// UNI with DM 31/07/2012 (st,max ≤ 300 mm; columns Ømin 12, As,min 0.003 Ac, scl,tmax = min(12 Ømin; b; 250)), DS with DK NA (chapter 9
    /// unchanged except the beam As,min 9.2.1.1(1) and ρw,min 9.2.2(5), pending). Slab and wall rules stay with the plate checks.
    /// </summary>
    public static class MemberDetailingCalculator
    {
        public const string MethodId = "Concrete.MemberDetailing";

        public static MemberDetailingResult Calculate(DetailingProfile profile, MemberDetailingInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (new[] { p.Compression, p.LinkDiameter, p.LinkSpacing, p.Aggregate, p.CoverDeviation }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v < 0)
                || new[] { p.ConcreteArea, p.Fck, p.Fctm, p.Fyk, p.Fyd, p.TopWidth, p.BottomWidth, p.WebWidth }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                || p.LinkLegs < 0 || double.IsNaN(p.NominalCover) || p.NominalCover < 0)
                throw new ArgumentException("Detailing: numerical data must be finite and non negative.");
            var g = p.Geometry; var bars = g.Bars; var r = new List<DetailingCheck>();
            bool ntc = DetailingProfiles.IsNtc(profile), uni = profile == DetailingProfile.UniEN1992p11, ds = profile == DetailingProfile.DsEN1992p11;
            string reference = ntc ? "NTC 2018 §4.1.6.1" : profile == DetailingProfile.EN1992p11 ? "EN 1992-1-1" : uni ? "UNI EN 1992-1-1 + DM 31/07/2012" : "DS/EN 1992-1-1 + DK NA";
            void Min(string key, double actual, double limit, string unit, string clause, string expression)
                => r.Add(new DetailingCheck(key, double.IsInfinity(actual) || double.IsNaN(actual) ? (double?)null : actual, limit, unit, actual >= limit, reference + " " + clause, expression));
            void Max(string key, double actual, double limit, string unit, string clause, string expression)
                => r.Add(new DetailingCheck(key, double.IsInfinity(actual) || double.IsNaN(actual) ? (double?)null : actual, limit, unit, actual <= limit, reference + " " + clause, expression));
            void Pending(string key, string clause, string explanation) => r.Add(new DetailingCheck(key, null, null, "", null, reference + " " + clause, explanation));

            double minPhi = bars.Min(b => b.Diameter), maxPhi = bars.Max(b => b.Diameter), steel = bars.Sum(b => b.Area);
            double clear = double.PositiveInfinity;
            for (int i = 0; i < bars.Count; i++)
                for (int j = i + 1; j < bars.Count; j++)
                    clear = Math.Min(clear, CrackSectionGeometry.Hypot(bars[i].X - bars[j].X, bars[i].Y - bars[j].Y) - (bars[i].Diameter + bars[j].Diameter) / 2);
            Min("ClearSpacing", clear, Math.Max(20, Math.Max(maxPhi, p.Aggregate + 5)), "mm", ntc ? ".3 / EC2 §8.2" : "§8.2(2)", "max(20 mm; Ømax; dg + 5 mm) on every pair of bars");
            if (p.MinimumDurabilityCover.HasValue && p.MinimumDurabilityCover.Value >= 0)
            {
                double cdur = p.MinimumDurabilityCover.Value;
                Min("NominalCover", p.NominalCover, Math.Max(10, Math.Max(cdur, (p.HasLinks ? p.LinkDiameter : maxPhi) + (p.Aggregate > 32 ? 5 : 0))) + p.CoverDeviation, "mm",
                    ntc ? ".3 / EC2 §4.4" : "§4.4.1", "max(10; cmin,dur; cmin,b) + Δcdev");
                double margin = double.PositiveInfinity;
                foreach (var bar in bars)
                    margin = Math.Min(margin, g.BarCover(bar) - (Math.Max(10, Math.Max(cdur, bar.Diameter + (p.Aggregate > 32 ? 5 : 0))) + p.CoverDeviation));
                Min("BarCoverMargin", margin, 0, "mm", ntc ? ".3 / EC2 §4.4" : "§4.4.1", "minimum (geometric cover of the bar − required cover), outline and holes");
            }
            else Pending("NominalCover", ntc ? ".3" : "§4.4.1", "cmin,dur of the durability design is required.");

            if (p.Kind == MemberDetailingKind.Column) Column(profile, p, r, Min, Max, Pending, minPhi, maxPhi, steel);
            else Beam(profile, p, r, Min, Max, Pending, minPhi, steel);
            if (ntc && p.HasLinks && !p.CompressionBarsRestrained) Pending("BarsHeldByLinks", "", "Confirm the arrangement of links and ties on the compression bars.");
            if (p.LapZone && p.Kind == MemberDetailingKind.Column) Max("MaximumAtLap", steel, .08 * p.ConcreteArea, "mm2", ntc ? "EC2 §§9.5.2" : "§9.5.2(3)", "As ≤ 0.08 Ac at laps, all lapped bars in the section");
            return new MemberDetailingResult { Profile = profile, Kind = p.Kind, Checks = r.AsReadOnly(), Reference = DetailingProfiles.Reference(profile) };
        }

        private static void Column(DetailingProfile profile, MemberDetailingInput p, List<DetailingCheck> r, Action<string, double, double, string, string, string> Min,
            Action<string, double, double, string, string, string> Max, Action<string, string, string> Pending, double minPhi, double maxPhi, double steel)
        {
            var g = p.Geometry; bool ntc = DetailingProfiles.IsNtc(profile), uni = profile == DetailingProfile.UniEN1992p11;
            double linkSpacing = p.HasLinks ? p.LinkSpacing : double.PositiveInfinity, linkDiameter = p.HasLinks ? p.LinkDiameter : 0;
            if (ntc)
            {
                Min("LongitudinalDiameter", minPhi, 12, "mm", ".2", "Ømin ≥ 12 mm");
                var spacing = g.MaximumSpacing(Enumerable.Range(0, g.Bars.Count).ToArray());
                if (spacing.HasValue) Max("LongitudinalSpacing", spacing.Value, 300, "mm", ".2", "spacing on the faces / perimeter ≤ 300 mm");
                else Pending("LongitudinalSpacing", ".2", "Bar arrangement not recognised automatically.");
                Min("MinimumLongitudinal", steel, Math.Max(.1 * p.Compression / p.Fyd, .003 * p.ConcreteArea), "mm2", ".2", "max(0.10 NEd/fyd; 0.003 Ac), largest NEd");
                if (!p.LapZone) Max("MaximumLongitudinal", steel, .04 * p.ConcreteArea, "mm2", ".2", "As ≤ 0.04 Ac outside laps");
                Min("LinkDiameter", linkDiameter, Math.Max(6, maxPhi / 4), "mm", ".2", "max(6 mm; Ømax/4)");
                Max("LinkSpacing", linkSpacing, Math.Min(250, 12 * minPhi), "mm", ".2", "min(250 mm; 12 Ømin)");
                return;
            }
            double bMin = Math.Min(g.Width, g.Height);
            Min("LongitudinalDiameter", minPhi, uni ? 12 : 8, "mm", "§9.5.2(1)", uni ? "Ømin ≥ 12 mm (DM 31/07/2012)" : "Ømin ≥ 8 mm");
            Min("MinimumLongitudinal", steel, Math.Max(.1 * p.Compression / p.Fyd, (uni ? .003 : .002) * p.ConcreteArea), "mm2", "§9.5.2(2)",
                uni ? "max(0.10 NEd/fyd; 0.003 Ac) (DM 31/07/2012)" : "max(0.10 NEd/fyd; 0.002 Ac) (9.12N)");
            if (!p.LapZone) Max("MaximumLongitudinal", steel, .04 * p.ConcreteArea, "mm2", "§9.5.2(3)", "As ≤ 0.04 Ac outside laps");
            if (g.Circular) Min("BarCount", g.Bars.Count, 4, "-", "§9.5.2(4)", "at least 4 bars in a circular column");
            Min("LinkDiameter", linkDiameter, Math.Max(6, maxPhi / 4), "mm", "§9.5.3(1)", "max(6 mm; Ømax/4)");
            double limit = uni ? Math.Min(12 * minPhi, Math.Min(bMin, 250)) : Math.Min(20 * minPhi, Math.Min(bMin, 400));
            if (p.LapZone) limit *= .6; // 9.5.3(4): at laps (and near beams and slabs) the spacing is reduced by 0.6
            Max("LinkSpacing", linkSpacing, limit, "mm", "§9.5.3(3)-(4)", (uni ? "min(12 Ømin; b; 250 mm)" : "min(20 Ømin; b; 400 mm)") + (p.LapZone ? " × 0.6 at laps" : ""));
            if (!p.CompressionBarsRestrained)
                Pending("BarsHeldByLinks", "§9.5.3(6)", "Confirm that every corner bar is held by links and no compression bar is farther than 150 mm from a restrained bar.");
            if (!p.EndZonesConfirmed) Pending("LinkSpacingNearBeams", "§9.5.3(4)", "Within max(b; h) of beams and slabs the link spacing is reduced by 0.6: confirm the end zones of the member.");
        }

        private static DetailingCheck National(string key, string clause, string explanation)
            => new DetailingCheck(key, null, null, "", null, "DS/EN 1992-1-1 + DK NA " + clause, explanation, true);

        private static void Beam(DetailingProfile profile, MemberDetailingInput p, List<DetailingCheck> r, Action<string, double, double, string, string, string> Min,
            Action<string, double, double, string, string, string> Max, Action<string, string, string> Pending, double minPhi, double steel)
        {
            var g = p.Geometry; bool ntc = DetailingProfiles.IsNtc(profile), uni = profile == DetailingProfile.UniEN1992p11, ds = profile == DetailingProfile.DsEN1992p11;
            double minY = g.Outline.Min(v => v.Y), maxY = g.Outline.Max(v => v.Y), middle = (minY + maxY) / 2;
            double rhoMin = Math.Max(.26 * p.Fctm / p.Fyk, .0013);
            double linksPerMetre = p.HasLinks && p.LinkSpacing > 0 ? p.LinkLegs * Math.PI * p.LinkDiameter * p.LinkDiameter / 4 * 1000 / p.LinkSpacing : 0;
            // Both faces: a reversal of bending is not silently omitted.
            foreach (bool top in new[] { false, true })
            {
                string face = top ? "Top" : "Bottom";
                var faceBars = g.Bars.Where(b => top ? b.Y >= middle : b.Y < middle).ToArray();
                if (faceBars.Length == 0)
                {
                    if (ds) r.Add(National("MinimumTension:" + face, "§9.2.1.1(1)", "No bar in this half of the section; DK NA As,min not available."));
                    else Min("MinimumTension:" + face, 0, rhoMin * g.Width * g.Height, "mm2", ntc ? ".1" : "§9.2.1.1(1)", "No bar in this half of the section.");
                    continue;
                }
                double area = faceBars.Sum(b => b.Area), ys = faceBars.Sum(b => b.Area * b.Y) / area;
                double d = top ? ys - minY : maxY - ys, bt = top ? p.TopWidth : p.BottomWidth;
                if (ds) r.Add(National("MinimumTension:" + face, "§9.2.1.1(1)", "DK NA national value of As,min not available in this implementation."));
                else Min("MinimumTension:" + face, area, rhoMin * bt * d, "mm2", ntc ? ".1 / EC2 §9.3.1.1" : "§9.2.1.1(1)", "max(0.26 fctm/fyk; 0.0013) bt d; both faces potentially tensioned");
                if (!p.LapZone) Max("MaximumTension:" + face, area, .04 * p.ConcreteArea, "mm2", ntc ? ".1" : "§9.2.1.1(3)", "As of the zone ≤ 0.04 Ac");
                if (ntc)
                {
                    Min("MinimumLinks:" + face, linksPerMetre, 1.5 * p.WebWidth, "mm2/m", ".1", "Ast/s ≥ 1.5 b mm²/m");
                    Max("LinkSpacing:" + face, p.HasLinks ? p.LinkSpacing : double.PositiveInfinity, Math.Min(1000d / 3, .8 * d), "mm", ".1", "at least 3 links per metre; spacing ≤ 0.8 d");
                }
                else
                {
                    if (ds) r.Add(National("MinimumLinks:" + face, "§9.2.2(5)", "DK NA national value of ρw,min not available in this implementation."));
                    else Min("MinimumLinks:" + face, linksPerMetre, .08 * Math.Sqrt(p.Fck) / p.Fyk * p.WebWidth * 1000, "mm2/m", "§9.2.2(5)", "ρw,min = 0.08 √fck / fyk (9.5N), links at 90°");
                    Max("LinkSpacing:" + face, p.HasLinks ? p.LinkSpacing : double.PositiveInfinity, .75 * d, "mm", "§9.2.2(6)", "sl,max = 0.75 d (1 + cot α), α = 90°");
                    double legLimit = Math.Min(.75 * d, uni ? 300 : 600);
                    if (p.HasLinks && p.LinkLegs >= 2)
                        Max("LinkLegSpacing:" + face, (p.WebWidth - 2 * p.NominalCover - p.LinkDiameter) / (p.LinkLegs - 1), legLimit, "mm", "§9.2.2(8)",
                            "st = (bw − 2 cnom − Øw)/(n − 1), legs equally spaced across the web; st,max = " + (uni ? "min(0.75 d; 300 mm) (DM 31/07/2012)" : "min(0.75 d; 600 mm)"));
                    else Pending("LinkLegSpacing:" + face, "§9.2.2(8)", "Transverse spacing of the legs: fewer than two legs.");
                }
            }
            if (ntc) Max("CompressionBarRestraint", p.HasLinks ? p.LinkSpacing : double.PositiveInfinity, 15 * minPhi, "mm", ".1", "spacing ≤ 15Ø of the compression bars considered resistant");
            else if (!p.CompressionBarsRestrained) Pending("BarsHeldByLinks", "§9.2.1.2 / 9.5.3(6)", "Confirm the restraint of the compression bars considered in the resistance.");
            if (!p.EndZonesConfirmed) Pending("EndSupportAnchorage", ntc ? ".1" : "§9.2.1.4", "Confirm the shift rule and the bottom bars anchored at the end supports.");
        }
    }
}
