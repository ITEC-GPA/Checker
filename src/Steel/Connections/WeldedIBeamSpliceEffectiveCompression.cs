using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Explicit alternative to the class1-3 compression screen. Normal filled
    /// holes, same physical fastener layout, EC Table3.3 inter-fastener strip and
    /// outstand requirements. Effective widths use the shared Steel/bridge kernel.
    /// The explicit low-shear overload extends the default zero-shear contract within a bounded domain;
    /// the explicit shear-buckling overload replaces that bound by EN1993-1-5 section5 and 7.1.</summary>
    internal static class WeldedIBeamSpliceEffectiveCompression
    {
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p)
            => Verify(p, false);

        /// <summary>Opt-in low-shear class4 extension. Keeps the default zero-shear
        /// contract. No free-panel buckling or high-shear interaction is inferred.</summary>
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, bool allowLowShear)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, allowLowShear, WeldedIBeamSplicePlates.Verify(p.Plates));
        }
        /// <summary>Opt-in shear-buckling extension: EN1993-1-5 section5 resistance of the whole-width
        /// panel and the 7.1 interaction replace the low-shear bound. Defaults are unchanged.</summary>
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> VerifyWithShearBuckling(WeldedIBeamSpliceDetailInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, SplicePlateStabilityMethod.EffectiveCompressionWithShearBuckling, WeldedIBeamSplicePlates.Verify(p.Plates));
        }
        internal static bool UsesEffectiveWidths(SplicePlateStabilityMethod method)
            => method == SplicePlateStabilityMethod.EffectiveCompression || method == SplicePlateStabilityMethod.EffectiveCompressionWithLowShear
                || method == SplicePlateStabilityMethod.EffectiveCompressionWithShearBuckling;
        internal static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, SplicePlateStabilityMethod method, WeldedIBeamSplicePlateResult plates)
        {
            if (!UsesEffectiveWidths(method)) throw new ArgumentOutOfRangeException(nameof(method));
            return Verify(p, method == SplicePlateStabilityMethod.EffectiveCompression ? ShearMode.ZeroShear
                : method == SplicePlateStabilityMethod.EffectiveCompressionWithLowShear ? ShearMode.LowShear : ShearMode.ShearBuckling, plates);
        }
        internal static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, bool allowLowShear, WeldedIBeamSplicePlateResult plates)
            => Verify(p, allowLowShear ? ShearMode.LowShear : ShearMode.ZeroShear, plates);
        private enum ShearMode { ZeroShear, LowShear, ShearBuckling }
        private static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, ShearMode mode, WeldedIBeamSplicePlateResult plates)
        {
            bool allowLowShear = mode == ShearMode.LowShear;
            if (!ReferenceEquals(p.Plates, plates.Input)) throw new ArgumentException("Prepared plates belong to another input.");
            var code = p.Plates.Policy.Code; var d = p.Plates.Bolts.Detail;
            var forces = plates.PreparedBolts.Forces;
            var issues = new List<string>(plates.Diagnostics.Concat(forces.Diagnostics));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var reported = new List<SectionResistanceValue>();
            foreach (var cover in plates.Covers)
            {
                string name = cover.Cover.ToString();
                var group = forces.CoverGroups.Single(g => g.Cover == cover.Cover && g.Side == SpliceSide.Left);
                var plate = group.Plate; var grid = WeldedIBeamSplicePlates.Grid(d, group);
                var bolt = group.IsWeb ? p.Plates.Bolts.WebBolt : p.Plates.Bolts.FlangeBolt;
                // Preserve every pre-existing issue except the two class3 screens that
                // this explicit effective-width calculation replaces.
                issues.AddRange(cover.LongitudinalCompression.Diagnostics.Where(x =>
                    x != "COMPRESSED_OUTSTAND_OUTSIDE_CLASS3_STRIP_SCOPE" && x != "COMPRESSED_INTERNAL_STRIP_OUTSIDE_CLASS3_SCOPE")
                    .Select(x => name + ":" + x));
                issues.AddRange(cover.Sections.Diagnostics.Select(x => name + ":" + x));
                double centre = (group.TransverseMinimum + group.TransverseMaximum) / 2;
                double lo = group.TransverseMinimum - centre, hi = group.TransverseMaximum - centre;
                var ys = group.Distribution.Points.Select(x => x.Y - centre).Distinct().OrderBy(x => x).ToArray();
                if (ys.Length == 0) { issues.Add(name + ".NO_BOLT_LINE"); continue; }
                double a = plate.Width * plate.Thickness, w = plate.Thickness * plate.Width * plate.Width / 6;
                bool compressed = cover.Cuts.Any(c => -c.N / a + Math.Abs(c.M) / w > 0);
                double stress = compressed ? -plate.Fy : 0;
                var bands = new List<Band>();
                var left = EffectivePlateReduction.Outstand(ys[0] - lo, plate.Thickness, stress, plate.Fy);
                var right = EffectivePlateReduction.Outstand(hi - ys[ys.Length - 1], plate.Thickness, stress, plate.Fy);
                Add(ys[0] - left.EffectiveAtStart, ys[0]);
                Add(ys[ys.Length - 1], ys[ys.Length - 1] + right.EffectiveAtStart);
                Value(name + ".LeftOutstandRho", left.Rho, "1"); Value(name + ".RightOutstandRho", right.Rho, "1");
                Value(name + ".LeftOutstandEffectiveWidth", left.EffectiveAtStart, "mm");
                Value(name + ".RightOutstandEffectiveWidth", right.EffectiveAtStart, "mm");
                bool reduced = left.Rho < 1 || right.Rho < 1;
                for (int j = 0; j < ys.Length - 1; ++j)
                {
                    var inner = EffectivePlateReduction.InternalPlate(ys[j + 1] - ys[j], plate.Thickness, stress, stress, plate.Fy);
                    Add(ys[j], ys[j] + inner.EffectiveAtStart);
                    Add(ys[j + 1] - inner.EffectiveAtEnd, ys[j + 1]);
                    Value(name + ".Internal" + j + ".Rho", inner.Rho, "1");
                    reduced |= inner.Rho < 1;
                }
                if (!reduced)
                    foreach (var check in cover.LongitudinalCompression.Checks.Where(cover.IsCompressionShearBending))
                        checks.Add(check.WithId(name + "." + check.Id));
                bool reducedWithShear = reduced && cover.Cuts.Any(c => c.V != 0);
                if (reducedWithShear && mode == ShearMode.ZeroShear)
                    issues.Add(name + ".CLASS4_COMPRESSION_WITH_SHEAR_REQUIRES_INTERACTION");
                var effective = Properties(bands, plate.Thickness);
                // Deduct only the intersection of each real hole with retained steel.
                // Ineffective intervals and holes must not be counted twice.
                var netBands = bands;
                foreach (double y in ys) netBands = Subtract(netBands, y - bolt.HoleDiameter / 2, y + bolt.HoleDiameter / 2);
                var net = Properties(netBands, plate.Thickness);
                if (!(effective.Area > 0 && effective.Inertia > 0 && net.Area > 0 && net.Inertia > 0)
                    || !Finite(effective.Area + effective.Inertia + net.Area + net.Inertia))
                { issues.Add(name + ".INVALID_EFFECTIVE_NET_SECTION"); continue; }
                if (reducedWithShear && allowLowShear)
                {
                    // EN1993-1-5 7.1(1): low shear does not reduce normal resistance.
                    // Do not infer restraint from bolt pitch for shear buckling. The
                    // existing whole-width exclusion is mandatory here, even standalone.
                    double slenderness = plate.Width / plate.Thickness;
                    double slendernessLimit = 60 * Math.Sqrt(235 / plate.Fy);
                    double shearResistance = Math.Min(PlasticShear.Resistance(a, plate.Fy, 1.27, code.Plates.GammaM0),
                        net.Area * Math.Min(plate.Fy / code.Plates.GammaM0, plate.Fu / code.Plates.GammaM2) / (1.5 * Math.Sqrt(3)));
                    // Also bound eta=1 web shear resistance with the separately selected
                    // gammaM1. User EC parameter sets need not have gammaM1=gammaM0.
                    double shearBucklingReference = PlasticShear.Resistance(a, plate.Fy, p.Plates.Policy.GammaM1);
                    shearResistance = Math.Min(shearResistance, shearBucklingReference);
                    Value(name + ".LowShear.ShearBucklingReferenceResistance", shearBucklingReference, "N");
                    double maximumShear = cover.Cuts.Max(c => Math.Abs(c.V));
                    Value(name + ".LowShear.WholeWidthThicknessRatio", slenderness, "1");
                    Value(name + ".LowShear.WholeWidthLimit", slendernessLimit, "1");
                    Value(name + ".LowShear.ConservativeShearResistance", shearResistance, "N");
                    Value(name + ".LowShear.MaximumDemand", maximumShear, "N");
                    Value(name + ".LowShear.AdmittedLimit", .5 * shearResistance, "N");
                    if (slenderness > slendernessLimit) issues.Add(name + ".CLASS4_SHEAR_BUCKLING_NOT_EXCLUDED");
                    if (maximumShear > .5 * shearResistance) issues.Add(name + ".CLASS4_SHEAR_EXCEEDS_HALF_RESISTANCE");
                }
                double pitch = grid.MaximumLongitudinalGap, length = .6 * pitch;
                double q = effective.Area / a;
                double lambdaGross = length * Math.Sqrt(12 * plate.Fy / p.Plates.Policy.ElasticModulus) / (Math.PI * plate.Thickness);
                double lambda = Math.Sqrt(q) * lambdaGross;
                bool exempt = !reduced && pitch / plate.Thickness <= 9 * Math.Sqrt(235 / plate.Fy);
                double chi = exempt ? 1 : BucklingReductionArithmetic.AxialChi(BucklingReductionArithmetic.Phi(.49, lambda), lambda);
                double limit = exempt ? plate.Fy / code.Plates.GammaM0 : chi * plate.Fy / p.Plates.Policy.GammaM1;
                Value(name + ".HasCompression", compressed ? 1 : 0, "1");
                Reported(name + ".IsLocallyReduced", reduced ? 1 : 0, "1");
                Value(name + ".GrossArea", a, "mm2"); Reported(name + ".EffectiveArea", effective.Area, "mm2");
                Value(name + ".EffectiveCentroid", effective.Centroid, "mm"); Value(name + ".EffectiveInertia", effective.Inertia, "mm4");
                Reported(name + ".EffectiveNetArea", net.Area, "mm2"); Value(name + ".EffectiveNetCentroid", net.Centroid, "mm");
                Value(name + ".EffectiveNetInertia", net.Inertia, "mm4");
                Value(name + ".AreaRatio", q, "1"); Value(name + ".EffectiveLength", length, "mm");
                Value(name + ".GrossSlenderness", lambdaGross, "1"); Value(name + ".EffectiveSlenderness", lambda, "1");
                Reported(name + ".Chi", chi, "1"); Value(name + ".GrossWeakAxisNcr", Math.PI * Math.PI * p.Plates.Policy.ElasticModulus
                    * a * plate.Thickness * plate.Thickness / (12 * length * length), "N");
                double maxCompression = 0;
                int k = 0;
                foreach (var cut in cover.Cuts)
                {
                    // Transport M to the actual effective centroid before checking the
                    // extreme fibres; keep the physical bounding edges conservatively.
                    double ce = Math.Max(Math.Abs(lo - effective.Centroid), Math.Abs(hi - effective.Centroid));
                    double cn = Math.Max(Math.Abs(lo - net.Centroid), Math.Abs(hi - net.Centroid));
                    double compression = Math.Max(0, -cut.N / effective.Area
                        + Math.Abs(cut.M + effective.Centroid * cut.N) * ce / effective.Inertia);
                    double sigmaNet = Math.Abs(cut.N) / net.Area + Math.Abs(cut.M + net.Centroid * cut.N) * cn / net.Inertia;
                    double tau = 1.5 * Math.Abs(cut.V) / net.Area;
                    maxCompression = Math.Max(maxCompression, compression);
                    Check(name + ".Cut" + k++ + ".EffectiveNetElasticNVM", ElasticBoltGroup.Hypot(sigmaNet, Math.Sqrt(3) * tau),
                        Math.Min(plate.Fy / code.Plates.GammaM0, .9 * plate.Fu / code.Plates.GammaM2),
                        "EN1993-1-5:2006 4.4 uniform-compression effective widths; supplementary conservative elastic net-section check", "MPa");
                }
                Check(name + ".EffectiveCompressionYield", maxCompression, plate.Fy / code.Plates.GammaM0,
                    "EN1993-1-5:2006 4.4; uniform compression at fy over every plate element", "MPa");
                Check(name + ".EffectiveInterFastenerBuckling", maxCompression, limit,
                    "EN1993-1-8:2005 Table3.3 note2, Lcr=0.6p; EN1993-1-1:2005 6.3.1 class4 Aeff, gross Ncr, curve c", "MPa");
                Value(name + ".MaximumEffectiveCompression", maxCompression, "MPa");
                if (mode == ShearMode.ShearBuckling && cover.Cuts.Any(c => c.V != 0))
                {
                    // EN1993-1-5 5.2/5.3 on the whole-width panel hw=b: no transverse stiffeners (k_tau=5.34),
                    // non-rigid end post (the cover ends are free edges), no flanges (Vbf=0), eta=1 (5.1(2) is a
                    // national choice the splice code does not carry). The bolt forces are the elastic group
                    // solution, so no tension-field anchorage is available: chi<=1/lambda_w^2, i.e. tau<=tau_cr.
                    if (ys.Length < 2 || grid.MaximumLongitudinalGap > Math.Min(14 * plate.Thickness, 200))
                        issues.Add(name + ".SHEAR_PANEL_RESTRAINT_NOT_ESTABLISHED");
                    var panel = Ec3WebPanelShear.Calculate(plate.Width, plate.Thickness, plate.Fy, p.Plates.Policy.ElasticModulus,
                        code.Plates.GammaM0, p.Plates.Policy.GammaM1, 1);
                    double elasticCap = 1 / (panel.Slenderness * panel.Slenderness), chiW = Math.Min(panel.Chi, elasticCap);
                    double vbw = chiW * a * plate.Fy / (Math.Sqrt(3) * p.Plates.Policy.GammaM1);
                    Value(name + ".ShearBuckling.KTau", panel.KTau, "1"); Value(name + ".ShearBuckling.TauCritical", panel.TauCritical, "MPa");
                    Reported(name + ".ShearBuckling.LambdaW", panel.Slenderness, "1"); Value(name + ".ShearBuckling.ChiTable5_1", panel.Chi, "1");
                    Value(name + ".ShearBuckling.ElasticCriticalCap", elasticCap, "1"); Reported(name + ".ShearBuckling.ChiW", chiW, "1");
                    Value(name + ".ShearBuckling.Eta", 1, "1"); Reported(name + ".ShearBuckling.VbwRd", vbw, "N");
                    int j = 0;
                    foreach (var cut in cover.Cuts)
                    {
                        string id = name + ".Cut" + j++;
                        double eta3 = Math.Abs(cut.V) / vbw;
                        Check(id + ".ShearBuckling", Math.Abs(cut.V), vbw,
                            "EN1993-1-5:2006 5.2(1),5.3, Table5.1 non-rigid end post, eta=1, chi_w<=1/lambda_w^2; whole-width panel, k_tau=5.34", "N");
                        if (!(eta3 > .5)) continue; // 7.1(1): no reduction of the normal resistance
                        // 7.1(5) refers a wholly compressed plate to the reduced stress method of section10.
                        if (cut.N / a + Math.Abs(cut.M) / w < 0)
                        {
                            if (reduced || chiW < 1) issues.Add(name + ".HIGH_SHEAR_WHOLE_PLATE_COMPRESSED_REQUIRES_EN1993_1_5_SECTION10");
                            continue;
                        }
                        // eta1 from the effective elastic section (4.6) bounds the 7.1 ratio M/MN,Rd; Mf,Rd=0.
                        double ce = Math.Max(Math.Abs(lo - effective.Centroid), Math.Abs(hi - effective.Centroid));
                        double eta1 = (Math.Abs(cut.N) / effective.Area + Math.Abs(cut.M + effective.Centroid * cut.N) * ce / effective.Inertia)
                            / (plate.Fy / code.Plates.GammaM0);
                        Check(id + ".ShearNormalInteraction", eta1 + Math.Pow(2 * eta3 - 1, 2), 1,
                            "EN1993-1-5:2006 7.1(1),(5) with Mf,Rd=0; eta1 of the effective elastic section (4.6), conservative for MN,Rd", "1");
                    }
                }
                void Add(double x, double y) { if (y > x) bands.Add(new Band(x, y)); }
            }
            if (plates.Covers.Count != 8) issues.Add("EIGHT_COVER_ASSESSMENTS_REQUIRED");
            return new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, code,
                "Steel.Connection.WeldedIBeamSplice.EffectiveCompression",
                "Effective outstand/internal widths and inter-fastener compression of normal-hole bolted covers. "
                + (allowLowShear ? "Locally class4 compression admits shear <= half a conservative resistance on the effective net area, only with whole-width shear-buckling exclusion (EN1993-1-5 7.1(1)). "
                    : mode == ShearMode.ShearBuckling ? "Concomitant shear of any magnitude is verified with the EN1993-1-5 section5 resistance of the whole-width panel and, above half of it, the 7.1 interaction; a wholly compressed cut at high shear needs section10 and is unsupported. "
                    : "Locally class4 compression is admitted only at exactly zero shear in every cut. ")
                + "The separate shear-buckling screen, detailing, bolts, blocks and parent checks remain mandatory.",
                checks, trace, issues.Distinct(), Assumptions(mode == ShearMode.ShearBuckling), reported);
            void Value(string id, double x, string unit)
            { if (Finite(x)) trace.Add(new SectionResistanceValue(id, x, unit)); else issues.Add(id + ".NUMERIC_RANGE"); }
            void Reported(string id, double x, string unit)
            { Value(id, x, unit); if (Finite(x)) reported.Add(trace[trace.Count - 1]); }
            void Check(string id, double demand, double resistance, string reference, string unit)
            {
                if (!Finite(demand) || !Finite(resistance) || demand < 0 || resistance <= 0) issues.Add(id + ".NUMERIC_RANGE");
                else checks.Add(new ConnectionCheck(id, demand, resistance, reference, unit));
            }
        }
        private static readonly string[] BaseAssumptions = {
            "Reuses the common Steel/CompositeBridge EN1993-1-5 kernel; no second effective-width formula. Every compressed cover uses uniform compression at fy, even for a stress gradient: favourable tension zones are not credited.",
            "Table3.3 note2 refers outstand buckling to EN1993-1-1. Longitudinal fastener lines delimit the same outstands/internal elements as the previous class3 screen; full standard pitch and geometry checks remain mandatory.",
            "Normal holes are filled by the physical fasteners. Gross area/inertia determine Ncr; Aeff determines class4 slenderness and compressive stress. Net fracture additionally deducts holes from the retained intervals, with no double subtraction.",
            "All cuts are transported to the effective centroid. Gross bounding edges and maximum compressive stress give a conservative strip demand; no plastic redistribution is used.",
            "The low-shear extension uses a stricter nominal shear resistance: effective net area, peak elastic factor1.5, min(fy/gammaM0,fu/gammaM2), bounded by the existing gross resistance. Combined effective-net elastic stress is still checked, with no plastic redistribution or use of gross area to hide removed material.",
            "This is not a general buckling model for plates with empty cut-outs, shear-buckling-sensitive free plates or class4 at high shear. The opt-in method uses EN1993-1-5 7.1(1) as an explicit supplementary design rule under NTC2018 as well, retaining its factors: NTC 4.2.4.1.1 refers class4 effective properties to UNI EN1993-1-5, 4.2.4.1.3.4 class4 plate buckling to documents of proven validity, and Circolare2019 C4.2.4.1.3.4 cites UNI EN1993-1-5."
        };
        private static string[] Assumptions(bool shearBuckling) => !shearBuckling ? BaseAssumptions : BaseAssumptions.Concat(new[] {
            "EffectiveCompressionWithShearBuckling: each sheared cover is one EN1993-1-5 section5 panel of depth hw=b and thickness t, restrained by its outer fastener rows (at least two, Table3.3 gaps); interior fasteners are not credited as stiffeners, so k_tau=5.34. "
            + "The cover ends are free edges (non-rigid end post, Table5.1) and there are no flanges (Vbf,Rd=0). eta=1. The elastic group bolt forces provide no tension-field anchorage: chi_w is also limited to 1/lambda_w^2 (tau_cr).",
            "Every cut checks |V|<=Vbw,Rd. Above eta3=0.5 the 7.1(1) interaction eta1+(2 eta3-1)^2<=1 is checked with Mf,Rd=0 and eta1 from the effective elastic section, an upper bound of M/MN,Rd. "
            + "A cut without any tensile fibre at eta3>0.5 is referred by 7.1(5) to section10 and remains unsupported for class4 or buckling-reduced covers. Under NTC2018 the same EN1993-1-5 route is applied as the library's declared supplementary method, with NTC factors (eta=1 of NTC 4.2.4.1.2.4 eq4.2.27, which sends webs beyond 72eps/eta to 4.2.4.1.3.4; 4.2.4.1.3.4 refers class4 plate buckling to documents of proven validity; Circolare2019 C4.2.4.1.3.4 cites UNI EN1993-1-5)." }).ToArray();
        private readonly struct Band
        {
            internal double Start { get; } internal double End { get; }
            internal Band(double start, double end) { Start = start; End = end; }
        }
        private readonly struct Section
        {
            internal double Area { get; } internal double Centroid { get; } internal double Inertia { get; }
            internal Section(double area, double centroid, double inertia) { Area = area; Centroid = centroid; Inertia = inertia; }
        }
        private static Section Properties(IReadOnlyList<Band> bands, double t)
        {
            double a = bands.Sum(x => (x.End - x.Start) * t);
            if (!(a > 0)) return new Section(0, 0, 0);
            double y = bands.Sum(x => (x.End - x.Start) * t * (x.End + x.Start) / 2) / a;
            double i = bands.Sum(x => t * (Math.Pow(x.End - x.Start, 3) / 12
                + (x.End - x.Start) * Math.Pow((x.End + x.Start) / 2 - y, 2)));
            return new Section(a, y, i);
        }
        private static List<Band> Subtract(IEnumerable<Band> bands, double lo, double hi)
        {
            var result = new List<Band>();
            foreach (var band in bands)
            {
                if (band.Start >= hi || band.End <= lo) result.Add(band);
                else
                {
                    if (band.Start < lo) result.Add(new Band(band.Start, lo));
                    if (band.End > hi) result.Add(new Band(hi, band.End));
                }
            }
            return result;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
