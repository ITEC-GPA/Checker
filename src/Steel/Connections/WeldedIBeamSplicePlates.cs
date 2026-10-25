using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedIBeamSplicePlateInput
    {
        public IBeamSplicePlatePolicy Policy { get; }
        public WeldedIBeamSpliceBoltInput Bolts { get; }
        public WeldedIBeamSplicePlateInput(IBeamSplicePlatePolicy policy, WeldedIBeamSpliceBoltInput bolts)
        { Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts)); }
    }

    /// <summary>One limiting section of a continuous cover, N positive in tension.
    /// M is about the actual cover centroid. ColumnLimit 0/1 means before/after
    /// the complete bolt column; -1 denotes the butt plane or a free edge.</summary>
    public sealed class SpliceCoverCut
    {
        public double X { get; }
        public int ColumnLimit { get; }
        public double N { get; }
        public double V { get; }
        public double M { get; }
        internal SpliceCoverCut(double x, int limit, double n, double v, double m)
        { X = x; ColumnLimit = limit; N = n; V = v; M = m; }
    }

    public sealed class SpliceCoverPlateAssessment
    {
        public WeldedSpliceCover Cover { get; }
        public IReadOnlyList<SpliceCoverCut> Cuts { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> Sections { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> LongitudinalCompression { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> AxialBlockTearing { get; }
        internal SpliceCoverPlateAssessment(WeldedSpliceCover cover, IEnumerable<SpliceCoverCut> cuts,
            ConnectionVerification<WeldedIBeamSplicePlateInput> sections,
            ConnectionVerification<WeldedIBeamSplicePlateInput> compression,
            ConnectionVerification<WeldedIBeamSplicePlateInput> block)
        { Cover = cover; Cuts = Array.AsReadOnly(cuts.ToArray()); Sections = sections; LongitudinalCompression = compression; AxialBlockTearing = block; }
    }

    public sealed class WeldedIBeamSplicePlateResult
    {
        public WeldedIBeamSplicePlateInput Input { get; }
        public IReadOnlyList<SpliceCoverPlateAssessment> Covers { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool CanQualifyWholeJoint => false;
        public bool HasFailedComponent => Covers.Any(c => new[] { c.Sections, c.LongitudinalCompression, c.AxialBlockTearing }.Any(r => r.Status == ConnectionStatus.Failed));
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "COMBINED_NET_SECTION_FRACTURE_AND_CATEGORY_C_NET_PLASTIC_NVM", "GENERAL_NVM_BLOCK_TEARING",
            "PLATE_SHEAR_AND_LATERAL_STABILITY", "PARENT_MEMBER_SECTION_AND_STABILITY",
            "LOCAL_SECONDARY_BENDING_AND_FORCE_INTRODUCTION" });
        internal WeldedIBeamSplicePlateResult(WeldedIBeamSplicePlateInput input, IEnumerable<SpliceCoverPlateAssessment> covers, IEnumerable<string> issues)
        { Input = input; Covers = Array.AsReadOnly(covers.ToArray()); Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray()); }
    }

    /// <summary>Section and longitudinal strip COMPONENTS of all eight covers.
    /// The exact concentrated-force envelope retains local transverse forces
    /// in the inner strips. No complete plate stability or NVM fracture claim.</summary>
    public static class WeldedIBeamSplicePlates
    {
        public static WeldedIBeamSplicePlateResult Verify(WeldedIBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var bolts = WeldedIBeamSpliceBolts.Verify(p.Bolts);
            var issues = new List<string>(bolts.ComponentAssessment.Diagnostics);
            var covers = new List<SpliceCoverPlateAssessment>();
            if (!SameCode(p.Policy.Code, p.Bolts.Code)) issues.Add("PLATE_BOLT_POLICY_MISMATCH");
            if (issues.Count != 0) return new WeldedIBeamSplicePlateResult(p, covers, issues);
            foreach (var group in bolts.Forces.CoverGroups.GroupBy(g => g.Cover))
            {
                var left = group.Single(g => g.Side == SpliceSide.Left);
                var plate = left.Plate; var grid = Grid(p.Bolts.Detail, left);
                var bolt = left.IsWeb ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                double end = End(p.Bolts.Detail, left), half = grid.FirstBoltDistance + grid.LongitudinalSpan + end;
                double centre = (left.TransverseMinimum + left.TransverseMaximum) / 2;
                var all = group.SelectMany(g => g.Distribution.Forces).ToArray();
                var localIssues = new List<string>();
                var cuts = Envelope(all, centre, half, localIssues);
                int rows = left.Distribution.Points.Select(b => b.Y).Distinct().Count();
                double area = plate.Width * plate.Thickness, net = (plate.Width - rows * bolt.HoleDiameter) * plate.Thickness;
                double w = plate.Thickness * plate.Width * plate.Width / 6;
                if (!new[] { area, net, w }.All(v => Finite(v) && v > 0)) localIssues.Add("NONPOSITIVE_OR_NONFINITE_PLATE_SECTION");
                var trace = new List<SectionResistanceValue>();
                Value(trace, "GrossArea", area, "mm2"); Value(trace, "NetArea", net, "mm2"); Value(trace, "ElasticModulus", w, "mm3");
                Value(trace, "HolesPerSection", rows, "1"); Value(trace, "TransverseCentroid", centre, "mm");
                var sectionChecks = new List<ConnectionCheck>(); var compressionChecks = new List<ConnectionCheck>();
                var compressionIssues = new List<string>(localIssues); var compressionTrace = new List<SectionResistanceValue>();
                var factors = p.Policy.Code.Plates;
                double maxCompression = 0;
                if (localIssues.Count == 0)
                {
                    double nr = area * plate.Fy / factors.GammaM0, nt = .9 * net * plate.Fu / factors.GammaM2;
                    double vg = area * plate.Fy / (1.27 * Math.Sqrt(3) * factors.GammaM0);
                    double vn = net * plate.Fu / (Math.Sqrt(3) * factors.GammaM2);
                    double mr = w * plate.Fy / factors.GammaM0;
                    foreach (var c in cuts.Where(c => c.X > -half && c.X < half))
                    {
                        string id = "Cut" + cuts.IndexOf(c);
                        double vr = Math.Abs(c.V) / Math.Min(vg, vn);
                        double rho = vr <= .5 ? 0 : vr >= 1 ? 1 : Math.Pow(2 * vr - 1, 2);
                        double normalCapacity = c.N >= 0 ? Math.Min(nr, nt) : nr;
                        double remainingM = Math.Max(0, 1 - Math.Abs(c.N) / normalCapacity) * (1 - rho) * mr;
                        double compression = Math.Max(0, -c.N / area + Math.Abs(c.M) / w);
                        if (!new[] { nr, nt, vg, vn, mr, remainingM, compression }.All(Finite))
                        { localIssues.Add("SECTION_RESISTANCE_NUMERIC_RANGE"); break; }
                        Add(sectionChecks, id + ".GrossTension", Math.Max(0, c.N), nr);
                        Add(sectionChecks, id + ".NetAxialTension", Math.Max(0, c.N), nt);
                        Add(sectionChecks, id + ".GrossCompression", Math.Max(0, -c.N), nr);
                        Add(sectionChecks, id + ".GrossShear", Math.Abs(c.V), vg);
                        Add(sectionChecks, id + ".NetShear", Math.Abs(c.V), vn);
                        Add(sectionChecks, id + ".AxialShearBending", Math.Abs(c.M), remainingM, "Nmm");
                        maxCompression = Math.Max(maxCompression, compression);
                    }
                    double pitch = Math.Max(grid.LongitudinalPitch, 2 * grid.FirstBoltDistance);
                    double eps = Math.Sqrt(235 / plate.Fy), length = .6 * pitch;
                    double lambda = length * Math.Sqrt(12 * plate.Fy / p.Policy.ElasticModulus) / (Math.PI * plate.Thickness);
                    bool exempt = pitch / plate.Thickness <= 9 * eps;
                    double chi = exempt ? 1 : BucklingReductionArithmetic.AxialChi(BucklingReductionArithmetic.Phi(.49, lambda), lambda);
                    double rb = exempt ? plate.Fy / factors.GammaM0 : chi * plate.Fy / p.Policy.GammaM1;
                    double edge = Math.Max(left.Distribution.Points.Min(b => b.Y) - left.TransverseMinimum,
                        left.TransverseMaximum - left.Distribution.Points.Max(b => b.Y));
                    if (maxCompression > 0)
                    {
                        if (edge / plate.Thickness > 14 * eps) compressionIssues.Add("COMPRESSED_OUTSTAND_OUTSIDE_CLASS3_STRIP_SCOPE");
                        if (rows > 1 && grid.TransversePitch / plate.Thickness > 42 * eps) compressionIssues.Add("COMPRESSED_INTERNAL_STRIP_OUTSIDE_CLASS3_SCOPE");
                    }
                    if (!new[] { rb, lambda, chi }.All(Finite)) compressionIssues.Add("STRIP_BUCKLING_NUMERIC_RANGE");
                    else
                    {
                        compressionChecks.Add(new ConnectionCheck("MaximumCompression.Yield", maxCompression, plate.Fy / factors.GammaM0, NormalReference(p), "MPa"));
                        compressionChecks.Add(new ConnectionCheck("MaximumCompression.InterFastenerBuckling", maxCompression, rb,
                            "SCI P398 p57; EN1993-1-8:2005 Table3.3 note2; EC solid-strip curve c with declared gammaM1", "MPa"));
                    }
                    Value(compressionTrace, "MaximumUnrestrainedPitch", pitch, "mm"); Value(compressionTrace, "EffectiveLength", length, "mm");
                    Value(compressionTrace, "Slenderness", lambda, "1"); Value(compressionTrace, "Chi", chi, "1");
                    Value(compressionTrace, "Exempt", exempt ? 1 : 0, "1"); Value(compressionTrace, "MaximumCompression", maxCompression, "MPa");
                }
                compressionIssues.AddRange(localIssues);
                covers.Add(new SpliceCoverPlateAssessment(left.Cover, cuts,
                    Result(p, "Sections." + left.Cover, "Gross elastic N-V-M envelope and separate net axial/shear components; NOT combined net-section fracture or plate stability.", sectionChecks, trace, localIssues),
                    Result(p, "LongitudinalCompression." + left.Cover, "Longitudinal filled-fastener strip compression only; NOT complete shear/lateral plate buckling.", compressionChecks, compressionTrace, compressionIssues),
                    AxialBlocks(p, group.ToArray(), localIssues)));

                void Add(IList<ConnectionCheck> list, string id, double demand, double resistance, string unit = "N")
                    => list.Add(new ConnectionCheck(id, demand, resistance, NormalReference(p), unit));
            }
            return new WeldedIBeamSplicePlateResult(p, covers, issues);
        }

        private static ConnectionVerification<WeldedIBeamSplicePlateInput> AxialBlocks(WeldedIBeamSplicePlateInput p,
            WeldedSpliceCoverForceGroup[] groups, IEnumerable<string> initialIssues)
        {
            var issues = new List<string>(initialIssues); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var left = groups.Single(g => g.Side == SpliceSide.Left); var plate = left.Plate;
            var grid = Grid(p.Bolts.Detail, left); var bolt = left.IsWeb ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
            if (grid.LongitudinalRows > 20 || left.Distribution.Points.Select(b => b.Y).Distinct().Count() > 16)
                issues.Add("AXIAL_BLOCK_ENUMERATION_LIMIT_20_BY_16");
            // Scope is determined by exact input actions/geometry, never an absolute force cutoff.
            foreach (var g in groups)
            {
                var a = g.Distribution.InputActions;
                double centre = (g.TransverseMinimum + g.TransverseMaximum) / 2;
                if (a.Fy != 0 || a.Mz != 0 || (a.Fx != 0 && (a.ApplicationY != centre
                    || g.Distribution.Points.Average(b => b.Y) != centre)))
                    issues.Add("BLOCK_NVM_OR_ECCENTRIC_FORCE_DISTRIBUTION_NOT_QUALIFIED");
            }
            if (issues.Count == 0)
            {
                foreach (var g in groups)
                {
                    // Left/right external vectors oppose; the physical cover tension is common.
                    double signedN = (g.Side == SpliceSide.Left ? 1 : -1) * g.Distribution.InputActions.Fx;
                    double perBolt = Math.Max(0, signedN) / g.Distribution.Forces.Count;
                    double[] ys = g.Distribution.Points.Select(b => b.Y).Distinct().OrderBy(y => y).ToArray();
                    double end = End(p.Bolts.Detail, g);
                    int path = 0;
                    // Every prefix from the free longitudinal end; no undocumented omission
                    // of a short block when it is more critical than the full group.
                    for (int countX = 1; countX <= grid.LongitudinalRows; countX++)
                    {
                        double shearLength = end + (countX - 1) * grid.LongitudinalPitch - (countX - .5) * bolt.HoleDiameter;
                        for (int first = 0; first < ys.Length; first++)
                        for (int last = first; last < ys.Length; last++)
                        {
                            int countY = last - first + 1;
                            if (countY > 1) Path(ys[last] - ys[first] - (countY - 1) * bolt.HoleDiameter,
                                2 * shearLength, countX * countY * perBolt, "Internal");
                            if (first == 0) Path(ys[last] - g.TransverseMinimum - (countY - .5) * bolt.HoleDiameter,
                                shearLength, countX * countY * perBolt, "LowerEdge");
                            if (last == ys.Length - 1) Path(g.TransverseMaximum - ys[first] - (countY - .5) * bolt.HoleDiameter,
                                shearLength, countX * countY * perBolt, "UpperEdge");
                        }
                    }
                    void Path(double nt, double nv, double demand, string mode)
                    {
                        string id = g.Side + ".Path" + (++path) + "." + mode;
                        double ant = nt * plate.Thickness, anv = nv * plate.Thickness;
                        // Always use the reduced tensile contribution of the eccentric EC expression;
                        // no concentrated, variable-direction NVM field is approved by this choice.
                        double resistance = .5 * ant * plate.Fu / p.Policy.Code.Plates.GammaM2
                            + anv * plate.Fy / (Math.Sqrt(3) * p.Policy.Code.Plates.GammaM0);
                        if (!(ant > 0) || !(anv > 0) || !Finite(resistance)) { issues.Add(id + ".INVALID_BLOCK_LIGAMENT"); return; }
                        checks.Add(new ConnectionCheck(id, demand, resistance,
                            "EN1993-1-8:2005 3.10.2(3) eq3.10; rectangular axial paths, conservative 0.5 tension term; NTC factors when selected"));
                        Value(trace, id + ".Ant", ant, "mm2"); Value(trace, id + ".Anv", anv, "mm2");
                    }
                }
            }
            return Result(p, "AxialBlocks." + left.Cover,
                "Rectangular cover block paths under centred axial loading only; all free-edge prefixes, reduced tensile contribution. No combined N-V-M, parent-web block or joint qualification.", checks, trace, issues);
        }

        internal static List<SpliceCoverCut> Envelope(ElasticBoltForce[] forces, double centre, double half, IList<string> issues)
        {
            var columns = forces.GroupBy(f => f.Position.X).ToDictionary(g => g.Key, g => g.ToArray());
            var xs = columns.Keys.Concat(new[] { -half, 0d, half }).Distinct().OrderBy(x => x);
            var n = new Sum(); var v = new Sum(); var m = new Sum();
            double ns = 0, vs = 0, ms = 0; var result = new List<SpliceCoverCut>();
            foreach (double x in xs)
            {
                if (!columns.TryGetValue(x, out var column)) { Capture(x, -1); continue; }
                Capture(x, 0);
                foreach (var f in column)
                {
                    n.Add(f.Fx); v.Add(f.Fy); m.Add(f.Position.X * f.Fy - (f.Position.Y - centre) * f.Fx);
                    ns += Math.Abs(f.Fx); vs += Math.Abs(f.Fy);
                    ms += Math.Abs(f.Position.X * f.Fy) + Math.Abs((f.Position.Y - centre) * f.Fx);
                }
                Capture(x, 1);
            }
            if (result.Count == 0) { issues.Add("NO_FINITE_COVER_CUT"); return result; }
            var last = result.Last();
            if (!Balanced(last.N, ns) || !Balanced(last.V, vs) || !Balanced(last.M, ms + half * vs)) issues.Add("COVER_FREE_EDGE_EQUILIBRIUM_FAILED");
            return result;
            void Capture(double x, int limit)
            {
                double moment = m.Value - x * v.Value;
                if (!new[] { n.Value, v.Value, moment }.All(Finite)) issues.Add("CUT_NUMERIC_RANGE");
                else result.Add(new SpliceCoverCut(x, limit, n.Value, v.Value, moment));
            }
        }
        internal static SpliceBoltLayout Grid(WeldedIBeamSpliceForceInput d, WeldedSpliceCoverForceGroup g) => g.IsWeb ? d.WebBolts : Top(g.Cover) ? d.Top.Bolts : d.Bottom.Bolts;
        internal static double End(WeldedIBeamSpliceForceInput d, WeldedSpliceCoverForceGroup g) => g.IsWeb ? d.WebCoverEnd : Top(g.Cover) ? d.Top.EndDistance : d.Bottom.EndDistance;
        internal static bool Top(WeldedSpliceCover c) => c == WeldedSpliceCover.TopOuter || c == WeldedSpliceCover.TopInnerNegative || c == WeldedSpliceCover.TopInnerPositive;
        internal static bool SameCode(ConnectionDesignCode a, ConnectionDesignCode b) => a.Plates.Standard == b.Plates.Standard && a.Plates.GammaM0 == b.Plates.GammaM0 && a.Plates.GammaM2 == b.Plates.GammaM2 && a.GammaM2Connection == b.GammaM2Connection;
        internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Balanced(double residual, double scale) => Finite(residual) && Finite(scale) && Math.Abs(residual) <= ElasticBoltGroup.EquilibriumRelativeTolerance * scale;
        private static void Value(IList<SectionResistanceValue> values, string name, double value, string unit) { if (Finite(value)) values.Add(new SectionResistanceValue(name, value, unit)); }
        private static string NormalReference(WeldedIBeamSplicePlateInput p) => "SCI P398 pp56-59; EN1993-1-1:2005 6.2.3,6.2.6,6.2.9.2,6.2.10" + (p.Policy.Code.IsNtc ? "; supplementary model with NTC2018 factors" : "");
        private static ConnectionVerification<WeldedIBeamSplicePlateInput> Result(WeldedIBeamSplicePlateInput p, string method, string scope,
            IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace, IEnumerable<string> issues)
            => new ConnectionVerification<WeldedIBeamSplicePlateInput>(p, p.Policy.Code, "Steel.Connection.WeldedIBeamSplice." + method, scope, checks, trace, issues.Distinct(), new[] {
                "Each cover is assessed separately with its own actual centre-plane force field. Both limits of each full column are retained; N,V constant and M affine between columns, so endpoint envelopes are exact for this beam model.",
                "Net area deducts every aligned hole at one transverse section. Net axial and net shear checks do not establish mixed-stress rupture or category-C net-plastic N-V-M resistance.",
                "SCI gross elastic bending interaction uses rho=0 below half shear resistance; no residual bending capacity beyond full shear/axial resistance. Compression holes are filled.",
                "Longitudinal strip curve c, Lcr=0.6 times maximum pitch including central spacing; class-3 screens do not prove continuous support at discrete bolt rows or close shear/lateral buckling.",
                "No plate or joint qualification follows from passing these components. Loads at bolt holes are beam-model resultants, not local stress concentrations or contact/prying solutions." });
        private sealed class Sum { private double sum, correction; internal double Value => sum; internal void Add(double x) { double y = x - correction, next = sum + y; correction = (next - sum) - y; sum = next; } }
    }
}
